namespace IMUNROK.Common
{
    /// <summary>
    /// <b>조사를 이름에 맞춰 붙인다.</b>
    ///
    /// 화면에 「성난 사내<b>이(가)</b> 불려 나온다」가 떴다. 이름을 코드가 끼워 넣는 자리마다
    /// 「이(가)」·「을(를)」을 괄호째 적어 두었기 때문인데, 괄호는 <b>서식이지 말이 아니다</b> —
    /// 사람 이름이 들어가는 자리에 그것이 남아 있으면 그 한 줄만 안내문 투가 된다.
    ///
    /// 가리는 법은 <b>받침</b> 하나다. 한글 음절은 0xAC00 에서 시작해 28 칸씩 끊어지고,
    /// 그 나머지가 곧 종성 번호다 — 0 이면 받침이 없다. 한자·숫자·로마자가 끝에 오면
    /// 소리로는 알 수 없으므로 <b>받침이 있는 것으로</b> 친다(「복동이」가 「복동가」보다 낫다).
    /// </summary>
    public static class Josa
    {
        /// <summary>이 말의 끝에 받침이 있나.</summary>
        public static bool HasFinal(string word)
        {
            if (string.IsNullOrEmpty(word)) return true;

            char c = ' ';
            for (int i = word.Length - 1; i >= 0; i--)
            {
                if (char.IsWhiteSpace(word[i])) continue;
                c = word[i];
                break;
            }

            if (c >= 0xAC00 && c <= 0xD7A3) return (c - 0xAC00) % 28 != 0;

            // 숫자는 읽는 소리로 가른다 — 1 은 「일」이라 받침이 있고 2 는 「이」라 없다.
            if (c >= '0' && c <= '9')
            {
                switch (c)
                {
                    case '2': case '4': case '5': case '9': return false;
                    default: return true;
                }
            }

            return true;   // 한자·로마자 — 소리를 모르니 받침 있는 쪽으로
        }

        /// <summary>이름 뒤에 <b>이/가</b>를 붙여 돌려준다.</summary>
        public static string IGa(string word) { return word + (HasFinal(word) ? "이" : "가"); }

        /// <summary>이름 뒤에 <b>을/를</b>.</summary>
        public static string EulReul(string word) { return word + (HasFinal(word) ? "을" : "를"); }

        /// <summary>이름 뒤에 <b>은/는</b>.</summary>
        public static string EunNeun(string word) { return word + (HasFinal(word) ? "은" : "는"); }

        /// <summary>이름 뒤에 <b>과/와</b>.</summary>
        public static string GwaWa(string word) { return word + (HasFinal(word) ? "과" : "와"); }

        /// <summary>이름 뒤에 <b>으로/로</b>. ㄹ 받침은 「로」다 — 「칼로」이지 「칼으로」가 아니다.</summary>
        public static string Euro(string word)
        {
            if (string.IsNullOrEmpty(word)) return word + "으로";
            char c = word[word.Length - 1];
            bool rieul = c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 == 8;
            return word + (!HasFinal(word) || rieul ? "로" : "으로");
        }
    }
}
