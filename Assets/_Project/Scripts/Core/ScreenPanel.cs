using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>화면에 붙는 판을 세우는 한 자리.</b>
    ///
    /// 자막 바·귀퉁이 단추·수첩을 차례로 월드에서 떼어 화면에
    /// 붙였다. 그런데 <b>떼어낸 자리마다 같은 여덟 줄을 다시 적었다</b> — 겹치기 모드,
    /// 기준 해상도, 세로 맞춤, 레이캐스터, 씬에 남아 있을 <see cref="WorldHudAnchor"/>
    /// 걷어내기. 세 판이 세 벌을 갖게 됐고, <b>기준 세로를 적는 법까지 셋으로 갈렸다</b>:
    /// 자막은 <c>866 × 2</c>, 단추는 <c>1732</c>, 수첩은 <c>판높이 ÷ 몫</c>.
    /// 값은 같은데 적는 법이 다르면, 한 곳을 고칠 때 나머지가 안 따라온다.
    ///
    /// <b>1732 라는 수는 우리가 지은 것이 아니다.</b> 견우팀 대화창의 셈이 「1.5m 앞,
    /// 세로 화각 60°에서 화면 반높이는 866단위」이고, 그 좌표계 위에 바 너비 2900 도
    /// 아래 여백 46 도 얹혀 있다. 화면에 붙이면서 그 좌표계를 그대로 가져왔으므로
    /// 저쪽이 정한 치수가 여기서도 뜻을 지킨다 — 즉 이것은 <b>공통 좌표계</b>다.
    ///
    /// <b><see cref="UiEvents"/> 를 여기서 챙긴다.</b> 화면 판을 세워 놓고
    /// <c>EventSystem</c> 이 없으면 단추가 통째로 죽는데, 화면에는 멀쩡히 그려지므로
    /// 원인을 단추에서 찾게 된다. 실제로 조사청에서 그렇게 하루를 썼다. 판을 세우는
    /// 자리에서 함께 챙기면 그 일이 되풀이될 자리가 없어진다.
    /// </summary>
    public static class ScreenPanel
    {
        /// <summary>
        /// <b>화면 세로를 몇 단위로 치는가.</b> 견우팀 대화창의 반높이 866 의 두 배다.
        /// 이 값을 고정으로 두는 것이 요점이다 — 화각이 어떻든 「화면은 늘 1732단위」로
        /// 치고 판을 그 안에 얹으면, 판이 차지하는 화면 비율이 변하지 않는다.
        /// </summary>
        public const float RefHeight = 1732f;

        /// <summary>
        /// <b>우리 대사 글씨.</b> 견우팀 것(42)보다 크다 — 저쪽은 눈앞 1.5m 에 뜬 판을
        /// 세상 속 판을 가까이서 보는 셈이었고 우리는 모니터 너머로 읽는다. 한 자리에 적어 두어야
        /// 자막과 귀퉁이 단추가 따로 놀지 않는다(그 둘이 어긋난 것을 이미 한 번 고쳤다).
        /// </summary>
        public const int LineSize = 56;

        /// <summary>자막 바·귀퉁이 단추가 서는 켜.</summary>
        public const int LayerBar = 100;

        /// <summary>수첩·문서가 서는 켜. 펴면 자막을 가린다.</summary>
        public const int LayerBook = 200;

        /// <summary>기본 좌표계(<see cref="RefHeight"/>) 위에 화면 판을 세운다.</summary>
        public static Canvas Raise(GameObject go, int order)
        {
            return Raise(go, order, RefHeight);
        }

        /// <summary>
        /// 화면 판을 세운다. <paramref name="refH"/> 로 <b>제 좌표계</b>를 쓸 수 있다 —
        /// 수첩은 「판 높이가 화면 세로의 몇 할」을 정해야 해서 기준을 그 몫에서 낸다.
        /// </summary>
        public static Canvas Raise(GameObject go, int order, float refH)
        {
            var canvas = go.GetComponent<Canvas>();
            if (canvas == null) canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;

            // 씬에 미리 놓인 판에는 월드 시절의 추종기가 붙어 있을 수 있다.
            // 화면 판에 그것이 남으면 매 칸 자리를 도로 월드로 끌어간다.
            var anchor = go.GetComponent<WorldHudAnchor>();
            if (anchor != null) Object.Destroy(anchor);

            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(refH * 16f / 9f, refH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;   // 세로로 맞춘다 — 몫을 정하는 것은 세로다

            if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();

            UiEvents.Ensure();
            return canvas;
        }
    }
}
