using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>가리키는 자리에 점 하나</b> — 견우 판과 같은 커서.
    ///
    /// 우리 화면에는 겨누는 표시가 없었다. 운영체제의 흰 화살표 하나뿐인데, 그것은 창을
    /// 닫고 파일을 여는 화살표라 조선의 밤 마당 위에 떠 있으면 그 순간 화면이 게임이
    /// 아니라 프로그램이 된다. 게다가 화살표는 겨눈 것이 눌리는 물건인지 아닌지를
    /// 말해 주지 않아, 물건 앞에 서서도 눌러 봐야 알았다.
    ///
    /// <b>모양을 두 번 고쳤다.</b> 처음에는 저쪽 <c>AimPanel</c> 을 본떠 <c>·</c>·<c>◆</c>
    /// 글자를 찍었는데, 그것은 저쪽이 <b>세상을 겨눌 때</b>(시선 정중앙) 쓰는 표시다.
    /// 사람들이 실제로 보는 것은 <b>판 위의 커서</b> — <see cref="IMUNROK.Ui.InventorySkin"/>
    /// 의 <c>Dot_</c> 이고, 가운데가 주칠 점이고 둘레에 한지빛 무리가 도는
    /// <b>붉은 동그라미</b>다. 우리 놀이는 마우스로 짚는 놀이라 저쪽의 판 커서와 같은
    /// 처지다 — 그러니 같은 그림을 쓴다. 그리는 것도 저쪽처럼 <b>판(캔버스)</b>이다:
    /// IMGUI 는 헤드셋에 아예 안 보이고, 우리 다른 판은 모두 캔버스로 옮겼다.
    ///
    /// 붙이는 곳: 눈(카메라). 짚는 부품(<see cref="MouseRaySelector"/>)과 같은 자리면 된다.
    /// </summary>
    public class AimMark : MonoBehaviour
    {
        [Tooltip("레이를 쏠 카메라. 비우면 이 오브젝트의 Camera 또는 Camera.main")]
        [SerializeField] private Camera _camera;
        [Tooltip("이만큼(m) 안의 것만 겨눈 것으로 친다. 짚는 부품과 같은 값이어야 한다")]
        [SerializeField] private float _maxDistance = 20f;
        [Tooltip("레이가 맞을 레이어(기본: 전부)")]
        [SerializeField] private LayerMask _mask = ~0;
        [Tooltip("우리가 점을 찍는 동안 운영체제 화살표를 감춘다. " +
                 "끄면 화살표와 점이 둘 다 떠서 가리키는 것이 둘이 된다")]
        [SerializeField] private bool _hideSystemCursor = true;
        [Tooltip("점의 지름(화면 1732단위 기준). 저쪽 판의 조준점이 40 이다")]
        [SerializeField] private float _size = 40f;

        /// <summary>꾸러미의 결. 정적으로 들면 판을 넘겨 살아남되 내용이 죽는다 — 제 것으로 든다.</summary>
        private readonly IMUNROK.Ui.InventorySkin _skin = new IMUNROK.Ui.InventorySkin();

        private RectTransform _dot;
        private Image _img;
        private bool _hot;

        // 가만히 있을 때와 눌리는 것을 겨눴을 때. 저쪽 판이 조준점에 얹는 빛깔 그대로다.
        private static readonly Color Idle = new Color(1f, 0.95f, 0.8f, 0.95f);
        private static readonly Color Hot = new Color(1f, 1f, 1f, 1f);

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) _camera = Camera.main;
            Build();
        }

        /// <summary>
        /// 판을 세운다. 수첩(200)보다 <b>위</b>다 — 커서는 무엇을 가리키든 그 위에 있어야 한다.
        /// </summary>
        private void Build()
        {
            var go = new GameObject("조준_점", typeof(RectTransform));
            ScreenPanel.Raise(go, ScreenPanel.LayerBook + 50);
            var canvasRt = (RectTransform)go.transform;

            _dot = new GameObject("점", typeof(RectTransform), typeof(Image))
                   .GetComponent<RectTransform>();
            _dot.SetParent(canvasRt, false);
            // 왼쪽 아래를 기준점으로 잡는다 — 마우스 자리를 그대로 얹기 좋다.
            _dot.anchorMin = _dot.anchorMax = new Vector2(0f, 0f);
            _dot.pivot = new Vector2(0.5f, 0.5f);
            _dot.sizeDelta = new Vector2(_size, _size);

            _img = _dot.GetComponent<Image>();
            _img.sprite = _skin.Dot_;
            _img.color = Idle;
            _img.raycastTarget = false;   // 커서가 제 밑의 단추를 가리면 안 된다
        }

        /// <summary>
        /// 판이 떠 있는 동안에도 점은 남는다 — 저쪽 판도 그 위에 커서를 찍는다.
        /// 다만 종이를 들여다보는 동안(돋보기)에는 손이 유리에 가 있으므로 걷는다.
        /// </summary>
        private bool Hidden { get { return MagnifierLens.Peering; } }

        private void LateUpdate()
        {
            if (_dot == null) return;
            bool show = !Hidden;
            if (_dot.gameObject.activeSelf != show) _dot.gameObject.SetActive(show);
            ManageCursor(show);
            if (!show) { _hot = false; return; }

            _dot.anchoredPosition = MousePoint();

            _hot = LooksPressable();
            var want = _hot ? Hot : Idle;
            if (_img.color != want) _img.color = want;
            // 눌리는 것을 겨누면 한 뼘 커진다 — 빛깔만으로는 어두운 마당에서 잘 안 걸린다.
            _dot.sizeDelta = Vector2.one * (_hot ? _size * 1.3f : _size);
        }

        /// <summary>커서가 있는 화면 자리(왼쪽 아래 기준, 캔버스 단위).</summary>
        private Vector2 MousePoint()
        {
            float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState != CursorLockMode.Locked)
            {
                Vector2 m = mouse.position.ReadValue();
                x = Mathf.Clamp(m.x, 0f, Screen.width);
                y = Mathf.Clamp(m.y, 0f, Screen.height);
            }
