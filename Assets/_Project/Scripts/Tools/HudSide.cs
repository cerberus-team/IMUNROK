namespace IMUNROK.Common
{
    /// <summary>
    /// 왼손/오른손잡이 HUD 배치 공유 설정.
    /// ToolbeltHud가 Awake에서 값을 세팅하면, 수첩·지도 버튼이 같은 값을 읽어 좌우를 맞춘다.
    /// (오른손잡이=false: 수첩 좌하단·지도 우상단 / 왼손잡이=true: 좌우 반전)
    /// </summary>
    public static class HudSide
    {
        public static bool LeftHanded = false;
    }
}
