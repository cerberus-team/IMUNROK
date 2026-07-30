using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 잠긴 문(DoorController)을 두드렸을 때 재생되는 대화 시퀀스.
    ///  두드림 → 대사 자동 진행(Space로 즉시 넘김) → 정해진 대사에서 문 열림.
    ///
    /// 카메라를 뺏지 않는다(플레이어는 계속 1인칭 조종 = VR 안전). 대사만 화면에 흐르고
    /// 정해진 순번에서 문이 스르륵 열린다. 甲이 나온 뒤부터는 플레이어가 직접 걸어 들어가면 됨.
    ///
    /// 붙이는 법: 대문(DoorController 있는 오브젝트)에 이 컴포넌트를 추가.
    ///   · DoorController의 Locked = ✔ (두드려야 열리게)
    ///   · _door는 비워두면 같은 오브젝트에서 자동으로 찾음.
    ///   · 대사(_lines)는 비워두면 옹고집 기본 대사가 들어감. 인스펙터에서 수정 가능.
    /// </summary>
    [RequireComponent(typeof(DoorController))]
    public class GateKnockSequence : MonoBehaviour
    {
        [System.Serializable]
        public class Line
        {
            public string speaker;
            [TextArea] public string text;
        }

        [SerializeField] private DoorController _door;
        [SerializeField] private Line[] _lines;
        [Tooltip("이 순번 대사에서 문이 열림(0부터)")]
        [SerializeField] private int _openAtLine = 3;
        [Tooltip("각 대사 자동 넘김 시간(초). Space로 즉시 넘김")]
        [SerializeField] private float _lineDuration = 3.5f;

        private bool _started, _done;
        private int _index;
        private float _timer;
        private GUIStyle _nameStyle, _textStyle, _hintStyle;

        private void Awake()
        {
            if (_door == null) _door = GetComponent<DoorController>();
            if (_lines == null || _lines.Length == 0) _lines = DefaultLines();
        }

        private void Start()
        {
            if (_door != null) _door.OnKnock.AddListener(Knock);
        }

        /// <summary>DoorController.OnKnock에서 호출(잠긴 문을 두드림).</summary>
        public void Knock()
        {
            if (_started || _done) return;
            _started = true;
            _index = 0;
            _timer = 0f;
            TryOpenAt(0);
        }

        private void Update()
        {
            if (!_started || _done) return;
            _timer += Time.deltaTime;

            bool advance = _timer >= _lineDuration;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                advance = true;
#endif
            if (advance) Next();
        }

        private void Next()
        {
            _index++;
            _timer = 0f;
            if (_index >= _lines.Length) { _done = true; return; }
            TryOpenAt(_index);
        }

        private void TryOpenAt(int i)
        {
            if (i == _openAtLine && _door != null)
            {
                _door.Unlock();
                _door.Open();
            }
        }

        private void OnGUI()
        {
            if (!_started || _done || _index >= _lines.Length) return;
            EnsureStyles();
            var line = _lines[_index];

            const float w = 720f, h = 132f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - h - 40f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none);
            if (!string.IsNullOrEmpty(line.speaker))
                GUI.Label(new Rect(x + 24, y + 14, w - 48, 28), line.speaker, _nameStyle);
            GUI.Label(new Rect(x + 24, y + 46, w - 48, 60), line.text, _textStyle);
            GUI.Label(new Rect(x + 24, y + h - 26, w - 48, 22), "(Space : 계속)", _hintStyle);
        }

        private void EnsureStyles()
        {
            if (_textStyle != null) return;
            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, wordWrap = true,
                normal = { textColor = Color.white }
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(1f, 1f, 1f, 0.55f) }
            };
        }

        private static Line[] DefaultLines() => new[]
        {
            new Line { speaker = "늙은 하인", text = "이 야심한 밤에… 뉘시오?" },
            new Line { speaker = "과객(나)",  text = "지나던 과객이오. 하룻밤 신세 좀 집시다." },
            new Line { speaker = "늙은 하인", text = "…잠시 기다리시오. 주인께 여쭙고 오리다." },
            new Line { speaker = "옹덕구(甲)", text = "허허, 누추하나 드시오. 사랑에 자리를 봐드리리다." },
            new Line { speaker = "",          text = "대문이 열렸다. 甲을 따라 안으로 들어가자." },
        };
    }
}
