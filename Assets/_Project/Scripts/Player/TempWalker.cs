using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 임시 PC 워커 — VR(Meta 네이티브) 리그를 붙이기 전, 에디터/PC에서 실제로
    /// 걸어다니며 경계 콜라이더(개천 링·성 밖 외곽·담·건물)를 검증하기 위한 이동 스크립트.
    /// ★파라미터는 최종 Meta 리그와 동일하게 맞춘다(CharacterController 쪽).
    ///
    /// [조작]
    ///   WASD           : 수평 이동(2.0m/s), Shift로 4.0m/s
    ///   마우스 좌우      : 워커 yaw 회전
    ///   마우스 상하      : 카메라 pitch(±80° 클램프, 카메라만)
    ///   Esc            : 커서 잠금 토글
    ///   중력 −9.81, 접지 시 vy 리셋. 점프 없음.
    ///
    /// DebugFlyCamera는 수정하지 않는다(별도 파일). 이 스크립트는 New Input System 전용.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TempWalker : MonoBehaviour
    {
        [SerializeField] private float _walkSpeed = 2.0f;
        [SerializeField] private float _runSpeed = 4.0f;
        [Tooltip("픽셀당 회전 각도")]
        [SerializeField] private float _lookSpeed = 0.12f;
        [SerializeField] private float _gravity = -9.81f;
        [Tooltip("pitch를 적용할 카메라(비우면 자식 카메라 자동 탐색)")]
        [SerializeField] private Transform _cameraPivot;

        private CharacterController _cc;
        private float _pitch;
        private float _vy;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) _cameraPivot = cam.transform;
            }
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (_cameraPivot != null) _pitch = _cameraPivot.localEulerAngles.x;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            // Esc: 커서 잠금 토글
            if (kb.escapeKey.wasPressedThisFrame)
            {
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = locked;
            }

            // 시점: 마우스 X = yaw(워커 전체), Y = pitch(카메라만)
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = mouse.delta.ReadValue();
                transform.Rotate(0f, d.x * _lookSpeed, 0f, Space.World);
                _pitch -= d.y * _lookSpeed;
                _pitch = Mathf.Clamp(_pitch, -80f, 80f);
                if (_cameraPivot != null)
                    _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            // 수평 입력 → 워커 로컬 방향
            Vector3 input = Vector3.zero;
            if (kb.wKey.isPressed) input += Vector3.forward;
            if (kb.sKey.isPressed) input += Vector3.back;
            if (kb.aKey.isPressed) input += Vector3.left;
            if (kb.dKey.isPressed) input += Vector3.right;
            float speed = kb.leftShiftKey.isPressed ? _runSpeed : _walkSpeed;
            Vector3 horiz = transform.TransformDirection(input.normalized) * speed;

            // 중력(점프 없음). 접지 시 vy 리셋.
            if (_cc.isGrounded && _vy < 0f) _vy = -2f;
            _vy += _gravity * Time.deltaTime;

            Vector3 vel = horiz + Vector3.up * _vy;
            _cc.Move(vel * Time.deltaTime);
#endif
        }
    }
}
