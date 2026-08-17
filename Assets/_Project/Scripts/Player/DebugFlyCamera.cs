using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 에디터/비-VR 테스트용 카메라. 두 모드(비행/걷기)를 Tab으로 전환.
    ///
    ///  ● 비행(Fly) — 자유롭게 날며 둘러보기(배치 확인용)
    ///  ● 걷기(Walk) — 사람처럼 눈높이 고정, 수평으로만 이동(잠행 동선 테스트용)
    ///
    /// [조작] — 마우스 오른쪽 버튼(RMB)을 "누르고 있는 동안"만 이동/회전
    ///   Tab            : 비행 ↔ 걷기 전환
    ///   G              : "바로 아래 바닥으로 내려서기" → 그 바닥을 걷는 높이로 잡음(걷기모드 자동 전환)
    ///   RMB + 마우스   : 시점 회전
    ///   RMB + W/A/S/D  : 이동
    ///   RMB + E / Q    : (비행 모드만) 위 / 아래
    ///   RMB + Shift    : 빠르게
    ///
    /// 화면 좌하단에 현재 모드/높이가 표시된다. RMB를 떼면 멈춘다(좌클릭 선택과 안 겹침).
    /// ※ G(바닥 내려서기)는 바닥에 Collider가 있어야 작동. 집 부품엔 대부분 Mesh Collider가 있음.
    /// </summary>
    public class DebugFlyCamera : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 3f;
        [Tooltip("픽셀당 회전 각도")]
        [SerializeField] private float _lookSpeed = 0.12f;
        [Tooltip("Shift로 빨라지는 배수")]
        [SerializeField] private float _sprintMultiplier = 3f;
        [Tooltip("걷기 모드로 시작할지")]
        [SerializeField] private bool _walkMode = false;
        [Tooltip("걷기/바닥내려서기 시 눈높이(바닥으로부터)")]
        [SerializeField] private float _eyeHeight = 1.6f;

        private float _yaw;
        private float _pitch;
        private float _walkY;
        private GUIStyle _hud;

        private void Start()
        {
            Vector3 e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x;
            _walkY = transform.position.y;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            // Tab: 비행 ↔ 걷기 (현재 높이를 눈높이로 고정)
            if (kb.tabKey.wasPressedThisFrame)
            {
                _walkMode = !_walkMode;
                _walkY = transform.position.y;
            }

            // G: 바로 아래 바닥으로 내려서서 그 높이를 걷는 눈높이로
            if (kb.gKey.wasPressedThisFrame)
            {
                if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out var hit, 200f))
                {
                    _walkMode = true;
                    _walkY = hit.point.y + _eyeHeight;
                    Vector3 p0 = transform.position; p0.y = _walkY; transform.position = p0;
                }
            }

            if (!mouse.rightButton.isPressed) return;

            // 시점 회전
            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * _lookSpeed;
            _pitch -= delta.y * _lookSpeed;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            float speed = _moveSpeed * (kb.leftShiftKey.isPressed ? _sprintMultiplier : 1f);

            if (_walkMode)
            {
                // 남이 나를 옮겼으면(순간이동) 그 높이를 받아들인다.
                // 이걸 안 하면 다음 줄에서 _walkY 로 되돌려 놓아, 마루로 올라간 순간
                // 다시 마당 높이로 끌어내려 머리가 바닥 밑에 처박힌다 — 화면이 캄캄해지는 원인이었다.
                if (Mathf.Abs(transform.position.y - _walkY) > 0.02f) _walkY = transform.position.y;

                // 수평(yaw 기준)으로만 이동, 높이 고정
                Vector3 fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
                Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
                Vector3 move = Vector3.zero;
                if (kb.wKey.isPressed) move += fwd;
                if (kb.sKey.isPressed) move -= fwd;
                if (kb.dKey.isPressed) move += right;
                if (kb.aKey.isPressed) move -= right;

                transform.position += move.normalized * speed * Time.deltaTime;

                // 발밑에서 "짧게" 아래로 쏴서 바닥을 따라감(지붕·처마로 튀지 않게)
                float feetY = transform.position.y - _eyeHeight;
                Vector3 origin = new Vector3(transform.position.x, feetY + 0.5f, transform.position.z);
                if (Physics.Raycast(origin, Vector3.down, out var gh, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float targetY = gh.point.y + _eyeHeight;
                    _walkY = Mathf.MoveTowards(_walkY, targetY, 4f * Time.deltaTime); // 계단·문턱만 천천히 오르내림
                }

                Vector3 p = transform.position;
                p.y = _walkY;
                transform.position = p;
            }
            else
            {
                Vector3 dir = Vector3.zero;
                if (kb.wKey.isPressed) dir += Vector3.forward;
                if (kb.sKey.isPressed) dir += Vector3.back;
                if (kb.aKey.isPressed) dir += Vector3.left;
                if (kb.dKey.isPressed) dir += Vector3.right;
                if (kb.eKey.isPressed) dir += Vector3.up;
                if (kb.qKey.isPressed) dir += Vector3.down;
                transform.Translate(dir.normalized * speed * Time.deltaTime, Space.Self);
            }
#endif
        }

        private void OnGUI()
        {
            if (_hud == null)
                _hud = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            string mode = _walkMode ? "<color=#8f8>걷기</color>" : "<color=#8cf>비행</color>";
            GUI.Label(new Rect(12, Screen.height - 46, 520, 22),
                $"카메라: {mode}  (Tab 전환 · G 바닥내려서기)  y={transform.position.y:0.0}", _hud);
        }
    }
}
