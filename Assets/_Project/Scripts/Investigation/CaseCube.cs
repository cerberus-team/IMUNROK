using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 사건판에 걸린 하나의 사건(문서 뭉치)을 나타내는 큐브.
    ///
    /// 역할:
    ///  1) GameState의 상태를 읽어 색을 바꾼다(NotStarted=회색, InProgress=주황, Completed=금색).
    ///  2) 상태가 바뀌면(OnCaseChanged) 자동으로 색을 갱신한다.
    ///  3) 선택하면 해당 사건 씬을 로드한다.
    ///
    /// <b>아직 오지 않은 사건</b>: 팀원의 씬이 아직 빌드 목록에 없으면 이 문서는 열리지
    /// 않는다. 여태는 그럴 때 콘솔에 한 줄 남기고 말았는데, 콘솔은 플레이하는 사람이
    /// 보는 데가 아니다. 눌러도 아무 일이 없으면 그것은 <b>고장으로 보인다</b> —
    /// 실제로 이 조사청에서 문서 셋 중 둘이 그랬다.
    ///
    /// 그래서 열리지 않는 문서는 <b>눈에 먼저</b> 다르게 보이고(어둑한 빛), 가리켜도
    /// 밝아지지 않으며, 눌렀을 때는 <b>말로</b> 까닭을 알린다.
    ///
    /// 어느 사건이 열리는지는 이름을 박아 정하지 않는다. <b>씬이 빌드 목록에 있으면
    /// 열린다</b>. 지금은 옹고집전 하나뿐이고, 팀원이 제 씬을 넣는 날 그 문서는
    /// 저절로 살아난다 — 여기를 다시 고칠 일이 없다.
    ///
    /// <b>차례는 없다</b>: 봉서 셋은 이어지는 이야기가 아니라 나란히 걸린 셋이다.
    /// 어느 것을 먼저 집든 상관없다. 한때 앞선 사건을 매듭져야 다음이 열리게 해
    /// 두었는데, 그러면 <b>씬이 없어서 어둑한 것</b>과 <b>차례가 아니라 어둑한 것</b>이
    /// 눈으로 구별되지 않는다 — 지금 둘이 어둑한 것은 오로지 씬이 아직 없어서다.
    ///
    /// <b>누르면 곧바로 들어가지 않는다</b>: 사건에 드는 것은 되돌리기 어려운 일이라
    /// 한 번은 묻는다. <see cref="CaseChoicePanel"/> 이 눈앞에 봉서로 풀려 내려와
    /// 제목과 요지를 보이고, 하던 것이 있으면 이어할지 처음부터 할지 고르게 한다.
    ///
    /// URP에서는 색을 MaterialPropertyBlock의 "_BaseColor"로 칠한다
    /// (머티리얼 에셋을 새로 만들지 않아도 되고, 큐브마다 독립적으로 색이 적용됨).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class CaseCube : MonoBehaviour, ISelectable
    {
        [Header("이 큐브가 나타내는 사건")]
        [SerializeField] private CaseId _caseId = CaseId.Case1_Onggojip;

        [Tooltip("선택 시 로드할 팀원 사건 씬 이름. 이 이름이 빌드 목록에 있어야 열린다 — " +
                 "없으면 문서가 어둑한 채로 남고, 눌러도 \"아직 오지 않았다\"고만 한다")]
        [SerializeField] private string _caseSceneName = "";

        // ── 상태별 빛깔 ──
        //
        // 회색·주황·금색으로 칠해 두었더니 벽에 신호등을 걸어 둔 꼴이었다. 종이가
        // 주황색이면 그것은 종이가 아니라 <b>표시</b>다 — 조사청에 어울리지 않고,
        // 무엇보다 무슨 뜻인지 아무도 못 알아본다(빨강이 시작 전인지 진행 중인지
        // 알 길이 없다).
        //
        // 그래서 빛깔을 <b>종이 안에서</b>만 움직인다. 넉 자리 다 한지 빛이고,
        // 다른 것은 <b>얼마나 빛을 받는가</b>뿐이다:
        //   · 손이 닿지 않는 것 — 그늘에 든 종이. 어둑하고 푸르다.
        //   · 아직 안 뜯은 것   — 갓 걸어 둔 흰 한지. 제일 밝다.
        //   · 보고 있는 것     — 등불빛을 먹어 누렇다. 손을 많이 탄 종이다.
        //   · 끝난 것          — 오래된 종이처럼 바래고 조금 어둡다. 할 일이 끝났다.
        // 진하고 옅고가 아니라 <b>밝고 어둡고</b>로 갈리므로, 어느 것이 지금 손에
        // 잡히는지가 한눈에 보이면서도 벽에 걸린 것은 여전히 종이로 보인다.
        [Header("상태별 빛깔 — 한지 안에서만 움직인다")]
        [Tooltip("아직 안 뜯은 봉서 — 갓 걸어 둔 흰 한지")]
        [SerializeField] private Color _notStartedColor = new Color(0.96f, 0.93f, 0.85f);
        [Tooltip("보고 있는 사건 — 등불빛을 먹어 누렇다")]
        [SerializeField] private Color _inProgressColor = new Color(0.98f, 0.86f, 0.62f);
        [Tooltip("끝난 사건 — 바랜 종이")]
        [SerializeField] private Color _completedColor  = new Color(0.64f, 0.58f, 0.48f);
        [Tooltip("씬이 아직 빌드 목록에 없는 사건. 그늘에 든 종이 — 손이 닿지 않는다는 것이 먼저 눈에 보여야 한다")]
        [SerializeField] private Color _notReadyColor   = new Color(0.30f, 0.31f, 0.33f);

        [Header("아직 오지 않은 사건")]
        [Tooltip("열리지 않는 문서를 눌렀을 때의 한 마디. 비우면 아무 말도 안 한다")]
        [SerializeField] private string _notReadyLine = "아직 봉서가 닿지 않은 사건이오.";
        [Tooltip("차례가 아직 오지 않은 사건을 눌렀을 때의 한 마디")]
        [SerializeField] private string _lockedLine = "앞선 사건부터 매듭지어야 하오.";
        [Tooltip("그 한 마디가 머무는 시간(초)")]
        [SerializeField] private float _notReadySeconds = 3.0f;

        [Header("고르는 창에 적을 것")]
        [Tooltip("비우면 사건 차례에서 짐작한다(제1사건…)")]
        [SerializeField] private string _order = "";
        [Tooltip("비우면 사건 이름에서 짐작한다")]
        [SerializeField] private string _caseName = "";
        [TextArea(2, 5)]
        [Tooltip("고르는 창에 적히는 요지 두어 줄")]
        [SerializeField] private string _brief = "";

        [Tooltip("가리켰을 때 흰색 쪽으로 섞는 정도(하이라이트)")]
        [Range(0f, 1f)]
        [SerializeField] private float _hoverBrighten = 0.35f;

        // URP Lit 셰이더의 색 프로퍼티 ID(문자열 조회 캐시)
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private GameState _state;   // 구독한 GameState 캐시(안전한 구독 해제용)
        private bool _hovered;

        public CaseId CaseId => _caseId;

        /// <summary>
        /// 이 사건에 지금 들어갈 수 있나 — <b>씬이 빌드 목록에 올라와 있는가</b>로만 본다.
        /// 사건 이름을 코드에 박지 않는 까닭이다. 팀원이 제 씬을 넣으면 그날부터 열린다.
        /// </summary>
        public bool Ready => InBuild(_caseSceneName);

        /// <summary>
        /// 이 이름의 씬이 빌드 목록에 켜져 있나.
        ///
        /// <c>Application.CanStreamedLevelBeLoaded</c> 를 쓰지 않는다 — 그것은
        /// <b>에디터에서 거짓을 돌려준다</b>. 빌드 목록에 멀쩡히 켜져 있는 Onggojip 을
        /// 두고도 false 라 하니, 편집 중에는 문서가 죄다 잠긴 것으로 보였다.
        /// 목록을 직접 읽으면 편집 중이든 실행 중이든 같은 답이 나온다.
        /// </summary>
        private static bool InBuild(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            int n = SceneManager.sceneCountInBuildSettings;   // 켜진 것만 센다
            for (int i = 0; i < n; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                int a = path.LastIndexOf('/') + 1;
                int b = path.LastIndexOf('.');
                if (b <= a) continue;
                if (string.Equals(path.Substring(a, b - a), sceneName,
                                  System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>사건이 <see cref="CaseId"/> 에서 몇 번째인가(0부터).</summary>
        private static int Order(CaseId id)
        {
            var all = System.Enum.GetValues(typeof(CaseId));
            for (int i = 0; i < all.Length; i++) if ((CaseId)all.GetValue(i) == id) return i;
            return 0;
        }

        /// <summary>
        /// 차례가 왔나.
        ///
        /// <b>차례는 없다.</b> 한때 앞선 사건을 매듭지어야 다음이 열리게 해 두었는데,
        /// 세 사건은 서로 이어지는 이야기가 아니라 <b>나란히 걸린 세 봉서</b>다.
        /// 어느 것을 먼저 집든 상관이 없고, 오히려 골라 짚는 것이 조사청의 뜻에 맞는다.
        ///
        /// 지금 둘이 어둑한 것은 차례를 기다려서가 아니라 <b>아직 그 씬이 없어서</b>다
        /// (<see cref="Ready"/>). 만들어 빌드 목록에 넣는 날 저절로 밝아진다.
        /// </summary>
        public bool Unlocked => true;

        /// <summary>지금 이 문서를 펼 수 있나.</summary>
        public bool Openable => Ready && Unlocked;

        /// <summary>
        /// 에디터 생성기(HubSceneBuilder)나 인스펙터 대신 코드로 세팅할 때 사용.
        /// </summary>
        public void Initialize(CaseId id, string sceneName)
        {
            _caseId = id;
            _caseSceneName = sceneName;
        }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _state = GameState.Instance;
            _state.OnCaseChanged += HandleCaseChanged;
            RefreshColor();
        }

        private void OnDisable()
        {
            // 캐시해 둔 인스턴스에서 해제(종료 중 GameState.Instance가 새로 생성되는 것 방지)
            if (_state != null)
                _state.OnCaseChanged -= HandleCaseChanged;
        }

        /// <summary>GameState에서 상태/판결이 바뀌면 호출됨. 내 사건일 때만 색 갱신.</summary>
        private void HandleCaseChanged(CaseId changed)
        {
            if (changed == _caseId) RefreshColor();
        }

        /// <summary>현재 상태(+하이라이트)에 맞춰 큐브 색을 다시 칠한다.</summary>
        private void RefreshColor()
        {
            // 두 값을 여기서 다시 챙긴다.
            //
            // 이것들은 Awake 에서 만들어 두는데, <b>Awake 를 거치지 않고 OnEnable 만
            // 도는 때</b>가 있다 — 플레이 중에 스크립트가 다시 컴파일되면 유니티가
            // 도메인을 갈아 끼우면서 직렬화되지 않는 값(이 둘)을 버리고 OnEnable 부터
            // 다시 부른다. 그때 GetPropertyBlock(null) 이 되어 터졌다.
            // 만들기가 거저인 값이므로 없으면 그 자리에서 만든다.
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_renderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_state == null) _state = GameState.Instance;
            // 씬을 닫는 중이면 GameState 가 null 을 돌려준다 — 그때는 칠할 것도 없다.
            if (_state == null) return;

            bool ready = Openable;
            Color c = !ready ? _notReadyColor : _state.GetStatus(_caseId) switch
            {
                CaseStatus.InProgress => _inProgressColor,
                CaseStatus.Completed  => _completedColor,
                _                     => _notStartedColor, // NotStarted
            };

            // 열리지 않는 문서는 가리켜도 밝아지지 않는다. 밝아지면 눌러도 된다는 뜻이 된다.
            if (_hovered && ready)
                c = Color.Lerp(c, Color.white, _hoverBrighten);

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _renderer.SetPropertyBlock(_mpb);
        }

        // ── ISelectable ─────────────────────────────

        public void OnHoverEnter()
        {
            _hovered = true;
            RefreshColor();
        }

        public void OnHoverExit()
        {
            _hovered = false;
            RefreshColor();
        }

        public void OnSelect()
        {
            // 이미 끝낸 사건은 다시 들어가지 않음
            if (_state.GetStatus(_caseId) == CaseStatus.Completed)
            {
                Say("이미 복명을 올린 사건이오.");
                return;
            }

            // 차례가 먼저다 — 사람에게는 이쪽이 진짜 까닭이고,
            // 씬이 없다는 것은 만드는 쪽 사정이라 나중에 본다.
            if (!Unlocked) { Say(_lockedLine); return; }

            if (!Ready)
            {
                // 아직 팀원 사건 씬이 없을 때: 상태는 건드리지 않는다.
                // (여기서 StartCase를 부르면 들어가지도 못한 사건이 영구히 InProgress(주황)로 남아,
                //  ResetAll 말고는 되돌릴 방법이 없어진다.)
                Say(_notReadyLine);
                Debug.Log($"[CaseCube] {_caseId} — 씬 '{_caseSceneName}' 이 빌드 목록에 없어 열지 않음.");
                return;
            }

            // 하던 것이 있나 — 들어가 본 적이 있거나 수첩에 이 사건 물증이 있으면.
            bool hasProgress = _state.GetStatus(_caseId) == CaseStatus.InProgress
                               || Journal.Instance.ClueCount(_caseId) > 0;

            CaseChoicePanel.Open(OrderLabel(), NameLabel(), _brief, hasProgress, Fresh, Enter);
        }

        /// <summary>처음부터 — 이 사건의 단서와 상태만 지운다. 다른 사건은 그대로 둔다.</summary>
        private void Fresh()
        {
            Journal.Instance.ClearCase(_caseId);
            _state.ResetCase(_caseId);
            Debug.Log($"[CaseCube] {_caseId} 처음부터");
            Enter();
        }

        /// <summary>사건 씬으로 든다. 암전으로 한 번 덮어야 눈앞이 뚝 끊기지 않는다.</summary>
        private void Enter()
        {
            _state.StartCase(_caseId);   // 색이 주황으로 → 진행중 시각 피드백
            _state.EnterCase(_caseId);   // 수첩이 이 사건 단서를 보여줌
            Debug.Log($"[CaseCube] {_caseId} 사건 씬 로드 → '{_caseSceneName}'");
            string scene = _caseSceneName;
            ScreenFade.Blink(0.4f, 0.5f, delegate { SceneManager.LoadScene(scene); });
        }

        private string OrderLabel()
        {
            if (!string.IsNullOrEmpty(_order)) return _order;
            string[] n = { "제1", "제2", "제3", "제4", "제5" };
            int i = Order(_caseId);
            return (i < n.Length ? n[i] : "제" + (i + 1)) + " 사건";
        }

        private string NameLabel()
        {
            if (!string.IsNullOrEmpty(_caseName)) return _caseName;
            switch (_caseId)
            {
                case CaseId.Case1_Onggojip: return "옹고집전";
                case CaseId.Case2_Seocheon: return "서천꽃밭";
                case CaseId.Case3_Gyeonu:   return "견우직녀";
            }
            return _caseId.ToString();
        }

        /// <summary>
        /// 눈앞에 한 마디 띄우고 잠시 뒤 거둔다.
        ///
        /// 콘솔이 아니라 자막인 까닭: 눌렀는데 아무 일이 없으면 사람은 그것을 고장으로
        /// 읽는다. 되든 안 되든 <b>눌린 것은 눌렸다고</b> 대답해야 한다.
        /// </summary>
        private void Say(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            SubtitleView.Show("", line, "");
            CancelInvoke(nameof(HushUp));
            Invoke(nameof(HushUp), Mathf.Max(0.5f, _notReadySeconds));
        }

        private void HushUp() => SubtitleView.Hide();
    }
}
