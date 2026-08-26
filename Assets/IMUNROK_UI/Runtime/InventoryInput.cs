using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Ui
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
    /// ■ VR (2026-08-26 — 이제 갈아끼움이 끝났다)
    ///   조준·버튼은 <see cref="UiPointers"/> 가 준다. PC면 마우스, VR이면 컨트롤러 광선이
    ///   같은 자리로 들어온다. 여기 코드는 <b>어느 쪽인지 알지 못한다</b> — 그게 목표였다.
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
        void OnEnable() => UiItems.Source.Added += OnItemAdded;

        void OnDisable()
        {
            UiItems.Source.Added -= OnItemAdded;
            ReleaseLocks();
        }

        void OnItemAdded(IUiItem item)
        {
            if (item == null || !item.AutoShowOnPickup) return;
            if (!isActiveAndEnabled) return;
            OpenUI(item, true);
        }

        void Update()
        {
            // 가리키개 하나로 마우스/컨트롤러를 갈아끼운다 (2026-08-26).
            // PC = 마우스 커서 광선 + 좌/우클릭 + 휠 + I·Esc, VR = 컨트롤러 광선 + 트리거 + 스틱 + 메뉴·B.
            var ptr = UiPointers.Get(transform);
            if (!ptr.Available) return;

            if (focus == null) focus = GetComponent<DebugFocusRig>();
            bool focusing = focus != null && focus.IsFocusing;

            // ── 여닫기 ──
            // 가리키개의 '메뉴'(PC=I, VR=메뉴 버튼)와, 인스펙터에서 바꿔 둔 글쇠 둘 다 받는다 —
            // toggleKey 를 다른 키로 바꿔 둔 씬이 있어도 그대로 돈다.
            var kbNow = Keyboard.current;
            bool toggle = ptr.MenuDown
                       || (UiModes.IsPc && toggleKey != Key.I && kbNow != null && kbNow[toggleKey].wasPressedThisFrame);
            if (toggle)
            {
                if (ui != null && ui.IsOpen) CloseUI();
                else if (!focusing) OpenUI();          // 퍼즐 조작 중에는 판을 열지 않는다
                return;
            }

            if (ui == null || !ui.IsOpen) return;

            // ── 물러나기 (상세 ▸ 목록 ▸ 닫기) ──
            if (ptr.BackDown)
            {
                if (!ui.Back()) CloseUI();
                return;
            }

            // ── 가리키기 ──
            ui.PointAt(ptr.PointRay);

            // ── 누르기 / 드래그 ──
            if (ptr.PressDown)
            {
                pressed = ui.Hovered;
                dragDist = 0f;
                // 빈 곳에서 시작한 드래그 = 모델 돌리기 (판이 떠 있는 동안 시선은 이미 멈춰 있다).
                // 어느 화면에서 돌릴 수 있는지는 판이 안다 — 여기서 모드를 따로 세지 않는다.
                rotating = pressed == null && ui.CanRotate;
            }

            if (ptr.PressHeld)
            {
                Vector2 d = ptr.Delta;
                dragDist += d.magnitude;
                if (rotating && d.sqrMagnitude > 0.0001f) ui.Drag(d);
            }

            if (ptr.PressUp)
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
            //    (VR 스틱도 같은 규칙을 탄다 — 가리키개가 ±1로 정규화해 준다)
            float sc = ptr.ScrollRaw;
            if (Mathf.Abs(sc) > 0.5f && Mathf.Abs(prevScroll) <= 0.5f) ui.Scroll(sc);
            prevScroll = sc;
        }

        /// <summary>
        /// **소지품이 아닌 것**을 전체 화면 조사로 띄운다 (2026-08-24, 서고 장부).
        ///
        /// 장부 기물은 손에 넣는 물건이 아니라 선반 위에서 들여다보기만 하는 것이다. 그러나
        /// 돌려 보기·확대·어두운 막은 조사 화면에 이미 다 있으므로, 목록을 거치지 않고
        /// 그 화면만 연다. 닫으면(Esc·우클릭·✕) 판까지 함께 닫히고 원래 하던 일로 돌아간다 —
        /// 획득 직후 화면(<c>PickupMode</c>)과 같은 규약이다.
        ///
        /// <paramref name="traits"/>를 주면 물건 곁에 "살펴본 것"이 함께 적힌다.
        /// </summary>
        public void InspectExternal(IUiItem item, string[] traits = null)
        {
            if (item == null) return;
            InventoryUI.Ensure().ExternalTraits = traits;
            OpenUI(item, true);
        }

        /// <summary>
        /// 대화 중 <b>증거를 고르는 화면</b>으로 판을 연다 (2026-08-25).
        /// 여는 문이 하나여야 걷기·조준·커서 잠금이 갈라지지 않으므로 소지품과 같은 길로 낸다 —
        /// <see cref="InspectExternal"/> 과 같은 규약이다.
        /// </summary>
        public void OpenPresent(System.Func<System.Collections.Generic.IReadOnlyList<IUiItem>> source,
                                System.Action<IUiItem> onPresent)
        {
            OpenUI(null, false, source, onPresent);
        }

        void OpenUI(IUiItem showItem = null, bool pickup = false,
                    System.Func<System.Collections.Generic.IReadOnlyList<IUiItem>> presentSource = null,
                    System.Action<IUiItem> onPresent = null)
        {
            if (ui != null && ui.IsOpen) ui.Close();
            ui = InventoryUI.Ensure();
            ui.CloseRequested -= CloseUI;      // 중복 구독 방지 (판은 씬을 넘겨 살아남는다)
            ui.CloseRequested += CloseUI;
            if (presentSource != null) ui.OpenForPresent(transform, presentSource, onPresent);
            else ui.Open(transform, showItem, pickup);
            if (walk != null) walk.uiOpen = true;   // 이동·시선 정지 (마우스를 조준에 내준다)
            if (interactor != null) interactor.enabled = false;   // 판 너머 세상은 잠시 못 만진다

            // 커서를 창 안에 가두되 **하드웨어 커서는 숨긴다** — 판 위에 우리가 그리는 조준점이
            // 진짜 커서다. 그래야 VR HMD에서도, 스크린샷에서도 같은 것이 보인다.
            // VR에서는 커서라는 것이 아예 없으므로 건드리지 않는다 (컨트롤러가 가리킨다).
            if (UiModes.IsPc)
            {
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = false;
            }
        }

        /// <summary>바깥에서 판을 닫는다 (증거를 골라 내민 뒤 등). 잠금 해제까지 같은 길로 처리된다.</summary>
        public void ClosePanel() => CloseUI();

        void CloseUI()
        {
            if (ui != null) ui.Close();
            ReleaseLocks();
        }

        void ReleaseLocks()
        {
            if (walk != null) { walk.uiOpen = false; walk.lookLocked = false; }
            // ⚠️ 포커스 퍼즐 도중에 조사 화면을 열었다 닫은 경우 — 조준을 되살리면 안 된다.
            //    포커스 리그가 진입할 때 꺼 둔 것이고, 물러날 때 스스로 되살린다 (2026-08-24).
            if (focus == null) focus = GetComponent<DebugFocusRig>();
            bool focusing = focus != null && focus.IsFocusing;
            if (interactor != null && !focusing) interactor.enabled = true;
            pressed = null;
            rotating = false;
            // 걷기로 복귀 — 커서를 다시 화면 중앙에 붙잡아 마우스가 시선이 된다 (PC 전용 규약)
            if (UiModes.IsPc && walk != null && walk.enabled)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
