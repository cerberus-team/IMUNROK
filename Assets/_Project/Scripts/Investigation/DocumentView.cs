using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 문서 한 장을 <b>손에 쥔다</b>.
    ///
    /// 처음에는 방을 까맣게 덮고 그 위에 종이를 띄웠다. 읽기는 편했으나 조사하던 사람이
    /// 갑자기 극장에 앉은 꼴이 되었다 — 방도 없고 몸도 없고 종이만 떠 있었다. 그래서
    /// 배경을 걷어냈다. 이제 종이는 눈앞 한 뼘 거리에서 <b>손에 들려</b> 있고, 방은 그
    /// 둘레로 그대로 보인다. 고개를 돌리면 종이도 따라 돌지만 조금 늦게 따라온다 —
    /// 손이 몸에 매달려 있기 때문이다.
    ///
    /// <b>종이 본문 위로는 아무것도 덧그리지 않는다.</b> 읽어야 할 잔글씨는 이미 문서에
    /// 그려져 있다 — 스무 해와 다른 두 줄의 필적, 마지막 두 줄에만 눌린 수결, '辛未年 死亡'.
    /// 한자로 적힌 본문 위에 한글 한 줄을 얹으면 그 순간 문서가 아니라 자막이 된다.
    /// 그래서 <see cref="MagnifierLens"/>로 들여다보아 다 읽고 나면, 읽어낸 바는
    /// 종이 <b>밖</b>(아래)에 적힌다. 단서도 그때 수첩에 오른다.
    ///
    /// 다만 <b>이름</b>만은 종이에 있다. 제목을 종이 위 허공에 띄워 두면 그것이
    /// 이름표가 되어 문서가 전시물처럼 보인다. 그래서 왼쪽 위 여백에 <b>표제 쪽지</b>를
    /// 붙인다 — 옛사람이 문서를 갈무리하며 겉에 붙여 두던 제첨(題簽)이다. 쪽지는 제
    /// 자리만 덮으므로 본문을 가리지 않고, 종이의 자식이라 함께 돌고 함께 뒤집힌다.
    ///
    /// 씬에 미리 둘 필요 없다 — 처음 부를 때 스스로 만든다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class DocumentView : MonoBehaviour
    {
        [SerializeField] private Font _font;
        [SerializeField] private int _fontSize = 26;
        [Tooltip("펼친 종이의 긴 변(월드 캔버스 단위 = mm). 300이면 한 뼘 남짓")]
        [SerializeField] private float _pageSpan = 300f;
        [Tooltip("종이를 든 거리(m). 팔을 굽혀 든 만큼 — 돋보기(0.4m)보다 멀어야 그 너머로 보인다")]
        [SerializeField] private float _holdDistance = 0.6f;
        [Tooltip("집은 자리에서 이만큼(m) 멀어지면 <b>도로 내려놓는다</b>. " +
                 "한 방 안에서 몇 발짝 옮기는 것으로는 안 놓이고, 방을 나서면 놓인다. 0이면 안 놓는다")]
        [SerializeField] private float _dropDistance = 3.5f;
        [Tooltip("눈높이보다 이만큼 아래(m). 종이는 내려다보는 것이다")]
        [SerializeField] private float _holdDrop = -0.06f;
        [SerializeField] private Color _paper = Color.white;
        private Color _textColor { get { return UiLook.Text; } }
        private Color _tabColor { get { return UiLook.With(UiLook.Deep(UiLook.Seal, 0.55f), 0.90f); } }

        [Header("옮긴 글")]
        [Tooltip("읽어낸 것을 <b>종이 위에</b> 얹는다 — 한자 위에 우리말이 배어 나오듯. " +
                 "끄면 예전처럼 종이 밖(아래)에만 적힌다")]
        [SerializeField] private bool _translateOnPage = true;
        [Tooltip("옮긴 글 밑에 까는 바탕의 짙기. <b>0이 기본이다</b> — 켜를 깔면 " +
                 "밑의 한자가 통째로 가려져, 읽어낸 것이 아니라 <b>덧붙인 쪽지</b>가 된다")]
        [Range(0f, 1f)] [SerializeField] private float _transPlateAlpha = 0f;
        [Tooltip("옮긴 글의 짙기. 진하면 종이에 인쇄된 글로 보이고, 너무 옅으면 안 읽힌다. " +
                 "0.6 안팎이면 <b>밑의 글자가 비쳐 보이면서도</b> 읽힌다")]
        [Range(0.2f, 1f)] [SerializeField] private float _transAlpha = 0.62f;

        /// <summary>
        /// <b>설명이 뜰 때 종이가 비켜서는 높이(m).</b>
        ///
        /// 여태 −0.34 였다 — 아래로 내려 두라는 뜻이다. 자막이 <b>눈앞 1.3m 허공에</b>
        /// 서 있던 시절에는 그것이 맞았다: 종이가 0.6m 로 더 앞이라 자막 한가운데를
        /// 통째로 덮었고, 그러니 종이가 내려가야 했다.
        ///
        /// 자막을 화면에 붙이면서 그 셈이 <b>뒤집혔다</b>. 화면판 자막은 아래 삼분의
        /// 일에 눕는다 — 종이를 내리면 바로 그 위로 내려앉는다. 재 보니 종이가
        /// 뷰포트 0.01 까지 밀려 화면 밑으로 반쯤 빠져 있었다.
        ///
        /// 비켜설 쪽이 아래에서 <b>위</b>로 바뀐 것이다. 종이(화면 높이의 43%)가
        /// 자막 바(위 끝 0.34) 위에 앉으려면 가운데에서 조금 올라와야 한다.
        /// </summary>
        private const float LowDrop = 0.06f;

        private static DocumentView _instance;

        private Canvas _canvas;
        private GameObject _chrome;       // 종이 아닌 것들(제목·요약·안내·내려놓기)
        private CanvasGroup _group;
        private WorldHudAnchor _anchor;
        private RectTransform _hand;      // 종이를 쥔 손 — 여기가 흔들린다
        private RawImage _page;
        private RectTransform _pageRt;

        /// <summary>그림 없는 문서를 적을 빈 종이의 빛깔. 한지.</summary>
        private static Color _blankPaper { get { return UiLook.With(UiLook.PaperDim, 0.99f); } }
        private Image _edge;
        private Image _backdrop;     // 수첩에서 볼 때 뒤를 덮는 어둠
        private Image _backFace;     // 종이 뒷면 — 뒤집었을 때 글씨가 비치지 않게
        private RawImage _backArt;   // 뒤에 새겨진 것(있으면)
        private bool _hasBackArt;
        private RawImage _stageImage;   // 종이가 아닌 것 — 무대에서 찍어 온 그림
        private static bool _onStage;   // 지금 무대에 물건이 올라 있는가
        private Image _transPlate;   // 옮긴 글이 앉는 바탕 — 종이 위에 한 켜
        private Text _trans;         // 옮긴 글 — 한자 위에 얹히는 우리말
        private Image _slip;         // 종이에 붙은 표제 쪽지
        private Text _slipText;      // 그 위에 세로로 적힌 이름
        private Text _body;
        private Text _fine;
        private Text _hint;
        private RectTransform _closeRt;   // 내려놓기 단추 — 익히는 동안엔 감춘다

        // ── 등불에 비추기 ──
        // 돋보기가 <b>이미 그려진 것을 알아보는</b> 도구라면, 등불은 <b>없던 것을
        // 불러내는</b> 도구다. 배접 속에 숨긴 글, 밀랍으로 눌러 쓴 자국, 물에 지운 먹.
        // 그래서 여기서는 종이에 손을 대도 된다 — 잔글씨와 달리 이 글자는 원래
        // 종이에 그려져 있지 않았다.
        private float _lit01;         // 종이가 달아오른 정도(0~1)
        private RawImage _pageLit;    // 빛에 드러난 종이 면(있으면 겹쳐 배어 나온다)
        private Text _lit;            // 빛에 드러난 것 — 종이 아래에

        private System.Action _onRead;
        private string _finePrint = "";
        private bool _readDone;
        private float _readProgress;
        private float _sinceRead;

        private System.Action _onLit;
        private string _litPrint = "";
        private string _litGlyphs = "";   // 획만 잡혔을 때 보여 줄 것(한자 그대로)
        private Texture _litTexture;
        private bool _litDone;
        private float _litProgress;
        private float _sinceLit;
        private Vector2 _tilt;            // 손목으로 종이를 기울인 정도
        private Vector2 _spin;            // 끌어서 돌린 정도(가로·세로)
        private bool _dragging;
        private bool _spinArmed;          // 집은 손을 한 번 뗐나 — 떼기 전에는 안 돌린다
        private float _dragRun;           // 누른 뒤 움직인 거리(픽셀). 문턱을 넘어야 돌린다

        /// <summary>이만큼(픽셀) 끌어야 돌리려는 손으로 친다. 겨누다 미끄러진 것과 가른다.</summary>
        private const float DragThreshold = 26f;

        /// <summary>지금 문서를 쥐고 있나. 다른 UI가 참고한다(도구벨트 숨김 등).</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>
        /// 문서를 손에 쥔다.
        /// </summary>
        /// <param name="page">종이 면(문서 텍스처)</param>
        /// <param name="title">무슨 문서인가</param>
        /// <param name="body">맨눈으로도 아는 것</param>
        /// <param name="finePrint">돋보기로 들여다봐야 알아지는 것. 다 읽으면 종이 아래에 뜬다</param>
        /// <param name="onRead">다 읽었을 때 한 번</param>
        /// <param name="litPage">등불에 비추면 배어 나오는 종이 면(선택). 원래 면 위로 겹쳐 든다</param>
        /// <param name="litPrint">등불에 비춰야 드러나는 것. 다 드러나면 종이 아래에 뜬다</param>
        /// <param name="onLit">다 드러났을 때 한 번</param>
        public static void Show(Texture page, string title, string body,
                                string finePrint = null, System.Action onRead = null, bool dim = false,
                                Texture litPage = null, string litPrint = null, System.Action onLit = null,
                                string litGlyphs = null, Texture backPage = null)
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<DocumentView>();
                if (_instance == null)
                {
                    var go = new GameObject("문서보기_판", typeof(Canvas));
                    go.AddComponent<WorldHudAnchor>().Configure(WorldHudAnchor.Placement.Front);
                    _instance = go.AddComponent<DocumentView>();
                }
            }
            // 읽는 자리는 하나뿐이다. 수첩이나 개요가 펴져 있으면 그쪽이 닫힌다.
            ReadingFocus.Claim(ReadingFocus.Panel.Document, Hide);
            _instance.ShowInternal(page, title, body, finePrint, onRead, dim, litPage, litPrint, onLit, litGlyphs, backPage);
        }

        /// <summary>
        /// 내려놓기를 <b>막는다</b>. 도구를 익히는 동안 쥐여 준 예시 증거에 쓴다 —
        /// 그것은 내가 집은 종이가 아니라 <b>받은 종이</b>라, 내려놓을 것이 아니다.
        /// 익히기가 끝나면 이쪽에서 거둔다.
        /// </summary>
        public static void SetCanPutDown(bool on)
        {
            _canPutDown = on;
            if (_instance != null && _instance._closeRt != null)
                _instance._closeRt.gameObject.SetActive(on);
        }

        private static bool _canPutDown = true;

        /// <summary>이 종이를 집은 자리(플레이어의 눈). 여기서 멀어지면 도로 내려놓는다.</summary>
        private Vector3 _openedAt;

        /// <summary>
        /// 이 종이를 <b>내려놓았을 때</b> 한 번 부를 것.
        ///
        /// 종이를 펴려고 걷어 둔 창이 있으면 도로 세워야 하기 때문이다. 고르는 창을
        /// 걷지 않고 그 위에 종이를 띄웠더니 둘이 겹쳐, 봉서를 폈는데도 창에 가려
        /// 아무것도 안 보였다. 걷고 펴되, 다 읽으면 <b>있던 자리로 돌려놓는다</b> —
        /// 알아보러 들어갔다가 고르는 자리를 잃으면 그것은 알아본 값이 아니다.
        /// </summary>
        public static System.Action OnPutDown;

        public static void Hide()
        {
            EvidenceStage.Clear();   // 안 보는 무대를 그릴 까닭이 없다
            _onStage = false;
            ReadingFocus.Release(ReadingFocus.Panel.Document);
            var back = OnPutDown;
            OnPutDown = null;
            if (back != null) back();
            if (_instance != null && _instance._anchor != null)
            {
                _instance._anchor.Frozen = false;   // 세워 둔 채로 걷어 버리면 다음 종이도 못 박힌다
                _instance._anchor.SetDistance(_instance._holdDistance, _instance._holdDrop);
            }
            if (_instance == null) return;
            _instance.SetVisible(false);
            _instance._onRead = null;
            _instance._onLit = null;
            IsOpen = false;
        }

        /// <summary>
        /// 이 광선이 쥐고 있는 종이를 지나가나. 돋보기가 종이를 짚었는지 볼 때 쓴다.
        /// 종이는 콜라이더가 없는 UI라 물리로는 못 짚는다 — 그래서 면과 광선을 직접 푼다.
        /// </summary>
        public static bool RayHitsPage(Ray ray)
        {
            if (_instance == null || !IsOpen || _instance._pageRt == null) return false;
            var rt = _instance._pageRt;

            Vector3 n = rt.forward;
            float denom = Vector3.Dot(n, ray.direction);
            if (Mathf.Abs(denom) < 1e-5f) return false;
            float t = Vector3.Dot(n, rt.position - ray.origin) / denom;
            if (t < 0f) return false;

            Vector3 local = rt.InverseTransformPoint(ray.GetPoint(t));
            Vector2 half = rt.rect.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        }

        /// <summary>이 캔버스가 '쥐고 있는 종이'인가. 돋보기가 찍을 때 이것만 남긴다.</summary>
        public static bool IsPageCanvas(Canvas c)
            => _instance != null && c != null && c == _instance._canvas;

        /// <summary>
        /// 종이 둘레의 글자판을 잠깐 감춘다 — 돋보기가 찍는 그 한 프레임 동안.
        /// 돋보기는 종이를 크게 보라고 든 것이지 제목을 크게 보라고 든 것이 아니다.
        /// </summary>
        public static void SetChromeVisible(bool on)
        {
            if (_instance == null || _instance._chrome == null) return;
            if (_instance._chrome.activeSelf != on) _instance._chrome.SetActive(on);
        }

        /// <summary>
        /// <b>옮긴 글이 글자 위로 배어 나온다</b>(0~1).
        ///
        /// 다 읽어야 나타나는 것이 아니라 <b>읽는 만큼</b> 짙어진다. 그래야 지금 하고 있는
        /// 일이 눈에 보인다 — 유리를 대고 있으면 뜻이 배어 나오고, 떼면 도로 옅어진다.
        /// </summary>
        private void ShowTranslation(float progress)
        {
            if (!_translateOnPage || _trans == null || string.IsNullOrEmpty(_finePrint)) return;

            float k = Mathf.Clamp01(_readDone ? 1f : progress);
            var go = _transPlate.gameObject;
            if (!go.activeSelf) go.SetActive(true);
            if (_trans.text.Length == 0) _trans.text = Emphasis.Rich(_finePrint, Emphasis.OnPaper);

            _transPlate.color = UiLook.With(UiLook.Paper, _transPlateAlpha * k);
            var c = _trans.color;
            _trans.color = new Color(c.r, c.g, c.b, _transAlpha * k);
        }

        /// <summary>돋보기가 종이를 들여다보는 중. 진행도가 1을 넘으면 다 읽은 것이다.</summary>
        public static void Reading(float progress)
        {
            if (_instance == null || !IsOpen) return;
            _instance._readProgress = progress;
            _instance._sinceRead = 0f;
            _instance.ShowTranslation(progress);
            if (progress < 1f || _instance._readDone) return;

            _instance._readDone = true;
            _instance.ShowTranslation(1f);
            // 종이 위에 옮겨 놓았으면 아래에 또 적지 않는다 — 같은 말이 두 군데 있으면
            // 눈이 어디를 봐야 할지 모른다.
            if (!string.IsNullOrEmpty(_instance._finePrint) && !_instance._translateOnPage)
            {
                _instance._fine.text = Emphasis.Rich(_instance._finePrint, Emphasis.OnDark);
                _instance._fine.gameObject.SetActive(true);
            }
            var cb = _instance._onRead;
            if (cb != null) cb();
            ToolPractice.Done("magnify");
        }

        /// <summary>
        /// 등불이 종이를 비추는 중. 진행도가 1을 넘으면 숨은 것이 다 배어 나온다.
        ///
        /// <see cref="Reading"/> 와 같은 꼴로 두었다. 도구가 둘인데 쓰는 법이 서로
        /// 다르면 하나를 익혀도 다른 하나를 또 처음부터 익혀야 한다 — <b>대고 기다린다</b>
        /// 하나로 통일해 두면, 다음에 팀원이 도구를 하나 더 얹어도 같은 손짓으로 쓴다.
        /// </summary>
        /// <summary>불에 비추고 있다 — <b>대고 있는 만큼</b> 읽힌다.</summary>
        public static void Lighting(float progress)
        {
            if (_instance == null || !IsOpen) return;
            var d = _instance;
            d._litProgress = progress;
            d._sinceLit = 0f;

            // 겹 사이의 장은 <b>진작부터</b> 비쳐 든다. 다 차야 나타나면 그때까지
            // 아무 일도 안 일어나는 것과 같아서, 대고 있는 것이 맞는지조차 알 수 없다.
            if (d._litTexture != null && d._pageLit != null && progress > 0.02f && !d._pageLit.enabled)
            {
                d._pageLit.texture = d._litTexture;
                d._pageLit.enabled = true;
            }

            // ── 읽히는 정도는 세 켜다 ──
            //
            // 글자가 비치는 것과 그 글자를 읽는 것과 뜻을 새기는 것은 다른 일이다.
            // 스치듯 대면 무언가 있다는 것만 알고, 오래 대야 글자가 잡히고, 끝까지
            // 대야 뜻이 새겨진다. 급히 지나가며 다 알아내는 조사는 없다.
            if (d._lit != null)
            {
                string line;
                if (progress < Glimpse) line = "";
                else if (progress < Legible)
                    line = "겹 사이로 무언가 비친다 — 글자 같기는 한데 획이 잡히지 않는다.";
                else if (progress < 1f)
                    line = string.IsNullOrEmpty(d._litGlyphs)
                         ? "글자가 잡힌다. 뜻까지 새기려면 더 대고 있어야 한다."
                         : d._litGlyphs + "  — 획은 잡히나 뜻이 아직 안 새겨진다.";
                else line = d._litPrint;

                bool on = !string.IsNullOrEmpty(line);
                if (on) d._lit.text = Emphasis.Rich(line, Emphasis.OnDark);
                if (d._lit.gameObject.activeSelf != on) d._lit.gameObject.SetActive(on);
            }

            if (progress < 1f || d._litDone) return;

            d._litDone = true;
            var cb = d._onLit;
            if (cb != null) cb();
            ToolPractice.Done("lantern");
        }

        /// <summary>이만큼은 대고 있어야 무언가 비친다는 것을 안다.</summary>
        private const float Glimpse = 0.20f;
        /// <summary>이만큼이면 획이 잡힌다. 뜻은 아직이다.</summary>
        private const float Legible = 0.62f;

        /// <summary>이 종이에 등불로 볼 것이 남아 있나. 안내 글줄을 고를 때 쓴다.</summary>
        public static bool HasBacklight
            => _instance != null && IsOpen && !_instance._litDone
               && (_instance._litTexture != null || !string.IsNullOrEmpty(_instance._litPrint));

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            _anchor = GetComponent<WorldHudAnchor>();
            if (_anchor == null) _anchor = gameObject.AddComponent<WorldHudAnchor>();
            _anchor.SetDistance(_holdDistance, _holdDrop);   // 팔 길이 — 손에 든 거리다

            _canvas = GetComponent<Canvas>();
            _font = UiFont.Resolve(_font);
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            Build();
            SetVisible(false);
        }

        private void OnDestroy() { if (_instance == this) { _instance = null; IsOpen = false; } }

        private void ShowInternal(Texture page, string title, string body,
                                  string finePrint, System.Action onRead, bool dim,
                                  Texture litPage, string litPrint, System.Action onLit,
                                  string litGlyphs, Texture backPage)
        {
            // 뒤에 새겨진 것이 있으면 뒷면에 얹는다. 없으면 예전처럼 민면이다.
            if (_backArt != null)
            {
                _backArt.texture = backPage;
                _backArt.enabled = false;          // 켜고 끄는 것은 뒤를 볼 때 정한다
            }
            _hasBackArt = backPage != null;

            // 이웃은 <b>부르는 쪽이 그때그때</b> 일러 준다. 안 일러 주면 없는 것이다 —
            // 앞서 수첩에서 꺼냈던 이웃이 방에서 집은 종이에 붙어 있으면 안 된다.
            SetNeighbors(null, null);

            // 무대도 마찬가지다. 종이를 펴는 부름에 앞서 올려 둔 물건이 남아 있으면
            // 종이 위에 마패가 겹쳐 뜬다.
            SetStageModel(null, Vector3.zero);
            // 수첩에서 꺼내 든 것은 <b>어둠 위에</b> 놓는다. 방을 보며 조사하는 중이 아니라
            // 앉아서 물건 하나를 뜯어보는 중이므로, 둘레가 비면 그 하나에만 눈이 간다.
            // 방에서 곧바로 짚은 것에는 어둠을 깔지 않는다 — 그때는 방도 함께 봐야 한다.
            if (_backdrop != null) _backdrop.enabled = dim;

            // 집은 자리를 적어 둔다. 여기서 멀어지면 도로 내려놓는다(DropIfWalkedAway).
            var camNow = Camera.main;
            _openedAt = camNow != null ? camNow.transform.position : transform.position;

            // 종이 비율을 지켜 편다. 가로로 긴 문서를 정사각으로 늘이면 글자가 찌그러져
            // 읽을 수 있던 것도 못 읽게 된다.
            if (page != null)
            {
                _page.texture = page;
                _page.color = Color.white;      // 빈 종이를 깔며 물들여 둔 것을 되돌린다
                float w = Mathf.Max(1, page.width), h = Mathf.Max(1, page.height);
                float k = _pageSpan / Mathf.Max(w, h);
                var size = new Vector2(w * k, h * k);
                _pageRt.sizeDelta = size;
                _backFace.rectTransform.sizeDelta = size;
                _edge.rectTransform.sizeDelta = size + new Vector2(10f, 10f);
                _pageLit.rectTransform.sizeDelta = size;
                _page.enabled = true;
                _edge.enabled = true;
            }
            else
            {
                // <b>그림이 없어도 종이는 깐다.</b> 사목처럼 구워 둔 면이 없고 글만
                // 있는 문서를 그냥 띄웠더니, 흰 글씨가 방 위에 그대로 떠서 뒤의
                // 창살과 겹쳐 읽히지가 않았다. 종이는 그림이 아니라 <b>읽을 바탕</b>이다.
                // 한지 빛 한 장을 깔고 그 위에 적는다.
                _page.texture = null;
                _page.color = _blankPaper;
                // <b>넓게 편다.</b> 구워 둔 문서는 돋보기로 들여다볼 것이라 좁아도
                // 되지만, 사목처럼 <b>읽어 내려갈</b> 종이가 그 폭이면 한 줄에 아홉
                // 자밖에 안 들어가 조목 하나가 네 줄로 흩어진다. 종이를 넓혀 한 줄에
                // 스물몇 자가 들어가게 한다 — 읽는 종이는 읽히는 폭이라야 한다.
                var size = new Vector2(_pageSpan * 1.9f, _pageSpan * 1.5f);
                _pageRt.sizeDelta = size;
                _backFace.rectTransform.sizeDelta = size;
                _edge.rectTransform.sizeDelta = size + new Vector2(10f, 10f);
                _pageLit.rectTransform.sizeDelta = size;
                _page.enabled = true;
                _edge.enabled = true;
            }

            SetSlip(page != null ? title : null);

            // 요약은 <b>수첩</b>에서 읽는 것이다. 방에서 종이를 짚었을 때는 종이만 보인다 —
            // 그때는 아직 무엇인지 알아보는 중이지 정리하는 중이 아니다.
            // <b>그림이 없으면 글이 곧 종이다.</b> 이 자리는 원래 <b>요약</b> 칸이라
            // 종이 아래에 한 줄 폭으로 나 있다. 사목처럼 구워 둔 면이 없고 조목만
            // 있는 문서를 그대로 넣었더니, 여섯 조목이 그 한 줄 칸에 쏟아져 종이도
            // 없이 방 위에 떠 버렸다. 그림이 없을 때는 글을 <b>종이 위로</b> 올린다.
            // 도드라짐도 <b>바탕을 따라간다</b>. 어두운 요약 칸에서는 밝게·굵게,
            // 한지 위에서는 진하게·<b>굵기 없이</b> — 열세 눈금짜리 글씨를 굵히면
            // 획 사이가 메워져 먹이 번진 것처럼 뭉친다.
            bool onPaper = page == null;
            PlaceBody(onPaper);
            _body.text = string.IsNullOrEmpty(body)
                       ? ""
                       : Emphasis.Rich(body, onPaper ? Emphasis.OnPaper : Emphasis.OnDark, !onPaper);
            _body.gameObject.SetActive(!string.IsNullOrEmpty(body));
            if (onPaper) FitBody();

            bool hasFine = !string.IsNullOrEmpty(finePrint);
            _finePrint = hasFine ? finePrint : "";
            _fine.text = "";
            if (_trans != null)
            {
                _trans.text = "";
                _trans.color = UiLook.With(UiLook.Ink, 0f);
                _transPlate.color = UiLook.With(UiLook.Paper, 0f);
                _transPlate.gameObject.SetActive(false);
            }
            _fine.gameObject.SetActive(false);

            _onRead = onRead;
            _readDone = false;
            _readProgress = 0f;
            _sinceRead = 99f;
            _tilt = Vector2.zero;
            _spin = Vector2.zero;
            _dragging = false;
            _spinArmed = false;   // 집은 그 누름이 그대로 돌리는 손이 되지 않게
            _dragRun = 0f;

            _litPrint = string.IsNullOrEmpty(litPrint) ? "" : litPrint;
            _litGlyphs = string.IsNullOrEmpty(litGlyphs) ? "" : litGlyphs;
            _litTexture = litPage;
            _onLit = onLit;
            _litDone = false;
            _litProgress = 0f;
            _sinceLit = 99f;
            _lit.text = "";
            _lit.gameObject.SetActive(false);
            _pageLit.texture = null;
            _pageLit.enabled = false;
            _lit01 = 0f;
            if (_page != null) _page.color = _paper;

            // 안내는 <b>남은 일 하나만</b> 짚는다. 두 도구를 한 줄에 늘어놓으면 둘 다
            // 지금 해야 하는 것처럼 읽혀, 아무것도 안 해도 되는 종이 앞에서도 헤맨다.
            // IsOpen 이 아직 false 라 HasBacklight 를 못 쓴다 — 방금 넣은 값으로 직접 본다.
            bool hasLit = litPage != null || !string.IsNullOrEmpty(litPrint);
            _hint.text = hasFine
                ? "끌어서 돌려 볼 수 있다 · 잔글씨는 " + Controls.Raise + " 종이를 들여다본다"
                : hasLit
                ? "끌어서 돌려 볼 수 있다 · 불빛 앞에 대면 겹 사이가 비친다"
                : _canPutDown ? "끌어서 돌려 볼 수 있다 · " + Controls.PutDown
                : "끌어서 돌려 볼 수 있다";

            if (_closeRt != null) _closeRt.gameObject.SetActive(_canPutDown);

            SetVisible(true);
            IsOpen = true;
            if (_anchor != null) _anchor.Recenter();

            // 자막을 종이 <b>위로</b> 올린다.
            //
            // 종이는 눈에서 0.6m, 자막은 1.3m 다. 종이가 앞이라 자막 한가운데를
            // 통째로 덮는다 — "예시로 한 장 드리겠소…" 의 가운데 토막이 종이에 가려
            // 앞뒤만 읽혔다. 둘 다 눈앞에 있어야 하는 것이니 하나를 끄는 대신
            // 자막을 종이 머리 위로 비켜 세운다. 내려놓으면 제자리로 돌아간다.
            // 자막은 <b>건드리지 않는다</b>. 읽어야 하는 것은 설명이고 설명은 눈앞에
            // 있어야 한다 — 밀려날 쪽은 <b>종이</b>다. 종이는 손에 든 것이니 내려다보면
            // 되고, 고개를 들면 설명이 있다.
            if (_anchor != null) _anchor.SetDistance(_holdDistance, LowDrop);
        }

        /// <summary>
        /// 표제 쪽지에 이름을 세로로 적는다.
        ///
        /// 괄호 안 한자는 떼어 낸다 — 제첨은 무엇인지 알아보라고 붙이는 것이지
        /// 본문을 되풀이하라고 붙이는 것이 아니다. 띄어쓰기도 뗀다. 그러고도 너무 길면
        /// 쪽지가 종이를 반이나 덮으므로 여덟 자에서 끊는다.
        /// </summary>
        private void SetSlip(string title)
        {
            if (_slip == null) return;

            string s = title ?? "";
            int cut = s.IndexOf('(');
            if (cut > 0) s = s.Substring(0, cut);
            s = s.Replace(" ", "").Trim();
            if (s.Length > 8) s = s.Substring(0, 8);

            if (s.Length == 0)
            {
                _slip.enabled = false;
                _slipText.gameObject.SetActive(false);
                return;
            }

            var v = new System.Text.StringBuilder(s.Length * 2);
            for (int i = 0; i < s.Length; i++) { if (i > 0) v.Append('\n'); v.Append(s[i]); }
            _slipText.text = v.ToString();

            float h = 14f + s.Length * 19f;
            _slip.rectTransform.sizeDelta = new Vector2(30f, h);
            _slipText.rectTransform.sizeDelta = new Vector2(26f, h - 8f);
            _slip.enabled = true;
            _slipText.gameObject.SetActive(true);
        }

        private void SetVisible(bool on)
        {
            if (_group == null) return;
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
            _group.interactable = on;
        }

        private void Update()
        {
            if (!IsOpen) return;

            // ── 들여다보는 동안 종이를 <b>세워 둔다</b> ──
            //
            // 렌즈는 눈에 붙어 있다. 그러니 겨누는 일이 곧 고개를 움직이는 일인데,
            // 종이도 고개를 따라오면 <b>겨눈 자리가 영영 안 바뀐다</b> — 제 얼굴에 붙은
            // 것을 들여다보려는 꼴이라, 아무리 고개를 움직여도 늘 같은 데만 보인다.
            // 게다가 종이는 늘 조금씩 숨을 쉬고 손목도 흔들리니 글자가 계속 미끄러진다.
            // 눈에 대는 동안만 종이를 세계에 못 박고 숨도 멈춘다. 그러면 움직인 만큼
            // 렌즈가 종이 위를 지나간다 — 그것이 들여다보는 일이다.
            // 그런데 <b>못 박는 것은 익히는 자리에서만</b>이다. 조사청에서 돋보기를
            // 배울 때는 종이가 세계에 서 있어야 렌즈가 그 위를 지나가는 일이 배워지지만,
            // 사건 한복판에서까지 종이가 허공에 붙박이면 조사하다 말고 종이를 찾아
            // 고개를 돌리게 된다. 방에서 집은 종이는 늘 <b>눈앞</b>에 있어야 한다.
            // 내려놓을 수 없는 종이(익히는 동안 쥐여 준 것)만 못 박는다.
            bool peering = MagnifierLens.Peering;
            bool pinNow = peering && !_canPutDown;
            if (_anchor != null && _anchor.Frozen != pinNow) _anchor.Frozen = pinNow;

            // 손에 든 것은 가만히 있지 않는다. 아주 조금 흔들려야 종이로 보인다.
            float t = Time.time;
            float breathe = peering ? 0f
                          : Mathf.Sin(t * 0.9f) * 0.7f + Mathf.Sin(t * 2.3f) * 0.25f;

            // 끌면 <b>손에 쥔 채로 돌린다</b> — 앞뒤 어느 쪽이든 볼 수 있다.
            // 놓으면 그 자세 그대로 남는다. 손에 든 물건은 놓는다고 제자리로 돌아가지 않는다.
            //
            // <b>저 혼자 빙빙 돌던 까닭</b>: 종이를 집는 것도 왼쪽 단추이고 돌리는 것도
            // 왼쪽 단추였다. 그래서 물건을 눌러 종이가 펴진 <b>바로 그 누름</b>이 그대로
            // 돌리는 손이 되어, 손을 떼기 전에 마우스가 조금만 움직여도 종이가 팽그르르
            // 돌았다. 집은 손을 <b>한 번 뗀 뒤</b>부터 돌리는 손으로 친다.
#if ENABLE_INPUT_SYSTEM
            // 집은 손을 뗀 순간부터 돌리는 손이 된다
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null && !m.leftButton.isPressed) { _spinArmed = true; _dragRun = 0f; }
#else
            _spinArmed = true;
