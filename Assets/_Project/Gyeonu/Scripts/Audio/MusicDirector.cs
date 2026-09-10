using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 제3사건 배경 음악 지휘자 (2026-09-09). 씬·시간대·날씨·층·연출을 보고 지금 흘러야 할 곡을
    /// 하나 고르고, 데크 두 개(<see cref="LoopDeck"/>)를 번갈아 써서 곡 사이를 2~3초로 섞는다.
    ///
    /// ■ 씬에 아무것도 두지 않는다
    ///   제3사건 씬(이름이 <c>Gyeonu</c> 로 시작)이 열리는 순간 스스로 생겨나 씬을 넘어 산다
    ///   (DontDestroyOnLoad — 소스가 씬과 함께 죽지 않는다). 곡 목록은 <c>Resources/BgmLibrary</c> 에서 읽는다.
    ///
    /// ■ 씬 전환 (2026-09-10 개정)
    ///   <see cref="SceneTransition.Departing"/> 을 받아 <b>출발하는 순간</b> 앞 곡을 <see cref="departHold"/> 까지만
    ///   내려 암전 동안 낮게 이어 틀고, 도착해서 새 곡과 <c>sceneCrossfade</c> 초 등파워로 섞는다.
    ///   출발 때 다 꺼 버리면 로드가 긴 경우(실측 4초) 암전 속에 빈 침묵이 생기고, 로드가 끝난 뒤에야
    ///   끄기 시작하면 앞 곡이 로드 내내 제 크기로 흐르다 뚝 끊긴다 — 그 사이를 택했다.
    ///   페이드는 <see cref="MaxStep"/> 으로 잘라 센다. 로드 직후의 긴 프레임(1~3초) 하나가
    ///   페이드를 단번에 끝내 '뚝' 끊기던 것이 그 때문이었다 (SceneTransition 도 같은 이유로 자른다).
    ///
    /// ■ 곡을 고르는 규칙 (대응표 그대로)
    ///   마을 낮/밤 · 은하담 낮/밤맑음/밤비 · 관아 낮/밤 · 집무실 · 관측실 위층/아래층 · 견우마을 · 선아의 방.
    ///   시간대·날씨는 <see cref="GyeonuWorld"/> 를 그대로 본다 (WorldTimeSync 가 보는 값과 같다).
    ///   견우마을은 WorldTimeSync 가 날씨를 맑음으로 고정하지만 곡은 하나뿐이라 상관없다.
    ///
    /// ■ 관측실 위층/아래층 — 리스너 높이 + 히스테리시스
    ///   실측: 관측실 바닥 -3.06~0, 서고 바닥 -6.6, 줄사다리는 -6.6→-3.06. 눈높이(리스너)로
    ///   내려갈 때는 <see cref="goDownBelowY"/> 아래로, 올라올 때는 <see cref="goUpAboveY"/> 위로 넘어야
    ///   바뀌고, 그 상태가 <see cref="floorDwell"/> 초 이어져야 곡을 바꾼다. 사다리 근처에서
    ///   오르내려도 두 문턱 사이에서는 곡이 튀지 않는다.
    ///
    /// ■ 밤하늘 재현(E01) — 점등부터 지도까지 (2026-09-09 개정)
    ///   점등 시퀀스가 시작되면 E01. 천장에 별이 뜨고, 안내(HonsangController.afterIgniteHint)가 뜨고,
    ///   플레이어가 비밀지도를 펼쳐 별빛 길 연출(<see cref="SecretMapReveal"/>)이 <b>끝나는 순간</b>
    ///   — 곧 <see cref="GyeonuWorld.F_타공지도_길밝힘"/> 이 서는 순간 — 관측실 곡으로 돌아온다.
    ///   규칙 한 줄: <c>E01 ⇔ 혼상이 켜져 있고 아직 길을 밝히지 않았다</c>. 그래서
    ///     · 지도를 안 쓰고 아래층에 가도 E01 이 이어진다 (별밤이 그대로니까)
    ///     · 씬을 나가면 그 씬 곡, 돌아오면 별밤이 복원돼 있으니 다시 E01
    ///     · 소등하면 관측실 곡
    ///   E01 은 132초짜리라 루프 지점(1.5~130초)으로 계속 돈다.
    ///
    /// ■ 최초의 두 사람(E02)
    ///   견우마을에 들어서면 S11. 두 사람이 자리에 서 있고(NpcSchedule) 플레이어가
    ///   <see cref="stingDistance"/> 안으로 처음 다가선 순간 E02. 55초짜리라 대화를 걸기 전에 끝나 버릴 수
    ///   있어 루프 지점(0~53초)으로 돌린다 (2026-09-10). 둘 중 하나와 대화가 열리면
    ///   <see cref="stingOutCrossfade"/> 초 등파워 크로스페이드로 S11 로 돌아오고, 그 뒤로는 플래그가 서 있어
    ///   다시 틀지 않는다.
    ///
    /// ■ 덕킹
    ///   아이들 노래(<see cref="ChildrenSong"/>)가 들리는 동안 <see cref="SetDuck"/> 으로 낮춘다.
    /// </summary>
    [AddComponentMenu("")]
    public class MusicDirector : MonoBehaviour
    {
        public static MusicDirector Instance { get; private set; }

        [Header("관측실 — 리스너(눈) 높이로 층을 가른다")]
        [Tooltip("위층에 있다가 이 높이 아래로 내려가면 서고 곡")]
        public float goDownBelowY = -3.9f;
        [Tooltip("아래층에 있다가 이 높이 위로 올라오면 관측실 곡")]
        public float goUpAboveY = -2.4f;
        [Tooltip("문턱을 넘은 상태가 이만큼(초) 이어져야 바꾼다")]
        public float floorDwell = 0.6f;

        [Header("견우마을 — 최초의 두 사람")]
        [Tooltip("두 사람에게 이 거리(m) 안으로 처음 다가서면 E02")]
        public float stingDistance = 20f;
        [Tooltip("대화가 열려 E02 → S11 로 돌아올 때 두 곡을 겹치는 길이(초)")]
        public float stingOutCrossfade = 4f;

        [Header("씬 전환")]
        [Tooltip("출발(암전 시작)할 때 앞 곡을 이 페이드 단계까지 내린다 (1 = 제 크기, 0 = 무음). 암전 동안 낮게 이어지다 도착해서 새 곡과 섞인다")]
        [Range(0f, 1f)] public float departHold = 0.3f;
        [Tooltip("출발할 때 내리는 데 걸리는 시간(초) — 암전(0.45초)과 비슷하게")]
        public float departFade = 0.6f;

        /// <summary>한 프레임에 페이드가 나아갈 수 있는 최대 시간(초). 로드 직후의 긴 프레임이 페이드를 삼키지 않게.</summary>
        const float MaxStep = 0.05f;

        /// <summary>검증용 — 켜면 매 프레임 데크 상태를 <see cref="TraceLog"/> 에 남긴다 (귀 대신 눈으로 본다).</summary>
        public static bool Trace;
        public static readonly List<string> TraceLog = new List<string>();

        BgmLibrary lib;
        readonly LoopDeck[] decks = new LoopDeck[2];
        readonly float[] level = new float[2];   // 곡 전환 페이드 0~1
        readonly float[] target = new float[2];
        readonly float[] speed = new float[2];   // 1/초
        int front = -1;
        BgmId current = BgmId.None;

        float duck = 1f, duckTarget = 1f, duckSpeed = 1f;

        string sceneName = "";
        Transform listener;

        // 관측실
        bool lowerFloor;
        bool starNight;                 // E01 이 흘러야 하는 동안 참
        HonsangController honsang;
        float floorCandidateSince = -1f;

        // 견우마을
        readonly List<NpcSchedule> firstTwo = new List<NpcSchedule>();
        readonly List<NpcDialogue> firstTwoTalk = new List<NpcDialogue>();
        bool sting;

        /// <summary>지금 흐르는 곡.</summary>
        public BgmId Current => current;
        public bool LowerFloor => lowerFloor;

        // ── 자체 생성 ─────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            Instance = null;                                   // 도메인 리로드를 꺼 둔 경우 대비
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (Instance != null) { Instance.EnterScene(s); return; }
            if (!IsCaseScene(s.name)) return;

            var lib = BgmLibrary.Load();
            if (lib == null)
            {
                Debug.LogWarning("[음악] Resources/" + BgmLibrary.ResourcePath + " 가 없다 — 배경 음악 없이 간다. " +
                                 "Tools ▸ 이문록 ▸ 음악 ▸ 「BGM 임포트 설정 + 라이브러리 굽기」");
                return;
            }
            var go = new GameObject("[음악]");
            DontDestroyOnLoad(go);
            go.AddComponent<MusicDirector>();          // OnEnable 이 라이브러리·데크를 갖추고 이 씬으로 들어간다
        }

        static bool IsCaseScene(string name) => !string.IsNullOrEmpty(name) && name.StartsWith("Gyeonu");

        /// <summary>
        /// 처음 생길 때와, <b>플레이 중 도메인 리로드</b>(스크립트를 고쳐 저장) 뒤에 온다.
        /// 리로드는 정적 필드·비직렬화 필드를 전부 지우므로(Instance·lib·데크·구독) 여기서 다시 갖춘다.
        /// </summary>
        void OnEnable()
        {
            Instance = this;
            if (lib != null && decks[0] != null) return;
            Rebuild();
        }

        void Rebuild()
        {
            lib = BgmLibrary.Load();
            if (lib == null) { Debug.LogWarning("[음악] Resources/" + BgmLibrary.ResourcePath + " 가 없다."); enabled = false; return; }

            // 예전 데크의 소스는 주인을 잃었다 — 지우고 새로 만든다
            foreach (var s in GetComponents<AudioSource>()) Destroy(s);
            decks[0] = new LoopDeck(gameObject, "A");
            decks[1] = new LoopDeck(gameObject, "B");
            for (int i = 0; i < 2; i++) { level[i] = 0f; target[i] = 0f; speed[i] = 1f; }
            front = -1; current = BgmId.None;
            duck = duckTarget = 1f;

            GyeonuWorld.Changed -= OnWorldChanged;                 GyeonuWorld.Changed += OnWorldChanged;
            HonsangController.StarNightChanged -= OnStarNight;      HonsangController.StarNightChanged += OnStarNight;
            SceneTransition.PlayerPlaced -= OnPlayerPlaced;         SceneTransition.PlayerPlaced += OnPlayerPlaced;
            SceneTransition.Departing -= OnDeparting;               SceneTransition.Departing += OnDeparting;
            SceneManager.sceneLoaded -= OnSceneLoaded;              SceneManager.sceneLoaded += OnSceneLoaded;

            EnterScene(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            GyeonuWorld.Changed -= OnWorldChanged;
            HonsangController.StarNightChanged -= OnStarNight;
            SceneTransition.PlayerPlaced -= OnPlayerPlaced;
            SceneTransition.Departing -= OnDeparting;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 씬 전환이 시작됐다 — 앞 곡을 <see cref="departHold"/> 까지 내려 암전 동안 낮게 이어 튼다.
        /// 도착하면 <see cref="SwitchTo"/> 가 이 데크를 0 으로 마저 내리며 새 곡과 섞는다.
        /// (도착한 씬이 같은 곡을 원하면 <see cref="EnterSceneNextFrame"/> 이 제 크기로 되돌린다.)
        /// </summary>
        void OnDeparting(string toScene)
        {
            if (front >= 0)
            {
                target[front] = Mathf.Min(level[front], departHold);
                speed[front] = 1f / Mathf.Max(0.05f, departFade);
            }
            if (Trace) TraceLog.Add("[출발] → " + toScene + " t=" + Time.unscaledTime.ToString("F2"));
        }

        // ── 씬 진입 ───────────────────────────────────────

        void EnterScene(Scene s)
        {
            sceneName = s.name;
            listener = null;
            starNight = false;
            honsang = null;
            sting = false;
            floorCandidateSince = -1f;
            firstTwo.Clear();
            firstTwoTalk.Clear();
            StopAllCoroutines();
            StartCoroutine(EnterSceneNextFrame());
        }

        /// <summary>씬 오브젝트의 Start() 가 한 번 돈 뒤에 살핀다 — 혼상 별밤 복원(HonsangFocusOrb)이 그때 끝난다.</summary>
        IEnumerator EnterSceneNextFrame()
        {
            yield return null;
            FindListener();

            if (sceneName == "Gyeonu_Observatory")
            {
                honsang = FindFirstObjectByType<HonsangController>();
                starNight = StarNightWanted();
                lowerFloor = listener != null && listener.position.y < (goDownBelowY + goUpAboveY) * 0.5f;
            }
            else if (sceneName == "Gyeonu_GyeonuVillage")
            {
                foreach (var d in FindObjectsByType<NpcDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (d.profile == null) continue;
                    if (d.profile.npcId != NpcId.FirstGyeonu && d.profile.npcId != NpcId.FirstJiknyeo) continue;
                    var sc = d.GetComponent<NpcSchedule>();
                    if (sc != null) firstTwo.Add(sc);
                    firstTwoTalk.Add(d);
                }
            }

            Reevaluate(lib.sceneCrossfade);
            // 같은 곡을 이어 쓰는 경우 — 출발 때 내려 둔 것을 제 크기로 되돌린다
            if (front >= 0 && target[front] < 1f) { target[front] = 1f; speed[front] = 1f / Mathf.Max(0.05f, lib.sceneCrossfade); }
        }

        void OnPlayerPlaced()
        {
            // 도착 스폰에 세워진 직후 — 관측실은 층을 다시 읽는다 (체류 시간 없이)
            if (sceneName != "Gyeonu_Observatory") return;
            FindListener();
            if (listener == null) return;
            bool lower = listener.position.y < (goDownBelowY + goUpAboveY) * 0.5f;
            if (lower == lowerFloor) return;
            lowerFloor = lower;
            floorCandidateSince = -1f;
            Reevaluate(lib.sceneCrossfade);
        }

        void FindListener()
        {
            var l = FindFirstObjectByType<AudioListener>();
            if (l != null) listener = l.transform;
            else if (Camera.main != null) listener = Camera.main.transform;
        }

        // ── 곡 고르기 ─────────────────────────────────────

        BgmId Desired()
        {
            if (!IsCaseScene(sceneName)) return BgmId.None;
            bool night = GyeonuWorld.Night, rain = GyeonuWorld.Rain;
            switch (sceneName)
            {
                case "Gyeonu":               return night ? BgmId.S02_마을_밤 : BgmId.S01_마을_낮;
                case "Gyeonu_EunhaDam":      return night ? (rain ? BgmId.S05_은하담_밤비 : BgmId.S04_은하담_밤맑음) : BgmId.S03_은하담_낮;
                case "Gyeonu_Gwana":         return night ? BgmId.S07_관아_밤 : BgmId.S06_관아_낮;
                case "Gyeonu_GwanaOffice":   return BgmId.S08_집무실;
                case "Gyeonu_Observatory":   return starNight ? BgmId.E01_밤하늘재현 : (lowerFloor ? BgmId.S10_서고 : BgmId.S09_관측실);
                case "Gyeonu_GyeonuVillage": return sting ? BgmId.E02_최초의두사람 : BgmId.S11_견우마을;
                case "Gyeonu_SeonaHouse":    return BgmId.S12_선아의방;
                default:                     return BgmId.None;
            }
        }

        void Reevaluate(float seconds)
        {
            var want = Desired();
            if (want == current) return;
            SwitchTo(want, seconds);
        }

        void SwitchTo(BgmId id, float seconds)
        {
            seconds = Mathf.Max(0.05f, seconds);
            var t = lib.Find(id);
            int old = front;
            current = id;

            if (t == null || t.clip == null)
            {
                if (id != BgmId.None) Debug.LogWarning("[음악] 라이브러리에 곡이 없다: " + id);
                front = -1;
                if (old >= 0) { target[old] = 0f; speed[old] = 1f / seconds; }
                return;
            }

            // 데크 고르기 — 전면 데크의 반대쪽. 전면이 없으면(씬 전환 중) 조용한 쪽을 쓴다
            int d;
            if (old >= 0) d = 1 - old;
            else if (!decks[0].IsPlaying) d = 0;
            else if (!decks[1].IsPlaying) d = 1;
            else d = level[0] <= level[1] ? 0 : 1;
            // 아직 잦아드는 중인 데크를 다시 쓰면 그 크기에서 이어 올린다 (뚝 끊기지 않게)
            float from = decks[d].IsPlaying ? level[d] : 0f;
            decks[d].Play(t);
            level[d] = from; target[d] = 1f; speed[d] = 1f / seconds;
            front = d;
            if (old >= 0) { target[old] = 0f; speed[old] = 1f / seconds; }

            Debug.Log("[음악] " + id + " (" + seconds.ToString("F1") + "초 전환)" +
                      (sceneName == "Gyeonu_Observatory" ? (lowerFloor ? " · 아래층" : " · 위층") : ""));
        }

        /// <summary>배경 음악을 잠시 낮춘다 (1 = 제 크기). 아이들 노래가 쓴다.</summary>
        public void SetDuck(float factor, float seconds)
        {
            duckTarget = Mathf.Clamp01(factor);
            duckSpeed = 1f / Mathf.Max(0.05f, seconds);
        }

        // ── 매 프레임 ─────────────────────────────────────

        void Update()
        {
            // 멈춰 세운 동안에도 여며야 하니 unscaled. 로드·UI 생성 직후의 긴 프레임은 MaxStep 으로 자른다 —
            // 안 자르면 그 한 프레임이 페이드를 통째로 삼켜 '뚝' 끊긴다.
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            duck = Mathf.MoveTowards(duck, duckTarget, duckSpeed * dt);

            for (int i = 0; i < 2; i++)
            {
                level[i] = Mathf.MoveTowards(level[i], target[i], speed[i] * dt);
                var deck = decks[i];
                if (!deck.IsPlaying) continue;
                if (level[i] <= 0f && target[i] <= 0f) { deck.Stop(); continue; }
                var tr = deck.Track;
                deck.Gain = lib.masterVolume * (tr != null ? tr.volume : 1f) * Mathf.Sin(level[i] * Mathf.PI * 0.5f) * duck;
                deck.Update(dt);
            }

            if (Trace)
            {
                var line = "t=" + Time.unscaledTime.ToString("F2") + " raw=" + Time.unscaledDeltaTime.ToString("F3") + " " + sceneName;
                for (int i = 0; i < 2; i++)
                {
                    var tr = decks[i].Track;
                    line += " | " + (decks[i].IsPlaying && tr != null ? tr.id.ToString().Substring(0, 3) : "---") + " lv=" + level[i].ToString("F2") + " g=" + decks[i].Gain.ToString("F3");
                }
                line += " | listener=" + (listener != null);
                TraceLog.Add(line);
            }

            if (front >= 0 && decks[front].Finished) OnFrontFinished();

            if (listener == null) FindListener();
            if (listener == null) return;
            if (sceneName == "Gyeonu_Observatory") UpdateFloor();
            else if (sceneName == "Gyeonu_GyeonuVillage") UpdateSting();
        }

        void OnFrontFinished()
        {
            if (current == BgmId.E02_최초의두사람) sting = false;
            Reevaluate(lib.timeCrossfade);
        }

        // ── 관측실 위층/아래층 ─────────────────────────────

        /// <summary>E01 이 흘러야 하는가 — 혼상이 켜져 있고 아직 지도로 길을 밝히지 않았다.</summary>
        bool StarNightWanted() =>
            honsang != null && honsang.IsLit && !GyeonuWorld.Has(GyeonuWorld.F_타공지도_길밝힘);

        void UpdateFloor()
        {
            bool want = StarNightWanted();
            if (want != starNight)
            {
                starNight = want;
                Reevaluate(want ? 2f : lib.timeCrossfade);
            }
            if (starNight) { floorCandidateSince = -1f; return; }
            float y = listener.position.y;
            bool wantLower = lowerFloor ? y <= goUpAboveY : y < goDownBelowY;
            if (wantLower == lowerFloor) { floorCandidateSince = -1f; return; }
            if (floorCandidateSince < 0f) floorCandidateSince = Time.unscaledTime;
            if (Time.unscaledTime - floorCandidateSince < floorDwell) return;
            lowerFloor = wantLower;
            floorCandidateSince = -1f;
            Reevaluate(lib.timeCrossfade);
        }

        void OnStarNight(HonsangController h, bool on)
        {
            if (sceneName != "Gyeonu_Observatory") return;
            honsang = h;
            starNight = StarNightWanted();      // 켜짐: 시퀀스 시작 → E01 (길을 이미 밝혔으면 제외). 꺼짐(소등): 관측실 곡
            Reevaluate(starNight ? 2f : lib.timeCrossfade);
        }

        // ── 견우마을 최초의 두 사람 ─────────────────────────

        void UpdateSting()
        {
            if (sting)
            {
                foreach (var d in firstTwoTalk)
                    if (d != null && d.Session != null) { sting = false; Reevaluate(stingOutCrossfade); return; }
                return;
            }
            if (firstTwo.Count == 0 || GyeonuWorld.Has(GyeonuWorld.F_최초두사람_주제곡)) return;
            foreach (var s in firstTwo)
            {
                if (s == null || !s.Visible) return;
                if (Vector3.Distance(listener.position, s.transform.position) > stingDistance) return;
            }
            sting = true;
            GyeonuWorld.Set(GyeonuWorld.F_최초두사람_주제곡);
            Reevaluate(1.2f);
        }

        void OnWorldChanged() => Reevaluate(lib.timeCrossfade);
    }
}
