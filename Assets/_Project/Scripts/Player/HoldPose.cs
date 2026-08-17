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

        private void OnEnable() => Apply();
        private void Start() => Apply();

        /// <summary>다시 그 자세로 세운다(자세가 흐트러졌을 때 외부에서 불러도 된다).</summary>
        public void Apply()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null || string.IsNullOrEmpty(_state)) return;
            if (_animator.runtimeAnimatorController == null) return;

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
