using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// "다가가서 · 바라보고 · 버튼" 방식으로 문(DoorHinge 여러 짝)을 여닫는 트리거.
    /// ★XR Interaction Toolkit을 쓰지 않는다(미설치). 손으로 잡는 방식이 아니라 근접+조준+버튼이다.
    ///
    /// [사용] 문 묶음의 부모 GO에 붙인다(예: 사립문 `Door01k (n)`).
    ///   Hinges           : 여닫을 DoorHinge들. ★비워 두면 자식에서 자동으로 모은다.
    ///   Trigger Radius   : 근접 판정 반경(m). 이 GO에 SphereCollider(isTrigger)가 없으면 자동 생성한다.
    ///   Aim Dot Min      : 카메라 forward와 "카메라→문" 방향의 내적 하한. 0.5 ≈ 60° 원뿔 안
    ///   Require Line Of Sight : 켜면 카메라→문 레이캐스트로 가림 검사. ★Boundary(8)는 마스크에서 제외한다
    ///   Interact Action  : 입력 액션 참조(권장). 비우면 아래 Fallback Key를 쓴다.
    ///                      ★액션에 Hold 인터랙션이 걸려 있어도 톡 누름으로 동작한다(InteractHeld 참조)
    ///   Fallback Key     : 액션이 비었을 때 쓸 키보드 키(임시 PC 워커 검증용, 기본 E)
    ///
    /// ★프롬프트 UI는 이 스크립트가 만들지 않는다. 표시 조건(PromptVisible)과
    ///   표시할 월드 위치(PromptWorldPosition), 문구(PromptText)만 노출한다. UI는 별도 작업.
    ///
    /// Meta 리그로 교체할 때는 Interact Action에 컨트롤러 버튼 액션을 물리면 되고
    /// 이 스크립트는 손대지 않는다(입력만 교체).
    /// </summary>
    [DisallowMultipleComponent]
    public class DoorInteractable : MonoBehaviour
    {
        [Header("대상")]
        [Tooltip("여닫을 문짝들. 비우면 자식에서 자동 수집")]
        [SerializeField] private DoorHinge[] _hinges;

        [Header("근접")]
        [Tooltip("근접 판정 반경(m). SphereCollider(isTrigger)가 없으면 자동 생성")]
        [SerializeField] private float _triggerRadius = 2.0f;
        [Tooltip("플레이어 태그. 태그가 안 맞아도 CharacterController가 있으면 플레이어로 본다")]
        [SerializeField] private string _playerTag = "Player";

        [Header("조준")]
        [Tooltip("카메라 forward와 '카메라→문' 방향의 내적 하한. 0.5 ≈ 60° 원뿔")]
        [SerializeField] private float _aimDotMin = 0.5f;
        [Tooltip("비우면 Camera.main")]
        [SerializeField] private Camera _playerCamera;
        [Tooltip("켜면 카메라→문 사이 가림(레이캐스트) 검사. Boundary(8)는 제외된다")]
        [SerializeField] private bool _requireLineOfSight = true;

        [Header("프롬프트 (UI는 별도 작업)")]
        [Tooltip("프롬프트를 띄울 위치. 비우면 이 GO 기준 아래 Offset")]
        [SerializeField] private Transform _promptAnchor;
        [SerializeField] private Vector3 _promptOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private string _promptOpenText = "문 열기";
        [SerializeField] private string _promptCloseText = "문 닫기";

        [Header("입력")]
#if ENABLE_INPUT_SYSTEM
        [Tooltip("권장: InputSystem_Actions의 Player/Interact 등을 물린다. 비우면 Fallback Key 사용")]
        [SerializeField] private InputActionReference _interactAction;
        [Tooltip("액션이 비었을 때 쓸 키(임시 PC 워커 검증용)")]
        [SerializeField] private Key _fallbackKey = Key.E;
#endif

        /// <summary>★Boundary 레이어(8)는 시야 판정에서 제외한다.</summary>
        private const int BoundaryLayer = 8;

        private Transform _player;
        private bool _inRange;

        /// <summary>상호작용 버튼이 직전 프레임에 눌려 있었는가(엣지 판정용).</summary>
        private bool _interactHeld;

        /// <summary>프롬프트를 지금 띄워야 하는가(근접 + 조준 + 가림없음 모두 만족).</summary>
        public bool PromptVisible { get; private set; }

        /// <summary>프롬프트를 띄울 월드 위치.</summary>
        public Vector3 PromptWorldPosition =>
            _promptAnchor != null ? _promptAnchor.position : transform.position + _promptOffset;

        /// <summary>지금 상황에 맞는 프롬프트 문구.</summary>
        public string PromptText => AnyWantsOpen() ? _promptCloseText : _promptOpenText;

        private void Awake()
        {
            if (_hinges == null || _hinges.Length == 0)
                _hinges = GetComponentsInChildren<DoorHinge>(true);

            var sc = GetComponent<SphereCollider>();
            if (sc == null)
            {
                sc = gameObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
            }
            sc.radius = _triggerRadius;
        }

#if ENABLE_INPUT_SYSTEM
        private void OnEnable()
        {
            if (_interactAction != null && _interactAction.action != null)
                _interactAction.action.Enable();
        }
#endif

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            _player = other.transform;
            _inRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            _inRange = false;
            _player = null;
            PromptVisible = false;
        }

        private bool IsPlayer(Collider c)
        {
            if (c == null) return false;
            if (!string.IsNullOrEmpty(_playerTag) && c.CompareTag(_playerTag)) return true;
            return c.GetComponentInParent<CharacterController>() != null;
        }

        private void Update()
        {
            // ★엣지는 프롬프트와 무관하게 매 프레임 갱신한다. 그렇지 않으면 버튼을 누른 채
            //   문에 다가왔을 때 "누른 적 없는 입력"이 한 번 먹는다.
            bool pressedThisFrame = InteractPressed();

            PromptVisible = _inRange && IsAimedAt();
            if (!PromptVisible) return;
            if (!pressedThisFrame) return;

            for (int i = 0; i < _hinges.Length; i++)
                if (_hinges[i] != null) _hinges[i].Toggle();
        }

        private bool IsAimedAt()
        {
            var cam = _playerCamera != null ? _playerCamera : Camera.main;
            if (cam == null) return false;

            Vector3 target = PromptWorldPosition;
            Vector3 to = target - cam.transform.position;
            float dist = to.magnitude;
            if (dist < 1e-4f) return true;
            if (Vector3.Dot(cam.transform.forward, to / dist) < _aimDotMin) return false;

            if (_requireLineOfSight)
            {
                // ★Boundary(8)는 투명 경계벽이므로 시야를 막는 것으로 보지 않는다.
                int mask = ~(1 << BoundaryLayer);
                RaycastHit hit;
                if (Physics.Raycast(cam.transform.position, to / dist, out hit, dist, mask,
                                    QueryTriggerInteraction.Ignore))
                {
                    // 문 자신(또는 그 자식)에 맞은 것은 가림이 아니다.
                    if (!hit.transform.IsChildOf(transform)) return false;
                }
            }
            return true;
        }

        /// <summary>이번 프레임에 상호작용 버튼이 "새로" 눌렸는가(누름 엣지).</summary>
        private bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            bool held = InteractHeld();
            bool edge = held && !_interactHeld;
            _interactHeld = held;
            return edge;
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// 지금 상호작용 버튼이 눌려 있는가.
        ///
        /// ★`WasPressedThisFrame()`을 쓰지 않는다. 팀 공통 `InputSystem_Actions`의
        ///   `Player/Interact`에는 <b>Hold 인터랙션</b>이 걸려 있어서 액션 phase가
        ///   0.4초 뒤에야 진행된다 — 톡 누름이 통째로 무시된다.
        ///   액션 에셋은 다른 시스템도 쓰므로 건드리지 않고, 여기서
        ///   ①액션 진행 상태와 ②바인딩된 컨트롤의 실제 눌림을 함께 본다.
        ///   ②를 보기 때문에 Hold·Tap 등 어떤 인터랙션이 붙어도 즉시 반응한다.
        /// </summary>
        private bool InteractHeld()
        {
            var action = (_interactAction != null) ? _interactAction.action : null;
            if (action != null)
            {
                if (action.IsPressed()) return true;

                var controls = action.controls;
                for (int i = 0; i < controls.Count; i++)
                {
                    var button = controls[i] as ButtonControl;
                    if (button != null && button.isPressed) return true;
                }
                return false;
            }

            var kb = Keyboard.current;
            return kb != null && kb[_fallbackKey].isPressed;
        }
#endif

        private bool AnyWantsOpen()
        {
            if (_hinges == null) return false;
            for (int i = 0; i < _hinges.Length; i++)
                if (_hinges[i] != null && _hinges[i].WantsOpen) return true;
            return false;
        }

        private void OnValidate()
        {
            var sc = GetComponent<SphereCollider>();
            if (sc != null) sc.radius = _triggerRadius;
        }
    }
}
