using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 돋보기 — <b>진짜로 크게 보이는</b> 렌즈.
    ///
    /// 여태 돋보기는 도구벨트에 이름만 올라 있었다. 손에 들면 어딘가에서 조용히
    /// "돋보기를 들었다"는 표시가 켜지고, 문서를 눌렀을 때 안 보이던 글줄이 하나 더
    /// 붙는 것이 전부였다. 그것은 돋보기가 아니라 <b>열쇠</b>다 — 가진 사람에게만 열리는.
    ///
    /// 돋보기는 눈과 물건 사이에 끼우는 유리다. 그래서 여기서는 유리를 실제로 만든다:
    ///   · 눈 자리에 카메라를 하나 더 두고, 렌즈가 가리는 만큼의 좁은 화각만 찍는다.
    ///   · 그 그림을 렌즈 원판에 그대로 입힌다.
    /// 렌즈가 하늘을 향하면 하늘이, 종이를 향하면 종이가 크게 보인다. 무엇을 크게
    /// 보이게 할지 미리 정해 둔 목록이 없다 — 그래서 돋보기다.
    ///
    /// <b>배율의 셈</b>: 눈에서 렌즈 원판이 가리는 반각을 θ 라 하면, 렌즈 카메라의
    /// 반화각을 θ/배율 로 두면 된다. 원판 넓이는 그대로인데 그 안에 담기는 세상이
    /// 배율만큼 좁아지니, 보이는 것은 정확히 그 배만큼 커진다.
    ///
    /// <b>렌즈가 제 눈에 안 들어오게</b>: 렌즈 카메라의 근평면을 유리보다 조금 뒤로
    /// 밀어 둔다. 그러지 않으면 렌즈가 제 몸을 찍어 거울 두 장을 마주 세운 꼴이 된다.
    /// 레이어를 따로 파지 않는 까닭이 이것이다 — 프로젝트 설정을 건드리지 않아도 된다.
    ///
    /// 씬에 미리 둘 필요 없다. 씬이 올라오면 카메라를 찾아 스스로 붙는다.
    /// </summary>
    public class MagnifierLens : MonoBehaviour
    {
        [Header("무엇을 들었을 때")]
        [Tooltip("도구벨트에서 이 id를 들었을 때만 렌즈가 올라온다")]
        [SerializeField] private string _toolId = "magnify";

        [Header("유리")]
        [Tooltip("배율. 2.5~3배가 종이의 잔글씨를 읽기에 알맞다")]
        [Range(1.2f, 6f)] [SerializeField] private float _zoom = 2.8f;
        [Tooltip("유리 반지름(m)")]
        [SerializeField] private float _glassRadius = 0.07f;
        [Tooltip("렌즈 그림의 해상도. 크면 또렷하나 비싸다")]
        [SerializeField] private int _texSize = 1024;
        [Tooltip("손에 쥔 종이를 짚고 있을 때 배율을 이만큼 더 준다. 종이의 잔글씨는 " +
                 "방 저쪽 물건보다 훨씬 잘아서, 같은 배율로는 유리를 대나 마나다")]
        [Range(1f, 2.5f)] [SerializeField] private float _pageZoomBoost = 1.5f;

        [Header("드는 자세")]
        [Tooltip("<b>씬에서 맞춰 둔 소품 자세를 그대로 쓴다.</b> 소품을 매단 자리(HeldToolModel 이 붙은 " +
                 "오브젝트)의 위치와 기울기가 곧 '평소 드는 자세'가 된다. 끄면 아래 두 값으로 코드가 잡는다")]
        [SerializeField] private bool _useAuthoredPose = true;

        [Tooltip("평소에도 이만큼 앞으로 더 내민다(m). 0이면 씬에 맞춰 둔 자리 그대로")]
        [Range(-0.2f, 0.4f)] [SerializeField] private float _readyPush = 0.06f;

        [Tooltip("눈에 댔을 때 유리 한가운데가 눈에서 이만큼 앞에 온다(m). 크면 더 내밀고 든 꼴이 된다")]
        [Range(0.15f, 0.7f)] [SerializeField] private float _eyeDistance = 0.36f;

        [Tooltip("눈에 댔을 때 시선 한가운데에서 이만큼 아래로 비껴 둔다(m)")]
        [Range(-0.1f, 0.15f)] [SerializeField] private float _eyeDrop = 0.03f;

        [Tooltip("눈에 댔을 때 돋보기가 이만큼 커진다. 1이면 평소 크기 그대로. " +
                 "<b>들여다보라고 든 것이니 들여다볼 만해야 한다</b> — 눈에 댄 돋보기가 " +
                 "엄지손톱만 하면 알 안을 볼 수가 없다")]
        [Range(1f, 2.5f)] [SerializeField] private float _eyeScale = 1.5f;

        [Tooltip("눈에 댈 때 맞춰 둔 기울기를 이만큼 바로 세운다. 0이면 그 각도 그대로 들여다본다")]
        // 0 이면 문갑에 놓였던 기울기 그대로 눈앞에 온다 — 유리가 비스듬히 서서
        // 들여다보는 자세가 안 나오고, 각도가 이상하다는 말이 그것이었다.
        // 눈에 댈 때만은 유리가 <b>눈을 마주 보아야</b> 한다.
        // 세우는 방법이 문제였다. 여태 <b>LookRotation</b> 으로 자세를 통째로 새로
        // 지었는데, 그러면 유리를 눈으로 돌리는 김에 <b>자루가 향하는 쪽</b>까지 같이
        // 정해진다. 그 기준으로 삼은 것이 머리의 위쪽이라, 고개를 숙여 종이를 볼 때마다
        // 기준이 함께 기울어 어느 순간 자루가 반대로 뒤집혔다 — 내밀 때 돋보기가
        // 홱 도는 것이 이것이었다.
        // 이제 <b>가장 짧은 길로</b> 돌린다(FromToRotation): 유리 면이 눈을 향하도록
        // 딱 그만큼만 돌고, 자루가 감긴 방향은 건드리지 않는다. 뒤집힐 여지가 없다.
        [Range(0f, 1f)] [SerializeField] private float _eyeStraighten = 1f;

        [Header("드는 자세 — 맞춰 둔 자세를 안 쓸 때만")]
        [Tooltip("평소 — 눈 아래 비껴 들고 있다. 앞이 안 가린다")]
        [SerializeField] private Vector3 _readyPose = new Vector3(0.20f, -0.17f, 0.42f);
        [Tooltip("눈에 댔을 때 — 시선 한가운데. 카메라 근평면보다 멀어야 한다(안 그러면 잘려 안 보인다)")]
        [SerializeField] private Vector3 _eyePose = new Vector3(0.015f, -0.03f, 0.40f);

        [Header("눈에 대기")]
        [Tooltip("자세가 바뀌는 빠르기")]
        [SerializeField] private float _raiseSpeed = 9f;
        [Tooltip("끄면 손에 든 내내 눈앞에 둔다(키를 안 눌러도 된다)")]
        [SerializeField] private bool _raiseWithButton = true;
        [Tooltip("켜면 한 번 눌러 올리고 다시 눌러 내린다. 끄면 누르고 있는 동안만 올린다")]
        [SerializeField] private bool _raiseToggle = true;
#if ENABLE_INPUT_SYSTEM
        [Tooltip("이 키로 눈에 댄다. <b>우클릭과 왼쪽 Shift 는 쓰지 않는다</b> — 둘 다 이미 " +
                 "카메라 돌리기와 달리기가 물고 있어, 돋보기를 올리려면 화면이 같이 돌아갔다")]
        [SerializeField] private Key _raiseKey = Key.F;
#endif
        [Tooltip("오른쪽 단추를 누르고 있는 동안에도 올린다. 카메라 돌리기와 겹치므로 평소엔 꺼 둔다")]
        [SerializeField] private bool _alsoRightButton = false;

        [Header("들여다보기")]
        [Tooltip("한곳을 이만큼(초) 들여다보면 읽은 것으로 친다")]
        // 0.8초는 <b>너무 빨랐다</b>. 대자마자 다 읽혀서, 무엇을 보고 있었는지 알기도
        // 전에 넘어간다. 들여다보는 것은 순간이 아니라 자세이므로 그만한 시간이 든다.
        [SerializeField] private float _readSeconds = 2.4f;
        [Tooltip("렌즈로 짚을 수 있는 거리(m)")]
        [SerializeField] private float _reach = 6f;

        [Header("소품")]
        [Tooltip("씬에 있는 돋보기 소품을 그대로 쓴다. 소품의 유리 자리를 스스로 재서 " +
                 "그 위에 렌즈 그림을 얹는다 — 테도 자루도 술도 네가 만든 것 그대로다")]
        [SerializeField] private bool _useProp = true;
        [Tooltip("비우면 같은 도구 id의 HeldToolModel 소품을 찾아 쓴다")]
        [SerializeField] private Transform _prop;
        [Tooltip("잰 유리 반지름에 곱한다. 1보다 조금 작아야 그림이 테 안쪽에 앉는다")]
        // 0.82 는 <b>넘쳤다</b>. 테를 재는 셈이 술과 목까지 걸려 실제보다 크게 잡히는
        // 일이 있어서, 그 위에 0.82 를 곱해도 알이 테 밖으로 비어져 나왔다 —
        // 동그라미 너머까지 유리가 있는 것처럼 보이던 것이 이것이다. 넉넉히 줄인다.
        // 0.62 는 <b>모자랐다</b>. 넘치는 것을 고치려다 반대로 갔다 — 알이 테 지름의
        // 62% 라 동그라미 안이 휑하니 비고, 테와 그림 사이에 빈 띠가 돌았다.
        // 재는 셈(테 정점까지 거리의 9할)이 이미 술·목을 걸러 내므로, 여기서는 유리가
        // 테에 닿을 만큼만 살짝 줄인다.
        [Range(0.4f, 1.1f)] [SerializeField] private float _glassInset = 0.94f;
        [Tooltip("소품의 앞뒤가 뒤집혀 보이면 켠다(유리 법선이 반대인 모델)")]
        [SerializeField] private bool _flipProp = false;

        [Header("손으로 맞추기 — 잰 값이 어긋날 때")]
        // 소품을 <b>재서</b> 유리 자리를 잡는 것이 여기 원칙이다. 모델을 바꿔도 따라오니
        // 대개는 맞는다. 그런데 테에 술이 달렸거나 알이 두 겹이거나 자루가 굽은 소품에서는
        // 재는 셈이 몇 mm 씩 어긋나고, 그때마다 재는 규칙을 고치면 다른 소품이 어긋난다.
        // 그래서 <b>잰 값 위에 손으로 더하는 자리</b>를 따로 둔다 — 규칙은 그대로 두고
        // 이 소품만 밀어 맞춘다. 0이면 잰 그대로다.
        [Tooltip("잰 유리 자리에서 이만큼 옮긴다(소품 기준 로컬, m). " +
                 "알이 테 한가운데서 밀려 보일 때 1mm(0.001)씩 준다")]
        [SerializeField] private Vector3 _glassNudge = Vector3.zero;

        [Tooltip("잰 유리 크기에 이만큼 곱한다. 알이 테보다 크면 1보다 작게, 안쪽이 비면 크게")]
        [Range(0.3f, 2f)] [SerializeField] private float _glassScaleTweak = 1f;

        [Tooltip("끄면 소품을 <b>재지 않는다</b> — 위의 '유리 반지름'과 아래 자리를 그대로 쓴다. " +
                 "재는 셈이 이 소품에서만 자꾸 어긋날 때, 한 번 손으로 맞춰 두고 잠근다")]
        [SerializeField] private bool _measureProp = true;

        [Tooltip("재지 않을 때 쓸 유리 한가운데(소품 뿌리 기준 로컬 좌표)")]
        [SerializeField] private Vector3 _glassCenterManual = Vector3.zero;

        [Header("소품 동작")]
        [Tooltip("소품에 붙은 동작 이름. 눈에 대는 정도에 맞춰 이 클립을 <b>긁어</b> 돌린다 — " +
                 "재생하는 것이 아니라 들어 올린 만큼의 프레임을 그때그때 보여 준다. " +
                 "그래서 술이 손과 한 몸으로 움직이고, 중간에 멈추면 술도 그 자리에 멈춘다")]
        [SerializeField] private string _liftState = "Tassel_Lift";
        [Tooltip("비우면 소품에서 Animator 를 찾아 쓴다")]
        [SerializeField] private Animator _propAnimator;
        [Tooltip("들어 올린 정도를 클립 어디까지 쓸지(긁기 방식일 때만). 1이면 클립 전체를 쓴다")]
        [Range(0.2f, 1f)] [SerializeField] private float _liftClipSpan = 1f;

        [Tooltip("켜면 눈에 대는 순간 동작을 <b>제 속도로 튼다</b>(1.63초). 끄면 들어 올린 정도에 맞춰 긁는다. " +
                 "긁는 쪽은 손과 술이 한 몸으로 움직이나, 눈에 대는 데 0.1초뿐이라 1.63초짜리 흔들림이 " +
                 "그 안에 뭉개져 아무 일도 안 일어난 것처럼 보인다. 술은 손보다 늦게 따라오는 것이 맞다")]
        [SerializeField] private bool _playLiftOnRaise = true;

        [Tooltip("이만큼 들어 올리면 동작을 튼다(0~1)")]
        [Range(0.05f, 0.9f)] [SerializeField] private float _liftTriggerAt = 0.15f;

        private bool _liftPlaying;

        private static MagnifierLens _instance;

        /// <summary>지금 돋보기를 손에 들고 있나.</summary>
        public static bool Held => _instance != null && _instance._held;

        /// <summary>지금 눈에 대고 들여다보는 중인가(그냥 들고만 있는 것과 다르다).</summary>
        public static bool Peering => _instance != null && _instance._held && _instance._raise > 0.55f;

        /// <summary>눈에서 렌즈 한가운데를 지나는 광선. 무엇을 들여다보는지 밖에서 볼 때.</summary>
        public static Ray Sight => _instance != null && _instance._lens != null
            ? new Ray(_instance._eye.position, (_instance._lens.position - _instance._eye.position).normalized)
            : new Ray(Vector3.zero, Vector3.forward);

        /// <summary>렌즈 한가운데가 지금 이 물건을 짚고 있나.</summary>
        public static bool IsOver(Component c)
        {
            if (c == null || _instance == null || _instance._focus == null) return false;
            return _instance._focus == c.transform || _instance._focus.IsChildOf(c.transform);
        }

        private Transform _eye;          // 눈(=카메라)
        private Transform _lens;         // 유리가 매달린 자리
        private Camera _lensCam;
        private RenderTexture _rt;
        private Renderer _glass;
        private GameObject _rig;
        private bool _held;
        private float _raise;            // 0 = 비껴 듦, 1 = 눈에 댐
        private Transform _propRoot;     // 손으로 만든 돋보기 소품
        private Transform _holder;       // 소품을 매단 자리(HeldToolModel). 여기 자세가 곧 드는 자세다
        private Vector3 _holderRestPos;  // 씬에서 맞춰 둔 자리
        private Quaternion _holderRestRot;
        private Vector3 _holderRestScale = Vector3.one;

        /// <summary>원판을 소품의 알보다 눈 쪽으로 이만큼(m) 당겨 놓는다. 겹치면 소품 알이 이겨 가린다.</summary>
        private const float GlassPush = 0.025f;
        private float _glassRadiusBase = -1f;   // 잰 그대로의 유리 반지름(키우기 전)
        private bool _raiseLatch;
        private Vector3 _propGlassLocal; // 그 소품에서 유리 한가운데
        private Vector3 _propNormalLocal;
        private Vector3 _propHandleLocal;

        /// <summary>테의 <b>두께 절반</b>(소품 메시 기준). 유리를 얼마나 앞으로 빼야 하는지 여기서 나온다.</summary>
        private float _propHalfThick;
        private Transform _focus;
        private Canvas[] _canvases = new Canvas[0];
        private float _canvasAge;
        private IMagnifiable _reading;
        private float _dwell;

        // ── 씬이 올라오면 스스로 붙는다 ──

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            Ensure();
        }

        private static void OnScene(Scene s, LoadSceneMode m) => Ensure();

        /// <summary>
        /// 이 씬에 돋보기를 세운다 — <b>도구를 쓰는 씬에서만</b>.
        ///
        /// 여태는 씬이 열릴 때마다 무턱대고 섰다. 그러니 도구가 아예 없는 어전과
        /// 조사청에서도 서서, 소품을 못 찾았다고 <b>매번 경고 한 줄</b>을 남겼다 —
        /// 고쳐야 할 것이 없는데 콘솔에는 늘 노란 줄이 있는 꼴이라, 정작 무언가
        /// 잘못됐을 때 그것이 묻힌다. 게다가 쓰지도 않을 렌즈 카메라와
        /// RenderTexture 한 장을 어전에서도 만들고 있었다.
        ///
        /// 씬에 <see cref="HeldToolModel"/> 이 하나라도 있으면 도구를 쓰는 씬이다.
        /// 없으면 서지 않는다 — 경고도 없다. 없는 것을 나무랄 일이 아니다.
        /// </summary>
        private static void Ensure()
        {
            if (_instance != null) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (FindFirstObjectByType<HeldToolModel>(FindObjectsInactive.Include) == null) return;
            var go = new GameObject("돋보기_렌즈");
            go.transform.SetParent(cam.transform, false);
            _instance = go.AddComponent<MagnifierLens>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            _eye = transform.parent != null ? transform.parent : transform;
            Build();
            SetShown(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }

        // ── 만들기 ──

        private void Build()
        {
            _rig = new GameObject("돋보기");
            _rig.transform.SetParent(transform, false);
            _lens = _rig.transform;

            if (_useProp) FindProp();      // 소품이 있으면 그 유리 크기를 따른다

            // 유리 — 원판 하나. UV는 [0,1] 정사각을 그대로 덮는다(렌즈 카메라가 찍는 넓이와 같다)
            var glassGo = new GameObject("유리");
            glassGo.transform.SetParent(_lens, false);
            var mf = glassGo.AddComponent<MeshFilter>();
            mf.sharedMesh = Disc(48);
            _glass = glassGo.AddComponent<MeshRenderer>();
            _glass.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _glass.receiveShadows = false;
            glassGo.transform.localScale = Vector3.one * _glassRadius;
            // 소품의 유리 알보다 눈 쪽으로 조금 — 같은 자리에 겹치면 소품의 알이 이겨서
            // 렌즈 그림이 시커멓게 가려진다
            if (_propRoot != null) glassGo.transform.localPosition = new Vector3(0f, 0f, -GlassPush);

            _rt = new RenderTexture(_texSize, _texSize, 24, RenderTextureFormat.DefaultHDR);
            _rt.name = "돋보기_렌즈그림";
            _rt.Create();

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Texture");
            var glassMat = new Material(unlit) { name = "돋보기_유리" };
            SetColor(glassMat, Color.white);
            SetTex(glassMat, _rt);
            DrawOnTop(glassMat);
            _glass.sharedMaterial = glassMat;

            // 테도 자루도 <b>만들지 않는다</b>.
            //
            // 한때 소품을 못 찾으면 여기서 원판과 테와 자루를 빚어 썼다. 없는 것보다
            // 낫다고 여겼는데, 그것이 손으로 만들어 넣은 돋보기 대신 눈앞에 떠 있는
            // 흰 물건의 정체였다. 못 찾으면 <b>안 만드는 것</b>이 맞다 — 흉내가 눈앞에
            // 떠 있으면 진짜가 왜 안 나오는지조차 알 수 없다.
            // 유리(렌즈 그림을 얹는 원판)만은 만든다. 그것은 흉내가 아니라 이 부품이
            // 하는 일 자체이고, 소품을 찾으면 그 알 위에 겹쳐 앉는다.
            if (_propRoot == null)
                Debug.LogWarning("[돋보기] 손에 드는 소품(HeldToolModel toolId=" + _toolId +
                                 ")을 못 찾았습니다 — 유리만 뜹니다. 씬에 소품을 달아 주세요.");

            // 눈 자리에 두는 렌즈 카메라. 유리가 가리는 각의 1/배율 만큼만 찍는다
            var camGo = new GameObject("돋보기_카메라");
            camGo.transform.SetParent(transform, false);
            _lensCam = camGo.AddComponent<Camera>();
            var main = _eye.GetComponent<Camera>();
            _lensCam.clearFlags = CameraClearFlags.SolidColor;
            _lensCam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            if (main != null)
            {
                // 눈은 못 보고 <b>유리만 보는</b> 층을 하나 둔다. 눌린 자국처럼 있는 줄도
                // 몰랐던 것이 여기 산다(<see cref="PressedMarks"/>). 눈의 카메라에서 빼고
                // 렌즈 카메라에만 더하면, 숨기고 드러내는 장치가 따로 필요 없다 —
                // 유리를 통해 보면 있고 치우면 없다. 각도를 돌려도 멀리서 보아도 그대로다.
                int hidden = LayerMask.NameToLayer(PressedMarks.LayerName);
                if (hidden >= 0)
                {
                    main.cullingMask &= ~(1 << hidden);
                    _lensCam.cullingMask = main.cullingMask | (1 << hidden);
                }
                else _lensCam.cullingMask = main.cullingMask;

                _lensCam.farClipPlane = main.farClipPlane;
            }
            // <b>눈이 보는 방식 그대로 찍는다.</b>
            //
            // 여태 렌즈 카메라는 맨 Camera 하나였다. URP 에서 후처리를 태우려면
            // UniversalAdditionalCameraData 가 있어야 하는데 그것이 없으니, 이 카메라만
            // <b>노출·톤매핑을 안 거친 날것</b>을 찍었다. 밤 씬은 후처리로 밝히는 씬이라
            // 날것은 거의 검다 — 눈으로 보는 방은 훤한데 <b>알 안만 캄캄하던</b> 것이
            // 이것이었다. 간간이가 아니라 어두운 데서는 늘 그랬다.
            CopyCameraLook(main, _lensCam);

            _lensCam.aspect = 1f;
            _lensCam.targetTexture = _rt;
            _lensCam.depth = -10f;      // 눈보다 먼저 찍어야 이번 프레임 그림이 유리에 오른다
            _lensCam.enabled = false;   // 손에 들 때만 켠다(SetShown). 켜 두면 URP 가 알아서 그린다
        }

        /// <summary>
        /// 씬에 있는 돋보기 소품을 찾아 <b>유리가 어디에 얼마만 한가</b>를 잰다.
        ///
        /// 재는 방법: 돋보기는 납작한 물건이라 메시가 한 축으로 얇다(그 축이 유리의 법선).
        /// 남은 두 축 가운데 긴 쪽이 자루-유리 방향이고, 그 절반씩을 견주면 <b>넓은 쪽이
        /// 유리, 좁은 쪽이 자루</b>다. 유리 쪽 정점들의 한가운데가 유리 한가운데이고,
        /// 거기서 잰 거리의 9할 되는 값이 테의 반지름이다(술이나 고리 같은 튀어나온 것에
        /// 끌려가지 않게 가장 먼 값을 그대로 쓰지 않는다).
        ///
        /// 이렇게 재 두면 소품을 다른 것으로 갈아도 코드를 고칠 일이 없다.
        /// </summary>
        /// <summary>
        /// 소품의 메시를 꺼낸다. 뼈가 든 소품(스킨메시)은 <b>지금 자세로 구워</b> 온다 —
        /// 뼈에 매인 정점은 원본 배열이 묶인 자세(bind pose)라, 그대로 재면 유리가
        /// 엉뚱한 데 있는 것으로 나온다.
        /// </summary>
        private static Mesh MeshOf(Transform t)
        {
            var sk = t.GetComponent<SkinnedMeshRenderer>();
            if (sk != null && sk.sharedMesh != null)
            {
                var baked = new Mesh { name = "돋보기_소품_잰것" };
                // useScale=true 로 구워야 원본 메시와 <b>같은 단위</b>가 나온다(재어 보니
                // 0.0038 대 0.0037). false 로 구우면 93배 커진 값이 나와, 아래에서 lossyScale 을
                // 또 곱하는 순간 유리 반지름이 5m 가 된다 — 한 번 그렇게 겪었다.
                sk.BakeMesh(baked, true);
                return baked;
            }
            var mf = t.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        private void FindProp()
        {
            if (_prop == null && _eye != null)
            {
                // <b>켜져 있는 것을 먼저, 없으면 꺼진 것이라도</b> 집는다.
                //
                // 여태 꺼진 모델을 건너뛰었다. 소품을 갈아 끼우며 옛것을 꺼서 남겨 두는
                // 일이 흔해서 그것을 피하자는 뜻이었는데, 정작 <b>손에 드는 소품은 늘
                // 꺼져 있다</b> — HeldToolModel 이 도구를 들기 전까지 감춰 두기 때문이다.
                // 그래서 이 부품은 씬에 멀쩡히 달린 돋보기를 한 번도 못 찾았고,
                // 못 찾을 때마다 제가 흉내를 빚어 눈앞에 띄웠다.
                // 켜진 것을 먼저 보되, 하나도 없으면 꺼진 것을 집는다 — 옛것을 피하려던
                // 뜻은 살고, 정상인 경우도 걸린다.
                Renderer spare = null;
                Transform spareHolder = null;
                foreach (var h in _eye.GetComponentsInChildren<HeldToolModel>(true))
                {
                    if (h.transform.IsChildOf(transform) || h.ToolId != _toolId) continue;

                    // 스킨메시도 받는다 — 술을 흔들려면 뼈가 있어야 하고, 뼈가 있으면
                    // MeshFilter 가 아니라 SkinnedMeshRenderer 다.
                    Renderer on = null, off = null;
                    foreach (var r in h.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                        if (r.gameObject.activeInHierarchy) { on = r; break; }
                        if (off == null) off = r;
                    }
                    if (on != null) { _prop = on.transform; _holder = h.transform; break; }
                    if (off != null && spare == null) { spare = off; spareHolder = h.transform; }
                }
                if (_prop == null && spare != null) { _prop = spare.transform; _holder = spareHolder; }
            }
            if (_prop == null) return;

            // 소품을 매단 자리. 씬에서 맞춰 둔 그 자세가 곧 평소 드는 자세다.
            if (_holder == null)
            {
                var h2 = _prop.GetComponentInParent<HeldToolModel>();
                _holder = h2 != null ? h2.transform : null;
            }
            if (_holder != null)
            {
                _holderRestPos = _holder.localPosition;
                _holderRestRot = _holder.localRotation;
                _holderRestScale = _holder.localScale;
            }

            var mesh = MeshOf(_prop);
            if (mesh == null) { _prop = null; return; }

            if (!mesh.isReadable)
            {
                // 못 읽으면 테두리 상자만으로 어림잡는다. 정확하진 않아도 안 보이는 것보다 낫다.
                var rr = _prop.GetComponent<Renderer>();
                _propRoot = _prop;
                _propGlassLocal = Vector3.zero;
                _propNormalLocal = Vector3.forward;
                _propHandleLocal = Vector3.up;
                if (rr != null) _glassRadius = Mathf.Min(rr.bounds.size.x, rr.bounds.size.y) * 0.35f;
                Debug.LogWarning("[돋보기] 소품 메시를 읽을 수 없어 유리 자리를 어림잡았다. " +
                                 "모델 임포트 설정에서 Read/Write 를 켜면 정확히 맞춘다.", _prop);
                return;
            }

            var v = mesh.vertices;
            var b = mesh.bounds;

            int flat = 0, lng = 0;                      // 얇은 축 · 긴 축
            var size = new float[] { b.size.x, b.size.y, b.size.z };
            for (int i = 1; i < 3; i++)
            {
                if (size[i] < size[flat]) flat = i;
                if (size[i] > size[lng]) lng = i;
            }
            int mid = 3 - flat - lng;

            // 긴 축을 반으로 갈라, 옆으로 더 넓게 퍼진 쪽을 유리로 본다
            float cut = b.center[lng];
            float loMin = 1e9f, loMax = -1e9f, hiMin = 1e9f, hiMax = -1e9f;
            for (int i = 0; i < v.Length; i++)
            {
                float m = v[i][mid];
                if (v[i][lng] < cut) { if (m < loMin) loMin = m; if (m > loMax) loMax = m; }
                else { if (m < hiMin) hiMin = m; if (m > hiMax) hiMax = m; }
            }
            bool glassOnHigh = (hiMax - hiMin) >= (loMax - loMin);

            // 유리 쪽 정점만 모은다
            var gx = new System.Collections.Generic.List<float>();
            var gy = new System.Collections.Generic.List<float>();
            float lo = 1e9f, hi = -1e9f;
            for (int i = 0; i < v.Length; i++)
            {
                bool high = v[i][lng] >= cut;
                if (high != glassOnHigh) continue;
                gx.Add(v[i][lng]); gy.Add(v[i][mid]);
                if (v[i][lng] < lo) lo = v[i][lng];
                if (v[i][lng] > hi) hi = v[i][lng];
            }
            int n = gx.Count;
            if (n < 32 || hi - lo < 1e-6f) { _prop = null; return; }

            // 테의 한가운데는 <b>가장 넓은 자리</b>로 찾는다.
            //
            // 정점을 통째로 평균 내면 자루가 붙은 목이며 술 같은 것이 한가운데를 끌어당겨,
            // 어긋난 자리를 유리로 알고 그림을 얹게 된다(테는 왼쪽 위, 그림은 오른쪽 아래).
            // 동그라미에서 가장 넓게 벌어지는 줄은 반드시 한가운데를 지나므로, 자루 방향으로
            // 잘게 썰어 가장 넓은 조각을 고르면 그 자리가 곧 유리의 한가운데다.
            const int B = 48;
            var bMin = new float[B]; var bMax = new float[B]; var bN = new int[B];
            for (int i = 0; i < B; i++) { bMin[i] = 1e9f; bMax[i] = -1e9f; }
            for (int i = 0; i < n; i++)
            {
                int bi = Mathf.Clamp(Mathf.FloorToInt((gx[i] - lo) / (hi - lo) * B), 0, B - 1);
                if (gy[i] < bMin[bi]) bMin[bi] = gy[i];
                if (gy[i] > bMax[bi]) bMax[bi] = gy[i];
                bN[bi]++;
            }
            // 테는 <b>끝에서부터</b> 찾는다.
            //
            // 통째로 가장 넓은 조각을 고르면 자루가 붙는 목(테와 자루 사이의 굵은 마디)이
            // 뽑힌다 — 거기도 위아래로 벌어져 있기 때문이다. 그러나 돋보기의 유리는 언제나
            // 손에서 가장 먼 끝에 있다. 그래서 끝에서 안쪽으로 걸어 들어오며 <b>처음 만나는
            // 봉우리</b>를 테로 삼는다. 그 뒤의 것은 이미 자루 쪽이다.
            int least = Mathf.Max(4, n / 200);          // 술 한 올처럼 성긴 조각은 세지 않는다
            int bestB = -1; float bestSpan = 0f;
            int fading = 0;
            for (int c2 = 0; c2 < B; c2++)
            {
                int i = glassOnHigh ? (B - 1 - c2) : c2;
                if (bN[i] < least) { if (bestB >= 0) fading++; continue; }
                float span = bMax[i] - bMin[i];
                if (span > bestSpan) { bestSpan = span; bestB = i; fading = 0; }
                else if (bestB >= 0 && span < bestSpan * 0.72f) fading++;
                else fading = 0;
                if (fading >= 3) break;                 // 봉우리를 지났다
            }
            if (bestB < 0) { _prop = null; return; }

            // 가장 넓은 조각은 대략의 자리를 줄 뿐이다(술 한 줌이 걸리면 그만큼 밀린다).
            // 그 언저리의 점들만 골라 <b>동그라미를 맞춰</b> 한가운데와 반지름을 다시 잡는다.
            float ax0 = lo + (hi - lo) * (bestB + 0.5f) / B;
            float ay0 = (bMin[bestB] + bMax[bestB]) * 0.5f;
            float rApprox = bestSpan * 0.5f;
            float cx = ax0, cy = ay0, rFit = rApprox;
            for (int pass = 0; pass < 3; pass++)
            {
                double Sx = 0, Sy = 0, Sxx = 0, Syy = 0, Sxy = 0, Sz = 0, Sxz = 0, Syz = 0;
                int m2 = 0;
                for (int i = 0; i < n; i++)
                {
                    float dx = gx[i] - cx, dy = gy[i] - cy;
                    float dd = Mathf.Sqrt(dx * dx + dy * dy);
                    if (Mathf.Abs(dd - rFit) > rFit * 0.22f) continue;      // 테 언저리만
                    double x = dx, y = dy, z = x * x + y * y;
                    Sx += x; Sy += y; Sxx += x * x; Syy += y * y; Sxy += x * y;
                    Sz += z; Sxz += x * z; Syz += y * z; m2++;
                }
                if (m2 < 24) break;
                double det = Sxx * (Syy * m2 - Sy * Sy) - Sxy * (Sxy * m2 - Sy * Sx) + Sx * (Sxy * Sy - Syy * Sx);
                if (System.Math.Abs(det) < 1e-18) break;
                double dA = Sxz * (Syy * m2 - Sy * Sy) - Sxy * (Syz * m2 - Sy * Sz) + Sx * (Syz * Sy - Syy * Sz);
                double dB = Sxx * (Syz * m2 - Sy * Sz) - Sxz * (Sxy * m2 - Sy * Sx) + Sx * (Sxy * Sz - Syz * Sx);
                double dC = Sxx * (Syy * Sz - Syz * Sy) - Sxy * (Sxy * Sz - Syz * Sx) + Sxz * (Sxy * Sy - Syy * Sx);
                double A = dA / det, Bq = dB / det, C = dC / det;
                double a = A * 0.5, bb = Bq * 0.5;
                double rsq = C + a * a + bb * bb;
                if (rsq <= 0) break;
                cx += (float)a; cy += (float)bb; rFit = (float)System.Math.Sqrt(rsq);
            }

            Vector3 center = Vector3.zero;
            center[lng] = cx;
            center[mid] = cy;
            center[flat] = b.center[flat];
            float rim = rFit;

            var normal = Vector3.zero; normal[flat] = 1f;
            var handle = Vector3.zero; handle[lng] = glassOnHigh ? -1f : 1f;   // 유리에서 자루 쪽

            _propRoot = _prop;

            // 소품이 동작을 들고 왔으면 그것도 같이 쥔다. 인스펙터로 따로 안 이어도 된다.
            // 동작은 대개 소품 <b>뿌리</b>에 붙는다(메시는 그 자식이다). 위아래로 다 찾는다.
            // <b>꺼진 것도 찾는다</b>. GetComponentInParent 는 기본이 켜진 것만 본다 —
            // 손에 드는 소품은 들기 전까지 꺼져 있으므로, 그냥 부르면 동작을 못 찾아
            // 술이 영영 안 흔들린다. 소품을 못 찾던 것과 같은 함정이다.
            if (_propAnimator == null) _propAnimator = _prop.GetComponentInParent<Animator>(true);
            if (_propAnimator == null) _propAnimator = _prop.GetComponentInChildren<Animator>(true);
            if (_propAnimator != null && _propAnimator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("[돋보기] 소품에 Animator 는 있는데 컨트롤러가 없습니다 — 술이 안 움직입니다.", _propAnimator);
                _propAnimator = null;
            }

            _propGlassLocal = center;
            _propNormalLocal = normal;
            _propHandleLocal = handle;
            _propHalfThick = b.size[flat] * 0.5f;
            // 손으로 잠가 둔 경우에는 <b>잰 값으로 덮지 않는다</b> — 인스펙터에 적어 둔
            // 반지름과 자리가 그대로 산다.
            if (_measureProp) _glassRadius = rim * Mathf.Abs(_prop.lossyScale[lng]) * _glassInset;

            PaintPropOnTop();

            Debug.Log("[돋보기] 소품 " + _prop.name + " 의 유리를 " + (_measureProp ? "쟀다" : "재지 않고 손값을 쓴다")
                      + " — 반지름 " + _glassRadius.ToString("F3") + "m, 잰 한가운데 " + center.ToString("F4")
                      + ", 실제로 쓰는 한가운데 " + GlassLocal.ToString("F4"), _prop);
        }

        /// <summary>
        /// 소품도 무엇보다 앞에 그린다 — 손에 쥔 종이(월드 Canvas)가 소품을 덮지 않게.
        /// 에셋의 재질은 건드리지 않는다. 실행 중에 뜨는 복사본에만 손댄다.
        /// </summary>
        private void PaintPropOnTop()
        {
            if (_propRoot == null) return;
            foreach (var r in _propRoot.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.materials;                 // 복사본 — 원본 에셋은 그대로다
                foreach (var m in mats) if (m != null) DrawOnTop(m);
                r.materials = mats;
            }
        }

        /// <summary>
        /// 술을 들어 올린 만큼 움직인다.
        ///
        /// 클립을 <b>틀지</b> 않고 <b>긁는다</b>. 트는 쪽이 쉬우나, 그러면 눈에 대는 데
        /// 0.1초 걸리는데 술은 1.63초짜리 클립을 처음부터 끝까지 돌리느라 손이 멈춘 뒤에도
        /// 혼자 흔들린다 — 손과 술이 딴 몸이 된다. 들어 올린 정도(_raise)를 그대로
        /// 클립의 어느 프레임인지로 삼으면 둘이 한 몸으로 움직이고, 중간에 멈추면
        /// 술도 그 자리에 선다. 되돌릴 때도 저절로 거꾸로 간다.
        /// </summary>
        private void DriveLift()
        {
            if (_propAnimator == null || string.IsNullOrEmpty(_liftState)) return;

            if (_playLiftOnRaise)
            {
                // 눈에 대기 시작하면 한 번 틀고, 제 속도로 끝까지 흔들리게 둔다.
                // 손은 0.1초에 올라가고 술은 1.63초에 걸쳐 따라온다 — 늦게 따라오는 것이
                // 술이라는 물건의 결이다. 다 내리면 다시 틀 수 있게 빗장을 푼다.
                bool up = _raise >= _liftTriggerAt;
                if (up && !_liftPlaying)
                {
                    _liftPlaying = true;
                    _propAnimator.speed = 1f;
                    _propAnimator.Play(_liftState, 0, 0f);
                }
                else if (!up && _liftPlaying)
                {
                    _liftPlaying = false;
                    _propAnimator.speed = 1f;
                    _propAnimator.Play(_liftState, 0, 0f);   // 내릴 때도 한 번 흔들린다
                }
                return;
            }

            // 긁기 — 차례가 중요하다. 속도를 0 으로 <b>먼저</b> 두면 Update(0) 이 표본을
            // 안 뜬다. 1 로 두고 찍은 뒤 0 으로 내린다(BokdongController 가 같은 차례를 쓴다).
            float t = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, _raise)) * _liftClipSpan;
            _propAnimator.speed = 1f;
            _propAnimator.Play(_liftState, 0, t);
            _propAnimator.Update(0f);
            _propAnimator.speed = 0f;                       // 저 혼자 흐르지 않게
        }

        /// <summary>
        /// 소품을 손에 쥔 자세로 옮긴다.
        ///
        /// <b>맞춰 둔 자세를 쓸 때</b>(<see cref="_useAuthoredPose"/>): 소품의 기울기에는
        /// 손도 대지 않는다. 씬에서 맞춰 둔 그대로 두고, 소품을 매단 자리를 통째로 밀어
        /// 유리 한가운데만 시선 위로 옮긴다. 그러고 나서 <b>렌즈 자리를 소품에 맞춘다</b>.
        ///
        /// 여태는 거꾸로였다 — 코드가 잡은 렌즈 자리에 소품을 끌어다 붙였다. 그래서
        /// 씬에서 아무리 각을 맞춰 두어도 플레이만 누르면 없던 일이 됐다.
        ///
        /// 미는 것은 피벗이 아니라 <b>유리 한가운데</b>다. 돋보기는 피벗이 자루 쪽에
        /// 있어서, 피벗을 시선에 맞추면 유리는 화면 밖으로 나간다.
        /// </summary>
        private void PlaceProp()
        {
            if (_propRoot == null) return;

            if (!_useAuthoredPose || _holder == null)
            {
                Vector3 nrm0 = _flipProp ? -_propNormalLocal : _propNormalLocal;
                Quaternion from0 = Quaternion.LookRotation(nrm0, _propHandleLocal);
                Quaternion to0 = Quaternion.LookRotation(-_lens.forward, -_lens.up);
                _propRoot.rotation = to0 * Quaternion.Inverse(from0);
                _propRoot.position += _lens.position - _propRoot.TransformPoint(GlassLocal);
                return;
            }

            // ── 진짜 손에 들려 있으면 <b>자리에 손을 안 댄다</b> ──────────
            //
            // 아래 셋(크기 키우기·눈 쪽으로 바로 세우기·눈앞으로 당기기)은 전부
            // <b>손이 없는 사람</b>을 위한 것이다. 화면으로 할 때는 돋보기를 눈에
            // 가져다 댈 손이 없으니 코드가 대신 가져다 대 준다.
            //
            // 헤드셋을 쓰면 그 손이 <b>있다</b>. 그런데도 코드가 계속 당기면,
            // 사람이 팔을 뻗어도 돋보기가 제자리로 튕겨 오고 — 무엇보다 이 값들은
            // <b>눈 기준</b>이라 컨트롤러에 매달린 것에 쓰면 손아귀에서 한 뼘씩
            // 어긋난 자리로 끌려간다.
            //
            // 남기는 것은 <b>렌즈를 유리에 맞추는 일</b>뿐이다(아래 3번). 유리가
            // 어디에 있든 눈에서 그리로 뚫어 보는 것이 돋보기다.
            if (HeldHand.InHand)
            {
                if (_glassRadiusBase < 0f) _glassRadiusBase = _glassRadius;
                _glassRadius = _glassRadiusBase;
                _lens.position = _propRoot.TransformPoint(GlassLocal);
                Vector3 off = _lens.position - _eye.position;
                if (off.sqrMagnitude > 1e-6f) _lens.rotation = Quaternion.LookRotation(off, _eye.up);
                return;
            }

            float t = Mathf.SmoothStep(0f, 1f, _raise);

            // 0) 크기 — 눈에 댈수록 커진다. 유리 원판과 배율 셈에 쓰는 반지름도 같이 키워야
            //    알 그림이 테 안에 그대로 앉는다(따로 놀면 그림이 테를 넘거나 안에서 논다).
            if (_glassRadiusBase < 0f) _glassRadiusBase = _glassRadius;
            float s = Mathf.Lerp(1f, Mathf.Max(1f, _eyeScale), t);
            _holder.localScale = _holderRestScale * s;
            _glassRadius = _glassRadiusBase * s;

            // 1) 기울기 — 맞춰 둔 그대로. 눈에 댈 때만 시킨 만큼 바로 세운다.
            _holder.localRotation = _holderRestRot;
            _holder.localPosition = _holderRestPos;
            if (_eyeStraighten > 0f && t > 0f)
            {
                // 유리 면이 지금 향한 쪽과, 향해야 할 쪽(눈).
                Vector3 nrm = _flipProp ? -_propNormalLocal : _propNormalLocal;
                Vector3 facing = _propRoot.TransformDirection(nrm);
                Vector3 toEye = _eye.position - _propRoot.TransformPoint(GlassLocal);
                if (facing.sqrMagnitude > 1e-6f && toEye.sqrMagnitude > 1e-6f)
                {
                    // <b>가장 짧은 길</b>로만 돌린다. 자세를 통째로 새로 지으면
                    // 자루가 감긴 방향까지 정해져 버려, 고개를 숙일 때 그 기준이
                    // 함께 기울다 어느 순간 홱 뒤집힌다.
                    Quaternion delta = Quaternion.FromToRotation(facing.normalized, toEye.normalized);
                    // 소품 뿌리가 아니라 매단 자리를 돌린다 — 뿌리를 돌리면 다음 프레임에 어긋난다.
                    _holder.rotation = Quaternion.Slerp(_holder.rotation, delta * _holder.rotation,
                                                        _eyeStraighten * t);
                }
            }

            // 2) 자리 — 유리 한가운데를 옮긴다. 맞춰 둔 자리에서 지금 어디 있는지 먼저 잰다.
            Vector3 rest = _eye.InverseTransformPoint(_propRoot.TransformPoint(GlassLocal));

            float floor = (Camera.main != null ? Camera.main.nearClipPlane : 0.05f) + 0.06f;
            Vector3 ready = rest + new Vector3(0f, 0f, _readyPush);
            Vector3 peek = new Vector3(0f, -_eyeDrop, Mathf.Max(floor, _eyeDistance));
            Vector3 want = Vector3.Lerp(ready, peek, t);
            if (want.z < floor) want.z = floor;

            // 미는 것은 월드로 옮겨 더한다. 매단 자리가 카메라 바로 밑이 아니라
            // 한 단계 더 들어가 있어도 그대로 맞는다.
            _holder.position += _eye.TransformVector(want - rest);

            // 3) 렌즈 자리를 소품의 유리에 맞춘다. 원판은 늘 눈을 마주 보므로 자리만 맞으면 된다.
            _lens.position = _propRoot.TransformPoint(GlassLocal);
            Vector3 away = _lens.position - _eye.position;
            if (away.sqrMagnitude > 1e-6f) _lens.rotation = Quaternion.LookRotation(away, _eye.up);
        }

        /// <summary>유리 한가운데(소품 기준). 잰 자리에 손으로 준 값을 더한 것.</summary>
        private Vector3 GlassLocal => (_measureProp ? _propGlassLocal : _glassCenterManual) + _glassNudge;

        /// <summary>반지름 1의 원판. UV는 (-1,1) 을 (0,1) 로 편다.</summary>
        private static Mesh Disc(int seg)
        {
            var m = new Mesh { name = "돋보기_원판" };
            var v = new Vector3[seg + 1];
            var uv = new Vector2[seg + 1];
            var tri = new int[seg * 3];
            // <b>세로를 뒤집지 않는다.</b>
            //
            // 한동안 여기서 v 를 (0.5 - y/2) 로 뒤집어 붙였다. "찍어 둔 그림은 위아래가
            // 뒤집혀 들어온다"고 여겼기 때문인데, <b>그것이 틀린 짐작이었다</b>.
            // 카메라가 RenderTexture 에 그린 그림은 어느 기계에서든 UV (0,0) 이
            // <b>왼쪽 아래</b>다 — 뒤집어 주는 일은 유니티가 이미 해 둔다.
            // 그래서 여기서 한 번 더 뒤집으면 알 안에서만 세상이 물구나무를 섰다.
            //
            // 원판의 로컬 +Y 는 화면 위쪽이고(유리는 늘 눈을 마주 보게 돌려 둔다),
            // 그 자리에 그림의 위쪽(uv.y = 1)이 와야 한다. 그러므로 그냥 편다.
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                v[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                uv[i + 1] = new Vector2(v[i + 1].x * 0.5f + 0.5f, v[i + 1].y * 0.5f + 0.5f);
            }
            for (int i = 0; i < seg; i++)
            {
                int n = (i + 1) % seg;
                tri[i * 3] = 0; tri[i * 3 + 1] = n + 1; tri[i * 3 + 2] = i + 1;
            }
            m.vertices = v; m.uv = uv; m.triangles = tri;
            m.RecalculateNormals();
            return m;
        }

        /// <summary>납작한 고리(테).</summary>
        private Transform Ring(string name, float thickness, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_lens, false);
            var m = new Mesh { name = "돋보기_테" };
            const int seg = 48;
            var v = new Vector3[seg * 2];
            var tri = new int[seg * 6];
            float inner = Mathf.Clamp01(1f - thickness / Mathf.Max(0.001f, _glassRadius));
            for (int i = 0; i < seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v[i * 2] = d; v[i * 2 + 1] = d * inner;
                int n = (i + 1) % seg;
                tri[i * 6 + 0] = i * 2; tri[i * 6 + 1] = n * 2; tri[i * 6 + 2] = i * 2 + 1;
                tri[i * 6 + 3] = n * 2; tri[i * 6 + 4] = n * 2 + 1; tri[i * 6 + 5] = i * 2 + 1;
            }
            m.vertices = v; m.triangles = tri; m.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = "돋보기_" + name };
            SetColor(mat, color);
            // 고리는 앞뒤가 없다 — 어느 쪽으로 감겼든 보이게 양면으로 둔다
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            DrawOnTop(mat);
            r.sharedMaterial = mat;
            return go.transform;
        }

        private static void SetTex(Material m, Texture t)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        /// <summary>
        /// 렌즈는 <b>무엇보다 앞</b>에 그린다.
        ///
        /// 손에 쥔 종이는 월드 공간 Canvas 로 그리는데, UI 는 깊이를 두고 다투지 않아
        /// 저보다 앞에 있는 유리를 덮어 버린다. 눈과 종이 사이에 든 유리가 종이 뒤로
        /// 숨는 셈이라, 정작 읽으려고 대면 아무 일도 안 일어난다.
        ///
        /// 그래서 깊이 견주기를 끄고 맨 나중에 그린다. 렌즈가 눈에 붙어 있는 물건이니
        /// 실제로도 늘 앞이다. 제 그림에 제가 찍히는 것은 렌즈 카메라의 근평면이 막는다.
        /// </summary>
        private static void DrawOnTop(Material m)
        {
            if (m.HasProperty("_ZTest"))
                m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            m.renderQueue = 4000;
        }

        // ── 매 프레임 ──

        private void LateUpdate()
        {
            var main = Camera.main;
            if (main == null) return;
            if (transform.parent != main.transform)   // 씬이 바뀌어 카메라가 갈렸다
            {
                _eye = main.transform;
                transform.SetParent(_eye, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                _prop = null; _propRoot = null;
                if (_useProp) FindProp();
            }

            bool want = ToolbeltHud.SelectedToolId == _toolId
                        && !JournalView.AnyOpen
                        && !InterrogationController.AnyOpen;
            if (want != _held) { _held = want; SetShown(want); }
            if (!_held) return;

            // 눈에 대기
            float target = _raiseWithButton ? (RaiseHeld() ? 1f : 0f) : 1f;
            _raise = Mathf.MoveTowards(_raise, target, _raiseSpeed * Time.deltaTime);

            bool authored = _useAuthoredPose && _propRoot != null && _holder != null;
            if (!authored)
            {
                var pose = Vector3.Lerp(_readyPose, _eyePose, Mathf.SmoothStep(0f, 1f, _raise));

                // 눈의 근평면보다 가까이 들면 유리가 통째로 잘려 나가 아무것도 안 보인다.
                // 한 번 겪고 나면 잊기 쉬운 종류의 일이라, 여기서 늘 밀어 둔다.
                float floor = main.nearClipPlane + 0.06f;
                if (pose.z < floor) pose *= floor / Mathf.Max(0.001f, pose.z);
                _lens.localPosition = pose;
                _lens.localRotation = Quaternion.Slerp(Quaternion.Euler(6f, -12f, 8f), Quaternion.identity, _raise);
            }

            // 차례가 바뀌었다. 맞춰 둔 자세를 쓰면 <b>소품이 렌즈 자리를 정하므로</b>
            // 소품을 먼저 놓고 그 다음에 카메라를 겨눈다.
            DriveLift();
            PlaceProp();
            AimLensCamera();
            Shoot();
            Look();
        }

        /// <summary>
        /// 눈에 대라는 신호인가.
        ///
        /// 예전에는 <b>우클릭이나 왼쪽 Shift</b> 를 누르고 있어야 올라갔다. 그런데 그 둘은
        /// 이미 임자가 있다 — 우클릭은 카메라 돌리기(DebugFlyCamera), Shift 는 달리기.
        /// 돋보기를 올리려면 화면이 같이 돌아가거나 사람이 뛰었고, 그 사실을 알려 주는
        /// 데도 없었다. 그래서 아무리 들어도 돋보기가 안 나갔다.
        ///
        /// 이제 제 키를 하나 준다(기본 F). 누르고 있을 필요도 없다 — 한 번 눌러 올리고
        /// 다시 눌러 내린다. 들여다보는 것은 순간이 아니라 <b>자세</b>이기 때문이다.
        /// </summary>
        private bool RaiseHeld()
        {
#if ENABLE_INPUT_SYSTEM
            // 오른쪽 단추는 <b>종이를 쥐고 있는 동안</b>에만 렌즈를 올린다.
            //
            // 여태 이 단추를 막아 두었다. 걸어 다닐 때 오른쪽 단추가 시점 회전이라
            // 둘이 겹치기 때문이다. 그런데 안내는 어디서나 "(오른쪽 단추)" 라고
            // 적혀 있었다 — 시킨 대로 눌러도 아무 일이 없으니, 등불은 되는데
            // 돋보기만 안 되는 것으로 보였다.
            // 종이를 쥐고 있는 동안에는 어차피 방을 짚지도 걷지도 않으므로(방을
            // 짚는 손은 이미 물러나 있다) 그때만 이 단추를 렌즈에 내준다.
            // 겹칠 일이 없어지고, 안내대로 하면 된다.
            // 두 도구가 <b>같은 손짓</b>을 쓴다(ToolRaise). 여기만 따로 두면 등불과
            // 어긋나고, 어긋나면 하나를 익혀도 다른 하나를 또 처음부터 익혀야 한다.
            if (ToolRaise.Held) return true;
            if (_alsoRightButton && Mouse.current != null && Mouse.current.rightButton.isPressed) return true;

            var kb = Keyboard.current;
            if (kb == null) return _raiseLatch;
            var key = kb[_raiseKey];
            if (key == null) return _raiseLatch;

            if (!_raiseToggle) return key.isPressed;
            if (key.wasPressedThisFrame) _raiseLatch = !_raiseLatch;
            return _raiseLatch;
#else
            return false;
#endif
        }

        /// <summary>렌즈 카메라를 눈에서 유리 한가운데를 지나는 방향으로 겨눈다.</summary>
        private void AimLensCamera()
        {
            Vector3 eye = _eye.position;
            Vector3 center = _lens.position;
            float dist = Mathf.Max(0.02f, Vector3.Distance(eye, center));

            _lensCam.transform.position = eye;
            _lensCam.transform.rotation = Quaternion.LookRotation(center - eye, _eye.up);

            // 유리가 가리는 반각 θ. 렌즈 카메라는 그 1/배율만 담는다 → 딱 그만큼 커 보인다
            float theta = Mathf.Atan2(_glassRadius, dist) * Mathf.Rad2Deg;

            // 종이를 짚고 있으면 더 조인다. 방 저쪽 물건과 손안의 잔글씨는 잘기가 다르다
            float zoom = _zoom;
            if (DocumentView.IsOpen && DocumentView.RayHitsPage(
                    new Ray(eye, (center - eye).normalized)))
                zoom *= _pageZoomBoost;

            _lensCam.fieldOfView = Mathf.Clamp(2f * theta / Mathf.Max(1.01f, zoom), 0.5f, 120f);

            // <b>근평면을 유리 뒤로 밀지 않는다.</b>
            //
            // 여태 dist + 0.06 이었다. 제 유리·테·자루를 안 찍으려는 뜻이었는데, 그 값이
            // 곧 <b>42cm 안쪽은 아무것도 안 찍힌다</b>는 말이 된다. 종이에 바짝 대는 순간
            // 볼 것이 통째로 잘려 나가고, 남는 것은 배경색뿐이라 <b>알이 시커멓게</b> 된다 —
            // 간간이 렌즈가 검어지던 것이 이것이다. 돋보기는 가까이 댈수록 잘 보여야 하는데
            // 가까이 대면 안 보였다.
            // 제 몸은 찍는 순간 <b>잠깐 감추면</b> 된다(Shoot). 창을 치우는 것과 같은 수법이다.
            var mainCam = _eye != null ? _eye.GetComponent<Camera>() : null;
            _lensCam.nearClipPlane = mainCam != null ? mainCam.nearClipPlane : 0.03f;

            // 유리는 늘 눈을 마주 본다(비스듬히 들어도 그림이 어긋나지 않게)
            _glass.transform.rotation = Quaternion.LookRotation(center - eye, _eye.up);

            // <b>테가 유리를 뚫고 나오던 것</b> — 알 오른쪽이 검게 파여 보이던 까닭.
            //
            // 여태 원판을 소품의 알에서 <b>고정된 2.5cm</b>만 눈 쪽으로 당겨 두었다.
            // 그런데 그 2.5cm 는 두 가지를 못 따라간다:
            //   · 눈에 댈 때 소품이 1.5배로 <b>커진다</b> — 테도 같이 두꺼워진다.
            //   · 돋보기는 늘 <b>비스듬히</b> 들린다 — 기울면 테의 한쪽이 그만큼 앞으로 나온다.
            // 그래서 기울어 나온 쪽 테가 원판보다 눈에 가까워지고, 그 자리만 원판을
            // 가려 <b>초승달 모양으로 검게</b> 파였다. 알이 반쯤 먹힌 것이 그것이다.
            //
            // 이제 그때그때 잰다. 테 두께 절반에, 기울어 나온 만큼(반지름 × sin)을 더하고,
            // 손가락 한 마디쯤 여유를 둔다. 그러면 어떻게 기울여 들어도 안 먹힌다.
            float push = GlassPush;
            if (_propRoot != null)
            {
                Vector3 toEye = (eye - center).normalized;
                Vector3 nrmW = _propRoot.TransformDirection(
                    _flipProp ? -_propNormalLocal : _propNormalLocal).normalized;
                float cosT = Mathf.Clamp(Mathf.Abs(Vector3.Dot(nrmW, toEye)), 0f, 1f);
                float sinT = Mathf.Sqrt(Mathf.Max(0f, 1f - cosT * cosT));

                int flatAx = Mathf.Abs(_propNormalLocal.x) > 0.5f ? 0
                           : Mathf.Abs(_propNormalLocal.y) > 0.5f ? 1 : 2;
                float halfW = _propHalfThick * Mathf.Abs(_propRoot.lossyScale[flatAx]);

                push = halfW + _glassRadius * sinT + 0.012f;
                _glass.transform.position = center + toEye * push;
            }

            // <b>당겨 놓은 만큼 줄인다.</b> 원판은 소품의 알보다 GlassPush 만큼 눈 쪽에
            // 있다. 가까운 것은 커 보이므로, 반지름을 그대로 두면 알이 테를 넘어 비어져
            // 나온다 — "동그라미 안에 안 맞는다"던 것의 나머지 절반이 이것이다.
            // 눈에서 본 크기가 테와 같아지도록 (dist-push)/dist 를 곱한다.
            float shrink = _propRoot != null ? Mathf.Max(0.05f, dist - push) / dist : 1f;
            _glass.transform.localScale = Vector3.one * (_glassRadius * shrink * _glassScaleTweak);

        }

        /// <summary>
        /// 렌즈 그림을 찍는다 — <b>떠 있는 창을 잠깐 치우고</b>.
        ///
        /// 돋보기는 <b>물건</b>을 크게 보는 것이지 글자판을 크게 보는 것이 아니다. 그냥
        /// 찍으면 자막이며 수첩이며 눈앞에 떠 있는 창까지 함께 부풀어, 유리 안에 글자
        /// 몇 개가 산더미처럼 들어앉는다. 그래서 찍는 그 한 순간만 창들을 꺼 둔다.
        /// 껐다 켜는 것은 같은 프레임 안에서 끝나므로 눈에는 아무 일도 없다.
        ///
        /// 손에 쥔 <b>종이</b>만은 남긴다 — 그것이야말로 들여다보라고 든 것이다.
        /// </summary>
        private void Shoot()
        {
            // <b>손으로 찍지 않는다.</b>
            //
            // 여태 여기서 _lensCam.Render() 를 불렀다. 그런데 URP 에서 그 부름은
            // <b>URP 를 통째로 건너뛴다</b> — 옛 방식으로 한 장 그리고 마는 것이라
            // 그림자도 후처리도 노출도 없다. 밤 씬은 후처리로 밝히는 씬이니 결과는
            // 거의 검다. <b>알 안만 캄캄하던</b> 진짜 까닭이 이것이었다.
            //
            // 이제 렌즈 카메라를 <b>정상 렌더 흐름에 태운다</b>(enabled=true, depth 를
            // 눈보다 앞에). 창을 치우고 제 몸을 감추는 일은 URP 가 이 카메라를 그리기
            // 직전·직후에 끼워 넣는다(BeginLens/EndLens).
            if (_lensCam != null) _lensCam.enabled = _held;
        }

        // ── 렌즈 카메라를 그리는 그 순간에만 끼어든다 ──
        //
        // 돋보기는 <b>물건</b>을 크게 보는 것이지 글자판을 크게 보는 것이 아니다. 그냥
        // 찍으면 자막이며 수첩이며 눈앞에 떠 있는 창까지 함께 부풀어, 유리 안에 글자
        // 몇 개가 산더미처럼 들어앉는다. 그래서 이 카메라를 그리는 동안만 창들을 꺼 둔다.
        // 손에 쥔 <b>종이</b>만은 남긴다 — 그것이야말로 들여다보라고 든 것이다.
        // 제 몸(유리·테·자루·술)도 함께 감춘다. 근평면으로 밀어내면 코앞의 것까지
        // 잘려 나가지만, 이렇게 감추면 코앞도 찍힌다.

        private readonly System.Collections.Generic.List<Canvas> _hiddenCanvases = new System.Collections.Generic.List<Canvas>();
        private readonly System.Collections.Generic.List<Renderer> _hiddenRenderers = new System.Collections.Generic.List<Renderer>();

        private void OnEnable()
        {
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += BeginLens;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += EndLens;
        }

        private void OnDisable()
        {
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= BeginLens;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= EndLens;
            Restore();
            if (_lensCam != null) _lensCam.enabled = false;
        }

        private void BeginLens(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _lensCam || _lensCam == null) return;

            _canvasAge -= Time.deltaTime;
            if (_canvasAge <= 0f)
            {
                _canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                _canvasAge = 0.5f;
            }

            foreach (var c in _canvases)
            {
                if (c == null || !c.enabled) continue;
                if (DocumentView.IsPageCanvas(c)) continue;   // 종이는 남긴다
                c.enabled = false;
                _hiddenCanvases.Add(c);
            }
            DocumentView.SetChromeVisible(false);

            HideSelf(_holder != null ? _holder : _propRoot, _hiddenRenderers);
            if (_glass != null && _glass.enabled) { _glass.enabled = false; _hiddenRenderers.Add(_glass); }
        }

        private void EndLens(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _lensCam) return;
            Restore();
        }

        private void Restore()
        {
            foreach (var r in _hiddenRenderers) if (r != null) r.enabled = true;
            _hiddenRenderers.Clear();
            if (_hiddenCanvases.Count == 0) return;
            DocumentView.SetChromeVisible(true);
            foreach (var c in _hiddenCanvases) if (c != null) c.enabled = true;
            _hiddenCanvases.Clear();
        }

        /// <summary>
        /// 눈의 카메라가 그림을 만드는 방식을 렌즈 카메라에 그대로 옮긴다 —
        /// 특히 <b>후처리</b>. 이것이 없으면 렌즈만 딴 세상처럼 어둡다.
        /// </summary>
        private static void CopyCameraLook(Camera from, Camera to)
        {
            if (from == null || to == null) return;
            to.allowHDR = from.allowHDR;
            to.allowMSAA = from.allowMSAA;

            var src = from.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var dst = to.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (dst == null) dst = to.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (src == null) { dst.renderPostProcessing = true; return; }

            dst.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
            dst.renderPostProcessing = src.renderPostProcessing;
            dst.volumeLayerMask = src.volumeLayerMask;
            dst.volumeTrigger = src.volumeTrigger;
            dst.requiresDepthOption = src.requiresDepthOption;
            dst.requiresColorOption = src.requiresColorOption;
            dst.renderShadows = src.renderShadows;

            // 안티에일리어싱은 굳이 안 따라간다 — 알 하나 찍자고 비싸질 것 없다.
            dst.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
        }

        /// <summary>찍는 한 순간만 돋보기 제 몸을 감춘다. 꺼 놓은 것을 돌려주어 도로 켠다.</summary>
        private static void HideSelf(Transform root, System.Collections.Generic.List<Renderer> offed)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled) continue;
                r.enabled = false;
                offed.Add(r);
            }
        }

        /// <summary>렌즈 한가운데가 무엇을 짚고 있나. 오래 짚으면 읽은 것으로 친다.</summary>
        private void Look()
        {
            var ray = new Ray(_eye.position, (_lens.position - _eye.position).normalized);

            // 손에 쥔 종이가 먼저다 — 종이를 펼쳐 놓고 그 위에 렌즈를 대는 것이 본디 쓰임이다
            if (DocumentView.IsOpen && DocumentView.RayHitsPage(ray))
            {
                _dwell += Time.deltaTime;
                DocumentView.Reading(_dwell / Mathf.Max(0.05f, _readSeconds));
                _focus = null; _reading = null;
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, _reach)) { Forget(); return; }

            _focus = hit.collider.transform;

            var m = hit.collider.GetComponentInParent<IMagnifiable>();
            if (!ReferenceEquals(m, _reading)) { _reading = m; _dwell = 0f; }
            if (_reading == null) return;

            _dwell += Time.deltaTime;
            _reading.OnMagnifiedGaze(_dwell / Mathf.Max(0.05f, _readSeconds));
        }

        private void Forget()
        {
            _focus = null;
            _reading = null;
            _dwell = 0f;
        }

        private void SetShown(bool on)
        {
            if (_rig != null) _rig.SetActive(on);
            if (_lensCam != null) _lensCam.enabled = on;
            if (!on) { _raise = 0f; _raiseLatch = false; Forget(); }
        }
    }

    /// <summary>
    /// 돋보기로 들여다보면 반응하는 것. 진행도(0~1)를 받는다 — 1을 넘으면 다 읽은 것이다.
    /// </summary>
    public interface IMagnifiable
    {
        void OnMagnifiedGaze(float progress);
    }
}
