// SeocheonGroundFit.cs
// 씬의 모든 건물 밑바닥을 "정점 단위"로 읽고, 그 높이에 맞춰 지형(높이맵)만 완만하게 수정한다.
// 건물/프리팹 Transform 은 절대 건드리지 않는다. 스플랫(페인트)도 건드리지 않는다.
// 개천 링(_Stream_Water) + 정남 다리(_Bridge) 밑 지형은 마스크로 완전 잠근다.
// 메뉴: Tools/Seocheon/GroundFit/Fit  (Scan -> Apply -> Verify -> Capture 를 한 번에)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class SeocheonGroundFit
{
    // ---- 파라미터 ----
    const float CORE_MARGIN  = 1.0f;   // footprint 바깥 이만큼까지는 baseY 로 완전 평탄
    const float FALLOFF      = 6.0f;   // 그 바깥 6m 에 걸쳐 원래 지형으로 smoothstep 복귀
    const float STREAM_KEEP  = 8.0f;   // 개천/다리 마스크 여유(m)
    const float PASS_TOL     = 0.03f;  // 판정 허용 |gap|
    const float BIG_CHANGE   = 2.5f;   // 이 이상 지형이 오르내리면 별도 보고
    const float LOWBAND      = 0.6f;   // 접지부 정점 높이 필터
    const float LOWBAND_WIDE = 1.2f;   // 접지부 정점 30개 미만 시 확장
    const float BUILDING_MINDIM    = 1.5f; // 세대 하위 pad 대상 판정: footprint 최대변 (담장 포함)
    const float BUILDING_MINHEIGHT = 1.4f; // 세대 하위 pad 대상 판정: 메시 높이 (담장 1.7m 포함, 소품 제외)
    const string RENDER_DIR  = @"C:\Users\User\_Renders\GroundFit\";

    static readonly string[] CONTAINERS = { "_Village_Sedae", "_Village_Landscape", "_Outside" };
    static readonly string[] SINGLETONS = { "GwanaGateSet" };
    static readonly string[] MASK_OBJS  = { "_Stream_Water", "_Bridge" };

    class Root
    {
        public string name;
        public Transform tr;
        public bool isSedae;
        public float baseY, topY;
        public float minX, maxX, minZ, maxZ;   // 접지 footprint AABB
        public int vtxCount, lowBandCount;
        public float bandUsed;
        public bool skipped;
        public bool nonFit;                     // 담장/소품 등 pad 미생성(블렌드 탑승)
        public string skipReason;
        // 검증
        public float gapBeforeMax, gapAfterMax;
        public float centerDelta; // 지형 변화량(중심)
        public float _cB;         // 적용 전 중심 지형높이
    }

    [MenuItem("Tools/Seocheon/GroundFit/Fit")]
    public static void Fit()
    {
        var log = new StringBuilder();
        var terrain = Terrain.activeTerrain;
        if (terrain == null) { Debug.LogError("[GroundFit] 활성 Terrain 없음. 중단."); return; }
        var td = terrain.terrainData;

        // ===== 시작 전 필수: 백업 =====
        string src = AssetDatabase.GetAssetPath(td);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        const string bkDir = "Assets/_Project/Seocheon/Art/Terrain/_Backup";
        if (!AssetDatabase.IsValidFolder(bkDir))
            AssetDatabase.CreateFolder("Assets/_Project/Seocheon/Art/Terrain", "_Backup");
        string dst = bkDir + "/Seocheon_Village_Terrain_GFBK_" + stamp + ".asset";
        bool okBk = AssetDatabase.CopyAsset(src, dst);
        if (!okBk) { Debug.LogError("[GroundFit] 백업 실패. 중단. src=" + src); return; }
        AssetDatabase.SaveAssets();
        log.AppendLine("BACKUP: " + dst);

        // ===== 대상 루트 수집 =====
        var roots = new List<Root>();
        foreach (var cn in CONTAINERS)
        {
            var c = GameObject.Find(cn);
            if (c == null) continue;
            if (cn == "_Village_Sedae")
            {
                // 세대 안의 '개별 건물' 단위로 세분화(집마다 바닥 높이가 달라서 세대 통짜로는 뜬다)
                foreach (Transform sede in c.transform)
                    foreach (Transform bld in sede.transform)
                        roots.Add(new Root { name = sede.name + "/" + bld.name, tr = bld, isSedae = true });
            }
            else
            {
                foreach (Transform child in c.transform)
                    roots.Add(new Root { name = cn + "/" + child.name, tr = child, isSedae = false });
            }
        }
        foreach (var sn in SINGLETONS)
        {
            var g = GameObject.Find(sn);
            if (g != null) roots.Add(new Root { name = sn, tr = g.transform });
        }

        // ===== 1) 정점 단위 baseY + 2) 접지 footprint =====
        foreach (var r in roots) ScanRoot(r);

        // 세대 하위 자식 중 '건물'만 pad 생성(담장·소품은 nonFit → 블렌드에 얹힘)
        foreach (var r in roots)
        {
            if (r.skipped || !r.isSedae) continue;
            float maxdim = Mathf.Max(r.maxX - r.minX, r.maxZ - r.minZ);
            float h = r.topY - r.baseY;
            if (maxdim < BUILDING_MINDIM || h < BUILDING_MINHEIGHT) r.nonFit = true;
        }
        var valid = roots.FindAll(rr => !rr.skipped && !rr.nonFit);
        if (valid.Count == 0) { Debug.LogError("[GroundFit] 유효 루트 0. 중단."); return; }

        // ===== 지형 그리드 =====
        int res = td.heightmapResolution;
        Vector3 size = td.size;
        Vector3 tpos = terrain.transform.position;
        float cx = size.x / (res - 1), cz = size.z / (res - 1);
        float[,] H0 = td.GetHeights(0, 0, res, res);     // 원본
        float[,] H  = (float[,])H0.Clone();              // 작업본

        // ===== 개천/다리 마스크 =====
        bool[,] mask = BuildMask(res, size, tpos, cx, cz);
        int maskedCells = 0;
        for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) if (mask[z, x]) maskedCells++;

        // ===== 검증용 '적용 전' 지형 높이 (5점 gap + 중심) =====
        foreach (var r in valid) { r.gapBeforeMax = GapMax(terrain, r, out float cbef); r._cB = cbef; }

        // ===== 3) 지형 수정 (가중 평균 blend) =====
        // 영향 영역 bbox
        float rMinX = 1e9f, rMaxX = -1e9f, rMinZ = 1e9f, rMaxZ = -1e9f;
        foreach (var r in valid) { rMinX = Mathf.Min(rMinX, r.minX); rMaxX = Mathf.Max(rMaxX, r.maxX); rMinZ = Mathf.Min(rMinZ, r.minZ); rMaxZ = Mathf.Max(rMaxZ, r.maxZ); }
        float pad = CORE_MARGIN + FALLOFF + 2f;
        int colMin = Mathf.Clamp(Mathf.FloorToInt((rMinX - pad - tpos.x) / cx), 0, res - 1);
        int colMax = Mathf.Clamp(Mathf.CeilToInt((rMaxX + pad - tpos.x) / cx), 0, res - 1);
        int rowMin = Mathf.Clamp(Mathf.FloorToInt((rMinZ - pad - tpos.z) / cz), 0, res - 1);
        int rowMax = Mathf.Clamp(Mathf.CeilToInt((rMaxZ + pad - tpos.z) / cz), 0, res - 1);

        for (int row = rowMin; row <= rowMax; row++)
        {
            float wz = row * cz + tpos.z;
            for (int col = colMin; col <= colMax; col++)
            {
                if (mask[row, col]) continue; // 개천/다리 잠금
                float wx = col * cx + tpos.x;
                float origH = H0[row, col] * size.y + tpos.y;
                float sumW = 0f, sumWB = 0f;
                for (int i = 0; i < valid.Count; i++)
                {
                    var r = valid[i];
                    float d = RectDist(wx, wz, r.minX, r.maxX, r.minZ, r.maxZ);
                    if (d > CORE_MARGIN + FALLOFF) continue;
                    float w = 1f - Smoothstep(CORE_MARGIN, CORE_MARGIN + FALLOFF, d);
                    if (w <= 0f) continue;
                    sumW += w; sumWB += w * r.baseY;
                }
                if (sumW <= 0f) continue;
                float w0 = Mathf.Max(0f, 1f - sumW);
                float newH = (sumWB + w0 * origH) / (sumW + w0);
                newH = Mathf.Clamp(newH, 0f, size.y);
                H[row, col] = Mathf.Clamp01((newH - tpos.y) / size.y);
            }
        }

        // 경계만 3x3 가벼운 스무딩 1회 (마스크 밖 셀에만)
        float[,] Hs = (float[,])H.Clone();
        for (int row = rowMin + 1; row < rowMax; row++)
            for (int col = colMin + 1; col < colMax; col++)
            {
                if (mask[row, col]) continue;
                float s = 0; int c = 0;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (mask[row + dz, col + dx]) continue;
                        s += H[row + dz, col + dx]; c++;
                    }
                if (c > 0) Hs[row, col] = s / c;
            }
        H = Hs;

        td.SetHeights(0, 0, H);
        td.SyncHeightmap();
        terrain.Flush();
        var tc = terrain.GetComponent<TerrainCollider>(); if (tc != null) tc.terrainData = td;
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

        // ===== 4) 검증 =====
        foreach (var r in valid) { r.gapAfterMax = GapMax(terrain, r, out float caft); r.centerDelta = caft - r._cB; }

        int pass = 0, fail = 0, nonFitCount = 0; float maxResid = 0f;
        var failList = new List<Root>();
        var bigList = new List<Root>();
        var skipList = new List<Root>();

        log.AppendLine();
        log.AppendLine("== GroundFit 결과표 (건물/트리/관아 pad 루트만; 담장·소품은 blend 탑승) ==");
        log.AppendLine(string.Format("{0,-30} {1,7} {2,9} {3,9} {4,12} {5,6}",
            "ROOT", "baseY", "gapBefr", "gapAftr", "footprint", "judge"));
        foreach (var r in roots)
        {
            if (r.skipped) { skipList.Add(r); continue; }
            if (r.nonFit) { nonFitCount++; continue; }
            bool ok = r.gapAfterMax <= PASS_TOL;
            if (ok) pass++; else { fail++; failList.Add(r); }
            maxResid = Mathf.Max(maxResid, r.gapAfterMax);
            if (Mathf.Abs(r.centerDelta) > BIG_CHANGE) bigList.Add(r);
            log.AppendLine(string.Format("{0,-30} {1,7:F2} {2,9:F2} {3,9:F3} {4,5:F0}x{5,-6:F0} {6,6}",
                Trunc(r.name, 30), r.baseY, r.gapBeforeMax, r.gapAfterMax,
                (r.maxX - r.minX), (r.maxZ - r.minZ), ok ? "PASS" : "FAIL"));
        }

        log.AppendLine();
        log.AppendLine("-- FAIL (|gap|>" + PASS_TOL.ToString("F2") + ") --");
        if (failList.Count == 0) log.AppendLine("  (없음)");
        else foreach (var r in failList) log.AppendLine(string.Format("  {0}  residGap={1:F3}", Trunc(r.name, 34), r.gapAfterMax));

        log.AppendLine();
        log.AppendLine("-- BIG CHANGE (|지형변화|>" + BIG_CHANGE.ToString("F1") + "m, 파묻힘 오브젝트 의심) --");
        if (bigList.Count == 0) log.AppendLine("  (없음)");
        else foreach (var r in bigList) log.AppendLine(string.Format("  {0}  centerDelta={1:+0.00;-0.00}m", Trunc(r.name, 34), r.centerDelta));

        log.AppendLine();
        log.AppendLine("-- SKIPPED (접지 정점 부족) --");
        if (skipList.Count == 0) log.AppendLine("  (없음)");
        else foreach (var r in skipList) log.AppendLine(string.Format("  {0}  ({1})", Trunc(r.name, 34), r.skipReason));

        log.AppendLine();
        log.AppendLine("== 요약 ==");
        log.AppendLine("  총 루트 : " + roots.Count + "  (pad생성 " + valid.Count + " / 담장·소품 blend " + nonFitCount + " / 스킵 " + skipList.Count + ")");
        log.AppendLine("  PASS         : " + pass);
        log.AppendLine("  FAIL         : " + fail);
        log.AppendLine("  최대 잔여 gap: " + maxResid.ToString("F3") + " m");
        log.AppendLine("  개천/다리 잠금 셀 : " + maskedCells);

        // ===== 5) 렌더 =====
        Directory.CreateDirectory(RENDER_DIR);
        var vb = VillageBounds(valid);
        string p1 = RENDER_DIR + "GF_01_birdseye45.png";
        string p2 = RENDER_DIR + "GF_02_eyelevel.png";
        string p3 = RENDER_DIR + "GF_03_from_stream.png";
        CaptureBirdseye(terrain, vb, p1);
        CaptureEyeLevel(terrain, vb, p2);
        CaptureFromStream(terrain, vb, p3);
        log.AppendLine();
        log.AppendLine("RENDERS:");
        log.AppendLine("  " + p1);
        log.AppendLine("  " + p2);
        log.AppendLine("  " + p3);

        // 콘솔 + 파일 보고
        string report = log.ToString();
        File.WriteAllText(RENDER_DIR + "report.txt", report);
        Debug.Log("[GroundFit]\n" + report);
    }

    // ---- 정점 단위 baseY + 접지 footprint ----
    static void ScanRoot(Root r)
    {
        var rends = r.tr.GetComponentsInChildren<MeshRenderer>(true);
        var ys = new List<float>(1 << 16);
        var pts = new List<Vector3>(1 << 16);
        foreach (var mr in rends)
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var verts = mf.sharedMesh.vertices;
            var m = mr.transform.localToWorldMatrix;
            for (int i = 0; i < verts.Length; i++)
            {
                var w = m.MultiplyPoint3x4(verts[i]);
                ys.Add(w.y); pts.Add(w);
            }
        }
        r.vtxCount = ys.Count;
        if (ys.Count == 0) { r.skipped = true; r.skipReason = "메시 정점 0"; return; }

        ys.Sort();
        int idx = Mathf.Clamp(Mathf.FloorToInt(0.005f * ys.Count), 0, ys.Count - 1); // 하위 0.5 퍼센타일
        r.baseY = ys[idx];
        r.topY = ys[ys.Count - 1];

        float band = LOWBAND;
        var xz = BandXZ(pts, r.baseY, band);
        if (xz.Count < 30) { band = LOWBAND_WIDE; xz = BandXZ(pts, r.baseY, band); }
        r.bandUsed = band; r.lowBandCount = xz.Count;
        if (xz.Count == 0) { r.skipped = true; r.skipReason = "접지 정점 0 (1.2m 확장 후에도)"; return; }

        float mnx = 1e9f, mxx = -1e9f, mnz = 1e9f, mxz = -1e9f;
        foreach (var p in xz) { mnx = Mathf.Min(mnx, p.x); mxx = Mathf.Max(mxx, p.x); mnz = Mathf.Min(mnz, p.y); mxz = Mathf.Max(mxz, p.y); }
        r.minX = mnx; r.maxX = mxx; r.minZ = mnz; r.maxZ = mxz;
    }

    static List<Vector2> BandXZ(List<Vector3> pts, float baseY, float band)
    {
        var o = new List<Vector2>();
        float hi = baseY + band;
        for (int i = 0; i < pts.Count; i++)
        {
            float y = pts[i].y;
            if (y >= baseY && y <= hi) o.Add(new Vector2(pts[i].x, pts[i].z));
        }
        return o;
    }

    // ---- 검증: footprint 중심+4모서리 gap 최대절대값, out 중심 지형높이 ----
    static float GapMax(Terrain terrain, Root r, out float centerH)
    {
        float cxp = (r.minX + r.maxX) * 0.5f, czp = (r.minZ + r.maxZ) * 0.5f;
        Vector2[] p = {
            new Vector2(cxp, czp),
            new Vector2(r.minX, r.minZ), new Vector2(r.minX, r.maxZ),
            new Vector2(r.maxX, r.minZ), new Vector2(r.maxX, r.maxZ)
        };
        centerH = terrain.SampleHeight(new Vector3(cxp, 0, czp)) + terrain.transform.position.y;
        float mx = 0f;
        for (int i = 0; i < p.Length; i++)
        {
            float g = r.baseY - (terrain.SampleHeight(new Vector3(p[i].x, 0, p[i].y)) + terrain.transform.position.y);
            mx = Mathf.Max(mx, Mathf.Abs(g));
        }
        return mx;
    }

    // ---- 개천/다리 마스크 ----
    static bool[,] BuildMask(int res, Vector3 size, Vector3 tpos, float cx, float cz)
    {
        var mask = new bool[res, res];
        foreach (var nm in MASK_OBJS)
        {
            var g = GameObject.Find(nm);
            if (g == null) continue;
            var rends = g.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in rends)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var verts = mf.sharedMesh.vertices;
                var tris = mf.sharedMesh.triangles;
                var m = mr.transform.localToWorldMatrix;
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(verts[tris[t]]);
                    Vector3 b = m.MultiplyPoint3x4(verts[tris[t + 1]]);
                    Vector3 c = m.MultiplyPoint3x4(verts[tris[t + 2]]);
                    RasterTri(mask, res, size, tpos, cx, cz,
                        a.x, a.z, b.x, b.z, c.x, c.z);
                }
            }
        }
        int k = Mathf.CeilToInt(STREAM_KEEP / Mathf.Min(cx, cz));
        Dilate(mask, res, k);
        return mask;
    }

    static void RasterTri(bool[,] mask, int res, Vector3 size, Vector3 tpos, float cx, float cz,
        float ax, float az, float bx, float bz, float cx2, float cz2)
    {
        float minx = Mathf.Min(ax, Mathf.Min(bx, cx2)), maxx = Mathf.Max(ax, Mathf.Max(bx, cx2));
        float minz = Mathf.Min(az, Mathf.Min(bz, cz2)), maxz = Mathf.Max(az, Mathf.Max(bz, cz2));
        int c0 = Mathf.Clamp(Mathf.FloorToInt((minx - tpos.x) / cx), 0, res - 1);
        int c1 = Mathf.Clamp(Mathf.CeilToInt((maxx - tpos.x) / cx), 0, res - 1);
        int r0 = Mathf.Clamp(Mathf.FloorToInt((minz - tpos.z) / cz), 0, res - 1);
        int r1 = Mathf.Clamp(Mathf.CeilToInt((maxz - tpos.z) / cz), 0, res - 1);
        for (int row = r0; row <= r1; row++)
        {
            float wz = row * cz + tpos.z;
            for (int col = c0; col <= c1; col++)
            {
                float wx = col * cx + tpos.x;
                if (PointInTri(wx, wz, ax, az, bx, bz, cx2, cz2)) mask[row, col] = true;
            }
        }
    }

    static bool PointInTri(float px, float pz, float ax, float az, float bx, float bz, float cx, float cz)
    {
        float d1 = Sign(px, pz, ax, az, bx, bz);
        float d2 = Sign(px, pz, bx, bz, cx, cz);
        float d3 = Sign(px, pz, cx, cz, ax, az);
        bool neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool pos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(neg && pos);
    }
    static float Sign(float px, float pz, float ax, float az, float bx, float bz)
        => (px - bx) * (az - bz) - (ax - bx) * (pz - bz);

    static void Dilate(bool[,] mask, int res, int k)
    {
        for (int step = 0; step < k; step++)
        {
            var nm = (bool[,])mask.Clone();
            for (int row = 0; row < res; row++)
                for (int col = 0; col < res; col++)
                {
                    if (mask[row, col]) continue;
                    bool any = false;
                    for (int dz = -1; dz <= 1 && !any; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int rr = row + dz, cc = col + dx;
                            if (rr < 0 || cc < 0 || rr >= res || cc >= res) continue;
                            if (mask[rr, cc]) { any = true; break; }
                        }
                    if (any) nm[row, col] = true;
                }
            Array.Copy(nm, mask, mask.Length);
        }
    }

    // ---- 수학 ----
    static float RectDist(float px, float pz, float minX, float maxX, float minZ, float maxZ)
    {
        float dx = Mathf.Max(0f, Mathf.Max(minX - px, px - maxX));
        float dz = Mathf.Max(0f, Mathf.Max(minZ - pz, pz - maxZ));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
    static float Smoothstep(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }
    static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(s.Length - n);

    // ---- 렌더 ----
    static Bounds VillageBounds(List<Root> valid)
    {
        float mnx = 1e9f, mxx = -1e9f, mnz = 1e9f, mxz = -1e9f, mny = 1e9f, mxy = -1e9f;
        bool any = false;
        foreach (var r in valid)
        {
            if (!r.isSedae) continue;
            any = true;
            mnx = Mathf.Min(mnx, r.minX); mxx = Mathf.Max(mxx, r.maxX);
            mnz = Mathf.Min(mnz, r.minZ); mxz = Mathf.Max(mxz, r.maxZ);
            mny = Mathf.Min(mny, r.baseY); mxy = Mathf.Max(mxy, r.baseY + 6f);
        }
        if (!any) { foreach (var r in valid) { mnx = Mathf.Min(mnx, r.minX); mxx = Mathf.Max(mxx, r.maxX); mnz = Mathf.Min(mnz, r.minZ); mxz = Mathf.Max(mxz, r.maxZ); mny = Mathf.Min(mny, r.baseY); mxy = Mathf.Max(mxy, r.baseY + 6f); } }
        var b = new Bounds();
        b.SetMinMax(new Vector3(mnx, mny, mnz), new Vector3(mxx, mxy, mxz));
        return b;
    }

    static void CaptureBirdseye(Terrain terrain, Bounds vb, string path)
    {
        Vector3 c = vb.center;
        float span = Mathf.Max(vb.size.x, vb.size.z);
        float d = span * 1.15f + 30f;
        Vector3 pos = new Vector3(c.x, c.y + d * 0.85f, c.z - d * 0.85f);
        RenderShot(pos, (c - pos).normalized, 55f, path);
    }

    static void CaptureEyeLevel(Terrain terrain, Bounds vb, string path)
    {
        Vector3 c = vb.center;
        float ex = vb.size.x * 0.5f;
        Vector3 eye = new Vector3(c.x - ex - 8f, 0, c.z - vb.size.z * 0.15f);
        eye.y = terrain.SampleHeight(new Vector3(eye.x, 0, eye.z)) + terrain.transform.position.y + 1.6f;
        Vector3 look = new Vector3(c.x, terrain.SampleHeight(new Vector3(c.x, 0, c.z)) + terrain.transform.position.y + 1.0f, c.z);
        RenderShot(eye, (look - eye).normalized, 60f, path);
    }

    static void CaptureFromStream(Terrain terrain, Bounds vb, string path)
    {
        Vector3 c = vb.center;
        // 북쪽 개천 쪽에서 마을을 향해
        float zStream = vb.max.z + 22f;
        Vector3 pos = new Vector3(c.x, 0, zStream);
        pos.y = terrain.SampleHeight(new Vector3(pos.x, 0, pos.z)) + terrain.transform.position.y + 3.0f;
        Vector3 look = new Vector3(c.x, terrain.SampleHeight(new Vector3(c.x, 0, c.z)) + terrain.transform.position.y + 1.5f, c.z);
        RenderShot(pos, (look - pos).normalized, 60f, path);
    }

    static void RenderShot(Vector3 pos, Vector3 dir, float fov, string path)
    {
        var go = new GameObject("__gf_cam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 3000f;
        cam.clearFlags = CameraClearFlags.Skybox;

        int W = 1600, Hh = 900;
        var rt = new RenderTexture(W, Hh, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(W, Hh, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, Hh), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        File.WriteAllBytes(path, tex.EncodeToPNG());

        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(go);
    }
}