#endif
            Vector2 drag = _spinArmed ? DragDelta() : Vector2.zero;

            // <b>무대에 물건이 올라 있으면 손은 그쪽으로 간다.</b> 종이는 판을 돌리지만
            // 물건은 무대의 회전축을 돌린다 — 돌아가는 것이 다르므로 손도 갈린다.
            if (_onStage)
            {
                if (drag.sqrMagnitude > 0.0001f) EvidenceStage.Spin(drag * 0.4f);
#if ENABLE_INPUT_SYSTEM
                var mw = UnityEngine.InputSystem.Mouse.current;
                if (mw != null)
                {
                    float dy = mw.scroll.ReadValue().y;
                    if (Mathf.Abs(dy) > 0.01f) EvidenceStage.Zoom(Mathf.Sign(dy));
                }
#endif
            }

            _spin.x -= drag.x;
            _spin.y += drag.y;
            _spin.y = Mathf.Clamp(_spin.y, -85f, 85f);

            // 끌지 않는 동안에는 손목이 저 혼자 조금 흔들린다
            _tilt = Vector2.Lerp(_tilt, Vector2.zero, 3f * Time.deltaTime);

            if (_hand != null)
            {
                _hand.localRotation = Quaternion.Euler(9f + breathe + _spin.y + _tilt.y,
                                                       _spin.x,
                                                       -2.5f + breathe * 0.4f);
                _hand.anchoredPosition = new Vector2(_tilt.x * 1.5f, breathe * 2.2f);
            }

            // 다 읽고 나면 종이가 그것을 알린다. 돋보기를 떼도 한동안 남는다.
            // 뒤를 보고 있나 — 그동안만 뒷면으로 덮는다
            if (_backFace != null && _pageRt != null)
            {
                var cam = Camera.main;
                bool seeingBack = cam != null &&
                    Vector3.Dot(_pageRt.forward, _pageRt.position - cam.transform.position) < 0f;
                // 무대에 물건이 서 있으면 종이 쪽은 통째로 쉰다 — 뒷면 판이 켜지면
                // 마패 뒤로 한지가 비쳐 「종이에 그려진 마패」가 된다.
                if (_onStage) seeingBack = false;
                if (_backFace.enabled != seeingBack) _backFace.enabled = seeingBack;
                bool showArt = seeingBack && _hasBackArt;
                if (_backArt != null && _backArt.enabled != showArt) _backArt.enabled = showArt;
            }

            _sinceRead += Time.deltaTime;
            // 유리를 떼면 옮긴 글도 도로 옅어진다 — 다 읽은 뒤에는 남는다
            if (!_readDone && _sinceRead > 0.25f && _readProgress > 0f)
            {
                _readProgress = Mathf.MoveTowards(_readProgress, 0f, 0.8f * Time.deltaTime);
                ShowTranslation(_readProgress);
            }
            if (_readDone) _hint.text = "다 읽었다 — 수첩에 적어 두었다";
            else if (_sinceRead < 0.25f && _readProgress > 0.05f)
                _hint.text = "읽는 중… " + Mathf.RoundToInt(Mathf.Clamp01(_readProgress) * 100f) + "%";

            // ── 등불 ──
            // 불빛은 <b>진행도를 따라 밝아지되 꺼질 때는 천천히</b> 진다. 손이 조금
            // 흔들려 등불이 잠깐 어긋날 때마다 종이가 껌뻑이면 등불을 든 것이 아니라
            // 스위치를 누르는 것이 된다.
            _sinceLit += Time.deltaTime;
            bool lighting = _sinceLit < 0.25f;
            float want = _litDone ? 1f : (lighting ? Mathf.Clamp01(_litProgress) : 0f);
            // 종이 <b>자체가</b> 달아오른다.
            _lit01 = Mathf.MoveTowards(_lit01, want, (want > _lit01 ? 1.9f : 0.8f) * Time.deltaTime);
            if (_page != null)
                _page.color = Color.Lerp(_paper, UiLook.Lit(UiLook.Gold, 0.35f), _lit01);
            if (_pageLit != null && _pageLit.enabled)
                _pageLit.color = new Color(1f, 1f, 1f, _lit01);

            if (_litDone) _hint.text = "빛에 다 배어 나왔다";
            else if (lighting && _litProgress > 0.03f)
                _hint.text = "불빛에 비추는 중… " + Mathf.RoundToInt(Mathf.Clamp01(_litProgress) * 100f)
                           + "%  (똑바로 마주 댈수록 빠르다)";

            DropIfWalkedAway();

