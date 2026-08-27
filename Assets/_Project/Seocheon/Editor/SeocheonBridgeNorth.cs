using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

// 서천 1부 마을 - 개천 링 북쪽 판석교 절차적 생성기
// 위치는 씬 기하(개천 링 중심 -> 북쪽 아크)에서 산출. 좌표 하드코딩 없음.
public static class SeocheonBridgeNorth
{
    const int SEED = 20260814;
    const string GRANITE = "Assets/_Project/Seocheon/Art/Materials/Donheon/M_Stone_Granite.mat";
    const string RUBBLE  = "Assets/_Project/Seocheon/Art/Materials/Donheon/M_Stone_Rubble.mat";
    const string PREFAB  = "Assets/_Project/Seocheon/Prefabs/SM_Seocheon_BridgeN.prefab";
    const string RENDERDIR = @"C:\Users\User\_Renders\BridgeN\";

    static int _tri;
    static System.Random _rng;

    [MenuItem("Tools/Seocheon/BridgeN/Build")]
    public static void Build()
    {
        _rng = new System.Random(SEED);
        _tri = 0;

        // 재실행 대비: 이전 _BridgeN 씬 인스턴스 제거(다른 것은 건드리지 않음)
        var _scn = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var _go in _scn.GetRootGameObjects()) if (_go.name == "_BridgeN") Object.DestroyImmediate(_go);

        var granite = AssetDatabase.LoadAssetAtPath<Material>(GRANITE);
        var rubble  = AssetDatabase.LoadAssetAtPath<Material>(RUBBLE);
        if (granite == null || rubble == null) { Debug.LogError("[BridgeN] 재질 로드 실패"); return; }

        var terrain = Terrain.activeTerrain;
        if (terrain == null) { Debug.LogError("[BridgeN] 활성 Terrain 없음"); return; }
        Vector3 tp = terrain.transform.position;

        // ---- 측정: 개천 링 중심 + 북쪽 물길 ----
        var stream = GameObject.Find("_Stream_Water");
        if (stream == null) { Debug.LogError("[BridgeN] _Stream_Water 없음"); return; }
        var mfs = stream.GetComponentsInChildren<MeshFilter>(true);
        // 링 중심(XZ) = 수면 메시 전체 bounds 중심
        Bounds sb = new Bounds();
        bool sinit = false;
        foreach (var r in stream.GetComponentsInChildren<Renderer>(true)) { if (!sinit) { sb = r.bounds; sinit = true; } else sb.Encapsulate(r.bounds); }
        float cx = sb.center.x, cz0 = sb.center.z;

