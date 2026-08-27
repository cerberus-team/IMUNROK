using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 어슬렁어슬렁 배회하는 NPC(마을사람 등).
    ///  · 목표 방향(_targetHeading)을 정해두고, 현재 방향(_heading)을 그쪽으로 부드럽게 돌리며
    ///    "항상" 앞으로 걷는다. 방향이 어긋난 동안엔 속도를 줄여 거의 제자리서 돌고,
    ///    방향을 맞추면 정상 속도로 곧게 직진한다. (원 그리며 돌지 않음 + 멈춰서 안 나가는 일 없음)
    ///  · 시작 지점 반경(_radius)을 벗어나면 집 쪽으로 방향을 잡아 되돌아온다.
    ///  · 가끔(_pauseChance) 잠깐 멈춘다. 0으로 두면 안 멈춤.
    ///  · 심문이 열리면(대화 중) 멈추고 플레이어(카메라)를 "부드럽게" 바라본다.
    ///
    /// 붙이는 곳: NPC 루트(InterrogationController 있는 오브젝트).
    ///   · 캐릭터 모델은 이 루트의 자식이며 Local Position이 (0,0,0)이어야 제자리서 돈다.
    ///   · 캐릭터 Animator의 "Apply Root Motion"은 꺼두기(켜져 있으면 이동이 충돌·정지함).
    ///   · Walk 클립 하나뿐이면 Loop Time 켜두기 → 멈출 때 속도 0으로 그 자세 정지.
    /// </summary>
    public class WanderingNpc : MonoBehaviour
    {
        [Header("이동")]
        [SerializeField] private float _speed = 0.7f;
        [Tooltip("시작 지점 기준 배회 반경(m). 이 밖으로 나가면 집 쪽으로 방향을 돌림")]
        [SerializeField] private float _radius = 4f;

        [Header("직진 구간")]
        [Tooltip("한 방향으로 걷는 시간(초) 최소~최대 중 랜덤")]
        [SerializeField] private float _straightMin = 2.5f;
        [SerializeField] private float _straightMax = 5f;

        [Header("방향 바꾸기")]
        [Tooltip("방향을 트는 속도(도/초). 높을수록 빠르게 전환")]
        [SerializeField] private float _turnSpeed = 140f;
        [Tooltip("한 번에 바꾸는 각도(도) 최소~최대")]
        [SerializeField] private float _turnAngleMin = 40f;
        [SerializeField] private float _turnAngleMax = 110f;
        [Tooltip("방향이 어긋난 동안 속도를 얼마나 줄일지(0~1). 높을수록 제자리서 도는 느낌")]
        [Range(0f, 0.9f)] [SerializeField] private float _turnSlowdown = 0.8f;

        [Header("멈춤(가끔)")]
        [Tooltip("방향 바꿀 때 잠깐 멈출 확률(0~1). 0이면 안 멈춤")]
        [Range(0f, 1f)] [SerializeField] private float _pauseChance = 0.15f;
        [SerializeField] private float _pauseTime = 1.2f;

        [Header("말 걸었을 때")]
        [Tooltip("플레이어를 돌아보는 부드러움. 낮을수록 천천히·자연스럽게 돎")]
        [SerializeField] private float _facePlayerEase = 3.5f;

        [Header("애니메이션")]
        [SerializeField] private Animator _animator;
        [SerializeField] private bool _freezeWhenStopped = true;
        [Tooltip("Idle 클립이 있을 때만 사용 — 없으면 비워도 됨")]
        [SerializeField] private string _walkBool = "IsWalking";
        private bool _hasWalkBool;

        private Vector3 _home;
        private Vector3 _heading;        // 현재 진행 방향
        private Vector3 _targetHeading;  // 맞춰갈 목표 방향
        private Transform _player;
        private float _retargetTimer;

        private float _pauseTimer;

        private void Start()
        {
            _home = transform.position;

            _heading = transform.forward;
            _heading.y = 0f;
            if (_heading.sqrMagnitude < 0.001f)
                _heading = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
            _heading.Normalize();
            _targetHeading = _heading;

            _retargetTimer = Random.Range(_straightMin, _straightMax);

            var cam = Camera.main;
            if (cam != null) _player = cam.transform;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            _hasWalkBool = HasParam(_walkBool);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 대화(심문) 중이면 멈추고 플레이어를 부드럽게 바라봄
            if (InterrogationController.AnyOpen)
            {
                SetWalking(false);
                FacePlayer(dt);
                return;
            }

            // 잠깐 쉬는 중
            if (_pauseTimer > 0f)
            {
                _pauseTimer -= dt;
                SetWalking(false);
                return;
            }

            // 목표 방향 결정: 반경 밖이면 집 쪽, 아니면 주기적으로 새 방향
            Vector3 fromHome = transform.position - _home;
            fromHome.y = 0f;
            if (fromHome.magnitude > _radius)
            {
                _targetHeading = (-fromHome).normalized;
            }
            else
            {
                _retargetTimer -= dt;
                if (_retargetTimer <= 0f)
                {
                    if (Random.value < _pauseChance)   // 가끔 잠깐 멈춤
                    {
                        _pauseTimer = _pauseTime;
                        SetWalking(false);
                        return;
                    }
                    PickNewDirection();
                    _retargetTimer = Random.Range(_straightMin, _straightMax);
                }
            }

            // 현재 방향을 목표로 부드럽게 회전
            float maxRad = _turnSpeed * Mathf.Deg2Rad * dt;
            _heading = Vector3.RotateTowards(_heading, _targetHeading, maxRad, 0f).normalized;
            transform.rotation = Quaternion.LookRotation(_heading);

            // 방향이 어긋난 동안엔 느리게(거의 제자리서 돎), 맞으면 정상 속도로 직진.
            // 단 최소 속도는 유지해 "돌기만 하고 안 나가는" 상태를 없앤다.
            float align = Vector3.Dot(_heading, _targetHeading);        // -1~1 (1=정렬)
            float moveFactor = Mathf.Lerp(1f - _turnSlowdown, 1f, Mathf.InverseLerp(0.4f, 1f, align));
            moveFactor = Mathf.Max(moveFactor, 0.18f);

            transform.position += _heading * _speed * moveFactor * dt;
            SetWalking(true);
        }

        private void PickNewDirection()
        {
            float ang = Random.Range(_turnAngleMin, _turnAngleMax) * (Random.value < 0.5f ? -1f : 1f);
            _targetHeading = (Quaternion.Euler(0f, ang, 0f) * _heading).normalized;
        }

        // 인형처럼 홱 돌지 않게 지수 감속(ease)으로 부드럽게 플레이어를 바라봄
        private void FacePlayer(float dt)
        {
            if (_player == null) return;
            Vector3 to = _player.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            Quaternion look = Quaternion.LookRotation(to);
            float t = 1f - Mathf.Exp(-_facePlayerEase * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, t);
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
