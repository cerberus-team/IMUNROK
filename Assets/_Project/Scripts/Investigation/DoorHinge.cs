using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 여닫이 문짝 한 짝을 "회전 보간"만으로 여닫는다.
    /// ★리깅·본·애니메이션 클립을 쓰지 않는다 — 이 GameObject의 localRotation을 직접 보간할 뿐이다.
    /// 그래서 콜라이더를 문짝에 붙여 두면 콜라이더가 자동으로 따라 돈다(별도 처리 불필요).
    ///
    /// [사용] 힌지 GO(회전축이 될 빈 부모)에 붙인다. 문짝 메시·콜라이더는 그 자식이어야 한다.
    ///   Open Angle     : 완전히 열렸을 때의 각도(도). ★부호는 Direction으로 준다(여기엔 양수).
    ///   Direction      : +1 / -1 — 어느 쪽으로 열리는지
    ///   Open Duration  : 여닫는 데 걸리는 시간(초). easing은 SmoothStep
    ///   Axis           : 회전축(로컬). 기본 = 로컬 Y
    ///   Start Open     : 씬 시작 시 열린 상태로 둘지(기본 false = 닫힘)
    ///
    /// ★닫힘 자세는 Awake 시점의 localRotation을 그대로 기준으로 삼는다.
    ///   따라서 씬에는 반드시 "닫힌 자세"로 저장해 두어야 한다(힌지 회전 0).
    ///
    /// 여는 중에 다시 Toggle()을 호출하면 그 지점에서 곧바로 반대로 돌아간다.
    /// </summary>
    public class DoorHinge : MonoBehaviour
    {
        [Header("열림")]
        [Tooltip("완전히 열렸을 때의 각도(도). 부호는 Direction으로 준다.")]
        [SerializeField] private float _openAngle = 85f;
        [Tooltip("+1 / -1 — 어느 쪽으로 열리는지")]
        [SerializeField] private float _direction = 1f;
        [Tooltip("여닫는 데 걸리는 시간(초)")]
        [SerializeField] private float _openDuration = 0.6f;
        [Tooltip("회전축(로컬). 기본 = 로컬 Y")]
        [SerializeField] private Vector3 _axis = Vector3.up;
        [Tooltip("씬 시작 시 열린 상태로 둘지")]
        [SerializeField] private bool _startOpen = false;

        private Quaternion _closedRot;
        private float _t;        // 0 = 닫힘, 1 = 열림
        private bool _wantOpen;

        /// <summary>현재 열린 쪽에 가까운가.</summary>
        public bool IsOpen => _t > 0.5f;

        /// <summary>지금 움직이는 중인가.</summary>
        public bool IsMoving => !Mathf.Approximately(_t, _wantOpen ? 1f : 0f);

        /// <summary>목표가 "열림"인가(움직이는 중에도 목표를 알 수 있다).</summary>
        public bool WantsOpen => _wantOpen;

        /// <summary>열림 진행도 0~1(보간 전 원시값).</summary>
        public float Progress => _t;

        private void Awake()
        {
            _closedRot = transform.localRotation;
            _wantOpen = _startOpen;
            _t = _startOpen ? 1f : 0f;
            Apply();
        }

        private void Update()
        {
            float goal = _wantOpen ? 1f : 0f;
            if (Mathf.Approximately(_t, goal)) return;

            float step = (_openDuration <= 0f) ? 1f : Time.deltaTime / _openDuration;
            _t = Mathf.MoveTowards(_t, goal, step);
            Apply();
        }

        private void Apply()
        {
            float e = Mathf.SmoothStep(0f, 1f, _t);
            float deg = _openAngle * Mathf.Sign(_direction) * e;
            Vector3 ax = _axis.sqrMagnitude < 1e-6f ? Vector3.up : _axis.normalized;
            transform.localRotation = _closedRot * Quaternion.AngleAxis(deg, ax);
        }

        /// <summary>열림/닫힘 전환. 움직이는 중이면 그 지점에서 반대로 돌아간다.</summary>
        public void Toggle() => _wantOpen = !_wantOpen;

        public void Open() => _wantOpen = true;
        public void Close() => _wantOpen = false;

        /// <summary>보간 없이 즉시 목표 자세로(에디터 미리보기·세이브 복원용).</summary>
        public void SetOpenImmediate(bool open)
        {
            _wantOpen = open;
            _t = open ? 1f : 0f;
            Apply();
        }
    }
}
