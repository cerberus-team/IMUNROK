using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 씬에 <b>EventSystem</b> 이 없으면 하나 세운다.
    ///
    /// <b>왜 이것이 필요한가</b>: 유니티에서 UI 단추는 <c>onClick</c> 을 걸어 두는 것만으로는
    /// 눌리지 않는다. 화면의 어느 지점이 어느 단추 위인지 짚어 주는 EventSystem 이 씬에
    /// 하나 있어야 하고, <b>없으면 아무 일도 안 일어나되 오류도 안 난다.</b>
    /// 단추는 그려지고, 손도 얹히고, 눌러도 조용하다.
    ///
    /// 조사청(HubScene)이 딱 그 꼴이었다. 문서의 <b>내려놓기</b>도, 사건판의
    /// <b>물러나기</b>도, 그 씬의 모든 단추가 통째로 죽어 있었다. 옹고집전 본편과
    /// 표제 씬에는 EventSystem 이 들어 있어서 거기서는 멀쩡했고, 그래서 "증거마다
    /// 내려놓기가 안 된다"로 보였다 — 증거의 문제가 아니라 <b>방의 문제</b>였다.
    ///
    /// 씬마다 손으로 놓게 두지 않는다. 씬은 앞으로도 늘어날 것이고, 늘어날 때마다
    /// 잊는 씬이 반드시 하나 생긴다. 게임이 시작될 때 저 혼자 서고 씬을 넘어가도
    /// 살아 있게 한다. 씬에 이미 있으면 그것을 쓴다.
    ///
    /// 입력 모듈은 <b>새 Input System</b> 쪽을 붙인다. 이 프로젝트는 입력을 그쪽으로
    /// 넘겨 놓았으므로, 옛 StandaloneInputModule 을 붙이면 그것대로 조용히 죽는다.
    /// </summary>
    public static class UiEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (EventSystem.current != null) return;
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("_UI이벤트");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            Object.DontDestroyOnLoad(go);
        }
    }
}
