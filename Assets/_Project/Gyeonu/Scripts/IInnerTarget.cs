namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// **가구 안에 있어 차단 박스에 늘 가리는** 조사 대상 (2026-08-24).
    ///
    /// 가구의 조준 판정은 몸통을 통째로 감싼 차단 콜라이더가 받는다. 그래서 궤·찬장 속에 든 것은
    /// 열려 있어도 조준되지 않는다. <see cref="DebugInteractor"/> 는 원래 <see cref="ItemPickup"/>
    /// 하나만 예외로 통과시켰는데, 집는 것이 아니라 **조작하는 것**(선반 위 장부)도 같은 사정이라
    /// 표식을 인터페이스로 뽑았다.
    ///
    /// ⚠️ 아무 Interactable이나 통과시키면 안 된다 — 열린 문 너머 엉뚱한 것이 조준되고
    ///    가구를 다시 닫을 방법이 사라진다. 이 표식을 단 것만 통과한다.
    /// </summary>
    public interface IInnerTarget
    {
    }
}
