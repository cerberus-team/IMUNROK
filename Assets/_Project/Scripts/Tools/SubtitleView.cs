using System.Collections.Generic;
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
        [SerializeField] private Color _panelColor = UiLook.Panel;
        [SerializeField] private Color _nameplateColor = UiLook.Seal;
        [SerializeField] private Color _textColor = UiLook.Text;
        [SerializeField] private Color _hintColor = UiLook.Dim;
        [Tooltip("새로 알아낸 것을 말할 때의 글빛. 수첩에 안 적히는 말이라 여기서 한 번 눈에 박혀야 한다")]
        [SerializeField] private Color _keyColor = UiLook.Lit(UiLook.Seal, 0.35f);

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
        /// <summary>바탕 판. 화면 아래에 눕히는 자리를 여기에 준다.</summary>
        private RectTransform _panel;
        private Text _nameText, _lineText, _hintText;
        private RectTransform _nameplate;
        private GameObject _closeTab;

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
                        var go = new GameObject("자막", typeof(Canvas),
                                                typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            // <b>이제 아무 일도 하지 않는다.</b> 판이 화면에 붙었으므로 「눈에서 몇 m」가
            // 없다. 부르는 자리(심문판·도구 익히기)를 다 고치는 대신 여기서 받아만 두는
            // 까닭은, 저 자리들이 <b>무엇을 바라는지</b>가 이름에 남아 있어서다 —
            // 「읽기 좋은 자리에 두어라」. 화면에서는 그 자리가 늘 같으므로 시킬 것이 없다.
            _wantDistance = distance; _wantDrop = verticalOffset; _hasWantDistance = true;
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
            // 화면에 붙은 판은 <b>늘 붙박여 있다</b>. 시킬 것이 없어졌다(위 참조).
            _wantPinned = on;
        }

        /// <summary>지금 자막이 떠 있는가(다른 UI가 겹치지 않게 참고).</summary>
        /// <summary>
        /// 이 인물보다 앞에 서게 한다. 바짝 붙어도 상대 몸이 자막을 덮지 않는다.
        /// null 을 넣으면 원래 읽는 거리로 돌아간다.
        /// </summary>
        public static void KeepInFrontOf(Transform target)
        {
            // 화면에 붙은 판은 <b>상대 몸에 가릴 수가 없다</b> — 세상보다 앞에 그려진다.
            // 이 부탁이 막으려던 일 자체가 없어졌다.
        }

        public static bool IsShowing => _instance != null && _instance._group != null && _instance._group.alpha > 0.5f;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            SitOnScreen();
            Build();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// <b>몇 번째 말인가.</b> 한 마디 띄울 때마다 하나씩 오른다.
        ///
        /// 자막판은 하나인데 <b>쓰는 사람이 여럿</b>이다 — 조사청 안내, 도구 익히기,
        /// 심문, 사건표. 그중에는 「몇 초 뒤에 내 말을 지워라」고 <b>미리 걸어 두는</b>
        /// 것이 있는데, 그 사이에 다른 이가 제 말을 띄우면 그 예약이 <b>남의 말을 지운다</b>.
        ///
        /// 실제로 그랬다: 조사청에 들어서면 「조사청이오」가 4.5초짜리 지우기를 걸어 두는데,
        /// 그 안에 돋보기를 누르면 익히기 첫 마디가 그 지우기에 맞아 사라졌다. 글은
        /// 판에 적혀 있는데 판이 안 보이니, 눌러도 아무 일이 없는 것으로 읽힌다.
        ///
        /// 그래서 띄울 때 번호를 받아 두고, 지울 때 <b>그 번호가 그대로인지</b> 본다.
        /// </summary>
        public static int Generation { get; private set; }

        /// <summary>내가 띄운 말이 아직 그대로면 지운다. 아니면 손대지 않는다.</summary>
        public static void HideIfUnchanged(int generation)
        {
            if (generation != Generation) return;
            Hide();
        }

        private void ShowInternal(string speaker, string line, string hint, bool key = false)
        {
            Generation++;
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
            // 딱지는 통째로 여닫는다. 콜라이더를 따로 여닫던 시절에는 그것만 꺼 두면
            // <b>글씨가 그대로 남아</b> 눌리지도 않는 「✕」가 화면에 붙어 있었다
            // (엔딩에서 실제로 그렇게 됐다). 이제 UI 단추 하나라 그럴 일이 없다.
            if (_closeTab != null) _closeTab.SetActive(on && Closable);
            if (was && !on && byUser) OnClosed?.Invoke();
        }

        // <b>눈각 맞추기(FitToEye)를 걷어냈다.</b> 1.30도 하한은 「눈에서 몇 도로
        // 보이나」를 따지는 헤드셋의 규칙인데, 모니터에서는 그 물음이 성립하지 않는다 —
        // 화면이 곧 시야라 몇 미터 앞에 앉느냐로 정해지지 우리가 정할 수가 없다.
        // 여태 모니터에서도 이걸 돌려서 안내 줄 19가 35로 부풀어 있었다.
        // 이제 <see cref="StyleNow"/> 가 적어 둔 값이 그대로 그려진다.
        private void Update()
        {
            if (_group == null || _group.alpha < 0.5f) return;
            PaintHeard();
            TickConfirm();
#if ENABLE_INPUT_SYSTEM
            // 옛 Input 클래스를 쓰면 안 된다. 이 프로젝트는 입력을 Input System 으로
            // 넘겨 놓아서, 저것을 읽는 순간 예외가 난다 — 자막이 떠 있는 내내 매 프레임
            // 터졌고, 그래서 Esc 로 자막을 닫는 곁길이 여태 한 번도 듣지 않았다.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            // <b>치는 동안에는 Backspace 를 먹지 않는다.</b> 여기가 자막을 닫는 자리인데,
            // 글을 고치려고 한 글자 지운 순간 판이 통째로 닫히면 무슨 일인지 알 수가 없다.
            if (kb.escapeKey.wasPressedThisFrame
                || (kb.backspaceKey.wasPressedThisFrame && !Typing.Now))
                SetVisible(false, true);        // 이것도 사람이 닫은 것이다

            // ── Enter — 묻기 ──
            //
            // 저쪽 대화창이 Enter 로 던진다. 우리는 여태 <b>던지는 절차 자체가 없었다</b>.
            //
            // ⚠️ <c>InputField.onSubmit</c> 을 쓰지 않는다. 칸이 선택을 잃을 때도 함께
            //    울리는 자리라, 늘 잡아 두는 규칙과 부딪힌다. 글쇠를 바로 보면 규칙이
            //    하나로 단순해진다 (저쪽이 같은 까닭으로 같은 선택을 했다).
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) AskNow();
#endif
        }

        /// <summary>
        /// 입력줄 칸에 <b>받아 적힌 말</b>을 비춘다.
        ///
        /// 세 가지가 번갈아 온다 — 지금 받아 적히는 중이면 그 글이 또렷하게, 던지고 나서
        /// 비었으면 <b>방금 던진 말이 묽게</b>, 그것마저 없으면 무엇을 누르라는 안내가 온다.
        /// 던진 말을 지워 버리면 무엇으로 전해졌는지 확인할 데가 없어진다.
        /// </summary>
        /// <summary>되물은 뒤 이만큼 지나면 없던 일로 한다 — 잘못 눌렀을 때 그냥 두면 된다.</summary>
        private void TickConfirm()
        {
            if (_confirmLeft <= 0f) return;
            _confirmLeft -= Time.unscaledDeltaTime;
            if (_confirmLeft <= 0f) { _confirmLeft = 0f; ResetEndLook(); }
        }

        private void PaintHeard()
        {
            if (_field == null) return;
            var a = InterrogationController.Active;

            // ── 받아 적힌 말이 새로 오면 칸에 얹는다 ──
            //
            // <b>매 칸 덮어쓰면 안 된다.</b> 그러면 치는 족족 지워진다.
            // 말이 <b>바뀐 그때</b>만 얹고, 그 뒤로는 손이 임자다 —
            // 잘못 알아들었으면 고쳐 칠 수 있어야 한다(저쪽 규약).
            string draft = a != null ? a.Draft : "";
            if (draft != _draftSeen)
            {
                _draftSeen = draft;
                if (!string.IsNullOrEmpty(draft)) _field.text = draft;
            }

            if (_heardHint != null)
            {
                string want = Controls.AskPrompt;
                if (_heardHint.text != want) _heardHint.text = want;
            }

            // 칸은 늘 칠 수 있다. 헤드셋을 쓰던 때는 여기서 못 치게 막았다 —
            // 쥘 손이 없어서였다. 이제 막을 까닭이 없다.
            if (!_field.interactable) _field.interactable = true;
            if (_field.readOnly) _field.readOnly = false;

            // ── 칸을 늘 잡아 둔다 ──
            //
            // 빈 곳을 한 번 누르면 선택이 풀린다. 그러면 치던 사람이 <b>아무 일도
            // 안 일어나는 것</b>을 겪는다. 심문하는 동안에는 늘 잡혀 있어야 한다.
            // (저쪽도 같은 까닭으로 같은 일을 한다)
            if (_inputRow != null && _inputRow.gameObject.activeSelf
                && !_field.isFocused && EventSystem.current != null)
                _field.ActivateInputField();
        }

        /// <summary>
        /// 칸에 있는 글을 던진다. <b>칸이 임자다</b> — 받아 적힌 말이든 손으로 친 말이든
        /// 지금 칸에 보이는 그것이 전해진다. 눈에 보이는 것과 전해지는 것이 달라서는 안 된다.
        /// </summary>
        private void AskNow()
        {
            var a = InterrogationController.Active;
            if (a == null || _field == null) return;
            string say = _field.text;
            if (string.IsNullOrWhiteSpace(say)) return;
            _field.text = "";
            _draftSeen = "";
            a.SetDraft(say);
            a.AskDraft();
            _field.ActivateInputField();
        }

        /// <summary>
        /// 이 판은 <b>깊이를 따지지 않고</b> 그린다.
        ///
        /// 자막은 눈에서 1.3m 앞에 선다. 그런데 조사청에서는 문갑 앞에 서면 창이며
        /// 기둥이 그보다 가까워서, 자막이 <b>창 뒤로 들어가</b> 살에 잘려 읽히지 않았다.
        /// 이것은 방에 놓인 물건이 아니라 <b>눈앞에 든 글</b>이므로 무엇에도 가리면
        /// 안 된다. 문서의 어둠판이 이미 같은 까닭으로 같은 일을 한다.
        /// </summary>

        /// <summary>
        /// <b>꾸러미 하단바의 치수 한 벌.</b> 이름과 뜻을 저쪽 <c>BottomStyle</c> 에서 그대로 가져왔다.
        ///
        /// <b>이제 베끼지 않는다.</b> 색을 쥔 <c>Palette()</c> 도, 치수를 쥔 <c>StyleOf</c> 도
        ///   저쪽에 물어 온다. 이 구조체는 그 답을 담는 그릇으로만 남는다 —
        ///   저쪽 <c>BottomStyle</c> 과 칸 이름이 달라 그대로 쓸 수가 없어서다.
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
        ///
        /// <b>다만 대사와 이름은 키웠다</b> — 대사 42→56, 이름 32→56. 베낀 값을 그대로
        /// 두었더니 화면에서 대사가 <b>화면 높이의 2.4%</b>(42÷1732)밖에 안 돼, 판은
        /// 널찍한데 글씨만 작아 보였다. 56 이면 3.2% 다. 이름패도 대사와 같은 크기로
        /// 두어, 누가 말하는지가 그 말과 같은 무게로 읽히게 한다
        /// (이름패 너비는 <c>st.name * 7</c> 이라 220 에서 392 로 따라 넓어진다).
        ///
        /// 그러면 판 높이가 따라 늘어 <b>461 이 아니라 550</b> 이 된다 — 위의 「PC 461」
        /// 은 이제 맞춰 볼 수 없는 수다. 베낀 것이 맞는지 재던 잣대를 잃는 셈이지만,
        /// 읽히지 않는 자막보다는 낫다. 나머지 치수(이름 32 · 입력 28 · 안내 19)는
        /// 저쪽 그대로 두었으니, 어긋난 것은 대사와 이름 둘뿐임을 여기 적어 둔다.
        /// </summary>
        private static BarStyle StyleNow()
        {
            // <b>이제 저쪽에 물어본다.</b> 위 주석이 「그것이 열리면 이 구조체를 지우고
            // 그쪽을 부르면 된다 — 팀원께 열어 달라고 청해 둘 것」이라 적어 둔 그 자리다.
            // 청할 것도 없었다: 견우 사건 코드 쪽 사본에는 이미 열려 있었고 꾸러미만
            // 안 따라와 있었다. 꾸러미를 열어 두 벌을 같은 데로 모았다.
            var st = IMUNROK.Ui.DialogueUI.StyleOf(IMUNROK.Ui.DialogueLayout.하단바_확정);

            return new BarStyle {
                w = 2900f,                       // 저쪽 BarGeom 이 이 배치에 주는 폭
                // <b>대사와 이름만 우리 값이다.</b> 어긋난 자리는 이 둘뿐이고, 그 까닭은
                // 위에 적어 두었다. 나머지는 한 자도 안 적는다 — 저쪽이 고치면 따라온다.
                line = 56, name = 56,
                input = st.inputSize, foot = st.footSize, inputH = st.inputH,
                padX = st.padX, padTop = st.padTop, nameToRule = st.nameToRule,
                ruleH = st.ruleH, ruleToLine = st.ruleToLine,
                lineToInput = st.lineToInput, inputToFoot = st.inputToFoot,
                footToEdge = st.footToEdge, footH = st.footH };
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
                // <b>먹빛은 저쪽 것을 쓰되 진하기만 올린다.</b>
                //
                // 꾸러미 바는 알파 0.65 다. 저쪽 화면에서는 그것이 맞는데, 어전은
                // <b>밝은 전돌바닥</b> 위에 바가 눕는다 — 0.65 면 돌 무늬가 글씨 사이로
                // 그대로 올라와 어디까지가 판이고 어디부터가 바닥인지 알 수 없다.
                // 빛깔은 저쪽 것 그대로 두고 진하기만 올린다. 색을 한 벌로 모은 것은
                // 지키면서 읽히기는 하는 자리를 찾는 셈이다.
                _panelColor = UiLook.With(pal.back, 0.88f);
                _textColor = pal.text;
                _hintColor = pal.dim;
                _nameplateColor = UiLook.Seal;   // 주칠
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
            _panel = panel;
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
            // <b>앞에 그리라는 손질은 더 안 한다.</b> 화면에 붙인 판은 세상보다 뒤에
            // 그려질 수가 없다. 재질을 스물두 장 복제해 깊이 검사를 끄던 일도,
            // 그 값이 도로 0 으로 돌아가는지 매 칸 살피던 일도 함께 없어졌다.
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

        /// <summary>
        /// 캔버스 1단위 = 1mm. 저쪽과 같다.
        /// </summary>
        private const float BarScale = 0.001f;   // 월드에 세우던 시절의 값. 셈의 내력으로 남긴다

        /// <summary>
        /// <b>화면 반높이를 단위로 적은 것.</b> 1.5m 앞, 세로 화각 60°에서
        /// 1.5 × tan30° ÷ 0.001 = 866 이다. 저쪽이 못 박아 둔 숫자다.
        ///
        /// 이 값을 <b>고정</b>으로 두는 것이 요점이다 — 화각이 어떻든 「화면은 늘
        /// 1732단위 높이」로 치고, 그렇게 되도록 <b>거리를 바꾼다</b>.
        /// 그러면 바가 차지하는 화면 비율이 변하지 않는다.
        /// </summary>
        private const float RefHalfHeight = 866f;

        /// <summary>
        /// <b>자막판을 월드에서 떼어 화면에 붙인다.</b>
        ///
        /// 이 판은 헤드셋 시절부터 눈앞 허공에 세워 두고 고개를 따라오게 했다.
        /// 그러느라 치른 값이 셋이다 —
        ///
        ///   · <b>바닥 밑으로 사라진다.</b> 판이 눈높이보다 아래 눕는데 어전에서는
        ///     그 자리가 바닥 밑이라, 깊이 검사를 끄지 않으면 전돌에 통째로 잠겼다.
        ///     그 끈 값이 유니티에 도로 되돌려지는 함정까지 딸려 있었다.
        ///   · <b>화각을 좇아야 했다.</b> 심문이 60°에서 42°로 당기면 같은 자리의 바가
        ///     1.5배로 부풀어 양옆이 잘렸다. 그래서 매 칸 거리를 다시 재고 있었다.
        ///   · <b>떠다닌다.</b> 고개를 돌리면 뒤따라 흔들린다.
        ///
        /// 셋 다 「화면에 적힌 말을 세상 속에 세워 둔」 데서 나온 것이고, 화면에 붙이면
        /// 한꺼번에 없어진다. 헤드셋이 없는 지금 세상에 세워 둘 까닭도 없다.
        ///
        /// 치수는 그대로다. 이 판의 셈은 애초에 <b>화면 반높이를 866단위로 치는</b>
        /// 것이었으므로(<see cref="RefHalfHeight"/>), 그 값을 기준 해상도로 넘기기만
        /// 하면 폭 2900(=94%)도 아래 여백 46도 뜻을 지킨다.
        /// </summary>
        private void SitOnScreen()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;           // 수첩(200)보다 아래. 수첩을 펴면 자막이 가린다

            var old = GetComponent<WorldHudAnchor>();
            if (old != null) Destroy(old);

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            float refH = RefHalfHeight * 2f;     // 1732 — 이 판의 셈이 늘 치던 화면 높이
            scaler.referenceResolution = new Vector2(refH * 16f / 9f, refH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;      // 세로로 맞춘다

            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        }

        private void SitLikeTheBar(float w, float h)
        {
            _barH = h;
            // <b>화면 아래 끝에서 46단위 띄워 눕힌다.</b> 저쪽 셈 그대로다 —
            // 화면 반높이를 866단위로 치므로, 중심 y = −(866 − 높이/2 − 46).
            //
            // 여태 이 값을 <b>거리</b>로 풀었다. 월드에 세운 판이라 화각이 바뀌면
            // 화면에서 차지하는 몫이 달라져서, 화각을 좇아 판을 앞뒤로 옮겨야
            // 그 몫이 지켜졌다(심문이 열리면 60°에서 42°로 당긴다). 화면에 붙인
            // 뒤로는 그럴 일이 없다 — 화각은 화면 UI 를 건드리지 않는다.
            if (_panel != null)
                _panel.anchoredPosition = new Vector2(0f, -(RefHalfHeight - h * 0.5f - 46f));
        }

        // <b>SitNow 를 걷었다.</b> 화각을 좇아 바를 앞뒤로 옮기던 자리다 —
        // 심문이 60°에서 42°로 당기면 같은 자리의 바가 1.5배로 부풀어(폭 94% → 141%)
        // 양옆이 잘려 나갔고, 그것을 거리로 되받고 있었다. 화면에 붙인 판은 화각이
        // 어떻든 차지하는 몫이 그대로라, 좇을 것이 없어졌다.


        private RectTransform _inputRow;
        private Text _heardText, _heardHint;
        private InputField _field;
        private string _draftSeen = "";

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
            //   묻기 10.5%, 증거 제시 16.5%, 사이 16.
            // 「마치 기」 하나만 우리가 더한다 — 아래 주석 참고.
            //
            // <b>「말하기」 칸이 여기 있었다.</b> 정사각으로 한 자리를 차지했는데,
            // 말로 묻는 길을 걷어냈으므로 그 자리를 <b>글쇠 칸에 준다</b> — 이제 묻는
            // 길이 치는 것 하나뿐이니 칠 자리가 넓은 편이 맞다.
            const float gap = 16f;
            float askW  = Mathf.Max(120f, inner * 0.105f);
            float presW = Mathf.Max(180f, inner * 0.165f);
            float endW  = Mathf.Max(180f, inner * 0.165f);
            float slotW = inner - askW - presW - endW - gap * 3f;

            float x = -inner * 0.5f;

            // ── 글쇠 칸 ──
            //
            // 여태 여기는 <b>글씨판</b>이었다. 받아 적힌 말을 비추기만 하고 칠 수는 없었다.
            // 그래서 마이크가 안 잡히는 자리에서는 <b>물을 방법이 아예 없었다</b> —
            // 영상을 찍으려면 손으로 칠 수 있어야 한다.
            //
            // 저쪽은 처음부터 진짜 칸이었다. 저쪽 주석이 왜 그런지도 적어 뒀다:
            // 「한글 IME 조합을 UGUI가 대신 처리해 준다. 직접 글쇠를 읽는 방식으로
            //  바꾸면 한글을 못 치게 된다.」 그래서 우리도 칸을 쓴다.
            var slot = NewRect("글쇠칸", new Vector2(x + slotW * 0.5f, 0f),
                               new Vector2(slotW, h), _inputRow);
            var slotBg = slot.gameObject.AddComponent<Image>();
            Skin(slotBg, _skin.Slot_, pal.slotBack);

            _heardText = NewText("글", "", new Vector2(16f, 0f), new Vector2(slotW - 32f, h),
                                 slot, st.input, pal.slotText);
            _heardText.alignment = TextAnchor.MiddleLeft;
            _heardText.supportRichText = false;   // 조합 중인 한글에 태그가 새지 않게

            _heardHint = NewText("안내글", "", new Vector2(16f, 0f), new Vector2(slotW - 32f, h),
                                 slot, st.input, pal.slotHint);
            _heardHint.alignment = TextAnchor.MiddleLeft;

            _field = slot.gameObject.AddComponent<InputField>();
            _field.textComponent = _heardText;
            _field.placeholder = _heardHint;
            _field.lineType = InputField.LineType.SingleLine;
            _field.characterLimit = 120;
            _field.customCaretColor = true;
            _field.caretColor = pal.slotText;     // 어두운 칸에서는 커서도 밝아야 보인다
            _field.selectionColor = UiLook.With(UiLook.Seal, 0.35f);
            _field.targetGraphic = slotBg;
            _field.transition = Selectable.Transition.None;
            x += slotW + gap;

            // ── 묻 기 ── 저쪽은 이것만 주칠이다. 한 줄에서 <b>지금 할 일</b>이 그것이라서다.
            Chip(_inputRow, "묻기", "묻 기", new Vector2(x + askW * 0.5f, 0f), new Vector2(askW, h),
                 st.input, UiLook.Seal, AskNow);
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

        private RectTransform _endChip;
        private Image _endBg;
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

        private RectTransform Chip(RectTransform parent, string name, string label, Vector2 at, Vector2 size,
                                   int fontSize, Color back, System.Action onClick)
        {
            var rt = NewRect(name, at, size, parent);
            var im = rt.gameObject.AddComponent<Image>();
            Skin(im, _skin.Wood_, back);
            NewText("글", label, Vector2.zero, size, rt, fontSize, UiLook.Text);
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
            Skin(rt.gameObject.AddComponent<Image>(), _skin.Slot_, UiLook.Slot);
            NewText("글", "✕", Vector2.zero, size, rt, _hintFontSize, _hintColor);

            // <b>UI 단추로 받는다.</b> 여태 이 딱지는 <see cref="NoticeCloseTab"/> 이었다 —
            // 콜라이더를 달고 세상의 광선(<see cref="MouseRaySelector"/>)에 짚히는 물건.
            // 판이 월드에 있을 때는 그것이 맞았지만, 화면에 붙인 뒤로는 콜라이더가
            // <b>화면 픽셀 좌표를 세계 좌표로 들고</b> 엉뚱한 데 서 있게 된다.
            // 화면에 그려지는 것은 화면의 손으로 받아야 한다.
            var closeBtn = rt.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = rt.GetComponent<Image>();
            closeBtn.onClick.AddListener(() => SetVisible(false, true));
            _closeTab = rt.gameObject;
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
