using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// 지나가면 다른 자리로 옮겨주는 영역. 중간대문처럼 "따라 들어가면 안쪽으로 넘어가는" 연출용.
    ///
    /// 어두워진 사이에 옮긴다(ScreenFade.Blink). VR에서 그냥 위치를 바꾸면
    /// 눈은 움직였다는데 몸은 가만히 있어서 멀미가 나기 때문이다.
    ///
    /// 옮기는 것은 카메라가 아니라 <b>플레이어 리그(카메라의 최상위 부모)</b>다.
    /// VR에서 카메라는 헤드셋이 직접 움직이므로, 카메라를 옮기면 다음 프레임에 되돌아간다.
    /// 리그를 옮기되 머리와 리그의 어긋남을 보정해, 도착 지점에 "머리"가 오도록 맞춘다.
    ///
    /// 붙이는 법:
    ///   1) 빈 GameObject를 중간대문 앞(넘어가는 자리)에 두고 이 컴포넌트 추가
    ///   2) _destination 에 도착 자리(빈 오브젝트)를 연결 — 그 오브젝트의 정면이 도착 후 바라볼 방향
    ///   3) (선택) _requireDoorOpen 에 중간대문의 DoorController 연결 → 문이 열려야만 발동
    ///   4) (선택) _requireLeaderPassed 에 복동 연결 → 복동이 먼저 지나간 뒤에만 발동
    /// 반경은 씬뷰에 노란 원으로 보인다.
    /// </summary>
    public class TeleportZone : MonoBehaviour
    {
        [Header("어디로")]
        [Tooltip("도착 자리. 이 오브젝트의 정면이 도착 후 바라볼 방향이 된다")]
        [SerializeField] private Transform _destination;
        [Tooltip("도착 후 바라보는 방향까지 맞출지. 끄면 위치만 옮긴다")]
        [SerializeField] private bool _matchFacing = true;

        [Tooltip("도착 지점 바닥을 못 찾았을 때만 쓰는 예비값(m). 바닥에 콜라이더가 있으면 " +
                 "그 높이를 재서 올려놓으므로 이 값은 쓰이지 않는다")]
        [SerializeField] private float _heightOffset = 0f;

        [Tooltip("도착 지점의 바닥을 재서 그 위에 세운다. 마당에서 마루로 오를 때 " +
                 "높이를 손으로 적어 넣지 않아도 되고, 날아서 넘어와도 지붕 속에 처박히지 않는다")]
        [SerializeField] private bool _standOnFloor = true;
        [Tooltip("바닥 위 눈높이(m). 데스크탑 테스트 카메라(리그=카메라)일 때만 쓴다. " +
                 "VR은 리그가 발밑이라 바닥 높이를 그대로 준다")]
        [SerializeField] private float _eyeHeight = 1.6f;

        [Header("발동 조건")]
        [Tooltip("이 반경(m) 안에 들어오면 발동")]
        [SerializeField] private float _radius = 1.2f;
        [Tooltip("연결하면 이 문이 열려 있을 때만 발동(중간대문)")]
        [SerializeField] private DoorController _requireDoorOpen;
        [Tooltip("연결하면 이 인물이 먼저 지나간 뒤에만 발동(복동을 따라 들어가는 연출)")]
        [SerializeField] private Transform _requireLeaderPassed;
        [Tooltip("한 번만 발동. 끄면 드나들 때마다 발동")]
        [SerializeField] private bool _once = true;

        [Header("연출")]
        [SerializeField] private float _fadeOut = 0.25f;
        [SerializeField] private float _fadeIn = 0.35f;

        [Header("플레이어")]
        [Tooltip("비우면 Camera.main 의 최상위 부모(=리그)를 자동으로 찾는다")]
        [SerializeField] private Transform _playerRig;

        [Tooltip("옮긴 직후 실행할 것(대사 띄우기 등)")]
        [SerializeField] private UnityEvent _onTeleported;

        private bool _fired;
        private bool _playerInside;

        private void Update()
        {
            if (_fired && _once) return;

            var cam = Camera.main;
            if (cam == null || _destination == null) return;

            // 플레이어가 반경 안에 있는가(수평 거리만 — 층 높이는 무시)
            Vector3 a = cam.transform.position, b = transform.position;
            a.y = b.y = 0f;
            bool inside = (a - b).sqrMagnitude <= _radius * _radius;

            // 들어온 "순간"에만 발동. 안에 서 있는 동안 계속 옮기면 안 된다.
            if (inside && !_playerInside && CanFire()) Fire(cam);
            _playerInside = inside;
        }

        private bool CanFire()
        {
            if (ScreenFade.IsFading) return false;                       // 이미 옮기는 중
            if (_requireDoorOpen != null && !_requireDoorOpen.IsOpen) return false;
            if (_requireLeaderPassed != null)
            {
                // 안내자가 나보다 문 안쪽에 있어야 "따라 들어가는" 것이 된다.
                Vector3 toDest = _destination.position - transform.position;
                toDest.y = 0f;
                Vector3 toLeader = _requireLeaderPassed.position - transform.position;
                toLeader.y = 0f;
                if (Vector3.Dot(toDest.normalized, toLeader) <= 0f) return false;
            }
            return true;
        }

        private void Fire(Camera cam)
        {
            _fired = true;
            Transform rig = ResolveRig(cam);

            ScreenFade.Blink(_fadeOut, _fadeIn, () =>
            {
                MoveRig(rig, cam.transform);
                _onTeleported?.Invoke();
            });
        }

        private Transform ResolveRig(Camera cam)
        {
            if (_playerRig != null) return _playerRig;
            // 카메라의 최상위 부모 = 리그. 부모가 없으면(데스크탑 테스트 카메라) 카메라 자신.
            return cam.transform.root;
        }

        /// <summary>
        /// 리그를 옮기되, "머리"가 도착 지점에 오도록 머리-리그 어긋남을 보정한다.
        /// 보정 없이 리그만 옮기면 방 안에서 걸어 다닌 만큼 도착 위치가 어긋난다.
        /// </summary>
        private void MoveRig(Transform rig, Transform head)
        {
            if (_matchFacing)
            {
                // 머리가 보는 수평 방향을 도착 방향에 맞춘다. 리그를 머리 중심으로 돌려야
                // 몸(리그)만 돌아가고 머리 위치는 유지된다.
                Vector3 headFlat = new Vector3(head.forward.x, 0f, head.forward.z);
                Vector3 destFlat = new Vector3(_destination.forward.x, 0f, _destination.forward.z);
                if (headFlat.sqrMagnitude > 0.0001f && destFlat.sqrMagnitude > 0.0001f)
                {
                    float yaw = Vector3.SignedAngle(headFlat, destFlat, Vector3.up);
                    rig.RotateAround(head.position, Vector3.up, yaw);
                }
            }

            // 머리의 수평 위치를 도착 지점으로 옮긴다(방 안에서 걸어 다닌 만큼의 어긋남 보정).
            Vector3 headOnGround = new Vector3(head.position.x, rig.position.y, head.position.z);
            Vector3 offset = rig.position - headOnGround;

            // 높이: 도착 지점의 바닥을 재서 그 위에 세운다.
            // 예전에는 리그 높이에 정해진 값을 더했다 — 그러면 넘어올 때 어느 높이에 있었는지에
            // 따라 마루 밑에 처박히거나 지붕 속에 들어가, 도착하자마자 화면이 캄캄해졌다.
            float y = rig.position.y + _heightOffset;
            if (_standOnFloor)
            {
                RaycastHit hit;
                Vector3 from = _destination.position + Vector3.up * 3f;
                if (Physics.Raycast(from, Vector3.down, out hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                {
                    bool rigIsHead = rig == head;              // 데스크탑 테스트 카메라
                    y = hit.point.y + (rigIsHead ? _eyeHeight : 0f);
                }
            }
            rig.position = new Vector3(_destination.position.x, y, _destination.position.z) + offset;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, _radius);
            if (_destination != null)
            {
                Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.9f);
                Gizmos.DrawLine(transform.position, _destination.position);
                Gizmos.DrawWireSphere(_destination.position, 0.25f);
                Gizmos.DrawRay(_destination.position, _destination.forward * 0.8f);   // 도착 후 바라볼 방향
            }
        }
    }
}
