using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>판의 결</b> — 색과 크기를 어디서 받아 오나.
    ///
    /// 견우팀 꾸러미(<c>IMUNROK.Ui</c>)를 들이면서 정한 갈래는 이랬다:
    /// <b>우리에게 없는 것은 부품째 받고, 있는 것은 디자인만 맞춘다.</b>
    /// 자막 바·수첩·심문 판은 이미 우리 것이 있고 물증 제시·<c>ISelectable</c>·Wit.ai
    /// 와 얽혀 있어서 통째로 갈아 끼울 수가 없다. 그러니 <b>겉만</b> 맞춘다.
    ///
    /// <b>베끼지 않고 물어 온다.</b> 색값을 여기 옮겨 적으면 그 순간 두 벌이 되고,
    /// 저쪽이 고칠 때마다 우리 쪽은 조용히 옛 색으로 남는다. 그래서 이 파일은
    /// 값을 <b>가지지 않고</b> <see cref="IMUNROK.Ui.UiSkin"/> 을 그대로 넘겨준다.
    /// 꾸러미를 새로 받으면 우리 판도 함께 따라간다.
    /// (그러려고 <c>IMUNROK.Common.asmdef</c> 에 <c>IMUNROK.Ui</c> 참조를 더했다.)
    ///
    /// <b>우리 것만 남기는 자리도 있다.</b> 이름표의 붉은 낙관은 꾸러미에 없는 색이고,
    /// 「누가 말하는가」를 낙관으로 찍는 것은 이 게임의 글투다. 그런 것은 여기 적는다 —
    /// 꾸러미에 없어서 못 물어 오는 것이지, 안 맞춘 것이 아니다.
    /// </summary>
    public static class UiLook
    {
        // ── 꾸러미에서 물어 오는 것 ─────────────────

        /// <summary>판 바탕. 꾸러미의 수첩 바탕과 같다.</summary>
        public static Color Back { get { return Ui.UiSkin.NoteBack; } }

        /// <summary>본문 글씨.</summary>
        public static Color Body { get { return Ui.UiSkin.NoteBody; } }

        /// <summary>머리글·강조.</summary>
        public static Color Head { get { return Ui.UiSkin.NoteHead; } }

        /// <summary>안내 한 줄 — 본문보다 물러나 있어야 한다.</summary>
        public static Color Hint { get { return Ui.UiSkin.HintText; } }

        /// <summary>
        /// 이름패 바탕 — <b>주칠(朱漆)</b>.
        ///
        /// 한동안 우리 낙관 색(0.62, 0.14, 0.11)을 「꾸러미에 없는 색」이라며 남겨 두었다.
        /// <b>틀렸다.</b> 꾸러미도 이름패는 붉다 — <c>InventorySkin.Vermilion</c>
        /// (0.667, 0.216, 0.161) 이고, 저쪽 주석에도 「주칠 이름패를 그대로 쓴다」고
        /// 적혀 있다. 없는 색이 아니라 <b>내가 안 찾아본 색</b>이었다.
        ///
        /// 그러니 우리 낙관은 버린다. 붉은 것을 잃는 것이 아니라 <b>같은 붉은 것</b>으로
        /// 모으는 일이다.
        /// </summary>
        public static Color Seal { get { return Ui.InventorySkin.Vermilion; } }

        /// <summary>이름패 위의 글씨 — 한지빛. 주칠 위에서는 이것이 뜬다.</summary>
        public static Color SealText { get { return Ui.InventorySkin.Hanji; } }

        // ── 한지 판(수첩·문서) ───────────────────────
        //
        // <b>꾸러미의 소지품 판도 한지다.</b> 검은 반투명으로 갈아엎어야 하는 줄 알았는데
        // 찾아보니 저쪽 палette 가 한지·먹·주칠이었다 — 우리와 <b>같은 결</b>이다.
        // 그래서 수첩은 뜯어고칠 일이 아니라 <b>값을 옮길</b> 일이었다.

        /// <summary>한지 바탕.</summary>
        public static Color Paper { get { return Ui.InventorySkin.Hanji; } }

        /// <summary>한지에서 한 켜 어두운 것 — 카드·덧면.</summary>
        public static Color PaperDim { get { return Ui.InventorySkin.HanjiDim; } }

        /// <summary>먹빛 글씨.</summary>
        public static Color Ink { get { return Ui.InventorySkin.Ink; } }

        /// <summary>묽은 먹 — 곁글·이미 읽은 것.</summary>
        public static Color InkSoft { get { return Ui.InventorySkin.InkSoft; } }

        /// <summary>나뭇결 — 테두리·표지.</summary>
        public static Color Wood { get { return Ui.InventorySkin.Wood; } }

        /// <summary>밝은 나뭇결.</summary>
        public static Color WoodLit { get { return Ui.InventorySkin.WoodLit; } }

        // ── 뒤늦게 채운 이름들 (2026-09-05) ──────────
        //
        // 화면 열여섯 곳이 색을 손으로 박아 두고 있었다. 세어 보니 87자리인데
        // <b>팔레트와 똑같은 것이 하나도 없었다</b> — 한지가 여섯 벌, 먹이 일곱 벌,
        // 주칠이 여덟 벌, 따뜻한 글씨가 열 벌로 갈라져 있었다.
        //
        // 갈라진 까닭은 게으름이 아니라 <b>부를 이름이 없어서</b>였다. 어두운 판 위의
        // 크림빛 글씨를 여기서 뭐라 부르는지 몰라 저마다 (0.98, 0.96, 0.92) 같은 것을
        // 새로 지어 적었다. 그러니 이름부터 짓는다.

        // ── 어두운 판 한 벌 ─────────────────────────
        //
        // <b>이것도 새로 지을 뻔했다.</b> 「어두운 판 위의 따뜻한 글씨」를 뭐라 부를지
        // 몰라 이름을 하나 지으려다, 꾸러미의 <c>DialogueUI.Palette()</c> 에 이미
        // <b>한 벌이 통째로</b> 있는 것을 보았다 — 뒤판·테두리·글씨·안내·글쇠칸까지.
        // 자막 바와 심문 판은 진작부터 그것을 쓰고 있었는데, 나머지 열세 곳이
        // 그런 것이 있는 줄 모르고 저마다 비슷한 색을 새로 적고 있었다.
        //
        // 그래서 여기서는 <b>이름만 빌려 준다</b>. 판마다
        // <c>IMUNROK.Ui.DialogueUI.Palette()</c> 를 직접 부르게 하면 그 긴 이름을
        // 아는 사람만 쓰게 된다 — 모르면 또 손으로 적는다.

        private static Ui.BarPalette Bar { get { return Ui.DialogueUI.Palette(); } }

        /// <summary>어두운 판의 뒤판 — 먹빛. 여섯 벌로 갈라져 있던 것이 이것이다.</summary>
        public static Color Panel { get { return Bar.back; } }

        /// <summary>판 테두리 — 나뭇결빛.</summary>
        public static Color Border { get { return Bar.border; } }

        /// <summary>어두운 판 위의 글씨. <b>열 벌</b>로 갈라져 있던 것이 이것이다.</summary>
        public static Color Text { get { return Bar.text; } }

        /// <summary>판 위의 안내 한 줄 — 글씨보다 물러나 있다.</summary>
        public static Color Dim { get { return Bar.dim; } }

        /// <summary>칠 수 있는 칸 안쪽 — 뒤판보다 조금 더 짙다.</summary>
        public static Color Slot { get { return Bar.slotBack; } }

        /// <summary>칸에 친 글.</summary>
        public static Color SlotText { get { return Bar.slotText; } }

        /// <summary>칸에 묽게 앉는 안내.</summary>
        public static Color SlotHint { get { return Bar.slotHint; } }

        /// <summary>금박 — 표제·강조. 아껴 쓴다.</summary>
        public static Color Gold { get { return Ui.InventorySkin.Gold; } }

        /// <summary>
        /// 글씨 밑에 까는 그림자. <b>이것만은 꾸러미에 없다</b> —
        /// 저쪽은 TMP 라 외곽선으로 하고, 우리 낡은 <c>UI.Text</c> 는 그림자로 한다.
        /// </summary>
        public static Color Shadow { get { return new Color(0f, 0f, 0f, 0.85f); } }

        /// <summary>같은 색을 <b>투명도만</b> 바꿔 쓴다. 색을 새로 짓지 않으려고 둔다.</summary>
        public static Color With(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }

        /// <summary>같은 색을 한 켜 <b>어둡게</b>. 눌린 자리·가라앉은 자리에 쓴다.</summary>
        public static Color Deep(Color c, float t = 0.35f)
        {
            return Color.Lerp(c, new Color(0f, 0f, 0f, c.a), Mathf.Clamp01(t));
        }

        /// <summary>같은 색을 한 켜 <b>밝게</b>. 얹힌 자리·고른 자리에 쓴다.</summary>
        public static Color Lit(Color c, float t = 0.25f)
        {
            return Color.Lerp(c, new Color(1f, 1f, 1f, c.a), Mathf.Clamp01(t));
        }

    }
}
