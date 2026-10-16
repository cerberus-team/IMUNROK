using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using IMUNROK.Common;

namespace IMUNROK.Onggojip
{
    /// <summary>
    /// 제1사건(옹고집전) 흐름 뼈대.
    ///  잠행(1부) → 출도(되돌릴 수 없음) → 공개조사(2부) → 판결
    ///
    /// 단서는 공통 수첩(Journal)에 기록하고, 판결은 공통 GameState에 남긴다.
    /// 진행 잠금(어떤 단서가 어떤 조사를 여는지)은 아래 CanDiscover/게이트에 규칙으로.
    ///
    /// ★ 지금은 로직 뼈대 + 디버그 패널(OnGUI)만. 방·인물·상호작용은 다음 단계에서 이 규칙 위에 얹는다.
    /// </summary>
    public class OnggojipCase : MonoBehaviour
    {
        public enum Phase { Stealth, Revealed, Judged }

        [SerializeField] private bool _showDebugPanel = false;   // 기본 꺼둠(F2로 켜기)
        [SerializeField] private string _hubSceneName = "HubScene";

        [Tooltip("이 씬의 시작 단계. 1부(옹씨댁 밤)=Stealth, 2부(관아 아침)=Revealed")]
        [SerializeField] private Phase _startPhase = Phase.Stealth;

        [Tooltip("출도 시 로드할 2부(관아) 씬 이름")]
        [SerializeField] private string _act2SceneName = "Onggojip_Gwana";

        private const CaseId ThisCase = CaseId.Case1_Onggojip;

        private Phase _phase = Phase.Stealth;
        private bool _gapri;
        private GameState _gs;
        private Journal _journal;

        private System.Action _pending;   // OnGUI에서 클릭한 동작을 다음 Update에 실행(레이아웃 안전)
        private Vector2 _scroll;
        private GUIStyle _rich, _btn, _head;
        private int _styledFor;           // 어느 화면 높이에 맞춰 지은 글씨인가

        /// <summary>
        /// <b>판은 화면에 맞춰 큰다.</b>
        ///
        /// 560화소·13pt 로 못 박아 두었더니 1440줄짜리 화면에서는 글씨가 깨알만 해
        /// 무엇이 적혔는지 읽으려면 화면에 얼굴을 붙여야 했다. 도구띠가 이미 같은 일을
        /// 겪고 화면 세로의 몫으로 옮겨 갔다(ToolbeltHud) — 여기도 같게 둔다.
        /// 1080줄에서 17pt, 1440줄에서 23pt.
        /// </summary>
        private void EnsureStyles()
        {
            if (_rich != null && _styledFor == Screen.height) return;
            _styledFor = Screen.height;
            int fs = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.016f), 13, 32);