#if ENABLE_INPUT_SYSTEM
            // 내려놓을 수 없는 종이는 <b>Esc 로도</b> 못 내려놓는다. 단추만 감추고 키는
            // 열어 두면, 받은 종이가 슬그머니 사라져 과제가 끝나지 않는다.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && _canPutDown) Hide();
#endif
        }

        /// <summary>
        /// <b>걸어 나가면 손에서 놓는다.</b>
        ///
        /// 남의 집 문갑에서 꺼낸 문서를 든 채로 마당을 가로질러 대문 밖까지 나가는 것은
        /// 조사가 아니라 훔치는 것이다. 그런데 여태 한 번 편 종이는 Esc 를 누르기 전까지
        /// 눈앞에 붙어 다녔다 — 방을 나서도, 씬을 건너가도.
        ///
        /// 집은 자리에서 이만큼 멀어지면 <b>본 자리에 도로 두었다</b>고 친다. 수첩에 적힌
        /// 단서는 그대로다 — 본 것이 없던 일이 되지는 않는다.
        /// 내려놓을 수 없는 종이(익히는 동안 쥐여 준 것)는 건드리지 않는다.
        /// </summary>
        private void DropIfWalkedAway()
        {
            if (!_canPutDown || _dropDistance <= 0f) return;
            var cam = Camera.main;
            if (cam == null) return;
            if ((cam.transform.position - _openedAt).sqrMagnitude < _dropDistance * _dropDistance) return;
            Hide();
        }

        /// <summary>왼쪽 단추를 누른 채 움직인 만큼(도). 누르지 않았으면 0.</summary>
        private Vector2 DragDelta()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) { _dragging = false; return Vector2.zero; }
            if (!mouse.leftButton.isPressed) { _dragging = false; return Vector2.zero; }
            Vector2 d = mouse.delta.ReadValue();
            if (!_dragging) { _dragging = true; return Vector2.zero; }   // 누른 첫 프레임은 튀지 않게

            // <b>문턱</b> — 겨누느라 손이 미끄러진 것과 돌리려고 끈 것을 가른다.
            // 문턱을 넘기 전에는 0을 돌려주므로, 눌러서 무언가를 고르는 동안에는
            // 종이가 꿈쩍도 하지 않는다.
            _dragRun += d.magnitude;
            if (_dragRun < DragThreshold) return Vector2.zero;
            return d * 0.35f;
