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
    /// <b>무엇을 심나</b>: 받아 둔 두 꾸러미에서 가벼운 것을 고른다.
    ///   · 나무 — 사시나무(2,084)가 압도적으로 싸다. 곰솔·버드나무를 몇 그루만 섞어 결을 낸다.
    ///     단풍은 한 그루에 42,734 라 멀리 두어도 값이 크다.
    ///   · 돌 — 작은 것(576·1,818)을 많이, 큰 것(9,520)은 하나만.
    ///   · 꽃 — 해당화가 40,540 이라 <b>가까이에만</b> 몇 포기 둔다. 어차피 꽃은
    ///     멀리서 보이지도 않는다. 나머지 바닥은 쑥·수염풀로 덮는다(1,486·1,985).
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
        private const float Clear = 14f;         // 조사청 둘레 이 안에는 아무것도 없다
        private const float Far = 78f;           // 이보다 멀리는 안 심는다(어차피 안 보인다)
        private const int Seed = 20260823;

        private const string GroundMatPath = "Assets/_Project/_Common/Materials/M_조사청_들판.mat";

        /// <summary>심을 것 — 경로·개수·크기 범위·안쪽 반지름.</summary>
        private class Plant
        {
            public string path;
            public int count;
            public float rMin, rMax;      // 조사청에서 이 거리 사이에
            public float sMin, sMax;      // 크기 흔들기
            public string group;
        }

        private static readonly Plant[] Kinds =
        {
            // 나무 — 싼 것을 많이, 비싼 것을 조금
            new Plant{ path="Assets/Soswaewon/Prefabs/Environments/SM_Aspen.prefab",   count=14, rMin=18f, rMax=Far, sMin=0.75f, sMax=1.25f, group="나무" },
            new Plant{ path="Assets/Coastal_Dune_Pack/Meshes/SM_Black_Pine.fbx",       count=4,  rMin=20f, rMax=60f, sMin=0.70f, sMax=1.10f, group="나무" },
            new Plant{ path="Assets/Soswaewon/Prefabs/Environments/SM_Willow.prefab",  count=2,  rMin=22f, rMax=45f, sMin=0.80f, sMax=1.05f, group="나무" },

            // 돌 — 작은 것이 여럿, 큰 것은 하나
            new Plant{ path="Assets/Soswaewon/Prefabs/Props/SM_Rock01d.prefab", count=12, rMin=15f, rMax=55f, sMin=0.6f, sMax=1.6f, group="돌" },
            new Plant{ path="Assets/Soswaewon/Prefabs/Props/SM_Rock01c.prefab", count=6,  rMin=16f, rMax=50f, sMin=0.7f, sMax=1.4f, group="돌" },
            new Plant{ path="Assets/Soswaewon/Prefabs/Props/SM_Rock01b.prefab", count=3,  rMin=18f, rMax=42f, sMin=0.8f, sMax=1.3f, group="돌" },
            new Plant{ path="Assets/Soswaewon/Prefabs/Props/SM_Rock01a.prefab", count=1,  rMin=26f, rMax=38f, sMin=0.9f, sMax=1.1f, group="돌" },

            // 꽃 — 해당화는 비싸다. 눈에 드는 데만.
            new Plant{ path="Assets/Coastal_Dune_Pack/Meshes/SM_Lugose.fbx",           count=5,  rMin=15f, rMax=26f, sMin=0.8f, sMax=1.3f, group="꽃" },
            new Plant{ path="Assets/Coastal_Dune_Pack/Meshes/SM_Eulalia.fbx",          count=10, rMin=16f, rMax=48f, sMin=0.8f, sMax=1.5f, group="풀" },
            new Plant{ path="Assets/Coastal_Dune_Pack/Meshes/SM_Artemisia.fbx",        count=26, rMin=15f, rMax=52f, sMin=0.7f, sMax=1.5f, group="풀" },
            new Plant{ path="Assets/Coastal_Dune_Pack/Meshes/SM_Anthephoroides.fbx",   count=22, rMin=15f, rMax=52f, sMin=0.7f, sMax=1.6f, group="풀" },
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
            var taken = new List<Vector2>();

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
                    if (!Spot(rng, k.rMin, k.rMax, taken, out p)) continue;
                    taken.Add(p);

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
        private static bool Spot(System.Random rng, float rMin, float rMax, List<Vector2> taken, out Vector2 p)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(Mathf.Max(rMin, Clear), rMax, Mathf.Sqrt((float)rng.NextDouble()));
                p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);

                // 앞길 — 남쪽으로 난 길목은 비워 둔다. 나무가 길을 막으면 나갈 데가 없어 보인다.
                if (Mathf.Abs(p.x) < 6f && p.y < 0f) continue;

                bool clash = false;
                foreach (var q in taken)
                    if ((q - p).sqrMagnitude < 9f) { clash = true; break; }   // 3m 안에 겹치지 않게
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
