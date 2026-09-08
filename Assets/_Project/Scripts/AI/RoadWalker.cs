using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>길을 오르내린다.</b> 두 자리 사이를 천천히 왔다 갔다 하고, 양 끝에서 잠깐 쉰다.
    ///
    /// <see cref="WanderingNpc"/> 는 제자리 둘레를 <b>아무렇게나</b> 도는 것이라,
    /// 말을 걸려고 다가서면 상대가 어디로 갈지 알 수 없다. 마을 어귀에서 처음
    /// 만나는 사람이 그러면 첫 대화가 술래잡기가 된다.
    ///
    /// 여기서는 <b>길</b>을 준다. 사람이 길을 따라 오르내리면 어디서 만날지가 눈에
    /// 보이고, 마주 오는 사람에게 말을 거는 것은 쫓아가서 붙드는 것과 다른 일이다.
    /// 게다가 오르내리는 몸이 있으면 그 길이 <b>다니는 길</b>임이 저절로 드러난다 —
    /// 비탈 하나를 그려 놓고 아무도 안 다니면 그것은 지형이지 길이 아니다.
    ///
    /// 높이는 안 정한다. 발밑은 <see cref="GroundFeet"/> 가 맡는다 — 여기서 y 까지
    /// 손대면 비탈에서 둘이 싸운다.
    ///
    /// 붙이는 곳: NPC 뿌리(<see cref="InterrogationController"/> 가 있는 오브젝트).
    /// </summary>
    public class RoadWalker : MonoBehaviour
    {
        [Header("오갈 두 자리 (높이는 안 본다)")]
        [Tooltip("한쪽 끝. 비워 두면 시작 자리를 쓴다")]
        [SerializeField] private Transform _a;
        [Tooltip("다른 끝")]
        [SerializeField] private Transform _b;

        [Tooltip("자리를 오브젝트로 두기 번거로울 때 — 이 값으로 직접 적는다(x, z)")]
        [SerializeField] private Vector2 _fromXZ;
        [SerializeField] private Vector2 _toXZ;

        [Header("걸음")]
        [SerializeField] private float _speed = 0.85f;
        [Tooltip("도는 빠르기(도/초)")]
        [SerializeField] private float _turnSpeed = 160f;
        [Tooltip("끝에 닿았다고 볼 거리(m)")]
        [SerializeField] private float _reach = 1.2f;
        [Tooltip("끝에서 쉬는 시간(초) 최소~최대")]
        [SerializeField] private float _restMin = 1.6f;
        [SerializeField] private float _restMax = 3.4f;

        [Header("애니메이션")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _walkBool = "IsWalking";

        private Vector3 _from, _to, _goal;
        private float _rest;
        private bool _hasWalkBool;

        /// <summary>이 사람 제 심문. 남의 심문에는 발이 안 묶인다.</summary>
        private InterrogationController _talk;
        private FaceThePlayer _face;

        private void Start()
        {
            _from = _a != null ? _a.position : new Vector3(_fromXZ.x, transform.position.y, _fromXZ.y);
            _to   = _b != null ? _b.position : new Vector3(_toXZ.x,   transform.position.y, _toXZ.y);
            if (_a == null && _fromXZ == Vector2.zero) _from = transform.position;

            _talk = GetComponent<InterrogationController>();
            _face = GetComponent<FaceThePlayer>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator != null && !string.IsNullOrEmpty(_walkBool))
                foreach (var p in _animator.parameters)
                    if (p.name == _walkBool && p.type == AnimatorControllerParameterType.Bool) _hasWalkBool = true;

            // 먼 쪽부터 간다 — 서 있던 자리에서 곧장 되돌아서지 않게
            _goal = Flat(_from - transform.position).sqrMagnitude > Flat(_to - transform.position).sqrMagnitude ? _from : _to;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        private void Update()
        {
            // <b>말을 거는 동안에는 걷지 않는다 — 말을 걸린 사람만.</b>
            //
            // 여기가 <c>InterrogationController.AnyOpen</c> 을 보고 있었다. 그것은
            // 「씬 어딘가에서 누군가 말을 하고 있다」는 뜻이라, 마당 저쪽에서 옹덕구를
            // 붙들면 담 앞의 마을사람까지 그 자리에 얼어붙었다. 사람이 말을 나누는
            // 동안 온 마을이 멈춰 서면 그것은 마을이 아니라 무대 장치다.
            // 발이 묶여야 하는 것은 <b>마주 선 그 사람</b> 하나뿐이다.
            if (_talk != null && _talk.IsOpen) { Walking(false); return; }

            if (_rest > 0f) { _rest -= Time.deltaTime; Walking(false); return; }

            // 걸을 참이면 돌아보기에게서 몸을 돌려받는다. 안 그러면 이쪽이 튼 몸을
            // 저쪽이 LateUpdate 에서 도로 돌려놓는다(FaceThePlayer.Release 참고).
            if (_face != null) _face.Release();

            Vector3 to = Flat(_goal - transform.position);
            if (to.magnitude <= _reach)
            {
                _goal = (_goal == _to) ? _from : _to;
                _rest = Random.Range(_restMin, _restMax);
                Walking(false);
                return;
            }

            var want = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, _turnSpeed * Time.deltaTime);

            // 등지고 있는 동안에는 안 나아간다 — 돌면서 옆걸음질 치는 꼴을 막는다
            float facing = Vector3.Dot(Flat(transform.forward).normalized, to.normalized);
            if (facing > 0.55f)
            {
                Vector3 step = Flat(transform.forward).normalized * (_speed * Time.deltaTime);
                transform.position += step;
                Walking(true);
            }
            else Walking(false);
        }

        private void Walking(bool on)
        {
            if (_hasWalkBool) _animator.SetBool(_walkBool, on);
        }

        /// <summary>씬뷰에서 오가는 길이 보이게.</summary>
        private void OnDrawGizmosSelected()
        {
            Vector3 a = _a != null ? _a.position : new Vector3(_fromXZ.x, transform.position.y, _fromXZ.y);
            Vector3 b = _b != null ? _b.position : new Vector3(_toXZ.x, transform.position.y, _toXZ.y);
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.4f);
            Gizmos.DrawWireSphere(b, 0.4f);
        }
    }
}
