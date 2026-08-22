// SeocheonMtnTrees.cs
// 산 사면 전체에 Terrain Tree 식재. Quercus 상수리 + Salix 버드나무 2종만.
// 핵심: 마커 반경이 아니라 "지형 고도 기반 밀도장"으로 심는다.
//  - 밀도 = 고도페이드(평지→0, 산몸통→1) × 클럼프노이즈(성긴/빽빽) × 글레이드마스크(빈터).
//  - 가장자리는 고도가 평지로 내려가며 자연 페이드 → 원형 경계선 없음.
//  - 개천+8m·마을(개천 북, 저지대)·노면/터 9m 금지.
//  - 스케일 0.6~1.4, 회전 랜덤. 총량 TARGET로 조절.
// 메뉴: Tools/Seocheon/Mtn/PlantTrees , RemoveTrees
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

public static class SeocheonMtnTrees
{
    const int    SEED        = 20260815;
    const int    TARGET      = 300;    // 총 그루수 목표(250~350)
    const float  KEEPOUT     = 9f;     // 노면·터 반경 금지
    const float  STREAM_KEEP = 8f;     // 개천+8m 금지
    const float  SALIX_FRAC  = 0.22f;  // 버드나무 비율(나머지 상수리)
    const string QUERCUS = "Assets/SeyeonjeongPavilion/Prefabs/SM_Quercusacutissima_Summer_2.prefab";
    const string SALIX   = "Assets/SeyeonjeongPavilion/Prefabs/SM_Salixpierotii_Summer_2.prefab";

    static Terrain terr; static TerrainData td; static Vector3 size, tpos;
    static List<Vector2> pathXZ; static Vector2 mudXZ, tombXZ;
    static List<Vector2> streamXZ;
    static float plainH, fadeLo, fadeHi;

    class Cand { public Vector2 p; public float h; public float w; }

