using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 심문 한 턴에 필요한 정보 묶음(대답을 만드는 쪽에 넘겨준다).
    /// </summary>
    public class NpcRequest
    {
        public InterrogationCharacter character;   // 누구인지(성격·비밀)
        public List<string> transcript;            // 지금까지 대화 줄들
        public List<string> unlockedFacts;         // 지금까지 열린 사실
        public string playerInput;                 // 방금 플레이어가 한 말(또는 제시한 증거 설명)
        public bool isEvidence;                    // 이번이 증거 제시인가
        public string justRevealedInfo;            // 이번 증거로 방금 열린 사실(있으면). 없으면 null
        public string scriptedAnswer;              // 추천 질문의 정해진 답(목업/폴백에서 사용). 없으면 null

        // 플레이어가 지금 누구로 보이는가(1막은 과객, 2막부터 어사). GameState가 정한다.
        public string playerTitle = "나그네";        // 대화 기록에 찍히는 이름표
        public string playerIdentityBrief = "";     // AI에게 주는 한 줄 설명

        // ── 지금 상태 — 무너짐을 <b>두 겹</b>으로 가르기 위한 것 ──────────
        //
        // AI 혼자 무너질 때를 정하면 말을 잘 지어내는 사람에게만 사건이 열리고,
        // 반대로 정해 둔 문구를 맞춰야 열리면 「복동아」와 「네 이름 복동이지?」가
        // 다른 말이 되어 답답해진다. 그래서 <b>열쇠를 쥐었는가</b>는 게임이 정해
        // 여기 실어 보내고, <b>정곡을 찔렀는가</b>는 AI가 뜻으로 읽는다.

        // 종이를 들이민 것도 말로 찌른 것도 아닌 <b>벌어진 일</b>인가.
        // 상 위의 게장이 앞으로 밀려 왔다거나, 팔에 놋쇠 자가 닿았다거나.
        // 이것을 안 가르면 아무도 문서를 안 꺼냈는데 「그 종이를 보니」라고 답한다.
        public bool isDeed;

        public bool identityRevealed;              // 2막인가(어사임을 아는가)
        public int stage;                          // 무너짐 단계 0~10
        public bool canBreak;                      // 마지막 열쇠를 다 쥐었는가
        public List<string> evidenceHeld;          // 어사가 지금 쥔 단서 줄들(제시했다는 뜻은 아니다)
    }

    /// <summary>
    /// "NPC의 대답을 만들어 주는 것"의 공통 계약(= 인형극의 목소리 구멍).
    /// 오늘은 MockNpcResponder(녹음테이프)가, 내일은 ClaudeNpcResponder(실제 AI)가
    /// 이 인터페이스를 구현한다. 무대(InterrogationController)는 어느 쪽이든 똑같이 호출한다.
    ///
    /// 비동기(네트워크) 대비: 결과를 바로 반환하지 않고 onReply 콜백으로 돌려준다.
    /// host는 코루틴(네트워크 대기)을 돌릴 수 있는 MonoBehaviour(내일 Claude가 사용).
    /// </summary>
    public interface INpcResponder
    {
        void GetResponse(MonoBehaviour host, NpcRequest req, Action<string> onReply, Action<string> onError);
    }
}
