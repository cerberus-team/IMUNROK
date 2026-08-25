using System.Collections.Generic;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 제3사건 진행·세계 상태로 들어가는 창구.
    ///
    /// ■ 2026-08-25 — 알맹이가 <see cref="GyeonuCase"/> 로 옮겨졌다
    ///   플래그·시간대·날씨가 여기 흩어져 있었고, 점수(신뢰도·증거도·경계도)와 단서 22종은
    ///   아예 없었다. 그것을 전부 <see cref="GyeonuCase"/> 한 곳으로 모으면서 이 클래스는
    ///   <b>얇은 창구</b>만 남았다. 하는 일은 예전과 똑같다 —
    ///   <c>GyeonuWorld.Set(F_암문퍼즐)</c> 은 여전히 같은 뜻이고, 씬·프리팹에 직렬화된
    ///   <b>플래그 문자열은 하나도 바뀌지 않았다</b>. 기존 퍼즐 코드는 손댈 필요가 없다.
    ///
    ///   새로 쓰는 코드는 <see cref="GyeonuCase"/> 를 직접 부르는 편이 낫다 —
    ///   단서·점수·엔딩까지 거기서 다룬다.
    ///
    /// ■ 지속 범위
    ///   세션(플레이 실행) 단위. 씬을 나갔다 들어와도 남고, Play를 멈추면 초기화된다.
    ///   앱을 껐다 켜도 남기려면 <see cref="GyeonuSave"/>.
    ///
    /// ■ 검증 중에 상태를 세우려면
    ///   Play 중 <b>F9</b> 디버그 창, 또는 Tools ▸ 이문록 ▸ 디버그 메뉴.
    /// </summary>
    public static class GyeonuWorld
    {
        // ── 진행 플래그 키 ─────────────────────────────────
        //   ⚠️ 이 문자열들은 씬·프리팹·소지품 에셋에 직렬화돼 있다. 값을 바꾸면 조용히 끊긴다.

        /// <summary>비밀지도를 손에 넣었다. (단서 A1)</summary>
        public const string F_비밀지도획득 = "secret_map_obtained";
        /// <summary>관측실에서 타공 지도로 길을 밝혔다. (단서 A2)</summary>
        public const string F_타공지도_길밝힘 = "map_path_lit";
        /// <summary>선아의 관측 수기를 손에 넣었다 — 혼천의 고리를 어떻게 맞추는지 알게 된다.
        /// 이것이 없으면 혼천의를 조사해도 퍼즐이 시작되지 않는다 (2026-08-23).</summary>
        public const string F_혼천의메모 = "honcheonui_memo";
        /// <summary>혼천의 여섯 고리를 여덟 방위에 맞췄다 — 이때부터 **혼상을 돌릴 수** 있다.
        /// 한 번 서면 다시 풀 필요가 없다.</summary>
        public const string F_혼천의퍼즐 = "honcheonui_solved";

        // ── 관측실 진행 사슬 (2026-08-23) ─────────────────
        //   혼천의 성공 → 혼상 회전 → 촛대 소지 → 점등
        //   한 단계씩만 열린다. 앞 단계를 건너뛰면 다음 단계는 조준해도 반응이 없다.

        /// <summary>혼상을 충분히 돌려 어디에 불을 넣어야 하는지 알아냈다 — 이때 촛대 잠금이 풀린다.</summary>
        public const string F_혼상회전 = "honsang_turned";
        /// <summary>작업실 촛대를 손에 들고 있다. 씬을 나갔다 와도 손에 남기려고 둔다.</summary>
        public const string F_촛대소지 = "lantern_held";
        /// <summary>혼상에 불을 넣었다 (점등 시퀀스를 시작한 시점).</summary>
        public const string F_혼상점등 = "honsang_lit";
        /// <summary>집무실 문갑 상판의 쌍학월도 렌즈 퍼즐을 풀었다 — 이때 문갑 잠금이 풀린다 (2026-08-24).</summary>
        public const string F_렌즈퍼즐 = "lens_puzzle_solved";
        /// <summary>문갑 서랍에서 수령의 비밀 열쇠를 꺼냈다 — 비밀문이 이 플래그를 본다.
        /// 소지품 쪽에서 <see cref="InventoryItem.worldFlag"/>로 세운다.</summary>
        public const string F_수령열쇠 = "suryeong_key_held";
        /// <summary>집무실 비밀문의 잠금이 풀렸다.</summary>
        public const string F_비밀문_해제 = "secret_door_unlocked";
        /// <summary>집무실 병풍을 치워 뒀다.</summary>
        public const string F_병풍_치움 = "folding_screen_folded";
        /// <summary>집무실 비밀문을 열어 뒀다.</summary>
        public const string F_비밀문_열림 = "secret_door_open";
        /// <summary>비밀문 첫 개방 안내를 이미 봤다.</summary>
        public const string F_비밀문_안내함 = "secret_door_announced";
        /// <summary>오작교 교대 암문의 존재를 아는 단서를 얻었다 — 이때만(그리고 밤에만) 문틈 빛이 보인다.
        /// 길에는 아무 표시도 없다 — 위치는 단서 문서로 알아야 한다 (2026-08-20). (단서 C4)</summary>
        public const string F_암문단서 = "ammun_clue_found";
        /// <summary>오작교 암문의 돌 자물쇠(개미수열)를 풀었다 — 한 번 풀면 다시 풀 필요가 없다 (2026-08-23).</summary>
        public const string F_암문퍼즐 = "ammun_puzzle_solved";

        // ── 종막 — 서고 장부 (2026-08-24) ─────────────────
        //   C1·C3를 손에 넣어야 시작되고, 세 단계를 지나면 C2가 놓인 자리가 드러난다.

        /// <summary>아버지의 검수 기록(C1)을 지녔다 — 2단계 대조의 전제.
        /// 소지품 쪽에서 <see cref="InventoryItem.worldFlag"/>로 세운다.</summary>
        public const string F_아버지검수기록 = "father_inspection_record";
        /// <summary>선아의 풀이표(C3)를 지녔다 — 장부를 읽을 줄 알게 된다. 1단계의 전제.</summary>
        public const string F_선아풀이표 = "seona_key_table";
        /// <summary>1단계 — 수령의 장부 배열을 복원했다.</summary>
        public const string F_장부복원 = "ledger_restored";
        /// <summary>2단계 — 아버지 기록과 같은 기물 셋을 골라냈다.</summary>
        public const string F_기물대조 = "ledger_matched";
        /// <summary>3단계 — 셋이 모두 같은 손(崔)을 거쳐 같은 곳(後庫)으로 갔음이 드러났다.
        /// 이때부터 서고 안쪽 창고방 서랍장을 뒤질 수 있다 — 게임은 어디로 가라 말하지 않는다.</summary>
        public const string F_후고단서 = "backstore_lead";

        /// <summary>마을 아이들에게 관아 담장 개구멍 이야기를 들었다 — 밤에만, 이 단서가 있어야
        /// 개구멍이 그냥 담장이 아니라 밀어서 여는 자리라는 걸 알아본다 (2026-08-21).
        /// 조건 미달이면 커서를 올려도 아무 반응이 없다 — 문인 줄도 모르는 상태다.</summary>
        public const string F_개구멍이야기 = "gwana_gap_hole_story_heard";

        /// <summary>
        /// 알려진 플래그 전부 — (키, 한글 이름). 디버그 창이 이 순서로 그린다.
        /// 새 플래그를 만들면 여기에도 한 줄 넣어야 창에 뜬다.
        /// </summary>
        public static readonly (string key, string label)[] Catalog =
        {
            (F_비밀지도획득,   "비밀지도 획득 (A1)"),
            (F_타공지도_길밝힘, "타공지도 길 밝힘 (A2)"),
            (F_혼천의메모,     "혼천의 메모 소지"),
            (F_혼천의퍼즐,     "혼천의 8방위 정렬"),
            (F_혼상회전,       "혼상 회전"),
            (F_촛대소지,       "촛대 소지"),
            (F_혼상점등,       "혼상 점등"),
            (F_암문단서,       "암문 단서 (C4)"),
            (F_암문퍼즐,       "암문 개미수열 해제"),
            (F_렌즈퍼즐,       "렌즈 퍼즐 해제"),
            (F_수령열쇠,       "수령의 비밀 열쇠"),
            (F_비밀문_해제,     "비밀문 잠금 해제"),
            (F_비밀문_열림,     "비밀문 열림"),
            (F_비밀문_안내함,   "비밀문 안내 봄"),
            (F_병풍_치움,      "병풍 치움"),
            (F_개구멍이야기,    "개구멍 이야기 들음"),
            (F_아버지검수기록,  "아버지 검수 기록 (C1)"),
            (F_선아풀이표,     "선아의 풀이표 (C3)"),
            (F_장부복원,       "서고 ① 장부 복원"),
            (F_기물대조,       "서고 ② 기물 대조"),
            (F_후고단서,       "서고 ③ 후고 단서"),
        };

        /// <summary>플래그·시간대가 바뀌면 발생. 조명·안내 UI가 따라 갱신하는 데 쓴다.</summary>
        public static event System.Action Changed
        {
            add { GyeonuCase.Changed += value; }
            remove { GyeonuCase.Changed -= value; }
        }

        // ── 진행 조건 ─────────────────────────────────────

        /// <summary>
        /// 켜면 모든 진행 조건 검사를 통과시킨다 — 배경 검증용 디버그 스위치.
        /// (F9 디버그 창, Tools ▸ 이문록 ▸ 디버그 ▸ 진행 조건 무시 토글, SceneExit 체크박스)
        /// </summary>
        public static bool DebugIgnoreConditions
        {
            get => GyeonuCase.DebugIgnoreConditions;
            set => GyeonuCase.DebugIgnoreConditions = value;
        }

        public static bool Has(string flag) => GyeonuCase.HasFlag(flag);

        public static void Set(string flag, bool on = true) => GyeonuCase.SetFlag(flag, on);

        /// <summary>여러 조건을 모두 만족하는가. 디버그 무시 스위치가 켜져 있으면 항상 참.</summary>
        public static bool HasAll(IEnumerable<string> flags) => GyeonuCase.HasAllFlags(flags);

        /// <summary>아직 모자란 조건만 골라 돌려준다 — 안내 문구를 상황에 맞게 짓는 데 쓴다.</summary>
        public static List<string> Missing(IEnumerable<string> flags) => GyeonuCase.MissingFlags(flags);

        // ── 시간대·날씨 ───────────────────────────────────

        /// <summary>
        /// 지금이 밤인가. 성하리 마을의 상태가 기준이고 다른 씬이 이 값을 물려받는다.
        /// 시간대는 이제 3단계(<see cref="GyeonuCase.Time"/>)이고, 이 값은 "낮이 아니다"와 같다.
        /// 참을 넣으면 초밤이 되고, 늦은 밤은 진행(견우마을 귀환 또는 씬 이동 3회)으로만 온다.
        /// </summary>
        public static bool Night
        {
            get => GyeonuCase.Night;
            set => GyeonuCase.Night = value;
        }

        /// <summary>비가 오는가.</summary>
        public static bool Rain
        {
            get => GyeonuCase.Rain;
            set => GyeonuCase.Rain = value;
        }

        /// <summary>"낮_맑음" 같은 하늘 프리셋 이름 조각. 프리셋 에셋 이름과 맞춘다.</summary>
        public static string SkyKey => GyeonuCase.SkyKey;

        /// <summary>
        /// 기준 씬(성하리)이 세계 시간대를 이미 시드했는가.
        ///
        /// 이 플래그가 없으면 마을 씬이 **로드될 때마다** 저장된 초기값으로 세계 상태를
        /// 덮어써서, 밤에 은하담을 갔다가 마을로 돌아오는 순간 낮으로 리셋된다
        /// (2026-08-19 Play 재현). 시드는 세션 최초 1회만 한다.
        /// </summary>
        public static bool TimeSeeded
        {
            get => GyeonuCase.TimeSeeded;
            set => GyeonuCase.TimeSeeded = value;
        }

        // ── 세이브 연동 ───────────────────────────────────

        public static string[] CaptureFlags() => GyeonuCase.CaptureFlags();

        public static void RestoreFlags(IEnumerable<string> flags) => GyeonuCase.RestoreFlags(flags);

        /// <summary>전부 초기화 (디버그). 점수·단서까지 함께 지워진다.</summary>
        public static void ResetAll() => GyeonuCase.ResetAll();
    }
}
