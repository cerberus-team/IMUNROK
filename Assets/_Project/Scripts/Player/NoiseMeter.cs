using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>소리의 크기</b>를 한 군데서 잰다 — 내가 낸 소리든, 내가 지른 소리든.
    ///
    /// <b>왜 필요한가</b>: 1부는 잠행이다(<c>OnggojipCase.Phase.Stealth</c>). 남의 집
    /// 안방을 밤중에 뒤지는 짓인데, 여태 서랍을 부수든 뛰어다니든 아무도 돌아보지
    /// 않았다. 들킬 일이 없으면 뒤지는 것이 아니라 <b>구경하는 것</b>이 된다.
    ///
    /// <b>두 갈래를 한 눈금에 모은다</b>
    ///   ① <b>마이크</b> — 사람이 실제로 낸 소리. 헤드셋을 쓰고 "어이쿠" 하면 그것이
    ///      곧 소리다. 이것만은 흉내로 대신할 수가 없다.
    ///   ② <b>짓</b> — 문 여닫기·서랍 빼기·보료 들추기·뛰기. 이쪽은 재는 것이 아니라
    ///      <b>매기는 것</b>이다. 실제로 튼 소리를 재면 배경음악이 다 잡아먹고, 소리를
    ///      아직 안 넣은 짓은 영영 조용한 짓이 된다. 짓마다 크기를 손으로 정해 둔다.
    ///
    /// 둘을 <b>큰 쪽으로</b> 합친다(더하지 않는다). 문을 여는 순간 마침 기침을 했다고
    /// 두 배로 시끄러워질 까닭이 없다 — 사람은 큰 소리 하나를 듣는다.
    ///
    /// <b>사그라든다</b>: 낸 소리는 <see cref="_decay"/> 로 잦아든다. 한 번 시끄러웠다고
    /// 그 밤 내내 시끄러운 집이 되면 안 된다.
    ///
    /// 듣는 쪽은 <see cref="NoiseListener"/> 다. 이 부품은 <b>재기만</b> 하고 누가
    /// 어떻게 반응할지는 모른다 — 그래야 사람마다 귀가 다를 수 있다.
    ///
    /// 씬에 둘 필요 없다. 게임이 시작되면 스스로 붙는다.
    /// </summary>
    public class NoiseMeter : MonoBehaviour
    {
        public static NoiseMeter Instance { get; private set; }

        [Header("언제부터 <b>무게</b>가 실리나")]
        // <b>마이크는 늘 켜져 있다.</b> 끄고 켜는 것은 귀가 아니라 <b>무게</b>다.
        //
        // 한 번 마이크를 껐다 켜면 열리는 데 한 박자가 걸리고, 그 사이에 낸 소리는
        // 통째로 사라진다. 하필 그 순간이 <b>복동이 문을 닫고 나가는 바로 그때</b>다 —
        // 조심하기 시작해야 할 첫 순간이 귀머거리가 되는 셈이다.
        // 그래서 귀는 처음부터 열어 두고, 복동이 나가면 <b>듣는 쪽이 반응하기 시작</b>한다.
        [Tooltip("켜 두면 처음부터 무게가 실린다. <b>꺼 두는 것이 맞다</b> — " +
                 "마주 앉아 말하는 동안 말소리를 들킴으로 셀 수는 없다")]
        [SerializeField] private bool _armedAtStart = false;

        [Tooltip("무게가 실리기 전에도 눈금은 움직인다(마이크는 늘 켜져 있다). " +
                 "다만 <b>아무도 듣지 않는다</b> — 그때 낸 소리로는 들키지 않는다")]
        [SerializeField] private bool _micAlwaysOn = true;

        [Tooltip("심문이 열려 있는 동안에는 마이크를 <b>내준다</b>. 말을 알아듣는 쪽(VoiceSDK)이 " +
                 "써야 하기 때문이다 — 마이크는 한 번에 하나만 잡는다")]
        [SerializeField] private bool _yieldMicToInterrogation = true;

        [Header("마이크")]
        [Tooltip("사람이 낸 실제 소리를 잰다. 마이크가 없거나 권한이 없으면 저절로 꺼진다")]
        [SerializeField] private bool _useMic = true;
        [Tooltip("이보다 작은 소리는 방 안의 잡음으로 보고 버린다(RMS). 숨소리·팬 소리를 거른다")]
        [SerializeField] private float _micFloor = 0.012f;
        [Tooltip("이만큼이면 눈금이 꽉 찬다(RMS). 작게 잡을수록 예민하다")]
        [SerializeField] private float _micFull = 0.16f;
        [Tooltip("마이크 눈금이 오르내리는 빠르기. 낮으면 뭉근하고 높으면 딱딱 붙는다")]
        [SerializeField] private float _micSmooth = 12f;

        [Header("짓")]
        [Tooltip("낸 소리가 잦아드는 데 걸리는 시간(초). 이만큼이면 1에서 0으로 내려온다")]
        [SerializeField] private float _decay = 1.6f;

        [Header("<b>이어서</b> 내는 소리")]
        // <b>한 번 크게</b>가 아니라 <b>오래</b>가 문제다.
        //
        // 여태는 봉우리 하나로 사람을 불렀다. 그러면 서랍을 한 번 확 빼는 순간 들키고,
        // 살살 열되 <b>스무 번</b> 여는 것은 아무 일도 아니게 된다. 실제로 밤중에 남의
        // 집을 뒤키는 사람이 조심하는 것은 <b>한 번의 큰 소리</b>가 아니라 <b>부스럭거림이
        // 이어지는 것</b>이다. 문밖 사람은 소리 하나에 깨는 것이 아니라, 뭔가 계속
        // 나는구나 싶어 일어난다.
        //
        // 그래서 눈금 위에 <b>달아오름</b>을 하나 더 둔다. 아래 크기를 넘겨 소리를 내고
        // 있으면 달아오르고, 조용해지면 천천히 식는다. 다 달아오르면 사람이 온다.
        [Tooltip("이 크기를 넘겨 소리를 내고 있으면 <b>이어지는 소리</b>로 친다")]
        [Range(0f, 1f)] [SerializeField] private float _heatFloor = 0.12f;
        [Tooltip("그만한 소리를 이만큼(초) 이어서 내면 다 달아오른다 — 그때 사람이 온다")]
        [SerializeField] private float _heatSeconds = 4.0f;
        [Tooltip("조용해지면 초당 이만큼 식는다. 달아오르는 것보다 <b>느려야</b> 한다 — " +
                 "한 번 부스럭거린 집이 곧바로 조용한 집이 되지는 않는다")]
        [SerializeField] private float _coolPerSecond = 0.18f;

        /// <summary>얼마나 <b>이어서</b> 시끄러웠나(0~1). 1이면 사람이 올 때다.</summary>
        public static float Heat => Instance != null ? Instance._heat : 0f;

        /// <summary>달아오름을 식힌다. 사람이 한 번 다녀갔으면 처음부터 다시 센다.</summary>
        public static void CoolDown() { if (Instance != null) Instance._heat = 0f; }

        /// <summary>이만큼 더 식힌다(0~1). 요에 누워 숨을 죽이고 있을 때처럼.</summary>
        public static void Cool(float amount)
        {
            if (Instance != null) Instance._heat = Mathf.Max(0f, Instance._heat - amount);
        }

        private float _heat;

        [Header("들리는 거리")]
        // 12m 는 <b>너무 짧았다</b>. 소리는 닿는 거리 끝에서 0이 되도록 잦아들므로,
        // 서랍 빼는 소리(0.5)는 6m 까지만 가고 그 절반 거리에서 이미 0.25 로 떨어진다.
        // 거기에 벽 몫까지 곱하면 문밖 사람에게는 0.06 — 문턱(0.22)의 4분의 1이다.
        // <b>재는 것은 다 만들어 두고 아무도 못 듣는</b> 눈금이었다.
        // 방 한 칸이 12m 인 집이라 눈금 하나가 방을 건너가야 한다.
        [Tooltip("눈금이 1일 때 소리가 닿는 거리(m). 작은 소리는 이보다 가까이서만 들린다. " +
                 "방 한 칸을 건너갈 만큼은 되어야 문밖 사람이 듣는다")]
        [SerializeField] private float _fullRange = 20f;

        [Header("눈금 보여주기")]
        [Tooltip("화면 구석에 눈금을 그린다. 맞춰 볼 때만 켠다")]
        [SerializeField] private bool _showGauge = true;

        /// <summary>
        /// <b>지금 조심해야 하는 때인가.</b>
        ///
        /// 1부에서 조심할 일이 생기는 것은 <b>복동이 방을 나간 뒤</b>다. 그 전까지는
        /// 주인과 마주 앉아 이야기하는 중이고, 그때 소리를 재는 것은 뜻이 없다 —
        /// 말을 하라고 앉혀 놓고 말소리를 들킴으로 세는 셈이 된다.
        /// 게다가 그동안 마이크는 <b>심문</b>이 쥐고 있어야 한다(VoiceSDK). 마이크는
        /// 한 번에 하나만 잡을 수 있으므로, 재기 시작하는 때를 나누는 것이 곧 다툼을 막는 길이다.
        /// </summary>
        public static bool Armed => Instance != null && Instance._armed;

        /// <summary>조사가 시작됐다 — 이제부터 잰다. BokdongController 의 '다 나갔다'에 건다.</summary>
        public static void Arm() { if (Instance != null) Instance.SetArmed(true); }

        /// <summary>다시 조심할 일이 없어졌다 — 재기를 그만두고 마이크도 놓는다.</summary>
        public static void Disarm() { if (Instance != null) Instance.SetArmed(false); }

        /// <summary>지금 이 순간의 소리 크기(0~1). 마이크와 짓 가운데 큰 쪽.</summary>
        public static float Level => Instance != null ? Instance._level : 0f;

        /// <summary>마이크만 따로(0~1). 눈금을 두 줄로 그릴 때.</summary>
        public static float MicLevel => Instance != null ? Instance._mic : 0f;

        /// <summary>짓으로 낸 소리만 따로(0~1).</summary>
        public static float ActLevel => Instance != null ? Instance._act : 0f;

        /// <summary>
        /// <b>소리를 냈다</b>고 알린다.
        ///
        /// <paramref name="loudness"/> 는 0~1. 어림잡은 값 — 문 여닫기 0.55, 서랍 0.5,
        /// 보료 들추기 0.25, 뛰기 0.7. 이미 더 큰 소리가 울리는 중이면 덮어쓰지 않는다.
        /// </summary>
        public static void Report(Vector3 at, float loudness, string what = null)
        {
            if (Instance == null) return;
            Instance.ReportInternal(at, Mathf.Clamp01(loudness), what);
        }

        /// <summary>자리를 안 주면 사람이 선 자리에서 난 것으로 친다(뛰기·기침).</summary>
        public static void Report(float loudness, string what = null)
        {
            var cam = Camera.main;
            Report(cam != null ? cam.transform.position : Vector3.zero, loudness, what);
        }

        /// <summary>
        /// <b>소리를 내면서 알린다</b> — 귀에 들리는 것과 눈금에 잡히는 것이 한 번에.
        ///
        /// 따로 두면 반드시 어긋난다. 소리는 나는데 눈금은 안 오르거나, 눈금은 올랐는데
        /// 아무 소리도 안 나거나. <b>낸 크기가 곧 들리는 크기</b>여야 사람이 규칙을 익힌다.
        ///
        /// 3D 로 튼다 — 어디서 났는지 들려야 피할 데를 안다. VR 에서는 이것이 특히 크다.
        /// </summary>
        /// <param name="startAt">클립의 이 자리(초)부터 튼다. 0이면 처음부터</param>
        /// <param name="seconds">이만큼(초)만 튼다. 0이면 끝까지 — 소리 <b>한 토막만</b> 쓸 때 준다.
        /// 문 여는 소리처럼 앞에 스르륵이 있고 뒤에 쿵이 붙은 녹음에서, 앞 토막만 쓰는 데 쓴다.
        /// 뚝 끊으면 딸깍 소리가 나므로 끝에서 아주 짧게 잦아들며 멎는다</param>
        public static void Play(Vector3 at, AudioClip clip, float loudness, string what = null, float pitch = 1f,
                                float startAt = 0f, float seconds = 0f)
        {
            Report(at, loudness, what);
            if (clip == null || Instance == null) return;
            Instance.Shoot(at, clip, Mathf.Clamp01(loudness), pitch, startAt, seconds);
        }

        [Header("소리 내기")]
        [Tooltip("낸 소리를 실제로 트는 크기 배수. 눈금 1이 이 크기로 들린다")]
        [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 0.85f;
        [Tooltip("바로 곁에서 들리는 거리(m). 이보다 멀어지면 잦아든다")]
        [SerializeField] private float _sfxNear = 1.5f;
        [Tooltip("여기까지만 들린다(m). 귀가 닿는 거리와 맞춰 둔다")]
        [SerializeField] private float _sfxFar = 14f;
        [Tooltip("한꺼번에 울릴 수 있는 소리 수. 넘으면 가장 오래된 것을 밀어낸다")]
        [SerializeField] private int _sfxVoices = 8;

        private AudioSource[] _voices;
        private int _voice;

        /// <summary>3D 로 한 번 튼다. 목소리를 돌려 쓰므로 새로 만들지 않는다.</summary>
        private void Shoot(Vector3 at, AudioClip clip, float loudness, float pitch,
                           float startAt = 0f, float seconds = 0f)
        {
            if (_voices == null)
            {
                _voices = new AudioSource[Mathf.Max(1, _sfxVoices)];
                for (int i = 0; i < _voices.Length; i++)
                {
                    var go = new GameObject("소리_" + i);
                    go.transform.SetParent(transform, false);
                    var a = go.AddComponent<AudioSource>();
                    a.playOnAwake = false;
                    a.spatialBlend = 1f;                       // 3D — 어디서 났는지 들려야 한다
                    a.rolloffMode = AudioRolloffMode.Linear;
                    a.dopplerLevel = 0f;                       // 걸어 다니는 소리에 도플러는 군더더기
                    _voices[i] = a;
                }
            }
            var src = _voices[_voice];
            _voice = (_voice + 1) % _voices.Length;

            src.transform.position = at;
            src.minDistance = _sfxNear;
            src.maxDistance = _sfxFar;
            src.clip = clip;
            src.pitch = pitch;
            src.volume = loudness * _sfxVolume;
            src.time = (startAt > 0f && startAt < clip.length) ? startAt : 0f;
            src.Play();
            if (seconds > 0f) StartCoroutine(CutAfter(src, seconds, loudness * _sfxVolume));
        }

        /// <summary>
        /// <b>소리를 한 토막만 쓴다</b> — 이만큼 튼 뒤 아주 짧게 잦아들며 멎는다.
        ///
        /// 녹음 하나에 두 소리가 든 것이 있다. 문은 <b>스르륵</b> 밀리다가 끝에 <b>쿵</b>
        /// 하고 문틀에 닿는데, 손으로 살며시 미는 문에 그 쿵이 붙으면 밀 때마다 문을
        /// 걷어차는 소리가 난다. 그렇다고 뚝 끊으면 파형이 잘려 딸깍한다.
        /// </summary>
        private System.Collections.IEnumerator CutAfter(AudioSource src, float seconds, float from)
        {
            var clip = src.clip;
            yield return new WaitForSeconds(Mathf.Max(0.02f, seconds - FadeOut));
            float t = 0f;
            while (t < FadeOut && src != null && src.isPlaying && src.clip == clip)
            {
                t += Time.deltaTime;
                src.volume = Mathf.Lerp(from, 0f, t / FadeOut);
                yield return null;
            }
            if (src != null && src.clip == clip) { src.Stop(); src.volume = from; }
        }

        /// <summary>토막 끝에서 잦아드는 시간(초). 짧아야 끊긴 티가 안 나고, 너무 짧으면 딸깍한다.</summary>
        private const float FadeOut = 0.07f;

        // ── 안 ──

        private float _level, _mic, _act;
        private Vector3 _actAt;
        private string _actWhat;
        private string _device;
        private AudioClip _clip;
        private float[] _buf = new float[512];
        private bool _micReady;
        private float _retry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Instance != null) return;
            var go = new GameObject("[소리계]");
            Instance = go.AddComponent<NoiseMeter>();
            DontDestroyOnLoad(go);
        }

        private void OnDestroy() { StopMic(); if (Instance == this) Instance = null; }
        private void OnApplicationPause(bool paused) { if (paused) StopMic(); else StartMic(); }

        private bool _armed;

        private void Start()
        {
            _armed = _armedAtStart;
            if (_micAlwaysOn) StartMic();     // 귀는 처음부터 열어 둔다
        }

        private void SetArmed(bool on)
        {
            if (_armed == on) return;
            _armed = on;
            if (!on) { _act = 0f; _actWhat = null; }
            if (on && !_micReady) StartMic();
            Debug.Log(on
                ? "[소리계] 조사가 시작됐다 — 이제부터 소리가 무게를 가진다."
                : "[소리계] 무게를 내린다. 눈금은 그대로 움직인다.");
        }

        /// <summary>
        /// 마이크를 연다. <b>못 열려도 조용히 넘어간다</b> — 마이크가 없다고 게임이
        /// 멈추면 안 된다. 그때는 짓으로 낸 소리만으로 잰다.
        ///
        /// 헤드셋(안드로이드)에서는 <b>권한을 먼저 받아야</b> 한다. 안 받으면 장치
        /// 목록부터 비어 있어서, 마이크가 없는 컴퓨터와 구별이 안 된다.
        /// </summary>
        private void StartMic()
        {
            if (!_useMic || _micReady) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                return;      // 다음 프레임에 다시 온다(Update 에서 되짚는다)
            }
