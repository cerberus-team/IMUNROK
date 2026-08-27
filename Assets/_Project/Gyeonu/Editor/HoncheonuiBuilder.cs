using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 혼천의 절차 생성 (2026-08-09, 멱등) — 관측실 별자리 퍼즐의 핵심 도구.
    ///
    /// 고리(환)는 납작한 띠(band) 단면 토러스 + 눈금(소/대)·장식 스터드를 한 메시로 합성.
    /// 각 환은 개별 GameObject이고 자기 로컬 Y축이 회전축 —
    /// tr.Rotate(0, deg, 0, Space.Self)로 제자리 회전한다 (퍼즐 요구).
    /// 계층: 지평환 → 자오환 → 적도환 → 황도환 → 내환부(소형환 3, 중심축, 중심구).
    /// 부모 환을 돌리면 안쪽이 전부 따라 돈다.
    ///
    /// 극축 기울기 37.5°(조선 위도), 황도환은 +23.5° 추가 경사.
    /// 받침 2종 비교였으나 B(장식받침) 채택 확정 (2026-08-09). A 프리팹은 보관만.
    /// 메시·머티리얼 에셋은 경로 고정 덮어쓰기라 GUID가 유지된다 (재실행 안전).
    /// 씬에 이미 있는 인스턴스는 위치를 보존하고 재생성한다 (사용자 배치 존중).
    ///
    /// 금속 광택 (2026-08-09): 황동 Smoothness 0.85 + 절차적 브러시 노멀맵.
    /// 금속은 반사 대상이 있어야 빛난다 — 실제 관측실(어두운 실내)을 흉내낸
    /// [혼천의 프리뷰 환경] 메뉴로 방·스포트라이트·커스텀 큐브맵 프로브를 세팅.
    /// </summary>
    public static class HoncheonuiBuilder
    {
        const string ModelDir = "Assets/_Project/Gyeonu/Art/Models/Observatory";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Observatory";
        const string PrefabDir = "Assets/_Project/Gyeonu/Prefabs/Observatory";
        const string TexDir = "Assets/_Project/Gyeonu/Art/Textures/Observatory";
        const string EnvRootName = "혼천의_프리뷰환경";
        const float CenterY = 1.15f;      // 구 중심 높이 — 전체 높이 약 1.77m
        const float PolarTilt = 52.5f;    // 90 - 위도 37.5

        [MenuItem("Tools/이문록/혼천의 생성")]
        public static void Build()
        {
            // 기존 인스턴스 위치 보존 후 재생성 (비활성 포함 탐색)
            var prevPos = new Dictionary<string, Vector3>();
            var prevActive = new Dictionary<string, bool>();
            foreach (var n in new[] { "혼천의_A_십자받침", "혼천의_B_장식받침" })
                for (GameObject g; (g = FindRootIncludingInactive(n)) != null;)
                {
                    prevPos[n] = g.transform.position;
                    prevActive[n] = g.activeSelf;
                    Object.DestroyImmediate(g);
                }

            EnsureFolder(ModelDir); EnsureFolder(MatDir); EnsureFolder(PrefabDir); EnsureFolder(TexDir);

            var normal = BuildBrushedNormal();
            // Metallic 1.0은 어두운 실내에서 순거울이 되어 얼룩짐 — 0.92로 디퓨즈를 약간 남긴다
            var brass = Mat("M_혼천의_황동", new Color(1.00f, 0.78f, 0.40f), 0.92f, 0.85f, normal, 0.25f);
            // 받침은 주물 느낌의 새틴 — 거울 반사 얼룩 방지. 노멀맵은 넣지 않는다:
            // 받침은 빌트인 프리미티브 합성이라 UV가 면당 0..1로 늘어나 요철이 대형 얼룩이 된다
            var brassSatin = Mat("M_혼천의_황동무광", new Color(0.96f, 0.72f, 0.36f), 0.72f, 0.52f);
            var bronze = Mat("M_혼천의_청동", new Color(0.62f, 0.44f, 0.24f), 0.90f, 0.72f, normal, 0.35f);
            var iron = Mat("M_혼천의_흑철", new Color(0.10f, 0.10f, 0.11f), 0.75f, 0.45f);
            var globe = Mat("M_혼천의_천구", new Color(0.38f, 0.20f, 0.13f), 0.35f, 0.65f);

            var p = new Parts
            {
                jip = SaveMesh(BuildRing(0.55f, 0.055f, 0.020f, 96, 72, 12, 4, false), "혼천의_지평환"),
                jao = SaveMesh(BuildRing(0.52f, 0.050f, 0.020f, 96, 72, 12, 0, true), "혼천의_자오환"),
                jeokdo = SaveMesh(BuildRing(0.46f, 0.045f, 0.018f, 80, 48, 12, 4, false), "혼천의_적도환"),
                hwangdo = SaveMesh(BuildRing(0.42f, 0.042f, 0.016f, 80, 48, 12, 0, false), "혼천의_황도환"),
                so1 = SaveMesh(BuildRing(0.30f, 0.035f, 0.014f, 64, 36, 12, 0, false), "혼천의_소형환1"),
                so2 = SaveMesh(BuildRing(0.28f, 0.032f, 0.012f, 64, 36, 12, 0, false), "혼천의_소형환2"),
                so3 = SaveMesh(BuildRing(0.26f, 0.030f, 0.012f, 64, 24, 8, 0, false), "혼천의_소형환3"),
                brass = brass, bronze = bronze, globe = globe,
            };
            var baseA = SaveMesh(BuildBaseCross(), "혼천의_받침십자");
            var baseB = SaveMesh(BuildBaseOrnate(), "혼천의_받침장식");

            var a = BuildVariant("혼천의_A_십자받침", baseA, iron, p);
            PrefabUtility.SaveAsPrefabAssetAndConnect(a, PrefabDir + "/혼천의_A_십자받침.prefab", InteractionMode.AutomatedAction);
            var b = BuildVariant("혼천의_B_장식받침", baseB, brassSatin, p);
            PrefabUtility.SaveAsPrefabAssetAndConnect(b, PrefabDir + "/혼천의_B_장식받침.prefab", InteractionMode.AutomatedAction);

            a.transform.position = prevPos.TryGetValue(a.name, out var pa) ? pa : Vector3.zero;
            b.transform.position = prevPos.TryGetValue(b.name, out var pb) ? pb : new Vector3(6f, 0f, 0f);
            if (prevActive.TryGetValue(a.name, out var aa)) a.SetActive(aa);
            if (prevActive.TryGetValue(b.name, out var ab)) b.SetActive(ab);

            // 관측실처럼 프리팹 인스턴스가 다른 루트(관측실) 아래에 있는 씬에서는
            // 위 루트 인스턴스가 잔여물이 된다 (2026-08-14 실측 — 암문 앞에 혼천의 유령 2기).
            // 프리팹 저장만으로 기존 자식 인스턴스에 전파되므로 루트 사본은 지운다
            if (GameObject.Find("관측실/혼천의_B_장식받침") != null)
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Debug.Log("[혼천의] 관측실 자식 인스턴스 감지 — 루트 사본은 만들지 않고 프리팹 전파로 갱신");
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[혼천의] 생성 완료 — A(십자받침) {CountTris(a):n0}tri / B(장식받침) {CountTris(b):n0}tri, 프리팹 2종 저장");
        }

        // ── 프리뷰 환경: 어두운 관측실 흉내 (샌드박스 씬 전용) ──

        [MenuItem("Tools/이문록/혼천의 프리뷰 환경 (어두운 실내)")]
        public static void BuildPreviewEnv()
        {
            var b = FindRootIncludingInactive("혼천의_B_장식받침");
            if (b == null) { Debug.LogError("[혼천의] B 인스턴스가 씬에 없습니다 — 먼저 '혼천의 생성' 실행"); return; }
            b.SetActive(true);
            var a = FindRootIncludingInactive("혼천의_A_십자받침");
            if (a != null) a.SetActive(false);
            var ground = FindRootIncludingInactive("Ground_Preview");
            if (ground != null) ground.SetActive(false);

            for (GameObject g; (g = FindRootIncludingInactive(EnvRootName)) != null;)
                Object.DestroyImmediate(g);

            Vector3 c = b.transform.position;
            var root = new GameObject(EnvRootName);
            root.transform.position = c;
            var roomMat = Mat("M_혼천의_프리뷰실내", new Color(0.13f, 0.11f, 0.09f), 0f, 0.30f);

            void Wall(string n, Vector3 localPos, Vector3 size)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = n;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = localPos;
                go.transform.localScale = size;
                go.GetComponent<MeshRenderer>().sharedMaterial = roomMat;
            }
            Wall("바닥", new Vector3(0, -0.05f, 0), new Vector3(8, 0.1f, 8));
            Wall("천장", new Vector3(0, 4.25f, 0), new Vector3(8, 0.1f, 8));
            Wall("벽_북", new Vector3(0, 2.1f, 4.05f), new Vector3(8, 4.4f, 0.1f));
            Wall("벽_남", new Vector3(0, 2.1f, -4.05f), new Vector3(8, 4.4f, 0.1f));
            Wall("벽_동", new Vector3(4.05f, 2.1f, 0), new Vector3(0.1f, 4.4f, 8));
            Wall("벽_서", new Vector3(-4.05f, 2.1f, 0), new Vector3(0.1f, 4.4f, 8));

            void Lamp(string n, LightType type, Vector3 localPos, Color col, float intensity, float range, float angle)
            {
                var go = new GameObject(n);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = localPos;
                var l = go.AddComponent<Light>();
                l.type = type; l.color = col; l.intensity = intensity; l.range = range;
                if (type == LightType.Spot)
                {
                    go.transform.LookAt(c + new Vector3(0, 1.15f, 0));
                    l.spotAngle = angle;
                    l.innerSpotAngle = angle * 0.5f;
                    l.shadows = LightShadows.Soft;
                    l.shadowStrength = 0.55f; // 고리 그림자가 받침에 흑백 얼룩을 만들지 않게 완화
                }
            }
            Lamp("조명_키", LightType.Spot, new Vector3(1.6f, 3.1f, -1.7f), new Color(1f, 0.92f, 0.78f), 20f, 12f, 55f);
            Lamp("조명_필", LightType.Spot, new Vector3(-2.0f, 2.4f, 1.6f), new Color(0.62f, 0.70f, 0.95f), 10f, 12f, 60f);
            Lamp("조명_림", LightType.Point, new Vector3(0.2f, 2.3f, 2.1f), new Color(1f, 0.85f, 0.60f), 7f, 8f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.080f, 0.075f, 0.090f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            // 방+조명을 큐브맵으로 구워 커스텀 프로브에 — 금속이 반사할 '무언가'.
            // 굽는 동안 스테일 큐브맵 참조가 URP 프로브 매니저 NRE를 유발하므로 먼저 끊는다.
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.customReflectionTexture = null;
            var rootRef = root.transform;
            StartCubemapBake(c + new Vector3(0, 1.15f, 0), cube =>
            {
                if (cube == null || rootRef == null) return;
                var probeGo = new GameObject("리플렉션프로브");
                probeGo.transform.SetParent(rootRef, false);
                probeGo.transform.localPosition = new Vector3(0, 1.6f, 0);
                var probe = probeGo.AddComponent<ReflectionProbe>();
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
                probe.customBakedTexture = cube;
                probe.size = new Vector3(8f, 4.4f, 8f);
                probe.boxProjection = true;
                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = cube;
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log("[혼천의] 프리뷰 환경 구성 완료 (어두운 실내 + 3점 조명 + 커스텀 프로브)");
            });

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[혼천의] 프리뷰 환경 방·조명 구성 — 큐브맵 굽는 중 (수 프레임 뒤 완료 로그)");
        }

        [MenuItem("Tools/이문록/혼천의 프리뷰 환경 제거")]
        public static void RemovePreviewEnv()
        {
            for (GameObject g; (g = FindRootIncludingInactive(EnvRootName)) != null;)
                Object.DestroyImmediate(g);
            var ground = FindRootIncludingInactive("Ground_Preview");
            if (ground != null) ground.SetActive(true);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.customReflectionTexture = null;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[혼천의] 프리뷰 환경 제거 — 기본 하늘 복귀");
        }

        // Camera.RenderToCubemap 및 한 프레임 내 다중 Camera.Render()는 URP 렌더그래프의
        // ZBinning 잡과 충돌(2026-08-09 확인) — delayCall로 프레임당 1면씩 나눠 굽는다.
        static void StartCubemapBake(Vector3 pos, System.Action<Cubemap> onDone)
        {
            const int res = 256;
            string path = TexDir + "/CM_혼천의_프리뷰실내.asset";
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (cube == null || cube.width != res)
            {
                cube = new Cubemap(res, TextureFormat.RGBA32, true);
                AssetDatabase.CreateAsset(cube, path);
                cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            }

            var rt = new RenderTexture(res, res, 16, RenderTextureFormat.ARGB32);
            var face = new Texture2D(res, res, TextureFormat.RGBA32, false);
            var camGo = new GameObject("__HonBakeCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.transform.position = pos;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.farClipPlane = 30f;
            cam.fieldOfView = 90f;
            cam.aspect = 1f;
            cam.targetTexture = rt;

            var faces = new (CubemapFace f, Vector3 euler)[]
            {
                (CubemapFace.PositiveX, new Vector3(0, 90, 0)),
                (CubemapFace.NegativeX, new Vector3(0, -90, 0)),
                (CubemapFace.PositiveY, new Vector3(-90, 0, 0)),
                (CubemapFace.NegativeY, new Vector3(90, 0, 0)),
                (CubemapFace.PositiveZ, new Vector3(0, 0, 0)),
                (CubemapFace.NegativeZ, new Vector3(0, 180, 0)),
            };
            var flipped = new Color[res * res];
            int i = 0;

            void Cleanup()
            {
                if (cam != null) cam.targetTexture = null;
                if (camGo != null) Object.DestroyImmediate(camGo);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (face != null) Object.DestroyImmediate(face);
            }

            void Step()
            {
                if (camGo == null || cube == null) { Cleanup(); onDone(null); return; }
                cam.transform.rotation = Quaternion.Euler(faces[i].euler);
                cam.Render();
                RenderTexture.active = rt;
                face.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                RenderTexture.active = null;
                var px = face.GetPixels();
                for (int y = 0; y < res; y++)          // 상하 반전 (큐브맵 면 원점 차이)
                    System.Array.Copy(px, y * res, flipped, (res - 1 - y) * res, res);
                cube.SetPixels(flipped, faces[i].f);
                i++;
                if (i < faces.Length) { EditorApplication.delayCall += Step; return; }
                cube.Apply(true);
                EditorUtility.SetDirty(cube);
                AssetDatabase.SaveAssets();
                Cleanup();
                onDone(cube);
            }
            EditorApplication.delayCall += Step;
        }

        class Parts
        {
            public Mesh jip, jao, jeokdo, hwangdo, so1, so2, so3;
            public Material brass, bronze, globe;
        }

        // ── 조립 ─────────────────────────────────────────────

        static GameObject BuildVariant(string name, Mesh baseMesh, Material baseMat, Parts p)
        {
            var root = new GameObject(name);
            MeshGO("받침", baseMesh, baseMat, root.transform, Vector3.zero, Quaternion.identity);

            // 축 규약 (2026-08-14 사용자 확정): 지평환 고정 / 자오환 수직축 / 적도환 극축 / 황도환 황도축.
            // 적도환(52.5°)·황도환(76°)은 GO 로컬 Y가 이미 그 축이라 그대로 두고,
            // 자오환만 로컬 Y가 수평(+Z)이라 **수직축 피벗(자오환_축)**을 사이에 세운다 —
            // 조작은 피벗을 돌리고, 적도환 이하가 그 자식이라 실물처럼 함께 딸려 돈다
            var jip = MeshGO("지평환", p.jip, p.brass, root.transform, new Vector3(0, CenterY, 0), Quaternion.identity);
            var jaoAxis = new GameObject("자오환_축");
            jaoAxis.transform.SetParent(jip.transform, false);
            var jao = MeshGO("자오환", p.jao, p.brass, jaoAxis.transform, Vector3.zero, Quaternion.Euler(90, 0, 0));
            var jeok = MeshGO("적도환", p.jeokdo, p.brass, jaoAxis.transform, Vector3.zero, Quaternion.Euler(PolarTilt, 0, 0));
            var hwang = MeshGO("황도환", p.hwangdo, p.bronze, jeok.transform, Vector3.zero, Quaternion.Euler(PolarTilt + 23.5f, 0, 0));

            var inner = new GameObject("내환부");
            inner.transform.SetParent(hwang.transform, false);
            inner.transform.rotation = Quaternion.Euler(PolarTilt, 0, 0);

            var so1 = MeshGO("소형환_1", p.so1, p.bronze, inner.transform, Vector3.zero, inner.transform.rotation);
            var so2 = MeshGO("소형환_2", p.so2, p.bronze, inner.transform, Vector3.zero, inner.transform.rotation * Quaternion.Euler(90, 0, 0));
            var so3 = MeshGO("소형환_3", p.so3, p.bronze, inner.transform, Vector3.zero, inner.transform.rotation * Quaternion.Euler(90, 0, 90));

            Prim("중심축", PrimitiveType.Cylinder, inner.transform, Vector3.zero, new Vector3(0.024f, 0.60f, 0.024f), p.bronze);
            Prim("축단추_상", PrimitiveType.Sphere, inner.transform, new Vector3(0, 0.60f, 0), Vector3.one * 0.05f, p.brass);
            Prim("축단추_하", PrimitiveType.Sphere, inner.transform, new Vector3(0, -0.60f, 0), Vector3.one * 0.05f, p.brass);
            Prim("중심구", PrimitiveType.Sphere, inner.transform, Vector3.zero, Vector3.one * 0.15f, p.globe);

            // 포커스 조작 (2026-08-14) — 고리 7개 휠 선택 + 드래그 회전. 퍼즐 판정은 별도.
            // 조작용 콜라이더가 있어야 레이캐스트가 잡는다 (보행 차단 박스는 Ignore Raycast 레이어)
            var grab = root.AddComponent<SphereCollider>();
            grab.center = new Vector3(0, CenterY, 0);
            grab.radius = 0.72f;
            var focus = root.AddComponent<IMUNROK.Gyeonu.HoncheonuiFocusRings>();
            focus.displayName = "혼천의";
            // 2.05면 관측실 배치(동벽 선반 1.66m·섬 탁자 1.58m)에서 카메라가 가구 속에 파묻힌다 (실측)
            focus.focusDistance = 1.5f;
            focus.highlight = new Color(0.45f, 0.28f, 0.12f);   // 1.7이면 블룸 과노출 (실측)
            focus.focusAnchor = jip.transform;   // 고리 중심 높이 (위치만 쓰므로 회전과 무관)
            // 지평환은 고정 부재라 조작 대상에서 제외. 자오환은 수직축 피벗을 돌린다
            focus.rings = new Transform[] { jaoAxis.transform, jeok.transform, hwang.transform,
                                            so1.transform, so2.transform, so3.transform };
            focus.ringRenderers = new Renderer[] { jao.GetComponent<Renderer>(), jeok.GetComponent<Renderer>(),
                                                   hwang.GetComponent<Renderer>(), so1.GetComponent<Renderer>(),
                                                   so2.GetComponent<Renderer>(), so3.GetComponent<Renderer>() };
            return root;
        }

        internal static GameObject MeshGO(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos, Quaternion worldRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.rotation = worldRot;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Prim(string name, PrimitiveType t, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ── 고리 메시: 띠 + 눈금 + 장식 합성 ────────────────

        internal static Mesh BuildRing(float R, float w, float t, int seg, int minor, int major, int bosses, bool finial)
        {
            var c = new List<CombineInstance>();
            var band = BuildBand(R, w, t, seg);
            Add(c, band, Vector3.zero, Quaternion.identity, Vector3.one);

            Mesh cube = PrimitiveMesh(PrimitiveType.Cube);
            Mesh sph = PrimitiveMesh(PrimitiveType.Sphere);
            float rO = R + t * 0.5f;

            for (int i = 0; i < minor; i++)
            {
                if (major > 0 && i * major % minor == 0) continue; // 대눈금과 겹치는 자리
                var d = DirDeg((float)i / minor * 360f);
                Add(c, cube, d * (rO + 0.003f), Quaternion.LookRotation(d), new Vector3(0.010f, w * 0.55f, 0.010f));
            }
            for (int i = 0; i < major; i++)
            {
                var d = DirDeg((float)i / major * 360f);
                Add(c, cube, d * (rO + 0.004f), Quaternion.LookRotation(d), new Vector3(0.016f, w * 0.85f, 0.014f));
            }
            for (int i = 0; i < bosses; i++)
            {
                var d = DirDeg(i * 90f + 45f);
                float bd = Mathf.Lerp(0.030f, 0.048f, R / 0.55f);
                Add(c, sph, d * (rO + 0.006f), Quaternion.identity, Vector3.one * bd);
            }
            if (finial) // 자오환 정수리 장식 — 로컬 -Z가 GO 회전(90,0,0) 후 월드 +Y
            {
                Add(c, sph, new Vector3(0, 0, -(rO + 0.028f)), Quaternion.identity, Vector3.one * 0.060f);
                Add(c, sph, new Vector3(0, 0, -(rO + 0.068f)), Quaternion.identity, Vector3.one * 0.034f);
            }

            var m = Combine(c);
            Object.DestroyImmediate(band);
            return m;
        }

        /// <summary>납작한 띠 단면 고리 (Y축 중심, 겉·안·위·아래 4면 하드엣지).</summary>
        internal static Mesh BuildBand(float radius, float width, float thick, int seg)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>();
            var uv = new List<Vector2>(); var tr = new List<int>();
            float rO = radius + thick * 0.5f, rI = radius - thick * 0.5f, hw = width * 0.5f;

            void Strip(float r0, float y0, float r1, float y1, float nr, float ny)
            {
                int start = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = (float)i / seg * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var nrm = (d * nr + Vector3.up * ny).normalized;
                    v.Add(d * r0 + Vector3.up * y0); n.Add(nrm); uv.Add(new Vector2((float)i / seg * 8f, 0));
                    v.Add(d * r1 + Vector3.up * y1); n.Add(nrm); uv.Add(new Vector2((float)i / seg * 8f, 1));
                }
                for (int i = 0; i < seg; i++)
                {
                    int b = start + i * 2;
                    tr.Add(b); tr.Add(b + 1); tr.Add(b + 2);
                    tr.Add(b + 2); tr.Add(b + 1); tr.Add(b + 3);
                }
            }
            Strip(rO, -hw, rO, hw, 1, 0);   // 겉면
            Strip(rI, hw, rI, -hw, -1, 0);  // 안면
            Strip(rO, hw, rI, hw, 0, 1);    // 윗면
            Strip(rI, -hw, rO, -hw, 0, -1); // 아랫면

            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            return m;
        }

        /// <summary>원형 단면 토러스 (Y축 중심). arcDeg &lt; 360이면 부분 호 — 받침 다리용.</summary>
        internal static Mesh BuildTorus(float R, float r, int segMajor, int segTube, float arcDeg = 360f)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>();
            var uv = new List<Vector2>(); var tr = new List<int>();
            float arc = arcDeg * Mathf.Deg2Rad;
            for (int i = 0; i <= segMajor; i++)
            {
                float u = (float)i / segMajor * arc;
                var d = new Vector3(Mathf.Cos(u), 0, Mathf.Sin(u));
                for (int j = 0; j <= segTube; j++)
                {
                    float w = (float)j / segTube * Mathf.PI * 2f;
                    var nrm = d * Mathf.Cos(w) + Vector3.up * Mathf.Sin(w);
                    v.Add(d * R + nrm * r); n.Add(nrm);
                    uv.Add(new Vector2((float)i / segMajor, (float)j / segTube));
                }
            }
            for (int i = 0; i < segMajor; i++)
                for (int j = 0; j < segTube; j++)
                {
                    int a = i * (segTube + 1) + j, b = a + segTube + 1;
                    tr.Add(a); tr.Add(a + 1); tr.Add(b);
                    tr.Add(b); tr.Add(a + 1); tr.Add(b + 1);
                }
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            return m;
        }

        // ── 받침 2종 ─────────────────────────────────────────

        /// <summary>A안: 십자 다리 + 기둥 4주 + 사선 버팀목 (흑철).</summary>
        static Mesh BuildBaseCross()
        {
            var c = new List<CombineInstance>();
            Mesh cube = PrimitiveMesh(PrimitiveType.Cube);
            Mesh cyl = PrimitiveMesh(PrimitiveType.Cylinder);

            Add(c, cube, new Vector3(0, 0.045f, 0), Quaternion.identity, new Vector3(1.5f, 0.09f, 0.14f));
            Add(c, cube, new Vector3(0, 0.045f, 0), Quaternion.Euler(0, 90, 0), new Vector3(1.5f, 0.09f, 0.14f));
            Add(c, cyl, new Vector3(0, 0.10f, 0), Quaternion.identity, new Vector3(0.24f, 0.05f, 0.24f));
            for (int k = 0; k < 4; k++)
            {
                var d = Quaternion.Euler(0, 90 * k, 0) * Vector3.right;
                AddDiag(c, cube, d * 0.55f + Vector3.up * 0.03f, d * 0.55f + Vector3.up * 1.135f, 0.075f);
                AddDiag(c, cube, d * 0.08f + Vector3.up * 0.10f, d * 0.53f + Vector3.up * 1.02f, 0.05f);
            }
            return Combine(c);
        }

        /// <summary>B안: 바닥 원형 좌대 + 발루스터 기둥 + 멍에 (황동).
        /// 3발 스크롤 다리·발바닥은 2026-08-09 단순화로 삭제 — 원형 받침을 바닥까지 내리고
        /// 지름 0.26 → 0.40으로 키워 안정감 확보. 발루스터(0.27 위)는 그대로.</summary>
        static Mesh BuildBaseOrnate()
        {
            var c = new List<CombineInstance>();
            Mesh cube = PrimitiveMesh(PrimitiveType.Cube);
            Mesh cyl = PrimitiveMesh(PrimitiveType.Cylinder);
            Mesh sph = PrimitiveMesh(PrimitiveType.Sphere);

            Add(c, cyl, new Vector3(0, 0.025f, 0), Quaternion.identity, new Vector3(0.40f, 0.025f, 0.40f)); // 좌대 하단
            Add(c, cyl, new Vector3(0, 0.065f, 0), Quaternion.identity, new Vector3(0.30f, 0.020f, 0.30f)); // 좌대 상단
            Add(c, cyl, new Vector3(0, 0.18f, 0), Quaternion.identity, new Vector3(0.07f, 0.10f, 0.07f));   // 기둥 하단 샤프트
            Add(c, sph, new Vector3(0, 0.27f, 0), Quaternion.identity, Vector3.one * 0.11f);
            Add(c, cyl, new Vector3(0, 0.38f, 0), Quaternion.identity, new Vector3(0.07f, 0.10f, 0.07f));
            Add(c, sph, new Vector3(0, 0.50f, 0), Quaternion.identity, Vector3.one * 0.10f);
            Add(c, cyl, new Vector3(0, 0.565f, 0), Quaternion.identity, new Vector3(0.06f, 0.035f, 0.06f));
            Add(c, cyl, new Vector3(0, 0.61f, 0), Quaternion.identity, new Vector3(0.10f, 0.018f, 0.10f));
            Add(c, cube, new Vector3(0, 0.645f, 0), Quaternion.identity, new Vector3(0.05f, 0.07f, 0.10f)); // 자오환 멍에

            return Combine(c);
        }

        // ── 공용 헬퍼 ────────────────────────────────────────

        internal static Vector3 DirDeg(float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        }

        internal static void Add(List<CombineInstance> list, Mesh m, Vector3 pos, Quaternion rot, Vector3 scale)
            => list.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, rot, scale) });

        internal static void AddDiag(List<CombineInstance> list, Mesh cube, Vector3 from, Vector3 to, float thick)
        {
            var dir = to - from;
            Add(list, cube, (from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, dir.normalized),
                new Vector3(thick, dir.magnitude, thick));
        }

        internal static Mesh Combine(List<CombineInstance> list)
        {
            var m = new Mesh();
            m.CombineMeshes(list.ToArray(), true, true);
            m.RecalculateNormals();
            // ⚠️ 필수 (2026-08-14 실측): BuildBand는 탄젠트가 없어 CombineMeshes가 0으로 채운다.
            //    제로 탄젠트 + 노멀맵(황동 브러시드) = 플레이 모드에서 스펙큘러가 NaN으로 터져
            //    혼천의가 화면을 덮는 흰 발광구가 된다 (작업실 '번쩍임'의 정체)
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        internal static Mesh PrimitiveMesh(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh; // 빌트인 메시 — 파괴 금지
            Object.DestroyImmediate(go);
            return m;
        }

        internal static Mesh SaveMesh(Mesh built, string name)
        {
            string path = $"{ModelDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                built.name = name;
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            existing.Clear();
            existing.indexFormat = built.indexFormat;
            existing.vertices = built.vertices;
            existing.normals = built.normals;
            existing.tangents = built.tangents;   // 노멀맵 필수 — 빠뜨리면 탄젠트 0으로 저장된다 (혼상에서 실측한 함정)
            existing.uv = built.uv;
            existing.triangles = built.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        internal static Material Mat(string name, Color c, float metallic, float smooth,
            Texture2D normal = null, float bumpScale = 1f)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", bumpScale);
            if (normal != null) m.EnableKeyword("_NORMALMAP");
            else m.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static GameObject FindRootIncludingInactive(string name)
        {
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.name == name) return g;
            return null;
        }

        /// <summary>브러시드 금속 노멀맵 — u방향(둘레) 결, 약한 두들김 요철. 512², PNG로 임포트.</summary>
        static Texture2D BuildBrushedNormal()
        {
            const int res = 512;
            string path = TexDir + "/T_혼천의_금속노멀.png";

            var height = new float[res, res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float streak = Mathf.PerlinNoise(x * 0.012f, y * 0.19f);          // 둘레 방향 결
                    float hammer = Mathf.PerlinNoise(x * 0.035f + 7.3f, y * 0.035f + 11.1f); // 두들김
                    height[x, y] = streak * 0.65f + hammer * 0.35f;
                }

            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false, true);
            const float strength = 3.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float dx = height[(x + 1) % res, y] - height[(x - 1 + res) % res, y];
                    float dy = height[x, (y + 1) % res] - height[x, (y - 1 + res) % res];
                    var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    tex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        internal static int CountTris(GameObject root)
        {
            int n = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) n += mf.sharedMesh.triangles.Length / 3;
            return n;
        }
    }
}
