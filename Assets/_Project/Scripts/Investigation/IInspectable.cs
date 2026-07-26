namespace IMUNROK.Common
{
    /// <summary>
    /// 확대경(등 조사 도구)으로 "살펴볼 수 있는" 대상이 구현하는 계약.
    ///
    /// 공통 파트는 "살펴보면 정보를 보여준다"는 메커니즘(틀)만 제공하고,
    /// 무엇이 보이는지(내용)는 각 사건 팀원이 채운다.
    ///  - 직접 구현하거나(자유),
    ///  - 코드 없이 쓰려면 InspectableNote 컴포넌트를 오브젝트에 붙이면 된다.
    ///
    /// 지금은 마우스(MouseInspector)가 살펴보지만, VR에서는 확대경을 든 손이
    /// 같은 계약을 호출하게 된다(입력만 교체, 대상 로직은 그대로).
    /// </summary>
    public interface IInspectable
    {
        /// <summary>살펴봤을 때 보여줄 제목.</summary>
        string GetInspectTitle();

        /// <summary>살펴봤을 때 보여줄 본문(관찰 내용).</summary>
        string GetInspectBody();

        /// <summary>처음 살펴본 순간 1회 호출(단서 자동 기록 등에 사용).</summary>
        void OnInspected();
    }
}
