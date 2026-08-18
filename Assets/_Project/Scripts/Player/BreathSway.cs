using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 멈춰 세워 둔 인물이 숨은 쉬게 한다. 등뼈만 아주 조금 돌린다.
    ///
    /// 왜 이렇게 하는가: 이 프로젝트의 인물들은 서 있는 클립이 없어, 걷기 클립의 첫
    /// 프레임에 얼려 세워 둔다. 그러면 사람이 아니라 인형이 된다. 옹덕구는 제자리
    /// 동작(발 구르기)이라 그 프레임 <b>앞뒤를 긁어</b> 숨을 쉬게 할 수 있었지만
    /// (HoldPose 참고), 복동·마름은 <b>걷기</b> 클립이라 같은 짓을 하면 다리가 움직여
    /// 발이 바닥에서 미끄러진다.
    ///
    /// 그래서 클립은 건드리지 않고, 애니메이터가 자세를 다 쓴 뒤(LateUpdate)에
    /// <b>등뼈만</b> 살짝 돌린다. 다리와 발은 엉덩이뼈 반대쪽 가지라 아예 움직이지
    /// 않는다 — 발이 미끄러질 일이 없고, 발을 바닥에 붙이는 GroundFeet 과도 다투지 않는다.
    ///
    /// 인물이 실제로 움직이는 동안에는(애니메이터 speed != 0) 손대지 않는다.
    /// 걷거나 문을 열거나 조는 중에는 이미 몸이 살아 있기 때문이다.
    ///
    /// 붙이는 법: 애니메이터가 붙은 오브젝트에 추가. 등뼈는 이름으로 스스로 찾는다.
    /// </summary>
    public class BreathSway : MonoBehaviour
    {
        [Tooltip("비우면 자기 자신이나 자식에서 찾는다")]
        [SerializeField] private Animator _animator;

        [Tooltip("돌릴 뼈. 비우면 Spine 계열을 이름으로 찾는다. 다리·발은 절대 넣지 말 것")]
        [SerializeField] private Transform[] _bones;

        [Tooltip("인물이 보는 방향의 기준. 비우면 부모(마커). 이 오브젝트의 오른쪽을 " +
                 "축으로 앞뒤로 숙였다 폈다 한다")]
        [SerializeField] private Transform _facing;

        [Tooltip("뼈 하나가 앞뒤로 기울 최대 각(도). 등뼈 셋이 겹치므로 실제로는 이 값의 몇 배로 보인다")]
        [SerializeField] private float _bowDegrees = 0.35f;

        [Tooltip("뼈 하나가 좌우로 돌 최대 각(도). 무게중심이 옮겨가는 느낌")]
        [SerializeField] private float _swayDegrees = 0.25f;

        [Tooltip("숨 한 번에 걸리는 시간(초)")]
        [SerializeField] private float _period = 5.5f;

        [Tooltip("좌우 흔들림 주기를 앞뒤와 어긋나게 한다(같은 박자면 기계 같아진다)")]
        [SerializeField] private float _swayPeriodScale = 1.6f;

        [Tooltip("사람마다 숨을 쉬는 때가 다르게. 0~1 중 아무 값")]
        [SerializeField] private float _phase = 0f;

        [Tooltip("보이는지 판단할 렌더러. 비우면 자식에서 찾는다")]
        [SerializeField] private Renderer _renderer;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_facing == null) _facing = transform.parent != null ? transform.parent : transform;
            if (_bones == null || _bones.Length == 0) FindSpine();
            if (_renderer == null) _renderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        /// <summary>등뼈를 이름으로 찾는다. 다리 쪽은 절대 담지 않는다.</summary>
        private void FindSpine()
        {
            var found = new System.Collections.Generic.List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.StartsWith("spine") || n.StartsWith("chest")) found.Add(t);
            }
            _bones = found.ToArray();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (_animator == null || _bones == null || _bones.Length == 0) return;

            // 이미 움직이는 중이면 숨을 얹지 않는다. 얼려 세워 둔 동안만 하는 일이다.
            if (_animator.speed != 0f) return;

            // 화면 밖이면 손대지 않는다. 애니메이터 컬링이 CullUpdateTransforms 라
            // 보이지 않는 동안에는 자세를 다시 써 주지 않는데, 그때도 각도를 더하면
            // 매 프레임 쌓여 몸이 통째로 비틀린 채 화면에 돌아온다.
            if (_renderer != null && !_renderer.isVisible) return;

            float t = Time.time + _phase * _period;
            float bow  = Mathf.Sin(t * (2f * Mathf.PI / Mathf.Max(0.01f, _period)));
            float sway = Mathf.Sin(t * (2f * Mathf.PI / Mathf.Max(0.01f, _period * _swayPeriodScale)));

            Vector3 right = _facing != null ? _facing.right : Vector3.right;
            Vector3 up    = _facing != null ? _facing.up    : Vector3.up;

            // 애니메이터가 매 프레임 자세를 다시 써 주므로, 여기서 더한 각은 쌓이지 않는다.
            foreach (var b in _bones)
            {
                if (b == null) continue;
                b.rotation = Quaternion.AngleAxis(bow * _bowDegrees, right)
                           * Quaternion.AngleAxis(sway * _swayDegrees, up)
                           * b.rotation;
            }
        }
    }
}
