using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 엔딩 감독 (2026-09-13). 「이문록 엔딩 시스템 기획 문서」의 흐름을 한 곳에서 돌린다.
    ///
    /// <code>
    ///   [진·노멀]  서고에서 선아 구출 → 서고를 나온다 → 관아 마당에서 견우가 맞이한다
    ///              → 재회 대화가 끝나는 순간 (판정 시점)
    ///              → 자동으로 아침 (암전 → 낮 → 밝힘, 조작 없음)
    ///              → 천천히 암전 (음악도 함께) → 나레이션 → 엔딩 문구 (검은 화면 그대로)
    ///              → 페이드아웃 → 조사청(HubScene) 복귀
    ///   [배드]     경계도 100 → 지금 대화가 닫히면 수령(없으면 관노)이 조사를 막는다
    ///              → 암전 → 시스템 문구 → 나레이션 → 「조사 중단」 → 복귀
    /// </code>
    ///
    /// ■ 왜 씬에 두지 않고 스스로 붙는가
    ///   엔딩은 관아에서 시작해 조사청까지 씬을 건넌다. 씬에 속한 오브젝트 위에서 코루틴을
    ///   돌리면 LoadScene 순간 끊긴다 (<see cref="SceneTransition"/> 이 러너를 따로 두는 것과 같은 까닭).
    ///   <see cref="MusicDirector"/> 처럼 사건 씬이 열리면 <c>DontDestroyOnLoad</c> 로 하나 생기고,
    ///   조사청에 도착하면 스스로 사라진다.
    ///
    /// ■ 엔딩 씬(조사청 아침의 책상)은 없앴다 (2026-09-13 당일 결정)
    ///   한때 <c>Gyeonu_Ending</c> 씬에 보료·서안·보고서 3종을 놓고 엔딩 문구를 그 위에 띄웠다. 문구는 이제
    ///   나레이션 뒤 검은 화면에 그대로 뜨고, 조사청 사건판의 문서 색·기록대 판결패는 공통
    ///   <c>GameState.SetVerdict</c> 가 알아서 바꾼다. 그때 배운 것만 적어 둔다 —
    ///   · <c>Assets/_Project/Art/Props/보료서안 1.glb</c> 는 서안(책상)이 아니라 보료 한 벌(요·안석·장침)이다.
    ///     서안이 필요하면 <c>unhyun__tongyeong_table.glb</c>(0.61 × 0.27 × 0.45 m)가 맞다.
    ///   · <c>먹.glb</c>·<c>붓.glb</c> 는 실물의 열 배(2 m)로 들어 있다. glb 뿌리의 임포트 회전을 덮어쓰면 등잔대가 눕는다.
    ///   · 직접 짠 얇은 판 메시는 법선 쪽에서 봐서 시계 방향으로 감아야 앞면이고, 앞면 UV 의 u 를 뒤집어야
    ///     글이 거울상이 아니다 (왼손 좌표계). 서고 문서 프리팹(문서_C1 등)은 이미 그렇게 되어 있다.
    ///   · <c>HanjiTextBaker</c> 는 텍스처를 512 로 잘라 굽는다 — 코앞에서 읽을 장은 임포터를 1024 로 되살려야 한다.
    ///
    /// ■ 판정 시점 — "재회 대화가 끝나는 순간"
    ///   구출 뒤 관아 마당의 견우(<c>Gyeonu_관아_재회</c>)와의 대화가 닫히면(<see cref="NpcDialogue.SessionEnded"/>)
    ///   <see cref="GyeonuWorld.F_재회완료"/> 를 세우고 아침 연출로 넘어간다. 그때까지 관아에서 마을로 나가는
    ///   출구는 잠근다 — 재회를 건너뛰고 마을로 나가 버리면 엔딩이 영영 오지 않는다.
    ///   인사만 하고 닫은 대화(플레이어 말이 한 줄도 없음)는 재회로 치지 않는다.
    ///
    /// ■ 조사청 복귀는 여기서만 연다
    ///   사건 도중의 조사청 출구는 놓지 않는다(SceneLinkBuilder.HubReturnEnabled = false). 엔딩의 마지막에
    ///   이 감독이 <see cref="SceneTransition.Go"/> 로 조사청을 직접 부른다.
    /// </summary>
    [AddComponentMenu("")]
    public class EndingDirector : MonoBehaviour
    {
        public const string GwanaSceneName = "Gyeonu_Gwana";
        /// <summary>관아에서 마을로 내려가는 출구 (SceneLinkBuilder.BuildGwana 의 이름).</summary>
        public const string GwanaVillageExit = "출구_마을";

        // ── 연출 시간 (초) ──────────────────────────────────────
        const float MorningFadeOut = 2.6f, MorningHold = 1.4f, MorningFadeIn = 2.8f, MorningLook = 5.0f;
        const float EndFadeOut = 3.2f;
        const float ParaFadeIn = 0.9f, ParaFadeOut = 0.7f, ParaMinHold = 1.5f;
        const float TitleHold = 7.0f, AfterTitleHold = 1.2f;
        const float BadLineSeconds = 4.2f;

        public static EndingDirector Instance { get; private set; }

        /// <summary>엔딩 연출이 도는 중인가 — 다른 연출·출구가 끼어들지 않게 본다.</summary>
        public static bool Running => Instance != null && Instance._running;

        bool _running;
        bool _gateLocked;        // 관아 마을 출구를 내가 잠갔는가
        bool _calledOut;         // 견우가 한 번 불렀는가
        EndingId _ending = EndingId.None;
        NarrationPanel _panel;
        ScreenFader _fader;

        public const string GateMessage = "견우가 마당 아래에서 이쪽을 보고 있다. 먼저 그와 이야기를 해야겠다.";
        public const string CallOutLine = "견우: 「나리… 선아는, 선아는 무사합니까.」";

        // ── 스스로 붙기 ────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
            Ensure(SceneManager.GetActiveScene().name);
        }

        static void OnAnySceneLoaded(Scene s, LoadSceneMode mode) => Ensure(s.name);

        static void Ensure(string sceneName)
        {
            if (Instance != null) return;
            if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith("Gyeonu")) return;
            var go = new GameObject("[엔딩]");
            DontDestroyOnLoad(go);
            go.AddComponent<EndingDirector>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            GyeonuCase.ThresholdFired += OnThreshold;
            NpcDialogue.SessionEnded += OnSessionEnded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneTransition.PlayerPlaced += OnPlayerPlaced;
        }

        void OnDisable()
        {
            GyeonuCase.ThresholdFired -= OnThreshold;
            NpcDialogue.SessionEnded -= OnSessionEnded;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneTransition.PlayerPlaced -= OnPlayerPlaced;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            ApplyGwanaGate();
            // 경계도 100 인 채로 씬이 열렸다 (세이브 복원·디버그) — 바로 막는다.
            if (GyeonuCase.EndingForced && !_running)
                StartCoroutine(BadEnding());
        }

        void Update()
        {
            if (_running || _calledOut || !_gateLocked) return;
            // 견우가 한 번 부른다 — 마루에서 내려오는 길에 마당의 견우가 눈에 들도록.
            var gyeonu = FindNpc(NpcId.Gyeonu);
            var walk = FindFirstObjectByType<DebugWalkController>();
            if (gyeonu == null || walk == null) return;
            if ((gyeonu.transform.position - walk.transform.position).sqrMagnitude > 14f * 14f) return;
            _calledOut = true;
            DebugToast.Show(CallOutLine, 5f);
        }

        // ── 신호 ──────────────────────────────────────────────
        void OnThreshold(Threshold t)
        {
            if (t != Threshold.Alert100 || _running) return;
            StartCoroutine(BadEnding());
        }

        void OnSessionEnded(NpcDialogue npc, DialogueSession session)
        {
            if (_running || GyeonuCase.EndingForced) return;
            if (npc == null || npc.profile == null || npc.profile.npcId != NpcId.Gyeonu) return;
            if (!GyeonuCase.SeonaRescued) return;
            if (SceneManager.GetActiveScene().name != GwanaSceneName) return;
            if (!HadPlayerLine(session)) return;   // 인사만 보고 닫았다 — 아직 재회가 아니다

            GyeonuWorld.Set(GyeonuWorld.F_재회완료);
            ApplyGwanaGate();
            StartCoroutine(MorningEnding());
        }

        static bool HadPlayerLine(DialogueSession s)
        {
            if (s == null || s.Lines == null) return false;
            foreach (var l in s.Lines) if (l != null && l.fromPlayer) return true;
            return false;
        }

        void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (s.name == SceneTransition.HubSceneName)
            {
                // 조사청에 닿았다 — 사건은 끝났다. 음악 감쇠를 되돌리고 물러난다.
                if (MusicDirector.Instance != null) MusicDirector.Instance.SetDuck(1f, 0.1f);
                Destroy(gameObject);
                return;
            }
            _gateLocked = false;
            _calledOut = false;
            ApplyGwanaGate();
        }

        void OnPlayerPlaced() => ApplyGwanaGate();

        /// <summary>구출 뒤 · 재회 전에는 관아에서 마을로 나가는 출구를 잠근다.</summary>
        void ApplyGwanaGate()
        {
            if (SceneManager.GetActiveScene().name != GwanaSceneName) return;
            bool lockIt = GyeonuCase.SeonaRescued && !GyeonuWorld.Has(GyeonuWorld.F_재회완료) && !GyeonuCase.EndingForced;
            if (lockIt == _gateLocked) return;

            var go = GameObject.Find(GwanaVillageExit);
            var exit = go != null ? go.GetComponent<SceneExit>() : null;
            if (exit == null)
            {
                if (lockIt) Debug.LogWarning("[엔딩] 관아에 '" + GwanaVillageExit + "' 출구가 없다 — 재회 전 잠금을 걸지 못했다.");
                return;
            }
            exit.locked = lockIt;
            if (lockIt) exit.lockedMessage = GateMessage;
            _gateLocked = lockIt;
        }

        // ── 진·노멀: 재회가 끝난 아침 ───────────────────────────
        IEnumerator MorningEnding()
        {
            _running = true;
            yield return WaitUntilFree();
            LockPlayer();
            DebugToast.HidePinned();

            // 엔딩 막 — 지금부터 낮 인물(수령 등)은 자리에 나오지 않는다 (NpcSchedule.Wanted).
            GyeonuCase.CurrentAct = Act.Ending;

            Duck(0.3f, MorningFadeOut);
            yield return Fade(1f, MorningFadeOut);

            GyeonuCase.Time = TimeOfDay.Day;          // 하늘·조명은 WorldTimeSync 가 따라온다
            NpcSchedule.RefreshAll();                 // 캄캄한 동안 자리를 한꺼번에 바꾼다 (InnRest 와 같은 규약)
            yield return new WaitForSeconds(MorningHold);

            DebugToast.Show(EndingScript.MorningLine, MorningFadeIn + MorningLook * 0.6f);
            yield return Fade(0f, MorningFadeIn);
            yield return new WaitForSeconds(MorningLook);

            yield return FinishSequence(GyeonuCase.Ending, systemLine: false);
        }

        // ── 배드: 경계도 100 ──────────────────────────────────
        IEnumerator BadEnding()
        {
            _running = true;
            yield return WaitUntilFree();
            LockPlayer();
            DebugToast.HidePinned();
            GyeonuCase.CurrentAct = Act.Ending;

            var lines = MagistratePresent() ? EndingScript.BadMagistrateLines : EndingScript.BadRelayLines;
            foreach (var line in lines)
            {
                DebugToast.Show(line, BadLineSeconds + 0.3f);
                yield return new WaitForSeconds(BadLineSeconds);
            }
            yield return new WaitForSeconds(0.6f);

            yield return FinishSequence(EndingId.Bad, systemLine: true);
        }

        // ── 공통: 암전 → 나레이션 → 엔딩 문구 → 조사청 ───────────
        IEnumerator FinishSequence(EndingId e, bool systemLine)
        {
            _ending = e;
            var recorded = GyeonuCase.FinishCase();
            if (recorded == EndingId.None)
            {
                // 디버그로 억지로 부른 경우 — 기록은 못 하지만 연출은 끝까지 보여 준다
                Debug.LogWarning("[엔딩] 판정 불가 상태에서 연출만 돈다: " + GyeonuCase.Label(e));
            }
            Debug.Log("[엔딩] 시작 — " + GyeonuCase.Label(e));

            Duck(0f, EndFadeOut);
            yield return Fade(1f, EndFadeOut);
            DebugToast.HidePinned();
            yield return new WaitForSeconds(0.8f);

            if (systemLine) yield return Paragraph(EndingScript.BadSystemLine, NarrationPanel.BodySize, 5.5f);

            foreach (var para in EndingScript.Narration(e))
                yield return Paragraph(para, NarrationPanel.BodySize, HoldFor(para));

            yield return new WaitForSeconds(1.2f);

            // 엔딩 문구 — 검은 화면 그대로 (문서 9.1 · 15.1 · 22 · 31)
            string title = EndingScript.CaseName + "\n" + EndingScript.Outcome(e);
            yield return Paragraph(title, NarrationPanel.TitleSize, TitleHold, subline: EndingScript.Title(e));

            yield return new WaitForSeconds(AfterTitleHold);
            yield return ReturnToHub();
        }

        IEnumerator ReturnToHub()
        {
            Duck(1f, 0.1f);
            if (!Application.CanStreamedLevelBeLoaded(SceneTransition.HubSceneName))
            {
                Debug.LogError("[엔딩] 조사청(HubScene)을 로드할 수 없다 — 화면을 되밝히고 멈춘다.");
                yield return Fade(0f, 1f);
                _running = false;
                yield break;
            }
            SceneTransition.Go(SceneTransition.HubSceneName, "", 0f, 1.2f);
        }

        // ── 나레이션 문단 ─────────────────────────────────────
        IEnumerator Paragraph(string text, int size, float hold, string subline = null)
        {
            var p = Panel();
            string body = string.IsNullOrEmpty(subline) ? text : text + "\n\n" + subline;
            p.Set(body, size);
            p.SetAlpha(0f);
            yield return PanelFade(p, 1f, ParaFadeIn);

            float t = 0f;
            while (t < hold)
            {
                t += Time.unscaledDeltaTime;
                if (t > ParaMinHold && SkipPressed()) break;
                yield return null;
            }
            yield return PanelFade(p, 0f, ParaFadeOut);
            p.Clear();
        }

        static float HoldFor(string para)
        {
            int n = para == null ? 0 : para.Length;
            return Mathf.Clamp(3.2f + n * 0.075f, 4f, 10f);
        }

        static bool SkipPressed()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        NarrationPanel Panel()
        {
            if (_panel != null) return _panel;
            _panel = NarrationPanel.Create(transform);
            return _panel;
        }

        static IEnumerator PanelFade(NarrationPanel p, float to, float seconds)
        {
            float from = to > 0.5f ? 0f : 1f, t = 0f;
            while (t < seconds)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                p.SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            p.SetAlpha(to);
        }

        // ── 도우미 ───────────────────────────────────────────
        /// <summary>대화·소지품 창·포커스 복귀·씬 전환이 모두 끝날 때까지.</summary>
        IEnumerator WaitUntilFree()
        {
            while (true)
            {
                bool busy = SceneTransition.IsTransitioning;
                if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen) busy = true;
                if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) busy = true;
                var rig = FindFirstObjectByType<DebugFocusRig>();
                if (rig != null && rig.IsFocusing) busy = true;
                if (!busy) yield break;
                yield return null;
            }
        }

        static void LockPlayer()
        {
            var walk = FindFirstObjectByType<DebugWalkController>();
            if (walk != null) walk.enabled = false;
            foreach (var it in FindObjectsByType<DebugInteractor>(FindObjectsSortMode.None)) it.enabled = false;
        }

        static void Duck(float factor, float seconds)
        {
            if (MusicDirector.Instance != null) MusicDirector.Instance.SetDuck(factor, seconds);
        }

        ScreenFader Fader()
        {
            if (_fader != null) return _fader;
            _fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
            if (_fader != null) return _fader;
            var go = new GameObject("[엔딩_암전]");
            DontDestroyOnLoad(go);
            _fader = go.AddComponent<ScreenFader>();
            return _fader;
        }

        IEnumerator Fade(float target, float seconds)
        {
            var f = Fader();
            float from = f.Alpha, t = 0f;
            while (t < seconds)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float k = Mathf.Clamp01(t / seconds);
                f.SetAlpha(Mathf.Lerp(from, target, k * k * (3f - 2f * k)));
                yield return null;
            }
            f.SetAlpha(target);
        }

        static NpcDialogue FindNpc(NpcId id)
        {
            foreach (var d in FindObjectsByType<NpcDialogue>(FindObjectsSortMode.None))
            {
                if (d.profile == null || d.profile.npcId != id) continue;
                var s = d.GetComponent<NpcSchedule>();
                if (s != null && !s.Visible) continue;
                return d;
            }
            return null;
        }

        static bool MagistratePresent() => FindNpc(NpcId.Magistrate) != null;

        // ── 디버그 ───────────────────────────────────────────
        /// <summary>지금 판정대로 엔딩 연출을 시작한다 (F9 창). 구출 전이면 판정 불가라 배드 연출로 보여 준다.</summary>
        public static void DebugStart()
        {
            var d = Instance;
            if (d == null || d._running) return;
            if (GyeonuCase.EndingForced || !GyeonuCase.SeonaRescued) { d.StartCoroutine(d.BadEnding()); return; }
            d.StartCoroutine(d.MorningEnding());
        }
    }
}
