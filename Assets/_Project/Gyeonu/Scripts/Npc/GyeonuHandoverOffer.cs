using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 「받기」 자리 (2026-09-11). 견우가 물건을 내밀고 선 동안만 켜지는 조준면이다.
    ///
    /// ■ 왜 견우 본체가 아니라 자식인가
    ///   견우에게는 이미 <see cref="NpcDialogue"/>(말 걸기)가 <see cref="Interactable"/> 로 붙어 있다.
    ///   같은 오브젝트에 둘을 달면 <c>GetComponentInParent&lt;Interactable&gt;</c> 가 <b>둘 중 아무거나</b>
    ///   집는다 — 어느 쪽이 잡힐지 컴포넌트 순서에 달린 셈이라 못 믿는다.
    ///   그래서 가슴 앞에 <b>제 콜라이더를 가진 자식</b>을 하나 두고 거기에 붙인다.
    ///   조준 광선은 몸통 캡슐보다 이쪽에 먼저 닿고, 그 콜라이더에서 위로 올라가면 이 부품이 나온다.
    ///
    /// ■ 트리거인데 조준이 되는 까닭
    ///   <see cref="DebugInteractor"/> 는 "같은 오브젝트에 몸통이 따로 있는 트리거"만 조준면에서 뺀다
    ///   (아이01의 노래 감지 구 같은 것). 이 자식에는 트리거 하나뿐이라 그 규칙에 걸리지 않는다.
    /// </summary>
    [AddComponentMenu("")]
    public class GyeonuHandoverOffer : Interactable
    {
        public GyeonuGiveKey owner;

        public override string Prompt => "받기";

        public override bool CanInteract(GameObject actor) => owner != null && owner.OfferOpen;

        public override void Interact(GameObject actor)
        {
            if (owner != null) owner.Accept();
        }
    }
}