    [MenuItem("Tools/Seocheon/Mtn/PlantTrees")]
    public static void Plant()
    {
        Random.InitState(SEED);
        terr = Terrain.activeTerrain; td = terr.terrainData; size = td.size; tpos = terr.transform.position;
        var mk = GameObject.Find("_MtnMarkers"); if (mk == null) { Debug.LogError("[Trees] _MtnMarkers 없음"); return; }
        pathXZ = ReadOrdered(mk.transform, "_MtnPath_");
        var ridge = ReadOrdered(mk.transform, "_MtnRidge_");
        var tm = mk.transform.Find("_Terrace_Mudang"); var tt = mk.transform.Find("_Terrace_Tomb");
        mudXZ = new Vector2(tm.position.x, tm.position.z); tombXZ = new Vector2(tt.position.x, tt.position.z);

        var q = AssetDatabase.LoadAssetAtPath<GameObject>(QUERCUS);
        var s = AssetDatabase.LoadAssetAtPath<GameObject>(SALIX);
        if (q == null || s == null) { Debug.LogError("[Trees] 프리팹 로드 실패"); return; }
        td.treePrototypes = new[] { new TreePrototype { prefab = q }, new TreePrototype { prefab = s } };
        td.RefreshPrototypes();

        streamXZ = CollectStreamXZ();

        // 산 몸통 + 사면 페이드까지 넉넉한 박스(능선 좌우로 크게)
        float minx = 1e9f, maxx = -1e9f, minz = 1e9f, maxz = -1e9f;
        var span = new List<Vector2>(pathXZ); span.AddRange(ridge); span.Add(mudXZ); span.Add(tombXZ);
        foreach (var p in span) { minx = Mathf.Min(minx, p.x); maxx = Mathf.Max(maxx, p.x); minz = Mathf.Min(minz, p.y); maxz = Mathf.Max(maxz, p.y); }
        minx -= 55; maxx += 55; minz -= 55; maxz += 26;

        plainH = CalibratePlain(minx, maxx, minz, maxz);
        fadeLo = plainH + 0.8f; fadeHi = plainH + 6.5f;   // 이 고도구간에서 밀도 0→1 (조밀한 곳은 fadeHi 위)

        // 후보 격자 + 밀도가중
        var cands = new List<Cand>();
        for (float wx = minx; wx <= maxx; wx += 3.2f)
            for (float wz = minz; wz <= maxz; wz += 3.2f)
            {
                var p = new Vector2(wx + Random.Range(-1.5f, 1.5f), wz + Random.Range(-1.5f, 1.5f));
                float h = H(p);
                if (h <= fadeLo) continue;                 // 평지·개천·마을(저지대) 제외
                if (KeepOut(p)) continue;                  // 노면·터 9m
                if (NearStream(p)) continue;               // 개천+8m
                float w = Density(p, h);
                if (w <= 0.02f) continue;                  // 글레이드/희박
                cands.Add(new Cand { p = p, h = h, w = w });
            }

        // 밀도가중 무작위 선택 → TARGET, 최소간격 유지
        for (int i = cands.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var t = cands[i]; cands[i] = cands[j]; cands[j] = t; }
        var placed = new List<Vector2>();
        var sel = new List<Cand>();
        float boost = 1f;
        for (int pass = 0; pass < 5 && sel.Count < TARGET; pass++)
        {
            for (int i = 0; i < cands.Count && sel.Count < TARGET; i++)
            {
                var c = cands[i]; if (c == null) continue;
                if (Random.value < c.w * boost && Space(placed, c.p, 3.0f)) { sel.Add(c); placed.Add(c.p); cands[i] = null; }
            }
            boost *= 1.7f;
        }

        var inst = new List<TreeInstance>(); int nQ = 0, nS = 0;
        foreach (var c in sel)
        {
            int proto = Random.value < SALIX_FRAC ? 1 : 0; if (proto == 0) nQ++; else nS++;
            inst.Add(MakeInstance(c.p, c.h, proto, Random.Range(0.6f, 1.4f)));
        }
        td.SetTreeInstances(inst.ToArray(), true);

        terr.treeDistance = 60f; terr.treeBillboardDistance = 50f; SetTreeLodBias(terr, 0.4f);
        terr.Flush(); EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        // 보고: 덮은 영역, 근거리 동시
        float bx0 = 1e9f, bx1 = -1e9f, bz0 = 1e9f, bz1 = -1e9f;
        foreach (var c in sel) { bx0 = Mathf.Min(bx0, c.p.x); bx1 = Mathf.Max(bx1, c.p.x); bz0 = Mathf.Min(bz0, c.p.y); bz1 = Mathf.Max(bz1, c.p.y); }
        long qT = Lod0Tris(q), sT = Lod0Tris(s);
        Vector2 bridge = new Vector2(462f, 561f);
        Vector2 midpath = pathXZ.Count > 3 ? pathXZ[3] : mudXZ;
        int nearB = 0, nearM = 0;
        foreach (var c in sel) { if (Vector2.Distance(c.p, bridge) <= 60f) nearB++; if (Vector2.Distance(c.p, midpath) <= 40f) nearM++; }
        Debug.Log(string.Format("[Trees] 식재: 상수리 {0} + 버드 {1} = {2}그루.  덮은영역 {3:F0}×{4:F0}m (bbox).  plainH={5:F1}, fade {6:F1}~{7:F1}",
            nQ, nS, nQ + nS, bx1 - bx0, bz1 - bz0, plainH, fadeLo, fadeHi));
        Debug.Log(string.Format("[Trees] 근거리 동시: 다리60m내 {0}그루 / 산길중턱40m내 {1}그루.  treeLODBias0.4라 대부분 LOD2(상수리 74k)~LOD3(37k) → 다리뷰 대략 {0}×~55k ≈ {2:N0} tris.  LOD0 상수리={3:N0} 버드={4:N0}",
            nearB, nearM, (long)(nearB * 55000L), qT, sT));
    }

    [MenuItem("Tools/Seocheon/Mtn/RemoveTrees")]
    public static void Remove()
    {
        var t = Terrain.activeTerrain; var d = t.terrainData;
        int n = d.treeInstanceCount;
        d.SetTreeInstances(new TreeInstance[0], true); t.Flush();
        EditorUtility.SetDirty(d); AssetDatabase.SaveAssets();
        Debug.Log("[Trees] Terrain 나무 " + n + "그루 제거.");
    }

