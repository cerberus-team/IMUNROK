using UnityEngine;
using UnityEngine.Events;

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

        [Header("시간")]
        [Tooltip("앉는 데 걸리는 시간(초). 뚝 떨어지면 앉은 게 아니라 꺼진 것으로 보인다")]
        [SerializeField] private float _sitSeconds = 1.3f;
        [SerializeField] private float _standSeconds = 1.1f;

        [Header("리그")]
        [Tooltip("비우면 카메라의 최상위 부모를 쓴다. 데스크탑 테스트에서는 카메라 자신이다")]
        [SerializeField] private Transform _rig;

        [Header("이벤트")]
        [Tooltip("자리를 권한 순간(화면에서만). 주인이 '이리 앉으시오' 하는 자리")]
        [SerializeField] private UnityEvent _onOffered;
        [SerializeField] private UnityEvent _onSeated;
        [SerializeField] private UnityEvent _onStood;

        private enum Phase { Standing, SittingDown, Seated, StandingUp }
        private Phase _phase = Phase.Standing;

        private Vector3 _from, _to;
        private Quaternion _fromRot, _toRot;
        private float _t, _len;
        private bool _turn;                 // 이번 이동에서 방향까지 맞추는가

        /// <summary>지금 앉아 있는가(다 앉은 뒤부터 일어서기 시작 전까지).</summary>
        public bool Seated => _phase == Phase.Seated;

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
            if (Rig != transform)        // 리그가 따로 있다 = 헤드셋
            {
                SitAt(_seatSpot);
                return;
            }

            var cushion = _seatSpot != null ? _seatSpot.GetComponentInChildren<SeatCushion>() : null;
            if (cushion == null) cushion = Object.FindFirstObjectByType<SeatCushion>();
            if (cushion == null) { SitAt(_seatSpot); return; }   // 방석이 없으면 그냥 앉힌다

            cushion.Offer();
            _onOffered?.Invoke();
        }

        /// <summary>이 자리에 앉는다. 비워 보내면 선 자리에 그대로 앉는다.</summary>
        public void SitAt(Transform spot)
        {
            if (_phase == Phase.Seated || _phase == Phase.SittingDown) return;

            Vector3 here = transform.position;
            Vector3 xz = spot != null ? spot.position : here;
            float floor = FloorY(new Vector3(xz.x, here.y, xz.z), here.y - _standingEyeHeight);

            _to = new Vector3(xz.x, floor + _seatedEyeHeight, xz.z);
            _toRot = FacingRotation();
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

            Vector3 here = transform.position;
            float floor = FloorY(here, here.y - _seatedEyeHeight);
            _to = new Vector3(here.x, floor + _standingEyeHeight, here.z);
            _toRot = transform.rotation;
            _turn = false;                  // 일어서면서 고개까지 돌려 주면 멀미가 난다
            Begin(_standSeconds);
            _phase = Phase.StandingUp;
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

        /// <summary>바라볼 방향. 마주 앉을 상대가 있으면 그쪽, 없으면 보던 쪽 그대로.</summary>
        private Quaternion FacingRotation()
        {
            if (_lookAt == null) return transform.rotation;
            Vector3 to = _lookAt.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return transform.rotation;
            return Quaternion.LookRotation(to, Vector3.up);
        }

        /// <summary>
        /// 발밑 바닥 높이. 못 찾으면 지금 발 높이를 그대로 쓴다 —
        /// 바닥을 못 찾았다고 0으로 떨어뜨리면 마루 밑에 처박힌다.
        /// </summary>
        private float FloorY(Vector3 near, float fallback)
        {
            RaycastHit hit;
            Vector3 from = new Vector3(near.x, near.y + 1.0f, near.z);
            if (Physics.Raycast(from, Vector3.down, out hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallback;
        }

        /// <summary>걸음을 막거나 푼다. 둘러보기는 건드리지 않는다.</summary>
        private void SetMoveLock(bool locked)
        {
            var fly = Rig.GetComponentInChildren<DebugFlyCamera>();
            if (fly == null) fly = GetComponent<DebugFlyCamera>();
            if (fly != null) fly.MoveLocked = locked;
        }
    }
}
