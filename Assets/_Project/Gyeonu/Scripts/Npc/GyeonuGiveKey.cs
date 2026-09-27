using System.Collections;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 견우가 무언가를 건네는 연출 (2026-09-11 전면 개정). 문서 「24. GiveKey — 총 2회」.
    ///
    /// <code>
    /// ① 조건이 갖춰지면 그 자리의 견우는 <b>숨는다</b> (집 안에 있는 셈)
    /// ② 플레이어가 마당에 들어오고 <b>문 쪽을 보고 있지 않을 때</b> 문 앞에 세우고 말을 건다 (자막)
    /// ③ 돌아보면 견우가 문 앞에 서 있다
    /// ④ 플레이어 앞 stopDistance 까지 걸어온다 (Walk)
    /// ⑤ 도착하면 GiveKey 를 틀고 <b>내민 자세에서 멈춘다</b>. 손에 진짜 물건이 들려 있다
    /// ⑥ 겨누면 「받기」 — 가슴 앞 자식 조준면(<see cref="GyeonuHandoverOffer"/>)
    /// ⑦ 누르면 화면이 어두워진다 (씬 전환의 암전을 그대로)
    /// ⑧ <b>캄캄한 동안</b> 소지품에 들어가며 획득 창이 암전 위에 뜬다 — 밝아지는 순간 이미 보인다
    /// ⑨ 견우는 제자리로 돌아가 평소처럼 말을 걸 수 있다
    /// </code>
    ///
    /// ■ 무엇이 바뀌었나 (2026-09-10 까지의 방식과)
    ///   예전에는 <b>말을 걸고 있는 동안</b> 슬며시 건넸다. 대화창에 가려 무슨 일이 일어나는지
    ///   보이지 않았고, 받는 행위도 플레이어의 몫이 아니었다. 이제 대화와 무관한 <b>한 장면</b>이다.
    ///
    /// ■ 한 자리에 한 가지만 건넨다
    ///   ⚠️ 열쇠는 <b>밤 · 선아 집 문 앞</b>, 지도는 <b>낮 · 견우 집 문 앞</b>이다. 예전에는 밤의 견우가
    ///      둘 다 건넬 수 있었지만, 자리가 연출의 일부가 된 이상 하나의 자리는 하나만 건넨다.
    ///      (밤에 신뢰도 70을 넘겼다면 지도는 다음 낮에 받는다 — 주막에서 쉬면 날이 밝는다)
    ///
    /// ■ 숨기는 일은 <see cref="NpcSchedule"/> 에 맡긴다
    ///   보이고 안 보이고를 두 곳에서 만지면 반드시 어긋난다. 여기서는 <see cref="NpcSchedule.externalGate"/>
    ///   에 "아직 나설 때가 아니다"를 걸어 두기만 한다. ⚠️ 일정에는 "보고 있는 동안에는 바꾸지 않는다"는
    ///   규칙이 있어, 플레이어가 코앞에 선 채로 조건이 채워지면 그 자리에서 사라지지는 않는다 —
    ///   한 번 멀어졌다 돌아와야 연출이 선다.
    /// </summary>
    [RequireComponent(typeof(NpcDialogue))]
    public class GyeonuGiveKey : MonoBehaviour
    {
        public enum Kind { Key, Map }

        [Header("무엇을")]
        public Kind kind = Kind.Key;

        [Tooltip("소지품에서 찾을 물건 id. 선아 집 열쇠는 SEONA_HOUSE_KEY, 타공 비밀지도는 A1")]
        public string itemId = "SEONA_HOUSE_KEY";

        [Tooltip("밤에만 건넨다 (열쇠). 낮의 지도는 꺼 둔다")]
        public bool needsNight = true;

        // ── 어디서 (2026-09-11 개정) ────────────────────────────────
        //   ⚠️ 처음에는 <b>집 건물 문 앞</b>에 세웠다. 마당 한가운데서 사람이 솟아나는 꼴이었다 —
        //      건물 문은 열리지도 않는데 그 앞에 서 있으니 어디서 왔는지 설명이 안 됐다.
        //      이제 <b>담장 사립문</b>에 세운다. 플레이어가 방금 지나온 그 문이라 저절로 '뒤'가 되고,
        //      "마당 밖에서 들어온 사람"으로 읽힌다.
        [Header("어디서")]
        [Tooltip("연출이 준비되는 자리 — 보통 이 사람의 제자리(마당 안). 비워 두면(0) 제자리")]
        public Vector3 triggerAnchor;

        [Tooltip("플레이어가 이 거리(m) 안에 들어오면 연출이 준비된다 — 마당 기준")]
        public float triggerRadius = 7f;

        [Tooltip("나타나는 자리 — 담장 사립문 <b>안쪽</b> 한 걸음. 비워 두면(0) 제자리")]
        public Vector3 gateSpot;

        [Tooltip("그 사립문(일각문) 오브젝트 이름. 연출이 설 때 닫혀 있으면 소리 없이 열어 둔다 — " +
                 "안 그러면 닫힌 문 안쪽에 사람이 서 있어 역시 설명이 안 된다")]
        public string gateObjectName = "";

        [Tooltip("문에서 이만큼(m)은 떨어져 있어야 연출이 선다. 문간에서 바로 발동하면 " +
                 "견우가 코앞에 나타나고 걸어올 거리도 없다")]
        public float minGateDistance = 3.5f;

        [Tooltip("사립문 쪽이 이 각(도) 안에 있으면 '보고 있다'로 쳐서 세우지 않는다")]
        public float viewAngle = 70f;

        [Tooltip("끝내 문 쪽을 안 보더라도 이만큼(초) 지나면 세운다 — 연출이 영영 멈추지 않게")]
        public float appearTimeout = 25f;

        [Header("말")]
        [TextArea(2, 4)]
        [Tooltip("뒤에서 들리는 목소리. 자막으로 뜬다")]
        public string voiceLine = "…거기 잠깐. 그 문은 그냥 열리지 않소.";

        public float voiceSeconds = 5f;

        [Tooltip("나타나고 이만큼(초) 뒤에 말을 건다")]
        public float voiceDelay = 0.5f;

        [Header("돌아보기")]
        [Tooltip("말을 건 뒤 <b>문 앞에 선 채로</b> 플레이어가 돌아볼 때까지 기다린다. " +
                 "이만큼(초) 지나도 안 보면 그냥 걸어온다 — 연출이 영영 멈추지 않게")]
        public float seenTimeout = 8f;

        [Tooltip("이만큼(초) 시야에 머물면 '보았다'로 친다")]
        public float seenSeconds = 0.4f;

        [Tooltip("보고 나서 걷기 시작할 때까지의 여운(초) — 문 앞에 선 모습을 눈에 담을 시간")]
        public float pauseBeforeWalk = 1.2f;

        [Header("걸음")]
        [Tooltip("플레이어 앞 이만큼(m) 에서 멈춘다")]
        public float stopDistance = 1.7f;

        [Tooltip("도착 판정 여유(m). 목표점에 선 뒤 부동소수 오차로 '아직 멀다'가 되지 않게")]
        public float arriveSlack = 0.02f;
        public float walkSpeed = 1.1f;
        public float turnSpeed = 200f;
        [Tooltip("걷기를 포기하는 시간(초) — 길이 막혀도 연출이 멈추지 않게")]
        public float walkTimeout = 20f;

        [Header("내미는 자세")]
        public string giveState = "GiveKey";

        [Tooltip("모션을 튼 뒤 이만큼(초)에 물건이 손에 나타난다 — 팔이 올라오기 시작하는 지점")]
        public float propInAt = 0.35f;

        [Tooltip("모션을 튼 뒤 이만큼(초)에서 <b>얼어붙는다</b> — 팔을 뻗은 그 자세로 기다린다")]
        public float freezeAt = 1.25f;

        [Header("받을 때")]
        public float fadeOut = 0.5f, holdBlack = 0.35f, fadeIn = 0.7f;

        // ── 손에 드는 자리 (2026-09-11 실측) ────────────────────────
        //   견우의 GiveKey 는 <b>Bone_023</b> 을 앞으로 뻗는다 (0.75~2.0초 구간에서 손이
        //   골반 앞 0.49m, 높이 1.21m 까지 나온다). 손가락 뿌리(Bone_022)는 손뼈 로컬 +Y 로
        //   9cm 지점이라, 손바닥 가운데는 +Y 3~4cm 쯤이다.
        //   ⚠️ 이 리그는 뼈의 lossyScale 이 100 이다 — <b>뼈 로컬 0.0001 이 1cm</b>이고
        //      배율은 0.01 이어야 프리팹의 제 크기가 나온다.
        [Header("손에 드는 자리 (뼈 로컬 · 0.0001 = 1cm)")]
        public string handBone = "Bone_023";
        public Vector3 handLocalPos = new Vector3(0f, 0.00035f, 0f);

        [Tooltip("팔을 뻗었을 때의 '위' 방향(뼈 로컬). 물건의 +Y 를 여기에 맞춘다 — 손바닥에 눕는다")]
        public Vector3 handLocalUp = new Vector3(0.24f, -0.02f, -0.97f);

        [Tooltip("손가락이 가리키는 방향(뼈 로컬). 물건의 +Z(긴 쪽)를 여기에 맞춘다. " +
                 "⚠️ 이것을 안 맞추면 긴 쪽이 아무 데나 향해 소매 속으로 들어가 버린다 (2026-09-11 실측)")]
        public Vector3 handLocalForward = new Vector3(0f, 1f, 0f);

        [Tooltip("손바닥에서 띄우는 거리")]
        public float handLift = 0.00018f;

        [Tooltip("열쇠 배율 — 0.01 이 프리팹의 제 크기(10cm)다. 1.7m 밖 밤 조명에서는 그 크기가 " +
                 "손바닥의 얼룩처럼 보여 0.018(18cm)로 키웠다. 수첩에 '나무로 깎은 것'이라 적힌 열쇠라 " +
                 "그만한 크기가 오히려 자연스럽다 (2026-09-11 실측)")]
        public float keyScale = 0.018f;

        [Tooltip("지도 배율 — 펼친 원본이 62×35cm 라 절반으로 접어 든 크기로 줄인다")]
        public float mapScale = 0.005f;

        // ── 상태 ──────────────────────────────────────────
        enum Phase { 기다림, 연출중, 내밈, 끝 }

        NpcDialogue talk;
        NpcActor actor;
        NpcSchedule sched;
        Animator anim;
        Vector3 homePos;
        Quaternion homeRot;
        Phase phase = Phase.기다림;
        GameObject prop;
        GyeonuHandoverOffer offer;
        Coroutine running;

        /// <summary>지금 「받기」를 누를 수 있는가 — 조준면이 본다.</summary>
        public bool OfferOpen => phase == Phase.내밈;

        /// <summary>이 자리가 아직 건넬 것을 들고 있는가 (숨어 기다리는 중).</summary>
        bool Pending => phase == Phase.기다림 && Ready();

        void Awake()
        {
            talk = GetComponent<NpcDialogue>();
            actor = GetComponent<NpcActor>();
            anim = GetComponent<Animator>();
            sched = GetComponent<NpcSchedule>();
            homePos = transform.position;
            homeRot = transform.rotation;
            if (gateSpot == Vector3.zero) gateSpot = homePos;
            if (triggerAnchor == Vector3.zero) triggerAnchor = homePos;
        }

        void OnEnable()
        {
            // 이미 받은 것이라면 처음부터 끝난 것으로 둔다 (씬을 다시 열었을 때)
            if (Got()) phase = Phase.끝;
            if (sched != null) sched.externalGate = () => !Pending;
        }

        void OnDisable()
        {
            if (sched != null && sched.externalGate != null) sched.externalGate = null;
            ClearProp();
        }

        // ── 조건 ──────────────────────────────────────────

        bool Ready()
        {
            if (Got()) return false;
            if (kind == Kind.Key)
            {
                if (!GyeonuCase.Fired(Threshold.Trust40)) return false;
                if (needsNight && !GyeonuCase.Night) return false;
            }
            else
            {
                if (!GyeonuCase.Fired(Threshold.Trust70)) return false;
                if (needsNight && !GyeonuCase.Night) return false;
            }
            return true;
        }

        bool Got() => kind == Kind.Key ? GyeonuCase.HasSeonaHouseKey : GyeonuCase.HasClue(ClueId.A1);

        Transform Player()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        // ── 매 프레임 ─────────────────────────────────────

        void Update()
        {
            if (phase != Phase.기다림) return;
            if (!Ready()) return;

            var cam = Player();
            if (cam == null) return;

            // 마당 안이어야 하고,
            Vector3 d = cam.position - triggerAnchor; d.y = 0f;
            if (d.magnitude > triggerRadius) return;
            // 사립문에서 충분히 들어와 있어야 한다 (문간에서 발동하면 코앞에 나타난다)
            Vector3 g = cam.position - gateSpot; g.y = 0f;
            if (g.magnitude < minGateDistance) return;

            phase = Phase.연출중;
            running = StartCoroutine(Run(cam));
        }

        IEnumerator Run(Transform cam)
        {
            // ② 사립문 쪽을 보고 있지 않을 때 세운다 — 그래야 '돌아보니 거기 있더라'가 된다
            float waited = 0f;
            while (waited < appearTimeout && InView(cam, gateSpot))
            {
                waited += Time.deltaTime;
                yield return null;
            }

            OpenGate();                                    // 닫힌 문 안쪽에 서 있으면 역시 어색하다

            Vector3 stand = NpcPatrol.Ground(gateSpot, 3f, 8f);
            Vector3 toPlayer = cam.position - stand; toPlayer.y = 0f;
            var look = toPlayer.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toPlayer.normalized, Vector3.up) : homeRot;
            transform.SetPositionAndRotation(stand, look);

            if (sched != null) sched.RefreshNow();          // 이제 보인다 (externalGate 가 풀렸다)
            if (talk != null) talk.enabled = false;         // 연출 중에는 말을 걸 수 없다
            if (actor != null) { actor.enabled = true; actor.PauseRandom(true); actor.ResetToBase(); }
            Debug.Log("[견우] " + kind + " 전달 — 사립문 안쪽에 섰다 @" + stand.ToString("F1")
                      + " (기다림 " + waited.ToString("F1") + "초)");

            // 자막 — 뒤에서 들리는 목소리
            yield return new WaitForSeconds(voiceDelay);
            if (!string.IsNullOrEmpty(voiceLine)) DebugToast.Show(voiceLine, voiceSeconds);

            // ③ 돌아볼 때까지 문 앞에 선 채로 기다린다 — 그 모습을 봐야 ④가 이야기가 된다
            yield return WaitUntilSeen(cam);

            // ④ 플레이어 앞까지 걸어온다
            yield return Walk(cam);

            // ⑤ 내미는 자세에서 멈춘다
            yield return Offer();
        }

        /// <summary>
        /// 사립문이 닫혀 있으면 열어 둔다. <b>즉시</b> 연다 — 플레이어가 그쪽을 보고 있지 않은
        /// 순간에만 부르므로 여닫는 동작을 볼 사람이 없고, 애니메이션을 기다릴 이유도 없다.
        /// 끝나고 다시 닫지는 않는다: 사람이 들어왔으면 문은 열려 있는 것이 자연스럽다.
        /// </summary>
        void OpenGate()
        {
            if (string.IsNullOrEmpty(gateObjectName)) return;
            var go = GameObject.Find(gateObjectName);
            if (go == null)
            {
                // 이름으로 못 찾으면 사립문 자리에서 가장 가까운 여닫이 문을 쓴다
                float best = 4f;
                foreach (var d0 in FindObjectsByType<DoubleHingeDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    float dist = Vector3.Distance(d0.transform.position, gateSpot);
                    if (dist < best) { best = dist; go = d0.gameObject; }
                }
                if (go == null) { Debug.LogWarning("[견우] 사립문을 찾지 못했다: " + gateObjectName); return; }
            }
            var door = go.GetComponent<DoubleHingeDoor>();
            if (door == null || door.IsOpen) return;
            door.SetOpen(true, instant: true);
            Debug.Log("[견우] 사립문을 열어 두었다 — " + go.name);
        }

        bool InView(Transform cam, Vector3 p)
        {
            Vector3 to = p + Vector3.up * 1.4f - cam.position;
            return Vector3.Angle(cam.forward, to) <= viewAngle;
        }

        /// <summary>플레이어가 이쪽을 볼 때까지. 끝내 안 보면 <see cref="seenTimeout"/> 에 스스로 넘어간다.</summary>
        IEnumerator WaitUntilSeen(Transform cam)
        {
            float waited = 0f, seen = 0f;
            while (waited < seenTimeout)
            {
                waited += Time.deltaTime;
                if (cam == null) break;
                seen = InView(cam, transform.position) ? seen + Time.deltaTime : 0f;
                if (seen >= seenSeconds) break;
                yield return null;
            }
            Debug.Log("[견우] " + kind + " — " + (seen >= seenSeconds ? "돌아봤다" : "안 돌아봐 그냥 간다")
                      + " (" + waited.ToString("F1") + "초)");
            yield return new WaitForSeconds(pauseBeforeWalk);
        }

        /// <summary>
        /// 플레이어 앞 <see cref="stopDistance"/> 까지 걸어온다. 목표는 <b>매 프레임</b> 플레이어 자리에서
        /// 다시 잡는다 — 플레이어가 물러나면 따라오고, 마주 걸어오면 그 자리에서 선다.
        ///
        /// ⚠️ 도착 판정에 여유(<see cref="arriveSlack"/>)를 둔다 (2026-09-11 실측).
        ///    목표점이 정확히 stopDistance 지점이라, 다 와서 서면 XZ 거리가 1.7000001 처럼 떨어져
        ///    <c>gap &lt;= stopDistance</c> 가 영영 참이 되지 않았다. 견우는 그 자리에서 Walk 를 튼 채
        ///    <b>제자리걸음으로 20초</b>(walkTimeout)를 보낸 뒤에야 내밀었다. 플레이어가 조금만 움직이면
        ///    목표점이 같이 밀려 <b>계속 밟고 들어오는 것처럼</b> 보였다.
        ///    그래서 ① 남은 거리가 한 걸음 안이면 <b>목표점에 딱 세우고 끝내며</b>, ② 거리 비교에도 2cm 여유를 둔다.
        /// </summary>
        IEnumerator Walk(Transform cam)
        {
            float t = 0f;
            while (t < walkTimeout)
            {
                t += Time.deltaTime;
                Vector3 to = cam.position - transform.position; to.y = 0f;
                float gap = to.magnitude;
                // 이미 충분히 가깝다 — 플레이어가 마주 걸어온 경우도 여기서 선다
                if (gap <= stopDistance + arriveSlack) break;

                actor?.Play("Walk", NpcActor.Pri.Move, hold: true);
                var want = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);

                Vector3 goal = cam.position - to.normalized * stopDistance;
                goal.y = transform.position.y;
                float stepLen = walkSpeed * Time.deltaTime;
                bool arrived = Vector3.Distance(transform.position, goal) <= stepLen;
                Vector3 step = arrived ? goal : Vector3.MoveTowards(transform.position, goal, stepLen);
                transform.position = NpcPatrol.Ground(step, 1.5f, 4f, transform);
                if (arrived) break;            // 목표점에 섰다 — 다음 프레임의 거리 비교에 맡기지 않는다
                yield return null;
            }

            // 멈추고 플레이어를 마주 본다
            actor?.Release();
            Vector3 face = cam.position - transform.position; face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
            {
                var want = Quaternion.LookRotation(face.normalized, Vector3.up);
                while (Quaternion.Angle(transform.rotation, want) > 2f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
                    yield return null;
                }
            }
        }

        IEnumerator Offer()
        {
            actor?.Play(giveState, NpcActor.Pri.Story, hold: true);

            yield return new WaitForSeconds(propInAt);
            ShowProp();

            yield return new WaitForSeconds(Mathf.Max(0.05f, freezeAt - propInAt));
            // ⚠️ 클립은 3.77초짜리고 끝에서 팔을 <b>도로 내린다</b>. hold 로 잡아 둬도 마지막 장에
            //    굳을 뿐이라 내민 자세가 남지 않는다. 그래서 애니메이터를 그 자리에서 멈춰 세운다.
            if (anim != null) anim.speed = 0f;

            EnsureOffer();
            phase = Phase.내밈;
            Debug.Log("[견우] " + kind + " — 내밀고 기다린다. 「받기」");
        }

        /// <summary>「받기」를 눌렀다.</summary>
        public void Accept()
        {
            if (phase != Phase.내밈) return;
            phase = Phase.끝;
            if (offer != null) offer.gameObject.SetActive(false);

            SceneTransition.Blink(
                atBlack: () =>
                {
                    ClearProp();
                    if (anim != null) anim.speed = 1f;
                    actor?.Release();
                    actor?.PauseRandom(false);
                    transform.SetPositionAndRotation(homePos, homeRot);   // ⑨ 제자리로
                    actor?.ResetToBase();
                    if (sched != null) sched.RefreshNow();                // 캄캄한 동안 자리를 다시 판정한다
                    // ⑧ 획득 창은 <b>캄캄한 동안</b> 띄운다 (2026-09-11 개정). 밝아진 뒤에 띄우면
                    //    세상이 먼저 보이고 창이 한 박자 늦게 튀어나온다. 조사 화면(InventoryInspect)은
                    //    암전 쿼드보다 뒤에 그려지도록 큐를 올려 두어, 어두운 채로 이미 떠 있다가
                    //    화면이 밝아지는 순간 그대로 보인다 — 안내 문구(ToastPanel)와 같은 처리다.
                    Grant();
                },
                onDone: () =>
                {
                    if (talk != null) talk.enabled = true;                // 다시 말을 걸 수 있다
                },
                fadeOut: fadeOut, hold: holdBlack, fadeIn: fadeIn);
        }

        /// <summary>연출을 다시 볼 수 있게 처음으로 되돌린다 — <b>디버그 전용</b>.</summary>
        public void DebugRearm()
        {
            if (running != null) { StopCoroutine(running); running = null; }
            ClearProp();
            if (offer != null) offer.gameObject.SetActive(false);
            if (anim != null) anim.speed = 1f;
            if (talk != null) talk.enabled = true;
            actor?.Release();
            actor?.PauseRandom(false);
            transform.SetPositionAndRotation(homePos, homeRot);
            actor?.ResetToBase();
            phase = Phase.기다림;
            if (sched != null) sched.RefreshNow();
        }

        // ── 지급 ──────────────────────────────────────────

        void Grant()
        {
            var item = Inventory.Find(itemId);
            if (kind == Kind.Key)
            {
                if (item != null) Inventory.Add(item);
                else Debug.LogWarning("[견우] 열쇠 물건(" + itemId + ")을 못 찾아 상태 표시만 세웠다.");
                GyeonuCase.HasSeonaHouseKey = true;
                Debug.Log("[견우] 선아 집 열쇠 지급 (신뢰도 " + GyeonuCase.Trust + ")");
            }
            else
            {
                if (item != null) { Inventory.Add(item); Debug.Log("[견우] 타공 비밀지도 지급 (신뢰도 " + GyeonuCase.Trust + ")"); }
                else
                {
                    // 소지품 정의가 아직 없을 때도 진행이 막히지 않게 단서만이라도 세운다.
                    GyeonuWorld.Set(GyeonuWorld.F_비밀지도획득);
                    Debug.LogWarning("[견우] 지도 물건(" + itemId + ")을 못 찾아 단서만 세웠다.");
                }
                // B1 — 견우의 진짜 증언. 지도를 건네는 이 자리가 곧 도피 계획을 말하는 자리다.
                GyeonuCase.AddClue(ClueId.B1);
            }
        }

        // ── 「받기」 조준면 ────────────────────────────────

        void EnsureOffer()
        {
            if (offer == null)
            {
                var go = new GameObject("받기_자리");
                go.transform.SetParent(transform, false);
                // 가슴 앞 — 몸통 캡슐(반지름 0.35)보다 앞으로 나와 조준 광선이 먼저 닿는다
                go.transform.localPosition = new Vector3(0f, 1.15f, 0.15f);
                var col = go.AddComponent<SphereCollider>();
                col.isTrigger = true;         // 걷기를 막지 않는다. 조준은 받는다 (DebugInteractor 규칙)
                col.radius = 0.75f;
                offer = go.AddComponent<GyeonuHandoverOffer>();
                offer.owner = this;
            }
            var it = Inventory.Find(itemId);
            offer.displayName = it != null ? it.displayName : (kind == Kind.Key ? "열쇠" : "지도");
            offer.gameObject.SetActive(true);
        }

        // ── 손에 든 물건 ───────────────────────────────────

        void ShowProp()
        {
            ClearProp();

            var item = Inventory.Find(itemId);
            var model = item != null ? item.modelPrefab : null;
            if (model == null)
            {
                Debug.LogWarning("[견우] 손에 들 모델이 없다 — " + itemId
                                 + " 의 modelPrefab 을 채울 것. 빈손으로 내민다.");
                return;
            }

            Transform hand = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == handBone) { hand = t; break; }
            if (hand == null) { Debug.LogWarning("[견우] " + handBone + " 뼈가 없다 — 빈손으로 내민다."); return; }

            Vector3 up = handLocalUp.sqrMagnitude > 0.0001f ? handLocalUp.normalized : Vector3.up;
            Vector3 fwd = handLocalForward.sqrMagnitude > 0.0001f ? handLocalForward.normalized : Vector3.forward;
            prop = Instantiate(model, hand);
            prop.name = "손_건넬것";
            // 두 축을 다 맞춘다 — 긴 쪽(+Z)은 손가락을 따라, 평평한 면(+Y)은 손바닥 위로.
            prop.transform.localRotation = Quaternion.LookRotation(fwd, up);
            prop.transform.localPosition = handLocalPos + up * handLift;
            // ⚠️ 프리팹이 <b>제 배율</b>을 갖고 있다 (선아집열쇠는 루트가 5배다). 그냥 덮어쓰면 그 배율이
            //    날아가 열쇠가 2cm 짜리로 쪼그라든다 (2026-09-11 실측). 곱해야 한다.
            prop.transform.localScale = model.transform.localScale * (kind == Kind.Key ? keyScale : mapScale);

            // 조준선을 가로채거나 몸통 캡슐과 부딪히면 안 된다.
            foreach (var c in prop.GetComponentsInChildren<Collider>(true)) Destroy(c);
        }

        void ClearProp()
        {
            if (prop == null) return;
            if (Application.isPlaying) Destroy(prop); else DestroyImmediate(prop);
            prop = null;
        }
    }
}
