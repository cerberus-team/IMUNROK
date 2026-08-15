using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 혼상 포커스 조작 (2026-08-14, 점등 흐름 2026-08-15) — FocusInteractable 구현.
    /// 정면 = 자오환이 원으로 보이는 ±X면 중 플레이어와 가까운 쪽.
    /// 드래그 가로 성분을 극축 회전(HonsangController.RotateOrb)으로 넘긴다 —
    /// 구멍 별 패턴과 바닥 빛점 투영이 함께 돈다. 어느 면에서 보든 드래그 방향과
    /// 구 표면 이동 방향이 일치하도록 관측면에 따라 부호를 뒤집는다.
    ///
    /// 점등 흐름 (2026-08-15 개정): 구를 일정량 돌리면 "빛이 필요하다" 안내가
    /// 하단(조작 힌트 자리)에 고정으로 뜨고(임시 판정 — 나중에 퍼즐 정답 판정으로 교체),
    /// 포커스가 자동으로 풀린다. 안내는 플레이어가 걷기 시작하면 사라진다(DebugToast).
    /// 이때 촛대 집기 게이트(LanternPickup.PickupAllowed)가 열린다 — 그 전에는
    /// 작업실 촛대를 조준해도 상호작용이 나타나지 않는다. 촛대를 들고 와서
    /// 혼상을 사용하면 촛대가 소모되며 점등 시퀀스(PlayIgnite)가 시작된다.
    /// </summary>
    public class HonsangFocusOrb : FocusInteractable
    {
        public HonsangController honsang;
        [Tooltip("드래그 픽셀당 회전 각도")]
        public float degreesPerPixel = 0.22f;
        [Tooltip("안내가 뜨는 누적 회전량(도) — 임시. 퍼즐 판정이 붙으면 교체된다")]
        public float hintAfterDegrees = 120f;

        float side = 1f;      // 마지막 포커스 관측면 (+X / -X)
        float turned;         // 누적 회전량 (안내 임시 판정용)
        bool hintShown;

        public override string Prompt =>
            LanternPickup.Held != null && honsang != null && !honsang.IsLit ? "불 넣기" : "살펴보기";

        public override bool CanInteract(GameObject actor) =>
            honsang == null || !honsang.SequenceRunning;

        public override Vector3 FocusPoint =>
            honsang != null && honsang.orb != null ? honsang.orb.position : transform.position;

        public override void GetFocusPose(Vector3 currentEyePos, out Vector3 pos, out Quaternion rot)
        {
            var c = FocusPoint;
            side = currentEyePos.x >= c.x ? 1f : -1f;
            pos = c + new Vector3(side * focusDistance, 0f, 0f);
            rot = Quaternion.LookRotation(c - pos);
        }

        public override void Interact(GameObject actor)
        {
            // 등불을 들고 왔으면 포커스 대신 점등 — 등불은 소모되고 소등 때 곁상으로 복귀
            if (honsang != null && !honsang.IsLit && !honsang.SequenceRunning && LanternPickup.Held != null)
            {
                LanternPickup.Held.Consume();
                honsang.PlayIgnite();
                return;
            }
            base.Interact(actor);
        }

        public override void HandleDrag(Vector2 delta)
        {
            if (honsang == null) return;
            float deg = -delta.x * degreesPerPixel * side;
            honsang.RotateOrb(deg);
            if (!hintShown && !honsang.IsLit)
            {
                turned += Mathf.Abs(deg);
                if (turned >= hintAfterDegrees)
                {
                    hintShown = true;
                    LanternPickup.PickupAllowed = true;   // 이제부터 작업실 촛대를 집을 수 있다
                    DebugToast.ShowPinned("빛이 필요하다. 작업실의 촛대를 가져와야겠다.");
                    var rig = FindFirstObjectByType<DebugFocusRig>();
                    if (rig != null) rig.ExitFocus();     // 안내가 뜨면 포커스는 자동으로 풀린다
                }
            }
        }
    }
}
