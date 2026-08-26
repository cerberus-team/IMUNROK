// 플레이어가 실제로 한 말에서 뽑아낸 말투 프로필입니다. 이후 모든 선택지가 이 말투로 생성됩니다.
using System;
using UnityEngine;

namespace IMUNROK.Seocheon.AI
{
    /// <summary>
    /// 어사의 말투 프로필.
    ///
    /// ★출처는 둘입니다.
    ///   ① 도입부에서 플레이어가 실제로 한 말을 AI 가 읽어 만든 것 (기본)
    ///   ② 스타일 카드 4장 중 고른 것 (VR 폴백 · 건너뛰기 · 생성 실패)
    /// 이후 경로는 ★완전히 같습니다. 대화 쪽은 어디서 왔는지 알지 못합니다.
    /// </summary>
    [Serializable]
    public sealed class SeocheonStyleProfile
    {
        [Tooltip("AI 가 붙인 말투 이름. 화면에 보여 줍니다")]
        public string styleName = "능글맞은 장사치";

        [Tooltip("말투의 특징 2~4개. 지시문에 그대로 들어갑니다")]
        public string[] traits = { "돌려 말한다", "너스레를 떤다" };

        [Tooltip("이 말투를 대표하는 한 마디")]
        public string sampleTone = "허허, 그거 참 재미난 말씀이오.";

        [Tooltip("곡물상 위장에 얼마나 어울리는가 — high / mid / low. ★수치로 화면에 노출하지 않습니다")]
        public string disguiseFit = "high";

        [Tooltip("스타일 카드에서 온 것인지(도입부 자유 발화가 아니라)")]
        public bool fromCard;

        public string TraitsJoined
        {
            get
            {
                if (traits == null || traits.Length == 0) return "(없음)";
                return string.Join(" · ", traits);
            }
        }

        /// <summary>위장 적합도를 AI 지시문 한 줄로 바꿉니다. ★플레이어에게는 안 보입니다.</summary>
        public string DisguiseInstruction
        {
            get
            {
                string fit = string.IsNullOrEmpty(disguiseFit) ? "mid" : disguiseFit.ToLowerInvariant();
                if (fit == "low")
                    return "★이 말투는 곡물상답지 않다. 너는 상대가 관아 사람인가 의심하며 말을 아끼고 짧게 답한다. " +
                           "★그래도 완전히 입을 닫지는 마라. 다른 이야기 끝에라도 조각은 결국 흘려라.";
                if (fit == "high")
                    return "이 말투는 장사꾼답다. 너는 경계하지 않고 편하게 푸념을 늘어놓는다.";
                return "이 말투는 그럭저럭 장사꾼으로 보인다. 너는 특별히 경계하지도, 아주 편해하지도 않는다.";
            }
        }

        public static SeocheonStyleProfile Default()
        {
            SeocheonStyleProfile p = new SeocheonStyleProfile();
            p.styleName = "능글맞은 장사치";
            p.traits = new string[] { "돌려 말한다", "너스레를 떤다", "존대를 쓴다" };
            p.sampleTone = "허허, 그거 참 재미난 말씀이오.";
            p.disguiseFit = "high";
            p.fromCard = true;
            return p;
        }

        /// <summary>스타일 카드를 프로필로 옮깁니다(VR 폴백 경로).</summary>
        public static SeocheonStyleProfile FromCard(TalkStyle style)
        {
            if (style == null) return Default();
            SeocheonStyleProfile p = new SeocheonStyleProfile();
            p.styleName = style.displayName;
            p.traits = new string[] { style.oneLine };
            p.sampleTone = style.exampleLine;
            p.disguiseFit = style.id == "stern" ? "low" : (style.id == "curious" ? "mid" : "high");
            p.fromCard = true;
            return p;
        }
    }
}
