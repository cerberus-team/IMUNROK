// 팀 대화창(IMUNROK.Ui.DialogueUI)에 서천 AI 를 물리는 어댑터. ★화면은 팀 것을 그대로 쓴다.
using System;
using System.Collections.Generic;
using IMUNROK.Seocheon.AI;
using IMUNROK.Ui;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 서천 말 상대 — 팀 <see cref="IDialogueBackend"/> 뒤에 <see cref="SeocheonGeminiResponder"/> 를 붙인 것.
    ///
    /// ■ 왜 어댑터인가
    ///   README §4 ③ 이 시키는 방식이다. 화면(1,500줄)은 팀 것을 <b>그대로</b> 쓰고
    ///   우리는 <b>일곱 가지</b>만 답한다. 서천 전용 대화창을 또 만들지 않는다.
    ///
    /// ■ 서천 응답은 문장이 여럿인데 팀 화면은 한 덩이다
    ///   <see cref="SeocheonReplyResult.sentences"/> 를 줄바꿈으로 이어 <see cref="CurrentLine"/> 에 넣는다.
    ///   팀 대사 상자는 넘치면 <b>굴려 읽기</b>라 잘리지 않는다.
    ///   ★어절 후보(options)는 <b>합친 글 기준으로 다시 매핑</b>해 <see cref="PickSource"/> 에 담는다 —
    ///     원본은 <see cref="LastResult"/> 에 그대로 남는다.
    ///
    /// ■ 증거 제시
    ///   서천에는 소지품 시스템이 없다. <see cref="Presentables"/> 가 빈 목록을 주면
    ///   팀 화면이 알아서 <b>내밀 것이 없다</b>고 처리한다 — 단추를 가짜로 두지 않는다.
    ///
    /// ■ 서천에만 있는 둘 (2026-08-27, <see cref="ISeocheonBarSource"/>)
    ///   ① <b>선택지 4개</b> — VR 에서는 글쇠를 칠 수 없어 이것이 기본 입력이다.
    ///   ② <b>어절 지목</b> — 대사에서 낱말을 짚어 수첩에 적는다.
    ///   둘 다 견우 <see cref="IDialogueBackend"/> 에는 없는 것이라 계약을 <b>하나 더</b> 구현한다.
    /// </summary>
    public sealed class SeocheonUiBackend : IDialogueBackend, ISeocheonBarSource
    {
        private readonly SeocheonNpcData npc;
        private readonly SeocheonGeminiResponder responder;
        private readonly MonoBehaviour host;

        private readonly List<string> transcript = new List<string>();
        private readonly List<string> collected = new List<string>();
        private static readonly IUiItem[] NoItems = new IUiItem[0];

        private string line = string.Empty;
        private bool busy;

        // ── ★서천 전용 ────────────────────────────────────
        private readonly List<SeocheonAsk> asks = new List<SeocheonAsk>();
        private WordPickNote.Sentence pick;

        /// <summary>어절이 원래 어느 문장의 것이었나 — ★수첩에는 <b>그 문장</b>이 적힌다.</summary>
        private readonly Dictionary<WordPickNote.WordOption, string> sentenceOf =
            new Dictionary<WordPickNote.WordOption, string>();

        /// <summary>마지막에 고른 선택지의 결(probe/press/idle/direct). ★화면에는 드러내지 않는다.</summary>
        public string LastAskTone { get; private set; }

        /// <summary>가장 마지막 AI 응답 원본. ★어절 후보가 여기 살아 있다(다음 라운드용).</summary>
        public SeocheonReplyResult LastResult { get; private set; }

        /// <summary>지금까지의 대화. 프로필 만들기·회고에 쓴다.</summary>
        public IReadOnlyList<string> Transcript { get { return transcript; } }

        public SeocheonUiBackend(SeocheonNpcData npcData, SeocheonAiConfig aiConfig, MonoBehaviour coroutineHost)
        {
            npc = npcData;
            host = coroutineHost;
            responder = new SeocheonGeminiResponder(aiConfig);
            if (npc != null)
            {
                line = npc.openingLine;
                transcript.Add(npc.npcName + ": " + npc.openingLine);
            }
            FillAsks(null);      // 첫 물음은 NPC 데이터의 고정 질문으로 채운다
        }

        // ── IDialogueBackend ─────────────────────────────
        public string SpeakerName { get { return npc != null ? npc.npcName : string.Empty; } }
        public string CurrentLine { get { return line; } }
        public bool Busy { get { return busy; } }
        public event Action Changed;

        public void Ask(string text)
        {
            if (busy || string.IsNullOrEmpty(text) || npc == null) return;

            transcript.Add("나: " + text);
            busy = true;
            Raise();

            RefreshCollected();
            responder.GetReply(host, npc, transcript, collected, text, OnReply);
        }

        /// <summary>서천에는 내밀 물건이 없다 — 언제나 거절한다.</summary>
        public bool Present(IUiItem item) { return false; }

        public IReadOnlyList<IUiItem> Presentables() { return NoItems; }

        // ── ISeocheonBarSource ───────────────────────────
        public IReadOnlyList<SeocheonAsk> Asks { get { return asks; } }
        public WordPickNote.Sentence PickSource { get { return pick; } }

        /// <summary>★선택지도 자유 입력과 <b>같은 길</b>로 간다 — 화면은 어느 쪽인지 모른다.</summary>
        public void ChooseAsk(int index)
        {
            if (index < 0 || index >= asks.Count) return;
            SeocheonAsk a = asks[index];
            if (a == null || string.IsNullOrEmpty(a.label)) return;
            LastAskTone = a.tone;
            Ask(a.label);
        }

        /// <summary>
        /// ★어절을 지목했다. 유효/오답을 <b>여기서 판정하지 않는다</b>.
        ///
        /// - 수첩에는 어느 쪽이든 <b>그 어절이 들어 있던 문장 전체</b>가 같은 형식의 key 로 들어간다.
        /// - clueId 는 저장소에만 남고 화면에는 나오지 않는다.
        /// - 성공/실패를 알리는 문구·색·소리를 내지 않는다.
        /// </summary>
        public void NotifyWordPicked(WordPickNote.WordOption option)
        {
            if (option == null) return;
            string sentence;
            if (!sentenceOf.TryGetValue(option, out sentence) || string.IsNullOrEmpty(sentence))
                sentence = option.word;

            SeocheonClueStore.Add(SpeakerName, sentence, option.word, option.clueId);
            RefreshCollected();
        }

        // ── 안쪽 ─────────────────────────────────────────
        private void OnReply(SeocheonReplyResult result)
        {
            busy = false;
            LastResult = result;

            if (result == null || result.sentences.Count == 0)
            {
                line = "…";
                pick = null;
                FillAsks(result);
                Raise();
                return;
            }

            // ★화면은 문장 여럿을 <b>한 덩이</b>로 그린다. 어절 자리를 잡으려면
            //   짚을 문장도 <b>화면과 똑같이</b> 이어 붙여야 글자 번호가 맞는다
            //   (<see cref="ISeocheonBarSource.PickSource"/> 주석 참고).
            var sb = new System.Text.StringBuilder();
            var opts = new List<WordPickNote.WordOption>();
            sentenceOf.Clear();

            for (int i = 0; i < result.sentences.Count; i++)
            {
                WordPickNote.Sentence sn = result.sentences[i];
                if (sn == null || string.IsNullOrEmpty(sn.text)) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(sn.text);
                transcript.Add(npc.npcName + ": " + sn.text);
                CollectOptions(sn, opts);
            }

            line = sb.ToString();
            pick = new WordPickNote.Sentence { text = line, options = opts.ToArray() };
            FillAsks(result);
            Raise();
        }

        /// <summary>
        /// 한 문장의 어절 후보를 합친 목록에 담는다.
        ///
        /// ⚠️ <b>같은 낱말이 둘이면 앞의 것만</b> 담는다. 판정기가 낱말을 문장에서
        ///    <c>IndexOf</c> 로 찾으므로 자리가 <b>어차피 하나뿐</b>이다 — 둘을 담으면
        ///    두 후보가 같은 자리를 가리켜 어느 쪽이 짚혔는지 알 수 없다.
        /// </summary>
        private void CollectOptions(WordPickNote.Sentence sn, List<WordPickNote.WordOption> opts)
        {
            if (sn.options == null) return;
            for (int j = 0; j < sn.options.Length; j++)
            {
                WordPickNote.WordOption o = sn.options[j];
                if (o == null || string.IsNullOrEmpty(o.word)) continue;
                if (sn.text.IndexOf(o.word, StringComparison.Ordinal) < 0) continue;   // 문장에 없는 어절
                if (HasWord(opts, o.word)) continue;
                opts.Add(o);
                sentenceOf[o] = sn.text;          // ★수첩에는 <b>원래 문장</b>이 적힌다(합친 것이 아니라)
            }
        }

        private static bool HasWord(List<WordPickNote.WordOption> list, string word)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].word == word) return true;
            return false;
        }

        /// <summary>AI 가 준 선택지를 올린다. 없으면 NPC 데이터의 고정 질문으로 채운다.</summary>
        private void FillAsks(SeocheonReplyResult result)
        {
            asks.Clear();
            if (result != null && result.asks != null && result.asks.Count > 0)
            {
                for (int i = 0; i < result.asks.Count && asks.Count < 4; i++)
                    if (result.asks[i] != null && !string.IsNullOrEmpty(result.asks[i].label))
                        asks.Add(result.asks[i]);
                if (asks.Count > 0) return;
            }

            if (npc == null || npc.fallbackAsks == null) return;
            for (int i = 0; i < npc.fallbackAsks.Length && asks.Count < 4; i++)
            {
                if (string.IsNullOrEmpty(npc.fallbackAsks[i])) continue;
                asks.Add(new SeocheonAsk { label = npc.fallbackAsks[i], tone = "probe" });
            }
        }

        /// <summary>이미 모은 조각을 알려 준다 — ★재제시 금지 게이팅용. 저장소를 고치지는 않는다.</summary>
        private void RefreshCollected()
        {
            SeocheonClueStore.CollectClueIds(collected);
        }

        private void Raise() { if (Changed != null) Changed(); }
    }
}
