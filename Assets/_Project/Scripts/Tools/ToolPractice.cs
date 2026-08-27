using System;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>도구를 한 번 써 봤다</b>는 소식만 나르는 자리.
    ///
    /// 조사청에서 도구를 익히는 마지막 켜는 <b>읽는 것이 아니라 해 보는 것</b>이다 —
    /// "문서를 쥐고 돋보기를 대 보시오"가 뜨고, 그 말대로 해야 익힌 것이 된다.
    /// 그러자면 "해냈다"를 누군가 알려 줘야 하는데, 알려 줄 쪽(문서·돋보기·등불)이
    /// 알려 받을 쪽(<see cref="ToolTutorial"/>)을 알고 있으면 안 된다. 도구는
    /// 사건 한복판에서도 쓰이는 물건이지 조사청 전용이 아니기 때문이다.
    ///
    /// 그래서 가운데에 이 빈 자리를 둔다. 도구 쪽은 <see cref="Done"/> 만 부르고
    /// 누가 듣는지 모른다. 조사청이 아닌 데서 불리면 듣는 이가 없어 그냥 흩어진다.
    /// </summary>
    public static class ToolPractice
    {
        /// <summary>도구 id 하나를 실제로 써 냈다.</summary>
        public static event Action<string> OnUsed;

        /// <summary>도구를 제대로 한 번 썼다고 알린다. 듣는 이가 없으면 그냥 흩어진다.</summary>
        public static void Done(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return;
            var cb = OnUsed;
            if (cb != null) cb(toolId);
        }
    }
}
