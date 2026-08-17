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
    public class LockedDoor : Interactable
    {
        [Tooltip("잠금 상태 — 나중에 퍼즐·열쇠가 Unlock()으로 푼다")]
        public bool locked = true;

        [TextArea]
        public string lockedMessage = "굳게 잠겨 있다. 여는 방법이 따로 있는 모양이다.";
        [TextArea]
        public string firstOpenMessage = "문 너머로 아래로 내려가는 계단이 보인다.";

        [Header("경첩")]
        public Vector3 pivotInParent;
        public float openAngle = 95f;
        public float duration = 1.1f;

        Vector3 closedLocalPos;
        Quaternion closedLocalRot;
        float t;
        bool open, announced;

        public override string Prompt => locked ? "살펴보기" : (open ? "닫기" : "열기");

        void Awake()
        {
            closedLocalPos = transform.localPosition;   // 씬은 닫힌 상태로 저장된다
            closedLocalRot = transform.localRotation;
        }

        /// <summary>퍼즐·열쇠 쪽에서 부르는 해제 진입점.</summary>
        public void Unlock()
        {
            locked = false;
        }

        public override void Interact(GameObject actor)
        {
            if (locked)
            {
                DebugToast.ShowPinned(lockedMessage);
                return;
            }

            open = !open;
            if (open && !announced)
            {
                announced = true;
                DebugToast.ShowPinned(firstOpenMessage);
            }
        }

        void Update()
        {
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.05f, duration));
            float s = t * t * (3f - 2f * t);
            var q = Quaternion.AngleAxis(openAngle * s, Vector3.up);
            transform.localPosition = pivotInParent + q * (closedLocalPos - pivotInParent);
            transform.localRotation = q * closedLocalRot;
        }
    }
}
