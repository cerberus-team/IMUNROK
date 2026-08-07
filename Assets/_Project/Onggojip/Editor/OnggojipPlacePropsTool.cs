using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 임포트한 소품 6개를 씬에 넣고, 문갑 마커 근처에 모아 배치한다(이름도 깔끔하게).
    /// 정확한 방 안 위치는 사용자가 Scene뷰에서 드래그로 마무리.
    /// glTFast(glb)·FBX 임포트가 끝나 있어야 함.
    /// 메뉴: [이문록 ▸ 옹고집: 소품 6개 씬에 넣기].
    /// </summary>
    public static class OnggojipPlacePropsTool
    {
        private const string Dir = "Assets/_Project/Onggojip/Art/Models/";

        private struct Prop { public string path; public string name; public Vector3 offset; }

        [MenuItem("이문록/옹고집: 소품 6개 씬에 넣기")]
        public static void Place()
        {
            var scene = EditorSceneManager.GetActiveScene();

            // 기준점 = 문갑 마커(있으면), 없으면 원점
            var anchor = Find(scene, "문갑");
            Vector3 basePos = anchor != null ? anchor.transform.position : Vector3.zero;

            var props = new[]
            {
                new Prop { path = Dir + "kcdf-mungap-03/source/Table04_Key.fbx", name = "문갑",     offset = new Vector3(0, 0, 0) },
                new Prop { path = Dir + "unhyun__quilted_mattress.glb",          name = "이부자리", offset = new Vector3(2, 0, 0) },
                new Prop { path = Dir + "unhyun__floor_cushion.glb",             name = "방석",     offset = new Vector3(2, 0, 1) },
                new Prop { path = Dir + "unhyun__tongyeong_table.glb",           name = "소반",     offset = new Vector3(3, 0, 0) },
                new Prop { path = Dir + "unhyun__wardrobe.glb",                  name = "장롱",     offset = new Vector3(-2, 0, 0) },
                new Prop { path = Dir + "kcdf-jangl3_03.glb",                    name = "장",       offset = new Vector3(-3, 0, 0) },
            };

            var group = new GameObject("소품(배치조정)");
            group.transform.position = basePos;

            int ok = 0;
            var missing = new System.Text.StringBuilder();
            foreach (var p in props)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p.path);
                if (asset == null)
                {
                    missing.AppendLine("· " + p.name + "  (" + p.path + ")");
                    continue;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                go.name = p.name;
                go.transform.SetParent(group.transform, false);
                go.transform.localPosition = p.offset;
                ok++;
            }

            Selection.activeGameObject = group;
            SceneView.FrameLastActiveSceneView();
            EditorSceneManager.MarkSceneDirty(scene);

            string msg = $"소품 {ok}개를 씬에 넣었어요 ('소품(배치조정)' 그룹).\n" +
                (anchor != null ? "문갑 마커 근처에 모아뒀어요." : "⚠ 문갑 마커를 못 찾아 원점(0,0,0)에 뒀어요.") +
                "\n\n이제 각 소품을 방 안 자리로 드래그하세요.\n" +
                "(문갑=바깥사랑채, 이부자리=침방, 방석·소반=분위기 등)";
            if (missing.Length > 0)
                msg += "\n\n못 불러온 것(임포트 확인 필요):\n" + missing;
            EditorUtility.DisplayDialog("이문록", msg, "확인");
        }

        private static GameObject Find(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var t = root.transform.Find(name);
                if (t != null) return t.gameObject;
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    if (tr.name == name) return tr.gameObject;
            }
            return null;
        }
    }
}
