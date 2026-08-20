using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 은하담 별 길 — 경로망(웨이포인트 그래프) 저작기. 멱등.
    ///
    /// 2026-08-18 개편: 전에는 별 메시를 고정 경로로 미리 구워 뒀다. 이제는 런타임이
    /// 플레이어 위치에서 목적지까지 그때그때 경로를 구하므로, 빌더가 하는 일은
    /// **어디를 지날 수 있는가**를 적어 두는 것뿐이다.
    ///
    /// ■ 그래프 설계
    ///   강이 남북으로 흐르고 건널 곳은 오작교 하나다. 그래서 노드를 세 덩어리로 둔다:
    ///     동안(진입로·석축 산책로) — 다리 마루 — 서안(강변·군락 우회·북서 언덕)
    ///   간선은 이웃한 노드끼리만 잇는다. 다리 노드를 거치지 않고는 동↔서로 못 가므로,
    ///   A*가 알아서 다리로 우회시킨다. 물 위나 다리 밑을 지나는 경로는 나올 수가 없다.
    ///
    /// ■ 높이
    ///   노드 XZ만 적고 Y는 실행 시 레이캐스트로 찍는다. 다리 마루(8.8)와 강변(2.0)이
    ///   섞여 있어 손으로 적으면 반드시 틀린다.
    /// </summary>
    public static class StarPathBuilder
    {
        const string SceneName = "Gyeonu_EunhaDam";
        const string RootName  = "별길_은하수";
        const string MatDir    = "Assets/_Project/Gyeonu/Art/Materials/StarPath";
        const string GlowTex   = "Assets/_Project/Gyeonu/Art/Textures/Observatory/T_관측실_별글로우.png";

        /// <summary>노드 XZ. Y는 레이캐스트로 찍는다.</summary>
        static readonly Vector2[] NodeXZ =
        {
            // ── 동안 (0~5) : 진입로와 석축 산책로 ──
            new Vector2(102f,   0f),   // 0  씬 진입 지점
            new Vector2( 84f,   0f),   // 1
            new Vector2( 68f,   0f),   // 2
            new Vector2( 54f,   0f),   // 3  문루 앞 단
            new Vector2( 44f,  -9f),   // 4  석축 산책로 북단
            new Vector2( 37f, -11f),   // 5  석축 산책로 남단 = 암문 앞

            // ── 다리 마루 (6~10) ──
            new Vector2( 46f,   0f),   // 6  동쪽 문루 계단
            new Vector2( 24f,   0f),   // 7
            new Vector2(  0f,   0f),   // 8  다리 한복판
            new Vector2(-24f,   0f),   // 9
            new Vector2(-46f,   0f),   // 10 서쪽 문루 계단

            // ── 서안 (11~18) : 군락을 북으로 돌아 언덕으로 ──
            new Vector2(-54f,   2f),   // 11 다리에서 내려선 강변
            new Vector2(-60f,  10f),   // 12
            new Vector2(-62f,  22f),   // 13 군락 동쪽 가장자리
            new Vector2(-60f,  34f),   // 14 군락을 북으로 우회
            new Vector2(-63f,  44f),   // 15
            new Vector2(-68f,  52f),   // 16 복귀 스폰 자리
            new Vector2(-74f,  58f),   // 17 견우마을 입구
            new Vector2(-56f, -14f),   // 18 남쪽 강변 (다른 볼일용 가지)
        };

        /// <summary>간선 — 이웃끼리만.</summary>
        static readonly int[] Edges =
        {
            0,1, 1,2, 2,3, 3,6,          // 진입로 → 문루
            3,4, 4,5,                    // 문루 → 석축 산책로 → 암문
            6,7, 7,8, 8,9, 9,10,         // 다리 마루
            10,11, 11,12, 12,13, 13,14, 14,15, 15,16, 16,17,   // 서안 북행
            11,18,                       // 남쪽 가지
        };

        const int GoalNode = 17;

        [MenuItem("Tools/이문록/별 길 경로망 생성 (은하담)", priority = 320)]
        /// <summary>기본 강도 = B안(표준). 2026-08-18 확정.</summary>
        public static void Build() => BuildWith(StarPathGuide.Strength.표준);

        [MenuItem("Tools/이문록/별 길 강도 — 은은", priority = 322)]
        static void S0() => SetStrength(StarPathGuide.Strength.은은);
        [MenuItem("Tools/이문록/별 길 강도 — 표준", priority = 323)]
        static void S1() => SetStrength(StarPathGuide.Strength.표준);
        [MenuItem("Tools/이문록/별 길 강도 — 강렬", priority = 324)]
        static void S2() => SetStrength(StarPathGuide.Strength.강렬);

        static void SetStrength(StarPathGuide.Strength s)
        {
            var guide = Object.FindFirstObjectByType<StarPathGuide>(FindObjectsInactive.Include);
            if (guide == null) { Debug.LogError("[별길] StarPathGuide가 없습니다 — 경로망을 먼저 생성하세요."); return; }
            Undo.RecordObject(guide, "별 길 강도");
            guide.strength = s;
            EditorUtility.SetDirty(guide);
            if (Application.isPlaying && guide.IsVisible) guide.Rebuild();
            Debug.Log("[별길] 강도 = " + s);
        }

        public static void BuildWith(StarPathGuide.Strength strength)
        {
            if (SceneManager.GetActiveScene().name != SceneName)
            {
                Debug.LogError($"[별길] '{SceneName}' 씬에서 실행하세요.");
                return;
            }

            EnsureFolder(MatDir);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTex);
            if (tex == null)
            {
                Debug.LogError($"[별길] 별 글로우 텍스처가 없습니다: {GlowTex}");
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "별 길 경로망 생성");

            // 노드 Y 찍기
            var nodes = new Vector3[NodeXZ.Length];
            for (int i = 0; i < NodeXZ.Length; i++)
            {
                var xz = NodeXZ[i];
                nodes[i] = new Vector3(xz.x, GroundY(xz), xz.y);
            }

            var router = root.AddComponent<StarPathRouter>();
            router.nodes = nodes;
            router.edges = Edges;

            var guide = root.AddComponent<StarPathGuide>();
            guide.router = router;
            guide.destinationPoint = nodes[GoalNode];
            guide.strength = strength;
            guide.material = StarMaterial(tex);

            // 간선 중 걸어갈 수 없는 것이 있으면 알린다 (지형이 바뀌었을 때 조기 발견)
            int bad = 0;
            for (int e = 0; e + 1 < Edges.Length; e += 2)
                if (!router.Walkable(nodes[Edges[e]], nodes[Edges[e + 1]]))
                {
                    Debug.LogWarning($"[별길] 간선 {Edges[e]}→{Edges[e + 1]} 이 보행 불가로 판정됨 " +
                                     $"({nodes[Edges[e]].ToString("F1")} → {nodes[Edges[e + 1]].ToString("F1")})");
                    bad++;
                }

            AssetDatabase.SaveAssets();
            EditorSceneManager_MarkDirty();
            Debug.Log($"[별길] 경로망 노드 {nodes.Length} / 간선 {Edges.Length / 2} 생성. " +
                      $"목적지 = 노드{GoalNode} {nodes[GoalNode].ToString("F1")}. 강도 {strength}. " +
                      (bad == 0 ? "간선 전부 보행 가능." : $"⚠ 보행 불가 간선 {bad}개."));
        }

        static void EditorSceneManager_MarkDirty()
            => UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        /// <summary>노드 높이 — 씬에 세워 둔 디버그 워커 캡슐은 건너뛴다(자기 몸에 맞으면 노드가 뜬다).</summary>
        static float GroundY(Vector2 xz)
        {
            var hits = Physics.RaycastAll(new Vector3(xz.x, 60f, xz.y), Vector3.down,
                                          140f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider is CharacterController) continue;
                if (h.collider.GetComponentInParent<DebugWalkController>() != null) continue;
                return h.point.y;
            }
            var t = Terrain.activeTerrain;
            return t != null ? t.SampleHeight(new Vector3(xz.x, 0f, xz.y)) : 0f;
        }

        static Material StarMaterial(Texture2D tex)
        {
            string path = MatDir + "/M_별길.mat";
            var shader = Shader.Find("IMUNROK/별길_가산");
            if (shader == null)
            {
                Debug.LogError("[별길] IMUNROK/별길_가산 셰이더가 없습니다");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = "M_별길" };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;

            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Intensity", 1f);
            m.SetFloat("_TwinkleAmp", 0.30f);
            m.SetFloat("_TwinkleSpeed", 1.6f);
            m.SetFloat("_FlowAmp", 0.55f);      // 흐름 파동의 깊이
            m.SetFloat("_FlowLength", 9f);      // 파장(m)
            m.SetFloat("_FlowSpeed", 6f);       // 진행 속도(m/s) — 걷는 속도(3)보다 빠르게
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
