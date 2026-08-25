using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>관아에서 새는 데를 메운다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑭ 새는 데 메우기]
    ///
    /// 두 가지가 새고 있었다.
    ///
    /// <b>㉠ 담이 막지를 못한다.</b> 담 조각마다 MeshCollider 가 하나씩 붙어 있어
    /// 겉보기로는 다 갖춘 것 같은데, <b>그 콜라이더에 메시가 안 들어 있다</b>.
    /// 담 조각은 LOD 묶음이라 눈에 보이는 그물은 LOD0~3 <b>자식</b>에 있고,
    /// 콜라이더는 그물이 없는 <b>부모</b>에 붙어 있다. 그래서 크기가 0 인 채로
    /// 아무것도 막지 않는다 — 재어 보면 min 과 max 가 같은 한 점이다.
    ///
    /// 눈으로는 안 보인다. 담은 멀쩡히 서 있고, 위에서 내리쬔 광선은 담 <b>윗면</b>을
    /// 맞으니(그건 다른 콜라이더다) 걷기 검사도 통과한다. 옆에서 쏴 봐야 드러난다 —
    /// 북쪽·동쪽·서쪽 어디로 걸어도 담을 그냥 통과해 관아 밖으로 나간다.
    ///
    /// 담은 곧은 널이라 <b>상자로 싸는 것이 참값</b>이다(4.26 × 0.86 × 2.09).
    /// 메시콜라이더를 되살리는 것보다 값도 싸고 어긋날 데도 없다.
    ///
    /// <b>㉡ 동헌 합각(박공)벽이 통째로 없다.</b> 대청 안에서 옆쪽을 올려다보면
    /// 문 위부터 지붕까지가 <b>뻥 뚫려</b> 하늘과 담 너머가 그대로 보인다.
    /// 이 집은 용마루가 z 방향으로 뻗어 있어 ±z 쪽이 합각이 되는데, 그 삼각형이
    /// 비어 있다. 서까래만 걸려 있고 벽이 없다.
    ///
    /// <b>지붕 선을 재서 그 모양대로 세운다.</b> 삼각형 꼭짓점 좌표를 손으로 적어
    /// 넣으면 지붕을 손볼 때마다 어긋나므로, 합각면에서 위로 광선을 쏴 <b>지금 지붕이
    /// 어디 있는지</b>를 재고 그 선 밑에 딱 맞춰 널을 세운다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class GwanaSeal
    {
        private const string WallMat = "Assets/_Project/_Common/Sets/Gwana/Donheon/Materials/MI_R_BrickConcrete1.mat";
        private const string PlasterName = "M_동헌_합각";

        /// <summary>합각면이 있는 z 자리(옆벽·옆문이 서 있는 면).</summary>
        private static readonly float[] GablePlanes = { -4.52f, 4.52f };

        /// <summary>합각을 채울 x 범위 — 대청의 옆벽이 뻗은 만큼.</summary>
        private const float GableFromX = 11.35f, GableToX = 17.15f;

        /// <summary>널 아랫변. 문 위(≈3.7)보다 조금 낮게 잡아 틈이 안 생기게 겹친다.</summary>
        private const float GableBottom = 3.40f;

        /// <summary>지붕 겉면에서 이만큼 내려온 자리를 윗변으로 삼는다(기와 두께).</summary>
        private const float UnderRoof = 0.30f;

        private const float GableThick = 0.16f;

        [MenuItem("이문록/관아/⑭ 새는 데 메우기")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 새는 데를 메운다\n");
            Physics.SyncTransforms();

            TwoSided(log);
            Walls(scene, log);
            Gables(scene, log);
            SeogoBand(scene, log);

            Physics.SyncTransforms();
            log.AppendLine(Survey());

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// <b>지붕이 한 면짜리라 안에서 보면 뚫려 보인다.</b>
        ///
        /// 이게 제일 찾기 어려웠다. 지붕은 <b>구멍 난 데가 없다</b> — 광선을 쏴 보면
        /// 삼각형 4만 개가 멀쩡히 덮고 있다. 그런데 그 재질이 <c>_Cull = Back</c> 이라
        /// <b>바깥에서만 그려진다</b>. 안에서 올려다보면 기와의 뒷면이라 그리지 않고
        /// 지나가 버려서, 서가 너머로 하늘이 지나가는 띠가 생긴다.
        ///
        /// 그래서 여태 재는 족족 헛다리를 짚었다 — 벽을 세워도 그 자리가 아니고,
        /// 콜라이더를 붙여 쏘면 멀쩡히 맞는다. 눈에만 보이고 광선에는 안 잡히는 탈이다.
        ///
        /// 양면으로 돌리면 그만이다. 겹쳐 그리는 값이 조금 더 들지만, 지붕은 하늘을
        /// 가리는 물건이라 <b>어느 쪽에서 보든 있어야 한다</b>.
        /// </summary>
        private static void TwoSided(System.Text.StringBuilder log)
        {
            string[] names = { "MI_R_Roof1", "MI_R_Buyeon_2.001", "M_서고_판벽_긴것" };
            int n = 0;
            foreach (var name in names)
                foreach (var guid in AssetDatabase.FindAssets(name + " t:Material"))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (m == null || m.name != name || !m.HasProperty("_Cull")) continue;
                    if (Mathf.Approximately(m.GetFloat("_Cull"), 0f)) continue;
                    m.SetFloat("_Cull", 0f);       // 0 = 양면
                    m.doubleSidedGI = true;
                    EditorUtility.SetDirty(m);
                    n++;
                }
            if (n > 0) { AssetDatabase.SaveAssets(); log.AppendLine("  · 지붕 재질 " + n + "개를 양면으로 돌렸다 — 안에서 올려다보면 뒷면이라 하늘이 비쳤다"); }
            else log.AppendLine("  · 지붕 재질은 이미 양면이다");
        }

        // ── ㉠ 담 ──

        /// <summary>
        /// 빈 껍데기 콜라이더를 <b>상자</b>로 갈아 끼운다.
        ///
        /// 눈에 보이는 그물은 LOD0 자식에 있으므로 그 몸피를 재어 상자를 만든다.
        /// LOD 자식마다 붙이지 않는다 — 같은 담이 네 겹이라 네 벌이 된다.
        /// </summary>
        private static void Walls(Scene scene, System.Text.StringBuilder log)
        {
            int stripped = 0, made = 0, redone = 0;
            foreach (var lod in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (lod.gameObject.scene != scene) continue;

                // 눈에 보이는 몸피는 가장 촘촘한 단계(LOD0)로 잰다
                Renderer best = null;
                foreach (var r in lod.GetComponentsInChildren<Renderer>(true))
                    if (best == null || r.name.EndsWith("LOD0")) { best = r; if (r.name.EndsWith("LOD0")) break; }
                var mf = best != null ? best.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null) continue;
                if (best.bounds.size.y < 0.4f) continue;            // 바닥에 깔린 것은 담이 아니다

                var go = lod.gameObject;

                var mc = go.GetComponent<MeshCollider>();
                if (mc != null && mc.sharedMesh == null) { Object.DestroyImmediate(mc); stripped++; }

                var had = go.GetComponent<BoxCollider>();
                if (had != null) { Object.DestroyImmediate(had); redone++; }   // 앞서 잘못 잰 것
                if (go.GetComponent<Collider>() != null) continue;             // 남의 콜라이더는 그대로

                // <b>돌아앉은 담을 상자로 싸려면 회전을 넣어야 한다.</b>
                //
                // 세계 몸피를 lossyScale 로만 나누면 <b>돌아간 만큼이 빠진다</b> — 담이
                // 90° 누워 있으면 길이와 두께가 뒤바뀌어, 4.2m 짜리 담에 0.8m 짜리
                // 상자가 붙는다. 그러면 담마다 3.4m 씩 벌어져 그 사이로 걸어 나간다.
                // 실제로 그렇게 되어 북쪽 담을 그냥 통과했다.
                //
                // 그물의 제 몸피를 여덟 귀퉁이로 펴서 이 오브젝트 자리로 옮겨 잰다.
                Bounds lb = mf.sharedMesh.bounds;
                Vector3 lo = new Vector3(9e9f, 9e9f, 9e9f), hi = new Vector3(-9e9f, -9e9f, -9e9f);
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3(
                        (c & 1) == 0 ? lb.min.x : lb.max.x,
                        (c & 2) == 0 ? lb.min.y : lb.max.y,
                        (c & 4) == 0 ? lb.min.z : lb.max.z);
                    var here = go.transform.InverseTransformPoint(best.transform.TransformPoint(corner));
                    lo = Vector3.Min(lo, here); hi = Vector3.Max(hi, here);
                }

                var bc = Undo.AddComponent<BoxCollider>(go);
                bc.center = (lo + hi) * 0.5f;
                bc.size = hi - lo;
                made++;
            }
            log.AppendLine("  · 담: 빈 껍데기 " + stripped + "개를 걷고, 잘못 잰 상자 " + redone + "개를 다시 재고, 상자 " + made + "개를 세웠다");
        }

        // ── ㉡ 합각 ──

        private static void Gables(Scene scene, System.Text.StringBuilder log)
        {
            var mat = EnsureMat(log);
            foreach (var z in GablePlanes)
            {
                string name = "동헌_합각_" + (z < 0 ? "남" : "북");
                var had = Find(scene, name);
                if (had != null) Object.DestroyImmediate(had);      // 다시 재서 새로 세운다

                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.position = new Vector3(0f, 0f, z);
                Undo.RegisterCreatedObjectUndo(go, "합각 세우기");

                var mesh = Build(z, log);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                // 사람이 부딪칠 데는 아니지만(문 위쪽이다) 광선이 지나가면 안 된다
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }
        }

        /// <summary>
        /// 합각 널 하나를 짓는다.
        ///
        /// x 를 잘게 끊어 가며 <b>그 자리의 지붕 높이를 재고</b>, 아랫변에서 그 높이까지
        /// 널을 채운다. 지붕이 어떻게 생겼든 따라간다 — 숫자를 손으로 적지 않는 까닭이다.
        /// </summary>
        private static Mesh Build(float z, System.Text.StringBuilder log)
        {
            const float step = 0.20f;
            var xs = new List<float>();
            var tops = new List<float>();
            for (float x = GableFromX; x <= GableToX + 0.001f; x += step)
            {
                RaycastHit h;
                float top = GableBottom + 0.2f;
                if (Physics.Raycast(new Vector3(x, 12f, z), Vector3.down, out h, 12f, ~0, QueryTriggerInteraction.Ignore))
                    top = h.point.y - UnderRoof;
                xs.Add(x);
                tops.Add(Mathf.Max(top, GableBottom + 0.05f));
            }
            log.AppendLine("  · 합각 z=" + z.ToString("F2") + " — x " + GableFromX.ToString("F2") + "~" + GableToX.ToString("F2")
                         + " 를 " + xs.Count + "칸으로 재어 세웠다 (가장 높은 데 " + Mathf.Max(tops.ToArray()).ToString("F2") + ")");

            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            float half = GableThick * 0.5f;
            for (int i = 0; i < xs.Count - 1; i++)
            {
                float x0 = xs[i], x1 = xs[i + 1], y0 = tops[i], y1 = tops[i + 1];
                // 두 면(안·밖)과 윗면
                AddQuad(v, t, uv,
                    new Vector3(x0, GableBottom, -half), new Vector3(x1, GableBottom, -half),
                    new Vector3(x1, y1, -half), new Vector3(x0, y0, -half));
                AddQuad(v, t, uv,
                    new Vector3(x1, GableBottom, half), new Vector3(x0, GableBottom, half),
                    new Vector3(x0, y0, half), new Vector3(x1, y1, half));
                AddQuad(v, t, uv,
                    new Vector3(x0, y0, -half), new Vector3(x1, y1, -half),
                    new Vector3(x1, y1, half), new Vector3(x0, y0, half));
            }
            var m = new Mesh { name = "동헌_합각" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        private static void AddQuad(List<Vector3> v, List<int> t, List<Vector2> uv,
                                    Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            // 회를 바른 벽이라 무늬가 굵으면 안 된다 — 1m 마다 한 번
            uv.Add(new Vector2(a.x, a.y)); uv.Add(new Vector2(b.x, b.y));
            uv.Add(new Vector2(c.x, c.y)); uv.Add(new Vector2(d.x, d.y));
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        private static Material EnsureMat(System.Text.StringBuilder log)
        {
            string path = "Assets/_Project/_Common/Sets/Gwana/Donheon/Materials/" + PlasterName + ".mat";
            var had = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (had != null) return had;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = PlasterName };
            // 합각은 회를 발라 희끄무레하다. 나무 기둥과 갈려야 벽으로 읽힌다.
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.80f, 0.78f, 0.73f, 1f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            log.AppendLine("  · 합각에 바를 회를 만들었다 (" + path + ")");
            return m;
        }

        /// <summary>
        /// <b>문서고 앞면, 문 위에 뜬 띠를 메운다.</b>
        ///
        /// 문서고는 옆벽·뒷벽이 <b>3.30</b> 까지 올라가 지붕 밑동과 맞물리는데,
        /// 앞면만 문 위 벽(문위벽)이 <b>3.05</b> 에서 끊긴다. 그 25cm 가 안에서 보면
        /// 서가 너머로 <b>하늘이 지나가는 띠</b>가 된다 — 지붕이 얹혀 있는데도 그 아래로
        /// 바깥이 비친다.
        ///
        /// 문 위를 가로지르는 인방(引枋) 하나를 얹는 셈이라 생김새로도 맞다.
        ///
        /// <b>다만 문서고에는 이미 인방이 있다</b>(문서고/판문/인방, 3.06~3.31).
        /// 벽이 3.05 에서 끊기니 그 사이 <b>1cm</b> 가 여태 하늘이었다. 그래서 이 널은
        /// 그 인방을 없애는 것이 아니라 <b>속에 끼워</b> 1cm 만 덮는 것이다.
        /// 두께를 인방보다 <b>얇게</b>(0.12 &lt; 0.16) 잡는 까닭이 여기 있다 — 앞뒤 면이
        /// 겹치면 둘이 서로 어른거린다. 재질도 인방 것을 가져다 쓴다.
        /// </summary>
        private static void SeogoBand(Scene scene, System.Text.StringBuilder log)
        {
            const string Name = "문서고_문위_인방";
            var go = Find(scene, Name);
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = Name;
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "문서고 인방");
                log.AppendLine("  · 문서고 문 위에 인방을 얹었다 — 3.05 에서 끊겨 하늘이 비쳤다");
            }
            // 앞면은 z=-5.30, 벽이 3.05 에서 끊기고 지붕 밑동이 3.30 이다. 넉넉히 겹친다.
            // 두께는 이미 선 인방(0.16)보다 얇게 — 앞뒤 면이 겹치면 어른거린다.
            Band(go, new Vector3(-2.0f, 3.20f, -5.30f), new Vector3(12.10f, 0.34f, 0.12f));
            var beam = Beam(scene);
            var gomr = go.GetComponent<MeshRenderer>();
            if (beam != null && gomr != null) gomr.sharedMaterial = beam;

            // <b>옆·뒤도 같은 병이다.</b> 벽은 3.30 에서 반듯하게 끊기는데 지붕은
            // 처마에서 용마루로 <b>비스듬히</b> 올라간다. 그래서 벽 윗선과 지붕 밑동
            // 사이가 벌어져, 서가 너머로 담과 하늘이 지나가는 띠가 생긴다.
            // 벽을 지붕 속으로 한 뼘 밀어 넣어 그 틈을 덮는다 — 지붕이 가리므로
            // 밖에서는 안 보인다.
            var plaster = EnsureMat(log);
            Skirt(scene, "문서고_윗벽_서", new Vector3(-8.00f, 3.60f, -8.30f), new Vector3(0.22f, 0.72f, 6.10f), plaster, log);
            Skirt(scene, "문서고_윗벽_동", new Vector3( 4.00f, 3.60f, -8.30f), new Vector3(0.22f, 0.72f, 6.10f), plaster, log);
            Skirt(scene, "문서고_윗벽_뒤", new Vector3(-2.00f, 3.60f, -11.30f), new Vector3(12.10f, 0.72f, 0.22f), plaster, log);
        }

        /// <summary>이미 선 인방의 재질. 새로 얹는 널을 나무빛에 맞춘다.</summary>
        private static Material Beam(Scene scene)
        {
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r.name == "인방" && r.gameObject.scene == scene) return r.sharedMaterial;
            return null;
        }

        /// <summary>벽 위에 덧대는 널 하나. 손 닿을 데가 아니라 콜라이더는 안 붙인다.</summary>
        private static void Skirt(Scene scene, string name, Vector3 pos, Vector3 size,
                                  Material mat, System.Text.StringBuilder log)
        {
            var go = Find(scene, name);
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "문서고 윗벽");
                log.AppendLine("  · " + name + " 을 덧댔다");
            }
            Band(go, pos, size);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
        }

        private static void Band(GameObject go, Vector3 pos, Vector3 size)
        {
            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            var wood = AssetDatabase.LoadAssetAtPath<Material>(WallMat);
            if (mr != null && mr.sharedMaterial == null && wood != null) mr.sharedMaterial = wood;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);   // 손 닿을 데가 아니다 — 값만 든다
        }

        // ── 다시 재기 ──

        /// <summary>메우고 나서 정말 안 새는지 그 자리에서 다시 쏴 본다.</summary>
        private static string Survey()
        {
            var sb = new System.Text.StringBuilder("  ── 메우고 다시 재기 ──\n");
            // <b>안에서 쏘는 광선은 지붕 뒷면을 그냥 지나간다.</b>
            //
            // 유니티는 기본으로 뒷면을 안 맞힌다(queriesHitBackfaces=false). 그래서 방 안에서
            // 위를 쏘면 지붕 <b>밑면</b>이 뒷면이라 광선이 통과해 버리고, 멀쩡한 지붕을 두고
            // "서른여섯 줄기가 샌다"고 나온다. 실제로 그렇게 나와서 없는 구멍을 찾아
            // 용마루를 재고 있었다. 재는 동안만 뒷면도 세게 한다.
            bool wasBack = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;

            var box = new Bounds(new Vector3(13.4f, 3.9f, 0f), new Vector3(11.4f, 8.0f, 18.7f));
            var eyes = new[]{
                new Vector3(12.6f,3.81f,0f), new Vector3(14.0f,3.81f,0f), new Vector3(15.8f,3.81f,0f),
                new Vector3(14.0f,3.81f,-3.2f), new Vector3(14.0f,3.81f,3.2f), new Vector3(15.3f,3.75f,0f),
            };
            int leak = 0;
            foreach (var eye in eyes)
                for (int yaw = 0; yaw < 360; yaw += 3)
                    for (int p = -15; p <= 75; p += 3)
                    {
                        var d = Quaternion.Euler(-p, yaw, 0f) * Vector3.forward;
                        if (d.x < -0.25f) continue;                 // 앞은 원래 트여 있다
                        float t = 0f;
                        for (float s = 0.15f; s < 25f; s += 0.15f) if (!box.Contains(eye + d * s)) { t = s; break; }
                        if (t <= 0.2f) continue;
                        if (!Physics.Raycast(eye, d, t - 0.05f, ~0, QueryTriggerInteraction.Ignore)) leak++;
                    }
            Physics.queriesHitBackfaces = wasBack;
            sb.AppendLine("    대청에서 껍데기를 뚫고 나가는 광선 = " + leak + "줄기" + (leak == 0 ? "   (다 막혔다)" : "   ※ 아직 샌다"));

            // <b>담은 네 군데만 찔러 보면 안 된다.</b> 조각마다 벌어질 수 있으므로
            // 둘레를 1m 걸음으로 다 훑는다. 대문 어귀(서쪽 z -6.4~6.4)는 원래 트인 데다.
            int gap = 0;
            var holes = new List<string>();
            for (float a = -14f; a <= 20f; a += 1f)
            {
                if (!Blocked(new Vector3(a, 1.6f, -11.5f), Vector3.back)) { gap++; holes.Add("남 x=" + a.ToString("F0")); }
                if (!Blocked(new Vector3(a, 1.6f, 11.5f), Vector3.forward)) { gap++; holes.Add("북 x=" + a.ToString("F0")); }
            }
            for (float a = -12f; a <= 12f; a += 1f)
            {
                if (!Blocked(new Vector3(19.5f, 1.6f, a), Vector3.right)) { gap++; holes.Add("동 z=" + a.ToString("F0")); }
                if (Mathf.Abs(a) > 6.5f && !Blocked(new Vector3(-14f, 1.6f, a), Vector3.left)) { gap++; holes.Add("서 z=" + a.ToString("F0")); }
            }
            sb.AppendLine("    담 둘레를 1m 걸음으로 훑음 — 벌어진 데 " + gap + "곳"
                        + (gap == 0 ? "   (다 막혔다)" : "   → " + string.Join(", ", holes.ToArray())));
            return sb.ToString();
        }

        /// <summary>이 자리에서 저쪽으로 걸어가면 막히나.</summary>
        private static bool Blocked(Vector3 from, Vector3 dir)
        {
            return Physics.Raycast(from, dir, 4.0f, ~0, QueryTriggerInteraction.Ignore);
        }

        private static GameObject Find(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
