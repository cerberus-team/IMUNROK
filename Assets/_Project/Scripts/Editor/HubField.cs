using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 조사청 바깥을 <b>들판</b>으로 깐다. 메뉴: [이문록 ▸ 조사청 ▸ 바깥 들판 깔기]
    ///
    /// <b>왜</b>: 조사청 배경이 받아온 소쇄원 정원 통째였다. 켜진 것만 세어
    /// <b>1,104만 삼각형</b>이고, 그중 지형 한 장(SM_Landscape_1)이 832만이다.
    /// 게다가 그 지형은 한 덩이라 <b>부분 컬링이 안 된다</b> — 문틈으로 귀퉁이 한 뼘만
    /// 보여도 832만이 통째로 그려진다. 조사청은 이제 상자 방이고 안에서 보이는 것은
    /// 열린 문과 창으로 드는 마당 한 뼘뿐인데, 그 한 뼘 값이 저것이다.
    ///
    /// 그래서 정원을 <b>끄고</b>(지우지 않는다) 평지 한 장에 나무·돌·꽃을 흩뿌린다.
    ///
    /// <b>무엇을 심나</b>: Fristy 꾸러미로 갈아탔다. 앞서 쓰던 문화재 꾸러미보다
    /// 자릿수가 다르게 가볍고, 스타일라이즈라 결도 하나로 간다.
    ///
    ///   갈래   Fristy        앞서 쓰던 것
    ///   나무   8,607~13,016  단풍 42,734 · 곰솔 17,331
    ///   바위     276~1,198   576~9,520
    ///   꽃       228~416     해당화 40,540   ← <b>백 분의 일</b>
    ///   풀        16~96      억새 5,968 · 쑥 1,486
    ///
    /// 꽃과 풀이 거의 공짜라, 여태 못 하던 <b>촘촘하게</b>가 된다. 앞서는 나무 스물에
    /// 꽃 다섯이었는데 그것으로 53만이었다. 이제 그보다 훨씬 배게 심고도 그 아래다.
    ///
    /// 렌더러 수도 본다. 한 프리팹이 렌더러 스물셋인 것도 있는데(Tree_Prefab_2),
    /// 삼각형은 같아도 드로우콜이 스물셋이다. 렌더러가 적은 쪽을 고른다.
    ///
    /// <b>건물을 기점으로 켜를 나눈다</b>: 조사청에서 멀어질수록 심는 것이 달라진다.
    ///   · 8~16m  풀과 꽃만. 집이 맨땅에 놓이지 않고 <b>풀 속에</b> 앉는다.
    ///   · 14~40m 거기에 바위와 덤불이 섞인다.
    ///   · 26~76m 나무가 선다. 멀리 갈수록 나무만 남아 숲 가장자리가 된다.
    /// 갈래마다 <b>서로 얼마나 붙어도 되는지</b>가 다르다(minGap) — 풀은 1.1m 까지
    /// 붙어도 되고 나무는 7m 는 떨어져야 한다. 한 값으로 재면 풀이 성기거나 나무가 겹친다.
    ///
    /// <b>흩뿌리기는 제자리를 지킨다</b>: 씨앗을 박아 둔 난수라 몇 번을 다시 깔아도
    /// 같은 자리에 같은 것이 선다. 눈으로 맞춰 놓고 다시 눌렀더니 딴 데 가 있으면
    /// 맞출 수가 없다. 조사청 둘레 <see cref="Clear"/> m 안과 앞길에는 아무것도 안 심는다.
    ///
    /// ★정원을 도로 켜려면 하이어라키에서 소쇄원_정원 을 켜고 이 들판을 끄면 된다.
    /// </summary>
    public static class HubField
    {
        private const string RootName = "조사청_들판";
        private const string GardenName = "소쇄원_정원";

        // ── 터 ──
        private static readonly Vector3 Center = new Vector3(70.13f, 0f, 283.33f);
        private const float GroundY = 136.95f;   // 기단이 앉는 높이
        private const float Ground = 190f;       // 평지 한 변(m)
        private const float Clear = 8f;          // 조사청 둘레 이 안에는 아무것도 없다(마당)
        private const float Far = 76f;           // 이보다 멀리는 안 심는다(어차피 안 보인다)
        private const int Seed = 20260823;

        private const string GroundMatPath = "Assets/_Project/_Common/Materials/M_조사청_들판.mat";

        /// <summary>심을 것 — 경로·개수·거리·크기·서로 떨어질 거리.</summary>
        private class Plant
        {
            public string path;
            public int count;
            public float rMin, rMax;      // 조사청에서 이 거리 사이에
            public float sMin, sMax;      // 크기 흔들기
            public float gap;             // 다른 것과 이만큼은 떨어진다(m)
            public string group;
        }

        private const string F = "Assets/Fristy stylize Modular Assets 2/Prefabs/";

        private static readonly Plant[] Kinds =
        {
            // ── 나무 — 값이 나가는 쪽이므로 멀리, 성기게 ──
            new Plant{ path=F+"3_1_Tree.prefab", count=12, rMin=26f, rMax=Far, sMin=0.80f, sMax=1.35f, gap=7f,  group="나무" },
            new Plant{ path=F+"3_2_Tree.prefab", count=10, rMin=30f, rMax=Far, sMin=0.75f, sMax=1.25f, gap=8f,  group="나무" },
            new Plant{ path=F+"3_3_Tree.prefab", count=8,  rMin=34f, rMax=Far, sMin=0.70f, sMax=1.15f, gap=9f,  group="나무" },

            // ── 바위 — 싸다(276~1,198). 크고 작은 것을 섞어 켜를 만든다 ──
            new Plant{ path=F+"2_Rock.prefab",   count=6,  rMin=22f, rMax=60f, sMin=0.55f, sMax=1.10f, gap=6f,  group="바위" },
            new Plant{ path=F+"4_Rock_2.prefab", count=10, rMin=16f, rMax=52f, sMin=0.60f, sMax=1.30f, gap=4f,  group="바위" },
            new Plant{ path=F+"4_Rock_1.prefab", count=14, rMin=14f, rMax=48f, sMin=0.60f, sMax=1.40f, gap=3f,  group="바위" },
            new Plant{ path=F+"4_Rock.prefab",   count=16, rMin=13f, rMax=46f, sMin=0.55f, sMax=1.35f, gap=3f,  group="바위" },
            new Plant{ path=F+"3_Rock_1.prefab", count=18, rMin=12f, rMax=44f, sMin=0.50f, sMax=1.30f, gap=2.4f, group="바위" },
            new Plant{ path=F+"3_Rock_3.prefab", count=16, rMin=12f, rMax=42f, sMin=0.50f, sMax=1.30f, gap=2.4f, group="바위" },
            new Plant{ path=F+"3_Rock_4.prefab", count=14, rMin=12f, rMax=40f, sMin=0.50f, sMax=1.25f, gap=2.4f, group="바위" },

            // ── 꽃 — 228·416 이라 마음껏 심는다. 집 가까이가 제일 촘촘하다 ──
            new Plant{ path=F+"Purple Plant Variant.prefab", count=46, rMin=8f,  rMax=30f, sMin=0.70f, sMax=1.50f, gap=1.5f, group="꽃" },
            new Plant{ path=F+"White Plant Variant.prefab",  count=42, rMin=8f,  rMax=32f, sMin=0.70f, sMax=1.50f, gap=1.5f, group="꽃" },
            new Plant{ path=F+"Plant_1 Variant.prefab",      count=34, rMin=9f,  rMax=36f, sMin=0.70f, sMax=1.40f, gap=1.6f, group="꽃" },
            new Plant{ path=F+"Plant_3 Variant.prefab",      count=22, rMin=12f, rMax=40f, sMin=0.60f, sMax=1.20f, gap=2.2f, group="꽃" },

            // ── 풀 — 16~96 삼각형. 여기서 촘촘함이 나온다 ──
            new Plant{ path=F+"1_Grass_2.prefab",        count=150, rMin=8f,  rMax=52f, sMin=0.70f, sMax=1.80f, gap=1.1f, group="풀" },
            new Plant{ path=F+"5_Weed 1.prefab",         count=90,  rMin=9f,  rMax=48f, sMin=0.70f, sMax=1.70f, gap=1.3f, group="풀" },
            new Plant{ path=F+"5_Weed 2 Variant.prefab", count=80,  rMin=9f,  rMax=46f, sMin=0.70f, sMax=1.70f, gap=1.3f, group="풀" },
            new Plant{ path=F+"5_Weed_7  Variant.prefab",count=70,  rMin=10f, rMax=44f, sMin=0.70f, sMax=1.60f, gap=1.4f, group="풀" },
            new Plant{ path=F+"6_Weed Variant.prefab",   count=60,  rMin=10f, rMax=42f, sMin=0.70f, sMax=1.60f, gap=1.5f, group="풀" },
        };

        [MenuItem("이문록/조사청/바깥 들판 깔기")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[들판] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "바깥 들판 깔기");

            var log = new StringBuilder();
            long tris = Ground_(root.transform, log);
            tris += Scatter(root.transform, log);

            // 정원은 끈다. 지우지 않는 까닭은 되돌릴 길을 남겨 두기 위해서다.
            var garden = Find(GardenName);
            if (garden != null && garden.activeSelf)
            {
                Undo.RecordObject(garden, "정원 끄기");
                garden.SetActive(false);
                log.AppendLine("   소쇄원_정원 을 껐습니다(지우지 않았습니다).");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("[들판] 깔았습니다 — " + tris.ToString("N0") + " 삼각형.\n" + log);
        }

        /// <summary>평지 한 장. 무늬는 제 크기에 맞춰 되풀이시킨다(안 그러면 10m 짜리 풀잎이 된다).</summary>
        private static long Ground_(Transform parent, StringBuilder log)
        {
            var mat = GroundMaterial();
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "평지";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(Center.x, GroundY - 0.5f, Center.z);
            go.transform.localScale = new Vector3(Ground, 1f, Ground);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            var st = new Vector4(Ground * 0.25f, Ground * 0.25f, 0f, 0f);
            var mpb = new MaterialPropertyBlock();
            mpb.SetVector(Shader.PropertyToID("_BaseMap_ST"), st);
            mpb.SetVector(Shader.PropertyToID("_MainTex_ST"), st);
            r.SetPropertyBlock(mpb);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
            log.AppendLine("   평지 " + Ground + "m · 윗면 y=" + GroundY);
            return 12;
        }

        /// <summary>씨앗을 박은 난수로 흩뿌린다. 몇 번을 다시 깔아도 같은 자리다.</summary>
        private static long Scatter(Transform parent, StringBuilder log)
        {
            var rng = new System.Random(Seed);
            var groups = new Dictionary<string, Transform>();
            long tris = 0;
            // (x, z, 이 자리가 요구하는 간격). 간격이 갈래마다 다르므로 자리마다 들고 다닌다 —
            // 풀 곁에 나무가 서는 것은 되지만, 나무 곁에 나무가 서면 안 된다.
            var taken = new List<Vector3>();

            foreach (var k in Kinds)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(k.path);
                if (src == null) { log.AppendLine("   ✘ 없음: " + k.path); continue; }

                Transform g;
                if (!groups.TryGetValue(k.group, out g))
                {
                    var go = new GameObject(k.group);
                    go.transform.SetParent(parent, false);
                    g = go.transform;
                    groups[k.group] = g;
                }

                long one = 0;
                foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                        one += (long)mf.sharedMesh.GetIndexCount(s) / 3;
                }

                int made = 0;
                for (int i = 0; i < k.count; i++)
                {
                    Vector2 p;
                    if (!Spot(rng, k.rMin, k.rMax, k.gap, taken, out p)) continue;
                    taken.Add(new Vector3(p.x, p.y, k.gap));

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, g);
                    inst.transform.position = new Vector3(Center.x + p.x, GroundY, Center.z + p.y);
                    inst.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    float s2 = Mathf.Lerp(k.sMin, k.sMax, (float)rng.NextDouble());
                    inst.transform.localScale = src.transform.localScale * s2;
                    GameObjectUtility.SetStaticEditorFlags(inst, StaticEditorFlags.BatchingStatic);
                    made++;
                }
                tris += one * made;
                log.AppendLine("   " + System.IO.Path.GetFileNameWithoutExtension(k.path).PadRight(20)
                               + made + "그루/개  " + (one * made).ToString("N0") + " 삼각형");
            }
            return tris;
        }

        /// <summary>
        /// 심을 자리 하나. 조사청 둘레와 앞길을 비우고, 이미 심은 것과 너무 붙지 않게 한다.
        /// 스무 번 굴려도 자리를 못 찾으면 그 그루는 건너뛴다 — 억지로 밀어 넣으면 겹친다.
        /// </summary>
        private static bool Spot(System.Random rng, float rMin, float rMax, float gap,
                                 List<Vector3> taken, out Vector2 p)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(Mathf.Max(rMin, Clear), rMax, Mathf.Sqrt((float)rng.NextDouble()));
                p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);

                // 앞길 — 남쪽으로 난 길목은 비워 둔다. 나무가 길을 막으면 나갈 데가 없어 보인다.
                if (Mathf.Abs(p.x) < 4.5f && p.y < 0f) continue;

                // 두 자리가 요구하는 간격 가운데 <b>큰 쪽</b>을 지킨다. 작은 쪽으로 재면
                // 풀이 요구한 1.1m 만 띄우고 나무가 나무 옆에 선다.
                bool clash = false;
                foreach (var q in taken)
                {
                    float need = Mathf.Max(gap, q.z);
                    float dx = q.x - p.x, dy = q.y - p.y;
                    if (dx * dx + dy * dy < need * need) { clash = true; break; }
                }
                if (!clash) return true;
            }
            p = Vector2.zero;
            return false;
        }

        /// <summary>들판 바닥 재질. 없으면 정원이 쓰던 지형 그림을 그대로 물려 만든다.</summary>
        private static Material GroundMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(GroundMatPath);
            if (m != null) return m;

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh) { name = "M_조사청_들판" };
            m.SetColor("_BaseColor", new Color(0.30f, 0.31f, 0.22f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.06f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);

            // 정원 지형이 쓰던 그림이 있으면 그것을 쓴다 — 두 배경이 같은 땅으로 보여야 한다.
            var land = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Soswaewon/Materials/Landscape/TransientM_Landscape_Inst(MID_M_Landscape_Inst_36_102184).mat");
            if (land != null && land.HasProperty("_BaseMap"))
            {
                var tex = land.GetTexture("_BaseMap");
                if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); }
            }
            AssetDatabase.CreateAsset(m, GroundMatPath);
            AssetDatabase.SaveAssets();
            return m;
        }

        /// <summary>꺼져 있어도 찾는다 — GameObject.Find 는 꺼진 것을 못 본다.</summary>
        private static GameObject Find(string name)
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }
    }
}
