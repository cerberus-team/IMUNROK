using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 조사청 지붕을 <b>원래 있던 기와로</b> 간다.
    /// 메뉴: [이문록 ▸ 조사청 ▸ 지붕을 기와로 갈기]
    ///
    /// <b>왜 이것이 맞나</b>: 조사청은 소쇄원의 광풍각 자리에, 광풍각의 자리와 방향을
    /// 그대로 받아 지었다(<see cref="HallBuilder"/>). 몸통을 상자로 다시 지으면서
    /// 지붕까지 상자로 얹었는데, 한옥에서 지붕은 <b>제일 눈에 띄는 곳</b>이다 —
    /// 처마의 휨과 기와의 골이 없으면 아무리 아래를 잘 지어도 창고가 된다.
    /// 광풍각의 기와는 이미 이 자리에 맞게 만들어져 있으므로, 그것을 도로 얹는 것이
    /// 새로 빚는 것보다 낫고 원본과도 어긋나지 않는다.
    ///
    /// 상자 지붕은 <b>지우지 않고 끈다</b>. 기와가 안 맞으면 도로 켜면 된다.
    ///
    /// ★플레이를 멈추고 실행할 것.
    /// </summary>
    public static class HubRoof
    {
        private const string RoomName = "조사청_실내";
        private const string RootName = "지붕_기와";
        private const string Dir = "Assets/Soswaewon/Prefabs/Structure/";

        /// <summary>광풍각 지붕 두 짝 — 아래채와 위채.</summary>
        private static readonly string[] Pieces = { "SM_GPG_Roof01a", "SM_GPG_Roof02a" };

        [MenuItem("이문록/조사청/지붕을 기와로 갈기")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[지붕] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var room = GameObject.Find(RoomName);
            if (room == null) { Debug.LogError("[지붕] " + RoomName + " 을 못 찾았습니다."); return; }

            var log = new StringBuilder();

            var old = GameObject.Find(RoomName + "/" + RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "지붕을 기와로");
            root.transform.SetParent(room.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            long tris = 0;
            int put = 0;
            foreach (var name in Pieces)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + name + ".prefab");
                if (prefab == null) { log.AppendLine("   ✘ " + name + " 없음"); continue; }

                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                g.name = name;
                // 광풍각 자리에 그대로 얹는다 — 방이 이미 광풍각의 자리와 방향을 쓰고 있으므로
                // 로컬 원점에 두면 원본과 같은 자리에 앉는다.
                g.transform.localPosition = Vector3.zero;
                g.transform.localRotation = Quaternion.identity;
                g.transform.localScale = Vector3.one;
                GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);

                foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf.sharedMesh; if (m == null) continue;
                    for (int s = 0; s < m.subMeshCount; s++) tris += (long)m.GetIndexCount(s) / 3;
                    // 처마 밑으로 걸어 들어가되 지붕을 뚫고 나가지는 않게
                    if (mf.GetComponent<Collider>() == null)
                    {
                        var mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = m;
                    }
                }
                put++;
                log.AppendLine("   " + name + " 를 얹었습니다");
            }

            // 상자 지붕은 끈다 — 지우지 않는다. 기와가 안 맞으면 도로 켜면 된다.
            var box = room.transform.Find("구조/지붕");
            if (box != null)
            {
                Undo.RecordObject(box.gameObject, "상자 지붕 끄기");
                box.gameObject.SetActive(false);
                log.AppendLine("   상자 지붕(구조/지붕)은 껐습니다 — 지우지 않았으니 되돌릴 수 있습니다");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(room.scene);
            Selection.activeGameObject = root;
            Debug.Log("[지붕] 기와 " + put + "짝 · " + tris.ToString("N0") + " 삼각형\n" + log);
        }
    }
}
