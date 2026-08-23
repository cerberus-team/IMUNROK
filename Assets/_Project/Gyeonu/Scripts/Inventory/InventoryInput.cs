using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 소지품 판의 **입력 측** (마우스·키보드 임시 — VR 컨트롤러로 교체 예정, 2026-08-23).
    /// 워커 카메라에 자동으로 붙는다 (DebugWalkController.Awake가 보강).
    ///
    /// ■ 조작
    ///   I ............ 열기 / 닫기
    ///   Esc / 우클릭 .. 상세 ▸ 목록 ▸ 닫기 (한 단계씩 물러난다)
    ///   시선 .......... 판 위 커서 (판은 월드에 놓여 있어 고개를 돌려 가리킨다)
    ///   좌클릭 ........ 가리킨 칸·버튼 누르기
    ///   좌드래그 ...... 상세에서 모델 돌리기 (버튼 위가 아닐 때)
    ///   휠 ............ 확대·축소 / 글 굴리기 / 쪽 넘기기
    ///
    /// ■ VR로 갈 때 바꿀 곳은 여기 하나뿐이다
    ///   <see cref="Ray"/>를 컨트롤러 광선으로, 좌클릭을 트리거로, I를 메뉴 버튼으로 바꾸고
    ///   InventoryUI의 PointAt/Activate/Drag/Scroll/Back을 그대로 부르면 된다.
    ///   판·미리보기 코드는 손대지 않는다.
    ///
    /// ■ 왜 I 인가 (Tab이 아니라)
    ///   Tab은 에디터 Game 뷰·브라우저에서 포커스 이동으로 먹히는 일이 잦고, 앞으로 붙을
    ///   수첩(Journal) UI와 짝을 맞추기에도 I(nventory)/J(ournal)가 헷갈리지 않는다.
    /// </summary>
    public class InventoryInput : MonoBehaviour
    {
        [Tooltip("판을 여닫는 키")]
        public Key toggleKey = Key.I;

        [Tooltip("버튼을 누른 것으로 칠 최대 드래그 거리(픽셀). 이보다 크면 돌리기로 본다")]
        public float clickSlop = 6f;

        DebugWalkController walk;
        DebugInteractor interactor;
        DebugFocusRig focus;
        Camera eyeCam;                // 화면 커서 → 광선 변환용 (VR에서는 컨트롤러가 대신한다)

        InventoryUI ui;
        InventoryHotspot pressed;     // 누르기 시작한 자리
        float dragDist;               // 누른 뒤 움직인 거리 — 클릭/드래그 판정
        bool rotating;                // 빈 곳에서 시작한 드래그 = 모델 돌리기
        float prevScroll;             // 휠 한 칸 판정용 (직전 프레임 값)

        void Awake()
        {
            walk = GetComponentInParent<DebugWalkController>();
            interactor = GetComponent<DebugInteractor>();

            // 판은 씬을 넘겨 살아남는다(DontDestroyOnLoad). 열어 둔 채 씬이 바뀌면
            // 이전 씬 자리에 뜬 채로 남으므로, 새 씬의 입력이 붙을 때 닫아 둔다.
            if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) InventoryUI.Instance.Close();
        }

        // 새로 얻은 물건은 목록이 아니라 **상세 화면**으로 먼저 보여 준다 (2026-08-24).
        // 같은 물건은 두 번 담기지 않으므로 이 알림도 물건당 한 번뿐이다 — 두 번째부터는
        // ItemPickup의 획득 문구만 뜬다. 입력 측이 맡는 까닭: 판을 여는 순간 걷기·조준을
        // 잠가야 하는데 그 권한이 여기 있다.
        void OnEnable() => Inventory.Added += OnItemAdded;

        void OnDisable()
        {
            Inventory.Added -= OnItemAdded;
            ReleaseLocks();
        }

        void OnItemAdded(InventoryItem item)
        {
            if (item == null || !item.autoShowOnPickup) return;
            if (!isActiveAndEnabled) return;
            OpenUI(item, true);
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (focus == null) focus = GetComponent<DebugFocusRig>();
            bool focusing = focus != null && focus.IsFocusing;

            // ── 여닫기 ──
            if (kb[toggleKey].wasPressedThisFrame)
            {
                if (ui != null && ui.IsOpen) CloseUI();
                else if (!focusing) OpenUI();          // 퍼즐 조작 중에는 판을 열지 않는다
                return;
            }

            if (ui == null || !ui.IsOpen) return;

            // ── 물러나기 (상세 ▸ 목록 ▸ 닫기) ──
            if (kb.escapeKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
            {
                if (!ui.Back()) CloseUI();
                return;
            }

            // ── 가리키기 ──
            ui.PointAt(PointerRay());

            // ── 누르기 / 드래그 ──
            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressed = ui.Hovered;
                dragDist = 0f;
                // 빈 곳에서 시작한 드래그 = 모델 돌리기 (판이 떠 있는 동안 시선은 이미 멈춰 있다).
                // 어느 화면에서 돌릴 수 있는지는 판이 안다 — 여기서 모드를 따로 세지 않는다.
                rotating = pressed == null && ui.CanRotate;
            }

            if (mouse.leftButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                dragDist += d.magnitude;
                if (rotating && d.sqrMagnitude > 0.0001f) ui.Drag(d);
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                // 같은 자리에서 떼었고 거의 안 움직였으면 누른 것
                if (!rotating && pressed != null && pressed == ui.Hovered && dragDist <= clickSlop)
                    ui.Activate(pressed);
                pressed = null;
                rotating = false;
            }

            // 휠은 **한 칸씩** 먹인다 (2026-08-24).
            // ⚠️ 매 프레임 값을 그대로 흘려 보내면, 값이 한 번 붙어 있는 동안 수십 번 호출돼
            //    상세 화면을 열자마자 설명이 끝까지 굴러가 버린다(실측 — 첫 줄이 안 보였다).
            //    떨어졌다 올라오는 순간만 잡으면 실제 휠 한 칸 = 한 번이 된다.
            float sc = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(sc) > 0.5f && Mathf.Abs(prevScroll) <= 0.5f) ui.Scroll(sc);
            prevScroll = sc;
        }

        /// <summary>
        /// 판을 가리키는 광선 — **VR로 갈 때 갈아끼울 곳은 여기 한 군데다.**
        /// 지금은 화면 커서 위치에서 쏜다. 컨트롤러 리그가 붙으면
        /// `new Ray(controller.position, controller.forward)` 한 줄로 바뀌고,
        /// 판·조준·클릭 판정 코드는 그대로 쓴다.
        ///
        /// ⚠️ 카메라 정면(transform.forward)으로 쏘면 안 된다 — 그러면 조준점이 화면
        ///    한가운데 못 박혀 마우스로 칸을 고를 수가 없다(2026-08-24 실측).
        /// </summary>
        Ray PointerRay()
        {
            var cam = eyeCam != null ? eyeCam : (eyeCam = GetComponent<Camera>());
            var m = Mouse.current;
            if (cam != null && m != null)
                return cam.ScreenPointToRay(m.position.ReadValue());
            return new Ray(transform.position, transform.forward);   // 마우스가 없으면 시선 조준
        }

        void OpenUI(InventoryItem showItem = null, bool pickup = false)
        {
            if (ui != null && ui.IsOpen) ui.Close();
            ui = InventoryUI.Ensure();
            ui.CloseRequested -= CloseUI;      // 중복 구독 방지 (판은 씬을 넘겨 살아남는다)
            ui.CloseRequested += CloseUI;
            ui.Open(transform, showItem, pickup);
            if (walk != null) walk.uiOpen = true;   // 이동·시선 정지 (마우스를 조준에 내준다)
            if (interactor != null) interactor.enabled = false;   // 판 너머 세상은 잠시 못 만진다

            // 커서를 창 안에 가두되 **하드웨어 커서는 숨긴다** — 판 위에 우리가 그리는 조준점이
            // 진짜 커서다. 그래야 VR HMD에서도, 스크린샷에서도 같은 것이 보인다.
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = false;
        }

        void CloseUI()
        {
            if (ui != null) ui.Close();
            ReleaseLocks();
        }

        void ReleaseLocks()
        {
            if (walk != null) { walk.uiOpen = false; walk.lookLocked = false; }
            if (interactor != null) interactor.enabled = true;
            pressed = null;
            rotating = false;
            // 걷기로 복귀 — 커서를 다시 화면 중앙에 붙잡아 마우스가 시선이 된다
            if (walk != null && walk.enabled)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
