// 조각 두 개가 맞물리는 규칙표입니다. 코드가 아니라 에셋에서 고칩니다.
using System;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>결합이 성립했을 때의 성격.</summary>
    public enum CombineKind
    {
        Contradiction,   // 모순 — 두 말이 서로 어긋난다
        Link,            // 연결 — 두 말이 같은 곳을 가리킨다
        Conclusion,      // 결론 — 사건의 골자가 드러난다
    }

    [Serializable]
    public sealed class CombineRule
    {
        [Tooltip("규칙 ID (예: M01)")]
        public string id = string.Empty;

        [Tooltip("재료 clueId 두 개. ★순서는 무관합니다")]
        public string[] inputs = Array.Empty<string>();

        [Tooltip("성립 성격")]
        public CombineKind kind = CombineKind.Contradiction;

        [Tooltip("결과 카드에 그대로 쓸 문구")]
        [TextArea(1, 3)]
        public string resultText = string.Empty;

        [Tooltip("결과가 갖는 clueId. ★이 값으로 다시 결합 재료가 됩니다")]
        public string resultClueId = string.Empty;

        [Tooltip("성립 시 단계를 진행시킬지")]
        public bool advancesPhase;

        [Tooltip("진행시킬 단계. advancesPhase 가 켜져 있을 때만 씁니다")]
        public Phase unlocksPhase = Phase.Arrival;

        public string KindLabel
        {
            get
            {
                if (kind == CombineKind.Link) return "연결";
                if (kind == CombineKind.Conclusion) return "결론";
                return "모순";
            }
        }
    }

    /// <summary>
    /// 결합 규칙표.
    ///
    /// ★하드코딩하지 않습니다. 규칙·결과 문구·단계 진행이 전부 이 에셋에 있습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "SeocheonCombineRules", menuName = "이문록/서천/결합 규칙표")]
    public sealed class SeocheonCombineRules : ScriptableObject
    {
        [Tooltip("가능한 결합 전부")]
        public CombineRule[] rules = Array.Empty<CombineRule>();

        /// <summary>두 clueId 로 규칙을 찾습니다. 순서 무관. 없으면 null.</summary>
        public CombineRule Match(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return null;
            for (int i = 0; i < rules.Length; i++)
            {
                CombineRule r = rules[i];
                if (r == null || r.inputs == null || r.inputs.Length != 2) continue;
                if ((r.inputs[0] == a && r.inputs[1] == b) || (r.inputs[0] == b && r.inputs[1] == a))
                    return r;
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (rules == null) return;
            for (int i = 0; i < rules.Length; i++)
            {
                CombineRule r = rules[i];
                if (r == null) continue;
                if (r.inputs == null || r.inputs.Length != 2)
                    Debug.LogWarning("[결합규칙] " + r.id + " 의 재료가 2개가 아닙니다.", this);
                if (string.IsNullOrEmpty(r.resultClueId))
                    Debug.LogWarning("[결합규칙] " + r.id + " 에 resultClueId 가 없습니다. 2단 결합 재료가 되지 못합니다.", this);
                if (r.inputs != null && r.inputs.Length == 2 && r.inputs[0] == r.inputs[1])
                    Debug.LogWarning("[결합규칙] " + r.id + " 의 재료 둘이 같습니다.", this);
            }
        }
#endif
    }
}