#else
            return Vector2.zero;
#endif
        }

        // ── 만들기 ──

        private void Build()
        {
            // 방에서 짚은 것에는 뒷배경이 없다 — 방이 그대로 보여야 "방에서 종이를 든 것"이
            // 된다. 수첩에서 꺼내 들 때만 이 어둠을 켠다.
            var backRt = NewRect("어둠", Vector2.zero, new Vector2(6000f, 4500f), transform);
            _backdrop = backRt.gameObject.AddComponent<Image>();
            // <b>덮되 지우지는 않는다.</b> 0.99 는 방을 통째로 없앤다 — 종이 한 장만
            // 남아 어디서 읽고 있는지조차 사라지니, 읽는 것이 아니라 화면이 갈린 것이
            // 된다. 0.82 면 뒤의 방이 <b>어렴풋이</b> 남아 「그 자리에서 종이를 든 것」
            // 으로 읽히면서도 눈은 종이로 간다.
            _backdrop.color = UiLook.With(UiLook.Panel, 0.82f);
            _backdrop.raycastTarget = false;
            _backdrop.enabled = false;

            // 어둠은 <b>깊이를 따지지 않고</b> 덮는다. 그러지 않으면 눈앞의 경상이며 책이며
            // 어둠보다 가까이 있는 것들이 그 위로 비어져 나와, 덮으려던 방이 도로 보인다.
            // 어둠보다 앞에 그려도 되는 것은 돋보기뿐이고, 그것은 더 나중에 그려진다.
            var ui = Shader.Find("UI/Default");
            if (ui != null)
            {
                var mat = new Material(ui) { name = "문서_어둠" };
                mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                _backdrop.material = mat;
            }

            _chrome = new GameObject("글자판", typeof(RectTransform));
            var crt = (RectTransform)_chrome.transform;
            crt.SetParent(transform, false);
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(900f, 900f);

            _hand = NewRect("손", Vector2.zero, new Vector2(_pageSpan * 1.2f, _pageSpan * 1.2f), transform);

            // 종이 가장자리 — 방 색에 종이가 묻히지 않게 얇게 두른다
            var edgeRt = NewRect("가장자리", Vector2.zero, new Vector2(_pageSpan + 10f, _pageSpan + 10f), _hand);
            _edge = edgeRt.gameObject.AddComponent<Image>();
            _edge.color = UiLook.With(UiLook.Wood, 0.55f);
            _edge.raycastTarget = false;

            // 등불빛은 <b>테두리로 두르지 않는다</b>.
            //
            // 종이보다 조금 큰 네모를 뒤에 깔고 노란빛을 넣었더니, 빛이 아니라
            // <b>노란 액자</b>가 종이를 둘렀다. 네모는 아무리 흐려도 네모다.
            // 뒤에서 빛이 든 종이는 둘레가 밝아지는 것이 아니라 <b>종이 자체가</b>
            // 누렇게 달아오르고 그 속의 것이 비쳐 나온다 — 종이 빛깔로 하면 된다.

            _pageRt = NewRect("종이", Vector2.zero, new Vector2(_pageSpan, _pageSpan), _hand);
            _page = _pageRt.gameObject.AddComponent<RawImage>();
            _page.color = _paper;
            _page.raycastTarget = false;

            // 빛에 드러난 면 — 종이 <b>위에</b> 겹쳐 배어 나온다. 사건 팀원이 같은 문서를
            // 한 장 더 그려 두면(숨은 글씨가 보이는 것으로) 그것이 서서히 떠오른다.
            // 여기서는 종이에 글자를 보태도 된다. 잔글씨와 달리 이 글자는 <b>원래 종이에
            // 그려져 있지 않았고</b>, 빛이 불러낸 것이기 때문이다.
            var litRt = NewRect("드러난면", Vector2.zero, new Vector2(_pageSpan, _pageSpan), _pageRt);
            _pageLit = litRt.gameObject.AddComponent<RawImage>();
            _pageLit.color = new Color(1f, 1f, 1f, 0f);
            _pageLit.raycastTarget = false;
            _pageLit.enabled = false;

            // ── 옮긴 글 — <b>한자 위에 우리말이 얹힌다</b> ──
            //
            // 여태 읽어낸 바는 종이 <b>밖</b>에 적었다. 종이 위에 한글을 찍으면 문서가
            // 아니라 자막이 된다는 까닭에서였다. 그런데 정작 읽는 사람에게는 그것이
            // <b>종이와 자막을 번갈아 보는 일</b>이 되었다 — 눈이 글자에 가 있는데
            // 뜻은 딴 데 적혀 있으니, 두 번 읽고 두 번 옮겨야 한다.
            //
            // 옛 문서를 읽는 사람이 실제로 하는 일은 <b>글자 위에 뜻을 겹쳐 보는</b>
            // 것이다. 그래서 옮긴 글을 종이 위에 앉힌다. 다만 한 켜 얇은 바탕을 깔아
            // 그것이 <b>원래 종이에 적힌 글이 아니라 내가 읽어낸 것</b>임을 보인다 —
            // 배접 속에서 배어 나오는 글(등불)과는 빛깔도 자리도 다르다.
            var transRt = NewRect("옮긴글", Vector2.zero, new Vector2(_pageSpan * 0.86f, _pageSpan * 0.42f), _pageRt);
            _transPlate = transRt.gameObject.AddComponent<Image>();
            _transPlate.color = UiLook.With(UiLook.Paper, 0f);
            _transPlate.raycastTarget = false;
            _trans = NewText("옮긴글자", "", Vector2.zero, new Vector2(_pageSpan * 0.80f, _pageSpan * 0.38f),
                             transRt, _fontSize - 8);
            _trans.color = UiLook.With(UiLook.Ink, 0f);
            _trans.alignment = TextAnchor.MiddleCenter;
            _trans.horizontalOverflow = HorizontalWrapMode.Wrap;
            _trans.verticalOverflow = VerticalWrapMode.Overflow;
            transRt.gameObject.SetActive(false);

            // 표제 쪽지 — <b>종이의 자식</b>이다. 그래야 종이를 돌리면 같이 돌고,
            // 뒤집으면 뒷면에 함께 덮인다. 손 밑에 따로 달면 종이는 돌아가는데 이름만
            // 제자리에 남아, 붙어 있는 것이 아니라 떠 있는 것이 된다.
            var slipRt = NewRect("표제", Vector2.zero, new Vector2(34f, 120f), _pageRt);
            slipRt.anchorMin = slipRt.anchorMax = new Vector2(0f, 1f);   // 종이 왼쪽 위 여백
            slipRt.pivot = new Vector2(0f, 1f);
            // 여백 안쪽으로 조금 들여 붙인다. 가장자리에 딱 붙이면, 타다 만 조각처럼
            // 테두리가 뜯긴 문서에서는 쪽지가 종이 밖 허공에 붙은 꼴이 된다.
            slipRt.anchoredPosition = new Vector2(17f, -13f);
            slipRt.localRotation = Quaternion.Euler(0f, 0f, 0.8f);        // 손으로 붙인 것은 반듯하지 않다
            _slip = slipRt.gameObject.AddComponent<Image>();
            _slip.color = UiLook.With(UiLook.Paper, 0.97f);
            _slip.raycastTarget = false;
            _slipText = NewText("이름", "", Vector2.zero, new Vector2(30f, 116f), slipRt, 15);
            _slipText.color = UiLook.Ink;             // 먹
            _slipText.alignment = TextAnchor.UpperCenter;
            _slipText.lineSpacing = 0.86f;

            // 종이 뒷면. UI는 앞뒤가 없어서 돌려 보면 글씨가 그대로 비쳐 보인다 —
            // 뒤집힌 글씨가 비치는 종이는 세상에 없다. 뒤를 보는 동안만 덮는다.
            //
            // <b>덮는 것이 곧 뒷면이다.</b> 여태 이 판은 한지빛 민면이었고, 앞글씨를
            // 가리는 것이 하는 일의 전부였다. 그런데 뒤에 <b>새겨진 것이 있는</b> 종이가
            // 있다 — 맞은편 백지에 눌린 자국, 재에 닿아 그을린 쪽, 배접과 수결.
            // 그런 것을 받을 자리를 낸다. 안 주면 예전처럼 민면이다.
            var backFaceRt = NewRect("뒷면", Vector2.zero, new Vector2(_pageSpan, _pageSpan), _hand);
            _backFace = backFaceRt.gameObject.AddComponent<Image>();
            _backFace.color = UiLook.Lit(UiLook.Paper, 0.30f);
            _backFace.raycastTarget = false;
            _backFace.enabled = false;

            // 뒤에 새겨진 것. 뒷면 판 위에 얹되 <b>좌우를 뒤집어</b> 그린다 —
            // 종이를 넘긴 것이므로 뒷그림도 넘긴 대로 서야 한다.
            var backArtRt = NewRect("뒷그림", Vector2.zero, new Vector2(_pageSpan, _pageSpan), backFaceRt);
            backArtRt.localScale = new Vector3(-1f, 1f, 1f);
            _backArt = backArtRt.gameObject.AddComponent<RawImage>();
            _backArt.raycastTarget = false;
            _backArt.enabled = false;

            // <b>종이가 아닌 것이 설 자리.</b> 마패·유척처럼 앞뒤가 아니라 사방이 있는
            // 물건은 평면 한 장으로는 안 보인다. 무대(<see cref="EvidenceStage"/>)에서
            // 찍어 온 그림을 여기 얹고, 그때는 종이 쪽을 통째로 끈다.
            var stageRt = NewRect("무대", Vector2.zero, new Vector2(_pageSpan, _pageSpan), _hand);
            _stageImage = stageRt.gameObject.AddComponent<RawImage>();
            _stageImage.raycastTarget = false;
            _stageImage.enabled = false;

            // 읽어낸 것 — 종이 <b>아래</b>에 뜬다. 종이 위에는 아무것도 덧그리지 않는다.
            //
            // 한때 이 글을 종이 면에 작게 얹었다. 잔글씨를 진짜로 작게 만들자는 뜻이었으나,
            // 한자로 적힌 문서 위에 한글 한 줄이 찍히는 꼴이 되었다 — 없던 글자가 종이에
            // 생겨난 것이다. 읽을 잔글씨는 이미 종이에 그려져 있다(다른 필적·수결·死亡).
            // 돋보기가 하는 일은 글자를 <b>보태는</b> 것이 아니라 그것을 <b>알아보는</b>
            // 것이므로, 읽어낸 바는 종이 밖에 적는다.
            _fine = NewText("읽어낸것", "", new Vector2(0f, -_pageSpan * 0.62f - 64f),
                            new Vector2(780f, 78f), _chrome.transform, _fontSize - 6);
            _fine.color = UiLook.Lit(UiLook.Gold, 0.40f);
            _fine.gameObject.SetActive(false);

            // 빛에 배어 나온 것 — 읽어낸 것과 같은 자리, 다른 빛깔. 불에 익은 글씨는
            // 누렇게 뜨는 것이 아니라 붉게 탄다.
            // 읽어낸 것보다 한 줄 아래다. 한 종이에 잔글씨와 숨은 글이 다 있으면
            // 둘이 같은 자리에 겹쳐 찍힌다.
            _lit = NewText("배어나온것", "", new Vector2(0f, -_pageSpan * 0.62f - 106f),
                           new Vector2(780f, 60f), _chrome.transform, _fontSize - 6);
            _lit.color = new Color(1f, 0.72f, 0.48f);
            _lit.gameObject.SetActive(false);

            _body = NewText("요약", "", new Vector2(0f, -_pageSpan * 0.62f), new Vector2(700f, 76f),
                            _chrome.transform, _fontSize - 4);
            _hint = NewText("안내", "", new Vector2(0f, -_pageSpan * 0.62f - 148f), new Vector2(700f, 38f),
                            _chrome.transform, _fontSize - 8);
            _hint.color = new Color(_textColor.r, _textColor.g, _textColor.b, 0.7f);

            _closeRt = NewRect("닫기", new Vector2(_pageSpan * 0.66f, _pageSpan * 0.58f),
                                  new Vector2(150f, 52f), _chrome.transform);
            var closeBg = _closeRt.gameObject.AddComponent<Image>();
            closeBg.color = _tabColor;
            var closeBtn = _closeRt.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(Hide);
            NewText("라벨", "내려놓기", Vector2.zero, new Vector2(150f, 56f), _closeRt, _fontSize - 8);

            // <b>이웃으로 넘기는 두 짝.</b> 수첩에서 꺼내 든 종이는 여럿 가운데 하나다 —
            // 다음 것을 보려고 매번 수첩을 폈다 덮었다 하면, 견주어 보는 일이 끊긴다.
            // 부르는 쪽이 이웃을 일러 주지 않으면(SetNeighbors) 이 두 짝은 안 뜬다.
            _prevRt = MakeStep(new Vector2(-_pageSpan * 0.78f, 0f), "◀", () => _onPrev?.Invoke());
            _nextRt = MakeStep(new Vector2(_pageSpan * 0.78f, 0f), "▶", () => _onNext?.Invoke());
            _prevRt.gameObject.SetActive(false);
            _nextRt.gameObject.SetActive(false);
        }

        private RectTransform MakeStep(Vector2 at, string glyph, UnityEngine.Events.UnityAction onClick)
        {
            var rt = NewRect("이웃", at, new Vector2(88f, 120f), _chrome.transform);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = _tabColor;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(onClick);
            NewText("글", glyph, Vector2.zero, new Vector2(88f, 120f), rt, _fontSize - 2);
            return rt;
        }

        private RectTransform _prevRt, _nextRt;
        private static System.Action _onPrev, _onNext;

        /// <summary>
        /// <b>지금 든 종이의 이웃을 일러 준다.</b> <see cref="Show"/> 바로 뒤에 부른다.
        ///
        /// Show 의 인자로 받지 않는 까닭: 이웃이 있고 없고는 <b>어디서 꺼냈느냐</b>에 달렸다.
        /// 방에서 집은 종이에는 이웃이 없고, 수첩에서 꺼낸 것에는 있다. 같은 물건인데
        /// 부르는 자리마다 다른 것이라, 종이의 생김새를 적는 자리에 끼워 넣을 것이 아니다.
        /// 두 짝을 다 비우면(null) 안 뜬다.
        /// </summary>
        /// <summary>
        /// <b>종이 대신 물건을 세운다</b> — 무대에서 찍어 온 그림으로.
        ///
        /// <see cref="Show"/> 바로 뒤에 부른다. 인자로 안 받는 까닭은 이웃과 같다:
        /// 3D 모델이 있고 없고는 <b>물건에 달린</b> 것이지 이 판의 생김새가 아니다.
        /// <c>null</c> 을 주면 도로 종이로 돌아간다.
        ///
        /// 종이 쪽을 <b>통째로</b> 끈다 — 앞면·뒷면·뒷그림까지. 반쯤 남겨 두면
        /// 마패 뒤로 한지가 비쳐 「종이에 그려진 마패」가 된다.
        /// </summary>
        public static void SetStageModel(GameObject prefab, Vector3 euler)
        {
            _onStage = prefab != null;
            if (_instance == null) return;

            if (_onStage) EvidenceStage.Show(prefab, euler);
            else EvidenceStage.Clear();

            if (_instance._stageImage != null)
            {
                _instance._stageImage.texture = EvidenceStage.Texture;
                _instance._stageImage.enabled = _onStage;
            }
            if (_instance._page != null) _instance._page.enabled = !_onStage;
            if (_instance._backFace != null && _onStage) _instance._backFace.enabled = false;
            if (_instance._backArt != null && _onStage) _instance._backArt.enabled = false;
        }

        public static void SetNeighbors(System.Action prev, System.Action next)
        {
            _onPrev = prev; _onNext = next;
            if (_instance == null) return;
            if (_instance._prevRt != null) _instance._prevRt.gameObject.SetActive(prev != null);
            if (_instance._nextRt != null) _instance._nextRt.gameObject.SetActive(next != null);
        }

        /// <summary>
        /// 글을 <b>종이 위</b>에 적을지 <b>종이 밑 요약 칸</b>에 적을지 정한다.
        ///
        /// 종이 위에 적을 때는 먹빛이라야 한다 — 한지 위에 흰 글씨를 얹으면 안 보인다.
        /// 그리고 위에서부터 적어 내려야 조목이 길어져도 첫 줄이 제자리에 있다.
        /// </summary>
        private void PlaceBody(bool onPaper)
        {
            if (_body == null) return;
            var rt = _body.rectTransform;

            if (onPaper)
            {
                rt.SetParent(_pageRt, false);
                var size = _pageRt.sizeDelta;
                rt.sizeDelta = new Vector2(size.x * 0.88f, size.y * 0.86f);
                rt.anchoredPosition = new Vector2(0f, -size.y * 0.02f);
                _body.alignment = TextAnchor.UpperLeft;
                _body.color = _paperInk;
                _body.fontSize = _fontSize - 8;
            }
            else
            {
                rt.SetParent(_chrome.transform, false);
                rt.sizeDelta = new Vector2(700f, 76f);
                rt.anchoredPosition = new Vector2(0f, -_pageSpan * 0.62f);
                _body.alignment = TextAnchor.MiddleCenter;
                _body.color = _textColor;
                _body.fontSize = _fontSize - 4;
            }
        }

        /// <summary>
        /// <b>글이 종이 밖으로 나가지 않게 글씨를 줄인다.</b>
        ///
        /// 종이 크기를 글에 맞춰 정해 두면, 조목을 한 줄 보태는 순간 그만큼 넘친다 —
        /// 실제로 사목의 여섯째 조목이 종이 밑으로 빠져 있었다. 못 박을 것은 종이지
        /// 글이 아니므로, 종이를 두고 <b>글씨</b> 쪽을 줄인다.
        ///
        /// 열두 눈금 밑으로는 안 내려간다. 그보다 작으면 줄이는 것이 아니라
        /// 안 보이게 하는 것이다 — 그때는 종이를 나누는 것이 맞다.
        /// </summary>
        private void FitBody()
        {
            if (_body == null || !_body.gameObject.activeSelf) return;
            float room = _body.rectTransform.sizeDelta.y;
            int size = _fontSize - 8;
            _body.fontSize = size;
            while (size > 12 && _body.preferredHeight > room)
            {
                size--;
                _body.fontSize = size;
            }
        }

        /// <summary>한지 위에 적는 먹빛.</summary>
        private static Color _paperInk { get { return UiLook.Ink; } }

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

        private Text NewText(string name, string s, Vector2 pos, Vector2 size, Transform parent, int fontSize)
        {
            var rt = NewRect(name, pos, size, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.color = _textColor;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = s;
            return t;
        }
    }
}
