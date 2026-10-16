using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// "이 오브젝트의 몸이 실제로 어디에 있는가"를 재는 도구.
    ///
    /// 왜 필요한가: 외부에서 받은 캐릭터 FBX는 피벗(Transform 원점)이 발밑에 있으리라는
    /// 보장이 없다. 옹덕구는 피벗이 몸에서 1.5m쯤 떨어져 있어서,
    ///   · 바닥에 맞추려 하면 엉뚱한 높이로 가고
    ///   · transform.position 으로 거리를 재면 코앞에 서 있어도 "멀다"고 나온다
    /// 그래서 위치를 추측하지 않고 렌더러 경계(실제로 그려지는 범위)를 잰다.
    /// </summary>
    public static class ModelBounds
    {
        /// <summary>미리 모아둔 렌더러들로 경계를 구한다(매 프레임 쓸 때 탐색 비용을 없애려고).</summary>
        public static bool TryGet(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            if (renderers == null || renderers.Length == 0) return false;

            bool started = false;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!started) { bounds = r.bounds; started = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return started;
        }

        /// <summary>자식까지 포함한 렌더러 경계(월드). 렌더러가 없으면 false.</summary>
        public static bool TryGet(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null) return false;

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        /// <summary>
        /// <b>얼굴께.</b> 몸 경계의 가로 한가운데, 높이로는 위에서 조금 내려온 자리.
        ///
        /// 사람을 쳐다보라고 이를 때 <c>transform.position</c> 을 주면 <b>발</b>을 본다 —
        /// 캐릭터의 피벗은 대개 발밑이고, 옹덕구처럼 몸에서 멀리 떨어진 것도 있다.
        /// 말을 걸고 나서 고개를 되돌렸더니 상대의 발치를 보고 있던 것이 그 까닭이다.
        ///
        /// 정수리가 아니라 <b>0.86</b> 께다. 머리 꼭대기를 보면 상대를 올려다보는
        /// 꼴이 되어, 눈을 맞추는 것이 아니라 갓을 보게 된다.
        /// 렌더러가 없으면 피벗 위 1.5m 로 물러선다(사람 키의 어림).
        /// </summary>
        public static Vector3 EyePoint(Transform root, float t = 0.86f)
        {
            if (root == null) return Vector3.zero;
            if (TryGet(root, out Bounds b))
                return new Vector3(b.center.x, Mathf.Lerp(b.min.y, b.max.y, t), b.center.z);
            return root.position + Vector3.up * 1.5f;
        }

        /// <summary>
        /// 어떤 지점에서 이 오브젝트의 "몸"까지의 거리.
        /// 피벗이 아니라 몸 표면 기준이라, 피벗이 어디 있든 사람이 느끼는 거리와 일치한다.
        /// 렌더러가 없으면 피벗까지의 거리로 물러선다.
        /// </summary>
        public static float DistanceTo(Transform root, Vector3 from)
        {
            if (TryGet(root, out Bounds b))
                return Vector3.Distance(from, b.ClosestPoint(from));
            return root != null ? Vector3.Distance(from, root.position) : float.MaxValue;
        }
    }
}
