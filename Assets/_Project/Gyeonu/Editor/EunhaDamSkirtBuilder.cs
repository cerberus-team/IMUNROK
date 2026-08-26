using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 은하담 지형 스커트 — 중앙 Terrain(200×200) 둘레에 동일 해상도(513px/200m)
    /// 이웃 Terrain 8타일을 붙여 총 600×600m로 확장한다. 원경 산(반경 220~280m) 기슭까지
    /// 지면이 이어져 지형 가장자리 허공이 사라진다.
    ///
    /// 색 일치 원리(과거 스커트 실패의 해법): 별도 메시가 아니라 같은 TerrainLayer 19종 +
    /// 같은 TerrainLit 머티리얼을 쓰는 진짜 Terrain이라 스플랫·조명이 그대로 이어진다.
    /// 높이·스플랫은 중앙 지형 가장자리를 바깥으로 압출(강 골도 그대로 연장) +
    /// 경계 35m 밖부터 완만한 펄린 기복. 중앙 TerrainData는 읽기만 하고 수정하지 않는다.
    ///
    /// 물 연장: 수면(72×205)을 복제해 z ±방향으로 이어붙인다 (y −1cm, 0.3m 겹침 — z파이팅 방지).
    ///
    /// 멱등 — 다시 실행하면 스커트 그룹·수면 연장을 지우고 재생성한다.
    /// TerrainData 에셋은 경로 재사용(GUID 보존).
    /// </summary>
    public static class EunhaDamSkirtBuilder
    {
        const string MainTerrainName = "Terrain_EunhaDam";
        const string TerrainRootName = "은하담_지형";
        const string SkirtGroupName = "지형스커트";
        const string WaterRootName = "은하담_물";
        const string WaterName = "수면";
        const string WaterExtPrefix = "수면_연장";
        const string DataDir = "Assets/_Project/Gyeonu/Art/Terrain";

        const float Tile = 200f;          // 중앙과 동일한 타일 크기
        const int HRes = 513;             // 높이맵 해상도 — 중앙과 동일해야 이음새가 정확히 붙는다
        const int ARes = 256;             // 스커트 스플랫 해상도 (원거리 전용이라 절반)
        const int BaseRes = 256;

        // 압출 기복 노이즈: 경계에서 0, 35m 밖부터 완전 적용. 강 골(높이 1m 미만)은 제외.
        const float NoiseAmp = 0.35f;
        const float NoiseFreq = 1f / 23f;
        const float NoiseFadeDist = 35f;
        const float NoiseMinGroundH = 1.0f;

        [MenuItem("Tools/이문록/은하담 지형 스커트 생성")]
        public static void Build()
        {
            var mainGo = GameObject.Find(TerrainRootName + "/" + MainTerrainName);
            if (mainGo == null) { Debug.LogError("[스커트] 중앙 지형을 찾지 못함: " + TerrainRootName + "/" + MainTerrainName); return; }
            var main = mainGo.GetComponent<Terrain>();
            var mainTd = main.terrainData;
            Vector3 mainPos = main.transform.position;                 // (-100, -10, -100)
            Vector3 mainSize = mainTd.size;                            // (200, 100, 200)
            int mRes = mainTd.heightmapResolution;                     // 513
            int mARes = mainTd.alphamapResolution;                     // 512
            int layerCount = mainTd.terrainLayers.Length;

            // ── 중앙 지형 읽기 (수정 없음) ──
            float[,] mainH = mainTd.GetHeights(0, 0, mRes, mRes);      // [z,x] 정규화 0~1
            float[,,] mainA = mainTd.GetAlphamaps(0, 0, mARes, mARes); // [z,x,layer]

            var terrainRoot = mainGo.transform.parent;
            var oldGroup = terrainRoot.Find(SkirtGroupName);
            if (oldGroup != null) Object.DestroyImmediate(oldGroup.gameObject);
            var group = new GameObject(SkirtGroupName).transform;
            group.SetParent(terrainRoot, false);

            var skirts = new Terrain[3, 3];
            skirts[1, 1] = main;
            int builtTiles = 0;

            for (int di = -1; di <= 1; di++)
            for (int dj = -1; dj <= 1; dj++)
            {
                if (di == 0 && dj == 0) continue;
                string suffix = (dj > 0 ? "N" : dj < 0 ? "S" : "") + (di > 0 ? "E" : di < 0 ? "W" : "");
                Vector3 tilePos = mainPos + new Vector3(di * Tile, 0f, dj * Tile);

                // TerrainData — 기존 에셋이 있으면 재사용(GUID 보존)
                string dataPath = DataDir + "/EunhaDam_Skirt_" + suffix + ".asset";
                var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
                if (td == null)
                {
                    // ⚠️ CreateAsset은 데이터 기록 전에 — 미저장 TerrainData에 SetAlphamaps 후
                    // CreateAsset하면 알파맵 텍스처가 직렬화에서 초기화된다(레이어0 100%로 리셋).
                    td = new TerrainData();
                    AssetDatabase.CreateAsset(td, dataPath);
                }

                td.heightmapResolution = HRes;
                td.alphamapResolution = ARes;
                td.baseMapResolution = BaseRes;
                td.size = new Vector3(Tile, mainSize.y, Tile);
                td.terrainLayers = mainTd.terrainLayers;

                td.SetHeights(0, 0, BuildHeights(tilePos, mainPos, mainSize, mRes, mainH));
                td.SetAlphamaps(0, 0, BuildAlphas(tilePos, mainPos, mARes, layerCount, mainA));

                EditorUtility.SetDirty(td);

                // Terrain GO
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
                t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 평탄 원거리 — 그림자 불필요
                t.drawTreesAndFoliage = false;

                var col = go.GetComponent<TerrainCollider>();
                if (col != null) col.enabled = true;   // 씬 경계(±140) 안쪽 스커트는 보행 영역 — 켜 둔다 (2026-08-07)

                skirts[di + 1, dj + 1] = t;
                builtTiles++;
            }

            // 이웃 연결 (LOD 이음새 봉합) — 중앙 지형은 컴포넌트 이웃 참조만 갱신, 데이터 무수정
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

            int waterExt = BuildWaterExtensions();
            RegisterDetails();   // 스커트 재생성 시 Terrain 컴포넌트 디테일 세팅이 초기화되므로 항상 재등록

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(mainGo.scene);
            Debug.Log("[스커트] 완료 — 지형 타일 " + builtTiles + "개(600×600m), 수면 연장 " + waterExt + "개");
        }

        /// <summary>스커트 8타일에 마을(Gyeonu) Terrain과 동일한 Detail 프로토타입(ALP 잔디·들꽃
        /// 12종)·해상도·풀 흔들림·Detail Distance 120/Density 1을 등록한다. 도색은 하지 않는다.</summary>
        [MenuItem("Tools/이문록/은하담 스커트 디테일 등록")]
        public static void RegisterDetails()
        {
            var vil = AssetDatabase.LoadAssetAtPath<TerrainData>(
                "Assets/_Project/Gyeonu/Art/Terrain/Gyeonu_TerrainData.asset");
            if (vil == null) { Debug.LogError("[스커트] 마을 TerrainData를 찾지 못함"); return; }

            int n = 0;
            foreach (var t in Terrain.activeTerrains)
            {
                if (!t.name.StartsWith("Terrain_Skirt_")) continue;
                var td = t.terrainData;
                if (td.detailScatterMode != vil.detailScatterMode)
                {
                    var m = typeof(TerrainData).GetMethod("SetDetailScatterMode");
                    if (m != null) m.Invoke(td, new object[] { vil.detailScatterMode });
                }
                td.SetDetailResolution(vil.detailResolution, vil.detailResolutionPerPatch);
                td.detailPrototypes = vil.detailPrototypes;
                td.wavingGrassAmount = vil.wavingGrassAmount;
                td.wavingGrassSpeed = vil.wavingGrassSpeed;
                td.wavingGrassStrength = vil.wavingGrassStrength;
                td.wavingGrassTint = vil.wavingGrassTint;
                t.detailObjectDistance = 120f;   // 마을 씬 실측값
                t.detailObjectDensity = 1f;
                EditorUtility.SetDirty(td);
                EditorUtility.SetDirty(t);
                n++;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[스커트] 디테일 등록 완료: " + n + "타일 × 12종 (도색 없음)");
        }

        /// <summary>스커트 타일 높이맵 — 중앙 가장자리 최근접 압출 + 경계 밖 페이드인 펄린 기복.</summary>
        static float[,] BuildHeights(Vector3 tilePos, Vector3 mainPos, Vector3 mainSize, int mRes, float[,] mainH)
        {
            float step = Tile / (HRes - 1);                            // 0.390625 — 중앙과 동일 격자
            var h = new float[HRes, HRes];
            for (int z = 0; z < HRes; z++)
            {
                float wz = tilePos.z + z * step;
                // 중앙 높이맵의 해당 행(클램프) — 격자가 정렬돼 있어 반올림이 정확히 떨어진다
                int mz = Mathf.Clamp(Mathf.RoundToInt((wz - mainPos.z) / step), 0, mRes - 1);
                float overZ = Mathf.Max(0f, Mathf.Max(mainPos.z - wz, wz - (mainPos.z + mainSize.z)));
                for (int x = 0; x < HRes; x++)
                {
                    float wx = tilePos.x + x * step;
                    int mx = Mathf.Clamp(Mathf.RoundToInt((wx - mainPos.x) / step), 0, mRes - 1);
                    float baseN = mainH[mz, mx];
                    float worldH = baseN * mainSize.y + mainPos.y;     // 실높이

                    float overX = Mathf.Max(0f, Mathf.Max(mainPos.x - wx, wx - (mainPos.x + mainSize.x)));
                    float outside = Mathf.Max(overX, overZ);
                    if (worldH > NoiseMinGroundH && outside > 0f)      // 강 골·물가는 기복 없이 그대로
                    {
                        float fade = SStep(outside / NoiseFadeDist);
                        float n = (Mathf.PerlinNoise(wx * NoiseFreq + 7.31f, wz * NoiseFreq + 2.17f) - 0.5f) * 2f * NoiseAmp;
                        worldH += n * fade;
                    }
                    h[z, x] = Mathf.Clamp01((worldH - mainPos.y) / mainSize.y);
                }
            }
            return h;
        }

        /// <summary>스커트 타일 스플랫 — 중앙 알파맵 가장자리 최근접 압출 (색 연속 보장).</summary>
        static float[,,] BuildAlphas(Vector3 tilePos, Vector3 mainPos, int mARes, int layerCount, float[,,] mainA)
        {
            float cell = Tile / ARes;
            float mainCell = Tile / mARes;
            var a = new float[ARes, ARes, layerCount];
            for (int z = 0; z < ARes; z++)
            {
                float wz = tilePos.z + (z + 0.5f) * cell;
                int mz = Mathf.Clamp(Mathf.FloorToInt((wz - mainPos.z) / mainCell), 0, mARes - 1);
                for (int x = 0; x < ARes; x++)
                {
                    float wx = tilePos.x + (x + 0.5f) * cell;
                    int mx = Mathf.Clamp(Mathf.FloorToInt((wx - mainPos.x) / mainCell), 0, mARes - 1);
                    for (int L = 0; L < layerCount; L++)
                        a[z, x, L] = mainA[mz, mx, L];
                }
            }
            return a;
        }

        /// <summary>수면 복제 2장을 z ±로 이어붙인다. 원본 수면·머티리얼은 수정하지 않는다.</summary>
        static int BuildWaterExtensions()
        {
            var waterGo = GameObject.Find(WaterRootName + "/" + WaterName);
            if (waterGo == null) { Debug.LogWarning("[스커트] 수면을 찾지 못해 물 연장 생략"); return 0; }
            var waterRoot = waterGo.transform.parent;

            for (int i = waterRoot.childCount - 1; i >= 0; i--)
            {
                var c = waterRoot.GetChild(i);
                if (c.name.StartsWith(WaterExtPrefix)) Object.DestroyImmediate(c.gameObject);
            }

            var mr = waterGo.GetComponent<MeshRenderer>();
            float lenZ = mr.bounds.size.z;                             // 205
            int made = 0;
            foreach (int dir in new[] { 1, -1 })
            {
                var ext = Object.Instantiate(waterGo, waterRoot);
                ext.name = WaterExtPrefix + (dir > 0 ? "_북" : "_남");
                // 0.3m 겹침 + y −1cm — 겹침 구간 z파이팅 방지 (100m 밖이라 단차 비가시)
                ext.transform.position = waterGo.transform.position
                    + new Vector3(0f, -0.01f, dir * (lenZ - 0.3f));
                foreach (var c in ext.GetComponents<Collider>()) Object.DestroyImmediate(c);
                made++;
            }
            return made;
        }

        // GLSL smoothstep(0,1,x) — Mathf.SmoothStep의 인자 순서 함정 회피용
        static float SStep(float x)
        {
            float t = Mathf.Clamp01(x);
            return t * t * (3f - 2f * t);
        }
    }
}
