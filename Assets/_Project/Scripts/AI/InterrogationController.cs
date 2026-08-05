using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 심문 무대 진행. 두 가지 행동:
    ///  ① 말하기 — 텍스트로 질문(나중에 음성으로 교체)
    ///  ② 증거 제시 — 수첩에서 단서를 골라 들이밀기(맞는 증거면 인물이 사실을 실토)
    ///
    /// NPC 대답은 INpcResponder가 만든다. 지금은 Mock(녹음테이프),
    /// 내일 Backend를 Claude로 바꾸면 실제 AI가 같은 자리에 들어온다.
    /// 자막은 OnGUI(한글 확실히 표시).
    /// </summary>
    public class InterrogationController : MonoBehaviour, ISelectable
    {
        public enum Backend { Mock, Gemini }

        [Header("설정")]
        [SerializeField] private InterrogationCharacter _character;
        [Tooltip("Mock=미리 정한 대사, Gemini=실제 AI. Gemini는 키 없으면 자동으로 Mock으로 대체됨")]
        [SerializeField] private Backend _backend = Backend.Gemini;
        [Tooltip("Gemini 모델 이름. 404 나면 [이문록 ▸ Gemini: 사용 가능 모델 목록 확인]으로 유효한 이름 찾아 넣기")]
        [SerializeField] private string _geminiModel = "gemini-flash-latest";
        [SerializeField] private string _hubSceneName = "HubScene";

        [Tooltip("테스트용: 인물의 증거 게이트 단서를 수첩에 미리 채워 제시할 수 있게 함")]
        [SerializeField] private bool _seedGateCluesForTest = true;

        [Tooltip("켜짐=씬 시작 시 자동 심문(단독 무대). 꺼짐=인물 큐브 클릭 시 시작(큐브에 붙일 때 이걸로)")]
        [SerializeField] private bool _beginOnStart = true;

        private bool _active;

        // 지금 심문창이 하나라도 열려 있나(다른 UI가 참고: 목표 HUD 숨김 등)
        private static int s_openCount;
        public static bool AnyOpen => s_openCount > 0;

        private INpcResponder _responder;
        private readonly List<string> _transcript = new List<string>();
        private readonly List<string> _unlockedFacts = new List<string>();
        private readonly HashSet<string> _unlockedGateKeys = new HashSet<string>();
        private readonly HashSet<string> _grantedTopics = new HashSet<string>();

        private string _npcLine = "";
        private string _typed = "";
        private bool _busy;
        private bool _exitRequested;

        private GUIStyle _nameStyle, _lineStyle, _logStyle, _hintStyle;

        private void Start()
        {
            if (_beginOnStart) Begin();
        }

        /// <summary>심문 시작(단독 무대는 Start에서, 인물 큐브는 클릭 때 호출).</summary>
        public void Begin()
        {
            if (_active) return;
            if (_character == null)
            {
                Debug.LogError("[InterrogationController] 심문 캐릭터가 지정되지 않았습니다.");
                return;
            }

            // 심문은 사건 안에서 일어난다 → 수첩이 이 사건 단서를 보여줌
            GameState.Instance.EnterCase(_character.caseId);

            // 테스트용: 제시할 단서가 없으면 게이트 단서를 수첩에 채워둔다
            if (_seedGateCluesForTest)
                foreach (var gate in _character.evidenceGates)
                    if (!string.IsNullOrEmpty(gate.clueKey))
                        Journal.Instance.AddClue(_character.caseId, gate.clueKey, gate.clueText);

            // 상태 초기화(다시 말 걸 때도 깨끗하게)
            _transcript.Clear();
            _unlockedFacts.Clear();
            _unlockedGateKeys.Clear();
            _grantedTopics.Clear();
            _typed = "";
            _busy = false;

            _responder = MakeResponder();
            _npcLine = _character.openingLine;
            _transcript.Add($"{_character.characterName}: {_npcLine}");
            _active = true;
            s_openCount++;
        }

        private void OnDisable()
        {
            if (_active) { _active = false; s_openCount = Mathf.Max(0, s_openCount - 1); }
        }

        // ── 클릭/VR 레이로 인물을 선택하면 심문 시작 ──
        public void OnHoverEnter() { }
        public void OnHoverExit() { }
        public void OnSelect() { if (!_active) Begin(); }

        private void ClosePanel()
        {
            if (_beginOnStart) { _exitRequested = true; return; }      // 단독 무대 → 조사청 복귀
            if (_active) { _active = false; s_openCount = Mathf.Max(0, s_openCount - 1); } // 큐브 → 패널만 닫기
        }

        private INpcResponder MakeResponder()
        {
            if (_backend == Backend.Gemini)
                return new GeminiNpcResponder(_geminiModel); // 키 없으면 내부에서 자동으로 Mock으로 대체됨
            return new MockNpcResponder();
        }

        // ── 행동 ①: 말하기 ──
        private void Say(string text)
        {
            if (_busy || string.IsNullOrWhiteSpace(text)) return;
            string say = text.Trim();
            _transcript.Add($"어사: {say}");

            var req = new NpcRequest
            {
                character = _character,
                transcript = _transcript,
                unlockedFacts = _unlockedFacts,
                playerInput = say,
                isEvidence = false,
                justRevealedInfo = null,
            };
            _busy = true;
            _responder.GetResponse(this, req, OnReply, OnError);
        }

        // ── 추천 질문 고르기 ──
        private void AskTopic(TopicQuestion t)
        {
            if (_busy || t == null) return;
            _transcript.Add($"어사: {t.question}");

            // 이 대화로 단서 얻기(한 번만)
            if (!string.IsNullOrEmpty(t.grantsClueKey) && !_grantedTopics.Contains(t.grantsClueKey))
            {
                _grantedTopics.Add(t.grantsClueKey);
                Journal.Instance.AddClue(_character.caseId, t.grantsClueKey, t.grantsClueText);
            }

            var req = new NpcRequest
            {
                character = _character,
                transcript = _transcript,
                unlockedFacts = _unlockedFacts,
                playerInput = t.question,
                isEvidence = false,
                justRevealedInfo = null,
                scriptedAnswer = t.mockAnswer,
            };
            _busy = true;
            _responder.GetResponse(this, req, OnReply, OnError);
        }

        // ── 행동 ②: 증거 제시 ──
        private void Present(ClueEntry clue)
        {
            if (_busy || clue == null) return;
            _transcript.Add($"어사(증거): {clue.text}");

            // 이 증거가 여는 사실이 있으면 잠금 해제(발뺌 전용이면 말만 하고 기록 안 함)
            string revealed = null;
            foreach (var gate in _character.evidenceGates)
            {
                if (gate.clueKey != clue.key) continue;

                if (gate.deflectionOnly)
                {
                    revealed = gate.revealsInfo;   // 발뺌 대사(반복 가능, 단서 기록 X)
                    break;
                }
                if (!_unlockedGateKeys.Contains(gate.clueKey))
                {
                    _unlockedGateKeys.Add(gate.clueKey);
                    _unlockedFacts.Add(gate.revealsInfo);
                    revealed = gate.revealsInfo;
                    // 열린 사실을 새 단서로 수첩에 기록(심문이 수첩을 키운다)
                    Journal.Instance.AddClue(_character.caseId, gate.clueKey + "_revealed", gate.revealsInfo);
                }
                break;
            }

            var req = new NpcRequest
            {
                character = _character,
                transcript = _transcript,
                unlockedFacts = _unlockedFacts,
                playerInput = clue.text,
                isEvidence = true,
                justRevealedInfo = revealed,
            };
            _busy = true;
            _responder.GetResponse(this, req, OnReply, OnError);
        }

        private void OnReply(string text)
        {
            _npcLine = text;
            _transcript.Add($"{_character.characterName}: {text}");
            _busy = false;
        }

        private void OnError(string err)
        {
            _npcLine = $"(대답 오류: {err})";
            _busy = false;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // ESC로 심문창 닫기(인물 큐브 방식일 때)
            if (_active && !_beginOnStart && UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                ClosePanel();
#endif

            // 씬 전환은 OnGUI 밖(안전)에서 처리
            if (_exitRequested)
            {
                _exitRequested = false;
                GameState.Instance.ExitToHub();
                if (!string.IsNullOrEmpty(_hubSceneName) && Application.CanStreamedLevelBeLoaded(_hubSceneName))
                    SceneManager.LoadScene(_hubSceneName);
            }
        }

        // ─────────────────────────────────────────────
        //  UI (OnGUI — 좌표 지정 방식으로 안전하게)
        // ─────────────────────────────────────────────
        private void OnGUI()
        {
            if (_character == null || !_active) return;
            EnsureStyles();

            float w = Mathf.Min(760f, Screen.width - 40f);
            float x = (Screen.width - w) * 0.5f;
            float y = 24f;

            // 인물 + 현재 대사
            GUI.Box(new Rect(x, y, w, 150f), GUIContent.none);
            GUI.Label(new Rect(x + 16, y + 10, w - 120, 26), _character.characterName, _nameStyle);
            if (GUI.Button(new Rect(x + w - 96, y + 8, 88, 26), "✕ 닫기 (ESC)"))
                ClosePanel();
            GUI.Label(new Rect(x + 16, y + 40, w - 32, 100f), _busy ? "…" : _npcLine, _lineStyle);
            y += 162f;

            // 최근 대화 로그(마지막 6줄)
            GUI.Box(new Rect(x, y, w, 150f), GUIContent.none);
            float ly = y + 10f;
            int start = Mathf.Max(0, _transcript.Count - 6);
            for (int i = start; i < _transcript.Count; i++)
            {
                GUI.Label(new Rect(x + 14, ly, w - 28, 22), _transcript[i], _logStyle);
                ly += 22f;
            }
            y += 162f;

            // 추천 질문(대화 버튼)
            if (_character.topics != null && _character.topics.Count > 0)
            {
                GUI.Label(new Rect(x, y, w, 22), "질문 고르기:", _hintStyle);
                y += 26f;
                foreach (var t in _character.topics)
                {
                    if (GUI.Button(new Rect(x, y, w, 26), $"“{t.question}”") && !_busy)
                        AskTopic(t);
                    y += 30f;
                }
                y += 6f;
            }

            // 말하기(자유 입력 — 나중에 VR 음성으로 교체)
            GUI.Label(new Rect(x, y, 120, 24), "말하기:", _hintStyle);
            GUI.SetNextControlName("SayField");
            _typed = GUI.TextField(new Rect(x + 70, y, w - 200, 26), _typed);
            if (GUI.Button(new Rect(x + w - 120, y, 120, 26), _busy ? "..." : "묻기"))
            {
                Say(_typed);
                _typed = "";
            }
            // Enter로도 전송
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
                && GUI.GetNameOfFocusedControl() == "SayField" && !_busy)
            {
                Say(_typed);
                _typed = "";
                Event.current.Use();
            }
            y += 40f;

            // 증거 제시(수첩의 현재 사건 단서)
            GUI.Label(new Rect(x, y, w, 22), "증거 제시 (수첩):", _hintStyle);
            y += 26f;
            var clues = Journal.Instance.GetClues(_character.caseId);
            foreach (var c in clues)
            {
                if (c.key.EndsWith("_revealed")) continue; // 실토로 생긴 사실은 제시 대상 아님
                if (GUI.Button(new Rect(x, y, w, 26), $"제시: {c.text}"))
                    Present(c);
                y += 30f;
            }

            // 나가기
            y += 8f;
            if (GUI.Button(new Rect(x, y, 160, 28), "심문 끝내기 →"))
                ClosePanel();
        }

        private void EnsureStyles()
        {
            if (_lineStyle != null) return;
            _nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
            _lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true,
                normal = { textColor = Color.white } };
            _logStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false,
                normal = { textColor = new Color(1f, 1f, 1f, 0.75f) } };
            _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14,
                normal = { textColor = new Color(0.85f, 0.9f, 1f) } };
        }
    }
}
