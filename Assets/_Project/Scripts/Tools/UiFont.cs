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

        /// <summary>
        /// 쓸 글씨를 정한다: 인스펙터에 꽂힌 것 → 먼저 올라온 것 →
        /// <b>Resources 에 적어 둔 것</b> → 유니티 기본 글씨.
        ///
        /// 셋째 칸이 뒤늦게 생긴 까닭: 선착순만으로는 <b>표제가 없는 씬</b>이
        /// 못 막힌다. 사건 현장에서 먼저 뜨는 판은 아무도 올리기 전에 물어보게
        /// 되고, 그러면 유니티 기본 글씨로 떨어지는데 거기엔 한글도 한자도 없어
        /// 운영체제가 글자마다 다른 얼굴을 주워 온다 — 한 줄 안에 필체가 섞인다.
        /// "한글 중간에 한자만 딴 글씨체" 는 폰트가 없어서가 아니라
        /// <b>아직 안 올라와서</b> 생긴다.
        /// </summary>
        public static Font Resolve(Font preferred = null)
        {
            if (preferred != null) return preferred;
            if (Korean != null) return Korean;

            var book = Resources.Load<UiFontBook>(UiFontBook.ResourceName);
            if (book != null && book.korean != null)
            {
                Korean = book.korean;
                return Korean;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
