using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// VR 기기 없이 에디터에서 GameState를 검증하기 위한 키보드 디버그 도구.
    /// 빈 GameObject에 붙여 두고 Play 하면, 키 입력으로 사건 선택·판결 기록을 흉내낼 수 있음.
    /// 화면 좌상단에 현재 상태가 실시간으로 표시됨(OnGUI).
    ///
    /// 이 프로젝트는 신 Input System 전용(Active Input Handling = Input System Package)이라
    /// Keyboard.current 로 키를 읽는다.
    ///
    /// [조작]
    ///   1 / 2 / 3 : 대상 사건 선택 + 그 사건에 진입(시뮬) — 수첩이 이 사건 단서를 보여줌
    ///   B         : 조사청(사건 밖) 시뮬 — 수첩에서 사건 단서가 사라짐
    ///   S         : 선택한 사건을 InProgress로 (StartCase)
    ///   T / M / F : 선택한 사건에 Truth / Mercy / AcceptFake 판결
    ///   G         : 갑리 처리 여부 토글 (제1사건 전용)
    ///   C         : 대상 사건에 샘플 단서 기록
    ///   R         : 전체 초기화(상태 + 수첩)
    ///   F1        : 이 디버그 오버레이 표시/숨김
    /// </summary>
    public class GameStateDebugTester : MonoBehaviour
    {
        [Tooltip("체크하면 화면에 상태 오버레이(OnGUI)를 표시")]
        [SerializeField] private bool _showOverlay = true;

        // 에디터 컴파일에선 이 필드가 아래 #else에서만 읽혀 '미사용' 경고가 나므로 억제(필드는 빌드에서 사용)
#pragma warning disable 0414
        [Tooltip("빌드에서도 디버그 입력을 허용할지. 기본은 에디터/개발빌드에서만 동작")]
        [SerializeField] private bool _allowInBuild = false;
#pragma warning restore 0414

        // 현재 키 입력의 대상이 되는 사건(1,2,3으로 전환)
        private CaseId _target = CaseId.Case1_Onggojip;

        // 샘플 단서 번호(C키로 증가)
        private int _clueCounter;

        private bool Enabled
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return _allowInBuild || Debug.isDebugBuild;
#endif
            }
        }

        private void Update()
        {
            if (!Enabled) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return; // 키보드가 없으면(헤드셋 단독) 무시

            // 대상 사건 선택 + 그 사건에 "진입" 시뮬(수첩이 이 사건 단서를 보여줌)
            if (kb.digit1Key.wasPressedThisFrame) { _target = CaseId.Case1_Onggojip; GameState.Instance.EnterCase(_target); }
            if (kb.digit2Key.wasPressedThisFrame) { _target = CaseId.Case2_Seocheon; GameState.Instance.EnterCase(_target); }
            if (kb.digit3Key.wasPressedThisFrame) { _target = CaseId.Case3_Gyeonu;   GameState.Instance.EnterCase(_target); }

            // B = 조사청(사건 밖) 시뮬 — 수첩에서 사건 단서가 사라짐
            if (kb.bKey.wasPressedThisFrame) GameState.Instance.ExitToHub();

            // F1 = 이 디버그 오버레이 표시/숨김(수첩 등과 겹칠 때)
            if (kb.f1Key.wasPressedThisFrame) _showOverlay = !_showOverlay;

            // 저장 / 불러오기 / 삭제
            if (kb.f5Key.wasPressedThisFrame) SaveSystem.Save();
            if (kb.f9Key.wasPressedThisFrame) SaveSystem.Load();
            if (kb.f8Key.wasPressedThisFrame) SaveSystem.Delete();

            // 상태/판결 조작
            if (kb.sKey.wasPressedThisFrame) GameState.Instance.StartCase(_target);
            if (kb.tKey.wasPressedThisFrame) GameState.Instance.SetVerdict(_target, Verdict.Truth);
            if (kb.mKey.wasPressedThisFrame) GameState.Instance.SetVerdict(_target, Verdict.Mercy);
            if (kb.fKey.wasPressedThisFrame) GameState.Instance.SetVerdict(_target, Verdict.AcceptFake);

            // 제1사건 전용 갑리 토글
            if (kb.gKey.wasPressedThisFrame) GameState.Instance.SetGapriHandled(!GameState.Instance.GapriHandled);

            // 수첩에 샘플 단서 기록(대상 사건에)
            if (kb.cKey.wasPressedThisFrame)
            {
                _clueCounter++;
                Journal.Instance.AddClue(_target, $"sample-{_target}-{_clueCounter}", $"샘플 단서 #{_clueCounter}");
            }

            // 전체 초기화(상태 + 수첩)
            if (kb.rKey.wasPressedThisFrame)
            {
                GameState.Instance.ResetAll();
                Journal.Instance.ClearAll();
            }
#endif
        }

        private void OnGUI()
        {
            if (!Enabled) return;

            // 스타일은 최초 1회만 생성(매 프레임 new 하면 낭비 + GUI 이벤트에 취약).
            if (_overlayStyle == null)
                _overlayStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };

            // 숨김 상태에서는 여는 법만 작게 표시
            if (!_showOverlay)
            {
                GUI.Label(new Rect(12, 12, 240, 24), "F1 : 디버그 표시", _overlayStyle);
                return;
            }

            var gs = GameState.Instance;

            // 배경 박스 + 위치 지정 방식(GUILayout 대신 GUI.Label)으로 그린다.
            // GUILayout은 Layout/Repaint 이벤트가 어긋나면 "Mismatched LayoutGroup" 에러를
            // 뿜기 쉬워서, 좌표를 직접 주는 GUI.Label로 안전하게 그린다.
            GUI.Box(new Rect(12, 12, 500, 410), GUIContent.none);

            float x = 24f;
            float y = 22f;
            const float lh = 24f; // line height

            void Line(string text) { GUI.Label(new Rect(x, y, 456, lh), text, _overlayStyle); y += lh; }

            Line("<b>[GameState 디버그]</b>");
            Line($"대상 사건(1/2/3): <b>{_target}</b>");
            Line($"현재 사건 : <b>{(gs.InCase ? gs.CurrentCase.ToString() : "조사청(사건 밖)")}</b>");
            y += 6f;
            Line($"Case1 : {gs.GetStatus(CaseId.Case1_Onggojip),-11} / {gs.GetVerdict(CaseId.Case1_Onggojip)}");
            Line($"Case2 : {gs.GetStatus(CaseId.Case2_Seocheon),-11} / {gs.GetVerdict(CaseId.Case2_Seocheon)}");
            Line($"Case3 : {gs.GetStatus(CaseId.Case3_Gyeonu),-11} / {gs.GetVerdict(CaseId.Case3_Gyeonu)}");
            y += 6f;
            Line($"완료 수 : {gs.CompletedCount} / {gs.CaseCount}   갑리처리:{gs.GapriHandled}");
            Line($"전부완료 : {gs.AllCasesCompleted}");
            var jn = Journal.Instance;
            Line($"수첩 단서 : C1={jn.ClueCount(CaseId.Case1_Onggojip)}  C2={jn.ClueCount(CaseId.Case2_Seocheon)}  C3={jn.ClueCount(CaseId.Case3_Gyeonu)}  (합 {jn.TotalClueCount})");
            y += 6f;
            Line("<size=12>1/2/3:사건진입  B:조사청  S:시작  T/M/F:판결  G:갑리  C:단서  R:리셋</size>");
            Line("<size=12>F1:숨김  F5:저장  F9:불러오기  F8:저장삭제</size>");
        }

        // OnGUI에서 재사용하는 스타일 캐시
        private GUIStyle _overlayStyle;
    }
}
