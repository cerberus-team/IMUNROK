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

        /// <summary>
        /// 甲이 나가는 문 — 아궁이가 있는 뒤쪽 쪽문이다.
        ///
        /// 처음엔 보료에서 가장 가까운 사랑방 창호(세 짝)로 내보냈는데, 세 짝이 한꺼번에
        /// 펄럭이며 열리는 것이 요란하기만 했다. 쪽문은 한 짝이고 원래부터 제대로 짜여 있다.
        /// </summary>
        private const string ExitDoor = "쪽문_서";

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
                BuildBojaHinge(root);
                BuildMungapDoors(root);
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

        /// <summary>
        /// 보료를 <b>실제로 들릴 수 있게</b> 남쪽 끝에 경첩을 세운다.
        ///
        /// 지금까지 보료 들추기는 눈에 보이는 변화가 거의 없었다. 덮인 모습 쪽이 아예 비어
        /// 있어서, 눌러도 깔린 보료는 그대로고 접힌 귀퉁이 흉내 한 덩이만 슬쩍 나타났다 —
        /// 마루와 같은 재질이라 나타난 줄도 모른다. 들췄다는 것은 <b>그 물건이 들리는</b>
        /// 일이어야 한다. 남쪽 끝을 축으로 삼아 북쪽 끝이 들리게 하면, 밑에 깔린 별급문기가
        /// 있는 쪽(z −9.6)이 열린다.
        /// </summary>
        private static void BuildBojaHinge(GameObject root)
        {
            var props = FindIn(root, "소품");
            var boja = FindIn(root, "보료");
            if (props == null || boja == null) return;

            // 다시 재려면 일단 꺼낸다(여러 번 눌러도 같은 자리에 서게)
            boja.transform.SetParent(props.transform, true);

            var hinge = FindIn(root, "보료_경첩");
            if (hinge == null)
            {
                hinge = new GameObject("보료_경첩");
                hinge.transform.SetParent(props.transform, false);
            }

            Bounds bb = WorldBounds(boja);
            hinge.transform.rotation = Quaternion.identity;      // 세계의 X 축으로 젖히게
            hinge.transform.position = new Vector3(bb.center.x, bb.min.y, bb.min.z);
            boja.transform.SetParent(hinge.transform, true);
        }

        /// <summary>
        /// 문갑 좌우 여닫이문을 <b>모델의 제 뼈</b>로 여닫는다.
        ///
        /// 서랍과 마찬가지로 문짝도 모델에 들어 있다. 뼈를 하나씩 밀어 보고 찾았다 —
        /// <c>Dummy051_02</c> 가 왼쪽, <c>Dummy052_01</c> 이 오른쪽 문짝을 움직인다.
        /// 두 뼈는 마침 경첩 쇠붙이가 박힌 자리(x 15.07 · 16.16)에 그대로 서 있고
        /// 국소 Y축이 수직이라, 따로 경첩을 세울 것 없이 그 자리에서 돌리면 된다.
        ///
        /// 문짝은 방 쪽(+Z)으로 열린다. 경첩이 바깥 모서리에 있으므로 좌우가 반대 부호다.
        /// 겨냥할 콜라이더는 뼈에 달아 문과 함께 돌게 한다 — 문이 열렸는데 손대는 자리만
        /// 제자리에 남아 있으면 열린 문을 통과해 허공을 누르게 된다.
        /// </summary>
        private static void BuildMungapDoors(GameObject root)
        {
            var mungap = FindIn(root, "문갑");
            if (mungap == null) return;

            Transform left = null, right = null;
            foreach (var t in mungap.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Dummy051_02") left = t;
                if (t.name == "Dummy052_01") right = t;
            }
            if (left == null || right == null) { Debug.LogWarning("[사랑채] 문갑 문짝 뼈를 못 찾음"); return; }

            FitDoorCollider(left, +1f);
            FitDoorCollider(right, -1f);

            var dc = mungap.GetComponent<DoorController>();
            if (dc == null) dc = mungap.AddComponent<DoorController>();
            var so = new SerializedObject(dc);
            so.FindProperty("_motion").enumValueIndex = 0;          // 여닫이
            var arr = so.FindProperty("_leaves");
            arr.arraySize = 2;
            SetLeaf(arr.GetArrayElementAtIndex(0), left, -85f);     // 왼쪽은 음수라야 방 쪽으로 열린다
            SetLeaf(arr.GetArrayElementAtIndex(1), right, +85f);
            so.FindProperty("_openDuration").floatValue = 0.8f;
            so.FindProperty("_playerCanToggle").boolValue = true;
            so.FindProperty("_maxTouchDistance").floatValue = 2f;
            so.FindProperty("_locked").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLeaf(SerializedProperty el, Transform pivot, float angle)
        {
            el.FindPropertyRelative("pivot").objectReferenceValue = pivot;
            el.FindPropertyRelative("swingAngle").floatValue = angle;
            el.FindPropertyRelative("slideOffset").vector3Value = Vector3.zero;
        }

        /// <summary>
        /// 문짝 판에 맞춘 콜라이더를 경첩 뼈에 단다.
        ///
        /// 크기를 뼈의 제 자로 나눠 주어야 한다 — glTF 는 뿌리에 1/100 짜리 배율이 걸려 있어,
        /// 25cm 라고 적으면 2.5mm 짜리가 달린다.
        /// </summary>
        private static void FitDoorCollider(Transform bone, float side)
        {
            var bc = bone.GetComponent<BoxCollider>();
            if (bc == null) bc = bone.gameObject.AddComponent<BoxCollider>();

            // 경첩에서 문짝 한가운데까지 — 재서 얻은 값이다(문짝 폭 0.25, 경첩은 바깥 모서리).
            Vector3 panel = bone.position + new Vector3(side * 0.126f, 0.162f, -0.017f);
            Vector3 ls = bone.lossyScale;
            bc.center = bone.InverseTransformPoint(panel);
            bc.size = new Vector3(0.26f / Mathf.Max(0.0001f, Mathf.Abs(ls.x)),
                                  0.34f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)),
                                  0.05f / Mathf.Max(0.0001f, Mathf.Abs(ls.z)));
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
                // 이미 짜여 있으면 손대지 않는다.
                //
                // 다시 짜면 지금 문짝이 서 있는 자세를 '닫힌 자세'로 굳혀 버린다.
                // 문 하나가 열린 채로 저장돼 있으면 그 열린 자세가 닫힘이 되고,
                // 다음 번에 또 열면 그만큼 더 돌아간다 — 사랑방문_0 이 그렇게
                // 90도 자리에서 175도까지 밀려나 있었다.
                // 다시 짜야 할 일이 있으면 [사랑방 창호 다시 짜기] 를 쓴다.
                if (old.childCount > 0) return false;
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

                // 경첩은 <b>그려지는 한가운데</b>에서 재야 한다.
                //
                // 변환 위치로 재면 안 된다 — 이 문짝들은 원점이 제 한가운데가 아니라
                // 한쪽으로 반 장(0.41m) 치우쳐 있다. 그래서 한쪽 무리는 바깥 모서리에
                // 제대로 걸렸지만, 반대쪽 무리는 경첩이 <b>제 한가운데</b>에 놓여 회전문이
                // 되었다. 열어도 자리가 그대로고 몸만 도는 것이 그것이었다.
                bool firstHalf = i < leaves.Count / 2f;
                Vector3 edge = lb.center;
                if (alongX) edge.x += firstHalf ? -w * 0.5f : w * 0.5f;
                else edge.z += firstHalf ? -w * 0.5f : w * 0.5f;
                edge.y = leaf.position.y;

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
            // 쪽문은 서쪽 벽(x 4.60)에 있고, 나가면 기단(-1.27)을 딛고 아궁이 쪽으로 간다.
            float floorY = seatSpot.transform.position.y + 0.05f;   // 마루 윗면 -0.80
            var doorFront = EnsureMarker("甲_나갈문앞", new Vector3(5.25f, floorY, -12.63f), 270f);
            var doorOut = EnsureMarker("甲_문밖", new Vector3(4.00f, -1.27f, -12.63f), 270f);

            var exitDoor = FindExitDoor();

            var bok = 甲.GetComponent<BokdongController>();
            if (bok == null) return "  (배선 건너뜀 — 甲에게 BokdongController 가 없음)";
            var bso = new SerializedObject(bok);
            bso.FindProperty("_leaveDoorSpot").objectReferenceValue = doorFront;
            bso.FindProperty("_leaveThroughSpot").objectReferenceValue = doorOut;
            bso.FindProperty("_leaveDoor").objectReferenceValue = exitDoor;
            // 문 여는 손동작은 넣지 않는다 — 제대로 된 동작이 나오기 전까지는
            // 어설픈 시늉보다 그냥 지나가는 편이 낫다.
            bso.FindProperty("_leaveOpenState").stringValue = "";
            bso.ApplyModifiedPropertiesWithoutUndo();
            WireHandholds();
            UseRealFurniture();

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

        /// <summary>甲이 나갈 문 부품. 프리팹을 저장한 뒤라 씬 인스턴스에 들어 있다.</summary>
        private static DoorController FindExitDoor()
        {
            var inst = GameObject.Find("사랑채_실내");
            if (inst == null) return null;
            var go = FindIn(inst, ExitDoor);
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

        // ── 손으로 잡아 여는 것들 ────────────────────

        /// <summary>서랍이 다 빠져나오는 거리(m). 서랍 깊이가 0.36 이라 그보다 조금 덜 뺀다.</summary>
        private const float DrawerPull = 0.28f;

        /// <summary>
        /// 증거 셋을 <b>눌러 잡아야</b> 열리게 잇는다 — 보료·문갑 서랍·아궁이 재.
        ///
        /// 스쳐 지나가며 한 번 누른 것으로 증거가 손에 들어오면 조사한 것이 아니라 주운
        /// 것이 된다. 잡고 있는 동안 물건이 실제로 움직이므로 진행 막대를 따로 그릴 필요가
        /// 없다 — 들려 올라가는 보료가 곧 진행 막대다.
        /// </summary>
        private static void WireHandholds()
        {
            var inst = GameObject.Find("사랑채_실내");

            // 보료 — 남쪽 끝을 축으로 북쪽 끝이 들린다
            var hinge = inst != null ? FindIn(inst, "보료_경첩") : null;
            SetHold("보료_들추기", hinge != null ? hinge.transform : null,
                    new Vector3(-30f, 0f, 0f), Vector3.zero, 1.0f);

            // 접힌 귀퉁이 흉내는 이제 군더더기다 — 보료가 진짜로 들린다
            var fold = FindInScene("접힌_귀퉁이");
            if (fold != null && fold.activeSelf) { Undo.RecordObject(fold, "귀퉁이 끄기"); fold.SetActive(false); }

            // 문갑은 UseRealFurniture() 가 모델의 진짜 서랍으로 잇는다.

            // 아궁이 — 재가 눌리며 헤집힌다. 크게 움직일 것이 없으니 눌리는 것으로 알린다.
            var ash = FindInScene("재_덮인");
            SetHold("아궁이", ash != null ? ash.transform : null,
                    Vector3.zero, new Vector3(0f, -0.03f, 0f), 0.8f);
        }

        /// <summary>
        /// 조사할 때 뜨는 글을 짧게 자른다. 메뉴: [이문록 ▸ 사랑채 글귀 되돌리기]
        ///
        /// <b>왜 따로 떼어 놓았나</b>: 이건 인스펙터에 손으로 적어 넣은 문구를 덮어쓴다.
        /// 실내 정리와 한 몸으로 두면, 문이나 세간을 다시 앉히려고 메뉴를 누를 때마다
        /// 애써 고쳐 놓은 대사가 도로 원래 값으로 돌아간다. 문구를 여기 표에서 고칠
        /// 생각이 아니라면 이 메뉴는 누르지 않으면 된다.
        ///
        /// 물건을 가리킬 때마다 두 문장씩 떠오르면 읽다가 조사가 끊긴다. 헤드셋 안에서는
        /// 더하다 — 글이 눈앞 1.3m 에 떠 있어서, 길면 방을 통째로 가린다. 본 것을 한 마디로
        /// 적고, 자세한 사연은 수첩에 맡긴다.
        ///
        /// 글이 여기 있는 까닭: 씬을 다시 만들 때마다 손으로 다시 치면 반드시 어긋난다.
        /// 문구를 고치려면 이 표를 고친다.
        /// </summary>
        [MenuItem("이문록/사랑채 글귀 되돌리기")]
        private static void ShortenTexts()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[사랑채] 재생 중입니다 — 재생을 끄고 다시 누르십시오.");
                return;
            }

            SetWords("아궁이",
                     "한여름인데 불을 땐 자리다.",
                     "타다 만 서찰 조각이 나온다.",
                     "(눌러 잡고 헤집기)", null,
                     "[J10] 아궁이 재 속에 타다 만 서찰 조각.");

            SetWords("문갑_서랍",
                     "문서를 넣어 두는 궤다.",
                     "문서 몇 장이 개켜져 있다.",
                     "(눌러 잡고 서랍 빼기)", null,
                     "문갑 서랍에 사삿집 문서 여러 장. 하나하나가 따로 단서다.");   // 서랍 자체는 단서가 아니다

            SetWords("보료_들추기",
                     "주인이 앉아 있던 자리다.",
                     "밑에 별급문기 한 장이 깔려 있다.",
                     "(눌러 잡고 들추기)",
                     "주인이 그 위에 앉아 있다.",
                     "[J11] 보료 밑 별급문기. 재산을 '오래 부린 종 복동에게' 준다 — 아들이라는 말이 없다.");

            // 물러가며 남기는 말도 한 마디로
            var ch = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/Onggojip/Data/Gap_Interrogation.asset");
            if (ch != null)
            {
                var cso = new SerializedObject(ch);
                cso.FindProperty("closingLine").stringValue = "볼일이 있어 이만 물러가겠소.";
                cso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ch);
            }
            EditorSceneManagerMarkDirty();
            Debug.Log("[사랑채] 글귀를 표의 값으로 되돌렸습니다.");
        }

        private static void SetWords(string rakeName, string before, string after, string hint, string locked, string clue)
        {
            var go = FindInScene(rakeName);
            var rake = go != null ? go.GetComponent<AshRake>() : null;
            if (rake == null) return;

            var so = new SerializedObject(rake);
            so.FindProperty("_bodyBefore").stringValue = before;
            so.FindProperty("_bodyAfter").stringValue = after;
            so.FindProperty("_hint").stringValue = hint;
            if (locked != null) so.FindProperty("_lockedBody").stringValue = locked;
            if (clue != null) so.FindProperty("_clueText").stringValue = clue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 받아온 모델을 그대로 쓴다 ────────────────

        /// <summary>
        /// 대신 세워 둔 것을 치우고 <b>받아온 모델의 제 부재</b>를 쓴다.
        ///
        /// 문갑은 서랍이 든 모델이다. 그런데 서랍이 빠져나오는 자리에 회색 상자를 하나
        /// 만들어 세워 두고 그걸 서랍이라 했다 — 모델을 가져다 놓고 쓰지 않은 것이다.
        /// 뼈를 하나씩 밀어 보니 <c>Dummy053_00</c> 이 서랍 두 짝을,
        /// <c>Dummy051_02</c>·<c>Dummy052_01</c> 이 좌우 여닫이문을 움직인다.
        /// 그 뼈를 밀면 진짜 서랍이 나온다.
        ///
        /// 장부도 마찬가지다. 경상 위에 놓인 것이 기본 재질을 쓴 1m 짜리 흰 정육면체였다.
        /// 펼친 책 모델(<c>_책A</c>)은 정작 마당 구석에 떨어져 있었다.
        /// </summary>
        private static void UseRealFurniture()
        {
            var inst = GameObject.Find("사랑채_실내");
            if (inst == null) return;

            // ── 문갑: 모델에 든 진짜 서랍
            var bone = FindIn(inst, DrawerBone);
            var box = FindInScene("빠진_서랍");
            if (bone != null && box != null)
            {
                var fake = box.GetComponent<MeshRenderer>();
                if (fake != null && fake.enabled) fake.enabled = false;   // 회색 상자를 끈다

                // 문서가 서랍을 따라 나와야 하므로 서랍 뼈 밑으로 넣는다.
                if (box.transform.parent != bone.transform) box.transform.SetParent(bone.transform, true);

                // 윗서랍 안에 개켜 쌓는다. 종잇장이라 4mm 씩만 띄운다.
                box.transform.position = DrawerInside;
                var kids = new List<Transform>();
                foreach (Transform c in box.transform) kids.Add(c);
                kids.Sort((a, c) => string.CompareOrdinal(a.name, c.name));
                for (int i = 0; i < kids.Count; i++)
                    kids[i].position = DrawerInside + new Vector3(0f, i * 0.004f, 0f);

                // 빼내는 거리는 뼈의 제 좌표로 바꿔 준다 — glTF 는 뿌리에 회전과 배율이 걸려 있다.
                Vector3 pull = bone.transform.parent != null
                             ? bone.transform.parent.InverseTransformVector(new Vector3(0f, 0f, DrawerPull))
                             : new Vector3(0f, 0f, DrawerPull);
                SetHold("문갑_서랍", bone.transform, Vector3.zero, pull, 0.9f);

                var rake = FindInScene("문갑_서랍");
                if (rake != null)
                {
                    var so = new SerializedObject(rake.GetComponent<AshRake>());
                    so.FindProperty("_after").objectReferenceValue = box;   // 서랍 속 문서
                    so.ApplyModifiedPropertiesWithoutUndo();

                    // 손대는 자리를 서랍 앞面으로 당겨 온다. 옛 회색 상자가 80cm 나 빠져
                    // 나오던 시절의 자리라, 문갑에서 한 뼘 떨어진 허공에 떠 있었다.
                    // 좌우 문짝 자리까지 덮으면 문을 눌러도 서랍이 열린다 —
                    // 가운데 서랍 칸만 덮게 좁힌다.
                    Undo.RecordObject(rake.transform, "서랍 손잡이 자리");
                    rake.transform.position = new Vector3(15.61f, -0.52f, -13.86f);
                    var rc = rake.GetComponent<BoxCollider>();
                    if (rc != null)
                    {
                        rc.center = Vector3.zero;
                        rc.size = new Vector3(0.50f, 0.30f, 0.14f);
                    }
                }
            }

            // ── 장부: 흰 상자 대신 펼친 책
            var placeholder = FindInScene("장부");
            var book = FindInScene("_책A");
            var desk = FindIn(inst, "경상");
            if (book != null && desk != null)
            {
                Bounds db = WorldBounds(desk);
                Bounds bb = WorldBounds(book);
                float span = Mathf.Max(bb.size.x, bb.size.z);
                // 지금 크기를 재서 목표 크기로 맞춘다 — 곱해 나가지 않으므로 여러 번 눌러도 같다.
                if (span > 0.001f)
                {
                    book.transform.localScale *= BookSpan / span;
                    bb = WorldBounds(book);
                }
                book.transform.position += new Vector3(db.center.x - bb.center.x,
                                                       db.max.y - bb.min.y,
                                                       db.center.z - bb.center.z);
            }
            if (placeholder != null && placeholder.activeSelf)
            {
                Undo.RecordObject(placeholder, "흰 상자 끄기");
                placeholder.SetActive(false);       // 같은 단서(J06)가 둘이 되지 않게
            }
        }

        /// <summary>서랍 두 짝을 움직이는 뼈. 뼈를 하나씩 밀어 보고 찾았다.</summary>
        private const string DrawerBone = "Dummy053_00";

        /// <summary>윗서랍 안. 문서를 여기에 개켜 쌓는다.</summary>
        private static readonly Vector3 DrawerInside = new Vector3(15.61f, -0.40f, -14.09f);

        /// <summary>경상 위에 놓을 장부의 길이(m).</summary>
        private const float BookSpan = 0.34f;

        private static void SetHold(string rakeName, Transform hinge, Vector3 euler, Vector3 offset, float seconds)
        {
            var go = FindInScene(rakeName);
            var rake = go != null ? go.GetComponent<AshRake>() : null;
            if (rake == null) { Debug.LogWarning("[사랑채] 못 찾음: " + rakeName); return; }

            var so = new SerializedObject(rake);
            so.FindProperty("_hinge").objectReferenceValue = hinge;
            so.FindProperty("_liftEuler").vector3Value = euler;
            so.FindProperty("_liftOffset").vector3Value = offset;
            so.FindProperty("_holdSeconds").floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
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
