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
        [Tooltip("대화 중에는 카드 화면을 열지 않습니다")]
        [SerializeField] private SeocheonDialogueUI dialogueUI;

        private void Update()
        {
            if (cardScreen == null) return;
            if (!SeocheonInput.JournalPressedThisFrame) return;
            if (!cardScreen.IsOpen && dialogueUI != null && dialogueUI.IsOpen) return;   // 대화 우선
            cardScreen.Toggle();
        }
    }
}
