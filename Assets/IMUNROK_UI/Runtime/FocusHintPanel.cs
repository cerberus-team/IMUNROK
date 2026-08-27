using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 포커스 조작 안내 (2026-08-26) — <see cref="DebugFocusRig"/> 의 IMGUI를 월드로 옮긴 것.
    ///
    /// ■ IMGUI 자리를 그대로
    ///   상태 줄 : 화면 바닥에서 53px, 16px, 색 (1, .92, .7, .9)   — 판정 문구가 있을 때만
    ///   조작 줄 : 화면 바닥에서 29px, 14px, 색 흰 55%
    ///   두 줄 다 화면 가로 전체에 가운데 맞춤이었다 — 판 너비를 '화면' 너비로 잡아 재현한다.
    ///
    /// ■ 자리 다툼 규칙도 그대로 옮겼다
    ///   ① 소지품 판(전체 화면 조사 포함)이 떠 있으면 안내를 내린다.
    ///   ② 아래 고정 안내(<see cref="DebugToast.ShowPinned"/>)가 떠 있으면 조작 줄만 비켜 준다.
    ///   이 두 규칙을 빠뜨리면 같은 자리에 글이 두 벌 겹친다 (2026-08-24에 이미 겪은 것).
    /// </summary>
    [AddComponentMenu("")]
    public class FocusHintPanel : VrPanel
    {
        string status, hint;

        TextMeshProUGUI statusText, hintText;

        public static FocusHintPanel Create(Transform parent)
        {
            var go = new GameObject("포커스_안내판", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<FocusHintPanel>();
        }

        public void Set(string status, string hint)
        {
            this.status = status;
            this.hint = hint;
        }

        protected override Vector2 PanelSize { get { return new Vector2(RefW, 80f); } }
        /// <summary>바닥 가운데에 밑변을 붙인다 — 화면 높이가 바뀌어도 두 줄의 자리가 안 흔들린다.</summary>
        protected override Vector2 Pivot { get { return new Vector2(0.5f, 0f); } }
        protected override Vector2 Anchor { get { return new Vector2(0.5f, 0f); } }

        protected override bool Visible
        {
            get { return !string.IsNullOrEmpty(status) || !string.IsNullOrEmpty(hint); }
        }

        protected override void Build()
        {
            statusText = MakeText(root, "상태", 16, TextAnchor.MiddleCenter, UiSkin.StatusText);
            hintText = MakeText(root, "조작", 14, TextAnchor.MiddleCenter, UiSkin.HintText);
        }

        protected override void Refresh()
        {
            // 화면 너비가 바뀌면 판도 따라 넓어져야 가운데 맞춤이 어긋나지 않는다
            float w = RefW;
            if (!Mathf.Approximately(root.sizeDelta.x, w)) root.sizeDelta = new Vector2(w, 80f);

            // 판 밑변이 화면 바닥 — 바닥에서 53px / 29px 자리에 앉힌다 (IMGUI와 같은 높이)
            Place(statusText.rectTransform, new Vector2(0f, 53f - 40f), new Vector2(w, 22f));
            Place(hintText.rectTransform, new Vector2(0f, 29f - 40f), new Vector2(w, 22f));

            string s = status ?? "";
            string h = hint ?? "";
            if (statusText.text != s) statusText.text = s;
            if (hintText.text != h) hintText.text = h;
        }
    }
}
