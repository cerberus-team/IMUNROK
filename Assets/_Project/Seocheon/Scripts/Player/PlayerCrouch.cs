using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Seocheon.Player
{
    /// <summary>
    /// 웅크리기 — 한옥 문 개구(1.41~1.45m)를 통과하기 위한 높이 제어.
    ///
    /// [조작]  왼쪽 Ctrl 을 누르고 있는 동안 웅크림(홀드). New Input System 전용.
    ///
    /// [VR 전환 지점]
    ///   목표 높이 결정은 <see cref="GetTargetHeight"/> 하나에 모여 있다.
    ///   Meta 리그로 갈아탈 때 이 함수만 HMD 로컬 Y(카메라 높이)를 읽도록 바꾸면
    ///   나머지(캡슐 갱신·카메라 보간·머리 위 검사)는 그대로 재사용된다.
    ///   예)  protected virtual float GetTargetHeight()
    ///            => Mathf.Clamp(_hmd.localPosition.y + _eyeToTop, _crouchHeight, _standHeight);
    ///
    /// [주의]
    ///   · radius 는 건드리지 않는다(0.30 고정). 문 유효폭 0.70 에 맞춰져 있다.
    ///   · height 와 center 는 반드시 함께 갱신한다(center.y = height/2).
    ///     center 를 안 바꾸면 발이 바닥에 파묻히거나 뜬다.
    ///   · 일어설 때는 머리 위를 SphereCast 로 검사하고, 막혀 있으면 웅크린 채 유지한다.
    ///     검사 마스크에서 Boundary(8) 는 제외한다(보이지 않는 경계벽이라 천장이 아니다).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class PlayerCrouch : MonoBehaviour
    {
        public const int BoundaryLayer = 8;

        [Header("서 있기")]
        [SerializeField] private float _standHeight = 1.85f;
        [SerializeField] private float _standEyeY = 1.70f;

        [Header("웅크리기")]
        [SerializeField] private float _crouchHeight = 1.30f;
        [SerializeField] private float _crouchEyeY = 1.15f;

        [Header("보간")]
        [Tooltip("서기<->웅크리기 전체 전환에 걸리는 시간(초). 0.15~0.20 권장. 즉시 전환은 멀미를 유발한다.")]
        [SerializeField] private float _transitionTime = 0.18f;

        [Header("머리 위 검사")]
        [Tooltip("일어설 공간이 있는지 검사할 레이어. Boundary(8)는 런타임에 강제로 제외된다.")]
        [SerializeField] private LayerMask _headCheckMask = ~0;
        [Tooltip("검사 구체 반지름을 캡슐 반지름보다 이만큼 줄인다(벽 스침 오검출 방지).")]
        [SerializeField] private float _headCheckShrink = 0.02f;
        [Tooltip("천장까지 이만큼 여유가 더 있어야 일어선다.")]
        [SerializeField] private float _headCheckMargin = 0.02f;

        [Header("참조")]
        [Tooltip("눈높이를 적용할 카메라. 비우면 자식 카메라를 자동 탐색.")]
        [SerializeField] private Transform _cameraPivot;

        private CharacterController _cc;
        private float _currentHeight;
        private bool _crouchInput;
        private bool _externalCrouch;
        private bool _blockedAbove;

        /// <summary>현재 캡슐 높이(보간 중 값).</summary>
        public float CurrentHeight { get { return _currentHeight; } }
        /// <summary>서 있는 높이에 도달하지 못한 상태인가.</summary>
        public bool IsCrouching { get { return _currentHeight < _standHeight - 0.001f; } }
        /// <summary>직전 프레임의 머리 위 검사 결과 — 막혀 있으면 true.</summary>
        public bool BlockedAbove { get { return _blockedAbove; } }
        public float StandHeight { get { return _standHeight; } }
        public float CrouchHeight { get { return _crouchHeight; } }

        /// <summary>테스트 하네스·컷신 등 외부에서 강제로 웅크리게 할 때.</summary>
        public void SetExternalCrouch(bool value) { _externalCrouch = value; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) _cameraPivot = cam.transform;
            }
            _currentHeight = _cc.height;
            ApplyHeight(_currentHeight);
        }

        private void Update()
        {
            ReadInput();

            float target = GetTargetHeight();

            float span = Mathf.Max(_standHeight - _crouchHeight, 0.0001f);
            float speed = span / Mathf.Max(_transitionTime, 0.0001f);
            _currentHeight = Mathf.MoveTowards(_currentHeight, target, speed * Time.deltaTime);

            ApplyHeight(_currentHeight);
        }

        private void ReadInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            bool key = kb != null && kb.leftCtrlKey.isPressed;
#else
            bool key = Input.GetKey(KeyCode.LeftControl);
#endif
            _crouchInput = key || _externalCrouch;
        }

        /// <summary>
        /// ★VR 전환 지점 — 목표 캡슐 높이를 정하는 유일한 곳.
        /// 데스크탑: Ctrl 홀드 = 웅크림, 놓으면 머리 위가 트인 경우에만 일어섬.
        /// </summary>
        public virtual float GetTargetHeight()
        {
            if (_crouchInput)
            {
                _blockedAbove = false;
                return _crouchHeight;
            }

            _blockedAbove = !HasHeadroomToStand();
            return _blockedAbove ? _crouchHeight : _standHeight;
        }

        /// <summary>서 있는 높이까지 머리 위 공간이 있는가.</summary>
        public bool HasHeadroomToStand()
        {
            float rise = _standHeight - _currentHeight;
            if (rise <= 0.0001f) return true;

            int mask = _headCheckMask.value & ~(1 << BoundaryLayer);
            if (mask == 0) return true;

            float r = Mathf.Max(_cc.radius - _headCheckShrink, 0.01f);
            // 현재 캡슐 윗구 중심 (발 = transform.position 기준, center.y = height/2 유지 전제)
            Vector3 topSphere = transform.position
                              + transform.up * (_currentHeight - _cc.radius)
                              + transform.right * _cc.center.x
                              + transform.forward * _cc.center.z;

            var hits = Physics.SphereCastAll(topSphere, r, transform.up, rise + _headCheckMargin,
                                             mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col == null) continue;
                if (col == _cc) continue;                                   // 자기 자신
                if (col.transform.IsChildOf(transform)) continue;           // 리그 하위
                return false;
            }
            return true;
        }

        private void ApplyHeight(float h)
        {
            _cc.height = h;
            Vector3 c = _cc.center;
            c.y = h * 0.5f;
            _cc.center = c;

            if (_cameraPivot != null)
            {
                float t = Mathf.InverseLerp(_crouchHeight, _standHeight, h);
                Vector3 lp = _cameraPivot.localPosition;
                lp.y = Mathf.Lerp(_crouchEyeY, _standEyeY, t);
                _cameraPivot.localPosition = lp;
            }
        }
    }
}
