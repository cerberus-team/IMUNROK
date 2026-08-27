// 카드 두 장을 맞춰 보고, 규칙에 맞으면 새 카드를 만듭니다. 무관하면 아무 일도 하지 않습니다.
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    public enum CombineOutcome
    {
        Unrelated,   // ★실패가 아닙니다. 그냥 맞물리지 않았을 뿐입니다
        Formed,
    }

    public struct CombineResult
    {
        public CombineOutcome outcome;
        public string newEntryId;
        public string ruleId;
        public CombineKind kind;
        public string resultText;
    }

    /// <summary>
    /// 결합 판정.
    ///
    /// ★카드 하나가 clueId 를 여럿 가질 수 있습니다(한 문장에서 어절을 둘 짚은 경우).
    ///   그래서 카드 대 카드가 아니라 ★clueId 원소끼리 모두 맞춰 봅니다.
    ///
    /// ★무관은 오류가 아닙니다. 로그도 남기지 않습니다 — 콘솔이 정답표가 되면 안 되니까요.
    /// </summary>
    public sealed class ClueCombiner : MonoBehaviour
    {
        [Tooltip("결합 규칙표")]
        [SerializeField] private SeocheonCombineRules rules;

        [Tooltip("결론 규칙이 성립했을 때 단계를 진행시킬 관리자. 비어 있으면 진행만 건너뜁니다")]
        [SerializeField] private SeocheonPhase phaseManager;

        [Tooltip("수집·결합 수를 표시할 표시기. 비워도 됩니다")]
        [SerializeField] private CollectionMeter collectionMeter;

        // 이미 성립시킨 규칙 (중복 생성 방지)
        private readonly HashSet<string> formedRuleIds = new HashSet<string>();

        public SeocheonCombineRules Rules { get { return rules; } }

        /// <summary>이 규칙을 이미 만들었는가.</summary>
        public bool AlreadyFormed(string ruleId)
        {
            return !string.IsNullOrEmpty(ruleId) && formedRuleIds.Contains(ruleId);
        }

        public void ResetFormed()
        {
            formedRuleIds.Clear();
        }

        /// <summary>
        /// 카드 두 장을 맞춰 봅니다. 플레이어가 집는 것은 clueId 가 아니라 ★수첩 항목(entryId) 입니다.
        /// </summary>
        public CombineResult Try(string entryIdA, string entryIdB)
        {
            CombineResult result = new CombineResult();
            result.outcome = CombineOutcome.Unrelated;

            if (rules == null) return result;
            if (string.IsNullOrEmpty(entryIdA) || string.IsNullOrEmpty(entryIdB)) return result;
            if (entryIdA == entryIdB) return result;               // 같은 카드끼리는 못 맞춥니다

            SeocheonClueRecord a = SeocheonClueStore.Get(entryIdA);
            SeocheonClueRecord b = SeocheonClueStore.Get(entryIdB);
            if (a == null || b == null) return result;

            // ★원소 단위 조합 탐색 — 카드가 여러 의미를 가질 수 있으므로.
            CombineRule found = null;
            for (int i = 0; i < a.clueIds.Count && found == null; i++)
                for (int j = 0; j < b.clueIds.Count; j++)
                {
                    CombineRule r = rules.Match(a.clueIds[i], b.clueIds[j]);
                    if (r == null) continue;
                    if (formedRuleIds.Contains(r.id)) continue;    // 이미 만든 조합
                    found = r;
                    break;
                }

            if (found == null) return result;                      // ★무관 — 조용히 끝

            List<string> sources = new List<string>(2);
            sources.Add(entryIdA);
            sources.Add(entryIdB);
            SeocheonClueRecord made = SeocheonClueStore.AddDerived(
                found.resultText, found.resultClueId, found.KindLabel, sources);
            if (made == null) return result;

            formedRuleIds.Add(found.id);
            if (collectionMeter != null) collectionMeter.RecordCombine();

            // ★단계 진행은 규칙 데이터가 정합니다. 코드에 사건 흐름을 박지 않습니다.
            // ★TryAdvanceTo 라서 이미 지나온 단계로는 되돌아가지 않습니다.
            if (found.advancesPhase && phaseManager != null)
                phaseManager.TryAdvanceTo(found.unlocksPhase);

            result.outcome = CombineOutcome.Formed;
            result.newEntryId = made.entryId;
            result.ruleId = found.id;
            result.kind = found.kind;
            result.resultText = found.resultText;
            return result;
        }
    }
}
