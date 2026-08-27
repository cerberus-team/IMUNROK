// 서천 전용 단서 카드 화면. 카드 그리드 + 슬롯 2칸 + 결과 칸으로 조각을 맞춰 봅니다.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IMUNROK.Seocheon.Player;
using IMUNROK.Ui;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 서천 전용 수첩 겸 결합 화면.
    ///
    /// ★공통 JournalView(OnGUI 목록)를 쓰지 않습니다.
    ///   - 스크롤이 없어 15줄부터 상자 밖으로 흘러넘치고(실측)
    ///   - 줄 높이 22px 고정이라 37자를 넘으면 뒷부분이 소리 없이 잘리며(실측)
    ///   - 결합은 "카드 두 장을 고르는" 조작이라 목록으로는 안 됩니다.
    ///   ★Journal.AddClue 기록 자체는 그대로 유지됩니다(공통 흐름·판결 연동).
    ///
    /// ★카드를 고르는 방식은 ★클릭 선택입니다. 드래그가 아닙니다.
    ///   VR 로 가면 컨트롤러 레이 + 트리거가 그대로 클릭에 대응하지만,
    ///   드래그는 레이 끝을 계속 따라다녀야 해 손떨림에 약합니다.
    /// </summary>
    public sealed class SeocheonCardScreen : MonoBehaviour
    {
        [Header("판")]
        [Tooltip("판 내용물의 뿌리. ★World Space 로 옮기면서 생겼습니다 — " +
                 "여닫이가 이것만 껐다 켭니다. 비워 두면 예전처럼 이 오브젝트 자체를 끕니다")]
        [SerializeField] private RectTransform content;

        [Header("그리드")]
        [SerializeField] private ScrollRect gridScroll;
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private ClueCardView cardPrefab;

        [Header("카드 그림 — ★사군자 (2026-08-27)")]
        [Tooltip("수집 카드. ★들은 말은 전부 <b>매화</b> 한 장뿐입니다 — 유효/오답이 갈리면 안 됩니다")]
        [SerializeField] private Sprite collectedFace;

        [Tooltip("결합 결과 — <b>모순</b>. 대나무(竹). 곧은 것이 서로 어긋난 모양")]
        [SerializeField] private Sprite derivedFaceContradiction;
        [Tooltip("결합 결과 — <b>연결</b>. 난초(蘭). 줄기가 이어지는 모양")]
        [SerializeField] private Sprite derivedFaceLink;
        [Tooltip("결합 결과 — <b>결론</b>. 국화(菊). 끝에 피는 것 " +
                 "(지시서의 '결말' 과 같은 것입니다 — 코드의 CombineKind.Conclusion)")]
        [SerializeField] private Sprite derivedFaceConclusion;

        [Tooltip("위 셋 중 비어 있는 자리를 대신할 그림. 옛 derivedFace 슬롯입니다")]
        [SerializeField] private Sprite derivedFace;

        [SerializeField] private Color cardTextColor = new Color(0.12f, 0.10f, 0.08f);

        [Tooltip("★결과 카드 문구 뒤에 까는 판. 사군자 그림의 먹선 위에서는 글자가 묻힙니다. " +
                 "그림이 밝은 한지 바탕이라 <b>어두운 판이 아니라 밝은 판</b>을 깔고 먹빛 글씨를 얹습니다")]
        [SerializeField] private Color cardPlateColor = new Color(0.94f, 0.91f, 0.84f, 0.88f);

        [Header("슬롯")]
        [SerializeField] private RectTransform slotA;
        [SerializeField] private RectTransform slotB;
        [SerializeField] private RectTransform resultSlot;
        [Tooltip("슬롯에 놓인 카드의 원문 문장. ★그리드에서는 감추고 여기서만 보여 줍니다")]
        [SerializeField] private TMP_Text slotACaption;
        [SerializeField] private TMP_Text slotBCaption;
        [SerializeField] private Button matchButton;
        [SerializeField] private TMP_Text matchButtonLabel;
        [SerializeField] private Button closeButton;

        [Header("툴팁")]
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private TMP_Text tooltipSentence;
        [SerializeField] private TMP_Text tooltipSource;
        [SerializeField] private Vector2 tooltipOffset = new Vector2(18f, -18f);

        [Header("표시")]
        [SerializeField] private CollectionMeter collectionMeter;
        [SerializeField] private TMP_Text hintLabel;

        [Header("연결")]
        [SerializeField] private ClueCombiner combiner;

        private readonly List<ClueCardView> gridCards = new List<ClueCardView>();
        private readonly List<string> shownEntryIds = new List<string>();

        private ClueCardView slotCardA;
        private ClueCardView slotCardB;
        private ClueCardView resultCard;
        private ClueCardView hovered;

        private Canvas rootCanvas;
        private Camera uiCamera;
        private bool isOpen;
        private bool playerLocked;

        public bool IsOpen { get { return isOpen; } }

        private void Awake()
        {
            ResolveCanvas();
            ApplyTeamSkin();
            BuildSlotFrames();
            BuildScrollHint();
            WireButtons();
            if (tooltip != null) tooltip.gameObject.SetActive(false);
            ShowContent(false);
        }

        /// <summary>
        /// ★내용물만 껐다 켠다. 이 오브젝트 자체를 끄면
        /// <see cref="SeocheonCardScreenOpener"/> 의 Update 가 멈춰 <b>J 로 다시 열 수 없다</b>
        /// (World Space 로 옮기며 실측으로 확인). 받침대는 늘 살아 있어야 한다.
        /// </summary>
        private void ShowContent(bool on)
        {
            if (content != null) { content.gameObject.SetActive(on); return; }
            gameObject.SetActive(on);      // 옛 구성(내용물 뿌리를 안 지정한 경우)
        }

        /// <summary>
        /// 그리는 캔버스와 <b>판정에 쓸 카메라</b>를 잡는다.
        ///
        /// ⚠️ World Space 캔버스의 <c>worldCamera</c> 가 <b>비어 있으면 null 이 그대로 흘러</b>
        ///    <see cref="RectTransformUtility"/> 가 화면 좌표를 Overlay 로 해석한다 —
        ///    그러면 카드가 <b>엉뚱한 자리에서 눌린다</b>. 대화창에서 이미 겪은 함정이라
        ///    여기서는 처음부터 <see cref="Camera.main"/> 으로 되돌린다.
        /// </summary>
        private void ResolveCanvas()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = null;
                return;
            }
            uiCamera = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
            // 판 자신이 쓰는 카메라도 채워 둔다 — UGUI 단추(맞춰보기·닫기)의 광선 판정이 이것을 본다.
            if (rootCanvas.worldCamera == null) rootCanvas.worldCamera = uiCamera;
        }

        // ─────────────────────────────────────────────
        //  겉모습 — ★팀 값을 그대로 가져온다
        // ─────────────────────────────────────────────

        private readonly InventorySkin skin = new InventorySkin();

        /// <summary>
        /// 판·단추·글자를 <b>팀 값</b>으로 맞춘다 (2026-08-27).
        ///
        /// ■ 왜 실행 중에 하는가
        ///   팀 목재·한지 무늬(<see cref="InventorySkin"/>)는 <b>실행 중에 그려지는</b> 절차적
        ///   텍스처다. 프리팹에 미리 박아 둘 수 없어 여는 시점에 입힌다.
        ///
        /// ■ 단추 규칙은 팀 <c>MakeButton</c> 그대로
        ///   목재 바탕 + 한지 글씨. 주칠(<see cref="InventorySkin.Vermilion"/>)은
        ///   <b>주 행동 하나</b>에만 준다 — 여기서는 「맞춰보기」다. 닫기는 목재다.
        ///
        /// ■ ★카드에는 손대지 않는다
        ///   카드는 사군자 그림이 주인공이라 팀 색을 덮으면 그림이 죽는다.
        /// </summary>
        private void ApplyTeamSkin()
        {
            var pal = IMUNROK.Ui.DialogueUI.Palette();

            DressButton(matchButton, InventorySkin.Vermilion);     // 주 행동
            DressButton(closeButton, InventorySkin.Wood);

            // 알파가 음수면 원래 알파를 지킨다 — 덮는 정도는 판마다 뜻이 있다.
            Tint("Backdrop", new Color(pal.back.r, pal.back.g, pal.back.b, -1f));
            Tint("Tooltip", new Color(pal.back.r, pal.back.g, pal.back.b, -1f));
            Tint("SlotA", pal.slotBack);
            Tint("SlotB", pal.slotBack);
            Tint("ResultSlot", pal.slotBack);

            Ink("Title", pal.text);
            Ink("SlotACaptionText", pal.text);
            Ink("SlotBCaptionText", pal.text);
            Ink("Plus", pal.dim);
            Ink("Arrow", pal.dim);
            Ink("MeterLabel", pal.dim);
            Ink("HintLabel", pal.dim);
            Ink("SlotACaption", pal.dim);
            Ink("SlotBCaption", pal.dim);
            Ink("ResultSlotCaption", pal.dim);
            Ink("SlotCaptionMark", pal.dim);
            if (tooltipSentence != null) tooltipSentence.color = pal.text;
            if (tooltipSource != null) tooltipSource.color = pal.dim;
        }

        private void DressButton(Button btn, Color baseColor)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = skin.Wood_;
                img.type = Image.Type.Sliced;
                img.color = baseColor;
            }
            // ★팀 규칙 — 가리키면 금빛으로 물든다. 색조는 이미지에 있으므로 밝기만 바꾼다.
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.18f, 0.95f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            btn.colors = cb;

            var lab = btn.GetComponentInChildren<TMP_Text>(true);
            if (lab != null) lab.color = InventorySkin.Hanji;
        }

        private void Tint(string childName, Color c)
        {
            Transform t = FindDeep(childName);
            if (t == null) return;
            var img = t.GetComponent<Image>();
            if (img == null) return;
            img.color = c.a < 0f ? new Color(c.r, c.g, c.b, img.color.a) : c;
        }

        private void Ink(string childName, Color c)
        {
            Transform t = FindDeep(childName);
            if (t == null) return;
            var lab = t.GetComponent<TMP_Text>();
            if (lab != null) lab.color = c;
        }

        private Transform FindDeep(string childName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) if (all[i].name == childName) return all[i];
            return null;
        }

        // ─────────────────────────────────────────────
        //  빈 칸 테두리 · 굴림 안내
        // ─────────────────────────────────────────────

        private TMP_Text scrollHint;
        private bool wasOverflowing;

        /// <summary>
        /// 빈 슬롯이 <b>구멍처럼</b> 보이지 않게 테두리를 두른다 (2026-08-27).
        ///
        /// 예전에는 흰색 알파 18짜리 상자 하나뿐이라, 어두운 뒤판 위에서 <b>검게 파인 자리</b>로
        /// 보였다. 얇은 테 넉 줄을 둘러 "여기에 놓는 자리"임을 알린다.
        /// ★카드를 놓으면 카드가 칸을 통째로 덮으므로 따로 껐다 켤 일이 없다.
        /// </summary>
        private void BuildSlotFrames()
        {
            var pal = IMUNROK.Ui.DialogueUI.Palette();
            MakeSlotFrame(slotA, pal.border);
            MakeSlotFrame(slotB, pal.border);
            MakeSlotFrame(resultSlot, pal.border);
        }

        private void MakeSlotFrame(RectTransform slot, Color line)
        {
            if (slot == null || slot.Find("빈칸테") != null) return;

            var holder = new GameObject("빈칸테", typeof(RectTransform));
            var hrt = (RectTransform)holder.transform;
            hrt.SetParent(slot, false);
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
            hrt.SetAsFirstSibling();

            const float T = 3f;   // 테 두께
            MakeEdge(hrt, "위",   new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, T), line);
            MakeEdge(hrt, "아래", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, T), line);
            MakeEdge(hrt, "왼",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(T, 0f), line);
            MakeEdge(hrt, "오른", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(T, 0f), line);
        }

        private static void MakeEdge(RectTransform parent, string name, Vector2 aMin, Vector2 aMax,
                                     Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        /// <summary>
        /// 「▼ 휠을 굴려 더 보기」 — 카드가 틀보다 많을 때만 뜬다.
        ///
        /// ★없으면 <b>잘린 줄이 고장으로 보인다</b>. 대화창이 긴 대사에서 같은 이유로 같은 표시를 낸다.
        /// </summary>
        private void BuildScrollHint()
        {
            if (scrollHint != null || gridScroll == null) return;
            var pal = IMUNROK.Ui.DialogueUI.Palette();

            var go = new GameObject("더있음", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(gridScroll.transform.parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-60f, 350f);
            rt.sizeDelta = new Vector2(320f, 30f);

            scrollHint = go.GetComponent<TextMeshProUGUI>();
            scrollHint.font = IMUNROK.Ui.UiSkin.Font;
            scrollHint.fontSize = 22;
            scrollHint.color = pal.dim;
            scrollHint.alignment = TextAlignmentOptions.MidlineRight;
            scrollHint.raycastTarget = false;
            scrollHint.overflowMode = TextOverflowModes.Overflow;
            scrollHint.text = "▼  휠을 굴려 더 보기";
            go.SetActive(false);
        }

        /// <summary>
        /// 넘치는지 재어 안내를 켜고 끈다.
        ///
        /// ⚠️ <b>넘치기 시작하는 순간 맨 위로 돌린다</b> (2026-08-27 실측으로 잡은 것).
        ///    UGUI 의 <c>verticalNormalizedPosition</c> 은 내용이 틀보다 <b>작을 때 0 으로 읽힌다</b>.
        ///    그 상태에서 카드가 늘어 넘치게 되면 그 0 이 <b>맨 아래</b>라는 뜻이 되어,
        ///    아무도 굴리지 않았는데 <b>첫 줄이 잘린 채</b> 뜬다. 화면이 고장난 것처럼 보인다.
        /// </summary>
        private void RefreshScrollHint()
        {
            if (gridScroll == null || gridScroll.viewport == null || gridContent == null) return;
            bool over = gridContent.rect.height > gridScroll.viewport.rect.height + 1f;

            if (over && !wasOverflowing) ScrollToTop();
            wasOverflowing = over;

            if (scrollHint != null && scrollHint.gameObject.activeSelf != over)
                scrollHint.gameObject.SetActive(over);
        }

        /// <summary>카드 줄을 맨 위로. ★여는 순간에는 언제나 첫 줄부터 보여야 한다.</summary>
        private void ScrollToTop()
        {
            if (gridScroll == null) return;
            Canvas.ForceUpdateCanvases();
            gridScroll.verticalNormalizedPosition = 1f;
            gridScroll.velocity = Vector2.zero;
        }

        /// <summary>
        /// 단추에 할 일을 잇는다. ★<b>여러 번 불러도 안전하다</b> — 먼저 떼고 다시 단다.
        ///
        /// ⚠️ <see cref="Awake"/> 에서 한 번만 달면 안 된다는 것을 실측으로 배웠다.
        ///    2026-08-27, 「맞춰보기」가 <b>눌러도 아무 일이 없는</b> 판이 나왔다 —
        ///    EventSystem 을 거친 진짜 클릭까지 무반응이었고, 리스너 목록이 <b>비어 있었다</b>.
        ///    판을 프리팹에서 여러 번 고쳐 끼우는 동안 이어 둔 것이 풀린 것으로 보인다.
        ///    여는 순간마다 다시 이어 두면, 무엇이 풀어 놓든 <b>플레이어가 누르기 전에</b> 복구된다.
        /// </summary>
        private void WireButtons()
        {
            if (matchButton != null)
            {
                matchButton.onClick.RemoveListener(DoMatch);
                matchButton.onClick.AddListener(DoMatch);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }
        }

        private void OnDestroy()
        {
            skin.Dispose();
            if (matchButton != null) matchButton.onClick.RemoveListener(DoMatch);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }

        // ─────────────────────────────────────────────
        //  열기 / 닫기
        // ─────────────────────────────────────────────

        public void Toggle()
        {
            if (isOpen) Close(); else Open();
        }

        public void Open()
        {
            if (isOpen) return;
            ResolveCanvas();               // 모드가 바뀌었을 수 있다 (F8)
            WireButtons();                 // ★여는 순간 다시 확인한다 (아래 주석)
            ShowContent(true);
            isOpen = true;
            if (!playerLocked) { PlayerControlLock.Push(); playerLocked = true; }
            ClearSlots();
            Rebuild();
            ScrollToTop();                 // ★열 때는 언제나 첫 줄부터
            if (hintLabel != null) hintLabel.text = "카드를 골라 아래 두 칸에 놓고 맞춰 보시오.  (J : 닫기)";
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            HideTooltip();
            if (playerLocked) { playerLocked = false; PlayerControlLock.Pop(); }
            ShowContent(false);
        }

        private void OnDisable()
        {
            isOpen = false;
            if (playerLocked) { playerLocked = false; PlayerControlLock.Pop(); }
        }

        // ─────────────────────────────────────────────
        //  그리드
        // ─────────────────────────────────────────────

        /// <summary>저장소에 새로 생긴 카드만 붙입니다. 전체를 다시 만들지 않습니다.</summary>
        public void Rebuild()
        {
            if (cardPrefab == null || gridContent == null) return;

            IReadOnlyList<SeocheonClueRecord> records = SeocheonClueStore.Records;
            for (int i = 0; i < records.Count; i++)
            {
                SeocheonClueRecord r = records[i];
                if (shownEntryIds.Contains(r.entryId)) continue;
                AddCard(r);
            }

            // ★수치는 저장소에서 직접 셉니다. 이벤트 누적은 세이브 복원·주입 경로에서 어긋납니다.
            if (collectionMeter != null)
            {
                int collected = 0, combined = 0;
                for (int i = 0; i < records.Count; i++)
                {
                    if (records[i].isDerived) combined++; else collected++;
                }
                collectionMeter.SetCounts(collected, combined);
            }

            // ★칸이 늘면 넘칠 수 있다 — 줄 때마다 다시 잰다.
            Canvas.ForceUpdateCanvases();
            if (gridContent != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(gridContent);
            RefreshScrollHint();
        }

        /// <summary>
        /// 이 기록에 맞는 카드 그림. ★수집 카드는 <b>언제나 매화</b> 한 장입니다 —
        /// 유효한 조각인지 오답인지가 그림으로 드러나면 지목이 추리가 아니라 정답 찾기가 됩니다.
        ///
        /// 결합 결과만 사군자로 갈립니다. 그건 이미 판정이 끝난 것이라 감출 것이 없습니다.
        /// ⚠️ 기록에는 <see cref="CombineKind"/> 가 아니라 <b>한글 표식</b>(combineKind)만 남아 있어
        ///    여기서 되짚습니다. 표식 문구는 <see cref="CombineRule.KindLabel"/> 이 정합니다.
        /// </summary>
        private Sprite FaceFor(SeocheonClueRecord record)
        {
            if (record == null || !record.isDerived) return collectedFace;

            Sprite picked;
            if (record.combineKind == "연결") picked = derivedFaceLink;
            else if (record.combineKind == "결론") picked = derivedFaceConclusion;
            else picked = derivedFaceContradiction;

            return picked != null ? picked : derivedFace;
        }

        private ClueCardView AddCard(SeocheonClueRecord record)
        {
            ClueCardView card = Instantiate(cardPrefab, gridContent);
            // ★배경·글자색을 카드가 스스로 고르지 않습니다. 여기서 같은 값을 줍니다.
            // ★수집 카드는 앞면에 글자가 없습니다(그림만). 결합 결과 카드만 문구를 보여 줍니다.
            card.Bind(record, FaceFor(record), cardTextColor, record.isDerived, cardPlateColor);
            card.Clicked += OnCardClicked;
            gridCards.Add(card);
            shownEntryIds.Add(record.entryId);
            return card;
        }

        // ─────────────────────────────────────────────
        //  슬롯
        // ─────────────────────────────────────────────

        private void OnCardClicked(ClueCardView card)
        {
            if (card == null) return;

            // 이미 슬롯에 있으면 빼서 그리드로 되돌립니다.
            if (card == slotCardA) { ReturnToGrid(ref slotCardA); SetSlotCaption(slotACaption, null); RefreshMatchButton(); return; }
            if (card == slotCardB) { ReturnToGrid(ref slotCardB); SetSlotCaption(slotBCaption, null); RefreshMatchButton(); return; }

            if (slotCardA == null) { MoveToSlot(card, slotA); slotCardA = card; SetSlotCaption(slotACaption, card); }
            else if (slotCardB == null) { MoveToSlot(card, slotB); slotCardB = card; SetSlotCaption(slotBCaption, card); }
            else return;                       // 두 칸이 다 찼으면 무시

            card.SetSelected(true);
            RefreshMatchButton();
        }

        private void MoveToSlot(ClueCardView card, RectTransform slot)
        {
            if (slot == null) return;
            card.transform.SetParent(slot, false);
            RectTransform rt = card.Rect;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void ReturnToGrid(ref ClueCardView slotCard)
        {
            if (slotCard == null) return;
            slotCard.SetSelected(false);
            slotCard.transform.SetParent(gridContent, false);
            RestoreGridOrder(slotCard);
            slotCard = null;
        }

        /// <summary>그리드의 원래 자리(수집 순서)로 되돌립니다.</summary>
        private void RestoreGridOrder(ClueCardView card)
        {
            int target = shownEntryIds.IndexOf(card.EntryId);
            if (target < 0) return;
            int index = 0;
            for (int i = 0; i < gridCards.Count; i++)
            {
                ClueCardView c = gridCards[i];
                if (c == null || c == card) continue;
                if (c.transform.parent != gridContent) continue;
                if (shownEntryIds.IndexOf(c.EntryId) < target) index++;
            }
            card.transform.SetSiblingIndex(index);
        }

        private void ClearSlots()
        {
            ReturnToGrid(ref slotCardA);
            ReturnToGrid(ref slotCardB);
            SetSlotCaption(slotACaption, null);
            SetSlotCaption(slotBCaption, null);
            RefreshMatchButton();
        }

        /// <summary>
        /// ★슬롯에 놓인 카드가 무엇인지 알려 줍니다.
        ///   그리드에서는 그림만 보이므로, 놓고 나서야 무슨 말이었는지 확인됩니다.
        /// </summary>
        private void SetSlotCaption(TMP_Text label, ClueCardView card)
        {
            if (label == null) return;
            if (card == null) { label.text = string.Empty; return; }
            SeocheonClueRecord rec = SeocheonClueStore.Get(card.EntryId);
            label.text = rec == null ? string.Empty : rec.sentence;
        }

        private void RefreshMatchButton()
        {
            bool ready = slotCardA != null && slotCardB != null;
            if (matchButton != null) matchButton.interactable = ready;
            if (matchButtonLabel != null) matchButtonLabel.color = ready
                ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.4f);
        }

        // ─────────────────────────────────────────────
        //  맞춰보기
        // ─────────────────────────────────────────────

        private void DoMatch()
        {
            if (slotCardA == null || slotCardB == null || combiner == null) return;

            CombineResult res = combiner.Try(slotCardA.EntryId, slotCardB.EntryId);

            if (res.outcome == CombineOutcome.Unrelated)
            {
                // ★무관은 실패가 아닙니다. 소리도 문구도 없이 슬롯만 비웁니다.
                ClearSlots();
                return;
            }

            ClearSlots();
            Rebuild();                                   // 새 카드가 그리드에 붙습니다

            // 방금 만든 카드를 결과 칸에 올려 보여 줍니다(그리드에도 남아 재료가 됩니다).
            ShowResult(res.newEntryId);
            if (hintLabel != null) hintLabel.text = res.kind == CombineKind.Conclusion
                ? "…맞물렸소. 이만하면 대강이 잡히는구려."
                : "…맞물렸소.";
        }

        private void ShowResult(string entryId)
        {
            if (resultSlot == null) return;
            if (resultCard != null) { Destroy(resultCard.gameObject); resultCard = null; }

            SeocheonClueRecord rec = SeocheonClueStore.Get(entryId);
            if (rec == null) return;

            resultCard = Instantiate(cardPrefab, resultSlot);
            resultCard.Bind(rec, FaceFor(rec), cardTextColor, true, cardPlateColor);
            RectTransform rt = resultCard.Rect;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // ─────────────────────────────────────────────
        //  입력 — ★전부 SeocheonInput 경유
        // ─────────────────────────────────────────────

        private void Update()
        {
            if (!isOpen) return;
            if (uiCamera == null && rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ResolveCanvas();

            // ★포인터 — PC 는 마우스, VR 은 컨트롤러 광선. 아래 판정 코드는 <b>한 줄도 안 달라집니다</b>
            //   (광선을 판 면과 만난 점의 <b>화면 좌표</b>로 바꿔 넘기기 때문입니다).
            bool pressed;
            Vector2 pointer;
            if (!TryGetPointer(out pointer, out pressed)) { HideTooltip(); hovered = null; return; }

            ClueCardView found = null;
            for (int i = 0; i < gridCards.Count; i++)
            {
                ClueCardView c = gridCards[i];
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                // 그리드 카드는 스크롤 뷰포트 안에 있을 때만 집힙니다(밀려난 카드 오클릭 방지).
                if (c.transform.parent == gridContent && gridScroll != null && gridScroll.viewport != null &&
                    !RectTransformUtility.RectangleContainsScreenPoint(gridScroll.viewport, pointer, uiCamera)) continue;
                if (!c.ContainsPointer(pointer, uiCamera)) continue;
                found = c;
                break;
            }

            if (found != hovered)
            {
                hovered = found;
                if (hovered != null) ShowTooltip(hovered, pointer); else HideTooltip();
            }
            else if (hovered != null && tooltip != null)
            {
                PlaceTooltip(pointer);
            }

            if (pressed && hovered != null)
                hovered.RaiseClicked();

            if (SeocheonInput.CancelPressedThisFrame) Close();
        }

        /// <summary>
        /// 이번 프레임의 포인터를 <b>화면 좌표</b>로 준다.
        ///
        /// ■ PC — 지금까지 그대로. 마우스 커서.
        /// ■ VR — 컨트롤러 광선을 판 면과 만나게 하고, 그 점을 카메라로 <b>화면 좌표로 되돌린다</b>.
        ///   ⚠️ 이렇게 하는 까닭: 아래 판정이 전부
        ///      <c>RectTransformUtility.RectangleContainsScreenPoint(rect, 화면점, 카메라)</c> 로 돌아간다.
        ///      광선 판정으로 갈아엎으면 카드·뷰포트·툴팁 자리 잡기를 <b>전부 다시 써야</b> 한다.
        ///      한 점만 바꿔 끼우면 나머지 200줄이 그대로 산다.
        ///   ⚠️ 판 뒤쪽(카메라 뒤)으로 넘어간 점은 화면 좌표가 뒤집혀 <b>엉뚱한 카드가 잡힌다</b>.
        ///      z 가 양수일 때만 쓴다.
        /// </summary>
        private bool TryGetPointer(out Vector2 pointer, out bool pressed)
        {
            if (!UiModes.IsVr || uiCamera == null)
            {
                pointer = SeocheonInput.PointerPosition;
                pressed = SeocheonInput.PointerPressedThisFrame;
                return true;
            }

            pointer = Vector2.zero;
            pressed = false;

            var ptr = UiPointers.Get(uiCamera.transform);
            if (ptr == null || !ptr.Available) return false;

            Transform panel = content != null ? content : transform;
            var plane = new Plane(-panel.forward, panel.position);
            float d;
            if (!plane.Raycast(ptr.PointRay, out d)) return false;

            Vector3 screen = uiCamera.WorldToScreenPoint(ptr.PointRay.GetPoint(d));
            if (screen.z <= 0f) return false;

            pointer = new Vector2(screen.x, screen.y);
            pressed = ptr.PressDown;
            return true;
        }

        private void ShowTooltip(ClueCardView card, Vector2 pointer)
        {
            if (tooltip == null) return;
            SeocheonClueRecord rec = SeocheonClueStore.Get(card.EntryId);
            if (rec == null) { HideTooltip(); return; }

            if (tooltipSentence != null) tooltipSentence.text = rec.sentence;
            if (tooltipSource != null)
            {
                if (rec.isDerived)
                {
                    string from = string.Empty;
                    for (int i = 0; i < rec.sourceEntryIds.Count; i++)
                    {
                        SeocheonClueRecord s = SeocheonClueStore.Get(rec.sourceEntryIds[i]);
                        if (s == null) continue;
                        if (from.Length > 0) from += "  +  ";
                        from += s.faceText;
                    }
                    tooltipSource.text = "맞물린 것 : " + from;
                }
                else
                {
                    tooltipSource.text = string.IsNullOrEmpty(rec.sourceNpc)
                        ? "본 것" : rec.sourceNpc + " 에게 들음";
                }
            }
            tooltip.gameObject.SetActive(true);
            PlaceTooltip(pointer);
        }

        // ★화면좌표 의존부 ②: 툴팁 배치. VR 에서는 카드 옆 월드 공간으로 바꿔야 합니다.
        private void PlaceTooltip(Vector2 pointer)
        {
            if (tooltip == null || rootCanvas == null) return;
            RectTransform canvasRect = (RectTransform)rootCanvas.transform;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointer, uiCamera, out local)) return;

            Vector2 size = tooltip.sizeDelta;
            Vector2 half = canvasRect.rect.size * 0.5f;
            float x = local.x + tooltipOffset.x;
            float y = local.y + tooltipOffset.y;
            // 화면 밖으로 나가면 반대쪽으로 접습니다.
            if (x + size.x > half.x) x = local.x - tooltipOffset.x - size.x;
            if (y - size.y < -half.y) y = local.y - tooltipOffset.y + size.y;
            tooltip.anchoredPosition = new Vector2(x, y);
        }

        private void HideTooltip()
        {
            if (tooltip != null) tooltip.gameObject.SetActive(false);
        }
    }
}
