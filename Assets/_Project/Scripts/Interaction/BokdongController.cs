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
        [Tooltip("서있기 클립이 없을 때, 걷기 클립의 한 프레임에서 멈춰 세워 '서있기'로 쓴다(마름과 같은 방식)")]
        [SerializeField] private bool _freezeWalkAsIdle = true;

        [Tooltip("걷기 클립의 몇 지점(0~1)에서 멈출지. 첫 프레임은 한 발이 들려 있어 " +
                 "걷다 만 것처럼 보인다 — 두 발이 다 땅에 닿는 순간을 찾아 넣을 것. " +
                 "양반걷기는 0.840 에서 두 발 높이차가 0.000, 앞뒤로도 거의 안 벌어진다")]
        [SerializeField] private float _standAtNormalized = 0f;

        [Header("맞이하러 나오기 (비우면 처음부터 그 자리에 서 있는다)")]
        [Tooltip("대문이 열리면 걸어나와 설 자리(안마당). 복동을 씬에 둔 자리 = 나오기 전 " +
                 "서 있던 곳(중문 안쪽). 대문 DoorController 의 OnOpened 에 ComeOutToGreet 을 걸면 된다")]
        [SerializeField] private Transform _greetSpot;
        [Tooltip("나오기 전 지날 길목. 비우면 직선")]
        [SerializeField] private Transform[] _greetWaypoints;
        [Tooltip("대문이 열리고 → 걸어나오기까지 뜸")]
        [SerializeField] private float _delayBeforeGreet = 0.4f;

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

        [Tooltip("도착 이만큼 앞에서부터 걸음을 늦춘다(m). 0이면 끝까지 같은 속도로 오다 뚝 선다 — " +
                 "그게 어색하다. 걷는 속도와 동작 속도를 함께 줄여야 발이 안 미끄러진다")]
        [SerializeField] private float _slowDownDist = 1.2f;
        [Tooltip("늦춘 끝의 속도 비율(0.3 = 원래의 30%)")]
        [SerializeField] private float _slowDownTo = 0.3f;
        [Tooltip("멈출 때 선 자세로 섞이는 시간(초). 0이면 하드컷")]
        [SerializeField] private float _standBlend = 0.35f;

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

        [Header("사랑방에 앉기")]
        [Tooltip("이 상태를 '거꾸로' 돌려 앉는 동작으로 쓴다(일어서기 애니 역재생). 마름과 같은 수")]
        [SerializeField] private string _sitDownReverseState = "Stand_Up3";
        [Tooltip("앉을 자리(보료 위). 비우면 선 자리에서 그대로 앉는다")]
        [SerializeField] private Transform _sitSpot;
        [Tooltip("앉는 동작 앞에 걷기→선 자세로 섞는 시간")]
        [SerializeField] private float _sitBlend = 0.2f;
        [Tooltip("앉은 높이 미세 조정(m). 몸이 자리에 파묻히면 올리고, 떠 있으면 내린다")]
        [SerializeField] private float _sitYOffset = 0f;
        [Tooltip("앉는 속도 배수. 일어서기 클립이 6초라 그대로 거꾸로 돌리면 느릿하다")]
        [SerializeField] private float _sitSpeed = 1.4f;
        [Tooltip("앉은 뒤 상체를 뒤로 기울이는 각도(도). 등 뒤 안석에 기댄 것처럼 보이게 한다. 0이면 꼿꼿이 앉는다")]
        [SerializeField] private float _leanBack = 14f;
        [Tooltip("기울일 등뼈. 비우면 이름으로 찾는다(Spine, Spine01, Spine02)")]
        [SerializeField] private Transform[] _leanBones;
        [Tooltip("넘어갈 때 지날 길목(문간을 비껴 돌 때). 비우면 직선")]
        [SerializeField] private Transform[] _throughDoorWaypoints;
        [Tooltip("문이 열리고 → 넘어가기 시작까지 뜸")]
        [SerializeField] private float _delayAfterOpen = 0.5f;

        [Tooltip("문을 넘어 마지막 자리까지 다 걸어가 선 순간 한 번 실행. " +
                 "순간이동으로 방에 들여보내던 시절에는 도착이라는 것이 없었다 — " +
                 "이제 손님이 뒤따라 걸어 들어오므로, 그가 보료에 가 앉는 것도 걸음의 끝이다")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onArrived;

        [Header("심문이 끝나면 물러가기 (비우면 앉은 채로 있는다)")]
        [Tooltip("일어서기 동작. 앉을 때 이것을 거꾸로 돌렸으니, 일어설 땐 바로 돌린다")]
        [SerializeField] private string _standUpState = "Stand_Up3";
        [Tooltip("심문창이 닫히고 → 일어서기까지 뜸. 마지막 한 마디를 듣고 나서 일어서야 한다")]
        [SerializeField] private float _delayBeforeLeave = 1.2f;
        [Tooltip("나가려고 서는 자리(문 앞). 이 오브젝트의 정면 = 문을 향한 방향")]
        [SerializeField] private Transform _leaveDoorSpot;
        [Tooltip("나가는 길에 지날 길목. 비우면 직선")]
        [SerializeField] private Transform[] _leaveWaypoints;
        [Tooltip("나가면서 열 문. 비우면 그냥 걸어 나간다")]
        [SerializeField] private DoorController _leaveDoor;
        [Tooltip("나갈 때 문 여는 동작. 비워 두면 손동작 없이 그냥 지나간다 — " +
                 "제대로 된 동작이 나오기 전까지는 어설픈 시늉을 넣는 것보다 없는 편이 낫다")]
        [SerializeField] private string _leaveOpenState = "";
        [Tooltip("문 앞에 서고 → 문에 손대기까지 뜸")]
        [SerializeField] private float _leaveOpenDelay = 0.4f;
        [Tooltip("문을 넘어가 설 자리(툇마루 쪽). 비우면 문간에서 사라진다")]
        [SerializeField] private Transform _leaveThroughSpot;
        [Tooltip("넘어가 서고 → 문을 도로 닫기까지 뜸. 열어 둔 채 가면 방이 열린 채로 남는다")]
        [SerializeField] private float _leaveCloseDelay = 0.6f;
        [Tooltip("문이 닫히고 → 몸을 치우기까지(초). 문짝 뒤로 가려진 다음에 없애야 한다")]
        [SerializeField] private float _hideDelay = 1.4f;
        [Tooltip("다 나간 뒤 몸을 끌지. 끄면 문 밖에 그대로 서 있는다")]
        [SerializeField] private bool _hideWhenGone = true;
        [Tooltip("다 나간 순간 한 번 실행. 그가 없어야 열리는 것을 여기에 건다 — " +
                 "보료 들추기, 플레이어 일어서기")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onLeft;

        private enum Phase
        {
            Idle, Greeting, Leading, Opening, GoingThrough, Arrived, SittingDown, Seated,
            StandingUp, Leaving, OpeningExit, GoingOut, Gone
        }
        private Phase _phase;
        private int _wpIndex;
        private float _wait;
        private System.Action _then;
        private bool _groundInit;
        private Vector3 _mvTarget;
        private Vector3 _mvFrom;
        private bool _mvHasTarget;
        private float _standBlendLeft;   // 선 자세로 섞이는 중 남은 시간
        private float _sitTimer;         // 앉기(일어서기 역재생) 남은 시간
        private float _sitLen;           // 일어서기 클립 길이

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

            float at = Mathf.Repeat(_standAtNormalized, 1f);
            if (_standBlend <= 0.001f)
            {
                _animator.speed = 1f;
                _animator.Play(_walkState, 0, at);
                _animator.Update(0f);
                _animator.speed = 0f;
                return;
            }

            // 걷다 말고 한 프레임으로 뚝 자르면 어색하다. 선 자세로 섞어 들어가되,
            // 섞이는 동안에도 클립이 흐르므로 그만큼 앞에서 시작해 끝나는 순간 딱 그 프레임에 선다.
            float len = ClipLength(_walkState);
            float startAt = len > 0.01f ? Mathf.Repeat(at - _standBlend / len, 1f) : at;
            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(_walkState, _standBlend, 0, startAt * Mathf.Max(0.01f, len));
            _standBlendLeft = _standBlend;
        }

        /// <summary>컨트롤러에서 클립 길이를 재생 없이 조회.</summary>
        private float ClipLength(string state)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return 0f;
            foreach (var c in _animator.runtimeAnimatorController.animationClips)
                if (c != null && c.name == state && c.length > 0.05f) return c.length;
            return 0f;
        }

        // ───────── 밖에서 부르는 신호 ─────────

        /// <summary>
        /// 대문이 열렸다 — 중문 쪽에서 안마당으로 걸어나와 손님을 맞는 자리에 선다.
        /// 처음부터 마당 한복판에 뒷짐 지고 서 있으면 "기다리고 있었다"가 되어 어색하다.
        /// </summary>
        public void ComeOutToGreet()
        {
            if (_phase != Phase.Idle || _greetSpot == null) return;
            Delay(_delayBeforeGreet, DoGreet);
        }

        /// <summary>
        /// 손님이 대문을 넘었다 — 사랑방으로 앞장선다.
        ///
        /// 걸어나오던 중에 이 신호가 와도 받는다. 예전엔 Idle 일 때만 받아서,
        /// 손님이 문 열리자마자 성큼 들어서면 이 신호가 통째로 버려졌다.
        /// 복동은 맞이 자리까지 걸어가 선 채로 영영 굳어 있었다.
        /// 이제는 걷던 자리에서 그대로 돌아서 앞장선다 — 맞이 자리까지 갔다가
        /// 되돌아 나오는 군더더기가 없다.
        /// </summary>
        public void LeadInside()
        {
            if (_phase == Phase.Greeting)
            {
                _wait = 0f; _then = null;                  // 걸어나오기를 그만둔다
                if (_animator != null) _animator.speed = 1f;   // 도착하며 늦춰둔 걸음을 되돌린다
                DoLead();
                return;
            }
            if (_phase != Phase.Idle) return;
            Delay(_delayBeforeLead, DoLead);
        }

        /// <summary>
        /// 사랑방에 앉는다. 앉는 클립이 따로 없으므로 <b>일어서기를 거꾸로</b> 돌려 쓴다
        /// (마름이 이미 쓰는 수다). 실내 씬이 올라온 순간에 부르면 된다 —
        /// 화면이 어두워졌다 밝아지는 사이라 자리를 옮겨도 눈에 띄지 않는다.
        /// </summary>
        public void SitDown()
        {
            if (_phase == Phase.SittingDown || _phase == Phase.Seated) return;

            _wait = 0f; _then = null;
            if (_sitSpot != null)
            {
                transform.position = _sitSpot.position;
                transform.rotation = _sitSpot.rotation;
                _groundInit = false;                    // 새 자리에서 발밑을 다시 잡는다
            }

            // 발 붙이기는 선 자세를 기준으로 보정한다 — 앉은 자세엔 그 보정이 안 맞아
            // 몸이 마루 밑으로 44cm 꺼진다. 앉는 동안은 꺼 두고 자리를 직접 잡는다.
            // 부품을 통째로 끄면 몸을 자리에 붙들어 두는 수평 고정까지 꺼져서
            // 앉기 클립의 원점 어긋남 때문에 몸이 2m 넘게 밀려난다. 높이만 끈다.
            var feet = GetComponent<GroundFeet>();
            if (feet != null) feet.PinHeight = false;

            _sitLen = Mathf.Max(0.05f, ClipLength(_sitDownReverseState) / Mathf.Max(0.1f, _sitSpeed));
            _sitTimer = _sitLen;
            if (_animator != null) { _animator.speed = 1f; CrossTo(_sitDownReverseState); }
            _standBlendLeft = 0f;
            _phase = Phase.SittingDown;
        }

        /// <summary>
        /// 심문이 끝났다 — 일어서서 문을 열고 나간다.
        ///
        /// 앉은 사람이 대사만 남기고 그 자리에 계속 앉아 있으면, 플레이어는 그가 보는
        /// 앞에서 방을 뒤지게 된다. 그래서 <b>몸이 실제로 나가야</b> 한다 — 일어서고,
        /// 문 앞까지 걸어가고, 문을 열고, 넘어가고, 문을 닫는다. 그가 문을 닫는 소리가
        /// 조사를 시작해도 좋다는 신호다.
        ///
        /// 심문창의 '닫는 순간' 이벤트에 걸면 된다. 나가는 길과 문은 인스펙터에서 준다.
        /// </summary>
        public void LeaveRoom()
        {
            if (_phase == Phase.Gone || _phase == Phase.StandingUp || _phase == Phase.Leaving ||
                _phase == Phase.OpeningExit || _phase == Phase.GoingOut) return;

            _wait = 0f; _then = null;
            Delay(_delayBeforeLeave, DoStandUp);
        }

        /// <summary>지금 이 방을 떠났나(보료를 들출 수 있는가).</summary>
        public bool HasLeft => _phase == Phase.Gone;

        /// <summary>
        /// 다 앉은 뒤 한 번만 — 몸이 자리(보료 또는 마루)에 닿게 높이를 맞춘다.
        ///
        /// 메시 경계로 맞추면 안 된다. 한복 도포 자락이 앉은 몸보다 한참 아래로 드리워서,
        /// 그 옷자락을 바닥에 맞추면 정작 몸은 위로 떠 버린다("보료 위에 떠 있다").
        /// 그래서 옷이 아니라 <b>가장 낮은 뼈</b>를 기준으로 삼는다 — 서 있을 때 발을
        /// 바닥에 붙이는 GroundFeet 과 같은 방식이다.
        ///
        /// 자리 높이는 밑에 깔린 것 중 가장 높은 면을 쓴다. 보료에 콜라이더가 있으면
        /// 방석 위에, 없으면 마루에 앉는다.
        /// </summary>
        private void SeatOnFloor()
        {
            var sk = GetComponentInChildren<SkinnedMeshRenderer>();
            if (sk == null || sk.bones == null || sk.bones.Length == 0) return;
            sk.updateWhenOffscreen = true;

            float lowest = float.MaxValue;
            for (int i = 0; i < sk.bones.Length; i++)
            {
                var b = sk.bones[i];
                if (b == null) continue;
                if (b.position.y < lowest) lowest = b.position.y;
            }
            if (lowest == float.MaxValue) return;

            // 앉을 면은 광선으로 찾지 않는다. 보료에는 콜라이더가 없어서 광선이 그 밑
            // 마루(-0.80)를 짚고, 그러면 보료 윗면(-0.65)보다 15cm 파묻힌 채 앉는다.
            // 자리 표식(甲_보료자리)이 이미 보료 윗면 높이에 놓여 있으므로 그 값을 쓴다.
            float surface;
            if (_sitSpot != null) surface = _sitSpot.position.y;
            else
            {
                Vector3 from = transform.position + Vector3.up * 1.5f;
                var hits = Physics.RaycastAll(from, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
                surface = float.MinValue;
                for (int i = 0; i < hits.Length; i++)
                    if (hits[i].point.y > surface) surface = hits[i].point.y;
                if (surface == float.MinValue) return;
            }

            float lift = (surface + _sitYOffset) - lowest;
            if (Mathf.Abs(lift) > 0.003f) transform.position += Vector3.up * lift;
        }

        // ───────── 진행 ─────────

        /// <summary>
        /// 앉아 있는 동안 상체를 뒤로 살짝 젖힌다 — 등 뒤 안석에 기댄 모양새.
        ///
        /// 애니메이터가 자세를 쓴 <b>뒤</b>에 손대야 한다(LateUpdate). Update 에서 돌리면
        /// 다음 프레임에 애니메이터가 그대로 덮어써서 아무 일도 일어나지 않는다.
        /// 앉기 클립은 꼿꼿이 앉은 자세뿐이라, 기대는 자세는 이렇게 만들어 쓴다.
        /// </summary>
        private void LateUpdate()
        {
            if (_phase != Phase.Seated || Mathf.Abs(_leanBack) < 0.01f) return;
            if (_leanBones == null || _leanBones.Length == 0) CacheLeanBones();
            if (_leanBones == null || _leanBones.Length == 0) return;
            if (_leanBase == null || _leanBase.Length != _leanBones.Length) return;

            // 먼저 앉은 그대로의 자세로 되돌린 다음 젖힌다.
            // 그냥 매 프레임 Rotate 만 하면 각도가 쌓여서, 몇 초 뒤엔 병풍에 처박힌다
            // (실제로 머리가 x 6.05 → 5.12 까지 젖혀졌다).
            for (int i = 0; i < _leanBones.Length; i++)
                if (_leanBones[i] != null) _leanBones[i].localRotation = _leanBase[i];

            // 등뼈 여러 마디에 나눠 걸어야 한 마디만 꺾이지 않고 자연스럽게 휜다.
            float each = _leanBack / _leanBones.Length;
            for (int i = 0; i < _leanBones.Length; i++)
                if (_leanBones[i] != null)
                    _leanBones[i].Rotate(transform.right, -each, Space.World);

            SeatOnFloor();      // 젖힌 만큼 몸이 내려앉으므로 높이를 다시 맞춘다
        }

        /// <summary>앉은 그대로의 등뼈 각도. 젖힘은 늘 여기서 다시 계산한다(쌓이면 안 된다).</summary>
        private Quaternion[] _leanBase;

        private void CacheLeanBones()
        {
            var found = new System.Collections.Generic.List<Transform>();
            var want = new string[] { "spine", "spine01", "spine02" };
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                for (int i = 0; i < want.Length; i++)
                    if (n == want[i]) { found.Add(t); break; }
            }
            _leanBones = found.ToArray();
            _leanBase = new Quaternion[_leanBones.Length];
            for (int i = 0; i < _leanBones.Length; i++)
                _leanBase[i] = _leanBones[i].localRotation;
        }

        private void Update()
        {
            // 선 자세로 섞이는 중 — 다 섞이면 그 프레임에 멈춘다
            if (_standBlendLeft > 0f)
            {
                _standBlendLeft -= Time.deltaTime;
                if (_standBlendLeft <= 0f)
                {
                    float len = ClipLength(_walkState);
                    _animator.speed = 1f;
                    _animator.Play(_walkState, 0, Mathf.Repeat(_standAtNormalized, 1f));
                    _animator.Update(0f);
                    _animator.speed = 0f;
                }
            }

            if (_wait > 0f)
            {
                _wait -= Time.deltaTime;
                if (_wait <= 0f) { var t = _then; _then = null; t?.Invoke(); }
                return;
            }

            // 앉는 중 — 일어서기 클립을 t=1 → 0 으로 긁어 거꾸로 돌린다.
            // 음수 속도로 틀면 비반복 상태에서 어긋나는 일이 있어 직접 긁는다(마름과 같은 방식).
            if (_phase == Phase.SittingDown)
            {
                if (_sitBlend > 0f && _sitTimer > _sitLen - _sitBlend) { _sitTimer -= Time.deltaTime; return; }
                if (_animator != null) _animator.speed = 0f;
                _sitTimer -= Time.deltaTime;
                float k = Mathf.Clamp01(_sitTimer / _sitLen);
                if (_animator != null) { _animator.Play(_sitDownReverseState, 0, k); _animator.Update(0f); }
                if (_sitTimer <= 0f)
                {
                    _phase = Phase.Seated;
                    CacheLeanBones();      // 앉은 자세를 기준으로 삼는다
                    SeatOnFloor();
                }
                return;
            }

            if (_phase == Phase.Seated) return;                  // 앉아 있는 동안은 가만히
            if (_phase == Phase.Gone) return;                    // 이미 나갔다

            // 일어서는 중 — 앉을 때 거꾸로 돌린 클립을 이번엔 바로 돌린다.
            if (_phase == Phase.StandingUp)
            {
                if (StateDone(_standUpState)) DoLeaveWalk();
                return;
            }

            // 문 앞까지 걸어간다.
            if (_phase == Phase.Leaving)
            {
                KeepWalking();
                if (_leaveWaypoints != null && _wpIndex < _leaveWaypoints.Length && _leaveWaypoints[_wpIndex] != null)
                {
                    if (MoveTo(_leaveWaypoints[_wpIndex].position, transform.rotation)) _wpIndex++;
                }
                else if (_leaveDoorSpot == null || MoveTo(_leaveDoorSpot.position, _leaveDoorSpot.rotation))
                {
                    if (_leaveDoorSpot != null) transform.rotation = _leaveDoorSpot.rotation;
                    HoldStand();
                    if (_leaveDoor != null) Delay(_leaveOpenDelay, DoLeaveOpen);
                    else DoLeaveThrough();
                }
                return;
            }

            // 나가는 문을 여는 동작이 끝나기를 기다린다.
            if (_phase == Phase.OpeningExit)
            {
                if (StateDone(_leaveOpenState)) DoLeaveOpened();
                return;
            }

            // 문간을 넘어 툇마루 쪽으로.
            if (_phase == Phase.GoingOut)
            {
                KeepWalking();
                if (MoveTo(_leaveThroughSpot.position, _leaveThroughSpot.rotation))
                {
                    transform.rotation = _leaveThroughSpot.rotation;
                    HoldStand();
                    Delay(_leaveCloseDelay, DoLeaveClose);
                }
                return;
            }


            // 중문 쪽에서 안마당으로 걸어나오는 중.
            if (_phase == Phase.Greeting)
            {
                KeepWalking();
                if (_greetWaypoints != null && _wpIndex < _greetWaypoints.Length && _greetWaypoints[_wpIndex] != null)
                {
                    if (MoveTo(_greetWaypoints[_wpIndex].position, transform.rotation)) _wpIndex++;
                }
                else if (MoveTo(_greetSpot.position, _greetSpot.rotation))
                {
                    transform.rotation = _greetSpot.rotation;
                    HoldStand();
                    _phase = Phase.Idle;      // 여기서부터 다시 LeadInside 를 받을 수 있다
                }
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
                    _onArrived?.Invoke();
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

        private void DoGreet() { _wpIndex = 0; _mvHasTarget = false; CrossTo(_walkState); _phase = Phase.Greeting; }

        private void DoLead() { _wpIndex = 0; _mvHasTarget = false; CrossTo(_walkState); _phase = Phase.Leading; }

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

        // ───────── 물러가기 ─────────

        /// <summary>일어선다. 일어서기 클립이 없으면 그냥 선 자세로 돌아간다.</summary>
        private void DoStandUp()
        {
            // 앉힐 때 꺼 둔 발 붙이기를 되돌린다. 이걸 안 켜면 서서 걷는 내내
            // 앉은 자세로 잡아 둔 높이를 그대로 끌고 다녀 마루 위에 떠 보인다.
            var feet = GetComponent<GroundFeet>();
            if (feet != null) feet.PinHeight = true;

            _phase = Phase.StandingUp;
            if (_animator == null || string.IsNullOrEmpty(_standUpState)) { DoLeaveWalk(); return; }
            _animator.speed = Mathf.Max(0.1f, _sitSpeed);
            CrossTo(_standUpState);
            _animator.speed = Mathf.Max(0.1f, _sitSpeed);   // CrossTo 가 1로 되돌려 놓는다
        }

        private void DoLeaveWalk()
        {
            if (_animator != null) _animator.speed = 1f;
            _wpIndex = 0;
            _mvHasTarget = false;
            _groundInit = false;
            if (_leaveDoorSpot == null && _leaveThroughSpot == null) { DoLeaveGone(); return; }
            CrossTo(_walkState);
            _phase = Phase.Leaving;
        }

        /// <summary>문에 손을 뻗는다. 전용 동작이 없으면 곧장 문짝만 연다.</summary>
        private void DoLeaveOpen()
        {
            if (_animator == null || string.IsNullOrEmpty(_leaveOpenState)) { DoLeaveOpened(); return; }
            CrossTo(_leaveOpenState);
            _phase = Phase.OpeningExit;
        }

        private void DoLeaveOpened()
        {
            if (_leaveDoor != null) _leaveDoor.Open();
            HoldStand();
            _phase = Phase.Arrived;
            Delay(_delayAfterOpen, DoLeaveThrough);
        }

        private void DoLeaveThrough()
        {
            if (_leaveThroughSpot == null) { DoLeaveClose(); return; }
            _mvHasTarget = false;
            CrossTo(_walkState);
            _phase = Phase.GoingOut;
        }

        /// <summary>넘어간 뒤 문을 도로 닫는다. 이 소리가 조사를 시작해도 좋다는 신호다.</summary>
        private void DoLeaveClose()
        {
            if (_leaveDoor != null) _leaveDoor.Close();
            Delay(_hideDelay, DoLeaveGone);
        }

        private void DoLeaveGone()
        {
            _phase = Phase.Gone;
            _onLeft?.Invoke();
            if (_hideWhenGone) gameObject.SetActive(false);
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
            // 도착이 가까우면 걸음을 늦춘다. 동작 속도도 같이 줄여야 발이 바닥에서 안 미끄러진다.
            float ease = 1f;
            if (_slowDownDist > 0.01f && flatDist < _slowDownDist)
                ease = Mathf.Lerp(_slowDownTo, 1f, flatDist / _slowDownDist);
            if (_animator != null && _standBlendLeft <= 0f && _animator.speed > 0f) _animator.speed = ease;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(dir, Vector3.up), _turnSpeed * ease * Time.deltaTime);
            Vector3 next = pos + dir * _moveSpeed * ease * Time.deltaTime;

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
            {
                float keep = _animator.speed;                 // 늦추던 속도를 잃지 않게
                Play(_walkState);
                if (keep > 0f && keep < 1f) _animator.speed = keep;
            }
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
