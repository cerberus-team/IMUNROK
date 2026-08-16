using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 심문 무대 진행. 두 가지 행동:
    ///  ① 추천 질문 — 인물마다 정해둔 질문 칩을 눌러 묻는다(대화로 단서를 얻기도 함)
    ///  ② 증거 제시 — 수첩에서 단서를 골라 들이밀기(맞는 증거면 인물이 사실을 실토)
    ///
    /// NPC 대답은 INpcResponder가 만든다(Mock=미리 정한 대사 / Gemini=실제 AI).
    ///
    /// ※ 자유 텍스트 입력은 아직 없다. 화면의 마이크 버튼은 VR 음성 입력(STT)을
    ///   붙일 자리표시이며 지금은 동작하지 않는다.
    /// ※ UI가 OnGUI(IMGUI)라 VR 헤드셋에는 렌더링되지 않는다 — World Space Canvas 이관 필요.
    /// </summary>
    public class InterrogationController : MonoBehaviour, ISelectable
    {
        public enum Backend { Mock, Gemini }

        [Header("설정")]
        [SerializeField] private InterrogationCharacter _character;
        [Tooltip("Mock=미리 정한 대사, Gemini=실제 AI. Gemini는 키 없으면 자동으로 Mock으로 대체됨")]
        [SerializeField] private Backend _backend = Backend.Gemini;
        [Tooltip("Gemini 모델 이름. 404 나면 모델명이 틀린 것")]
        [SerializeField] private string _geminiModel = "gemini-flash-latest";
        [SerializeField] private string _hubSceneName = "HubScene";

        [Tooltip("테스트용: 인물의 증거 게이트 단서를 수첩에 미리 채워 제시할 수 있게 함. " +
                 "켜면 조사를 안 해도 결정적 증거를 전부 들이밀 수 있으니 평소엔 꺼둘 것")]
        [SerializeField] private bool _seedGateCluesForTest = false;

        [Tooltip("켜짐=씬 시작 시 자동 심문(단독 무대). 꺼짐=인물 큐브 클릭 시 시작(큐브에 붙일 때 이걸로)")]
        [SerializeField] private bool _beginOnStart = true;

        [Tooltip("추천 질문(대사 위 제안) 표시. VR(음성)에선 꺼서 '말로만' 진행 가능")]
        [SerializeField] private bool _showTopics = true;

        [Tooltip("이 거리(m) 안에서만 말을 걸 수 있음. 너무 멀면 클릭해도 안 열림")]
        [SerializeField] private float _maxTalkDistance = 3f;

        [Header("한지 테마(선택 — 넣으면 두루마리 느낌)")]
        [Tooltip("패널 배경으로 쓸 한지 텍스처(없으면 어두운 기본)")]
        [SerializeField] private Texture2D _paperTex;
        [Tooltip("대사·버튼 폰트(조선궁서체 등). 없으면 기본")]
        [SerializeField] private Font _font;
        [Tooltip("한지 패널 불투명도(낮출수록 뒤가 비침)")]
        [Range(0.3f, 1f)] [SerializeField] private float _paperAlpha = 0.5f;

        private bool _active;

        // 지금 심문창이 하나라도 열려 있나(다른 UI가 참고: 목표 HUD 숨김 등)
        private static int s_openCount;
        public static bool AnyOpen => s_openCount > 0;

        // 지금 열려 있는 심문(수첩에서 증거를 들이밀 대상)
        public static InterrogationController Active { get; private set; }

        private INpcResponder _responder;
        private readonly List<string> _transcript = new List<string>();
        private readonly List<string> _unlockedFacts = new List<string>();
        private readonly HashSet<string> _unlockedGateKeys = new HashSet<string>();
        private readonly HashSet<string> _grantedTopics = new HashSet<string>();

        private string _npcLine = "";
        private bool _busy;
        private bool _exitRequested;

        private GUIStyle _nameStyle, _lineStyle, _playerStyle, _hintStyle, _btnStyle, _micStyle;
        private Texture2D _texDim, _texPanel, _texName, _texBtn, _texBtnOn;
        private string _lastPlayerLine = "";
        private Texture2D _evidenceImg;   // 방금 제시한 증거 그림(잠깐 표시)
        private float _evidenceImgTimer;

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
            _busy = false;

            _responder = MakeResponder();
            _npcLine = _character.openingLine;
            _transcript.Add($"{_character.characterName}: {_npcLine}");
            _active = true;
            s_openCount++;
            Active = this;
        }

        private void OnDisable()
        {
            if (_active) { _active = false; s_openCount = Mathf.Max(0, s_openCount - 1); }
            if (Active == this) Active = null;
        }

        // ── 클릭/VR 레이로 인물을 선택하면 심문 시작 ──
        public void OnHoverEnter() { }
        public void OnHoverExit() { }
        public void OnSelect()
        {
            if (_active) return;
            var cam = Camera.main;
            if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > _maxTalkDistance)
                return;   // 너무 멀다 → 무시(다가가야 말을 걸 수 있음)
            Begin();
        }

        private void ClosePanel()
        {
            if (_beginOnStart) { _exitRequested = true; return; }      // 단독 무대 → 조사청 복귀
            if (_active) { _active = false; s_openCount = Mathf.Max(0, s_openCount - 1); } // 큐브 → 패널만 닫기
            if (Active == this) Active = null;
        }

        /// <summary>수첩에서 단서를 골라 "들이밀기" 눌렀을 때 호출(외부에서 증거 제시).</summary>
        public void PresentFromJournal(ClueEntry clue)
        {
            if (_active && !_busy) Present(clue);
        }

        private INpcResponder MakeResponder()
        {
            if (_backend == Backend.Gemini)
                return new GeminiNpcResponder(_geminiModel); // 키 없으면 내부에서 자동으로 Mock으로 대체됨
            return new MockNpcResponder();
        }

        // ── 행동 ①: 추천 질문 고르기 ──
        private void AskTopic(TopicQuestion t)
        {
            if (_busy || t == null) return;
            _lastPlayerLine = t.question;
            _transcript.Add($"어사: {t.question}");

            // 이 대화로 단서 얻기(한 번만)
            if (!string.IsNullOrEmpty(t.grantsClueKey) && !_grantedTopics.Contains(t.grantsClueKey))
            {
                _grantedTopics.Add(t.grantsClueKey);
                Journal.Instance.AddClue(_character.caseId, t.grantsClueKey, t.grantsClueText, t.grantsClueImage, ClueKind.정황);
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
            _lastPlayerLine = $"(증거) {clue.text}";
            _transcript.Add($"어사(증거): {clue.text}");

            // 제시한 증거의 상황 그림을 잠깐 "탁" 띄운다
            _evidenceImg = Journal.Instance.GetClueImage(_character.caseId, clue.key);
            _evidenceImgTimer = _evidenceImg != null ? 4.5f : 0f;

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
                    Journal.Instance.AddClue(_character.caseId, gate.clueKey + "_revealed", gate.revealsInfo, null, ClueKind.정황);
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
            if (_evidenceImgTimer > 0f) _evidenceImgTimer -= Time.deltaTime;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            // ESC로 심문창 닫기(인물 큐브 방식일 때). J는 수첩(JournalView)이 처리
            if (_active && kb != null && !_beginOnStart && kb.escapeKey.wasPressedThisFrame)
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
            if (JournalView.AnyOpen) return;   // 수첩 펼치면 심문 UI 숨김(수첩만 보이게)
            EnsureStyles();

            // 화면 전체를 살짝 어둡게 → 대사에 집중(연출)
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _texDim);

            // 방금 제시한 증거 그림을 상단 중앙에 잠깐 (마지막 1초 페이드)
            if (_evidenceImgTimer > 0f && _evidenceImg != null)
            {
                float iwd = 300f, ihd = 220f;
                float ex = (Screen.width - iwd) * 0.5f, ey = 40f;
                float a = Mathf.Clamp01(_evidenceImgTimer);
                var prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.55f * a);
                GUI.DrawTexture(new Rect(ex - 8, ey - 8, iwd + 16, ihd + 16), _texPanel);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(new Rect(ex, ey, iwd, ihd), _evidenceImg, ScaleMode.ScaleToFit);
                GUI.color = prev;
            }

            float margin = 24f;

            // 하단부터 위로: [마이크] → [추천 질문] → [대사 박스]
            float micSize = 60f;
            float micX = (Screen.width - micSize) * 0.5f;
            float micY = Screen.height - margin - micSize;

            bool hasTopics = _showTopics && _character.topics != null && _character.topics.Count > 0;
            const float chipH = 30f;
            float chipY = micY - 14f - chipH;

            float dlgW = Mathf.Min(760f, Screen.width - margin * 2f);
            float dlgH = 150f;
            float dlgX = (Screen.width - dlgW) * 0.5f;
            float dlgBottom = hasTopics ? (chipY - 14f) : (micY - 14f);
            float dlgY = dlgBottom - dlgH;

            // ── 대사 박스 ──
            PanelBg(new Rect(dlgX, dlgY, dlgW, dlgH));

            string nm = _character.characterName;
            float nameW = Mathf.Max(120f, _nameStyle.CalcSize(new GUIContent(nm)).x + 30f);
            GUI.DrawTexture(new Rect(dlgX + 18f, dlgY - 16f, nameW, 32f), _texName);
            GUI.Label(new Rect(dlgX + 18f, dlgY - 16f, nameW, 32f), nm, _nameStyle);

            if (GUI.Button(new Rect(dlgX + dlgW - 92f, dlgY - 14f, 84f, 28f), "✕ 닫기", _btnStyle))
                ClosePanel();

            float sy = dlgY + 16f;
            if (!string.IsNullOrEmpty(_lastPlayerLine))
            {
                GUI.Label(new Rect(dlgX + 22f, sy, dlgW - 44f, 22f), "어사 —  " + _lastPlayerLine, _playerStyle);
                sy += 26f;
            }
            GUI.Label(new Rect(dlgX + 22f, sy, dlgW - 44f, dlgY + dlgH - sy - 30f), _busy ? "…" : _npcLine, _lineStyle);
            GUI.Label(new Rect(dlgX + 22f, dlgY + dlgH - 24f, dlgW - 44f, 20f), "증거는 수첩(J)에서 제시", _hintStyle);

            // ── 추천 질문(가로 한 줄, 가운데) ──
            if (hasTopics)
            {
                int nT = _character.topics.Count;
                const float gap = 14f;
                var ws = new float[nT];
                float total = 0f;
                for (int i = 0; i < nT; i++)
                {
                    ws[i] = _btnStyle.CalcSize(new GUIContent(_character.topics[i].question)).x + 28f;
                    total += ws[i];
                }
                total += gap * (nT - 1);
                float sx = (Screen.width - total) * 0.5f;
                for (int i = 0; i < nT; i++)
                {
                    if (GUI.Button(new Rect(sx, chipY, ws[i], chipH), _character.topics[i].question, _btnStyle) && !_busy)
                        AskTopic(_character.topics[i]);
                    sx += ws[i] + gap;
                }
            }

            // ── 마이크 버튼(하단 중앙) — VR에서 눌러 말하기(음성) 자리 ──
            if (GUI.Button(new Rect(micX, micY, micSize, micSize), "🎤", _micStyle))
            {
                // 지금은 자리표시. VR에서 음성 입력(STT)과 연결 예정.
            }
        }

        // 패널 배경: 한지 텍스처(있으면 반투명)로, 없으면 어두운 기본
        private void PanelBg(Rect r)
        {
            if (_paperTex != null)
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, _paperAlpha);
                GUI.DrawTexture(r, _paperTex, ScaleMode.ScaleAndCrop);
                GUI.color = prev;
            }
            else GUI.DrawTexture(r, _texPanel);
        }

        private void EnsureStyles()
        {
            if (_lineStyle != null) return;

            bool paper = _paperTex != null;
            Color ink = new Color(0.16f, 0.11f, 0.07f);           // 먹색
            Color inkSoft = new Color(0.16f, 0.11f, 0.07f, 0.7f);

            _texDim   = Solid(new Color(0f, 0f, 0f, 0.16f));
            _texPanel = Solid(new Color(0.03f, 0.035f, 0.05f, 0.86f));
            _texName  = Solid(new Color(0.62f, 0.14f, 0.11f, 0.95f));   // 낙관(붉은 인장) 느낌
            _texBtn   = Solid(paper ? new Color(0f, 0f, 0f, 0.06f) : new Color(1f, 1f, 1f, 0.07f));
            _texBtnOn = Solid(paper ? new Color(0.62f, 0.14f, 0.11f, 0.18f) : new Color(1f, 0.85f, 0.4f, 0.22f));

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.98f, 0.94f, 0.86f) }   // 인장 위 밝은 글씨
            };
            _lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21, wordWrap = true, richText = true,
                normal = { textColor = paper ? ink : Color.white }
            };
            _playerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Italic,
                normal = { textColor = paper ? inkSoft : new Color(1f, 1f, 1f, 0.55f) }
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                normal = { textColor = paper ? new Color(0.5f, 0.2f, 0.12f) : new Color(1f, 0.85f, 0.5f, 0.95f) }
            };
            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, alignment = TextAnchor.MiddleCenter,
                normal = { background = _texBtn, textColor = paper ? ink : new Color(1f, 1f, 1f, 0.92f) },
                hover  = { background = _texBtnOn, textColor = paper ? new Color(0.5f, 0.15f, 0.1f) : Color.white },
                active = { background = _texBtnOn, textColor = paper ? new Color(0.5f, 0.15f, 0.1f) : Color.white }
            };
            _micStyle = new GUIStyle(GUI.skin.button) { fontSize = 30, alignment = TextAnchor.MiddleCenter };

            // 폰트 적용(조선궁서체 등) — 마이크(이모지)는 기본 폰트 유지
            if (_font != null)
            {
                _nameStyle.font = _font; _lineStyle.font = _font; _playerStyle.font = _font;
                _hintStyle.font = _font; _btnStyle.font = _font;
            }
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        // HideAndDontSave 텍스처는 씬이 바뀌어도 자동 정리되지 않는다.
        // 인물마다 5장씩 만들므로 직접 파괴해 준다.
        private void OnDestroy()
        {
            foreach (var t in new[] { _texDim, _texPanel, _texName, _texBtn, _texBtnOn })
                if (t != null) Destroy(t);
        }
    }
}