            _rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = fs };
            _head = new GUIStyle(_rich) { fontSize = fs + 4 };
            _btn  = new GUIStyle(GUI.skin.button) { richText = true, fontSize = fs, wordWrap = true };
            _btn.padding = new RectOffset(fs / 2, fs / 2, fs / 3, fs / 3);
        }

        private void Start()
        {
            _gs = GameState.Instance;
            _journal = Journal.Instance;
            _gs.EnterCase(ThisCase);   // 수첩이 이 사건 단서만 보이도록
            _phase = _startPhase;      // 씬이 곧 단계(1부=Stealth, 2부=Revealed)

            // <b>관아를 바로 재생하면 신분이 아직 과객이었다.</b> 마패를 꺼내 출도한 길로만
            // 신분이 바뀌므로, 편집기에서 2막 씬을 곧장 틀면 「2막 것」으로 적어 둔 물음·
            // 게이트·명령이 하나도 안 뜨고 AI 도 1막인 줄 안다 — 「2막이 비었다」로 보인다.
            // 2막에서 시작하는 씬이면 신분도 2막이어야 한다.
            // 다만 <b>들켜서 붙들려 온 길</b>이면 서리 앞에서 마패를 꺼낼 때(GwanaUsher)까지 과객으로 둔다 —
            // 여기서 곧장 드러내면 「자네는 누구길래」 하는 동안 호칭·물음이 이미 어사였다(2026-09-15).
            if (_startPhase == Phase.Revealed && !_gs.IdentityRevealed && !_gs.CaughtInStealth) _gs.RevealIdentity(true);
            // <b>1막으로 들어서면 과객으로 되돌린다</b>(2026-09-15). 조사청은 늘 1막을 여는데, 한 번 2막까지 갔던 판은
            // 신분이 어사로 남아 요의 출도 단추가 안 떴다(1막을 끝낼 길이 들키는 것뿐이었다).
            if (_startPhase == Phase.Stealth)
            {
                if (_gs.IdentityRevealed) _gs.RevealIdentity(false);
                if (_gs.CaughtInStealth) _gs.SetCaughtInStealth(false);
                StealthLives.ResetForNight();
            }
            RoyalWarrant.MissingNote = MissingNote;
        }

        private void Update()
        {
            // F2: 흐름 디버그 패널 껐다 켜기 — 편집기·개발 빌드에서만(배포판에서 출도를 건너뛸 수 있었다)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
                _showDebugPanel = !_showDebugPanel;
#endif

            if (_pending != null)
            {
                var a = _pending;
                _pending = null;
                a();
            }
        }

        // ── 조회 ──
        private bool Has(string key) => _journal.HasClue(ThisCase, key);

        /// <summary>이 단서를 지금 얻을 수 있는가(단계 + 진행 잠금 규칙).</summary>
        private bool CanDiscover(ClueDef c)
        {
            if (Has(c.key)) return false;

            if (c.stealth)
                return _phase == Phase.Stealth;   // J는 잠행 중에만

            if (_phase != Phase.Revealed) return false;   // G는 출도 후에만
            switch (c.key)
            {
                // <b>여기는 F2 판이 쓰는 표다.</b> 실제 잠금은 씬의 _needsClueKey 와 자료의
                // alsoNeedsClueKeys 가 따로 건다(아래 CanDiscover 주석 참조). 그래도 두 벌이
                // 어긋나면 F2 로 흐름을 짚어 보는 사람이 오진하므로 씬에 맞춰 적어 둔다.
                case "G01": case "G02": case "G04": case "G14": return true;   // 문서고 문서는 조건 없이 열린다(G01 만 팔 재기의 근거)
                case "G05": return Has("J06");                          // 아내 추궁 — 필적을 들이밀어야 연다
                case "G11": return Has("G03");                          // 입안대장을 쥐고 등불을 대야 지운 밑글이 뜬다
                case "G12": return true;                                // 등경 · 동경 — 등불 놀이(LightBeam 등불 모드)
                case "G13": return Has("J13") && Has("G03");            // 甲의 자백
                case "G15": return Has("G09");                          // 乙에게 게장
                case "G16": return Has("J08") || Has("G14");             // 乙의 갑리 인정
                case "G17": return Has("J11") || Has("G05");             // 乙의 형제 인정
                case "G06": return Has("J11");                          // 별급문기 → 늙은 하인 추궁(속량)
                case "G07": return Has("G01");                          // 대장을 봐야 유척으로 견줄 수가 있다
                case "G08": return Has("G01");                          // 저쪽 팔도 같은 자로 잰다
                case "G09": return true;                                // 아내에게 묻기만 하면 된다
                case "G10": return Has("G09");                          // 아내의 말을 쥐고 甲에게 게장을 권한다
                default: return true;
            }
        }

        private void Discover(ClueDef c) => _journal.AddClue(ThisCase, c.key, c.text);

        /// <summary>출도 가능? (잠행 중 + 필수 4단서 확보)</summary>
        private bool CanReveal()
        {
            if (_phase != Phase.Stealth) return false;
            foreach (var k in OnggojipClues.RequiredForReveal)
                if (!Has(k)) return false;
            return true;
        }

        /// <summary>요의 출도 확인에 덧붙는 한 줄 — 무엇을 아직 못 챙겼는지(결론 없이 갈래만).</summary>
        private string MissingNote()
        {
            var miss = new System.Collections.Generic.List<string>();
            if (!Has("J02")) miss.Add("주인에게서 들을 말");
            if (!Has("J06")) miss.Add("장부의 손");
            if (!Has("J11")) miss.Add("주인이 깔고 앉았던 자리");
            if (!Has("J13")) miss.Add("관에 올린 신고");
            if (!Has("J15")) miss.Add("잠긴 것");
            return miss.Count == 0 ? null : "아직 살피지 못한 것 — " + string.Join(" · ", miss.ToArray());
        }

        private string MissingRequired()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var k in OnggojipClues.RequiredForReveal)
                if (!Has(k)) list.Add(k);
            return list.Count == 0 ? "(없음)" : string.Join(", ", list);
        }

        private void Reveal()
        {
            if (!CanReveal()) return;
            Debug.Log("[옹고집] 출도(出道) — 관아(2부)로 이동. 되돌릴 수 없음.");
            NoiseMeter.Disarm();   // 소리계가 관아까지 따라갔다

            // <b>막을 넘긴다.</b> 여태 이 자리가 씬만 갈아 끼우고 신분은 그대로 두어,
            // F2 로 출도한 사람은 「2막 것」으로 적어 둔 물음·게이트·명령이 하나도 안 뜨는
            // 관아에 들어섰다. 마패를 꺼내는 정규 길(RoyalWarrant)만 신분을 바꾸고 있었다.
            var gs = GameState.Instance;
            if (gs != null && !gs.IdentityRevealed) gs.RevealIdentity(true);

            // 되돌릴 수 없음: 2부 씬 로드(단서는 수첩에 그대로 유지됨).
            if (!string.IsNullOrEmpty(_act2SceneName) && Application.CanStreamedLevelBeLoaded(_act2SceneName))
            {
                SceneManager.LoadScene(_act2SceneName);
            }
            else
            {
                // 2부 씬이 아직 없으면 같은 씬에서 단계만 전환(안전 대체)
                _phase = Phase.Revealed;
                Debug.LogWarning($"[옹고집] 2부 씬('{_act2SceneName}')이 없어 같은 씬에서 Revealed로 대체.");
            }
        }

        /// <summary>
        /// 진실 규명의 결정타(대면 심문 '복동아')가 가능한가.
        /// <b>조건은 한 자리에만 둔다</b> — <see cref="OnggojipEnding.TruthProven"/>.
        /// 여기와 거기에 같은 조건을 두 벌 적으면 한쪽만 고쳐져 어긋난다.
        /// </summary>
        private bool CanFinalPress => OnggojipEnding.TruthProven;

        private void Judge(Verdict v)
        {
            // <b>판결은 이야기의 끝을 거쳐 적는다.</b> 여태 여기서 곧바로
            // SetVerdict 를 불렀는데, 그러면 공통 기록에 남는 것은 판결 셋뿐이라
            // 「이 사건이 어떻게 끝났는가」를 물을 자리가 없다. 제3사건은 그 켜를
            // 이미 지니고 있다(GyeonuCase.FinishCase) — 같은 얼개로 맞춘다.
            var ending = OnggojipEnding.FinishCase(v, _gapri);
            _phase = Phase.Judged;
            Debug.Log($"[옹고집] 판결: {v} / 갑리처결:{_gapri} / 끝: {OnggojipEnding.LabelOf(ending)} → 조사청 복귀");
            _gs.ExitToHub();
            if (!string.IsNullOrEmpty(_hubSceneName) && Application.CanStreamedLevelBeLoaded(_hubSceneName))
                SceneManager.LoadScene(_hubSceneName);
        }

        /// <summary>지금 갑리 토글 그대로 그 판결을 고르면 어떤 끝이 되는가(판에 미리 보이려고).</summary>
        private OnggojipEndingId Peek(Verdict v)
        {
            if (v == Verdict.Truth) return _gapri ? OnggojipEndingId.Truth : OnggojipEndingId.HalfTruth;
            return OnggojipEnding.EndingFor(v);
        }

        private int CountDiscovered(bool stealth)
        {
            int n = 0;
            foreach (var c in OnggojipClues.All)
                if (c.stealth == stealth && Has(c.key)) n++;
            return n;
        }

        // ─────────────────────────────────────────────
        //  디버그 패널 — 사건을 처음부터 끝까지 클릭으로 확인
        // ─────────────────────────────────────────────
        private void OnGUI()
        {
            if (!_showDebugPanel) return;
            EnsureStyles();

            // 너비도 화면을 따른다. 좁은 화면에서는 화면의 절반, 넓은 화면에서는
            // 아무리 넓어도 눈이 한 번에 훑을 수 있는 만큼까지만.
            float w = Mathf.Clamp(Screen.width * 0.42f, 420f, 900f);
            float m = Mathf.Round(Screen.height * 0.012f);
            GUILayout.BeginArea(new Rect(m, m, w, Screen.height - m * 2f), GUI.skin.box);
            GUILayout.Label("<b>[옹고집 사건 — 흐름 디버그]</b>  (F2: 끄기/켜기)", _head);
            // 분모를 손으로 적어 두었더니 표가 늘어도 6 에 머물렀다. 표에서 센다.
            int allJ = 0, allG = 0;
            foreach (var c in OnggojipClues.All) { if (c.stealth) allJ++; else allG++; }
            GUILayout.Label($"단계: <b>{_phase}</b>    잠행단서 {CountDiscovered(true)}/{allJ}   공개단서 {CountDiscovered(false)}/{allG}", _rich);
            // <b>저울을 보이게 둔다.</b> 물증 두 점, 들은 말 한 점(Journal.Score).
            // 결말의 문턱을 여기 걸 생각이면 먼저 이 수가 어떻게 움직이는지 보여야 한다 —
            // 안 보이는 수에 문턱을 걸면 얼마로 잡아야 할지 알 길이 없다.
            GUILayout.Label($"무게: <b>{_journal.Score(ThisCase)}</b>점  "
                          + $"(물증 {_journal.WeighedCount(ThisCase, ClueKind.물증)}×{Journal.WeightMuljeung} + "
                          + $"들은 말 {_journal.WeighedCount(ThisCase, ClueKind.정황)}×{Journal.WeightJeonghwang})", _rich);

            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("── 1부 잠행 단서 (J) ──", _rich);
            foreach (var c in OnggojipClues.All)
                if (c.stealth) DrawClueRow(c);

            // 1부(잠행)에서만: 출도
            if (_phase == Phase.Stealth)
            {
                GUILayout.Space(8);
                bool canReveal = CanReveal();
                GUI.enabled = canReveal;
                if (GUILayout.Button("▶ 출도(마패) — 관아로, 되돌릴 수 없음", _btn)) _pending = Reveal;
                GUI.enabled = true;
                if (!canReveal)
                    GUILayout.Label($"필수 단서 부족: {MissingRequired()}", _rich);
            }

            // 2부(공개조사)에서만: 공개 단서 + 판결
            if (_phase == Phase.Revealed)
            {
                GUILayout.Space(8);
                GUILayout.Label("── 2부 공개 단서 (G) ──", _rich);
                foreach (var c in OnggojipClues.All)
                    if (!c.stealth) DrawClueRow(c);

                GUILayout.Space(8);
                GUILayout.Label($"최종 추궁 '복동아' 가능(J13+G03): <b>{CanFinalPress}</b>", _rich);

                GUILayout.Space(8);
                GUILayout.Label("── 판결 ──", _rich);
                _gapri = GUILayout.Toggle(_gapri, " 갑리(고리대) 처결");

                // <b>어떤 끝이 되는지 먼저 보여 준다.</b> 판결 단추 셋만 있으면
                // 갑리 토글이 결말을 가른다는 것이 화면에 안 나온다 — 안 보이는
                // 갈래에 손을 얹게 두면 무엇을 고른 것인지 모르고 고르게 된다.
                GUILayout.Label($"지금 고르면 → <b>{OnggojipEnding.LabelOf(Peek(Verdict.Truth))}</b> / "
                              + $"{OnggojipEnding.LabelOf(Peek(Verdict.Mercy))} / "
                              + $"{OnggojipEnding.LabelOf(Peek(Verdict.AcceptFake))}", _rich);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("진실대로", _btn)) _pending = () => Judge(Verdict.Truth);
                if (GUILayout.Button("정상참작", _btn)) _pending = () => Judge(Verdict.Mercy);
                if (GUILayout.Button("甲 인정", _btn)) _pending = () => Judge(Verdict.AcceptFake);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawClueRow(ClueDef c)
        {
            if (Has(c.key))
            {
                GUILayout.Label($"<color=#8f8>✓</color> {c.key}  {c.text}{(c.required ? "  <color=#fc6>[필수]</color>" : "")}", _rich);
                return;
            }
            GUI.enabled = CanDiscover(c);
            if (GUILayout.Button($"발견 {c.key}: {c.text}", _btn))
            {
                var captured = c;
                _pending = () => Discover(captured);
            }
            GUI.enabled = true;
        }
    }
}
