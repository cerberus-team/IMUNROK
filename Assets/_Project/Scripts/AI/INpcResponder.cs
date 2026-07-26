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
