using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 바깥을 <b>땅에 앉히고 몸을 붙인다</b>.
    /// 메뉴: [이문록 ▸ 조사청 ▸ 바닥에 앉히고 몸 붙이기]
    ///
    /// <b>① 땅에 앉힌다</b>: 흩뿌릴 때 자리를 (x, z) 로만 잡고 y 는 땅 높이를 그대로
    /// 넣었다. 그런데 프리팹마다 <b>피벗이 어디 있는지가 다르다</b> — 밑동에 있는 것도
    /// 있고 한가운데 있는 것도 있다. 그래서 같은 y 를 넣어도 어떤 나무는 뿌리가 땅에
    /// 묻히고 어떤 나무는 반 자쯤 떠 있다. 재어 보니 -0.61m 에서 +0.53m 까지 벌어져
    /// 있었다. 자리는 그대로 두고 <b>제 발밑이 땅에 닿도록</b> y 만 고쳐 앉힌다.
    ///
    /// <b>② 몸을 붙인다</b>: 나무 열아홉 그루에 콜라이더가 하나도 없었다. 지붕도 벽도
    /// 마찬가지다. 걸어가면 그냥 통과한다. 다만 <b>다 붙이지는 않는다</b>:
    ///   · 나무 — 줄기에만 가는 기둥을 세운다. 잎까지 막으면 가지 밑을 못 지나간다.
    ///   · 꽃·풀 — 안 붙인다. 풀은 헤치고 다니는 것이다.
    ///   · 살·서까래·도리 — 안 붙인다. 손이 닿지 않는 높이고, 살만 246 대다.
    ///   · 벽·지붕·기둥·주춧돌·창틀·문틀 — 붙인다. 통과하면 안 되는 것들이다.
    ///
    /// 이미 몸이 있는 것은 건드리지 않는다. 다시 눌러도 덧나지 않는다.
    ///
    /// ★플레이를 멈추고 실행할 것.
    /// </summary>
    public static class HubSolid
    {
        private const string RoomName = "조사청_실내";
        private const string FieldName = "조사청_들판";
        private const string YardName = "조사청_마당";

        /// <summary>땅에 이만큼 묻어 앉힌다(m). 딱 맞추면 이가 뜬 자리에 그림자가 샌다.</summary>
        private const float Bury = 0.03f;

        /// <summary>줄기 기둥의 굵기 — 밑동 너비의 이만큼(0~1).</summary>
        private const float TrunkShare = 0.16f;

        [MenuItem("이문록/조사청/바닥에 앉히고 몸 붙이기")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[앉히기] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var room = GameObject.Find(RoomName);
            if (room == null) { Debug.LogError("[앉히기] " + RoomName + " 을 못 찾았습니다."); return; }

            float ground = GroundTop();
            if (float.IsNaN(ground)) { Debug.LogError("[앉히기] 평지를 못 찾았습니다."); return; }

            var log = new StringBuilder();
            log.AppendLine("   땅 윗면 y = " + ground.ToString("F2"));

            Sit(FieldName, new[] { "나무", "바위", "꽃", "풀" }, ground, log);
            Sit(YardName, new[] { "디딤돌", "문바위", "마당나무" }, ground, log);

            Trunks(FieldName, "나무", log);
            Trunks(YardName, "마당나무", log);
            Blocks(YardName, new[] { "문바위", "디딤돌" }, log);
            Structure(room.transform, log);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(room.scene);
            Debug.Log("[앉히고 몸 붙이기]\n" + log);
        }

        /// <summary>평지 윗면의 월드 y.</summary>
        private static float GroundTop()
        {
            var field = GameObject.Find(FieldName);
            var t = field != null ? field.transform.Find("평지") : null;
            var r = t != null ? t.GetComponent<Renderer>() : null;
            return r != null ? r.bounds.max.y : float.NaN;
        }

        // ── ① 땅에 앉히기 ──────────────────────────────

        private static void Sit(string rootName, string[] groups, float ground, StringBuilder log)
        {
            var root = GameObject.Find(rootName);
            if (root == null) return;

            foreach (var name in groups)
            {
                var g = root.transform.Find(name);
                if (g == null) continue;

                int moved = 0;
                float worst = 0f;
                foreach (Transform t in g)
                {
                    var b = Bounds(t);
                    if (b.size == Vector3.zero) continue;

                    float gap = b.min.y - ground;
                    if (Mathf.Abs(gap + Bury) < 0.005f) continue;   // 이미 앉아 있다

                    Undo.RecordObject(t, "땅에 앉히기");
                    t.position += Vector3.up * (-gap - Bury);
                    moved++;
                    if (Mathf.Abs(gap) > Mathf.Abs(worst)) worst = gap;
                }
                log.AppendLine("   " + rootName + "/" + name + " — " + moved + "개를 앉혔습니다"
                               + (moved > 0 ? "  (가장 어긋난 것 " + worst.ToString("F2") + "m)" : ""));
            }
        }

        private static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // ── ② 몸 붙이기 ────────────────────────────────

        /// <summary>
        /// 나무 줄기에 가는 기둥을 세운다.
        ///
        /// 나무 전체를 감싸면 가지가 드리운 자리까지 벽이 된다 — 나무 밑을 지나갈 수가
        /// 없고, 숲 가장자리가 보이지 않는 담이 되어 버린다. 사람이 부딪히는 것은
        /// <b>줄기</b>뿐이므로 밑동 굵기만큼만 세운다.
        /// </summary>
        private static void Trunks(string rootName, string groupName, StringBuilder log)
        {
            var root = GameObject.Find(rootName);
            var g = root != null ? root.transform.Find(groupName) : null;
            if (g == null) return;

            int added = 0;
            foreach (Transform t in g)
            {
                if (t.GetComponentInChildren<Collider>(true) != null) continue;
                var b = Bounds(t);
                if (b.size == Vector3.zero) continue;

                var c = Undo.AddComponent<CapsuleCollider>(t.gameObject);
                c.direction = 1;                                  // y 축
                float r = Mathf.Max(0.12f, Mathf.Min(b.size.x, b.size.z) * TrunkShare);
                float h = Mathf.Max(r * 2.1f, b.size.y * 0.75f);  // 줄기 높이만큼만

                // 콜라이더는 <b>제 좌표</b>로 잡는다. 물건마다 배율이 달라서 월드 치수를
                // 그대로 넣으면 배율만큼 또 곱해진다.
                Vector3 s = t.lossyScale;
                float k = Mathf.Max(0.0001f, Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
                c.radius = r / k;
                c.height = h / k;
                Vector3 local = t.InverseTransformPoint(new Vector3(b.center.x, b.min.y + h * 0.5f, b.center.z));
                c.center = local;
                added++;
            }
            log.AppendLine("   " + rootName + "/" + groupName + " — 줄기 " + added + "대에 몸을 붙였습니다");
        }

        /// <summary>바위·디딤돌처럼 통째로 막아도 되는 것.</summary>
        private static void Blocks(string rootName, string[] groups, StringBuilder log)
        {
            var root = GameObject.Find(rootName);
            if (root == null) return;

            foreach (var name in groups)
            {
                var g = root.transform.Find(name);
                if (g == null) continue;

                int added = 0;
                foreach (Transform t in g)
                {
                    if (t.GetComponentInChildren<Collider>(true) != null) continue;
                    foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null) continue;
                        var bc = Undo.AddComponent<BoxCollider>(mf.gameObject);
                        bc.center = mf.sharedMesh.bounds.center;
                        bc.size = mf.sharedMesh.bounds.size;
                    }
                    added++;
                }
                log.AppendLine("   " + rootName + "/" + name + " — " + added + "개에 몸을 붙였습니다");
            }
        }

        /// <summary>
        /// 조사청 구조 가운데 <b>통과하면 안 되는 것</b>에 몸을 붙인다.
        ///
        /// 무엇에 붙일지는 이름으로 가른다. 살·서까래·도리·반자는 뺀다 — 손이 닿지
        /// 않는 높이인데다 살만 246 대라, 붙이면 콜라이더가 수백 개 늘고 얻는 것이 없다.
        /// </summary>
        private static void Structure(Transform room, StringBuilder log)
        {
            var 구조 = room.Find("구조");
            if (구조 == null) return;

            string[] groups = { "주춧돌", "벽", "창", "지붕", "문" };
            string[] skip = { "살", "가로살", "세로살", "창호지", "한지" };

            int added = 0;
            foreach (var name in groups)
            {
                var g = 구조.Find(name);
                if (g == null) continue;

                foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    if (mf.GetComponent<Collider>() != null) continue;

                    bool pass = false;
                    foreach (var s in skip) if (mf.name.StartsWith(s)) { pass = true; break; }
                    // 살 무리 밑에 있는 것도 통째로 뺀다
                    if (!pass && mf.transform.parent != null && mf.transform.parent.name == "살") pass = true;
                    if (pass) continue;

                    var bc = Undo.AddComponent<BoxCollider>(mf.gameObject);
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = mf.sharedMesh.bounds.size;
                    added++;
                }
            }
            log.AppendLine("   조사청 구조 — " + added + "곳에 몸을 붙였습니다(살·서까래·도리는 뺐습니다)");
        }
    }
}
