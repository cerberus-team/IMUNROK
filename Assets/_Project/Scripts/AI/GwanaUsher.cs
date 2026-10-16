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

        [Header("붙들려 왔을 때 — 앞에 붙는 말")]
        [Tooltip("1막에서 <b>붙들려</b> 넘어온 경우에만 먼저 나온다" +
                 "(<see cref=\"GameState.CaughtInStealth\"/>). 제 발로 출도한 사람에게는 " +
                 "이 줄이 아예 없다 — 마패를 이미 내보이고 온 사람에게 「게 뉘시오」는 어림없다")]
        [SerializeField] private Line[] _caughtLines;

        [Tooltip("붙들려 온 경우, 그 말 끝에 <b>마패를 손에 들 때까지</b> 기다린다. " +
                 "들면 아래 공손한 말로 넘어간다")]
        [SerializeField] private bool _awaitWarrant = true;
        [Tooltip("기다리는 동안 걸어 두는 한 줄")]
        [TextArea(2, 3)]
        [SerializeField] private string _awaitLine = "…품에 든 것을 꺼내 보인다.";
        [Tooltip("아랫줄 안내. 도구띠에서 마패를 고르라는 말")]
        [SerializeField] private string _awaitHint = "(Q 로 *마패* 를 손에 든다)";
        [Tooltip("도구띠에서 마패의 이름")]
        [SerializeField] private string _warrantToolId = "mapae";
        [Tooltip("마패를 내보이는 순간 한 번")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onWarrantShown;

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

        private bool _caught;      // 붙들려 온 길인가
        private bool _waiting;     // 마패를 기다리는 중인가

        private void Awake()
        {
            if (_lines == null || _lines.Length == 0) _lines = Default();
            if (_caughtLines == null || _caughtLines.Length == 0) _caughtLines = CaughtDefault();

            _caught = GameState.Instance != null && GameState.Instance.CaughtInStealth;
            if (!_caught) return;

            // <b>앞에 붙인다.</b> 뒤엣말(공손한 인사)은 그대로 쓴다 — 마패를 보고 나면
            // 하는 말이 같아지는 것이 맞다. 달라지는 것은 <b>그 앞</b>뿐이다.
            var all = new System.Collections.Generic.List<Line>(_caughtLines);
            all.AddRange(_lines);
            _waitAt = _caughtLines.Length;
            _lines = all.ToArray();
        }

        /// <summary>이 줄 앞에서 마패를 기다린다. 붙들려 온 길에서만 0 보다 크다.</summary>
        private int _waitAt = -1;

        /// <summary>마패를 손에 들었나.</summary>
        private bool Holding
        {
            get { return ToolbeltHud.SelectedToolId == _warrantToolId; }
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

            // <b>마패를 꺼낼 때까지 기다린다.</b> 붙들려 온 길에서는 「자네는 누구길래」
            // 다음이 그냥 넘어가면 안 된다 — 그 물음에 답하는 것이 이 게임의 마패다.
            if (_waiting)
            {
                if (!Holding) return;
                _waiting = false;
                if (GameState.Instance != null) GameState.Instance.RevealIdentity(true);
                _onWarrantShown?.Invoke();
                _t = 0f;
                Show();
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
            if (_awaitWarrant && _i == _waitAt && !Holding)
            {
                _waiting = true;
                SubtitleView.Show(_lines[_i - 1].말한이, _awaitLine, _awaitHint);
                return;
            }
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
        /// 마패를 본 <b>뒤</b>의 말. 두 길이 여기서 하나로 합쳐진다 —
        /// 제 발로 출도해 온 사람도, 붙들려 왔다가 패를 꺼낸 사람도 이 말을 듣는다.
        /// 말투는 아전의 것이다: <b>격식은 갖추되 짧다</b>.
        /// 「-습지요」 같은 굽은 말씨는 서리가 아니라 촌로의 것이라 걷었다.
        ///
        /// <b>왜 쩔쩔매나.</b> 암행어사는 <b>기별 없이</b> 오는 것이 본디다 — 기별을 하면
        /// 그 사이에 장부를 고친다. 그러니 이 아침은 서리에게 「예정에 없던 일」이고,
        /// 아전으로서는 그것이 당연한 줄 알면서도 낯이 선다. 굽신거리되 겁먹지는 않는
        /// 사람이라야, 이 고을이 아직 아주 썩지는 않았다는 것도 함께 보인다.
        /// </summary>
        private static Line[] Default()
        {
            return new[]
            {
                new Line { 말한이 = "서리", 말 = "어이구, *어사또* 나리시옵니까.\n어찌 말도 없이 오시고…", 아랫줄 = null, 초 = 4.2f },
                // 제 말이 무엄했음을 스스로 알아채고 곧바로 주워 담는다.
                new Line { 말한이 = "서리", 말 = "아이, 당연한 것이옵지요. 기별을 하고 오시면\n그게 어디 암행이겠습니까. 소인이 망령이 났습니다.", 아랫줄 = null, 초 = 5.2f },
                new Line { 말한이 = "서리", 말 = "소인은 이 고을 서리올시다.\n하문하실 일이 있거든 무엇이든 이르십시오.", 아랫줄 = null, 초 = 4.5f },
                // 문서고와 동헌은 마당을 사이에 두고 <b>마주</b> 서 있다 — 위아래가 아니다.
                // <b>오른편이다.</b> 여태 「왼편」이라 일러 두었는데, 어사가 대문으로 들어서
                // 동헌을 보고 서면 문서고는 오른손 쪽(−z)에 있다. 길을 알려 주는 사람이
                // 반대쪽을 가리키면 그 다음 한 마디까지 못 믿게 된다.
                new Line { 말한이 = "서리", 말 = "오른편이 *문서고*입니다. 호적이며 대장이며,\n이 고을 문서는 모두 그 안에 있습니다.", 아랫줄 = null, 초 = 5.5f },
                new Line { 말한이 = "서리", 말 = "맞은편이 *동헌*이올시다. 뜰에 다섯을 대령시켜 두었으니,\n마루에 오르시어 이름을 부르시면 한 사람씩 올리겠습니다.", 아랫줄 = null, 초 = 6f },
                // 끝줄은 앞말을 되풀지 않는다(그 몫은 상태줄이 한다). 대신 <b>규칙</b>을 하나 이른다.
                new Line { 말한이 = "서리", 말 = "…다만 뜰에서는 입을 열지 않을 것입니다.\n물으실 일은 동헌에서 물으셔야 합니다.", 아랫줄 = "", 초 = 5f },
            };
        }

        /// <summary>
        /// <b>붙들려 온 길</b>의 첫마디. 이 사람은 아직 어사를 보지 못했다 —
        /// 밤중에 남의 집을 뒤지다 끌려온 <b>웬 사내</b>를 보고 있을 뿐이다.
        ///
        /// 그래서 말이 <b>낮춤말</b>이고, 그 낮춤말이 곧 이 대목의 값이다.
        /// 「자네」라 불리고 나서 꺼내는 마패라야 마패가 무게를 가진다 —
        /// 제 발로 출도한 사람은 이 맛을 못 본다. <b>밤을 그르친 사람만 얻는 장면</b>이다.
        ///
        /// 이 뒤에서 <see cref="_awaitWarrant"/> 가 손을 기다린다. 패를 꺼내야
        /// <see cref="Default"/> 의 「어이구, 어사또 나리시옵니까」로 넘어간다.
        /// </summary>
        private static Line[] CaughtDefault()
        {
            return new[]
            {
                new Line { 말한이 = "서리", 말 = "이자인가. 밤새 옹가네 안방을 뒤졌다는 것이.", 아랫줄 = null, 초 = 4.2f },
                new Line { 말한이 = "서리", 말 = "자네는 누구길래 남의 집을 그리 뒤지고 있는 게야.\n관을 우습게 아는가.", 아랫줄 = null, 초 = 5.2f },
            };
        }
    }
}
