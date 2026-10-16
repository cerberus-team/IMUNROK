using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>동헌 심문실의 장지문 두 짝을 집에서 떼어 낸다.</b>
    ///
    /// <b>왜 떼어 내야 했나.</b> 화성행궁 <c>SM_Yuyeotaek</c> 은 집 한 채가 <b>그물 한 덩이</b>다
    /// (꼭짓점 145만, 삼각형 61만). 심문실은 사방이 막혀 있고 들어오는 데는 남쪽 장지문뿐인데,
    /// 그 문이 집에 <b>닫힌 채로 구워져</b> 있었다. 그래서 불려 온 사람이 문을 뚫고 들어왔다 —
    /// <see cref="CourtSummon"/> 에 문을 물려 두었지만 그 문은 옛 관아세트의 것이라 꺼져 있었고,
    /// 새 집에는 여닫을 문이 아예 없었다.
    ///
    /// <b>어떻게 짚었나.</b> 창호지 재질(<c>MI_KoreanPaper_1</c>)의 삼각형만 골라 x 로 늘어놓으니
    /// 0.16m 짜리 빈 구간이 다섯 군데 나왔다 — 그것이 문설주고, 사이가 문짝이다. 문짝 나비는
    /// 0.8135m 로 고르고, 왼쪽 두 짝의 이음매(x=14.633)가 마침 불려 오는 이가 밟는 길목이다.
    /// 짐작이 아니라 <b>그물이 말해 준 자리</b>다.
    ///
    /// <b>값이 싸지는 않다.</b> 문을 뗀 나머지를 새 그물로 저장해야 하는데, 이 프로젝트는
    /// 텍스트 직렬화(ForceText)라 그것만 170MB 가까이 된다. 그래서 그 파일은 커밋에서 빼고,
    /// 대신 <b>언제든 다시 만들 수 있게</b> 이 도구를 남긴다 — 파일을 나르는 대신 만드는 법을 나른다.
    /// </summary>
    public static class DonheonDoorCarve
    {
        private const string MenuPath = "Tools/異聞錄/동헌 장지문 떼어 내기";
        private const string MeshDir = "Assets/_Project/Onggojip/Art/문";
        private const string RestName = "SM_Yuyeotaek_문뗀것";

        /// <summary>문짝 두 짝이 선 자리 — 왼쪽 끝 · 가운데 이음매 · 오른쪽 끝.</summary>
        private const float XL = 13.817f, XM = 14.633f, XR = 15.448f;

        /// <summary>문짝이 선 켜의 한가운데와 두께.</summary>
        private const float ZMid = 3.71f, ZThick = 0.40f;

        /// <summary>문짝이 걸친 높이(가운데와 폭). 기단 위부터 창방 아래까지.</summary>
        private const float YMid = 3.30f, YSpan = 2.30f;

        private static readonly string[] LeafNames = { "동헌_장지문_좌", "동헌_장지문_우" };

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var go = GameObject.Find("관아_동헌/SM_Yuyeotaek");
            if (go == null) { Debug.LogError("[동헌문] 「관아_동헌/SM_Yuyeotaek」을 못 찾았다."); return; }
            if (GameObject.Find(LeafNames[0]) != null) { Debug.LogWarning("[동헌문] 이미 떼어 두었다."); return; }

            var mf = go.GetComponent<MeshFilter>();
            var src = mf.sharedMesh;
            if (src == null) { Debug.LogError("[동헌문] 그물이 없다."); return; }

            if (!AssetDatabase.IsValidFolder(MeshDir))
                AssetDatabase.CreateFolder("Assets/_Project/Onggojip/Art", "문");

            var rr = go.GetComponent<Renderer>();
            var l2w = go.transform.localToWorldMatrix;
            var v = src.vertices;
            var nrm = src.normals;
            var uv0 = src.uv;
            var uv1 = src.uv2;
            var tan = src.tangents;

            var box = new Bounds(new Vector3((XL + XR) * 0.5f, YMid, ZMid),
                                 new Vector3(XR - XL, YSpan, ZThick));

            var restSubs = new List<int[]>();
            var leaf = new Piece[2];
            for (int k = 0; k < 2; k++) leaf[k] = new Piece(v.Length);

            int cut = 0;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tri = src.GetTriangles(s);
                var rest = new List<int>();
                var kept = new List<int>[2];
                kept[0] = new List<int>();
                kept[1] = new List<int>();

                for (int i = 0; i < tri.Length; i += 3)
                {
                    Vector3 a = l2w.MultiplyPoint3x4(v[tri[i]]);
                    Vector3 b = l2w.MultiplyPoint3x4(v[tri[i + 1]]);
                    Vector3 c = l2w.MultiplyPoint3x4(v[tri[i + 2]]);
                    Vector3 ctr = (a + b + c) / 3f;
                    if (!box.Contains(ctr))
                    {
                        rest.Add(tri[i]); rest.Add(tri[i + 1]); rest.Add(tri[i + 2]);
                        continue;
                    }
                    cut++;
                    int k = ctr.x < XM ? 0 : 1;
                    for (int j = 0; j < 3; j++)
                        kept[k].Add(leaf[k].Take(tri[i + j], v, nrm, uv0, tan));
                }
                restSubs.Add(rest.ToArray());
                for (int k = 0; k < 2; k++)
                    if (kept[k].Count > 0) leaf[k].Push(kept[k].ToArray(), rr.sharedMaterials[s]);
            }

            // ── 문을 뗀 집 ──
            var rest2 = new Mesh();
            rest2.name = RestName;
            rest2.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            rest2.SetVertices(v);
            if (nrm.Length == v.Length) rest2.SetNormals(nrm);
            if (uv0.Length == v.Length) rest2.SetUVs(0, uv0);
            if (uv1.Length == v.Length) rest2.SetUVs(1, uv1);
            if (tan.Length == v.Length) rest2.SetTangents(tan);
            rest2.subMeshCount = restSubs.Count;
            for (int s = 0; s < restSubs.Count; s++) rest2.SetTriangles(restSubs[s], s);
            rest2.RecalculateBounds();
            AssetDatabase.CreateAsset(rest2, MeshDir + "/" + RestName + ".asset");
            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/" + RestName + ".asset");
            var mc = go.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = mf.sharedMesh;

            // ── 문짝 둘 ──
            float[] hinge = { XL, XR };
            float[] angle = { 85f, -85f };      // 둘 다 대청 쪽(−z)으로 열린다
            var doors = new SwingDoor[2];
            for (int k = 0; k < 2; k++)
            {
                var dm = leaf[k].Bake(LeafNames[k]);
                AssetDatabase.CreateAsset(dm, MeshDir + "/" + LeafNames[k] + ".asset");
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/" + LeafNames[k] + ".asset");

                var obj = new GameObject(LeafNames[k]);
                obj.transform.SetParent(go.transform.parent, false);
                obj.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
                obj.transform.localScale = go.transform.localScale;
                obj.AddComponent<MeshFilter>().sharedMesh = saved;
                obj.AddComponent<MeshRenderer>().sharedMaterials = leaf[k].mats.ToArray();
                var bc = obj.AddComponent<BoxCollider>();
                bc.center = saved.bounds.center;
                bc.size = saved.bounds.size;
                doors[k] = obj.AddComponent<SwingDoor>();
            }
            // 한 짝을 밀면 두 짝이 함께 열린다 — 사람이 드나드는 문이라 반만 열 까닭이 없다.
            doors[0].Setup(new[] { doors[0].transform }, new Vector3(XL, 3f, ZMid), angle[0], new[] { doors[1] });
            doors[1].Setup(new[] { doors[1].transform }, new Vector3(XR, 3f, ZMid), angle[1], new[] { doors[0] });

            int wired = 0;
            foreach (var cs in Object.FindObjectsByType<CourtSummon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(cs);
                var dp = so.FindProperty("_doors");
                dp.ClearArray();
                dp.arraySize = 2;
                dp.GetArrayElementAtIndex(0).objectReferenceValue = doors[0];
                dp.GetArrayElementAtIndex(1).objectReferenceValue = doors[1];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cs);
                wired++;
            }

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[동헌문] 삼각형 " + cut + "개를 떼어 두 짝으로 세웠다. 문을 물린 사람 " + wired + "명.");
        }

        /// <summary>떼어 낸 조각 한 벌 — 쓰는 꼭짓점만 옮겨 담는다.</summary>
        private class Piece
        {
            public readonly List<Material> mats = new List<Material>();
            private readonly int[] _map;
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<Vector3> _n = new List<Vector3>();
            private readonly List<Vector2> _u = new List<Vector2>();
            private readonly List<Vector4> _t = new List<Vector4>();
            private readonly List<int[]> _subs = new List<int[]>();

            public Piece(int n)
            {
                _map = new int[n];
                for (int i = 0; i < n; i++) _map[i] = -1;
            }

            public int Take(int o, Vector3[] v, Vector3[] n, Vector2[] u, Vector4[] t)
            {
                if (_map[o] >= 0) return _map[o];
                _map[o] = _v.Count;
                _v.Add(v[o]);
                if (n.Length == v.Length) _n.Add(n[o]);
                if (u.Length == v.Length) _u.Add(u[o]);
                if (t.Length == v.Length) _t.Add(t[o]);
                return _map[o];
            }

            public void Push(int[] tris, Material mat) { _subs.Add(tris); mats.Add(mat); }

            public Mesh Bake(string name)
            {
                var m = new Mesh();
                m.name = name;
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(_v);
                if (_n.Count == _v.Count) m.SetNormals(_n);
                if (_u.Count == _v.Count) m.SetUVs(0, _u);
                if (_t.Count == _v.Count) m.SetTangents(_t);
                m.subMeshCount = _subs.Count;
                for (int s = 0; s < _subs.Count; s++) m.SetTriangles(_subs[s], s);
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
