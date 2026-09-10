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
        /// <param name="bold">
        /// 굵게도 할 것인가.
        ///
        /// <b>한지 위에서는 끄는 것이 낫다.</b> 어두운 자막판의 큰 글씨에서는 굵기가
        /// "한 켜 도드라짐" 이지만, 종이에 열세 눈금으로 적힌 작은 글씨에서는 획 사이가
        /// 메워져 <b>먹이 번진 것</b>처럼 뭉친다 — 사목에서 실제로 그렇게 됐다.
        /// 작은 글씨에서는 색만으로도 충분히 떨어진다.
        /// </param>
        public static string Rich(string text, Color? tint = null, bool bold = true)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(Mark) < 0) return text;

            string b0 = bold ? "<b>" : "";
            string b1 = bold ? "</b>" : "";
            string open = tint.HasValue
                        ? b0 + "<color=#" + ColorUtility.ToHtmlStringRGB(tint.Value) + ">"
                        : (bold ? b0 : "");
            string close = tint.HasValue ? "</color>" + b1 : b1;
            if (!bold && !tint.HasValue) return Plain(text);   // 줄 것이 없으면 표시만 걷는다

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

        /// <summary>
        /// <b>남이 지은 글을 화면에 올리기 전에 씻는다.</b>
        ///
        /// 인물의 대답은 <b>우리가 안 쓴 글</b>이다(LLM). 거기에는 우리가 모르는 표시가
        /// 섞여 온다 — <c>**굵게**</c> 같은 마크다운, <c>&lt;thinking&gt;</c>·<c>&lt;답&gt;</c> 처럼
        /// 제가 지어낸 꼬리표, 닫히지 않은 태그.
        ///
        /// 낡은 <c>UI.Text</c> 는 모르는 태그를 <b>글자로</b> 그렸고 TMP 도 그렇게 한다.
        /// 그래서 대사 한복판에 <c>&lt;/b&gt;</c> 같은 것이 그대로 찍혔다 — 사람이 그렇게 말할 리 없으니
        /// 그 순간 화면이 <b>사람의 말이 아니라 기계의 출력</b>이 된다.
        ///
        /// 그래서 <b>아는 태그만 남긴다</b>. 목록에 없는 것은 통째로 걷고, 마크다운의
        /// 별표·밑줄 겹침도 함께 없앤다(우리 별표 표시 <see cref="Mark"/> 하나는 남긴다 —
        /// 그것은 <see cref="Rich"/> 가 곧 먹는다).
        ///
        /// ⚠️ 대답을 <b>수첩에 적기 전에</b>도 이걸 거쳐야 한다. 화면만 씻으면
        ///    수첩 카드에 태그가 그대로 남는다.
        /// </summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // ① 마크다운 겹침을 먼저 없앤다. 우리 표시는 별표 <b>하나</b>이므로,
            //    두 개짜리를 그냥 두면 Rich 가 여는 짝·닫는 짝을 잘못 맞춘다.
            text = text.Replace("**", "").Replace("__", "");

            // ② 아는 태그만 남긴다.
            var sb = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '<') { sb.Append(text[i]); continue; }
                int close = text.IndexOf('>', i + 1);
                if (close < 0) { sb.Append(text[i]); continue; }        // 짝이 없으면 그냥 글자다
                string inside = text.Substring(i + 1, close - i - 1);
                if (Known(inside)) sb.Append(text, i, close - i + 1);   // 아는 것은 그대로
                i = close;                                              // 모르는 것은 통째로 걷는다
            }
            return sb.ToString();
        }

        /// <summary>우리가 그릴 줄 아는 태그인가. 이름만 본다(값은 안 따진다).</summary>
        private static bool Known(string inside)
        {
            if (string.IsNullOrEmpty(inside)) return false;
            if (inside[0] == '/') inside = inside.Substring(1);
            int cut = inside.IndexOfAny(new[] { '=', ' ' });
            if (cut > 0) inside = inside.Substring(0, cut);
            switch (inside.ToLowerInvariant())
            {
                case "b": case "i": case "u": case "s":
                case "color": case "size": case "br":
                case "sup": case "sub": case "mark": case "align":
                    return true;
            }
            return false;
        }
    }
}
