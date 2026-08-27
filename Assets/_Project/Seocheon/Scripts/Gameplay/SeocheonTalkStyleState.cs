// 이번 판의 말투 프로필을 들고 있습니다. 도입부 자유 발화 또는 스타일 카드에서 옵니다.
using System;
using IMUNROK.Seocheon.AI;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 말투 프로필 보관소.
    ///
    /// ★대화 쪽은 프로필이 ★어디서 왔는지 알지 못합니다.
    ///   도입부 자유 발화든, 스타일 카드든, 기본값이든 같은 필드를 봅니다.
    /// </summary>
    public static class SeocheonTalkStyleState
    {
        private static SeocheonStyleProfile profile;

        /// <summary>프로필이 정해졌을 때. UI 갱신용.</summary>
        public static event Action<SeocheonStyleProfile> Changed;

        public static SeocheonStyleProfile Profile { get { return profile; } }
        public static bool HasChosen { get { return profile != null; } }
        public static string CurrentName { get { return profile != null ? profile.styleName : "(미정)"; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            profile = null;
            Changed = null;
        }

        /// <summary>도입부에서 AI 가 만든 프로필.</summary>
        public static void SetProfile(SeocheonStyleProfile value)
        {
            profile = value != null ? value : SeocheonStyleProfile.Default();
            if (Changed != null) Changed(profile);
        }

        /// <summary>★스타일 카드에서 (VR 폴백 · 건너뛰기 · 생성 실패).</summary>
        public static void Choose(TalkStyle style)
        {
            SetProfile(SeocheonStyleProfile.FromCard(style));
        }

        /// <summary>아무것도 못 정했을 때의 기본값.</summary>
        public static void UseDefault()
        {
            SetProfile(SeocheonStyleProfile.Default());
        }

        public static void ChooseById(SeocheonTalkStyles table, string id)
        {
            if (table == null) { UseDefault(); return; }
            Choose(table.Find(id));
        }
    }
}
