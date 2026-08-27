using System;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>읽는 창은 한 번에 하나만.</b>
    ///
    /// 왜 필요한가: 창을 띄우는 것들이 저마다 눈앞 거리를 제 손으로 잡는다 —
    /// 손에 든 문서 0.60m, 사건 개요 0.75m, 수첩 0.85m. 서로를 모르므로 셋이 한꺼번에
    /// 켜지면 눈앞 25cm 안에서 겹쳐 버린다. 사랑채에서 증거를 보다가 수첩을 펴면
    /// 종이 위에 수첩이 포개지는 것이 이것이다.
    ///
    /// 거리를 더 벌려 피하는 길도 있었으나 그르다. 읽는 창은 <b>읽으라고</b> 띄우는 것이고,
    /// 읽는 동안 다른 읽을거리가 옆에 떠 있을 까닭이 없다. 겹침은 배치의 문제가 아니라
    /// <b>동시에 떠 있다는 것</b> 자체가 문제다.
    ///
    /// 그래서 자리를 하나만 둔다. 새로 펴는 쪽이 그 자리를 가져가고, 먼저 있던 쪽은
    /// 스스로 닫는다(닫는 법은 각자가 등록할 때 넘긴다).
    ///
    /// <b>여기 끼지 않는 것들</b>: 돋보기(0.40m)는 읽는 창이 아니라 <b>읽는 도구</b>라
    /// 문서와 같이 떠야 한다. 도구벨트는 허리춤(-0.5m 아래)이라 시야를 안 가리고,
    /// 알림·자막(1.6m)은 뒤에 있어 겹치지 않는다.
    /// </summary>
    public static class ReadingFocus
    {
        public enum Panel
        {
            None,
            /// <summary>손에 든 문서(DocumentView) — 0.60m</summary>
            Document,
            /// <summary>수첩(JournalPanel) — 0.85m</summary>
            Journal,
            /// <summary>사건 개요 두루마리(CaseBriefing) — 0.75m</summary>
            Briefing,
        }

        private static Panel _current;
        private static Action _closeCurrent;

        /// <summary>지금 자리를 쥐고 있는 창.</summary>
        public static Panel Current => _current;

        /// <summary>
        /// 자리를 가져간다. 먼저 있던 창이 있으면 그것을 닫는다.
        /// </summary>
        /// <param name="who">펴는 창</param>
        /// <param name="closeMe">이 창을 닫는 법. 다음 창이 들어올 때 불린다</param>
        public static void Claim(Panel who, Action closeMe)
        {
            if (who == Panel.None) return;
            if (_current != Panel.None && _current != who)
            {
                var close = _closeCurrent;
                // 먼저 비워 둔다 — 닫는 쪽이 Release 를 부르면서 새 주인을 지우지 않게.
                _current = Panel.None;
                _closeCurrent = null;
                try { close?.Invoke(); }
                catch (Exception e) { Debug.LogError("[읽는창] 앞 창을 닫다가 오류: " + e); }
            }
            _current = who;
            _closeCurrent = closeMe;
        }

        /// <summary>자리를 놓는다. 이미 다른 창이 가져갔으면 아무 일도 하지 않는다.</summary>
        public static void Release(Panel who)
        {
            if (_current != who) return;
            _current = Panel.None;
            _closeCurrent = null;
        }

        /// <summary>씬을 갈아 끼울 때처럼 통째로 비워야 할 때.</summary>
        public static void Clear()
        {
            _current = Panel.None;
            _closeCurrent = null;
        }
    }
}
