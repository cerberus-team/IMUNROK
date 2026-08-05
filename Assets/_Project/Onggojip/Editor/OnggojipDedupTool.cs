using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 씬에서 "완전히 겹친 중복 오브젝트"만 안전하게 제거한다.
    /// 같은 프리팹(고택 부품)이 같은 위치·회전에 두 개 이상 있으면 하나만 남기고 삭제.
    ///  - 고택(KimMyeonggwanHouse) 프리팹 인스턴스만 대상(게임요소·대문·카메라 등 내 작업은 안 건드림)
    ///  - 위치가 조금이라도 다르면 안 지움(진짜 겹친 것만)
    /// 실행 시 먼저 개수만 알려주고, 확인해야 삭제. 실행 후 Ctrl+S.
    /// 메뉴: [이문록 ▸ 옹고집: 씬 중복 오브젝트 정리].
    /// </summary>
    public static class OnggojipDedupTool
    {
        [MenuItem("이문록/옹고집: 씬 중복 오브젝트 정리")]
        public static void Dedup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var seen = new HashSet<string>();
            var dupes = new List<GameObject>();

            foreach (var root in scene.GetRootGameObjects())
            {
                CollectDupes(root, seen, dupes);
            }

            if (dupes.Count == 0)
            {
                EditorUtility.DisplayDialog("이문록",
                    "완전히 겹친 중복은 없습니다.\n\n" +
                    "(만약 두 겹인데 위치가 살짝 달라 안 잡히면, 채팅으로 알려주세요 — 다른 방법 안내할게요.)", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog("이문록",
                    $"완전히 겹친 중복 오브젝트 {dupes.Count}개를 발견했습니다.\n" +
                    "하나만 남기고 나머지를 삭제할까요? (고택 부품만, 내 작업물은 안 건드림)", "삭제", "취소"))
                return;

            int n = dupes.Count;
            foreach (var go in dupes) Object.DestroyImmediate(go);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("이문록",
                $"중복 {n}개 삭제 완료!\nCtrl+S로 저장하세요.\n\n" +
                "(Game 뷰 Stats 창에서 Tris/Batches가 줄었는지 확인해보세요.)", "확인");
        }

        private static void CollectDupes(GameObject root, HashSet<string> seen, List<GameObject> dupes)
        {
            // 프리팹 인스턴스 루트만 취급
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src == null) continue;

                // 프리팹 인스턴스의 "루트"만(중복 판정 단위)
                if (PrefabUtility.GetNearestPrefabInstanceRoot(go) != go) continue;

                string path = AssetDatabase.GetAssetPath(src);
                if (string.IsNullOrEmpty(path) || !path.Contains("KimMyeonggwanHouse")) continue;

                Vector3 p = go.transform.position;
                Vector3 r = go.transform.eulerAngles;
                string key = $"{path}|{Round(p.x)},{Round(p.y)},{Round(p.z)}|{Round(r.x)},{Round(r.y)},{Round(r.z)}";

                if (!seen.Add(key)) dupes.Add(go);   // 이미 본 것 = 중복
            }
        }

        private static int Round(float v) => Mathf.RoundToInt(v * 100f); // 0.01 단위
    }
}
