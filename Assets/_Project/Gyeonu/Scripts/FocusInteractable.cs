using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 포커스형 상호작용 베이스 (어드벤처식 퍼즐 조작, 2026-08-14) — 동작 측.
    /// 사용하면 카메라가 대상 정면으로 미끄러져 고정되고, 드래그 입력이 HandleDrag로 들어온다.
    ///
    /// 입력과 동작의 분리:
    ///   - 이 클래스(와 서브클래스)는 "무엇이 어떻게 움직이는가"만 안다.
    ///   - 데스크톱 입력은 DebugFocusRig(마우스 임시)가 담당하고, VR로 갈 때는
    ///     컨트롤러 리그가 GetFocusPose / HandleDrag / OnFocusChanged 같은 API를
    ///     그대로 호출하면 된다 — 대상 코드는 무수정.
    ///   - Interact()는 기존 Interactable 규약의 단일 진입점 — 데스크톱 경로에서는
    ///     포커스 진입 요청으로 쓴다. VR 리그는 자기 진입점에서 직접 세션을 열어도 된다.
    ///
    /// 혼상·혼천의가 공유하는 공통 베이스다 — 대상별 조작(회전 축·감도)만 서브클래스가 구현한다.
    /// </summary>
    public abstract class FocusInteractable : Interactable
    {
        [Header("포커스")]
        [Tooltip("카메라가 대상 중심에서 떨어질 거리(m)")]
        public float focusDistance = 1.7f;
        [Tooltip("진입·복귀 보간 시간(초)")]
        public float transitionTime = 0.5f;
        [Tooltip("포커스 중 화면 가장자리 어둡히기 강도 (0~1)")]
        [Range(0f, 1f)] public float dimStrength = 0.6f;
        [Tooltip("바라볼 지점. 비우면 렌더러 바운즈 중심")]
        public Transform focusAnchor;

        /// <summary>카메라가 바라볼 지점.</summary>
        public virtual Vector3 FocusPoint
        {
            get
            {
                if (focusAnchor != null) return focusAnchor.position;
                var rends = GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) return transform.position;
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                return b.center;
            }
        }

        /// <summary>카메라 목표 자세. 기본: 현재 방위를 유지한 채 수평으로 다가가 바라본다.
        /// "정면"이 정해진 대상(혼상 등)은 오버라이드로 면을 고른다.</summary>
        public virtual void GetFocusPose(Vector3 currentEyePos, out Vector3 pos, out Quaternion rot)
        {
            var c = FocusPoint;
            var dir = currentEyePos - c;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            dir.Normalize();
            pos = c + dir * focusDistance;
            rot = Quaternion.LookRotation(c - pos);
        }

        /// <summary>포커스 중 드래그 입력 (픽셀 단위 델타). 대상별 조작은 여기서.</summary>
        public abstract void HandleDrag(Vector2 delta);

        /// <summary>
        /// 드래그 입력 + **그 프레임의 포인터 레이** (2026-08-24, 렌즈 퍼즐).
        /// 돌려 보는 대상은 델타만 있으면 되지만, **무언가를 끌어다 놓는 대상**은
        /// 지금 어디를 가리키고 있는지를 알아야 한다(델타를 적분하면 어긋남이 쌓인다).
        /// 기본 구현은 델타판으로 넘긴다 — 혼상·혼천의·암문은 무수정.
        /// VR 리그로 갈아끼울 때는 컨트롤러 포인터 레이를 그대로 넘기면 된다.
        /// </summary>
        public virtual void HandleDrag(Ray ray, Vector2 delta) => HandleDrag(delta);

        /// <summary>드래그를 놓았다 (버튼 뗌). 놓는 순간의 연출·소리가 있는 대상만 구현한다.</summary>
        public virtual void HandleRelease() { }

        /// <summary>
        /// 포커스 중 쓸 카메라 화각(도). 0이면 그대로 둔다.
        /// 20cm짜리 음각판처럼 **작은 것을 화면 가득 보아야 하는 대상**은 다가서는 대신
        /// 화각을 좁힌다 — 코앞까지 다가가면 원근 왜곡이 심해 판이 사다리꼴로 늘어진다.
        /// </summary>
        public virtual float FocusFov => 0f;

        /// <summary>포커스 중 스크롤/보조 입력 (부호 = 방향). 부품 전환 등에 쓴다 — 기본 무시.</summary>
        public virtual void HandleScroll(float direction) { }

        // ─────────────────────────────────────────────────────────
        //  조준점 — 포커스 중 "지금 어디를 겨누는가"
        // ─────────────────────────────────────────────────────────

        [Header("조준점")]
        [Tooltip("포커스 중 겨누는 자리에 조준점을 그린다. 끄면 아무것도 안 그린다")]
        public bool showReticle = true;

        [Tooltip("조준점 지름(m). 0이면 대상 크기에 맞춰 저절로 잡는다")]
        public float reticleSize = 0f;

        [Tooltip("대상 표면에서 띄우는 거리(m) — 표면에 파묻히지 않게")]
        public float reticleLift = 0.004f;

        [Tooltip("겨눔 면을 대상 중심에서 얼마나 앞으로 낼지(m). 0이면 대상 두께에 맞춰 저절로. " +
                 "돌려 보는 둥근 대상(혼상·혼천의)에서 조준점이 속에 파묻히지 않게 하는 값이다")]
        public float reticleFront = 0f;

        [Tooltip("조준점이 대상 중심에서 벗어날 수 있는 반경(m). 0이면 대상 크기에 맞춰 저절로")]
        public float reticleRadius = 0f;

        readonly FocusReticle _reticle = new FocusReticle();
        Bounds _bounds;
        bool _boundsDone;

        /// <summary>
        /// 누르지 않은 채 **가리키고만** 있는 매 프레임의 포인터 레이 (2026-08-25).
        ///
        /// <see cref="DebugFocusRig"/> 가 포커스에 들어가면서 하드웨어 커서를 감추므로,
        /// <b>대상이 자기 몸 위에 조준점을 그려야</b> 어디를 겨누는지 알 수 있다. 그리지 않으면
        /// 보이지 않는 커서로 조준하게 된다 — 렌즈 퍼즐에서 실제로 그랬다(2026-08-25).
        /// 월드에 그리므로 스크린샷에도, 나중에 VR 헤드셋에도 그대로 보인다.
        ///
        /// 기본은 <b>시선을 마주 보는 면</b>에 찍는다. 판이 있는 대상(렌즈·암문)은
        /// <see cref="ReticleSurface"/> 만 갈아 끼우면 제 판 위에 정확히 얹힌다.
        /// </summary>
        public virtual void HandlePoint(Ray ray)
        {
            if (!showReticle) { _reticle.Hide(); return; }
            if (!ReticleSurface(ray, out var pos, out var normal, out var up)) { _reticle.Hide(); return; }
            _reticle.Place(transform, pos + normal * reticleLift, normal, up, ResolvedReticleSize, 0.85f);
        }

        /// <summary>포커스에서 물러날 때 <see cref="DebugFocusRig"/> 가 부른다.</summary>
        public void HideReticle() => _reticle.Hide();

        protected virtual void OnDestroy() => _reticle.Dispose();

        /// <summary>
        /// 조준점을 어느 자리에 찍을까. 기본은 <b>대상 앞으로 나온, 시선과 마주 보는 면</b>이다.
        /// 판이 정해진 대상은 이것만 오버라이드하면 된다 — 나머지(그리기·정리)는 베이스가 맡는다.
        /// </summary>
        /// <returns>겨눌 자리를 찾았으면 true. false면 조준점을 감춘다.</returns>
        /// <summary>조준점이 맴돌 중심. 기본은 바라보는 지점 — 조작할 것이 한쪽에 몰려 있으면
        /// (암문의 두 돌처럼) 그쪽으로 옮긴다.</summary>
        protected virtual Vector3 ReticleCenter => FocusPoint;

        protected virtual bool ReticleSurface(Ray ray, out Vector3 pos, out Vector3 normal, out Vector3 up)
        {
            Vector3 c = ReticleCenter;
            normal = ray.origin - c;
            if (normal.sqrMagnitude < 1e-6f) { pos = c; normal = Vector3.up; up = Vector3.forward; return false; }
            normal.Normalize();

            // ⚠️ 겨눔 면을 대상 두께만큼 앞으로 내되 **보는 사람을 넘어가면 안 된다**.
            //    혼천의처럼 큰 대상은 바운즈 반지름이 카메라까지의 거리보다 커서, 막지 않으면
            //    조준점이 카메라 자리(또는 등 뒤)에 놓여 통째로 사라진다(2026-08-25 실측).
            float toViewer = Vector3.Distance(ray.origin, c);
            float front = Mathf.Clamp(ResolvedFront(normal), 0f, Mathf.Max(0.02f, toViewer - 0.12f));

            Vector3 center = c + normal * front;
            up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;

            var plane = new Plane(normal, center);
            if (!plane.Raycast(ray, out float d) || d <= 0f) { pos = center; return false; }
            pos = ray.GetPoint(d);

            // ⚠️ 대상 밖을 겨눠도 **가장자리에 붙잡는다**. 놓치면 조준점이 통째로 사라져
            //    지금 어디를 겨누는지 다시 알 수 없게 된다 (소지품 판과 같은 규약).
            //    반경도 보는 거리 안으로 묶는다 — 큰 대상에서 화면 밖까지 나가지 않게.
            Vector3 rel = pos - center;
            float r = Mathf.Min(ResolvedRadius, toViewer * 0.75f);
            if (rel.magnitude > r) pos = center + rel.normalized * r;
            return true;
        }

        /// <summary>대상 전체를 감싸는 바운즈 — 한 번만 재고 기억한다.</summary>
        protected Bounds SelfBounds
        {
            get
            {
                if (_boundsDone) return _bounds;
                _boundsDone = true;
                var rends = GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) { _bounds = new Bounds(transform.position, Vector3.one * 0.2f); return _bounds; }
                _bounds = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) _bounds.Encapsulate(rends[i].bounds);
                return _bounds;
            }
        }

        float ResolvedFront(Vector3 n)
        {
            if (reticleFront > 0f) return reticleFront;
            var b = SelfBounds;
            float half = Mathf.Abs(b.extents.x * n.x) + Mathf.Abs(b.extents.y * n.y) + Mathf.Abs(b.extents.z * n.z);
            return Vector3.Dot(b.center - ReticleCenter, n) + half + 0.02f;
        }

        float ResolvedRadius => reticleRadius > 0f ? reticleRadius : Mathf.Max(0.06f, SelfBounds.extents.magnitude);

        float ResolvedReticleSize => reticleSize > 0f
            ? reticleSize
            : Mathf.Clamp(ResolvedRadius * 0.16f, 0.008f, 0.05f);

        /// <summary>
        /// 포커스 중 클릭 (화면 좌표에서 쏜 카메라 레이). **눌러서 조작하는 대상**만 구현한다
        /// (암문 자물쇠, 2026-08-23). 돌려 보는 대상(혼상·혼천의)은 기본 빈 구현 그대로 두면 된다.
        ///
        /// 포커스 중에는 걷기 컨트롤러가 꺼지면서 커서 잠금이 함께 풀리므로(DebugWalkController.OnDisable)
        /// 데스크톱에서는 마우스로 직접 가리킬 수 있다. VR 리그로 갈아끼울 때는 컨트롤러
        /// 포인터 레이를 그대로 이 함수에 넘기면 된다 — 대상 코드는 무수정.
        /// </summary>
        public virtual void HandleClick(Ray ray) { }

        /// <summary>포커스 중 하단에 띄울 조작 안내. 대상마다 조작이 다르다.</summary>
        public virtual string FocusHint => "드래그: 돌리기   Esc/우클릭: 물러나기";

        /// <summary>지금 물러날 수 있는가. 연출이 도는 동안 막고 싶으면 false를 돌려준다.</summary>
        public virtual bool CanExitFocus => true;

        /// <summary>포커스 중 하단에 띄울 상태 문구 (예: 조작 중인 고리 이름). null이면 생략.</summary>
        public virtual string FocusStatus => null;

        /// <summary>진입(true)·이탈(false) 알림 — 연출 훅.</summary>
        public virtual void OnFocusChanged(bool focused) { }

        /// <summary>기존 Interactable 규약의 단일 진입점 — 포커스 진입 요청.</summary>
        public override void Interact(GameObject actor) => DebugFocusRig.Begin(this, actor);
    }
}
