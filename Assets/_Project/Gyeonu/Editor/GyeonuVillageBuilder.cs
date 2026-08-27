using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 견우마을(Gyeonu_GyeonuVillage) 씬 조립기. 멱등 — 다시 실행하면 관리 그룹을 지우고 재생성한다.
    ///
    /// 공간 연출: 남쪽 끝 숲길 입구 → 고지대(6.8m)를 가르는 좁은 협곡길이 S자로 굽어
    /// 시야를 막다가 → 서남 모퉁이(-13,-5)를 돌면 연못·은둔처가 한눈에 트인다.
    /// 모퉁이 앞 둔덕(0,-2, +5.5m)이 협곡 중간에서 연못이 미리 보이는 것을 막는다.
    ///
    /// 연못은 세연정 데모 실측을 축소 반영: 넓은 못(원 4개 메타볼, 약 40×25m) +
    /// 북동으로 굽이치는 좁은 입수 계류(캡슐 3개) + 서남 출수 계류(캡슐 1개).
    /// 물가는 완사면(세연정은 석축이지만 석축 소품은 식생 단계에서), 수위 0.5.
    /// 은둔처 대(臺)는 연못 북안 물가(4,35) — 세연정 정자처럼 물에 반쯤 걸친 축대 느낌.
    ///
    /// 지형 좌표계: 씬 중앙 원점, 지형 100×100 (±50). 스커트 8타일로 300×300 확장.
    /// 외곽(r 38~50)은 5~10m 상승 + 원경 산 링(은하담 에셋 재사용)으로 지평선을 가린다.
    /// </summary>
    public static class GyeonuVillageBuilder
    {
        const string ScenePath = "Assets/_Project/Gyeonu/Scenes/Gyeonu_GyeonuVillage.unity";
        const string TerrainDataPath = "Assets/_Project/Gyeonu/Art/Terrain/GyeonuVillage_TerrainData.asset";
        const string VillageTerrainDataPath = "Assets/_Project/Gyeonu/Art/Terrain/Gyeonu_TerrainData.asset";
        const string DataDir = "Assets/_Project/Gyeonu/Art/Terrain";

        const string WaterTemplatePath = "Assets/Shaders/Uber Stylized Water/Template Materials/UWa-Template-Clear.mat";
        const string WaterMatPath = "Assets/_Project/Gyeonu/Art/Materials/Water_GyeonuVillage_Stylized.mat";
        const string WaterPresetPath = "Assets/_Project/Gyeonu/Art/Water/물_맑음.asset";
        const string SkyPresetPath = "Assets/_Project/Gyeonu/Art/Lighting/SkyPreset_밤_맑음.asset";
        const string ModelFolder = "Assets/_Project/Gyeonu/Art/Models";

        // ── 물·지형 설계 상수 ────────────────────────────────
        public const float WaterLevel = 0.5f;
        const float BedY = -1.0f;
        const float LowlandY = 1.3f;      // 연못가 저지대
        const float HighlandY = 6.8f;     // 숲길이 뚫고 가는 고지대
        const float TerrainBaseY = -10f;  // 터레인 GO의 y (heights 0~1 오프셋)
        const float Half = 50f;           // 지형 반폭

        // 은둔처 대(臺): 연못 북안 — 남단이 물에 살짝 잠기며 자연스런 물가 축대가 된다
        public const float PadX = 4f, PadZ = 35f, PadY = 1.7f;
        const float PadR = 5f, PadFall = 4f;

        // 시야 차단 둔덕: 협곡 중간~출구에서 연못·은둔처가 미리 보이지 않게
        const float MoundX = 0f, MoundZ = -2f, MoundH = 5.5f, MoundR = 4f, MoundFall = 5f;

        // ── 연못 형태 (세연정 축소·재구성): 원 메타볼 + 캡슐 계류 ──
        // {cx, cz, r}
        static readonly float[][] PondCircles =
        {
            new[] { -6f, 14f, 9f },
            new[] { 5f, 19f, 10f },
            new[] { 15f, 13f, 7f },
            new[] { -15f, 20f, 5.5f },   // 서쪽 볼록 — 물가선 불규칙화
        };
        // {x1, z1, x2, z2, r} — 북동 입수 계류(S자), 서남 출수 계류
        static readonly float[][] PondCapsules =
        {
            new[] { 22f, 18f, 29f, 27f, 3.5f },
            new[] { 29f, 27f, 25f, 35f, 3.0f },
            new[] { 25f, 35f, 31f, 43f, 2.6f },
            new[] { -13f, 11f, -21f, 5f, 2.8f },
        };

        // ── 숲길 폴리라인 (남쪽 입구 → 연못 서남 어귀) ──
        static readonly Vector2[] PathPts =
        {
            new Vector2(2f, -51f),    // 지형 밖까지 연장 — 가장자리 행 압출로 스커트에 이어짐
            new Vector2(3f, -42f),
            new Vector2(11f, -34f),
            new Vector2(12f, -25f),
            new Vector2(3f, -17f),
            new Vector2(-7f, -12f),
            new Vector2(-13f, -5f),   // 모퉁이 — 여기를 돌면 연못이 트인다
            new Vector2(-11f, 1f),
        };
        // 길바닥 높이 프로필 (t = 호길이 정규화)
        static readonly float[][] PathProfile =
        {
            new[] { 0f, 2.4f }, new[] { 0.45f, 3.3f }, new[] { 0.8f, 2.2f }, new[] { 1f, 1.45f },
        };
        static float[] _pathCum;   // 누적 호길이 캐시

        [MenuItem("Tools/이문록/견우마을 조립 (전체)")]
        public static void BuildAll()
        {
            var scene = EnsureScene();
            EnsureWaterAssets();
            BuildTerrain();
            BuildSkirt();
            BuildFarMountains();
            BuildWater();
            BuildLighting();          // 물 프리셋 연동이 있어 물 다음에 적용
            BuildMarkers();
            EnsureGroup("견우마을_건물");
            EnsureGroup("견우마을_식생");
            EnsureCamera();
            EunhaDamWalkSetup.InstallWalker();   // 디버그 워커 (SpawnPoint_PlayerStart 사용)

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[견우마을] 조립 완료: " + ScenePath);
        }

        /// <summary>지형만 다시 생성 (물·마커 등은 건드리지 않음).</summary>
        [MenuItem("Tools/이문록/견우마을 지형만 재생성")]
        public static void RebuildTerrainOnly()
        {
            var scene = EnsureScene();
            BuildTerrain();
            BuildSkirt();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[견우마을] 지형 재생성 완료");
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

        // ── 높이 함수 ────────────────────────────────────────
        static float SStep(float e0, float e1, float x) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(e0, e1, x));

        /// <summary>연못 부호 거리 — 음수면 물 안. 원·캡슐 유니온.</summary>
        public static float PondSignedDist(float wx, float wz)
        {
            float sd = float.MaxValue;
            foreach (var c in PondCircles)
            {
                float d = Mathf.Sqrt((wx - c[0]) * (wx - c[0]) + (wz - c[1]) * (wz - c[1])) - c[2];
                if (d < sd) sd = d;
            }
            foreach (var c in PondCapsules)
            {
                float d = DistToSegment(wx, wz, c[0], c[1], c[2], c[3]) - c[4];
                if (d < sd) sd = d;
            }
            return sd;
        }

        static float DistToSegment(float px, float pz, float ax, float az, float bx, float bz)
        {
            float abx = bx - ax, abz = bz - az;
            float t = Mathf.Clamp01(((px - ax) * abx + (pz - az) * abz) / (abx * abx + abz * abz));
            float dx = px - (ax + abx * t), dz = pz - (az + abz * t);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>숲길 폴리라인까지 거리와 호길이 위치(t 0~1).</summary>
        static float PathDist(float wx, float wz, out float tOut)
        {
            if (_pathCum == null || _pathCum.Length != PathPts.Length)
            {
                _pathCum = new float[PathPts.Length];
                for (int i = 1; i < PathPts.Length; i++)
                    _pathCum[i] = _pathCum[i - 1] + Vector2.Distance(PathPts[i - 1], PathPts[i]);
            }
            float total = _pathCum[_pathCum.Length - 1];
            float best = float.MaxValue; tOut = 0f;
            for (int i = 1; i < PathPts.Length; i++)
            {
                Vector2 a = PathPts[i - 1], b = PathPts[i];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(wx, wz) - a, ab) / ab.sqrMagnitude);
                float d = Vector2.Distance(new Vector2(wx, wz), a + ab * t);
                if (d < best)
                {
                    best = d;
                    tOut = (_pathCum[i - 1] + ab.magnitude * t) / total;
                }
            }
            return best;
        }

        static float PathFloorH(float t)
        {
            var k = PathProfile;
            if (t <= k[0][0]) return k[0][1];
            for (int i = 1; i < k.Length; i++)
                if (t <= k[i][0])
                    return Mathf.Lerp(k[i - 1][1], k[i][1], Mathf.InverseLerp(k[i - 1][0], k[i][0], t));
            return k[k.Length - 1][1];
        }

        public static float GroundHeight(float wx, float wz)
        {
            float sd = PondSignedDist(wx, wz);

            // 1) 기저: 연못가 저지대 → 멀어질수록 숲 고지대 (연못 분지)
            float n1 = Mathf.PerlinNoise(wx * 0.05f + 3.1f, wz * 0.05f + 8.7f) - 0.5f;
            float n2 = Mathf.PerlinNoise(wx * 0.035f + 6.4f, wz * 0.035f + 1.9f) - 0.5f;
            float lowland = LowlandY + n1 * 0.5f;
            float highland = HighlandY + n2 * 3.2f;
            float h = Mathf.Lerp(lowland, highland, SStep(8f, 18f, sd));

            // 2) 시야 차단 둔덕 (협곡 중간에서 연못이 미리 안 보이게)
            {
                float d = Vector2.Distance(new Vector2(wx, wz), new Vector2(MoundX, MoundZ));
                h += MoundH * (1f - SStep(MoundR, MoundR + MoundFall, d));
            }

            // 3) 외곽 상승 (r 38~50에서 5~10m) — 지평선 가림 1차
            {
                float r = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz));
                float n3 = Mathf.PerlinNoise(wx * 0.03f + 9.2f, wz * 0.03f + 4.4f);
                h += SStep(38f, 50f, r) * (5f + 5f * n3);
            }

            // 4) 숲길 협곡 절개 — 바닥 반폭 1.8m, 7m에 걸쳐 벽 복귀 (약 43도 벽)
            {
                float t;
                float pd = PathDist(wx, wz, out t);
                if (pd < 7f)
                {
                    float m = 1f - SStep(1.8f, 7f, pd);
                    h = Mathf.Lerp(h, PathFloorH(t), m);
                }
            }

            // 5) 연못 절개 — sd -4(바닥) → +3(물가 위). 수위 0.5는 sd≈0.4에서 교차
            {
                float m = 1f - SStep(2.5f, 6f, sd);
                if (m > 0f)
                {
                    float pondH = Mathf.Lerp(BedY, LowlandY + 0.1f, SStep(-4f, 3f, sd));
                    h = Mathf.Lerp(h, pondH, m);
                }
            }

            // 6) 은둔처 대(臺) 평탄화 — 남단이 물가에 걸친다 (마지막: 연못보다 우선)
            {
                float d = Vector2.Distance(new Vector2(wx, wz), new Vector2(PadX, PadZ));
                h = Mathf.Lerp(h, PadY, 1f - SStep(PadR, PadR + PadFall, d));
            }
            return h;
        }

        // ── 터레인 ───────────────────────────────────────────
        const int HRes = 257;   // 100m/257px ≈ 0.39m/px — 은하담과 동일 격자 밀도
        const int ARes = 256;

        static void BuildTerrain()
        {
            var villageTd = AssetDatabase.LoadAssetAtPath<TerrainData>(VillageTerrainDataPath);

            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, TerrainDataPath);
            }

            td.heightmapResolution = HRes;
            td.size = new Vector3(2f * Half, 100f, 2f * Half);
            td.alphamapResolution = ARes;
            td.baseMapResolution = 512;
            if (villageTd != null) td.terrainLayers = villageTd.terrainLayers;

            var heights = new float[HRes, HRes];
            for (int z = 0; z < HRes; z++)
            {
                float wz = z / (float)(HRes - 1) * 2f * Half - Half;
                for (int x = 0; x < HRes; x++)
                {
                    float wx = x / (float)(HRes - 1) * 2f * Half - Half;
                    heights[z, x] = (GroundHeight(wx, wz) - TerrainBaseY) / 100f;
                }
            }
            td.SetHeights(0, 0, heights);

            // 스플랫: 못바닥 자갈 → 물가 흙 → 숲바닥 풀, 협곡 벽 암반, 숲길 흙길
            int layerCount = td.terrainLayers != null ? td.terrainLayers.Length : 0;
            int gravel = 16, dirt = 0, grass = 2, path = 7, cliff = 10;
            if (layerCount > gravel)
            {
                var alpha = new float[ARes, ARes, layerCount];
                for (int z = 0; z < ARes; z++)
                {
                    float wz = z / (float)(ARes - 1) * 2f * Half - Half;
                    for (int x = 0; x < ARes; x++)
                    {
                        float wx = x / (float)(ARes - 1) * 2f * Half - Half;
                        float h = GroundHeight(wx, wz);

                        float sx = GroundHeight(wx + 1f, wz) - GroundHeight(wx - 1f, wz);
                        float sz = GroundHeight(wx, wz + 1f) - GroundHeight(wx, wz - 1f);
                        float slopeDeg = Mathf.Atan(0.5f * Mathf.Sqrt(sx * sx + sz * sz)) * Mathf.Rad2Deg;

                        float wGravel = 1f - SStep(0.2f, 0.7f, h);
                        float wGrass = SStep(0.7f, 1.1f, h);
                        float wDirt = Mathf.Clamp01(1f - wGravel - wGrass);

                        float t;
                        float pd = PathDist(wx, wz, out t);
                        float wPath = 1f - SStep(1.4f, 3.2f, pd);

                        float cliffNoise = Mathf.PerlinNoise(wx * 0.09f + 2.2f, wz * 0.09f + 6.6f);
                        float wCliff = SStep(32f, 44f, slopeDeg) * SStep(0.35f, 0.65f, cliffNoise);

                        wGravel *= 1f - wPath; wDirt *= 1f - wPath; wGrass *= 1f - wPath;
                        wGravel *= 1f - wCliff; wDirt *= 1f - wCliff; wGrass *= 1f - wCliff; wPath *= 1f - wCliff;
                        float sum = wGravel + wDirt + wGrass + wPath + wCliff;
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

            var group = RecreateGroup("견우마을_지형");
            var tgo = Terrain.CreateTerrainGameObject(td);
            tgo.name = "Terrain_GyeonuVillage";
            tgo.transform.SetParent(group.transform, false);
            tgo.transform.position = new Vector3(-Half, TerrainBaseY, -Half);

            var terrain = tgo.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 25f;
            terrain.basemapDistance = 300f;
        }

        // ── 스커트 8타일 (은하담 방식 — 가장자리 압출 + 펄린 기복) ──
        const float NoiseAmp = 0.35f;
        const float NoiseFreq = 1f / 23f;
        const float NoiseFadeDist = 35f;

        static void BuildSkirt()
        {
            var mainGo = GameObject.Find("견우마을_지형/Terrain_GyeonuVillage");
            if (mainGo == null) { Debug.LogError("[견우마을] 중앙 지형 없음 — 스커트 생략"); return; }
            var main = mainGo.GetComponent<Terrain>();
            var mainTd = main.terrainData;
            Vector3 mainPos = main.transform.position;
            Vector3 mainSize = mainTd.size;
            float tile = mainSize.x;
            int mRes = mainTd.heightmapResolution;
            int mARes = mainTd.alphamapResolution;
            int layerCount = mainTd.terrainLayers.Length;
            float[,] mainH = mainTd.GetHeights(0, 0, mRes, mRes);
            float[,,] mainA = mainTd.GetAlphamaps(0, 0, mARes, mARes);

            var terrainRoot = mainGo.transform.parent;
            var oldGroup = terrainRoot.Find("지형스커트");
            if (oldGroup != null) Object.DestroyImmediate(oldGroup.gameObject);
            var group = new GameObject("지형스커트").transform;
            group.SetParent(terrainRoot, false);

            var skirts = new Terrain[3, 3];
            skirts[1, 1] = main;
            float step = tile / (mRes - 1);

            for (int di = -1; di <= 1; di++)
            for (int dj = -1; dj <= 1; dj++)
            {
                if (di == 0 && dj == 0) continue;
                string suffix = (dj > 0 ? "N" : dj < 0 ? "S" : "") + (di > 0 ? "E" : di < 0 ? "W" : "");
                Vector3 tilePos = mainPos + new Vector3(di * tile, 0f, dj * tile);

                string dataPath = DataDir + "/GyeonuVillage_Skirt_" + suffix + ".asset";
                var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
                if (td == null)
                {
                    // ⚠️ CreateAsset은 데이터 기록 전에 (미저장 TerrainData에 SetAlphamaps 후
                    // CreateAsset하면 알파맵이 직렬화에서 초기화된다)
                    td = new TerrainData();
                    AssetDatabase.CreateAsset(td, dataPath);
                }
                td.heightmapResolution = mRes;
                td.alphamapResolution = 128;    // 원거리 전용
                td.baseMapResolution = 256;
                td.size = new Vector3(tile, mainSize.y, tile);
                td.terrainLayers = mainTd.terrainLayers;

                // 높이: 가장자리 최근접 압출 + 경계 밖 페이드인 기복
                var h = new float[mRes, mRes];
                for (int z = 0; z < mRes; z++)
                {
                    float wz = tilePos.z + z * step;
                    int mz = Mathf.Clamp(Mathf.RoundToInt((wz - mainPos.z) / step), 0, mRes - 1);
                    float overZ = Mathf.Max(0f, Mathf.Max(mainPos.z - wz, wz - (mainPos.z + mainSize.z)));
                    for (int x = 0; x < mRes; x++)
                    {
                        float wx = tilePos.x + x * step;
                        int mx = Mathf.Clamp(Mathf.RoundToInt((wx - mainPos.x) / step), 0, mRes - 1);
                        float worldH = mainH[mz, mx] * mainSize.y + mainPos.y;
                        float overX = Mathf.Max(0f, Mathf.Max(mainPos.x - wx, wx - (mainPos.x + mainSize.x)));
                        float outside = Mathf.Max(overX, overZ);
                        if (worldH > 1.0f && outside > 0f)
                        {
                            float fade = SStep(0f, 1f, outside / NoiseFadeDist);
                            worldH += (Mathf.PerlinNoise(wx * NoiseFreq + 7.31f, wz * NoiseFreq + 2.17f) - 0.5f)
                                    * 2f * NoiseAmp * fade;
                        }
                        h[z, x] = Mathf.Clamp01((worldH - mainPos.y) / mainSize.y);
                    }
                }
                td.SetHeights(0, 0, h);

                // 스플랫: 가장자리 최근접 압출
                int aRes = td.alphamapResolution;
                float cell = tile / aRes, mainCell = tile / mARes;
                var a = new float[aRes, aRes, layerCount];
                for (int z = 0; z < aRes; z++)
                {
                    float wz = tilePos.z + (z + 0.5f) * cell;
                    int mz = Mathf.Clamp(Mathf.FloorToInt((wz - mainPos.z) / mainCell), 0, mARes - 1);
                    for (int x = 0; x < aRes; x++)
                    {
                        float wx = tilePos.x + (x + 0.5f) * cell;
                        int mx = Mathf.Clamp(Mathf.FloorToInt((wx - mainPos.x) / mainCell), 0, mARes - 1);
                        for (int L = 0; L < layerCount; L++)
                            a[z, x, L] = mainA[mz, mx, L];
                    }
                }
                td.SetAlphamaps(0, 0, a);
                EditorUtility.SetDirty(td);

                var go = Terrain.CreateTerrainGameObject(td);
                go.name = "Terrain_Skirt_" + suffix;
                go.transform.SetParent(group, false);
                go.transform.position = tilePos;

                var t = go.GetComponent<Terrain>();
                t.materialTemplate = main.materialTemplate;
                t.heightmapPixelError = main.heightmapPixelError;
                t.basemapDistance = main.basemapDistance;
                t.drawInstanced = main.drawInstanced;
                t.groupingID = main.groupingID;
                t.allowAutoConnect = true;
                t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t.drawTreesAndFoliage = false;
                skirts[di + 1, dj + 1] = t;
            }

            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                var t = skirts[i, j];
                if (t == null) continue;
                t.SetNeighbors(
                    i > 0 ? skirts[i - 1, j] : null,
                    j < 2 ? skirts[i, j + 1] : null,
                    i < 2 ? skirts[i + 1, j] : null,
                    j > 0 ? skirts[i, j - 1] : null);
            }
            Terrain.SetConnectivityDirty();
            AssetDatabase.SaveAssets();
        }

        // ── 원경 산 (은하담 능선 메시·머티리얼 재사용) ──
        const int MountainSeed = 20260809;

        static void BuildFarMountains()
        {
            var variants = new[]
            {
                AssetDatabase.LoadAssetAtPath<Mesh>(ModelFolder + "/산_원경_Ridge_A.asset"),
                AssetDatabase.LoadAssetAtPath<Mesh>(ModelFolder + "/산_원경_Ridge_B.asset"),
                AssetDatabase.LoadAssetAtPath<Mesh>(ModelFolder + "/산_원경_Ridge_C.asset"),
            };
            var matNear = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Gyeonu/Art/Materials/산_근경.mat");
            var matFar = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Gyeonu/Art/Materials/산_원경.mat");
            if (variants.Any(v => v == null) || matNear == null || matFar == null)
            {
                Debug.LogWarning("[견우마을] 원경 산 에셋 없음 — 은하담 조립을 먼저 실행하면 생성됨. 건너뜀");
                return;
            }

            var parent = EnsureGroup("견우마을_지형");
            var old = parent.transform.Find("원경산");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("원경산");
            group.transform.SetParent(parent.transform, false);

            var rnd = new System.Random(MountainSeed);
            float R(float a, float b) => Mathf.Lerp(a, b, (float)rnd.NextDouble());

            // 씬이 은하담의 절반이라 링도 절반 거리: 근경 130~165, 원경 190~250.
            // 밤 안개(35→170)라 원경 링은 실루엣만 — FarMountain 셰이더가 안개 저항.
            int placed = 0;
            for (int ring = 0; ring < 2; ring++)
            {
                int count = ring == 0 ? 11 : 8;
                for (int i = 0; i < count; i++)
                {
                    float ang = (360f / count) * i + (ring == 0 ? 0f : 21f) + R(-12f, 12f);
                    float rad = ring == 0 ? R(130f, 165f) : R(190f, 250f);
                    var scale = ring == 0
                        ? new Vector3(R(120f, 190f), R(24f, 38f), R(55f, 90f))
                        : new Vector3(R(220f, 330f), R(45f, 70f), R(100f, 150f));

                    var pos = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * rad;
                    var go = new GameObject((ring == 0 ? "산_근경_" : "산_원경_") + i,
                        typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(group.transform, false);
                    go.transform.SetPositionAndRotation(new Vector3(pos.x, -1f, pos.z),
                        Quaternion.Euler(0f, ang + 90f + R(-20f, 20f), 0f));
                    go.transform.localScale = scale;
                    go.GetComponent<MeshFilter>().sharedMesh = variants[rnd.Next(3)];
                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = ring == 0 ? matNear : matFar;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    placed++;
                }
            }
            Debug.Log("[견우마을] 원경 산 " + placed + "개 배치");
        }

        // ── 물 ──────────────────────────────────────────────
        static void EnsureWaterAssets()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            if (mat == null)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(WaterTemplatePath) == null)
                {
                    Debug.LogError("[견우마을] Uber Stylized Water 팩이 없습니다. 임포트 후 다시 실행하세요.");
                    return;
                }
                AssetDatabase.CopyAsset(WaterTemplatePath, WaterMatPath);
                mat = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            }
            // 은하담과 동일한 기능 구성 — 고요한 밤 못
            SetKeyword(mat, "_ENABLEPLANERREFLECTION", false);
            SetKeyword(mat, "_ENABLENORMAL", true);
            SetKeyword(mat, "_ENABLEREFRACTION", true);
            SetKeyword(mat, "_ENABLECAUSTICS", true);
            SetKeyword(mat, "_ENABLEWAVE", false);
            SetKeyword(mat, "_ENABLESHORELINE", false);
            SetKeyword(mat, "_ENABLE_UNDERWATERLAYER", false);
            mat.SetVector("_SurfaceDistortion_Pan", new Vector4(0f, 0.18f, 0f, 0f));
            mat.SetFloat("_SurfaceDistortion_Strength", 0.12f);
            mat.SetVector("_SurfFoam_Pan", new Vector4(0f, 0.1f, 0f, 0f));
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }

        static void SetKeyword(Material mat, string keyword, bool on)
        {
            if (mat.HasFloat(keyword)) mat.SetFloat(keyword, on ? 1f : 0f);
            if (on) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        static void BuildWater()
        {
            var group = RecreateGroup("견우마을_물");
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "수면";
            var col = plane.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            plane.transform.SetParent(group.transform, false);
            // 연못 전역(x -30~40, z -2~50) 덮기 — 물가선은 지형이 만든다
            plane.transform.position = new Vector3(5f, WaterLevel, 24f);
            plane.transform.localScale = new Vector3(7.0f, 1f, 5.2f);

            var rend = plane.GetComponent<Renderer>();
            rend.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var surface = plane.AddComponent<WaterSurface>();
            surface.preset = AssetDatabase.LoadAssetAtPath<WaterPreset>(WaterPresetPath);
            if (surface.preset != null)
                surface.preset.ApplyTo(rend.sharedMaterial);
        }

        // ── 조명 (밤_맑음) ───────────────────────────────────
        static void BuildLighting()
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
            else Debug.LogWarning("[견우마을] SkyPreset_밤_맑음 없음 — 조명 프리셋 건너뜀");
        }

        // ── 마커·카메라 ─────────────────────────────────────
        static void BuildMarkers()
        {
            var group = RecreateGroup("견우마을_마커");

            var spawn = Marker(group, "SpawnPoint_PlayerStart", 2f, -48f);
            spawn.transform.rotation = Quaternion.Euler(0f, 9f, 0f);    // 숲길 방향(북북동)

            Marker(group, "Exit_ToEunhaDam", 2f, -49.5f);               // 되돌아가는 남쪽 경계
            Marker(group, "Marker_은둔처터", PadX, PadZ);                // 건물 단계에서 거처 배치 기준

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[견우마을] 마커 3종 배치 (스폰·은하담 출구·은둔처터)");
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
            return GroundHeight(Mathf.Clamp(x, -49f, 49f), Mathf.Clamp(z, -49f, 49f));
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
            camGo.transform.position = new Vector3(-30f, 12f, -20f);
            camGo.transform.LookAt(new Vector3(PadX, 2f, PadZ));
        }
    }
}
