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
        // <b>추천 질문을 걷었다.</b> 마주 선 사람의 발치에 물음 셋이 늘 떠 있어,
        // 눈을 맞추는 자리가 <b>고르는 자리</b>가 되었다. 게다가 그 판만 아직 월드에
        // 서 있어서 몸에 묻히거나 발밑에 깔렸다.
        //
        // 물음은 이제 <b>쳐서</b> 한다. 없어지는 것은 「목록에서 고르기」뿐이고,
        // 저 물음들이 여는 것은 그대로 열린다 — 낱말만 스치면 열리게 되어 있다(Loosen).
        [SerializeField] private bool _showTopics = false;

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
        /// <summary>
        /// 지금 묻는 이가 누구로 보이는가. GameState 는 씬을 닫는 동안 null 을 돌려주므로
        /// (그때 새로 세우면 치울 사람이 없다) 여기서 받아 준다 — 늦게 도착한 대답이
        /// 이 값을 물어보는 일이 있다.
        /// </summary>
        private static string PlayerTitle
        {
            get
            {
                var gs = GameState.Instance;
                return gs != null ? gs.PlayerTitle : "나그네";
            }
        }

        /// <summary>AI 에게 주는 한 줄 신분 설명. 위와 같은 까닭으로 받아 준다.</summary>
        private static string PlayerBrief
        {
            get
            {
                var gs = GameState.Instance;
                return gs != null ? gs.PlayerIdentityBrief : "";
            }
        }

        // 지금 심문창이 하나라도 열려 있나(다른 UI가 참고: 목표 HUD 숨김 등)
        private static int s_openCount;
        public static bool AnyOpen => s_openCount > 0;

        // 지금 열려 있는 심문(수첩에서 증거를 들이밀 대상)
        public static InterrogationController Active { get; private set; }

        /// <summary>
        /// 지금 마주 앉은 사람의 이름. 없으면 빈 글.
        ///
        /// <b>제 말을 제게 내밀지 못하게</b> 하려고 수첩이 물어본다 — 마을 사람에게서
        /// 들은 말을 그 마을 사람 앞에 도로 꺼내 보이는 것은 증거를 대는 것이 아니다.
        /// </summary>
        /// <summary>
        /// 지금 마주한 사람이 <b>이 단서를 기다리는 자리</b>가 있는가(이 막의 게이트). 있으면 수첩은
        /// 말한 이·들이밀 수 있음을 따지지 않고 내밀게 둔다 — 甲이 1막에 흘린 자랑(J02)을 2막에서
        /// 甲 앞에 되돌리는 것이 그 게이트의 뜻인데, 「제 말을 제게 내민다」로 막혔다.
        /// </summary>
        public static bool ActiveWants(string key)
        {
            var a = Active;
            if (a == null || a._character == null || a._character.evidenceGates == null || string.IsNullOrEmpty(key)) return false;
            foreach (var g in a._character.evidenceGates)
                if (g != null && ActGate.Fits(g.act) && (g.clueKey == key || g.RequiredKeys().Contains(key))) return true;
            return false;
        }

        /// <summary>
        /// <b>자백을 가른다.</b> 무너짐의 열쇠(breakOnClues)를 다 쥔 채, 자백 낱말로 찌르거나 열쇠 가운데
        /// 하나를 들이밀면 — 수첩에 자백을 적고 단계를 끝까지 올린다. AI 에게는 「무너져 실토한다」로 건넨다.
        /// </summary>
        private string TryConfess(string say, string presented)
        {
            if (_character == null || string.IsNullOrEmpty(_character.confessClueKey)) return null;
            var journal = Journal.Instance;
            if (journal == null || journal.HasClue(_character.caseId, _character.confessClueKey)) return null;
            var keys = _character.BreakKeys();
            if (keys.Length == 0) return null;
            foreach (var k in keys) if (!journal.HasClue(_character.caseId, k)) return null;

            bool hit = presented != null
                     ? System.Array.IndexOf(keys, presented) >= 0
                     : !string.IsNullOrWhiteSpace(_character.confessKeywords) && TalkKeywords.Hit(say, _character.confessKeywords);
            if (!hit) return null;

            string text = string.IsNullOrEmpty(_character.confessClueText) ? _character.confessLine : _character.confessClueText;
            _stageFloor = 10;
            if (!_unlockedFacts.Contains(text)) _unlockedFacts.Add(text);
            Record(_character.confessClueKey, text, true, null, _character.confessClueTitle);
            _loosenedAnswer = _character.confessLine;
            return "무너져 실토한다: " + text;
        }

        public static string ActiveName
        {
            get { return Active != null && Active._character != null ? Active._character.ShownName : ""; }
        }

        /// <summary>대답을 기다리는 중인가(이 사이에 벌어진 일은 건네지지 않는다).</summary>
        public bool Busy => _busy;

        /// <summary>이 사람과 지금 마주 이야기하는 중인가. 돌아보기(FaceThePlayer)가 참고한다.</summary>
        public bool IsOpen => _active;

        private INpcResponder _responder;
        private readonly List<string> _transcript = new List<string>();
        private readonly List<string> _unlockedFacts = new List<string>();
        private readonly HashSet<string> _unlockedGateKeys = new HashSet<string>();

        /// <summary>
        /// <b>정곡을 찔린 자리가 끌어올려 둔 단계.</b>
        ///
        /// 단계를 열린 게이트 <b>개수</b>로만 세면, 게이트가 늘어날수록 한 자리의 무게가
        /// 줄어든다 — 스무 해 못 먹던 것을 사들인 것이 들통난 사람이 「다섯 중 하나」라는
        /// 이유로 태연할 수는 없다. 그런 한 방은 제 무게를 스스로 적어 둔다.
        /// </summary>
        private int _stageFloor;
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
        /// 캐낸 정황과 받아낸 실토는 이제 수첩에도 <b>정황</b>으로 남지만(<see cref="Record"/>),
        /// 그것과 별개로 <b>들을 때 한 번은 티가 나야 한다</b> — 흰 글씨로 스쳐 가면 방금 그
        /// 한 마디가 이 사건에서 무엇이었는지 그 자리에서 알 수가 없고, 수첩을 펴 보고서야
        /// 안다면 그건 알아챈 것이 아니라 확인한 것이다.
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
                _stageFloor = 0;
                _grantedTopics.Clear();
                _asked.Clear();
            }
            _lineIsKey = false;
            _busy = false;
            _everBegun = true;

            _responder = MakeResponder();
            // 첫 대사도 <b>막을 가려</b> 고른다. 성격만 갈라 놓고 첫 대사를 한 벌로 두면,
            // 어사 앞에 끌려 나온 사람이 1막의 인사말로 맞이한다.
            _npcLine = (!fresh && !string.IsNullOrEmpty(_character.recallLine))
                     ? _character.recallLine
                     : _character.OpeningFor(ActGate.Revealed);
            _transcript.Add($"{_character.ShownName}: {_npcLine}");
            _active = true;
            s_openCount++;
            Active = this;
            // 지난 사람에게 하려던 말이 다음 사람 칸에 남아 있으면 안 된다.
            Draft = "";

            // <b>심문 중에는 「✕」를 내린다.</b> 그 딱지는 자막판의 것이라, 눌러도
            // 자막만 사라지고 <b>묻는 창은 그대로 남는다</b> - 닫으려고 누른 사람 눈에는
            // 아무 반응이 없는 단추다. 물러나는 길은 창 안의 「잠시 멈추다」·「이만
            // 마치겠소」 두 줄로 충분하고, 그 둘은 자리를 파하는 일까지 제대로 한다.
            SubtitleView.SetClosable(false);

            RefreshSubtitle();
            InterrogationPanel.Open(this);

            // 대사창은 눈앞 1.3m 에 못 박혀 있고 월드 캔버스는 깊이 검사를 받는다.
            // 인물에게 1m 안쪽으로 다가서면 상대 몸이 글씨를 덮어 버린다.
            // "뒤로 물러서세요" 라고 가르치는 대신, 창이 알아서 상대 앞으로 당겨 온다.
            SubtitleView.KeepInFrontOf(transform);
            InterrogationPanel.KeepInFrontOf(transform);

            // 어디에 서서 눌렀든 <b>같은 그림</b>이 되게 잡고, 상대는 이쪽을 마주 본다.
            // 단독 무대(동헌)는 제 연출이 따로 있으므로 건드리지 않는다.
            if (!_beginOnStart) TalkFraming.Enter(transform);
        }

        private void OnDisable()
        {
            if (_active) { _active = false; s_openCount = Mathf.Max(0, s_openCount - 1); }
            if (Active == this) Active = null;
            Draft = "";
            TalkFraming.Leave();
            SubtitleView.SetClosable(true);   // 씬이 바뀌어도 닫는 길은 남겨 둔다
            SubtitleView.KeepInFrontOf(null);
            SubtitleView.Hide();
            InterrogationPanel.Close();
        }

        // ── 클릭으로 인물을 고르면 심문 시작 ──
        public void OnHoverEnter() { }
        public void OnHoverExit() { }
        [Header("부르는 자리에서만")]
        [Tooltip("<b>켜면 이 사람은 직접 다가가 말을 걸 수 없다.</b> 동헌에서 불러야 한다. " +
                 "2막의 뜰에 선 사람들에게 켠다. 1막처럼 찾아가 묻는 자리에서는 끈다")]
        [SerializeField] private bool _onlyWhenCalled = false;

        [TextArea(2, 3)]
        [Tooltip("직접 말을 걸었을 때의 한 마디. 비우면 조용히 물러난다")]
        [SerializeField] private string _callMeUpLine =
            "…소인이 어찌 여기서 입을 열겠습니까. *동헌*으로 부르시면 나아가 아뢰겠습니다.";

        /// <summary>이 사람을 지금 직접 붙잡고 물을 수 있나.</summary>
        private bool DirectTalkAllowed => !_onlyWhenCalled;

        public void OnSelect()
        {
            if (_active || _locked) return;
            var cam = Camera.main;
            // 피벗이 아니라 "몸"까지의 거리로 잰다. 외부 FBX는 피벗이 몸에서 멀리 떨어져 있을 수 있고,
            // 그러면 코앞에 서 있어도 '멀다'로 막혀 말을 걸 수 없다(옹덕구가 그랬다).
            if (cam != null && ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTalkDistance)
                return;

            // <b>뜰에서는 못 묻는다.</b>
            //
            // 어사가 관아에 들었으면 묻는 자리는 동헌이다. 뜰에 세워 둔 사람을 붙잡고
            // 그 자리에서 캐물으면 그것은 심문이 아니라 실랑이고, 무엇보다 <b>부르기 판</b>이
            // 있을 까닭이 없어진다 — 불러 세우는 절차가 곧 이 막의 뜻인데 걸어가서
            // 누르면 같은 창이 열리니 절차가 장식이 된다.
            //
            // 다만 <b>아무 말 없이 안 열리면 고장으로 읽힌다</b>. 그래서 사람이 제 입으로
            // 물린다 — 안 되는 까닭과 되는 길이 한 마디에 같이 있어야 한다.
            if (!DirectTalkAllowed)
            {
                // <b>물리는 말은 저마다 다르다.</b> 인물에 적어 둔 것이 있으면 그것을 쓰고,
                // 없을 때만 부품의 두루뭉술한 것으로 내려간다 — 다섯이 같은 문장으로
                // 물리면 처지도 신분도 다 지워진다.
                string line = _character != null && !string.IsNullOrWhiteSpace(_character.calledUpLine)
                    ? _character.calledUpLine
                    : _callMeUpLine;
                if (!string.IsNullOrEmpty(line))
                    SubtitleView.Show(_character != null ? _character.ShownName : "",
                                      line, "", true);
                return;
            }

            Begin();
        }

        /// <summary>
        /// 밖에서 닫아 달라 이를 때. Esc 말고 다른 길이 필요해서,
        /// 컨트롤러 단추가 이리로 들어온다.
        /// </summary>
        public void CloseFromOutside() { if (_active) ClosePanel(); }

        private void ClosePanel()
        {
            SubtitleView.SetClosable(true);                            // 물러났으면 도로 닫을 수 있어야 한다
            TalkFraming.Leave();                                       // 좁혔던 화각을 되돌린다
            if (_beginOnStart) { _exitRequested = true; return; }      // 단독 무대 → 조사청 복귀
            if (_active) { _active = false; s_openCount = Mathf.Max(0, s_openCount - 1); } // 큐브 → 패널만 닫기
            if (Active == this) Active = null;
            SubtitleView.KeepInFrontOf(null);
            InterrogationPanel.Close();

            // 돌아설 때 등 뒤로 던지는 한 마디.
            // 조작창은 닫고 자막만 잠깐 더 남긴다 — 무엇을 물었든 이건 반드시 듣게 된다.
            string closing = _character != null ? _character.ClosingFor(ActGate.Revealed) : "";
            if (!string.IsNullOrEmpty(closing))
            {
                SubtitleView.Show(_character.ShownName, closing, "");
                // <b>내보낸 다음에 적는다.</b> 자막에 뜨지도 않은 말을 「했다」고 적으면
                // 한 번도 못 들은 귀띔이 그대로 사라진다.
                _character.NoteClosingSaid();
                CancelInvoke(nameof(HideClosingLine));
                Invoke(nameof(HideClosingLine), _closingLineSeconds);
            }
            else SubtitleView.Hide();

            // <b>말이 끝났으면 물러간다.</b>
            //
            // 여태 나가는 길이 없었다 — 「이만 마치겠소」를 눌러도 불려 온 이는 교의 앞에
            // 그대로 앉아 있었고, 다른 이를 부를 때에야 비로소 일어섰다. 그래서 동헌에는
            // 파한 사람이 하나씩 쌓였다. 부르는 것이 걸어 들어오는 일이면 파하는 것도
            // 걸어 나가는 일이어야 한다 — 나가면서 문도 제 손으로 닫는다(<see cref="CourtSummon"/>).
            var walk = GetComponent<CourtSummon>();
            if (walk != null && walk.IsUp) walk.StepBack();

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
            _transcript.Add($"{PlayerTitle}: {say}");

            // 단추를 안 눌렀다고 빈손으로 돌려보내지 않는다 — 같은 곳을 짚었으면 같은 것을 준다.
            _loosenedAnswer = null;
            string loosened = TryConfess(say, null) ?? Loosen(say);
            _lineIsKey = loosened != null;

            var req = new NpcRequest
            {
                character = _character,
                transcript = _transcript,
                unlockedFacts = _unlockedFacts,
                playerInput = say,
                isEvidence = false,
                justRevealedInfo = loosened,
                scriptedAnswer = _loosenedAnswer,
                playerTitle = PlayerTitle,
                playerIdentityBrief = PlayerBrief,
            };
            Stamp(req);
            _busy = true;
            RefreshSubtitle();
            _responder.GetResponse(this, req, OnReply, OnError);
        }

        /// <summary>
        /// <b>부탁에 지금 상태를 찍는다.</b>
        ///
        /// 막(1막/2막), 무너짐 단계, 마지막 열쇠를 쥐었는지, 어사가 지금 쥔 단서 — 넷이다.
        /// 이 넷이 없으면 AI 는 「무너져도 되는 때」를 저 혼자 정하게 되고, 그러면
        /// 말재간이 좋은 사람에게는 첫 마디에 사건이 열리고 그렇지 않은 사람에게는
        /// 영영 안 열린다. <b>열쇠는 게임이 쥐고, 정곡은 AI 가 읽는다.</b>
        ///
        /// <b>단계</b>는 이 인물에게 <b>실제로 들이민</b> 증거가 몇 개나 먹혔는지로 센다.
        /// 아는 것이 아니라 <b>내민 것</b>이라야 추궁의 무게가 된다.
        /// </summary>
        private void Stamp(NpcRequest req)
        {
            if (req == null) return;
            var gs = GameState.Instance;
            var journal = Journal.Instance;
            req.identityRevealed = gs != null && gs.IdentityRevealed;

            // <b>분모는 열릴 수 있는 것만</b>. 발뺌 줄과 다른 막의 줄까지 세면, 甲처럼
            // 셋 중 하나만 실토형인 인물은 그 하나를 열어도 단계가 3 에 그친다 —
            // 정곡을 찔린 사람이 「셋 중 하나」라는 이유로 태연해진다.
            int gates = 0;
            if (_character != null && _character.evidenceGates != null)
                foreach (var g in _character.evidenceGates)
                    if (g != null && !g.deflectionOnly && ActGate.Fits(g.act)) gates++;
            req.stage = gates > 0
                      ? Mathf.Clamp(Mathf.RoundToInt(10f * _unlockedGateKeys.Count / gates), 0, 10)
                      : 0;
            if (_stageFloor > req.stage) req.stage = _stageFloor;

            var keys = _character != null ? _character.BreakKeys() : System.Array.Empty<string>();
            bool all = keys.Length > 0 && journal != null;
            foreach (var k in keys)
                if (journal == null || !journal.HasClue(_character.caseId, k)) { all = false; break; }
            req.canBreak = all;
            // 마지막 열쇠를 다 쥐었으면 단계도 끝까지 올린다 — 아직 안 내밀었어도
            // 「무너질 수 있는 자리」에 서 있다는 것은 태도에 배어야 한다.
            if (all) req.stage = Mathf.Max(req.stage, 8);

            // 1막의 과객은 쥔 것을 내보인 적이 없다 — 목록을 주면 사목·장부 이야기가 AI 입에서 샜다.
            if (journal != null && _character != null && req.identityRevealed)
            {
                var held = new System.Collections.Generic.List<string>();
                foreach (var c in journal.GetClues(_character.caseId))
                    if (c != null && !string.IsNullOrEmpty(c.text))
                        held.Add(c.text.TrimStart().StartsWith("[") ? c.text : "[" + c.key + "] " + c.text);   // 성격이 번호로 조건을 건다
                req.evidenceHeld = held;
            }
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
            // <b>친 말이 명령이면 명령으로 한다</b> — 「소매를 걷어라」를 물음으로 넘기면 인물이 말로만 받고
            // 소매는 안 걷혔다. 근거가 없으면 까닭을 이르고 물음으로도 넘기지 않는다.
            foreach (var o in Orders)
            {
                if (o == null || !o.enabled || !o.Matches(say)) continue;
                if (o.Available) { o.Run(this); InterrogationPanel.Refresh(); return; }
                if (o.LockedReason != null) { SubtitleView.Show("", o.LockedReason, "", false); return; }
            }
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

            // 이 물음으로 무언가 캐냈나(한 번만). 대사를 붉게 내보내고, 인물이 이후에도
            // 그 사실을 아는 채로 말하도록 밝혀진 것에 얹고, 수첩에 정황으로 적는다.
            // 적는 몫은 GrantTopic 이 진다.
            _lineIsKey = false;
            if (!string.IsNullOrEmpty(t.grantsClueKey) && !_grantedTopics.Contains(TopicId(t)))
            {
                GrantTopic(t);
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
                playerIdentityBrief = PlayerBrief,
            };
            Stamp(req);
            _busy = true;
            RefreshSubtitle();
            _responder.GetResponse(this, req, OnReply, OnError);
        }

        // ── 행동 ②: 증거 제시 ──
        /// <summary>「[J01]」 처럼 코드가 붙인 이름표를 걷는다 — 사람에게 보일 말이 아니다.</summary>
        private static string CleanClue(string t)
        {
            t = (t ?? "").Trim();
            if (t.StartsWith("["))
            {
                int close = t.IndexOf(']');
                if (close > 0 && close <= 6 && close + 1 < t.Length) t = t.Substring(close + 1).Trim();
            }
            return t.Replace("*", "");
        }

        /// <summary>칸 한 줄에 들어갈 만큼만.</summary>
        private static string Shorten(string t, int room)
        {
            t = (t ?? "").Replace('\n', ' ');
            return t.Length <= room ? t : t.Substring(0, room).TrimEnd() + "…";
        }

        private void Present(ClueEntry clue)
        {
            if (_busy || clue == null) return;
            // <b>내민 것은 「친 말」이 아니다.</b>
            //
            // 여태 글쇠 칸 자리에 「(증거) [J01] 온 마을이 甲을 주인으로 알고…」가 그대로
            // 찍혔다. 두 가지가 어긋난다 — <c>[J01]</c> 은 <b>우리끼리 쓰는 이름표</b>지
            // 어사가 입에 담는 말이 아니고, 물건을 내미는 짓이 <b>타자로 친 물음</b>처럼
            // 보이면 두 행동이 한 자리에서 뭉갠다.
            //
            // 그러니 이름표를 걷고, 무엇을 어떻게 했는지로 적는다 —
            // 종이는 <b>내밀고</b> 들은 말은 <b>짚는다</b>.
            string plain = CleanClue(clue.text);
            bool heard = !string.IsNullOrEmpty(clue.speaker);
            _lastPlayerLine = heard
                ? "▸ " + clue.speaker + "에게서 들은 말을 짚는다 — " + Shorten(plain, 42)
                : "▸ " + Shorten(plain, 42) + " 을(를) 내민다";
            _lineIsKey = false;
            _transcript.Add($"{PlayerTitle}(증거): {plain}");

            // 제시한 증거의 상황 그림을 잠깐 "탁" 띄운다
            InterrogationPanel.ShowEvidence(Journal.Instance.GetClueImage(_character.caseId, clue.key), 4.5f);

            // 이 증거가 여는 사실이 있으면 잠금 해제(발뺌 전용이면 말만 하고 기록 안 함)
            string revealed = TryConfess(null, clue.key);
            if (revealed == null)
            foreach (var gate in _character.evidenceGates)
            {
                // 발뺌은 겹칠 것이 없다 — 그 종이 하나로 되받는 말이다.
                if (gate.deflectionOnly)
                {
                    if (gate.clueKey != clue.key || !ActGate.Fits(gate.act)) continue;
                    revealed = gate.revealsInfo;   // 반복 가능, 단서 기록 X
                    break;
                }
                if (!GateOpens(gate, clue.key)) continue;
                if (OpenGate(gate)) revealed = gate.revealsInfo;
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
                playerIdentityBrief = PlayerBrief,
            };
            Stamp(req);
            _busy = true;
            RefreshSubtitle();
            // 방금 열린 실토가 다른 명령의 근거가 될 수 있다.
            InterrogationPanel.Refresh();
            _responder.GetResponse(this, req, OnReply, OnError);
        }

        /// <summary>
        /// <b>이 게이트가 지금 열리는가.</b>
        ///
        /// 여태 조건은 「이 단서를 들이밀었나」 하나였다. 그런데 혼자서는 아무 말도 못 하는
        /// 단서가 있다 — 아내의 한 마디도, 물목기의 한 줄도 저 혼자서는 장바구니 얘기다.
        /// <b>둘을 겹쳐야</b> 사들여 놓고 못 먹는 척한다는 말이 선다.
        ///
        /// 그래서 요구하는 단서를 <b>묶음</b>으로 본다. 묶음이 다 손에 있어야 하고,
        /// 지금 내민 것은 그 묶음 안에 있으면 된다 — <b>어느 쪽을 먼저 꺼내도</b> 같은 자리가 열린다.
        /// 무엇을 먼저 주웠느냐로 길이 갈리면 그건 순서를 외우는 일이지 조사가 아니다.
        ///
        /// <paramref name="presented"/> 가 비면 들이민 것 없이 <b>말로</b> 찌른 경우다.
        /// 그때는 묶음을 다 쥐고 있기만 하면 된다.
        /// </summary>
        private bool GateOpens(EvidenceGate gate, string presented)
        {
            if (gate == null || !ActGate.Fits(gate.act)) return false;
            var need = gate.RequiredKeys();
            if (need.Count == 0) return false;
            if (!string.IsNullOrEmpty(presented) && !need.Contains(presented)) return false;

            var journal = Journal.Instance;
            if (journal == null) return false;
            foreach (var k in need)
                if (!journal.HasClue(_character.caseId, k)) return false;
            return true;
        }

        /// <summary>게이트 하나를 연다. 이미 열렸으면 거짓.</summary>
        /// <summary>
        /// 열린 게이트를 세는 이름표. <b>막까지 넣는다</b> — 같은 단서에 1막 줄과 2막 줄을
        /// 나란히 적어 두라고 해 놓고 key 로만 세면, 1막에서 연 순간 2막 줄이 잠긴다.
        /// </summary>
        private static string GateId(EvidenceGate g) => g.clueKey + "#" + (int)g.act;

        private bool OpenGate(EvidenceGate gate)
        {
            if (_unlockedGateKeys.Contains(GateId(gate))) return false;
            _unlockedGateKeys.Add(GateId(gate));
            _unlockedFacts.Add(gate.revealsInfo);
            if (gate.stageAtLeast > _stageFloor) _stageFloor = gate.stageAtLeast;
            // 받아낸 말도 <b>남에게는</b> 들이밀 수 있다(2026-09-15) — 아내의 증언(G05)을 甲 앞에 짚는 것이
            // 2막의 한 수인데, false 로 적어 두니 수첩이 막고 장계 증좌 칸에도 안 떴다.
            // 말한 이 앞에 도로 내미는 것은 수첩(JournalPanel.CanPresent)이 따로 막는다.
            Record(gate.grantsClueKey, gate.revealsInfo, true, null, gate.grantsClueTitle);
            return true;
        }

        // ── 말로 여는 길 ──

        /// <summary>
        /// <b>친 말이 무언가를 열었는가.</b> 열었으면 그 사실을, 아니면 null.
        ///
        /// 여태 손으로 친 말은 아무리 정확해도 분위기였다. 「저 사내가 누구요」를 제 손으로
        /// 친 사람과 단추를 누른 사람이 <b>같은 답을 듣고도</b> 한쪽만 빈손으로 돌아섰다.
        /// 물어본 값을 주지 않으면 손으로 칠 까닭이 없어지고, 그러면 이 게임은 단추 셋을
        /// 차례로 누르는 일이 된다.
        ///
        /// 두 갈래로 연다.
        ///  · <b>추천 질문의 낱말</b> — 같은 곳을 짚었으면 같은 것을 준다. 그 질문은
        ///    물은 것으로 쳐 줄에서 내려간다(안 그러면 같은 말을 두 번 묻게 된다).
        ///  · <b>증거 게이트의 낱말</b> — 다만 그 물증을 <b>수첩에 쥐고 있어야</b> 열린다.
        ///    쥐지 않은 채 정곡을 찌르는 것은 넘겨짚기지 조사가 아니다.
        ///
        /// 한 마디에 하나만 연다. 셋이 우수수 열리면 붉은 줄이 무엇을 가리키는지 알 수 없다.
        /// 발뺌 게이트는 여기서 건너뛴다 — 발뺌은 종이를 눈앞에 들이밀었을 때의 그림이다.
        /// </summary>
        /// <summary>말로 연 자리가 딸려 보낸 사람 말(목업이 쓴다). 한 차례만 산다.</summary>
        private string _loosenedAnswer;

        private string Loosen(string say)
        {
            if (_character == null || string.IsNullOrWhiteSpace(say)) return null;

            if (_character.topics != null)
            {
                foreach (var t in _character.topics)
                {
                    if (t == null || string.IsNullOrEmpty(t.grantsClueKey)) continue;
                    if (!ActGate.Fits(t.act)) continue;
                    if (_grantedTopics.Contains(TopicId(t))) continue;
                    if (!TalkKeywords.Hit(say, t.keywords)) continue;

                    GrantTopic(t);
                    _asked.Add(t.question);
                    InterrogationPanel.Refresh();
                    // 단추로 눌렀을 때 나오던 사람 말을 여기서도 챙긴다. 이것이 없으면
                    // 열쇠가 없는 PC(목업)에서 같은 자리가 단추로는 사람 말을,
                    // 손으로 치면 <b>수첩 문구</b>를 내놓는다 — 「같은 곳을 짚었으면 같은
                    // 것을 준다」가 거기서만 깨진다.
                    _loosenedAnswer = t.mockAnswer;
                    // AI 에게는 <b>사람이 할 말</b>을 흘리게 한다(2026-09-15). 여태 수첩 문구를 넘겨
                    // 「필적이 둘로 갈린다 — 누가 대신 쓴 것이다」 같은 <b>어사의 결론</b>을 인물이 제 입으로 말했다.
                    return !string.IsNullOrEmpty(t.mockAnswer) ? t.mockAnswer : t.question;
                }
            }

            var journal = Journal.Instance;
            if (_character.evidenceGates != null && journal != null)
            {
                foreach (var g in _character.evidenceGates)
                {
                    if (g == null || g.deflectionOnly) continue;
                    if (string.IsNullOrEmpty(g.revealsInfo)) continue;
                    if (_unlockedGateKeys.Contains(GateId(g))) continue;
                    if (!TalkKeywords.Hit(say, g.keywords)) continue;
                    if (!GateOpens(g, null)) continue;      // 묶음을 다 쥐었는가

                    OpenGate(g);
                    return g.revealsInfo;
                }
            }
            return null;
        }

        private static string TopicId(TopicQuestion t) { return t.grantsClueKey + "|" + t.keywords + "|" + t.question; }

        /// <summary>추천 질문으로 캐낸 것을 쥐여 준다 — 밝혀진 사실에 얹고, 수첩에 적는다.</summary>
        private void GrantTopic(TopicQuestion t)
        {
            _grantedTopics.Add(TopicId(t));
            if (!string.IsNullOrEmpty(t.grantsClueText)) _unlockedFacts.Add(t.grantsClueText);
            var j = Journal.Instance;
            string had = null;
            if (j != null) foreach (var c in j.GetClues(_character.caseId)) if (c != null && c.key == t.grantsClueKey) { had = c.text; break; }
            // 같은 열쇠를 나눠 가진 둘째 물음(마을 사람: 집주인 / 담 밖 사내)은 적힌 줄에 <b>덧붙인다</b>.
            // 여태 열쇠로만 「물었다」를 세어, 한쪽을 물으면 다른 쪽은 영영 대답이 안 나왔다.
            if (had != null && !string.IsNullOrEmpty(t.grantsClueText) && !had.Contains(t.grantsClueText))
                j.UpgradeClue(_character.caseId, t.grantsClueKey, had + System.Environment.NewLine + System.Environment.NewLine + t.grantsClueText);
            else
                Record(t.grantsClueKey, t.grantsClueText, true, t.grantsClueImage, t.grantsClueTitle);
        }

        /// <summary>
        /// <b>들은 말을 수첩에 적는다</b> — 정황으로.
        ///
        /// 여태 적지 않았다. 수첩은 주운 물건을 담는 그릇이라 보았기 때문인데, 그 규칙
        /// 아래에서는 출도에 반드시 필요한 J02(「문서는 다 내 손을 거쳤소」)가 <b>甲의
        /// 입에서만</b> 나오면서 수첩에는 영영 안 들어와, 필수 넷 가운데 하나를 채울 길이
        /// 없었다. 아내의 G05 도 마름의 G06 도 마찬가지 처지였다.
        ///
        /// 붉게 지나가는 것은 그대로 두고, 지나간 뒤에 남게만 했다.
        /// <paramref name="presentable"/> 가 false 면 남되 들이밀 수는 없다 —
        /// 받아낸 실토를 그 사람에게 도로 내미는 일은 없다.
        /// </summary>
        private void Record(string key, string text, bool presentable, Texture2D image = null,
                            string title = null)
        {
            if (string.IsNullOrEmpty(key) || _character == null) return;
            var journal = Journal.Instance;
            if (journal == null) return;

            // <b>증언에는 그린 그림이 없다.</b> 물증은 종이라 찍어 둔 면이 있지만
            // 들은 말에는 볼 것이 없어서, 수첩에 「言」자만 찍힌 카드가 줄줄이 깔렸다.
            // 그런데 증언에서 볼 것은 <b>누가 했는가</b>이고, 그 얼굴은 지금 눈앞에 있다.
            if (image == null) image = FacePortrait.Snapshot(transform);

            journal.AddClue(_character.caseId, key,
                            string.IsNullOrEmpty(text) ? key : text,
                            image, ClueKind.정황, presentable, title);
            journal.SetSpeaker(_character.caseId, key, _character.characterName);
            // 이미 적혀 있던 줄에는 AddClue 가 아무것도 안 한다 — 제목만 따로 채운다.
            journal.SetTitle(_character.caseId, key, title);
        }

        // ── 행동 ④: 물건을 내밀거나 무엇을 하다 ──

        /// <summary>
        /// <b>묻지 않고 벌어진 일을 건넨다.</b> 상 위의 게장을 앞으로 밀거나,
        /// 드러난 팔에 놋쇠 자를 대거나.
        ///
        /// 증거 제시와 갈라 둔 까닭: 들이미는 것은 <b>수첩 속의 종이</b>라 「(증거) …」로
        /// 기록에 남는데, 게장은 종이가 아니다. 한 틀에 넣으면 「(증거) 게장」을
        /// 들이미는 꼴이 되고 상대는 그 종이를 읽으려 든다.
        ///
        /// 대답은 정해 두지 않는다. 무슨 일이 벌어졌는지만 건네고, 어떻게 반응하는지는
        /// 그 인물의 성격이 정한다 — <see cref="ItemOrder"/> 가 부른다.
        /// </summary>
        public void Offer(string deed, string revealsInfo, string grantsClueKey, string grantsClueText,
                          int stageAtLeast = 0)
        {
            if (!_active || _busy) return;
            if (string.IsNullOrWhiteSpace(deed)) deed = "(무언가를 내민다)";
            // 물건이 하는 말은 사람의 말보다 무겁다 — 제 무게를 스스로 적어 온다.
            if (stageAtLeast > _stageFloor) _stageFloor = stageAtLeast;

            _lastPlayerLine = deed;
            _transcript.Add($"{PlayerTitle}(행동): {deed}");

            string revealed = null;
            if (!string.IsNullOrWhiteSpace(revealsInfo))
            {
                revealed = revealsInfo;
                if (!_unlockedFacts.Contains(revealsInfo)) _unlockedFacts.Add(revealsInfo);
                // 본 것은 남는다 — 다만 실토와 같아서 다시 들이밀 수는 없다.
                Record(grantsClueKey,
                       string.IsNullOrWhiteSpace(grantsClueText) ? revealsInfo : grantsClueText,
                       true);      // 남 앞에는 짚을 수 있다(말한 이 앞은 수첩이 막는다)
            }
            _lineIsKey = revealed != null;

            var req = new NpcRequest
            {
                character = _character,
                transcript = _transcript,
                unlockedFacts = _unlockedFacts,
                playerInput = deed,
                isEvidence = false,
                isDeed = true,
                justRevealedInfo = revealed,
                playerTitle = PlayerTitle,
                playerIdentityBrief = PlayerBrief,
            };
            Stamp(req);
            _busy = true;
            RefreshSubtitle();
            // 방금 적힌 것이 명령의 근거가 될 수 있다 — 근거가 섰는데 단추가 안 뜨면
            // 그 자리는 없는 것과 같다.
            InterrogationPanel.Refresh();
            _responder.GetResponse(this, req, OnReply, OnError);
        }

        private void OnReply(string text)
        {
            _busy = false;
            // <b>돌아섰는데 뒤늦게 오는 대답이 있다.</b> 창을 닫아도 이 부품은 꺼지지
            // 않으므로 오가던 부탁은 계속 살아 돌아온다. 그때 자막을 다시 그리면
            // 마지막 한 마디나 다른 안내를 지운다 — 받아만 두고 그리지는 않는다.
            if (_character == null) return;
            // <b>남이 지은 글은 씻어서 받는다.</b> 인물의 대답에는 우리가 모르는 표시가
            // 섞여 온다(마크다운·제가 지어낸 꼬리표). 화면에 그대로 올리면 대사 한복판에
            // 태그가 글자로 찍혀, 그 순간 사람의 말이 아니라 기계의 출력이 된다.
            text = Emphasis.OneBreath(Emphasis.Clean(StripMetaAside(text)));
            _npcLine = text;
            _transcript.Add($"{_character.ShownName}: {text}");
            if (!_active) return;
            RefreshSubtitle();
        }

        private static readonly System.Text.RegularExpressions.Regex MetaAside =
            new System.Text.RegularExpressions.Regex(@"[(（][^()（）]*(답하라|하지\s*마라|말고|말라|지시|반응으로만|덧붙이지|대답하라|연기하라|규칙)[^()（）]*[)）]\s*");

        /// <summary>
        /// <b>인물이 제 대사에 지시문을 적어 올 때가 있다.</b> 우리가 벌어진 일을 「(왼 소매를 걷게 한다)」
        /// 처럼 괄호로 건네자, 모형이 그 꼴을 흉내 내 「(질문과 무관한 … 반응으로만 답하라.)」 같은
        /// <b>제게 내린 지시</b>를 대사 앞에 붙여 왔고 그것이 자막에 그대로 떴다(2026-09-14).
        /// 괄호 안에 명령조 낱말이 든 토막만 걷는다 — 「(한숨)」 같은 몸짓은 남긴다.
        /// </summary>
        private static string StripMetaAside(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string t = MetaAside.Replace(text, "").Trim();
            return t.Length == 0 ? text : t;
        }

        private void OnError(string err)
        {
            _npcLine = $"(대답 오류: {err})";
            _busy = false;
            _lineIsKey = false;   // 앞 차례가 붉었다고 오류 줄까지 붉으면 안 된다
            if (!_active) return;
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
            SubtitleView.Show(_character.ShownName, line, hint, _lineIsKey && !_busy);
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
            // <b>Esc 로 자리를 파하던 길을 걷었다.</b>
            //
            // Esc 는 이 게임에서 <b>물러나는 글쇠</b>다 — 든 것을 내려놓고, 수첩을 접고,
            // 판을 닫는다. 그 글쇠 하나에 「사람 앞에서 돌아서기」까지 얹어 두었더니,
            // 손에 든 것을 내려놓으려고 누른 Esc 에 말하던 자리가 통째로 파했다.
            // 되돌리는 길은 없다(첫 마디는 다시 안 나온다).
            //
            // 자리를 파하는 것은 <b>되돌릴 수 없는 일</b>이므로 손으로 골라야 한다 —
            // 판의 「이만 마치겠소」 하나만 남긴다(<see cref="InterrogationPanel"/>).

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
                    if (!ActGate.Fits(t.act)) continue;
                    if (_asked.Contains(t.question)) continue;
                    _shown.Add(t);
                    if (_shown.Count >= cap) break;
                }
                return _shown;
            }
        }

        /// <summary>
        /// <b>이 사람과 마주 선 동안 손에 남겨야 하는 연장인가.</b> 도구띠가 물어본다.
        ///
        /// 마주 서면 벨트는 등불만 남기고 다 내려놓는다. 그런데 이 사람에게 내릴 명령이
        /// 그 연장을 쥐고 있으라 하면, 명령이 뜨기 전에 손이 먼저 비어 영영 못 뜬다.
        /// 씬마다 벨트 목록을 맞춰 적게 두면 한 군데만 빠뜨려도 그 자리가 통째로 죽으므로,
        /// <b>쓸 사람에게 물어본다</b>.
        /// </summary>
        public bool WantsTool(string toolId)
        {
            if (!_active || string.IsNullOrEmpty(toolId)) return false;
            foreach (var o in GetComponents<ItemOrder>())
                if (o != null && o.NeedsToolId == toolId) return true;
            return false;
        }

        /// <summary>
        /// 이 인물에게 내릴 수 있는 명령들(소매 걷기·물건 내밀기). 근거가 없으면 비어 있다.
        /// 막이 안 맞는 것은 여기서 이미 걸러진다 — 과객이 남의 팔을 잡을 수는 없다.
        /// </summary>
        public IEnumerable<InterrogationOrder> Orders
        {
            get
            {
                foreach (var o in GetComponents<InterrogationOrder>())
                    if (o != null && o.FitsAct) yield return o;
            }
        }

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

        /// <summary>
        /// 마주 앉은 자리에서 물러난다. 다시 말을 걸면 이어진다.
        /// 「잠시 멈추다」 단추는 없어졌고, 지금 이것을 부르는 것은 마루의 자리
        /// (<see cref="InterrogationBench"/>)에서 일어설 때다.
        /// </summary>
        public void CloseFromUi() => ClosePanel();

        /// <summary>
        /// 월드 패널의 '이만 마치겠소' 버튼이 호출 — 이 인물과의 볼일을 끝낸다.
        ///
        /// 잠시 창을 치우는 것과 자리를 파하는 것은 다른 일이다. 한 버튼으로 묶어 두면
        /// 손이 미끄러진 한 번에 심문이 영영 끝나 버린다. 끝낸 뒤에는 다시 잠가 둔다 —
        /// 물러가는 사람을 붙잡고 처음부터 다시 물을 수는 없다.
        /// </summary>
        private float _finishAskUntil;

        public void FinishFromUi()
        {
            if (_character != null && !string.IsNullOrWhiteSpace(_character.finishWarnAct1) && !_finished
                && (GameState.Instance == null || !GameState.Instance.IdentityRevealed)
                && Time.unscaledTime > _finishAskUntil)
            {
                _finishAskUntil = Time.unscaledTime + 5f;
                SubtitleView.Show("", _character.finishWarnAct1, "(한 번 더 누르면 자리를 파한다)", true);
                return;
            }
            _finishAskUntil = 0f;
            ClosePanel();

            // 되돌릴 수 없는 일은 한 번만. 이미 마친 사람이면 창만 닫는다.
            //
            // 잠그는 것도 여기 안에 있어야 한다. 밖에 두었더니 두 번째로 마칠 때 잠기기만
            // 하고 풀어 줄 신호(물러가기)는 오지 않아, 그 뒤로 영영 말을 걸 수 없었다.
            if (_finished) return;
            _finished = true;

            // <b>물러갈 사람만 잠근다.</b>
            //
            // 여태 무조건 잠갔다. 잠금을 푸는 것은 <see cref="_onFinished"/> 에 걸린
            // 물러가기 연출이 끝나면서 <see cref="Unlock"/> 을 부르는 일인데, 그 자리가
            // <b>비어 있는 사람</b>이 있다(마당의 마을사람이 그렇다). 그러면 잠그기만
            // 하고 풀 사람이 없어 <b>영영</b> 말을 못 건다.
            //
            // 여태 안 드러난 까닭은 Esc 라는 곁길이 있어서였다 — Esc 는 그냥 창만
            // 닫았으므로 다시 누르면 열렸다. 그 곁길을 걷고 나니 이 자리가 유일한
            // 출구가 되면서 잠금이 그대로 드러났다.
            //
            // 물러가는 연출이 걸려 있으면 그동안만 잠그고, 없으면 자리를 파했을 뿐이니
            // 다시 말을 걸 수 있어야 한다. 마을에서 스친 사람에게 두 번 말을 못 붙일
            // 까닭이 없다.
            bool walksOff = _onFinished != null && _onFinished.GetPersistentEventCount() > 0;
            if (walksOff) _locked = true;
            _onFinished?.Invoke();
        }

        /// <summary>
        /// 이미 한 번 자리를 파했나. <b>다시 못 건다는 뜻은 아니다</b> —
        /// 그것은 <see cref="Locked"/> 가 정한다(물러가는 연출이 있는 사람만 잠긴다).
        /// </summary>
        public bool Finished => _finished;
        private bool _finished;

    }
}
