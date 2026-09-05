namespace IMUNROK.Common
{
    /// <summary>
    /// <b>지금은 찍는 중이다</b>를 켜 두는 스위치 하나.
    ///
    /// 씬을 손보는 것만으로는 조용해지지 않는 것들이 있다. <see cref="NoiseMeter"/> 는
    /// 씬에 놓여 있지 않고 <c>RuntimeInitializeOnLoadMethod</c> 로 <b>제 발로 선다</b> —
    /// 그러니 편집 모드에서 재우려 해도 재울 것이 없다.
    /// 실제로 그래서 [영상 채비] 가 소리계를 재웠다고 적어 놓고도 재생하면
    /// 마이크가 열렸다.
    ///
    /// 그 둘이 재생 중에 하는 일이 <b>영상을 망친다</b>:
    ///   · VR 몸짓기는 링크를 3초마다 두드리는데, 링크가 꺼져 있으면 오큘러스가
    ///     실패를 돌려주기까지 <b>5초 넘게 주 실을 붙잡는다</b>. 8초마다 5초씩
    ///     화면이 얼어붙는다 — 봉서가 굴러오는 참에 걸리면 굴러오다 멈춘다.
    ///   · 소리계는 마이크를 여는데, 그것도 한 번은 걸린다.
    ///
    /// 그래서 <b>런타임이 읽을 수 있는 자리</b>에 켜 둔다. 에디터 안에서만 살면
    /// 되므로 EditorPrefs 에 둔다 — 빌드에는 아예 코드가 안 들어가고,
    /// 켜 둔 채로 빌드해도 켜지지 않는다.
    /// </summary>
    public static class ShotMode
    {
        private const string Key = "이문록.촬영중";

        /// <summary>찍는 중인가. 빌드에서는 언제나 거짓이다.</summary>
        public static bool On
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(Key, false);
#else
                return false;
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetBool(Key, value);
#endif
            }
        }
    }
}
