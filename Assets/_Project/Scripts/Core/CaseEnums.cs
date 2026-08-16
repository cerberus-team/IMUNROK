namespace IMUNROK.Common
{
    /// <summary>
    /// 세 개의 사건을 식별하는 값.
    /// 폴더/어셈블리 매핑: Case1=Onggojip, Case2=Seocheon, Case3=Gyeonu
    /// (아래 enum 값과 반드시 일치시킬 것 — 어긋나면 씬·단서가 다른 사건에 붙는다)
    /// </summary>
    public enum CaseId
    {
        /// <summary>제1사건: 누가 진짜인가 (옹고집전 모티브)</summary>
        Case1_Onggojip = 0,

        /// <summary>제2사건: 네 번째 환생자 (서천꽃밭)</summary>
        Case2_Seocheon = 1,

        /// <summary>제3사건: 칠석 실종 사건 (견우직녀)</summary>
        Case3_Gyeonu = 2,
    }

    /// <summary>
    /// 한 사건의 진행 상태.
    /// 사건판 큐브 색상과 직접 연결됨(NotStarted=회색, InProgress=주황, Completed=금색).
    /// </summary>
    public enum CaseStatus
    {
        /// <summary>아직 시작하지 않음</summary>
        NotStarted = 0,

        /// <summary>사건 씬에 들어갔으나 아직 판결 전</summary>
        InProgress = 1,

        /// <summary>판결까지 내려 완료된 상태</summary>
        Completed = 2,
    }

    /// <summary>
    /// 한 사건에 대해 플레이어가 내린 판결.
    /// 엔딩(복명)에서 이 세 판결의 "경향"을 읽어 왕의 총평이 분기됨.
    /// </summary>
    public enum Verdict
    {
        /// <summary>아직 판결하지 않음</summary>
        None = 0,

        /// <summary>진실대로 — 설화 뒤의 사실을 그대로 고함</summary>
        Truth = 1,

        /// <summary>정상참작 — 사정을 헤아려 관대히 처결</summary>
        Mercy = 2,

        /// <summary>甲 인정 — 가짜(설화가 덮은 쪽)를 진짜로 인정</summary>
        AcceptFake = 3,
    }
}
