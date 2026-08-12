using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 심문창 한지 테마 일괄 적용:
    ///  · Art/UI 의 한지 텍스처 + Art/Fonts 의 폰트(조선궁서체)를 찾아
    ///    씬의 모든 InterrogationController에 연결한다.
    /// 파일만 그 폴더에 넣고 이 메뉴를 누르면 인물마다 일일이 드래그할 필요 없음.
    /// </summary>
    public static class InterrogationThemeSetup
    {
        private const string UiFolder = "Assets/_Project/_Common/Art/Textures";
        private const string FontFolder = "Assets/_Project/_Common/Art/Fonts";

        [MenuItem("이문록/연출: 심문 한지 테마 적용")]
        public static void Apply()
        {
            Texture2D paper = FindFirst<Texture2D>(UiFolder, "t:Texture2D");
            Font font = FindFirst<Font>(FontFolder, "t:Font");

            if (paper == null && font == null)
            {
                EditorUtility.DisplayDialog("이문록",
                    "한지/폰트를 못 찾았어요.\n\n" +
                    "· 한지 PNG → " + UiFolder + "\n" +
                    "· 폰트(.ttf/.otf) → " + FontFolder + "\n\n" +
                    "에 넣고 다시 실행하세요.", "확인");
                return;
            }

            var controllers = Object.FindObjectsByType<InterrogationController>(FindObjectsSortMode.None);
            int n = 0;
            foreach (var c in controllers)
            {
                var so = new SerializedObject(c);
                if (paper != null) so.FindProperty("_paperTex").objectReferenceValue = paper;
                if (font != null) so.FindProperty("_font").objectReferenceValue = font;
                so.FindProperty("_paperAlpha").floatValue = 0.5f;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(c);
                n++;
            }

            // 수첩(JournalView)에도 같은 테마 적용
            int jn = 0;
            foreach (var j in Object.FindObjectsByType<JournalView>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(j);
                if (paper != null) so.FindProperty("_paperTex").objectReferenceValue = paper;
                if (font != null) so.FindProperty("_font").objectReferenceValue = font;
                so.FindProperty("_paperAlpha").floatValue = 1f;   // 수첩은 불투명(책처럼)
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(j);
                jn++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("이문록",
                "심문 한지 테마 적용 완료!\n\n" +
                "· 한지: " + (paper != null ? paper.name : "(없음)") + "\n" +
                "· 폰트: " + (font != null ? font.name : "(없음)") + "\n" +
                "· 적용된 심문 인물: " + n + "명, 수첩: " + jn + "개\n\n" +
                "투명도는 각 인물 ▸ InterrogationController ▸ Paper Alpha로 조절. Ctrl+S.", "확인");
        }

        private static T FindFirst<T>(string folder, string filter) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            var guids = AssetDatabase.FindAssets(filter, new[] { folder });
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
