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

        [Header("맺음 — 왕의 말이 끝난 뒤")]
        /// <summary>
        /// <b>어전이 꺼지고 맺음말만 남는다.</b>
        ///
        /// 왕의 마지막 물음("그래서, 그 이야기들은 거짓이었느냐")에는 답이 없다.
        /// 답을 자막으로 붙이면 그건 이 게임이 제 물음에 제가 답하는 꼴이 된다.
        /// 그러니 그 자리에서 <b>세상을 끈다</b> — 물음만 남기고 어전이 사라지면,
        /// 답할 사람은 화면 밖에 남는다.
        ///
        /// 그 어둠 위에 맺음말과 만든 사람을 적는다.
        /// </summary>
        [Tooltip("맺음말이 뜰 때 끌 것(어전 통째로). 비우면 세상이 그대로 남는다")]
        [SerializeField] private GameObject _worldToHide;

        [TextArea(2, 3)] [SerializeField] private string _thanksLine = "플레이해 주셔서 감사합니다.";

        [Tooltip("만든 사람. 줄을 바꿔 여럿 적으면 그대로 뜬다")]
        [TextArea(3, 8)] [SerializeField] private string _creditsLine =
            "이문록(異聞錄)\n\n만든 사람\n(여기에 이름을 적으십시오)";

        /// <summary>
        /// <b>빌려 온 것을 밝힌다.</b>
        ///
        /// CC-BY 와 공공누리 제1유형은 둘 다 <b>출처를 적으면 쓸 수 있다</b>는 조건이다.
        /// 적지 않으면 조건을 안 지킨 것이 되고, 그 조건은 <b>학내 시연에도 걸린다</b> —
        /// 밖에 내놓을 때만의 일이 아니다.
        ///
        /// 만든 사람 다음에 온다. 사람 이름과 빌린 것을 한 판에 섞으면 둘 다 안 읽힌다.
        ///
        /// 낱낱의 목록은 Docs/에셋_출처와_라이선스.md 에 있다 — 화면에는
        /// <b>어디서 왔는지</b>만 적는다. 스무 줄을 띄워 봐야 아무도 안 읽는다.
        /// </summary>
        [Tooltip("빌려 온 것의 출처. <b>한 칸이 고리에 걸리는 판 하나</b>다 — " +
                 "한 칸에 몰아 넣으면 판 하나가 길어져 두른 보람이 없다")]
        [TextArea(2, 4)] [SerializeField] private string[] _sourceLines = {
            "쓰인 것들",
            "한국공예디자인문화진흥원\nKCDF",
            "운현궁 소장품 3D",
            "국가유산청 — 경복궁 3D\n공공누리 제1유형",
            "한국저작권위원회 공유마당\n음향 · CC BY",
        };

        [Tooltip("크레딧 고리가 한 바퀴 도는 데 걸리는 시간(초). " +
                 "이 시간 안에 모든 판이 한 번씩 정면을 지난다")]
        [SerializeField] private float _creditsSeconds = 46f;

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
            //    <b>이 줄들에서 봉서가 한 통씩 왕에게 굴러 올라간다</b>(복명).
            //    몇 번째 줄인지 적어 두어야 그때를 안다.
            _verdictFrom = _lines.Count;
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

            // 5) 맺음.
            //
            // <b>여기서 자막으로 크레딧을 띄우지 않는다.</b> 자막 바는 대사 그릇이라,
            // 거기에 이름과 출처를 넣으니 짜쳤다. 왕의 마지막 말이 끝나면
            // <see cref="OutroCeremony"/> 가 어전을 저물게 하고, 크레딧은
            // <see cref="CreditsRing"/> 이 <b>사방에 둘러</b> 건다.
            //
            // 그런데도 줄을 여기 넣어 두는 까닭: 의식이 없는 씬(안전망)에서는
            // 이 줄들이 자막으로 나가야 하고, 무엇보다 <b>이 줄 수만큼 재생이
            // 이어져야</b> 한다. 여기를 비우면 왕의 마지막 말과 동시에 끝나 버린다.
            _fromLine = _lines.Count;
            if (!string.IsNullOrEmpty(_thanksLine)) _lines.Add(_thanksLine);
            if (!string.IsNullOrEmpty(_creditsLine)) _lines.Add(_creditsLine);
            if (_sourceLines != null)
                for (int i = 0; i < _sourceLines.Length; i++)
                    if (!string.IsNullOrEmpty(_sourceLines[i])) _lines.Add(_sourceLines[i]);

            // <b>엔딩은 닫을 수 없다.</b> 왕의 마지막 말과 만든 사람 이름에 「닫기 ✕」가
            // 붙어 있으면, 읽으라고 띄운 것이 아니라 지나가는 알림처럼 보인다.
            // 게다가 정말 닫으면 그 뒤로는 아무것도 안 뜨고 빈 어전만 남는다.
            SubtitleView.Closable = false;

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
            {
                SubtitleView.Closable = true;   // 어전을 나서면 도로 닫을 수 있어야 한다
                SceneManager.LoadScene(_hubSceneName);
            }

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
            bool click = UiGuard.AnywherePressed;   // 단추 위에서는 안 센다
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

        [Header("복명 의식")]
        [Tooltip("발을 걷고 어전을 저물게 하는 것. 비우면 예전처럼 끄고 자막으로 적는다")]
        [SerializeField] private OutroCeremony _ceremony;

        /// <summary>판결을 읊는 첫 줄. 그 줄부터 셋 동안 봉서가 한 통씩 올라간다.</summary>
        private int _verdictFrom = -1;
        private int _sentUpTo = -1;
        private bool _closing;

        /// <summary>
        /// <b>크레딧은 사방을 두른다.</b>
        ///
        /// 앞서 한지 한 장에 적어 눈앞에 띄웠다. 그릇은 맞았는데 <b>크기가 틀렸다</b> —
        /// 만든 사람 셋과 빌려 온 것 여남은 줄이 손바닥만 한 종이에 다 들어가니
        /// 끝을 맺는 것이 아니라 <b>쪽지 한 장 읽고 마는</b> 것이 됐다. 헤드셋에서는
        /// 그 종이 하나 말고 온 사방이 텅 비어 있기까지 했다.
        ///
        /// <see cref="CreditsRing"/> 이 글을 고리처럼 둘러 걸고 한 바퀴 돌린다.
        /// 가만히 서 있어도 모든 글이 한 번씩 정면을 지나고, 고개를 돌리면 지나간
        /// 것과 올 것이 사방에 걸려 있다.
        ///
        /// <b>뭉치를 나눠 넘긴다</b> — 한 덩어리로 이어 붙이면 판 하나에 다 들어가
        /// 두른 보람이 없다. 맺음말 · 만든 사람 · 빌려 온 것들이 저마다 한 판이다.
        /// </summary>
        private void Credits()
        {
            var blocks = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(_thanksLine)) blocks.Add(_thanksLine);
            if (!string.IsNullOrEmpty(_creditsLine)) blocks.Add(_creditsLine);
            if (_sourceLines != null)
                for (int i = 0; i < _sourceLines.Length; i++)
                    if (!string.IsNullOrEmpty(_sourceLines[i])) blocks.Add(_sourceLines[i]);

            CreditsRing.Show(blocks, _creditsSeconds);
        }

        private void LateUpdate()
        {
            if (_lines.Count == 0) return;
            int at = Mathf.Clamp(_index, 0, _lines.Count - 1);
            if (at == _shown && !_justFinished) return;
            _shown = at;
            _justFinished = false;

            // 판결을 읊는 줄마다 봉서 한 통이 어도를 따라 왕에게 굴러 올라간다.
            if (_ceremony != null && _verdictFrom >= 0
                && at >= _verdictFrom && at < _verdictFrom + 3 && at > _sentUpTo)
            {
                _sentUpTo = at;
                _ceremony.SendScroll(at - _verdictFrom);
            }

            // 왕의 말이 다 끝났다. <b>어전을 끄지 않는다</b> — 저물게 한다.
            bool ending = _fromLine >= 0 && at >= _fromLine;
            if (ending && !_closing)
            {
                _closing = true;
                if (_ceremony != null) { _ceremony.Close(Credits); return; }

                // 의식이 없으면 예전처럼 끄고 자막으로 적는다(안전망)
                if (_worldToHide != null && _worldToHide.activeSelf)
                {
                    _worldToHide.SetActive(false);
                    RenderSettings.ambientIntensity = 0f;
                }
            }
            if (ending && _ceremony != null) return;   // 의식이 도는 동안엔 자막을 안 띄운다

            // <b>붉은 글씨는 여기 것이 아니다.</b> 마지막 인자는 「결정적 한마디」 표시라,
            // 심문에서 <b>물증이 상대의 말을 뒤집는 순간</b>에만 붉게 지나가라고 둔 것이다.
            // 그것을 엔딩 전 줄에 걸어 두었더니 왕의 맺음말도, 만든 사람 이름도, 출처도
            // 죄 붉었다. 다 붉으면 붉은 것이 아무 뜻도 없다 — 게다가 어전은 어둡고
            // 발은 검어서, 그 위의 붉은 글씨는 <b>경고문</b>처럼 읽힌다.
            SubtitleView.Show(ending ? "" : _speakerName, _lines[at], Hint(), false);
        }

        private bool _justFinished;

        /// <summary>맺음말이 시작되는 줄. -1 이면 없다.</summary>
        private int _fromLine = -1;

        /// <summary>
        /// 아랫줄.
        /// </summary>
        private string Hint()
        {
            if (!_finished) return Controls.Skip;
            return "— 복명을 마친다 —    (H: 조사청으로,  Esc: 종료)";
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
