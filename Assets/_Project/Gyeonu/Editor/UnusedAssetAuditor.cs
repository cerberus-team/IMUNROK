using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 참조되지 않는 에셋 목록을 뽑는다. <b>읽기 전용이다 — 무엇도 지우거나 옮기지 않는다.</b>
    ///
    /// 사용법: 상단 메뉴 → Tools → 이문록 → 도구 ▸ 미사용 에셋 감사 (보고서만)
    /// 결과:   docs/UnusedAssets_&lt;날짜&gt;.md
    ///
    /// 뿌리(root) — 여기서 출발해 닿는 것이 「쓰이는 에셋」이다
    ///   ① 빌드 설정에 등록된 씬 전부
    ///   ② 모든 Resources 폴더 안의 에셋 (경로로 로드되므로 참조가 안 보인다)
    ///   ③ ProjectSettings/*.asset 이 GUID 로 물고 있는 것 (렌더 파이프라인, 항상 포함 셰이더,
    ///      XR 설정, 입력 액션 등 — 넣지 않으면 통째로 오탐이 난다)
    ///   ④ PlayerSettings 의 Preloaded Assets
    ///
    /// 수집 방법 — 두 가지를 합집합으로 쓴다
    ///   · 씬은 AssetDatabase.GetDependencies 로 훑는다. 씬을 열지 않고도 재귀 의존성을
    ///     얻는다 (데모 씬을 실수로 저장할 위험이 없다 — 작업 원칙 1).
    ///   · 씬이 아닌 뿌리는 실제로 로드해 EditorUtility.CollectDependencies 를 태운다.
    ///
    /// 오탐이 나기 쉬운 것은 「확신도 낮음」으로 따로 표시한다. 사유는 보고서에 함께 적는다.
    /// </summary>
    public static class UnusedAssetAuditor
    {
        private const string MenuPath = "Tools/이문록/도구 ▸ 미사용 에셋 감사 (보고서만)";

        // 폴더별 상세 목록에서 한 폴더당 최대 몇 줄까지 적을지 (보고서가 무한정 길어지는 것을 막는다)
        private const int MaxLinesPerFolder = 40;
        private const int TopFileCount = 20;

        // ── 확신도 판정용 확장자 묶음 ─────────────────────────────
        private static readonly HashSet<string> CodeExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".dll", ".asmdef", ".asmref", ".rsp", ".jslib", ".jspre",
            ".a", ".so", ".dylib", ".aar", ".jar", ".java", ".mm", ".h", ".cpp", ".c"
        };

        private static readonly HashSet<string> ShaderExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".shader", ".shadergraph", ".shadersubgraph", ".hlsl", ".cginc",
            ".compute", ".glslinc", ".shadervariants", ".raytrace"
        };

        private static readonly HashSet<string> NonAssetExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".md", ".pdf", ".html", ".htm", ".rtf", ".csv", ".xml", ".yml", ".yaml",
            ".log", ".unitypackage", ".zip", ".7z", ".rar", ".blend", ".blend1",
            ".max", ".ma", ".mb", ".spp", ".psb", ".bat", ".sh", ".url", ".json", ".txt"
        };

        private static readonly HashSet<string> FontExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".ttf", ".otf", ".ttc", ".fontsettings"
        };

        private sealed class Entry
        {
            public string Path;
            public long Size;
            public bool LowConfidence;
            public string Reason;       // 확신도가 낮은 사유. 높으면 null
            public bool DemoOnly;       // 빌드 설정에 없는 씬에서만 참조됨
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;

            try
            {
                // ── ① 뿌리 모으기 ────────────────────────────────
                EditorUtility.DisplayProgressBar("미사용 에셋 감사", "뿌리를 모으는 중…", 0.05f);

                var rootScenes = new List<string>();
                int disabledScenes = 0;
                foreach (var s in EditorBuildSettings.scenes)
                {
                    if (string.IsNullOrEmpty(s.path)) continue;
                    if (!File.Exists(Path.Combine(projectRoot, s.path))) continue;
                    rootScenes.Add(Norm(s.path));
                    if (!s.enabled) disabledScenes++;
                }

                var resourceAssets = CollectResourceAssets();
                var settingsAssets = CollectProjectSettingsRefs(projectRoot);
                var preloaded = CollectPreloadedAssets();

                var objectRoots = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in resourceAssets) objectRoots.Add(p);
                foreach (var p in settingsAssets) objectRoots.Add(p);
                foreach (var p in preloaded) objectRoots.Add(p);

                // ── ② 참조 수집 ─────────────────────────────────
                var referenced = new HashSet<string>(StringComparer.Ordinal);

                EditorUtility.DisplayProgressBar("미사용 에셋 감사", "씬 의존성을 훑는 중…", 0.15f);
                AddDependencies(referenced, rootScenes);

                EditorUtility.DisplayProgressBar("미사용 에셋 감사", "Resources·설정 의존성을 훑는 중…", 0.3f);
                AddDependencies(referenced, objectRoots);
                CollectDependenciesByObject(referenced, objectRoots);

                // ── ③ 빌드 설정에 없는 씬 (따로 표시하려고 별도 집합) ──
                EditorUtility.DisplayProgressBar("미사용 에셋 감사", "미등록 씬을 훑는 중…", 0.45f);
                var registered = new HashSet<string>(rootScenes, StringComparer.Ordinal);
                var strayScenes = AssetDatabase.FindAssets("t:SceneAsset")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => !string.IsNullOrEmpty(p) && p.StartsWith("Assets/", StringComparison.Ordinal))
                    .Where(p => !registered.Contains(p))
                    .Distinct()
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .ToList();

                var strayReferenced = new HashSet<string>(StringComparer.Ordinal);
                AddDependencies(strayReferenced, strayScenes);

                // ── ④ 디스크의 전체 파일과 대조 ──────────────────
                EditorUtility.DisplayProgressBar("미사용 에셋 감사", "Assets 아래 전체 파일과 대조하는 중…", 0.6f);
                var allFiles = EnumerateAssetFiles();

                EditorUtility.DisplayProgressBar("미사용 에셋 감사", "코드 문자열을 훑는 중…", 0.7f);
                var codeTokens = CollectCodeStringTokens();

                var unused = new List<Entry>();
                long totalSize = 0, unusedSize = 0;
                foreach (var rel in allFiles)
                {
                    long size = SafeSize(projectRoot, rel);
                    totalSize += size;
                    if (referenced.Contains(rel)) continue;

                    var e = new Entry
                    {
                        Path = rel,
                        Size = size,
                        DemoOnly = strayReferenced.Contains(rel)
                    };
                    e.Reason = LowConfidenceReason(rel, codeTokens);
                    e.LowConfidence = e.Reason != null;
                    unused.Add(e);
                    unusedSize += size;
                }

                // ── ⑤ 보고서 ───────────────────────────────────
                EditorUtility.DisplayProgressBar("미사용 에셋 감사", "보고서를 쓰는 중…", 0.9f);
                var sb = BuildReport(unused, allFiles.Count, referenced.Count, totalSize, unusedSize,
                                     rootScenes, disabledScenes, resourceAssets.Count, settingsAssets.Count,
                                     preloaded.Count, strayScenes, strayReferenced.Count);

                var docsDir = Path.Combine(projectRoot, "docs");
                Directory.CreateDirectory(docsDir);
                var outPath = Path.Combine(docsDir, "UnusedAssets_" + DateTime.Now.ToString("yyyy-MM-dd") + ".md");
                File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));

                Debug.Log($"[미사용 에셋 감사] 전체 {allFiles.Count:N0}개 중 미참조 {unused.Count:N0}개 " +
                          $"({FormatSize(unusedSize)}). 확신도 높음 {unused.Count(x => !x.LowConfidence):N0}개.\n" +
                          $"보고서: {outPath}\n※ 아무것도 지우지 않았다.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ─────────────────────────────────────────────────────────
        // 뿌리 모으기
        // ─────────────────────────────────────────────────────────

        /// <summary>Resources 폴더 안의 것은 경로로 로드되므로 참조가 보이지 않는다 — 전부 뿌리로 잡는다.</summary>
        private static List<string> CollectResourceAssets()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { "Assets" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(p) || AssetDatabase.IsValidFolder(p)) continue;
                if (p.IndexOf("/Resources/", StringComparison.Ordinal) < 0) continue;
                result.Add(p);
            }
            return result.ToList();
        }

        /// <summary>
        /// ProjectSettings/*.asset 이 GUID 로 물고 있는 에셋. 렌더 파이프라인 에셋·항상 포함 셰이더·
        /// XR 설정·입력 액션이 여기 걸린다. 넣지 않으면 통째로 오탐이 난다. (읽기만 한다)
        /// </summary>
        private static List<string> CollectProjectSettingsRefs(string projectRoot)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var dir = Path.Combine(projectRoot, "ProjectSettings");
            if (!Directory.Exists(dir)) return result.ToList();

            var rx = new Regex("guid: ([0-9a-fA-F]{32})", RegexOptions.Compiled);
            foreach (var file in Directory.GetFiles(dir, "*.asset", SearchOption.TopDirectoryOnly))
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch { continue; }

                foreach (Match m in rx.Matches(text))
                {
                    var p = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                    if (!string.IsNullOrEmpty(p) && p.StartsWith("Assets/", StringComparison.Ordinal))
                        result.Add(p);
                }
            }
            return result.ToList();
        }

        private static List<string> CollectPreloadedAssets()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var o in PlayerSettings.GetPreloadedAssets())
            {
                if (o == null) continue;
                var p = AssetDatabase.GetAssetPath(o);
                if (!string.IsNullOrEmpty(p) && p.StartsWith("Assets/", StringComparison.Ordinal))
                    result.Add(p);
            }
            return result.ToList();
        }

        // ─────────────────────────────────────────────────────────
        // 참조 수집
        // ─────────────────────────────────────────────────────────

        private static void AddDependencies(HashSet<string> sink, IEnumerable<string> roots)
        {
            var list = roots.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            const int chunk = 64;   // 한 번에 너무 많이 넣으면 에디터가 오래 멈춘다
            for (int i = 0; i < list.Count; i += chunk)
            {
                var slice = list.Skip(i).Take(chunk).ToArray();
                foreach (var dep in AssetDatabase.GetDependencies(slice, true))
                    if (dep.StartsWith("Assets/", StringComparison.Ordinal))
                        sink.Add(Norm(dep));
            }
        }

        /// <summary>요청대로 EditorUtility.CollectDependencies 도 태운다. 경로 기반이 놓치는 것을 합집합으로 덮는다.</summary>
        private static void CollectDependenciesByObject(HashSet<string> sink, IEnumerable<string> roots)
        {
            var objs = new List<UnityEngine.Object>();
            foreach (var p in roots)
            {
                if (p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) continue;  // 씬은 열지 않는다
                var o = AssetDatabase.LoadMainAssetAtPath(p);
                if (o != null) objs.Add(o);
            }
            if (objs.Count == 0) return;

            foreach (var dep in EditorUtility.CollectDependencies(objs.ToArray()))
            {
                if (dep == null) continue;
                var p = AssetDatabase.GetAssetPath(dep);
                if (!string.IsNullOrEmpty(p) && p.StartsWith("Assets/", StringComparison.Ordinal))
                    sink.Add(Norm(p));
            }
        }

        // ─────────────────────────────────────────────────────────
        // 디스크 훑기
        // ─────────────────────────────────────────────────────────

        private static List<string> EnumerateAssetFiles()
        {
            var dataPath = Application.dataPath.Replace('\\', '/');
            var result = new List<string>(16384);

            foreach (var full in Directory.EnumerateFiles(dataPath, "*", SearchOption.AllDirectories))
            {
                var norm = full.Replace('\\', '/');
                var rel = "Assets" + norm.Substring(dataPath.Length);
                if (IsIgnoredByUnity(rel)) continue;
                result.Add(rel);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>Unity 가 애초에 에셋으로 취급하지 않는 것. 세면 숫자만 부푼다.</summary>
        private static bool IsIgnoredByUnity(string rel)
        {
            if (rel.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return true;

            var name = Path.GetFileName(rel);
            if (name.StartsWith(".", StringComparison.Ordinal)) return true;
            if (name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return true;

            // 폴더 이름이 '~' 로 끝나거나 '.' 으로 시작하면 Unity 가 통째로 무시한다
            var parts = rel.Split('/');
            for (int i = 0; i < parts.Length - 1; i++)
                if (parts[i].EndsWith("~", StringComparison.Ordinal) || parts[i].StartsWith(".", StringComparison.Ordinal))
                    return true;

            return false;
        }

        /// <summary>
        /// Assets 아래 모든 .cs 의 문자열 리터럴에서 토큰을 뽑는다.
        /// 이 프로젝트는 배치를 에디터 스크립트로 하므로 LoadAssetAtPath("…/외삼문.fbx") 처럼
        /// 경로·이름 문자열로만 잡는 에셋이 많다. 의존성 그래프에는 절대 안 잡히는 참조다.
        /// </summary>
        private static HashSet<string> CollectCodeStringTokens()
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rx = new Regex("\"([^\"\\\\\\r\\n]{2,300})\"", RegexOptions.Compiled);
            var sep = new[] { '/', '\\', '.', ' ', '\t', ',', ';', ':', '(', ')', '=', '|', '*' };

            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(p) || !p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;

                string text;
                try { text = File.ReadAllText(p); }
                catch { continue; }

                foreach (Match m in rx.Matches(text))
                {
                    var lit = m.Groups[1].Value;
                    tokens.Add(lit);
                    foreach (var t in lit.Split(sep, StringSplitOptions.RemoveEmptyEntries))
                        if (t.Length >= 3) tokens.Add(t);
                }
            }
            return tokens;
        }

        // ─────────────────────────────────────────────────────────
        // 확신도 판정
        // ─────────────────────────────────────────────────────────

        /// <summary>확신도가 낮으면 사유를, 높으면 null 을 준다.</summary>
        private static string LowConfidenceReason(string rel, HashSet<string> codeTokens)
        {
            var ext = Path.GetExtension(rel);
            var name = Path.GetFileName(rel);
            var stem = Path.GetFileNameWithoutExtension(rel);

            if (rel.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                rel.IndexOf("/Editor Default Resources/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                rel.IndexOf("/Gizmos/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "에디터 전용 경로 — 씬·Resources 에서 출발하는 의존성에는 원래 안 잡힌다";

            if (CodeExt.Contains(ext))
                return "스크립트·어셈블리 — 다른 코드에서만 참조될 수 있다";

            if (ShaderExt.Contains(ext))
                return "셰이더·인클루드 — 서브셰이더·Shader.Find·폴백 참조는 의존성에 안 잡힌다";

            if (rel.StartsWith("Assets/StreamingAssets/", StringComparison.OrdinalIgnoreCase))
                return "StreamingAssets — 실행 중에 경로로 읽는다";

            if (string.Equals(ext, ".unity", StringComparison.OrdinalIgnoreCase))
                return "씬 — 빌드 설정에 등록되지 않았을 뿐일 수 있다";

            if (FontExt.Contains(ext))
                return "글꼴 — 굽기 도구가 이름으로 찾아 쓴다";

            if (name.StartsWith("Lightmap-", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("ReflectionProbe-", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("LightingData.asset", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".lighting", StringComparison.OrdinalIgnoreCase))
                return "라이팅 굽기 산출물 — 씬 라이팅 설정에 매여 있다";

            if (string.Equals(ext, ".spriteatlas", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".preset", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".signature", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".inputactions", StringComparison.OrdinalIgnoreCase))
                return "설정성 에셋 — 임포터·설정 창이 물고 있을 수 있다";

            if (NonAssetExt.Contains(ext))
                return "Unity 참조 대상이 아닌 파일 (문서·설정·DCC 원본)";

            if (stem.Length >= 3 && codeTokens.Contains(stem))
                return "코드 문자열에 이름이 등장 — 에디터 빌더가 경로·이름으로 찾는 것일 수 있다";

            if (codeTokens.Contains(rel))
                return "코드 문자열에 경로가 그대로 등장";

            return null;
        }

        // ─────────────────────────────────────────────────────────
        // 보고서
        // ─────────────────────────────────────────────────────────

        private static StringBuilder BuildReport(
            List<Entry> unused, int totalFiles, int referencedCount, long totalSize, long unusedSize,
            List<string> rootScenes, int disabledScenes, int resourceCount, int settingsCount,
            int preloadedCount, List<string> strayScenes, int strayRefCount)
        {
            var sb = new StringBuilder();
            var high = unused.Where(e => !e.LowConfidence).ToList();
            var low = unused.Where(e => e.LowConfidence).ToList();

            sb.AppendLine("# 미사용 에셋 감사 — " + DateTime.Now.ToString("yyyy-MM-dd"));
            sb.AppendLine();
            sb.AppendLine("> **이 문서는 목록일 뿐이다. 도구는 아무것도 지우거나 옮기지 않는다.**");
            sb.AppendLine("> 지우기 전에 반드시 개별 확인할 것 — 아래 「함정」 절을 먼저 읽어라.");
            sb.AppendLine();
            sb.AppendLine("생성: `Tools ▸ 이문록 ▸ 도구 ▸ 미사용 에셋 감사 (보고서만)` · " +
                          DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine();

            // ── 요약 ──
            sb.AppendLine("## 요약");
            sb.AppendLine();
            sb.AppendLine("| 항목 | 값 |");
            sb.AppendLine("|---|---:|");
            sb.AppendLine($"| Assets 아래 전체 파일 | {totalFiles:N0}개 · {FormatSize(totalSize)} |");
            sb.AppendLine($"| 뿌리에서 닿은 에셋 | {referencedCount:N0}개 |");
            sb.AppendLine($"| **참조되지 않은 파일** | **{unused.Count:N0}개 · {FormatSize(unusedSize)}** |");
            sb.AppendLine($"| ├ 확신도 높음 | {high.Count:N0}개 · {FormatSize(high.Sum(e => e.Size))} |");
            sb.AppendLine($"| └ 확신도 낮음 (오탐 주의) | {low.Count:N0}개 · {FormatSize(low.Sum(e => e.Size))} |");
            sb.AppendLine($"| 미등록 씬에서만 참조됨 | {unused.Count(e => e.DemoOnly):N0}개 · " +
                          $"{FormatSize(unused.Where(e => e.DemoOnly).Sum(e => e.Size))} |");
            sb.AppendLine();

            // ── 방법 ──
            sb.AppendLine("## 어떻게 셌는가");
            sb.AppendLine();
            sb.AppendLine("**뿌리(root)** — 여기서 출발해 닿으면 「쓰이는 것」이다.");
            sb.AppendLine();
            sb.AppendLine($"- 빌드 설정에 등록된 씬 **{rootScenes.Count}개**" +
                          (disabledScenes > 0 ? $" (그중 체크 해제 {disabledScenes}개도 포함해서 셌다)" : string.Empty));
            sb.AppendLine($"- `Resources` 폴더 안의 에셋 **{resourceCount}개** — 경로로 로드되므로 참조가 안 보인다");
            sb.AppendLine($"- `ProjectSettings/*.asset` 이 GUID 로 물고 있는 에셋 **{settingsCount}개** — 렌더 파이프라인·항상 포함 셰이더·XR 설정·입력 액션. 넣지 않으면 통째로 오탐이 난다");
            sb.AppendLine($"- PlayerSettings 의 Preloaded Assets **{preloadedCount}개**");
            sb.AppendLine();
            sb.AppendLine("**수집** — 두 가지를 합집합으로 썼다.");
            sb.AppendLine();
            sb.AppendLine("- 씬은 `AssetDatabase.GetDependencies(paths, recursive)` 로 훑었다. 씬을 열지 않고도 재귀 의존성을 얻는다 — 데모 씬을 실수로 저장할 위험이 없다 (작업 원칙 1).");
            sb.AppendLine("- 씬이 아닌 뿌리는 실제로 로드해 `EditorUtility.CollectDependencies` 를 태웠다.");
            sb.AppendLine();

            // ── 경고: 미등록 씬 ──
            sb.AppendLine("## ⚠ 뿌리에서 빠진 씬");
            sb.AppendLine();
            if (strayScenes.Count == 0)
            {
                sb.AppendLine("디스크의 모든 씬이 빌드 설정에 등록돼 있다.");
            }
            else
            {
                sb.AppendLine($"빌드 설정에 없는 씬이 **{strayScenes.Count}개** 있다. 이 씬들이 쓰는 에셋 {strayRefCount:N0}개는");
                sb.AppendLine("위 뿌리에서 닿지 않으므로 「미참조」로 잡힌다 — 목록에서 **`[미등록씬]`** 표시가 붙은 것이 그것이다.");
                sb.AppendLine("에셋 팩 데모 씬이면 무시해도 되지만, **우리 씬이 여기 끼어 있으면 등록부터 해야 한다.**");
                sb.AppendLine();
                foreach (var p in strayScenes)
                {
                    var ours = p.StartsWith("Assets/_Project/", StringComparison.Ordinal);
                    sb.AppendLine($"- {(ours ? "**⚠ 우리 씬** " : string.Empty)}`{p}`");
                }
            }
            sb.AppendLine();

            // ── 함정 ──
            sb.AppendLine("## 함정 — 「확신도 낮음」을 붙인 이유");
            sb.AppendLine();
            sb.AppendLine("의존성 그래프는 **직렬화된 참조만** 본다. 아래는 원리상 절대 안 잡히므로, 목록에 떴다고 해서 안 쓰이는 게 아니다.");
            sb.AppendLine();
            sb.AppendLine("| 종류 | 왜 안 잡히나 |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| 에디터 전용 경로 | 씬·Resources 에서 출발하는 경로에 애초에 없다 |");
            sb.AppendLine("| 스크립트·어셈블리 | 다른 코드가 `using`·`new` 로만 쓰면 에셋 참조가 아니다 |");
            sb.AppendLine("| 셰이더·인클루드 | 서브셰이더·`Shader.Find`·폴백·`#include` 는 참조가 아니다 |");
            sb.AppendLine("| StreamingAssets | 실행 중에 파일 경로로 읽는다 |");
            sb.AppendLine("| 글꼴 | 굽기 도구가 이름으로 찾아 쓴다 (`GyeonuFontBaker`) |");
            sb.AppendLine("| 라이팅 굽기 산출물 | 씬 라이팅 설정에 매여 있다 |");
            sb.AppendLine("| 코드 문자열에 이름이 등장 | **이 프로젝트는 배치를 에디터 스크립트로 한다** — `LoadAssetAtPath(\"…\")` 로만 잡는 에셋이 많다 |");
            sb.AppendLine();
            sb.AppendLine("반대로 「확신도 높음」도 **삭제 승인이 아니다.** 아직 안 쓴 후보 에셋, 팀원이 곧 쓸 전달본, 다음 작업에 필요한 것이 섞여 있다.");
            sb.AppendLine();

            // ── 구역별 합계 ──
            sb.AppendLine("## 구역별 합계");
            sb.AppendLine();
            sb.AppendLine("`Assets/_Project/…` 은 우리(팀) 작업물이고, 나머지 최상위 폴더는 임포트한 에셋 팩이다 (대부분 gitignore).");
            sb.AppendLine();
            sb.AppendLine("| 구역 | 미참조 | 용량 | 확신 높음 | 확신 낮음 | 미등록씬 |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|");

            var byZone = unused.GroupBy(e => Zone(e.Path))
                               .Select(g => new { Key = g.Key, Items = g.ToList() })
                               .OrderByDescending(g => g.Items.Sum(e => e.Size));
            foreach (var g in byZone)
            {
                sb.AppendLine($"| `{g.Key}` | {g.Items.Count:N0} | {FormatSize(g.Items.Sum(e => e.Size))} " +
                              $"| {g.Items.Count(e => !e.LowConfidence):N0} | {g.Items.Count(e => e.LowConfidence):N0} " +
                              $"| {g.Items.Count(e => e.DemoOnly):N0} |");
            }
            sb.AppendLine();

            // ── 상위 파일 ──
            sb.AppendLine($"## 확신도 높은 미참조 — 용량 상위 {TopFileCount}");
            sb.AppendLine();
            if (high.Count == 0)
            {
                sb.AppendLine("없음.");
            }
            else
            {
                sb.AppendLine("| # | 용량 | 파일 | 비고 |");
                sb.AppendLine("|---:|---:|---|---|");
                int i = 1;
                foreach (var e in high.OrderByDescending(x => x.Size).Take(TopFileCount))
                    sb.AppendLine($"| {i++} | {FormatSize(e.Size)} | `{e.Path}` | {(e.DemoOnly ? "미등록씬에서 쓰임" : string.Empty)} |");
            }
            sb.AppendLine();

            // ── 낮은 확신도 사유별 ──
            sb.AppendLine("## 확신도 낮음 — 사유별");
            sb.AppendLine();
            sb.AppendLine("| 사유 | 개수 | 용량 |");
            sb.AppendLine("|---|---:|---:|");
            foreach (var g in low.GroupBy(e => e.Reason).OrderByDescending(g => g.Sum(e => e.Size)))
                sb.AppendLine($"| {g.Key} | {g.Count():N0} | {FormatSize(g.Sum(e => e.Size))} |");
            sb.AppendLine();

            // ── 폴더별 상세 ──
            sb.AppendLine("## 폴더별 상세");
            sb.AppendLine();
            sb.AppendLine($"한 폴더당 최대 {MaxLinesPerFolder}줄까지만 적는다. `[낮음]` 은 오탐 주의, `[미등록씬]` 은 빌드 설정에 없는 씬이 쓰는 것.");
            sb.AppendLine();

            var byFolder = unused.GroupBy(e => Folder(e.Path))
                                 .Select(g => new { Key = g.Key, Items = g.ToList() })
                                 .OrderByDescending(g => g.Items.Sum(e => e.Size));
            foreach (var g in byFolder)
            {
                sb.AppendLine($"### `{g.Key}` — {g.Items.Count:N0}개 · {FormatSize(g.Items.Sum(e => e.Size))}");
                sb.AppendLine();
                foreach (var e in g.Items.OrderByDescending(x => x.Size).Take(MaxLinesPerFolder))
                {
                    var tags = new StringBuilder();
                    if (e.LowConfidence) tags.Append(" `[낮음: " + e.Reason + "]`");
                    if (e.DemoOnly) tags.Append(" `[미등록씬]`");
                    sb.AppendLine($"- `{Path.GetFileName(e.Path)}` — {FormatSize(e.Size)}{tags}");
                }
                if (g.Items.Count > MaxLinesPerFolder)
                    sb.AppendLine($"- …외 {g.Items.Count - MaxLinesPerFolder:N0}개");
                sb.AppendLine();
            }

            return sb;
        }

        // ─────────────────────────────────────────────────────────
        // 잡일
        // ─────────────────────────────────────────────────────────

        private static string Norm(string p)
        {
            return p.Replace('\\', '/');
        }

        private static string Folder(string rel)
        {
            int i = rel.LastIndexOf('/');
            return i < 0 ? rel : rel.Substring(0, i);
        }

        /// <summary>우리 작업물은 3단계(`Assets/_Project/Gyeonu`)까지, 외부 팩은 2단계까지 묶는다.</summary>
        private static string Zone(string rel)
        {
            var parts = rel.Split('/');
            int depth = (parts.Length > 1 && parts[1] == "_Project") ? 3 : 2;
            depth = Math.Min(depth, parts.Length - 1);
            return string.Join("/", parts.Take(Math.Max(depth, 1)));
        }

        private static long SafeSize(string projectRoot, string rel)
        {
            try
            {
                var fi = new FileInfo(Path.Combine(projectRoot, rel));
                return fi.Exists ? fi.Length : 0L;
            }
            catch { return 0L; }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024 * 1024)).ToString("N2") + " GB";
            if (bytes >= 1024L * 1024) return (bytes / (1024.0 * 1024)).ToString("N1") + " MB";
            if (bytes >= 1024L) return (bytes / 1024.0).ToString("N0") + " KB";
            return bytes + " B";
        }
    }
}
