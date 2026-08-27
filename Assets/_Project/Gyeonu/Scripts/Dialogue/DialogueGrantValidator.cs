using System;
using System.Collections.Generic;

namespace IMUNROK.Gyeonu
{
    /// <summary>AI 표식을 후보 신호로만 취급하고 실제 질문·답변·사건 상태를 최종 검증한다.</summary>
    public static class DialogueGrantValidator
    {
        static readonly string[] Greetings = { "안녕", "누구", "성함", "어디요", "어디오", "날씨", "처음 뵙", "반갑" };

        public static bool IsGreetingOnly(string input)
        {
            string s = N(input);
            if (string.IsNullOrEmpty(s)) return true;
            return HasAny(s, Greetings) && !HasAny(s, "선아", "실종", "칠석", "전설", "그날", "아버지", "검수관", "오작교", "관아", "수령", "순찰", "관측실", "아래층", "등불", "서고");
        }

        public static bool CanGrant(NpcProfile p, ClueId clue, string question, string answer)
        {
            if (p == null || GyeonuCase.HasClue(clue) || IsGreetingOnly(question)) return false;
            if (p.npcId != NpcId.Gyeonu && GyeonuCase.NpcHostile(p.npcId)) return false;
            if (p.npcId == NpcId.Gyeonu && GyeonuCase.GyeonuLocked) return false;
            if (!p.CanGrant(clue) && !IsNewConversationClue(p.npcId, clue)) return false;

            string q = N(question), a = N(answer);
            switch (clue)
            {
                case ClueId.B7:
                    return p.npcId == NpcId.Gyeonu &&
                           HasAny(q, "그날", "실종 당일", "사라진 날", "마지막으로") &&
                           HasAny(q, "어디", "무엇을", "뭘 했", "행적", "선아를 본 뒤") &&
                           (HasAny(a, "말할 수 없", "말하기 어렵", "말하고 싶지", "드릴 말 없", "드릴 말씀", "대답할 수 없", "대답하지 않", "묻지 말", "묻지 마", "밝힐 수 없", "기억이 나지", "기억이 안", "더는 물어", "할 말이 없", "이만 가", "가보겠", "가봐야") ||
                            (HasAny(a, "집에 있", "집에만 있") && HasAny(a, "그날", "그날 밤", "밤은", "밤에"))) &&
                           !HasAny(a, "옛길", "길목", "오작교 건너", "기다렸", "기다리다", "선아가 오지", "혼자 돌아", "탈출", "도망", "타공 지도");
                case ClueId.C8:
                    return p.npcId == NpcId.Magistrate && HasAny(q, "선아 실종", "이 사건", "사건을", "누가 가장 의심", "어떻게 조사", "어떻게 보고") &&
                           HasAny(a, "견우", "그 사내", "올해의 견우") &&
                           HasAny(a, "의심", "수상", "캐물", "행적", "조사", "먼저 찾아", "찾아보라", "찾아보는");
                case ClueId.A7:
                    return p.npcId == NpcId.Jumo && HasAny(q, "정말", "사실", "믿", "생각") && HasAny(q, "칠석", "전설", "하늘로 사라", "직녀") &&
                           HasAny(a, "사실", "틀림없", "분명", "믿") && HasAny(a, "하늘", "전설", "직녀", "칠석") && !HasAny(a, "소문일 뿐", "믿지 않");
                case ClueId.A3:
                    return (p.npcId == NpcId.Jumo || p.npcId == NpcId.FestivalMerchant) &&
                           HasAny(q, "예전", "지난", "그때", "사라졌", "실종") && HasAny(q, "날씨", "맑", "비") &&
                           HasAny(a, "그때", "그 해", "실종", "칠석") && HasAny(a, "맑", "비가 오지", "비 없는", "구름 없");
                case ClueId.B3:
                    return p.npcId == NpcId.Jumo && HasAny(q, "선아", "그 아씨", "아씨") &&
                           HasAny(q, "실종 전", "최근", "밤", "해 진", "해가 진", "늦은", "평소와 달") &&
                           HasAny(a, "밤마다", "밤에", "해 진", "늦은 시각") && HasAny(a, "나갔", "나서", "외출", "드나들", "돌아다니", "나다니", "밖에서 봤", "밖에 있");
                case ClueId.B4:
                    return p.npcId == NpcId.FestivalMerchant && HasAny(q, "견우", "사내") && HasAny(q, "그날", "당일") && HasAny(q, "밤", "오작교", "다리", "건너편") &&
                           HasAny(a, "견우", "사내") && HasAny(a, "오작교", "다리", "건너편") && HasAny(a, "혼자", "서 있", "있었");
                case ClueId.C6:
                    return p.npcId == NpcId.FestivalMerchant && HasAny(q, "과거", "지난", "예전", "해마다", "실종 때", "사라진 해") && HasAny(q, "순찰", "관아", "포졸") &&
                           HasAny(a, "해마다", "실종이 있던", "그런 해", "사라진 해", "예전에도") && HasAny(a, "순찰", "포졸", "관아") && HasAny(a, "많", "심했", "늘었", "유독", "잦", "자주 돌", "빈번", "평소보다");
                case ClueId.A4:
                    return p.npcId == NpcId.Mother && HasAny(q, "은하담") && HasAny(q, "이름", "부르", "언제부터", "왜") &&
                           HasAny(a, "은하담", "그 이름", "이 이름") && HasAny(a, "딸", "직녀", "그 일", "그 사건", "사건 뒤", "사건 이후") && HasAny(a, "이름 붙", "이름을 붙", "붙였", "붙여", "붙었다", "부르게", "부르기 시작", "붙여 부");
                case ClueId.C7:
                    return p.npcId == NpcId.Mother && HasAny(q, "아버지", "검수관") && HasAny(q, "죄인", "관물", "훔", "빼돌", "잡혀") &&
                           HasAny(a, "죄인 아니", "죄인이 아니", "죄인이 아님", "빼돌리지", "훔치지", "누명") && HasAny(a, "관아를 조사", "관아를 캐", "비리를 조사", "비리를 캐", "고발", "검수");
                case ClueId.B5:
                    return p.npcId == NpcId.FirstJiknyeo && HasAny(q, "그날", "그날 밤", "실종 날") && HasAny(q, "선아", "젊은 여인", "누군가") && HasAny(q, "왔", "찾아", "들어") &&
                           (HasAny(a, "오지 않았", "아무도 오지", "온 사람 없", "찾아오지 않았") ||
                            (HasAny(a, "찾아온", "들어온", "도착한", "온 사람", "온 이") && HasAny(a, "아무도 없", "사람은 없", "이는 없", "사람이 없", "없었")));
                case ClueId.C3:
                    return p.npcId == NpcId.Seona && GyeonuCase.SeonaRescued && GyeonuCase.HasFlag(GyeonuWorld.F_선아협력요청) &&
                           HasAny(q, "돕", "도와", "수사", "조사", "협력", "증거", "풀이", "대조", "밝히") &&
                           (HasAny(a, "풀이표", "비교표", "비교 기준", "비교하는 기준", "대조 기준", "판독 기준") ||
                            (HasAny(a, "맞대어 보", "대조해 보") && HasAny(a, "기준")));
                case ClueId.B1:
                    return p.npcId == NpcId.Gyeonu && GyeonuCase.Trust >= 70 && HasAny(q, "그날", "선아", "약속", "행적") && HasAny(a, "약속", "길목", "오지 않았", "기다렸");
                default:
                    return p.CanGrant(clue) && !string.IsNullOrEmpty(a);
            }
        }

