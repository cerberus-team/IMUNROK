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
    /// 제1사건(옹고집전)의 단서 목록과 규칙 데이터.
    /// - J01~J15: 1부 잠행에서만 얻을 수 있음(출도 후엔 영영 못 얻음).
    /// - G01~G06: 2부 공개조사에서만 접근.
    /// - 출도 필수: J04·J09·J13·J15 (하나라도 없으면 2부에서 막힘 → 판별 불가).
    /// 실제 단서 텍스트는 여기서 자유롭게 수정 가능.
    /// </summary>
    public static class OnggojipClues
    {
        public static readonly ClueDef[] All =
        {
            // ── 1부 잠행 (J) ──
            new ClueDef { key = "J01", text = "乙이 마을에서 미친 사람 취급을 받는다", stealth = true },
            new ClueDef { key = "J02", text = "마을 사람들은 甲을 진짜라 여긴다", stealth = true },
            new ClueDef { key = "J03", text = "甲의 손 — 붓 굳은살과 먹 자국", stealth = true },
            new ClueDef { key = "J04", text = "\"문서는 다 내 손을 거쳤소\" (스무 해)", stealth = true, required = true },
            new ClueDef { key = "J05", text = "甲이 최근 집안일을 완벽히 안다", stealth = true },
            new ClueDef { key = "J06", text = "마름의 취중 증언 — 둘이 닮았다", stealth = true },
            new ClueDef { key = "J07", text = "하인들이 호칭에 머뭇거린다", stealth = true },
            new ClueDef { key = "J08", text = "사랑방 문갑 — 자물쇠 부서지고 비어 있음", stealth = true },
            new ClueDef { key = "J09", text = "장부 필적이 한 달 전후로 바뀜", stealth = true, required = true },
            new ClueDef { key = "J10", text = "물목 장부 — 스무 해엔 게(押) 없고 최근 한 달엔 있음", stealth = true },
            new ClueDef { key = "J11", text = "차용증 이자가 갑리(연 10할)", stealth = true },
            new ClueDef { key = "J12", text = "미회수 증서 — 큰 금액, 다른 고을", stealth = true },
            new ClueDef { key = "J13", text = "아궁이 재 — 타다 만 서찰 조각", stealth = true, required = true },
            new ClueDef { key = "J14", text = "재 안쪽 — 종이 재 한 뭉치 더", stealth = true },
            new ClueDef { key = "J15", text = "행랑채 궤 — 속량 문서", stealth = true, required = true },

            // ── 2부 공개 (G) ──
            new ClueDef { key = "G01", text = "옛 호적대장 — \"노 복동, 왼팔 안쪽 데인 자국 두 치 남짓\"", stealth = false },
            new ClueDef { key = "G02", text = "이번 호구단자 — \"노 복동 신미년 사망\", 필체 다름", stealth = false },
            new ClueDef { key = "G03", text = "입안 대장 — 별급문기 사본, 수취인 \"종 복동\"", stealth = false },
            new ClueDef { key = "G04", text = "환곡·소작 대장 — 한 달간 소작료 인하", stealth = false },
            new ClueDef { key = "G05", text = "아내 증언 — \"서방님께 아우가 하나 있긴 했습니다\"", stealth = false },
            new ClueDef { key = "G06", text = "늙은 하인 — 속량 관련해 무너짐", stealth = false },
        };

        /// <summary>출도(1부→2부)에 반드시 필요한 단서 key.</summary>
        public static readonly string[] RequiredForReveal = { "J04", "J09", "J13", "J15" };
    }
}
