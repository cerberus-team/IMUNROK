using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>심문은 문서고 안으로, 판결은 대청 교의로.</b>
    /// 메뉴: [이문록 ▸ 관아 ▸ ㉓ 심문은 문서고에서 판결은 대청에서]
    ///
    /// 두 번 헛짚고 나서 자리가 정해졌다. 처음엔 뜰에 세우고 마루에서 내려다보게 했고
    /// (⑫), 그 다음엔 대청 앞을 세살문으로 막아 방을 만들려 했다(㉒). 둘 다 아니었다 —
    /// <b>대청은 원래 앞이 트인 것</b>이고, 옆 세살문 너머에는 재 보니 방이 없다
    /// (바닥이 기단 1.65 에서 끊기고 그 너머는 바깥이다).
    ///
    /// <b>이 관아에서 문 열고 들어가는 진짜 실내는 문서고 하나뿐이다.</b> 여닫이 여섯 짝이
    /// 달렸고(⑮), 어둡게 해 두었고(⑯), 등경과 등불이 이미 그 어둠을 쓰라고 놓여 있다.
    /// 심문을 그리로 옮긴다. 재어 보니 마루가 <b>12m × 6m</b> 에 기둥 사이가 트여 있어
    /// 마주 앉고도 남는다.
    ///
    /// <b>두 자리가 갈린다.</b>
    ///   · <b>심문</b>은 문서고 안, 걸상 둘이 2.3m 를 두고 마주 본다. 어사가 문을 등지지
    ///     않고 <b>문 쪽을 보고</b> 앉아, 불려 온 사람이 문을 열고 들어오는 것을 본다.
    ///   · <b>판결</b>은 대청 <b>교의</b>다. 교의는 대청마루(2.21) 위 단(0.30) 에 놓인
    ///     이 집에서 가장 높은 자리라, 장계를 봉하는 자리로 그만이다. 서안을 교의 앞으로
    ///     끌어다 놓아, <b>앉은 그 자리에서 그대로 붓을 든다</b> — 구석으로 옮겨 앉는
    ///     군더더기가 없어진다.
    ///
    /// <b>발은 방향이 뒤집힌다.</b> 대청에서는 둘이 x 를 두고 마주 봐서 발을 90도 돌려
    /// 걸었는데, 문서고에서는 z 를 두고 마주 본다. 그대로 두면 발이 시선과 나란히 서서
    /// <b>3cm 짜리 옆구리</b>만 보인다 — 전에 한 번 그렇게 걸어 놓고 헤맸다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class GwanaSeogoCourt
    {
        /// <summary>문서고 마루 · 대청 마루.</summary>
        private const float Seogo = 0.82f, Maru = 2.21f;

        /// <summary>문서고에서 마주 앉는 줄(x). 기둥(-6·-3·-1·2)을 비껴간 자리다.</summary>
        private const float Line = -4.20f;

        /// <summary>어사 · 죄인 · 발이 서는 z. 사이가 2.30m 다.</summary>
        private const float JudgeZ = -9.40f, SeatZ = -7.10f, VeilZ = -8.25f;

        /// <summary>문 앞(바깥) · 문 안. 여닫이는 z -5.30 에 걸려 있다.</summary>
        private const float OutZ = -3.60f, InZ = -6.20f;

        /// <summary>발이 걸리는 높이. 문서고 도리가 3.40 이라 그 밑이다.</summary>
        private const float VeilY = 3.20f;

        [MenuItem("이문록/관아/㉓ 심문은 문서고에서 판결은 대청에서")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 심문을 문서고로, 판결을 대청으로 옮긴다\n");
            Interrogation(scene, log);
            Verdict(scene, log);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ── ① 심문을 문서고 안으로 ────────────────

        private static void Interrogation(Scene scene, System.Text.StringBuilder log)
        {
            // 죄인은 문 가까이, 어사는 안쪽. 둘이 z 를 두고 마주 본다.
            Put(scene, "죄인_걸상", new Vector3(Line, Seogo, SeatZ), 180f);
            Put(scene, "대청_앞자리", new Vector3(Line, Seogo, SeatZ), 180f);
            Put(scene, "어사_자리", new Vector3(Line, Seogo, JudgeZ), 0f);

            // 어사도 앉을 것이 있어야 한다. 문서고는 문서를 캐는 방이지 재판정이 아니라
            // 교의를 들이지 않는다 — 죄인의 걸상을 한 벌 더 베껴 마주 놓는다.
            var stool = Find(scene, "죄인_걸상");
            var mine = Find(scene, "어사_걸상");
            if (mine == null && stool != null)
            {
                var copy = Object.Instantiate(stool.gameObject);
                copy.name = "어사_걸상";
                Undo.RegisterCreatedObjectUndo(copy, "어사 걸상");
                // 그림만 남긴다. 부품이 딸려 오면 유령이 선다(겉짝이 여닫이를 물려받았던 일).
                foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null) Object.DestroyImmediate(mb);
                mine = copy.transform;
            }
            if (mine != null) mine.SetPositionAndRotation(new Vector3(Line, Seogo, JudgeZ), Quaternion.Euler(0f, 0f, 0f));

            // 발을 둘 사이에 건다. <b>도는 쪽이 뒤집힌다</b> — 대청에서는 x 를 두고
            // 마주 봐 90도였으나 여기서는 z 를 두고 마주 보므로 0도라야 시선을 가로막는다.
            var veil = Find(scene, "발");
            if (veil != null)
            {
                Undo.RecordObject(veil, "발 옮기기");
                veil.SetPositionAndRotation(new Vector3(Line, VeilY, VeilZ), Quaternion.Euler(0f, 0f, 0f));
                log.AppendLine("── 발을 문서고 안으로 (돌림 90° → 0°, 시선을 가로지르게)");
            }

            // 오가는 길. 뜰에서 곧장 문서고 앞으로 가고, 문 안에서 한 번 서서 걸상으로 든다.
            var outSpot = Spot(scene, "문서고_문앞", new Vector3(Line, 0f, OutZ), 180f);
            var inSpot = Spot(scene, "문서고_문안", new Vector3(Line, Seogo, InZ), 180f);

            // 여닫이 여섯을 다 모아 CourtSummon 에 건다 — 불리면 열리고 물러나면 닫힌다.
            var doors = new System.Collections.Generic.List<SwingDoor>();
            foreach (var d in Object.FindObjectsByType<SwingDoor>(FindObjectsSortMode.None))
                if (d.transform.root.name == "문서고") doors.Add(d);

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
                    via.GetArrayElementAtIndex(0).objectReferenceValue = outSpot;
                    via.GetArrayElementAtIndex(1).objectReferenceValue = inSpot;
                    so.FindProperty("_frontSpot").objectReferenceValue = front;
                    so.FindProperty("_walkBool").stringValue = "Walking";
                    so.FindProperty("_sitBool").stringValue = "Sitting";
                    var dp = so.FindProperty("_doors");
                    dp.arraySize = doors.Count;
                    for (int i = 0; i < doors.Count; i++) dp.GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(cs);
                    n++;
                }
            log.AppendLine("── 심문 자리: 문서고 안 x " + Line.ToString("F2")
                         + " · 어사 z " + JudgeZ.ToString("F2") + " · 죄인 z " + SeatZ.ToString("F2") + " (사이 2.30m)");
            log.AppendLine("── " + n + "사람이 문(" + doors.Count + "짝)을 열고 들어와 앉는다");
        }

        // ── ② 판결을 대청 교의로 ──────────────────

        private static void Verdict(Scene scene, System.Text.StringBuilder log)
        {
            // 교의·단은 대청에 그대로 둔다. 이 집에서 제일 높은 자리라 장계를 봉할 자리다.
            var chair = Find(scene, "어사_교의");
            if (chair == null) { log.AppendLine("── 교의를 못 찾았다"); return; }

            // 서안을 교의 앞으로 끌어다 놓는다. 앉은 그 자리에서 그대로 붓을 든다.
            var desk = Find(scene, "서안_자리");
            if (desk != null)
            {
                Undo.RecordObject(desk, "서안 옮기기");
                desk.SetPositionAndRotation(new Vector3(14.35f, Maru, 0f), Quaternion.Euler(0f, 90f, 0f));
            }

            // 앉는 자리는 교의 그 자리. 서안 밑에 있던 표식을 교의 위로 옮긴다.
            var seat = desk != null ? Deep(desk, "앉는자리") : null;
            if (seat == null)
            {
                var go = new GameObject("앉는자리");
                Undo.RegisterCreatedObjectUndo(go, "앉는자리");
                if (desk != null) go.transform.SetParent(desk, true);
                seat = go.transform;
            }
            seat.SetPositionAndRotation(new Vector3(15.30f, Maru, 0f), Quaternion.Euler(0f, 270f, 0f));

            var rd = desk != null ? desk.GetComponent<ReportDesk>() : null;
            if (rd != null)
            {
                var so = new SerializedObject(rd);
                so.FindProperty("_seat").objectReferenceValue = seat;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rd);
            }
            log.AppendLine("── 판결 자리: 대청 교의(15.30, " + Maru.ToString("F2") + ", 0) · 서안을 그 앞 14.35 로");
        }

        // ── 잔심부름 ──────────────────────────────

        private static void Put(Scene scene, string name, Vector3 pos, float yaw)
        {
            var t = Find(scene, name);
            if (t == null) return;
            Undo.RecordObject(t, "자리 옮기기");
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        }

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

        private static Transform Deep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
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
