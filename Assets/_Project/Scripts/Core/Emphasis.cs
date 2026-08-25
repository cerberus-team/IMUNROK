using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>한 문장 안에서 한두 낱말만 도드라지게</b> 한다.
    ///
    /// 대사와 설명은 이미 짧게 줄여 두었다. 그런데 짧은 문장 안에서도 걸리는 것은 늘
    /// 한두 낱말이다 — "노 복동이 신미년에 죽었다고 적혀 있다" 에서 사람이 붙드는 것은
    /// '신미년'과 '죽었다'이지 문장 전체가 아니다. 여태 글은 처음부터 끝까지 한 굵기였고,
    /// 그래서 읽는 사람이 <b>무엇을 붙들어야 하는지</b>를 스스로 찾아야 했다.
    ///
    /// <b>쓰는 법</b>: 도드라지게 할 낱말을 별표로 감싼다.
    ///   "노 복동이 *신미년 사망* 이라 적혀 있다"
    /// 인스펙터에 그렇게 적어 두면 화면에 나올 때 그 낱말만 굵고 조금 밝게 나온다.
    /// 별표 자체는 화면에 안 보인다. 별표를 글자로 쓰고 싶으면 두 번 겹쳐 적는다.
    ///
    /// <b>왜 색이 아니라 굵기부터인가</b>: 색을 크게 바꾸면 그 낱말이 <b>단추</b>처럼
    /// 보인다. 조사에서 눈에 띄어야 할 것은 누를 것이 아니라 <b>읽을 것</b>이므로,
    /// 굵기를 먼저 주고 색은 아주 조금만 얹는다 — "약간 더" 가 여기서 지켜야 할 몫이다.
    ///
    /// 글이 놓이는 바탕이 둘이라 색도 둘이다. 어두운 자막판 위에서는 조금 <b>밝게</b>,
    /// 한지 위에서는 조금 <b>진하게</b> — 어느 쪽이든 바탕에서 한 켜만 떨어진다.
    /// </summary>
    public static class Emphasis
    {
        /// <summary>어두운 바탕(자막판·물건글) 위에서 도드라지는 색.</summary>
        public static readonly Color OnDark = new Color(1f, 0.93f, 0.74f);

        /// <summary>밝은 바탕(한지·수첩) 위에서 도드라지는 색.</summary>
        public static readonly Color OnPaper = new Color(0.35f, 0.13f, 0.10f);

        /// <summary>별표로 감싼 낱말을 굵고 조금 도드라진 글로 바꾼다. 표시가 없으면 그대로 온다.</summary>
        /// <param name="text">원래 글(별표 표시가 섞여 있을 수 있다)</param>
        /// <param name="tint">도드라질 색. 비우면 굵기만 준다</param>
        public static string Rich(string text, Color? tint = null)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(Mark) < 0) return text;

            string open = tint.HasValue
                        ? "<b><color=#" + ColorUtility.ToHtmlStringRGB(tint.Value) + ">"
                        : "<b>";
            string close = tint.HasValue ? "</color></b>" : "</b>";

            var sb = new System.Text.StringBuilder(text.Length + 32);
            bool inside = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != Mark) { sb.Append(c); continue; }

                // 표시가 둘 붙어 있으면 글자 그대로 하나를 찍는다
                if (i + 1 < text.Length && text[i + 1] == Mark) { sb.Append(Mark); i++; continue; }

                sb.Append(inside ? close : open);
                inside = !inside;
            }
            if (inside) sb.Append(close);   // 짝이 안 맞아도 태그는 닫고 나간다
            return sb.ToString();
        }

        /// <summary>표시만 걷어낸 맨 글. 수첩에 적거나 길이를 잴 때 쓴다.</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(Mark) < 0) return text;

            var sb = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != Mark) { sb.Append(c); continue; }
                if (i + 1 < text.Length && text[i + 1] == Mark) { sb.Append(Mark); i++; }
            }
            return sb.ToString();
        }

        /// <summary>도드라지게 할 낱말을 감싸는 표시.</summary>
        private const char Mark = '*';
    }
}
