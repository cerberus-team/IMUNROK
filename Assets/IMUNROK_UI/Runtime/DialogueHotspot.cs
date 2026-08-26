using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 대화창에서 가리켜 누를 수 있는 자리 (2026-08-25).
    /// 소지품 판의 <see cref="InventoryHotspot"/> 과 같은 규약이다 — 마우스 좌표가 아니라
    /// <b>광선</b>으로 판정한다. 데스크톱에서는 화면 커서, VR에서는 컨트롤러 광선이 같은 코드를 탄다.
    ///
    /// ⚠️ 단서를 고르는 자리는 여기 없다 — 증거 제시는 소지품 판(<see cref="InventoryUI"/>)이 맡는다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DialogueHotspot : MonoBehaviour
    {
        /// <summary>
        /// ⚠️ <c>말하기</c> 는 <b>누르고 있는 동안</b> 녹음하는 자리다 (2026-08-26, 하단 바 시안).
        ///    나머지처럼 눌렀다 떼는 순간 한 번 발화하는 것이 아니라서
        ///    <c>ClickHovered</c> 가 아니라 <c>HandleVoice</c> 가 직접 본다.
        ///    보내는 것·점수 매기는 것은 하나도 안 달라졌다 — 왼쪽 Ctrl 을 누르는 것과 같은 길이다.
        /// </summary>
        public enum Kind { 묻기, 단서열기, 끝내기, 입력칸, 말하기 }

        public Kind kind;
        public bool interactable = true;

        [HideInInspector] public Image frame;
        [HideInInspector] public TextMeshProUGUI label;
        [HideInInspector] public Color idleColor;
        [HideInInspector] public Color hoverColor;

        public void SetHovered(bool on)
        {
            if (frame != null) frame.color = (on && interactable) ? hoverColor : idleColor;
        }
    }
}
