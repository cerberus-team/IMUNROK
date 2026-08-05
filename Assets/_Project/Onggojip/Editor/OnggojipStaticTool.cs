using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 고택의 "안 움직이는" 부분을 Static(배칭·오클루전)으로 표시한다.
    ///  → 정적 배칭으로 드로우콜(Batches) 확 줄고, Occlusion Culling 대상이 됨.
    /// 움직이는 것(대문·게임요소=인물/단서·카메라)은 제외한다.
    ///
    /// 실행 후: Window ▸ Rendering ▸ Occlusion Culling ▸ Bake 로 오클루전 구우면 완성.
    /// 메뉴: [이문록 ▸ 옹고집: 고택 Static 표시(배칭·오클루전)].
    /// </summary>
    public static class OnggojipStaticTool
    {
        [MenuItem("이문록/옹고집: 고택 Static 표시(배칭·오클루전)")]
        public static void MarkStatic()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var flags = StaticEditorFlags.BatchingStatic
                      | StaticEditorFlags.OccluderStatic
                      | StaticEditorFlags.OccludeeStatic;

            int marked = 0, skipped = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var go = r.gameObject;
                    if (IsMover(go)) { skipped++; continue; }   // 움직이는 것 제외
                    GameObjectUtility.SetStaticEditorFlags(go, flags);
                    marked++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("이문록",
                $"Static 표시 완료!\n· 표시: {marked}개  · 제외(움직임): {skipped}개\n\n" +
                "다음: Window ▸ Rendering ▸ Occlusion Culling ▸ Bake 탭 ▸ Bake\n" +
                "그 뒤 Ctrl+S. Play해서 Stats의 Batches가 줄었는지 확인하세요.", "확인");
        }

        /// <summary>대문·게임요소·카메라 밑이면 움직이는 것 → Static 제외.</summary>
        private static bool IsMover(GameObject go)
        {
            var t = go.transform;
            while (t != null)
            {
                string n = t.name;
                if (n.Contains("대문") || n.Contains("게임요소") ||
                    n.Contains("Camera") || n.Contains("카메라") ||
                    n.StartsWith("임시_") || n.Contains("Light"))
                    return true;
                t = t.parent;
            }
            return false;
        }
    }
}
