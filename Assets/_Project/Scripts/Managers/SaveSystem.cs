using System;
using System.IO;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 저장/불러오기 — GameState(판결·진행)와 Journal(단서)을 하나의 JSON 파일로 저장한다.
    /// 파일 위치: Application.persistentDataPath (앱을 껐다 켜도 유지되는 사용자 폴더).
    ///
    /// 사용 예:
    ///   SaveSystem.Save();          // 현재 진행 저장
    ///   if (SaveSystem.HasSave) SaveSystem.Load();  // 있으면 불러오기
    ///   SaveSystem.Delete();        // 저장 삭제(새로 시작)
    ///
    /// 자동 저장을 원하면: GameState.Instance.OnCaseChanged += _ => SaveSystem.Save();
    ///   (지금은 수동/디버그 키로만 — 테스트 중 매번 이어하기 되면 불편하므로)
    /// </summary>
    public static class SaveSystem
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "imunrok_save.json");

        [Serializable]
        private class Combined
        {
            public string game;
            public string journal;
        }

        public static bool HasSave => File.Exists(SavePath);

        public static void Save()
        {
            try
            {
                var c = new Combined
                {
                    game = GameState.Instance.ToJson(),
                    journal = Journal.Instance.ToJson(),
                };
                File.WriteAllText(SavePath, JsonUtility.ToJson(c));
                DevLog.Note($"[SaveSystem] 저장 완료 → {SavePath}");
            }
            catch (Exception e) { Debug.LogWarning($"[SaveSystem] 저장 실패: {e.Message}"); }
        }

        public static bool Load()
        {
            if (!HasSave) { DevLog.Note("[SaveSystem] 저장 파일이 없습니다."); return false; }
            try
            {
                var c = JsonUtility.FromJson<Combined>(File.ReadAllText(SavePath));
                if (c == null) return false;
                GameState.Instance.FromJson(c.game);
                Journal.Instance.FromJson(c.journal);
                DevLog.Note("[SaveSystem] 불러오기 완료");
                return true;
            }
            catch (Exception e) { Debug.LogWarning($"[SaveSystem] 불러오기 실패: {e.Message}"); return false; }
        }

        public static void Delete()
        {
            try { if (HasSave) File.Delete(SavePath); DevLog.Note("[SaveSystem] 저장 삭제"); }
            catch (Exception e) { Debug.LogWarning($"[SaveSystem] 삭제 실패: {e.Message}"); }
        }
    }
}
