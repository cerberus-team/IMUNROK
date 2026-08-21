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
    /// <b>잔글씨</b>는 종이에 진짜로 작게 적힌다. 맨눈으로는 획이 뭉개져 무슨 글자인지
    /// 모르고, <see cref="MagnifierLens"/>를 눈에 대고 들여다보면 그제야 읽힌다.
    /// "돋보기를 들었으니 글줄을 하나 더 보여준다"가 아니라, 정말로 작아서 안 보이는
    /// 것을 유리로 키워 보는 것이다. 단서도 그때 적힌다.
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
        [Tooltip("눈높이보다 이만큼 아래(m). 종이는 내려다보는 것이다")]
        [SerializeField] private float _holdDrop = -0.06f;
        [Tooltip("잔글씨 크기(본문 대비). 작을수록 돋보기가 있어야 읽힌다")]
        [Range(0.2f, 0.8f)] [SerializeField] private float _fineScale = 0.26f;
        [SerializeField] private Color _paper = Color.white;
        [SerializeField] private Color _textColor = new Color(0.98f, 0.96f, 0.92f);
        [SerializeField] private Color _inkColor = new Color(0.13f, 0.09f, 0.06f);
        [SerializeField] private Color _tabColor = new Color(0.28f, 0.10f, 0.09f, 0.9f);

        private static DocumentView _instance;

        private CanvasGroup _group;
        private WorldHudAnchor _anchor;
        private RectTransform _hand;      // 종이를 쥔 손 — 여기가 흔들린다
        private RawImage _page;
        private RectTransform _pageRt;
        private Image _edge;
        private Text _title;
        private Text _body;
        private Text _fine;
        private Text _hint;

        private System.Action _onRead;
        private bool _readDone;
        private float _readProgress;
        private float _sinceRead;
        private Vector2 _tilt;            // 손목으로 종이를 기울인 정도

        /// <summary>지금 문서를 쥐고 있나. 다른 UI가 참고한다(도구벨트 숨김 등).</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>
        /// 문서를 손에 쥔다.
        /// </summary>
        /// <param name="page">종이 면(문서 텍스처)</param>
        /// <param name="title">무슨 문서인가</param>
        /// <param name="body">맨눈으로도 아는 것</param>
        /// <param name="finePrint">종이에 작게 적히는 것 — 돋보기로만 읽힌다. 없으면 비운다</param>
        /// <param name="onRead">잔글씨를 다 읽었을 때 한 번</param>
        public static void Show(Texture page, string title, string body,
                                string finePrint = null, System.Action onRead = null)
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<DocumentView>();
                if (_instance == null)
                {
                    var go = new GameObject("VR_문서보기", typeof(Canvas));
                    go.AddComponent<WorldHudAnchor>().Configure(WorldHudAnchor.Placement.Front);
                    _instance = go.AddComponent<DocumentView>();
                }
            }
            _instance.ShowInternal(page, title, body, finePrint, onRead);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.SetVisible(false);
            _instance._onRead = null;
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

        /// <summary>돋보기가 종이를 들여다보는 중. 진행도가 1을 넘으면 다 읽은 것이다.</summary>
        public static void Reading(float progress)
        {
            if (_instance == null || !IsOpen) return;
            _instance._readProgress = progress;
            _instance._sinceRead = 0f;
            if (progress < 1f || _instance._readDone) return;

            _instance._readDone = true;
            var cb = _instance._onRead;
            if (cb != null) cb();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            _anchor = GetComponent<WorldHudAnchor>();
            if (_anchor == null) _anchor = gameObject.AddComponent<WorldHudAnchor>();
            _anchor.SetDistance(_holdDistance, _holdDrop);   // 팔 길이 — 손에 든 거리다

            _font = UiFont.Resolve(_font);
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            Build();
            SetVisible(false);
        }

        private void OnDestroy() { if (_instance == this) { _instance = null; IsOpen = false; } }

        private void ShowInternal(Texture page, string title, string body,
                                  string finePrint, System.Action onRead)
        {
            // 종이 비율을 지켜 편다. 가로로 긴 문서를 정사각으로 늘이면 글자가 찌그러져
            // 읽을 수 있던 것도 못 읽게 된다.
            if (page != null)
            {
                _page.texture = page;
                float w = Mathf.Max(1, page.width), h = Mathf.Max(1, page.height);
                float k = _pageSpan / Mathf.Max(w, h);
                var size = new Vector2(w * k, h * k);
                _pageRt.sizeDelta = size;
                _edge.rectTransform.sizeDelta = size + new Vector2(10f, 10f);
                _page.enabled = true;
                _edge.enabled = true;
            }
            else { _page.enabled = false; _edge.enabled = false; }

            _title.text = title;
            _body.text = body;

            bool hasFine = !string.IsNullOrEmpty(finePrint);
            _fine.text = hasFine ? finePrint : "";
            _fine.gameObject.SetActive(hasFine);
            // 잔글씨는 종이 아래쪽 여백에 적힌다 — 본문 위에 겹쳐 놓으면 얼룩으로 보인다
            _fine.rectTransform.sizeDelta = new Vector2(_pageRt.sizeDelta.x * 0.82f, _pageRt.sizeDelta.y * 0.3f);
            _fine.rectTransform.anchoredPosition = new Vector2(0f, -_pageRt.sizeDelta.y * 0.36f);

            _onRead = onRead;
            _readDone = false;
            _readProgress = 0f;
            _sinceRead = 99f;
            _tilt = Vector2.zero;

            _hint.text = hasFine
                ? "잔글씨가 있다. 돋보기를 들고 오른쪽 단추로 눈에 대어 본다"
                : "(Esc — 내려놓기)";

            SetVisible(true);
            IsOpen = true;
            if (_anchor != null) _anchor.Recenter();
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

            // 손에 든 것은 가만히 있지 않는다. 아주 조금 흔들려야 종이로 보인다.
            float t = Time.time;
            float breathe = Mathf.Sin(t * 0.9f) * 0.7f + Mathf.Sin(t * 2.3f) * 0.25f;

            // 마우스를 움직이면 손목을 틀어 종이를 기울인다(비춰 보는 짓)
            Vector2 aim = LookNudge();
            _tilt = Vector2.Lerp(_tilt, aim, 6f * Time.deltaTime);

            if (_hand != null)
            {
                _hand.localRotation = Quaternion.Euler(9f + breathe + _tilt.y, _tilt.x, -2.5f + breathe * 0.4f);
                _hand.anchoredPosition = new Vector2(_tilt.x * 1.5f, breathe * 2.2f);
            }

            // 다 읽고 나면 종이가 그것을 알린다. 돋보기를 떼도 한동안 남는다.
            _sinceRead += Time.deltaTime;
            if (_readDone) _hint.text = "다 읽었다 — 수첩에 적어 두었다";
            else if (_sinceRead < 0.25f && _readProgress > 0.05f)
                _hint.text = "읽는 중… " + Mathf.RoundToInt(Mathf.Clamp01(_readProgress) * 100f) + "%";

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Hide();
#endif
        }

        /// <summary>마우스를 움직인 만큼 손목을 튼다. VR에서는 0(고개가 곧 손이다).</summary>
        private static Vector2 LookNudge()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return Vector2.zero;
            Vector2 d = mouse.delta.ReadValue();
            return new Vector2(Mathf.Clamp(d.x * 0.35f, -7f, 7f), Mathf.Clamp(-d.y * 0.35f, -7f, 7f));
