using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 심문 무대 진행. 세 가지 행동:
    ///  ① 말하기 — 마이크로 직접 묻는다(<see cref="MicInput"/>의 전사가 <see cref="Say"/>로 들어옴)
    ///  ② 추천 질문 — 인물마다 정해둔 질문 칩을 눌러 묻는다(대화로 단서를 얻기도 함)
    ///  ③ 증거 제시 — 수첩에서 단서를 골라 들이밀기(맞는 증거면 인물이 사실을 실토)
    ///
    /// NPC 대답은 INpcResponder가 만든다(Mock=미리 정한 대사 / Gemini=실제 AI).
    /// 마이크는 씬에 MicInput이 있을 때만 붙는다 — 없으면 ②③으로만 진행된다.
    ///
    /// UI는 전부 월드 공간이다 — 대사는 SubtitleView, 조작은 InterrogationPanel.
    /// 둘 다 씬에 미리 둘 필요 없이 심문이 시작될 때 스스로 만들어진다.
    /// </summary>
    public class InterrogationController : MonoBehaviour, ISelectable
    {
        public enum Backend { Mock, Gemini }

        [Header("설정")]
        [SerializeField] private InterrogationCharacter _character;
        [Tooltip("Mock=미리 정한 대사, Gemini=실제 AI. Gemini는 키 없으면 자동으로 Mock으로 대체됨")]
        [SerializeField] private Backend _backend = Backend.Gemini;
        [Tooltip("Gemini 모델 이름. 404 나면 모델명이 틀린 것")]
        [SerializeField] private string _geminiModel = "gemini-3.5-flash-lite";
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

        [Tooltip("켜면 처음엔 말을 걸 수 없다. 다른 스크립트가 Unlock()을 부른 뒤부터 열림. " +
                 "제 볼일이 끝난 다음에야 붙잡을 수 있는 인물(마름처럼)에 쓴다")]
        [SerializeField] private bool _lockedAtStart = false;

        [Tooltip("심문을 닫은 뒤 인물의 마지막 한 마디를 몇 초 동안 자막으로 남길지")]
        [SerializeField] private float _closingLineSeconds = 5f;

        [Tooltip("대사에 쓸 한글 폰트(조선궁서체 등). 월드 UI가 공용으로 가져다 쓴다")]
        [SerializeField] private Font _font;

        private bool _active;
        private bool _locked;

        /// <summary>인물들이 플레이어를 부르는 이름표. 1막은 과객이라 '나그네'(→ GameState가 정한다).</summary>
        private static string PlayerTitle => GameState.Instance.PlayerTitle;

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

        private string _lastPlayerLine = "";

        [Tooltip("클릭용 콜라이더가 없으면 몸 크기에 맞춰 자동으로 붙인다. 없으면 말을 걸 수 없다")]
        [SerializeField] private bool _autoFitCollider = true;

        private void Awake()
        {
            // 이 컴포넌트에 이미 한글 폰트가 연결돼 있으면 월드 Canvas 쪽 UI도 같이 쓰게 공유한다.
            UiFont.Publish(_font);
        }

        /// <summary>
        /// 눈에 보이는 몸을 클릭할 수 있게 만든다.
        ///
        /// 인물은 보통 [마커] → [캐릭터 FBX] 구조인데, 콜라이더가 마커에만 있으면
        /// 화면의 캐릭터를 눌러도 레이가 그냥 통과한다(마커 큐브는 눈에 안 보이니 원인 찾기가 어렵다).
        /// 그래서 "콜라이더가 있는가"가 아니라 "몸이 있는 자리를 덮는 콜라이더가 있는가"를 본다.
        /// </summary>
        private void EnsureClickable()
        {
            if (!_autoFitCollider) return;
            if (!ModelBounds.TryGet(transform, out Bounds body)) return;

            foreach (var c in GetComponentsInChildren<Collider>())
                if (c.bounds.Contains(body.center)) return;   // 몸 자리를 덮는 콜라이더가 이미 있다

            // 렌더러가 달린 오브젝트(=실제 몸)에 붙여야 클릭 위치가 몸과 일치한다.
            var rend = GetComponentInChildren<Renderer>();
            var host = rend != null ? rend.transform : transform;
            // SkinnedMeshRenderer는 본 아래에 있을 수 있으니, 모델 루트 쪽으로 한 단계 올린다.
            if (host != transform && host.parent != null && host.parent != transform) host = host.parent;

            var col = host.gameObject.AddComponent<CapsuleCollider>();
            Vector3 ls = host.lossyScale;
            col.center = host.InverseTransformPoint(body.center);
            col.height = body.size.y / Mathf.Max(0.0001f, Mathf.Abs(ls.y));
            col.radius = Mathf.Max(body.size.x, body.size.z) * 0.5f / Mathf.Max(0.0001f, Mathf.Abs(ls.x));
            Debug.Log($"[InterrogationController] '{name}' — 몸을 덮는 콜라이더가 없어 '{host.name}' 에 자동으로 붙였습니다.");
        }

#if UNITY_EDITOR
        /// <summary>왜 클릭이 안 되는지 알아볼 수 있게 지금 상태를 그대로 찍는다.</summary>
        [ContextMenu("진단: 이 인물 상태 찍기")]
        private void Diagnose()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[진단] {name}");
            sb.AppendLine($"  이 오브젝트 pos = {transform.position}");
            bool has = ModelBounds.TryGet(transform, out Bounds b);
            sb.AppendLine(has ? $"  키(높이) = {b.size.y:F3} m   ← 인물끼리 비교할 값"
                              : "  렌더러를 못 찾음 — 자식에 모델이 없다");
            if (has) sb.AppendLine($"  몸 center={b.center} min.y={b.min.y:F3} size={b.size}");
            foreach (var r in GetComponentsInChildren<Renderer>())
                sb.AppendLine($"    렌더러: {r.name}  ({r.GetType().Name})  pos={r.transform.position}");
            var cols = GetComponentsInChildren<Collider>();
            sb.AppendLine($"  콜라이더 {cols.Length}개");
            foreach (var c in cols)
                sb.AppendLine($"    {c.name} ({c.GetType().Name}) bounds.center={c.bounds.center} size={c.bounds.size}" +
                              (has && c.bounds.Contains(b.center) ? "  <- 몸 자리를 덮음" : "  <- 몸과 어긋남"));
            var anim = GetComponentInChildren<Animator>();
            sb.AppendLine(anim != null ? $"  Animator: '{anim.name}' pos={anim.transform.position} rootMotion={anim.applyRootMotion}"
                                       : "  Animator 없음");
            var cam = Camera.main;
            if (cam != null)
            {
                sb.AppendLine($"  카메라~피벗 = {Vector3.Distance(cam.transform.position, transform.position):F2} m");
                sb.AppendLine($"  카메라~몸   = {ModelBounds.DistanceTo(transform, cam.transform.position):F2} m   (허용 {_maxTalkDistance} m)");
            }
            Debug.Log(sb.ToString(), this);
        }
