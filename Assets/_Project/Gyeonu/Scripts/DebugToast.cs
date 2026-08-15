using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 임시 화면 안내 문구 (2026-08-15) — 퍼즐 힌트 등을 띄운다.
    /// DebugInteractor와 같은 디버그 계층의 IMGUI — VR에서는 월드 UI로 교체 예정.
    ///   - Show("문구", 4f): 화면 상단에 몇 초 떴다 사라진다.
    ///   - ShowPinned("문구"): 하단(포커스 조작 힌트 자리)에 고정 —
    ///     플레이어가 이동하기 시작하면 사라진다 (2026-08-15, "빛이 필요하다" 안내용).
    ///     떠 있는 동안 DebugFocusRig가 조작 힌트를 그리지 않는다 (자리 교대).
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

        void Update()
        {
            if (!PinnedActive) return;
            if (walk == null) walk = FindFirstObjectByType<DebugWalkController>();
            if (walk == null) return;
            // 포커스 복귀 보간 중에는 walk가 꺼져 있다 — 조작이 돌아온 뒤부터 기준점을 잡는다
            if (!walk.enabled) { anchorSet = false; return; }
            if (!anchorSet) { moveAnchor = walk.transform.position; anchorSet = true; return; }
            if ((walk.transform.position - moveAnchor).sqrMagnitude > 0.04f) HidePinned();
        }

        void OnGUI()
        {
            if (Time.time < until && !string.IsNullOrEmpty(msg))
            {
                var style = MakeStyle(18);
                float w = Screen.width, y = Screen.height * 0.22f;
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(w * 0.5f - 260f, y - 6f, 520f, 40f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(0f, y, w, 28f), msg, style);
            }
            if (PinnedActive)
            {
                var style = MakeStyle(16);
                float w = Screen.width, y = Screen.height - 46f;
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(w * 0.5f - 260f, y - 6f, 520f, 36f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(0f, y, w, 24f), pinnedMsg, style);
            }
        }

        static GUIStyle MakeStyle(int size)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = new Color(1f, 0.92f, 0.7f);
            return style;
        }
    }
}
