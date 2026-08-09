using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 어슬렁어슬렁 배회하는 NPC(마을사람 등).
    ///  · "직진 → 방향 바꿈 → 직진"을 반복한다.
    ///     - Walking: 정해진 방향으로 쭉 곧게 걷는다(원 그리며 돌지 않음).
    ///     - Turning: 거의 제자리에서 새 방향으로 몸을 돌린 뒤 다시 직진.
    ///  · 시작 지점 반경(_radius)을 벗어나면 집 쪽으로 방향을 바꿔 되돌아온다.
    ///  · 가끔(_pauseChance) 방향 바꾸기 전에 잠깐 멈춘다. 0으로 두면 안 멈춤.
    ///  · 심문이 열리면(대화 중) 멈추고 플레이어(카메라)를 바라본다.
    ///
    /// 붙이는 곳: NPC 루트(InterrogationController 있는 오브젝트).
    /// Animator는 자식 캐릭터 모델에 있어도 자동으로 찾음.
    ///   · Walk 클립 하나뿐이면 Loop Time 켜두기 → 멈출 때 속도 0으로 그 자세 정지.
    /// </summary>
    public class WanderingNpc : MonoBehaviour
    {
        [Header("이동")]
        [SerializeField] private float _speed = 0.7f;
        [Tooltip("시작 지점 기준 배회 반경(m). 이 밖으로 나가면 집 쪽으로 방향을 돌림")]
        [SerializeField] private float _radius = 4f;

        [Header("직진 구간")]
        [Tooltip("한 번 직진하는 시간(초) 최소~최대 중 랜덤")]
        [SerializeField] private float _straightMin = 2.5f;
        [SerializeField] private float _straightMax = 5f;

        [Header("방향 바꾸기")]
        [Tooltip("몸이 새 방향으로 도는 속도(도/초). 높을수록 빠르게 방향 전환")]
        [SerializeField] private float _turnSpeed = 150f;
        [Tooltip("한 번에 바꾸는 각도(도) 최소~최대")]
        [SerializeField] private float _turnAngleMin = 40f;
        [SerializeField] private float _turnAngleMax = 110f;
        [Tooltip("도는 동안 얼마나 천천히 이동할지(0~1). 1이면 제자리서 돎")]
        [Range(0f, 1f)] [SerializeField] private float _turnSlowdown = 0.85f;

        [Header("멈춤(가끔)")]
        [Tooltip("방향 바꾸기 직전 잠깐 멈출 확률(0~1). 0이면 안 멈춤")]
        [Range(0f, 1f)] [SerializeField] private float _pauseChance = 0.15f;
        [SerializeField] private float _pauseTime = 1.2f;

        [Header("애니메이션")]
        [SerializeField] private Animator _animator;
        [SerializeField] private bool _freezeWhenStopped = true;
        [Tooltip("Idle 클립이 있을 때만 사용 — 없으면 비워도 됨")]
        [SerializeField] private string _walkBool = "IsWalking";
        private bool _hasWalkBool;

        private enum State { Walking, Turning }
        private State _state = State.Walking;

        private Vector3 _home;
        private Vector3 _heading;        // 지금 걷는 방향(직진용, 고정)
        private Vector3 _targetHeading;  // 돌아서 맞출 새 방향
        private Transform _player;
        private float _straightTimer;
        private float _pauseTimer;

        private void Start()
        {
            _home = transform.position;

            _heading = transform.forward;
            _heading.y = 0f;
            if (_heading.sqrMagnitude < 0.001f)
                _heading = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
            _heading.Normalize();

            _straightTimer = Random.Range(_straightMin, _straightMax);

            var cam = Camera.main;
            if (cam != null) _player = cam.transform;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            _hasWalkBool = HasParam(_walkBool);
        }

        private void Update()
        {
            // 대화(심문) 중이면 멈추고 플레이어를 바라봄
            if (InterrogationController.AnyOpen)
            {
                SetWalking(false);
                FacePlayer();
                return;
            }

            // 잠깐 쉬는 중 → 끝나면 회전 시작
            if (_pauseTimer > 0f)
            {
                _pauseTimer -= Time.deltaTime;
                SetWalking(false);
                if (_pauseTimer <= 0f) _state = State.Turning;
                return;
            }

            if (_state == State.Walking)
                Walk();
            else
                Turn();
        }

        // 정해진 방향으로 곧게 직진
        private void Walk()
        {
            // 반경 밖으로 나가면 집 쪽으로 방향 전환
            Vector3 fromHome = transform.position - _home;
            fromHome.y = 0f;
            if (fromHome.magnitude > _radius)
            {
                _targetHeading = (-fromHome).normalized;
                _state = State.Turning;
                return;
            }

            // 몸을 걷는 방향으로 정렬(잔여 오차 제거 → 완전 직선)
            transform.rotation = Quaternion.LookRotation(_heading);
            transform.position += _heading * _speed * Time.deltaTime;
            SetWalking(true);

            _straightTimer -= Time.deltaTime;
            if (_straightTimer <= 0f)
            {
                // 가끔 멈췄다가 돌기
                if (Random.value < _pauseChance)
                {
                    _pauseTimer = _pauseTime;
                    SetWalking(false);
                    return;
                }
                PickNewDirection();
                _state = State.Turning;
            }
        }

        // 거의 제자리에서 새 방향으로 몸을 돌림
        private void Turn()
        {
            if (_targetHeading.sqrMagnitude < 0.001f) PickNewDirection();

            Quaternion look = Quaternion.LookRotation(_targetHeading);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _turnSpeed * Time.deltaTime);

            // 도는 동안엔 아주 조금만 이동
            transform.position += transform.forward * _speed * (1f - _turnSlowdown) * Time.deltaTime;
            SetWalking(true);

            // 다 돌면 그 방향으로 직진 시작
            if (Quaternion.Angle(transform.rotation, look) < 2f)
            {
                _heading = _targetHeading;
                _straightTimer = Random.Range(_straightMin, _straightMax);
                _state = State.Walking;
            }
        }

        private void PickNewDirection()
        {
            float ang = Random.Range(_turnAngleMin, _turnAngleMax) * (Random.value < 0.5f ? -1f : 1f);
            _targetHeading = (Quaternion.Euler(0f, ang, 0f) * _heading).normalized;
        }

        private void FacePlayer()
        {
            if (_player == null) return;
            Vector3 to = _player.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            Quaternion look = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _turnSpeed * Time.deltaTime);
        }

        private bool HasParam(string name)
        {
            if (_animator == null || string.IsNullOrEmpty(name)) return false;
            foreach (var p in _animator.parameters)
                if (p.type == AnimatorControllerParameterType.Bool && p.name == name) return true;
            return false;
        }

        private void SetWalking(bool walking)
        {
            if (_animator == null) return;
            if (_freezeWhenStopped) _animator.speed = walking ? 1f : 0f;
            if (_hasWalkBool) _animator.SetBool(_walkBool, walking);
        }
    }
}
