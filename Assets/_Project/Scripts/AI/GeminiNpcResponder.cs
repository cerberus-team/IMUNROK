using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace IMUNROK.Common
{
    /// <summary>
    /// 실제 AI(Google Gemini)로 NPC 대답을 생성하는 대답기.
    /// INpcResponder를 구현하므로 심문 무대는 그대로, 이 클래스만 꽂으면 됨.
    ///
    /// API 키: 프로젝트 루트(Assets 상위 폴더)의 "gemini_api_key.txt" 파일에서 읽는다.
    ///   → 이 파일은 .gitignore에 등록되어 커밋되지 않음(키 유출 방지).
    ///   → 키가 없으면 자동으로 목업(MockNpcResponder)으로 대체되어 게임은 계속 돌아감.
    ///
    /// ※ 빌드(APK) 배포용으로는 키를 클라이언트에 넣으면 안 됨(중계 서버 필요). 지금은 에디터 개발용.
    /// </summary>
    public class GeminiNpcResponder : INpcResponder
    {
        // 모델 이름 — 저렴하고 빠른 Flash 계열. 404가 나면 "gemini-1.5-flash"로 바꿔보세요.
        private const string Model = "gemini-2.0-flash";
        private const int MaxOutputTokens = 300;

        private readonly MockNpcResponder _fallback = new MockNpcResponder();
        private readonly string _apiKey;

        public GeminiNpcResponder()
        {
            _apiKey = LoadApiKey();
        }

        private static string LoadApiKey()
        {
            try
            {
                string path = Path.Combine(Application.dataPath, "..", "gemini_api_key.txt");
                if (File.Exists(path)) return File.ReadAllText(path).Trim();
            }
            catch (Exception e) { Debug.LogWarning($"[Gemini] 키 읽기 실패: {e.Message}"); }
            return "";
        }

        public void GetResponse(MonoBehaviour host, NpcRequest req, Action<string> onReply, Action<string> onError)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogWarning("[Gemini] API 키가 없어 목업으로 대체합니다. " +
                                 "(프로젝트 루트에 gemini_api_key.txt 파일을 만들고 키를 넣으세요)");
                _fallback.GetResponse(host, req, onReply, onError);
                return;
            }
            if (host == null) { onError?.Invoke("코루틴 host가 없습니다."); return; }
            host.StartCoroutine(Send(req, onReply, onError));
        }

        private IEnumerator Send(NpcRequest req, Action<string> onReply, Action<string> onError)
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={_apiKey}";
            string body = BuildRequestJson(req);

            using (var www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Gemini] 요청 실패: {www.responseCode} {www.error}\n{www.downloadHandler.text}");
                    onError?.Invoke($"{www.responseCode} {www.error}");
                    yield break;
                }

                string reply = ParseReply(www.downloadHandler.text);
                if (string.IsNullOrEmpty(reply)) { onError?.Invoke("빈 응답(안전필터 등)"); yield break; }
                onReply?.Invoke(reply.Trim());
            }
        }

        // ── 요청 JSON ──
        private string BuildRequestJson(NpcRequest req)
        {
            var reqObj = new GReq
            {
                systemInstruction = new GSystem { parts = new[] { new GPart { text = BuildSystemInstruction(req) } } },
                contents = BuildContents(req),
                generationConfig = new GGenConfig { maxOutputTokens = MaxOutputTokens, temperature = 0.9f },
            };
            return JsonUtility.ToJson(reqObj);
        }

        private string BuildSystemInstruction(NpcRequest req)
        {
            var sb = new StringBuilder();
            sb.AppendLine(req.character != null ? req.character.persona : "너는 심문받는 인물이다.");
            sb.AppendLine();
            sb.AppendLine("[규칙] 위 인물로서 어사의 심문에 답하라. 2~3문장으로 짧게, 조선시대 말투로.");
            sb.AppendLine("아래 '밝혀진 사실'에 없는 핵심 비밀은 절대 먼저 말하지 마라. 시치미를 떼라.");
            sb.AppendLine();
            sb.AppendLine("[밝혀진 사실]");
            if (req.unlockedFacts != null && req.unlockedFacts.Count > 0)
                foreach (var f in req.unlockedFacts) sb.AppendLine("- " + f);
            else
                sb.AppendLine("- (아직 없음)");
            if (!string.IsNullOrEmpty(req.justRevealedInfo))
            {
                sb.AppendLine();
                sb.AppendLine("[방금 결정적 증거가 제시됨] 다음 사실을 마지못해 인정하라: " + req.justRevealedInfo);
            }
            return sb.ToString();
        }

        private GContent[] BuildContents(NpcRequest req)
        {
            var list = new List<GContent>();
            bool started = false;
            if (req.transcript != null)
            {
                foreach (var line in req.transcript)
                {
                    bool isUser = line.StartsWith("어사");
                    if (!started) { if (!isUser) continue; started = true; } // 첫 어사(user) 턴부터
                    list.Add(new GContent
                    {
                        role = isUser ? "user" : "model",
                        parts = new[] { new GPart { text = StripSpeaker(line) } },
                    });
                }
            }
            if (list.Count == 0)
                list.Add(new GContent { role = "user", parts = new[] { new GPart { text = req.playerInput ?? "..." } } });
            return list.ToArray();
        }

        private static string StripSpeaker(string line)
        {
            int idx = line.IndexOf(": ", StringComparison.Ordinal);
            return idx >= 0 ? line.Substring(idx + 2) : line;
        }

        // ── 응답 파싱 ──
        private string ParseReply(string json)
        {
            try
            {
                var resp = JsonUtility.FromJson<GResp>(json);
                if (resp?.candidates != null && resp.candidates.Length > 0)
                {
                    var parts = resp.candidates[0].content?.parts;
                    if (parts != null && parts.Length > 0) return parts[0].text;
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Gemini] 응답 파싱 실패: {e.Message}\n{json}"); }
            return "";
        }

        // ── JsonUtility용 직렬화 클래스 ──
        [Serializable] private class GPart { public string text; }
        [Serializable] private class GSystem { public GPart[] parts; }
        [Serializable] private class GContent { public string role; public GPart[] parts; }
        [Serializable] private class GGenConfig { public int maxOutputTokens; public float temperature; }
        [Serializable] private class GReq { public GSystem systemInstruction; public GContent[] contents; public GGenConfig generationConfig; }

        [Serializable] private class GRespPart { public string text; }
        [Serializable] private class GRespContent { public GRespPart[] parts; }
        [Serializable] private class GCandidate { public GRespContent content; }
        [Serializable] private class GResp { public GCandidate[] candidates; }
    }
}
