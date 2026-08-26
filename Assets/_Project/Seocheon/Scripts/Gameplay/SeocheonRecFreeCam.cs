using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 촬영용 자유 카메라 (2026-08-27).
    ///
    /// ■ <see cref="IMUNROK.Common.DebugFlyCamera"/> 와 무엇이 다른가
    ///   그쪽은 <b>오른쪽 버튼을 누르고 있는 동안만</b> 움직인다 — 디버그 키와 안 겹치게 하려는 설계다.
    ///   촬영할 때는 그게 불편하다. 한 손으로 버튼을 붙들고 다른 손으로 이동키를 누르면
    ///   손이 굳어 <b>화면이 떨린다</b>. 그래서 여기서는 이동을 버튼 없이 항상 받는다.
    ///
    /// ■ 조작
    ///   W / S            앞 / 뒤
    ///   A / D            왼 / 오른
    ///   E / Q            위 / 아래          ← 크레인 업은 E
    ///   마우스 우클릭 + 이동   시점 회전 (누르는 동안만 — 놓으면 각도가 고정된다)
    ///   Shift            빠르게
    ///   Ctrl             느리게             ← 미세 조정·부드러운 마무리에 쓴다
    ///   ↑ / ↓            시야각(FOV) 좁히기 / 넓히기
    ///
    /// ■ 왜 회전만 우클릭인가
    ///   놓는 순간 각도가 그대로 멈춰 준다. 촬영 중 «여기서 각도 고정하고 위로만 올리기» 가
    ///   자주 필요한데, 그때 마우스를 놓기만 하면 된다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class SeocheonRecFreeCam : MonoBehaviour
    {
        [Tooltip("기본 이동 속도 (m/초). 촬영은 느린 편이 보기 좋다.")]
        [SerializeField] private float moveSpeed = 3f;
        [Tooltip("Shift 배수")]
        [SerializeField] private float fastMultiplier = 4f;
        [Tooltip("Ctrl 배수 — 1보다 작게")]
        [SerializeField] private float slowMultiplier = 0.25f;
        [Tooltip("마우스 픽셀당 회전 각도")]
        [SerializeField] private float lookSpeed = 0.08f;
        [Tooltip("가감속 부드럽기(초). 0이면 즉시 — 클수록 미끄러지듯 움직인다.")]
        [SerializeField] private float smoothing = 0.25f;
        [Tooltip("↑/↓ 로 FOV 를 바꾸는 속도(도/초)")]
        [SerializeField] private float fovSpeed = 12f;

        private Camera cam;
        private float yaw, pitch;
        private Vector3 vel;      // 부드러운 가감속용

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
            Vector3 e = transform.eulerAngles;
            yaw = e.y;
            // ★eulerAngles 는 0~360 이라 24° 와 336°(=-24°) 를 구분 못 한다. -180~180 으로 되돌린다.
            pitch = e.x > 180f ? e.x - 360f : e.x;
            vel = Vector3.zero;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            // ── 시점 — 우클릭 누르는 동안만 ──
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                yaw += d.x * lookSpeed;
                pitch = Mathf.Clamp(pitch - d.y * lookSpeed, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // ── 이동 — 버튼 없이 항상 ──
            Vector3 dir = Vector3.zero;
            if (kb.wKey.isPressed) dir += Vector3.forward;
            if (kb.sKey.isPressed) dir += Vector3.back;
            if (kb.aKey.isPressed) dir += Vector3.left;
            if (kb.dKey.isPressed) dir += Vector3.right;
            if (kb.eKey.isPressed) dir += Vector3.up;
            if (kb.qKey.isPressed) dir += Vector3.down;

            float speed = moveSpeed;
            if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) speed *= fastMultiplier;
            if (kb.leftCtrlKey.isPressed  || kb.rightCtrlKey.isPressed)  speed *= slowMultiplier;

            Vector3 target = dir.sqrMagnitude > 0f ? dir.normalized * speed : Vector3.zero;
            // 부드러운 가감속 — 손으로 눌렀다 뗄 때 툭 끊기지 않게
            vel = smoothing <= 0f ? target
                                  : Vector3.Lerp(vel, target, 1f - Mathf.Exp(-Time.deltaTime / smoothing));
            transform.Translate(vel * Time.deltaTime, Space.Self);

            // ── FOV ──
            if (cam != null)
            {
                if (kb.upArrowKey.isPressed)   cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - fovSpeed * Time.deltaTime, 15f, 100f);
                if (kb.downArrowKey.isPressed) cam.fieldOfView = Mathf.Clamp(cam.fieldOfView + fovSpeed * Time.deltaTime, 15f, 100f);
            }
        }
    }
}
