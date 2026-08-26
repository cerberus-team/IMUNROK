using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 열린 개구멍 문짝 — 조준하면 "닫기"만 담당한다 (2026-08-21 2차 다듬기).
    ///
    /// <see cref="GapHoleDoor"/>(구멍 오브젝트)가 밀기·들어가기·나가기를 맡고, 이쪽은
    /// 열려서 옆으로 물러난 돌덩이 자체를 조준했을 때만 반응한다. 문짝의 콜라이더는
    /// 닫혀 있는 동안 GapHoleDoor가 꺼 두므로(같은 자리에 "구멍" 콜라이더와 겹쳐 raycast가
    /// 갈리는 것을 막기 위해) 이 컴포넌트는 열렸을 때만 실제로 걸린다.
    /// </summary>
    [AddComponentMenu("이문록/관아 개구멍 문짝 (GapHoleLeaf)")]
    [DisallowMultipleComponent]
    public class GapHoleLeaf : Interactable
    {
        public GapHoleDoor door;
        public string promptClose = "닫기";

        public override bool CanInteract(GameObject actor)
            => door != null && door.IsOpen && !door.IsCrawling;

        public override string Prompt => promptClose;

        public override void Interact(GameObject actor) => door?.Close();
    }
}
