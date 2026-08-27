// J 키로 서천 카드 화면을 여닫습니다. 대화 중에는 열리지 않습니다.
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 카드 화면 여닫이.
    ///
    /// ★공통 JournalView 는 Seocheon 씬에 두지 않습니다. 서천 전용 화면만 씁니다.
    ///   (Journal.AddClue 기록은 그대로 유지되므로 공통 흐름·판결 연동은 살아 있습니다.)
    /// </summary>
    public sealed class SeocheonCardScreenOpener : MonoBehaviour
    {
        [SerializeField] private SeocheonCardScreen cardScreen;
        [Tooltip("★쓰지 않습니다. 옛 서천 전용 대화창 시절의 자리로, 지우면 씬 연결이 끊겨 남겨 둡니다")]
        [SerializeField] private SeocheonDialogueUI dialogueUI;

        /// <summary>
        /// 대화 중인가. ★지금 쓰는 대화창은 <see cref="SeocheonDialogueBar"/> 다
        /// (2026-08-27 에 갈아탔다). 옛 <see cref="SeocheonDialogueUI"/> 는 꺼져 있어
        /// 그것만 보면 <b>대화 중에도 카드 화면이 열린다</b>.
        /// </summary>
        private bool Talking
        {
            get
            {
                var bar = SeocheonDialogueBar.Instance;
                if (bar != null && bar.IsOpen) return true;
                return dialogueUI != null && dialogueUI.enabled && dialogueUI.IsOpen;
            }
        }

        private void Update()
        {
            if (cardScreen == null) return;
            if (!SeocheonInput.JournalPressedThisFrame) return;
            if (!cardScreen.IsOpen && Talking) return;   // 대화 우선
            cardScreen.Toggle();
        }
    }
}
