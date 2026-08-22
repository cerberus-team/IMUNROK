using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 지나치게 잘게 쪼개진 메시를 줄여 새 메시로 굽고, 씬의 오브젝트에 갈아 끼운다.
    ///
    /// 왜 필요한가: 받아온 고택과 AI 소품이 말이 안 되게 촘촘하다 —
    /// 지붕 한 채가 41만 삼각형, 10cm짜리 소품 하나가 10만 삼각형이다.
    /// 기와 굴곡은 이미 노멀맵이 하고 있어서, 그 촘촘함은 화면에 보이지 않고
    /// 프레임만 갉아먹는다. 퀘스트 한 프레임 예산이 20~50만인데 이 씬은 430만이다.
    ///
    /// 어떻게 줄이나 — <b>격자 뭉치기(vertex clustering)</b>:
    /// 공간을 일정한 크기의 칸으로 나누고, 한 칸에 들어온 정점들을 하나로 합친다.
    /// 그 결과 납작해진 삼각형(세 꼭짓점이 같은 칸)은 버린다.
    /// 촘촘하게 고르게 쪼개진 메시에는 이 방법이 잘 듣는다 — 모양의 큰 흐름은 남고
    /// 눈에 안 보이는 잔 조각만 사라진다.
    ///
    /// 지키는 것:
    ///   · 서브메시(재질 나눔) — 칸을 재질별로 따로 센다. 안 그러면 재질이 섞인다.
    ///   · UV 이음새 — 자리가 같아도 UV가 크게 다르면 합치지 않는다. 합치면 텍스처가 찢어진다.
    ///   · 법선 — 칸 안 정점들의 평균으로. 부드러운 면이 각지지 않는다.
    ///
    /// 원본은 건드리지 않는다. 새 메시를 별도 파일로 굽고 씬의 MeshFilter 만 바꾼다 —
    /// 되돌리려면 씬만 되돌리면 된다.
    ///
    /// 메뉴: [이문록 ▸ 에셋: 무거운 메시 줄이기]
    /// </summary>
    public static class MeshDecimator
    {
        /// <summary>이 삼각형 수를 넘는 메시만 손댄다.</summary>
        private const int Threshold = 8000;

        /// <summary>줄인 뒤 메시를 넣을 폴더 이름(원본 옆에 만든다).</summary>
        private const string OutFolder = "_간소화메시";

        private static bool _showDialog;

        [MenuItem("이문록/에셋: 무거운 메시 줄이기")]
        private static void FromMenu() { _showDialog = true; try { Run(); } finally { _showDialog = false; } }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("이문록", "플레이를 멈추고 다시 실행하세요.", "확인");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var targets = new Dictionary<Mesh, List<MeshFilter>>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var m = mf.sharedMesh;
                if (m == null || m.triangles.Length / 3 <= Threshold) continue;
                if (!m.isReadable) continue;                       // 못 읽는 메시는 건드릴 수 없다
                if (!targets.TryGetValue(m, out var list)) targets[m] = list = new List<MeshFilter>();
                list.Add(mf);
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("이문록", $"삼각형 {Threshold} 개를 넘는 메시가 없습니다.", "확인");
                return;
            }

            long before = 0, after = 0;
            int done = 0;
            var log = new System.Text.StringBuilder();

            try
            {
                int i = 0;
                foreach (var kv in targets)
                {
                    var src = kv.Key;
                    EditorUtility.DisplayProgressBar("무거운 메시 줄이기",
                        src.name + " (" + (++i) + "/" + targets.Count + ")", (float)i / targets.Count);

                    int srcTri = src.triangles.Length / 3;
                    int target = TargetTriangles(src);
                    before += (long)srcTri * kv.Value.Count;

                    var baked = BakeSimplified(src, target);
                    if (baked == null) { after += (long)srcTri * kv.Value.Count; continue; }

                    int newTri = baked.triangles.Length / 3;
                    after += (long)newTri * kv.Value.Count;
                    foreach (var mf in kv.Value) { mf.sharedMesh = baked; EditorUtility.SetDirty(mf); }
                    done++;
                    log.AppendLine("   " + src.name + " : " + srcTri + " → " + newTri +
                                   " (" + (100f * newTri / srcTri).ToString("F0") + "%)");
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            string msg = "메시 " + done + "종을 줄였습니다.\n\n삼각형 " + before + " → " + after +
                         " (" + (100f * after / Mathf.Max(1, before)).ToString("F0") + "%)";
            Debug.Log("[메시줄이기] " + msg.Replace("\n\n", " / ") + "\n" + log);
            if (_showDialog) EditorUtility.DisplayDialog("이문록", msg, "확인");
        }

        /// <summary>
        /// 이 메시는 몇 삼각형이면 충분한가. 큰 건물은 넉넉히, 작은 소품은 적게 —
        /// 화면에서 차지하는 크기에 견줘 정한다.
        /// </summary>
        private static int TargetTriangles(Mesh m)
        {
            float diag = m.bounds.size.magnitude;                  // m
            int src = m.triangles.Length / 3;

            // 작은 소품은 과감하게. 10cm 짜리가 10만 삼각형인 것은 어떤 각도에서도 보이지 않는
            // 낭비라, 몇 천이면 충분하고도 남는다.
            if (diag < 1.5f) return Mathf.Clamp(Mathf.RoundToInt(diag * 2000f), 500, 3000);

            // 지붕·담장처럼 큰 것은 조심스럽게. 기와 골이 텍스처가 아니라 진짜 굴곡이라
            // 너무 줄이면 지붕이 민짜 판이 된다. 넷에 하나 정도만 남긴다.
            return Mathf.Max(20000, Mathf.RoundToInt(src * 0.25f));
        }

        /// <summary>
        /// 다른 도구가 쓰라고 열어 둔 문. 씬을 건드리지 않고 줄인 메시만 구워 돌려준다.
        /// (먼거리 판을 만드는 <see cref="HeavyMeshLod"/> 가 쓴다)
        /// </summary>
        public static Mesh Bake(Mesh src, int targetTriangles) => BakeSimplified(src, targetTriangles);

        /// <summary>줄인 메시를 만들어 원본 옆 폴더에 저장한다. 이미 있으면 덮어쓴다.</summary>
        private static Mesh BakeSimplified(Mesh src, int targetTriangles)
        {
            string srcPath = AssetDatabase.GetAssetPath(src);
            if (string.IsNullOrEmpty(srcPath)) return null;
            string dir = System.IO.Path.GetDirectoryName(srcPath).Replace('\\', '/');
            string outDir = dir + "/" + OutFolder;
            if (!AssetDatabase.IsValidFolder(outDir)) AssetDatabase.CreateFolder(dir, OutFolder);

            // 칸 크기를 이분 탐색으로 찾는다. 목표보다 조금 적게 나오는 쪽을 고른다.
            float lo = m_MinCell(src), hi = src.bounds.size.magnitude * 0.25f;
            Mesh best = null;
            for (int it = 0; it < 9; it++)
            {
                float mid = Mathf.Sqrt(lo * hi);                    // 로그 중앙 — 칸 크기는 배수로 움직인다
                var cand = Cluster(src, mid);
                int tri = cand.triangles.Length / 3;
                if (tri > targetTriangles) { lo = mid; Object.DestroyImmediate(cand); }
                else
                {
                    if (best != null) Object.DestroyImmediate(best);
                    best = cand; hi = mid;
                }
                if (hi / lo < 1.15f) break;
            }
            if (best == null) best = Cluster(src, hi);

            // 이름만으로 파일을 짓지 않는다.
            //
            // glTF 로 들여온 모델은 메시 이름이 죄다 'mesh' 나 'Model_material0_0' 이다.
            // 이름만 쓰면 안석과 보료가, 벼루·등잔대·동제등잔이 같은 파일에 구워져
            // 나중 것이 앞엣것을 덮어쓴다. 실제로 그렇게 되어 보료 자리에 안석 모양이
            // 서 있었다 — 자리는 그대로인데 물건이 바뀌어 있으니 찾기가 고약했다.
            // 원본이 어느 파일에서 왔는지를 이름에 함께 적는다.
            string owner = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(src));
            string stem = string.IsNullOrEmpty(owner) || owner == src.name
                        ? Sanitize(src.name)
                        : Sanitize(owner) + "_" + Sanitize(src.name);
            best.name = stem + "_간소";
            string path = outDir + "/" + stem + "_간소.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // 같은 파일을 계속 쓴다 — 씬이 물고 있는 참조(GUID)가 안 끊긴다.
                existing.Clear();
                existing.indexFormat = best.indexFormat;
                existing.SetVertices(new List<Vector3>(best.vertices));
                existing.SetNormals(new List<Vector3>(best.normals));
                existing.SetUVs(0, new List<Vector2>(best.uv));
                existing.subMeshCount = best.subMeshCount;
                for (int s = 0; s < best.subMeshCount; s++) existing.SetTriangles(best.GetTriangles(s), s);
                existing.RecalculateBounds();
                existing.RecalculateTangents();
                Object.DestroyImmediate(best);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(best, path);
            return best;
        }

        /// <summary>칸 크기 하한 — 원본 정점 간격보다 잘게 잡아봐야 아무것도 안 줄어든다.</summary>
        private static float m_MinCell(Mesh src)
            => Mathf.Max(0.0005f, src.bounds.size.magnitude / 2000f);

        /// <summary>
        /// 격자 뭉치기 한 판. cell 크기 칸마다 정점을 하나로 합치고 삼각형을 다시 엮는다.
        /// </summary>
        private static Mesh Cluster(Mesh src, float cell)
        {
            var verts = src.vertices;
            var norms = src.normals;
            var uvs = src.uv;
            bool hasN = norms != null && norms.Length == verts.Length;
            bool hasUV = uvs != null && uvs.Length == verts.Length;

            var map = new Dictionary<long, int>(verts.Length / 4 + 16);
            var remap = new int[verts.Length];
            var outPos = new List<Vector3>();
            var outNrm = new List<Vector3>();
            var outUv = new List<Vector2>();
            var count = new List<int>();

            Vector3 min = src.bounds.min;
            float inv = 1f / Mathf.Max(0.00001f, cell);

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = verts[i] - min;
                long cx = (long)Mathf.Floor(p.x * inv);
                long cy = (long)Mathf.Floor(p.y * inv);
                long cz = (long)Mathf.Floor(p.z * inv);
                // UV 이음새를 지킨다 — 자리가 같아도 UV가 멀면 다른 정점으로 남긴다.
                long ux = hasUV ? (long)Mathf.Floor(uvs[i].x * 16f) : 0;
                long uy = hasUV ? (long)Mathf.Floor(uvs[i].y * 16f) : 0;
                long key = ((cx & 0xFFFFF) << 44) ^ ((cy & 0xFFFFF) << 24) ^ ((cz & 0xFFFFF) << 4)
                         ^ ((ux & 0x3F) << 58) ^ (uy & 0x3F);

                int idx;
                if (map.TryGetValue(key, out idx))
                {
                    outPos[idx] += verts[i];
                    if (hasN) outNrm[idx] += norms[i];
                    if (hasUV) outUv[idx] += uvs[i];
                    count[idx]++;
                }
                else
                {
                    idx = outPos.Count;
                    map[key] = idx;
                    outPos.Add(verts[i]);
                    outNrm.Add(hasN ? norms[i] : Vector3.up);
                    outUv.Add(hasUV ? uvs[i] : Vector2.zero);
                    count.Add(1);
                }
                remap[i] = idx;
            }

            for (int i = 0; i < outPos.Count; i++)
            {
                float c = count[i];
                outPos[i] = outPos[i] / c;
                outUv[i] = outUv[i] / c;
                outNrm[i] = outNrm[i].sqrMagnitude > 0.000001f ? outNrm[i].normalized : Vector3.up;
            }

            var mesh = new Mesh();
            mesh.indexFormat = outPos.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(outPos);
            mesh.SetNormals(outNrm);
            mesh.SetUVs(0, outUv);
            mesh.subMeshCount = src.subMeshCount;

            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tri = src.GetTriangles(s);
                var kept = new List<int>(tri.Length / 2);
                for (int t = 0; t + 2 < tri.Length; t += 3)
                {
                    int a = remap[tri[t]], b = remap[tri[t + 1]], c = remap[tri[t + 2]];
                    if (a == b || b == c || a == c) continue;       // 납작해진 삼각형은 버린다
                    kept.Add(a); kept.Add(b); kept.Add(c);
                }
                mesh.SetTriangles(kept, s, false);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c > 127 ? c : '_');
            return sb.ToString();
        }
    }
}
