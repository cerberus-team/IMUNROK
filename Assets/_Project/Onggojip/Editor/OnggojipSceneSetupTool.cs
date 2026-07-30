using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 현재 옹고집 씬을 "돌아가게" 셋업하는 도구. (집을 지우지 않고 필요한 것만 추가/정리)
    ///  ① 수첩 UI(JournalView)가 없으면 추가 → J키로 수첩이 열림.
    ///  ② '게임요소' 그룹의 마커(플레이어·단서·인물)를 바로 아래 바닥에 딱 붙임(공중에 안 뜨게).
    ///
    /// 바닥 판정은 아래로 레이캐스트 → Collider가 있는 바닥/기단/마당에 붙는다.
    /// (Collider가 없는 곳은 못 붙이고 '실패'로 보고 → 그 마커는 손으로 내려주면 됨)
    ///
    /// 메뉴: [이문록 ▸ 옹고집: 현재 씬 셋업(수첩+바닥붙이기)]. 실행 후 Ctrl+S.
    /// </summary>
    public static class OnggojipSceneSetupTool
    {
        [MenuItem("이문록/옹고집: 현재 씬 셋업(수첩+바닥붙이기)")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();

            // ① 수첩(JournalView) 보장
            bool addedJournal = false;
            if (Object.FindObjectOfType<JournalView>() == null)
            {
                var go = new GameObject("수첩(JournalView)");
                go.AddComponent<JournalView>();
                addedJournal = true;
            }

            // ② 게임요소 바닥에 붙이기
            int snapped = 0, missed = 0;
            var group = FindRoot(scene, "게임요소");
            if (group != null)
            {
                foreach (Transform child in group.transform)
                {
                    if (SnapToFloor(child)) snapped++;
                    else missed++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("이문록",
                (addedJournal ? "· 수첩(JournalView) 추가됨 → J키로 열림\n"
                              : "· 수첩은 이미 있음\n") +
                (group != null
                    ? $"· 바닥에 붙인 마커: {snapped}개" + (missed > 0 ? $" (실패 {missed}개: 그 아래 Collider 없음 → 손으로 내려주세요)" : "")
                    : "· '게임요소' 그룹이 없어 마커 정리는 건너뜀") +
                "\n\nCtrl+S로 저장하세요.", "확인");
        }

        /// <summary>마커를 바로 아래 바닥에 붙임(마커 밑면이 바닥에 닿게). 성공 여부 반환.</summary>
        private static bool SnapToFloor(Transform t)
        {
            var col = t.GetComponent<Collider>();
            bool hadCol = col != null && col.enabled;
            if (col != null) col.enabled = false; // 자기 자신에 안 맞도록 잠시 끔

            bool ok = false;
            Vector3 origin = t.position + Vector3.up * 20f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                var rend = t.GetComponent<Renderer>();
                float half = rend != null ? rend.bounds.extents.y : 0.5f;
                Vector3 p = t.position;
                p.y = hit.point.y + half;   // 밑면이 바닥에 닿게
                t.position = p;
                ok = true;
            }

            if (col != null) col.enabled = hadCol;
            return ok;
        }

        private static GameObject FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }
    }
}
