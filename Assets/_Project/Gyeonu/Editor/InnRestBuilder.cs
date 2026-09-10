using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// 주막 「쉬기」 자리 (2026-09-10). 마을 씬 주막 마당 평상(Low_Wooden_Bench)의 북서쪽 빈 모서리에
    /// 트리거 부피 하나와 <see cref="InnRest"/> 를 놓고 저장한다. 평상·가구는 옮기지 않는다. 멱등.
    /// </summary>
    public static class InnRestBuilder
    {
        const string VillageScene = "Assets/_Project/Gyeonu/Scenes/Gyeonu.unity";
        const string Parent = "성하리_집터/Plot_10_Tavern";

        [MenuItem("Tools/이문록/마을 ▸ 주막 쉬기 자리 만들기", priority = 351)]
        public static void Build()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path != VillageScene)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                active = EditorSceneManager.OpenScene(VillageScene, OpenSceneMode.Single);
            }

            var go = GameObject.Find(InnRest.SpotName);
            if (go == null)
            {
                go = new GameObject(InnRest.SpotName);
                Undo.RegisterCreatedObjectUndo(go, "주막 쉬기 자리");
                var parent = GameObject.Find(Parent);
                if (parent != null) go.transform.SetParent(parent.transform, true);
            }
            go.transform.position = InnRest.SpotPosition;
            go.transform.rotation = Quaternion.identity;

            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = InnRest.SpotSize;

            var rest = go.GetComponent<InnRest>();
            if (rest == null) rest = Undo.AddComponent<InnRest>(go);
            rest.displayName = "주막 평상";
            EditorUtility.SetDirty(go);

            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            Debug.Log("[주막쉬기] " + InnRest.SpotName + " 을 " + InnRest.SpotPosition + " 에 놓고 저장했다.");
        }
    }
}
