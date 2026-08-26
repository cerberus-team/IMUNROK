using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// NPC 조준 점검 (2026-08-27). <b>말을 걸 수 있는가</b>를 씬 전체에 대고 실측한다.
    ///
    /// ■ 왜 만들었나 — 「검증했는데 안 되는」 사고
    ///   <see cref="NpcTestKit.Stand"/> 는 <c>Interact()</c> 를 <b>직접</b> 부른다. 그래서 대화가
    ///   열리는지는 확인되지만 <b>조준이 되는지는 한 번도 확인되지 않았다</b> — 실제 플레이에서
    ///   말을 거는 길은 <see cref="DebugInteractor"/> 의 조준선뿐인데, 검증 도구가 그 길을
    ///   건너뛴 것이다. 수령(짧은 캡슐 밖으로 나온 머리)과 어머니(건물 차단 상자 안)가
    ///   그 틈으로 빠져나갔다. 이 도구는 <b>조준선만</b> 본다.
    ///
    /// ■ 재는 법
    ///   사람이 설 수 있는 자리(사방 12방위 × 거리 넷)를 골라, 그 자리 눈높이에서
    ///   상대의 <b>머리·얼굴·가슴</b> 세 높이를 겨눈다. 판정은 <see cref="DebugInteractor"/> 와
    ///   같은 규칙이다 — 자기 몸은 건너뛰고, 그 다음 가장 가까운 표면 하나만 본다.
    ///
    /// ■ 쓰는 법
    ///   씬을 열고 <b>Play 중에</b> Tools ▸ 이문록 ▸ NPC ▸ 조준 점검. 몇 초 걸린다
    ///   (사람마다 자세가 자리잡기를 기다렸다 잰다). 결과는 콘솔에 표로 찍힌다.
    ///
    /// ■ 씬을 고치지 않는다
    ///   시간대·플래그로 숨어 있는 사람도 재려고 잠시 켜지만, 끝나면 원래대로 되돌린다.
    /// </summary>
    public static class NpcAimAudit
    {
        const float EyeHeight = 1.70f;          // DebugWalkController 의 눈높이
        const float PlayerRadius = 0.30f;
        const float PlayerHeight = 1.80f;
        const float MaxDistance = 3.5f;         // DebugInteractor.maxDistance 기본값
        static readonly float[] Radii = { 1.2f, 1.8f, 2.5f, 3.2f };
        const int Dirs = 12;

        /// <summary>마지막 점검 결과 — 다른 도구가 그대로 읽어 갈 수 있게 남긴다.</summary>
        public static string LastReport = "";

        [MenuItem("Tools/이문록/NPC/조준 점검 (Play 중)")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("조준 점검", "Play 중에 눌러야 한다.\n" +
                    "애니메이션이 돌아야 실제 자세를 잴 수 있다.", "알았다");
                return;
            }
            EditorApplication.isPaused = false;
            Application.runInBackground = true;
            Begin();
        }

        // ── 진행 (한 사람씩, 자세가 자리잡기를 기다렸다 잰다) ──────────
        /// <summary>
        /// 한 자리. 시간대·플래그로 <b>숨어 있는</b> 자리도 재려면 잠시 켜야 한다.
        /// ⚠️ <see cref="NpcSchedule"/> 는 <c>SetActive(false)</c> 가 아니라 <b>렌더러·콜라이더·
        ///    애니메이터를 하나씩 끄는</b> 방식이다 (꺼진 오브젝트는 Update 가 안 돌아 다시 못 켜므로).
        ///    그래서 여기서도 같은 것들을 하나씩 켜고, 끝나면 하나씩 되돌린다.
        /// </summary>
        class Job
        {
            public NpcDialogue npc;
            public bool wasActive;
            public NpcSchedule sched;
            public bool schedWasEnabled;
            public bool hidden;                       // 원래 숨어 있던 자리인가
            public Collider[] cols; public bool[] colWas;
            public Renderer[] rends; public bool[] rendWas;
            public Animator anim; public bool animWas;
        }

        static List<Job> jobs;
        static int at;
        static int waited;
        static StringBuilder report;

        static void Begin()
        {
            jobs = new List<Job>();
            foreach (var d in Object.FindObjectsByType<NpcDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                jobs.Add(new Job { npc = d });

            report = new StringBuilder();
            report.Append("[조준 점검] ").Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
                  .Append(" — ").Append(jobs.Count).AppendLine("자리\n");
            at = -1;
            waited = 0;
            EditorApplication.update -= Step;
            EditorApplication.update += Step;
            Next();
        }

        static void Next()
        {
            if (at >= 0 && at < jobs.Count) Restore(jobs[at]);
            at++;
            waited = 0;
            if (at >= jobs.Count) { Finish(); return; }

            var j = jobs[at];
            if (j.npc == null) { Next(); return; }

            // 시간대·플래그로 숨은 사람도 재려고 잠시 켠다 (끝나면 되돌린다)
            var go = j.npc.gameObject;
            j.wasActive = go.activeSelf;
            j.sched = j.npc.GetComponent<NpcSchedule>();
            if (j.sched != null) { j.schedWasEnabled = j.sched.enabled; j.sched.enabled = false; }
            if (!j.wasActive) go.SetActive(true);

            j.cols = go.GetComponentsInChildren<Collider>(true);
            j.colWas = new bool[j.cols.Length];
            for (int i = 0; i < j.cols.Length; i++) { j.colWas[i] = j.cols[i].enabled; j.cols[i].enabled = true; }

            j.rends = go.GetComponentsInChildren<Renderer>(true);
            j.rendWas = new bool[j.rends.Length];
            for (int i = 0; i < j.rends.Length; i++) { j.rendWas[i] = j.rends[i].enabled; j.rends[i].enabled = true; }

            j.anim = go.GetComponent<Animator>();
            if (j.anim != null) { j.animWas = j.anim.enabled; j.anim.enabled = true; }

            j.hidden = !j.wasActive;
            foreach (var was in j.colWas) if (!was) j.hidden = true;
            if (j.anim != null && !j.animWas) j.hidden = true;

            // 숨어 있던 사람은 조준 몸통도 아직 안 맞춰졌다 — 여기서 한 번 맞춘다
            // (평소에는 NpcDialogue 가 켜져 있는 동안 스스로 한다)
            var cap = go.GetComponent<CapsuleCollider>();
            if (j.hidden && cap != null) pendingFit = cap;
        }

        /// <summary>숨어 있던 자리의 조준 몸통 — 자세가 자리잡은 뒤에 맞춘다.</summary>
        static CapsuleCollider pendingFit;

        static void Restore(Job j)
        {
            if (j == null || j.npc == null) return;
            if (j.cols != null) for (int i = 0; i < j.cols.Length; i++) if (j.cols[i] != null) j.cols[i].enabled = j.colWas[i];
            if (j.rends != null) for (int i = 0; i < j.rends.Length; i++) if (j.rends[i] != null) j.rends[i].enabled = j.rendWas[i];
            if (j.anim != null) j.anim.enabled = j.animWas;
            if (j.sched != null) j.sched.enabled = j.schedWasEnabled;
            if (j.npc.gameObject.activeSelf != j.wasActive) j.npc.gameObject.SetActive(j.wasActive);
        }

        /// <summary>켠 직후에는 아직 바인드 자세다 — 애니메이터가 돌 틈을 준다.</summary>
        static void Step()
        {
            if (at < 0 || at >= jobs.Count) return;
            waited++;
            // 자세가 자리잡은 뒤에 조준 몸통을 맞춘다 (NpcDialogue 가 하는 것과 같은 순서)
            if (waited == 20 && pendingFit != null)
            { NpcAimBody.Fit(pendingFit.gameObject, pendingFit); pendingFit = null; }
            if (waited < 30) return;                   // 대략 0.5초
            Measure(jobs[at]);
            Next();
        }

        static void Finish()
        {
            EditorApplication.update -= Step;
            report.AppendLine("\n  ○ = 조준됨 / × = 막힘·빗나감.  '머리'가 ×면 얼굴을 겨눈 사람은 말을 못 건다.");
            LastReport = report.ToString();
            Debug.Log(LastReport);
        }

        // ── 한 사람 재기 ──────────────────────────────────────────
        static void Measure(Job j)
        {
            var npc = j.npc;
            var go = npc.gameObject;
            var cap = go.GetComponent<CapsuleCollider>();

            report.Append("● ").Append(go.name);
            if (j.hidden) report.Append("  (지금은 숨어 있는 자리 — 강제로 켜서 잼)");
            report.AppendLine();

            report.Append("   프로필=").Append(npc.profile == null ? "★없음★" : npc.profile.name)
                  .Append("  성격글=").Append(npc.profile == null ? 0 : npc.profile.persona.Length).Append("자")
                  .Append("  대화몸통=").Append(cap == null ? "★캡슐 없음★" : "루트 CapsuleCollider")
                  .AppendLine();

            if (cap == null || npc.profile == null) { report.AppendLine("   → ★설치가 덜 됐다★\n"); return; }

            if (!Body(go, out float bodyLo, out float bodyHi))
            { report.AppendLine("   → 스킨 메시가 없어 몸을 재지 못했다\n"); return; }

            float capLo = cap.bounds.min.y, capHi = cap.bounds.max.y;
            report.Append("   몸 y ").Append(bodyLo.ToString("F2")).Append("~").Append(bodyHi.ToString("F2"))
                  .Append("   캡슐 y ").Append(capLo.ToString("F2")).Append("~").Append(capHi.ToString("F2"));
            float over = bodyHi - capHi;
            report.Append(over > 0.02f ? "   ★머리가 " + over.ToString("F2") + "m 삐져나옴★" : "   (머리까지 덮음)")
                  .AppendLine();

            // 겨눌 세 높이 — 머리 / 얼굴(FocusPoint) / 가슴
            float headY = bodyHi - 0.06f;
            float faceY = npc.FocusPoint.y;
            float chestY = Mathf.Lerp(bodyLo, bodyHi, 0.45f);

            int stands = 0, okHead = 0, okFace = 0, okChest = 0;
            var blockers = new Dictionary<string, int>();
            Vector3 c = go.transform.position;

            foreach (float r in Radii)
                for (int i = 0; i < Dirs; i++)
                {
                    Vector3 p = c + Quaternion.Euler(0f, i * (360f / Dirs), 0f) * Vector3.forward * r;
                    if (!CanStand(go, p, out float floor)) continue;
                    Vector3 eye = new Vector3(p.x, floor + EyeHeight, p.z);
                    stands++;
                    if (Aim(go, eye, new Vector3(c.x, headY, c.z), blockers)) okHead++;
                    if (Aim(go, eye, new Vector3(c.x, faceY, c.z), blockers)) okFace++;
                    if (Aim(go, eye, new Vector3(c.x, chestY, c.z), blockers)) okChest++;
                }

            report.Append("   설 수 있는 자리 ").Append(stands).Append("곳 — 머리 ")
                  .Append(Mark(okHead, stands)).Append("  얼굴 ").Append(Mark(okFace, stands))
                  .Append("  가슴 ").Append(Mark(okChest, stands)).AppendLine();

            string worst = Worst(blockers);
            if (worst != null) report.Append("   주로 막는 것: ").AppendLine(worst);

            report.Append("   → ");
            if (stands == 0) report.AppendLine("★설 자리가 없다 — 다가갈 수 없는 자리★");
            else if (okFace == 0 && okChest == 0 && okHead == 0) report.AppendLine("★말을 걸 수 없다★");
            else if (okHead == 0) report.AppendLine("⚠ 얼굴 위쪽을 겨누면 빗나간다 (몸통을 겨눠야만 됨)");
            else if (okFace * 2 < stands) report.AppendLine("⚠ 되는 방향이 절반도 안 된다");
            else report.AppendLine("정상");
            report.AppendLine();
        }

        static string Mark(int ok, int total) =>
            total == 0 ? "─" : (ok == 0 ? "×" : (ok == total ? "○" : "△")) + " " + ok + "/" + total;

        static string Worst(Dictionary<string, int> blockers)
        {
            string name = null; int max = 0;
            foreach (var kv in blockers) if (kv.Value > max) { max = kv.Value; name = kv.Key; }
            return name == null ? null : name + " (" + max + "회)";
        }

        /// <summary>지금 자세 그대로 구운 몸의 위·아래 끝 (월드 y).</summary>
        static bool Body(GameObject go, out float lo, out float hi)
        {
            lo = float.MaxValue; hi = float.MinValue;
            var skins = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skins.Length == 0) return false;
            var mesh = new Mesh();
            foreach (var skin in skins)
            {
                if (skin.sharedMesh == null) continue;
                skin.BakeMesh(mesh, true);      // ⚠️ NpcAimBody 와 같은 이유로 켠다 (FBX 가 cm 단위)
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                                             (i & 2) == 0 ? b.min.y : b.max.y,
                                             (i & 4) == 0 ? b.min.z : b.max.z);
                    float y = skin.transform.TransformPoint(corner).y;
                    if (y < lo) lo = y;
                    if (y > hi) hi = y;
                }
            }
            Object.DestroyImmediate(mesh);
            return hi > lo;
        }

        /// <summary>거기에 사람이 설 수 있는가 — 발 디딜 바닥이 있고, 몸이 들어갈 틈이 있는가.</summary>
        static bool CanStand(GameObject npc, Vector3 p, out float floor)
        {
            floor = 0f;
            // ⚠️ 탐침을 너무 높은 데서 쏘면 <b>천장</b>을 바닥으로 읽는다 (서고에서 실측:
            //    6m 위에서 쏘아 −4.68 의 구조물을 바닥이라 보고 "설 자리 없음"으로 오판했다).
            //    사람 키보다 조금 위에서 시작한다.
            var hits = Physics.RaycastAll(new Ray(new Vector3(p.x, npc.transform.position.y + 2f, p.z), Vector3.down), 12f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.root == npc.transform) continue;
                // 몸통차단 상자 위는 지붕이지 바닥이 아니다 — 거기 설 수는 없다
                if (h.collider.gameObject.name == DebugInteractor.BodyBlockerName) continue;
                // ⚠️ 트리거 부피는 발을 디딜 바닥이 아니다. 안 걸렀더니 아이01의 노래 트리거
                //    (반지름 7m 구) 껍질을 바닥으로 읽어 눈높이가 6m 상공에 잡혔다.
                if (h.collider.isTrigger) continue;
                floor = h.point.y; found = true; break;
            }
            if (!found) return false;
            // NPC 발치에서 너무 높거나 낮은 곳(지붕·강바닥)은 접근로가 아니다
            if (Mathf.Abs(floor - npc.transform.position.y) > 1.6f) return false;

            Vector3 foot = new Vector3(p.x, floor + PlayerRadius + 0.05f, p.z);
            Vector3 head = new Vector3(p.x, floor + PlayerHeight - PlayerRadius, p.z);
            foreach (var c in Physics.OverlapCapsule(foot, head, PlayerRadius))
            {
                if (c.transform.root == npc.transform) continue;
                if (c is TerrainCollider) continue;
                if (c.isTrigger) continue;          // 통과할 수 있는 부피 (출구·노래 트리거)
                return false;                       // 몸이 들어갈 틈이 없다
            }
            return true;
        }

        /// <summary>몸통이 따로 있는 트리거 — 조준면이 아니다. <see cref="DebugInteractor"/> 와 같은 규칙.</summary>
        static bool EventVolume(Collider c)
        {
            if (!c.isTrigger) return false;
            foreach (var o in c.GetComponents<Collider>()) if (!o.isTrigger) return true;
            return false;
        }

        /// <summary><see cref="DebugInteractor"/> 와 같은 규칙으로 겨눈다.</summary>
        static bool Aim(GameObject npc, Vector3 eye, Vector3 target, Dictionary<string, int> blockers)
        {
            Vector3 dir = target - eye;
            if (dir.magnitude > MaxDistance) return false;
            var hits = Physics.RaycastAll(new Ray(eye, dir.normalized), MaxDistance);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.gameObject.name == DebugInteractor.BodyBlockerName) continue;
                if (EventVolume(h.collider)) continue;      // 사건 트리거 (DebugInteractor 와 같은 규칙)
                if (h.collider.transform.root == npc.transform) return true;
                string n = h.collider.name;
                blockers.TryGetValue(n, out int k);
                blockers[n] = k + 1;
                return false;
            }
            blockers.TryGetValue("(아무것도 안 맞음)", out int m);
            blockers["(아무것도 안 맞음)"] = m + 1;
            return false;
        }
    }
}
