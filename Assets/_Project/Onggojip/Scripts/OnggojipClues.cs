namespace IMUNROK.Onggojip
{
    /// <summary>단서 하나의 정의. stealth=잠행(J)에서만 얻음, required=출도 필수 단서.</summary>
    public struct ClueDef
    {
        public string key;
        public string text;
        public bool stealth;   // true=1부 잠행(J), false=2부 공개(G)
        public bool required;  // 출도(出道)에 반드시 필요한 단서인가
    }

    /// <summary>
    /// 제1사건(옹고집전)의 단서 목록.
    ///
    /// <b>이 표는 씬이 실제로 주는 것과 한 벌이어야 한다.</b>
    /// 한동안 두 벌이었다 — 여기 적힌 J06 은 "마름의 취중 증언"인데 씬의 J06 은
    /// "필적이 다르다"였고, 여기 J13 은 "아궁이 재"인데 씬의 J13 은 "호구단자 위조"였다.
    /// 뜻이 어긋나니 출도 조건도 어긋나, 필수 넷 가운데 <b>J15 는 아무도 주지 않는</b>
    /// 번호였다. 넷 중 하나를 영영 못 채워 2부가 열리지 않았다.
    /// 이제 <b>씬 쪽에 맞췄다</b>. 표를 고칠 때는 씬에서 실제로 주는 번호를 확인할 것.
    ///
    /// 어디서 나오는지도 함께 적어 둔다 — 번호만 있으면 다음에 또 어긋난다.
    /// </summary>
    public static class OnggojipClues
    {
        public static readonly ClueDef[] All =
        {
            // ── 1부 잠행 (J) — 밤에 옹씨댁에서만 얻는다 ──

            // 사람의 입에서
            new ClueDef { key = "J01", text = "마을은 甲을 주인으로 알고, 담 밖의 사내를 죽은 종 '복동'이라 부른다", stealth = true },   // 마을사람 · 乙
            new ClueDef { key = "J02", text = "\"문서는 다 내 손을 거쳤소\" — 스무 해를 그리 했다", stealth = true, required = true },   // 甲
            new ClueDef { key = "J03", text = "甲이 최근 집안일을 지나치게 잘 안다", stealth = true },                                     // 甲
            new ClueDef { key = "J04", text = "마름의 취중 증언 — 둘이 닮았다", stealth = true },                                          // 마름
            new ClueDef { key = "J05", text = "하인들이 호칭에 머뭇거린다", stealth = true },                                              // 늙은하인
            new ClueDef { key = "J14", text = "甲이 형제를 딱 잘라 부인했다 — 乙이 살아 있는데도", stealth = true },                       // 甲

            // 손의 자취
            new ClueDef { key = "J06", text = "장부 필적이 한 달 전후로 바뀌고, 맞은편 백지에 그 두 줄을 미리 쓴 자국", stealth = true, required = true },  // _책A · 눌린자국
            new ClueDef { key = "J07", text = "물목 장부 — 스무 해엔 수결(押)이 없고 최근 한 달치에만 있다", stealth = true },             // 물목장부

            // 셈속
            new ClueDef { key = "J08", text = "차용증 이자가 갑리(甲利) — 연 10할", stealth = true },                                      // 차용증
            new ClueDef { key = "J09", text = "미회수 수표 — 무장현 박가에게 일백 냥, 삼 년째", stealth = true },                          // 수표_J09

            // <b>자물쇠</b> — 씬에 부서진 자물쇠를 세워 두고도 단서로 잡지 않고 있었다.
            // 이것 하나로 "누가 열었나"가 갈린다. 열쇠를 쥔 사람은 자물쇠를 부수지 않는다.
            new ClueDef { key = "J15", text = "문갑 자물쇠가 비틀려 부서졌다 — 열쇠를 가진 사람이 연 것이 아니다", stealth = true },  // 부서진자물쇠

            // 태우고 감춘 것
            new ClueDef { key = "J10", text = "아궁이 재 — 한여름인데 불을 땐 자리, 타다 만 서찰 조각", stealth = true },                  // 아궁이
            new ClueDef { key = "J11", text = "보료 밑 별급문기 — 수취인이 \"오래 부린 종 복동\", 아들이라는 말이 없다", stealth = true, required = true },  // 보료_들추기 · 별급문기

            // 관을 속인 자취
            new ClueDef { key = "J12", text = "호적대장(삼 년 전) — 노 복동이 살아 있다", stealth = true },                                // 호적대장
            new ClueDef { key = "J13", text = "호구단자(올해) — \"노 복동 신미년 사망\", 관리의 필적이 아니다", stealth = true, required = true },  // 호구단자

            // ── 2부 공개 (G) — 출도한 뒤 관아에서만 ──
            // G01·G02 는 관아가 보관한 원본이다. 집에서 나온 J12·J13 을 여기 원본과
            // 대조해야 위조가 확정된다.
            new ClueDef { key = "G01", text = "관아 호적대장 원본 — \"노 복동, 왼팔 안쪽 데인 자국 두 치 남짓\"", stealth = false },
            new ClueDef { key = "G02", text = "관아가 받은 호구단자 — 집에서 나온 것과 같은 필적", stealth = false },
            new ClueDef { key = "G03", text = "입안 대장 — 별급문기 사본, 수취인 \"종 복동\"", stealth = false },
            new ClueDef { key = "G04", text = "환곡·소작 대장 — 한 달간 소작료 인하", stealth = false },
            new ClueDef { key = "G05", text = "아내 증언 — \"서방님께 아우가 하나 있긴 했습니다\"", stealth = false },
            new ClueDef { key = "G06", text = "마름 — 속량 관련해 무너짐", stealth = false },

            // <b>유척이 짚는 마지막 한 치.</b> 사람의 말은 흔들려도 치수는 안 흔들린다 —
            // 대장에 적힌 "두 치 남짓"과 지금 저 팔의 흉터를 대어 보면 그것으로 끝난다.
            new ClueDef { key = "G07", text = "유척으로 잰 왼팔 안쪽 흉터 — 두 치 남짓, 대장과 같다", stealth = false },
        };

        /// <summary>
        /// 출도(1부→2부)에 반드시 필요한 단서 key.
        ///
        /// 넷을 <b>갈래가 겹치지 않게</b> 골랐다 — 하나라도 빠지면 무고가 된다.
        ///   · J02 <b>말</b>   — 甲의 입으로 스무 해를 문서에 붙어 살았다 했다
        ///   · J06 <b>필적</b> — 그 손이 한 달 전에 바뀌었다
        ///   · J11 <b>문서</b> — 재산을 받은 이름이 "종 복동"이다
        ///   · J13 <b>위조</b> — 그 복동이 죽었다는 기록을 관리가 쓰지 않았다
        /// </summary>
        public static readonly string[] RequiredForReveal = { "J02", "J06", "J11", "J13" };
    }
}
