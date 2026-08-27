using UnityEngine;
using UnityEngine.EventSystems;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>지금 글을 치고 있나.</b>
    ///
    /// 자막 바에 글쇠 칸이 생기면서 <b>바로 걸리는 탈</b>이 하나 있다 —
    /// 이 게임의 조작이 죄 <b>맨 글쇠 하나</b>에 매여 있다.
    /// I는 수첩, M은 지도, F는 도구 들기, Space는 일어서기, WASD는 걷기,
    /// Backspace는 자막 닫기다. 칸에 「이만」이라고 치면 그 사이에
    /// <b>수첩이 열리고 지도가 펴지고 몸이 걸어간다</b>.
    ///
    /// 한글은 더 나쁘다. 두벌식으로 「ㅇ」은 <c>d</c>, 「ㅏ」는 <c>k</c> 자리라
    /// 무슨 말을 치든 온갖 글쇠가 다 눌린다.
    ///
    /// 그래서 <b>묻는 자리를 하나 둔다.</b> 글쇠를 읽는 쪽은 그 전에 여기를 본다.
    /// 칸이 잡혀 있는 동안에는 게임이 글쇠를 안 듣는다.
    ///
    /// <b>왜 EventSystem 에 묻나</b>: 어느 칸이 잡혀 있는지는 UGUI가 안다.
    /// 우리가 따로 세어 두면 칸이 늘 때마다 여기도 고쳐야 하고, 한 곳을
    /// 빠뜨리면 <b>그 칸에서만</b> 조작이 새는 — 찾기 고약한 탈이 된다.
    ///
    /// TMP 칸도 같이 친다. 견우팀 꾸러미는 <c>TMP_InputField</c> 를 쓰는데,
    /// 우리는 아직 낡은 <c>UI.InputField</c> 다. 언젠가 갈아탈 때
    /// <b>이 파일이 조용히 거짓이 되는 것</b>을 막으려고 이름으로 본다.
    /// </summary>
    public static class Typing
    {
        /// <summary>손으로 켜 두는 자리. 글쇠 칸이 아닌 것으로 글을 받을 때 쓴다.</summary>
        public static bool Forced;

        public static bool Now
        {
            get
            {
                if (Forced) return true;
                var es = EventSystem.current;
                if (es == null) return false;
                var go = es.currentSelectedGameObject;
                if (go == null) return false;

                // <b>isFocused 로만 보면 안 된다.</b> 재 보니 칸이 잡힌 뒤에도 한동안
                // false 다 — 낡은 <c>InputField</c> 는 <c>LateUpdate</c> 에 가서야
                // 그 깃발을 세운다. 그 한두 칸 사이에 눌린 글쇠는 <b>게임이 먹는다</b>.
                // 잡혀 있고 칠 수 있으면 치는 중으로 친다.
                var legacy = go.GetComponent<UnityEngine.UI.InputField>();
                if (legacy != null) return legacy.isFocused || (legacy.interactable && !legacy.readOnly);

                // TMP 칸 — 꾸러미가 쓰는 것. 참조를 더하지 않으려고 이름으로 본다.
                var all = go.GetComponents<Component>();
                for (int i = 0; i < all.Length; i++)
                    if (all[i] != null && all[i].GetType().Name == "TMP_InputField") return true;

                return false;
            }
        }
    }
}
