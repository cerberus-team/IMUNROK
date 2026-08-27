using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static IMUNROK.Gyeonu.Editor.GwanaLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 동헌 외부(Gyeonu_Gwana) 씬 조립기. 멱등 — 다시 실행하면 관리 그룹을 지우고 재생성한다.
    ///
    /// 공간 연출 (진입축 = +Z 일직선):
    ///   스폰(z −48, 마당보다 1.7m 낮은 언덕길) → 짧은 오르막 14m → 외삼문(z −34)
    ///   → 문을 지나면 34×38m 마당이 한 번에 열리고 → 어도(박석)가 눈을 월대로 끌고
    ///   → 3단 석축 월대(상면 +1.2) 위 동헌(마루 +3.4, 용마루 +9.1)이 정면에 선다.
    ///
    /// 스폰에서는 담장(3.1m)이 마당을 가리고 동헌 지붕만 담 위로 걸친다 — 문을 지나야
    /// 전모가 드러나게 계산한 높이다.
    ///
    /// 배경: 북쪽 뒷산이 대지 바로 뒤(z 36~)에서 최고 48m까지 솟아 동헌을 받치고,
    /// 좌우는 능선(|x| 54~)이, 그 밖은 은하담 원경 산 링이 지평선을 막는다.
    /// 남쪽은 마을에서 올라온 절벽이라 뒤돌아보면 아래가 트인다.
    /// </summary>
    public static class GwanaBuilder
    {
        const string ScenePath = "Assets/_Project/Gyeonu/Scenes/Gyeonu_Gwana.unity";
        const string TerrainDataPath = "Assets/_Project/Gyeonu/Art/Terrain/Gwana_TerrainData.asset";
        const string VillageTerrainDataPath = "Assets/_Project/Gyeonu/Art/Terrain/Gyeonu_TerrainData.asset";
        const string DataDir = "Assets/_Project/Gyeonu/Art/Terrain";
        const string SkyPresetPath = "Assets/_Project/Gyeonu/Art/Lighting/SkyPreset_낮_맑음.asset";
        const string ModelFolder = "Assets/_Project/Gyeonu/Art/Models";

        const int HRes = 513;    // 200m / 512칸 = 0.39m — 은하담·마을과 동일 격자 밀도
        const int ARes = 512;

        // 걷힌 뒤 최종 안개 = 성하리 마을(Gyeonu.unity) 실측값 (Linear, 파티클·Volume 없음)
        const float FogStartEnd = 40f, FogEndEnd = 160f;
        static readonly Color FogColorEnd = new Color(0.76f, 0.79f, 0.81f);

        [MenuItem("Tools/이문록/관아 ▸ ② 씬 조립 (전체)")]
        public static void BuildAll()
        {
            var scene = EnsureScene();
            BuildTerrain();
            BuildSkirt();
            BuildFarMountains();
            BuildLighting();
            BuildStructures();
            BuildMarkers();
            BuildIntroFog();
            EnsureCamera();
            GwanaWalkSetup.BuildColliders();
            GwanaWalkSetup.InstallWalker();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[관아] 조립 완료: " + ScenePath + " (Build Settings 미등록 — 의도된 상태)");
        }

        [MenuItem("Tools/이문록/관아 ▸ 지형만 재생성")]
        public static void RebuildTerrainOnly()
        {
            var scene = EnsureScene();
            BuildTerrain();
            BuildSkirt();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[관아] 지형 재생성 완료");
        }

        [MenuItem("Tools/이문록/관아 ▸ 구조물만 재생성")]
        public static void RebuildStructuresOnly()
        {
            var scene = EnsureScene();
            BuildStructures();
            GwanaWalkSetup.BuildColliders();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[관아] 구조물 재생성 완료");
        }

        // ── 씬 ───────────────────────────────────────────────
        static Scene EnsureScene()
        {
            Scene scene;
            if (System.IO.File.Exists(ScenePath))
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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

        static GameObject FindRoot(string name) =>
            SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);

        static GameObject RecreateGroup(string name)
        {
            var old = FindRoot(name);
            if (old != null) Object.DestroyImmediate(old);
            return new GameObject(name);
        }

        // ── 지형 ─────────────────────────────────────────────
        static void BuildTerrain()
        {
            var villageTd = AssetDatabase.LoadAssetAtPath<TerrainData>(VillageTerrainDataPath);
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (td == null) { td = new TerrainData(); AssetDatabase.CreateAsset(td, TerrainDataPath); }

            td.heightmapResolution = HRes;
            td.size = new Vector3(2f * Half, TerrainHeight, 2f * Half);
            td.alphamapResolution = ARes;
            td.baseMapResolution = 1024;
            if (villageTd != null) td.terrainLayers = villageTd.terrainLayers;

            var heights = new float[HRes, HRes];
            for (int z = 0; z < HRes; z++)
            {
                float wz = z / (float)(HRes - 1) * 2f * Half - Half;
                for (int x = 0; x < HRes; x++)
                {
                    float wx = x / (float)(HRes - 1) * 2f * Half - Half;
                    heights[z, x] = Mathf.Clamp01((GroundHeight(wx, wz) - TerrainBaseY) / TerrainHeight);
                }
            }
            td.SetHeights(0, 0, heights);
            PaintSplat(td);
            EditorUtility.SetDirty(td);

            var group = RecreateGroup("관아_지형");
            var tgo = Terrain.CreateTerrainGameObject(td);
            tgo.name = "Terrain_Gwana";
            tgo.transform.SetParent(group.transform, false);
            tgo.transform.position = new Vector3(-Half, TerrainBaseY, -Half);

            var terrain = tgo.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 12f;   // 마당이 평탄해야 해 은하담(25)보다 촘촘히
            terrain.basemapDistance = 400f;
        }

        // Gyeonu_TerrainData 레이어 인덱스
        const int L_Soil = 0, L_Madang = 1, L_Grass = 2, L_DirtSos = 4, L_GrassSos = 5,
                  L_Boulder = 6, L_PathDirt = 7, L_DirtDark = 8, L_Moss = 9, L_Rock = 10;

        static void PaintSplat(TerrainData td)
        {
            int L = td.terrainLayers.Length;
            if (L <= L_Rock) { Debug.LogWarning("[관아] 지형 레이어 부족 — 스플랫 생략"); return; }

            // 2026-08-17 개편: 예전에는 마당흙/흙길/풀 3종을 고정 비율로 칠해 풀 하나가 82%를
            // 차지했고, 그래서 바닥이 통짜 색면으로 읽혔다. 이제 노이즈 4종으로
            //   ① 마당 안: 다져진 흙 + 흙 얼룩 + 사람이 다닌 자국(관문→월대 동선) + 담장 밑동 풀
            //   ② 담장 밖: 풀 2종을 저주파로 섞어 색 얼룩 + 흙 드러난 자리 + 담장 그늘 이끼
            //   ③ 언덕길: 폭이 들쭉날쭉한 흙길 + 가장자리 어두운 흙
            // 을 섞는다.
            var a = new float[ARes, ARes, L];
            var w = new float[L];
            for (int z = 0; z < ARes; z++)
            {
                float wz = z / (float)(ARes - 1) * 2f * Half - Half;
                for (int x = 0; x < ARes; x++)
                {
                    float wx = x / (float)(ARes - 1) * 2f * Half - Half;
                    float ax = Mathf.Abs(wx);
                    for (int l = 0; l < L; l++) w[l] = 0f;

                    float nLow = Mathf.PerlinNoise(wx * 0.028f + 5.5f, wz * 0.028f + 9.9f);
                    float nMid = Mathf.PerlinNoise(wx * 0.075f + 41.2f, wz * 0.075f + 17.3f);
                    float nHi = Mathf.PerlinNoise(wx * 0.17f + 3.1f, wz * 0.17f + 27.6f);
                    float nEdge = Mathf.PerlinNoise(wz * 0.11f + 63.4f, wx * 0.11f + 8.8f);

                    float inCourt = (1f - SStep(WallHalfX - 1.2f, WallHalfX + 1.2f, ax))
                                  * SStep(WallZS - 1.2f, WallZS + 1.2f, wz)
                                  * (1f - SStep(WallZN - 1.2f, WallZN + 1.2f, wz));

                    // ── 마당 안 ──
                    if (inCourt > 0.001f)
                    {
                        // 관문 → 월대 동선. 어도 양옆으로 밟히고, 문 앞·계단 앞에서 부채꼴로 퍼진다
                        float traffic = 0f;
                        if (wz > WallZS - 1f && wz < 4.5f)
                        {
                            float t = Mathf.InverseLerp(-32f, 2.4f, wz);
                            float wdt = 5.0f + 4.2f * Mathf.Abs(t * 2f - 1f);
                            float jit = (nEdge - 0.5f) * 4.6f;
                            traffic = 1f - SStep(wdt * 0.5f, wdt, Mathf.Abs(wx + jit));
                            traffic *= 0.55f + 0.45f * nMid;
                        }
                        // 담장 밑동에는 밟히지 않아 풀이 남는다 (군데군데 끊기게)
                        float dIn = Mathf.Min(Mathf.Min(WallHalfX - ax, wz - WallZS), WallZN - wz) - 0.55f;
                        float edgeGrass = (1f - SStep(1.0f, 4.0f, dIn)) * SStep(0.34f, 0.66f, nHi);

                        float bare = inCourt * (1f - edgeGrass);
                        w[L_Grass] += inCourt * edgeGrass * 0.7f;
                        w[L_GrassSos] += inCourt * edgeGrass * 0.3f;
                        w[L_PathDirt] += bare * traffic * 0.68f;
                        w[L_DirtDark] += bare * traffic * 0.22f;
                        float calm = bare * (1f - traffic * 0.9f);
                        w[L_Madang] += calm * (0.52f + 0.34f * nLow);
                        w[L_Soil] += calm * (0.48f - 0.34f * nLow);
                    }

                    // ── 언덕길 (흙길) ──
                    float roadJit = (nEdge - 0.5f) * 5.2f;
                    float onRoad = (1f - SStep(3.0f, 7.4f, Mathf.Abs(wx + roadJit * 0.6f)))
                                 * (1f - SStep(WallZS - 5f, WallZS - 1.5f, wz))
                                 * SStep(PlayZS - 5f, PlayZS + 5f, wz)
                                 * (0.6f + 0.4f * nMid);
                    onRoad *= 1f - inCourt;
                    w[L_PathDirt] += onRoad * 0.7f;
                    w[L_DirtDark] += onRoad * 0.3f;

                    // ── 담장 밖 들판 ──
                    float rest = Mathf.Clamp01(1f - inCourt - onRoad);
                    if (rest > 0.001f)
                    {
                        // 풀 2종을 저주파로 섞어 색이 얼룩지게 (단일 초록 색면 방지)
                        float mix = SStep(0.30f, 0.70f, nLow);
                        float bareSpot = SStep(0.62f, 0.86f, nMid) * (0.55f + 0.45f * nHi);
                        // 담장 그늘 밑동은 이끼
                        float moss = (1f - SStep(0.8f, 3.6f, DistOutsideWall(wx, wz)))
                                   * SStep(0.40f, 0.72f, nHi) * 0.65f;
                        float g = rest * (1f - bareSpot * 0.85f) * (1f - moss);
                        w[L_Grass] += g * (1f - mix);
                        w[L_GrassSos] += g * mix;
                        w[L_DirtSos] += rest * bareSpot * 0.62f;
                        w[L_Soil] += rest * bareSpot * 0.23f;
                        w[L_Moss] += rest * moss;
                    }

                    float sum = 0f;
                    for (int l = 0; l < L; l++) sum += w[l];
                    if (sum < 1e-4f) { w[L_Grass] = 1f; sum = 1f; }
                    for (int l = 0; l < L; l++) a[z, x, l] = w[l] / sum;
                }
            }
            td.SetAlphamaps(0, 0, a);
        }

        // ── 스커트 8타일 (은하담·견우마을과 같은 가장자리 압출 방식) ──
        const float NoiseAmp = 0.6f, NoiseFreq = 1f / 26f, NoiseFadeDist = 40f;

        static void BuildSkirt()
        {
            var mainGo = GameObject.Find("관아_지형/Terrain_Gwana");
            if (mainGo == null) { Debug.LogError("[관아] 중앙 지형 없음 — 스커트 생략"); return; }
            var main = mainGo.GetComponent<Terrain>();
            var mainTd = main.terrainData;
            Vector3 mainPos = main.transform.position, mainSize = mainTd.size;
            float tile = mainSize.x;
            int mRes = mainTd.heightmapResolution, mARes = mainTd.alphamapResolution;
            int layerCount = mainTd.terrainLayers.Length;
            var mainH = mainTd.GetHeights(0, 0, mRes, mRes);
            var mainA = mainTd.GetAlphamaps(0, 0, mARes, mARes);

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

                    string dataPath = DataDir + "/Gwana_Skirt_" + suffix + ".asset";
                    var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
                    // ⚠️ CreateAsset은 데이터 기록 전에 — 미저장 TerrainData에 SetAlphamaps 후
                    // CreateAsset하면 알파맵이 직렬화에서 초기화된다
                    if (td == null) { td = new TerrainData(); AssetDatabase.CreateAsset(td, dataPath); }
                    td.heightmapResolution = mRes;
                    td.alphamapResolution = 128;
                    td.baseMapResolution = 256;
                    td.size = new Vector3(tile, mainSize.y, tile);
                    td.terrainLayers = mainTd.terrainLayers;

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
                            if (outside > 0f)
                            {
                                float fade = SStep(0f, NoiseFadeDist, outside);
                                worldH += (Mathf.PerlinNoise(wx * NoiseFreq + 7.31f, wz * NoiseFreq + 2.17f) - 0.5f)
                                        * 2f * NoiseAmp * fade;
                            }
                            h[z, x] = Mathf.Clamp01((worldH - mainPos.y) / mainSize.y);
                        }
                    }
                    td.SetHeights(0, 0, h);

                    int aRes = td.alphamapResolution;
                    float cell = tile / aRes, mainCell = tile / mARes;
                    var al = new float[aRes, aRes, layerCount];
                    for (int z = 0; z < aRes; z++)
                    {
                        float wz = tilePos.z + (z + 0.5f) * cell;
                        int mz = Mathf.Clamp(Mathf.FloorToInt((wz - mainPos.z) / mainCell), 0, mARes - 1);
                        for (int x = 0; x < aRes; x++)
                        {
                            float wx = tilePos.x + (x + 0.5f) * cell;
                            int mx = Mathf.Clamp(Mathf.FloorToInt((wx - mainPos.x) / mainCell), 0, mARes - 1);
                            for (int l = 0; l < layerCount; l++) al[z, x, l] = mainA[mz, mx, l];
                        }
                    }
                    td.SetAlphamaps(0, 0, al);
                    EditorUtility.SetDirty(td);

                    var go = Terrain.CreateTerrainGameObject(td);
                    go.name = "Terrain_Skirt_" + suffix;
                    go.transform.SetParent(group, false);
                    go.transform.position = tilePos;

                    var t = go.GetComponent<Terrain>();
                    t.materialTemplate = main.materialTemplate;
                    t.heightmapPixelError = 25f;
                    t.basemapDistance = main.basemapDistance;
                    t.drawInstanced = main.drawInstanced;
                    t.groupingID = main.groupingID;
                    t.allowAutoConnect = true;
                    t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    t.drawTreesAndFoliage = false;
                    var col = go.GetComponent<TerrainCollider>();
                    if (col != null) col.enabled = false;   // 보행 영역 밖 — 씬 경계 콜라이더가 막는다
                    skirts[di + 1, dj + 1] = t;
                }

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var t = skirts[i, j];
                    if (t == null) continue;
                    t.SetNeighbors(i > 0 ? skirts[i - 1, j] : null, j < 2 ? skirts[i, j + 1] : null,
                                   i < 2 ? skirts[i + 1, j] : null, j > 0 ? skirts[i, j - 1] : null);
                }
            Terrain.SetConnectivityDirty();
            AssetDatabase.SaveAssets();
        }

        // ── 원경 산 (은하담 능선 메시·머티리얼 재사용) ──
        const int MountainSeed = 20260816;

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
                Debug.LogWarning("[관아] 원경 산 에셋 없음 — 은하담 조립을 먼저 실행하면 생성됨. 건너뜀");
                return;
            }

            var parent = FindRoot("관아_지형") ?? new GameObject("관아_지형");
            var old = parent.transform.Find("원경산");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("원경산");
            group.transform.SetParent(parent.transform, false);

            var rnd = new System.Random(MountainSeed);
            float R(float a, float b) => Mathf.Lerp(a, b, (float)rnd.NextDouble());

            // 은하담 방식: 능선 메시는 밑면이 로컬 y=0인 평평한 판이라, **그 자리 지면 높이보다
            // 살짝 아래**에 놓아야 밑동이 묻혀 지형과 이어져 보인다. 은하담은 지형이 평탄해서
            // 고정값 y=−1로 충분했지만 여기는 남쪽 사면이 있으므로 산마다 지면을 샘플링한다.
            // 두 링 모두 지형+스커트 범위(±300) 안에 둬서 허공에 뜨는 산이 생기지 않게 한다.
            // (안개 60→220이라 220m 밖 지면은 안개에 지워지고 산만 FogResist로 남는다)
            const float Burial = 3.0f;   // 스커트 펄린 기복(±0.6)까지 흡수하는 깊이
            int placed = 0;
            for (int ring = 0; ring < 2; ring++)
            {
                int count = ring == 0 ? 13 : 10;
                for (int i = 0; i < count; i++)
                {
                    float ang = (360f / count) * i + (ring == 0 ? 0f : 17f) + R(-11f, 11f);
                    var scale = ring == 0
                        ? new Vector3(R(180f, 260f), R(55f, 85f), R(85f, 130f))
                        : new Vector3(R(300f, 440f), R(90f, 140f), R(140f, 200f));
                    // ⚠️ 반경을 중심 거리로 잡으면 안 된다. 두 가지를 같이 봐야 한다:
                    //   ① 능선 메시는 깊이(scale.z)가 최대 200m — 중심 반경 150이면 앞자락이 50까지 온다
                    //   ② 보행 구역이 원점 대칭이 아니다 — 남쪽 스폰이 z −69까지 내려가 있어
                    //      같은 반경이라도 남쪽 산만 코앞에 선다 (실제로 이 함정을 밟았다)
                    // → 방위별 "보행 경계에서 산 앞자락까지의 여유"를 기준으로 잡고 반깊이를 더한다.
                    float nearEdge = Mathf.Min(290f,
                        PlayBoundaryDist(ang) + (ring == 0 ? R(105f, 145f) : R(200f, 250f)));
                    float rad = nearEdge + scale.z * 0.5f;

                    var pos = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * rad;
                    float baseY = SampleTerrainH(pos.x, pos.z) - Burial;

                    var go = new GameObject((ring == 0 ? "산_근경_" : "산_원경_") + i,
                        typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(group.transform, false);
                    go.transform.SetPositionAndRotation(new Vector3(pos.x, baseY, pos.z),
                        Quaternion.Euler(0f, ang + 90f + R(-18f, 18f), 0f));
                    go.transform.localScale = scale;
                    go.GetComponent<MeshFilter>().sharedMesh = variants[rnd.Next(3)];
                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = ring == 0 ? matNear : matFar;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    placed++;
                }
            }
            Debug.Log("[관아] 원경 산 " + placed + "개 (앞자락 근경 140~185 / 원경 210~255, 밑동 " + Burial + "m 매설)");
        }

        /// <summary>담장 사각 바깥면까지의 거리 (담장 안이면 큰 값 — 이끼는 바깥에만).</summary>
        static float DistOutsideWall(float x, float z)
        {
            float dx = Mathf.Abs(x) - WallHalfX, dz = Mathf.Max(WallZS - z, z - WallZN);
            if (dx <= 0f && dz <= 0f) return 99f;
            return Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dz, 0f) * Mathf.Max(dz, 0f));
        }

        /// <summary>원점에서 그 방위로 나갈 때 보행 구역 경계(|x|≤PlayHalfX, z∈[PlayZS,PlayZN])까지의 거리.
        /// 보행 구역이 남쪽으로 길어(스폰 −69) 방위마다 여유가 달라야 산이 고르게 물러난다.</summary>
        static float PlayBoundaryDist(float angDeg)
        {
            var d = Quaternion.Euler(0f, angDeg, 0f) * Vector3.forward;
            float tx = Mathf.Abs(d.x) < 1e-4f ? float.MaxValue : PlayHalfX / Mathf.Abs(d.x);
            float tz = Mathf.Abs(d.z) < 1e-4f ? float.MaxValue : (d.z > 0f ? PlayZN : PlayZS) / d.z;
            return Mathf.Min(tx, tz);
        }

        // ── 조명 (낮_맑음 — 마을·은하담과 같은 프리셋 시스템) ──
        static void BuildLighting()
        {
            var lightGo = FindRoot("Directional Light");
            if (lightGo == null) lightGo = new GameObject("Directional Light");
            // ?? 는 쓰지 않는다 — UnityEngine.Object의 가짜 null은 ??를 통과한다
            var light = lightGo.GetComponent<Light>();
            if (light == null) light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;

            var preset = AssetDatabase.LoadAssetAtPath<SkyPreset>(SkyPresetPath);
            if (preset != null) preset.Apply(light);
            else Debug.LogWarning("[관아] SkyPreset_낮_맑음 없음 — 조명 프리셋 건너뜀");

            // ── 태양 각도만 씬에서 덮어쓴다 (2026-08-17) ──
            // 프리셋 값 (50°, 60°)는 정오에 가까워 지붕만 밝고 벽면은 0.32밖에 안 받는다
            // → 건물이 평평하게 읽히고 그림자도 짧다. 고도를 34°로 낮추고 방위를 42°로 틀면
            //   동헌 정면 0.63 / 지붕 0.56 이 되어 정면이 주역이 되고, 나무 그림자도 길게 눕는다.
            // ⚠️ SkyPreset 에셋 자체는 고치지 않는다 — 마을·은하담·견우마을이 공유하는 자산이다.
            lightGo.transform.rotation = Quaternion.Euler(34f, 42f, 0f);

            // ── 안개도 씬에서 덮어쓴다 (2026-08-17) ──
            // 프리셋 에셋은 60→220이지만, 성하리 마을(Gyeonu.unity)의 실제 씬 값은 40→160 /
            // (0.76,0.79,0.81)이고 그쪽이 자연스럽다(파티클·Volume 없이 순수 Linear Fog다).
            // 사용자 지시대로 마을 값을 "걷힌 뒤 최종 상태"로 삼는다.
            // ⚠️ SkyPreset 에셋은 건드리지 않는다 — 마을·은하담·견우마을이 공유한다.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColorEnd;
            RenderSettings.fogStartDistance = FogStartEnd;
            RenderSettings.fogEndDistance = FogEndEnd;
        }

        // ── 구조물 + 동헌 ────────────────────────────────────
        static void BuildStructures()
        {
            var group = RecreateGroup("관아_건물");
            GwanaStructures.BuildInto(group.transform);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DonheonPrep.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[관아] 동헌 프리팹 없음 — 「① 동헌 에셋 준비」를 먼저 실행하세요.");
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            go.name = "동헌";
            // 모델 정면이 +Z라 180° 돌려 마당(−Z)을 바라보게 한다
            go.transform.SetPositionAndRotation(new Vector3(0f, DonheonY, DonheonZ), Quaternion.Euler(0f, 180f, 0f));
        }

        // ── 마커 ─────────────────────────────────────────────
        static void BuildMarkers()
        {
            var group = RecreateGroup("관아_마커");

            var spawn = Marker(group, "SpawnPoint_FromVillage", 0f, SpawnZ);
            spawn.transform.rotation = Quaternion.identity;             // +Z(외삼문)를 바라본다

            var exit = Marker(group, "Exit_ToVillage", 0f, ExitZ);
            exit.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 마을 쪽(−Z)

            // 동헌 대청 앞 — 나중에 Gyeonu_GwanaOffice 실내 씬 전환 트리거가 붙는다
            var door = new GameObject("Door_Donheon");
            door.transform.SetParent(group.transform, false);
            door.transform.SetPositionAndRotation(
                new Vector3(0f, DonheonMaru, DonheonZ - 2.6f), Quaternion.Euler(0f, 180f, 0f));

            Debug.Log($"[관아] 마커 3종 — 스폰(0,{spawn.transform.position.y:F2},{SpawnZ}) / " +
                      $"마을출구 / 동헌문(y {DonheonMaru:F2})");
        }

        // ── 진입 안개 연출 ───────────────────────────────────
        /// <summary>
        /// 스폰(z −74)에서 언덕 마루(−58)까지 16m를 오르는 동안 안개가 걷힌다 (약 5.3초).
        ///
        /// ⚠️ 시작값을 정하는 기준 (1차 시도가 "회색 페인트"로 보인 이유):
        ///   예전 값 2.5→24는 **그라데이션 폭이 21.5m뿐**이라 24m 밖이 전부 100% 단색으로
        ///   뭉갰고, 게다가 색까지 따로 덮어써 지평선 색과 어긋났다. 그래서 안개가 아니라
        ///   물체에 회색을 칠한 것처럼 보였다.
        ///   마을(40→160)은 폭이 120m다. 그 형태를 유지한 채 축소해 **10→52 (폭 42m)** 로 잡고
        ///   색 오버라이드는 쓰지 않는다 — 지평선과 같은 색이어야 대기로 읽힌다.
        ///
        /// 끝 상태는 FogReveal이 Awake에서 씬의 RenderSettings를 그대로 읽어 쓴다
        /// (= BuildLighting이 칠해 둔 마을 값 40→160). 여기서는 시작값만 지정한다.
        /// </summary>
        /// <summary>진입 안개 연출(파티클 + 하늘 베일 + 보조 Linear Fog).
        /// 기본값은 확정 강도(중 × 1.2) — ② 전체 조립·조명 갱신으로 재생성해도 이 값이 나온다.</summary>
        public static void BuildIntroFog() => GwanaFogFx.Build(GwanaFogFx.Level.Final);

        /// <summary>지형·구조물·식생은 그대로 두고 조명·마커·진입 연출·워커만 다시 만든다.</summary>
        [MenuItem("Tools/이문록/관아 ▸ 조명·마커·진입 연출 갱신")]
        public static void RefreshFx()
        {
            var scene = EnsureScene();
            BuildLighting();
            BuildMarkers();
            BuildIntroFog();
            GwanaWalkSetup.InstallWalker();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[관아] 조명·마커·진입 연출 갱신 완료 (지형·구조물·식생 무수정)");
        }

        static GameObject Marker(GameObject group, string name, float x, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(group.transform, false);
            go.transform.position = new Vector3(x, SampleTerrainH(x, z), z);
            return go;
        }

        public static float SampleTerrainH(float x, float z)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var p = t.transform.position; var s = t.terrainData.size;
                if (x >= p.x && x <= p.x + s.x && z >= p.z && z <= p.z + s.z)
                    return t.SampleHeight(new Vector3(x, 0f, z)) + p.y;
            }
            return GroundHeight(x, z);
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
            camGo.transform.position = new Vector3(0f, 4f, SpawnZ - 4f);
            camGo.transform.LookAt(new Vector3(0f, DonheonMaru, DonheonZ));
        }
    }
}
