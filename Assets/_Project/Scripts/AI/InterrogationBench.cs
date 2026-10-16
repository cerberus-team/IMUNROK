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
            [Tooltip("이름표에 <b>덮어쓸</b> 말. 비워 두면 인물 자료의 이름을 그대로 쓴다 — " +
                     "그 편이 옳다. 여기 손으로 적어 두면 자막은 「옹덕구」라 부르는데 " +
                     "이름표만 「갓 쓴 자」로 남아, 같은 사람이 화면에서 두 이름을 갖는다")]
            public string 이름 = "";

            [Tooltip("부를 사람. 비워 두면 '아직 안 왔다'로 뜬다")]
            public InterrogationController 사람;

            [Tooltip("사람이 아직 없을 때 눌렀을 때의 한 마디")]
            [TextArea(2, 3)] public string 아직 = "아직 오지 않았다.";

            /// <summary>이름표에 쓸 말. 덮어쓴 것이 없으면 <b>인물이 제 이름을 댄다</b>.</summary>
            public string 부를이름
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(이름)) return 이름.Trim();
                    if (사람 != null && 사람.Character != null &&
                        !string.IsNullOrWhiteSpace(사람.Character.characterName))
                        return 사람.Character.ShownName;
                    return "?";
                }
            }
        }

        /// <summary>씬에 하나. 조작판이 이걸 보고 아랫줄을 그린다.</summary>
        public static InterrogationBench Instance { get; private set; }

        [SerializeField] private Seat[] _seats;

        [Header("어디서 보이나")]
        [Tooltip("이 자리에서 잰다. 비우면 이 오브젝트 자리. 보통 어사가 앉는 마루 위")]
        [SerializeField] private Transform _from;

        [Tooltip("이 거리(m) 안에 있어야 판이 뜬다. 아래 <b>방 크기</b>가 0이 아니면 그쪽이 먼저다")]
        [SerializeField] private float _radius = 9f;

        [Tooltip("<b>이 방 안에서만</b> 부를 수 있다(재는 자리를 한가운데로 삼는 상자, m). " +
                 "0,0,0 이면 위의 거리로 잰다. 동그라미로 재면 벽 너머 판결 자리까지 함께 들어와, " +
                 "「어디서 부를 수 있는가」가 방이 아니라 반지름으로 정해진다 — 심문은 <b>방</b>에서 한다")]
        [SerializeField] private Vector3 _roomSize = Vector3.zero;

        /// <summary>이 자리가 부를 수 있는 데인가. 방을 적어 두었으면 방으로, 아니면 거리로 잰다.</summary>
        private bool Inside(Vector3 p)
        {
            if (_roomSize.sqrMagnitude < 0.01f) return Vector3.Distance(p, Here) <= _radius;
            var d = p - Here;
            return Mathf.Abs(d.x) <= _roomSize.x * 0.5f
                && Mathf.Abs(d.y) <= _roomSize.y * 0.5f
                && Mathf.Abs(d.z) <= _roomSize.z * 0.5f;
        }

        [Tooltip("동헌에 들어선 순간 한 마디. 비우면 조용히 뜬다")]
        [TextArea(2, 3)] [SerializeField] private string _enterLine = "";
        [SerializeField] private string _enterSpeaker = "";

        private bool _inside;

        /// <summary>지금 부르기 판을 펴 둘 것인가. 동헌에 들어선 순간 켜지고, 접으면 꺼진다.</summary>
        private bool _wanted;

        [Tooltip("접어 둔 부르기 판을 다시 펴는 열쇠")]
        [SerializeField] private UnityEngine.InputSystem.Key _openKey = UnityEngine.InputSystem.Key.R;

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
            if (come == null) { _coming = null; s.사람.CallUp(); return; }

            // <b>부른 순간 창부터 열지 않는다.</b>
            //
            // 여태는 이름표를 누르자마자 심문이 열렸고, 열리면서 화각이 좁아졌다
            // (<see cref="TalkFraming"/>). 그런데 그 사람은 아직 뜰에 있다 — 화면은
            // 코앞까지 당겨졌는데 정작 상대는 대여섯 걸음 밖에서 걸어오는 중이라,
            // 「부른다」가 아니라 <b>화면이 먼저 튀는</b> 꼴이 됐다.
            //
            // 부르면 <b>걸어온다</b>. 그 동안 판은 그대로 두고 오는 중이라고만 이른다.
            // 다 와서 서면 그때 입을 연다 — 부르는 절차가 이 막의 뜻인데 그 절차가
            // 한 프레임에 끝나 버리면 둘 사이의 다섯 걸음과 두 길 높이가 다 없던 것이 된다.
            _coming = s.사람;
            come.StepForward();
            SubtitleView.Show("", Josa.IGa(s.부를이름) + " 불려 나온다.", "", false);
            StartCoroutine(OpenWhenArrived(s.사람, come));
        }

        /// <summary>지금 걸어 나오는 중인 사람. 도중에 딴 이를 부르면 이것이 바뀐다.</summary>
        private InterrogationController _coming;

        /// <summary>다 걸어와 섰을 때 비로소 심문을 연다.</summary>
        private System.Collections.IEnumerator OpenWhenArrived(InterrogationController who, CourtSummon walk)
        {
            while (walk != null && walk.Walking)
            {
                // 걸어오는 사이에 딴 이를 불렀으면 이 사람의 차례는 없던 일이 된다.
                if (_coming != who) yield break;
                yield return null;
            }
            if (_coming != who || who == null || walk == null || !walk.IsUp) yield break;
            _coming = null;
            who.CallUp();
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

            // 접었다 폈다 — 글을 치는 중에는 R 이 글자다.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && !Typing.Now && kb[_openKey].wasPressedThisFrame
                && !InterrogationController.AnyOpen && !JournalView.AnyOpen && !DocumentView.IsOpen
                && PlayerSeat.Instance != null && PlayerSeat.Instance.Seated
                && Inside(cam.transform.position))
                _wanted = !_wanted;

            // <b>앉아야 부른다.</b>
            //
            // 부름은 자리에서 나온다. 마루를 서성이며 뜰의 사람을 불러 세우는 어사는 없다 —
            // 앉아야 비로소 그 자리가 <b>묻는 자리</b>가 되고, 일어서면 그것으로 끝난다.
            // 거리만 보던 여태는 판결 자리에서도 뜰에서도 판이 따라다녀, 어디서 무엇을
            // 할 수 있는지가 흐려졌다. <see cref="PlayerSeat"/> 한 줄이 그것을 가른다.
            var seat = PlayerSeat.Instance;
            bool seated = seat != null && seat.Seated;

            bool near = seated && Inside(cam.transform.position);

            // <b>일어서면 그것으로 파한다.</b>
            //
            // 자리에서 일어나 뜰로 내려가도 심문창이 그대로 따라다녔다 — 불려 온 이는
            // 빈 교의를 보고 계속 아뢰고, 어사는 문서고에서 그 말을 듣는 꼴이다.
            // 부름이 <b>자리</b>에서 나오는 것이라면 파하는 것도 자리에서 일어서는 일이어야 한다.
            // <b>앉아 있던 적이 있어야 일어선 것이다.</b> 자리에 앉는 데는 한 숨이 걸리고
            // (<see cref="PlayerSeat"/> 의 앉는 그림), 그 사이에도 여기는 돈다 — 앉기를
            // 기다리지 않고 파하면, 지도에서 「동헌 내부(심문)」로 들어와 사람을 부르는
            // 바로 그 순간에 그 부름이 취소된다.
            if (!near && _inside)
            {
                var open = InterrogationController.Active;
                if (open != null)
                {
                    open.CloseFromUi();
                    var back = open.GetComponent<CourtSummon>();
                    if (back != null) back.StepBack();      // 물러가면서 문도 닫힌다
                }
                // 창을 닫아 둔 채(지도를 펴느라) 떠나도 불려 나온 이는 물러간다 — 여태는
                // 창이 열려 있을 때만 물렸다. 그래서 빈 교의 앞에 甲 이 홀로 서 있었다.
                if (_seats != null)
                    foreach (var s in _seats)
                    {
                        if (s == null || s.사람 == null || !IsUp(s)) continue;
                        var c = s.사람.GetComponent<CourtSummon>();
                        if (c != null) c.StepBack();
                    }
                _coming = null;
            }

            if (near != _inside)
            {
                _inside = near;
                if (near)
                {
                    _wanted = true;      // 앉으면 한 번은 펴 준다 — 무엇을 할 수 있는지 보여야 한다
                    if (!string.IsNullOrEmpty(_enterLine))
                        SubtitleView.Show(_enterSpeaker, _enterLine, "", false);
                }
                if (!near) _wanted = false;   // 동헌을 나서면 부르기는 접힌다
            }
            // 동헌을 벗어나면 이름표를 <b>내린다</b>. 내리는 쪽이 없으면 판이 따라다녀서,
            // 문서고 서가 앞에서도 甲을 부를 수 있는 것처럼 보인다.
            if (!near) { InterrogationPanel.CloseRoster(); return; }

            // 수첩·문서·지도를 펼쳐 놓았으면 판을 겹치지 않는다 — 지금은 그쪽을 보는 중이다.
            //
            // <b>지도가 늦게 붙었다.</b> 앉은 채로 M 을 누르면 지도 그림과 사람 이름표가
            // 화면 한가운데서 포개져, 어느 쪽을 누르라는 것인지가 없어졌다.
            bool mapUp = MapView.AnyOpen
                      || (TravelBoard.Instance != null && TravelBoard.Instance.IsOpen);
            if (JournalView.AnyOpen || DocumentView.IsOpen || mapUp) { InterrogationPanel.CloseRoster(); return; }

            // <b>부를 마음이 있을 때만 편다.</b>
            //
            // 여태는 동헌 안에 있기만 하면 늘 펴 두었다. 그런데 동헌은 부르는 데이기도
            // 하고 <b>판결을 쓰는 데이기도 하다</b> — 장계를 채우는 내내 다섯 얼굴이
            // 화면을 가로질러 있었고, 심문을 닫아도 그 프레임에 다시 펴져서
            // 「눌렀다 하면 안 사라진다」가 됐다. 부름은 <b>짓</b>이지 상태가 아니다.
            //
            // 그래서 한 번 접으면 접힌 채로 두고, 다시 부르고 싶을 때 편다.
            // 여는 열쇠는 아래 <see cref="_openKey"/>(기본 R) 와, 동헌에 갓 들어선 순간이다.
            if (!InterrogationController.AnyOpen)
            {
                if (_wanted) InterrogationPanel.OpenRoster();
                else InterrogationPanel.CloseRoster();
            }
        }
    }
}
