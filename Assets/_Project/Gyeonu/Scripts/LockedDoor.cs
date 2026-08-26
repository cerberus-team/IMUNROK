using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 잠긴 문 — 기본은 잠겨 있고, 클릭하면 안내 문구만 뜬다.
    /// <see cref="Unlock"/> 을 호출하면 그때부터 여닫이가 된다.
    ///
    /// 관측실 바닥 비밀문(TrapdoorLid)과 대비되는 물건이다: 저쪽은 **우연히 발견되는**
    /// 숨은 문이라 열리기는 하되 내려갈 수단이 없고, 이쪽은 **잠긴** 문이라 발견해도
    /// 열쇠·퍼즐 없이는 열리지 않는다. 해제 조건은 아직 붙이지 않았다(사용자 지시) —
    /// 나중에 퍼즐 쪽에서 Unlock()만 부르면 된다.
    ///
    /// 회전 방식은 HingeDoor와 같다 (부모 로컬 경첩점 기준 자전).
    /// </summary>
    public class LockedDoor : Interactable, IOpenable
    {
        [Tooltip("잠금 상태 — 나중에 퍼즐·열쇠가 Unlock()으로 푼다")]
        public bool locked = true;

        [TextArea]
        public string lockedMessage = "굳게 잠겨 있다. 여는 방법이 따로 있는 모양이다.";
        [TextArea]
        public string firstOpenMessage = "문 너머로 아래로 내려가는 계단이 보인다.";

        [Header("열쇠 (2026-08-24 — 비우면 예전 그대로 '늘 잠김')")]
        [Tooltip("이 GyeonuWorld 플래그를 지니고 있으면 클릭 한 번에 풀리고 그대로 열린다. " +
                 "소지품 쪽은 InventoryItem.worldFlag로 이 플래그를 세운다 — " +
                 "문은 소지품 시스템을 몰라도 된다")]
        public string keyFlag = "";
        [TextArea]
        public string keyUnlockMessage = "열쇠가 자물쇠에 꼭 맞는다. 빗장이 풀리고, 문 너머로 아래로 내려가는 계단이 보인다.";

        [Header("경첩")]
        public Vector3 pivotInParent;
        public float openAngle = 95f;
        public float duration = 1.1f;

        [Header("상태 유지 (비우면 씬 저장값만 씀)")]
        [Tooltip("잠금 해제 여부를 기억할 GyeonuWorld 키")]
        public string unlockedKey = "";
        [Tooltip("열린 상태를 기억할 키 — 서고에 다녀와도 열린 채로 남는다")]
        public string openKey = "";
        [Tooltip("첫 개방 안내를 이미 봤는지 기억할 키 — 다시 뜨지 않게")]
        public string announcedKey = "";

        Vector3 closedLocalPos;
        Quaternion closedLocalRot;
        float t;
        bool open, announced;

        public override string Prompt => locked ? "살펴보기" : (open ? "닫기" : "열기");

        /// <summary>열림 지시 상태 (씬 전환 등 바깥에서 읽는다). 잠긴 동안은 항상 false.</summary>
        public bool IsOpen => open;

        void Awake()
        {
            closedLocalPos = transform.localPosition;   // 씬은 닫힌 상태로 저장된다
            closedLocalRot = transform.localRotation;

            // 세션에 기억된 상태 복원 — 서고에 다녀와도 잠금·열림·안내 여부가 그대로다.
            if (GyeonuWorld.Has(unlockedKey)) locked = false;
            if (GyeonuWorld.Has(announcedKey)) announced = true;
            if (GyeonuWorld.Has(openKey))
            {
                open = true;
                t = 1f;
                ApplyHinge(1f);   // 애니메이션 없이 곧바로 열린 자세로
            }
        }

        /// <summary>퍼즐·열쇠 쪽에서 부르는 해제 진입점.</summary>
        public void Unlock()
        {
            locked = false;
            GyeonuWorld.Set(unlockedKey);
        }

        public override void Interact(GameObject actor)
        {
            bool byKey = false;
            if (locked)
            {
                // 열쇠를 지녔으면 그 자리에서 풀고 **그대로 연다** — 잠금을 푸는 클릭과 여는 클릭을
                // 따로 요구하면 "열쇠가 맞았다"는 순간이 두 번으로 쪼개져 김이 샌다.
                if (!string.IsNullOrEmpty(keyFlag) && GyeonuWorld.Has(keyFlag)) { Unlock(); byKey = true; }
                else { DebugToast.ShowPinned(lockedMessage); return; }
            }

            open = !open;
            GyeonuWorld.Set(openKey, open);

            if (open && (!announced || byKey))
            {
                announced = true;
                GyeonuWorld.Set(announcedKey);
                DebugToast.ShowPinned(byKey ? keyUnlockMessage : firstOpenMessage);
            }
        }

        void Update()
        {
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.05f, duration));
            ApplyHinge(t * t * (3f - 2f * t));
        }

        void ApplyHinge(float s)
        {
            var q = Quaternion.AngleAxis(openAngle * s, Vector3.up);
            transform.localPosition = pivotInParent + q * (closedLocalPos - pivotInParent);
            transform.localRotation = q * closedLocalRot;
        }
    }
}
