// 서천 전용 Gemini 호출기. 대사를 JSON(문장 + 지목 가능 어절)으로 받아 옵니다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace IMUNROK.Seocheon.AI
{
    /// <summary>이번 응답이 어디서 왔는지.</summary>
    public enum SeocheonReplySource
    {
        Ai,                 // AI 응답 · JSON 파싱 성공
        FallbackNoKey,      // 키 없음
        FallbackNetwork,    // HTTP 실패 · 타임아웃
        FallbackParse,      // 응답은 왔으나 JSON 이 아님 → 원문을 문장으로 표시
        FallbackEmpty,      // 본문이 비어 있음(토큰 소진 등)
    }

    /// <summary>다음 턴에 플레이어가 고를 수 있는 질문 하나.</summary>
    public sealed class SeocheonAsk
    {
        public string label = string.Empty;
        /// <summary>probe(떠보기) / press(파고들기) / idle(딴청) / direct(대놓고). ★UI 에 드러내지 않습니다.</summary>
        public string tone = string.Empty;
    }

    public sealed class SeocheonReplyResult
    {
        public List<WordPickNote.Sentence> sentences = new List<WordPickNote.Sentence>();
        /// <summary>★다음 턴 선택지. 비어 있으면 NPC 데이터의 고정 질문을 씁니다.</summary>
        public List<SeocheonAsk> asks = new List<SeocheonAsk>();
        public SeocheonReplySource source = SeocheonReplySource.Ai;
        public string note = string.Empty;      // 실패 사유(화면 표시용, ★키는 절대 포함하지 않음)
        public long elapsedMs;
    }

    /// <summary>
    /// 서천 전용 대답기.
    ///
    /// ★공통 GeminiNpcResponder 를 고치거나 복사하지 않았습니다. 필요한 것이 다릅니다.
    ///   - 응답을 ★JSON(문장 배열 + 어절 후보)으로 받아야 어절 지목이 성립합니다.
    ///   - 단서 게이팅(허용 clueId 목록 · 이미 준 것 제외)이 지시문에 들어가야 합니다.
    ///   - 실패해도 ★고정 대사로 끝까지 진행해야 합니다.
    ///
    /// ★폴백은 조용하지 않습니다. 실패하면 콘솔에 사유를 남기고 결과에도 source 를 실어 보냅니다.
    /// </summary>
    public sealed class SeocheonGeminiResponder
    {
        private readonly SeocheonAiConfig config;
        private string apiKey = string.Empty;
        private int fallbackCursor;
        private int parseFailCursor;

        public bool HasApiKey { get { return !string.IsNullOrEmpty(apiKey); } }
        public string ModelName { get { return config != null ? config.model : "(설정 없음)"; } }

        public SeocheonGeminiResponder(SeocheonAiConfig aiConfig)
        {
            config = aiConfig;
            ReloadApiKey();
        }

        /// <summary>키 파일을 다시 읽습니다. ★키 값은 어디에도 출력하지 않습니다.</summary>
        public void ReloadApiKey()
        {
            apiKey = string.Empty;
            if (config == null)
            {
                Debug.LogError("[서천AI] SeocheonAiConfig 가 지정되지 않았습니다. 고정 대사로만 동작합니다.");
                return;
            }

            try
            {
                string path = Path.Combine(Application.dataPath, "..", config.apiKeyFileName);
                if (File.Exists(path))
                {
                    string raw = File.ReadAllText(path).Trim();
                    if (raw.Length > 0) apiKey = raw;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[서천AI] 키 파일을 읽지 못했습니다: " + e.Message);
            }

            if (!HasApiKey)
            {
                // ★조용한 폴백 금지 — 시연 중 AI 가 죽은 것을 모르면 안 됩니다.
                Debug.LogError("[서천AI] ★API 키가 없습니다. 대화는 ★고정 대사로만 진행됩니다. " +
                               "프로젝트 루트에 " + config.apiKeyFileName + " 을 두십시오. " +
                               "(찾은 위치: <프로젝트 루트>/" + config.apiKeyFileName + ")");
            }
            else if (config.verboseLog)
            {
                Debug.Log("[서천AI] 키 확인됨(값 미출력). 모델 = " + config.model +
                          " · maxOutputTokens = " + config.maxOutputTokens);
            }
        }

        // ─────────────────────────────────────────────
        //  요청
        // ─────────────────────────────────────────────

        public void GetReply(MonoBehaviour host, SeocheonNpcData npc,
                             List<string> transcript, List<string> collectedClueIds,
                             string playerInput, Action<SeocheonReplyResult> onDone)
        {
            if (onDone == null) return;

            if (npc == null)
            {
                onDone(MakeFallback(null, SeocheonReplySource.FallbackNoKey, "NPC 데이터 없음", 0));
                return;
            }
            if (!HasApiKey || config == null)
            {
                onDone(MakeFallback(npc, SeocheonReplySource.FallbackNoKey, "API 키 없음", 0));
                return;
            }
            // ★비활성 오브젝트에서는 코루틴이 시작되지 않습니다.
            //   그대로 두면 onDone 이 영영 안 불려 대화가 "생각하는 중" 에서 멈춥니다.
            //   (Phase 전환으로 NPC 그룹이 꺼지는 경우가 실제로 있습니다.)
            if (host == null || !host.isActiveAndEnabled)
            {
                Debug.LogWarning("[서천AI] 호스트가 비활성이라 요청을 보낼 수 없습니다 → 고정 대사로 대체합니다.");
                onDone(MakeFallback(npc, SeocheonReplySource.FallbackNetwork, "호스트 비활성", 0));
                return;
            }

            host.StartCoroutine(Send(npc, transcript, collectedClueIds, playerInput, onDone));
        }

        private IEnumerator Send(SeocheonNpcData npc, List<string> transcript,
                                 List<string> collectedClueIds, string playerInput,
                                 Action<SeocheonReplyResult> onDone)
        {
            string url = config.endpointBase + config.model + ":generateContent?key=" + apiKey;
            string body = BuildRequestJson(npc, transcript, collectedClueIds, playerInput);

            float t0 = Time.realtimeSinceStartup;
            string responseText = null;
            long httpCode = 0;
            string netError = null;

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = config.timeoutSeconds;

                yield return www.SendWebRequest();

                httpCode = www.responseCode;
                if (www.result != UnityWebRequest.Result.Success)
                    netError = www.error;
                responseText = www.downloadHandler != null ? www.downloadHandler.text : null;
            }

            long ms = (long)((Time.realtimeSinceStartup - t0) * 1000f);

            if (netError != null)
            {
                // ★응답 본문에 키가 섞여 올 일은 없지만, 만에 하나를 위해 지웁니다.
                string safe = Redact(responseText);
                Debug.LogError("[서천AI] ★요청 실패 " + httpCode + " " + netError + "\n" + Trim(safe, 600));
                onDone(MakeFallback(npc, SeocheonReplySource.FallbackNetwork, "HTTP " + httpCode + " " + netError, ms));
                yield break;
            }

            // ★토큰 사용량을 남깁니다 — 잘림의 원인이 추론인지 본문인지 여기서 갈립니다.
            string usage = DescribeUsage(responseText);
            if (config.verboseLog) Debug.Log("[서천AI] " + ms + " ms · " + usage);

            string raw = ExtractText(responseText);
            if (string.IsNullOrEmpty(raw))
            {
                Debug.LogError("[서천AI] ★본문이 비어 있습니다. maxOutputTokens(" + config.maxOutputTokens +
                               ") 를 추론이 다 써 버린 경우입니다. " + usage);
                onDone(MakeFallback(npc, SeocheonReplySource.FallbackEmpty, "빈 응답", ms));
                yield break;
            }

            SeocheonReplyResult result = new SeocheonReplyResult();
            result.elapsedMs = ms;

            if (!TryParseSentences(raw, npc, collectedClueIds, result.sentences, result.asks))
            {
                // ★파싱 실패 — 원문은 ★콘솔에만 남기고 화면에는 인물다운 회피 대사를 냅니다.
                Debug.LogError("[서천AI] ★응답 형식이 깨졌습니다. 화면에는 회피 대사를 내보냅니다. " + usage +
                               "\n----- 받은 원문(콘솔 전용) -----\n" + Trim(raw, 1200));
                onDone(MakeParseFail(npc, ms, usage));
                yield break;
            }

            if (result.sentences.Count == 0)
            {
                Debug.LogWarning("[서천AI] 문장이 0개로 파싱됐습니다 → 고정 대사로 대체합니다.");
                onDone(MakeFallback(npc, SeocheonReplySource.FallbackParse, "문장 0개", ms));
                yield break;
            }

            // ★asks 가 비면 NPC 데이터의 고정 질문으로 채웁니다(선택지가 없어 막히면 안 됩니다).
            if (result.asks.Count == 0)
            {
                FillFallbackAsks(npc, result.asks);
                Debug.LogWarning("[서천AI] asks 가 비어 고정 질문으로 채웠습니다.");
            }

            result.source = SeocheonReplySource.Ai;
            if (config.verboseLog)
                Debug.Log("[서천AI] 응답 OK · " + ms + " ms · 문장 " + result.sentences.Count + "개");
            onDone(result);
        }

        // ─────────────────────────────────────────────
        //  지시문
        // ─────────────────────────────────────────────

        private string BuildSystemInstruction(SeocheonNpcData npc, List<string> collectedClueIds)
        {
            StringBuilder sb = new StringBuilder(1024);

            sb.AppendLine("[인물]");
            sb.AppendLine(string.IsNullOrEmpty(npc.persona) ? "너는 조선시대 어느 고을의 백성이다." : npc.persona);
            sb.AppendLine();

            sb.AppendLine("[상황]");
            sb.AppendLine("- ★이 고을의 이름은 '안락현(安樂縣)'이다. 고을을 부를 때는 '안락현' 또는 '이 고을'이라 한다.");
            sb.AppendLine("  ★'서천'이라는 이름은 이 고을과 아무 상관이 없다. 절대 쓰지 마라.");
            sb.AppendLine("- 너와 이야기하는 상대는 곡물을 사러 온 외지 장사꾼이다.");
            sb.AppendLine("- 상대가 암행어사라는 것을 너는 모른다. 아무도 모른다.");
            sb.AppendLine("- ★이 고을 수령의 성은 邊(변)이다. 사람들은 그를 '변 사또' 또는 '사또 나리'라 부른다.");
            sb.AppendLine("  ★수령 이야기가 나오면 적어도 한 번은 '변 사또'라고 성을 붙여 불러라.");
            sb.AppendLine("  성을 알려 주는 것뿐이니 자연스럽게 흘리면 된다. 따로 강조하지 마라.");
            sb.AppendLine("- 이 고을 수령이 죽었다는 사실을 너는 모른다. 장례가 몰래 치러져 아무도 모른다.");
            sb.AppendLine("  ★그러므로 변 사또는 살아서 관아에 있는 사람처럼 말한다. 과거형으로 말하지 마라.");
            sb.AppendLine("- 무당 임말손에 대해 먼저 말하지 마라. 물어도 그저 굿을 하는 사람으로만 안다.");
            sb.AppendLine("- 너는 사건의 결론을 내리지 않는다. 네가 겪은 것, 본 것, 들은 것만 말한다.");
            sb.AppendLine();

            SeocheonStyleProfile prof = SeocheonTalkStyleState.Profile;
            sb.AppendLine("[상대의 말투 — 선택지를 이 말투로 써라]");
            if (prof == null)
            {
                sb.AppendLine("- 평범한 장사꾼 말투.");
            }
            else
            {
                sb.AppendLine("- 이름 : " + prof.styleName);
                sb.AppendLine("- 특징 : " + prof.TraitsJoined);
                if (!string.IsNullOrEmpty(prof.sampleTone))
                    sb.AppendLine("- 이렇게 말한다 : \"" + prof.sampleTone + "\"");
                sb.AppendLine("- " + prof.DisguiseInstruction);
                sb.AppendLine("- 호감·경계의 정도를 숫자나 표식으로 말하지 마라. 말투로만 드러내라.");
            }
            sb.AppendLine();

            sb.AppendLine("[말투]");
            sb.AppendLine("- 어미만 사극이고 어휘는 현대말을 쓴다. '~하오 / ~구려 / ~습디다 / ~시오' 정도.");
            sb.AppendLine("- 옛 한자어나 고어를 억지로 쓰지 마라.");
            int cap = Mathf.Max(1, config.maxSentencesPerReply);
            sb.AppendLine("- 한 문장은 20~35자.");
            sb.AppendLine("- ★한 번에 정확히 " + cap + "문장만 말한다. 더도 덜도 안 된다. reply 배열 길이가 " + cap + " 이어야 한다.");
            sb.AppendLine("- 길게 설명하지 마라. 짧게 끊어라.");
            sb.AppendLine();

            sb.AppendLine("[흘릴 수 있는 조각]");
            if (npc.clues == null || npc.clues.Length == 0)
            {
                sb.AppendLine("- 없다. 어떤 조각도 흘리지 마라.");
            }
            else
            {
                for (int i = 0; i < npc.clues.Length; i++)
                {
                    SeocheonClue c = npc.clues[i];
                    if (c == null || string.IsNullOrEmpty(c.clueId)) continue;
                    bool already = collectedClueIds != null && collectedClueIds.Contains(c.clueId);
                    sb.Append("- ").Append(c.clueId).Append(" : ").Append(c.journalText);
                    sb.Append("  / 흘리는 조건: ").Append(c.revealCondition);
                    sb.AppendLine(already ? "  / ★이미 줬다. 다시 주지 마라." : string.Empty);
                }
            }
            sb.AppendLine();

            sb.AppendLine("[조각 규칙]");
            sb.AppendLine("- 위 목록에 없는 clueId 를 ★절대 만들지 마라.");
            sb.AppendLine("- 이미 준 조각은 ★다시 주지 마라. options 에서 빼라.");
            sb.AppendLine("- 조건에 맞지 않는 질문이면 인물답게 얼버무리고 options 를 빈 배열로 둬라.");
            sb.AppendLine("- 조각을 흘릴 때도 결론을 말하지 말고, 겪은 일을 이야기하듯 흘려라.");
            sb.AppendLine();

            sb.AppendLine("[출력 형식]");
            sb.AppendLine("아래 JSON 만 출력한다. 마크다운 코드펜스를 쓰지 마라. 설명을 덧붙이지 마라.");
            sb.AppendLine("{\"reply\":[{\"text\":\"한 문장\",\"options\":[{\"word\":\"어절\",\"clueId\":\"A1\"}]}]}");
            sb.AppendLine("- text 는 네가 하는 말 한 문장이다.");
            sb.AppendLine("- options 는 그 문장에서 상대가 수상히 여겨 짚어 볼 만한 어절이다.");
            sb.AppendLine("- word 는 ★같은 text 안에 글자 그대로 들어 있어야 한다. 토씨 하나도 다르면 안 된다.");
            sb.AppendLine("- 조각에 해당하는 어절이면 clueId 를 위 목록의 값으로 적는다.");
            sb.AppendLine("- 조각이 아닌 그럴듯한 미끼 어절이면 clueId 를 null 로 둔다. 한 응답에 0~2개.");
            sb.AppendLine("- 짚을 것이 없는 문장은 options 를 빈 배열로 둔다.");
            sb.AppendLine();
            sb.AppendLine("[다음 질문 후보 — asks]");
            sb.AppendLine("같은 JSON 에 \"asks\" 배열도 함께 넣는다. 형식:");
            sb.AppendLine("\"asks\":[{\"label\":\"곳간이 넉넉해 보이는구려.\",\"tone\":\"probe\"}]");
            sb.AppendLine("- 3개에서 4개.");
            sb.AppendLine("- label 은 ★상대(장사꾼)가 할 말이다. 네 대사가 아니다.");
            sb.AppendLine("- label 은 ★20자 이내, ★위에 적힌 상대의 말투로 쓴다.");
            sb.AppendLine("- tone 은 probe / press / idle / direct 중 하나.");
            sb.AppendLine("- ★어느 질문이 조각으로 이어지는지 label 로 티내지 마라.");
            sb.AppendLine("  조각과 무관한 질문도 똑같이 그럴듯하게 써라.");
            sb.AppendLine("- 지금까지 오간 말과, 아직 주지 않은 조각을 고려해 만든다.");

            return sb.ToString();
        }

        private string BuildRequestJson(SeocheonNpcData npc, List<string> transcript,
                                        List<string> collectedClueIds, string playerInput)
        {
            GReq req = new GReq();
            req.systemInstruction = new GSystem();
            req.systemInstruction.parts = new GPart[1];
            req.systemInstruction.parts[0] = new GPart();
            req.systemInstruction.parts[0].text = BuildSystemInstruction(npc, collectedClueIds);

            List<GContent> contents = new List<GContent>();
            if (transcript != null)
            {
                // ★최근 N턴만 보냅니다. 이력이 길면 느려지고, 추론 토큰이 늘어 본문이 잘립니다.
                int keep = Mathf.Max(1, config.historyTurns) * 2;
                int from = 0;
                int userSeen = 0;
                for (int i = transcript.Count - 1; i >= 0; i--)
                {
                    if (string.IsNullOrEmpty(transcript[i])) continue;
                    if (transcript[i].StartsWith("나:", StringComparison.Ordinal)) userSeen++;
                    if (userSeen > Mathf.Max(1, config.historyTurns)) { from = i + 1; break; }
                }

                bool started = false;
                for (int i = from; i < transcript.Count; i++)
                {
                    string line = transcript[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    bool isUser = line.StartsWith("나:", StringComparison.Ordinal);
                    if (!started) { if (!isUser) continue; started = true; }

                    GContent c = new GContent();
                    c.role = isUser ? "user" : "model";
                    c.parts = new GPart[1];
                    c.parts[0] = new GPart();
                    c.parts[0].text = StripSpeaker(line);
                    contents.Add(c);
                }
                if (contents.Count > keep) contents.RemoveRange(0, contents.Count - keep);
            }
            if (contents.Count == 0)
            {
                GContent c = new GContent();
                c.role = "user";
                c.parts = new GPart[1];
                c.parts[0] = new GPart();
                c.parts[0].text = string.IsNullOrEmpty(playerInput) ? "..." : playerInput;
                contents.Add(c);
            }
            req.contents = contents.ToArray();

            req.generationConfig = new GGenConfig();
            req.generationConfig.maxOutputTokens = config.maxOutputTokens;
            req.generationConfig.temperature = config.temperature;
            req.generationConfig.responseMimeType = "application/json";   // ★순수 JSON 강제

            string json = JsonUtility.ToJson(req);

            // ★추론 강도. JsonUtility 로는 중첩 객체를 빼기 어려워 문자열로 끼워 넣습니다.
            //   비어 있으면 아예 보내지 않아 모델 기본값을 씁니다.
            if (!string.IsNullOrEmpty(config.thinkingLevel))
            {
                // 꼬리가 …"responseMimeType":"application/json"}}  (generationConfig 닫기 + 루트 닫기)
                // → 두 괄호를 떼고  ,"thinkingConfig":{…}  +  }}  를 다시 붙입니다.
                int close = json.LastIndexOf("}}", StringComparison.Ordinal);
                if (close > 0)
                    json = json.Substring(0, close)
                         + ",\"thinkingConfig\":{\"thinkingLevel\":\"" + config.thinkingLevel + "\"}}}";
            }
            return json;
        }

        private static string StripSpeaker(string line)
        {
            int idx = line.IndexOf(": ", StringComparison.Ordinal);
            return idx >= 0 ? line.Substring(idx + 2) : line;
        }

        // ─────────────────────────────────────────────
        //  말투 프로필 만들기 (도입부 전용, 1회)
        // ─────────────────────────────────────────────

        /// <summary>
        /// ★도입부에서 플레이어가 실제로 한 말들을 보내 말투 프로필을 받아 옵니다.
        /// 실패하면 기본 프로필로 돌려주므로 ★호출부는 멈추지 않습니다.
        /// </summary>
        public void BuildProfile(MonoBehaviour host, IList<string> playerLines,
                                 Action<SeocheonStyleProfile, string> onDone)
        {
            if (onDone == null) return;
            if (!HasApiKey || config == null)
            {
                Debug.LogWarning("[서천AI] 키가 없어 말투 프로필을 만들 수 없습니다 → 기본 프로필.");
                onDone(SeocheonStyleProfile.Default(), "키 없음");
                return;
            }
            if (host == null || !host.isActiveAndEnabled)
            {
                onDone(SeocheonStyleProfile.Default(), "호스트 비활성");
                return;
            }
            if (playerLines == null || playerLines.Count == 0)
            {
                onDone(SeocheonStyleProfile.Default(), "발화 없음");
                return;
            }
            host.StartCoroutine(SendProfile(playerLines, onDone));
        }

        private IEnumerator SendProfile(IList<string> playerLines,
                                        Action<SeocheonStyleProfile, string> onDone)
        {
            StringBuilder ins = new StringBuilder(700);
            ins.AppendLine("아래는 어떤 사람이 낯선 고을에서 실제로 한 말들이다.");
            ins.AppendLine("이 사람의 ★말투를 읽어 프로필을 만들어라.");
            ins.AppendLine();
            ins.AppendLine("[판단 기준]");
            ins.AppendLine("- 존대/반말, 길이, 돌려 말하는지 직설적인지, 너스레·감탄·명령조 여부를 본다.");
            ins.AppendLine("- 이 사람은 곡물을 사러 온 장사꾼으로 ★위장 중이다.");
            ins.AppendLine("  말투가 장사꾼답게 들리면 disguiseFit 은 high,");
            ins.AppendLine("  그저 그러면 mid, 관아 사람이나 취조하는 사람처럼 들리면 low 로 한다.");
            ins.AppendLine();
            ins.AppendLine("[출력 형식] 아래 JSON 만. 코드펜스 금지. 설명 금지.");
            ins.AppendLine("{\"styleName\":\"능글맞은 장사치\",\"traits\":[\"돌려 말한다\",\"너스레를 떤다\"],");
            ins.AppendLine(" \"sampleTone\":\"허허, 그거 참 재미난 말씀이오.\",\"disguiseFit\":\"high\"}");
            ins.AppendLine("- styleName : 조선시대풍으로 한 마디. 12자 이내.");
            ins.AppendLine("- traits : 2~4개. 각 12자 이내.");
            ins.AppendLine("- sampleTone : 이 사람이 할 법한 말 한 줄. 25자 이내.");
            ins.AppendLine("- disguiseFit : high / mid / low 중 하나.");

            StringBuilder said = new StringBuilder(400);
            for (int i = 0; i < playerLines.Count; i++)
            {
                if (string.IsNullOrEmpty(playerLines[i])) continue;
                said.Append("- ").AppendLine(playerLines[i]);
            }

            GReq req = new GReq();
            req.systemInstruction = new GSystem();
            req.systemInstruction.parts = new GPart[1];
            req.systemInstruction.parts[0] = new GPart();
            req.systemInstruction.parts[0].text = ins.ToString();
            GContent c = new GContent();
            c.role = "user";
            c.parts = new GPart[1];
            c.parts[0] = new GPart();
            c.parts[0].text = "그 사람이 한 말:" + System.Environment.NewLine + said.ToString();
            req.contents = new GContent[] { c };
            req.generationConfig = new GGenConfig();
            req.generationConfig.maxOutputTokens = config.maxOutputTokens;
            req.generationConfig.temperature = 0.4f;      // 프로필은 흔들리지 않는 편이 낫습니다
            req.generationConfig.responseMimeType = "application/json";
            string body = JsonUtility.ToJson(req);
            if (!string.IsNullOrEmpty(config.thinkingLevel))
            {
                int close = body.LastIndexOf("}}", StringComparison.Ordinal);
                if (close > 0)
                    body = body.Substring(0, close)
                         + ",\"thinkingConfig\":{\"thinkingLevel\":\"" + config.thinkingLevel + "\"}}}";
            }

            string url = config.endpointBase + config.model + ":generateContent?key=" + apiKey;
            float t0 = Time.realtimeSinceStartup;
            string responseText = null; string netError = null;
            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = config.timeoutSeconds;
                yield return www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success) netError = www.error;
                responseText = www.downloadHandler != null ? www.downloadHandler.text : null;
            }
            long ms = (long)((Time.realtimeSinceStartup - t0) * 1000f);

            if (netError != null)
            {
                Debug.LogError("[서천AI] ★말투 프로필 요청 실패 " + netError + " → 기본 프로필. " + Trim(Redact(responseText), 400));
                onDone(SeocheonStyleProfile.Default(), "요청 실패");
                yield break;
            }

            string raw = ExtractText(responseText);
            string json = StripFences(raw);
            if (string.IsNullOrEmpty(json) || json[0] != '{' || json[json.Length - 1] != '}')
            {
                Debug.LogError("[서천AI] ★말투 프로필 형식이 깨졌습니다 → 기본 프로필. " + DescribeUsage(responseText));
                onDone(SeocheonStyleProfile.Default(), "형식 오류");
                yield break;
            }

            SeocheonStyleProfile made = null;
            try { made = JsonUtility.FromJson<SeocheonStyleProfile>(json); }
            catch (Exception e) { Debug.LogError("[서천AI] 프로필 파싱 예외: " + e.GetType().Name); }

            if (made == null || string.IsNullOrEmpty(made.styleName))
            {
                Debug.LogError("[서천AI] ★말투 프로필이 비었습니다 → 기본 프로필.");
                onDone(SeocheonStyleProfile.Default(), "빈 프로필");
                yield break;
            }
            if (made.traits == null || made.traits.Length == 0) made.traits = new string[] { "특징 없음" };
            if (string.IsNullOrEmpty(made.disguiseFit)) made.disguiseFit = "mid";
            made.fromCard = false;

            if (config.verboseLog)
                Debug.Log("[서천AI] 말투 프로필 " + ms + " ms · " + made.styleName +
                          " · " + made.TraitsJoined + " · fit=" + made.disguiseFit);
            onDone(made, "ok");
        }

        // ─────────────────────────────────────────────
        //  응답 파싱
        // ─────────────────────────────────────────────

        /// <summary>finishReason 과 토큰 사용량을 한 줄로. ★키는 포함되지 않습니다.</summary>
        private string DescribeUsage(string responseJson)
        {
            if (string.IsNullOrEmpty(responseJson)) return "(응답 없음)";
            try
            {
                GResp resp = JsonUtility.FromJson<GResp>(responseJson);
                string finish = (resp != null && resp.candidates != null && resp.candidates.Length > 0)
                    ? resp.candidates[0].finishReason : "?";
                string prompt = "?", cand = "?", thoughts = "?";
                if (resp != null && resp.usageMetadata != null)
                {
                    prompt = resp.usageMetadata.promptTokenCount.ToString();
                    cand = resp.usageMetadata.candidatesTokenCount.ToString();
                    thoughts = resp.usageMetadata.thoughtsTokenCount.ToString();
                }
                string warn = finish == "MAX_TOKENS"
                    ? "  ★MAX_TOKENS — maxOutputTokens 를 올리거나 historyTurns 를 줄이십시오"
                    : string.Empty;
                return "finish=" + finish + " · prompt=" + prompt + " · 추론=" + thoughts +
                       " · 본문=" + cand + " / 상한 " + config.maxOutputTokens + warn;
            }
            catch (Exception) { return "(사용량 파싱 실패)"; }
        }

        private string ExtractText(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                GResp resp = JsonUtility.FromJson<GResp>(json);
                if (resp != null && resp.candidates != null && resp.candidates.Length > 0)
                {
                    GRespContent content = resp.candidates[0].content;
                    if (content != null && content.parts != null)
                    {
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < content.parts.Length; i++)
                            if (!string.IsNullOrEmpty(content.parts[i].text)) sb.Append(content.parts[i].text);
                        return sb.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[서천AI] 응답 봉투 파싱 실패: " + e.Message);
            }
            return null;
        }

        /// <summary>JSON 대사를 Sentence 목록으로. 실패하면 false.</summary>
        private bool TryParseSentences(string raw, SeocheonNpcData npc,
                                       List<string> collectedClueIds,
                                       List<WordPickNote.Sentence> results,
                                       List<SeocheonAsk> askResults)
        {
            results.Clear();
            if (askResults != null) askResults.Clear();
            string json = StripFences(raw);
            if (string.IsNullOrEmpty(json)) return false;

            // ★잘린 응답을 파서에 넣기 전에 걸러 냅니다.
            //   { 로 시작하고 } 로 끝나지 않으면 어차피 온전한 JSON 이 아닙니다.
            if (json[0] != '{' || json[json.Length - 1] != '}')
            {
                Debug.LogWarning("[서천AI] 응답이 '{'…'}' 형태가 아닙니다(잘렸거나 군더더기가 붙음). 길이 " + json.Length);
                return false;
            }

            GReplyRoot root;
            try { root = JsonUtility.FromJson<GReplyRoot>(json); }
            catch (Exception e) { Debug.LogWarning("[서천AI] JSON 파싱 예외: " + e.GetType().Name); return false; }
            if (root == null || root.reply == null || root.reply.Length == 0) return false;

            for (int i = 0; i < root.reply.Length; i++)
            {
                GReplyLine line = root.reply[i];
                if (line == null || string.IsNullOrEmpty(line.text)) continue;

                WordPickNote.Sentence s = new WordPickNote.Sentence();
                s.text = line.text.Trim();

                List<WordPickNote.WordOption> opts = new List<WordPickNote.WordOption>();
                if (line.options != null)
                {
                    for (int j = 0; j < line.options.Length; j++)
                    {
                        GReplyOption go = line.options[j];
                        if (go == null || string.IsNullOrEmpty(go.word)) continue;

                        // ★word 가 문장 안에 그대로 없으면 버립니다(AI 가 토씨를 바꾼 경우).
                        if (s.text.IndexOf(go.word, StringComparison.Ordinal) < 0)
                        {
                            Debug.LogWarning("[서천AI] 어절 \"" + go.word + "\" 가 문장 안에 없어 버렸습니다: " + s.text);
                            continue;
                        }

                        WordPickNote.WordOption opt = new WordPickNote.WordOption();
                        opt.word = go.word;

                        if (string.IsNullOrEmpty(go.clueId) || go.clueId == "null")
                        {
                            // 미끼 어절
                            opt.isValid = false;
                            opt.clueId = string.Empty;
                            opt.journalText = go.word;
                        }
                        else
                        {
                            SeocheonClue clue = npc.FindClue(go.clueId);
                            if (clue == null)
                            {
                                // ★목록에 없는 ID 는 게이팅 위반 → 옵션 자체를 버립니다.
                                Debug.LogWarning("[서천AI] ★허용 목록에 없는 clueId \"" + go.clueId + "\" 를 만들어 버렸습니다.");
                                continue;
                            }
                            if (collectedClueIds != null && collectedClueIds.Contains(clue.clueId))
                            {
                                // ★이미 준 조각을 또 주려 함 → 버립니다.
                                Debug.LogWarning("[서천AI] 이미 수집된 clueId \"" + clue.clueId + "\" 를 다시 제시해 버렸습니다.");
                                continue;
                            }
                            opt.isValid = true;
                            opt.clueId = clue.clueId;
                            opt.journalText = clue.journalText;    // ★수첩 문구는 AI 가 아니라 에셋에서
                        }
                        opts.Add(opt);
                    }
                }

                s.options = opts.ToArray();
                results.Add(s);
            }

            // ★다음 턴 선택지
            if (askResults != null && root.asks != null)
            {
                for (int i = 0; i < root.asks.Length; i++)
                {
                    GReplyAsk ga = root.asks[i];
                    if (ga == null || string.IsNullOrEmpty(ga.label)) continue;
                    SeocheonAsk ask = new SeocheonAsk();
                    ask.label = ga.label.Trim();
                    ask.tone = string.IsNullOrEmpty(ga.tone) ? "probe" : ga.tone.Trim();
                    askResults.Add(ask);
                    if (askResults.Count >= 4) break;
                }
            }

            return results.Count > 0;
        }

        private static string StripFences(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = s.Trim();
            if (!t.StartsWith("```", StringComparison.Ordinal)) return t;

            int first = t.IndexOf('\n');
            if (first < 0) return t;
            int last = t.LastIndexOf("```", StringComparison.Ordinal);
            if (last <= first) return t.Substring(first + 1).Trim();
            return t.Substring(first + 1, last - first - 1).Trim();
        }

        // ─────────────────────────────────────────────
        //  폴백
        // ─────────────────────────────────────────────

        /// <summary>
        /// ★형식이 깨졌을 때. 원문을 절대 화면에 올리지 않고 회피 대사만 내보냅니다.
        /// 지목할 어절도 없으므로 대화는 이어지되 수집은 일어나지 않습니다.
        /// </summary>
        private SeocheonReplyResult MakeParseFail(SeocheonNpcData npc, long ms, string usage)
        {
            SeocheonReplyResult r = new SeocheonReplyResult();
            r.source = SeocheonReplySource.FallbackParse;
            r.note = "응답 오류";
            r.elapsedMs = ms;

            string line = "…글쎄올시다.";
            if (npc != null && npc.parseFailLines != null && npc.parseFailLines.Length > 0)
            {
                string candidate = npc.parseFailLines[parseFailCursor % npc.parseFailLines.Length];
                parseFailCursor++;
                if (!string.IsNullOrEmpty(candidate)) line = candidate;
            }

            WordPickNote.Sentence s = new WordPickNote.Sentence();
            s.text = line;
            s.options = Array.Empty<WordPickNote.WordOption>();
            r.sentences.Add(s);
            FillFallbackAsks(npc, r.asks);
            return r;
        }

        /// <summary>★AI 가 선택지를 못 줬을 때. NPC 데이터의 고정 질문을 씁니다.</summary>
        private static void FillFallbackAsks(SeocheonNpcData npc, List<SeocheonAsk> into)
        {
            if (into == null) return;
            into.Clear();
            if (npc != null && npc.fallbackAsks != null)
            {
                for (int i = 0; i < npc.fallbackAsks.Length && into.Count < 4; i++)
                {
                    if (string.IsNullOrEmpty(npc.fallbackAsks[i])) continue;
                    SeocheonAsk a = new SeocheonAsk();
                    a.label = npc.fallbackAsks[i];
                    a.tone = "probe";
                    into.Add(a);
                }
            }
            if (into.Count == 0)
            {
                SeocheonAsk a = new SeocheonAsk();
                a.label = "이 고을 사정이 어떻소?";
                a.tone = "probe";
                into.Add(a);
            }
        }

        private SeocheonReplyResult MakeFallback(SeocheonNpcData npc, SeocheonReplySource source,
                                                 string note, long ms)
        {
            SeocheonReplyResult r = new SeocheonReplyResult();
            r.source = source;
            r.note = note;
            r.elapsedMs = ms;

            if (npc != null && npc.fallbackSentences != null && npc.fallbackSentences.Length > 0)
            {
                WordPickNote.Sentence s = npc.fallbackSentences[fallbackCursor % npc.fallbackSentences.Length];
                fallbackCursor++;
                if (s != null && !string.IsNullOrEmpty(s.text)) r.sentences.Add(s);
            }

            if (r.sentences.Count == 0)
            {
                WordPickNote.Sentence s = new WordPickNote.Sentence();
                s.text = "…글쎄올시다. 소인은 잘 모르는 일이오.";
                s.options = Array.Empty<WordPickNote.WordOption>();
                r.sentences.Add(s);
            }
            FillFallbackAsks(npc, r.asks);
            return r;
        }

        private string Redact(string s)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(apiKey)) return s;
            return s.Replace(apiKey, "<<KEY>>");
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + " …(잘림)";
        }

        // ─────────────────────────────────────────────
        //  JsonUtility 직렬화 클래스
        // ─────────────────────────────────────────────

        [Serializable] private class GPart { public string text; }
        [Serializable] private class GSystem { public GPart[] parts; }
        [Serializable] private class GContent { public string role; public GPart[] parts; }
        [Serializable] private class GGenConfig
        {
            public int maxOutputTokens;
            public float temperature;
            public string responseMimeType;
        }
        [Serializable] private class GReq
        {
            public GSystem systemInstruction;
            public GContent[] contents;
            public GGenConfig generationConfig;
        }

        [Serializable] private class GRespPart { public string text; }
        [Serializable] private class GRespContent { public GRespPart[] parts; }
        [Serializable] private class GCandidate { public GRespContent content; public string finishReason; }
        [Serializable] private class GUsage
        {
            public int promptTokenCount;
            public int candidatesTokenCount;
            public int thoughtsTokenCount;
            public int totalTokenCount;
        }
        [Serializable] private class GResp { public GCandidate[] candidates; public GUsage usageMetadata; }

        [Serializable] private class GReplyOption { public string word; public string clueId; }
        [Serializable] private class GReplyLine { public string text; public GReplyOption[] options; }
        [Serializable] private class GReplyAsk { public string label; public string tone; }
        [Serializable] private class GReplyRoot { public GReplyLine[] reply; public GReplyAsk[] asks; }
    }
}
