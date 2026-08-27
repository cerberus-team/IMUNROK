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

            // 종이를 쥐고 있는 동안에는 방을 짚지 않는다.
            //
            // 종이는 끌어서 돌린다. 그 끄는 손이 그대로 방을 짚으면, 문서를 돌려 보려다
            // 등 뒤의 장을 열게 되고 쥐고 있던 종이가 다른 종이로 바뀐다.
            // 한 손으로 두 가지를 할 수는 없다 — 먼저 내려놓아야(Esc) 방에 손이 간다.
            if (DocumentView.IsOpen) { ReleaseHold(); _current?.OnHoverExit(); _current = null; return; }

            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(mousePos);

            // 지금 프레임에 가리키는 대상 찾기
            ISelectable hit = null;
            if (Physics.Raycast(ray, out RaycastHit info, _maxDistance, _mask))
            {
                hit = info.collider.GetComponentInParent<ISelectable>();
                // <b>어디를 짚었는지</b>를 남겨 둔다. 두 짝 문처럼 한 물건 안에서도
                // 짚은 자리에 따라 하는 일이 다른 것이 있다(DoorController).
                Pointing.Set(info.point, info.collider != null ? info.collider.transform : null);
            }

            // 도구를 익히는 동안에는 손이 방으로 가지 않는다.
            //
            // 익히는 중에는 <b>어디를 눌러도</b> 다음 마디로 넘어간다(ToolTutorial).
            // 그 누름이 방에도 닿으면, 눈앞에 든 돋보기를 뚫고 나간 레이가 뒤의
            // 문짝을 맞혀 한 마디 넘길 때마다 문이 여닫힌다.
            // 자막의 닫기 표만은 예외다 — 그것이 그만두는 유일한 길이다.
            if (ToolTutorial.Learning && !(hit is NoticeCloseTab)) hit = null;

            // 대상이 바뀌면 hover 전환
            if (!ReferenceEquals(hit, _current))
            {
                ReleaseHold();
                _current?.OnHoverExit();
                _current = hit;
                _current?.OnHoverEnter();
            }

            // 눌러 잡고 있어야 되는 것 — 잡은 대상에서 손이 벗어나면 놓은 것으로 친다.
            // 잡을 일이 끝난 것은 더 이상 "잡는 것"이 아니다 — 톡 누르기로 흘려보낸다.
            // 안 그러면 다 들춘 보료를 도로 내려놓을 길이 없다(IHoldable.HoldReady 참고).
            var holdable = _current as IHoldable;
            if (holdable != null && !holdable.HoldReady) holdable = null;

            if (holdable != null && mouse.leftButton.isPressed)
            {
                // 마주 앉은 자리에서는 손이 먼저 제지당한다
                if (mouse.leftButton.wasPressedThisFrame && TouchRefusal.Blocks(_current as Component)) return;
                _holding = holdable;
                holdable.OnHoldTick(Time.deltaTime);
                return;                      // 잡고 있는 동안엔 클릭으로 안 친다
            }
            ReleaseHold();

            // 좌클릭 = 선택
            if (mouse.leftButton.wasPressedThisFrame && holdable == null)
            {
                if (TouchRefusal.Blocks(_current as Component)) return;
                // 사람을 눌렀으면 먼저 이쪽을 돌아본다 — 벽을 보고 대답하는 사람은 없다
                FaceThePlayer.Notify(_current as Component);
                _current?.OnSelect();
            }
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
