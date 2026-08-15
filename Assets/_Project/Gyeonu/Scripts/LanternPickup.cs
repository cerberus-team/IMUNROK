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

        /// <summary>혼상 안내가 뜨기 전까지 집기를 막는 게이트 — HonsangFocusOrb가 연다.</summary>
        public static bool PickupAllowed;

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
            PickupAllowed && Held == null && consumed != this;

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
            Held = this;
            if (aimCollider != null) aimCollider.enabled = false;
            transform.SetParent(actor.transform, false);   // actor = 워커 카메라
            transform.localPosition = heldLocalPos;
            transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        }

        /// <summary>혼상에 불을 넣어 소모됨 — 숨겼다가 소등 때 되살린다.</summary>
        public void Consume()
        {
            if (Held == this) Held = null;
            consumed = this;
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
