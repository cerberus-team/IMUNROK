using System.Text;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>말로 정곡을 찔렀는가</b>를 가리는 자.
    ///
    /// 여태 단서는 <b>줄에 걸린 추천 질문</b>을 눌러야만 나왔다. 손으로 쳐 넣은 말은
    /// 아무리 정확해도 분위기였다 — 「저 사내가 누구요」라고 제 손으로 친 사람과
    /// 단추를 누른 사람이 같은 답을 듣고도 한쪽만 빈손으로 돌아섰다. 물어본 사람에게
    /// 물어본 값을 주지 않으면 손으로 칠 까닭이 없어지고, 그러면 이 게임은
    /// 단추 셋을 차례로 누르는 것이 된다.
    ///
    /// 그래서 <b>핵심 낱말</b>로 잰다. 「복동아」와 「네 이름 복동이지?」는 다른 문장이지만
    /// 같은 곳을 찌른다 — 조사가 붙든 말든 낱말이 들었으면 닿은 것으로 본다.
    /// 문장 전체를 맞추게 하면 그것은 암호를 대는 일이지 심문이 아니다.
    /// </summary>
    public static class TalkKeywords
    {
        /// <summary>
        /// 견주기 전에 <b>군더더기를 턴다</b> — 띄어쓰기·마침표·물음표.
        /// 「왼 팔?」과 「왼팔」이 다른 말이 되면 안 된다.
        /// </summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (char.IsWhiteSpace(ch)) continue;
                if (char.IsPunctuation(ch) || char.IsSymbol(ch)) continue;
                sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 친 말에 핵심 낱말이 들었는가.
        ///
        /// <paramref name="csv"/> 는 쉼표로 나눈 낱말 목록이고, <b>하나만 들어도</b> 닿은 것이다.
        /// 한 낱말 안에 <c>+</c> 를 쓰면 <b>둘 다</b> 들어야 한다 —
        /// 「왼팔+자국」은 왼팔만 물어서는 안 열리고 자국까지 짚어야 열린다.
        /// 목록이 비었으면 언제나 거짓이다(말로는 안 열리는 자리).
        /// </summary>
        public static bool Hit(string said, string csv)
        {
            if (string.IsNullOrWhiteSpace(said) || string.IsNullOrWhiteSpace(csv)) return false;
            string hay = Normalize(said);
            if (hay.Length == 0) return false;

            var terms = csv.Split(',');
            foreach (var raw in terms)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var parts = raw.Split('+');
                bool all = true;
                foreach (var p in parts)
                {
                    string needle = Normalize(p);
                    if (needle.Length == 0) { all = false; break; }
                    if (!hay.Contains(needle)) { all = false; break; }
                }
                if (all) return true;
            }
            return false;
        }
    }
}
