using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 신호를 받으면 정해둔 자리로 옮겨 놓는다.
    ///
    /// 화면이 어두워진 사이에 인물을 다음 장면 자리로 보내는 용도다 — 복동이 중문을
    /// 열어주고, 플레이어가 그 문을 넘는 순간(암전) 그는 이미 사랑방 보료에 앉아 있다.
    /// 실제로 걸어가는 것을 보여줄 수 없는 거리를 이렇게 건너뛴다.
    ///
    /// 붙이는 법: 옮길 인물에 붙이고 _target 에 갈 자리를 연결한 뒤,
    /// TeleportZone 의 _onTeleported 같은 신호에 Go() 를 물린다.
    /// </summary>
    public class SnapTo : MonoBehaviour
    {
        [Tooltip("옮겨 놓을 자리. 이 오브젝트의 정면이 옮긴 뒤 바라볼 방향이 된다")]
        [SerializeField] private Transform _target;

        [Tooltip("바라보는 방향까지 맞출지")]
        [SerializeField] private bool _matchFacing = true;

        [Tooltip("한 번만. 끄면 신호마다 옮긴다")]
        [SerializeField] private bool _once = true;

        private bool _done;

        /// <summary>옮긴다. UnityEvent 에서 부른다.</summary>
        public void Go()
        {
            if (_target == null || (_once && _done)) return;
            _done = true;
            transform.position = _target.position;
            if (_matchFacing) transform.rotation = _target.rotation;
        }

        private void OnDrawGizmosSelected()
        {
            if (_target == null) return;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            Gizmos.DrawLine(transform.position, _target.position);
            Gizmos.DrawWireSphere(_target.position, 0.25f);
            Gizmos.DrawRay(_target.position, _target.forward * 0.6f);
        }
    }
}
