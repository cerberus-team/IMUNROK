using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>지금 어디를 짚고 있나</b> — 짚은 자리 한 점을 남겨 두는 곳.
    ///
    /// <see cref="ISelectable.OnSelect"/> 는 "눌렸다"만 알려 준다. 대개는 그것으로 되는데,
    /// 한 물건 안에서 <b>짚은 자리에 따라 하는 일이 다른</b> 것이 있다 — 두 짝 문이 그렇다.
    /// 왼짝을 밀면 왼짝이 열리고 오른짝을 밀면 오른짝이 열려야 하는데, 눌렸다는 사실만으로는
    /// 어느 짝인지 알 길이 없어 여태 두 짝이 함께 열렸다.
    ///
    /// 카메라 자리로 어림잡을 수도 있으나, 문 한가운데 서면 그 셈이 뒤집힌다. 짚은 점은
    /// 레이가 이미 알고 있으므로 여기 두고 쓰면 된다.
    ///
    /// <b>입력 방식과 무관하다</b>: 마우스가 짚으면 마우스가 적고, 나중에 다른 것이
    /// 광선이 짚으면 그쪽이 적는다. 받아 쓰는 쪽은 어느 손이 짚었는지 몰라도 된다.
    /// </summary>
    public static class Pointing
    {
        /// <summary>마지막으로 짚은 세계 좌표.</summary>
        public static Vector3 LastPoint { get; private set; }

        /// <summary>그때 맞은 콜라이더의 자리. 어느 가지에 속한 것인지 볼 때 쓴다.</summary>
        public static Transform LastHit { get; private set; }

        /// <summary>그 일이 있었던 프레임. 묵은 값을 쓰지 않으려고 함께 적는다.</summary>
        public static int LastFrame { get; private set; } = -999;

        /// <summary>짚은 자리를 적는다. 짚는 쪽(마우스·컨트롤러)이 부른다.</summary>
        public static void Set(Vector3 point, Transform hit)
        {
            LastPoint = point;
            LastHit = hit;
            LastFrame = Time.frameCount;
        }

        /// <summary>지금(또는 바로 앞 프레임) 짚은 것이 있나. 묵은 값이면 거짓.</summary>
        public static bool Fresh => Time.frameCount - LastFrame <= 1;
    }
}
