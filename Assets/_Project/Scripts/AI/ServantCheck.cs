using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>늙은 하인이 살피러 온다</b> — 밤에 안방에서 소리가 나면.
    ///
    /// 소리계(<see cref="NoiseMeter"/>)와 귀(<see cref="NoiseListener"/>)는 만들어 두고도
    /// 오래 죽어 있었다. 눈금이 올라가도 <b>아무도 오지 않았기</b> 때문이다. 들킴은 숫자가
    /// 아니라 <b>사람이 오는 일</b>이라야 무섭다.
    ///
    /// 그가 하는 일은 셋뿐이다.
    ///   ① <b>돌아본다</b> — 조금 들렸다. 하던 자리에서 소리 난 쪽으로 몸만 돌린다.
    ///   ② <b>살피러 온다</b> — 크게 들렸다. 담벼락에서 방 앞까지 걸어와 한 마디 묻는다.
    ///   ③ <b>돌아간다</b> — 잠깐 서 있다가 제자리로.
    ///
    /// <b>방에는 안 들어온다.</b> 신을 신은 하인이 주인의 사랑방에 그냥 올라서는 법은 없다.
    /// 문 밖 툇마루까지 와서 묻는다 — 그래서 더 무섭다. 문 하나를 사이에 두고,
    /// 방 안의 사람은 숨을 죽이고 있어야 한다.
    ///
    /// <b>묻는 말이 곧 함정이다</b>: "나리께서 무슨 일이냐 물으십니다" — 이 집에서
    /// 나리는 甲이다. 손님은 그 말을 듣는 순간, 자기가 지금 <b>누구의 집을</b> 뒤지고
    /// 있는지 다시 새기게 된다.
    ///
    /// 붙이는 곳: 하인 본인. 같은 오브젝트의 <see cref="NoiseListener"/> 이벤트를
    /// <see cref="OnNoticed"/>·<see cref="OnAlarmed"/> 에 물린다.
    /// </summary>
    public class ServantCheck : MonoBehaviour
    {
        [Header("자리")]
        [Tooltip("평소 서 있는 곳(담벼락). 비우면 게임이 시작될 때 선 자리를 제 자리로 삼는다")]
        [SerializeField] private Transform _home;
        [Tooltip("살피러 오는 곳 — <b>문 밖</b>이다. 방 안으로는 들어오지 않는다")]
        [SerializeField] private Transform _checkSpot;
        [Tooltip("밤자리에서 살피는자리까지 <b>돌아 오는 길</b>. 순서대로 거친다. " +
                 "비우면 곧게 온다 — 사이에 담이 있으면 담을 뚫고 온다")]
        [SerializeField] private Transform[] _via;

        [Header("걸음")]
        [Tooltip("걷는 빠르기(m/초). 늙은 사람의 걸음이다 — 뛰지 않는다")]
        [SerializeField] private float _walkSpeed = 0.85f;
        [Tooltip("도는 빠르기(초당 도)")]
        [SerializeField] private float _turnSpeed = 200f;
        [Tooltip("이만큼 안에 들면 다 온 것으로 친다(m)")]
        [SerializeField] private float _arrive = 0.25f;

        [Header("동작")]
        [Tooltip("비우면 자식에서 찾는다")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _idleState = "Idle";
        [SerializeField] private string _walkState = "Walking";

        [Header("무슨 말을 하나")]
        [SerializeField] private string _speaker = "늙은 하인";
        [Tooltip("살피러 와서 하는 말. 여럿이면 돌아가며 한다 — 같은 말을 세 번 들으면 사람이 아니라 기계가 된다")]
        [TextArea(2, 3)]
        [SerializeField] private string[] _lines = {
            "나리, 아직 안 주무십니까. *무슨 일이 있으신가* 여쭈랍니다.",
            "이 밤중에 무슨 소리가 났는데… *깨어 계십니까*.",
            "나리께서 *잠이 깨셨습니다*. 곧 건너오신다 하십니다."
        };
        [Tooltip("돌아볼 만큼만 들렸을 때 혼잣말. 비우면 몸만 돌린다")]
        [TextArea(2, 3)]
        [SerializeField] private string _mutter = "";

        [TextArea(2, 3)]
        [Tooltip("와서 보니 <b>손님이 자고 있을 때</b> 하는 말. 이때는 들킨 것으로 세지 않는다 — " +
                 "요에 누워 숨을 죽인 것이 통한 것이다")]
        [SerializeField] private string[] _asleepLines = {
            "…주무시는군. 헛것을 들었나 보다.",
            "곤히 주무시는데… 내가 잘못 들었나 보오."
        };

        [Header("사이")]
        [Tooltip("와서 이만큼(초) 서 있다가 돌아간다")]
        [SerializeField] private float _staySeconds = 7f;
        [Tooltip("한 번 다녀가면 이만큼(초)은 다시 안 온다")]
        [SerializeField] private float _cooldown = 12f;   // 30초 동안은 무슨 소리를 내도 안 와서 마음껏 뒤질 수 있었다

        [Header("눈에 보이게")]
        [Tooltip("하인이 들고 오는 등불. 켜 두면 <b>창호에 그림자</b>가 지나가 방 안에서도 " +
                 "누가 오는지 보인다 — 자막만으로는 '밖에 사람이 있다'가 안 보였다")]
        [SerializeField] private bool _carryLantern = true;
        [Tooltip("등불 빛깔·세기")]
        [SerializeField] private Color _lampColor = new Color(1f, 0.78f, 0.45f);
        [SerializeField] private float _lampRange = 4.5f;
        [SerializeField] private float _lampIntensity = 2.2f;

        [Header("다가오는 것이 들리게")]
        [Tooltip("흙·돌을 딛는 발소리. 걸음마다 하나씩 돌아가며 튼다")]
        [SerializeField] private AudioClip[] _stepsYard;
        [Tooltip("툇마루를 딛는 발소리")]
        [SerializeField] private AudioClip[] _stepsWood;
        [Tooltip("마루에 올라서면 가끔 삐걱인다")]
        [SerializeField] private AudioClip[] _creaks;
        [Tooltip("발소리 크기")]
        [Range(0f, 1f)] [SerializeField] private float _stepVolume = 0.9f;
        [Tooltip("여기까지 들린다(m). 마당 끝에서 떠나도 방 안에서 들려야 한다")]
        [SerializeField] private float _stepHearing = 26f;
        [Tooltip("이 높이보다 위를 딛으면 마루로 친다(월드 y). 마당은 −1.6, 툇마루는 −0.8 이다")]
        [SerializeField] private float _woodAbove = -1.2f;

        [Header("올 때 노래를 죽인다")]
        [Tooltip("살피러 오는 동안 방 노래를 이만큼으로 낮춘다(0~1). 0.15 면 거의 멎는다. " +
                 "사람이 오는 순간에 노래가 그대로 흐르면 그 순간이 <b>장면</b>이 되지 않는다")]
        [Range(0f, 1f)] [SerializeField] private float _duckMusicTo = 0.15f;

        [Header("왔을 때")]
        [Tooltip("살피러 오기 시작할 때 한 번")]
        [SerializeField] private UnityEvent _onComing;
        [Tooltip("몇 번이나 오게 만들면 <b>들킨 것</b>으로 칠지. 0이면 안 센다")]
        [SerializeField] private int _caughtAt = 3;
        [SerializeField] private UnityEvent _onCaught;

        /// <summary>지금 살피러 와 있나. 다른 곳에서 "지금은 움직이지 마라"에 쓴다.</summary>
        public static bool Watching { get; private set; }

        /// <summary>문 앞으로 <b>걸어오는 중</b>인가. 이때만 목숨이 위태롭다 — 닿기 전에 요에 누우면 산다.</summary>
        public static bool Coming => _instance != null && _instance._phase == Phase.오는중;

        /// <summary>지금 씬에 서 있는 하인. 씬이 달라 인스펙터로 못 잇는 쪽에서 부른다.</summary>
        private static ServantCheck _instance;

        /// <summary>
        /// <b>방 안의 귀가 하인을 부른다.</b>
        ///
        /// 뒤지는 방(사랑방)과 하인이 선 마당은 <b>다른 씬</b>이다. 유니티는 씬을 건너뛰는
        /// 참조를 저장하지 못하므로 인스펙터로는 이을 수가 없다. 그렇다고 하인을 방 씬에
        /// 넣으면, 마당에 나갔을 때 그가 <b>사라진다</b> — 저녁에 행랑에서 마주치는 그 사람이
        /// 없어지는 것이다. 그래서 부르는 쪽이 이름 없이 부른다.
        /// </summary>
        public static void Summon(Vector3 at)
        {
            if (_instance != null) _instance.OnAlarmed(at);
        }

        /// <summary>조금 들렸다 — 몸만 돌린다. 위와 같은 까닭으로 정적이다.</summary>
        public static void Glance(Vector3 at)
        {
            if (_instance != null) _instance.OnNoticed(at);
        }

        private void OnEnable() { _instance = this; }
        private void OnDisable() { if (_instance == this) { _instance = null; Watching = false; } }   // 씬을 떠나면 「보는 중」도 걷는다

        /// <summary>몇 번이나 오게 만들었나.</summary>
        public int Visits { get; private set; }

        private enum Phase { 제자리, 오는중, 살핌, 돌아감 }
        private Phase _phase = Phase.제자리;
        private Vector3 _homePos;
        private Quaternion _homeRot;
        private float _until;
        private float _quietUntil;
        private int _lineTurn;
        private bool _caught;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            FoldModelYaw();
            _travel.Bind(transform, _animator);
            _homePos = _home != null ? _home.position : transform.position;
            _homeRot = _home != null ? _home.rotation * _modelYaw : transform.rotation;
        }

        /// <summary>
        /// <b>몸의 앞을 걷는 방향과 맞춘다.</b>
        ///
        /// 모형(하인_모형)이 뿌리보다 299.5° 틀어져 앉혀 있었다. 서 있을 때는 그 틀어진 쪽을
        /// 보는 것이 맞았지만, 걸을 때는 뿌리의 앞으로 가므로 <b>몸이 61° 옆을 보며 게걸음</b>을
        /// 쳤다 — 「걷는 폼이 아쉽다」. 틀어진 만큼을 뿌리로 옮기고 모형은 곧게 둔다.
        /// 서 있는 모습은 그대로다(밤자리 방향에도 같은 만큼을 더한다).
        /// </summary>
        private void FoldModelYaw()
        {
            if (_animator == null || _animator.transform == transform) return;
            var model = _animator.transform;
            float yaw = model.localEulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)) < 0.5f) return;
            _modelYaw = Quaternion.Euler(0f, yaw, 0f);
            Vector3 keep = model.position;
            transform.rotation = transform.rotation * _modelYaw;
            model.localRotation = Quaternion.Inverse(_modelYaw) * model.localRotation;
            model.position = keep;
        }

        private Quaternion _modelYaw = Quaternion.identity;

        /// <summary>걸음이 엉덩이뼈에 구워져 있어(믹사모) 몸이 앞서 떠 가다 되튄다 — 되민다.</summary>
        private readonly BakedTravel _travel = new BakedTravel();

        private Light _lamp;
        private float _lampBase;
        private AudioSource _feet;
        private float _stepLeft;
        private int _stepTurn;
        private float _natural;          // 동작 속도 1일 때 몸이 실제로 나아가는 빠르기(m/초)
        private float _lastAhead;
        private bool _aheadValid;
        private float _route;            // 부름을 받았을 때 문 앞까지 남은 길(m)
        private float _barTick;

        private void Start()
        {
            Play(_idleState);
            if (!_carryLantern) return;
            // 손에 든 등불 — 켜 두면 창호에 그림자가 진다
            var go = new GameObject("하인_등불");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.22f, 0.95f, 0.12f);
            _lamp = go.AddComponent<Light>();
            _lamp.type = LightType.Point;
            _lamp.color = _lampColor;
            _lamp.range = _lampRange;
            _lamp.intensity = _lampIntensity;
            _lamp.shadows = LightShadows.Soft;   // 그림자가 져야 창호에 비친다
            _lampBase = _lampIntensity;
        }

        /// <summary>제 발소리를 낼 귀. 3D 라 멀리서는 작고 문 앞에 올수록 커진다.</summary>
        private AudioSource Feet()
        {
            if (_feet != null) return _feet;
            var go = new GameObject("하인_발소리");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            _feet = go.AddComponent<AudioSource>();
            _feet.playOnAwake = false;
            _feet.spatialBlend = 1f;
            _feet.rolloffMode = AudioRolloffMode.Linear;
            _feet.minDistance = 2f;
            _feet.maxDistance = _stepHearing;
            _feet.dopplerLevel = 0f;
            return _feet;
        }

        /// <summary>노래를 죽이고 살린다. 방에 음악이 없으면 아무 일도 안 한다.</summary>
        private void Music(bool quiet)
        {
            // 노래와 밤소리가 <b>둘</b>이다. 하나만 찾으면 어느 한쪽만 죽었다.
            if (quiet) SceneMusic.DuckAll(_duckMusicTo, 0.5f);
            else SceneMusic.UnduckAll(1.2f);
        }

        /// <summary>조금 들렸다 — 하던 자리에서 소리 난 쪽만 본다.</summary>
        public void OnNoticed(Vector3 at)
        {
            if (_phase != Phase.제자리) return;
            FaceFlat(at);
            if (!string.IsNullOrEmpty(_mutter)) SubtitleView.Show(_speaker, _mutter, "");
        }

        /// <summary>크게 들렸다 — 살피러 온다.</summary>
        public void OnAlarmed(Vector3 at)
        {
            if (_phase != Phase.제자리 || Time.time < _quietUntil) { OnNoticed(at); return; }
            if (_checkSpot == null) { OnNoticed(at); return; }

            _phase = Phase.오는중;
            _leg = 0;
            Watching = true;
            // <b>오고 있다는 것</b>은 지나가는 자막이 아니라 눈에 박혀 있어야 한다.
            // 이 한 줄이 뜬 동안이 곧 요까지 뛰어갈 수 있는 시간이다.
            _route = RouteLeft();
            _aheadValid = false;
            _stepLeft = 0.2f;
            ComingBar();
            Play(_walkState);
            Music(true);          // 노래를 죽인다 — 발소리가 들려야 한다
            _onComing?.Invoke();
        }

        /// <summary>발밑을 다시 짚을 때까지 남은 시간(초). 서 있는 동안에는 자주 볼 것 없다.</summary>
        private float _standCheck;

        private void Update()
        {
            // <b>서 있는 동안에도 바닥을 딛는다.</b>
            //
            // 이 사람은 <b>씬을 가로질러</b> 산다 — 마당이 깔렸을 때와 사랑채 안이
            // 깔렸을 때 발밑에 있는 것이 서로 다른 물건이다. 씬을 갈아 끼우면
            // 딛고 섰던 기단이 통째로 사라지므로, 처음 세워 둔 높이 하나로는
            // 어느 한쪽에서 반드시 <b>공중에 뜨거나 땅에 묻힌다</b>.
            // 걸을 때는 이미 한 걸음마다 짚고 있으니, 서 있을 때만 가끔 짚어 준다.
            if (_phase == Phase.제자리)
            {
                _standCheck -= Time.deltaTime;
                if (_standCheck <= 0f) { _standCheck = 0.5f; StandOnGround(); }
            }

            switch (_phase)
            {
                case Phase.오는중:
                    _barTick -= Time.deltaTime;
                    if (_barTick <= 0f) { _barTick = 0.2f; ComingBar(); }
                    // <b>담을 돌아 온다.</b> 곧게 오면 담을 뚫으므로 경유점을 차례로 밟는다.
                    if (_leg < LegCount)
                    {
                        if (StepTo(LegAt(_leg))) _leg++;
                        break;
                    }
                    if (StepTo(_checkSpot.position))
                    {
                        _phase = Phase.살핌;
                        Play(_idleState);
                        StatusPanel.Set("인기척", 2, HidePlace.Hiding
                            ? "▶ 하인이 문 앞에 섰다 — *가만히 있어라*"
                            : "▶ *하인과 마주쳤다* — 목숨 하나를 잃었다");
                        var cam = Camera.main;
                        if (cam != null) FaceFlat(cam.transform.position);

                        // <b>자고 있으면 아무 일도 없다.</b> 요에 누워 숨을 죽인 것이 통한 것이다 —
                        // 이때만 들킨 셈에서 빠진다. 서서 마주치면 그때 세어진다.
                        if (HidePlace.Hiding)
                        {
                            _until = Time.time + Mathf.Min(_staySeconds, 6f);     // 3.5초는 짧아 곧장 일어나도 들킬 틈이 없었다
                            SayAsleep();
                        }
                        else
                        {
                            _until = Time.time + _staySeconds;
                            // 하인 말이 먼저, 붙들림은 그 뒤 — 여태 Caught 의 첫 마디를 Say 가 곧바로 덮어 안 보였다
                            Say();
                            Caught();
                        }
                    }
                    break;

                case Phase.살핌:
                    if (Time.time >= _until)
                    {
                        _phase = Phase.돌아감;
                        _leg = 0;
                        Play(_walkState);
                    }
                    break;

                case Phase.돌아감:
                    // 왔던 길을 거꾸로 되짚는다.
                    if (_leg < LegCount)
                    {
                        if (StepTo(LegAt(LegCount - 1 - _leg))) _leg++;
                        break;
                    }
                    if (StepTo(_homePos))
                    {
                        _phase = Phase.제자리;
                        Watching = false;
                        StatusPanel.Clear("인기척");
                        Music(false);                 // 노래를 도로 살린다
                        NoiseMeter.CoolDown();        // 다녀갔으니 처음부터 다시 센다
                        _quietUntil = Time.time + _cooldown;
                        transform.rotation = _homeRot;
                        Play(_idleState);
                    }
                    break;
            }
        }

        /// <summary>지금 몇 번째 다리를 걷고 있나.</summary>
        /// <summary>
        /// 문 앞까지 남은 길을 눈금으로 — <b>다가온다는 것이 보이게</b>.
        ///
        /// 여태 「하인이 오고 있다」 한 줄이 떴다가 그대로였다. 창호에 그림자가 비치기는 해도
        /// 얼마나 왔는지는 알 길이 없어, 오는 것이 아니라 <b>와 있는 것</b>으로 느껴졌다.
        /// 발소리가 커지는 만큼 눈금도 찬다.
        /// </summary>
        private void ComingBar()
        {
            float left = RouteLeft();
            float k = _route > 0.01f ? Mathf.Clamp01(1f - left / _route) : 1f;
            int n = Mathf.RoundToInt(k * 10f);
            string bar = new string('■', n) + new string('·', 10 - n);
            StatusPanel.Set("인기척", 2, "▶ *하인이 오고 있다* — 요에 누워 자는 척하라" + NewLine +
                                         "   발소리  " + bar);
        }

        private static readonly string NewLine = ((char)10).ToString();

        /// <summary>지금 자리에서 문 앞까지 남은 길(m). 경유점을 차례로 밟는다.</summary>
        private float RouteLeft()
        {
            if (_checkSpot == null) return 0f;
            float sum = 0f;
            Vector3 at = transform.position;
            for (int i = _leg; i < LegCount; i++)
            {
                Vector3 p = LegAt(i);
                sum += Flat(p - at);
                at = p;
            }
            return sum + Flat(_checkSpot.position - at);
        }

        private static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }

        private void LateUpdate()
        {
            bool walking = _phase == Phase.오는중 || _phase == Phase.돌아감;

            // ① 구워진 걸음을 되민다 — 그리고 되민 거리로 <b>몸이 실제로 나아가는 빠르기</b>를 잰다.
            float ahead = _travel.Cancel();
            if (walking && _animator != null)
            {
                if (_aheadValid && Time.deltaTime > 0.0001f)
                {
                    float d = ahead - _lastAhead;          // 한 바퀴가 돌면 뒤로 튄다 — 그 칸은 버린다
                    if (d > 0.0005f && d < 0.2f)
                    {
                        float at1 = d / Time.deltaTime / Mathf.Max(0.05f, _animator.speed);
                        _natural = _natural <= 0f ? at1 : Mathf.Lerp(_natural, at1, 0.1f);
                    }
                }
                _lastAhead = ahead;
                _aheadValid = true;

                // ② <b>발이 미끄러지지 않게</b> 동작 빠르기를 걸음에 맞춘다. 늙은 걸음(0.85m/초)에
                //    제 빠르기 동작을 그대로 틀면 발은 성큼성큼인데 몸은 느릿느릿 미끄러진다.
                if (_natural > 0.05f)
                    _animator.speed = Mathf.Clamp(_walkSpeed / _natural, 0.55f, 1.4f);

                // ③ 발소리 — 한 바퀴(1.4초)에 두 걸음
                _stepLeft -= Time.deltaTime;
                if (_stepLeft <= 0f)
                {
                    _stepLeft = 0.7f / Mathf.Max(0.3f, _animator.speed);
                    Step();
                }
            }
            else
            {
                _aheadValid = false;
                if (_animator != null && !Mathf.Approximately(_animator.speed, 1f)) _animator.speed = 1f;
            }

            // ④ 등불 — 걸음에 맞춰 흔들리고 불꽃이 떤다. 창호에 비친 빛이 <b>살아 움직여야</b>
            //    그림자 하나가 아니라 사람이 들고 오는 불로 읽힌다.
            if (_lamp != null)
            {
                float t = Time.time;
                float flick = 0.88f + 0.12f * Mathf.PerlinNoise(t * 7f, 0.3f);
                _lamp.intensity = _lampBase * flick;
                float bob = walking ? Mathf.Sin(t * Mathf.PI * 2f / 0.7f) * 0.035f : 0f;
                _lamp.transform.localPosition = new Vector3(0.22f, 0.95f + bob, 0.12f);
            }
        }

        private void Step()
        {
            bool wood = transform.position.y > _woodAbove;
            var set = wood ? _stepsWood : _stepsYard;
            if (set == null || set.Length == 0) set = wood ? _stepsYard : _stepsWood;
            if (set == null || set.Length == 0) return;
            var clip = set[_stepTurn % set.Length];
            _stepTurn++;
            if (clip == null) return;
            var feet = Feet();
            feet.pitch = 0.92f + Random.value * 0.1f;          // 늙은 사람의 무거운 발
            feet.PlayOneShot(clip, _stepVolume);
            if (wood && _creaks != null && _creaks.Length > 0 && Random.value < 0.35f)
            {
                var c = _creaks[Random.Range(0, _creaks.Length)];
                if (c != null) feet.PlayOneShot(c, _stepVolume * 0.8f);
            }
        }

        private int _leg;

        /// <summary>거쳐야 할 자리의 수(빈 칸은 안 센다).</summary>
        private int LegCount
        {
            get
            {
                if (_via == null) return 0;
                int n = 0;
                for (int i = 0; i < _via.Length; i++) if (_via[i] != null) n++;
                return n;
            }
        }

        /// <summary><paramref name="i"/> 번째로 거칠 자리.</summary>
        private Vector3 LegAt(int i)
        {
            int n = 0;
            for (int k = 0; k < _via.Length; k++)
            {
                if (_via[k] == null) continue;
                if (n == i) return _via[k].position;
                n++;
            }
            return _homePos;
        }

        /// <summary>그 자리로 한 걸음. 다 왔으면 참.</summary>
        private bool StepTo(Vector3 target)
        {
            Vector3 flat = target - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude <= _arrive * _arrive) return true;

            Vector3 dir = flat.normalized;
            var want = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, _turnSpeed * Time.deltaTime);
            transform.position += dir * _walkSpeed * Time.deltaTime;

            // 발밑을 딛는다 — 툇마루와 마당의 높이가 다르다
            if (Physics.Raycast(transform.position + Vector3.up * 1.0f, Vector3.down,
                                out RaycastHit floor, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                var p = transform.position;
                p.y = Mathf.MoveTowards(p.y, floor.point.y, 2f * Time.deltaTime);
                transform.position = p;
            }
            return false;
        }

        /// <summary>제자리에서 발밑을 짚어 그 높이로 내려앉는다. 못 짚으면 그대로 둔다.</summary>
        private void StandOnGround()
        {
            RaycastHit floor;
            if (!Physics.Raycast(transform.position + Vector3.up * 1.2f, Vector3.down,
                                 out floor, 4f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (Mathf.Abs(transform.position.y - floor.point.y) < 0.01f) return;
            var p = transform.position;
            p.y = Mathf.MoveTowards(p.y, floor.point.y, 3f * Time.deltaTime);
            transform.position = p;
        }

        private void FaceFlat(Vector3 at)
        {
            Vector3 flat = at - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude < 0.0004f) return;
            transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        /// <summary>서 있는 손님과 마주쳤다 — 그때만 센다.</summary>
        private void Caught()
        {
            Visits++;
            StealthLives.Lose();
            // 목숨(하트)과 붙들림을 한 셈으로 — 여태 하인의 방문 수와 하트 수를 따로 세어, 하트가 남았는데 붙들리거나
            // 하트가 다 떨어졌는데 안 붙들렸다(다시 들어온 판). 하트가 다 떨어지면 붙들린다.
            if (_caughtAt <= 0 || _caught || (StealthLives.Left > 0 && Visits < _caughtAt)) return;
            if (StealthLives.Left > 0) return;
            _caught = true;
            _onCaught?.Invoke();
        }

        private void SayAsleep()
        {
            if (_asleepLines == null || _asleepLines.Length == 0) return;
            SubtitleView.Show(_speaker, _asleepLines[_lineTurn % _asleepLines.Length], "");
            _lineTurn++;
        }

        private void Say()
        {
            if (_lines == null || _lines.Length == 0) return;
            string line = _lines[_lineTurn % _lines.Length];
            _lineTurn++;
            SubtitleView.Show(_speaker, line, "");
        }

        private void Play(string state)
        {
            if (_animator == null || string.IsNullOrEmpty(state)) return;
            if (_animator.HasState(0, Animator.StringToHash(state)))
                _animator.CrossFadeInFixedTime(state, 0.25f);
        }
    }
}
