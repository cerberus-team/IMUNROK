using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// 헤집거나 들춰야 나오는 단서. 덮인 채로는 아무것도 안 보이고,
    /// 한 번 손을 대야 밑에 있던 것이 드러난다.
    ///
    /// 이름은 아궁이 재에서 왔지만 하는 일은 "감춘 것을 들추기"라 그대로 쓴다 —
    /// 아궁이 재, 甲이 깔고 앉았던 보료, 문갑 서랍이 전부 같은 장치다.
    ///
    /// 가리키면 설명이 뜨고(IInspectable), <b>눌러야</b> 헤집어진다(ISelectable).
    /// 가리키기만 해도 헤집히면 지나가다 눈만 스쳐도 단서가 열려버린다 —
    /// 파헤치는 것은 마음먹고 하는 짓이어야 한다.
    ///
    /// 노트북(비-VR)에서도 그대로 된다: 마우스를 올리면 MouseInspector 가 글을 띄우고,
    /// 클릭하면 MouseRaySelector 가 OnSelect() 를 부른다. VR에서는 손이 같은 자리를 부른다.
    ///
    /// 붙이는 법: 콜라이더 있는 오브젝트에 붙이고 _before / _after 에 각각의 모습을 연결.
    /// 단서는 헤집은 뒤에만 기록된다 — 덮인 재를 본 것으로 서찰을 찾았다 할 수 없다.
    /// </summary>
    public class AshRake : MonoBehaviour, IInspectable, ISelectable, IHoldable
    {
        [Header("손을 대면 실제로 움직이는 것")]
        [Tooltip("들리거나 빠져나오는 것 자체(보료·서랍). 비우면 모습만 갈아 끼운다 — " +
                 "그러면 '들췄다'는 느낌이 안 난다. 눈앞에서 물건이 움직여야 들춘 것이다")]
        [SerializeField] private Transform _hinge;
        [Tooltip("다 들렸을 때의 회전(도). 보료는 한쪽 끝을 잡고 젖히는 것이라 X 로 준다")]
        [SerializeField] private Vector3 _liftEuler;
        [Tooltip("다 빠졌을 때의 이동(m, 제 좌표). 서랍처럼 미끄러져 나오는 것에 쓴다")]
        [SerializeField] private Vector3 _liftOffset;

        [Tooltip("<b>천처럼 휘어 들리는 것</b>(요·이불·보자기). 걸어 두면 경첩을 뻣뻣하게 " +
                 "돌리는 대신 이쪽으로 들어 올린다 — 자리마다 다른 각도로 휘므로 널빤지가 " +
                 "아니라 천으로 보인다. 비우면 예전처럼 통째로 돈다")]
        [SerializeField] private SoftLift _soft;
        [Tooltip("잡고 있어야 하는 시간(초). 0이면 예전처럼 한 번 눌러 끝난다 — " +
                 "스쳐 지나가며 누른 것으로 증거가 손에 들어오면 조사한 것이 아니라 주운 것이다")]
        [SerializeField] private float _holdSeconds = 0.8f;
        [Tooltip("손을 대기만 해도 이만큼(0~1) 먼저 움직인다. 만질 수 있는 것임을 몸으로 알린다")]
        [SerializeField] private float _hoverHint = 0.06f;
        [Tooltip("놓았을 때 제자리로 돌아가는 속도 배수")]
        [SerializeField] private float _fallBackSpeed = 2.5f;
        [Tooltip("이만큼(0~1) 넘게 들어 올려야 걸린다. 그 아래서 손을 놓으면 무게에 못 이겨 " +
                 "도로 떨어진다. 0이면 예전처럼 들리는 대로 들린다")]
        [Range(0f, 0.95f)] [SerializeField] private float _catchAt = 0.62f;

        [Tooltip("켜면 <b>걸리는 데가 없다</b> — 손을 놓으면 언제나 도로 덮인다. " +
                 "보료처럼 솜이 두툼해 놓는 순간 그대로 내려앉는 것에 쓴다. " +
                 "다 들면 밑엣것이 드러나고 수첩에도 적히지만 그래도 놓으면 덮인다 — " +
                 "들여다보는 내내 <b>한 손이 묶여 있다</b>")]
        [SerializeField] private bool _holdToKeep = false;
        [Tooltip("걸리기 전까지 손끝에 느껴지는 무게. 들어 올리는 동안 조금씩 되끌린다")]
        [Range(0f, 0.6f)] [SerializeField] private float _weight = 0.28f;
        [Tooltip("들어 올리는 동안 손끝이 떨리는 크기(0~1). <b>0이 기본이다</b> — " +
                 "무겁다는 것을 떨림으로 알리려 했는데, 초당 열한 번 흔들리니 무거운 것이 " +
                 "아니라 <b>덜컹거리는</b> 것이 되었다. 무게는 이미 느리게 올라가는 것으로 알린다")]
        [Range(0f, 0.3f)] [SerializeField] private float _tremble = 0f;
        [Tooltip("떨리는 빠르기(초당 회). 떨림을 쓸 때만")]
        [SerializeField] private float _trembleHz = 4f;
        [Tooltip("켜면 잡고 있는 동안 이미 '헤집은 뒤' 모습이 보인다 — 서랍이 열리면서 " +
                 "안에 든 것이 같이 딸려 나와야 하기 때문이다")]
        [SerializeField] private bool _revealWhileHolding = true;
        [Tooltip("한 번 들춘 뒤에도 눌러서 도로 내려놓을 수 있다. 들춘 채로 두면 방이 어질러진다")]
        [SerializeField] private bool _canPutBack = true;
        [Tooltip("도로 내려놓고 다시 드는 데 걸리는 시간(초). 손으로 가만히 놓는 만큼")]
        [SerializeField] private float _putBackSeconds = 0.9f;
        [Tooltip("들춘 뒤 제 콜라이더를 <b>경첩 쪽 이 몫만</b> 남기고 줄인다(0~1). " +
                 "보료는 들려 올라가는데 콜라이더는 바닥에 그대로 누워 있어서, 밑에 깔린 " +
                 "별급문기를 영영 가로막았다 — 눈에는 보이는데 눌러지지가 않는다. " +
                 "1이면 안 줄인다")]
        [Range(0.15f, 1f)] [SerializeField] private float _rakedColliderKeep = 0.40f;

        [Tooltip("들춘 뒤 <b>들려 올라간 것 위에</b> 손댈 자리를 하나 둔다. " +
                 "여태 손자리는 바닥에 눌린 채 남아 있어서, 들린 보료를 짚어도 아무 일이 " +
                 "없었다 — '보료가 왜 안 떨어지지' 가 그 말이었다. 들린 것을 짚으면 " +
                 "도로 내려놓는다")]
        [SerializeField] private bool _grabLifted = true;

        [Header("소리")]
        [Tooltip("들추거나 빼는 동안 나는 소리 크기(0~1). 잠행 중에 누가 듣는다(NoiseMeter). " +
                 "서랍은 나무가 긁혀 0.5, 솜 보료는 0.25 남짓, 재 헤집기는 0.2")]
        [Range(0f, 1f)] [SerializeField] private float _noise = 0.35f;
        [Tooltip("들추는 <b>동안 이어지는</b> 소리(서랍 긁힘·보료 스침·재 헤집기). " +
                 "손을 놓으면 멎는다 — 끊기지 않고 이어져야 '내가 지금 소리를 내고 있다'가 된다")]
        [SerializeField] private AudioClip _sound;
        [Tooltip("그 소리가 되풀이되나. 잡는 시간보다 짧은 소리면 켠다")]
        [SerializeField] private bool _soundLoops = true;

        [Header("재 헤집기 — 옆으로 젓는 것")]
        [Tooltip("<b>재는 들리는 것이 아니라 젓는 것이다.</b>\n\n" +
                 "이 장치는 원래 보료·서랍처럼 <b>들어 올리는</b> 것을 위해 만들었다. " +
                 "그것을 아궁이에 그대로 썼더니, 헤집는 짓의 전부가 <b>재가 3cm 가라앉는 것</b> " +
                 "하나였다 — 3cm 는 눈에 안 보인다. 그래서 「재를 헤집는 느낌이 전혀 안 난다」가 됐다.\n\n" +
                 "재를 젓는 손은 <b>옆으로 오간다</b>. 그 왕복 폭(m). 0 이면 안 젓는다 " +
                 "(보료·서랍은 0으로 둔다)")]
        [SerializeField] private float _stirWidth = 0f;
        [Tooltip("젓는 빠르기(초당 왕복). <b>느려야 한다</b> — 예전에 손 떨림을 초당 11번으로 " +
                 "넣었다가 무거운 것이 아니라 덜컹거리는 것이 되어 걷어냈다. 젓는 손은 그보다 훨씬 느리다")]
        [SerializeField] private float _stirHz = 0.85f;
        [Tooltip("헤집는 만큼 재가 눌려 <b>얇아지는</b> 몫(0~1). 0.5 면 절반 두께로 주저앉는다. " +
                 "가라앉기만 하면 재판이 통째로 내려가는 것으로 보이고, 얇아져야 <b>파인다</b>")]
        [Range(0f, 0.9f)] [SerializeField] private float _flatten = 0f;
        [Tooltip("헤집는 <b>내내</b> 먼지가 인다. 끄면 다 헤집은 순간에 한 번만 풀썩한다 — " +
                 "그러면 헤집는 동안에는 아무 일도 안 일어나는 것처럼 보인다")]
        [SerializeField] private bool _dustWhileRaking = false;
        [Tooltip("헤집는 동안 이는 먼지의 양(초당 알). 다 헤집었을 때의 <b>풀썩</b>은 이것과 " +
                 "별개로 그대로 터진다 — 이건 그 앞의 <b>자욱함</b>이다")]
        [SerializeField] private float _dustPerSecond = 26f;

        [Header("두 가지 모습")]
        [Tooltip("헤집기 전 — 고르게 덮인 재")]
        [SerializeField] private GameObject _before;
        [Tooltip("헤집은 뒤 — 흩어진 재와 그 밑에서 나온 것")]
        [SerializeField] private GameObject _after;

        [Header("헤집을 때")]
        [Tooltip("재가 풀썩 이는 먼지. 한 번 터뜨린다")]
        [SerializeField] private ParticleSystem _puff;
        [Tooltip("재를 헤집으면 바람이 들어 되살아나는 것들(잉걸불·연기). 잠깐 세게 튼다")]
        [SerializeField] private ParticleSystem[] _stirUp;
        [Tooltip("잉걸불 빛. 헤집는 동안 잠깐 밝아진다. 없으면 무시")]
        [SerializeField] private Light _ember;
        [SerializeField] private float _emberBoost = 2.2f;
        [SerializeField] private float _boostSeconds = 2.5f;

        [Header("글")]
        [SerializeField] private string _title = "아궁이";
        [TextArea(2, 4)]
        [Tooltip("헤집기 전에 보이는 것. 아직 서찰 얘기를 하면 안 된다")]
        [SerializeField] private string _bodyBefore = "한여름 밤인데 불을 땐 자리다. 재가 아직 식지 않았다.";
        [TextArea(2, 4)]
        [Tooltip("헤집은 뒤에 보이는 것")]
        [SerializeField] private string _bodyAfter = "재를 헤집으니 타다 만 서찰 조각이 나온다.";
        [Tooltip("아직 안 헤집었을 때 덧붙는 한 마디(눌러보라는 신호)")]
        [SerializeField] private string _hint = "(헤집어 본다)";

        [Header("헤집은 뒤 수첩에 기록")]
        [SerializeField] private bool _recordClue = true;
        [SerializeField] private CaseId _clueCase = CaseId.Case1_Onggojip;
        [SerializeField] private string _clueKey = "J10";
        [TextArea(2, 4)]
        [SerializeField] private string _clueText = "[J10] 한여름 밤인데 불을 땐 자리다. 재를 헤집으니 타다 만 서찰 조각이 나온다.";
        [SerializeField] private Texture2D _clueImage;
        [Tooltip("밑에서 나온 것이 종이라면 그 종이 면. 넣어 두면 수첩에서 다시 펼쳐 볼 수 있다")]
        [SerializeField] private Texture2D _cluePage;
        [Tooltip("수첩 카드에 적힐 이름. 비우면 이 장치의 제목을 쓰는데, 그러면 '아궁이'처럼 " +
                 "<b>나온 자리</b>가 물건 이름 자리에 앉는다 — 찾은 것은 아궁이가 아니라 서찰이다")]
        [SerializeField] private string _clueName = "";
        [TextArea(2, 4)]
        [Tooltip("그 종이의 잔글씨 — 돋보기를 대야 읽힌다")]
        [SerializeField] private string _clueFine = "";

        [Tooltip("헤집은 순간 한 번 실행(더 안쪽을 열어주는 등)")]
        [SerializeField] private UnityEvent _onRaked;

        [Header("잠금")]
        [Tooltip("켜면 처음엔 손댈 수 없다. Unlock() 을 부른 뒤부터 열린다 — " +
                 "甲이 자리를 뜬 뒤에야 보료를 들출 수 있게 할 때 쓴다")]
        [SerializeField] private bool _lockedAtStart = false;
        [Tooltip("잠겨 있을 때 가리키면 뜨는 말. 왜 지금은 못 하는지 알려준다")]
        [SerializeField] private string _lockedBody = "지금은 손을 댈 수 없다.";

        /// <summary>이미 헤집었나. 다른 스크립트가 '더 안쪽'을 열 때 조건으로 쓴다.</summary>
        public bool Raked { get; private set; }

        private bool _locked;

        /// <summary>손댈 수 있게 연다(甲이 나간 뒤 등).</summary>
        public void Unlock() { _locked = false; }
        /// <summary>다시 잠근다.</summary>
        public void Lock() { _locked = true; }

        private float _boostLeft;
        private float _emberBase = -1f;

        private void Start()
        {
            _locked = _lockedAtStart;
            if (_hinge != null) { _restPos = _hinge.localPosition; _restRot = _hinge.localRotation; _restScale = _hinge.localScale; _restScaleTaken = true; }

            _box = GetComponent<BoxCollider>();
            if (_box != null) { _boxCenter = _box.center; _boxSize = _box.size; }

            ShowState();
            ApplyLift();
            ApplyColliderShrink();
        }

        private BoxCollider _box;
        private Vector3 _boxCenter, _boxSize;

        /// <summary>
        /// <b>들린 쪽 콜라이더를 거둔다.</b>
        ///
        /// 보료는 경첩을 돌아 들려 올라가는데, 눌러 잡는 콜라이더는 이 오브젝트에 붙어
        /// 있어 <b>바닥에 그대로 누워</b> 있었다. 그 상자가 1.2×0.3×2.6 이라 밑에 깔린
        /// 별급문기(0.22×0.05×0.27)를 통째로 삼킨다 — 보료를 들춰 문서가 눈에 보이는데도
        /// 광선은 늘 보료를 먼저 맞아, 아무리 눌러도 문서가 안 집혔다.
        ///
        /// 경첩 쪽 몫만 남긴다. 경첩 근처는 들려도 거의 안 움직이므로 거기가 곧
        /// "보료를 도로 내려놓는" 손잡이가 되고, 들린 쪽은 밑이 훤히 열린다.
        /// </summary>
        private void ApplyColliderShrink()
        {
            if (_box == null) return;

            bool lifted = Raked && !_lowered && _rakedColliderKeep < 0.999f && _hinge != null;
            if (!lifted) { _box.center = _boxCenter; _box.size = _boxSize; return; }

            // 경첩이 상자 어느 쪽에 있나 — 제 좌표로 옮겨 가장 뚜렷한 축을 고른다.
            Vector3 toHinge = transform.InverseTransformPoint(_hinge.position) - _boxCenter;
            int ax = 0;
            float bestScore = -1f;
            for (int i = 0; i < 3; i++)
            {
                float score = Mathf.Abs(toHinge[i]) / Mathf.Max(0.0001f, _boxSize[i]);
                if (score > bestScore) { bestScore = score; ax = i; }
            }

            float sign = toHinge[ax] >= 0f ? 1f : -1f;
            var size = _boxSize;
            var center = _boxCenter;
            size[ax] = _boxSize[ax] * _rakedColliderKeep;
            center[ax] = _boxCenter[ax] + sign * (_boxSize[ax] - size[ax]) * 0.5f;
            _box.size = size;
            _box.center = center;
        }

        // ───────── 눌러 잡고 있기 ─────────
        //
        // 잡고 있는 만큼 물건이 들린다. 다 들리면 그때 밑에 있던 것이 나온다. 놓으면 도로 덮인다.
        // 진행 막대를 따로 그리지 않는 까닭이 여기 있다 — 들려 올라가는 보료가 곧 진행 막대다.

        private float _hold;            // 0 = 덮인 채, 1 = 다 들림
        private bool _lowered;          // 헤집은 뒤에 도로 내려놓았나
        private bool _holdingNow;
        private bool _caught;           // 걸렸다 — 손을 놓아도 안 떨어진다
        private float _fallSpeed;       // 떨어지는 빠르기(무게가 붙으면 점점 빨라진다)
        private bool _hovering;
        private Vector3 _restPos;
        private Quaternion _restRot;

        public void OnHoldTick(float dt)
        {
            if (Raked || _locked) return;
            _holdingNow = true;
            if (_holdSeconds <= 0.01f) { Rake(); return; }

            // 무게 — 걸리기 전까지는 드는 손과 끌어내리는 무게가 맞선다.
            // 그래서 반쯤 들다 놓으면 도로 덮인다. 보료는 솜이 두툼한 요다.
            float pull = _hold < _catchAt ? _weight : 0f;
            _hold = Mathf.Clamp01(_hold + (1f - pull) * dt / _holdSeconds);
            if (_hold >= _catchAt && !_holdToKeep) _caught = true;

            if (_revealWhileHolding && _hold > 0.02f) ShowState(true);
            ApplyLift();

            // 들추는 <b>동안</b> 계속 난다. 다 들춘 순간에만 한 번 나면, 살살 반쯤 열다
            // 마는 것이 소리 없는 짓이 되어 버린다 — 긁히는 소리는 움직이는 내내 난다.
            if (_noise > 0f && _hold > 0.02f)
            {
                float lv = _noise * Mathf.Clamp01(_hold + 0.3f);
                NoiseMeter.Report(NoiseAt(), lv, _title + " 뒤지는 소리");
                Rasp(lv);
            }

            if (_hold < 1f) return;
            if (_holdToKeep) RevealOnce();      // 드러나되 걸리지는 않는다
            else Rake();
        }

        private AudioSource _rasp;

        /// <summary>
        /// 긁히는 소리를 <b>이어서</b> 낸다. 한 번 트는 것이 아니라 손이 움직이는
        /// 동안 이어지고, 손을 놓으면 멎는다 — 그래야 살살 여는 것이 조용한 짓이 된다.
        /// </summary>
        private void Rasp(float level)
        {
            if (_sound == null) return;
            if (_rasp == null)
            {
                var go = new GameObject("긁는소리");
                go.transform.SetParent(_hinge != null ? _hinge : transform, false);
                _rasp = go.AddComponent<AudioSource>();
                _rasp.playOnAwake = false;
                _rasp.spatialBlend = 1f;
                _rasp.rolloffMode = AudioRolloffMode.Linear;
                _rasp.dopplerLevel = 0f;
                _rasp.minDistance = 1.2f;
                _rasp.maxDistance = 14f;
                _rasp.clip = _sound;
                _rasp.loop = _soundLoops;
            }
            _rasp.volume = level;
            if (!_rasp.isPlaying) _rasp.Play();
            _raspOn = 0.12f;   // 이만큼 안 부르면 손을 놓은 것이다
        }

        private float _raspOn;

        /// <summary>손이 멎으면 소리도 멎는다. 뚝 끊지 않고 잠깐 사이에 잦아든다.</summary>
        private void RaspFade()
        {
            if (_rasp == null || !_rasp.isPlaying) return;
            _raspOn -= Time.deltaTime;
            if (_raspOn > 0f) return;
            _rasp.volume = Mathf.MoveTowards(_rasp.volume, 0f, Time.deltaTime * 4f);
            if (_rasp.volume <= 0.001f) _rasp.Stop();
        }

        /// <summary>소리가 나는 자리. 움직이는 것이 있으면 그쪽에서 난다.</summary>
        private Vector3 NoiseAt()
        {
            if (_hinge != null) return _hinge.position;
            return ModelBounds.TryGet(transform, out var b) ? b.center : transform.position;
        }

        public void OnHoldRelease() { _holdingNow = false; }

        /// <summary>
        /// 다 들춘 뒤에는 <b>잡을 일이 없다</b>. 그래야 누름이 톡 누르기로 흘러가
        /// <see cref="OnSelect"/> 가 불리고, 도로 내려놓을 수 있다.
        /// </summary>
        public bool HoldReady => !Raked && !_locked && _holdSeconds > 0.01f;

        /// <summary>
        /// 움직일 것을 밖에서 물려 준다. 씬을 나눠 놓으면 인스펙터로는 못 잇는다 —
        /// 유니티는 씬을 건너뛰는 참조를 저장하지 못하므로, 실내 씬이 올라온 뒤에
        /// 이름으로 찾아 여기로 넣어 준다.
        /// </summary>
        public void BindHinge(Transform hinge)
        {
            _hinge = hinge;
            if (_hinge != null) { _restPos = _hinge.localPosition; _restRot = _hinge.localRotation; _restScale = _hinge.localScale; _restScaleTaken = true; }
            ApplyLift();
        }

        /// <summary>헤집은 뒤에 드러날 것을 밖에서 물려 준다.</summary>
        public void BindAfter(GameObject after)
        {
            _after = after;
            ShowState();
        }

        /// <summary>지금 잡고 있는 만큼을 물건에 반영한다.</summary>
        private void ApplyLift()
        {
            if (_hinge == null) return;
            float k = Raked ? _hold : Mathf.Max(_hold, _hovering && !_locked ? _hoverHint : 0f);

            // 손끝 떨림 — 기본은 0이다. 켜 두면 오르내리는 길이 톱니처럼 되어
            // "부드럽게 열리지 않는다"는 말이 나온다.
            if (_tremble > 0f && !Raked && !_caught && _holdingNow && _hold > 0.05f)
                k -= _tremble * (1f - _hold) * Mathf.Abs(Mathf.Sin(Time.time * _trembleHz * Mathf.PI * 2f));

            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));

            // 천으로 휘어 들리는 것은 경첩을 돌리지 않는다 — 돌리면 그 위에 또 휘어
            // 두 번 들린다. 휘는 쪽에 맡기고 여기서는 자리만 옮긴다.
            if (_soft != null) _soft.SetLift(e);
            else _hinge.localRotation = _restRot * Quaternion.Euler(_liftEuler * e);
            _hinge.localPosition = _restPos + _liftOffset * e;

            Stir(e);
            Dust(e);
        }

        /// <summary>
        /// <b>재를 옆으로 젓는다.</b>
        ///
        /// 젓는 방향은 <b>경첩이 놓인 자세의 오른쪽</b>이다. 부모 좌표의 x 를 그대로 쓰면
        /// 아궁이를 돌려 놓는 순간 엉뚱한 쪽으로 젓는다.
        ///
        /// 왕복은 손을 대고 있는 동안만 나오는 것이 아니라 <b>진행에 비례</b>한다 —
        /// 막 손을 댔을 때는 살살, 깊이 파고들수록 크게 젓는다. 처음부터 크게 저으면
        /// 손을 대는 순간 재가 벌떡 튀어 놀란다.
        /// </summary>
        private void Stir(float e)
        {
            if (_hinge == null) return;

            // <b>얇아지는 것은 늘 진행에 매단다.</b> 젓는 것과 함께 묶어 두었더니, 다 헤집고
            // 손을 뗀 순간 여기서 곧장 돌아서는 바람에 <b>눌린 두께가 그대로 굳었다</b> —
            // 그 상태에서 도로 덮으면 납작해진 재가 되살아난다.
            if (_flatten > 0.0001f && _restScaleTaken)
            {
                var s = _restScale;
                s.y *= 1f - _flatten * e;
                _hinge.localScale = s;
            }

            if (_stirWidth <= 0.0001f) return;
            if (Raked && !_holdingNow) return;      // 다 헤집은 뒤에는 젓지 않는다

            float swing = Mathf.Sin(Time.time * _stirHz * Mathf.PI * 2f) * _stirWidth * e;
            _hinge.localPosition += (_restRot * Vector3.right) * swing;
        }

        /// <summary>
        /// 헤집는 <b>동안</b> 먼지가 인다. 세기는 진행에 비례한다.
        ///
        /// 여태 먼지는 다 헤집은 순간에 한 번만 터졌다. 그래서 잡고 있는 1초 남짓
        /// 동안에는 화면에서 아무 일도 안 일어났고, 다 되고 나서야 풀썩했다 —
        /// <b>내가 지금 무엇을 하고 있는지</b>가 그 1초 동안 안 보였던 것이다.
        /// </summary>
        private void Dust(float e)
        {
            if (!_dustWhileRaking || _puff == null || Raked) return;
            if (e <= 0.03f) { _dustCarry = 0f; return; }

            // <b>배율을 만지지 않고 직접 뿜는다.</b> 재먼지는 「0.9초 동안 40알을 한 번에」
            // 터뜨리는 <b>버스트</b>로 짜여 있어서 rateOverTime 이 0이다. 거기에 배수를
            // 곱해 봐야 0 × 무엇이라 <b>한 알도 안 나온다</b>. 게다가 Play() 를 부르면
            // 그 버스트 40알이 통째로 터져 「다 헤집었을 때의 풀썩」을 미리 써 버린다.
            // Emit 으로 필요한 만큼만 얹으면 버스트는 마지막 순간을 위해 남는다.
            _dustCarry += _dustPerSecond * Mathf.Clamp01(e) * Time.deltaTime;
            int n = Mathf.FloorToInt(_dustCarry);
            if (n <= 0) return;
            _dustCarry -= n;
            _puff.Emit(n);
        }

        private float _dustCarry;
        private Vector3 _restScale;
        private bool _restScaleTaken;

        /// <summary>들린 것 위에 붙는 손자리. 들춘 뒤에만 켜진다.</summary>
        private BoxCollider _liftedGrab;

        /// <summary>
        /// <b>들린 것을 짚을 수 있게 한다.</b>
        ///
        /// 들춘 뒤 제 손자리는 경첩 쪽으로 줄어든다(<see cref="_rakedColliderKeep"/>) —
        /// 바닥에 누운 콜라이더가 밑에서 나온 것을 가로막지 않게 하려고 그렇게 했다.
        /// 그런데 그 바람에 <b>들려 올라간 물건 자체</b>에는 짚을 데가 없어졌다.
        /// 보료가 비스듬히 서 있는데 아무리 눌러도 반응이 없으니, 내려놓는 법이 있는
        /// 줄도 모르고 "왜 안 떨어지느냐"가 된다.
        ///
        /// 그래서 들린 동안만, 들린 것 위에 손자리를 하나 띄운다. 이 손자리는 <b>이
        /// 오브젝트의 자식</b>이라 짚으면 부모의 이 부품이 잡힌다 — 경첩이 다른 가지에
        /// 달려 있어도 상관없다.
        /// </summary>
        private void FollowLifted()
        {
            if (!_grabLifted || _hinge == null) return;
            bool want = Raked && !_lowered && _hold > 0.35f;

            if (_liftedGrab == null)
            {
                if (!want) return;
                var go = new GameObject("들린것_손자리");
                go.transform.SetParent(transform, false);
                _liftedGrab = go.AddComponent<BoxCollider>();
                _liftedGrab.isTrigger = true;
            }
            if (_liftedGrab.gameObject.activeSelf != want) _liftedGrab.gameObject.SetActive(want);
            if (!want) return;

            // 들린 것이 지금 어디 있나 — 눈에 보이는 몸피에 맞춘다. 다만 <b>윗면은 비운다</b>:
            // 들린 보료 위에 깔려 있던 종이가 나와 있는데, 손자리가 그 위까지 덮으면
            // 종이를 짚으려다 보료가 잡혀 도로 내려놓게 된다.
            if (!ModelBounds.TryGet(_hinge, out var b)) return;
            float keepY = Mathf.Max(0.05f, b.size.y * 0.55f);
            _liftedGrab.transform.position = new Vector3(b.center.x, b.center.y - b.size.y * 0.22f, b.center.z);
            Vector3 lossy = _liftedGrab.transform.lossyScale;
            _liftedGrab.size = new Vector3(
                Mathf.Abs(lossy.x) > 1e-4f ? b.size.x * 0.9f / Mathf.Abs(lossy.x) : b.size.x,
                Mathf.Abs(lossy.y) > 1e-4f ? keepY / Mathf.Abs(lossy.y) : keepY,
                Mathf.Abs(lossy.z) > 1e-4f ? b.size.z * 0.9f / Mathf.Abs(lossy.z) : b.size.z);
        }

        private void Update()
        {
            RaspFade();
            FollowLifted();

            // 헤집은 뒤 — 눌러서 도로 내려놓고, 다시 눌러서 들춘다. 뚝 떨어지지 않고
            // 손으로 가만히 놓는 만큼의 시간을 들여 오르내린다.
            if (Raked)
            {
                float want = _lowered ? 0f : 1f;
                if (!Mathf.Approximately(_hold, want))
                {
                    _hold = Mathf.MoveTowards(_hold, want, Time.deltaTime / Mathf.Max(0.05f, _putBackSeconds));
                    ShowState(_hold > 0.5f);
                    ApplyLift();
                }
                _holdingNow = false;
                return;
            }

            if (!Raked && _caught && !_holdingNow && _hold < 1f)
            {
                // 한 번 걸린 뒤로는 손을 떼도 마저 넘어간다 — 문지방을 넘은 것이다
                _hold = Mathf.MoveTowards(_hold, 1f, Time.deltaTime / Mathf.Max(0.01f, _holdSeconds));
                if (_revealWhileHolding) ShowState(true);
                ApplyLift();
                if (_hold >= 1f) Rake();
            }
            // 덜 들고 놓았으면 무게에 못 이겨 도로 내려앉는다(떨어질수록 빨라진다)
            else if (!Raked && !_holdingNow && _hold > 0f)
            {
                _fallSpeed += 2.2f * Time.deltaTime;
                _hold = Mathf.MoveTowards(_hold, 0f,
                        (_fallBackSpeed + _fallSpeed) * Time.deltaTime / Mathf.Max(0.01f, _holdSeconds));
                if (_revealWhileHolding && _hold <= 0.02f) ShowState();
                ApplyLift();
            }
            if (_holdingNow) _fallSpeed = 0f;
            _holdingNow = false;      // 잡고 있으면 다음 프레임에 다시 켜진다

            if (_boostLeft <= 0f) return;
            _boostLeft -= Time.deltaTime;
            if (_ember != null && _emberBase >= 0f)
            {
                // 헤집어 바람이 든 잉걸이 서서히 도로 사그라든다
                float k = Mathf.Clamp01(_boostLeft / _boostSeconds);
                _ember.intensity = Mathf.Lerp(_emberBase, _emberBase * _emberBoost, k);
            }
        }

        // ───────── 살펴보기(가리키면) ─────────

        public string GetInspectTitle() => _title;

        public string GetInspectBody()
        {
            // 들춘 뒤에는 <b>도로 내려놓을 수 있다는 것</b>을 알려야 한다. 여태 아무 말이
            // 없어서, 들린 보료를 어떻게 내리는지 알 길이 없었다.
            if (Raked)
                return _canPutBack
                    ? _bodyAfter + "\n" + (_lowered ? "(눌러 다시 들추기)" : "(눌러 도로 내려놓기)")
                    : _bodyAfter;
            if (_locked) return string.IsNullOrEmpty(_lockedBody) ? _bodyBefore : _lockedBody;

            // <b>잡고 있어야 들리는 것</b>은 잡고 있는 동안에도 말이 달라야 한다.
            // 이미 무엇이 있는지 봤는데 "들춰 보시오"가 계속 뜨면, 놓으면 덮인다는
            // 것을 모른 채 손만 놓게 된다.
            if (_holdToKeep && _hold > 0.5f)
                return _bodyAfter + "\n" + "(손을 놓으면 *도로 덮인다*)";

            return string.IsNullOrEmpty(_hint) ? _bodyBefore : _bodyBefore + "\n" + _hint;
        }

        /// <summary>가리킨 것만으로는 아무 일도 없다. 단서는 헤집어야 열린다.</summary>
        public void OnInspected() { }

        // ───────── 헤집기(누르면) ─────────

        public void OnHoverEnter() { _hovering = true; ApplyLift(); }
        public void OnHoverExit() { _hovering = false; ApplyLift(); }

        /// <summary>
        /// 눌렀을 때. 아직 안 헤집었으면 — 잡을 시간을 0으로 둔 것만 여기서 열린다.
        /// 이미 헤집은 뒤라면 <b>도로 내려놓거나 다시 들춘다</b>.
        ///
        /// 한 번 들춘 것이 영영 들린 채로 있으면 방이 어질러진 채로 남는다. 보료를 들추고
        /// 밑을 본 다음에는 도로 덮어 두는 것이 사람이 하는 일이다. 덮는다고 본 것이
        /// 없던 일이 되지는 않으므로, 수첩에 적힌 단서는 그대로 둔다.
        /// </summary>
        public void OnSelect()
        {
            if (!Raked) { if (_holdSeconds <= 0.01f) Rake(); return; }
            if (!_canPutBack) return;
            _lowered = !_lowered;
            ApplyColliderShrink();
        }

        /// <summary>
        /// <b>드러나되 걸리지는 않는다</b> — 다 들어 올린 그 순간 밑엣것이 보이고
        /// 수첩에 적히지만, 물건은 여전히 손에 매달려 있다. 놓으면 도로 덮인다.
        ///
        /// <b>왜 이렇게 두나</b>: 보료는 걷어 놓는 물건이 아니다. 솜이 두툼한 요를
        /// 한쪽으로 젖혀 놓으면 그대로 서 있지 않고 제 무게로 도로 내려앉는다.
        /// 그리고 그 편이 놀이로도 낫다 — 밑엣것을 보는 <b>내내 한 손이 묶여</b>
        /// 있으므로, 밖에서 발소리가 나면 놓고 일어설지 조금 더 볼지를 고르게 된다.
        /// 한 번 젖혀 두면 그 뒤로는 아무 값도 치르지 않는다.
        ///
        /// <see cref="Rake"/> 와 달리 <see cref="Raked"/> 를 세우지 않는다. 그래서
        /// 콜라이더도 안 줄고, 들린 것 손자리도 안 생기고, 도로 내려놓을 일도 없다 —
        /// 손만 놓으면 저절로 덮이기 때문이다.
        /// </summary>
        private bool _revealed;

        private void RevealOnce()
        {
            if (_revealed || _locked) return;
            _revealed = true;

            if (_puff != null) { _puff.Clear(true); _puff.Play(true); }
            foreach (var ps in _stirUp)
            {
                if (ps == null) continue;
                if (!ps.isPlaying) ps.Play(true);
                ps.Emit(12);
            }

            if (_recordClue && !string.IsNullOrEmpty(_clueKey) && Journal.Instance != null)
            {
                Journal.Instance.AddClue(_clueCase, _clueKey, _clueText, _clueImage);
                if (_cluePage != null)
                    Journal.Instance.AttachDocument(_clueCase, _clueKey, _cluePage,
                        string.IsNullOrEmpty(_clueName) ? _title : _clueName, _bodyAfter, _clueFine);
            }
            _onRaked?.Invoke();
        }

        private void Rake()
        {
            if (Raked || _locked) return;
            Raked = true;
            _hold = 1f;

            ShowState();
            ApplyLift();
            ApplyColliderShrink();

            if (_puff != null) { _puff.Clear(true); _puff.Play(true); }
            foreach (var ps in _stirUp)
            {
                if (ps == null) continue;
                if (!ps.isPlaying) ps.Play(true);
                ps.Emit(12);   // 헤집는 순간 왈칵
            }
            if (_ember != null)
            {
                if (_emberBase < 0f) _emberBase = _ember.intensity;
                _boostLeft = _boostSeconds;
            }

            if (_recordClue && !string.IsNullOrEmpty(_clueKey) && Journal.Instance != null)
            {
                Journal.Instance.AddClue(_clueCase, _clueKey, _clueText, _clueImage);
                // 밑에서 나온 것이 종이라면 수첩에서 다시 펼쳐 볼 수 있게 함께 적어 둔다
                if (_cluePage != null)
                    Journal.Instance.AttachDocument(_clueCase, _clueKey, _cluePage,
                        string.IsNullOrEmpty(_clueName) ? _title : _clueName, _bodyAfter, _clueFine);
            }

            _onRaked?.Invoke();
        }

        /// <summary>덮인 모습과 헤집은 모습을 바꾼다. 잡는 중에는 미리 헤집은 쪽을 보인다.</summary>
        private void ShowState(bool previewAfter = false)
        {
            bool after = Raked || previewAfter;
            if (_before != null) _before.SetActive(!after);
            if (_after != null) _after.SetActive(after);
            if (Raked) YieldTo(_after);
        }

        [Tooltip("다 헤집고 나면 <b>이쪽 콜라이더를 물린다</b>. 나온 물건이 이 콜라이더 " +
                 "속에 들어앉아 있으면 광선이 늘 이쪽을 먼저 맞아, 나온 것을 집을 수가 없다")]
        [SerializeField] private bool _yieldWhenDone = true;

        /// <summary>
        /// <b>다 헤집었으면 자리를 내준다.</b>
        ///
        /// 서고의 문서궤에서 드러난 탈이다. 궤를 헤집으면 대장이 나오는데 <b>집을 수가
        /// 없었다</b>. 까닭은 종이도 단서도 아니고 <b>콜라이더</b>였다 — 궤의 상자가
        /// 24cm 높이로 궤 <b>속까지</b> 덮고 있어서, 그 안에 놓인 대장(6cm)을 통째로
        /// 품는다. 광선은 늘 바깥 상자를 먼저 맞고, 거슬러 올라가 찾는
        /// <see cref="ISelectable"/> 은 이 부품이다. 그래서 대장을 눌러도 "궤를 헤집는다"만
        /// 되풀이됐다 — 무슨 증거인지 알 길이 없었던 것이 이것이다.
        ///
        /// 다 헤집은 뒤에는 이 부품이 할 일이 없다. 자리를 내주면 광선이 안의 것을 맞는다.
        /// 나온 것에 제 콜라이더가 <b>없으면</b> 물리지 않는다 — 그러면 아무것도 못 짚는
        /// 자리가 되어 버린다.
        /// </summary>
        private void YieldTo(GameObject after)
        {
            if (!_yieldWhenDone || _yielded) return;
            if (after == null || after.GetComponentInChildren<Collider>(true) == null) return;

            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            _yielded = true;
        }

        private bool _yielded;
    }
}