#endif

        private void Start()
        {
            EnsureClickable();
            _locked = _lockedAtStart;
            if (_beginOnStart) Begin();
        }

        /// <summary>지금 말을 걸 수 있나(잠겨 있으면 클릭해도 안 열린다).</summary>
        public bool Locked => _locked;

        /// <summary>말을 걸 수 있게 연다. 연출이 끝난 시점에 부르면 된다(마름은 제자리에 앉은 뒤).</summary>
        public void Unlock() { _locked = false; }

        /// <summary>다시 잠근다.</summary>
        public void Lock() { _locked = true; }

        /// <summary>심문 시작(단독 무대는 Start에서, 인물 큐브는 클릭 때 호출).</summary>
        public void Begin()
        {
            if (_active) return;
            if (_character == null)
            {
                Debug.LogError("[InterrogationController] 심문 캐릭터가 지정되지 않았습니다.");
                return;
            }

            // 앞서 헤어질 때 남긴 자막이 아직 사라지는 중일 수 있다 — 새 대화를 덮어쓰지 않게 취소한다
            CancelInvoke(nameof(HideClosingLine));

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

            if (MicInput.Instance != null)
            {
                MicInput.Instance.OnPartial += OnMicPartial;
                MicInput.Instance.OnFinal += OnMicFinal;
            }

            RefreshSubtitle();
            InterrogationPanel.Open(this);
        }

        private void OnDisable()
        {
            if (_active) { _active = false; s_openCount = Mathf.Max(0, s_openCount - 1); }
            if (Active == this) Active = null;
            UnsubscribeMic();
            SubtitleView.Hide();
            InterrogationPanel.Close();
        }

        private void UnsubscribeMic()
        {
            if (MicInput.Instance == null) return;
            MicInput.Instance.OnPartial -= OnMicPartial;
            MicInput.Instance.OnFinal -= OnMicFinal;
        }

        // ── 클릭/VR 레이로 인물을 선택하면 심문 시작 ──
        public void OnHoverEnter() { }
        public void OnHoverExit() { }
        public void OnSelect()
        {
            if (_active || _locked) return;
            var cam = Camera.main;
            // 피벗이 아니라 "몸"까지의 거리로 잰다. 외부 FBX는 피벗이 몸에서 멀리 떨어져 있을 수 있고,
            // 그러면 코앞에 서 있어도 '멀다'로 막혀 말을 걸 수 없다(옹덕구가 그랬다).
            if (cam != null && ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTalkDistance)
                return;
            Begin();
        }

        private void ClosePanel()
        {
            if (_beginOnStart) { _exitRequested = true; return; }      // 단독 무대 → 조사청 복귀
            if (_active) { _active = false; s_openCount = Mathf.Max(0, s_openCount - 1); } // 큐브 → 패널만 닫기
            if (Active == this) Active = null;
            UnsubscribeMic();
            InterrogationPanel.Close();

            // 돌아설 때 등 뒤로 던지는 한 마디.
            // 조작창은 닫고 자막만 잠깐 더 남긴다 — 무엇을 물었든 이건 반드시 듣게 된다.
            if (_character != null && !string.IsNullOrEmpty(_character.closingLine))
            {
                SubtitleView.Show(_character.characterName, _character.closingLine, "");
                CancelInvoke(nameof(HideClosingLine));
                Invoke(nameof(HideClosingLine), _closingLineSeconds);
            }
            else SubtitleView.Hide();
        }

        private void HideClosingLine() => SubtitleView.Hide();

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

        // ── 행동 ①: 말하기(마이크) ──

        /// <summary>플레이어가 말한 문장을 인물에게 던진다. MicInput의 최종 전사가 여기로 들어온다.</summary>
        public void Say(string text)
        {
            if (!_active || _busy || string.IsNullOrWhiteSpace(text)) return;
            string say = text.Trim();
            _lastPlayerLine = say;
            _transcript.Add($"{PlayerTitle}: {say}");

            var req = new NpcRequest
            {
                character = _character,
                transcript = _transcript,
                unlockedFacts = _unlockedFacts,
                playerInput = say,
                isEvidence = false,
                justRevealedInfo = null,
                playerTitle = PlayerTitle,
                playerIdentityBrief = GameState.Instance.PlayerIdentityBrief,
            };
            _busy = true;
            RefreshSubtitle();
            _responder.GetResponse(this, req, OnReply, OnError);
        }

        // 말하는 도중의 중간 전사 — 확정 전이라 대화 기록엔 넣지 않고 화면에만 보여준다.
        private void OnMicPartial(string text) { if (!_active) return; _lastPlayerLine = text; RefreshSubtitle(); }

        private void OnMicFinal(string text) { if (_active) Say(text); }

        // ── 행동 ②: 추천 질문 고르기 ──
        private void AskTopic(TopicQuestion t)
        {
            if (_busy || t == null) return;
            _lastPlayerLine = t.question;
            _transcript.Add($"{PlayerTitle}: {t.question}");

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
                playerTitle = PlayerTitle,
                playerIdentityBrief = GameState.Instance.PlayerIdentityBrief,
            };
            _busy = true;
            RefreshSubtitle();
            _responder.GetResponse(this, req, OnReply, OnError);
        }

        // ── 행동 ②: 증거 제시 ──
        private void Present(ClueEntry clue)
        {
            if (_busy || clue == null) return;
            _lastPlayerLine = $"(증거) {clue.text}";
            _transcript.Add($"{PlayerTitle}(증거): {clue.text}");

            // 제시한 증거의 상황 그림을 잠깐 "탁" 띄운다
            InterrogationPanel.ShowEvidence(Journal.Instance.GetClueImage(_character.caseId, clue.key), 4.5f);

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
                playerTitle = PlayerTitle,
                playerIdentityBrief = GameState.Instance.PlayerIdentityBrief,
            };
            _busy = true;
            RefreshSubtitle();
            _responder.GetResponse(this, req, OnReply, OnError);
        }

        private void OnReply(string text)
        {
            _npcLine = text;
            _transcript.Add($"{_character.characterName}: {text}");
            _busy = false;
            RefreshSubtitle();
        }

        private void OnError(string err)
        {
            _npcLine = $"(대답 오류: {err})";
            _busy = false;
            RefreshSubtitle();
        }

        /// <summary>
        /// 지금 대사를 월드 공간 자막(SubtitleView)에 내보낸다.
        /// 마이크로 말하는 동안에는 중간 전사가 아랫줄에 실시간으로 찍힌다.
        /// </summary>
        private void RefreshSubtitle()
        {
            if (!_active || _character == null) { SubtitleView.Hide(); return; }
            string line = _busy ? "…" : _npcLine;
            string hint = string.IsNullOrEmpty(_lastPlayerLine)
                        ? "마이크로 묻거나, 수첩에서 증거를 제시하시오"
                        : $"{PlayerTitle} — {_lastPlayerLine}";
            SubtitleView.Show(_character.characterName, line, hint);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            // ESC로 심문창 닫기(인물 큐브 방식일 때). J는 수첩(JournalView)이 처리
            if (_active && kb != null && !_beginOnStart && kb.escapeKey.wasPressedThisFrame)
                ClosePanel();
#endif

            // 씬 전환은 UI 콜백 밖(안전)에서 처리
            if (_exitRequested)
            {
                _exitRequested = false;
                GameState.Instance.ExitToHub();
                if (!string.IsNullOrEmpty(_hubSceneName) && Application.CanStreamedLevelBeLoaded(_hubSceneName))
                    SceneManager.LoadScene(_hubSceneName);
            }
        }

        // ─────────────────────────────────────────────
        //  UI — 전부 월드 공간으로 옮겼다.
        //  대사는 SubtitleView, 조작(마이크·추천질문·닫기·증거그림)은 InterrogationPanel.
        //  OnGUI는 헤드셋에 렌더링되지 않아 VR에서 아무것도 보이지 않았고,
        //  데스크탑에선 월드 UI와 겹쳐 보여 오히려 가렸다.
        // ─────────────────────────────────────────────

        /// <summary>월드 패널이 읽는 추천 질문 목록. 끄면(_showTopics=false) 말로만 진행한다.</summary>
        public IReadOnlyList<TopicQuestion> Topics
            => (_showTopics && _character != null) ? _character.topics : null;

        /// <summary>월드 패널의 추천 질문 버튼이 호출.</summary>
        public void AskTopicFromUi(TopicQuestion t) => AskTopic(t);

        /// <summary>월드 패널의 닫기 버튼이 호출.</summary>
        public void CloseFromUi() => ClosePanel();

    }
}
