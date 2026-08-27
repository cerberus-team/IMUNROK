using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 관측실 바닥 비밀문 문짝 (2026-08-15) — 평소 닫혀 있어 마루와 구분되지 않는 작은 문.
    /// 클릭하면 경첩(부모 로컬 pivotInParent, axisInParent 축)을 중심으로 부드럽게 젖혀진다.
    /// HingeDoor(담장 문, Y축 회전)와 같은 문짝-자체-회전 방식이되 축이 수평이라 별도 컴포넌트.
    /// 콜라이더가 문짝에 붙어 함께 움직인다 — 닫히면 밟고 지나가는 보행면이 된다.
    /// (추락 방지 투명판은 구멍에 따로 깔려 있어 열려 있어도 떨어지지 않는다 — ArchiveBuilder 소관)
    ///
    /// 다 열리고 나면 안내 문구를 혼상 힌트와 같은 방식(DebugToast.ShowPinned —
    /// 하단 고정, 플레이어가 이동하면 소멸)으로 한 번 띄운다.
    /// </summary>
    public class TrapdoorLid : Interactable
    {
        [Tooltip("부모 로컬 좌표계의 경첩 위치 (북변 모서리)")]
        public Vector3 pivotInParent;
        [Tooltip("경첩 축 (부모 로컬)")]
        public Vector3 axisInParent = Vector3.right;
        [Tooltip("열림 각도 — +X축 기준 +는 북쪽으로 젖혀 올라간다")]
        public float openAngle = 110f;
        [Tooltip("여닫는 시간(초)")]
        public float duration = 1.1f;
        [Tooltip("위층(관측실)에서 열었을 때 — 하단 고정, 이동 시 소멸")]
        public string openMessageUpper = "내려갈 방법이 없다. 다른 길을 찾아야겠다.";
        [Tooltip("아래층(서고)에서 열었을 때 — 위가 아까 그 관측실임을 알아본다")]
        public string openMessageLower = "위는 아까 그 관측실이다. 그 바닥 밑에 이 서고가 숨어 있었다.";
        [Tooltip("연 사람의 y가 이 값보다 위면 위층으로 판정 (위층 눈높이 -1.4 / 아래층 -4.9)")]
        public float floorSplitY = -3.5f;

        public bool IsOpen => open;

        Vector3 closedLocalPos;
        Quaternion closedLocalRot;
        float t;          // 0=닫힘, 1=열림
        bool open;

        public override string Prompt => open ? "닫기" : "열기";

        void Awake()
        {
            closedLocalPos = transform.localPosition;   // 씬은 닫힌 상태로 저장됨
            closedLocalRot = transform.localRotation;
        }

        public override void Interact(GameObject actor)
        {
            open = !open;
            // 여는 즉시 띄운다 — 애니메이션(1.1s)이 끝난 뒤에 띄우면 그 사이 플레이어가
            // 조금만 움직여도 이동 감지 기준점이 어긋나 뜨자마자 사라진다 (v4 실측: "안 뜨는" 원인)
            bool fromUpper = ActorY(actor) > floorSplitY;
            if (open) DebugToast.ShowPinned(fromUpper ? openMessageUpper : openMessageLower);
            else DebugToast.HidePinned();

            // 지도 중첩(A2) 뒤 위층에서 문틈을 직접 열어 아래 흔적을 확인한 경우에만 B2.
            if (open && fromUpper && GyeonuCase.HasClue(ClueId.A2)) GyeonuCase.AddClue(ClueId.B2);
        }

        /// <summary>연 사람의 높이 — actor(보통 워커 카메라)가 없으면 워커를 찾고, 그마저 없으면 위층 취급.</summary>
        float ActorY(GameObject actor)
        {
            if (actor != null) return actor.transform.position.y;
            var walk = FindFirstObjectByType<DebugWalkController>();
            return walk != null ? walk.transform.position.y + 1.6f : 0f;
        }

        void Update()
        {
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.05f, duration));
            float s = t * t * (3f - 2f * t);            // 스무스스텝
            var q = Quaternion.AngleAxis(openAngle * s, axisInParent.normalized);
            transform.localPosition = pivotInParent + q * (closedLocalPos - pivotInParent);
            transform.localRotation = q * closedLocalRot;
        }
    }
}
