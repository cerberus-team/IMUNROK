using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 1막 나머지 심문 캐릭터(마름·늙은하인·乙·마을사람) 에셋을 한 번에 생성.
    /// 각 인물의 성격·추천질문(단서 획득)·증거 반응을 채워둔다.
    ///
    /// 생성 후: 각 큐브에 InterrogationController 붙이고 해당 에셋 연결(Backend=Gemini, BeginOnStart 끄기).
    /// 메뉴: [이문록 ▸ 옹고집: 1막 나머지 심문 캐릭터 생성].
    /// </summary>
    public static class OnggojipNpcBuilder
    {
        private const string Dir = "Assets/_Project/Onggojip/Data";

        [MenuItem("이문록/옹고집: 1막 나머지 심문 캐릭터 생성")]
        public static void Build()
        {
            EnsureFolder(Dir);

            // ── 마름 ──
            Make("Mareum_Interrogation", "마름",
                "옹덕구네 소작을 관리하는 마름. 술을 좋아하고 취하면 입이 가벼워진다. " +
                "주인(甲)을 오래 봐서 뭔가 미심쩍은 걸 느끼지만 맨정신엔 함구한다. " +
                "취중엔 '주인과 누군가가 닮았다'는 말을 무심코 흘린다. 어사에게 과하게 굽신거린다.",
                "어이쿠, 어사또… 이 밤중에 어인 일로…",
                new[]
                {
                    T("주인어른을 오래 모셨소?", "예에… 스무 해 넘게 이 댁 땅을 봐왔지요.", "", ""),
                    T("한 잔 걸치셨소?", "허허… 조금… 아주 조금 했습니다요.", "", ""),
                    T("주인어른, 옛날 그대로시오?", "그러고 보믄… 참 닮았지비… 아이고, 아무것도 아닙니다요!",
                        "J06", "마름의 취중 증언 — 둘이 닮았다"),
                },
                new[] { G("J09", "장부 필적이 한 달 전후로 바뀜", "글씨는… 소인이 뭘 알겠습니까요.", true) });

            // ── 늙은 하인 ──
            Make("Servant_Interrogation", "늙은 하인",
                "이 집에서 평생을 산 늙은 종. 진짜 주인을 어릴 적부터 봐왔다. " +
                "지금 주인(甲)이 어딘가 이상하다는 걸 알지만, 두려움과 오랜 습관 때문에 말하지 못한다. " +
                "주인을 부를 때 호칭을 머뭇거린다. 1막에서는 절대 진실을 말하지 않고 회피한다.",
                "…어사또. 소인 같은 늙은것에게 무슨 볼일이…",
                new[]
                {
                    T("주인어른을 뭐라 부르시오?", "나… 나리… (말끝을 흐린다)", "J07", "하인들이 호칭에 머뭇거린다"),
                    T("이 집에 오래 계셨소?", "평생을… 이 집에서 늙었습니다요.", "", ""),
                    T("주인께 형제가 있었소?", "…소인은 아무것도 모릅니다. 부디…", "", ""),
                },
                new[] { G("J15", "행랑채 궤 — 속량 문서", "그건… 소인은… 모르는 일입니다…", true) });

            // ── 乙 (진짜 옹덕구) ──
            Make("EulOng_Interrogation", "乙 (진짜 옹덕구?)",
                "자신이 진짜 옹덕구라 주장하는 사내. 집에서 쫓겨나 미친 사람 취급을 받는다. " +
                "억울하고 절박하게 진실을 외치지만 아무도 믿어주지 않는다. 두서없고 격앙돼 있다.",
                "내가! 내가 진짜 옹덕구요! 저 안에 있는 놈은 가짜란 말이오!",
                new[]
                {
                    T("진정하시오. 무슨 일이오?", "저 놈이 내 이름, 내 집, 내 처자식까지 다 빼앗았소!",
                        "J01", "乙이 마을에서 미친 사람 취급을 받는다"),
                    T("증거가 있소?", "문서요! 별급문기! 헌데 그놈이… 다 태워버렸을 거요!", "", ""),
                },
                new EvidenceGate[0]);

            // ── 마을 사람 ──
            Make("Villager_Interrogation", "마을 사람",
                "평범한 마을 사람. 甲을 진짜 옹덕구 나리로 알고, 乙을 미친 사람으로 여긴다. " +
                "밤중에 나타난 낯선 이(어사)를 조금 경계한다.",
                "뉘신지요… 이 늦은 밤에?",
                new[]
                {
                    T("저 댁 주인이 누구요?", "옹덕구 나리시지요. 온 마을이 다 아는 어른입니다.",
                        "J02", "마을 사람들은 甲을 진짜라 여긴다"),
                    T("저기 저 사내는?", "아이고, 저 미친것… 상대 마십시오.",
                        "J01", "乙이 마을에서 미친 사람 취급을 받는다"),
                },
                new EvidenceGate[0]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("이문록",
                "1막 심문 캐릭터 4명 생성 완료!\n" +
                "· 마름 / 늙은 하인 / 乙 / 마을 사람\n(" + Dir + ")\n\n" +
                "각 큐브에 InterrogationController 붙이고 해당 에셋 연결 →\nBackend=Gemini, Gemini Model=gemini-flash-latest, Begin On Start 끄기.", "확인");
        }

        // ── 헬퍼 ──
        private static TopicQuestion T(string q, string a, string key, string text) =>
            new TopicQuestion { question = q, mockAnswer = a, grantsClueKey = key, grantsClueText = text };

        private static EvidenceGate G(string key, string clueText, string reveal, bool deflect) =>
            new EvidenceGate { clueKey = key, clueText = clueText, revealsInfo = reveal, deflectionOnly = deflect };

        private static void Make(string fileName, string charName, string persona, string opening,
            TopicQuestion[] topics, EvidenceGate[] gates)
        {
            var c = ScriptableObject.CreateInstance<InterrogationCharacter>();
            c.characterName = charName;
            c.caseId = CaseId.Case1_Onggojip;
            c.persona = persona;
            c.openingLine = opening;
            c.topics = new List<TopicQuestion>(topics);
            c.evidenceGates = new List<EvidenceGate>(gates);

            string path = $"{Dir}/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<InterrogationCharacter>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(c, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