    // ---------- density field ----------
    static float Density(Vector2 p, float h)
    {
        float hf = Smooth01((h - fadeLo) / Mathf.Max(0.1f, fadeHi - fadeLo));      // 고도 페이드 0..1
        float clump = Mathf.PerlinNoise(p.x * 0.035f + 11.3f, p.y * 0.035f + 7.7f); // 성긴/빽빽 (~28m)
        float cf = Mathf.Lerp(0.18f, 1.15f, clump);
        float glade = Mathf.PerlinNoise(p.x * 0.02f + 131.7f, p.y * 0.02f + 59.1f); // 큰 빈터 (~50m)
        float gladeMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.24f, 0.33f, glade)); // <0.24 완전 빈터
        return hf * cf * gladeMask;
    }

    static float CalibratePlain(float minx, float maxx, float minz, float maxz)
    {
        var hs = new List<float>();
        for (float wx = minx; wx <= maxx; wx += 6f)
            for (float wz = minz; wz <= maxz; wz += 6f)
                hs.Add(H(new Vector2(wx, wz)));
        hs.Sort();
        int idx = Mathf.Clamp((int)(hs.Count * 0.10f), 0, hs.Count - 1); // 10퍼센타일 = 주변 평지
        return hs[idx];
    }

    static List<Vector2> CollectStreamXZ()
    {
        var o = new List<Vector2>();
        var go = GameObject.Find("_Stream_Water");
        if (go == null)
            foreach (var t in GameObject.FindObjectsOfType<Transform>())
                if (t.name.Contains("Stream_Water")) { go = t.gameObject; break; }
        if (go == null) { Debug.LogWarning("[Trees] _Stream_Water 못찾음 → 개천 가드는 고도(fadeLo)로 대체"); return o; }
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var vs = mf.sharedMesh.vertices; var m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < vs.Length; i += 7) { var w = m.MultiplyPoint3x4(vs[i]); o.Add(new Vector2(w.x, w.z)); }
        }
        return o;
    }

    static bool NearStream(Vector2 p)
    {
        float k2 = STREAM_KEEP * STREAM_KEEP;
        for (int i = 0; i < streamXZ.Count; i++) if ((streamXZ[i] - p).sqrMagnitude < k2) return true;
        return false;
    }

    // ---------- helpers ----------
    static TreeInstance MakeInstance(Vector2 p, float h, int proto, float scale)
    {
        var ti = new TreeInstance();
        ti.position = new Vector3((p.x - tpos.x) / size.x, (h - tpos.y) / size.y, (p.y - tpos.z) / size.z);
        ti.prototypeIndex = proto;
        ti.widthScale = scale; ti.heightScale = scale;
        ti.rotation = Random.Range(0f, Mathf.PI * 2f);
        ti.color = Color.white; ti.lightmapColor = Color.white;
        return ti;
    }

    static float H(Vector2 p) { return terr.SampleHeight(new Vector3(p.x, 0, p.y)) + tpos.y; }
    static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    static bool KeepOut(Vector2 p)
    {
        if (Vector2.Distance(p, mudXZ) < KEEPOUT) return true;
        if (Vector2.Distance(p, tombXZ) < KEEPOUT) return true;
        return DistToPath(p) < KEEPOUT;
    }

    static bool Space(List<Vector2> placed, Vector2 p, float min)
    {
        float m2 = min * min;
        for (int i = 0; i < placed.Count; i++) if ((placed[i] - p).sqrMagnitude < m2) return false;
        return true;
    }

    static float DistToPath(Vector2 p)
    {
        float best = 1e9f;
        for (int i = 0; i + 1 < pathXZ.Count; i++)
        {
            Vector2 a = pathXZ[i], b = pathXZ[i + 1]; Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    static List<Vector2> ReadOrdered(Transform mk, string prefix)
    {
        var list = new List<Transform>();
        foreach (Transform c in mk) if (c.name.StartsWith(prefix)) list.Add(c);
        list.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
        var o = new List<Vector2>();
        foreach (var c in list) o.Add(new Vector2(c.position.x, c.position.z));
        return o;
    }

    static long Lod0Tris(GameObject prefab)
    {
        long tris = 0;
        var lg = prefab.GetComponentInChildren<LODGroup>();
        if (lg != null)
        {
            var lods = lg.GetLODs();
            if (lods.Length > 0)
                foreach (var r in lods[0].renderers)
                {
                    if (r == null) continue; var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                }
            return tris;
        }
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
        return tris;
    }

    static void SetTreeLodBias(Terrain t, float v)
    {
        var pi = typeof(Terrain).GetProperty("treeLODBiasMultiplier", BindingFlags.Instance | BindingFlags.Public);
        if (pi != null && pi.CanWrite) { pi.SetValue(t, v, null); Debug.Log("[Trees] treeLODBiasMultiplier=" + v); }
        else Debug.LogWarning("[Trees] treeLODBiasMultiplier 없음 → QualitySettings.lodBias(" + QualitySettings.lodBias + ") 사용. 품질설정에서 0.4 권장.");
    }
}
