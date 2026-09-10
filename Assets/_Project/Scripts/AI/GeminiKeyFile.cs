using System;
using System.IO;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// Gemini API 키 파일 — 어디서 읽는지를 한 곳에 모았다 (2026-09-10).
    ///
    /// ■ 두 자리를 본다 (앞이 우선)
    ///   ① <c>Assets/StreamingAssets/gemini_key.txt</c> — 빌드에 그대로 실려 나간다
    ///      (Windows 빌드에서는 <c>IMUNROK_Data/StreamingAssets/gemini_key.txt</c>).
    ///      심사위원이 받는 빌드에는 키가 들어 있어야 하므로 여기가 정식 자리다.
    ///   ② 프로젝트 루트(빌드에서는 실행 파일 옆)의 <c>gemini_api_key.txt</c> — 예전 자리. 그대로 살려 둔다.
    ///
    /// ■ git 에는 올리지 않는다
    ///   두 파일 모두 .gitignore 에 걸려 있다. 키 값은 어디에도 출력하지 않는다.
    ///
    /// ■ 대답기 넷(공통 · 견우 · 서천 · DialogueSession.HasApiKey)이 전부 이 하나를 부른다.
    ///   (Android 에서는 StreamingAssets 가 apk 안에 압축돼 File 로 못 읽는다 — 지금 타겟은 Windows 다.)
    /// </summary>
    public static class GeminiKeyFile
    {
        public const string StreamingName = "gemini_key.txt";
        public const string RootName = "gemini_api_key.txt";

        /// <summary>키. 없으면 빈 문자열. 값은 절대 로그에 남기지 않는다.</summary>
        public static string Load(string rootFileName = RootName)
        {
            string k = ReadTrimmed(Path.Combine(Application.streamingAssetsPath, StreamingName));
            if (k.Length > 0) return k;
            return ReadTrimmed(Path.Combine(Application.dataPath, "..", string.IsNullOrEmpty(rootFileName) ? RootName : rootFileName));
        }

        public static bool Exists(string rootFileName = RootName) => Load(rootFileName).Length > 0;

        /// <summary>어디서 읽었는지 — 로그·상태 표시용. 키 값은 담지 않는다.</summary>
        public static string Where(string rootFileName = RootName)
        {
            if (ReadTrimmed(Path.Combine(Application.streamingAssetsPath, StreamingName)).Length > 0) return "StreamingAssets/" + StreamingName;
            if (ReadTrimmed(Path.Combine(Application.dataPath, "..", string.IsNullOrEmpty(rootFileName) ? RootName : rootFileName)).Length > 0) return "루트/" + rootFileName;
            return "(없음)";
        }

        static string ReadTrimmed(string path)
        {
            try
            {
                if (!File.Exists(path)) return "";
                return (File.ReadAllText(path) ?? "").Trim();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Gemini] 키 파일을 읽지 못했다: " + e.Message);
                return "";
            }
        }
    }
}
