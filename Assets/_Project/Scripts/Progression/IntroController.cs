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
    /// <b>고른 봉서의 사건으로 곧장 간다.</b> (2026-08-27 되돌림)
    ///
    /// 한동안 세 통을 한꺼번에 받아 조사청으로 보냈다. 「첫 조사청 방문이 할 일 없는
    /// 통로가 된다」는 것이 그 까닭이었는데, 그것은 <b>내가 잘못 짚은 것</b>이었다.
    /// 애초 설계가 어전에서 하나를 골라 그 사건으로 드는 그림이었다.
    ///
    /// 조사청은 <b>사건을 마치고 돌아오는 자리</b>다. 그러니 통로가 될 일이 없다 —
    /// 첫 사건을 끝내고 돌아왔을 때 비로소 남은 봉서 둘이 거기 놓여 있다.
    /// 고르지 않은 둘은 <see cref="IntroDocument.Freeze"/> 로 얼려 두므로
    /// 사건판이 그것을 그대로 이어받는다.
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
        [Tooltip("봉서를 맡은 뒤, 눈을 감기 직전에 한 마디. 비우면 안 뜬다. " +
                 "<b>갈 곳을 밝히지 않는다</b> — 봉서마다 가는 데가 다르므로 여기서 " +
                 "한 곳을 대면 셋 중 둘에게 거짓말이 된다")]
        [SerializeField] private string _goingLine = "길을 나선다.";
        [SerializeField] private string _goingSpeaker = "";
        [Tooltip("그 한 마디를 읽을 틈(초). 떠나는 시간이 그만큼 늘어난다")]
        [SerializeField] private float _goingHold = 1.6f;

        [SerializeField] private string _hubSceneName = "HubScene";

        [Tooltip("봉서를 안 고르고 조사청으로 드는 단추에 적힐 말. <b>비우면 그 단추가 안 뜬다</b>")]
        [SerializeField] private string _hubLabel = "사건은 나중에 · 조사청으로";

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
            RaiseHubButton();
        }

        /// <summary>
        /// <b>사건을 안 고르고 조사청으로 드는 길.</b>
        ///
        /// 여태 어전을 빠져나가는 길은 봉서를 집는 것 하나뿐이었다. 그런데 이 자리는
        /// 「어느 사건을 맡을까」를 고르는 자리이지 「반드시 지금 고르라」는 자리가
        /// 아니다 — 셋을 다 펼쳐 읽어 보고도 정하지 못할 수 있고, 조사청을 먼저
        /// 둘러보고 싶을 수도 있다. 고르지 않은 봉서는 어차피 사건판에 남으므로
        /// 나중에 아무 때나 집으면 된다.
        ///
        /// 자리는 <b>아래 가운데</b>다(<see cref="CornerButton.Below"/>). 귀퉁이에
        /// 두면 「빠져나가는 길」로 보이는데, 이것은 빠져나가는 길이 아니라
        /// <b>넷째 선택지</b>다 — 봉서 셋과 같은 줄에 서야 함께 견줘진다.
        /// </summary>
        private void RaiseHubButton() => UpdateHubButton();

        /// <summary>사건을 안 맡은 채 조사청으로 든다.</summary>
        public void GoToHubWithoutCase()
        {
            if (_taken) return;
            _taken = true;

            CornerButton.Hide();
            SubtitleView.Hide();

            // 발치의 봉서는 걷는다 — 화면을 덮을 것이 없으니 그냥 두면 캄캄해진
            // 화면 위에 그것만 남아 떠 있다(맡고 갈 때와 같은 까닭이다).
            if (_documents != null)
                foreach (var d in _documents)
                    if (d != null) d.HideNow();

            if (string.IsNullOrEmpty(_hubSceneName) || !Application.CanStreamedLevelBeLoaded(_hubSceneName))
            {
                Debug.LogWarning($"[IntroController] 조사청 씬('{_hubSceneName}')을 빌드 목록에서 " +
                                 "못 찾았습니다. File ▸ Build Profiles 를 확인하세요.", this);
                _taken = false;
                return;
            }

            ScreenFade.Blink(0.45f, 0.5f, () => SceneManager.LoadScene(_hubSceneName));
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

            // <b>단추는 안내와 따로 논다.</b> 아래 <c>_everPicked</c> 로 돌아 나가는 것은
            // 「집어 보라」는 <b>안내</b>이지 「조사청으로」가 아니다. 한동안 둘을 한 줄에
            // 묶어 두어서, 봉서를 한 번 집어 본 사람에게는 도로 내려놓아도 단추가
            // 영영 안 돌아왔다.
            // 손에서 놓았으면 그 표를 지운다. 여기서만 지운다 —
            // <see cref="NowReading"/> 직후에는 <c>Lift()</c> 가 아직이라 IsReading 이
            // 거짓이고, 그때 지우면 방금 집은 것을 안 집은 것으로 세게 된다.
            if (_reading != null && !_reading.IsReading) _reading = null;
            UpdateHubButton();

            // 한 번이라도 집어 본 사람에게는 다시 알리지 않는다. 그 안내는 무엇을
            // 하라는 것인지 모를 때를 위한 것이지, 아는 사람 앞을 계속 가릴 이유가 없다.
            if (_everPicked) return;

            if (_documents != null)
                foreach (var d in _documents)
                    if (d != null && d.IsReading) return;
            ShowPickPrompt("(가리켜 누르기)");
        }

        /// <summary>
        /// <b>「조사청으로」가 지금 서 있어야 하는가</b> — 그 답이 <b>바뀔 때만</b> 손댄다.
        ///
        /// 여태 세우고 걷는 일을 자리마다 따로 불렀다. 그런데 <see cref="RestorePickPrompt"/>
        /// 는 봉서에서 눈을 뗄 때마다 불린다 — 셋 위를 훑기만 해도 단추가 걷혔다 섰다
        /// 하며 <b>깜빡였다</b>. <c>CornerButton.Show</c> 는 서 있던 것을 걷고 새로 세우면서
        /// 배어 나오기를 처음부터 다시 하므로, 같은 값으로 다시 부르는 것이 곧 깜빡임이다.
        ///
        /// 그래서 <b>있어야 하는가</b>만 셈하고, 지금 모습과 다를 때만 세우거나 걷는다.
        /// </summary>
        private void UpdateHubButton()
        {
            bool want = _speechDone && !_taken && !string.IsNullOrEmpty(_hubLabel) && !AnyReading();
            if (want == _hubUp) return;
            _hubUp = want;
            if (want) CornerButton.Show(_hubLabel, GoToHubWithoutCase, _skipFadeIn, CornerButton.Below);
            else CornerButton.Hide();
        }

        /// <summary>
        /// 봉서 하나라도 눈앞에 펼쳐 들고 있는가.
        ///
        /// <c>_reading</c> 을 따로 두는 까닭: <see cref="NowReading"/> 은 <c>Lift()</c> <b>앞에</b>
        /// 불린다(다른 봉서를 먼저 내려놓아야 하므로). 그 순간에는 방금 집은 것조차
        /// <c>IsReading</c> 이 아직 거짓이라, 이것만 보면 단추가 안 걷힌다.
        /// </summary>
        private bool AnyReading()
        {
            if (_reading != null) return true;
            if (_documents == null) return false;
            foreach (var d in _documents) if (d != null && d.IsReading) return true;
            return false;
        }

        /// <summary>방금 집어 펼치려는 봉서. 위 참조.</summary>
        private IntroDocument _reading;

        /// <summary>지금 「조사청으로」가 서 있는가. 우리가 세운 것만 센다.</summary>
        private bool _hubUp;

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
            _reading = open;

            // <b>봉서를 펼쳐 든 동안에는 「조사청으로」를 걷는다.</b> 읽고 있는 것은
            // 「이 사건을 맡을까」를 재는 일이고, 그 앞에 「사건은 나중에」가 같이
            // 떠 있으면 무엇을 묻는 화면인지 흐려진다. 도로 내려놓으면 다시 선다.
            UpdateHubButton();
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

            UpdateHubButton();        // 「조사청으로」는 고르기 전까지만 서 있다

            // 고르지 않은 둘은 사건판에 회색으로 남는다. 나중에 아무 때나 집으면 된다.
            // 고른 하나는 <b>잠근다</b> — 받잡겠다 해 놓고 도로 물릴 수 있으면 그 말이 헛말이다.
            foreach (var d in _documents)
                if (d != null) { if (d.CaseId != chosen) d.Freeze(); else d.Seal(); }

            if (!string.IsNullOrEmpty(_takeLine)) SubtitleView.Show(_takeSpeaker, _takeLine);
            else SubtitleView.Hide();

            // 어디로 가는지 한 마디. 여태 여기서 곧장 캄캄해졌다 낯선 마당에서
            // 떴는데, 그러면 화면이 <b>바뀐 것</b>이지 <b>간 것</b>이 아니다.
            // 갈 곳을 듣고 나서 눈을 감아야 옮겨 간 것이 된다.
            //
            // <b>받는 말이 다 읽히고 나서</b> 다음 말을 얹는다. 여태 <c>_leaveAfter</c> 의
            // 0.55 배(=0.99초) 만에 갈아치웠는데, 「…삼가 받잡겠나이다.」는 열 자다 —
            // 눈이 닿기도 전에 다른 말로 바뀌니 <b>한 번 깜빡이고 만</b> 꼴이었다.
            // 이제 그 값을 그대로 쓴다(1.8초). 떠나는 때는 그만큼 뒤로 밀리지 않는다 —
            // 어차피 <c>_goingHold</c> 를 더해 세기 때문이다.
            if (!string.IsNullOrEmpty(_goingLine))
                Invoke(nameof(SayGoing), Mathf.Max(0.1f, _leaveAfter));

            Invoke(nameof(LeaveForHub), Mathf.Max(0.1f, _leaveAfter) + _goingHold);
        }

        private void SayGoing()
        {
            if (_taken) SubtitleView.Show(_goingSpeaker, _goingLine, "");
        }

        [Tooltip("눈이 감기는 데 걸리는 시간(초). 다 감긴 뒤에야 사건 씬으로 들어간다")]
        [SerializeField] private float _coverSeconds = 0.75f;

        [Header("사건 씬 이름 — 봉서를 집으면 여기로 곧장 간다")]
        [Tooltip("제1사건 옹고집")] [SerializeField] private string _case1Scene = "Onggojip";
        [Tooltip("제2사건 서천")]   [SerializeField] private string _case2Scene = "Seocheon";
        [Tooltip("제3사건 견우")]   [SerializeField] private string _case3Scene = "Gyeonu";

        /// <summary>
        /// 고른 사건이 어느 씬인가. 못 찾으면 <b>조사청으로 떨어진다</b> —
        /// 빈 화면에 멈춰 서느니 갈 데가 있는 편이 낫다.
        /// </summary>
        private string SceneForTaken()
        {
            string want;
            switch (_takenCase)
            {
                case CaseId.Case2_Seocheon: want = _case2Scene; break;
                case CaseId.Case3_Gyeonu:   want = _case3Scene; break;
                default:                    want = _case1Scene; break;
            }
            if (!string.IsNullOrEmpty(want) && Application.CanStreamedLevelBeLoaded(want)) return want;

            Debug.LogWarning($"[IntroController] {_takenCase} 사건 씬('{want}')을 빌드 목록에서 못 찾았습니다. " +
                             "조사청으로 보냅니다 — File ▸ Build Profiles 를 확인하세요.", this);
            return _hubSceneName;
        }

        private void LeaveForHub()
        {
            SubtitleView.Hide();

            string go = SceneForTaken();
            if (string.IsNullOrEmpty(go) || !Application.CanStreamedLevelBeLoaded(go))
            {
                Debug.LogWarning($"[IntroController] 갈 씬('{go}')을 찾을 수 없습니다. " +
                                 "File ▸ Build Profiles 의 씬 목록을 확인하세요.", this);
                return;
            }

            // <b>눈을 감았다 뜬다.</b> 종이는 그 자리에 둔다 — 다가오지 않는다.
            //
            // 한동안 맡은 봉서가 시야를 덮으며 넘어가게 했다. 「그 봉서가 조사청까지
            // 따라온 것이 된다」는 뜻이었는데, 화면으로 보면 <b>종이가 얼굴로 날아드는</b>
            // 것이다. 다가오는 결을 눅이고 시간을 늘려도 그 성질은 안 바뀌었다 —
            // 덮는 물건은 시야 안에서 커지는 것이고, 커지는 것은 다가오는 것이다.
            //
            // 자리를 옮기는 데 필요한 것은 <b>덮개</b>가 아니라 <b>눈꺼풀</b>이다.
            // 감았다 뜨면 그 사이에 어디로 갔든 자연스럽고, 무엇도 날아들지 않는다.
            //
            // <b>감는 동안 뒤에서 읽는다.</b> 동기로 읽으면 감긴 순간에 화면이 굳는다 —
            // 재 보니 서천 1.72초 · 옹고집 1.15초였다. 미리 읽어 두면 그 멎음이
            // 어둠 뒤로 숨는다. 다 감기기 전에는 들여보내지 않는다(EnterWhenReady).
            var op = SceneManager.LoadSceneAsync(go);
            op.allowSceneActivation = false;
            ScreenFade.To(1f, Mathf.Max(0.2f, _coverSeconds));
            ScreenFade.EnterWhenReady(op);
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
            // 단추 위에서는 안 센다 — 「튜토리얼 넘기기」를 눌렀는데 그것이 눌리는
            // 동시에 대사가 한 줄 넘어가면, 한 번 누른 것이 두 걸음이 된다.
            return space || UiGuard.AnywherePressed;
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
            return space || UiGuard.AnywhereHeld;
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
            DevLog.Note("[IntroController] 어명 본 기록을 지웠습니다 — 다음 실행에서 건너뛸 수 없습니다.");
        }
    }
}
