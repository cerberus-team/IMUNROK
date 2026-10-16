using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 장면 1 — 어전(IntroScene)을 프리미티브로 자동 생성하는 에디터 도구.
    /// 상단 메뉴 [이문록 ▸ 어전 씬 생성 (IntroScene)].
    ///
    /// 구성: 어두운 어전 + 왕좌 단(모델 없음) + 무릎높이 카메라 + IntroController +
    ///       세 사건 문서(종이 프리미티브). 대사 후 문서가 등장하고, 집으면 조사청으로.
    /// IntroScene을 Build Settings의 "첫 씬(index 0)"으로 등록해 게임이 여기서 시작되게 한다.
    /// </summary>
    public static class IntroSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Core/IntroScene.unity";

        [MenuItem("이문록/어전 씬 생성 (IntroScene)")]
        public static void BuildIntroScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Environment");

            // 바닥(어두운 어전)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform);
            floor.transform.localScale = new Vector3(1.2f, 1f, 1.2f);

            // 왕좌 단(모델 없음 — 상징적 단만). 왕은 목소리+자막.
            var dais = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dais.name = "ThroneDais";
            dais.transform.SetParent(root.transform);
            dais.transform.position = new Vector3(0, 0.25f, 3.2f);
            dais.transform.localScale = new Vector3(3f, 0.5f, 1.2f);

            // 조명: 어전답게 낮고 무겁게
            var lightGO = new GameObject("Sun");
            lightGO.transform.SetParent(root.transform);
            lightGO.transform.rotation = Quaternion.Euler(55, 20, 0);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.5f;
            light.color = new Color(0.75f, 0.7f, 0.6f);
            RenderSettings.ambientLight = new Color(0.12f, 0.11f, 0.12f);

            // 카메라: 무릎 꿇고 고개 숙여(부복) 바닥을 내려다보는 고정 시점.
            // 화면은 자유로 움직이지 않는다 — 어전에서는 시점이 고정이다.
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(root.transform);
            camGO.transform.position = new Vector3(0, 1.25f, -1.5f);
            camGO.transform.LookAt(new Vector3(0, 0.08f, -0.55f)); // 무릎 앞 바닥의 문서를 내려다봄
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            AtmosphereSetup.ApplyDarkSkybox(cam); // 360 배경
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<MouseRaySelector>();  // 문서 클릭. 시점은 고정.

            // 세 사건 문서: 무릎 앞 "바닥"에 눕혀 한 줄(왕이 내려놓은 형태).
            var holder = new GameObject("Documents");
            holder.transform.position = new Vector3(0, 0.03f, -0.55f);

            float[] xs = { -0.55f, 0f, 0.55f };
            CaseId[] ids = { CaseId.Case1_Onggojip, CaseId.Case2_Seocheon, CaseId.Case3_Gyeonu };

            var docs = new IntroDocument[3];
            for (int i = 0; i < 3; i++)
            {
                var doc = GameObject.CreatePrimitive(PrimitiveType.Cube);
                doc.name = $"Document_{i + 1}";
                doc.transform.SetParent(holder.transform);
                doc.transform.localPosition = new Vector3(xs[i], 0f, 0f);
                doc.transform.localScale = new Vector3(0.32f, 0.02f, 0.44f); // 납작한 종이/두루마리 느낌
                var d = doc.AddComponent<IntroDocument>();
                d.Initialize(ids[i], "HubScene");
                docs[i] = d;
            }

            // 도입 컨트롤러(대사 → 문서 등장)
            var introGO = new GameObject("_IntroController");
            var intro = introGO.AddComponent<IntroController>();
            SetDocuments(intro, docs);

            EnsureFolder("Assets/_Project/Scenes/Core");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettingsFirst(ScenePath);

            Debug.Log($"[IntroSceneBuilder] 어전 씬 생성 완료 → {ScenePath}");
            EditorUtility.DisplayDialog("이문록",
                "어전(IntroScene) 생성 완료!\n게임 시작 씬으로 등록되었습니다.\n" + ScenePath, "확인");
        }

        /// <summary>IntroController의 비공개 _documents 필드에 참조를 주입(SerializedObject 사용).</summary>
        private static void SetDocuments(IntroController intro, IntroDocument[] docs)
        {
            var so = new SerializedObject(intro);
            var prop = so.FindProperty("_documents");
            prop.arraySize = docs.Length;
            for (int i = 0; i < docs.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = docs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>어전을 Build Settings 맨 앞(첫 씬)으로 등록.</summary>
        private static void AddSceneToBuildSettingsFirst(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
