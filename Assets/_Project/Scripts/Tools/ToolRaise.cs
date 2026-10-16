using UnityEngine;
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
    /// <b>오른쪽 단추로는 올리지 않는다.</b> 평소엔 시점 회전이 물고 있고, 종이를 쥔
    /// 동안에만 내주던 것도 거뒀다 — 원치 않을 때 돋보기가 튀어나왔다.
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
                if (Typing.Now) return false;   // 치는 동안에는 도구가 안 올라온다
#if ENABLE_INPUT_SYSTEM
                // 오른쪽 단추는 <b>안 쓴다</b>. 종이를 쥔 동안만 내주었는데, 종이를 돌려 보다
                // 손이 스치면 돋보기가 불쑥 올라왔다. 들어 올리는 손짓은 F 하나다.
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

    }
}
