using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 곁글 판 (2026-08-26) — 퍼즐 옆에 붙는 <b>여러 줄 글상자</b>.
    /// <see cref="LensPuzzle"/> 의 힌트와 <see cref="LedgerPuzzle"/> 의 「선아의 메모」가
    /// IMGUI로 그리던 것을 하나로 합쳤다. 둘 다 "어두운 상자 + 줄바꿈 글"이라 같은 판으로 충분하다.
    ///
    /// ■ PC에서 같아 보이게
    ///   두 IMGUI의 <c>Rect</c>·글자 크기·색·여백을 그대로 옮겨 담을 수 있게 값을 다 밖으로 뺐다.
    ///     렌즈 : x26 y26 폭520, 위아래 여백 14, 본문 17px 굵게, 제목 없음
    ///     장부 : x22 y=화면높이22% 폭 min(360, 너비26%), 제목 15px + 본문 14px
    ///
    /// ■ 높이는 글이 정한다
    ///   IMGUI는 <c>CalcHeight</c> 로 쟀다. 여기서는 <c>Text.preferredHeight</c> 로 재고
    ///   <b>매 프레임 판 크기를 다시 잡는다</b> — 글이 바뀌면 상자도 따라 자란다.
    /// </summary>
    [AddComponentMenu("")]
    public class NotePanel : VrPanel
    {
        // ── 설정 (부르는 쪽이 채운다) ─────────────────────────
        public string title;                 // null이면 제목 줄이 없다
        public string body;
        public float width = 520f;
        /// <summary>왼쪽 위 자리(px). <see cref="topFraction"/> 이 0보다 크면 y는 무시된다.</summary>
        public Vector2 topLeftPx = new Vector2(26f, 26f);
        /// <summary>위 자리를 화면 높이 비율로 잡을 때 (장부 메모 = 0.22). 0이면 픽셀을 쓴다.</summary>
        public float topFraction;
        public int headSize = 15;
        public int bodySize = 17;
        public bool bodyBold;
        public float padX = 14f, padTop = 14f, padBottom = 14f;
        /// <summary>제목 줄 높이 + 제목~본문 사이. 제목이 있을 때만 쓴다.</summary>
        public float titleBlock = 26f;
        public Color backColor = UiSkin.NoteBack;
        public Color headColor = UiSkin.NoteHead;
        public Color bodyColor = UiSkin.NoteBody;
        /// <summary>전체 투명도 — 렌즈 힌트의 서서히 뜨는 연출에 쓴다.</summary>
        public float alpha = 1f;

        Image box;
        TextMeshProUGUI head, text;

        /// <summary>'화면' 가로 단위 수 — 폭을 화면 비율로 잡는 판(장부 메모)이 쓴다.
        /// PC = Screen.width, VR = 고정 기준값이라 부르는 쪽이 Screen을 직접 보면 안 된다.</summary>
        public float ScreenWidthUnits { get { return RefW; } }

        public static NotePanel Create(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<NotePanel>();
        }

        protected override Vector2 PanelSize { get { return new Vector2(width, 80f); } }
        protected override Vector2 Pivot { get { return new Vector2(0f, 1f); } }   // 왼쪽 위 — IMGUI Rect와 같은 기준

        protected override Vector2 Anchor
        {
            get
            {
                float y = topFraction > 0f ? RefH * topFraction : topLeftPx.y;
                return FromTopLeft(topLeftPx.x, y);
            }
        }

        protected override bool Visible { get { return !string.IsNullOrEmpty(body) && alpha > 0.01f; } }

        protected override void Build()
        {
            box = MakeBox(root, "바탕", backColor);
            Stretch(box.rectTransform, 0f);

            head = MakeText(root, "제목", headSize, TextAnchor.UpperLeft, headColor);
            head.fontStyle = FontStyles.Bold;
            head.textWrappingMode = TextWrappingModes.Normal;
            head.overflowMode = TextOverflowModes.Overflow;

            text = MakeText(root, "글", bodySize, TextAnchor.UpperLeft, bodyColor);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        protected override void Refresh()
        {
            if (text == null) return;

            bool hasTitle = !string.IsNullOrEmpty(title);
            float innerW = Mathf.Max(20f, width - padX * 2f);
            float bodyTop = padTop + (hasTitle ? titleBlock + 10f : 0f);

            if (text.fontSize != bodySize) text.fontSize = bodySize;
            text.fontStyle = bodyBold ? FontStyles.Bold : FontStyles.Normal;
            if (text.text != (body ?? "")) text.text = body ?? "";

            // 폭을 먼저 확정해야 preferredHeight 가 줄바꿈을 반영한다
            var trt = text.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.sizeDelta = new Vector2(innerW, 0f);
            float bodyH = Mathf.Max(bodySize + 4f, text.preferredHeight);
            trt.sizeDelta = new Vector2(innerW, bodyH);
            trt.anchoredPosition = new Vector2(padX, -bodyTop);

            float total = bodyTop + bodyH + padBottom;
            root.sizeDelta = new Vector2(width, total);

            head.gameObject.SetActive(hasTitle);
            if (hasTitle)
            {
                if (head.fontSize != headSize) head.fontSize = headSize;
                if (head.text != title) head.text = title;
                if (head.color != Fade(headColor)) head.color = Fade(headColor);
                var hrt = head.rectTransform;
                hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f);
                hrt.pivot = new Vector2(0f, 1f);
                hrt.sizeDelta = new Vector2(innerW, 22f);
                hrt.anchoredPosition = new Vector2(padX, -padTop);
            }

            if (box != null && box.color != Fade(backColor)) box.color = Fade(backColor);
            if (text.color != Fade(bodyColor)) text.color = Fade(bodyColor);
        }

        Color Fade(Color c) { c.a *= Mathf.Clamp01(alpha); return c; }
    }
}
