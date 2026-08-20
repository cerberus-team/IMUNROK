namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 여닫히는 물건의 열림 여부를 밖에서 읽기 위한 최소 계약.
    ///
    /// 문 종류(HingeDoor·DoubleHingeDoor·LockedDoor)마다 회전 방식이 달라 공통 베이스로
    /// 묶기는 어렵지만, "지금 열려 있는가"는 셋 다 같은 뜻이다. 씬 전환(DoorSceneExit)처럼
    /// 문의 상태만 알면 되는 쪽이 문 종류를 구별하지 않게 하려고 이 인터페이스만 둔다.
    /// </summary>
    public interface IOpenable
    {
        /// <summary>열림 지시가 내려진 상태인가(애니메이션 완료 여부와는 별개).</summary>
        bool IsOpen { get; }
    }
}
