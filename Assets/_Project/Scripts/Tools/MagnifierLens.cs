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

        [Header("드는 자세")]
        [Tooltip("평소 — 눈 아래 비껴 들고 있다. 앞이 안 가린다")]
        [SerializeField] private Vector3 _readyPose = new Vector3(0.20f, -0.17f, 0.42f);
        [Tooltip("눈에 댔을 때 — 시선 한가운데. 카메라 근평면보다 멀어야 한다(안 그러면 잘려 안 보인다)")]
        [SerializeField] private Vector3 _eyePose = new Vector3(0.015f, -0.03f, 0.40f);
        [Tooltip("자세가 바뀌는 빠르기")]
        [SerializeField] private float _raiseSpeed = 9f;
        [Tooltip("켜면 오른쪽 단추를 누르고 있는 동안만 눈에 댄다. 끄면 늘 눈앞에 둔다")]
        [SerializeField] private bool _raiseWithButton = true;

        [Header("들여다보기")]
        [Tooltip("한곳을 이만큼(초) 들여다보면 읽은 것으로 친다")]
        [SerializeField] private float _readSeconds = 0.8f;
        [Tooltip("렌즈로 짚을 수 있는 거리(m)")]
        [SerializeField] private float _reach = 6f;

        [Header("옛 돋보기 소품")]
        [Tooltip("같은 도구 id의 소품이 카메라에 매달려 있으면 끈다. " +
                 "그러지 않으면 돋보기가 둘이 된다 — 하나는 보이기만 하고 하나는 보이게 하는")]
        [SerializeField] private bool _hideOldProp = true;

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
        private Transform _focus;
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

        private static void Ensure()
        {
            if (_instance != null) return;
            var cam = Camera.main;
            if (cam == null) return;
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

            // 유리 — 원판 하나. UV는 [0,1] 정사각을 그대로 덮는다(렌즈 카메라가 찍는 넓이와 같다)
            var glassGo = new GameObject("유리");
            glassGo.transform.SetParent(_lens, false);
            var mf = glassGo.AddComponent<MeshFilter>();
            mf.sharedMesh = Disc(48);
            _glass = glassGo.AddComponent<MeshRenderer>();
            _glass.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _glass.receiveShadows = false;
            glassGo.transform.localScale = Vector3.one * _glassRadius;

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

            // 테와 자루 — 손에 쥔 물건이라는 느낌만 낸다. 유리와 같은 거리에 두어
            // 렌즈 카메라의 근평면에 함께 잘려 나가게 한다(제 몸을 찍지 않게)
            var rim = Ring("테", 0.016f, new Color(0.46f, 0.36f, 0.17f));
            rim.localScale = new Vector3(_glassRadius * 1.20f, _glassRadius * 1.20f, 1f);
            rim.localPosition = new Vector3(0f, 0f, -0.001f);   // 유리보다 눈 쪽 — 테가 유리를 두른다

            var grip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(grip.GetComponent<Collider>());
            grip.name = "자루";
            grip.transform.SetParent(_lens, false);
            grip.transform.localPosition = new Vector3(0f, -_glassRadius * 1.75f, 0.002f);
            grip.transform.localScale = new Vector3(0.011f, _glassRadius * 0.75f, 0.011f);
            var wood = new Material(unlit) { name = "돋보기_자루" };
            SetColor(wood, new Color(0.24f, 0.15f, 0.09f));
            DrawOnTop(wood);
            grip.GetComponent<Renderer>().sharedMaterial = wood;

            // 눈 자리에 두는 렌즈 카메라. 유리가 가리는 각의 1/배율 만큼만 찍는다
            var camGo = new GameObject("돋보기_카메라");
            camGo.transform.SetParent(transform, false);
            _lensCam = camGo.AddComponent<Camera>();
            var main = _eye.GetComponent<Camera>();
            _lensCam.clearFlags = CameraClearFlags.SolidColor;
            _lensCam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            if (main != null)
            {
                _lensCam.cullingMask = main.cullingMask;
                _lensCam.farClipPlane = main.farClipPlane;
            }
            _lensCam.aspect = 1f;
            _lensCam.targetTexture = _rt;
            _lensCam.depth = -10f;         // 눈보다 먼저 찍어야 이번 프레임 그림이 유리에 오른다
            _lensCam.enabled = false;

            if (_hideOldProp) HideOldProp();
        }

        /// <summary>같은 도구의 옛 소품을 끈다 — 돋보기가 둘이 되지 않게.</summary>
        private void HideOldProp()
        {
            if (_eye == null) return;
            foreach (var h in _eye.GetComponentsInChildren<HeldToolModel>(true))
            {
                if (h.transform.IsChildOf(transform)) continue;
                if (h.ToolId != _toolId) continue;
                h.gameObject.SetActive(false);
                Debug.Log("[돋보기] 옛 소품 " + h.name + " 을 껐다 — 이제 진짜 렌즈가 그 자리를 대신한다.");
            }
        }

        /// <summary>반지름 1의 원판. UV는 (-1,1) 을 (0,1) 로 편다.</summary>
        private static Mesh Disc(int seg)
        {
            var m = new Mesh { name = "돋보기_원판" };
            var v = new Vector3[seg + 1];
            var uv = new Vector2[seg + 1];
            var tri = new int[seg * 3];
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
                if (_hideOldProp) HideOldProp();
            }

            bool want = ToolbeltHud.SelectedToolId == _toolId
                        && !JournalView.AnyOpen
                        && !InterrogationController.AnyOpen;
            if (want != _held) { _held = want; SetShown(want); }
            if (!_held) return;

            // 눈에 대기
            float target = _raiseWithButton ? (RaiseHeld() ? 1f : 0f) : 1f;
            _raise = Mathf.MoveTowards(_raise, target, _raiseSpeed * Time.deltaTime);
            var pose = Vector3.Lerp(_readyPose, _eyePose, Mathf.SmoothStep(0f, 1f, _raise));

            // 눈의 근평면보다 가까이 들면 유리가 통째로 잘려 나가 아무것도 안 보인다.
            // 한 번 겪고 나면 잊기 쉬운 종류의 일이라, 여기서 늘 밀어 둔다.
            float floor = main.nearClipPlane + 0.06f;
            if (pose.z < floor) pose *= floor / Mathf.Max(0.001f, pose.z);
            _lens.localPosition = pose;
            _lens.localRotation = Quaternion.Slerp(Quaternion.Euler(6f, -12f, 8f), Quaternion.identity, _raise);

            AimLensCamera();
            Look();
        }

        private static bool RaiseHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed) return true;
            var kb = Keyboard.current;
            if (kb != null && kb.leftShiftKey.isPressed) return true;
#endif
            return false;
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
            _lensCam.fieldOfView = Mathf.Clamp(2f * theta / Mathf.Max(1.01f, _zoom), 0.5f, 120f);
            _lensCam.nearClipPlane = dist + 0.02f;    // 제 유리·테·자루는 찍지 않는다
            _lensCam.enabled = true;

            // 유리는 늘 눈을 마주 본다(비스듬히 들어도 그림이 어긋나지 않게)
            _glass.transform.rotation = Quaternion.LookRotation(center - eye, _eye.up);
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
            if (!on) { _raise = 0f; Forget(); }
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
