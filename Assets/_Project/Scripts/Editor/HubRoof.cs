using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 조사청에 <b>기와 지붕 한 채</b>를 통째로 얹는다.
    /// 메뉴: [이문록 ▸ 조사청 ▸ 지붕을 기와로 갈기]
    ///
    /// <b>왜 상자로는 안 되나</b>: 한옥에서 지붕은 제일 눈에 띄는 곳이다. 처마의 휨과
    /// 기와의 골과 용마루가 없으면 아무리 아래를 잘 지어도 창고가 된다.
    ///
    /// <b>왜 광풍각 기와가 아닌가</b>: 처음엔 소쇄원 광풍각의 지붕을 도로 얹었다.
    /// 자리도 방향도 그 집에서 받아 지었으니 맞을 줄 알았는데, 재어 보니
    /// <c>SM_GPG_Roof01a</c> 는 8.2×3.0 짜리 <b>한쪽 지붕면</b>이었다 — 지붕 한 채가
    /// 아니라 비탈 한 짝이다. 그것을 얹으니 뚜껑이 아니라 뜯어 온 조각이 되었다.
    ///
    /// 그래서 고택 팩에서 <b>완성된 한 채</b>를 찾아 썼다. <c>SM_Roof01C</c> 는 네 귀가
    /// 다 있는 팔작지붕이고(용마루·내림마루·추녀·부연까지), 완성품 가운데 제일 가볍다
    /// (56,052 삼각형 — 01B 는 20만, 01D 는 29만이다). 몸통 재질이 이미 이 팩에서 왔으니
    /// 결도 같이 간다.
    ///
    /// <b>키워서 얹는다</b>: 원본이 5.0×5.3 이라 9m 몸통에는 작다. 몸통에 처마를
    /// 더한 크기에 맞춰 통째로 키운다. 기와 낱장이 그만큼 굵어지지만, 조사청은
    /// 관아의 큰 집이므로 낱장이 굵은 편이 오히려 규모에 맞는다.
    ///
    /// 상자 지붕과 서까래는 <b>지우지 않고 끈다</b>. 마음에 안 들면 도로 켜면 된다.
    ///
    /// ★플레이를 멈추고 실행할 것.
    /// </summary>
    public static class HubRoof
    {
        private const string RoomName = "조사청_실내";
        private const string RootName = "지붕_기와";
        private const string Piece =
            "Assets/_Project/Onggojip/Art/KimMyeonggwanHouse/Mesh/Roof/SM_Roof01C.fbx";

        // 몸통 치수 — HallBuilder 와 같은 값이다. 그쪽이 private 이라 여기에 적어 둔다.
        private const float HallX0 = -4.54f, HallX1 = 4.46f;
        private const float HallZ0 = -4.54f, HallZ1 = 4.46f;
        private const float BeamBottom = 3.10f, BeamThick = 0.22f;

        /// <summary>처마가 몸통 밖으로 나오는 길이(m). 한옥 처마는 깊다.</summary>
        private const float Eave = 1.05f;

        /// <summary>
        /// 처마 끝이 앉는 높이(방 기준 y) — <b>도리 윗면</b>이다.
        ///
        /// 도리 <b>밑면</b>에 물렸더니 도리와 보가 처마보다 위에 있어서, 앞에서 보면
        /// 지붕 위로 굵은 나무 격자가 한 겹 떠 있었다. 한옥에서 지붕은 도리 <b>위에</b>
        /// 얹히는 것이므로 그 윗면에 앉히는 것이 맞다.
        /// </summary>
        private const float RoofSeat = BeamBottom + BeamThick;

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

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Piece);
            if (src == null) { Debug.LogError("[지붕] " + Piece + " 를 못 찾았습니다."); return; }

            var log = new StringBuilder();

            var old = room.transform.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "지붕을 기와로");
            root.transform.SetParent(room.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            var g = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            g.name = "기와_한채";
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
            g.transform.localScale = Vector3.one;

            // ① 얼마나 키울지 — 몸통에 처마를 더한 크기를 덮게
            var raw = LocalBounds(g, room.transform);
            if (raw.size.x < 0.01f || raw.size.z < 0.01f)
            { Debug.LogError("[지붕] 지붕 크기를 못 쟀습니다."); return; }

            float wantX = (HallX1 - HallX0) + Eave * 2f;
            float wantZ = (HallZ1 - HallZ0) + Eave * 2f;
            float scale = Mathf.Max(wantX / raw.size.x, wantZ / raw.size.z);
            g.transform.localScale = Vector3.one * scale;

            // ② 재어서 앉힌다 — 가운데를 몸통 가운데에, 밑동을 도리 윗면에.
            //
            // 로컬 원점에 그냥 두면 엉뚱한 데 선다. 조각마다 제 좌표에서 어디쯤에
            // 그려져 있는지가 다르기 때문이다. 키운 <b>뒤에</b> 다시 재야 한다 —
            // 키우면 그 어긋남도 같이 커진다.
            var b = LocalBounds(g, room.transform);
            float cx = (HallX0 + HallX1) * 0.5f, cz = (HallZ0 + HallZ1) * 0.5f;
            g.transform.localPosition += new Vector3(cx - b.center.x,
                                                     RoofSeat - b.min.y,
                                                     cz - b.center.z);

            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);

            long tris = 0;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh; if (m == null) continue;
                for (int s = 0; s < m.subMeshCount; s++) tris += (long)m.GetIndexCount(s) / 3;
                // 처마 밑으로는 걸어 들어가되 지붕을 뚫고 나가지는 않게
                if (mf.GetComponent<Collider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = m;
            }

            var after = LocalBounds(g, room.transform);
            log.AppendLine("   " + System.IO.Path.GetFileNameWithoutExtension(Piece)
                           + " 를 " + scale.ToString("F2") + "배로 키워 얹었습니다");
            log.AppendLine("   덮은 크기 " + after.size.x.ToString("F1") + " × " + after.size.z.ToString("F1")
                           + "  (몸통 " + (HallX1 - HallX0).ToString("F1") + " × " + (HallZ1 - HallZ0).ToString("F1")
                           + " + 처마 " + Eave + ")");
            log.AppendLine("   처마 끝 y " + after.min.y.ToString("F2") + " · 용마루 y " + after.max.y.ToString("F2"));

            // 상자 지붕과 <b>서까래</b>를 끈다 — 지우지 않는다. 되돌릴 수 있어야 한다.
            //
            // 서까래도 같이 끄는 까닭: 그것은 상자 지붕을 받치라고 평평하게 깔아 둔
            // 격자다. 처마가 내려앉는 진짜 기와에서는 그 격자가 지붕을 <b>뚫고 나온다</b> —
            // 지붕 둘레에 널판이 한 겹 둘러진 것처럼 보인다.
            // 기와에는 제 서까래와 부연이 이미 조각되어 있다.
            foreach (var n in new[] { "구조/지붕", "구조/서까래" })
            {
                var t = room.transform.Find(n);
                if (t == null) continue;
                Undo.RecordObject(t.gameObject, "상자 지붕 끄기");
                t.gameObject.SetActive(false);
                log.AppendLine("   " + n + " 을 껐습니다 — 지우지 않았으니 되돌릴 수 있습니다");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(room.scene);
            Selection.activeGameObject = root;
            Debug.Log("[지붕] " + tris.ToString("N0") + " 삼각형\n" + log);
        }

        /// <summary>
        /// 이 물건이 <paramref name="room"/> 기준으로 차지한 상자.
        ///
        /// 메시가 <b>제 좌표에서</b> 차지한 상자를 물건의 행렬로 옮겨 재야 한다.
        /// Renderer.bounds(월드 축 상자)의 귀퉁이를 옮기면, 방이 140도 돌아앉아 있어서
        /// 상자가 두 번 부푼다 — 8.2×3.0 짜리 조각이 11.1×11.1 로 나왔고,
        /// 그 값을 믿고 "크기가 딱 맞는다" 고 여긴 것이 처음의 잘못이었다.
        /// </summary>
        private static Bounds LocalBounds(GameObject go, Transform room)
        {
            var inv = room.worldToLocalMatrix;
            bool first = true;
            var acc = new Bounds();

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh; if (m == null) continue;
                var mat = inv * mf.transform.localToWorldMatrix;
                var c = m.bounds.center; var e = m.bounds.extents;
                for (int i = 0; i < 8; i++)
                {
                    var p = mat.MultiplyPoint3x4(new Vector3(
                        c.x + ((i & 1) == 0 ? -e.x : e.x),
                        c.y + ((i & 2) == 0 ? -e.y : e.y),
                        c.z + ((i & 4) == 0 ? -e.z : e.z)));
                    if (first) { acc = new Bounds(p, Vector3.zero); first = false; }
                    else acc.Encapsulate(p);
                }
            }
            return first ? new Bounds(Vector3.zero, Vector3.zero) : acc;
        }
    }
}
