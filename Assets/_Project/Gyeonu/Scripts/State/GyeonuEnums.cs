namespace IMUNROK.Gyeonu
{
    // ─────────────────────────────────────────────────────────────
    //  제3사건 《성하리 칠석 실종 사건》 — 상태 식별자 모음
    //  최종기획안 11차 「34. 데이터 구조」를 그대로 옮긴 것.
    //  ⚠️ 값(숫자)을 바꾸면 세이브 호환이 깨진다. 뒤에 덧붙이기만 할 것.
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 단서 21종. 코드가 곧 소지품 <see cref="InventoryItem.itemId"/> 와 같다(A1·C4 …).
    /// A = 전설과 옛길 / B = 올해의 실종 / C = 진범.
    /// ⚠️ A5(끊긴 옛길)는 2026-09-10 기획에서 뺐다 — 오브젝트가 없고 보조 단서라 진행에 영향이 없다.
    ///    번호는 그대로 둔다(A6=5, A7=6). 옛 세이브의 "A5"는 읽을 때 조용히 버려진다.
    /// </summary>
    public enum ClueId
    {
        A1 = 0, A2 = 1, A3 = 2, A4 = 3, A6 = 5, A7 = 6,
        B1 = 10, B2, B3, B4, B5, B6, B7,
        C1 = 20, C2, C3, C4, C5, C6, C7, C8,
    }

    /// <summary>모순 2종. M3(도망쳤다는 말)는 기획에서 삭제됐다.</summary>
    public enum ContradictionId
    {
        /// <summary>수령의 알리바이 — 수령 진술 ↔ 상인 C6</summary>
        M1 = 0,
        /// <summary>죄인이 된 검수관 — 주모 진술 ↔ 어머니 C7</summary>
        M2 = 1,
    }

    /// <summary>대화 상대. 무례 누적·신분 소문 판정의 키.</summary>
    public enum NpcId
    {
        Gyeonu = 0,             // 올해의 견우
        Magistrate = 1,         // 수령 (진범)
        Jumo = 2,               // 주모
        FestivalMerchant = 3,   // 칠석제 상인
        Child01 = 4,
        Child02 = 5,
        Child03 = 6,
        Mother = 7,             // 최초의 직녀 어머니
        FirstGyeonu = 8,
        FirstJiknyeo = 9,
        Seona = 10,
        /// <summary>이름 없는 마을 사람 — 소문이 퍼지는 쪽으로 취급한다.</summary>
        Villager = 11,
    }

    /// <summary>막(幕). 진행 위치를 한 값으로 요약한다.</summary>
    public enum Act
    {
        Village = 0,        // 1막 — 마을 조사와 견우 신뢰
        Observatory = 1,    // 2막 — 오작교와 관측실
        GyeonuVillage = 2,  // 3막 — 견우마을
        Gwana = 3,          // 종막 — 관아와 서고
        Ending = 4,
    }

    /// <summary>
    /// 시간대 3단계. 기존 <see cref="GyeonuWorld.Night"/>(bool)는
    /// <c>Day가 아니면 밤</c> 으로 이 값에 얹혀 있다 — 옛 코드는 그대로 돈다.
    /// </summary>
    public enum TimeOfDay
    {
        Day = 0,
        /// <summary>초밤 — 주모·아이들·상인 아직 있음. 견우는 선아 집·은하담 탐색</summary>
        EarlyNight = 1,
        /// <summary>늦은 밤 — 마을 NPC 퇴장. 관아 잠입 가능</summary>
        LateNight = 2,
    }

    public enum Weather
    {
        Clear = 0,
        Rain = 1,
    }

    /// <summary>비가역 임계 이벤트. 한 번 발화하면 되돌아가지 않는다.</summary>
    public enum Threshold
    {
        Trust40 = 0,   // 선아 집 열쇠
        Trust70 = 1,   // 타공 지도 + 진짜 증언, 날씨가 비로
        Alert40 = 2,   // 집무실 장부 일부 소각
        Alert70 = 3,   // 은닉처 점검 결심 — 남은 밤 2회
        Alert100 = 4,  // 서고를 먼저 정리 — '전설의 완성' 고정
    }

    /// <summary>
    /// 견우와의 대화 한 마디의 등급. AI 판정기가 붙기 전까지는
    /// 디버그 창·대화 스크립트가 직접 넣어 준다.
    /// </summary>
    public enum TalkTone
    {
        /// <summary>호의 +5, 최대 3회</summary>
        Favor = 0,
        /// <summary>중립 0, 무제한</summary>
        Neutral = 1,
        /// <summary>압박 −5, 최대 3회 — 추리에 필요한 질문이므로 낮게 유지한다</summary>
        Pressure = 2,
        /// <summary>모욕 −15, 매회. 3회면 신뢰도 0 고정 + 핵심 대화 잠김</summary>
        Insult = 3,
    }

    /// <summary>수령에게 무엇을 캐물었는가 — 경계도 가산의 갈래.</summary>
    public enum AlertTopic
    {
        /// <summary>일반 칠석제·마을 질문 — 0점</summary>
        Ordinary = 0,
        /// <summary>실종 사건을 직접 캐묻기 — +5, 최대 2회</summary>
        Disappearance = 1,
        /// <summary>선아 아버지 사건 언급 — +15, 1회</summary>
        FatherCase = 2,
        /// <summary>오작교 또는 암문 언급 — +10, 1회</summary>
        BridgeOrGate = 3,
        /// <summary>서고를 알고 있다는 언급 — +20, 1회</summary>
        Archive = 4,
        /// <summary>M1 모순을 직접 지적 — +15, 1회</summary>
        ContradictionM1 = 5,
    }

    /// <summary>저널에 보여 줄 증거도 단계. 숫자는 끝까지 감춘다.</summary>
    public enum EvidenceStage
    {
        Insufficient = 0,   // 정황 부족
        Notable = 1,        // 중요한 증거 확보
        PartialCore = 2,    // 핵심 증거 일부
        Provable = 3,       // 범행 입증 가능
    }

    /// <summary>엔딩 4종. 구출 여부 × 입증 여부.</summary>
    public enum EndingId
    {
        None = 0,
        /// <summary>진상 — 구출 O · 입증 O</summary>
        Truth = 1,
        /// <summary>절반의 구원 — 구출 O · 입증 X</summary>
        HalfSalvation = 2,
        /// <summary>늦은 문 — 구출 X · 입증 O</summary>
        LateDoor = 3,
        /// <summary>전설의 완성 — 구출 X · 입증 X (경계도 100 도달 시 강제)</summary>
        LegendComplete = 4,
    }
}
