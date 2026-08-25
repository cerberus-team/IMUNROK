using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>등불을 내려놓는 자리</b> — 등경(燈檠).
    ///
    /// 등불에는 여태 손이 하나 묶여 있었다. 종이를 한 손에 들고 등불을 다른 손에 들면
    /// <b>돋보기를 들 손이 없다</b>. 그래서 배접 속을 비춰 놓고도 그 글자를 확대해
    /// 읽을 수가 없었다 — 두 도구가 서로를 막고 있었던 셈이다.
    ///
    /// 내려놓으면 풀린다. 놓인 등불은 <see cref="Firelight"/> 를 그대로 지니므로
    /// <b>그 자리에서 계속 비춘다</b>(배접을 읽는 것은 등불의 재주가 아니라 불의
    /// 재주다). 손은 둘 다 빈다. 대신 <b>그 자리를 떠날 수 없다</b> — 들고 다니면
    /// 어디서나 비추되 한 손이 묶이고, 내려놓으면 손이 풀리되 발이 묶인다.
    /// 그것이 이 물건이 만드는 선택이다.
    ///
    /// 붙이는 곳: 등경 뿌리(콜라이더와 함께). <see cref="_prop"/> 에는 <b>놓였을 때
    /// 보일 등불</b>을 걸어 두고 꺼 둔다 — 그 등불에 빛(Light)과 Firelight 가
    /// 붙어 있어야 놓았을 때 제 몫을 한다.
    ///
    /// <b>빈 등경으로 시작한다.</b> 처음부터 불이 놓여 있으면 플레이어가 제 등불을
    /// 내려놓을 까닭이 없다. 방은 어둡고 등경은 비어 있어야, 들고 온 불을 여기 얹는
    /// 일이 제 손으로 하는 일이 된다.
    /// </summary>
    public class LanternStand : MonoBehaviour, IInspectable, ISelectable
    {
        [Tooltip("이 도구를 들고 있을 때만 내려놓을 수 있다")]
        [SerializeField] private string _toolId = "lantern";

        [Tooltip("놓였을 때 보일 등불. 빛(Light)과 Firelight 가 붙어 있어야 한다. 처음엔 꺼 둔다")]
        [SerializeField] private GameObject _prop;

        [Tooltip("이 거리(m) 안에서만 놓고 집을 수 있다")]
        [SerializeField] private float _maxTouchDistance = 2.5f;

        [SerializeField] private string _title = "등경";

        [Tooltip("비었고 등불도 없을 때")]
        [TextArea(2, 3)] [SerializeField] private string _emptyBody = "등잔을 얹는 자리다. 걸이가 비어 있다.";

        [Tooltip("비었고 등불을 손에 들었을 때")]
        [TextArea(2, 3)] [SerializeField] private string _readyBody = "여기 등불을 얹으면 두 손이 빈다.";

        [Tooltip("등불이 놓여 있을 때")]
        [TextArea(2, 3)] [SerializeField] private string _litBody = "등불이 걸려 있다. 이 자리를 비추고 있다.";

        [SerializeField] private string _readyHint = "(눌러 얹기)";
        [SerializeField] private string _litHint = "(눌러 도로 들기)";

        /// <summary>지금 등불이 얹혀 있나.</summary>
        public bool Loaded => _prop != null && _prop.activeSelf;

        private void Awake()
        {
            if (_prop != null) _prop.SetActive(false);
            if (GetComponent<Collider>() == null)
                Debug.LogWarning($"[{name}] 콜라이더가 없어 눌러도 잡히지 않습니다.", this);
        }

        // ── 가리키기 ──
        public string GetInspectTitle() => _title;

        public string GetInspectBody()
        {
            if (Loaded) return Join(_litBody, _litHint);
            if (Holding) return Join(_readyBody, _readyHint);
            return _emptyBody;
        }

        public void OnInspected() { }

        private static string Join(string body, string hint)
            => string.IsNullOrEmpty(hint) ? body : body + "\n" + hint;

        /// <summary>등불을 손에 들고 있나.</summary>
        private bool Holding => ToolbeltHud.SelectedToolId == _toolId;

        // ── 누르기 ──
        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public void OnSelect()
        {
            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;
            if (_prop == null) return;

            var belt = ToolbeltHud.Instance;
            if (Loaded)
            {
                // 도로 든다. 벨트에 등불이 없으면(아직 못 받았으면) 얹힌 채로 둔다 —
                // 집었는데 손에 아무것도 없는 꼴이 되면 불만 사라진다.
                if (belt == null || !belt.SelectTool(_toolId)) return;
                _prop.SetActive(false);
            }
            else
            {
                if (!Holding) return;
                _prop.SetActive(true);
                if (belt != null) belt.Select(0);       // 손을 비운다
            }
        }
    }
}
