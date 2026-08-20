using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 경첩 문 — 문짝(이 컴포넌트가 붙은 GO)이 부모 좌표계의 경첩점(pivotInParent)을 축으로
    /// 부드럽게 회전한다. 콜라이더가 같은 GO에 있어 함께 움직인다 — 열리면 통과, 닫히면 차단.
    ///
    /// 힌지 부모 GO 방식이 아닌 이유: 집터가 프리팹 인스턴스라 기존 자식의 리페어런트
    /// (구조 변경)가 금지됨 — 컴포넌트 추가 + 트랜스폼 오버라이드만 허용되므로
    /// 문짝 스스로 피벗 기준 회전. 부모는 Y회전만 갖는다고 가정(담장 문 전제).
    /// 리깅은 VillageDoorRigger가 pivotInParent·openAngle을 계산해 넣는다.
    /// </summary>
    public class HingeDoor : Interactable, IOpenable
    {
        [Tooltip("부모 로컬 좌표계의 경첩 위치 (리거가 계산)")]
        public Vector3 pivotInParent;
        [Tooltip("열림 각도(부호 = 방향, 마당 안쪽)")]
        public float openAngle = 100f;
        [Tooltip("여닫는 시간(초)")]
        public float duration = 0.8f;

        Vector3 closedLocalPos;
        Quaternion closedLocalRot;
        float t;          // 0=닫힘, 1=열림
        bool open;

        public override string Prompt => open ? "닫기" : "열기";

        /// <summary>열림 지시 상태 (씬 전환 등 바깥에서 읽는다).</summary>
        public bool IsOpen => open;

        void Awake()
        {
            closedLocalPos = transform.localPosition;   // 씬은 닫힌 상태로 저장됨
            closedLocalRot = transform.localRotation;
        }

        public override void Interact(GameObject actor)
        {
            open = !open;
        }

        void Update()
        {
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.05f, duration));
            float s = t * t * (3f - 2f * t);            // 스무스스텝
            var q = Quaternion.AngleAxis(openAngle * s, Vector3.up);
            transform.localPosition = pivotInParent + q * (closedLocalPos - pivotInParent);
            transform.localRotation = q * closedLocalRot;
        }
    }
}
