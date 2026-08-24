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
    /// ■ 진행 사슬에서 이 컴포넌트가 맡은 칸 (2026-08-23 재정리)
    ///   혼천의 성공 → **혼상 회전(여기)** → 촛대 소지 → 점등. 한 칸씩만 열린다.
    ///   - <see cref="GyeonuWorld.F_혼천의퍼즐"/> 전에는 **구가 아예 돌지 않는다.**
    ///     어느 쪽으로 얼마나 돌려야 할지 모르는 상태이므로 손만 미끄러진다.
    ///   - 그 뒤 누적 <see cref="turnToLearn"/>도를 돌리면 "빛이 필요하다" 안내가 뜨고
    ///     <see cref="GyeonuWorld.F_혼상회전"/>이 서면서 **비로소 촛대 잠금이 풀린다.**
    ///   - 촛대를 들고 와서 혼상을 사용하면 촛대가 소모되며 점등 시퀀스(PlayIgnite)가 시작된다.
    ///
    /// ⚠️ 누적 회전 판정은 원래 "혼천의 퍼즐이 붙기 전까지의 임시 판정"이었다가
    ///    2026-08-23에 잠깐 걷어냈는데, 그러자 **혼상을 돌리는 단계가 통째로 사라졌다**
    ///    (혼천의를 풀면 곧장 촛대가 열렸다). 같은 날 정식 단계로 되살렸다 — 이제 임시가 아니다.
    /// </summary>
    public class HonsangFocusOrb : FocusInteractable
    {
        public HonsangController honsang;
        [Tooltip("드래그 픽셀당 회전 각도")]
        public float degreesPerPixel = 0.22f;

        [Tooltip("혼천의를 맞춘 뒤 이만큼 누적으로 돌리면 촛대 자리를 알게 된다(도). " +
                 "0.22°/픽셀이므로 150도 ≒ 화면 가로 한 번 반쯤 끄는 양")]
        public float turnToLearn = 150f;

        [TextArea]
        public string notReadyMessage = "구는 묵직하게 돌아갈 뿐이다. 어느 쪽으로 얼마나 돌려야 할지 알 수가 없다.";
        [TextArea]
        public string needLightMessage = "빛이 필요하다. 작업실의 촛대를 가져와야겠다.";

        float side = 1f;      // 마지막 포커스 관측면 (+X / -X)
        float turned;         // 이번 세션 누적 회전량
        bool notReadyShown;   // 안내를 포커스 한 번에 한 번만 띄우려고

        /// <summary>혼천의를 맞췄는가 — 이때부터 구가 돈다.</summary>
        static bool Ready => GyeonuWorld.Has(GyeonuWorld.F_혼천의퍼즐)
                          || GyeonuWorld.DebugIgnoreConditions;

        /// <summary>이미 다 돌려 촛대 자리를 알아냈는가.</summary>
        static bool Learned => GyeonuWorld.Has(GyeonuWorld.F_혼상회전)
                            || GyeonuWorld.DebugIgnoreConditions;

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
                // 사슬의 마지막 칸. **부르는 쪽에서** 세운다 — 점등 시퀀스 자체는 손대지 않는다.
                GyeonuWorld.Set(GyeonuWorld.F_혼상점등);
                honsang.PlayIgnite();
                return;
            }
            base.Interact(actor);
        }

        public override void HandleDrag(Vector2 delta)
        {
            if (honsang == null) return;

            // ① 혼천의를 아직 못 맞췄다 — 구가 돌지 않는다
            if (!Ready)
            {
                if (!notReadyShown) { notReadyShown = true; DebugToast.ShowPinned(notReadyMessage); }
                return;
            }

            float deg = -delta.x * degreesPerPixel * side;
            honsang.RotateOrb(deg);

            // ② 이미 알아냈거나 불이 들어와 있으면 더 셀 것이 없다 — 구경거리로만 돈다
            if (Learned || honsang.IsLit || honsang.SequenceRunning) return;

            turned += Mathf.Abs(deg);
            if (turned >= turnToLearn) LearnLantern();
        }

        /// <summary>충분히 돌렸다 — 촛대 자리를 알게 되고, 그때 비로소 촛대 잠금이 풀린다.</summary>
        void LearnLantern()
        {
            GyeonuWorld.Set(GyeonuWorld.F_혼상회전);
            LanternPickup.PickupAllowed = true;
            DebugToast.ShowPinned(needLightMessage);
            var rig = FindFirstObjectByType<DebugFocusRig>();
            if (rig != null) rig.ExitFocus();      // 안내가 뜨면 포커스는 자동으로 풀린다
            Debug.Log("[혼상] 충분히 돌렸다 — 작업실 촛대를 집을 수 있다");
        }

        public override void OnFocusChanged(bool focused)
        {
            if (focused) notReadyShown = false;    // 다시 들여다보면 안내도 다시 뜬다
        }

        /// <summary>
        /// 씬을 다시 들어왔을 때 **불이 켜져 있던 상태를 되살린다.**
        /// <see cref="HonsangController.IsLit"/> 는 씬 개체마다 새로 나므로 플래그가 없으면
        /// 방이 도로 밝아지고, 타공 지도(불빛이 있어야 길이 뜬다)도 함께 죽는다.
        ///
        /// ⚠️ 점등 **시퀀스**는 건드리지 않는다 — 이미 공개돼 있는 즉시 토글
        /// <see cref="HonsangController.StarNight"/> 만 부른다. 한 번 본 연출을 다시 보여 줄 이유도 없다.
        /// </summary>
        void Start()
        {
            if (honsang == null || honsang.IsLit) return;
            if (GyeonuWorld.Has(GyeonuWorld.F_혼상점등)) honsang.StarNight(true);
        }

        public override string FocusStatus =>
            !Ready ? "구가 헛돈다" : null;
    }
}
