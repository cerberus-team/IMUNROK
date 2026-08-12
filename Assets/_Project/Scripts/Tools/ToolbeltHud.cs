using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 손에 든 도구 선택 — 맨손 → 등불 → 돋보기 → … 를 돌려가며 고른다.
    ///  · 전환: 마우스 휠 또는 Q 키(개발용). VR에선 컨트롤러 버튼으로 같은 방식.
    ///  · 화면 하단 중앙에 벨트를 작게 표시(현재 든 것 강조). 슬롯 클릭으로도 선택.
    ///  · 수첩(J)·지도(M)는 "손에 드는 도구"가 아니라 UI라서 여기 없음 → 코너 버튼으로 분리.
    ///
    /// SelectedToolId: 지금 손에 든 도구 id("" = 맨손). 등불 등 다른 시스템이 이걸 참고.
    /// </summary>
    public class ToolbeltHud : MonoBehaviour
    {
        [Tooltip("손에 들 수 있는 도구들(등불·돋보기 등). 앞에 '맨손'이 자동으로 붙음")]
        [SerializeField] private List<ToolDef> _tools = new List<ToolDef>();
        [Tooltip("왼손잡이 배려: 수첩/지도 코너를 좌우 반전")]
        [SerializeField] private bool _leftHanded = false;

        public static string SelectedToolId { get; private set; } = "";
        private int _index;   // 0 = 맨손, 1.. = _tools[_index-1]

        private GUIStyle _iconText, _label;
        private Texture2D _slot, _slotOn;

        private void Awake() => HudSide.LeftHanded = _leftHanded;

        private void Update()
        {
            // 심문 중엔 도구 전환 잠금(든 도구 그대로 유지)
            if (InterrogationController.AnyOpen) return;

            int n = _tools.Count + 1;   // 맨손 포함
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool next = false, prev = false;

            if (kb != null && kb.qKey.wasPressedThisFrame) next = true;
            if (mouse != null)
            {
                float sy = mouse.scroll.ReadValue().y;
                if (sy > 0f) next = true;
                else if (sy < 0f) prev = true;
            }
            if (next) _index = (_index + 1) % n;
            if (prev) _index = (_index - 1 + n) % n;
#endif
            SelectedToolId = (_index == 0 || _tools[_index - 1] == null) ? "" : _tools[_index - 1].id;
        }

        private void OnGUI()
        {
            if (InterrogationController.AnyOpen || JournalView.AnyOpen) return;   // 심문·수첩 중엔 벨트 숨김
            EnsureStyles();
            const float size = 54f, gap = 8f, margin = 16f;
            int n = _tools.Count + 1;
            float totalW = n * size + (n - 1) * gap;
            float y = Screen.height - size - margin;
            float x0 = (Screen.width - totalW) * 0.5f;   // 하단 중앙(수첩·지도 코너와 안 겹침)

            for (int i = 0; i < n; i++)
            {
                Rect r = new Rect(x0 + i * (size + gap), y, size, size);
                bool on = (i == _index);
                GUI.DrawTexture(r, on ? _slotOn : _slot);

                if (i == 0)
                {
                    GUI.Label(r, "맨손", _iconText);
                }
                else
                {
                    var t = _tools[i - 1];
                    if (t == null) continue;
                    if (t.icon != null)
                        GUI.DrawTexture(new Rect(r.x + 7, r.y + 7, size - 14, size - 14), t.icon, ScaleMode.ScaleToFit);
                    else
                        GUI.Label(r, First(t.displayName), _iconText);
                }

                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _index = i;   // 클릭으로도 선택
            }

            string cur = (_index == 0 || _tools[_index - 1] == null) ? "맨손" : _tools[_index - 1].displayName;
            GUI.Label(new Rect(x0, y - 24f, totalW, 22f), $"손 : {cur}     (Q / 휠 전환)", _label);
        }

        private static string First(string s) => string.IsNullOrEmpty(s) ? "?" : s.Substring(0, 1);

        private void EnsureStyles()
        {
            if (_iconText != null) return;
            _slot   = Solid(new Color(1f, 1f, 1f, 0.08f));
            _slotOn = Solid(new Color(1f, 0.85f, 0.4f, 0.32f));
            _iconText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.6f, 0.95f) }
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
