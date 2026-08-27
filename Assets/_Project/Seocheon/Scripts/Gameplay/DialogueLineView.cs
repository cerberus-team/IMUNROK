// 대화 로그의 한 줄. NPC 발화면 어절 판정 영역을 들고 있으며 로그 어디에 있든 지목됩니다.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 대화 로그 한 줄.
    ///
    /// NPC 발화는 ★WordRegionBuilder 로 어절 판정 영역을 만들어 둡니다.
    /// 이 영역은 줄이 스크롤돼 올라가도 그대로 유효합니다
    /// (판정은 매번 화면좌표 → 이 줄의 로컬좌표로 변환해 수행하므로).
    /// </summary>
    public sealed class DialogueLineView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform highlightRoot;

        private readonly List<WordRegion> regions = new List<WordRegion>();
        private readonly List<Image> hoverPool = new List<Image>();
        private readonly List<Image> markPool = new List<Image>();

        private int hoverIndex = -1;
        private bool isNpcLine;
        private string sourceSentence = string.Empty;

        /// <summary>이 줄이 표시하고 있는 문장 전체. ★수첩에 적히는 문구입니다.</summary>
        public string SourceSentence { get { return sourceSentence; } }

        public TMP_Text Label { get { return label; } }
        public RectTransform Rect { get { return (RectTransform)transform; } }
        public bool IsNpcLine { get { return isNpcLine; } }
        public bool HasPickable { get { return regions.Count > 0; } }

        /// <summary>플레이어 발화 — 판정 영역 없음.</summary>
        public void BindPlayer(string text, Color color)
        {
            isNpcLine = false;
            regions.Clear();
            if (label != null) { label.text = text ?? string.Empty; label.color = color; }
            ClearPool(hoverPool);
            ClearPool(markPool);
        }

        /// <summary>NPC 발화 — 텍스트만 먼저 넣습니다. 영역은 레이아웃이 확정된 뒤 BuildRegions 로.</summary>
        public void BindNpc(string text, Color color)
        {
            isNpcLine = true;
            regions.Clear();
            if (label != null) { label.text = text ?? string.Empty; label.color = color; }
            ClearPool(hoverPool);
            ClearPool(markPool);
        }

        /// <summary>지난 발화를 흐리게 하는 등 색만 바꿉니다. 판정 영역에는 영향이 없습니다.</summary>
        public void SetTextColor(Color color)
        {
            if (label != null) label.color = color;
        }

        /// <summary>레이아웃·메시가 확정된 뒤에 호출해야 합니다.</summary>
        public void BuildRegions(WordPickNote.Sentence sentence, float padX, float padY)
        {
            regions.Clear();
            hoverIndex = -1;
            sourceSentence = sentence != null ? (sentence.text ?? string.Empty) : string.Empty;
            if (!isNpcLine || label == null) return;
            label.ForceMeshUpdate();
            WordRegionBuilder.Build(label, sentence, padX, padY, regions);
        }

        /// <summary>화면 좌표가 이 줄의 어느 어절 위인지. 없으면 -1.</summary>
        public int HitTest(Vector2 screenPoint, Camera uiCamera)
        {
            if (!isNpcLine || regions.Count == 0 || label == null) return -1;

            // 줄 바깥이면 즉시 탈락 — 로그가 길어져도 비용이 늘지 않게.
            if (!RectTransformUtility.RectangleContainsScreenPoint(Rect, screenPoint, uiCamera)) return -1;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    label.rectTransform, screenPoint, uiCamera, out local)) return -1;

            return WordRegionBuilder.Find(regions, local);
        }

        public WordPickNote.WordOption GetOption(int index)
        {
            if (index < 0 || index >= regions.Count) return null;
            return regions[index].option;
        }

        public void SetHover(int index, Color color)
        {
            if (hoverIndex == index) return;
            hoverIndex = index;
            ClearPool(hoverPool);
            if (index < 0 || index >= regions.Count || highlightRoot == null) return;
            DrawRects(regions[index].rects, hoverPool, color);
        }

        /// <summary>지목 완료 표시. ★유효/오답 구분 없이 같은 색입니다.</summary>
        public void MarkConsumed(int index, Color color)
        {
            if (index < 0 || index >= regions.Count) return;
            regions[index].consumed = true;
            hoverIndex = -1;
            ClearPool(hoverPool);

            // 이미 찍힌 표시는 지우지 않고 그 위에 덧붙입니다.
            List<Rect> rects = regions[index].rects;
            for (int i = 0; i < rects.Count; i++)
            {
                Image img = MakeImage(markPool, markPool.Count);
                PlaceUnderline(img, rects[i], color);
                img.gameObject.SetActive(true);
            }
        }

        // ── 그리기 ──

        private void DrawRects(List<Rect> rects, List<Image> pool, Color color)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                Image img = MakeImage(pool, i);
                RectTransform rt = img.rectTransform;
                rt.pivot = Vector2.zero;
                rt.sizeDelta = new Vector2(rects[i].width, rects[i].height);
                rt.position = label.rectTransform.TransformPoint(new Vector3(rects[i].x, rects[i].y, 0f));
                img.color = color;
                img.gameObject.SetActive(true);
            }
        }

        /// <summary>지목 완료는 상자가 아니라 ★얇은 밑줄로 — 최소한의 표시.</summary>
        private void PlaceUnderline(Image img, Rect r, Color color)
        {
            RectTransform rt = img.rectTransform;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(r.width, 2f);
            rt.position = label.rectTransform.TransformPoint(new Vector3(r.x, r.y + 2f, 0f));
            img.color = color;
        }

        private Image MakeImage(List<Image> pool, int index)
        {
            while (pool.Count <= index)
            {
                GameObject go = new GameObject("Mark", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(highlightRoot != null ? highlightRoot : transform, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                Image img = go.GetComponent<Image>();
                img.raycastTarget = false;
                go.SetActive(false);
                pool.Add(img);
            }
            return pool[index];
        }

        private static void ClearPool(List<Image> pool)
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null) pool[i].gameObject.SetActive(false);
        }
    }
}
