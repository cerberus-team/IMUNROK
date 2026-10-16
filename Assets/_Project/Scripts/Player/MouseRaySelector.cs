using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 마우스로 짚는 선택 도구.
    /// 카메라에서 마우스 방향으로 레이를 쏘아 ISelectable 대상을 가리키고(하이라이트),
    /// 좌클릭하면 OnSelect()를 호출한다.
    ///
    /// ★ 짚는 방식이 바뀌면 이 부품 대신 다른 것이 같은
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

            // <b>화면 판 위에서는 방을 짚지 않는다.</b>
            //
            // 자막의 「✕ 닫기」를 누르면 그 너머의 문이 열리고 있었다. 화면 UI 와 세상이
            // 같은 누름 하나를 나눠 갖고 있었기 때문이다 — 이 프로젝트에서 손이 판 위에
            // 있는지 묻는 데가 <b>한 곳도 없었다</b>.
            //
            // 가리키는 것까지 함께 끊는다. 판 뒤의 물건이 판 너머에서 밝아 있으면
            // 「이건 눌러도 되나」가 흐려진다 — 덮인 것은 안 보이는 것이 맞다.
            if (UiGuard.OverPanel) { ReleaseHold(); _current?.OnHoverExit(); _current = null; return; }

            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(mousePos);

            // 지금 프레임에 가리키는 대상 찾기
            ISelectable hit = null;
            // <b>「Ignore Raycast」 켜는 손이 짚지 않는다.</b>
            //
            // 빛줄기가 맞힐 과녁은 <b>눈에 안 보이는 상자</b>다(관아 문서궤의 뚜껑 자국).
            // 그 상자가 열린 궤 위를 통째로 덮고 있어서, 궤 속 입안대장을 어느 자리에서도
            // 누를 수가 없었다 — 甲을 무너뜨리는 열쇠 둘 가운데 하나다. 빛은 그 상자를
            // 맞혀야 하고, 손은 그 상자를 지나가야 한다. 켜로 가른다: 빛줄기는 제
            // 거르개가 「모두」라 여전히 맞히고, 손은 이 켜를 건너뛴다.
            // (씬 넷을 훑어 이 켜에 누를 수 있는 것이 하나도 없는 것을 확인했다.)
            int clickMask = _mask & ~(1 << IgnoreRaycastLayer);
            if (Physics.Raycast(ray, out RaycastHit info, _maxDistance, clickMask))
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

            // <b>판 위에서 시작한 누름은 끝날 때까지 방에 안 닿는다.</b> 아래 좌클릭 대목의
            // 가림은 「톡 누르기」만 막았다 — 잡아 끄는 것(보료·서랍)은 그보다 먼저 여기서
            // 누름을 가져가, 「×」를 누르는 손이 뒤의 물건을 들추기 시작했다.
            if (mouse.leftButton.wasPressedThisFrame) _pressOnUi = OverClickableUi(mousePos);
            if (!mouse.leftButton.isPressed) _pressOnUi = false;
            if (_pressOnUi) { ReleaseHold(); return; }

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
                // <b>판 위를 누른 손은 방까지 닿지 않는다.</b>
                //
                // 이 손은 화면에 뜬 판을 모른 채 늘 세상으로 광선을 쐈다. 그래서
                // 「×」를 누르면 자막이 닫히는 <b>동시에</b> 그 뒤의 물건도 짚혔다 —
                // 요 위에 누운 채로 자막을 닫으려 하면 요가 함께 짚여 벌떡 일어났다.
                // 한 번 누른 것이 두 군데서 듣는다.
                //
                // <b>눌리는 판만 가린다.</b> 자막 바탕처럼 아무 일도 안 하는 판까지
                // 가리면 화면 아래 세 몫이 통째로 죽은 자리가 된다 — 상 위의 게장도
                // 마루에 떨어진 종이도 그 안에 있다. 그래서 맨 위에 잡힌 것이
                // <b>누름을 받는 판</b>일 때에만 손을 거둔다.
                if (OverClickableUi(mousePos)) return;
                if (TouchRefusal.Blocks(_current as Component)) return;
                // 사람을 눌렀으면 먼저 이쪽을 돌아본다 — 벽을 보고 대답하는 사람은 없다
                FaceThePlayer.Notify(_current as Component);
                _current?.OnSelect();
            }
#endif
        }

        /// <summary>
        /// 지금 손끝이 <b>눌리는 판</b> 위에 있나.
        ///
        /// <see cref="UnityEngine.EventSystems.EventSystem.IsPointerOverGameObject"/> 를 쓰지
        /// 않는 까닭: 그것은 <b>레이를 받는 것 아무거나</b>에 참이라 자막 바탕에도 걸린다.
        /// 우리가 가리려는 것은 눌러서 무슨 일이 나는 자리(단추·칸)뿐이다.
        /// </summary>
        private static bool OverClickableUi(Vector2 screenPos)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            // 판을 세우는 쪽이 EventSystem 을 늦게 만들 수 있어, 쓸 때 잡는다.
            if (_uiProbe == null) _uiProbe = new UnityEngine.EventSystems.PointerEventData(es);
            _uiProbe.position = screenPos;
            _uiHits.Clear();
            es.RaycastAll(_uiProbe, _uiHits);
            for (int i = 0; i < _uiHits.Count; i++)
            {
                var go = _uiHits[i].gameObject;
                if (go == null) continue;
                // 맨 위에 잡힌 것에서 거슬러 올라가며 <b>누름을 받는 것</b>을 찾는다.
                // 단추의 글자(TMP)가 맨 위에 잡히는 일이 흔하므로 부모까지 본다.
                if (go.GetComponentInParent<UnityEngine.EventSystems.IPointerClickHandler>() != null) return true;
            }
            return false;
        }

        private static UnityEngine.EventSystems.PointerEventData _uiProbe;
        private static readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> _uiHits =
            new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();

        /// <summary>유니티 내장 켜 「Ignore Raycast」 — 늘 2번이다.</summary>
        private const int IgnoreRaycastLayer = 2;

        private IHoldable _holding;

        /// <summary>지금 누르고 있는 손이 판 위에서 눌렸나.</summary>
        private bool _pressOnUi;

        private void ReleaseHold()
        {
            if (_holding == null) return;
            _holding.OnHoldRelease();
            _holding = null;
        }
    }
}
