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

        [Tooltip("가는 길에 <b>거쳐 갈 자리</b>. 나올 때는 적은 차례대로, 물러갈 때는 거꾸로 밟는다.\n" +
                 "동헌 안까지 올라오려면 이것이 있어야 한다 — 기단은 앞면이 죄 1.65m 벽이고 " +
                 "오를 수 있는 데는 한가운데 계단(z 폭 2.2m) 하나뿐이다. 똑바로 걸으면 벽에 붙어 선다")]
        [SerializeField] private Transform[] _via;

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
        [Tooltip("앞에 다다르면 켤 bool(<b>앉기</b>). 클립이 없으면 비워 둘 것. " +
                 "몸짓표가 이것으로 Stand_To_Sit → Sitting_Idle 로 넘어가고, 끄면 " +
                 "Sit_To_Stand 로 일어선다 — 앉고 서는 그림은 클립이 다 가지고 있다")]
        [SerializeField] private string _sitBool = "";

        [Tooltip("<b>들어올 때 열 문</b>. 불리면 열고, 물러나 제자리에 닿으면 닫는다. " +
                 "비우면 문 없이 드나든다 — 앞이 트인 대청이면 그래도 된다")]
        [SerializeField] private SwingDoor[] _doors;

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

        private GroundFeet _feet;

        private void Awake()
        {
            _home = transform.position;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            _feet = GetComponent<GroundFeet>();
        }

        private Vector3 WaitAt => _waitSpot != null ? _waitSpot.position : _home;
        private Vector3 FrontAt => _frontSpot != null ? _frontSpot.position : _home;

        /// <summary>앞으로 나와 선다.</summary>
        public void StepForward()
        {
            if (_up) return;
            _up = true;
            // <b>문이 먼저 열린다.</b> 걷기 시작할 때 열어 두어야, 대여섯 걸음 걸어오는
            // 동안 문이 다 열려 있다. 다다라서 열면 코앞에서 문이 밀리는 꼴이 된다.
            Doors(true);
            SetBool(_sitBool, false);
            SetBool(_walkBool, true);
            _leg = 0;
            Next();
        }

        /// <summary>물러간다.</summary>
        public void StepBack()
        {
            if (!_up) return;
            _up = false;
            SetBool(_sitBool, false);
            SetBool(_walkBool, true);
            _leg = 0;
            Next();
        }

        /// <summary>
        /// 다음 다리로. 경유점을 순서대로(물러갈 때는 거꾸로) 밟고 마지막에 목적지로 간다.
        ///
        /// <b>왜 곧장 못 가나</b>: 동헌 기단은 앞면이 죄 1.65m 벽이고 오를 수 있는 데는
        /// 한가운데 계단뿐이다. 뜰의 대기 자리에서 마루로 직선을 그으면 그 선이 벽을
        /// 지나므로, 걸어가서 <b>벽에 코를 박고 선다</b>. 계단 아래를 한 번 거쳐야 한다.
        /// </summary>
        private void Next()
        {
            int n = _via != null ? _via.Length : 0;

            if (_leg < n)
            {
                // 나올 때는 앞에서부터, 물러갈 때는 뒤에서부터 밟는다
                _goal = _up ? _via[_leg] : _via[n - 1 - _leg];
                _leg++;
                if (_goal != null) { MarkLeg(); return; }
            }

            _goal = _up ? _frontSpot : _waitSpot;
            _leg = int.MaxValue;                        // 이제부터는 목적지다
            if (_goal == null) { Arrive(); return; }    // 갈 자리가 없으면 그 자리에 선 채로
            MarkLeg();
        }

        /// <summary>이 다리의 출발 자리와 수평 길이를 적어 둔다. 높이는 이 몫으로 오른다.</summary>
        private void MarkLeg()
        {
            _legFrom = transform.position;
            if (_goal == null) { _legLen = 0f; return; }
            Vector3 flat = _goal.position - _legFrom; flat.y = 0f;
            _legLen = flat.magnitude;
        }

        private int _leg = int.MaxValue;

        /// <summary>연출 없이 제자리로(씬을 켤 때).</summary>
        public void SnapBack()
        {
            _up = false;
            _goal = null;
            _leg = int.MaxValue;
            SetBool(_walkBool, false);
            SetBool(_sitBool, false);
            Doors(false);
            // 높이까지 제자리로. 심문이 동헌 <b>안</b>으로 들어간 뒤로는 대청(2.19)에
            // 올라가 있을 수 있어, 여기서 y 를 그대로 두면 뜰 위 두 길에 뜬 채로 선다.
            transform.position = WaitAt;
        }

        private void Update()
        {
            if (_goal == null) return;

            Vector3 here = transform.position;
            Vector3 to = _goal.position; to.y = here.y;

            Vector3 d = to - here;
            if (d.sqrMagnitude <= _arriveAt * _arriveAt) { Arrive(); return; }

            transform.position = Vector3.MoveTowards(here, to, _speed * Time.deltaTime);
            Climb();
            Face(d);
        }

        /// <summary>
        /// <b>다리를 걷는 만큼 높이도 오른다.</b>
        ///
        /// 여태 여기서 y 를 아예 안 만졌다. 뜰이 평평했으니 그래도 됐고, 발 높이는
        /// <see cref="GroundFeet"/> 가 맡는다고 적어 두었다. 그런데 심문이 동헌
        /// <b>안</b>으로 들어가면서 뜰(0.00)에서 대청(2.19)까지 두 길을 올라야 하게 됐다.
        ///
        /// GroundFeet 은 이 일을 못 한다. 그것은 <b>모델</b>을 밀어 바닥에 붙이는
        /// 부품이고, 발보다 한참 위에 있는 것은 아예 바닥으로 안 친다(<c>_maxStepUp</c>) —
        /// 처마나 문짝을 바닥으로 잘못 짚어 사람이 솟구치는 일을 막으려고 그렇게 해 뒀다.
        /// 그러니 대청 마루는 <b>바닥 후보에도 안 든다</b>. 불러 놓으면 甲 은 뜰 높이
        /// 그대로 걸어와, 마루 밑 기단 속에 파묻힌 채로 앉았다.
        ///
        /// 둘이 다투지도 않는다. 여기서는 <b>거쳐 갈 자리에 적힌 높이</b>를 따라가고,
        /// GroundFeet 은 그 자리에서 모델을 몇 cm 다듬을 뿐이다. 서로 다른 것을 만진다.
        ///
        /// 다리를 걸은 <b>몫만큼</b> 올린다 — 계단을 딛고 오르는 꼴이 된다. 정해진
        /// 빠르기로 올리면 계단에 닿기도 전에 허공으로 떠오른다.
        /// </summary>
        private void Climb()
        {
            if (_goal == null) return;
            float goalY = _goal.position.y;
            if (_legLen <= 0.01f) { Snap(goalY); return; }

            Vector3 flat = transform.position - _goal.position; flat.y = 0f;
            float k = Mathf.Clamp01(1f - flat.magnitude / _legLen);
            Snap(Mathf.Lerp(_legFrom.y, goalY, k));
        }

        private void Snap(float y)
        {
            var p = transform.position;
            if (Mathf.Abs(p.y - y) < 0.0005f) return;
            p.y = y;
            transform.position = p;
        }

        private Vector3 _legFrom;   // 이 다리를 시작한 자리
        private float _legLen;      // 그 다리의 수평 길이

        private void Arrive()
        {
            // 아직 경유하는 중이면 멈추지 않고 다음 다리로 이어 걷는다
            int n = _via != null ? _via.Length : 0;
            if (_leg <= n) { Next(); return; }

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
                SetBool(_sitBool, true);
                _onArrivedFront?.Invoke();
                return;
            }

            // 물러난 사람은 도로 마루 쪽을 보고 선다. 등을 돌린 채 굳어 있으면
            // 뜰에 사람이 아니라 허수아비가 서 있는 것처럼 보인다.
            if (_waitSpot != null) transform.rotation = _waitSpot.rotation;
            // 다 나가고 나서 닫는다. 나가는 중에 닫으면 문이 사람을 뚫고 지나간다.
            Doors(false);
            _onArrivedBack?.Invoke();
        }

        /// <summary>딸린 문을 다 여닫는다. 같은 칸의 다른 벌에게도 번지게 둔다.</summary>
        private void Doors(bool open)
        {
            if (_doors == null) return;
            foreach (var d in _doors)
                if (d != null && d.IsOpen != open) d.SetOpen(open, true);
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
            // <b>앉고 서면 발붙임에 일러 준다.</b> 발 뼈에서 몸의 맨 아래까지의 거리가
            // 자세에 따라 달라진다 — 서면 신발 두께뿐이지만 앉으면 치맛단이 발보다
            // 아래로 처진다. 안 일러 주면 선 자세에서 잰 값을 그대로 써서 아내가
            // 앉는 순간 치마가 마루를 13cm 뚫었다.
            if (_feet != null && param == _sitBool && !string.IsNullOrEmpty(param)) _feet.Repose();

            if (_animator == null || string.IsNullOrEmpty(param)) return;
            foreach (var p in _animator.parameters)
                if (p.type == AnimatorControllerParameterType.Bool && p.name == param)
                { _animator.SetBool(param, on); return; }
        }
    }
}
