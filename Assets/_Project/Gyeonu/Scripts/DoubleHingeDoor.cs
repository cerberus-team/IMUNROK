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
    public class DoubleHingeDoor : Interactable, IOpenable
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
        [Tooltip("잠김 — 켜면 클릭해도 열리지 않는다. 관아 외삼문의 '밤에는 통제' 연출용 스위치.\n" +
                 "바깥(GateDayNight 등)에서 켜고 끈다.")]
        public bool locked;
        [Tooltip("잠겨 있을 때 보여줄 문구")]
        public string lockedPrompt = "잠겨 있다";
        [Tooltip("플레이어가 만질 수 있는 문인가.\n" +
                 "끄면 조준해도 조준점·이름이 뜨지 않고 클릭해도 아무 일도 없다 — 연출(시간대)만 여닫는다.\n" +
                 "관아 외삼문이 이 경우다: 낮엔 열려 있고 밤엔 닫혀 있으며 플레이어는 관여하지 못한다.")]
        public bool playerOperable = true;

        Vector3 lPos, rPos;
        Quaternion lRot, rRot;
        float t;
        bool open;

        public override string Prompt => locked ? lockedPrompt : (open ? "닫기" : "열기");

        /// <summary>열림 지시 상태 (씬 전환 등 바깥에서 읽는다).</summary>
        public bool IsOpen => open;

        // DebugInteractor 는 CanInteract 가 false 면 target 자체를 잡지 않는다
        // → 조준점 강조도, 이름·행동 표시도, 클릭도 전부 일어나지 않는다.
        public override bool CanInteract(GameObject actor) => playerOperable && !locked;

        /// <summary>
        /// 바깥에서 여닫이 상태를 지시한다 (시간대 기본값 등).
        /// instant 를 켜면 애니메이션 없이 즉시 그 자세로 놓는다 — 씬이 뜨는 순간
        /// 문이 스르륵 움직이는 것을 막는다. 잠김과 무관하게 동작한다(연출 측 지시).
        /// </summary>
        public void SetOpen(bool value, bool instant = false)
        {
            Capture();
            open = value;
            if (!instant) return;
            t = value ? 1f : 0f;
            float s = t * t * (3f - 2f * t);
            Apply(leftLeaf, leftPivot, leftAngle * s, lPos, lRot);
            Apply(rightLeaf, rightPivot, rightAngle * s, rPos, rRot);
        }

        bool captured;

        void Awake() => Capture();

        /// <summary>
        /// 닫힌 자세를 기억한다. Awake 뿐 아니라 SetOpen/Update 진입에서도 한 번 더 확인한다 —
        /// ★에디터에서 AddComponent 하면 Awake 없이 OnEnable 이 먼저 도는 경우가 있어,
        ///  기억 전에 SetOpen 이 불리면 닫힌 자세를 (0,0,0)으로 잡아 문짝이 엉뚱한 데로 날아간다
        ///  (2026-08-20 관아 외삼문 리깅에서 실제로 발생 — 문짝이 처마 밖으로 1.5m 밀려났다).
        /// </summary>
        void Capture()
        {
            if (captured) return;
            if (leftLeaf != null) { lPos = leftLeaf.localPosition; lRot = leftLeaf.localRotation; }
            if (rightLeaf != null) { rPos = rightLeaf.localPosition; rRot = rightLeaf.localRotation; }
            captured = true;
        }

        public override void Interact(GameObject actor)
        {
            if (!playerOperable || locked) return;
            open = !open;
        }

        void Update()
        {
            Capture();
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
