using UnityEngine;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 화면 안내 문구 (2026-08-15, 2026-08-26 월드 UI로 교체).
    ///   - Show("문구", 4f): 화면 상단에 몇 초 떴다 사라진다.
    ///   - ShowPinned("문구"): 하단(포커스 조작 힌트 자리)에 고정 —
    ///     플레이어가 이동하기 시작하면 사라진다 (2026-08-15, "빛이 필요하다" 안내용).
    ///     떠 있는 동안 DebugFocusRig가 조작 힌트를 그리지 않는다 (자리 교대).
    ///
    /// ■ 2026-08-26 — IMGUI를 버렸다
    ///   <c>OnGUI</c> 는 HMD에 아예 보이지 않는다. 그리는 일은 <see cref="ToastPanel"/>
    ///   (월드 스페이스 캔버스)이 맡고, 이 클래스는 <b>무엇을 언제 띄울지</b>만 안다.
    ///   <b>정적 API는 그대로다</b> — 부르는 곳 21개 파일 43군데를 한 줄도 고치지 않았다.
    /// </summary>
    public class DebugToast : MonoBehaviour
    {
        static DebugToast inst;
        static string pinnedMsg;
        string msg;
        float until;
        Vector3 moveAnchor;
        bool anchorSet;
        DebugWalkController walk;
        ToastPanel topPanel, pinPanel;

        // 도메인 리로드가 꺼진 프로젝트 — static이 플레이 세션을 넘겨 살아남으므로 직접 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { inst = null; pinnedMsg = null; }

        /// <summary>하단 고정 문구가 떠 있는가 — DebugFocusRig가 조작 힌트 자리를 비켜 준다.</summary>
        public static bool PinnedActive => !string.IsNullOrEmpty(pinnedMsg);

        public static void Show(string message, float duration)
        {
            Ensure();
            inst.msg = message;
            inst.until = Time.time + duration;
        }

        /// <summary>하단 고정 문구 — 플레이어가 움직이기 시작하면 사라진다.</summary>
        public static void ShowPinned(string message)
        {
            Ensure();
            pinnedMsg = message;
            inst.anchorSet = false;
        }

        public static void HidePinned() => pinnedMsg = null;

        static void Ensure()
        {
            if (inst != null) return;
            var go = new GameObject("디버그_토스트");
            inst = go.AddComponent<DebugToast>();
        }

        void Awake()
        {
            topPanel = ToastPanel.Create(transform, false);
            pinPanel = ToastPanel.Create(transform, true);
        }

        void LateUpdate()
        {
            // 판에 지금 무엇이 적혀야 하는지 넘겨 준다 (그리는 일은 판이 한다)
            if (topPanel != null) topPanel.SetMessage(Time.time < until ? msg : null);
            if (pinPanel != null) pinPanel.SetMessage(pinnedMsg);

            if (!PinnedActive) return;
            if (walk == null) walk = FindFirstObjectByType<DebugWalkController>();
            if (walk == null) return;
            // 포커스 복귀 보간 중에는 walk가 꺼져 있다 — 조작이 돌아온 뒤부터 기준점을 잡는다
            if (!walk.enabled) { anchorSet = false; return; }
            if (!anchorSet) { moveAnchor = walk.transform.position; anchorSet = true; return; }
            if ((walk.transform.position - moveAnchor).sqrMagnitude > 0.04f) HidePinned();
        }
    }
}
