using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using IMUNROK.Common;

namespace IMUNROK.Onggojip
{
    /// <summary>
    /// <b>2막에서 무엇을 살필 것인가</b> — 어디서 무엇을 하라는 목록(결론은 없다).
    ///
    /// 관아에 들어서면 문서고·동헌·교의가 다 열려 있는데, 무엇을 모아야 사건이 끝나는지
    /// 말해 주는 곳이 없었다. 그래서 문서고의 볕 놀이를 풀어도 <b>무엇이 달라졌는지</b>
    /// 알 길이 없었다(2026-09-13). 사건의 끝은 장계이고, 장계는 넉 줄을 채워야 봉해진다 —
    /// 그러니 그 넉 줄을 <b>어디서 캐는지</b>를 목록으로 둔다.
    ///
    /// <b>결론은 안 적는다.</b> 「甲이 가짜다」가 아니라 「관의 호적 원본을 찾아 두 사람의 몸에서
    /// 확인한다」 — 어디를 보라는 안내이지 무엇이 나온다는 말이 아니다. 캐낸 줄에는 ● 가 붙는다.
    ///
    /// 뜨는 때: <b>H 를 누를 때만</b>. 들어서자마자는 안 뜬다.
    /// 몇 초 머물다 저 혼자 진다 — 늘 떠 있는 안내는 창이 된다.
    ///
    /// <b>저절로는 한 줄만 띄운다</b>(2026-10-17). 여태 칸이 하나 채워질 때마다 목록 열두 줄을
    /// 통째로 띄웠는데, 칸은 대개 심문 중에 채워지므로 「이만 마치겠소」를 누르면 그때마다
    /// 위 안내창에 목록이 주르륵 쏟아졌다. 무엇이 왜 떴는지 알 길이 없었다.
    /// 이제 심문을 파하면 「심문을 마쳤다」 한 줄, 칸이 찼으면 「살필 것 한 칸이 찼다」를 덧붙인다.
    /// </summary>
    public class Act2Brief : MonoBehaviour
    {
        [System.Serializable]
        public class Step
        {
            [Tooltip("할 일 한 줄. 결론이 아니라 어디서 무엇을 하라는 말")]
            public string 할일 = "";
            [Tooltip("이 단서가 수첩에 있으면 ●. 여럿이면 다 있어야 ●")]
            public string[] 단서 = new string[0];
        }

        [System.Serializable]
        public class Line
        {
            [Tooltip("장계의 줄 이름")]
            public string 줄 = "";
            public Step[] 할일들 = new Step[0];
        }

        [SerializeField] private string _title = "살필 것";
        [SerializeField] private Line[] _lines =
        {
            new Line { 줄 = "두 사람 가리기",
                할일들 = new[] {
                    new Step { 할일 = "관의 호적 원본 (문서고)", 단서 = new[] { "G01" } },
                    new Step { 할일 = "두 사람 팔 재기 (동헌)", 단서 = new[] { "G07", "G08" } },
                } },
            new Line { 줄 = "집안 내력",
                할일들 = new[] {
                    new Step { 할일 = "안주인에게 묻기 (동헌)", 단서 = new[] { "G05" } },
                    new Step { 할일 = "두 사람에게 게장 권하기 (동헌)", 단서 = new[] { "G10", "G15" } },
                } },
            new Line { 줄 = "자백",
                할일들 = new[] {
                    new Step { 할일 = "열쇠를 쥐고 甲을 몰아세우기 (동헌)", 단서 = new[] { "G13" } },
                } },
            new Line { 줄 = "갑리",
                할일들 = new[] {
                    new Step { 할일 = "고을의 호소 찾기 (문서고)", 단서 = new[] { "G14" } },
                } },
            new Line { 줄 = "입안대장",
                할일들 = new[] {
                    new Step { 할일 = "고친 줄에 등불 (문서고)", 단서 = new[] { "G11" } },
                    new Step { 할일 = "사라진 장 찾기 (문서고)", 단서 = new[] { "G12" } },
                } },
            new Line { 줄 = "호구단자",
                할일들 = new[] {
                    new Step { 할일 = "관에 올라온 것과 견주기 (문서고)", 단서 = new[] { "G02" } },
                } },
        };

        [SerializeField] private float _holdSeconds = 12f;

        [Header("저절로 뜨는 한 줄")]
        [SerializeField] private string _closedLine = "심문을 마쳤다";
        [SerializeField] private string _filledLine = "살필 것 한 칸이 찼다";
        [SerializeField] private float _noticeSeconds = 3.5f;

