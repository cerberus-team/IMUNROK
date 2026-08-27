using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 집어 들 수 있는 불붙은 기물 (2026-08-15) — 혼상 점등 흐름의 열쇠.
    /// 작업실 큰 탁자 위의 촛대(SM_Candlestick128)에 붙는다 (2026-08-15 등롱→촛대 교체).
    /// 사용하면 카메라(손) 앞에 붙어 따라다니고, 촛불 포인트라이트가 함께 오므로
    /// 주변이 밝아져 "들고 있음"이 화면으로 읽힌다.
    /// 혼상에 불을 넣으면 Consume()으로 사라졌다가, 불을 거두면 RestoreConsumed()로
    /// 원래 자리(탁자)에 되돌아온다.
    ///
    /// 집기 게이트: 혼상 안내("빛이 필요하다")가 뜨기 전에는 조준해도 상호작용이
    /// 나타나지 않는다 — HonsangFocusOrb가 안내를 띄우며 PickupAllowed를 연다.
    /// 입력 측(DebugInteractor/VR)은 Interact()만 호출한다 — 파지 표현은 이 컴포넌트 소관.
    /// </summary>
    public class LanternPickup : Interactable
    {
        /// <summary>지금 손에 들려 있는 것 (없으면 null).</summary>
        public static LanternPickup Held { get; private set; }

        /// <summary>
        /// 집기를 막는 게이트. **혼상을 충분히 돌려 촛대 자리를 알아냈을 때** 열린다
        /// (<see cref="HonsangFocusOrb"/> → <see cref="GyeonuWorld.F_혼상회전"/>).
        ///
        /// ⚠️ 2026-08-23 한때 혼천의 퍼즐(F_혼천의퍼즐)이 바로 열게 해 뒀는데, 그러면
        ///    **혼상을 돌리는 단계가 통째로 사라진다.** 같은 날 한 칸 뒤로 물렸다.
        /// </summary>
        public static bool PickupAllowed;

        /// <summary>지금 집을 수 있는 상태인가 — 혼상을 다 돌렸거나, 이번 세션에 이미 열렸거나.</summary>
        static bool Gate => PickupAllowed
                         || GyeonuWorld.Has(GyeonuWorld.F_혼상회전)
                         || GyeonuWorld.DebugIgnoreConditions;

        static LanternPickup consumed;   // 혼상에 넣어 사라진 것 — 소등 시 복귀

        // 도메인 리로드가 꺼진 프로젝트 — static이 플레이 세션을 넘겨 살아남으므로 직접 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Held = null; consumed = null; PickupAllowed = false; }

        [Tooltip("들었을 때 카메라 기준 위치 — 촛대(높이 0.4)가 화면 하단에 걸리는 자리 (등롱 시절 0.55m는 화면 절반을 가렸다)")]
        public Vector3 heldLocalPos = new Vector3(0.33f, -0.38f, 0.80f);

        Transform homeParent;
        Vector3 homePos;
        Quaternion homeRot;
        Collider aimCollider;

        public override string Prompt => "집어 들기";

        public override bool CanInteract(GameObject actor) =>
            Gate && Held == null && consumed != this;

        void Awake()
        {
            homeParent = transform.parent;
            homePos = transform.localPosition;
            homeRot = transform.localRotation;
            aimCollider = GetComponent<Collider>();
        }

        public override void Interact(GameObject actor)
        {
            if (Held != null) return;
            TakeInto(actor.transform);
            GyeonuWorld.Set(GyeonuWorld.F_촛대소지);
        }

        /// <summary>손에 든 자세로 붙인다 (집을 때·씬을 다시 들어와 되살릴 때 공용).</summary>
        void TakeInto(Transform hand)
        {
            Held = this;
            if (aimCollider != null) aimCollider.enabled = false;
            transform.SetParent(hand, false);              // hand = 워커 카메라
            transform.localPosition = heldLocalPos;
            transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        }

        /// <summary>
        /// 씬을 나갔다 들어왔을 때 **손에 들고 있던 상태를 되살린다.**
        /// static <see cref="Held"/> 는 세션을 넘겨 살아남지만 가리키던 오브젝트는 씬과 함께
        /// 파괴되므로(가짜 null), 플래그가 없으면 촛대가 탁자로 되돌아가 다시 집히게 된다.
        /// Awake가 아니라 Start인 까닭 — 그때라야 워커가 씬에 서 있다.
        /// </summary>
        void Start()
        {
            if (Held != null || consumed == this) return;
            if (!GyeonuWorld.Has(GyeonuWorld.F_촛대소지)) return;
            var walk = FindFirstObjectByType<DebugWalkController>(FindObjectsInactive.Exclude);
            if (walk == null || walk.eye == null) return;
            TakeInto(walk.eye);
            Debug.Log("[촛대] 들고 있던 상태로 되살림");
        }

        /// <summary>혼상에 불을 넣어 소모됨 — 숨겼다가 소등 때 되살린다.</summary>
        public void Consume()
        {
            if (Held == this) Held = null;
            consumed = this;
            GyeonuWorld.Set(GyeonuWorld.F_촛대소지, false);   // 더는 손에 없다
            gameObject.SetActive(false);
        }

        /// <summary>소등(불 거두기) 시 원래 자리(탁자)로 복귀시킨다.</summary>
        public static void RestoreConsumed()
        {
            if (consumed == null) return;
            var it = consumed;
            consumed = null;
            it.gameObject.SetActive(true);
            it.PutBack();
        }

        public void PutBack()
        {
            if (Held == this) Held = null;
            transform.SetParent(homeParent, false);
            transform.localPosition = homePos;
            transform.localRotation = homeRot;
            if (aimCollider != null) aimCollider.enabled = true;
        }
    }
}
