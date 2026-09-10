using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>지금 무엇을 겨누고 있는지 한 점으로 알린다.</b>
    ///
    /// 우리 화면에는 <b>겨누는 표시가 아예 없었다</b> — 운영체제의 흰 화살표 하나뿐이다.
    /// 그것은 창을 닫고 파일을 여는 화살표라, 조선의 밤 마당 위에 떠 있으면 그 순간
    /// 화면이 <b>게임이 아니라 프로그램</b>이 된다. 게다가 화살표는 겨눈 것이 눌리는
    /// 물건인지 아닌지를 말해 주지 않아, 물건 앞에 서서도 눌러 봐야 알았다.
    ///
    /// 견우 화면에는 그 표시가 있다(<c>IMUNROK.Ui.AimPanel</c>). 화면을 나란히 놓고 보면
    /// 그쪽만 조작이 보이고 이쪽은 안 보인다 — 같은 게임으로 안 읽히는 첫째 까닭이다.
    ///
    /// <b>저쪽 규약을 그대로 옮긴다.</b> 값을 새로 정하지 않는다:
    ///   · 겨눈 것이 없을 때 — <c>·</c> 흰빛 45%
    ///   · 겨눈 것이 있을 때 — <c>◆</c> 노랑
    /// 색은 <see cref="UiLook.AimIdle"/>·<see cref="UiLook.AimHot"/> 로 받아 온다.
    ///
    /// <b>글씨는 조선궁서체다.</b> 저쪽은 TMP 라 글리프 모양 기준으로 가운데를 맞추는데
    /// (베이스라인에 맡기면 ◆ 가 4~5화소 내려앉는다), 낡은 IMGUI 에는 그 손잡이가 없다.
    /// 그래서 여기서는 <b>칸을 조금 올려</b> 눈으로 맞춘다.
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
        [Tooltip("우리가 조준점을 그리는 동안 운영체제 화살표를 감춘다. " +
                 "끄면 화살표와 조준점이 둘 다 떠서 가리키는 것이 둘이 된다")]
        [SerializeField] private bool _hideSystemCursor = true;

        private bool _hot;
        private GUIStyle _mark;
        private int _styledFor;

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) _camera = Camera.main;
        }

        /// <summary>
        /// 판이 떠 있는 동안에는 안 그린다.
        ///
        /// 심문·수첩·종이를 펴면 겨누는 일이 아니라 <b>읽는 일</b>이고, 그때는 커서가
        /// 풀려 진짜 마우스가 뜬다. 그 위에 조준점까지 겹치면 가리키는 것이 둘이 된다.
        /// </summary>
        private bool Hidden
        {
            get
            {
                return InterrogationController.AnyOpen || JournalView.AnyOpen
                    || DocumentView.IsOpen || MagnifierLens.Peering;
            }
        }

        private void Update()
        {
            ManageCursor();
            if (_camera == null || Hidden) { _hot = false; return; }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) { _hot = false; return; }
            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
#else
            Ray ray = _camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
#endif
            RaycastHit info;
            if (!Physics.Raycast(ray, out info, _maxDistance, _mask, QueryTriggerInteraction.Ignore))
            {
                _hot = false;
                return;
            }

            // <b>「눌러서 무슨 일이 나는가」만 본다.</b> 무엇이 나는지는 저마다의 안내가
            // 말해 준다(살펴보기 판·자막). 여기서 이름까지 적으려 들면 물건마다 부를 말을
            // 새로 정해야 하고, 그 말이 없는 물건은 조용히 빠진다.
            _hot = info.collider.GetComponentInParent<ISelectable>() != null
                || info.collider.GetComponentInParent<IInspectable>() != null;
        }

        private void OnGUI()
        {
            if (Hidden) return;
            EnsureStyle();

            // 겨누는 자리는 커서가 있는 곳이다. 잠겨 있으면(시점을 마우스로 돌리는 씬)
            // 유니티가 커서를 화면 한복판에 잡아 두므로 한복판에 그린다.
            float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState != CursorLockMode.Locked)
            {
                Vector2 p = mouse.position.ReadValue();
                x = p.x; y = Screen.height - p.y;      // IMGUI 는 위에서 아래로 잰다
            }
#endif
            float box = Mathf.Round(Screen.height * 0.03f);
            _mark.normal.textColor = _hot ? UiLook.AimHot : UiLook.AimIdle;
            // 베이스라인 탓에 글리프가 조금 내려앉는다 — 칸을 그만큼 올려 눈으로 맞춘다.
            GUI.Label(new Rect(x - box * 0.5f, y - box * 0.5f - box * 0.12f, box, box),
                      _hot ? "◆" : "·", _mark);
        }

        /// <summary>
        /// <b>우리가 그리는 동안에는 진짜 커서를 감춘다.</b>
        ///
        /// 안 감추면 화살표와 조준점이 <b>둘 다</b> 떠서 가리키는 것이 둘이 된다.
        /// 감추기만 하고 돌려주지 않아도 안 된다 — 판이 뜨면 단추를 눌러야 하고,
        /// 그때는 운영체제의 화살표가 있어야 어디를 누르는지 몸에 익은 대로 읽힌다.
        ///
        /// 시점을 마우스로 돌리는 씬(<see cref="TempWalker"/>)에서는 커서가 아예
        /// 잠겨 있다. 그 자리는 그쪽이 맡으므로 손대지 않는다.
        /// </summary>
        private void ManageCursor()
        {
            if (!_hideSystemCursor) return;
            if (Cursor.lockState == CursorLockMode.Locked) return;
            bool want = Hidden;                 // 판이 떠 있을 때만 진짜 커서
            if (Cursor.visible != want) Cursor.visible = want;
        }

        private void OnDisable()
        {
            if (_hideSystemCursor && Cursor.lockState != CursorLockMode.Locked) Cursor.visible = true;
        }

        private void EnsureStyle()
        {
            if (_mark != null && _styledFor == Screen.height) return;
            _styledFor = Screen.height;
            _mark = new GUIStyle(GUI.skin.label)
            {
                font = UiFont.Resolve(null),
                fontSize = Mathf.RoundToInt(Screen.height * 0.019f),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
            };
        }
    }
}
