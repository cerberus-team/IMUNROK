using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 제3사건 진행·세계 상태의 단일 보관소. 씬을 넘나들어도 유지된다.
    ///
    /// ■ 왜 GameState(공통)가 아니라 여기인가
    ///   공통 폴더는 읽기 전용이고, GameState는 사건 단위 판결·단서를 다룬다. 반면 여기 담기는 것은
    ///   **견우 사건 안에서만 의미 있는 잔상태**다 — 병풍을 치웠는지, 비밀문을 열어 뒀는지,
    ///   지금이 낮인지 밤인지. 씬에 저장하면 씬을 다시 로드하는 순간 되돌아가므로 씬 밖에 둔다.
    ///
    /// ■ 지속 범위
    ///   정적 필드라 **세션(플레이 실행) 단위**로 유지된다. 씬을 나갔다 들어와도 남고,
    ///   앱을 껐다 켜면 초기화된다. 세이브 파일이 생기면 <see cref="CaptureFlags"/> /
    ///   <see cref="RestoreFlags"/> 로 통째 직렬화하면 된다.
    ///
    /// ■ 에디터에서 Play를 멈추면
    ///   도메인 리로드로 정적 필드가 날아간다. 검증 중에 상태를 유지하려면
    ///   Tools ▸ 이문록 ▸ 디버그 메뉴로 다시 세우면 된다.
    /// </summary>
    public static class GyeonuWorld
    {
        // ── 진행 플래그 키 ─────────────────────────────────
        /// <summary>비밀지도를 손에 넣었다.</summary>
        public const string F_비밀지도획득 = "secret_map_obtained";
        /// <summary>관측실에서 타공 지도로 길을 밝혔다.</summary>
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
        /// <summary>집무실 비밀문의 잠금이 풀렸다.</summary>
        public const string F_비밀문_해제 = "secret_door_unlocked";
        /// <summary>집무실 병풍을 치워 뒀다.</summary>
        public const string F_병풍_치움 = "folding_screen_folded";
        /// <summary>집무실 비밀문을 열어 뒀다.</summary>
        public const string F_비밀문_열림 = "secret_door_open";
        /// <summary>비밀문 첫 개방 안내를 이미 봤다.</summary>
        public const string F_비밀문_안내함 = "secret_door_announced";
        /// <summary>오작교 교대 암문의 존재를 아는 단서를 얻었다 — 이때만(그리고 밤에만) 문틈 빛이 보인다.
        /// 길에는 아무 표시도 없다 — 위치는 단서 문서로 알아야 한다 (2026-08-20).</summary>
        public const string F_암문단서 = "ammun_clue_found";
        /// <summary>오작교 암문의 돌 자물쇠(개미수열)를 풀었다 — 한 번 풀면 다시 풀 필요가 없다 (2026-08-23).</summary>
        public const string F_암문퍼즐 = "ammun_puzzle_solved";
        /// <summary>마을 아이들에게 관아 담장 개구멍 이야기를 들었다 — 밤에만, 이 단서가 있어야
        /// 개구멍이 그냥 담장이 아니라 밀어서 여는 자리라는 걸 알아본다 (2026-08-21).
        /// 조건 미달이면 커서를 올려도 아무 반응이 없다 — 문인 줄도 모르는 상태다.</summary>
        public const string F_개구멍이야기 = "gwana_gap_hole_story_heard";

        static readonly HashSet<string> _flags = new HashSet<string>();

        /// <summary>플래그·시간대가 바뀌면 발생. 조명·안내 UI가 따라 갱신하는 데 쓴다.</summary>
        public static event System.Action Changed;

        // ── 진행 조건 ─────────────────────────────────────

        /// <summary>
        /// 켜면 모든 진행 조건 검사를 통과시킨다 — 배경 검증용 디버그 스위치.
        /// (Tools ▸ 이문록 ▸ 디버그 ▸ 진행 조건 무시 토글, 또는 SceneExit 인스펙터 체크박스)
        /// </summary>
        public static bool DebugIgnoreConditions;

        public static bool Has(string flag) => !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        public static void Set(string flag, bool on = true)
        {
            if (string.IsNullOrEmpty(flag)) return;
            bool changed = on ? _flags.Add(flag) : _flags.Remove(flag);
            if (changed) Changed?.Invoke();
        }

        /// <summary>여러 조건을 모두 만족하는가. 디버그 무시 스위치가 켜져 있으면 항상 참.</summary>
        public static bool HasAll(IEnumerable<string> flags)
        {
            if (DebugIgnoreConditions) return true;
            if (flags == null) return true;
            foreach (var f in flags)
                if (!string.IsNullOrEmpty(f) && !_flags.Contains(f)) return false;
            return true;
        }

        /// <summary>아직 모자란 조건만 골라 돌려준다 — 안내 문구를 상황에 맞게 짓는 데 쓴다.</summary>
        public static List<string> Missing(IEnumerable<string> flags)
        {
            var miss = new List<string>();
            if (DebugIgnoreConditions || flags == null) return miss;
            foreach (var f in flags)
                if (!string.IsNullOrEmpty(f) && !_flags.Contains(f)) miss.Add(f);
            return miss;
        }

        // ── 시간대·날씨 ───────────────────────────────────

        static bool _night, _rain;

        /// <summary>지금이 밤인가. 성하리 마을의 상태가 기준이고 다른 씬이 이 값을 물려받는다.</summary>
        public static bool Night
        {
            get => _night;
            set { if (_night == value) return; _night = value; Changed?.Invoke(); }
        }

        /// <summary>비가 오는가.</summary>
        public static bool Rain
        {
            get => _rain;
            set { if (_rain == value) return; _rain = value; Changed?.Invoke(); }
        }

        /// <summary>"낮_맑음" 같은 하늘 프리셋 이름 조각. 프리셋 에셋 이름과 맞춘다.</summary>
        public static string SkyKey => (_night ? "밤" : "낮") + "_" + (_rain ? "비" : "맑음");

        /// <summary>
        /// 기준 씬(성하리)이 세계 시간대를 이미 시드했는가.
        ///
        /// 이 플래그가 없으면 마을 씬이 **로드될 때마다** 저장된 초기값으로 세계 상태를
        /// 덮어써서, 밤에 은하담을 갔다가 마을로 돌아오는 순간 낮으로 리셋된다
        /// (2026-08-19 Play 재현). 시드는 세션 최초 1회만 한다.
        /// </summary>
        public static bool TimeSeeded;

        // ── 세이브 연동 자리 ──────────────────────────────

        public static string[] CaptureFlags()
        {
            var arr = new string[_flags.Count];
            _flags.CopyTo(arr);
            return arr;
        }

        public static void RestoreFlags(IEnumerable<string> flags)
        {
            _flags.Clear();
            if (flags != null) foreach (var f in flags) if (!string.IsNullOrEmpty(f)) _flags.Add(f);
            Changed?.Invoke();
        }

        /// <summary>전부 초기화 (디버그).</summary>
        public static void ResetAll()
        {
            _flags.Clear();
            _night = _rain = false;
            DebugIgnoreConditions = false;
            TimeSeeded = false;
            Changed?.Invoke();
        }
    }
}
