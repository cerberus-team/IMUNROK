using System;
using System.Collections.Generic;
using UnityEngine;
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
        public bool Hidden => InterrogationController.AnyOpen || JournalView.AnyOpen;

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
            Apply();
        }

        private void Apply()
        {
            OnChanged?.Invoke();
        }

        // ─────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            HudSide.LeftHanded = _leftHanded;
            UiFont.Publish(_font);
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var t in new[] { _slot, _slotOn })
                if (t != null) Destroy(t);
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
            if (mouse != null)
            {
                float sy = mouse.scroll.ReadValue().y;
                if (sy > 0f) Next();
                else if (sy < 0f) Prev();
            }
#endif
        }

        // ─────────────────────────────────────────────
        //  벨트 뷰(OnGUI)
        // ─────────────────────────────────────────────

        private GUIStyle _iconText, _iconOn, _label, _howTo;
        private Texture2D _slot, _slotOn;

        private void OnGUI()
        {
            if (Hidden) return;
            EnsureStyles();

            // <b>칸은 화면에 맞춰 큰다.</b>
            //
            // 54는 <b>화소</b>였다. 1440줄짜리 화면에서 54화소면 손톱만 하고, 창을
            // 반으로 줄여 보면 도로 커진다 — 화면이 커질수록 벨트가 작아진다는 뜻이다.
            // 화면 세로의 몫으로 잡으면 어느 화면에서나 같은 크기로 보인다.
            // 6.2%는 1080줄에서 67, 1440줄에서 89화소다.
            float size = Mathf.Round(Screen.height * 0.062f);
            float gap = Mathf.Round(size * 0.16f);
            float margin = Mathf.Round(size * 0.34f);
            int n = SlotCount;
            float totalW = n * size + (n - 1) * gap;
            float y = Screen.height - size - margin;
            float x0 = (Screen.width - totalW) * 0.5f;   // 하단 중앙(수첩·지도 코너와 안 겹침)

            for (int i = 0; i < n; i++)
            {
                Rect r = new Rect(x0 + i * (size + gap), y, size, size);
                GUI.DrawTexture(r, i == _index ? _slotOn : _slot);

                // <b>그림쇠는 끄고 이름을 적는다.</b>
                //
                // 조선 후기 옹당촌에 렌즈·랜턴 픽토그램이 붙으면 <b>그것만</b> 딴 세상
                // 물건이 된다. 손에 든 것은 어차피 화면에 실물로 들려 있으므로 띠가
                // 할 일은 「지금 무엇을 들었나」를 짚어 주는 것뿐이고, 그 일에는
                // 이름 두 글자가 그림쇠보다 낫다 — 등불과 돋보기는 생김새가 비슷하지도
                // 않은데 작은 칸에 넣으면 둘 다 까만 덩어리가 된다.
                var icon = _useIcons ? SlotIcon(i) : null;
                float pad = Mathf.Round(size * 0.13f);
                if (icon != null)
                    GUI.DrawTexture(new Rect(r.x + pad, r.y + pad, size - pad * 2f, size - pad * 2f), icon, ScaleMode.ScaleToFit);
                else
                    GUI.Label(r, SlotName(i), i == _index ? _iconOn : _iconText);

                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Select(i);   // 클릭으로도 선택
            }

            // <b>「Q / 휠 전환」은 처음 한 번만.</b>
            //
            // 한 번 익히면 다시 볼 일이 없는 말인데 화면 아래에 <b>내내</b> 붙어 있었다.
            // 늘 있는 안내는 안 읽히면서 자리만 차지하고, 무엇보다 조선 후기 옹당촌에
            // 「Q / 휠」이라 적힌 것이 계속 떠 있으면 그 말이 <b>세상 밖의 것</b>임이
            // 매 순간 드러난다.
            //
            // 그래서 <b>한 번이라도 갈아 들면</b> 그때부터 안 적는다. 갈아 들었다는 것이
            // 곧 익혔다는 뜻이다. 익히지 않은 사람에게는 그대로 남는다.
            // 칸마다 이름이 적히므로 「손 : 돋보기」를 또 적지 않는다 — 같은 말을
            // 두 줄에 두면 둘 다 안 읽힌다. 남는 것은 <b>바꾸는 법</b> 한 번뿐이다.
            float line = Mathf.Round(size * 0.42f);
            if (!_everSwitched)
                GUI.Label(new Rect(x0, y - line - 4f, totalW, line), "휠을 굴려 바꾼다 (Q)", _label);

            // <b>쓰는 법을 그 자리에서 일러 준다.</b>
            //
            // Controls.HowTo 는 도구마다 한 줄씩 적어 두고도 <b>아무도 부르지 않아</b>
            // 화면에 뜬 적이 없었다. 벨트는 무엇을 들었는지만 말하고 쓰는 법은 아무 데도
            // 없어서, 돋보기를 손에 쥐고도 무엇을 눌러야 하는지 몰라 서 있게 된다
            // (「우클릭인지 F인지 모르겠다」가 그 말이다).
            //
            // 드는 동안 <b>내내</b> 둔다. 한 번 스치고 사라지면 마침 딴 데를 보던 사람은
            // 못 읽고, 그 뒤로 다시 볼 길이 없다. 맨손이면 적을 것이 없으니 안 적는다.
            string how = Controls.HowTo(CurrentId);
            if (!string.IsNullOrEmpty(how))
                GUI.Label(new Rect(x0 - totalW * 0.5f, y - line * 2f - 8f, totalW * 2f, line),
                          how, _howTo);
        }

        private int _styledFor;   // 어느 화면 높이에 맞춰 지은 글씨인가

        private void EnsureStyles()
        {
            // 글씨도 칸을 따라 큰다. 칸만 키우고 글씨를 두면 「맨손」이 칸 한복판에
            // 깨알같이 앉는다.
            if (_iconText != null && _styledFor == Screen.height) return;
            _styledFor = Screen.height;
            float size = Mathf.Round(Screen.height * 0.062f);

            if (_slot == null)   _slot   = Solid(UiLook.With(UiLook.Paper, 0.08f));
            if (_slotOn == null) _slotOn = Solid(UiLook.With(UiLook.Gold, 0.32f));
            // 안 든 것은 묽게, 든 것은 낙관빛으로. 두 글자가 들어가야 하므로
            // 칸 높이의 3분의 1이 아니라 4분의 1쯤으로 잡는다.
            _iconText = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(size * 0.26f),
                alignment = TextAnchor.MiddleCenter, wordWrap = false,
                normal = { textColor = UiLook.With(UiLook.Paper, 0.55f) }
            };
            _iconOn = new GUIStyle(_iconText)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = UiLook.With(UiLook.Lit(UiLook.Gold, 0.45f), 1f) }
            };
            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(size * 0.24f), alignment = TextAnchor.MiddleCenter,
                normal = { textColor = UiLook.With(UiLook.Lit(UiLook.Gold, 0.30f), 0.95f) }
            };
            // 쓰는 법은 이름줄보다 한 뼘 묽게 — 읽히되 이름을 덮지 않게.
            // 글쇠 이름에 낙관빛을 입히므로 richText 를 켠다(Controls.Key).
            _howTo = new GUIStyle(_label)
            {
                fontSize = Mathf.RoundToInt(size * 0.22f),
                richText = true, wordWrap = false,
                normal = { textColor = UiLook.With(UiLook.Paper, 0.72f) }
            };
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
