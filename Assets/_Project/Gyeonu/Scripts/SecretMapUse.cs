using System;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 타공 비밀지도(A1)의 **머리** — 지금 여기서 지도를 펴면 무슨 일이 일어나는가 (2026-08-23).
    ///
    /// ■ 한 버튼, 세 가지 일
    ///   같은 「펼쳐보기」가 선 자리에 따라 다르게 동작한다. 무엇을 할지 고르는 곳은 **여기 한 군데**다.
    ///     ① 관측실 + 혼상 점등  → 길 밝히기 연출 (<see cref="SecretMapReveal"/>)
    ///     ② 은하담 + 길 밝힌 뒤 → 발밑 별빛 길 켜기/끄기 (<see cref="StarPathGuide"/>)
    ///     ③ 그 밖              → 안내 문구만
    ///
    /// ■ 어떻게 가르나 — 씬에 무엇이 있는지로 가른다
    ///   "지금 씬 이름이 무엇인가"로 갈라도 되지만, 그러면 씬 이름을 바꾸는 순간 조용히 깨진다.
    ///   관측실에는 <see cref="HonsangController"/>가, 은하담에는 <see cref="StarPathGuide"/>가
    ///   있다는 사실은 그 씬이 그 씬인 이유 자체다. 둘은 같은 씬에 함께 있지 않는다.
    ///
    /// ■ 왜 <see cref="SecretMapReveal"/>과 나눠 두었나
    ///   저쪽은 10초짜리 카메라 연출 하나만 안다. 여기는 "어느 쪽을 부를 것인가"만 안다.
    ///   나중에 지도로 할 일이 하나 더 늘면 이 파일에만 가지가 붙는다.
    /// </summary>
    public static class SecretMapUse
    {
        /// <summary>소지품 정의(Item_A1_타공비밀지도)의 id. 빌더와 <see cref="ItemUse"/>가 참조한다.</summary>
        public const string ItemId = "A1";

        // ── 안내 문구 ─────────────────────────────────────
        // 조건을 설명하지 않는다. 지금 눈에 보이는 것만 말하고, 무엇이 모자란지는 스스로 깨닫게 둔다.
        public const string MsgElsewhere = "펼쳐 보아도 성글게 뚫린 구멍뿐이다. 지금은 아무것도 보이지 않는다.";

        /// <summary>은하담에서 길이 켜져 있을 때의 버튼 문구 — 「펼쳐보기」의 반대말.</summary>
        const string LabelFold = "접기";

        // ═══════════════════════════════════════════════════
        /// <summary><see cref="ItemUse"/>가 부른다. 무엇을 했든 지도가 맡았으면 true.</summary>
        public static bool Try(InventoryItem item, Action closeBoard)
        {
            if (item == null) return false;

            // ① 관측실 — 혼상이 있는 씬. 점등 여부·연출·문구는 전부 저쪽이 판단한다
            var hon = Find<HonsangController>();
            if (hon != null) return SecretMapReveal.Use(item, closeBoard, hon);

            // ② 은하담 — 발밑 별빛 길
            var guide = Find<StarPathGuide>();
            if (guide != null && PathKnown) return ToggleGround(guide, closeBoard);

            // ③ 그 밖 (그리고 은하담이되 아직 길을 못 밝힌 경우)
            DebugToast.Show(MsgElsewhere, 3.5f);
            return true;
        }

        /// <summary>
        /// 상세 창의 쓰임 버튼에 쓸 문구. 발밑 길이 켜져 있을 때만 「접기」로 바뀐다 —
        /// 「펼쳐보기」라고 적힌 버튼이 접는 동작을 하면 두 번은 헷갈리고 세 번째엔 버그로 읽힌다.
        /// </summary>
        public static string Label(InventoryItem item)
        {
            var guide = Find<StarPathGuide>();
            if (guide != null && PathKnown && guide.IsVisible) return LabelFold;
            return item != null ? item.useLabel : "";
        }

        // ── 은하담 발밑 별빛 길 ────────────────────────────

        /// <summary>관측실에서 이미 길을 밝혔는가 — 발밑 길의 선행 조건.</summary>
        static bool PathKnown => GyeonuWorld.Has(GyeonuWorld.F_타공지도_길밝힘)
                              || GyeonuWorld.DebugIgnoreConditions;

        /// <summary>
        /// 펴 들면 켜지고 접으면 꺼진다 — **토글**이다.
        ///
        /// 왜 시간 제한이 아닌가: 은하담에서 견우마을 입구까지는 백 미터가 넘는 야외다.
        /// 몇 초 뒤 저절로 꺼지면 걷다 말고 소지품을 다시 여는 일이 길 내내 되풀이된다.
        /// 게다가 이 길은 **플레이어를 따라 다시 그려진다**(StarPathGuide) — 켜 둔 채 걷는 것이
        /// 원래 설계다. 지도를 펴 들고 걷다가 접는 쪽이 물건의 생김새와도 맞는다.
        /// </summary>
        static bool ToggleGround(StarPathGuide guide, Action closeBoard)
        {
            bool on = !guide.IsVisible;
            // 판이 눈앞을 가린 채로는 발밑이 안 보인다 — 켤 때도 끌 때도 물러난다
            closeBoard?.Invoke();
            guide.Show(on);
            Debug.Log("[비밀지도] 발밑 별빛 길 " + (on ? "켬" : "끔") +
                      $" (경로 {guide.LastPointCount}점 / 별 {guide.LastStarCount}개)");
            return true;
        }

        static T Find<T>() where T : Component =>
            UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Exclude);
    }
}
