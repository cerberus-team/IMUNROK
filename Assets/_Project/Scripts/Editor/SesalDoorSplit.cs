using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>사분합문을 네 짝으로 가른다.</b>
    ///
    /// 문서고 앞 판문은 한 칸이 <b>네 짝</b>인데(사진을 세어 보면 칸마다 살문이 넷이다),
    /// 그물로는 두 덩이였다 — <c>SM_Door_Sesal_r40</c> 하나가 두 짝을 통째로 물고 있다.
    /// 그래서 여태 한 번 누르면 칸이 통째로 열렸다. 조사청의 문처럼 <b>한 짝씩</b>
    /// 열리게 하려면 그물을 갈라야 한다.
    ///
    /// <b>갈라도 되나.</b> 재어 보니 한가운데(local y 의 가운데)를 <b>걸치는 삼각형이
    /// 하나도 없다</b>. 두 짝이 맞닿아 있을 뿐 이어 붙지 않았다는 뜻이라, 가운데에서
    /// 자르면 이 빠진 데 없이 둘로 떨어진다.
    ///
    /// <b>왜 이것이 놀이와 얽히나.</b> 문서고는 어두워야 등불이 뜻을 가진다. 그런데
    /// 칸이 통째로 열리면 한낮의 볕이 죄 쏟아져 들어와 어둠도 등불도 없어진다.
    /// 짝 하나만 열면 들어오는 것은 <b>한 줄기</b>다 — 그 줄기를 거울로 꺾어
    /// 글자에 앉히는 것이 이 방의 놀이가 된다. 그러니 이 가름은 꾸밈이 아니라
    /// 놀이의 밑돌이다.
    /// </summary>
    public static class SesalDoorSplit
    {
        private const string MenuPath = "Tools/異聞錄/문서고 사분합문 네 짝으로 가르기";

        /// <summary>뒷면 껍데기의 이름 꼬리.</summary>
        private const string SkinSuffix = "_겉";

        /// <summary>가른 그물을 놓아 둘 데.</summary>
        private const string MeshDir = "Assets/_Project/Onggojip/Art/문";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var root = Find("판문");
            if (root == null) { Debug.LogError("[문가르기] 「판문」을 못 찾았다."); return; }

            var units = new List<SwingDoor>(root.GetComponentsInChildren<SwingDoor>(true));
            if (units.Count == 0) { Debug.LogError("[문가르기] 판문 밑에 문이 없다."); return; }
            if (units.Count >= 12) { Debug.LogWarning("[문가르기] 이미 갈라 둔 듯하다 — 문이 " + units.Count + "짝이다."); return; }

            if (!AssetDatabase.IsValidFolder(MeshDir))
                AssetDatabase.CreateFolder("Assets/_Project/Onggojip/Art", "문");

            int made = 0;
            foreach (var unit in units) made += Split(unit);

            AssetDatabase.SaveAssets();
            Rename(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log("[문가르기] " + units.Count + "덩이를 " + made + "짝으로 갈랐다.");
        }

        /// <summary>한 덩이를 두 짝으로. 만든 짝 수를 돌려준다.</summary>
        private static int Split(SwingDoor unit)
        {
            var so = new SerializedObject(unit);
            var partsProp = so.FindProperty("_parts");
            float angle = so.FindProperty("_openAngle").floatValue;
            Vector3 hinge = so.FindProperty("_hinge").vector3Value;

            Transform door = unit.transform;
            Transform skin = null;
            var stiles = new List<Transform>();
            for (int i = 0; i < partsProp.arraySize; i++)
            {
                var t = partsProp.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (t == null || t == door) continue;
                if (t.name.EndsWith(SkinSuffix)) skin = t;
                else stiles.Add(t);
            }

            var mf = door.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("[문가르기] 그물이 없다 — " + door.name, door); return 0; }

            Mesh lo = Half(mf.sharedMesh, true);
            Mesh hi = Half(mf.sharedMesh, false);
            if (lo == null || hi == null) { Debug.LogWarning("[문가르기] 가를 수 없다 — " + door.name, door); return 0; }

            // 이 덩이가 선 폭. 돌쩌귀의 반대편 끝이 안짝의 돌쩌귀가 된다.
            Bounds span = door.GetComponent<Renderer>().bounds;
            float far = Mathf.Abs(span.min.x - hinge.x) > Mathf.Abs(span.max.x - hinge.x) ? span.min.x : span.max.x;

            // 두 짝을 뜬다. 어느 반쪽이 어느 자리에 서는지는 <b>세계 자리</b>로 가려낸다 —
            // 뒷면 껍데기는 x 배율이 −1 이라 그물의 왼쪽이 세계의 오른쪽에 선다.
            GameObject dA = Clone(door.gameObject, lo, "_가");
            GameObject dB = Clone(door.gameObject, hi, "_나");
            GameObject sA = skin != null ? Clone(skin.gameObject, lo, "_가") : null;
            GameObject sB = skin != null ? Clone(skin.gameObject, hi, "_나") : null;

            for (int k = 0; k < 2; k++)
            {
                GameObject leaf = k == 0 ? dA : dB;
                float x = leaf.GetComponent<Renderer>().bounds.center.x;
                bool outer = Mathf.Abs(x - hinge.x) < Mathf.Abs(x - far);

                var parts = new List<Transform>();
                parts.Add(leaf.transform);

                GameObject mySkin = Nearest(sA, sB, x);
                if (mySkin != null) parts.Add(mySkin.transform);

                float myHinge = outer ? hinge.x : far;
                float myAngle = outer ? angle : -angle;

                // 맞댐대는 <b>자유로운 쪽 끝</b>에 선 것만 데려간다. 돌쩌귀 위에 선 것
                // (칸 한가운데의 이음)은 문설주 노릇이니 제자리에 둔다.
                Bounds lb = leaf.GetComponent<Renderer>().bounds;
                float freeEdge = Mathf.Abs(lb.min.x - myHinge) > Mathf.Abs(lb.max.x - myHinge) ? lb.min.x : lb.max.x;
                foreach (var st in stiles)
                    if (st != null && Mathf.Abs(st.position.x - freeEdge) < 0.12f) parts.Add(st);

                var sd = leaf.GetComponent<SwingDoor>();
                if (sd == null) sd = leaf.AddComponent<SwingDoor>();
                sd.Setup(parts.ToArray(), new Vector3(myHinge, hinge.y, hinge.z), myAngle, new SwingDoor[0]);
                EditorUtility.SetDirty(sd);
            }

            if (sA != null) Strip(sA);
            if (sB != null) Strip(sB);

            Object.DestroyImmediate(door.gameObject);
            if (skin != null) Object.DestroyImmediate(skin.gameObject);
            return 2;
        }

        /// <summary>세계 x 가 더 가까운 쪽.</summary>
        private static GameObject Nearest(GameObject a, GameObject b, float x)
        {
            if (a == null) return b;
            if (b == null) return a;
            float da = Mathf.Abs(a.GetComponent<Renderer>().bounds.center.x - x);
            float db = Mathf.Abs(b.GetComponent<Renderer>().bounds.center.x - x);
            return da <= db ? a : b;
        }

        /// <summary>껍데기는 문이 아니다 — 여닫는 부품과 부딪는 몸을 걷는다.</summary>
        private static void Strip(GameObject go)
        {
            var sd = go.GetComponent<SwingDoor>();
            if (sd != null) Object.DestroyImmediate(sd);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        /// <summary>같은 자리에 그물만 갈아 낀 벌을 하나 뜬다.</summary>
        private static GameObject Clone(GameObject src, Mesh mesh, string suffix)
        {
            var go = Object.Instantiate(src, src.transform.parent);
            go.name = src.name + suffix;
            go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            go.transform.localScale = src.transform.localScale;

            var mf = go.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = mesh;

            var bc = go.GetComponent<BoxCollider>();
            if (bc != null) { bc.center = mesh.bounds.center; bc.size = mesh.bounds.size; }
            return go;
        }

        /// <summary>
        /// 한가운데에서 자른 반쪽. 걸치는 삼각형은 <b>버리지 않고</b> 무게중심이 있는 쪽에
        /// 붙인다 — 이 문에는 걸치는 것이 없지만, 다른 문에 썼다가 구멍이 나면 안 된다.
        /// </summary>
        private static Mesh Half(Mesh src, bool lower)
        {
            var v = src.vertices;
            var n = src.normals;
            var uv = src.uv;
            var uv2 = src.uv2;
            var tan = src.tangents;
            float mid = src.bounds.center.y;

            var map = new int[v.Length];
            for (int i = 0; i < map.Length; i++) map[i] = -1;

            var nv = new List<Vector3>();
            var nn = new List<Vector3>();
            var nuv = new List<Vector2>();
            var nuv2 = new List<Vector2>();
            var ntan = new List<Vector4>();
            var subs = new List<int[]>();
            int total = 0;

            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tri = src.GetTriangles(s);
                var keep = new List<int>();
                for (int i = 0; i < tri.Length; i += 3)
                {
                    float mean = (v[tri[i]].y + v[tri[i + 1]].y + v[tri[i + 2]].y) / 3f;
                    if ((mean <= mid) != lower) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int o = tri[i + k];
                        if (map[o] < 0)
                        {
                            map[o] = nv.Count;
                            nv.Add(v[o]);
                            if (n.Length == v.Length) nn.Add(n[o]);
                            if (uv.Length == v.Length) nuv.Add(uv[o]);
                            if (uv2.Length == v.Length) nuv2.Add(uv2[o]);
                            if (tan.Length == v.Length) ntan.Add(tan[o]);
                        }
                        keep.Add(map[o]);
                    }
                }
                total += keep.Count;
                subs.Add(keep.ToArray());
            }
            if (total == 0) return null;

            var dst = new Mesh();
            dst.name = src.name + (lower ? "_가" : "_나");
            dst.indexFormat = nv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32
                                               : UnityEngine.Rendering.IndexFormat.UInt16;
            dst.SetVertices(nv);
            if (nn.Count == nv.Count) dst.SetNormals(nn);
            if (nuv.Count == nv.Count) dst.SetUVs(0, nuv);
            if (nuv2.Count == nv.Count) dst.SetUVs(1, nuv2);
            if (ntan.Count == nv.Count) dst.SetTangents(ntan);
            dst.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) dst.SetTriangles(subs[s], s);
            dst.RecalculateBounds();

            string path = AssetDatabase.GenerateUniqueAssetPath(MeshDir + "/" + dst.name + ".asset");
            AssetDatabase.CreateAsset(dst, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        /// <summary>가르고 나서 왼쪽부터 차례로 이름을 다시 준다 — 칸마다 짝1~짝4.</summary>
        private static void Rename(Transform root)
        {
            var doors = new List<SwingDoor>(root.GetComponentsInChildren<SwingDoor>(true));
            doors.Sort(delegate (SwingDoor a, SwingDoor b)
            {
                return a.GetComponent<Renderer>().bounds.center.x
                    .CompareTo(b.GetComponent<Renderer>().bounds.center.x);
            });
            for (int i = 0; i < doors.Count; i++)
            {
                int bay = i / 4 + 1;
                int leaf = i % 4 + 1;
                doors[i].name = "문_" + bay + "_짝" + leaf;
            }
        }

        private static Transform Find(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }
    }
}
