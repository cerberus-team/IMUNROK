using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>소리계를 켜고 끄는 이음쇠.</b>
    ///
    /// <see cref="NoiseMeter"/> 는 씬에 없는 부품이다 — 게임이 시작되면 스스로 붙는다.
    /// 그래서 인스펙터의 UnityEvent 로는 끌어다 걸 수가 없다(없는 것을 끌 수는 없다).
    /// 이 작은 부품이 씬에 앉아 대신 불러 준다.
    ///
    /// 어디에 거나: 복동이 방을 <b>다 나간 순간</b>(BokdongController 의 나감 이벤트).
    /// 그때부터가 본격 조사이고, 조심해야 하는 때다. 그 전까지는 마주 앉아 말을 하는
    /// 중이라 소리를 잴 일이 없고, 마이크도 심문 쪽이 쥐고 있어야 한다.
    /// </summary>
    public class NoiseArmer : MonoBehaviour
    {
        [Tooltip("이 오브젝트가 켜질 때 저절로 켠다. 이벤트로만 켜려면 꺼 둔다")]
        [SerializeField] private bool _armOnEnable = false;

        private void OnEnable() { if (_armOnEnable) Arm(); }

        /// <summary>이제부터 잰다.</summary>
        public void Arm() => NoiseMeter.Arm();

        /// <summary>그만 잰다. 마이크도 놓는다.</summary>
        public void Disarm() => NoiseMeter.Disarm();
    }
}
