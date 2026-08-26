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

            // 세워 둔 동안에는 손을 대지 않는다 — 지금 있는 그 자리 그대로.
            if (Frozen) return;

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

        [Tooltip("<b>화면과 나란히</b> 세운다 — 카메라 회전을 그대로 쓰고 자리만 앞·위로 민다. " +
                 "꾸러미 하단바가 그렇게 선다. 눈을 마주 보게 눕히면 아래로 치우친 넓은 판이 " +
                 "<b>사다리꼴로 일그러져</b> 글이 읽기 나빠진다")]
        [SerializeField] private bool _screenParallel;

        /// <summary>화면과 나란히 설지 밖에서 정한다. 하단바처럼 넓고 아래에 눕는 판이 쓴다.</summary>
        public void SetScreenParallel(bool on) { _screenParallel = on; }

        [Header("벽 피하기")]
        [Tooltip("앞을 막은 것이 있으면 그 앞으로 당겨 온다. 당긴 만큼 배율도 함께 줄어 " +
                 "<b>보이는 크기는 그대로</b>다 — 그 셈은 이미 아래에 있다.\n\n" +
                 "견우팀 꾸러미(VrPanel)가 쟀던 것을 옮겨 온 것이다. 좁은 방(사랑방·문서고)에서 " +
                 "판이 벽에 파묻히던 자리를 막는다")]
        [SerializeField] private bool _avoidWalls = true;
        [Tooltip("막은 것에서 이만큼 앞에 선다(m)")]
        [SerializeField] private float _wallMargin = 0.06f;
        [Tooltip("아무리 막혀도 이보다 가까이는 안 온다(m)")]
        [SerializeField] private float _wallMinDistance = 0.45f;

        /// <summary>지금 프레임에 쓸 거리. 대상이 나보다 가까우면 그 앞으로 당긴다.</summary>
        private float EffectiveDistance(Transform head)
        {
            float d = _distance;
            if (_keepInFrontOf != null)
            {
                float toTarget = ModelBounds.DistanceTo(_keepInFrontOf, head.position);
                d = Mathf.Clamp(toTarget - _frontMargin, _minDistance, _distance);
            }
            return _avoidWalls ? PullBeforeWall(head, d) : d;
        }

        /// <summary>
        /// <b>앞을 막은 것 앞으로 당겨 온다.</b>
        ///
        /// 이 앵커는 눈앞 정해진 거리에 판을 세우는데, 월드 캔버스는 깊이 검사를 받는다.
        /// 그래서 좁은 방에서는 판이 <b>벽 속에 파묻혀</b> 글자가 반쯤 잘린다.
        /// 사랑방과 문서고가 그렇다.
        ///
        /// <b>판 가운데로만 한 번 재면 안 된다</b> — 견우팀이 헤드셋에서 재고 적어 둔 것이다.
        /// 판이 화면 한가운데가 아니라 아래쪽에 앉아 있으면, 가운데 광선은 앞의 물건을
        /// <b>비껴가</b> 막힌 줄을 모른다. 그래서 네 귀퉁이까지 다섯 줄기를 재고
        /// 가장 가까운 것에 맞춘다.
        ///
        /// <b>손에 든 것은 안 센다.</b> 등불과 돋보기는 눈앞 반 미터에 있어서, 그것까지
        /// 세면 도구를 드는 순간 자막이 코앞으로 끌려온다. 머리와 한 몸에 달린 것은
        /// 벽이 아니다.
        /// </summary>
        private float PullBeforeWall(Transform head, float want)
        {
            if (want <= 0.4f || _rect == null) return want;   // 이미 코앞이면 잴 까닭이 없다

            Vector3 ahead = _anchorForward.sqrMagnitude > 1e-4f ? _anchorForward : head.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 1e-4f) ahead = head.forward;
            ahead.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, ahead).normalized;
            Vector3 up = Vector3.Cross(ahead, right);

            Vector2 half = _rect.sizeDelta * 0.5f * _canvasScale;
            Vector3 mid = ahead * want + up * _verticalOffset;

            // <b>막힌 것이 있을 때만 당긴다.</b> 처음엔 마지막에 무조건 여유(_wallMargin)를
            // 빼게 짜 두었더니, 아무것도 안 막았는데도 판이 늘 6cm 앞으로 나와 있었다 —
            // 재 보고 알았다(앵커 0.85m 인데 실제 0.81m). 아무 일도 없을 때는
            // <b>시킨 거리 그대로</b>여야 한다.
            float d = want;
            bool blocked = false;
            Transform mine = head.root;
            for (int i = 0; i < 5; i++)
            {
                float hx = i == 0 ? 0f : ((i == 1 || i == 3) ? -half.x : half.x);
                float hy = i == 0 ? 0f : ((i == 1 || i == 2) ? -half.y : half.y);
                Vector3 dir = (mid + right * hx + up * hy).normalized;

                RaycastHit hit;
                if (!Physics.Raycast(head.position, dir, out hit, want * 1.3f, ~0,
                                     QueryTriggerInteraction.Ignore)) continue;
                if (hit.transform != null && hit.transform.IsChildOf(mine)) continue;   // 내 몸·내 손

                // <b>바닥은 벽이 아니다.</b> 판이 넓고 아래로 치우쳐 있으면 아래 귀퉁이
                // 광선이 코앞의 마루를 짚는다 — 그것을 막힌 것으로 세면 판이 늘
                // 최소 거리까지 끌려온다. 사랑방에서 재 보니 1.30m 짜리 자막이
                // <b>0.46m</b> 까지 왔다. 글을 읽는 판을 가로막는 것은 <b>서 있는 면</b>이지
                // 누워 있는 면이 아니다.
                if (Vector3.Dot(hit.normal, Vector3.up) > 0.7f) continue;

                // 비스듬한 광선의 길이를 <b>판 면까지의 수직 거리</b>로 환산한다.
                // 안 그러면 귀퉁이 광선이 길다는 이유로 판을 덜 당긴다.
                float along = hit.distance * Vector3.Dot(dir, ahead);
                if (along < d) { d = along; blocked = true; }
            }
            return blocked ? Mathf.Max(_wallMinDistance, d - _wallMargin) : want;
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
        /// <summary>보고 선 쪽의 <b>수평</b> 방향. 천장이나 바닥을 정면으로 볼 때는 머리 위쪽을 쓴다.</summary>
        private static Vector3 Level(Transform head)
        {
            Vector3 v = head.forward;
            v.y = 0f;
            if (v.sqrMagnitude < 0.0001f) { v = head.up; v.y = 0f; }
            if (v.sqrMagnitude < 0.0001f) return Vector3.forward;
            return v.normalized;
        }

        /// <summary>
        /// <b>세워 둔다</b> — 지금 있는 자리에 그대로 두고 아예 따라오지 않는다.
        ///
        /// 돋보기가 그렇다. 렌즈는 눈에 붙어 있으므로 겨누는 일이 곧 <b>고개를 움직이는</b>
        /// 일인데, 종이까지 고개를 따라오면 겨눈 자리가 영영 안 바뀐다 — 제 얼굴에 붙은
        /// 것을 들여다보려는 꼴이라 아무리 움직여도 같은 데만 보인다. 들여다보는 동안
        /// 종이를 세계에 못 박아 두면, 고개를 움직인 만큼 렌즈가 종이 위를 지나간다.
        /// </summary>
        [System.NonSerialized] public bool Frozen;

        private Vector3 ViewDirection(Transform head)
        {
            Vector3 dir = _anchorForward;
            Vector3 eye = head.forward;
            float off = Vector3.Angle(dir, eye);
            if (off <= _maxOffAxis) return dir;

            return Vector3.RotateTowards(dir, eye, (off - _maxOffAxis) * Mathf.Deg2Rad, 0f).normalized;
        }

        /// <summary>
        /// <b>일자 앞에 붙박는다</b> — 보고 선 쪽의 <b>수평 정면</b>. 고개를 숙이든 들든
        /// 글은 그 자리에 그대로 있고, 몸을 돌리면 같이 돈다.
        ///
        /// 평소 이 창은 <b>일부러 늦게</b> 따라온다. 머리에 붙은 판이 아니라 앞에 놓인
        /// 판처럼 보이게 하려고, 제 방향을 들고 있다가 시선이 <see cref="_maxOffAxis"/>
        /// 도를 넘게 벗어나야 비로소 따라 돈다(<see cref="ViewDirection"/>). 방을
        /// 둘러보는 동안에는 그것이 맞다.
        ///
        /// 그런데 <b>설명을 읽는 동안</b>에는 정반대다. 물건은 아래에 두고 글은 눈앞에
        /// 두었는데, 물건을 보려고 고개를 숙이면 글이 저만치 뒤에 남는다 — 읽으려고
        /// 다시 들면 이번엔 글이 따라오느라 흔들린다. 읽는 글은 <b>붙박여</b> 있어야 한다.
        ///
        /// 한 번은 시선을 <b>그대로</b>(head.forward) 따라 붙였는데, 그러면 늘 시야
        /// 한가운데라 <b>고개를 숙여도 글이 따라 내려와 물건을 덮는다</b> — 아래를 봐도
        /// 물건이 안 보이니 겹치지 않게 위아래로 나눈 뜻이 없어진다. 붙박는 것은
        /// <b>수평 방향</b>이라야 한다: 글은 일자 앞에 서 있고, 물건을 보려면 고개를
        /// 숙이고, 읽으려면 고개를 든다. 그 두 자세가 곧 살피기와 읽기다.
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

            // 수첩이 펴지면 나머지는 무릎께로 물러난다.
            //
            // 붙박은 창만은 예외다. 도구를 익히는 동안 벨트를 내리려고 <see cref="StowAll"/>
            // 을 켜는데, 그 신호가 <b>설명 자막까지 같이 끌어내렸다</b> — 눈앞에 두라고
            // 붙박아 놓고는 42cm 를 내려 도구와 겹치게 만들고 있었다. 붙박은 것은
            // 물러날 것이 아니다.
            float want = (_stowable && StowAll && !Pinned) ? 1f : 0f;
            _stow = Mathf.MoveTowards(_stow, want, _stowSpeed * Time.deltaTime);
            drop -= _stowDrop * Mathf.SmoothStep(0f, 1f, _stow);

            // 붙박은 동안에는 <b>수평 정면</b>이다 — 고개의 위아래는 안 따라간다.
            // 들고 있던 방향도 같이 끌어 두어야 풀었을 때 홱 돌아가지 않는다.
            Vector3 dir;
            if (Pinned) { dir = Level(head); _anchorForward = dir; }
            else dir = ViewDirection(head);

            // 아래로 치우치는 양은 시선 기준이라야 한다. 세계의 아래로 내리면
            // 고개를 숙였을 때 창이 발밑으로 파고든다.
            // 붙박은 동안만은 세계 기준이다 — 방향부터 수평이라, 여기서 고개를 따라가면
            // 숙일 때마다 글이 도로 아래로 쓸려 내려간다.
            Vector3 down = (_placement == Placement.Waist || Pinned) ? Vector3.up : head.up;

            // ── 화면과 나란히 세우기 ────────────────────
            //
            // 꾸러미 하단바가 못 박아 둔 규칙이다. 저쪽 주석 그대로:
            // 「전에는 판을 18도 눕혀 놨더니 원근 때문에 <b>사다리꼴로 일그러져</b>
            //  글을 읽기 불편했다. 판을 <b>카메라 회전 그대로</b> 세우고 자리만
            //  카메라의 위·오른쪽 축으로 밀어낸다.」
            //
            // 우리 앵커는 판을 <b>눈을 마주 보게</b> 눕히는데, 그러면 아래로 치우친
            // 판이 비스듬해진다 — 재 보니 왼쪽 귀퉁이가 화면 y 0.34, 오른쪽이 0.30 으로
            // 어긋나 있었다. 넓은 바일수록 그 어긋남이 크게 보인다.
            //
            // 그리고 <b>자리를 각도로 잡으면 안 된다</b>(저쪽의 또 다른 경고):
            // 방향을 돌려 곱하면 판 면까지의 거리가 달라져 가장자리가 작아 보인다.
            // 앞으로 d, 위로 drop — 두 축으로 <b>밀어야</b> 한다.
            if (_screenParallel)
            {
                var flat = head.position + head.forward * d + head.up * drop;
                var rot = head.rotation;
                if (instant) transform.SetPositionAndRotation(flat, rot);
                else
                {
                    float k = 1f - Mathf.Exp(-_damping * Time.deltaTime);
                    transform.SetPositionAndRotation(
                        Vector3.Lerp(transform.position, flat, k),
                        Quaternion.Slerp(transform.rotation, rot, k));
                }
                return;
            }

            Vector3 target = head.position
                             + dir * d
                             + down * drop;

            // Waist는 아래를 보고 있으므로 살짝 눕혀서 정면으로 마주 보게 한다.
            Vector3 toHead = head.position - target;
            // 붙박은 동안에는 <b>똑바로 선 판</b>이다. 눈을 마주 보게 눕히면 고개를 든
            // 정도만큼 판이 뒤로 젖혀져, 글자가 사다리꼴로 찌그러져 보인다.
            Quaternion targetRot = Pinned
                ? Quaternion.LookRotation(dir, Vector3.up)
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
