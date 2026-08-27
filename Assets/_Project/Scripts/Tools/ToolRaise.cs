using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>도구를 들어 올린다</b> — 손짓 하나.
    ///
    /// 여태 두 도구가 서로 다른 문법을 썼다.
    ///   · 돋보기 — 손에 들고 <b>또 한 번 올려서</b> 종이를 <b>겨눈다</b>
    ///   · 등불   — 손에 들면 <b>그것으로 끝</b>. 시간이 저 혼자 찬다.
    /// 그래서 등불은 <b>쓰는</b> 물건이 아니라 <b>걸치는</b> 물건이었다. 아무 짓도
    /// 하지 않았는데 종이가 저 혼자 밝아지니, 무엇 때문에 밝아졌는지 알 수가 없다.
    ///
    /// 둘은 사실 <b>같은 손짓</b>이다. 다른 것은 도구가 가는 <b>쪽</b>이다:
    ///
    ///     돋보기   눈 ── [유리] ── 종이      끼운다 — 사이에 든다
    ///     등불     눈 ── 종이 ── [불]        비춘다 — 뒤로 간다
    ///
    /// 이것은 꾸며 낸 규칙이 아니라 물건의 이치다. 확대는 눈과 물건 사이에 유리를
    /// 끼워야 되고, 배접 속을 읽는 것은 종이 <b>뒤에서</b> 빛을 넣어야 된다.
    /// 그래서 손짓 하나만 익히면 둘 다 쓸 줄 알게 되고, 어느 쪽으로 가는지는
    /// 눈으로 보인다 — 유리는 앞으로 오고 등불은 종이 너머로 넘어간다.
    ///
    /// 여기서는 <b>지금 들어 올리고 있는가</b>만 답한다. 무엇이 어떻게 움직일지는
    /// 도구마다 제가 정한다.
    ///
    /// <b>오른쪽 단추는 종이를 쥐고 있을 때만</b> 이 손짓이 된다. 평소에는 시점
    /// 회전이 물고 있어서, 둘이 겹치면 도구를 올리려다 화면이 같이 돌아간다.
    /// 종이를 쥔 동안에는 어차피 걷지도 방을 짚지도 않으므로 겹칠 일이 없다.
    /// </summary>
    public static class ToolRaise
    {
        /// <summary>종이를 쥐고 있지 않아도 이 키로는 언제나 들어 올린다.</summary>
        public const Key Key_ = Key.F;

        /// <summary>지금 도구를 들어 올리고 있나.</summary>
        public static bool Held
        {
            get
            {
                if (Vr()) return true;
                if (Typing.Now) return false;   // 치는 동안에는 도구가 안 올라온다
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse != null && mouse.rightButton.isPressed && DocumentView.IsOpen) return true;

                var kb = Keyboard.current;
                if (kb != null)
                {
                    var k = kb[Key_];
                    if (k != null && k.isPressed) return true;
                }
#endif
                return false;
            }
        }

        /// <summary>
        /// <b>헤드셋에서는 이 손짓이 아예 없었다.</b> 돋보기를 손에 들 수는 있는데
        /// 눈에 댈 길이 없어, 들어 봐야 아무 일도 안 나는 물건이었다 — F 키와
        /// 오른쪽 마우스 단추가 유일한 길이었고 헤드셋을 쓰면 그 둘을 누를 손이 없다.
        /// "돋보기를 어떻게 쓰는지 모르겠다"는 말이 이것이었다.
        ///
        /// <b>오른손 스틱을 누른다</b> — 왼손 스틱 누름이 일어서기이므로 짝이 맞고,
        /// 아무도 안 쓰던 자리라 다투지 않는다.
        ///
        /// <b>종이를 쥐고 있을 때는 방아쇠로도 된다</b>. 책상에서 오른쪽 마우스 단추에
        /// 걸어 둔 규칙과 같은 자리다 — 종이를 편 동안에는 방을 짚을 일이 없으니
        /// 방아쇠가 놀고 있다. 다만 <b>단추를 겨누고 있을 때는 아니다</b>. 그 방아쇠는
        /// 단추를 누르러 당긴 것이고, 그 김에 유리까지 올라오면 화면이 통째로 바뀐다.
        /// </summary>
        private static bool Vr()
        {
            var hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!hand.isValid) return false;

            bool v;
            if (hand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out v) && v) return true;
            if (DocumentView.IsOpen && !VRUiRay.AnyOnUi
                && hand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out v) && v) return true;
            return false;
        }
    }
}
