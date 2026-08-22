// SeocheonOccluder.cs  (v3: 터 마스크 + 측정기반 능선 + 최소 안부)
// 마을→무당집·묘역, 무당집↔묘역 시선을 지형만으로 차단.
//  - 터 반경 12m는 현재 높이 유지(마스크), 그 바깥에서만 raise.
//  - 무당집 북/묘역 북 차폐 노브(측정 필요높이 기반) + 연결 능선 + 사이 능선.
//  - 길 안부는 최소폭. 개천+8m·마을(개천 북)·스플랫 불변. 하이트맵 백업.
// 메뉴: Tools/Seocheon/Mtn/BuildOccluders
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class SeocheonOccluder
{
    static Terrain terr; static TerrainData td; static int res; static Vector3 size, tpos; static float cx, cz;
    static float[,] H0;
    static List<Vector2> streamXZ, pathXZ;
    static Vector2 mudXZ, tombXZ, KM, KT;

    const float W1 = 24f;               // 연결 능선 반폭
    const float RKM = 20f, RKT = 15f;   // 노브 반경(v3=차폐 확보)
    const float WMID = 15f;             // 사이 능선 반폭
    const float PASS_HALF = 3f, PASS_BLEND = 4f; // 노면 보존(완만화는 사후 스무딩이 담당)
    const float ROADKEEP = 3.5f;        // 스무딩 시 노면 고정 반경
    const int   SMOOTH_PASSES = 3;      // 절개면 완만화 스무딩(3x3)
    const float TERR_MASK = 12f;        // 터 현재높이 유지 반경
    const float MASK_RAMP = 5f;
    const float END_FRAC = 0.16f;
    const float ZCAP = 556f;

    static readonly string[] OBS_NAME = { "광장", "외삼문앞", "동헌앞", "다리북단", "마루방", "주막", "아전", "남단" };
    static readonly Vector2[] OBS = {
        new Vector2(466,660), new Vector2(512,612), new Vector2(527,620), new Vector2(463,583),
        new Vector2(445,672), new Vector2(441,705), new Vector2(505,672), new Vector2(466,590) };

    static Vector3[] mudT; static Vector3[] tombT;

    [MenuItem("Tools/Seocheon/Mtn/BuildOccluders")]
    public static void Build()
    {
        terr = Terrain.activeTerrain; td = terr.terrainData; res = td.heightmapResolution; size = td.size; tpos = terr.transform.position;
        cx = size.x / (res - 1); cz = size.z / (res - 1);

        string tdPath = AssetDatabase.GetAssetPath(td);
        string bkPath = "Assets/_Project/Seocheon/Art/Terrain/_Backup/Seocheon_Village_Terrain_OccV3BK_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".asset";
        AssetDatabase.CopyAsset(tdPath, bkPath); Debug.Log("[Occ] 하이트맵 백업: " + bkPath);

        H0 = td.GetHeights(0, 0, res, res);
        var mk = GameObject.Find("_MtnMarkers").transform;
        var tMud = mk.Find("_Terrace_Mudang"); var tTomb = mk.Find("_Terrace_Tomb");
        mudXZ = new Vector2(tMud.position.x, tMud.position.z); tombXZ = new Vector2(tTomb.position.x, tTomb.position.z);
        pathXZ = new List<Vector2>();
        var pn = new List<Transform>(); foreach (Transform c in mk) if (c.name.StartsWith("_MtnPath_")) pn.Add(c);
        pn.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (var c in pn) pathXZ.Add(new Vector2(c.position.x, c.position.z));
        streamXZ = CollectStreamXZ();

        // 노브 위치: 각 터 북쪽(마을쪽) 12~13m, 측정된 관측선 교차점 부근
        KM = new Vector2(468f, 495f);   // 무당집 북(교차 ~466~472,490)
        KT = new Vector2(439f, 528f);   // 묘역 북(교차 ~440,524)

        float tombGround = SampleWorld(H0, tombXZ.x, tombXZ.y);
        mudT = new Vector3[] { new Vector3(466f, 30.97f, 481.7f), new Vector3(466f, 28.92f, 481.7f) };
        tombT = new Vector3[] { new Vector3(tombXZ.x, tombGround + 2.8f, tombXZ.y), new Vector3(tombXZ.x, tombGround + 1.4f, tombXZ.y) };

        float c1 = 23f;                 // 연결 능선(자연스러운 마루선)
        // 무당집 노브 상승
        float cM = 24f; for (; cM <= 44f; cM += 0.5f) { if (VillageBlocked(BuildH(c1, 0f, cM, 20f), mudT)) break; }
        // 묘역 노브 상승
        float cT = 20f; for (; cT <= 36f; cT += 0.5f) { if (VillageBlocked(BuildH(c1, 0f, cM, cT), tombT)) break; }
        // 사이 능선: 달성 가능한 무당집→묘역만 겨냥(묘역→무당집은 길 복도로 원리적 불가)
        float c2 = 21f; for (; c2 <= 30f; c2 += 0.5f) { if (MutualMudToTomb(BuildH(c1, c2, cM, cT))) break; }

        // 스무딩 여유 마진 → 완만화 후에도 차폐 유지
        cM += 2f; cT += 2f; c2 += 2f;
        var H = BuildH(c1, c2, cM, cT);
        // 차폐가 유지되는 한도까지만 절개면 완만화
        int sPass = 0;
        for (int pass = 0; pass < 12; pass++)
        {
            var H2 = (float[,])H.Clone(); SmoothOnce(H2);
            if (VillageBlocked(H2, mudT) && VillageBlocked(H2, tombT) && MutualMudToTomb(H2)) { H = H2; sPass++; } else break;
        }
        Debug.Log("[Occ] 완만화 스무딩 " + sPass + "회 적용(차폐 유지 한도). crest(+2마진) KM=" + cM + " KT=" + cT + " 사이=" + c2);
        td.SetHeights(0, 0, H); td.SyncHeightmap(); terr.Flush();
        var tcol = terr.GetComponent<TerrainCollider>(); if (tcol != null) tcol.terrainData = td;
        EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();

        Report(H, c1, c2, cM, cT);
    }

    // ================= 배경 산 결 디테일 (다중 옥타브 노이즈) =================
    [MenuItem("Tools/Seocheon/Mtn/AddMountainDetail")]
    public static void AddDetail()
    {
        terr = Terrain.activeTerrain; td = terr.terrainData; res = td.heightmapResolution; size = td.size; tpos = terr.transform.position;
        cx = size.x / (res - 1); cz = size.z / (res - 1);
        string tdPath = AssetDatabase.GetAssetPath(td);
        string bk = "Assets/_Project/Seocheon/Art/Terrain/_Backup/Seocheon_Village_Terrain_DetailBK_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".asset";
        AssetDatabase.CopyAsset(tdPath, bk); Debug.Log("[Detail] 백업: " + bk);

        var mk = GameObject.Find("_MtnMarkers").transform;
        mudXZ = new Vector2(mk.Find("_Terrace_Mudang").position.x, mk.Find("_Terrace_Mudang").position.z);
        tombXZ = new Vector2(mk.Find("_Terrace_Tomb").position.x, mk.Find("_Terrace_Tomb").position.z);
        pathXZ = new List<Vector2>();
        var pn = new List<Transform>(); foreach (Transform c in mk) if (c.name.StartsWith("_MtnPath_")) pn.Add(c);
        pn.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (var c in pn) pathXZ.Add(new Vector2(c.position.x, c.position.z));
        streamXZ = CollectStreamXZ();

        var baseH = td.GetHeights(0, 0, res, res);
        float tombGround = baseH[Mathf.Clamp((int)(tombXZ.y / cz), 0, res - 1), Mathf.Clamp((int)(tombXZ.x / cx), 0, res - 1)] * size.y + tpos.y;
        mudT = new Vector3[] { new Vector3(466f, 30.97f, 481.7f), new Vector3(466f, 28.92f, 481.7f) };
        tombT = new Vector3[] { new Vector3(tombXZ.x, tombGround + 2.8f, tombXZ.y), new Vector3(tombXZ.x, tombGround + 1.4f, tombXZ.y) };

        float medS = 1f, fineS = 1f; float[,] Hd = null; int tries = 0;
        for (; tries < 6; tries++)
        {
            Hd = NoiseField(baseH, medS, fineS);
            if (VillageBlocked(Hd, mudT) && VillageBlocked(Hd, tombT) && MutualMudToTomb(Hd)) break;
            medS *= 0.7f; fineS *= 0.7f;
        }
        td.SetHeights(0, 0, Hd); td.SyncHeightmap(); terr.Flush();
        var tcol = terr.GetComponent<TerrainCollider>(); if (tcol != null) tcol.terrainData = td;
        EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("== 옥타브 (현재 하이트맵 위에 가산) ==");
        sb.AppendLine("대(ridged 지맥) 파장~45m 진폭 3.0m(가산·하강없음) / 중(signed 골·어깨) ~15m 진폭 " + (1.2f * medS).ToString("F2") + "m / 잔(signed 거칠기) ~4.5m 진폭 " + (0.35f * fineS).ToString("F2") + "m");
        sb.AppendLine("경계·노면(±6m)·터(12m)·개천+8m·평지 페이드, 은폐 패치 " + tries + "회(중·잔 ×" + medS.ToString("F2") + ")");
        sb.AppendLine();
        sb.AppendLine("== 8곳 + 상호 재판정 ==");
        for (int i = 0; i < OBS.Length; i++)
        {
            Vector3 e = Eye(Hd, OBS[i]);
            bool bm = true; foreach (var t in mudT) if (!Blocked(Hd, e, t)) bm = false;
            bool bt = true; foreach (var t in tombT) if (!Blocked(Hd, e, t)) bt = false;
            sb.AppendLine("  " + OBS_NAME[i] + " 무당집 " + (bm ? "O" : "X") + "  묘역 " + (bt ? "O" : "X"));
        }
        sb.AppendLine("  상호 무당집→묘역 " + (MutualMudToTomb(Hd) ? "O" : "X") + "  (묘역→무당집은 길 복도로 원리적 불가)");
        Debug.Log(sb.ToString());
    }

    static float[,] NoiseField(float[,] baseH, float medS, float fineS)
    {
        var H = (float[,])baseH.Clone();
        float x0 = 402f, x1 = 498f, z0 = 478f, z1 = 553f;
        int r0 = Mathf.Clamp((int)(z0 / cz), 1, res - 2), r1 = Mathf.Clamp((int)(z1 / cz), 1, res - 2);
        int c0 = Mathf.Clamp((int)(x0 / cx), 1, res - 2), c1 = Mathf.Clamp((int)(x1 / cx), 1, res - 2);
        for (int row = r0; row <= r1; row++)
        {
            float wz = row * cz;
            for (int col = c0; col <= c1; col++)
            {
                float wx = col * cx; var p = new Vector2(wx, wz);
                if (NearStream(p)) continue;
                float curW = baseH[row, col] * size.y + tpos.y;
                float edge = Mathf.Min(Mathf.Min(wx - x0, x1 - wx), Mathf.Min(wz - z0, z1 - wz));
                float aEdge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 10f));
                float aRoad = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((DistToPath(p) - 6f) / 5f));
                float dmt = Mathf.Min((p - mudXZ).magnitude, (p - tombXZ).magnitude);
                float aTerr = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dmt - 12f) / 5f));
                float aHeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((curW - 14f) / 3f)); // 평지 제외
                float amp = aEdge * aRoad * aTerr * aHeight;
                if (amp <= 0.001f) continue;
                float pL = Mathf.PerlinNoise((wx + 13.1f) / 48f, (wz + 7.7f) / 48f);
                float pL2 = Mathf.PerlinNoise((wx + 200.5f) / 24f, (wz + 90.3f) / 24f);
                float large = (1f - Mathf.Abs(2f * pL - 1f)) * 3.6f + (1f - Mathf.Abs(2f * pL2 - 1f)) * 1.7f; // ridged 지맥 2옥타브(가산)
                float med = (Mathf.PerlinNoise((wx + 61.3f) / 15f, (wz + 29.9f) / 15f) - 0.5f) * 2f * 1.5f * medS;  // 골·어깨(±)
                float fine = (Mathf.PerlinNoise((wx + 113.7f) / 4.5f, (wz + 51.1f) / 4.5f) - 0.5f) * 2f * 0.4f * fineS;
                float add = (large + med + fine) * amp;
                H[row, col] = Mathf.Clamp01((curW + add - tpos.y) / size.y);
            }
        }
        return H;
    }

    static float[,] BuildH(float c1, float c2, float cM, float cT)
    {
        var H = (float[,])H0.Clone();
        int r0 = Mathf.Clamp((int)(470f / cz), 0, res - 1), r1 = Mathf.Clamp((int)(ZCAP / cz) + 1, 0, res - 1);
        int col0 = Mathf.Clamp((int)(400f / cx), 0, res - 1), col1 = Mathf.Clamp((int)(500f / cx) + 1, 0, res - 1);
        for (int row = r0; row <= r1; row++)
        {
            float wz = row * cz; if (wz > ZCAP) continue;
            for (int col = col0; col <= col1; col++)
            {
                float wx = col * cx; var p = new Vector2(wx, wz);
                if (NearStream(p)) continue;
                float baseW = H[row, col] * size.y + tpos.y;
                float add = 0f;
                add = Mathf.Max(add, RidgeAdd(p, new Vector2(410, 531), new Vector2(496, 531), W1, c1, baseW));
                add = Mathf.Max(add, RidgeAdd(p, new Vector2(469, 513), new Vector2(439, 485), WMID, c2, baseW));
                add = Mathf.Max(add, KnollAdd(p, KM, RKM, cM, baseW));
                add = Mathf.Max(add, KnollAdd(p, KT, RKT, cT, baseW));
                if (add <= 0f) continue;
                // 터 12m 현재높이 유지, 12~17m 램프
                float dmt = Mathf.Min((p - mudXZ).magnitude, (p - tombXZ).magnitude);
                if (dmt < TERR_MASK) continue;
                if (dmt < TERR_MASK + MASK_RAMP) add *= Mathf.SmoothStep(0f, 1f, (dmt - TERR_MASK) / MASK_RAMP);
                if (add <= 0f) continue;
                H[row, col] = Mathf.Clamp01((baseW + add - tpos.y) / size.y);
            }
        }
        return H;
    }

    // 절개면 완만화 1회 (노면·터·개천·마을 고정). 반환 없이 H 수정.
    static void SmoothOnce(float[,] H)
    {
        int r0 = Mathf.Clamp((int)(470f / cz), 1, res - 2), r1 = Mathf.Clamp((int)(ZCAP / cz), 1, res - 2);
        int col0 = Mathf.Clamp((int)(400f / cx), 1, res - 2), col1 = Mathf.Clamp((int)(500f / cx), 1, res - 2);
        var src = (float[,])H.Clone();
        for (int row = r0; row <= r1; row++)
        {
            float wz = row * cz;
            for (int col = col0; col <= col1; col++)
            {
                float wx = col * cx; var p = new Vector2(wx, wz);
                if (NearStream(p)) continue;
                float dmt = Mathf.Min((p - mudXZ).magnitude, (p - tombXZ).magnitude);
                if (dmt < TERR_MASK) continue;
                float dpp = DistToPath(p);
                if (dpp < ROADKEEP || dpp > 16f) continue;   // 통로 벽(절개면)만 완만화
                float sum = 0f; for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++) sum += src[row + dz, col + dx];
                H[row, col] = sum / 9f;
            }
        }
    }

    static float RidgeAdd(Vector2 p, Vector2 a, Vector2 b, float W, float crestH, float baseW)
    {
        if (crestH <= 0f) return 0f;
        Vector2 ab = b - a; float len = ab.magnitude; if (len < 1e-3f) return 0f;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / (len * len));
        float d = Vector2.Distance(p, a + ab * t); if (d >= W) return 0f;
        float f = 0.5f * (1f + Mathf.Cos(Mathf.PI * d / W));
        float et = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / END_FRAC)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / END_FRAC));
        float raw = crestH - baseW; if (raw <= 0f) return 0f;
        return raw * f * et * Saddle(p);
    }

    static float KnollAdd(Vector2 p, Vector2 c, float R, float crestH, float baseW)
    {
        if (crestH <= 0f) return 0f;
        float d = Vector2.Distance(p, c); if (d >= R) return 0f;
        float f = 0.5f * (1f + Mathf.Cos(Mathf.PI * d / R));
        float raw = crestH - baseW; if (raw <= 0f) return 0f;
        return raw * f * Saddle(p);
    }

    static float Saddle(Vector2 p)
    {
        float dp = DistToPath(p);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dp - PASS_HALF) / PASS_BLEND));
    }

    // ---------- verify ----------
    static bool VillageBlocked(float[,] H, Vector3[] targets)
    {
        for (int i = 0; i < OBS.Length; i++) { Vector3 e = Eye(H, OBS[i]); foreach (var t in targets) if (!Blocked(H, e, t)) return false; }
        return true;
    }
    static bool MutualMudToTomb(float[,] H)
    {
        Vector3 eM = new Vector3(mudXZ.x, SampleWorld(H, mudXZ.x, mudXZ.y) + 1.6f, mudXZ.y);
        foreach (var t in tombT) if (!Blocked(H, eM, t)) return false;
        return true;
    }
    static bool MutualBlocked(float[,] H)
    {
        Vector3 eM = new Vector3(mudXZ.x, SampleWorld(H, mudXZ.x, mudXZ.y) + 1.6f, mudXZ.y);
        Vector3 eT = new Vector3(tombXZ.x, SampleWorld(H, tombXZ.x, tombXZ.y) + 1.6f, tombXZ.y);
        foreach (var t in tombT) if (!Blocked(H, eM, t)) return false;
        foreach (var t in mudT) if (!Blocked(H, eT, t)) return false;
        return true;
    }
    static Vector3 Eye(float[,] H, Vector2 p) { return new Vector3(p.x, SampleWorld(H, p.x, p.y) + 1.6f, p.y); }
    static bool Blocked(float[,] H, Vector3 eye, Vector3 target)
    {
        float dist = Vector2.Distance(new Vector2(eye.x, eye.z), new Vector2(target.x, target.z));
        int steps = Mathf.Clamp((int)(dist * 2), 6, 800);
        for (int i = 2; i < steps - 1; i++) { Vector3 p = Vector3.Lerp(eye, target, (float)i / steps); if (SampleWorld(H, p.x, p.z) > p.y + 0.05f) return true; }
        return false;
    }
    static float MinClear(float[,] H, Vector3 eye, Vector3 target)
    {
        float dist = Vector2.Distance(new Vector2(eye.x, eye.z), new Vector2(target.x, target.z));
        int steps = Mathf.Clamp((int)(dist * 2), 6, 800); float mn = 1e9f;
        for (int i = 2; i < steps - 1; i++) { Vector3 p = Vector3.Lerp(eye, target, (float)i / steps); float g = p.y - SampleWorld(H, p.x, p.z); if (g < mn) mn = g; }
        return mn;
    }

    // ---------- report ----------
    static void Report(float[,] H, float c1, float c2, float cM, float cT)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("== 능선(터 12m 마스크, 안부 최소폭 " + PASS_HALF + "m) ==");
        sb.AppendLine(string.Format("무당집 북 노브 KM({0:F0},{1:F0}) r{2:F0} crest {3:F1}m", KM.x, KM.y, RKM, cM));
        sb.AppendLine(string.Format("묘역 북 노브 KT({0:F0},{1:F0}) r{2:F0} crest {3:F1}m", KT.x, KT.y, RKT, cT));
        sb.AppendLine(string.Format("연결 능선 z531 crest {0:F1}m / 사이 능선 crest {1:F1}m", c1, c2));
        sb.AppendLine();
        sb.AppendLine("== 관측점 8곳 (막힘O/뚫림X, 여유값 min(시선-지형)m) ==");
        for (int i = 0; i < OBS.Length; i++)
        {
            Vector3 e = Eye(H, OBS[i]);
            bool bm = true; float cm = 1e9f; foreach (var t in mudT) { if (!Blocked(H, e, t)) bm = false; cm = Mathf.Min(cm, MinClear(H, e, t)); }
            bool bt = true; float ct2 = 1e9f; foreach (var t in tombT) { if (!Blocked(H, e, t)) bt = false; ct2 = Mathf.Min(ct2, MinClear(H, e, t)); }
            sb.AppendLine(string.Format("  {0,-8} 무당집 {1}({2:+0.0;-0.0}) 묘역 {3}({4:+0.0;-0.0})", OBS_NAME[i], bm ? "O" : "X", cm, bt ? "O" : "X", ct2));
        }
        Vector3 eM = new Vector3(mudXZ.x, SampleWorld(H, mudXZ.x, mudXZ.y) + 1.6f, mudXZ.y);
        Vector3 eT = new Vector3(tombXZ.x, SampleWorld(H, tombXZ.x, tombXZ.y) + 1.6f, tombXZ.y);
        bool m2t = true; foreach (var t in tombT) if (!Blocked(H, eM, t)) m2t = false;
        bool t2m = true; foreach (var t in mudT) if (!Blocked(H, eT, t)) t2m = false;
        sb.AppendLine();
        sb.AppendLine("== 상호 == 무당집→묘역 " + (m2t ? "O" : "X") + "  묘역→무당집 " + (t2m ? "O" : "X"));
        sb.AppendLine();
        sb.AppendLine("== 터 = 길 드나드는 방향 열림? (터→최근접 path노드 방향으로 6/12m 지형이 안 오르막이면 열림) ==");
        sb.AppendLine("  무당집터: " + PathOpen(H, mudXZ));
        sb.AppendLine("  묘역터  : " + PathOpen(H, tombXZ));
        Debug.Log(sb.ToString());
    }

    static string PathOpen(float[,] H, Vector2 c)
    {
        Vector2 near = pathXZ[0]; float best = 1e9f;
        for (int i = 0; i < pathXZ.Count; i++) { float d = (pathXZ[i] - c).magnitude; if (d < best) { best = d; near = pathXZ[i]; } }
        Vector2 dir = (near - c).normalized; float ch = SampleWorld(H, c.x, c.y);
        float d6 = SampleWorld(H, c.x + dir.x * 6f, c.y + dir.y * 6f) - ch;
        float d12 = SampleWorld(H, c.x + dir.x * 12f, c.y + dir.y * 12f) - ch;
        bool open = d6 < 0.6f && d12 < 1.2f;
        return string.Format("최근접노드({0:F0},{1:F0}) 방향 6m Δ{2:+0.0;-0.0} 12m Δ{3:+0.0;-0.0} → {4}", near.x, near.y, d6, d12, open ? "열림(구덩이 아님)" : "막힘(주의)");
    }

    // ---------- helpers ----------
    static float SampleWorld(float[,] H, float wx, float wz)
    {
        float fx = wx / cx, fz = wz / cz;
        int x0 = Mathf.Clamp((int)fx, 0, res - 1), z0 = Mathf.Clamp((int)fz, 0, res - 1);
        int x1 = Mathf.Min(x0 + 1, res - 1), z1 = Mathf.Min(z0 + 1, res - 1);
        float tx = Mathf.Clamp01(fx - x0), tz = Mathf.Clamp01(fz - z0);
        float a = Mathf.Lerp(H[z0, x0], H[z0, x1], tx), b = Mathf.Lerp(H[z1, x0], H[z1, x1], tx);
        return Mathf.Lerp(a, b, tz) * size.y + tpos.y;
    }
    static float DistToPath(Vector2 p)
    {
        float best = 1e9f;
        for (int i = 0; i + 1 < pathXZ.Count; i++)
        {
            Vector2 a = pathXZ[i], b = pathXZ[i + 1], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }
    static List<Vector2> CollectStreamXZ()
    {
        var o = new List<Vector2>(); var go = GameObject.Find("_Stream_Water");
        if (go == null) foreach (var t in GameObject.FindObjectsOfType<Transform>()) if (t.name.Contains("Stream_Water")) { go = t.gameObject; break; }
        if (go == null) return o;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) { if (mf.sharedMesh == null) continue; var vs = mf.sharedMesh.vertices; var m = mf.transform.localToWorldMatrix; for (int i = 0; i < vs.Length; i += 6) { var w = m.MultiplyPoint3x4(vs[i]); o.Add(new Vector2(w.x, w.z)); } }
        return o;
    }
    static bool NearStream(Vector2 p) { float k2 = 64f; for (int i = 0; i < streamXZ.Count; i++) if ((streamXZ[i] - p).sqrMagnitude < k2) return true; return false; }
}
