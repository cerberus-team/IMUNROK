using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 조준점 + 행동 문구 (2026-08-26) — <see cref="DebugInteractor"/> 의 IMGUI를 월드로 옮긴 것.
    ///
    /// ■ IMGUI 자리를 그대로
    ///   조준점 : 화면 정중앙, 40×40 칸에 20px 글리프 (◆ 노랑 / · 흰 45%)
    ///   문구   : 중앙에서 아래로 27px, 240×22, 15px 노랑
    ///
    /// ■ VR에서 달라지는 것
    ///   판 자체는 같지만 <see cref="UiTuning"/> 이 VR 배율을 주므로 글자가 1.6배쯤 커지고,
    ///   판이 시야를 느슨히 따라간다. <b>조준 광선은 <see cref="UiPointers"/> 가 정한다</b> —
    ///   PC는 화면 정중앙 시선, VR은 컨트롤러 광선이다. 그래서 VR에서는 조준점이
    ///   화면 한가운데가 아니라 <b>광선이 맞은 자리</b>에 있어야 맞다.
    ///   ⚠️ 지금은 화면 한가운데에 그린다 — VR에서 광선 끝에 붙이는 것은 컨트롤러가 실제로
    ///      잡히는 걸 보고 맞춰야 해서 미완이다. <see cref="FocusReticle"/>(퍼즐용 월드 조준점)이
    ///      이미 "맞은 자리에 찍는" 방식이라, 리그가 붙으면 그쪽 방식을 여기로 가져오면 된다.
    /// </summary>
    [AddComponentMenu("")]
    public class AimPanel : VrPanel
    {
        string label;
        bool hot;

        TextMeshProUGUI dot, text;

        public static AimPanel Create(Transform parent)
        {
            var go = new GameObject("조준_판", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<AimPanel>();
        }

        /// <summary><paramref name="prompt"/> 가 비어 있으면 조준점만 흐리게 뜬다.</summary>
        public void SetTarget(string prompt)
        {
            label = prompt;
            hot = !string.IsNullOrEmpty(prompt);
        }

        protected override Vector2 PanelSize { get { return new Vector2(240f, 90f); } }
        protected override Vector2 Anchor { get { return new Vector2(0.5f, 0.5f); } }

        protected override void Build()
        {
            dot = MakeText(root, "조준점", 20, TextAnchor.MiddleCenter, UiSkin.AimIdle);
            // ⚠️ 글자를 **줄 상자**가 아니라 **글리프 모양** 기준으로 가운데 맞춘다.
            //    이걸 안 켜면 ◆ 가 글꼴의 베이스라인 규칙을 따라 화면 중심에서 4~5px 내려앉는다
            //    (2026-08-26 실측: 세로 +4.5px). 조준점은 겨눈 자리를 가리키는 것이라
            //    몇 픽셀 어긋나도 곧바로 티가 난다.
            //    TMP 로 옮기며 이름이 바뀌었다 — 레거시의 alignByGeometry 에 해당하는 것이
            //    세로 정렬 <c>Midline</c>(글자 모양의 중간선에 맞춤)이다.
            dot.alignment = TextAlignmentOptions.Midline;
            Place(dot.rectTransform, Vector2.zero, new Vector2(40f, 40f));
            dot.text = "·";

            text = MakeText(root, "문구", 15, TextAnchor.MiddleCenter, UiSkin.AimHot);
            Place(text.rectTransform, new Vector2(0f, -27f), new Vector2(240f, 22f));
        }

        protected override void Refresh()
        {
            if (dot != null)
            {
                string want = hot ? "◆" : "·";
                if (dot.text != want) dot.text = want;
                var c = hot ? UiSkin.AimHot : UiSkin.AimIdle;
                if (dot.color != c) dot.color = c;
            }
            if (text != null)
            {
                string want = label ?? "";
                if (text.text != want) text.text = want;
            }
        }
    }
}
