using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace IMUNROK.Gyeonu.Tests
{
    public class DialogueGrantValidatorTests
    {
        NpcProfile profile;

        [SetUp] public void SetUp() => GyeonuCase.ResetAll();

        [TearDown]
        public void TearDown()
        {
            if (profile != null) Object.DestroyImmediate(profile);
            GyeonuCase.ResetAll();
        }

        static IEnumerable<TestCaseData> ClueCases()
        {
            // 각 단서: 직접 질문, 자연스러운 우회 질문, 포괄 질문, 무관 질문, 정답 유도+짧은 동의.
            yield return C(ClueId.A3,NpcId.Jumo,true,"예전에도 이런 실종이 있었소? 그때 날씨는 어땠소?","그때 실종이 있던 칠석도 구름 없이 맑았지.","A3-direct");
            yield return C(ClueId.A3,NpcId.Jumo,true,"비 오는 해에도 사람이 사라졌소?","그 해 실종은 비 없는 맑은 칠석에만 있었네.","A3-indirect");
            yield return C(ClueId.A3,NpcId.Jumo,false,"오늘 날씨가 좋구려.","오늘은 맑네.","A3-broad");
            yield return C(ClueId.A3,NpcId.Jumo,false,"칠석 음식은 무엇이오?","부침개라네.","A3-unrelated");
            yield return C(ClueId.A3,NpcId.Jumo,false,"실종은 맑은 칠석에만 있었지?","그래.","A3-leading-short");

            yield return C(ClueId.A4,NpcId.Mother,true,"왜 이 연못을 은하담이라 부르오?","딸이 직녀가 된 그 일 뒤 이곳을 은하담이라 부르게 됐소.","A4-direct");
            yield return C(ClueId.A4,NpcId.Mother,true,"이곳 이름은 언제부터 은하담이었소?","내 딸의 그 일이 있은 뒤 은하담이라는 이름을 붙였소.","A4-indirect");
            yield return C(ClueId.A4,NpcId.Mother,false,"연못이 예쁘구려.","그렇소.","A4-broad");
            yield return C(ClueId.A4,NpcId.Mother,false,"여기서 물고기가 잡히오?","잡히오.","A4-unrelated");
            yield return C(ClueId.A4,NpcId.Mother,false,"딸 때문에 은하담이라 이름 붙였지?","그렇소.","A4-leading-short");

            yield return C(ClueId.A7,NpcId.Jumo,true,"사람들이 정말 하늘로 사라졌다고 믿소?","칠석이면 직녀가 하늘로 데려간다는 전설은 틀림없는 사실이야.","A7-direct");
            yield return C(ClueId.A7,NpcId.Jumo,true,"칠석 전설이 사실이라고 생각하시오?","난 그 칠석 전설을 분명한 사실로 믿지.","A7-indirect");
            yield return C(ClueId.A7,NpcId.Jumo,false,"선아는 어떤 아이요?","착한 아이였지.","A7-broad");
            yield return C(ClueId.A7,NpcId.Jumo,false,"주막은 언제부터 했소?","오래됐지.","A7-unrelated");
            yield return C(ClueId.A7,NpcId.Jumo,false,"직녀가 하늘로 데려간 게 틀림없지?","그래.","A7-leading-short");

            yield return C(ClueId.B3,NpcId.Jumo,true,"실종 전에 선아가 밤에 나간 적이 있소?","며칠 동안 밤마다 밖으로 나갔지.","B3-direct");
            yield return C(ClueId.B3,NpcId.Jumo,true,"해 진 뒤 그 아씨를 밖에서 본 적 있소?","두어 번 늦은 시각에 집을 나서는 걸 봤지.","B3-indirect");
            yield return C(ClueId.B3,NpcId.Jumo,false,"선아는 어떤 사람이오?","착한 사람이었지.","B3-broad");
            yield return C(ClueId.B3,NpcId.Jumo,false,"오늘 장사는 잘되오?","그럭저럭이지.","B3-unrelated");
            yield return C(ClueId.B3,NpcId.Jumo,false,"선아가 매일 밤 수령을 만나러 나갔다면서?","그런 건 모르네.","B3-false-premise");
            yield return C(ClueId.B3,NpcId.Jumo,false,"선아가 며칠 동안 밤마다 몰래 나갔다던데 사실이오?","그래.","B3-leading-short");

            yield return C(ClueId.B4,NpcId.FestivalMerchant,true,"그날 밤 오작교 근처에서 견우를 보았소?","그날 견우가 오작교 건너편에 혼자 서 있었소.","B4-direct");
            yield return C(ClueId.B4,NpcId.FestivalMerchant,true,"그날 다리 건너편에 혼자 있던 사내가 있었소?","사내 하나가 다리 건너편에 혼자 있었지.","B4-indirect");
            yield return C(ClueId.B4,NpcId.FestivalMerchant,false,"견우는 평소 어떤 사람이오?","조용한 사내요.","B4-broad");
            yield return C(ClueId.B4,NpcId.FestivalMerchant,false,"오작교는 오래됐소?","오래됐지.","B4-unrelated");
            yield return C(ClueId.B4,NpcId.FestivalMerchant,false,"그날 견우가 다리 건너에 혼자 있었지?","그래.","B4-leading-short");

            yield return C(ClueId.B5,NpcId.FirstJiknyeo,true,"그날 선아라는 사람이 여기까지 왔소?","선아는 그날 이 마을에 오지 않았소.","B5-direct");
            yield return C(ClueId.B5,NpcId.FirstJiknyeo,true,"그날 밤 젊은 여인이 이 마을로 들어왔소?","그날 밤 찾아온 사람은 아무도 없었소.","B5-indirect");
            yield return C(ClueId.B5,NpcId.FirstJiknyeo,false,"성하리 소식을 들었소?","조금 들었소.","B5-broad");
            yield return C(ClueId.B5,NpcId.FirstJiknyeo,false,"여기에는 몇 명이 사오?","열쯤 사오.","B5-unrelated");
            yield return C(ClueId.B5,NpcId.FirstJiknyeo,false,"그날 선아는 오지 않았지?","그렇소.","B5-leading-short");

            yield return C(ClueId.B7,NpcId.Gyeonu,true,"선아가 사라진 날 어디 있었소?","그 일은 말할 수 없습니다.","B7-direct");
            yield return C(ClueId.B7,NpcId.Gyeonu,true,"그날 마지막으로 선아를 본 뒤 어디로 갔소?","그 뒤 행적은 밝힐 수 없습니다.","B7-indirect");
            yield return C(ClueId.B7,NpcId.Gyeonu,false,"견우는 평소 어떤 사람이오?","저는 목수입니다.","B7-broad");
            yield return C(ClueId.B7,NpcId.Gyeonu,false,"이름이 무엇이오?","견우입니다.","B7-unrelated");
            yield return C(ClueId.B7,NpcId.Gyeonu,false,"그날 숨어 있었지?","그렇습니다.","B7-leading-short");

            yield return C(ClueId.C6,NpcId.FestivalMerchant,true,"예전 실종 때도 관아 순찰이 많았소?","예전에도 실종이 있던 해마다 포졸 순찰이 유독 많았소.","C6-direct");
            yield return C(ClueId.C6,NpcId.FestivalMerchant,true,"사람이 사라진 해마다 관아 움직임이 달랐소?","그런 해에는 관아 순찰이 늘었지.","C6-indirect");
            yield return C(ClueId.C6,NpcId.FestivalMerchant,false,"그날 밤 수령을 봤소?","다리에서 수령을 봤소.","C6-vs-M1");
            yield return C(ClueId.C6,NpcId.FestivalMerchant,false,"오늘 장사는 잘되오?","잘되오.","C6-unrelated");
            yield return C(ClueId.C6,NpcId.FestivalMerchant,false,"실종 때마다 순찰이 심했지?","그렇소.","C6-leading-short");

            yield return C(ClueId.C7,NpcId.Mother,true,"선아 아버지가 정말 관물을 훔친 죄인이었소?","죄인이 아니오. 관아 비리를 조사하다 누명을 썼소.","C7-direct");
            yield return C(ClueId.C7,NpcId.Mother,true,"그 검수관이 무슨 일을 하다 잡혀갔소?","관아를 조사하던 이였고 훔치지 않았는데 누명을 썼소.","C7-indirect");
            yield return C(ClueId.C7,NpcId.Mother,false,"선아 아버지는 어떤 사람이었소?","성실한 사람이었소.","C7-broad");
            yield return C(ClueId.C7,NpcId.Mother,false,"오늘은 평안하시오?","그렇소.","C7-unrelated");
            yield return C(ClueId.C7,NpcId.Mother,false,"검수관은 죄인이 아니었지?","그렇소.","C7-leading-short");

            yield return C(ClueId.C8,NpcId.Magistrate,true,"선아 실종을 어떻게 보고 계시오?","올해의 견우가 수상하니 그 사내 행적을 조사해야 하오.","C8-direct");
            yield return C(ClueId.C8,NpcId.Magistrate,true,"관아에서는 이 사건을 어떻게 조사하고 있소?","견우라는 사내를 가장 의심해 캐묻고 있소.","C8-indirect");
            yield return C(ClueId.C8,NpcId.Magistrate,false,"수령 나리 평안하시오?","평안하오.","C8-broad");
            yield return C(ClueId.C8,NpcId.Magistrate,false,"칠석제 준비는 잘 되었소?","잘 되었소.","C8-unrelated");
            yield return C(ClueId.C8,NpcId.Magistrate,false,"견우가 범인이지요?","그렇소.","C8-leading-short");

            yield return C(ClueId.C3,NpcId.Seona,true,"진상을 밝히는 수사를 돕겠소. 풀이 기준을 줄 수 있소?","이 풀이표를 가져가세요. 기록의 비교 기준이 될 거예요.","C3-direct");
            yield return C(ClueId.C3,NpcId.Seona,true,"관아 장부 조사를 함께 돕고 싶소.","제가 만든 비교표와 판독 기준을 드릴게요.","C3-indirect");
            yield return C(ClueId.C3,NpcId.Seona,false,"그 뒤 어떻게 지냈소?","힘들었어요.","C3-broad");
            yield return C(ClueId.C3,NpcId.Seona,false,"날씨가 좋소.","그러네요.","C3-unrelated");
            yield return C(ClueId.C3,NpcId.Seona,false,"풀이표를 주겠지?","그래요.","C3-leading-short");

            yield return C(ClueId.B3,NpcId.Jumo,true,"해가 진 뒤 그 아씨를 밖에서 본 적 있소?","실종 전 며칠 동안 밤마다 어디론가 돌아다니더라고.","B3-natural-roaming");
            yield return C(ClueId.C6,NpcId.FestivalMerchant,true,"예전 실종 때도 관아 순찰이 많았소?","그런 해에는 관아 순찰이 유난히 잦았소.","C6-natural-frequent");
            yield return C(ClueId.A4,NpcId.Mother,true,"왜 이 못을 은하담이라 부르오?","그 이름은 딸의 사건 뒤부터 사람들이 붙여 부른 것이오.","A4-pronoun-origin");
            yield return C(ClueId.C7,NpcId.Mother,true,"선아 아버지가 정말 관물을 훔친 죄인이었소?","그이는 죄인이 아니었소. 오히려 관아를 캐고 다니던 사람이었지.","C7-natural-investigation");
            yield return C(ClueId.A4,NpcId.Mother,true,"이곳 이름은 언제부터 은하담이었소?","내 딸의 그 사건 뒤 은하담이라는 이름을 붙였소.","A4-name-particle");
            yield return C(ClueId.B3,NpcId.Jumo,true,"해가 진 뒤 그 아씨를 밖에서 본 적 있소?","실종 전 며칠 동안 밤마다 밖을 나다니더라고.","B3-sunset-particle");
            yield return C(ClueId.C3,NpcId.Seona,true,"이 일을 끝까지 조사하겠소. 기록 대조를 도와주겠소?","두 기록을 비교하는 기준을 드리겠습니다.","C3-comparing-standard");
            yield return C(ClueId.B7,NpcId.Gyeonu,true,"선아가 사라진 그날 밤 어디 있었소?","…그날 밤은, 그냥 집에 있었습니다. 저를 너무 그렇게 보지 마십시오.","B7-false-home-alibi");
            yield return C(ClueId.B7,NpcId.Gyeonu,true,"선아가 사라진 그날 밤 어디 있었소?","…그날 밤은, 그냥 집에 있었습니다. 자, 저는 이만 가봐야겠습니다.","B7-leaving-evasion");
            yield return C(ClueId.B7,NpcId.Gyeonu,true,"선아가 사라진 그날 밤 어디 있었소?","…그날 밤은, 그냥 집에 있었습니다. 더는 드릴 말씀이 없습니다.","B7-no-more-to-say");
            yield return C(ClueId.C8,NpcId.Magistrate,true,"선아 실종 사건을 어떻게 보고 계시오?","마지막으로 그 아이를 본 자가 따로 있다 하니, 견우를 먼저 찾아보는 것이 어떻겠소.","C8-deflect-to-gyeonu");
            yield return C(ClueId.C3,NpcId.Seona,true,"당신을 구하고 이 일을 끝까지 조사하겠소. 기록 대조를 도와주겠소?","두 기록을 맞대어 보는 기준을 알려 드리겠습니다.","C3-match-against-standard");
        }

        static TestCaseData C(ClueId clue, NpcId npc, bool expected, string q, string a, string name) =>
            new TestCaseData(clue, npc, expected, q, a).SetName(name);

        [TestCaseSource(nameof(ClueCases))]
        public void ConversationClues(ClueId clue, NpcId npc, bool expected, string question, string answer)
        {
            profile = Make(npc, clue);
            if (clue == ClueId.C3)
            {
                GyeonuCase.SeonaRescued = true; // SeonaRescue가 StandingUp 완료 뒤 설정하는 값
                DialogueGrantValidator.ObserveFacts(profile, question, answer);
            }
            Assert.That(DialogueGrantValidator.CanGrant(profile, clue, question, answer), Is.EqualTo(expected));
            Assert.That(GyeonuCase.IdentityRevealed, Is.False, "C3 대화는 공식 신분 증명과 분리되어야 한다.");
        }

        [TestCase(NpcId.Magistrate,"선아가 사라진 그날 밤 어디 계셨소?","그날은 동헌에서 칠석제 마무리를 하고 있었소.",GyeonuWorld.F_수령최초알리바이,true)]
        [TestCase(NpcId.Magistrate,"그 시각 수령 나리는 무엇을 하고 계셨소?","사라진 날 관아에 있었소.",GyeonuWorld.F_수령최초알리바이,true)]
        [TestCase(NpcId.Magistrate,"평소 밤에는 어디 계시오?","대개 동헌에 있소.",GyeonuWorld.F_수령최초알리바이,false)]
        [TestCase(NpcId.FestivalMerchant,"그날 밤 오작교에서 관아 어른을 보았소?","수령 나리가 다리를 둘러보는 것을 봤소.",GyeonuWorld.F_상인수령당일목격,true)]
        [TestCase(NpcId.FestivalMerchant,"새벽에 다리 쪽에서 수령을 본 적 있소?","새벽 오작교에서 수령 나리를 봤소.",GyeonuWorld.F_상인수령당일목격,true)]
        [TestCase(NpcId.FestivalMerchant,"예전에도 순찰이 많았소?","예전 실종 때 순찰이 많았소.",GyeonuWorld.F_상인수령당일목격,false)]
        [TestCase(NpcId.Jumo,"선아 아버지가 죄인이었다는 게 무슨 말이오?","그 검수관은 관물을 훔친 죄인이었지.",GyeonuWorld.F_주모죄인주장,true)]
        [TestCase(NpcId.Jumo,"그 검수관이 정말 관물을 빼돌렸소?","관물을 빼돌린 죄인이라고 들었네.",GyeonuWorld.F_주모죄인주장,true)]
        [TestCase(NpcId.Jumo,"선아 아버지는 어떤 사람이오?","잘 모르네.",GyeonuWorld.F_주모죄인주장,false)]
        public void ObservedFacts(NpcId npc, string q, string a, string flag, bool expected)
        {
            profile = Make(npc);
            DialogueGrantValidator.ObserveFacts(profile, q, a);
            Assert.That(GyeonuCase.HasFlag(flag), Is.EqualTo(expected));
        }

        [Test]
        public void M1RequiresBothFactsAndExplicitContradiction()
        {
            profile = Make(NpcId.Magistrate);
            GyeonuCase.SetFlag(GyeonuWorld.F_수령최초알리바이);
            GyeonuCase.SetFlag(GyeonuWorld.F_상인수령당일목격);
            Assert.That(DialogueGrantValidator.CanConfirmM1(profile,"아까는 동헌이라 했는데 그날 오작교 목격과 어째 모순되지 않소?","잠시 순찰을 돌며 둘러봤을 뿐이오."),Is.True);
            Assert.That(DialogueGrantValidator.CanConfirmM1(profile,"그날 순찰은 어땠소?","잠시 순찰했소."),Is.False);
        }

        [Test]
        public void M2SourceAdmissionRequiresClaimAndC7()
        {
            profile = Make(NpcId.Jumo);
            Assert.That(DialogueGrantValidator.CanConfirmM2(profile,"아까 죄인이라 했는데 출처가 어디요?","직접 본 건 아니고 관아에서 들은 소문이네."),Is.False);
            GyeonuCase.SetFlag(GyeonuWorld.F_주모죄인주장); GyeonuCase.AddClue(ClueId.C7);
            Assert.That(DialogueGrantValidator.CanConfirmM2(profile,"아까 죄인이라 했는데 직접 본 것이오?","직접 본 건 아니고 관아에서 들은 소문이네."),Is.True);
            Assert.That(DialogueGrantValidator.CanConfirmM2(profile,"오늘 장사는 어떻소?","관아에서 들었네."),Is.False);
        }

        [TestCase(NpcId.Child02,TalkTone.Neutral,"관아 담장 쪽에 드나들 틈 같은 건 없니?","담장 뒤편에 작은 틈이 있어요.",true)]
        [TestCase(NpcId.Child02,TalkTone.Favor,"너희가 몰래 다니는 길이 있니?","관아 뒤편 개구멍으로 다녀요.",true)]
        [TestCase(NpcId.Child02,TalkTone.Pressure,"빨리 말해. 어디로 숨어들면 돼?","담장 뒤편에 구멍이 있어요.",false)]
        [TestCase(NpcId.Child02,TalkTone.Insult,"말 안 하면 혼날 줄 알아. 비밀길을 말해.","관아 담장에 틈이 있어요.",false)]
        public void ChildSecret(NpcId npc, TalkTone tone, string q, string a, bool expected)
        {
            profile = Make(npc); profile.secretFlag = "child_secret";
            Assert.That(DialogueGrantValidator.CanRevealSecret(profile,tone,q,a),Is.EqualTo(expected));
        }

        [Test]
        public void FirstGyeonuSecretRequiresB2()
        {
            profile = Make(NpcId.FirstGyeonu); profile.secretFlag = "first_gyeonu_secret";
            string q = "관측실 바닥 아래에 흔적을 봤소. 관아 쪽 통로가 있소?";
            string a = "그 아래 길은 관아 통로로 이어지오.";
            Assert.That(DialogueGrantValidator.CanRevealSecret(profile,TalkTone.Neutral,q,a),Is.False);
            GyeonuCase.AddClue(ClueId.B2);
            Assert.That(DialogueGrantValidator.CanRevealSecret(profile,TalkTone.Neutral,q,a),Is.True);
            Assert.That(DialogueGrantValidator.CanRevealSecret(profile,TalkTone.Pressure,q,a),Is.False);
        }

        static NpcProfile Make(NpcId npc, params ClueId[] grants)
        {
            var p = ScriptableObject.CreateInstance<NpcProfile>();
            p.npcId = npc; p.grantableClues = grants;
            return p;
        }
    }
}
