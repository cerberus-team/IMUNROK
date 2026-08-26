namespace IMUNROK.Common
{
    /// <summary>
    /// <b>마이크를 지금 누가 쥐고 있나</b> — 임자를 가리는 걸쇠 하나.
    ///
    /// 이 게임에서 마이크를 원하는 데가 셋이다:
    ///   · <see cref="NoiseMeter"/> — 잠행 중 <b>내가 낸 소리</b>를 잰다. 늘 쥐고 있다.
    ///   · <see cref="InterrogationController"/> — 심문에서 <b>내가 하는 말</b>을 알아듣는다.
    ///   · UI 꾸러미의 대화창(IMUNROK.Ui) — 「누르고 말하기」로 받아쓴다.
    ///
    /// 윈도에서 <c>Microphone.Start</c> 는 <b>같은 장치를 다시 열면 앞의 것을 끊는다</b>.
    /// 소리계가 쥔 채로 다른 쪽이 열면 소리계의 눈금이 <b>오류 한 줄 없이 죽는다</b> —
    /// 잠행 중에 소리를 아무리 내도 아무도 안 오게 된다.
    ///
    /// 소리계는 심문창이 열렸는지는 이미 보고 있었다(<c>InterrogationController.AnyOpen</c>).
    /// 그런데 꾸러미의 대화창은 <b>다른 어셈블리</b>라 그 조건에 안 걸린다. 그렇다고
    /// 소리계가 꾸러미를 알게 하면 <b>핵심이 UI 꾸러미에 매인다</b> — 갈아 끼울 수 있게
    /// 인터페이스 뒤로 물려 둔 것을 도로 끌어오는 셈이다.
    ///
    /// 그래서 <b>이름 없는 걸쇠</b>를 둔다. 마이크를 쓸 쪽이 올리고, 놓을 때 내린다.
    /// 소리계는 누가 올렸는지 모른 채 <b>비켜 준다</b>.
    ///
    /// <code>
    ///   MicHold.Claim();     // 녹음 시작
    ///   …
    ///   MicHold.Release();   // 녹음 끝
    /// </code>
    ///
    /// <b>세는 것이지 켜고 끄는 것이 아니다.</b> 둘이 겹쳐 쥐었다 하나가 놓을 때
    /// 참·거짓이면 나머지 하나가 쥔 채로 걸쇠가 내려간다.
    /// </summary>
    public static class MicHold
    {
        private static int _claims;

        /// <summary>지금 누군가 쥐고 있나.</summary>
        public static bool Held { get { return _claims > 0; } }

        /// <summary>몇이 쥐고 있나. 어긋난 것을 잡을 때 본다.</summary>
        public static int Claims { get { return _claims; } }

        /// <summary>쥔다.</summary>
        public static void Claim() { _claims++; }

        /// <summary>놓는다. 안 쥐고 놓아도 밑으로 안 내려간다.</summary>
        public static void Release() { if (_claims > 0) _claims--; }

        /// <summary>
        /// 다 놓는다. <b>판이 새로 시작될 때 스스로 부른다</b> — 정적 값은 도메인
        /// 리로드가 꺼진 프로젝트에서 <b>플레이 세션을 넘겨 살아남는다</b>. 쥔 채로
        /// 재생이 끝나면 다음 판에서 소리계가 영영 비켜선 채로 있게 된다.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() { _claims = 0; }
    }
}
