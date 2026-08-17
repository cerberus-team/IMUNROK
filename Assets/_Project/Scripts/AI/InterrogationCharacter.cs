using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 증거로 잠금 해제되는 사실 하나. clueKey(수첩 단서)를 제시하면 revealsInfo가 열린다.
    /// </summary>
    [System.Serializable]
    public class EvidenceGate
    {
        [Tooltip("이 사실을 여는 수첩 단서의 key")]
        public string clueKey = "";

        [Tooltip("수첩/제시 목록에 보이는 단서 문구")]
        public string clueText = "";

        [Tooltip("이 증거를 제시하면 인물이 털어놓는 사실")]
        [TextArea(2, 4)]
        public string revealsInfo = "";

        [Tooltip("체크하면: 위 대사는 '발뺌'일 뿐 — 단서로 기록되지 않고 여러 번 반복 가능(1막 甲처럼 안 무너짐)")]
        public bool deflectionOnly = false;

        [Tooltip("이 증거를 제시할 때 뜨는 상황 그림(선택)")]
        public Texture2D clueImage;
    }

    /// <summary>
    /// 추천 질문 하나(=대화 버튼). 플레이어가 고르면 어사가 이 질문을 하고, 인물이 답한다.
    /// grantsClueKey가 있으면 이 대화로 단서가 수첩에 기록된다(예: 甲의 자랑 → J04).
    /// </summary>
    [System.Serializable]
    public class TopicQuestion
    {
        [Tooltip("버튼에 뜨는 질문(어사의 대사)")]
        public string question = "";

        [Tooltip("목업(키 없을 때)에서 인물이 하는 대답. Gemini면 AI가 생성")]
        [TextArea(2, 4)]
        public string mockAnswer = "";

        [Tooltip("이 대화로 얻는 단서 key(선택). 비우면 단서 없음(분위기용)")]
        public string grantsClueKey = "";

        [Tooltip("그 단서의 수첩 문구")]
        public string grantsClueText = "";

        [Tooltip("이 단서의 상황 그림(선택). 수첩 카드·증거 제시 때 뜸")]
        public Texture2D grantsClueImage;
    }

    /// <summary>
    /// 심문할 인물 한 명의 데이터. 사건 팀원이 코드 없이 인스펙터로 작성한다.
    /// (틀은 공통, 알맹이는 사건 — GameState/Journal과 같은 방식)
    ///
    /// - persona: 인물의 성격·아는 사실·말투 (AI에게 주는 지침이자, 목업의 성격)
    /// - evidenceGates: "이 증거를 제시받으면 이 사실을 털어놓는다" 규칙 목록
    ///
    /// 만드는 법: 프로젝트 창 우클릭 ▸ Create ▸ 이문록 ▸ 심문 캐릭터
    /// </summary>
    [CreateAssetMenu(fileName = "InterrogationCharacter", menuName = "이문록/심문 캐릭터")]
    public class InterrogationCharacter : ScriptableObject
    {
        public string characterName = "인물";

        [Tooltip("이 인물이 속한 사건(수첩 단서 범위와 연결)")]
        public CaseId caseId = CaseId.Case1_Onggojip;

        [Tooltip("인물의 성격·아는 사실·말투. 실제 AI의 시스템 프롬프트가 되고, 목업의 성격 기준이 됨")]
        [TextArea(4, 12)]
        public string persona = "";

        [Tooltip("심문 시작 시 인물의 첫 대사")]
        [TextArea(2, 4)]
        public string openingLine = "무슨 일로 오셨소?";

        [Tooltip("심문을 끝내고 돌아설 때 인물이 등 뒤로 던지는 한 마디. 비우면 그냥 닫힌다.\n" +
                 "무엇을 물었든 반드시 듣게 되므로, 다음에 갈 곳을 흘리는 자리로 쓰기 좋다")]
        [TextArea(2, 4)]
        public string closingLine = "";

        [Tooltip("추천 질문(대화 버튼). 플레이어가 고를 수 있는 질문들")]
        public List<TopicQuestion> topics = new List<TopicQuestion>();

        [Tooltip("증거를 제시하면 열리는 사실들")]
        public List<EvidenceGate> evidenceGates = new List<EvidenceGate>();
    }
}
