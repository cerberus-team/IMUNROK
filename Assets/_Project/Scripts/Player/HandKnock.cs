using UnityEngine;
using UnityEngine.XR;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>손으로 직접 두드린다.</b>
    ///
    /// 광선으로 짚고 방아쇠를 당겨도 문은 두드려졌다. 다만 그건 <b>두드리는 시늉</b>이지
    /// 두드리는 것이 아니다 — 팔은 가만히 있고 손가락만 까딱했는데 문이 울린다.
    /// 헤드셋을 쓰고 있다면 <b>팔을 뻗어 치는 그 동작</b>이 곧 두드림이라야 한다.
    ///
    /// <b>세 번을 사람이 친다.</b> 전에는 한 번 눌러 놓으면 쿵·쿵·쿵 이 저절로 났다.
    /// 이제는 <b>친 만큼만</b> 난다 — 한 번 치면 한 번 쿵 하고 손이 한 번 울린다.
    /// 세 번 채워야 안에서 사람이 나온다. 두 번 치다 말면 아무도 안 나온다.
    ///
    /// <b>부딪힘으로 잡지 않는다.</b> 손에 리지드바디를 달고 트리거로 받으면, 그 손이
    /// 지나가는 길에 있는 온갖 콜라이더와 말을 섞게 되고 잡는 것·미는 것과도 다툰다.
    /// 대신 <b>손이 가는 쪽으로 짧게 쏘아</b> 본다 — 빠르게 다가가는 중일 때만 쏘므로,
    /// 문 앞에 손을 가만히 대고 있으면 아무 일도 안 난다.
    ///
    /// <b>한 번 친 손은 물러나야 다시 친다.</b> 안 그러면 문에 손을 밀어 넣고 있는 동안
    /// 매 칸 두드린 것이 되어 드르륵 울린다.
    ///
    /// <see cref="VRRig"/> 가 손을 지을 때 같이 붙인다.
    /// </summary>
    public class HandKnock : MonoBehaviour
    {
        [Tooltip("이 빠르기(m/s) 보다 빨리 다가가야 친 것으로 친다")]
        [SerializeField] private float _minSpeed = 0.9f;

        [Tooltip("손끝에서 이만큼(m) 앞까지 닿는다")]
        [SerializeField] private float _reach = 0.16f;

        [Tooltip("한 번 치고 이만큼(m) 물러나야 다시 칠 수 있다")]
        [SerializeField] private float _rearm = 0.10f;

        [Tooltip("어느 손인가 — 친 손이 울려야 한다")]
        [SerializeField] private XRNode _hand = XRNode.RightHand;

        private Vector3 _last;
        private bool _armed = true;
        private float _lastHitTime;

        /// <summary>친 자리. 여기서 <see cref="_rearm"/> 만큼 멀어져야 다시 쳐진다.</summary>
        private Vector3 _hitAt;

        private void OnEnable() { _last = transform.position; }

        private void Update()
        {
            Vector3 now = transform.position;
            Vector3 step = now - _last;
            _last = now;
            if (Time.deltaTime <= 0f) return;

            float speed = step.magnitude / Time.deltaTime;

            // 물러났나 — <b>친 자리에서 그만큼 멀어져야</b> 다시 칠 채비가 선다.
            //
            // 여태 "빠르기 0.25 로 조금이라도 움직이면" 이었다. 그러면 문에 손을 댄 채
            // 손이 떨리기만 해도 채비가 서서, 한 번 두드리려다 <b>대여섯 번 두드려진다</b> —
            // 소리계에 그만큼 쌓이므로 잠행 중에는 그것만으로 사람이 나온다.
            // 물러난 것은 빠르기가 아니라 <b>거리</b>다.
            if (!_armed && Time.time - _lastHitTime > 0.08f
                && (now - _hitAt).sqrMagnitude >= _rearm * _rearm) _armed = true;
            if (!_armed || speed < _minSpeed) return;

            // 손이 가는 쪽으로 쏜다. 손이 보는 쪽이 아니라 <b>가는 쪽</b>이라야
            // 옆으로 후려쳐도 맞는다.
            var dir = step.normalized;
            RaycastHit hit;
            if (!Physics.SphereCast(now - dir * 0.03f, 0.035f, dir, out hit, _reach + 0.03f,
                                    ~0, QueryTriggerInteraction.Ignore)) return;

            var door = hit.collider.GetComponentInParent<DoorController>();
            if (door == null || !door.CanBeKnocked) return;

            door.KnockByHand(_hand);
            _armed = false;
            _hitAt = now;
            _lastHitTime = Time.time;
        }

        /// <summary>세우는 쪽이 값을 조절할 자리.</summary>
        public void Setup(XRNode hand) { _hand = hand; }
    }
}
