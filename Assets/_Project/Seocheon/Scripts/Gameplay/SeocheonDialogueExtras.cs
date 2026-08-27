using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IMUNROK.Ui;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 견우 대화창(<see cref="DialogueUI"/>) 위에 <b>서천에만 있는 것</b>을 얹는다 (2026-08-26).
    ///
    /// ■ 무엇을 얹는가
    ///   ① <b>선택지 4개</b> — VR 에서는 글쇠를 칠 수 없다. 서천의 기본 입력이다.
    ///   ② <b>워드픽</b> — 대사에서 낱말을 짚어 단서로 모은다.
    ///   ③ <b>「증거 제시」 단추 감추기</b> — 서천에는 제시가 없다
    ///      (<see cref="SeocheonDialogueBackend.Present"/> 가 늘 false 다).
    ///      꾸러미는 목록이 비어도 단추를 계속 그리므로 여기서 꺼 준다.
    ///
    /// ■ ★꾸러미 파일은 건드리지 않는다
    ///   문서에 "꾸러미는 <c>Tools ▸ 이문록 ▸ UI 패키지 내보내기</c> 로 언제든 다시 구워진다"
    ///   고 적혀 있다 — 그쪽을 고치면 <b>다음 배포본에 덮여 사라진다</b>.
    ///   그래서 판 안의 요소는 <b>한글 이름으로 찾아</b>(꾸러미가 그렇게 지어 둔다) 쓰고,
    ///   우리 것은 판의 자식으로 새로 만들어 붙인다.
    ///
    /// ■ 선택지를 왜 바 <b>안</b>이 아니라 <b>위</b>에 두는가
    ///   꾸러미 바의 높이(PC 461 / VR 535)는 여백의 <b>합으로 계산된</b> 값이다
    ///   (<c>BottomStyle.TotalHeight</c>). 안에 끼워 넣으면 그 계산이 어긋나 안내줄이 판 밖으로 나간다.
    ///   위에 별도 띠로 얹으면 꾸러미 판은 <b>손댈 것이 없다</b>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeocheonDialogueExtras : MonoBehaviour
    {
        // ── 꾸러미가 지어 둔 요소의 이름 (DialogueUI.BuildBottom 참고) ──
        private const string NameOfLine = "대사";
        private const string NameOfPresent = "증거제시";

        /// <summary>선택지가 눌렸다 — (label, tone).</summary>
        public event Action<string, string> AskChosen;
        /// <summary>낱말을 지목했다 — (option, 그 문장 전체).</summary>
        public event Action<WordPickNote.WordOption, string> WordPicked;

        [Tooltip("지목 강조색. ★유효/오답을 가르지 않는다 — 지목 시점에는 판정하지 않는다")]
        [SerializeField] private Color hoverColor = new Color(1f, 0.85f, 0.4f, 0.28f);
        [Tooltip("지목 완료 밑줄. ★유효/오답 동일")]
        [SerializeField] private Color consumedColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private float padX = 3f;
        [SerializeField] private float padY = 2f;

        // ── 얹은 것 ──
        private RectTransform askStrip;
        private readonly List<Image> askBoxes = new List<Image>();
        private readonly List<TextMeshProUGUI> askLabels = new List<TextMeshProUGUI>();
        private readonly List<string> askTones = new List<string>();

        // ── 워드픽 ──
        private TMP_Text lineLabel;
        private RectTransform highlightRoot;
        private readonly List<WordRegion> regions = new List<WordRegion>();
        private readonly List<Image> hoverMarks = new List<Image>();
        private WordPickNote.Sentence merged;
        private string mergedText = string.Empty;
        private int hoverIndex = -1;

        private RectTransform Root { get { return (RectTransform)transform; } }

        /// <summary>
        /// 판이 다시 지어졌는지 — F8 로 모드를 바꾸면 꾸러미가 판을 <b>통째로 다시 짓는다</b>
        /// (PC 2900 ↔ VR 1500 은 치수가 아예 다르다). 그때 우리가 얹은 것도 같이 사라지므로
        /// 매 프레임 살아 있는지 보고 없으면 다시 얹는다.
        /// </summary>
        private void LateUpdate()
        {
            if (lineLabel == null || askStrip == null) Reattach();
            if (lineLabel == null) return;

            HidePresentButton();
            UpdateHover();
        }

        // ─────────────────────────────────────────────────────────
        //  얹기
        // ─────────────────────────────────────────────────────────
        private void Reattach()
        {
            lineLabel = FindByName<TMP_Text>(NameOfLine);
            if (lineLabel == null) return;

            if (highlightRoot == null)
            {
                var go = new GameObject("서천_지목", typeof(RectTransform));
                highlightRoot = (RectTransform)go.transform;
                highlightRoot.SetParent(lineLabel.rectTransform, false);
                Stretch(highlightRoot);
                highlightRoot.SetAsFirstSibling();      // 글자 뒤에 깔린다
            }

            if (askStrip == null) BuildAskStrip();
        }

        /// <summary>서천에는 증거 제시가 없다 — 꾸러미가 그린 단추를 끈다.</summary>
        private void HidePresentButton()
        {
            var t = FindTransformByName(NameOfPresent);
            if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false);
        }

        /// <summary>선택지 띠 — 꾸러미 바의 <b>위쪽 바깥</b>에 2×2 로 앉힌다.</summary>
        private void BuildAskStrip()
        {
            var pal = DialogueUI.Palette();
            Vector2 bar = Root.sizeDelta;
            bool vr = UiModes.IsVr;

            float padSide = vr ? 60f : 90f;
            float w = bar.x - padSide * 2f;
            int fontSize = vr ? 34 : 28;
            float rowH = vr ? 76f : 62f;
            float gap = 12f;
            float stripH = rowH * 2f + gap;
            // 바 위쪽 바깥 — 바 윗변에서 gap 만큼 띄운다
            float stripY = bar.y * 0.5f + gap + stripH * 0.5f;

            var stripGo = new GameObject("서천_선택지", typeof(RectTransform));
            askStrip = (RectTransform)stripGo.transform;
            askStrip.SetParent(Root, false);
            askStrip.sizeDelta = new Vector2(w, stripH);
            askStrip.anchoredPosition = new Vector2(0f, stripY);

            askBoxes.Clear();
            askLabels.Clear();
            float cellW = (w - gap) * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                int col = i % 2, row = i / 2;
                float x = -w * 0.5f + cellW * 0.5f + col * (cellW + gap);
                float y = stripH * 0.5f - rowH * 0.5f - row * (rowH + gap);

                var boxGo = new GameObject("선택지" + i, typeof(RectTransform), typeof(Image));
                var box = boxGo.GetComponent<Image>();
                var brt = (RectTransform)boxGo.transform;
                brt.SetParent(askStrip, false);
                brt.sizeDelta = new Vector2(cellW, rowH);
                brt.anchoredPosition = new Vector2(x, y);
                box.color = pal.slotBack;                     // 글쇠 칸과 같은 낯 — 누를 자리임을 알린다
                box.raycastTarget = true;

                var lab = MakeText(brt, pal.slotText, fontSize);
                askBoxes.Add(box);
                askLabels.Add(lab);

                int captured = i;
                var btn = boxGo.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => OnAskClicked(captured));
            }
            SetAsks(null, null);
        }

        private TextMeshProUGUI MakeText(RectTransform parent, Color color, int size)
        {
            var go = new GameObject("글", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = UiSkin.Font;                              // ★조선 궁서체 — 꾸러미 것을 그대로 쓴다
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;       // ★Truncate 는 줄을 통째로 버린다
            return t;
        }

        // ─────────────────────────────────────────────────────────
        //  선택지
        // ─────────────────────────────────────────────────────────
        /// <summary>선택지 문구를 갈아 끼운다. 빈 자리는 감춘다.</summary>
        public void SetAsks(IList<string> labels, IList<string> tones)
        {
            askTones.Clear();
            for (int i = 0; i < askBoxes.Count; i++)
            {
                bool has = labels != null && i < labels.Count && !string.IsNullOrEmpty(labels[i]);
                askBoxes[i].gameObject.SetActive(has);
                if (!has) { askTones.Add(string.Empty); continue; }
                askLabels[i].text = labels[i];
                askTones.Add(tones != null && i < tones.Count ? tones[i] : string.Empty);
            }
        }

        private void OnAskClicked(int i)
        {
            if (i < 0 || i >= askLabels.Count) return;
            if (AskChosen != null)
                AskChosen(askLabels[i].text, i < askTones.Count ? askTones[i] : string.Empty);
        }

        // ─────────────────────────────────────────────────────────
        //  워드픽
        // ─────────────────────────────────────────────────────────
        /// <summary>
        /// 새 대꾸가 왔다 — 문장들을 <b>하나로 이어</b> 지목 영역을 다시 만든다.
        /// 꾸러미 대화창은 대사를 한 덩어리(<c>CurrentLine</c>)로 그리므로,
        /// 문장별 후보(<c>options</c>)도 합쳐서 한 벌로 넘긴다.
        /// </summary>
        public void SetSentences(IReadOnlyList<WordPickNote.Sentence> sentences)
        {
            var opts = new List<WordPickNote.WordOption>();
            var sb = new System.Text.StringBuilder();
            if (sentences != null)
                for (int i = 0; i < sentences.Count; i++)
                {
                    var s = sentences[i];
                    if (s == null || string.IsNullOrEmpty(s.text)) continue;
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(s.text);
                    if (s.options != null) opts.AddRange(s.options);
                }

            mergedText = sb.ToString();
            merged = new WordPickNote.Sentence { text = mergedText, options = opts.ToArray() };
            hoverIndex = -1;
            RebuildRegions();
        }

        private void RebuildRegions()
        {
            regions.Clear();
            ClearMarks();
            if (lineLabel == null || merged == null) return;

            lineLabel.ForceMeshUpdate();                        // ★Build 전에 끝나 있어야 한다
            WordRegionBuilder.Build(lineLabel, merged, padX, padY, regions);
        }

        private void UpdateHover()
        {
            if (regions.Count == 0) return;

            Vector2 screen = Pointer();
            var rt = lineLabel.rectTransform;
            Camera cam = CanvasCamera();
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, cam, out local))
            { SetHover(-1); return; }

            int hit = WordRegionBuilder.Find(regions, local);
            SetHover(hit);

            if (hit >= 0 && Clicked()) Pick(hit);
        }

        private void Pick(int index)
        {
            if (index < 0 || index >= regions.Count) return;
            var reg = regions[index];
            if (reg.consumed) return;
            reg.consumed = true;
            SetHover(-1);
            Mark(reg, consumedColor);
            if (WordPicked != null) WordPicked(reg.option, mergedText);
        }

        private void SetHover(int index)
        {
            if (hoverIndex == index) return;
            hoverIndex = index;
            ClearMarks();
            if (index >= 0 && index < regions.Count) Mark(regions[index], hoverColor);
        }

        private void Mark(WordRegion reg, Color c)
        {
            if (highlightRoot == null || reg == null) return;
            for (int i = 0; i < reg.rects.Count; i++)
            {
                var go = new GameObject("표시", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(highlightRoot, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                Rect r = reg.rects[i];
                rt.anchoredPosition = new Vector2(r.xMin, r.yMin);
                rt.sizeDelta = new Vector2(r.width, r.height);
                var im = go.GetComponent<Image>();
                im.color = c;
                im.raycastTarget = false;
                hoverMarks.Add(im);
            }
        }

        private void ClearMarks()
        {
            for (int i = 0; i < hoverMarks.Count; i++)
                if (hoverMarks[i] != null) Destroy(hoverMarks[i].gameObject);
            hoverMarks.Clear();
        }

        // ─────────────────────────────────────────────────────────
        //  잡동사니
        // ─────────────────────────────────────────────────────────
        private static Vector2 Pointer()
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null ? m.position.ReadValue() : Vector2.zero;
        }

        private static bool Clicked()
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
        }

        /// <summary>월드 캔버스는 카메라를 넘겨야 좌표가 맞는다.</summary>
        private Camera CanvasCamera()
        {
            var cv = GetComponentInParent<Canvas>();
            if (cv == null) return null;
            if (cv.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return cv.worldCamera != null ? cv.worldCamera : Camera.main;
        }

        private T FindByName<T>(string n) where T : Component
        {
            var t = FindTransformByName(n);
            return t != null ? t.GetComponent<T>() : null;
        }

        private Transform FindTransformByName(string n)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
            return null;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
