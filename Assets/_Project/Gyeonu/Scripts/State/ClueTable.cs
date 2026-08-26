using System.Collections.Generic;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 단서 22종·모순 2종의 배점표. 최종기획안 11차 「10·11·12」를 그대로 옮긴 것이다.
    ///
    /// ■ 왜 표를 코드 밖이 아니라 여기 두는가
    ///   숫자가 흩어지면 검산(문서 「11. 검산」)을 다시 할 수 없다. 한 곳에 모아 두고
    ///   <see cref="SelfCheck"/> 로 합계를 확인한다. 기획이 바뀌면 이 파일 한 장만 고친다.
    ///
    /// ■ 여기 없는 것
    ///   AI 대화 등급 판정은 만들지 않는다(문서 지시). 등급이 정해진 뒤의
    ///   가감만 <see cref="GyeonuCase.ReportGyeonuTone"/> 에 있다.
    /// </summary>
    public static class ClueTable
    {
        /// <summary>단서 한 줄의 고정 정보.</summary>
        public class Info
        {
            public ClueId id;
            public string title;      // 단서 이름
            public string source;     // 어디서 얻는가
            public int evidence;      // 증거도 가산 (1회)
            public int trust;         // 견우에게 제시했을 때 신뢰도 증감 (1회)
            public bool redHerring;   // 레드헤링인가
            public string Code => id.ToString();
            public char Series => Code[0];
        }

        static readonly Dictionary<ClueId, Info> _map = new Dictionary<ClueId, Info>();
        static readonly List<Info> _all = new List<Info>();

        static ClueTable()
        {
            // ── A 계열 — 전설과 옛길 ─────────────────────────────
            Add(ClueId.A1, "타공 비밀지도",         "견우 신뢰도 70",     0,   0);
            Add(ClueId.A2, "밤하늘에 반응하는 지도", "혼상 퍼즐 후 사용",   0,   0);
            Add(ClueId.A3, "맑은 칠석에만",         "주모 또는 상인",      5,  +5);
            Add(ClueId.A4, "은하담이라는 이름",      "어머니",             0,  +5);
            Add(ClueId.A5, "끊긴 옛길",             "은하담 건너편",       0, +10);
            Add(ClueId.A6, "아이들의 노래",         "마을 자동",           0, -10, redHerring: true);
            Add(ClueId.A7, "주모의 확신",           "주모 첫 대화",        0, -10, redHerring: true);

            // ── B 계열 — 올해의 실종 ─────────────────────────────
            Add(ClueId.B1, "견우의 진짜 증언",       "신뢰도 70",         10,   0);
            Add(ClueId.B2, "아래층의 흔적",         "관측실 조사",         0,   0);
            Add(ClueId.B3, "밤마다 나간 선아",       "주모",               0, +25);
            Add(ClueId.B4, "다리 건너의 사내",       "상인",               0, +25);
            Add(ClueId.B5, "그 아이는 오지 않았다",  "견우마을",            5,   0);
            Add(ClueId.B6, "선아 집의 흔적",         "선아 집 조사",        0, +10);
            Add(ClueId.B7, "말하지 않는 견우",       "견우 첫 대화",        0, -15, redHerring: true);

            // ── C 계열 — 진범 ───────────────────────────────────
            Add(ClueId.C1, "아버지의 검수 기록",     "관측실",             20,   0);
            Add(ClueId.C2, "수령의 은닉 기록·기물",  "서고",               20,   0);
            Add(ClueId.C3, "선아의 풀이표",          "구출 후",            20,   0);
            Add(ClueId.C4, "아버지의 유품 메모",     "선아 집",             5,   0);
            Add(ClueId.C5, "장부의 어색함",          "집무실",             10,   0);
            Add(ClueId.C6, "유독 심했던 순찰",       "상인",                5,  +5);
            Add(ClueId.C7, "죄인이 아니었다",        "어머니",              5, +10);
            Add(ClueId.C8, "협조적인 수령",          "관아 첫 대화",        0,  -5, redHerring: true);
        }

        static void Add(ClueId id, string title, string source, int evidence, int trust, bool redHerring = false)
        {
            var info = new Info { id = id, title = title, source = source, evidence = evidence, trust = trust, redHerring = redHerring };
            _map[id] = info;
            _all.Add(info);
        }

        /// <summary>선언 순서(A1…C8) 그대로의 전체 목록 — UI가 이 순서로 그린다.</summary>
        public static IReadOnlyList<Info> All => _all;

        public static Info Get(ClueId id) => _map.TryGetValue(id, out var i) ? i : null;

        public static int Evidence(ClueId id) => _map.TryGetValue(id, out var i) ? i.evidence : 0;
        public static int Trust(ClueId id) => _map.TryGetValue(id, out var i) ? i.trust : 0;
        public static string Title(ClueId id) => _map.TryGetValue(id, out var i) ? i.title : id.ToString();

        /// <summary>"A3 맑은 칠석에만" 처럼 코드와 이름을 붙인 표시용 문구.</summary>
        public static string Label(ClueId id) => id + " " + Title(id);

        /// <summary>문자열 코드("C4")를 단서로 — 소지품 id·플래그 이름에서 되찾을 때 쓴다.</summary>
        public static bool TryParse(string code, out ClueId id)
        {
            id = default;
            if (string.IsNullOrEmpty(code)) return false;
            foreach (var i in _all)
                if (string.Equals(i.Code, code, System.StringComparison.OrdinalIgnoreCase)) { id = i.id; return true; }
            return false;
        }

        // ── 모순 ────────────────────────────────────────────────

        /// <summary>모순 하나를 알아냈을 때 증거도 가산. 둘 다 +10.</summary>
        public const int ContradictionEvidence = 10;

        public static string Title(ContradictionId id)
        {
            switch (id)
            {
                case ContradictionId.M1: return "수령의 알리바이";
                case ContradictionId.M2: return "죄인이 된 검수관";
                default: return id.ToString();
            }
        }

        public static string Label(ContradictionId id) => id + " " + Title(id);

        // ── 입증 조건 (문서 「11. 입증 조건」) ────────────────────

        /// <summary>증거도 하한.</summary>
        public const int ProofEvidenceMin = 80;

        /// <summary>없으면 아무리 모아도 입증되지 않는 핵심 3종.</summary>
        public static readonly ClueId[] CoreClues = { ClueId.C1, ClueId.C2, ClueId.C3 };

        // ── 견우 대화 등급 (문서 「10. AI 대화 등급」) ─────────────

        public const int FavorDelta = +5, FavorMax = 3;
        public const int PressureDelta = -5, PressureMax = 3;
        public const int InsultDelta = -15, InsultLockAt = 3;

        // ── 수령 경계도 (문서 「12」) ─────────────────────────────

        /// <summary>화제별 (증감, 최대 반복). 최대 0은 점수가 없다는 뜻.</summary>
        public static void Alert(AlertTopic topic, out int delta, out int maxTimes)
        {
            switch (topic)
            {
                case AlertTopic.Disappearance:   delta = +5;  maxTimes = 2; return;
                case AlertTopic.FatherCase:      delta = +15; maxTimes = 1; return;
                case AlertTopic.BridgeOrGate:    delta = +10; maxTimes = 1; return;
                case AlertTopic.Archive:         delta = +20; maxTimes = 1; return;
                case AlertTopic.ContradictionM1: delta = +15; maxTimes = 1; return;
                default:                         delta = 0;   maxTimes = 0; return;
            }
        }

        public static string Title(AlertTopic topic)
        {
            switch (topic)
            {
                case AlertTopic.Disappearance:   return "실종 사건을 직접 캐묻기";
                case AlertTopic.FatherCase:      return "선아 아버지 사건 언급";
                case AlertTopic.BridgeOrGate:    return "오작교 또는 암문 언급";
                case AlertTopic.Archive:         return "서고를 알고 있다는 언급";
                case AlertTopic.ContradictionM1: return "M1 모순을 직접 지적";
                default:                         return "일반 칠석제·마을 질문";
            }
        }

        /// <summary>실제 신분(암행어사) 공개 시 경계도 가산.</summary>
        public const int IdentityRevealAlert = 40;

        /// <summary>
        /// 소문을 퍼뜨리는 NPC인가 — 이쪽에 신분을 밝히면 마을 전체에 퍼져 경계도 +40(전역 1회).
        /// 견우·선아·상인·어머니·최초의 두 사람은 마패까지 보여 줘도 0이다.
        /// </summary>
        public static bool SpreadsRumor(NpcId npc)
        {
            switch (npc)
            {
                case NpcId.Jumo:
                case NpcId.Child01:
                case NpcId.Child02:
                case NpcId.Child03:
                case NpcId.Villager: return true;
                default: return false;
            }
        }

        /// <summary>견우 외 NPC가 태도를 닫는 무례 누적 횟수.</summary>
        public const int NpcInsultLimit = 3;

        // ── 검산 (문서 「11. 검산」) ──────────────────────────────

        /// <summary>
        /// 배점 합계를 되짚는다. 결정적 60 + 준결정 20 + 보조 25 + 모순 20 = 125 → 100 클램프.
        /// 최소 경로 C1·C2·C3 + B1 + C4·C6 = 80 이 성립하는지도 함께 본다.
        /// </summary>
        public static string SelfCheck()
        {
            int sum = 0;
            foreach (var i in _all) sum += i.evidence;
            int withContradictions = sum + ContradictionEvidence * 2;

            int minPath = Evidence(ClueId.C1) + Evidence(ClueId.C2) + Evidence(ClueId.C3)
                        + Evidence(ClueId.B1) + Evidence(ClueId.C4) + Evidence(ClueId.C6);

            int trustPlus = 0, trustMinus = 0;
            foreach (var i in _all) { if (i.trust > 0) trustPlus += i.trust; else trustMinus += i.trust; }

            return "단서 증거도 합 " + sum + " + 모순 20 = " + withContradictions + " (→100 클램프)\n" +
                   "최소 경로(C1·C2·C3·B1·C4·C6) = " + minPath + " / 필요 " + ProofEvidenceMin + "\n" +
                   "신뢰도 단서 제시 총합 +" + trustPlus + " / " + trustMinus;
        }
    }
}
