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

        [Tooltip("오갈 때마다 끝자리를 이만큼(m) 흩뜨린다. 0이면 늘 같은 두 점을 오간다.\n" +
                 "두 점만 오가면 그 길이 <b>선로</b>로 읽힌다 — 사람이 아니라 궤도를 도는 것이 된다")]
        [SerializeField] private float _scatter = 1.6f;

        [Header("걸음")]
        [SerializeField] private float _speed = 0.85f;
        [Tooltip("걷기 클립이 한 바퀴에 <b>실제로 나아가는</b> 빠르기(m/초). " +
                 "동작 속도를 이 값에 맞춰 늦추거나 재촉해 <b>발이 미끄러지지 않게</b> 한다.\n" +
                 "믹사모 걷기는 1.03초에 1.92m — 곧 1.87 이다. 0 이면 안 맞춘다(옛 동작)")]
        [SerializeField] private float _clipStride = 1.87f;
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

        /// <summary>
        /// 클립에 구워진 앞뒤 이동을 걷어내는 장치. 까닭은 <see cref="BakedTravel"/> 에.
        ///
        /// 마을사람의 걷기도 마름과 같은 판에서 왔다 — 한 바퀴에 엉덩이뼈가 <b>1.91m</b>
        /// 앞으로 나갔다가 되돌아온다. 그동안 '동작 속도를 걸음에 맞추는' 손질로 발
        /// 미끄러짐만 달래 왔는데, 몸이 제 발치보다 앞서 떠 다니는 것은 그대로였다.
        /// </summary>
        private readonly BakedTravel _travel = new BakedTravel();

        private void Start()
        {
            _from = _a != null ? _a.position : new Vector3(_fromXZ.x, transform.position.y, _fromXZ.y);
            _to   = _b != null ? _b.position : new Vector3(_toXZ.x,   transform.position.y, _toXZ.y);
            if (_a == null && _fromXZ == Vector2.zero) _from = transform.position;

            _talk = GetComponent<InterrogationController>();
            _face = GetComponent<FaceThePlayer>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            _travel.Bind(transform, _animator);
            if (_animator != null && !string.IsNullOrEmpty(_walkBool))
                foreach (var p in _animator.parameters)
                    if (p.name == _walkBool && p.type == AnimatorControllerParameterType.Bool) _hasWalkBool = true;

            // <b>걸음과 동작을 맞춘다.</b> 클립은 1.87m/초로 걷는 사람의 것인데 몸은
            // 0.8m/초로 나아가고 있었다 — 그 차이가 고스란히 <b>미끄러지는 발</b>이 된다.
            // 동작을 그 비만큼 늦추면 발이 땅을 짚는 자리와 몸이 나아가는 자리가 맞는다.
            if (_animator != null && _clipStride > 0.01f)
                _animator.speed = Mathf.Clamp(_speed / _clipStride, 0.2f, 2.5f);

            // 먼 쪽부터 간다 — 서 있던 자리에서 곧장 되돌아서지 않게
            _goal = Scatter(Flat(_from - transform.position).sqrMagnitude > Flat(_to - transform.position).sqrMagnitude ? _from : _to);
        }

        /// <summary>
        /// 애니메이터가 포즈를 다 쓴 뒤에, 앞뒤로 새어 나간 만큼 뼈대를 되민다.
        /// 이 자리를 <see cref="FaceThePlayer"/> 도 같이 쓰지만 저쪽은 <b>돌리기</b>만
        /// 하므로 서로 밟지 않는다.
        /// </summary>
        private void LateUpdate() { _travel.Cancel(); }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>끝자리를 조금 흩뜨린다 — 같은 두 점을 오가면 궤도가 된다.</summary>
        private Vector3 Scatter(Vector3 p)
        {
            if (_scatter <= 0.01f) return p;
            var d = Random.insideUnitCircle * _scatter;
            return new Vector3(p.x + d.x, p.y, p.z + d.y);
        }

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
                // 어느 쪽 끝에 닿았는지는 <b>어디에 더 가까운가</b>로 본다 —
                // 흩뜨린 자리는 _from·_to 와 값이 같지 않으므로 그것과 견줄 수 없다.
                bool atFrom = Flat(_from - transform.position).sqrMagnitude
                            < Flat(_to - transform.position).sqrMagnitude;
                _goal = Scatter(atFrom ? _to : _from);
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
