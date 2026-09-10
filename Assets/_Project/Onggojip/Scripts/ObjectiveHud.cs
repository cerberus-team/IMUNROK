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
    /// (<b>H</b> 를 누르면 다시 뜬다. 다만 <b>글에는 안 적는다</b> — 지나가는 한 마디에
    ///  단축키까지 붙으면 그것이 도로 창이 된다.)
    ///
    /// 표시는 월드 공간 알림판(WorldNotice)이 맡는다.
    /// </summary>
    public class ObjectiveHud : MonoBehaviour
    {
        [Tooltip("한 번 뜨면 이만큼(초) 머물다 스르르 진다. 0 이면 안 사라진다")]
        [SerializeField] private float _holdSeconds = 6f;
        // 한 줄로 붙여 두면 방문 한 장이 화면을 가로지른다. 굵은 한 마디와
        // 곁말로 나눈다 — 눈에 먼저 드는 것은 「어디에 닿았는가」다.
        [TextArea] [SerializeField] private string _introLine = "마을 어귀에 닿았다";
        // ⚠️ 글쇠가 <b>거짓말</b>이었다. 판은 I 로 열리는데(JournalView) 여기는 J 라 적혀 있었다.
        //    그리고 판의 이름은 이제 「소지품」이다 — 화면에 적힌 이름과 부르는 이름이 달라선 안 된다.
        [TextArea] [SerializeField] private string _introSub = "소지품(I)을 살피거나, 지나는 이에게 말을 걸어보자";
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
            // 치는 동안에는 H 가 안내가 아니라 <b>글자</b>다.
            if (!Typing.Now && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                { _lastMain = null; _everShown = null; }   // 같은 말이라도 다시 뜨게 한다
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

        /// <summary>
        /// <b>이미 한 번 띄운 말.</b> 지워진 뒤에도 남는다.
        ///
        /// 「마을 어귀에 닿았다」가 몇 번이고 다시 뜨던 까닭이 여기 있었다. 판이
        /// 가려질 때마다(수첩을 펴거나 심문이 열릴 때) <see cref="_lastMain"/> 을
        /// 비워 두었는데, 그것이 곧 「아직 한 번도 안 띄웠다」는 뜻이라 판이 걷히는
        /// 순간 같은 말이 처음처럼 다시 떴다. 집 안에서 등불을 켜도, 단서를 아직
        /// 하나도 못 얻었으면 문구는 여전히 도입이라 그것이 또 떠올랐다.
        ///
        /// 지우는 것과 <b>안 본 것으로 치는 것</b>은 다르다. 그래서 「지금 걸린 말」과
        /// 「이미 본 말」을 따로 센다 — H 는 이쪽까지 비워 다시 보게 하는 손짓이다.
        /// </summary>
        private string _everShown;

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
                    ? _introSub
                    : $"필수 단서 {have}/{req}  ·  발견 {found}/{total}");

            if (main == _lastMain && sub == _lastSub) return;
            _lastMain = main; _lastSub = sub;

            // 이미 한 번 지나간 말이면 다시 안 띄운다. 값은 위에서 갱신해 두었으므로
            // 다음에 <b>다른</b> 말이 되면 그때는 뜬다.
            if (main == _everShown) return;
            _everShown = main;

            // 곁말이 비면 줄바꿈도 없앤다 — 빈 줄이 남으면 한 마디가 괜히 두 줄이 된다.
            StatusPanel.Set("목표", 0, string.IsNullOrEmpty(sub) ? main : $"{main}\n{sub}");

            // 말이 바뀐 <b>그때</b>부터 센다. 몇 초 뒤 저 혼자 진다 —
            // 늘 떠 있는 안내는 안내가 아니라 창이다.
            _until = _holdSeconds > 0f ? Time.time + _holdSeconds : -1f;
        }
    }
}
