using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 장부의 글을 <b>책 지면에 새긴다</b>. 메뉴: [이문록 ▸ 서책 ▸ 지면을 책에 새기기]
    ///
    /// <b>지금까지 어땠나</b>: 펼친 서책 위에 유니티 기본 Quad 한 장을 띄우고 거기에
    /// 문서 그림을 붙여 왔다. 그 판은 책의 가장 높은 곳보다 4mm 위에 떠 있고, 오른쪽
    /// 지면은 낮게 눕는 책이라 그쪽에서는 <b>3cm 남짓 공중에 뜬다</b>. 가까이 대고 읽는
    /// 물증인데 글이 종이에 얹힌 것이 아니라 종이 위를 맴돈다.
    ///
    /// <b>왜 예전에 이렇게 했나</b>: 책 모델의 UV 는 펼친 지면을 좌우 반쪽으로 나눠
    /// 각각 같은 자리에 감아 두었다. 거기에 문서를 얹으면 제목이 두 번 찍힌다.
    /// 그래서 판을 띄우는 쪽으로 물러섰다(73c41b5).
    ///
    /// <b>이 도구가 하는 일</b>: 모델의 UV 를 고치려 들지 않는다 — 남의 언랩이라
    /// 손대면 다른 것이 깨진다. 대신 <b>지면 삼각형만 골라 새 서브메시로 떼어내고</b>,
    /// 그 정점에만 위에서 내려다본 평면 투영으로 UV 를 다시 매긴다. 책 몸통은
    /// 서브메시 0 에 남아 무지 종이 재질을 그대로 쓰고, 지면은 서브메시 1 이 되어
    /// 문서 재질을 받는다. 몸통 재질에는 텍스처가 없으므로 그쪽 UV 가 어떻게 되든
    /// 보이는 것은 달라지지 않는다.
    ///
    /// <b>지면을 어떻게 고르나</b>: "위를 보면서, 그 자리에서 가장 높은 면". XZ 를
    /// 48x32 칸으로 나눠 칸마다 최고 높이를 재 두고, 위를 보는 삼각형 중 제 칸의
    /// 꼭대기에서 <see cref="TopTol"/> 안에 드는 것만 남긴다. 단순히 "y 가 높은 것"으로
    /// 고르면 왼쪽 지면만 잡힌다 — 이 책은 넘긴 장이 왼쪽에 쌓여 오른쪽이 3cm 낮다.
    ///
    /// <b>UV 방향</b>: 지금 떠 있는 판과 똑같이 맞춘다 — u 는 월드 +X, v 는 월드 +Z.
    /// 책 메시의 로컬축이 월드축과 나란해서(회전이 위아래로 두 번 상쇄된다) 그대로 쓴다.
    /// 판은 책보다 87% 크기로 조금 안쪽에 있었는데, 문서 그림이 제 여백을 갖고 있으므로
    /// 여기서는 지면을 꽉 채운다. 그래야 가장자리에서 늘어난 픽셀이 안 나온다.
    ///
    /// ★되돌리려면 git 으로 프리팹 둘을 되돌리면 된다. 만들어진 메시는 새 에셋이라
    ///  원본 glb 는 건드리지 않는다.
    /// </summary>
    public static class BookPageEngrave
    {
        private const string SrcMeshPath = "Assets/_Project/Art/Props/서책.glb";
        private const string OutMeshPath = "Assets/_Project/Onggojip/Art/서책_지면_Mesh.asset";
        private const string PaperMat = "Assets/_Project/Onggojip/Art/Materials/서책_종이_Mat.mat";
        private const string PageChild = "지면";

        /// <summary>제 칸의 꼭대기에서 이만큼 안이면 지면으로 본다(메시 단위, 1 = 1cm 남짓).</summary>
        private const float TopTol = 0.4f;

        /// <summary>이보다 눕지 않은 면만 지면 후보다(0.30 = 72도까지).</summary>
        private const float UpDot = 0.30f;

        private static readonly string[,] Books =
        {
            { "Assets/_Project/Onggojip/Prefabs/장부_전조기_J09.prefab",
              "Assets/_Project/Onggojip/Art/Materials/문서_장부_Mat.mat" },
            { "Assets/_Project/Onggojip/Prefabs/장부_물목기_J10.prefab",
              "Assets/_Project/Onggojip/Art/Materials/문서_물목_Mat.mat" },
        };

        [MenuItem("이문록/서책/지면을 책에 새기기")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[서책] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var src = FindBookMesh();
            if (src == null) { Debug.LogError("[서책] " + SrcMeshPath + " 안에서 책 메시를 못 찾았습니다."); return; }

            var log = new StringBuilder();
            var baked = Build(src, log);
            if (baked == null) { Debug.LogError("[서책] 지면 삼각형을 하나도 못 골랐습니다.\n" + log); return; }

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(OutMeshPath);
            if (existing != null) { EditorUtility.CopySerialized(baked, existing); Object.DestroyImmediate(baked); baked = existing; }
            else AssetDatabase.CreateAsset(baked, OutMeshPath);
            AssetDatabase.SaveAssets();

            var paper = AssetDatabase.LoadAssetAtPath<Material>(PaperMat);
            for (int i = 0; i < Books.GetLength(0); i++)
                Rewire(Books[i, 0], baked, paper, AssetDatabase.LoadAssetAtPath<Material>(Books[i, 1]), log);

            AssetDatabase.Refresh();
            Debug.Log("[서책] 지면을 책에 새겼습니다.\n" + log);
        }

        private static Mesh FindBookMesh()
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(SrcMeshPath))
            {
                var m = o as Mesh;
                if (m != null && m.vertexCount > 100) return m;
            }
            return null;
        }

        /// <summary>지면을 갈라낸 새 메시를 만든다. 정점은 그대로 두고 삼각형만 나눈다.</summary>
        private static Mesh Build(Mesh src, StringBuilder log)
        {
            var v = src.vertices;
            var tris = src.triangles;
            var b = src.bounds;

            // ── 칸마다 꼭대기 높이 ──
            const int NX = 48, NZ = 32;
            var top = new float[NX, NZ];
            for (int i = 0; i < NX; i++) for (int j = 0; j < NZ; j++) top[i, j] = float.NegativeInfinity;
            for (int i = 0; i < v.Length; i++)
            {
                int a = Bin(v[i].x, b.min.x, b.size.x, NX), c = Bin(v[i].z, b.min.z, b.size.z, NZ);
                if (v[i].y > top[a, c]) top[a, c] = v[i].y;
            }

            // ── 지면 삼각형 고르기 ──
            var page = new List<int>();
            var body = new List<int>();
            var onPage = new bool[v.Length];
            for (int i = 0; i < tris.Length; i += 3)
            {
                var p0 = v[tris[i]]; var p1 = v[tris[i + 1]]; var p2 = v[tris[i + 2]];
                var n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                var ct = (p0 + p1 + p2) / 3f;
                bool isPage = n.y >= UpDot && ct.y >= top[Bin(ct.x, b.min.x, b.size.x, NX),
                                                          Bin(ct.z, b.min.z, b.size.z, NZ)] - TopTol;
                var into = isPage ? page : body;
                into.Add(tris[i]); into.Add(tris[i + 1]); into.Add(tris[i + 2]);
                if (isPage) for (int k = 0; k < 3; k++) onPage[tris[i + k]] = true;
            }
            if (page.Count == 0) return null;

            // ── 지면 정점에만 평면 투영 UV ──
            float x0 = float.PositiveInfinity, x1 = float.NegativeInfinity;
            float z0 = float.PositiveInfinity, z1 = float.NegativeInfinity;
            for (int i = 0; i < v.Length; i++)
            {
                if (!onPage[i]) continue;
                if (v[i].x < x0) x0 = v[i].x;
                if (v[i].x > x1) x1 = v[i].x;
                if (v[i].z < z0) z0 = v[i].z;
                if (v[i].z > z1) z1 = v[i].z;
            }
            var uv = src.uv;
            if (uv == null || uv.Length != v.Length) uv = new Vector2[v.Length];
            int moved = 0;
            for (int i = 0; i < v.Length; i++)
            {
                if (!onPage[i]) continue;
                uv[i] = new Vector2(Mathf.InverseLerp(x0, x1, v[i].x), Mathf.InverseLerp(z0, z1, v[i].z));
                moved++;
            }

            var mesh = Object.Instantiate(src);
            mesh.name = "서책_지면_Mesh";
            mesh.uv = uv;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(page, 1);
            mesh.RecalculateBounds();

            log.AppendLine("   지면 삼각형 " + (page.Count / 3) + " / 몸통 " + (body.Count / 3)
                           + "   UV 다시 매긴 정점 " + moved);
            log.AppendLine("   지면 넓이 x " + x0.ToString("F1") + ".." + x1.ToString("F1")
                           + "  z " + z0.ToString("F1") + ".." + z1.ToString("F1"));
            return mesh;
        }

        private static int Bin(float value, float min, float size, int n)
        {
            return Mathf.Clamp(Mathf.FloorToInt((value - min) / Mathf.Max(1e-6f, size) * n), 0, n - 1);
        }

        /// <summary>프리팹의 책 렌더러에 새 메시와 재질 두 벌을 물리고, 떠 있던 판을 걷는다.</summary>
        private static void Rewire(string prefabPath, Mesh mesh, Material paper, Material doc, StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) { log.AppendLine("   ✘ " + prefabPath + " 를 못 열었습니다"); return; }
            try
            {
                MeshFilter target = null;
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && mf.sharedMesh.vertexCount > 100) { target = mf; break; }

                if (target == null) { log.AppendLine("   ✘ " + prefabPath + " 안에서 책 렌더러를 못 찾았습니다"); return; }

                target.sharedMesh = mesh;
                var r = target.GetComponent<MeshRenderer>();
                if (r != null) r.sharedMaterials = new[] { paper, doc };

                var quad = root.transform.Find(PageChild);
                bool dropped = quad != null;
                if (dropped) Object.DestroyImmediate(quad.gameObject);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                log.AppendLine("   " + System.IO.Path.GetFileNameWithoutExtension(prefabPath)
                               + " : 메시 갈아 끼움 · 재질 [" + (paper != null ? paper.name : "null") + ", "
                               + (doc != null ? doc.name : "null") + "]"
                               + (dropped ? " · 떠 있던 판 걷음" : " · 판은 이미 없었음"));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
