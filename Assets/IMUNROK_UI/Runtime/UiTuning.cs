using UnityEngine;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 모드별 배치 수치 — <b>PC와 VR이 갈리는 곳은 여기 한 파일뿐</b>이다 (2026-08-26).
    ///
    /// ■ 핵심 아이디어 : 판을 '화면 픽셀 단위'로 짓는다
    ///   월드 캔버스라도 <b>캔버스 1단위 = 화면 1픽셀</b>이 되도록 배율을 잡아 두면,
    ///   지금 IMGUI가 쓰던 픽셀 좌표·글자 크기를 <b>숫자 그대로 옮겨 담을 수 있다</b>.
    ///   그래서 PC 모드에서는 교체 전후가 같아 보인다 — 이 프로젝트의 요구다.
    ///   배율 = (그 거리에서의 화면 실제 높이) ÷ (기준 단위 수).
    ///     PC : 기준 = Screen.height, 화각 = 카메라 화각  → 1단위 = 1픽셀 (완전 일치)
    ///     VR : 기준 = <see cref="VrRefHeight"/>, 화각 = <see cref="VrContentVFov"/>
    ///
    /// ■ VR 쪽 숫자를 이렇게 잡은 이유
    ///   ① 화각 42° × 61° — Quest 3의 시야(가로 약 110°)보다 훨씬 좁게 잡았다. 판이 시야를
    ///      다 덮으면 모서리를 보려고 <b>눈을 크게 굴려야</b> 하고, 렌즈 주변부는 흐리다.
    ///      "고개를 크게 돌리지 않아도 전체가 보이는" 자리는 대략 ±30° 안쪽이다.
    ///   ② 기준 높이 388 — IMGUI 시절 12~18px 글자를 그대로 옮겨도 <b>1.3°~1.9°</b>가 되게
    ///      역산한 값이다(42 ÷ 388 = 0.108°/단위). VR에서 편히 읽히는 하한이 약 1.3°다.
    ///      숫자 하나만 만지면 <b>글자·상자·여백이 함께</b> 커지고 작아진다 — 배치가 안 깨진다.
    ///   ③ 거리 1.0 m — 0.75~2 m 가 초점이 편한 구간이다. 실내에서 벽에 박히면
    ///      <see cref="VrPanel"/> 이 앞으로 당기되 배율을 함께 줄여 보이는 각을 지킨다.
    ///
    /// ⚠️ ①~③ 은 <b>계산으로 잡은 값이지 헤드셋으로 잰 값이 아니다.</b> 실물이 들어오면
    ///    여기 상수 넷만 고치면 모든 판이 함께 따라온다 — 그러라고 한곳에 모아 뒀다.
    /// </summary>
    public static class UiTuning
    {
        // ── PC ────────────────────────────────────────────────
        /// <summary>PC에서 판을 세우는 거리(m).
        /// ⚠️ <b>일부러 아주 가깝다.</b> IMGUI는 무엇에도 가리지 않았는데 월드 판은 벽·가구에
        ///    가린다. 걷기 캡슐 반지름이 0.3 m 라 어떤 벽도 눈에서 0.3 m 안으로 들어오지 못한다 —
        ///    0.22 m 에 두면 <b>가려질 수가 없다</b>. 모니터에는 거리가 보이지 않으니 손해도 없다.</summary>
        public const float PcDistance = 0.22f;

        // ── VR ────────────────────────────────────────────────
        public const float VrDistance = 1.00f;
        /// <summary>VR에서 '화면'으로 삼는 세로 화각(도).</summary>
        public const float VrContentVFov = 42f;
        /// <summary>VR '화면'의 가로:세로. 1.45 → 가로 ±30.5°.</summary>
        public const float VrContentAspect = 1.45f;
        /// <summary>VR 기준 단위 수(세로). <b>이 숫자 하나가 VR UI 전체의 크기를 정한다.</b></summary>
        public const float VrRefHeight = 388f;

        /// <summary>VR에서 판이 시야를 따라갈 때 — 이 각도 안의 움직임은 무시한다(멀미 방지).
        /// 소지품 판이 쓰던 값과 같게 뒀다 — 이미 손으로 맞춰 본 값이다.</summary>
        public const float VrFollowDeadZone = 7f;
        /// <summary>따라잡는 데 걸리는 시간(초).</summary>
        public const float VrFollowLag = 0.16f;

        /// <summary>
        /// 캔버스 글리프를 굽는 밀도.
        /// PC는 1이 가장 또렷하다(2026-08-23 실측 — 올리면 오히려 뭉갠다).
        /// VR은 눈당 렌더 해상도가 모니터보다 촘촘하고 Link가 슈퍼샘플링까지 하므로 2로 둔다.
        /// ⚠️ VR 쪽 2는 <b>계산상 타당할 뿐 실측이 아니다.</b> 헤드셋에서 1·1.5·2를 견주어 볼 것.
        /// </summary>
        public const float PcPixelsPerUnit = 1f;
        public const float VrPixelsPerUnit = 2f;

        /// <summary>한 프레임의 배치 수치. <see cref="Compute"/> 가 매번 새로 만든다.</summary>
        public struct Layout
        {
            public UiMode mode;
            public float distance;      // 눈에서 판까지(m)
            public float halfWidth;     // 그 거리에서의 '화면' 반너비(m)
            public float halfHeight;
            public float unitScale;     // 캔버스 1단위 = 몇 m
            public float refWidth;      // 기준 단위 수 (PC = Screen.width)
            public float refHeight;
            public float pixelsPerUnit;
            public bool follow;         // 시야를 따라가는가 (VR) / 화면에 붙박이인가 (PC)
            public float followDeadZone, followLag;

            /// <summary>캔버스 1단위가 화면에서 차지하는 각(도) — 글자 크기 검산용.</summary>
            public float DegPerUnit => unitScale / Mathf.Max(1e-4f, distance) * Mathf.Rad2Deg;

            /// <summary>글자 <paramref name="units"/> 단위가 몇 도로 보이는가.</summary>
            public float TextDeg(float units) => units * DegPerUnit;

            /// <summary>화면 비율 좌표(0~1, 왼쪽아래 원점)를 판 로컬 오프셋(m)으로.</summary>
            public Vector2 ViewportToMeters(Vector2 v)
                => new Vector2((v.x - 0.5f) * 2f * halfWidth, (v.y - 0.5f) * 2f * halfHeight);
        }

        /// <summary>지금 모드와 카메라로 배치 수치를 낸다.</summary>
        public static Layout Compute(Camera cam)
        {
            var l = new Layout();
            l.mode = UiModes.Current;

            if (l.mode == UiMode.VR)
            {
                l.distance = VrDistance;
                l.refHeight = VrRefHeight;
                l.refWidth = VrRefHeight * VrContentAspect;
                l.halfHeight = VrDistance * Mathf.Tan(VrContentVFov * 0.5f * Mathf.Deg2Rad);
                l.halfWidth = l.halfHeight * VrContentAspect;
                l.pixelsPerUnit = VrPixelsPerUnit;
                l.follow = true;
                l.followDeadZone = VrFollowDeadZone;
                l.followLag = VrFollowLag;
            }
            else
            {
                // ⚠️ VR에서는 cam.fieldOfView 가 HMD 투영에 밀려 뜻을 잃는다. 그래서 카메라 화각을
                //    읽는 것은 **PC 갈래에서만** 한다 — 조사에서 지적된 함정을 여기서 끊는다.
                float vfov = cam != null ? cam.fieldOfView : 60f;
                float aspect = cam != null && cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
                l.distance = PcDistance;
                l.refHeight = Mathf.Max(1f, Screen.height);
                l.refWidth = Mathf.Max(1f, Screen.width);
                l.halfHeight = PcDistance * Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad);
                l.halfWidth = l.halfHeight * aspect;
                l.pixelsPerUnit = PcPixelsPerUnit;
                l.follow = false;
                l.followDeadZone = 0f;
                l.followLag = 0f;
            }

            l.unitScale = 2f * l.halfHeight / l.refHeight;
            return l;
        }
    }
}
