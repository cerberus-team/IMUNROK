using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>문을 달고, 열고 들어와 앉게 한다.</b> 메뉴: [이문록 ▸ 관아 ▸ ㉒ 문 달고 들어와 앉게]
    ///
    /// ⑫에서 심문을 대청으로 들였는데, <b>안으로 다 들어오지 못했다</b>. 재어 보니
    /// 동헌의 방은 이렇게 생겼다:
    ///
    ///   뒤(+x) 17.06 벽 · 옆(±z) 4.52 세살문 벽 · <b>앞(-x) 12.85 는 통째로 트임</b>
    ///
    /// 어사는 15.30 이라 방 안인데 불려 온 사람은 <b>12.40</b> — 문선(12.85)보다
    /// 반 자 앞이라 방 밖이었다. 옆에 종이 벽이 서 있어도 저 혼자 처마 밑에 선
    /// 것이라, 실내에서 마주 앉는 그림이 안 나왔다.
    ///
    /// 그래서 셋을 한다:
    ///   ① <b>자리를 안으로</b> — 걸상을 13.35 로 들이고 발을 그 사이(14.35)로 옮긴다.
    ///   ② <b>앞에 문을 단다</b> — 트인 앞(9.04m)에 세살문 넉 짝을 건다. 이제 방이
    ///      네 면으로 닫힌다. 불리면 <b>문이 먼저 열리고</b> 그 사이로 걸어 들어온다.
    ///   ③ <b>앉힌다</b> — 다섯 사람 모두 Stand_To_Sit · Sitting_Idle · Sit_To_Stand 를
    ///      이미 지니고 있고 몸짓표에 <c>Sitting</c> 이 걸려 있다. 불러 놓고 세워만
    ///      두던 것을 이제 앉힌다.
    ///
    /// <b>문은 걷기 시작할 때 연다.</b> 다다라서 열면 코앞에서 문이 밀리는 꼴이 된다.
    /// 물러갈 때는 거꾸로, <b>다 나가고 나서</b> 닫는다 — 나가는 중에 닫으면 문이
    /// 사람을 뚫고 지나간다.
    ///
    /// <b>계단은 그대로 둔다.</b> 오르는 것을 안 봐도 된다 하셨으나, 뜰에서 이름이
    /// 불려 계단을 오르는 그 대여섯 걸음이 "불려 들어간다"는 말을 대신하고 이미
    /// 돌아가고 있어 굳이 걷어낼 까닭이 없다. 계단 위 <b>문 앞(11.90)</b>에 경유점을
    /// 하나 더 두어, 옆으로 새지 않고 문으로 들어오게만 한다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class GwanaInnerCourt
    {
        /// <summary>대청 바닥.</summary>
        private const float Maru = 2.21f;

        /// <summary>문선이 서는 선 — 방의 앞면.</summary>
        private const float DoorX = 12.85f;

        /// <summary>옆 세살문 벽이 서는 z. 앞문도 이 폭에 맞춰 건다.</summary>
        private const float ZHalf = 4.52f;

        /// <summary>세살문 한 짝의 위아래.</summary>
        private const float DoorLow = 2.21f, DoorHigh = 4.07f;

        /// <summary>죄인이 앉는 자리 · 발이 걸리는 자리 · 문 앞에 서는 자리.</summary>
        private const float SeatX = 13.35f, VeilX = 14.35f, PorchX = 11.90f;

        private const string GroupName = "동헌_앞문";

        [MenuItem("이문록/관아/㉒ 문 달고 들어와 앉게")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 문을 달고 들어와 앉게 한다\n");
            MoveSeats(scene, log);
            var doors = HangFront(scene, log);
            var porch = Spot(scene, "문_앞자리", new Vector3(PorchX, Maru, 0f), 90f);
            Lamp(scene, log);
            Rewire(scene, doors, porch, log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ── ① 자리를 안으로 ───────────────────────

        private static void MoveSeats(Scene scene, System.Text.StringBuilder log)
        {
            var seat = Find(scene, "죄인_걸상");
            if (seat != null)
            {
                Undo.RecordObject(seat, "걸상 들이기");
                var p = seat.position; p.x = SeatX; seat.position = p;
                log.AppendLine("── 걸상을 " + SeatX.ToString("F2") + " 로 들였다 (문선 " + DoorX.ToString("F2") + " 안쪽)");
            }
            var veil = Find(scene, "발");
            if (veil != null)
            {
                Undo.RecordObject(veil, "발 옮기기");
                var p = veil.position; p.x = VeilX; veil.position = p;
                log.AppendLine("── 발을 " + VeilX.ToString("F2") + " 로 옮겼다 — 걸상과 교의 사이");
            }
            // 앞에 서던 자리도 걸상 위로 옮긴다. 여기까지 걸어와 앉는다.
            var front = Find(scene, "대청_앞자리");
            if (front != null)
            {
                Undo.RecordObject(front, "앞자리 들이기");
                var p = front.position; p.x = SeatX; front.position = p;
            }
        }

        // ── ② 앞에 문을 단다 ──────────────────────

        /// <summary>
        /// 트인 앞면에 세살문 넉 짝을 건다.
        ///
        /// <b>있는 문짝을 베껴 쓴다.</b> 옆벽의 세살문과 같은 그림이라야 한 집으로
        /// 보인다. 다만 그 문짝은 z 를 보고 서 있으므로 <b>90도 돌려</b> x 를 보게
        /// 세운다. 베낀 것에는 그림만 남긴다 — 부품이 딸려 오면 지난번 겉짝처럼
        /// 유령이 선다.
        ///
        /// 넉 짝이 둘씩 짝지어 <b>-2.26 과 +2.26 에서 맞물리고</b>, 열리면 바깥(-x)
        /// 으로 접힌다. 돌쩌귀는 짝마다 바깥 세로변이다.
        /// </summary>
        private static SwingDoor[] HangFront(Scene scene, System.Text.StringBuilder log)
        {
            var old = Find(scene, GroupName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var src = FindLeafSource(scene);
            if (src == null) { log.AppendLine("── 베낄 세살문을 못 찾았다"); return new SwingDoor[0]; }

            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "앞문");

            float span = ZHalf * 2f;            // 9.04m
            float pane = span / 8f;             // 한 장 1.13m
            // 짝마다 { 첫 장 번호, 돌쩌귀 z, 여는 각 }
            //
            // <b>돌쩌귀는 넷 다 바깥쪽이다.</b> 처음에는 안쪽 두 짝을 한가운데(0)에
            // 달았는데, 그러면 열릴 때 두 짝이 <b>한가운데로 뻗어 나와</b> 들어오는
            // 길을 막았다 — 문 앞(11.90)에서 걸상(13.35)으로 가는 그 선 위에 문짝이
            // 가로누웠다. 넷을 다 바깥으로 접으면 가운데가 뚫린 채 열린다.
            float[][] leaves = {
                new float[] { 0f, -ZHalf,        -85f },
                new float[] { 2f, -ZHalf * 0.5f, -85f },
                new float[] { 4f,  ZHalf * 0.5f,  85f },
                new float[] { 6f,  ZHalf,         85f },
            };
            string[] tag = { "남바깥", "남안", "북안", "북바깥" };

            var made = new List<SwingDoor>();
            var mem = new List<Transform[]>();
            for (int k = 0; k < leaves.Length; k++)
            {
                var leaf = new GameObject("앞문_" + tag[k]);
                Undo.RegisterCreatedObjectUndo(leaf, "앞문");
                leaf.transform.SetParent(group.transform, true);

                var parts = new List<Transform>();
                for (int j = 0; j < 2; j++)
                {
                    int idx = (int)leaves[k][0] + j;
                    float zc = -ZHalf + pane * (idx + 0.5f);
                    var pn = Clone(src, leaf.transform, new Vector3(DoorX, (DoorLow + DoorHigh) * 0.5f, zc));
                    pn.name = "짝_" + tag[k] + "_" + (j + 1);
                    parts.Add(pn.transform);
                }

                // 누를 것이 있어야 열린다. 짝 전체를 덮는 상자를 짝 뿌리에 준다.
                var box = Undo.AddComponent<BoxCollider>(leaf);
                float z0 = -ZHalf + pane * (int)leaves[k][0];
                box.center = new Vector3(DoorX, (DoorLow + DoorHigh) * 0.5f, z0 + pane);
                box.size = new Vector3(0.12f, DoorHigh - DoorLow, pane * 2f);

                var sd = Undo.AddComponent<SwingDoor>(leaf);
                made.Add(sd);
                mem.Add(parts.ToArray());
            }

            // 짝지어 함께 돌게 묶는다 — 한 짝만 열리는 문은 없다.
            // 짝을 다 세우고 나서야 서로를 가리킬 수 있으니 여기서 한 번에 매긴다.
            int[] mate = { 1, 0, 3, 2 };
            for (int k = 0; k < made.Count; k++)
                made[k].Setup(mem[k],
                              new Vector3(DoorX, Maru, leaves[k][1]),
                              leaves[k][2],
                              new[] { made[mate[k]] });

            log.AppendLine("── 앞문 " + made.Count + "짝 (한 장 " + pane.ToString("F2") + "m · 폭 " + span.ToString("F2") + "m)");
            return made.ToArray();
        }

        /// <summary>옆벽에 걸린 세살문 한 장을 고른다 — 겉짝·맞댐대가 아닌 본짝이라야 한다.</summary>
        private static Transform FindLeafSource(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("SM_Door_Sesal")) continue;
                    if (t.name.Contains("겉") || t.name.Contains("맞댐")) continue;
                    if (t.GetComponent<MeshFilter>() == null) continue;
                    return t;
                }
            return null;
        }

        /// <summary>
        /// 문짝 한 장을 베껴 90도 돌려 세운다. 재고 나서 옮기므로 원본이 어디에 서
        /// 있었든 상관없다 — <b>범위의 가운데</b>를 달라는 자리에 맞춘다.
        /// </summary>
        private static GameObject Clone(Transform src, Transform parent, Vector3 want)
        {
            var copy = Object.Instantiate(src.gameObject, parent);
            Undo.RegisterCreatedObjectUndo(copy, "문짝");

            // 그림만 남긴다. 부품이 딸려 오면 유령이 선다(겉짝이 여닫이를 물려받았던 일).
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) Object.DestroyImmediate(mb);

            // <b>세계 기준으로 돌린다.</b> src.rotation 뒤에 곱하면 문짝 <i>제</i> 축을
            // 도는데, 이 집 문짝은 제 축이 누워 있어 그러면 문이 <b>상처럼 눕는다</b> —
            // 처음에 그렇게 깔아 놓고 왜 안 보이나 했다. 앞에 곱해 세로축으로 돌린다.
            copy.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * src.rotation;

            var r = copy.GetComponentInChildren<Renderer>(true);
            if (r != null) copy.transform.position += want - r.bounds.center;
            return copy;
        }

        // ── ③ 방을 밝힌다 ────────────────────────

        /// <summary>
        /// <b>앞을 막았더니 방이 캄캄해졌다.</b>
        ///
        /// 그전에는 앞이 통째로 트여 아침 해가 마루 깊숙이 들어왔다. 문을 달고 나니
        /// 지붕 밑에 남은 것이 하늘빛(ambient)뿐이라, 마주 앉은 사람의 얼굴이 안 보인다.
        /// 이 게임의 심문은 <b>얼굴을 읽는 일</b>이라 그러면 방을 만든 뜻이 없어진다.
        ///
        /// 등불을 하나 켜는 대신 <b>창호지로 든 낮빛</b>을 흉내낸다 — 문 안쪽에 넓고
        /// 흐린 점등 하나. 그림자를 안 지우므로(이 씬은 그림자를 안 쓴다) 값싸고,
        /// 종이를 지난 빛처럼 누렇게 데워 둔다.
        /// </summary>
        private static void Lamp(Scene scene, System.Text.StringBuilder log)
        {
            var t = Find(scene, "동헌_방빛");
            if (t == null)
            {
                var go = new GameObject("동헌_방빛");
                Undo.RegisterCreatedObjectUndo(go, "방빛");
                t = go.transform;
            }
            t.position = new Vector3(14.0f, 3.55f, 0f);
            var l = t.GetComponent<Light>();
            if (l == null) l = Undo.AddComponent<Light>(t.gameObject);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.94f, 0.82f);
            l.intensity = 2.6f;
            l.range = 11f;
            l.shadows = LightShadows.None;
            EditorUtility.SetDirty(l);
            log.AppendLine("── 방빛 하나 (창호지로 든 낮빛 흉내)");
        }

        // ── ④ 걸어 들어와 앉게 ────────────────────

        private static void Rewire(Scene scene, SwingDoor[] doors, Transform porch, System.Text.StringBuilder log)
        {
            var stair = Find(scene, "뜰_계단아래");
            var front = Find(scene, "대청_앞자리");
            int n = 0;

            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var cs = t.GetComponent<CourtSummon>();
                    if (cs == null) continue;

                    var so = new SerializedObject(cs);
                    var via = so.FindProperty("_via");
                    via.arraySize = 2;
                    via.GetArrayElementAtIndex(0).objectReferenceValue = stair;
                    via.GetArrayElementAtIndex(1).objectReferenceValue = porch;
                    if (front != null) so.FindProperty("_frontSpot").objectReferenceValue = front;
                    so.FindProperty("_walkBool").stringValue = "Walking";
                    so.FindProperty("_sitBool").stringValue = "Sitting";
                    var dp = so.FindProperty("_doors");
                    dp.arraySize = doors.Length;
                    for (int i = 0; i < doors.Length; i++) dp.GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(cs);
                    n++;
                }
            log.AppendLine("── " + n + "사람에게 다시 매겼다 — 계단 → 문 앞 → 걸상, 걸으며 Walking, 다다르면 Sitting");
        }

        // ── 잔심부름 ──────────────────────────────

        private static Transform Spot(Scene scene, string name, Vector3 pos, float yaw)
        {
            var t = Find(scene, name);
            if (t == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "자리");
                t = go.transform;
            }
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        private static Transform Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            }
            return null;
        }
    }
}
