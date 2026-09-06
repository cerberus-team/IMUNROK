using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 단서 종류. <b>수첩에는 물증만 적힌다</b>(<see cref="Journal.AddClue"/> 참조).
    ///
    /// 정황은 값이 남아 있을 뿐 더는 기록되지 않는다 — 예전에 저장한 파일을 읽을 때
    /// 이 값이 나오므로 이름을 없애지 않는다.
    /// </summary>
    public enum ClueKind { 물증, 정황 }

    /// <summary>수첩에 기록되는 단서 한 줄.</summary>
    [Serializable]
    public class ClueEntry
    {
        public CaseId caseId;
        public string key;   // 중복 기록 방지용 식별자(같은 key는 한 번만)
        public string text;  // 표시 문구
        public ClueKind kind; // 물증/정황

        /// <summary>
        /// 심문에서 들이밀 수 있는가. 사건에 들어설 때 받는 조사종이처럼
        /// "내가 이미 아는 것"은 증거가 아니라 출발점이라 들이밀 수 없다.
        /// 예전에 저장한 파일에는 이 값이 없는데, 그때는 이 초기값(true)이 그대로 남는다.
        /// </summary>
        public bool presentable = true;
    }

    /// <summary>
    /// 수첩(手帖): 조사 중 발견한 단서를 사건별로 기록·누적하는 공통 시스템.
    /// 씬이 바뀌어도 유지되는 싱글톤(GameState와 동일한 방식).
    ///
    /// ─────────────────────────────────────────────────────────────
    ///  팀원(사건 씬 제작자)이 쓰는 법 — using IMUNROK.Common; 후:
    ///
    ///   // 단서를 기록(같은 key면 중복 없이 한 번만 들어감)
    ///   Journal.Instance.AddClue(CaseId.Case1_Onggojip, "mole", "진짜 옹고집의 점은 왼뺨에 있다.");
    ///
    ///   // key 없이 간단히(문구 자체가 key가 됨)
    ///   Journal.Instance.AddClue(CaseId.Case1_Onggojip, "부인은 두 사람을 구별하지 못했다.");
    ///
    ///   // 이미 찾았는지 확인 / 개수
    ///   bool has = Journal.Instance.HasClue(CaseId.Case1_Onggojip, "mole");
    ///   int  n   = Journal.Instance.ClueCount(CaseId.Case1_Onggojip);
    /// ─────────────────────────────────────────────────────────────
    /// </summary>
    public class Journal : MonoBehaviour
    {
        // ── 싱글톤 (GameState와 동일 패턴) ──
        private static Journal _instance;
        /// <summary>지금 게임이 끝나거나 씬이 닫히는 중인가. 이때는 싱글턴을 새로 세우지 않는다.</summary>
        private static bool Quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetQuitFlag()
        {
            Quitting = false;
            Application.quitting -= MarkQuitting;
            Application.quitting += MarkQuitting;
        }

        private static void MarkQuitting() { Quitting = true; }

        public static Journal Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 씬을 닫는 중에는 <b>새로 만들지 않는다</b>.
                    //
                    // 끝내거나 씬을 갈아 끼우는 동안 남의 OnDestroy·OnDisable 이 이 값을
                    // 물어보는데, 그때 하나를 새로 세우면 그것이 정리 뒤에 태어난 것이라
                    // 치울 사람이 없다 — "Some objects were not cleaned up when closing
                    // the scene" 이 그 소리다. 닫는 중이면 없는 대로 null 을 돌려준다.
                    if (Quitting) return null;

                    _instance = FindFirstObjectByType<Journal>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[Journal]");
                        _instance = go.AddComponent<Journal>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── 데이터 ──
        [SerializeField] private List<ClueEntry> _clues = new List<ClueEntry>();

        /// <summary>새 단서가 기록될 때 발생(UI 갱신 등에 구독).</summary>
        public event Action<ClueEntry> OnClueAdded;

        // ── 쓰기 API ──

        // ── 증거 그림(런타임 전용: 에셋 참조라 저장 안 하고, 다시 얻을 때 재설정) ──
        private readonly Dictionary<string, Texture2D> _clueImages = new Dictionary<string, Texture2D>();
        private static string ImgKey(CaseId c, string key) => ((int)c) + ":" + key;

        /// <summary>
        /// 단서를 기록. 같은 (사건,key)는 한 번만. 새로 기록되면 true.
        ///
        /// <b>물증만 적힌다</b>. 정황(<see cref="ClueKind.정황"/>)을 넘기면 조용히 흘린다.
        ///
        /// 왜인가: 수첩은 <b>주운 것</b>을 담는 그릇이다. 증언·목격·실토는 손에 쥔 것이
        /// 아니라 그 자리에서 <b>들은 말</b>이고, 그것까지 적어 두면 들이밀 것과 들은 것이
        /// 한 장에 섞여 어느 쪽이 상대의 입을 여는 물건인지 알 수 없게 된다. 들은 말은
        /// 심문 자막에서 <b>붉게</b> 한 번 지나가고 사라진다 — 받아 적는 것은 조사관의 몫이다.
        ///
        /// 흘리더라도 그림은 받아 둔다. 뒤에 같은 key 가 물증으로 들어올 수 있다.
        /// </summary>
        public bool AddClue(CaseId caseId, string key, string text, Texture2D image = null,
                            ClueKind kind = ClueKind.물증, bool presentable = true)
        {
            if (string.IsNullOrEmpty(key)) key = text;
            if (image != null) _clueImages[ImgKey(caseId, key)] = image;
            if (kind != ClueKind.물증) return false;
            if (HasClue(caseId, key)) return false;

            var entry = new ClueEntry { caseId = caseId, key = key, text = text, kind = kind, presentable = presentable };
            _clues.Add(entry);
            Debug.Log($"[Journal] 단서 기록: [{caseId}] {text}");
            OnClueAdded?.Invoke(entry);
            JournalPanel.Refresh();   // 수첩을 펼쳐둔 채 단서를 얻어도 바로 반영되게
            return true;
        }

        /// <summary>
        /// 이미 적힌 단서를 <b>고쳐 적는다</b> — 같은 것을 더 알게 되었을 때.
        ///
        /// 새로 안 것을 죄다 새 줄로 적으면 수첩이 금세 스무 줄이 되고, 그중 어느 둘이
        /// 같은 이야기인지 알 수 없게 된다. 장부의 필적이 다르다는 것과 그 두 줄을 미리
        /// 연습한 자국이 있다는 것은 <b>한 가지 일</b>이다 — 뒤엣것은 앞엣것을 굳힐 뿐이다.
        /// 그러니 줄을 늘리지 않고 그 줄을 고쳐 적는다.
        /// </summary>
        /// <returns>고쳐 적었으면 true. 그런 단서가 아직 없으면 false(그때는 새로 적을 것).</returns>
        public bool UpgradeClue(CaseId caseId, string key, string text)
        {
            var e = _clues.Find(c => c.caseId == caseId && c.key == key);
            if (e == null || string.IsNullOrEmpty(text) || e.text == text) return false;
            e.text = text;
            Debug.Log($"[Journal] 단서를 고쳐 적음: [{caseId}] {text}");
            JournalPanel.Refresh();
            return true;
        }

        /// <summary>key 없이 기록(문구 자체를 key로).</summary>
        public bool AddClue(CaseId caseId, string text) => AddClue(caseId, text, text);

        /// <summary>단서에 딸린 증거 그림(없으면 null).</summary>
        public Texture2D GetClueImage(CaseId caseId, string key)
            => _clueImages.TryGetValue(ImgKey(caseId, key), out var t) ? t : null;

        // ── 물증에 딸린 문서(런타임 전용) ──

        /// <summary>수첩에서 다시 펼쳐 볼 수 있는 한 장.</summary>
        public class ClueDocument
        {
            public Texture2D page;
            public string title;
            public string body;
            public string fine;    // 돋보기로만 읽히는 잔글씨
            public Texture2D back;  // 뒤에 새겨진 것 — 뒤집어야 나온다

            /// <summary>종이가 아닌 것 — 무대에 세워 돌려 보는 3D 모델(있으면).</summary>
            public GameObject model;
            /// <summary>무대에 올릴 때의 첫 자세. 마패는 눕혀 두면 앞이 안 보인다.</summary>
            public Vector3 modelEuler;

            /// <summary>
            /// <b>사건 종이인가</b> — 어전에서 받아 온 봉서. 물증이 아니라 <b>출발점</b>이다.
            ///
            /// 조사청에서 수첩을 펴면 이것만 보인다. 물증은 사건 안의 것이라 사건을
            /// 나오면 안 보이는 것이 맞지만, 무엇을 맡았는지는 방을 나와서도 알아야 한다.
            /// </summary>
            public bool caseSheet;

            /// <summary>어느 사건의 것인가. 조사청에서 늘어놓을 때 차례를 잡는 데 쓴다.</summary>
            public CaseId caseId;
        }

        private readonly Dictionary<string, ClueDocument> _clueDocs = new Dictionary<string, ClueDocument>();

        /// <summary>
        /// 이 단서의 종이를 함께 적어 둔다 — 수첩에서 다시 펼쳐 볼 수 있게.
        ///
        /// 정황(증언·목격)은 들은 것이라 다시 볼 것이 없지만, 물증은 손에 잡히는 종이다.
        /// 심문 도중 "그 장부에 뭐라 적혀 있었더라" 하고 되짚을 때 방으로 돌아갈 수는 없다.
        /// </summary>
        public void AttachDocument(CaseId caseId, string key, Texture2D page,
                                   string title, string body, string fine = null,
                                   Texture2D back = null,
                                   GameObject model = null, Vector3 modelEuler = default,
                                   bool caseSheet = false)
        {
            // <b>종이가 없어도 받는다.</b> 마패·유척처럼 종이가 아닌 물증은 펼칠 장이
            // 없고 세울 모델만 있다. 여태 page 가 없으면 그냥 돌아 나갔다.
            if (string.IsNullOrEmpty(key)) return;
            if (page == null && model == null) return;
            _clueDocs[ImgKey(caseId, key)] = new ClueDocument
            { page = page, title = title, body = body, fine = fine, back = back,
              model = model, modelEuler = modelEuler, caseSheet = caseSheet, caseId = caseId };
        }

        /// <summary>
        /// <b>받아 온 사건 종이들</b> — 사건 차례대로.
        ///
        /// 조사청 수첩이 펴 보이는 것이 이것뿐이다. 사건 안의 물증은 사건 안에서 본다.
        /// </summary>
        public List<ClueDocument> CaseSheets()
        {
            var list = new List<ClueDocument>();
            foreach (var kv in _clueDocs)
                if (kv.Value != null && kv.Value.caseSheet) list.Add(kv.Value);
            list.Sort((a, b) => ((int)a.caseId).CompareTo((int)b.caseId));
            return list;
        }

        /// <summary>이 단서에 딸린 문서(없으면 null).</summary>
        public ClueDocument GetDocument(CaseId caseId, string key)
        {
            ClueDocument d;
            return _clueDocs.TryGetValue(ImgKey(caseId, key), out d) ? d : null;
        }

        // ── 첫 장(사건 개요) ──

        private readonly Dictionary<CaseId, string> _briefs = new Dictionary<CaseId, string>();

        /// <summary>
        /// 수첩 첫 장에 적히는 사건 개요(조사종이 요지).
        ///
        /// 예전에는 이것도 단서 한 줄로 밀어 넣었으나, 조사종이는 <b>내가 이미 아는 것</b>이라
        /// 주운 물증이 아니다. 그래서 단서 목록이 아니라 따로 둔다.
        /// </summary>
        public void SetBrief(CaseId caseId, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _briefs[caseId] = text.Trim();
            JournalPanel.Refresh();
        }

        /// <summary>이 사건의 개요(없으면 null).</summary>
        public string GetBrief(CaseId caseId)
        {
            string s;
            return _briefs.TryGetValue(caseId, out s) ? s : null;
        }

        /// <summary>모두 지움(디버그/재시작용).</summary>
        public void ClearAll()
        {
            _briefs.Clear();
            _clues.Clear();
            _clueImages.Clear();   // 그림 참조도 같이 버린다(안 지우면 텍스처를 계속 붙들고 있음)
            _clueDocs.Clear();
            Debug.Log("[Journal] 수첩 초기화");
        }

        /// <summary>한 사건의 단서만 지움.</summary>
        public void ClearCase(CaseId caseId)
        {
            foreach (var c in _clues)
                if (c.caseId == caseId) _clueImages.Remove(ImgKey(caseId, c.key));
            _clues.RemoveAll(c => c.caseId == caseId);
        }

        // ── 읽기 API ──

        public bool HasClue(CaseId caseId, string key)
        {
            return _clues.Exists(c => c.caseId == caseId && c.key == key);
        }

        /// <summary>해당 사건의 단서 목록.</summary>
        public IReadOnlyList<ClueEntry> GetClues(CaseId caseId)
        {
            return _clues.FindAll(c => c.caseId == caseId);
        }

        /// <summary>모든 단서.</summary>
        public IReadOnlyList<ClueEntry> AllClues => _clues;

        public int ClueCount(CaseId caseId)
        {
            int n = 0;
            foreach (var c in _clues) if (c.caseId == caseId) n++;
            return n;
        }

        public int TotalClueCount => _clues.Count;

        // ── 저장 / 불러오기 (SaveSystem이 호출) ──

        [Serializable]
        private class SaveDTO { public List<ClueEntry> clues; }

        public string ToJson() => JsonUtility.ToJson(new SaveDTO { clues = _clues });

        public void FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var d = JsonUtility.FromJson<SaveDTO>(json);
            if (d == null || d.clues == null) return;
            _clues.Clear();
            _clues.AddRange(d.clues);
        }
    }
}
