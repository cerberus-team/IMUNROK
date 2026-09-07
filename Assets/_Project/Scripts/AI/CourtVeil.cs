using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>발(簾)</b> — 아녀자를 심문할 때 사이에 내리는 가림.
    ///
    /// 조선의 법정에서 사족(士族)의 부녀는 <b>얼굴을 마주하고 신문하지 않았다</b>.
    /// 사이에 발을 드리우고, 목소리만 오간다. 이것은 예의이기도 하고 <b>보호</b>이기도
    /// 하다 — 관장 앞에 얼굴을 드러내는 것 자체가 흠이 되던 때다.
    ///
    /// 게임으로도 값이 있다. 발이 내려오면 <b>얼굴이 안 보인다</b>. 지금까지 심문은
    /// 얼굴을 읽는 일이었는데(甲 과 乙 을 가르는 것이 그것이다) 이 사람만은 그 길이
    /// 막힌다. 대신 실루엣과 <b>말</b>만 남는다 — 아내를 심문하는 대목이 다른 셋과
    /// 결이 달라야 한다면, 그 다름을 규칙 하나로 만들 수 있다.
    ///
    /// <b>말려 내려온다.</b> 위 끝을 축으로 잡고 길이를 늘인다 — 발은 위에서 풀려
    /// 내려오는 물건이지 통째로 떨어지는 물건이 아니다.
    ///
    /// 붙이는 곳: 발을 매단 자리(들보 밑). <see cref="_blind"/> 에는 늘어날 널을,
    /// <see cref="_who"/> 에는 <b>이 사람이 나오면 내린다</b> 할 사람을 건다.
    /// </summary>
    public class CourtVeil : MonoBehaviour
    {
        [Tooltip("이 사람이 앞으로 나오면 발이 내려온다")]
        [SerializeField] private CourtSummon _who;

        [Tooltip("늘어날 널. 이 오브젝트(매단 자리)의 자식이라야 한다")]
        [SerializeField] private Transform _blind;

        [Tooltip("다 내렸을 때의 길이(m)")]
        [SerializeField] private float _length = 1.84f;

        [Tooltip("발 너비(m)")]
        [SerializeField] private float _width = 3.60f;

        [Tooltip("널 두께(m)")]
        [SerializeField] private float _thick = 0.03f;

        [Tooltip("풀려 내려오는 데 걸리는 시간(초). 손을 놓으면 제 무게로 떨어진다")]
        [SerializeField] private float _dropSeconds = 1.1f;

        [Tooltip("걷어 올리는 데 걸리는 시간(초). <b>내리는 것보다 느려야 한다</b> — " +
                 "내리는 것은 놓는 일이고 올리는 것은 <b>당기는</b> 일이다")]
        [SerializeField] private float _raiseSeconds = 2.0f;

        [Tooltip("다 내려온 뒤 남아 흔들리는 폭(도). 0 이면 안 흔들린다")]
        [SerializeField] private float _swayDegrees = 2.2f;

        [Tooltip("흔들림이 잦아드는 데 걸리는 시간(초)")]
        [SerializeField] private float _swaySeconds = 1.3f;

        [Tooltip("풀리는 소리(대나무 발). 비우면 조용히 내려온다")]
        [SerializeField] private AudioClip _sound;

        private Renderer _skin;
        private float _now = -1f;
        private float _t;            // 0~1, 시간으로 재는 진행
        private bool _falling;       // 지금 내려오는 중인가(소리·흔들림은 내릴 때만)
        private float _swayLeft;
        private bool _rang;

        private void Awake()
        {
            if (_blind != null) _skin = _blind.GetComponent<Renderer>();
            _t = 0f;
            Place(0f);
        }

        private void Update()
        {
            bool want = _who != null && _who.IsUp;

            if (want && _t < 1f)
            {
                if (!_falling) { _falling = true; _rang = false; }
                if (!_rang) { Ring(); _rang = true; }
                _t = Mathf.Min(1f, _t + Time.deltaTime / Mathf.Max(0.05f, _dropSeconds));
                if (_t >= 1f) _swayLeft = _swaySeconds;
            }
            else if (!want && _t > 0f)
            {
                _falling = false; _rang = false; _swayLeft = 0f;
                _t = Mathf.Max(0f, _t - Time.deltaTime / Mathf.Max(0.05f, _raiseSeconds));
            }

            // <b>내려오는 것과 올라가는 것의 결이 다르다.</b>
            //
            // 여태 둘 다 한 빠르기로 오르내렸다. 그러면 <b>기계로 감아 올리는 가림막</b>이지
            // 발이 아니다. 발은 손을 놓으면 제 무게로 떨어지므로 <b>처음이 느리고 끝이
            // 빠르다</b>(t²). 걷어 올릴 때는 사람이 줄을 당기는 것이라 고르게 올라간다.
            float k = _falling ? _t * _t : _t;

            if (!Mathf.Approximately(_now, k) || _swayLeft > 0f) Place(k);
        }

        /// <summary>0 = 다 걷힘, 1 = 다 내려옴.</summary>
        private void Place(float k)
        {
            _now = Mathf.Clamp01(k);
            if (_blind == null) return;

            // 다 걷힌 발은 <b>안 그린다</b>. 두께 0 짜리 널을 그리면 뒤가 얼비쳐
            // 걷었는데도 자국이 남는다.
            bool on = _now > 0.01f;
            if (_skin != null && _skin.enabled != on) _skin.enabled = on;
            if (!on) return;

            float h = _length * _now;
            _blind.localScale = new Vector3(_width, h, _thick);
            _blind.localPosition = new Vector3(0f, -h * 0.5f, 0f);   // 위 끝이 제자리에 남는다

            // <b>다 내려온 뒤에 한 번 남아 흔들린다.</b> 떨어지던 것이 딱 멈추면 널이
            // 아니라 벽이다. 매단 위 끝을 축으로 삼아 좌우로 조금 흔들고 잦아들게 둔다.
            float deg = 0f;
            if (_swayLeft > 0f)
            {
                _swayLeft = Mathf.Max(0f, _swayLeft - Time.deltaTime);
                float u = _swayLeft / Mathf.Max(0.01f, _swaySeconds);       // 1 → 0
                deg = _swayDegrees * u * u * Mathf.Sin(u * Mathf.PI * 6f);
            }
            _blind.localRotation = Quaternion.Euler(0f, 0f, deg);
        }

        /// <summary>풀리는 소리. 걷어 올릴 때는 안 낸다 — 조용히 당겨 올리는 일이다.</summary>
        private void Ring()
        {
            if (_sound == null) return;
            AudioSource.PlayClipAtPoint(_sound, transform.position, 0.8f);
        }

        /// <summary>세우는 도구가 값을 넣어 준다.</summary>
        public void Setup(CourtSummon who, Transform blind, float length, float width, float thick)
        {
            _who = who; _blind = blind; _length = length; _width = width; _thick = thick;
        }
    }
}
