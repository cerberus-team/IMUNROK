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
    /// 공통 파트가 틀을 만들고 사건 팀이 말을 채우는 이 프로젝트의 나눔과 꼭 맞는다.
    ///
    /// <b>왜 객관식이 아니라 빈칸인가.</b> 객관식은 "셋 중에 고르시오"라 답이 눈앞에
    /// 다 있다. 빈칸은 <b>고를 말 자체를 조사해서 얻어야</b> 한다 — 보기 하나하나에
    /// 필요한 단서를 걸어 두면, 안 캐낸 말은 목록에 <b>뜨지도 않는다</b>. 흉터를
    /// 안 재 본 사람은 "왼팔 안쪽의 흉터"라고 쓸 수가 없다. 그것이 이 게임에서
    /// 조사와 판결을 잇는 유일한 매듭이다.
    ///
    /// 만드는 법: 프로젝트 창에서 <b>만들기 ▸ 이문록 ▸ 장계</b>.
    /// </summary>
    [CreateAssetMenu(menuName = "이문록/장계", fileName = "장계_")]
    public class CaseReport : ScriptableObject
    {
        /// <summary>빈칸에 넣을 수 있는 말 하나.</summary>
        [Serializable]
        public class Choice
        {
            [Tooltip("빈칸에 들어갈 말")]
            public string 말 = "";

            [Tooltip("이 단서를 얻어야 보기로 뜬다. 비우면 늘 뜬다 — " +
                     "조사해서 알아낸 것만 쓸 수 있게 하는 것이 이 칸의 뜻이다")]
            public string 필요단서 = "";

            [Tooltip("이것이 참값인가")]
            public bool 맞음 = false;

            [Tooltip("이 말을 고르면 판결이 이렇게 정해진다(None 이면 안 건드린다). " +
                     "처분 빈칸에만 쓴다 — 엄히 다스리면 Truth, 사정을 헤아리면 Mercy")]
            public Verdict 판결 = Verdict.None;

            [Tooltip("<b>쐐기</b>인가 — 이것 하나로 사람이 못 빠져나가는 증좌." +
                     "곁증좌만 대고는 엄히 다스릴 수 없다. 사람을 맞혀도 쐐기가 없으면 " +
                     "판결이 Mercy 로 내려앉는다 — 조선의 재판에서도 증거 없이 엄형은 못 했다." +
                     "증좌 빈칸에만 쓴다")]
            public bool 쐐기 = false;
        }

        /// <summary>한 줄. 빈칸 하나를 품는다.</summary>
        [Serializable]
        public class Blank
        {
            [Tooltip("빈칸 앞의 글")]
            [TextArea(1, 3)] public string 앞 = "";

            [Tooltip("빈칸 뒤의 글")]
            [TextArea(1, 3)] public string 뒤 = "";

            [Tooltip("이 줄을 누르면 말 목록 위에 뜨는 안내")]
            public string 물음 = "";

            public Choice[] 보기 = new Choice[0];
        }

        [Tooltip("어느 사건의 장계인가")]
        public CaseId 사건 = CaseId.Case1_Onggojip;

        [TextArea(2, 4)]
        [Tooltip("첫머리. 빈칸이 없는 고정된 글")]
        public string 머리말 = "臣이 옹진현에 이르러 살피니,";

        [Tooltip("첫째 빈칸은 <b>누구인가</b>라야 한다 — 그것만은 틀리면 판결이 통째로 뒤집힌다")]
        public Blank[] 빈칸 = new Blank[0];

        [TextArea(2, 4)]
        [Tooltip("맺음. 빈칸이 없는 고정된 글")]
        public string 맺음말 = "삼가 아뢰옵나이다.";

        [Header("뒷일 — 봉하고 나서 한 줄씩 지나간다")]
        [TextArea(2, 4)] [Tooltip("다 맞았을 때")]
        public string 참끝 = "";

        [TextArea(2, 4)] [Tooltip("사람은 맞았으나 증좌가 성글 때")]
        public string 반끝 = "";

        [TextArea(2, 4)] [Tooltip("사람을 틀렸을 때")]
        public string 헛끝 = "";

        [TextArea(1, 3)]
        [Tooltip("이문록에 오르는 한 줄. {판결} 을 쓰면 그 자리에 판결이 들어간다")]
        public string 이문록줄 = "옹진현 옹고집 — {판결}";
    }
}
