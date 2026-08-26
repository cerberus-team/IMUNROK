using UnityEngine;
using UnityEngine.XR;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>컨트롤러 단추를 게임에 잇는다.</b>
    ///
    /// 헤드셋을 꽂고 나서야 드러난 것이 있다 — <b>움직이는 것 말고는 아무것도 안 걸려
    /// 있었다</b>. 스틱으로 걷고 돌고, 트리거로 짚어 누르는 것까지는 되는데,
    /// 말하기(T)·도구 바꾸기(Q)·수첩(I)·지도(M)·일어서기(Space)·닫기(Esc) 가 전부
    /// <b>키보드</b>에 매여 있었다. 헤드셋을 쓰면 그 자판을 누를 손이 없다.
    ///
    /// 그래서 같은 일을 컨트롤러 단추에도 건다. 키보드는 그대로 둔다 — 책상에서 고칠
    /// 때는 그쪽이 빠르고, 둘이 서로 방해하지도 않는다.
    ///
    /// <code>
    ///   그립(양손)   말하기 — <b>누르고 있는 동안</b> 듣는다
    ///   왼손 ☰      수첩
    ///   왼손 X      지도
    ///   왼손 Y      닫기 — 오른손 B 와 같다
    ///   왼손 스틱↓   일어서기(숨은 자리에서)
    ///   오른손 A     도구 바꾸기
    ///   오른손 B     닫기 — 문서를 내려놓거나 심문을 끝낸다
    ///   오른손 스틱↓  도구 들어 올리기 — 돋보기를 눈에 댄다(<see cref="ToolRaise"/>)
    ///   방아쇠        짚기 — 물건이든 <b>화면의 단추</b>든(<see cref="VRUiRay"/>)
    /// </code>
    ///
    /// <b>2026-08-27 — 자리를 견우팀 꾸러미에 맞춰 옮겼다.</b> 저쪽 <c>XrUiPointer</c> 는
    /// 트리거로 짚고, <b>그립으로 말하고</b>, B·Y 로 물러나고, ☰ 로 소지품을 연다.
    /// 우리는 X 로 말하고 Y 로 수첩을 열고 있었다 — 판을 똑같이 맞춰 놓고 손만 다른 데를
    /// 누르게 두면 <b>같은 화면이 아니라 닮은 화면</b>이다. 그래서 셋을 옮겼다:
    /// 말하기 X→그립, 수첩 Y→☰, 지도 ☰→X. 밀려난 Y 는 <b>닫기</b>가 받는다
    /// (저쪽이 「B·Y — 대화 끝내기」라 적어 두었고, 우리는 B 하나뿐이었다).
    ///
    /// ⚠️ 그립을 쓰려고 <see cref="VRRaySelector"/> 에서 <b>그립으로 짚는 길을 떼어 냈다</b>.
    ///    겹쳐 두면 말하려고 쥔 손이 눈앞의 단추를 함께 누른다.
    /// ⚠️ 여기는 헤드셋으로 확인하지 못했다 — 옮긴 자리가 실제 컨트롤러에서 맞는지는
    ///    써 보고 정해야 한다.
    ///
    /// <b>말하기만 누르고 있는 방식이다.</b> 한 번 눌러 켜고 다시 눌러 끄게 하면,
    /// 끄는 것을 잊은 채 돌아다니다 엉뚱한 혼잣말이 인물에게 날아간다. 입을 여는 동안
    /// 손가락도 눌려 있어야 <b>말하고 있다는 것을 몸이 안다</b>.
    ///
    /// <b>단추는 스스로 눌린 순간을 안 알려 준다.</b> <see cref="InputDevices"/> 는
    /// 지금 눌려 있나만 말해 주므로, 지난 칸의 상태를 들고 있다가 견줘서 "이번에 눌렸다"
    /// 를 만든다.
    ///
    /// <see cref="VRRig"/> 가 몸을 지을 때 같이 붙인다.
    /// </summary>
    public class VRButtons : MonoBehaviour
    {
        private bool _lx, _ly, _lm, _ls, _ra, _rb, _talk;

        private void Update()
        {
            var L = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var R = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            // ── 그립(어느 손이든) — 말하기(누르는 동안) ──
            //
            // 양손 다 받는다. 저쪽은 오른손을 먼저 보고 없으면 왼손을 보는데, 그러면
            // 쥔 손을 가려 잡아야 한다. 말하는 데에 어느 손인지는 아무 상관이 없다.
            bool talk = Held(L, CommonUsages.gripButton) || Held(R, CommonUsages.gripButton);
            if (talk != _talk)
            {
                var mic = MicInput.Instance;
                if (mic != null) { if (talk) mic.StartListening(); else mic.StopListening(); }
                _talk = talk;
            }

            // ── 왼손 ☰ — 수첩 ──
            bool lm = Held(L, CommonUsages.menuButton);
            if (lm && !_lm)
            {
                var j = Object.FindFirstObjectByType<JournalView>(FindObjectsInactive.Include);
                if (j != null) j.Toggle();
            }
            _lm = lm;

            // ── 왼손 X — 지도 ──
            bool lx = Held(L, CommonUsages.primaryButton);
            if (lx && !_lx && TravelBoard.Instance != null) TravelBoard.Instance.Toggle();
            _lx = lx;

            // ── 왼손 스틱 누름 — 일어서기 ──
            bool ls = Held(L, CommonUsages.primary2DAxisClick);
            if (ls && !_ls)
            {
                var hide = Object.FindFirstObjectByType<HidePlace>(FindObjectsInactive.Include);
                if (hide != null) hide.RiseNow();
            }
            _ls = ls;

            // ── 오른손 A — 도구 바꾸기 ──
            bool ra = Held(R, CommonUsages.primaryButton);
            if (ra && !_ra && ToolbeltHud.Instance != null) ToolbeltHud.Instance.Next();
            _ra = ra;

            // ── 오른손 B — 닫기 ──
            bool rb = Held(R, CommonUsages.secondaryButton);
            if (rb && !_rb) CloseOne();
            _rb = rb;

            // ── 왼손 Y — 닫기(오른손 B 와 같다) ──
            //
            // 저쪽 안내가 「B·Y — 대화 끝내기」다. 물러나는 단추를 한 손에만 두면
            // 반대 손으로 무언가를 쥐고 있는 동안에는 나올 수가 없다.
            bool ly = Held(L, CommonUsages.secondaryButton);
            if (ly && !_ly) CloseOne();
            _ly = ly;
        }

        /// <summary>
        /// 한 겹만 걷는다. 문서를 든 채 심문 중일 수 있는데 둘을 한꺼번에 닫으면
        /// 단추 한 번에 두 겹이 걷혀 어디로 돌아온 것인지 알 수가 없다.
        /// 나중에 연 것부터 닫는다 — 문서가 먼저다.
        /// </summary>
        private static void CloseOne()
        {
            if (DocumentView.IsOpen) DocumentView.Hide();
            else if (InterrogationController.Active != null)
                InterrogationController.Active.CloseFromOutside();
        }

        private static bool Held(InputDevice d, InputFeatureUsage<bool> f)
            => d.isValid && d.TryGetFeatureValue(f, out bool v) && v;
    }
}
