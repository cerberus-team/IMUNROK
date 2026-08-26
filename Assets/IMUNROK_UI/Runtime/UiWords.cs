namespace IMUNROK.Ui
{
    /// <summary>
    /// 안내 문구에 들어가는 <b>조작 이름</b> (2026-08-26).
    ///
    /// "좌클릭"·"휠"·"Esc" 는 PC 말이다. VR에서 그대로 두면 <b>있지도 않은 것을 누르라고</b>
    /// 적혀 있게 된다. 문장을 두 벌로 쓰는 대신 <b>이름만</b> 갈아 끼운다 —
    /// 문구를 고칠 때 한 곳만 고치면 되고, 두 모드가 갈라지지 않는다.
    ///
    /// 버튼 배치는 <see cref="XrUiPointer"/> 가 읽는 것과 짝을 맞춰 둔 것이다.
    /// ⚠️ 실제 Quest 컨트롤러에서 이 배치가 맞는지는 확인하지 못했다.
    /// </summary>
    public static class UiWords
    {
        /// <summary>누르기 — 좌클릭 / 트리거</summary>
        public static string Press => UiModes.IsVr ? "트리거" : "좌클릭";
        /// <summary>물러나기 — Esc·우클릭 / B·Y</summary>
        public static string Back => UiModes.IsVr ? "B·Y" : "Esc / 우클릭";
        /// <summary>물러나기(짧게) — Esc / B·Y</summary>
        public static string BackShort => UiModes.IsVr ? "B·Y" : "Esc";
        /// <summary>굴리기·넘기기 — 휠 / 스틱</summary>
        public static string Wheel => UiModes.IsVr ? "스틱" : "휠";
        /// <summary>소지품 여닫기 — I·Esc / 메뉴 버튼</summary>
        public static string Menu => UiModes.IsVr ? "메뉴 버튼" : "I / Esc";
        /// <summary>가리키는 동작 — 바라보고 / 겨누고</summary>
        public static string Aim => UiModes.IsVr ? "겨누고" : "바라보고";
        /// <summary>말하기 — 왼쪽 Ctrl / 그립</summary>
        public static string Talk => UiModes.IsVr ? "그립" : "왼쪽 Ctrl";
    }
}
