using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Gyeonu
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
        public enum Kind { 묻기, 단서열기, 끝내기, 입력칸 }

        public Kind kind;
        public bool interactable = true;

        [HideInInspector] public Image frame;
        [HideInInspector] public Text label;
        [HideInInspector] public Color idleColor;
        [HideInInspector] public Color hoverColor;

        public void SetHovered(bool on)
        {
            if (frame != null) frame.color = (on && interactable) ? hoverColor : idleColor;
        }
    }
}
