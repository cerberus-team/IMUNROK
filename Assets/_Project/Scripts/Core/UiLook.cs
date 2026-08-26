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

        // ── 크기 ───────────────────────────────────

        /// <summary>
        /// <b>헤드셋에서 편히 읽히는 글씨의 하한(도).</b>
        ///
        /// 꾸러미가 역산해 둔 값이다 — 기준 높이 388단위를 세로 화각 42°에 담으면
        /// 1단위가 0.108°이고, 그때 12~18px 글자가 1.3~1.9°가 된다.
        /// </summary>
        public const float MinAngle = 1.30f;

        /// <summary>
        /// 이 판에서 <paramref name="units"/> 짜리 글씨가 <b>몇 도로 보이는가</b>.
        ///
        /// 눈금이 없으면 「좀 작은 것 같은데」로 끝난다. 재는 자를 여기 둔다.
        /// </summary>
        public static float Angle(float units, float canvasScale, float distance)
        {
            if (distance <= 0.01f || canvasScale <= 0f) return 0f;
            return Mathf.Atan(units * canvasScale / distance) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// <paramref name="wanted"/> 크기가 하한에 못 미치면 <b>하한에 맞게 키워</b> 돌려준다.
        ///
        /// 자막 바의 안내 줄이 30단위로 <b>1.29°</b> 였다 — 하한에서 0.01° 모자란다.
        /// 눈으로는 못 가리는 차이인데, 그런 것이 헤드셋에서 「읽히긴 하는데 눈이
        /// 피로한」 자리를 만든다. 판마다 숫자를 손으로 고치는 대신 여기를 지나가게 한다.
        /// </summary>
        public static int AtLeast(int wanted, float canvasScale, float distance)
        {
            if (distance <= 0.01f || canvasScale <= 0f) return wanted;
            float perUnit = Mathf.Atan(canvasScale / distance) * Mathf.Rad2Deg;
            if (perUnit <= 0f) return wanted;
            int need = Mathf.CeilToInt(MinAngle / perUnit);
            return Mathf.Max(wanted, need);
        }

        /// <summary>
        /// 판을 훑어 하한에 못 미치는 글씨가 있으면 <b>한 줄 일러 준다</b>.
        ///
        /// <b>왜 키우지 않고 이르기만 하나</b>: 자막 바처럼 글상자가 넉넉한 판은
        /// 그냥 키워도 되지만, 수첩은 카드 크기가 손으로 맞춰져 있어서 글씨를
        /// 몰래 키우면 <b>글이 카드를 넘친다</b>. 배치를 아는 사람이 고쳐야 한다.
        ///
        /// 재 보니 수첩은 지금 다 넘긴다(가장 작은 것이 1.31도). 그러니 이것은
        /// 고치는 손이 아니라 <b>다음에 줄일 때 걸리는 자</b>다 — 눈으로는 못 가리는
        /// 0.1도 차이가 헤드셋에서 눈을 피로하게 만든다.
        /// </summary>
        public static void WarnIfTooSmall(Component panel, string what)
        {
            if (panel == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            float scale = panel.transform.lossyScale.y;
            float dist = Vector3.Distance(cam.transform.position, panel.transform.position);
            if (scale <= 0f || dist < 0.05f || dist > 20f) return;
            float perUnit = Mathf.Atan(scale / dist) * Mathf.Rad2Deg;

            int worst = int.MaxValue;
            foreach (var t in panel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (t.fontSize < worst && !string.IsNullOrEmpty(t.text)) worst = t.fontSize;
            if (worst == int.MaxValue) return;

            float angle = worst * perUnit;
            if (angle >= MinAngle) return;
            Debug.LogWarning("[판 " + what + "] 가장 작은 글씨가 " + worst + "단위 → "
                           + angle.ToString("F2") + "도. 헤드셋 하한은 " + MinAngle.ToString("F2")
                           + "도다 — 눈에 띄게 작지는 않아도 읽는 내내 눈이 피로해진다. "
                           + "글씨를 키우거나 판을 눈에서 " + (worst * scale / Mathf.Tan(MinAngle * Mathf.Deg2Rad)).ToString("F2")
                           + "m 안쪽으로 당길 것.");
        }
    }
}
