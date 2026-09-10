using System;
using System.Collections.Generic;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// AI 표식을 후보 신호로만 취급하고 실제 질문·답변·사건 상태를 최종 검증한다.
    ///
    /// ■ 낱말 목록의 기준 (2026-09-10 전수 점검)
    ///   답변 쪽 낱말은 <b>NpcPersonas 의 페르소나가 실제로 쓰는 표현</b>에서 뽑는다. 페르소나 문장을
    ///   그대로 옮겨 말한 답이 검증기에 걸리면 안 된다 — B3가 그랬다(주모: "등불을 들고 나가는 것을 봤다"
    ///   ↔ 목록에는 "나갔·나서"뿐). 반대로 질문 쪽은 문서 「제2부」의 획득 조건이 요구하는 화제를
    ///   벗어나지 않게, 넓히더라도 그 화제를 가리키는 낱말만 더한다.
    ///   비교는 공백을 지우고 소문자로 한 문자열 포함 검사(<see cref="HasAny"/>)다 — "드릴 말 없" 은
    ///   "드릴말없" 이 되므로 조사가 끼는 변형("드릴 말이 없")은 따로 적어야 걸린다.
    /// </summary>
    public static class DialogueGrantValidator
    {
        static readonly string[] Greetings = { "안녕", "누구", "성함", "어디요", "어디오", "날씨", "처음 뵙", "반갑" };

        public static bool IsGreetingOnly(string input)
        {
            string s = N(input);
            if (string.IsNullOrEmpty(s)) return true;
            return HasAny(s, Greetings) && !HasAny(s, "선아", "실종", "칠석", "전설", "그날", "아버지", "검수관", "오작교", "관아", "수령", "순찰", "관측실", "아래층", "등불", "서고", "사라진", "은하담", "사내");
        }

        // ── 답변에서 되풀이되는 묶음 ─────────────────────────────

        /// <summary>견우가 그날 일을 피하는 말 — 페르소나 「말투」의 회피 방식 다섯 가지를 전부 덮는다.</summary>
        static readonly string[] GyeonuEvasion =
        {
            "말할 수 없", "말하기 어렵", "말하기 힘", "말하기가 어렵", "말하고 싶지", "말씀드리기 어렵", "말씀드리기 힘", "말씀드릴 수 없",
            "드릴 말 없", "드릴 말이 없", "드릴 말씀", "할 말이 없", "할 말 없",
            "대답할 수 없", "대답하지 않", "대답하기 어렵", "답하기 어렵", "답하기 힘", "답할 수 없",
            "묻지 말", "묻지 마", "더는 물어", "그만 물어", "묻지 않으셨으면", "그 얘기는", "그 이야기는",
            "밝힐 수 없", "기억이 나지", "기억이 안", "꺼내기 힘", "꺼내기가 힘", "꺼내고 싶지", "꺼내기 어렵",
            "이만 가", "가보겠", "가봐야", "지금은 말", "지금은 아직",
        };

        public static bool CanGrant(NpcProfile p, ClueId clue, string question, string answer)
        {
            if (p == null || GyeonuCase.HasClue(clue) || IsGreetingOnly(question)) return false;
            if (p.npcId != NpcId.Gyeonu && GyeonuCase.NpcHostile(p.npcId)) return false;
            if (p.npcId == NpcId.Gyeonu && GyeonuCase.GyeonuLocked) return false;
            if (!p.CanGrant(clue) && !IsNewConversationClue(p.npcId, clue)) return false;

            string q = N(question), a = N(answer);
            switch (clue)
            {
                // 견우 — 그날 행적을 물었는데 바로 말하지 않는다 (레드헤링)
                case ClueId.B7:
                    return p.npcId == NpcId.Gyeonu &&
                           HasAny(q, "그날", "실종 당일", "사라진 날", "사라진 밤", "마지막으로", "칠석날") &&
                           HasAny(q, "어디", "무엇을", "뭘 했", "뭘 하", "행적", "선아를 본 뒤", "무슨 일") &&
                           (HasAny(a, GyeonuEvasion) ||
                            (HasAny(a, "집에 있", "집에만 있") && HasAny(a, "그날", "그날 밤", "밤은", "밤에"))) &&
                           !HasAny(a, "옛길", "길목", "오작교 건너", "기다렸", "기다리다", "선아가 오지", "혼자 돌아", "탈출", "도망", "타공 지도");

                // 수령 — 사건을 어떻게 보느냐고 물으면 견우 쪽으로 이끈다 (레드헤링)
                case ClueId.C8:
                    return p.npcId == NpcId.Magistrate &&
                           HasAny(q, "선아 실종", "이 사건", "사건을", "사건은", "실종", "누가 가장 의심", "누구를 의심", "의심", "어떻게 조사", "조사 방향", "조사 중", "어떻게 보", "어떻게 생각") &&
                           HasAny(a, "견우", "그 사내", "그 청년", "올해의 견우", "마지막 목격자", "마지막으로 함께") &&
                           HasAny(a, "의심", "수상", "캐물", "행적", "조사", "먼저 찾아", "찾아보", "알아보", "순서", "물어보", "눈여겨");

                // 주모 — 전설을 사실로 단언한다 (레드헤링)
                case ClueId.A7:
                    return p.npcId == NpcId.Jumo &&
                           HasAny(q, "정말", "사실", "믿", "생각", "진짜") &&
                           HasAny(q, "칠석", "전설", "하늘로 사라", "하늘로 갔", "하늘 문", "은하수", "직녀") &&
                           HasAny(a, "사실", "틀림없", "분명", "믿", "정말", "그렇고말고", "맞", "진짜") &&
                           HasAny(a, "하늘", "전설", "직녀", "칠석", "은하수", "문이 열") &&
                           !HasAny(a, "소문일 뿐", "믿지 않", "믿지 못", "헛소리");

                // 주모·상인 — 사람이 사라진 칠석은 전부 맑았다
                case ClueId.A3:
                    return (p.npcId == NpcId.Jumo || p.npcId == NpcId.FestivalMerchant) &&
                           HasAny(q, "예전", "지난", "그때", "그 해", "사라졌", "사라진", "실종", "옛날", "해마다", "칠석") &&
                           HasAny(q, "날씨", "맑", "비", "구름", "하늘") &&
                           HasAny(a, "그때", "그 해", "실종", "칠석", "사라진", "사라졌", "예로부터", "사람이 사라") &&
                           HasAny(a, "맑", "비가 오지", "비 없는", "구름 없", "구름 한 점", "비 오는 칠석엔", "비 오는 날엔", "비가 온 해", "비가 오면", "비 오는 해");

                // 주모 — 선아가 실종 전 밤마다 나다녔다
                case ClueId.B3:
                    return p.npcId == NpcId.Jumo &&
                           HasAny(q, "선아", "그 아씨", "아씨", "그 처자", "처자") &&
                           HasAny(q, "실종 전", "실종되기 전", "사라지기 전", "며칠 전", "최근", "밤", "해 진", "해가 진", "늦은", "평소와 달", "등불", "어디를 다", "나다", "나가") &&
                           HasAny(a, "밤마다", "밤에", "밤이면", "해 진", "해가 진", "늦은 시각", "며칠 전부터") &&
                           HasAny(a, "나갔", "나서", "나가는", "나가", "외출", "드나들", "돌아다니", "나다니", "다녔", "다니", "밖에서 봤", "밖에 있", "등불을 들고", "등불");

                // 상인 — 그날 밤 오작교 건너편에 혼자 서 있던 사내
                case ClueId.B4:
                    return p.npcId == NpcId.FestivalMerchant &&
                           HasAny(q, "견우", "사내", "남자", "누구", "누군가", "사람", "청년", "젊은") &&
                           HasAny(q, "그날", "당일", "칠석날", "그 밤", "그날 밤") &&
                           HasAny(q, "밤", "오작교", "다리", "건너편", "봤", "보았", "본 적", "목격") &&
                           HasAny(a, "견우", "사내", "젊은이", "젊은 이", "청년", "남자") &&
                           HasAny(a, "오작교", "다리", "건너편", "건너") &&
                           HasAny(a, "혼자", "홀로", "서 있", "있었", "돌아갔", "한참");

                // 상인 — 사람이 사라진 해마다 순찰이 유난히 잦았다
                case ClueId.C6:
                    return p.npcId == NpcId.FestivalMerchant &&
                           HasAny(q, "과거", "지난", "예전", "해마다", "실종 때", "사라진 해", "사람이 사라진", "그때", "옛날", "다른 해") &&
                           HasAny(q, "순찰", "관아", "포졸", "나졸", "돌았") &&
                           HasAny(a, "해마다", "실종이 있던", "그런 해", "사라진 해", "사람이 사라진", "예전에도", "그 해") &&
                           HasAny(a, "순찰", "포졸", "관아", "나졸") &&
                           HasAny(a, "많", "심했", "늘었", "유독", "유난", "잦", "자주 돌", "빈번", "평소보다", "다른 해에는", "다른 해엔");

                // 어머니 — 은하담이라는 이름은 그 일 뒤에 붙었다
                case ClueId.A4:
                    return p.npcId == NpcId.Mother &&
                           HasAny(q, "은하담") &&
                           HasAny(q, "이름", "부르", "언제부터", "왜", "원래", "뜻") &&
                           HasAny(a, "은하담", "그 이름", "이 이름", "그렇게 부르", "그리 부르", "그 못", "저 못", "이 못", "못이라", "이름") &&
                           HasAny(a, "딸", "직녀", "그 일", "그 사건", "사건 뒤", "사건 이후", "마흔 해", "사십 년", "원래 없던", "없던 이름", "그 전에는", "그전에는", "그 뒤", "있고 나서", "있고 난 뒤") &&
                           HasAny(a, "이름 붙", "이름을 붙", "붙였", "붙여", "붙었", "부르게", "부르기 시작", "부르기시작", "붙여 부", "불렀", "부른", "부르더", "그리 부르", "그렇게 부르");

                // 어머니 — 검수관은 죄인이 아니라 관아를 캐던 쪽
                case ClueId.C7:
                    return p.npcId == NpcId.Mother &&
                           HasAny(q, "아버지", "검수관", "아비") &&
                           HasAny(q, "죄인", "관물", "훔", "빼돌", "잡혀", "누명", "죄") &&
                           HasAny(a, "죄인 아니", "죄인이 아니", "죄인이 아님", "죄인은 아니", "죄인이라니", "죄인이었던 게 아니", "빼돌리지", "빼돌린 적", "훔치지", "누명", "그런 사람이 아니") &&
                           HasAny(a, "관아를 조사", "관아를 캐", "관아를 캐고", "캐고 다니", "캐던", "캐고", "비리를 조사", "비리를 캐", "관아의 비리", "관아 일을", "고발", "검수", "물으러", "조사하", "파고", "밝히려");

                // 최초의 직녀 — 그 아이는 여기 오지 않았다
                case ClueId.B5:
                    return p.npcId == NpcId.FirstJiknyeo &&
                           HasAny(q, "선아", "젊은 여인", "누군가", "아이", "여인", "처자", "사람", "누가") &&
                           HasAny(q, "왔", "찾아", "들어", "온 적", "오지", "도착", "지나갔", "지나간") &&
                           HasAny(q, "그날", "칠석", "최근", "여기", "이곳", "이 마을", "이 길", "성하리", "올해", "며칠") &&
                           (HasAny(a, "오지 않았", "오지 않", "아무도 오지", "온 사람 없", "온 사람은 없", "온 이 없", "온 이는 없", "찾아오지 않", "온 적 없", "온 적이 없", "들어오지 않", "오질 않") ||
                            (HasAny(a, "찾아온", "들어온", "도착한", "온 사람", "온 이") && HasAny(a, "아무도 없", "사람은 없", "이는 없", "사람이 없", "없었")));

                // 선아 — 구출 뒤, 협력을 청했을 때 풀이표(비교 기준)를 준다
                case ClueId.C3:
                    return p.npcId == NpcId.Seona && GyeonuCase.SeonaRescued &&
                           (GyeonuCase.HasFlag(GyeonuWorld.F_선아협력요청) || IsHelpOffer(q)) &&
                           HasAny(q, "돕", "도와", "수사", "조사", "협력", "증거", "풀이", "대조", "밝히", "기준", "맞대") &&
                           // 페르소나 「풀이표의 내용」: "표식과 흔적으로 가른 뒤 … 맞댄다" + "적어 드리겠습니다"
                           (HasAny(a, "풀이표", "비교표", "비교 기준", "비교하는 기준", "대조 기준", "판독 기준", "맞대는 기준", "가르는 기준", "나누는 기준", "기준 삼") ||
                            (HasAny(a, "맞대", "대조", "견주", "맞춰 보", "맞추어 보") && HasAny(a, "기준", "표식", "흔적")) ||
                            (HasAny(a, "표식") && HasAny(a, "매화", "학", "구름", "거북") && HasAny(a, "관아", "외고", "후고", "흔적")) ||
                            (HasAny(a, "적어 드리", "적어드리", "써 드리", "쪽지") && HasAny(a, "기준", "표식", "흔적", "기록")));

                // 견우 — 신뢰도 70 뒤의 진짜 행적
                case ClueId.B1:
                    return p.npcId == NpcId.Gyeonu && GyeonuCase.Trust >= 70 &&
                           HasAny(q, "그날", "선아", "약속", "행적", "칠석날", "무슨 일") &&
                           HasAny(a, "약속", "길목", "오지 않았", "오지 않", "기다렸", "기다리", "혼자 돌아", "옛길 초입", "초입");

                default:
                    return p.CanGrant(clue) && !string.IsNullOrEmpty(a);
            }
        }

        /// <summary>구출 뒤 선아에게 수사에 협력하겠다는 뜻을 보이는 말인가 — 가드와 플래그가 같은 기준을 쓴다.</summary>
        public static bool IsHelpOffer(string question) =>
            HasAny(question, "돕", "도와", "수사", "조사", "협력", "증거", "밝히", "함께", "힘을");

        public static void ObserveFacts(NpcProfile p, string question, string answer)
        {
            if (p == null || IsGreetingOnly(question)) return;
            string q = N(question), a = N(answer);

            // 수령 — "그날 밤 나는 칠석제 마무리로 동헌에 있었다"
            if (p.npcId == NpcId.Magistrate &&
                HasAny(q, "그날", "당일", "사라진 날", "그 시각", "칠석날") && HasAny(q, "밤", "그 시각", "어디", "무엇", "계셨", "있었") &&
                HasAny(a, "동헌", "관아") && HasAny(a, "칠석제 마무리", "마무리", "있었", "있었네", "머물"))
                GyeonuCase.SetFlag(GyeonuWorld.F_수령최초알리바이);

            // 상인 — "그날 새벽, 오작교 일대를 관아 어른이 손수 둘러보고 다녔다. 수령이었다"
            if (p.npcId == NpcId.FestivalMerchant &&
                HasAny(q, "그날", "당일", "새벽", "사라진 날", "칠석날") &&
                HasAny(q, "수령", "관아", "어른", "순찰", "사또", "포졸", "나리") &&
                HasAny(a, "수령", "관아 어른", "관아어른", "나리", "사또", "갓") &&
                HasAny(a, "오작교", "다리", "둘러보", "순찰", "일대"))
                GyeonuCase.SetFlag(GyeonuWorld.F_상인수령당일목격);

            // 주모 — "선아 아버지는 관물을 빼돌린 죄인이었다"
            if (p.npcId == NpcId.Jumo &&
                HasAny(q, "아버지", "검수관", "아비") && HasAny(q, "죄인", "관물", "훔", "빼돌", "죄", "어떤 사람") &&
                HasAny(a, "죄인", "범인", "죄를") && HasAny(a, "빼돌", "훔", "관물", "가져갔", "빼내"))
                GyeonuCase.SetFlag(GyeonuWorld.F_주모죄인주장);

            if (p.npcId == NpcId.Seona && GyeonuCase.SeonaRescued && IsHelpOffer(q))
                GyeonuCase.SetFlag(GyeonuWorld.F_선아협력요청);
        }

        public static bool CanConfirmM1(NpcProfile p, string question, string answer)
        {
            return p != null && p.npcId == NpcId.Magistrate && GyeonuCase.HasFlag(GyeonuWorld.F_수령최초알리바이) &&
                   GyeonuCase.HasFlag(GyeonuWorld.F_상인수령당일목격) &&
                   HasAny(question, "동헌", "알리바이", "오작교", "목격", "순찰", "그날", "다리", "새벽", "갓") &&
                   HasAny(question, "거짓", "모순", "어째", "아까", "그런데", "않았", "봤다", "보았다", "본 사람", "말이 다르", "다르지 않") &&
                   HasAny(answer, "잠시", "순찰", "둘러보", "잠깐", "나가 보");
        }

        public static bool CanConfirmM2(NpcProfile p, string question, string answer)
        {
            return p != null && p.npcId == NpcId.Jumo && GyeonuCase.HasFlag(GyeonuWorld.F_주모죄인주장) && GyeonuCase.HasClue(ClueId.C7) &&
                   HasAny(question, "아까", "죄인", "다른 사람", "어머니", "노파", "관아를 조사", "관아를 캐", "소문", "출처", "어디서 들", "누가 그러", "근거", "직접 봤") &&
                   HasAny(answer, "직접 본", "직접 본 건", "소문", "들었", "들은", "관아에서", "전해", "그리 말했", "그런 줄 알");
        }

        public static bool CanRevealSecret(NpcProfile p, TalkTone tone, string question, string answer)
        {
            if (p == null || string.IsNullOrEmpty(p.secretFlag) || GyeonuCase.HasFlag(p.secretFlag)) return false;
            if (tone == TalkTone.Pressure || tone == TalkTone.Insult || GyeonuCase.NpcHostile(p.npcId)) return false;
            if (p.npcId == NpcId.Child02)
                return HasAny(question, "관아", "담장", "개구멍", "틈", "몰래", "들어가는 길", "들어갈 수", "아는 길", "비밀") &&
                       HasAny(answer, "담장", "틈", "개구멍", "구멍", "뒤편", "돌 하나", "기어");
            if (p.npcId == NpcId.FirstGyeonu)
                return GyeonuCase.HasClue(ClueId.B2) &&
                       HasAny(question, "관측실", "관측소", "아래", "아래층", "등불", "흔적", "통로", "밑에") &&
                       HasAny(answer, "관아", "통로", "이어", "길", "갈래");
            return false;
        }

        /// <summary>
        /// 아이가 [노래] 를 적었고, 플레이어가 정말 노래를 <b>불러 달라고 청했는가</b> (2026-09-09, 09-10 좁힘).
        /// AI 표식은 후보 신호일 뿐이다 — 최종 판정은 <see cref="IsSongRequest"/> 가 한다.
        /// </summary>
        public static bool CanSing(NpcProfile p, string question, string answer)
        {
            if (p == null) return false;
            if (p.npcId != NpcId.Child01 && p.npcId != NpcId.Child02 && p.npcId != NpcId.Child03) return false;
            if (GyeonuCase.NpcHostile(p.npcId)) return false;
            if (!System.Text.RegularExpressions.Regex.IsMatch(answer ?? "", @"[\[\(【]\s*노래\s*[\]\)】]")) return false;
            return IsSongRequest(question);
        }

        // 노래에 대해 '묻는' 말 — 이 낱말이 섞이면 청과 겹쳐 있어도 애매하니 부르지 않는다 (과하게 부르는 쪽이 더 어색하다)
        static readonly string[] SongQuestionWords =
            { "무슨", "뭐", "뭔", "어디", "누구", "왜", "언제", "어떤", "가사", "배웠", "배운", "제목", "뜻", "얘기", "이야기", "설명" };

        /// <summary>
        /// 노래를 <b>불러 달라고 청한</b> 말인가 (2026-09-10).
        /// 부른다: "아까 그 노래 다시 불러줘", "한 번만 더 불러줄래", "노래 좀 들려줘".
        /// 안 부른다: "무슨 노래야?", "그 노래 어디서 배웠어?", "노래 가사가 뭐였지?" — 묻거나 언급했을 뿐이다.
        /// 규칙: 불러/들려/노래해 뒤에 청하는 꼬리(줘·주·줄래·달라·봐…)가 붙어야 하고, 질문어가 섞이면 안 된다.
        /// </summary>
        public static bool IsSongRequest(string question)
        {
            string q = N(question);
            if (string.IsNullOrEmpty(q)) return false;
            foreach (var w in SongQuestionWords) if (q.Contains(w)) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(
                q, @"(불러|들려|노래해|노래를해|노래한번|노래한곡)(줘|주|달라|다오|봐|볼래|줄래|줄수|주시|주십|주라|주면|주겠|주지)");
        }

        static bool IsNewConversationClue(NpcId npc, ClueId clue) =>
            (npc == NpcId.Gyeonu && clue == ClueId.B7) || (npc == NpcId.Magistrate && clue == ClueId.C8) || (npc == NpcId.Jumo && clue == ClueId.A7);

        static string N(string s) => (s ?? "").Replace(" ", "").Replace("\n", "").ToLowerInvariant();
        static bool HasAny(string s, params string[] words)
        {
            s = N(s);
            foreach (var w in words) if (s.Contains(N(w))) return true;
            return false;
        }
    }
}
