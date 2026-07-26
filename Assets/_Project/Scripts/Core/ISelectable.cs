namespace IMUNROK.Common
{
    /// <summary>
    /// 레이로 가리켜 선택할 수 있는 대상(사건 큐브, 봉서함 등)이 구현하는 인터페이스.
    ///
    /// 지금은 에디터에서 마우스 레이(MouseRaySelector)가 호출하지만,
    /// VR 단계에서 컨트롤러 레이 인터랙터로 교체돼도 이 세 메서드만 호출하면 되도록
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
}
