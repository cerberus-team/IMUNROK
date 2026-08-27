using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>장계를 쓰는 자리</b> — 동헌 안, 신문하던 자리 옆의 서안(書案).
    ///
    /// 앉아야 쓴다. 서서 갈겨쓰는 장계는 없고, 무엇보다 <b>앉는다는 것이 곧 조사가
    /// 끝났다는 뜻</b>이다 — 뜰을 내려다보던 자리에서 일어나 책상 앞으로 옮겨 앉는
    /// 그 한 걸음이 이 사건의 마지막 장면을 연다.
    ///
    /// <b>왜 안에 두는가.</b> 신문도 대청 안에서 하고 장계도 대청 안에서 쓴다.
    /// 어사가 고을에 와서 하는 일이 다 한 지붕 아래에서 벌어져야, 나가고 들어오는
    /// 것이 곧 사건의 마디가 된다.
    ///
    /// <b>다 캐기 전에는 안 앉힌다.</b> 반쯤 조사하고 앉으면 빈칸을 채울 말이
    /// 목록에 뜨지도 않아, 플레이어는 제가 무엇을 못 했는지도 모르는 채 헛장계를
    /// 봉하게 된다. <see cref="_needsClues"/> 에 적은 단서를 다 얻어야 앉는다.
    ///
    /// 붙이는 곳: 서안(콜라이더와 함께). 소품은 나중에 갈아 끼워도 이 부품은 그대로다.
    /// </summary>
    public class ReportDesk : MonoBehaviour, IInspectable, ISelectable
    {
        [Tooltip("여기서 쓸 장계")]
        [SerializeField] private CaseReport _form;

        [Tooltip("이 단서를 다 얻어야 앉을 수 있다. 비우면 언제든 앉는다")]
        [SerializeField] private string[] _needsClues = new string[0];

        [Tooltip("이 거리(m) 안에서만 앉을 수 있다")]
        [SerializeField] private float _maxTouchDistance = 2.2f;

        [SerializeField] private string _title = "서안";

        [TextArea(2, 3)] [SerializeField] private string _readyBody =
            "장계를 쓸 자리다. 지필묵이 놓여 있다.";

        [TextArea(2, 3)] [SerializeField] private string _earlyBody =
            "장계를 쓸 자리다. 아직 아뢸 것이 여물지 않았다.";

        [SerializeField] private string _readyHint = "(눌러 앉아 장계를 쓰기)";

        [Tooltip("앉을 자리. 비우면 이 오브젝트 앞에 앉는다")]
        [SerializeField] private Transform _seat;

        private void Awake()
        {
            if (GetComponent<Collider>() == null)
                Debug.LogWarning($"[{name}] 콜라이더가 없어 눌러도 잡히지 않습니다.", this);
        }

        /// <summary>아뢸 것이 여물었나.</summary>
        public bool Ready
        {
            get
            {
                if (_needsClues == null || _needsClues.Length == 0) return true;
                if (Journal.Instance == null || _form == null) return false;
                foreach (var k in _needsClues)
                    if (!string.IsNullOrEmpty(k) && !Journal.Instance.HasClue(_form.사건, k)) return false;
                return true;
            }
        }

        public string GetInspectTitle() => _title;

        public string GetInspectBody()
            => Ready ? (string.IsNullOrEmpty(_readyHint) ? _readyBody : _readyBody + "\n" + _readyHint)
                     : _earlyBody;

        public void OnInspected() { }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public void OnSelect()
        {
            if (VerdictReport.IsOpen) return;
            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;
            if (!Ready) return;
            if (_form == null) { Debug.LogWarning($"[{name}] 쓸 장계가 안 걸려 있습니다.", this); return; }

            // 앉는다. 앉는 일은 이미 있는 부품에 맡긴다 — 신문 자리와 같은 몸짓이라야 한다.
            if (PlayerSeat.Instance != null && _seat != null) PlayerSeat.Instance.SitAt(_seat);

            VerdictReport.Open(_form);
        }

        /// <summary>세우는 도구가 값을 넣어 준다.</summary>
        public void Setup(CaseReport form, Transform seat, string[] needs)
        {
            _form = form; _seat = seat; _needsClues = needs;
        }
    }
}
