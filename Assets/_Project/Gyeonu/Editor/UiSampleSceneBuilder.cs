using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// <b>UI 예제 씬을 만든다</b> (2026-08-26, 멱등).
    ///
    /// ■ 왜 씬 파일을 꾸러미에 넣지 않고 만들어 쓰나
    ///   씬은 붙어 있는 스크립트를 <b>GUID</b>로 가리킨다. 그런데 이 꾸러미는 원본을 사본으로 떠서
    ///   굽기 때문에(<see cref="UiPackageExporter"/>), 사본의 GUID 는 원본과 다르다.
    ///   미리 만들어 둔 씬을 넣으면 받는 쪽에서 <b>붙어 있던 스크립트가 전부 떨어져 나간다.</b>
    ///   그래서 씬은 <b>받은 사람 프로젝트에서 그 자리에 만든다</b> — 그러면 GUID 가 언제나 맞는다.
    ///   (이 프로젝트의 규약이기도 하다: 배치는 손이 아니라 에디터 스크립트로.)
    ///
    /// ■ 씬에 무엇이 서는가
    ///   지면·해 / 걷는 플레이어(눈 = MainCamera) / 더미 소지품 넷 / 더미 NPC 하나 /
    ///   더미 소품 둘 / 모드 감시기(F8).
    ///   열자마자 Play 를 누르면 대화창·소지품·안내·조준점·퍼즐 얼개를 전부 볼 수 있다.
    ///
    /// ⚠️ 이 씬은 <b>Build Settings 에 넣지 않는다</b> — 예제일 뿐이다.
    /// </summary>
    public static class UiSampleSceneBuilder
    {
        const string Folder = "Assets/IMUNROK_UI/Sample";
        const string ScenePath = Folder + "/UI_Sample.unity";

        // 메뉴 자리를 견우 쪽으로 옮긴다 — 같은 경로를 꾸러미(IMUNROK_UI)도 쓰고 있어 둘 중 하나가 등록에 실패했다.
        [MenuItem("Tools/이문록/견우/UI 예제 씬 만들기", false, 430)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[UI 예제] Play 중에는 만들 수 없다. 멈추고 다시 눌러라.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            MakeGround();
            MakeSun();
            var eye = MakePlayer();
            MakeDummies();
            MakeNpc();
            MakeProps();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            // ⚠️ 여기서 EditorUtility.DisplayDialog 를 띄우지 말 것 (2026-08-26에 물렸다).
            //    모달 대화상자는 에디터 주 스레드를 멈춰 세운다 — 자동화(MCP)로 이 메뉴를 부르면
            //    누가 「확인」을 누를 때까지 유니티가 통째로 얼어붙는다. 알릴 것은 콘솔로 충분하다.
            Debug.Log("[UI 예제] 씬을 만들었다 — " + ScenePath
                      + "\n  Play 를 누르고:"
                      + "\n    · 앞의 사람을 바라보고 좌클릭 → 대화창"
                      + "\n    · I → 소지품 판 (칸을 좌클릭 → 상세, 돋보기 → 전체 화면 조사)"
                      + "\n    · 옆의 도형을 좌클릭 → 퍼즐 얼개 (문제 글·조작 안내·비네트)"
                      + "\n    · F8 → PC ↔ VR 전환"
                      + "\n  ⚠ 이 씬은 Build Settings 에 넣지 않는다.");
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/IMUNROK_UI"))
                AssetDatabase.CreateFolder("Assets", "IMUNROK_UI");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/IMUNROK_UI", "Sample");
        }

        static void MakeGround()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "지면";
            g.transform.localScale = new Vector3(4f, 1f, 4f);
            Paint(g, new Color(0.34f, 0.30f, 0.25f));

            // 뒤쪽 벽 — 판이 벽을 어떻게 피하는지 보라고 하나 세워 둔다
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "벽";
            w.transform.position = new Vector3(0f, 1.6f, 6f);
            w.transform.localScale = new Vector3(12f, 3.2f, 0.3f);
            Paint(w, new Color(0.62f, 0.60f, 0.57f));
        }

        static void MakeSun()
        {
            var go = new GameObject("해");
            go.transform.rotation = Quaternion.Euler(46f, 35f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            l.color = new Color(1f, 0.96f, 0.88f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.46f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.35f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.20f, 0.18f);
        }

        static Transform MakePlayer()
        {
            var go = new GameObject("플레이어");
            go.transform.position = new Vector3(0f, 0.1f, -2.2f);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = 0.3f; cc.center = new Vector3(0f, 0.85f, 0f);

            var eye = new GameObject("눈");
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            eye.tag = "MainCamera";                       // ⚠ 이 태그가 있어야 판들이 눈을 찾는다
            var cam = eye.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            eye.AddComponent<AudioListener>();

            var walk = go.AddComponent<DebugWalkController>();
            walk.eye = eye.transform;                     // 조준·소지품 입력은 워커가 알아서 붙인다

            var watcher = new GameObject("모드감시_F8");
            watcher.AddComponent<UiModeWatcher>();
            return eye.transform;
        }

        static void MakeDummies()
        {
            var go = new GameObject("더미_소지품");
            go.AddComponent<DummyItemSource>();
        }

        static void MakeNpc()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "더미NPC_선비";
            go.transform.position = new Vector3(1.1f, 0.9f, 1.4f);
            go.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            Paint(go, new Color(0.30f, 0.32f, 0.40f));
            var npc = go.AddComponent<DummyNpc>();
            npc.displayName = "선비";
            npc.speakerName = "이름 모를 선비";
        }

        static void MakeProps()
        {
            var a = GameObject.CreatePrimitive(PrimitiveType.Cube);
            a.name = "더미소품_문짝";
            a.transform.position = new Vector3(-1.6f, 1.0f, 1.2f);
            a.transform.localScale = new Vector3(0.7f, 1.2f, 0.12f);
            Paint(a, new Color(0.40f, 0.28f, 0.18f));
            var pa = a.AddComponent<DummyProp>();
            pa.displayName = "문짝";
            pa.note = "돌쩌귀에 긁힌 자국이 넉 줄. 세 줄은 깊고 한 줄은 얕다.";
            pa.status = "아직 무엇을 뜻하는지 모르겠다.";

            var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            b.name = "더미소품_항아리";
            b.transform.position = new Vector3(-0.3f, 0.55f, 2.2f);
            b.transform.localScale = new Vector3(0.5f, 0.55f, 0.5f);
            Paint(b, new Color(0.33f, 0.26f, 0.22f));
            var pb = b.AddComponent<DummyProp>();
            pb.displayName = "항아리";
            pb.note = "안이 비어 있다. 바닥에 마른 곡식 낟알 몇이 남았다.";
        }

        static void Paint(GameObject go, Color c)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { color = c };
            mr.sharedMaterial = mat;
        }
    }
}
