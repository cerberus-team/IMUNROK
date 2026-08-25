using System;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// NPC 한 명의 대화 정의 (ScriptableObject, 2026-08-25).
    /// 만드는 법: Project 창 ▸ Create ▸ 이문록 ▸ 견우 NPC
    ///
    /// ■ 왜 공통 <see cref="IMUNROK.Common.InterrogationCharacter"/> 를 그대로 쓰지 않는가
    ///   공통 쪽은 "증거 key → 실토 문구" 짝(EvidenceGate)으로 짜여 있다. 제3사건의 단서는
    ///   <see cref="ClueId"/> 22종으로 이미 코드화돼 있고, 증감은 <see cref="ClueTable"/> 배점표가
    ///   정한다 — 같은 표를 문자열로 한 벌 더 적으면 검산이 갈라진다. 그래서 정의는 여기 두고,
    ///   <b>AI 호출은 공통 연동(<see cref="IMUNROK.Common.GeminiNpcResponder"/>)을 그대로 쓴다.</b>
    ///   공통 폴더는 읽기만 한다.
    ///
    /// ■ 프롬프트가 만들어지는 순서
    ///   ① <see cref="persona"/>  — 인물·말투·아는 것·절대 금지·대답 형식
    ///   ② <see cref="DialogueSession"/> 이 매 턴 끼워 넣는 [지금의 태도] (신뢰도 구간)
    ///   ③ 공통 연동이 덧붙이는 공용 규칙("2~3문장으로 짧게, 조선시대 말투로")
    ///   ①은 붙박이, ②는 매 턴 바뀐다 — 그래서 ②를 persona가 아니라 '밝혀진 사실'로 보낸다.
    /// </summary>
    [CreateAssetMenu(menuName = "이문록/견우 NPC", fileName = "Npc_새인물")]
    public class NpcProfile : ScriptableObject
    {
        /// <summary>신뢰도 한 구간의 태도. 이 구간에 들어오면 AI에게 이 문장을 준다.</summary>
        [Serializable]
        public class TrustBand
        {
            [Tooltip("이 구간의 최저 신뢰도 (이 값 이상이면 이 구간)")]
            public int min;

            [Tooltip("구간 이름 — 디버그·로그에만 쓴다")]
            public string label = "";

            [Tooltip("AI에게 '지금 너는 이런 상태다'로 주는 문장. 무엇까지 말해도 되는지를 적는다")]
            [TextArea(2, 6)]
            public string attitude = "";
        }

        [Header("식별")]
        public NpcId npcId = NpcId.Gyeonu;

        [Tooltip("대화창 위에 뜨는 이름")]
        public string displayName = "이름 없는 사람";

        [Tooltip("조준했을 때 뜨는 행동 문구")]
        public string talkVerb = "말 걸기";

        [Header("프롬프트")]
        [Tooltip("인물·말투·아는 것·절대 금지·대답 형식. 그대로 AI의 시스템 지침이 된다")]
        [TextArea(10, 40)]
        public string persona = "";

        [Tooltip("대화를 열 때 NPC가 먼저 던지는 말. AI를 부르지 않는다")]
        [TextArea(1, 4)]
        public string openingLine = "…무슨 일이십니까.";

        [Header("신뢰도 구간별 태도 (견우 전용)")]
        [Tooltip("켜면 매 턴 신뢰도에 맞는 태도 문장을 AI에게 함께 준다")]
        public bool useTrustBands = false;

        [Tooltip("min 오름차순으로 적는다. 현재 신뢰도 이하 중 가장 큰 min이 골라진다")]
        public TrustBand[] trustBands = Array.Empty<TrustBand>();

        [Tooltip("모욕 3회로 마음을 닫았을 때의 태도")]
        [TextArea(2, 5)]
        public string lockedAttitude = "이 사람과는 더 말하고 싶지 않다. 한 마디로 끊고 입을 다문다.";

        [Header("등급 판정")]
        [Tooltip("켜면 AI 응답 첫 줄의 [등급:…] 을 읽어 점수에 반영한다. " +
                 "지금은 견우(신뢰도)만 점수가 붙고, 나머지는 무례 누적만 센다")]
        public bool gradeTalk = true;

        [Header("마주 서기")]
        [Tooltip("눈을 맞출 높이(m) — 발밑에서 잰다. 어른 1.55, 아이 1.05쯤")]
        public float eyeHeight = 1.55f;

        [Tooltip("마주 설 거리(m). 너무 가까우면 VR에서 압박감이 크다")]
        public float talkDistance = 1.55f;

        /// <summary>지금 신뢰도에 맞는 태도 문장. 구간을 안 쓰는 인물이면 빈 문자열.</summary>
        public string AttitudeFor(int trust, bool locked)
        {
            if (!useTrustBands) return "";
            if (locked && !string.IsNullOrEmpty(lockedAttitude)) return lockedAttitude;
            string picked = "";
            int best = int.MinValue;
            foreach (var b in trustBands)
            {
                if (b == null || b.min > trust || b.min <= best) continue;
                best = b.min;
                picked = b.attitude;
            }
            return picked;
        }

        /// <summary>지금 구간의 이름 — 로그·디버그 표시용.</summary>
        public string BandLabelFor(int trust)
        {
            if (!useTrustBands) return "";
            string picked = "";
            int best = int.MinValue;
            foreach (var b in trustBands)
            {
                if (b == null || b.min > trust || b.min <= best) continue;
                best = b.min;
                picked = string.IsNullOrEmpty(b.label) ? b.min + "+" : b.label;
            }
            return picked;
        }
    }
}
