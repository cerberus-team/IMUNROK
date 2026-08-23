using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 조사청에 <b>마당과 담장</b>을 두른다. 메뉴: [이문록 ▸ 조사청 ▸ 마당 두르기]
    ///
    /// <b>왜</b>: 들판을 깔고 나니 조사청이 벌판 한가운데 홀로 선 집이 되었다. 관아든
    /// 사가든 조선 집은 담을 두르고 그 안이 마당이다. 담이 있어야 <b>안과 밖</b>이
    /// 생기고, 안이 생겨야 그 안이 조사청 마당이 된다.
    ///
    /// <b>세 면만 두른다</b>: 남·동·서. 북쪽은 트여 들판으로 이어진다. 네 면을 다
    /// 두르면 상자가 되고, 무엇보다 이 담 낱장이 한 장에 11,316 삼각형이라 네 면이면
    /// 이십만이 넘는다 — 들판 전체(42만)의 절반이다. 뒤가 트인 담은 조선 집에 흔하고,
    /// 값도 사분의 일이 빠진다.
    ///
    /// <b>대문 자리</b>: 남쪽 한가운데를 <see cref="GateGap"/> m 비운다. 들판을 흩뿌릴
    /// 때 비워 둔 앞길(|x| &lt; 4.5)과 같은 자리라, 담을 세워도 길이 막히지 않는다.
    ///
    /// <b>샤바샤바</b>: 대문에서 기단까지 디딤돌을 놓고(한 장 108 삼각형이라 거저다),
    /// 문 양옆에 바위를 앉히고, 마당 두 귀퉁이에 나무를 한 그루씩 세운다.
    /// 마당은 비어 있어야 마당이므로 가운데에는 아무것도 두지 않는다.
    ///
    /// 자리와 방향은 조사청_실내에서 그대로 받아 온다. 다시 부르면 지웠다 새로 짓는다.
    /// </summary>
    public static class HubYard
    {
        private const string RootName = "조사청_마당";
        private const string RoomName = "조사청_실내";
        private const string HH = "Assets/HwaseongHaenggung/Prefabs/Parts/";
        private const string F = "Assets/Fristy stylize Modular Assets 2/Prefabs/";
        private const string SW = "Assets/Soswaewon/Meshes/Buildings/SM_Stone_Steps.fbx";

        // ── 마당 넓이(조사청 기준 로컬 m) ──
        private const float HalfX = 9.0f;     // 좌우
        private const float BackZ = 7.5f;     // 북쪽(트인 쪽)까지
        private const float FrontZ = -7.5f;   // 남쪽 담
        private const float GateGap = 5.0f;   // 대문 자리
        private const float GroundY = 136.95f;

        private const float Tile1 = 4.26f;    // 긴 낱장
        private const float Tile2 = 2.16f;    // 짧은 낱장 — 자투리를 메운다

        private static Vector3 _origin;
        private static float _yaw;

        [MenuItem("이문록/조사청/마당 두르기")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[마당] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var room = GameObject.Find(RoomName);
            if (room == null) { Debug.LogError("[마당] " + RoomName + " 을 못 찾았습니다."); return; }
            _origin = new Vector3(room.transform.position.x, GroundY, room.transform.position.z);
            _yaw = room.transform.eulerAngles.y;

            var t1 = AssetDatabase.LoadAssetAtPath<GameObject>(HH + "SM_StraightStronewall_1.prefab");
            var t2 = AssetDatabase.LoadAssetAtPath<GameObject>(HH + "SM_StraightStronewall_2.prefab");
            if (t1 == null || t2 == null) { Debug.LogError("[마당] 담 낱장을 못 찾았습니다: " + HH); return; }

            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "마당 두르기");
            root.transform.position = _origin;

            var log = new StringBuilder();
            int n = 0;
            var walls = Group(root.transform, "담장");

            // 남쪽 — 대문 자리를 비우고 좌우로 나눠 세운다
            float half = GateGap * 0.5f;
            n += Wall(walls, t1, t2, new Vector2(-HalfX, FrontZ), 90f, HalfX - half, "담_남서");
            n += Wall(walls, t1, t2, new Vector2(half, FrontZ), 90f, HalfX - half, "담_남동");

            // 동·서 — 남쪽 끝에서 북쪽으로. 북쪽은 트여 둔다.
            n += Wall(walls, t1, t2, new Vector2(-HalfX, FrontZ), 0f, BackZ - FrontZ, "담_서");
            n += Wall(walls, t1, t2, new Vector2(HalfX, FrontZ), 0f, BackZ - FrontZ, "담_동");
            log.AppendLine("   담장 낱장 " + n + "장");

            Props(root.transform, log);

            long tris = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh; if (m == null) continue;
                for (int s = 0; s < m.subMeshCount; s++) tris += (long)m.GetIndexCount(s) / 3;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(room.scene);
            Selection.activeGameObject = root;
            Debug.Log("[마당] 둘렀습니다 — " + tris.ToString("N0") + " 삼각형.\n" + log);
        }

        /// <summary>조사청 기준 로컬 (x, z) → 월드.</summary>
        private static Vector3 W(float x, float z)
        {
            return _origin + Quaternion.Euler(0f, _yaw, 0f) * new Vector3(x, 0f, z);
        }

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>
        /// 담 한 줄. 긴 낱장으로 채우고 남는 자투리는 짧은 낱장을 늘여 메운다 —
        /// 낱장을 잘라 낼 수는 없으니 마지막 한 장만 눌러 붙인다.
        /// 낱장의 피벗은 한쪽 끝이라 <paramref name="start"/> 에서 뻗어 나간다.
        /// </summary>
        private static int Wall(Transform parent, GameObject t1, GameObject t2,
                                Vector2 start, float turn, float length, string label)
        {
            var group = Group(parent, label);
            float yaw = _yaw + turn;
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var from = W(start.x, start.y);

            int n = 0;
            float done = 0f;
            while (length - done >= Tile1 - 0.01f) { Put(group, t1, from + dir * done, yaw, 1f, ++n); done += Tile1; }
            while (length - done >= Tile2 - 0.01f) { Put(group, t2, from + dir * done, yaw, 1f, ++n); done += Tile2; }
            float rest = length - done;
            if (rest > 0.05f) Put(group, t2, from + dir * done, yaw, rest / Tile2, ++n);
            return n;
        }

        private static void Put(Transform parent, GameObject prefab, Vector3 pos, float yaw, float stretch, int i)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            g.name = prefab.name + "_" + i.ToString("00");
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (!Mathf.Approximately(stretch, 1f)) g.transform.localScale = new Vector3(1f, 1f, stretch);
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
        }

        /// <summary>디딤돌·바위·마당나무. 마당 한가운데는 비워 둔다 — 비어야 마당이다.</summary>
        private static void Props(Transform parent, StringBuilder log)
        {
            var step = AssetDatabase.LoadAssetAtPath<GameObject>(SW);
            var rock = AssetDatabase.LoadAssetAtPath<GameObject>(F + "2_Rock.prefab");
            var tree = AssetDatabase.LoadAssetAtPath<GameObject>(F + "3_1_Tree.prefab");

            // 디딤돌 — 대문에서 기단 앞까지. 한 장 108 삼각형이라 거저다.
            if (step != null)
            {
                var g = Group(parent, "디딤돌");
                for (int i = 0; i < 6; i++)
                {
                    float z = FrontZ + 0.9f + i * 0.78f;
                    float x = (i % 2 == 0 ? -0.16f : 0.16f);      // 좌우로 조금씩 어긋나야 걸음이 된다
                    Place(g, step, W(x, z), _yaw + (i * 37f % 360f), 0.85f + (i % 3) * 0.08f, "디딤돌_" + i);
                }
                log.AppendLine("   디딤돌 6장");
            }

            // 대문 양옆 바위 — 문설주 노릇을 한다
            if (rock != null)
            {
                var g = Group(parent, "문바위");
                Place(g, rock, W(-GateGap * 0.5f - 0.7f, FrontZ + 0.4f), _yaw + 20f, 0.55f, "문바위_좌");
                Place(g, rock, W(GateGap * 0.5f + 0.7f, FrontZ + 0.5f), _yaw + 200f, 0.48f, "문바위_우");
                log.AppendLine("   문바위 2");
            }

            // 마당나무 둘 — 뒤쪽 귀퉁이. 앞에 세우면 집을 가린다.
            if (tree != null)
            {
                var g = Group(parent, "마당나무");
                Place(g, tree, W(-HalfX + 1.8f, BackZ - 2.2f), _yaw + 15f, 0.85f, "마당나무_서");
                Place(g, tree, W(HalfX - 2.0f, BackZ - 1.6f), _yaw + 200f, 0.72f, "마당나무_동");
                log.AppendLine("   마당나무 2");
            }
        }

        private static void Place(Transform parent, GameObject prefab, Vector3 pos, float yaw, float scale, string name)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            g.name = name;
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localScale = prefab.transform.localScale * scale;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
        }
    }
}
