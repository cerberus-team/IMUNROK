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

        /// <summary>
        /// 광풍각 지붕 <b>한 짝</b>만 쓴다.
        ///
        /// 팩에는 01a·02a 둘이 들어 있는데, 둘 다 얹으면 겹친다 — 원본 씬에서 서로
        /// 다른 자리에 놓여 한 채를 이루던 조각이라, 자리 값을 잃은 채로 둘을 같이
        /// 얹으면 지붕이 두 겹이 된다. 01a 하나가 11.2×11.1 로 9m 몸통에 처마
        /// 1.2m 씩을 더한 크기와 그대로 맞으므로 그것만 쓴다.
        /// </summary>
        private static readonly string[] Pieces = { "SM_GPG_Roof01a" };

        // 몸통 치수 — HallBuilder 와 같은 값이다. 그쪽이 private 이라 여기에 적어 둔다.
        private const float HallX0 = -4.54f, HallX1 = 4.46f;
        private const float HallZ0 = -4.54f, HallZ1 = 4.46f;
        private const float BeamBottom = 3.10f, BeamThick = 0.22f;

        /// <summary>
        /// 처마 끝이 앉는 높이(방 기준 y) — <b>도리 윗면</b>이다.
        ///
        /// 처음엔 도리 <b>밑면</b>에 물렸다(2.88). 그랬더니 도리와 보가 처마보다 위에
        /// 있어서, 앞에서 보면 지붕 위로 굵은 나무 격자가 한 겹 떠 있었다 —
        /// 지붕이 몸통보다 좁아 보이던 까닭이 이것이다. 한옥에서 지붕은 도리 <b>위에</b>
        /// 얹히는 것이므로 그 윗면(3.10+0.22)에 앉히는 것이 맞다.
        /// </summary>
        private const float RoofSeat = BeamBottom + BeamThick;

        /// <summary>이 물건이 <paramref name="room"/> 기준으로 차지한 상자.</summary>
        private static Bounds LocalBounds(GameObject g, Transform room)
        {
            var rs = g.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);

            var w = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) w.Encapsulate(rs[i].bounds);

            var inv = room.worldToLocalMatrix;
            var c = w.center; var e = w.extents;
            var b = new Bounds(inv.MultiplyPoint3x4(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
                b.Encapsulate(inv.MultiplyPoint3x4(new Vector3(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z))));
            return b;
        }

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
                g.transform.localPosition = Vector3.zero;
                g.transform.localRotation = Quaternion.identity;
                g.transform.localScale = Vector3.one;

                // <b>재어서 앉힌다</b>.
                //
                // 로컬 원점에 그냥 두면 엉뚱한 데 선다. 이 조각은 원본 씬에서 제 자리
                // 값을 지니고 놓여 있던 것이라, 그 값이 메시 안에 배어 있다 — 처음
                // 얹었을 때 지붕이 마당 한쪽에 널브러진 까닭이 이것이다.
                // 크기는 맞으므로(11.2×11.1 대 9m+처마), <b>가운데를 몸통 가운데에</b>
                // 맞추고 <b>밑동을 도리에</b> 물리면 제자리에 앉는다.
                var b = LocalBounds(g, room.transform);
                if (b.size != Vector3.zero)
                {
                    float cx = (HallX0 + HallX1) * 0.5f, cz = (HallZ0 + HallZ1) * 0.5f;
                    g.transform.localPosition += new Vector3(cx - b.center.x,
                                                             RoofSeat - b.min.y,
                                                             cz - b.center.z);
                    log.AppendLine("   " + name + " 를 재어 앉혔습니다 — 크기 " + b.size.ToString("F1"));
                }
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

            // 상자 지붕과 <b>서까래</b>를 끈다 — 지우지 않는다. 되돌릴 수 있어야 한다.
            //
            // 서까래도 같이 끄는 까닭: 그것은 상자 지붕을 받치라고 3.52m 높이에 평평하게
            // 깔아 둔 격자다. 진짜 기와는 처마 끝이 2.9m 로 내려앉고 용마루로 솟으므로,
            // 격자가 처마 언저리에서 지붕을 <b>뚫고 나온다</b> — 지붕 둘레에 널판이
            // 한 겹 둘러진 것처럼 보인다. 기와에는 제 서까래가 이미 조각되어 있다.
            foreach (var n in new[] { "구조/지붕", "구조/서까래" })
            {
                var g = room.transform.Find(n);
                if (g == null) continue;
                Undo.RecordObject(g.gameObject, "상자 지붕 끄기");
                g.gameObject.SetActive(false);
                log.AppendLine("   " + n + " 을 껐습니다 — 지우지 않았으니 되돌릴 수 있습니다");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(room.scene);
            Selection.activeGameObject = root;
            Debug.Log("[지붕] 기와 " + put + "짝 · " + tris.ToString("N0") + " 삼각형\n" + log);
        }
    }
}
