using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IMUNROK.Ui;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>장계를 쓰고 봉한다</b> — 사건 하나가 끝나는 공통 절차.
    ///
    /// 어사는 교의에 앉아 장계를 쓰고, 봉하고, 이문록에 한 줄을 올린다. 사건마다 다른 것은
    /// 채워 넣는 말뿐이다(<see cref="CaseReport"/>).
    ///
    /// <b>2026-09-14 에 새로 지었다.</b>
    ///   · <b>화면에 박힌다.</b> 여태 눈앞 0.62m 에 떠 있는 판(월드)이라 고개를 돌리면 따라오고
    ///     기울어 보여 VR 판 같았다. 이제 화면 덮개(Overlay) 한 장이다.
    ///   · <b>한 줄 = 사람 + 증좌.</b> 사람 칸에는 인물이 <b>다</b> 뜨고, 증좌 칸에는 수첩의 것을 골라
    ///     넣는다(줄마다 셋까지). 증좌를 고르는 판에는 카드 곁에 <b>그 증좌의 수첩 글</b>이 크게 떠서,
    ///     넣기 전에 다시 읽어 볼 수 있다 — 장계를 꼼꼼히 쓰는 것이 이 판의 일이다.
    ///   · <b>접어 둘 수 있다.</b> 교의에서 일어서거나 Esc 를 누르면 접히고, 쓰던 것은 남는다.
    ///     다시 앉으면 그대로 이어 쓴다 — 쓰다가 심문하러, 문서고로 가도 된다.
    ///   · <b>점수로 끝을 가른다.</b> 줄마다 사람 점수 + 증좌 점수(맞지 않는 증좌는 감점),
    ///     증좌 합이 기준을 넘어야 그 줄이 선다. 핵심 줄의 사람을 틀리면 헛끝.
    /// </summary>
    public class VerdictReport : MonoBehaviour
    {
        private const float W = 1600f;
        private const float H = 990f;
        private const int Base = 30;
        private const int PageCards = 8;

        private static VerdictReport _instance;

        /// <summary>지금 장계를 펴 놓고 있나. 다른 조작이 이걸 보고 물러난다.</summary>
        public static bool IsOpen { get { return _instance != null && _instance._open; } }

        /// <summary>쓰다 만 장계 — 접었다 다시 펴도 남는다.</summary>
        private class Draft
        {
            public string[] person;
            public List<string>[] evidence;
            public int[] choice;
            public bool sealed_;
        }

        private static readonly Dictionary<CaseReport, Draft> Drafts = new Dictionary<CaseReport, Draft>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Drafts.Clear(); _justSealed = false; }

        /// <summary>새 판을 시작할 때 — 쓰다 만 장계와 봉한 장계를 다 걷는다(앱을 안 끄고 새로 하면 남아 있었다).</summary>
        public static void ClearDrafts() { Drafts.Clear(); _justSealed = false; }

        private static bool _justSealed;

        /// <summary>
        /// <b>방금 이 판에서 봉했는가</b> — 한 번 읽으면 내린다. 판이 닫힐 때 부르는 쪽(ReportDesk)이 이것으로
        /// 엔딩으로 갈지 정한다. 여태 「판결이 적혀 있나」로 보아, 판결이 이미 있으면 <b>접기만 해도</b> 엔딩으로 갔다.
        /// </summary>
        public static bool ConsumeJustSealed() { bool v = _justSealed; _justSealed = false; return v; }

        private enum View { Sheet, Person, Evidence, Choice, Result }

        private CaseReport _form;
        private Draft _d;
        private System.Action _onClosed;
        private InventorySkin _skin;
        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _board, _layer;
        private bool _open;
        private View _view;
        private int _line = -1;
        private int _page;
        private string _focus;
        private float _confirmUntil;
        private bool _seatedAtOpen;
        private string _note;
        private float _noteUntil;
        private float _closeAt = -1f;

        // ── 열고 닫기 ────────────────────────────

        public static void Open(CaseReport form, System.Action onClosed = null)
        {
            if (form == null) { Debug.LogWarning("[장계] 쓸 글이 없습니다."); return; }
            if (_instance == null)
            {
                var go = new GameObject("장계_판", typeof(RectTransform));
                _instance = go.AddComponent<VerdictReport>();
            }
            _instance.Show(form, onClosed);
        }

        /// <summary>쓰던 것을 남기고 접는다(봉하지 않았으면).</summary>
        public static void Fold() { if (_instance != null && _instance._open) _instance.Close(); }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _skin = new InventorySkin();

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 260;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            // 뒤를 어둡게 덮는다 — <b>눌리는 판</b>이어야 짚는 손(MouseRaySelector)이 방까지 안 닿는다
            var dim = Skin(transform, "어둠", null, new Color(0.02f, 0.02f, 0.03f, 0.62f));
            Stretch(dim, 0f);
            var dimImg = dim.GetComponent<Image>();
            dimImg.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<UnityEngine.UI.Button>();
            dimBtn.transition = Selectable.Transition.None;

            _board = Rect(transform, "판");
            Place(_board, Vector2.zero, new Vector2(W, H));
            Stretch(Skin(_board, "틀", _skin.Wood_, Color.white), 30f);
            Stretch(Skin(_board, "바탕", _skin.Hanji_, Color.white), 0f);
            Stretch(Skin(_board, "테선", _skin.Wood_, InventorySkin.Wood), -14f);
            Stretch(Skin(_board, "속지", _skin.Hanji_, Color.white), -20f);
        }

        private void OnDestroy()
        {
            if (_skin != null) _skin.Dispose();
            if (_instance == this) _instance = null;
        }

        private void Show(CaseReport form, System.Action onClosed)
        {
            _form = form;
            _onClosed = onClosed;
            if (!Drafts.TryGetValue(form, out _d) || _d == null || _d.person == null || _d.person.Length != form.줄들.Length)
            {
                int n = form.줄들 != null ? form.줄들.Length : 0;
                _d = new Draft { person = new string[n], evidence = new List<string>[n], choice = new int[n] };
                for (int i = 0; i < n; i++) { _d.evidence[i] = new List<string>(); _d.choice[i] = -1; }
                Drafts[form] = _d;
            }
            _view = _d.sealed_ ? View.Result : View.Sheet;
            _line = -1; _page = 0; _focus = null; _confirmUntil = 0f; _closeAt = -1f;
            _seatedAtOpen = PlayerSeat.Instance != null && PlayerSeat.Instance.Seated;
            _open = true;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            WorldHudAnchor.StowAll = true;
            SubtitleView.Hide();
            Redraw();
        }

        private void Close()
        {
            if (!_open) return;
            _open = false;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            WorldHudAnchor.StowAll = false;
            var cb = _onClosed; _onClosed = null;
            if (cb != null) cb();
        }

        private void Update()
        {
            if (!_open) return;
            if (_closeAt > 0f && Time.unscaledTime >= _closeAt) { _closeAt = -1f; Close(); return; }

            // 교의에서 일어났다 — 접는다(쓰던 것은 남는다). 펴는 순간에는 아직 앉는 중이라 뒤늦게 센다
            if (!_seatedAtOpen && PlayerSeat.Instance != null && PlayerSeat.Instance.Seated) _seatedAtOpen = true;
            if (_seatedAtOpen && !_d.sealed_ && PlayerSeat.Instance != null && !PlayerSeat.Instance.Seated) { Close(); return; }

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && !Typing.Now && kb.escapeKey.wasPressedThisFrame)
            {
                if (_view == View.Sheet || _view == View.Result) Close();
                else { _view = View.Sheet; Redraw(); }
                return;
            }