#endif
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.Log("[소리계] 마이크가 없다 — 짓으로 낸 소리만 잰다.");
                return;
            }
            _device = Microphone.devices[0];
            // 1초짜리 고리 하나면 된다. 저장하려는 것이 아니라 <b>지금 얼마나 큰가</b>만 본다.
            _clip = Microphone.Start(_device, true, 1, 16000);
            _micReady = _clip != null;
            if (_micReady) Debug.Log("[소리계] 마이크: " + _device);
        }

        private void StopMic()
        {
            if (!_micReady) return;
            Microphone.End(_device);
            _micReady = false;
            _clip = null;
        }

        private void ReportInternal(Vector3 at, float loudness, string what)
        {
            if (loudness <= _act) return;      // 더 큰 소리가 울리는 중이다
            _act = loudness;
            _actAt = at;
            _actWhat = what;
            Ring(at, loudness, what);
        }

        private void Update()
        {
            // <b>심문 중에는 마이크를 내준다.</b> 말을 알아듣는 쪽이 잡아야 하기 때문이다.
            // 창이 닫히면 도로 잡는다 — 이 갈아타기는 사람 눈에 안 띈다.
            bool talking = _yieldMicToInterrogation && InterrogationController.AnyOpen;
            if (talking && _micReady) StopMic();

            // 권한을 물어보고 온 참이면 여기서 다시 연다. 매 프레임 두드리면 안 된다 —
            // 장치 목록을 훑는 것도, 권한을 다시 묻는 것도 싼 일이 아니다.
            if (_useMic && !_micReady && !talking && (_micAlwaysOn || _armed))
            {
                _retry -= Time.deltaTime;
                if (_retry <= 0f) { _retry = 1.5f; StartMic(); }
            }

            // ① 마이크 — 고리에서 지금 자리 앞쪽을 떠다 RMS 를 낸다
            if (_micReady && _clip != null)
            {
                int pos = Microphone.GetPosition(_device) - _buf.Length;
                if (pos >= 0)
                {
                    _clip.GetData(_buf, pos);
                    double sum = 0;
                    for (int i = 0; i < _buf.Length; i++) sum += _buf[i] * _buf[i];
                    float rms = Mathf.Sqrt((float)(sum / _buf.Length));
                    float want = Mathf.InverseLerp(_micFloor, _micFull, rms);
                    _mic = Mathf.MoveTowards(_mic, want, _micSmooth * Time.deltaTime);
                }
            }
            else _mic = 0f;

            // ② 짓으로 낸 소리는 잦아든다
            if (_act > 0f)
            {
                _act = Mathf.MoveTowards(_act, 0f, Time.deltaTime / Mathf.Max(0.05f, _decay));
                if (_act <= 0f) _actWhat = null;
            }

            // ③ <b>둘을 합친다.</b>
            //
            // 여태 큰 쪽 하나만 셌다("사람은 큰 소리 하나를 듣는다"). 그런데 그러면
            // <b>서랍을 열면서 떠드는 것</b>이 서랍만 여는 것과 똑같아진다 — 실제로는
            // 그게 제일 위험한 짓인데도. 소리는 겹치면 더 시끄럽다.
            //
            // 그렇다고 그냥 더하면 0.6 짜리 둘이 1.2 가 되어 눈금이 곧장 꽉 찬다.
            // 소리는 <b>기운(에너지)으로 더해지므로</b> 제곱해 더하고 도로 제곱근을 낸다 —
            // 같은 크기 둘이 겹치면 1.41배가 되고, 하나가 작으면 큰 쪽에 거의 묻힌다.
            // 사람 귀가 실제로 그렇게 듣는다.
            _level = Mathf.Clamp01(Mathf.Sqrt(_mic * _mic + _act * _act));

            WorldGauge();

            // ④ 이어지는 소리 — 넘겨 내고 있으면 달아오르고, 그치면 식는다.
            //    무게가 실리기 전(복동이 나가기 전)에는 달아오르지 않는다.
            if (_armed && _level > _heatFloor)
                _heat = Mathf.Min(1f, _heat + Time.deltaTime / Mathf.Max(0.2f, _heatSeconds));
            else
                _heat = Mathf.Max(0f, _heat - _coolPerSecond * Time.deltaTime);

            // 마이크 소리는 <b>사람이 선 자리</b>에서 난다. 짓보다 크면 그 자리가 소리의 자리다.
            if (_mic > _act)
            {
                var cam = Camera.main;
                if (cam != null) Ring(cam.transform.position, _mic, "목소리");
            }
        }

        /// <summary>
        /// 소리가 퍼진다 — 듣는 귀들에게 알린다.
        ///
        /// <b>무게가 실리기 전에는 아무도 안 듣는다.</b> 눈금은 그대로 움직이므로
        /// 사람은 제가 얼마나 시끄러운지 미리 익힐 수 있고, 그러다 복동이 나가는 순간
        /// 같은 눈금이 <b>뜻을 가지기 시작한다</b>. 규칙을 새로 배울 것이 없다.
        /// </summary>
        private void Ring(Vector3 at, float loudness, string what)
        {
            if (!_armed) return;
            float reach = _fullRange * loudness;
            foreach (var ear in NoiseListener.All)
            {
                if (ear == null) continue;
                ear.Hear(at, loudness, reach, what);
            }
        }

        /// <summary>
        /// 화면 구석 눈금 — <b>모니터에서 맞춰 볼 때만</b> 쓴다.
        ///
        /// 헤드셋 안에서는 이 그림이 <b>보이지 않는다</b>. OnGUI 는 화면에 곧장 그리는
        /// 것이라 눈앞의 두 화면에는 안 올라온다. VR 에서 눈금을 보이려면 월드 공간에
        /// 세워야 한다(SubtitleView·ToolbeltHud 가 하는 방식).
        /// </summary>
        [Header("머리 위 알림판에도 보이기")]
        [Tooltip("화면 구석 눈금(OnGUI)은 <b>헤드셋에서 안 보인다</b>. 켜면 목표 알림판 아래에 " +
                 "월드 공간으로 한 줄 더 띄운다 — 헤드셋에서도 제 소리가 보인다")]
        [SerializeField] private bool _worldGauge = true;
        [Tooltip("알림판 높이(시선 기준). 목표 안내가 0.42 이므로 그 아래에 둔다")]
        [SerializeField] private float _worldGaugeHeight = 0.30f;

        private float _gaugeTick;

        /// <summary>
        /// <b>머리 위에 소리를 적는다.</b>
        ///
        /// 눈금이 화면 구석에만 있으면 헤드셋을 쓴 사람에게는 없는 것과 같다. 그런데
        /// 잠행에서 제일 알아야 할 것이 바로 그 눈금이다 — 지금 내가 시끄러운가.
        /// 목표 안내가 이미 눈 위에 떠 있으니 그 아래 한 줄을 붙인다.
        ///
        /// <b>무게가 실린 뒤에만</b> 뜬다. 그 전에는 눈금이 움직여도 아무 일이 아니므로,
        /// 띄워 두면 아직 조심할 것이 없는데 조심하게 만든다.
        /// </summary>
        private void WorldGauge()
        {
            if (!_worldGauge) return;
            if (!_armed) { StatusPanel.Clear("소리"); return; }

            _gaugeTick -= Time.deltaTime;
            if (_gaugeTick > 0f) return;
            _gaugeTick = 0.12f;

            // <b>"이어짐"이 무슨 말인지 아무도 몰랐다.</b>
            //
            // 눈금 이름은 <b>그것이 차면 무슨 일이 나는지</b>로 지어야 한다.
            // 이 눈금이 차면 늙은 하인이 살피러 온다. 그러니 이름은 "하인"이다.
            // "이어짐"은 <b>만든 사람이 아는 말</b>이지 하는 사람이 아는 말이 아니다.
            int n = Mathf.RoundToInt(Mathf.Clamp01(_level) * 10f);
            int hn = Mathf.RoundToInt(_heat * 10f);
            string bar = new string('■', n) + new string('·', 10 - n);
            string heat = new string('■', hn) + new string('·', 10 - hn);

            string tail = _heat > 0.75f ? "   *곧 온다 — 요로 가라*"
                        : _heat > 0.35f ? "   (조용히)"
                        : "";

            StatusPanel.Set("소리", 1,
                "지금 소리  " + bar + "\n" +
                "하인 오기  " + heat + tail);
        }

        private void OnGUI()
        {
            if (!_showGauge) return;

            // <b>조심할 것이 없으면 아무것도 안 그린다.</b>
            //
            // 여태 무게가 실리기 전에도 눈금을 흐리게나마 그렸다. "아직 아무도 안
            // 듣는다"고 적어 두면 친절하리라 여긴 것인데, 실제로는 표제에도 어전에도
            // 조사청에도 화면 왼쪽 아래에 막대 두 줄이 <b>늘</b> 박혀 있었다.
            // 영상을 찍으면 그것부터 눈에 든다.
            //
            // 잠행이 아닌 데서는 소리가 아무 뜻이 없다. 뜻이 생길 때(Arm) 나타나면 된다.
            if (!_armed) return;
            const float w = 220f, h = 14f;
            float x = 16f, y = Screen.height - 76f;

            // 무게가 실리기 전에는 눈금이 <b>흐리게</b> 뜬다 — 움직이기는 하되 아직
            // 아무 일도 아니라는 것이 한눈에 보여야 한다.
            var was = GUI.color;
            if (!_armed) GUI.color = new Color(1f, 1f, 1f, 0.45f);

            GUI.Label(new Rect(x, y - 20f, 340f, 20f),
                      (_armed ? "소리 " : "소리(아직 아무도 안 듣는다) ") + $"{_level * 100f:F0}%"
                      + (string.IsNullOrEmpty(_actWhat) ? "" : "  ← " + _actWhat));
            GUI.color = was;
            Bar(new Rect(x, y, w, h), _mic, new Color(0.45f, 0.75f, 1f), "마이크");
            Bar(new Rect(x, y + h + 4f, w, h), _act, new Color(1f, 0.72f, 0.35f), "짓");
        }

        private static Texture2D _px;
        private static void Bar(Rect r, float v, Color c, string tag)
        {
            if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, _px);
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), _px);
            GUI.color = old;
            GUI.Label(new Rect(r.x + r.width + 8f, r.y - 2f, 80f, 20f), tag);
        }
    }
}
