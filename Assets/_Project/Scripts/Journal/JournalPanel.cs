using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 수첩(手帖)의 월드 공간 화면 — 지금 사건의 단서를 물증/정황으로 나눠 보여주고,
    /// 심문 중이면 각 단서에 "들이밀기" 버튼을 붙인다.
    ///
    /// 상태(열림/닫힘)는 <see cref="JournalView"/>가 들고 있고, 여기는 그리기만 한다.
    /// 씬에 미리 둘 필요 없다 — 수첩을 처음 펼칠 때 스스로 만들어진다.
    ///
    /// 카드가 많아지면 스크롤이 필요하지만, 지금은 사건당 단서가 20개 남짓이라
    /// 두 칸으로 나눠 한 화면에 담는다(VR에서 스크롤은 조작이 번거롭다).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class JournalPanel : MonoBehaviour
    {
        [SerializeField] private Font _font;
        [SerializeField] private int _titleFontSize = 34;
        [SerializeField] private int _clueFontSize = 26;
        [SerializeField] private Color _paperColor = new Color(0.90f, 0.86f, 0.74f, 0.96f);
        [SerializeField] private Color _inkColor = new Color(0.16f, 0.11f, 0.07f);
        [SerializeField] private Color _cardColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color _presentColor = new Color(0.62f, 0.14f, 0.11f, 0.92f);
        [Tooltip("물증을 다시 펼쳐 보는 단추. 들이밀기(붉은색)와 헷갈리지 않게 먹빛으로")]
        [SerializeField] private Color _readColor = new Color(0.24f, 0.22f, 0.18f, 0.90f);

        private static JournalPanel _instance;

        private JournalView _owner;
        private CanvasGroup _group;
        private WorldHudAnchor _anchor;
        private RectTransform _leftCol, _rightCol;
        private Text _title;
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
            _instance.OpenInternal(owner);
        }

        public static void Close()
        {
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
            var page = NewRect("한지", Vector2.zero, new Vector2(PageW, PageH), transform);
            page.gameObject.AddComponent<Image>().color = _paperColor;

            _title = NewText("제목", "수첩", new Vector2(0f, PageH * 0.5f - 60f),
                             new Vector2(PageW - 80f, 60f), page, _titleFontSize, _inkColor);

            var close = NewRect("닫기", new Vector2(PageW * 0.5f - 140f, PageH * 0.5f - 60f),
                                new Vector2(200f, 66f), page);
            var closeBg = close.gameObject.AddComponent<Image>();
            closeBg.color = new Color(0.30f, 0.12f, 0.10f, 0.85f);
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => _owner?.Close());
            NewText("라벨", "✕ 덮기", Vector2.zero, new Vector2(200f, 66f), close, _clueFontSize,
                    new Color(0.98f, 0.94f, 0.86f));

            float colW = (PageW - 120f) * 0.5f;
            _leftCol  = NewRect("물증", new Vector2(-colW * 0.5f - 20f, -40f), new Vector2(colW, PageH - 200f), page);
            _rightCol = NewRect("정황", new Vector2( colW * 0.5f + 20f, -40f), new Vector2(colW, PageH - 200f), page);
        }

        // ── 내용(펼칠 때마다) ──

        private void Rebuild()
        {
            foreach (var c in _cards) if (c != null) Destroy(c);
            _cards.Clear();

            var gs = GameState.Instance;
            if (!gs.InCase)
            {
                _title.text = "수첩 — 사건 밖에서는 단서가 보이지 않는다";
                return;
            }

            CaseId caseId = gs.CurrentCase.Value;
            var clues = Journal.Instance.GetClues(caseId);
            string brief = _owner != null ? _owner.CaseBrief : null;
            _title.text = string.IsNullOrWhiteSpace(brief)
                        ? $"수첩 — 단서 {clues.Count}"
                        : $"{brief}      (단서 {clues.Count})";

            // 심문 중일 때만 들이밀 수 있다
            bool talking = InterrogationController.AnyOpen && InterrogationController.Active != null;

            var muljeung = new List<ClueEntry>();
            var jeonghwang = new List<ClueEntry>();
            foreach (var c in clues) (c.kind == ClueKind.물증 ? muljeung : jeonghwang).Add(c);

            FillColumn(_leftCol, "물증", muljeung, caseId, talking);
            FillColumn(_rightCol, "정황", jeonghwang, caseId, talking);
        }

        private void FillColumn(RectTransform col, string header, List<ClueEntry> list,
                                CaseId caseId, bool talking)
        {
            float w = col.sizeDelta.x;
            float y = col.sizeDelta.y * 0.5f - 40f;

            var h = NewText($"머리{header}", $"── {header} ──", new Vector2(0f, y), new Vector2(w, 44f),
                            col, _clueFontSize + 4, _inkColor);
            _cards.Add(h.gameObject);
            y -= 60f;

            if (list.Count == 0)
            {
                var e = NewText("없음", "(아직 없다)", new Vector2(0f, y), new Vector2(w, 40f),
                                col, _clueFontSize, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.5f));
                _cards.Add(e.gameObject);
                return;
            }

            foreach (var c in list)
            {
                const float cardH = 76f;
                var card = NewRect("단서", new Vector2(0f, y - cardH * 0.5f), new Vector2(w, cardH), col);
                card.gameObject.AddComponent<Image>().color = _cardColor;
                _cards.Add(card.gameObject);

                // 심문에서 이미 밝혀진 사실("_revealed")은 다시 들이밀 수 없다.
                // 조사종이처럼 처음부터 쥐고 있던 것도 마찬가지다 — 증거가 아니라 출발점이다.
                bool canPresent = talking && c.presentable && !c.key.EndsWith("_revealed");

                // 물증에 딸린 종이가 있으면 수첩에서 다시 펼쳐 본다. 정황은 들은 것이라 볼 것이 없다.
                var doc = Journal.Instance.GetDocument(caseId, c.key);
                bool canRead = doc != null;

                float btnW = (canPresent ? 190f : 0f) + (canRead ? 150f : 0f);

                var label = NewText("문구", "· " + c.text, new Vector2(-btnW * 0.5f, 0f),
                                    new Vector2(w - 32f - btnW, cardH - 12f), card, _clueFontSize, _inkColor);
                label.alignment = TextAnchor.MiddleLeft;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;

                if (canRead)
                {
                    float x = w * 0.5f - (canPresent ? 268f : 80f);
                    var r = NewRect("펼쳐보기", new Vector2(x, 0f), new Vector2(136f, 56f), card);
                    var rbg = r.gameObject.AddComponent<Image>();
                    rbg.color = _readColor;
                    var rbtn = r.gameObject.AddComponent<Button>();
                    rbtn.targetGraphic = rbg;
                    var d = doc;
                    rbtn.onClick.AddListener(() =>
                    {
                        DocumentView.Show(d.page, d.title, d.body, d.fine);
                        _owner?.Close();   // 수첩을 덮어야 종이를 손에 쥔다
                    });
                    NewText("라벨", "펼쳐보기", Vector2.zero, new Vector2(136f, 56f), r, _clueFontSize - 2,
                            new Color(0.98f, 0.94f, 0.86f));
                }

                if (canPresent)
                {
                    var b = NewRect("들이밀기", new Vector2(w * 0.5f - 100f, 0f), new Vector2(176f, 56f), card);
                    var bg = b.gameObject.AddComponent<Image>();
                    bg.color = _presentColor;
                    var btn = b.gameObject.AddComponent<Button>();
                    btn.targetGraphic = bg;
                    var captured = c;
                    btn.onClick.AddListener(() =>
                    {
                        InterrogationController.Active?.PresentFromJournal(captured);
                        _owner?.Close();   // 수첩을 덮어야 제시 장면(대사·증거 그림)이 보인다
                    });
                    NewText("라벨", "들이밀기", Vector2.zero, new Vector2(176f, 56f), b, _clueFontSize - 2,
                            new Color(0.98f, 0.94f, 0.86f));
                }

                y -= cardH + 10f;
            }
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
