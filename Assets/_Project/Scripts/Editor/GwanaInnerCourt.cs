using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>걸어 들어와 앉게 한다.</b> 메뉴: [이문록 ▸ 관아 ▸ ㉒ 걸어 들어와 앉게]
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

        [MenuItem("이문록/관아/㉒ 걸어 들어와 앉게")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 문을 달고 들어와 앉게 한다\n");
            Undo_Front(scene, log);
            MoveSeats(scene, log);
            var porch = Spot(scene, "문_앞자리", new Vector3(PorchX, Maru, 0f), 90f);
            Rewire(scene, new SwingDoor[0], porch, log);

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

        // ── ② 앞문을 걷어낸다 ─────────────────

        /// <summary>
        /// <b>앞에 달았던 문을 도로 뗀다.</b>
        ///
        /// "실내에서" 라는 말을 <b>트인 앞을 막으라</b>는 뜻으로 읽고 세살문 넉 짝을
        /// 걸었는데, 그게 아니었다. 대청은 <b>원래 앞이 트인 것</b>이 맞다 — 마루에
        /// 앉아 뜰을 내다보는 그 트임이 동헌이라는 집의 생김새다. 막아 놓으니
        /// 어사가 뜰을 못 보고 방까지 캄캄해졌다.
        ///
        /// 앞을 막으려고 켰던 방빛도 같이 끈다 — 앞이 트이면 아침 해가 도로 든다.
        /// </summary>
        private static void Undo_Front(Scene scene, System.Text.StringBuilder log)
        {
            foreach (var n in new[] { GroupName, "동헌_방빛" })
            {
                var t = Find(scene, n);
                if (t == null) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
                log.AppendLine("── " + n + " 을 걷어냈다");
            }
        }

        // ── ③ 걸어 들어와 앉게 ────────────────

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
