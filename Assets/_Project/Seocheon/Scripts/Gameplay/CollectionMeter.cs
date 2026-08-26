// TMP 라벨을 연결해 수집한 조각 수와 이어붙인 수를 표시합니다. 유효율은 표시하지 않습니다.
using TMPro;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 수집 현황 표시기.
    ///
    /// ★"유효율 %" 를 폐기했습니다. 지목 시점에는 유효·오답을 판정하지 않으므로
    ///   비율을 띄우는 것 자체가 ★정답을 즉시 알려 주는 장치가 됩니다.
    ///
    /// 대신 두 가지만 셉니다.
    ///   - 수집: 몇 조각을 적어 두었는가 (노력의 양)
    ///   - 이어붙임: 몇 번 말이 맞물렸는가 (★실제 진척)
    /// </summary>
    public sealed class CollectionMeter : MonoBehaviour
    {
        [Tooltip("수집 현황을 표시할 TMP 텍스트")]
        [SerializeField] private TMP_Text meterText;

        [Tooltip("이어붙임 수를 함께 표시할지. 결합 기능이 붙기 전에는 꺼 두어도 됩니다")]
        [SerializeField] private bool showCombineCount = true;

        public int CollectedCount { get; private set; }
        public int CombinedCount { get; private set; }

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>조각 하나를 적어 두었을 때. ★유효 여부를 인자로 받지 않습니다.</summary>
        public void RecordCollect()
        {
            CollectedCount++;
            Refresh();
        }

        /// <summary>조각 둘이 맞물려 새 항목이 생겼을 때.</summary>
        public void RecordCombine()
        {
            CombinedCount++;
            Refresh();
        }

        /// <summary>세이브 복원 등으로 값을 직접 맞출 때.</summary>
        public void SetCounts(int collected, int combined)
        {
            CollectedCount = collected;
            CombinedCount = combined;
            Refresh();
        }

        public void Refresh()
        {
            if (meterText == null) return;
            meterText.text = showCombineCount
                ? "적어 둔 말 " + CollectedCount + "  ·  이어붙임 " + CombinedCount
                : "적어 둔 말 " + CollectedCount;
        }
    }
}
