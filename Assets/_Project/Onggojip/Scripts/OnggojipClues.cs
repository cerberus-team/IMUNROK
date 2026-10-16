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
            new ClueDef { key = "J02", text = "\"장부는 스무 해를 내 손으로 적었소\" — 남의 손에 맡긴 적이 없다 했다", stealth = true, required = true },   // 甲
            new ClueDef { key = "J03", text = "甲이 최근 집안일을 지나치게 잘 안다", stealth = true },                                     // 甲
            new ClueDef { key = "J04", text = "마름의 취중 증언 — 둘이 닮았다", stealth = true },                                          // 마름
            new ClueDef { key = "J05", text = "하인들이 호칭에 머뭇거린다", stealth = true },                                              // 늙은하인
            new ClueDef { key = "J14", text = "甲이 형제를 딱 잘라 부인했다 — 乙이 살아 있는데도", stealth = true },                       // 甲

            // 손의 자취
            new ClueDef { key = "J06", text = "장부 필적이 한 달 전후로 바뀌고, 맞은편 백지에 그 두 줄을 미리 쓴 자국", stealth = true, required = true },  // _책A · 눌린자국
            // <b>아래 J07·J08 둘은 문갑 안에 있다</b>(위의 J06 은 방에 바로 있다). 문갑은 부서진 자물쇠(J15)를 알아본 사람에게만
            // 열린다(UnlockWhenClued). J 는 잠행 중에만 얻히므로 J15 를 놓치면 이 둘은 영영 못 얻는다.
            // 2026-09-15 — 장계 갑리 줄이 J08 을 무겁게 치므로, 놓친 사람을 위해 2막 문서고에 <b>소지(G14)</b>를 두었고
            // 乙 에게 들이밀면 갑리를 인정한다(G16). 게장(G10)은 이제 J07 없이 동헌의 게장 한 종지로 연다.
            new ClueDef { key = "J07", text = "물목 장부 — 스무 해엔 수결(押)이 없고 최근 한 달치에만 있다", stealth = true },             // 물목장부(문갑)

            // 셈속
            new ClueDef { key = "J08", text = "차용증 이자가 갑리(甲利) — 연 10할", stealth = true },   // 차용증(문갑 — J15 뒤)                                      // 차용증
            new ClueDef { key = "J09", text = "미회수 수표 — 무장현 박가에게 일백 냥, 삼 년째", stealth = true },                          // 수표_J09

            // <b>자물쇠</b> — 씬에 부서진 자물쇠를 세워 두고도 단서로 잡지 않고 있었다.
            // 이것 하나로 "누가 열었나"가 갈린다. 열쇠를 쥔 사람은 자물쇠를 부수지 않는다.
            new ClueDef { key = "J15", text = "문갑 자물쇠가 비틀려 부서졌다 — 열쇠를 가진 사람이 연 것이 아니다", stealth = true },  // 부서진자물쇠

            // 태우고 감춘 것
            new ClueDef { key = "J10", text = "아궁이 재 — 한여름인데 불을 땐 자리, 타다 만 서찰 조각", stealth = true },                  // 아궁이
            new ClueDef { key = "J11", text = "보료 밑 별급문기 — 수취인이 \"오래 부린 종 복동\", 아들이라는 말이 없다", stealth = true, required = true },  // 보료_들추기 · 별급문기

            // 관을 속인 자취
            new ClueDef { key = "J12", text = "호적대장(삼 년 전) — 노 복동이 살아 있다", stealth = true },                                // 호적대장
            new ClueDef { key = "J13", text = "호구단자(올해) — \"노 복동 임오년 유월 사망\", 관리의 필적이 아니다", stealth = true, required = true },  // 호구단자

            // ── 2부 공개 (G) — 출도한 뒤 관아에서만 ──
            // G01·G02 는 관아가 보관한 원본이다. 집에서 나온 J12·J13 을 여기 원본과
            // 대조해야 위조가 확정된다.
            new ClueDef { key = "G01", text = "관아 호적대장 원본 — \"노 복동, 왼팔 안쪽 흉터 두 치 남짓\"", stealth = false },
            new ClueDef { key = "G02", text = "관아가 받은 호구단자 — 집에서 나온 것과 같은 필적", stealth = false },
            new ClueDef { key = "G03", text = "입안 대장 — 별급문기 사본, 수취인 \"종 복동\"", stealth = false },
            new ClueDef { key = "G04", text = "환곡·소작 대장 — 한 달간 소작료 인하", stealth = false },
            new ClueDef { key = "G05", text = "아내 증언 — \"서방님께 아우가 하나 있긴 했습니다\"", stealth = false },
            // <b>주인을 바로잡았다.</b> 표에는 마름이라 적혀 있었으나 속량에 매수되어 입을
            // 다문 사람은 늙은 하인이고, 마름의 2막은 「새로 알려 줄 것이 없다」로 적혀 있다.
            // 그러면서 이 번호는 <b>아무도 주지 않는</b> 고아였다 — 이제 하인이 준다.
            new ClueDef { key = "G06", text = "늙은 하인 — 속량을 약속받고 거짓을 말했다고 실토", stealth = false },

            // <b>유척이 짚는 마지막 한 치.</b> 사람의 말은 흔들려도 치수는 안 흔들린다 —
            // 대장에 적힌 "두 치 남짓"과 지금 저 팔의 흉터를 대어 보면 그것으로 끝난다.
            new ClueDef { key = "G07", text = "유척으로 잰 왼팔 안쪽 흉터 — 두 치 남짓, 대장과 같다", stealth = false },

            // <b>같은 자로 저쪽 팔도 잰다.</b> 甲의 팔에 자국이 있다는 것만으로는 절반이다 —
            // 乙의 팔이 말끔해야 대장의 두 치가 저 사람 것이 아니라는 말이 선다.
            // 씬은 여태 이것을 주고 있었는데 표에만 없었다.
            new ClueDef { key = "G08", text = "乙의 왼팔 안쪽에는 흉터가 없다 — 대장과 어긋난다", stealth = false },

            // <b>게.</b> 몸에 밴 것은 스무 해를 외워도 못 따라 한다. 진짜 옹덕구는
            // 여섯 살에 체한 뒤로 비린 것을 입에 못 대는데, 안채의 아내가 그것을 안다.
            new ClueDef { key = "G09", text = "아내 — \"비린 것은 입에도 못 대시던 분인데, 요즘은 게장을 청하십니다\"", stealth = false },
            new ClueDef { key = "G10", text = "甲은 게장을 사들이고도 못 먹는 척한다 — 물목기와 아내의 말이 어긋난다", stealth = false },

            // <b>등불을 댄 사람만 갖는다.</b> 입안대장 다섯째 줄을 긁어내고 덧쓴 자리 —
            // 지운 밑글이 「복동 면천, 기묘년 시월」이다. 한동안 이것이 G05 를 쓰고 있어
            // 아내의 증언과 한 칸을 다투었다(먼저 들어온 쪽만 남았다). 갈라 두었다.
            new ClueDef { key = "G11", text = "입안대장이 고쳐졌다 — 지운 밑글은 「복동 면천, 기묘년 시월」", stealth = false },

            // <b>거울로 꺾은 볕이 짚는 것.</b> 문서궤 뚜껑에 남은 눌린 자국 —
            // 궤 안의 입안대장에서는 긁어 지운 그 줄이, 뚜껑에는 <b>지우기 전</b> 모습으로
            // 배어 있다. G11 이 「고쳐졌다」면 이것은 「고치기 전에 무엇이었나」다.
            // 한동안 이 자리가 J_ 로 시작하는 열쇠를 쓰고 있었다 — 2막에서 얻는 것에
            // 잠행(J) 번호를 붙여 두면 어느 막의 것인지 표가 거짓말을 한다.
            //
            // 2026-09-14 — 볕과 뚜껑 자국을 걷고 <b>등불과 동경</b>으로 다시 짰다. 뚜껑에 「누구의 손」을 적어 둔
            // 전날 판은 이야기와 어긋났다(甲=복동이 제 면천을 지울 까닭이 없다). 이제 G12 는 입안대장에서
            // <b>뜯겨 나간 원래 장</b>이다 — 등경 자리와 동경의 각을 맞춰 불빛을 서가 한 칸에 앉히면 떨어진다.
            // 장계 셋째 줄(복동은 면천된 몸)의 가장 무거운 증좌.
            new ClueDef { key = "G12", text = "면천 입안 — 입안대장에서 뜯겨 서가 뒤에 숨은 장, 「기묘년 시월 복동 면천」", stealth = false },

            // ── 2026-09-15 더한 것 ──
            new ClueDef { key = "G13", text = "甲의 자백 — 「내가 복동이오」", stealth = false },                              // 甲: 열쇠 J13·G03 을 쥐고 정곡(복동·본명…)
            new ClueDef { key = "G14", text = "소지 — 옹당촌 백성들이 옹덕구의 갑리를 관에 호소했다", stealth = false },       // 문서고 환상 궤
            new ClueDef { key = "G15", text = "乙은 게장을 권하자 고개를 돌렸다 — 비린 것을 못 먹는다", stealth = false },     // 동헌 게장 종지
            new ClueDef { key = "G16", text = "乙이 갑리를 인정했다", stealth = false },                                        // 乙 에게 J08·G14
            new ClueDef { key = "G17", text = "乙이 복동이 이복 아우임을 인정했다", stealth = false },                           // 乙 에게 J11·G05
        };

        /// <summary>
        /// 출도(1부→2부)에 반드시 필요한 단서 key.
        ///
        /// 넷을 <b>갈래가 겹치지 않게</b> 골랐다 — 하나라도 빠지면 무고가 된다.
        ///   · J02 <b>말</b>   — 甲의 입으로 스무 해 장부를 제 손으로 적었다 했다
        ///   · J06 <b>필적</b> — 그 손이 한 달 전에 바뀌었다
        ///   · J11 <b>문서</b> — 재산을 받은 이름이 "종 복동"이다
        ///   · J13 <b>위조</b> — 그 복동이 죽었다는 기록을 관리가 쓰지 않았다
        /// </summary>
        public static readonly string[] RequiredForReveal = { "J02", "J06", "J11", "J13" };
    }
}