#else
            return Vector2.zero;
#endif
        }

        // ── 만들기 ──

        private void Build()
        {
            // 뒷배경은 없다. 방이 그대로 보여야 "방에서 종이를 든 것"이 된다.
            _hand = NewRect("손", Vector2.zero, new Vector2(_pageSpan * 1.2f, _pageSpan * 1.2f), transform);

            // 종이 가장자리 — 방 색에 종이가 묻히지 않게 얇게 두른다
            var edgeRt = NewRect("가장자리", Vector2.zero, new Vector2(_pageSpan + 10f, _pageSpan + 10f), _hand);
            _edge = edgeRt.gameObject.AddComponent<Image>();
            _edge.color = new Color(0.20f, 0.16f, 0.12f, 0.55f);
            _edge.raycastTarget = false;

            _pageRt = NewRect("종이", Vector2.zero, new Vector2(_pageSpan, _pageSpan), _hand);
            _page = _pageRt.gameObject.AddComponent<RawImage>();
            _page.color = _paper;
            _page.raycastTarget = false;

            // 잔글씨는 종이 위에 진짜로 작게 적힌다 — 돋보기로만 읽힌다
            _fine = NewText("잔글씨", "", Vector2.zero, new Vector2(_pageSpan * 0.86f, _pageSpan * 0.4f),
                            _pageRt, Mathf.Max(4, Mathf.RoundToInt(_fontSize * _fineScale)));
            // 흐린 먹으로 작게 — 맨눈에는 획이 뭉개져야 한다
            _fine.color = new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.86f);
            _fine.alignment = TextAnchor.UpperCenter;

            _title = NewText("제목", "", new Vector2(0f, _pageSpan * 0.60f), new Vector2(700f, 42f),
                             transform, _fontSize - 2);
            _body = NewText("본문", "", new Vector2(0f, -_pageSpan * 0.62f), new Vector2(700f, 76f),
                            transform, _fontSize - 4);
            _hint = NewText("안내", "", new Vector2(0f, -_pageSpan * 0.62f - 62f), new Vector2(700f, 38f),
                            transform, _fontSize - 8);
            _hint.color = new Color(_textColor.r, _textColor.g, _textColor.b, 0.7f);

            var closeRt = NewRect("닫기", new Vector2(_pageSpan * 0.66f, _pageSpan * 0.58f),
                                  new Vector2(150f, 52f), transform);
            var closeBg = closeRt.gameObject.AddComponent<Image>();
            closeBg.color = _tabColor;
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(Hide);
            NewText("라벨", "내려놓기", Vector2.zero, new Vector2(150f, 56f), closeRt, _fontSize - 8);
        }

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
