using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 손에 든 도구 선택 — 맨손 → 등불 → 돋보기 → … 를 돌려가며 고른다.
    ///
    /// 이 클래스는 "무엇을 들고 있는가"(상태)만 책임진다. 그리는 일은 아래 OnGUI 가 맡는다 —
    /// 뷰를 갈아끼워도 이 파일은 건드리지 않는다.
    ///
    /// 전환: 마우스 휠 또는 Q 키. <see cref="Next"/>·<see cref="Prev"/> 는 public 이라
    /// UnityEvent 에 그대로 연결할 수 있다.
    ///
    /// SelectedToolId: 지금 손에 든 도구 id("" = 맨손). 등불·돋보기 등이 이걸 참고한다.
    /// </summary>
    public class ToolbeltHud : MonoBehaviour
    {
        [Tooltip("손에 들 수 있는 도구들(등불·돋보기 등). 앞에 '맨손'이 자동으로 붙음")]
        [SerializeField] private List<ToolDef> _tools = new List<ToolDef>();
        [Tooltip("왼손잡이 배려: 수첩/지도 코너를 좌우 반전")]
        [SerializeField] private bool _leftHanded = false;
        [Tooltip("벨트 글씨에 쓸 한글 폰트. 비우면 씬의 다른 UI 가 올려 둔 공용 폰트를 쓴다")]
        [SerializeField] private Font _font;

        [Tooltip("<b>그림쇠를 쓸까.</b> 끄면 이름만 적는다.\n" +
                 "조선 후기 옹당촌에 렌즈·랜턴 픽토그램이 붙으면 그것만 딴 세상 물건이 된다 — " +
                 "손에 든 것은 어차피 화면에 <b>실물로</b> 들려 있으므로, 띠는 이름만 짚어 주면 된다")]
        [SerializeField] private bool _useIcons = false;

        [Tooltip("사람과 마주 선 동안에도 손에 남는 도구 id. 쉼표로 여럿. 나머지는 내려놓는다.\n" +
                 "등불은 밤에 얼굴을 보려고 남기고, 유척은 <b>드러난 팔을 재려고</b> 남긴다 — " +
                 "소매를 걷으라 해 놓고 정작 잴 것을 손에서 뺏으면 그 명령이 갈 데가 없다")]
        [SerializeField] private string _keepWhileTalking = "lantern,yucheok";

        /// <summary>
        /// 지금 손에 든 도구 id("" = 맨손).
        ///
        /// 값을 따로 들고 있지 않고 <b>벨트에게 물어본다</b>. 예전에는 이 자리에 static
        /// 값을 두고 Select 할 때마다 적어 넣었는데, 벨트가 Awake 할 때도 한 번 적으므로
        /// 씬이 하나 더 올라오거나 벨트가 다시 깨어나면 골라 둔 것이 조용히 ""로 지워졌다.
        /// 손에는 돋보기가 들려 있는데 아무 일도 일어나지 않는 상태가 그것이다.
        /// </summary>
        public static string SelectedToolId => Instance != null ? Instance.CurrentId : "";

        /// <summary>이 벨트가 지금 든 도구 id.</summary>
        private string CurrentId =>
            (_index <= 0 || _index > _tools.Count || _tools[_index - 1] == null) ? "" : _tools[_index - 1].id;

        /// <summary>씬에 하나만 두는 도구벨트. 뷰가 이걸 찾아 붙는다.</summary>
        public static ToolbeltHud Instance { get; private set; }

        private int _index;   // 0 = 맨손, 1.. = _tools[_index-1]

        // ─────────────────────────────────────────────
        //  모델 — 뷰가 읽는 부분
        // ─────────────────────────────────────────────

        /// <summary>도구 목록(맨손은 포함하지 않는다). 슬롯 개수는 SlotCount를 쓸 것.</summary>
        public IReadOnlyList<ToolDef> Tools => _tools;

        /// <summary>맨손을 포함한 전체 슬롯 수.</summary>
        public int SlotCount => _tools.Count + 1;

        /// <summary>지금 고른 슬롯(0 = 맨손).</summary>
        public int SelectedIndex => _index;

        /// <summary>선택이 바뀌면 발생. 뷰가 구독해 하이라이트를 갱신한다.</summary>
        public event Action OnChanged;

        /// <summary>지금 벨트를 감춰야 하는가(심문·수첩 중엔 숨긴다).</summary>
        public bool Hidden => InterrogationController.AnyOpen || JournalView.AnyOpen || HubEnding.Playing || BadukBoard.Playing || VerdictReport.IsOpen;   // 마지막 걸음 동안에는 손에 쥘 것이 없다

        /// <summary>슬롯 i의 표시 이름("맨손" 포함).</summary>
        public string SlotName(int i)
        {
            if (i <= 0 || i > _tools.Count || _tools[i - 1] == null) return "맨손";
            return _tools[i - 1].displayName;
        }

        /// <summary>슬롯 i의 아이콘(없으면 null).</summary>
        public Texture2D SlotIcon(int i)
            => (i <= 0 || i > _tools.Count || _tools[i - 1] == null) ? null : _tools[i - 1].icon;

        // ─────────────────────────────────────────────
        //  조작 — 뷰가 부른다
        // ─────────────────────────────────────────────

        /// <summary>다음 도구로(마지막 다음은 맨손으로 순환). UnityEvent 에 연결할 수 있다.</summary>
        /// <summary>한 번이라도 도구를 갈아 들었나. 그러면 「Q / 휠 전환」을 더는 안 적는다.</summary>
        private bool _everSwitched;

        /// <summary>
        /// 마주 선 동안에도 손에 남기는 도구인가.
        ///
        /// 목록에 적어 둔 것 말고, <b>지금 마주 선 사람이 그 연장을 쓰라고 하는 경우</b>도
        /// 남긴다. 소매를 걷으라 해 놓고 잴 것을 손에서 뺏으면 그 명령이 갈 데가 없다 —
        /// 씬마다 목록을 맞춰 적게 두면 한 군데만 빠뜨려도 그 자리가 통째로 죽는다.
        /// </summary>
        private bool KeptWhileTalking(string id)
        {
            if (!string.IsNullOrEmpty(_keepWhileTalking))
                foreach (var raw in _keepWhileTalking.Split(','))
                    if (raw.Trim() == id) return true;

            var who = InterrogationController.Active;
            return who != null && who.WantsTool(id);
        }

        public void Next() => Select(_index + 1);

        /// <summary>이전 도구로. UnityEvent 에 연결할 수 있다.</summary>
        public void Prev() => Select(_index - 1);

        /// <summary>이 도구를 이미 들고 있는가.</summary>
        public bool Has(ToolDef def) => def != null && _tools.Contains(def);

        /// <summary>
        /// 도구를 벨트에 넣는다(조사청에서 지급받는 길). 이미 있으면 아무 일도 없다.
        ///
        /// 벨트 목록을 인스펙터에서만 채우게 두면 "조사청에서 도구를 받는다"는 흐름을
        /// 만들 수가 없다 — 처음부터 다 들고 있거나, 아예 못 들거나 둘 중 하나가 된다.
        /// </summary>
        /// <returns>이번에 새로 들어갔으면 true.</returns>
        public bool Grant(ToolDef def)
        {
            if (def == null || _tools.Contains(def)) return false;
            _tools.Add(def);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 잠깐 뺐던 도구를 <b>제자리에</b> 도로 넣는다.
        ///
        /// <see cref="Grant"/> 는 늘 뒤에 붙인다 — 새로 받는 물건은 뒤에 붙는 것이 맞다.
        /// 그런데 등경에 얹었다 도로 집는 등불처럼 <b>잠깐 뺐다 넣는</b> 것까지 뒤로 가면,
        /// 돌아올 때마다 띠의 차례가 바뀐다. 맨손·등불·돋보기·마패·유척으로 외워 둔 손이
        /// 휠을 굴릴 때마다 딴것을 집게 되니, 차례가 <b>아무렇게나</b> 도는 것으로 읽힌다.
        /// 도구띠의 차례는 <b>물건의 차례</b>여야지 집었다 놓은 내력이어서는 안 된다.
        /// </summary>
        /// <param name="at">돌려놓을 자리(0 부터). 범위를 벗어나면 뒤에 붙인다.</param>
        public bool GrantAt(ToolDef def, int at)
        {
            if (def == null || _tools.Contains(def)) return false;
            if (at < 0 || at > _tools.Count) _tools.Add(def);
            else _tools.Insert(at, def);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>이 도구가 띠의 몇째 자리인가(0 부터). 없으면 -1.</summary>
        public int IndexOf(ToolDef def) => def == null ? -1 : _tools.IndexOf(def);

        /// <summary>
        /// 도구를 벨트에서 <b>도로 뺀다</b>.
        ///
        /// 조사청에서 쓴다. 거기서 도구를 손에 쥐는 것은 <b>써 보라는 뜻</b>이지
        /// 가지라는 뜻이 아니다 — 익히고 나면 물건은 문갑에 도로 놓고 손은 빈 채로
        /// 나선다. 가져가고 말고는 나중에 따로 물을 일이다.
        /// 빼면 맨손으로 돌아간다.
        /// </summary>
        public bool Revoke(ToolDef def)
        {
            if (def == null || !_tools.Remove(def)) return false;
            _index = 0;                 // 손에 든 것이 사라졌으니 맨손이다
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 이름으로 골라 든다. 벨트에 없으면 아무 일도 없고 거짓을 돌려준다.
        ///
        /// 등경에서 등불을 <b>도로 집을 때</b> 쓴다. 칸 번호는 도구를 받은 차례에 따라
        /// 달라지므로 밖에서 셀 수 있는 값이 아니다 — 이름으로 물어야 한다.
        /// </summary>
        public bool SelectTool(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < _tools.Count; i++)
            {
                if (_tools[i] == null || _tools[i].id != id) continue;
                Select(i + 1);          // 0 은 맨손이라 한 칸 민다
                return true;
            }
            return false;
        }

        /// <summary>슬롯을 직접 고른다(0 = 맨손). 범위를 벗어나면 순환한다.</summary>
        public void Select(int slot)
        {
            int n = SlotCount;
            int next = ((slot % n) + n) % n;
            if (next == _index) return;
            _index = next;
            _everSwitched = true;      // 한 번 갈아 들었으면 익힌 것이다 — 안내를 걷는다
            _howToUntil = Time.unscaledTime + HowToSeconds;   // 바꾼 그 순간부터 잠깐
            Apply();
        }

        private void Apply()
        {
            OnChanged?.Invoke();
        }

        // ─────────────────────────────────────────────

        /// <summary>꾸러미의 결. 정적으로 들면 판을 넘겨 살아남되 내용이 죽는다 — 제 것으로 든다.</summary>
        private readonly IMUNROK.Ui.InventorySkin _skin = new IMUNROK.Ui.InventorySkin();

        private void Awake()
        {
            Instance = this;
            HudSide.LeftHanded = _leftHanded;
            UiFont.Publish(_font);
            BuildBelt();
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Update()
        {
            // <b>사람과 마주 서면 손에 든 것을 내려놓는다 — 등불만 빼고.</b>
            //
            // 여태 심문 중에는 벨트만 잠갔다. 그런데 돋보기는 심문이 열리면 스스로
            // 물러나므로(<see cref="MagnifierLens"/>), 손에 든 채로 말을 걸면 <b>렌즈가
            // 소리 없이 사라졌다</b>. 벨트는 여전히 「손 : 돋보기」라 하는데 손에는
            // 아무것도 없는 꼴이다.
            //
            // 사람 앞에 유리를 들이대는 것은 어차피 하는 일이 아니고, 등불은 다르다 —
            // 밤에 얼굴을 보려면 불이 있어야 한다. 그래서 등불만 남기고 내려놓는다.
            // 잠그는 대신 <b>실제로 손을 비운다</b>: 벨트가 말하는 것과 손에 있는 것이
            // 어긋나지 않는 편이 낫다.
            if (InterrogationController.AnyOpen)
            {
                string held = CurrentId;
                if (!string.IsNullOrEmpty(held) && !KeptWhileTalking(held)) Select(0);
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // 치는 동안에는 Q 가 도구가 아니라 <b>글자</b>다.
            if (kb != null && !Typing.Now && kb.qKey.wasPressedThisFrame) { Next(); return; }
            // <b>판이 떠 있으면 휠은 판의 것이다.</b> 수첩 상세를 휠로 내려 읽거나 물증 무대를
            // 당겨 보는 동안 도구띠까지 같이 굴러, 수첩을 덮고 나면 손에 딴 도구가 들려 있었다.
            bool wheelBusy = JournalView.AnyOpen || DocumentView.IsOpen;
            if (mouse != null && !wheelBusy)
            {
                // <b>휠을 내리면 목록도 내려간다.</b>
                //
                // 띠는 위에서부터 맨손·등불·돋보기·마패·유척으로 <b>내려</b> 그린다(RowY).
                // 그런데 휠을 <b>올릴</b> 때 그 차례로 내려갔다 — 손가락이 가는 쪽과 눈이
                // 따라가는 쪽이 반대라, 한 칸 건너뛴 것처럼 읽히고 매번 되짚게 된다.
                // 굴리는 쪽과 짚히는 쪽을 같게 둔다.
                float sy = mouse.scroll.ReadValue().y;
                if (sy < 0f) Next();
                else if (sy > 0f) Prev();
            }
#endif
        }

        // ─────────────────────────────────────────────
        //  벨트 뷰(OnGUI)
        // ─────────────────────────────────────────────

        private GUIStyle _iconText, _iconOn, _label, _howTo;
        private Texture2D _slot, _slotOn;

        /// <summary>
        /// 쓰는 법을 <b>얼마나</b> 띄울까(초).
        ///
        /// 여태 손에 든 <b>내내</b> 띄웠다. 한 번 익히면 다시 읽지 않는 말인데 화면
        /// 오른쪽에 계속 붙어 있으니, 안 읽히면서 자리만 차지하고 무엇보다 조선 후기
        /// 마당에 「[F] 를 눌러…」가 <b>매 순간</b> 떠 있으면 그 말이 세상 밖의 것임이
        /// 내내 드러난다. 바꿔 든 그 자리에서 한 번 읽히면 그것으로 족하다.
        /// </summary>
        private const float HowToSeconds = 1.8f;

        /// <summary>
        /// 마지막 <b>이만큼</b>은 스러지며 간다(초).
        ///
        /// 시간이 다 찼다고 글자가 <b>툭</b> 없어지면 눈이 그것을 「사라짐」이 아니라
        /// <b>깜빡임</b>으로 읽는다 — 무엇이 잘못된 줄 알고 그쪽을 다시 본다.
        /// 등불 심지가 잦아들 듯 옅어져야 읽고 흘려보낸 것이 된다.
        /// </summary>
        private const float HowToFade = 0.7f;

        /// <summary>이 때까지만 쓰는 법을 적는다.</summary>
        private float _howToUntil;

        private void LateUpdate()
        {
            SyncBelt();                 // 도구가 늘거나 줄었으면 칸도 따라간다
            if (_root == null) return;
            bool show = !Hidden;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;

            for (int i = 0; i < _cells.Count; i++)
            {
                bool on = i == _index;
                _cells[i].color = on ? SlotOn : SlotOff;
                _names[i].color = on ? NameOn : NameOff;
                _names[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                if (_icons.Count > i && _icons[i] != null)
                {
                    var tex = _useIcons ? SlotIcon(i) : null;
                    _icons[i].texture = tex;
                    _icons[i].enabled = tex != null;
                    _names[i].enabled = tex == null;
                }
            }

            // <b>「Q / 휠 전환」은 처음 한 번만.</b> 한 번 갈아 들면 익힌 것이다.
            if (_wheelHint != null) _wheelHint.enabled = !_everSwitched;

            // <b>쓰는 법은 바꿔 든 뒤 잠깐만</b>, 그리고 스러지며 간다.
            float left = _howToUntil - Time.unscaledTime;
            string how = left > 0f ? Controls.HowTo(CurrentId) : "";
            if (_howToText != null)
            {
                bool lit = !string.IsNullOrEmpty(how);
                if (_howToText.enabled != lit) _howToText.enabled = lit;
                if (lit)
                {
                    _howToText.text = how;
                    var c = HowToColor;
                    c.a *= Mathf.Clamp01(left / HowToFade);
                    _howToText.color = c;
                    // 줄은 <b>지금 든 칸 옆</b>에 나란히 선다 — 어느 칸을 두고 하는 말인지
                    // 눈으로 이어져야 한다.
                    var rt = _howToText.rectTransform;
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, RowY(_index));
                }
            }
        }

        // ── 판 짓기 ──────────────────────────────────────────────
        //
        // <b>IMGUI 에서 캔버스로 옮겼다.</b> 띠만 낡은 방식으로 남아 있었는데 그러면
        // 둘이 어긋난다: IMGUI 는 TMP 를 못 써서 이 판만 글씨가 가늘었고, 헤드셋에서는
        // 화면 버퍼에 곧장 그려져 <b>아예 안 보인다</b>(견우 꾸러미 문서의 첫 경고).
        //
        // <b>자리와 크기는 그대로 옮긴다.</b> 화면 세로의 6.2% 짜리 칸이 오른쪽 벽에
        // 세로로 서던 그 모습이다 — 옮기는 김에 자리까지 바꾸면 무엇 때문에 달라
        // 보이는지 가릴 수 없게 된다. 화소 대신 화면 1732단위로 적을 뿐이다.
        private const float CellSize = ScreenPanel.RefHeight * 0.062f;
        private static float Gap { get { return Mathf.Round(CellSize * 0.16f); } }
        private static float Margin { get { return Mathf.Round(CellSize * 0.34f); } }

        private static Color SlotOff { get { return UiLook.With(UiLook.Paper, 0.08f); } }
        private static Color SlotOn { get { return UiLook.With(UiLook.Gold, 0.32f); } }
        private static Color NameOff { get { return UiLook.With(UiLook.Paper, 0.55f); } }
        private static Color NameOn { get { return UiLook.With(UiLook.Lit(UiLook.Gold, 0.45f), 1f); } }
        private static Color HowToColor { get { return UiLook.With(UiLook.Lit(UiLook.Gold, 0.42f), 0.92f); } }

        private RectTransform _root;
        private readonly List<Image> _cells = new List<Image>();
        private readonly List<TextMeshProUGUI> _names = new List<TextMeshProUGUI>();
        private readonly List<RawImage> _icons = new List<RawImage>();
        private TextMeshProUGUI _wheelHint, _howToText;

        /// <summary>
        /// <b>띠가 앉는 높이</b> — 화면 세로의 이 몫에 띠의 <b>한가운데</b>가 온다.
        ///
        /// 여태 화면 <b>맨 아래</b>에 붙어 있었다. 그 자리는 자막 바가 쓰는 자리이기도 해서,
        /// 사람 앞에 서면 띠와 말판이 겹쳤다 — 띠는 심문 중에 스스로 물러나지만,
        /// 물러나기 전 한 칸이나 상태 줄이 겹쳐 뜰 때가 있었다.
        /// 오른쪽 <b>가운데보다 살짝 위</b>로 올리면 아래쪽은 통째로 말판의 자리가 된다.
        /// </summary>
        private const float BeltCenterShare = 0.56f;

        /// <summary>칸 <paramref name="i"/> 의 한복판 높이(캔버스 단위, 아래가 0).</summary>
        private float RowY(int i)
        {
            int n = SlotCount;
            float totalH = n * CellSize + (n - 1) * Gap;
            float center = ScreenPanel.RefHeight * BeltCenterShare;
            // 위에서부터 맨손 · 등불 · 돋보기 차례로 내려간다(IMGUI 시절 그대로).
            float topOfStack = center + totalH * 0.5f;
            return topOfStack - CellSize * 0.5f - i * (CellSize + Gap);
        }

        /// <summary>
        /// <b>칸 수가 달라졌으면 띠를 다시 짓는다.</b>
        ///
        /// 여태 <see cref="BuildBelt"/> 는 Awake 에 <b>한 번</b>만 돌았다. 그런데 도구는
        /// 그 뒤에도 는다 — 관아의 유척은 <see cref="ToolIssue"/> 가 Start 에서 내주고,
        /// 등경에 등불을 얹으면 하나가 <b>빠진다</b>. 칸이 Awake 때 굳어 있으니 늘어난
        /// 도구는 <b>띠에 자리가 없어 안 보였다</b> — 유척은 Q 로 돌리면 손에는 들리는데
        /// 화면 오른쪽 목록에는 없었다. 있는데 없는 것으로 보이는 것이 가장 나쁘다.
        ///
        /// 칸을 통째로 다시 짓는 까닭: 칸의 높이(<see cref="RowY"/>)가 <b>전체 칸 수</b>로
        /// 정해지므로, 한 칸을 덧붙이면 나머지도 다 옮겨 앉아야 한다.
        /// </summary>
        private void SyncBelt()
        {
            if (_cells.Count == SlotCount) return;
            BuildBelt();
        }

        private void BuildBelt()
        {
            // 다시 지을 때는 먼저 헌 것을 걷는다 — 안 걷으면 띠가 겹쳐 그려진다.
            if (_root != null) Destroy(_root.gameObject);
            _cells.Clear(); _names.Clear(); _icons.Clear();
            _wheelHint = null; _howToText = null;

            var go = new GameObject("도구띠", typeof(RectTransform));
            ScreenPanel.Raise(go, ScreenPanel.LayerBar - 20);
            _root = (RectTransform)go.transform;

            float x = ScreenPanel.RefHeight * 16f / 9f - Margin - CellSize * 0.5f;
            int n = SlotCount;
            for (int i = 0; i < n; i++)
            {
                var cell = Corner("칸_" + i, new Vector2(x, RowY(i)), new Vector2(CellSize, CellSize));
                var img = cell.gameObject.AddComponent<Image>();
                img.sprite = _skin.Slot_;
                img.color = SlotOff;
                _cells.Add(img);

                // 눌러서도 고른다. 광선이 아니라 화면 단추다 — 우리 놀이는 마우스로 짚는다.
                var btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                int at = i;
                btn.onClick.AddListener(() => Select(at));

                float pad = Mathf.Round(CellSize * 0.13f);
                var iconRt = Corner("그림쇠", Vector2.zero, new Vector2(CellSize - pad * 2f, CellSize - pad * 2f));
                iconRt.SetParent(cell, false);
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                var raw = iconRt.gameObject.AddComponent<RawImage>();
                raw.raycastTarget = false;
                raw.enabled = false;
                _icons.Add(raw);

                // <b>그림쇠는 끄고 이름을 적는다.</b> 조선 후기 옹당촌에 렌즈·랜턴
                // 픽토그램이 붙으면 그것만 딴 세상 물건이 된다.
                var nameGo = new GameObject("이름", typeof(RectTransform), typeof(TextMeshProUGUI));
                var nameRt = (RectTransform)nameGo.transform;
                nameRt.SetParent(cell, false);
                nameRt.anchorMin = nameRt.anchorMax = nameRt.pivot = new Vector2(0.5f, 0.5f);
                nameRt.anchoredPosition = Vector2.zero;
                nameRt.sizeDelta = new Vector2(CellSize, CellSize);
                var name = UiText.Dress(nameGo.GetComponent<TextMeshProUGUI>(),
                                        Mathf.Round(CellSize * 0.26f), TextAnchor.MiddleCenter, NameOff);
                name.text = SlotName(i);
                UiText.Wrap(name, false);
                _names.Add(name);
            }

            // ⚠️ 글도 <b>왼쪽 아래 기준</b>으로 앉혀야 한다. UiText.New 는 가운데를 기준으로
            //    잡으므로, 칸과 같은 좌표를 그대로 넘기면 글만 화면 밖으로 날아간다
            //    (칸은 Corner 로 왼쪽 아래에 매달아 두었다).
            float capW = 760f;
            float capX = x - CellSize * 0.5f - Gap - capW * 0.5f;

            var wheelRt = Corner("바꾸는법", new Vector2(capX, RowY(0) + CellSize * 0.5f + Gap + 22f),
                                 new Vector2(capW, 44f));
            _wheelHint = UiText.Dress(wheelRt.gameObject.AddComponent<TextMeshProUGUI>(),
                                      Mathf.Round(CellSize * 0.24f), TextAnchor.MiddleRight,
                                      UiLook.With(UiLook.Lit(UiLook.Gold, 0.30f), 0.95f));
            _wheelHint.text = "휠을 굴려 바꾼다 (Q)";
            UiText.Wrap(_wheelHint, false);

            var howRt = Corner("쓰는법", new Vector2(capX, RowY(0)), new Vector2(capW, 88f));
            _howToText = UiText.Dress(howRt.gameObject.AddComponent<TextMeshProUGUI>(),
                                      Mathf.Round(CellSize * 0.22f), TextAnchor.MiddleRight, HowToColor);
            UiText.Wrap(_howToText, true);
        }

        /// <summary>오른쪽 아래를 기준점으로 잡은 네모 하나 — 띠는 오른쪽 벽에 붙는다.</summary>
        private RectTransform Corner(string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

    }
}
