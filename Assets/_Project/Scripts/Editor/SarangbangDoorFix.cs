using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 사랑방 문을 제 노릇대로 나눈다. 메뉴: [이문록 ▸ 사랑방 문 바로잡기]
    ///
    /// <b>바깥 창호(사랑방문_0~3)</b> — 열지 않는다. 마당을 향한 분합문이라 밤새 닫아 둔다.
    /// 눌러도 안 열리게 막는다. 콜라이더는 남겨 두어야 몸이 벽을 뚫고 나가지 않는다.
    ///
    /// <b>방과 방 사이(칸막이여닫이_0·1)</b> — <b>미닫이</b>다. 옆으로 밀고 넘어다닌다.
    /// 두 짝이 서로 반대쪽으로 물러나 각각 옆 벽 속으로 들어가고, 그만큼 문간이 열린다.
    /// 좁은 방에서 여닫이로 두면 문짝이 사람 쪽으로 휘둘러진다.
    ///
    /// 세 가지를 한꺼번에 고친다.
    ///
    /// ① <b>문짝이 정적(Static)으로 잠겨 있었다.</b> 이것이 "문이 안 열린다"의 진짜 까닭이다.
    ///    정적으로 표시된 메시는 유니티가 실행할 때 한 덩어리로 <b>구워 붙인다</b>(Combined
    ///    Mesh). 그러면 경첩을 아무리 돌려도 그려지는 나무는 제자리에 남는다 — 문은 열렸다고
    ///    하는데(IsOpen=true) 눈에는 아무 일도 안 일어난다. 움직이는 것에는 정적 표시를
    ///    붙이면 안 된다. 쪽문만 표시가 없어서 그것만 여닫혔던 것이다.
    ///
    /// ② <b>경첩이 문짝 한가운데 있었다</b>(칸막이문 두 짝). 그러면 열리는 게 아니라
    ///    제자리에서 도는 회전문이 된다. 경첩은 칸의 <b>바깥쪽 모서리</b>로 옮긴다.
    ///
    /// ③ <b>여는 방향</b>. 칸 한가운데를 기준으로 왼쪽 무리는 왼쪽으로, 오른쪽 무리는
    ///    오른쪽으로 — 대문처럼 가운데가 갈라져 열린다. 그리고 방 안이 아니라 <b>마루 쪽</b>
    ///    으로 젖혀진다. 방향은 방 한가운데에서 그 칸이 어느 쪽에 있는지로 정한다.
    ///
    /// 문짝은 제자리에 그대로 두고 경첩만 옮기므로, 눌러도 문이 튀지 않는다.
    /// </summary>
    public static class SarangbangDoorFix
    {
        private const string PrefabPath = "Assets/_Project/Onggojip/Prefabs/사랑채_실내.prefab";
        private const float Swing = 85f;
        private const string NL = "\n";

        [MenuItem("이문록/사랑방 문 바로잡기")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[문] 재생 중입니다 — 재생을 끄고 다시 누르십시오.");
                return;
            }

            var log = new System.Text.StringBuilder();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { Debug.LogError("[문] 실내 프리팹을 못 열었습니다."); return; }
            try
            {
                Vector3 roomCenter = RoomCenter(root.transform);
                int done = 0;
                foreach (var dc in root.GetComponentsInChildren<DoorController>(true))
                {
                    if (!IsSwingBay(dc)) continue;
                    if (FixBay(dc, roomCenter, log)) done++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                log.Insert(0, $"칸 {done}개를 바로잡았습니다.\n");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // 씬에 놓인 인스턴스에 정적 표시가 따로 덧씌워져 있을 수 있다. 거기도 벗긴다.
            int inScene = ClearStaticInScenes();
            if (inScene > 0) log.Append($"씬 인스턴스에서도 정적 표시 {inScene}개를 벗겼습니다.\n");

            AssetDatabase.SaveAssets();
            Debug.Log("[문] " + log);
        }

        /// <summary>창호·칸막이처럼 <b>경첩으로 도는</b> 칸만 손댄다(문갑·장은 뼈가 문짝이라 제외).</summary>
        private static bool IsSwingBay(DoorController dc)
        {
            return dc.name.StartsWith("사랑방문") || dc.name.StartsWith("칸막이") || dc.name.StartsWith("쪽문");
        }

        /// <summary>이 칸은 미닫이인가 — 방과 방 사이는 옆으로 민다.</summary>
        private static bool IsSliding(DoorController dc) => dc.name.StartsWith("칸막이");

        /// <summary>이 칸은 닫아 둘 것인가 — 마당을 향한 바깥 창호.</summary>
        private static bool IsShut(DoorController dc) => dc.name.StartsWith("사랑방문");

        private static bool FixBay(DoorController dc, Vector3 roomCenter, System.Text.StringBuilder log)
        {
            var so = new SerializedObject(dc);
            var arr = so.FindProperty("_leaves");
            if (arr == null || arr.arraySize == 0) return false;

            // ① 움직일 것에서 정적 표시를 벗긴다
            ClearStatic(dc.gameObject);

            var pivots = new List<Transform>();
            var bounds = new List<Bounds>();
            for (int i = 0; i < arr.arraySize; i++)
            {
                var p = arr.GetArrayElementAtIndex(i).FindPropertyRelative("pivot").objectReferenceValue as Transform;
                if (p == null) continue;
                ClearStatic(p.gameObject);
                var b = MeshWorldBounds(p);
                if (b.size == Vector3.zero) continue;
                pivots.Add(p);
                bounds.Add(b);
            }
            if (pivots.Count == 0) return false;

            // ② 칸이 어느 축을 따라 늘어섰나 — 문짝이 넓은 쪽이 그 축이다
            bool alongX = bounds[0].size.x >= bounds[0].size.z;

            float min = float.MaxValue, max = float.MinValue;
            foreach (var b in bounds)
            {
                float lo = alongX ? b.min.x : b.min.z;
                float hi = alongX ? b.max.x : b.max.z;
                if (lo < min) min = lo;
                if (hi > max) max = hi;
            }
            float bayCenter = (min + max) * 0.5f;

            // ③ 어느 쪽이 '바깥'인가 — 방 한가운데에서 이 칸을 보는 방향
            float wallOffset = (alongX ? bounds[0].center.z : bounds[0].center.x)
                             - (alongX ? roomCenter.z : roomCenter.x);
            Vector3 outward = alongX
                ? new Vector3(0f, 0f, Mathf.Sign(wallOffset))
                : new Vector3(Mathf.Sign(wallOffset), 0f, 0f);

            bool sliding = IsSliding(dc);
            so.FindProperty("_motion").enumValueIndex = sliding ? 1 : 0;   // 0 여닫이 · 1 미닫이

            for (int i = 0; i < pivots.Count; i++)
            {
                var pivot = pivots[i];
                var b = bounds[i];
                float c = alongX ? b.center.x : b.center.z;

                if (sliding)
                {
                    // 미닫이 — 제 폭만큼 <b>바깥쪽으로</b> 물러난다. 두 짝이 서로 반대쪽으로
                    // 밀리며 각각 옆 벽 속으로 들어가고, 그 사이가 문간이 된다.
                    float width = alongX ? b.size.x : b.size.z;
                    float dir = (c <= bayCenter) ? -1f : 1f;
                    Vector3 world = (alongX ? Vector3.right : Vector3.forward) * (width * dir);
                    var se = arr.GetArrayElementAtIndex(i);
                    se.FindPropertyRelative("slideOffset").vector3Value = dc.transform.InverseTransformVector(world);
                    se.FindPropertyRelative("swingAngle").floatValue = 0f;
                    log.Append("  ").Append(dc.name).Append(" / ").Append(pivot.name)
                       .Append(" 미닫이 ").Append(world.ToString("F2")).Append(NL);
                    continue;
                }

                // 칸 한가운데에서 먼 쪽 모서리 = 이 짝의 바깥 모서리. 거기에 경첩을 건다.
                float edge = (c <= bayCenter) ? (alongX ? b.min.x : b.min.z)
                                              : (alongX ? b.max.x : b.max.z);

                Vector3 target = pivot.position;
                if (alongX) { target.x = edge; target.z = b.center.z; }
                else { target.z = edge; target.x = b.center.x; }
                MoveHingeKeepingLeaf(pivot, target);

                // 경첩에서 문짝으로 뻗은 팔이 바깥쪽으로 눕도록 부호를 정한다.
                Vector3 arm = b.center - pivot.position;
                arm.y = 0f;
                if (arm.sqrMagnitude < 1e-6f) continue;
                arm.Normalize();
                float sign = Mathf.Sign(arm.z * outward.x - arm.x * outward.z);
                if (Mathf.Approximately(sign, 0f)) sign = 1f;

                var el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("swingAngle").floatValue = Swing * sign;

                log.Append("  ").Append(dc.name).Append(" / ").Append(pivot.name)
                   .Append(" 경첩→").Append(edge.ToString("F2"))
                   .Append(" 각 ").Append((Swing * sign).ToString("F0")).Append("\n");
            }

            // 바깥 창호는 잠가 둔다 — 마당을 향한 분합문이라 밤새 닫아 두는 문이다.
            // 콜라이더는 남긴다(몸이 벽을 뚫고 나가면 안 되므로).
            if (IsShut(dc))
            {
                so.FindProperty("_playerCanToggle").boolValue = false;
                so.FindProperty("_startOpen").boolValue = false;
                log.Append("  ").Append(dc.name).Append(" — 닫아 두고 잠금").Append(NL);
            }
            else if (sliding)
            {
                so.FindProperty("_playerCanToggle").boolValue = true;
                so.FindProperty("_openDuration").floatValue = 0.9f;
                so.FindProperty("_maxTouchDistance").floatValue = 2.5f;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dc);
            return true;
        }

        /// <summary>
        /// 경첩만 옮기고 문짝은 <b>월드에서 제자리에</b> 둔다. 그러지 않으면 경첩을 옮긴
        /// 만큼 문이 벽을 뚫고 튀어나간다.
        /// </summary>
        private static void MoveHingeKeepingLeaf(Transform pivot, Vector3 target)
        {
            if ((pivot.position - target).sqrMagnitude < 1e-8f) return;
            int n = pivot.childCount;
            var pos = new Vector3[n];
            var rot = new Quaternion[n];
            for (int i = 0; i < n; i++) { pos[i] = pivot.GetChild(i).position; rot[i] = pivot.GetChild(i).rotation; }
            pivot.position = target;
            for (int i = 0; i < n; i++) { pivot.GetChild(i).position = pos[i]; pivot.GetChild(i).rotation = rot[i]; }
        }

        /// <summary>
        /// 메시로 잰 월드 상자. <b>렌더러 상자를 쓰면 안 된다</b> — 꺼져 있는 오브젝트는
        /// 0을 돌려주고, 실행 중에는 구워 붙인 덩어리의 상자를 돌려준다.
        /// </summary>
        private static Bounds MeshWorldBounds(Transform t)
        {
            bool any = false;
            var acc = new Bounds();
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? mb.min.x : mb.max.x,
                        (i & 2) == 0 ? mb.min.y : mb.max.y,
                        (i & 4) == 0 ? mb.min.z : mb.max.z);
                    var w = m.MultiplyPoint3x4(corner);
                    if (!any) { acc = new Bounds(w, Vector3.zero); any = true; }
                    else acc.Encapsulate(w);
                }
            }
            return any ? acc : new Bounds();
        }

        private static Vector3 RoomCenter(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "장판바닥")
                {
                    var b = MeshWorldBounds(t);
                    if (b.size != Vector3.zero) return b.center;
                }
            return MeshWorldBounds(root).center;
        }

        /// <summary>움직이는 것에서 정적 표시를 벗긴다(제 자신과 모든 자식).</summary>
        private static void ClearStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (!t.gameObject.isStatic && GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                t.gameObject.isStatic = false;
                EditorUtility.SetDirty(t.gameObject);
            }
        }

        /// <summary>열려 있는 씬의 인스턴스에도 덧씌워진 정적 표시가 있으면 벗긴다.</summary>
        private static int ClearStaticInScenes()
        {
            int n = 0;
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var rootGo in scene.GetRootGameObjects())
                    foreach (var dc in rootGo.GetComponentsInChildren<DoorController>(true))
                    {
                        if (!IsSwingBay(dc)) continue;
                        foreach (var t in dc.GetComponentsInChildren<Transform>(true))
                        {
                            if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0 && !t.gameObject.isStatic) continue;
                            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                            t.gameObject.isStatic = false;
                            EditorUtility.SetDirty(t.gameObject);
                            n++;
                        }
                    }
                if (n > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            }
            if (n > 0) UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            return n;
        }
    }
}
