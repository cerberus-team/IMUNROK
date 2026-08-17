using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// 마름 NPC — 단순 버전:
    ///   [네가 둔 자리]에서 서있기 → (문 여는 순번) 문 여는 동작 → (뜸) (경유점들 거쳐) Walking → [_standSpot]에 서기
    ///
    /// _stepAsideSpot 을 걸면 한 단계가 더 붙는다(문지기다운 순서):
    ///   문 여는 동작 → 옆으로 물러서 길 틈 → <b>플레이어가 지나갈 때까지 기다림</b>
    ///   → (PlayerPassed 신호) 문 앞으로 돌아가 문 닫기 → 그제서야 제자리로 가서 앉기
    /// 신호는 중간대문 TeleportZone 의 _onTeleported 에 PlayerPassed() 를 물리면 된다.
    /// 이 자리를 비워두면 예전처럼 문만 열고 바로 제자리로 간다(기존 씬 그대로 동작).
    ///
    /// 위치 규칙(WYSIWYG):
    ///   · 마름을 씬에 둔 자리 = 시작해서 "문 여는 자리".
    ///   · _standSpot = 걸어가서 설 "도착 자리"(그 오브젝트의 회전 = 도착 방향). 비우면 이동 없음.
    ///   · _waypoints = 도착 전에 순서대로 지날 경유점(문을 피해 돌아갈 때). 비우면 직선.
    ///
    /// 애니 상태 이름(마름_AC): opening_door, Walking
    /// 서있는 전용 idle이 없어 '서있기'는 opening_door 첫 프레임(선 자세)에서 멈춰 세운다.
    /// </summary>
    public class MareumController : MonoBehaviour
    {
        [Header("애니메이터 / 상태 이름")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _openState = "opening_door";
        [SerializeField] private string _walkState = "Walking";
        [Tooltip("서 있을 때 멈춰 세울 클립. 그 클립의 첫 프레임이 '선 자세'가 된다. " +
                 "비우면 걷기 클립을 쓴다(문 여는 클립은 손을 뻗은 자세라 안 어울린다)")]
        [SerializeField] private string _standPoseState = "";

        [Header("경로")]
        [Tooltip("문 열고 걸어가서 설/앉을 자리(이 오브젝트의 회전 = 도착 방향). 비우면 이동 없음.")]
        [SerializeField] private Transform _standSpot;
        [Tooltip("도착 전에 순서대로 지날 경유점(문을 피해 돌아갈 때 사용). 비우면 직선.")]
        [SerializeField] private Transform[] _waypoints;

        [Header("길 비켜주기 (비우면 예전처럼 바로 자리로 간다)")]
        [Tooltip("문을 연 뒤 물러설 자리. 문간을 막지 않게 한두 걸음 옆. " +
                 "연결하면 여기서 플레이어가 지나가기를 기다린다")]
        [SerializeField] private Transform _stepAsideSpot;
        [Tooltip("플레이어가 지나간 뒤 닫을 문. 마름이 여는 문(대문)의 DoorController를 연결")]
        [SerializeField] private DoorController _door;
        [Tooltip("문간에 둔 빈 오브젝트. 이 오브젝트의 <b>정면(파란 화살표)이 안쪽</b>을 보게 두면, " +
                 "플레이어가 그 면을 넘어가는 순간 '지나갔다'로 친다. 비우면 PlayerPassed()를 직접 불러야 한다")]
        [SerializeField] private Transform _passSpot;
        [Tooltip("플레이어가 지나갔다는 신호를 받고 문 앞으로 돌아가기까지 뜸")]
        [SerializeField] private float _delayBeforeClose = 0.8f;

        [Header("도착 후 앉기(앉아 졸기)")]
        [Tooltip("도착하면 앉을지. 끄면 그냥 서있음")]
        [SerializeField] private bool _sitOnArrive = true;
        [Tooltip("이 상태를 '거꾸로' 재생해 앉는 동작으로 씀(일어서기 애니 역재생)")]
        [SerializeField] private string _sitDownReverseState = "Sit_to_Stand_Transition_M";
        [Tooltip("앉은 뒤 반복할 상태(앉아 졸기)")]
        [SerializeField] private string _sitIdleState = "Sit_and_Doze_Off";
        [Tooltip("앉는 동작(서서 앉기) 중 캐릭터가 놓일 자리(빈 오브젝트 드래그). 비우면 도착 자리 그대로")]
        [SerializeField] private Transform _sitDownSpot;
        [Tooltip("앉아 졸기 중 캐릭터가 놓일 자리(빈 오브젝트 드래그). 비우면 앉는 자리 그대로")]
        [SerializeField] private Transform _dozeSpot;

        [Header("시간차 (초)")]
        [Tooltip("문 여는 신호 → 실제 여는 동작까지 뜸")]
        [SerializeField] private float _delayBeforeOpen = 0.15f;
        [Tooltip("문 연 뒤 → 걸어가기 시작까지 뜸(문 잡고 잠깐 서 있음)")]
        [SerializeField] private float _delayBeforeWalk = 1.0f;

        [Header("속도")]
        [Tooltip("문 여는 동작 재생 속도(1=정상, 낮을수록 느림). 문짝 속도는 대문 DoorController의 _openDuration에서.")]
        [SerializeField] private float _openSpeed = 1f;
        [Tooltip("동작 전환 섞기 시간(초). 클수록 부드럽게 이어짐(끊김 감소). 0이면 하드컷")]
        [SerializeField] private float _blend = 0.2f;
        [SerializeField] private float _moveSpeed = 1.1f;
        [SerializeField] private float _turnSpeed = 540f;
        [SerializeField] private float _arriveDist = 0.12f;

        [Header("자리 잡은 뒤")]
        [Tooltip("문 열어주는 일이 끝나고 제자리에 앉거나 선 순간 한 번 실행. " +
                 "여기에 마름의 InterrogationController.Unlock 을 걸면 '굳이 다시 찾아왔을 때만' 말을 걸 수 있게 된다")]
        [SerializeField] private UnityEvent _onSettled;

        [Header("바닥 붙이기")]
        [Tooltip("켜면 매 프레임 바닥을 훑어 따라감(평지용). 돌담·계단은 끄고 경유점 높이를 쓰는 게 자연스러움")]
        [SerializeField] private bool _stickToGround = false;
        [Tooltip("발 높이 보정(발이 바닥에 파묻히면 +, 뜨면 -)")]
        [SerializeField] private float _groundOffset = 0f;
        [Tooltip("현재 위치에서 이만큼 위에서 아래로 바닥을 탐색(높은 돌턱이면 키우기)")]
        [SerializeField] private float _groundRayUp = 1.6f;
        [Tooltip("바닥으로 칠 레이어(기본 Everything)")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [Tooltip("내려갈 때 바닥 따라가는 속도(낮을수록 완만). 올라갈 땐 즉시라 파묻힘 없음")]
        [SerializeField] private float _groundFollowSpeed = 5f;

        private enum Phase
        {
            StandAtDoor, Opening,
            StepAside, WaitingForPass, ReturnToDoor, Closing,   // 길 비켜주고 → 기다리고 → 닫으러 돌아가기
            WalkToStand, SittingDown, Sitting, Stood, WalkToDoor
        }
        private Phase _phase;
        private Vector3 _homePos;         // 둔 자리 = 문 여는 시작 자리
        private Quaternion _homeRot;
        private int _wpIndex;
        private float _sitTimer;      // 남은 앉기 시간
        private float _sitDownLen;    // 일어서기 클립 실제 길이(자동 감지)
        private float _sitBlend;      // 걷기→선 자세 블렌드 남은 시간
        private float _closeTimer;    // 문 닫기(여는 동작 역재생) 남은 시간
        private float _closeLen;      // 문 여는 클립 길이
        private float _closeBlend;    // 문 연 끝 자세로 붙는 블렌드 남은 시간

        private float _wait;
        private System.Action _then;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            _homePos = transform.position;
            _homeRot = transform.rotation;
            HoldStand();
            _phase = Phase.StandAtDoor;
        }

        // ───────── 밖에서 부르는 신호 ─────────

        public void BeginGreet() { /* 서 있기 유지 */ }

        /// <summary>정해진 대사 순번 → (뜸 후) 문 여는 동작 → 경유점 거쳐 도착 자리로.</summary>
        public void OpenDoorThenStepAside()
        {
            if (_phase != Phase.StandAtDoor) return;   // 이미 열었거나 진행 중이면 무시
            Delay(_delayBeforeOpen, DoOpen);
        }

        /// <summary>
        /// 플레이어가 문을 지나갔다는 신호. 중간대문 TeleportZone 의 _onTeleported 에 물리면 된다.
        /// 비켜서서 기다리던 마름이 문 앞으로 돌아가 문을 닫고, 그제서야 제자리로 간다.
        /// (_stepAsideSpot 을 안 걸어두면 애초에 기다리지 않으므로 이 신호는 무시된다)
        /// </summary>
        public void PlayerPassed()
        {
            if (_phase != Phase.WaitingForPass) return;
            Delay(_delayBeforeClose, DoReturnToDoor);
        }

        /// <summary>
        /// 플레이어가 문간을 넘어 안쪽으로 들어섰나. _passSpot 의 정면이 '안쪽'이다.
        /// 문 앞에서 서성이는 동안 잘못 걸리지 않게, 문간 좌우로 너무 벗어난 위치는 제외한다.
        /// </summary>
        private bool PlayerCrossedGate()
        {
            var cam = Camera.main;
            if (cam == null) return false;

            Vector3 inward = _passSpot.forward; inward.y = 0f;
            if (inward.sqrMagnitude < 0.0001f) return false;
            inward.Normalize();

            Vector3 toPlayer = cam.transform.position - _passSpot.position;
            toPlayer.y = 0f;

            if (Vector3.Dot(inward, toPlayer) <= 0f) return false;            // 아직 바깥
            // 문간에서 옆으로 크게 벗어나 담을 따라 걷는 중이면 통과로 치지 않는다
            return Vector3.ProjectOnPlane(toPlayer, inward).magnitude <= 3f;
        }

        /// <summary>(선택) 문 여는 자리로 되돌아가 대기.</summary>
        public void ReturnHome()
        {
            if (_phase == Phase.WalkToDoor || _phase == Phase.StandAtDoor) return;
            CrossTo(_walkState);
            _phase = Phase.WalkToDoor;
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

            // 문 닫기: 문 여는 클립을 t=1→0으로 긁어 거꾸로 재생한다(= 닫는 동작).
            // 앉기와 같은 수법 — 전용 클립이 없어 여는 동작을 뒤집어 쓴다.
            if (_phase == Phase.Closing)
            {
                if (_closeBlend > 0f) { _closeBlend -= Time.deltaTime; return; }
                if (_animator != null) _animator.speed = 0f;
                _closeTimer -= Time.deltaTime * Mathf.Max(0.01f, _openSpeed);
                float ct = Mathf.Clamp01(_closeTimer / _closeLen);
                if (_animator != null) { _animator.Play(_openState, 0, ct); _animator.Update(0f); }
                if (_closeTimer <= 0f)
                {
                    if (_standSpot != null) DoWalk();
                    else OnArrived();
                }
                return;
            }

            // 앉기: ①걷기→선 자세로 블렌드(_sitBlend) → ②일어서기 클립을 t=1→0으로 긁어 거꾸로(=앉기)
            if (_phase == Phase.SittingDown)
            {
                if (_sitBlend > 0f) { _sitBlend -= Time.deltaTime; return; }   // 블렌드 동안은 애니메이터가 섞음
                if (_animator != null) _animator.speed = 0f;                   // 스크럽 모드
                _sitTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_sitTimer / _sitDownLen);             // 1 → 0
                if (_animator != null) { _animator.Play(_sitDownReverseState, 0, t); _animator.Update(0f); }
                if (_sitTimer <= 0f)
                {
                    if (_dozeSpot != null)   // 졸기 자리로
                        transform.SetPositionAndRotation(_dozeSpot.position, _dozeSpot.rotation);
                    CrossTo(_sitIdleState);   // 앉기→졸기 부드럽게
                    _phase = Phase.Sitting;
                    Settle();
                }
                return;
            }

            switch (_phase)
            {
                case Phase.Opening:
                    if (StateDone(_openState))
                    {
                        // 비켜설 자리를 걸어뒀으면 자리로 바로 가지 않는다 — 문간을 열어주고 기다린다.
                        if (_stepAsideSpot != null) Delay(_delayBeforeWalk, DoStepAside);
                        else if (_standSpot != null) Delay(_delayBeforeWalk, DoWalk);
                        else OnArrived();
                    }
                    break;

                case Phase.StepAside:
                    KeepWalking();
                    if (MoveTo(_stepAsideSpot.position, _stepAsideSpot.rotation))
                    {
                        transform.rotation = _stepAsideSpot.rotation;
                        HoldStand();
                        _phase = Phase.WaitingForPass;   // 여기서부터 PlayerPassed() 를 기다린다
                    }
                    break;

                case Phase.WaitingForPass:
                    // 문 옆에 서서 기다리는 중. 문간을 넘어가는 순간을 스스로 본다.
                    if (_passSpot != null && PlayerCrossedGate()) PlayerPassed();
                    break;

                case Phase.ReturnToDoor:
                    KeepWalking();
                    if (MoveTo(_homePos, _homeRot))
                    {
                        transform.rotation = _homeRot;
                        DoClose();
                    }
                    break;


                case Phase.WalkToStand:
                    KeepWalking();   // 이동 내내 걷기 애니 유지
                    // 남은 경유점이 있으면 먼저 들르고, 없으면 도착 자리로.
                    if (_waypoints != null && _wpIndex < _waypoints.Length && _waypoints[_wpIndex] != null)
                    {
                        if (MoveTo(_waypoints[_wpIndex].position, transform.rotation)) _wpIndex++;
                    }
                    else if (MoveTo(_standSpot.position, _standSpot.rotation))
                    {
                        transform.rotation = _standSpot.rotation;   // 도착 방향 정확히 맞춤
                        OnArrived();
                    }
                    break;

                case Phase.WalkToDoor:
                    KeepWalking();
                    if (MoveTo(_homePos, _homeRot))
                    {
                        transform.rotation = _homeRot;
                        HoldStand(); _phase = Phase.StandAtDoor;
                    }
                    break;
            }
        }

        // ───────── 단계 동작 ─────────

        private void DoOpen()
        {
            CrossTo(_openState);
            if (_animator != null) _animator.speed = Mathf.Max(0.01f, _openSpeed);   // 문 여는 동작만 느리게
            _phase = Phase.Opening;
        }

        private void DoWalk() { _wpIndex = 0; CrossTo(_walkState); _phase = Phase.WalkToStand; }

        /// <summary>문을 열어둔 채 한두 걸음 물러서 길을 튼다.</summary>
        private void DoStepAside() { CrossTo(_walkState); _phase = Phase.StepAside; }

        /// <summary>플레이어가 지나갔다 — 문 앞으로 되돌아간다.</summary>
        private void DoReturnToDoor() { CrossTo(_walkState); _phase = Phase.ReturnToDoor; }

        /// <summary>
        /// 문을 닫는다. 전용 클립이 없으니 문 여는 동작을 <b>거꾸로</b> 돌린다 —
        /// 그냥 정방향으로 틀면 닫으면서 여는 시늉을 하게 된다. 문짝은 DoorController가 돌린다.
        /// </summary>
        private void DoClose()
        {
            if (_door != null) _door.Close();

            _closeLen = ClipLength(_openState, 1.5f);
            if (_animator != null)
            {
                _animator.speed = 1f;
                // 문을 연 '끝 자세'로 먼저 붙였다가, 다음 Update부터 t=1→0으로 긁는다.
                _animator.CrossFadeInFixedTime(_openState, _blend, 0, _closeLen);
            }
            _closeBlend = _blend;
            _closeTimer = _closeLen;
            _phase = Phase.Closing;
        }

        // 도착 처리: 앉기 켜졌으면 앉고, 아니면 서있기.
        private void OnArrived()
        {
            if (_sitOnArrive) DoSitDown();          // 앉기가 끝나는 순간에 Settle()
            else { HoldStand(); _phase = Phase.Stood; Settle(); }
        }

        // 제 볼일이 끝나 자리를 잡은 순간(한 번만). 여기서부터는 플레이어가 붙잡고 말을 걸어도 된다.
        private bool _settled;
        private void Settle()
        {
            if (_settled) return;
            _settled = true;
            _onSettled?.Invoke();
        }

        // 일어서기 클립을 프레임 직접 스크럽으로 거꾸로 재생 → 앉는 동작(클립 원래 속도).
        private void DoSitDown()
        {
            if (_sitDownSpot != null)   // 앉는 동작 자리로
                transform.SetPositionAndRotation(_sitDownSpot.position, _sitDownSpot.rotation);

            _sitDownLen = ClipLength(_sitDownReverseState, 1.2f);   // 재생 안 하고 길이만 조회(블렌드 유지)
            if (_animator != null)
            {
                _animator.speed = 1f;
                // 걷기 → 일어서기의 '끝(선 자세)'으로 부드럽게 블렌드. 그다음 Update에서 거꾸로 스크럽.
                _animator.CrossFadeInFixedTime(_sitDownReverseState, _blend, 0, _sitDownLen);
            }
            _sitBlend = _blend;
            _sitTimer = _sitDownLen;
            _phase = Phase.SittingDown;
        }

        // 컨트롤러에서 클립 길이를 재생 없이 조회(전환 블렌드가 안 끊기게).
        private float ClipLength(string name, float fallback)
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
                foreach (var c in _animator.runtimeAnimatorController.animationClips)
                    if (c != null && c.name == name && c.length > 0.05f) return c.length;
            return fallback;
        }

        // 서있는 idle이 없어 어떤 클립의 첫 프레임에서 멈춰 세워 '서있기'로 쓴다.
        // 문 여는 클립의 첫 프레임은 이미 문에 손을 뻗는 자세라 가만히 서 있는 것으로 안 읽힌다 —
        // 걷기 클립의 첫 프레임이 훨씬 자연스럽다.
        private void HoldStand()
        {
            if (_animator == null) return;
            string pose = string.IsNullOrEmpty(_standPoseState) ? _walkState : _standPoseState;
            if (string.IsNullOrEmpty(pose)) return;
            _animator.speed = 1f;
            _animator.Play(pose, 0, 0f);
            _animator.Update(0f);
            _animator.speed = 0f;
        }

        // ───────── 헬퍼 ─────────

        private void Delay(float sec, System.Action then) { _wait = Mathf.Max(0.0001f, sec); _then = then; }

        private Vector3 _mvTarget;
        private Vector3 _mvFrom;
        private bool _mvHasTarget;

        private bool MoveTo(Vector3 target, Quaternion faceWhenArrived)
        {
            // 새 목표면 이 구간의 시작점 기록(높이 보간 기준)
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

            // Y는 '수평 진행률'로 보간 → 점프 없이 경사처럼 부드럽게 오르내림
            float total = new Vector2(target.x - _mvFrom.x, target.z - _mvFrom.z).magnitude;
            float remain = new Vector2(target.x - next.x, target.z - next.z).magnitude;
            float frac = total > 0.001f ? Mathf.Clamp01(1f - remain / total) : 1f;
            next.y = Mathf.Lerp(_mvFrom.y, target.y, frac);

            transform.position = next;
            SnapToGround();   // _stickToGround 켜졌을 때만(끄면 위 보간 높이 사용)
            return false;
        }

        // 바닥으로 레이를 쏴 발을 바닥에 붙인다(붕 뜸 방지 + 돌턱 내려감).
        // 자기 자신·마커 큐브 콜라이더는 무시(안 그러면 그것들 위로 떠버림).
        private void SnapToGround()
        {
            if (!_stickToGround) return;
            Vector3 origin = transform.position + Vector3.up * _groundRayUp;
            var hits = Physics.RaycastAll(origin, Vector3.down, _groundRayUp + 5f, _groundMask, QueryTriggerInteraction.Ignore);
            Transform ignore = transform.parent != null ? transform.parent : transform;   // 마커 큐브(부모) 이하 전부 무시
            float bestDist = float.MaxValue; float bestY = 0f; bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(ignore)) continue;   // 자기/마커 제외
                if (h.distance < bestDist) { bestDist = h.distance; bestY = h.point.y; found = true; }   // 발 바로 밑 바닥
            }
            if (!found) return;

            float targetY = bestY + _groundOffset;
            float curY = transform.position.y;
            // 올라갈 땐 즉시(파묻힘 방지), 내려갈 땐 부드럽게(돌턱 자연스럽게)
            float y = (!_groundInit || targetY >= curY)
                ? targetY
                : Mathf.MoveTowards(curY, targetY, _groundFollowSpeed * Time.deltaTime);
            _groundInit = true;
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
        }
        private bool _groundInit;

        // 이동 중 걷기 애니가 끊기지 않게 유지(클립이 Loop 아니어도 다시 재생).
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

        private bool StateDone(string state)
        {
            if (_animator == null) return true;
            var st = _animator.GetCurrentAnimatorStateInfo(0);
            return st.IsName(state) && st.normalizedTime >= 1f;
        }

        // 씬에 경로 표시: 시작(문 여는 자리) → 경유점들 → 도착 자리. 높이 맞추기 편하게.
        private void OnDrawGizmos()
        {
            Vector3 prev = transform.position;
            Gizmos.color = new Color(1f, 0.6f, 0.1f);          // 시작 = 주황
            Gizmos.DrawSphere(prev, 0.12f);
            if (_waypoints != null)
                foreach (var w in _waypoints)
                {
                    if (w == null) continue;
                    Gizmos.color = new Color(1f, 0.85f, 0.2f);  // 경유점 = 노랑
                    Gizmos.DrawLine(prev, w.position);
                    Gizmos.DrawSphere(w.position, 0.1f);
                    prev = w.position;
                }
            if (_standSpot != null)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 0.35f);    // 도착 = 초록
                Gizmos.DrawLine(prev, _standSpot.position);
                Gizmos.DrawSphere(_standSpot.position, 0.13f);
                Gizmos.DrawLine(_standSpot.position, _standSpot.position + _standSpot.forward * 0.4f);  // 도착 방향
            }
            if (_sitDownSpot != null)   // 앉는 자리 = 자홍
            {
                Gizmos.color = new Color(1f, 0.3f, 0.8f);
                Gizmos.DrawSphere(_sitDownSpot.position, 0.1f);
                Gizmos.DrawLine(_sitDownSpot.position, _sitDownSpot.position + _sitDownSpot.forward * 0.3f);
            }
            if (_dozeSpot != null)      // 졸기 자리 = 청록
            {
                Gizmos.color = new Color(0.3f, 1f, 1f);
                Gizmos.DrawSphere(_dozeSpot.position, 0.1f);
                Gizmos.DrawLine(_dozeSpot.position, _dozeSpot.position + _dozeSpot.forward * 0.3f);
            }
        }
    }
}
