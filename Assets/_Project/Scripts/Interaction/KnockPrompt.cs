using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 잠긴 문 앞에 다가서면 "두들겨 보자"고 일러 준다.
    ///
    /// 왜 필요한가: 대문은 클릭해야 두드려지는데, 그걸 아무도 알려주지 않으면
    /// 플레이어는 닫힌 문 앞에서 서성이다 만다. 문이 열리는 연출은 두드림에서
    /// 시작하므로, 그 한 걸음을 놓치면 1막이 시작조차 되지 않는다.
    ///
    /// 붙이는 법: 문(DoorController) 이 붙은 오브젝트나 문간 빈 오브젝트에 추가.
    ///   · _door 를 연결(비우면 같은 오브젝트에서 찾는다)
    ///   · _at 에 문간 자리를 넣으면 그 자리를 기준으로 거리를 잰다(문 피벗이 엉뚱한 데 있을 때)
    ///   · 문의 OnKnock 에 이 컴포넌트의 Done() 을 걸면, 한 번 두드린 뒤로는 안 뜬다
    /// </summary>
    public class KnockPrompt : MonoBehaviour
    {
        [Tooltip("비우면 같은 오브젝트에서 찾는다")]
        [SerializeField] private DoorController _door;

        [Tooltip("거리를 잴 기준 자리. 비우면 이 오브젝트. 문 피벗이 원점에 있는 경우가 많아 " +
                 "문간에 빈 오브젝트를 두고 그걸 넣는 편이 정확하다")]
        [SerializeField] private Transform _at;

        [Tooltip("이 거리(m) 안에 들어오면 알려 준다")]
        [SerializeField] private float _radius = 3.5f;

        [Tooltip("문이 이미 열려 있으면 알리지 않는다")]
        [SerializeField] private bool _onlyWhileClosed = true;

        [SerializeField] private string _text = "굳게 닫힌 대문이다. 두들겨 보자.";
        [SerializeField] private string _hint = "(문을 눌러 두드리기)";

        private bool _shown;
        private bool _done;

        private void Awake()
        {
            if (_door == null) _door = GetComponent<DoorController>();
            if (_at == null) _at = transform;
        }

        /// <summary>두드렸다 — 이제 이 안내는 할 일이 끝났다. 문의 OnKnock 에 건다.</summary>
        public void Done()
        {
            _done = true;
            if (_shown) { SubtitleView.Hide(); _shown = false; }
        }

        private void Update()
        {
            if (_done) return;

            var cam = Camera.main;
            if (cam == null) return;

            bool blocked = InterrogationController.AnyOpen                       // 말하는 중엔 끼어들지 않는다
                        || (_onlyWhileClosed && _door != null && _door.IsOpen);

            Vector3 a = cam.transform.position, b = _at.position;
            a.y = b.y = 0f;
            bool near = !blocked && (a - b).sqrMagnitude <= _radius * _radius;

            if (near && !_shown) { SubtitleView.Show("", _text, _hint); _shown = true; }
            else if (!near && _shown) { SubtitleView.Hide(); _shown = false; }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere((_at != null ? _at : transform).position, _radius);
        }
    }
}
