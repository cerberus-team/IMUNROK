using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>어느 씬에서든 화면 UI 가 손을 받게 한다.</b>
    ///
    /// 조사청에서 자막의 「✕ 닫기」가 안 눌렸다. 단추는 멀쩡했다 — 활성이고,
    /// <c>interactable</c> 이고, 그 자리를 짚으면 레이캐스트에 <b>닫기</b>가 맨 먼저
    /// 맞았고, <c>onClick</c> 을 직접 부르면 자막이 닫혔다. 그런데 눌러도 아무 일이 없었다.
    ///
    /// 까닭은 <b>씬에 <see cref="EventSystem"/> 이 하나도 없었다</b>는 것이다.
    /// 그것이 없으면 눌림을 UI 로 나르는 것이 없어 <b>모든 단추가 통째로 죽는다</b> —
    /// 어느 한 단추가 잘못된 것이 아니라 화면 전체가 손을 못 받는 상태다.
    /// 그런데 화면에는 단추가 멀쩡히 그려져 있으므로, 원인을 단추에서 찾게 된다.
    ///
    /// <b>같은 일이 전에도 있었다</b>(27e040d — 「조사청에 EventSystem 이 없어 단추가
    /// 통째로 죽어 있었다」). 씬마다 손으로 챙기는 물건은 <b>반드시 한 씬에서 빠진다</b>.
    /// 씬을 새로 짜거나 통째로 다시 지으면 그때 사라지고, 사라진 것은 눈에 안 띈다.
    ///
    /// 그래서 씬에 두지 않고 게임이 보장한다. 이미 있으면 아무 일도 하지 않는다 —
    /// 두 개가 서 있으면 유니티가 경고를 내고 하나는 저절로 꺼진다.
    /// </summary>
    public static class UiEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene sc,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Ensure();
        }

        /// <summary>없으면 세운다. 있으면 손대지 않는다.</summary>
        public static void Ensure()
        {
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Exclude) != null) return;

            var go = new GameObject("_UI이벤트");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            // 이 프로젝트는 입력을 Input System 으로 넘겨 놓았다. 낡은
            // StandaloneInputModule 을 붙이면 「옛 입력이 꺼져 있다」며 예외가 난다.
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            DevLog.Note("[UI] 이 씬에 EventSystem 이 없어 새로 세웠습니다 — 없으면 단추가 통째로 죽습니다.", go);
        }
    }
}
