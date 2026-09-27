using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 안내 문구 판 (2026-08-26) — <see cref="DebugToast"/> 가 IMGUI로 그리던 것을 월드로 옮긴 것.
    ///
    /// ■ 2026-09-11 ① — <b>판이 하나로 줄었다</b>
    ///   예전에는 두 장이었다. 잠깐 떴다 사라지는 줄은 화면 <b>위</b>(높이 22%)에, 고정 줄은
    ///   <b>아래</b>(바닥 34px)에 떴다. 같은 성격의 안내가 부르는 쪽에 따라 다른 높이에 떠서
    ///   눈이 글을 찾아 헤맸다. 이제 <b>둘 다 아래 한 자리</b>에 뜬다 — 어느 쪽을 부르든
    ///   생김새도 자리도 같다. 무엇을 띄울지 고르는 일은 <see cref="DebugToast"/> 가 한다.
    ///
    /// ■ 2026-09-11 ② — <b>화면 폭을 꽉 채우는 띠</b>
    ///   글 길이에 맞춰 상자가 줄었다 늘었다 하니 안내가 뜰 때마다 화면이 들썩였다.
    ///   이제 뒤판은 <b>화면 좌우 끝까지</b> 가는 고정 폭이고, 바뀌는 것은 <b>높이</b>뿐이다 —
    ///   대화창(하단 가로 바)과 같은 결이다. 글은 그 안에서 가운데 정렬로 앉는다.
    ///
    /// ■ 줄바꿈은 우리가 한다
    ///   TMP 에 맡기면 마지막 줄에 어절 하나만 남는 일이 잦다. <see cref="HangulWrap"/> 이
    ///   <b>어절 단위로 끊고 줄 길이를 고르게</b> 맞춘 뒤 <c>\n</c> 을 박아 넘긴다.
    ///   글자 칸은 띠보다 좁다(<see cref="TextFraction"/>) — 화면 폭 전체로 늘이면 한 줄이
    ///   너무 길어 눈이 되돌아올 곳을 잃는다.
    /// </summary>
    [AddComponentMenu("")]
    public class ToastPanel : VrPanel
    {
        /// <summary>화면 바닥에서 띠 밑변까지(px). 0 = 바닥에 붙는다.</summary>
        public const float BottomMargin = 0f;

        /// <summary>글자 크기(px). 예전 고정판의 값 — 이것이 안내 문구의 '기본 형식'이다.</summary>
        public const int FontSize = 16;

        /// <summary>띠 위아래 여백(px).</summary>
        public const float PadY = 15f;

        /// <summary>글자 칸이 차지하는 화면 폭의 비율. 나머지는 좌우 여백이다.</summary>
        const float TextFraction = 0.68f;

        /// <summary>글자 칸의 상한(px) — 화면이 아무리 넓어도 한 줄이 이보다 길어지지 않는다.</summary>
        const float TextWidthCap = 820f;

        /// <summary>한 줄의 높이(px). 조선 궁서체는 글자 크기의 1.25배다.</summary>
        const float LineStep = 20f;

        string message;          // 부르는 쪽이 준 원문
        string wrapped;          // 줄바꿈을 박은 것 (그리는 것은 이쪽)
        float wrappedFor = -1f;  // 그때의 글자 칸 폭 — 화면이 바뀌면 다시 접는다
        Image box;
        TextMeshProUGUI label;
        Vector2 measured = new Vector2(600f, 48f);

        /// <summary>지금 띠의 높이(px). 아무것도 안 뜨면 0 — 조작 안내가 이만큼 위로 비켜선다.</summary>
        public float Height { get { return Visible ? measured.y : 0f; } }

        public static ToastPanel Create(Transform parent)
        {
            var go = new GameObject("안내_판", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<ToastPanel>();
        }

        /// <summary>비우거나 null이면 판이 통째로 사라진다.</summary>
        public void SetMessage(string msg) { message = msg; }

        /// <summary>글자 칸의 폭(px).</summary>
        float TextWidth { get { return Mathf.Min(TextWidthCap, Mathf.Max(160f, RefW * TextFraction)); } }

        protected override Vector2 PanelSize { get { return measured; } }

        /// <summary>밑변을 바닥에 맞춘다 — 줄이 늘면 위로만 자라고 바닥 여백은 그대로다.</summary>
        protected override Vector2 Pivot { get { return new Vector2(0.5f, 0f); } }
        protected override Vector2 Anchor { get { return new Vector2(0.5f, BottomMargin / RefH); } }

        protected override bool Visible { get { return !string.IsNullOrEmpty(message); } }

        /// <summary>
        /// <see cref="ScreenFader"/> 의 암전 쿼드(<c>Overlay</c> = 4000)보다 <b>뒤에</b> 그린다.
        ///
        /// 왜: 집무실에서 쫓겨나는 연출은 "화면이 어두워진 뒤 관노의 말이 뜬다" 이다. 암전 쿼드는
        /// 근평면 바로 앞(0.05m)에 서고 안내 판은 0.22m 에 서므로, 그냥 두면 <b>캄캄한 화면이
        /// 글을 덮어</b> 아무것도 안 보인다. 이 판의 재질만 큐를 올려 암전 위로 올린다.
        /// (씬 전환 중에 안내가 남아 있으면 암전 위에 비치므로, <see cref="DebugToast"/> 가
        ///  전환을 떠날 때 글을 지운다)
        /// </summary>
        const int QueueAboveFade = ScreenFader.QueueAbove;

        protected override void Build()
        {
            box = MakeBox(root, "바탕", UiSkin.ToastBack);
            Stretch(box.rectTransform, 0f);
            box.material = new Material(box.material) { renderQueue = QueueAboveFade };

            label = MakeText(root, "글", FontSize, TextAnchor.MiddleCenter, UiSkin.ToastText);
            label.fontMaterial.renderQueue = QueueAboveFade;   // fontMaterial 은 이 글만의 사본이다
            label.fontStyle = FontStyles.Bold;
            // 줄바꿈은 HangulWrap 이 이미 박아 넘긴다 — TMP 가 또 끊으면 어절이 갈라진다.
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            Stretch(label.rectTransform, 0f);
        }

        protected override void Refresh()
        {
            if (label == null) return;

            float tw = TextWidth;
            string s = message ?? "";

            // 글이나 화면 폭이 바뀌었을 때만 다시 접는다 — 접는 일은 TMP 측정을 여러 번 부른다
            if (wrapped == null || label.text != wrapped || !Mathf.Approximately(wrappedFor, tw) || LastSource != s)
            {
                LastSource = s;
                wrappedFor = tw;
                wrapped = HangulWrap.Wrap(label, s, tw);
                label.text = wrapped;
            }

            int lines = Lines(wrapped);
            var want = new Vector2(RefW, Mathf.Round(lines * LineStep + PadY * 2f));
            if ((want - measured).sqrMagnitude > 0.01f)
            {
                measured = want;
                root.sizeDelta = measured;
            }
        }

        /// <summary>마지막으로 접은 원문 — 같은 글이면 다시 접지 않는다.</summary>
        string LastSource { get; set; }

        static int Lines(string s)
        {
            if (string.IsNullOrEmpty(s)) return 1;
            int n = 1;
            foreach (var c in s) if (c == '\n') n++;
            return n;
        }
    }
}
