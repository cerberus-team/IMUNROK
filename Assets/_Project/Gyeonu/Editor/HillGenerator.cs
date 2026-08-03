using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 성하리 외곽 언덕 생성기.
    ///
    /// 낙안읍성 데모 지형(1000m, 높이맵 513) 실측 분석값을 축소 적용한다:
    ///   - 경사 분포: 중앙값 23°, p75 35°, p90 43.5° → 기본 사면 경사 35°
    ///   - 스케일별 굴곡 감쇠(persistence) ≈ 0.55
    ///   - 능선 파장 약 270m, 100m 창 기복 중앙값 48m → 200m 지형에 맞게 축소
    ///
    /// 보호 규칙:
    ///   - 마을 중심(8.19, 10.16) 반경 70m 원은 절대 불변
    ///   - 반경 밖에 있는 배치물(건물·소품 = 렌더러 바운즈 + 4m, 나무 = 밑동 3m)의
    ///     발자국도 보호 구역으로 스탬프 → 거리장(chamfer) 기반으로 그 바깥에서만 상승
    ///   - 마을 입구(-Z)와 은하담(+X) 방향은 고개처럼 낮게 튼다
    /// </summary>
    public static class HillGenerator
    {
        // ── 마을 ──
        const float VillageCX = 8.19f, VillageCZ = 10.16f;
        const float ProtectRadius = 70f;   // 이 안은 절대 불변
        const float Apron = 5f;            // 보호 구역 가장자리 평지 여유

        // ── 데모 분석값 기반 ──
        const float RampSlope = 0.70f;     // tan35° (데모 p75 경사)
        const float EdgeBase = 28f;        // 가장자리 목표 높이 기본값 (25~35 요구)
        const float EdgeVariation = 7f;    // 방향별 ±변화
        const float MaxHeight = 38f;       // 최종 상한
        const float NoisePersistence = 0.55f;

        // 노이즈 옥타브 파장(m) — 능선 파장 270m를 200m 지형 규모로 축소
        static readonly float[] Wavelengths = { 90f, 45f, 22f, 11f };

        // 재현성을 위한 고정 오프셋
        const float OffRing = 10.37f, OffValley = 210.5f;
        static readonly Vector2[] Offs = { new Vector2(137.3f, 59.7f), new Vector2(311.7f, 173.1f), new Vector2(523.9f, 269.5f), new Vector2(77.7f, 419.3f) };

        [MenuItem("Tools/이문록/성하리 외곽 언덕 생성 (높이+텍스처)")]
        public static void Generate()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) { Debug.LogError("[HillGenerator] Terrain 없음"); return; }
            var td = terrain.terrainData;
            Vector3 tpos = terrain.transform.position;   // (-100, 0, -100)
            Vector3 tsize = td.size;                     // (200, 100, 200)
            int res = td.heightmapResolution;            // 513
            float cell = tsize.x / (res - 1);

            var original = td.GetHeights(0, 0, res, res);

            // ── 1. 보호 마스크 ─────────────────────────────
            var protect = new bool[res, res];
            int protectedCells = 0;

            // 1a. 마을 반경 70m 원
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float wx = tpos.x + cell * x, wz = tpos.z + cell * z;
                    float dx = wx - VillageCX, dz = wz - VillageCZ;
                    if (dx * dx + dz * dz <= ProtectRadius * ProtectRadius) { protect[z, x] = true; protectedCells++; }
                }

            // 1b. 반경 밖 배치물 발자국
            System.Action<float, float, float, float> stampRect = (minX, maxX, minZ, maxZ) =>
            {
                int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - tpos.x) / cell), 0, res - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - tpos.x) / cell), 0, res - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - tpos.z) / cell), 0, res - 1);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - tpos.z) / cell), 0, res - 1);
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++) protect[z, x] = true;
            };

            string[] structureGroups = { "성하리_집터", "성하리_건물", "성하리_소품" };
            int stamped = 0;
            foreach (var gname in structureGroups)
            {
                var g = GameObject.Find(gname);
                if (g == null) continue;
                foreach (var r in g.GetComponentsInChildren<Renderer>())
                {
                    var b = r.bounds;
                    float dx = b.center.x - VillageCX, dz = b.center.z - VillageCZ;
                    if (dx * dx + dz * dz < 55f * 55f) continue; // 원 안은 이미 보호됨
                    stampRect(b.min.x - 4f, b.max.x + 4f, b.min.z - 4f, b.max.z + 4f);
                    stamped++;
                }
            }
            // 나무는 캐노피가 아니라 밑동만 보호 (언덕이 숲 뒤로 자연스럽게 올라가도록)
            var veg = GameObject.Find("성하리_식생");
            if (veg != null)
            {
                foreach (var lod in veg.GetComponentsInChildren<LODGroup>())
                {
                    var p = lod.transform.position;
                    float dx = p.x - VillageCX, dz = p.z - VillageCZ;
                    if (dx * dx + dz * dz < 55f * 55f) continue;
                    stampRect(p.x - 3f, p.x + 3f, p.z - 3f, p.z + 3f);
                    stamped++;
                }
                foreach (var r in veg.GetComponentsInChildren<Renderer>())
                {
                    if (r.GetComponentInParent<LODGroup>() != null) continue;
                    var p = r.bounds.center;
                    float dx = p.x - VillageCX, dz = p.z - VillageCZ;
                    if (dx * dx + dz * dz < 55f * 55f) continue;
                    stampRect(p.x - 3f, p.x + 3f, p.z - 3f, p.z + 3f);
                    stamped++;
                }
            }

            // ── 2. 거리장 (chamfer 2-pass) ─────────────────
            const float INF = 1e9f;
            float orth = cell, diag = cell * 1.41421356f;
            var dist = new float[res, res];
            for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) dist[z, x] = protect[z, x] ? 0f : INF;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float d = dist[z, x];
                    if (x > 0) d = Mathf.Min(d, dist[z, x - 1] + orth);
                    if (z > 0) d = Mathf.Min(d, dist[z - 1, x] + orth);
                    if (x > 0 && z > 0) d = Mathf.Min(d, dist[z - 1, x - 1] + diag);
                    if (x < res - 1 && z > 0) d = Mathf.Min(d, dist[z - 1, x + 1] + diag);
                    dist[z, x] = d;
                }
            for (int z = res - 1; z >= 0; z--)
                for (int x = res - 1; x >= 0; x--)
                {
                    float d = dist[z, x];
                    if (x < res - 1) d = Mathf.Min(d, dist[z, x + 1] + orth);
                    if (z < res - 1) d = Mathf.Min(d, dist[z + 1, x] + orth);
                    if (x < res - 1 && z < res - 1) d = Mathf.Min(d, dist[z + 1, x + 1] + diag);
                    if (x > 0 && z < res - 1) d = Mathf.Min(d, dist[z + 1, x - 1] + diag);
                    dist[z, x] = d;
                }

            // ── 3. 높이 합성 ──────────────────────────────
            var h = new float[res, res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    if (protect[z, x]) { h[z, x] = original[z, x]; continue; }
                    float wx = tpos.x + cell * x, wz = tpos.z + cell * z;
                    float D = dist[z, x];
                    if (D <= Apron) { h[z, x] = original[z, x]; continue; }

                    // 3a. 보호 구역에서 멀어질수록 35° 경사로 상승
                    float ramp = RampSlope * (D - Apron);

                    // 3b. 방향별 가장자리 목표 높이 (능선 들쭉날쭉 + 고개 2곳)
                    float phi = Mathf.Atan2(wz - VillageCZ, wx - VillageCX);
                    float phiDeg = phi * Mathf.Rad2Deg;
                    float ring = Mathf.PerlinNoise(OffRing + Mathf.Cos(phi) * 1.5f, OffRing + Mathf.Sin(phi) * 1.5f);
                    float target = EdgeBase + EdgeVariation * (2f * ring - 1f);
                    target *= 1f - 0.88f * Gauss(Mathf.DeltaAngle(phiDeg, -90f), 15f); // 마을 입구 -Z 고개
                    target *= 1f - 0.88f * Gauss(Mathf.DeltaAngle(phiDeg, 0f), 13f);   // 은하담 +X 고개
                    // 큰 스케일 저지/고지 — 단조로움 방지 (계곡 섞기)
                    float valley = 0.75f + 0.5f * Mathf.PerlinNoise((wx + OffValley) / 140f, (wz + OffValley) / 140f);
                    target = Mathf.Clamp(target * valley, 0f, 36f);

                    // 3c. 지형 가장자리에 가까울수록 목표 높이에 도달
                    float dEdge = Mathf.Min(Mathf.Min(wx - tpos.x, tpos.x + tsize.x - wx),
                                            Mathf.Min(wz - tpos.z, tpos.z + tsize.z - wz));
                    float band = Mathf.Clamp01((60f - dEdge) / 60f);
                    band = band * band * (3f - 2f * band);
                    float cap = target * band;

                    float baseH = Mathf.Min(ramp, cap);

                    // 3d. 능선 노이즈 (데모 persistence 0.55, 상위 2옥타브는 ridged)
                    float mask = Mathf.Clamp01(baseH / 10f);
                    float amp = 6f;
                    float n = 0f;
                    for (int o = 0; o < Wavelengths.Length; o++)
                    {
                        float v = Mathf.PerlinNoise(wx / Wavelengths[o] + Offs[o].x, wz / Wavelengths[o] + Offs[o].y);
                        float s = o < 2 ? (1f - Mathf.Abs(2f * v - 1f) - 0.5f) * 2f  // ridged → 능선
                                        : 2f * v - 1f;
                        n += amp * s;
                        amp *= NoisePersistence;
                    }
                    h[z, x] = Mathf.Clamp(baseH + mask * n, 0f, MaxHeight) / tsize.y + original[z, x];
                }

            // ── 4. 스무딩 2회 (보호 구역은 이후 원복) ──────
            for (int pass = 0; pass < 2; pass++)
            {
                var src = (float[,])h.Clone();
                for (int z = 1; z < res - 1; z++)
                    for (int x = 1; x < res - 1; x++)
                    {
                        if (protect[z, x]) continue;
                        h[z, x] = (src[z, x] * 4f + src[z, x - 1] + src[z, x + 1] + src[z - 1, x] + src[z + 1, x]
                                   + 0.5f * (src[z - 1, x - 1] + src[z - 1, x + 1] + src[z + 1, x - 1] + src[z + 1, x + 1])) / 10f;
                    }
            }
            for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) if (protect[z, x]) h[z, x] = original[z, x];

            Undo.RegisterCompleteObjectUndo(td, "성하리 외곽 언덕 생성");
            td.SetHeights(0, 0, h);

            // ── 5. 결과 통계 ──────────────────────────────
            float maxH = 0f, maxSlope = 0f; int modified = 0;
            for (int z = 1; z < res - 1; z++)
                for (int x = 1; x < res - 1; x++)
                {
                    float hm = h[z, x] * tsize.y;
                    if (hm > maxH) maxH = hm;
                    if (Mathf.Abs(h[z, x] - original[z, x]) * tsize.y > 0.05f) modified++;
                    if (hm > 0.5f)
                    {
                        float sx = (h[z, x + 1] - h[z, x - 1]) * tsize.y / (2 * cell);
                        float sz = (h[z + 1, x] - h[z - 1, x]) * tsize.y / (2 * cell);
                        float sl = Mathf.Atan(Mathf.Sqrt(sx * sx + sz * sz)) * Mathf.Rad2Deg;
                        if (sl > maxSlope) maxSlope = sl;
                    }
                }

            PaintHills(td, tpos, tsize);
            EditorUtility.SetDirty(td);

            Debug.Log("[HillGenerator] 완료. 보호셀(마을)=" + protectedCells + ", 배치물 스탬프=" + stamped
                + "개, 변경셀=" + modified + ", 최고 " + maxH.ToString("F1") + "m, 최대경사 " + maxSlope.ToString("F1") + "°");
        }

        /// <summary>언덕 텍스처: 완경사 풀+흙, 급경사(28°부터) 바위 블렌드. 마을 바닥과는 낮은 곳에서 서서히 섞는다.</summary>
        static void PaintHills(TerrainData td, Vector3 tpos, Vector3 tsize)
        {
            // 레이어 인덱스 (Gyeonu_TerrainData 기준)
            const int L_MadangSoil = 1;  // 마당흙_낙안 (마을 바닥)
            const int L_Grass = 2;       // 풀_낙안
            const int L_Dirt = 4;        // 흙_소쇄원
            const int L_Boulder = 6;     // 바윗돌_소쇄원
            const int L_Rock = 10;       // 암반_세연정

            int ar = td.alphamapResolution;
            int L = td.terrainLayers.Length;
            var a = td.GetAlphamaps(0, 0, ar, ar);
            var w = new float[L];

            for (int z = 0; z < ar; z++)
                for (int x = 0; x < ar; x++)
                {
                    float nx = x / (ar - 1f), nz = z / (ar - 1f);
                    float wx = tpos.x + tsize.x * nx, wz = tpos.z + tsize.z * nz;
                    float hgt = td.GetInterpolatedHeight(nx, nz);
                    float steep = td.GetSteepness(nx, nz);
                    float rVC = Mathf.Sqrt((wx - VillageCX) * (wx - VillageCX) + (wz - VillageCZ) * (wz - VillageCZ));
                    if (rVC < ProtectRadius + 1f && hgt < 0.4f) continue; // 마을 안은 그대로

                    for (int l = 0; l < L; l++) w[l] = 0f;
                    float gn = Mathf.PerlinNoise(wx / 17f + 31.4f, wz / 17f + 62.8f);
                    float rockW = Mathf.InverseLerp(28f, 42f, steep);
                    float green = 1f - rockW;
                    w[L_Rock] = rockW * (0.6f + 0.25f * gn);
                    w[L_Boulder] = rockW - w[L_Rock];
                    w[L_Grass] = green * (0.55f + 0.3f * gn);
                    w[L_Dirt] = green - w[L_Grass];

                    float blend;
                    if (hgt >= 0.4f)
                        blend = Mathf.Clamp01((hgt - 0.4f) / 4f) * 0.95f;        // 언덕: 높이에 따라 침투
                    else
                    {
                        // 평지 외곽(고개 길 등): 마당흙 → 풀밭으로 서서히
                        blend = Mathf.Clamp01((rVC - 72f) / 18f) * 0.8f;
                        w[L_Rock] = w[L_Boulder] = 0f;
                        w[L_Grass] = 0.55f; w[L_Dirt] = 0.25f; w[L_MadangSoil] = 0.2f;
                    }

                    float sum = 0f;
                    for (int l = 0; l < L; l++)
                    {
                        a[z, x, l] = a[z, x, l] * (1f - blend) + w[l] * blend;
                        sum += a[z, x, l];
                    }
                    if (sum > 0.001f) for (int l = 0; l < L; l++) a[z, x, l] /= sum;
                }

            td.SetAlphamaps(0, 0, a);
        }

        static float Gauss(float d, float sigma) => Mathf.Exp(-(d * d) / (2f * sigma * sigma));
    }
}
