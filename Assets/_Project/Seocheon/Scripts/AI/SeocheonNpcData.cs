// 서천 NPC 한 명의 성격·흘릴 단서·조건·폴백 대사를 담는 에셋입니다.
using System;
using UnityEngine;

namespace IMUNROK.Seocheon.AI
{
    /// <summary>이 NPC 가 흘릴 수 있는 단서 하나와, 흘리는 조건.</summary>
    [Serializable]
    public sealed class SeocheonClue
    {
        [Tooltip("단서 ID. ★내부 전용입니다 — 수첩에는 나가지 않습니다 (예: A1)")]
        public string clueId = string.Empty;

        [Tooltip("이 조각이 뜻하는 바. ★수첩에는 나가지 않습니다. AI 지시문과 결합 결과 문구에 쓰입니다")]
        [TextArea(1, 3)]
        public string journalText = string.Empty;

        [Tooltip("어떤 질문을 받으면 이 단서를 흘리는지. 그대로 AI 지시문에 들어갑니다")]
        [TextArea(1, 4)]
        public string revealCondition = string.Empty;
    }

    /// <summary>
    /// 조각 하나를 들이밀었을 때 이 인물이 보이는 반응 (2026-08-27).
    ///
    /// ★없으면 AI 가 아무 반응이나 지어냅니다. 그러면 게임이 무너집니다 —
    ///   무관한 조각에도 뜨끔해하거나, 관련 조각에 한 번에 다 실토해 버립니다.
    ///   그래서 <b>규칙이 있는 조각만</b> 태도가 바뀌고, 나머지는 어리둥절해하고 넘어갑니다.
    /// </summary>
    [Serializable]
    public sealed class SeocheonPresentReaction
    {
        [Tooltip("들이밀린 조각의 clueId. ★이 인물이 준 것이 아니어도 됩니다 — 남의 말을 물어 오는 것이 심문입니다")]
        public string clueId = string.Empty;

        [Tooltip("그때 이 인물이 어떻게 나오는가. 그대로 AI 지시문에 들어갑니다. " +
                 "★한 번에 다 실토하게 쓰지 마십시오 — 태도가 달라지는 정도로 씁니다")]
        [TextArea(2, 5)]
        public string reaction = string.Empty;

        [Tooltip("이 반응으로 새로 흘릴 조각의 clueId. 비워도 됩니다. " +
                 "★clues 목록에 있는 값이어야 합니다")]
        public string revealsClueId = string.Empty;
    }

    /// <summary>
    /// 서천 전용 심문 인물 데이터.
    ///
    /// ★공통 InterrogationCharacter 를 쓰지 않은 이유:
    ///   공통의 EvidenceGate 는 "플레이어가 단서를 ★제시하면 인물이 사실을 실토한다" 구조입니다.
    ///   서천은 반대로 "인물이 ★어떤 질문에 어떤 조각을 흘리는가" 가 필요하고,
    ///   수첩 문구(journalText)와 AI 실패 시 쓸 폴백 대사도 있어야 합니다.
    ///   공통 파일은 수정 금지이므로 필드를 더할 수 없어 서천 전용으로 새로 만들었습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "SeocheonNpc", menuName = "이문록/서천/NPC 데이터")]
    public sealed class SeocheonNpcData : ScriptableObject
    {
        [Tooltip("화면에 표시할 이름")]
        public string npcName = "이름 없는 사람";

        [Tooltip("성격·처지·아는 것. AI 지시문의 첫 단락이 됩니다")]
        [TextArea(4, 12)]
        public string persona = string.Empty;

        [Tooltip("대화창을 열 때 먼저 하는 말")]
        [TextArea(2, 4)]
        public string openingLine = "무슨 일로 오셨소?";

        [Tooltip("이 인물이 줄 수 있는 단서. ★AI 는 이 목록 밖의 clueId 를 만들 수 없습니다")]
        public SeocheonClue[] clues = Array.Empty<SeocheonClue>();

        [Tooltip("★조각을 들이밀렸을 때의 반응. 여기 없는 조각을 들이밀면 어리둥절해하고 넘어갑니다")]
        public SeocheonPresentReaction[] presentReactions = Array.Empty<SeocheonPresentReaction>();

        [Tooltip("★AI 실패 시 쓸 고정 대사. 순서대로 한 턴에 하나씩 나옵니다")]
        public WordPickNote.Sentence[] fallbackSentences = Array.Empty<WordPickNote.Sentence>();

        [Tooltip("★AI 가 선택지를 못 줬을 때 쓸 고정 질문. 3~4개. " +
                 "스타일별로 나누지 않고 ★공통 하나로 둡니다(사유는 보고 참조)")]
        [TextArea(1, 2)]
        public string[] fallbackAsks =
        {
            "이 고을 사정이 어떻소?",
            "요즘 관아는 어떻소?",
            "쌀값은 어떻소?",
            "그만 가 보겠소.",
        };

        [Tooltip("★응답 형식이 깨졌을 때 대신 내보낼 회피 대사. " +
                 "JSON 원문을 화면에 노출하지 않기 위한 것입니다. 순서대로 돌아가며 나옵니다")]
        [TextArea(1, 3)]
        public string[] parseFailLines =
        {
            "…글쎄올시다.",
            "무슨 말씀이신지 잘 모르겠구려.",
            "허, 소인이 잠시 딴생각을 했소.",
        };

        /// <summary>
        /// 이 조각을 들이밀렸을 때의 반응을 찾습니다. 없으면 null —
        /// ★그것은 <b>이 인물과 무관한 조각</b>이라는 뜻이고, 지시문이 어리둥절해하라고 시킵니다.
        /// </summary>
        public SeocheonPresentReaction FindPresentReaction(string clueId)
        {
            if (string.IsNullOrEmpty(clueId) || presentReactions == null) return null;
            for (int i = 0; i < presentReactions.Length; i++)
                if (presentReactions[i] != null && presentReactions[i].clueId == clueId) return presentReactions[i];
            return null;
        }

        /// <summary>clueId 로 단서를 찾습니다. 없으면 null(= AI 가 만들어 낸 ID).</summary>
        public SeocheonClue FindClue(string clueId)
        {
            if (string.IsNullOrEmpty(clueId) || clues == null) return null;
            for (int i = 0; i < clues.Length; i++)
                if (clues[i] != null && clues[i].clueId == clueId) return clues[i];
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (clues == null) return;
            for (int i = 0; i < clues.Length; i++)
            {
                if (clues[i] == null) continue;
                if (string.IsNullOrEmpty(clues[i].clueId))
                    Debug.LogWarning("[SeocheonNpcData] " + name + " 단서[" + i + "] 의 clueId 가 비었습니다.", this);
                if (clues[i].clueId != null && clues[i].clueId.StartsWith("SC"))
                    Debug.LogWarning("[SeocheonNpcData] " + name + " 단서[" + i + "] 의 clueId 가 \"SC\" 로 시작합니다. " +
                                     "수첩 항목 ID 형식과 겹쳐 혼동됩니다.", this);
            }
        }
#endif
    }
}
