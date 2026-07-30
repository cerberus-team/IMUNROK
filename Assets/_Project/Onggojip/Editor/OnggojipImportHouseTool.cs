using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 김명관(정읍) 고택 에셋의 완성 씬(Demo.unity)에 조립돼 있는 집 전체를,
    /// 지금 열려 있는 옹고집 씬(Onggojip 1부)으로 "통째로 합쳐" 오는 도구.
    ///
    /// 안전장치:
    ///  · 이미 '김명관고택'이 있으면 중단(중복 방지).
    ///  · try/catch로 도중 실패해도 상태를 알려줌.
    ///  · Demo의 모든 오브젝트를 '김명관고택' 하나 밑으로 확실히 묶음(통째 이동/삭제 쉽게).
    ///  · 합친 뒤 옛 프리미티브 자리표시(Zone_*, 담장(墻), 임시_*)를 자동 삭제.
    ///  · Demo.unity 원본 파일은 건드리지 않음.
    ///
    /// 실행 후 Ctrl+S. 메뉴: [이문록 ▸ 옹고집: 김명관 고택을 현재 씬에 합치기].
    /// </summary>
    public static class OnggojipImportHouseTool
    {
        private const string DemoPath =
            "Assets/_Project/Onggojip/Art/KimMyeonggwanHouse/Scene/Demo.unity";
        private const string GroupName = "김명관고택";

        [MenuItem("이문록/옹고집: 김명관 고택을 현재 씬에 합치기")]
        public static void Import()
        {
            var dest = EditorSceneManager.GetActiveScene();

            // 중복 방지
            foreach (var root in dest.GetRootGameObjects())
                if (root.name == GroupName)
                {
                    EditorUtility.DisplayDialog("이문록",
                        $"이미 '{GroupName}'가 이 씬에 있습니다.\n" +
                        "먼저 그걸 지우고(또는 씬을 새로 만든 뒤) 다시 실행하세요.", "확인");
                    return;
                }

            if (!System.IO.File.Exists(DemoPath))
            {
                EditorUtility.DisplayDialog("이문록", $"Demo 씬을 못 찾았습니다:\n{DemoPath}", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog("이문록",
                    "김명관 고택(조립 완성본)을 지금 이 씬으로 딱 한 번 합칩니다.\n" +
                    "· 집 전체가 '김명관고택' 오브젝트 밑으로 들어옵니다.\n" +
                    "· 옛 자리표시(Zone_*, 담장(墻), 임시_*)는 자동 삭제됩니다.\n" +
                    "· Demo.unity 원본은 안 건드립니다.\n\n계속할까요?", "합치기", "취소"))
                return;

            Scene demo = default;
            try
            {
                demo = EditorSceneManager.OpenScene(DemoPath, OpenSceneMode.Additive);

                var group = new GameObject(GroupName);
                SceneManager.MoveGameObjectToScene(group, demo);

                int moved = 0, removedCam = 0;
                foreach (var root in demo.GetRootGameObjects())
                {
                    if (root == group) continue;
                    if (root.name == "Camera" || root.name == "Main Camera" || root.name == "Directional Light")
                    {
                        Object.DestroyImmediate(root);
                        removedCam++;
                        continue;
                    }
                    root.transform.SetParent(group.transform, true);
                    moved++;
                }

                EditorSceneManager.MergeScenes(demo, dest);

                // 옛 프리미티브 자리표시 청소
                int cleaned = CleanPlaceholders(dest);

                EditorSceneManager.MarkSceneDirty(dest);
                EditorUtility.DisplayDialog("이문록",
                    $"고택 합치기 완료!\n\n" +
                    $"· '{GroupName}' 밑으로 모은 오브젝트: {moved}개\n" +
                    $"· 지운 여분 카메라/조명: {removedCam}개\n" +
                    $"· 지운 옛 자리표시: {cleaned}개\n\n" +
                    $"이제 [바닥 높이 자동 맞추기] → Ctrl+S 하세요.", "확인");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("이문록",
                    "합치는 중 오류가 났습니다:\n" + e.Message +
                    "\n\n씬을 새로 생성(리셋)한 뒤 다시 시도하세요.", "확인");
                Debug.LogException(e);
            }
        }

        /// <summary>Zone_*, 담장(墻), 임시_* 같은 옛 프리미티브 자리표시를 삭제.</summary>
        private static int CleanPlaceholders(Scene scene)
        {
            var kill = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == GroupName) continue;
                if (root.name.StartsWith("Zone_") ||
                    root.name.StartsWith("임시_") ||
                    root.name == "담장(墻)")
                    kill.Add(root);
            }
            foreach (var go in kill) Object.DestroyImmediate(go);
            return kill.Count;
        }
    }
}
