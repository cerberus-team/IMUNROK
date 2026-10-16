using System;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>장계(狀啓)</b> — 사건 하나를 닫는 글. 사건마다 한 벌씩 만든다.
    ///
    /// 어사가 고을에서 본 것을 임금께 올리는 글이다. 조선의 장계는 <b>꼴이 정해져
    /// 있다</b> — 어디에 이르러, 무엇을 보았고, 그 증좌가 무엇이며, 어찌 하시라는
    /// 네 마디다. 그러니 사건이 달라도 <b>틀은 같고 채우는 말만 다르다</b>.
    ///
    /// <b>한 줄 = 한 주장 + 그 증좌</b>(2026-09-14). 여태는 사람을 고르는 칸이 여럿이고 증좌는
    /// 한 칸에 몰려 있어, 누구를 짚었는지와 <b>무엇으로</b> 짚었는지가 따로 놀았다. 이제 줄마다
    /// 「누구는 이러하니, 그 증좌는 …」이다 — 사람 칸에는 <b>모든 인물</b>이 늘 뜨고(답을 게임이 추려
    /// 주지 않는다), 증좌 칸에는 수첩에 적힌 것 가운데 <b>아무거나</b> 골라 넣을 수 있다.
    ///
    /// <b>점수</b>: 사람을 맞히면 그 줄의 인물 점수, 증좌는 그 줄에 적어 둔 점수(맞지 않는 증좌는 감점).
    /// 줄마다 증좌 점수가 기준을 넘어야 그 줄이 <b>선다</b>. 끝은 <see cref="VerdictReport"/> 가 가른다.
    ///
    /// 만드는 법: 프로젝트 창에서 <b>만들기 ▸ 이문록 ▸ 장계</b>.
    /// </summary>
    [CreateAssetMenu(menuName = "이문록/장계", fileName = "장계_")]
    public class CaseReport : ScriptableObject
    {
        [Serializable]
        public class Person
        {
            [Tooltip("줄의 정답이 가리키는 식별자")]
            public string id = "";
            [Tooltip("카드에 뜨는 이름")]
            public string 이름 = "";
            [Tooltip("얼굴 책(이문록_얼굴)의 이름조각. 비우면 이름만 크게 쓴 카드")]
            public string 얼굴 = "";
        }

        [Serializable]
        public class EvidenceScore
        {
            [Tooltip("수첩의 단서 key")]
            public string 단서 = "";
            [Tooltip("이 줄에서 이 증좌가 얼마나 무거운가")]
            public int 점수 = 1;
        }

        /// <summary>처분처럼 정해진 말 가운데 고르는 칸.</summary>
        [Serializable]
        public class Choice
        {
            public string 말 = "";
            [Tooltip("이 단서들 가운데 <b>하나라도</b> 얻어야 보기로 뜬다(쉼표로 여럿). 비우면 늘 뜬다")]
            public string 필요단서 = "";
            [Tooltip("이 말을 고르면 판결이 이렇게 정해진다")]
            public Verdict 판결 = Verdict.None;
            public int 점수 = 0;
        }

        [Serializable]
        public class Line
        {
            [Tooltip("사람 칸 앞의 글")]
            [TextArea(1, 3)] public string 앞 = "";

            [Header("사람 칸")]
            public bool 인물칸 = false;
            public string 정답인물 = "";
            public int 인물점수 = 2;

            [Tooltip("사람 칸과 증좌 칸 사이의 글")]
            [TextArea(1, 3)] public string 가운데 = "";

            [Header("증좌 칸")]
            public bool 증거칸 = false;
            public EvidenceScore[] 증거점수 = new EvidenceScore[0];
            [Tooltip("골라 넣은 증좌의 점수 합이 이만큼은 되어야 이 줄이 선다")]
            public int 증거기준 = 2;
            [Range(1, 4)] public int 증거최대 = 3;

            [Header("고르는 칸(처분)")]
            public Choice[] 보기 = new Choice[0];

            [TextArea(1, 3)] public string 뒤 = "";

            [Tooltip("이 줄을 누르면 위에 뜨는 물음")]
            public string 물음 = "";

            [Tooltip("<b>핵심 줄</b> — 여기서 사람을 틀리면 무엇을 적었든 가짜를 인정한 장계다")]
            public bool 핵심 = false;
            [Tooltip("이 줄이 서면 갑리를 처결한 것으로 적는다(제1사건)")]
            public bool 갑리줄 = false;

            public bool IsChoice => 보기 != null && 보기.Length > 0;
        }

        [Tooltip("어느 사건의 장계인가")]
        public CaseId 사건 = CaseId.Case1_Onggojip;

        [TextArea(2, 4)] public string 머리말 = "臣이 옹진현에 이르러 살피니,";

        [Tooltip("사람 칸에 늘 뜨는 인물 — 모두 넣는다")]
        public Person[] 인물들 = new Person[0];

        public Line[] 줄들 = new Line[0];

        [TextArea(2, 4)] public string 맺음말 = "삼가 아뢰옵나이다.";

        [Header("점수와 끝")]
        [Tooltip("수첩에 있으나 그 줄의 표에 없는 증좌를 넣으면 이만큼 깎는다")]
        public int 오답감점 = 1;
        [Tooltip("엄히 다스리는 처분으로 「참끝」이 되려면 모든 줄이 서고 합이 이만큼은 되어야 한다")]
        public int 참기준 = 30;

        [TextArea(2, 4)] [Tooltip("모든 줄이 서고 엄히 다스렸을 때")]
        public string 참끝 = "";
        [TextArea(2, 4)] [Tooltip("사람은 맞혔으나 줄이 성글 때")]
        public string 반끝 = "";
        [TextArea(2, 4)] [Tooltip("사정을 헤아리는 처분을 증좌로 받쳤을 때")]
        public string 자비끝 = "";
        [TextArea(2, 4)] [Tooltip("사람을 틀렸거나 가짜를 그대로 둔 처분일 때")]
        public string 헛끝 = "";

        [TextArea(1, 3)]
        [Tooltip("이문록에 오르는 한 줄. {끝} 을 쓰면 그 자리에 끝의 이름이 들어간다")]
        public string 이문록줄 = "옹진현 옹고집 — {끝}";
    }
}
