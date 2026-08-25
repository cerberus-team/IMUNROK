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
        [SerializeField] private int _lineFontSize = 58;
        [SerializeField] private int _nameFontSize = 40;
        [SerializeField] private int _hintFontSize = 30;

        [Header("색")]
        [SerializeField] private Color _panelColor = new Color(0.03f, 0.035f, 0.05f, 0.86f);
        [SerializeField] private Color _nameplateColor = new Color(0.62f, 0.14f, 0.11f, 0.95f);
        [SerializeField] private Color _textColor = new Color(0.98f, 0.96f, 0.92f);
        [SerializeField] private Color _hintColor = new Color(1f, 0.85f, 0.5f, 0.75f);
        [Tooltip("새로 알아낸 것을 말할 때의 글빛. 수첩에 안 적히는 말이라 여기서 한 번 눈에 박혀야 한다")]
        [SerializeField] private Color _keyColor = new Color(0.95f, 0.34f, 0.28f);

        private static SubtitleView _instance;
        private CanvasGroup _group;
        private WorldHudAnchor _anchor;
        private Text _nameText, _lineText, _hintText;
        private RectTransform _nameplate;
        private NoticeCloseTab _closeTab;

        /// <summary>자막이 닫힐 때 알린다. 도구 익히기처럼 자막에 얹혀 도는 것이 참고한다.</summary>
        public static event System.Action OnClosed;

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

        /// <summary>
        /// 자막을 띄운다. speaker/hint는 비워도 된다(그 줄이 사라진다).
        ///
        /// <paramref name="key"/> 를 켜면 대사가 <b>붉게</b> 나온다 — 지금 이 한 마디가
        /// 조사에 쓰일 말이라는 뜻이다. 수첩에는 물증만 적히므로, 들은 말은 여기서
        /// 한 번 티가 나야 지나쳐 버리지 않는다.
        /// </summary>
        public static void Show(string speaker, string line, string hint = null, bool key = false)
            => Instance.ShowInternal(speaker, line, hint, key);

        /// <summary>자막을 감춘다.</summary>
        public static void Hide()
        {
            if (_instance != null) _instance.SetVisible(false);
        }

        /// <summary>
        /// 읽기 거리를 실행 중에 바꾼다. 헤드셋을 쓰고 직접 보며 맞추는 용도 —
        /// 편한 거리는 사람마다 다르고 모니터로는 판단이 안 된다.
        ///   SubtitleView.SetReadingDistance(1.0f, -0.22f);
        /// </summary>
        public static void SetReadingDistance(float distance, float verticalOffset = -0.28f)
        {
            // <b>없어도 적어 둔다.</b> 자막판은 첫 Show 때 비로소 만들어지는데, 도구
            // 익히기는 물건이 떠오르기 <b>전에</b> 자리를 잡아 둔다 — 그 사이에는
            // _instance 가 없어서 여기서 조용히 돌아 나갔고, 그렇게 잡아 둔 자리는
            // 없던 일이 되었다. 아무 말도 안 나오니 고쳐도 그대로인 것처럼 보인다.
            _wantDistance = distance; _wantDrop = verticalOffset; _hasWantDistance = true;
            if (_instance == null || _instance._anchor == null) return;   // 없으면 만들지 않는다
            _instance._anchor.SetDistance(distance, verticalOffset);
        }

        // 자막판이 생기기 전에 미리 시켜 둔 것들. 태어날 때 이대로 받아 든다.
        private static bool _wantPinned;
        private static bool _hasWantDistance;
        private static float _wantDistance = 1.3f, _wantDrop = -0.28f;

        /// <summary>
        /// 자막을 <b>눈앞에 붙박는다</b> — 고개를 어디로 돌리든 늘 시야 한가운데.
        ///
        /// 도구를 익히는 동안에만 켠다. 물건은 아래에 두고 글은 눈앞에 두었는데,
        /// 물건을 보려고 고개를 숙이면 글이 저만치 뒤에 남고 다시 들면 흔들려 따라온다.
        /// 읽는 글은 붙박여 있어야 한다.
        /// </summary>
        public static void SetPinned(bool on)
        {
            _wantPinned = on;                                             // 없어도 적어 둔다(위 참조)
            if (_instance == null || _instance._anchor == null) return;
            _instance._anchor.Pinned = on;
            if (on) _instance._anchor.Recenter();
        }

        /// <summary>지금 자막이 떠 있는가(다른 UI가 겹치지 않게 참고).</summary>
        /// <summary>
        /// 이 인물보다 앞에 서게 한다. 바짝 붙어도 상대 몸이 자막을 덮지 않는다.
        /// null 을 넣으면 원래 읽는 거리로 돌아간다.
        /// </summary>
        public static void KeepInFrontOf(Transform target)
        {
            // Instance 를 쓰면 안 된다 — 없을 때 새로 만들어 버린다.
            // 심문이 끝날 때(OnDisable) 풀어주는데, 그 순간이 씬이 닫히는 중일 수 있다.
            // 그러면 "닫는 중에 오브젝트가 새로 생겼다"고 유니티가 경고한다.
            if (_instance == null || _instance._anchor == null) return;
            _instance._anchor.KeepInFrontOf(target);
        }

        public static bool IsShowing => _instance != null && _instance._group != null && _instance._group.alpha > 0.5f;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _anchor = GetComponent<WorldHudAnchor>();
            if (_anchor == null) _anchor = gameObject.AddComponent<WorldHudAnchor>();
            // 태어나기 전에 시켜 둔 것을 받아 든다
            if (_hasWantDistance) _anchor.SetDistance(_wantDistance, _wantDrop);
            _anchor.Pinned = _wantPinned;
            Build();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void ShowInternal(string speaker, string line, string hint, bool key = false)
        {
            bool wasHidden = _group.alpha < 0.5f;

            bool hasName = !string.IsNullOrEmpty(speaker);
            _nameplate.gameObject.SetActive(hasName);
            if (hasName) _nameText.text = speaker;

            _lineText.color = key ? _keyColor : _textColor;
            // 한 문장 안에서 한두 낱말만 도드라진다 — 별표로 감싼 것(Emphasis)
            _lineText.text = Emphasis.Rich(line ?? "", Emphasis.OnDark);
            _hintText.text = hint ?? "";

            SetVisible(true);
            // 숨겨져 있다가 다시 뜰 땐 눈앞으로 바로 가져온다(감쇠 때문에 옆에서 날아오지 않게)
            if (wasHidden && _anchor != null) _anchor.Recenter();
        }

        /// <summary>
        /// 자막을 보이거나 감춘다.
        ///
        /// <paramref name="byUser"/> 는 <b>사람이 손으로 닫았는가</b>다. 이것이 참일 때만
        /// <see cref="OnClosed"/> 가 울린다.
        ///
        /// 여태는 어떤 까닭으로 사라지든 다 울렸다. 그런데 자막을 치우는 손은 여럿이다 —
        /// 문서를 펴면 읽는 자리를 내주느라 치우고, 대문에서 멀어지면 안내가 스스로
        /// 물러나고, 심문이 끝나면 정리된다. 그 모두가 "사람이 그만두었다"로 읽혔다.
        /// 조사청에서 도구를 익히다 말고 물건이 저 혼자 문갑으로 내려앉던 것이 이것이다:
        /// 다음 한 마디를 띄우려는 참에 다른 무엇이 자막을 한 번 치우면, 익히기가
        /// 그 자리에서 접혔다. <b>사라진 것과 그만둔 것은 다르다.</b>
        /// </summary>
        private void SetVisible(bool on, bool byUser = false)
        {
            if (_group == null) return;
            bool was = _group.alpha > 0.5f;
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
            // 안 보이는 동안에는 닫기 표의 콜라이더도 꺼야 한다. 켜 둔 채로 두면
            // 눈앞에 보이지 않는 판이 남아 뒤쪽 물건으로 가는 레이를 가로챈다.
            if (_closeTab != null) _closeTab.SetActive(on);
            if (was && !on && byUser) OnClosed?.Invoke();
        }

        /// <summary>
        /// 눈앞의 말을 치운다. 헤드셋에는 Esc 가 없으니 <b>보이는 표</b>가 본길이고,
        /// 키는 모니터로 시험할 때 쓰는 곁길이다.
        /// </summary>
        private void Update()
        {
            if (_group == null || _group.alpha < 0.5f) return;
#if ENABLE_INPUT_SYSTEM
            // 옛 Input 클래스를 쓰면 안 된다. 이 프로젝트는 입력을 Input System 으로
            // 넘겨 놓아서, 저것을 읽는 순간 예외가 난다 — 자막이 떠 있는 내내 매 프레임
            // 터졌고, 그래서 Esc 로 자막을 닫는 곁길이 여태 한 번도 듣지 않았다.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
                SetVisible(false, true);        // 이것도 사람이 닫은 것이다
#endif
        }

        /// <summary>
        /// 이 판은 <b>깊이를 따지지 않고</b> 그린다.
        ///
        /// 자막은 눈에서 1.3m 앞에 선다. 그런데 조사청에서는 문갑 앞에 서면 창이며
        /// 기둥이 그보다 가까워서, 자막이 <b>창 뒤로 들어가</b> 살에 잘려 읽히지 않았다.
        /// 이것은 방에 놓인 물건이 아니라 <b>눈앞에 든 글</b>이므로 무엇에도 가리면
        /// 안 된다. 문서의 어둠판이 이미 같은 까닭으로 같은 일을 한다.
        /// </summary>
        private void DrawOnTop(Graphic g)
        {
            if (g == null) return;
            var src = g.material != null ? g.material : g.defaultMaterial;
            if (src == null) return;
            var m = new Material(src) { name = src.name + "_앞에", hideFlags = HideFlags.HideAndDontSave };
            m.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            g.material = m;
        }

        private void AllOnTop()
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true)) DrawOnTop(g);
        }

        private void Build()
        {
            _font = UiFont.Resolve(_font);
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            const float w = 1200f, h = 380f;

            var panel = NewRect("바탕", Vector2.zero, new Vector2(w, h), transform);
            panel.gameObject.AddComponent<Image>().color = _panelColor;

            // 화자 이름표 — 바탕 왼쪽 위에 걸치는 낙관
            _nameplate = NewRect("이름판", new Vector2(-w * 0.5f + 150f, h * 0.5f), new Vector2(260f, 74f), panel);
            _nameplate.gameObject.AddComponent<Image>().color = _nameplateColor;
            _nameText = NewText("이름", "", Vector2.zero, new Vector2(260f, 74f), _nameplate, _nameFontSize, _textColor);

            _lineText = NewText("대사", "", new Vector2(0f, 16f), new Vector2(w - 140f, h - 140f),
                                panel, _lineFontSize, _textColor);
            _lineText.alignment = TextAnchor.MiddleLeft;
            _lineText.horizontalOverflow = HorizontalWrapMode.Wrap;

            _hintText = NewText("힌트", "", new Vector2(0f, -h * 0.5f + 40f), new Vector2(w - 140f, 44f),
                                panel, _hintFontSize, _hintColor);

            BuildCloseTab(panel, w, h);
            AllOnTop();
        }

        /// <summary>바탕 오른쪽 위 귀퉁이에 걸치는 작은 닫기 표.</summary>
        private void BuildCloseTab(RectTransform panel, float w, float h)
        {
            var size = new Vector2(150f, 68f);
            var rt = NewRect("닫기", new Vector2(w * 0.5f - size.x * 0.5f, h * 0.5f), size, panel);
            rt.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.17f, 0.16f, 0.95f);
            NewText("글", "닫기 ✕", Vector2.zero, size, rt, _hintFontSize, _hintColor);

            _closeTab = rt.gameObject.AddComponent<NoticeCloseTab>();
            _closeTab.Bind(() => SetVisible(false, true), new Vector3(size.x, size.y, 8f));
            _closeTab.SetActive(false);
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
