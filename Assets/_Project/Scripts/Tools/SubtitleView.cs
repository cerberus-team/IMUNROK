using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 월드 공간 자막 — 화자 이름 + 대사 + 진행 힌트.
    ///
    /// 프로젝트 안에서 이 모양이 네 곳에 거의 똑같이 반복된다
    /// (대문 앞 대사 · 심문 자막 · 복명 낭독 · 인트로).
    /// OnGUI는 헤드셋에 렌더링되지 않으므로 그 넷을 전부 여기로 모은다.
    ///
    /// 쓰는 법 — 씬에 미리 둘 필요 없다. 어디서든:
    ///   SubtitleView.Show("마름", "이 야심한 밤에… 뉘시오?", "(계속)");
    ///   SubtitleView.Hide();
    /// 처음 호출될 때 Canvas·앵커와 함께 스스로 만들어진다.
    ///
    /// 화자 이름은 낙관(붉은 인장) 느낌의 작은 판 위에 얹는다 — 기존 OnGUI 연출을 그대로 옮긴 것.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class SubtitleView : MonoBehaviour
    {
        [Header("모양")]
        [SerializeField] private Font _font;
        [SerializeField] private int _lineFontSize = 34;
        [SerializeField] private int _nameFontSize = 26;
        [SerializeField] private int _hintFontSize = 22;

        [Header("색")]
        [SerializeField] private Color _panelColor = new Color(0.03f, 0.035f, 0.05f, 0.86f);
        [SerializeField] private Color _nameplateColor = new Color(0.62f, 0.14f, 0.11f, 0.95f);
        [SerializeField] private Color _textColor = new Color(0.98f, 0.96f, 0.92f);
        [SerializeField] private Color _hintColor = new Color(1f, 0.85f, 0.5f, 0.75f);

        private static SubtitleView _instance;
        private CanvasGroup _group;
        private WorldHudAnchor _anchor;
        private Text _nameText, _lineText, _hintText;
        private RectTransform _nameplate;

        /// <summary>없으면 만들어서 돌려준다. 씬에 미리 배치해 뒀으면 그걸 쓴다.</summary>
        public static SubtitleView Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<SubtitleView>();
                    if (_instance == null)
                    {
                        var go = new GameObject("VR_자막", typeof(Canvas));
                        go.AddComponent<WorldHudAnchor>().Configure(WorldHudAnchor.Placement.Front);
                        _instance = go.AddComponent<SubtitleView>();
                    }
                }
                return _instance;
            }
        }

        /// <summary>자막을 띄운다. speaker/hint는 비워도 된다(그 줄이 사라진다).</summary>
        public static void Show(string speaker, string line, string hint = null)
            => Instance.ShowInternal(speaker, line, hint);

        /// <summary>자막을 감춘다.</summary>
        public static void Hide()
        {
            if (_instance != null) _instance.SetVisible(false);
        }

        /// <summary>지금 자막이 떠 있는가(다른 UI가 겹치지 않게 참고).</summary>
        public static bool IsShowing => _instance != null && _instance._group != null && _instance._group.alpha > 0.5f;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _anchor = GetComponent<WorldHudAnchor>();
            if (_anchor == null) _anchor = gameObject.AddComponent<WorldHudAnchor>();
            Build();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void ShowInternal(string speaker, string line, string hint)
        {
            bool wasHidden = _group.alpha < 0.5f;

            bool hasName = !string.IsNullOrEmpty(speaker);
            _nameplate.gameObject.SetActive(hasName);
            if (hasName) _nameText.text = speaker;

            _lineText.text = line ?? "";
            _hintText.text = hint ?? "";

            SetVisible(true);
            // 숨겨져 있다가 다시 뜰 땐 눈앞으로 바로 가져온다(감쇠 때문에 옆에서 날아오지 않게)
            if (wasHidden && _anchor != null) _anchor.Recenter();
        }

        private void SetVisible(bool on)
        {
            if (_group == null) return;
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
        }

        private void Build()
        {
            _font = UiFont.Resolve(_font);
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            const float w = 1300f, h = 300f;

            var panel = NewRect("바탕", Vector2.zero, new Vector2(w, h), transform);
            panel.gameObject.AddComponent<Image>().color = _panelColor;

            // 화자 이름표 — 바탕 왼쪽 위에 걸치는 낙관
            _nameplate = NewRect("이름판", new Vector2(-w * 0.5f + 130f, h * 0.5f), new Vector2(220f, 60f), panel);
            _nameplate.gameObject.AddComponent<Image>().color = _nameplateColor;
            _nameText = NewText("이름", "", Vector2.zero, new Vector2(220f, 60f), _nameplate, _nameFontSize, _textColor);

            _lineText = NewText("대사", "", new Vector2(0f, 12f), new Vector2(w - 120f, h - 110f),
                                panel, _lineFontSize, _textColor);
            _lineText.alignment = TextAnchor.MiddleLeft;
            _lineText.horizontalOverflow = HorizontalWrapMode.Wrap;

            _hintText = NewText("힌트", "", new Vector2(0f, -h * 0.5f + 34f), new Vector2(w - 120f, 40f),
                                panel, _hintFontSize, _hintColor);
        }

        // ── UI 만들기 헬퍼 ──

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
