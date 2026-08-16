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
    ///  3) 선택하면 해당 사건 씬을 로드한다(씬이 아직 없으면 로그만 남기고 넘어감).
    ///
    /// URP에서는 색을 MaterialPropertyBlock의 "_BaseColor"로 칠한다
    /// (머티리얼 에셋을 새로 만들지 않아도 되고, 큐브마다 독립적으로 색이 적용됨).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class CaseCube : MonoBehaviour, ISelectable
    {
        [Header("이 큐브가 나타내는 사건")]
        [SerializeField] private CaseId _caseId = CaseId.Case1_Onggojip;

        [Tooltip("선택 시 로드할 팀원 사건 씬 이름. 아직 없으면 로그만 출력됨.")]
        [SerializeField] private string _caseSceneName = "";

        [Header("상태별 색")]
        [SerializeField] private Color _notStartedColor = new Color(0.5f, 0.5f, 0.5f);   // 회색
        [SerializeField] private Color _inProgressColor = new Color(1f, 0.55f, 0.1f);    // 주황
        [SerializeField] private Color _completedColor  = new Color(1f, 0.84f, 0.0f);    // 금색

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

            Color c = _state.GetStatus(_caseId) switch
            {
                CaseStatus.InProgress => _inProgressColor,
                CaseStatus.Completed  => _completedColor,
                _                     => _notStartedColor, // NotStarted
            };

            if (_hovered)
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
                Debug.Log($"[CaseCube] {_caseId} 는 이미 완료된 사건입니다.");
                return;
            }

            // 사건 씬 로드 시도
            bool canLoad = !string.IsNullOrEmpty(_caseSceneName)
                           && Application.CanStreamedLevelBeLoaded(_caseSceneName);

            if (!canLoad)
            {
                // 아직 팀원 사건 씬이 없을 때: 상태를 건드리지 않고 로그만 남긴다.
                // (여기서 StartCase를 부르면 들어가지도 못한 사건이 영구히 InProgress(주황)로 남아,
                //  ResetAll 말고는 되돌릴 방법이 없어진다.)
                Debug.Log($"[CaseCube] {_caseId} 선택됨. 사건 씬('{_caseSceneName}')이 아직 없어 건너뜀. " +
                          $"(팀원 씬이 준비되면 자동 로드됨)");
                return;
            }

            // 실제로 들어갈 수 있을 때만 상태를 바꾼다.
            _state.StartCase(_caseId);   // 색이 주황으로 → 진행중 시각 피드백
            _state.EnterCase(_caseId);   // 수첩이 이 사건 단서를 보여줌
            Debug.Log($"[CaseCube] {_caseId} 사건 씬 로드 → '{_caseSceneName}'");
            SceneManager.LoadScene(_caseSceneName);
        }
    }
}
