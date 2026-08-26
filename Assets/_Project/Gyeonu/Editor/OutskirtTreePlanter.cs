using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 성하리 외곽 나무 심기 (고정 시드 = 재실행 시 동일 결과).
    ///
    /// 배치 규칙 (2026-08-04 지시):
    ///   - 3~7그루 덩어리, 위치·회전 랜덤, 스케일 0.7~1.3 균일
    ///   - 언덕 위쪽 적게(높이 가중), 마을 경계 많게, 경사 20° 초과 금지
    ///   - 마을 입구(-Z)·은하담(+X) 통로 비움
    ///   - 종 구성: 원경 Aspen 위주 / 중경 벚나무JG·흑송JG / 경계 왕대JG / 근경 소나무VR 소수
    ///   - 기존 배치물(집터·건물·소품·식생) 발자국 + 4m 회피
    /// </summary>
    public static class OutskirtTreePlanter
    {
        const int Seed = 20260804;
        const int TargetCount = 260;          // 200~300 요구의 중간값
        const string VegPath = "Assets/_Project/Gyeonu/Prefabs/Vegetation/";
        const string GroupName = "외곽나무";

        // 마을 코어(이 안에는 클러스터 중심을 두지 않음 — 경계 틈새는 개별 나무로 허용)
        const float CoreXMin = -50f, CoreXMax = 42f, CoreZMin = -40f, CoreZMax = 60f;
        // 통로 (클러스터·나무 모두 금지)
        static readonly Rect[] Corridors =
        {
            new Rect(-26f, -100f, 28f, 60f),   // 마을 입구 -Z (오공문 x=-13 주변)
            new Rect(45f, -2f, 55f, 24f),      // 은하담 +X
        };

        static readonly string[] PoolAspen = { "Aspen_사시나무_원경" };
        static readonly string[] PoolCherry =
        {
            "Cherry_벚나무JG_대_01", "Cherry_벚나무JG_대_05", "Cherry_벚나무JG_중_01",
            "Cherry_벚나무JG_중_05", "Cherry_벚나무JG_소_01", "Cherry_벚나무JG_소_05",
        };
        static readonly string[] PoolBamboo =
        {
            "Bamboo_왕대JG_대_01", "Bamboo_왕대JG_대_02", "Bamboo_왕대JG_중_01",
            "Bamboo_왕대JG_중_02", "Bamboo_왕대JG_소_01", "Bamboo_왕대JG_소_02",
        };
        static readonly string[] PoolBlackPine =
        {
            "Pine_흑송JG_대_01", "Pine_흑송JG_중_01", "Pine_흑송JG_중_02",
            "Pine_흑송JG_소_01", "Pine_흑송JG_소_02",
        };
        static readonly string[] PoolPineVR = { "Pine_소나무_PinusDensiflora_1_VR", "Pine_소나무_PinusDensiflora_2_VR" };
        static readonly string[] PoolHenonis = { "Bamboo_대나무_Henonis_1_VR", "Bamboo_대나무_Henonis_2_VR" };

        const int PineVRCap = 10;             // LOD0 5.8만 tri — 근경 소수만
        const int HenonisCap = 8;

        [MenuItem("Tools/이문록/성하리 외곽 나무 심기")]
        public static void Plant()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) { Debug.LogError("[OutskirtTreePlanter] Terrain 없음"); return; }
            var td = terrain.terrainData;
            Vector3 tpos = terrain.transform.position;

            var prefabs = new Dictionary<string, GameObject>();
            foreach (var pool in new[] { PoolAspen, PoolCherry, PoolBamboo, PoolBlackPine, PoolPineVR, PoolHenonis })
                foreach (var n in pool)
                {
                    var p = AssetDatabase.LoadAssetAtPath<GameObject>(VegPath + n + ".prefab");
                    if (p == null) { Debug.LogError("[OutskirtTreePlanter] 프리팹 없음: " + n); return; }
                    prefabs[n] = p;
                }

            // ── 회피 구역: 기존 배치물 발자국 (XZ AABB + 4m) ──
            var occupied = new List<Rect>();
            foreach (var rootName in new[] { "성하리_집터", "성하리_건물", "성하리_소품" })
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (Transform child in root.transform)
                {
                    Vector3 mn = Vector3.one * float.MaxValue, mx = Vector3.one * float.MinValue;
                    foreach (var r in child.GetComponentsInChildren<Renderer>(true))
                    { mn = Vector3.Min(mn, r.bounds.min); mx = Vector3.Max(mx, r.bounds.max); }
                    if (mn.x > mx.x) continue;
                    occupied.Add(Rect.MinMaxRect(mn.x - 4f, mn.z - 4f, mx.x + 4f, mx.z + 4f));
                }
            }
            // 기존 식생은 점 + 2m
            var treePoints = new List<Vector2>();
            var veg = GameObject.Find("성하리_식생");
            foreach (Transform child in veg.transform)
                if (child.name != GroupName)
                    treePoints.Add(new Vector2(child.position.x, child.position.z));

            var old = veg.transform.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject(GroupName).transform;
            group.SetParent(veg.transform, false);

            var rnd = new System.Random(Seed);
            int planted = 0, pineVRUsed = 0, henonisUsed = 0, attempts = 0;
            var counts = new Dictionary<string, int>();

            while (planted < TargetCount && attempts < 6000)
            {
                attempts++;
                float cx = Mathf.Lerp(-95f, 95f, (float)rnd.NextDouble());
                float cz = Mathf.Lerp(-95f, 95f, (float)rnd.NextDouble());

                if (InCore(cx, cz) || InCorridor(cx, cz)) continue;
                float h = terrain.SampleHeight(new Vector3(cx, 0, cz)) + tpos.y;
                if (Steepness(td, tpos, cx, cz) > 20f) continue;

                // 높이 가중: 언덕 위로 갈수록 확률 급감
                float acc = h < 1.5f ? 1f : h < 4f ? 0.6f : h < 8f ? 0.3f : 0.08f;
                // 마을 경계 근처 가중
                if (RingDist(cx, cz) > 22f) acc *= 0.5f;
                if (rnd.NextDouble() > acc) continue;

                // ── 클러스터 ──
                int size = 3 + rnd.Next(5);                       // 3~7
                float radius = 4f + (float)rnd.NextDouble() * 5f; // 4~9m
                string[] pool = PickPool(rnd, RingDist(cx, cz), h, ref pineVRUsed, ref henonisUsed);
                string dominant = pool[rnd.Next(pool.Length)];

                for (int i = 0; i < size && planted < TargetCount; i++)
                {
                    float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                    float dist = (float)System.Math.Sqrt(rnd.NextDouble()) * radius;
                    float x = cx + Mathf.Cos(ang) * dist;
                    float z = cz + Mathf.Sin(ang) * dist;
                    if (x < -96f || x > 96f || z < -96f || z > 96f) continue;
                    if (InCorridor(x, z) || InOccupied(occupied, x, z)) continue;
                    if (Steepness(td, tpos, x, z) > 22f) continue;

                    bool tooClose = false;
                    var p2 = new Vector2(x, z);
                    foreach (var q in treePoints)
                        if ((q - p2).sqrMagnitude < 2.2f * 2.2f) { tooClose = true; break; }
                    if (tooClose) continue;

                    // 70%는 클러스터 대표종 → 덩어리 느낌, 30%는 풀에서 랜덤
                    string species = rnd.NextDouble() < 0.7 ? dominant : pool[rnd.Next(pool.Length)];
                    if (System.Array.IndexOf(PoolPineVR, species) >= 0)
                    {
                        if (pineVRUsed >= PineVRCap) species = dominant == species ? PoolCherry[rnd.Next(PoolCherry.Length)] : dominant;
                        else pineVRUsed++;
                    }
                    if (System.Array.IndexOf(PoolHenonis, species) >= 0)
                    {
                        if (henonisUsed >= HenonisCap) species = PoolBamboo[rnd.Next(PoolBamboo.Length)];
                        else henonisUsed++;
                    }

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[species]);
                    go.transform.SetParent(group, false);
                    float y = terrain.SampleHeight(new Vector3(x, 0, z)) + tpos.y;
                    go.transform.position = new Vector3(x, y, z);
                    go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                    float s = 0.7f + (float)rnd.NextDouble() * 0.6f;
                    go.transform.localScale = new Vector3(s, s, s);

                    treePoints.Add(p2);
                    planted++;
                    counts[species] = counts.TryGetValue(species, out var c) ? c + 1 : 1;
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("[OutskirtTreePlanter] 완료: ").Append(planted).Append("그루 (시도 ").Append(attempts).Append(")\n");
            foreach (var kv in counts) sb.Append("  ").Append(kv.Key).Append(": ").Append(kv.Value).Append('\n');
            Debug.Log(sb.ToString());
            EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        }

        // 마을 코어 사각형 안인가
        static bool InCore(float x, float z)
            => x > CoreXMin && x < CoreXMax && z > CoreZMin && z < CoreZMax;

        // 코어 가장자리에서 바깥으로 떨어진 거리
        static float RingDist(float x, float z)
        {
            float dx = Mathf.Max(0, Mathf.Max(CoreXMin - x, x - CoreXMax));
            float dz = Mathf.Max(0, Mathf.Max(CoreZMin - z, z - CoreZMax));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static bool InCorridor(float x, float z)
        {
            foreach (var r in Corridors) if (r.Contains(new Vector2(x, z))) return true;
            return false;
        }

        static bool InOccupied(List<Rect> rects, float x, float z)
        {
            var p = new Vector2(x, z);
            foreach (var r in rects) if (r.Contains(p)) return true;
            return false;
        }

        static float Steepness(TerrainData td, Vector3 tpos, float x, float z)
            => td.GetSteepness((x - tpos.x) / td.size.x, (z - tpos.z) / td.size.z);

        // 거리·높이에 따른 종 구성 (CLAUDE.md 식생 방침)
        static string[] PickPool(System.Random rnd, float ringDist, float h, ref int pineVRUsed, ref int henonisUsed)
        {
            if (ringDist > 22f || h > 5f)   // 원경·언덕: 사시나무 위주
                return rnd.NextDouble() < 0.85 ? PoolAspen
                     : rnd.NextDouble() < 0.7 ? PoolCherry : PoolBlackPine;
            if (ringDist > 8f)              // 중경: 벚나무 주력 + 사시나무
                return rnd.NextDouble() < 0.5 ? PoolCherry
                     : rnd.NextDouble() < 0.7 ? PoolAspen : PoolBlackPine;
            // 근경(마을 경계): 왕대·벚 + 흑송·소나무VR 소수
            double roll = rnd.NextDouble();
            if (roll < 0.45) return PoolBamboo;
            if (roll < 0.75) return PoolCherry;
            if (roll < 0.85) return PoolBlackPine;
            if (roll < 0.93 && pineVRUsed < PineVRCap) return PoolPineVR;
            if (henonisUsed < HenonisCap) return PoolHenonis;
            return PoolBamboo;
        }
    }
}
