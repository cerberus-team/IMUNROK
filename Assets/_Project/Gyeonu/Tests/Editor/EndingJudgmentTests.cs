using NUnit.Framework;

namespace IMUNROK.Gyeonu.Tests
{
    /// <summary>
    /// 엔딩 판정 (2026-09-13 「엔딩 시스템 기획 문서」 28·30). 우선순위와 진엔딩 기준을 못 박는다.
    /// 판정 <b>시점</b>(재회 대화의 끝)은 EndingDirector 의 몫이라 여기서는 다루지 않는다.
    /// </summary>
    public class EndingJudgmentTests
    {
        [SetUp] public void SetUp() => GyeonuCase.ResetAll();
        [TearDown] public void TearDown() => GyeonuCase.ResetAll();

        static void GiveCore()
        {
            GyeonuCase.AddClue(ClueId.C1);
            GyeonuCase.AddClue(ClueId.C2);
            GyeonuCase.AddClue(ClueId.C3);
        }

        [Test]
        public void 구출_전에는_판정하지_않는다()
        {
            GiveCore();
            GyeonuCase.SetEvidence(100);
            Assert.AreEqual(EndingId.None, GyeonuCase.Ending);
            Assert.AreEqual(EndingId.None, GyeonuCase.FinishCase(), "판정 불가면 기록도 하지 않는다");
        }

        [Test]
        public void 구출_증거충분_핵심3종_진엔딩()
        {
            GiveCore();
            GyeonuCase.SetEvidence(80);
            GyeonuCase.SeonaRescued = true;
            Assert.IsTrue(GyeonuCase.Proven);
            Assert.AreEqual(EndingId.Truth, GyeonuCase.Ending);
        }

        [Test]
        public void 구출_증거부족_노멀엔딩()
        {
            GiveCore();
            GyeonuCase.SetEvidence(79);
            GyeonuCase.SeonaRescued = true;
            Assert.AreEqual(EndingId.Normal, GyeonuCase.Ending);
        }

        [Test]
        public void 증거도80이어도_핵심3종_없으면_노멀()
        {
            GyeonuCase.AddClue(ClueId.C1);
            GyeonuCase.AddClue(ClueId.C3);
            GyeonuCase.SetEvidence(100);
            GyeonuCase.SeonaRescued = true;
            Assert.IsFalse(GyeonuCase.Proven);
            Assert.AreEqual(EndingId.Normal, GyeonuCase.Ending);
        }

        [Test]
        public void 경계도100은_구출과_증거를_이긴다()
        {
            GiveCore();
            GyeonuCase.SetEvidence(100);
            GyeonuCase.SeonaRescued = true;
            GyeonuCase.SetAlert(100);
            Assert.IsTrue(GyeonuCase.EndingForced);
            Assert.IsTrue(GyeonuCase.Fired(Threshold.Alert100));
            Assert.AreEqual(EndingId.Bad, GyeonuCase.Ending);
        }

        [Test]
        public void 경계도100_뒤의_구출은_결과를_바꾸지_못한다()
        {
            GyeonuCase.SetAlert(100);
            GiveCore();
            GyeonuCase.SetEvidence(100);
            GyeonuCase.SeonaRescued = true;
            Assert.AreEqual(EndingId.Bad, GyeonuCase.Ending);
        }

        [Test]
        public void 서고_최소경로만으로는_진엔딩에_모자란다()
        {
            // 서고에서 얻는 C1·C2·C3(60) + 견우 B1(10) = 70. 낮에 하나 더 모았어야 한다 — 노멀의 근거.
            GiveCore();
            GyeonuCase.AddClue(ClueId.B1);
            GyeonuCase.SeonaRescued = true;
            Assert.AreEqual(70, GyeonuCase.Evidence);
            Assert.AreEqual(EndingId.Normal, GyeonuCase.Ending);

            GyeonuCase.AddClue(ClueId.C5);       // 집무실 장부 10
            Assert.AreEqual(EndingId.Truth, GyeonuCase.Ending);
        }

        [Test]
        public void 엔딩_이름은_문서_31을_따른다()
        {
            Assert.AreEqual("밝혀진 진실", GyeonuCase.Label(EndingId.Truth));
            Assert.AreEqual("살아 돌아온 직녀", GyeonuCase.Label(EndingId.Normal));
            Assert.AreEqual("끝나지 않은 칠석", GyeonuCase.Label(EndingId.Bad));
            Assert.AreEqual("사건 종결", EndingScript.Outcome(EndingId.Truth));
            Assert.AreEqual("조사 종결", EndingScript.Outcome(EndingId.Normal));
            Assert.AreEqual("조사 중단", EndingScript.Outcome(EndingId.Bad));
        }

        [Test]
        public void 옛_세이브의_고정값은_배드로_읽힌다()
        {
            var d = new GyeonuSaveData { forcedEnding = 3 };
            GyeonuSave.FromJson(UnityEngine.JsonUtility.ToJson(d));
            Assert.AreEqual(EndingId.Bad, GyeonuCase.Ending);

            d.forcedEnding = 2;                  // 옛 '절반의 구원'은 고정값이 아니었다
            GyeonuSave.FromJson(UnityEngine.JsonUtility.ToJson(d));
            Assert.IsFalse(GyeonuCase.EndingForced);
        }
    }
}
