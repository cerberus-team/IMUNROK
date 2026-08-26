using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Ui
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

        /// <summary>UI(소지품 판 등)가 조작을 가져갔다 — 이동·시선·Esc·R을 전부 놓는다.
        /// ⚠️ 처음엔 "시선은 남긴다"였다(고개로 판을 가리키는 VR 방식). 그런데 데스크톱에서는
        ///    마우스가 시선과 조준을 겸할 수 없어 **커서가 화면 한가운데 못 박히고 마우스를 움직이면
        ///    머리만 돌아갔다**(2026-08-24 실측: 마우스 우측 이동 → yaw 180°→228°, 조준점은 (0,0) 고정).
        ///    지금은 판이 열리면 커서를 풀어 마우스로 직접 가리킨다 — 시선은 그동안 멈춘다.</summary>
        [HideInInspector] public bool uiOpen;

        /// <summary>드래그로 무언가를 돌리는 중 — 시선까지 멈춘다 (마우스가 두 일을 겸하지 않게).</summary>
        [HideInInspector] public bool lookLocked;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            Application.runInBackground = true;   // 에디터 포커스 없어도 게임 루프 유지 (원격 검증용이기도)
            spawnPos = transform.position;         // R키 탈출용 (끼임 대비)
            spawnRot = transform.rotation;

            // ⚠️ 조준점·안내 텍스트가 통째로 안 나오는 사고 방지 (2026-08-20)
            //   워커는 씬마다 다른 설치 메뉴가 만드는데, 어떤 씬(은하담)에는 눈에
            //   DebugInteractor가 빠져 있었다. 그러면 어떤 오브젝트도 조준되지 않아
            //   "이 씬만 상호작용이 안 된다"로 보인다 — 원인 찾기 어려운 종류의 결함이다.
            //   워커가 있으면 조준 입력도 반드시 있게 여기서 보강한다.
            if (eye == null) return;
            if (eye.GetComponent<DebugInteractor>() == null) eye.gameObject.AddComponent<DebugInteractor>();
            if (eye.GetComponent<InventoryInput>() == null) eye.gameObject.AddComponent<InventoryInput>();
            if (EyeSetup != null) EyeSetup(eye.gameObject);
        }

        /// <summary>
        /// <b>눈에 더 붙일 것이 있으면 채워 넣는 자리</b> (2026-08-26).
        ///
        /// 위의 조준·소지품 입력은 UI 꾸러미와 함께 다니므로 늘 붙는다. 그 밖에 자기 사건에만
        /// 필요한 부품이 있으면 여기에 등록하면 된다. 비워 두어도 된다 — 기본값이 비어 있다.
        ///
        /// ⚠️ 도메인 리로드가 꺼진 프로젝트다. 등록하는 쪽은
        ///    <see cref="RuntimeInitializeOnLoadMethod"/> 로 <b>세션마다 다시 걸 것.</b>
        /// </summary>
        public static System.Action<GameObject> EyeSetup;

        // 도메인 리로드가 꺼진 프로젝트 — 정적 훅이 세션을 넘겨 살아남으므로 직접 비운다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { EyeSetup = null; }

        void OnEnable() => SetCursorLock(true);
        void OnDisable() => SetCursorLock(false);

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            // Esc·R은 판이 떠 있는 동안 UI 쪽(뒤로 가기)이 가져간다
            if (!uiOpen && kb.escapeKey.wasPressedThisFrame) SetCursorLock(false);
            if (!uiOpen && kb.rKey.wasPressedThisFrame)      // 끼임 탈출: 스폰으로 복귀
            {
                cc.enabled = false;
                transform.SetPositionAndRotation(spawnPos, spawnRot);
                fallSpeed = 0f;
                cc.enabled = true;
            }
            // 판이 떠 있는 동안에는 커서를 다시 잡지 않는다 — 그 커서로 판을 가리키는 중이다
            if (!uiOpen && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLock(true);

            if (Cursor.lockState == CursorLockMode.Locked && !lookLocked && !uiOpen)
            {
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                if (eye != null) eye.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }

            // 판이 떠 있는 동안에는 제자리 — 중력만 계속 먹인다
            Vector2 wasd = Vector2.zero;
            if (!uiOpen)
            {
                if (kb.wKey.isPressed) wasd.y += 1f;
                if (kb.sKey.isPressed) wasd.y -= 1f;
                if (kb.dKey.isPressed) wasd.x += 1f;
                if (kb.aKey.isPressed) wasd.x -= 1f;
            }
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
