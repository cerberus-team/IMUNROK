using System;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>돌쩌귀를 축으로 도는 여닫이문.</b> 눌러 열고 다시 눌러 닫는다.
    ///
    /// <b>왜 부모를 새로 만들어 그 밑에 넣지 않았나.</b> 문을 돌리는 가장 쉬운 길은
    /// 돌쩌귀 자리에 빈 것을 하나 세우고 문짝을 그 자식으로 넣어 부모를 돌리는 것이다.
    /// 그런데 이 집의 문 여섯 짝은 세트 프리팹 <b>안</b>에 들어 있어서, 그 자식을
    /// 밖으로 빼면 프리팹에 '자리를 옮겼다'는 덧댐이 생겨 두고두고 성가시다.
    /// 그래서 부모를 만드는 대신 <b>이 부품이 조각들의 자리를 직접 놓는다</b> —
    /// 쉴 때의 자리를 기억해 두고, 돌쩌귀 둘레로 돌린 자리를 매기면 그만이다.
    ///
    /// <b>한 벌이 통째로 돈다.</b> 문짝 하나(SM_Door_Sesal)는 그림으로는 두 짝이지만
    /// 그물로는 한 덩이라, 가운데를 갈라 따로 돌릴 수가 없다. 그래서 한 벌을 통째로
    /// 바깥 세로변에서 돌린다 — 사분합문이 두 벌씩 접혀 열리는 것과 같은 꼴이다.
    /// 갈라지지 않으니 벌 한가운데의 맞댐대도 같이 돌면 되어 오히려 편하다.
    ///
    /// <b>이 부품은 문짝 <i>위에</i> 붙인다.</b> 가리키는 광선은 맞은 콜라이더에서
    /// 위로 거슬러 올라가며 <see cref="ISelectable"/> 을 찾는다
    /// (<c>GetComponentInParent</c>). 딴 데 세워 두면 문을 눌러도 안 잡힌다.
    ///
    /// 한 칸에 두 벌이 서면 서로를 <see cref="_together"/> 에 걸어 둔다. 한쪽을 눌러도
    /// 두 벌이 함께 열린다 — 한 짝만 열리는 문은 없다.
    /// </summary>
    public class SwingDoor : MonoBehaviour, IInspectable, ISelectable
    {
        [Tooltip("이 문과 함께 도는 조각들 — 문짝 제 몸, 겉짝, 맞댐대. 비우면 제 몸만 돈다")]
        [SerializeField] private Transform[] _parts;

        [Tooltip("돌쩌귀가 지나는 세로선의 세계 자리. 높이는 안 쓴다")]
        [SerializeField] private Vector3 _hinge;

        [Tooltip("다 열었을 때의 각(도). <b>부호가 도는 쪽</b>을 정한다 — " +
                 "왼짝과 오른짝은 부호가 반대다")]
        [SerializeField] private float _openAngle = 85f;

        [Tooltip("도는 빠르기(초당 도)")]
        [SerializeField] private float _speed = 170f;

        [Tooltip("같은 칸의 다른 벌. 한쪽을 눌러도 함께 열린다")]
        [SerializeField] private SwingDoor[] _together;

        [Tooltip("처음부터 열려 있나")]
        [SerializeField] private bool _openAtStart;

        [Tooltip("가리키면 이만큼(0~1) 들썩인다 — 만질 수 있는 문임을 몸으로 알린다")]
        [Range(0f, 0.2f)] [SerializeField] private float _hoverNudge = 0.045f;

        [Tooltip("이 거리(m) 안에서만 여닫을 수 있다")]
        [SerializeField] private float _maxTouchDistance = 3.0f;

        [SerializeField] private string _title = "문";
        [SerializeField] private string _shutBody = "닫혀 있다.";
        [SerializeField] private string _openBody = "열려 있다.";
        [SerializeField] private string _shutHint = "(눌러 열기)";
        [SerializeField] private string _openHint = "(눌러 닫기)";

        private Vector3[] _restPos;
        private Quaternion[] _restRot;
        private float _now;      // 지금 열린 정도 0~1
        private float _want;     // 가려는 정도
        private bool _hovering;

        /// <summary>열려 있나(가는 중이면 가려는 쪽).</summary>
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (_parts == null || _parts.Length == 0) _parts = new[] { transform };
            _restPos = new Vector3[_parts.Length];
            _restRot = new Quaternion[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] == null) continue;
                _restPos[i] = _parts[i].position;
                _restRot[i] = _parts[i].rotation;
            }
            IsOpen = _openAtStart;
            _want = _now = IsOpen ? 1f : 0f;
            Place(_now);

            if (GetComponent<Collider>() == null)
                Debug.LogWarning($"[{name}] 콜라이더가 없어 눌러도 잡히지 않습니다.", this);
        }

        private void Update()
        {
            float target = _want;
            // 닫힌 채로 가리키면 조금 들썩인다. 여는 중에는 방해하지 않는다.
            if (!IsOpen && _hovering) target = Mathf.Max(target, _hoverNudge);
            if (Mathf.Approximately(_now, target)) return;

            float rate = Mathf.Abs(_openAngle) > 0.01f ? _speed / Mathf.Abs(_openAngle) : 1f;
            _now = Mathf.MoveTowards(_now, target, rate * Time.deltaTime);
            Place(_now);
        }

        /// <summary>열린 정도를 자리에 매긴다.</summary>
        private void Place(float k)
        {
            if (_restPos == null) return;
            var q = Quaternion.AngleAxis(_openAngle * k, Vector3.up);
            for (int i = 0; i < _parts.Length; i++)
            {
                var t = _parts[i];
                if (t == null) continue;
                t.SetPositionAndRotation(_hinge + q * (_restPos[i] - _hinge), q * _restRot[i]);
            }
        }

        /// <summary>여닫는다. <paramref name="spread"/> 가 참이면 같은 칸의 다른 벌에게도 이른다.</summary>
        public void SetOpen(bool open, bool spread)
        {
            IsOpen = open;
            _want = open ? 1f : 0f;
            if (!spread || _together == null) return;
            foreach (var d in _together)
                if (d != null && d != this && d.IsOpen != open) d.SetOpen(open, false);
        }

        // ── 가리키기 ──
        public string GetInspectTitle() => _title;

        public string GetInspectBody()
        {
            string body = IsOpen ? _openBody : _shutBody;
            string hint = IsOpen ? _openHint : _shutHint;
            return string.IsNullOrEmpty(hint) ? body : body + "\n" + hint;
        }

        public void OnInspected() { }

        // ── 누르기 ──
        public void OnHoverEnter() { _hovering = true; }
        public void OnHoverExit() { _hovering = false; }

        public void OnSelect()
        {
            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;
            SetOpen(!IsOpen, true);
        }

        /// <summary>세우는 도구가 값을 넣어 준다.</summary>
        public void Setup(Transform[] parts, Vector3 hinge, float openAngle, SwingDoor[] together)
        {
            _parts = parts; _hinge = hinge; _openAngle = openAngle; _together = together;
        }
    }
}
