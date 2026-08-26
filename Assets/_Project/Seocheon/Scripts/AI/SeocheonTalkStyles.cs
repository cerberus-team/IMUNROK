// 어사가 어떤 사람인지 — 대화 스타일 정의입니다. 코드가 아니라 에셋에서 고칩니다.
using System;
using UnityEngine;

namespace IMUNROK.Seocheon.AI
{
    /// <summary>
    /// 대화 스타일 하나.
    ///
    /// ★정답 스타일은 없습니다. 어느 것을 골라도 단서에 도달할 수 있고,
    ///   ★얻는 것과 잃는 것이 다를 뿐입니다.
    /// </summary>
    [Serializable]
    public sealed class TalkStyle
    {
        [Tooltip("내부 ID (예: sly)")]
        public string id = string.Empty;

        [Tooltip("선택 화면에 뜨는 이름")]
        public string displayName = string.Empty;

        [Tooltip("한 줄 설명")]
        [TextArea(1, 3)]
        public string oneLine = string.Empty;

        [Tooltip("예시 대사 한 줄. 선택 화면에도 뜨고 AI 지시문에도 들어갑니다")]
        [TextArea(1, 3)]
        public string exampleLine = string.Empty;

        [Tooltip("AI 에게 줄 말투 규칙. 선택지 label 을 이 말투로 씁니다")]
        [TextArea(2, 6)]
        public string promptRule = string.Empty;

        [Tooltip("이 말투가 곡물상 위장에 어울리는지, 인물이 어떻게 반응할지에 대한 지시")]
        [TextArea(2, 6)]
        public string disguiseNote = string.Empty;
    }

    /// <summary>
    /// 스타일 목록. ★하드코딩하지 않습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "SeocheonTalkStyles", menuName = "이문록/서천/대화 스타일표")]
    public sealed class SeocheonTalkStyles : ScriptableObject
    {
        public TalkStyle[] styles = Array.Empty<TalkStyle>();

        public TalkStyle Find(string id)
        {
            if (string.IsNullOrEmpty(id) || styles == null) return null;
            for (int i = 0; i < styles.Length; i++)
                if (styles[i] != null && styles[i].id == id) return styles[i];
            return null;
        }
    }
}
