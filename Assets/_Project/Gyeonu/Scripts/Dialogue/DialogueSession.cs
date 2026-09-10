using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 대화 한 판(NPC 한 명과 마주 선 동안)의 진행 (2026-08-25).
    /// 화면(<see cref="DialogueUI"/>)과 자리잡기(<see cref="NpcDialogue"/>)에서 떼어 놓았다 —
    /// 여기는 "무엇을 묻고 무엇이 돌아왔고 점수가 어떻게 움직였는가"만 안다.
    ///
    /// ■ AI는 공통 연동을 그대로 쓴다
    ///   <see cref="INpcResponder"/> 계약에 맞춰 <see cref="GeminiNpcResponder"/> 를 부른다.
    ///   공통 코드는 한 줄도 고치지 않았다. 대신 공통이 열어 둔 두 구멍으로 사건 사정을 넣는다.
    ///     · character.persona   — 인물 정의 (붙박이)
    ///     · unlockedFacts       — 지금의 태도 · 방금 제시받은 것 (매 턴 갈아 끼움)
    ///   공통 쪽이 systemInstruction에 "밝혀진 사실에 없는 핵심 비밀은 말하지 마라"를 이미
    ///   붙여 주므로, 태도 문장을 여기 실으면 <b>구간이 오르기 전에는 말하지 않는다</b>가 그대로 산다.
    ///
    /// ■ 등급 판정을 같은 요청에 얹은 까닭 (기획 「10. AI 대화 등급」)
    ///   ① 왕복이 두 번이면 대화 한 마디의 지연이 두 배가 된다 — VR에서 눈앞의 사람이
    ///      2~3초 굳어 있는 것은 그대로 체감된다.
    ///   ② 등급은 문맥이 정한다. "왜 말하지 않았습니까"가 압박인지 단순 확인인지는 앞의
    ///      대화를 봐야 갈린다. 대답을 짓는 그 문맥 안에서 함께 판정하는 편이 정확하다.
    ///   ③ 비용·실패 지점이 절반이 된다.
    ///   그래서 응답 첫 줄에 [등급:호의|중립|압박|모욕] 을 적게 하고 여기서 떼어 낸다.
    ///   ⚠️ 못 읽으면 <b>중립(0점)</b> 이다 — 문서의 "정상적인 추리 질문에 과도한 페널티를 주지
    ///      않는다"와 같은 방향의 안전한 기본값이다. 판정 실패가 벌이 되면 안 된다.
    /// </summary>
    public class DialogueSession : IDialogueBackend
    {
        /// <summary>대화창에 쌓이는 한 줄.</summary>
        public class Line
        {
            public bool fromPlayer;
            public string speaker;
            public string text;
            public TalkTone tone = TalkTone.Neutral;
            public bool graded;      // 등급이 실제로 점수에 반영된 줄인가 (제시·중립은 false)
        }

        public NpcProfile Profile { get; }
        public IReadOnlyList<Line> Lines => _lines;
        public bool Busy { get; private set; }
        /// <summary>지금 쓰이는 대답기 이름 — 화면 구석에 밝혀 둔다(목업인지 진짜 AI인지).</summary>
        public string BackendName { get; }

        readonly List<Line> _lines = new List<Line>();
        readonly List<string> _transcript = new List<string>();   // 공통 연동에 넘길 원문 줄
        readonly INpcResponder _responder;
        readonly InterrogationCharacter _character;               // 공통 연동에 넘길 껍데기
        readonly MonoBehaviour _host;                             // 코루틴(네트워크) 주인
        string _lastPlayerInput = "";

        /// <summary>줄이 하나 늘었다 — 화면이 다시 그린다.</summary>
        public event Action Changed;

        /// <summary>비밀 한 조각을 털어놓았다 — 아이02의 Secret 연출이 여기에 붙는다.</summary>
        public event Action SecretTold;

        /// <summary>고마움을 표했다 — 어머니·최초의 직녀의 Thank 모션이 여기에 붙는다.</summary>
        public event Action Thanked;

        /// <summary>노래를 청해 아이가 응했다 — <see cref="ChildrenSong"/> 이 여기에 붙는다 (2026-09-09).</summary>
        public event Action SongRequested;

        public DialogueSession(NpcProfile profile, MonoBehaviour host)
        {
            Profile = profile;
            _host = host;

            // 공통 연동은 InterrogationCharacter를 받는다. 에셋으로 만들지 않고 그때그때 채운다 —
            // 인물 정의의 원본은 NpcProfile 하나뿐이어야 두 곳이 어긋나지 않는다.
            _character = ScriptableObject.CreateInstance<InterrogationCharacter>();
            _character.characterName = profile.displayName;
            _character.caseId = CaseId.Case3_Gyeonu;
            _character.persona = profile.persona;
            _character.openingLine = profile.openingLine;

            if (HasApiKey)
            {
                // ⚠️ 공통 GeminiNpcResponder가 아니라 사건 쪽 대답기를 쓴다 — 공통 쪽 모델
                //    (gemini-2.0-flash)이 내려가 404가 나는데 그 상수는 읽기 전용 폴더에 있다.
                //    까닭은 GyeonuGeminiResponder 주석에 적어 두었다. 계약은 똑같다.
                _responder = new GyeonuGeminiResponder();
                BackendName = "Gemini " + GyeonuGeminiResponder.Model;
            }
            else
            {
                // 공통 MockNpcResponder는 등급을 붙이지 않아 점수 경로가 통째로 안 돌아간다.
                // 키가 없을 때도 끝까지 시험할 수 있게 사건 쪽 대타를 쓴다.
                _responder = new GyeonuMockResponder(profile);
                BackendName = "목업(키 없음)";
            }

            AddNpcLine(profile.openingLine, TalkTone.Neutral, false);
            MarkFirstTalkOnly();
        }

        /// <summary>
        /// 문서에서 '첫 대화'·'마을 자동'으로 적힌 단서(A6·A7·B7·C8)를 여기서 준다.
        /// 전부 <b>레드헤링</b>이라 얻는 것 자체가 함정이다 — 견우에게 내밀면 신뢰도가 깎인다.
        /// 그래서 AI 표식에 맡기지 않고 만나는 순간 확정적으로 준다. 기획의 배치가 그렇게 되어 있다.
        /// </summary>
        void MarkFirstTalkOnly()
        {
            if (GyeonuCase.HasFlag(Profile.TalkedFlag)) return;
            // ⚠️ 단서가 없어도 '만났다'는 사실 자체는 남긴다 — 상인이 은하담에서 주막으로
            //    옮겨 가는 조건이 이 플래그다 (NpcSchedule.forbidFlags).
            GyeonuCase.SetFlag(Profile.TalkedFlag);

            foreach (var f in Profile.flagsOnFirstTalk) GyeonuCase.SetFlag(f);

            // grantOnFirstTalk는 하위 에셋 호환을 위해 필드는 남기되 런타임에서는 사용하지 않는다.
        }

        /// <summary>키 파일이 있는가 — StreamingAssets/gemini_key.txt 또는 루트 gemini_api_key.txt (<see cref="GeminiKeyFile"/>).</summary>
        public static bool HasApiKey => GeminiKeyFile.Exists();

        // ─────────────────────────────────────────────────────────
        //  ① 말하기
        // ─────────────────────────────────────────────────────────
        public void Ask(string text)
        {
            if (Busy || string.IsNullOrWhiteSpace(text)) return;
            string say = text.Trim();
            _lastPlayerInput = say;
            AddPlayerLine(say);
            // ⚠️ 접두사 "어사"는 공통 연동이 <b>화자 역할(user/model)을 가르는 표식</b>이다
            //    (GeminiNpcResponder.BuildContents). 보내기 전에 떼어 내므로 AI는 이 말을 보지 않는다 —
            //    제3사건의 플레이어는 나그네지 어사가 아니다. 표식만 규약대로 맞춘다.
            _transcript.Add("어사: " + say);

            Send(new NpcRequest
            {
                character = _character,
                transcript = _transcript,
                unlockedFacts = BuildFacts(null),
                playerInput = say,
                isEvidence = false,
                justRevealedInfo = null,
            }, grade: Profile.gradeTalk);
        }

        // ─────────────────────────────────────────────────────────
        //  ② 단서 제시
        // ─────────────────────────────────────────────────────────
        /// <summary>
        /// 단서를 내민다. 점수는 <see cref="GyeonuCase"/> 가 정한다 — 여기서 숫자를 세지 않는다.
        /// 제시에는 <b>등급을 매기지 않는다</b>. 증감표(문서 「10. 단서 제시」)가 이미 정해 놓았고,
        /// 거기에 대화 등급까지 얹으면 한 번의 행동이 두 번 점수를 움직인다.
        /// </summary>
        public void Present(ClueId id)
        {
            if (Busy) return;
            var info = ClueTable.Get(id);
            string label = ClueTable.Label(id);
            _lastPlayerInput = label;

            int delta = 0;
            if (Profile.npcId == NpcId.Gyeonu)
            {
                bool already = GyeonuCase.HasPresented(id);
                bool scored = GyeonuCase.PresentClueToGyeonu(id);
                delta = scored ? ClueTable.Trust(id) : 0;
                if (already) Debug.Log("[대화] 이미 제시한 단서 — 신뢰도는 다시 움직이지 않는다: " + label);
            }

            AddPlayerLine("〔" + label + "〕 을(를) 내밀었다.");
            _transcript.Add("어사(증거): " + label + (info != null ? " — " + info.source + "에서 얻은 것" : ""));

            // 제시 결과를 '방금 열린 사실'로 넘긴다 — 공통 연동이 "마지못해 인정하라"로 감싸 준다.
            string revealed = null;
            if (delta > 0) revealed = "이 이야기는 네게 도움이 된다. 마음이 조금 열린다. 아는 만큼만 대답하라.";
            else if (delta < 0) revealed = "이 이야기는 너를 서운하게 한다. 서운함을 짧게 드러내되 화를 내지는 않는다.";

            Send(new NpcRequest
            {
                character = _character,
                transcript = _transcript,
                unlockedFacts = BuildFacts(label),
                playerInput = label,
                isEvidence = true,
                justRevealedInfo = revealed,
            }, grade: false);
        }

        /// <summary>정보 단서가 아닌 실제 물건 제시. 신분은 AI 문장이 아니라 마패 실물만 확정한다.</summary>
        public bool PresentItem(InventoryItem item)
        {
            if (Busy || item == null || !Inventory.Has(item) || !IsMapae(item)) return false;
            AddPlayerLine("〔" + item.displayName + "〕 을(를) 실제로 내밀었다.");
            _transcript.Add("어사(증거): " + item.displayName + "을 실제로 제시했다.");
            GyeonuCase.RevealIdentity(Profile.npcId);
            return true;
        }

        static bool IsMapae(InventoryItem item)
        {
            string key = (item.Key ?? "").ToUpperInvariant();
            string name = item.displayName ?? "";
            return key == "MAPAE" || key.Contains("MAPAE_") || name.Contains("마패");
        }

        // ─────────────────────────────────────────────────────────
        void Send(NpcRequest req, bool grade)
        {
            Busy = true;
            Changed?.Invoke();
            _responder.GetResponse(_host, req,
                reply => OnReply(reply, grade),
                err =>
                {
                    Busy = false;
                    AddNpcLine("(대답이 오지 않았다 — " + err + ")", TalkTone.Neutral, false);
                });
        }

        void OnReply(string raw, bool grade)
        {
            Busy = false;
            var tone = ParseTone(ref raw);
            ApplyMarkers(ref raw, tone, _lastPlayerInput);
            bool applied = false;
            if (grade)
            {
                Report(tone);
                applied = tone != TalkTone.Neutral;
            }
            if (string.IsNullOrWhiteSpace(raw)) raw = "…";
            _transcript.Add(Profile.displayName + ": " + raw);
            AddNpcLine(raw, tone, applied);
        }

        // ─────────────────────────────────────────────────────────
        //  표식 — 단서 · 화제 · 모순 · 신분
        // ─────────────────────────────────────────────────────────
        /// <summary>
        /// 응답에 섞인 표식을 떼어 내고 그대로 <see cref="GyeonuCase"/> 에 옮긴다 (2026-08-25).
        ///
        /// ■ 왜 표식인가
        ///   NPC 대사는 자유 문장이라 "지금 B3을 말했는가"를 코드가 알 방법이 없다. 등급을 같은
        ///   요청에 얹은 것과 같은 이유로(왕복·문맥·비용), <b>말한 당사자가 표식을 붙이게</b> 한다.
        ///   [등급:…] 이 이미 그 방식으로 돌고 있으니 규약도 한 벌로 유지된다.
        ///
        /// ■ ⚠️ 지어낸 단서를 막는다
        ///   AI가 아무 코드나 적으면 추리가 통째로 앞질러 간다. 그래서 <see cref="NpcProfile.CanGrant"/>
        ///   — 그 인물이 실제로 줄 수 있다고 기획에 적힌 단서 — 만 통과시킨다.
        ///   문서 「7. 배치 요약」이 그 목록의 원본이다.
        ///
        /// ■ 표식을 못 읽어도 벌하지 않는다
        ///   표식이 없으면 단서를 주지 않을 뿐 대사는 그대로 뜬다. 등급 판정과 같은 방향이다.
        /// </summary>
        void ApplyMarkers(ref string text, TalkTone tone, string playerMessage)
        {
            if (string.IsNullOrEmpty(text)) return;

            string answerWithMarkers = text;
            DialogueGrantValidator.ObserveFacts(Profile, playerMessage, answerWithMarkers);

            // B7/A7/C8은 예전 첫 대화 단서였다. 이제 실제 질문과 답변이 맞을 때만 후보로 넣는다.
            var candidates = new HashSet<ClueId>();
            if (Profile.npcId == NpcId.Gyeonu) candidates.Add(ClueId.B7);
            if (Profile.npcId == NpcId.Magistrate) candidates.Add(ClueId.C8);
            if (Profile.npcId == NpcId.Jumo) candidates.Add(ClueId.A7);

            foreach (var g in Matches(text, @"[\[\(【]\s*단서\s*[:：]?\s*([A-Ca-c]\s*[0-9])\s*[\]\)】]"))
            {
                string code = g.Replace(" ", "").ToUpperInvariant();
                if (!ClueTable.TryParse(code, out var id)) continue;
                candidates.Add(id);
            }

            foreach (var id in candidates)
            {
                if (!DialogueGrantValidator.CanGrant(Profile, id, playerMessage, answerWithMarkers)) continue;
                if (GyeonuCase.AddClue(id))
                {
                    Debug.Log("[대화] " + Profile.displayName + " → 단서 " + ClueTable.Label(id));
                    GyeonuCase.CheckContradictionsFromClues();

                    // C3 선아의 풀이표는 정보이자 <b>쪽지</b>다 (2026-09-10). 대화로 얻는 순간 실물도 소지품에
                    // 넣어 서고에서 다시 펴 볼 수 있게 한다 — 장부 1단계는 플래그만 보지만, 기준을 한 번 들은
                    // 말로만 기억하게 두면 서고 앞에서 막힌다. 이미 지녔으면 Add가 그냥 false를 돌려준다.
                    if (id == ClueId.C3)
                    {
                        var sheet = Inventory.Find("C3");
                        if (sheet != null && Inventory.Add(sheet)) Debug.Log("[대화] 선아가 풀이표 쪽지를 건넸다 — 소지품 C3");
                    }
                }
                foreach (var f in Profile.grantableFlags) GyeonuCase.SetFlag(f);
            }

            if (Profile.tagAlertTopic)
                foreach (var g in Matches(text, @"[\[\(【]\s*화제\s*[:：]?\s*(일반|실종|아버지|다리|서고)\s*[\]\)】]"))
                    GyeonuCase.AskMagistrate(TopicOf(g));

            if (DialogueGrantValidator.CanConfirmM1(Profile, playerMessage, answerWithMarkers))
            {
                GyeonuCase.AddContradiction(ContradictionId.M1);
                GyeonuCase.AskMagistrate(AlertTopic.ContradictionM1);
            }

            if (DialogueGrantValidator.CanConfirmM2(Profile, playerMessage, answerWithMarkers))
            {
                GyeonuCase.SetFlag(GyeonuWorld.F_주모소문인정);
                GyeonuCase.AddContradiction(ContradictionId.M2);
            }

            // ⚠️ 말로만 하는 허세는 여기로 들어오지 않는다 (문서 「12. 암행어사 신분」).
            //    페르소나가 "증표를 실제로 내밀지 않았으면 [신분:증명]을 쓰지 마라"를 못 박는다.
            // 신분 표식은 AI 의도일 뿐이다. 실제 마패 소지품 Present 경로만 RevealIdentity를 부를 수 있다.

            // ── 단서 번호가 없는 비밀 한 조각 ──────────────────────
            //    아이02의 개구멍, 최초의 견우가 아는 관아 통로. 둘 다 종막 진입의 열쇠인데
            //    A~C 어느 계열에도 번호가 없다 — 플래그로만 남기고 모션 연출을 함께 깨운다.
            if (DialogueGrantValidator.CanRevealSecret(Profile, tone, playerMessage, answerWithMarkers))
            {
                if (!GyeonuCase.HasFlag(Profile.secretFlag))
                {
                    GyeonuCase.SetFlag(Profile.secretFlag);
                    Debug.Log("[대화] " + Profile.displayName + " → 비밀 " + Profile.secretFlag);
                }
                SecretTold?.Invoke();
            }

            // ── 고마움 ────────────────────────────────────────────
            //    어머니와 최초의 직녀의 Thank는 랜덤이 아니라 필수 반응이다 (문서 「29·30」).
            //    어머니는 이 순간이 곧 무례 3회의 <b>유일한</b> 회복 경로이기도 하다 (문서 「13」).
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"[\[\(【]\s*고마움\s*[\]\)】]"))
            {
                GyeonuCase.RecoverNpc(Profile.npcId);
                Thanked?.Invoke();
            }

            // ── 노래 청하기 ─────────────────────────────────────
            //    아이들이 [노래] 를 적으면 실제 노래(V01)가 아이들 자리에서 난다. 플레이어가 정말
            //    노래를 청했을 때만 통과시킨다 — AI가 제멋대로 부르기 시작하면 안 된다.
            if (DialogueGrantValidator.CanSing(Profile, playerMessage, answerWithMarkers))
            {
                Debug.Log("[대화] " + Profile.displayName + " → 노래를 청함");
                SongRequested?.Invoke();
            }

            // 떼어 낸다 — 플레이어에게는 대사만 보여야 한다.
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"[\[\(【]\s*(단서|화제|모순|신분|비밀|고마움|노래)\s*[:：]?\s*[^\]\)】]*[\]\)】]", "").Trim();
            // 닫히지 않은 내부 표식도 줄 끝까지만 보수적으로 제거한다.
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"(?im)^\s*[\[【(]?\s*(단서|화제|모순|신분|비밀|고마움|노래)\s*[:：][^\r\n\]】)]*[\]】)]?\s*$", "").Trim();
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"(?im)^\s*[\[【(]\s*(단서|화제|모순|신분|비밀|고마움|노래)\s*$", "").Trim();
        }

        static IEnumerable<string> Matches(string text, string pattern)
        {
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(text, pattern))
                yield return m.Groups[1].Value;
        }

        static AlertTopic TopicOf(string word)
        {
            switch (word)
            {
                case "실종": return AlertTopic.Disappearance;
                case "아버지": return AlertTopic.FatherCase;
                case "다리": return AlertTopic.BridgeOrGate;
                case "서고": return AlertTopic.Archive;
                default: return AlertTopic.Ordinary;
            }
        }

        /// <summary>등급을 점수로 옮긴다. 어느 축이 움직이는지는 상대가 정한다.</summary>
        void Report(TalkTone tone)
        {
            if (Profile.npcId == NpcId.Gyeonu)
            {
                GyeonuCase.ReportGyeonuTone(tone);
                return;
            }
            // 견우 밖의 인물은 아직 점수축이 붙지 않았다 — 무례만 센다 (문서 「13. 다른 NPC의 태도」).
            if (tone == TalkTone.Insult) GyeonuCase.ReportNpcInsult(Profile.npcId);
        }

        /// <summary>
        /// 응답 첫 줄의 [등급:…] 을 떼어 내고 등급을 돌려준다.
        /// 표식이 없으면 <see cref="TalkTone.Neutral"/> — 판정 실패가 벌이 되면 안 된다.
        /// </summary>
        public static TalkTone ParseTone(ref string text)
        {
            if (string.IsNullOrEmpty(text)) return TalkTone.Neutral;
            var m = System.Text.RegularExpressions.Regex.Match(
                text, @"^\s*[\[\(【]\s*등급\s*[:：]?\s*(호의|중립|압박|모욕)\s*[\]\)】]\s*");
            if (!m.Success) return TalkTone.Neutral;
            text = text.Substring(m.Length).TrimStart();
            switch (m.Groups[1].Value)
            {
                case "호의": return TalkTone.Favor;
                case "압박": return TalkTone.Pressure;
                case "모욕": return TalkTone.Insult;
                default: return TalkTone.Neutral;
            }
        }

        /// <summary>
        /// 이번 턴에 AI가 알아야 할 사건 사정. 공통 연동의 '밝혀진 사실' 자리에 실린다 —
        /// 그 앞에 "여기 없는 핵심 비밀은 먼저 말하지 마라"가 이미 붙어 있다.
        /// </summary>
        List<string> BuildFacts(string presentedLabel)
        {
            var facts = new List<string>();

            // 문서 「13. 다른 NPC의 태도」 — 견우 밖에는 수치가 없고 무례 3회로만 닫힌다.
            // 회복 조건은 인물마다 다르므로 <see cref="GyeonuCase.RecoverNpc"/> 를 부르는 쪽은
            // 그 조건을 아는 곳이다 (어머니는 Thank 이벤트, 상인·아이는 시간대 교체).
            bool hostile = Profile.npcId != NpcId.Gyeonu && GyeonuCase.NpcHostile(Profile.npcId);

            if (Profile.useTrustBands)
            {
                int trust = GyeonuCase.Trust;
                bool locked = Profile.npcId == NpcId.Gyeonu && GyeonuCase.GyeonuLocked;
                string att = Profile.AttitudeFor(trust, locked);
                if (!string.IsNullOrEmpty(att))
                    facts.Add("[지금의 태도] " + att.Replace("\n", " "));
            }
            else if (hostile && !string.IsNullOrEmpty(Profile.lockedAttitude))
            {
                facts.Add("[지금의 태도] " + Profile.lockedAttitude.Replace("\n", " "));
            }

            // 닫힌 상태에서는 조건부 태도(= 무엇까지 말해도 되는지)를 주지 않는다 — 그것이 곧 정보 차단이다.
            if (!hostile) Profile.CollectConditionalFacts(facts);

            if (!hostile && Profile.npcId == NpcId.Magistrate &&
                GyeonuCase.HasFlag(GyeonuWorld.F_수령최초알리바이) &&
                GyeonuCase.HasFlag(GyeonuWorld.F_상인수령당일목격))
                facts.Add("[M1 후보] 상대가 동헌 알리바이와 사건 당일 오작교 목격을 직접 모순으로 지적할 때만, 잠시 순찰을 돌았을 뿐이라고 말을 바꿔라.");

            if (!hostile && Profile.npcId == NpcId.Jumo &&
                GyeonuCase.HasFlag(GyeonuWorld.F_주모죄인주장) && GyeonuCase.HasClue(ClueId.C7))
                facts.Add("[M2 후보] 상대가 앞선 죄인 주장과 C7을 직접 대조해 출처를 캐물을 때만, 직접 본 것이 아니라 관아에서 들은 소문이었다고 인정하라.");

            facts.Add(GyeonuCase.Night ? "[지금] 밤이다. 마을은 어둡다." : "[지금] 낮이다.");
            if (GyeonuCase.Rain) facts.Add("[지금] 비가 내린다.");

            // 아이들 — 노래를 실제로 불렀는지, 지금 부르는 중인지 (2026-09-09). 페르소나의 「노래를 청하면」과 짝이다.
            if (!hostile && (Profile.npcId == NpcId.Child01 || Profile.npcId == NpcId.Child02 || Profile.npcId == NpcId.Child03))
            {
                if (ChildrenSong.Singing)
                    facts.Add("[노래] 지금 너희가 그 노래를 부르고 있다. 다시 불러 달라 하면 지금 부르는 중이라고만 답하고 [노래] 는 쓰지 마라.");
                else if (ChildrenSong.Sung)
                    facts.Add("[노래] 아까 너희가 그 노래를 한 번 불렀다. 불러 달라고 청했을 때만 응하고 마지막 줄에 [노래] 를 적어라. 노래에 대해 묻기만 하면 말로만 답하고 [노래] 를 쓰지 마라.");
                else
                    facts.Add("[노래] 아직 이 사람 앞에서 노래를 부르지 않았다. 불러 달라고 청했을 때만 응하고 마지막 줄에 [노래] 를 적어라. 노래에 대해 묻기만 하면 말로만 답하고 [노래] 를 쓰지 마라.");
            }

            if (!string.IsNullOrEmpty(presentedLabel))
                facts.Add("[방금 상대가 내민 것] " + presentedLabel + " — 이에 대해 아는 만큼만 반응하라.");

            return facts;
        }

        // ── 줄 쌓기 ──────────────────────────────────────────────
        void AddPlayerLine(string text)
        {
            _lines.Add(new Line { fromPlayer = true, speaker = "나", text = text });
            Changed?.Invoke();
        }

        void AddNpcLine(string text, TalkTone tone, bool graded)
        {
            _lines.Add(new Line { fromPlayer = false, speaker = Profile.displayName, text = text, tone = tone, graded = graded });
            Changed?.Invoke();
        }

        /// <summary>지금 화면 큰 자리에 띄울 NPC의 마지막 말.</summary>
        public string CurrentNpcLine
        {
            get
            {
                for (int i = _lines.Count - 1; i >= 0; i--)
                    if (!_lines[i].fromPlayer) return _lines[i].text;
                return "";
            }
        }

        // ─────────────────────────────────────────────────────────
        //  제시 목록 — 소지품 판이 그릴 것들
        // ─────────────────────────────────────────────────────────

        readonly List<InventoryItem> _presentables = new List<InventoryItem>();
        readonly List<InventoryItem> _standIns = new List<InventoryItem>();   // 정보 단서용 임시 소지품

        /// <summary>
        /// 지금 내밀 수 있는 것 전부 — <b>물건 단서와 정보 단서를 한 목록에</b> (2026-08-25).
        ///
        /// ■ 왜 정보 단서를 가짜 소지품으로 만드는가
        ///   플레이어에게 A1 지도(손에 쥔 것)와 B3 증언(들은 것)은 똑같이 "내가 아는 것"이다.
        ///   그런데 소지품 판은 <see cref="InventoryItem"/> 만 그릴 줄 안다. 판을 고쳐 두 자료형을
        ///   다루게 하면 소지품 쪽이 대화 사정을 알아야 하고, 목록을 둘로 나누면 플레이어가
        ///   "이건 왜 여기 없지"를 겪는다. 그래서 <b>정보 단서에 임시 껍데기를 씌워</b> 올린다 —
        ///   판은 여전히 물건만 그리고, 대화 쪽만 이 사정을 안다.
        ///   껍데기는 <see cref="Inventory"/> 에 넣지 않는다. 소지품은 더럽혀지지 않는다.
        /// </summary>
        public IReadOnlyList<InventoryItem> Presentables()
        {
            _presentables.Clear();
            foreach (var info in ClueTable.All)
            {
                if (!GyeonuCase.HasClue(info.id)) continue;
                string code = info.id.ToString();

                // 실물이 있으면 그것을 쓴다 — 3D로 돌려 보며 고를 수 있다
                var real = Inventory.Find(code);
                if (real != null) { _presentables.Add(real); continue; }

                _presentables.Add(StandInFor(info));
            }
            foreach (var item in Inventory.Items)
                if (item != null && IsMapae(item) && !_presentables.Contains(item)) _presentables.Add(item);
            return _presentables;
        }

        InventoryItem StandInFor(ClueTable.Info info)
        {
            string code = info.id.ToString();
            foreach (var s in _standIns) if (s != null && s.itemId == code) return s;

            var it = ScriptableObject.CreateInstance<InventoryItem>();
            it.hideFlags = HideFlags.HideAndDontSave;
            it.itemId = code;
            it.displayName = code + " " + info.title;
            it.pickupVerb = "내밀기";
            it.description = "「" + info.title + "」\n\n알게 된 곳 — " + info.source +
                             "\n\n손에 쥔 물건이 아니라 듣거나 보아서 알게 된 것이다." +
                             (GyeonuCase.HasPresented(info.id) ? "\n\n이미 한 번 내밀었다." : "");
            it.modelPrefab = null;
            it.usable = false;
            it.autoShowOnPickup = false;
            _standIns.Add(it);
            return it;
        }

        /// <summary>대화가 끝났다 — 껍데기 SO를 치운다.</summary>
        public void Dispose()
        {
            Changed = null;
            SecretTold = null;
            Thanked = null;
            SongRequested = null;
            if (_character != null) UnityEngine.Object.Destroy(_character);
            foreach (var s in _standIns) if (s != null) UnityEngine.Object.Destroy(s);
            _standIns.Clear();
            _presentables.Clear();
        }

        /// <summary>로그로 남길 전체 대화.</summary>
        public string Dump()
        {
            var sb = new StringBuilder();
            foreach (var l in _lines) sb.AppendLine((l.fromPlayer ? "나: " : l.speaker + ": ") + l.text);
            return sb.ToString();
        }

        // ── IDialogueBackend ─────────────────────────────────
        //
        // 대화창이 묻는 것을 우리 것에 이어 준다 (2026-08-26).
        // 화면은 이제 DialogueSession 도 Gemini 도 GyeonuCase 도 모른다 — 이 인터페이스만 안다.
        // <c>Busy</c>·<c>Changed</c>·<c>Ask</c> 는 이름과 형이 이미 맞아 그대로 쓰인다.

        string IDialogueBackend.SpeakerName => Profile != null ? Profile.displayName : "";
        string IDialogueBackend.CurrentLine => CurrentNpcLine;

        /// <summary><c>IReadOnlyList</c> 는 공변이지만 인터페이스 구현은 형이 정확히 맞아야 해서 감싼다.</summary>
        IReadOnlyList<IUiItem> IDialogueBackend.Presentables() => Presentables();

        /// <summary>
        /// 증거를 내민다.
        ///
        /// ⚠️ <b>이 갈래가 예전에는 대화창 안에 있었다</b> (2026-08-26에 여기로 내렸다).
        ///    화면이 <see cref="ClueTable"/> 을 알면 안 되기 때문이다 — 단서 코드는 사건의 것이다.
        ///    <b>판정 순서와 결과는 예전 그대로다</b>: 먼저 정보 단서인지 보고(코드로 파싱되면
        ///    <see cref="Present(ClueId)"/>), 아니면 물건으로 내민다. 둘 다 아니면 false 가 나가고
        ///    화면이 「이건 내밀 것이 못 된다」를 띄운다.
        /// </summary>
        bool IDialogueBackend.Present(IUiItem item)
        {
            if (item == null) return false;
            ClueId id;
            if (ClueTable.TryParse(item.Key, out id)) { Present(id); return true; }
            var inv = item as InventoryItem;
            return inv != null && PresentItem(inv);
        }
    }
}
