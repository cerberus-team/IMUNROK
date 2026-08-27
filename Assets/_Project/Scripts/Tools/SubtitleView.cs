using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

        /// <summary>
        /// 꾸러미의 <b>결(무늬)</b>. 한지·나뭇결·칸을 코드로 그려 들고 있다.
        ///
        /// <b>정적으로 들면 안 된다</b> — 저쪽 주석의 경고다. 도메인 리로드가 꺼진
        /// 프로젝트에서 정적 텍스처 참조는 판을 넘겨 살아남는데 <b>내용은 죽는다</b>.
        /// 판마다 제 것을 들고, 판이 죽으면 같이 죽는다.
        ///
        /// 색만 맞추고 이걸 안 썼더니 「같은 주칠인데 왜 달라 보이지」가 됐다 —
        /// 저쪽은 나뭇결이 비치고 우리는 판판했다.
        /// </summary>
        private readonly IMUNROK.Ui.InventorySkin _skin = new IMUNROK.Ui.InventorySkin();

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

            // 물을 상대가 있을 때만 입력줄이 뜬다
            if (_inputRow != null) _inputRow.gameObject.SetActive(InterrogationController.AnyOpen);

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

            // <b>이것은 헤드셋의 규칙이다.</b> 1.30도 하한은 「눈에서 몇 도로 보이나」를
            // 따지는 값인데, 모니터에서는 그 물음이 성립하지 않는다 — 화면이 곧 시야라
            // 몇 미터 앞에 앉느냐로 정해지지 우리가 정할 수가 없다.
            //
            // 그런데 여태 모니터에서도 이걸 돌리고 있었다. 재 보니 1.5m·0.001배에서
            // 1단위가 0.038도라 안내 줄 <b>19가 35로</b>, 이름 32가 35로 부풀었다.
            // 저쪽 PC판은 19·32 그대로다 — 판 치수를 한 픽셀까지 맞춰 놓고
            // <b>글씨만 두 배로 키워</b> 놓고 있었던 것이다.
            if (!VRRig.Active) { _fitted = true; return; }
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
            SitNow();
            if (_group == null || _group.alpha < 0.5f) return;
            PaintHeard();
            PaintMic();
            TickConfirm();
#if ENABLE_INPUT_SYSTEM
            // 옛 Input 클래스를 쓰면 안 된다. 이 프로젝트는 입력을 Input System 으로
            // 넘겨 놓아서, 저것을 읽는 순간 예외가 난다 — 자막이 떠 있는 내내 매 프레임
            // 터졌고, 그래서 Esc 로 자막을 닫는 곁길이 여태 한 번도 듣지 않았다.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
                SetVisible(false, true);        // 이것도 사람이 닫은 것이다

            // ── Enter — 묻기 ──
            //
            // 저쪽 대화창이 Enter 로 던진다. 우리는 여태 <b>던지는 절차 자체가 없었다</b> —
            // 마이크가 문장을 끝내는 순간 그대로 날아갔다. 이제 칸에 올라 있는 말을
            // 사람이 보고 Enter 로 던진다(「묻 기」 단추와 같은 길이다).
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                var a = InterrogationController.Active;
                if (a != null) a.AskDraft();
            }
