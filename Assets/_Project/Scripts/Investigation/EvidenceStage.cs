using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>물건을 어둠 위에 세워 돌려 보는 무대.</b>
    ///
    /// 우리 물증은 대개 <b>종이</b>였다. 종이는 평면이라 판을 그대로 돌리면 됐고,
    /// 그래서 여태 무대가 없었다. 그런데 어사가 받는 것에는 종이 아닌 것이 섞인다 —
    /// 마패, 유척, 비틀려 부서진 자물쇠. 그런 것은 <b>앞뒤가 아니라 사방</b>이 있다.
    ///
    /// <b>견우팀 <c>InventoryPreview</c> 의 얼개를 그대로 받아 왔다.</b> 저쪽이 이미
    /// 겪고 적어 둔 함정이 넷이고, 그 넷을 다시 겪을 까닭이 없다:
    ///
    ///   · <b>씬 한복판에 모델을 띄우지 않는다.</b> 벽·가구에 파묻히고, 밤 씬이면
    ///     캄캄해진다. 무대를 <b>멀리 떼어</b> 두고 전용 카메라로만 찍으면 어느 씬에서
    ///     열어도 같은 그림이 나온다.
    ///   · <b>레이어를 안 쓴다.</b> 레이어를 늘리면 <c>TagManager</c> 가 바뀌어 팀 전체에
    ///     퍼진다. 대신 무대를 멀찍이(본 카메라 far 밖) 두고 이쪽 far 를 20m 로 줄인다.
    ///   · <b>MSAA 를 켜지 않는다.</b> MSAA 렌더텍스처는 그대로 샘플링할 수 없어
    ///     판에 <b>청록색 한 장</b>만 뜬다. 원인 찾기가 고약한 종류다.
    ///   · <b>URP 에서는 <c>Camera.Render()</c> 를 직접 못 부른다.</b> 볼 때만 카메라를
    ///     켜는 것으로 대신한다.
    ///
    /// 우리 무대는 저쪽과 <b>반대편</b>(−5000)에 세운다. 한 씬에 둘이 설 일은 없지만,
    /// 언젠가 겹치면 서로의 조명이 상대 무대를 물들인다 — 그것도 원인 찾기 고약한 종류다.
    ///
    /// 조명은 셋(키·필·림)이다. 디렉셔널을 쓰면 무대만이 아니라 씬 전체가 밝아진다.
    /// </summary>
    public static class EvidenceStage
    {
        /// <summary>씬에서 멀찍이 떨어진 무대 자리. 견우팀 무대(+5000)의 반대편이다.</summary>
        private static readonly Vector3 StageOrigin = new Vector3(-5000f, -5000f, -5000f);

        private const int RtSize = 1024;

        private static Transform _stage;
        private static Transform _pivot;
        private static Camera _cam;
        private static RenderTexture _rt;
        private static GameObject _model;
        private static float _baseDistance = 1f;
        private static float _zoom = 1f;

        /// <summary>무대에 올린 그림. 판에 얹어 쓴다.</summary>
        public static RenderTexture Texture { get { return _rt; } }

        /// <summary>지금 무대에 무엇이 올라 있는가.</summary>
        public static bool HasModel { get { return _model != null; } }

        private static void EnsureStage()
        {
            if (_stage != null) return;

            // <b>지난 판의 무대를 걷는다.</b> 무대는 저장하지 않는(DontSave) 물건이라 재생을
            // 멈춰도 에디터에 남고, 정적 참조만 비워진 채 다음 재생에서 새 무대가 또 선다.
            // 같은 자리(−5000)에 무대가 겹겹이 서니 조명이 서너 벌 겹쳐 물건이 하얗게 날아가고,
            // 지난번에 올린 팔과 자가 이번 그림에 비쳐 들었다(관아에서 실제로 그랬다).
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t != null && t.parent == null && t.name == "물증_무대")
                    Object.DestroyImmediate(t.gameObject);

            var root = new GameObject("물증_무대") { hideFlags = HideFlags.DontSave };
            root.transform.position = StageOrigin;
            _stage = root.transform;
            Object.DontDestroyOnLoad(root);

            _pivot = new GameObject("회전축").transform;
            _pivot.SetParent(_stage, false);

            // MSAA 를 켜면 판에 청록색 한 장만 뜬다(위 참조).
            _rt = new RenderTexture(RtSize, RtSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "물증_무대RT",
                antiAliasing = 1,
            };
            _rt.Create();

            var camGo = new GameObject("무대카메라");
            camGo.transform.SetParent(_stage, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // 알파 0 — 물건만 남는다
            _cam.fieldOfView = 32f;
            _cam.nearClipPlane = 0.02f;
            _cam.farClipPlane = 20f;                            // 본 카메라와 서로 안 보이게
            _cam.targetTexture = _rt;
            _cam.enabled = false;                               // 볼 때만 켠다

            var extra = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (extra == null) extra = camGo.AddComponent<UniversalAdditionalCameraData>();
            extra.renderPostProcessing = false;   // 씬의 톤매핑이 무대까지 물들이지 않게
            extra.renderShadows = false;
            extra.requiresColorOption = CameraOverrideOption.Off;
            extra.requiresDepthOption = CameraOverrideOption.Off;

            // <b>바깥 해를 무대에 들이지 않는다.</b> 방향광은 거리가 없어 −5000 에 선 무대까지
            // 그대로 비춘다. 밤 마당에서는 티가 안 났지만 관아 한낮에는 무대 조명에 해가 겹쳐
            // 팔이 하얗게 날아가 흉터가 안 보였다. 무대 카메라가 그리는 동안만 끈다.
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= HideSun;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= ShowSun;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += HideSun;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += ShowSun;

            AddLight("키", new Vector3(-0.8f, 1.0f, -1.1f), new Color(1f, 0.96f, 0.88f), 6f);
            AddLight("필", new Vector3(1.1f, 0.2f, -0.7f), new Color(0.82f, 0.86f, 1f), 2.2f);
            AddLight("림", new Vector3(0.2f, 0.6f, 1.2f), new Color(1f, 0.88f, 0.72f), 3f);
        }

        private static readonly System.Collections.Generic.List<Light> _suns = new System.Collections.Generic.List<Light>();

        private static void HideSun(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == null || cam != _cam) return;
            _suns.Clear();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || !l.enabled || l.type != LightType.Directional) continue;
                if (_stage != null && l.transform.IsChildOf(_stage)) continue;
                l.enabled = false;
                _suns.Add(l);
            }
        }

        private static void ShowSun(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == null || cam != _cam) return;
            for (int i = 0; i < _suns.Count; i++) if (_suns[i] != null) _suns[i].enabled = true;
            _suns.Clear();
        }

        private static void AddLight(string name, Vector3 at, Color color, float intensity)
        {
            var go = new GameObject("조명_" + name);
            go.transform.SetParent(_stage, false);
            go.transform.localPosition = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity * _lightScale;
            _lights.Add(l);
            _lightBase.Add(intensity);
            l.range = 4f;
            l.shadows = LightShadows.None;
        }

        /// <summary>
        /// 물건을 무대에 올리고 <b>화면에 꽉 차게</b> 잡는다.
        ///
        /// 자동 프레이밍: 모델 둘레의 가운데를 회전축 원점으로 끌어오고, 그 반지름에서
        /// 카메라 거리를 낸다. 물건마다 크기가 제각각이라 자리를 손으로 잡아 두면
        /// 마패는 코앞이고 유척은 점이 된다.
        /// </summary>
        public static void Show(GameObject prefab, Vector3 euler)
        {
            EnsureStage();
            _stage.gameObject.SetActive(true);   // 둘레를 재려면 켜져 있어야 한다
            Clear();
            if (prefab == null) return;

            _model = Object.Instantiate(prefab, _pivot);
            _model.name = "무대_" + prefab.name;
            // <b>켜서 올린다.</b> 원본이 씬에서 꺼져 있는 물건일 수 있고(집기 전의 소품),
            // Instantiate 는 그 꺼진 상태를 그대로 물려준다 — 그러면 무대에 아무것도
            // 안 서는데 판은 멀쩡히 떠 있어서, 빈 판을 보며 원인을 찾게 된다.
            _model.SetActive(true);
            foreach (var c in _model.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var b in _model.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;

            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.Euler(euler);

            var b2 = WorldBounds(_model);
            _model.transform.localPosition -= _pivot.InverseTransformPoint(b2.center);
            float radius = Mathf.Max(0.02f, b2.extents.magnitude);
            _baseDistance = radius / Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f;

            _zoom = 1f;
            _pivot.localRotation = Quaternion.identity;
            _cam.enabled = true;
            ApplyCamera();
        }

        /// <summary>
        /// <b>그림의 가로세로 비와 바탕을 정한다</b> — <see cref="Show"/> 앞에 부른다.
        ///
        /// 물증은 정사각 그림에 투명한 바탕으로 찍어 판 위에 얹는다. 걷힌 팔은 다르다 —
        /// 그 그림이 <b>화면 전체</b>가 되어 뒤의 방을 덮어야 하므로, 화면과 같은 비로 찍고
        /// 바탕도 칠한다(<paramref name="background"/> 의 알파 1). 1 · 투명으로 부르면 평소로 돌아간다.
        /// </summary>
        public static void SetFrame(float aspect, Color background, int height = RtSize)
        {
            EnsureStage();
            aspect = Mathf.Clamp(aspect, 0.5f, 3f);
            height = Mathf.Clamp(height, 256, 4096);
            _cam.backgroundColor = background;
            int w = Mathf.RoundToInt(height * aspect);
            if (_rt != null && _rt.width == w && _rt.height == height) return;
            var old = _rt;
            _rt = new RenderTexture(w, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "물증_무대RT",
                antiAliasing = 1,   // MSAA 를 켜면 판에 청록색 한 장만 뜬다(위 참조)
            };
            _rt.Create();
            _cam.targetTexture = _rt;
            _cam.ResetAspect();
            if (old != null) { old.Release(); Object.Destroy(old); }
        }

        private static readonly System.Collections.Generic.List<Light> _lights = new System.Collections.Generic.List<Light>();
        private static readonly System.Collections.Generic.List<float> _lightBase = new System.Collections.Generic.List<float>();
        private static float _lightScale = 1f;

        /// <summary>
        /// 무대 조명의 세기를 몇 배로 할지. 흰 살갗(걷힌 팔)은 물증용 세기(키 6)에 하얗게 날아가
        /// <b>흉터가 안 보였다</b> — 판에 비벼 넣은 붉은 자국이 바탕과 같은 흰빛이 되어 버린다.
        /// </summary>
        public static void SetLightScale(float scale)
        {
            _lightScale = Mathf.Max(0f, scale);
            for (int i = 0; i < _lights.Count; i++)
                if (_lights[i] != null) _lights[i].intensity = _lightBase[i] * _lightScale;
        }

        // ── 무대 위의 물건을 짚는다 ──
        // 무대에 올린 물건은 콜라이더를 다 끈다(씬의 것과 섞이지 않게). 걷힌 팔처럼 <b>어디를 대느냐</b>가
        // 일인 물건은 무대에서만 쓰는 콜라이더를 따로 단다 — 무대 카메라로 쏜 선이 거기 맞는다.
        private static readonly System.Collections.Generic.List<MeshCollider> _pick = new System.Collections.Generic.List<MeshCollider>();

        /// <summary>올라 있는 물건을 짚을 수 있게 한다(<see cref="Show"/> 뒤에).</summary>
        public static void EnablePicking()
        {
            _pick.Clear();
            if (_model == null) return;
            foreach (var mf in _model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                _pick.Add(mc);
            }
            Physics.SyncTransforms();   // 방금 세운 물건의 자리를 물리에 곧바로 알린다(다음 물리 칸을 안 기다린다)
        }

        /// <summary>그림 위의 자리(0~1)로 물건을 짚는다. 맞은 자리·면의 결·그림 좌표가 <paramref name="hit"/> 에 온다.</summary>
        public static bool Pick(Vector2 viewport, out RaycastHit hit)
        {
            hit = default;
            if (_cam == null || _model == null || _pick.Count == 0) return false;
            var ray = _cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            bool any = false; float best = float.MaxValue;
            foreach (var mc in _pick)
            {
                if (mc == null || !mc.Raycast(ray, out var h, 50f) || h.distance >= best) continue;
                best = h.distance; hit = h; any = true;
            }
            return any;
        }

        /// <summary>물건 뿌리의 한 축이 지금 무대에서 가리키는 쪽.</summary>
        public static Vector3 ModelAxis(Vector3 local)
            => _model != null ? _model.transform.TransformDirection(local).normalized : local;

        /// <summary>물건의 크기(둘레 상자의 가장 긴 변). 띄울 높이 같은 것을 이 몫으로 잰다.</summary>
        public static float ModelSize
        {
            get
            {
                if (_model == null) return 1f;
                var b = WorldBounds(_model);
                return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            }
        }

        /// <summary>곁것(유척)을 무대 안의 자리로 옮긴다 — 손에 든 채 따라다니게.</summary>
        public static void PlaceProp(Vector3 worldPos, Quaternion worldRot)
        {
            if (_prop != null) _prop.transform.SetPositionAndRotation(worldPos, worldRot);
        }

        /// <summary>
        /// <b>무대 위 물건이 방을 비추지 않게 한다</b> — 살갗처럼 거울이 아닌 것에 쓴다.
        ///
        /// 무대는 씬에서 멀리 떨어져 있어도 씬의 <b>반사 프로브</b>(방을 찍어 둔 반사 그림)를 그대로 받는다.
        /// 걷힌 팔 재질은 매끈함이 0.5 라, 살갗 위에 동헌의 문살과 바닥 무늬가 어렴풋이 비쳤고 —
        /// 돋보기로 키우면 그것이 또렷해져 「뒤가 비친다」로 보였다(2026-10-17, 배경을 다 막아도 남아서 알았다).
        /// 물건마다 재질을 <b>제 벌로</b> 떠서(원본 자산은 안 건드린다) 주변 반사를 끄고 매끈함을 낮춘다.
        /// </summary>
        public static void Matte(float smoothness = 0.15f)
        {
            if (_model == null) return;
            foreach (var r in _model.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                var mats = r.materials;   // 이 무대 위의 사본 — 자산의 재질은 그대로다
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
                    m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Min(m.GetFloat("_Smoothness"), smoothness));
                    if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", Mathf.Min(m.GetFloat("_Glossiness"), smoothness));
                }
                r.materials = mats;
            }
        }

        /// <summary>바탕만 갈아 칠한다(그림 크기는 그대로).</summary>
        public static void SetBackground(Color background)
        {
            if (_cam != null && _cam.backgroundColor != background) _cam.backgroundColor = background;
        }

        /// <summary>지금 그림의 가로세로 비.</summary>
        public static float Aspect => _rt != null ? (float)_rt.width / Mathf.Max(1, _rt.height) : 1f;

        /// <summary>
        /// 물건의 <b>긴 쪽</b>이 그림 가로의 몇 할을 차지하게 다가선다.
        /// 걷힌 팔은 가로로 길다 — 둘레 공 하나로 잡으면 세로에 맞춰져 손끝이 잘리거나 너무 작다.
        /// </summary>
        public static void FitWidth(float share)
        {
            if (_cam == null || _model == null) return;
            var b = WorldBounds(_model);
            float len = Mathf.Max(b.size.x, b.size.y);
            float tanH = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Aspect;
            float d = len / Mathf.Max(0.05f, share) / (2f * Mathf.Max(0.01f, tanH));
            _zoom = Mathf.Clamp(d / Mathf.Max(0.01f, _baseDistance), 0.2f, 4f);
            ApplyCamera();
        }

        /// <summary>처음 크기를 정한다(1 = 물건 전체가 꽉 차게, 작을수록 다가선다).</summary>
        public static void SetZoom(float zoom)
        {
            _zoom = Mathf.Clamp(zoom, 0.35f, 2.5f);
            ApplyCamera();
        }

        public static void Spin(Vector2 delta)
        {
            if (_pivot == null) return;
            _pivot.rotation = Quaternion.AngleAxis(-delta.x, Vector3.up) * _pivot.rotation;
            _pivot.rotation = Quaternion.AngleAxis(delta.y, _cam.transform.right) * _pivot.rotation;
        }

        /// <summary>휠로 당겼다 물렸다. 너무 붙으면 앞 자름면에 잘리므로 가둔다.</summary>
        public static void Zoom(float wheel)
        {
            if (_cam == null) return;
            _zoom = Mathf.Clamp(_zoom * (1f - wheel * 0.1f), 0.35f, 2.5f);
            ApplyCamera();
        }

        private static void ApplyCamera()
        {
            if (_cam == null) return;
            float d = Mathf.Max(0.1f, _baseDistance * _zoom);
            _cam.transform.localPosition = _pivot.localPosition + new Vector3(0f, 0f, -d);
            _cam.transform.LookAt(_pivot.position);
        }

        /// <summary>무대를 비운다. 카메라도 끈다 — 안 보는 무대를 그릴 까닭이 없다.</summary>
        private static GameObject _prop;

        /// <summary>
        /// <b>물건 곁에 딸린 것을 올린다</b> — 팔 위에 놓는 유척처럼.
        /// 자리는 물건 프리팹의 뿌리 기준이라, 물건을 돌리면 함께 돈다.
        /// </summary>
        public static void ShowProp(GameObject prefab, Vector3 localPos, Quaternion localRot, Material mat = null)
        {
            ClearProp();
            if (prefab == null || _model == null) return;
            _prop = Object.Instantiate(prefab, _model.transform);
            _prop.name = "무대_곁_" + prefab.name;
            _prop.SetActive(true);
            foreach (var c in _prop.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var b in _prop.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
            if (mat != null) foreach (var r in _prop.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            _prop.transform.localPosition = localPos;
            _prop.transform.localRotation = localRot;
            // 뿌리의 배율을 물려받지 않게 — 한 자는 한 자다.
            var s = _model.transform.lossyScale;
            var own = prefab.transform.localScale;
            _prop.transform.localScale = new Vector3(own.x / Mathf.Max(0.0001f, s.x), own.y / Mathf.Max(0.0001f, s.y), own.z / Mathf.Max(0.0001f, s.z));
        }

        public static void ClearProp()
        {
            if (_prop != null) Object.Destroy(_prop);
            _prop = null;
        }

        public static void Clear()
        {
            _pick.Clear();
            ClearProp();
            if (_model != null) Object.Destroy(_model);
            _model = null;
            if (_cam != null) _cam.enabled = false;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
