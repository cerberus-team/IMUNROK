using UnityEngine;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 상세 보기의 3D 무대 (2026-08-23). 아이템 모델을 렌더텍스처에 그려 UI 판에 얹는다.
    ///
    /// ■ 왜 렌더텍스처인가 — 씬 한복판에 모델을 띄우면 벽·가구에 파묻히고 씬 조명(밤이면 캄캄)에
    ///   먹힌다. 무대를 씬에서 **멀리 떼어** 두고 전용 카메라로만 찍으면 어느 씬에서 열어도
    ///   같은 그림이 나온다. UI 판에 붙는 이미지 한 장이라 VR 월드 캔버스에서도 그대로 보인다.
    ///
    /// ■ 왜 레이어를 안 쓰나 — 레이어 추가는 ProjectSettings/TagManager 변경이라 팀 전체에
    ///   퍼진다(금지). 대신 무대를 <see cref="StageOrigin"/>(5km 밖)에 두고 미리보기 카메라의
    ///   far를 20m로 줄였다 — 본 카메라의 far(최대 3000 수준)가 닿지 않으니 서로 안 보인다.
    ///
    /// ■ 조명 — 전용 **포인트라이트 3개**(키·필·림, range 3m). 디렉셔널을 쓰면 무대만이 아니라
    ///   씬 전체가 밝아진다. 씬의 태양·환경광이 무대에도 닿긴 하지만, 이 3개가 지배하도록
    ///   세기를 잡았다.
    /// </summary>
    public class InventoryPreview
    {
        /// <summary>씬에서 멀찍이 떨어진 무대 자리. 본 카메라 far(≤3000)보다 멀다.</summary>
        public static readonly Vector3 StageOrigin = new Vector3(5000f, 5000f, 5000f);

        public const int RtSize = 1024;   // 전체 화면 조사에서 크게 띄우므로 넉넉히 (2026-08-25)

        Transform stage;      // 무대 뿌리 (조명·카메라·모델의 부모)
        Transform pivot;      // 모델이 매달리는 회전 축 — 드래그가 이걸 돌린다
        Camera cam;
        RenderTexture rt;
        GameObject model;
        float baseDistance = 1f;   // 자동 프레이밍이 잡은 거리
        float zoom = 1f;           // 휠 배율 (작을수록 확대)

        public RenderTexture Texture => rt;
        public bool HasModel => model != null;
        /// <summary>지금 무대에 올라 있는 물건 — 상세→조사로 넘어갈 때 각도·배율을 이어 가려고 본다.</summary>
        public IUiItem ShowingItem { get; private set; }

        public void EnsureStage()
        {
            if (stage != null) return;

            var root = new GameObject("소지품_미리보기무대") { hideFlags = HideFlags.DontSave };
            root.transform.position = StageOrigin;
            stage = root.transform;
            Object.DontDestroyOnLoad(root);

            pivot = new GameObject("회전축").transform;
            pivot.SetParent(stage, false);

            // ⚠️ antiAliasing(MSAA)를 켜지 말 것 (2026-08-23 실측). MSAA 렌더텍스처는 그대로
            //    샘플링할 수 없어 RawImage에 **청록색 판**만 뜬다 — 원인 찾기 고약한 종류다.
            rt = new RenderTexture(RtSize, RtSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "소지품_미리보기RT",
                antiAliasing = 1,
            };
            rt.Create();

            var camGo = new GameObject("미리보기카메라");
            camGo.transform.SetParent(stage, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            // 알파 0 — 모델만 남고 뒤로 한지 바탕이 비친다
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 20f;
            cam.targetTexture = rt;
            // ⚠️ VR에서 이걸 빠뜨리면 XR이 이 카메라까지 **스테레오로** 그리려 든다 —
            //    미리보기는 판에 얹는 납작한 한 장이므로 양안이 필요 없다 (2026-08-26).
            //    ⚠️ 헤드셋으로 확인하지 못했다. 리그가 붙으면 미리보기가 제대로 나오는지 볼 것.
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            // ⚠️ URP에서는 Camera.Render()를 직접 부를 수 없다 (SRP 비지원 — 에러만 뜬다).
            //    상세 보기일 때만 카메라를 켜는 방식으로 비용을 아낀다.
            cam.enabled = false;
            var extra = camGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (extra == null) extra = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            extra.renderPostProcessing = false;   // 씬의 블룸·톤매핑이 미리보기까지 물들이지 않게
            extra.renderShadows = false;
            extra.requiresColorOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
            extra.requiresDepthOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;

            AddLight("키", new Vector3(-0.8f, 1.0f, -1.1f), new Color(1f, 0.96f, 0.88f), 6f);
            AddLight("필", new Vector3(1.1f, 0.2f, -0.7f), new Color(0.82f, 0.86f, 1f), 2.2f);
            AddLight("림", new Vector3(0.2f, 0.6f, 1.2f), new Color(1f, 0.88f, 0.72f), 3f);
        }

        void AddLight(string name, Vector3 localPos, Color color, float intensity)
        {
            var go = new GameObject("조명_" + name);
            go.transform.SetParent(stage, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = 4f;
            l.shadows = LightShadows.None;
        }

        /// <summary>모델을 무대에 올리고 화면에 꽉 차게 자동 프레이밍한다.</summary>
        public void Show(IUiItem item)
        {
            EnsureStage();
            // 자동 프레이밍이 Renderer.bounds를 읽는다 — 무대가 꺼져 있으면 0이 나온다. 먼저 켠다.
            stage.gameObject.SetActive(true);
            Clear();
            if (item == null || item.ModelPrefab == null) return;

            ShowingItem = item;
            model = Object.Instantiate(item.ModelPrefab, pivot);
            model.name = "미리보기_" + item.DisplayName;
            foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var b in model.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;

            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(item.PreviewEuler);

            // 자동 프레이밍: 모델 바운즈 중심을 회전축 원점으로 끌어오고, 반지름에서 거리를 낸다.
            var b2 = WorldBounds(model);
            model.transform.localPosition -= pivot.InverseTransformPoint(b2.center);
            float radius = Mathf.Max(0.02f, b2.extents.magnitude);
            baseDistance = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f;

            zoom = 1f / Mathf.Max(0.1f, item.PreviewZoom);
            pivot.localRotation = Quaternion.identity;
            ApplyCamera();
        }

        public void Clear()
        {
            if (model != null) Object.DestroyImmediate(model);
            model = null;
            ShowingItem = null;
        }

        /// <summary>드래그 회전 — 좌우는 월드 Y축(늘 같은 방향으로 돈다), 상하는 카메라 옆축.</summary>
        public void Rotate(Vector2 pixelDelta, float sensitivity = 0.35f)
        {
            if (pivot == null) return;
            pivot.rotation = Quaternion.AngleAxis(-pixelDelta.x * sensitivity, Vector3.up) * pivot.rotation;
            pivot.rotation = Quaternion.AngleAxis(pixelDelta.y * sensitivity, cam.transform.right) * pivot.rotation;
        }

        /// <summary>휠 확대·축소 (부호 = 방향).</summary>
        public void Zoom(float direction)
        {
            zoom = Mathf.Clamp(zoom * (direction > 0f ? 0.88f : 1f / 0.88f), 0.35f, 2.5f);
            ApplyCamera();
        }

        public void ResetView()
        {
            if (pivot == null) return;
            pivot.localRotation = Quaternion.identity;
            zoom = 1f;
            ApplyCamera();
        }

        void ApplyCamera()
        {
            if (cam == null) return;
            cam.transform.localPosition = new Vector3(0f, 0f, -baseDistance * zoom);
            cam.transform.localRotation = Quaternion.identity;
        }

        /// <summary>무대를 돌린다·세운다 — 상세 보기일 때만 켠다 (목록·닫힘에서는 비용 0).</summary>
        public void SetRendering(bool on)
        {
            if (stage == null) return;
            stage.gameObject.SetActive(on);
            if (cam != null) cam.enabled = on && model != null;
        }

        // ── 목록 칸에 붙일 작은 그림 ──────────────────────────
        public const int ThumbSize = 256;
        readonly System.Collections.Generic.Dictionary<IUiItem, Texture2D> thumbs =
            new System.Collections.Generic.Dictionary<IUiItem, Texture2D>();

        /// <summary>목록 칸용 썸네일. 같은 무대에서 한 장 찍어 두고 재사용한다.
        /// URP는 Camera.Render()를 막으므로 <c>SubmitRenderRequest</c>로 그 자리에서 한 프레임 뽑는다 —
        /// 카메라를 켜 두고 다음 프레임을 기다리는 방식이면 목록이 뜨는 첫 프레임에 칸이 비어 보인다.</summary>
        public Texture2D Thumbnail(IUiItem item)
        {
            if (item == null || item.ModelPrefab == null) return null;
            if (thumbs.TryGetValue(item, out var cached) && cached != null) return cached;

            EnsureStage();
            bool wasActive = stage.gameObject.activeSelf;
            bool wasCam = cam.enabled;

            // 상세 보기 중이면 그 모델을 잠시 꺼 둔다 — 켜 둔 채 찍으면 두 물건이 겹쳐 나온다
            var keepModel = model;
            var keepRot = pivot.localRotation;
            float keepZoom = zoom, keepDist = baseDistance;
            if (keepModel != null) keepModel.SetActive(false);
            model = null;

            Show(item);
            stage.gameObject.SetActive(true);

            var tex = new Texture2D(ThumbSize, ThumbSize, TextureFormat.RGBA32, false)
            {
                name = "소지품_그림_" + item.Key,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var rt2 = RenderTexture.GetTemporary(ThumbSize, ThumbSize, 24, RenderTextureFormat.ARGB32);
            var req = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt2 };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req))
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
                var prev = RenderTexture.active;
                RenderTexture.active = rt2;
                tex.ReadPixels(new Rect(0, 0, ThumbSize, ThumbSize), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
            }
            RenderTexture.ReleaseTemporary(rt2);

            // 무대를 원래 쓰던 상태로 돌려놓는다
            Clear();
            model = keepModel;
            if (model != null)
            {
                model.SetActive(true);
                baseDistance = keepDist; zoom = keepZoom; pivot.localRotation = keepRot;
                ApplyCamera();
            }
            stage.gameObject.SetActive(wasActive);
            cam.enabled = wasCam;

            thumbs[item] = tex;
            return tex;
        }

        public void Dispose()
        {
            foreach (var kv in thumbs) if (kv.Value != null) Object.Destroy(kv.Value);
            thumbs.Clear();
            Clear();
            if (stage != null) Object.Destroy(stage.gameObject);
            stage = null;
            if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
    }
}
