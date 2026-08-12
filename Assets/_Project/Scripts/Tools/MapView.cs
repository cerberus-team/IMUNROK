using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 사건 지도 — M 키 또는 도구벨트의 '지도'로 펼친다.
    /// 챕터마다 _mapImage를 그 사건 지도로 바꿔 끼우면 된다(코드 수정 없음).
    /// </summary>
    public class MapView : MonoBehaviour
    {
        [Tooltip("이 챕터(사건)의 지도 이미지. 챕터마다 다르게 지정")]
        [SerializeField] private Texture2D _mapImage;
        [SerializeField] private string _title = "사건 지도";

        private bool _open;
        public bool IsOpen => _open;
        public void Toggle() => _open = !_open;
        public void Open() => _open = true;
        public void Close() => _open = false;

        private GUIStyle _titleStyle, _hintStyle;

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) _open = !_open;
#endif
        }

        private void OnGUI()
        {
            if (JournalView.AnyOpen) return;   // 수첩 펼치면 지도 UI 숨김
            EnsureStyles();

            // 닫힘 상태: 우상단(왼손잡이면 좌상단) 코너 버튼
            if (!_open)
            {
                const float bw = 130f, bh = 32f, m = 16f;
                float bx = HudSide.LeftHanded ? m : (Screen.width - m - bw);
                if (GUI.Button(new Rect(bx, m, bw, bh), "지도 (M)"))
                    _open = true;
                return;
            }

            float w = Mathf.Min(760f, Screen.width - 80f);
            float h = Mathf.Min(560f, Screen.height - 80f);
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none);
            GUI.Label(new Rect(x + 20, y + 14, w - 120, 26), _title, _titleStyle);
            if (GUI.Button(new Rect(x + w - 96, y + 12, 82, 28), "✕ 닫기")) _open = false;
            GUI.Label(new Rect(x + 20, y + h - 28, w - 40, 22), "(M 으로도 닫힘)", _hintStyle);

            Rect img = new Rect(x + 20, y + 48, w - 40, h - 84);
            if (_mapImage != null)
                GUI.DrawTexture(img, _mapImage, ScaleMode.ScaleToFit);
            else
                GUI.Label(img, "(이 사건의 지도 이미지를 MapView ▸ Map Image 에 넣으세요)", _hintStyle);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, wordWrap = true,
                normal = { textColor = new Color(1f, 1f, 1f, 0.6f) }
            };
        }
    }
}
