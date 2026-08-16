using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>단서 종류 — 수첩 탭 분류. 물증(찾은 물건) / 정황(증언·목격·상황 그림).</summary>
    public enum ClueKind { 물증, 정황 }

    /// <summary>수첩에 기록되는 단서 한 줄.</summary>
    [Serializable]
    public class ClueEntry
    {
        public CaseId caseId;
        public string key;   // 중복 기록 방지용 식별자(같은 key는 한 번만)
        public string text;  // 표시 문구
        public ClueKind kind; // 물증/정황
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
        public static Journal Instance
        {
            get
            {
                if (_instance == null)
                {
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

        /// <summary>단서를 기록. 같은 (사건,key)는 한 번만. 새로 기록되면 true.
        /// image: 상황 그림(선택), kind: 물증/정황(수첩 탭 분류).</summary>
        public bool AddClue(CaseId caseId, string key, string text, Texture2D image = null, ClueKind kind = ClueKind.물증)
        {
            if (string.IsNullOrEmpty(key)) key = text;
            if (image != null) _clueImages[ImgKey(caseId, key)] = image;
            if (HasClue(caseId, key)) return false;

            var entry = new ClueEntry { caseId = caseId, key = key, text = text, kind = kind };
            _clues.Add(entry);
            Debug.Log($"[Journal] 단서 기록: [{caseId}] {text}");
            OnClueAdded?.Invoke(entry);
            JournalPanel.Refresh();   // 수첩을 펼쳐둔 채 단서를 얻어도 바로 반영되게
            return true;
        }

        /// <summary>key 없이 기록(문구 자체를 key로).</summary>
        public bool AddClue(CaseId caseId, string text) => AddClue(caseId, text, text);

        /// <summary>단서에 딸린 증거 그림(없으면 null).</summary>
        public Texture2D GetClueImage(CaseId caseId, string key)
            => _clueImages.TryGetValue(ImgKey(caseId, key), out var t) ? t : null;

        /// <summary>모두 지움(디버그/재시작용).</summary>
        public void ClearAll()
        {
            _clues.Clear();
            _clueImages.Clear();   // 그림 참조도 같이 버린다(안 지우면 텍스처를 계속 붙들고 있음)
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
