using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 복명(EndingScene)을 자동 생성하는 에디터 도구.
    /// 상단 메뉴 [이문록 ▸ 복명 씬 생성 (EndingScene)] 을 누르면
    /// 카메라 + EndingController 만 있는 단출한 씬을 만들어
    /// Assets/_Project/Scenes/Core/EndingScene.unity 로 저장하고 Build Settings에 등록한다.
    ///
    /// (자막은 EndingController가 OnGUI로 그리므로 별도 UI 캔버스가 필요 없다.)
    /// </summary>
    public static class EndingSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Core/EndingScene.unity";

        [MenuItem("이문록/복명 씬 생성 (EndingScene)")]
        public static void BuildEndingScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 카메라(어두운 배경) — 왕은 모델 없이 목소리/자막만이므로 빈 어둠으로 둔다.
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            AtmosphereSetup.ApplyDarkSkybox(cam); // 360 배경
            camGO.AddComponent<AudioListener>();

            // 엔딩 진행 컨트롤러
            var go = new GameObject("_EndingController");
            go.AddComponent<EndingController>();

            EnsureFolder("Assets/_Project/Scenes/Core");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"[EndingSceneBuilder] 복명 씬 생성 완료 → {ScenePath}");
            EditorUtility.DisplayDialog("이문록", "복명(EndingScene) 생성 완료!\n" + ScenePath, "확인");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
