using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 특정 문 자리에 두는 발동 영역. 플레이어(카메라)가 이 지점 반경 안으로 들어오면
    /// 마름이 졸던 자리로 돌아가 다시 앉아 졸도록 한 번만 신호를 보낸다.
    ///
    /// 붙이는 법: 빈 GameObject를 '특정 문' 위치에 두고 이 컴포넌트 추가 →
    ///   · _mareum 에 마름_캐릭터(MareumController) 연결
    ///   · _radius 로 발동 범위 조절(씬뷰에서 파란 원으로 보임)
    /// 플레이어가 콜라이더 없는 카메라라서 거리 기반으로 감지한다(_player 비우면 Camera.main).
    /// </summary>
    public class MareumReturnZone : MonoBehaviour
    {
        [SerializeField] private MareumController _mareum;
        [Tooltip("이 반경 안으로 플레이어가 들어오면 발동(m)")]
        [SerializeField] private float _radius = 1.5f;
        [Tooltip("비우면 Camera.main(플레이어 시점)을 사용")]
        [SerializeField] private Transform _player;

        private bool _fired;

        private void Update()
        {
            if (_fired || _mareum == null) return;
            Transform p = _player != null ? _player
                        : (Camera.main != null ? Camera.main.transform : null);
            if (p == null) return;

            Vector3 a = p.position, b = transform.position; a.y = b.y = 0f;
            if ((a - b).sqrMagnitude <= _radius * _radius)
            {
                _fired = true;
                _mareum.ReturnHome();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
