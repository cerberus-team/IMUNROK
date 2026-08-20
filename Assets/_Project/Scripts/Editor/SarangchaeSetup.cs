using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 사랑채 실내를 한 번에 정리한다. 메뉴: [이문록 ▸ 사랑채 실내 정리]
    ///
    /// 손으로 고쳐 두면 재생을 껐다 켤 때마다 도로 돌아간다(재생 중에 만진 씬은 저장되지
    /// 않는다). 그래서 <b>다시 돌릴 수 있는 형태</b>로 남긴다. 여러 번 눌러도 같은 결과가
    /// 되도록 지금 어긋난 만큼을 재서 고치지, 정해진 값을 더하지 않는다.
    ///
    /// 하는 일 셋:
    ///   ① 실내 프리팹 안에서 — 바닥에 박힌 세간을 마루에 앉히고, 창호를 여닫이로 짠다.
    ///   ② 씬에서 — 마당에 박힌 아궁이를 앉힌다.
    ///   ③ 씬에서 — 마주 앉기와 물러가기를 잇는다(플레이어 앉힘 · 甲이 문 열고 나가기).
    ///
    /// 왜 프리팹 안에서 하는가: 사랑채 실내는 프리팹이다. 프리팹 인스턴스 안에서는
    /// 자식의 부모를 바꿀 수 없어(경첩을 세워 문짝을 그 밑에 넣는 일) 씬에서는 막힌다 —
    /// 처음에 그렇게 짰다가 "Setting the parent of a transform which resides in a Prefab
    /// instance is not possible" 만 스무 줄 찍고 문짝은 그대로였다.
    /// </summary>
    public static class SarangchaeSetup
    {
        private const string PrefabPath = "Assets/_Project/Onggojip/Prefabs/사랑채_실내.prefab";

        /// <summary>甲이 나갈 때 열 창호 칸. 그가 앉은 보료에서 가장 가까운 칸이다.</summary>
        private const int ExitBay = 1;

        [MenuItem("이문록/사랑채 실내 정리")]
        public static void Run()
        {
            // 재생 중에 고친 씬은 저장되지 않는다. 예외를 뱉고 반쯤 고쳐 놓느니 아예 안 한다.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[사랑채] 재생 중입니다 — 지금 고쳐도 재생을 끄는 순간 도로 돌아갑니다. " +
                                 "재생을 끄고 다시 누르십시오.");
                return;
            }

            int doors = FixInterior();
            int props = SitSceneProps();
            string wired = WireSeatAndLeave();

            EditorSceneManagerMarkDirty();
            Debug.Log($"[사랑채] 창호 {doors}칸을 여닫이로 짜고, 씬 세간 {props}개를 바닥에 앉혔습니다.\n{wired}");
        }

        private static void EditorSceneManagerMarkDirty()
        {
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s);
        }

        // ── ① 실내 프리팹 ────────────────────────────

        /// <summary>실내 프리팹을 열어 세간을 앉히고 창호를 짠 뒤 저장한다.</summary>
        private static int FixInterior()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { Debug.LogError("[사랑채] 실내 프리팹을 못 열었습니다: " + PrefabPath); return 0; }

            int bays;
            try
            {
                // 마루에 앉힐 것들. 프리팹 안이라 물리 광선을 못 쓴다(미리보기 씬에는 물리가 없다) —
                // 그래서 렌더러 크기를 재서 밑에 깔린 면을 찾는다.
                SitOnFloor(root, "문갑");
                SitOnFloor(root, "장롱");
                PutCushionAtSeat(root);
                bays = BuildDoors(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // 씬 인스턴스에 남아 있는 옛 흔적을 치운다.
            CleanInstance();
            return bays;
        }

        /// <summary>
        /// 물건 바로 밑에 깔린 면 위에 앉힌다.
        ///
        /// 정해진 값을 더하지 않는 까닭: 두 번 누르면 두 번 올라간다. 실제로 부뚜막을
        /// 그렇게 올렸다가 공중에 띄웠다 — 부뚜막은 아궁이의 자식이어서 부모를 올릴 때
        /// 이미 따라 올라간 뒤였다.
        /// </summary>
        private static void SitOnFloor(GameObject root, string name)
        {
            var go = FindIn(root, name);
            if (go == null) { Debug.LogWarning("[사랑채] 못 찾음: " + name); return; }

            Bounds b = WorldBounds(go);
            if (b.size == Vector3.zero) return;

            float top;
            if (!SurfaceUnder(root, go, b, out top)) { Debug.LogWarning("[사랑채] 앉을 면을 못 찾음: " + name); return; }

            float delta = top - b.min.y;
            if (Mathf.Abs(delta) < 0.005f) return;
            go.transform.position += new Vector3(0f, delta, 0f);
        }

        /// <summary>
        /// 물건 밑에 깔린 면 중 가장 높은 것. 자기 자신과 제 위쪽 것은 세지 않는다.
        ///
        /// 광선을 쏘지 않는 까닭이 하나 더 있다: 실내는 방에 들어서기 전까지 꺼져 있다.
        /// 꺼진 콜라이더는 광선에 안 잡히므로, 씬에서 광선을 쏘면 마루(-0.80)를 지나쳐
        /// 그 아래 고택 원본 바닥(-0.90)을 짚는다 — 세간이 마루에 10cm 박힌 채로 앉은
        /// 것이 이것이었다.
        /// </summary>
        private static bool SurfaceUnder(GameObject root, GameObject self, Bounds b, out float top)
        {
            top = float.MinValue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(self.transform)) continue;
                var rb = r.bounds;
                if (rb.size.x < 1f || rb.size.z < 1f) continue;              // 바닥이라 할 만한 넓이
                if (b.center.x < rb.min.x || b.center.x > rb.max.x) continue;
                if (b.center.z < rb.min.z || b.center.z > rb.max.z) continue;
                if (rb.max.y > b.max.y - 0.02f) continue;                    // 물건보다 위에 있는 것은 바닥이 아니다
                if (rb.max.y > top) top = rb.max.y;
            }
            return top != float.MinValue;
        }

        /// <summary>
        /// 손님 방석을 플레이어가 앉을 자리로 옮긴다.
        ///
        /// 맨 마루에 앉으면 "앉았다"가 아니라 "가라앉았다"로 보인다. 방석이 눈 아래
        /// 깔려 있어야 앉은 자리가 읽힌다. 세 장 중 홀로 떨어져 있던 것을 쓴다.
        /// </summary>
        private static void PutCushionAtSeat(GameObject root)
        {
            var seat = GameObject.Find("중문_도착지점");
            if (seat == null) return;

            var cushion = FindIn(root, "방석");
            if (cushion == null) return;

            var b = WorldBounds(cushion);
            float lift = b.center.y - b.min.y;                  // 방석 밑면이 마루에 닿게
            float floor;
            if (!SurfaceUnder(root, cushion, b, out floor)) floor = b.min.y;
            cushion.transform.position = new Vector3(seat.transform.position.x, floor + lift, seat.transform.position.z);
        }

        // ── ① 창호를 여닫이로 ────────────────────────

        /// <summary>
        /// 판때기로 서 있던 문짝들을 여닫이로 짠다 — 사랑방 창호 네 칸과 칸막이문 두 칸.
        ///
        /// 문짝에는 콜라이더도 경첩도 없었다. 눌러도 레이가 맞을 것이 없으니 아무 일도
        /// 일어나지 않는다. 문짝마다 바깥쪽 모서리에 경첩을 세우고 그 밑으로 넣는다 —
        /// 문짝 한가운데를 돌리면 회전문이 된다.
        /// </summary>
        private static int BuildDoors(GameObject root)
        {
            // 문이 어느 쪽으로 열려야 하는지는 방 한가운데를 기준으로 잰다(아래 참고).
            var floor = FindIn(root, "장판바닥");
            Vector3 hall = floor != null ? WorldBounds(floor).center : WorldBounds(root).center;

            int bays = 0;
            var doorGroup = FindIn(root, "문");
            if (doorGroup != null)
            {
                for (int bay = 0; bay <= 8; bay++)
                    if (BuildBay(doorGroup.transform, "사랑방문_" + bay, "문_" + bay + "_", hall)) bays++;
                if (BuildBay(doorGroup.transform, "서쪽문", "쪽문_서", hall)) bays++;
            }

            var partition = FindIn(root, "칸막이");
            if (partition != null)
                for (int bay = 0; bay <= 4; bay++)
                    if (BuildBay(partition.transform, "칸막이여닫이_" + bay, "칸막이문" + bay + "_", hall)) bays++;

            return bays;
        }

        /// <summary>한 칸을 여닫이로. 이미 짜여 있으면 도로 풀고 다시 짠다(여러 번 눌러도 같게).</summary>
        private static bool BuildBay(Transform group, string holderName, string leafPrefix, Vector3 hall)
        {
            var old = group.Find(holderName);
            if (old != null)
            {
                foreach (Transform hinge in old)
                    while (hinge.childCount > 0) hinge.GetChild(0).SetParent(group, true);
                Object.DestroyImmediate(old.gameObject);
            }

            var leaves = new List<Transform>();
            foreach (Transform c in group)
                if (c.name.StartsWith(leafPrefix)) leaves.Add(c);
            if (leaves.Count == 0) return false;

            // 이미 제대로 짜인 문은 건드리지 않는다.
            //
            // 쪽문_서 는 처음부터 [문 ▸ 경첩 ▸ 문짝] 으로 짜여 있고 제 부품도 달고 있었다.
            // 그것을 모르고 한 겹 더 씌웠더니 문 하나에 부품이 둘이 되어, 누르면 문짝이
            // 두 번 돌아간다. 게다가 문짝이 아니라 묶음에 콜라이더를 붙여 놓아,
            // 눈에 안 보이는 1m 상자가 문간을 막고 서 있었다.
            foreach (var leaf in leaves)
                if (leaf.GetComponentInChildren<DoorController>(true) != null)
                {
                    foreach (var l in leaves)
                    {
                        if (l.GetComponent<Renderer>() != null) continue;      // 진짜 문짝은 그대로 둔다
                        var stray = l.GetComponent<BoxCollider>();
                        if (stray != null) Object.DestroyImmediate(stray);     // 내가 잘못 붙인 상자를 뗀다
                    }
                    return false;
                }

            // 문짝이 어느 축으로 늘어서 있는지는 재서 안다. 사랑방 창호는 X 로 늘어서고,
            // 칸막이문은 Z 로 늘어선다 — 한쪽만 맞춰 두면 다른 쪽이 실처럼 서 버린다.
            Bounds span = RendererBounds(leaves[0]);
            foreach (var t in leaves) span.Encapsulate(RendererBounds(t));
            bool alongX = span.size.x >= span.size.z;

            leaves.Sort((a, b) => (alongX ? a.position.x : a.position.z).CompareTo(alongX ? b.position.x : b.position.z));

            // 어느 쪽으로 열릴 것인가 — 방 한가운데의 반대쪽, 곧 바깥이다.
            //
            // 부호를 아무렇게나 주면 문짝이 방 안으로 쓸고 들어온다. 甲은 나가려고 문 앞
            // 65cm 에 서 있으므로, 안으로 열리는 문은 그의 몸을 뚫고 지나간다. 한옥 창호도
            // 원래 밖으로 연다. 방 한가운데에서 이 칸이 어느 쪽에 붙어 있는지를 재면
            // 바깥이 어느 쪽인지 저절로 나온다 — 칸마다 손으로 적어 넣지 않아도 된다.
            float outward = alongX ? Mathf.Sign(span.center.z - hall.z) : Mathf.Sign(span.center.x - hall.x);
            if (outward == 0f) outward = 1f;
            float swing = 85f * outward;

            var holder = new GameObject(holderName);
            holder.transform.SetParent(group, false);

            var hinges = new List<Transform>();
            var angles = new List<float>();
            for (int i = 0; i < leaves.Count; i++)
            {
                var leaf = leaves[i];
                var lb = RendererBounds(leaf);
                float w = alongX ? lb.size.x : lb.size.z;

                // 콜라이더는 <b>보이는 문짝</b>에만 붙인다. 렌더러가 없는 것은 묶음이지 문짝이 아니다.
                if (leaf.GetComponent<Renderer>() != null && leaf.GetComponent<Collider>() == null)
                {
                    // 종잇장이라 두께가 5cm 다. 그대로면 겨냥이 어려워 얇은 축으로만 조금 두껍게.
                    var bc = leaf.gameObject.AddComponent<BoxCollider>();
                    Vector3 ls = leaf.localScale;
                    bc.size = alongX
                        ? new Vector3(1f, 1f, Mathf.Max(1f, 0.08f / Mathf.Max(0.001f, Mathf.Abs(ls.z))))
                        : new Vector3(Mathf.Max(1f, 0.08f / Mathf.Max(0.001f, Mathf.Abs(ls.x))), 1f, 1f);
                }

                bool firstHalf = i < leaves.Count / 2f;
                Vector3 edge = leaf.position;
                if (alongX) edge.x += firstHalf ? -w * 0.5f : w * 0.5f;
                else edge.z += firstHalf ? -w * 0.5f : w * 0.5f;

                var hinge = new GameObject("경첩_" + i);
                hinge.transform.SetParent(holder.transform, false);
                hinge.transform.position = edge;
                hinge.transform.rotation = leaf.rotation;
                leaf.SetParent(hinge.transform, true);

                hinges.Add(hinge.transform);
                // 경첩이 어느 모서리에 섰느냐에 따라 같은 바깥쪽이 반대 부호가 된다.
                angles.Add(firstHalf == alongX ? -swing : swing);
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
            return true;
        }

        /// <summary>씬 인스턴스에 남은 옛 흔적 치우기 — 빈 문 묶음과 손으로 옮긴 세간 자리.</summary>
        private static void CleanInstance()
        {
            var inst = GameObject.Find("사랑채_실내");
            if (inst == null) return;

            // 앞선 실패로 인스턴스에 얹혀 있던 빈 묶음(경첩도 문짝도 못 들어간 것)
            var strays = new List<GameObject>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("사랑방문_") && PrefabUtility.IsAddedGameObjectOverride(t.gameObject))
                    strays.Add(t.gameObject);
            foreach (var s in strays) Object.DestroyImmediate(s);

            // 프리팹에서 제자리를 잡았으니, 씬에 남은 손자국(위치 덮어쓰기)은 지운다.
            foreach (string n in new[] { "문갑", "장롱", "방석" })
            {
                var go = FindIn(inst, n);
                if (go == null) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                    PrefabUtility.RevertObjectOverride(go.transform, InteractionMode.AutomatedAction);
            }
        }

        // ── ② 씬 세간 ────────────────────────────────

        /// <summary>
        /// 씬에 그냥 놓여 있는 것(아궁이). 마당 바닥을 재서 그 위에 앉힌다.
        ///
        /// 여기서는 광선을 쓴다 — 실내와 반대다. 마당에 있는 것들은 늘 켜져 있어서 광선에
        /// 잡히고, 크기로 찾으면 오히려 틀린다: 고택 기단의 네모난 크기 상자가 부엌 쪽까지
        /// 덮고 있어서, 마당(-1.67)에 있어야 할 아궁이가 기단 윗면(-1.26)으로 41cm 솟았다.
        /// 돌계단은 네모가 아닌데 네모로 재니 그렇다.
        /// </summary>
        private static int SitSceneProps()
        {
            int n = 0;
            foreach (string name in new[] { "아궁이" })     // 부뚜막은 아궁이의 자식이라 같이 따라 올라간다
            {
                var go = FindInScene(name);
                if (go == null) { Debug.LogWarning("[사랑채] 못 찾음: " + name); continue; }

                Bounds b = WorldBounds(go);
                if (b.size == Vector3.zero) continue;

                // 한 점만 재면 안 된다. 아궁이는 기단 모서리에 <b>걸터앉아</b> 있어서,
                // 한가운데에서 재면 돌 윗면(-1.31)이 잡히고 그러면 마당 쪽 절반이 36cm
                // 공중에 뜬다. 발자국의 네 귀와 한가운데를 재서 <b>가장 낮은 면</b>에 앉힌다 —
                // 낮은 데를 딛고 서서 옆구리를 돌계단에 붙인 모양이 실제 아궁이다.
                float top = float.MinValue;
                foreach (var p in FootprintSamples(b))
                {
                    float y;
                    if (!NearestSurface(go, p, b.max.y, out y)) continue;
                    if (top == float.MinValue || y < top) top = y;
                }
                if (top == float.MinValue) { Debug.LogWarning("[사랑채] 앉을 면을 못 찾음: " + name); continue; }

                float delta = top - b.min.y;
                if (Mathf.Abs(delta) < 0.005f) continue;
                Undo.RecordObject(go.transform, "세간 앉히기");
                go.transform.position += new Vector3(0f, delta, 0f);
                n++;
            }
            return n;
        }

        /// <summary>발자국의 한가운데와 네 귀(조금 안쪽으로). 모서리에 걸친 것을 알아보려면 여러 점을 재야 한다.</summary>
        private static IEnumerable<Vector3> FootprintSamples(Bounds b)
        {
            float ix = b.extents.x * 0.8f, iz = b.extents.z * 0.8f;
            yield return b.center;
            yield return b.center + new Vector3(+ix, 0f, +iz);
            yield return b.center + new Vector3(+ix, 0f, -iz);
            yield return b.center + new Vector3(-ix, 0f, +iz);
            yield return b.center + new Vector3(-ix, 0f, -iz);
        }

        /// <summary>이 자리 바로 밑에 있는 면. 자기 자신과 물건보다 위에 있는 것은 세지 않는다.</summary>
        private static bool NearestSurface(GameObject self, Vector3 at, float objectTop, out float y)
        {
            y = 0f;
            var hits = Physics.RaycastAll(new Vector3(at.x, objectTop + 2f, at.z),
                                          Vector3.down, 16f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(self.transform)) continue;
                if (h.point.y > objectTop - 0.02f) continue;
                if (h.distance < nearest) { nearest = h.distance; y = h.point.y; found = true; }
            }
            return found;
        }

        // ── ③ 마주 앉기와 물러가기 ───────────────────

        /// <summary>
        /// 방에 들어서면 앉고, 심문이 끝나면 상대가 문을 열고 나가고, 그가 나가야 일어선다.
        ///
        /// 이 세 가지가 한 줄로 이어져야 뜻이 산다. 앉기만 하고 일어설 길이 없으면 갇히고,
        /// 상대가 안 나가면 그가 보는 앞에서 방을 뒤지게 되고, 나가는 신호가 없으면
        /// 보료를 언제 들춰도 되는지 알 수가 없다.
        /// </summary>
        private static string WireSeatAndLeave()
        {
            var cam = Camera.main;
            var 甲 = FindInScene("甲_가짜");
            var seatSpot = FindInScene("중문_도착지점");
            var bojaSpot = FindInScene("甲_보료자리");
            var bojaLift = FindInScene("보료_들추기");
            if (cam == null || 甲 == null || seatSpot == null || bojaSpot == null)
                return "  (배선 건너뜀 — 카메라·甲·자리 표식 중 하나를 못 찾음)";

            // 플레이어 앉히기
            var seat = cam.GetComponent<PlayerSeat>();
            if (seat == null) seat = Undo.AddComponent<PlayerSeat>(cam.gameObject);
            var sso = new SerializedObject(seat);
            sso.FindProperty("_seatSpot").objectReferenceValue = seatSpot.transform;
            sso.FindProperty("_lookAt").objectReferenceValue = bojaSpot.transform;
            sso.ApplyModifiedPropertiesWithoutUndo();

            // 나가는 길 — 문 앞에 서는 자리와 문 밖 자리
            float floorY = seatSpot.transform.position.y + 0.05f;   // 마루 윗면
            var doorFront = EnsureMarker("甲_나갈문앞", new Vector3(7.64f, floorY, -12.10f), 180f);
            var doorOut = EnsureMarker("甲_문밖", new Vector3(7.64f, floorY, -13.75f), 180f);

            var exitDoor = FindExitDoor();

            var bok = 甲.GetComponent<BokdongController>();
            if (bok == null) return "  (배선 건너뜀 — 甲에게 BokdongController 가 없음)";
            var bso = new SerializedObject(bok);
            bso.FindProperty("_leaveDoorSpot").objectReferenceValue = doorFront;
            bso.FindProperty("_leaveThroughSpot").objectReferenceValue = doorOut;
            bso.FindProperty("_leaveDoor").objectReferenceValue = exitDoor;
            bso.ApplyModifiedPropertiesWithoutUndo();

            // 방에 들어선 순간 → 앉는다
            var tz = Object.FindFirstObjectByType<TeleportZone>(FindObjectsInactive.Include);
            if (tz != null) SetCall(tz, "_onTeleported", seat, new UnityAction(seat.Sit));

            // '이만 마치겠소'를 누른 순간 → 일어서서 나간다.
            //
            // 그냥 창을 닫는 것(_onClosed)에 걸면 안 된다. 잠깐 창을 치우려고 누른 한 번에
            // 그가 자리를 떠 버리고, 다시 물을 길이 없다. 보료 들추기도 여기서 떼어 냈다 —
            // 그가 아직 그 위에 앉아 있는데 보료를 들출 수는 없다.
            var ic = 甲.GetComponent<InterrogationController>();
            if (ic != null)
            {
                ClearCalls(ic, "_onClosed");
                ClearCalls(ic, "_onFinished");
                SetCall(ic, "_onFinished", bok, new UnityAction(bok.LeaveRoom));
            }

            // 다 나간 순간 → 일어서고, 보료가 열린다
            ClearCalls(bok, "_onLeft");
            SetCall(bok, "_onLeft", seat, new UnityAction(seat.Stand));
            var rake = bojaLift != null ? bojaLift.GetComponent<AshRake>() : null;
            if (rake != null) SetCall(bok, "_onLeft", rake, new UnityAction(rake.Unlock));

            return "  앉을 자리=" + seatSpot.name +
                   " / 나갈 문=" + (exitDoor == null ? "없음(그냥 걸어 나감)" : exitDoor.name) +
                   " / 보료는 甲이 나간 뒤에 열림";
        }

        /// <summary>甲이 나갈 창호 칸의 문 부품. 프리팹을 저장한 뒤라 씬 인스턴스에 들어 있다.</summary>
        private static DoorController FindExitDoor()
        {
            var inst = GameObject.Find("사랑채_실내");
            if (inst == null) return null;
            var go = FindIn(inst, "사랑방문_" + ExitBay);
            return go != null ? go.GetComponent<DoorController>() : null;
        }

        /// <summary>자리 표식. 있으면 그 자리를 고쳐 쓰고, 없으면 만든다.</summary>
        private static Transform EnsureMarker(string name, Vector3 pos, float yaw)
        {
            var go = FindInScene(name);
            if (go == null)
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "자리 표식");
                var parent = FindInScene("게임요소");
                if (parent != null) go.transform.SetParent(parent.transform, true);
            }
            Undo.RecordObject(go.transform, "자리 표식");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go.transform;
        }

        // ── UnityEvent 잇기 ─────────────────────────
        //
        // 인스펙터에서 손으로 끄는 대신 코드로 잇는다. 씬을 다시 만들 때마다 손으로 다섯 군데를
        // 끌어다 놓으면 반드시 하나를 빠뜨린다. 이벤트 필드가 private 이라 리플렉션으로 꺼낸다.

        private static UnityEventBase GetEvent(Object owner, string field)
        {
            var f = owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            return f == null ? null : f.GetValue(owner) as UnityEventBase;
        }

        private static void ClearCalls(Object owner, string field)
        {
            var evt = GetEvent(owner, field);
            if (evt == null) return;
            for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(evt, i);
            EditorUtility.SetDirty(owner);
        }

        /// <summary>같은 대상·같은 함수가 이미 걸려 있으면 두 번 걸지 않는다.</summary>
        private static void SetCall(Object owner, string field, Object target, UnityAction call)
        {
            var evt = GetEvent(owner, field);
            if (evt == null) { Debug.LogWarning("[사랑채] 이벤트를 못 찾음: " + owner.name + "." + field); return; }

            for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                if (evt.GetPersistentTarget(i) == target && evt.GetPersistentMethodName(i) == call.Method.Name)
                    UnityEventTools.RemovePersistentListener(evt, i);

            UnityEventTools.AddVoidPersistentListener(evt, call);
            EditorUtility.SetDirty(owner);
        }

        // ── 도구 ────────────────────────────────────

        private static GameObject FindIn(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static GameObject FindInScene(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t.gameObject;
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

        private static Bounds RendererBounds(Transform t)
        {
            var r = t.GetComponent<Renderer>();
            if (r != null) return r.bounds;
            return WorldBounds(t.gameObject);
        }
    }
}
