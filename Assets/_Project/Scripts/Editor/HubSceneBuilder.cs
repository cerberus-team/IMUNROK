using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 조사청(HubScene)을 프리미티브로 자동 생성하는 에디터 도구.
    ///
    /// 상단 메뉴 [이문록 ▸ 조사청 씬 생성 (HubScene)] 을 누르면
    /// 방(바닥·벽·창호)과 네 구역(봉서함·사건판·기록대·도구선반), 사건 큐브 3개,
    /// 도구 5개 자리, 조명, 카메라, 디버그 도구가 배치된 씬을 만들어
    /// Assets/_Project/Scenes/Core/HubScene.unity 로 저장한다.
    ///
    /// ★ 이 도구는 단계가 진행될수록 확장된다(사건판 로직·기록대·창호 연출 등).
    ///   씬을 손으로 수정하기보다, 이 생성기를 다시 실행해 최신 상태를 얻는 것을 권장.
    ///   (즉, HubScene을 손으로 크게 고치면 재생성 시 사라질 수 있음 — 로직은 스크립트로.)
    /// </summary>
    public static class HubSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Core/HubScene.unity";

        // 방 크기(한 변의 절반). 3.5 → 약 7m x 7m 방.
        private const float Half = 3.5f;
        private const float WallHeight = 3f;

        [MenuItem("이문록/조사청 씬 생성 (HubScene)")]
        public static void BuildHubScene()
        {
            // 저장되지 않은 현재 씬이 있으면 물어본다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // 빈 씬에서 시작
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();   // 바닥·벽·창호·조명·카메라
            BuildBongseoBox();    // 봉서함
            BuildCaseBoard();     // 사건판 + 사건 큐브 3개
            BuildRecordStand();   // 기록대
            BuildToolShelf();     // 도구선반 + 도구 5개
            BuildDebugHelper();   // 키보드 디버그 도구

            // 저장
            EnsureFolder("Assets/_Project/Scenes/Core");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"[HubSceneBuilder] 조사청 씬 생성 완료 → {ScenePath}");
            EditorUtility.DisplayDialog("이문록", "조사청(HubScene) 생성 완료!\n" + ScenePath, "확인");
        }

        // ─────────────────────────────────────────────
        //  방: 바닥·벽·창호·조명·카메라
        // ─────────────────────────────────────────────
        private static void BuildEnvironment()
        {
            var root = new GameObject("Environment");

            // 바닥(Plane 기본 10x10 → scale로 방 크기에 맞춤)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform);
            floor.transform.localScale = new Vector3(Half * 2f / 10f, 1f, Half * 2f / 10f);

            // 벽 4개(얇은 큐브). 북/남/동/서
            CreateWall(root.transform, "Wall_North", new Vector3(0, WallHeight / 2f, Half), new Vector3(Half * 2f, WallHeight, 0.2f));
            CreateWall(root.transform, "Wall_South", new Vector3(0, WallHeight / 2f, -Half), new Vector3(Half * 2f, WallHeight, 0.2f));
            CreateWall(root.transform, "Wall_East", new Vector3(Half, WallHeight / 2f, 0), new Vector3(0.2f, WallHeight, Half * 2f));
            CreateWall(root.transform, "Wall_West", new Vector3(-Half, WallHeight / 2f, 0), new Vector3(0.2f, WallHeight, Half * 2f));

            // 창호: 북쪽 벽에 낸 창(Quad). 5단계에서 어둠→밝음 전환의 대상.
            var window = GameObject.CreatePrimitive(PrimitiveType.Quad);
            window.name = "Window_Changho";
            window.transform.SetParent(root.transform);
            window.transform.position = new Vector3(0, 1.6f, Half - 0.11f); // 북벽 살짝 안쪽
            window.transform.rotation = Quaternion.Euler(0, 180, 0);        // 방 안쪽을 향하게
            window.transform.localScale = new Vector3(2.4f, 1.6f, 1f);

            // 조명: 도입부의 "바깥이 없는" 어두운 분위기. 낮은 강도로 시작(5단계에서 밝힘).
            var lightGO = new GameObject("Sun");
            lightGO.transform.SetParent(root.transform);
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.7f;               // 다소 어둑하지만 색은 보이게(5단계에서 더 밝힘)
            light.color = new Color(0.7f, 0.74f, 0.85f); // 차갑고 창백한 빛
            RenderSettings.ambientLight = new Color(0.18f, 0.18f, 0.22f); // 전역 앰비언트

            // 카메라: VR 전이라 비-VR로도 방을 볼 수 있게 배치(플레이어 눈높이).
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(root.transform);
            camGO.transform.position = new Vector3(0, 1.6f, -Half + 0.6f);
            camGO.transform.LookAt(new Vector3(0, 1.4f, Half)); // 사건판 쪽을 바라봄
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f); // 창밖의 어둠/안개
            camGO.AddComponent<AudioListener>();

            // 비-VR 테스트용 마우스 레이 선택기(사건 큐브 클릭 검증). VR 단계에서 컨트롤러 레이로 대체.
            camGO.AddComponent<MouseRaySelector>();

            // 비-VR 테스트용 자유 비행 카메라(RMB 누른 채 WASD로 방을 둘러봄). VR 단계에서 제거/비활성.
            camGO.AddComponent<DebugFlyCamera>();
        }

        private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
        }

        // ─────────────────────────────────────────────
        //  봉서함: 왕의 명이 도착하는 곳(시작 지점). 5단계에서 활성화 연출.
        // ─────────────────────────────────────────────
        private static void BuildBongseoBox()
        {
            var zone = new GameObject("Zone_BongseoBox");
            zone.transform.position = new Vector3(0, 0, -Half + 0.8f); // 입구/시작 쪽

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = "BongseoBox";
            pedestal.transform.SetParent(zone.transform);
            pedestal.transform.localPosition = new Vector3(0, 0.5f, 0);
            pedestal.transform.localScale = new Vector3(0.6f, 1f, 0.4f);
        }

        // ─────────────────────────────────────────────
        //  사건판: 세 사건이 문서 뭉치(큐브)로 걸린 선택 지점. 3단계에서 색/선택 로직.
        // ─────────────────────────────────────────────
        private static void BuildCaseBoard()
        {
            var zone = new GameObject("Zone_CaseBoard");
            zone.transform.position = new Vector3(0, 0, Half - 0.15f); // 북벽 앞

            // 판(배경). 얇은 큐브.
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(zone.transform);
            board.transform.localPosition = new Vector3(0, 1.5f, 0);
            board.transform.localScale = new Vector3(3.2f, 1.6f, 0.1f);

            // 사건 큐브 3개. CaseCube 컴포넌트를 붙여 CaseId·사건 씬 이름을 세팅.
            // 사건 씬 이름은 팀원 폴더명 기준 추정값(아직 씬이 없으면 선택 시 로그만 남음).
            float[] xs = { -1f, 0f, 1f };
            CaseId[] ids = { CaseId.Case1_Onggojip, CaseId.Case2_Seocheon, CaseId.Case3_Gyeonu };
            string[] sceneNames = { "Onggojip", "Seocheon", "Gyeonu" };

            for (int i = 0; i < 3; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"CaseCube_{i + 1}"; // CaseCube_1 → Case1 ...
                cube.transform.SetParent(zone.transform);
                cube.transform.localPosition = new Vector3(xs[i], 1.5f, -0.25f); // 판 앞으로 살짝
                cube.transform.localScale = new Vector3(0.5f, 0.7f, 0.15f);

                var caseCube = cube.AddComponent<CaseCube>();
                caseCube.Initialize(ids[i], sceneNames[i]);
            }
        }

        // ─────────────────────────────────────────────
        //  기록대: 푼 사건의 판결이 쌓이는 곳. 4단계에서 스택 표시.
        // ─────────────────────────────────────────────
        private static void BuildRecordStand()
        {
            var zone = new GameObject("Zone_RecordStand");
            zone.transform.position = new Vector3(Half - 0.7f, 0, 0); // 동쪽

            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.SetParent(zone.transform);
            table.transform.localPosition = new Vector3(0, 0.45f, 0);
            table.transform.localScale = new Vector3(1.2f, 0.9f, 1.6f);

            // 판결이 쌓일 기준점(빈 오브젝트). 완료된 사건마다 이 위에 판결패가 쌓인다.
            var stackAnchor = new GameObject("StackAnchor");
            stackAnchor.transform.SetParent(zone.transform);
            stackAnchor.transform.localPosition = new Vector3(0, 0.9f, 0); // 상판 위

            // 기록대 로직: 완료 사건 수만큼 판결패를 쌓아 표시.
            var stand = zone.AddComponent<RecordStand>();
            stand.Initialize(stackAnchor.transform);
        }

        // ─────────────────────────────────────────────
        //  도구선반: 확대경·수첩·등불·유척·마패 자리.
        // ─────────────────────────────────────────────
        private static void BuildToolShelf()
        {
            var zone = new GameObject("Zone_ToolShelf");
            zone.transform.position = new Vector3(-Half + 0.5f, 0, 0); // 서쪽

            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "Shelf";
            shelf.transform.SetParent(zone.transform);
            shelf.transform.localPosition = new Vector3(0, 1.0f, 0);
            shelf.transform.localScale = new Vector3(0.4f, 1.6f, 2.4f);

            // 도구 5개 자리(작은 큐브). 실제 에셋은 나중에 교체.
            string[] tools = { "확대경", "수첩", "등불", "유척", "마패" };
            for (int i = 0; i < tools.Length; i++)
            {
                var tool = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tool.name = $"Tool_{tools[i]}";
                tool.transform.SetParent(zone.transform);
                // 선반 앞면에 세로로 나열
                tool.transform.localPosition = new Vector3(0.3f, 0.5f + i * 0.35f, -0.8f + i * 0.4f);
                tool.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
            }
        }

        // ─────────────────────────────────────────────
        //  디버그 도구: 키보드로 GameState를 조작(1단계에서 만든 것).
        // ─────────────────────────────────────────────
        private static void BuildDebugHelper()
        {
            var go = new GameObject("_DebugTester");
            go.AddComponent<GameStateDebugTester>();
        }

        // ─────────────────────────────────────────────
        //  유틸
        // ─────────────────────────────────────────────

        /// <summary>폴더가 없으면 생성(중첩 경로 지원).</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Build Settings에 씬이 없으면 추가(맨 앞에).</summary>
        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
