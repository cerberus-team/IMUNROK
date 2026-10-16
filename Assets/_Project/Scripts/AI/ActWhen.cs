namespace IMUNROK.Common
{
    /// <summary>
    /// <b>어느 막의 것인가.</b>
    ///
    /// 이 사건은 한 사람을 <b>두 번</b> 만난다. 1막에서 甲은 하룻밤 청하는 과객을 맞는
    /// 주인이고, 2막에서 같은 甲은 어사 앞에 끌려 나온 자다. 그런데 여태 심문 자료에는
    /// 막을 가리는 칸이 없어서, 1막에 「그건 어디서 주웠소? 허튼 종이 쪼가리요」라고
    /// 발뺌하도록 적어 둔 그 한 줄이 <b>2막에서도 똑같이</b> 나왔다. 무너지기 직전의
    /// 사람이 어제와 같은 말투로 같은 말을 한다.
    ///
    /// 한 칸을 놓아 갈라 준다. 같은 단서에 두 줄을 적어 두고 하나는 1막, 하나는 2막으로
    /// 두면 <b>같은 종이를 두 번 들이밀되 다른 대답</b>을 듣는다 — 그것이 막이 넘어갔다는 뜻이다.
    ///
    /// 값이 0(<see cref="두막다"/>)이라 여태 적어 둔 것들은 그대로 두 막에서 산다.
    /// </summary>
    public enum ActWhen
    {
        /// <summary>막을 가리지 않는다(여태의 자리).</summary>
        두막다 = 0,
        /// <summary>1막 잠행 — 묻는 이가 과객으로 보일 때만.</summary>
        잠행1막 = 1,
        /// <summary>2막 공개 — 어사임을 안 뒤에만.</summary>
        공개2막 = 2,
    }

    /// <summary>막을 재는 자. <see cref="GameState.IdentityRevealed"/> 하나로 갈린다.</summary>
    public static class ActGate
    {
        /// <summary>지금이 2막인가. GameState 가 아직 없으면 1막으로 본다.</summary>
        public static bool Revealed
        {
            get
            {
                var gs = GameState.Instance;
                return gs != null && gs.IdentityRevealed;
            }
        }

        /// <summary>이 막 표시가 지금 막에 맞는가.</summary>
        public static bool Fits(ActWhen when, bool revealed)
        {
            if (when == ActWhen.두막다) return true;
            return (when == ActWhen.공개2막) == revealed;
        }

        /// <summary>지금 막에 맞는가.</summary>
        public static bool Fits(ActWhen when) => Fits(when, Revealed);
    }
}
