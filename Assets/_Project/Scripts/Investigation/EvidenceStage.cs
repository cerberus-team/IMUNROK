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

            AddLight("키", new Vector3(-0.8f, 1.0f, -1.1f), new Color(1f, 0.96f, 0.88f), 6f);
            AddLight("필", new Vector3(1.1f, 0.2f, -0.7f), new Color(0.82f, 0.86f, 1f), 2.2f);
            AddLight("림", new Vector3(0.2f, 0.6f, 1.2f), new Color(1f, 0.88f, 0.72f), 3f);
        }

        private static void AddLight(string name, Vector3 at, Color color, float intensity)
        {
            var go = new GameObject("조명_" + name);
            go.transform.SetParent(_stage, false);
            go.transform.localPosition = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
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

        /// <summary>끌어서 돌린다. 가로는 세로축을 돌리고, 세로는 눈높이를 넘긴다.</summary>
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
        public static void Clear()
        {
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
