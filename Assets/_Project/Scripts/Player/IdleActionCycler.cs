using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 서있는 NPC 연출 반복: "정지 → idle" 을 몇 번 한 뒤, 가끔 랜덤 동작을 끝까지 재생.
    ///   (정지→idle) ×N → 랜덤 동작(끝까지) → (반복)
    ///  · 정지: idle 첫 프레임에 멈춤(가만히 쉼).
    ///  · idle: idle 애니 재생.
    ///  · 랜덤 동작: _actions 중 하나를 끝까지 재생.
    /// Animator 상태 이름은 컨트롤러 상태명과 같아야 함.
    /// 제자리 NPC라 매 프레임 바닥 높이(Y)를 고정해 동작마다 뜨거나 파묻히는 것 방지.
    /// </summary>
    public class IdleActionCycler : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private string _idleState = "Idle";
        [Tooltip("가끔 랜덤으로 재생할 동작들")]
        [SerializeField] private string[] _actions = { "Angry_Ground_Stomp", "Head_Hold_in_Pain" };
        [Tooltip("동작 하나 나오기 전에 (정지→idle)을 몇 번 반복할지")]
        [SerializeField] private int _idlesBeforeAction = 3;
        [Tooltip("동작 사이 정지(쉼) 시간")]
        [SerializeField] private float _stillTime = 0.4f;
        [Tooltip("idle 재생 시간")]
        [SerializeField] private float _idleTime = 1.5f;
        [Tooltip("동작 최대 시간(안전장치). 보통은 동작이 끝나면 자동으로 넘어감")]
        [SerializeField] private float _actionMaxTime = 8f;
        [Header("제자리 고정")]
        [Tooltip("제자리 유지: 시작 높이(Y)를 매 프레임 고정(동작마다 높이 튐 방지)")]
        [SerializeField] private bool _lockGroundY = true;
        [Tooltip("가로(X·Z)도 고정. 끄면 루트 모션이 남아 있는 동작에서 조금씩 밀려난다")]
        [SerializeField] private bool _lockHorizontal = true;
        [Tooltip("회전도 고정. 동작 클립이 몸을 돌리며 방향이 틀어지는 것 방지")]
        [SerializeField] private bool _lockRotation = true;
        [Tooltip("Animator의 Apply Root Motion을 끈다. 제자리 NPC가 걸어 나가는 가장 큰 원인")]
        [SerializeField] private bool _disableRootMotion = true;

        [Header("바닥 맞추기")]
        [Tooltip("시작할 때 발밑으로 레이를 쏴 바닥 높이를 찾는다(공중에 뜨거나 파묻히는 것 방지)")]
        [SerializeField] private bool _snapToGround = true;
        [Tooltip("레이를 쏘기 시작할 머리 위 높이(m)")]
        [SerializeField] private float _groundProbeUp = 2f;
        [Tooltip("발밑으로 이만큼까지 바닥을 찾는다(m)")]
        [SerializeField] private float _groundProbeDown = 5f;

        private enum Phase { Still, Idle, Action }
        private Phase _phase;
        private float _timer;
        private int _idleCount;
        private string _currentAction = "";
        private float _groundY;

        private Vector3 _homePos;
        private Quaternion _homeRot;

        private void Start()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            // 제자리 NPC가 걸어 나가는 원인 대부분이 이것. 동작 클립(발 구르기 등)에 남아 있는
            // 루트 모션이 매번 조금씩 몸을 밀어내고, 그게 쌓이면 집 밖까지 나간다.
            if (_disableRootMotion && _animator != null) _animator.applyRootMotion = false;

            _homePos = transform.position;
            _homeRot = transform.rotation;

            if (_snapToGround && TryFindGround(out float y)) _homePos.y = y;
            _groundY = _homePos.y;
            transform.position = _homePos;

            EnterStill();
        }

        /// <summary>머리 위에서 발밑으로 레이를 쏴 바닥 높이를 찾는다(자기 몸에 맞은 건 무시).</summary>
        private bool TryFindGround(out float groundY)
        {
            groundY = transform.position.y;
            Vector3 origin = transform.position + Vector3.up * _groundProbeUp;
            var hits = Physics.RaycastAll(origin, Vector3.down, _groundProbeUp + _groundProbeDown,
                                          ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            float best = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (h.collider != null && h.collider.transform.IsChildOf(transform)) continue;  // 자기 몸통
                if (h.point.y > best) { best = h.point.y; found = true; }   // 발밑에서 가장 높은 면 = 서 있을 바닥
            }
            if (found) groundY = best;
            return found;
        }

        private void Update()
        {
            if (_phase == Phase.Action)
            {
                _timer -= Time.deltaTime;   // 안전장치
                if (_animator != null)
                {
                    var st = _animator.GetCurrentAnimatorStateInfo(0);
                    bool finished = st.IsName(_currentAction) && st.normalizedTime >= 1f;
                    if (finished || _timer <= 0f) EnterStill();
                }
                else if (_timer <= 0f) EnterStill();
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            if (_phase == Phase.Still) EnterIdle();
            else
            {
                // idle 한 번 끝남 → N번 채우면 동작, 아니면 또 정지→idle
                _idleCount++;
                if (_idleCount >= Mathf.Max(1, _idlesBeforeAction)) { _idleCount = 0; EnterAction(); }
                else EnterStill();
            }
        }

        // 애니메이션이 루트를 움직여도 제자리·바닥에 붙여 둔다.
        // Update가 아니라 LateUpdate인 이유: Animator가 이 프레임의 포즈를 적용한 "뒤"에 되돌려야
        // 한 프레임 튀는 것 없이 고정된다.
        private void LateUpdate()
        {
            var p = transform.position;
            if (_lockGroundY) p.y = _groundY;
            if (_lockHorizontal) { p.x = _homePos.x; p.z = _homePos.z; }
            if (p != transform.position) transform.position = p;

            if (_lockRotation && transform.rotation != _homeRot) transform.rotation = _homeRot;
        }

        /// <summary>지금 자리를 새 제자리로 삼는다(연출로 옮긴 뒤 호출).</summary>
        public void ResetHome()
        {
            _homePos = transform.position;
            _homeRot = transform.rotation;
            if (_snapToGround && TryFindGround(out float y)) _homePos.y = y;
            _groundY = _homePos.y;
            transform.position = _homePos;
        }

        private void EnterStill()
        {
            _phase = Phase.Still;
            _timer = _stillTime;
            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.Play(_idleState, 0, 0f);
                _animator.speed = 0f;   // 첫 프레임에 멈춤 = 가만히
            }
        }

        private void EnterIdle()
        {
            _phase = Phase.Idle;
            _timer = _idleTime;
            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.Play(_idleState, 0, 0f);
            }
        }

        private void EnterAction()
        {
            if (_actions == null || _actions.Length == 0) { EnterStill(); return; }
            _phase = Phase.Action;
            _timer = _actionMaxTime;
            _currentAction = _actions[Random.Range(0, _actions.Length)];
            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.Play(_currentAction, 0, 0f);
            }
        }
    }
}
