using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 말을 걸면 <b>이쪽을 돌아본다</b> — 하던 자세는 그대로 둔 채.
    ///
    /// 사람에게 말을 걸었는데 그 사람이 벽을 보고 선 채로 대답하면, 대화가 아니라
    /// 방송이 된다. 그렇다고 동작을 갈아 끼우면 조는 마름이 벌떡 서 버린다.
    /// 여기서 하는 일은 <b>몸을 돌리는 것뿐</b>이다 — 앉아 있으면 앉은 채로,
    /// 졸고 있으면 조는 채로 이쪽으로 돌아앉는다. 동작(Animator)은 손대지 않는다.
    ///
    /// <b>걷는 중에는 돌리지 않는다.</b> 제 갈 길을 가는 사람(마름이 문을 열러 가는 중,
    /// 마을사람이 어슬렁대는 중, 甲이 물러가는 중)을 억지로 돌려세우면 몸이 옆으로
    /// 미끄러진다. 그래서 자리가 움직이고 있으면 돌아보기를 그만둔다.
    ///
    /// 붙이는 곳: 사람마다 하나. 씬에 사람을 새로 넣을 때도 이것만 붙이면 된다.
    /// 말을 걸었다는 신호는 <see cref="MouseRaySelector"/> 가 눌린 자리에서 보내 준다.
    /// </summary>
    public class FaceThePlayer : MonoBehaviour
    {
        [Tooltip("실제로 돌릴 것. 비우면 이 오브젝트. 모델이 자식으로 따로 있으면 그쪽을 넣는다")]
        [SerializeField] private Transform _turn;
        [Tooltip("도는 빠르기(초당 도). 사람이 고개를 돌리는 만큼")]
        [SerializeField] private float _turnSpeed = 160f;
        [Tooltip("이만큼(초) 이쪽을 보다가 제자리로 돌아간다. 심문 중에는 계속 본다")]
        [SerializeField] private float _holdSeconds = 8f;
        [Tooltip("끄면 한 번 돌아본 자세로 그냥 있는다")]
        [SerializeField] private bool _returnAfter = true;
        [Tooltip("이 각(도)보다 조금 틀어진 것은 굳이 돌리지 않는다")]
        [SerializeField] private float _deadZone = 8f;
        [Tooltip("자리가 이보다 많이 움직이면 걷는 중으로 보고 돌아보기를 그만둔다(m/프레임)")]
        [SerializeField] private float _moveTolerance = 0.004f;

        private Transform Body => _turn != null ? _turn : transform;

        private Quaternion _home;
        private bool _homeKnown;
        private float _left;
        private Vector3 _lastPos;
        private InterrogationController _talk;
        private Animator _anim;
        private bool _rootMotionWas;
        private bool _rootMotionHeld;

        private void Awake()
        {
            _talk = GetComponent<InterrogationController>();
            _lastPos = Body.position;
            _anim = Body.GetComponent<Animator>();
        }

        /// <summary>
        /// 돌리는 동안에는 <b>루트 모션</b>을 잠깐 끈다.
        ///
        /// 마름의 조는 동작에는 루트 모션이 켜져 있어서, 동작이 매 프레임 제 회전을 다시
        /// 써 넣는다. 그 위에 아무리 돌려 놓아도 다음 프레임에 도로 제자리다 — 8초 동안
        /// 1도밖에 안 돌던 것이 이것이었다. 자리를 옮기는 일은 이 프로젝트에서 전부
        /// 코드가 하므로(MoveTo), 도는 동안 꺼 두어도 걸음에는 지장이 없다.
        /// </summary>
        private void HoldRootMotion(bool hold)
        {
            if (_anim == null || _rootMotionHeld == hold) return;
            if (hold) { _rootMotionWas = _anim.applyRootMotion; _anim.applyRootMotion = false; }
            else _anim.applyRootMotion = _rootMotionWas;
            _rootMotionHeld = hold;
        }

        /// <summary>이쪽을 돌아보게 한다. 밖에서 부른다(눌렸을 때·말을 걸었을 때).</summary>
        public void Face()
        {
            if (!_homeKnown) { _home = Body.rotation; _homeKnown = true; }
            _left = Mathf.Max(_left, _holdSeconds);
        }

        /// <summary>돌아보기를 그만둔다(자리를 뜰 때).</summary>
        public void StopFacing() { _left = 0f; }

        private void LateUpdate()
        {
            // 제 갈 길을 가는 중이면 손대지 않는다.
            //
            // 위아래는 세지 않는다 — 발을 땅에 붙이는 장치(GroundFeet)가 높이를 매 프레임
            // 조금씩 만지므로, 그것까지 '걷는 중'으로 치면 가만히 앉은 사람도 영영
            // 돌아보지 않는다. 걷는다는 것은 바닥 위에서 자리를 옮기는 일이다.
            Vector3 pos = Body.position;
            Vector3 step = pos - _lastPos;
            step.y = 0f;
            bool moving = step.sqrMagnitude > _moveTolerance * _moveTolerance;
            _lastPos = pos;
            if (moving) { _left = 0f; _homeKnown = false; HoldRootMotion(false); return; }

            // 심문 중에는 내내 마주 본다
            if (_talk != null && _talk.IsOpen) Face();

            if (_left <= 0f)
            {
                if (!_returnAfter || !_homeKnown) { HoldRootMotion(false); return; }
                Body.rotation = Quaternion.RotateTowards(Body.rotation, _home, _turnSpeed * Time.deltaTime);
                if (Quaternion.Angle(Body.rotation, _home) < 0.5f) { _homeKnown = false; HoldRootMotion(false); }
                return;
            }

            _left -= Time.deltaTime;
            HoldRootMotion(true);

            var cam = Camera.main;
            if (cam == null) return;

            // 고개만 든다 — 위아래로는 돌리지 않는다. 사람은 서서 발끝을 보지 않는다.
            Vector3 to = cam.transform.position - Body.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0004f) return;

            Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
            if (Quaternion.Angle(Body.rotation, want) < _deadZone) return;
            Body.rotation = Quaternion.RotateTowards(Body.rotation, want, _turnSpeed * Time.deltaTime);
        }

        /// <summary>이 자리(또는 그 어버이)에 사람이 있으면 돌아보게 한다.</summary>
        public static void Notify(Component hit)
        {
            if (hit == null) return;
            var f = hit.GetComponentInParent<FaceThePlayer>();
            if (f != null) f.Face();
        }
    }
}
