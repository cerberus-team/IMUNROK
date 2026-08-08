using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 쌍여닫이 문 — 좌우 문짝이 각각 바깥쪽 경첩을 축으로 동시에 안쪽으로 열려
    /// 가운데가 갈라진다. 이 컴포넌트는 문 부모(Door01k)에 붙고, 문짝(자식 GO)의
    /// 콜라이더에서 GetComponentInParent로 발견된다 — 어느 짝을 클릭해도 둘 다 동작.
    /// 문짝·피벗은 VillageDoorRigger가 메시를 좌우로 분할해 세팅한다.
    /// 부모는 Y회전만 갖는다고 가정(담장 문 전제).
    /// </summary>
    public class DoubleHingeDoor : Interactable
    {
        public Transform leftLeaf;
        public Transform rightLeaf;
        [Tooltip("부모 로컬 좌표계의 경첩 위치 (각 문짝 바깥쪽 끝)")]
        public Vector3 leftPivot;
        public Vector3 rightPivot;
        [Tooltip("열림 각도(부호 = 방향, 리거가 마당 안쪽으로 실측)")]
        public float leftAngle = 100f;
        public float rightAngle = -100f;
        [Tooltip("여닫는 시간(초)")]
        public float duration = 0.8f;

        Vector3 lPos, rPos;
        Quaternion lRot, rRot;
        float t;
        bool open;

        public override string Prompt => open ? "닫기" : "열기";

        void Awake()
        {
            if (leftLeaf != null) { lPos = leftLeaf.localPosition; lRot = leftLeaf.localRotation; }
            if (rightLeaf != null) { rPos = rightLeaf.localPosition; rRot = rightLeaf.localRotation; }
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
            Apply(leftLeaf, leftPivot, leftAngle * s, lPos, lRot);
            Apply(rightLeaf, rightPivot, rightAngle * s, rPos, rRot);
        }

        static void Apply(Transform leaf, Vector3 pivot, float angle, Vector3 closedPos, Quaternion closedRot)
        {
            if (leaf == null) return;
            var q = Quaternion.AngleAxis(angle, Vector3.up);
            leaf.localPosition = pivot + q * (closedPos - pivot);
            leaf.localRotation = q * closedRot;
        }
    }
}
