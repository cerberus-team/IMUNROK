using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 진입 안개 연출 — 지정한 구간을 걸어 나아갈수록 짙은 안개가 걷힌다.
    /// 관아 언덕길뿐 아니라 견우마을 숲길·은하담 진입 등 "한 구간 걸어 들어오며 시야가
    /// 열리는" 연출에 그대로 재사용할 수 있게 씬 의존 값을 두지 않았다.
    ///
    /// ■ 왜 시간이 아니라 거리인가
    ///   연출의 의미가 "언덕을 오르는 동안"이라 진행도는 **구간 투영 거리**로 잰다.
    ///   시간 기반이면 플레이어가 멈춰 서서 둘러보는 동안에도 안개가 걷혀 버리고,
    ///   반대로 뛰어가면 아직 언덕 밑인데 이미 다 걷힌 상태가 된다.
    ///   다만 순간이동·질주에 안개가 튀지 않도록 **초당 변화량 상한(maxClearSpeed)** 을
    ///   시간 기반으로 걸어 두 방식의 장점만 취한다.
    ///
    /// ■ 되돌아가도 다시 짙어지지 않는 이유
    ///   진행도는 단조 증가(_progress = Max(_progress, 지금값))로만 갱신한다.
    ///   그래서 언덕을 다 오른 뒤 내려갔다 올라와도 재발동하지 않는다.
    ///
    /// ■ 끝 상태 = 씬의 현재 안개 설정
    ///   Awake에서 RenderSettings를 그대로 읽어 목표값으로 삼는다. 즉 하늘 프리셋이
    ///   칠해 둔 값이 최종 도착 상태이고, 이 컴포넌트는 "시작할 때만 그보다 짙게"만 한다.
    ///   프리셋 에셋은 절대 건드리지 않는다.
    ///
    /// ■ 반대 방향(내려갈수록 짙어지는) 연출도 같은 컴포넌트로 낸다
    ///   구간을 뒤집어 잡으면(from = 짙은 끝 = 아래, to = 맑은 끝 = 위) 그대로 하산 연출이 된다.
    ///   단 <see cref="monotonic"/> 을 끄고 <see cref="armAfterReachingEnd"/> 를 켤 것 —
    ///   게이트가 없으면 씬에 들어선 순간(아래에 서 있다) 곧바로 짙어져 진입 연출과 겹친다.
    ///   관아는 이 방식으로 **진입용 / 하산용 FogReveal 두 개**를 각자 제 값으로 돌린다.
    ///   (진입은 약하게 0.34, 하산은 앞이 안 보일 만큼 짙게 — 하나로는 두 값을 못 낸다)
    ///
    /// ■ 나중에 씬 전환 시스템이 붙으면
    ///   arm = External 로 바꾸고, 전환 직전에 <see cref="Arm"/>(revealKey) 를 호출하면
    ///   "마을에서 올라왔을 때만" 발동한다. 그 전까지는 SceneEntry(씬 진입 = 첫 진입).
    /// </summary>
    [AddComponentMenu("이문록/진입 안개 연출 (FogReveal)")]
    [DisallowMultipleComponent]
    public class FogReveal : MonoBehaviour
    {
        public enum ArmMode
        {
            /// <summary>씬이 로드될 때마다 발동 (현재 기본 — 씬 진입 = 첫 진입).</summary>
            SceneEntry,
            /// <summary>앱 실행 중 최초 1회만. 씬을 다시 로드해도 재발동하지 않는다.</summary>
            OncePerSession,
            /// <summary>Arm(key) 를 미리 호출해 둔 경우에만 발동 — 씬 전환 시스템용.</summary>
            External,
        }

        [Header("발동 조건")]
        public ArmMode arm = ArmMode.SceneEntry;
        [Tooltip("OncePerSession / External 모드에서 이 연출을 구분하는 키")]
        public string revealKey = "";

        [Header("진행 구간 (마커가 있으면 마커 우선)")]
        public Transform fromMarker;
        public Transform toMarker;
        public Vector3 fromPoint;
        public Vector3 toPoint;
        [Tooltip("높이차를 무시하고 평면 거리로만 진행도를 잰다 — 경사·계단에서 안정적")]
        public bool flatten = true;

        [Tooltip("끄면 진행도가 **위치만 따라간다** — 되돌아 내려가면 안개가 다시 짙어진다.\n" +
                 "언덕이 양방향 통로일 때 쓴다: 올라오면 걷히고 내려가면 다시 자욱해져,\n" +
                 "아래가 마을인지 숲인지 모르는 상태가 유지된다. 이 모드에서는 스스로 끝나지 않는다.")]
        public bool monotonic = true;

        [Tooltip("켜면 **진행도 1에 한 번 닿기 전까지는 안개를 일절 내지 않는다.**\n" +
                 "\"내려갈수록 짙어지는\" 연출용 게이트다: 구간을 from=짙은 끝 / to=맑은 끝 으로 잡고\n" +
                 "monotonic 을 끄면 위치를 그대로 따라가는데, 그러면 **씬에 들어온 순간 아래쪽에\n" +
                 "서 있는 플레이어에게도 곧바로 짙은 안개가 걸린다** — 올라오는 연출과 값이 겹친다.\n" +
                 "이 게이트를 켜면 '한 번 위까지 올라가 본 뒤'에만 발동하므로,\n" +
                 "올라올 때는 진입 안개(별개 FogReveal)가, 내려갈 때는 이쪽이 각자 제 값으로 논다.")]
        public bool armAfterReachingEnd = false;

        [Header("시작(짙은) 안개 — 끝 상태는 씬의 현재 설정을 그대로 쓴다")]
        public float startFogStart = 3f;
        public float startFogEnd = 26f;
        [Tooltip("Exponential 계열 안개일 때만 사용")]
        public float startFogDensity = 0.09f;
        public bool overrideStartColor = false;
        public Color startFogColor = new Color(0.82f, 0.85f, 0.88f);

        [Header("Linear Fog 사용 여부")]
        // ★유니티 기본 Fog 는 원리가 lerp(물체색, 안개색, f(거리)) 다 —— **화면에 이미 그려진 픽셀을
        //  안개색으로 물들이는 것**이라 물체 색을 안 바꿀 방법이 없다. 거리를 아무리 밀어도
        //  가까운 물체까지 조금씩 씻긴다. 관아는 담장·동헌 색이 정확해야 하므로 아예 끄고
        //  안개감은 파티클(공기 중의 별개 지오메트리)과 하늘 베일로만 낸다.
        [Tooltip("끄면 RenderSettings 안개를 일절 건드리지 않는다. 안개감은 파티클·하늘베일로만 낸다")]
        public bool useLinearFog = true;

        [Header("끝 상태 — 켜면 씬 안개 대신 이 값으로 끝낸다")]
        // 원래는 arm 시점의 RenderSettings 를 그대로 '끝 상태'로 삼았다. 그러면 연출이 끝난 뒤에도
        // 씬 안개(관아는 40~160)가 남아 담장·동헌 색을 씻는다. 게다가 끝 상태를 언제 캡처하느냐가
        // WorldTimeSync 와의 실행 순서에 좌우돼 불안정했다.
        // → 걷힌 뒤 안개가 없어야 하는 씬은 여기에 못박는다(실행 순서와 무관해진다).
        [Tooltip("켜면 연출이 끝났을 때 안개를 아래 거리로 밀어 사실상 없앤다")]
        public bool overrideEndFog = false;
        public float endFogStart = 250f, endFogEnd = 1500f;

        [Header("보간")]
        public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("초당 진행도 상한 — 순간이동·질주에도 안개가 튀지 않게 한다")]
        public float maxClearSpeed = 0.6f;

        [Header("플레이어 (비우면 MainCamera)")]
        public Transform player;

        [Header("안개 파티클 (선택) — 공기 중에 실제로 떠 있는 것")]
        [Tooltip("Linear Fog는 화면에 그려진 픽셀만 물들여서 짙게 할수록 '칠한 것'처럼 보인다. " +
                 "빈 공간에 실제 지오메트리를 띄우는 건 파티클뿐이라 이게 주역이다.")]
        public ParticleSystem[] fogParticles;
        [Range(0f, 1f)] public float particleStartAlpha = 0.16f;

        [Header("하늘 베일 (선택) — 스카이박스까지 안개색에 잠기게")]
        [Tooltip("Unity 안개는 스카이박스에 걸리지 않는다. 카메라를 감싸는 큰 구를 안개색 반투명으로 " +
                 "덮어 하늘 픽셀만 물들인다(가까운 물체는 깊이 테스트로 걸러진다). " +
                 "⚠️ 공유 스카이박스 머티리얼은 건드리지 않는다.")]
        public Renderer skyVeil;
        [Range(0f, 1f)] public float skyVeilStartAlpha = 0.55f;

        MaterialPropertyBlock _mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        // ── 세션 상태 (도메인 리로드/앱 재시작 때 초기화) ──
        static readonly HashSet<string> _consumed = new HashSet<string>();
        static readonly HashSet<string> _armed = new HashSet<string>();

        /// <summary>씬 전환 시스템이 로드 직전에 호출 — External 모드 연출을 예약한다.</summary>
        public static void Arm(string key) { if (!string.IsNullOrEmpty(key)) _armed.Add(key); }
        /// <summary>이 키의 연출을 이번 세션에서 소비 처리 (재발동 차단).</summary>
        public static void Consume(string key) { if (!string.IsNullOrEmpty(key)) _consumed.Add(key); }
        /// <summary>다시 볼 수 있게 되돌린다 (디버그·테스트용).</summary>
        public static void ResetKey(string key) { _consumed.Remove(key); _armed.Remove(key); }
        public static void ResetAll() { _consumed.Clear(); _armed.Clear(); }

        /// <summary>0=짙은 안개 / 1=완전히 걷힘.</summary>
        public float Progress => _shown;
        public bool IsPlaying => _active && !_done;

        bool _active, _done;
        float _progress, _shown;
        /// <summary>armAfterReachingEnd 게이트. 열리기 전에는 안개를 내지 않는다(= 항상 맑음).</summary>
        bool _gateOpen;

        // 끝 상태 (= 하늘 프리셋이 칠해 둔 값)
        bool _endFog;
        FogMode _endMode;
        Color _endColor;
        float _endStart, _endEnd, _endDensity;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            CaptureEndState();

            bool armed;
            switch (arm)
            {
                case ArmMode.OncePerSession: armed = !_consumed.Contains(revealKey); break;
                case ArmMode.External: armed = _armed.Contains(revealKey) && !_consumed.Contains(revealKey); break;
                default: armed = true; break;
            }
            if (!armed)
            {
                // 발동하지 않을 때도 파티클·베일은 반드시 꺼 둔다 —
                // 에디터 프리뷰용으로 켜 둔 상태가 그대로 남으면 씬이 계속 뿌옇다
                RestoreEndState();
                enabled = false;
                return;
            }

            _active = true;
            _progress = _shown = 0f;
            _gateOpen = !armAfterReachingEnd;   // 게이트를 안 쓰면 처음부터 열린 셈

            // 비단조 모드는 첫 프레임 값을 **플레이어 위치**에서 얻어야 한다.
            // 0으로 시작하면 언덕 위(집무실에서 나온 자리)에 서 있는데도 한 번 자욱해졌다가
            // 걷히는 헛연출이 보인다. 위치를 아직 모르면 Start에서 다시 잡는다.
            if (!monotonic)
            {
                if (player == null && Camera.main != null) player = Camera.main.transform;
                if (player != null) { _progress = _shown = RawProgress(); UpdateGate(); }
            }

            ApplyFog(Eval());   // 첫 프레임부터 제 값으로
        }

        void OnEnable() { if (_active && !_done) ApplyFog(Eval()); }

        void Start()
        {
            bool hadPlayer = player != null;
            if (player == null && Camera.main != null) player = Camera.main.transform;

            // Awake 때 플레이어를 못 찾았으면 여기서 위치 기준을 다시 잡는다 (비단조 모드)
            if (!monotonic && !hadPlayer && player != null) { _progress = _shown = RawProgress(); UpdateGate(); }

            if (_active && !_done) ApplyFog(Eval());
        }

        void LateUpdate()
        {
            if (!_active || _done) return;
            if (player == null)
            {
                if (Camera.main == null) return;
                player = Camera.main.transform;
            }

            float raw = RawProgress();
            UpdateGate(raw);
            _progress = monotonic ? Mathf.Max(_progress, raw) : raw;   // 단조 모드만 되돌아가도 안 짙어진다
            _shown = Mathf.MoveTowards(_shown, _progress, maxClearSpeed * Time.deltaTime);
            ApplyFog(Eval());

            // 비단조 모드는 끝내지 않는다 — 끝내 버리면 내려갈 때 다시 짙어질 수 없다.
            if (monotonic && _shown >= 0.9995f) Finish();
        }

        /// <summary>
        /// 실제로 화면에 적용할 값. 게이트가 닫혀 있으면 **무조건 1(= 완전히 걷힘)** 이다.
        /// 게이트를 안 쓰는 연출(진입 안개)에서는 _gateOpen 이 Awake에서 true라 아무 영향이 없다.
        /// </summary>
        float Eval() => _gateOpen ? curve.Evaluate(_shown) : 1f;

        void UpdateGate() { if (player != null) UpdateGate(RawProgress()); }

        /// <summary>진행도 1(맑은 끝)에 한 번 닿으면 게이트를 연다. 한 번 열리면 닫히지 않는다.</summary>
        void UpdateGate(float raw)
        {
            if (!_gateOpen && raw >= 0.999f) _gateOpen = true;
        }

        float RawProgress()
        {
            Vector3 a = fromMarker != null ? fromMarker.position : fromPoint;
            Vector3 b = toMarker != null ? toMarker.position : toPoint;
            Vector3 p = player.position;
            if (flatten) { a.y = 0f; b.y = 0f; p.y = 0f; }
            Vector3 ab = b - a;
            float sq = ab.sqrMagnitude;
            if (sq < 1e-4f) return 1f;                       // 구간이 비어 있으면 즉시 종료
            return Mathf.Clamp01(Vector3.Dot(p - a, ab) / sq);
        }

        void CaptureEndState()
        {
            _endFog = RenderSettings.fog;
            _endMode = RenderSettings.fogMode;
            _endColor = RenderSettings.fogColor;
            _endStart = RenderSettings.fogStartDistance;
            _endEnd = RenderSettings.fogEndDistance;
            _endDensity = RenderSettings.fogDensity;

            if (overrideEndFog)
            {
                _endStart = endFogStart;
                _endEnd = endFogEnd;
                _endDensity = 0f;
            }
        }

        void ApplyFog(float t)
        {
            if (useLinearFog)
            {
                RenderSettings.fog = true;                   // 안개가 꺼진 씬에서도 연출 동안은 켠다
                RenderSettings.fogMode = _endMode;
                RenderSettings.fogStartDistance = Mathf.Lerp(startFogStart, _endStart, t);
                RenderSettings.fogEndDistance = Mathf.Lerp(startFogEnd, _endEnd, t);
                RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, _endDensity, t);
                if (overrideStartColor) RenderSettings.fogColor = Color.Lerp(startFogColor, _endColor, t);
            }

            // 파티클·하늘 베일도 같은 진행도로 옅어진다.
            // ⚠️ 머티리얼을 직접 만지면 에디터에서 공유 에셋이 더러워진다 → MaterialPropertyBlock 사용
            float a = Mathf.Lerp(particleStartAlpha, 0f, t);
            SetParticleAlpha(a);
            SetVeilAlpha(Mathf.Lerp(skyVeilStartAlpha, 0f, t));
        }

        void SetParticleAlpha(float a)
        {
            if (fogParticles == null) return;
            // 도메인 리로드(스크립트 재컴파일)가 일어나면 _mpb 가 null 이 된 채 LateUpdate 가 계속 돌아
            // 콘솔이 ArgumentNullException 으로 도배된다. 여기서 한 번 더 세워 준다.
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            foreach (var ps in fogParticles)
            {
                if (ps == null) continue;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                var c = _endColor; c.a = a;
                _mpb.SetColor(BaseColorId, c);
                _mpb.SetColor(ColorId, c);      // Sprites/Default 계열 대비
                r.SetPropertyBlock(_mpb);
                r.enabled = a > 0.002f;
            }
        }

        void SetVeilAlpha(float a)
        {
            if (skyVeil == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            skyVeil.GetPropertyBlock(_mpb);
            var c = _endColor; c.a = a;
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId, c);
            skyVeil.SetPropertyBlock(_mpb);
            skyVeil.enabled = a > 0.002f;
        }

        /// <summary>끝 상태를 정확히 복원하고 스스로 멈춘다 (보간 오차를 남기지 않는다).</summary>
        void Finish()
        {
            RestoreEndState();
            _done = true;
            _shown = _progress = 1f;
            Consume(revealKey);
            enabled = false;
        }

        void RestoreEndState()
        {
            if (useLinearFog)
            {
                RenderSettings.fog = _endFog;
                RenderSettings.fogMode = _endMode;
                RenderSettings.fogColor = _endColor;
                RenderSettings.fogStartDistance = _endStart;
                RenderSettings.fogEndDistance = _endEnd;
                RenderSettings.fogDensity = _endDensity;
            }
            else RenderSettings.fog = false;   // 파티클 전용 — 씬 안개는 처음부터 끝까지 꺼 둔다

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            SetParticleAlpha(0f);
            SetVeilAlpha(0f);
            if (fogParticles != null)
                foreach (var ps in fogParticles)
                    if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // 연출 도중 씬을 떠나거나 오브젝트가 꺼져도 안개를 짙은 채로 남기지 않는다
        void OnDisable() { if (_active && !_done) RestoreEndState(); }
        void OnDestroy() { if (_active && !_done) RestoreEndState(); }
    }
}
