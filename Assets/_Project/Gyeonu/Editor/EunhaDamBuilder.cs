using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 은하담(Gyeonu_EunhaDam) 씬 조립기. 멱등 — 다시 실행하면 관리 그룹을 지우고 재생성한다.
    ///
    /// 배치 좌표계: 다리 축 = X (문루 ±38.65), 강 흐름 = Z. 씬 중앙이 다리 중앙.
    /// 다리 구성은 BridgeCheck 씬의 ReducedBridge_Compare(감축본, 손으로 맞춘 배치)를
    /// z-45 오프셋으로 복사해 온다. BridgeCheck는 읽기만 하고 저장하지 않는다.
    ///
    /// 강 단면 (|x| 기준):
    ///   0~26 강바닥(-1.2) → 26~30 사면 → 30~45 교대 선반(+0.55, 교대 밑단 0.56)
    ///   → 45~70 완만한 언덕(+1.5). 수면은 y=0.5 — 교각 바닥(0.02)이 약 0.5m 잠긴다.
    ///   교각 3기 발밑에는 수중 기초 둔덕(-0.1)을 올려 바닥과 이어 붙인다.
    /// </summary>
    public static class EunhaDamBuilder
    {
        const string ScenePath = "Assets/_Project/Gyeonu/Scenes/Gyeonu_EunhaDam.unity";
        const string BridgeScenePath = "Assets/_Project/Scenes/Sandbox/Gyeonu/BridgeCheck.unity";
        const string BridgeSourceRoot = "ReducedBridge_Compare";
        const float BridgeZOffset = -45f;   // Compare 그룹이 z+45에 놓여 있음

        const string VillageTerrainDataPath = "Assets/_Project/Gyeonu/Art/Terrain/Gyeonu_TerrainData.asset";
        const string TerrainDataPath = "Assets/_Project/Gyeonu/Art/Terrain/EunhaDam_TerrainData.asset";

        const string NormalSrcPath = "Assets/Soswaewon/Textures/Environments/T_Water_NM.png";
        const string NormalDstPath = "Assets/_Project/Gyeonu/Art/Textures/T_Water_NM_EunhaDam.png";
        const string WaterMatPath = "Assets/_Project/Gyeonu/Art/Materials/Water_EunhaDam.mat";
        const string PresetFolder = "Assets/_Project/Gyeonu/Art/Water";
        const string SkyPresetPath = "Assets/_Project/Gyeonu/Art/Lighting/SkyPreset_낮_맑음.asset";

        // ── 물·지형 설계 상수 ────────────────────────────────
        const float WaterLevel = 0.5f;
        const float BedY = -1.2f;
        const float BankShelfY = 0.55f;
        const float BankHighY = 1.5f;
        const float MoundY = -0.1f;
        const float TerrainBaseY = -10f;    // 터레인 GO의 y (heights는 0~1이라 음수 지형용 오프셋)

        // 교각 발밑 기초 둔덕 (월드 XZ 사각형, BridgeCheck 실측 바운드 + z-45)
        static readonly Rect[] PierFootprints =
        {
            new Rect(-15.52f, -7.82f, 4.44f, 15.64f),   // Pier_2
            new Rect(-1.03f, -7.82f, 4.36f, 15.64f),    // Pier_3
            new Rect(13.48f, -7.82f, 4.44f, 15.64f),    // Pier_4
        };

        static readonly Dictionary<string, string> NameMap = new Dictionary<string, string>
        {
            { "Nugag_Reduced", "누각" },
            { "SouthGate_Reduced", "문루_남" },
            { "NorthGate_Reduced", "문루_북" },
            { "Piers_Compare", "교각" },
            { "Abutment_South_Compare", "교대_남" },
            { "Abutment_North_Compare", "교대_북" },
        };

        [MenuItem("Tools/이문록/은하담 조립 (전체)")]
        public static void BuildAll()
        {
            var scene = EnsureScene();
            EnsureWaterAssets();
            BuildLighting(scene);
            BuildTerrain();
            BuildBridge(scene);
            BuildWater();
            BuildMarkers();
            EnsureGroup("은하담_식생");   // 식생은 추후 배치 — 그룹만 확보
            EnsureCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[은하담] 조립 완료: " + ScenePath);
        }

        // ── 씬 ──────────────────────────────────────────────
        static Scene EnsureScene()
        {
            Scene scene;
            if (System.IO.File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                System.IO.Directory.CreateDirectory(
                    System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(ScenePath)));
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            SceneManager.SetActiveScene(scene);
            return scene;
        }

        static GameObject RecreateGroup(string name)
        {
            var old = FindRoot(name);
            if (old != null) Object.DestroyImmediate(old);
            return new GameObject(name);
        }

        static GameObject EnsureGroup(string name) => FindRoot(name) ?? new GameObject(name);

        static GameObject FindRoot(string name) =>
            SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);

        // ── 조명 (낮_맑음 프리셋) ────────────────────────────
        static void BuildLighting(Scene scene)
        {
            var lightGo = FindRoot("Directional Light");
            if (lightGo == null) lightGo = new GameObject("Directional Light");
            var light = lightGo.GetComponent<Light>();
            if (light == null) light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;

            var preset = AssetDatabase.LoadAssetAtPath<SkyPreset>(SkyPresetPath);
            if (preset != null) preset.Apply(light);
            else Debug.LogWarning("[은하담] SkyPreset_낮_맑음을 찾지 못해 조명 프리셋을 건너뜀");
        }

        // ── 지형 ────────────────────────────────────────────
        static float GroundHeight(float wx, float wz)
        {
            float ax = Mathf.Abs(wx);
            float h;
            if (ax <= 26f) h = BedY;
            else if (ax <= 30f) h = Mathf.SmoothStep(BedY, BankShelfY, (ax - 26f) / 4f);
            else if (ax <= 45f) h = BankShelfY;
            else if (ax <= 70f) h = Mathf.SmoothStep(BankShelfY, BankHighY, (ax - 45f) / 25f);
            else h = BankHighY;

            // 먼 언덕 살짝 울퉁불퉁하게
            if (ax > 46f)
                h += (Mathf.PerlinNoise(wx * 0.045f + 7.31f, wz * 0.045f + 2.17f) - 0.5f) * 0.5f;

            // 교각 기초 둔덕 (2m 마진 스무스 폴오프)
            foreach (var r in PierFootprints)
            {
                float dx = Mathf.Max(r.xMin - wx, 0f, wx - r.xMax);
                float dz = Mathf.Max(r.yMin - wz, 0f, wz - r.yMax);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < 2f)
                {
                    float t = 1f - Mathf.SmoothStep(0f, 1f, d / 2f);
                    h = Mathf.Max(h, Mathf.Lerp(h, MoundY, t));
                }
            }
            return h;
        }

        static void BuildTerrain()
        {
            var villageTd = AssetDatabase.LoadAssetAtPath<TerrainData>(VillageTerrainDataPath);

            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, TerrainDataPath);
            }

            td.heightmapResolution = 513;
            td.size = new Vector3(200f, 100f, 200f);
            td.alphamapResolution = 512;
            td.baseMapResolution = 1024;
            if (villageTd != null) td.terrainLayers = villageTd.terrainLayers;

            // 높이
            const int res = 513;
            var heights = new float[res, res];
            for (int z = 0; z < res; z++)
            {
                float wz = z / (float)(res - 1) * 200f - 100f;
                for (int x = 0; x < res; x++)
                {
                    float wx = x / (float)(res - 1) * 200f - 100f;
                    heights[z, x] = (GroundHeight(wx, wz) - TerrainBaseY) / 100f;
                }
            }
            td.SetHeights(0, 0, heights);

            // 스플랫: 강바닥 자갈 → 물가 흙 → 언덕 풀
            int layerCount = td.terrainLayers != null ? td.terrainLayers.Length : 0;
            int gravel = 16, dirt = 0, grass = 2;   // 자갈2_JG / 흙_낙안 / 풀_낙안
            if (layerCount > gravel)
            {
                const int ar = 512;
                var alpha = new float[ar, ar, layerCount];
                for (int z = 0; z < ar; z++)
                {
                    float wz = z / (float)(ar - 1) * 200f - 100f;
                    for (int x = 0; x < ar; x++)
                    {
                        float wx = x / (float)(ar - 1) * 200f - 100f;
                        float h = GroundHeight(wx, wz);
                        float wGravel = 1f - Mathf.SmoothStep(0.2f, 0.7f, h);
                        float wGrass = Mathf.SmoothStep(0.7f, 1.1f, h);
                        float wDirt = Mathf.Clamp01(1f - wGravel - wGrass);
                        alpha[z, x, gravel] = wGravel;
                        alpha[z, x, dirt] = wDirt;
                        alpha[z, x, grass] = wGrass;
                    }
                }
                td.SetAlphamaps(0, 0, alpha);
            }
            EditorUtility.SetDirty(td);

            var group = RecreateGroup("은하담_지형");
            var tgo = Terrain.CreateTerrainGameObject(td);
            tgo.name = "Terrain_EunhaDam";
            tgo.transform.SetParent(group.transform, false);
            tgo.transform.position = new Vector3(-100f, TerrainBaseY, -100f);

            var terrain = tgo.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 6f;
            terrain.basemapDistance = 400f;
        }

        // ── 다리 (BridgeCheck에서 복사) ──────────────────────
        static void BuildBridge(Scene target)
        {
            var group = RecreateGroup("은하담_다리");

            var src = EditorSceneManager.OpenScene(BridgeScenePath, OpenSceneMode.Additive);
            try
            {
                var root = src.GetRootGameObjects().FirstOrDefault(g => g.name == BridgeSourceRoot);
                if (root == null)
                {
                    Debug.LogError("[은하담] BridgeCheck에서 " + BridgeSourceRoot + " 를 찾지 못함");
                    return;
                }

                foreach (Transform child in root.transform)
                {
                    GameObject copy;
                    if (PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
                    {
                        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                        var path = AssetDatabase.GetAssetPath(prefabAsset);
                        copy = (GameObject)PrefabUtility.InstantiatePrefab(
                            AssetDatabase.LoadAssetAtPath<GameObject>(path), target);
                    }
                    else
                    {
                        copy = Object.Instantiate(child.gameObject);
                        SceneManager.MoveGameObjectToScene(copy, target);
                    }

                    copy.name = NameMap.TryGetValue(child.name, out var clean) ? clean : child.name;
                    copy.transform.SetParent(group.transform, false);
                    copy.transform.localPosition = child.localPosition + new Vector3(0f, 0f, BridgeZOffset);
                    copy.transform.localRotation = child.localRotation;
                    copy.transform.localScale = child.localScale;

                    // 손으로 맞춘 인스턴스 상태(비활성 자식·메시 교체 등)를 그대로 반영.
                    // 누각 프리팹은 원본+감축 메시가 공존하고 씬 인스턴스에서 원본을 꺼둔 구조라 필수.
                    SyncChildren(child, copy.transform);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(src, true);   // 저장하지 않고 닫기
            }

            // 정적 플래그 (배칭·오클루전 대비)
            ApplyStaticFlags(group);
        }

        /// <summary>같은 프리팹에서 나온 두 계층을 형제 순서로 짝지어 인스턴스 상태를 복사한다.</summary>
        static void SyncChildren(Transform src, Transform dst)
        {
            dst.gameObject.SetActive(src.gameObject.activeSelf);

            var srcMf = src.GetComponent<MeshFilter>();
            var dstMf = dst.GetComponent<MeshFilter>();
            if (srcMf != null && dstMf != null && dstMf.sharedMesh != srcMf.sharedMesh)
                dstMf.sharedMesh = srcMf.sharedMesh;

            var srcMr = src.GetComponent<MeshRenderer>();
            var dstMr = dst.GetComponent<MeshRenderer>();
            if (srcMr != null && dstMr != null)
            {
                dstMr.enabled = srcMr.enabled;
                dstMr.sharedMaterials = srcMr.sharedMaterials;
            }

            int n = Mathf.Min(src.childCount, dst.childCount);
            for (int i = 0; i < n; i++)
            {
                var s = src.GetChild(i);
                var d = dst.GetChild(i);
                d.localPosition = s.localPosition;
                d.localRotation = s.localRotation;
                d.localScale = s.localScale;
                SyncChildren(s, d);
            }
        }

        static void ApplyStaticFlags(GameObject group)
        {
            var flags = StaticEditorFlags.BatchingStatic
                      | StaticEditorFlags.OccluderStatic
                      | StaticEditorFlags.OccludeeStatic;
            foreach (var t in group.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        // ── 물 에셋 (텍스처·머티리얼·프리셋) ──────────────────
        static void EnsureWaterAssets()
        {
            // 1) 물결 노멀맵: 소쇄원 원본을 _Project로 복제 (원본 폴더 수정 금지 규칙)
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalDstPath);
            if (normal == null)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(NormalSrcPath) != null)
                {
                    EnsureFolder("Assets/_Project/Gyeonu/Art/Textures");
                    AssetDatabase.CopyAsset(NormalSrcPath, NormalDstPath);
                    var imp = (TextureImporter)AssetImporter.GetAtPath(NormalDstPath);
                    imp.textureType = TextureImporterType.NormalMap;
                    imp.maxTextureSize = 1024;
                    imp.SaveAndReimport();
                    normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalDstPath);
                }
                else
                {
                    Debug.LogWarning("[은하담] 소쇄원 T_Water_NM 미임포트 — 물결 노멀 없이 진행");
                }
            }

            // 2) 수면 머티리얼 (URP Lit, 알파 블렌드 투명)
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, WaterMatPath);
            }
            mat.SetFloat("_Surface", 1f);   // Transparent
            mat.SetFloat("_Blend", 0f);     // Alpha
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.SetFloat("_SpecularHighlights", 1f);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            // 수면 64×205m, 타일 1장 ≈ 5.3m
            mat.SetTextureScale("_BaseMap", new Vector2(12f, 40f));
            EditorUtility.SetDirty(mat);

            // 3) 프리셋 2종
            EnsureFolder(PresetFolder);
            EnsurePreset("물_맑음", new Color(0.13f, 0.24f, 0.26f, 0.35f), 0.95f, 0.6f, new Vector2(0f, 0.025f));
            EnsurePreset("물_흐림", new Color(0.15f, 0.18f, 0.16f, 0.88f), 0.55f, 1.4f, new Vector2(0f, 0.045f));
            AssetDatabase.SaveAssets();
        }

        static void EnsurePreset(string name, Color color, float smooth, float wave, Vector2 flow)
        {
            var path = PresetFolder + "/" + name + ".asset";
            var p = AssetDatabase.LoadAssetAtPath<WaterPreset>(path);
            if (p != null) return;   // 이미 있으면 사용자가 조정한 값을 보존
            p = ScriptableObject.CreateInstance<WaterPreset>();
            p.waterColor = color;
            p.smoothness = smooth;
            p.waveStrength = wave;
            p.flowSpeed = flow;
            AssetDatabase.CreateAsset(p, path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ── 수면 배치 ────────────────────────────────────────
        static void BuildWater()
        {
            var group = RecreateGroup("은하담_물");

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "수면";
            var col = plane.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);   // 물은 막지 않음

            plane.transform.SetParent(group.transform, false);
            plane.transform.position = new Vector3(0f, WaterLevel, 0f);
            plane.transform.localScale = new Vector3(6.4f, 1f, 20.5f);   // 64 × 205 m

            var rend = plane.GetComponent<Renderer>();
            rend.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var surface = plane.AddComponent<WaterSurface>();
            surface.preset = AssetDatabase.LoadAssetAtPath<WaterPreset>(PresetFolder + "/물_맑음.asset");
            if (surface.preset != null)
                surface.preset.ApplyTo(rend.sharedMaterial);
        }

        // ── 마커·카메라 ─────────────────────────────────────
        static void BuildMarkers()
        {
            var group = RecreateGroup("은하담_마커");

            var spawn = new GameObject("SpawnPoint_PlayerStart");
            spawn.transform.SetParent(group.transform, false);
            spawn.transform.position = new Vector3(48f, 0.7f, 0f);      // 남안(마을 방향), 문루 앞
            spawn.transform.rotation = Quaternion.Euler(0f, 270f, 0f);  // 다리를 바라봄 (-X)

            var exit = new GameObject("Exit_ToVillage");
            exit.transform.SetParent(group.transform, false);
            exit.transform.position = new Vector3(60f, 0.9f, 0f);
        }

        static void EnsureCamera()
        {
            var camGo = FindRoot("Main Camera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            camGo.transform.position = new Vector3(0f, 8f, -55f);
            camGo.transform.LookAt(new Vector3(0f, 6f, 0f));
        }
    }
}
