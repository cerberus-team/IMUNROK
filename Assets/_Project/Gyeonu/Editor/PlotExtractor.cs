using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 데모 씬에서 "집터" 단위를 자동으로 찾아 프리팹으로 뽑아낸다.
    ///
    /// 동작
    ///   1. 건물(House/Tavern/Smithy/Mill...)을 기준점으로 삼는다
    ///   2. 가까이 붙은 건물들은 하나의 집터로 합친다
    ///   3. 나머지 오브젝트(담장·소품)를 가장 가까운 집터에 배정한다
    ///   4. Buildings / Walls / Props 로 분류해 프리팹 저장
    ///
    /// 안전장치
    ///   - 원본은 복사만 하고 건드리지 않는다. 작업 후 복사본은 전부 삭제된다
    ///   - 삼각형이 많은 소품은 자동 제외하고 목록으로 보고한다
    ///   - "미리보기"로 결과를 먼저 확인할 수 있다 (아무것도 만들지 않음)
    ///
    /// ⚠ 실행 후 씬 저장 여부를 물으면 반드시 "Don't Save"
    /// </summary>
    public static class PlotExtractor
    {
        // ─── 조절 가능한 값 ─────────────────────────────
        // 이 거리 안의 건물들은 한 집터로 본다 (미터)
        private const float AnchorMergeDistance = 18f;
        // 집터 경계에서 이 거리 안의 소품·담장을 그 집터 소속으로 본다 (미터)
        private const float ClaimPadding = 6f;
        // 이 삼각형 수를 넘는 소품은 제외한다
        private const int HeavyPropThreshold = 10000;
        // 프리팹 저장 위치
        private const string OutputFolder = "Assets/_Project/Gyeonu/Prefabs/Naganeupseong";

        // 건물로 인정할 이름 패턴
        private static readonly string[] BuildingKeywords =
            { "House", "Tavern", "Smithy", "Mill", "Storehouse", "Shed", "Pavilion" };
        // 담장으로 분류할 이름 패턴
        private static readonly string[] WallKeywords = { "Wall", "Fence", "Gate", "Door" };
        // 아예 무시할 것 (데모 전용)
        private static readonly string[] IgnoreKeywords =
            { "Terrain", "SkySphere", "Camera", "Light", "Ground", "Hand_Mill" };

        // ═══════════════════════════════════════════════
        [MenuItem("이문록/집터 추출/1. 미리보기 (아무것도 안 만듦)", priority = 1)]
        public static void Preview() => Run(dryRun: true);

        [MenuItem("이문록/집터 추출/2. 프리팹 생성", priority = 2)]
        public static void Extract() => Run(dryRun: false);

        // ═══════════════════════════════════════════════
        private static void Run(bool dryRun)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) { Debug.LogError("[집터추출] 열린 씬이 없습니다."); return; }

            var roots = scene.GetRootGameObjects()
                             .Where(g => !MatchesAny(g.name, IgnoreKeywords))
                             .Where(g => g.GetComponentInChildren<Renderer>(true) != null)
                             .ToList();

            // ── 1. 건물(기준점) 골라내기 ──────────────────
            var anchors = roots.Where(g => MatchesAny(g.name, BuildingKeywords)).ToList();
            if (anchors.Count == 0)
            {
                Debug.LogError("[집터추출] 건물을 하나도 못 찾았습니다. BuildingKeywords를 확인하세요.");
                return;
            }

            // ── 2. 가까운 건물끼리 합쳐 집터 만들기 ────────
            var plots = MergeAnchors(anchors);

            // ── 3. 나머지를 가장 가까운 집터에 배정 ────────
            var claimed = new HashSet<GameObject>(plots.SelectMany(p => p.Members));
            var leftovers = new List<GameObject>();
            var excludedHeavy = new List<(string name, long tris)>();

            foreach (var go in roots)
            {
                if (claimed.Contains(go)) continue;

                long tris = CountTriangles(go);
                if (tris >= HeavyPropThreshold)
                {
                    excludedHeavy.Add((go.name, tris));
                    continue;
                }

                var plot = NearestPlot(plots, go, ClaimPadding);
                if (plot != null) { plot.Members.Add(go); claimed.Add(go); }
                else leftovers.Add(go);
            }

            // ── 4. 보고서 ────────────────────────────────
            var sb = new StringBuilder();
            sb.AppendLine($"# 집터 추출 {(dryRun ? "미리보기" : "결과")} — {scene.name}");
            sb.AppendLine();
            sb.AppendLine($"- 집터 {plots.Count}개");
            sb.AppendLine($"- 제외된 무거운 소품 {excludedHeavy.Count}개");
            sb.AppendLine($"- 어느 집터에도 안 붙은 것 {leftovers.Count}개");
            sb.AppendLine();
            sb.AppendLine("| 집터 이름 | 건물 | 담장 | 소품 | 삼각형 | 크기(m) |");
            sb.AppendLine("|---|---:|---:|---:|---:|---|");

            foreach (var p in plots.OrderByDescending(p => p.Members.Sum(CountTriangles)))
            {
                int b = p.Members.Count(m => MatchesAny(m.name, BuildingKeywords));
                int w = p.Members.Count(m => MatchesAny(m.name, WallKeywords));
                int pr = p.Members.Count - b - w;
                long t = p.Members.Sum(CountTriangles);
                var size = CombinedBounds(p.Members).size;
                sb.AppendLine($"| {p.Name} | {b} | {w} | {pr} | {t:N0} | {size.x:F1} x {size.y:F1} x {size.z:F1} |");
            }

            if (excludedHeavy.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"## 제외된 무거운 소품 ({HeavyPropThreshold:N0} 삼각형 이상)");
                sb.AppendLine();
                foreach (var (n, t) in excludedHeavy.OrderByDescending(x => x.tris))
                    sb.AppendLine($"- {n} — {t:N0}");
            }

            if (leftovers.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 집터에 안 붙은 것 (독립 배치용)");
                sb.AppendLine();
                foreach (var g in leftovers.OrderBy(g => g.name))
                    sb.AppendLine($"- {g.name} — {CountTriangles(g):N0}");
            }

            // ── 5. 실제 생성 ─────────────────────────────
            if (!dryRun)
            {
                EnsureFolder(OutputFolder);
                int made = 0;
                try
                {
                    for (int i = 0; i < plots.Count; i++)
                    {
                        EditorUtility.DisplayProgressBar("집터 추출",
                            $"{plots[i].Name} ({i + 1}/{plots.Count})", (float)i / plots.Count);
                        if (BuildPrefab(plots[i])) made++;
                    }
                }
                finally { EditorUtility.ClearProgressBar(); }

                sb.AppendLine();
                sb.AppendLine($"→ 프리팹 {made}개 생성: `{OutputFolder}`");
                AssetDatabase.Refresh();
            }

            var outPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                       $"PlotExtract_{scene.name}.md");
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));

            Debug.Log($"[집터추출] {(dryRun ? "미리보기 완료" : "생성 완료")} — 집터 {plots.Count}개\n{outPath}");
            if (!dryRun)
                Debug.LogWarning("[집터추출] 씬 저장 여부를 물으면 반드시 'Don't Save'를 누르세요.");

            EditorUtility.RevealInFinder(outPath);
        }

        // ═══════════════════════════════════════════════
        private class Plot
        {
            public string Name;
            public List<GameObject> Members = new List<GameObject>();
        }

        /// <summary>가까이 붙은 건물들을 한 집터로 합친다.</summary>
        private static List<Plot> MergeAnchors(List<GameObject> anchors)
        {
            var remaining = new List<GameObject>(anchors);
            var plots = new List<Plot>();

            while (remaining.Count > 0)
            {
                var seed = remaining[0];
                remaining.RemoveAt(0);
                var group = new List<GameObject> { seed };

                bool grew = true;
                while (grew)
                {
                    grew = false;
                    for (int i = remaining.Count - 1; i >= 0; i--)
                    {
                        var c = remaining[i];
                        if (group.Any(g => FlatDistance(g, c) <= AnchorMergeDistance))
                        {
                            group.Add(c);
                            remaining.RemoveAt(i);
                            grew = true;
                        }
                    }
                }

                // 가장 큰 건물 이름을 집터 이름으로
                var main = group.OrderByDescending(CountTriangles).First();
                plots.Add(new Plot { Name = Sanitize(main.name) + "_Set", Members = group });
            }

            // 이름 중복 방지
            var used = new HashSet<string>();
            foreach (var p in plots)
            {
                var n = p.Name; int k = 1;
                while (!used.Add(n)) n = $"{p.Name}_{++k}";
                p.Name = n;
            }
            return plots;
        }

        private static Plot NearestPlot(List<Plot> plots, GameObject go, float padding)
        {
            Plot best = null; float bestDist = float.MaxValue;
            var b = CombinedBounds(new List<GameObject> { go });

            foreach (var p in plots)
            {
                var pb = CombinedBounds(p.Members);
                var a = new Vector3(b.center.x, 0, b.center.z);
                var c = new Vector3(pb.center.x, 0, pb.center.z);
                float edge = Mathf.Max(0, Vector3.Distance(a, c)
                                        - new Vector2(pb.extents.x, pb.extents.z).magnitude
                                        - new Vector2(b.extents.x, b.extents.z).magnitude);
                if (edge <= padding && edge < bestDist) { bestDist = edge; best = p; }
            }
            return best;
        }

        /// <summary>복사본으로 프리팹을 만들고 복사본은 즉시 삭제한다.</summary>
        private static bool BuildPrefab(Plot plot)
        {
            var bounds = CombinedBounds(plot.Members);
            var pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            var root = new GameObject(plot.Name);
            root.transform.position = pivot;

            var gBuild = MakeChild(root, "Buildings");
            var gWall = MakeChild(root, "Walls");
            var gProp = MakeChild(root, "Props");

            foreach (var src in plot.Members)
            {
                var target = MatchesAny(src.name, BuildingKeywords) ? gBuild
                           : MatchesAny(src.name, WallKeywords) ? gWall
                           : gProp;

                var copy = Object.Instantiate(src);
                copy.name = src.name;                       // (Clone) 제거
                copy.transform.SetParent(target.transform, true);
            }

            var path = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{plot.Name}.prefab");
            var ok = PrefabUtility.SaveAsPrefabAsset(root, path) != null;

            Object.DestroyImmediate(root);                   // 씬에서 흔적 제거
            return ok;
        }

        // ─── 유틸 ───────────────────────────────────────
        private static GameObject MakeChild(GameObject parent, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent.transform, false);
            return g;
        }

        private static bool MatchesAny(string name, string[] keys) =>
            keys.Any(k => name.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0);

        private static string Sanitize(string s) =>
            string.Join("_", s.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");

        private static float FlatDistance(GameObject a, GameObject b)
        {
            var pa = CombinedBounds(new List<GameObject> { a }).center;
            var pb = CombinedBounds(new List<GameObject> { b }).center;
            return Vector2.Distance(new Vector2(pa.x, pa.z), new Vector2(pb.x, pb.z));
        }

        private static Bounds CombinedBounds(List<GameObject> objs)
        {
            Bounds? b = null;
            foreach (var go in objs)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (b == null) b = r.bounds;
                    else { var t = b.Value; t.Encapsulate(r.bounds); b = t; }
                }
            return b ?? new Bounds(Vector3.zero, Vector3.zero);
        }

        private static long CountTriangles(GameObject go)
        {
            long total = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh; if (m == null) continue;
                for (int i = 0; i < m.subMeshCount; i++) total += m.GetIndexCount(i) / 3;
            }
            return total;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
