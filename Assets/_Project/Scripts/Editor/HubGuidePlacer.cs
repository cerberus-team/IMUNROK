using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>조사청에 들어선 사람에게 길을 일러 줄 것을 놓는다.</b>
    /// 메뉴: [이문록 ▸ 조사청 ▸ 들어선 자리에 길잡이 두기]
    ///
    /// 조사청에는 여태 안내가 하나도 없었다 — 소쩍새도 안 놓여 있고, 도착했을 때
    /// 여기가 어디인지 말해 주는 것도 없었다. 어전에서 봉서를 맡으면 화면이 한 번
    /// 캄캄해졌다가 낯선 마당 한복판에서 뜨는 것이 전부였고, 사건 문서 셋은 실내에
    /// 있어 들어선 방향에 따라 등 뒤에 놓였다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class HubGuidePlacer
    {
        private const string ScenePath = "Assets/_Project/Scenes/Core/HubScene.unity";

        [MenuItem("이문록/조사청/들어선 자리에 길잡이 두기")]
        public static void Place()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[조사청] 재생을 멈추고 다시 누르십시오 — 재생 중에는 씬을 못 고칩니다.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var log = new System.Text.StringBuilder("[조사청] 길잡이\n");

            GameObject host = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "_WorldState") host = root;
            if (host == null)
            {
                host = new GameObject("_길잡이");
                UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(host, scene);
                log.AppendLine("── 담을 데가 없어 '_길잡이' 를 새로 세웠다");
            }

            var arrival = host.GetComponent<HubArrival>();
            if (arrival == null)
            {
                arrival = Undo.AddComponent<HubArrival>(host);
                log.AppendLine("── 들어선 자리 안내를 '" + host.name + "' 에 붙였다");
            }
            else log.AppendLine("── 들어선 자리 안내는 이미 있다");

            int cubes = Object.FindObjectsByType<CaseCube>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            log.AppendLine("── 가리킬 사건 문서 " + cubes + "개");
            if (cubes == 0)
                log.AppendLine("   ⚠ 하나도 없다 — 가리킬 것이 없으면 안내는 아무 말도 안 한다");

            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(log.ToString());
        }
    }
}
