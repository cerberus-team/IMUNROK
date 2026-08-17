using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 복동(가짜 옹덕구 甲) — 안내 NPC:
    ///   [문 앞/안쪽]에 서있기 → (맞이 대사 순번) → (뜸) 사랑방으로 앞장서 Walking → 도착해 서있기
    /// 플레이어는 강제 이동 없이 알아서 따라감(VR 안전).
    ///
    /// 위치 규칙(WYSIWYG): 복동을 둔 자리 = 시작(맞이) 자리. _arriveSpot = 사랑방 도착 자리.
    /// _waypoints = 도착 전 순서대로 지날 길목(모서리 돌 때).
    ///
    /// 애니 상태 이름(복동_AC): Idle(서있기), Walking. 모델에 맞게 인스펙터에서 바꿀 수 있음.
    /// 신호: GateKnockSequence → LeadInside() (맞이 대사 순번에서 호출).
    /// </summary>
    public class BokdongController : MonoBehaviour
    {
        [Header("애니메이터 / 상태 이름")]
        [SerializeField] private Animator _animator;
        [Tooltip("서있기 클립. 복동은 전용 idle이 없어 비워두는 게 맞다(아래 _freezeWalkAsIdle 참고)")]
        [SerializeField] private string _idleState = "";
        [Tooltip("걷기. 복동은 양반걸음이라 기본 Walking 이 아니다")]
        [SerializeField] private string _walkState = "양반걷기";
        [Tooltip("서있기 클립이 없을 때, 걷기 클립의 첫 프레임(선 자세)에서 멈춰 세워 '서있기'로 쓴다(마름과 같은 방식)")]
        [SerializeField] private bool _freezeWalkAsIdle = true;

        [Header("경로 — 사랑방까지")]
        [Tooltip("앞장서 걸어가 설 도착 자리(사랑방 앞). 이 오브젝트 회전 = 도착 방향. 비우면 이동 없음.")]
        [SerializeField] private Transform _arriveSpot;
        [Tooltip("도착 전 순서대로 지날 길목(모서리 돌 때). 비우면 직선.")]
        [SerializeField] private Transform[] _waypoints;

        [Header("시간차 / 속도")]
        [Tooltip("맞이 신호 → 앞장서 걷기 시작까지 뜸")]
        [SerializeField] private float _delayBeforeLead = 0.6f;
        [Tooltip("동작 전환 섞기 시간(초). 클수록 부드럽게 이어짐")]
        [SerializeField] private float _blend = 0.2f;
        [Tooltip("걷는 속도. 양반걸음 클립의 실제 보폭이 2.54m/2.97s = 0.86m/s라 " +
                 "이 값이 어긋나면 발이 바닥에서 미끄러진다")]
        [SerializeField] private float _moveSpeed = 0.86f;
        [SerializeField] private float _turnSpeed = 540f;
        [SerializeField] private float _arriveDist = 0.12f;

        [Header("바닥 붙이기")]
        [Tooltip("켜면 바닥 자동추적(평지용). 돌담·계단은 끄고 경유점 높이를 쓰는 게 자연스러움")]
        [SerializeField] private bool _stickToGround = false;
        [SerializeField] private float _groundOffset = 0f;
        [SerializeField] private float _groundRayUp = 1.6f;
        [SerializeField] private LayerMask _groundMask = ~0;
        [Tooltip("내려갈 때 바닥 따라가는 속도(올라갈 땐 즉시)")]
        [SerializeField] private float _groundFollowSpeed = 5f;

        [Header("도착해서 문 열기 (비우면 그냥 서 있는다)")]
        [Tooltip("도착 자리에서 열어줄 문. 중문의 DoorController 를 연결")]
        [SerializeField] private DoorController _door;
        [Tooltip("문 여는 동작 상태 이름. 없으면 문짝만 열린다")]
        [SerializeField] private string _openState = "open_door_2";
        [Tooltip("도착 → 문에 손대기까지 뜸")]
        [SerializeField] private float _delayBeforeOpen = 0.4f;
        [Tooltip("문이 다 열린 순간 한 번 실행(순간이동 영역 열기 등)")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onDoorOpened;

        [Header("문을 넘어 계속 걸어가기 (비우면 문 앞에서 멈춘다)")]
        [Tooltip("문을 열고 나서 넘어가 설 자리(사랑채 쪽). 앞장서는 사람이 문간에서 " +
                 "멈춰 서 있으면 따라 들어갈 마음이 안 든다 — 넘어가 걸어가야 뒤를 따라간다")]
        [SerializeField] private Transform _throughDoorSpot;
        [Tooltip("넘어갈 때 지날 길목(문간을 비껴 돌 때). 비우면 직선")]
        [SerializeField] private Transform[] _throughDoorWaypoints;
        [Tooltip("문이 열리고 → 넘어가기 시작까지 뜸")]
        [SerializeField] private float _delayAfterOpen = 0.5f;

        private enum Phase { Idle, Leading, Opening, GoingThrough, Arrived }
        private Phase _phase;
        private int _wpIndex;
        private float _wait;
        private System.Action _then;
        private bool _groundInit;
        private Vector3 _mvTarget;
        private Vector3 _mvFrom;
        private bool _mvHasTarget;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            HoldStand();
            _phase = Phase.Idle;
        }

        /// <summary>
        /// 서있기. 전용 idle 클립이 있으면 그것을, 없으면 걷기 클립의 첫 프레임에서 멈춰 세운다.
        /// (복동 모델에는 서있는 클립이 없다 — 양반걸음·문열기·일어서기·달리기뿐)
        /// </summary>
        private void HoldStand()
        {
            if (_animator == null) return;
            if (!string.IsNullOrEmpty(_idleState)) { Play(_idleState); return; }
            if (!_freezeWalkAsIdle || string.IsNullOrEmpty(_walkState)) return;

            _animator.speed = 1f;
            _animator.Play(_walkState, 0, 0f);
            _animator.Update(0f);
            _animator.speed = 0f;
        }

        // ───────── 밖에서 부르는 신호 ─────────

        /// <summary>맞이 대사 순번 → (뜸 후) 사랑방으로 앞장서 걷기.</summary>
        public void LeadInside()
        {
            if (_phase != Phase.Idle) return;
            Delay(_delayBeforeLead, DoLead);
        }

        // ───────── 진행 ─────────

        private void Update()
        {
            if (_wait > 0f)
            {
                _wait -= Time.deltaTime;
                if (_wait <= 0f) { var t = _then; _then = null; t?.Invoke(); }
                return;
            }

            // 문 여는 동작이 끝나면 문짝을 열고 알린다.
            if (_phase == Phase.Opening)
            {
                if (StateDone(_openState)) DoOpened();
                return;
            }

            // 문을 열고 나서 문간을 넘어 사랑채 쪽으로 계속 걸어간다.
            if (_phase == Phase.GoingThrough)
            {
                KeepWalking();
                if (_throughDoorWaypoints != null && _wpIndex < _throughDoorWaypoints.Length && _throughDoorWaypoints[_wpIndex] != null)
                {
                    if (MoveTo(_throughDoorWaypoints[_wpIndex].position, transform.rotation)) _wpIndex++;
                }
                else if (MoveTo(_throughDoorSpot.position, _throughDoorSpot.rotation))
                {
                    transform.rotation = _throughDoorSpot.rotation;
                    HoldStand();
                    _phase = Phase.Arrived;
                }
                return;
            }

            if (_phase != Phase.Leading) return;
            KeepWalking();

            if (_waypoints != null && _wpIndex < _waypoints.Length && _waypoints[_wpIndex] != null)
            {
                if (MoveTo(_waypoints[_wpIndex].position, transform.rotation)) _wpIndex++;
            }
            else if (_arriveSpot == null || MoveTo(_arriveSpot.position, _arriveSpot.rotation))
            {
                if (_arriveSpot != null) transform.rotation = _arriveSpot.rotation;
                if (_door != null) { HoldStand(); Delay(_delayBeforeOpen, DoOpen); }
                else { HoldStand(); _phase = Phase.Arrived; }
            }
        }

        private void DoLead() { _wpIndex = 0; CrossTo(_walkState); _phase = Phase.Leading; }

        /// <summary>문에 손을 뻗는다. 전용 동작이 없으면 곧장 문짝만 연다.</summary>
        private void DoOpen()
        {
            if (_animator == null || string.IsNullOrEmpty(_openState)) { DoOpened(); return; }
            CrossTo(_openState);
            _phase = Phase.Opening;
        }

        /// <summary>문짝을 열고, 이 뒤로 벌어질 일(순간이동 등)에 신호를 준다.</summary>
        private void DoOpened()
        {
            if (_door != null) _door.Open();
            HoldStand();
            _phase = Phase.Arrived;
            _onDoorOpened?.Invoke();
            if (_throughDoorSpot != null) Delay(_delayAfterOpen, DoWalkThrough);
        }

        /// <summary>문간을 넘어 사랑채 쪽으로. 플레이어는 뒤를 따라오다 넘어가게 된다.</summary>
        private void DoWalkThrough()
        {
            _wpIndex = 0;
            _mvHasTarget = false;
            CrossTo(_walkState);
            _phase = Phase.GoingThrough;
        }

        private bool StateDone(string state)
        {
            if (_animator == null) return true;
            var st = _animator.GetCurrentAnimatorStateInfo(0);
            return st.IsName(state) && st.normalizedTime >= 1f;
        }

        // ───────── 헬퍼(마름과 동일) ─────────

        private void Delay(float sec, System.Action then) { _wait = Mathf.Max(0.0001f, sec); _then = then; }

        private bool MoveTo(Vector3 target, Quaternion faceWhenArrived)
        {
            if (!_mvHasTarget || _mvTarget != target)
            {
                _mvTarget = target; _mvFrom = transform.position; _mvHasTarget = true;
            }

            Vector3 pos = transform.position;
            Vector3 flat = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
            float flatDist = flat.magnitude;
            if (flatDist <= _arriveDist)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, faceWhenArrived, _turnSpeed * Time.deltaTime);
                transform.position = new Vector3(pos.x, target.y, pos.z);
                _mvHasTarget = false;
                SnapToGround();
                return true;
            }

            Vector3 dir = flat / flatDist;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(dir, Vector3.up), _turnSpeed * Time.deltaTime);
            Vector3 next = pos + dir * _moveSpeed * Time.deltaTime;

            float total = new Vector2(target.x - _mvFrom.x, target.z - _mvFrom.z).magnitude;
            float remain = new Vector2(target.x - next.x, target.z - next.z).magnitude;
            float frac = total > 0.001f ? Mathf.Clamp01(1f - remain / total) : 1f;
            next.y = Mathf.Lerp(_mvFrom.y, target.y, frac);

            transform.position = next;
            SnapToGround();
            return false;
        }

        private void SnapToGround()
        {
            if (!_stickToGround) return;
            Vector3 origin = transform.position + Vector3.up * _groundRayUp;
            var hits = Physics.RaycastAll(origin, Vector3.down, _groundRayUp + 5f, _groundMask, QueryTriggerInteraction.Ignore);
            Transform ignore = transform.parent != null ? transform.parent : transform;
            float bestDist = float.MaxValue; float bestY = 0f; bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(ignore)) continue;
                if (h.distance < bestDist) { bestDist = h.distance; bestY = h.point.y; found = true; }
            }
            if (!found) return;

            float targetY = bestY + _groundOffset;
            float curY = transform.position.y;
            float y = (!_groundInit || targetY >= curY)
                ? targetY
                : Mathf.MoveTowards(curY, targetY, _groundFollowSpeed * Time.deltaTime);
            _groundInit = true;
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
        }

        private void KeepWalking()
        {
            if (_animator == null) return;
            if (_animator.IsInTransition(0)) return;   // 전환(블렌드) 중엔 안 건드림 → CrossFade 안 끊김
            var st = _animator.GetCurrentAnimatorStateInfo(0);
            if (_animator.speed == 0f || !st.IsName(_walkState) || st.normalizedTime >= 1f)
                Play(_walkState);
        }

        private void Play(string state)
        {
            if (_animator == null || string.IsNullOrEmpty(state)) return;
            _animator.speed = 1f;
            _animator.Play(state, 0, 0f);
        }

        // 부드럽게 섞어 전환(끊김 방지). _blend=0이면 하드컷.
        private void CrossTo(string state)
        {
            if (_animator == null || string.IsNullOrEmpty(state)) return;
            _animator.speed = 1f;
            if (_blend <= 0f) _animator.Play(state, 0, 0f);
            else _animator.CrossFadeInFixedTime(state, _blend, 0);
        }

        // 씬에 경로 표시: 시작(맞이 자리) → 경유점들 → 사랑방 도착. 높이 맞추기 편하게.
        private void OnDrawGizmos()
        {
            Vector3 prev = transform.position;
            Gizmos.color = new Color(0.3f, 0.6f, 1f);          // 시작 = 파랑
            Gizmos.DrawSphere(prev, 0.12f);
            if (_waypoints != null)
                foreach (var w in _waypoints)
                {
                    if (w == null) continue;
                    Gizmos.color = new Color(0.4f, 0.8f, 1f);   // 경유점 = 하늘
                    Gizmos.DrawLine(prev, w.position);
                    Gizmos.DrawSphere(w.position, 0.1f);
                    prev = w.position;
                }
            if (_arriveSpot != null)
            {
                Gizmos.color = new Color(0.6f, 0.4f, 1f);       // 도착 = 보라
                Gizmos.DrawLine(prev, _arriveSpot.position);
                Gizmos.DrawSphere(_arriveSpot.position, 0.13f);
                Gizmos.DrawLine(_arriveSpot.position, _arriveSpot.position + _arriveSpot.forward * 0.4f);
            }
        }
    }
}
