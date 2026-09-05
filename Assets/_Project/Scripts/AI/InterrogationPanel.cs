using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 심문의 조작부를 월드 공간 Canvas로 그린다. 대사 자체는 <see cref="SubtitleView"/>가
    /// 맡고, 여기는 "누를 것"만 담당한다.
    ///
    /// OnGUI로는 헤드셋에 아무것도 안 뜨므로, 이게 없으면 VR에서 심문을 시작하거나
    /// 끝낼 방법이 없다.
    ///
    /// <b>네 줄로 되어 있다</b>(위에서 아래로):
    ///   ① 마이크 · 마치기 — 늘 있는 것
    ///   ② <b>물음</b>   — 추천 질문. 한 번에 셋까지만(<see cref="InterrogationController.Topics"/>)
    ///   ③ <b>명령</b>   — 묻는 것이 아니라 시키는 것(<see cref="InterrogationOrder"/>).
    ///                     근거가 없으면 줄 자체가 안 뜬다
    ///   ④ <b>사람</b>   — 동헌의 부르기 판(<see cref="InterrogationBench"/>). 이름을 누르면
    ///                     있던 이가 물러나고 그 사람이 선다
    ///
    /// <b>아무도 안 불렀을 때</b>는 ④번 줄만 뜬다(<see cref="OpenRoster"/>). 마루에 앉아
    /// 뜰을 내려다보는데 마이크며 마치기가 먼저 떠 있으면 누구에게 하는 말인지 알 수 없다.
    ///
    /// 씬에 미리 둘 필요 없다 — 심문이 시작될 때 스스로 만든다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class InterrogationPanel : MonoBehaviour
    {
        [SerializeField] private Font _font;
        [SerializeField] private int _fontSize = 30;
        [Tooltip("켜면 색을 <b>꾸러미(IMUNROK.Ui)</b> 에서 받아 온다 — 자막 바·수첩과 한 결이 된다")]
        [SerializeField] private bool _useCommonLook = true;
        [SerializeField] private Color _chipColor = UiLook.With(UiLook.Slot, 0.85f);
        [SerializeField] private Color _micColor = UiLook.With(UiLook.Slot, 0.90f);
        [SerializeField] private Color _micOnColor = UiLook.With(UiLook.Seal, 0.95f);
        [Tooltip("되돌릴 수 없는 '마치기' 버튼")]
        [SerializeField] private Color _endColor = UiLook.With(UiLook.WoodLit, 0.95f);
        [Tooltip("되물을 때의 색. 한 번 더 눌러야 끝난다는 것이 색으로도 보여야 한다")]
        [SerializeField] private Color _confirmColor = UiLook.With(UiLook.Seal, 0.97f);

        [Tooltip("명령 단추. 묻는 것과 <b>색으로 갈라 둔다</b> — 이 줄을 누르면 대답이 아니라 일이 벌어진다")]
        [SerializeField] private Color _orderColor = UiLook.With(UiLook.Seal, 0.95f);

        [Tooltip("부를 사람 이름표")]
        [SerializeField] private Color _seatColor = UiLook.With(UiLook.Slot, 0.88f);
        [Tooltip("지금 불려 나와 있는 사람")]
        [SerializeField] private Color _seatUpColor = UiLook.With(UiLook.WoodLit, 0.95f);
        [Tooltip("아직 안 온 사람 — 자리는 있으나 사람이 없다")]
        [SerializeField] private Color _seatEmptyColor = UiLook.With(UiLook.Panel, 0.55f);

        [SerializeField] private Color _textColor = UiLook.Text;

        /// <summary>꾸러미의 결. 정적으로 들면 판을 넘겨 살아남되 내용이 죽는다 — 판마다 제 것.</summary>
        private readonly IMUNROK.Ui.InventorySkin _skin = new IMUNROK.Ui.InventorySkin();

        private static InterrogationPanel _instance;

        private InterrogationController _owner;
        private CanvasGroup _group;
        private WorldHudAnchor _anchor;
        private Image _micBg;
        private Text _micLabel;
        private RectTransform _micRt;
        private readonly List<GameObject> _chips = new List<GameObject>();
        private readonly List<GameObject> _orderChips = new List<GameObject>();
        private readonly List<GameObject> _seatChips = new List<GameObject>();
        private RectTransform _chipRow;
        private RectTransform _orderRow;
        private RectTransform _seatRow;
        private RectTransform _endRt;
        private Image _endBg;
        private Text _endLabel;
        private float _confirmLeft;      // 되물은 뒤 남은 시간

        /// <summary>아무도 안 불렀을 때 — 이름표 줄만 뜬 상태.</summary>
        private bool _rosterOnly;

        private const string EndWord = "이만 마치겠소";
        private const string AskWord = "정말 마치겠소?";

        /// <summary>되묻고 이만큼(초) 안에 다시 눌러야 끝난다. 지나면 원래대로 돌아간다.</summary>
        private const float ConfirmSeconds = 3f;

        /// <summary>
        /// 한 번 누르면 되묻고, 그 사이에 다시 누르면 끝낸다.
        ///
        /// 버튼을 둘로 나누는 대신 이 버튼이 두 번 묻는다. 되돌릴 수 없는 일을 막는 데는
        /// 이편이 낫다 — 곁에 둔 버튼은 무슨 일을 하는지 따로 배워야 하지만, 되묻는 말은
        /// 그 자리에서 읽힌다.
        /// </summary>
        private void OnEndPressed()
        {
            if (_confirmLeft > 0f) { _confirmLeft = 0f; ResetEndLook(); _owner?.FinishFromUi(); return; }
            _confirmLeft = ConfirmSeconds;
            if (_endLabel != null) _endLabel.text = AskWord;
            if (_endBg != null) _endBg.color = _confirmColor;
        }

        private void ResetEndLook()
        {
            if (_endLabel != null) _endLabel.text = EndWord;
            if (_endBg != null) _endBg.color = _endColor;
        }
        private RawImage _evidence;
        private float _evidenceTimer;

        /// <summary>심문이 시작될 때 호출 — 없으면 만들고, 이 심문에 맞춰 버튼을 다시 만든다.</summary>
        public static void Open(InterrogationController owner)
        {
            Ensure();
            _instance.OpenInternal(owner);
        }

        /// <summary>
        /// <b>이름표 줄만 띄운다</b> — 아직 아무도 안 불렀을 때.
        ///
        /// 동헌에 들어서면 부르기 판(<see cref="InterrogationBench"/>)이 이걸 부른다.
        /// 마이크도 마치기도 안 뜬다 — 마주한 사람이 없는데 "이만 마치겠소" 가 떠 있으면
        /// 무엇을 마치겠다는 것인지 알 수 없다.
        /// </summary>
        public static void OpenRoster()
        {
            if (InterrogationBench.Instance == null) return;
            Ensure();
            // <b>이미 떠 있으면 그대로 둔다.</b> 부르기 판이 매 프레임 이걸 부르는데,
            // 그때마다 다시 지으면 단추를 초당 예순 번 부수고 새로 만든다 — 눌리지도 않고
            // 값만 잔뜩 든다. 자리 상태가 바뀌면 Refresh 가 고쳐 그린다.
            if (_instance._rosterOnly && _instance._group != null && _instance._group.alpha > 0f) return;
            _instance.OpenRosterInternal();
        }

        /// <summary>지금 떠 있는 판을 다시 그린다(질문을 하나 물었거나 명령이 사라졌을 때).</summary>
        public static void Refresh()
        {
            if (_instance == null || _instance._group == null || _instance._group.alpha <= 0f) return;
            if (_instance._rosterOnly) _instance.BuildSeats();
            else
            {
                _instance.RebuildChips(_instance._owner != null ? _instance._owner.Topics : null);
                _instance.BuildOrders();
                _instance.BuildSeats();
            }
        }

        private static void Ensure()
        {
            if (_instance != null) return;
            _instance = FindFirstObjectByType<InterrogationPanel>();
            if (_instance != null) return;
            var go = new GameObject("VR_심문조작", typeof(Canvas));
            // 자막(-0.28)보다 아래 — 대사를 가리지 않게
            go.AddComponent<WorldHudAnchor>().Configure(WorldHudAnchor.Placement.Front);
            _instance = go.AddComponent<InterrogationPanel>();
        }

        /// <summary>제시한 증거 그림을 잠깐 띄운다("탁" 들이미는 연출).</summary>
        public static void ShowEvidence(Texture2D tex, float seconds)
        {
            if (_instance == null || tex == null) return;
            _instance._evidence.texture = tex;
            _instance._evidenceTimer = seconds;
        }

        /// <summary>이 인물보다 앞에 서게 한다(상대 몸에 가리지 않게).</summary>
        public static void KeepInFrontOf(Transform target)
        {
            var p = _instance;
            if (p != null && p._anchor != null) p._anchor.KeepInFrontOf(target);
        }

        /// <summary>심문이 끝날 때 호출.</summary>
        public static void Close()
        {
            if (_instance != null) _instance.SetVisible(false);
        }

        /// <summary>지금 이름표 줄만 떠 있나.</summary>
        public static bool RosterShowing =>
            _instance != null && _instance._rosterOnly && _instance._group != null && _instance._group.alpha > 0f;

        /// <summary>
        /// 이름표 줄을 내린다 — 동헌에서 걸어 나갔을 때.
        ///
        /// <b>내리는 쪽이 없어서 판이 따라다녔다.</b> 띄우는 일만 있고 내리는 일이 없으니,
        /// 마루에서 내려와 문서고까지 걸어가도 부를 사람 이름표가 발밑에 그대로 붙어
        /// 있었다 — 서가 앞에서 甲을 부를 수 있는 것처럼 보인다.
        /// 심문 중인 판은 건드리지 않는다. 그건 심문이 제 손으로 닫는다.
        /// </summary>
        public static void CloseRoster()
        {
            if (RosterShowing) _instance.SetVisible(false);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _anchor = GetComponent<WorldHudAnchor>();
            if (_anchor == null) _anchor = gameObject.AddComponent<WorldHudAnchor>();
            ApplyPlacement();

            _font = UiFont.Resolve(_font);
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            AdoptCommonLook();
            BuildFixedParts();
            SetVisible(false);
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        /// <summary>
        /// 판과 자막이 서로를 밟지 않게 자리를 잡는다.
        ///
        /// <b>왜 두 곳을 한 자리에서 정하나.</b> 부르기 판이 있는 씬에서는 줄이 넷이라
        /// 판이 아래로 0.36m 길어진다. 그대로 두면 <b>맨 아랫줄(사람 이름표)이 화면
        /// 밖으로 나간다</b> — 시야 반각이 30° 인데 이름표가 30.2° 에 놓여, 부를 사람을
        /// 고르라는 판이 정작 안 보였다.
        ///
        /// 그래서 판을 올린다. 그런데 올리면 이번엔 <b>자막을 밟는다</b>. 자막은 여태
        /// 눈 아래 0.28m 에 있었는데, 그 자리는 판이 두 줄이던 시절에 잡은 자리다.
        /// 판이 길어졌으면 자막도 함께 올라가야 한다 — 둘 중 하나만 고치면 반드시
        /// 다른 하나가 밀린다.
        /// </summary>
        private void ApplyPlacement()
        {
            bool tall = InterrogationBench.Instance != null;     // 사람 줄이 붙는 씬인가
            _anchor.SetDistance(1.3f, tall ? -0.42f : -0.55f);
            SubtitleView.SetReadingDistance(1.3f, tall ? -0.13f : -0.28f);
        }

        private void OpenInternal(InterrogationController owner)
        {
            bool wasRoster = _rosterOnly || _group.alpha <= 0f;
            _owner = owner;
            _rosterOnly = false;
            _confirmLeft = 0f;
            ResetEndLook();
            ApplyPlacement();
            // <b>말하기와 마치기는 이제 자막 바에 있다</b> (2026-08-27).
            //
            // 꾸러미 하단바는 「글쇠 칸 · 말하기 · 묻기 · 증거 제시」를 <b>한 줄</b>에 둔다.
            // 우리는 그 줄을 그대로 지어 놓고도 말하기는 이 판에, 묻기는 저 바에 두어
            // <b>같은 일이 두 판에 나뉘어</b> 있었다 — 어느 쪽을 눌러야 하는지 알 수가 없다.
            // 그래서 둘을 바로 옮겼다. 이 판에는 <b>이 판에만 있는 것</b>만 남는다:
            // 추천 질문·명령·자리. 저쪽에 짝이 없는 것들이다.
            _micRt.gameObject.SetActive(false);
            _endRt.gameObject.SetActive(false);
            RebuildChips(owner != null ? owner.Topics : null);
            BuildOrders();
            BuildSeats();
            SetVisible(true);
            if (wasRoster && _anchor != null) _anchor.Recenter();
        }

        private void OpenRosterInternal()
        {
            bool wasHidden = _group.alpha <= 0f;
            _owner = null;
            _rosterOnly = true;
            _confirmLeft = 0f;
            ResetEndLook();
            ApplyPlacement();
            _micRt.gameObject.SetActive(false);
            _endRt.gameObject.SetActive(false);
            RebuildChips(null);
            BuildOrders();
            BuildSeats();
            SetVisible(true);
            if (wasHidden && _anchor != null) { _anchor.KeepInFrontOf(null); _anchor.Recenter(); }
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
            // 듣는 중이면 마이크 버튼을 붉게 — 말해도 되는지 한눈에 보이게
            var mic = MicInput.Instance;
            bool listening = mic != null && mic.IsListening;
            if (_micBg != null) _micBg.color = listening ? _micOnColor : _micColor;
            if (_micLabel != null) _micLabel.text = listening ? "● 듣는 중 (다시 눌러 끝내기)" : "🎤 눌러서 말하기";

            if (_confirmLeft > 0f)
            {
                _confirmLeft -= Time.deltaTime;
                if (_confirmLeft <= 0f) ResetEndLook();
            }

            if (_evidence != null)
            {
                bool show = _evidenceTimer > 0f;
                if (show) _evidenceTimer -= Time.deltaTime;
                if (_evidence.gameObject.activeSelf != show) _evidence.gameObject.SetActive(show);
                if (show) _evidence.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_evidenceTimer));   // 마지막 1초 페이드
            }
        }

        // ── 만들기 ──

        /// <summary>
        /// <b>색을 꾸러미에서 물어 온다.</b> 자막 바·수첩에 이어 셋째다.
        ///
        /// 이 판은 뒤판 없이 단추만 떠 있어서 색이 열 자리로 흩어져 있다.
        /// 하나하나 꾸러미의 <b>뜻이 같은 색</b>에 붙인다 — 값이 아니라 뜻을 맞춘다.
        ///
        /// <b>마이크의 초록만은 옮길 데가 없었다.</b> 꾸러미에 초록이 없다. 그래서
        /// 뜻으로 옮겼다 — 평소에는 <c>slotBack</c>(「눌러도 되는 자리」를 알리는 색),
        /// 녹음 중에는 <c>Vermilion</c>(주칠). 팀원 PC판에서도 마이크는 어두운 칸이고
        /// 누르면 붉어진다.
        ///
        /// 알파(비침)는 우리 값을 지킨다 — 저쪽은 낱색만 주고, 얼마나 비칠지는
        /// 판마다 다르다.
        /// </summary>
        private void AdoptCommonLook()
        {
            if (!_useCommonLook) return;
            var pal = IMUNROK.Ui.DialogueUI.Palette();

            _chipColor      = Keep(pal.slotBack, _chipColor.a);      // 물음 칩 — 고를 수 있는 자리
            _micColor       = Keep(pal.slotBack, _micColor.a);       // 마이크(평소)
            _micOnColor     = Keep(UiLook.Seal, _micOnColor.a);      // 마이크(녹음 중) — 주칠
            _endColor       = Keep(UiLook.WoodLit, _endColor.a);     // 마치기 — 나뭇결
            _confirmColor   = Keep(UiLook.Seal, _confirmColor.a);    // 다짐 — 주칠
            _orderColor     = Keep(UiLook.Seal, _orderColor.a);      // 명령 — 주칠
            _seatColor      = Keep(pal.slotBack, _seatColor.a);      // 사람 칸
            _seatUpColor    = Keep(UiLook.WoodLit, _seatUpColor.a);  // 부른 사람
            _seatEmptyColor = Keep(pal.back, _seatEmptyColor.a);     // 빈 칸 — 먹빛
            _textColor      = pal.text;
        }

        /// <summary>낱색은 꾸러미 것, 비침은 우리 것.</summary>
        private static Color Keep(Color c, float alpha) { return new Color(c.r, c.g, c.b, alpha); }

        /// <summary>무늬를 깔고 그 위에 색을 얹는다 — 꾸러미가 판을 그리는 방식이다.</summary>
        private static void Skin(Image im, Sprite sp, Color c)
        {
            im.sprite = sp;
            im.type = Image.Type.Simple;
            im.color = c;
        }

        private void BuildFixedParts()
        {
            // 마이크 — 가장 크게. VR에서 주된 입력 수단이다.
            _micRt = NewRect("마이크", new Vector2(-250f, 88f), new Vector2(560f, 96f), transform);
            _micBg = _micRt.gameObject.AddComponent<Image>();
            Skin(_micBg, _skin.Slot_, _micColor);
            var micBtn = _micRt.gameObject.AddComponent<Button>();
            micBtn.targetGraphic = _micBg;
            micBtn.onClick.AddListener(() => MicInput.Instance?.Toggle());
            // <b>그림글자 🎤 는 조선 궁서체에 없다</b> — 네모로 뜬다. 꾸러미도 같은 데서
            // 걸려 마이크를 직접 그려 두었다(InventorySkin.Mic_). 그 그림을 얻어 쓴다.
            var micIcon = NewRect("마이크그림", new Vector2(-200f, 0f), new Vector2(56f, 56f), _micRt);
            Skin(micIcon.gameObject.AddComponent<Image>(), _skin.Mic_, _textColor);
            _micLabel = NewText("라벨", "눌러서 말하기", new Vector2(30f, 0f), new Vector2(460f, 96f), _micRt, _fontSize);

            // 마치기 — 이건 되돌릴 수 없다. 상대가 자리를 뜬다.
            //
            // 한때 '잠시 멈추다' 를 곁에 두었다. 손이 미끄러진 한 번에 심문이 영영 끝나는
            // 것을 막으려던 것인데, 마주 앉아 있는 자리에서 <b>대화를 잠시 치운다</b>는 것이
            // 무슨 뜻인지 애매했다 — 치워 놓고 할 일이 없다. 안전은 버튼을 하나 더 두어
            // 얻을 것이 아니라 <b>이 버튼 자신이</b> 두 번 물어 얻는 것이다.
            _endRt = NewRect("마치기", new Vector2(300f, 88f), new Vector2(340f, 96f), transform);
            _endBg = _endRt.gameObject.AddComponent<Image>();
            Skin(_endBg, _skin.Wood_, _endColor);
            var endBtn = _endRt.gameObject.AddComponent<Button>();
            endBtn.targetGraphic = _endBg;
            endBtn.onClick.AddListener(OnEndPressed);
            _endLabel = NewText("라벨", EndWord, Vector2.zero, new Vector2(340f, 96f), _endRt, _fontSize - 4);

            // 물음 · 명령 · 사람 — 세 줄. 위에서 아래로 무거워진다:
            // 묻는 것보다 시키는 것이, 시키는 것보다 사람을 갈아 세우는 것이 큰일이다.
            _chipRow  = NewRect("물음줄", new Vector2(0f, -16f),  new Vector2(1200f, 90f), transform);
            _orderRow = NewRect("명령줄", new Vector2(0f, -104f), new Vector2(1200f, 72f), transform);
            _seatRow  = NewRect("사람줄", new Vector2(0f, -188f), new Vector2(1200f, 76f), transform);

            // 제시한 증거 그림 — 평소엔 꺼져 있다가 잠깐 뜬다
            var evRt = NewRect("증거그림", new Vector2(0f, 300f), new Vector2(420f, 300f), transform);
            _evidence = evRt.gameObject.AddComponent<RawImage>();
            _evidence.raycastTarget = false;
            evRt.gameObject.SetActive(false);
        }

        private void RebuildChips(IReadOnlyList<TopicQuestion> topics)
        {
            foreach (var c in _chips) if (c != null) Destroy(c);
            _chips.Clear();
            if (topics == null || topics.Count == 0) return;

            int n = topics.Count;
            const float gap = 16f;
            float w = Mathf.Min(380f, (1200f - gap * (n - 1)) / n);
            float x0 = -(n * w + gap * (n - 1)) * 0.5f + w * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var t = topics[i];
                if (t == null) continue;
                var rt = NewRect($"질문{i}", new Vector2(x0 + i * (w + gap), 0f), new Vector2(w, 84f), _chipRow);
                var bg = rt.gameObject.AddComponent<Image>();
                Skin(bg, _skin.Slot_, _chipColor);
                var btn = rt.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                var captured = t;
                btn.onClick.AddListener(() => _owner?.AskTopicFromUi(captured));
                var label = NewText("라벨", t.question, Vector2.zero, new Vector2(w - 24f, 84f), rt, _fontSize - 4);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                _chips.Add(rt.gameObject);
            }
        }

        /// <summary>
        /// 명령 줄. <b>근거가 선 것만 뜬다</b> — 그래서 이 줄이 비어 있는 것 자체가
        /// "아직 시킬 수 있는 것이 없다"는 말이 된다.
        /// </summary>
        private void BuildOrders()
        {
            foreach (var c in _orderChips) if (c != null) Destroy(c);
            _orderChips.Clear();
            if (_owner == null) return;

            var live = new List<InterrogationOrder>();
            foreach (var o in _owner.Orders)
                if (o != null && o.enabled && o.Available) live.Add(o);
            if (live.Count == 0) return;

            const float gap = 16f;
            float w = Mathf.Min(420f, (1200f - gap * (live.Count - 1)) / live.Count);
            float x0 = -(live.Count * w + gap * (live.Count - 1)) * 0.5f + w * 0.5f;

            for (int i = 0; i < live.Count; i++)
            {
                var o = live[i];
                var rt = NewRect($"명령{i}", new Vector2(x0 + i * (w + gap), 0f), new Vector2(w, 66f), _orderRow);
                var bg = rt.gameObject.AddComponent<Image>();
                Skin(bg, _skin.Wood_, _orderColor);
                var btn = rt.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                var captured = o;
                btn.onClick.AddListener(() => _owner?.RunOrderFromUi(captured));
                var label = NewText("라벨", o.Label, Vector2.zero, new Vector2(w - 24f, 66f), rt, _fontSize - 6);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                _orderChips.Add(rt.gameObject);
            }
        }

        /// <summary>
        /// 사람 줄 — 동헌의 부르기 판. 판이 없는 씬(1막 마당·사랑방)에서는 아예 안 그린다.
        /// 거기서는 걸어가서 말을 거는 것이 옳다.
        /// </summary>
        private void BuildSeats()
        {
            foreach (var c in _seatChips) if (c != null) Destroy(c);
            _seatChips.Clear();

            var bench = InterrogationBench.Instance;
            if (bench == null || bench.Seats == null || bench.Seats.Count == 0) return;

            int n = bench.Seats.Count;
            const float gap = 14f;
            float w = Mathf.Min(280f, (1200f - gap * (n - 1)) / n);
            float x0 = -(n * w + gap * (n - 1)) * 0.5f + w * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var s = bench.Seats[i];
                if (s == null) continue;
                bool up = bench.IsUp(s);
                bool here = s.사람 != null;

                var rt = NewRect($"사람{i}", new Vector2(x0 + i * (w + gap), 0f), new Vector2(w, 70f), _seatRow);
                var bg = rt.gameObject.AddComponent<Image>();
                Skin(bg, up ? _skin.Wood_ : _skin.Slot_,
                     !here ? _seatEmptyColor : (up ? _seatUpColor : _seatColor));
                var btn = rt.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                var captured = s;
                btn.onClick.AddListener(() => { bench.Call(captured); Refresh(); });

                string label = !here ? s.이름 + " (아직)" : (up ? "▶ " + s.이름 : s.이름);
                NewText("라벨", label, Vector2.zero, new Vector2(w - 20f, 70f), rt, _fontSize - 6);
                _seatChips.Add(rt.gameObject);
            }
        }

        // ── UI 헬퍼 ──

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

        private Text NewText(string name, string content, Vector2 pos, Vector2 size, Transform parent, int size2)
        {
            var rt = NewRect(name, pos, size, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size2;
            t.color = _textColor;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = content;
            return t;
        }
    }
}
