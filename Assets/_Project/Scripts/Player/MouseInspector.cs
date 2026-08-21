using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 에디터/비-VR 테스트용 "살펴보기" 도구(확대경 대역).
    /// 카메라에서 마우스 방향으로 레이를 쏘아 IInspectable 대상을 가리키면
    /// 화면에 제목·본문 패널을 띄우고, 처음 가리킨 순간 OnInspected()를 호출한다.
    ///
    /// ★ 지금은 "가리키면 바로" 보이지만, 실제 게임에선 "확대경을 든 상태"에서만
    ///   보이도록 게이트할 예정(VR에서 확대경 Grab과 연결). 그래도 대상 로직
    ///   (IInspectable / InspectableNote)은 그대로 재사용된다.
    /// </summary>
    public class MouseInspector : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _maxDistance = 20f;
        [SerializeField] private LayerMask _mask = ~0;
        [Tooltip("돋보기로 취급할 도구 id")]
        [SerializeField] private string _magnifierToolId = "magnify";

        private IInspectable _current;
        private bool _needMagHint;   // 돋보기 필요한데 안 든 물건을 가리키는 중
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) _camera = Camera.main;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null || _camera == null) return;

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            IInspectable hit = null;
            if (Physics.Raycast(ray, out RaycastHit info, _maxDistance, _mask))
                hit = info.collider.GetComponentInParent<IInspectable>();

            // 이 물건이 '돋보기 필요' 표시인데 지금 들여다보고 있지 않으면 → 힌트만, 단서 기록 X
            //
            // 예전에는 '돋보기를 들었는가'만 봤다. 그러면 돋보기는 주머니에 든 열쇠지
            // 눈에 대는 유리가 아니다. 이제는 렌즈를 눈에 대고 그 물건을 짚어야 한다.
            bool hasMag = MagnifierLens.Peering
                          || (ToolbeltHud.SelectedToolId == _magnifierToolId && !MagnifierLens.Held);
            bool needsMag = (hit as InspectableNote)?.RequiresMagnifier ?? false;
            if (hit != null && needsMag && !hasMag)
            {
                _current = null;
                _needMagHint = true;
                return;
            }
            _needMagHint = false;

            if (!ReferenceEquals(hit, _current))
            {
                _current = hit;
                _current?.OnInspected(); // 처음 가리킨 순간 1회(단서 자동 기록 등)
            }
#endif
        }

        private void OnGUI()
        {
            if (JournalView.AnyOpen) return;   // 수첩 펼치면 살펴보기 UI 숨김
            EnsureStyles();

            float w = 380f, h = 120f;
            float x = Screen.width - w - 20f;
            float y = 58f;   // 우상단 지도 버튼과 안 겹치게 살짝 내림

            // 돋보기 필요한 물건인데 안 든 상태 → 안내 힌트만
            if (_current == null)
            {
                if (_needMagHint)
                    GUI.Label(new Rect(x, y, w, 24),
                              MagnifierLens.Held ? "돋보기를 눈에 대야겠다 (오른쪽 단추)"
                                                 : "돋보기로 자세히 봐야 할 것 같다…", _titleStyle);
                return;
            }

            GUI.Box(new Rect(x, y, w, h), GUIContent.none);
            GUI.Label(new Rect(x + 14, y + 10, w - 28, 26), _current.GetInspectTitle(), _titleStyle);
            GUI.Label(new Rect(x + 14, y + 42, w - 28, h - 52), _current.GetInspectBody(), _bodyStyle);
        }

        private void EnsureStyles()
        {
            if (_bodyStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.9f, 0.6f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, wordWrap = true,
                normal = { textColor = Color.white }
            };
        }
    }
}
