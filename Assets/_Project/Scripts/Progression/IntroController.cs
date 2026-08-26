using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 장면 1 — 어전(도입). 플레이어는 왕 앞에 부복한 채로 시작한다.
    /// 왕은 모습을 보이지 않고 목소리와 자막으로 세 미제 사건을 내린다.
    /// 대사가 끝나면 봉서 세 통이 하나씩 밀려나오고, 받으면 조사청으로 간다.
    ///
    /// <b>봉서는 세 통을 한꺼번에 받는다.</b> 예전에는 하나를 골라 집으면 그 사건이
    /// 곧장 시작됐는데, 그러면 첫 조사청 방문이 할 일 없는 통로가 된다. 게다가
    /// 마지막에 세 사건을 모두 복명하는 구조인데 하나만 받아 나가는 그림은 앞뒤가
    /// 안 맞는다. 고르는 일은 조사청 사건판이 맡는다.
    ///
    /// <b>자막은 SubtitleView 로 그린다.</b> 예전 OnGUI 는 헤드셋에 아예 렌더링되지
    /// 않는다 — 모니터로 보면 멀쩡한데 쓰고 보면 왕이 말없이 서 있다.
    ///
    /// <b>건너뛰기는 한 번 본 뒤부터만 열린다.</b> 어명은 내가 왜 여기 있는지를 말해
    /// 주는 유일한 자리이고, 신분을 감춘다는 전제도 여기서만 선다(1막에서 사람들이
    /// 어사또라 부르지 않는 이유가 여기 있다). 첫 회에 건너뛰면 그걸 통째로 놓친다.
    /// 대신 다시 시작하거나 시연할 때 매번 앉아 있을 수는 없으니, 끝까지 한 번 본
    /// 기록이 남으면 그때부터 열어 준다.
    /// 누르는 방식은 <b>길게 누르기</b>다 — VR 컨트롤러는 스치기만 해도 눌리는데,
    /// 실수로 프롤로그를 날리면 되돌릴 방법이 없다.
    /// </summary>
    public class IntroController : MonoBehaviour
    {
        [Header("진행")]
        [SerializeField] private float _secondsPerLine = 4.5f;
        [SerializeField] private string _speakerName = "왕";

        [Header("왕의 대사(인스펙터에서 수정 가능)")]
        [TextArea]
        [SerializeField]
        private string[] _kingLines =
        {
            "어사(御史)는 부복하라.",
            "괴이하다는 말로 닫힌 문서들이다.",
            "도술이라 하고, 신이 데려갔다 하고, 꽃이 되살렸다 한다.",
            "나는 그 말을 믿지 않는다.",
            "봉서와 마패, 유척을 내리니 — 가서 무엇이 있었는지 기록해 오라.",
        };

        [Tooltip("봉서가 다 굴러온 뒤에 띄울 안내. <b>비우면 아무것도 안 뜬다</b> — " +
                 "셋이 발치에 굴러와 멎는 그림이 이미 그 말을 하고 있다")]
        [SerializeField] private string _pickPrompt = "";

        [Header("문서 등장 연출")]
        [SerializeField] private IntroDocument[] _documents;
        [Tooltip("문서 사이 등장 시차(초)")]
        [SerializeField] private float _docStagger = 0.4f;
        [SerializeField] private float _docSlideDuration = 0.6f;
        [Tooltip("문서가 왕 쪽(뒤)에서 밀려나오는 거리")]
        [SerializeField] private float _docFromDistance = 1.2f;

        [Header("받은 뒤")]
        [Tooltip("봉서를 받을 때 한 줄. 비우면 그냥 넘어간다")]
        [SerializeField] private string _takeLine = "…삼가 받잡겠나이다.";
        [SerializeField] private string _takeSpeaker = "나";
        [Tooltip("받은 뒤 조사청으로 넘어가기까지(초)")]
        [SerializeField] private float _leaveAfter = 1.8f;
        [Header("어디로 가는지")]
        [Tooltip("봉서를 맡은 뒤, 눈을 감기 직전에 한 마디. 비우면 안 뜬다")]
        [SerializeField] private string _goingLine = "조사청으로 든다.";
        [SerializeField] private string _goingSpeaker = "";
        [Tooltip("그 한 마디를 읽을 틈(초). 떠나는 시간이 그만큼 늘어난다")]
        [SerializeField] private float _goingHold = 1.6f;

        [SerializeField] private string _hubSceneName = "HubScene";

        [Header("건너뛰기")]
        // <b>꾹 누르기를 걷어냈다.</b> 눌러도 한참 아무 일이 없다가 갑자기 되는 방식이라
        // 처음 온 사람에게는 고장난 것과 구별이 안 되고, 무엇보다 <b>건너뛸 수 있다는
        // 사실 자체가 안 보였다</b> — 자막 아랫줄에 "꾹 누르기"라고 적어 두어도 그것을
        // 읽고 시험해 볼 사람은 이미 건너뛸 마음이 있는 사람뿐이다.
        //
        // 이제 화면 오른쪽 위에 <b>단추가 서서히 배어 나온다</b>(CornerButton).
        // 볼 사람은 그대로 보고, 넘길 사람은 눈에 보이는 것을 누른다.
        [Tooltip("단추에 적힐 말")]
        [SerializeField] private string _skipLabel = "튜토리얼 넘기기";
        [Tooltip("어명이 시작되고 이만큼(초) 뒤에 단추가 배어 나오기 시작한다. " +
                 "곧바로 띄우면 연출이 아니라 기다리는 화면이 된다")]
        [SerializeField] private float _skipAppearsAfter = 3f;
        [Tooltip("배어 나오는 데 걸리는 시간(초)")]
        [SerializeField] private float _skipFadeIn = 1.4f;
        [Tooltip("켜면 <b>끝까지 한 번 본 뒤부터만</b> 단추가 뜬다. 어명은 내가 왜 여기 " +
                 "있는지를 말해 주는 유일한 자리라 한때 그렇게 막아 두었는데, 눈에 보이는 " +
                 "단추를 누르는 것은 실수로 되는 일이 아니므로 이제 열어 둔다")]
        [SerializeField] private bool _skipOnlyAfterSeen = false;

        /// <summary>어명을 끝까지 본 적이 있는가. 기기에 남는다.</summary>
        private const string SeenKey = "이문록_어명_봄";

        private int _index;
        private float _timer;
        private bool _speechDone;
        private bool _docsRevealed;
        private bool _taken;
        private bool _everPicked;   // 봉서를 한 번이라도 집어 봤는가
        private float _holdTimer;

        private static bool SeenBefore
        {
            get { return PlayerPrefs.GetInt(SeenKey, 0) == 1; }
            set { PlayerPrefs.SetInt(SeenKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private bool CanSkip => !_skipOnlyAfterSeen || SeenBefore;
        private float _skipTimer;

        private void Start()
        {
            _index = 0;
            _timer = 0f;
            _skipTimer = 0f;
            ShowLine();
        }

        private void Update()
        {
            if (_speechDone) return;

            RaiseSkipButton();

            _timer += Time.deltaTime;
            if (_timer >= _secondsPerLine || AdvancePressed())
                Next();
        }

        /// <summary>
        /// 몇 마디가 지나면 오른쪽 위에 <b>넘기기</b>가 배어 나온다.
        ///
        /// 곧바로 띄우지 않는 까닭: 어명이 시작되자마자 "넘기기"가 떠 있으면 그것은
        /// 연출이 아니라 <b>기다리는 화면</b>이 된다. 세 셈쯤 뒤에 나타나면, 볼 사람은
        /// 이미 첫 마디에 들어가 있고 넘길 사람은 그때쯤 손이 움직인다.
        /// </summary>
        private void RaiseSkipButton()
        {
            if (!CanSkip || CornerButton.Up) return;
            _skipTimer += Time.deltaTime;
            if (_skipTimer < _skipAppearsAfter) return;
            CornerButton.Show(_skipLabel, EndSpeech, _skipFadeIn);
        }

        private void Next()
        {
            _timer = 0f;
            _index++;
            if (_index >= _kingLines.Length) { EndSpeech(); return; }
            ShowLine();
        }

        private void ShowLine()
        {
            if (_kingLines == null || _kingLines.Length == 0) { EndSpeech(); return; }
            string line = _kingLines[Mathf.Clamp(_index, 0, _kingLines.Length - 1)];
            SubtitleView.Show(_speakerName, line, HintText());
        }

        private string HintText()
        {
            return "(다음 — 누르기)";
        }

        private void EndSpeech()
        {
            if (_speechDone) return;
            _speechDone = true;
            CornerButton.Hide();
            _index = Mathf.Max(0, _kingLines.Length - 1);

            // 끝까지 들었든 건너뛰었든, 이 지점에 닿았으면 본 것으로 친다.
            SeenBefore = true;

            ShowPickPrompt("(봉서를 가리켜 집는다)");
            RevealDocuments();
        }

        /// <summary>봉서 셋을 시차를 두고 등장시키고 집을 수 있게 만든다.</summary>
        private void RevealDocuments()
        {
            if (_docsRevealed) return;
            _docsRevealed = true;

            if (_documents == null || _documents.Length == 0)
            {
                Debug.LogWarning("[IntroController] 연결된 봉서가 없습니다.", this);
                return;
            }

            for (int i = 0; i < _documents.Length; i++)
                if (_documents[i] != null)
                    _documents[i].PlaySlideIn(i * _docStagger, _docSlideDuration, _docFromDistance);
        }

        /// <summary>
        /// 어느 봉서를 지금 펼쳐 읽고 있는지 알린다. 나머지는 도로 내려놓는다 —
        /// 둘이 한꺼번에 눈앞에 떠 있으면 무엇을 고른 것인지 알 수 없다.
        /// </summary>
        /// <summary>
        /// 아무 봉서도 가리키지 않게 되면 "집어 보라" 안내를 도로 띄운다.
        /// 하나라도 눈앞에 펼쳐 읽는 중이면 띄우지 않는다 — 그때는 읽는 것이 먼저다.
        /// </summary>
        public void RestorePickPrompt()
        {
            if (_taken || !_speechDone) return;

            // 한 번이라도 집어 본 사람에게는 다시 알리지 않는다. 그 안내는 무엇을
            // 하라는 것인지 모를 때를 위한 것이지, 아는 사람 앞을 계속 가릴 이유가 없다.
            if (_everPicked) return;

            if (_documents != null)
                foreach (var d in _documents)
                    if (d != null && d.IsReading) return;
            ShowPickPrompt("(가리켜 누르기)");
        }

        /// <summary>
        /// 집으라는 안내를 띄운다 — <b>적을 것이 있을 때만</b>.
        ///
        /// 안내를 비워 두면 그냥 <c>Show("", "", …)</c> 가 되어, 글 없는 자막판이
        /// 어전 한복판에 덩그러니 걸린다. 안내를 안 쓰기로 했으면 판까지 걷어야 한다.
        /// 봉서 셋이 발치로 굴러와 멎는 그림 자체가 이미 "집으라"는 말이다.
        /// </summary>
        private void ShowPickPrompt(string hint)
        {
            if (string.IsNullOrEmpty(_pickPrompt)) { SubtitleView.Hide(); return; }
            SubtitleView.Show("", _pickPrompt, hint);
        }

        public void NowReading(IntroDocument open)
        {
            _everPicked = true;
            SubtitleView.Hide();
            if (_documents == null) return;
            foreach (var d in _documents)
                if (d != null && d != open) d.Lower();
        }

        /// <summary>
        /// 봉서 하나를 골라 받았다. 고른 사건은 <see cref="IntroDocument"/> 가 이미
        /// 진행중으로 표시했고, 여기서는 받는 말 한 마디와 조사청으로 넘어가는 일만 한다.
        /// </summary>
        public void TakeChosen(CaseId chosen)
        {
            if (_taken) return;
            _taken = true;
            _takenCase = chosen;

            // 고르지 않은 둘은 사건판에 회색으로 남는다. 나중에 아무 때나 집으면 된다.
            foreach (var d in _documents)
                if (d != null && d.CaseId != chosen) d.Freeze();

            if (!string.IsNullOrEmpty(_takeLine)) SubtitleView.Show(_takeSpeaker, _takeLine);
            else SubtitleView.Hide();

            // 어디로 가는지 한 마디. 여태 여기서 곧장 캄캄해졌다 낯선 마당에서
            // 떴는데, 그러면 화면이 <b>바뀐 것</b>이지 <b>간 것</b>이 아니다.
            // 갈 곳을 듣고 나서 눈을 감아야 옮겨 간 것이 된다.
            if (!string.IsNullOrEmpty(_goingLine))
                Invoke(nameof(SayGoing), Mathf.Max(0.1f, _leaveAfter) * 0.55f);

            Invoke(nameof(LeaveForHub), Mathf.Max(0.1f, _leaveAfter) + _goingHold);
        }

        private void SayGoing()
        {
            if (_taken) SubtitleView.Show(_goingSpeaker, _goingLine, "");
        }

        [Tooltip("맡은 봉서로 화면을 덮으며 넘어가는 데 걸리는 시간(초). " +
                 "0 이면 예전처럼 그냥 캄캄해졌다 뜬다")]
        [SerializeField] private float _coverSeconds = 0.75f;

        private void LeaveForHub()
        {
            SubtitleView.Hide();

            if (string.IsNullOrEmpty(_hubSceneName) || !Application.CanStreamedLevelBeLoaded(_hubSceneName))
            {
                Debug.LogWarning($"[IntroController] 조사청 씬('{_hubSceneName}')을 찾을 수 없습니다. " +
                                 "File ▸ Build Profiles 의 씬 목록을 확인하세요.", this);
                return;
            }

            // <b>맡은 봉서로 화면을 덮으며 넘어간다.</b>
            //
            // 여태는 그냥 캄캄해졌다 떴다. 그 검은 막은 아무것도 아니라서, 화면이
            // <b>바뀐 것</b>이지 내가 <b>들고 간 것</b>이 아니었다. 손에 쥔 종이가
            // 시야를 덮으며 넘어가면 그 봉서가 조사청까지 따라온 것이 된다 —
            // 도착해서 손에 봉서가 있는 것이 그제야 자연스럽다.
            IntroDocument taken = null;
            if (_documents != null)
                foreach (var d in _documents)
                    if (d != null && d.CaseId == _takenCase && d.IsReading) taken = d;

            if (taken != null && _coverSeconds > 0.01f)
            {
                taken.CoverScreen(_coverSeconds, () => SceneManager.LoadScene(_hubSceneName));
                return;
            }

            // 덮을 것이 없으면(펼치지 않고 맡았거나 값이 0) 예전 길로 간다.
            // 눈앞에 들어 올린 두루마리는 검은 막보다 앞에 있어, 그냥 두면
            // 캄캄해진 화면 위에 그것만 남아 떠 있다.
            if (_documents != null)
                foreach (var d in _documents)
                    if (d != null) d.HideNow();
            ScreenFade.Blink(0.45f, 0.5f, () => SceneManager.LoadScene(_hubSceneName));
        }

        /// <summary>어느 사건을 맡았나 — 화면을 덮을 봉서를 고를 때 쓴다.</summary>
        private CaseId _takenCase;

        /// <summary>다음 줄로 넘기려고 눌렀는가(한 번 누름).</summary>
        private bool AdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool space = kb != null && kb.spaceKey.wasPressedThisFrame;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            return space || click;
#else
            return false;
#endif
        }

        /// <summary>누르고 있는가(건너뛰기 판정용).</summary>
        private bool AdvanceHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool space = kb != null && kb.spaceKey.isPressed;
            bool click = mouse != null && mouse.leftButton.isPressed;
            return space || click;
#else
            return false;
#endif
        }

        /// <summary>기록을 지운다 — 처음 보는 것처럼 다시 시험하고 싶을 때.</summary>
        [ContextMenu("어명 본 기록 지우기")]
        private void ForgetSeen()
        {
            PlayerPrefs.DeleteKey(SeenKey);
            PlayerPrefs.Save();
            Debug.Log("[IntroController] 어명 본 기록을 지웠습니다 — 다음 실행에서 건너뛸 수 없습니다.");
        }
    }
}
