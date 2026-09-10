using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>화면 판이 세상보다 먼저 손을 받는다</b> — 그것을 한 군데에서 묻는 문지기.
    ///
    /// 화면에 뜬 단추를 눌렀는데 <b>뒤에 있는 방이 반응했다</b>. 자막의 「✕ 닫기」를
    /// 누르면 그 너머의 문이 열리고, 「튜토리얼 넘기기」를 누르면 그것이 눌리는 동시에
    /// 왕의 대사가 한 줄 넘어갔다. 화면 UI 와 세상이 <b>같은 누름 하나를 나눠 갖고</b>
    /// 있었기 때문이다.
    ///
    /// 파 보니 이 프로젝트에서 <c>IsPointerOverGameObject</c> 를 부르는 데가 <b>한 곳도
    /// 없었다</b>. 마우스를 직접 읽는 자리가 아홉이었고, 그 아홉이 저마다 「눌렸으면 내
    /// 일을 한다」고만 적혀 있었다. 그러니 UI 를 눌러도 아홉이 다 같이 반응했다.
    ///
    /// <b>막는 결이 둘이다.</b> 하나로 뭉뚱그리면 반드시 어긋난다 —
    ///
    ///   · <see cref="OverPanel"/>  판 위에 있기만 해도 참. <b>세상을 짚는 일</b>을 막는다.
    ///     자막 바 위를 눌렀다고 그 너머 문이 열려서는 안 된다.
    ///   · <see cref="OverButton"/>  <b>누를 수 있는 것</b> 위에 있을 때만 참.
    ///     대사 넘기기·연출 건너뛰기처럼 「아무 데나 누르면 되는」 일을 막는다.
    ///
    /// 왜 둘이어야 하는가: 대사는 <b>자막 바 위를 눌러도 넘어가야 한다</b>. 바는 화면
    /// 아래를 넓게 덮으므로, 판 위를 전부 막으면 화면 아래 삼분의 일에서 대사가 안 넘어간다.
    /// 그러나 그 바에 얹힌 「✕ 닫기」를 눌렀을 때는 닫히기만 해야지 대사까지 넘어가면 안 된다.
    /// 앞엣것은 <b>판</b>이고 뒤엣것은 <b>단추</b>다 — 그 둘을 가르는 것이 이 클래스의 일이다.
    ///
    /// (짚은 <b>자리</b>를 남기는 <see cref="Pointing"/> 과는 다른 물음이다. 저쪽은
    ///  「세상 어디를 짚었나」, 이쪽은 「손이 무엇 위에 있나」다.)
    /// </summary>
    public static class UiGuard
    {
        /// <summary>레이캐스트 결과를 받아 두는 그릇. 매 번 새로 만들지 않는다.</summary>
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        /// <summary>지금 손이 있는 화면 자리.</summary>
        public static Vector2 Screen
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                return m != null ? m.position.ReadValue() : Vector2.zero;
#else
                return Vector2.zero;
#endif
            }
        }

        /// <summary>이번 칸에 눌렸는가(누른 순간 한 번).</summary>
        public static bool Pressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                return m != null && m.leftButton.wasPressedThisFrame;
#else
                return false;
#endif
            }
        }

        /// <summary>누르고 있는가.</summary>
        public static bool Held
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                return m != null && m.leftButton.isPressed;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// <b>화면 판 위에 손이 있는가.</b> 판이든 글이든, 레이캐스트를 받는 것이면 다.
        /// 세상을 짚는 쪽(<see cref="MouseRaySelector"/>)이 이것을 묻는다.
        /// </summary>
        public static bool OverPanel
        {
            get
            {
                var es = EventSystem.current;
                return es != null && es.IsPointerOverGameObject();
            }
        }

        /// <summary>
        /// <b>누를 수 있는 것 위에 손이 있는가.</b> 단추·글쇠칸처럼 제 일을 가진 것만 센다.
        ///
        /// <see cref="OverPanel"/> 로 갈음할 수 없다 — 자막 바는 화면 아래를 넓게 덮는
        /// <b>판</b>일 뿐이고, 그 위를 눌러 대사를 넘기는 것까지 막을 일은 아니다.
        /// </summary>
        public static bool OverButton
        {
            get
            {
                var es = EventSystem.current;
                if (es == null) return false;

                var data = new PointerEventData(es) { position = Screen };
                Hits.Clear();
                es.RaycastAll(data, Hits);
                for (int i = 0; i < Hits.Count; i++)
                {
                    var go = Hits[i].gameObject;
                    if (go == null) continue;
                    var sel = go.GetComponentInParent<Selectable>();
                    if (sel != null && sel.IsInteractable()) return true;
                }
                return false;
            }
        }

        /// <summary><b>세상을 짚어도 되는 누름</b> — 눌렸고, 판 위가 아니다.</summary>
        public static bool WorldPressed { get { return Pressed && !OverPanel; } }

        /// <summary><b>아무 데나 눌러 넘기는 누름</b> — 눌렸고, 단추 위는 아니다.</summary>
        public static bool AnywherePressed { get { return Pressed && !OverButton; } }

        /// <summary><b>아무 데나 누르고 있는 손</b> — 누르고 있고, 단추 위는 아니다.</summary>
        public static bool AnywhereHeld { get { return Held && !OverButton; } }
    }
}
