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

        [Tooltip("연결하면 도착 자리의 정면 대신 이쪽을 바라본다. " +
                 "사랑방에 들어서는 순간 복동이 앉는 장면이 눈에 들어오게 할 때 쓴다")]
        [SerializeField] private Transform _lookAt;

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

        /// <summary>
        /// 씬을 건너뛴 문을 나중에 물려 준다(<see cref="YardBinder"/> 가 부른다).
        /// 고택이 마당 씬으로 떨어져 나가면서 인스펙터로는 이을 수 없게 되었다 —
        /// 유니티는 씬을 건너뛰는 참조를 저장하지 못한다.
        /// </summary>
        public void BindRequiredDoor(DoorController door) { if (door != null) _requireDoorOpen = door; }
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
        private bool _wasReady;      // 지난 프레임에 "안에 있고 조건도 맞았나"

        private void Update()
        {
            if (_fired && _once) return;

            var cam = Camera.main;
            if (cam == null || _destination == null) return;

            // 플레이어가 반경 안에 있는가(수평 거리만 — 층 높이는 무시)
            Vector3 a = cam.transform.position, b = transform.position;
            a.y = b.y = 0f;
            bool inside = (a - b).sqrMagnitude <= _radius * _radius;

            // 조건이 다 갖춰지는 "순간"에 발동한다.
            //
            // 예전엔 '들어온 순간'만 봤다. 그런데 복동을 바짝 따라가면 내가 먼저 중문 앞에
            // 서 있고 복동은 아직 문간을 넘는 중이라, 그 순간엔 안내자 조건이 걸려 못 나간다.
            // 그 뒤로는 내가 계속 '안에 있는' 상태라 '들어온 순간'이 다시 오지 않아,
            // 복동이 다 지나가도 영영 발동하지 않았다 — 한 발 나갔다 다시 들어와야 했다.
            // 이제는 '안에 있고 + 조건이 맞는' 상태가 되는 순간을 본다.
            bool ready = inside && CanFire();
            if (ready && !_wasReady) Fire(cam);
            _wasReady = ready;
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

                // _lookAt 을 걸어두면 그쪽을 바라보게 한다 — 사랑방에 들어서는 순간
                // 복동이 앉는 장면이 눈에 들어와야 하는데, 도착 자리의 정면이
                // 늘 그쪽을 향한다는 보장이 없다.
                Vector3 destFlat;
                if (_lookAt != null)
                {
                    Vector3 toward = _lookAt.position - _destination.position;
                    destFlat = new Vector3(toward.x, 0f, toward.z);
                    if (destFlat.sqrMagnitude < 0.0001f)
                        destFlat = new Vector3(_destination.forward.x, 0f, _destination.forward.z);
                }
                else destFlat = new Vector3(_destination.forward.x, 0f, _destination.forward.z);
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
