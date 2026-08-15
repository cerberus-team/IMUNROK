using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 배경 확인용 임시 걷기 컨트롤러 (VR 리그 전까지, Game 뷰 전용).
    /// WASD 이동 / 마우스 시선 / Shift 질주 / Esc 커서 해제(좌클릭으로 재잠금).
    /// 눈높이 1.7, 중력·계단(stepOffset)·경사는 CharacterController가 처리.
    /// 프로젝트가 새 Input System 전용(activeInputHandler=1)이라 InputSystem API 사용.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DebugWalkController : MonoBehaviour
    {
        public float walkSpeed = 3f;
        public float sprintSpeed = 9f;
        public float mouseSensitivity = 0.12f;
        public Transform eye;   // y 1.7 자식 (카메라)

        CharacterController cc;
        float pitch;

        /// <summary>시선 핏치 (도, +아래/−위). 연출이 시선을 부드럽게 유도할 때 쓴다 —
        /// 여기로 세팅해 두면 조작 복귀 때 시선이 튀지 않는다.</summary>
        public float Pitch
        {
            get => pitch;
            set
            {
                pitch = Mathf.Clamp(value, -85f, 85f);
                if (eye != null) eye.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }
        }
        float fallSpeed;
        Vector3 spawnPos;
        Quaternion spawnRot;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            Application.runInBackground = true;   // 에디터 포커스 없어도 게임 루프 유지 (원격 검증용이기도)
            spawnPos = transform.position;         // R키 탈출용 (끼임 대비)
            spawnRot = transform.rotation;
        }

        void OnEnable() => SetCursorLock(true);
        void OnDisable() => SetCursorLock(false);

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (kb.escapeKey.wasPressedThisFrame) SetCursorLock(false);
            if (kb.rKey.wasPressedThisFrame)      // 끼임 탈출: 스폰으로 복귀
            {
                cc.enabled = false;
                transform.SetPositionAndRotation(spawnPos, spawnRot);
                fallSpeed = 0f;
                cc.enabled = true;
            }
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLock(true);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                if (eye != null) eye.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }

            Vector2 wasd = Vector2.zero;
            if (kb.wKey.isPressed) wasd.y += 1f;
            if (kb.sKey.isPressed) wasd.y -= 1f;
            if (kb.dKey.isPressed) wasd.x += 1f;
            if (kb.aKey.isPressed) wasd.x -= 1f;
            float speed = kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;

            Vector3 move = (transform.right * wasd.x + transform.forward * wasd.y);
            move = Vector3.ClampMagnitude(move, 1f) * speed;
            fallSpeed = cc.isGrounded ? -1f : fallSpeed - 20f * Time.deltaTime;
            move.y = fallSpeed;
            cc.Move(move * Time.deltaTime);
        }

        static void SetCursorLock(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }
    }
}
