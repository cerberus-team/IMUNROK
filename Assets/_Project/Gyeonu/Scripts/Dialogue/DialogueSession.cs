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
    public class DialogueSession
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

        /// <summary>줄이 하나 늘었다 — 화면이 다시 그린다.</summary>
        public event Action Changed;

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
        }

        /// <summary>공통 연동과 <b>같은 자리</b>의 키 파일을 본다 (프로젝트 루트, .gitignore 등재).</summary>
        public static bool HasApiKey
        {
            get
            {
                try
                {
                    string path = Path.Combine(Application.dataPath, "..", "gemini_api_key.txt");
                    return File.Exists(path) && File.ReadAllText(path).Trim().Length > 0;
                }
                catch { return false; }
            }
        }

        // ─────────────────────────────────────────────────────────
        //  ① 말하기
        // ─────────────────────────────────────────────────────────
        public void Ask(string text)
        {
            if (Busy || string.IsNullOrWhiteSpace(text)) return;
            string say = text.Trim();
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

            if (Profile.useTrustBands)
            {
                int trust = GyeonuCase.Trust;
                bool locked = Profile.npcId == NpcId.Gyeonu && GyeonuCase.GyeonuLocked;
                string att = Profile.AttitudeFor(trust, locked);
                if (!string.IsNullOrEmpty(att))
                    facts.Add("[지금의 태도] " + att.Replace("\n", " "));
            }

            facts.Add(GyeonuCase.Night ? "[지금] 밤이다. 마을은 어둡다." : "[지금] 낮이다.");
            if (GyeonuCase.Rain) facts.Add("[지금] 비가 내린다.");

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
    }
}
