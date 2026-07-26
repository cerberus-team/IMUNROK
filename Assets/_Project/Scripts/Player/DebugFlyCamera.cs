using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 에디터/비-VR 테스트용 자유 비행 카메라.
    /// VR 이동(텔레포트)을 붙이기 전에, Play 화면에서 방 안을 둘러보며
    /// 네 구역(사건판·기록대·도구선반·봉서함)을 확인할 수 있게 해준다.
    ///
    /// [조작] — 마우스 오른쪽 버튼(RMB)을 "누르고 있는 동안"만 작동
    ///   RMB 누른 채 마우스 이동 : 시점 회전(둘러보기)
    ///   RMB + W/A/S/D          : 앞/왼/뒤/오른 이동
    ///   RMB + E / Q            : 위 / 아래 이동
    ///   RMB + Shift            : 빠르게 이동
    ///
    /// RMB를 떼면 카메라는 멈추고, 평소처럼 좌클릭으로 사건 큐브를 선택하거나
    /// 디버그 키(1/2/3, T/M/F 등)를 쓸 수 있다. (그래서 이동키와 디버그키가 안 겹침)
    /// </summary>
    public class DebugFlyCamera : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 3f;
        [Tooltip("픽셀당 회전 각도")]
        [SerializeField] private float _lookSpeed = 0.12f;
        [Tooltip("Shift로 빨라지는 배수")]
        [SerializeField] private float _sprintMultiplier = 3f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            // 오른쪽 버튼을 누르고 있을 때만 카메라 조작(디버그 키와 충돌 방지)
            if (!mouse.rightButton.isPressed) return;

            // 시점 회전
            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * _lookSpeed;
            _pitch -= delta.y * _lookSpeed;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // 이동
            Vector3 dir = Vector3.zero;
            if (kb.wKey.isPressed) dir += Vector3.forward;
            if (kb.sKey.isPressed) dir += Vector3.back;
            if (kb.aKey.isPressed) dir += Vector3.left;
            if (kb.dKey.isPressed) dir += Vector3.right;
            if (kb.eKey.isPressed) dir += Vector3.up;
            if (kb.qKey.isPressed) dir += Vector3.down;

            float speed = _moveSpeed * (kb.leftShiftKey.isPressed ? _sprintMultiplier : 1f);
            transform.Translate(dir.normalized * speed * Time.deltaTime, Space.Self);
#endif
        }
    }
}
