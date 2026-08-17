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
        private GUIStyle _rich;

        private void Start()
        {
            _gs = GameState.Instance;
            _journal = Journal.Instance;
            _gs.EnterCase(ThisCase);   // 수첩이 이 사건 단서만 보이도록
            _phase = _startPhase;      // 씬이 곧 단계(1부=Stealth, 2부=Revealed)
        }

        private void Update()
        {
            // F2: 흐름 디버그 패널 껐다 켜기(나중에 통째로 지워도 됨)
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
                _showDebugPanel = !_showDebugPanel;

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
                case "G01": case "G02": case "G04": return Has("J09"); // 필적 차이가 있어야 문서고에서 뭘 찾을지 앎
                case "G05": return Has("J09");                          // 아내 추궁
                case "G03": return Has("G05");                          // 아내 증언 → 입안 대장
                case "G06": return Has("J15");                          // 속량 문서 → 하인 추궁
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

        /// <summary>진실 규명의 결정타(대면 심문 '복동아')가 가능한가.</summary>
        private bool CanFinalPress => Has("J13") && Has("G03");

        private void Judge(Verdict v)
        {
            _gs.SetGapriHandled(_gapri);
            _gs.SetVerdict(ThisCase, v);   // 완료 처리 + 조사청 큐브 금색 + 기록대 누적
            _phase = Phase.Judged;
            Debug.Log($"[옹고집] 판결: {v} / 갑리처결:{_gapri} → 조사청 복귀");
            _gs.ExitToHub();
            if (!string.IsNullOrEmpty(_hubSceneName) && Application.CanStreamedLevelBeLoaded(_hubSceneName))
                SceneManager.LoadScene(_hubSceneName);
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
            if (_rich == null)
                _rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 13 };

            GUILayout.BeginArea(new Rect(12, 12, 560, Screen.height - 24), GUI.skin.box);
            GUILayout.Label("<b>[옹고집 사건 — 흐름 디버그]</b>  <size=11>(F2: 끄기/켜기)</size>", _rich);
            GUILayout.Label($"단계: <b>{_phase}</b>    잠행단서 {CountDiscovered(true)}/15   공개단서 {CountDiscovered(false)}/6", _rich);

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
                if (GUILayout.Button("▶ 출도(마패) — 관아로, 되돌릴 수 없음")) _pending = Reveal;
                GUI.enabled = true;
                if (!canReveal)
                    GUILayout.Label($"<size=11>필수 단서 부족: {MissingRequired()}</size>", _rich);
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
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("진실대로")) _pending = () => Judge(Verdict.Truth);
                if (GUILayout.Button("정상참작")) _pending = () => Judge(Verdict.Mercy);
                if (GUILayout.Button("甲 인정")) _pending = () => Judge(Verdict.AcceptFake);
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
            if (GUILayout.Button($"발견 {c.key}: {c.text}"))
            {
                var captured = c;
                _pending = () => Discover(captured);
            }
            GUI.enabled = true;
        }
    }
}
