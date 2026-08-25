using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>돋보기 유리알을 잰다.</b> 메뉴: [이문록 ▸ 도구 ▸ 돋보기 유리알 재기]
    ///
    /// 렌즈 그림을 띄울 <b>동그라미</b>가 소품의 테와 안 맞는 것이 오래 걸렸다.
    /// 까닭은 코드가 아니라 <b>에셋</b>에 있다 — FBX 를 뜯어 보면 덩이가 둘뿐이고
    /// (놋쇠 3420 삼각, 술 1660 삼각) <b>유리알이라는 조각이 따로 없다</b>. 그러니
    /// <see cref="MagnifierLens"/> 는 유리가 어디에 얼마만 한지를 어림잡을 수밖에 없었고,
    /// 실제로 그렇게 돼 있다:
    ///
    /// <code>_glassRadius = Mathf.Min(bounds.size.x, bounds.size.y) * 0.35f;</code>
    ///
    /// 몸피는 <b>테와 자루를 함께</b> 잰 값이라 자루가 조금만 길어도 동그라미가 어긋난다.
    /// 그래서 손으로 맞추는 손잡이(_glassInset, _glassScaleTweak)가 붙어 있고,
    /// 그 손잡이를 돌려 맞추는 일이 어려웠던 것이다.
    ///
    /// <b>어림잡지 말고 잰다.</b> 유리알 자리는 <b>테가 둘러싼 안쪽</b>이다.
    ///
    ///   ㉠ 놋쇠 덩이를 유리면에 눌러 펴고 모서리를 그어 칸을 채운다.
    ///   ㉡ 테두리 밖에서 <b>물을 붓는다</b>. 젖지 않고 남은 데가 둘러싸인 안쪽이다.
    ///   ㉢ 그 안에서 놋쇠까지 가장 먼 자리를 고른다. 거기가 한가운데, 그 거리가 반지름.
    ///
    /// <b>세 번 헛디뎠다.</b> 그 자취를 남겨 둔다 — 다음에 다른 소품을 재게 될 때
    /// 같은 데를 또 밟지 않도록.
    ///
    ///   · <b>모서리를 다 그었다.</b> 유리알을 덮은 성긴 삼각형이 그 안을 잔금으로
    ///     가로질러, 가장 큰 빈 원이 잔금 사이 좁쌀만 한 데로 나왔다(0.008m — 자루보다
    ///     작다).
    ///   · <b>낯이 꺾이는 모서리만 그었다.</b> 이 그물은 통째로 각져 있어(Meshy 로 뽑은
    ///     것이라 낯을 안 부드럽게 했다) 모서리가 죄 꺾인 것으로 잡힌다. 걸러진 것이
    ///     하나도 없었다.
    ///   · <b>긴 모서리를 손으로 정한 길이에서 잘랐다.</b> 가운데 값의 여덟 배로 잘랐더니
    ///     가로지르는 선 98 줄만 빠지고 유리알을 성기게 덮은 그물은 그대로 남았다.
    ///     <b>손으로 정한 값은 늘 이렇게 어긋난다.</b>
    ///
    /// 그래서 자를 길이도 <b>재게</b> 했다. 놋쇠 꼭짓점 7759 개 가운데 6122 개(79%)가
    /// 반지름 한 줄에 몰려 있으니 <b>테는 잘고 유리알은 굵다</b>. 자를 길이를 조금씩
    /// 낮추면서 매번 물을 부어 본다 — 굵은 것부터 빠지므로 원이 점점 커지다가,
    /// <b>테까지 끊기는 순간 물이 새어</b> 둘러싸인 데가 사라진다. 그 직전이 참값이다.
    /// 곧 <b>테가 물을 가둘 수 있는 한계까지</b> 낮춘 것이다.
    ///
    /// 한 번 재서 값으로 굳혀 두므로(<c>_measureProp = false</c>) 실행 중에는 값이 안 든다.
    /// 잰 원을 그림으로도 뽑는다 — 눈으로 확인하고 넘어가시라고.
    /// </summary>
    public static class MagnifierFit
    {
        /// <summary>칸 수. 크면 정밀하나 물 붓기와 거리 재기가 그만큼 오래 걸린다.</summary>
        private const int N = 384;

        /// <summary>자를 길이를 가운데 값의 이 배들로 훑는다 — 굵은 것부터 뺀다.</summary>
        private static readonly float[] Cuts =
            { 40f, 20f, 12f, 8f, 6f, 4.5f, 3.5f, 2.8f, 2.2f, 1.8f, 1.5f, 1.3f, 1.1f };

        [MenuItem("이문록/도구/돋보기 유리알 재기")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            var cam = MainCam(scene);
            if (cam == null) { Debug.LogWarning("[돋보기] 씬에서 카메라를 못 찾았습니다."); return; }

            HeldToolModel held = null;
            foreach (var h in cam.GetComponentsInChildren<HeldToolModel>(true))
                if (h.ToolId == "magnify") held = h;
            if (held == null)
            {
                Debug.LogWarning("[돋보기] 카메라 밑에 돋보기 소품이 없습니다. " +
                                 "[이문록 ▸ 관아 ▸ ⑰ 도구를 관아로 옮긴다] 를 먼저 누르십시오.");
                return;
            }

            // <b>켜진 것을 먼저</b> 집는다. 소품을 갈아 끼우며 옛 모델을 꺼서 남겨 두는
            // 일이 흔해서, 그냥 첫째를 집으면 안 쓰는 옛것을 재게 된다 — 실제로 한 번
            // 그렇게 재고 "돋보기_모델(옛것)" 을 쟀다고 찍혔다.
            Renderer rend = null, spare = null;
            foreach (var r in held.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (r.gameObject.activeInHierarchy) { rend = r; break; }
                if (spare == null) spare = r;
            }
            if (rend == null) rend = spare;
            if (rend == null) { Debug.LogWarning("[돋보기] 소품에 그물이 없습니다."); return; }

            var mesh = MeshOf(rend);
            if (mesh == null) { Debug.LogWarning("[돋보기] 그물을 못 꺼냈습니다."); return; }

            // ── 놋쇠 덩이를 고른다. 술은 테를 이루지 않는다 ──
            int brass = 0, most = -1;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                int n = mesh.GetTriangles(i).Length;
                if (n > most) { most = n; brass = i; }
            }
            var tris = mesh.GetTriangles(brass);
            var verts = mesh.vertices;

            // ── 유리면: 놋쇠가 가장 얇은 축이 법선이다 ──
            var lo = new Vector3(9e9f, 9e9f, 9e9f);
            var hi = new Vector3(-9e9f, -9e9f, -9e9f);
            for (int i = 0; i < tris.Length; i++) { var v = verts[tris[i]]; lo = Vector3.Min(lo, v); hi = Vector3.Max(hi, v); }
            var size = hi - lo;
            int flat = (size.x <= size.y && size.x <= size.z) ? 0 : (size.y <= size.z ? 1 : 2);
            int u = (flat + 1) % 3, v2 = (flat + 2) % 3;

            float span = Mathf.Max(hi[u] - lo[u], hi[v2] - lo[v2]);
            float pad = span * 0.03f;
            float uMin = lo[u] - pad, vMin = lo[v2] - pad;
            float cell = (span + pad * 2f) / N;

            float median = Median(tris, verts, u, v2);

            // ── 자를 길이를 낮춰 가며, 테가 물을 가둘 수 있는 한계를 찾는다 ──
            bool[] keepSolid = null, keepWet = null;
            int keepIdx = -1; float keepR = -1f, keepMult = 0f;
            var trace = new System.Text.StringBuilder();

            foreach (float mult in Cuts)
            {
                var solid = new bool[N * N];
                int cutOff = Outline(tris, verts, solid, u, v2, uMin, vMin, cell, median * mult);
                var wet = Flood(solid);
                int idx; float r;
                Largest(solid, wet, out idx, out r);

                trace.AppendLine("      ×" + mult.ToString("F1").PadLeft(5)
                               + "  뺀 모서리 " + cutOff.ToString().PadLeft(5)
                               + "  반지름 " + (idx < 0 ? "— (물이 샜다)" : (r * cell).ToString("F5")));

                if (idx < 0) break;                       // 테가 끊겼다. 더 낮추면 안 된다
                if (r < keepR * 0.8f) break;              // 원이 도로 작아졌다 — 지난 것이 참값
                if (r > keepR) { keepR = r; keepIdx = idx; keepSolid = solid; keepWet = wet; keepMult = mult; }
            }

            if (keepIdx < 0)
            {
                Debug.LogWarning("[돋보기] 테에 둘러싸인 데를 못 찾았습니다 — 테가 끊겨 있거나 " +
                                 "이 소품에는 유리알 자리가 없습니다.\n" + trace);
                return;
            }

            float cx = uMin + (keepIdx % N + 0.5f) * cell;
            float cy = vMin + (keepIdx / N + 0.5f) * cell;
            float rLocal = keepR * cell;

            var center = Vector3.zero;
            center[u] = cx; center[v2] = cy; center[flat] = (lo[flat] + hi[flat]) * 0.5f;

            // 세계 반지름 = 로컬 반지름 × 그 축의 배율. MagnifierLens 가 쓰는 것과 같은 셈이다.
            float rWorld = rLocal * Mathf.Abs(rend.transform.lossyScale[u]);

            // ── 값을 굳힌다 ──
            var lens = Lens(cam);
            var so = new SerializedObject(lens);
            so.FindProperty("_measureProp").boolValue = false;      // 잰 값을 실행 중에 덮지 않게
            so.FindProperty("_glassCenterManual").vector3Value = center;
            so.FindProperty("_glassRadius").floatValue = rWorld;
            so.FindProperty("_glassNudge").vector3Value = Vector3.zero;
            so.FindProperty("_glassScaleTweak").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lens);
            EditorSceneManager.MarkSceneDirty(scene);

            string shot = Picture(keepSolid, keepWet, keepIdx, keepR);

            Debug.Log("[돋보기] 유리알을 쟀다\n"
                + "  · 소품 " + rend.name + "  놋쇠 덩이 " + brass + " (삼각 " + (most / 3) + ")\n"
                + "  · 유리면 법선 = 로컬 " + "xyz"[flat] + " 축,  칸 " + N + "×" + N + " (한 칸 " + cell.ToString("F6") + ")\n"
                + "  · 자를 길이를 낮춰 가며 테가 물을 가두는 한계를 찾았다 (모서리 가운데 값 "
                + median.ToString("F6") + ")\n" + trace
                + "  · 고른 것: 가운데 값의 ×" + keepMult.ToString("F1") + "\n"
                + "  · 유리알 한가운데 " + center.ToString("F5") + "\n"
                + "  · 반지름 로컬 " + rLocal.ToString("F5") + "  →  세계 " + rWorld.ToString("F4") + "m"
                + "  (지름 " + (rWorld * 200f).ToString("F1") + "cm)\n"
                + "  · 손보정(_glassInset·_glassScaleTweak)은 이제 안 쓴다 — 잰 값으로 잠갔다\n"
                + "  · 재어 본 그림: " + shot, lens);
        }

        // ── 재는 손 ───────────────────────────────

        /// <summary>모서리 길이의 가운데 값. 테가 잘고 촘촘하므로 이 값이 곧 테의 눈금이다.</summary>
        private static float Median(int[] tris, Vector3[] verts, int u, int v)
        {
            var len = new List<float>(tris.Length);
            for (int t = 0; t + 2 < tris.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    var a = verts[tris[t + e]]; var b = verts[tris[t + (e + 1) % 3]];
                    float du = a[u] - b[u], dv = a[v] - b[v];
                    len.Add(Mathf.Sqrt(du * du + dv * dv));
                }
            if (len.Count == 0) return 0f;
            len.Sort();
            return len[len.Count / 2];
        }

        /// <summary>이 길이 안에 드는 모서리만 긋는다. 뺀 개수를 돌려준다.</summary>
        private static int Outline(int[] tris, Vector3[] verts, bool[] solid,
                                   int u, int v, float uMin, float vMin, float cell, float cut)
        {
            int skipped = 0;
            for (int t = 0; t + 2 < tris.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    var a = verts[tris[t + e]]; var b = verts[tris[t + (e + 1) % 3]];
                    float du = a[u] - b[u], dv = a[v] - b[v];
                    if (du * du + dv * dv > cut * cut) { skipped++; continue; }
                    Line(solid, a, b, u, v, uMin, vMin, cell);
                }
            return skipped;
        }

        /// <summary>테두리 밖에서 물을 부어 빈 칸을 적신다.</summary>
        private static bool[] Flood(bool[] solid)
        {
            var wet = new bool[solid.Length];
            var q = new Queue<int>();
            for (int i = 0; i < N; i++)
            {
                Pour(solid, wet, q, i, 0); Pour(solid, wet, q, i, N - 1);
                Pour(solid, wet, q, 0, i); Pour(solid, wet, q, N - 1, i);
            }
            while (q.Count > 0)
            {
                int p = q.Dequeue(); int x = p % N, y = p / N;
                Pour(solid, wet, q, x - 1, y); Pour(solid, wet, q, x + 1, y);
                Pour(solid, wet, q, x, y - 1); Pour(solid, wet, q, x, y + 1);
            }
            return wet;
        }

        /// <summary>젖지 않고 남은 데에서 놋쇠까지 가장 먼 자리.</summary>
        private static void Largest(bool[] solid, bool[] wet, out int idx, out float radius)
        {
            var dist = Chamfer(solid);
            idx = -1; radius = -1f;
            for (int i = 0; i < solid.Length; i++)
            {
                if (solid[i] || wet[i]) continue;
                if (dist[i] > radius) { radius = dist[i]; idx = i; }
            }
            // 한두 칸짜리는 잔금 사이에 낀 좁쌀이지 유리알이 아니다.
            if (radius < 2f) { idx = -1; radius = -1f; }
        }

        // ── 잔손 ─────────────────────────────────

        /// <summary>뼈가 든 소품은 지금 자세로 구워 온다(MagnifierLens 와 같은 셈).</summary>
        private static Mesh MeshOf(Renderer r)
        {
            var sk = r as SkinnedMeshRenderer;
            if (sk != null && sk.sharedMesh != null)
            {
                var baked = new Mesh { name = "돋보기_잰것" };
                sk.BakeMesh(baked, true);
                return baked;
            }
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        private static void Line(bool[] solid, Vector3 a, Vector3 b, int u, int v, float uMin, float vMin, float cell)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((a[u] - uMin) / cell), 0, N - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((a[v] - vMin) / cell), 0, N - 1);
            int x1 = Mathf.Clamp(Mathf.FloorToInt((b[u] - uMin) / cell), 0, N - 1);
            int y1 = Mathf.Clamp(Mathf.FloorToInt((b[v] - vMin) / cell), 0, N - 1);
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            for (int guard = 0; guard < N * 4; guard++)
            {
                solid[y0 * N + x0] = true;
                if (x0 == x1 && y0 == y1) break;
                int e2 = err * 2;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private static void Pour(bool[] solid, bool[] wet, Queue<int> q, int x, int y)
        {
            if (x < 0 || y < 0 || x >= N || y >= N) return;
            int i = y * N + x;
            if (solid[i] || wet[i]) return;
            wet[i] = true;
            q.Enqueue(i);
        }

        /// <summary>칸마다 놋쇠까지의 거리(칸 단위). 두 번 훑는 어림셈이라 빠르고 넉넉히 정확하다.</summary>
        private static float[] Chamfer(bool[] solid)
        {
            const float A = 1f, B = 1.41421356f;
            var d = new float[solid.Length];
            for (int i = 0; i < d.Length; i++) d[i] = solid[i] ? 0f : 9e9f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x; float m = d[i];
                    if (x > 0) m = Mathf.Min(m, d[i - 1] + A);
                    if (y > 0) m = Mathf.Min(m, d[i - N] + A);
                    if (x > 0 && y > 0) m = Mathf.Min(m, d[i - N - 1] + B);
                    if (x < N - 1 && y > 0) m = Mathf.Min(m, d[i - N + 1] + B);
                    d[i] = m;
                }
            for (int y = N - 1; y >= 0; y--)
                for (int x = N - 1; x >= 0; x--)
                {
                    int i = y * N + x; float m = d[i];
                    if (x < N - 1) m = Mathf.Min(m, d[i + 1] + A);
                    if (y < N - 1) m = Mathf.Min(m, d[i + N] + A);
                    if (x < N - 1 && y < N - 1) m = Mathf.Min(m, d[i + N + 1] + B);
                    if (x > 0 && y < N - 1) m = Mathf.Min(m, d[i + N - 1] + B);
                    d[i] = m;
                }
            return d;
        }

        /// <summary>잰 것을 그림 한 장으로 뽑는다 — 눈으로 보고 넘어가시라고.</summary>
        private static string Picture(bool[] solid, bool[] wet, int bestI, float rCells)
        {
            var tex = new Texture2D(N, N, TextureFormat.RGB24, false);
            var px = new Color32[N * N];
            int cx = bestI % N, cy = bestI / N;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    Color32 c;
                    if (solid[i]) c = new Color32(210, 170, 90, 255);        // 놋쇠
                    else if (wet[i]) c = new Color32(28, 30, 36, 255);       // 바깥
                    else c = new Color32(60, 90, 130, 255);                  // 둘러싸인 안쪽
                    float dr = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (Mathf.Abs(dr - rCells) < 1.2f) c = new Color32(255, 90, 90, 255);   // 잰 원
                    px[i] = c;
                }
            tex.SetPixels32(px); tex.Apply();
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "이문록");
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "돋보기_유리알.png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        private static MagnifierLens Lens(Camera cam)
        {
            var lens = cam.GetComponentInChildren<MagnifierLens>(true);
            if (lens != null) return lens;
            var go = new GameObject("돋보기_렌즈");
            go.transform.SetParent(cam.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "돋보기 렌즈");
            return Undo.AddComponent<MagnifierLens>(go);
        }

        private static Camera MainCam(Scene scene)
        {
            Camera any = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Camera>(true))
                {
                    if (c.CompareTag("MainCamera")) return c;
                    if (any == null) any = c;
                }
            return any;
        }
    }
}