        private const string Key = "살필것";   // 서리(GwanaUsher)의 「목표」 줄과 자리를 따로 쓴다
        private const CaseId ThisCase = CaseId.Case1_Onggojip;
        private float _showUntil = -1f;
        private int _lastDone = -1;
        private bool _pending;        // 칸이 새로 찼다 — 창이 걷히면 알린다
        private bool _wasTalking;     // 지난 프레임에 심문이 열려 있었나
        private bool _closedTalk;     // 심문을 막 파했다 — 창이 걷히면 알린다
        private string _notice;       // 저절로 띄우는 한 줄(null 이면 목록)

        private void Update()
        {
            var j = Journal.Instance;
            if (j == null) return;

            bool talking = InterrogationController.AnyOpen;
            if (_wasTalking && !talking) _closedTalk = true;
            _wasTalking = talking;

            bool busy = talking || JournalView.AnyOpen || DocumentView.IsOpen
                     || VerdictReport.IsOpen || HubEnding.Playing;

            int done = CountDone(j);
            if (_lastDone < 0) _lastDone = done;
            if (done > _lastDone) { _lastDone = done; _pending = true; }   // 한 칸이 새로 채워졌다
            // 증좌는 대개 문서를 펴 둔 채 적힌다 — 그 프레임에 띄우면 곧바로 busy 로 지워졌다. 창이 걷히면 띄운다
            if ((_pending || _closedTalk) && !busy)
            {
                string line = _closedTalk ? _closedLine : "";
                if (_pending) line = (line.Length > 0 ? line + "  ·  " : "") + _filledLine;
                _pending = _closedTalk = false;
                Notice(line + "  <size=70%>(H 살필 것)</size>");
            }

            // 들어서자마자는 안 띄운다(2026-09-14) — 무엇을 할지도 모르는데 목록부터 뜨면 어색했다.
            // 처음 한 칸이 채워질 때, 또는 H 를 누를 때 뜬다.

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && !Typing.Now && kb.hKey.wasPressedThisFrame)
            {
                // 한 줄 알림이 떠 있을 때 H 는 목록으로 바꿔 편다
                if (_showUntil > Time.time && _notice == null) { _showUntil = -1f; StatusPanel.Clear(Key); }
                else Show();
            }
#endif
            if (_showUntil < 0f) return;
            if (busy || Time.time >= _showUntil) { _showUntil = -1f; _notice = null; StatusPanel.Clear(Key); return; }
            StatusPanel.Set(Key, 0, _notice ?? Text(j));
        }

        /// <summary>목록을 편다(H).</summary>
        private void Show() { _notice = null; _showUntil = Time.time + _holdSeconds; }

        /// <summary>한 줄만 잠깐 띄운다.</summary>
        private void Notice(string line) { _notice = line; _showUntil = Time.time + _noticeSeconds; }

        private int CountDone(Journal j)
        {
            int n = 0;
            foreach (var line in _lines)
                foreach (var s in line.할일들)
                    if (s.단서.Length > 0 && Has(j, s)) n++;
            return n;
        }

        private static bool Has(Journal j, Step s)
        {
            foreach (var k in s.단서) if (!j.HasClue(ThisCase, k)) return false;
            return true;
        }

        private string Text(Journal j)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>").Append(_title).Append("</b>  <size=70%>(H)</size>");
            foreach (var line in _lines)
            {
                sb.Append('\n').Append("<b>").Append(line.줄).Append("</b>");
                bool firstStep = true;
                foreach (var s in line.할일들)
                {
                    // 줄 이름 옆에 첫 할 일, 둘째부터는 다음 줄에 — 같은 너비만큼 투명하게 비워 칸을 맞춘다.
                    // 할 일 여럿을 한 줄에 이으면 판 너비를 넘겨 중간에서 꺾였다
                    if (!firstStep) sb.Append('\n').Append("<color=#00000000>").Append(line.줄).Append("</color>");
                    firstStep = false;
                    bool ok = s.단서.Length > 0 && Has(j, s);
                    sb.Append(ok ? "    ● <color=#9a8f7a>" : "    ○ ").Append(s.할일).Append(ok ? "</color>" : "");
                }
            }
            return sb.ToString();
        }

        private void OnDisable() { StatusPanel.Clear(Key); }
    }
}