        // 북쪽 아크: X≈cx, Z>중심 인 수면 정점들
        float zMin = 9999, zMax = -9999; double ySum = 0; int yCnt = 0;
        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null) continue;
            var vs = mf.sharedMesh.vertices; var tr = mf.transform;
            for (int i = 0; i < vs.Length; i++)
            {
                var w = tr.TransformPoint(vs[i]);
                if (Mathf.Abs(w.x - cx) < 3f && w.z > cz0)
                { if (w.z < zMin) zMin = w.z; if (w.z > zMax) zMax = w.z; ySum += w.y; yCnt++; }
            }
        }
        if (yCnt == 0) { Debug.LogError("[BridgeN] 북쪽 물길 정점 없음"); return; }
        float waterY = (float)(ySum / yCnt);
        float waterWidth = zMax - zMin;
        float zc = (zMin + zMax) * 0.5f;

        float Bed(float x, float z) { return tp.y + terrain.SampleHeight(new Vector3(x, 0, z)); }
        float bankS = Bed(cx, zMin - 2f);   // 남쪽 둑(2m 바깥)
        float bankN = Bed(cx, zMax + 2f);   // 북쪽 둑
        float bedY  = Bed(cx, zc);          // 물 밑 바닥
        float bankAvg = (bankS + bankN) * 0.5f;

        // ---- 치수 ----
        float L = waterWidth + 3.0f;                 // 물 폭 + 양쪽 1.5m
        float zStart = zc - L * 0.5f, zEnd = zc + L * 0.5f;
        // 스펙 공식은 둑평균+0.05(둑이 수면 위 전제). 그러나 이 구역은 수면 메시가 낮은 지형 위에 얹혀
        // 수면이 바닥/둑보다 ~1.7m 높음. 상판을 수면 바로 위에 두면 교각(막돌 단)이 전부 수몰되어
        // 스펙 핵심인 "단 그림자"가 사라짐. 레퍼런스처럼 교각이 물 밖으로 ~1m 드러나도록 수면+1.2로 올림.
        float deckTopBase = Mathf.Max(bankAvg + 0.05f, waterY + 1.2f);
        const float archAmp = 0.12f;
        int numSpans = Mathf.Clamp(Mathf.RoundToInt(L / 2.8f), 2, 12);
        float spanLen = L / numSpans;
        const float slabThk = 0.26f, rowW = 0.72f, rowGap = 0.015f;

        float DeckTop(float z) { return deckTopBase + archAmp * Mathf.Sin(Mathf.PI * Mathf.Clamp01((z - zStart) / L)); }
        float R(float mag) { return (float)(_rng.NextDouble() * 2.0 - 1.0) * mag; }

        Debug.Log(string.Format("[BridgeN][MEASURE] ringC=({0:F1},{1:F1}) waterY={2:F2} width={3:F2} bankS={4:F2} bankN={5:F2} bed={6:F2} L={7:F2} spans={8} spanLen={9:F2}",
            cx, cz0, waterY, waterWidth, bankS, bankN, bedY, L, numSpans, spanLen));

        // ---- 루트(프리팹 로컬원점 = 밟는 면 중심, +Z 길이축) ----
        var root = new GameObject("_BridgeN");
        root.transform.position = new Vector3(cx, DeckTop(zc), zc);

        // ---- 교각(막돌 4~5단 + 관석) : 내부 경간 경계 ----
        for (int s = 1; s < numSpans; s++)
        {
            float pz = zStart + s * spanLen;
            float capBottom = DeckTop(pz) - slabThk - 0.18f;
            MakeBox(new Vector3(1.8f, 0.18f, 1.2f), rubble, root.transform, "Cap", new Vector3(cx, capBottom + 0.09f, pz), Quaternion.Euler(0, R(3f), 0));
            int nTier = 4 + _rng.Next(0, 2);
            float pierTop = capBottom, pierBot = bedY - 0.2f;
            float pierH = Mathf.Max(0.4f, pierTop - pierBot);
            float tierH = pierH / nTier;
            for (int t = 0; t < nTier; t++)
            {
                float sc = Mathf.Pow(0.95f, t);
                float cy = pierBot + (t + 0.5f) * tierH;
                var size = new Vector3(1.5f * sc * (1 + R(0.06f)), tierH * 1.04f, 0.9f * sc * (1 + R(0.06f)));
                var pos = new Vector3(cx + R(0.04f), cy, pz + R(0.04f));
                MakeBox(size, rubble, root.transform, "PierStone", pos, Quaternion.Euler(0, R(8f), 0));
            }
        }

        // ---- 교대(abutment) : 양 끝, 둑에 물림 ----
        float[] ends = { zStart, zEnd };
        foreach (var e in ends)
        {
            int nA = 3 + _rng.Next(0, 2);
            float topY = DeckTop(e) - slabThk;
            float baseBank = Bed(cx, e);
            float botY = baseBank - 0.8f;
            float aH = Mathf.Max(0.3f, (topY - botY) / nA);
            float inward = (e < zc) ? 0.3f : -0.3f;
            for (int t = 0; t < nA; t++)
            {
                float cy = botY + (t + 0.5f) * aH;
                var size = new Vector3(2.2f * (1 + R(0.05f)), aH * 1.05f, 1.3f);
                MakeBox(size, rubble, root.transform, "Abutment", new Vector3(cx + R(0.04f), cy, e + inward), Quaternion.Euler(0, R(6f), 0));
            }
        }

        // ---- 판석 상판 : 3줄, 이음새 지그재그 ----
        for (int rr = 0; rr < 3; rr++)
        {
            float rowX = cx + (rr - 1) * (rowW + rowGap);
            float off = rr * 0.25f;
            float sPos = zStart - off;
            while (sPos < zEnd - 0.05f)
            {
                float a = Mathf.Max(sPos, zStart);
                float b = Mathf.Min(sPos + spanLen, zEnd);
                sPos += spanLen;
                float segLen = b - a;
                if (segLen < 0.25f) continue;
                float zmid = (a + b) * 0.5f;
                float thk = slabThk * (1 + R(0.08f));
                float topY = DeckTop(zmid) + R(0.012f);
                MakeBox(new Vector3(rowW, thk, segLen - 0.015f), granite, root.transform, "Slab", new Vector3(rowX, topY - thk * 0.5f, zmid), Quaternion.Euler(0, R(1.2f), 0));
            }
        }

        // ---- 콜라이더 : 상판 1개 + 낙하방지벽 2개 ----
        var deckCol = root.AddComponent<BoxCollider>();
        deckCol.center = root.transform.InverseTransformPoint(new Vector3(cx, deckTopBase + archAmp * 0.5f - slabThk * 0.5f, zc));
        deckCol.size = new Vector3(2.3f, slabThk + 0.12f, L);
        for (int side = -1; side <= 1; side += 2)
        {
            var g = new GameObject("FallGuard");
            g.transform.SetParent(root.transform, false);
            g.transform.position = new Vector3(cx + side * 1.25f, deckTopBase + 0.6f, zc);
            var gc = g.AddComponent<BoxCollider>();
            gc.size = new Vector3(0.1f, 1.2f, L);
        }

        // ---- Static ----
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

        if (_tri > 1500) Debug.LogError(string.Format("[BridgeN] tris {0} > 1500 상한 초과", _tri));

        // ---- 프리팹 저장 + 씬 연결 ----
        var dir = Path.GetDirectoryName(PREFAB);
        if (!AssetDatabase.IsValidFolder(dir)) Directory.CreateDirectory(dir);
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PREFAB, InteractionMode.AutomatedAction);
        root.name = "_BridgeN";

        // ---- 렌더 3장 ----
        Directory.CreateDirectory(RENDERDIR);
        float half = L * 0.5f;
        RenderView(new Vector3(cx + half + 6f, deckTopBase + 2.5f, zc), new Vector3(cx, deckTopBase - 0.4f, zc), 42f, RENDERDIR + "01_side.png");        // 측면 정면
        RenderView(new Vector3(cx + half * 0.7f + 5f, waterY + 0.15f, zc - 3f), new Vector3(cx, waterY + 0.35f, zc), 46f, RENDERDIR + "02_waterline.png"); // 수면 접선
        RenderView(new Vector3(cx, DeckTop(zStart) + 1.6f, zStart - 1.6f), new Vector3(cx, deckTopBase + 1.1f, zEnd), 55f, RENDERDIR + "03_eye.png");     // 눈높이 진입

        float gapS = DeckTop(zStart) - slabThk - bankS;
        float gapN = DeckTop(zEnd) - slabThk - bankN;
        Debug.Log(string.Format("[BridgeN][REPORT] tris={0} spans={1} pierStonesPerCol=4~5 length={2:F2} deckTopY={3:F2}~{4:F2} gapToBankS={5:F2} gapToBankN={6:F2} prefab={7}",
            _tri, numSpans, L, deckTopBase, deckTopBase + archAmp, gapS, gapN, PREFAB));
    }

    static GameObject MakeBox(Vector3 size, Material mat, Transform parent, string name, Vector3 worldPos, Quaternion rot)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = BuildBoxMesh(size.x, size.y, size.z);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.transform.position = worldPos;
        go.transform.rotation = rot;
        go.transform.SetParent(parent, true);
        _tri += 12;
        return go;
    }

    // 월드 크기 UV(1m=1타일) 박스 메시
    static Mesh BuildBoxMesh(float sx, float sy, float sz)
    {
        float hx = sx * 0.5f, hy = sy * 0.5f, hz = sz * 0.5f;
        var verts = new List<Vector3>(); var norm = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
        void Face(Vector3 o, Vector3 u, Vector3 v)
        {
            int bi = verts.Count;
            Vector3 n = Vector3.Cross(u, v).normalized;
            float uw = u.magnitude, vh = v.magnitude;
            verts.Add(o); verts.Add(o + u); verts.Add(o + u + v); verts.Add(o + v);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(uw, 0)); uv.Add(new Vector2(uw, vh)); uv.Add(new Vector2(0, vh));
            for (int k = 0; k < 4; k++) norm.Add(n);
            tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2); tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 3);
        }
        Face(new Vector3(hx, -hy, -hz), new Vector3(0, sy, 0), new Vector3(0, 0, sz));   // +X
        Face(new Vector3(-hx, -hy, hz), new Vector3(0, sy, 0), new Vector3(0, 0, -sz));  // -X
        Face(new Vector3(-hx, hy, -hz), new Vector3(0, 0, sz), new Vector3(sx, 0, 0));   // +Y
        Face(new Vector3(-hx, -hy, hz), new Vector3(0, 0, -sz), new Vector3(sx, 0, 0));  // -Y
        Face(new Vector3(-hx, -hy, hz), new Vector3(sx, 0, 0), new Vector3(0, sy, 0));   // +Z
        Face(new Vector3(hx, -hy, -hz), new Vector3(-sx, 0, 0), new Vector3(0, sy, 0));  // -Z
        var m = new Mesh();
        m.SetVertices(verts); m.SetNormals(norm); m.SetUVs(0, uv); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    static void RenderView(Vector3 pos, Vector3 look, float fov, string path)
    {
        var camGO = new GameObject("~BridgeCam");
        var cam = camGO.AddComponent<Camera>();
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 3000f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 2 };
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(camGO);
        Debug.Log("[BridgeN][RENDER] " + path);
    }
}
