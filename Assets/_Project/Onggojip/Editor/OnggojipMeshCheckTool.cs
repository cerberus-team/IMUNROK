using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// "Failed getting triangles / Submesh index out of bounds" 원인 점검·수정.
    ///  ① 머티리얼 슬롯이 메시 서브메시보다 많은 렌더러 → 남는 슬롯을 잘라 맞춤(에러 해결).
    ///  ② 메시가 비어있는(Missing) MeshFilter → 목록만 보고(수동 처리 필요).
    ///
    /// 메뉴: [이문록 ▸ 옹고집: 메시 서브메시 문제 점검·수정]. 실행 후 Ctrl+S.
    /// </summary>
    public static class OnggojipMeshCheckTool
    {
        [MenuItem("이문록/옹고집: 메시 서브메시 문제 점검·수정")]
        public static void Check()
        {
            var scene = EditorSceneManager.GetActiveScene();
            int trimmed = 0, nullMesh = 0;
            var nullList = new List<string>();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    var mesh = mf != null ? mf.sharedMesh : null;

                    if (mesh == null)
                    {
                        nullMesh++;
                        if (nullList.Count < 15) nullList.Add(Path(mr.transform));
                        continue;
                    }

                    int sub = mesh.subMeshCount;
                    var mats = mr.sharedMaterials;
                    if (sub > 0 && mats.Length > sub)
                    {
                        var fixedMats = new Material[sub];
                        for (int i = 0; i < sub; i++) fixedMats[i] = mats[i];
                        mr.sharedMaterials = fixedMats;   // 남는 머티리얼 슬롯 제거
                        trimmed++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);

            string msg = $"점검 완료!\n· 머티리얼 슬롯 잘라 고침: {trimmed}개 (← 이 에러의 원인)\n" +
                         $"· 메시 비어있음(Missing): {nullMesh}개";
            if (nullList.Count > 0)
                msg += "\n\n(Missing 예시)\n" + string.Join("\n", nullList);
            msg += "\n\nCtrl+S 후 Occlusion을 다시 Bake 해보세요.";

            Debug.Log("[MeshCheck] trimmed=" + trimmed + ", nullMesh=" + nullMesh);
            EditorUtility.DisplayDialog("이문록", msg, "확인");
        }

        private static string Path(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }
    }
}
