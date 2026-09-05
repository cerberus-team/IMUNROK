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
    /// (VR에서 스크롤은 조작이 번거롭다).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class JournalPanel : MonoBehaviour
    {
        [SerializeField] private Font _font;
        [SerializeField] private int _titleFontSize = 34;
        [SerializeField] private int _clueFontSize = 26;
        [Tooltip("켜면 색을 <b>꾸러미(IMUNROK.Ui)</b> 에서 받아 온다 — 견우팀 판과 결이 같아진다. " +
                 "뜯어고칠 일이 아니었다: 꾸러미의 소지품 판도 <b>한지</b>다(InventorySkin 의 " +
                 "색 한 벌이 한지·먹·주칠이다). 우리와 같은 결이라 값만 옮기면 된다")]
        [SerializeField] private bool _useCommonLook = true;
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
        private WorldHudAnchor _anchor;
        private RectTransform _col;
        private Text _title, _brief;
        private readonly List<GameObject> _cards = new List<GameObject>();

        private const float PageW = 1500f, PageH = 900f;

        public static void Open(JournalView owner)
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<JournalPanel>();
                if (_instance == null)
                {
                    var go = new GameObject("VR_수첩", typeof(Canvas));
                    go.AddComponent<WorldHudAnchor>().Configure(WorldHudAnchor.Placement.Front);
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
            _anchor = GetComponent<WorldHudAnchor>();
            if (_anchor == null) _anchor = gameObject.AddComponent<WorldHudAnchor>();
            // 수첩은 손에 든 것처럼 가깝게, 눈높이보다 조금 아래
            _anchor.SetDistance(0.85f, -0.20f);
            _anchor.SetStowable(false);   // 물러나는 쪽이 아니라 물러나게 하는 쪽이다

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
            Rebuild();
            SetVisible(true);
            if (_anchor != null) _anchor.Recenter();
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
            if (!gs.InCase)
            {
                _title.text = "수첩 — 사건 밖에서는 물증이 보이지 않는다";
                _brief.text = "";
                return;
            }

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

            const float cw = 320f, ch = 190f, gap = 14f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((w + gap) / (cw + gap)));
            float x0 = -(perRow * cw + (perRow - 1) * gap) * 0.5f + cw * 0.5f;

            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                int r = i / perRow, k = i % perRow;
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
                var dd = doc;
                var cc = c;
                btn.onClick.AddListener(() =>
                {
                    // 수첩에서 꺼내 든 것은 어둠 위에 놓는다(dim) — 둘레가 비어야 그 하나만 보인다
                    if (dd != null) DocumentView.Show(dd.page, dd.title, dd.body, dd.fine, null, true);
                    else DocumentView.Show(shot, ShortName(null, cc), cc.text, null, null, true);
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
        }

        /// <summary>카드에 적을 짧은 이름. 문서가 있으면 그 제목, 없으면 단서 문구의 앞 토막.</summary>
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
