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

        /// <summary>포커스 중 스크롤/보조 입력 (부호 = 방향). 부품 전환 등에 쓴다 — 기본 무시.</summary>
        public virtual void HandleScroll(float direction) { }

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
