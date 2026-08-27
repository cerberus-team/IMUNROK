// SeocheonRidge.cs — 앞 능선에 배경 산맥과 같은 이방성 침식 결 부여.
//  IdentifyMeasure(1단계): 백업 + 해상도 + [A]시선기준 대상특정 + [B]국소좌표 측정 + 오버레이 부감.
//  좌표 하드코딩 없음: 마커·랜드마크·하이트맵에서 도출.
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class SeocheonRidge
{
    static Terrain terr; static TerrainData td; static int res; static Vector3 size, tpos; static float cell;
    static float[,] H;
    static bool[,] target;
    static Vector2 mudXZ, tombXZ;
    static List<Vector2> streamXZ, pathXZ;

    [MenuItem("Tools/Seocheon/Mtn/Ridge_IdentifyMeasure")]
    public static void IdentifyMeasure()
    {
        terr = Terrain.activeTerrain; td = terr.terrainData; res = td.heightmapResolution; size = td.size; tpos = terr.transform.position;
        cell = size.x / (res - 1);
        string tdPath = AssetDatabase.GetAssetPath(td);
        string bk = "Assets/_Project/Seocheon/Art/Terrain/_Backup/Seocheon_Village_Terrain_RidgeBK_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".asset";
        AssetDatabase.CopyAsset(tdPath, bk);
        H = td.GetHeights(0, 0, res, res);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[0] 백업: " + bk);
        sb.AppendLine("[5선행] heightmapResolution=" + res + ", 셀 " + cell.ToString("F3") + "m → 에일리어싱 한계 골파장 ≥ " + (cell * 4f).ToString("F1") + "m");

        var mk = GameObject.Find("_MtnMarkers").transform;
        mudXZ = new Vector2(mk.Find("_Terrace_Mudang").position.x, mk.Find("_Terrace_Mudang").position.z);
        tombXZ = new Vector2(mk.Find("_Terrace_Tomb").position.x, mk.Find("_Terrace_Tomb").position.z);
        pathXZ = new List<Vector2>(); var pn = new List<Transform>(); foreach (Transform c in mk) if (c.name.StartsWith("_MtnPath_")) pn.Add(c);
        pn.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); foreach (var c in pn) pathXZ.Add(new Vector2(c.position.x, c.position.z));
        streamXZ = CollectStream();

        // ===== [A] 시선기준 대상 =====
        var brgo = GameObject.Find("_Bridge"); var bb = ObjB(brgo.transform);
        float bx = bb.center.x, bz = bb.min.z;   // 다리 남단(성밖쪽)
        Vector3 eye = new Vector3(bx, WH(bx, bz) + 1.6f, bz);
        var crest = new List<Vector2>();
        for (int deg = -60; deg <= 60; deg++)
        {
            float a = deg * Mathf.Deg2Rad; Vector2 dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));  // 남향 부채꼴
            float bestAng = 0.001f; Vector2 bestP = Vector2.zero; bool found = false;
            for (float d = 6f; d <= 130f; d += 1f)
            {
                Vector2 p = new Vector2(bx, bz) + dir * d; if (p.x < 2 || p.x > size.x - 2 || p.y < 2 || p.y > size.z - 2) break;
                float ang = Mathf.Atan2(WH(p.x, p.y) - eye.y, d);
                if (ang > bestAng) { bestAng = ang; bestP = p; found = true; }
            }
            if (found) crest.Add(bestP);
        }
        // 마루셀에서 경사하강 확장 (BFS), 정지: 경사<12° / 노면±6 / 터12 / 마루서 25m 초과
        target = new bool[res, res];
        var seedCells = new List<int>();
        foreach (var cp in crest) { int r = Ri(cp.y), c = Ci(cp.x); if (!target[r, c]) { target[r, c] = true; seedCells.Add(r * res + c); } }
        // 각 셀의 최근접 마루점 거리 판정용
        var q = new Queue<int>(); foreach (var s in seedCells) q.Enqueue(s);
        int marked = seedCells.Count;
        while (q.Count > 0)
        {
            int id = q.Dequeue(); int r = id / res, c = id % res; float wx = c * cell, wz = r * cell;
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue; int nr = r + dz, nc = c + dx; if (nr < 1 || nr >= res - 1 || nc < 1 || nc >= res - 1) continue; if (target[nr, nc]) continue;
                    float nwx = nc * cell, nwz = nr * cell; var np = new Vector2(nwx, nwz);
                    if (SlopeDeg(nwx, nwz) < 12f) continue;                 // 골/평지 정지
                    if (DistPath(np) < 6f) continue;                       // 노면±6
                    if ((np - mudXZ).magnitude < 12f || (np - tombXZ).magnitude < 12f) continue; // 터12
                    if (NearStream(np)) continue;
                    float dc = 1e9f; foreach (var cp in crest) dc = Mathf.Min(dc, (cp - np).magnitude); if (dc > 25f) continue; // 마루서 25m
                    target[nr, nc] = true; marked++; q.Enqueue(nr * res + nc);
                }
        }
        // 연결성분 수 + bbox 채움률 + 무당집 포함여부
        int comps = CountComps(); int br0 = res, br1 = 0, bc0 = res, bc1 = 0; foreach (var pp2 in AllTrue()) { br0 = Mathf.Min(br0, pp2 / res); br1 = Mathf.Max(br1, pp2 / res); bc0 = Mathf.Min(bc0, pp2 % res); bc1 = Mathf.Max(bc1, pp2 % res); }
        float fill = (float)marked / Mathf.Max(1, (br1 - br0 + 1) * (bc1 - bc0 + 1));
        bool mudIn = target[Ri(mudXZ.y), Ci(mudXZ.x)];
        sb.AppendLine();
        sb.AppendLine("[A] 시선기준 대상: 마루점 " + crest.Count + "개, 마크셀 " + marked + " (면적 " + (marked * cell * cell).ToString("F0") + "m²)");
        sb.AppendLine("  bbox x[" + (bc0 * cell).ToString("F0") + "~" + (bc1 * cell).ToString("F0") + "] z[" + (br0 * cell).ToString("F0") + "~" + (br1 * cell).ToString("F0") + "], 채움률 " + fill.ToString("F2") + ", 연결성분 " + comps + "개, 무당집 포함=" + (mudIn ? "예(실패!)" : "아니오"));
        if (fill > 0.8f) sb.AppendLine("  ⚠ 채움률>0.8 → 직사각형 의심(실패 가능)");
        if (comps < 2) sb.AppendLine("  ⚠ 연결성분 " + comps + "개 → 좌우 두 덩어리로 안 갈라짐");

        // ===== [B] 국소좌표 측정 =====
        Vector2 cenM = Vector2.zero; foreach (var pp in pathXZ) cenM += pp; cenM /= pathXZ.Count;
        Vector2 bgc = FindBackground(cenM);
        var bg = LocalStats(bgc, 30f); var fg = LocalStatsRegion();
        sb.AppendLine();
        sb.AppendLine("[B] 국소좌표 측정 (경사≥25° 셀만, high-pass λ~8m)");
        sb.AppendLine("배경창 중심(" + bgc.x.ToString("F0") + "," + bgc.y.ToString("F0") + ") 표본 " + bg.nSamp + "셀" + (bg.nSamp < 500 ? " ⚠<500 신뢰불가" : "") + " / 앞능선 표본 " + fg.nSamp + "셀" + (fg.nSamp < 500 ? " ⚠<500" : ""));
        sb.AppendLine("항목 | 배경산 | 앞능선");
        sb.AppendLine("  골파장(등고선 1st-min)m | " + bg.groove.ToString("F1") + " | " + fg.groove.ToString("F1"));
        sb.AppendLine("  이방성비(낙하선/등고선) | " + bg.aniso.ToString("F2") + " | " + fg.aniso.ToString("F2"));
        sb.AppendLine("  낙하선 상관길이m | " + bg.clFall.ToString("F1") + " | " + fg.clFall.ToString("F1"));
        sb.AppendLine("  등고선 상관길이m | " + bg.clCont.ToString("F1") + " | " + fg.clCont.ToString("F1"));
        sb.AppendLine("  경사 중앙값° | " + bg.slopeMed.ToString("F1") + " | " + fg.slopeMed.ToString("F1"));
        if (bg.groove < cell * 4f) sb.AppendLine("  ⚠ 배경 골파장 < 4셀(" + (cell * 4f).ToString("F1") + "m) → 에일리어싱, 목표 상향 필요");
        if (bg.nSamp < 500 || bg.groove <= 0.1f) sb.AppendLine("  ⚠ 측정 신뢰불가 → [C] 후보봉 표시 필요(사용자 선택)");

        System.IO.Directory.CreateDirectory(@"C:\Users\User\_Renders");
        System.IO.File.WriteAllText(@"C:\Users\User\_Renders\ridge_report.txt", sb.ToString());
        Debug.Log("[Ridge] 리포트: C:/Users/User/_Renders/ridge_report.txt");
        OverlayRender();
    }

    struct St { public float groove, aniso, clFall, clCont, slopeMed; public int nSamp; }

    // 국소좌표 자기상관: 경사≥25° 셀 표본, 각 셀 낙하선/등고선 방향 1D 자기상관 집계
    static St LocalStats(Vector2 c, float half)
    {
        var cells = new List<Vector2>();
        for (float wx = c.x - half; wx <= c.x + half; wx += cell) for (float wz = c.y - half; wz <= c.y + half; wz += cell)
            { if (SlopeDeg(wx, wz) >= 25f) cells.Add(new Vector2(wx, wz)); }
        return AutoCorr(cells);
    }
    static St LocalStatsRegion()
    {
        var cells = new List<Vector2>(); foreach (var id in AllTrue()) { int r = id / res, cc = id % res; float wx = cc * cell, wz = r * cell; if (SlopeDeg(wx, wz) >= 20f) cells.Add(new Vector2(wx, wz)); }
        return AutoCorr(cells);
    }
    static St AutoCorr(List<Vector2> cells)
    {
        int L = 16; var fall = new double[L + 1]; var cont = new double[L + 1]; var fn = new int[L + 1]; var cn = new int[L + 1];
        var slopes = new List<float>();
        foreach (var p in cells)
        {
            Vector2 g = Grad(p.x, p.y); if (g.magnitude < 1e-3f) continue; Vector2 fd = g.normalized; Vector2 cd = new Vector2(-fd.y, fd.x);
            slopes.Add(Mathf.Atan(g.magnitude) * Mathf.Rad2Deg);
            float h0 = HP(p.x, p.y);
            for (int lag = 0; lag <= L; lag++)
            {
                Vector2 pf = p + fd * (lag * cell); Vector2 pc = p + cd * (lag * cell);
                fall[lag] += h0 * HP(pf.x, pf.y); fn[lag]++; cont[lag] += h0 * HP(pc.x, pc.y); cn[lag]++;
            }
        }
        var s = new St(); s.nSamp = cells.Count;
        if (cells.Count < 5 || fall[0] <= 0) { s.groove = 0; s.aniso = 1; return s; }
        double f0 = fall[0] / fn[0], c0 = cont[0] / cn[0];
        // 정규화 배열
        var fc = new float[L + 1]; var cc2 = new float[L + 1];
        for (int lag = 0; lag <= L; lag++) { fc[lag] = (float)((fall[lag] / fn[lag]) / f0); cc2[lag] = (float)((cont[lag] / cn[lag]) / c0); }
        // 골파장: 등고선 자기상관 첫 극소점
        int minLag = 0; for (int lag = 2; lag < L; lag++) { if (cc2[lag] < cc2[lag - 1] && cc2[lag] < cc2[lag + 1]) { minLag = lag; break; } }
        s.groove = minLag * cell * 2f;   // 첫 극소=반파장 → ×2
        // 상관길이 (1/e)
        s.clFall = CorrLen(fc) * cell; s.clCont = CorrLen(cc2) * cell;
        s.aniso = s.clCont < 1e-3f ? 1f : s.clFall / s.clCont;
        slopes.Sort(); s.slopeMed = slopes.Count > 0 ? slopes[slopes.Count / 2] : 0;
        return s;
    }
    static float CorrLen(float[] ac) { for (int lag = 1; lag < ac.Length; lag++) if (ac[lag] < 0.37f) return lag; return ac.Length - 1; }
    static float HP(float wx, float wz) { return WH(wx, wz) - Smooth(wx, wz, 4); }  // high-pass λ~8m
    static float Smooth(float wx, float wz, int rad) { float s = 0; int n = 0; for (int dz = -rad; dz <= rad; dz++) for (int dx = -rad; dx <= rad; dx++) { s += WH(wx + dx * cell, wz + dz * cell); n++; } return s / n; }

    static Vector2 FindBackground(Vector2 cen)
    {
        float bestH = -1e9f; Vector2 best = cen + new Vector2(120, 0);
        for (float wx = 60; wx < size.x - 60; wx += 12f) for (float wz = 60; wz < size.z - 60; wz += 12f) { var p = new Vector2(wx, wz); if ((p - cen).magnitude < 80f) continue; float h = WH(wx, wz); if (h > bestH) { bestH = h; best = p; } }
        return best;
    }

    // ---------- 오버레이 부감(직교) ----------
    static void OverlayRender()
    {
        int br0 = res, br1 = 0, bc0 = res, bc1 = 0; foreach (var id in AllTrue()) { br0 = Mathf.Min(br0, id / res); br1 = Mathf.Max(br1, id / res); bc0 = Mathf.Min(bc0, id % res); bc1 = Mathf.Max(bc1, id % res); }
        float ccx = (bc0 + bc1) * 0.5f * cell, ccz = (br0 + br1) * 0.5f * cell;
        float span = Mathf.Max((bc1 - bc0) * cell, (br1 - br0) * cell) * 0.7f + 80f;
        var camGO = new GameObject("_OrthoCam"); var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = span; cam.clearFlags = CameraClearFlags.Skybox; cam.farClipPlane = 3000;
        int W = 1400, Ht = 1400; float aspect = 1f; cam.aspect = aspect;
        cam.transform.position = new Vector3(ccx, 300f, ccz); cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        var rt = new RenderTexture(W, Ht, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(W, Ht, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, Ht), 0, 0); tex.Apply();
        var px = tex.GetPixels();
        for (int py = 0; py < Ht; py++) for (int pxi = 0; pxi < W; pxi++)
            {
                float wx = ccx + ((float)pxi / (W - 1) - 0.5f) * (2f * span); float wz = ccz + ((float)py / (Ht - 1) - 0.5f) * (2f * span);
                int r = Mathf.Clamp((int)(wz / cell), 0, res - 1), c = Mathf.Clamp((int)(wx / cell), 0, res - 1);
                if (target[r, c]) { int idx = py * W + pxi; var col = px[idx]; px[idx] = new Color(Mathf.Min(1, col.r * 0.4f + 0.6f), col.g * 0.4f, col.b * 0.4f); }
            }
        tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(@"C:\Users\User\IMUNROK\Assets\Screenshots\ridge_overlay2.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex); RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(camGO);
        Debug.Log("[Ridge] 오버레이2 저장: ridge_overlay2.png (빨강=대상)");
    }

    // ---------- helpers ----------
    static int Ri(float wz) { return Mathf.Clamp((int)(wz / cell), 0, res - 1); }
    static int Ci(float wx) { return Mathf.Clamp((int)(wx / cell), 0, res - 1); }
    static float WH(float wx, float wz) { return terr.SampleHeight(new Vector3(wx, 0, wz)) + tpos.y; }
    static Vector2 Grad(float wx, float wz) { float hx = (WH(wx + cell, wz) - WH(wx - cell, wz)) / (2 * cell); float hz = (WH(wx, wz + cell) - WH(wx, wz - cell)) / (2 * cell); return new Vector2(hx, hz); }
    static float SlopeDeg(float wx, float wz) { return Mathf.Atan(Grad(wx, wz).magnitude) * Mathf.Rad2Deg; }
    static float DistPath(Vector2 p) { float best = 1e9f; for (int i = 0; i + 1 < pathXZ.Count; i++) { Vector2 a = pathXZ[i], b = pathXZ[i + 1], ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude)); best = Mathf.Min(best, Vector2.Distance(p, a + ab * t)); } return best; }
    static List<int> AllTrue() { var o = new List<int>(); for (int r = 0; r < res; r++) for (int c = 0; c < res; c++) if (target[r, c]) o.Add(r * res + c); return o; }
    static int CountComps()
    {
        var seen = new bool[res, res]; int comps = 0;
        for (int r = 0; r < res; r++) for (int c = 0; c < res; c++)
            {
                if (!target[r, c] || seen[r, c]) continue; comps++; var st = new Stack<int>(); st.Push(r * res + c);
                while (st.Count > 0) { int id = st.Pop(); int rr = id / res, cc = id % res; if (rr < 0 || rr >= res || cc < 0 || cc >= res) continue; if (seen[rr, cc] || !target[rr, cc]) continue; seen[rr, cc] = true; st.Push((rr + 1) * res + cc); st.Push((rr - 1) * res + cc); st.Push(rr * res + cc + 1); st.Push(rr * res + cc - 1); }
            }
        return comps;
    }
    static Bounds ObjB(Transform t) { var rs = t.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); return b; }
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
