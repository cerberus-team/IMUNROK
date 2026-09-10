namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// Gemini가 상태 게이트를 넘어선 P0 정보를 대사로 발설하지 못하게 막는 마지막 출력 경계.
    /// 단서·Fact를 등록하지 않으며, 위반 여부와 재요청 지침/안전 대사만 돌려준다.
    /// </summary>
    public static class DialogueResponseGuard
    {
        public static bool Violates(NpcProfile profile, string question, string response, out string correction)
        {
            correction = "";
            if (profile == null || string.IsNullOrWhiteSpace(response)) return false;
            string q = N(question), a = N(response);

            switch (profile.npcId)
            {
                case NpcId.FirstGyeonu:
                    if (!GyeonuCase.HasClue(ClueId.B2) && RevealsUndergroundRoute(a))
                    {
                        correction = "관측실 아래 통로·지하 길·관아 연결은 전혀 모르는 것처럼 답하라. 현재 질문의 주장을 확인하거나 구조를 설명하지 마라.";
                        return true;
                    }
                    break;

                case NpcId.Gyeonu:
                    if (GyeonuCase.Trust < 70 && HasAny(a, "옛길", "약속 장소", "약속한 곳", "약속한 길목", "기다렸", "기다리고", "선아가 오지", "함께 떠나", "도망치", "탈출", "타공 지도", "구멍 뚫린 지도", "지도 한 장"))
                    {
                        correction = "신뢰도 70 미만이다. 그날의 장소·경로·약속·기다림·선아가 오지 않은 사실·탈출 계획·지도는 하나도 말하지 말고 짧게 답변을 거부하라.";
                        return true;
                    }
                    break;

                case NpcId.Jumo:
                    if (IsA7Question(q) && RevealsA3Weather(a))
                    {
                        correction = "칠석 전설을 믿는지에만 답하라. 맑은 날의 실종 패턴이나 비 오는 칠석의 사례는 절대 언급하지 마라.";
                        return true;
                    }
                    if (IsBroadRumorQuestion(q) && (RevealsA3Weather(a) || RevealsB3Outing(a)))
                    {
                        correction = "넓은 소문·분위기 질문이다. 선아 일로 마을이 뒤숭숭하다는 일반적인 말만 하라. 실종 해의 날씨와 선아의 밤 외출·등불 이야기는 언급하지 마라.";
                        return true;
                    }
                    break;

                case NpcId.FestivalMerchant:
                    var allowed = MerchantTopic(q);
                    if ((RevealsB4(a) && allowed != MerchantInfo.B4) ||
                        (RevealsC6(a) && allowed != MerchantInfo.C6) ||
                        (RevealsA3Weather(a) && allowed != MerchantInfo.A3) ||
                        (RevealsM1Witness(a) && allowed != MerchantInfo.M1))
                    {
                        correction = "현재 질문에 직접 답하는 정보 하나만 말하라. 다리 건너 사내, 과거 순찰, 칠석 날씨, 사건 당일 수령 목격 중 질문받지 않은 정보는 전부 빼라.";
                        return true;
                    }
                    break;

                case NpcId.Child01:
                    if (RevealsChildRoute(a))
                    {
                        correction = "비밀 장소의 존재 자체를 말하지 마라. 관아·뒤편·담장·틈·구멍·개구멍·드나드는 길을 부정문에도 넣지 말고, 모른다고만 짧게 답하라.";
                        return true;
                    }
                    break;

                case NpcId.Seona:
                    if (!CanRevealC3(q) && RevealsConcreteC3(a))
                    {
                        correction = "아직 C3 협력 조건이 성립하지 않았다. 풀이표·비교 기준이나 검수 기록과 은닉 기록을 맞대는 구체적 방법을 말하지 말고, 현재 질문에 일반적으로만 답하라.";
                        return true;
                    }
                    break;
            }
            return false;
        }

        public static string Fallback(NpcProfile profile)
        {
            if (profile == null) return "[등급:중립]\n…그 일은 잘 모르겠습니다.";
            switch (profile.npcId)
            {
                case NpcId.FirstGyeonu: return "[등급:중립]\n그런 길은 들어 본 적이 없소.";
                case NpcId.Gyeonu: return "[등급:압박]\n…그날 일은 지금은 말씀드리기 어렵습니다. 더 묻지 말아주십시오.";
                case NpcId.Jumo: return "[등급:중립]\n글쎄, 요즘은 다들 그 아이 일로 뒤숭숭할 뿐이오.";
                case NpcId.FestivalMerchant: return "[등급:중립]\n그런 일은 모른다. 나는 본 것 말고는 답할 수 없소.";
                case NpcId.Child01: return "[등급:중립]\n그런 건 몰라요. 왜 우리한테 물어요?";
                case NpcId.Seona: return "[등급:중립]\n지금은 자세히 말씀드리기 어렵습니다.";
                default: return "[등급:중립]\n…그 일은 잘 모르겠습니다.";
            }
        }

        static bool RevealsUndergroundRoute(string a) =>
            HasAny(a, "관측실 아래", "관측소 아래", "바닥 아래", "지하") && HasAny(a, "통로", "길", "이어", "갈래", "관아 쪽", "관아로");

        static bool IsA7Question(string q) => HasAny(q, "믿", "사실", "정말", "생각") && HasAny(q, "칠석", "전설", "하늘", "은하수", "사라졌");
        static bool IsBroadRumorQuestion(string q) =>
            HasAny(q, "소문", "이상한 일", "분위기", "무슨 일", "어떻게 생각") &&
            !HasAny(q, "날씨", "맑", "비 오는", "밤마다", "등불", "밤에 나", "해가 진");
        static bool RevealsA3Weather(string a) =>
            (HasAny(a, "맑", "구름 없") && HasAny(a, "실종", "사라진", "칠석")) ||
            (HasAny(a, "비 오는", "비가 온", "비가 오") && HasAny(a, "아무 일 없", "사라지지", "실종 없"));
        static bool RevealsB3Outing(string a) =>
            HasAny(a, "밤마다", "밤에", "해 진 뒤") && HasAny(a, "등불", "나갔", "나서", "돌아다니", "나다니", "드나들");
        static bool RevealsChildRoute(string a) =>
            (HasAny(a, "관아") && HasAny(a, "담장", "틈", "구멍", "개구멍")) ||
            (HasAny(a, "관아 뒤편") && HasAny(a, "들어가", "드나들", "기어"));
        /// <summary>
        /// 구출 뒤 협력 요청이 <b>이미 기록돼 있거나, 지금 이 질문이 곧 협력 요청</b>이면 풀이표를 말해도 된다 (2026-09-10).
        /// 플래그는 대답이 돌아온 뒤 <see cref="DialogueGrantValidator.ObserveFacts"/> 가 세우므로, 플래그만 보면
        /// 첫 협력 질문은 언제나 가드에 막혀 안전 대사로 바뀌었다 — 두 번 물어야 C3가 나오던 원인이다.
        /// 질문 판정 낱말은 ObserveFacts와 같은 목록을 쓴다.
        /// </summary>
        static bool CanRevealC3(string q) =>
            GyeonuCase.SeonaRescued &&
            (GyeonuCase.HasFlag(GyeonuWorld.F_선아협력요청) || DialogueGrantValidator.IsHelpOffer(q));
        static bool RevealsConcreteC3(string a) =>
            HasAny(a, "풀이표", "비교표") ||
            (HasAny(a, "비교 기준", "비교하는 기준", "대조 기준", "맞대어 보는 기준") && HasAny(a, "아버지", "검수", "관아", "은닉", "기록")) ||
            (HasAny(a, "검수 기록") && HasAny(a, "은닉 기록", "관아 기록", "관아 장부") && HasAny(a, "맞대", "대조", "비교"));
        static bool RevealsB4(string a) => HasAny(a, "오작교", "다리 건너", "건너편") && HasAny(a, "사내", "남자", "젊은 이") && HasAny(a, "혼자", "홀로", "서 있");
        static bool RevealsC6(string a) => HasAny(a, "예전", "해마다", "사라진 해", "실종이 있던") && HasAny(a, "순찰", "포졸", "관아") && HasAny(a, "잦", "많", "유난", "자주", "늘었");
        static bool RevealsM1Witness(string a) => HasAny(a, "그날", "그날 새벽", "그날 밤") && HasAny(a, "오작교", "다리") && HasAny(a, "수령", "관아 어른", "사또") && HasAny(a, "봤", "보았", "목격", "둘러보");

        enum MerchantInfo { None, B4, C6, A3, M1 }
        static MerchantInfo MerchantTopic(string q)
        {
            if (HasAny(q, "서고", "갇혀", "감금")) return MerchantInfo.None;
            if (HasAny(q, "그날", "당일", "새벽") && HasAny(q, "수령", "관아 사람", "관아 어른", "사또")) return MerchantInfo.M1;
            if (HasAny(q, "예전", "해마다", "사라진 해", "실종 때") && HasAny(q, "순찰", "포졸", "관아")) return MerchantInfo.C6;
            if (HasAny(q, "날씨", "맑", "비") && HasAny(q, "예전", "실종", "사라진", "칠석")) return MerchantInfo.A3;
            if (HasAny(q, "오작교", "다리", "건너편") && HasAny(q, "사내", "남자", "누구", "혼자")) return MerchantInfo.B4;
            if (HasAny(q, "그날", "당일", "그 밤") &&
                HasAny(q, "누군가", "사람", "이상한 사람", "수상한 사람", "사내", "남자") &&
                HasAny(q, "봤", "보았", "본 적", "목격")) return MerchantInfo.B4;
            return MerchantInfo.None;
        }

        static string N(string s) => (s ?? "").Replace(" ", "").Replace("\n", "").ToLowerInvariant();
        static bool HasAny(string s, params string[] words)
        {
            s = N(s);
            foreach (var word in words) if (s.Contains(N(word))) return true;
            return false;
        }
    }
}
