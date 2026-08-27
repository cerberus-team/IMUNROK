// 서천의 데스크톱 입력을 한곳에 모은 추상화 계층이며, XR 전환 시 이 파일의 구현만 교체합니다.
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Seocheon
{
    public static class SeocheonInput
    {
        public static bool InteractPressedThisFrame
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.eKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.E);
#endif
            }
        }

        // ── 지목 UI용 포인터 조회 ──
        // XR 전환 시 여기만 레이 기반 포인터로 바꾸면 호출부(WordPickUI)는 그대로입니다.

        /// <summary>화면 좌표계 포인터 위치. 포인터가 없으면 화면 중앙을 돌려줍니다.</summary>
        public static Vector2 PointerPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Mouse mouse = Mouse.current;
                if (mouse != null) return mouse.position.ReadValue();
#else
                return Input.mousePosition;
#endif
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
        }

        /// <summary>이번 프레임에 지목(좌클릭)이 눌렸는지.</summary>
        public static bool PointerPressedThisFrame
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Mouse mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        /// <summary>이번 프레임에 수첩(J)이 눌렸는지. 공통 JournalView 와 같은 키를 씁니다.</summary>
        public static bool JournalPressedThisFrame
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.jKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.J);
#endif
            }
        }

        /// <summary>이번 프레임에 취소(Esc)가 눌렸는지. UI 가 멈췄을 때의 탈출구입니다.</summary>
        public static bool CancelPressedThisFrame
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Escape);
#endif
            }
        }

        public static bool TryGetDebugPhaseIndex(out int index)
        {
            index = -1;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (keyboard.digit1Key.wasPressedThisFrame) index = 0;
            else if (keyboard.digit2Key.wasPressedThisFrame) index = 1;
            else if (keyboard.digit3Key.wasPressedThisFrame) index = 2;
            else if (keyboard.digit4Key.wasPressedThisFrame) index = 3;
            else if (keyboard.digit5Key.wasPressedThisFrame) index = 4;
            else if (keyboard.digit6Key.wasPressedThisFrame) index = 5;
            else if (keyboard.digit7Key.wasPressedThisFrame) index = 6;
            else if (keyboard.digit8Key.wasPressedThisFrame) index = 7;
            else if (keyboard.digit9Key.wasPressedThisFrame) index = 8;
#else
            if (Input.GetKeyDown(KeyCode.Alpha1)) index = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) index = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) index = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) index = 3;
            else if (Input.GetKeyDown(KeyCode.Alpha5)) index = 4;
            else if (Input.GetKeyDown(KeyCode.Alpha6)) index = 5;
            else if (Input.GetKeyDown(KeyCode.Alpha7)) index = 6;
            else if (Input.GetKeyDown(KeyCode.Alpha8)) index = 7;
            else if (Input.GetKeyDown(KeyCode.Alpha9)) index = 8;
#endif
            return index >= 0;
        }

        // TODO(XR): E키/숫자키/마우스 조회를 XR Input Action 조회로 교체합니다. 호출부는 변경하지 않습니다.
    }
}
