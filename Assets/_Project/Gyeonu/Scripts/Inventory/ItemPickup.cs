using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 씬에 놓인 **집을 수 있는 물건** (2026-08-23).
    /// 기존 Interactable 규약을 그대로 탄다 — 입력 측(DebugInteractor / 훗날 VR 컨트롤러)은
    /// Interact()만 부르고, 여기서 소지품에 넣고 씬에서 치운다.
    ///
    /// ■ 행동 문구는 놓인 자리가 정한다
    ///   같은 서책이라도 서안 위면 "살피기", 바닥이면 "줍기", 궤 안이면 "꺼내기", NPC가 주면 "받기".
    ///   기본값은 아이템 정의(<see cref="InventoryItem.pickupVerb"/>)에 있고, 자리마다 다르면
    ///   <see cref="verbOverride"/>로 이 인스턴스만 덮어쓴다.
    ///
    /// ■ 가구 안의 물건
    ///   <see cref="insideFurniture"/>에 여닫이 가구를 물리면 **열려 있을 때만** 집을 수 있다.
    ///   닫혀 있으면 조준해도 아무 반응이 없다 — 안에 뭐가 있는지 모르는 상태다.
    ///
    /// ■ 왜 Destroy가 아니라 비활성인가
    ///   씬을 나갔다 들어오면 프리팹이 되살아난다. Awake에서 "이미 지닌 물건인지" 물어
    ///   되살아난 것을 다시 감춘다 — Destroy로는 이 판정을 할 자리가 없다.
    ///   (LanternPickup이 소모/복귀를 다루는 방식과 같은 결)
    /// </summary>
    public class ItemPickup : Interactable
    {
        [Tooltip("집으면 들어갈 소지품 정의")]
        public InventoryItem item;

        [Tooltip("조준했을 때 물건 이름을 함께 보여 준다. 기본은 꺼짐 — 무엇을 집는지는 " +
                 "집어 들고 상세로 봐야 안다. 이름을 미리 알아도 되는 물건에만 켠다")]
        public bool revealName = false;

        [Tooltip("revealName일 때 쓸 이름. 비우면 아이템 정의의 이름")]
        public string overrideName = "";

        [Tooltip("비우면 아이템 정의의 pickupVerb를 쓴다 (자리마다 다른 동사를 줄 때만 채운다)")]
        public string verbOverride = "";

        [Tooltip("이 여닫이 가구가 열려 있어야 집을 수 있다 (비우면 항상 가능)")]
        public MonoBehaviour insideFurniture;

        [Tooltip("집었을 때 화면에 띄울 문구. **기본은 없음** — 집으면 상세 창이 바로 뜨므로 " +
                 "따로 알릴 것이 없고, 조작 안내는 앞 사건에서 이미 배운 것이다")]
        public string pickupToast = "";

        [Tooltip("이 진행 조건이 모두 차야 집을 수 있다 (GyeonuWorld 플래그)")]
        public string[] requiredFlags;

        public override string Prompt =>
            !string.IsNullOrEmpty(verbOverride) ? verbOverride
            : (item != null && !string.IsNullOrEmpty(item.pickupVerb) ? item.pickupVerb : "살피기");

        /// <summary>물린 가구가 열려 있는가 (안 물렸으면 늘 참).</summary>
        public bool FurnitureOpen
        {
            get
            {
                if (insideFurniture == null) return true;
                var openable = insideFurniture as IOpenable;
                return openable == null || openable.IsOpen;
            }
        }

        void Awake()
        {
            // 조준 문구는 기본이 **동사뿐**이다 ("꺼내기"). displayName이 비면 DebugInteractor가
            // 접두사 없이 행동만 띄운다 — 무엇을 얻었는지는 집어서 상세로 봐야 안다.
            displayName = !revealName ? ""
                : (!string.IsNullOrEmpty(overrideName) ? overrideName
                   : (item != null ? item.displayName : ""));

            // 이미 지닌 물건이면 씬을 다시 들어와도 자리에 없다
            if (item != null && Inventory.Has(item)) gameObject.SetActive(false);
        }

        public override bool CanInteract(GameObject actor) =>
            item != null && !Inventory.Has(item) && FurnitureOpen && GyeonuWorld.HasAll(requiredFlags);

        public override void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            if (!Inventory.Add(item)) return;

            // 기본은 아무 문구도 띄우지 않는다 — 이름을 알려 주지도, 조작을 가르치지도 않는다.
            if (!string.IsNullOrEmpty(pickupToast)) DebugToast.Show(pickupToast, 3.5f);

            gameObject.SetActive(false);
        }
    }
}
