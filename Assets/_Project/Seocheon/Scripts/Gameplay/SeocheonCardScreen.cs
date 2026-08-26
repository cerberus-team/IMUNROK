// 서천 전용 단서 카드 화면. 카드 그리드 + 슬롯 2칸 + 결과 칸으로 조각을 맞춰 봅니다.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IMUNROK.Seocheon.Player;

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
        [Header("그리드")]
        [SerializeField] private ScrollRect gridScroll;
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private ClueCardView cardPrefab;

        [Header("카드 겉모습 — ★전 카드 공통")]
        [Tooltip("수집 카드 배경. ★들은 말")]
        [SerializeField] private Sprite collectedFace;
        [Tooltip("결합 결과 카드 배경. ★알아낸 것 — 여기만 다릅니다")]
        [SerializeField] private Sprite derivedFace;
        [SerializeField] private Color cardTextColor = new Color(0.12f, 0.10f, 0.08f);

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
            rootCanvas = GetComponentInParent<Canvas>();
            uiCamera = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                       ? rootCanvas.worldCamera : null;
            if (matchButton != null) matchButton.onClick.AddListener(DoMatch);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (tooltip != null) tooltip.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
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
            gameObject.SetActive(true);
            isOpen = true;
            if (!playerLocked) { PlayerControlLock.Push(); playerLocked = true; }
            ClearSlots();
            Rebuild();
            if (hintLabel != null) hintLabel.text = "카드를 골라 아래 두 칸에 놓고 맞춰 보시오.  (J : 닫기)";
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            HideTooltip();
            if (playerLocked) { playerLocked = false; PlayerControlLock.Pop(); }
            gameObject.SetActive(false);
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
        }

        private ClueCardView AddCard(SeocheonClueRecord record)
        {
            ClueCardView card = Instantiate(cardPrefab, gridContent);
            // ★배경·글자색을 카드가 스스로 고르지 않습니다. 여기서 같은 값을 줍니다.
            // ★수집 카드는 앞면에 글자가 없습니다(그림만). 결합 결과 카드만 문구를 보여 줍니다.
            card.Bind(record, record.isDerived ? derivedFace : collectedFace, cardTextColor, record.isDerived);
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
            resultCard.Bind(rec, derivedFace, cardTextColor, true);
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

            // ★화면좌표 의존부 ①: 포인터 위치.
            //   VR 전환 시 SeocheonInput.PointerPosition 을 컨트롤러 레이 히트로 바꾸면
            //   이 아래 코드는 그대로 둘 수 있습니다.
            Vector2 pointer = SeocheonInput.PointerPosition;

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

            if (SeocheonInput.PointerPressedThisFrame && hovered != null)
                hovered.RaiseClicked();

            if (SeocheonInput.CancelPressedThisFrame) Close();
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
