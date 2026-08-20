using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 에디터/비-VR 테스트용 선택 도구.
    /// 카메라에서 마우스 방향으로 레이를 쏘아 ISelectable 대상을 가리키고(하이라이트),
    /// 좌클릭하면 OnSelect()를 호출한다.
    ///
    /// ★ VR 단계에서는 이 컴포넌트 대신 컨트롤러 레이 인터랙터가 같은
    ///   ISelectable.OnHoverEnter/Exit/Select 를 호출하게 만든다.
    ///   즉, 선택 "대상" 로직(CaseCube 등)은 그대로 두고 입력 "방식"만 갈아끼운다.
    /// </summary>
    public class MouseRaySelector : MonoBehaviour
    {
        [Tooltip("레이를 쏠 카메라. 비우면 이 오브젝트의 Camera 또는 Camera.main 사용")]
        [SerializeField] private Camera _camera;

        [SerializeField] private float _maxDistance = 20f;

        [Tooltip("레이가 맞을 레이어(기본: 전부)")]
        [SerializeField] private LayerMask _mask = ~0;

        // 현재 가리키고 있는 대상
        private ISelectable _current;

        private void Awake()
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();
            if (_camera == null)
                _camera = Camera.main;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null || _camera == null) return;

            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(mousePos);

            // 지금 프레임에 가리키는 대상 찾기
            ISelectable hit = null;
            if (Physics.Raycast(ray, out RaycastHit info, _maxDistance, _mask))
                hit = info.collider.GetComponentInParent<ISelectable>();

            // 대상이 바뀌면 hover 전환
            if (!ReferenceEquals(hit, _current))
            {
                ReleaseHold();
                _current?.OnHoverExit();
                _current = hit;
                _current?.OnHoverEnter();
            }

            // 눌러 잡고 있어야 되는 것 — 잡은 대상에서 손이 벗어나면 놓은 것으로 친다.
            var holdable = _current as IHoldable;
            if (holdable != null && mouse.leftButton.isPressed)
            {
                _holding = holdable;
                holdable.OnHoldTick(Time.deltaTime);
                return;                      // 잡고 있는 동안엔 클릭으로 안 친다
            }
            ReleaseHold();

            // 좌클릭 = 선택
            if (mouse.leftButton.wasPressedThisFrame && holdable == null)
                _current?.OnSelect();
#endif
        }

        private IHoldable _holding;

        private void ReleaseHold()
        {
            if (_holding == null) return;
            _holding.OnHoldRelease();
            _holding = null;
        }
    }
}
