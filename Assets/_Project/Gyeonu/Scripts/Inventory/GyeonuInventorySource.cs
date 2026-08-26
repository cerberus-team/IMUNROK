using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 견우의 <see cref="Inventory"/>(정적 저장소)를 소지품 판이 아는 모양(<see cref="IUiItemSource"/>)으로
    /// 이어 주는 얇은 껍데기 (2026-08-26).
    ///
    /// ■ 왜 필요한가
    ///   판은 이제 <c>Inventory</c> 를 모른다 — 다른 사건에서 화면만 가져다 쓸 수 있게 끊었다.
    ///   대신 <see cref="UiItems.Source"/> 에 꽂힌 것을 본다. 그 자리에 우리를 꽂는 것이 이 파일이다.
    ///
    /// ■ 동작은 하나도 안 달라진다
    ///   목록도 이벤트도 <c>Inventory</c> 것을 그대로 넘긴다. 판이 보는 값이 예전과 같다.
    ///
    /// ⚠️ 도메인 리로드가 꺼진 프로젝트라 정적 값이 플레이 세션을 넘겨 살아남는다.
    ///    <see cref="UiItems"/> 가 세션 시작에 자리를 비우므로, 여기서 <b>세션마다 다시 꽂는다.</b>
    ///    안 그러면 두 번째 Play 부터 소지품이 통째로 비어 보인다.
    /// </summary>
    public class GyeonuInventorySource : IUiItemSource
    {
        public static readonly GyeonuInventorySource Instance = new GyeonuInventorySource();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() { UiItems.Source = Instance; }

        /// <summary><c>IReadOnlyList</c> 는 공변(covariant)이라 그대로 넘겨도 된다.</summary>
        public IReadOnlyList<IUiItem> Items { get { return Inventory.Items; } }

        public event Action Changed
        {
            add { Inventory.Changed += value; }
            remove { Inventory.Changed -= value; }
        }

        /// <summary>물건 하나를 실어 나르는 이벤트는 형이 달라 중계가 필요하다
        /// (<c>Action&lt;InventoryItem&gt;</c> → <c>Action&lt;IUiItem&gt;</c>).</summary>
        public event Action<IUiItem> Added
        {
            add
            {
                Action<InventoryItem> bridge = it => value(it);
                bridges[value] = bridge;
                Inventory.Added += bridge;
            }
            remove
            {
                Action<InventoryItem> bridge;
                if (!bridges.TryGetValue(value, out bridge)) return;
                Inventory.Added -= bridge;
                bridges.Remove(value);
            }
        }

        readonly Dictionary<Action<IUiItem>, Action<InventoryItem>> bridges =
            new Dictionary<Action<IUiItem>, Action<InventoryItem>>();
    }
}
