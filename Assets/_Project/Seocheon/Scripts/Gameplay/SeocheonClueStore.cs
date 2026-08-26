// 지목한 조각을 서천 전용으로 보관합니다. 수첩에는 문장만 남고, clueId 는 내부에만 있습니다.
using System;
using System.Collections.Generic;
using IMUNROK.Common;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 수첩 한 줄에 대응하는 기록.
    ///
    /// ★수첩에 보이는 것은 <see cref="sentence"/> 뿐입니다.
    ///   어느 어절을 짚었는지(words)와 그것이 유효 조각인지(clueIds)는 ★내부 전용이며
    ///   ★수집 시점에는 플레이어에게 어떤 형태로도 드러나지 않습니다.
    ///   판정은 나중에 조각을 ★결합할 때만 일어납니다.
    /// </summary>
    [Serializable]
    public sealed class SeocheonClueRecord
    {
        [Tooltip("수첩 key. ★유효/오답과 무관하게 같은 형식입니다")]
        public string entryId = string.Empty;

        [Tooltip("수첩에 표시되는 문구 = 그 어절이 들어 있던 문장 전체")]
        public string sentence = string.Empty;

        [Tooltip("이 말을 한 인물")]
        public string sourceNpc = string.Empty;

        [Tooltip("이 문장에서 지목된 어절들. ★내부 전용")]
        public List<string> words = new List<string>();

        [Tooltip("그중 유효 조각의 ID. 비어 있으면 아직 의미가 밝혀지지 않은 것. ★내부 전용")]
        public List<string> clueIds = new List<string>();

        [Tooltip("결합으로 생긴 카드인지. ★이건 숨길 필요가 없습니다 — 이미 판정이 끝난 것입니다")]
        public bool isDerived;

        [Tooltip("결합 유형 표식(모순 / 연결 / 결론). 결합 결과에만 붙습니다")]
        public string combineKind = string.Empty;

        [Tooltip("이 결과를 만든 재료 카드들")]
        public List<string> sourceEntryIds = new List<string>();

        [Tooltip("카드 앞면에 쓸 글자. 수집 카드는 지목한 어절, 결합 카드는 결과 문구")]
        public string faceText = string.Empty;

        /// <summary>★결합 판정용. UI 에 노출하면 안 됩니다.</summary>
        public bool HasMeaning { get { return clueIds.Count > 0; } }

        public bool Carries(string clueId)
        {
            if (string.IsNullOrEmpty(clueId)) return false;
            for (int i = 0; i < clueIds.Count; i++) if (clueIds[i] == clueId) return true;
            return false;
        }
    }

    /// <summary>
    /// 서천 조각 저장소.
    ///
    /// ★왜 Journal 만으로 부족한가:
    ///   공통 Journal 은 { key, text } 뿐이라 "이 줄이 사실은 A1 이다" 를 담을 자리가 없습니다.
    ///   공통 파트는 수정 금지이므로, 서천은 여기에 그림자 기록을 두고
    ///   Journal 에는 ★형태가 동일한 항목만 밀어 넣습니다.
    ///
    /// ★수첩 key 형식은 SC001, SC002 … 로 전부 같습니다. 접두사로 유효/오답을 나누지 않습니다.
    /// </summary>
    public static class SeocheonClueStore
    {
        private static readonly List<SeocheonClueRecord> records = new List<SeocheonClueRecord>();

        /// <summary>
        /// ★static 이라 도메인 리로드를 끄면 Play 를 멈춰도 기록이 남습니다.
        /// 그대로 두면 다음 플레이에 ★지난 세션의 조각이 섞여 들어옵니다.
        /// 매 실행 시작에 반드시 비웁니다(세이브 복원은 그 뒤에 FromJson 으로).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            records.Clear();
            Changed = null;   // ★구독도 함께 비웁니다. 안 그러면 지난 세션의 UI 가 물려 있습니다.
        }

        /// <summary>기록이 하나 늘 때마다. UI 가 폴링하지 않도록.</summary>
        public static event System.Action Changed;

        public static IReadOnlyList<SeocheonClueRecord> Records { get { return records; } }
        public static int Count { get { return records.Count; } }

        /// <summary>
        /// 지목 결과를 기록합니다. ★유효/오답을 구분하지 않고 같은 경로로 들어옵니다.
        ///
        /// 같은 인물의 같은 문장은 ★한 줄로 합칩니다.
        /// (한 문장에서 어절을 두 개 짚어도 수첩에 같은 문장이 두 번 적히지 않게)
        /// </summary>
        /// <param name="clueId">유효 조각이면 그 ID, 아니면 비워 둡니다. ★수첩에는 나가지 않습니다.</param>
        public static SeocheonClueRecord Add(string sourceNpc, string sentence, string word, string clueId)
        {
            if (string.IsNullOrEmpty(sentence)) return null;

            SeocheonClueRecord rec = Find(sourceNpc, sentence);
            if (rec == null)
            {
                rec = new SeocheonClueRecord();
                rec.entryId = MakeEntryId();
                rec.sentence = sentence;
                rec.sourceNpc = sourceNpc ?? string.Empty;
                records.Add(rec);
            }

            if (!string.IsNullOrEmpty(word) && !rec.words.Contains(word)) rec.words.Add(word);
            if (!string.IsNullOrEmpty(clueId) && !rec.clueIds.Contains(clueId)) rec.clueIds.Add(clueId);

            // 카드 앞면은 ★지목한 어절만. 여러 개면 가운뎃점으로 잇습니다.
            rec.faceText = rec.words.Count > 0 ? string.Join(" · ", rec.words.ToArray()) : rec.sentence;

            // ★수첩에는 문장만. key 형식도 동일. 같은 key 는 Journal 이 알아서 한 번만 넣습니다.
            Journal.Instance.AddClue(CaseId.Case2_Seocheon, rec.entryId, rec.sentence);
            return rec;
        }

        /// <summary>
        /// ★결합으로 새 카드를 만듭니다. 수집 카드와 달리 이건 ★숨길 것이 없습니다
        /// (이미 판정이 끝났고, 결과 문구를 그대로 보여 줍니다).
        /// </summary>
        public static SeocheonClueRecord AddDerived(string resultText, string resultClueId,
                                                    string kind, IList<string> sourceEntryIds)
        {
            if (string.IsNullOrEmpty(resultText)) return null;

            SeocheonClueRecord rec = new SeocheonClueRecord();
            rec.entryId = MakeEntryId();
            rec.sentence = resultText;
            rec.faceText = resultText;
            rec.sourceNpc = string.Empty;
            rec.isDerived = true;
            rec.combineKind = kind ?? string.Empty;
            if (!string.IsNullOrEmpty(resultClueId)) rec.clueIds.Add(resultClueId);
            if (sourceEntryIds != null)
                for (int i = 0; i < sourceEntryIds.Count; i++)
                    if (!string.IsNullOrEmpty(sourceEntryIds[i])) rec.sourceEntryIds.Add(sourceEntryIds[i]);

            records.Add(rec);
            Journal.Instance.AddClue(CaseId.Case2_Seocheon, rec.entryId, rec.sentence);
            if (Changed != null) Changed();
            return rec;
        }

        public static SeocheonClueRecord Get(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return null;
            for (int i = 0; i < records.Count; i++) if (records[i].entryId == entryId) return records[i];
            return null;
        }

        /// <summary>이 clueId 를 이미 가진 기록이 있는가. ★단서 게이팅용(AI 에게 재제시 금지 통보).</summary>
        public static bool HasClue(string clueId)
        {
            if (string.IsNullOrEmpty(clueId)) return false;
            for (int i = 0; i < records.Count; i++) if (records[i].Carries(clueId)) return true;
            return false;
        }

        /// <summary>지금까지 모인 유효 조각 ID 를 채웁니다(중복 없음).</summary>
        public static void CollectClueIds(List<string> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            for (int i = 0; i < records.Count; i++)
            {
                List<string> ids = records[i].clueIds;
                for (int j = 0; j < ids.Count; j++)
                    if (!buffer.Contains(ids[j])) buffer.Add(ids[j]);
            }
        }

        public static void Clear()
        {
            records.Clear();
        }

        private static SeocheonClueRecord Find(string npc, string sentence)
        {
            for (int i = 0; i < records.Count; i++)
                if (records[i].sentence == sentence && records[i].sourceNpc == (npc ?? string.Empty))
                    return records[i];
            return null;
        }

        private static string MakeEntryId()
        {
            // 형식이 항상 같아야 합니다. 번호만 늘어납니다.
            int n = records.Count + 1;
            for (int guard = 0; guard < 10000; guard++)
            {
                string id = "SC" + n.ToString("000");
                if (Get(id) == null) return id;
                n++;
            }
            return "SC" + (records.Count + 1);
        }

        // ─────────────────────────────────────────────
        //  저장 (나중에 SaveSystem 에 붙일 자리)
        // ─────────────────────────────────────────────

        [Serializable] private class SaveDto { public List<SeocheonClueRecord> items; }

        public static string ToJson()
        {
            SaveDto dto = new SaveDto();
            dto.items = records;
            return JsonUtility.ToJson(dto);
        }

        public static void FromJson(string json)
        {
            records.Clear();
            if (string.IsNullOrEmpty(json)) return;
            SaveDto dto = JsonUtility.FromJson<SaveDto>(json);
            if (dto != null && dto.items != null) records.AddRange(dto.items);
        }
    }
}
