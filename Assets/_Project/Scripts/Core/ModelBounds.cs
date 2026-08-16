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
