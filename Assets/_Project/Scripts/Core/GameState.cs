using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 세 사건의 진행 상태·판결을 담는 게임 전역 상태.
    /// 씬이 바뀌어도 유지되는 싱글톤(DontDestroyOnLoad)이며,
    /// 조사청(허브)·복명(엔딩)·각 사건 씬이 공유하는 단 하나의 진실 원본.
    ///
    /// ─────────────────────────────────────────────────────────────
    ///  팀원(사건 씬 제작자)이 쓰는 법 — using IMUNROK.Common; 후:
    ///
    ///   // 1) 사건에 처음 진입했을 때 (선택 사항: 허브가 이미 처리해 줌)
    ///   GameState.Instance.StartCase(CaseId.Case1_Onggojip);
    ///
    ///   // 2) 플레이어가 판결을 내렸을 때 — 이 한 줄이면 상태까지 Completed로 바뀜
    ///   GameState.Instance.SetVerdict(CaseId.Case1_Onggojip, Verdict.Mercy);
    ///
    ///   // 3) 제1사건 전용 — 갑리(가짜)를 처리했는지 기록
    ///   GameState.Instance.SetGapriHandled(true);
    ///
    ///   // 4) 판결을 마쳤으면 허브로 돌아가면 됨(씬 로드는 사건 팀 담당).
    /// ─────────────────────────────────────────────────────────────
    /// </summary>
    public class GameState : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  싱글톤
        // ─────────────────────────────────────────────

        private static GameState _instance;

        /// <summary>
        /// 어디서든 접근하는 진입점. 씬에 인스턴스가 없으면 자동 생성하므로,
        /// 팀원은 GameState를 씬에 배치하지 않아도 SetVerdict 등을 바로 호출할 수 있음.
        /// </summary>
        public static GameState Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 이미 씬에 배치돼 있으면 그걸 사용
                    _instance = FindFirstObjectByType<GameState>();

                    // 없으면 런타임에 하나 만들어 유지
                    if (_instance == null)
                    {
                        var go = new GameObject("[GameState]");
                        _instance = go.AddComponent<GameState>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            // 씬 전환 시 중복 생성 방지 + 유지
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureInitialized();
        }

        // ─────────────────────────────────────────────
        //  데이터
        // ─────────────────────────────────────────────

        /// <summary>한 사건의 상태+판결을 묶은 레코드. 인스펙터에서 확인 가능하도록 Serializable.</summary>
        [Serializable]
        public class CaseRecord
        {
            public CaseId id;
            public CaseStatus status = CaseStatus.NotStarted;
            public Verdict verdict = Verdict.None;
        }

        // 인스펙터에서 디버깅으로 볼 수 있게 노출(읽기 전용처럼 취급 — 값 변경은 API로만)
        [SerializeField]
        private List<CaseRecord> _cases = new List<CaseRecord>();

        [Tooltip("제1사건 전용: 갑리(가짜)를 처리했는지 여부")]
        [SerializeField]
        private bool _gapriHandled = false;

        // 현재 플레이어가 들어가 있는 사건(챕터). 조사청(사건 밖)에선 null.
        // 수첩은 이 값에 해당하는 사건의 단서만 보여준다.
        private CaseId? _currentCase = null;

        // CaseId → CaseRecord 빠른 조회용(런타임 캐시)
        private readonly Dictionary<CaseId, CaseRecord> _lookup = new Dictionary<CaseId, CaseRecord>();

        private bool _initialized = false;

        /// <summary>세 사건 레코드를 보장. 최초 접근/Awake 시 1회 세팅.</summary>
        private void EnsureInitialized()
        {
            // _initialized가 true라도 런타임 캐시(_lookup)가 비어 있으면 다시 구성한다.
            // (에디터에서 Play 중 스크립트가 핫 리로드되면 직렬화되지 않는 _lookup이
            //  비워지는데, 이때 재구성하지 않으면 키 조회에서 KeyNotFoundException이 난다.)
            if (_initialized && _lookup.Count == Enum.GetValues(typeof(CaseId)).Length)
                return;

            _lookup.Clear();

            // 세 사건이 항상 존재하도록 채운다(누락/중복 방지).
            foreach (CaseId id in Enum.GetValues(typeof(CaseId)))
            {
                CaseRecord rec = _cases.Find(c => c.id == id);
                if (rec == null)
                {
                    rec = new CaseRecord { id = id };
                    _cases.Add(rec);
                }
                _lookup[id] = rec;
            }

            _initialized = true;
        }

        /// <summary>
        /// 사건 레코드를 안전하게 얻는다. 캐시에 없으면(핫 리로드 등) 예외를 던지지 않고
        /// 즉석에서 만들어 등록한 뒤 반환한다. 모든 읽기/쓰기 API는 이걸 거친다.
        /// </summary>
        private CaseRecord GetRecord(CaseId id)
        {
            EnsureInitialized();

            if (_lookup.TryGetValue(id, out var rec) && rec != null)
                return rec;

            // 방어적 복구: 목록에서 찾고, 없으면 새로 만든다.
            rec = _cases.Find(c => c.id == id);
            if (rec == null)
            {
                rec = new CaseRecord { id = id };
                _cases.Add(rec);
            }
            _lookup[id] = rec;
            return rec;
        }

        // ─────────────────────────────────────────────
        //  이벤트 — 허브 씬이 색/조명 갱신용으로 구독
        // ─────────────────────────────────────────────

        /// <summary>어떤 사건이든 상태 또는 판결이 바뀌면 발생. 인자는 변경된 사건.</summary>
        public event Action<CaseId> OnCaseChanged;

        /// <summary>세 사건이 모두 Completed가 되는 순간 1회 발생(세계가 열리는 연출 트리거).</summary>
        public event Action OnAllCasesCompleted;

        // ─────────────────────────────────────────────
        //  쓰기 API (팀원·허브가 호출)
        // ─────────────────────────────────────────────

        /// <summary>사건을 InProgress로. 이미 Completed면 되돌리지 않음.</summary>
        public void StartCase(CaseId id)
        {
            var rec = GetRecord(id);
            if (rec.status == CaseStatus.Completed) return;

            if (rec.status != CaseStatus.InProgress)
            {
                rec.status = CaseStatus.InProgress;
                RaiseChanged(id);
            }
        }

        /// <summary>
        /// 판결을 기록하고 해당 사건을 Completed로 만든다.
        /// 팀원이 가장 많이 쓰는 핵심 API — 이 한 줄로 상태 완료 처리까지 끝남.
        /// </summary>
        public void SetVerdict(CaseId id, Verdict verdict)
        {
            if (verdict == Verdict.None)
            {
                Debug.LogWarning($"[GameState] {id} 판결을 None으로 설정했습니다. 완료 처리하지 않습니다.");
                var r = GetRecord(id);
                r.verdict = Verdict.None;
                RaiseChanged(id);
                return;
            }

            var rec = GetRecord(id);
            rec.verdict = verdict;
            rec.status = CaseStatus.Completed;
            Debug.Log($"[GameState] {id} 판결 기록: {verdict} → Completed");

            RaiseChanged(id);
            CheckAllCompleted();
        }

        /// <summary>제1사건 전용: 갑리(가짜) 처리 여부 기록.</summary>
        public void SetGapriHandled(bool handled)
        {
            EnsureInitialized();
            _gapriHandled = handled;
            Debug.Log($"[GameState] 갑리 처리 여부: {handled}");
        }

        // ── 현재 사건(챕터) 컨텍스트 ──
        // 사건 씬에 들어가면 EnterCase, 조사청으로 나오면 ExitToHub 를 호출한다.
        // 수첩(JournalView)이 "지금 사건의 단서만" 보여주는 근거가 된다.

        /// <summary>지금 플레이어가 들어가 있는 사건. 조사청(사건 밖)이면 null.</summary>
        public CaseId? CurrentCase => _currentCase;

        /// <summary>사건 안에 있는가(조사청이 아니라).</summary>
        public bool InCase => _currentCase.HasValue;

        /// <summary>사건 씬에 진입할 때 호출(허브의 사건 큐브 선택 시 자동 호출됨).</summary>
        public void EnterCase(CaseId id)
        {
            _currentCase = id;
            Debug.Log($"[GameState] 사건 진입: {id}");
        }

        /// <summary>조사청(사건 밖)으로 나올 때 호출.</summary>
        public void ExitToHub()
        {
            _currentCase = null;
            Debug.Log("[GameState] 조사청(사건 밖)으로 이동");
        }

        /// <summary>모든 상태를 초기화(디버그/재시작용).</summary>
        public void ResetAll()
        {
            EnsureInitialized();
            foreach (var rec in _cases)
            {
                rec.status = CaseStatus.NotStarted;
                rec.verdict = Verdict.None;
            }
            _gapriHandled = false;
            _allCompletedFired = false;
            Debug.Log("[GameState] 전체 상태 초기화");

            foreach (CaseId id in Enum.GetValues(typeof(CaseId)))
                RaiseChanged(id);
        }

        // ─────────────────────────────────────────────
        //  읽기 API (허브·엔딩이 호출)
        // ─────────────────────────────────────────────

        public CaseStatus GetStatus(CaseId id)
        {
            return GetRecord(id).status;
        }

        public Verdict GetVerdict(CaseId id)
        {
            return GetRecord(id).verdict;
        }

        public bool GapriHandled
        {
            get { EnsureInitialized(); return _gapriHandled; }
        }

        /// <summary>Completed된 사건 수(기록대 쌓기·진행도 표시에 사용).</summary>
        public int CompletedCount
        {
            get
            {
                EnsureInitialized();
                int n = 0;
                foreach (var rec in _cases)
                    if (rec.status == CaseStatus.Completed) n++;
                return n;
            }
        }

        /// <summary>세 사건이 전부 Completed인가(세계 열림·엔딩 조건).</summary>
        public bool AllCasesCompleted => CompletedCount >= CaseCount;

        /// <summary>전체 사건 수(항상 3).</summary>
        public int CaseCount
        {
            get { EnsureInitialized(); return _cases.Count; }
        }

        /// <summary>
        /// 엔딩 낭독 순서대로(Case1→2→3) 판결을 반환.
        /// 복명 씬이 자막 낭독과 총평 분기에 사용.
        /// </summary>
        public IReadOnlyList<Verdict> GetVerdictsInOrder()
        {
            EnsureInitialized();
            var list = new List<Verdict>(CaseCount);
            foreach (CaseId id in Enum.GetValues(typeof(CaseId)))
                list.Add(GetRecord(id).verdict);
            return list;
        }

        // ─────────────────────────────────────────────
        //  내부 헬퍼
        // ─────────────────────────────────────────────

        private void RaiseChanged(CaseId id)
        {
            OnCaseChanged?.Invoke(id);
        }

        private bool _allCompletedFired = false;

        private void CheckAllCompleted()
        {
            if (_allCompletedFired) return;
            if (AllCasesCompleted)
            {
                _allCompletedFired = true;
                Debug.Log("[GameState] 세 사건 모두 완료 — 세계가 열립니다.");
                OnAllCasesCompleted?.Invoke();
            }
        }
    }
}
