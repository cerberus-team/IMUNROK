using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>바닥의 결. 발소리와 <b>삐걱</b>이 여기서 갈린다.</summary>
    public enum FloorKind
    {
        /// <summary>마당·안뜰의 맨땅. 밟아도 울지 않는다 — 흙은 소리를 <b>먹는다</b>.</summary>
        흙,
        /// <summary>마루·툇마루·장판. 낡은 널은 <b>가끔 운다</b>. 이 결에서만 삐걱이 난다.</summary>
        마루,
        /// <summary>기단·댓돌·디딤돌. 딱딱하고 짧게 울린다. 삐걱은 없다.</summary>
        돌,
    }

    /// <summary>
    /// <b>이 바닥은 무엇으로 되었는가</b>를 손으로 못 박아 두는 표.
    ///
    /// 아래 <see cref="FloorKindProbe"/> 는 이름으로 결을 알아맞히는데, 이름이 없거나
    /// 엉뚱한 물건(이불을 깔아 둔 자리, 흙을 덮은 널)에는 그 짐작이 틀린다.
    /// 그럴 때만 이것을 붙인다 — 붙어 있으면 이름은 안 본다.
    ///
    /// 붙이는 곳: 바닥 오브젝트(또는 그 부모).
    /// </summary>
    public class FloorPatch : MonoBehaviour
    {
        [Tooltip("이 바닥의 결. 이름으로 짐작하는 것보다 이것이 앞선다")]
        public FloorKind kind = FloorKind.마루;
    }

    /// <summary>
    /// <b>발밑을 물어본다</b> — 지금 딛고 선 것이 흙인가 마루인가 돌인가.
    ///
    /// <b>왜 필요했나</b>: 발소리 한 벌과 삐걱 한 벌을 걸어 두고 <b>어디서나</b> 틀었다.
    /// 그래서 마당 흙바닥을 걸어도 마루가 끼익 울었다 — 남의 집 안방 널이 우는 소리는
    /// 잠행에서 <b>사람을 배신하는 대목</b>인데, 그것이 마당 한복판에서도 나면
    /// 긴장이 아니라 잡음이 된다. 어디서나 나는 소리는 아무 뜻도 없다.
    ///
    /// 그래서 <b>밟은 것</b>을 보고 고른다. 흙은 흙 소리가 나고 울지 않는다.
    /// 기단·댓돌은 딱딱하게 울리고 역시 울지 않는다. <b>마루만 운다</b>.
    ///
    /// <b>이름으로 가른다</b>: 고택 에셋의 이름이 이미 그렇게 지어져 있다 —
    /// SM_Floor* 는 널이고 SM_Gidan*·SM_Stone* 은 돌이다. 바닥 수백 장에 표를
    /// 일일이 붙이는 것보다, 이미 있는 이름을 읽는 편이 안 틀린다. 이름이 어긋나는
    /// 몇 장에만 <see cref="FloorPatch"/> 를 붙이면 된다.
    /// </summary>
    public static class FloorKindProbe
    {
        /// <summary>발밑을 훑는 깊이(m). 턱을 오르내리는 사이에도 바닥을 놓치지 않을 만큼.</summary>
        private const float Depth = 2.5f;

        /// <summary>가장 마지막에 짚은 바닥의 이름. 무엇을 밟았는지 들여다볼 때 쓴다.</summary>
        public static string LastName { get; private set; }

        /// <summary>
        /// 발이 있는 자리에서 아래를 짚어 결을 낸다. 아무것도 안 짚히면 <see cref="FloorKind.흙"/>.
        /// </summary>
        public static FloorKind Under(Vector3 feet)
        {
            RaycastHit hit;
            if (!Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out hit,
                                 Depth, ~0, QueryTriggerInteraction.Ignore))
            {
                LastName = "(허공)";
                return FloorKind.흙;
            }
            LastName = hit.collider.name;

            // 손으로 못 박아 둔 표가 있으면 그것이 먼저다
            var patch = hit.collider.GetComponentInParent<FloorPatch>();
            if (patch != null) return patch.kind;

            return FromName(hit.collider.transform);
        }

        /// <summary>
        /// 이름으로 결을 짚는다. 밟은 것 <b>자신</b>부터 위로 세 대(代)까지 올려다본다 —
        /// 널 한 장의 이름이 SM_Floor03A 여도 그 부모가 SM_Gidan 인 일은 없지만,
        /// 반대로 콜라이더만 이름 없는 자식으로 떨어져 나온 물건은 흔하다.
        /// </summary>
        private static FloorKind FromName(Transform t)
        {
            for (int up = 0; up < 4 && t != null; up++, t = t.parent)
            {
                string n = t.name;
                if (Has(n, "Floor") || Has(n, "마루") || Has(n, "널") || Has(n, "장판") ||
                    Has(n, "온돌") || Has(n, "방바닥") || Has(n, "Wood") || Has(n, "Maru"))
                    return FloorKind.마루;

                if (Has(n, "Gidan") || Has(n, "기단") || Has(n, "Stone") || Has(n, "돌") ||
                    Has(n, "댓돌") || Has(n, "디딤") || Has(n, "Step") || Has(n, "Foundation"))
                    return FloorKind.돌;
            }
            return FloorKind.흙;
        }

        private static bool Has(string haystack, string needle)
            => haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
