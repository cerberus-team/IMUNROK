using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 봉서(封書) 더미 — 해결한 사건 수에 비례해 더미가 쌓여 보인다.
    ///  · 최종(전부 쌓인) 더미를 만들어 조각들을 _pieces에 넣는다(아래→위 순서).
    ///  · 시작 시 진행도에 맞춰 아래에서부터 그만큼만 켠다. 해결 '순서'는 무관, '개수'만 본다.
    ///  · GameState가 바뀌면 자동 갱신.
    /// </summary>
    public class BongseoStack : MonoBehaviour
    {
        [Tooltip("최종 더미 조각들(아래→위 순서). 진행도만큼 아래부터 켜짐")]
        [SerializeField] private GameObject[] _pieces;

        private GameState _state;

        private void OnEnable()
        {
            _state = GameState.Instance;
            if (_state != null) _state.OnCaseChanged += HandleChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (_state != null) _state.OnCaseChanged -= HandleChanged;
        }

        private void HandleChanged(CaseId _) => Refresh();

        private void Refresh()
        {
            if (_pieces == null || _pieces.Length == 0 || _state == null) return;

            int total = System.Enum.GetValues(typeof(CaseId)).Length;   // 3
            int solved = 0;
            foreach (CaseId id in System.Enum.GetValues(typeof(CaseId)))
                if (_state.GetStatus(id) == CaseStatus.Completed) solved++;

            // 진행도 비례로 몇 조각 보일지(예: 9조각·3사건 → 사건당 3조각)
            int show = total > 0 ? Mathf.RoundToInt(_pieces.Length * (solved / (float)total)) : 0;
            for (int i = 0; i < _pieces.Length; i++)
                if (_pieces[i] != null) _pieces[i].SetActive(i < show);
        }

#if UNITY_EDITOR
        // 에디터에서 최종 모습 만들 때 미리보기용
        [ContextMenu("미리보기: 전부 쌓기")]
        private void PreviewFull() { if (_pieces != null) foreach (var p in _pieces) if (p != null) p.SetActive(true); }
        [ContextMenu("미리보기: 전부 치우기")]
        private void PreviewEmpty() { if (_pieces != null) foreach (var p in _pieces) if (p != null) p.SetActive(false); }
#endif
    }
}