#endif
            // 화면 화소를 캔버스 단위로 옮긴다. 판은 세로로 맞춰 놓았으므로 세로 비로 나눈다.
            float k = ScreenPanel.RefHeight / Mathf.Max(1f, Screen.height);
            return new Vector2(x * k, y * k);
        }

        /// <summary>겨눈 것이 <b>눌러서 무슨 일이 나는 것</b>인가.</summary>
        private bool LooksPressable()
        {
            // 판이 떠 있으면 세상을 겨누는 것이 아니다 — 그때는 단추를 짚는 중이다.
            if (InterrogationController.AnyOpen || JournalView.AnyOpen || DocumentView.IsOpen) return false;
            if (_camera == null) return false;

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
#else
            Ray ray = _camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
#endif
            RaycastHit info;
            if (!Physics.Raycast(ray, out info, _maxDistance, _mask, QueryTriggerInteraction.Ignore))
                return false;

            // 무엇이 나는지는 저마다의 안내가 말해 준다(살펴보기 판·자막). 여기서 이름까지
            // 적으려 들면 물건마다 부를 말을 새로 정해야 하고, 그 말이 없는 물건은 빠진다.
            return info.collider.GetComponentInParent<ISelectable>() != null
                || info.collider.GetComponentInParent<IInspectable>() != null;
        }

        /// <summary>
        /// <b>우리가 찍는 동안에는 진짜 커서를 감춘다.</b> 안 감추면 화살표와 점이 둘 다 떠서
        /// 가리키는 것이 둘이 된다. 시점을 마우스로 돌리는 씬(<see cref="TempWalker"/>)에서는
        /// 커서가 아예 잠겨 있다 — 그 자리는 그쪽이 맡으므로 손대지 않는다.
        /// </summary>
        private void ManageCursor(bool ours)
        {
            if (!_hideSystemCursor) return;
            if (Cursor.lockState == CursorLockMode.Locked) return;
            if (Cursor.visible == !ours) return;
            Cursor.visible = !ours;
        }

        private void OnDisable()
        {
            if (_hideSystemCursor && Cursor.lockState != CursorLockMode.Locked) Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (_dot != null && _dot.parent != null) Destroy(_dot.parent.gameObject);
        }
    }
}
