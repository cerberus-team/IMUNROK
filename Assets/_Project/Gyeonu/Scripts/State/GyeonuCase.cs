using System;
using System.Collections.Generic;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 제3사건의 상태 원본 (2026-08-25). 단서·점수 3축·진행 플래그·시간대를 **한 곳**에 모은다.
    /// 최종기획안 11차 「제3부 · 점수 시스템」과 「34. 데이터 구조」를 그대로 구현한 것이다.
    ///
    /// ■ 공통 GameState와의 관계 — 대체가 아니라 얹기
    ///   공통 <see cref="GameState"/>는 세 사건 공통의 <b>판결·완료 여부</b>만 다루고,
    ///   <see cref="Journal"/>은 단서를 <b>문자열 key로</b> 기록한다. 둘 다 읽기 전용 영역이라
    ///   손대지 않는다. 여기 담기는 것은 제3사건 안에서만 의미가 있는 것 —
    ///   신뢰도·증거도·경계도, 22종 단서 코드, 퍼즐 진행 플래그, 시간대 3단계다.
    ///   기록이 갈라지지 않도록 단서를 넣으면 <see cref="Journal"/>에도 같은 줄이 남고,
    ///   엔딩이 정해지면 <see cref="GameState.SetVerdict"/> 까지 대신 불러 준다.
    ///
    /// ■ GyeonuWorld와의 관계 — 이름만 바뀌고 동작은 같다
    ///   기존 <see cref="GyeonuWorld"/>는 이제 이 클래스로 가는 얇은 창구다. 씬·프리팹에
    ///   문자열 플래그가 직렬화돼 있으므로 <b>플래그 이름은 하나도 바꾸지 않았다</b>.
    ///   기존 퍼즐 코드는 한 줄도 고칠 필요가 없다.
    ///
    /// ■ 점수를 계산값으로 둔 이유
    ///   3축은 "더한 값"이 아니라 <b>보유 상태에서 다시 계산한 값</b>이다.
    ///   같은 단서를 두 번 넣어도 두 번 가산되지 않고(문서: 모든 단서는 1회만),
    ///   디버그로 단서를 껐다 켜도 점수가 어긋나지 않는다.
    ///
    /// ■ 지속 범위
    ///   정적 필드 = 세션(플레이 실행) 단위. 씬을 오가도 남고 Play를 멈추면 초기화된다.
    ///   앱을 껐다 켜도 남기려면 <see cref="GyeonuSave"/> 를 쓴다.
    /// </summary>
    public static class GyeonuCase
    {
        // ─────────────────────────────────────────────────────────
        //  보유 상태 — 여기서 3축이 계산된다
        // ─────────────────────────────────────────────────────────

        static readonly HashSet<ClueId> _clues = new HashSet<ClueId>();
        static readonly HashSet<ClueId> _presented = new HashSet<ClueId>();      // 견우에게 제시한 단서
        static readonly HashSet<ContradictionId> _contradictions = new HashSet<ContradictionId>();
        static readonly HashSet<Threshold> _thresholdFired = new HashSet<Threshold>();
        static readonly HashSet<string> _flags = new HashSet<string>();
        static readonly Dictionary<AlertTopic, int> _alertTopicHits = new Dictionary<AlertTopic, int>();
        static readonly Dictionary<NpcId, int> _npcInsult = new Dictionary<NpcId, int>();

        static int _gyeonuFavor, _gyeonuPressure, _gyeonuInsult;
        static int _trustOffset, _evidenceOffset, _alertOffset;   // 디버그·연출용 직접 보정
        static bool _identityRevealed, _identitySpread;
        static bool _seonaRescued, _hasSeonaHouseKey;
        static bool _ledgerBurned, _c5LostForever;
        static int _nightSceneTransitions, _nightsRemaining;
        static Act _act = Act.Village;
        static TimeOfDay _time = TimeOfDay.Day;
        static Weather _weather = Weather.Clear;
        static EndingId _forcedEnding = EndingId.None;

        /// <summary>
        /// 켜면 모든 진행 조건 검사를 통과시킨다 — 배경 검증용 디버그 스위치.
        /// (기존 GyeonuWorld.DebugIgnoreConditions 와 같은 값이다.)
        /// </summary>
        public static bool DebugIgnoreConditions;

        /// <summary>기준 씬(성하리)이 세계 시간대를 이미 시드했는가. 세션 최초 1회만 시드한다.</summary>
        public static bool TimeSeeded;

        /// <summary>무엇이든 바뀌면 발생. 조명·안내 UI·디버그 창이 따라 갱신한다.</summary>
        public static event Action Changed;

        /// <summary>단서를 새로 얻는 순간 발생 — 획득 알림 연출용.</summary>
        public static event Action<ClueId> ClueAdded;

        /// <summary>임계 이벤트 발화(1회성·비가역). 연출·잠금이 여기에 붙는다.</summary>
        public static event Action<Threshold> ThresholdFired;

        // 플레이 세션마다 확실히 처음부터 — 도메인 리로드가 꺼져도 상태가 새지 않게 (Inventory와 같은 방식)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            HardReset();
            Changed = null;
            ClueAdded = null;
            ThresholdFired = null;
        }

        // 소지품 획득을 단서 획득으로 이어 붙인다. SubsystemRegistration(=Inventory 초기화) 다음에 돈다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void HookInventory()
        {
            Inventory.Added -= OnItemAdded;
            Inventory.Added += OnItemAdded;
        }

        static void OnItemAdded(InventoryItem item)
        {
            // 소지품 id가 곧 단서 코드다 (A1·C1·C2·C3·C4 …). HONCHEON_MEMO처럼 아닌 것은 그냥 지나간다.
            if (item != null && ClueTable.TryParse(item.Key, out var id)) AddClue(id);
        }

        // ─────────────────────────────────────────────────────────
        //  3축 — 전부 계산값 (0~100)
        // ─────────────────────────────────────────────────────────

        /// <summary>신뢰도 — 현재의 견우. 모욕 3회면 0에 고정된다.</summary>
        public static int Trust
        {
            get
            {
                if (GyeonuLocked) return 0;
                int v = _trustOffset;
                foreach (var c in _presented) v += ClueTable.Trust(c);
                v += Mathf.Min(_gyeonuFavor, ClueTable.FavorMax) * ClueTable.FavorDelta;
                v += Mathf.Min(_gyeonuPressure, ClueTable.PressureMax) * ClueTable.PressureDelta;
                v += _gyeonuInsult * ClueTable.InsultDelta;
                return Mathf.Clamp(v, 0, 100);
            }
        }

        /// <summary>증거도 — 수령의 범행을 얼마나 입증했는가.</summary>
        public static int Evidence
        {
            get
            {
                int v = _evidenceOffset;
                foreach (var c in _clues) v += ClueTable.Evidence(c);
                v += _contradictions.Count * ClueTable.ContradictionEvidence;
                return Mathf.Clamp(v, 0, 100);
            }
        }

        /// <summary>경계도 — 수령이 플레이어를 얼마나 위험하게 보는가.</summary>
        public static int Alert
        {
            get
            {
                int v = _alertOffset;
                foreach (var kv in _alertTopicHits)
                {
                    int delta, max;
                    ClueTable.Alert(kv.Key, out delta, out max);
                    v += Mathf.Min(kv.Value, max) * delta;
                }
                if (_identitySpread) v += ClueTable.IdentityRevealAlert;
                return Mathf.Clamp(v, 0, 100);
            }
        }

        /// <summary>모욕 3회로 견우의 핵심 대화가 잠겼는가. 되돌릴 수 없다.</summary>
        public static bool GyeonuLocked => _gyeonuInsult >= ClueTable.InsultLockAt;

        // ─────────────────────────────────────────────────────────
        //  단서
        // ─────────────────────────────────────────────────────────

        public static bool HasClue(ClueId id) => _clues.Contains(id);
        public static int ClueCount => _clues.Count;
        public static IEnumerable<ClueId> Clues => _clues;

        /// <summary>
        /// 단서를 얻는다. 처음이면 증거도가 자동으로 오르고(1회만) 공통 수첩에도 한 줄 남는다.
        /// 이미 가진 단서면 아무 일도 없고 false.
        /// </summary>
        public static bool AddClue(ClueId id)
        {
            // 경계도 40에서 장부가 탔고 그때 C5를 못 얻었으면 영구 소실이다 (문서 「12. 임계」).
            if (id == ClueId.C5 && _c5LostForever)
            {
                Debug.Log("[제3사건] C5는 이미 소각되어 얻을 수 없다 (경계도 40 임계).");
                return false;
            }
            if (!_clues.Add(id)) return false;

            SyncFlagFromClue(id, true);

            // ⚠️ 에디터 메뉴가 편집 모드에서 플래그를 세울 때도 여기로 온다. 그때 Journal.Instance를
            //    건드리면 열려 있는 씬에 [Journal] 오브젝트가 생겨 씬이 더러워진다 — Play 중에만 기록한다.
            if (Application.isPlaying)
            {
                var info = ClueTable.Get(id);
                Journal.Instance.AddClue(CaseId.Case3_Gyeonu, id.ToString(),
                                         info != null ? id + " " + info.title : id.ToString());
            }

            Debug.Log("[제3사건] 단서 " + ClueTable.Label(id) + " 획득 — 증거도 " + Evidence);
            ClueAdded?.Invoke(id);
            AfterChange();
            return true;
        }

        /// <summary>단서를 지운다 (디버그용). 증거도는 다시 계산되어 자동으로 내려간다.</summary>
        public static bool RemoveClue(ClueId id)
        {
            if (!_clues.Remove(id)) return false;
            _presented.Remove(id);
            SyncFlagFromClue(id, false);
            AfterChange();
            return true;
        }

        /// <summary>핵심 3종(C1·C2·C3)을 전부 가졌는가.</summary>
        public static bool HasCoreClues
        {
            get
            {
                foreach (var c in ClueTable.CoreClues) if (!_clues.Contains(c)) return false;
                return true;
            }
        }

        /// <summary>범행을 입증할 수 있는가 — 증거도 80 이상 <b>이면서</b> 핵심 3종 보유.</summary>
        public static bool Proven => Evidence >= ClueTable.ProofEvidenceMin && HasCoreClues;

        /// <summary>저널에 보여 줄 단계. 숫자는 끝까지 감춘다 (문서 「14. 점수 표시」).</summary>
        public static EvidenceStage Stage
        {
            get
            {
                if (Proven) return EvidenceStage.Provable;
                if (Evidence >= 60) return EvidenceStage.PartialCore;
                if (Evidence >= 30) return EvidenceStage.Notable;
                return EvidenceStage.Insufficient;
            }
        }

        public static string StageLabel
        {
            get
            {
                switch (Stage)
                {
                    case EvidenceStage.Provable: return "범행 입증 가능";
                    case EvidenceStage.PartialCore: return "핵심 증거 일부";
                    case EvidenceStage.Notable: return "중요한 증거 확보";
                    default: return "정황 부족";
                }
            }
        }

        // ─────────────────────────────────────────────────────────
        //  단서 제시 — 견우 신뢰도
        // ─────────────────────────────────────────────────────────

        public static bool HasPresented(ClueId id) => _presented.Contains(id);

        /// <summary>
        /// 견우에게 단서를 제시한다. 같은 단서는 <b>1회만</b> 신뢰도에 반영된다.
        /// 가지고 있지 않은 단서는 제시할 수 없다.
        /// 구출 이후에는 추가 점수를 주지 않는다 (문서 「10. 구출 이후」).
        /// </summary>
        public static bool PresentClueToGyeonu(ClueId id)
        {
            if (!_clues.Contains(id)) { Debug.Log("[제3사건] 가지고 있지 않은 단서는 제시할 수 없다: " + id); return false; }
            if (GyeonuLocked) { Debug.Log("[제3사건] 견우가 마음을 닫았다 — 단서 제시가 통하지 않는다."); return false; }
            if (_seonaRescued) { Debug.Log("[제3사건] 구출 이후에는 신뢰도를 더 주지 않는다."); return false; }
            if (!_presented.Add(id)) return false;

            int d = ClueTable.Trust(id);
            Debug.Log("[제3사건] 견우에게 제시: " + ClueTable.Label(id) + " (" + (d >= 0 ? "+" : "") + d + ") → 신뢰도 " + Trust);
            AfterChange();
            return true;
        }

        /// <summary>
        /// 대화 한 마디의 등급을 반영한다.
        /// ⚠️ 등급을 <b>판정</b>하는 일(AI)은 아직 만들지 않는다 — 판정된 결과만 여기로 들어온다.
        /// </summary>
        public static void ReportGyeonuTone(TalkTone tone)
        {
            if (_seonaRescued) return;   // 구출 이후 신뢰도 시스템은 역할이 끝난다

            switch (tone)
            {
                case TalkTone.Favor:    _gyeonuFavor++; break;
                case TalkTone.Pressure: _gyeonuPressure++; break;
                case TalkTone.Insult:
                    _gyeonuInsult++;
                    if (GyeonuLocked)
                        Debug.LogWarning("[제3사건] 견우 모욕 3회 — 신뢰도 0 고정, 핵심 대화 잠김. 지도 획득 불가.");
                    break;
                default: return;         // 중립은 아무 일도 없다
            }
            AfterChange();
        }

        public static int GyeonuFavorCount => _gyeonuFavor;
        public static int GyeonuPressureCount => _gyeonuPressure;
        public static int GyeonuInsultCount => _gyeonuInsult;

        // ─────────────────────────────────────────────────────────
        //  모순
        // ─────────────────────────────────────────────────────────

        public static bool HasContradiction(ContradictionId id) => _contradictions.Contains(id);
        public static IEnumerable<ContradictionId> Contradictions => _contradictions;

        /// <summary>모순을 알아냈다. 각 +10, 1회만.</summary>
        public static bool AddContradiction(ContradictionId id)
        {
            if (!_contradictions.Add(id)) return false;
            if (Application.isPlaying)
                Journal.Instance.AddClue(CaseId.Case3_Gyeonu, id.ToString(), ClueTable.Label(id));
            Debug.Log("[제3사건] 모순 " + ClueTable.Label(id) + " → 증거도 " + Evidence);
            AfterChange();
            return true;
        }

        public static bool RemoveContradiction(ContradictionId id)
        {
            if (!_contradictions.Remove(id)) return false;
            AfterChange();
            return true;
        }

        /// <summary>두 단서를 다 가졌을 때 모순이 성립하는지 자동 판정 — 대화 쪽에서 불러 쓴다.</summary>
        public static void CheckContradictionsFromClues()
        {
            // M1: 수령의 알리바이 ↔ C6 유독 심했던 순찰 (수령 재질문이 전제라 대화 쪽에서 확정한다)
            // M2: 주모의 '죄인' 진술 ↔ C7 죄인이 아니었다
            if (HasClue(ClueId.C7) && HasClue(ClueId.A7)) AddContradiction(ContradictionId.M2);
        }

        // ─────────────────────────────────────────────────────────
        //  경계도 — 수령
        // ─────────────────────────────────────────────────────────

        /// <summary>수령에게 무엇을 캐물었다. 화제별로 반복 상한이 있다.</summary>
        public static void AskMagistrate(AlertTopic topic)
        {
            int delta, max;
            ClueTable.Alert(topic, out delta, out max);
            if (max <= 0) return;                       // 일반 질문은 0점

            int hits;
            _alertTopicHits.TryGetValue(topic, out hits);
            if (hits >= max) return;                    // 상한 도달 — 더 오르지 않는다
            _alertTopicHits[topic] = hits + 1;

            Debug.Log("[제3사건] 수령 경계 +" + delta + " (" + ClueTable.Title(topic) + ") → " + Alert);
            AfterChange();
        }

        /// <summary>
        /// 실제로 암행어사 신분을 증명했다 (마패 제시 등).
        /// ⚠️ 말로만 하는 허세는 여기로 들어오지 않는다 — 판정은 대화 쪽 몫이다.
        /// 소문을 퍼뜨리는 NPC에게 밝히면 마을 전체에 퍼져 +40 (전역 1회).
        /// </summary>
        public static void RevealIdentity(NpcId to)
        {
            _identityRevealed = true;

            bool spreads = ClueTable.SpreadsRumor(to) || to == NpcId.Magistrate;
            if (spreads && !_identitySpread)
            {
                _identitySpread = true;
                Debug.LogWarning("[제3사건] 신분이 드러났다 (" + to + ") — 경계도 +" + ClueTable.IdentityRevealAlert);
            }
            AfterChange();
        }

        public static bool IdentityRevealed => _identityRevealed;
        public static bool IdentitySpreadInVillage => _identitySpread;

        /// <summary>경계도 40에서 장부가 탔는가.</summary>
        public static bool LedgerBurned => _ledgerBurned;
        /// <summary>C5를 영영 얻을 수 없게 됐는가.</summary>
        public static bool C5LostForever => _c5LostForever;
        /// <summary>경계도 70 이후 서고 진입까지 남은 밤. 0 이하면 제한 없음(아직 발화 전).</summary>
        public static int NightsRemaining => _nightsRemaining;

        // ─────────────────────────────────────────────────────────
        //  다른 NPC의 태도 — 수치 없이 무례 누적만
        // ─────────────────────────────────────────────────────────

        public static int NpcInsultCount(NpcId npc)
        {
            int n;
            return _npcInsult.TryGetValue(npc, out n) ? n : 0;
        }

        /// <summary>⚠️ 명백한 조롱·모욕만 센다. 압박 질문은 포함하지 않는다 (문서 「13」).</summary>
        public static void ReportNpcInsult(NpcId npc)
        {
            if (npc == NpcId.Gyeonu) { ReportGyeonuTone(TalkTone.Insult); return; }
            int n = NpcInsultCount(npc) + 1;
            _npcInsult[npc] = n;
            if (n == ClueTable.NpcInsultLimit) Debug.LogWarning("[제3사건] " + npc + " 무례 3회 — 태도가 닫혔다.");
            AfterChange();
        }

        /// <summary>그 NPC가 태도를 닫았는가.</summary>
        public static bool NpcHostile(NpcId npc) => NpcInsultCount(npc) >= ClueTable.NpcInsultLimit;

        /// <summary>회복 조건을 만족했다 — 회복 조건 자체는 NPC마다 달라 대화 쪽에서 판정한다.</summary>
        public static void RecoverNpc(NpcId npc)
        {
            if (_npcInsult.Remove(npc)) AfterChange();
        }

        // ─────────────────────────────────────────────────────────
        //  진행 플래그 — 문자열. 기존 퍼즐 코드가 그대로 쓴다
        // ─────────────────────────────────────────────────────────

        public static bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        public static void SetFlag(string flag, bool on = true)
        {
            if (string.IsNullOrEmpty(flag)) return;
            bool changed = on ? _flags.Add(flag) : _flags.Remove(flag);
            if (!changed) return;

            SyncClueFromFlag(flag, on);
            AfterChange();
        }

        public static bool HasAllFlags(IEnumerable<string> flags)
        {
            if (DebugIgnoreConditions) return true;
            if (flags == null) return true;
            foreach (var f in flags)
                if (!string.IsNullOrEmpty(f) && !_flags.Contains(f)) return false;
            return true;
        }

        public static List<string> MissingFlags(IEnumerable<string> flags)
        {
            var miss = new List<string>();
            if (DebugIgnoreConditions || flags == null) return miss;
            foreach (var f in flags)
                if (!string.IsNullOrEmpty(f) && !_flags.Contains(f)) miss.Add(f);
            return miss;
        }

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
            AfterChange();
        }

        // ── 플래그 ↔ 단서 다리 ───────────────────────────────────
        //   퍼즐·소지품은 예전처럼 문자열 플래그만 세우면 되고, 단서 코드는 여기서 따라붙는다.
        //   덕분에 기존 코드를 한 줄도 고치지 않고 증거도가 오른다.

        static readonly Dictionary<string, ClueId> _flagToClue = new Dictionary<string, ClueId>
        {
            { GyeonuWorld.F_비밀지도획득,   ClueId.A1 },
            { GyeonuWorld.F_타공지도_길밝힘, ClueId.A2 },
            { GyeonuWorld.F_아버지검수기록,  ClueId.C1 },
            { GyeonuWorld.F_선아풀이표,     ClueId.C3 },
            { GyeonuWorld.F_암문단서,       ClueId.C4 },
        };

        static bool _bridging;   // 서로 부르다 되돌아오는 것을 막는다

        static void SyncClueFromFlag(string flag, bool on)
        {
            ClueId id;
            if (_bridging || !_flagToClue.TryGetValue(flag, out id)) return;
            _bridging = true;
            try { if (on) AddClue(id); else RemoveClue(id); }
            finally { _bridging = false; }
        }

        static void SyncFlagFromClue(ClueId id, bool on)
        {
            if (_bridging) return;
            foreach (var kv in _flagToClue)
            {
                if (kv.Value != id) continue;
                _bridging = true;
                try { SetFlag(kv.Key, on); }
                finally { _bridging = false; }
                return;
            }
        }

        // ─────────────────────────────────────────────────────────
        //  시간대 · 날씨
        // ─────────────────────────────────────────────────────────

        /// <summary>시간대 3단계. 낮 → 초밤 → 늦은 밤. 되돌아가지 않는 게 정상이지만 디버그로는 된다.</summary>
        public static TimeOfDay Time
        {
            get => _time;
            set
            {
                if (_time == value) return;
                _time = value;
                Debug.Log("[제3사건] 시간대 = " + TimeLabel);
                AfterChange();
            }
        }

        public static Weather Weather
        {
            get => _weather;
            set
            {
                if (_weather == value) return;
                _weather = value;
                AfterChange();
            }
        }

        /// <summary>밤인가 (초밤·늦은 밤 모두 참). 기존 GyeonuWorld.Night 이 이 값이다.</summary>
        public static bool Night
        {
            get => _time != TimeOfDay.Day;
            set
            {
                if (value == Night) return;
                Time = value ? TimeOfDay.EarlyNight : TimeOfDay.Day;
            }
        }

        public static bool Rain
        {
            get => _weather == Weather.Rain;
            set => Weather = value ? Weather.Rain : Weather.Clear;
        }

        /// <summary>"낮_맑음" 같은 하늘 프리셋 이름 조각.</summary>
        public static string SkyKey => (Night ? "밤" : "낮") + "_" + (Rain ? "비" : "맑음");

        public static string TimeLabel
        {
            get
            {
                switch (_time)
                {
                    case TimeOfDay.EarlyNight: return "초밤";
                    case TimeOfDay.LateNight: return "늦은 밤";
                    default: return "낮";
                }
            }
        }

        /// <summary>밤에 씬을 몇 번 옮겼는가 — 늦은 밤 전환 조건 ②.</summary>
        public static int NightSceneTransitions => _nightSceneTransitions;

        /// <summary>
        /// 씬을 옮겼다고 알린다 (<see cref="SceneTransition"/>이 부른다).
        /// 초밤에 3번 이상 옮기면 늦은 밤으로 넘어간다 — 정규 경로는 견우마을(B5)이다.
        /// </summary>
        public static void NotifySceneTransition()
        {
            if (_time != TimeOfDay.EarlyNight) return;
            _nightSceneTransitions++;
            TryAdvanceToLateNight();
            AfterChange();
        }

        static void TryAdvanceToLateNight()
        {
            if (_time != TimeOfDay.EarlyNight) return;
            bool byB5 = HasClue(ClueId.B5);                 // ① 견우마을을 다녀왔다 (정규)
            bool byWalk = _nightSceneTransitions >= 3;      // ② 밤이 된 뒤 씬을 3번 이상 옮겼다
            if (!byB5 && !byWalk) return;

            Time = TimeOfDay.LateNight;
            Debug.Log("[제3사건] 늦은 밤 — 관아 잠입이 가능해졌다 (" + (byB5 ? "견우마을 귀환" : "씬 이동 3회") + ")");
        }

        // ─────────────────────────────────────────────────────────
        //  진행
        // ─────────────────────────────────────────────────────────

        public static Act CurrentAct
        {
            get => _act;
            set { if (_act == value) return; _act = value; AfterChange(); }
        }

        /// <summary>선아 집 열쇠를 받았는가 (신뢰도 40 임계).</summary>
        public static bool HasSeonaHouseKey
        {
            get => _hasSeonaHouseKey;
            set { if (_hasSeonaHouseKey == value) return; _hasSeonaHouseKey = value; AfterChange(); }
        }

        /// <summary>선아를 구출했는가. 엔딩 판정의 절반.</summary>
        public static bool SeonaRescued
        {
            get => _seonaRescued;
            set
            {
                if (_seonaRescued == value) return;
                _seonaRescued = value;
                if (value) Debug.Log("[제3사건] 선아 구출 — 신뢰도 시스템의 역할은 여기서 끝난다.");
                AfterChange();
            }
        }

        /// <summary>밤을 하나 넘긴다 — 경계도 70 이후의 제한 시간을 깎는다.</summary>
        public static void ConsumeNight()
        {
            if (_nightsRemaining <= 0) return;
            _nightsRemaining--;
            Debug.Log("[제3사건] 남은 밤 " + _nightsRemaining);
            AfterChange();
        }

        // ─────────────────────────────────────────────────────────
        //  임계 이벤트 — 전부 1회성·비가역
        // ─────────────────────────────────────────────────────────

        public static bool Fired(Threshold t) => _thresholdFired.Contains(t);

        static void EvaluateThresholds()
        {
            int trust = Trust, alert = Alert;

            if (trust >= 40) Fire(Threshold.Trust40);
            if (trust >= 70) Fire(Threshold.Trust70);
            if (alert >= 40) Fire(Threshold.Alert40);
            if (alert >= 70) Fire(Threshold.Alert70);
            if (alert >= 100) Fire(Threshold.Alert100);
        }

        static void Fire(Threshold t)
        {
            if (!_thresholdFired.Add(t)) return;

            switch (t)
            {
                case Threshold.Trust40:
                    Debug.Log("[제3사건] 임계 — 신뢰도 40. 밤에 선아 집 마당에서 견우가 열쇠를 준다.");
                    break;

                case Threshold.Trust70:
                    // 하늘을 직접 볼 수 없게 만들어 관측실이 필요해진다 (문서 「3. 날씨」)
                    Weather = Weather.Rain;
                    Debug.Log("[제3사건] 임계 — 신뢰도 70. 타공 지도와 진짜 증언. 날씨가 비로 바뀐다.");
                    break;

                case Threshold.Alert40:
                    _ledgerBurned = true;
                    if (!HasClue(ClueId.C5))
                    {
                        _c5LostForever = true;
                        Debug.LogWarning("[제3사건] 임계 — 경계도 40. 장부가 탔고 C5는 영구 소실됐다.");
                    }
                    else Debug.Log("[제3사건] 임계 — 경계도 40. 장부 일부 소각 (C5는 이미 확보).");
                    break;

                case Threshold.Alert70:
                    _nightsRemaining = 2;
                    Debug.LogWarning("[제3사건] 임계 — 경계도 70. 서고 진입까지 밤 2회.");
                    // ⚠️ 시간 제한은 모르면 불공정하다 — 반드시 명시적으로 알린다 (문서 「12」)
                    if (Application.isPlaying)
                        DebugToast.Show("수령이 움직이기 시작했다. 서두르는 편이 좋겠다.", 6f);
                    break;

                case Threshold.Alert100:
                    _forcedEnding = EndingId.LegendComplete;
                    Debug.LogWarning("[제3사건] 임계 — 경계도 100. 수령이 서고를 먼저 정리했다. '전설의 완성' 고정.");
                    break;
            }

            ThresholdFired?.Invoke(t);
        }

        // ─────────────────────────────────────────────────────────
        //  엔딩
        // ─────────────────────────────────────────────────────────

        /// <summary>지금 상태로 끝난다면 어떤 엔딩인가.</summary>
        public static EndingId Ending
        {
            get
            {
                if (_forcedEnding != EndingId.None) return _forcedEnding;
                if (_seonaRescued) return Proven ? EndingId.Truth : EndingId.HalfSalvation;
                return Proven ? EndingId.LateDoor : EndingId.LegendComplete;
            }
        }

        public static string EndingLabel
        {
            get
            {
                switch (Ending)
                {
                    case EndingId.Truth: return "진상";
                    case EndingId.HalfSalvation: return "절반의 구원";
                    case EndingId.LateDoor: return "늦은 문";
                    default: return "전설의 완성";
                }
            }
        }

        /// <summary>
        /// 사건을 끝맺는다 — 공통 <see cref="GameState"/>에 판결까지 기록한다.
        /// 진상=Truth / 절반의 구원·늦은 문=Mercy / 전설의 완성=AcceptFake 로 옮긴다.
        /// </summary>
        public static EndingId FinishCase()
        {
            var e = Ending;
            Verdict v;
            switch (e)
            {
                case EndingId.Truth: v = Verdict.Truth; break;
                case EndingId.HalfSalvation:
                case EndingId.LateDoor: v = Verdict.Mercy; break;
                default: v = Verdict.AcceptFake; break;
            }
            CurrentAct = Act.Ending;
            GameState.Instance.SetVerdict(CaseId.Case3_Gyeonu, v);
            Debug.Log("[제3사건] 엔딩 = " + EndingLabel + " → 판결 " + v);
            return e;
        }

        // ─────────────────────────────────────────────────────────
        //  디버그 — 3축을 직접 만지기
        // ─────────────────────────────────────────────────────────

        /// <summary>신뢰도를 그 값으로 맞춘다 (보정치를 조절하는 방식이라 단서 가산과 어긋나지 않는다).</summary>
        public static void SetTrust(int value)
        {
            _trustOffset += Mathf.Clamp(value, 0, 100) - Trust;
            AfterChange();
        }

        public static void SetEvidence(int value)
        {
            _evidenceOffset += Mathf.Clamp(value, 0, 100) - Evidence;
            AfterChange();
        }

        public static void SetAlert(int value)
        {
            _alertOffset += Mathf.Clamp(value, 0, 100) - Alert;
            AfterChange();
        }

        public static void AddTrust(int delta) => SetTrust(Trust + delta);
        public static void AddEvidence(int delta) => SetEvidence(Evidence + delta);
        public static void AddAlert(int delta) => SetAlert(Alert + delta);

        /// <summary>임계 발화 기록만 지운다 (디버그 — 같은 연출을 다시 보려고).</summary>
        public static void ClearThresholds()
        {
            _thresholdFired.Clear();
            _ledgerBurned = _c5LostForever = false;
            _nightsRemaining = 0;
            _forcedEnding = EndingId.None;
            AfterChange();
        }

        /// <summary>전부 초기화.</summary>
        public static void ResetAll()
        {
            HardReset();
            Debug.Log("[제3사건] 상태 전체 초기화");
            Changed?.Invoke();
        }

        static void HardReset()
        {
            _clues.Clear(); _presented.Clear(); _contradictions.Clear();
            _thresholdFired.Clear(); _flags.Clear();
            _alertTopicHits.Clear(); _npcInsult.Clear();
            _gyeonuFavor = _gyeonuPressure = _gyeonuInsult = 0;
            _trustOffset = _evidenceOffset = _alertOffset = 0;
            _identityRevealed = _identitySpread = false;
            _seonaRescued = _hasSeonaHouseKey = false;
            _ledgerBurned = _c5LostForever = false;
            _nightSceneTransitions = _nightsRemaining = 0;
            _act = Act.Village;
            _time = TimeOfDay.Day;
            _weather = Weather.Clear;
            _forcedEnding = EndingId.None;
            DebugIgnoreConditions = false;
            TimeSeeded = false;
        }

        // ─────────────────────────────────────────────────────────
        //  내부
        // ─────────────────────────────────────────────────────────

        static void AfterChange()
        {
            TryAdvanceToLateNight();
            EvaluateThresholds();
            Changed?.Invoke();
        }

        // ── 세이브 훅 (GyeonuSave가 쓴다) ────────────────────────

        internal static void CaptureInto(GyeonuSaveData d)
        {
            d.clues = ToCodes(_clues);
            d.presented = ToCodes(_presented);
            d.contradictions = new List<string>();
            foreach (var c in _contradictions) d.contradictions.Add(c.ToString());
            d.thresholds = new List<string>();
            foreach (var t in _thresholdFired) d.thresholds.Add(t.ToString());
            d.flags = new List<string>(_flags);

            d.alertTopics = new List<string>(); d.alertHits = new List<int>();
            foreach (var kv in _alertTopicHits) { d.alertTopics.Add(kv.Key.ToString()); d.alertHits.Add(kv.Value); }
            d.npcs = new List<string>(); d.npcInsults = new List<int>();
            foreach (var kv in _npcInsult) { d.npcs.Add(kv.Key.ToString()); d.npcInsults.Add(kv.Value); }

            d.gyeonuFavor = _gyeonuFavor; d.gyeonuPressure = _gyeonuPressure; d.gyeonuInsult = _gyeonuInsult;
            d.trustOffset = _trustOffset; d.evidenceOffset = _evidenceOffset; d.alertOffset = _alertOffset;
            d.identityRevealed = _identityRevealed; d.identitySpread = _identitySpread;
            d.seonaRescued = _seonaRescued; d.hasSeonaHouseKey = _hasSeonaHouseKey;
            d.ledgerBurned = _ledgerBurned; d.c5Lost = _c5LostForever;
            d.nightSceneTransitions = _nightSceneTransitions; d.nightsRemaining = _nightsRemaining;
            d.act = (int)_act; d.time = (int)_time; d.weather = (int)_weather;
            d.forcedEnding = (int)_forcedEnding;
            d.timeSeeded = TimeSeeded;
            d.items = Inventory.CaptureKeys();
        }

        internal static void RestoreFrom(GyeonuSaveData d)
        {
            HardReset();
            if (d == null) { Changed?.Invoke(); return; }

            FromCodes(d.clues, _clues);
            FromCodes(d.presented, _presented);
            if (d.contradictions != null)
                foreach (var s in d.contradictions)
                    { ContradictionId c; if (Enum.TryParse(s, out c)) _contradictions.Add(c); }
            if (d.thresholds != null)
                foreach (var s in d.thresholds)
                    { Threshold t; if (Enum.TryParse(s, out t)) _thresholdFired.Add(t); }
            if (d.flags != null) foreach (var f in d.flags) if (!string.IsNullOrEmpty(f)) _flags.Add(f);

            if (d.alertTopics != null && d.alertHits != null)
                for (int i = 0; i < d.alertTopics.Count && i < d.alertHits.Count; i++)
                    { AlertTopic t; if (Enum.TryParse(d.alertTopics[i], out t)) _alertTopicHits[t] = d.alertHits[i]; }
            if (d.npcs != null && d.npcInsults != null)
                for (int i = 0; i < d.npcs.Count && i < d.npcInsults.Count; i++)
                    { NpcId n; if (Enum.TryParse(d.npcs[i], out n)) _npcInsult[n] = d.npcInsults[i]; }

            _gyeonuFavor = d.gyeonuFavor; _gyeonuPressure = d.gyeonuPressure; _gyeonuInsult = d.gyeonuInsult;
            _trustOffset = d.trustOffset; _evidenceOffset = d.evidenceOffset; _alertOffset = d.alertOffset;
            _identityRevealed = d.identityRevealed; _identitySpread = d.identitySpread;
            _seonaRescued = d.seonaRescued; _hasSeonaHouseKey = d.hasSeonaHouseKey;
            _ledgerBurned = d.ledgerBurned; _c5LostForever = d.c5Lost;
            _nightSceneTransitions = d.nightSceneTransitions; _nightsRemaining = d.nightsRemaining;
            _act = (Act)d.act; _time = (TimeOfDay)d.time; _weather = (Weather)d.weather;
            _forcedEnding = (EndingId)d.forcedEnding;
            TimeSeeded = d.timeSeeded;
            Inventory.RestoreKeys(d.items);

            Changed?.Invoke();
        }

        static List<string> ToCodes(HashSet<ClueId> set)
        {
            var l = new List<string>(set.Count);
            foreach (var c in set) l.Add(c.ToString());
            return l;
        }

        static void FromCodes(List<string> codes, HashSet<ClueId> into)
        {
            if (codes == null) return;
            foreach (var s in codes) { ClueId id; if (ClueTable.TryParse(s, out id)) into.Add(id); }
        }
    }
}
