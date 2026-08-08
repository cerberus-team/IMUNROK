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
    /// 강 단면 v2 (|x| 기준, 폭은 z에 따라 가변):
    ///   못(|z|&lt;30) 반폭 27m → 상·하류(|z|&gt;75) 15m로 좁아진다.
    ///   바닥(-1.2) → 물가 완사면 → 선반(0.55) → 둑(다리 근처 2.0, 먼 곳 1.2) → 초지(1.0).
    ///   수면 y=0.5 — 물가 완사면 위 h 0.5 지점이 물가선. 교각 발밑 기초 둔덕(-0.1) 유지.
    ///   +X 입구 통로(z±8)는 둑을 가르고 0.62→1.35로 완만히 오른다 (마커 y 0.7/0.9와 정합).
    ///   외곽은 r 70~100 언덕(최대 12m)으로 지평선을 가리되 강줄기·마을 골목은 노치로 뚫는다.
    ///   서안에 정자 마당 2곳(초정 -44,30 / 풍영정 -50,-38)과 돌다리용 개울(z≈20)을 판다.
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
            BuildFarMountains();
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

        // ── 지형 v5 (2026-08-07 3차: 교대는 "옆을 채우되 덮지 않는다") ────────────
        // 실측: 교대 상단 9.03/8.79 (|x| 28.9~44.4, z -33.5~15.1),
        //       문루 기단·월대(계단) 밑면 6.94 (|x| 42.1~57.7, |z|<13.6).
        // 둑 상단 8.3 — 교대 상단(9.03)보다 낮아 석축 머리가 풀 위로 보인다.
        // 사면 13m(약 32도)로 둑이 교대 뒤(44.4)에서 8.1까지 자연히 닿고, 석축은
        // 사면에서 반쯤 튀어나온다. (v4의 9.15 강제 스탬프는 교대를 통째로 묻어 폐기,
        // 어깨 둔덕 방식도 물가에 수직 절벽을 만들어 폐기 — 순수 단면 프로파일만 사용)
        const float BankTopNearY = 8.3f;   // 다리 인접 둑 상단 (교대 상단 9.03 - 0.7)
        const float BankTopFarY = 1.2f;    // 다리에서 먼 둑
        const float EdgeY = 2.0f;          // 지도 가장자리 기준 높이 (스커트 1.3보다 위)
        const float ForecourtY = 7.02f;    // 문루 앞마당 — 월대·기단 밑면(6.94)을 8cm 묻는다

        // 풍영정 호수 대(臺): 수면(0.5) 아래 0.15로 평탄화 — 정자가 물 위에 뜬다
        public const float PondPadX = -13f, PondPadZ = 40f;
        const float PondPadR = 8f, PondPadFall = 5f, PondPadY = 0.15f;

        /// <summary>
        /// GLSL식 smoothstep(edge0, edge1, x) — 0~1 반환.
        /// ⚠️ Mathf.SmoothStep(a,b,t)는 a→b "값 보간"이라 edge 용도로 쓰면 안 된다
        /// (t가 0~1로 클램프되어 x>1이면 항상 b를 반환 — 2026-08-07 지형 폭주 버그의 원인).
        /// </summary>
        static float SStep(float edge0, float edge1, float x) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));

        /// <summary>
        /// 사각 채움 마스크: 변별 페이드 폭 + 정규화 유클리드 거리로 모서리가 둥글게
        /// 빠진다. 안쪽 1, 각 변에서 해당 페이드 폭에 걸쳐 0으로.
        /// </summary>
        static float FillMask(float x, float z, float xMin, float xMax, float zMin, float zMax,
                              float fadeXMin, float fadeXMax, float fadeZ)
        {
            float ox = Mathf.Max(Mathf.Max((xMin - x) / fadeXMin, (x - xMax) / fadeXMax), 0f);
            float oz = Mathf.Max(Mathf.Max((zMin - z) / fadeZ, (z - zMax) / fadeZ), 0f);
            float d = Mathf.Sqrt(ox * ox + oz * oz);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d));
        }

        // 북 전면벽 crest(y=7.1) 안쪽면 폴리라인 — z 0.5m 간격 절단 실측 (2026-08-07).
        // z -28~-6은 완만한 사선(-34.01→-33.06), 중앙블록(z -5.5~7.6)은 -34.14,
        // 북단(z 8.6~)은 -33.2. 직각 구간 근사는 사선을 못 따라가 뜨는 지점을 만든다.
        static readonly float[][] NorthCrestFace =
        {
            new[] { -28.5f, -34.05f }, new[] { -28.0f, -34.01f }, new[] { -26.0f, -33.89f },
            new[] { -24.0f, -33.77f }, new[] { -22.0f, -33.65f }, new[] { -20.0f, -33.51f },
            new[] { -18.0f, -33.37f }, new[] { -16.0f, -33.26f }, new[] { -14.0f, -33.14f },
            new[] { -12.0f, -33.01f }, new[] { -10.0f, -33.03f }, new[] { -8.0f, -33.04f },
            // 중앙블록 모서리 전환은 실측 모서리 위치(z≈-5.75, 8.7)에 맞춘 급경사 —
            // 완만히 보간하면 모서리 구석이 덜 차거나(z -6) 면을 넘는다(z 8.5)
            new[] { -6.0f, -33.06f }, new[] { -5.85f, -33.06f }, new[] { -5.65f, -34.14f },
            new[] { 7.6f, -34.14f }, new[] { 8.0f, -34.00f }, new[] { 8.55f, -34.00f },
            new[] { 8.75f, -33.17f }, new[] { 13.9f, -33.21f },
        };

        /// <summary>북 전면벽 채움 경계: 폴리라인 선형 보간 + 갓돌 안쪽으로 0.15m 관입
        /// (갓돌 폭 ~0.5m 안 — 접촉 보장, 유출 없음).</summary>
        static float NorthCrestXMax(float wz)
        {
            var t = NorthCrestFace;
            if (wz <= t[0][0]) return t[0][1] + 0.15f;
            for (int i = 1; i < t.Length; i++)
                if (wz <= t[i][0])
                    return Mathf.Lerp(t[i - 1][1], t[i][1],
                        Mathf.InverseLerp(t[i - 1][0], t[i][0], wz)) + 0.15f;
            return t[t.Length - 1][1] + 0.15f;
        }

        /// <summary>강 반폭: 못(|z|&lt;30) 27m → 상·하류(|z|&gt;75) 15m.</summary>
        public static float ChannelHalfWidth(float wz)
        {
            float az = Mathf.Abs(wz);
            return Mathf.Lerp(27f, 15f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 75f, az)));
        }

        public static float GroundHeight(float wx, float wz)
        {
            float ax = Mathf.Abs(wx);
            float az = Mathf.Abs(wz);
            float w = ChannelHalfWidth(wz);

            // 1) 강 단면: 바닥 → 물가 완사면 (h 0.5 부근이 물가선 — 갈대 밭) → 선반
            float h;
            if (ax <= w - 4f) h = BedY;
            else if (ax <= w + 5f) h = Mathf.SmoothStep(BedY, BankShelfY, (ax - (w - 4f)) / 9f);
            else h = BankShelfY;

            // 2) 둑: 다리 구간(|z|<30)은 상단 9.2 — 교대 뒤·옆이 완전히 묻혀 앞면(석축)만
            //    보인다. 중간 단을 두어 계단식 잔디 둑. 둑이 높을수록 사면을 넓게 잡아
            //    경사를 20~25도로 유지. 둑 너머 배후지는 지도 끝 2.0으로 완만히
            float bankTop = Mathf.Lerp(BankTopNearY, BankTopFarY,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 65f, az)));
            // 사면 폭 13m 고정 — 다리 근처 경사 약 32도. 이 기울기로 둑이 교대 뒤(44.4)에서
            // 8.1까지 올라 붙고, 석축은 사면에서 반쯤 튀어나온다 (둔덕·스탬프 불필요)
            const float rampW = 13f;
            if (ax > w + 5f)
            {
                if (ax <= w + 5f + rampW)
                {
                    float u = (ax - (w + 5f)) / rampW;
                    float t = 0.5f * (SStep(0f, 0.55f, u) + SStep(0.45f, 1f, u));
                    h = Mathf.Lerp(BankShelfY, bankTop, t);
                }
                else
                    h = Mathf.Lerp(bankTop, EdgeY,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(w + 9f + rampW, 95f, ax)));
            }

            // 3) 배후지 노이즈 (둑 사면·물가는 매끈하게 유지)
            float noiseMask = SStep(w + 16f, w + 26f, ax);
            h += (Mathf.PerlinNoise(wx * 0.045f + 7.31f, wz * 0.045f + 2.17f) - 0.5f) * 0.6f * noiseMask;

            // 3.7) 교대 안쪽 채움: ㄷ자 옹벽이 감싸는 빈 공간을 7.12로 — 날개벽 마루
            //      (남 7.23/북 7.28)가 흙 위로 살짝 드러나고 전면벽 상단(7.5~9.0)은 그대로.
            //      페이드: 강측 2.5m(전면벽 두께 5m 안에 흡수) / 측면 5m(날개 끝을 어깨처럼
            //      감쌈) / 열린 후방 8m(둑·앞마당과 완만히 합류). 모서리는 마스크가 라운드.
            //      2층 마스크: ① 하드(7.12) — 경계는 갓돌 안쪽면 y=7.1 절단 실측치에서
            //      0.1~0.2m 안쪽, 페이드 0.35m. ⚠️ 석축은 속 빈 셸이라 "벽 두께 관입"이
            //      불가능하다: 채움 높이대 벽은 두께 0~0.6m 갓돌/수직면뿐 — 더 밀면 강 쪽으로
            //      뚫린다(2026-08-07 메시 넘침의 원인). 이 경계면 낙차 구간이 갓돌 돌출부
            //      아래(남 전면 수직면 x≈32.45 안쪽)에 숨어 틈도 유출도 없다.
            //      ② 소프트 — 날개 끝 어깨 감쌈·후방 합류용 (5/8m 페이드).
            //      crest 실측: 남 전면 32.92(중앙블록 33.99)/날개 11.83~12.03, -31.02~-31.27
            //              북 전면 z구간별 -33.84/-33.24/-34.14(평면상도 계단식)/날개 13.91, -28.35
            {
                float fill;
                if (wx > 0f)
                    fill = Mathf.Max(
                        FillMask(wx, wz, 32.80f, 46f, -31.15f, 11.80f, 0.35f, 8f, 0.35f),
                        FillMask(wx, wz, 35.5f, 46f, -31.2f, 9.8f, 2.5f, 8f, 5f));
                else
                {
                    // 전면 경계는 crest 폴리라인(사선·중앙블록 계단을 그대로 추종)
                    float hardN = FillMask(wx, wz, -46f, NorthCrestXMax(wz),
                        -28.45f, 13.85f, 8f, 0.35f, 0.35f);
                    fill = Mathf.Max(hardN, FillMask(wx, wz, -46f, -35.1f, -27.1f, 13.0f, 8f, 2.5f, 5f));
                }
                if (fill > 0f) h = Mathf.Max(h, Mathf.Lerp(h, 7.12f, fill));
            }

            // 3.8) 남 전면 물가 앞치마: 석축 앞 자갈 물가 — 물가선·양끝을 펄린 노이즈로
            //      흩뜨려 붓으로 그린 듯 불규칙하게. 폭 2~4.5m 가변, 높이 0.56~0.66.
            //      양끝은 기존 물가 사면(자갈 스플랫 h<0.7)으로 소멸해 북측 물가와 이어진다
            {
                float n1 = Mathf.PerlinNoise(wz * 0.14f + 3.3f, 7.7f);
                float n2 = Mathf.PerlinNoise(wz * 0.33f + 9.1f, 2.6f);
                float xFront = 27.1f - 2.2f * n1 - 0.9f * n2;              // 물쪽 경계 24.0~27.1
                float m = SStep(xFront, xFront + 1.7f, wx)
                        * (1f - SStep(29.9f, 30.6f, wx))                    // 벽 밑까지 (석축이 가림)
                        * SStep(-33f + 4f * n1, -27f + 2f * n2, wz)         // 남쪽 끝 소멸
                        * (1f - SStep(8f + 3f * n1, 14f + 2f * n2, wz));    // 북쪽 끝 소멸
                float apronY = 0.56f + 0.10f * Mathf.PerlinNoise(wx * 0.4f + 1.9f, wz * 0.22f + 5.5f);
                if (m > 0f) h = Mathf.Max(h, Mathf.Lerp(h, apronY, m));
            }

            // 4) 문루 앞마당 절개: 기단·월대 밑면(6.94)에 밀착하되 절개 벽은 완만한 경사로
            //    (동·서 대칭). 둑 8.3 → 마당 7.0이라 절개 깊이도 1.3m로 얕다
            {
                float m = SStep(38.5f, 44f, ax) * (1f - SStep(58.5f, 63f, ax))
                        * (1f - SStep(9f, 18f, az));
                if (m > 0f) h = Mathf.Lerp(h, ForecourtY, m);
            }

            // 4.5) 입구 통로 (+X 마을 방향): 앞마당 7.0에서 마을 쪽 2.2로 넓게 내려간다
            if (wx > 56f)
            {
                float corridorH = Mathf.Lerp(ForecourtY, 2.2f, Mathf.InverseLerp(58f, 100f, wx));
                float mask = (1f - SStep(12f, 20f, az)) * SStep(56f, 60f, wx);
                h = Mathf.Lerp(h, corridorH, mask);
            }

            // 5) 풍영정 못가 대(臺) 평탄화 (수면 아래 0.15)
            {
                float d = Vector2.Distance(new Vector2(wx, wz), new Vector2(PondPadX, PondPadZ));
                h = Mathf.Lerp(h, PondPadY, 1f - SStep(PondPadR, PondPadR + PondPadFall, d));
            }

            // 6) 교각 기초 둔덕 (2m 마진 스무스 폴오프)
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

            // 스플랫 v2: 강바닥 자갈 → 물가 흙 → 초지 풀, 급경사 암반, 통로·마당은 흙길
            int layerCount = td.terrainLayers != null ? td.terrainLayers.Length : 0;
            int gravel = 16, dirt = 0, grass = 2, path = 7, cliff = 10;
            // 자갈2_JG / 흙_낙안 / 풀_낙안 / 흙길_세연정 / 암반_세연정 (레이어 순서 바꾸면 깨짐)
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

                        // 경사 (중앙차분, 1m 간격)
                        float sx = GroundHeight(wx + 1f, wz) - GroundHeight(wx - 1f, wz);
                        float sz = GroundHeight(wx, wz + 1f) - GroundHeight(wx, wz - 1f);
                        float slopeDeg = Mathf.Atan(0.5f * Mathf.Sqrt(sx * sx + sz * sz)) * Mathf.Rad2Deg;

                        float wGravel = 1f - SStep(0.2f, 0.7f, h);
                        float wGrass = SStep(0.7f, 1.1f, h);
                        float wDirt = Mathf.Clamp01(1f - wGravel - wGrass);

                        // 문루 앞마당(동·서) 흙 광장 + 마을 방향 3m 폭 흙길
                        float wPath = SStep(41f, 44f, Mathf.Abs(wx)) * (1f - SStep(56f, 59f, Mathf.Abs(wx)))
                                    * (1f - SStep(8f, 11f, Mathf.Abs(wz)));
                        if (wx > 54f)
                            wPath = Mathf.Max(wPath, (1f - SStep(1.5f, 3.5f, Mathf.Abs(wz)))
                                  * SStep(54f, 58f, wx));

                        // 급경사 암반 (언덕 사면) — 노이즈로 끊어 띠처럼 발리지 않게
                        float cliffNoise = Mathf.PerlinNoise(wx * 0.09f + 2.2f, wz * 0.09f + 6.6f);
                        float wCliff = SStep(32f, 44f, slopeDeg)
                                     * SStep(0.35f, 0.65f, cliffNoise);

                        float sum = 0f;
                        wGravel *= 1f - wPath; wDirt *= 1f - wPath; wGrass *= 1f - wPath;
                        wGravel *= 1f - wCliff; wDirt *= 1f - wCliff; wGrass *= 1f - wCliff; wPath *= 1f - wCliff;
                        sum = wGravel + wDirt + wGrass + wPath + wCliff;
                        if (sum < 1e-4f) { wGrass = 1f; sum = 1f; }
                        alpha[z, x, gravel] = wGravel / sum;
                        alpha[z, x, dirt] = wDirt / sum;
                        alpha[z, x, grass] = wGrass / sum;
                        alpha[z, x, path] = wPath / sum;
                        alpha[z, x, cliff] = wCliff / sum;
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

        /// <summary>지형만 다시 생성 (다리·물·마커는 건드리지 않음).</summary>
        [MenuItem("Tools/이문록/은하담 지형만 재생성")]
        public static void RebuildTerrainOnly()
        {
            var scene = EnsureScene();
            BuildTerrain();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[은하담] 지형 재생성 완료");
        }

        // ── 원경 산 (지평선 가림용 배경 메시 — 외곽 언덕 대체) ──
        const string ModelFolder = "Assets/_Project/Gyeonu/Art/Models";
        const string SkirtMeshPath = ModelFolder + "/산_원경_Skirt.asset";
        const string PathMeshPath = ModelFolder + "/산_원경_길.asset";
        const string GradientTexPath = "Assets/_Project/Gyeonu/Art/Textures/산_원경_Gradient.png";
        const string MountainShaderPath = "Assets/_Project/Gyeonu/Art/Shaders/FarMountain.shader";
        const int MountainSeed = 20260807;

        /// <summary>
        /// Terrain 밖에 저폴리 능선 메시를 2겹으로 세워 지평선을 가린다.
        /// 낮 안개(60→220)가 220m 밖을 지우므로 전용 무안개 셰이더(FarMountain, _FogResist)로
        /// 산 색을 유지한다 — 덕분에 호수 주변에 NPC용 개활지를 두고 산을 멀리(230~430m) 민다.
        /// 명암은 능선 높이 그라데이션 텍스처, 실루엣은 노이즈 변형 3종으로 흩는다.
        /// 강 축(±Z)과 마을 골목(+X)은 비워 강줄기·마을 길이 이어져 보이게 한다.
        /// 지형 밖 바닥은 비운다(스커트 폐기) — 산 실루엣과 하늘 안개만 남는다.
        /// 배경 전용(콜라이더·그림자 없음). 멱등.
        /// </summary>
        [MenuItem("Tools/이문록/은하담 원경 산 생성")]
        public static void BuildFarMountains()
        {
            EnsureFolder(ModelFolder);
            EnsureFolder("Assets/_Project/Gyeonu/Art/Textures");
            var variants = new Mesh[3];
            for (int i = 0; i < 3; i++)
                variants[i] = WriteMeshAsset(ModelFolder + "/산_원경_Ridge_" + (char)('A' + i) + ".asset",
                    BuildRidgeMesh(i * 37 + 11));
            // 구 자산 정리: 단일 능선 + 스커트·원경 길(2026-08-07 폐기 — 색·질감이 지형과
            // 안 맞고 강 출구 수면을 덮었다. 지형 밖은 산 실루엣과 하늘 안개만 남긴다)
            AssetDatabase.DeleteAsset(ModelFolder + "/산_원경_Ridge.asset");
            AssetDatabase.DeleteAsset(SkirtMeshPath);
            AssetDatabase.DeleteAsset(PathMeshPath);
            AssetDatabase.DeleteAsset("Assets/_Project/Gyeonu/Art/Materials/들판_스커트.mat");
            AssetDatabase.DeleteAsset("Assets/_Project/Gyeonu/Art/Materials/길_원경.mat");

            var gradient = BakeMountainGradient();
            // 지형 풀숲과 어울리는 녹갈색은 텍스처에 굽고, 틴트로 거리감만 준다.
            // 저항값이 낮으면 밝은 낮 안개색에 씻겨 회색이 되므로 근경은 0.8까지 올린다
            var matNear = EnsureMountainMat("산_근경", Color.white, 0.8f, gradient);
            var matFar = EnsureMountainMat("산_원경", new Color(0.75f, 0.85f, 1f), 0.52f, gradient);

            var group = RecreateGroup("은하담_원경산");
            var rnd = new System.Random(MountainSeed);
            float R(System.Random r, float a, float b) => Mathf.Lerp(a, b, (float)r.NextDouble());

            // 근경 링 230~290m(진한 올리브), 원경 링 340~430m(옅은 청회 — 셰이더가 안개 저항).
            // 비움: 강 축(0°/180°) ±15°(근경만), 마을 골목(+X=90°) 근경 ±25°/원경 ±12°
            int placed = 0;
            for (int ring = 0; ring < 2; ring++)
            {
                int count = ring == 0 ? 13 : 10;
                for (int i = 0; i < count; i++)
                {
                    float ang = (360f / count) * i + (ring == 0 ? 0f : 17f) + R(rnd, -11f, 11f);
                    float rad = ring == 0 ? R(rnd, 230f, 290f) : R(rnd, 340f, 430f);
                    var scale = ring == 0
                        ? new Vector3(R(rnd, 190f, 290f), R(rnd, 34f, 52f), R(rnd, 80f, 130f))
                        : new Vector3(R(rnd, 320f, 470f), R(rnd, 60f, 95f), R(rnd, 140f, 200f));

                    float riverAxis = Mathf.Min(Mathf.Abs(Mathf.DeltaAngle(ang, 0f)),
                                                Mathf.Abs(Mathf.DeltaAngle(ang, 180f)));
                    float villageAxis = Mathf.Abs(Mathf.DeltaAngle(ang, 90f));
                    if (ring == 0 && riverAxis < 15f) continue;
                    if (villageAxis < (ring == 0 ? 25f : 12f)) continue;

                    var pos = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * rad;
                    AddBackdrop(group, (ring == 0 ? "산_근경_" : "산_원경_") + i,
                        variants[rnd.Next(3)], ring == 0 ? matNear : matFar,
                        new Vector3(pos.x, -1f, pos.z),
                        Quaternion.Euler(0f, ang + 90f + R(rnd, -20f, 20f), 0f), scale);
                    placed++;
                }
            }

            ApplyStaticFlags(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[은하담] 원경 산 생성 완료: 산 " + placed + "개 (변형 3종, 스커트·원경 길 제거됨)");
        }

        static void AddBackdrop(GameObject group, string name, Mesh mesh, Material mat,
            Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(group.transform, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// 단위 능선 메시: XZ [-0.5,0.5]² 격자, 길이축 X, uv.y = 높이(명암 그라데이션용).
        /// 노이즈 2옥타브로 실루엣을 흩는다. 시드별 변형. 약 230 삼각형.
        /// </summary>
        static Mesh BuildRidgeMesh(int seed)
        {
            const int nx = 20, nz = 7;
            float o1 = seed * 0.731f, o2 = seed * 0.389f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    float u = x / (float)nx, v = z / (float)nz;
                    // sin(π)이 float에서 음수 엡실론이라 Pow가 NaN — Max(0,·)로 방어
                    float envL = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)), 0.75f);
                    float envW = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(v * Mathf.PI)), 1.1f);
                    float n = 0.45f + 0.62f * Mathf.PerlinNoise(u * 4.3f + o1, v * 2.9f + o2)
                            + 0.28f * Mathf.PerlinNoise(u * 9.7f + o2, v * 6.1f + o1);
                    float y = envL * envW * n;
                    verts.Add(new Vector3(u - 0.5f, y, v - 0.5f));
                    uvs.Add(new Vector2(u, Mathf.Clamp01(y / 1.1f)));
                }
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int i = z * (nx + 1) + x;
                    tris.AddRange(new[] { i, i + nx + 1, i + 1, i + 1, i + nx + 1, i + nx + 2 });
                }
            var m = new Mesh { name = "산_원경_Ridge" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// 능선 명암 텍스처: 지형 풀숲과 어울리는 녹갈색 — 아래 짙은 숲색, 위로 갈수록
        /// 밝은 풀색. 가로 줄노이즈로 숲 질감. (회색 단색으로 보이던 문제의 교정:
        /// 색을 틴트가 아니라 텍스처에 직접 굽는다 — 밉맵 평균도 녹갈색을 유지)
        /// </summary>
        static Texture2D BakeMountainGradient()
        {
            const int size = 256;
            var colLow = new Color(0.09f, 0.13f, 0.055f);   // 골짜기 짙은 숲
            var colMid = new Color(0.19f, 0.26f, 0.115f);   // 중턱 숲
            var colHigh = new Color(0.38f, 0.42f, 0.22f);   // 능선 마른 풀
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                var baseC = v < 0.55f
                    ? Color.Lerp(colLow, colMid, v / 0.55f)
                    : Color.Lerp(colMid, colHigh, (v - 0.55f) / 0.45f);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float streak = Mathf.PerlinNoise(u * 9f, v * 26f) - 0.5f;   // 숲 능선 줄무늬
                    float grain = Mathf.PerlinNoise(u * 40f + 9f, v * 40f + 3f) - 0.5f;
                    float l = 1f + streak * 0.30f + grain * 0.14f;
                    px[y * size + x] = new Color(
                        Mathf.Clamp01(baseC.r * l), Mathf.Clamp01(baseC.g * l),
                        Mathf.Clamp01(baseC.b * l), 1f);
                }
            }
            tex.SetPixels(px);
            System.IO.File.WriteAllBytes(GradientTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(GradientTexPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(GradientTexPath);
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 256;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GradientTexPath);
        }

        static Material EnsureMountainMat(string name, Color color, float fogResist, Texture2D gradient)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(MountainShaderPath);
            if (shader == null)
            {
                Debug.LogError("[은하담] FarMountain.shader 없음 — URP Lit로 대체");
                return EnsureFlatMat(name, color);
            }
            string path = "Assets/_Project/Gyeonu/Art/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", gradient);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_FogResist", fogResist);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>메시 에셋을 GUID 유지한 채 갱신 저장.</summary>
        static Mesh WriteMeshAsset(string path, Mesh built)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            existing.Clear();
            existing.SetVertices(new List<Vector3>(built.vertices));
            existing.SetUVs(0, new List<Vector2>(built.uv));
            existing.subMeshCount = built.subMeshCount;
            for (int i = 0; i < built.subMeshCount; i++)
                existing.SetTriangles(built.GetTriangles(i), i);
            existing.RecalculateNormals();
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        static Material EnsureFlatMat(string name, Color color)
        {
            string path = "Assets/_Project/Gyeonu/Art/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
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
        // 2026-08-07 정리: 스폰 = 씬 진입 지점(구 지형 +X 끝, x 102) — 랜드마크 버드나무(91,9)
        // 가지가 프레임이 되는 시점. 높이는 실제 Terrain 샘플(스커트 포함 — GroundHeight는 ±100 밖 무효).
        [MenuItem("Tools/이문록/은하담 마커 배치")]
        public static void BuildMarkers()
        {
            var group = RecreateGroup("은하담_마커");

            var spawn = Marker(group, "SpawnPoint_PlayerStart", 102f, 0f);
            spawn.transform.rotation = Quaternion.Euler(0f, 270f, 0f);   // 다리(-X)를 바라봄 — 버드나무 프레임

            Marker(group, "Exit_ToVillage", 136f, 0f);                   // 마을 방향 +X 끝 (씬 경계 ±140 직전)
            Marker(group, "Exit_ToHub", 102f, -7f);                      // 조사청 복귀 (진입 지점 옆)

            // 관측실 입구(오작교 암문 예정지) — 다리 마루 북측 난간 앞. 암문 구조물은 추후.
            var obs = new GameObject("Exit_ToObservatory");
            obs.transform.SetParent(group.transform, false);
            obs.transform.position = new Vector3(0f, 8.8f, 4.2f);        // 마루 실측 8.8
            obs.transform.rotation = Quaternion.Euler(0f, 0f, 0f);       // 난간(+Z) 방향

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[은하담] 마커 4종 배치 완료 (스폰 x102·출구 x136·허브·관측실)");
        }

        static GameObject Marker(GameObject group, string name, float x, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(group.transform, false);
            go.transform.position = new Vector3(x, SampleTerrainH(x, z), z);
            return go;
        }

        static float SampleTerrainH(float x, float z)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var p = t.transform.position; var s = t.terrainData.size;
                if (x >= p.x && x <= p.x + s.x && z >= p.z && z <= p.z + s.z)
                    return t.SampleHeight(new Vector3(x, 0f, z)) + p.y;
            }
            return GroundHeight(Mathf.Clamp(x, -99f, 99f), Mathf.Clamp(z, -99f, 99f));
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
