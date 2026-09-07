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
            // 심문 중엔 도구 전환 잠금(든 도구 그대로 유지)
            if (InterrogationController.AnyOpen) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null && kb.qKey.wasPressedThisFrame) { Next(); return; }
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

        private GUIStyle _iconText, _label;
        private Texture2D _slot, _slotOn;

        private void OnGUI()
        {
            if (Hidden) return;
            EnsureStyles();

            const float size = 54f, gap = 8f, margin = 16f;
            int n = SlotCount;
            float totalW = n * size + (n - 1) * gap;
            float y = Screen.height - size - margin;
            float x0 = (Screen.width - totalW) * 0.5f;   // 하단 중앙(수첩·지도 코너와 안 겹침)

            for (int i = 0; i < n; i++)
            {
                Rect r = new Rect(x0 + i * (size + gap), y, size, size);
                GUI.DrawTexture(r, i == _index ? _slotOn : _slot);

                var icon = SlotIcon(i);
                if (icon != null)
                    GUI.DrawTexture(new Rect(r.x + 7, r.y + 7, size - 14, size - 14), icon, ScaleMode.ScaleToFit);
                else
                    GUI.Label(r, i == 0 ? "맨손" : First(SlotName(i)), _iconText);

                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Select(i);   // 클릭으로도 선택
            }

            GUI.Label(new Rect(x0, y - 24f, totalW, 22f),
                      $"손 : {SlotName(_index)}     (Q / 휠 전환)", _label);
        }

        private static string First(string s) => string.IsNullOrEmpty(s) ? "?" : s.Substring(0, 1);

        private void EnsureStyles()
        {
            if (_iconText != null) return;
            _slot   = Solid(UiLook.With(UiLook.Paper, 0.08f));
            _slotOn = Solid(UiLook.With(UiLook.Gold, 0.32f));
            _iconText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = UiLook.With(UiLook.Lit(UiLook.Gold, 0.30f), 0.95f) }
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
