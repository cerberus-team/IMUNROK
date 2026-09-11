using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Onggojip
{
    /// <summary>제1사건이 어떻게 끝났는가 — <b>이야기의 끝</b>. 넷이다.</summary>
    public enum OnggojipEndingId
    {
        None = 0,

        /// <summary>진상 — 진짜를 세우고 갑리도 끊었다</summary>
        Truth = 1,

        /// <summary>절반의 진상 — 진짜는 세웠으나 갑리는 남았다</summary>
        HalfTruth = 2,

        /// <summary>헤아린 매 — 사정을 헤아려 덮었다</summary>
        Mercy = 3,

        /// <summary>옹고집전 — 가짜를 인정했다. 설화가 그대로 남는다</summary>
        LegendStands = 4,
    }

    /// <summary>
    /// <b>제1사건의 끝을 제3사건과 같은 얼개로 둔다.</b>
    ///
    /// <b>왜 이것이 따로 있어야 하는가.</b> 공통 <see cref="GameState"/> 가 적는 것은
    /// <see cref="Verdict"/> 셋(진실대로·정상참작·甲 인정)뿐이다. 그것은 <b>온 게임의
    /// 판결</b>이지 <b>이 이야기의 끝</b>이 아니다. 제3사건(견우)은 그 둘을 갈라 두었다 —
    /// <c>GyeonuCase.EndingId</c> 가 이름 붙은 끝 넷을 지니고, <c>FinishCase()</c> 가
    /// 그것을 공통 <see cref="Verdict"/> 로 옮겨 적는다. 제1사건에는 그 켜가 <b>아예
    /// 없어서</b>, 판결 단추가 곧바로 <c>SetVerdict</c> 를 부르고 끝이었다. 그래서
    /// 「이 사건이 어떻게 끝났는가」를 물을 자리가 없었다.
    ///
    /// 여기서는 제3사건과 <b>같은 이름, 같은 차례</b>로 둔다 —
    /// <see cref="Ending"/> · <see cref="EndingLabel"/> · <see cref="FinishCase"/>.
    /// 두 사건이 같은 말로 끝나야 마무리 연출과 복명(엔딩 씬)이 한 틀로 받는다.
    ///
    /// <b>다만 무엇이 끝을 가르는지는 사건마다 다르다.</b> 제3사건은 <b>세상에서 일어난
    /// 일</b>(구출했는가 × 입증했는가)이 끝을 가른다 — 거기서는 판결이 그 결과로
    /// 딸려 나온다. 제1사건은 반대다. 여기서 마지막에 벌어지는 일은 <b>고을 원 앞에서
    /// 무엇이라 고하는가</b>이고, 그 고함이 곧 결말이다. 그러므로
    ///
    ///   끝 = (고한 판결) × (갑리를 처결했는가)
    ///
    /// 로 가른다. 셋 곱하기 둘이면 여섯이 되지만, 정상참작과 甲 인정에서는 갑리가
    /// 곁가지라 이름이 갈리지 않는다 — 그래서 넷이다.
    ///
    /// <b>입증은 끝을 가르지 않고 <see cref="TruthProven"/> 으로 따로 둔다.</b>
    /// 손에 쥔 것 없이 「진실대로」를 고르는 일은 막아야 하지만, 그것은 <b>고를 수
    /// 있는가</b>의 문제이지 <b>어떻게 끝났는가</b>의 문제가 아니다. 판 쪽에서 이 값으로
    /// 단추를 잠그면 된다.
    /// </summary>
    public static class OnggojipEnding
    {
        private const CaseId ThisCase = CaseId.Case1_Onggojip;

        /// <summary>
        /// <b>진상의 결정타를 쥐었는가.</b> 필적이 바뀐 자국(J13)과 입안대장(G03)이
        /// 함께 있어야 「복동아」로 몰아붙일 수 있다 — <c>OnggojipCase.CanFinalPress</c>
        /// 와 같은 조건이다. 두 벌이 어긋나지 않게 여기 한 자리에 둔다.
        /// </summary>
        public static bool TruthProven
        {
            get
            {
                var j = Journal.Instance;
                if (j == null) return false;
                return j.HasClue(ThisCase, "J13") && j.HasClue(ThisCase, "G03");
            }
        }

        /// <summary>갑리(고리대)를 처결했는가. 공통 기록에 남아 있다.</summary>
        public static bool GapriHandled
        {
            get
            {
                var gs = GameState.Instance;
                return gs != null && gs.GapriHandled;
            }
        }

        /// <summary>이 판결로 끝낸다면 어떤 끝인가.</summary>
        public static OnggojipEndingId EndingFor(Verdict v)
        {
            switch (v)
            {
                case Verdict.Truth:
                    return GapriHandled ? OnggojipEndingId.Truth : OnggojipEndingId.HalfTruth;
                case Verdict.Mercy:
                    return OnggojipEndingId.Mercy;
                case Verdict.AcceptFake:
                    return OnggojipEndingId.LegendStands;
                default:
                    return OnggojipEndingId.None;
            }
        }

        /// <summary>이미 적어 둔 판결로 본 끝. 아직 안 고했으면 <see cref="OnggojipEndingId.None"/>.</summary>
        public static OnggojipEndingId Ending
        {
            get
            {
                var gs = GameState.Instance;
                if (gs == null) return OnggojipEndingId.None;
                return EndingFor(gs.GetVerdict(ThisCase));
            }
        }

        /// <summary>사람에게 보일 이름.</summary>
        public static string EndingLabel { get { return LabelOf(Ending); } }

        public static string LabelOf(OnggojipEndingId e)
        {
            switch (e)
            {
                case OnggojipEndingId.Truth:        return "진상";
                case OnggojipEndingId.HalfTruth:    return "절반의 진상";
                case OnggojipEndingId.Mercy:        return "헤아린 매";
                case OnggojipEndingId.LegendStands: return "옹고집전";
                default:                            return "아직";
            }
        }

        /// <summary>
        /// 끝에 붙는 한 줄. 복명에서 읽어 주기 좋게 여기 둔다 —
        /// 제3사건의 <c>EndingLabel</c> 이 이름만 주는 것과 달리, 제1사건은
        /// 갑리가 남았는지 아닌지가 <b>이름만으로는 안 보이므로</b> 한 줄을 붙인다.
        /// </summary>
        public static string LineOf(OnggojipEndingId e)
        {
            switch (e)
            {
                case OnggojipEndingId.Truth:
                    return "가짜를 벗기고 갑리까지 끊었다. 옹당촌에 옹덕구가 하나로 남는다.";
                case OnggojipEndingId.HalfTruth:
                    return "진짜는 제자리로 돌아갔으나 장리쌀은 그대로다. 빚은 사람을 바꾸지 않는다.";
                case OnggojipEndingId.Mercy:
                    return "사정을 헤아려 덮었다. 덮은 것이 사라지지는 않는다.";
                case OnggojipEndingId.LegendStands:
                    return "가짜를 진짜로 적었다. 이 고을에 남는 것은 사실이 아니라 이야기다.";
                default:
                    return "";
            }
        }

        /// <summary>
        /// <b>사건을 끝맺는다.</b> 공통 <see cref="GameState"/> 에 판결을 적고,
        /// 그 판결이 어떤 끝이었는지를 돌려준다.
        ///
        /// 제3사건의 <c>GyeonuCase.FinishCase()</c> 와 <b>같은 자리</b>다. 다른 점은
        /// 판결을 <b>받아서</b> 적는다는 것뿐이다 — 제3사건은 세상의 결과에서 판결을
        /// 뽑아내지만, 여기서는 판결 자체가 플레이어의 마지막 행동이다.
        /// </summary>
        public static OnggojipEndingId FinishCase(Verdict chosen, bool gapriHandled)
        {
            var gs = GameState.Instance;
            if (gs == null) return OnggojipEndingId.None;

            gs.SetGapriHandled(gapriHandled);
            gs.SetVerdict(ThisCase, chosen);

            var e = EndingFor(chosen);
            Debug.Log("[옹고집] 끝 = " + LabelOf(e) + "  (판결 " + chosen +
                      " / 갑리 " + (gapriHandled ? "처결" : "그대로") +
                      " / 진상 결정타 " + (TruthProven ? "쥠" : "못 쥠") + ")");
            return e;
        }
    }
}
