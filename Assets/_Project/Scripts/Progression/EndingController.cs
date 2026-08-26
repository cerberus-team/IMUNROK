using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 복명(엔딩): 누적된 세 판결을 왕이 읽고 총평한다.
    ///  - 왕은 3D 모델 없음. 지금은 자막만(추후 오디오 추가).
    ///  - 판결 3개를 사건 순서대로 낭독.
    ///  - 세 판결의 경향(다수결)에 따라 왕의 총평이 분기.
    ///  - 마지막 대사 "그래서, 그 이야기들은 거짓이었느냐." 로 종료.
    ///
    /// 자막은 <see cref="SubtitleView"/> 로 세상 속에 띄운다 — OnGUI 는 헤드셋에 안 보인다.
    /// 진행: 자동으로 넘어가거나(줄당 시간), 스페이스/클릭으로 즉시 다음 줄.
    /// </summary>
    public class EndingController : MonoBehaviour
    {
        [Header("진행")]
        [Tooltip("한 줄이 자동으로 다음으로 넘어가기까지의 시간(초). 스페이스/클릭으로 즉시 넘길 수도 있음")]
        [SerializeField] private float _secondsPerLine = 4.5f;

        [Header("대사(인스펙터에서 수정 가능)")]
        [SerializeField] private string _speakerName = "왕";
        [TextArea] [SerializeField] private string _introLine = "복명(復命)이라. 어사, 세 사건의 전말을 아뢰어라.";

        [Header("총평 분기 — 판결 경향별")]
        [TextArea] [SerializeField] private string _summaryTruthHeavy =
            "그대는 이야기를 걷어내고 사실만을 좇았구나. 냉정하나, 그것이 국법이다.";
        [TextArea] [SerializeField] private string _summaryMercyHeavy =
            "그대는 법 앞에서도 사람의 사정을 먼저 보았다. 무르나, 어질다 하겠다.";
        [TextArea] [SerializeField] private string _summaryFakeHeavy =
            "그대는 설화가 덮은 것을 그대로 두었다. 편하나, 그 자리에 진실은 없다.";
        [TextArea] [SerializeField] private string _summaryMixed =
            "그대의 판결에는 하나의 결이 없구나. 사안마다 저울이 달랐다.";

        [Header("마지막 대사")]
        [TextArea] [SerializeField] private string _finalLine = "그래서, 그 이야기들은 거짓이었느냐.";

        [Header("종료 후")]
        [SerializeField] private string _hubSceneName = "HubScene";

        [Header("디버그")]
        [Tooltip("판결 데이터가 비어 있으면(엔딩 씬을 단독 실행) 샘플 판결로 채워 테스트")]
        [SerializeField] private bool _autofillIfEmpty = true;

        // 실제 낭독할 자막 줄들
        private readonly List<string> _lines = new List<string>();
        private int _index;
        private float _timer;
        private bool _finished;


        private void Start()
        {
            var gs = GameState.Instance;

            // 단독 실행 등으로 판결이 비어 있으면 샘플로 채움(테스트 편의)
            if (_autofillIfEmpty && gs.CompletedCount == 0)
            {
                Debug.Log("[EndingController] 판결 데이터가 없어 샘플 판결로 채웁니다(디버그).");
                gs.SetVerdict(CaseId.Case1_Onggojip, Verdict.Truth);
                gs.SetVerdict(CaseId.Case2_Seocheon, Verdict.Mercy);
                gs.SetVerdict(CaseId.Case3_Gyeonu, Verdict.Truth);
            }

            BuildLines(gs);
        }

        /// <summary>낭독할 자막 줄 목록을 구성한다.</summary>
        private void BuildLines(GameState gs)
        {
            _lines.Clear();

            // 1) 도입
            _lines.Add(_introLine);

            // 2) 사건별 판결 낭독(Case1→2→3 순서)
            foreach (CaseId id in Enum.GetValues(typeof(CaseId)))
            {
                Verdict v = gs.GetVerdict(id);
                string line = $"{CaseTitle(id)}\n— 판결: {VerdictText(v)}.";

                // 제1사건은 갑리 처리 여부를 덧붙임(전용 데이터 활용)
                if (id == CaseId.Case1_Onggojip)
                    line += gs.GapriHandled ? " 갑리는 처결되었다." : " 갑리의 처분은 기록되지 않았다.";

                _lines.Add(line);
            }

            // 3) 경향에 따른 총평 분기
            _lines.Add(SummaryLine(gs));

            // 4) 마지막 대사
            _lines.Add(_finalLine);

            _index = 0;
            _timer = 0f;
            _finished = false;
        }

        /// <summary>세 판결의 다수결로 총평을 고른다. 최다 판결이 2개 이상이면 그 경향, 아니면 혼합.</summary>
        private string SummaryLine(GameState gs)
        {
            int truth = 0, mercy = 0, fake = 0;
            foreach (Verdict v in gs.GetVerdictsInOrder())
            {
                if (v == Verdict.Truth) truth++;
                else if (v == Verdict.Mercy) mercy++;
                else if (v == Verdict.AcceptFake) fake++;
            }

            // 최다 경향 찾기
            int max = Mathf.Max(truth, Mathf.Max(mercy, fake));
            if (max < 2) return _summaryMixed; // 셋이 제각각이면 혼합

            if (truth == max) return _summaryTruthHeavy;
            if (mercy == max) return _summaryMercyHeavy;
            return _summaryFakeHeavy;
        }

        private void Update()
        {
            if (_finished)
            {
                HandleFinishedInput();
                return;
            }

            _timer += Time.deltaTime;
            if (_timer >= _secondsPerLine || AdvancePressed())
                Next();
        }

        private void Next()
        {
            _timer = 0f;
            _index++;
            if (_index >= _lines.Count)
            {
                _index = _lines.Count - 1;
                _finished = true;
                _justFinished = true;
                Debug.Log("[EndingController] 복명을 마칩니다.");
            }
        }

        private void HandleFinishedInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            // H: 조사청으로 돌아가기
            if (kb.hKey.wasPressedThisFrame && Application.CanStreamedLevelBeLoaded(_hubSceneName))
                SceneManager.LoadScene(_hubSceneName);

            // Esc: 종료(에디터에선 Play 정지, 빌드에선 앱 종료)
            if (kb.escapeKey.wasPressedThisFrame)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
#endif
        }

        private bool AdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool space = kb != null && kb.spaceKey.wasPressedThisFrame;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            return space || click;
