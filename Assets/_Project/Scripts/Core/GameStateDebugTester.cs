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
    ///   1 / 2 / 3 : 대상 사건 선택 (Case1 / Case2 / Case3)
    ///   S         : 선택한 사건을 InProgress로 (StartCase)
    ///   T         : 선택한 사건에 Truth      판결
    ///   M         : 선택한 사건에 Mercy      판결
    ///   F         : 선택한 사건에 AcceptFake 판결
    ///   G         : 갑리 처리 여부 토글 (제1사건 전용)
    ///   R         : 전체 초기화
    /// </summary>
    public class GameStateDebugTester : MonoBehaviour
    {
        [Tooltip("체크하면 화면에 상태 오버레이(OnGUI)를 표시")]
        [SerializeField] private bool _showOverlay = true;

        [Tooltip("빌드에서도 디버그 입력을 허용할지. 기본은 에디터/개발빌드에서만 동작")]
        [SerializeField] private bool _allowInBuild = false;

        // 현재 키 입력의 대상이 되는 사건(1,2,3으로 전환)
        private CaseId _target = CaseId.Case1_Onggojip;

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

            // 대상 사건 선택
            if (kb.digit1Key.wasPressedThisFrame) { _target = CaseId.Case1_Onggojip; Log("대상 → Case1"); }
            if (kb.digit2Key.wasPressedThisFrame) { _target = CaseId.Case2_Gyeonu;   Log("대상 → Case2"); }
            if (kb.digit3Key.wasPressedThisFrame) { _target = CaseId.Case3_Seocheon; Log("대상 → Case3"); }

            // 상태/판결 조작
            if (kb.sKey.wasPressedThisFrame) GameState.Instance.StartCase(_target);
            if (kb.tKey.wasPressedThisFrame) GameState.Instance.SetVerdict(_target, Verdict.Truth);
            if (kb.mKey.wasPressedThisFrame) GameState.Instance.SetVerdict(_target, Verdict.Mercy);
            if (kb.fKey.wasPressedThisFrame) GameState.Instance.SetVerdict(_target, Verdict.AcceptFake);

            // 제1사건 전용 갑리 토글
            if (kb.gKey.wasPressedThisFrame) GameState.Instance.SetGapriHandled(!GameState.Instance.GapriHandled);

            // 전체 초기화
            if (kb.rKey.wasPressedThisFrame) GameState.Instance.ResetAll();
#endif
        }

        private void Log(string msg) => Debug.Log($"[DebugTester] {msg}");

        private void OnGUI()
        {
            if (!_showOverlay || !Enabled) return;

            var gs = GameState.Instance;

            // 스타일은 최초 1회만 생성(매 프레임 new 하면 낭비 + GUI 이벤트에 취약).
            if (_overlayStyle == null)
                _overlayStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };

            // 배경 박스 + 위치 지정 방식(GUILayout 대신 GUI.Label)으로 그린다.
            // GUILayout은 Layout/Repaint 이벤트가 어긋나면 "Mismatched LayoutGroup" 에러를
            // 뿜기 쉬워서, 좌표를 직접 주는 GUI.Label로 안전하게 그린다.
            GUI.Box(new Rect(12, 12, 460, 300), GUIContent.none);

            float x = 24f;
            float y = 22f;
            const float lh = 24f; // line height

            void Line(string text) { GUI.Label(new Rect(x, y, 440, lh), text, _overlayStyle); y += lh; }

            Line("<b>[GameState 디버그]</b>");
            Line($"대상 사건(1/2/3): <b>{_target}</b>");
            y += 6f;
            Line($"Case1 : {gs.GetStatus(CaseId.Case1_Onggojip),-11} / {gs.GetVerdict(CaseId.Case1_Onggojip)}");
            Line($"Case2 : {gs.GetStatus(CaseId.Case2_Gyeonu),-11} / {gs.GetVerdict(CaseId.Case2_Gyeonu)}");
            Line($"Case3 : {gs.GetStatus(CaseId.Case3_Seocheon),-11} / {gs.GetVerdict(CaseId.Case3_Seocheon)}");
            y += 6f;
            Line($"완료 수 : {gs.CompletedCount} / {gs.CaseCount}   갑리처리:{gs.GapriHandled}");
            Line($"전부완료 : {gs.AllCasesCompleted}");
            y += 6f;
            Line("<size=12>S:시작  T:Truth  M:Mercy  F:AcceptFake  G:갑리  R:리셋</size>");
        }

        // OnGUI에서 재사용하는 스타일 캐시
        private GUIStyle _overlayStyle;
    }
}
