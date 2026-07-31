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
    /// 데모 씬에서 재사용 가능한 프리팹을 "포함 관계"로 뽑아낸다.
    ///
    /// 묶는 규칙 (거리 기반 아님 — 거리로 하면 사슬 현상으로 전부 한 덩어리가 됨)
    ///   - 기준점(anchor): 건물(House/Tavern/Smithy/Ox_Mill), 받침(평상·멍석·소반)
    ///   - 소품의 XZ 중심이 기준점의 바닥 면적(bounds) 안에 들어오면 그 소속
    ///   - 여러 기준점에 겹치면 바닥 면적이 더 작은 쪽에 준다
    ///   - 어느 기준점에도 안 들어가면 낱개(Parts)
    ///
    /// 출력
    ///   Buildings/  건물 1채 + 그 안의 소품 (건물끼리 합치지 않음)
    ///               건물 범위 안의 받침은 얹힌 물건째 건물에 포함
    ///   Props/      야외 받침 + 위에 얹힌 물건 (얹힌 게 없으면 Parts로 강등)
    ///   Parts/      낱개 소품·담장(Wall01c/02c/03c)·문(Door01k) — 이름별 대표 1개
    ///
    /// 규칙
    ///   - 담장·문은 어떤 프리팹에도 포함하지 않는다 (마을 배치가 달라 직접 깐다)
    ///   - 삼각형 1만 이상 소품은 제외하고 보고
    ///   - 피벗은 각 프리팹의 바닥 중앙
    ///
    /// ⚠ 실행 후 씬 저장 여부를 물으면 반드시 "Don't Save"
    /// </summary>
    public static class PlotExtractor
    {
        // ─── 조절 가능한 값 ─────────────────────────────
        // 이 삼각형 수 이상의 소품은 제외한다
        private const int HeavyPropThreshold = 10000;
        // 프리팹 저장 위치
        private const string OutputFolder = "Assets/_Project/Gyeonu/Prefabs/Naganeupseong";

        // 건물 기준점 이름 패턴 (Hand_Mill(맷돌)은 소품이므로 "Mill" 대신 "Ox_Mill"로 한정)
        private static readonly string[] BuildingKeywords =
            { "House", "Tavern", "Smithy", "Ox_Mill" };
        // 받침 기준점 이름 패턴 (위에 물건을 얹는 판형)
        private static readonly string[] PlatformKeywords =
            { "Low_Wooden_Bench", "Straw_Mat", "Mat", "Small_Dining_Table" };
        // 담장·문 — 어느 프리팹에도 포함하지 않고 Parts로만
        private static readonly string[] WallKeywords = { "Wall", "Door" };
        // 아예 무시할 것 (데모 전용)
        private static readonly string[] IgnoreKeywords =
            { "Terrain", "SkySphere", "Camera", "Light", "Ground" };

        // ═══════════════════════════════════════════════
        [MenuItem("이문록/집터 추출/1. 미리보기 (아무것도 안 만듦)", priority = 1)]
        public static void Preview() => Run(dryRun: true);

        [MenuItem("이문록/집터 추출/2. 프리팹 생성", priority = 2)]
        public static void Extract() => Run(dryRun: false);

        // ═══════════════════════════════════════════════
        private class Anchor
        {
            public GameObject Root;
            public bool IsBuilding;          // true=건물, false=받침
            public Rect Footprint;           // XZ 바닥 면적
            public List<GameObject> Claimed = new List<GameObject>();
            public float Area => Footprint.width * Footprint.height;
        }

        private static void Run(bool dryRun)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) { Debug.LogError("[집터추출] 열린 씬이 없습니다."); return; }

            var roots = scene.GetRootGameObjects()
                             .Where(g => !MatchesAny(g.name, IgnoreKeywords))
                             .Where(g => g.GetComponentInChildren<Renderer>(true) != null)
                             .ToList();

            // ── 1. 분류: 기준점 / 담장 / 소품 ──────────────
            var anchors = new List<Anchor>();
            var walls = new List<GameObject>();
            var props = new List<GameObject>();

            foreach (var go in roots)
            {
                if (MatchesAny(go.name, BuildingKeywords))
                    anchors.Add(MakeAnchor(go, isBuilding: true));
                else if (MatchesAny(go.name, WallKeywords))
                    walls.Add(go);                       // 담장·문은 claim 대상에서 제외
                else if (MatchesAny(go.name, PlatformKeywords))
                    anchors.Add(MakeAnchor(go, isBuilding: false));
                else
                    props.Add(go);
            }

            if (anchors.Count == 0)
            {
                Debug.LogError("[집터추출] 기준점을 하나도 못 찾았습니다. 키워드를 확인하세요.");
                return;
            }

            // ── 2. 포함 관계로 소품 배정 ──────────────────
            var excludedHeavy = new List<(string name, long tris)>();
            var looseParts = new List<GameObject>();

            foreach (var go in props)
            {
                long tris = CountTriangles(go);
                if (tris >= HeavyPropThreshold)
                {
                    excludedHeavy.Add((go.name, tris));
                    continue;
                }

                var center = CombinedBounds(go).center;
                var p = new Vector2(center.x, center.z);

                // 포함하는 기준점 중 바닥 면적이 가장 작은 쪽
                Anchor best = null;
                foreach (var a in anchors)
                    if (a.Footprint.Contains(p) && (best == null || a.Area < best.Area))
                        best = a;

                if (best != null) best.Claimed.Add(go);
                else looseParts.Add(go);
            }

            // 건물 범위 안의 받침은 얹힌 물건째 그 건물에 포함시킨다
            foreach (var pf in anchors.Where(a => !a.IsBuilding).ToList())
            {
                var c = FlatCenter(pf.Root);
                Anchor host = null;
                foreach (var b in anchors.Where(x => x.IsBuilding))
                    if (b.Footprint.Contains(c) && (host == null || b.Area < host.Area))
                        host = b;
                if (host != null)
                {
                    host.Claimed.Add(pf.Root);
                    host.Claimed.AddRange(pf.Claimed);
                    anchors.Remove(pf);
                }
            }

            // 얹힌 게 없는 야외 받침은 낱개 소품으로 강등
            var emptyPlatforms = anchors.Where(a => !a.IsBuilding && a.Claimed.Count == 0).ToList();
            foreach (var a in emptyPlatforms) { anchors.Remove(a); looseParts.Add(a.Root); }

            var buildings = anchors.Where(a => a.IsBuilding).ToList();
            var platforms = anchors.Where(a => !a.IsBuilding).ToList();

            // Parts는 이름별 대표 1개 (Wall01c ×208 → 프리팹 1개)
            var partReps = walls.Concat(looseParts)
                                .GroupBy(g => BaseName(g.name))
                                .OrderBy(g => g.Key)
                                .ToList();

            // ── 3. 보고서 ────────────────────────────────
            var sb = new StringBuilder();
            sb.AppendLine($"# 집터 추출 {(dryRun ? "미리보기" : "결과")} — {scene.name}");
            sb.AppendLine();
            sb.AppendLine($"- Buildings: 건물 {buildings.Count}채 (각각 별도 프리팹)");
            sb.AppendLine($"- Props: 받침 단위 {platforms.Count}개");
            sb.AppendLine($"- Parts: 이름별 대표 {partReps.Count}종 (담장 포함, 인스턴스 총 {walls.Count + looseParts.Count}개)");
            sb.AppendLine($"- 제외된 무거운 소품: {excludedHeavy.Count}개");
            sb.AppendLine();

            sb.AppendLine("## Buildings/ — 건물 + 포함 소품");
            sb.AppendLine();
            foreach (var a in buildings.OrderBy(a => a.Root.name))
            {
                long t = CountTriangles(a.Root) + a.Claimed.Sum(CountTriangles);
                sb.AppendLine($"### {a.Root.name} — 소품 {a.Claimed.Count}개, 삼각형 {t:N0}, 바닥 {a.Footprint.width:F1} x {a.Footprint.height:F1} m");
                foreach (var grp in a.Claimed.GroupBy(g => BaseName(g.name)).OrderBy(g => g.Key))
                    sb.AppendLine($"- {grp.Key} x{grp.Count()}");
                sb.AppendLine();
            }

            sb.AppendLine("## Props/ — 받침 + 얹힌 물건");
            sb.AppendLine();
            foreach (var a in platforms.OrderBy(a => a.Root.name))
            {
                var host = buildings.FirstOrDefault(b =>
                    b.Footprint.Contains(FlatCenter(a.Root)));
                var where = host != null ? $" (위치: {host.Root.name} 범위 안)" : "";
                sb.AppendLine($"### {a.Root.name}{where}");
                foreach (var grp in a.Claimed.GroupBy(g => BaseName(g.name)).OrderBy(g => g.Key))
                    sb.AppendLine($"- {grp.Key} x{grp.Count()}");
                sb.AppendLine();
            }

            sb.AppendLine("## Parts/ — 낱개 (이름별 대표 1개)");
            sb.AppendLine();
            foreach (var grp in partReps)
                sb.AppendLine($"- {grp.Key} (인스턴스 {grp.Count()}개, 개당 {CountTriangles(grp.First()):N0} 삼각형)");

            if (excludedHeavy.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"## 제외 — 삼각형 {HeavyPropThreshold:N0} 이상");
                sb.AppendLine();
                foreach (var grp in excludedHeavy.GroupBy(x => BaseName(x.name)).OrderByDescending(g => g.Max(x => x.tris)))
                    sb.AppendLine($"- {grp.Key} x{grp.Count()} — 개당 최대 {grp.Max(x => x.tris):N0}");
            }

            // ── 4. 실제 생성 ─────────────────────────────
            if (!dryRun)
            {
                EnsureFolder($"{OutputFolder}/Buildings");
                EnsureFolder($"{OutputFolder}/Props");
                EnsureFolder($"{OutputFolder}/Parts");

                int made = 0, total = buildings.Count + platforms.Count + partReps.Count, done = 0;
                try
                {
                    foreach (var a in buildings)
                    {
                        Progress(a.Root.name, ++done, total);
                        if (BuildAnchorPrefab(a, $"{OutputFolder}/Buildings")) made++;
                    }
                    int seq = 0;
                    foreach (var a in platforms)
                    {
                        Progress(a.Root.name, ++done, total);
                        if (BuildAnchorPrefab(a, $"{OutputFolder}/Props", $"{Sanitize(BaseName(a.Root.name))}_Set_{++seq:D2}")) made++;
                    }
                    foreach (var grp in partReps)
                    {
                        Progress(grp.Key, ++done, total);
                        if (BuildSinglePrefab(grp.First(), $"{OutputFolder}/Parts", Sanitize(grp.Key))) made++;
                    }
                }
                finally { EditorUtility.ClearProgressBar(); }

                sb.AppendLine();
                sb.AppendLine($"→ 프리팹 {made}개 생성: `{OutputFolder}`");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            var outPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                       $"PlotExtract_{scene.name}.md");
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));

            Debug.Log($"[집터추출] {(dryRun ? "미리보기 완료" : "생성 완료")} — 건물 {buildings.Count} / 받침 {platforms.Count} / 낱개 {partReps.Count}종\n{outPath}");
            if (!dryRun)
                Debug.LogWarning("[집터추출] 씬 저장 여부를 물으면 반드시 'Don't Save'를 누르세요.");
        }

        // ═══════════════════════════════════════════════
        private static Anchor MakeAnchor(GameObject go, bool isBuilding)
        {
            var b = CombinedBounds(go);
            return new Anchor
            {
                Root = go,
                IsBuilding = isBuilding,
                Footprint = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z),
            };
        }

        /// <summary>기준점 + 소속 소품을 복사해 프리팹으로 저장하고 복사본은 즉시 삭제한다.</summary>
        private static bool BuildAnchorPrefab(Anchor a, string folder, string overrideName = null)
        {
            var all = new List<GameObject> { a.Root };
            all.AddRange(a.Claimed);
            var bounds = CombinedBounds(all);
            var pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            var name = overrideName ?? Sanitize(a.Root.name);
            var root = new GameObject(name);
            root.transform.position = pivot;

            CopyInto(a.Root, root.transform);
            if (a.Claimed.Count > 0)
            {
                var gProp = new GameObject("Props");
                gProp.transform.SetParent(root.transform, false);
                foreach (var src in a.Claimed) CopyInto(src, gProp.transform);
            }

            return SaveAndDestroy(root, folder, name);
        }

        /// <summary>낱개 오브젝트 하나를 프리팹으로 저장한다.</summary>
        private static bool BuildSinglePrefab(GameObject src, string folder, string name)
        {
            var b = CombinedBounds(src);
            var pivot = new Vector3(b.center.x, b.min.y, b.center.z);

            var root = new GameObject(name);
            root.transform.position = pivot;
            CopyInto(src, root.transform);

            return SaveAndDestroy(root, folder, name);
        }

        private static void CopyInto(GameObject src, Transform parent)
        {
            var copy = Object.Instantiate(src);
            copy.name = src.name;                        // (Clone) 제거
            copy.transform.SetParent(parent, true);      // 월드 위치 유지
        }

        private static bool SaveAndDestroy(GameObject root, string folder, string name)
        {
            root.transform.position = Vector3.zero;      // 피벗을 원점으로 (자식은 상대 오프셋 유지)
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.prefab");
            var ok = PrefabUtility.SaveAsPrefabAsset(root, path) != null;
            Object.DestroyImmediate(root);               // 씬에서 흔적 제거
            return ok;
        }

        private static void Progress(string label, int done, int total) =>
            EditorUtility.DisplayProgressBar("집터 추출", $"{label} ({done}/{total})", (float)done / total);

        // ─── 유틸 ───────────────────────────────────────
        private static bool MatchesAny(string name, string[] keys) =>
            keys.Any(k => name.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>"Wall01c (3)" → "Wall01c"</summary>
        private static string BaseName(string s) =>
            System.Text.RegularExpressions.Regex.Replace(s, @"\s*\(\d+\)$", "").Trim();

        private static string Sanitize(string s) =>
            string.Join("_", s.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");

        private static Vector2 FlatCenter(GameObject go)
        {
            var c = CombinedBounds(go).center;
            return new Vector2(c.x, c.z);
        }

        private static Bounds CombinedBounds(GameObject go) =>
            CombinedBounds(new List<GameObject> { go });

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
