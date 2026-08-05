using System;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 가짜(목업) 대답기 — 미리 정한 규칙으로 NPC 대사를 만든다. 무료·오프라인·헤드셋/키 불필요.
    /// 내일 ClaudeNpcResponder(실제 AI)로 이 자리를 교체한다.
    ///
    /// 규칙:
    ///  - 증거가 사실을 방금 열었으면(justRevealedInfo) → 그 사실을 실토.
    ///  - 그 외 증거 제시 → "상관없다"고 버팀.
    ///  - 그냥 질문 → 능청스러운 회피(돌아가며).
    /// </summary>
    public class MockNpcResponder : INpcResponder
    {
        private static readonly string[] Deflections =
        {
            "글쎄올시다, 소인은 모르는 일이오.",
            "그건 대답하기 어렵소, 어사또.",
            "무얼 근거로 그런 말씀을 하시오?",
            "소인은 떳떳하오. 무엇이 문제란 말이오?",
        };

        public void GetResponse(MonoBehaviour host, NpcRequest req, Action<string> onReply, Action<string> onError)
        {
            string reply;

            if (!string.IsNullOrEmpty(req.justRevealedInfo))
            {
                // 결정적 증거를 제시받음 → 마지못해 실토(또는 발뺌 대사)
                reply = req.justRevealedInfo;
            }
            else if (!string.IsNullOrEmpty(req.scriptedAnswer))
            {
                // 추천 질문의 정해진 답
                reply = req.scriptedAnswer;
            }
            else if (req.isEvidence)
            {
                reply = "그 증거는… 이 일과 상관없소.";
            }
            else
            {
                int i = (req.transcript != null ? req.transcript.Count : 0) % Deflections.Length;
                reply = Deflections[i];
            }

            // 목업은 즉시 대답(네트워크 없음)
            onReply?.Invoke(reply);
        }
    }
}
