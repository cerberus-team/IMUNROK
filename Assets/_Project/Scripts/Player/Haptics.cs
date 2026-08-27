using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>손을 울린다.</b>
    ///
    /// 문을 두드리는 것은 <b>소리보다 손에 먼저 온다</b>. 실제로 문을 치면 손마디에
    /// 울림이 돌아오고, 그 울림이 "내가 쳤다"는 것을 귀보다 빨리 알려 준다. 헤드셋을
    /// 쓰고 트리거만 당기면 소리는 나는데 손은 가만히 있어, 문을 친 것이 아니라
    /// <b>문 소리를 튼</b> 느낌이 든다.
    ///
    /// <b>박자는 소리와 같이 간다.</b> 두드리는 소리(<c>문_두드림_셋</c>)가 0 · 0.24 ·
    /// 0.50초에 세 번 치게 만들어져 있으니, 울림도 <b>그 자리에서</b> 세 번 온다.
    /// 소리는 소리대로 손은 손대로 놀면 두 번 두드린 것처럼 들린다.
    ///
    /// <b>친 손만 울린다.</b> 양손을 다 울리면 어느 손으로 쳤는지 몸이 헷갈린다 —
    /// 마지막으로 방아쇠를 당긴 손을 <see cref="VRRaySelector"/> 가 적어 두고, 여기서
    /// 그 손을 골라 울린다.
    ///
    /// 헤드셋이 없으면 아무 일도 안 한다. 책상에서 눌러 봐도 탈나지 않는다.
    /// </summary>
    public class Haptics : MonoBehaviour
    {
        public static Haptics Instance { get; private set; }

        /// <summary>두드리는 소리의 박자 — 문_두드림_셋 과 같은 자리다.</summary>
        private static readonly float[] KnockBeats = { 0f, 0.24f, 0.50f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            if (Instance != null) return;
            var go = new GameObject("_손울림");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<Haptics>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary><b>쿵 · 쿵 · 쿵</b> — 문을 세 번 두드린 손맛.</summary>
        public static void Knock()
        {
            if (Instance == null) Spawn();
            if (Instance != null) Instance.StartCoroutine(Instance.Beat(KnockBeats, 0.85f, 0.075f));
        }

        /// <summary>한 번만 울린다. 집고 놓고 봉하는 데 쓴다.</summary>
        public static void Tap(float strength = 0.45f, float seconds = 0.045f)
        {
            if (Instance == null) Spawn();
            if (Instance != null) Instance.Pulse(VRRaySelector.LastHand, strength, seconds);
        }

        /// <summary>
        /// <b>이 손을</b> 울린다. 손으로 직접 친 것은 방아쇠를 당긴 것이 아니라서,
        /// 마지막에 당긴 손을 고르면 <b>엉뚱한 손</b>이 울린다 — 오른손으로 문을 쳤는데
        /// 왼손이 떨리면 몸이 어리둥절해진다. 친 쪽이 스스로 일러 준다.
        /// </summary>
        public static void TapOn(XRNode hand, float strength = 0.45f, float seconds = 0.045f)
        {
            if (Instance == null) Spawn();
            if (Instance != null) Instance.Pulse(hand, strength, seconds);
        }

        private IEnumerator Beat(float[] beats, float strength, float seconds)
        {
            float t = 0f;
            for (int i = 0; i < beats.Length; i++)
            {
                float wait = beats[i] - t;
                if (wait > 0f) yield return new WaitForSeconds(wait);
                t = beats[i];
                Pulse(VRRaySelector.LastHand, strength, seconds);
            }
        }

        private void Pulse(XRNode node, float strength, float seconds)
        {
            var dev = InputDevices.GetDeviceAtXRNode(node);
            if (!dev.isValid) return;
            // 이 컨트롤러가 울릴 줄 아는지 먼저 묻는다. 못 하는 기기에 밀어 넣으면
            // 조용히 아무 일도 안 나는 게 아니라 예외가 난다.
            if (!dev.TryGetHapticCapabilities(out HapticCapabilities cap) || !cap.supportsImpulse) return;
            dev.SendHapticImpulse(0u, Mathf.Clamp01(strength), Mathf.Max(0.01f, seconds));
        }
    }
}
