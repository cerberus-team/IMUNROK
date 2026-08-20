using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 문서 한 장을 <b>눈앞에 크게</b> 펼쳐 보여준다.
    ///
    /// 왜 필요한가: 장부도 별급문기도 손바닥만 한 종잇장이라, 마루에 놓인 채로는 글자가
    /// 획 하나도 안 보인다. 돋보기를 손에 익혀 놓고도 쓸 데가 없었던 까닭이 이것이다 —
    /// 들여다볼 방법이 없으니 들고 다녀도 아무 일이 안 생긴다.
    ///
    /// 그래서 <b>집어 들지 않고</b> 크게 본다. 종이는 놓인 자리에 그대로 두고 그 종이의
    /// 면만 눈앞으로 끌어와 펼친다. 돋보기를 들여다보는 일이 원래 그런 것이다 —
    /// 물건을 옮기는 게 아니라 눈을 갖다 대는 것.
    ///
    /// 맨눈과 돋보기가 다르다:
    ///   · 맨눈   — 종이는 크게 보이나 잔글씨는 뭉개진다. 무슨 문서인지까지만 안다.
    ///   · 돋보기 — 획까지 읽힌다. 이때에야 필적이 갈리는 것이 보이고 단서가 적힌다.
    ///
    /// 씬에 미리 둘 필요 없다 — 처음 부를 때 스스로 만든다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class DocumentView : MonoBehaviour
    {
        [SerializeField] private Font _font;
        [SerializeField] private int _fontSize = 28;
        [Tooltip("펼친 종이의 긴 변(월드 캔버스 단위). 눈앞 가득 차되 고개를 안 돌려도 되게")]
        [SerializeField] private float _pageSpan = 620f;
        [SerializeField] private Color _paper = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color _backdrop = new Color(0.04f, 0.04f, 0.05f, 0.86f);
        [SerializeField] private Color _textColor = new Color(0.98f, 0.96f, 0.92f);
        [SerializeField] private Color _tabColor = new Color(0.28f, 0.10f, 0.09f, 0.9f);

        private static DocumentView _instance;

        private CanvasGroup _group;
        private WorldHudAnchor _anchor;
        private RawImage _page;
        private RectTransform _pageRt;
        private Text _title;
        private Text _body;
        private Text _hint;

        /// <summary>지금 문서를 펼쳐 놓고 있나. 다른 UI가 참고한다(도구벨트 숨김 등).</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>
        /// 문서를 펼친다. <paramref name="magnified"/> 가 참이면 잔글씨까지 읽힌다.
        /// </summary>
        public static void Show(Texture page, string title, string body, bool magnified)
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
            _instance.ShowInternal(page, title, body, magnified);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.SetVisible(false);
            IsOpen = false;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            _anchor = GetComponent<WorldHudAnchor>();
            if (_anchor == null) _anchor = gameObject.AddComponent<WorldHudAnchor>();
            _anchor.SetDistance(1.1f, -0.02f);   // 자막보다 가깝게 — 읽는 것이 지금 할 일이다

            _font = UiFont.Resolve(_font);
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            Build();
            SetVisible(false);
        }

        private void OnDestroy() { if (_instance == this) { _instance = null; IsOpen = false; } }

        private void ShowInternal(Texture page, string title, string body, bool magnified)
        {
            // 종이 비율을 지켜 펼친다. 가로로 긴 문서를 정사각으로 늘이면 글자가 찌그러져
            // 읽을 수 있던 것도 못 읽게 된다.
            if (page != null)
            {
                _page.texture = page;
                float w = Mathf.Max(1, page.width), h = Mathf.Max(1, page.height);
                float k = _pageSpan / Mathf.Max(w, h);
                _pageRt.sizeDelta = new Vector2(w * k, h * k);
                _page.enabled = true;
            }
            else _page.enabled = false;

            _title.text = title;
            _body.text = body;
            _hint.text = magnified
                ? "(돋보기로 들여다보는 중 — Esc 또는 닫기)"
                : "(잔글씨는 알아볼 수 없다. 돋보기를 들고 다시 보시오)";

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
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Hide();
#endif
        }

        // ── 만들기 ──

        private void Build()
        {
            // 뒤를 어둡게 깔아 종이만 남긴다. 방이 그대로 비치면 글자를 못 읽는다.
            var back = NewRect("뒷배경", Vector2.zero, new Vector2(1400f, 900f), transform);
            var bg = back.gameObject.AddComponent<Image>();
            bg.color = _backdrop;

            _pageRt = NewRect("종이", new Vector2(0f, 40f), new Vector2(_pageSpan, _pageSpan), transform);
            _page = _pageRt.gameObject.AddComponent<RawImage>();
            _page.color = _paper;
            _page.raycastTarget = false;

            _title = NewText("제목", "", new Vector2(0f, 400f), new Vector2(1200f, 60f), transform, _fontSize + 8);
            _body = NewText("본문", "", new Vector2(0f, -350f), new Vector2(1100f, 90f), transform, _fontSize);
            _hint = NewText("안내", "", new Vector2(0f, -430f), new Vector2(1100f, 50f), transform, _fontSize - 6);
            _hint.color = new Color(_textColor.r, _textColor.g, _textColor.b, 0.72f);

            var closeRt = NewRect("닫기", new Vector2(520f, 400f), new Vector2(200f, 78f), transform);
            var closeBg = closeRt.gameObject.AddComponent<Image>();
            closeBg.color = _tabColor;
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(Hide);
            NewText("라벨", "✕ 닫기", Vector2.zero, new Vector2(200f, 78f), closeRt, _fontSize - 4);
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
