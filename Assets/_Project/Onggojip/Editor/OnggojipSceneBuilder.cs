using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IMUNROK.Onggojip;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 제1사건 흐름 테스트용 "Onggojip" 씬 생성기.
    /// 메뉴: [이문록 ▸ 옹고집 사건 씬 생성 (Onggojip)].
    /// 이 씬 이름이 "Onggojip"이라, 조사청에서 제1사건 큐브를 선택하면 자동 로드된다.
    ///
    /// 지금은 로직 뼈대(OnggojipCase 디버그 패널)만. 이후 방·인물·상호작용을 이 위에 얹는다.
    /// </summary>
    public static class OnggojipSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Onggojip/Scenes/Onggojip.unity";

        [MenuItem("이문록/옹고집 사건 씬 생성 (Onggojip)")]
        public static void BuildOnggojipScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
            camGO.AddComponent<AudioListener>();

            var go = new GameObject("_OnggojipCase");
            go.AddComponent<OnggojipCase>();

            EnsureFolder("Assets/_Project/Onggojip/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"[OnggojipSceneBuilder] 옹고집 사건 씬 생성 완료 → {ScenePath}");
            EditorUtility.DisplayDialog("이문록",
                "옹고집 사건 씬 생성 완료!\n" + ScenePath +
                "\n\n조사청에서 제1사건 큐브를 누르면 이 씬이 로드됩니다.", "확인");
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
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
