using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 새 문갑(kcdf-mungap_03.glb)만 씬에 넣어 문갑 마커 자리에 놓는다.
    /// 문짝이 따로 있으면(움직이는 장) 이후 DoorController로 여닫게 연결.
    /// 메뉴: [이문록 ▸ 옹고집: 문갑만 배치].
    /// </summary>
    public static class OnggojipPlaceMungapTool
    {
        private const string MungapPath = "Assets/_Project/Onggojip/Art/Models/kcdf-mungap_03.glb";

        [MenuItem("이문록/옹고집: 문갑만 배치")]
        public static void Place()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MungapPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("이문록",
                    "문갑을 못 불러왔어요:\n" + MungapPath +
                    "\n\nglTFast 임포트가 끝났는지, 파일명이 맞는지 확인하세요.", "확인");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            var anchor = Find(scene, "문갑");   // 문갑 마커(큐브)

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.name = "문갑(소품)";
            go.transform.position = anchor != null ? anchor.transform.position : Vector3.zero;

            Selection.activeGameObject = go;
            SceneView.FrameLastActiveSceneView();
            EditorSceneManager.MarkSceneDirty(scene);

            EditorUtility.DisplayDialog("이문록",
                "문갑 배치 완료!\n" +
                (anchor != null ? "문갑 마커 자리에 놓았어요." : "⚠ 문갑 마커를 못 찾아 원점(0,0,0)에 뒀어요. 드래그로 옮기세요.") +
                "\n\n다음: '문갑(소품)'을 Hierarchy에서 펼쳐서(▶) 문짝/서랍 이름을 보고 알려주면,\nDoorController로 여닫게 연결해줄게요.", "확인");
        }

        private static GameObject Find(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    if (tr.name == name) return tr.gameObject;
            return null;
        }
    }
}
