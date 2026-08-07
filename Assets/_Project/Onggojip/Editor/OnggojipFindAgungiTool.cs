using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 씬에 배치된 아궁이(Fireplace)·가마솥(Gamasot)을 모두 찾아
    /// Hierarchy에서 선택 + Scene뷰에서 프레임 + 위치를 Console에 출력한다.
    ///  → "아궁이가 어디 있지?"를 눈으로 바로 확인. 바깥사랑채 근처 것을 고르면 됨.
    ///
    /// 메뉴: [이문록 ▸ 옹고집: 아궁이·가마솥 위치 찾기].
    /// </summary>
    public static class OnggojipFindAgungiTool
    {
        [MenuItem("이문록/옹고집: 아궁이·가마솥 위치 찾기")]
        public static void Find()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var found = new List<GameObject>();
            var sb = new StringBuilder();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (src == null) continue;
                    if (PrefabUtility.GetNearestPrefabInstanceRoot(go) != go) continue; // 인스턴스 루트만

                    string path = AssetDatabase.GetAssetPath(src);
                    bool isAgungi = path.Contains("Fireplace");
                    bool isGamasot = path.Contains("Gamasot");
                    if (!isAgungi && !isGamasot) continue;

                    found.Add(go);
                    Vector3 p = go.transform.position;
                    sb.AppendLine($"{(isAgungi ? "🔥아궁이" : "🍲가마솥")}  {go.name}  pos=({p.x:0.0}, {p.y:0.0}, {p.z:0.0})");
                }
            }

            if (found.Count == 0)
            {
                EditorUtility.DisplayDialog("이문록", "아궁이·가마솥을 못 찾았습니다. (고택이 씬에 있는지 확인)", "확인");
                return;
            }

            Selection.objects = found.ToArray();          // Hierarchy에 전부 선택
            SceneView.FrameLastActiveSceneView();          // Scene뷰가 선택된 것들로 이동
            Debug.Log($"[아궁이 찾기] {found.Count}개 발견:\n{sb}");
            EditorUtility.DisplayDialog("이문록",
                $"아궁이·가마솥 {found.Count}개를 찾아 선택했어요!\n\n" +
                "· Scene뷰가 그쪽으로 이동했어요(안 보이면 F 키)\n" +
                "· Hierarchy에 파랗게 선택돼 있어요 → 하나씩 클릭하며 확인\n" +
                "· 위치 좌표는 Console에 출력됨\n\n" +
                "바깥사랑채(#4) 근처에 있는 아궁이가 J13(탄 서찰) 놓을 곳이에요.", "확인");
        }
    }
}
