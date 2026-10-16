using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// UI 모드를 지켜보는 작은 파수꾼 (2026-08-26).
    ///
    /// ■ 하는 일
    ///   <b>F8</b> 로 자동 ▸ PC고정 ▸ VR고정 을 돌린다.
    ///
    /// ■ 2026-09-07 — 헤드셋을 지켜보던 일을 걷었다 (이문록 쪽 요청)
    ///   원래는 0.5초마다 헤드셋이 붙었나 물어보고, 뜰 때마다 그 결과를 콘솔에 찍었다:
    ///     「[UI 모드] 시작 — … 헤드셋 감지 = False」
    ///   이 프로젝트에는 XR 공급자가 <b>깔려 있지 않으므로</b> 그 물음의 답은 늘 False 다.
    ///   0.5초마다 늘 같은 답을 받아 오고, 매 실행마다 그 말을 찍는다 — 값도 값이지만
    ///   헤드셋을 다 걷어낸 판에서 콘솔에 「헤드셋」이 찍히는 것이 사람을 헷갈리게 한다.
    ///   그래서 <b>물음과 로그만</b> 걷는다. F8 은 그대로다 — 그것은 헤드셋이 아니라
    ///   <b>배치</b>를 고르는 스위치이고, 모니터에서 VR 배치를 미리 보는 유일한 길이다.
    ///   (리그가 붙는 날에는 이 두 줄을 도로 살리면 된다.)
    ///      헤드셋이 없어도 <b>모니터에서 VR 배치를 그대로 볼 수 있는 유일한 길</b>이라
    ///      디버그 스위치가 아니라 정식 기능으로 둔다.
    ///
    /// ■ 왜 씬에 두지 않고 스스로 뜨는가
    ///   씬은 일곱이고 배경 배치는 건드리지 않기로 했다. 씬 파일을 하나도 고치지 않으려고
    ///   <see cref="RuntimeInitializeOnLoadMethod"/> 로 자기 오브젝트를 만들어
    ///   DontDestroyOnLoad에 얹는다 — 어느 씬에서 Play를 시작해도 붙는다
    ///   (<see cref="GyeonuDebugWindow"/> 와 같은 방식).
    /// </summary>
    [AddComponentMenu("")]
    public class UiModeWatcher : MonoBehaviour
    {
        public const Key ToggleKey = Key.F8;

        static UiModeWatcher _inst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _inst = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            if (_inst != null) return;
            var go = new GameObject("[UI_모드]");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<UiModeWatcher>();
            UiModes.Refresh();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[ToggleKey].wasPressedThisFrame) UiModes.Cycle();

            // 헤드셋이 붙었나를 0.5초마다 묻던 자리다. 물을 것이 없어졌다 — 위 참조.
        }
    }
}
