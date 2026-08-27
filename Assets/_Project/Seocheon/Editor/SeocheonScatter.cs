// SeocheonScatter.cs
// 마을 조경 대량 식재. 기존 _Village_Landscape 의 각 종(버드나무/대나무/갈대/곰솔/배롱/느릅/고사리/잔디)
// 인스턴스를 소스로 복제해서 구역별로 흩뿌린다. 건물(_Village_Sedae)·관아·개천 링은 피한다.
// 지면(SampleHeight)에 맞춰 심고, 소스의 접지 오프셋을 보존한다. Undo 가능.
// 메뉴: Tools/Seocheon/Landscape/Scatter   (다시 실행하면 이전 _Scatter_Auto 를 지우고 재생성)
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class SeocheonScatter
{
    const int   SEED     = 20260814;
    const float DISC_R   = 60f;      // 마을 디스크 반경
    const float EXCL_PAD = 3.0f;     // 건물 바깥 이만큼은 식재 금지
    const float STREAM_MINY = 8.3f;  // 이보다 낮은(둑/물가) 지형엔 나무 금지
    static readonly Vector2 CENTER = new Vector2(466f, 670f);

    class Src { public string prefix; public GameObject go; public float groundOffset; }

    static List<float> eX0, eX1, eZ0, eZ1;      // 건물 제외 사각형
    static List<Vector2> sedaeCenters;          // 세대 중심(대숲용)
    static List<Vector2> sedaeExtent;           // 세대 반크기
    static Terrain terr;
    static float tY;

    [MenuItem("Tools/Seocheon/Landscape/Scatter")]
    public static void Scatter()
    {
        Random.InitState(SEED);
        terr = Terrain.activeTerrain;
        tY = terr.transform.position.y;
        var vl = GameObject.Find("_Village_Landscape");
        if (vl == null) { Debug.LogError("[Scatter] _Village_Landscape 없음"); return; }

        // 이전 자동 식재 제거
        var old = vl.transform.Find("_Scatter_Auto");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var groupGO = new GameObject("_Scatter_Auto");
        Undo.RegisterCreatedObjectUndo(groupGO, "Scatter");
        groupGO.transform.SetParent(vl.transform, true);
        var group = groupGO.transform;

        // 소스(종별 첫 인스턴스) 수집
        var want = new[] { "SM_Salixpierotii", "SM_Henonis", "SM_PhragmitesAustralis",
            "SM_PinusThunbergii", "SM_Lagerstroemiaindica", "SM_UlmusDavidiana", "SM_Deparia", "SM_Grass" };
        var src = new Dictionary<string, Src>();
        foreach (Transform c in vl.transform)
        {
            foreach (var w in want)
                if (c.name.StartsWith(w) && !src.ContainsKey(w))
                {
                    float g = terr.SampleHeight(new Vector3(c.position.x, 0, c.position.z)) + tY;
                    src[w] = new Src { prefix = w, go = c.gameObject, groundOffset = c.position.y - g };
                }
        }
        foreach (var w in want) if (!src.ContainsKey(w)) Debug.LogWarning("[Scatter] 소스 없음: " + w);

        // 건물/관아 제외 사각형 + 세대 중심
        eX0 = new List<float>(); eX1 = new List<float>(); eZ0 = new List<float>(); eZ1 = new List<float>();
        sedaeCenters = new List<Vector2>(); sedaeExtent = new List<Vector2>();
        var sed = GameObject.Find("_Village_Sedae");
        foreach (Transform s in sed.transform)
        {
            var b = ChildBounds(s);
            AddExcl(b, EXCL_PAD);
            sedaeCenters.Add(new Vector2(b.center.x, b.center.z));
            sedaeExtent.Add(new Vector2(b.extents.x, b.extents.z));
        }
        var gw = GameObject.Find("GwanaGateSet");
        if (gw != null) AddExcl(ChildBounds(gw.transform), EXCL_PAD);

        var placed = new List<Vector2>();
        int nW = 0, nR = 0, nB = 0, nE = 0, nA = 0, nG = 0, nF = 0;

        // 1) 개천가 버드나무 벨트 (링 안쪽, 저지대 피해 조금 안쪽)
        for (int i = 0; i < 28; i++)
        {
            float ang = (i / 28f) * Mathf.PI * 2f + Random.Range(-0.10f, 0.10f);
            float r = Random.Range(46f, 55f);
            var p = CENTER + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            if (Place(src, "SM_Salixpierotii", p, 3.2f, 0.9f, 1.25f, placed, group)) nW++;
        }
        // 2) 갈대 — 물가(반경 60~64) + 북문 개천 입구
        for (int i = 0; i < 30; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(59f, 64f);
            var p = CENTER + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            if (Place(src, "SM_PhragmitesAustralis", p, 1.0f, 0.8f, 1.3f, placed, group, true)) nR++;
        }
        // 3) 집 뒤 대숲 — 세대마다 바깥쪽에 군락 (디스크 안으로 클램프)
        foreach (var scz in ScatterGroveCenters())
            for (int k = 0; k < 12; k++)
            {
                var p = scz + Random.insideUnitCircle * 5.0f;
                if (Place(src, "SM_Henonis", p, 1.6f, 0.85f, 1.2f, placed, group)) nB++;
            }
        // 4) 어귀(북문) 큰나무 — 느릅+곰솔 (저지대 허용)
        var gate = new Vector2(466f, 713f);
        var big = new[] { gate + new Vector2(-7, 3), gate + new Vector2(8, 1), gate + new Vector2(-1, 8) };
        foreach (var p in big) if (Place(src, "SM_UlmusDavidiana", p, 5f, 1.1f, 1.35f, placed, group, true)) nE++;
        if (Place(src, "SM_PinusThunbergii", gate + new Vector2(12, 6), 5f, 1.1f, 1.3f, placed, group, true)) nE++;
        // 5) 마당 포인트 — 배롱 + 곰솔 흩뿌리기
        for (int i = 0; i < 12; i++)
        {
            var p = CENTER + Random.insideUnitCircle * 50f;
            if (Place(src, "SM_Lagerstroemiaindica", p, 4f, 0.9f, 1.2f, placed, group)) nA++;
        }
        for (int i = 0; i < 8; i++)
        {
            var p = CENTER + Random.insideUnitCircle * 52f;
            if (Place(src, "SM_PinusThunbergii", p, 4.5f, 0.95f, 1.25f, placed, group)) nA++;
        }
        // 6) 지피 — 잔디/고사리 (담장밑·공터)
        for (int i = 0; i < 55; i++)
        {
            var p = CENTER + Random.insideUnitCircle * 56f;
            if (Place(src, "SM_Grass", p, 0.9f, 0.8f, 1.4f, placed, group)) nG++;
        }
        for (int i = 0; i < 35; i++)
        {
            var p = CENTER + Random.insideUnitCircle * 54f;
            if (Place(src, "SM_Deparia", p, 1.0f, 0.8f, 1.3f, placed, group)) nF++;
        }

        // tris 합계
        long tris = 0;
        foreach (var mf in vl.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;

        Debug.Log(string.Format("[Scatter] 버드{0} 갈대{1} 대나무{2} 어귀{3} 마당{4} 잔디{5} 고사리{6}  = 신규 {7}개.  _Village_Landscape 총 {8}개, tris합계 {9:N0}",
            nW, nR, nB, nE, nA, nG, nF, (nW + nR + nB + nE + nA + nG + nF), vl.transform.childCount + CountChildren(group) - 1, tris));

        SceneView.RepaintAll();
    }

    static int CountChildren(Transform t) { return t.childCount; }

    static List<Vector2> ScatterGroveCenters()
    {
        var o = new List<Vector2>();
        for (int i = 0; i < sedaeCenters.Count; i++)
        {
            var c = sedaeCenters[i];
            var dir = (c - CENTER);
            if (dir.sqrMagnitude < 0.01f) dir = new Vector2(0, 1);
            dir.Normalize();
            float off = Mathf.Max(sedaeExtent[i].x, sedaeExtent[i].y) + 5f;
            var gc = c + dir * off;
            if (Vector2.Distance(gc, CENTER) > DISC_R - 8f)
                gc = CENTER + (gc - CENTER).normalized * (DISC_R - 8f);
            o.Add(gc);
        }
        return o;
    }

    static bool Place(Dictionary<string, Src> src, string key, Vector2 p, float minSpacing,
        float sMin, float sMax, List<Vector2> placed, Transform parent, bool allowLowGround = false)
    {
        if (!src.ContainsKey(key)) return false;
        // 디스크 안?
        if (Vector2.Distance(p, CENTER) > DISC_R + (allowLowGround ? 5f : 0f)) return false;
        // 지형 높이
        float g = terr.SampleHeight(new Vector3(p.x, 0, p.y)) + tY;
        if (!allowLowGround && g < STREAM_MINY) return false;
        // 건물 제외
        for (int i = 0; i < eX0.Count; i++)
            if (p.x >= eX0[i] && p.x <= eX1[i] && p.y >= eZ0[i] && p.y <= eZ1[i]) return false;
        // 최소 간격
        for (int i = 0; i < placed.Count; i++)
            if ((placed[i] - p).sqrMagnitude < minSpacing * minSpacing) return false;

        var s = src[key];
        GameObject go;
        var asset = PrefabUtility.GetCorrespondingObjectFromSource(s.go);
        if (asset != null) go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        else { go = Object.Instantiate(s.go, parent); }
        go.transform.position = new Vector3(p.x, g + s.groundOffset, p.y);
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        float sc = Random.Range(sMin, sMax);
        go.transform.localScale = s.go.transform.localScale * sc;
        go.name = s.prefix + "_scatter";
        Undo.RegisterCreatedObjectUndo(go, "Scatter");
        placed.Add(p);
        return true;
    }

    static void AddExcl(Bounds b, float pad)
    {
        eX0.Add(b.min.x - pad); eX1.Add(b.max.x + pad);
        eZ0.Add(b.min.z - pad); eZ1.Add(b.max.z + pad);
    }

    static Bounds ChildBounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<MeshRenderer>(true);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
