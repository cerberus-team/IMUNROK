using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 소지품 판이 <b>물건에게 묻는 것 전부</b> (2026-08-26).
    ///
    /// ■ 왜 인터페이스인가
    ///   판·상세·조사·미리보기 1,400여 줄은 원래 견우의 <c>InventoryItem</c>(ScriptableObject)에
    ///   직접 붙어 있었다. 다른 사건 담당자가 화면만 가져다 쓰려면 우리 물건 정의까지
    ///   끌고 가야 했다. 실측해 보니 <b>판이 실제로 읽는 것은 아래 열 가지뿐</b>이라,
    ///   그만큼만 인터페이스로 뽑았다.
    ///
    /// ■ 구현하는 쪽은 무엇을 하나
    ///   자기 물건 클래스에 <c>: IUiItem</c> 을 붙이고 프로퍼티를 이어 주면 끝이다.
    ///   견우의 <see cref="InventoryItem"/> 이 그 예다 — <b>필드는 하나도 안 바꾸고</b>
    ///   프로퍼티만 얹었다(그래서 기존 .asset 이 그대로 읽힌다).
    /// </summary>
    public interface IUiItem
    {
        /// <summary>같은 물건인지 가리는 열쇠. 미리보기 캐시가 이걸로 구분한다.</summary>
        string Key { get; }

        /// <summary>칸과 상세 머리에 뜨는 이름.</summary>
        string DisplayName { get; }

        /// <summary>상세 화면의 본문. 줄바꿈(\n)을 써도 된다.</summary>
        string Description { get; }

        /// <summary>돌려 볼 실물. <b>없으면(null) 조사 화면이 빈 막으로 뜬다.</b></summary>
        GameObject ModelPrefab { get; }

        /// <summary>미리보기 초기 각도.</summary>
        Vector3 PreviewEuler { get; }

        /// <summary>미리보기 초기 배율 (1 = 기본).</summary>
        float PreviewZoom { get; }

        /// <summary>상세 화면에 「사용하기」 단추를 낼지.</summary>
        bool ShowUseButton { get; }

        /// <summary>그 단추의 글귀.</summary>
        string UseLabel { get; }

        /// <summary>아직 쓸 수 없을 때 띄울 안내.</summary>
        string UseNotReadyHint { get; }

        /// <summary>손에 넣은 순간 조사 화면을 저절로 열지.</summary>
        bool AutoShowOnPickup { get; }
    }

    /// <summary>
    /// 소지품 판이 <b>목록을 얻는 곳</b>. 저장·획득 규칙은 구현하는 쪽 마음이다.
    /// </summary>
    public interface IUiItemSource
    {
        IReadOnlyList<IUiItem> Items { get; }

        /// <summary>목록이 달라졌다 (판이 다시 그린다).</summary>
        event Action Changed;

        /// <summary>물건을 새로 얻었다 (획득 연출을 띄운다).</summary>
        event Action<IUiItem> Added;
    }

    /// <summary>
    /// <b>「사용하기」 단추가 눌렸을 때 무엇을 할지</b> 정하는 자리.
    ///
    /// 판은 물건마다의 사연을 몰라야 한다 — 지도를 펴면 어떻게 되는지 따위를 판에 적기 시작하면
    /// 판이 사건 진행을 알게 되고 물건이 늘 때마다 판을 고쳐야 한다.
    /// 그래서 판은 "눌렸다"만 알리고, 무엇을 할지는 이 구현이 정한다.
    ///
    /// 아무도 안 꽂으면 <b>단추 글귀는 물건이 말한 대로 나오고 눌러도 아무 일이 없다</b>
    /// (물건의 <see cref="IUiItem.UseNotReadyHint"/> 가 뜬다).
    /// </summary>
    public interface IUiItemUse
    {
        /// <summary>단추에 그릴 글귀. 상황에 따라 하는 일이 달라지는 물건은 글귀도 따라 바뀐다.</summary>
        string LabelFor(IUiItem item);

        /// <summary>물건을 쓴다. 맡은 곳이 있으면 true — 그쪽이 안내·연출까지 책임진다.</summary>
        /// <param name="closePanel">화면을 차지하는 연출로 넘어갈 때만 부를 것.</param>
        bool Try(IUiItem item, Action closePanel);
    }

    /// <summary>
    /// <b>지금 쓰는 소지품 저장소를 꽂는 자리.</b>
    ///
    /// 아무도 안 꽂으면 <see cref="Empty"/> 가 답한다 — 그래서 <b>아무것도 구현하지 않아도
    /// 소지품 판은 뜨고 조작된다</b>("아직 지닌 것이 없다"가 뜬 채로). 그 상태에서 자기
    /// 저장소를 만들어 꽂으면 그대로 살아난다.
    ///
    /// ⚠️ 도메인 리로드가 꺼진 프로젝트다 — 정적 값이 플레이 세션을 넘겨 살아남는다.
    ///    꽂는 쪽은 <see cref="RuntimeInitializeOnLoadMethod"/> 로 <b>세션마다 다시 꽂을 것</b>.
    /// </summary>
    public static class UiItems
    {
        static IUiItemSource _source;
        static IUiItemUse _use;

        public static IUiItemSource Source
        {
            get { return _source ?? Empty; }
            set { _source = value; }
        }

        /// <summary>「사용하기」를 맡는 곳. 안 꽂으면 아무 일도 안 하는 기본 구현이 답한다.</summary>
        public static IUiItemUse Use
        {
            get { return _use ?? NoUse; }
            set { _use = value; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _source = null; _use = null; }

        /// <summary>아무것도 안 꽂혔을 때 답하는 빈 저장소.</summary>
        public static readonly IUiItemSource Empty = new EmptySource();

        /// <summary>아무것도 안 꽂혔을 때 답하는 빈 쓰임.</summary>
        public static readonly IUiItemUse NoUse = new DefaultUse();

        class EmptySource : IUiItemSource
        {
            static readonly IUiItem[] none = new IUiItem[0];
            public IReadOnlyList<IUiItem> Items { get { return none; } }
            public event Action Changed { add { } remove { } }
            public event Action<IUiItem> Added { add { } remove { } }
        }

        class DefaultUse : IUiItemUse
        {
            public string LabelFor(IUiItem item) { return item != null ? item.UseLabel : ""; }
            public bool Try(IUiItem item, Action closePanel) { return false; }
        }
    }
}
