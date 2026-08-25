using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 플레이어를 자리에 앉힌다 — 그리고 앉아 있는 동안은 <b>한 발짝도 못 움직인다</b>.
    ///
    /// 왜 앉히는가: 사랑방에 들어서서 주인과 마주 앉는 자리다. 선 채로 상대를 내려다보며
    /// 방 안을 돌아다니면 과객이 아니라 수색하는 관원이 된다. 앉은 사람은 눈높이가 낮고,
    /// 낮은 눈으로 보는 방은 다른 방이다.
    ///
    /// 왜 아예 막는가: 심문하는 동안 뒤쪽 건넌방(장롱·문갑)에 미리 손대면 그 다음에
    /// 벌어질 일이 무너진다. "가지 마시오" 하고 말로 막는 것보다 앉혀 두는 편이 자연스럽다 —
    /// 앉은 사람은 원래 못 걷는다. 둘러보기는 그대로 둔다. 앉았다고 고개까지 굳으면
    /// VR에서는 갇힌 느낌이 나고, 상대를 볼 수 없으면 심문이 성립하지 않는다.
    ///
    /// 붙이는 곳: 플레이어 카메라. 신호는 둘 —
    ///   · <see cref="Sit"/>   방에 들어선 순간(TeleportZone 의 도착 이벤트)
    ///   · <see cref="Stand"/> 상대가 물러간 뒤(BokdongController 의 물러남 이벤트)
    /// </summary>
    public class PlayerSeat : MonoBehaviour
    {
        /// <summary>씬에 하나뿐이다. 이벤트를 잇지 않고 코드에서 부를 때 쓴다.</summary>
        public static PlayerSeat Instance { get; private set; }

        [Header("자리")]
        [Tooltip("앉을 자리. 비우면 지금 서 있는 자리에 그대로 앉는다")]
        [SerializeField] private Transform _seatSpot;
        [Tooltip("앉으면서 바라볼 것(마주 앉는 상대). 비우면 보던 쪽을 그대로 본다")]
        [SerializeField] private Transform _lookAt;

        [Header("눈높이")]
        [Tooltip("앉았을 때 바닥에서 눈까지(m). 방바닥에 앉으면 대략 이만큼이다")]
        [SerializeField] private float _seatedEyeHeight = 1.05f;
        [Tooltip("일어섰을 때 바닥에서 눈까지(m). 카메라의 걷기 눈높이와 맞출 것")]
        [SerializeField] private float _standingEyeHeight = 1.60f;

        [Header("헤드셋에서 앉기")]
        [Tooltip("켜면 헤드셋에서는 <b>몸이 실제로 내려앉아야</b> 앉은 것으로 친다. " +
                 "끄면 예전처럼 눈높이를 대신 내려 준다")]
        [SerializeField] private bool _vrSitByHeight = true;
        [Tooltip("선 키에서 이만큼(m) 내려앉으면 앉은 것으로 본다. " +
                 "방바닥에 앉으면 50~60cm 내려가므로 30cm면 무릎을 굽힌 것만으로는 안 된다")]
        [SerializeField] private float _sitDropRequired = 0.32f;
        [Tooltip("내려앉은 자세를 이만큼(초) 지켜야 앉은 것으로 친다. 잠깐 숙인 것과 가른다")]
        [SerializeField] private float _sitHoldSeconds = 0.4f;

        [Header("시간")]
        [Tooltip("앉는 데 걸리는 시간(초). 뚝 떨어지면 앉은 게 아니라 꺼진 것으로 보인다")]
        [SerializeField] private float _sitSeconds = 1.3f;
        [SerializeField] private float _standSeconds = 1.1f;

        [Header("리그")]
        [Tooltip("비우면 카메라의 최상위 부모를 쓴다. 데스크탑 테스트에서는 카메라 자신이다")]
        [SerializeField] private Transform _rig;

        [Header("일어서기")]
        [Tooltip("앉은 채로 이 키를 누르면 <b>제 발로 일어선다</b>. None 이면 못 일어난다 — " +
                 "사랑방처럼 상대가 물러가야 끝나는 자리는 None 으로 둔다. 동헌은 어사가 " +
                 "제 볼일을 보고 제 발로 내려오는 자리라 키를 준다")]
        [SerializeField] private Key _riseKey = Key.None;

        [Tooltip("앉아 있는 동안 상태창에 남길 말. 비우면 아무것도 안 남긴다. " +
                 "*Space* 처럼 별표로 감싼 낱말은 도드라진다")]
        [TextArea(2, 3)]
        [SerializeField] private string _seatedLine = "";

        [Header("이벤트")]
        [Tooltip("자리를 권한 순간(화면에서만). 주인이 '이리 앉으시오' 하는 자리")]
        [SerializeField] private UnityEvent _onOffered;
        [SerializeField] private UnityEvent _onSeated;
        [SerializeField] private UnityEvent _onStood;

        private enum Phase { Standing, Offered, SittingDown, Seated, StandingUp }
        private Phase _phase = Phase.Standing;

        private Vector3 _from, _to;
        private Quaternion _fromRot, _toRot;
        private float _t, _len;
        private bool _turn;                 // 이번 이동에서 방향까지 맞추는가

        /// <summary>지금 앉아 있는가(다 앉은 뒤부터 일어서기 시작 전까지).</summary>
        public bool Seated => _phase == Phase.Seated;

        /// <summary>자리를 권해 놓고 <b>기다리는</b> 중인가(방석을 누르거나 몸을 낮추기를).</summary>
        public bool Offered => _phase == Phase.Offered;

        private float _standHeadY;      // 권할 때의 머리 높이(바닥 기준)
        private float _offerFloorY;
        private float _lowHold;         // 내려앉은 자세를 지킨 시간

        private Transform Rig
        {
            get
            {
                if (_rig != null) return _rig;
                return transform.root;
            }
        }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ── 밖에서 부르는 신호 ──────────────────────

        /// <summary>인스펙터에 걸어 둔 자리에 앉는다.</summary>
        public void Sit() { SitAt(_seatSpot); }

        /// <summary>
        /// 자리를 <b>권한다</b> — 앉히지는 않는다.
        ///
        /// 화면(리그가 곧 카메라)에서는 방석을 눌러야 앉는다. 방에 들어서자마자 시야가
        /// 스르르 내려가면 앉은 것이 아니라 가라앉은 것이 된다. 주인이 권하고 내가 골라
        /// 앉아야 마주 앉은 것이 된다.
        ///
        /// 헤드셋에서는 <b>몸이 실제로 앉는다</b>. 방석을 눌러 앉으라고 하면 서 있는 채로
        /// 눈만 내려앉아 멀미가 난다. 그래서 VR이면 권하는 절차 없이 눈높이만 내려 준다.
        /// </summary>
        public void OfferSeat()
        {
            if (_phase == Phase.Seated || _phase == Phase.SittingDown || _phase == Phase.Offered) return;

            var cushion = _seatSpot != null ? _seatSpot.GetComponentInChildren<SeatCushion>() : null;
            if (cushion == null) cushion = Object.FindFirstObjectByType<SeatCushion>();

            if (Rig != transform)        // 리그가 따로 있다 = 헤드셋
            {
                if (!_vrSitByHeight) { SitAt(_seatSpot); return; }

                // 방석 위로 몸만 옮겨 놓고, 앉는 것은 <b>사람이 한다</b>.
                // 눈높이를 대신 내려 주면 몸은 선 채로 눈만 가라앉아 멀미가 난다.
                if (_seatSpot != null)
                {
                    Vector3 d = _seatSpot.position - transform.position;
                    d.y = 0f;
                    Rig.position += d;
                }
                _offerFloorY = FloorY(transform.position, transform.position.y - _standingEyeHeight, _seatSpot);
                _standHeadY = transform.position.y - _offerFloorY;
                _lowHold = 0f;
                _phase = Phase.Offered;
                SetMoveLock(true);       // 권한 자리에서 걸어 나가지는 못한다
                if (cushion != null) cushion.Offer();
                _onOffered?.Invoke();
                return;
            }

            if (cushion == null) { SitAt(_seatSpot); return; }   // 방석이 없으면 그냥 앉힌다

            cushion.Offer();
            _phase = Phase.Offered;
            _onOffered?.Invoke();
        }

        /// <summary>이 자리에 앉는다. 비워 보내면 선 자리에 그대로 앉는다.</summary>
        public void SitAt(Transform spot)
        {
            if (_phase == Phase.Seated || _phase == Phase.SittingDown) return;

            // 헤드셋에서 몸으로 앉기로 해 두었으면, 눈높이를 대신 내려 주지 않는다.
            // 방석을 눌러 앉히는 길(데스크탑)이 이리로 들어오는 것도 여기서 막힌다.
            if (Rig != transform && _vrSitByHeight)
            {
                if (_phase != Phase.Offered) OfferSeat();
                return;
            }

            Vector3 here = transform.position;
            Vector3 xz = spot != null ? spot.position : here;
            float floor = FloorY(new Vector3(xz.x, here.y, xz.z), here.y - _standingEyeHeight, spot);

            _to = new Vector3(xz.x, floor + _seatedEyeHeight, xz.z);
            _toRot = FacingRotation(_to);        // <b>앉을 자리</b>에서 잰다 — 서 있던 자리가 아니라
            _turn = true;
            Begin(_sitSeconds);
            _phase = Phase.SittingDown;

            SetMoveLock(true);      // 앉기 시작하는 순간부터 막는다. 다 앉을 때까지 기다리면
                                    // 그 사이에 걸어 나갈 수 있다
        }

        /// <summary>일어선다. 막아 두었던 걸음을 다시 푼다.</summary>
        public void Stand()
        {
            if (_phase == Phase.Standing || _phase == Phase.StandingUp) return;

            // 헤드셋에서는 일어서는 것도 몸이 한다. 걸음만 풀어 주면 된다.
            if (Rig != transform && _vrSitByHeight)
            {
                _phase = Phase.Standing;
                SetMoveLock(false);
                _onStood?.Invoke();
                return;
            }

            Vector3 here = transform.position;

            // <b>방석은 바닥이 아니다.</b> 앉을 때는 방석을 빼고 쟀는데(FloorY 의 ignore)
            // 일어설 때는 그냥 쟀다. 그래서 방석 윗면이 마루로 잡혀, 일어선 키가 방석
            // 두께만큼 붕 떴다 — 일어나기 높이가 이상하다던 것이 이것이다.
            float floor = FloorY(here, here.y - _seatedEyeHeight, _seatSpot);
            _standFloorY = floor;
            _to = new Vector3(here.x, floor + StandingEye, here.z);
            _toRot = transform.rotation;
            _turn = false;                  // 일어서면서 고개까지 돌려 주면 멀미가 난다
            Begin(_standSeconds);
            _phase = Phase.StandingUp;
        }

        private float _standFloorY;

        /// <summary>
        /// 선 사람의 눈높이. <b>카메라가 아는 값을 그대로 쓴다.</b>
        ///
        /// 여기에 따로 적어 두면 반드시 어긋난다 — 실제로 1.60 과 1.70 으로 갈라져 있었다.
        /// 일어서면 1.60 에 세워 놓고, 한 발짝 걷는 순간 카메라가 1.70 으로 끌어올려
        /// 키가 스르르 자랐다. 걷는 키를 아는 것은 걷는 쪽이다.
        /// </summary>
        private float StandingEye
        {
            get
            {
                var fly = Rig.GetComponentInChildren<DebugFlyCamera>();
                if (fly == null) fly = GetComponent<DebugFlyCamera>();
                return fly != null ? fly.EyeHeight : _standingEyeHeight;
            }
        }

        /// <summary>앉을 자리와 마주 볼 것을 밖에서 물려 준다(실내가 다른 씬일 때).</summary>
        public void BindSeat(Transform spot, Transform lookAt)
        {
            if (spot != null) _seatSpot = spot;
            if (lookAt != null) _lookAt = lookAt;
        }

        /// <summary>연출 없이 곧장 앉힌다(씬을 켜자마자 앉아 있어야 할 때).</summary>
        public void SitNow()
        {
            SitAt(_seatSpot);
            _t = _len;                      // 다음 Update 에서 곧바로 끝난다
        }

        // ── 진행 ────────────────────────────────────

        private void Begin(float seconds)
        {
            _from = transform.position;
            _fromRot = transform.rotation;
            _len = Mathf.Max(0.01f, seconds);
            _t = 0f;
        }

        private void Update()
        {
            RiseKey();

            // 자리를 권해 놓고 기다리는 중 — 헤드셋이면 <b>머리가 내려오는 것</b>을 본다.
            // 앉으라는 말을 듣고 실제로 앉는 것, 그것 말고는 진행시키지 않는다.
            if (_phase == Phase.Offered)
            {
                if (Rig == transform || !_vrSitByHeight) return;   // 데스크탑은 방석을 누른다

                float head = transform.position.y - _offerFloorY;
                bool low = head <= _standHeadY - _sitDropRequired;
                _lowHold = low ? _lowHold + Time.deltaTime : 0f;
                if (_lowHold < _sitHoldSeconds) return;

                _phase = Phase.Seated;
                _onSeated?.Invoke();
                return;
            }

            if (_phase != Phase.SittingDown && _phase != Phase.StandingUp) return;

            _t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / _len));

            PlaceHead(Vector3.Lerp(_from, _to, k),
                      _turn ? Quaternion.Slerp(_fromRot, _toRot, k) : transform.rotation);

            if (_t < _len) return;

            if (_phase == Phase.SittingDown)
            {
                _phase = Phase.Seated;
                _onSeated?.Invoke();
            }
            else
            {
                _phase = Phase.Standing;

                // 카메라에게 <b>마루 높이를 알려 주고</b> 걸음을 푼다. 안 알려 주면
                // 카메라가 제 발밑을 찾다가 방석을 딛고, 걸음을 뗄 때 키가 다시 흔들린다.
                var fly = Rig.GetComponentInChildren<DebugFlyCamera>();
                if (fly == null) fly = GetComponent<DebugFlyCamera>();
                if (fly != null && Rig == transform) fly.StandAtFloor(_standFloorY);

                SetMoveLock(false);
                _onStood?.Invoke();
            }
        }

        // ── 도구 ────────────────────────────────────

        /// <summary>
        /// 머리를 이 자리에 둔다.
        ///
        /// 데스크탑 테스트에서는 리그가 곧 카메라라 그냥 옮기면 된다. VR이면 머리는
        /// 헤드셋이 쥐고 있으므로 손대지 않고, <b>수평으로만</b> 리그를 밀어 머리가 그 자리에
        /// 오게 한다 — 높이는 실제로 앉은 사람의 몸이 정한다. 억지로 눈높이를 내리면
        /// 몸은 서 있는데 눈만 내려가 멀미가 난다.
        /// </summary>
        private void PlaceHead(Vector3 pos, Quaternion rot)
        {
            var rig = Rig;
            if (rig == transform)
            {
                transform.SetPositionAndRotation(pos, rot);
                return;
            }
            Vector3 d = pos - transform.position;
            d.y = 0f;
            rig.position += d;
        }

        /// <summary>
        /// 바라볼 방향. 마주 앉을 상대가 있으면 그쪽, 없으면 보던 쪽 그대로.
        ///
        /// <b>서 있던 자리가 아니라 앉을 자리에서 잰다.</b> 여태 지금 서 있는 자리에서
        /// 쟀는데, 자리 옆에서 눌러 앉으면 그 비스듬한 각도가 그대로 굳는다 —
        /// 동헌 교의에서 실제로 33° 가 틀어져, 앉고 나니 마주 선 사람이 화면 왼쪽 끝에
        /// 걸려 있었다. 사랑방은 방석 코앞에서 누르니 티가 안 났을 뿐이다.
        /// </summary>
        private Quaternion FacingRotation(Vector3 from)
        {
            if (_lookAt == null) return transform.rotation;
            Vector3 to = _lookAt.position - from;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return transform.rotation;
            return Quaternion.LookRotation(to, Vector3.up);
        }

        /// <summary>
        /// 발밑 바닥 높이. 못 찾으면 지금 발 높이를 그대로 쓴다 —
        /// 바닥을 못 찾았다고 0으로 떨어뜨리면 마루 밑에 처박힌다.
        ///
        /// <paramref name="ignore"/> 밑의 콜라이더는 세지 않는다. 방석에 눌러 잡을
        /// 콜라이더를 붙이고 나니 그 윗면이 바닥으로 잡혀, 앉은 눈높이가 방석 두께만큼
        /// 올라갔다(1.05 로 앉혔는데 마루에서 1.28 이 나왔다). 앉는 높이는 <b>마루</b>에서
        /// 재야 한다 — 방석 위에 앉는다는 것은 이미 그 값에 들어 있다.
        /// </summary>
        private float FloorY(Vector3 near, float fallback, Transform ignore = null)
        {
            Vector3 from = new Vector3(near.x, near.y + 1.0f, near.z);
            var hits = Physics.RaycastAll(from, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue, y = fallback;
            bool found = false;
            foreach (var h in hits)
            {
                if (ignore != null && (h.collider.transform == ignore || h.collider.transform.IsChildOf(ignore))) continue;
                if (h.distance < best) { best = h.distance; y = h.point.y; found = true; }
            }
            return found ? y : fallback;
        }

        /// <summary>걸음을 막거나 푼다. 둘러보기는 건드리지 않는다.</summary>
        /// <summary>
        /// 앉은 사람이 <b>제 발로</b> 일어선다.
        ///
        /// 여태 일어서는 길은 하나뿐이었다 — 마주 앉은 상대가 물러가면서 Stand() 를
        /// 불러 주는 것. 사랑방에서는 그것이 옳았다. 심문하다 말고 일어나 방을 뒤지면
        /// 그 다음에 벌어질 일이 무너지니까.
        ///
        /// 그런데 동헌은 <b>어사가 주인인 자리</b>다. 부를 사람을 다 부르고 나면
        /// 일어나 서고로 가야 하는데, 물러가 줄 사람이 없으니 앉은 채로 갇힌다.
        /// 그래서 키를 하나 준다 — 다만 <b>자리마다 따로</b> 준다. 기본은 None 이라
        /// 사랑방은 여태대로다.
        /// </summary>
        private void RiseKey()
        {
#if ENABLE_INPUT_SYSTEM
            if (_riseKey == Key.None) return;

            bool seated = _phase == Phase.Seated;
            if (seated != _wasSeated)
            {
                _wasSeated = seated;
                if (!seated) StatusPanel.Clear("앉음");   // 일어섰으면 안내도 거둔다
            }
            if (!seated) return;

            // <b>마주 앉은 동안에는 안내를 치운다.</b> 상태창은 눈 위 한가운데에 뜨는데,
            // 심문 중에는 그 자리에 상대의 대사가 와야 한다. "Space 일어서기"가 상대
            // 얼굴 위에 떠 있으면 지금 읽어야 할 것이 무엇인지 갈린다.
            // 키는 그대로 산다 — 안내만 물러난다.
            bool busy = InterrogationController.AnyOpen || JournalView.AnyOpen || DocumentView.IsOpen;
            if (busy) StatusPanel.Clear("앉음");
            else if (!string.IsNullOrEmpty(_seatedLine)) StatusPanel.Set("앉음", 3, _seatedLine);

            var kb = Keyboard.current;
            if (kb != null && kb[_riseKey].wasPressedThisFrame)
            {
                StatusPanel.Clear("앉음");
                Stand();
            }
#endif
        }

        private bool _wasSeated;

        private void SetMoveLock(bool locked)
        {
            var fly = Rig.GetComponentInChildren<DebugFlyCamera>();
            if (fly == null) fly = GetComponent<DebugFlyCamera>();
            if (fly != null) fly.MoveLocked = locked;
        }
    }
}
