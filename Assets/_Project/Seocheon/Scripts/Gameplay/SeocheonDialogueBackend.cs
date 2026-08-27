using System;
using System.Collections.Generic;
using UnityEngine;
using IMUNROK.Ui;
using IMUNROK.Seocheon.AI;   // SeocheonGeminiResponder · SeocheonNpcData · SeocheonReplyResult · SeocheonAsk

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 서천의 대화 알맹이를 견우 대화창(<see cref="DialogueUI"/>)에 물리는 다리 (2026-08-26).
    ///
    /// ■ 왜 이렇게 하는가
    ///   화면은 견우 것으로 통일한다(글꼴·색·판 치수는 팀 규약이라 손대지 않는다).
    ///   그런데 서천의 알맹이 — <see cref="SeocheonGeminiResponder"/> 834줄 — 은 그대로 쓴다.
    ///   대화창이 실제로 묻는 것은 <see cref="IDialogueBackend"/> 일곱 가지뿐이므로
    ///   그 일곱만 채워 주면 된다. 화면은 Gemini도 워드픽도 <b>모른다</b>.
    ///
    /// ■ 서천에만 있는 것은 여기서 흘려보낸다
    ///   견우 대화창에는 <b>선택지</b>와 <b>워드픽</b>이 없다. 그 둘은
    ///   <see cref="SeocheonDialogueExtras"/> 가 판 위에 얹어 그리는데,
    ///   그리는 데 필요한 재료(<see cref="LastSentences"/> · <see cref="LastAsks"/>)를
    ///   이 다리가 내어 준다.
    ///
    /// ⚠️ 꽂는 일은 세션마다 다시 해야 한다 — 이 프로젝트 규약은
    ///    <see cref="RuntimeInitializeOnLoadMethod"/> 다 (꾸러미 문서 §4).
    /// </summary>
    public sealed class SeocheonDialogueBackend : IDialogueBackend
    {
        private readonly MonoBehaviour host;
        private readonly SeocheonNpcData npc;
        private readonly SeocheonGeminiResponder responder;
        private readonly List<string> transcript;
        private readonly List<string> collectedClueIds;

        private string currentLine = string.Empty;
        private bool busy;

        /// <summary>마지막 대꾸의 문장들 — 워드픽이 이 단위로 낱말을 짚는다.</summary>
        public IReadOnlyList<WordPickNote.Sentence> LastSentences { get { return lastSentences; } }
        private List<WordPickNote.Sentence> lastSentences = new List<WordPickNote.Sentence>();

        /// <summary>마지막 대꾸가 딸려 보낸 되물음 후보 — 선택지 단추가 된다.</summary>
        public IReadOnlyList<SeocheonAsk> LastAsks { get { return lastAsks; } }
        private List<SeocheonAsk> lastAsks = new List<SeocheonAsk>();

        /// <summary>대꾸가 새로 왔다 — 선택지·워드픽을 다시 그리라는 신호.</summary>
        public event Action ReplyArrived;

        public SeocheonDialogueBackend(MonoBehaviour host, SeocheonNpcData npc,
                                       SeocheonGeminiResponder responder,
                                       List<string> transcript, List<string> collectedClueIds)
        {
            this.host = host;
            this.npc = npc;
            this.responder = responder;
            this.transcript = transcript ?? new List<string>();
            this.collectedClueIds = collectedClueIds ?? new List<string>();
            currentLine = npc != null ? npc.openingLine : string.Empty;
        }

        // ── IDialogueBackend ──────────────────────────────────────────────
        public string SpeakerName { get { return npc != null ? npc.npcName : "이름 없는 사람"; } }
        public string CurrentLine { get { return currentLine; } }
        public bool Busy { get { return busy; } }
        public event Action Changed;

        public void Ask(string text)
        {
            if (busy || responder == null || string.IsNullOrEmpty(text)) return;

            busy = true;
            Raise();                                   // 화면이 「…」를 띄우고 입력을 막는다

            responder.GetReply(host, npc, transcript, collectedClueIds, text, result =>
            {
                busy = false;
                Apply(result);
            });
        }

        /// <summary>
        /// ★서천에는 <b>증거 제시가 없다.</b> 늘 false 를 돌려 단추를 죽여 둔다.
        ///
        /// 서천의 단서 흐름은 <b>대사에서 낱말을 지목해 모으고(워드픽) → 조합하고(ClueCombiner)
        /// → 카드로 본다(SeocheonCardScreen)</b> 이다. NPC 에게 내미는 단계가 아예 없다.
        /// <see cref="SeocheonNpcData"/> 주석의 EvidenceGate 는 <b>공통 얼개 설명</b>이고,
        /// 서천은 그것을 쓰지 않는다 — 모은 단서는 AI 에게 "이미 준 것"을 알려
        /// 재제시를 막는 데(<see cref="SeocheonDialogue"/>) 쓸 뿐이다.
        ///
        /// ⚠️ 나중에 제시 규칙이 생기면 여기와 <see cref="Presentables"/> 만 채우면 된다 —
        ///    화면(견우 대화창)은 고칠 것이 없다.
        /// </summary>
        public bool Present(IUiItem item) { return false; }

        /// <summary>내밀 수 있는 것 — <b>없다</b> (위 <see cref="Present"/> 주석 참고).
        /// 빈 목록을 주면 견우 대화창이 「증거 제시」 단추를 눌러도 열 것이 없다.</summary>
        public IReadOnlyList<IUiItem> Presentables() { return System.Array.Empty<IUiItem>(); }

        // ── 내부 ──────────────────────────────────────────────────────────
        private void Apply(SeocheonReplyResult result)
        {
            lastSentences.Clear();
            lastAsks.Clear();

            if (result != null)
            {
                if (result.sentences != null) lastSentences.AddRange(result.sentences);
                if (result.asks != null) lastAsks.AddRange(result.asks);
            }

            // 화면에 보일 한 덩어리 — 문장들을 이어 붙인다.
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lastSentences.Count; i++)
            {
                var s = lastSentences[i];
                if (s == null || string.IsNullOrEmpty(s.text)) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(s.text);
            }
            currentLine = sb.ToString();

            Raise();
            if (ReplyArrived != null) ReplyArrived();
        }

        private void Raise() { if (Changed != null) Changed(); }
    }
}
