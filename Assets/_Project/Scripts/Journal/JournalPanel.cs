using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 수첩(手帖)의 월드 공간 화면 — 지금 사건에서 <b>주운 물증</b>을 카드로 깔고,
    /// 심문 중이면 각 물증에 "들이밀기" 버튼을 붙인다.
    ///
    /// 상태(열림/닫힘)는 <see cref="JournalView"/>가 들고 있고, 여기는 그리기만 한다.
    /// 씬에 미리 둘 필요 없다 — 수첩을 처음 펼칠 때 스스로 만들어진다.
    ///
    /// <b>정황 칸은 없앴다</b>. 예전에는 왼쪽에 물증, 오른쪽에 정황(증언·목격·실토)을
    /// 나란히 깔았는데, 그러면 들이밀 수 있는 것과 그저 들은 것이 한 장에 섞여
    /// 어느 쪽이 상대의 입을 여는 물건인지 알 수 없게 된다. 들은 말은 이제 심문
    /// 자막에서 붉게 한 번 지나가고, 수첩에는 손에 쥔 것만 남는다.
    ///
    /// 그래서 물증 카드가 한 장 넓이를 다 쓴다 — 한 줄에 넉 장씩 들어간다.
    /// 카드가 많아지면 스크롤이 필요하지만, 사건당 물증이 열 남짓이라 한 화면에 담긴다
    /// (긴 목록을 굴리는 것은 조작이 번거롭다).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class JournalPanel : MonoBehaviour
    {
        [SerializeField] private Font _font;
        [SerializeField] private int _titleFontSize = 44;
        [SerializeField] private int _clueFontSize = 34;
        private Color _paperColor = UiLook.With(UiLook.Paper, 0.96f);
        private Color _inkColor = UiLook.Ink;
        private Color _cardColor = UiLook.With(UiLook.PaperDim, 0.45f);
        private Color _presentColor = UiLook.With(UiLook.Seal, 0.92f);
        [Tooltip("물증을 다시 펼쳐 보는 단추. 들이밀기(붉은색)와 헷갈리지 않게 먹빛으로")]
        private Color _readColor = UiLook.With(UiLook.InkSoft, 0.90f);

        /// <summary>꾸러미의 결. 정적으로 들면 판을 넘겨 살아남되 내용이 죽는다 — 판마다 제 것.</summary>
        private readonly IMUNROK.Ui.InventorySkin _skin = new IMUNROK.Ui.InventorySkin();

        private static JournalPanel _instance;

        private JournalView _owner;
        private CanvasGroup _group;
        private RectTransform _col;
        private Text _title, _brief;
        private readonly List<GameObject> _cards = new List<GameObject>();

        /// <summary>
        /// <b>판의 좌표계를 견우팀 꾸러미에서 통째로 받아 왔다.</b>
        ///
        /// 화면에 보이는 크기는 여태도 거의 같았다 — 재 보면 우리 82.8°×55.8°,
        /// 저쪽 84°×55° 였고 본문 글자도 1.75° 대 1.82° 였다. 다른 것은 <b>단위</b>였다:
        /// 저쪽은 1920×1120 을 0.0014m 로 세워 1.50m 앞에 두고, 우리는 1500×900 을
        /// 0.001m 로 세워 0.85m 앞에 두었다. 보이기는 같은데 <b>적히는 수가 달라</b>,
        /// 저쪽 치수를 한 줄도 그대로 옮겨 올 수 없었다.
        ///
        /// 그래서 좌표계째로 받는다. 그러면 칸 320×272 · 사이 28 · 본문 34 같은 수가
        /// <b>뜻을 지닌 채</b> 건너온다 — 자막 바에서 <c>BarStyle</c> 을 그렇게 받아 온 것과
        /// 같은 셈이다. 한 줄에 다섯 장이 들어가는 것도 이 폭에서 저절로 나온다.
        /// </summary>
        private static float PageW { get { return IMUNROK.Ui.InventoryUI.PanelW; } }
        private static float PageH { get { return IMUNROK.Ui.InventoryUI.PanelH; } }

        /// <summary>
        /// <b>판이 화면 세로에서 차지하는 몫.</b>
        ///
        /// 월드에 세울 때는 「1.79m 앞」이 그 몫을 정했다. 화면에 붙인 뒤로는 거리라는
        /// 것이 없으므로 그 몫을 <b>곧바로</b> 적는다 — 저쪽 판이 저쪽 화면에서 덮던
        /// 79%, 우리가 재서 맞춰 둔 76% 가 이 수다.
        /// </summary>
        private const float PageScreenShare = 0.76f;

        /// <summary>
        /// <b>저쪽 판이 저쪽 화면에서 차지하던 몫을 우리 화면에서도 지킨다.</b>
        ///
        /// 저쪽 <c>PanelDistance</c> 는 1.50m 인데, 그 값은 <b>세로 화각 70°</b> 짜리
        /// 게임 뷰를 놓고 잡은 것이다 — 거기서 55° 짜리 판은 화면의 79% 를 덮어
        /// 「거의 채운」 것으로 읽힌다. 우리 카메라는 60° 라, 같은 1.50m 에 세우면
        /// 92% 가 되어 <b>아래가 잘려 나간다</b>(재 보니 뷰포트 −0.23 까지 내려갔다).
        ///
        /// 그래서 거리로 되받는다. 1.568m 짜리 판이 47.4°(=60°의 79%)로 보이는 자리는
        /// 1.79m 다. 재 보면 가로 74% · 세로 76% — 저쪽이 적어 둔 「70%대」다.
        /// 숫자를 옮겨 오는 것과 <b>보이는 몫을 옮겨 오는 것</b>은 다른 일이고,
        /// 여기서 지킬 것은 뒤엣것이다.
        /// </summary>
        /// (더 쓰지 않는다 — 화면에 붙였으므로 거리가 없다. 위 셈의 내력이라 남긴다)

        public static void Open(JournalView owner)
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<JournalPanel>();
                if (_instance == null)
                {
                    var go = new GameObject("수첩", typeof(Canvas),
                                            typeof(UnityEngine.UI.CanvasScaler),
                                            typeof(UnityEngine.UI.GraphicRaycaster));
                    _instance = go.AddComponent<JournalPanel>();
                }
            }
            // 읽는 자리는 하나뿐이다. 손에 든 문서가 있으면 그것이 내려간다.
            ReadingFocus.Claim(ReadingFocus.Panel.Journal, Close);
            _instance.OpenInternal(owner);
        }

        /// <summary>Esc 로도 덮는다 — 단추가 안 먹는 날에도 빠져나올 길은 있어야 한다.</summary>
        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();

            // 휠로도 쪽을 넘긴다. 저쪽 꾸러미가 휠에 쪽 넘기기를 걸어 두었으니
            // 같은 판을 보는 사람이 같은 손짓을 쓰는 것이 맞다.
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null && _pageCount > 1)
            {
                float dy = m.scroll.ReadValue().y;
                if (dy > 0.01f) Turn(-1);
                else if (dy < -0.01f) Turn(1);
            }
