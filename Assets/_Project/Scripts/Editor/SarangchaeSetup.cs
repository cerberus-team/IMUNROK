using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 사랑채 실내를 한 번에 정리한다. 메뉴: [이문록 ▸ 사랑채 실내 정리]
    ///
    /// 손으로 고쳐 두면 재생을 껐다 켤 때마다 도로 돌아간다(재생 중에 만진 씬은 저장되지
    /// 않는다). 그래서 <b>다시 돌릴 수 있는 형태</b>로 남긴다. 여러 번 눌러도 같은 결과가
    /// 되도록 지금 어긋난 만큼을 재서 고치지, 정해진 값을 더하지 않는다.
    ///
    /// 하는 일 둘:
    ///   ① 바닥에 박히거나 뜬 세간을 바닥에 앉힌다(아궁이·문갑·장롱).
    ///   ② 사랑방 창호를 여닫이로 짠다 — 판때기 열두 장에 경첩과 콜라이더가 없어
    ///      눌러도 아무 일이 없었다.
    /// </summary>
    public static class SarangchaeSetup
    {
        /// <summary>바닥에 앉힐 것들. 이름과, 바닥에서 얼마나 띄울지(신발 두께 같은 것).</summary>
        private static readonly (string name, float lift)[] Sitters =
        {
            ("아궁이", 0f),   // 부뚜막은 아궁이의 자식이라 같이 따라 올라간다
            ("문갑",   0f),
            ("장롱",   0f),
        };

        [MenuItem("이문록/사랑채 실내 정리")]
        public static void Run()
        {
            int moved = SitOnFloor();
            int doors = BuildSlidingDoors();
            EditorSceneManagerMarkDirty();
            Debug.Log($"[사랑채] 세간 {moved}개를 바닥에 앉히고, 창호 {doors}칸을 여닫이로 짰습니다.");
        }

        private static void EditorSceneManagerMarkDirty()
        {
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s);
        }

        // ── ① 바닥에 앉히기 ──────────────────────────

        /// <summary>
        /// 물건 바로 밑의 바닥을 재서 그 위에 앉힌다.
        ///
        /// 정해진 값을 더하지 않는 까닭: 두 번 누르면 두 번 올라간다. 실제로 부뚜막을
        /// 그렇게 올렸다가 공중에 띄웠다 — 부뚜막은 아궁이의 자식이어서 부모를 올릴 때
        /// 이미 따라 올라간 뒤였다.
        /// </summary>
        private static int SitOnFloor()
        {
            int n = 0;
            foreach (var (name, lift) in Sitters)
            {
                var go = Find(name);
                if (go == null) { Debug.LogWarning("[사랑채] 못 찾음: " + name); continue; }

                var b = WorldBounds(go);
                if (b.size == Vector3.zero) continue;

                float groundY;
                if (!GroundUnder(go, b, out groundY)) { Debug.LogWarning("[사랑채] 바닥을 못 찾음: " + name); continue; }

                float delta = (groundY + lift) - b.min.y;
                if (Mathf.Abs(delta) < 0.005f) continue;

                Undo.RecordObject(go.transform, "세간 앉히기");
                go.transform.position += new Vector3(0f, delta, 0f);
                n++;
            }
            return n;
        }

        /// <summary>물건 밑의 바닥 높이. 자기 자신과 제 위쪽 것은 세지 않는다.</summary>
        private static bool GroundUnder(GameObject self, Bounds b, out float y)
        {
            y = 0f;
            var hits = Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 2f, b.center.z),
                                          Vector3.down, 14f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(self.transform)) continue;
                if (h.point.y > b.max.y - 0.02f) continue;      // 물건보다 위에 있는 것은 바닥이 아니다
                if (h.distance < best) { best = h.distance; y = h.point.y; found = true; }
            }
            return found;
        }

        // ── ② 창호를 여닫이로 ────────────────────────

        /// <summary>
        /// 사랑방 남쪽 창호 네 칸을 여닫이로 짠다.
        ///
        /// 문짝은 상자 열두 장으로 서 있었을 뿐 콜라이더도 경첩도 없었다 — 눌러도 레이가
        /// 맞을 것이 없으니 아무 일도 일어나지 않는다. 문짝마다 바깥쪽 모서리에 경첩을
        /// 세우고 그 밑으로 넣는다. 문짝 한가운데를 돌리면 회전문이 된다.
        /// </summary>
        private static int BuildSlidingDoors()
        {
            var swap = Object.FindFirstObjectByType<InteriorSceneSwap>(FindObjectsInactive.Include);
            if (swap == null) { Debug.LogWarning("[사랑채] 실내 교체 부품을 못 찾음"); return 0; }
            var so = new SerializedObject(swap);
            var show = so.FindProperty("_showWhileInside").objectReferenceValue as GameObject;
            if (show == null) { Debug.LogWarning("[사랑채] 실내 묶음이 비어 있음"); return 0; }

            Transform doorGroup = null;
            foreach (var t in show.GetComponentsInChildren<Transform>(true))
                if (t.name == "문") { doorGroup = t; break; }
            if (doorGroup == null) { Debug.LogWarning("[사랑채] 문 묶음을 못 찾음"); return 0; }

            int bays = 0;
            for (int bay = 0; bay <= 8; bay++)
            {
                // 이미 짠 것이 있으면 문짝을 도로 꺼내고 지운다(여러 번 눌러도 같게)
                var old = doorGroup.Find("사랑방문_" + bay);
                if (old != null)
                {
                    foreach (Transform hinge in old)
                        while (hinge.childCount > 0) hinge.GetChild(0).SetParent(doorGroup, true);
                    Undo.DestroyObjectImmediate(old.gameObject);
                }

                var leaves = new System.Collections.Generic.List<Transform>();
                foreach (Transform c in doorGroup)
                    if (c.name.StartsWith("문_" + bay + "_")) leaves.Add(c);
                if (leaves.Count == 0) continue;
                leaves.Sort((a, b) => a.position.x.CompareTo(b.position.x));

                var holder = new GameObject("사랑방문_" + bay);
                Undo.RegisterCreatedObjectUndo(holder, "창호 짜기");
                holder.transform.SetParent(doorGroup, false);

                var hinges = new System.Collections.Generic.List<Transform>();
                var angles = new System.Collections.Generic.List<float>();
                for (int i = 0; i < leaves.Count; i++)
                {
                    var leaf = leaves[i];
                    var r = leaf.GetComponent<Renderer>();
                    float w = r != null ? r.bounds.size.x : 0.8f;

                    if (leaf.GetComponent<Collider>() == null)
                    {
                        // 종잇장이라 두께가 5cm 다. 그대로면 겨냥이 어려워 앞뒤로만 조금 두껍게.
                        var bc = leaf.gameObject.AddComponent<BoxCollider>();
                        bc.size = new Vector3(1f, 1f, Mathf.Max(1f, 0.08f / Mathf.Max(0.001f, leaf.localScale.z)));
                    }

                    bool leftHalf = i < leaves.Count / 2f;
                    float edgeX = leaf.position.x + (leftHalf ? -w * 0.5f : w * 0.5f);

                    var hinge = new GameObject("경첩_" + bay + "_" + i);
                    hinge.transform.SetParent(holder.transform, false);
                    hinge.transform.position = new Vector3(edgeX, leaf.position.y, leaf.position.z);
                    hinge.transform.rotation = leaf.rotation;
                    leaf.SetParent(hinge.transform, true);

                    hinges.Add(hinge.transform);
                    angles.Add(leftHalf ? -85f : 85f);     // 마루 쪽으로 열린다
                }

                var dc = holder.AddComponent<DoorController>();
                var dso = new SerializedObject(dc);
                dso.FindProperty("_motion").enumValueIndex = 0;      // 여닫이
                var arr = dso.FindProperty("_leaves");
                arr.arraySize = hinges.Count;
                for (int i = 0; i < hinges.Count; i++)
                {
                    var el = arr.GetArrayElementAtIndex(i);
                    el.FindPropertyRelative("pivot").objectReferenceValue = hinges[i];
                    el.FindPropertyRelative("swingAngle").floatValue = angles[i];
                    el.FindPropertyRelative("slideOffset").vector3Value = Vector3.zero;
                }
                dso.FindProperty("_openDuration").floatValue = 1.0f;
                dso.FindProperty("_playerCanToggle").boolValue = true;
                dso.FindProperty("_maxTouchDistance").floatValue = 3f;
                dso.FindProperty("_locked").boolValue = false;
                dso.ApplyModifiedPropertiesWithoutUndo();
                bays++;
            }
            return bays;
        }

        // ── 도구 ────────────────────────────────────

        private static GameObject Find(string n)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == n) return t.gameObject;
            return null;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