        public static void ObserveFacts(NpcProfile p, string question, string answer)
        {
            if (p == null || IsGreetingOnly(question)) return;
            string q = N(question), a = N(answer);

            if (p.npcId == NpcId.Magistrate && HasAny(q, "그날", "당일", "사라진 날", "그 시각") && HasAny(q, "밤", "그 시각", "어디", "무엇") &&
                HasAny(a, "동헌", "관아") && HasAny(a, "칠석제 마무리", "마무리", "있었"))
                GyeonuCase.SetFlag(GyeonuWorld.F_수령최초알리바이);

            if (p.npcId == NpcId.FestivalMerchant && HasAny(q, "그날", "당일", "새벽", "사라진 날") && HasAny(q, "오작교", "다리", "다리 쪽") && HasAny(q, "수령", "관아", "어른", "순찰") &&
                HasAny(a, "수령", "관아 어른", "나리") && HasAny(a, "오작교", "다리", "둘러보", "순찰"))
                GyeonuCase.SetFlag(GyeonuWorld.F_상인수령당일목격);

            if (p.npcId == NpcId.Jumo && HasAny(q, "아버지", "검수관") && HasAny(q, "죄인", "관물", "훔", "빼돌") &&
                HasAny(a, "죄인", "범인") && HasAny(a, "빼돌", "훔", "관물"))
                GyeonuCase.SetFlag(GyeonuWorld.F_주모죄인주장);

            if (p.npcId == NpcId.Seona && GyeonuCase.SeonaRescued && HasAny(q, "돕", "도와", "수사", "조사", "협력", "증거", "밝히"))
                GyeonuCase.SetFlag(GyeonuWorld.F_선아협력요청);
        }

        public static bool CanConfirmM1(NpcProfile p, string question, string answer)
        {
            return p != null && p.npcId == NpcId.Magistrate && GyeonuCase.HasFlag(GyeonuWorld.F_수령최초알리바이) &&
                   GyeonuCase.HasFlag(GyeonuWorld.F_상인수령당일목격) && HasAny(question, "동헌", "알리바이", "오작교", "목격", "순찰", "그날") &&
                   HasAny(question, "거짓", "모순", "어째", "아까", "그런데", "않았") && HasAny(answer, "잠시", "순찰", "둘러보");
        }

        public static bool CanConfirmM2(NpcProfile p, string question, string answer)
        {
            return p != null && p.npcId == NpcId.Jumo && GyeonuCase.HasFlag(GyeonuWorld.F_주모죄인주장) && GyeonuCase.HasClue(ClueId.C7) &&
                   HasAny(question, "아까", "죄인", "다른 사람", "어머니", "관아를 조사", "소문", "출처") &&
                   HasAny(answer, "직접 본", "소문", "들었", "관아에서", "전해");
        }

        public static bool CanRevealSecret(NpcProfile p, TalkTone tone, string question, string answer)
        {
            if (p == null || string.IsNullOrEmpty(p.secretFlag) || GyeonuCase.HasFlag(p.secretFlag)) return false;
            if (tone == TalkTone.Pressure || tone == TalkTone.Insult || GyeonuCase.NpcHostile(p.npcId)) return false;
            if (p.npcId == NpcId.Child02)
                return HasAny(question, "관아", "담장", "개구멍", "틈", "몰래", "들어가는 길") && HasAny(answer, "담장", "틈", "개구멍", "구멍", "뒤편");
            if (p.npcId == NpcId.FirstGyeonu)
                return GyeonuCase.HasClue(ClueId.B2) && HasAny(question, "관측실", "아래", "아래층", "등불", "흔적", "통로") && HasAny(answer, "관아", "통로", "이어", "길");
            return false;
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
