using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// 대화창 가림 점검 (2026-08-27). <b>판이 온전히 보이는가</b>를 씬 전체에 대고 실측한다.
    ///
    /// ■ 왜 만들었나
    ///   앉은 어머니에게 말을 걸면 판이 <b>상대보다 1 m 뒤</b>에 서서 치마·툇마루·댓돌·기둥에
    ///   잘렸다. 「조준이 되는가」(<see cref="NpcAimAudit"/>)와는 다른 결함이라 따로 잰다.
    ///
    /// ■ 재는 법
    ///   판의 네 귀퉁이를 얻어 <b>7×3 격자</b>로 점을 찍고, 눈에서 각 점까지 광선을 쏜다.
    ///   그 점보다 <b>앞에서</b> 무언가에 맞으면 그 칸은 가려진 것이다.
    ///   ⚠️ 광선은 콜라이더만 본다 — 그래서 <b>말 상대의 보이는 몸</b>(렌더러 AABB)은 따로 잰다.
    ///      어머니를 가린 주범이 바로 그것이다(조준 캡슐보다 치마가 훨씬 앞으로 나온다).
    ///
    /// ■ 쓰는 법
    ///   씬을 열고 <b>Play 중에</b> Tools ▸ 이문록 ▸ NPC ▸ 대화창 가림 점검.
    ///   숨어 있는 자리도 잠시 켜서 재고, 끝나면 되돌린다. 씬은 고치지 않는다.
    ///
    /// ⚠️ <b>서는 자리는 <see cref="NpcTestKit.Stand"/> 가 정한다 — 사람이 설 수 있는 자리인지
    ///    가리지 않는다.</b> 은하담 상인에서 그 자리가 돌다리 난간에 낀 자리라 「1/21 가림」이
    ///    나왔는데, 걷기 캡슐이 들어갈 수 있는 자리에서 다시 재니 0/21 이었다.
    ///    가림이 한두 칸으로 나오면 <b>그 자리에 정말 설 수 있는지부터</b> 볼 것
    ///    (설 자리 판정은 <see cref="NpcAimAudit"/> 쪽에 있다).
    /// </summary>
    public static class DialoguePanelAudit
    {
        const int GridX = 7, GridY = 3;

        public static string LastReport = "";

        [MenuItem("Tools/이문록/NPC/대화창 가림 점검 (Play 중)")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("대화창 가림 점검", "Play 중에 눌러야 한다.", "알았다");
                return;
            }
            EditorApplication.isPaused = false;
            Application.runInBackground = true;
            Begin();
        }

        class Job
        {
            public NpcDialogue npc;
            public bool hidden;
            public NpcSchedule sched; public bool schedWas;
            public Collider[] cols; public bool[] colWas;
            public Renderer[] rends; public bool[] rendWas;
            public Animator anim; public bool animWas;
            public bool talkWas;
        }

        static List<Job> jobs;
        static int at, waited;
        static StringBuilder report;
        /// <summary>2 = 리그가 물러나기를 끝내기를 기다림, 0 = 판이 자리잡기를 기다림, 1 = 닫는 중.
        /// ⚠️ 물러나기와 새 진입을 <b>같은 프레임에</b> 하면 포커스 리그가 새 진입을 흘려보낸다
        ///    (전이 중에는 Idle이 아니다). 그래서 Idle 이 될 때까지 기다렸다 말을 건다 —
        ///    이걸 빠뜨렸더니 열 자리 중 다섯이 「대화창이 열리지 않았다」로 나왔다.</summary>
        static int phase;

        static void Begin()
        {
            jobs = new List<Job>();
            foreach (var d in Object.FindObjectsByType<NpcDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                jobs.Add(new Job { npc = d });

            report = new StringBuilder();
            report.Append("[대화창 가림 점검] ")
                  .Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
                  .Append(" — ").Append(jobs.Count).AppendLine("자리\n");
            at = -1; waited = 0; phase = 0;
            EditorApplication.update -= Step;
            EditorApplication.update += Step;
            Next();
        }

        static void Next()
        {
            if (at >= 0 && at < jobs.Count) Restore(jobs[at]);
            at++; waited = 0; phase = 2;
            if (at >= jobs.Count) { Finish(); return; }

            var j = jobs[at];
            if (j.npc == null) { Next(); return; }
            var go = j.npc.gameObject;

            // 숨어 있는 자리도 재려면 잠시 켠다 (NpcSchedule 은 SetActive 가 아니라 부품을 하나씩 끈다)
            j.sched = go.GetComponent<NpcSchedule>();
            if (j.sched != null) { j.schedWas = j.sched.enabled; j.sched.enabled = false; }
            j.cols = go.GetComponentsInChildren<Collider>(true);
            j.colWas = new bool[j.cols.Length];
            for (int i = 0; i < j.cols.Length; i++) { j.colWas[i] = j.cols[i].enabled; j.cols[i].enabled = true; }
            j.rends = go.GetComponentsInChildren<Renderer>(true);
            j.rendWas = new bool[j.rends.Length];
            for (int i = 0; i < j.rends.Length; i++) { j.rendWas[i] = j.rends[i].enabled; j.rends[i].enabled = true; }
            j.anim = go.GetComponent<Animator>();
            if (j.anim != null) { j.animWas = j.anim.enabled; j.anim.enabled = true; }
            j.talkWas = j.npc.enabled;
            j.npc.enabled = true;

            j.hidden = !j.talkWas;
            foreach (var w in j.colWas) if (!w) j.hidden = true;
            // 말 걸기는 리그가 Idle 이 된 뒤에 (phase 2 → Step)
        }

        /// <summary>포커스 리그가 아무것도 붙들고 있지 않은가.</summary>
        static bool RigIdle()
        {
            var walk = Object.FindFirstObjectByType<DebugWalkController>();
            var rig = walk != null && walk.eye != null ? walk.eye.GetComponent<DebugFocusRig>() : null;
            if (rig == null) return true;
            var f = typeof(DebugFocusRig).GetField("phase",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f == null) return true;
            return f.GetValue(rig).ToString() == "Idle";
        }

        static void Restore(Job j)
        {
            if (j == null || j.npc == null) return;
            var walk = Object.FindFirstObjectByType<DebugWalkController>();
            var rig = walk != null && walk.eye != null ? walk.eye.GetComponent<DebugFocusRig>() : null;
            if (rig != null) rig.ExitFocus();

            if (j.cols != null) for (int i = 0; i < j.cols.Length; i++) if (j.cols[i] != null) j.cols[i].enabled = j.colWas[i];
            if (j.rends != null) for (int i = 0; i < j.rends.Length; i++) if (j.rends[i] != null) j.rends[i].enabled = j.rendWas[i];
            if (j.anim != null) j.anim.enabled = j.animWas;
            j.npc.enabled = j.talkWas;
            if (j.sched != null) j.sched.enabled = j.schedWas;
        }

        static void Step()
        {
            if (at < 0 || at >= jobs.Count) return;
            waited++;
            if (phase == 2)
            {
                if (!RigIdle() && waited < 200) return;      // 앞 사람에게서 물러나기를 끝낼 때까지
                NpcTestKit.Stand(jobs[at].npc.gameObject.name, true);
                phase = 0; waited = 0;
                return;
            }
            if (phase == 0)
            {
                if (waited < 45) return;          // 포커스 전이 + 판 자리잡기
                Measure(jobs[at]);
                phase = 1; waited = 0;
                return;
            }
            if (waited < 10) return;              // 물러나기까지 한 박자
            Next();
        }

        static void Finish()
        {
            EditorApplication.update -= Step;
            report.AppendLine("\n  가린 칸 0 = 판이 온전히 보인다.");
            LastReport = report.ToString();
            Debug.Log(LastReport);
        }

        static void Measure(Job j)
        {
            var npc = j.npc;
            report.Append("● ").Append(npc.gameObject.name);
            if (j.hidden) report.Append("  (숨은 자리 — 강제로 켜서 잼)");
            report.AppendLine();

            var ui = DialogueUI.Instance;
            if (ui == null || !ui.gameObject.activeInHierarchy || !ui.IsOpen)
            { report.AppendLine("   → ★대화창이 열리지 않았다★\n"); return; }

            var walk = Object.FindFirstObjectByType<DebugWalkController>();
            var eye = walk != null ? walk.eye : null;
            var cam = eye != null ? eye.GetComponent<Camera>() : null;
            if (eye == null) { report.AppendLine("   → 눈을 찾지 못했다\n"); return; }

            var rt = (RectTransform)ui.transform;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);   // 0=좌하 1=좌상 2=우상 3=우하

            float depth = Vector3.Dot(rt.position - eye.position, eye.forward);

            int blocked = 0, total = 0;
            var who = new Dictionary<string, int>();
            for (int iy = 0; iy < GridY; iy++)
                for (int ix = 0; ix < GridX; ix++)
                {
                    float fx = GridX == 1 ? 0.5f : ix / (float)(GridX - 1);
                    float fy = GridY == 1 ? 0.5f : iy / (float)(GridY - 1);
                    Vector3 bottom = Vector3.Lerp(c[0], c[3], fx);
                    Vector3 top = Vector3.Lerp(c[1], c[2], fx);
                    Vector3 p = Vector3.Lerp(bottom, top, fy);

                    total++;
                    Vector3 d = p - eye.position;
                    float len = d.magnitude;
                    RaycastHit hit;
                    if (!Physics.Raycast(eye.position, d / len, out hit, len - 0.02f, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                    blocked++;
                    string n = hit.collider.name;
                    int k; who.TryGetValue(n, out k); who[n] = k + 1;
                }

            // 말 상대의 보이는 몸 — 광선이 못 보는 것.
            // ⚠️ Renderer.bounds 는 못 쓴다: 이 모델들의 렌더러 AABB 는 바인드 자세 기준이라
            //    키 1.7 m 인 주모가 2.38 m 짜리 상자로 잡힌다. 지금 자세를 구워서 잰다.
            // ⚠️ 구운 것의 AABB 로 재면 안 된다 — 축 정렬 상자가 몸보다 한참 부풀어(서 있는 주모가
            //    폭 1.77 m) 앞면이 실제보다 훨씬 가깝게 나온다. 꼭짓점을 그대로 훑는다.
            //    (DialogueUI 가 쓰는 것과 같은 잣대여야 판정이 맞물린다)
            float bodyFront = float.MaxValue;
            var scratch = new Mesh();
            var verts = new List<Vector3>();
            foreach (var skin in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skin == null || !skin.enabled || skin.sharedMesh == null) continue;
                skin.BakeMesh(scratch, true);
                scratch.GetVertices(verts);
                var tr = skin.transform;
                for (int i = 0; i < verts.Count; i++)
                {
                    float dd = Vector3.Dot(tr.TransformPoint(verts[i]) - eye.position, eye.forward);
                    if (dd < bodyFront) bodyFront = dd;
                }
            }
            Object.DestroyImmediate(scratch);

            report.Append("   판 깊이 ").Append(depth.ToString("F2")).Append(" m   말 상대 몸 앞면 ")
                  .Append(bodyFront == float.MaxValue ? "?" : bodyFront.ToString("F2") + " m");
            report.Append(bodyFront < depth ? "   ★상대가 판보다 앞에 있다★" : "   (판이 상대보다 앞)").AppendLine();

            if (cam != null)
            {
                var v0 = cam.WorldToViewportPoint(c[0]);
                var v2 = cam.WorldToViewportPoint(c[2]);
                report.Append("   화면 자리 x ").Append(v0.x.ToString("F3")).Append("~").Append(v2.x.ToString("F3"))
                      .Append("  y ").Append(v0.y.ToString("F3")).Append("~").Append(v2.y.ToString("F3")).AppendLine();
            }

            report.Append("   가린 칸 ").Append(blocked).Append("/").Append(total);
            foreach (var kv in who) report.Append("   [").Append(kv.Key).Append(" ").Append(kv.Value).Append("]");
            report.AppendLine();
            report.Append("   → ").AppendLine(blocked == 0 && bodyFront >= depth ? "정상" : "★판이 가려진다★");
            report.AppendLine();
        }
    }
}
