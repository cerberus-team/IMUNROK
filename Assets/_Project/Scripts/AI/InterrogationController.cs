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
    public class InterrogationController : MonoBehaviour
    {
        public enum Backend { Mock, Claude }

        [Header("설정")]
        [SerializeField] private InterrogationCharacter _character;
        [SerializeField] private Backend _backend = Backend.Mock;
        [SerializeField] private string _hubSceneName = "HubScene";

        [Tooltip("테스트용: 인물의 증거 게이트 단서를 수첩에 미리 채워 제시할 수 있게 함")]
        [SerializeField] private bool _seedGateCluesForTest = true;

        private INpcResponder _responder;
        private readonly List<string> _transcript = new List<string>();
        private readonly List<string> _unlockedFacts = new List<string>();
        private readonly HashSet<string> _unlockedGateKeys = new HashSet<string>();

        private string _npcLine = "";
        private string _typed = "";
        private bool _busy;
        private bool _exitRequested;

        private GUIStyle _nameStyle, _lineStyle, _logStyle, _hintStyle;

        private void Start()
        {
            if (_character == null)
            {
                Debug.LogError("[InterrogationController] 심문 캐릭터가 지정되지 않았습니다.");
                enabled = false;
                return;
            }

            // 심문은 사건 안에서 일어난다 → 수첩이 이 사건 단서를 보여줌
            GameState.Instance.EnterCase(_character.caseId);

            // 테스트용: 제시할 단서가 없으면 게이트 단서를 수첩에 채워둔다
            if (_seedGateCluesForTest)
                foreach (var gate in _character.evidenceGates)
                    if (!string.IsNullOrEmpty(gate.clueKey))
                        Journal.Instance.AddClue(_character.caseId, gate.clueKey, gate.clueText);

            _responder = MakeResponder();

            _npcLine = _character.openingLine;
            _transcript.Add($"{_character.characterName}: {_npcLine}");
        }

        private INpcResponder MakeResponder()
        {
            if (_backend == Backend.Claude)
            {
                // 내일 여기에 return new ClaudeNpcResponder(...); 를 연결한다.
                Debug.LogWarning("[InterrogationController] 실제 AI(Claude)는 아직 연결 전 — 목업으로 대체합니다.");
            }
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

        // ── 행동 ②: 증거 제시 ──
        private void Present(ClueEntry clue)
        {
            if (_busy || clue == null) return;
            _transcript.Add($"어사(증거): {clue.text}");

            // 이 증거가 여는 사실이 있으면 잠금 해제
            string revealed = null;
            foreach (var gate in _character.evidenceGates)
            {
                if (gate.clueKey == clue.key && !_unlockedGateKeys.Contains(gate.clueKey))
                {
                    _unlockedGateKeys.Add(gate.clueKey);
                    _unlockedFacts.Add(gate.revealsInfo);
                    revealed = gate.revealsInfo;
                    // 열린 사실을 새 단서로 수첩에 기록(심문이 수첩을 키운다)
                    Journal.Instance.AddClue(_character.caseId, gate.clueKey + "_revealed", gate.revealsInfo);
                    break;
                }
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
            if (_character == null) return;
            EnsureStyles();

            float w = Mathf.Min(760f, Screen.width - 40f);
            float x = (Screen.width - w) * 0.5f;
            float y = 24f;

            // 인물 + 현재 대사
            GUI.Box(new Rect(x, y, w, 150f), GUIContent.none);
            GUI.Label(new Rect(x + 16, y + 10, w - 32, 26), _character.characterName, _nameStyle);
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

            // 말하기
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
                _exitRequested = true;
        }

        private void EnsureStyles()
        {
            if (_lineStyle != null) return;
            _nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
            _lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true,
                normal = { textColor = Color.white } };
            _logStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false,
                normal = { textColor = new Color(1f, 1f, 1f, 0.75f) } };
            _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14,
                normal = { textColor = new Color(0.85f, 0.9f, 1f) } };
        }
    }
}
