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

        private IInspectable _current;
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

            if (!ReferenceEquals(hit, _current))
            {
                _current = hit;
                _current?.OnInspected(); // 처음 가리킨 순간 1회(단서 자동 기록 등)
            }
#endif
        }

        private void OnGUI()
        {
            if (_current == null) return;
            EnsureStyles();

            float w = 380f, h = 120f;
            float x = Screen.width - w - 20f;
            float y = 20f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none);
            GUI.Label(new Rect(x + 14, y + 10, w - 28, 26), "🔍 " + _current.GetInspectTitle(), _titleStyle);
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
