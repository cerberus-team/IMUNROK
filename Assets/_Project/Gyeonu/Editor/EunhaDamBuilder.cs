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

        // Uber Stylized Water (Assets/Shaders/ — gitignore, 팀원 각자 임포트)
        const string WaterTemplatePath = "Assets/Shaders/Uber Stylized Water/Template Materials/UWa-Template-Clear.mat";
        const string WaterMatPath = "Assets/_Project/Gyeonu/Art/Materials/Water_EunhaDam_Stylized.mat";
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
            terrain.heightmapPixelError = 25f;   // 2026-08-05 렌더 최적화 값 유지
            terrain.basemapDistance = 300f;
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

        // ── 물 에셋 (머티리얼·프리셋) ────────────────────────
        static void EnsureWaterAssets()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            if (mat == null)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(WaterTemplatePath) == null)
                {
                    Debug.LogError("[은하담] Uber Stylized Water 팩이 없습니다 (Assets/Shaders/Uber Stylized Water). 임포트 후 다시 실행하세요.");
                    return;
                }
                // 원본 폴더 무수정 규칙: 템플릿을 _Project로 복제해서 사용
                AssetDatabase.CopyAsset(WaterTemplatePath, WaterMatPath);
                mat = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            }

            // 기능 키워드는 머티리얼에 고정 (프리셋은 값만 바꿈 — 빌드 배리언트 안전)
            SetKeyword(mat, "_ENABLEPLANERREFLECTION", false);  // 평면 반사 OFF → 프로브 경로 = VR 안전, 별 반사는 스카이박스로 충분
            SetKeyword(mat, "_ENABLENORMAL", true);
            SetKeyword(mat, "_ENABLEREFRACTION", true);
            SetKeyword(mat, "_ENABLECAUSTICS", true);
            SetKeyword(mat, "_ENABLEWAVE", false);              // 버텍스 파도는 평면 정점이 적어 미사용
            SetKeyword(mat, "_ENABLESHORELINE", false);
            SetKeyword(mat, "_ENABLE_UNDERWATERLAYER", false);

            // 고요한 못: 패닝은 +Z로 아주 느리게, 왜곡도 약하게 (별 반사가 뭉개지지 않게)
            mat.SetVector("_SurfaceDistortion_Pan", new Vector4(0f, 0.25f, 0f, 0f));
            mat.SetFloat("_SurfaceDistortion_Strength", 0.15f);
            mat.SetVector("_SurfFoam_Pan", new Vector4(0f, 0.12f, 0f, 0f));
            EditorUtility.SetDirty(mat);

            // 프리셋 2종 — 없을 때만 기본값으로 생성 (사용자 조정 보존)
            if (AssetDatabase.LoadAssetAtPath<WaterPreset>(PresetFolder + "/물_맑음.asset") == null ||
                AssetDatabase.LoadAssetAtPath<WaterPreset>(PresetFolder + "/물_흐림.asset") == null)
                ResetPresetDefaults();

            AssetDatabase.SaveAssets();
        }

        static void SetKeyword(Material mat, string keyword, bool on)
        {
            if (mat.HasFloat(keyword)) mat.SetFloat(keyword, on ? 1f : 0f);
            if (on) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        /// <summary>프리셋 2종을 코드에 정의된 기본값으로 되돌린다 (셰이더 교체 마이그레이션 포함).</summary>
        [MenuItem("Tools/이문록/은하담 물 프리셋 기본값 재설정")]
        public static void ResetPresetDefaults()
        {
            EnsureFolder(PresetFolder);
            // 반사는 "별은 보이되 하늘 복사 느낌은 없는" 선 — 대신 물결을 키워 수면을 살림
            WritePreset("물_맑음", p =>
            {
                p.shallowColor = new Color(0.18f, 0.55f, 0.50f, 0.05f);
                p.deepColor = new Color(0.02f, 0.20f, 0.28f, 0.60f);
                p.waterDepth = 1.2f;
                p.reflectionStrength = 0.50f;
                p.reflectionFresnel = 2.5f;   // 정면 반사 억제 = "하늘 복사" 방지, 스침각 별 강조
                p.reflectionDistortion = 0.04f;
                p.normalStrength = 0.12f;     // 은하담은 "고요한 못" — 잔잔해야 별이 비친다
                p.normalPan = 0.06f;
                p.causticsStrength = 1.2f;   // 밤에 도드라지지 않는 수준
                p.refractionStrength = 0.5f;
            });
            WritePreset("물_흐림", p =>
            {
                p.shallowColor = new Color(0.30f, 0.30f, 0.22f, 0.90f);
                p.deepColor = new Color(0.11f, 0.13f, 0.10f, 1f);
                p.waterDepth = 0.35f;             // 금방 탁해짐
                p.reflectionStrength = 0.28f;
                p.reflectionFresnel = 1.2f;
                p.reflectionDistortion = 0.6f;    // 반사를 흐트러뜨림
                p.normalStrength = 0.60f;
                p.normalPan = 0.30f;
                p.causticsStrength = 0f;
                p.refractionStrength = 0.1f;
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[은하담] 물 프리셋 기본값 재설정 완료");
        }

        static void WritePreset(string name, System.Action<WaterPreset> fill)
        {
            var path = PresetFolder + "/" + name + ".asset";
            var p = AssetDatabase.LoadAssetAtPath<WaterPreset>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<WaterPreset>();
                AssetDatabase.CreateAsset(p, path);
            }
            fill(p);
            EditorUtility.SetDirty(p);
        }

        /// <summary>물 관련만 다시 조립 (다리·지형은 건드리지 않음).</summary>
        [MenuItem("Tools/이문록/은하담 물만 재조립")]
        public static void RebuildWaterOnly()
        {
            var scene = EnsureScene();
            EnsureWaterAssets();
            BuildWater();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[은하담] 물 재조립 완료");
        }

        // ── 밤하늘 텍스처 하단 어둡게 굽기 ────────────────────
        const string SkyboxFolder = "Assets/_Project/Gyeonu/Art/Lighting/Skybox";
        const string NightSrcMatPath = "Assets/Fantasy Skybox FREE/Panoramics/FS003/FS003_Night.mat";
        const string NightSrcTexPath = "Assets/Fantasy Skybox FREE/Panoramics/FS003/FS003_Night.png";
        const string NightDarkMatPath = SkyboxFolder + "/FS003_Night_Dark.mat";
        const string NightDarkTexPath = SkyboxFolder + "/FS003_Night_Dark.png";

        /// <summary>확정 강도(중=0.8) 네이비 그라데이션 + 별 합성으로 밤하늘을 굽는다.</summary>
        [MenuItem("Tools/이문록/밤하늘 하단 어둡게 굽기")]
        public static void DarkenNightSkybox() => BakeNavyNightSkybox(NightDarkMatPath, NightDarkTexPath, 0.8f);

        // 별 합성 파라미터 — 텍스처에 직접 찍으므로 Play·포커스와 무관하게 항상 보이고,
        // 수면 반사도 스카이박스 반사 경로로 자동 해결된다 (파티클 방식은 2026-08-06 폐기)
        const int StarCount = 900;
        const float StarHorizonDensity = 0.35f;   // 수평선 근처 밀도 (1 = 천정과 동일)
        const int StarSeed = 20260807;            // 고정 시드 — 재굽기해도 같은 별자리

        /// <summary>
        /// FS003_Night 파노라마(등장방형) 복제본의 하단을 청록→네이비로 색조 이동시켜 굽는다.
        /// 밝기만 곱하지 않고 녹색 채널을 강하게 죽이고 파란 채널을 상대적으로 살려 남색화한다.
        /// v 0.70(고도 약 36도)부터 수평선(0.48) 아래까지 smoothstep이라 경계선이 안 생긴다.
        /// navyMix: 0~1, 하단에서 네이비로 끌어당기는 강도.
        /// </summary>
        public static void BakeNavyNightSkybox(string matPath, string texPath, float navyMix)
        {
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(NightSrcTexPath);
            if (src == null) { Debug.LogError("[밤하늘] 원본 텍스처 없음: " + NightSrcTexPath); return; }
            EnsureFolder(SkyboxFolder);

            // 원본을 읽기 가능하게 복사 (임포터 설정 무수정 — Blit 경유)
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = tex.GetPixels();
            int w = src.width, h = src.height;
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                float t = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.70f, v));
                float mix = t * navyMix;
                if (mix <= 0.001f) continue;
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var c = pixels[i];
                    // 남색화: G를 강하게 죽이고 B는 상대적으로 유지, R도 낮춰 차가운 톤
                    var navy = new Color(c.r * 0.10f, c.g * 0.16f, c.b * 0.55f, c.a);
                    pixels[i] = Color.Lerp(c, navy, mix);
                }
            }
            // ── 별 합성 (상반구, 고도 가중: 천정 빽빽 → 수평선 성김) ──
            // 등장방형은 세로축=고도라 v=0.5+고도/180. 고위도에서 가로가 늘어나므로
            // 점이 3D에서 원형으로 보이게 x반경을 1/cos(고도)로 보정한다.
            var rnd = new System.Random(StarSeed);
            for (int i = 0; i < StarCount; i++)
            {
                // 고도 상한 80° — 등장방형 천정부는 한 점이 가로줄로 퍼져 폭죽 아티팩트가 생긴다
                float elev;
                do { elev = 2f + (float)rnd.NextDouble() * 78f; }
                while (rnd.NextDouble() > Mathf.Lerp(StarHorizonDensity, 1f, elev / 80f));

                float v = 0.5f + elev / 180f;
                int cy = Mathf.RoundToInt(v * (h - 1));
                int cx = Mathf.RoundToInt((float)rnd.NextDouble() * (w - 1));

                // 크기는 통일(1.2~2.0px), 등급 차이는 밝기로 — 큰 별이 반사에서 방울지는 것 방지
                bool brightStar = rnd.NextDouble() < 0.12;
                float radius = 1.2f + (float)rnd.NextDouble() * 0.8f;
                float amp = brightStar ? 1.0f : 0.5f + (float)rnd.NextDouble() * 0.3f;

                float xStretch = 1f / Mathf.Max(Mathf.Cos(elev * Mathf.Deg2Rad), 0.12f);
                int rx = Mathf.CeilToInt(radius * xStretch) + 1;
                int ry = Mathf.CeilToInt(radius) + 1;
                for (int dy = -ry; dy <= ry; dy++)
                {
                    int py = cy + dy;
                    if (py < 0 || py >= h) continue;
                    for (int dx = -rx; dx <= rx; dx++)
                    {
                        int px = ((cx + dx) % w + w) % w;   // 가로는 이어지므로 랩
                        float nx = dx / (radius * xStretch);
                        float ny = dy / radius;
                        float d2 = nx * nx + ny * ny;
                        if (d2 >= 1f) continue;
                        float fall = (1f - d2) * (1f - d2) * amp;
                        int idx = py * w + px;
                        var c0 = pixels[idx];
                        pixels[idx] = new Color(
                            Mathf.Min(1f, c0.r + fall),
                            Mathf.Min(1f, c0.g + fall),
                            Mathf.Min(1f, c0.b + fall * 1.03f), c0.a);
                    }
                }
            }

            tex.SetPixels(pixels);
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.maxTextureSize = 2048;
            imp.wrapModeU = TextureWrapMode.Repeat;   // 파노라마 가로는 이어짐
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.SaveAndReimport();

            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) == null)
                AssetDatabase.CopyAsset(NightSrcMatPath, matPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("[밤하늘] 네이비 굽기 완료 (mix=" + navyMix + "): " + matPath);
        }

        // ── 하늘·물 통합 프리셋 4종 구성 ─────────────────────
        /// <summary>
        /// 날씨 2종 × 시간대 2종 = 4상태 프리셋을 구성한다 (마을·은하담 공용).
        /// 기존 에셋은 GUID 유지를 위해 rename하고, 각 프리셋에 스카이박스·조명·안개·물 프리셋을
        /// 연결한 뒤 하늘 반사 큐브맵까지 다시 굽는다. 멱등 — 다시 실행해도 안전.
        /// </summary>
        [MenuItem("Tools/이문록/하늘·물 프리셋 4종 구성")]
        public static void ConfigureSkyPresets()
        {
            const string dir = "Assets/_Project/Gyeonu/Art/Lighting";

            // 1) 이름 정리 (GUID 보존): 밤 → 밤_맑음, 낮_흐림 → 낮_비
            RenamePresetIfNeeded(dir, "SkyPreset_밤", "SkyPreset_밤_맑음");
            RenamePresetIfNeeded(dir, "SkyPreset_낮_흐림", "SkyPreset_낮_비");

            var clearWater = AssetDatabase.LoadAssetAtPath<WaterPreset>(PresetFolder + "/물_맑음.asset");
            var murkyWater = AssetDatabase.LoadAssetAtPath<WaterPreset>(PresetFolder + "/물_흐림.asset");

            // 2) 4종 값 구성
            ConfigurePreset(dir, "SkyPreset_낮_맑음",
                "Assets/Fantasy Skybox FREE/Panoramics/FS003/FS003_Day.mat", clearWater, Color.white,
                new Vector3(50f, 60f, 0f), new Color(1f, 0.97f, 0.90f), 1.1f, 0.85f,
                new Color(0.55f, 0.62f, 0.72f), new Color(0.45f, 0.45f, 0.42f), new Color(0.25f, 0.23f, 0.20f),
                new Color(0.75f, 0.80f, 0.84f), 60f, 220f);

            // 밤_맑음: 하단 어둡게 구운 복제 스카이박스 (없으면 먼저 굽는다)
            if (AssetDatabase.LoadAssetAtPath<Material>(NightDarkMatPath) == null)
                DarkenNightSkybox();
            ConfigurePreset(dir, "SkyPreset_밤_맑음",
                NightDarkMatPath, clearWater,
                new Color(0.30f, 0.36f, 0.52f),   // 달빛 푸른기 — 밤에 물색이 낮처럼 밝지 않게
                new Vector3(40f, 250f, 0f), new Color(0.55f, 0.65f, 0.90f), 0.28f, 0.55f,
                new Color(0.08f, 0.10f, 0.18f), new Color(0.05f, 0.06f, 0.10f), new Color(0.02f, 0.02f, 0.04f),
                new Color(0.10f, 0.15f, 0.21f), 35f, 170f,
                0.85f, 1.2f, 0.02f);   // 별 반사 강조: 강한 반사 + 낮은 프레넬(내려다봐도 별이 깔림) + 최소 왜곡

            ConfigurePreset(dir, "SkyPreset_낮_비",
                "Assets/Fantasy Skybox FREE/Panoramics/FS003/FS003_Rainy.mat", murkyWater,   // FS003 계열 통일
                new Color(0.72f, 0.75f, 0.78f),
                new Vector3(45f, 60f, 0f), new Color(0.62f, 0.66f, 0.70f), 0.5f, 0.2f,
                new Color(0.32f, 0.34f, 0.37f), new Color(0.24f, 0.25f, 0.27f), new Color(0.12f, 0.12f, 0.13f),
                new Color(0.42f, 0.45f, 0.49f), 25f, 160f);   // 다리 전체가 보이되 비 분위기 유지

            ConfigurePreset(dir, "SkyPreset_밤_비",
                "Assets/Fantasy Skybox FREE/Panoramics/FS017/FS017_Rainy.mat", murkyWater,
                new Color(0.14f, 0.16f, 0.22f),
                new Vector3(40f, 250f, 0f), new Color(0.20f, 0.23f, 0.30f), 0.14f, 0f,
                new Color(0.035f, 0.04f, 0.06f), new Color(0.02f, 0.025f, 0.04f), new Color(0.01f, 0.01f, 0.02f),
                new Color(0.025f, 0.03f, 0.045f), 10f, 60f);

            // 3) 이름이 바뀐 옛 큐브맵 정리 후 재굽기
            AssetDatabase.DeleteAsset(dir + "/EnvCube_SkyPreset_밤.asset");
            AssetDatabase.DeleteAsset(dir + "/EnvCube_SkyPreset_낮_흐림.asset");
            AssetDatabase.SaveAssets();
            BakeSkyReflectionCubemaps();
            Debug.Log("[프리셋4종] 구성 완료 (낮_맑음/밤_맑음/낮_비/밤_비)");
        }

        static void RenamePresetIfNeeded(string dir, string from, string to)
        {
            if (AssetDatabase.LoadAssetAtPath<SkyPreset>(dir + "/" + to + ".asset") != null) return;
            if (AssetDatabase.LoadAssetAtPath<SkyPreset>(dir + "/" + from + ".asset") == null) return;
            var err = AssetDatabase.RenameAsset(dir + "/" + from + ".asset", to);
            if (!string.IsNullOrEmpty(err)) Debug.LogError("[프리셋4종] rename 실패: " + err);
        }

        static void ConfigurePreset(string dir, string name, string skyboxPath, WaterPreset water, Color waterTint,
            Vector3 sunEuler, Color sunColor, float sunIntensity, float shadowStrength,
            Color ambSky, Color ambEq, Color ambGround, Color fogColor, float fogStart, float fogEnd,
            float reflStrength = -1f, float reflFresnel = -1f, float reflDistortion = -1f)
        {
            var path = dir + "/" + name + ".asset";
            var p = AssetDatabase.LoadAssetAtPath<SkyPreset>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<SkyPreset>();
                AssetDatabase.CreateAsset(p, path);
            }
            var skybox = AssetDatabase.LoadAssetAtPath<Material>(skyboxPath);
            if (skybox == null)
                Debug.LogError("[프리셋4종] 스카이박스 없음: " + skyboxPath + " (Fantasy Skybox FREE 임포트 확인)");
            p.skyboxMaterial = skybox;
            p.waterPreset = water;
            p.waterColorMultiplier = waterTint;
            p.sunEulerAngles = sunEuler;
            p.sunColor = sunColor;
            p.sunIntensity = sunIntensity;
            p.sunShadowStrength = shadowStrength;
            p.ambientSky = ambSky;
            p.ambientEquator = ambEq;
            p.ambientGround = ambGround;
            p.fogEnabled = true;
            p.fogColor = fogColor;
            p.fogStart = fogStart;
            p.fogEnd = fogEnd;
            p.waterReflectionStrength = reflStrength;
            p.waterReflectionFresnel = reflFresnel;
            p.waterReflectionDistortion = reflDistortion;
            EditorUtility.SetDirty(p);
        }

        // ── 난간 콜라이더 (물에 빠지지 않게 통행로 양옆을 막음) ──
        [MenuItem("Tools/이문록/은하담 난간 콜라이더")]
        public static void EnsureRailingColliders()
        {
            var bridge = FindRoot("은하담_다리");
            if (bridge == null) { Debug.LogError("[난간] 은하담_다리 그룹이 없음"); return; }

            var old = bridge.transform.Find("난간_콜라이더");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var group = new GameObject("난간_콜라이더");
            group.transform.SetParent(bridge.transform, false);
            // 장마루 폭 z ±4.4 기준. 문루 통로까지 커버하도록 x ±46.
            AddRailBox(group, "난간_남", new Vector3(0f, 9.5f, -4.55f), new Vector3(92f, 1.8f, 0.35f));
            AddRailBox(group, "난간_북", new Vector3(0f, 9.5f, 4.55f), new Vector3(92f, 1.8f, 0.35f));
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[난간] 박스 콜라이더 2개 배치");
        }

        static void AddRailBox(GameObject parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            go.isStatic = true;
        }

        // ── 하늘 반사 큐브맵 굽기 ────────────────────────────
        /// <summary>
        /// 모든 SkyPreset의 스카이박스를 큐브맵 에셋으로 구워 프리셋에 연결한다.
        /// SkyPreset.Apply가 이를 customReflectionTexture로 지정 → 수면에 그 하늘(밤엔 별·달)이 비친다.
        /// 스카이박스 머티리얼을 바꾸면 다시 실행할 것.
        /// </summary>
        [MenuItem("Tools/이문록/하늘 반사 큐브맵 굽기")]
        public static void BakeSkyReflectionCubemaps() => BakeSkyReflectionCubemaps(null);

        /// <summary>onlyPresetName을 주면 그 프리셋만 굽는다 (2048 전체 굽기는 무거워 분할 실행용).</summary>
        public static void BakeSkyReflectionCubemaps(string onlyPresetName)
        {
            const int res = 2048;   // 별 점이 밉 블러에서 살아남도록 상향 (2026-08-07 재상향)
            var presets = AssetDatabase.FindAssets("t:SkyPreset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SkyPreset>)
                .Where(p => p != null && p.skyboxMaterial != null)
                .Where(p => onlyPresetName == null || p.name == onlyPresetName)
                .ToList();
            if (presets.Count == 0) { Debug.LogWarning("[하늘굽기] SkyPreset이 없습니다"); return; }

            var prevSkybox = RenderSettings.skybox;
            var rt = new RenderTexture(res, res, 16) { dimension = UnityEngine.Rendering.TextureDimension.Cube };
            var face = new Texture2D(res, res, TextureFormat.RGBA32, false);
            var camGo = new GameObject("__SkyBakeCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.cullingMask = 0;    // 하늘만 (별은 스카이박스 텍스처에 합성돼 있음)
                cam.farClipPlane = 900f;

                foreach (var preset in presets)
                {
                    RenderSettings.skybox = preset.skyboxMaterial;
                    if (!cam.RenderToCubemap(rt))
                    {
                        Debug.LogError("[하늘굽기] RenderToCubemap 실패: " + preset.name);
                        continue;
                    }

                    var path = "Assets/_Project/Gyeonu/Art/Lighting/EnvCube_" + preset.name + ".asset";
                    var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                    if (cube == null || cube.width != res)
                    {
                        cube = new Cubemap(res, TextureFormat.RGBA32, true);
                        AssetDatabase.CreateAsset(cube, path);
                        cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                    }
                    for (int f = 0; f < 6; f++)
                    {
                        Graphics.SetRenderTarget(rt, 0, (CubemapFace)f);
                        face.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                        cube.SetPixels(face.GetPixels(), (CubemapFace)f);
                    }
                    Graphics.SetRenderTarget(null);
                    cube.Apply(true);
                    EditorUtility.SetDirty(cube);

                    preset.reflectionCubemap = cube;
                    EditorUtility.SetDirty(preset);
                    Debug.Log("[하늘굽기] " + preset.name + " → " + path);
                }
            }
            finally
            {
                RenderSettings.skybox = prevSkybox;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(face);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[하늘굽기] 완료: " + presets.Count + "개 프리셋");
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
            plane.transform.localScale = new Vector3(7.2f, 1f, 20.5f);   // 강폭 72m (다리 65m보다 넓게) × 205m

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