#endif
            if (_noteUntil > 0f && Time.unscaledTime > _noteUntil) { _noteUntil = 0f; _note = null; Redraw(); }
        }

        // ── 그리기 ───────────────────────────────

        private void Redraw()
        {
            if (_layer != null) Destroy(_layer.gameObject);
            _layer = Rect(_board, "켜");
            Stretch(_layer, 0f);
            switch (_view)
            {
                case View.Sheet: DrawSheet(); break;
                case View.Person: DrawPersons(); break;
                case View.Evidence: DrawEvidence(); break;
                case View.Choice: DrawChoices(); break;
                case View.Result: DrawResult(); break;
            }
        }

        private void DrawSheet()
        {
            Label(_layer, "표제", "장 계", new Vector2(0f, 440f), new Vector2(W - 200f, 70f), Base + 16, TextAnchor.MiddleCenter, InventorySkin.Ink);
            Label(_layer, "머리말", _form.머리말, new Vector2(0f, 388f), new Vector2(W - 200f, 44f), Base - 2, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
            Rule(_layer, 360f);

            int n = _form.줄들.Length;
            const float top = 340f, bottom = -350f, gap = 10f;
            float rowH = Mathf.Min(122f, (top - bottom - gap * (n - 1)) / Mathf.Max(1, n));
            for (int i = 0; i < n; i++)
            {
                float y = top - rowH * 0.5f - i * (rowH + gap);
                DrawRow(i, y, rowH);
            }

            Label(_layer, "맺음말", _form.맺음말, new Vector2(-300f, -392f), new Vector2(700f, 40f), Base - 4, TextAnchor.MiddleLeft, InventorySkin.InkSoft);

            Btn(_layer, "접기", "접어 두기  (Esc)", new Vector2(-560f, -440f), new Vector2(320f, 60f), InventorySkin.Wood, Close);
            bool ready = Missing() == null;
            bool asking = Time.unscaledTime < _confirmUntil;
            string seal = !ready ? "아직 비운 칸이 있다" : asking ? "한 번 더 — 봉하면 되돌릴 수 없다" : "마패로 봉하다";
            var sb = Btn(_layer, "봉함", seal, new Vector2(470f, -440f), new Vector2(520f, 60f),
                         ready ? InventorySkin.Vermilion : Color.Lerp(InventorySkin.Vermilion, InventorySkin.Wood, 0.6f), Seal);
            sb.interactable = ready;

            string hint = !string.IsNullOrEmpty(_note) ? _note
                        : !ready ? Missing()
                        : "다 채웠다. 다시 읽어 보고 봉하시오. 일어서면 접히고, 쓴 것은 남는다.";
            Label(_layer, "안내", hint, new Vector2(-40f, -440f), new Vector2(520f, 56f), Base - 8, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
        }

        private void DrawRow(int i, float y, float h)
        {
            var L = _form.줄들[i];
            var row = Skin(_layer, "줄" + i, _skin.Slot_, Filled(i) ? Color.white : Color.Lerp(Color.white, InventorySkin.Gold, 0.28f));
            Place(row, new Vector2(0f, y), new Vector2(W - 120f, h));

            Label(row, "번호", Num(i), new Vector2(-(W - 120f) * 0.5f + 46f, 0f), new Vector2(80f, h), Base - 4, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
            var sentence = Label(row, "글", Sentence(i), new Vector2(-150f, 0f), new Vector2(930f, h - 12f), Base - 6, TextAnchor.MiddleLeft, InventorySkin.Ink);
            UiText.Wrap(sentence, true);

            float right = (W - 120f) * 0.5f - 12f;
            if (L.IsChoice)
            {
                string pick = _d.choice[i] >= 0 && _d.choice[i] < L.보기.Length ? "고쳐 고르기" : "처분을 고른다";
                Btn(row, "고르기", pick, new Vector2(right - 160f, 0f), new Vector2(300f, h - 24f), InventorySkin.Wood, delegate { OpenPick(View.Choice, i); });
                return;
            }
            float x = right;
            if (L.증거칸)
            {
                int k = _d.evidence[i].Count;
                Btn(row, "증좌", k == 0 ? "증좌를 넣는다" : "증좌 " + k + " / " + L.증거최대,
                    new Vector2(x - 105f, 0f), new Vector2(210f, h - 24f), InventorySkin.Wood, delegate { OpenPick(View.Evidence, i); });
                x -= 220f;
            }
            if (L.인물칸)
            {
                var who = PersonById(_d.person[i]);
                var pb = Btn(row, "사람", who == null ? "사람을 짚는다" : who.이름, new Vector2(x - 110f, 0f),
                             new Vector2(220f, h - 24f), InventorySkin.Wood, delegate { OpenPick(View.Person, i); });
                var face = who != null ? FaceOf(who) : null;
                if (face != null) Picture(row, face, new Vector2(x - 262f, 0f), new Vector2(h - 28f, h - 28f));
            }
        }

        private void OpenPick(View v, int line)
        {
            if (_d.sealed_) return;
            _view = v; _line = line; _page = 0;
            _focus = v == View.Evidence && _d.evidence[line].Count > 0 ? _d.evidence[line][0] : null;
            Redraw();
        }

        private void Back() { _view = View.Sheet; _line = -1; Redraw(); }

        // ── 사람 고르기 ──

        private void DrawPersons()
        {
            var L = _form.줄들[_line];
            Label(_layer, "물음", string.IsNullOrEmpty(L.물음) ? "누구인가." : L.물음, new Vector2(0f, 430f), new Vector2(W - 200f, 64f), Base + 6, TextAnchor.MiddleCenter, InventorySkin.Ink);
            var s = Label(_layer, "그줄", Num(_line) + ".  " + Sentence(_line), new Vector2(0f, 366f), new Vector2(W - 240f, 70f), Base - 6, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
            UiText.Wrap(s, true);
            Rule(_layer, 322f);

            var ps = _form.인물들;
            int n = ps.Length, cols = n <= 4 ? n : Mathf.CeilToInt(n / 2f);
            int rows = Mathf.CeilToInt(n / (float)Mathf.Max(1, cols));
            float cw = 300f, ch = 300f, gap = 30f;
            for (int k = 0; k < n; k++)
            {
                var p = ps[k];
                int r = k / cols, c = k % cols;
                int inRow = Mathf.Min(cols, n - r * cols);
                float x = (c - (inRow - 1) * 0.5f) * (cw + gap);
                float y = -20f + ((rows - 1) * 0.5f - r) * (ch + gap);
                bool chosen = _d.person[_line] == p.id;
                var card = Btn(_layer, "사람" + k, "", new Vector2(x, y), new Vector2(cw, ch),
                               chosen ? InventorySkin.Gold : InventorySkin.Wood, delegate { _d.person[_line] = p.id; Back(); });
                Stretch(Skin(card.transform, "속", _skin.Slot_, Color.white), -8f);
                var face = FaceOf(p);
                if (face != null) Picture(card.transform, face, new Vector2(0f, 30f), new Vector2(210f, 210f));
                else Label(card.transform, "큰이름", p.이름, new Vector2(0f, 30f), new Vector2(260f, 210f), Base + 14, TextAnchor.MiddleCenter, InventorySkin.Ink);
                Label(card.transform, "이름", p.이름, new Vector2(0f, -118f), new Vector2(280f, 50f), Base - 2, TextAnchor.MiddleCenter, InventorySkin.Ink);
            }
            Btn(_layer, "그만", "그만두다", new Vector2(0f, -440f), new Vector2(360f, 60f), InventorySkin.Wood, Back);
        }

        // ── 증좌 고르기 ──

        private void DrawEvidence()
        {
            var L = _form.줄들[_line];
            var sel = _d.evidence[_line];
            Label(_layer, "물음", string.IsNullOrEmpty(L.물음) ? "무엇으로 그것을 아는가." : L.물음, new Vector2(0f, 430f), new Vector2(W - 200f, 64f), Base + 6, TextAnchor.MiddleCenter, InventorySkin.Ink);
            var s = Label(_layer, "그줄", Num(_line) + ".  " + Sentence(_line), new Vector2(0f, 366f), new Vector2(W - 240f, 70f), Base - 6, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
            UiText.Wrap(s, true);
            Rule(_layer, 322f);

            var pool = Pool();
            if (pool.Count == 0)
            {
                Label(_layer, "없음", "수첩에 적힌 것이 아직 없다 — 나가서 더 살펴야 한다.", Vector2.zero, new Vector2(1000f, 80f), Base, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
                Btn(_layer, "그만", "돌아가기", new Vector2(0f, -440f), new Vector2(360f, 60f), InventorySkin.Wood, Back);
                return;
            }
            int pages = Mathf.CeilToInt(pool.Count / (float)PageCards);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            if (_focus == null) _focus = pool[Mathf.Min(_page * PageCards, pool.Count - 1)].key;

            // 왼쪽 — 카드 넷씩 두 줄
            const float cw = 180f, ch = 230f, gap = 16f;
            float left = -W * 0.5f + 50f;
            for (int k = 0; k < PageCards; k++)
            {
                int idx = _page * PageCards + k;
                if (idx >= pool.Count) break;
                var e = pool[idx];
                int r = k / 4, c = k % 4;
                float x = left + cw * 0.5f + c * (cw + gap);
                float y = 170f - r * (ch + gap);
                int order = sel.IndexOf(e.key);
                bool focus = e.key == _focus;
                string key = e.key;
                var card = Btn(_layer, "카드" + k, "", new Vector2(x, y), new Vector2(cw, ch),
                               order >= 0 ? InventorySkin.Vermilion : focus ? InventorySkin.Gold : InventorySkin.Wood,
                               delegate { _focus = key; Toggle(key); });
                Stretch(Skin(card.transform, "속", _skin.Slot_, Color.white), -7f);
                var pic = PictureOfClue(e.key);
                if (pic != null) Picture(card.transform, pic, new Vector2(0f, 30f), new Vector2(cw - 26f, ch - 90f));
                var t = Label(card.transform, "이름", TitleOf(e), new Vector2(0f, -ch * 0.5f + 32f), new Vector2(cw - 16f, 56f), Base - 12, TextAnchor.MiddleCenter, InventorySkin.Ink);
                UiText.Wrap(t, true);
                if (order >= 0) Label(card.transform, "차례", (order + 1).ToString(), new Vector2(cw * 0.5f - 22f, ch * 0.5f - 22f), new Vector2(36f, 36f), Base - 4, TextAnchor.MiddleCenter, InventorySkin.Vermilion);
            }
            if (pages > 1)
            {
                float mid = left + 2f * (cw + gap) - gap * 0.5f;
                Btn(_layer, "앞장", "◀", new Vector2(mid - 140f, -300f), new Vector2(90f, 50f), InventorySkin.Wood, delegate { _page = Mathf.Max(0, _page - 1); _focus = null; Redraw(); });
                Label(_layer, "쪽", (_page + 1) + " / " + pages, new Vector2(mid, -300f), new Vector2(120f, 50f), Base - 6, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
                Btn(_layer, "뒷장", "▶", new Vector2(mid + 140f, -300f), new Vector2(90f, 50f), InventorySkin.Wood, delegate { _page = Mathf.Min(pages - 1, _page + 1); _focus = null; Redraw(); });
            }

            // 오른쪽 — 짚은 증좌의 수첩 글
            var f = pool.Find(x => x.key == _focus);
            var pane = Skin(_layer, "살피기", _skin.Slot_, Color.white);
            Place(pane, new Vector2(410f, -10f), new Vector2(680f, 620f));
            if (f != null)
            {
                var pic = PictureOfClue(f.key);
                if (pic != null) Picture(pane, pic, new Vector2(0f, 170f), new Vector2(620f, 250f));
                Label(pane, "제목", TitleOf(f), new Vector2(0f, 20f), new Vector2(640f, 44f), Base - 2, TextAnchor.MiddleCenter, InventorySkin.Ink);
                var body = Label(pane, "글", Emphasis.Rich(Clean(f.text), Emphasis.OnPaper), new Vector2(0f, -160f), new Vector2(620f, 300f), Base - 10, TextAnchor.UpperLeft, InventorySkin.Ink);
                UiText.Wrap(body, true);
                body.overflowMode = TextOverflowModes.Ellipsis;
                bool inIt = sel.Contains(f.key);
                string fk = f.key;
                Btn(_layer, "넣기", inIt ? "이 증좌를 뺀다" : "이 증좌를 넣는다", new Vector2(410f, -350f), new Vector2(360f, 56f),
                    inIt ? InventorySkin.Wood : InventorySkin.Vermilion, delegate { Toggle(fk); });
            }

            string count = "넣은 증좌 " + sel.Count + " / " + L.증거최대 + (string.IsNullOrEmpty(_note) ? "   ·   카드를 누르면 넣고, 다시 누르면 뺀다" : "   ·   " + _note);
            Label(_layer, "셈", count, new Vector2(-380f, -372f), new Vector2(760f, 44f), Base - 8, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
            Btn(_layer, "다", "다 골랐다", new Vector2(0f, -440f), new Vector2(360f, 60f), InventorySkin.Wood, Back);
        }

        private void Toggle(string key)
        {
            var L = _form.줄들[_line];
            var sel = _d.evidence[_line];
            if (sel.Contains(key)) sel.Remove(key);
            else if (sel.Count >= L.증거최대) Say("이 줄에는 " + L.증거최대 + " 가지까지만 넣는다 — 하나를 빼고 넣으시오.");
            else sel.Add(key);
            Redraw();
        }

        // ── 처분 고르기 ──

        private void DrawChoices()
        {
            var L = _form.줄들[_line];
            Label(_layer, "물음", string.IsNullOrEmpty(L.물음) ? "어찌 처결하시라 아뢸 것인가." : L.물음, new Vector2(0f, 430f), new Vector2(W - 200f, 64f), Base + 6, TextAnchor.MiddleCenter, InventorySkin.Ink);
            Rule(_layer, 380f);
            float y = 240f;
            for (int k = 0; k < L.보기.Length; k++)
            {
                if (!Known(L.보기[k])) continue;
                int idx = k;
                bool chosen = _d.choice[_line] == k;
                Btn(_layer, "말" + k, L.보기[k].말, new Vector2(0f, y), new Vector2(W - 360f, 96f),
                    chosen ? InventorySkin.Gold : InventorySkin.Wood, delegate { _d.choice[_line] = idx; Back(); });
                y -= 116f;
            }
            Btn(_layer, "그만", "그만두다", new Vector2(0f, -440f), new Vector2(360f, 60f), InventorySkin.Wood, Back);
        }

        // ── 봉하기와 끝 ─────────────────────────

        private string Missing()
        {
            for (int i = 0; i < _form.줄들.Length; i++)
            {
                var L = _form.줄들[i];
                if (L.IsChoice && _d.choice[i] < 0) return Num(i) + " 줄 — 처분을 아직 안 골랐다.";
                if (L.인물칸 && string.IsNullOrEmpty(_d.person[i])) return Num(i) + " 줄 — 사람을 아직 안 짚었다.";
                if (L.증거칸 && _d.evidence[i].Count == 0) return Num(i) + " 줄 — 증좌를 하나도 안 넣었다.";
            }
            return null;
        }

        private bool Filled(int i)
        {
            var L = _form.줄들[i];
            if (L.IsChoice) return _d.choice[i] >= 0;
            return (!L.인물칸 || !string.IsNullOrEmpty(_d.person[i])) && (!L.증거칸 || _d.evidence[i].Count > 0);
        }

        private void Seal()
        {
            if (_d.sealed_ || Missing() != null) return;
            if (Time.unscaledTime >= _confirmUntil) { _confirmUntil = Time.unscaledTime + 4f; Redraw(); return; }
            _confirmUntil = 0f;

            var r = Evaluate();
            _d.sealed_ = true;
            _justSealed = true;
            _result = r;

            var gs = GameState.Instance;
            if (gs != null)
            {
                gs.SetGapriHandled(r.gapri);
                gs.SetEnding(_form.사건, r.tag, r.total);
                gs.SetVerdict(_form.사건, r.verdict);
            }
            string line = (_form.이문록줄 ?? "").Replace("{끝}", TagName(r.tag)).Replace("{판결}", TagName(r.tag));
            if (!string.IsNullOrEmpty(line)) WorldNotice.Show("이문록", "이문록에 오르다 — " + line, 0.95f);

            _view = View.Result;
            Redraw();
            _closeAt = Time.unscaledTime + 14f;
        }

        public class Result
        {
            public int total, max;
            public bool[] pass;
            public int[] evidencePts;
            public Verdict verdict;
            public string tag;
            public bool gapri;
        }

        private Result _result;

        /// <summary>
        /// <b>점수를 매기고 끝을 가른다.</b>
        ///   · 줄마다: 사람이 맞으면 인물점수. 넣은 증좌는 그 줄의 표대로 더하고, 표에 없으면 오답감점.
        ///     증좌 합이 기준 이상이고 사람도 맞아야 그 줄이 <b>선다</b>.
        ///   · 핵심 줄의 사람을 틀렸거나, 가짜를 그대로 두는 처분(AcceptFake)이면 → 헛끝.
        ///   · 핵심 줄이 서지 못했으면(사람은 맞았으나 증좌가 성글다) → 엄히 다스리지 못한다: 반끝, 판결은 Mercy.
        ///   · 엄히 다스리는 처분(Truth): 모든 줄이 서고 합이 참기준 이상 → 참끝, 아니면 반끝.
        ///   · 헤아리는 처분(Mercy): 모든 줄이 서면 자비끝, 아니면 반끝.
        /// </summary>
        public Result Evaluate()
        {
            var r = new Result();
            int n = _form.줄들.Length;
            r.pass = new bool[n];
            r.evidencePts = new int[n];
            bool corePerson = true, coreEvidence = true, allPass = true;
            Verdict chosen = Verdict.None;

            for (int i = 0; i < n; i++)
            {
                var L = _form.줄들[i];
                if (L.IsChoice)
                {
                    int c = _d.choice[i];
                    if (c >= 0 && c < L.보기.Length)
                    {
                        r.total += L.보기[c].점수;
                        if (L.보기[c].판결 != Verdict.None) chosen = L.보기[c].판결;
                    }
                    int best = 0; foreach (var chc in L.보기) best = Mathf.Max(best, chc.점수);
                    r.max += best;
                    r.pass[i] = c >= 0;
                    continue;
                }

                bool personOk = !L.인물칸 || _d.person[i] == L.정답인물;
                if (L.인물칸) { r.max += L.인물점수; if (personOk) r.total += L.인물점수; }

                int ev = 0;
                if (L.증거칸)
                {
                    foreach (var key in _d.evidence[i]) ev += PointsFor(L, key);
                    var sorted = new List<int>();
                    foreach (var es in L.증거점수) sorted.Add(es.점수);
                    sorted.Sort(); sorted.Reverse();
                    for (int k = 0; k < Mathf.Min(L.증거최대, sorted.Count); k++) r.max += Mathf.Max(0, sorted[k]);
                    r.total += Mathf.Max(0, ev);
                }
                r.evidencePts[i] = ev;
                bool ok = personOk && (!L.증거칸 || ev >= L.증거기준);
                r.pass[i] = ok;
                if (!ok) allPass = false;
                if (L.핵심) { corePerson = personOk; coreEvidence = !L.증거칸 || ev >= L.증거기준; }
                if (L.갑리줄) r.gapri = ok;
            }

            if (!corePerson || chosen == Verdict.AcceptFake) { r.verdict = Verdict.AcceptFake; r.tag = "헛"; }
            else if (!coreEvidence) { r.verdict = Verdict.Mercy; r.tag = "반"; }
            else if (chosen == Verdict.Mercy) { r.verdict = Verdict.Mercy; r.tag = allPass ? "자비" : "반"; }
            else { r.verdict = Verdict.Truth; r.tag = allPass && r.total >= _form.참기준 ? "참" : "반"; }
            return r;
        }

        private int PointsFor(CaseReport.Line L, string key)
        {
            if (L.증거점수 != null)
                foreach (var es in L.증거점수) if (es != null && es.단서 == key) return es.점수;
            return -Mathf.Abs(_form.오답감점);
        }

        private void DrawResult()
        {
            var r = _result ?? Evaluate();
            Label(_layer, "표제", "장 계  ·  封", new Vector2(0f, 420f), new Vector2(W - 200f, 80f), Base + 16, TextAnchor.MiddleCenter, InventorySkin.Vermilion);
            Rule(_layer, 370f);
            string end = r.tag == "참" ? _form.참끝 : r.tag == "자비" ? _form.자비끝 : r.tag == "헛" ? _form.헛끝 : _form.반끝;
            var e = Label(_layer, "끝", end, new Vector2(0f, 250f), new Vector2(W - 300f, 200f), Base, TextAnchor.MiddleCenter, InventorySkin.Ink);
            UiText.Wrap(e, true);
            Label(_layer, "끝이름", "「" + TagName(r.tag) + "」   증좌 점수 " + r.total + " / " + r.max, new Vector2(0f, 110f), new Vector2(W - 300f, 50f), Base - 2, TextAnchor.MiddleCenter, InventorySkin.InkSoft);

            float y = 40f;
            for (int i = 0; i < _form.줄들.Length; i++)
            {
                var L = _form.줄들[i];
                if (L.IsChoice) continue;
                string mark = r.pass[i] ? "○ 섰다" : "× 서지 못했다";
                Label(_layer, "줄끝" + i, Num(i) + " 줄 — " + mark, new Vector2(0f, y), new Vector2(900f, 44f), Base - 6,
                      TextAnchor.MiddleCenter, r.pass[i] ? InventorySkin.Ink : InventorySkin.Vermilion);
                y -= 50f;
            }
            Btn(_layer, "닫기", "닫기", new Vector2(0f, -440f), new Vector2(360f, 60f), InventorySkin.Wood, Close);
        }

        private static string TagName(string tag)
        {
            switch (tag)
            {
                case "참": return "진상";
                case "반": return "절반의 진상";
                case "자비": return "헤아린 매";
                case "헛": return "옹고집전";
                default: return "아직";
            }
        }

        // ── 말 짓기 ──────────────────────────────

        private string Sentence(int i)
        {
            var L = _form.줄들[i];
            const string blank = "<color=#8a5a2a>＿＿＿＿</color>";
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(L.앞)) sb.Append(L.앞).Append(' ');
            if (L.IsChoice)
            {
                int c = _d.choice[i];
                sb.Append(c >= 0 && c < L.보기.Length ? "「" + L.보기[c].말 + "」" : blank);
            }
            if (L.인물칸)
            {
                var who = PersonById(_d.person[i]);
                sb.Append(who != null ? "<b>" + who.이름 + "</b>" : blank);
            }
            if (!string.IsNullOrEmpty(L.가운데)) sb.Append(L.가운데);
            if (L.증거칸)
            {
                sb.Append(' ');
                if (_d.evidence[i].Count == 0) sb.Append(blank);
                else
                {
                    var names = new List<string>();
                    var pool = Pool();
                    foreach (var k in _d.evidence[i]) { var e = pool.Find(x => x.key == k); names.Add(e != null ? TitleOf(e) : k); }
                    sb.Append("「").Append(string.Join("」·「", names.ToArray())).Append("」");
                }
            }
            if (!string.IsNullOrEmpty(L.뒤)) sb.Append(' ').Append(L.뒤);
            return sb.ToString();
        }

        private List<ClueEntry> Pool()
        {
            var list = new List<ClueEntry>();
            var j = Journal.Instance;
            if (j == null) return list;
            foreach (var c in j.GetClues(_form.사건))
                // 받아낸 증언(말한 이가 있는 줄)도 넣는다 — 옛 저장에는 들이밀 수 없음으로 적혀 있다
                if (c != null && (c.presentable || !string.IsNullOrEmpty(c.speaker)) && !string.IsNullOrEmpty(c.key)) list.Add(c);
            return list;
        }

        private bool Known(CaseReport.Choice c)
        {
            if (c == null) return false;
            if (string.IsNullOrWhiteSpace(c.필요단서)) return true;
            var j = Journal.Instance;
            if (j == null) return false;
            foreach (var k in c.필요단서.Split(','))
                if (!string.IsNullOrWhiteSpace(k) && j.HasClue(_form.사건, k.Trim())) return true;
            return false;
        }

        private CaseReport.Person PersonById(string id)
        {
            if (string.IsNullOrEmpty(id) || _form.인물들 == null) return null;
            foreach (var p in _form.인물들) if (p != null && p.id == id) return p;
            return null;
        }

        private static Texture FaceOf(CaseReport.Person p)
        {
            if (p == null || string.IsNullOrEmpty(p.얼굴)) return null;
            var book = FaceBook.Book;
            var e = book != null ? book.FindByName(p.얼굴) : null;
            return e != null ? FaceCards.Card(book, e) : null;
        }

        private Texture PictureOfClue(string key)
        {
            var j = Journal.Instance;
            if (j == null || string.IsNullOrEmpty(key)) return null;
            var img = j.GetClueImage(_form.사건, key);
            if (img != null) return img;
            var d = j.GetDocument(_form.사건, key);
            return d != null ? d.page : null;
        }

        private static readonly System.Text.RegularExpressions.Regex KeyTag =
            new System.Text.RegularExpressions.Regex(@"^\s*\[[A-Z]\d+\]\s*");

        private static string Clean(string t) { return string.IsNullOrEmpty(t) ? "" : KeyTag.Replace(t, ""); }

        private static string TitleOf(ClueEntry e)
        {
            if (e == null) return "";
            if (!string.IsNullOrWhiteSpace(e.title)) return e.title.Trim();
            string t = Clean(e.text);
            int cut = t.IndexOf('—');
            if (cut > 0 && cut < 24) return t.Substring(0, cut).Trim();
            return t.Length > 16 ? t.Substring(0, 16) + "…" : t;
        }

        private void Say(string line) { _note = line; _noteUntil = Time.unscaledTime + 3f; }

        private static string Num(int i)
        {
            string[] n = { "하나", "둘", "셋", "넷", "다섯", "여섯", "일곱" };
            return i < n.Length ? n[i] : (i + 1).ToString();
        }

        // ── 잔손 ─────────────────────────────────

        private static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Skin(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-inset, -inset);
            rt.offsetMax = new Vector2(inset, inset);
        }

        private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private void Rule(Transform parent, float y)
        {
            var r = Skin(parent, "구분선", _skin.Wood_, InventorySkin.Wood);
            Place(r, new Vector2(0f, y), new Vector2(W - 160f, 4f));
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string content, Vector2 pos,
                                             Vector2 size, float fontSize, TextAnchor anchor, Color color)
        {
            var t = UiText.New(name, content, pos, size, parent, fontSize, anchor, color);
            t.raycastTarget = false;
            return t;
        }

        private UnityEngine.UI.Button Btn(Transform parent, string name, string text, Vector2 pos, Vector2 size, Color color,
                                          UnityEngine.Events.UnityAction onClick)
        {
            var cell = Skin(parent, name, _skin.Wood_, color);
            Place(cell, pos, size);
            var img = cell.GetComponent<Image>();
            img.raycastTarget = true;
            var b = cell.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.15f, 1.1f, 1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            b.colors = colors;
            b.onClick.AddListener(onClick);
            if (!string.IsNullOrEmpty(text))
            {
                var t = Label(cell, "라벨", text, Vector2.zero, size - new Vector2(18f, 8f), Base - 6, TextAnchor.MiddleCenter, InventorySkin.Hanji);
                UiText.Wrap(t, true);
            }
            return b;
        }

        private static RawImage Picture(Transform parent, Texture tex, Vector2 pos, Vector2 box)
        {
            var ri = new GameObject("그림", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            ri.transform.SetParent(parent, false);
            ri.raycastTarget = false;
            ri.texture = tex;
            Vector2 size = box;
            if (tex != null && tex.width > 0 && tex.height > 0)
            {
                float a = (float)tex.width / tex.height;
                size = box.x / box.y > a ? new Vector2(box.y * a, box.y) : new Vector2(box.x, box.x / a);
            }
            Place(ri.rectTransform, pos, size);
            return ri;
        }
    }
}