#endif
        }

        public static void Close()
        {
            ReadingFocus.Release(ReadingFocus.Panel.Journal);
            if (_instance != null) _instance.SetVisible(false);
        }

        /// <summary>단서가 새로 기록되면 다시 그린다(펼쳐둔 채로 단서를 얻는 경우).</summary>
        public static void Refresh()
        {
            if (_instance != null && _instance._group.alpha > 0.5f) _instance.Rebuild();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            SitOnScreen();

            _font = UiFont.Resolve(_font);
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            BuildFrame();
            SetVisible(false);
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void OpenInternal(JournalView owner)
        {
            _owner = owner;
            _pageNo = 0;              // 펼 때마다 첫 쪽부터
            Rebuild();
            SetVisible(true);
        }

        /// <summary>
        /// <b>수첩을 월드에서 떼어 화면에 붙인다.</b>
        ///
        /// 여태 이 판은 눈앞 허공에 세워 두고 고개를 따라오게 했다. 세상에 세워 두던
        /// 시절의 방식이다 — 거기서는 화면이라는 것이 없어서 <b>세울 데가 세상밖에</b>
        /// 없었다. 화면으로 보는 지금 그 방식이 남기는 것은 두 가지 탈뿐이다:
        ///
        ///   · <b>기둥에 뚫린다.</b> 월드에 놓인 판이라 깊이 검사를 받는다. 조사청에서
        ///     재 보니 대들보가 수첩을 가로질러 지나갔다. 자막판이 재질을 갈아 끼워
        ///     피하던 바로 그 탈이고, 이쪽은 그 손질조차 없었다.
        ///   · <b>떠다닌다.</b> 아무리 굼뜨게 잡아도 고개를 돌리면 판이 뒤따라 흔들린다.
        ///     화면 게임에서 수첩은 <b>펴면 거기 있는</b> 것이지 쫓아오는 것이 아니다.
        ///
        /// 화면에 붙이면 둘 다 한꺼번에 없어진다. 덧붙여 누르기도 제대로 산다 —
        /// 월드 캔버스에는 <c>GraphicRaycaster</c> 조차 안 붙어 있었다.
        ///
        /// 치수는 그대로 쓴다. 견우팀에서 받아 온 1920×1120 좌표계를 <c>CanvasScaler</c>
        /// 의 기준 해상도로 넘기면 칸 320×272 도 글씨 34 도 뜻을 지킨다. 기준 세로를
        /// <c>PageH ÷ PageScreenShare</c> 로 잡는 것이 요점이다 — 그래야 판이 화면
        /// 세로의 그 몫만큼만 덮고, 화면이 넓든 좁든 그 몫이 변하지 않는다.
        /// </summary>
        private void SitOnScreen()
        {
            // 수첩만 제 좌표계를 쓴다. 판이 화면 세로의 몇 할을 덮을지가 이 판의
            // 셈이라, 기준 세로를 그 몫에서 낸다(다른 판은 공통 1732 를 쓴다).
            ScreenPanel.Raise(gameObject, ScreenPanel.LayerBook, PageH / PageScreenShare);
        }

        /// <summary>테두리 넉 줄. 상자 하나에 외곽선을 그릴 길이 없어 얇은 띠 넷을 두른다.</summary>
        private void Edge(RectTransform page, float w, float h, Color c)
        {
            const float t = 5f;
            Strip(page, "테_위", new Vector2(0f, h * 0.5f - t * 0.5f), new Vector2(w, t), c);
            Strip(page, "테_아래", new Vector2(0f, -h * 0.5f + t * 0.5f), new Vector2(w, t), c);
            Strip(page, "테_좌", new Vector2(-w * 0.5f + t * 0.5f, 0f), new Vector2(t, h), c);
            Strip(page, "테_우", new Vector2(w * 0.5f - t * 0.5f, 0f), new Vector2(t, h), c);
        }

        private void Strip(RectTransform parent, string name, Vector2 at, Vector2 size, Color c)
        {
            var rt = NewRect(name, at, size, parent);
            Skin(rt.gameObject.AddComponent<Image>(), _skin.Wood_, c);
        }

        /// <summary>무늬를 깔고 그 위에 색을 얹는다 — 꾸러미가 판을 그리는 방식이다.</summary>
        private static void Skin(Image im, Sprite sp, Color c)
        {
            im.sprite = sp;
            im.type = Image.Type.Simple;
            im.color = c;
        }

        private void SetVisible(bool on)
        {
            if (_group == null) return;
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
            _group.interactable = on;
        }

        // ── 뼈대(한 번만) ──

        private void BuildFrame()
        {
            // <b>여기서 색을 덮어쓰던 자리를 걷었다.</b> 인스펙터에 손으로 적어 둔 색이
            // 씬에 구워져 있어서, 그것을 실행 중에 꾸러미 색으로 도로 갈아 끼우고
            // 있었다. 이제 칸에 적힌 것이 곧 꾸러미 색이고 인스펙터에 새지도 않으므로
            // 갈아 끼울 것이 없다 — 적힌 값과 보이는 값이 같아졌다.

            var page = NewRect("한지", Vector2.zero, new Vector2(PageW, PageH), transform);
            Skin(page.gameObject.AddComponent<Image>(), _skin.Hanji_, _paperColor);

            // <b>나뭇결 테두리</b> — 꾸러미 판에 있고 우리에게 없던 것이다.
            // 한지 판이 밝은 배경(낮 마당) 앞에 서면 종이의 가장자리가 녹아 사라진다.
            Edge(page, PageW, PageH, UiLook.Wood);

            _title = NewText("제목", "수첩", new Vector2(0f, PageH * 0.5f - 60f),
                             new Vector2(PageW - 80f, 60f), page, _titleFontSize, _inkColor);

            var close = NewRect("닫기", new Vector2(PageW * 0.5f - 140f, PageH * 0.5f - 60f),
                                new Vector2(200f, 66f), page);
            var closeBg = close.gameObject.AddComponent<Image>();
            Skin(closeBg, _skin.Wood_, new Color(UiLook.Wood.r, UiLook.Wood.g, UiLook.Wood.b, 0.85f));
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => _owner?.Close());
            NewText("라벨", "✕ 덮기", Vector2.zero, new Vector2(200f, 66f), close, _clueFontSize,
                    UiLook.SealText);

            // 사건 개요는 제목 바로 밑에 한 줄로 눕힌다 — 조사종이를 안 읽고 지나갔어도
            // 첫 장에는 남아 있어야 한다.
            _brief = NewText("개요", "", new Vector2(0f, PageH * 0.5f - 118f), new Vector2(PageW - 120f, 56f),
                             page, _clueFontSize - 2, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.72f));
            _brief.horizontalOverflow = HorizontalWrapMode.Wrap;

            _col = NewRect("물증", new Vector2(0f, -70f), new Vector2(PageW - 120f, PageH - 260f), page);
        }

        // ── 내용(펼칠 때마다) ──

        private void Rebuild()
        {
            foreach (var c in _cards) if (c != null) Destroy(c);
            _cards.Clear();

            var gs = GameState.Instance;
            if (!gs.InCase) { FillCaseSheets(); return; }

            CaseId caseId = gs.CurrentCase.Value;
            var clues = Journal.Instance.GetClues(caseId);
            string head = _owner != null ? _owner.CaseBrief : null;
            _title.text = string.IsNullOrWhiteSpace(head)
                        ? $"수첩 — 물증 {clues.Count}"
                        : $"{head}      (물증 {clues.Count})";

            string brief = Journal.Instance.GetBrief(caseId);
            _brief.text = string.IsNullOrWhiteSpace(brief) ? "" : brief;

            // 심문 중일 때만 들이밀 수 있다
            bool talking = InterrogationController.AnyOpen && InterrogationController.Active != null;

            var muljeung = new List<ClueEntry>();
            foreach (var c in clues) if (c.kind == ClueKind.물증) muljeung.Add(c);

            FillEvidence(_col, muljeung, caseId, talking);
        }

        /// <summary>
        /// 물증은 <b>카드</b>로 깐다 — 생김새 한 장과 이름.
        ///
        /// 물증은 글이 아니라 물건이다. 줄글로 늘어놓으면 "[J06] 스무 해치 기록 뒤…" 같은
        /// 것이 열 줄 쌓이고, 그 중 어느 것이 그 낡은 장부였는지 알 수 없게 된다.
        /// 생김새가 먼저 보여야 손이 기억한 것과 이어진다.
        ///
        /// 카드를 누르면 그 물건을 <b>손에 든다</b> — 끌어 돌려 앞뒤를 보고, 아래에서
        /// 요약을 읽고, 돋보기를 대면 잔글씨까지 읽힌다.
        /// </summary>
        private void FillEvidence(RectTransform col, List<ClueEntry> list, CaseId caseId, bool talking)
        {
            _shown = list;            // 손에 들었을 때 이웃으로 넘어갈 수 있게 들고 있는다
            _shownCase = caseId;
            float w = col.sizeDelta.x;
            float y = col.sizeDelta.y * 0.5f - 40f;

            var h = NewText("머리물증", "── 주운 물증 ──", new Vector2(0f, y), new Vector2(w, 44f),
                            col, _clueFontSize + 4, _inkColor);
            _cards.Add(h.gameObject);
            y -= 64f;

            if (list.Count == 0)
            {
                var e = NewText("없음", "(아직 주운 것이 없다. 들은 말은 여기 적히지 않는다)",
                                new Vector2(0f, y), new Vector2(w, 40f),
                                col, _clueFontSize, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.5f));
                _cards.Add(e.gameObject);
                return;
            }

            // <b>칸 치수를 저쪽에 물어 온다.</b> 여태 320·272·28 을 손으로 적어 두었는데,
            // 적어 둔 수는 저쪽이 고쳐도 안 따라온다. 꾸러미의 그 칸들이 닫혀 있어
            // 베낄 수밖에 없던 것이라, 열고 나서 갈아 끼웠다.
            float cw = IMUNROK.Ui.InventoryUI.CellW;
            float ch = IMUNROK.Ui.InventoryUI.CellH;
            float gap = IMUNROK.Ui.InventoryUI.Gap;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((w + gap) / (cw + gap)));
            float x0 = -(perRow * cw + (perRow - 1) * gap) * 0.5f + cw * 0.5f;

            // <b>넘치면 쪽을 넘긴다.</b> 여태 한 판에 다 깔았다 — 사건당 물증이 열 남짓이라
            // 한 화면에 담긴다고 보았기 때문인데, 제2막의 관아 문서까지 더하면 그 수를
            // 넘는다. 넘친 것은 <b>없는 것과 같다</b>: 판 밖으로 나간 카드는 보이지도
            // 눌리지도 않으면서 있는 줄 알게 만든다.
            //
            // 몇 장이 들어가는지는 <b>남은 자리가 정한다</b>. 칸 크기를 저쪽에서 받아 왔으니
            // 쪽당 장수도 저쪽 것을 베낄 것이 아니라 이 판에서 나와야 맞는다.
            float roomY = y + col.sizeDelta.y * 0.5f - 56f;          // 아래에 쪽 넘기는 줄을 남긴다
            int rows = Mathf.Max(1, Mathf.FloorToInt((roomY + gap) / (ch + gap)));
            _perPage = Mathf.Max(1, perRow * rows);
            int pages = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)_perPage));
            _pageNo = Mathf.Clamp(_pageNo, 0, pages - 1);
            _pageCount = pages;

            int from = _pageNo * _perPage;
            int to = Mathf.Min(list.Count, from + _perPage);

            for (int i = from; i < to; i++)
            {
                var c = list[i];
                int r = (i - from) / perRow, k = (i - from) % perRow;
                var card = NewRect("증거카드",
                                   new Vector2(x0 + k * (cw + gap), y - ch * 0.5f - r * (ch + gap)),
                                   new Vector2(cw, ch), col);
                var bg = card.gameObject.AddComponent<Image>();
                Skin(bg, _skin.Slot_, _cardColor);
                _cards.Add(card.gameObject);

                var doc = Journal.Instance.GetDocument(caseId, c.key);
                Texture2D shot = doc != null ? doc.page : Journal.Instance.GetClueImage(caseId, c.key);

                // 생김새
                var shotRt = NewRect("모양", new Vector2(0f, 22f), new Vector2(cw - 40f, 108f), card);
                if (shot != null)
                {
                    var raw = shotRt.gameObject.AddComponent<RawImage>();
                    raw.texture = shot;
                    raw.raycastTarget = false;
                    float ar = (float)shot.width / Mathf.Max(1, shot.height);
                    float hh = 108f, ww = Mathf.Min(cw - 40f, hh * ar);
                    shotRt.sizeDelta = new Vector2(ww, hh);
                }
                else
                {
                    var mark = NewText("표", "?", Vector2.zero, new Vector2(cw - 40f, 108f), shotRt,
                                       _clueFontSize + 20, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.35f));
                    mark.raycastTarget = false;
                }

                // 이름 — 단서 문구의 첫 토막만. 나머지는 손에 들면 아래에 나온다.
                var name = NewText("이름", ShortName(doc, c), new Vector2(0f, -66f),
                                   new Vector2(cw - 24f, 54f), card, _clueFontSize - 4, _inkColor);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.raycastTarget = false;

                // 카드 전체가 단추다 — 누르면 손에 든다
                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                int at = i;
                btn.onClick.AddListener(() =>
                {
                    OpenAt(at);
                    _owner?.Close();   // 수첩을 덮어야 두 손이 빈다
                });

                // 심문 중이면 들이밀 수 있다
                bool canPresent = talking && c.presentable && !c.key.EndsWith("_revealed");
                if (!canPresent) continue;
                var b = NewRect("들이밀기", new Vector2(0f, -ch * 0.5f + 26f), new Vector2(cw - 40f, 44f), card);
                var pbg = b.gameObject.AddComponent<Image>();
                pbg.color = _presentColor;
                var pbtn = b.gameObject.AddComponent<Button>();
                pbtn.targetGraphic = pbg;
                var captured = c;
                pbtn.onClick.AddListener(() =>
                {
                    InterrogationController.Active?.PresentFromJournal(captured);
                    _owner?.Close();
                });
                NewText("라벨", "들이밀기", Vector2.zero, new Vector2(cw - 40f, 44f), b, _clueFontSize - 6,
                        UiLook.SealText);
            }

            PageRow(col, pages);
        }

        /// <summary>카드에 적을 짧은 이름. 문서가 있으면 그 제목, 없으면 단서 문구의 앞 토막.</summary>
        /// <summary>
        /// <b>조사청에서 펴면 사건 종이만 보인다.</b>
        ///
        /// 여태 「사건 밖에서는 물증이 보이지 않는다」 한 줄만 띄우고 판을 비웠다.
        /// 물증이 안 보이는 것은 맞다 — 그것은 <b>사건 안에서 손에 잡은 것</b>이고,
        /// 방을 나오면 그 방에 두고 온다. 그러나 <b>무엇을 맡았는지</b>는 방을 나와서도
        /// 알아야 한다. 어전에서 받아 온 봉서는 물증이 아니라 <b>출발점</b>이기 때문이다.
        ///
        /// 그래서 조사청 수첩에는 받아 온 사건 종이만 사건 차례로 눕는다.
        /// 끝난 사건의 물증을 여기서 다시 뒤지게 하지 않는다 — 조사청은 정리하는
        /// 방이 아니라 <b>다음 사건을 고르는</b> 방이다.
        /// </summary>
        private void FillCaseSheets()
        {
            var sheets = Journal.Instance.CaseSheets();
            _title.text = sheets.Count == 0
                        ? "수첩 — 아직 맡은 사건이 없다"
                        : "수첩 — 맡은 사건 " + sheets.Count;
            _brief.text = "";
            _pageCount = 1;
            _pageNo = 0;
            _shown = null;                  // 여기서는 이웃으로 넘길 물증 목록이 없다

            float w = _col.sizeDelta.x;
            float y = _col.sizeDelta.y * 0.5f - 40f;

            var h = NewText("머리사건", "── 받아 온 봉서 ──", new Vector2(0f, y), new Vector2(w, 44f),
                            _col, _clueFontSize + 4, _inkColor);
            _cards.Add(h.gameObject);
            y -= 64f;

            if (sheets.Count == 0)
            {
                var e = NewText("없음", "(어전에서 봉서를 받으면 여기 남는다)",
                                new Vector2(0f, y), new Vector2(w, 40f),
                                _col, _clueFontSize, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.5f));
                _cards.Add(e.gameObject);
                return;
            }

            float cw = IMUNROK.Ui.InventoryUI.CellW, ch = IMUNROK.Ui.InventoryUI.CellH,
                  gap = IMUNROK.Ui.InventoryUI.Gap;
            float x0 = -(sheets.Count * cw + (sheets.Count - 1) * gap) * 0.5f + cw * 0.5f;

            for (int i = 0; i < sheets.Count; i++)
            {
                var d = sheets[i];
                var card = NewRect("사건카드", new Vector2(x0 + i * (cw + gap), y - ch * 0.5f),
                                   new Vector2(cw, ch), _col);
                var bg = card.gameObject.AddComponent<Image>();
                Skin(bg, _skin.Slot_, _cardColor);
                _cards.Add(card.gameObject);

                var shotRt = NewRect("모양", new Vector2(0f, 22f), new Vector2(cw - 40f, 108f), card);
                if (d.page != null)
                {
                    var img = shotRt.gameObject.AddComponent<RawImage>();
                    img.texture = d.page;
                    img.raycastTarget = false;
                    float ar = d.page.height > 0 ? (float)d.page.width / d.page.height : 1f;
                    float hh = 108f, ww = hh * ar;
                    if (ww > cw - 40f) { ww = cw - 40f; hh = ww / Mathf.Max(0.01f, ar); }
                    shotRt.sizeDelta = new Vector2(ww, hh);
                }

                var name = NewText("이름", string.IsNullOrEmpty(d.title) ? d.caseId.ToString() : d.title,
                                   new Vector2(0f, -66f), new Vector2(cw - 24f, 54f),
                                   card, _clueFontSize - 4, _inkColor);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.raycastTarget = false;

                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                var dd = d;
                btn.onClick.AddListener(() =>
                {
                    DocumentView.Show(dd.page, dd.title, dd.body, dd.fine, null, true,
                                      null, null, null, null, dd.back);
                    DocumentView.SetStageModel(dd.model, dd.modelEuler);
                    _owner?.Close();
                });
            }
        }

        /// <summary>지금 판에 깔린 물증들. 손에 든 종이에서 이웃으로 넘어갈 때 쓴다.</summary>
        private List<ClueEntry> _shown;
        private CaseId _shownCase;

        /// <summary>
        /// <b>이 자리의 물증을 손에 든다</b> — 그리고 앞뒤 이웃을 일러 준다.
        ///
        /// 여태 카드를 누르면 종이 한 장을 펴 주고 끝이었다. 그러면 다음 것을 보려고
        /// 매번 수첩을 폈다 덮었다 해야 하는데, 물증은 <b>견주어 보는</b> 물건이다 —
        /// 필적이 같은지 다른지는 두 장을 잇달아 봐야 안다.
        /// </summary>
        private void OpenAt(int i)
        {
            if (_shown == null || i < 0 || i >= _shown.Count) return;
            var c = _shown[i];
            var doc = Journal.Instance.GetDocument(_shownCase, c.key);
            Texture2D shot = doc != null ? doc.page : Journal.Instance.GetClueImage(_shownCase, c.key);

            // 수첩에서 꺼내 든 것은 어둠 위에 놓는다(dim) — 둘레가 비어야 그 하나만 보인다
            if (doc != null)
            {
                DocumentView.Show(doc.page, doc.title, doc.body, doc.fine, null, true,
                                  null, null, null, null, doc.back);
                DocumentView.SetStageModel(doc.model, doc.modelEuler);   // 종이가 아니면 무대로
            }
            else
                DocumentView.Show(shot, ShortName(null, c), c.text, null, null, true);

            DocumentView.SetNeighbors(i > 0 ? new System.Action(() => OpenAt(i - 1)) : null,
                                      i < _shown.Count - 1 ? new System.Action(() => OpenAt(i + 1)) : null);
        }

        /// <summary>
        /// <b>쪽 넘기는 줄</b> — 판 아래에 「◀   2 / 3   ▶」.
        ///
        /// 휠로도 넘어간다. 저쪽 꾸러미가 휠에 쪽 넘기기를 걸어 두었고(<c>UiWords.Wheel</c>),
        /// 같은 판을 보는 사람이 같은 손짓을 쓰는 것이 맞다.
        /// </summary>
        private void PageRow(RectTransform col, int pages)
        {
            if (pages <= 1) return;
            float w = col.sizeDelta.x;
            float y = -col.sizeDelta.y * 0.5f + 30f;

            var row = NewRect("쪽", new Vector2(0f, y), new Vector2(w, 56f), col);
            _cards.Add(row.gameObject);

            MakePageStep(row, -150f, "◀", -1);
            var lab = NewText("쪽수", (_pageNo + 1) + " / " + pages, Vector2.zero, new Vector2(200f, 56f),
                              row, _clueFontSize - 2, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.72f));
            lab.raycastTarget = false;
            MakePageStep(row, 150f, "▶", 1);
        }

        private void MakePageStep(RectTransform row, float x, string glyph, int step)
        {
            var rt = NewRect("쪽넘김", new Vector2(x, 0f), new Vector2(72f, 56f), row);
            var bg = rt.gameObject.AddComponent<Image>();
            Skin(bg, _skin.Wood_, new Color(UiLook.Wood.r, UiLook.Wood.g, UiLook.Wood.b, 0.85f));
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => Turn(step));
            NewText("글", glyph, Vector2.zero, new Vector2(72f, 56f), rt, _clueFontSize, UiLook.SealText);
        }

        /// <summary>쪽을 넘긴다. 끝에서는 더 안 간다 — 도로 첫 쪽으로 돌면 어디까지 봤는지 잃는다.</summary>
        private void Turn(int step)
        {
            int want = Mathf.Clamp(_pageNo + step, 0, Mathf.Max(0, _pageCount - 1));
            if (want == _pageNo) return;
            _pageNo = want;
            Rebuild();
        }

        /// <summary>지금 보고 있는 쪽. 덮었다 펴면 첫 쪽부터다.</summary>
        private int _pageNo;
        private int _pageCount = 1;
        private int _perPage = 15;

        private static string ShortName(Journal.ClueDocument doc, ClueEntry c)
        {
            if (doc != null && !string.IsNullOrEmpty(doc.title)) return Emphasis.Plain(doc.title);
            // 카드 이름은 토막이라 강조 표시를 그대로 두면 별표가 글자로 남는다 — 걷어낸다
            string t = Emphasis.Plain(c.text ?? "");
            int close = t.IndexOf(']');
            if (close >= 0 && close + 1 < t.Length) t = t.Substring(close + 1).Trim();
            if (t.Length > 22) t = t.Substring(0, 22) + "…";
            return t;
        }

        // ── UI 헬퍼 ──

        private RectTransform NewRect(string name, Vector2 pos, Vector2 size, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private Text NewText(string name, string content, Vector2 pos, Vector2 size,
                             Transform parent, int fontSize, Color color)
        {
            var rt = NewRect(name, pos, size, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = content;
            return t;
        }
    }
}
