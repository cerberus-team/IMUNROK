namespace IMUNROK.Common
{
    /// <summary>
    /// <b>안내에 적힐 손짓을 한 군데에 모은다.</b>
    ///
    /// 안내문이 죄 <b>거짓말</b>이 되어 있었다. 종이를 펴면 "오른쪽 단추를 누른 채"라
    /// 적히고, 서리는 "(Space 넘기기)"라 말한다. 헤드셋을 쓰고 있으면 오른쪽 단추도
    /// Space 도 누를 손이 없다 — 시킨 대로 했는데 아무 일이 없으니, 도구가 고장 난
    /// 것으로 읽힌다. "돋보기나 등불을 어떻게 쓰는지 모르겠다"는 말이 그것이었다.
    ///
    /// 글귀를 쓰는 자리마다 <c>if (VR)</c> 를 두면 곧 어긋난다 — 단추를 옮기는 날
    /// 고쳐야 할 데가 열 곳이고, 그중 한 곳은 반드시 남는다. 그래서 <b>묻는 자리</b>를
    /// 하나로 둔다. 단추가 바뀌면 여기만 고친다.
    ///
    /// <b>2026-08-27 — 견우팀 꾸러미와 손짓을 맞췄다.</b>
    /// 색과 치수를 맞추고 나니 판은 같아 보이는데 <b>손이 다른 것을 눌러야</b> 했다.
    /// 저쪽은 왼쪽 Ctrl 로 말하고 Enter 로 묻는데 우리는 T 로 말하고 묻는 절차가 아예
    /// 없었다. 같은 판에 다른 조작이 붙어 있으면 <b>같은 화면이 아니라 닮은 화면</b>일
    /// 뿐이다. 그래서 이름을 <c>IMUNROK.Ui.UiWords</c> 와 하나씩 짝지어 두었다 —
    /// 저쪽이 단추를 옮기면 여기도 그대로 옮기면 된다.
    ///
    /// <b>다만 값을 물어 오지는 않는다.</b> <c>UiWords</c> 는 <c>UiModes.IsVr</c>(F8 로
    /// 손수 바꾸는 스위치)를 보고, 우리는 <see cref="VRRig.Active"/>(헤드셋이 실제로
    /// 서 있나)를 본다. 판단하는 근거가 다르므로 <b>말만 맞추고 판단은 우리가 한다</b>.
    ///
    /// 지금 매여 있는 자리는 <see cref="VRButtons"/> 와 <see cref="ToolRaise"/> 에 있다.
    /// </summary>
    public static class Controls
    {
        /// <summary>헤드셋을 쓰고 서 있나.</summary>
        public static bool Vr { get { return VRRig.Active; } }

        // ── 꾸러미와 짝지은 이름들 (UiWords 와 한 줄씩 대응) ──────────

        /// <summary>누르기 — 좌클릭 / 트리거. (<c>UiWords.Press</c>)</summary>
        public static string Press { get { return Vr ? "트리거" : "좌클릭"; } }

        /// <summary>
        /// 물러나기 — Esc / B·Y. (<c>UiWords.Back</c>)
        ///
        /// <b>여기 하나는 저쪽과 다르게 뒀다.</b> 저쪽은 「Esc / 우클릭」인데, 우리 화면에서
        /// 오른쪽 단추는 <b>누르고 있는 동안 고개를 돌리는</b> 손짓이다
        /// (<see cref="DebugFlyCamera"/>). 그대로 맞추면 심문 중에 주위를 둘러볼 때마다
        /// 판이 닫힌다 — 오류 한 줄 안 나면서 「왜 자꾸 꺼지지」가 되는 종류다.
        /// 없는 단추를 적지 않으려고 만든 것이 이 클래스인데, 있는 단추에 두 가지 일을
        /// 시켜 놓고 적는 것도 같은 거짓말이다. 그래서 <b>Esc 만</b> 적는다.
        /// </summary>
        public static string Back { get { return Vr ? "B·Y" : "Esc"; } }

        /// <summary>수첩 여닫기 — I·Esc / 메뉴 버튼. (<c>UiWords.Menu</c>)</summary>
        public static string Menu { get { return Vr ? "메뉴 버튼" : "I / Esc"; } }

        /// <summary>가리키는 동작 — 바라보고 / 겨누고. (<c>UiWords.Aim</c>)</summary>
        public static string Aim { get { return Vr ? "겨누고" : "바라보고"; } }

        /// <summary>
        /// 말하기 — 왼쪽 Ctrl / 그립. (<c>UiWords.Talk</c>)
        ///
        /// <b>T 에서 왼쪽 Ctrl 로 옮겼다.</b> T 를 고른 까닭이 따로 있던 것이 아니라
        /// 그냥 비어 있는 글쇠였다. 저쪽이 Ctrl 인 이상 맞추는 편이 낫다 —
        /// 글쇠 칸에 한글을 치는 중에도 Ctrl 은 글자를 먹지 않기 때문이다.
        ///
        /// VR 쪽은 <b>왼손 X 에서 그립으로</b> 옮겼다. 그러려고 짚기(광선)에서 그립을
        /// 떼어 냈다 — 둘이 같은 단추에 있으면 <b>말하려고 쥔 손이 눈앞의 단추를 누른다</b>.
        /// </summary>
        public static string Speak { get { return Vr ? "그립" : "왼쪽 Ctrl"; } }

        /// <summary>묻기(받아 적힌 말을 던지기) — Enter / 「묻 기」.</summary>
        public static string Ask { get { return Vr ? "「묻 기」" : "Enter"; } }

        /// <summary>
        /// 빈 입력줄에 묽게 앉는 말. 저쪽 <c>placeholder</c> 자리다.
        ///
        /// 저쪽 PC 문구는 「묻고 싶은 것을 치거나, 왼쪽 Ctrl을 누르고 말하시오…」인데
        /// <b>우리는 칠 칸이 없다</b> — 물을 길이 목소리 하나뿐이라 앞 절을 뺀다.
        /// 있지도 않은 조작을 적지 않는다는 규칙이 여기에도 그대로 걸린다.
        /// </summary>
        public static string SpeakPrompt { get { return Speak + "을 누르고 말하시오…"; } }

        // ── 우리에게만 있는 것들 ─────────────────────────────
        //
        // 저쪽에 짝이 없다. 도구를 손에 드는 놀이가 우리 쪽에만 있어서다.

        /// <summary>도구를 눈앞으로 들어 올리는 손짓.</summary>
        public static string Raise
        {
            get { return Vr ? "오른손 스틱을 <b>누른 채</b>" : "오른쪽 단추를 <b>누른 채</b>"; }
        }

        /// <summary>든 것을 내려놓는 손짓.</summary>
        public static string PutDown
        {
            get { return Vr ? "(오른손 B — 내려놓기)" : "(Esc — 내려놓기)"; }
        }

        /// <summary>
        /// 한 마디를 넘기는 손짓. 헤드셋에서는 <b>가만두면 저 혼자 넘어간다</b> —
        /// 넘기는 단추를 따로 두지 않았으므로 없는 단추를 적지 않는다.
        /// </summary>
        public static string Skip
        {
            get { return Vr ? "(잠깐 두면 넘어갑니다)" : "(Space 넘기기)"; }
        }

        /// <summary>
        /// 심문 중 자막 바 아랫줄에 늘 걸리는 <b>조작 안내</b>.
        ///
        /// 저쪽 <c>DialogueUI.HintLine</c> 을 그대로 옮겨 적은 것이다. 순서까지 같게 뒀다 —
        /// 두 화면을 나란히 놓고 볼 사람이 <b>같은 자리에서 같은 말</b>을 읽어야 한다.
        /// 말하기만 주칠로 물들이는 것도 저쪽 그대로다(그 하나가 이 게임의 조작이라서).
        /// </summary>
        public static string InterrogationHint
        {
            get
            {
                // 색은 저쪽 값 그대로다. 다만 자릿수를 여덟로 적는다 —
                // 저쪽은 TMP 라 여섯 자리도 알아듣지만, 우리 아랫줄은 아직 낡은
                // <c>UI.Text</c> 라 <b>여덟 자리(투명도까지)</b>가 아니면 그냥 안 먹는다.
                string red = "<color=#8E2C20FF>" + Speak + " — 누르고 말하기</color>";
                if (Vr) return red + "     트리거 — 누르기     B·Y — 심문 끝내기";
                return "Enter — 묻기     " + red + "     좌클릭 — 누르기     Esc — 심문 끝내기";
            }
        }

        /// <summary>
        /// 손에 든 도구를 <b>어떻게 쓰는가</b> — 한 줄.
        ///
        /// 도구를 골라 놓고도 무엇을 해야 할지 몰라 서 있는 일이 잦았다. 벨트는
        /// 무엇을 들었는지만 말해 주고, 쓰는 법은 아무 데도 안 적혀 있었다.
        /// 도구를 바꾼 그 자리에서 한 줄로 일러 준다.
        /// </summary>
        public static string HowTo(string toolId)
        {
            switch (toolId)
            {
                case "lantern":
                    return "종이를 편 뒤 불을 <b>종이 너머로</b> 대면 겹 사이가 비친다";
                case "magnify":
                    return Raise + " 눈에 댄다 · 댄 채로 들여다보면 잔글씨가 읽힌다";
                case "mapae":
                    return "사람 앞에서 내보이면 알아본다";
                case "yucheok":
                    return "물건에 대면 치수가 읽힌다";
            }
            return "";
        }
    }
}
