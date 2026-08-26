// TMP 텍스트 한 장에 문장을 통째로 그리고, 글자 인덱스 구간으로 어절 클릭 영역을 만들어 판정합니다.
using System;
using System.Collections.Generic;
using IMUNROK.Seocheon.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 어절 지목 UI.
    ///
    /// ★판정 방식: 문장 전체를 ★TMP 하나로 그리고, TMP_TextInfo 의 ★글자 인덱스 구간으로
    ///   클릭 영역(Rect)을 만들어 직접 히트 테스트합니다.
    ///   - 어절마다 TMP 오브젝트를 쪼개지 않습니다 → 유효/오답이 ★완전히 같은 그림으로 나옵니다.
    ///   - TMP 의 wordInfo(공백 단위 낱말)는 쓰지 않습니다. "6개월 전부터요." 처럼
    ///     ★낱말 경계와 어긋나는 어절을 잡을 수 없기 때문입니다.
    ///
    /// ★강조는 hover 에만 걸리며 색·크기가 유효 여부와 무관하게 동일합니다.
    /// ★uGUI Button / EventSystem 을 쓰지 않습니다(입력은 전부 SeocheonInput 경유).
    /// ★인스펙터 데이터에 직접 접근하지 않습니다. Sentence 하나만 받아서 그립니다.
    /// </summary>
    public sealed class WordPickUI : MonoBehaviour
    {
        [Header("표시")]
        [Tooltip("전체 문장을 표시할 TMP 텍스트")]
        [SerializeField] private TMP_Text sentenceText;
        [Tooltip("화자 이름 라벨. 비워도 됩니다")]
        [SerializeField] private TMP_Text speakerLabel;
        [Tooltip("진행 표시 라벨(1/4). 비워도 됩니다")]
        [SerializeField] private TMP_Text progressLabel;

        [Header("지목 강조")]
        [Tooltip("hover 강조 사각형이 생성될 부모. 문장 텍스트보다 뒤에 그려져야 합니다")]
        [SerializeField] private RectTransform highlightRoot;
        [Tooltip("hover 강조 색. ★유효/오답 구분 없이 이 색 하나만 씁니다")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.4f, 0.28f);
        [Tooltip("클릭 영역 가로 여유(px)")]
        [SerializeField] private float paddingX = 3f;
        [Tooltip("클릭 영역 세로 여유(px)")]
        [SerializeField] private float paddingY = 2f;

        [Header("건너뛰기")]
        [Tooltip("짚을 것이 없을 때 누르는 영역")]
        [SerializeField] private RectTransform skipButton;
        [Tooltip("건너뛰기 영역의 배경 이미지. hover 시 색이 바뀝니다")]
        [SerializeField] private Graphic skipBackground;
        [SerializeField] private Color skipNormalColor = new Color(1f, 1f, 1f, 0.10f);
        [SerializeField] private Color skipHoverColor = new Color(1f, 1f, 1f, 0.22f);

        [Header("동작")]
        [Tooltip("UI 가 열려 있는 동안 플레이어 이동·시점을 잠급니다")]
        [SerializeField] private bool lockPlayerWhileOpen = true;

        private readonly List<WordRegion> regions = new List<WordRegion>();
        private readonly List<Image> highlightPool = new List<Image>();

        private Action<WordPickNote.WordOption> onPicked;
        private Action onSkipped;
        private Action onFallback;

        private Canvas rootCanvas;
        private Camera uiCamera;
        private int hoverIndex = -1;
        private bool isOpen;
        private bool playerLocked;

        private void Awake()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            CacheUiCamera();
            gameObject.SetActive(false);
        }

        private void CacheUiCamera()
        {
            // Overlay 캔버스는 카메라가 null 이어야 좌표 변환이 맞습니다.
            if (rootCanvas == null) { uiCamera = null; return; }
            uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        }

        // ─────────────────────────────────────────────
        //  열기 / 닫기
        // ─────────────────────────────────────────────

        /// <summary>
        /// 문장 하나를 표시합니다. 문장의 출처(인스펙터 / AI)는 알지 못합니다.
        /// </summary>
        /// <param name="onPickedCallback">어절을 지목했을 때</param>
        /// <param name="onSkippedCallback">"짚을 것이 없소" 를 눌렀을 때</param>
        /// <param name="onFallbackCallback">★어절 판정이 불가능할 때(문장 전체 기록용)</param>
        public void Show(WordPickNote.Sentence sentence, string speakerName,
                         int index, int total,
                         Action<WordPickNote.WordOption> onPickedCallback,
                         Action onSkippedCallback,
                         Action onFallbackCallback)
        {
            onPicked = onPickedCallback;
            onSkipped = onSkippedCallback;
            onFallback = onFallbackCallback;

            if (sentence == null || string.IsNullOrEmpty(sentence.text))
            {
                // 그릴 것이 없으면 그냥 넘깁니다(멈추지 않음).
                InvokeSkip();
                return;
            }

            if (sentenceText == null)
            {
                Debug.LogWarning("[WordPickUI] sentenceText 가 연결되지 않았습니다. 문장 전체 기록으로 넘어갑니다.", this);
                InvokeFallback();
                return;
            }

            gameObject.SetActive(true);
            if (rootCanvas == null) { rootCanvas = GetComponentInParent<Canvas>(); CacheUiCamera(); }
            LockPlayer();

            if (speakerLabel != null) speakerLabel.text = speakerName ?? string.Empty;
            if (progressLabel != null) progressLabel.text = (index + 1) + " / " + total;

            sentenceText.text = sentence.text;
            sentenceText.ForceMeshUpdate();

            // ★판정은 공용 WordRegionBuilder 하나만 씁니다(대화 로그와 동일 코드).
            WordRegionBuilder.Build(sentenceText, sentence, paddingX, paddingY, regions);

            // ★폴백: 지목할 어절이 등록돼 있는데 판정 영역이 하나도 안 잡히면
            //   문장 전체를 수첩에 넣고 넘어갑니다. 여기서 멈추면 안 됩니다.
            if (regions.Count == 0 && sentence.options != null && sentence.options.Length > 0)
            {
                InvokeFallback();
                return;
            }

            hoverIndex = -1;
            ClearHighlights();
            if (skipBackground != null) skipBackground.color = skipNormalColor;
            isOpen = true;
        }

        public void Hide()
        {
            isOpen = false;
            hoverIndex = -1;
            regions.Clear();
            ClearHighlights();
            onPicked = null;
            onSkipped = null;
            onFallback = null;
            UnlockPlayer();
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // 씬 전환·비활성으로 닫힐 때 조작이 잠긴 채 남지 않도록.
            isOpen = false;
            UnlockPlayer();
        }


        // ─────────────────────────────────────────────
        //  입력 (SeocheonInput 경유)
        // ─────────────────────────────────────────────

        private void Update()
        {
            if (!isOpen) return;

            Vector2 pointer = SeocheonInput.PointerPosition;

            // 1) 어절 hover
            int found = -1;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    sentenceText.rectTransform, pointer, uiCamera, out local))
                found = WordRegionBuilder.Find(regions, local);

            if (found != hoverIndex)
            {
                hoverIndex = found;
                RefreshHighlights();
            }

            // 2) 건너뛰기 hover
            bool skipHover = skipButton != null &&
                             RectTransformUtility.RectangleContainsScreenPoint(skipButton, pointer, uiCamera);
            if (skipBackground != null)
                skipBackground.color = skipHover ? skipHoverColor : skipNormalColor;

            // 3) 지목
            if (SeocheonInput.PointerPressedThisFrame)
            {
                if (hoverIndex >= 0 && hoverIndex < regions.Count)
                {
                    InvokePick(regions[hoverIndex].option);
                    return;                       // 콜백이 다음 문장을 열었을 수 있음
                }
                if (skipHover)
                {
                    InvokeSkip();
                    return;
                }
                // 빈 곳 클릭은 ★아무 일도 하지 않습니다(실수로 넘어가는 것을 막음).
            }

            // 4) 탈출구 — 어떤 경우에도 갇히지 않도록
            if (SeocheonInput.CancelPressedThisFrame)
                InvokeSkip();
        }

        // 콜백 재진입 방어: 부르기 전에 지역 변수로 옮깁니다.
        private void InvokePick(WordPickNote.WordOption option)
        {
            Action<WordPickNote.WordOption> cb = onPicked;
            isOpen = false;
            if (cb != null) cb(option);
        }

        private void InvokeSkip()
        {
            Action cb = onSkipped;
            isOpen = false;
            if (cb != null) cb();
        }

        private void InvokeFallback()
        {
            Action cb = onFallback;
            isOpen = false;
            if (cb != null) cb(); else InvokeSkip();
        }

        // ─────────────────────────────────────────────
        //  강조 사각형 (풀링 — 매 프레임 생성하지 않습니다)
        // ─────────────────────────────────────────────

        private void RefreshHighlights()
        {
            ClearHighlights();
            if (hoverIndex < 0 || hoverIndex >= regions.Count || highlightRoot == null) return;

            List<Rect> rects = regions[hoverIndex].rects;
            for (int i = 0; i < rects.Count; i++)
            {
                Image img = GetHighlight(i);
                RectTransform rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(rects[i].width, rects[i].height);
                // 문장 텍스트 로컬 좌표 → 강조 부모 로컬 좌표
                Vector3 world = sentenceText.rectTransform.TransformPoint(new Vector3(rects[i].x, rects[i].y, 0f));
                rt.position = world;
                img.color = highlightColor;      // ★유효/오답 구분 없음
                img.gameObject.SetActive(true);
            }
        }

        private Image GetHighlight(int i)
        {
            while (highlightPool.Count <= i)
            {
                GameObject go = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(highlightRoot, false);
                Image img = go.GetComponent<Image>();
                img.raycastTarget = false;
                go.SetActive(false);
                highlightPool.Add(img);
            }
            return highlightPool[i];
        }

        private void ClearHighlights()
        {
            for (int i = 0; i < highlightPool.Count; i++)
                if (highlightPool[i] != null) highlightPool[i].gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────
        //  조작 잠금
        // ─────────────────────────────────────────────

        private void LockPlayer()
        {
            if (!lockPlayerWhileOpen || playerLocked) return;
            PlayerControlLock.Push();
            playerLocked = true;
        }

        private void UnlockPlayer()
        {
            if (!playerLocked) return;
            playerLocked = false;
            PlayerControlLock.Pop();
        }
    }
}
