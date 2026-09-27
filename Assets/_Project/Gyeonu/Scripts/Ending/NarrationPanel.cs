using UnityEngine;
using TMPro;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 검은 화면 위의 나레이션 판 (2026-09-13). 엔딩의 "화면이 완전히 검어진 뒤 글로 결말을 전한다"를 그린다.
    ///
    /// ■ 안내 판(<see cref="ToastPanel"/>)과 다른 점
    ///   안내 판은 화면 아래 띠에 조작 안내를 적는다. 이 판은 <b>화면 한가운데</b>에, 뒤판 없이, 더 큰 글자로
    ///   문단 하나를 앉힌다. 문단은 <see cref="EndingDirector"/> 가 하나씩 넘긴다 — 판은 글과 투명도만 안다.
    ///
    /// ■ 암전 위에 그린다
    ///   <see cref="ScreenFader"/> 의 암전 쿼드(Overlay 4000)보다 뒤에 그려야 캄캄한 화면에 글이 비친다.
    ///   안내 판과 같은 방법 — 글 재질의 렌더 큐를 <see cref="ScreenFader.QueueAbove"/> 로 올린다.
    ///
    /// ■ 줄바꿈
    ///   글쓴이의 <c>\n</c> 은 문단 안의 줄 경계로 지키고, 그 안에서 <see cref="HangulWrap"/> 이 어절 단위로 접는다.
    /// </summary>
    [AddComponentMenu("")]
    public class NarrationPanel : VrPanel
    {
        /// <summary>나레이션 본문 글자 크기(px).</summary>
        public const int BodySize = 21;
        /// <summary>엔딩 제목 글자 크기(px).</summary>
        public const int TitleSize = 34;

        const float TextFraction = 0.62f;
        const float TextWidthCap = 760f;
        const float PadX = 24f, PadY = 18f;

        string text;
        int size = BodySize;
        float alpha = 1f;
        string wrapped;
        float wrappedFor = -1f;
        int wrappedSize = -1;
        TextMeshProUGUI label;
        Vector2 measured = new Vector2(760f, 120f);

        public static NarrationPanel Create(Transform parent)
        {
            var go = new GameObject("나레이션_판", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<NarrationPanel>();
        }

        /// <summary>글을 바꾼다. 비우면 판이 사라진다.</summary>
        public void Set(string body, int fontSize)
        {
            text = body;
            size = fontSize;
        }

        public void Clear() { text = null; }

        /// <summary>0 = 안 보임, 1 = 다 보임. 감독이 문단마다 서서히 밝혔다 어둡힌다.</summary>
        public void SetAlpha(float a) { alpha = Mathf.Clamp01(a); }

        float TextWidth { get { return Mathf.Min(TextWidthCap, Mathf.Max(200f, RefW * TextFraction)); } }
        float LineStep { get { return Mathf.Round(size * 1.25f + 4f); } }

        protected override Vector2 PanelSize { get { return measured; } }
        protected override Vector2 Anchor { get { return new Vector2(0.5f, 0.5f); } }
        protected override Vector2 Pivot { get { return new Vector2(0.5f, 0.5f); } }
        protected override bool Visible { get { return !string.IsNullOrEmpty(text) && alpha > 0.004f; } }

        protected override void Build()
        {
            label = MakeText(root, "글", BodySize, TextAnchor.MiddleCenter, UiSkin.ToastText);
            label.fontMaterial.renderQueue = ScreenFader.QueueAbove;   // fontMaterial 은 이 글만의 사본이다
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.lineSpacing = 6f;
            Stretch(label.rectTransform, 0f);
        }

        protected override void Refresh()
        {
            if (label == null) return;

            float tw = TextWidth;
            string s = text ?? "";

            if (wrappedSize != size)
            {
                wrappedSize = size;
                label.fontSize = size * UiSkin.FontScale;
                wrapped = null;                       // 크기가 바뀌면 폭도 바뀐다 — 다시 접는다
            }

            if (wrapped == null || LastSource != s || !Mathf.Approximately(wrappedFor, tw))
            {
                LastSource = s;
                wrappedFor = tw;
                wrapped = HangulWrap.Wrap(label, s, tw);
                label.text = wrapped;
            }

            var c = label.color; c.a = alpha; label.color = c;

            int lines = Lines(wrapped);
            var want = new Vector2(Mathf.Round(tw + PadX * 2f), Mathf.Round(lines * LineStep + PadY * 2f));
            if ((want - measured).sqrMagnitude > 0.01f)
            {
                measured = want;
                root.sizeDelta = measured;
            }
        }

        string LastSource { get; set; }

        static int Lines(string s)
        {
            if (string.IsNullOrEmpty(s)) return 1;
            int n = 1;
            foreach (var ch in s) if (ch == '\n') n++;
            return n;
        }
    }
}
