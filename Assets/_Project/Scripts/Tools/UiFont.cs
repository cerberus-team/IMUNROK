using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 한글 UI 폰트를 한 곳에 모아두는 공유 자리(HudSide와 같은 방식).
    ///
    /// OnGUI를 월드 Canvas로 옮기면 UI 조각이 여러 개로 늘어나는데, 그때마다 인스펙터에서
    /// 폰트를 일일이 연결하면 하나만 빠뜨려도 그 패널만 한글이 네모로 깨진다.
    /// 폰트를 가진 컴포넌트가 <see cref="Publish"/>로 올려두면, 나머지는 <see cref="Korean"/>을
    /// 그냥 가져다 쓴다. 먼저 올린 것이 이기고(선착순), 덮어쓰지 않는다.
    /// </summary>
    public static class UiFont
    {
        /// <summary>한글이 나오는 폰트. 아무도 올리지 않았으면 null.</summary>
        public static Font Korean { get; private set; }

        /// <summary>폰트를 공유 자리에 올린다(이미 있으면 무시). null은 무시한다.</summary>
        public static void Publish(Font font)
        {
            if (font != null && Korean == null) Korean = font;
        }

        /// <summary>공유 폰트 → 없으면 유니티 기본 폰트(한글은 깨진다).</summary>
        public static Font Resolve(Font preferred = null)
        {
            if (preferred != null) return preferred;
            if (Korean != null) return Korean;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
