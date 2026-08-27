// 도입부에서 상대가 던지는 상황들입니다. 코드가 아니라 에셋에서 고칩니다.
using System;
using UnityEngine;

namespace IMUNROK.Seocheon.AI
{
    [Serializable]
    public sealed class IntroPrompt
    {
        [Tooltip("상대가 던지는 말")]
        [TextArea(1, 3)]
        public string npcLine = string.Empty;

        [Tooltip("입력칸에 흐리게 뜨는 안내. 무엇을 답하면 될지 알려 줍니다")]
        public string placeholder = "…무어라 답하시겠소";

        [Tooltip("답한 뒤 상대가 짧게 받는 말. ★여기서는 단서를 주지 않습니다")]
        [TextArea(1, 3)]
        public string reaction = string.Empty;
    }

    /// <summary>
    /// 도입부 말투 수집 대본.
    ///
    /// ★이 구간의 목적은 정보가 아니라 ★플레이어의 말투를 받아 오는 것입니다.
    ///   그래서 상대는 짧게만 반응하고 ★단서를 하나도 흘리지 않습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "SeocheonIntroPrompts", menuName = "이문록/서천/도입부 대본")]
    public sealed class SeocheonIntroPrompts : ScriptableObject
    {
        [Tooltip("도입부에서 말을 거는 사람")]
        public string speakerName = "길 가던 사람";

        [Tooltip("상황 3~4개. 순서대로 던집니다")]
        public IntroPrompt[] prompts = Array.Empty<IntroPrompt>();

        [Tooltip("마지막에 상대가 하는 마무리 말")]
        [TextArea(1, 3)]
        public string closingLine = "그럼 살펴 가시오.";
    }
}
