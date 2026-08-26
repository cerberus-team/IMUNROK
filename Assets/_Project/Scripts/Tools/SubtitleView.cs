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
        [Tooltip("켜면 색을 <b>꾸러미(IMUNROK.Ui)</b> 에서 받아 온다 — 견우팀 판과 결이 같아진다. " +
                 "끄면 아래 값을 그대로 쓴다.\n\n" +
                 "우리에게 없는 판은 부품째 받고, 있는 판은 <b>디자인만</b> 맞추기로 한 그 갈래다. " +
                 "자막 바는 물증 제시·자막 흐름과 얽혀 있어 통째로 못 갈아 끼운다")]
        [SerializeField] private bool _useCommonLook = true;
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

        /// <summary>
        /// <b>닫을 수 있는 자막인가.</b>
        ///
        /// 자막판은 대개 <b>거치적거리는 것</b>이라 닫는 길이 있어야 한다 — 물건 이름이
        /// 뜬 채로 방을 뒤지면 판이 앞을 가린다. 그런데 <b>닫을 수 없어야 하는 자리</b>도
        /// 있다. 엔딩이 그렇다 — 왕의 마지막 말과 만든 사람 이름을 「닫기」로 치울 수
        /// 있으면, 그건 읽으라고 띄운 것이 아니라 <b>지나가는 알림</b>이 된다.
        /// 게다가 닫아 버리면 그 뒤로는 아무것도 안 뜨고 어전만 남는다.
        ///
        /// 기본은 참이다. 닫는 길을 막을 쪽이 잠깐 내렸다가 끝나면 도로 올린다.
        /// </summary>
        public static bool Closable = true;

        /// <summary>
        /// 판이 새로 시작될 때 되돌린다. 정적 값은 도메인 리로드가 꺼진 프로젝트에서
        /// <b>플레이 세션을 넘겨 산다</b> — 엔딩에서 내려 둔 채로 재생이 끝나면
        /// 다음 판에서 자막을 영영 못 닫는다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetClosable() { Closable = true; }

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
            if (_closeTab != null)
            {
                // <b>둘을 따로 꺼야 한다.</b> NoticeCloseTab.SetActive 는 짚는
                // 콜라이더만 여닫는다 — 그것만 꺼 두면 <b>글씨는 그대로 남아</b>
                // 「닫기 ✕」가 눌리지도 않으면서 화면에 붙어 있다.
                // 엔딩에서 실제로 그렇게 됐다.
                _closeTab.SetActive(on && Closable);
                _closeTab.gameObject.SetActive(on && Closable);
            }
            if (was && !on && byUser) OnClosed?.Invoke();
        }

        /// <summary>
        /// 눈앞의 말을 치운다. 헤드셋에는 Esc 가 없으니 <b>보이는 표</b>가 본길이고,
        /// 키는 모니터로 시험할 때 쓰는 곁길이다.
        /// </summary>
        private bool _fitted;

        /// <summary>
        /// <b>글씨가 하한(1.30도) 밑으로 안 내려가게 한 번 재서 키운다.</b>
        ///
        /// 재 보니 안내 줄이 30단위로 <b>1.29도</b>였다 — 하한에서 0.01도 모자란다.
        /// 눈으로는 못 가리는 차이인데, 그런 자리가 헤드셋에서 「읽히긴 하는데 눈이
        /// 피로한」 것이 된다.
        ///
        /// <b>지을 때 재면 안 된다.</b> 처음에 Build 에서 쟀더니 아무것도 안 커졌다 —
        /// 그때는 판이 아직 제자리에 안 가 있어 배율이 1 이고, 1단위가 37도로 잡혀
        /// 「넉넉하다」는 답이 나온다. 앵커가 판을 옮기고 줄인 <b>뒤</b>에 재야 한다.
        /// 그래서 첫 칸이 아니라 <b>자리를 잡은 첫 칸</b>에 한 번 한다.
        /// </summary>
        private void FitToEye()
        {
            if (_fitted || _lineText == null) return;
            float scale = transform.lossyScale.y;
            if (scale > 0.5f) return;              // 아직 앵커가 안 줄였다
            var cam = Camera.main;
            if (cam == null) return;
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist < 0.05f || dist > 20f) return;   // 아직 제자리가 아니다

            _fitted = true;
            Bump(_lineText, ref _lineFontSize, scale, dist);
            Bump(_nameText, ref _nameFontSize, scale, dist);
            Bump(_hintText, ref _hintFontSize, scale, dist);
            // 닫기 딱지의 글씨도 안내 줄과 같은 크기로 짓는다 — 같이 키운다
            if (_closeTab != null)
                foreach (var t in _closeTab.GetComponentsInChildren<Text>(true))
                    if (t.fontSize < _hintFontSize) t.fontSize = _hintFontSize;
        }

        private void Bump(Text t, ref int size, float scale, float dist)
        {
            if (t == null) return;
            int want = UiLook.AtLeast(size, scale, dist);
            if (want == size) return;
            size = want;
            t.fontSize = want;
        }

        private void Update()
        {
            FitToEye();
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

        // ── 꾸러미 하단바의 세로 차림 ─────────────────
        //
        // 견우팀 꾸러미의 <b>하단바 확정안(VR)</b>을 그대로 옮긴 값이다.
        // 위에서부터: 여백 → 이름패 → 사이 → 구분선 → 사이 → 대사 → 사이 → 안내 → 여백.
        //
        // <b>입력줄만 뺐다.</b> 꾸러미 바에는 글쇠로 쳐 넣는 칸이 한 줄 있는데,
        // 우리는 <b>말로 묻는다</b>(Wit.ai). 칠 데가 없는 칸을 남겨 두면 판만 높아지고
        // 「여기에 뭘 치라는 거지」가 된다.
        //
        // ⚠ 숫자를 <b>베껴 왔다</b>. 색은 <c>DialogueUI.Palette()</c> 가 공개라 물어 오는데,
        //   치수를 쥔 <c>StyleOf</c> 는 비공개다. 견우팀에 그것도 열어 달라고 적어 둘 것 —
        //   열리면 이 상수들을 지우고 그쪽을 부르면 된다.
        private const float BarW = 1500f;
        private const float PadX = 60f, PadTop = 18f;
        private const float NameToRule = 12f, RuleH = 3f, RuleToLine = 28f;
        private const float LineToFoot = 40f, FootH = 36f, FootToEdge = 24f;

        private void Build()
        {
            _font = UiFont.Resolve(_font);
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            // ── 색은 <b>한 자락도 안 베낀다</b> ─────────────
            //
            // 낙관의 붉은색까지 꾸러미 것으로 넘긴다. 한동안 「그건 꾸러미에 없는
            // 색이고 이 게임의 글투」라며 남겨 두었는데, 찾아보니 <b>저쪽도 이름패는
            // 붉었다</b> — InventorySkin.Vermilion, 주칠이다. 없는 색이 아니라
            // 내가 안 찾아본 색이었다.
            //
            // 이제 이 판에 우리가 손으로 정한 색은 하나도 없다.
            var pal = IMUNROK.Ui.DialogueUI.Palette();
            if (_useCommonLook)
            {
                _panelColor = pal.back;          // 먹빛 65%
                _textColor = pal.text;
                _hintColor = pal.dim;
                _nameplateColor = UiLook.Seal;   // 주칠
                _lineFontSize = 46; _nameFontSize = 36; _hintFontSize = 34;
            }

            float nameH = _nameFontSize + 18f;
            float lineBoxH = Mathf.Round(_lineFontSize * 1.28f) * 3f;   // 대사 세 줄
            float h = PadTop + nameH + NameToRule + RuleH + RuleToLine
                    + lineBoxH + LineToFoot + FootH + FootToEdge;
            const float w = BarW;

            var panel = NewRect("바탕", Vector2.zero, new Vector2(w, h), transform);
            panel.gameObject.AddComponent<Image>().color = _panelColor;

            // <b>목재 테두리</b> — 꾸러미 바에 있고 우리에게 없던 것이다.
            // 먹빛 판이 밤 배경에 얹히면 어디까지가 판인지 경계가 사라진다.
            Edge(panel, w, h, pal.border);

            // 위에서부터 쌓아 내려간다. 자리를 하나씩 손으로 잡으면 값 하나만 고쳐도
            // 아래가 죄 어긋난다 — 커서를 두고 내린다.
            float top = h * 0.5f;
            float y = top - PadTop;

            // 이름표 — 낙관. 판 <b>안</b> 왼쪽 위다(꾸러미와 같은 자리).
            float nameW = 300f;
            _nameplate = NewRect("이름판",
                new Vector2(-w * 0.5f + PadX + nameW * 0.5f, y - nameH * 0.5f),
                new Vector2(nameW, nameH), panel);
            _nameplate.gameObject.AddComponent<Image>().color = _nameplateColor;
            // 이름 글씨는 대사와 다른 색이다 — 주칠 위에서는 한지빛이라야 뜬다
            _nameText = NewText("이름", "", Vector2.zero, new Vector2(nameW, nameH),
                                _nameplate, _nameFontSize,
                                _useCommonLook ? UiLook.SealText : _textColor);
            y -= nameH + NameToRule;

            // 구분선 — 이름과 말을 가른다
            var rule = NewRect("구분선", new Vector2(0f, y - RuleH * 0.5f),
                               new Vector2(w - PadX * 2f, RuleH), panel);
            rule.gameObject.AddComponent<Image>().color = pal.border;
            y -= RuleH + RuleToLine;

            _lineText = NewText("대사", "", new Vector2(0f, y - lineBoxH * 0.5f),
                                new Vector2(w - PadX * 2f, lineBoxH),
                                panel, _lineFontSize, _textColor);
            _lineText.alignment = TextAnchor.UpperLeft;
            _lineText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _lineText.verticalOverflow = VerticalWrapMode.Truncate;
            y -= lineBoxH + LineToFoot;

            _hintText = NewText("힌트", "", new Vector2(0f, y - FootH * 0.5f),
                                new Vector2(w - PadX * 2f, FootH),
                                panel, _hintFontSize, _hintColor);
            _hintText.alignment = TextAnchor.MiddleCenter;

            BuildCloseTab(panel, w, h);
            AllOnTop();
        }

        /// <summary>
        /// 테두리 넉 줄. 꾸러미 바의 목재 테두리다.
        ///
        /// 상자 하나에 외곽선을 그릴 방법이 없어(Image 는 테두리를 안 그린다)
        /// 얇은 띠 넷을 두른다. 값은 싸고 결과는 같다.
        /// </summary>
        private void Edge(RectTransform panel, float w, float h, Color c)
        {
            const float t = 4f;
            Strip(panel, "테_위", new Vector2(0f, h * 0.5f - t * 0.5f), new Vector2(w, t), c);
            Strip(panel, "테_아래", new Vector2(0f, -h * 0.5f + t * 0.5f), new Vector2(w, t), c);
            Strip(panel, "테_좌", new Vector2(-w * 0.5f + t * 0.5f, 0f), new Vector2(t, h), c);
            Strip(panel, "테_우", new Vector2(w * 0.5f - t * 0.5f, 0f), new Vector2(t, h), c);
        }

        private void Strip(RectTransform parent, string name, Vector2 at, Vector2 size, Color c)
        {
            var rt = NewRect(name, at, size, parent);
            rt.gameObject.AddComponent<Image>().color = c;
        }

        private void BuildCloseTab(RectTransform panel, float w, float h)
        {
            // 꾸러미와 같이 <b>판 안</b> 오른쪽 위다. 예전에는 판 밖에 걸터앉아 있어서
            // 어디에 딸린 단추인지 알 수 없었다.
            var size = new Vector2(120f, 56f);
            var rt = NewRect("닫기",
                new Vector2(w * 0.5f - PadX - size.x * 0.5f, h * 0.5f - PadTop - size.y * 0.5f),
                size, panel);
            // 이 딱지 색도 손으로 정하지 않는다. 꾸러미의 <b>글쇠 칸</b> 색을 쓴다 —
            // 저쪽에서 「눌러도 되는 자리」를 알리는 데 쓰는 색이라 뜻이 맞는다.
            rt.gameObject.AddComponent<Image>().color = IMUNROK.Ui.DialogueUI.Palette().slotBack;
            NewText("글", "✕", Vector2.zero, size, rt, _hintFontSize, _hintColor);

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
