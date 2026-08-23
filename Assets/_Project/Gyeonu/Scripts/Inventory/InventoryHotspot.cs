using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 소지품 판에서 **가리켜 누를 수 있는 자리** (칸·버튼·설명 영역).
    ///
    /// 마우스 좌표가 아니라 **광선**으로 판정한다 — 데스크톱에서는 화면 중앙 시선,
    /// VR에서는 컨트롤러 광선이 그대로 같은 코드를 탄다. 그래서 UGUI EventSystem을
    /// 쓰지 않고 각 자리에 트리거 콜라이더를 달았다 (커서 잠금과도 안 싸운다).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class InventoryHotspot : MonoBehaviour
    {
        public enum Kind { 칸, 사용, 뒤로, 설명, 쪽넘김, 이웃, 닫기, 돋보기, 조사닫기 }

        public Kind kind;
        /// <summary>칸이면 쪽 안에서의 자리, 쪽넘김·이웃이면 방향(±1).</summary>
        public int index;
        /// <summary>꺼져 있으면 가리켜도 반응하지 않는다 (사용 불가 아이템의 사용 버튼 등).</summary>
        public bool interactable = true;

        [HideInInspector] public Image frame;      // 호버 시 색이 바뀌는 테두리
        [HideInInspector] public RawImage icon;    // 칸에 뜨는 물건 그림
        [HideInInspector] public Text label;       // 칸에 뜨는 이름
        [HideInInspector] public Color idleColor;
        [HideInInspector] public Color hoverColor;

        public void SetHovered(bool on)
        {
            if (frame != null) frame.color = (on && interactable) ? hoverColor : idleColor;
        }
    }
}
