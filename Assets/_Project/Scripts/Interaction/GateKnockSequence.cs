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
    /// 대사는 SubtitleView(월드 공간 Canvas)로 띄운다 — OnGUI는 헤드셋에 렌더링되지 않는다.
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
        [Tooltip("문을 열어주는 마름(비우면 캐릭터 연출 없이 대사·문만 진행)")]
        [SerializeField] private MareumController _mareum;
        [Tooltip("맞이하고 사랑방으로 앞장서는 복동(甲). 비워 두는 것이 보통이다. 앞장서는 신호는 마름이 준다 — 손님이 문간을 넘는 " +
                 "것을 보고 나서(MareumController ▸ On Player Passed → LeadInside). " +
                 "여기에 걸면 대사 순번에서, 곧 손님이 아직 문 밖에 선 채로 앞장서 버린다")]
        [SerializeField] private BokdongController _bokdong;
        [SerializeField] private Line[] _lines;
        [Tooltip("이 순번 대사에서 마름이 안에 여쭈러 간다(0부터). 이 줄은 문이 " +
                 "움직이기 시작할 때까지 걸려 있다 — 기다리는 대목이 여기다")]
        [SerializeField] private int _askAtLine = 2;

        // 문이 열리는 동안 걸리는 대사는 여쭈러 간 줄 바로 다음이다. 따로 적어 둘 값이
        // 아니다 — 두 곳에 적으면 둘이 어긋날 수 있고, 어긋나면 말이 문을 앞지른다.
        [Tooltip("이 순번 대사에서 복동이 앞장서 걷기 시작(기본=마지막 줄)")]
        [SerializeField] private int _bokdongLeadAtLine = 4;
        [Tooltip("각 대사 자동 넘김 시간(초). Space로 즉시 넘김")]
        [SerializeField] private float _lineDuration = 3.5f;

        private bool _started, _done;
        private int _index;
        private float _timer;

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
            if (_mareum != null) _mareum.BeginGreet();   // 마름: 졸다 일어남
            TryOpenAt(0);
            ShowCurrentLine();
        }

        private void Update()
        {
            if (!_started || _done) return;
            _timer += Time.deltaTime;

            bool advance = _timer >= _lineDuration;
#if ENABLE_INPUT_SYSTEM
            if (!Typing.Now && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                advance = true;
#endif
            // <b>여쭈러 간 줄은 시계로 넘기지 않는다.</b>
            //
            // "잠시 기다리시오, 주인께 여쭙고 오리다" 하고 안으로 들어갔는데 3.5초가
            // 지났다고 다음 줄로 넘어가 버리면, 주인의 대답이 문보다 한참 앞서 뜬다.
            // 반대로 문이 먼저 열려 버리면 허락 없이 열린 꼴이 된다.
            //
            // 그래서 이 줄은 <b>문짝이 실제로 움직이기 시작하는 그 프레임</b>에 넘어간다.
            // 그러면 다음 줄 — "허허, 누추하나 드시오" — 이 문이 열리는 동안 걸린다.
            // 마름의 동작이 길어지든 짧아지든 말과 문이 어긋나지 않는다.
            if (_index == _askAtLine && _door != null)
                advance = _door.IsOpen || _timer >= _lineDuration * 6f;   // 뒤엣것은 만일의 빗장

            if (advance) Next();
        }

        private void Next()
        {
            _index++;
            _timer = 0f;
            if (_index >= _lines.Length) { _done = true; SubtitleView.Hide(); return; }
            TryOpenAt(_index);
            ShowCurrentLine();
        }

        private void TryOpenAt(int i)
        {
            if (i == _askAtLine && _door != null)
            {
                if (_mareum != null)
                {
                    // 문은 <b>마름이</b> 연다. 여기서 같이 열면 안 된다 —
                    // 마름은 대꾸를 하고, 안에 여쭈러 갔다 오고, 문 여는 동작의 손이
                    // 문에 닿는 프레임에 가서야 문짝을 민다. 그런데 이 줄에서 곧바로
                    // 열어 버리니, 마름이 아직 돌아서지도 않았는데 문이 저절로 열렸다.
                    // 두 곳에서 같은 문을 열고 있었던 것이 '타이밍이 안 맞던' 까닭이다.
                    _mareum.OpenDoorThenStepAside();
                }
                else
                {
                    _door.Unlock();     // 마름이 없으면 이 줄에서 그냥 열린다
                    _door.Open();
                }
            }
            if (i == _bokdongLeadAtLine && _bokdong != null)
                _bokdong.LeadInside();                                  // 복동: 사랑방으로 앞장서 걷기
        }

        /// <summary>지금 줄을 월드 자막으로 띄운다. 끝났으면 자막을 내린다.</summary>
        private void ShowCurrentLine()
        {
            if (!_started || _done || _index >= _lines.Length) { SubtitleView.Hide(); return; }
            var line = _lines[_index];
            SubtitleView.Show(line.speaker, line.text, "(계속)");
        }

        private static Line[] DefaultLines() => new[]
        {
            new Line { speaker = "마름", text = "이 야심한 밤에… 뉘시오?" },
            new Line { speaker = "과객(나)",  text = "지나던 과객이오. 하룻밤 신세 좀 집시다." },
            new Line { speaker = "마름", text = "…잠시 기다리시오. 주인께 여쭙고 오리다." },
            // 마지막 줄이 걸린 채 문이 열린다. <b>뒤에 안내를 한 줄 더 붙이지 않는다</b> —
            // "대문이 열렸다, 따라 들어가자"는 눈앞에 벌어지는 일을 글로 한 번 더 적는
            // 것이라, 보고 있는 사람에게는 군말이다. 문이 열리는 것을 보면 따라 들어간다.
            new Line { speaker = "옹덕구(甲)", text = "허허, 누추하나 드시오. 사랑에 자리를 봐드리리다." },
        };
    }
}
