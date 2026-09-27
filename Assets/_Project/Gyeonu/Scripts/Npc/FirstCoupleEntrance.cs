using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 최초의 견우·직녀 등장 연출 (2026-09-10). 문서 「30」 — "첫 방문은 밤. 집 근처에 들어오면
    /// <b>플레이어 뒤에서 등장</b>한다."
    ///
    /// <code>
    /// ① 견우마을에 들어와도 두 사람이 안 보인다      (NpcSchedule 이 F_최초두사람_등장 을 요구한다)
    /// ② 플레이어가 집 앞 triggerRadius 안으로 들어오면
    /// ③ 플레이어가 방금 지나온 길목(behindDistance 뒤)에 두 사람을 세우고 플래그를 세운다 → 보인다
    /// ④ 정체를 묻는 말(자막) — E02 는 MusicDirector 가 '보이고 20m 안'이면 스스로 튼다
    /// ⑤ 플레이어가 돌아본다 — 두 사람은 <b>그냥 서 있다</b> (2026-09-11)
    /// ⑥ 화면이 어두워졌다 밝아지면 두 사람은 집 앞 제자리에 서 있다
    ///    F_최초두사람_도착 — 다음 방문부터는 처음부터 서 있다
    /// </code>
    ///
    /// ■ 뒤에서 나타나는 자리를 왜 고정 좌표로 두지 않는가
    ///   연못을 왼쪽으로 돌아올 수도, 오른쪽으로 돌아올 수도 있다. 그래서 플레이어의 발자취를
    ///   0.5m 마다 남겨 두고, 거기서 <see cref="behindDistance"/> 만큼 되짚은 점에 세운다 —
    ///   반드시 <b>플레이어가 방금 밟은 땅</b>이고, 카메라 시야 밖이다. 자취가 모자라면
    ///   (씬 입구에서 곧장 트리거된 경우) 카메라 뒤 방향으로 같은 거리에 세운다.
    ///
    /// ■ 왜 걸어오지 않는가 (2026-09-11)
    ///   예전에는 같은 자취를 거꾸로 밟아 플레이어 곁을 지나 집 앞까지 <b>걸어왔다</b>. 길찾기가
    ///   없어 막히지는 않았지만, 두 사람이 나란히 총총 걸어오는 모습이 이야기의 무게와 맞지 않았다
    ///   — 이들은 사람이 아니다. 이제 <b>서 있을 뿐</b>이고, 자리를 옮기는 일은 암전이 대신한다.
    ///   암전은 씬 전환이 쓰는 것 그대로다(<see cref="SceneTransition.Blink"/>).
    ///
    /// ■ 언제 어두워지는가
    ///   <b>플레이어가 돌아본 것</b>을 먼저 본다 — 두 사람이 시야 <see cref="viewAngle"/> 안에 들어와
    ///   <see cref="seenSeconds"/> 만큼 머물면, 보았다고 치고 한 박자 뒤 어두워진다. 끝내 돌아보지
    ///   않아도 말이 끝나고 <see cref="turnTimeout"/> 초가 지나면 어두워진다 — 연출이 영영 멈춰
    ///   두 사람이 등 뒤에 박제되는 일이 없게.
    ///
    /// ■ 정체를 묻는 말
    ///   대화창은 한 사람과 마주 서서 묻고 답하는 틀이라, 등 뒤에서 던지는 한마디에는 맞지 않는다.
    ///   자막(<see cref="DebugToast"/>)으로 띄운다. 대화창을 원하면 <see cref="line"/> 을 비우고
    ///   도착 뒤 말을 걸게 두면 된다.
    /// </summary>
    public class FirstCoupleEntrance : MonoBehaviour
    {
        [Header("두 사람")]
        public NpcActor gyeonu;
        public NpcActor jiknyeo;

        [Header("발동")]
        [Tooltip("두 사람이 서 있을 자리(둘의 가운데)에서 이 거리(m) 안으로 들어오면 시작한다")]
        public float triggerRadius = 8f;

        [Tooltip("플레이어가 지나온 자취를 이만큼(m) 되짚은 자리에 세운다")]
        public float behindDistance = 9f;

        [Tooltip("이 각도(도) 안에 있으면 '보인다'로 쳐서 더 뒤로 물린다")]
        public float viewAngle = 75f;

        [Header("연출")]
        [Tooltip("나타나고 이만큼(초) 뒤에 말을 건다")]
        public float lineDelay = 0.8f;

        [Tooltip("정체를 묻는 말. 자막으로 띄운다")]
        public string line = "게 뉘시오… 이 집엔 어찌 찾아오셨소.";

        public float lineSeconds = 5f;

        [Header("돌아봄 → 암전")]
        [Tooltip("두 사람이 시야에 이만큼(초) 머물면 '보았다'로 친다")]
        public float seenSeconds = 0.7f;

        [Tooltip("보고 나서 어두워지기까지 두는 여운(초)")]
        public float afterSeen = 1.2f;

        [Tooltip("끝내 돌아보지 않아도 말이 끝나고 이만큼(초) 지나면 어두워진다")]
        public float turnTimeout = 9f;

        [Tooltip("어두워지는 시간 / 캄캄한 시간 / 밝아지는 시간(초)")]
        public float fadeOut = 0.9f, holdBlack = 0.5f, fadeIn = 1.1f;

        [Tooltip("둘이 나란히 선 간격(m)")]
        public float sideBySide = 0.9f;

        // ── 상태 ──────────────────────────────────────────
        readonly List<Vector3> trail = new List<Vector3>();
        Vector3 homeG, homeJ;
        Quaternion rotG, rotJ;
        bool running, done;

        void Awake()
        {
            if (gyeonu != null) { homeG = gyeonu.transform.position; rotG = gyeonu.transform.rotation; }
            if (jiknyeo != null) { homeJ = jiknyeo.transform.position; rotJ = jiknyeo.transform.rotation; }
        }

        void Start()
        {
            if (GyeonuWorld.Has(GyeonuWorld.F_최초두사람_도착)) { done = true; return; }
            // 걸어오는 도중에 씬을 나갔다 — 다음 방문에는 이미 제자리에 서 있으니 도착으로 친다.
            if (GyeonuWorld.Has(GyeonuWorld.F_최초두사람_등장)) { GyeonuWorld.Set(GyeonuWorld.F_최초두사람_도착); done = true; }
        }

        Transform Player()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        void Update()
        {
            if (done || running || gyeonu == null || jiknyeo == null) return;
            var cam = Player();
            if (cam == null) return;

            // 발자취 — 0.5m 마다 한 점
            Vector3 foot = cam.position; foot.y -= 1.6f;
            if (trail.Count == 0 || Vector3.Distance(trail[trail.Count - 1], foot) >= 0.5f) trail.Add(foot);

            if (!GyeonuWorld.Has(GyeonuWorld.F_타공지도_길밝힘)) return;
            Vector3 mid = (homeG + homeJ) * 0.5f;
            Vector3 d = cam.position - mid; d.y = 0f;
            if (d.magnitude > triggerRadius) return;

            running = true;
            StartCoroutine(Run(cam));
        }

        /// <summary>자취를 <paramref name="back"/> 만큼 되짚은 점. 자취 색인도 돌려준다.</summary>
        Vector3 BehindPoint(Transform cam, float back, out int index)
        {
            float acc = 0f;
            for (int i = trail.Count - 1; i > 0; i--)
            {
                acc += Vector3.Distance(trail[i], trail[i - 1]);
                if (acc >= back) { index = i - 1; return trail[i - 1]; }
            }
            // 자취가 모자라면 카메라 뒤로
            index = -1;
            Vector3 f = cam.forward; f.y = 0f; f.Normalize();
            Vector3 p = cam.position - f * back; p.y -= 1.6f;
            return p;
        }

        bool InView(Transform cam, Vector3 p)
        {
            Vector3 to = p + Vector3.up * 1.4f - cam.position;
            return Vector3.Angle(cam.forward, to) <= viewAngle;
        }

        IEnumerator Run(Transform cam)
        {
            // ③ 자리 — 시야에 들면 더 뒤로 물린다 (최대 3번)
            int idx; float back = behindDistance;
            Vector3 spawn = BehindPoint(cam, back, out idx);
            for (int k = 0; k < 3 && InView(cam, spawn); k++) { back += 4f; spawn = BehindPoint(cam, back, out idx); }

            Vector3 toPlayer = cam.position - spawn; toPlayer.y = 0f;
            Vector3 fwd = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 pG = NpcPatrol.Ground(spawn + right * (sideBySide * 0.5f), 3f, 8f);
            Vector3 pJ = NpcPatrol.Ground(spawn - right * (sideBySide * 0.5f), 3f, 8f);
            var look = Quaternion.LookRotation(fwd, Vector3.up);
            gyeonu.transform.SetPositionAndRotation(pG, look);
            jiknyeo.transform.SetPositionAndRotation(pJ, look);
            Debug.Log("[최초의 두 사람] 뒤에서 등장 — " + spawn.ToString("F1") + " (플레이어 " + cam.position.ToString("F1") + ", 자취 " + trail.Count + "점, " + back + "m 뒤)");

            GyeonuWorld.Set(GyeonuWorld.F_최초두사람_등장);          // → NpcSchedule 이 보이게 한다
            var sG = gyeonu.GetComponent<NpcSchedule>(); var sJ = jiknyeo.GetComponent<NpcSchedule>();
            float wait = 0f;
            while (wait < 3f && ((sG != null && !sG.Visible) || (sJ != null && !sJ.Visible))) { wait += Time.deltaTime; yield return null; }

            // 연출이 도는 동안은 말을 걸 수 없다
            var tG = gyeonu.GetComponent<NpcDialogue>(); var tJ = jiknyeo.GetComponent<NpcDialogue>();
            if (tG != null) tG.enabled = false;
            if (tJ != null) tJ.enabled = false;

            // ④ 말 — 음악(E02)은 MusicDirector.UpdateSting 이 '보이고 가까우면' 스스로 튼다
            yield return new WaitForSeconds(lineDelay);
            if (!string.IsNullOrEmpty(line)) DebugToast.Show(line, lineSeconds);

            // ⑤ 돌아보기를 기다린다. 두 사람은 아무것도 하지 않는다 — 그냥 서 있다.
            yield return WaitUntilSeen(cam);

            // ⑥ 어두워졌다 밝아지면 집 앞 제자리
            bool moved = false;
            SceneTransition.Blink(
                atBlack: () =>
                {
                    gyeonu.transform.SetPositionAndRotation(homeG, rotG);
                    jiknyeo.transform.SetPositionAndRotation(homeJ, rotJ);
                    gyeonu.Release(); jiknyeo.Release();
                    moved = true;
                },
                fadeOut: fadeOut, hold: holdBlack, fadeIn: fadeIn);

            float guard = 0f;
            while (!moved && guard < 5f) { guard += Time.deltaTime; yield return null; }
            if (!moved)
            {
                // 암전이 돌지 못했다(이미 전환 중 등) — 연출만 건너뛰고 자리는 반드시 맞춘다.
                gyeonu.transform.SetPositionAndRotation(homeG, rotG);
                jiknyeo.transform.SetPositionAndRotation(homeJ, rotJ);
                gyeonu.Release(); jiknyeo.Release();
                Debug.LogWarning("[최초의 두 사람] 암전이 돌지 않아 그냥 자리를 옮겼다.");
            }
            yield return new WaitForSeconds(fadeIn * 0.5f);

            if (tG != null) tG.enabled = true;
            if (tJ != null) tJ.enabled = true;
            GyeonuWorld.Set(GyeonuWorld.F_최초두사람_도착);
            done = true;
            Debug.Log("[최초의 두 사람] 집 앞 제자리.");
        }

        /// <summary>
        /// 플레이어가 돌아볼 때까지 기다린다. <see cref="seenSeconds"/> 만큼 시야에 머물면 보았다고
        /// 치고 <see cref="afterSeen"/> 만큼 여운을 둔다. 끝내 안 돌아보면 <see cref="turnTimeout"/> 에
        /// 스스로 넘어간다.
        /// </summary>
        IEnumerator WaitUntilSeen(Transform cam)
        {
            float waited = 0f, seen = 0f;
            while (waited < turnTimeout)
            {
                waited += Time.deltaTime;
                if (cam == null) break;
                Vector3 mid = (gyeonu.transform.position + jiknyeo.transform.position) * 0.5f;
                seen = InView(cam, mid) ? seen + Time.deltaTime : 0f;
                if (seen >= seenSeconds)
                {
                    Debug.Log("[최초의 두 사람] 플레이어가 돌아보았다 (" + waited.ToString("F1") + "초).");
                    yield return new WaitForSeconds(afterSeen);
                    yield break;
                }
                yield return null;
            }
            Debug.Log("[최초의 두 사람] 돌아보지 않아 " + turnTimeout + "초 만에 넘어간다.");
        }
    }
}
