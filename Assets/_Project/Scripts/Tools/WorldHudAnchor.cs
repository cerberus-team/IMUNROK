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

        [Tooltip("시선에서 이 각도(도) 넘게 벗어나면 그만큼만 끌어온다. " +
                 "수평만 따라가면 고개를 크게 숙였을 때 창이 화면 위로 빠져나가 아예 안 보인다")]
        [Range(5f, 45f)] [SerializeField] private float _maxOffAxis = 20f;

        [Header("크기")]
        [Tooltip("Canvas 스케일. 월드 Canvas는 1픽셀=1m라 아주 작게 잡아야 한다")]
        [SerializeField] private float _canvasScale = 0.001f;
        [Tooltip("Canvas 폭·높이(픽셀). 스케일과 곱해져 실제 크기(m)가 된다")]
        [SerializeField] private Vector2 _canvasSize = new Vector2(900f, 260f);

        [Tooltip("비우면 Camera.main을 쓴다")]
        [SerializeField] private Camera _camera;

        [Header("물러나기")]
        [Tooltip("수첩처럼 더 앞서는 것이 펼쳐지면 이 창은 아래로 내려앉는다. 수첩 자신은 끈다")]
        [SerializeField] private bool _stowable = true;
        [Tooltip("내려앉는 깊이(m). 손에 든 것을 무릎에 내려놓는 만큼")]
        [SerializeField] private float _stowDrop = 0.42f;
        [Tooltip("내려앉고 일어서는 빠르기")]
        [SerializeField] private float _stowSpeed = 4f;

        /// <summary>
        /// 지금 <b>모두 물러나야</b> 하는가. 수첩을 펼치면 참이 된다.
        ///
        /// 수첩은 두 손으로 펴 드는 것이라, 그 앞에 자막이며 도구벨트며 쥐고 있던 종이가
        /// 그대로 떠 있으면 겹쳐서 읽을 수가 없다. 끄지 않고 <b>내려놓는</b> 까닭은,
        /// 없어진 것과 잠시 무릎에 둔 것은 손에 남는 느낌이 다르기 때문이다.
        /// </summary>
        public static bool StowAll { get; set; }

        private float _stow;   // 0 = 눈앞, 1 = 내려놓음

        [Header("가림 피하기")]
        [Tooltip("이 대상보다 앞에 서게 한다. 심문 중인 인물을 넣으면, 바짝 붙어도 상대 몸에 대사가 가리지 않는다")]
        [SerializeField] private Transform _keepInFrontOf;
        [Tooltip("대상보다 이만큼 앞(m)")]
        [SerializeField] private float _frontMargin = 0.35f;
        [Tooltip("아무리 가까워도 이보다 가까이는 안 붙인다(m). 너무 붙으면 눈이 아프다")]
        [SerializeField] private float _minDistance = 0.6f;

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

        /// <summary>
        /// 코드로 만들 때 배치를 한 번에 잡는다. 배치마다 적당한 거리·높이가 다르다:
        /// Waist는 허리춤이라 가깝고 아래(0.6m / -0.5m), Front는 읽는 거리(2m / 눈높이 살짝 아래).
        /// </summary>
        public void Configure(Placement placement)
        {
            _placement = placement;
            if (placement == Placement.Waist)
            {
                _distance = 0.6f;
                _verticalOffset = -0.5f;
                _recenterAngle = 35f;      // 허리춤은 더 둔감하게 — 자주 따라오면 거슬린다
                _canvasSize = new Vector2(900f, 320f);
            }
            else
            {
                // 자막은 읽어야 하므로 가깝게. 2m는 글자가 시야각 1도 남짓이라 작다.
                _distance = 1.3f;
                _verticalOffset = -0.28f;  // 시선 정면보다 살짝 아래 — 앞을 보면서 읽기 편하게
                _recenterAngle = 20f;
                _canvasSize = new Vector2(1200f, 380f);
            }

            if (_canvas != null) ConfigureCanvas();   // Awake가 이미 지났으면 새 값으로 다시 잡는다
            _placed = false;
        }

        /// <summary>
        /// 거리·높이를 실행 중에 바꾼다. 헤드셋을 쓰고 직접 보면서 맞출 때 쓴다
        /// (읽기 편한 거리는 사람마다 다르고, 화면으로는 판단이 안 된다).
        /// </summary>
        public void SetDistance(float distance, float verticalOffset)
        {
            _distance = Mathf.Clamp(distance, 0.4f, 5f);
            _verticalOffset = verticalOffset;
            _placed = false;   // 감쇠 없이 새 자리로 바로
        }

        /// <summary>지금 거리(m).</summary>
        public float Distance => _distance;

        /// <summary>지금 높이 오프셋(m).</summary>
        public float VerticalOffset => _verticalOffset;

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

        /// <summary>
        /// 심문 중엔 이 인물보다 앞에 서라고 알려 준다.
        ///
        /// 왜 필요한가: 이 앵커는 눈앞 1.3m에 못 박혀 있고, 월드 캔버스는 깊이 검사를 받는다.
        /// 그래서 인물에게 1m 안쪽으로 다가서면 <b>상대 몸이 대사창을 덮어</b> 글씨가 안 보인다.
        /// "뒤로 물러서세요"라고 안내하는 대신, 창이 알아서 상대 앞으로 당겨 온다.
        /// (VR에서는 뒤에 벽이 있어 물러설 수 없는 경우가 실제로 있다)
        /// </summary>
        public void KeepInFrontOf(Transform target) => _keepInFrontOf = target;

        /// <summary>지금 프레임에 쓸 거리. 대상이 나보다 가까우면 그 앞으로 당긴다.</summary>
        private float EffectiveDistance(Transform head)
        {
            if (_keepInFrontOf == null) return _distance;
            float toTarget = ModelBounds.DistanceTo(_keepInFrontOf, head.position);
            return Mathf.Clamp(toTarget - _frontMargin, _minDistance, _distance);
        }

        /// <summary>
        /// 자막이 놓일 방향. 밑바탕은 수평 시선(위아래로 출렁이면 멀미가 난다)이지만,
        /// 고개를 크게 숙이거나 든 자세에서는 그것만으로는 창이 화면 밖으로 나간다.
        ///
        /// 어전이 그랬다 — 부복한 카메라가 51도 아래를 보는데 창은 수평에서 12도
        /// 아래에 놓여, 왕의 말이 나오고는 있는데 프레임 위쪽 바깥에 있었다.
        /// 그래서 시선과 벌어진 각이 정해진 값을 넘으면 넘은 만큼만 끌어온다.
        /// 평소처럼 고개를 조금 움직이는 동안에는 예전과 똑같이 가만히 있는다.
        /// </summary>
        private Vector3 ViewDirection(Transform head)
        {
            Vector3 dir = _anchorForward;
            Vector3 eye = head.forward;
            float off = Vector3.Angle(dir, eye);
            if (off <= _maxOffAxis) return dir;

            return Vector3.RotateTowards(dir, eye, (off - _maxOffAxis) * Mathf.Deg2Rad, 0f).normalized;
        }

        /// <summary>
        /// <b>눈앞에 붙박는다</b> — 고개를 어디로 돌리든 늘 시야 한가운데.
        ///
        /// 평소 이 창은 <b>일부러 늦게</b> 따라온다. 머리에 붙은 판이 아니라 앞에 놓인
        /// 판처럼 보이게 하려고, 제 방향을 들고 있다가 시선이 <see cref="_maxOffAxis"/>
        /// 도를 넘게 벗어나야 비로소 따라 돈다(<see cref="ViewDirection"/>). 방을
        /// 둘러보는 동안에는 그것이 맞다.
        ///
        /// 그런데 <b>설명을 읽는 동안</b>에는 정반대다. 물건은 아래에 두고 글은 눈앞에
        /// 두었는데, 물건을 보려고 고개를 숙이면 글이 저만치 뒤에 남는다 — 읽으려고
        /// 다시 들면 이번엔 글이 따라오느라 흔들린다. 읽는 글은 <b>붙박여</b> 있어야 한다.
        /// 그동안만 켠다.
        /// </summary>
        [System.NonSerialized] public bool Pinned;

        private void ApplyTransform(Transform head, bool instant)
        {
            float d = EffectiveDistance(head);

            // 가까이 당겨오면 글씨가 그만큼 커 보인다 — 거리에 맞춰 같은 비율로 줄인다.
            // 그래야 "자리만 옮겼을 뿐 보기엔 똑같다"가 된다.
            float scale = _canvasScale * (d / Mathf.Max(0.01f, _distance));
            if (!Mathf.Approximately(_rect.localScale.x, scale))
                _rect.localScale = Vector3.one * scale;

            // 위아래 치우침도 같은 비율로. 안 그러면 당겨온 창이 시야 아래로 내려앉는다.
            float drop = _verticalOffset * (d / Mathf.Max(0.01f, _distance));

            // 수첩이 펴지면 나머지는 무릎께로 물러난다
            float want = (_stowable && StowAll) ? 1f : 0f;
            _stow = Mathf.MoveTowards(_stow, want, _stowSpeed * Time.deltaTime);
            drop -= _stowDrop * Mathf.SmoothStep(0f, 1f, _stow);

            // 붙박은 동안에는 <b>시선 그대로</b>다. 들고 있던 방향도 같이 끌어 두어야
            // 풀었을 때 홱 돌아가지 않는다.
            Vector3 dir;
            if (Pinned) { dir = head.forward; _anchorForward = dir; }
            else dir = ViewDirection(head);

            // 아래로 치우치는 양은 시선 기준이라야 한다. 세계의 아래로 내리면
            // 고개를 숙였을 때 창이 발밑으로 파고든다.
            Vector3 down = _placement == Placement.Waist ? Vector3.up : head.up;

            Vector3 target = head.position
                             + dir * d
                             + down * drop;

            // Waist는 아래를 보고 있으므로 살짝 눕혀서 정면으로 마주 보게 한다.
            Vector3 toHead = head.position - target;
            Quaternion targetRot = _placement == Placement.Waist
                ? Quaternion.LookRotation(-toHead.normalized, Vector3.up)
                : Quaternion.LookRotation(-toHead.normalized, Vector3.up);

            if (instant || Pinned)
            {
                transform.SetPositionAndRotation(target, targetRot);
                return;
            }

            float t = 1f - Mathf.Exp(-_damping * Time.deltaTime);   // 프레임률에 안 흔들리는 감쇠
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, target, t),
                Quaternion.Slerp(transform.rotation, targetRot, t));
        }

        /// <summary>이 창도 물러나야 하는가. 수첩 자신처럼 앞에 서는 것은 끈다.</summary>
        public void SetStowable(bool on) { _stowable = on; if (!on) _stow = 0f; }

        /// <summary>다음 프레임에 감쇠 없이 눈앞으로 다시 가져온다(패널을 열 때 호출).</summary>
        public void Recenter() => _placed = false;
    }
}
