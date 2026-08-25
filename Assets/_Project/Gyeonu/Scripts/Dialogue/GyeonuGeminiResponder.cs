using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using IMUNROK.Common;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 제3사건의 Gemini 대답기 (2026-08-25). 공통 <see cref="INpcResponder"/> 계약을 그대로 지키므로
    /// <see cref="DialogueSession"/> 은 어느 쪽이 꽂혀도 똑같이 부른다.
    ///
    /// ■ ⚠️ 왜 공통 <see cref="GeminiNpcResponder"/> 를 못 쓰는가 (2026-08-25 실측)
    ///   공통 쪽은 모델 이름이 <c>gemini-2.0-flash</c> 로 **붙박여** 있는데, 이 모델은 이미 내려갔다.
    ///   실제 키로 부르면 이렇게 돌아온다:
    ///     404 — "This model models/gemini-2.0-flash is no longer available."
    ///   공통 폴더는 읽기만 하기로 되어 있어 그 상수를 고칠 수 없다. 그래서 <b>같은 계약을 지키는
    ///   사건 쪽 대답기</b>를 두었다. 공통 담당자가 모델을 올리면 이 클래스를 지우고
    ///   <see cref="DialogueSession"/> 의 한 줄만 되돌리면 된다.
    ///
    /// ■ 모델을 <c>flash-lite</c> 로 고른 까닭 — 실측한 지연
    ///     gemini-3.6-flash ......... 8.7초  (생각 토큰 283)
    ///     gemini-3.5-flash-lite .... 1.0초  ← 채택
    ///     gemini-3.1-flash-lite .... 0.8초
    ///   눈앞의 사람이 8.7초 굳어 있는 것은 VR에서 그대로 체감된다. 대사 품질은 셋 다
    ///   조선 말투·등급 표식을 지켰고, 3.5-lite가 어투 변화가 가장 넉넉했다.
    ///
    /// ■ 키
    ///   공통과 <b>같은 자리</b>의 <c>gemini_api_key.txt</c>(프로젝트 루트)에서 읽는다.
    ///   .gitignore 의 <c>*_api_key.txt</c> 로 이미 막혀 있다 — 코드·씬에 키를 넣지 않는다.
    /// </summary>
    public class GyeonuGeminiResponder : INpcResponder
    {
        /// <summary>대사 생성 모델. 바꿔 볼 일이 있으면 여기 한 줄만 고친다.</summary>
        public const string Model = "gemini-3.5-flash-lite";

        /// <summary>받아쓰기 모델 — 대사와 같은 것을 쓴다(음성도 함께 이해한다).</summary>
        public const string VoiceModel = "gemini-3.5-flash-lite";

        const int MaxOutputTokens = 400;
        const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/";

        static string _key;
        static bool _keyRead;

        /// <summary>프로젝트 루트의 키 파일. 없으면 빈 문자열.</summary>
        public static string ApiKey
        {
            get
            {
                if (_keyRead) return _key;
                _keyRead = true;
                _key = "";
                try
                {
                    string path = Path.Combine(Application.dataPath, "..", "gemini_api_key.txt");
                    if (File.Exists(path)) _key = File.ReadAllText(path).Trim();
                }
                catch (Exception e) { Debug.LogWarning("[Gemini] 키 읽기 실패: " + e.Message); }
                return _key;
            }
        }

        public static bool HasKey => !string.IsNullOrEmpty(ApiKey);

        // ─────────────────────────────────────────────────────────
        //  ① 대사 — 공통 계약
        // ─────────────────────────────────────────────────────────
        public void GetResponse(MonoBehaviour host, NpcRequest req, Action<string> onReply, Action<string> onError)
        {
            if (!HasKey) { onError?.Invoke("API 키가 없다"); return; }
            if (host == null) { onError?.Invoke("코루틴 host가 없다"); return; }
            host.StartCoroutine(Send(req, onReply, onError));
        }

        IEnumerator Send(NpcRequest req, Action<string> onReply, Action<string> onError)
        {
            string body = BuildJson(req);
            yield return Post(Model, body, raw =>
            {
                string reply = ParseText(raw);
                if (string.IsNullOrEmpty(reply)) onError?.Invoke("빈 응답(안전필터 등)");
                else onReply?.Invoke(reply.Trim());
            }, onError);
        }

        // ─────────────────────────────────────────────────────────
        //  ② 받아쓰기 — 목소리를 글로
        // ─────────────────────────────────────────────────────────
        /// <summary>
        /// 녹음한 WAV를 글로 받아 적는다.
        ///
        /// ■ 왜 받아쓰기와 대사를 <b>따로</b> 부르는가
        ///   한 번에 음성을 던져 바로 대사를 받으면 왕복이 줄지만, 플레이어가 <b>자기가 무슨 말로
        ///   전달됐는지</b> 볼 수 없다. 잘못 알아들었을 때 고칠 방법이 사라진다. 그래서 받아 적은
        ///   글을 먼저 입력칸에 올리고, 보내기는 글자 입력과 <b>완전히 같은 길</b>을 타게 했다.
        ///   덤으로 대사 쪽(공통 계약)은 음성을 몰라도 된다.
        /// </summary>
        public static void Transcribe(MonoBehaviour host, byte[] wav, Action<string> onText, Action<string> onError)
        {
            if (!HasKey) { onError?.Invoke("API 키가 없다"); return; }
            if (wav == null || wav.Length < 64) { onError?.Invoke("녹음이 비어 있다"); return; }
            host.StartCoroutine(SendVoice(wav, onText, onError));
        }

        static IEnumerator SendVoice(byte[] wav, Action<string> onText, Action<string> onError)
        {
            // ⚠️ 고유명사를 먼저 알려 준다 (2026-08-25 실측). 힌트 없이 넣었더니
            //    "선아를"이 **"선하를"** 로 적혔다. 사건의 이름들은 흔한 말이 아니라
            //    소리만으로는 갈리지 않는다 — 후보를 주면 그쪽으로 붙는다.
            const string prompt =
                "이 음성을 한국어로 받아 적어라. 받아 적은 말만 한 줄로 출력하라. " +
                "설명·따옴표·군말을 붙이지 마라. 알아들을 수 없으면 빈 줄을 출력하라.\n" +
                "이 대화에 나오는 이름들이다. 비슷한 소리가 들리면 이 표기를 쓰라: " +
                "선아, 견우, 직녀, 성하리, 은하담, 오작교, 칠석, 관아, 수령, 주모, 혼천의, 혼상, 서고.";

            var sb = new StringBuilder();
            sb.Append("{\"contents\":[{\"role\":\"user\",\"parts\":[");
            sb.Append("{\"text\":").Append(Quote(prompt)).Append("},");
            sb.Append("{\"inline_data\":{\"mime_type\":\"audio/wav\",\"data\":\"")
              .Append(Convert.ToBase64String(wav)).Append("\"}}");
            sb.Append("]}],\"generationConfig\":{\"maxOutputTokens\":256,\"temperature\":0}}");

            yield return Post(VoiceModel, sb.ToString(), raw =>
            {
                string text = ParseText(raw);
                text = text == null ? "" : text.Trim().Trim('"');
                if (string.IsNullOrEmpty(text)) onError?.Invoke("알아듣지 못했다");
                else onText?.Invoke(text);
            }, onError);
        }

        // ─────────────────────────────────────────────────────────
        static IEnumerator Post(string model, string body, Action<string> onOk, Action<string> onError)
        {
            string url = Endpoint + model + ":generateContent?key=" + ApiKey;
            using (var www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = 30;

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[Gemini] 요청 실패 " + www.responseCode + " " + www.error + "\n" + www.downloadHandler.text);
                    onError?.Invoke(www.responseCode + " " + www.error);
                    yield break;
                }
                onOk?.Invoke(www.downloadHandler.text);
            }
        }

        // ── 요청 만들기 ──────────────────────────────────────────
        string BuildJson(NpcRequest req)
        {
            var reqObj = new GReq
            {
                systemInstruction = new GSystem { parts = new[] { new GPart { text = BuildSystem(req) } } },
                contents = BuildContents(req),
                generationConfig = new GGenConfig { maxOutputTokens = MaxOutputTokens, temperature = 0.9f },
            };
            return JsonUtility.ToJson(reqObj);
        }

        /// <summary>공통 대답기와 <b>같은 뼈대</b>로 짠다 — 나중에 공통으로 되돌아가도 프롬프트가 안 바뀐다.</summary>
        string BuildSystem(NpcRequest req)
        {
            var sb = new StringBuilder();
            sb.AppendLine(req.character != null ? req.character.persona : "너는 심문받는 인물이다.");
            sb.AppendLine();
            sb.AppendLine("[규칙] 위 인물로서 답하라. 2~3문장으로 짧게, 조선시대 말투로.");
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
                sb.AppendLine("[방금 결정적 증거가 제시됨] 다음을 따르라: " + req.justRevealedInfo);
            }
            return sb.ToString();
        }

        GContent[] BuildContents(NpcRequest req)
        {
            var list = new List<GContent>();
            bool started = false;
            if (req.transcript != null)
            {
                foreach (var line in req.transcript)
                {
                    bool isUser = line.StartsWith("어사");
                    if (!started) { if (!isUser) continue; started = true; }   // 첫 플레이어 턴부터
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

        static string StripSpeaker(string line)
        {
            int idx = line.IndexOf(": ", StringComparison.Ordinal);
            return idx >= 0 ? line.Substring(idx + 2) : line;
        }

        // ── 응답 읽기 ────────────────────────────────────────────
        /// <summary>
        /// 후보의 <b>모든</b> part를 이어 붙인다.
        /// ⚠️ 공통 대답기처럼 <c>parts[0]</c> 만 읽으면 안 된다 — 생각을 하는 모델은 앞쪽 part에
        ///    생각 조각을 담아 보내서, 첫 조각만 읽으면 부스러기가 대사로 뜬다(2026-08-25 실측).
        /// </summary>
        static string ParseText(string json)
        {
            try
            {
                var resp = JsonUtility.FromJson<GResp>(json);
                if (resp?.candidates == null || resp.candidates.Length == 0) return "";
                var parts = resp.candidates[0].content?.parts;
                if (parts == null) return "";
                var sb = new StringBuilder();
                foreach (var p in parts)
                    if (!string.IsNullOrEmpty(p.text)) sb.Append(p.text);
                return sb.ToString();
            }
            catch (Exception e) { Debug.LogWarning("[Gemini] 응답 파싱 실패: " + e.Message + "\n" + json); }
            return "";
        }

        /// <summary>JSON 문자열 한 토막을 따옴표까지 붙여 만든다 (손으로 짜는 음성 요청용).</summary>
        static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        // ── JsonUtility용 ────────────────────────────────────────
        [Serializable] class GPart { public string text; }
        [Serializable] class GSystem { public GPart[] parts; }
        [Serializable] class GContent { public string role; public GPart[] parts; }
        [Serializable] class GGenConfig { public int maxOutputTokens; public float temperature; }
        [Serializable] class GReq { public GSystem systemInstruction; public GContent[] contents; public GGenConfig generationConfig; }

        [Serializable] class GRespPart { public string text; }
        [Serializable] class GRespContent { public GRespPart[] parts; }
        [Serializable] class GCandidate { public GRespContent content; }
        [Serializable] class GResp { public GCandidate[] candidates; }
    }
}
