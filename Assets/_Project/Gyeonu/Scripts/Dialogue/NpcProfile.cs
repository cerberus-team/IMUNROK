using System;
using System.Collections.Generic;
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
    ///   ② <see cref="DialogueSession"/> 이 매 턴 끼워 넣는 [지금의 태도]
    ///        · 견우      — <see cref="trustBands"/> (신뢰도 구간)
    ///        · 그 밖     — <see cref="conditionalFacts"/> 중 지금 조건을 만족한 것
    ///        · 무례 3회  — <see cref="lockedAttitude"/> 로 통째로 갈아 낀다
    ///   ③ 공통 연동이 덧붙이는 공용 규칙("2~3문장으로 짧게, 조선시대 말투로")
    ///   ①은 붙박이, ②는 매 턴 바뀐다 — 그래서 ②를 persona가 아니라 '밝혀진 사실'로 보낸다.
    ///
    /// ■ 2026-08-25 확장 — NPC 11인
    ///   견우 한 사람으로 구조를 확인한 뒤 나머지 10인을 붙이면서 세 가지가 늘었다.
    ///     · <see cref="conditionalFacts"/> — 신뢰도가 아닌 <b>조건</b>으로 열리는 태도.
    ///       수령의 경계도 구간, 상인의 목격 게이트, 어머니의 Thank 조건이 전부 이 한 틀로 들어간다.
    ///     · <see cref="grantableClues"/>   — 이 인물이 <b>줄 수 있는</b> 단서. AI가 응답 끝에
    ///       [단서:B3] 을 적으면 대화 쪽이 떼어 내 <see cref="GyeonuCase.AddClue"/> 를 부른다.
    ///       목록에 없는 코드는 무시한다 — AI가 지어낸 단서로 추리가 앞질러 가면 안 된다.
    ///     · 모션 이름 — <see cref="NpcActor"/> 가 쓰는 애니메이터 상태 이름. 인물마다 다르다
    ///       (수령은 앉은 자세가 기본, 상인은 술상 앞, 선아는 쓰러진 자세).
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

        /// <summary>
        /// 조건이 맞을 때만 AI에게 주는 문장 (2026-08-25).
        /// 신뢰도 구간을 쓰지 않는 인물의 '지금의 태도'가 전부 이 틀로 들어온다.
        ///
        /// ⚠️ 조건을 비워 두면 <b>항상</b> 붙는다 — 붙박이 사정(예: 상인의 목격 게이트 규칙)을
        ///    적을 때 쓴다. 반대로 조건이 하나라도 있으면 전부 만족해야 붙는다(AND).
        /// </summary>
        [Serializable]
        public class ConditionalFact
        {
            [Tooltip("무엇을 위한 줄인지 — 디버그·로그에만 쓴다")]
            public string label = "";

            [Tooltip("플레이어가 이 단서를 전부 가지고 있어야 한다")]
            public ClueId[] requireClues = Array.Empty<ClueId>();

            [Tooltip("이 진행 플래그가 전부 서 있어야 한다 (GyeonuWorld.F_…)")]
            public string[] requireFlags = Array.Empty<string>();

            [Tooltip("이 단서 중 하나라도 가지고 있으면 이 줄은 붙지 않는다")]
            public ClueId[] hideIfClues = Array.Empty<ClueId>();

            [Tooltip("선아를 구출한 뒤에만")]
            public bool requireRescued = false;

            [Tooltip("선아를 구출하면 이 줄은 더 붙지 않는다 (구출 전 상태 설명)")]
            public bool hideAfterRescue = false;

            [Tooltip("경계도 하한 (−1이면 무시). 수령 전용")]
            public int minAlert = -1;

            [Tooltip("경계도 상한 (−1이면 무시). 수령 전용")]
            public int maxAlert = -1;

            [Tooltip("이 시간대에만 (없으면 아무 때나)")]
            public TimeOfDay[] onlyAt = Array.Empty<TimeOfDay>();

            [Tooltip("AI에게 주는 문장")]
            [TextArea(2, 8)]
            public string text = "";

            public bool Matches()
            {
                foreach (var c in requireClues) if (!GyeonuCase.HasClue(c)) return false;
                foreach (var f in requireFlags) if (!GyeonuCase.HasFlag(f)) return false;
                foreach (var c in hideIfClues) if (GyeonuCase.HasClue(c)) return false;
                if (requireRescued && !GyeonuCase.SeonaRescued) return false;
                if (hideAfterRescue && GyeonuCase.SeonaRescued) return false;
                if (minAlert >= 0 && GyeonuCase.Alert < minAlert) return false;
                if (maxAlert >= 0 && GyeonuCase.Alert > maxAlert) return false;
                if (onlyAt != null && onlyAt.Length > 0)
                {
                    bool ok = false;
                    foreach (var t in onlyAt) if (t == GyeonuCase.Time) { ok = true; break; }
                    if (!ok) return false;
                }
                return true;
            }
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

        [Header("조건부 태도 (견우 밖의 인물)")]
        [Tooltip("조건이 맞는 것만 매 턴 AI에게 준다. 조건이 비면 항상 붙는다")]
        public ConditionalFact[] conditionalFacts = Array.Empty<ConditionalFact>();

        [Tooltip("모욕 3회로 마음을 닫았을 때의 태도")]
        [TextArea(2, 5)]
        public string lockedAttitude = "이 사람과는 더 말하고 싶지 않다. 한 마디로 끊고 입을 다문다.";

        [Tooltip("무례 3회 뒤 무엇을 하면 풀리는지 — 사람이 읽는 메모다. 코드가 읽지 않는다")]
        [TextArea(1, 4)]
        public string recoveryNote = "";

        [Header("등급 판정")]
        [Tooltip("켜면 AI 응답 첫 줄의 [등급:…] 을 읽어 점수에 반영한다. " +
                 "견우는 신뢰도가 움직이고, 나머지는 모욕만 세어 무례 누적에 쓴다")]
        public bool gradeTalk = true;

        [Header("단서 게이트")]
        [Tooltip("이 인물이 내놓을 수 있는 단서. AI가 [단서:B3] 을 적었을 때 " +
                 "여기 없는 코드는 무시한다 — 지어낸 단서로 추리가 앞질러 가면 안 된다")]
        public ClueId[] grantableClues = Array.Empty<ClueId>();

        [Tooltip("단서를 내줄 때 함께 세울 진행 플래그 (grantableClues와 짝이 아니라 별개다)")]
        public string[] grantableFlags = Array.Empty<string>();

        [Tooltip("이 인물과 처음 말을 섞는 순간 그냥 얻는 단서. " +
                 "문서에서 '첫 대화'·'마을 자동'으로 적힌 것들이다 (A6·A7·B7·C8)")]
        public ClueId[] grantOnFirstTalk = Array.Empty<ClueId>();

        [Tooltip("이 인물과 처음 말을 섞는 순간 서는 진행 플래그. " +
                 "최초의 직녀를 만났다는 사실처럼, 단서 코드가 없는 '만남' 자체를 기록한다")]
        public string[] flagsOnFirstTalk = Array.Empty<string>();

        [Header("비밀 한 조각 — 단서 코드가 없는 것")]
        [Tooltip("AI가 [비밀] 을 적었을 때 세울 플래그. " +
                 "아이02의 개구멍, 최초의 견우가 아는 관아 통로가 이쪽이다 — 둘 다 단서 번호가 없다")]
        public string secretFlag = "";

        [Tooltip("[비밀] 이 나왔을 때 알림창에 띄울 한 줄")]
        public string secretToast = "";

        /// <summary>이 인물과 이미 말을 섞었는가 — 첫 대화 단서를 두 번 주지 않으려고 둔다.</summary>
        public string TalkedFlag => "npc_talked_" + npcId;

        [Header("수령 전용")]
        [Tooltip("켜면 응답 끝의 [화제:…] 를 읽어 경계도를 올린다 (문서 「12」)")]
        public bool tagAlertTopic = false;

        [Tooltip("켜면 응답 끝의 [모순:M1] 을 읽어 M1을 성립시킨다")]
        public bool tagContradictionM1 = false;

        [Header("모션 — NpcActor가 쓰는 애니메이터 상태 이름")]
        [Tooltip("기본 자세. 아무 일도 없을 때 여기로 돌아온다")]
        public string idleState = "Idle";

        [Tooltip("대화 중에 쓸 모션. 비우면 기본 자세를 그대로 둔다 (아이02·주모가 그렇다)")]
        public string talkState = "";

        [Tooltip("걷기. 없으면 비워 둔다 — 상인은 Walk가 없다")]
        public string walkState = "";

        [Tooltip("평상시 랜덤 모션. 문서 「23. 랜덤 간격 요약」 그대로 적는다")]
        public RandomMotion[] randomMotions = Array.Empty<RandomMotion>();

        [Tooltip("첫 랜덤까지 더 기다릴 시간(초). 아이 3인을 서로 어긋내는 데 쓴다")]
        public float randomStartDelay = 0f;

        /// <summary>평상시에 이따금 도는 모션 한 줄.</summary>
        [Serializable]
        public class RandomMotion
        {
            public string state = "";
            [Tooltip("다음 재생까지 최소 초")] public float minInterval = 15f;
            [Tooltip("다음 재생까지 최대 초")] public float maxInterval = 30f;
            [Tooltip("때가 됐을 때 실제로 재생할 확률 0~1. '낮은 확률'은 0.35쯤")]
            [Range(0f, 1f)] public float chance = 1f;
        }

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

        /// <summary>지금 조건을 만족한 조건부 태도 전부. 순서는 적은 순서 그대로다.</summary>
        public void CollectConditionalFacts(List<string> into)
        {
            if (into == null || conditionalFacts == null) return;
            foreach (var f in conditionalFacts)
            {
                if (f == null || string.IsNullOrEmpty(f.text)) continue;
                if (!f.Matches()) continue;
                into.Add(f.text.Replace("\n", " "));
            }
        }

        /// <summary>AI가 적은 단서 코드를 이 인물이 실제로 줄 수 있는가.</summary>
        public bool CanGrant(ClueId id)
        {
            foreach (var c in grantableClues) if (c == id) return true;
            return false;
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
