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
    /// 지금 매여 있는 자리는 <see cref="VRButtons"/> 와 <see cref="ToolRaise"/> 에 있다.
    /// </summary>
    public static class Controls
    {
        /// <summary>헤드셋을 쓰고 서 있나.</summary>
        public static bool Vr { get { return VRRig.Active; } }

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

        /// <summary>말하기(심문에서 입을 여는 것).</summary>
        public static string Speak
        {
            get { return Vr ? "왼손 X 를 누른 채 말하시오" : "T 를 누른 채 말하시오"; }
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
