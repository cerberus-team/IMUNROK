using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 은하담 구조물·식생 배치기 (고정 시드 = 재실행 시 동일 결과, 멱등).
    ///
    /// 구조물(2026-08-07 확정): 정자는 풍영정 하나 — 세연정·광풍각처럼 못 위 대(臺,
    ///         수면 아래 0.15 평탄부)에 세우고 StoneBridge_1로 서안과 잇는다.
    ///         정자에서 남쪽으로 오작교가 보인다. 풍영정 FBX는 텍스처 미연결이라
    ///         URP Lit 머티리얼(Art/Materials/풍영정_*)을 씬 인스턴스에만 입힌다 (원본 무수정).
    ///
    /// 식생(2026-08-01 확정 방침): 버드나무 Salix_VR(물가), 갈대 Phragmites_VR,
    ///         수련 Nymphaea(수면 0.5 부유), 흑송JG 중경 + 소나무VR 근경.
    ///         원경은 산 메시(은하담_원경산)가 담당하므로 사시나무는 못가 배경 소량만.
    ///         높이는 EunhaDamBuilder.GroundHeight(해석 함수)에서 직접 샘플.
    /// </summary>
    public static class EunhaDamDecorPlanter
    {
        const int Seed = 20260806;
        const string VegPath = "Assets/_Project/Gyeonu/Prefabs/Vegetation/";
        const string SeyeonPath = "Assets/_Project/Gyeonu/Prefabs/Seyeonjeong/";
        const string MatFolder = "Assets/_Project/Gyeonu/Art/Materials";

        const float WaterLevel = 0.5f;

        // 회피 구역 (xMin, xMax, zMin, zMax)
        static readonly float[][] AvoidRects =
        {
            new[] { -60f, 60f, -16f, 16f },     // 다리 전체 (문루~문루)
            new[] { 36f, 100f, -15f, 15f },     // 입구 통로 (둑 위 진입로)
        };

        [MenuItem("Tools/이문록/은하담 구조물 배치")]
        public static void PlaceStructures()
        {
            var group = RecreateGroup("은하담_구조물");
            var rnd = new System.Random(Seed);

            // ── 풍영정 (확정): 호수 안쪽 대(臺) 위 — 사방이 물, 계단(정면 local +Z)은
            //    서쪽 물가(bearing 260)를 향하고 남동 개방면으로 오작교가 보인다 ──
            float px = EunhaDamBuilder.PondPadX, pz = EunhaDamBuilder.PondPadZ;
            var pyj = PlacePrefab(group,
                "Assets/woljeonggyo/pungyeongjeong-pavilion/source/01_PoongYoungJung.fbx",
                "정자_풍영정", new Vector3(px, 0.15f, pz), 260f, 1f, snapBaseY: true);
            if (pyj != null) ApplyPungyeongMaterials(pyj);

            // ── 돌다리 사슬: 서안 물가(-29.6, 39.8) → 정자 계단(-18.5, 39.0) 3연결 + 받침 바위 ──
            PlacePrefab(group, SeyeonPath + "SM_StoneBridge_1.prefab",
                "돌다리_정자연결_1", new Vector3(-27.8f, 0.42f, 39.66f), 2f, 1.3f);
            PlacePrefab(group, SeyeonPath + "SM_StoneBridge_1.prefab",
                "돌다리_정자연결_2", new Vector3(-24.05f, 0.42f, 39.4f), 6f, 1.3f);
            PlacePrefab(group, SeyeonPath + "SM_StoneBridge_1.prefab",
                "돌다리_정자연결_3", new Vector3(-20.3f, 0.42f, 39.13f), 3f, 1.3f);
            PlacePrefab(group, SeyeonPath + "SM_Rock_K_VR.prefab",
                "돌다리_받침_1", new Vector3(-25.9f, -0.15f, 39.55f), 40f, 0.7f);
            PlacePrefab(group, SeyeonPath + "SM_Rock_K_VR.prefab",
                "돌다리_받침_2", new Vector3(-22.2f, -0.15f, 39.25f), 200f, 0.65f);
            // 동안 물가 징검다리
            PlacePrefab(group, SeyeonPath + "SM_StoneBridge_2.prefab",
                "돌다리_물가", new Vector3(31.8f, 0.18f, -24f), 75f, 1f);

            // ── 물가 바위 ──
            string[] rockNames = { "SM_Rock_A_VR", "SM_Rock_C_VR", "SM_Rock_E_VR",
                                   "SM_Rock_G_VR", "SM_Rock_K_VR", "SM_Rock_L_VR" };
            var rocks = rockNames.Select(n =>
                AssetDatabase.LoadAssetAtPath<GameObject>(SeyeonPath + n + ".prefab")).ToArray();
            if (rocks.Any(r => r == null)) { Debug.LogError("[은하담] 세연정 바위 프리팹 누락"); return; }

            int placedShore = 0, placedWater = 0, attempts = 0;
            var points = new List<Vector2>();
            while (placedShore < 16 && attempts++ < 800)
            {
                float z = Mathf.Lerp(-85f, 85f, (float)rnd.NextDouble());
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float x = side * (EunhaDamBuilder.ChannelHalfWidth(z) + Mathf.Lerp(-2f, 4.5f, (float)rnd.NextDouble()));
                float h = EunhaDamBuilder.GroundHeight(x, z);
                if (h < 0.0f || h > 1.0f) continue;
                if (NearPavilion(x, z, 4f)) continue;
                if (Blocked(x, z, points, 5f)) continue;

                int idx = rnd.Next(rocks.Length);
                float s = Mathf.Lerp(0.6f, 1.1f, (float)rnd.NextDouble());
                if (idx == 0) s *= 0.55f;   // Rock_A는 9.6m급 — 축소
                var go = (GameObject)PrefabUtility.InstantiatePrefab(rocks[idx], group.transform);
                go.name = "바위_물가_" + placedShore;
                go.transform.position = new Vector3(x, h - 0.12f, z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * s;
                points.Add(new Vector2(x, z));
                placedShore++;
            }
            attempts = 0;
            while (placedWater < 5 && attempts++ < 400)
            {
                float z = Mathf.Lerp(-70f, 70f, (float)rnd.NextDouble());
                if (Mathf.Abs(z) < 16f) continue;
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float x = side * (EunhaDamBuilder.ChannelHalfWidth(z) - Mathf.Lerp(2f, 8f, (float)rnd.NextDouble()));
                float h = EunhaDamBuilder.GroundHeight(x, z);
                if (h > 0.0f) continue;
                if (NearPavilion(x, z, 6f)) continue;
                if (Blocked(x, z, points, 8f)) continue;

                int idx = rnd.Next(rocks.Length);
                if (idx == 5) idx = 2;      // 물속엔 작은 L 대신 중형
                float s = Mathf.Lerp(0.7f, 1.0f, (float)rnd.NextDouble());
                if (idx == 0) s *= 0.55f;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(rocks[idx], group.transform);
                go.name = "바위_물속_" + placedWater;
                go.transform.position = new Vector3(x, h - 0.15f, z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * s;
                points.Add(new Vector2(x, z));
                placedWater++;
            }

            ApplyStaticFlags(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[은하담] 구조물 배치 완료: 풍영정 1, 돌다리 2, 바위 물가 {placedShore} + 물속 {placedWater}");
        }

        [MenuItem("Tools/이문록/은하담 식생 배치")]
        public static void PlantVegetation()
        {
            var root = FindRoot("은하담_식생") ?? new GameObject("은하담_식생");
            foreach (var n in new[] { "버드나무", "갈대", "수련", "외곽숲" })
            {
                var old = root.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var willowG = NewChild(root, "버드나무");
            var reedG = NewChild(root, "갈대");
            var lilyG = NewChild(root, "수련");
            var forestG = NewChild(root, "외곽숲");

            var rnd = new System.Random(Seed + 1);
            var planted = new List<Vector2>();

            // ── 버드나무 (은하담 상징 — 물가선 h 0.6~0.9 띠, x는 강폭에서 계산) ──
            var willows = new[]
            {
                LoadVeg("Willow_버드나무_Salix_1_VR"),
                LoadVeg("Willow_버드나무_Salix_2_VR"),
            };
            float[][] willowAnchors =   // { z, 강가 쪽(-1 서안 / +1 동안) }
            {
                new[] { 55f, -1f }, new[] { 22f, -1f }, new[] { -36f, -1f }, new[] { -62f, -1f },
                new[] { 22f, 1f }, new[] { 48f, 1f }, new[] { -38f, 1f }, new[] { -65f, 1f },
                new[] { 70f, -1f },
            };
            int w = 0;
            foreach (var a in willowAnchors)
            {
                float z = a[0] + Jitter(rnd, 1.5f);
                float x = a[1] * (EunhaDamBuilder.ChannelHalfWidth(z) + 5.5f) + Jitter(rnd, 1f);
                if (NearPavilion(x, z, 6f)) continue;
                float h = EunhaDamBuilder.GroundHeight(x, z);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(willows[w % 2], willowG);
                go.name = "버드나무_" + w;
                go.transform.position = new Vector3(x, h - 0.08f, z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.2f, (float)rnd.NextDouble());
                planted.Add(new Vector2(x, z));
                w++;
            }

            // ── 갈대 (물가선 h 0.05~0.6 띠) ──
            var reeds = new[] { LoadVeg("Reed_갈대_Phragmites_1_VR"), LoadVeg("Reed_갈대_Phragmites_2_VR") };
            int reedCount = 0, attempts = 0;
            while (reedCount < 60 && attempts++ < 3000)
            {
                float z = Mathf.Lerp(-88f, 88f, (float)rnd.NextDouble());
                if (Mathf.Abs(z) < 16f) continue;   // 다리 아래 제외
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float x = side * (EunhaDamBuilder.ChannelHalfWidth(z) + Mathf.Lerp(2f, 6f, (float)rnd.NextDouble()));
                float h = EunhaDamBuilder.GroundHeight(x, z);
                if (h < 0.05f || h > 0.65f) continue;
                if (NearPavilion(x, z, 8f)) continue;
                if (Blocked(x, z, planted, 1.6f)) continue;
                PlaceClump(reedG, reeds[rnd.Next(2)], "갈대_" + reedCount,
                    new Vector3(x, h - 0.05f, z), rnd, 0.8f, 1.3f);
                planted.Add(new Vector2(x, z));
                reedCount++;
            }

            // ── 수련 (수면 y 0.5, 못 가장자리 군집 — 풍영정 둘레 포함) ──
            var lilyBlooms = new List<GameObject>();
            var lilyLeaves = new List<GameObject>();
            for (int i = 1; i <= 4; i++)
            {
                var bloom = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SeyeonjeongPavilion/Prefabs/SM_NymphaeaTetragona_" + i + ".prefab");
                if (bloom != null) lilyBlooms.Add(bloom);
                var leaf = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SeyeonjeongPavilion/Prefabs/SM_NymphaeaTetragona_Leaf_" + i + ".prefab");
                if (leaf != null) lilyLeaves.Add(leaf);
            }
            if (lilyLeaves.Count == 0) lilyLeaves = lilyBlooms;   // Leaf 프리팹이 없으면 꽃으로
            float[][] lilyClusters =    // { z, 쪽 }
            {
                new[] { 28f, -1f }, new[] { 34f, 1f }, new[] { -32f, 1f },
                new[] { -42f, -1f }, new[] { 56f, 1f }, new[] { -58f, -1f },
            };
            int lilyCount = 0;
            void LilyCluster(float cx, float cz, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = cx + Jitter(rnd, 2.5f), z = cz + Jitter(rnd, 2.5f);
                    if (EunhaDamBuilder.GroundHeight(x, z) > 0.35f) continue;   // 물속만
                    var pool = rnd.NextDouble() < 0.35 && lilyBlooms.Count > 0 ? lilyBlooms : lilyLeaves;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pool[rnd.Next(pool.Count)], lilyG);
                    go.name = "수련_" + lilyCount;
                    go.transform.position = new Vector3(x, WaterLevel, z);
                    go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                    go.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.4f, (float)rnd.NextDouble());
                    lilyCount++;
                }
            }
            foreach (var c in lilyClusters)
                LilyCluster(c[1] * (EunhaDamBuilder.ChannelHalfWidth(c[0]) - 4.5f), c[0], 4 + rnd.Next(3));
            // 풍영정 둘레 수련 고리 (대 가장자리 밖 수면)
            LilyCluster(-5.9f, 31.6f, 5);
            LilyCluster(-16.8f, 29.7f, 4);
            LilyCluster(-20.1f, 48.4f, 5);
            LilyCluster(-2.2f, 41.9f, 4);

            // ── 중경 숲 (원경은 산 메시 담당 — 사시나무는 배경 소량만) ──
            var aspen = LoadVeg("Aspen_사시나무_원경");
            var blackPines = new[] { "Pine_흑송JG_대_01", "Pine_흑송JG_대_02", "Pine_흑송JG_중_01",
                                     "Pine_흑송JG_중_02", "Pine_흑송JG_소_01", "Pine_흑송JG_소_02" }
                             .Select(LoadVeg).ToArray();
            var pinesVR = new[] { LoadVeg("Pine_소나무_PinusDensiflora_1_VR"), LoadVeg("Pine_소나무_PinusDensiflora_2_VR") };

            int aspenCount = 0; attempts = 0;
            while (aspenCount < 24 && attempts++ < 3000)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(70f, 95f, (float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                float h = EunhaDamBuilder.GroundHeight(x, z);
                if (h < 1.5f || h > 5.5f) continue;
                if (SlopeDeg(x, z) > 25f) continue;
                if (InAvoid(x, z)) continue;
                if (Blocked(x, z, planted, 4f)) continue;
                Plant(forestG, aspen, "사시_" + aspenCount, x, h, z, rnd, 0.8f, 1.35f);
                planted.Add(new Vector2(x, z));
                aspenCount++;
            }

            int bpCount = 0; attempts = 0;
            while (bpCount < 30 && attempts++ < 4000)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(50f, 90f, (float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                float h = EunhaDamBuilder.GroundHeight(x, z);
                if (h < 1.5f || h > 6.5f) continue;
                if (SlopeDeg(x, z) > 28f) continue;
                if (InAvoid(x, z) || NearPavilion(x, z, 10f)) continue;
                if (Blocked(x, z, planted, 3f)) continue;
                Plant(forestG, blackPines[rnd.Next(blackPines.Length)], "흑송_" + bpCount, x, h, z, rnd, 0.85f, 1.25f);
                planted.Add(new Vector2(x, z));
                bpCount++;
            }

            int pvCount = 0; attempts = 0;
            while (pvCount < 8 && attempts++ < 2000)
            {
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float x = side * Mathf.Lerp(34f, 74f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(-70f, 70f, (float)rnd.NextDouble());
                float h = EunhaDamBuilder.GroundHeight(x, z);
                if (h < 1.2f || h > 7.2f) continue;
                if (SlopeDeg(x, z) > 28f) continue;
                if (InAvoid(x, z) || NearPavilion(x, z, 8f)) continue;
                if (Blocked(x, z, planted, 5f)) continue;
                Plant(forestG, pinesVR[pvCount % 2], "소나무_" + pvCount, x, h, z, rnd, 0.9f, 1.15f);
                planted.Add(new Vector2(x, z));
                pvCount++;
            }

            // 풍영정 맞은편 서안 둑 대나무 (배경 가림) + 남서안 균형
            var henonis = new[] { LoadVeg("Bamboo_대나무_Henonis_1_VR"), LoadVeg("Bamboo_대나무_Henonis_2_VR") };
            int hCount = 0;
            foreach (var c in new[] { new Vector2(-38f, 44f), new Vector2(-41f, 39f), new Vector2(-35f, 49f),
                                      new Vector2(-37f, -42f), new Vector2(-40f, -37f), new Vector2(-34f, -47f) })
            {
                float x = c.x + Jitter(rnd, 1f), z = c.y + Jitter(rnd, 1f);
                float h = EunhaDamBuilder.GroundHeight(x, z);
                Plant(forestG, henonis[hCount % 2], "대나무_" + hCount, x, h, z, rnd, 0.9f, 1.2f);
                hCount++;
            }

            // 마을 방향 길목 양옆 프레이밍 (정면은 트고 좌우만 나무·대숲으로 막는다)
            int fCount = 0;
            var flankPool = new[] { henonis[0], blackPines[2], aspen, henonis[1],
                                    blackPines[4], aspen, blackPines[0], henonis[0] };
            foreach (var c in new[] { new Vector2(70f, 15f), new Vector2(78f, -14.5f), new Vector2(84f, 16f),
                                      new Vector2(92f, -15f), new Vector2(97f, 14f), new Vector2(75f, -17f),
                                      new Vector2(88f, 13.5f), new Vector2(96f, -13.5f) })
            {
                float x = c.x + Jitter(rnd, 1.2f), z = c.y + Jitter(rnd, 1.2f);
                float h = EunhaDamBuilder.GroundHeight(x, z);
                Plant(forestG, flankPool[fCount % flankPool.Length], "길목_" + fCount, x, h, z, rnd, 0.9f, 1.2f);
                fCount++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[은하담] 식생 배치 완료: 버드나무 {w}, 갈대 {reedCount}, 수련 {lilyCount}, " +
                      $"사시 {aspenCount}, 흑송 {bpCount}, 소나무VR {pvCount}, 대나무 {hCount}");
        }

        // ── 풍영정 URP 머티리얼 (원본 FBX·폴더 무수정 — 씬 인스턴스에만 적용) ──
        static void ApplyPungyeongMaterials(GameObject instance)
        {
            const string texDir = "Assets/woljeonggyo/pungyeongjeong-pavilion/textures/";
            var mats = new Dictionary<string, Material>();
            foreach (var key in new[] { "woods", "Roof", "extra" })
            {
                string matPath = MatFolder + "/풍영정_" + key + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Poongyoung_" + key + "_albedo.jpg");
                mat.SetTexture("_BaseMap", albedo);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.2f);
                EditorUtility.SetDirty(mat);
                mats["Poongyoung_" + key] = mat;
            }
            AssetDatabase.SaveAssets();

            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                    if (shared[i] != null && mats.TryGetValue(shared[i].name, out var m))
                        shared[i] = m;
                r.sharedMaterials = shared;
            }
        }

        // ── 헬퍼 ────────────────────────────────────────────
        static GameObject PlacePrefab(GameObject parent, string path, string name,
            Vector3 pos, float rotY, float scale, bool snapBaseY = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError("[은하담] 프리팹 없음: " + path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            go.transform.localScale = Vector3.one * scale;
            if (snapBaseY)
            {
                var rends = go.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    go.transform.position += Vector3.up * (pos.y - b.min.y);
                }
            }
            return go;
        }

        static void Plant(Transform parent, GameObject prefab, string name,
            float x, float h, float z, System.Random rnd, float sMin, float sMax)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = new Vector3(x, h - 0.06f, z);
            go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            go.transform.localScale = Vector3.one * Mathf.Lerp(sMin, sMax, (float)rnd.NextDouble());
        }

        static void PlaceClump(Transform parent, GameObject prefab, string name,
            Vector3 pos, System.Random rnd, float sMin, float sMax)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            go.transform.localScale = Vector3.one * Mathf.Lerp(sMin, sMax, (float)rnd.NextDouble());
        }

        static GameObject LoadVeg(string name)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(VegPath + name + ".prefab");
            if (p == null) Debug.LogError("[은하담] 식생 프리팹 없음: " + name);
            return p;
        }

        static float SlopeDeg(float x, float z)
        {
            float sx = EunhaDamBuilder.GroundHeight(x + 1f, z) - EunhaDamBuilder.GroundHeight(x - 1f, z);
            float sz = EunhaDamBuilder.GroundHeight(x, z + 1f) - EunhaDamBuilder.GroundHeight(x, z - 1f);
            return Mathf.Atan(0.5f * Mathf.Sqrt(sx * sx + sz * sz)) * Mathf.Rad2Deg;
        }

        static bool InAvoid(float x, float z) =>
            AvoidRects.Any(r => x > r[0] && x < r[1] && z > r[2] && z < r[3]);

        static bool NearPavilion(float x, float z, float margin) =>
            Vector2.Distance(new Vector2(x, z),
                new Vector2(EunhaDamBuilder.PondPadX, EunhaDamBuilder.PondPadZ)) < margin;

        static bool Blocked(float x, float z, List<Vector2> pts, float minDist)
        {
            var v = new Vector2(x, z);
            foreach (var p in pts) if (Vector2.Distance(p, v) < minDist) return true;
            return false;
        }

        static float Jitter(System.Random rnd, float amp) =>
            ((float)rnd.NextDouble() * 2f - 1f) * amp;

        static Transform NewChild(GameObject parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent.transform, false);
            return t;
        }

        static GameObject RecreateGroup(string name)
        {
            var old = FindRoot(name);
            if (old != null) Object.DestroyImmediate(old);
            return new GameObject(name);
        }

        static GameObject FindRoot(string name) =>
            SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);

        static void ApplyStaticFlags(GameObject group)
        {
            var flags = StaticEditorFlags.BatchingStatic
                      | StaticEditorFlags.OccluderStatic
                      | StaticEditorFlags.OccludeeStatic;
            foreach (var t in group.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }
    }
}
