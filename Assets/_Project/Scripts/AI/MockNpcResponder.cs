using System;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 가짜(목업) 대답기 — 미리 정한 규칙으로 NPC 대사를 만든다. 무료·오프라인·열쇠 불필요.
    ///
    /// <b>여기는 예비 자리가 아니라 자주 서는 자리다.</b> 열쇠 파일이 없는 PC 에서는
    /// <see cref="GeminiNpcResponder"/> 가 조용히 이쪽으로 넘긴다. 씬의 인물은 모두
    /// Gemini 로 맞춰져 있으므로, 시연하는 사람의 컴퓨터에 열쇠가 없으면 <b>이 파일이
    /// 그 사건의 모든 대사</b>다. 그러니 여기서 어긋나는 것은 팀원 PC 에서만 나타난다.
    ///
    /// 규칙:
    ///  - 벌어진 일(<c>isDeed</c>) → 지문은 <b>그대로 읽지 않는다</b>. 인물이 할 만한 한 마디로 받는다.
    ///  - 증거가 사실을 방금 열었으면(justRevealedInfo) → 그 사실을 실토.
    ///  - 그 외 증거 제시 → "상관없다"고 버팀.
    ///  - 그냥 질문 → 능청스러운 회피(돌아가며). 막에 따라 말씨가 갈린다.
    /// </summary>
    public class MockNpcResponder : INpcResponder
    {
        // 1막 — 묻는 이는 하룻밤 청하는 과객이다. 경계가 없고 말이 가볍다.
        private static readonly string[] DeflectionsAct1 =
        {
            "글쎄올시다, 나는 모르는 일이오.",
            "그건 대답하기 어렵소.",
            "무얼 근거로 그런 말씀을 하시오?",
            "허, 손님도 별걸 다 물으시는구려.",
        };

        // 2막 — 어사 앞이다. 같은 회피라도 말이 짧고 조심스럽다.
        private static readonly string[] DeflectionsAct2 =
        {
            "소인은 아무것도 모릅니다.",
            "그런 일은 없었습니다.",
            "무엇을 근거로 그리 말씀하시옵니까.",
            "…….",
        };

        public void GetResponse(MonoBehaviour host, NpcRequest req, Action<string> onReply, Action<string> onError)
        {
            string reply;
            var pool = req.identityRevealed ? DeflectionsAct2 : DeflectionsAct1;
            // 인물마다 적어 둔 말투가 있으면 그것이 먼저다 — 양반 행세하는 甲 이 「소인」이라 하던 것
            if (req.character != null)
            {
                var own = req.identityRevealed ? req.character.mockDeflectAct2 : req.character.mockDeflectAct1;
                if (own != null && own.Length > 0) pool = own;
            }

            if (req.isDeed)
            {
                // <b>지문을 그대로 읽으면 안 된다.</b> 이 칸에 적힌 것은 「자가 팔에 닿는다.
                // 팔을 빼려다 만다」 같은 <b>3인칭 서술</b>이라, 그대로 뱉으면 인물이 제 입으로
                // 제 동작을 중계한다. Gemini 쪽은 이것을 [벌어진 일] 로 따로 일러 두는데
                // (GeminiNpcResponder), 여기에는 그 갈래가 없으므로 <b>말을 잃은 반응</b>으로 받는다.
                string said = Spoken(req.justRevealedInfo);
                reply = said ?? (req.canBreak ? "…어찌… 그것을…" : (req.stage >= 6 ? "……." : "무슨 짓이오."));
            }
            else if (!string.IsNullOrEmpty(req.scriptedAnswer))
            {
                // <b>사람 말이 먼저다</b>(2026-09-15). 여태 열린 사실(수첩 문구)을 먼저 보아, 인물이
                // 「게장 이야기가 나오자 말이 막힌다」 같은 3인칭 서술을 제 입으로 읽었다.
                reply = req.scriptedAnswer;
            }
            else if (!string.IsNullOrEmpty(req.justRevealedInfo))
            {
                // 수첩 문구에서 사람이 한 말(「」 안)만 꺼낸다. 없으면 말을 잃은 반응
                reply = Spoken(req.justRevealedInfo) ?? (req.stage >= 6 ? "…그, 그것은……" : "……허.");
            }
            else if (req.isEvidence)
            {
                reply = req.identityRevealed ? "그 종이는… 소인과 상관없는 일입니다." : "그 증거는… 이 일과 상관없소.";
            }
            else
            {
                int i = (req.transcript != null ? req.transcript.Count : 0) % pool.Length;
                reply = pool[i];
            }

            // 목업은 즉시 대답(네트워크 없음)
            onReply?.Invoke(reply);
        }

        private static readonly System.Text.RegularExpressions.Regex Quote =
            new System.Text.RegularExpressions.Regex("「([^」]+)」");

        /// <summary>
        /// 적어 둔 문구 가운데 <b>사람이 입으로 한 말</b>. 「」 안이 있으면 그것을, 없으면 서술이 아닌
        /// 말투(…오/…소/…다 로 끝나는 짧은 말)일 때만 그대로. 서술(걷는다·막힌다)이면 null.
        /// </summary>
        private static string Spoken(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var ms = Quote.Matches(text);
            if (ms.Count > 0)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (System.Text.RegularExpressions.Match m in ms) parts.Add(m.Groups[1].Value);
                return string.Join(" ", parts.ToArray());
            }
            string t = text.Trim();
            if (t.StartsWith("무너져 실토한다")) return null;
            string[] narr = { "걷는다", "드러난다", "막힌다", "떨린다", "굳는다", "흐린다", "다문다", "숙인다", "있다.", "없다." };
            foreach (var n in narr) if (t.Contains(n)) return null;
            return t.Length <= 80 ? t : null;
        }
    }
}
