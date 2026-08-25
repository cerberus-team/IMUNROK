using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>동헌의 부르기 판</b> — 마루에 앉은 채로 뜰의 사람을 갈아 부른다.
    ///
    /// <b>왜 여태 쓰던 방식이 여기서는 안 되나.</b> 1막에서는 말을 걸 사람에게
    /// <b>걸어가서</b> 눌렀다. 그것이 잠행이라 옳았다 — 마당을 가로질러 하인 곁에
    /// 다가서는 그 몇 걸음이 곧 들킬 위험이었으니까.
    ///
    /// 그런데 동헌은 반대다. 어사는 마루 위(y 2.21)에 있고 불려 온 사람은 뜰(y 0.00)에
    /// 있다. <b>둘 사이는 다섯 걸음이고 두 길 높이다</b> — 그것이 이 자리의 뜻이다.
    /// 물으려고 뜰로 내려가 상대 앞에 서면 심문이 아니라 실랑이가 된다. 그래서 여기서는
    /// 몸이 움직이는 것이 아니라 <b>부름이 움직인다</b>.
    ///
    /// <b>어떻게 보이나</b>: 동헌 안에 들어서면 조작판 맨 아랫줄에 <b>사람 이름표</b>가
    /// 뜬다. 아직 아무도 안 불렀으면 그 줄만 뜬다 — 누를 것이 하나뿐이라 헤맬 데가 없다.
    /// 하나를 누르면 그 사람의 심문이 열리고, 다른 이름을 누르면 <b>있던 이가 물러나고</b>
    /// 그 사람이 선다.
    ///
    /// <b>아직 안 온 사람</b>(<see cref="Seat.사람"/> 이 비었으면)도 이름표는 둔다.
    /// 자리가 비어 있는 것과 그런 사람이 없는 것은 다르다 — 아내는 부를 수 있는 사람인데
    /// 아직 안 왔을 뿐이고, 그 사실이 판에 보여야 기다릴 줄을 안다.
    ///
    /// <b>지금은 세워만 둔다.</b> 부르면 걸어 들어오고 물러나면 걸어 나가는 것,
    /// 앉았다 일어서는 동작은 나중에 얹는다. 이 판은 그때도 그대로 쓴다 —
    /// 바뀌는 것은 <see cref="Call"/> 안에서 무엇을 시키느냐뿐이다.
    ///
    /// 붙이는 곳: 동헌의 빈 오브젝트 하나(보통 Zone_2 밑).
    /// </summary>
    public class InterrogationBench : MonoBehaviour
    {
        [System.Serializable]
        public class Seat
        {
            [Tooltip("이름표에 뜨는 말. 짧을수록 좋다 — 판 아랫줄에 넷이 나란히 선다")]
            public string 이름 = "";

            [Tooltip("부를 사람. 비워 두면 '아직 안 왔다'로 뜬다")]
            public InterrogationController 사람;

            [Tooltip("사람이 아직 없을 때 눌렀을 때의 한 마디")]
            [TextArea(2, 3)] public string 아직 = "아직 오지 않았다.";
        }

        /// <summary>씬에 하나. 조작판이 이걸 보고 아랫줄을 그린다.</summary>
        public static InterrogationBench Instance { get; private set; }

        [SerializeField] private Seat[] _seats;

        [Header("어디서 보이나")]
        [Tooltip("이 자리에서 잰다. 비우면 이 오브젝트 자리. 보통 어사가 앉는 마루 위")]
        [SerializeField] private Transform _from;

        [Tooltip("이 거리(m) 안에 있어야 판이 뜬다. 동헌 마루를 넉넉히 덮을 만큼")]
        [SerializeField] private float _radius = 9f;

        [Tooltip("동헌에 들어선 순간 한 마디. 비우면 조용히 뜬다")]
        [TextArea(2, 3)] [SerializeField] private string _enterLine = "";
        [SerializeField] private string _enterSpeaker = "";

        private bool _inside;

        public IReadOnlyList<Seat> Seats => _seats;

        private void OnEnable() { Instance = this; }
        private void OnDisable() { if (Instance == this) Instance = null; }

        private Vector3 Here => _from != null ? _from.position : transform.position;

        /// <summary>
        /// 지금 이 사람이 불려 나와 있나(이름표를 도드라지게 그리려고).
        ///
        /// <b>심문창이 열렸나가 아니라 앞에 나와 있나로 본다.</b> 창을 잠시 닫아도
        /// 사람은 아직 어사 앞에 서 있고, 부르자마자 창이 열리기 전에도 이미 걸어
        /// 나오는 중이다. 이름표가 가리켜야 하는 것은 <b>뜰의 사정</b>이지 창의 사정이 아니다.
        /// </summary>
        public bool IsUp(Seat s)
        {
            if (s == null || s.사람 == null) return false;
            var c = s.사람.GetComponent<CourtSummon>();
            return c != null ? c.IsUp : s.사람.IsOpen;
        }

        /// <summary>
        /// 이름표를 눌렀다.
        ///
        /// 있던 이는 먼저 물러난다. 둘을 나란히 세우는 것(대질)은 따로 할 일이라,
        /// 여기서 어물쩍 둘이 겹치게 두면 나중에 대질이 무엇이 다른지 알 수 없게 된다.
        /// </summary>
        public void Call(Seat s)
        {
            if (s == null) return;
            if (s.사람 == null)
            {
                SubtitleView.Show("", string.IsNullOrEmpty(s.아직) ? "아직 오지 않았다." : s.아직, "", false);
                return;
            }
            if (s.사람.IsOpen) return;      // 이미 저 사람과 이야기하는 중이다

            var open = InterrogationController.Active;
            if (open != null && open != s.사람)
            {
                open.CloseFromUi();
                var back = open.GetComponent<CourtSummon>();
                if (back != null) back.StepBack();     // 있던 이가 등을 보이고 물러난다
            }

            var come = s.사람.GetComponent<CourtSummon>();
            if (come != null) come.StepForward();

            s.사람.CallUp();
        }

        /// <summary>씬을 켤 때 넷을 제 자리에 돌려 놓는다(연출 없이).</summary>
        private void Start()
        {
            if (_seats == null) return;
            foreach (var s in _seats)
            {
                if (s == null || s.사람 == null) continue;
                var c = s.사람.GetComponent<CourtSummon>();
                if (c != null) c.SnapBack();
            }
        }

        /// <summary>
        /// 판을 띄우고 내린다.
        ///
        /// <b>LateUpdate 에서 본다.</b> 심문을 닫는 쪽이 그 프레임에 조작판을 내려 버리므로,
        /// 같은 프레임의 늦은 자리에서 다시 띄워야 사람을 갈아 부를 때 판이 한 번 깜빡이지 않는다.
        /// </summary>
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            bool near = Vector3.Distance(cam.transform.position, Here) <= _radius;
            if (near != _inside)
            {
                _inside = near;
                if (near && !string.IsNullOrEmpty(_enterLine))
                    SubtitleView.Show(_enterSpeaker, _enterLine, "", false);
            }
            // 동헌을 벗어나면 이름표를 <b>내린다</b>. 내리는 쪽이 없으면 판이 따라다녀서,
            // 문서고 서가 앞에서도 甲을 부를 수 있는 것처럼 보인다.
            if (!near) { InterrogationPanel.CloseRoster(); return; }

            // 수첩이나 문서를 펼쳐 놓았으면 판을 겹치지 않는다 — 지금은 종이를 보는 중이다.
            if (JournalView.AnyOpen || DocumentView.IsOpen) { InterrogationPanel.CloseRoster(); return; }

            // 아무도 안 불렀으면 이름표 줄만 띄운다. 누를 것이 하나뿐이라 헤맬 데가 없다.
            if (!InterrogationController.AnyOpen) InterrogationPanel.OpenRoster();
        }
    }
}
