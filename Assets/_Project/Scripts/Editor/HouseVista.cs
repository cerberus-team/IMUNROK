using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>1막의 먼 데를 채운다 — 달밤 하늘, 원경 산, 들길.</b>
    /// 메뉴: [이문록 ▸ 옹고집 ▸ 먼 데 채우기(하늘·산·길)]
    ///
    /// 나무를 두르고 나니 <b>나무 위가 비었다</b>. 담 너머로 우듬지가 보이고 그 위는
    /// 그냥 하늘이라, 집이 아직 아무 데도 아닌 데 서 있는 것처럼 보였다.
    ///
    /// 넷을 놓는다.
    ///
    /// <b>① 하늘은 달밤이다.</b> 1막은 한밤중이다 — 과객이 하룻밤 재워 달라 청하는
    /// 이야기고, 등불도 서고의 어둠도 다 그 밤을 쓰라고 있는 것이다. 낮하늘을 씌우면
    /// "이 밤중에 어인 일이시오" 하는 첫마디가 깨진다. <c>CoriolisNight4k</c> 에는
    /// <b>달이 떠 있어</b> 방향등을 그 쪽에서 비추면 달빛이 된다.
    ///
    /// <b>② 원경 산은 능선 셋을 겹친다.</b> 진짜 산을 세우지 않는다 — 어차피 담 밖으로
    /// 나갈 수 없으니 <b>실루엣</b>이면 족하다. 반지름이 다른 휘장 셋을 두르고 멀수록
    /// 옅게 칠하면, 그 옅어짐이 곧 거리가 된다(공기원근). 능선은 <b>주기가 다른 물결
    /// 셋을 겹쳐</b> 만든다 — 난수로 뽑으면 누를 때마다 산이 바뀐다.
    ///
    /// <b>③ 땅을 넓힌다.</b> 고택이 딛고 선 바닥 판은 113×138 이라 <b>56m 에서 끊긴다</b>.
    /// 산만 세우면 그 끊긴 자리와 산 사이로 하늘이 비쳐, 산이 허공에 뜬다.
    /// 반지름 320m 짜리 원반을 산보다 낮게 깔아 그 틈을 없앤다.
    ///
    /// <b>④ 길을 낸다.</b> 조선의 마을 그림에서 눈을 잡는 것은 집이 아니라 <b>구부러진
    /// 흙길</b>이다. 바깥 땅을 풀빛으로 덮고 그 위에 <b>원래의 흙빛</b>으로 길을 얹는다 —
    /// 길을 새로 칠하는 것이 아니라 <b>풀을 덮고 길만 남기는</b> 쪽이 손이 적고, 흙과
    /// 풀이 같은 집안 텍스처라 이물감도 없다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class HouseVista
    {
        private const string Shell = "Assets/_Project/Onggojip/Scenes/Onggojip.unity";
        private const string Yard  = "Assets/_Project/Onggojip/Scenes/Onggojip_마당.unity";
        private const string YardName = "Onggojip_마당";

        private const string SkyPath = "Assets/SkySeries Freebie/CoriolisNight4k.mat";
        private const string GrassBC = "Assets/Fristy stylize Modular Assets 2/Textures/Grass_DefuseFinal 2.png";
        private const string MatDir  = "Assets/_Project/Onggojip/Art/KimMyeonggwanHouse/Material";

        private const string RootName = "먼데";

        /// <summary>바깥 땅의 높이. 원반은 이보다 살짝 낮게 깐다.</summary>
        private const float Ground = -1.67f;

        [MenuItem("이문록/옹고집/먼 데 채우기(하늘·산·길)")]
        public static void Run()
        {
            var scene = SceneManager.GetSceneByName(YardName);
            if (!scene.isLoaded)
            {
                EditorSceneManager.OpenScene(Shell, OpenSceneMode.Single);
                scene = EditorSceneManager.OpenScene(Yard, OpenSceneMode.Additive);
            }

            var log = new System.Text.StringBuilder("[옹고집] 먼 데를 채운다\n");

            foreach (var r in scene.GetRootGameObjects())
                if (r.name == RootName) Undo.DestroyObjectImmediate(r);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "먼데");
            SceneManager.MoveGameObjectToScene(root, scene);

            Sky(log);
            Plate(root.transform, log);
            Ridges(root.transform, log);
            Paths(root.transform, log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ── ① 달밤 ────────────────────────────────

        private static void Sky(System.Text.StringBuilder log)
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null) { log.AppendLine("── 밤하늘을 못 찾았다"); return; }
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;

            // 달이 떠 있는 쪽에서 비춘다. 푸르고 여려야 달빛이지, 세면 그냥 흐린 낮이 된다.
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                Undo.RecordObject(l.transform, "달빛"); Undo.RecordObject(l, "달빛");
                l.transform.rotation = Quaternion.Euler(38f, 210f, 0f);
                l.color = new Color(0.72f, 0.80f, 1f);
                l.intensity = 0.55f;
                EditorUtility.SetDirty(l);
            }
            UnityEngine.DynamicGI.UpdateEnvironment();
            log.AppendLine("── 하늘 CoriolisNight4k · 달빛 38° 푸른빛 0.55");
        }

        // ── ③ 넓힌 땅 ─────────────────────────────

        private static void Plate(Transform parent, System.Text.StringBuilder log)
        {
            var go = Make(parent, "먼땅", Disc(320f, 96), GrassMat("M_들풀", new Color(0.42f, 0.50f, 0.34f), 90f));
            go.transform.position = new Vector3(-3f, Ground - 0.05f, 1f);
            log.AppendLine("── 먼땅 반지름 320m (바닥 판이 56m 에서 끊겨 산이 뜨는 것을 막는다)");

            // 원래의 바깥 바닥도 풀빛으로 덮는다. 그래야 그 위에 낸 흙길이 길로 보인다.
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.name != "Cube" || r.bounds.size.x < 100f) continue;
                Undo.RecordObject(r, "바깥 땅 풀빛");
                r.sharedMaterial = GrassMat("M_들풀_가까이", new Color(0.46f, 0.54f, 0.36f), 26f);
                EditorUtility.SetDirty(r);
                log.AppendLine("── 바깥 바닥을 풀빛으로 덮었다");
            }
        }

        // ── ② 원경 산 ─────────────────────────────

        // { 반지름, 밑높이, 웃높이 }. 멀수록 높고 옅다.
        //
        // 처음에 150m 에 30~56m 로 세웠더니 <b>산이 아니라 검은 담</b>이 됐다.
        // 가까울수록 하늘을 많이 가리는데, 그 높이면 나무 우듬지 위를 통째로 덮는다.
        // 멀찍이 물리고 낮춘다 — 원경은 <b>작게 보여야</b> 멀어 보인다.
        //
        // 빛깔도 너무 어두웠다. 달밤이라도 먼 산은 밤하늘보다 <b>조금 밝다</b> —
        // 사이의 공기가 달빛을 머금기 때문이다. 새까맣게 칠하면 구멍처럼 보인다.
        private static readonly float[][] Ring =
        {
            new float[] { 260f, 20f, 38f },
            new float[] { 360f, 30f, 56f },
            new float[] { 470f, 42f, 74f },
        };
        private static readonly Color[] Tint =
        {
            new Color(0.135f, 0.160f, 0.215f),
            new Color(0.175f, 0.205f, 0.265f),
            new Color(0.215f, 0.245f, 0.305f),
        };

        private static void Ridges(Transform parent, System.Text.StringBuilder log)
        {
            for (int k = 0; k < Ring.Length; k++)
            {
                var go = Make(parent, "능선_" + (k + 1),
                              Ridge(Ring[k][0], Ring[k][1], Ring[k][2], k),
                              UnlitMat("M_능선_" + (k + 1), Tint[k]));
                go.transform.position = new Vector3(-3f, Ground, 1f);
            }
            log.AppendLine("── 원경 산 능선 셋 (260m·360m·470m, 멀수록 높고 옅게)");
        }

        /// <summary>
        /// 능선 휘장 하나. 밑을 <b>한참 아래까지</b> 내려 하늘이 새지 않게 하고,
        /// 웃선은 주기가 다른 물결 셋을 겹쳐 들쭉날쭉하게 만든다.
        /// 안팎 어느 쪽에서 봐도 보이게 두 겹으로 감는다 — 몇백 장뿐이라 아깝지 않다.
        /// </summary>
        private static Mesh Ridge(float radius, float lo, float hi, int seed)
        {
            const int N = 96;
            var v = new List<Vector3>(); var tri = new List<int>();
            float p1 = seed * 2.3f, p2 = seed * 5.1f, p3 = seed * 1.7f;

            for (int i = 0; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                float w = 0.55f * Mathf.Sin(a * 3f + p1) + 0.30f * Mathf.Sin(a * 7f + p2) + 0.15f * Mathf.Sin(a * 13f + p3);
                float h = Mathf.Lerp(lo, hi, (w + 1f) * 0.5f);
                float cx = Mathf.Cos(a) * radius, cz = Mathf.Sin(a) * radius;
                v.Add(new Vector3(cx, -60f, cz));   // 밑 — 지평선 한참 아래
                v.Add(new Vector3(cx, h, cz));      // 웃 — 능선
            }
            for (int i = 0; i < N; i++)
            {
                int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                tri.Add(b0); tri.Add(t0); tri.Add(b1);
                tri.Add(t0); tri.Add(t1); tri.Add(b1);
                tri.Add(b1); tri.Add(t0); tri.Add(b0);   // 뒤에서도 보이게
                tri.Add(b1); tri.Add(t1); tri.Add(t0);
            }
            var m = new Mesh { name = "능선" };
            m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── ④ 들길 ────────────────────────────────

        private static void Paths(Transform parent, System.Text.StringBuilder log)
        {
            // 대문에서 남쪽으로 뻗어 숲을 지나 들로 나가는 길. 곧게 그으면 길이 아니라
            // 자국이 된다 — 옆으로 조금씩 흔들어야 사람이 다닌 길로 보인다.
            var gate = new[]
            {
                new Vector3(-7.5f, 0f, -37f), new Vector3(-8.6f, 0f, -48f), new Vector3(-6.2f, 0f, -60f),
                new Vector3(-9.4f, 0f, -74f), new Vector3(-5.0f, 0f, -92f), new Vector3(-7.0f, 0f, -115f),
            };
            var go1 = Make(parent, "길_대문", Ribbon(gate, 3.0f), DirtMat());
            go1.transform.position = new Vector3(0f, Ground + 0.02f, 0f);

            // 그 길과 만나 동서로 지나가는 들길.
            var cross = new[]
            {
                new Vector3(-120f, 0f, -66f), new Vector3(-80f, 0f, -61f), new Vector3(-40f, 0f, -64f),
                new Vector3(-8.0f, 0f, -60f), new Vector3(30f, 0f, -66f), new Vector3(75f, 0f, -58f),
                new Vector3(120f, 0f, -62f),
            };
            var go2 = Make(parent, "길_들", Ribbon(cross, 2.4f), DirtMat());
            go2.transform.position = new Vector3(0f, Ground + 0.018f, 0f);
            log.AppendLine("── 들길 둘 (대문에서 남으로 · 동서로 가로질러)");
        }

        /// <summary>가운뎃선을 따라 폭을 준 띠. 모서리마다 앞뒤 방향을 평균 내 각을 부드럽게 한다.</summary>
        private static Mesh Ribbon(Vector3[] pts, float width)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            float run = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 fwd;
                if (i == 0) fwd = pts[1] - pts[0];
                else if (i == pts.Length - 1) fwd = pts[i] - pts[i - 1];
                else fwd = (pts[i + 1] - pts[i - 1]);
                fwd.y = 0f; fwd.Normalize();
                var side = new Vector3(-fwd.z, 0f, fwd.x) * (width * 0.5f);
                if (i > 0) run += Vector3.Distance(pts[i - 1], pts[i]);
                v.Add(pts[i] - side); uv.Add(new Vector2(0f, run * 0.25f));
                v.Add(pts[i] + side); uv.Add(new Vector2(1f, run * 0.25f));
            }
            for (int i = 0; i < pts.Length - 1; i++)
            {
                // <b>감는 방향에 낯이 달렸다.</b> 처음에 a→c→b 로 감았더니 낯이 <b>땅속을</b>
                // 보고 누워, 길이 깔리긴 깔렸는데 위에서는 한 줄도 안 보였다.
                // (앞뒤로 재 보니 법선이 −y 였다.) 뒤집어 감는다.
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tri.Add(a); tri.Add(b); tri.Add(c);
                tri.Add(b); tri.Add(d); tri.Add(c);
            }
            var m = new Mesh { name = "길" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── 잔심부름 ──────────────────────────────

        private static Mesh Disc(float radius, int n)
        {
            var v = new List<Vector3> { Vector3.zero };
            var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var tri = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
                uv.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
            }
            for (int i = 1; i <= n; i++) { tri.Add(0); tri.Add(i + 1); tri.Add(i); }
            var m = new Mesh { name = "먼땅" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        private static GameObject Make(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private static Material GrassMat(string name, Color tint, float tiling)
        {
            var m = Load(name, "Universal Render Pipeline/Lit");
            var bc = AssetDatabase.LoadAssetAtPath<Texture>(GrassBC);
            if (bc != null) { m.SetTexture("_BaseMap", bc); m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling)); }
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", 0.04f); m.SetFloat("_Metallic", 0f);
            Save(m); return m;
        }

        /// <summary>
        /// 길의 흙.
        ///
        /// 처음에 마당 흙(<c>MI_Ground01A</c>)을 그대로 물렸더니 <b>시멘트 포장</b>처럼
        /// 허옇게 났다. 그 재질은 낮의 마당에 맞춰 밝게 칠해 둔 것이고, 달밤의 풀밭
        /// 위에서는 저 혼자 뜬다. 게다가 마당이 같이 쓰는 재질이라 손대면 마당까지 바뀐다.
        /// 그림만 빌리고 <b>따로 한 벌</b> 만들어 어둡게 눌러 쓴다. 길이 늘어지지 않게
        /// 결도 따라 깔아 준다.
        /// </summary>
        private static Material DirtMat()
        {
            var m = Load("M_들길", "Universal Render Pipeline/Lit");
            var src = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/MI_Ground01A.mat");
            if (src != null && src.HasProperty("_BaseMap"))
            {
                var t = src.GetTexture("_BaseMap");
                if (t != null) m.SetTexture("_BaseMap", t);
            }
            m.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
            // 어둡게 누르니 이번엔 풀에 묻혀 길이 안 보였다. 참고로 삼은 마을 사진에서도
            // <b>길은 둘레보다 밝다</b> — 밟혀 풀이 죽고 마른 흙이 드러난 자리라 그렇다.
            // 밝기는 도로 올리되 <b>누렇게</b> 데워, 시멘트가 아니라 흙으로 보이게 한다.
            m.SetColor("_BaseColor", new Color(0.66f, 0.56f, 0.43f));
            m.SetFloat("_Smoothness", 0.03f); m.SetFloat("_Metallic", 0f);
            Save(m); return m;
        }

        private static Material UnlitMat(string name, Color c)
        {
            // <b>빛을 안 받게 한다.</b> 원경 산에 달빛이 걸리면 가까운 나무와 같은 결로
            // 밝아져 거리가 죽는다. 산은 칠한 대로만 보여야 멀어 보인다.
            var m = Load(name, "Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", c);
            Save(m); return m;
        }

        private static Material Load(string name, string shader)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        private static void Save(Material m) { EditorUtility.SetDirty(m); AssetDatabase.SaveAssets(); }
    }
}
