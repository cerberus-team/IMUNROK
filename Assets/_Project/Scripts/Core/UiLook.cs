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

        // ── 우리 것 ─────────────────────────────────

        /// <summary>
        /// 이름표의 붉은 낙관. <b>꾸러미에 없는 색</b>이다.
        /// 말하는 이를 낙관으로 찍는 것은 이 게임의 글투라 그대로 둔다.
        /// </summary>
        public static readonly Color Seal = new Color(0.62f, 0.14f, 0.11f, 0.95f);

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
    }
}
