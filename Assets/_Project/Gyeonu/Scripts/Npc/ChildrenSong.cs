using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 마을 아이들의 노래 (V01, 2026-09-09). 예전 <c>ChildSongTrigger</c>(연결 지점만 있던 빈 껍데기)를
    /// 실제 노래로 채운 것이다. 아이01(<c>VillageChild_01</c>)에 붙고, 소리는 세 아이의 한가운데에
    /// 만든 자식 오브젝트 「아이들노래」에서 <b>3D</b>로 난다 — 가까이 가면 들리고 멀어지면 잦아든다.
    ///
    /// ■ 언제 부르는가 — 한 번 부르고 쉰다
    ///   플레이어가 <see cref="wakeDistance"/> 안에 있고 아이들이 자리에 서 있을 때(NpcSchedule),
    ///   씬에 들어와 <see cref="firstDelay"/> 초 뒤 한 번 부른다. 다 부르면
    ///   <see cref="restMin"/>~<see cref="restMax"/> 초(기본 100~150초) 쉬고, 그 뒤 플레이어가 다시
    ///   근처에 있으면 또 부른다.
    ///
    /// ■ 말을 걸면 그친다 (2026-09-09 개정)
    ///   아이 셋 중 <b>누구에게든</b> 말을 걸면 부르던 노래는 <see cref="stopFade"/> 초에 걸쳐 잦아들며
    ///   멎고, 대화 중에는 새로 시작하지 않는다. 대화가 끝나면 쉬는 간격부터 다시 센다.
    ///
    /// ■ 청하면 다시 부른다
    ///   대화에서 아이가 응답 끝에 <c>[노래]</c> 를 적으면(<see cref="DialogueSession.SongRequested"/>)
    ///   쉬는 시간을 무시하고 바로 부른다. 이것만은 대화 중에도 난다. 다만 <b>대화를 끝내면</b>
    ///   청해서 부르던 노래도 같은 페이드로 멎는다 — 부르다 만 것처럼 뚝 끊기지 않게.
    ///
    /// ■ 배경음 덕킹
    ///   노래가 들리는 정도(거리)에 비례해 <see cref="MusicDirector"/> 를 <see cref="duckTo"/> 까지 낮춘다.
    ///   멀리서 안 들리는 노래 때문에 배경음이 줄어들면 이상하므로 거리에 따라 섞는다.
    ///
    /// ■ 단서 A6 「아이들의 노래」
    ///   노래가 나는 동안 <see cref="hearDistance"/> 안에 <see cref="hearDelay"/> 초 머물면 '들었다'로 친다.
    ///   예전 ChildSongTrigger 의 접근 판정을 그대로 잇되, 이제는 실제 노래가 나올 때만 준다.
    ///
    /// ■ 씬에 두지 않는다
    ///   마을 씬이 열리면 스스로 아이01에 붙는다. 에디터 설치(NpcSetup)도 같은 부품을 붙인다.
    /// </summary>
    public class ChildrenSong : MonoBehaviour
    {
        public static ChildrenSong Instance { get; private set; }

        /// <summary>지금 부르는 중인가 — 대화 프롬프트가 본다. (잦아드는 중은 이미 '그친 것'으로 친다)</summary>
        public static bool Singing => Instance != null && Instance.IsSinging && Instance.fading == null;

        /// <summary>이 세션에서 한 번이라도 불렀는가.</summary>
        public static bool Sung => Instance != null && Instance.sungOnce;

        [Header("소리")]
        public BgmId songId = BgmId.V01_아이들노래;
        [Range(0f, 1f)] public float volume = 0.85f;
        [Tooltip("이 거리(m)까지는 제 크기")]
        public float minDistance = 4f;
        [Tooltip("이 거리(m)에서 들리지 않게 된다 (선형 감쇠)")]
        public float maxDistance = 30f;
        [Range(0f, 360f)]
        [Tooltip("셋이 같이 부르는 소리라 한 점에서 나지 않게 조금 퍼뜨린다")]
        public float spread = 60f;

        [Header("언제 부르는가")]
        [Tooltip("씬에 들어와 처음 부르기까지(초)")]
        public float firstDelay = 3f;
        [Tooltip("플레이어가 이 거리(m) 안에 있어야 부른다 — 아무도 없는데 부르지 않는다")]
        public float wakeDistance = 32f;
        [Tooltip("한 번 부른 뒤 쉬는 시간(초) — 이 사이에서 무작위")]
        public float restMin = 100f;
        public float restMax = 150f;

        [Header("대화")]
        [Tooltip("말을 걸거나(대화 시작) 청해 부른 노래 중에 대화를 끝내면, 이만큼(초)에 걸쳐 잦아들며 멎는다")]
        public float stopFade = 0.6f;

        [Header("배경음 덕킹")]
        [Range(0f, 1f)]
        [Tooltip("노래가 온전히 들릴 때 배경음을 이 배율까지 낮춘다")]
        public float duckTo = 0.35f;

        [Header("단서 A6")]
        [Tooltip("이 거리(m) 안에서")]
        public float hearDistance = 18f;
        [Tooltip("이만큼(초) 들으면 '들었다'")]
        public float hearDelay = 4f;

        public string[] childNames = { "VillageChild_01", "VillageChild_02", "VillageChild_03" };

        AudioSource src;
        Transform listener;
        NpcSchedule schedule;
        readonly List<NpcDialogue> talks = new List<NpcDialogue>();
        float nextAllowed;
        bool wasSinging;
        bool sungOnce;
        bool requested;          // 지금 곡이 대화에서 청해 시작된 것인가
        bool wasInDialogue;
        float heardT;
        Coroutine fading;

        public bool IsSinging => src != null && src.isPlaying;

        // ── 자체 부착 ─────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Ensure();

        static void Ensure()
        {
            var child = GameObject.Find("VillageChild_01");
            if (child == null || child.GetComponent<ChildrenSong>() != null) return;
            child.AddComponent<ChildrenSong>();
        }

        /// <summary>
        /// 처음 붙을 때와, 플레이 중 도메인 리로드 뒤에 온다. 리로드는 Instance·src 같은
        /// 비직렬화 상태를 지우므로 이미 만들어 둔 「아이들노래」 자식을 다시 찾아 잇는다.
        /// </summary>
        void OnEnable()
        {
            Instance = this;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (src == null) Setup();
        }

        void Setup()
        {
            schedule = GetComponent<NpcSchedule>();

            talks.Clear();
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var nm in childNames)
            {
                var g = GameObject.Find(nm);
                if (g == null) continue;
                sum += g.transform.position; n++;
                var d = g.GetComponent<NpcDialogue>();
                if (d != null) talks.Add(d);
            }
            Vector3 center = n > 0 ? sum / n : transform.position;

            var old = transform.Find("아이들노래");
            if (old != null)
            {
                // 리로드 전에 만들어 둔 것 — 그대로 잇는다
                src = old.GetComponent<AudioSource>();
                if (src != null) { nextAllowed = Time.time + firstDelay; return; }
                Destroy(old.gameObject);
            }

            var go = new GameObject("아이들노래");
            go.transform.SetParent(transform, false);
            go.transform.position = center + Vector3.up * 1.2f;   // 아이 입높이쯤
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.spread = spread;
            src.dopplerLevel = 0f;
            src.volume = volume;
            src.priority = 32;

            var lib = BgmLibrary.Load();
            var track = lib != null ? lib.Find(songId) : null;
            if (track != null && track.clip != null) src.clip = track.clip;
            else Debug.LogWarning("[아이들노래] 라이브러리에 " + songId + " 가 없다 — 노래가 나지 않는다.", this);

            nextAllowed = Time.time + firstDelay;
        }

        void OnDestroy()
        {
            if (wasSinging) Release();
            if (Instance == this) Instance = null;
            // ⚠️ sceneLoaded 구독은 남겨 둔다 — 정적 Ensure 라서 다음에 마을이 열릴 때 다시 붙어야 한다
        }

        // ── 매 프레임 ─────────────────────────────────────

        void Update()
        {
            if (src == null || src.clip == null) return;
            if (listener == null)
            {
                var l = FindFirstObjectByType<AudioListener>();
                listener = l != null ? l.transform : null;
                if (listener == null) return;
            }
            float d = Vector3.Distance(listener.position, src.transform.position);
            bool inDialogue = InDialogueWithChild();

            if (IsSinging)
            {
                // 밤이 되어 아이들이 자리를 떴으면 노래도 그친다 (보고 있는 동안에는 NpcSchedule이 안 바꾼다)
                if (schedule != null && !schedule.Visible) src.Stop();
                // 말을 걸면 그친다 — 청해서 부르는 곡만 대화 중에 이어진다
                else if (inDialogue && !requested) FadeOutAndStop("말을 걸어");
                // 청해 부르던 곡은 대화를 끝내면 그친다
                else if (!inDialogue && requested && wasInDialogue) FadeOutAndStop("대화가 끝나");
                else if (fading == null) WhileSinging(d);
                wasInDialogue = inDialogue;
                return;
            }
            wasInDialogue = inDialogue;

            if (wasSinging)
            {
                wasSinging = false;
                requested = false;
                nextAllowed = Time.time + Random.Range(restMin, restMax);
                Release();
                Debug.Log("[아이들노래] 그쳤다 — " + (nextAllowed - Time.time).ToString("F0") + "초 쉰다");
            }

            if (Time.time < nextAllowed) return;
            if (schedule != null && !schedule.Visible) return;
            if (d > wakeDistance) return;
            if (inDialogue) return;                       // 대화 중에는 새로 시작하지 않는다
            Sing("때가 되어", false);
        }

        void WhileSinging(float d)
        {
            float audibility = 1f - Mathf.Clamp01((d - minDistance) / Mathf.Max(0.1f, maxDistance - minDistance));
            if (MusicDirector.Instance != null)
                MusicDirector.Instance.SetDuck(Mathf.Lerp(1f, duckTo, audibility), 0.6f);

            if (d <= hearDistance)
            {
                heardT += Time.deltaTime;
                if (heardT >= hearDelay && !GyeonuCase.HasFlag(GyeonuWorld.F_아이노래들음))
                {
                    GyeonuCase.SetFlag(GyeonuWorld.F_아이노래들음);
                    GyeonuCase.AddClue(ClueId.A6);
                }
            }
        }

        void Release()
        {
            if (MusicDirector.Instance != null) MusicDirector.Instance.SetDuck(1f, 1.5f);
        }

        bool InDialogueWithChild()
        {
            foreach (var t in talks) if (t != null && t.Session != null) return true;
            return false;
        }

        // ── 시작 · 그침 ───────────────────────────────────

        /// <summary>대화에서 청했다 — 쉬는 중이어도, 방금 말을 걸어 잦아드는 중이어도 바로 부른다.</summary>
        public void RequestSing()
        {
            if (src == null || src.clip == null) return;
            if (fading != null) { StopCoroutine(fading); fading = null; src.Stop(); }
            else if (IsSinging) return;
            Sing("청을 받아", true);
        }

        void Sing(string why, bool byRequest)
        {
            src.volume = volume;
            src.Play();
            wasSinging = true;
            sungOnce = true;
            requested = byRequest;
            heardT = 0f;
            Debug.Log("[아이들노래] " + why + " 부른다 (" + src.clip.length.ToString("F0") + "초)");
        }

        void FadeOutAndStop(string why)
        {
            if (fading != null) return;
            fading = StartCoroutine(FadeOut(why));
        }

        IEnumerator FadeOut(string why)
        {
            float v0 = src.volume;
            float dur = Mathf.Max(0.05f, stopFade);
            float t = 0f;
            while (t < dur && src.isPlaying)
            {
                t += Time.deltaTime;
                src.volume = Mathf.Lerp(v0, 0f, t / dur);
                yield return null;
            }
            src.Stop();
            src.volume = volume;
            fading = null;
            Debug.Log("[아이들노래] " + why + " 그친다 (" + dur.ToString("F1") + "초 페이드)");
        }
    }
}
