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

        [Header("상태별 색")]
        [SerializeField] private Color _notStartedColor = new Color(0.5f, 0.5f, 0.5f);   // 회색
        [SerializeField] private Color _inProgressColor = new Color(1f, 0.55f, 0.1f);    // 주황
        [SerializeField] private Color _completedColor  = new Color(1f, 0.84f, 0.0f);    // 금색
        [Tooltip("씬이 아직 빌드 목록에 없는 사건. 손이 닿지 않는다는 것이 먼저 눈에 보여야 한다")]
        [SerializeField] private Color _notReadyColor   = new Color(0.20f, 0.19f, 0.17f); // 어둑한 먹빛

        [Header("아직 오지 않은 사건")]
        [Tooltip("열리지 않는 문서를 눌렀을 때의 한 마디. 비우면 아무 말도 안 한다")]
        [SerializeField] private string _notReadyLine = "아직 봉서가 닿지 않은 사건이오.";
        [Tooltip("그 한 마디가 머무는 시간(초)")]
        [SerializeField] private float _notReadySeconds = 3.0f;

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
        public bool Ready => !string.IsNullOrEmpty(_caseSceneName)
                             && Application.CanStreamedLevelBeLoaded(_caseSceneName);

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
            if (_renderer == null) return;

            bool ready = Ready;
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

            if (!Ready)
            {
                // 아직 팀원 사건 씬이 없을 때: 상태는 건드리지 않는다.
                // (여기서 StartCase를 부르면 들어가지도 못한 사건이 영구히 InProgress(주황)로 남아,
                //  ResetAll 말고는 되돌릴 방법이 없어진다.)
                Say(_notReadyLine);
                Debug.Log($"[CaseCube] {_caseId} — 씬 '{_caseSceneName}' 이 빌드 목록에 없어 열지 않음.");
                return;
            }

            // 실제로 들어갈 수 있을 때만 상태를 바꾼다.
            _state.StartCase(_caseId);   // 색이 주황으로 → 진행중 시각 피드백
            _state.EnterCase(_caseId);   // 수첩이 이 사건 단서를 보여줌
            Debug.Log($"[CaseCube] {_caseId} 사건 씬 로드 → '{_caseSceneName}'");
            SceneManager.LoadScene(_caseSceneName);
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
