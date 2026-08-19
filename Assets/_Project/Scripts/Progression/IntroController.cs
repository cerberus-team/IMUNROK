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

        [Tooltip("문서를 집으라는 안내(대사 후 표시)")]
        [SerializeField] private string _pickPrompt = "세 문서 중 하나를 집으라. 거기서부터 조사가 시작된다.";

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
        [SerializeField] private string _hubSceneName = "HubScene";

        [Header("건너뛰기")]
        [Tooltip("이만큼 누르고 있으면 어명을 건너뛴다. 스치듯 눌러 날아가지 않게 길게 잡는다")]
        [SerializeField] private float _skipHoldSeconds = 1.2f;
        [Tooltip("끄면 처음부터 건너뛸 수 있다(시연·개발용)")]
        [SerializeField] private bool _skipOnlyAfterSeen = true;

        /// <summary>어명을 끝까지 본 적이 있는가. 기기에 남는다.</summary>
        private const string SeenKey = "이문록_어명_봄";

        private int _index;
        private float _timer;
        private bool _speechDone;
        private bool _docsRevealed;
        private bool _taken;
        private float _holdTimer;

        private static bool SeenBefore
        {
            get { return PlayerPrefs.GetInt(SeenKey, 0) == 1; }
            set { PlayerPrefs.SetInt(SeenKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private bool CanSkip => !_skipOnlyAfterSeen || SeenBefore;

        private void Start()
        {
            _index = 0;
            _timer = 0f;
            ShowLine();
        }

        private void Update()
        {
            if (_speechDone) return;

            HandleSkipHold();
            if (_speechDone) return;

            _timer += Time.deltaTime;
            if (_timer >= _secondsPerLine || AdvancePressed())
                Next();
        }

        /// <summary>누르고 있는 시간을 재서 건너뛴다. 짧게 누른 것은 '다음 줄'이라 여기 걸리지 않는다.</summary>
        private void HandleSkipHold()
        {
            if (!CanSkip) return;

            if (!AdvanceHeld()) { _holdTimer = 0f; return; }

            _holdTimer += Time.deltaTime;
            if (_holdTimer < _skipHoldSeconds) return;

            _holdTimer = 0f;
            EndSpeech();
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
            return CanSkip
                ? "(다음 — 누르기 · 건너뛰기 — 꾹 누르기)"
                : "(다음 — 누르기)";
        }

        private void EndSpeech()
        {
            if (_speechDone) return;
            _speechDone = true;
            _index = Mathf.Max(0, _kingLines.Length - 1);

            // 끝까지 들었든 건너뛰었든, 이 지점에 닿았으면 본 것으로 친다.
            SeenBefore = true;

            SubtitleView.Show("", _pickPrompt, "(봉서를 가리켜 집는다)");
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
        /// 봉서 하나를 골라 받았다. 고른 사건은 <see cref="IntroDocument"/> 가 이미
        /// 진행중으로 표시했고, 여기서는 받는 말 한 마디와 조사청으로 넘어가는 일만 한다.
        /// </summary>
        public void TakeChosen(CaseId chosen)
        {
            if (_taken) return;
            _taken = true;

            // 고르지 않은 둘은 사건판에 회색으로 남는다. 나중에 아무 때나 집으면 된다.
            foreach (var d in _documents)
                if (d != null && d.CaseId != chosen) d.Freeze();

            if (!string.IsNullOrEmpty(_takeLine)) SubtitleView.Show(_takeSpeaker, _takeLine);
            else SubtitleView.Hide();

            Invoke(nameof(LeaveForHub), Mathf.Max(0.1f, _leaveAfter));
        }

        private void LeaveForHub()
        {
            SubtitleView.Hide();

            if (string.IsNullOrEmpty(_hubSceneName) || !Application.CanStreamedLevelBeLoaded(_hubSceneName))
            {
                Debug.LogWarning($"[IntroController] 조사청 씬('{_hubSceneName}')을 찾을 수 없습니다. " +
                                 "File ▸ Build Profiles 의 씬 목록을 확인하세요.", this);
                return;
            }

            // 눈을 한 번 감았다 뜨는 사이에 옮긴다 — 갑자기 자리가 바뀌면 멀미가 난다.
            ScreenFade.Blink(0.45f, 0.5f, () => SceneManager.LoadScene(_hubSceneName));
        }

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
