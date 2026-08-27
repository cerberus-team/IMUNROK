// NPC 를 마주보는 대화창. 현재 발화만 아래쪽에 띄우고, 지난 말은 버튼으로 펼쳐 지목합니다.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IMUNROK.Ui;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 서천 대화창.
    ///
    /// ★채팅 로그가 아니라 "인물을 마주보는 화면" 입니다.
    ///   - 기본 상태: ★NPC 의 현재 발화만 화면 아래쪽에 뜹니다. 배경(마을)이 보입니다.
    ///   - ★플레이어 발화는 화면에 남기지 않습니다(전송하면 사라집니다).
    ///   - ★[지난 말] 을 누르면 지난 발화가 펼쳐지고, 거기서도 어절을 지목할 수 있습니다.
    ///
    /// ★대화 모드와 지목 모드가 나뉘지 않습니다. 응답을 기다리는 동안에도 지목은 됩니다.
    /// ★유효/오답 어절은 색·크기가 완전히 같습니다. 지목 완료만 얇은 밑줄로 표시합니다.
    /// </summary>
    public sealed class SeocheonDialogueUI : MonoBehaviour
    {
        [Header("발화")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private DialogueLineView npcLinePrefab;
        [SerializeField] private TMP_Text speakerLabel;

        [Header("패널 크기")]
        [Tooltip("기본(현재 발화만) 높이")]
        [SerializeField] private float compactHeight = 300f;
        [Tooltip("[지난 말] 을 펼쳤을 때 높이")]
        [SerializeField] private float expandedHeight = 620f;
        [SerializeField] private RectTransform panel;

        [Header("선택지 — ★VR 에서 타이핑이 불가능해 이것이 기본 입력입니다")]
        [Tooltip("질문 버튼들. 3~4개. ★tone 에 따라 모양을 달리하지 않습니다")]
        [SerializeField] private Button[] askButtons;
        [SerializeField] private TMP_Text[] askLabels;

        [Header("자유 입력 — ★데스크톱 개발 편의용")]
        [Tooltip("끄면 입력창·묻기 버튼이 사라지고 선택지만 남습니다(VR 기본값)")]
        [SerializeField] private bool enableFreeTyping = true;
        [SerializeField] private GameObject freeTypingRoot;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button historyButton;
        [SerializeField] private TMP_Text historyLabel;
        [SerializeField] private Button endButton;
        [SerializeField] private TMP_Text statusLabel;

        [Header("지목 표시")]
        [Tooltip("hover 강조. ★유효 여부와 무관하게 이 색 하나만 씁니다")]
        [SerializeField] private Color hoverColor = new Color(1f, 0.85f, 0.4f, 0.28f);
        [Tooltip("지목 완료 밑줄. ★유효/오답 동일")]
        [SerializeField] private Color consumedColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private float paddingX = 3f;
        [SerializeField] private float paddingY = 2f;

        [Header("색")]
        [SerializeField] private Color npcTextColor = new Color(1f, 0.97f, 0.90f);
        [SerializeField] private Color pastTextColor = new Color(0.78f, 0.76f, 0.72f);

        // 한 턴에 나온 NPC 줄 묶음
        private sealed class Turn
        {
            public readonly List<DialogueLineView> views = new List<DialogueLineView>();
        }

        private readonly List<Turn> turns = new List<Turn>();
        private readonly List<DialogueLineView> npcLines = new List<DialogueLineView>();
        /// <summary>줄과 짝을 이루는 문장. ★모드가 바뀌면 어절 상자를 다시 만들어야 해서 들고 있는다.</summary>
        private readonly List<WordPickNote.Sentence> lineSentences = new List<WordPickNote.Sentence>();
        private readonly List<string> askLabelCache = new List<string>();
        private readonly List<string> askToneCache = new List<string>();
        private readonly List<DialogueLineView> pendingViews = new List<DialogueLineView>();
        private readonly List<WordPickNote.Sentence> pendingHits = new List<WordPickNote.Sentence>();

        private Canvas rootCanvas;
        private Camera uiCamera;
        private RectTransform logViewport;
        private DialogueLineView hoverLine;
        private int hoverIndex = -1;
        private bool isOpen;
        private bool busy;
        private bool historyShown;

        /// <summary>
        /// 어절을 지목했을 때. (지목한 어절, 그 어절이 들어 있던 ★문장 전체)
        /// ★유효/오답을 구분해 부르지 않습니다. 판정은 나중에 결합에서만 합니다.
        /// </summary>
        public event Action<WordPickNote.WordOption, string> WordPicked;
        public event Action<string> PlayerSubmitted;
        /// <summary>선택지를 골랐을 때. (문구, tone) — ★tone 은 로그·분석용이며 화면에 쓰지 않습니다.</summary>
        public event Action<string, string> AskChosen;
        public event Action EndRequested;

        public bool IsOpen { get { return isOpen; } }
        public bool HistoryShown { get { return historyShown; } }

        /// <summary>
        /// 캔버스와 히트테스트용 카메라를 찾는다.
        /// ★Awake 에서 한 번만 잡으면 안 된다 — 월드 판(<see cref="SeocheonDialogueVrHost"/>)은
        ///   자신의 Awake 에서 Canvas 를 붙이는데, 같은 오브젝트에 붙은 두 컴포넌트의 Awake
        ///   순서는 보장되지 않는다. 우리가 먼저 돌면 rootCanvas 가 null 로 굳어
        ///   워드픽 지목이 통째로 죽는다. 그래서 열 때마다 다시 잡는다.
        /// </summary>
        private void ResolveCanvas()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            // ★VrPanel 이 만든 월드 캔버스는 worldCamera 를 비워 둔다. 그대로 null 을 물면
            //   히트테스트가 Overlay 인 줄 알고 풀어 ★어절 지목이 통째로 죽는다(실측).
            //   그래서 비어 있으면 Camera.main 으로 내려간다.
            uiCamera = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                       ? (rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main)
                       : null;
        }

        private void Awake()
        {
            ResolveCanvas();
            if (scrollRect != null) logViewport = scrollRect.viewport;

            if (sendButton != null) sendButton.onClick.AddListener(SubmitFromField);
            BindAskButtons();
            if (endButton != null) endButton.onClick.AddListener(RaiseEnd);
            if (historyButton != null) historyButton.onClick.AddListener(ToggleHistory);
            if (inputField != null) inputField.onSubmit.AddListener(OnFieldSubmit);

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // ★모드 구독은 OnDisable 이 떼지만, 꺼진 채로 파괴되면 OnDisable 이 오지 않는다.
            //   정적 이벤트에 죽은 객체가 남아 다음 전환에서 MissingReferenceException 이 난다(실측).
            UiModes.Changed -= OnUiModeChanged;
            if (sendButton != null) sendButton.onClick.RemoveListener(SubmitFromField);
            if (endButton != null) endButton.onClick.RemoveListener(RaiseEnd);
            if (historyButton != null) historyButton.onClick.RemoveListener(ToggleHistory);
            if (inputField != null) inputField.onSubmit.RemoveListener(OnFieldSubmit);
        }

        // ─────────────────────────────────────────────
        //  열기 / 닫기
        // ─────────────────────────────────────────────

        public void Open(string npcName)
        {
            gameObject.SetActive(true);
            ResolveCanvas();          // ★월드 판이 Canvas 를 붙인 뒤일 수 있다 (ResolveCanvas 주석 참고)
            ClearAll();
            if (speakerLabel != null) speakerLabel.text = npcName ?? string.Empty;
            if (freeTypingRoot != null) freeTypingRoot.SetActive(enableFreeTyping);
            historyShown = false;
            ApplyPanelSize();
            SetBusy(false);
            isOpen = true;
            FocusInput();
        }

        public void Close()
        {
            isOpen = false;
            ClearHover();
            gameObject.SetActive(false);
        }

        private void ClearAll()
        {
            for (int i = 0; i < npcLines.Count; i++)
                if (npcLines[i] != null) Destroy(npcLines[i].gameObject);
            npcLines.Clear();
            lineSentences.Clear();
            turns.Clear();
            hoverLine = null;
            hoverIndex = -1;
        }

        // ─────────────────────────────────────────────
        //  발화
        // ─────────────────────────────────────────────

        /// <summary>NPC 의 새 발화. 지난 턴은 접히고 이 턴만 보입니다.</summary>
        public void ShowNpcTurn(IList<WordPickNote.Sentence> sentences, string npcName)
        {
            if (sentences == null || npcLinePrefab == null || content == null) return;

            pendingViews.Clear();
            pendingHits.Clear();

            Turn turn = new Turn();
            for (int i = 0; i < sentences.Count; i++)
            {
                WordPickNote.Sentence s = sentences[i];
                if (s == null || string.IsNullOrEmpty(s.text)) continue;

                DialogueLineView view = Instantiate(npcLinePrefab, content);
                view.BindNpc(s.text, npcTextColor);
                npcLines.Add(view);
                turn.views.Add(view);

                WordPickNote.Sentence forHit = new WordPickNote.Sentence();
                forHit.text = s.text;
                forHit.options = s.options;
                lineSentences.Add(forHit);

                pendingViews.Add(view);
                pendingHits.Add(forHit);
            }
            if (turn.views.Count == 0) return;
            turns.Add(turn);

            // ★레이아웃이 확정된 뒤에 판정 영역을 만듭니다(줄 너비가 정해져야 줄바꿈이 맞습니다).
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < pendingViews.Count; i++)
                pendingViews[i].BuildRegions(pendingHits[i], paddingX, paddingY);

            pendingViews.Clear();
            pendingHits.Clear();

            ApplyVisibility();
        }

        /// <summary>현재 턴만 보일지, 지난 말까지 보일지 반영합니다.</summary>
        private void ApplyVisibility()
        {
            for (int t = 0; t < turns.Count; t++)
            {
                bool isLatest = (t == turns.Count - 1);
                bool visible = historyShown || isLatest;
                List<DialogueLineView> views = turns[t].views;
                for (int i = 0; i < views.Count; i++)
                {
                    if (views[i] == null) continue;
                    if (views[i].gameObject.activeSelf != visible) views[i].gameObject.SetActive(visible);
                    views[i].SetTextColor(isLatest ? npcTextColor : pastTextColor);
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
            ClearHover();
        }

        private void ToggleHistory()
        {
            historyShown = !historyShown;
            if (historyLabel != null) historyLabel.text = historyShown ? "지난 말 접기" : "지난 말";
            ApplyPanelSize();
            ApplyVisibility();
            // ★펼치면 지난 발화가 화면에 올라오므로, 그 어절의 판정 좌표도 그때 다시 맞습니다.
        }

        // ─────────────────────────────────────────────
        //  재배치 — 팀 확정안 (README 5절)
        // ─────────────────────────────────────────────

        private bool builtForVr;
        private Image ruleLine;
        private TMP_Text footLabel;

        /// <summary>
        /// 판을 팀 확정 치수로 다시 앉힌다.
        ///
        /// ★<c>compactHeight</c>·<c>expandedHeight</c> 는 더 쓰지 않는다. 판 높이는
        ///   <see cref="SeocheonBarMetrics"/> 가 정한 <b>고정값</b>(PC 461 · VR 535)이고,
        ///   [지난 말] 은 판을 키우는 대신 <b>대사 상자 안에서 굴려 읽는다</b> —
        ///   팀 문서가 "잘라내지 말고 굴려 읽는다"고 못박은 방식이다.
        /// </summary>
        private void ApplyPanelSize()
        {
            if (panel == null) return;
            var m = SeocheonBarMetrics.Of(UiModes.IsVr);
            builtForVr = UiModes.IsVr;

            panel.sizeDelta = new Vector2(m.panelW, m.panelH);
            float innerW = m.panelW - m.padX * 2f;
            float y = m.padTop;

            // ── 1행 : 이름패(좌) · 안내(가운데) · 지난 말 · 닫기 ×(우)
            float endW = 64f, histW = m.showFootInNameRow ? 190f : 210f, gap = 10f;
            PlaceTL(endButton, m.panelW - m.padX - endW, y, endW, m.nameH);
            PlaceTL(historyButton, m.panelW - m.padX - endW - gap - histW, y, histW, m.nameH);
            PlaceTL(speakerLabel, m.padX, y, 520f, m.nameH);
            Dress(speakerLabel, m.nameSize, TextAlignmentOptions.Left);

            float rightEdge = m.panelW - m.padX - endW - gap - histW - gap;
            if (m.showFootInNameRow)
            {
                EnsureFoot();
                footLabel.gameObject.SetActive(true);
                PlaceTL(footLabel, m.padX + 520f + gap, y,
                        Mathf.Max(120f, rightEdge - (m.padX + 520f + gap) - 260f), m.nameH);
                Dress(footLabel, m.footSize, TextAlignmentOptions.Left);
                footLabel.text = "Enter — 묻기    Esc — 끝내기";
                PlaceTL(statusLabel, rightEdge - 250f, y, 250f, m.nameH);
            }
            else
            {
                if (footLabel != null) footLabel.gameObject.SetActive(false);
                PlaceTL(statusLabel, rightEdge - 320f, y, 320f, m.nameH);
            }
            Dress(statusLabel, m.footSize, TextAlignmentOptions.Right);
            DressButton(historyButton, m.inputSize);
            // ★닫기는 확정안대로 글자 하나 '×' 다. "대화 끝내기 →" 는 64폭에서 세 줄로 접힌다(실측).
            //   '×'(U+00D7) 는 두 글꼴 모두에 있다 — README 가 못 쓴다고 한 '✕'(U+2715) 와 다른 글자다.
            if (endButton != null)
            {
                var el = endButton.GetComponentInChildren<TMP_Text>(true);
                if (el != null) { el.text = "×"; Dress(el, m.nameSize, TextAlignmentOptions.Center); }
            }
            y += m.nameH + m.nameToRule;

            // ── 2행 : 구분선
            EnsureRule();
            PlaceTL(ruleLine.rectTransform, m.padX, y, innerW, m.ruleH);
            y += m.ruleH + m.ruleToLine;

            // ── 3행 : 대사 상자 (굴려 읽기 · ★어절 지목)
            if (scrollRect != null) PlaceTL((RectTransform)scrollRect.transform, m.padX, y, innerW, m.lineBoxH);
            y += m.lineBoxH + m.lineToAsk;

            // ── 4행 : 선택지 (PC 1×4 · VR 2×2)
            //   ★단추들은 프리팹에서 <b>담는 상자</b>(AskButtons) 밑에 있다. 상자를 먼저 앉히고
            //     단추는 그 상자 안 좌표로 놓는다 — 안 그러면 옛 상자 자리에 그대로 갇힌다.
            if (askButtons != null && askButtons.Length > 0 && askButtons[0] != null)
            {
                RectTransform box = askButtons[0].transform.parent as RectTransform;
                if (box != null && box != panel) PlaceTL(box, m.padX, y, innerW, m.AskBlockH);
                float ox = (box != null && box != panel) ? 0f : m.padX;
                float oy = (box != null && box != panel) ? 0f : y;
                float cellW = (innerW - m.askGap * (m.askCols - 1)) / m.askCols;
                for (int i = 0; i < askButtons.Length; i++)
                {
                    if (askButtons[i] == null) continue;
                    int col = i % m.askCols, row = i / m.askCols;
                    PlaceTL(askButtons[i], ox + col * (cellW + m.askGap),
                            oy + row * (m.askH + m.askGap), cellW, m.askH);
                    if (askLabels != null && i < askLabels.Length)
                        Dress(askLabels[i], m.inputSize, TextAlignmentOptions.Center);
                }
            }
            y += m.AskBlockH;

            // ── 5행 : 입력줄 (VR 은 글쇠를 못 쳐서 통째로 없다)
            bool showInput = m.showInputRow && enableFreeTyping;
            if (freeTypingRoot != null) freeTypingRoot.SetActive(showInput);
            if (showInput)
            {
                y += m.askToInput;
                float sendW = 150f;
                RectTransform fbox = freeTypingRoot != null ? freeTypingRoot.transform as RectTransform : null;
                if (fbox != null && fbox != panel) PlaceTL(fbox, m.padX, y, innerW, m.inputH);
                float fx = (fbox != null && fbox != panel) ? 0f : m.padX;
                float fy = (fbox != null && fbox != panel) ? 0f : y;
                PlaceTL(inputField, fx, fy, innerW - sendW - gap, m.inputH);
                PlaceTL(sendButton, fx + innerW - sendW, fy, sendW, m.inputH);
                DressButton(sendButton, m.inputSize);
                if (inputField != null)
                {
                    Dress(inputField.textComponent, m.inputSize, TextAlignmentOptions.Left);
                    Dress(inputField.placeholder as TMP_Text, m.inputSize, TextAlignmentOptions.Left);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        /// <summary>판 <b>왼쪽 위</b>를 원점으로 자리를 잡는다 — 치수표가 그 순서로 쓰였다.</summary>
        private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }
        private static void PlaceTL(Component c, float x, float y, float w, float h)
        { if (c != null) PlaceTL((RectTransform)c.transform, x, y, w, h); }

        private static void Dress(TMP_Text t, int size, TextAlignmentOptions align)
        {
            if (t == null) return;
            t.fontSize = size;
            t.alignment = align;
            t.overflowMode = TextOverflowModes.Overflow;   // ★Truncate 는 줄을 통째로 버린다
        }

        private void DressButton(Button b, int size)
        {
            if (b == null) return;
            var t = b.GetComponentInChildren<TMP_Text>(true);
            Dress(t, size, TextAlignmentOptions.Center);
        }

        private void EnsureRule()
        {
            if (ruleLine != null) return;
            var go = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(panel, false);
            ruleLine = go.GetComponent<Image>();
            ruleLine.color = new Color(0.44f, 0.35f, 0.25f, 0.87f);   // 팀 pal.border
            ruleLine.raycastTarget = false;
        }

        private void EnsureFoot()
        {
            if (footLabel != null) return;
            var go = new GameObject("FootLabel", typeof(RectTransform));
            go.transform.SetParent(panel, false);
            footLabel = go.AddComponent<TextMeshProUGUI>();
            footLabel.font = speakerLabel != null ? speakerLabel.font : footLabel.font;
            footLabel.color = new Color(0.820f, 0.790f, 0.730f, 0.78f);   // 팀 pal.dim
            footLabel.raycastTarget = false;
        }

        private void OnEnable() { UiModes.Changed += OnUiModeChanged; }
        private void OnDisable() { UiModes.Changed -= OnUiModeChanged; }

        /// <summary>F8 로 갈아탈 때. ★PC와 VR은 치수가 아예 달라 다시 앉혀야 한다.</summary>
        private void OnUiModeChanged(UiMode m)
        {
            if (this == null || panel == null) return;      // ★파괴된 뒤 불려도 조용히 빠진다
            if (builtForVr == UiModes.IsVr) return;
            ApplyPanelSize();
            ApplyVisibility();
            RebuildWordRegions();
        }

        /// <summary>
        /// 어절 판정 상자를 다시 만든다.
        ///
        /// ★PC와 VR은 <b>판 너비도 글자 크기도 다르다</b>(2720/42 ↔ 1380/46). 글이 다시 접히므로
        ///   전에 만든 상자는 글자와 어긋난다 — 그대로 두면 F8 뒤에 <b>어절이 하나도 안 짚힌다</b>(실측).
        /// </summary>
        private void RebuildWordRegions()
        {
            if (content == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < npcLines.Count && i < lineSentences.Count; i++)
                if (npcLines[i] != null && lineSentences[i] != null)
                    npcLines[i].BuildRegions(lineSentences[i], paddingX, paddingY);
            ClearHover();
        }

        // ─────────────────────────────────────────────
        //  상태
        // ─────────────────────────────────────────────

        /// <summary>응답 대기. ★입력만 잠그고 어절 지목은 계속 됩니다.</summary>
        public void SetBusy(bool value)
        {
            busy = value;
            if (inputField != null) inputField.interactable = !value;
            if (sendButton != null) sendButton.interactable = !value;
            SetAsksInteractable(!value);
            if (statusLabel != null && value) statusLabel.text = "…생각하는 중";
            if (!value) FocusInput();
        }

        public void SetStatus(string text)
        {
            if (statusLabel != null) statusLabel.text = text ?? string.Empty;
        }

        private void FocusInput()
        {
            if (inputField == null || busy || !enableFreeTyping) return;
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }

        private void OnFieldSubmit(string value) { SubmitFromField(); }

        private void SubmitFromField()
        {
            if (busy || inputField == null || !enableFreeTyping) return;
            string text = inputField.text;
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return;

            // ★플레이어 발화는 화면에 남기지 않습니다 — 입력칸을 비우는 것으로 끝.
            inputField.text = string.Empty;
            if (PlayerSubmitted != null) PlayerSubmitted(text.Trim());
        }

        // ─────────────────────────────────────────────
        //  선택지
        // ─────────────────────────────────────────────

        private void BindAskButtons()
        {
            if (askButtons == null) return;
            for (int i = 0; i < askButtons.Length; i++)
            {
                if (askButtons[i] == null) continue;
                int index = i;                       // 클로저 캡처
                askButtons[i].onClick.AddListener(delegate { OnAskClicked(index); });
            }
        }

        /// <summary>
        /// 선택지를 채웁니다. ★tone 에 따라 색·크기·아이콘을 달리하지 않습니다 —
        /// 어느 것이 위험한지 미리 알려 주면 고를 이유가 사라집니다.
        /// </summary>
        public void SetAsks(IList<string> labels, IList<string> tones)
        {
            askLabelCache.Clear();
            askToneCache.Clear();
            if (labels != null)
                for (int i = 0; i < labels.Count && i < (askButtons == null ? 0 : askButtons.Length); i++)
                {
                    if (string.IsNullOrEmpty(labels[i])) continue;
                    askLabelCache.Add(labels[i]);
                    askToneCache.Add(tones != null && i < tones.Count ? tones[i] : string.Empty);
                }

            if (askButtons == null) return;
            for (int i = 0; i < askButtons.Length; i++)
            {
                bool used = i < askLabelCache.Count;
                if (askButtons[i] != null) askButtons[i].gameObject.SetActive(used);
                if (used && askLabels != null && i < askLabels.Length && askLabels[i] != null)
                    askLabels[i].text = askLabelCache[i];
            }
            SetAsksInteractable(!busy);
        }

        private void SetAsksInteractable(bool value)
        {
            if (askButtons == null) return;
            for (int i = 0; i < askButtons.Length; i++)
                if (askButtons[i] != null) askButtons[i].interactable = value;
        }

        private void OnAskClicked(int index)
        {
            if (busy || index < 0 || index >= askLabelCache.Count) return;
            string label = askLabelCache[index];
            string tone = index < askToneCache.Count ? askToneCache[index] : string.Empty;
            // ★고른 질문은 화면에 남기지 않습니다(기존 방침).
            if (AskChosen != null) AskChosen(label, tone);
        }

        private void RaiseEnd()
        {
            if (EndRequested != null) EndRequested();
        }

        // ─────────────────────────────────────────────
        //  어절 지목 — 화면에 떠 있는 발화 전부가 대상
        // ─────────────────────────────────────────────

        private void Update()
        {
            if (!isOpen) return;

            Vector2 pointer = SeocheonInput.PointerPosition;

            // ★스크롤 밖으로 밀려난 줄은 화면에 안 보여도 RectTransform 은 그 자리에 있습니다.
            //   뷰포트 안인지 먼저 걸러 내지 않으면 ★보이지 않는 어절이 눌립니다.
            bool insideViewport = logViewport == null ||
                RectTransformUtility.RectangleContainsScreenPoint(logViewport, pointer, uiCamera);

            DialogueLineView foundLine = null;
            int foundIndex = -1;

            if (insideViewport)
            {
                for (int i = npcLines.Count - 1; i >= 0; i--)      // 최근 발화부터
                {
                    DialogueLineView line = npcLines[i];
                    if (line == null || !line.gameObject.activeInHierarchy || !line.HasPickable) continue;
                    int hit = line.HitTest(pointer, uiCamera);
                    if (hit >= 0) { foundLine = line; foundIndex = hit; break; }
                }
            }

            if (foundLine != hoverLine || foundIndex != hoverIndex)
            {
                if (hoverLine != null && hoverLine != foundLine) hoverLine.SetHover(-1, hoverColor);
                hoverLine = foundLine;
                hoverIndex = foundIndex;
                if (hoverLine != null) hoverLine.SetHover(hoverIndex, hoverColor);
            }

            // ★대기 중에도 지목은 됩니다.
            if (SeocheonInput.PointerPressedThisFrame && hoverLine != null && hoverIndex >= 0)
            {
                WordPickNote.WordOption option = hoverLine.GetOption(hoverIndex);
                string sentence = hoverLine.SourceSentence;
                // ★표시는 유효/오답 구분 없이 동일한 밑줄 하나뿐입니다.
                hoverLine.MarkConsumed(hoverIndex, consumedColor);
                hoverLine = null;
                hoverIndex = -1;
                if (option != null && WordPicked != null) WordPicked(option, sentence);
            }
        }

        private void ClearHover()
        {
            if (hoverLine != null) hoverLine.SetHover(-1, hoverColor);
            hoverLine = null;
            hoverIndex = -1;
        }
    }
}
