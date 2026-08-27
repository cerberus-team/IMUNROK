using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>관아 서리</b> — 2막의 첫 사람. 어디가 어디인지 일러 주고 물러난다.
    ///
    /// <b>왜 사람이 일러 주어야 하나.</b> 출도하고 나면 눈앞에 관아가 통째로 열린다.
    /// 문서고와 동헌이 마당 하나를 사이에 두고 마주 서 있는데, 처음 온 사람에게는
    /// 그냥 기와집 둘이다. 어느 쪽이 무엇 하는 데인지 <b>글로 띄워 알리면</b> 그건
    /// 안내판이지 관아가 아니다 — 관아에는 원래 사람이 나와서 길을 일러 준다.
    ///
    /// 그리고 이 자리는 2막이 1막과 무엇이 다른지 말해 주는 자리이기도 하다.
    /// 1막에서는 담을 넘어 몰래 뒤졌다. 2막에서는 <b>서리가 문을 열어 준다</b>.
    /// 같은 일을 해도 이제 떳떳하게 한다는 것을 하인의 인사 한 마디가 말한다.
    ///
    /// <b>하는 일 둘</b>
    ///   · 말이 끝나면 상태창 맨 윗줄에 <b>두 갈래</b>를 남긴다 —
    ///     "찾을 것은 문서고, 물을 것은 동헌".
    ///   · 같은 오브젝트에 <see cref="InterrogationController"/> 가 있으면 <b>잠금을 푼다</b>.
    ///     인사가 끝난 뒤에야 붙잡고 이것저것 물을 수 있다.
    ///
    /// 붙이는 곳: 서리(사람). 모델은 나중에 와도 된다 — 몸이 없어도 말은 나온다.
    /// </summary>
    public class GwanaUsher : MonoBehaviour
    {
        [System.Serializable]
        public class Line
        {
            public string 말한이 = "서리";
            [TextArea(2, 4)] public string 말 = "";
            [Tooltip("아랫줄 작은 글씨")] public string 아랫줄 = "";
            [Tooltip("이 줄이 걸려 있는 시간(초). 0이면 아래 기본값")]
            public float 초 = 0f;
            [Tooltip("이 줄이 뜨는 순간 한 번")]
            public UnityEngine.Events.UnityEvent 그때;
        }

        [Header("언제 말을 거나")]
        [Tooltip("이 거리(m) 안에 들어서면 다가와 인사한다. 0이면 씬이 열리자마자")]
        [SerializeField] private float _greetWithin = 12f;
        [Tooltip("씬이 열리고 이만큼(초)은 그냥 둔다 — 화면이 자리를 잡기 전에 말이 먼저 뜨면 못 읽는다")]
        [SerializeField] private float _delay = 1.2f;

        [Header("말")]
        [SerializeField] private Line[] _lines;
        [Tooltip("한 줄이 걸려 있는 시간(초). Space 로 바로 넘긴다")]
        [SerializeField] private float _lineSeconds = 4.2f;

        [Header("끝난 뒤")]
        [Tooltip("상태창 맨 윗줄에 남길 두 갈래. 비우면 아무것도 안 남긴다")]
        [TextArea(2, 3)]
        [SerializeField] private string _objective = "찾을 것은 *문서고*, 물을 것은 *동헌*";
        [Tooltip("인사가 끝나면 이 사람에게 말을 걸 수 있게 푼다(같은 오브젝트의 심문)")]
        [SerializeField] private bool _unlockSelf = true;
        [SerializeField] private UnityEngine.Events.UnityEvent _onDone;

        private bool _started, _done;
        private int _i;
        private float _t;

        private void Awake()
        {
            if (_lines == null || _lines.Length == 0) _lines = Default();
        }

        /// <summary>밖에서도 시작시킬 수 있다(문을 지나는 순간 따위).</summary>
        public void Greet()
        {
            if (_started || _done) return;
            _started = true;
            _i = 0; _t = 0f;
            Show();
        }

        private void Update()
        {
            if (_done) return;

            if (!_started)
            {
                _t += Time.deltaTime;
                if (_t < _delay) return;
                var cam = Camera.main;
                bool near = _greetWithin <= 0f || cam == null ||
                            Vector3.Distance(cam.transform.position, transform.position) <= _greetWithin;
                if (near) Greet();
                return;
            }

            _t += Time.deltaTime;
            float len = _lines[_i].초 > 0f ? _lines[_i].초 : _lineSeconds;
            bool advance = _t >= len;
#if ENABLE_INPUT_SYSTEM
            if (!Typing.Now && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) advance = true;
#endif
            if (advance) Next();
        }

        private void Next()
        {
            _i++;
            _t = 0f;
            if (_i < _lines.Length) { Show(); return; }

            _done = true;
            if (SubtitleView.IsShowing) SubtitleView.Hide();
            if (!string.IsNullOrEmpty(_objective)) StatusPanel.Set("목표", 0, _objective);
            if (_unlockSelf)
            {
                var talk = GetComponent<InterrogationController>();
                if (talk != null) talk.Unlock();
            }
            _onDone?.Invoke();
        }

        private void Show()
        {
            var l = _lines[_i];
            SubtitleView.Show(l.말한이, l.말, string.IsNullOrEmpty(l.아랫줄) ? Controls.Skip : l.아랫줄);
            l.그때?.Invoke();
        }

        /// <summary>
        /// 기본 대사. 서리는 <b>마패를 보고 나서야</b> 말투가 바뀐다 —
        /// 어사가 왔다고 말해 주는 것보다 하급 관리의 허리가 굽는 편이 빠르다.
        /// </summary>
        private static Line[] Default()
        {
            return new[]
            {
                new Line { 말한이 = "서리", 말 = "게 뉘시오. 관아에 함부로 드는 법이 어디…", 아랫줄 = null },
                new Line { 말한이 = "서리", 말 = "…아이고. *마패*올시다. 어사또 나리, 몰라뵈었습니다.", 아랫줄 = null },
                new Line { 말한이 = "서리", 말 = "이 아래가 *문서고*입니다. 이 고을 문서는 죄 저 안에 들어 있습지요.\n호적이며 대장이며, 찾으실 것이 있거든 저리로 드십시오.", 아랫줄 = null, 초 = 5.5f },
                new Line { 말한이 = "서리", 말 = "저 위가 *동헌*이올시다. 부르실 사람이 있거든 마루에 오르시면\n뜰에 대령시키겠습니다.", 아랫줄 = null, 초 = 5f },
                new Line { 말한이 = "서리", 말 = "찾으실 것은 문서고에서, 물으실 것은 동헌에서 —\n그리 아시면 되겠습니다.", 아랫줄 = "" },
            };
        }
    }
}
