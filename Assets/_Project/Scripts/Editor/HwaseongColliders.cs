using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>화성행궁 조각의 빈 콜라이더를 채운다.</b>
    ///
    /// 이 꾸러미의 프리팹은 뿌리에 <c>MeshCollider</c> 를 하나 달아 두고 <b>그물은 안 꽂아</b>
    /// 놓았다. 그림은 자식(LOD0~3)이 그리고 뿌리에는 <c>MeshFilter</c> 가 없으니, 콜라이더는
    /// 낀 채로 아무것도 막지 않는다 — 인스펙터에는 콜라이더가 있으므로 <b>있는 줄 알고</b>
    /// 넘어가게 된다.
    ///
    /// 실제로 관아의 <b>담 스물아홉 조각이 전부 그랬다</b>. 어사가 담을 그냥 걸어 통과했고,
    /// 계단 프리팹도 마찬가지라 놓아 봐야 밟히지 않았다. 「계단을 손으로 지어 붙인」 까닭이
    /// 이것이었는데, 지어 붙일 일이 아니라 <b>꽂아 줄</b> 일이었다.
    ///
    /// 채우는 차례:
    ///   ① 같은 fbx 안에 <c>_ConvexHulls</c> 가 있으면 그것(꾸러미가 건물용으로 따로 구운 것)
    ///   ② 없으면 <c>_LOD0</c> — 계단의 단 높이까지 살아 있어야 밟고 오른다
    ///
    /// 이미 그물이 꽂힌 콜라이더는 건드리지 않는다.
    /// </summary>
    public static class HwaseongColliders
    {
        private const string MenuPath = "Tools/異聞錄/화성행궁 조각에 콜라이더 채우기";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            int filled = 0, already = 0, cannot = 0;
            var log = new System.Text.StringBuilder();

            foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mc.sharedMesh != null) { already++; continue; }

                var mesh = PickMesh(mc.gameObject);
                if (mesh == null)
                {
                    cannot++;
                    log.AppendLine("  ? " + Path(mc.transform) + " — 꽂을 그물을 못 찾았다");
                    continue;
                }
                Undo.RecordObject(mc, "콜라이더 채우기");
                mc.sharedMesh = mesh;
                EditorUtility.SetDirty(mc);
                filled++;
                log.AppendLine("  + " + Path(mc.transform) + " ← " + mesh.name);
            }

            if (filled > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[콜라이더] 채움 " + filled + " · 이미 있던 것 " + already + " · 못 채운 것 " + cannot
                      + (filled > 0 ? "\n" + log : ""));
        }

        /// <summary>
        /// 이 조각이 부딪는 몸으로 쓸 그물. 제 <c>MeshFilter</c> → 자식의 <c>_ConvexHulls</c>
        /// → 자식의 <c>_LOD0</c> → 아무 자식 그물 차례로 찾는다.
        /// </summary>
        private static Mesh PickMesh(GameObject go)
        {
            var own = go.GetComponent<MeshFilter>();
            if (own != null && own.sharedMesh != null) return own.sharedMesh;

            var kids = new List<MeshFilter>(go.GetComponentsInChildren<MeshFilter>(true));
            if (kids.Count == 0) return null;

            // 같은 fbx 안에 따로 구운 부딪는 몸이 있으면 그것이 가장 낫다.
            foreach (var mf in kids)
            {
                if (mf.sharedMesh == null) continue;
                string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (string.IsNullOrEmpty(path)) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var m = o as Mesh;
                    if (m != null && m.name.EndsWith("_ConvexHulls")) return m;
                }
                break;      // 한 조각은 한 fbx 에서 온다 — 한 번만 훑으면 된다
            }

            foreach (var mf in kids)
                if (mf.sharedMesh != null && mf.sharedMesh.name.EndsWith("_LOD0")) return mf.sharedMesh;

            foreach (var mf in kids)
                if (mf.sharedMesh != null) return mf.sharedMesh;

            return null;
        }

        private static string Path(Transform t)
        {
            string s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
