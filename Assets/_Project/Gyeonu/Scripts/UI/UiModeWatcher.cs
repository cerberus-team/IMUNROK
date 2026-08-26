using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// UI 모드를 지켜보는 작은 파수꾼 (2026-08-26).
    ///
    /// ■ 하는 일 둘
    ///   ① 헤드셋이 붙거나 빠지는지 주기적으로 본다(0.5초마다) — <see cref="UiModes.Preference.자동"/> 용.
    ///   ② <b>F8</b> 로 자동 ▸ PC고정 ▸ VR고정 을 돌린다.
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
        const float PollInterval = 0.5f;

        static UiModeWatcher _inst;
        float nextPoll;

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
            Debug.Log("[UI 모드] 시작 — 설정 " + UiModes.Pref + " / 지금 " + UiModes.Current
                    + " (F8로 전환, 헤드셋 감지 = " + UiModes.HeadsetPresent + ")");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[ToggleKey].wasPressedThisFrame) UiModes.Cycle();

            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + PollInterval;
            UiModes.Refresh();
        }
    }
}
