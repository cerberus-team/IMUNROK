namespace IMUNROK.Common
{
    /// <summary>
    /// <b>밤의 목숨 셋</b> — 소리를 내서 하인을 불러들이고, 그가 왔을 때 깨어 있다 들키면 하나를 잃는다.
    ///
    /// 여태 들킨 횟수는 <see cref="ServantCheck"/> 와 <see cref="NoiseWarning"/> 가 저마다 속으로
    /// 셌다. 셋이 차면 붙들려 가는데, 몇 번 남았는지는 <b>아무 데도 안 보였다</b> — 조심할
    /// 까닭이 눈에 없으니 소리를 두려워할 까닭도 없었다. 셈을 한 곳으로 모으고 화면에 박는다
    /// (<see cref="NoiseMeter"/> 의 눈금 밑 「목숨 ♥ ♥ ♥」).
    ///
    /// <b>잃는 때는 하나다</b>: 하인이 문 앞에 닿았는데 요에 누워 있지 않을 때.
    /// 불러들인 것만으로는 안 잃는다 — 발소리를 듣고 요로 뛰어가 숨을 죽이면 산다.
    /// 그래야 「오고 있다」는 경고가 도망칠 틈이 되고, 요가 쓸모를 갖는다.
    /// </summary>
    public static class StealthLives
    {
        public const int Max = 3;

        /// <summary>잃은 목숨.</summary>
        public static int Lost { get; private set; }

        /// <summary>남은 목숨.</summary>
        public static int Left => Max - Lost;

        /// <summary>잃을 때마다 한 번.</summary>
        public static event System.Action Changed;

        /// <summary>밤을 새로 시작한다 — 1막에 들어설 때. 여태 재생을 켤 때만 0이 되어, 다시 들어오면 하트가 빈 채였다.</summary>
        public static void ResetForNight() { Lost = 0; Changed?.Invoke(); }

        public static void Lose()
        {
            if (Lost >= Max) return;
            Lost++;
            Changed?.Invoke();
        }

        // 도메인 리로드가 꺼져 있어도 판마다 새로 센다.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() { Lost = 0; Changed = null; }
    }
}
