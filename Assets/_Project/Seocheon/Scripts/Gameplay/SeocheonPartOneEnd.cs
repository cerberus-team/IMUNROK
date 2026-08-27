// 1부 조사 구간이 끝났음을 알리고 그 자리에서 멈춥니다. ★없는 내용을 있는 척하지 않습니다.
using IMUNROK.Seocheon.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 1부 종료 안내.
    ///
    /// ★결론(M12)에 도달하면 화면을 덮고 "여기까지 구현됐다"고 알린 뒤 멈춥니다.
    ///   어사출두·꿈·무당집 재방문은 아직 없으므로, 있는 것처럼 연출하지 않습니다.
    ///
    /// ★닫기 버튼이 없습니다. 조작은 잠긴 채로 둡니다.
    ///   단 에디터에서는 Esc 로 닫을 수 있게 해 두었습니다(테스트 편의).
    /// </summary>
    public sealed class SeocheonPartOneEnd : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image backdrop;
        [SerializeField] private TMP_Text[] lineLabels;

        [Tooltip("안내 문구. ★코드가 아니라 여기서 고칩니다")]
        [TextArea(1, 3)]
        [SerializeField]
        private string[] lines =
        {
            "수령이 곡식을 빼돌린 정황이 모두 드러났다.",
            "─────────────",
            "어사출두 · 꿈 · 무당집 재방문은 구현 준비 중입니다.",
            "1부 조사 구간은 여기까지입니다."
        };

        [Header("연출")]
        [Tooltip("화면이 어두워지는 데 걸리는 시간(초)")]
        [SerializeField] private float fadeSeconds = 1.2f;
        [Tooltip("배경이 도달할 어두움")]
        [Range(0f, 1f)]
        [SerializeField] private float backdropAlpha = 1f;

        [Header("함께 닫을 UI")]
        [Tooltip("안내를 띄울 때 꺼 버릴 화면들. ★안 보여도 입력이 살아 있으면 안 되므로 끕니다. " +
                 "★이 배열에 자기 자신(_UI_PartOneEnd)을 넣지 마십시오")]
        [SerializeField] private GameObject[] closeOnShow = new GameObject[0];

        [Header("어느 조각에 반응하는가")]
        [Tooltip("이 clueId 가 만들어지면 안내를 띄웁니다")]
        [SerializeField] private string triggerClueId = "R12";

        private bool shown;
        private bool playerLocked;
        private float fade;

        public bool IsShown { get { return shown; } }

        private void Awake()
        {
            // ★이 오브젝트 자체는 계속 켜 둡니다. 꺼 버리면 OnDisable 로 구독이 끊겨
            //   결론이 나와도 못 듣습니다. 감추는 것은 panelRoot 만.
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            SeocheonClueStore.Changed += OnStoreChanged;
        }

        private void OnDisable()
        {
            SeocheonClueStore.Changed -= OnStoreChanged;
            if (playerLocked) { playerLocked = false; PlayerControlLock.Pop(); }
        }

        private void OnStoreChanged()
        {
            if (shown) return;
            if (string.IsNullOrEmpty(triggerClueId)) return;
            if (!SeocheonClueStore.HasClue(triggerClueId)) return;
            Show();
        }

        /// <summary>결론에 도달했을 때. 바깥에서 직접 불러도 됩니다.</summary>
        public void Show()
        {
            if (shown) return;
            shown = true;
            if (panelRoot != null) panelRoot.SetActive(true);
            if (!playerLocked) { PlayerControlLock.Push(); playerLocked = true; }

            if (lineLabels != null)
                for (int i = 0; i < lineLabels.Length; i++)
                {
                    if (lineLabels[i] == null) continue;
                    lineLabels[i].text = (lines != null && i < lines.Length) ? lines[i] : string.Empty;
                    lineLabels[i].alpha = 0f;
                }
            if (backdrop != null)
            {
                Color c = backdrop.color; c.a = 0f; backdrop.color = c;
            }
            fade = 0f;

            // ★같은 프레임에 끄면, 지금 이 호출을 만든 버튼 핸들러가
            //   자기가 딛고 선 오브젝트를 잃습니다. 한 프레임 미룹니다.
            if (isActiveAndEnabled) StartCoroutine(CloseOthersNextFrame());
            else CloseOthers();
        }

        private System.Collections.IEnumerator CloseOthersNextFrame()
        {
            yield return null;
            CloseOthers();
        }

        private void CloseOthers()
        {
            if (closeOnShow == null) return;
            for (int i = 0; i < closeOnShow.Length; i++)
            {
                GameObject go = closeOnShow[i];
                if (go == null) continue;
                if (go == gameObject) continue;              // ★자기 자신은 절대 끄지 않습니다
                if (panelRoot != null && go == panelRoot) continue;
                go.SetActive(false);
            }
        }

        private void Update()
        {
            if (!shown) return;

            if (fade < 1f)
            {
                fade = fadeSeconds <= 0f ? 1f : Mathf.Min(1f, fade + Time.unscaledDeltaTime / fadeSeconds);
                if (backdrop != null)
                {
                    Color c = backdrop.color; c.a = backdropAlpha * fade; backdrop.color = c;
                }
                if (lineLabels != null)
                    for (int i = 0; i < lineLabels.Length; i++)
                        if (lineLabels[i] != null) lineLabels[i].alpha = fade;
            }

#if UNITY_EDITOR
            // ★테스트 편의. 빌드에는 들어가지 않으므로 플레이어는 닫을 수 없습니다.
            if (SeocheonInput.CancelPressedThisFrame) Hide();
#endif
        }

#if UNITY_EDITOR
        private void Hide()
        {
            if (!shown) return;
            shown = false;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (playerLocked) { playerLocked = false; PlayerControlLock.Pop(); }
        }
#endif
    }
}
