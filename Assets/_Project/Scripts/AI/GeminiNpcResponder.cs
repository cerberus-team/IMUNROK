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
        // 모델 이름이 틀리면 404가 난다. 쓸 수 있는 목록은 아래로 확인:
        //   https://generativelanguage.googleapis.com/v1beta/models  (헤더 x-goog-api-key 에 키)
        private const int MaxOutputTokens = 120;

        // flash-lite = 가장 싸고 빠른 등급. NPC 대답은 1~2문장(120토큰)뿐이라 상위 등급이 필요 없고,
        // 대화에서는 응답 속도가 품질보다 체감에 크게 작용한다.
        // '-latest' 별칭 대신 버전을 고정한다 — 별칭은 어느 날 모델이 바뀌며 말투가 달라질 수 있어
        // 시연을 앞둔 프로젝트에선 위험하다. 모델이 내려가면 그때 번호만 올리면 된다.
        private readonly string _model = "gemini-3.5-flash-lite";
        private readonly MockNpcResponder _fallback = new MockNpcResponder();
        private readonly string _apiKey;

        public GeminiNpcResponder(string model = null)
        {
            _apiKey = LoadApiKey();
            if (!string.IsNullOrEmpty(model)) _model = model;
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
            // 키는 쿼리스트링이 아니라 헤더로 보낸다.
            // URL에 붙이면 프록시·서버 로그·유니티 네트워크 프로파일러에 키가 그대로 남는다.
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";
            string body = BuildRequestJson(req);

            using (var www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("x-goog-api-key", _apiKey);

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
            sb.AppendLine("[규칙] 위 인물로서 묻는 이에게 답하라. 반드시 1~2문장, 아주 짧게(한두 줄). 장황하게 늘어놓지 마라.");
            // 말투를 "조선시대 말투로" 라고만 이르면 어미가 섞여 나온다 —
            // "…어디 있겠소이다까" 같은 것이 실제로 나왔다(하오체 '소이다' 에 의문 '까' 를 겹친 것).
            // 쓸 어미를 몇 개 못박고, 섞지 말라고 따로 이른다.
            sb.AppendLine("[말투] 조선 후기 말씨를 쓰되 어미를 섞지 마라. " +
                          "양반은 하오체(…소/…오/…구려/…시오)로만, 아랫사람은 합쇼체(…습니다/…습니까/…지요)로만 말한다. " +
                          "'소이다까'처럼 두 말씨를 겹친 어미는 없는 말이다. 현대 말씨(…했어요/…네요/…거든요)도 쓰지 마라.");
            sb.AppendLine("아래 '밝혀진 사실'에 없는 핵심 비밀은 절대 먼저 말하지 마라. 시치미를 떼라.");

            // ── 이름 ──
            //
            // <b>여태 인물에게 제 이름을 알려 주지 않았다.</b> 성격만 통째로 주었는데,
            // 가짜 옹덕구의 성격에는 「실은 종 '복동'이 주인 행세를 한다」가 적혀 있다.
            // 그러니 이름을 물으면 AI는 그것을 <b>제가 아는 사실</b>로 읽고 그대로 답한다 —
            // 「복동이오」. 첫 마디에 사건이 통째로 샌다.
            //
            // 이름패를 그대로 물려 줄 수도 없다. 진짜 옹덕구의 이름패는
            // 「행색 사나운 사내」인데, 그건 <b>손님이 아직 모르니 그렇게 보인다</b>는
            // 뜻이지 그가 제 입으로 댈 이름이 아니다. 그래서 대는 이름을 따로 둔다.
            if (req.character != null)
            {
                string spoken = string.IsNullOrEmpty(req.character.spokenName)
                              ? req.character.characterName : req.character.spokenName;
                if (!string.IsNullOrEmpty(spoken))
                    sb.AppendLine("[네 이름] 너는 스스로를 '" + spoken + "'이라 한다. "
                                + "이름을 묻거든 그리 답하고, 그 밖의 이름으로 제 자신을 부르지 마라.");

                if (!string.IsNullOrEmpty(req.character.secretWords))
                    sb.AppendLine("[입에 담지 않는 말] " + req.character.secretWords
                                + " — 이 낱말들은 네 입으로 먼저 꺼내지 마라. "
                                + "묻는 이가 증거를 들이밀어 아래 '밝혀진 사실'에 오르기 전까지는 모르는 척하라.");
            }
            // 플레이어의 신분은 이야기의 반전이다. 인물이 먼저 "어사또"라 부르면 그 반전이 새어나간다.
            if (!string.IsNullOrEmpty(req.playerIdentityBrief))
                sb.AppendLine("[묻는 이] " + req.playerIdentityBrief);
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
                    // 이름표(=req.playerTitle)로 플레이어 줄을 가려낸다. 호칭이 막마다 바뀌므로 하드코딩하지 않는다.
                    string me = string.IsNullOrEmpty(req.playerTitle) ? "나그네" : req.playerTitle;
                    bool isUser = line.StartsWith(me, StringComparison.Ordinal);
                    if (!started) { if (!isUser) continue; started = true; } // 플레이어의 첫 턴부터
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
