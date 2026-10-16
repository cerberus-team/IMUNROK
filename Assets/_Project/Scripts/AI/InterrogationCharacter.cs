using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 증거로 잠금 해제되는 사실 하나. clueKey(수첩 단서)를 제시하면 revealsInfo가 열린다.
    /// </summary>
    [System.Serializable]
    public class EvidenceGate
    {
        [Tooltip("<b>어느 막의 것인가.</b> 같은 단서에 두 줄을 적어 두고 하나는 1막, 하나는 2막으로 " +
                 "두면 같은 종이를 두 번 들이밀되 다른 대답을 듣는다 — 그것이 막이 넘어갔다는 뜻이다.\n" +
                 "1막의 甲은 「허튼 종이 쪼가리요」로 넘기고, 2막의 甲은 같은 종이 앞에서 말을 잃어야 한다")]
        public ActWhen act = ActWhen.두막다;

        [Tooltip("이 사실을 여는 수첩 단서의 key")]
        public string clueKey = "";

        [Tooltip("수첩/제시 목록에 보이는 단서 문구")]
        public string clueText = "";

        [Tooltip("<b>같이 쥐고 있어야 열리는 것들.</b> 쉼표로 여럿. 위 clueKey 와 <b>모두</b> 있어야 한다.\n\n" +
                 "혼자서는 아무것도 못 하는 단서가 있다. 아내가 「비린 것을 못 드시던 분인데 " +
                 "요즘은 게장을 청하십니다」 한 것만으로는 안주인의 말일 뿐이고, 물목기에 " +
                 "게장을 석 냥에 사들인 줄만으로는 그저 장 본 기록이다. <b>둘을 겹쳐야</b> " +
                 "사들여 놓고 못 먹는 척한다는 말이 선다.\n\n" +
                 "둘 중 <b>어느 것을 들이밀어도</b> 열린다 — 나머지를 쥐고만 있으면 된다")]
        public string alsoNeedsClueKeys = "";

        [Tooltip("이 게이트가 열리면 <b>무너짐 단계</b>를 적어도 이만큼으로 올린다(0~10, 0이면 안 건드림).\n" +
                 "게이트 개수로만 세면 게이트가 늘어날수록 한 자리의 무게가 줄어든다. " +
                 "정곡을 찌른 한 방은 그것 하나로 사람을 흔들어야 한다")]
        [Range(0, 10)]
        public int stageAtLeast = 0;

        [Tooltip("이 증거를 제시하면 인물이 털어놓는 사실")]
        [TextArea(2, 4)]
        public string revealsInfo = "";

        [Tooltip("<b>말로도 열리는 낱말.</b> 쉼표로 여럿(예: 아우,동생,형제). 한 낱말 안의 +는 둘 다 필요.\n" +
                 "수첩에서 들이밀지 않고 손으로 쳐 넣어도, 이 낱말이 들었고 위 단서를 " +
                 "<b>이미 쥐고 있으면</b> 열린다. 쥐지 않았으면 아무리 정확히 짚어도 안 열린다 — " +
                 "넘겨짚기는 조사가 아니다.\n비우면 들이밀어야만 열린다")]
        public string keywords = "";

        [Tooltip("<b>수첩 칸에 뜰 제목.</b> 비우면 아래 실토의 첫 토막을 잘라 쓴다(옛 방식). " +
                 "「[안주인의 증언] 서방님의 아우」처럼 <b>무엇에 관한 말인지</b>만 짧게 — " +
                 "말한 이는 수첩이 앞에 붙여 주므로 여기 또 적지 마라. 여덟 자 안이 좋다")]
        public string grantsClueTitle = "";

        [Tooltip("이 실토를 수첩에 <b>정황</b>으로 적을 때의 key(예: G05). " +
                 "비우면 적히지 않고 그 자리에서 붉게 한 번 지나갈 뿐이다.\n" +
                 "적어 두어야 다른 인물이 「어사가 아내의 증언을 쥐었는가」를 볼 수 있다")]
        public string grantsClueKey = "";

        [Tooltip("체크하면: 위 대사는 '발뺌'일 뿐 — 단서로 기록되지 않고 여러 번 반복 가능(1막 甲처럼 안 무너짐)")]
        public bool deflectionOnly = false;

        [Tooltip("이 증거를 제시할 때 뜨는 상황 그림(선택)")]
        public Texture2D clueImage;

        /// <summary>이 게이트가 요구하는 단서 모두(첫째가 clueKey). 하나면 하나만 돌아온다.</summary>
        public List<string> RequiredKeys()
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(clueKey)) list.Add(clueKey.Trim());
            if (!string.IsNullOrWhiteSpace(alsoNeedsClueKeys))
                foreach (var raw in alsoNeedsClueKeys.Split(','))
                {
                    var k = raw.Trim();
                    if (k.Length > 0 && !list.Contains(k)) list.Add(k);
                }
            return list;
        }
    }

    /// <summary>
    /// 추천 질문 하나(=대화 버튼). 플레이어가 고르면 어사가 이 질문을 하고, 인물이 답한다.
    /// grantsClueKey가 있으면 이 대화로 단서가 수첩에 기록된다(예: 甲의 자랑 → J02).
    /// </summary>
    [System.Serializable]
    public class TopicQuestion
    {
        [Tooltip("<b>어느 막의 물음인가.</b> 1막에만 통하는 물음이 있다 — 마음이 놓인 주인은 " +
                 "묻지 않아도 자랑을 흘리지만, 어사 앞에 끌려 나온 뒤에는 같은 물음에 입을 다문다.\n" +
                 "안 맞는 막에서는 이 물음이 줄에 아예 안 뜬다")]
        public ActWhen act = ActWhen.두막다;

        [Tooltip("버튼에 뜨는 질문(어사의 대사)")]
        public string question = "";

        [Tooltip("<b>단추를 안 눌러도 같은 곳에 닿는 낱말.</b> 쉼표로 여럿(예: 문서,장부,스무해). " +
                 "한 낱말 안의 +는 둘 다 필요(예: 왼팔+자국).\n" +
                 "손으로 쳐 넣은 말에 이 낱말이 들면 이 물음을 한 것으로 보고 같은 단서를 준다 — " +
                 "제 손으로 정곡을 짚은 사람이 단추를 누른 사람보다 빈손이면 손으로 칠 까닭이 없다.\n" +
                 "비우면 단추로만 열린다")]
        public string keywords = "";

        [Tooltip("목업(키 없을 때)에서 인물이 하는 대답. Gemini면 AI가 생성")]
        [TextArea(2, 4)]
        public string mockAnswer = "";

        [Tooltip("이 대화로 얻는 단서 key(선택). 비우면 단서 없음(분위기용)")]
        public string grantsClueKey = "";

        [Tooltip("그 단서의 수첩 문구 — <b>본문</b>이다. 칸에 들어갈 길이를 걱정하지 말고, " +
                 "들은 말을 <b>넉넉히</b> 적어라. 칸에 뜨는 것은 아래 제목이다")]
        [TextArea(2, 5)]
        public string grantsClueText = "";

        [Tooltip("<b>수첩 칸에 뜰 제목.</b> 비우면 위 문구의 첫 토막을 잘라 쓴다(옛 방식). " +
                 "「[마을 사람의 증언] 문 앞 사내의 정체」처럼 <b>무엇에 관한 말인지</b>만 " +
                 "짧게 — 말한 이는 수첩이 앞에 붙여 주므로 여기 또 적지 마라. 여덟 자 안이 좋다")]
        public string grantsClueTitle = "";

        [Tooltip("이 단서의 상황 그림(선택). 수첩 카드·증거 제시 때 뜸")]
        public Texture2D grantsClueImage;
    }

    /// <summary>
    /// 심문할 인물 한 명의 데이터. 사건 팀원이 코드 없이 인스펙터로 작성한다.
    /// (틀은 공통, 알맹이는 사건 — GameState/Journal과 같은 방식)
    ///
    /// - persona: 인물의 성격·아는 사실·말투 (AI에게 주는 지침이자, 목업의 성격)
    /// - evidenceGates: "이 증거를 제시받으면 이 사실을 털어놓는다" 규칙 목록
    ///
    /// 만드는 법: 프로젝트 창 우클릭 ▸ Create ▸ 이문록 ▸ 심문 캐릭터
    /// </summary>
    [CreateAssetMenu(fileName = "InterrogationCharacter", menuName = "이문록/심문 캐릭터")]
    public class InterrogationCharacter : ScriptableObject
    {
        public string characterName = "인물";

        [Tooltip("<b>제 입으로 대는 이름.</b> 비우면 위의 이름표를 그대로 쓴다. " +
                 "둘이 갈리는 자리가 있다. 진짜 옹덕구는 이름패가 「성난 사내」인데 " +
                 "— 손님은 아직 그가 누구인지 모르니까 — 정작 제 입으로는 「옹덕구」라 한다. " +
                 "이름패를 그대로 물려 주면 「내 이름은 성난 사내요」라고 답한다")]
        public string spokenName = "";

        [Tooltip("2막(어사임을 밝힌 뒤) 자막·이름패에 쓰는 이름. 비우면 characterName. " +
                 "1막의 「성난 사내」는 과객 눈에 그리 보인다는 뜻이라, 관아에서는 「옹덕구(乙)」로 불러야 한다")]
        public string nameAct2 = "";

        /// <summary>지금 막에 맞는 이름패. 수첩의 말한 이(speaker)는 늘 characterName 으로 남긴다 — 들이밀기 판정이 그것을 본다.</summary>
        public string ShownName
        {
            get
            {
                var gs = GameState.Instance;
                return gs != null && gs.IdentityRevealed && !string.IsNullOrWhiteSpace(nameAct2) ? nameAct2 : characterName;
            }
        }

        [Tooltip("<b>제 입으로 절대 먼저 꺼내지 않을 낱말.</b> 쉼표로 여럿. " +
                 "성격에 비밀을 적어 두면 AI가 그것을 <b>아는 사실</b>로 읽고 이름을 물었을 때 " +
                 "그대로 댄다 — 가짜 옹덕구에게 이름을 물었더니 「복동이오」라 답했다. " +
                 "사건이 통째로 새는 자리다. 여기 적은 낱말은 증거로 들이밀기 전까지 안 나온다")]
        public string secretWords = "";

        [Tooltip("이 인물이 속한 사건(수첩 단서 범위와 연결)")]
        public CaseId caseId = CaseId.Case1_Onggojip;

        [Tooltip("인물의 성격·아는 사실·말투. 실제 AI의 시스템 프롬프트가 되고, 목업의 성격 기준이 됨.\n" +
                 "아래 1막/2막 성격이 적혀 있으면 그쪽이 먼저다 — 이 칸은 둘 다 비었을 때 쓰는 옛 자리")]
        [TextArea(4, 12)]
        public string persona = "";

        [Header("막마다 다른 성격")]
        [Tooltip("<b>1막 · 잠행</b> — 묻는 이가 하룻밤 청하는 과객으로 보인다. 경계가 풀려 있고, " +
                 "그래서 흘리는 것이 있다. 비우면 아래 공통 성격을 쓴다")]
        [TextArea(6, 24)]
        public string personaAct1 = "";

        [Tooltip("<b>2막 · 공개</b> — 묻는 이가 어사임을 안다. 방어하고, 증거 앞에서만 물러난다. " +
                 "비우면 1막 것을, 그것도 비면 공통 성격을 쓴다")]
        [TextArea(6, 24)]
        public string personaAct2 = "";

        [Header("무너지는 조건")]
        [Tooltip("이 인물이 무너지려면 어사가 <b>쥐고 있어야</b> 하는 단서 key. 쉼표로 여럿(예: J13,G03).\n\n" +
                 "<b>왜 조건을 자료로 두는가</b>: 무너짐을 AI 혼자 판단하게 두면 말을 잘 지어내는 사람에게만 " +
                 "사건이 열린다. 반대로 <b>정확한 문구</b>를 맞춰야 열리면 「복동아」와 「네 이름 복동이지?」가 " +
                 "다른 말이 되어 답답해진다. 그래서 두 겹으로 나눈다 — <b>열쇠를 쥐었는가</b>는 여기서 " +
                 "게임이 정하고, <b>정곡을 찔렀는가</b>는 AI가 뜻으로 읽는다.\n\n" +
                 "<b>비우면 이 인물은 무너지지 않는다</b> — 무너질 것이 없는 사람이 그렇다. " +
                 "진짜 옹덕구는 무너질 거짓이 없고, 마름은 아는 것의 끝이 소문이며, 서리는 심문 대상이 아니다.\n" +
                 "여기가 「조건 없음(언제든 무너짐)」이면 실수로 비워 둔 인물이 첫 마디에 통째로 " +
                 "실토해 사건이 새어 나간다. 빈칸은 <b>잠긴 쪽</b>으로 읽는다")]
        public string breakOnClues = "";

        [Header("마치기 전에 한 번 묻기")]
        [Tooltip("1막에서 「이만 마치겠소」를 누르면 이 말을 띄우고 한 번 더 눌러야 마친다. 비우면 곧장 마친다. " +
                 "甲 과의 자리를 파하면 밤이 되고 甲 은 들어가 버린다 — 그에게서만 듣는 말(J02·J03·J14)을 영영 놓친다")]
        [TextArea(2, 3)] public string finishWarnAct1 = "";

        [Header("자백 — 무너짐을 상태로 남긴다")]
        [Tooltip("위 열쇠를 다 쥔 채 이 낱말(쉼표로 여럿)로 정곡을 찌르거나, 열쇠 가운데 하나를 들이밀면 " +
                 "<b>자백으로 친다</b>. 여태 무너짐은 AI 대사로만 끝나 수첩·장계·결말 어디에도 안 남았다(2026-09-15)")]
        public string confessKeywords = "";
        [Tooltip("자백을 수첩에 적을 key. 비우면 자백 칸이 없다")]
        public string confessClueKey = "";
        public string confessClueTitle = "";
        [TextArea(2, 4)] public string confessClueText = "";
        [Tooltip("자백하는 사람의 말(목업이 쓴다. Gemini 는 이 뜻으로 제 말을 짓는다)")]
        [TextArea(2, 4)] public string confessLine = "";

        [Header("목업 말투 — 열쇠가 없거나 네트워크가 끊겼을 때")]
        [Tooltip("1막 회피 말. 비우면 공통 말투")]
        [TextArea(1, 2)] public string[] mockDeflectAct1 = new string[0];
        [Tooltip("2막 회피 말. 비우면 공통 말투(소인…) — 양반 행세하는 이에게는 꼭 따로 적을 것")]
        [TextArea(1, 2)] public string[] mockDeflectAct2 = new string[0];

        /// <summary>
        /// 지금 막에 맞는 성격을 고른다. 2막 것이 비어 있으면 1막 것을, 그것도 비면 옛 칸을 쓴다 —
        /// 아직 안 채운 인물이 말을 잃지 않게.
        /// </summary>
        public string PersonaFor(bool identityRevealed)
        {
            if (identityRevealed && !string.IsNullOrWhiteSpace(personaAct2)) return personaAct2;
            if (!string.IsNullOrWhiteSpace(personaAct1)) return personaAct1;
            if (!string.IsNullOrWhiteSpace(persona)) return persona;
            return "너는 심문받는 인물이다.";
        }

        /// <summary>무너지는 데 필요한 단서 key 들. 비었으면 조건이 없다.</summary>
        public string[] BreakKeys()
        {
            if (string.IsNullOrWhiteSpace(breakOnClues)) return System.Array.Empty<string>();
            var raw = breakOnClues.Split(',');
            var list = new List<string>();
            foreach (var r in raw)
            {
                var t = r.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }

        [Tooltip("심문 시작 시 인물의 첫 대사. <b>1막 것</b>으로 적는다 — 2막 것은 아래 칸에")]
        [TextArea(2, 4)]
        public string openingLine = "무슨 일로 오셨소?";

        [Tooltip("<b>2막 · 공개</b>의 첫 대사. 비우면 위의 것을 그대로 쓴다.\n" +
                 "성격(persona)은 막을 갈라 두면서 첫 대사는 한 벌뿐이라, 어사 앞에 끌려 나온 甲이 " +
                 "1막의 주인처럼 「이 밤중에 어인 일이시오」 하고 맞았다 — 막이 넘어간 것이 " +
                 "말에서 안 드러나면 넘어간 것이 아니다")]
        [TextArea(2, 4)]
        public string openingLineAct2 = "";

        [Tooltip("물러났다가 <b>다시 불려 나왔을 때</b>의 첫 대사. 비우면 첫 대사를 그대로 쓴다.\n" +
                 "동헌처럼 불렀다 물렸다 하는 자리에서 쓴다 — 두 번째로 끌려 나온 사람이 " +
                 "처음처럼 '무슨 일로 오셨소' 하면 그 사이의 일이 다 없던 것이 된다")]
        [TextArea(2, 4)]
        public string recallLine = "";

        [Tooltip("심문을 끝내고 돌아설 때 인물이 등 뒤로 던지는 한 마디. 비우면 그냥 닫힌다.\n" +
                 "무엇을 물었든 반드시 듣게 되므로, 다음에 갈 곳을 흘리는 자리로 쓰기 좋다")]
        [TextArea(2, 4)]
        public string closingLine = "";

        [Tooltip("<b>2막 · 공개</b>의 마지막 한 마디. 비우면 위의 것을 그대로 쓴다.\n" +
                 "1막의 「볼일이 있어 이만 물러가겠소」는 손님을 물리는 주인의 말이다 — " +
                 "어사 앞에 불려 나온 자가 제 볼일을 핑계로 물러갈 수는 없다")]
        [TextArea(2, 4)]
        public string closingLineAct2 = "";

        /// <summary>지금 막에 맞는 첫 대사. 2막 것이 비어 있으면 1막 것을 쓴다.</summary>
        public string OpeningFor(bool identityRevealed)
            => identityRevealed && !string.IsNullOrWhiteSpace(openingLineAct2) ? openingLineAct2 : openingLine;

        [Tooltip("<b>이 단서를 얻고 나면</b> 위의 마지막 한 마디를 그만한다. 비우면 늘 한다. " +
                 "마지막 한 마디는 대개 <b>다음에 갈 곳을 흘리는</b> 자리다 — 乙 이 " +
                 "「이 삼복더위에 아궁이는 왜 때는 것이오」 하는 것이 그렇다. 그런데 " +
                 "귀띔은 <b>알아듣고 나면 끝나야</b> 한다. 이미 아궁이를 헤집어 재를 " +
                 "본 사람에게 같은 말을 또 하면, 그것은 귀띔이 아니라 고장난 축음기다")]
        public string closingLineUntilClue = "";

        [Tooltip("그 단서를 얻은 뒤에 대신 할 말. 비우면 <b>아무 말 없이</b> 돌아선다")]
        [TextArea(2, 4)]
        public string closingLineAfter = "";

        [Tooltip("<b>한 번만 하고 그만한다.</b> 귀띔이 <b>연출</b>로 못 박히는 자리가 있다 — " +
                 "乙이 아궁이를 들먹이면 카메라가 지붕 위 연기로 끌려간다(ForcedGaze). " +
                 "그 연출은 제 손으로 한 번만 도는데 말은 닫을 때마다 또 나서, 같은 귀띔을 " +
                 "세 번 듣고도 연기는 한 번만 보았다. <b>보여 준 다음의 말은 말이 아니라 " +
                 "잔소리다</b>. 체크하면 한 번 한 뒤로는 아래의 되풀이할 말로 갈아탄다. " +
                 "위의 closingLineUntilClue 와 함께 두어도 된다 — 단서를 얻었으면 그쪽이 먼저다")]
        public bool closingLineOnce = false;

        [Tooltip("한 번 한 뒤로 대신 할 말. <b>비우면 아무 말 없이 돌아선다</b>. " +
                 "여기는 <b>아무 데도 가리키지 않는</b> 말을 적는다 — 갈 곳을 또 가리키면 " +
                 "한 번만 하기로 한 뜻이 없어진다")]
        [TextArea(2, 4)]
        public string closingLineAgain = "";

        // <b>한 번 했다는 것을 어디에 적어 두는가.</b> 자산(ScriptableObject)에 적으면
        // 에디터에서 그 값이 그대로 굳어, 다음에 켜도 이미 한 것이 된다. 그래서 판이
        // 도는 동안만 사는 자리에 둔다 — 놀이를 다시 시작하면 처음으로 돌아간다.
        private static readonly HashSet<string> _saidOnce = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetSaidLines() { _saidOnce.Clear(); }

        /// <summary>이 말을 <b>실제로 내보낸</b> 뒤에 부른다 — 자막에 떴을 때만이다.</summary>
        public void NoteClosingSaid()
        {
            if (closingLineOnce) _saidOnce.Add(name + "|" + characterName);
        }

        private bool SaidClosingAlready => _saidOnce.Contains(name + "|" + characterName);

        /// <summary>
        /// 지금 막에 맞는 마지막 한 마디.
        ///
        /// <see cref="closingLineUntilClue"/> 가 적혀 있고 그 단서를 이미 얻었으면
        /// <see cref="closingLineAfter"/> 로 갈아탄다 — 귀띔은 한 번 알아들으면 끝이다.
        /// </summary>
        public string ClosingFor(bool identityRevealed)
        {
            if (identityRevealed && !string.IsNullOrWhiteSpace(closingLineAct2))
            {
                // <b>2막의 마지막 말이 따로 적혀 있으면 그쪽이 먼저다.</b>
                //
                // 갈아타기는 <b>1막의 일</b>이다. 아궁이 귀띔은 그 밤의 것이고, 관아에
                // 불려 나온 자가 등 뒤로 던지는 말은 이미 다른 말로 적혀 있다. 여기서
                // 귀띔 쪽을 먼저 보면, 2막의 乙 이 「재까지 보셨소?」로 물러가면서
                // 어제 밤의 말을 되풀이한다.
                return closingLineAct2;
            }
            string line = closingLine;

            // <b>단서를 얻었으면 그쪽이 먼저다.</b> 이미 재를 헤집어 본 사람에게는
            // 귀띔이 아니라 확인을 해 주는 것이 맞다.
            if (!string.IsNullOrWhiteSpace(closingLineUntilClue))
            {
                var j = Journal.Instance;
                if (j != null && j.HasClue(caseId, closingLineUntilClue.Trim()))
                    return closingLineAfter;
            }

            // 단서는 아직 없어도, <b>한 번 했으면 그만한다</b>. 귀띔에 딸린 연출은
            // 이미 한 번 돌았고, 말만 남아 되풀이되는 중이다.
            if (closingLineOnce && SaidClosingAlready) return closingLineAgain;

            return line;
        }

        [Tooltip("<b>뜰에서 붙잡고 물었을 때</b> 물리는 한 마디. 비우면 부품에 적힌 두루뭉술한 것을 쓴다.\n" +
                 "여기가 비어 있으면 다섯이 <b>같은 말</b>로 물린다 — 갓 쓴 양반도, 늙은 종도, " +
                 "양반가 아녀자도 똑같이 「소인이 어찌…」 한다. 안 되는 까닭은 하나여도 " +
                 "<b>물리는 말투는 저마다의 처지</b>다. 종은 매가 무서워 물리고, 양반은 체면 때문에 " +
                 "물리고, 아녀자는 낯을 들 수 없어 물린다 — 그 셋이 같은 문장을 쓰면 " +
                 "이 다섯은 이름만 다른 한 사람이 된다")]
        [TextArea(2, 4)]
        public string calledUpLine = "";

        [Tooltip("추천 질문(대화 버튼). 플레이어가 고를 수 있는 질문들")]
        public List<TopicQuestion> topics = new List<TopicQuestion>();

        [Tooltip("증거를 제시하면 열리는 사실들")]
        public List<EvidenceGate> evidenceGates = new List<EvidenceGate>();
    }
}
