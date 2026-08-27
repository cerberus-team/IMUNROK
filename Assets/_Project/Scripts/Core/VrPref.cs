namespace IMUNROK.Common
{
    /// <summary>
    /// <b>이 자리에서는 헤드셋을 안 쓴다</b>를 켜 두는 스위치.
    ///
    /// 링크가 꺼져 있을 때 XR 을 올려 보는 일은 공짜가 아니다 — 오큘러스 런타임은
    /// "없다"고 대답하기까지 <b>5초 넘게 주 실을 붙잡는다</b>. 책상에서 화면으로만
    /// 만지는 동안에는 그 5초가 재생할 때마다 두 번씩 그냥 버려진다.
    ///
    /// <see cref="ShotMode"/> 와 나눠 둔 까닭: 저것은 <b>한 장면 찍는 동안</b>만
    /// 켰다 끄는 것이고, 이것은 <b>당분간 헤드셋 안 쓴다</b>는 사람의 형편이다.
    /// 하나로 묶으면 영상 되돌리기가 남의 설정을 도로 켜 버린다.
    ///
    /// 켜고 끄는 곳: [이문록 ▸ VR ▸ 헤드셋 쓰기] (체크 표시가 곧 지금 값)
    /// 에디터에서만 산다 — 빌드에는 코드째 안 들어가고, 켜 둔 채 빌드해도 켜지지 않는다.
    /// </summary>
    public static class VrPref
    {
        private const string Key = "이문록.헤드셋안씀";

        /// <summary>헤드셋을 안 쓰나(= 링크를 두드리지 않나). 빌드에서는 언제나 거짓.</summary>
        public static bool HeadsetOff
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
