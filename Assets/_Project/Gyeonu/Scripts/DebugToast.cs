using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 화면 안내 문구 (2026-08-15, 2026-08-26 월드 UI로 교체, 2026-09-11 자리 통일).
    ///   - Show("문구", 4f): 몇 초 떴다 사라진다.
    ///   - ShowPinned("문구"): 사라지지 않고 남는다 — 플레이어가 <b>이동하기 시작하면</b> 사라진다
    ///     (2026-08-15, "빛이 필요하다" 안내용). 떠 있는 동안 DebugFocusRig가 조작 힌트를
    ///     그리지 않는다 (자리 교대).
    ///
    /// ■ 2026-08-26 — IMGUI를 버렸다
    ///   <c>OnGUI</c> 는 HMD에 아예 보이지 않는다. 그리는 일은 <see cref="ToastPanel"/>
    ///   (월드 스페이스 캔버스)이 맡고, 이 클래스는 <b>무엇을 언제 띄울지</b>만 안다.
    ///
    /// ■ 2026-09-11 — <b>자리를 하나로 모았다</b>
    ///   예전에는 <see cref="Show"/> 가 화면 <b>위</b>(높이 22%), <see cref="ShowPinned"/> 가
    ///   <b>아래</b>(바닥 34px)에 떴다. 같은 성격의 안내가 부르는 쪽에 따라 다른 높이에 떠서
    ///   눈이 글을 찾아 헤맸다. 이제 판은 하나뿐이고 <b>전부 아래</b>에 뜬다.
    ///   (대화창은 이 길을 쓰지 않는다 — <see cref="DialogueUI"/> 가 제 자리를 지킨다)
    ///
    ///   두 통로는 남는다. 성격이 다르기 때문이다 — 하나는 시간이 지우고, 하나는 발걸음이 지운다.
    ///   같은 자리를 쓰므로 <b>나중에 요청한 쪽이 보인다</b>. 시간제한 문구가 끝나면 아직 살아 있는
    ///   고정 문구가 다시 드러난다 (여관 확인 안내처럼 "잠깐 알리고 원래 안내로 돌아가는" 흐름).
    ///
    ///   <b>정적 API는 그대로다</b> — 부르는 곳 21개 파일 43군데를 한 줄도 고치지 않았다.
    /// </summary>
    public class DebugToast : MonoBehaviour
    {
        static DebugToast inst;
        static string pinnedMsg;
        static string timedMsg;
        static float timedUntil;
        static bool timedOnTop;          // 시간제한 문구가 고정 문구보다 나중에 왔는가

        Vector3 moveAnchor;
        bool anchorSet;
        DebugWalkController walk;
        ToastPanel panel;

        // 도메인 리로드가 꺼진 프로젝트 — static이 플레이 세션을 넘겨 살아남으므로 직접 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            inst = null; pinnedMsg = null; timedMsg = null; timedUntil = 0f; timedOnTop = false;
        }

        /// <summary>고정 문구가 떠 있는가 — 여관 확인처럼 "안내가 살아 있는 동안만"을 보는 곳이 쓴다.</summary>
        public static bool PinnedActive => !string.IsNullOrEmpty(pinnedMsg);

        /// <summary>지금 안내가 하나라도 화면에 있는가 — 조작 힌트가 자리를 비켜 줄지 볼 때 쓴다.</summary>
        public static bool Active => Current != null;

        /// <summary>안내 상자의 지금 높이(px). 없으면 0 — 같은 아래쪽을 쓰는 판이 이만큼 비켜선다.</summary>
        public static float PanelHeight => inst != null && inst.panel != null ? inst.panel.Height : 0f;

        /// <summary>지금 화면에 적혀야 할 것. 나중에 요청한 쪽이 이긴다.</summary>
        static string Current
        {
            get
            {
                bool timed = !string.IsNullOrEmpty(timedMsg) && Time.time < timedUntil;
                if (timed && (timedOnTop || !PinnedActive)) return timedMsg;
                if (PinnedActive) return pinnedMsg;
                return timed ? timedMsg : null;
            }
        }

        public static void Show(string message, float duration)
        {
            Ensure();
            timedMsg = message;
            timedUntil = Time.time + duration;
            timedOnTop = true;
        }

        /// <summary>플레이어가 움직이기 시작하면 사라지는 안내.</summary>
        public static void ShowPinned(string message)
        {
            Ensure();
            pinnedMsg = message;
            timedOnTop = false;
            if (inst != null) inst.anchorSet = false;
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
            panel = ToastPanel.Create(transform);
        }

        // 씬을 떠날 때 적혀 있던 글을 지운다. 안내 판은 암전보다 위에 그리므로(ToastPanel)
        // 지우지 않으면 캄캄한 전환 화면에 앞 씬의 안내가 떠 있다. 새 씬으로 따라가지도 않게 된다.
        void OnEnable() { SceneTransition.Departing += OnDeparting; }
        void OnDisable() { SceneTransition.Departing -= OnDeparting; }
        void OnDeparting(string _) { pinnedMsg = null; timedMsg = null; timedUntil = 0f; }

        void LateUpdate()
        {
            // 판에 지금 무엇이 적혀야 하는지 넘겨 준다 (그리는 일은 판이 한다)
            if (panel != null) panel.SetMessage(Current);

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
