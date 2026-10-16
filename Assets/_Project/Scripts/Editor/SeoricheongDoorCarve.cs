using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>서리청(문서고) 앞면의 살문 열 짝을 집에서 떼어 낸다.</b>
    ///
    /// 손으로 지어 두었던 문서고를 화성행궁 <c>SM_Seoricheong</c> 으로 갈면서, 볕·거울
    /// 놀이를 다시 세워야 했다. 놀이의 밑돌은 <b>한 짝만 열린다</b>는 것이고
    /// (<see cref="SunShaft"/>·<see cref="LightBeam"/>), 그러려면 그물에 구워진 문을
    /// 짝마다 떼어 내야 한다 — 동헌에서 한 것과 같은 일이다(<see cref="DonheonDoorCarve"/>).
    ///
    /// <b>자리는 그물이 말해 준다.</b> 창호지 재질(<c>MI_KoreanPaper_1</c>)의 삼각형만
    /// 앞면 켜에서 골라 x 로 늘어놓으면, 0.17m 짜리 빈 구간이 다섯 쌍 나온다 — 그것이
    /// 칸 가운데의 맞댐대고, 사이가 문짝이다. 짐작이 아니라 잰 값이다.
    ///
    /// 한 칸은 두 짝이고 칸이 다섯이라 모두 <b>열 짝</b>. 짝마다 제 돌쩌귀에서 따로
    /// 열리고, 칸 바깥쪽 기둥에 매달아 두 짝이 서로 반대로 열린다.
    /// </summary>
    public static class SeoricheongDoorCarve
    {
        private const string MenuPath = "Tools/異聞錄/문서고(서리청) 살문 떼어 내기";
        private const string MeshDir = "Assets/_Project/Onggojip/Art/문";
        private const string Host = "문서고_서리청";
        private const string RestName = "SM_Seoricheong_문뗀것";

        /// <summary>문짝이 선 켜(z)와 두께.</summary>
        private const float ZMid = -6.346f, ZThick = 0.34f;

        /// <summary>문짝이 걸친 높이 — 마루(0.84) 바로 위부터 창방 아래까지.</summary>
        private const float YLo = 0.80f, YHi = 3.05f;

        /// <summary>
        /// 칸 다섯의 <b>바깥 기둥 · 가운데 맞댐대 · 바깥 기둥</b>.
        /// 창호지가 끊긴 자리에서 잰 값이다.
        /// </summary>
        private static readonly float[][] Bays =
        {
            new[] { -8.717f, -8.008f, -7.298f },
            new[] { -5.695f, -4.989f, -4.283f },
            new[] { -2.683f, -1.978f, -1.272f },
            new[] {  0.329f,  1.035f,  1.741f },
            new[] {  3.338f,  4.048f,  4.757f },
        };

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var host = GameObject.Find(Host);
            if (host == null) { Debug.LogError("[문서고문] 「" + Host + "」을 못 찾았다."); return; }
            if (GameObject.Find("문서고_살문_1") != null) { Debug.LogWarning("[문서고문] 이미 떼어 두었다."); return; }

            var mf = host.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogError("[문서고문] 그물이 없다."); return; }
            var src = mf.sharedMesh;
            var rend = mf.GetComponent<Renderer>();
            var l2w = mf.transform.localToWorldMatrix;

            if (!AssetDatabase.IsValidFolder(MeshDir))
                AssetDatabase.CreateFolder("Assets/_Project/Onggojip/Art", "문");

            var v = src.vertices;
            var nrm = src.normals;
            var uv0 = src.uv;
            var uv1 = src.uv2;
            var tan = src.tangents;

            int leafCount = Bays.Length * 2;
            var leaf = new Piece[leafCount];
            for (int i = 0; i < leafCount; i++) leaf[i] = new Piece(v.Length);
            var restSubs = new List<int[]>();
            int cut = 0;

            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tri = src.GetTriangles(s);
                var rest = new List<int>();
                var kept = new List<int>[leafCount];
                for (int i = 0; i < leafCount; i++) kept[i] = new List<int>();

                for (int i = 0; i < tri.Length; i += 3)
                {
                    Vector3 a = l2w.MultiplyPoint3x4(v[tri[i]]);
                    Vector3 b = l2w.MultiplyPoint3x4(v[tri[i + 1]]);
                    Vector3 c = l2w.MultiplyPoint3x4(v[tri[i + 2]]);
                    Vector3 ctr = (a + b + c) / 3f;

                    int which = LeafOf(a, b, c);
                    if (which < 0)
                    {
                        rest.Add(tri[i]); rest.Add(tri[i + 1]); rest.Add(tri[i + 2]);
                        continue;
                    }
                    cut++;
                    for (int j = 0; j < 3; j++)
                        kept[which].Add(leaf[which].Take(tri[i + j], v, nrm, uv0, tan));
                }
                restSubs.Add(rest.ToArray());
                for (int i = 0; i < leafCount; i++)
                    if (kept[i].Count > 0) leaf[i].Push(kept[i].ToArray(), rend.sharedMaterials[s]);
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
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/" + RestName + ".asset");
            mf.sharedMesh = saved;
            var hostCol = host.GetComponent<MeshCollider>();
            if (hostCol != null) hostCol.sharedMesh = saved;

            // ── 문짝 열 ──
            var made = new List<SwingDoor>();
            for (int i = 0; i < leafCount; i++)
            {
                int bay = i / 2, side = i % 2;
                var dm = leaf[i].Bake("문서고_살문_" + (i + 1));
                if (dm == null) { Debug.LogWarning("[문서고문] " + (i + 1) + "번 짝이 비었다"); continue; }
                AssetDatabase.CreateAsset(dm, MeshDir + "/문서고_살문_" + (i + 1) + ".asset");
                var dsaved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/문서고_살문_" + (i + 1) + ".asset");

                var obj = new GameObject("문서고_살문_" + (i + 1));
                obj.transform.SetParent(host.transform.parent, false);
                obj.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                obj.transform.localScale = mf.transform.lossyScale;
                obj.AddComponent<MeshFilter>().sharedMesh = dsaved;
                obj.AddComponent<MeshRenderer>().sharedMaterials = leaf[i].mats.ToArray();
                var bc = obj.AddComponent<BoxCollider>();
                bc.center = dsaved.bounds.center;
                bc.size = dsaved.bounds.size;

                var sd = obj.AddComponent<SwingDoor>();
                // 두 짝은 칸의 <b>바깥 기둥</b>에 매달려 서로 반대로 열린다.
                float hinge = side == 0 ? Bays[bay][0] : Bays[bay][2];
                float angle = side == 0 ? -85f : 85f;
                sd.Setup(new[] { obj.transform }, new Vector3(hinge, 1.9f, ZMid), angle, new SwingDoor[0]);
                obj.AddComponent<SunShaft>();      // 짝마다 볕 한 줄기
                made.Add(sd);
            }

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(host.scene);
            Debug.Log("[문서고문] 삼각형 " + cut + "개를 떼어 " + made.Count + "짝으로 세웠다.");
        }

        /// <summary>
        /// 이 삼각형이 어느 짝의 것인가. −1 이면 집에 남는다.
        ///
        /// <b>무게중심으로 가르면 안 된다.</b> 처음에 그렇게 했더니 문짝 하나가 3m 나
        /// 되었다 — 앞면을 가로지르는 <b>긴 인방·문지방</b>이 있고, 그 긴 삼각형의
        /// 무게중심이 마침 한 짝 안에 떨어지면 대들보가 통째로 그 짝에 딸려 나온다.
        /// 그러면 문을 여는 순간 집의 가로대가 같이 돌아간다.
        ///
        /// 그러니 <b>세 꼭짓점이 다</b> 그 짝 안에 들어야 한다. 걸치는 것은 집에 남긴다 —
        /// 짝 사이를 잇는 부재는 원래 집의 것이다.
        /// </summary>
        private static int LeafOf(Vector3 a, Vector3 b, Vector3 c)
        {
            float ylo = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), yhi = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            if (ylo < YLo || yhi > YHi) return -1;
            float zlo = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), zhi = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
            if (zlo < ZMid - ZThick * 0.5f || zhi > ZMid + ZThick * 0.5f) return -1;

            float xlo = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), xhi = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            for (int i = 0; i < Bays.Length; i++)
            {
                if (xlo < Bays[i][0] || xhi > Bays[i][2]) continue;
                float mid = Bays[i][1];
                if (xhi <= mid + 0.001f) return i * 2;
                if (xlo >= mid - 0.001f) return i * 2 + 1;
                return -1;      // 맞댐대를 걸친다 — 집에 남긴다
            }
            return -1;
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

            public Piece(int n) { _map = new int[n]; for (int i = 0; i < n; i++) _map[i] = -1; }

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
                if (_v.Count == 0) return null;
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
