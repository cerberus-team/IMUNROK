using System.Collections.Generic;

namespace IMUNROK.Gyeonu
{
    /// <summary>제작자 표식 — 4종. 장부 판의 **세로 줄(열)** 을 가른다.</summary>
    public enum LedgerMark { 매화, 학, 구름, 거북 }

    /// <summary>처리 장소 — 3종. 장부 판의 **가로 줄(행)** 을 가른다.</summary>
    public enum LedgerSite { 관아, 외고, 후고 }

    /// <summary>놓을 때 나는 소리를 가르는 재질 구분.</summary>
    public enum LedgerTouch { 쇠, 유리, 종이 }

    /// <summary>기물 한 점의 정의.</summary>
    public class LedgerPieceDef
    {
        /// <summary>1~12. 임포트한 에셋 폴더 이름과 같다.</summary>
        public int id;
        public string name;
        public LedgerMark mark;
        public LedgerSite site;
        public LedgerTouch touch;

        /// <summary>확대 조사 화면에 뜨는 특징 줄. 첫 줄은 늘 제작자 표식이다.</summary>
        public string[] traits;

        /// <summary>2단계 정답이면 0·1·2 (아버지 기록 세 줄 중 몇 번째), 아니면 -1.</summary>
        public int answerOf = -1;

        /// <summary>3단계에 드러나는 처리 정보.</summary>
        public string handler;      // 처리인
        public string destination;  // 행선지

        public bool IsAnswer => answerOf >= 0;
    }

    /// <summary>아버지의 검수 기록(C1) 한 줄 — 2단계에서 대조할 대상.</summary>
    public class LedgerRecordDef
    {
        public string name;         // 혼상 외환 연결쇠
        public LedgerMark mark;
        public string[] traits;     // 기록에 적힌 특징 (온전한 문장)

        /// <summary>
        /// 판에 새겨 넣을 **줄인 문구**. 대조대 한 칸의 너비가 0.235m뿐이라 온전한 문장은
        /// 글자가 뭉개진다 — 판에는 이것만 적고, 온전한 기록은 소지품(C1) 상세에서 읽는다.
        /// </summary>
        public string[] shortTraits;
    }

    /// <summary>
    /// 종막 퍼즐(서고 장부)의 **모든 진실이 적힌 한 자리** (2026-08-24).
    ///
    /// ■ 왜 코드에 박아 두는가
    ///   12기물·7라벨·3기록은 기획이 확정한 고정 자료다. SO로 흩어 놓으면 정답표가 열두 파일에
    ///   나뉘어 "어느 줄이 왜 정답인가"를 한눈에 검산할 수 없다. 여기 한 표에 모아 두면
    ///   빌더·판정·확대 조사가 같은 것을 보고, 기획안과 나란히 놓고 대조할 수 있다.
    ///
    /// ■ 판정은 이름을 보지 않는다
    ///   1단계는 <see cref="LedgerPieceDef.mark"/>·<see cref="LedgerPieceDef.site"/> 만,
    ///   2단계는 <see cref="LedgerPieceDef.answerOf"/> 만 본다. 기물 이름은 화면에 안 뜬다 —
    ///   기획이 요구한 "이름보다 표식, 형태, 남은 흔적"이 곧 자료 구조다.
    /// </summary>
    public static class LedgerData
    {
        public const int Cols = 4;   // 제작자 표식 4종
        public const int Rows = 3;   // 처리 장소 3종

        /// <summary>
        /// ⚠️ 기획안 11차 표의 정답 1 기록은 「결합부 3개」로 적혀 있으나, 그러면
        /// 세 후보(1=3개 / 2=2개 / 3=4개) 중 정답으로 지정된 3번이 기록과 어긋나고
        /// 오히려 오답인 1번이 일치해 **논리로 풀 수 없다**. 실물 모델의 고정쇠가 4개이므로
        /// 기록을 4개로 잡았다. 기획안대로 되돌리려면 이 상수만 3으로 바꾸면 된다.
        /// </summary>
        public const int 기록_결합부 = 4;

        /// <summary>아버지의 검수 기록 세 줄 — 2단계 대조대의 왼쪽에 펼쳐진다.</summary>
        public static readonly LedgerRecordDef[] Records =
        {
            new LedgerRecordDef
            {
                name = "혼상 외환 연결쇠",
                mark = LedgerMark.매화,
                traits = new[]
                {
                    "매화 표식",
                    "안쪽 테두리에 평행한 홈 2줄",
                    "결합부 " + 기록_결합부 + "개",
                    "오른쪽 아래에 눌린 자국",
                },
                shortTraits = new[] { "매화", "홈 두 줄", "결합부 " + 기록_결합부, "눌림 오른쪽 아래" },
            },
            new LedgerRecordDef
            {
                name = "혼천의 방위 회전축",
                mark = LedgerMark.학,
                traits = new[]
                {
                    "학 표식",
                    "축 끝이 팔각",
                    "중앙보다 약간 왼쪽에 붉은 산화",
                    "끝 근처에 짧은 세로 흠",
                },
                shortTraits = new[] { "학", "끝이 팔각", "붉은 녹 중앙 왼쪽", "끝에 세로 흠" },
            },
            new LedgerRecordDef
            {
                name = "관측경 수정편",
                mark = LedgerMark.구름,
                traits = new[]
                {
                    "구름 표식",
                    "빛에 비추면 푸른빛",
                    "한쪽 끝이 초승달 모양으로 깨짐",
                    "아래에 작은 기포 1개",
                },
                shortTraits = new[] { "구름", "푸른빛", "초승달 깨짐", "기포 하나" },
            },
        };

