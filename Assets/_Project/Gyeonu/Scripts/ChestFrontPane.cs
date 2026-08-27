using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 찬장 **속 상자의 앞판**을 문이 열려 있는 동안만 비쳐 보이게 한다 (2026-08-25).
    ///
    /// ■ 왜 이런 것이 필요한가
    ///   원본 찬장의 문짝은 **속이 빈 테두리**다. 닫힌 찬장에서 문살 안쪽에 보이는 검은 판은
    ///   문이 아니라, 몸통 메시에 따로 들어 있는 **속 상자의 앞면 두 삼각형**이다.
    ///   그 판이 문 구멍 뒤를 그대로 막고 있어, 문을 열어도 안이 보이지 않는다.
    ///   그렇다고 그 두 삼각형을 지우거나 옮기면 닫힌 모습이 원본과 달라진다.
    ///
    /// ■ 그래서 재질만 바꾼다
    ///   몸통 메시를 **삼각형·정점이 완전히 같은 사본**으로 두되 인덱스만 두 서브메시로 나눠,
    ///   앞판 두 장에만 다른 재질 칸을 준다. 삼각형은 늘 그 자리에서 그려진다 —
    ///   닫히면 원본과 같은 나뭇결, 열리면 투명.
    ///
    /// ■ 바꾸는 순간이 안 보이는 이유
    ///   여는 순간에는 문이 아직 닫혀 있고, 닫힘이 끝나는 순간에도 문이 이미 닫혀 있다.
    ///   두 순간 모두 문짝이 앞판을 가리므로 재질이 바뀌는 것을 볼 수 없다.
    ///   (그래서 <see cref="FurnitureParts.IsOpen"/> 만 보면 안 되고 <see cref="FurnitureParts.Progress"/>
    ///    까지 봐야 한다 — 닫기 시작하는 순간에 되돌리면 열린 문 너머로 판이 튀어나온다)
    /// </summary>
    [DisallowMultipleComponent]
    public class ChestFrontPane : MonoBehaviour
    {
        [Tooltip("이 찬장이 열려 있는 동안 앞판을 비운다")]
        public FurnitureParts doors;

        [Tooltip("앞판이 들어 있는 렌더러 (찬장 몸통)")]
        public Renderer body;

        [Tooltip("앞판이 배정된 재질 칸(서브메시) 번호")]
        public int slot = 1;

        [Tooltip("닫혔을 때 — 원본 찬장 재질 그대로")]
        public Material closedMaterial;

        [Tooltip("열렸을 때 — 투명 재질")]
        public Material openMaterial;

        bool opaque = true;

        void Awake() => Apply(true);

        void OnEnable() => Apply(IsClosed);

        /// <summary>문이 **완전히** 닫혀 있는가. 여닫는 중에는 false다.</summary>
        bool IsClosed => doors == null || (!doors.IsOpen && doors.Progress <= 0.0001f);

        // FurnitureParts.Update 가 진행도를 굴린 뒤에 본다
        void LateUpdate()
        {
            bool closed = IsClosed;
            if (closed != opaque) Apply(closed);
        }

        void Apply(bool closed)
        {
            opaque = closed;
            if (body == null) return;
            var mats = body.sharedMaterials;
            if (slot < 0 || slot >= mats.Length) return;
            var want = closed ? closedMaterial : openMaterial;
            if (want == null || mats[slot] == want) return;
            mats[slot] = want;
            body.sharedMaterials = mats;   // 공유 재질 배열 교체 — 인스턴스가 새지 않는다
        }
    }
}