#endif
        }

        /// <summary>
        /// 입력줄 칸에 <b>받아 적힌 말</b>을 비춘다.
        ///
        /// 세 가지가 번갈아 온다 — 지금 받아 적히는 중이면 그 글이 또렷하게, 던지고 나서
        /// 비었으면 <b>방금 던진 말이 묽게</b>, 그것마저 없으면 무엇을 누르라는 안내가 온다.
        /// 던진 말을 지워 버리면 무엇으로 전해졌는지 확인할 데가 없어진다.
        /// </summary>
        /// <summary>듣고 있는 동안 마이크가 <b>주칠로 물든다</b> — 저쪽이 하는 것과 같다.
        /// 단추가 눌린 것만으로는 정말 듣고 있는지 알 수가 없다.</summary>
        private void PaintMic()
        {
            if (_micIcon == null) return;
            bool listening = MicInput.Instance != null && MicInput.Instance.IsListening;
            var want = listening ? UiLook.Seal : UiLook.Paper;
            if (_micIcon.color != want) _micIcon.color = want;
        }

        /// <summary>되물은 뒤 이만큼 지나면 없던 일로 한다 — 잘못 눌렀을 때 그냥 두면 된다.</summary>
        private void TickConfirm()
        {
            if (_confirmLeft <= 0f) return;
            _confirmLeft -= Time.unscaledDeltaTime;
            if (_confirmLeft <= 0f) { _confirmLeft = 0f; ResetEndLook(); }
        }

        private void PaintHeard()
        {
            if (_heardText == null) return;
            var a = InterrogationController.Active;
            var pal = IMUNROK.Ui.DialogueUI.Palette();

            string draft = a != null ? a.Draft : "";
            if (!string.IsNullOrEmpty(draft))
            {
                if (_heardText.text != draft) _heardText.text = draft;
                if (_heardText.color != pal.text) _heardText.color = pal.text;
                return;
            }

            string last = a != null ? a.LastPlayerLine : "";
            string show = string.IsNullOrEmpty(last)
                        ? Controls.SpeakPrompt
                        : last;
            if (_heardText.text != show) _heardText.text = show;
            if (_heardText.color != pal.slotHint) _heardText.color = pal.slotHint;
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

        /// <summary>
        /// <b>꾸러미 하단바의 치수 한 벌.</b> 이름과 뜻을 저쪽 <c>BottomStyle</c> 에서 그대로 가져왔다.
        ///
        /// ⚠ <b>베껴 온 것이다.</b> 색을 쥔 <c>Palette()</c> 는 공개라 물어 오는데,
        ///   치수를 쥔 <c>StyleOf</c> 는 비공개다. 그것이 열리면 이 구조체를 지우고
        ///   그쪽을 부르면 된다 — 팀원께 열어 달라고 청해 둘 것.
        /// </summary>
        private struct BarStyle
        {
            public float w;             // 판 너비
            public int line, name, foot, input;
            public float inputH;
            public float padX, padTop, nameToRule, ruleH, ruleToLine, lineToInput, inputToFoot, footToEdge, footH;

            public float NameH { get { return name + 18f; } }

            /// <summary>대사 자리 높이 — 저쪽 <c>LineBox</c> 와 같은 셈이다(궁서체 줄높이 1.25 + 0.28).</summary>
            public float LineBoxH { get { return Mathf.Ceil(line * (1.25f + 0.28f) * 3f) + 6f; } }

            /// <summary>여백까지 더한 판 높이. 저쪽 <c>TotalHeight</c> 와 같은 셈이다.</summary>
            public float Total
            {
                get
                {
                    return padTop + NameH + nameToRule + ruleH + ruleToLine
                         + LineBoxH + lineToInput + inputH + inputToFoot + footH + footToEdge;
                }
            }
        }

        /// <summary>
        /// <b>PC 와 VR 은 치수가 다르다.</b> 그것을 몰라 여태 VR 값만 넣어 두었고,
        /// 그래서 화면으로 보면 안내줄이 팀원 것(19단위)보다 배 가까이 컸다.
        ///
        /// VR 값이 큰 데는 까닭이 있다 — 저쪽 주석 그대로다:
        /// 「1.5m 앞에서 19단위 안내줄은 <b>0.73도</b>인데 헤드셋 하한이 1.30도다.
        ///  그래서 작은 글씨부터 키웠다: 안내 19→34, 입력 28→34, 이름 32→36, 대사 42→46.」
        ///
        /// 셈을 그대로 돌려 보면 저쪽이 적어 둔 판 높이가 나온다 —
        /// PC 461 · VR 535. 같은 값이 나오면 베낀 것이 맞게 옮겨진 것이다.
        /// </summary>
        private static BarStyle StyleNow()
        {
            if (VRRig.Active)
                return new BarStyle {
                    w = 1500f, line = 46, name = 36, input = 34, foot = 34, inputH = 76f,
                    padX = 60f, padTop = 18f, nameToRule = 12f, ruleH = 3f, ruleToLine = 28f,
                    lineToInput = 40f, inputToFoot = 26f, footToEdge = 24f, footH = 36f };

            return new BarStyle {
                w = 2900f, line = 42, name = 32, input = 28, foot = 19, inputH = 62f,
                padX = 90f, padTop = 14f, nameToRule = 10f, ruleH = 3f, ruleToLine = 24f,
                lineToInput = 34f, inputToFoot = 22f, footToEdge = 19f, footH = 24f };
        }

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

            // ── 치수는 모드에 따라 갈린다 ──────────────
            var st = StyleNow();
            if (_useCommonLook)
            { _lineFontSize = st.line; _nameFontSize = st.name; _hintFontSize = st.foot; }

            float nameH = st.NameH;
            float lineBoxH = st.LineBoxH;
            // <b>입력줄 자리를 비워 둔다.</b> 저쪽 바에는 글쇠 칸이 한 줄 있고, 그 높이가
            // 판 높이 셈에 들어간다. 우리는 그 칸을 <b>심문할 때만</b> 채우지만, 자리는
            // 늘 둔다 — 안 그러면 판 높이가 저쪽과 달라져 「같은 바」가 아니게 된다.
            float h = st.Total;
            float w = st.w;

            var panel = NewRect("바탕", Vector2.zero, new Vector2(w, h), transform);
            panel.gameObject.AddComponent<Image>().color = _panelColor;
            Edge(panel, w, h, pal.border);

            float top = h * 0.5f;
            float y = top - st.padTop;

            // 이름패 — 판 안 왼쪽 위(저쪽과 같은 자리). 나뭇결 위에 주칠.
            float nameW = Mathf.Max(220f, st.name * 7f);
            _nameplate = NewRect("이름판",
                new Vector2(-w * 0.5f + st.padX + nameW * 0.5f, y - nameH * 0.5f),
                new Vector2(nameW, nameH), panel);
            Skin(_nameplate.gameObject.AddComponent<Image>(), _skin.Wood_, _nameplateColor);
            _nameText = NewText("이름", "", Vector2.zero, new Vector2(nameW, nameH),
                                _nameplate, _nameFontSize,
                                _useCommonLook ? UiLook.SealText : _textColor);
            y -= nameH + st.nameToRule;

            var rule = NewRect("구분선", new Vector2(0f, y - st.ruleH * 0.5f),
                               new Vector2(w - st.padX * 2f, st.ruleH), panel);
            Skin(rule.gameObject.AddComponent<Image>(), _skin.Wood_, pal.border);
            y -= st.ruleH + st.ruleToLine;

            _lineText = NewText("대사", "", new Vector2(0f, y - lineBoxH * 0.5f),
                                new Vector2(w - st.padX * 2f, lineBoxH),
                                panel, _lineFontSize, _textColor);
            _lineText.alignment = TextAnchor.UpperLeft;
            _lineText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _lineText.verticalOverflow = VerticalWrapMode.Truncate;
            y -= lineBoxH + st.lineToInput;

            BuildInputRow(panel, w, y, st);
            y -= st.inputH + st.inputToFoot;

            _hintText = NewText("힌트", "", new Vector2(0f, y - st.footH * 0.5f),
                                new Vector2(w - st.padX * 2f, st.footH),
                                panel, _hintFontSize, _hintColor);
            _hintText.alignment = TextAnchor.MiddleCenter;

            BuildCloseTab(panel, w, h, st);
            SitLikeTheBar(w, h);
            AllOnTop();
        }

        /// <summary>
        /// <b>바가 앉는 자리도 저쪽 셈으로 잡는다.</b>
        ///
        /// 치수만 옮기고 자리를 그냥 두었더니 판이 화면 밖으로 넘쳤다 — 2900단위짜리
        /// 바를 눈앞 1.3m 에 세우면 2.9m 폭이 되어 화면을 훌쩍 넘는다.
        ///
        /// 저쪽 셈은 이렇다(주석 그대로): 「1.5m 앞, 세로 화각 60° 기준으로 화면
        /// 반높이가 866단위, 16:9 반너비가 1540단위다. 바를 화면 <b>아래 끝에서
        /// 46단위 띄워</b> 눕히려면 중심 y = −(866 − 높이/2 − 46).」
        /// 너비 2900 은 화면 폭 3080 의 94% — 「거의 전체 폭」이 그 뜻이다.
        ///
        /// 화각을 60으로 못 박지 않고 <b>지금 카메라에서 뽑는다</b> — 60이 아닌 날에도 맞는다.
        /// </summary>
        private float _barH;
        private float _sitFov = -1f;

        /// <summary>
        /// 캔버스 1단위 = 1mm. 저쪽과 같다.
        /// </summary>
        private const float BarScale = 0.001f;

        /// <summary>
        /// <b>화면 반높이를 단위로 적은 것.</b> 1.5m 앞, 세로 화각 60°에서
        /// 1.5 × tan30° ÷ 0.001 = 866 이다. 저쪽이 못 박아 둔 숫자다.
        ///
        /// 이 값을 <b>고정</b>으로 두는 것이 요점이다 — 화각이 어떻든 「화면은 늘
        /// 1732단위 높이」로 치고, 그렇게 되도록 <b>거리를 바꾼다</b>.
        /// 그러면 바가 차지하는 화면 비율이 변하지 않는다.
        /// </summary>
        private const float RefHalfHeight = 866f;

        private void SitLikeTheBar(float w, float h)
        {
            _barH = h;
            _sitFov = -1f;
            // 넓고 아래에 눕는 판이라 <b>화면과 나란히</b> 서야 한다 — 눈을 마주 보게
            // 눕히면 사다리꼴로 일그러진다(재 보니 좌우 귀퉁이가 화면에서 0.04 어긋났다).
            if (_anchor != null) _anchor.SetScreenParallel(true);
            SitNow();
        }

        /// <summary>
        /// <b>바가 앉는 자리를 매 칸 다시 잡는다.</b>
        ///
        /// 여태는 <see cref="Build"/> 에서 <b>한 번만</b> 쟀다. 그 순간의 화각으로 재고
        /// 끝냈으니, 화각이 그대로인 동안에는 맞았다.
        ///
        /// 그런데 이 게임은 <b>심문이 열리면 화각을 좁힌다</b> —
        /// <see cref="ConversationView"/> 가 60°에서 42°로 당긴다(인물에 초점을 준다).
        /// 화각이 좁아지면 화면이 확대되는 것이라, 같은 자리에 선 바가 <b>1.5배로 부푼다</b>.
        /// 재 보니 화면 폭의 94%였던 것이 <b>141%</b>가 되어 양옆이 잘려 나갔다.
        /// 「자막이 너무 크다」와 「인물에 너무 당겨진다」가 <b>같은 하나였다</b>.
        ///
        /// 그래서 화각을 좇는다. 화면 높이를 늘 <see cref="RefHalfHeight"/>×2 단위로 치고
        /// 그렇게 되는 거리에 바를 세우면, 당기든 물러나든 <b>화면에서 차지하는 자리가
        /// 그대로</b>다. 42°에서는 1.5m 가 아니라 2.26m 에 선다.
        ///
        /// ⚠️ 헤드셋에서는 <c>cam.fieldOfView</c> 를 읽으면 안 된다 — HMD 투영이 덮어써
        ///    뜻을 잃는다(저쪽 <c>UiTuning</c> 주석에 같은 경고가 있다). VR은 저쪽처럼
        ///    1.5m 에 못 박고 −16°로 눕힌다.
        /// </summary>
        private void SitNow()
        {
            if (!_useCommonLook || _anchor == null || _barH <= 0f) return;
            var cam = Camera.main;
            if (cam == null) return;

            if (VRRig.Active)
            {
                const float vrDist = 1.5f, vrUpDeg = -16f;
                if (_sitFov > 0f) return;                       // VR은 한 번이면 된다
                _sitFov = 1f;
                _anchor.SetDistance(vrDist, vrDist * Mathf.Tan(vrUpDeg * Mathf.Deg2Rad));
                return;
            }

            float fov = cam.fieldOfView;
            if (Mathf.Abs(fov - _sitFov) < 0.05f) return;       // 안 바뀌었으면 손대지 않는다
            _sitFov = fov;

            float dist = RefHalfHeight * BarScale / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float y = -(RefHalfHeight - _barH * 0.5f - 46f);    // 아래 끝에서 46단위 — 이건 안 변한다
            _anchor.SetDistance(dist, y * BarScale);
        }

        private RectTransform _inputRow;
        private Text _heardText;

        /// <summary>
        /// <b>입력줄</b> — 저쪽 바의 「글쇠 칸 + 단추 셋」 자리다.
        ///
        /// 팀원 PC판을 보면 이 줄에 <c>[친 글] [🎤] [묻 기] [증거 제시]</c> 가 있다.
        /// 우리는 글쇠로 치지 않고 <b>말로 묻는다</b>(Wit.ai). 그래서 칸은 남기되
        /// 쓰임이 바뀐다 — 친 글이 아니라 <b>받아 적힌 말</b>이 여기 뜬다.
        ///
        /// <b>심문할 때만 보인다.</b> 문 앞 대사나 복명 낭독에는 물을 상대가 없다.
        /// 그래도 <b>자리는 늘 잡아 둔다</b> — 판 높이가 저쪽과 같아야 「같은 바」다.
        /// </summary>
        private void BuildInputRow(RectTransform panel, float w, float top, BarStyle st)
        {
            float inner = w - st.padX * 2f;
            _inputRow = NewRect("입력줄", new Vector2(0f, top - st.inputH * 0.5f),
                                new Vector2(inner, st.inputH), panel);

            var pal = IMUNROK.Ui.DialogueUI.Palette();
            float h = st.inputH;

            // 폭 배분도 저쪽 셈 그대로다 (DialogueUI.InputRowBottom):
            //   말하기는 정사각(낮은 바에서 가장 안 튄다), 묻기 10.5%, 증거 제시 16.5%, 사이 16.
            // 「마치 기」 하나만 우리가 더한다 — 아래 주석 참고.
            const float gap = 16f;
            float micW  = h;
            float askW  = Mathf.Max(120f, inner * 0.105f);
            float presW = Mathf.Max(180f, inner * 0.165f);
            float endW  = Mathf.Max(180f, inner * 0.165f);
            float slotW = inner - micW - askW - presW - endW - gap * 4f;

            float x = -inner * 0.5f;

            // 받아 적힌 말이 뜨는 칸 — 저쪽 글쇠 칸 자리다
            var slot = NewRect("받아적힌말", new Vector2(x + slotW * 0.5f, 0f),
                               new Vector2(slotW, h), _inputRow);
            Skin(slot.gameObject.AddComponent<Image>(), _skin.Slot_, pal.slotBack);
            _heardText = NewText("글", "", new Vector2(16f, 0f), new Vector2(slotW - 32f, h),
                                 slot, st.input, pal.slotHint);
            _heardText.alignment = TextAnchor.MiddleLeft;
            x += slotW + gap;

            // ── 말하기 ──
            //
            // 여태 이 자리에는 <b>그림만</b> 있었다. 마이크처럼 생긴 것을 그려 놓고
            // 누를 수는 없게 두었으니, 실제로 말하려면 옛 심문 판의 「눌러서 말하기」를
            // 눌러야 했다 — <b>같은 일이 두 판에 나뉘어</b> 있었다.
            // 저쪽은 이 단추를 「왼쪽 Ctrl 을 누른 것과 똑같이」 친다. 그대로 한다:
            // <b>누르고 있는 동안</b> 듣는다. 한 번 눌러 켜는 방식으로 하면 끄는 것을
            // 잊은 채 돌아다니다 엉뚱한 혼잣말이 인물에게 날아간다.
            _micRt = NewRect("말하기", new Vector2(x + micW * 0.5f, 0f), new Vector2(micW, h), _inputRow);
            _micBg = _micRt.gameObject.AddComponent<Image>();
            Skin(_micBg, _skin.Wood_, UiLook.Wood);
            var micIcon = NewRect("그림", Vector2.zero, new Vector2(h * 0.56f, h * 0.56f), _micRt);
            _micIcon = micIcon.gameObject.AddComponent<Image>();
            Skin(_micIcon, _skin.Mic_, UiLook.Paper);
            PushToTalk(_micRt);
            x += micW + gap;

            // ── 묻 기 ── 저쪽은 이것만 주칠이다. 한 줄에서 <b>지금 할 일</b>이 그것이라서다.
            Chip(_inputRow, "묻기", "묻 기", new Vector2(x + askW * 0.5f, 0f), new Vector2(askW, h),
                 st.input, UiLook.Seal, delegate {
                     var a = InterrogationController.Active;
                     if (a != null) a.AskDraft();
                 });
            x += askW + gap;

            Chip(_inputRow, "증거제시", "증거 제시", new Vector2(x + presW * 0.5f, 0f), new Vector2(presW, h),
                 st.input, UiLook.Wood, delegate {
                     var v = FindFirstObjectByType<JournalView>();
                     if (v != null) JournalPanel.Open(v);
                 });
            x += presW + gap;

            // ── 마치 기 ──
            //
            // <b>저쪽 바에는 없는 단추다.</b> 저쪽은 아랫줄에 「Esc — 대화 끝내기」라
            // 적어 두고 끝이다. 그런데 우리에게는 이미 「이만 마치겠소」가 있고,
            // 그것이 옛 심문 판에 홀로 떠 있었다 — 판을 하나로 모으는 마당에
            // 끝내는 길만 딴 판에 두면 그 판을 못 없앤다.
            //
            // 두 번 물어 끝낸다. 되돌릴 수 없는 일이라 곁에 확인 단추를 하나 더 두는
            // 대신 <b>이 단추가 스스로 되묻는다</b> — 곁에 둔 단추는 무슨 일을 하는지
            // 따로 배워야 하지만, 되묻는 말은 그 자리에서 읽힌다. (옛 판이 쓰던 규약 그대로)
            _endChip = Chip(_inputRow, "마치기", EndWord, new Vector2(x + endW * 0.5f, 0f),
                            new Vector2(endW, h), st.input, UiLook.Wood, OnEndPressed);
            _endLabel = _endChip.GetComponentInChildren<Text>();
            _endBg = _endChip.GetComponent<Image>();

            _inputRow.gameObject.SetActive(false);
        }

        private RectTransform _micRt, _endChip;
        private Image _micBg, _micIcon, _endBg;
        private Text _endLabel;

        private const string EndWord = "이만 마치겠소";
        private const string EndAsk  = "정말 마치겠소?";
        private const float  ConfirmSeconds = 3f;
        private float _confirmLeft;

        /// <summary>
        /// 한 번 누르면 되묻고, 그 사이에 다시 누르면 끝낸다.
        /// </summary>
        private void OnEndPressed()
        {
            var a = InterrogationController.Active;
            if (a == null) return;
            if (_confirmLeft > 0f) { _confirmLeft = 0f; ResetEndLook(); a.FinishFromUi(); return; }
            _confirmLeft = ConfirmSeconds;
            if (_endLabel != null) _endLabel.text = EndAsk;
            if (_endBg != null) _endBg.color = UiLook.Seal;   // 되물을 때만 붉다
        }

        private void ResetEndLook()
        {
            if (_endLabel != null && _endLabel.text != EndWord) _endLabel.text = EndWord;
            if (_endBg != null && _endBg.color != UiLook.Wood) _endBg.color = UiLook.Wood;
        }

        /// <summary>
        /// <b>누르고 있는 동안 듣는다.</b> 마우스든 VR 광선이든 같은 이벤트로 들어온다 —
        /// <c>Button</c> 은 「눌렀다 뗐다」만 알려 주므로 쓰지 않고 눌림·뗌을 직접 받는다.
        /// </summary>
        private void PushToTalk(RectTransform rt)
        {
            var trig = rt.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(delegate { if (MicInput.Instance != null) MicInput.Instance.StartListening(); });
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(delegate { if (MicInput.Instance != null) MicInput.Instance.StopListening(); });
            // 단추 밖에서 손을 떼도 녹음이 안 끊기면 영원히 듣고 있게 된다.
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(delegate {
                if (MicInput.Instance != null && MicInput.Instance.IsListening) MicInput.Instance.StopListening();
            });
            trig.triggers.Add(down);
            trig.triggers.Add(up);
            trig.triggers.Add(exit);

            // <b>판정면을 따로 깔면 안 된다.</b> 한 번 그렇게 했다가 판이 통째로 안 떴다 —
            // <c>Graphic</c> 은 <c>DisallowMultipleComponent</c> 라 이 물건에 이미 붙어
            // 있는 나뭇결 위에 <c>Image</c> 를 하나 더 붙이면 <b>null 이 돌아오고</b>,
            // 거기에 색을 칠하려다 터져서 <b>그 아래로 짓던 것이 전부 없던 일이 된다</b>
            // (안내 줄도, 닫기 딱지도, 바 자리 잡기까지). 오류 한 줄만 나고 화면은
            // 그냥 「입력이 안 되는」 것으로 보인다.
            //
            // 이미 있는 나뭇결이 판정면 노릇을 한다. 광선을 받게만 켜 준다.
            var bg = rt.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = true;
        }

        private RectTransform Chip(RectTransform parent, string name, string label, Vector2 at, Vector2 size,
                                   int fontSize, Color back, System.Action onClick)
        {
            var rt = NewRect(name, at, size, parent);
            var im = rt.gameObject.AddComponent<Image>();
            Skin(im, _skin.Wood_, back);
            NewText("글", label, Vector2.zero, size, rt, fontSize, IMUNROK.Ui.DialogueUI.Palette().text);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = im;
            btn.onClick.AddListener(() => { if (onClick != null) onClick(); });
            return rt;
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
            Skin(rt.gameObject.AddComponent<Image>(), _skin.Wood_, c);
        }

        /// <summary>무늬를 깔고 그 위에 색을 얹는다 — 꾸러미가 판을 그리는 방식이다.</summary>
        private static void Skin(Image im, Sprite sp, Color c)
        {
            im.sprite = sp;
            im.type = Image.Type.Simple;
            im.color = c;
        }

        private void BuildCloseTab(RectTransform panel, float w, float h, BarStyle st)
        {
            // 꾸러미와 같이 <b>판 안</b> 오른쪽 위다. 예전에는 판 밖에 걸터앉아 있어서
            // 어디에 딸린 단추인지 알 수 없었다. 높이는 <b>이름패와 같게</b> 맞춘다 —
            // 저쪽 주석에 「닫기 ×도 같은 높이로 맞춘다」고 적혀 있다.
            var size = new Vector2(st.NameH * 2.2f, st.NameH);
            var rt = NewRect("닫기",
                new Vector2(w * 0.5f - st.padX - size.x * 0.5f, h * 0.5f - st.padTop - size.y * 0.5f),
                size, panel);
            // 이 딱지 색도 손으로 정하지 않는다. 꾸러미의 <b>글쇠 칸</b> 색을 쓴다 —
            // 저쪽에서 「눌러도 되는 자리」를 알리는 데 쓰는 색이라 뜻이 맞는다.
            Skin(rt.gameObject.AddComponent<Image>(), _skin.Slot_, IMUNROK.Ui.DialogueUI.Palette().slotBack);
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