        /// <summary>
        /// 12기물. 배열 순서가 곧 폴더 번호(1~12) 순이다.
        ///
        /// 설계 기준 배열 (게임에서는 무작위로 섞어 시작한다):
        /// <code>
        ///        매화        학          구름        거북
        ///  관아   1 궤짝쇠    4 대문축    7 등잔유리  10 난간핀
        ///  외고   2 문짝고리  5 빗장축    8 창호유리  11 용마루축
        ///  후고   3 폐환★    6 축재★    9 수정편★  12 장식축
        /// </code>
        /// </summary>
        public static readonly LedgerPieceDef[] Pieces =
        {
            new LedgerPieceDef
            {
                id = 1, name = "궤짝 테두리쇠",
                mark = LedgerMark.매화, site = LedgerSite.관아, touch = LedgerTouch.쇠,
                handler = "윤", destination = "관아",
                traits = new[]
                {
                    "매화 표식",
                    "안쪽 테두리에 평행한 홈 2줄",
                    "결합부 3개",
                    "왼쪽 위에 눌린 자국",
                },
            },
            new LedgerPieceDef
            {
                id = 2, name = "문짝 고리",
                mark = LedgerMark.매화, site = LedgerSite.외고, touch = LedgerTouch.쇠,
                handler = "박", destination = "외고",
                traits = new[]
                {
                    "매화 표식",
                    "안쪽 테두리에 평행한 홈 2줄",
                    "결합부 2개",
                    "오른쪽 아래에 눌린 자국",
                },
            },
            new LedgerPieceDef
            {
                id = 3, name = "수선용 폐환",
                mark = LedgerMark.매화, site = LedgerSite.후고, touch = LedgerTouch.쇠,
                answerOf = 0, handler = "최", destination = "후고",
                traits = new[]
                {
                    "매화 표식",
                    "안쪽 테두리에 평행한 홈 2줄",
                    "결합부 4개",
                    "오른쪽 아래에 눌린 자국",
                },
            },
            new LedgerPieceDef
            {
                id = 4, name = "대문 회전축",
                mark = LedgerMark.학, site = LedgerSite.관아, touch = LedgerTouch.쇠,
                handler = "이", destination = "관아",
                traits = new[]
                {
                    "학 표식",
                    "축 끝이 팔각",
                    "오른쪽 끝에 붉은 산화",
                    "세로 흠 없음",
                },
            },
            new LedgerPieceDef
            {
                id = 5, name = "창고 빗장축",
                mark = LedgerMark.학, site = LedgerSite.외고, touch = LedgerTouch.쇠,
                handler = "윤", destination = "외고",
                traits = new[]
                {
                    "학 표식",
                    "축 끝이 팔각",
                    "붉은 산화 없음",
                    "끝 근처에 짧은 세로 흠",
                },
            },
            new LedgerPieceDef
            {
                id = 6, name = "수선용 축재",
                mark = LedgerMark.학, site = LedgerSite.후고, touch = LedgerTouch.쇠,
                answerOf = 1, handler = "최", destination = "후고",
                traits = new[]
                {
                    "학 표식",
                    "축 끝이 팔각",
                    "중앙보다 약간 왼쪽에 붉은 산화",
                    "끝 근처에 짧은 세로 흠",
                },
            },
            new LedgerPieceDef
            {
                id = 7, name = "등잔 유리편",
                mark = LedgerMark.구름, site = LedgerSite.관아, touch = LedgerTouch.유리,
                handler = "박", destination = "관아",
                traits = new[]
                {
                    "구름 표식",
                    "빛에 비추면 푸른빛",
                    "한쪽 끝이 둥근 모양으로 깨짐",
                    "아래에 작은 기포 1개",
                },
            },
            new LedgerPieceDef
            {
                id = 8, name = "창호 유리편",
                mark = LedgerMark.구름, site = LedgerSite.외고, touch = LedgerTouch.유리,
                handler = "이", destination = "외고",
                traits = new[]
                {
                    "구름 표식",
                    "빛에 비추어도 무색",
                    "한쪽 끝이 초승달 모양으로 깨짐",
                    "아래에 작은 기포 1개",
                },
            },
            new LedgerPieceDef
            {
                id = 9, name = "파손 수정편",
                mark = LedgerMark.구름, site = LedgerSite.후고, touch = LedgerTouch.유리,
                answerOf = 2, handler = "최", destination = "후고",
                traits = new[]
                {
                    "구름 표식",
                    "빛에 비추면 푸른빛",
                    "한쪽 끝이 초승달 모양으로 깨짐",
                    "아래에 작은 기포 1개",
                },
            },
            new LedgerPieceDef
            {
                id = 10, name = "난간 회전핀",
                mark = LedgerMark.거북, site = LedgerSite.관아, touch = LedgerTouch.쇠,
                handler = "윤", destination = "관아",
                traits = new[]
                {
                    "거북 표식",
                    "머리가 둥근 원반",
                    "가장자리에 얕은 이 자국",
                    "산화 없음",
                },
            },
            new LedgerPieceDef
            {
                id = 11, name = "지붕 용마루축",
                mark = LedgerMark.거북, site = LedgerSite.외고, touch = LedgerTouch.쇠,
                handler = "박", destination = "외고",
                traits = new[]
                {
                    "거북 표식",
                    "축 끝이 국화 모양",
                    "몸통에 감아 묶은 자국",
                    "산화 없음",
                },
            },
            new LedgerPieceDef
            {
                id = 12, name = "폐기 장식축",
                mark = LedgerMark.거북, site = LedgerSite.후고, touch = LedgerTouch.쇠,
                handler = "한", destination = "후고",
                traits = new[]
                {
                    "거북 표식",
                    "몸통이 휘고 한쪽이 그을림",
                    "속이 비어 뚫림",
                    "산화 없음",
                },
            },
        };

