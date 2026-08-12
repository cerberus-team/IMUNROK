using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 손 도구 벨트 + 지도 HUD 설치:
    ///  ① Data/Tools 폴더 + 손 도구 에셋(등불·돋보기) 생성
    ///  ② 씬에 _HUD + MapView(지도) + ToolbeltHud(손 도구 전환) 배치
    /// 수첩(좌하단)·지도(우상단) 버튼은 JournalView/MapView가 스스로 그린다.
    /// 새 챕터: ToolbeltHud 목록에 도구 추가, MapView ▸ Map Image에 그 사건 지도만 넣으면 됨.
    /// </summary>
    public static class ToolbeltHudSetup
    {
        private const string DataFolder = "Assets/_Project/Data/Tools";

        [MenuItem("이문록/연출: 도구벨트·지도 HUD 설치")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Tools");

            // 손에 드는 도구만(수첩·지도는 도구 아님 → 코너 버튼)
            var tLantern = MakeTool("lantern", "등불",   "어두운 곳 밝히기 (L)");
            var tMagnify = MakeTool("magnify", "돋보기", "자세히 살펴보기");
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.GetActiveScene();
            var hud  = GameObject.Find("_HUD") ?? new GameObject("_HUD");
            if (hud.GetComponent<MapView>() == null) hud.AddComponent<MapView>();
            var belt = hud.GetComponent<ToolbeltHud>() ?? hud.AddComponent<ToolbeltHud>();

            var so = new SerializedObject(belt);
            var list = so.FindProperty("_tools");
            var tools = new ToolDef[] { tLantern, tMagnify };
            list.arraySize = tools.Length;
            for (int i = 0; i < tools.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = tools[i];
            so.ApplyModifiedProperties();

            var journal = Object.FindFirstObjectByType<JournalView>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("이문록",
                "손 도구 벨트 · 지도 HUD 설치 완료!\n\n" +
                "· 손 도구: 맨손 → 등불 → 돋보기 (Q 또는 휠로 전환)\n" +
                "· 지도: 우상단 버튼 또는 M\n" +
                "· 수첩: 좌하단 버튼 또는 J" + (journal == null ? " ⚠(씬에 JournalView 없음)" : "") + "\n" +
                "· 왼손잡이: _HUD ▸ ToolbeltHud ▸ Left Handed 체크 → 코너 좌우 반전\n\n" +
                "새 챕터: ToolbeltHud 목록에 도구 추가, MapView ▸ Map Image에 그 사건 지도만 넣으면 됨. Ctrl+S.", "확인");
        }

        private static ToolDef MakeTool(string id, string name, string tip)
        {
            string path = $"{DataFolder}/Tool_{id}.asset";
            var t = AssetDatabase.LoadAssetAtPath<ToolDef>(path);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<ToolDef>();
                t.id = id; t.displayName = name; t.tooltip = tip;
                AssetDatabase.CreateAsset(t, path);
            }
            return t;
        }
    }
}
