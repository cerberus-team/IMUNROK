using UnityEngine;
using UnityEngine.XR;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>몸을 옮긴다</b> — 왼쪽 스틱으로 걷고, 오른쪽 스틱으로 딱딱 돌아선다.
    ///
    /// 붙는 곳은 <b>몸</b>(카메라의 부모)이다. 머리는 헤드셋이 움직이고, 몸은 이것이
    /// 움직인다. 이 둘을 한 트랜스폼에 겹쳐 두면 고개를 돌린 것과 몸을 돌린 것이
    /// 뒤엉켜 어느 쪽으로 걷는지 알 수 없게 된다.
    ///
    /// <b>왜 딱딱 돌아서나(스냅)</b>: 부드럽게 도는 것은 눈은 도는데 몸속 평형은 가만히
    /// 있어서 멀미가 난다. 조사는 오래 서 있는 놀이라 그 차이가 크다. 각을 뚝 끊어
    /// 돌리면 뇌가 "장면이 바뀌었다"로 받아들여 훨씬 덜 어지럽다.
    ///
    /// 걷는 방향은 <b>머리가 보는 쪽</b>이다. 몸이 보는 쪽으로 걸으면, 고개만 돌려 옆을
    /// 보며 걷는 일이 안 된다.
    /// </summary>
    public class VRLocomotion : MonoBehaviour
    {
        [Tooltip("걷는 빠르기(m/초). 남의 집 안방을 뒤지는 걸음이라 빠를 것 없다")]
        [SerializeField] private float _speed = 1.5f;

        [Tooltip("한 번에 돌아서는 각(도)")]
        [SerializeField] private float _snapAngle = 30f;

        [Tooltip("스틱을 이만큼 넘게 밀어야 친다(0~1). 손 떨림에 저절로 돌지 않게")]
        [Range(0.1f, 0.9f)] [SerializeField] private float _deadZone = 0.6f;

        [Tooltip("머리. 비우면 자식에서 카메라를 찾는다")]
        [SerializeField] private Transform _head;

        [Tooltip("발밑을 짚어 그 높이로 내려앉는다. 마루와 마당의 높이가 다르므로 필요하다")]
        [SerializeField] private bool _stickToFloor = true;

        private bool _turnArmed = true;

        private void Awake()
        {
            if (_head == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) _head = cam.transform;
            }
        }

        private void Update()
        {
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            // ── 걷기 ──
            if (left.isValid && left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 mv)
                && mv.sqrMagnitude > 0.02f)
            {
                Vector3 fwd = _head != null ? _head.forward : transform.forward;
                Vector3 rightDir = _head != null ? _head.right : transform.right;
                fwd.y = 0f; rightDir.y = 0f;
                if (fwd.sqrMagnitude > 1e-4f) fwd.Normalize();
                if (rightDir.sqrMagnitude > 1e-4f) rightDir.Normalize();
                transform.position += (fwd * mv.y + rightDir * mv.x) * _speed * Time.deltaTime;
            }

            // ── 돌아서기 ──
            if (right.isValid && right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 tn))
            {
                if (Mathf.Abs(tn.x) < _deadZone * 0.6f) _turnArmed = true;
                else if (_turnArmed && Mathf.Abs(tn.x) > _deadZone)
                {
                    // 머리를 축으로 돈다 — 몸 한가운데를 축으로 돌면 서 있던 자리가 밀린다
                    Vector3 pivot = _head != null ? new Vector3(_head.position.x, transform.position.y, _head.position.z)
                                                  : transform.position;
                    transform.RotateAround(pivot, Vector3.up, Mathf.Sign(tn.x) * _snapAngle);
                    _turnArmed = false;
                }
            }

            // ── 발밑 ──
            if (!_stickToFloor) return;
            if (Physics.Raycast(transform.position + Vector3.up * 1.2f, Vector3.down,
                                out RaycastHit floor, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                var p = transform.position;
                p.y = Mathf.MoveTowards(p.y, floor.point.y, 3f * Time.deltaTime);
                transform.position = p;
            }
        }
    }
}
