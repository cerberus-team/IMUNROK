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
    ///   · 필수 단서(J04·J09·J13·J15) 부족 → "집 안을 조사하라 (필수 N/4)"
    ///   · 필수 단서 다 모음         → "출도하라! (F2 ▸ 출도)"
    /// H 키로 켜고 끌 수 있다. 아무 오브젝트(예: _OnggojipCase)에 붙이면 됨.
    /// 표시는 월드 공간 알림판(WorldNotice)이 맡는다.
    /// </summary>
    public class ObjectiveHud : MonoBehaviour
    {
        [SerializeField] private bool _show = true;
        [TextArea] [SerializeField] private string _introLine = "마을 어귀에 닿았다. 수첩(I)을 살피거나, 지나는 이에게 말을 걸어보자";
        [TextArea] [SerializeField] private string _investigateLine = "밤이다. 몰래 집 안을 조사하라";
        [TextArea] [SerializeField] private string _readyLine = "증거를 충분히 모았다 — 출도하라!  (F2 ▸ 출도)";

        private const CaseId ThisCase = CaseId.Case1_Onggojip;

        private bool _inside;

        /// <summary>
        /// 집 안으로 들어섰다는 신호. 중간대문 TeleportZone 의 _onTeleported 에 물리면 된다.
        /// 이 뒤로는 "지나는 이에게 말을 걸어보자"가 뜨지 않는다 — 이미 지나쳐 온 단계다.
        /// </summary>
        public void EnterInside() => _inside = true;

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                _show = !_show;
#endif
        }

        // 목표 안내는 월드 공간 알림판으로 띄운다(OnGUI는 헤드셋에 안 보인다).
        // 매 프레임 문자열을 새로 만들 필요가 없어 값이 바뀔 때만 갱신한다.
        private string _lastMain, _lastSub;

        private void LateUpdate()
        {
            bool hide = !_show || InterrogationController.AnyOpen || JournalView.AnyOpen;
            var journal = Journal.Instance;
            if (hide || journal == null)
            {
                if (_lastMain != null) { WorldNotice.Hide("목표"); _lastMain = null; _lastSub = null; }
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
                    ? "(H: 안내 숨기기)"
                    : $"필수 단서 {have}/{req}  ·  발견 {found}/{total}   (H: 목표 숨기기)");

            if (main == _lastMain && sub == _lastSub) return;
            _lastMain = main; _lastSub = sub;
            WorldNotice.Show("목표", $"{main}\n{sub}", 0.42f);   // 시선보다 위 — 앞을 가리지 않게
        }
    }
}
