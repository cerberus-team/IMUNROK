using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// <see cref="GyeonuCase"/> 한 판을 통째로 담는 그릇.
    /// JsonUtility가 다룰 수 있게 전부 평평한 배열·기본형이다
    /// (HashSet·Dictionary는 직렬화되지 않으므로 문자열 목록으로 편다).
    ///
    /// ⚠️ 필드 이름이 곧 저장 형식이다. 이름을 바꾸면 옛 저장이 깨진다 — 덧붙이기만 할 것.
    /// </summary>
    [Serializable]
    public class GyeonuSaveData
    {
        public int version = 1;

        public List<string> clues;
        public List<string> presented;
        public List<string> contradictions;
        public List<string> thresholds;
        public List<string> flags;
        public List<string> items;              // 소지품 id

        public List<string> alertTopics;        // 경계도 화제별 횟수 (키)
        public List<int> alertHits;             //                     (값)
        public List<string> npcs;               // NPC 무례 누적 (키)
        public List<int> npcInsults;            //                (값)

        public int gyeonuFavor, gyeonuPressure, gyeonuInsult;
        public int trustOffset, evidenceOffset, alertOffset;
        public bool identityRevealed, identitySpread;
        public bool seonaRescued, hasSeonaHouseKey;
        public bool ledgerBurned, c5Lost;
        public int nightSceneTransitions, nightsRemaining;
        public int act, time, weather, forcedEnding;
        public bool timeSeeded;
    }

    /// <summary>
    /// 제3사건 상태의 저장·불러오기.
    ///
    /// ■ 왜 공통 <see cref="SaveSystem"/>에 합치지 않았는가
    ///   공통 폴더는 읽기 전용이라 손댈 수 없다. 그래서 같은 폴더에 <b>파일만 따로</b> 둔다.
    ///   나중에 공통 담당자가 훅을 열어 주면 <see cref="ToJson"/>/<see cref="FromJson"/>
    ///   두 줄을 그쪽 Combined DTO에 끼워 넣으면 끝이다 — 이 클래스는 그대로 지워도 된다.
    ///
    /// ■ 지금 실제로 필요한 것은 세션 유지뿐
    ///   씬을 오가도 남는 것은 <see cref="GyeonuCase"/>의 정적 필드가 이미 해 준다.
    ///   여기 파일 저장은 "앱을 껐다 켜도" 를 위한 것이고, 붙일 자리를 미리 잡아 둔 것이다.
    /// </summary>
    public static class GyeonuSave
    {
        const string FileName = "imunrok_gyeonu.json";

        static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave => File.Exists(Path);

        /// <summary>현재 상태를 JSON 문자열로. (공통 세이브에 끼워 넣을 때 이 값을 넘긴다)</summary>
        public static string ToJson()
        {
            var d = new GyeonuSaveData();
            GyeonuCase.CaptureInto(d);
            return JsonUtility.ToJson(d);
        }

        /// <summary>JSON 문자열에서 상태를 복원.</summary>
        public static void FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var d = JsonUtility.FromJson<GyeonuSaveData>(json);
            GyeonuCase.RestoreFrom(d);
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(Path, ToJson());
                Debug.Log("[제3사건] 저장 완료 → " + Path);
            }
            catch (Exception e) { Debug.LogWarning("[제3사건] 저장 실패: " + e.Message); }
        }

        public static bool Load()
        {
            if (!HasSave) { Debug.Log("[제3사건] 저장 파일이 없다."); return false; }
            try
            {
                FromJson(File.ReadAllText(Path));
                Debug.Log("[제3사건] 불러오기 완료");
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[제3사건] 불러오기 실패: " + e.Message); return false; }
        }

        public static void Delete()
        {
            try { if (HasSave) File.Delete(Path); Debug.Log("[제3사건] 저장 삭제"); }
            catch (Exception e) { Debug.LogWarning("[제3사건] 삭제 실패: " + e.Message); }
        }
    }
}
