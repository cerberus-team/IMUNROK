using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 가리키개 — <b>마우스와 컨트롤러 광선을 갈아끼우는 한 곳</b> (2026-08-26).
    ///
    /// ■ 왜 인터페이스인가
    ///   조사에서 확인했듯 이 프로젝트의 조준은 이미 전부 <b>광선 + 콜라이더</b>다
    ///   (EventSystem은 대화창 글쇠 칸에만 쓰인다). 그래서 VR로 갈 때 바꿀 것은
    ///   "광선을 어디서 쏘는가" 하나뿐이다. 그 하나를 여기로 모았다.
    ///
    /// ■ 광선이 둘인 이유
    ///   <see cref="PointRay"/> — <b>판</b>을 가리킨다. PC에서는 마우스 커서 위치에서 쏜다.
    ///   <see cref="GazeRay"/>  — <b>세상</b>을 조준한다. PC에서는 화면 정중앙(=시선)이다.
    ///   PC에서 이 둘이 다른 까닭: 마우스 하나가 시선과 조준을 겸할 수 없다. 판이 열리면
    ///   시선을 멈추고 커서를 풀어 가리키게 한다(2026-08-24 실측으로 정해진 규약).
    ///   VR에서는 컨트롤러가 둘 다 맡으므로 <b>같은 광선</b>이 된다.
    ///
    /// ■ Delta 는 VR에 대응물이 없다 — 그래서 각도를 픽셀로 환산한다
    ///   혼상·혼천의 회전이 <c>HandleDrag(Vector2 델타)</c> 에 매달려 있다. 컨트롤러에는
    ///   픽셀 델타가 없으므로, <b>광선이 돌아간 각도 × <see cref="XrUiPointer.PixelsPerDegree"/></b>
    ///   를 델타로 준다. 퍼즐 코드는 한 줄도 고치지 않는다.
    ///   ⚠️ 이 환산 계수는 <b>손맛을 헤드셋에서 맞춰 봐야 하는 값</b>이다. 지금은 추정치다.
    /// </summary>
    public interface IUiPointer
    {
        /// <summary>이 가리키개를 지금 쓸 수 있는가 (마우스 없음·컨트롤러 미추적이면 false).</summary>
        bool Available { get; }

        /// <summary>판을 가리키는 광선.</summary>
        Ray PointRay { get; }
        /// <summary>세상을 조준하는 광선.</summary>
        Ray GazeRay { get; }

        bool PressDown { get; }
        bool PressHeld { get; }
        bool PressUp { get; }

        /// <summary>드래그 델타(픽셀 상당).</summary>
        Vector2 Delta { get; }
        /// <summary>휠 값 그대로 — 한 칸 판정은 쓰는 쪽이 한다.</summary>
        float ScrollRaw { get; }

        /// <summary>물러나기 (Esc / 우클릭 · B·Y 버튼).</summary>
        bool BackDown { get; }
        /// <summary>소지품 여닫기 (I · 메뉴 버튼).</summary>
        bool MenuDown { get; }

        /// <summary>말하기 (왼쪽 Ctrl · 그립) — 누르고 있는 동안 녹음.</summary>
        bool TalkHeld { get; }
        bool TalkDown { get; }
        bool TalkUp { get; }

        /// <summary>한 프레임 갱신. <paramref name="eye"/> 는 플레이어 카메라의 트랜스폼.</summary>
        void Tick(Transform eye);
    }

    /// <summary>
    /// 지금 쓰는 가리키개. 모드가 바뀌면 저절로 갈린다.
    /// 쓰는 쪽은 <c>UiPointers.Get(eye)</c> 한 줄이면 된다 — 프레임당 한 번만 갱신된다.
    /// </summary>
    public static class UiPointers
    {
        static MouseUiPointer _mouse;
        static XrUiPointer _xr;
        static int _tickedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _mouse = null; _xr = null; _tickedFrame = -1; }

        /// <summary>
        /// 이번 프레임의 가리키개.
        ///
        /// ⚠️ VR 모드인데 컨트롤러가 안 잡히면 <b>마우스로 되돌린다.</b> 그래야
        ///    헤드셋 없이 「VR 고정」으로 배치를 확인할 때도 조작이 살아 있다 —
        ///    지금 우리가 VR을 볼 수 있는 유일한 방법이라 이 폴백이 꼭 필요하다.
        /// </summary>
        public static IUiPointer Get(Transform eye)
        {
            if (_mouse == null) _mouse = new MouseUiPointer();
            if (_xr == null) _xr = new XrUiPointer();

            // XR 쪽은 눌림 판정을 자기가 들고 있어 프레임당 한 번만 돌아야 한다 (안에서 막는다).
            if (UiModes.IsVr) _xr.Tick(eye);

            if (UiModes.IsVr && _xr.Available) return _xr;

            // 마우스 쪽은 그때그때 읽기만 하므로 여러 번 불러도 값이 같다.
            _mouse.Tick(eye);
            _tickedFrame = Time.frameCount;
            return _mouse;
        }

        /// <summary>컨트롤러가 실제로 잡히고 있는가 — 진단용.</summary>
        public static bool XrControllerTracked => _xr != null && _xr.Available;
    }

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// 마우스·키보드 가리키개 (PC).
    ///
    /// ⚠️ 이 클래스는 <b>지금 InventoryInput·DebugFocusRig·DebugInteractor가 하던 것을
    ///    그대로 옮겨 적은 것</b>이다. 새 규칙을 넣지 않았다 — PC 사용감이 바뀌면 안 된다.
    /// </summary>
    public class MouseUiPointer : IUiPointer
    {
        Ray point, gaze;
        Vector2 delta;
        float scroll;

        public bool Available => Mouse.current != null && Keyboard.current != null;

        public Ray PointRay => point;
        public Ray GazeRay => gaze;

        public bool PressDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public bool PressHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public bool PressUp => Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

        public Vector2 Delta => delta;
        public float ScrollRaw => scroll;

        public bool BackDown =>
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);

        public bool MenuDown => Keyboard.current != null && Keyboard.current[Key.I].wasPressedThisFrame;

        public bool TalkHeld => Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
        public bool TalkDown => Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame;
        public bool TalkUp => Keyboard.current != null && Keyboard.current.leftCtrlKey.wasReleasedThisFrame;

        public void Tick(Transform eye)
        {
            var m = Mouse.current;
            delta = m != null ? m.delta.ReadValue() : Vector2.zero;
            scroll = m != null ? m.scroll.ReadValue().y : 0f;

            if (eye != null) gaze = new Ray(eye.position, eye.forward);

            // ⚠️ 카메라 정면으로 쏘면 안 된다 — 조준점이 화면 한가운데 못 박혀 마우스로
            //    칸을 고를 수가 없다 (2026-08-24 실측). 커서 위치에서 쏜다.
            var cam = eye != null ? eye.GetComponent<Camera>() : null;
            if (cam != null && m != null) point = cam.ScreenPointToRay(m.position.ReadValue());
            else point = gaze;
        }
    }

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// 컨트롤러 광선 가리키개 (VR).
    ///
    /// ■ 왜 <c>UnityEngine.XR.InputDevices</c> 인가
    ///   엔진 기본 모듈(<c>com.unity.modules.xr</c>)에 들어 있어 <b>추가 패키지 참조 없이</b>
    ///   어느 XR 공급자(Meta·OpenXR)에서도 같은 코드가 돈다. 지금 이 프로젝트에는 XR 리그도
    ///   로더 설정도 없어서 어차피 아무 기기도 안 잡히는데, 리그가 붙는 날 <b>이 파일은
    ///   고치지 않아도</b> 그대로 살아나야 하기 때문이다.
    ///
    /// ⚠️ <b>여기는 통째로 미검증이다.</b> 헤드셋이 없어 한 줄도 실물로 확인하지 못했다.
    ///    특히 두 가지가 틀릴 수 있다:
    ///    ① 기준 좌표계 — 컨트롤러 자세는 <b>추적 공간</b> 기준이라 리그 루트를 곱해야 한다.
    ///       여기서는 카메라의 부모를 리그 루트로 본다. 실제 리그 구조가 다르면 광선이 어긋난다.
    ///    ② 버튼 배치 — 트리거/B·Y/메뉴/그립을 표준 usage로 읽지만, 기기마다 다를 수 있다.
    /// </summary>
    public class XrUiPointer : IUiPointer
    {
        /// <summary>광선이 1° 돌면 몇 픽셀 움직인 것으로 칠지. 혼상·혼천의 회전 감도를 정한다.
        /// ⚠️ 손맛 값 — 헤드셋에서 맞춰야 한다. 12는 "마우스 감도 0.12로 12px 움직인 만큼"에서 잡은 추정치.</summary>
        public const float PixelsPerDegree = 12f;

        Ray ray;
        Vector2 delta;
        float scroll;
        Vector3 lastDir;
        bool hasLast;
        bool tracked;
        bool trigNow, trigPrev;
        bool backNow, backPrev;
        bool menuNow, menuPrev;
        bool talkNow, talkPrev;
        int lastFrame = -1;

        public bool Available => tracked;

        public Ray PointRay => ray;
        public Ray GazeRay => ray;   // VR에서는 가리키는 것과 조준하는 것이 같다

        public bool PressDown => trigNow && !trigPrev;
        public bool PressHeld => trigNow;
        public bool PressUp => !trigNow && trigPrev;

        public Vector2 Delta => delta;
        public float ScrollRaw => scroll;

        public bool BackDown => backNow && !backPrev;
        public bool MenuDown => menuNow && !menuPrev;

        public bool TalkHeld => talkNow;
        public bool TalkDown => talkNow && !talkPrev;
        public bool TalkUp => !talkNow && talkPrev;

        public void Tick(Transform eye)
        {
            if (lastFrame == Time.frameCount) return;   // 프레임당 한 번만 — 눌림 판정이 두 번 소모되지 않게
            lastFrame = Time.frameCount;

            trigPrev = trigNow; backPrev = backNow; menuPrev = menuNow; talkPrev = talkNow;

            var dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            if (!dev.isValid) dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);

            Vector3 pos; Quaternion rot;
            tracked = dev.isValid
                   && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out pos)
                   && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rot);
            if (!tracked)
            {
                hasLast = false;
                delta = Vector2.zero; scroll = 0f;
                trigNow = backNow = menuNow = talkNow = false;
                return;
            }
            dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out pos);
            dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rot);

            // 추적 공간 → 월드. 리그 루트는 카메라의 부모로 본다 (⚠️ 위 주석 ①).
            Transform origin = eye != null ? eye.parent : null;
            Vector3 wPos = origin != null ? origin.TransformPoint(pos) : pos;
            Quaternion wRot = origin != null ? origin.rotation * rot : rot;
            ray = new Ray(wPos, wRot * Vector3.forward);

            // 각도 변화를 픽셀 델타로 — 퍼즐의 HandleDrag(Vector2)가 그대로 먹게
            Vector3 dir = ray.direction;
            if (hasLast)
            {
                Vector3 up = origin != null ? origin.up : Vector3.up;
                Vector3 right = Vector3.Cross(up, lastDir).normalized;
                Vector3 trueUp = Vector3.Cross(lastDir, right).normalized;
                float dx = Mathf.Asin(Mathf.Clamp(Vector3.Dot(dir, right), -1f, 1f)) * Mathf.Rad2Deg;
                float dy = Mathf.Asin(Mathf.Clamp(Vector3.Dot(dir, trueUp), -1f, 1f)) * Mathf.Rad2Deg;
                delta = new Vector2(dx, dy) * PixelsPerDegree;
            }
            else delta = Vector2.zero;
            lastDir = dir; hasLast = true;

            bool b;
            trigNow = dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out b) && b;
            backNow = dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out b) && b;
            menuNow = dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out b) && b;
            if (!menuNow) menuNow = dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out b) && b;
            talkNow = dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out b) && b;

            Vector2 stick;
            scroll = dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out stick)
                   ? (Mathf.Abs(stick.y) > 0.6f ? Mathf.Sign(stick.y) : 0f)
                   : 0f;
        }
    }
}
