using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 소지품 한 종류의 정의 (ScriptableObject, 2026-08-23).
    /// 아이템을 늘리는 방법 = 이 에셋을 하나 더 만드는 것.
    ///   Project 창 ▸ Create ▸ 이문록 ▸ 소지품
    /// 코드를 고칠 필요가 없다 — 20개, 50개가 되어도 에셋만 늘어난다.
    ///
    /// ■ 왜 SO인가
    ///   씬·프리팹·디버그 메뉴가 **같은 하나**를 가리켜야 중복 획득 판정(<see cref="itemId"/>)이
    ///   성립한다. 씬마다 복사본이 생기는 구조면 "이미 가진 물건"을 알아볼 수 없다.
    ///
    /// ■ 단서 코드와의 연결
    ///   <see cref="itemId"/> 가 곧 단서 코드다(예: C4). <see cref="journalKey"/> 를 채우면
    ///   획득 순간 공통 수첩(<see cref="Journal"/>)에도 자동으로 한 줄 남는다.
    /// </summary>
    [CreateAssetMenu(menuName = "이문록/소지품", fileName = "Item_새소지품")]
    public class InventoryItem : ScriptableObject, IUiItem
    {
        [Header("식별")]
        [Tooltip("단서 코드와 같은 값 (예: C4). 중복 획득 판정의 기준 — 반드시 유일하게")]
        public string itemId = "";

        [Tooltip("목록·상세에 뜨는 이름")]
        public string displayName = "이름 없는 물건";

        [Tooltip("씬에서 조준했을 때 뜨는 행동 문구. 놓인 자리에 맞춰 고른다 — " +
                 "책상 위=살피기 / 바닥=줍기 / 가구 안=꺼내기 / NPC가 건넴=받기")]
        public string pickupVerb = "살피기";

        [Header("내용")]
        [Tooltip("상세 보기 오른쪽에 뜨는 글. 길어도 된다 — 넘치면 휠로 굴려 읽는다")]
        [TextArea(4, 30)]
        public string description = "";

        [Header("상세 보기 3D")]
        [Tooltip("돌려 보는 모델. 비우면 상세 보기에 글만 뜬다")]
        public GameObject modelPrefab;

        [Tooltip("모델을 세울 자세 보정 (오일러 각). 눕혀서 만든 모델을 세울 때 쓴다")]
        public Vector3 previewEuler = Vector3.zero;

        [Tooltip("자동 프레이밍에 곱하는 배율. 1이면 화면에 꽉 차게 자동으로 맞춘다")]
        public float previewZoom = 1f;

        [Header("쓰임")]
        [Tooltip("상세 보기에 쓰임 버튼을 띄울지. **끄면 버튼이 아예 안 나온다** — " +
                 "서책·문서처럼 옆에 글이 다 나오는 물건은 누를 것이 없다")]
        public bool usable = false;

        [Tooltip("버튼 문구 — 물건에 맞게. 지도=펼쳐보기 / 열쇠=사용하기 / 부적=붙이기 …. " +
                 "비워 두면 usable이 켜져 있어도 버튼을 그리지 않는다")]
        public string useLabel = "사용하기";

        [Tooltip("사용 동작이 아직 안 붙었을 때 뜨는 안내")]
        public string useNotReadyHint = "아직 여기서 쓸 수 없다.";

        [Tooltip("처음 손에 넣는 순간 상세 보기를 자동으로 띄운다 (그 물건당 한 번). 끄면 알림만 뜬다")]
        public bool autoShowOnPickup = true;

        [Header("획득 연동")]
        [Tooltip("채우면 획득 순간 공통 수첩에 이 key로 단서가 기록된다. 비우면 기록하지 않는다")]
        public string journalKey = "";

        [Tooltip("수첩에 남길 문구. 비우면 설명 첫 줄을 쓴다")]
        [TextArea(1, 4)]
        public string journalText = "";

        [Tooltip("채우면 획득 순간 GyeonuWorld에 이 플래그가 선다 (2026-08-23).\n" +
                 "물건 자체가 곧 단서인 경우에 쓴다 — 예: C4 서책을 손에 넣으면 암문의 존재를 " +
                 "알게 되므로 ammun_clue_found. 문·힌트 쪽은 플래그만 보면 되므로 " +
                 "소지품 시스템을 몰라도 된다.")]
        public string worldFlag = "";

        /// <summary>중복 판정용 키. itemId가 비어 있으면 에셋 이름으로 대신한다.</summary>
        public string Key => string.IsNullOrEmpty(itemId) ? name : itemId;

        /// <summary>상세 보기에 쓰임 버튼을 그릴 것인가 — 켜져 있고 문구도 있어야 그린다.</summary>
        public bool ShowUseButton => usable && !string.IsNullOrEmpty(useLabel);

        // ── IUiItem ──────────────────────────────────────────
        //
        // 소지품 판이 묻는 것을 우리 필드에 이어 준다 (2026-08-26).
        // ⚠️ **필드는 하나도 안 바꿨다.** 이름만 파스칼로 다시 내보이는 얇은 껍데기다 —
        //    그래야 이미 만들어 둔 .asset 일곱 개가 그대로 읽힌다.
        //    판은 이제 InventoryItem 을 모르고 IUiItem 만 안다.

        string IUiItem.DisplayName => displayName;
        string IUiItem.Description => description;
        GameObject IUiItem.ModelPrefab => modelPrefab;
        Vector3 IUiItem.PreviewEuler => previewEuler;
        float IUiItem.PreviewZoom => previewZoom;
        string IUiItem.UseLabel => useLabel;
        string IUiItem.UseNotReadyHint => useNotReadyHint;
        bool IUiItem.AutoShowOnPickup => autoShowOnPickup;
    }
}
