using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 장부 판 위에서 **끌어다 놓는 한 점** — 기물 12개와 표찰 7개가 같은 부품을 쓴다 (2026-08-24).
    ///
    /// ■ 왜 기물과 표찰이 같은 부품인가
    ///   판 입장에서 둘의 차이는 "어느 자리에 놓일 수 있는가"뿐이다(<see cref="LedgerSlot.Accepts"/>).
    ///   집고·따라오고·자리에 앉는 동작은 완전히 같다. 나누면 같은 코드가 두 벌이 된다.
    ///
    /// ■ 놓인 자세
    ///   자리(Slot)의 위치를 그대로 쓰되, **자세는 만들 때 정한 것을 유지한다**.
    ///   기물마다 눕는 방향이 다르고(축은 가로, 고리는 평면), 그건 빌더가 실측해 정해 둔다.
    /// </summary>
    public class LedgerPiece : MonoBehaviour
    {
        [Tooltip("표찰이면 true — 기물 칸이 아니라 머리·보관 자리에만 놓인다")]
        public bool isLabel;

        [Tooltip("기물: 1~12 (LedgerData.Pieces의 id). 표찰: 0~3 = 매화·학·구름·거북 / 4~6 = 관아·외고·후고")]
        public int id;

        [Tooltip("표찰일 때 — 제작자 표식 표찰인가(false면 처리 장소)")]
        public bool labelIsMark;

        [Tooltip("확대 조사로 띄울 모델·설명. 표찰은 비워 둔다")]
        public InventoryItem inspectItem;

        [Tooltip("지금 놓여 있는 자리")]
        public LedgerSlot slot;

        [Tooltip("자리에 앉을 때 판 바깥으로 띄우는 거리(m) — 판에 파묻히지 않게")]
        public float restLift = 0.006f;

        /// <summary>이 표찰이 가리키는 제작자 표식 (표찰이 아니거나 장소 표찰이면 뜻 없음).</summary>
        public LedgerMark Mark => (LedgerMark)id;
        /// <summary>이 표찰이 가리키는 처리 장소.</summary>
        public LedgerSite Site => (LedgerSite)(id - 4);

        /// <summary>기물 정의 (표찰이면 null).</summary>
        public LedgerPieceDef Def => isLabel ? null : LedgerData.Piece(id);

        Quaternion restRot;
        Vector3 grabOffset;
        bool captured;

        void Awake() => Capture();

        /// <summary>빌더가 정해 둔 놓인 자세를 원장에 잡는다 (FurnitureParts와 같은 사정).</summary>
        public void Capture()
        {
            if (captured) return;
            restRot = transform.localRotation;
            captured = true;
        }

        /// <summary>자리에 앉힌다. 이전 자리는 비운다.</summary>
        public void SitIn(LedgerSlot s)
        {
            Capture();
            if (slot != null && slot.occupant == this) slot.occupant = null;
            slot = s;
            if (s == null) return;
            s.occupant = this;
            // 자리는 모두 같은 부모(판 뿌리) 밑에 있다 — 그 좌표계에서 판 앞(+Z)으로만 띄운다
            transform.SetParent(s.transform.parent, false);
            transform.localPosition = s.transform.localPosition + new Vector3(0f, 0f, restLift);
            transform.localRotation = restRot;
        }

        /// <summary>집어 든다 — 판 앞으로 살짝 떠오른다.</summary>
        public void Grab(Vector3 worldPoint)
        {
            Capture();
            grabOffset = transform.position - worldPoint;
        }

        /// <summary>끌려다니는 동안의 자리. 판 평면 위 점을 받아 그 위로 띄운다.</summary>
        public void DragTo(Vector3 worldPointOnBoard, Vector3 boardForward, float lift)
        {
            transform.position = worldPointOnBoard + grabOffset + boardForward * lift;
        }

        /// <summary>놓지 못했을 때 원래 자리로 돌아간다.</summary>
        public void ReturnHome()
        {
            if (slot != null) SitIn(slot);
        }
    }
}
