using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 장부 판 위의 **자리** 하나 (2026-08-24).
    ///
    /// 자리는 빈 Transform이다 — 그림도 콜라이더도 없다. 무엇이 어디 놓였는지는
    /// <see cref="occupant"/> 한 칸으로만 표현하고, 판정은 이 표를 읽는다.
    /// 판을 다시 그리거나 칸 크기를 바꿔도 판정 코드는 무영향이다.
    /// </summary>
    public class LedgerSlot : MonoBehaviour
    {
        public enum Kind
        {
            /// <summary>3×4 격자의 기물 칸. index = 행*4 + 열</summary>
            칸,
            /// <summary>열 머리 — 제작자 표식 라벨이 놓일 자리. index = 열(0~3)</summary>
            열머리,
            /// <summary>행 머리 — 처리 장소 라벨이 놓일 자리. index = 행(0~2)</summary>
            행머리,
            /// <summary>아래 칸의 라벨 보관 자리 (1단계 시작 위치). index = 0~6</summary>
            보관,
            /// <summary>2단계 대조대. index = 아버지 기록 번호(0~2)</summary>
            대조,
        }

        public Kind kind;
        public int index;

        [Tooltip("지금 이 자리에 놓인 것 (없으면 비어 있다)")]
        public LedgerPiece occupant;

        public bool Empty => occupant == null;

        /// <summary>이 자리가 받아 줄 수 있는 물건인가.</summary>
        public bool Accepts(LedgerPiece p)
        {
            if (p == null) return false;
            switch (kind)
            {
                case Kind.칸: return !p.isLabel;
                case Kind.대조: return !p.isLabel;
                case Kind.열머리:
                case Kind.행머리:
                case Kind.보관: return p.isLabel;
            }
            return false;
        }
    }
}
