using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 한국어 줄바꿈 (2026-09-11). 한 줄짜리 글을 <b>어절 단위로</b> 끊고, 끊은 줄들의 길이를
    /// 고르게 맞춘다.
    ///
    /// ■ 왜 TMP 에 맡기지 않는가
    ///   TMP 는 폭이 차면 끊는다. 그래서 마지막 줄에 <b>어절 하나만</b> 남는 일이 흔하다 —
    ///   "…있을지도" / "모른다." 처럼. 게다가 한글은 낱자 단위로도 끊으려 들어
    ///   (<see cref="UiSkin"/> 이 현대 한글 규칙을 켜서 막아 두긴 했다) 조사·어미만 다음 줄로
    ///   넘어가기도 한다. 자막은 <b>줄 길이가 고를 때</b> 가장 잘 읽힌다.
    ///
    /// ■ 어떻게 고르게 만드는가
    ///   ① 최대폭으로 욕심껏 채워 <b>필요한 줄 수 n</b> 을 구한다.
    ///   ② 줄 수를 n 으로 유지하는 <b>가장 좁은 폭</b>을 이분 탐색으로 찾는다.
    ///   ③ 그 폭으로 다시 채운다 — 줄 수는 그대로인데 마지막 줄만 짧던 것이 고루 퍼진다.
    ///   어절 하나가 최대폭보다 길면 그 줄은 그냥 넘긴다(자르지 않는다).
    ///
    /// ■ 재는 일은 TMP 가 한다
    ///   <c>GetPreferredValues</c> 로 실제 글꼴·크기의 폭을 잰다. 글자 수로 세면 한글·숫자·
    ///   라틴 문자가 섞였을 때 어긋난다.
    /// </summary>
    public static class HangulWrap
    {
        const float Unbounded = 100000f;

        /// <summary>
        /// <paramref name="text"/> 를 <paramref name="maxWidth"/> 안에서 어절 단위로 끊는다.
        /// 글쓴이가 넣은 <c>\n</c> 은 문단 경계로 그대로 지킨다.
        /// </summary>
        /// <param name="probe">폭을 재는 데 쓸 글 — 실제로 그릴 그것을 넘길 것.</param>
        public static string Wrap(TMP_Text probe, string text, float maxWidth)
        {
            if (probe == null || string.IsNullOrEmpty(text) || maxWidth <= 1f) return text;

            var paragraphs = text.Split('\n');
            var outLines = new List<string>();
            foreach (var para in paragraphs)
            {
                if (para.Length == 0) { outLines.Add(""); continue; }
                WrapParagraph(probe, para, maxWidth, outLines);
            }
            return string.Join("\n", outLines.ToArray());
        }

        static void WrapParagraph(TMP_Text probe, string para, float maxWidth, List<string> outLines)
        {
            var words = para.Split(' ');
            // 빈 토막(공백 둘)은 버린다 — 줄 첫머리에 공백이 남으면 가운데 맞춤이 어긋난다
            var use = new List<string>();
            foreach (var w in words) if (w.Length > 0) use.Add(w);
            if (use.Count == 0) { outLines.Add(para); return; }

            int n = LineCount(probe, use, maxWidth);
            if (n <= 1) { outLines.Add(string.Join(" ", use.ToArray())); return; }

            // 줄 수를 n 으로 유지하는 가장 좁은 폭 — 그만큼 줄들이 고르게 나뉜다
            float lo = Width(probe, LongestWord(probe, use)), hi = maxWidth;
            for (int i = 0; i < 14 && hi - lo > 1f; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (LineCount(probe, use, mid) <= n) hi = mid; else lo = mid;
            }
            Fill(probe, use, hi, outLines);
        }

        /// <summary>그 폭으로 욕심껏 채웠을 때 몇 줄이 되는가.</summary>
        static int LineCount(TMP_Text probe, List<string> words, float width)
        {
            int lines = 1;
            string cur = words[0];
            for (int i = 1; i < words.Count; i++)
            {
                string next = cur + " " + words[i];
                if (Width(probe, next) <= width) cur = next;
                else { lines++; cur = words[i]; }
            }
            return lines;
        }

        static void Fill(TMP_Text probe, List<string> words, float width, List<string> outLines)
        {
            string cur = words[0];
            for (int i = 1; i < words.Count; i++)
            {
                string next = cur + " " + words[i];
                if (Width(probe, next) <= width) cur = next;
                else { outLines.Add(cur); cur = words[i]; }
            }
            outLines.Add(cur);
        }

        static string LongestWord(TMP_Text probe, List<string> words)
        {
            string best = words[0];
            float bw = Width(probe, best);
            for (int i = 1; i < words.Count; i++)
            {
                float w = Width(probe, words[i]);
                if (w > bw) { bw = w; best = words[i]; }
            }
            return best;
        }

        static float Width(TMP_Text probe, string s)
        {
            return probe.GetPreferredValues(s, Unbounded, 0f).x;
        }
    }
}
