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
    ///   왼손 X      말하기 — <b>누르고 있는 동안</b> 듣는다
    ///   왼손 Y      수첩
    ///   왼손 ☰      지도
    ///   왼손 스틱↓   일어서기(숨은 자리에서)
    ///   오른손 A     도구 바꾸기
    ///   오른손 B     닫기 — 문서를 내려놓거나 심문을 끝낸다
    ///   오른손 스틱↓  도구 들어 올리기 — 돋보기를 눈에 댄다(<see cref="ToolRaise"/>)
    ///   방아쇠        짚기 — 물건이든 <b>화면의 단추</b>든(<see cref="VRUiRay"/>)
    /// </code>
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
        private bool _lx, _ly, _lm, _ls, _ra, _rb;

        private void Update()
        {
            var L = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var R = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            // ── 왼손 X — 말하기(누르는 동안) ──
            bool lx = Held(L, CommonUsages.primaryButton);
            if (lx != _lx)
            {
                var mic = MicInput.Instance;
                if (mic != null) { if (lx) mic.StartListening(); else mic.StopListening(); }
                _lx = lx;
            }

            // ── 왼손 Y — 수첩 ──
            bool ly = Held(L, CommonUsages.secondaryButton);
            if (ly && !_ly)
            {
                var j = Object.FindFirstObjectByType<JournalView>(FindObjectsInactive.Include);
                if (j != null) j.Toggle();
            }
            _ly = ly;

            // ── 왼손 ☰ — 지도 ──
            bool lm = Held(L, CommonUsages.menuButton);
            if (lm && !_lm && TravelBoard.Instance != null) TravelBoard.Instance.Toggle();
            _lm = lm;

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
            //
            // 하나만 닫는다. 문서를 든 채 심문 중일 수 있는데 둘을 한꺼번에 닫으면
            // 단추 한 번에 두 겹이 걷혀 어디로 돌아온 것인지 알 수가 없다.
            // 나중에 연 것부터 닫는다 — 문서가 먼저다.
            bool rb = Held(R, CommonUsages.secondaryButton);
            if (rb && !_rb)
            {
                if (DocumentView.IsOpen) DocumentView.Hide();
                else if (InterrogationController.Active != null)
                    InterrogationController.Active.CloseFromOutside();
            }
            _rb = rb;
        }

        private static bool Held(InputDevice d, InputFeatureUsage<bool> f)
            => d.isValid && d.TryGetFeatureValue(f, out bool v) && v;
    }
}
