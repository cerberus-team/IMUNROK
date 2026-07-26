using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 봉서함: 왕의 명이 도착하는 곳. 게임 시작의 도입이자, 세 사건을 모두 푼 뒤
    /// 복명(엔딩)으로 이어지는 출구.
    ///
    /// 평소(비활성)에는 어둑한 색이고 선택해도 반응하지 않는다.
    /// 세계가 열리면(WorldStateController가 Activate 호출) 빛나며 선택 가능해지고,
    /// 선택 시 복명 씬으로 이어진다.
    ///
    /// ※ 6단계에서 OnSelect에 실제 복명 씬 로드를 연결한다(지금은 로그만).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class BongseoBox : MonoBehaviour, ISelectable
    {
        [Header("색")]
        [SerializeField] private Color _inactiveColor = new Color(0.35f, 0.32f, 0.28f); // 어둑한 나무색
        [SerializeField] private Color _activeColor   = new Color(1.0f, 0.85f, 0.3f);   // 빛나는 금빛

        [Tooltip("가리켰을 때 흰색 쪽으로 섞는 정도(활성 상태에서만)")]
        [Range(0f, 1f)]
        [SerializeField] private float _hoverBrighten = 0.35f;

        [Tooltip("선택 시 이동할 복명(엔딩) 씬 이름")]
        [SerializeField] private string _endingSceneName = "EndingScene";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private bool _active;
        private bool _hovered;

        public bool IsActive => _active;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            RefreshColor();
        }

        /// <summary>세계가 열릴 때 호출 — 빛나고 선택 가능해진다.</summary>
        public void Activate()
        {
            _active = true;
            RefreshColor();
            Debug.Log("[BongseoBox] 봉서함 활성화 — 복명으로 나아갈 수 있습니다.");
        }

        /// <summary>닫힘 상태로(초기/리셋).</summary>
        public void Deactivate()
        {
            _active = false;
            _hovered = false;
            RefreshColor();
        }

        private void RefreshColor()
        {
            if (_renderer == null) return;
            Color c = _active ? _activeColor : _inactiveColor;
            if (_active && _hovered)
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
            if (!_active)
            {
                Debug.Log("[BongseoBox] 아직 봉서함이 열리지 않았습니다. 세 사건을 모두 마쳐야 합니다.");
                return;
            }

            // 복명(EndingScene)으로 이동
            if (!string.IsNullOrEmpty(_endingSceneName) && Application.CanStreamedLevelBeLoaded(_endingSceneName))
            {
                Debug.Log($"[BongseoBox] 봉서함 선택 — 복명으로 이어집니다: '{_endingSceneName}'");
                SceneManager.LoadScene(_endingSceneName);
            }
            else
            {
                Debug.LogWarning($"[BongseoBox] 복명 씬('{_endingSceneName}')을 찾을 수 없습니다. " +
                                 $"[이문록 ▸ 복명 씬 생성] 을 먼저 실행했는지 확인하세요.");
            }
        }
    }
}
