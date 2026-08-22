using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 너무 넓게 퍼진 메시를 <b>격자로 쪼갠다</b>. 메뉴: [이문록 ▸ 에셋: 넓게 퍼진 메시 쪼개기]
    ///
    /// 왜 필요한가: 고택 지붕 두 채가 한 덩이로 <b>30.1m·35.6m</b> 다(SM_RoofE 414,464 ·
    /// SM_RoofG 408,777). LOD 는 물체가 화면에서 차지하는 높이로 단을 고르는데, 이만한
    /// 덩이는 마당 어디에 서도 화면을 크게 차지하므로 <b>늘 원본</b>이 나온다.
    /// 문턱을 상한(1.0)까지 올려도 26~31m 안이 원본이고, 고택이 60m 남짓이니 소용이 없다.
    ///
    /// 먼거리 판을 더 얇게 굽는 길도 막혀 있다 — 8% 는 물론 25% 로 구워도 처마 밑에서
    /// 서까래가 조각나 보인다. 이 지붕들은 가까이서는 원본 말고 답이 없다.
    ///
    /// 그러니 <b>덩이를 줄인다</b>. 10m 칸으로 쪼개면 조각 하나가 10m 짜리가 되어,
    /// 발치의 한두 조각만 원본이고 나머지는 중간 판으로 내려간다. 쪼개도 <b>정점은 그대로</b>
    /// 쓰므로 이음매가 벌어지지 않는다 — 삼각형을 어느 조각에 넣을지만 가른다.
    ///
    /// 재질도 그대로 간다. 삼각형을 서브메시별로 갈라 담으므로 조각마다 원본과 같은
    /// 서브메시 차례를 갖는다.
    ///
    /// 원본 렌더러는 <b>끄기만</b> 한다(지우지 않는다). 마음에 안 들면 조각을 지우고
    /// 원본 렌더러를 도로 켜면 된다.
    ///
    /// 쪼갠 뒤에는 [이문록 ▸ 에셋: 무거운 메시에 먼거리 판 붙이기] 를 한 번 더 돌린다.
    /// </summary>
    public static class BigMeshSplitter
    {
        /// <summary>이 삼각형 수를 넘고,</summary>
        private const int TriThreshold = 100000;

        /// <summary>이만큼 넓게 퍼진 메시만 쪼갠다(m).</summary>
        private const float SizeThreshold = 20f;

        /// <summary>격자 한 칸(m). 조각 하나가 이만해진다.</summary>
        private const float Cell = 10f;

        private const string PieceRoot = "_조각";

        [MenuItem("이문록/에셋: 넓게 퍼진 메시 쪼개기")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[쪼개기] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var work = new List<MeshFilter>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var m = mf.sharedMesh;
                if (m == null || !m.isReadable) continue;
                if (m.triangles.Length / 3 < TriThreshold) continue;
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || !r.enabled) continue;
                if (mf.transform.parent != null && mf.transform.parent.name == PieceRoot) continue;   // 내가 만든 조각
                if (r.bounds.size.magnitude < SizeThreshold) continue;
                work.Add(mf);
            }

            if (work.Count == 0)
            {
                Debug.Log($"[쪼개기] 삼각형 {TriThreshold} 이상이면서 {SizeThreshold}m 넘게 퍼진 메시가 없습니다.");
                return;
            }

            var log = new StringBuilder();
            int total = 0;
            try
            {
                for (int i = 0; i < work.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("넓게 퍼진 메시 쪼개기",
                        work[i].name + " (" + (i + 1) + "/" + work.Count + ")", (float)(i + 1) / work.Count);
                    total += Split(work[i], log);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[쪼개기] {work.Count}덩이를 {total}조각으로 쪼갰습니다.\n" + log
                      + "\n이제 [이문록 ▸ 에셋: 무거운 메시에 먼거리 판 붙이기] 를 한 번 더 돌리십시오.");
        }

        /// <summary>
        /// 한 덩이를 격자로 쪼갠다. 삼각형의 <b>무게중심</b>이 어느 칸에 드는가로 가른다 —
        /// 꼭짓점으로 가르면 칸 경계에 걸친 삼각형이 두 조각에 다 들어가거나 어디에도 안 든다.
        /// </summary>
        private static int Split(MeshFilter mf, StringBuilder log)
        {
            var go = mf.gameObject;
            var src = mf.sharedMesh;
            var srcR = go.GetComponent<MeshRenderer>();
            var l2w = go.transform.localToWorldMatrix;

            var vs = src.vertices; var ns = src.normals; var uv = src.uv; var tg = src.tangents;

            // 칸 → (서브메시 → 삼각형 인덱스)
            var cells = new Dictionary<Vector2Int, List<int>[]>();
            for (int si = 0; si < src.subMeshCount; si++)
            {
                var tri = src.GetTriangles(si);
                for (int i = 0; i < tri.Length; i += 3)
                {
                    var c = (l2w.MultiplyPoint3x4(vs[tri[i]]) + l2w.MultiplyPoint3x4(vs[tri[i + 1]])
                             + l2w.MultiplyPoint3x4(vs[tri[i + 2]])) / 3f;
                    var key = new Vector2Int(Mathf.FloorToInt(c.x / Cell), Mathf.FloorToInt(c.z / Cell));
                    if (!cells.TryGetValue(key, out var subs))
                    {
                        subs = new List<int>[src.subMeshCount];
                        for (int k = 0; k < subs.Length; k++) subs[k] = new List<int>();
                        cells[key] = subs;
                    }
                    subs[si].Add(tri[i]); subs[si].Add(tri[i + 1]); subs[si].Add(tri[i + 2]);
                }
            }

            if (cells.Count <= 1) { log.AppendLine("   " + go.name + " : 한 칸에 다 들어가 쪼갤 것이 없습니다."); return 0; }

            var root = go.transform.Find(PieceRoot);
            if (root != null) Undo.DestroyObjectImmediate(root.gameObject);
            var rootGo = new GameObject(PieceRoot);
            Undo.RegisterCreatedObjectUndo(rootGo, "메시 쪼개기");
            rootGo.transform.SetParent(go.transform, false);

            string dir = OutDir(src);
            int made = 0;
            foreach (var kv in cells)
            {
                var m = BuildPiece(src, vs, ns, uv, tg, kv.Value, out int tri);
                if (m == null || tri == 0) continue;

                string stem = Sanitize(go.name) + "_" + kv.Key.x + "_" + kv.Key.y;
                m.name = stem;
                string path = dir + "/" + stem + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null) { EditorUtility.CopySerialized(m, existing); Object.DestroyImmediate(m); m = existing; }
                else AssetDatabase.CreateAsset(m, path);

                var piece = new GameObject(stem);
                piece.transform.SetParent(rootGo.transform, false);
                GameObjectUtility.SetStaticEditorFlags(piece, GameObjectUtility.GetStaticEditorFlags(go));
                piece.AddComponent<MeshFilter>().sharedMesh = m;
                var pr = piece.AddComponent<MeshRenderer>();
                pr.sharedMaterials = srcR.sharedMaterials;
                pr.shadowCastingMode = srcR.shadowCastingMode;
                pr.receiveShadows = srcR.receiveShadows;
                pr.lightProbeUsage = srcR.lightProbeUsage;
                made++;
            }

            // 원본은 끄기만 한다. 되돌리려면 조각을 지우고 이걸 도로 켜면 된다.
            // 원본에 붙어 있던 LOD 는 조각마다 새로 붙일 것이므로 걷어낸다.
            var oldLod = go.GetComponent<LODGroup>();
            if (oldLod != null) Undo.DestroyObjectImmediate(oldLod);
            foreach (var name in new[] { "_먼거리", "_먼거리_중간" })
            {
                var c = go.transform.Find(name);
                if (c != null) Undo.DestroyObjectImmediate(c.gameObject);
            }
            Undo.RecordObject(srcR, "원본 끄기");
            srcR.enabled = false;
            EditorUtility.SetDirty(go);

            log.AppendLine("   " + go.name + " : " + (src.triangles.Length / 3).ToString("N0")
                           + " → " + made + "조각");
            return made;
        }

        /// <summary>칸 하나의 삼각형으로 메시를 만든다. 쓰는 정점만 추려 번호를 다시 매긴다.</summary>
        private static Mesh BuildPiece(Mesh src, Vector3[] vs, Vector3[] ns, Vector2[] uv, Vector4[] tg,
                                       List<int>[] subs, out int triCount)
        {
            var map = new Dictionary<int, int>();
            var nv = new List<Vector3>(); var nn = new List<Vector3>();
            var nu = new List<Vector2>(); var nt = new List<Vector4>();
            var outSubs = new List<int[]>();
            triCount = 0;

            foreach (var list in subs)
            {
                var arr = new int[list.Count];
                for (int k = 0; k < list.Count; k++)
                {
                    int o = list[k];
                    if (!map.TryGetValue(o, out int n))
                    {
                        n = nv.Count; map[o] = n;
                        nv.Add(vs[o]);
                        nn.Add(ns.Length > o ? ns[o] : Vector3.up);
                        nu.Add(uv.Length > o ? uv[o] : Vector2.zero);
                        nt.Add(tg.Length > o ? tg[o] : new Vector4(1f, 0f, 0f, 1f));
                    }
                    arr[k] = n;
                }
                outSubs.Add(arr);
                triCount += arr.Length / 3;
            }
            if (triCount == 0) return null;

            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(nv); m.SetNormals(nn); m.SetUVs(0, nu); m.SetTangents(nt);
            m.subMeshCount = outSubs.Count;
            for (int i = 0; i < outSubs.Count; i++) m.SetTriangles(outSubs[i], i);
            m.RecalculateBounds();
            return m;
        }

        private static string OutDir(Mesh src)
        {
            string srcPath = AssetDatabase.GetAssetPath(src);
            string dir = string.IsNullOrEmpty(srcPath)
                ? "Assets/_Project/Art/Props"
                : System.IO.Path.GetDirectoryName(srcPath).Replace('\\', '/');
            string outDir = dir + "/_쪼갠메시";
            if (!AssetDatabase.IsValidFolder(outDir)) AssetDatabase.CreateFolder(dir, "_쪼갠메시");
            return outDir;
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s) sb.Append(System.Array.IndexOf(System.IO.Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            return sb.ToString();
        }
    }
}
