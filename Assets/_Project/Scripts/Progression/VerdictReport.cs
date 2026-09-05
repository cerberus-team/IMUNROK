using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>장계를 쓰고 봉한다</b> — 사건 하나가 끝나는 공통 절차.
    ///
    /// 어느 사건이든 끝은 같다. 어사는 <b>서안 앞에 앉아 장계를 쓰고, 마패로 봉하고,
    /// 이문록에 한 줄을 올린다.</b> 사건마다 다른 것은 그 글에 채워 넣는 말뿐이다
    /// (<see cref="CaseReport"/>). 그래서 이것은 사건 팀이 아니라 <b>공통 파트</b>가 짓는다.
    ///
    /// <b>왜 이 절차가 필요한가.</b> 여태 판결은 코드 한 줄이었다
    /// (<c>GameState.SetVerdict(...)</c>). 그러면 몇 시간을 조사하고도 마지막에
    /// 단추 하나를 누르는 것으로 끝난다 — 조사한 것과 판결한 것 사이에 아무 매듭이 없다.
    /// 빈칸을 채우게 하면 그 매듭이 생긴다. <b>안 캐낸 말은 보기에 뜨지도 않으므로</b>,
    /// 흉터를 안 재 본 사람은 흉터를 증좌로 쓸 수가 없다.
    ///
    /// <b>마패로 봉한다.</b> 다 채워도 마패를 손에 들지 않으면 봉하지 못한다. 마패는
    /// 여태 도구벨트에 이름만 올라 있던 물건인데, 이 대목에서 <b>제 일</b>이 생긴다 —
    /// 어사의 글이 어사의 글임을 증명하는 것이 그 물건의 본디 쓰임이다.
    ///
    /// 판결은 두 갈래로 갈린다.
    ///   · <b>첫째 빈칸(누구인가)을 틀리면</b> 무엇을 골랐든 <see cref="Verdict.AcceptFake"/> 다.
    ///     사람을 잘못 짚은 판결은 그 아래가 아무리 정연해도 가짜를 인정한 것이다.
    ///   · 맞혔으면 <b>처분 빈칸</b>이 정한다(엄히 다스리면 Truth, 헤아리면 Mercy).
    ///     증좌를 얼마나 짚었는지는 판결이 아니라 <b>뒷일</b>에 남는다 — 성글게 쓴
    ///     장계는 판결은 서되 뒷말이 따른다.
    ///
    /// 씬에 미리 둘 필요 없다. <see cref="ReportDesk"/> 가 부르면 스스로 선다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class VerdictReport : MonoBehaviour
    {
        // <b>종이 크기는 글에 맞춰 잰 값이다.</b> 처음엔 1180×1100 에 글자 30 으로 두었더니
        // 줄이 종이 밖으로 삐져나가 "…종으로 있" 에서 잘렸다. 한 줄이 마흔 자 남짓이니
        // 글자 25 × 마흔이 대략 1000 칸, 여백까지 1240 이 든다. 줄 높이는 두 줄로
        // 접혀도 안 겹치게 124 로 둔다.
        private const float W = 1240f;          // 종이 너비(캔버스 칸)
        private const float H = 1240f;          // 종이 높이
        private const float RowH = 124f;
        private const int Base = 25;            // 글자 크기

        private static VerdictReport _instance;

        /// <summary>지금 장계를 펴 놓고 있나. 다른 조작이 이걸 보고 물러난다.</summary>
        public static bool IsOpen => _instance != null && _instance._open;

        private CaseReport _form;
        private System.Action _onClosed;
        private Font _font;
        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _paper;
        private RectTransform _wordBox;
        private Text _title, _head, _tail, _hint;
        private readonly List<Text> _rowLabels = new List<Text>();
        private readonly List<RectTransform> _rows = new List<RectTransform>();
        private int[] _picked;                  // 빈칸마다 고른 보기 번호. -1 이면 아직
        private int _openRow = -1;
        private bool _open;
        private Button _sealBtn;
        private Text _sealLabel;
        private bool _sealed;

        /// <summary>장계를 편다. 다 끝나면 <paramref name="onClosed"/> 가 불린다.</summary>
        public static void Open(CaseReport form, System.Action onClosed = null)
        {
            if (form == null) { Debug.LogWarning("[장계] 쓸 글이 없습니다."); return; }
            if (_instance == null)
            {
                var go = new GameObject("장계", typeof(Canvas));
                _instance = go.AddComponent<VerdictReport>();
            }
            _instance.Show(form, onClosed);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _font = UiFont.Resolve(null);

            _canvas = GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var anchor = gameObject.AddComponent<WorldHudAnchor>();
            anchor.Configure(WorldHudAnchor.Placement.Front);
            // 서안에 앉아 내려다보는 글이다. 팔 길이보다 가깝고, 눈보다 아래.
            anchor.SetDistance(0.58f, -0.16f);

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            Build();
            gameObject.SetActive(true);
        }

        // ── 짓기 ─────────────────────────────────

        private void Build()
        {
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(W, H);
            rt.localScale = Vector3.one * 0.00072f;

            _paper = Panel("종이", Vector2.zero, new Vector2(W, H), UiLook.With(UiLook.Paper, 0.98f), rt);
            _title = Label("표제", "狀 啓", new Vector2(0f, 540f), new Vector2(W - 80f, 80f), _paper, Base + 20);
            _title.color = UiLook.Ink;
            _head = Label("머리말", "", new Vector2(0f, 452f), new Vector2(W - 120f, 84f), _paper, Base);
            _tail = Label("맺음말", "", new Vector2(0f, -318f), new Vector2(W - 140f, 130f), _paper, Base - 1);
            _hint = Label("안내", "", new Vector2(0f, -428f), new Vector2(W - 120f, 70f), _paper, Base - 4);
            _hint.color = UiLook.Lit(UiLook.InkSoft, 0.15f);

            // 마패로 봉하는 자리
            var seal = Panel("봉함", new Vector2(0f, -518f), new Vector2(560f, 92f), UiLook.With(UiLook.Deep(UiLook.Seal, 0.45f), 0.95f), _paper);
            _sealBtn = seal.gameObject.AddComponent<Button>();
            _sealBtn.targetGraphic = seal.GetComponent<Image>();
            _sealBtn.onClick.AddListener(Seal);
            _sealLabel = Label("라벨", "마패로 봉하다", Vector2.zero, new Vector2(540f, 86f), seal, Base);
            _sealLabel.color = UiLook.SealText;

            _wordBox = Panel("말고르기", new Vector2(0f, -700f), new Vector2(W, 40f),
                             UiLook.With(UiLook.Ink, 0.96f), rt);
            _wordBox.gameObject.SetActive(false);
        }

        private void Show(CaseReport form, System.Action onClosed)
        {
            _form = form; _onClosed = onClosed; _sealed = false; _openRow = -1;
            _picked = new int[form.빈칸 != null ? form.빈칸.Length : 0];
            for (int i = 0; i < _picked.Length; i++) _picked[i] = -1;

            _head.text = form.머리말;
            _tail.text = form.맺음말;

            foreach (var r in _rows) if (r != null) Destroy(r.gameObject);
            _rows.Clear(); _rowLabels.Clear();

            float top = 340f;
            for (int i = 0; i < _picked.Length; i++)
            {
                int at = i;
                var row = Panel("줄" + i, new Vector2(0f, top - i * RowH), new Vector2(W - 100f, RowH - 14f),
                                UiLook.With(UiLook.Paper, 0.06f), _paper);
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = row.GetComponent<Image>();
                btn.onClick.AddListener(() => PickRow(at));
                // 글상자를 살짝 오른쪽으로 밀어 왼쪽에 여백을 준다 — 번호가 테두리에 붙으면 안 읽힌다
                var lab = Label("글", "", new Vector2(14f, 0f), new Vector2(W - 150f, RowH - 20f), row, Base);
                lab.alignment = TextAnchor.MiddleLeft;
                lab.color = UiLook.Ink;
                _rows.Add(row); _rowLabels.Add(lab);
            }

            Redraw();
            _open = true;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
        }

        // ── 채우기 ───────────────────────────────

        private void PickRow(int row)
        {
            if (_sealed) return;
            _openRow = _openRow == row ? -1 : row;
            Redraw();
        }

        private void PickWord(int row, int choice)
        {
            if (_sealed) return;
            _picked[row] = choice;
            _openRow = -1;
            Redraw();
        }

        /// <summary>
        /// 이 보기를 쓸 수 있나 — <b>캐낸 말만 목록에 오른다</b>.
        /// 조사와 판결을 잇는 매듭이 바로 이 한 줄이다.
        /// </summary>
        private bool Known(CaseReport.Choice c)
        {
            if (c == null) return false;
            if (string.IsNullOrEmpty(c.필요단서)) return true;
            return Journal.Instance != null && Journal.Instance.HasClue(_form.사건, c.필요단서);
        }

        private void Redraw()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var b = _form.빈칸[i];
                string word = _picked[i] >= 0 && _picked[i] < b.보기.Length
                            ? "「" + b.보기[_picked[i]].말 + "」"
                            : "<color=#8a5a2a>____________</color>";
                string body = (string.IsNullOrEmpty(b.앞) ? "" : b.앞 + " ") + word
                            + (string.IsNullOrEmpty(b.뒤) ? "" : " " + b.뒤);
                _rowLabels[i].text = Emphasis.Rich(Num(i) + ".  " + body, Emphasis.OnPaper);
                var img = _rows[i].GetComponent<Image>();
                img.color = i == _openRow ? UiLook.With(UiLook.Gold, 0.22f)
                          : (_picked[i] >= 0 ? UiLook.With(UiLook.Ink, 0.05f) : UiLook.With(UiLook.Gold, 0.10f));
            }

            bool all = true;
            for (int i = 0; i < _picked.Length; i++) if (_picked[i] < 0) all = false;

            bool holdsSeal = ToolbeltHud.SelectedToolId == "mapae";
            _sealBtn.interactable = all && holdsSeal && !_sealed;
            _sealLabel.text = _sealed ? "봉하였다"
                            : !all ? "아직 비워 둔 데가 있다"
                            : !holdsSeal ? "마패를 손에 들어야 봉한다"
                            : "마패로 봉하다";

            _hint.text = _openRow >= 0 ? _form.빈칸[_openRow].물음
                       : (_sealed ? "" : "줄을 눌러 빈칸을 채우시오.");

            Words();
        }

        private void Words()
        {
            for (int i = _wordBox.childCount - 1; i >= 0; i--) Destroy(_wordBox.GetChild(i).gameObject);
            if (_openRow < 0 || _sealed) { _wordBox.gameObject.SetActive(false); return; }

            var b = _form.빈칸[_openRow];
            var usable = new List<int>();
            for (int i = 0; i < b.보기.Length; i++) if (Known(b.보기[i])) usable.Add(i);

            float h = 30f + usable.Count * 78f;
            _wordBox.sizeDelta = new Vector2(W, h);
            _wordBox.anchoredPosition = new Vector2(0f, -(H * 0.5f) - 30f - h * 0.5f);
            _wordBox.gameObject.SetActive(true);

            if (usable.Count == 0)
            {
                var none = Label("없음", "여기에 쓸 말을 아직 알지 못한다.", Vector2.zero,
                                 new Vector2(W - 60f, 70f), _wordBox, Base - 6);
                none.color = UiLook.Dim;
                return;
            }

            float y = h * 0.5f - 54f;
            for (int k = 0; k < usable.Count; k++)
            {
                int idx = usable[k];
                var row = Panel("말" + k, new Vector2(0f, y - k * 78f), new Vector2(W - 60f, 66f),
                                UiLook.With(UiLook.Paper, 0.08f), _wordBox);
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = row.GetComponent<Image>();
                int at = _openRow;
                btn.onClick.AddListener(() => PickWord(at, idx));
                var lab = Label("글", b.보기[idx].말, Vector2.zero, new Vector2(W - 90f, 60f), row, Base - 4);
                lab.color = UiLook.Text;
            }
        }

        // ── 봉하기 ───────────────────────────────

        private void Seal()
        {
            if (_sealed) return;
            _sealed = true;

            // ① 사람을 짚었나. 첫째 빈칸이 그것이다.
            bool whoRight = Right(0);
            int rightCount = 0, total = _picked.Length;
            for (int i = 0; i < total; i++) if (Right(i)) rightCount++;

            // ② 판결.
            //
            //   · 사람을 틀리면 무엇을 골랐든 가짜를 인정한 것이다.
            //   · 맞혔으면 처분 빈칸이 결을 정한다.
            //   · 다만 <b>쐐기 없이는 엄히 못 다스린다</b>. 곁증좌만 대고 Truth 를 고르면
            //     Mercy 로 내려앉는다 — 조선의 재판에서도 증거 없는 엄형은 못 했고,
            //     게임으로도 그래야 증좌를 캐는 일이 값을 갖는다. 안 그러면 이름만
            //     맞히면 되는 객관식과 다를 바 없다.
            bool wedge = false;
            for (int i = 0; i < total; i++)
            {
                var c = Chosen(i);
                if (c != null && c.쐐기) wedge = true;
            }

            var verdict = Verdict.Truth;
            if (!whoRight) verdict = Verdict.AcceptFake;
            else
            {
                for (int i = 0; i < total; i++)
                {
                    var c = Chosen(i);
                    if (c != null && c.판결 != Verdict.None) verdict = c.판결;
                }
                if (verdict == Verdict.Truth && !wedge) verdict = Verdict.Mercy;
            }

            if (GameState.Instance != null) GameState.Instance.SetVerdict(_form.사건, verdict);

            // ③ 뒷일. 증좌를 얼마나 짚었는지는 판결이 아니라 여기에 남는다.
            // 뒷일도 쐐기로 갈린다. 다 맞혀도 쐐기가 없으면 <b>판결은 서되 뒷말이 남는다</b>.
            string after = !whoRight ? _form.헛끝
                         : (wedge && rightCount >= total ? _form.참끝 : _form.반끝);

            _title.text = "狀 啓  (封)";
            _hint.text = "";
            _tail.text = string.IsNullOrEmpty(after) ? _form.맺음말 : after;
            Redraw();

            // ④ 이문록에 한 줄이 오른다 — 이 게임의 이름이 곧 그 책이다.
            string line = (_form.이문록줄 ?? "").Replace("{판결}", Korean(verdict));
            if (!string.IsNullOrEmpty(line))
                // 종이 <b>위로</b> 띄운다. 한가운데 두었더니 넷째 줄을 덮어,
                // 방금 제가 쓴 글을 못 읽은 채 사건이 끝났다.
                WorldNotice.Show("이문록", "이문록에 오르다 — " + line, 0.95f);

            Invoke(nameof(Close), 5.5f);
        }

        private CaseReport.Choice Chosen(int row)
        {
            if (row < 0 || row >= _picked.Length) return null;
            var b = _form.빈칸[row];
            int at = _picked[row];
            return (at >= 0 && at < b.보기.Length) ? b.보기[at] : null;
        }

        private bool Right(int row)
        {
            var c = Chosen(row);
            return c != null && c.맞음;
        }

        private static string Korean(Verdict v)
        {
            if (v == Verdict.Truth) return "사실대로 아뢰다";
            if (v == Verdict.Mercy) return "사정을 헤아리다";
            if (v == Verdict.AcceptFake) return "가짜를 인정하다";
            return "아직";
        }

        private void Close()
        {
            _open = false;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            var cb = _onClosed; _onClosed = null;
            if (cb != null) cb();
        }

        // ── 잔손 ─────────────────────────────────

        private RectTransform Panel(string name, Vector2 pos, Vector2 size, Color color, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        private Text Label(string name, string content, Vector2 pos, Vector2 size, Transform parent, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.text = content;
            t.supportRichText = true;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = UiLook.Ink;
            t.raycastTarget = false;
            return t;
        }

        private static string Num(int i)
        {
            string[] n = { "하나", "둘", "셋", "넷", "다섯", "여섯" };
            return i < n.Length ? n[i] : (i + 1).ToString();
        }

        // 마패를 들었는지는 매 프레임 바뀐다 — 봉함 단추가 그때그때 살아나야 한다.
        private float _tick;
        private void Update()
        {
            if (!_open || _sealed) return;
            _tick += Time.deltaTime;
            if (_tick < 0.25f) return;
            _tick = 0f;
            bool all = true;
            for (int i = 0; i < _picked.Length; i++) if (_picked[i] < 0) all = false;
            bool holds = ToolbeltHud.SelectedToolId == "mapae";
            if (_sealBtn.interactable != (all && holds)) Redraw();
        }
    }
}
