using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 심문 무대 진행. 세 가지 행동:
    ///  ① 손으로 치기 — 자막 바의 글쇠 칸에 물음을 쳐서 던진다(<see cref="Say"/>로 들어옴)
    ///  ② 추천 질문 — 인물마다 정해둔 질문 칩을 눌러 묻는다(대화로 단서를 얻기도 함)
    ///  ③ 증거 제시 — 수첩에서 단서를 골라 들이밀기(맞는 증거면 인물이 사실을 실토)
    ///
    /// NPC 대답은 INpcResponder가 만든다(Mock=미리 정한 대사 / Gemini=실제 AI).
    ///
    /// <b>2026-09-05 — 말하기(마이크)를 걷어냈다.</b> ①은 원래 마이크로 말하면 받아
    /// 적히는 길이었다. Voice SDK(Wit.ai)가 딸려 오던 꾸러미를 걷어내면서
    /// 그것도 같이 나갔다. 그 자리는 <b>글쇠 칸</b>이 받는다 —
    /// 받아 적힌 말을 사람이 눈으로 보고 던지던 절차가 이미 있었으므로
    /// (<see cref="Draft"/>), 그 절차의 앞머리만 목소리에서 손으로 바뀐 셈이다.
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

        [Tooltip("추천 질문(대사 위 제안)을 띄운다")]
        [SerializeField] private bool _showTopics = true;

        [Tooltip("한 번에 보여 줄 추천 질문 수. 넷 다 늘어놓으면 고르는 것이 아니라 훑는 것이 된다 — " +
                 "셋이면 한눈에 들어오고, 하나를 물으면 그 자리에 다음 것이 올라선다")]
        [Range(1, 6)] [SerializeField] private int _topicsAtOnce = 3;

        [Tooltip("켜면 다시 말을 걸어도 <b>앞서 한 이야기를 기억한다</b> — 이미 물은 질문은 다시 안 뜨고 " +
                 "대화 기록도 이어진다. 동헌처럼 <b>불렀다 물렸다</b> 하는 자리에 쓴다. " +
                 "끄면 말을 걸 때마다 처음부터다(1막 마당 사람들이 그렇다)")]
        [SerializeField] private bool _rememberBetweenTalks = false;

        [Tooltip("이 거리(m) 안에서만 말을 걸 수 있음. 너무 멀면 클릭해도 안 열림")]
        [SerializeField] private float _maxTalkDistance = 3f;

        [Tooltip("말을 거는 중에 이만큼 멀어지면 대화가 저절로 끝난다. 0이면 안 끝난다. " +
                 "말 걸 수 있는 거리보다 넉넉히 잡는다 — 조금 물러섰다고 창이 닫히면 답답하다")]
        [SerializeField] private float _walkAwayDistance = 5f;

        [Tooltip("멀어진 뒤 이만큼(초) 지나야 끝낸다. 스쳐 지나가듯 잠깐 벗어난 것으로 끊기지 않게")]
        [SerializeField] private float _walkAwayGrace = 0.7f;

        [Tooltip("켜면 처음엔 말을 걸 수 없다. 다른 스크립트가 Unlock()을 부른 뒤부터 열림. " +
                 "제 볼일이 끝난 다음에야 붙잡을 수 있는 인물(마름처럼)에 쓴다")]
        [SerializeField] private bool _lockedAtStart = false;

        [Tooltip("심문을 닫은 뒤 인물의 마지막 한 마디를 몇 초 동안 자막으로 남길지")]
        [SerializeField] private float _closingLineSeconds = 5f;

        [Tooltip("대사에 쓸 한글 폰트(조선궁서체 등). 월드 UI가 공용으로 가져다 쓴다")]
        [SerializeField] private Font _font;

        [Tooltip("심문창을 닫을 때마다 실행. 잠시 멈춘 것일 수도 있으니 되돌릴 수 없는 일은 걸지 말 것")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onClosed;

        [Tooltip("'이만 마치겠소'를 눌러 심문을 끝낸 순간 한 번만 실행. 되돌릴 수 없는 일은 여기에 건다 — " +
                 "甲이 일어서서 방을 나가고, 그가 깔고 앉았던 보료가 열리는 것처럼")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onFinished;

        private bool _active;
        private bool _locked;

        /// <summary>인물들이 플레이어를 부르는 이름표. 1막은 과객이라 '나그네'(→ GameState가 정한다).</summary>
        private static string PlayerTitle => GameState.Instance.PlayerTitle;

        // 지금 심문창이 하나라도 열려 있나(다른 UI가 참고: 목표 HUD 숨김 등)
        private static int s_openCount;
        public static bool AnyOpen => s_openCount > 0;

        // 지금 열려 있는 심문(수첩에서 증거를 들이밀 대상)
        public static InterrogationController Active { get; private set; }

        /// <summary>이 사람과 지금 마주 이야기하는 중인가. 돌아보기(FaceThePlayer)가 참고한다.</summary>
        public bool IsOpen => _active;

        private INpcResponder _responder;
        private readonly List<string> _transcript = new List<string>();
        private readonly List<string> _unlockedFacts = new List<string>();
        private readonly HashSet<string> _unlockedGateKeys = new HashSet<string>();
        private readonly HashSet<string> _grantedTopics = new HashSet<string>();

        /// <summary>이미 물어본 추천 질문. 물은 것은 줄에서 내려가고 다음 것이 올라선다.</summary>
        private readonly HashSet<string> _asked = new HashSet<string>();

        /// <summary>지금 줄에 세울 질문(매번 새로 만들지 않으려고 담아 둔다).</summary>
        private readonly List<TopicQuestion> _shown = new List<TopicQuestion>();

        /// <summary>한 번이라도 말을 걸어 본 적이 있나(기억하는 인물의 초기화 판단).</summary>
        private bool _everBegun;

        private string _npcLine = "";
        private bool _busy;
        private bool _exitRequested;

        private string _lastPlayerLine = "";

        /// <summary>
        /// <b>받아 적혔지만 아직 안 던진 말.</b>
        ///
        /// 여태는 마이크가 문장을 끝내는 순간 그대로 인물에게 날아갔다. 잘못 알아들으면
        /// 그것이 그대로 질문이 되고, 무엇으로 전해졌는지는 대답이 온 뒤에야 알았다.
        /// 견우팀 꾸러미는 그러지 않는다 — 받아 적은 글을 <b>칸에 올려 두고</b>,
        /// 사람이 눈으로 확인한 뒤 Enter(또는 「묻 기」)로 던진다.
        /// 저쪽 주석 그대로다: 「잘못 알아들었을 때 고칠 수 있어야 한다.」
        ///
        /// 그래서 우리도 <b>한 박자 둔다</b>. 자막 바의 입력줄이 이 값을 비춘다
        /// (<see cref="Tools.SubtitleView"/>).
        /// </summary>
        public string Draft { get; private set; } = "";

        /// <summary>방금 던진 말. 칸이 비어 있을 때 <b>묽게</b> 남아 무엇을 물었는지 보여 준다.</summary>
        public string LastPlayerLine { get { return _lastPlayerLine; } }

        /// <summary>
        /// 지금 대사가 <b>새로 알아낸 것</b>인가. 그러면 자막이 붉게 나온다.
        ///
        /// 수첩에는 물증만 적힌다(<see cref="Journal.AddClue"/>). 추천 질문으로 캐낸 정황과
        /// 증거를 들이밀어 받아낸 실토는 손에 쥐는 물건이 아니라 <b>그 자리에서 듣는 말</b>이라
        /// 적히지 않는다. 그래서 들을 때 한 번은 티가 나야 한다 — 흰 글씨로 스쳐 가면
        /// 방금 그 한 마디가 이 사건에서 무엇이었는지 알 수가 없다.
        ///
        /// 물음을 새로 던지면 꺼진다. 같은 말을 두 번 물어 두 번 붉을 일은 없다.
        /// </summary>
        private bool _lineIsKey;

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
            // 마커 오브젝트 자신의 렌더러(안 보이게 꺼둔 큐브)를 잡으면 안 된다 —
            // 그러면 콜라이더가 마커에 박혀, 인물이 걸어가 버린 뒤에도 빈 자리를 클릭하게 된다.
            Renderer rend = GetComponentInChildren<SkinnedMeshRenderer>();
            if (rend == null)
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    if (r.transform != transform) { rend = r; break; }
            var host = rend != null ? rend.transform : transform;
            // SkinnedMeshRenderer는 본 아래에 있을 수 있으니, 모델 루트 쪽으로 한 단계 올린다.
            if (host != transform && host.parent != null && host.parent != transform) host = host.parent;

            var col = host.gameObject.AddComponent<CapsuleCollider>();
            // 트리거로 둔다. 클릭 레이는 트리거도 집지만, 바닥을 찾는 레이(발 붙이기·걷기)는
            // 트리거를 무시한다 — 안 그러면 인물 몸통이 '바닥'으로 잡혀 옆 사람이 그 위에 올라선다.
            col.isTrigger = true;
            Vector3 ls = host.lossyScale;

            // <b>사람 크기를 벗어나지 못하게 묶는다.</b>
            //
            // 몸 상자는 스킨드 메시에서 얻는데, 그 상자는 첫 자세에서 한 번 잡히고는
            // 잘 갱신되지 않아 실제보다 몇 배로 부풀어 있는 일이 흔하다. 마름이 그랬다 —
            // 반지름 1.7m 짜리 콜라이더가 몸에서 두 걸음 앞까지 뻗어 나와 <b>대문의 절반을
            // 삼켰다</b>. 문을 두드리려고 오른쪽을 눌러도 짚이는 것은 문이 아니라 마름이라
            // 아무 일도 일어나지 않았다. 사람은 3.6m 로 부풀지 않는다.
            float wide = Mathf.Clamp(Mathf.Max(body.size.x, body.size.z) * 0.5f, 0.15f, 0.45f);
            float tall = Mathf.Clamp(body.size.y, 1.2f, 2.2f);
            Vector3 center = body.center;
            center.y = Mathf.Min(body.center.y, body.min.y + tall * 0.5f);

            col.direction = 1;                                  // Y축으로 선다
            col.center = host.InverseTransformPoint(center);
            col.height = tall / Mathf.Max(0.0001f, Mathf.Abs(ls.y));
            col.radius = wide / Mathf.Max(0.0001f, Mathf.Abs(ls.x));
            DevLog.Note($"[InterrogationController] '{name}' — 몸을 덮는 콜라이더가 없어 '{host.name}' 에 자동으로 붙였습니다.");
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
            DevLog.Note(sb.ToString(), this);
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

            // 상태 초기화(다시 말 걸 때도 깨끗하게).
            //
            // 다만 <b>기억하는 인물은 안 지운다</b>. 동헌에서는 甲을 물렸다가 다시 부르는
            // 일이 예사인데, 부를 때마다 처음으로 돌아가면 이미 캐낸 것을 또 캐야 하고
            // 무엇보다 <b>이미 무너진 사람이 멀쩡한 얼굴로 다시 선다</b>.
            bool fresh = !_rememberBetweenTalks || !_everBegun;
            if (fresh)
            {
                _transcript.Clear();
                _unlockedFacts.Clear();
                _unlockedGateKeys.Clear();
                _grantedTopics.Clear();
                _asked.Clear();
            }
            _lineIsKey = false;
            _busy = false;
            _everBegun = true;

            _responder = MakeResponder();
            _npcLine = (!fresh && !string.IsNullOrEmpty(_character.recallLine))
                     ? _character.recallLine : _character.openingLine;
            _transcript.Add($"{_character.characterName}: {_npcLine}");
            _active = true;
            s_openCount++;
            Active = this;
            // 지난 사람에게 하려던 말이 다음 사람 칸에 남아 있으면 안 된다.
            Draft = "";

            RefreshSubtitle();
            InterrogationPanel.Open(this);

            // 대사창은 눈앞 1.3m 에 못 박혀 있고 월드 캔버스는 깊이 검사를 받는다.
            // 인물에게 1m 안쪽으로 다가서면 상대 몸이 글씨를 덮어 버린다.
            // "뒤로 물러서세요" 라고 가르치는 대신, 창이 알아서 상대 앞으로 당겨 온다.
            SubtitleView.KeepInFrontOf(transform);
            InterrogationPanel.KeepInFrontOf(transform);
        }

        private void OnDisable()
        {
            if (_active) { _active = false; s_openCount = Mathf.Max(0, s_openCount - 1); }
            if (Active == this) Active = null;
            Draft = "";
            SubtitleView.KeepInFrontOf(null);
            SubtitleView.Hide();
            InterrogationPanel.Close();
        }

        // ── 클릭으로 인물을 고르면 심문 시작 ──
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

        /// <summary>
        /// 밖에서 닫아 달라 이를 때. Esc 말고 다른 길이 필요해서,
        /// 컨트롤러 단추가 이리로 들어온다.
        /// </summary>
        public void CloseFromOutside() { if (_active) ClosePanel(); }

        private void ClosePanel()
        {
            if (_beginOnStart) { _exitRequested = true; return; }      // 단독 무대 → 조사청 복귀
            if (_active) { _active = false; s_openCount = Mathf.Max(0, s_openCount - 1); } // 큐브 → 패널만 닫기
            if (Active == this) Active = null;
            SubtitleView.KeepInFrontOf(null);
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

            _onClosed?.Invoke();
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

        // ── 행동 ①: 손으로 쳐서 묻기 ──

        /// <summary>플레이어의 물음을 인물에게 던진다. 글쇠 칸에 친 말이 여기로 들어온다.</summary>
        public void Say(string text)
        {
            if (!_active || _busy || string.IsNullOrWhiteSpace(text)) return;
            string say = text.Trim();
            _lastPlayerLine = say;
            _lineIsKey = false;
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

        // 말하는 도중의 중간 전사 — 확정 전이라 대화 기록엔 넣지 않고 입력줄에만 비친다.
        /// <summary>손으로 친 말을 칸에서 받아 온다 — 자막 바가 던지기 직전에 부른다.</summary>
        public void SetDraft(string text) { Draft = text == null ? "" : text; }

        /// <summary>칸에 올라 있는 말을 던진다 — Enter 와 「묻 기」 단추가 부른다.</summary>
        public void AskDraft()
        {
            if (string.IsNullOrWhiteSpace(Draft)) return;
            string say = Draft;
            Draft = "";
            Say(say);
        }

        // ── 행동 ②: 추천 질문 고르기 ──
        private void AskTopic(TopicQuestion t)
        {
            if (_busy || t == null) return;
            _lastPlayerLine = t.question;
            _transcript.Add($"{PlayerTitle}: {t.question}");

            // 물은 것은 줄에서 내려간다 — 같은 말을 두 번 묻게 두면 셋이라는 칸이
            // 있으나 마나다. 내려간 자리에는 아직 안 물은 것이 올라선다.
            _asked.Add(t.question);
            InterrogationPanel.Refresh();

            // 이 물음으로 무언가 캐냈나(한 번만). 수첩에 적지는 않는다 — 들은 말이다.
            // 대신 대사를 붉게 내보내고, 인물이 이후에도 그 사실을 아는 채로 말하도록
            // 밝혀진 것에 얹어 둔다.
            _lineIsKey = false;
            if (!string.IsNullOrEmpty(t.grantsClueKey) && !_grantedTopics.Contains(t.grantsClueKey))
            {
                _grantedTopics.Add(t.grantsClueKey);
                if (!string.IsNullOrEmpty(t.grantsClueText)) _unlockedFacts.Add(t.grantsClueText);
                _lineIsKey = true;
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
            _lineIsKey = false;
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
                    // 실토는 수첩에 적지 않는다. 들이민 물증은 이미 수첩에 있고,
                    // 그 물증이 무엇을 열었는지는 지금 눈앞에서 붉게 지나간다.
                }
                break;
            }

            _lineIsKey = revealed != null;

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
            // 아랫줄은 <b>늘 조작 안내</b>다 — 꾸러미 바와 같은 자리에 같은 말이 온다.
            // 여태는 여기에 「판관 — 내가 한 말」이 왔는데, 그 말은 이제 입력줄 칸으로
            // 갔다(저쪽이 글쇠 칸에 두는 자리다). 한 줄에 둘을 번갈아 넣으면
            // <b>조작을 알려 주는 줄이 말할 때마다 사라진다</b>.
            string hint = Controls.InterrogationHint;
            // 붉은 글씨는 말이 다 나온 뒤에만. 기다리는 동안의 "…" 까지 붉으면
            // 무엇이 붉은 것인지 흐려진다.
            SubtitleView.Show(_character.characterName, line, hint, _lineIsKey && !_busy);
        }

        private float _awayFor;

        /// <summary>
        /// 말을 걸어놓고 걸어가 버렸는가. 그러면 대화를 끝낸다 —
        /// 등을 돌리고 멀어졌는데 창이 계속 떠 있으면 말이 안 된다.
        /// </summary>
        private void CheckWalkedAway()
        {
            if (!_active || _beginOnStart || _walkAwayDistance <= 0f) return;
            var cam = Camera.main;
            if (cam == null) return;

            if (ModelBounds.DistanceTo(transform, cam.transform.position) <= _walkAwayDistance)
            {
                _awayFor = 0f;
                return;
            }
            _awayFor += Time.deltaTime;
            if (_awayFor >= _walkAwayGrace) { _awayFor = 0f; ClosePanel(); }
        }

        private void Update()
        {
            CheckWalkedAway();
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            // ESC로 심문창 닫기(인물 큐브 방식일 때). I는 수첩(JournalView)이 처리
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
        //  OnGUI 는 세상 속 판과 켜가 어긋났고,
        //  데스크탑에선 월드 UI와 겹쳐 보여 오히려 가렸다.
        // ─────────────────────────────────────────────

        /// <summary>
        /// 월드 패널이 읽는 추천 질문 목록. 끄면(_showTopics=false) 말로만 진행한다.
        ///
        /// <b>한 번에 셋까지만 내놓는다</b>(<see cref="_topicsAtOnce"/>). 넷을 한 줄에
        /// 늘어놓으면 고르는 것이 아니라 훑는 것이 되고, 훑는 순간 물음이 아니라 목록이 된다.
        /// 하나를 물으면 그 질문은 줄에서 <b>내려가고</b> 그 자리에 다음 것이 올라선다 —
        /// 그래서 다 물을 수는 있되 한눈에 보이는 것은 늘 셋이다.
        /// </summary>
        public IReadOnlyList<TopicQuestion> Topics
        {
            get
            {
                if (!_showTopics || _character == null) return null;
                _shown.Clear();
                int cap = Mathf.Max(1, _topicsAtOnce);
                foreach (var t in _character.topics)
                {
                    if (t == null || string.IsNullOrEmpty(t.question)) continue;
                    if (_asked.Contains(t.question)) continue;
                    _shown.Add(t);
                    if (_shown.Count >= cap) break;
                }
                return _shown;
            }
        }

        /// <summary>이 인물에게 내릴 수 있는 명령들(소매 걷기 따위). 근거가 없으면 비어 있다.</summary>
        public IEnumerable<InterrogationOrder> Orders => GetComponents<InterrogationOrder>();

        /// <summary>지금 심문하는 인물의 데이터(이름·성격). 부르기 판이 이름표에 쓴다.</summary>
        public InterrogationCharacter Character => _character;

        /// <summary>
        /// <b>부른다.</b> 뜰에 세워 둔 사람을 마루에서 불러 세우는 길 — 거리도 잠금도 안 본다.
        ///
        /// 클릭(<see cref="OnSelect"/>)과 달리 다가설 필요가 없다. 어사가 마루에서 이름을
        /// 부르는데 몇 걸음 안에 있어야 한다면 그건 부르는 것이 아니다.
        /// </summary>
        public void CallUp()
        {
            _locked = false;
            Begin();
        }

        /// <summary>월드 패널의 추천 질문 버튼이 호출.</summary>
        public void AskTopicFromUi(TopicQuestion t) => AskTopic(t);

        /// <summary>월드 패널의 명령 버튼이 호출.</summary>
        public void RunOrderFromUi(InterrogationOrder o)
        {
            if (!_active || o == null || !o.Available) return;
            o.Run(this);
            InterrogationPanel.Refresh();   // 걷었으면 그 단추는 사라져야 한다
        }

        /// <summary>월드 패널의 '잠시 멈추다' 버튼이 호출. 다시 말을 걸면 이어진다.</summary>
        public void CloseFromUi() => ClosePanel();

        /// <summary>
        /// 월드 패널의 '이만 마치겠소' 버튼이 호출 — 이 인물과의 볼일을 끝낸다.
        ///
        /// 잠시 창을 치우는 것과 자리를 파하는 것은 다른 일이다. 한 버튼으로 묶어 두면
        /// 손이 미끄러진 한 번에 심문이 영영 끝나 버린다. 끝낸 뒤에는 다시 잠가 둔다 —
        /// 물러가는 사람을 붙잡고 처음부터 다시 물을 수는 없다.
        /// </summary>
        public void FinishFromUi()
        {
            ClosePanel();

            // 되돌릴 수 없는 일은 한 번만. 이미 마친 사람이면 창만 닫는다.
            //
            // 잠그는 것도 여기 안에 있어야 한다. 밖에 두었더니 두 번째로 마칠 때 잠기기만
            // 하고 풀어 줄 신호(물러가기)는 오지 않아, 그 뒤로 영영 말을 걸 수 없었다.
            if (_finished) return;
            _finished = true;

            // 물러가는 사람을 붙잡고 다시 물을 수는 없으니 그동안만 잠근다.
            // 다 물러간 뒤에 도로 열린다 — 마을 사람에게 몇 번이고 다시 말을 걸 수 있듯,
            // 이 사람도 마당에서 다시 만난다.
            _locked = true;
            _onFinished?.Invoke();
        }

        /// <summary>이미 볼일이 끝났나. 끝난 뒤에는 다시 말을 걸 수 없다.</summary>
        public bool Finished => _finished;
        private bool _finished;

    }
}
