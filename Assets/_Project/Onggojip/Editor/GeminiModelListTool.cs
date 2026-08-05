using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 내 Gemini API 키로 "쓸 수 있는 모델 목록"을 확인한다(404 원인 = 모델 이름 안 맞음).
    /// 프로젝트 루트의 gemini_api_key.txt를 읽어 모델 목록을 Console에 출력.
    /// 그 중 하나를 InterrogationController의 'Gemini Model' 칸에 넣으면 됨.
    ///
    /// 메뉴: [이문록 ▸ Gemini: 사용 가능 모델 목록 확인].
    /// </summary>
    public static class GeminiModelListTool
    {
        [MenuItem("이문록/Gemini: 사용 가능 모델 목록 확인")]
        public static void ListModels()
        {
            string keyPath = Path.Combine(Application.dataPath, "..", "gemini_api_key.txt");
            if (!File.Exists(keyPath))
            {
                EditorUtility.DisplayDialog("이문록",
                    "gemini_api_key.txt 를 못 찾았습니다.\n프로젝트 루트(Assets 상위)에 키 파일을 만드세요.", "확인");
                return;
            }
            string key = File.ReadAllText(keyPath).Trim();
            if (string.IsNullOrEmpty(key))
            {
                EditorUtility.DisplayDialog("이문록", "키 파일이 비어 있습니다.", "확인");
                return;
            }

            string url = "https://generativelanguage.googleapis.com/v1beta/models?key=" + key;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                string json;
                using (var wc = new WebClient { Encoding = Encoding.UTF8 })
                    json = wc.DownloadString(url);

                var names = new System.Collections.Generic.List<string>();
                foreach (Match m in Regex.Matches(json, "\"name\"\\s*:\\s*\"models/([^\"]+)\""))
                    names.Add(m.Groups[1].Value);

                if (names.Count == 0)
                {
                    Debug.LogWarning("[Gemini] 모델 이름을 못 찾음. 원본 응답:\n" + json);
                    EditorUtility.DisplayDialog("이문록", "모델을 못 찾았어요. Console 응답을 확인하세요.", "확인");
                    return;
                }

                // flash 계열을 위로(대화용으로 저렴·빠름)
                names.Sort((a, b) => (b.Contains("flash") ? 1 : 0) - (a.Contains("flash") ? 1 : 0));
                Debug.Log("[Gemini] 사용 가능 모델 목록:\n  " + string.Join("\n  ", names));
                EditorUtility.DisplayDialog("이문록",
                    $"모델 {names.Count}개 확인! Console에 목록 있어요.\n\n" +
                    "그 중 'flash' 들어간 이름 하나를 복사해서\n" +
                    "InterrogationController의 'Gemini Model' 칸에 넣으세요.\n" +
                    "(예: " + names[0] + ")", "확인");
            }
            catch (WebException we)
            {
                string detail = "";
                if (we.Response is HttpWebResponse resp)
                    using (var sr = new StreamReader(resp.GetResponseStream()))
                        detail = sr.ReadToEnd();
                Debug.LogError($"[Gemini] 모델 목록 실패: {we.Message}\n{detail}");
                EditorUtility.DisplayDialog("이문록",
                    "실패: " + we.Message + "\n\n" +
                    "① 키가 맞는지 ② Google AI Studio에서 'Generative Language API'가 켜져 있는지 확인하세요.\n" +
                    "자세한 오류는 Console 참고.", "확인");
            }
        }
    }
}
