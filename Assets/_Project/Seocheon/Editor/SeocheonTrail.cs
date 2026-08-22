// SeocheonTrail.cs — 성 밖 흙길을 진짜 산길처럼 재조성 (A안: 현재 2단 높이 유지, 외형만).
//  - 사행 ±7m(사면 비스듬히 가로지르게) · 비대칭 절성토(산쪽 절토/계곡쪽 성토) · 잔노이즈 · 스플랫 재도색.
//  - 좌표 하드코딩 없음: 마커·터·랜드마크·경로 전부 씬에서 읽음. 터/개천+8m/마을/능선디테일 보존.
//  - 은폐 재판정(마을 관측점=씬 랜드마크에서 도출) + 흙길 가시율.
// 메뉴: Tools/Seocheon/Mtn/RebuildTrail
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class SeocheonTrail
{
    const float ROAD_HALF = 1.75f, BLEND_OUT = 6.5f, FADE = 6f;
    const float WANDER = 7f, WANDER_WL = 28f;
    const float CUT_GRADE = 0.85f;   // 산쪽 절토 rise/run (1:1.2)
    const float FILL_GRADE = 0.55f;  // 계곡쪽 성토 (1:1.8)
    const float TERR_MASK = 12f, ZCAP = 556f;
    const float FINE_AMP = 0.4f;
    const int DIRT = 1;              // 흙 레이어

    static Terrain terr; static TerrainData td; static int res; static Vector3 size, tpos; static float cx, cz;
    static List<Vector2> streamXZ;
    static Vector2 mudXZ, tombXZ;
    static float[,] H0;

    class RP { public Vector2 p; public Vector2 perp; public float uph; public float prof; public float arc; }
    static List<RP> road;

    [MenuItem("Tools/Seocheon/Mtn/RebuildTrail")]
    public static void Rebuild()
    {
        terr = Terrain.activeTerrain; td = terr.terrainData; res = td.heightmapResolution; size = td.size; tpos = terr.transform.position;
        cx = size.x / (res - 1); cz = size.z / (res - 1);
        string tdPath = AssetDatabase.GetAssetPath(td);
        string bk = "Assets/_Project/Seocheon/Art/Terrain/_Backup/Seocheon_Village_Terrain_RebuildTrailBK_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".asset";
        AssetDatabase.CopyAsset(tdPath, bk); Debug.Log("[Trail] 백업: " + bk);

        var mk = GameObject.Find("_MtnMarkers").transform;
        mudXZ = new Vector2(mk.Find("_Terrace_Mudang").position.x, mk.Find("_Terrace_Mudang").position.z);
        tombXZ = new Vector2(mk.Find("_Terrace_Tomb").position.x, mk.Find("_Terrace_Tomb").position.z);
        var pnT = new List<Transform>(); foreach (Transform c in mk) if (c.name.StartsWith("_MtnPath_")) pnT.Add(c);
        pnT.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var poly = new List<Vector2>(); foreach (var c in pnT) poly.Add(new Vector2(c.position.x, c.position.z));
        streamXZ = CollectStream();
        H0 = td.GetHeights(0, 0, res, res);

        BuildRoad(poly);                      // 사행 중심선 + 종단 프로파일

        // 지형 등급(절성토) — H0 기준으로 새 배열 생성
        var H = (float[,])H0.Clone();
        GradeTerrain(H);
        td.SetHeights(0, 0, H); td.SyncHeightmap(); terr.Flush();
        var tcol = terr.GetComponent<TerrainCollider>(); if (tcol != null) tcol.terrainData = td;

        // 오브젝트 동반이동 (터 높이 delta) — A안이라 delta≈0
        string objLog = LiftTerraceObjects(H);

        // 스플랫 재도색 (새 노면)
        RepaintSplat(H);

        EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Report(H, objLog);
    }

    // ---------- 도로(사행 중심선 + 프로파일) ----------
    static void BuildRoad(List<Vector2> poly)
    {
        // 리샘플 2m
        var pts = new List<Vector2>(); var arcs = new List<float>();
        float acc = 0f; pts.Add(poly[0]); arcs.Add(0f);
        for (int i = 0; i + 1 < poly.Count; i++)
        {
            Vector2 a = poly[i], b = poly[i + 1]; float len = (b - a).magnitude; int n = Mathf.Max(1, (int)(len / 2f));
            for (int k = 1; k <= n; k++) { float t = (float)k / n; acc = arcs[arcs.Count - 1] + (Vector2.Lerp(a, b, t) - pts[pts.Count - 1]).magnitude; pts.Add(Vector2.Lerp(a, b, t)); arcs.Add(acc); }
        }
        float total = arcs[arcs.Count - 1];
        // 무당집/묘역 아크 (최근접)
        float tombArc = 0f, mudArc = 0f; float dT = 1e9f, dM = 1e9f;
        for (int i = 0; i < pts.Count; i++) { float a = (pts[i] - tombXZ).magnitude; if (a < dT) { dT = a; tombArc = arcs[i]; } float b = (pts[i] - mudXZ).magnitude; if (b < dM) { dM = b; mudArc = arcs[i]; } }
        float hBridge = H0Sample(pts[0].x, pts[0].y);
        float hTomb = H0Sample(tombXZ.x, tombXZ.y);
        float hMud = H0Sample(mudXZ.x, mudXZ.y);
        float hEnd = H0Sample(pts[pts.Count - 1].x, pts[pts.Count - 1].y);

        road = new List<RP>();
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 dir; if (i + 1 < pts.Count) dir = (pts[i + 1] - pts[i]).normalized; else dir = (pts[i] - pts[i - 1]).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            // 사행: 저주파 노이즈, 터/양끝 근처는 0으로 테이퍼
            float w = (Mathf.PerlinNoise(arcs[i] / WANDER_WL, 3.3f) - 0.5f) * 2f * WANDER;
            float taper = Mathf.Clamp01(arcs[i] / 10f) * Mathf.Clamp01((total - arcs[i]) / 10f)
                          * Mathf.Clamp01(Mathf.Abs(arcs[i] - tombArc) / 8f) * Mathf.Clamp01(Mathf.Abs(arcs[i] - mudArc) / 8f);
            Vector2 wp = pts[i] + perp * (w * taper);
            // 종단: 앵커 smoothstep
            float prof = ProfileH(arcs[i], tombArc, mudArc, total, hBridge, hTomb, hMud, hEnd);
            // 산쪽(uphill) 판정: 기존지형 perp ±6m 비교
            float hu = H0Sample(wp.x + perp.x * 6f, wp.y + perp.y * 6f);
            float hd = H0Sample(wp.x - perp.x * 6f, wp.y - perp.y * 6f);
            float uph = (hu >= hd) ? 1f : -1f;
            road.Add(new RP { p = wp, perp = perp, uph = uph, prof = prof, arc = arcs[i] });
        }
    }

    static float ProfileH(float a, float tA, float mA, float total, float hB, float hT, float hM, float hE)
    {
        // 앵커: 0=hB, tA=hT(±10m 평탄), mA=hM(±10m 평탄), total=hE
        if (a <= tA) return SLerp(hB, hT, a / Mathf.Max(1f, tA));
        if (a <= mA) return SLerp(hT, hM, (a - tA) / Mathf.Max(1f, mA - tA));
        return SLerp(hM, hE, (a - mA) / Mathf.Max(1f, total - mA));
    }
    static float SLerp(float a, float b, float t) { t = Mathf.Clamp01(t); return Mathf.Lerp(a, b, t * t * (3f - 2f * t)); }

    // ---------- 절성토 ----------
    static void GradeTerrain(float[,] H)
    {
        float rmax = BLEND_OUT + FADE;
        // 경계 박스
        float minx = 1e9f, maxx = -1e9f, minz = 1e9f, maxz = -1e9f;
        foreach (var r in road) { minx = Mathf.Min(minx, r.p.x); maxx = Mathf.Max(maxx, r.p.x); minz = Mathf.Min(minz, r.p.y); maxz = Mathf.Max(maxz, r.p.y); }
        int c0 = Mathf.Clamp((int)((minx - rmax) / cx), 0, res - 1), c1 = Mathf.Clamp((int)((maxx + rmax) / cx) + 1, 0, res - 1);
        int r0 = Mathf.Clamp((int)((minz - rmax) / cz), 0, res - 1), r1 = Mathf.Clamp((int)((maxz + rmax) / cz) + 1, 0, res - 1);
        for (int row = r0; row <= r1; row++)
        {
            float wz = row * cz; if (wz > ZCAP) continue;
            for (int col = c0; col <= c1; col++)
            {
                float wx = col * cx; var q = new Vector2(wx, wz);
                if (NearStream(q)) continue;
                float dmt = Mathf.Min((q - mudXZ).magnitude, (q - tombXZ).magnitude);
                if (dmt < TERR_MASK) continue;                       // 터 12m 보존
                // 최근접 road point
                int bi = -1; float bd = 1e9f, bsign = 0f;
                for (int i = 0; i < road.Count; i++) { float d = (road[i].p - q).sqrMagnitude; if (d < bd) { bd = d; bi = i; } }
                var rp = road[bi]; float dist = Mathf.Sqrt(bd);
                if (dist > rmax) continue;
                float signedD = Vector2.Dot(q - rp.p, rp.perp);
                bool uphillSide = (Mathf.Sign(signedD) == rp.uph);
                float existing = H[row, col] * size.y + tpos.y;
                float roadH = rp.prof;
                float target;
                if (dist <= ROAD_HALF) target = roadH;
                else
                {
                    float o = dist - ROAD_HALF;
                    float face;
                    if (uphillSide) face = Mathf.Min(existing, roadH + o * CUT_GRADE);   // 절토
                    else face = Mathf.Max(existing, roadH - o * FILL_GRADE);             // 성토
                    if (dist > BLEND_OUT) { float f = Mathf.SmoothStep(0f, 1f, (dist - BLEND_OUT) / FADE); face = Mathf.Lerp(face, existing, f); }
                    target = face;
                }
                // 잔 옥타브만 약하게
                float fine = (Mathf.PerlinNoise(wx / 4.5f + 11.7f, wz / 4.5f + 5.3f) - 0.5f) * 2f * FINE_AMP;
                float roadW = Mathf.Clamp01(1f - (dist - ROAD_HALF) / BLEND_OUT);
                target += fine * (0.4f + 0.6f * roadW);
                H[row, col] = Mathf.Clamp01((target - tpos.y) / size.y);
            }
        }
    }

    // ---------- 오브젝트 동반이동 ----------
    static string LiftTerraceObjects(float[,] H)
    {
        var sb = new System.Text.StringBuilder();
        var outside = GameObject.Find("_Outside");
        foreach (var terrace in new[] { new { c = mudXZ, nm = "무당집" }, new { c = tombXZ, nm = "묘역" } })
        {
            float before = H0Sample(terrace.c.x, terrace.c.y);
            float after = SampleWorld(H, terrace.c.x, terrace.c.y);
            float delta = after - before;
            if (outside != null)
                foreach (Transform t in outside.transform)
                {
                    var b = ObjBounds(t); if (b == null) continue;
                    var cxz = new Vector2(((Bounds)b).center.x, ((Bounds)b).center.z);
                    if ((cxz - terrace.c).magnitude < TERR_MASK && Mathf.Abs(delta) > 0.01f)
                    { t.position = new Vector3(t.position.x, t.position.y + delta, t.position.z); sb.Append(terrace.nm + ":" + t.name + " ΔY" + delta.ToString("F2") + " "); }
                }
            sb.Append("[" + terrace.nm + "터 delta " + delta.ToString("F2") + "] ");
        }
        return sb.ToString();
    }

    // ---------- 스플랫 재도색 ----------
    static void RepaintSplat(float[,] H)
    {
        int aRes = td.alphamapResolution; var A = td.GetAlphamaps(0, 0, aRes, aRes); int nL = td.alphamapLayers;
        float rmax = 6f;
        float minx = 1e9f, maxx = -1e9f, minz = 1e9f, maxz = -1e9f;
        foreach (var r in road) { minx = Mathf.Min(minx, r.p.x); maxx = Mathf.Max(maxx, r.p.x); minz = Mathf.Min(minz, r.p.y); maxz = Mathf.Max(maxz, r.p.y); }
        int az0 = Mathf.Clamp((int)((minz - rmax - tpos.z) / size.z * (aRes - 1)), 0, aRes - 1), az1 = Mathf.Clamp((int)((maxz + rmax - tpos.z) / size.z * (aRes - 1)) + 1, 0, aRes - 1);
        int ax0 = Mathf.Clamp((int)((minx - rmax - tpos.x) / size.x * (aRes - 1)), 0, aRes - 1), ax1 = Mathf.Clamp((int)((maxx + rmax - tpos.x) / size.x * (aRes - 1)) + 1, 0, aRes - 1);
        for (int az = az0; az <= az1; az++)
        {
            float wz = (float)az / (aRes - 1) * size.z + tpos.z; if (wz > ZCAP) continue;
            for (int ax = ax0; ax <= ax1; ax++)
            {
                float wx = (float)ax / (aRes - 1) * size.x + tpos.x; var q = new Vector2(wx, wz);
                if (NearStream(q)) continue;
                float bd = 1e9f; int bi = -1;
                for (int i = 0; i < road.Count; i++) { float d = (road[i].p - q).sqrMagnitude; if (d < bd) { bd = d; bi = i; } }
                float dist = Mathf.Sqrt(bd); if (dist > rmax) continue;
                float feather = 1.5f + (Mathf.PerlinNoise(wx * 0.2f, wz * 0.2f)) * 1.0f;   // 1.5~2.5 불규칙
                float dirt;
                if (dist <= ROAD_HALF) dirt = 1f;
                else if (dist <= ROAD_HALF + feather) dirt = 1f - Mathf.Clamp01((dist - ROAD_HALF) / feather);
                else continue;
                // 경사 25° 넘으면 대비 낮춤
                float slope = TerrainSlope(H, wx, wz);
                if (slope > 0.466f) dirt *= 0.55f;
                float cur = A[az, ax, DIRT]; float nd = Mathf.Max(cur, dirt);
                if (nd <= cur + 1e-5f) continue;
                float rest = 1f - nd; float others = 0f; for (int L = 0; L < nL; L++) if (L != DIRT) others += A[az, ax, L];
                A[az, ax, DIRT] = nd;
                if (others > 0.0001f) { float k = rest / others; for (int L = 0; L < nL; L++) if (L != DIRT) A[az, ax, L] *= k; }
                else A[az, ax, DIRT] = 1f;
            }
        }
        td.SetAlphamaps(0, 0, A);
    }

    // ---------- 보고/검증 ----------
    static void Report(float[,] H, string objLog)
    {
        var sb = new System.Text.StringBuilder();
        // 실제 적용 상승/경사/굽이
        float hB = road[0].prof, hT = 0, hM = 0; float gmax = 0; float gsum = 0; int gn = 0;
        for (int i = 1; i < road.Count; i++) { float dh = Mathf.Abs(road[i].prof - road[i - 1].prof); float dl = (road[i].p - road[i - 1].p).magnitude; if (dl > 0.01f) { float g = dh / dl; gmax = Mathf.Max(gmax, g); gsum += g; gn++; } }
        float hTb = H0Sample(tombXZ.x, tombXZ.y), hMd = H0Sample(mudXZ.x, mudXZ.y);
        // 굽이 개수: 사행 부호 변화
        int bends = 0; float prevSign = 0;
        for (int i = 1; i < road.Count; i++) { float s = Vector2.Dot(road[i].p - road[i - 1].p, road[0].perp); }
        // 사행 진폭 기준 부호변화 근사
        Vector2 baseDir = (road[road.Count - 1].p - road[0].p).normalized; Vector2 basePerp = new Vector2(-baseDir.y, baseDir.x); float ps = 0;
        for (int i = 0; i < road.Count; i++) { float off = Vector2.Dot(road[i].p - road[0].p, basePerp); float s = Mathf.Sign(off); if (i > 0 && s != ps && Mathf.Abs(off) > 2f) bends++; ps = s; }

        sb.AppendLine("== 실제 적용 ==");
        sb.AppendLine("상승: 다리 " + hB.ToString("F2") + " → 묘역 " + hTb.ToString("F2") + "(+" + (hTb - hB).ToString("F2") + ") → 무당집 " + hMd.ToString("F2") + "(+" + (hMd - hTb).ToString("F2") + ")  [A안: 현재 유지]");
        sb.AppendLine("종단경사 평균 " + (gsum / Mathf.Max(1, gn) * 100f).ToString("F1") + "% / 최대 " + (gmax * 100f).ToString("F1") + "%  · 사행 진폭 ±" + WANDER + "m · 굽이 " + bends + "개");
        sb.AppendLine("절성토: 산쪽 절토 1:" + (1f / CUT_GRADE).ToString("F1") + " / 계곡쪽 성토 1:" + (1f / FILL_GRADE).ToString("F1") + " (비대칭)");
        sb.AppendLine("오브젝트 이동: " + objLog);
        sb.AppendLine();

        // 관측점 = 씬 랜드마크에서 도출 (좌표 하드코딩 없음)
        var obs = VillageObs();
        var mudT = new Vector3[] { MudTop(), new Vector3(mudXZ.x, hMd + 3.0f, mudXZ.y) };
        var tombT = new Vector3[] { new Vector3(tombXZ.x, hTb + 2.8f, tombXZ.y), new Vector3(tombXZ.x, hTb + 1.4f, tombXZ.y) };
        sb.AppendLine("== 은폐 재판정 (관측점=씬 랜드마크) ==");
        foreach (var o in obs)
        {
            Vector3 e = new Vector3(o.Value.x, SampleWorld(H, o.Value.x, o.Value.y) + 1.6f, o.Value.y);
            bool bm = true; foreach (var t in mudT) if (!Blocked(H, e, t)) bm = false;
            bool bt = true; foreach (var t in tombT) if (!Blocked(H, e, t)) bt = false;
            sb.AppendLine("  " + o.Key + " 무당집 " + (bm ? "O" : "X") + " 묘역 " + (bt ? "O" : "X"));
        }
        Vector3 eM = new Vector3(mudXZ.x, SampleWorld(H, mudXZ.x, mudXZ.y) + 1.6f, mudXZ.y);
        bool m2t = true; foreach (var t in tombT) if (!Blocked(H, eM, t)) m2t = false;
        sb.AppendLine("  상호 무당집→묘역 " + (m2t ? "O" : "X") + " (묘역→무당집은 길 복도로 원리적 불가)");

        // 흙길 가시율: road 중심선 샘플, 마을에서 보이는가
        int vis30 = 0, tot30 = 0, visAfter = 0, totAfter = 0;
        for (int i = 0; i < road.Count; i++)
        {
            var rp = road[i]; float rh = SampleWorld(H, rp.p.x, rp.p.y) + 1.0f;
            bool seen = false;
            foreach (var o in obs) { Vector3 e = new Vector3(o.Value.x, SampleWorld(H, o.Value.x, o.Value.y) + 1.6f, o.Value.y); if (!Blocked(H, e, new Vector3(rp.p.x, rh, rp.p.y))) { seen = true; break; } }
            if (rp.arc <= 30f) { tot30++; if (seen) vis30++; } else { totAfter++; if (seen) visAfter++; }
        }
        sb.AppendLine();
        sb.AppendLine("== 흙길 가시율 ==");
        sb.AppendLine("  초입 30m: " + vis30 + "/" + tot30 + " 보임 (진입로는 보여야 정상)");
        sb.AppendLine("  이후 구간: " + visAfter + "/" + totAfter + " 보임 (0에 가까워야 정상 — 첫 굽이 뒤 은폐)");
        Debug.Log(sb.ToString());
    }

    static List<KeyValuePair<string, Vector2>> VillageObs()
    {
        var o = new List<KeyValuePair<string, Vector2>>();
        var br = GameObject.Find("_Bridge"); if (br != null) { var b = ObjBoundsC(br.transform); o.Add(new KeyValuePair<string, Vector2>("다리북단", new Vector2(b.center.x, b.max.z + 2f))); }
        var gw = GameObject.Find("GwanaGateSet"); if (gw != null) { var b = ObjBoundsC(gw.transform); o.Add(new KeyValuePair<string, Vector2>("외삼문앞", new Vector2(b.center.x, b.min.z - 3f))); }
        var don = GameObject.Find("Donheon"); if (don != null) o.Add(new KeyValuePair<string, Vector2>("동헌앞", new Vector2(don.transform.position.x, don.transform.position.z - 10f)));
        var sed = GameObject.Find("_Village_Sedae");
        if (sed != null)
        {
            Vector2 cen = Vector2.zero; int n = 0; float minZ = 1e9f; Vector2 minZc = Vector2.zero;
            foreach (Transform s in sed.transform) { var b = ObjBoundsC(s); var c = new Vector2(b.center.x, b.center.z); cen += c; n++; if (c.y < minZ) { minZ = c.y; minZc = c; } o.Add(new KeyValuePair<string, Vector2>("가옥:" + s.name, c)); }
            if (n > 0) o.Add(new KeyValuePair<string, Vector2>("광장", cen / n));
        }
        return o;
    }
    static Vector3 MudTop()
    {
        var h = GameObject.Find("_Outside").transform.Find("Mudang_House");
        var rs = h.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    // ---------- helpers ----------
    static float H0Sample(float wx, float wz) { return SampleWorld(H0, wx, wz); }
    static float SampleWorld(float[,] H, float wx, float wz)
    {
        float fx = wx / cx, fz = wz / cz;
        int x0 = Mathf.Clamp((int)fx, 0, res - 1), z0 = Mathf.Clamp((int)fz, 0, res - 1);
        int x1 = Mathf.Min(x0 + 1, res - 1), z1 = Mathf.Min(z0 + 1, res - 1);
        float tx = Mathf.Clamp01(fx - x0), tz = Mathf.Clamp01(fz - z0);
        float a = Mathf.Lerp(H[z0, x0], H[z0, x1], tx), b = Mathf.Lerp(H[z1, x0], H[z1, x1], tx);
        return Mathf.Lerp(a, b, tz) * size.y + tpos.y;
    }
    static float TerrainSlope(float[,] H, float wx, float wz)
    {
        float hL = SampleWorld(H, wx - cx, wz), hR = SampleWorld(H, wx + cx, wz), hD = SampleWorld(H, wx, wz - cz), hU = SampleWorld(H, wx, wz + cz);
        float gx = (hR - hL) / (2f * cx), gz = (hU - hD) / (2f * cz);
        return Mathf.Sqrt(gx * gx + gz * gz);
    }
    static bool Blocked(float[,] H, Vector3 eye, Vector3 target)
    {
        float dist = Vector2.Distance(new Vector2(eye.x, eye.z), new Vector2(target.x, target.z));
        int steps = Mathf.Clamp((int)(dist * 2), 6, 800);
        for (int i = 2; i < steps - 1; i++) { Vector3 p = Vector3.Lerp(eye, target, (float)i / steps); if (SampleWorld(H, p.x, p.z) > p.y + 0.05f) return true; }
        return false;
    }
    static Bounds ObjBoundsC(Transform t) { var rs = t.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); return b; }
    static object ObjBounds(Transform t) { var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return null; var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); return b; }
    static List<Vector2> CollectStream()
    {
        var o = new List<Vector2>(); var go = GameObject.Find("_Stream_Water");
        if (go == null) foreach (var t in GameObject.FindObjectsOfType<Transform>()) if (t.name.Contains("Stream_Water")) { go = t.gameObject; break; }
        if (go == null) return o;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) { if (mf.sharedMesh == null) continue; var vs = mf.sharedMesh.vertices; var m = mf.transform.localToWorldMatrix; for (int i = 0; i < vs.Length; i += 6) { var w = m.MultiplyPoint3x4(vs[i]); o.Add(new Vector2(w.x, w.z)); } }
        return o;
    }
    static bool NearStream(Vector2 p) { float k2 = 64f; for (int i = 0; i < streamXZ.Count; i++) if ((streamXZ[i] - p).sqrMagnitude < k2) return true; return false; }
}