        // ── 조회 ─────────────────────────────────────────────

        public static LedgerPieceDef Piece(int id)
        {
            foreach (var p in Pieces) if (p.id == id) return p;
            return null;
        }

        /// <summary>정답 세 점을 기록 순서(0·1·2)대로.</summary>
        public static IEnumerable<LedgerPieceDef> Answers()
        {
            for (int i = 0; i < Records.Length; i++)
                foreach (var p in Pieces) if (p.answerOf == i) { yield return p; break; }
        }

        /// <summary>
        /// 처리 장소가 남긴 **눈에 보이는 흔적**. 1단계는 이것과 제작자 표식만 보고 가른다.
        /// </summary>
        public static string SiteTrace(LedgerSite site)
        {
            switch (site)
            {
                case LedgerSite.관아: return "붉은 관인이 찍힌 종이 꼬리표";
                case LedgerSite.외고: return "짚끈으로 열십자 묶고 작은 목패";
                default: return "검은 먹점과 먹줄, 반출패 없음";
            }
        }

        /// <summary>
        /// 확대 조사 화면에 적을 줄 — **단계가 무엇을 보여 줄지 정한다** (2026-08-25).
        ///
        /// 1단계에서 미세 특징(홈 개수·결합부·눌린 자국·녹 위치·깨짐·기포)까지 적어 주면
        /// 아직 손에 쥐지도 않은 아버지 기록의 답을 미리 흘리는 셈이 된다. 그래서 1단계에는
        /// **분류에 필요한 것만** — 만든 이의 표식과 보내진 곳의 흔적 — 두 줄만 적는다.
        /// </summary>
        public static string[] TraitsFor(LedgerPieceDef def, bool fine)
        {
            if (def == null) return null;
            if (fine) return def.traits;
            return new[] { def.traits[0], SiteTrace(def.site) };
        }

        /// <summary>라벨 7종의 표시 문구 — 열 4개 다음에 행 3개.</summary>
        public static string LabelText(bool isMark, int index)
            => isMark ? ((LedgerMark)index).ToString() : ((LedgerSite)index).ToString();

        // ── 선아의 메모 ──────────────────────────────────────

        public const string 메모_1단계 =
            "수령의 기록은 두 기준으로 기물을 나누었다. 하나는 만든 이의 표식, 하나는 보내진 곳.\n" +
            "밖으로 나가지 않은 것은 관인이 남았고, 외고로 간 것은 묶여 목패가 달렸다.\n" +
            "반출패 없이 먹으로만 표시된 것들은 후고로 돌린 듯하다.\n" +
            "일곱 표찰과 열두 기물을 제자리로 돌려놓을 것.";

        public const string 메모_2단계 =
            "장부의 배열은 맞았다. 이제 아버지의 기록과 같은 물건을 찾을 것.\n" +
            "이름보다 표식, 형태, 남은 흔적을 볼 것.";

        public const string 문구_3단계 =
            "서로 다른 세 기물이 같은 손을 거쳐 같은 곳으로 갔다면, 그곳에 남은 것이 있을 것이다.";
    }
}
