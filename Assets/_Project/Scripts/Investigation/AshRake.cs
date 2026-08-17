using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// 헤집어야 나오는 단서. 재를 덮어둔 채로는 아무것도 안 보이고,
    /// 한 번 헤집어야 밑에 있던 것이 드러난다.
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
    public class AshRake : MonoBehaviour, IInspectable, ISelectable
    {
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

        /// <summary>이미 헤집었나. 다른 스크립트가 '더 안쪽'을 열 때 조건으로 쓴다.</summary>
        public bool Raked { get; private set; }

        private float _boostLeft;
        private float _emberBase = -1f;

        private void Start() => ShowState();

        private void Update()
        {
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
            => Raked ? _bodyAfter
                     : (string.IsNullOrEmpty(_hint) ? _bodyBefore : _bodyBefore + "\n" + _hint);

        /// <summary>가리킨 것만으로는 아무 일도 없다. 단서는 헤집어야 열린다.</summary>
        public void OnInspected() { }

        // ───────── 헤집기(누르면) ─────────

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public void OnSelect()
        {
            if (Raked) return;
            Raked = true;

            ShowState();

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

        private void ShowState()
        {
            if (_before != null) _before.SetActive(!Raked);
            if (_after != null) _after.SetActive(Raked);
        }
    }
}
