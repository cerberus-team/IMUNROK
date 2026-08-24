using System;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 소지품 "쓰임" 버튼이 눌렸을 때 무엇을 할지 가르는 자리 (2026-08-23).
    ///
    /// ■ 왜 한 군데로 모았나
    ///   InventoryUI는 판을 그리는 일만 안다. 물건마다의 사연(지도를 펴면 어떻게 되는가)을
    ///   거기 적기 시작하면 판이 사건 진행을 알게 되고, 물건이 늘 때마다 판을 고쳐야 한다.
    ///   그래서 판은 "눌렸다"만 알리고, 무엇을 할지는 여기서 물건 id로 갈라 보낸다.
    ///
    /// ■ 물건을 붙이는 법
    ///   아래 switch에 한 줄 늘리면 된다. 아무도 맡지 않으면 <c>false</c>가 돌아가고,
    ///   판이 그 물건의 <see cref="InventoryItem.useNotReadyHint"/>를 띄운다.
    /// </summary>
    public static class ItemUse
    {
        /// <summary>
        /// 물건을 쓴다. 맡은 곳이 있으면 true (그쪽이 안내·연출까지 책임진다).
        /// </summary>
        /// <param name="item">쓰려는 물건</param>
        /// <param name="closeBoard">
        /// 소지품 판을 닫는 손잡이. 화면을 차지하는 연출로 넘어갈 때만 부른다 —
        /// 그저 안내 문구만 띄우는 경우에는 판을 그대로 둔다 (다른 물건을 마저 보게).
        /// </param>
        public static bool Try(InventoryItem item, Action closeBoard)
        {
            if (item == null) return false;

            switch (item.Key)
            {
                case SecretMapUse.ItemId:
                    return SecretMapUse.Try(item, closeBoard);
            }
            return false;
        }

        /// <summary>
        /// 쓰임 버튼에 그릴 문구. 기본은 <see cref="InventoryItem.useLabel"/> 그대로지만,
        /// **상황에 따라 하는 일이 달라지는 물건**은 문구도 따라 바뀌어야 한다
        /// (지도: 발밑 길이 켜져 있으면 「펼쳐보기」가 아니라 「접기」).
        /// 판은 이것만 물어보고 무엇을 하는 물건인지는 알 필요가 없다.
        /// </summary>
        public static string LabelFor(InventoryItem item)
        {
            if (item == null) return "";

            switch (item.Key)
            {
                case SecretMapUse.ItemId:
                    return SecretMapUse.Label(item);
            }
            return item.useLabel;
        }
    }
}
