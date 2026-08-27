using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>하늘을 열고 관아 밖에 숲을 두른다.</b>
    ///
    /// 관아를 밖에서 보면 <b>허공에 떠 있었다</b>. 담 너머로 아무것도 없어 마당 바닥이
    /// 끝나는 자리에서 세상이 잘려 나갔다. 안에서만 놀 때는 안 보이던 것이, 대문을
    /// 나서거나 대청에 올라 담 너머를 보는 순간 드러난다.
    ///
    /// 세 가지를 한다:
    ///   ① <b>하늘</b> — 맑은 아침. 해를 낮게(24°) 눕히고 살짝 데운다.
    ///   ② <b>바깥땅</b> — 담 밖으로 240m 짜리 풀밭 한 장. 나무만 심으면 그 나무가
    ///      허공에 서므로, 땅이 먼저다.
    ///   ③ <b>숲</b> — 담 바로 밖에 <b>좁고 빽빽하게</b> 두른다.
    ///
    /// <b>소나무가 없다.</b> 가진 꾸러미에 침엽수가 한 그루도 없어 소쇄원의 것으로
    /// 짰다 — 사시나무를 바탕으로 깔고 대나무를 군데군데 섞는다. 대숲은 원래 빽빽하게
    /// 자라니 <b>좁게 빽빽하게</b>라는 말과 맞고, 관아 담 뒤에 대숲이 선 그림도
    /// 조선에서 낯설지 않다.
    ///
    /// <b>삼각형을 세면서 심는다.</b> 대나무 한 떨기가 25,250이고 단풍은 42,734다 —
    /// 그것으로 다 채우면 씬이 300만을 넘긴다. 사시나무는 2,084뿐이라 이것으로 숲을
    /// 이루고, 비싼 것은 <b>마릿수를 못 박아</b> 점점이 섞는다. 심고 나서 얼마나
    /// 늘었는지 콘솔에 적는다 — 퀘스트에 올릴 때 깎을 자리를 알아야 한다.
    ///
    /// <b>대문 앞은 비운다.</b> 외삼문이 서쪽을 보고 있으니 그 앞은 나무를 안 심는다.
    /// 길이 숲에 막히면 어디로 들어가는 집인지 알 수가 없다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다 — 먼저 걷어내고 다시 심는다. 심는 자리는 못 박은
    /// 씨앗에서 나오므로 누를 때마다 숲이 달라지지도 않는다.
    ///
    /// 메뉴: [이문록 ▸ 관아 ▸ ㉑ 하늘 열고 밖에 숲 두르기]
    /// </summary>
    public static class GwanaGrove
    {
        private const string SkyPath = "Assets/SkySeries Freebie/CasualDay.mat";
        private const string MatDir  = "Assets/_Project/_Common/Sets/Gwana/Donheon/Materials";
        // 소쇄원의 T_Grass_BC 는 이름만 풀이고 평균색이 (105, 88, 59) — 마른 흙빛이라
        // 깔아 놓으니 담 안 마당과 구별이 안 갔다. 숲 바닥은 초록이라야 담을 사이에 두고
        // 쓸어 놓은 마당과 우거진 밖이 갈린다.
        private const string GrassBC = "Assets/Fristy stylize Modular Assets 2/Textures/Grass_DefuseFinal 2.png";
        private const string GrassNM = "Assets/Fristy stylize Modular Assets 2/Textures/Grass_normals.png";

        private const string Aspen  = "Assets/Soswaewon/Prefabs/Environments/SM_Aspen.prefab";
        private const string Bamboo = "Assets/Soswaewon/Prefabs/Environments/SM_Bamboo.prefab";
        private const string Maple  = "Assets/Soswaewon/Prefabs/Environments/SM_Maple.prefab";
        private const string Willow = "Assets/Soswaewon/Prefabs/Environments/SM_Willow.prefab";

        private const string GroundName = "관아_바깥땅";
        private const string GroveName  = "관아_바깥숲";

        /// <summary>담 밖으로 숲이 뻗는 너비(m). 좁게 두르라 하여 아홉 자다.</summary>
        private const float Belt = 9f;

        /// <summary>나무 사이(m). 여기에 흔들림을 얹어 줄지어 선 티를 지운다.</summary>
        private const float Step = 2.2f;

        /// <summary>담·처마에서 이만큼은 떨어뜨린다.</summary>
        private const float Clear = 1.5f;

        /// <summary>대문 앞으로 비워 둘 길의 반쪽 너비(m).</summary>
        private const float RoadHalf = 7f;

        /// <summary>비싼 나무는 마릿수를 못 박는다. 나머지는 다 사시나무다.</summary>
        private const int MaxBamboo = 12, MaxMaple = 3, MaxWillow = 3;

        [MenuItem("이문록/관아/㉑ 하늘 열고 밖에 숲 두르기")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 하늘을 열고 밖에 숲을 두른다\n");
            Sky(log);
            var yard = Yard(scene, log);
            Ground(scene, yard, log);
            Grove(scene, yard, log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ── ① 하늘 ────────────────────────────────

        private static void Sky(System.Text.StringBuilder log)
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null) { log.AppendLine("── 하늘 재질을 못 찾았다: " + SkyPath); return; }

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;

            // 해를 낮게 눕히고 살짝 데운다. 한낮의 해는 그림자가 발밑에 깔려 기와의
            // 결이 안 산다 — 아침 해라야 처마가 길게 눕는다.
            //
            // RoomDarkness 는 켜질 때의 ambientIntensity 를 <b>바깥 밝기</b>로 기억한다.
            // 그러니 여기서 정한 값이 곧 서고를 나왔을 때 돌아갈 밝기가 된다.
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null)
            {
                Undo.RecordObject(sun.transform, "아침 해");
                Undo.RecordObject(sun, "아침 해");
                sun.transform.rotation = Quaternion.Euler(24f, 335f, 0f);
                sun.color = new Color(1f, 0.95f, 0.86f);
                sun.intensity = 1.25f;
                EditorUtility.SetDirty(sun);
            }
            RenderSettings.sun = sun;
            DynamicGI.UpdateEnvironment();
            log.AppendLine("── 하늘 " + sky.name + " · 해 24° 아침" + (sun == null ? " (해를 못 찾았다)" : ""));
        }

        // ── ② 어디까지가 관아인가 ──────────────────

        /// <summary>
        /// 나무를 못 심는 자리. <b>담만 재면 안 된다</b> — 지붕과 외삼문이 담보다 밖으로
        /// 나와 있어서, 담을 기준으로 심으면 처마 밑에 나무가 선다. 세워 둔 것을
        /// 실제로 재서 합친다.
        /// </summary>
        private static Bounds Yard(Scene scene, System.Text.StringBuilder log)
        {
            string[] want = { "관아세트", "관아마당_좁힌담", "마당바닥", "문서고" };
            bool any = false; var b = new Bounds();
            foreach (var root in scene.GetRootGameObjects())
            {
                bool hit = false;
                for (int i = 0; i < want.Length; i++) if (root.name == want[i]) hit = true;
                if (!hit) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
            }
            if (!any) b = new Bounds(new Vector3(2.75f, 0f, 0f), new Vector3(40f, 8f, 32f));
            b.Expand(new Vector3(Clear * 2f, 0f, Clear * 2f));
            log.AppendLine("── 관아가 차지한 자리 x " + b.min.x.ToString("F1") + "~" + b.max.x.ToString("F1")
                         + "  z " + b.min.z.ToString("F1") + "~" + b.max.z.ToString("F1"));
            return b;
        }

        // ── ③ 바깥땅 ──────────────────────────────

        private static void Ground(Scene scene, Bounds yard, System.Text.StringBuilder log)
        {
            var old = Find(scene, GroundName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = GroundName;
            Undo.RegisterCreatedObjectUndo(g, "바깥땅");
            g.transform.localScale = new Vector3(24f, 1f, 24f);   // Plane 은 배율 1이 10m
            // 마당 바닥(y=0)보다 살짝 낮춘다. 같은 높이면 두 면이 다퉈 얼룩진다.
            g.transform.position = new Vector3(yard.center.x, -0.03f, yard.center.z);

            g.GetComponent<Renderer>().sharedMaterial = GrassMat();
            log.AppendLine("── 바깥땅 240m × 240m");
        }

        private static Material GrassMat()
        {
            string path = MatDir + "/M_관아_바깥풀.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            var bc = AssetDatabase.LoadAssetAtPath<Texture>(GrassBC);
            var nm = AssetDatabase.LoadAssetAtPath<Texture>(GrassNM);
            if (bc != null) { m.SetTexture("_BaseMap", bc); m.SetTextureScale("_BaseMap", new Vector2(60f, 60f)); }
            if (nm != null)
            {
                m.SetTexture("_BumpMap", nm);
                m.SetTextureScale("_BumpMap", new Vector2(60f, 60f));
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Smoothness", 0.05f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        // ── ④ 숲 ──────────────────────────────────

        private static void Grove(Scene scene, Bounds yard, System.Text.StringBuilder log)
        {
            var old = Find(scene, GroveName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var root = new GameObject(GroveName);
            Undo.RegisterCreatedObjectUndo(root, "바깥숲");

            var aspen  = AssetDatabase.LoadAssetAtPath<GameObject>(Aspen);
            var bamboo = AssetDatabase.LoadAssetAtPath<GameObject>(Bamboo);
            var maple  = AssetDatabase.LoadAssetAtPath<GameObject>(Maple);
            var willow = AssetDatabase.LoadAssetAtPath<GameObject>(Willow);
            if (aspen == null) { log.AppendLine("── 나무를 못 찾았다: " + Aspen); return; }

            // 못 박은 씨앗. 누를 때마다 숲이 바뀌면 어제 본 그림과 오늘 그림이 달라진다.
            var rng = new System.Random(20260826);

            float x0 = yard.min.x - Belt, x1 = yard.max.x + Belt;
            float z0 = yard.min.z - Belt, z1 = yard.max.z + Belt;

            int nb = 0, nm = 0, nw = 0, na = 0;
            long tri = 0;

            for (float x = x0; x <= x1; x += Step)
                for (float z = z0; z <= z1; z += Step)
                {
                    float px = x + (float)(rng.NextDouble() - 0.5) * Step * 0.8f;
                    float pz = z + (float)(rng.NextDouble() - 0.5) * Step * 0.8f;

                    // 관아 안이면 건너뛴다
                    if (px > yard.min.x && px < yard.max.x && pz > yard.min.z && pz < yard.max.z) continue;
                    // 대문 앞길이면 건너뛴다 — 외삼문은 서쪽(-x)을 본다
                    if (px < yard.min.x && Mathf.Abs(pz) < RoadHalf) continue;

                    GameObject src = aspen; int kind = 0;
                    int roll = rng.Next(100);
                    if (roll < 5 && bamboo != null && nb < MaxBamboo) { src = bamboo; kind = 1; }
                    else if (roll < 7 && maple != null && nm < MaxMaple) { src = maple; kind = 2; }
                    else if (roll < 9 && willow != null && nw < MaxWillow) { src = willow; kind = 3; }

                    var t = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
                    t.transform.position = new Vector3(px, -0.03f, pz);
                    t.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    float s = 0.75f + (float)rng.NextDouble() * 0.5f;
                    t.transform.localScale = new Vector3(s, s, s);

                    // 부딪힐 일이 없는 나무다. 담이 사람을 막으니 콜라이더는 짐만 된다.
                    foreach (var c in t.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                    foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null) tri += mf.sharedMesh.triangles.Length / 3;

                    if (kind == 1) nb++; else if (kind == 2) nm++; else if (kind == 3) nw++; else na++;
                }

            log.AppendLine("── 숲 " + (na + nb + nm + nw) + "그루"
                         + " (사시나무 " + na + " · 대나무 " + nb + " · 단풍 " + nm + " · 버들 " + nw + ")");
            log.AppendLine("   삼각형 " + (tri / 1000) + "k 를 보탰다");
        }

        private static Transform Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;
            return null;
        }
    }
}
