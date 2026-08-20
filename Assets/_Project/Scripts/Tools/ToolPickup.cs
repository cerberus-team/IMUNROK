using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 조사청 선반에 놓인 도구 하나. 집으면 도구벨트로 들어간다.
    ///
    /// 왜 벨트에 미리 안 넣어 두는가: 조사청은 <b>도구를 지급받는 곳</b>이다. 처음부터
    /// 다 들고 시작하면 그 방에 들를 까닭이 하나 없어지고, 무엇을 쥐고 있는지도 모른 채
    /// 사건에 들어가게 된다. 여기서 하나씩 집어 가야 손에 무엇이 있는지 기억에 남는다.
    ///
    /// 집은 도구는 선반에서 사라진다 — 빈자리가 "이건 이미 챙겼다"를 말해 준다.
    /// 사건 씬에서 조사청으로 돌아와도 벨트는 그대로이므로 다시 집을 일은 없다.
    ///
    /// 붙이는 법: 선반에 놓인 물건에 콜라이더와 함께 붙이고 <see cref="_tool"/> 에
    /// 도구 정의(Assets/_Project/Data/Tools)를 건다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class ToolPickup : MonoBehaviour, ISelectable
    {
        [Tooltip("집었을 때 벨트에 들어갈 도구")]
        [SerializeField] private ToolDef _tool;

        [Tooltip("집었을 때 할 말. {0} 자리에 도구 이름이 들어간다")]
        [SerializeField] private string _takenWord = "{0}을 챙겼다.";

        [Tooltip("집으면 선반에서 치운다")]
        [SerializeField] private bool _hideWhenTaken = true;

        [Range(0f, 1f)]
        [SerializeField] private float _hoverBrighten = 0.35f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Color _base = Color.white;

        /// <summary>이 자리에 놓인 도구. 생성기가 씬을 짤 때 채워 넣는다.</summary>
        public ToolDef Tool { get => _tool; set => _tool = value; }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(BaseColorId))
                _base = _renderer.sharedMaterial.GetColor(BaseColorId);
        }

        private void Start()
        {
            // 이미 벨트에 있는 도구라면 선반에 둘 까닭이 없다(사건에서 돌아온 경우).
            if (_hideWhenTaken && _tool != null && ToolbeltHud.Instance != null
                && ToolbeltHud.Instance.Has(_tool))
                gameObject.SetActive(false);
        }

        public void OnHoverEnter() => Tint(_base + Color.white * _hoverBrighten);
        public void OnHoverExit() => Tint(_base);

        public void OnSelect()
        {
            if (_tool == null) return;

            var belt = ToolbeltHud.Instance;
            if (belt == null)
            {
                Debug.LogWarning("[도구] 씬에 도구벨트(ToolbeltHud)가 없습니다.");
                return;
            }

            bool added = belt.Grant(_tool);
            if (!string.IsNullOrEmpty(_takenWord))
                SubtitleView.Show("", string.Format(_takenWord, _tool.displayName));

            if (added && _hideWhenTaken) gameObject.SetActive(false);
        }

        private void Tint(Color c)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
