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
        [Tooltip("모델의 실제 밑면(렌더러 경계)을 재서 바닥에 올린다. " +
                 "피벗이 발밑이 아니어도(허리·머리에 있어도) 맞는다 — 위치를 추측하지 않고 재기 때문.")]
        [SerializeField] private bool _alignFeetToGround = true;
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

        // 애니메이션이 실제로 움직이는 대상은 Animator가 달린 트랜스폼이다.
        // 이 컴포넌트가 붙은 오브젝트와 다를 수 있어(마커 → 모델 구조), 그쪽도 같이 붙잡아야
        // 좌우로 밀려나는 것이 멈춘다.
        private Transform _animT;
        private Vector3 _animHomeLocalPos;
        private Quaternion _animHomeLocalRot;

        // 동작이 바뀔 때마다 발 높이를 다시 맞춘다(클립마다 몸 높이가 달라서 생기는 문제).
        private int _realignFrames;

        private void Start()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            // 제자리 NPC가 걸어 나가는 원인 대부분이 이것. 동작 클립(발 구르기 등)에 남아 있는
            // 루트 모션이 매번 조금씩 몸을 밀어내고, 그게 쌓이면 집 밖까지 나간다.
            if (_disableRootMotion && _animator != null) _animator.applyRootMotion = false;

            _homePos = transform.position;
            _homeRot = transform.rotation;
            _groundY = _homePos.y;

            if (_animator != null && _animator.transform != transform)
            {
                _animT = _animator.transform;
                _animHomeLocalPos = _animT.localPosition;
                _animHomeLocalRot = _animT.localRotation;
            }

            EnterStill();
        }

        /// <summary>
        /// 모델을 바닥에 올린다. 피벗이 어디에 있든 상관없이 맞는 이유:
        /// 피벗 위치를 추측하지 않고 렌더러 경계(실제로 그려지는 범위)의 밑면을 재서 그만큼 옮긴다.
        /// 애니메이터가 첫 포즈를 적용한 뒤에 재야 하므로 Start가 아니라 첫 LateUpdate에서 한다.
        /// </summary>
        private void AlignFeetToGround()
        {
            if (!ModelBounds.TryGet(transform, out Bounds b)) return;

            // 바닥 높이는 처음 한 번만 찾아 기억한다. 동작마다 다시 찾으면 그때그때 다른 면에 맞아 튄다.
            if (!_floorFound)
            {
                if (!TryFindGroundUnder(b.center, out float found)) return;
                _floorY = found;
                _floorFound = true;
            }

            float lift = _floorY - b.min.y;                // 밑면을 바닥까지 끌어올릴(내릴) 양
            if (Mathf.Abs(lift) < 0.001f) return;

            _homePos.y += lift;
            _groundY = _homePos.y;
            transform.position = new Vector3(transform.position.x, _homePos.y, transform.position.z);
        }

        /// <summary>주어진 지점 아래의 바닥 높이(자기 몸에 맞은 건 무시).</summary>
        private bool TryFindGroundUnder(Vector3 from, out float groundY)
        {
            groundY = from.y;
            Vector3 origin = new Vector3(from.x, from.y + _groundProbeUp, from.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, _groundProbeUp + _groundProbeDown,
                                          ~0, QueryTriggerInteraction.Ignore);
            // '가장 높은 면'을 고르면 머리 위를 지나는 서까래·툇마루에 올라타 버린다.
            // 지금 서 있는 높이에 가장 가까운 면을 바닥으로 본다 — 살짝 뜬 경우와 살짝 묻힌 경우 모두 맞는다.
            bool found = false;
            float best = 0f, bestGap = float.PositiveInfinity;
            float myY = from.y;
            foreach (var h in hits)
            {
                if (h.collider != null && h.collider.transform.IsChildOf(transform)) continue;  // 자기 몸통
                float gap = Mathf.Abs(h.point.y - myY);
                if (gap < bestGap) { bestGap = gap; best = h.point.y; found = true; }
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
        private bool _aligned;
        private bool _floorFound;
        private float _floorY;

        private void LateUpdate()
        {
            // 첫 프레임: 애니메이터가 포즈를 적용한 뒤라야 렌더러 경계가 실제 몸 크기로 나온다.
            if (!_aligned)
            {
                _aligned = true;
                if (_alignFeetToGround) AlignFeetToGround();
            }

            // 애니메이터가 달린 자식이 밀려나면 이 오브젝트를 아무리 잡아도 몸은 흘러간다.
            if (_animT != null)
            {
                if (_lockHorizontal || _lockGroundY) _animT.localPosition = _animHomeLocalPos;
                if (_lockRotation) _animT.localRotation = _animHomeLocalRot;
            }

            var p = transform.position;
            if (_lockGroundY) p.y = _groundY;
            if (_lockHorizontal) { p.x = _homePos.x; p.z = _homePos.z; }
            if (p != transform.position) transform.position = p;

            if (_lockRotation && transform.rotation != _homeRot) transform.rotation = _homeRot;

            // 동작을 바꾼 직후 한 프레임 뒤에 다시 잰다 — 그때라야 새 클립의 포즈가 반영돼 있다.
            if (_realignFrames > 0)
            {
                _realignFrames--;
                if (_realignFrames == 0 && _alignFeetToGround) AlignFeetToGround();
            }
        }

        /// <summary>지금 자리를 새 제자리로 삼는다(연출로 옮긴 뒤 호출).</summary>
        public void ResetHome()
        {
            _homePos = transform.position;
            _homeRot = transform.rotation;
            _groundY = _homePos.y;
            if (_alignFeetToGround) AlignFeetToGround();
        }

#if UNITY_EDITOR
        /// <summary>지금 상태를 그대로 찍는다 — 높이가 왜 어긋나는지 눈으로 보려고.</summary>
        [ContextMenu("진단: 몸·바닥 재보기")]
        private void DiagnoseBody()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[진단] {name}");
            sb.AppendLine($"  피벗 pos = {transform.position}");
            if (ModelBounds.TryGet(transform, out Bounds b))
            {
                sb.AppendLine($"  몸 min.y = {b.min.y:F3}  center = {b.center}  size = {b.size}");
                sb.AppendLine($"  피벗과 발의 차이 = {(transform.position.y - b.min.y):F3} m");
                if (TryFindGroundUnder(b.center, out float fy))
                    sb.AppendLine($"  찾은 바닥 y = {fy:F3}  → 발과의 차이 = {(fy - b.min.y):F3} m");
                else
                    sb.AppendLine("  바닥을 못 찾음 — 발밑에 Collider가 있는 바닥이 없다");
            }
            else sb.AppendLine("  렌더러 없음");
            var an = GetComponentInChildren<Animator>();
            if (an != null)
                sb.AppendLine($"  Animator '{an.name}' (이 오브젝트와 {(an.transform == transform ? "같음" : "다름 — 자식")}) rootMotion={an.applyRootMotion}");
            Debug.Log(sb.ToString(), this);
        }

        /// <summary>에디터에서 지금 바로 바닥에 맞춰본다(Play 없이 확인용).</summary>
        [ContextMenu("바닥에 맞추기")]
        private void AlignNow()
        {
            _homePos = transform.position;
            AlignFeetToGround();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void EnterStill()
        {
            _realignFrames = 2;   // 새 클립 포즈가 적용된 뒤 발 높이 다시 맞추기
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
            _realignFrames = 2;   // 새 클립 포즈가 적용된 뒤 발 높이 다시 맞추기
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
            _realignFrames = 2;   // 새 클립 포즈가 적용된 뒤 발 높이 다시 맞추기
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
