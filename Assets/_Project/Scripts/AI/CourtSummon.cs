using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>부르면 나오고, 물리면 물러간다.</b> 동헌 뜰에 선 사람의 걸음.
    ///
    /// <b>왜 걸어야 하나.</b> 넷을 한 줄로 세워 놓고 이름표만 갈아 누르면, 그건 사람을
    /// 부르는 것이 아니라 <b>말할 상대를 고르는 것</b>이다. 넷이 다 같은 자리에 서서
    /// 차례를 기다리는데 누구에게 묻고 있는지는 화면 아래 글씨로만 안다.
    ///
    /// 뜰에서 앞으로 나오는 <b>대여섯 걸음</b>이 그 일을 대신한다. 불린 사람은 나와서
    /// 어사 앞에 서고 나머지는 뒤에 남는다 — 지금 누가 심문받는 중인지를 눈이 먼저 안다.
    /// 그리고 다른 이를 부르면 <b>있던 사람이 등을 보이고 물러난다</b>. 그 등이
    /// "이 사람 이야기는 여기서 끊긴다"는 말이 된다.
    ///
    /// <b>동작이 없어도 걷는다.</b> 걷는 클립이 있는 인물(복동)은 클립을 돌리고, 없는
    /// 인물(옹덕구·하인)은 클립 없이 미끄러진다. 클립이 들어오면 그때 이름만 적어 주면
    /// 된다 — 없는 파라미터를 부르면 유니티가 프레임마다 경고를 뱉으므로, <b>있는지
    /// 먼저 보고</b> 부른다.
    ///
    /// <b>y 는 건드리지 않는다.</b> 발 높이는 <see cref="GroundFeet"/> 가 맡는다.
    /// 여기서 같이 만지면 둘이 서로 밀어 몸이 떤다.
    ///
    /// 붙이는 곳: 인물(<see cref="InterrogationController"/> 가 달린 오브젝트).
    /// </summary>
    public class CourtSummon : MonoBehaviour
    {
        [Header("자리")]
        [Tooltip("뒤에서 기다리는 자리. 비우면 씬이 열릴 때 서 있던 자리를 제 자리로 삼는다")]
        [SerializeField] private Transform _waitSpot;

        [Tooltip("불려 나와 서는 자리. 어사 앞")]
        [SerializeField] private Transform _frontSpot;

        [Tooltip("다 서면 이쪽을 본다(어사 자리). 비우면 걸어온 쪽을 그대로 본다")]
        [SerializeField] private Transform _faceTarget;

        [Header("걸음")]
        [Tooltip("걷는 빠르기(m/s). 끌려 나온 사람의 걸음이라 재지 않는다")]
        [SerializeField] private float _speed = 1.05f;

        [Tooltip("돌아서는 빠르기(도/초)")]
        [SerializeField] private float _turnSpeed = 320f;

        [Tooltip("이만큼(m) 안에 들면 다 온 것으로 친다")]
        [SerializeField] private float _arriveAt = 0.06f;

        [Header("동작 — 있으면 쓴다")]
        [Tooltip("비우면 자식에서 찾는다")]
        [SerializeField] private Animator _animator;
        [Tooltip("걷는 동안 켜 둘 bool 이름. 클립이 없으면 비워 둘 것")]
        [SerializeField] private string _walkBool = "";
        [Tooltip("앞에 서면 켤 bool(꿇기). 클립이 없으면 비워 둘 것")]
        [SerializeField] private string _kneelBool = "";

        [Header("그때")]
        [SerializeField] private UnityEvent _onArrivedFront;
        [SerializeField] private UnityEvent _onArrivedBack;

        private Transform _goal;
        private bool _up;          // 앞에 나와 있나(가는 중이어도 참)

        /// <summary>불려 나와 있나. 이름표를 도드라지게 그릴 때 쓴다.</summary>
        public bool IsUp => _up;

        /// <summary>지금 걷는 중인가.</summary>
        public bool Walking => _goal != null;

        private Vector3 _home;     // 자리 표식이 없을 때 쓸 제 자리

        private void Awake()
        {
            _home = transform.position;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        private Vector3 WaitAt => _waitSpot != null ? _waitSpot.position : _home;
        private Vector3 FrontAt => _frontSpot != null ? _frontSpot.position : _home;

        /// <summary>앞으로 나와 선다.</summary>
        public void StepForward()
        {
            if (_up) return;
            _up = true;
            _goal = _frontSpot;
            SetBool(_kneelBool, false);
            SetBool(_walkBool, true);
            if (_frontSpot == null) Arrive();          // 갈 자리가 없으면 그 자리에 선 채로
        }

        /// <summary>물러간다.</summary>
        public void StepBack()
        {
            if (!_up) return;
            _up = false;
            _goal = _waitSpot;
            SetBool(_kneelBool, false);
            SetBool(_walkBool, true);
            if (_waitSpot == null) Arrive();
        }

        /// <summary>연출 없이 제자리로(씬을 켤 때).</summary>
        public void SnapBack()
        {
            _up = false;
            _goal = null;
            SetBool(_walkBool, false);
            SetBool(_kneelBool, false);
            Vector3 p = WaitAt; p.y = transform.position.y;
            transform.position = p;
        }

        private void Update()
        {
            if (_goal == null) return;

            // <b>수평만 움직인다.</b> 발 높이는 GroundFeet 몫이라, 여기서 y 까지
            // 만지면 둘이 서로 밀어 몸이 위아래로 떤다.
            Vector3 here = transform.position;
            Vector3 to = _goal.position; to.y = here.y;

            Vector3 d = to - here;
            if (d.sqrMagnitude <= _arriveAt * _arriveAt) { Arrive(); return; }

            transform.position = Vector3.MoveTowards(here, to, _speed * Time.deltaTime);
            Face(d);
        }

        private void Arrive()
        {
            _goal = null;
            SetBool(_walkBool, false);

            if (_up)
            {
                // 어사를 마주 본다. 뜰에서 마루를 올려다보는 각이라 고개는 안 든다 —
                // 몸만 돌린다(y 축). 올려다보는 것은 FaceThePlayer 가 따로 한다.
                if (_faceTarget != null)
                {
                    Vector3 look = _faceTarget.position - transform.position; look.y = 0f;
                    if (look.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(look);
                }
                SetBool(_kneelBool, true);
                _onArrivedFront?.Invoke();
                return;
            }

            // 물러난 사람은 도로 마루 쪽을 보고 선다. 등을 돌린 채 굳어 있으면
            // 뜰에 사람이 아니라 허수아비가 서 있는 것처럼 보인다.
            if (_waitSpot != null) transform.rotation = _waitSpot.rotation;
            _onArrivedBack?.Invoke();
        }

        private void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(dir), _turnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 없는 파라미터는 안 부른다.
        ///
        /// 이름만 적어 두고 클립이 아직 없으면 유니티가 <b>프레임마다</b>
        /// "파라미터가 없다" 고 경고를 뱉어 콘솔이 그것으로 덮인다. 진짜 오류가
        /// 그 사이에 묻힌다.
        /// </summary>
        private void SetBool(string param, bool on)
        {
            if (_animator == null || string.IsNullOrEmpty(param)) return;
            foreach (var p in _animator.parameters)
                if (p.type == AnimatorControllerParameterType.Bool && p.name == param)
                { _animator.SetBool(param, on); return; }
        }
    }
}
