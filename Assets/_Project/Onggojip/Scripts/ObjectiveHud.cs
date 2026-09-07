using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using IMUNROK.Common;

namespace IMUNROK.Onggojip
{
    /// <summary>
    /// 1막 목표 안내 HUD — 화면 상단에 "지금 뭘 할지"를 보여준다(길 잃음 방지).
    /// 진행에 따라 문구가 바뀐다:
    ///   · 필수 단서(OnggojipClues.RequiredForReveal) 부족 → "집 안을 조사하라 (필수 N/4)"
    ///   · 필수 단서 다 모음         → "품속의 마패를 내보여라 (F 꾹 누르기)"
    /// <b>붙박아 두지 않는다.</b> 여태 이 줄은 화면에 <b>계속</b> 떠 있었다. 그러면
    /// 안내가 아니라 <b>창</b>이 된다 — 늘 있는 것은 안 읽히고, 안 읽히는데 자리는 차지한다.
    /// 그래서 <b>말이 바뀔 때만</b> 떠서 몇 초 머물다 스르르 진다.
    /// 다시 보고 싶으면 <b>H</b> 를 누른다 — 끄는 스위치가 아니라 <b>다시 보기</b>다.
    ///
    /// 표시는 월드 공간 알림판(WorldNotice)이 맡는다.
    /// </summary>
    public class ObjectiveHud : MonoBehaviour
    {
        [Tooltip("한 번 뜨면 이만큼(초) 머물다 스르르 진다. 0 이면 안 사라진다")]
        [SerializeField] private float _holdSeconds = 6f;
        [TextArea] [SerializeField] private string _introLine = "마을 어귀에 닿았다. 수첩(I)을 살피거나, 지나는 이에게 말을 걸어보자";
        [TextArea] [SerializeField] private string _investigateLine = "밤이다. 몰래 집 안을 조사하라";
        [TextArea] [SerializeField] private string _readyLine = "증거를 다 모았다 — 품속의 마패를 내보여라  (F 꾹 누르기)";

        private const CaseId ThisCase = CaseId.Case1_Onggojip;

        private bool _inside;

        /// <summary>
        /// 집 안으로 들어섰다는 신호. 중간대문 TeleportZone 의 _onTeleported 에 물리면 된다.
        /// 이 뒤로는 "지나는 이에게 말을 걸어보자"가 뜨지 않는다 — 이미 지나쳐 온 단계다.
        /// </summary>
        public void EnterInside() => _inside = true;

        /// <summary>언제까지 띄워 둘 것인가. 지나면 스스로 진다.</summary>
        private float _until = -1f;

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // <b>H 는 다시 보기다.</b> 끄는 스위치가 아니다 — 어차피 저 혼자 진다.
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                _lastMain = null;              // 같은 말이라도 다시 뜨게 한다
#endif
            if (_until > 0f && Time.time >= _until)
            {
                _until = 0f;
                StatusPanel.Clear("목표");
            }
        }

        // 목표 안내는 월드 공간 알림판으로 띄운다.
        // 매 프레임 문자열을 새로 만들 필요가 없어 값이 바뀔 때만 갱신한다.
        private string _lastMain, _lastSub;

        private void LateUpdate()
        {
            bool hide = InterrogationController.AnyOpen || JournalView.AnyOpen;
            var journal = Journal.Instance;
            if (hide || journal == null)
            {
                if (_lastMain != null) { StatusPanel.Clear("목표"); _lastMain = null; _lastSub = null; }
                return;
            }

            // 필수 단서 진행도
            int req = 0, have = 0;
            foreach (var k in OnggojipClues.RequiredForReveal)
            {
                req++;
                if (journal.HasClue(ThisCase, k)) have++;
            }
            // 발견한 잠행 단서 수
            int found = 0, total = 0;
            foreach (var c in OnggojipClues.All)
            {
                if (!c.stealth) continue;
                total++;
                if (journal.HasClue(ThisCase, c.key)) found++;
            }

            bool ready = have >= req;
            // 도입 문구("지나는 이에게 말을 걸어보자")는 마을 어귀에서만 뜻이 있다.
            // 아무에게도 안 묻고 담을 넘어 들어와 버렸다면 단서 수는 0이어도 이미 도입이 아니다.
            bool intro = found == 0 && !_inside;
            string main = ready ? _readyLine : (intro ? _introLine : _investigateLine);
            string sub = ready
                ? "필수 단서 완료"
                : (intro
                    ? "(H — 다시 보기)"
                    : $"필수 단서 {have}/{req}  ·  발견 {found}/{total}   (H — 다시 보기)");

            if (main == _lastMain && sub == _lastSub) return;
            _lastMain = main; _lastSub = sub;
            StatusPanel.Set("목표", 0, $"{main}\n{sub}");   // 상태창 맨 윗줄

            // 말이 바뀐 <b>그때</b>부터 센다. 몇 초 뒤 저 혼자 진다 —
            // 늘 떠 있는 안내는 안내가 아니라 창이다.
            _until = _holdSeconds > 0f ? Time.time + _holdSeconds : -1f;
        }
    }
}
