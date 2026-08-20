using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 별 길이 쓰는 경로 탐색기 — 웨이포인트 그래프 + A* 방식.
    ///
    /// ■ 왜 NavMesh가 아닌가
    ///   NavMesh를 쓰려면 씬의 지형·구조물을 Navigation Static으로 표시하고 베이크해야 한다.
    ///   그러면 ① 씬 오브젝트의 플래그를 대량으로 건드리고(배치 변경 금지), ② NavMesh 에셋과
    ///   ProjectSettings 쪽 설정이 딸려 나오며(설정 수정 금지), ③ 다리 프리팹 원본에도 손이 간다.
    ///   지금 규칙으로는 셋 다 못 한다.
    ///
    /// ■ 왜 직선 + 장애물 회피가 아닌가
    ///   은하담의 핵심 제약은 강이다. 강은 남북으로 흐르고 건널 수 있는 곳은 오작교 하나뿐이라,
    ///   국소 회피로는 절대 못 푼다 — 강가에 붙어 하염없이 맴돈다. "다리를 반드시 지나야 한다"는
    ///   전역 정보라 그래프로 못 박는 편이 정확하고 싸다.
    ///
    /// ■ 그래프가 하는 일 / 레이캐스트가 하는 일
    ///   그래프는 어디를 지나야 하는가(강은 다리로, 군락은 돌아서)만 정한다.
    ///   실제 높이는 <see cref="Densify"/> 가 촘촘히 잘라 매 점마다 아래로 레이캐스트해 결정한다.
    ///   그래서 교대 계단처럼 지형이 아닌 구조물 위에서도 별이 바닥에 붙어 올라간다.
    /// </summary>
    [AddComponentMenu("이문록/별 길 경로망 (StarPathRouter)")]
    public class StarPathRouter : MonoBehaviour
    {
        [Header("경로망 (빌더가 채운다)")]
        public Vector3[] nodes;
        [Tooltip("간선 — 두 개씩 짝지은 노드 인덱스")]
        public int[] edges;

        [Header("지면 스냅")]
        [Tooltip("경로를 이 간격으로 잘라 높이를 다시 찍는다(m). 촘촘할수록 계단을 잘 탄다")]
        public float sampleStep = 0.8f;
        [Tooltip("각 점에서 이만큼 위에서 아래로 쏜다(m)")]
        public float probeUp = 3.5f;
        [Tooltip("별을 보행면에서 띄우는 높이(m)")]
        public float lift = 0.07f;

        List<int>[] _adj;

        void Awake() => BuildAdjacency();

        void BuildAdjacency()
        {
            if (nodes == null) return;
            _adj = new List<int>[nodes.Length];
            for (int i = 0; i < nodes.Length; i++) _adj[i] = new List<int>();
            if (edges == null) return;
            for (int e = 0; e + 1 < edges.Length; e += 2)
            {
                int a = edges[e], b = edges[e + 1];
                if (a < 0 || b < 0 || a >= nodes.Length || b >= nodes.Length) continue;
                _adj[a].Add(b);
                _adj[b].Add(a);
            }
        }

        /// <summary>
        /// from(플레이어) → to(목적지)의 보행 경로를 지면에 붙은 점열로 돌려준다.
        /// 경로가 없으면 빈 리스트.
        /// </summary>
        public List<Vector3> Route(Vector3 from, Vector3 to)
        {
            if (_adj == null) BuildAdjacency();
            var empty = new List<Vector3>();
            if (nodes == null || nodes.Length < 2) return empty;

            int start = NearestReachable(from);
            int goal = NearestReachable(to);
            if (start < 0 || goal < 0) return empty;

            var viaNodes = AStar(start, goal);
            if (viaNodes == null) return empty;

            var coarse = new List<Vector3> { from };
            foreach (var n in viaNodes) coarse.Add(nodes[n]);
            coarse.Add(to);

            return Densify(coarse);
        }

        /// <summary>가장 가까우면서 곧장 걸어갈 수 있는 노드. 없으면 그냥 최근접.</summary>
        int NearestReachable(Vector3 p)
        {
            int best = -1, fallback = -1;
            float bestSq = float.MaxValue, fbSq = float.MaxValue;
            for (int i = 0; i < nodes.Length; i++)
            {
                float sq = (nodes[i] - p).sqrMagnitude;
                if (sq < fbSq) { fbSq = sq; fallback = i; }
                if (sq > 40f * 40f) continue;
                if (!Walkable(p, nodes[i])) continue;
                if (sq < bestSq) { bestSq = sq; best = i; }
            }
            return best >= 0 ? best : fallback;
        }

        /// <summary>두 점 사이를 곧장 걸을 수 있는가 — 1m마다 지면을 찍어 낭떠러지와 물을 걸러낸다.</summary>
        public bool Walkable(Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
            int n = Mathf.Max(2, Mathf.CeilToInt(len));
            float prevY = 0f;
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                if (!Ground(p, out float y, out Collider col)) return false;
                if (col != null && (col.name.Contains("물") || col.name.Contains("Water"))) return false;
                if (i > 0 && Mathf.Abs(y - prevY) > 1.3f) return false;   // 단차 = 낭떠러지
                prevY = y;
            }
            return true;
        }

        bool Ground(Vector3 p, out float y, out Collider col) => RayDown(p.x, p.z, p.y, out y, out col);
        /// <summary>
        /// (x,z)에서 fromY+probeUp 높이부터 아래로 쏴 보행면을 찾는다.
        ///
        /// ⚠️ 플레이어 자신의 캡슐은 건너뛴다. 경로는 발밑에서 시작하므로 그냥 쏘면
        ///    첫 점이 자기 몸통에 맞아 별이 공중에 뜬다(에디터에서는 씬에 세워 둔
        ///    디버그_워커가 경로망 검사까지 망친다 — 2026-08-18 실측).
        /// </summary>
        bool RayDown(float x, float z, float fromY, out float y, out Collider col)
        {
            y = 0f; col = null;
            var hits = Physics.RaycastAll(new Vector3(x, fromY + probeUp, z), Vector3.down,
                                          probeUp + 60f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (IsPlayerCollider(h.collider)) continue;
                y = h.point.y; col = h.collider; return true;
            }
            return false;
        }

        static bool IsPlayerCollider(Collider c)
            => c is CharacterController
            || c.GetComponentInParent<DebugWalkController>() != null;


        List<int> AStar(int start, int goal)
        {
            int n = nodes.Length;
            var g = new float[n]; var f = new float[n]; var prev = new int[n];
            var open = new List<int>(); var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.MaxValue; prev[i] = -1; }
            g[start] = 0f; f[start] = H(start, goal); open.Add(start);

            while (open.Count > 0)
            {
                int cur = open[0];
                for (int i = 1; i < open.Count; i++) if (f[open[i]] < f[cur]) cur = open[i];
                if (cur == goal)
                {
                    var path = new List<int>();
                    for (int c = cur; c >= 0; c = prev[c]) path.Add(c);
                    path.Reverse();
                    return path;
                }
                open.Remove(cur); closed[cur] = true;

                foreach (var nb in _adj[cur])
                {
                    if (closed[nb]) continue;
                    float tentative = g[cur] + Vector3.Distance(nodes[cur], nodes[nb]);
                    if (tentative >= g[nb]) continue;
                    prev[nb] = cur; g[nb] = tentative; f[nb] = tentative + H(nb, goal);
                    if (!open.Contains(nb)) open.Add(nb);
                }
            }
            return null;
        }

        float H(int a, int b) => Vector3.Distance(nodes[a], nodes[b]);

        /// <summary>
        /// 굵은 점열을 sampleStep 간격으로 잘라 매 점을 실제 보행면에 앉힌다.
        /// 계단·구조물 위도 이 레이캐스트가 잡아 준다.
        ///
        /// ⚠️ 출발 높이를 직전 점에서 가져오는 이유: 하늘에서 쏘면 난간·처마·지붕에 맞고,
        ///    발밑에서 쏘면 다음 계단 단을 놓친다. 직전 점보다 probeUp 만큼만 위에서 쏜다.
        /// </summary>
        public List<Vector3> Densify(List<Vector3> coarse)
        {
            var outPts = new List<Vector3>();
            for (int i = 0; i < coarse.Count - 1; i++)
            {
                var a = coarse[i]; var b = coarse[i + 1];
                float len = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.1f, sampleStep)));
                for (int s = 0; s < steps; s++)
                {
                    var p = Vector3.Lerp(a, b, s / (float)steps);
                    float from = outPts.Count > 0 ? outPts[outPts.Count - 1].y : p.y;
                    if (RayDown(p.x, p.z, from, out float gy, out _))
                        outPts.Add(new Vector3(p.x, gy + lift, p.z));
                }
            }
            if (coarse.Count > 0)
            {
                var last = coarse[coarse.Count - 1];
                float from = outPts.Count > 0 ? outPts[outPts.Count - 1].y : last.y;
                if (RayDown(last.x, last.z, from, out float gy, out _))
                    outPts.Add(new Vector3(last.x, gy + lift, last.z));
            }
            return outPts;
        }
    }
}
