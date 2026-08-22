using System;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 조사청에 앉아 있는 소쩍새 — 무엇을 어떻게 하는지 일러 준다.
    ///
    /// 무엇을 일러 주는가: <b>도구 쓰는 법과 지금 할 차례</b>뿐이다.
    /// 사건의 답은 절대 말하지 않는다. 그래서 이 새는 조사청 밖으로 나가지 않는다 —
    /// 현장에 데려가면 실시간으로 답을 흘리게 되고, 그러면 플레이어가 스스로 찾아낸
    /// 것이 하나도 남지 않는다. 여기 앉아 있는 한 새가 아는 것은 "아직 안 한 일"뿐이다.
    ///
    /// 왜 메뉴가 아니라 새인가: 추리물에는 막힌 사람을 풀어 줄 장치가 반드시 필요한데,
    /// 그걸 메뉴로 만들면 방 안에 있던 사람이 갑자기 화면 앞으로 끌려 나온다.
    /// 새한테 물으면 방 안에 그대로 있게 된다.
    ///
    /// 하는 일 셋:
    ///   · 말을 걸면 — 지금 무엇을 할 차례인지 한마디
    ///   · 도구를 처음 꺼내면 — 그 도구 쓰는 법을 스스로 한마디(도구마다 한 번씩만)
    ///   · 가만히 두면 — 이따금 운다
    ///
    /// 도구는 선반에서 알아서 집는 물건이 아니라 <b>사건마다 지급받는</b> 것이다.
    /// 그래서 이 새가 도구를 두고 하는 말은 "챙기시오"가 아니라 "받은 것을 꺼내 보시오"다.
    /// 지급이 조사청에서 이뤄지는 한, 새로 받은 물건을 처음 손에 쥐는 자리에 늘 이 새가
    /// 있게 된다 — 설명이 필요한 바로 그 순간에.
    ///
    /// 붙이는 법: 조사청의 소쩍새 모델에 붙이고 콜라이더를 하나 준다. 아무것도
    /// 채우지 않아도 기본 대사가 들어 있어 그대로 돌아간다.
    /// </summary>
    public class OwlGuide : MonoBehaviour, ISelectable
    {
        /// <summary>어느 때에 할 말인가. 배열 순서대로 훑어 처음 맞는 것을 말한다.</summary>
        public enum When
        {
            언제나,
            /// <summary>이번에 받은 도구를 아직 한 번도 안 꺼내 본 때.</summary>
            받은_도구를_아직_안_꺼내본_때,
            사건을_아직_안_고른_때,
            사건이_진행중일_때,
            세_사건이_다_끝난_때,
        }

        [Serializable]
        public class StepWord
        {
            public When when = When.언제나;
            [TextArea] public string[] lines;
        }

        [Serializable]
        public class ToolWord
        {
            [Tooltip("ToolDef 의 id — journal · map · lantern · magnify …")]
            public string toolId;
            [TextArea] public string[] lines;
        }

        [Header("이름표")]
        [SerializeField] private string _speaker = "소쩍새";

        [Header("말을 걸면 — 지금 할 차례")]
        [Tooltip("위에서부터 훑어 처음 맞는 것을 말한다. 맨 아래에 '언제나'를 하나 두면 빈말이 안 나온다")]
        [SerializeField]
        private StepWord[] _stepWords =
        {
            new StepWord { when = When.세_사건이_다_끝난_때, lines = new[]
            {
                "창이 밝았소. 봉서함이 열렸으니, 이제 아뢰러 갈 때요.",
            }},
            new StepWord { when = When.받은_도구를_아직_안_꺼내본_때, lines = new[]
            {
                "이번에 받은 것들을 한 번씩 꺼내 보시오. 손에 들어야 아는 법이오.",
                "받은 것이 사건마다 다르오. 지난번에 있던 게 이번엔 없을 수도 있소.",
            }},
            new StepWord { when = When.사건이_진행중일_때, lines = new[]
            {
                "다녀왔으면 기록대에 앉으시오. 먹을 갈고, 붓을 들고.",
                "아직 마음이 안 섰거든 사건판을 다시 보시오.",
            }},
            new StepWord { when = When.사건을_아직_안_고른_때, lines = new[]
            {
                "사건판에 셋이 걸려 있소. 아무거나 먼저 잡으시오 — 차례는 없소.",
            }},
            new StepWord { when = When.언제나, lines = new[]
            {
                "소쩍… 나는 아는 게 없소. 발로 찾으시오.",
            }},
        };

        [Header("도구를 처음 꺼내면 — 쓰는 법")]
        [SerializeField]
        private ToolWord[] _toolWords =
        {
            new ToolWord { toolId = "journal", lines = new[]
            {
                "수첩이오. 주운 물증이 저절로 적히오. I 를 누르면 펴지고, 첫 장에 사건 개요가 있소. " +
                "귀로 들은 말은 여기 안 적히니, 붉게 지나가는 말은 그 자리에서 새겨 두시오.",
            }},
            new ToolWord { toolId = "map",     lines = new[]
            {
                "지도요. M 을 누르면 펴지오. 어디를 아직 안 밟았는지 보일 거요.",
            }},
            new ToolWord { toolId = "lantern", lines = new[]
            {
                "등불이오. 든 채로 어두운 데를 보시오. 안 들면 아무것도 안 보이는 곳이 있소.",
            }},
            new ToolWord { toolId = "magnify", lines = new[]
            {
                "돋보기요. 이걸 들고 봐야 보이는 것이 따로 있소. 글자나 자국 같은 것.",
            }},
        };

        [Header("가만히 두면")]
        [Tooltip("이따금 우는 소리. 비우면 안 운다")]
        [SerializeField] private string _idleCall = "소쩍… 소쩍…";
        [Tooltip("우는 간격(초). 너무 짧으면 새장이 된다")]
        [SerializeField] private float _idleEverySeconds = 55f;
        [Tooltip("이 거리 안에 사람이 있을 때만 운다(m). 0이면 거리를 안 본다")]
        [SerializeField] private float _idleWithin = 5f;

        [Header("고개")]
        [Tooltip("말할 때 사람 쪽으로 도는 부분. 비우면 안 돈다")]
        [SerializeField] private Transform _head;
        [Tooltip("고개가 도는 빠르기")]
        [SerializeField] private float _turnSpeed = 3.5f;

        // 이미 일러 준 도구는 다시 말하지 않는다. 같은 말을 두 번 들으면 잔소리가 된다.
        private readonly System.Collections.Generic.HashSet<string> _toldTools =
            new System.Collections.Generic.HashSet<string>();

        private string _lastToolId = "";
        private bool _everPickedTool;
        private float _idleTimer;
        private int _stepTurn;      // 같은 처지에서 여러 번 물으면 다른 줄이 나오게

        private void Update()
        {
            WatchToolbelt();
            IdleCall();
            TurnHead();
        }

        /// <summary>도구를 처음 꺼내는 순간을 잡아 그 도구 쓰는 법을 일러 준다.</summary>
        private void WatchToolbelt()
        {
            string id = ToolbeltHud.SelectedToolId;
            if (id == _lastToolId) return;
            _lastToolId = id;

            if (string.IsNullOrEmpty(id)) return;      // 맨손으로 돌아온 것
            _everPickedTool = true;

            if (_toldTools.Contains(id)) return;
            var word = FindToolWord(id);
            if (word == null || word.lines == null || word.lines.Length == 0) return;

            _toldTools.Add(id);
            Say(word.lines[0]);
        }

        private ToolWord FindToolWord(string id)
        {
            if (_toolWords == null) return null;
            foreach (var w in _toolWords)
                if (w != null && w.toolId == id) return w;
            return null;
        }

        private void IdleCall()
        {
            if (string.IsNullOrEmpty(_idleCall) || _idleEverySeconds <= 0f) return;

            _idleTimer += Time.deltaTime;
            if (_idleTimer < _idleEverySeconds) return;
            _idleTimer = 0f;

            if (SubtitleView.IsShowing) return;        // 남의 말을 자르지 않는다
            if (!PlayerNear()) return;

            Say(_idleCall);
        }

        private bool PlayerNear()
        {
            if (_idleWithin <= 0f) return true;
            var cam = Camera.main;
            if (cam == null) return false;
            return Vector3.Distance(cam.transform.position, transform.position) <= _idleWithin;
        }

        private void TurnHead()
        {
            if (_head == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 to = cam.transform.position - _head.position;
            to.y = 0f;                                  // 새가 하늘을 보게 두지 않는다
            if (to.sqrMagnitude < 0.0001f) return;

            _head.rotation = Quaternion.Slerp(_head.rotation, Quaternion.LookRotation(to),
                                              Time.deltaTime * Mathf.Max(0.1f, _turnSpeed));
        }

        // ── 말 걸기 ────────────────────────────────────

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public void OnSelect() => Ask();

        /// <summary>지금 무엇을 할 차례인지 한마디. UnityEvent 에 그대로 걸어도 된다.</summary>
        public void Ask()
        {
            var word = PickStepWord();
            if (word == null || word.lines == null || word.lines.Length == 0)
            {
                Say(_idleCall);
                return;
            }

            // 같은 처지에서 또 물으면 다음 줄로 넘어간다. 같은 말만 되풀이하면
            // 새가 고장 난 것처럼 보인다.
            string line = word.lines[_stepTurn % word.lines.Length];
            _stepTurn++;
            Say(line);
        }

        private StepWord PickStepWord()
        {
            if (_stepWords == null) return null;
            foreach (var w in _stepWords)
                if (w != null && Matches(w.when)) return w;
            return null;
        }

        private bool Matches(When when)
        {
            var gs = GameState.Instance;
            switch (when)
            {
                case When.언제나:
                    return true;

                case When.받은_도구를_아직_안_꺼내본_때:
                    // 도구는 선반에서 알아서 집는 물건이 아니라 사건마다 지급받는 것이다.
                    // 그러니 "챙겼는가"가 아니라 "받은 것을 꺼내 봤는가"를 본다.
                    // 지급이 아직 안 됐으면(벨트가 비었으면) 할 말이 없다.
                    var belt = ToolbeltHud.Instance;
                    if (belt == null || belt.Tools == null || belt.Tools.Count == 0) return false;
                    return !_everPickedTool;

                case When.세_사건이_다_끝난_때:
                    return gs.AllCasesCompleted;

                case When.사건이_진행중일_때:
                    foreach (CaseId id in Enum.GetValues(typeof(CaseId)))
                        if (gs.GetStatus(id) == CaseStatus.InProgress) return true;
                    return false;

                case When.사건을_아직_안_고른_때:
                    foreach (CaseId id in Enum.GetValues(typeof(CaseId)))
                        if (gs.GetStatus(id) != CaseStatus.NotStarted) return false;
                    return true;
            }
            return false;
        }

        private void Say(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _idleTimer = 0f;                            // 말한 직후에 또 울면 겹친다
            SubtitleView.Show(_speaker, line);
        }
    }
}
