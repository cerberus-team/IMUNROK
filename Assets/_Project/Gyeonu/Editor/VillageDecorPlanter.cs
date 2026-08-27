using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 마을 내부 장식 배치 (벚나무·회양목·소형 바위) — 외곽과 달리 절제.
    ///
    /// 규칙 (2026-08-04 지시):
    ///   - 벚나무 중·소 위주 15~25그루 (대 3그루 이하), 집터 사이·담장 모서리·마당 가장자리
    ///   - 우물 광장(추정 -16,-8 — 우물 미배치라 문서 배치도 기준 가정) 가장자리에 그늘 나무 1~2
    ///   - 회양목 30~50개: 담장 밑·집 모서리·길가 (군락+한그루 혼합)
    ///   - 바위 15~25개: L/K/G/C 소형만, 절반쯤 매몰 일부(-0.2~-0.5)
    ///   - 길·마당·광장·출입구 앞 2m·입구→주막/우물 시선 확보
    ///   - 고정 시드 = 재실행 동일 결과. 그룹 삭제 후 재생성(멱등)
    /// </summary>
    public static class VillageDecorPlanter
    {
        const int Seed = 20260805;
        const string VegPath = "Assets/_Project/Gyeonu/Prefabs/Vegetation/";
        const string GroupName = "마을내부장식";

        const int CherryTarget = 20, CherryBigMax = 3;
        const int BushTarget = 40;
        const int RockTarget = 20;

        static readonly Vector2 Plaza = new Vector2(-16f, -8f);   // 우물 광장 추정 중심
        const float PlazaR = 7f;

        // 통로·시선 캡슐 (a→b, 반폭)
        static readonly float[,] Corr = {
            { -13,-72, -13,-40, 5f },   // 입구 길
            { -13,-40,  -4,-22, 5f },   // 입구→주막
            { -13,-68,   0,-20, 4f },   // 시선: 입구→주막
            { -13,-68, -16, -8, 4f },   // 시선: 입구→우물 광장
            {  -4,-18, -16, -8, 4f },   // 주막→광장
            {   8, 10,  45, 10, 5f },   // 중심→은하담(+X)
        };

        static readonly string[] CherrySmall = { "Cherry_벚나무JG_소_01", "Cherry_벚나무JG_소_05" };
        static readonly string[] CherryMid = { "Cherry_벚나무JG_중_01", "Cherry_벚나무JG_중_05" };
        static readonly string[] CherryBig = { "Cherry_벚나무JG_대_01", "Cherry_벚나무JG_대_05" };
        static readonly string[] Bushes = {
            "Bush_회양목JG_군락_01", "Bush_회양목JG_군락_02", "Bush_회양목JG_군락_03",
            "Bush_회양목JG_한그루_01", "Bush_회양목JG_한그루_02", "Bush_회양목JG_한그루_03" };
        // 삼각형은 작아도 실물 크기가 제각각: L 1.4m / G 2.7m / K 5.5m / C 4.5m (스케일 1 기준)
        // → L 주력, G는 축소+매몰, K/C는 마을 가장자리 반매몰 너럭바위 최대 2개
        static readonly string[] RocksSmall = { "SM_Rock_L_VR" };
        static readonly string[] RocksMid = { "SM_Rock_G_VR" };
        static readonly string[] RocksBig = { "SM_Rock_K_VR", "SM_Rock_C_VR" };
        const int RockBigMax = 2;

        [MenuItem("Tools/이문록/마을 내부 장식 배치")]
        public static void Plant()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            var veg = GameObject.Find("성하리_식생");
            if (terrain == null || veg == null) { Debug.LogError("[VillageDecorPlanter] Terrain/식생 그룹 없음"); return; }
            Vector3 tpos = terrain.transform.position;

            // ── 컨텍스트 수집 ──
            var buildingRects = new List<Rect>();   // 건물 본체 (출입구 보호용)
            var plotRects = new List<Rect>();       // 집터 콘텐츠 (마당 회피용)
            var wallSegs = new List<Bounds>();      // 담장·울타리 조각 (덤불 앵커)

            var bldRoot = GameObject.Find("성하리_건물");
            foreach (Transform c in bldRoot.transform)
            {
                if (c.name.EndsWith("_담장"))
                {
                    foreach (Transform w in c) if (w.name.Contains("직선")) wallSegs.Add(RendererBounds(w));
                    continue;
                }
                var b = RendererBounds(c);
                if (b.size.x > 0) buildingRects.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
            }
            var plotsRoot = GameObject.Find("성하리_집터");
            foreach (Transform plot in plotsRoot.transform)
            {
                var pb = RendererBounds(plot);
                if (pb.size.x > 0) plotRects.Add(Rect.MinMaxRect(pb.min.x, pb.min.z, pb.max.x, pb.max.z));
                foreach (Transform c in plot)
                {
                    string n = c.name;
                    if (n.StartsWith("Wall01c") || n.StartsWith("Wall02c") || n.StartsWith("Wall03c")) wallSegs.Add(RendererBounds(c));
                    else if (c.childCount >= 15) { var b = RendererBounds(c); buildingRects.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z)); }
                }
            }

            var vegPoints = new List<Vector2>();
            foreach (var t in veg.GetComponentsInChildren<Transform>(true))
                if (t != veg.transform && t.parent != null && (t.parent == veg.transform || t.parent.name == "외곽나무") && t.name != GroupName)
                    vegPoints.Add(new Vector2(t.position.x, t.position.z));

            var old = veg.transform.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject(GroupName).transform;
            group.SetParent(veg.transform, false);
            var gCherry = new GameObject("벚나무").transform; gCherry.SetParent(group, false);
            var gBush = new GameObject("덤불").transform; gBush.SetParent(group, false);
            var gRock = new GameObject("바위").transform; gRock.SetParent(group, false);

            var rnd = new System.Random(Seed);
            var newCherry = new List<Vector2>();
            var newBush = new List<Vector2>();
            var newRock = new List<Vector2>();

            // ── 1. 우물 광장 그늘 나무 2그루 (광장 가장자리, 시선 캡슐 회피) ──
            int planted = 0, bigUsed = 0;
            for (int attempt = 0; attempt < 200 && planted < 2; attempt++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var p = Plaza + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (PlazaR + 0.8f);
                if (InCorridor(p) || InAnyRect(buildingRects, p, 2f) || InAnyRect(plotRects, p, 0.5f)) continue;
                if (TooClose(vegPoints, p, 4.5f) || TooClose(newCherry, p, 5f)) continue;
                string species = planted == 0 && bigUsed < CherryBigMax ? CherryBig[rnd.Next(2)] : CherryMid[rnd.Next(2)];
                if (species.Contains("_대_")) bigUsed++;
                Place(gCherry, terrain, species, p, rnd, 0f);
                newCherry.Add(p); planted++;
            }

            // ── 2. 벚나무 나머지: 집터 사이 빈 공간·경계 근처 ──
            for (int attempt = 0; attempt < 4000 && planted < CherryTarget; attempt++)
            {
                var p = new Vector2(Mathf.Lerp(-50f, 42f, (float)rnd.NextDouble()), Mathf.Lerp(-40f, 58f, (float)rnd.NextDouble()));
                if (InCorridor(p) || (p - Plaza).magnitude < PlazaR) continue;
                if (InAnyRect(buildingRects, p, 2f) || InAnyRect(plotRects, p, 0.5f)) continue;
                float dPlot = MinRectDist(plotRects, p);
                if (dPlot > 6f && MinSegDist(wallSegs, p) > 4f) continue;   // 경계 근처 선호
                if (TooClose(vegPoints, p, 4.5f) || TooClose(newCherry, p, 5f)) continue;
                double roll = rnd.NextDouble();
                string species = roll < 0.1 && bigUsed < CherryBigMax ? CherryBig[rnd.Next(2)]
                               : roll < 0.45 ? CherryMid[rnd.Next(2)] : CherrySmall[rnd.Next(2)];
                if (species.Contains("_대_")) bigUsed++;
                Place(gCherry, terrain, species, p, rnd, 0f);
                newCherry.Add(p); planted++;
            }

            // ── 3. 회양목: 담장·울타리 밑 70%, 건물 모서리 30% ──
            int bushCount = 0;
            for (int attempt = 0; attempt < 6000 && bushCount < BushTarget; attempt++)
            {
                Vector2 p;
                if (rnd.NextDouble() < 0.7 && wallSegs.Count > 0)
                {
                    var seg = wallSegs[rnd.Next(wallSegs.Count)];
                    bool alongX = seg.size.x > seg.size.z;
                    float t = (float)rnd.NextDouble();
                    float off = 0.45f + (float)rnd.NextDouble() * 0.3f;
                    float side = rnd.NextDouble() < 0.5 ? 1f : -1f;
                    p = alongX
                        ? new Vector2(Mathf.Lerp(seg.min.x, seg.max.x, t), seg.center.z + side * (seg.extents.z + off))
                        : new Vector2(seg.center.x + side * (seg.extents.x + off), Mathf.Lerp(seg.min.z, seg.max.z, t));
                }
                else
                {
                    var r = buildingRects[rnd.Next(buildingRects.Count)];
                    var corner = new Vector2(rnd.NextDouble() < 0.5 ? r.xMin : r.xMax, rnd.NextDouble() < 0.5 ? r.yMin : r.yMax);
                    var dirOut = (corner - r.center).normalized;
                    p = corner + dirOut * (0.4f + (float)rnd.NextDouble() * 0.4f);
                }
                if (InCorridor(p) || (p - Plaza).magnitude < PlazaR) continue;
                if (InAnyRect(buildingRects, p, 0.3f)) continue;
                if (InAnyRect(plotRects, p, -0.3f) && rnd.NextDouble() < 0.7) continue;   // 마당 안은 최소한만
                if (TooClose(newBush, p, 1.0f) || TooClose(vegPoints, p, 0.8f)) continue;
                Place(gBush, terrain, Bushes[rnd.Next(Bushes.Length)], p, rnd, 0f);
                newBush.Add(p); bushCount++;
            }

            // ── 4. 바위: 담장 밑 50% / 나무 아래 30% / 길가 20%, 일부 매몰 ──
            int rockCount = 0, bigUsedRocks = 0;
            for (int attempt = 0; attempt < 6000 && rockCount < RockTarget; attempt++)
            {
                Vector2 p;
                double mode = rnd.NextDouble();
                if (mode < 0.5 && wallSegs.Count > 0)
                {
                    var seg = wallSegs[rnd.Next(wallSegs.Count)];
                    bool alongX = seg.size.x > seg.size.z;
                    float t = (float)rnd.NextDouble();
                    float off = 0.5f + (float)rnd.NextDouble() * 0.5f;
                    float side = rnd.NextDouble() < 0.5 ? 1f : -1f;
                    p = alongX
                        ? new Vector2(Mathf.Lerp(seg.min.x, seg.max.x, t), seg.center.z + side * (seg.extents.z + off))
                        : new Vector2(seg.center.x + side * (seg.extents.x + off), Mathf.Lerp(seg.min.z, seg.max.z, t));
                }
                else if (mode < 0.8 && newCherry.Count > 0)
                {
                    var c = newCherry[rnd.Next(newCherry.Count)];
                    float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                    p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (1f + (float)rnd.NextDouble() * 1.5f);
                }
                else
                {
                    int ci = rnd.Next(Corr.GetLength(0));
                    float t = (float)rnd.NextDouble();
                    var a = new Vector2(Corr[ci,0], Corr[ci,1]);
                    var b = new Vector2(Corr[ci,2], Corr[ci,3]);
                    var mid = Vector2.Lerp(a, b, t);
                    var dir = (b - a).normalized;
                    var perp = new Vector2(-dir.y, dir.x) * (rnd.NextDouble() < 0.5 ? 1f : -1f);
                    p = mid + perp * (Corr[ci,4] + 1f + (float)rnd.NextDouble() * 1.5f);
                }
                if (InCorridor(p) || (p - Plaza).magnitude < PlazaR) continue;
                if (InAnyRect(buildingRects, p, 0.8f)) continue;
                if (InAnyRect(plotRects, p, -0.5f)) continue;   // 마당 한가운데 금지 (울타리 1.5m 밴드는 허용)
                if (TooClose(newRock, p, 1.2f) || TooClose(newBush, p, 0.6f)) continue;

                double sizeRoll = rnd.NextDouble();
                string rockName; float scale; float bury;
                if (sizeRoll < 0.85 || bigUsedRocks >= RockBigMax)
                {
                    bool mid = sizeRoll >= 0.55 && sizeRoll < 0.85;
                    rockName = mid ? RocksMid[0] : RocksSmall[0];
                    scale = mid ? 0.6f + (float)rnd.NextDouble() * 0.3f : 0.7f + (float)rnd.NextDouble() * 0.6f;
                    bury = mid ? 0.25f + (float)rnd.NextDouble() * 0.25f
                               : (rnd.NextDouble() < 0.5 ? 0.2f + (float)rnd.NextDouble() * 0.3f : 0.05f);
                }
                else
                {
                    // 너럭바위: 광장·통로에서 멀리, 깊게 매몰
                    if ((p - Plaza).magnitude < 15f) continue;
                    rockName = RocksBig[rnd.Next(RocksBig.Length)];
                    scale = 0.6f + (float)rnd.NextDouble() * 0.15f;
                    bury = 0.4f + (float)rnd.NextDouble() * 0.2f;
                    bigUsedRocks++;
                }
                var rk = Place(gRock, terrain, rockName, p, rnd, -bury);
                if (rk != null) rk.transform.localScale = Vector3.one * scale;
                newRock.Add(p); rockCount++;
            }

            Debug.Log("[VillageDecorPlanter] 벚나무 " + planted + " (대 " + bigUsed + ") / 회양목 " + bushCount + " / 바위 " + rockCount);
            EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        }

        static GameObject Place(Transform parent, Terrain terrain, string prefabName, Vector2 p, System.Random rnd, float yOffset)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VegPath + prefabName + ".prefab");
            if (prefab == null) { Debug.LogError("[VillageDecorPlanter] 프리팹 없음: " + prefabName); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            float y = terrain.SampleHeight(new Vector3(p.x, 0, p.y)) + terrain.transform.position.y;
            go.transform.position = new Vector3(p.x, y + yOffset, p.y);
            go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
            float s = 0.7f + (float)rnd.NextDouble() * 0.6f;
            go.transform.localScale = new Vector3(s, s, s);
            return go;
        }

        static Bounds RendererBounds(Transform t)
        {
            Vector3 mn = Vector3.one * float.MaxValue, mx = Vector3.one * float.MinValue;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { mn = Vector3.Min(mn, r.bounds.min); mx = Vector3.Max(mx, r.bounds.max); }
            var b = new Bounds();
            if (mn.x <= mx.x) b.SetMinMax(mn, mx);
            return b;
        }

        static bool InCorridor(Vector2 p)
        {
            for (int i = 0; i < Corr.GetLength(0); i++)
            {
                var a = new Vector2(Corr[i,0], Corr[i,1]);
                var b = new Vector2(Corr[i,2], Corr[i,3]);
                if (SegDist(p, a, b) < Corr[i,4]) return true;
            }
            return false;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        static bool InAnyRect(List<Rect> rects, Vector2 p, float margin)
        {
            foreach (var r in rects)
                if (p.x > r.xMin - margin && p.x < r.xMax + margin && p.y > r.yMin - margin && p.y < r.yMax + margin) return true;
            return false;
        }

        static float MinRectDist(List<Rect> rects, Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var r in rects)
            {
                float dx = Mathf.Max(0, Mathf.Max(r.xMin - p.x, p.x - r.xMax));
                float dy = Mathf.Max(0, Mathf.Max(r.yMin - p.y, p.y - r.yMax));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < best) best = d;
            }
            return best;
        }

        static float MinSegDist(List<Bounds> segs, Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var s in segs)
            {
                float dx = Mathf.Max(0, Mathf.Max(s.min.x - p.x, p.x - s.max.x));
                float dz = Mathf.Max(0, Mathf.Max(s.min.z - p.y, p.y - s.max.z));
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < best) best = d;
            }
            return best;
        }

        static bool TooClose(List<Vector2> pts, Vector2 p, float minDist)
        {
            float sq = minDist * minDist;
            foreach (var q in pts) if ((q - p).sqrMagnitude < sq) return true;
            return false;
        }
    }
}
