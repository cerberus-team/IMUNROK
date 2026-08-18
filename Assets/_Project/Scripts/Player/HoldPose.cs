using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 인물을 한 동작의 특정 순간에 세워 둔다.
    ///
    /// 왜 필요한가: 이 프로젝트의 캐릭터들은 <b>가만히 선 동작이 없다</b>. 받아온 클립이
    /// 걷기·달리기·발 구르기·머리 감싸쥐기뿐이라, 그중 무엇을 틀어도 몸이 기울거나
    /// 팔을 휘두른다. 그런데 클립 <b>안에는</b> 몸이 곧게 선 순간이 있다 —
    /// 옹덕구는 Angry_Ground_Stomp 의 0.67초에서 수직으로부터 0.5도밖에 안 기운다.
    /// 그 한 순간을 붙잡아 세워 두는 것이 지금 할 수 있는 가장 나은 서있기다.
    ///
    /// 쓰는 법: 애니메이터가 붙은 오브젝트(또는 그 부모)에 붙이고 상태 이름과 시각을 넣는다.
    /// 곁동작을 돌리는 IdleActionCycler 가 같이 붙어 있으면 그쪽이 이걸 덮으므로 꺼야 한다.
    ///
    /// 서있는 클립이 생기면 이 컴포넌트를 떼고 그 클립을 그냥 재생하면 된다.
    /// </summary>
    [ExecuteAlways]
    public class HoldPose : MonoBehaviour
    {
        [Tooltip("비우면 자기 자신이나 자식에서 찾는다")]
        [SerializeField] private Animator _animator;

        [Tooltip("멈춰 세울 동작(상태) 이름")]
        [SerializeField] private string _state = "";

        [Tooltip("그 동작의 몇 초 지점에서 멈출지")]
        [SerializeField] private float _atSeconds = 0f;

        [Tooltip("진짜 서 있는 클립이 있으면 켠다 — 멈춰 세우지 않고 그냥 돌린다. " +
                 "켜면 위의 '몇 초 지점'과 아래 숨쉬기는 쓰이지 않는다(그럴 필요가 없다). " +
                 "가끔 하는 몸짓은 그대로 작동하고, 몸짓이 끝나면 이 클립으로 돌아온다")]
        [SerializeField] private bool _loop = false;

        [Header("숨쉬기 (0이면 정말로 얼어붙는다)")]
        [Tooltip("멈춘 자리 앞뒤로 이만큼(초)을 아주 느리게 왕복한다. 서 있는 클립이 없어 " +
                 "한 프레임에 세워 두는데, 그러면 사람이 아니라 인형이 된다. 대신 그 프레임 " +
                 "주변만 오가면 진짜 동작 데이터로 숨을 쉰다 — 몸이 기울지 않는 폭까지만 준다. " +
                 "옹덕구는 0.63~0.71초 사이가 1.3도 안쪽이라 0.08 이 한계다")]
        [SerializeField] private float _swaySeconds = 0f;
        [Tooltip("한 번 왕복하는 데 걸리는 시간(초). 숨 한 번쯤")]
        [SerializeField] private float _swayPeriod = 5f;

        [Header("가끔 몸짓 (비우면 완전히 멈춰 있는다)")]
        [Tooltip("이따금 한 번씩 통째로 재생할 동작들. 끝나면 다시 위 자세로 돌아온다. " +
                 "제자리에서 하는 동작만 넣을 것 — 걷기를 넣으면 어디론가 가버린다")]
        [SerializeField] private string[] _gestureStates;
        [Tooltip("몸짓 사이 간격(초) 최소~최대")]
        [SerializeField] private Vector2 _gestureEvery = new Vector2(7f, 14f);

        private float _nextGesture;
        private bool _gesturing;

        private void OnEnable() { Apply(); Schedule(); }
        private void Start() { Apply(); Schedule(); }

        private void Schedule()
        {
            _gesturing = false;
            _nextGesture = Time.time + Random.Range(_gestureEvery.x, _gestureEvery.y);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;                    // 에디터에서는 그냥 멈춰 있는다
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            if (_gesturing)
            {
                var st = _animator.GetCurrentAnimatorStateInfo(0);
                if (st.normalizedTime >= 1f) { Apply(); Schedule(); }   // 몸짓 끝 → 다시 선 자세
                return;
            }

            if (!_loop) Breathe();

            if (_gestureStates == null || _gestureStates.Length == 0) return;
            if (Time.time < _nextGesture) return;
            string pick = _gestureStates[Random.Range(0, _gestureStates.Length)];
            if (string.IsNullOrEmpty(pick)) { Schedule(); return; }
            _animator.speed = 1f;
            _animator.Play(pick, 0, 0f);
            _gesturing = true;
        }

        /// <summary>
        /// 멈춰 세운 자리 주변을 아주 느리게 오간다. 클립을 재생하는 것이 아니라
        /// 세워 둔 프레임 앞뒤를 긁는 것이라, 몸이 그 자세를 벗어나지 않는다.
        /// </summary>
        private void Breathe()
        {
            if (_swaySeconds <= 0.0001f || _swayPeriod <= 0.01f) return;

            float len = ClipLength();
            if (len <= 0.01f) return;

            // -1~1 을 오가되 끝에서 잠깐 머무는 모양(사인) — 숨을 들이켜고 내쉬는 결
            float k = Mathf.Sin(Time.time * (2f * Mathf.PI / _swayPeriod));
            float at = Mathf.Clamp(_atSeconds + k * _swaySeconds * 0.5f, 0f, len);

            _animator.speed = 1f;
            _animator.Play(_state, 0, at / len);
            _animator.Update(0f);
            _animator.speed = 0f;
        }

        private float ClipLength()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return 0f;
            foreach (var c in _animator.runtimeAnimatorController.animationClips)
                if (c != null && c.name == _state && c.length > 0.01f) return c.length;
            return 0f;
        }

        /// <summary>다시 그 자세로 세운다(자세가 흐트러졌을 때 외부에서 불러도 된다).</summary>
        public void Apply()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null || string.IsNullOrEmpty(_state)) return;
            if (_animator.runtimeAnimatorController == null) return;

            // 서 있는 클립이 있으면 멈춰 세울 이유가 없다 — 그냥 돌린다.
            if (_loop)
            {
                _animator.speed = 1f;
                _animator.Play(_state, 0, 0f);
                return;
            }

            float len = 1f;
            foreach (var c in _animator.runtimeAnimatorController.animationClips)
                if (c != null && c.name == _state && c.length > 0.01f) { len = c.length; break; }

            _animator.speed = 1f;
            _animator.Play(_state, 0, Mathf.Clamp01(_atSeconds / len));
            _animator.Update(0f);
            _animator.speed = 0f;      // 그 프레임에 멈춘다
        }
    }
}
