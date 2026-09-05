namespace IMUNROK.Common
{
    /// <summary>
    /// <b>이번 재생은 여기서부터</b>를 한 번만 적어 두는 쪽지.
    ///
    /// 옹고집 한 판은 대문 두드리기 → 마름 → 복동 → 사랑채 → 마당 순으로 이어진다.
    /// 아궁이 하나를 보려고 그 줄을 매번 다시 따라가는 것은 만드는 사람에게만
    /// 드는 값이다. 그래서 <b>중간에서 시작하는 문</b>을 하나 낸다.
    ///
    /// <b>한 번 쓰고 지운다.</b> 켜 둔 채로 남으면 그다음에 그냥 재생을 눌렀을 때도
    /// 사랑채에서 시작해 버린다 — 그것은 도움이 아니라 <b>조용한 함정</b>이다.
    /// <see cref="Take"/> 가 읽는 즉시 지우므로, 「바로 시작」 메뉴를 누른 그 한 판만
    /// 건너뛰고 다음 재생은 처음부터다.
    ///
    /// <see cref="ShotMode"/> 와 같은 자리(EditorPrefs)에 산다 —
    /// 빌드에는 코드째 안 들어가고, 적어 둔 채로 빌드해도 아무 일도 안 일어난다.
    /// </summary>
    public static class StartAt
    {
        private const string Key = "이문록.바로시작";

        /// <summary>사랑채 안에 내려놓는다.</summary>
        public const string 사랑채 = "사랑채";

        /// <summary>다음 재생을 어디서 시작하나. 빈 문자열이면 처음부터.</summary>
        public static string Where
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetString(Key, "");
#else
                return "";
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetString(Key, value ?? "");
#endif
            }
        }

        /// <summary>
        /// 읽으면서 <b>지운다</b>. 부르는 쪽이 지우는 것을 잊어도 함정이 안 남게
        /// 읽기와 지우기를 한 손에 묶어 둔다.
        /// </summary>
        public static string Take()
        {
            var w = Where;
            if (!string.IsNullOrEmpty(w)) Where = "";
            return w;
        }
    }
}
