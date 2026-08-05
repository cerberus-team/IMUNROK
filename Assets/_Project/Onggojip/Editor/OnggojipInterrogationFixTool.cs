using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 씬의 모든 InterrogationController를 "클릭할 때만 시작"으로 바꾼다(BeginOnStart 끔).
    /// → 시작하자마자 심문창이 뜨는 문제 해결. 어디에 붙어있는지도 목록으로 알려줌
    ///   (빈 오브젝트에 남은 테스트용이 있으면 그 이름이 보임 → 지우면 됨).
    ///
    /// 메뉴: [이문록 ▸ 옹고집: 심문창 클릭시작으로 정리]. 실행 후 Ctrl+S.
    /// </summary>
    public static class OnggojipInterrogationFixTool
    {
        [MenuItem("이문록/옹고집: 심문창 클릭시작으로 정리")]
        public static void Fix()
        {
            var controllers = Object.FindObjectsByType<InterrogationController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (controllers.Length == 0)
            {
                EditorUtility.DisplayDialog("이문록", "씬에 InterrogationController가 없습니다.", "확인");
                return;
            }

            var names = new System.Text.StringBuilder();
            int fixedCount = 0;
            foreach (var c in controllers)
            {
                var so = new SerializedObject(c);
                var prop = so.FindProperty("_beginOnStart");
                if (prop != null && prop.boolValue)
                {
                    prop.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    fixedCount++;
                }
                names.AppendLine("· " + Path(c.transform) +
                    (c.GetComponent<Collider>() == null ? "  ⚠(콜라이더 없음=클릭 안됨)" : ""));
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("이문록",
                $"심문창 {controllers.Length}개 발견, {fixedCount}개를 '클릭 시작'으로 바꿨습니다.\n\n" +
                "붙어있는 위치:\n" + names +
                "\n혹시 인물 큐브가 아닌 '빈 오브젝트'에 있으면 그건 옛 테스트용 → 지우세요.\nCtrl+S 하세요.", "확인");
        }

        private static string Path(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }
    }
}
