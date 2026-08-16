using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 월드 공간 UI를 VR에서 "멀미 없이" 플레이어에게 붙여 두는 앵커.
    /// OnGUI(IMGUI)는 헤드셋에 렌더링되지 않으므로, VR UI는 전부 World Space Canvas로 그려야 한다.
    /// 그 Canvas를 어디에 어떻게 띄울지를 이 컴포넌트 하나가 담당한다.
    ///
    /// 붙이는 법: 빈 GameObject에 Canvas + 이 컴포넌트를 추가(Canvas가 없으면 자동 생성·설정).
    ///   · Front — 시야 정면. 자막·심문창처럼 "읽어야 하는" UI.
    ///   · Waist — 허리춤. 도구벨트처럼 "차고 다니는" UI. 고개를 숙여야 보이므로 시야를 안 가린다.
    ///
    /// VR에서 UI가 멀미를 유발하는 가장 큰 원인은 머리를 따라 "즉시" 붙어 오는 것이다.
    /// 그래서 두 가지를 쓴다:
    ///   ① 데드존(_recenterAngle) — 고개를 그만큼 돌리기 전엔 아예 따라오지 않는다.
    ///   ② 감쇠(_damping)        — 따라올 때도 부드럽게 지연을 두고 쫓아온다.
    /// 둘 다 0으로 두면 얼굴에 못 박힌 것처럼 되어 멀미가 난다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class WorldHudAnchor : MonoBehaviour
    {
        public enum Placement
        {
            /// <summary>시야 정면 — 읽어야 하는 UI(자막·심문·수첩)</summary>
            Front,
            /// <summary>허리춤 — 차고 다니는 UI(도구벨트). 고개를 숙여야 보인다</summary>
            Waist,
        }

        [Header("배치")]
        [SerializeField] private Placement _placement = Placement.Front;
        [Tooltip("카메라에서 떨어진 거리(m). VR 가독성 스위트스팟은 1.5~3m")]
        [Range(0.4f, 5f)] [SerializeField] private float _distance = 2f;
        [Tooltip("눈높이 기준 위아래 오프셋(m). Waist는 보통 -0.5 ~ -0.7")]
        [SerializeField] private float _verticalOffset = -0.55f;

        [Header("따라오기(멀미 방지)")]
        [Tooltip("고개를 이 각도(도) 이상 돌려야 따라오기 시작. 0이면 항상 따라옴(멀미 위험)")]
        [Range(0f, 60f)] [SerializeField] private float _recenterAngle = 25f;
        [Tooltip("따라오는 부드러움. 낮을수록 천천히·자연스럽게")]
        [Range(0.5f, 12f)] [SerializeField] private float _damping = 3f;

        [Header("크기")]
        [Tooltip("Canvas 스케일. 월드 Canvas는 1픽셀=1m라 아주 작게 잡아야 한다")]
        [SerializeField] private float _canvasScale = 0.001f;
        [Tooltip("Canvas 폭·높이(픽셀). 스케일과 곱해져 실제 크기(m)가 된다")]
        [SerializeField] private Vector2 _canvasSize = new Vector2(900f, 260f);

        [Tooltip("비우면 Camera.main을 쓴다")]
        [SerializeField] private Camera _camera;

        private Canvas _canvas;
        private RectTransform _rect;
        private bool _placed;      // 첫 프레임엔 감쇠 없이 즉시 배치
        private Vector3 _anchorForward;   // 마지막으로 정렬한 시선 방향(수평)

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _rect = (RectTransform)transform;
            ConfigureCanvas();
        }

        private void OnEnable() => _placed = false;   // 다시 켜질 땐 눈앞에 바로 오도록

        /// <summary>월드 Canvas로 만들고 카메라를 물린다(레이 인터랙터 클릭이 먹으려면 필수).</summary>
        private void ConfigureCanvas()
        {
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = Cam;              // 없으면 UI 클릭이 안 먹는다
            _rect.sizeDelta = _canvasSize;
            _rect.localScale = Vector3.one * _canvasScale;

            // 버튼 클릭(마우스든 VR 레이든)은 GraphicRaycaster가 있어야 전달된다.
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // 씬에 EventSystem이 없으면 클릭이 아예 안 먹는데 에러도 안 나서 원인 찾기 어렵다.
            if (EventSystem.current == null)
                Debug.LogWarning($"[WorldHudAnchor] '{name}' — 씬에 EventSystem이 없어 UI 클릭이 동작하지 않습니다. " +
                                 "Hierarchy 우클릭 ▸ UI ▸ Event System 을 추가하세요.");
        }

        private Camera Cam
        {
            get
            {
                if (_camera == null) _camera = Camera.main;
                return _camera;
            }
        }

        private void LateUpdate()   // 카메라가 움직인 뒤에 따라가야 한 프레임 밀리지 않는다
        {
            var cam = Cam;
            if (cam == null) return;
            if (_canvas.worldCamera == null) _canvas.worldCamera = cam;

            Transform head = cam.transform;

            // 수평 시선만 쓴다. 고개를 위아래로 젓는 것까지 따라가면 UI가 출렁여서 멀미가 난다.
            Vector3 look = head.forward;
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f) look = head.up;   // 천장/바닥을 정면으로 볼 때
            look.Normalize();

            if (!_placed)
            {
                _anchorForward = look;
                _placed = true;
                ApplyTransform(head, instant: true);
                return;
            }

            // 데드존: 고개를 충분히 돌렸을 때만 목표 방향을 새로 잡는다.
            if (Vector3.Angle(_anchorForward, look) > _recenterAngle)
                _anchorForward = look;

            ApplyTransform(head, instant: false);
        }

        private void ApplyTransform(Transform head, bool instant)
        {
            Vector3 target = head.position
                             + _anchorForward * _distance
                             + Vector3.up * _verticalOffset;

            // Waist는 아래를 보고 있으므로 살짝 눕혀서 정면으로 마주 보게 한다.
            Vector3 toHead = head.position - target;
            Quaternion targetRot = _placement == Placement.Waist
                ? Quaternion.LookRotation(-toHead.normalized, Vector3.up)
                : Quaternion.LookRotation(_anchorForward, Vector3.up);

            if (instant)
            {
                transform.SetPositionAndRotation(target, targetRot);
                return;
            }

            float t = 1f - Mathf.Exp(-_damping * Time.deltaTime);   // 프레임률에 안 흔들리는 감쇠
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, target, t),
                Quaternion.Slerp(transform.rotation, targetRot, t));
        }

        /// <summary>다음 프레임에 감쇠 없이 눈앞으로 다시 가져온다(패널을 열 때 호출).</summary>
        public void Recenter() => _placed = false;
    }
}
