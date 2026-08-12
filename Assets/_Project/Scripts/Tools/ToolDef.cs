using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 도구 하나의 정의(재사용 에셋). 돋보기·수첩·지도·등불처럼 하나 만들면
    /// 어느 챕터에서든 도구벨트 목록에 담아 재사용한다.
    ///
    /// 만들기: Project 창 우클릭 ▸ Create ▸ 이문록 ▸ 도구(Tool)
    /// 특수 id:
    ///   · "journal" → 클릭 시 수첩 열기/닫기
    ///   · "map"     → 클릭 시 지도 열기/닫기
    ///   · 그 외      → 선택 도구로 표시(ToolbeltHud.SelectedToolId), 다른 시스템이 참고
    /// </summary>
    [CreateAssetMenu(menuName = "이문록/도구(Tool)", fileName = "Tool_")]
    public class ToolDef : ScriptableObject
    {
        [Tooltip("도구 식별자. journal/map은 수첩·지도 토글로 특수 동작")]
        public string id = "tool";
        public string displayName = "도구";
        [Tooltip("HUD 아이콘(없으면 이름 첫 글자로 표시)")]
        public Texture2D icon;
        [TextArea] public string tooltip;
    }
}
