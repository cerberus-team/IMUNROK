namespace IMUNROK.Common
{
    /// <summary>
    /// 레이로 가리켜 선택할 수 있는 대상(사건 큐브, 봉서함 등)이 구현하는 인터페이스.
    ///
    /// 지금은 에디터에서 마우스 레이(MouseRaySelector)가 호출하지만,
    /// 짚는 방식이 바뀌어도 이 세 메서드만 부르면 되도록
    /// 선택 로직과 입력 방식을 분리한다.
    /// </summary>
    public interface ISelectable
    {
        /// <summary>레이가 이 대상을 가리키기 시작할 때(하이라이트 켜기).</summary>
        void OnHoverEnter();

        /// <summary>레이가 이 대상에서 벗어날 때(하이라이트 끄기).</summary>
        void OnHoverExit();

        /// <summary>트리거/클릭으로 실제 선택했을 때.</summary>
        void OnSelect();
    }

    /// <summary>
    /// <b>눌러 잡고 있어야</b> 되는 것. 한 번 톡 누르면 열리는 것과 다르다.
    ///
    /// 재를 헤집고 보료를 들추고 서랍을 빼는 일은 손에 힘이 들어가는 짓이다. 스쳐 지나가며
    /// 한 번 누른 것으로 증거가 손에 들어오면 조사한 것이 아니라 주운 것이 된다.
    /// 잡고 있는 동안 물건이 실제로 움직이므로, 진행 막대 같은 것을 따로 그릴 필요가 없다 —
    /// 들려 올라가는 보료가 곧 진행 막대다.
    ///
    /// 이것을 구현한 대상에게는 <see cref="ISelectable.OnSelect"/> 를 부르지 않는다.
    /// 짚는 방식을 갈아 끼울 때도 같다 — 누르고 있는 동안 <see cref="OnHoldTick"/> 를 부른다.
    /// </summary>
    public interface IHoldable
    {
        /// <summary>잡고 있는 동안 매 프레임.</summary>
        void OnHoldTick(float deltaTime);

        /// <summary>다 채우기 전에 놓았을 때. 물건은 제자리로 돌아간다.</summary>
        void OnHoldRelease();

        /// <summary>
        /// <b>아직 잡을 일이 남았나.</b> 다 들춰 버린 뒤에는 거짓을 돌려준다.
        ///
        /// 왜 필요한가: 이것을 구현했다는 이유만으로 <see cref="ISelectable.OnSelect"/> 가
        /// 영영 안 불렸다. 그래서 한 번 들춘 보료를 <b>도로 내려놓을 길이 없었다</b> —
        /// 내려놓는 코드가 바로 그 OnSelect 안에 있는데, 누르면 잡기로 가로채이고
        /// 잡기는 "이미 들췄다"며 곧장 돌아 나왔다. 아무 일도 안 일어난다.
        /// 잡을 일이 끝나면 손을 놓아, 누름이 <b>톡 누르기</b>로 흘러가게 한다.
        /// </summary>
        bool HoldReady { get; }
    }
}
