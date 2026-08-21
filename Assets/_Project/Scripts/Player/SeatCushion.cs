using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 권해 놓은 자리 — <b>눌러야 앉는다</b>.
    ///
    /// 왜 저절로 앉히지 않는가: 방에 들어서자마자 시야가 스르르 내려가면 앉은 것이 아니라
    /// 가라앉은 것이 된다. 주인이 자리를 권하고, 내가 그 자리를 골라 앉아야 "마주 앉았다"가
    /// 된다. 앉는 것은 이 방에서 내가 하는 첫 번째 행동이다.
    ///
    /// 헤드셋에서는 다르다. 거기서는 <b>몸이 실제로 앉는다</b> — 방석을 눌러 앉으라고 하면
    /// 서 있는 채로 눈만 내려앉아 멀미가 난다. 그래서 VR이면 권하는 절차 없이 눈높이만
    /// 내려 주고 이 부품은 물러난다(<see cref="PlayerSeat.OfferSeat"/> 가 갈라 준다).
    ///
    /// 붙이는 법: 방석에 콜라이더와 함께 붙인다. 자리를 권하기 전에는 눌러도 안 앉는다.
    ///
    /// <b>콜라이더를 요구하지 않는 까닭</b>: RequireComponent(typeof(Collider)) 를 걸면
    /// 콜라이더가 없는 물건에 붙일 때 유니티가 Collider 를 대신 붙이려 하는데, 그것은
    /// 추상이라 붙지 않는다 — 그 자리에서 AddComponent 가 조용히 null 을 돌려준다.
    /// 어떤 콜라이더를 쓸지는 붙이는 쪽이 정하게 두고, 없으면 아래에서 일러 준다.
    /// </summary>
    public class SeatCushion : MonoBehaviour, IInspectable, ISelectable
    {
        [Tooltip("자리를 권하기 전에 가리키면 뜨는 말")]
        [SerializeField] private string _idleBody = "손님 자리다.";

        [Tooltip("자리를 권한 뒤 가리키면 뜨는 말")]
        [SerializeField] private string _offeredBody = "주인이 권한 자리다.";

        [SerializeField] private string _title = "방석";
        [Tooltip("눌러보라는 신호")]
        [SerializeField] private string _hint = "(눌러 앉기)";

        [Tooltip("가리키면 이만큼 도톰해진다 — 만질 수 있는 자리임을 몸으로 알린다")]
        [SerializeField] private float _hoverRise = 0.02f;

        [Tooltip("이 거리(m) 안에서만 앉을 수 있다")]
        [SerializeField] private float _maxTouchDistance = 2.5f;

        /// <summary>자리를 권했나. 권하기 전에는 눌러도 앉지 않는다.</summary>
        public bool Offered { get; private set; }

        private Vector3 _home;
        private bool _hovering;

        private void Awake()
        {
            _home = transform.position;
            if (GetComponent<Collider>() == null)
                Debug.LogWarning($"[{name}] 콜라이더가 없어 눌러도 잡히지 않습니다.", this);
        }

        /// <summary>주인이 자리를 권했다. 이제부터 눌러 앉을 수 있다.</summary>
        public void Offer() { Offered = true; }

        /// <summary>다시 잠근다(앉고 난 뒤).</summary>
        public void Withdraw() { Offered = false; SetHover(false); }

        // ── 가리키기 ──
        public string GetInspectTitle() => _title;

        public string GetInspectBody()
        {
            if (!Offered) return _idleBody;
            return string.IsNullOrEmpty(_hint) ? _offeredBody : _offeredBody + "\n" + _hint;
        }

        public void OnInspected() { }

        // ── 누르기 ──
        public void OnHoverEnter() { SetHover(Offered); }
        public void OnHoverExit() { SetHover(false); }

        public void OnSelect()
        {
            if (!Offered) return;

            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;

            Withdraw();
            if (PlayerSeat.Instance != null) PlayerSeat.Instance.SitAt(transform);
        }

        private void SetHover(bool on)
        {
            if (_hovering == on) return;
            _hovering = on;
            transform.position = _home + new Vector3(0f, on ? _hoverRise : 0f, 0f);
        }
    }
}