#else
            return false;
#endif
        }

        // ─────────────────────────────────────────────
        //  자막 — <b>세상 속에 띄운다</b>
        // ─────────────────────────────────────────────
        //
        // 여태 이 대목만 OnGUI 였다. 그때는 그것이 편했다 — 폰트 에셋 없이도 한글이
        // 확실히 찍히니까. 그런데 <b>OnGUI 는 헤드셋에 안 보인다</b>. 화면 위에 덧그리는
        // 그림이라 두 눈으로 갈리는 그림에는 아예 끼지 못한다. 그러니 헤드셋을 쓰고
        // 복명에 들어서면 왕의 말이 <b>한 줄도 안 뜬다</b> — 캄캄한 데 서서 아무 일도
        // 안 일어나는 것으로 보인다.
        //
        // 이 게임은 이미 세상 속에 뜨는 자막을 가지고 있다(<see cref="SubtitleView"/>).
        // 어전이라고 다를 까닭이 없다. 줄이 바뀔 때마다 그쪽에 넘긴다.

        /// <summary>지금 띄워 둔 줄. 바뀔 때만 다시 띄운다.</summary>
        private int _shown = -1;

        private void LateUpdate()
        {
            if (_lines.Count == 0) return;
            int at = Mathf.Clamp(_index, 0, _lines.Count - 1);
            if (at == _shown && !_justFinished) return;
            _shown = at;
            _justFinished = false;

            SubtitleView.Show(_speakerName, _lines[at], Hint(), true);
        }

        private bool _justFinished;

        /// <summary>
        /// 아랫줄. <b>헤드셋에서는 자판 이름을 안 적는다</b> — 누를 손이 없다.
        /// </summary>
        private string Hint()
        {
            if (!_finished) return Controls.Skip;
            return Controls.Vr ? "— 복명을 마친다 —" : "— 복명을 마친다 —    (H: 조사청으로,  Esc: 종료)";
        }

        private string CurrentLine()
        {
            if (_lines.Count == 0) return "";
            return _lines[Mathf.Clamp(_index, 0, _lines.Count - 1)];
        }

        // ─────────────────────────────────────────────
        //  표시용 텍스트 매핑
        // ─────────────────────────────────────────────
        private static string CaseTitle(CaseId id)
        {
            return id switch
            {
                CaseId.Case1_Onggojip => "제1사건 · 옹고집전 — 누가 진짜인가",
                CaseId.Case2_Seocheon => "제2사건 · 서천꽃밭 — 네 번째 환생자",
                CaseId.Case3_Gyeonu   => "제3사건 · 견우직녀 — 칠석 실종 사건",
                _ => id.ToString(),
            };
        }

        private static string VerdictText(Verdict v)
        {
            return v switch
            {
                Verdict.Truth      => "진실대로 고함",
                Verdict.Mercy      => "정상을 참작함",
                Verdict.AcceptFake => "갑(甲)을 진짜로 인정함",
                _                  => "판결하지 못함",
            };
        }
    }
}
