using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 1막 甲(가짜 옹덕구) 대면 심문 캐릭터 에셋을 만든다(대사·추천질문·증거 반응 채워서).
    ///  - 추천 질문으로 甲이 스스로 자랑 → J04(필수)·J05 획득(제 발등 찍기)
    ///  - 증거(J09 필적/J15 속량문서) 제시 → 1막엔 발뺌만(안 무너짐, 2막에서 무너짐)
    ///
    /// 목업으로 바로 테스트 가능(키 불필요). 나중에 Backend를 Gemini로 바꾸면 AI가 이어받음.
    /// 메뉴: [이문록 ▸ 옹고집: 甲 심문 캐릭터 생성].
    /// </summary>
    public static class GapInterrogationBuilder
    {
        private const string Path = "Assets/_Project/Onggojip/Data/Gap_Interrogation.asset";

        [MenuItem("이문록/옹고집: 甲 심문 캐릭터 생성")]
        public static void Build()
        {
            var c = ScriptableObject.CreateInstance<InterrogationCharacter>();
            c.characterName = "옹덕구(甲)";
            c.caseId = CaseId.Case1_Onggojip;
            c.persona =
                "겉은 이 집의 주인 옹덕구. 실은 스무 해 문서를 다루던 얼자 출신 종 '복동'이 진짜 주인 행세를 한다. " +
                "영리하고 능청스러우며, 자신이 이 집 주인임을 한 치도 의심받지 않으려 한다. " +
                "과객(어사)에게는 여유로운 주인처럼 대한다. 절대 먼저 실토하지 않으며, 문서·필적·아우(乙)·속량 이야기엔 태연히 발뺌한다. " +
                "다만 자기 능력(문서를 다 다뤘다는 자부심)은 은근히 자랑한다.";
            c.openingLine = "허허, 먼 길 오셨소. 누추한 집에 무슨 볼일이시오?";

            c.topics = new List<TopicQuestion>
            {
                new TopicQuestion {
                    question = "이 큰 살림을 홀로 꾸리시오?",
                    mockAnswer = "문서는 다 내 손을 거쳤소. 스무 해를 그리 했지. 이만한 집을 아무나 건사하는 줄 아시오?",
                    grantsClueKey = "J04",
                    grantsClueText = "\"문서는 다 내 손을 거쳤소\" (스무 해)"
                },
                new TopicQuestion {
                    question = "요즘 집안은 두루 평안하시오?",
                    mockAnswer = "다 잘 있소. 안사람도, 아이들도. 이 집 일이라면 내 모르는 게 없소이다.",
                    grantsClueKey = "J05",
                    grantsClueText = "甲이 최근 집안일을 완벽히 안다"
                },
                new TopicQuestion {
                    question = "형제분은 없으시오?",
                    mockAnswer = "…형제? 없소. 나 혼자요. 어찌 그런 걸 다 묻소.",
                    grantsClueKey = "",   // 단서 없음(乙 복선·긴장)
                    grantsClueText = ""
                },
            };

            c.evidenceGates = new List<EvidenceGate>
            {
                new EvidenceGate {
                    clueKey = "J09",
                    clueText = "장부 필적이 한 달 전후로 바뀜",
                    revealsInfo = "필체야 세월 따라 변하는 법. 그게 무슨 흠이라도 되오?",
                    deflectionOnly = true
                },
                new EvidenceGate {
                    clueKey = "J15",
                    clueText = "행랑채 궤 — 속량 문서",
                    revealsInfo = "그건 어디서 주웠소? …허튼 종이 쪼가리요. 나와 무슨 상관이란 말이오.",
                    deflectionOnly = true
                },
            };

            EnsureFolder("Assets/_Project/Onggojip/Data");
            var existing = AssetDatabase.LoadAssetAtPath<InterrogationCharacter>(Path);
            if (existing != null) AssetDatabase.DeleteAsset(Path);
            AssetDatabase.CreateAsset(c, Path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = c;
            EditorGUIUtility.PingObject(c);
            EditorUtility.DisplayDialog("이문록",
                "甲 심문 캐릭터 생성 완료!\n" + Path + "\n\n" +
                "테스트: 빈 오브젝트에 InterrogationController 추가 → Character에 이 에셋 드래그 → Backend=Mock → Play.\n" +
                "추천 질문으로 J04·J05 얻고, 증거(J09/J15) 제시하면 발뺌합니다.", "확인");
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
