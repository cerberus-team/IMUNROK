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
        [Tooltip("잡고 있어야 하는 시간(초). 0이면 예전처럼 한 번 눌러 끝난다 — " +
                 "스쳐 지나가며 누른 것으로 증거가 손에 들어오면 조사한 것이 아니라 주운 것이다")]
        [SerializeField] private float _holdSeconds = 0.8f;
        [Tooltip("손을 대기만 해도 이만큼(0~1) 먼저 움직인다. 만질 수 있는 것임을 몸으로 알린다")]
        [SerializeField] private float _hoverHint = 0.06f;
        [Tooltip("놓았을 때 제자리로 돌아가는 속도 배수")]
        [SerializeField] private float _fallBackSpeed = 2.5f;
        [Tooltip("켜면 잡고 있는 동안 이미 '헤집은 뒤' 모습이 보인다 — 서랍이 열리면서 " +
                 "안에 든 것이 같이 딸려 나와야 하기 때문이다")]
        [SerializeField] private bool _revealWhileHolding = true;

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
        [SerializeField] private string _clueKey = "J13";
        [TextArea(2, 4)]
        [SerializeField] private string _clueText = "[J13] 한여름 밤인데 불을 땐 자리다. 재를 헤집으니 타다 만 서찰 조각이 나온다.";
        [SerializeField] private Texture2D _clueImage;

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
            if (_hinge != null) { _restPos = _hinge.localPosition; _restRot = _hinge.localRotation; }
            ShowState();
            ApplyLift();
        }

        // ───────── 눌러 잡고 있기 ─────────
        //
        // 잡고 있는 만큼 물건이 들린다. 다 들리면 그때 밑에 있던 것이 나온다. 놓으면 도로 덮인다.
        // 진행 막대를 따로 그리지 않는 까닭이 여기 있다 — 들려 올라가는 보료가 곧 진행 막대다.

        private float _hold;            // 0 = 덮인 채, 1 = 다 들림
        private bool _holdingNow;
        private bool _hovering;
        private Vector3 _restPos;
        private Quaternion _restRot;

        public void OnHoldTick(float dt)
        {
            if (Raked || _locked) return;
            _holdingNow = true;
            if (_holdSeconds <= 0.01f) { Rake(); return; }

            _hold = Mathf.Clamp01(_hold + dt / _holdSeconds);
            if (_revealWhileHolding && _hold > 0.02f) ShowState(true);
            ApplyLift();
            if (_hold >= 1f) Rake();
        }

        public void OnHoldRelease() { _holdingNow = false; }

        /// <summary>지금 잡고 있는 만큼을 물건에 반영한다.</summary>
        private void ApplyLift()
        {
            if (_hinge == null) return;
            float k = Raked ? 1f : Mathf.Max(_hold, _hovering && !_locked ? _hoverHint : 0f);
            float e = Mathf.SmoothStep(0f, 1f, k);
            _hinge.localRotation = _restRot * Quaternion.Euler(_liftEuler * e);
            _hinge.localPosition = _restPos + _liftOffset * e;
        }

        private void Update()
        {
            // 놓았으면 도로 내려앉는다
            if (!Raked && !_holdingNow && _hold > 0f)
            {
                _hold = Mathf.MoveTowards(_hold, 0f, _fallBackSpeed * Time.deltaTime / Mathf.Max(0.01f, _holdSeconds));
                if (_revealWhileHolding && _hold <= 0.02f) ShowState();
                ApplyLift();
            }
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
            if (Raked) return _bodyAfter;
            if (_locked) return string.IsNullOrEmpty(_lockedBody) ? _bodyBefore : _lockedBody;
            return string.IsNullOrEmpty(_hint) ? _bodyBefore : _bodyBefore + "\n" + _hint;
        }

        /// <summary>가리킨 것만으로는 아무 일도 없다. 단서는 헤집어야 열린다.</summary>
        public void OnInspected() { }

        // ───────── 헤집기(누르면) ─────────

        public void OnHoverEnter() { _hovering = true; ApplyLift(); }
        public void OnHoverExit() { _hovering = false; ApplyLift(); }

        /// <summary>잡을 시간을 0으로 둔 것만 이리로 온다. 나머지는 잡고 있어야 열린다.</summary>
        public void OnSelect() { if (_holdSeconds <= 0.01f) Rake(); }

        private void Rake()
        {
            if (Raked || _locked) return;
            Raked = true;
            _hold = 1f;

            ShowState();
            ApplyLift();

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
                Journal.Instance.AddClue(_clueCase, _clueKey, _clueText, _clueImage);

            _onRaked?.Invoke();
        }

        /// <summary>덮인 모습과 헤집은 모습을 바꾼다. 잡는 중에는 미리 헤집은 쪽을 보인다.</summary>
        private void ShowState(bool previewAfter = false)
        {
            bool after = Raked || previewAfter;
            if (_before != null) _before.SetActive(!after);
            if (_after != null) _after.SetActive(after);
        }
    }
}
