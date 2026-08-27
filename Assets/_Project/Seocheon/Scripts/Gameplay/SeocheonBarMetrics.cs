// 대화창 하단 바의 치수표 — 팀 확정안(README 5절)에 서천의 <선택지 줄>을 더한 것.
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 하단 바 치수. ★팀 <c>IMUNROK.Ui.DialogueUI.BottomStyle</c> 의 값을 그대로 쓰되,
    /// 서천에만 있는 <b>선택지 줄</b>을 끼우기 위해 다른 항목에서 자리를 덜어 왔다.
    ///
    /// ■ 왜 판 치수를 안 늘렸나
    ///   팀 문서는 판 치수(PC 2900×461 · VR 1500×535)를 <b>「고치지 말 것」</b> 목록에 둔다 —
    ///   시야각 계산으로 잡은 값이기 때문이다. 그래서 높이를 늘리는 대신,
    ///   · PC : 안내줄을 <b>이름패 줄 오른쪽</b>으로 올려 통째로 한 줄(24+22+19=65)을 비웠다.
    ///   · VR : 글쇠를 칠 수 없으니 <b>입력줄을 뺀다</b>(76+12=88). 안내줄도 뺀다 —
    ///          "Enter — 묻기"는 헤드셋에서 뜻이 없다.
    ///   그렇게 비운 자리에 선택지를 넣어 <b>합이 정확히 461 / 535</b> 가 되게 맞췄다.
    ///
    /// ■ 선택지를 PC는 1×4, VR은 2×2 로 나눈 까닭
    ///   폭이 다르다. PC 2900 에서 1×4 면 칸당 약 690 — "관아 사람들도 자주 오시오?"가 한 줄에 든다.
    ///   VR 1500 에서 1×4 면 칸당 약 350 이라 글자가 두 줄로 접힌다. 그래서 2×2 로 눕혔다.
    /// </summary>
    public struct SeocheonBarMetrics
    {
        public float panelW, panelH;
        public float padX, padTop, padBottom;
        public float nameH, nameToRule, ruleH, ruleToLine;
        public float lineBoxH;
        public float lineToAsk, askH, askGap;
        public int askCols, askRows;
        public float askToInput, inputH;
        public int lineSize, nameSize, inputSize, footSize;
        public bool showInputRow;      // VR 은 글쇠를 못 친다
        public bool showFootInNameRow; // 안내를 이름패 줄에 얹는가

        /// <summary>대사 상자 높이 — ★줄 높이의 정수배여야 마지막 줄이 반만 보이지 않는다.</summary>
        public static float LineBox(int lineSize, int lines)
        {
            const float PerLine = 1.25f + 0.28f;                 // 궁서체 줄높이 + 우리가 얹은 줄간격
            return Mathf.Ceil(lineSize * PerLine * lines) + 6f;  // 6 = 첫 줄 윗여유
        }

        public float AskBlockH { get { return askH * askRows + askGap * (askRows - 1); } }

        /// <summary>세로 항목의 합. ★panelH 와 같아야 한다(아래 Check 로 검산).</summary>
        public float StackH
        {
            get
            {
                float h = padTop + nameH + nameToRule + ruleH + ruleToLine
                        + lineBoxH + lineToAsk + AskBlockH + padBottom;
                if (showInputRow) h += askToInput + inputH;
                return h;
            }
        }

        public static SeocheonBarMetrics Of(bool vr)
        {
            if (vr)
                return new SeocheonBarMetrics
                {
                    panelW = 1500f, panelH = 535f,
                    padX = 60f, padTop = 18f, padBottom = 42f,
                    nameH = 36f + 18f, nameToRule = 12f, ruleH = 3f, ruleToLine = 28f,
                    lineBoxH = LineBox(46, 3),                 // 218
                    lineToAsk = 20f, askH = 66f, askGap = 8f, askCols = 2, askRows = 2,
                    askToInput = 0f, inputH = 0f,
                    lineSize = 46, nameSize = 36, inputSize = 34, footSize = 34,
                    showInputRow = false, showFootInNameRow = false
                };

            return new SeocheonBarMetrics
            {
                panelW = 2900f, panelH = 461f,
                padX = 90f, padTop = 14f, padBottom = 23f,
                nameH = 32f + 18f, nameToRule = 10f, ruleH = 3f, ruleToLine = 24f,
                lineBoxH = LineBox(42, 3),                     // 199
                lineToAsk = 16f, askH = 62f, askGap = 12f, askCols = 4, askRows = 1,
                askToInput = 14f, inputH = 46f,
                lineSize = 42, nameSize = 32, inputSize = 28, footSize = 19,
                showInputRow = true, showFootInNameRow = true
            };
        }
    }
}
