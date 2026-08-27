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
    /// 열린 씬의 하이어라키를 텍스트로 덤프한다. 씬을 수정하지 않는다 (읽기 전용).
    ///
    /// 사용법: 상단 메뉴 → 이문록 → 도구 → 하이어라키 덤프
    /// 결과: 프로젝트 루트에 HierarchyDump_<씬이름>.md 생성 후 탐색기로 열림
    ///
    /// 표시 기호
    ///   [G]  그룹 — 메시 없이 자식만 있음 (묶는 용도의 빈 오브젝트)
    ///   [M]  메시 — 자기 자신이 3D 조각. 자식 없음
    ///   [GM] 둘 다 — 자기도 메시고 자식도 있음
    ///   [-]  메시도 자식도 없음 (라이트, 카메라 등)
    /// </summary>
    public static class HierarchyDumper
    {
        // 트리를 이 깊이까지만 개별 출력한다. 더 깊으면 개수만 표시.
        private const int MaxDepth = 3;

        [MenuItem("이문록/도구/하이어라키 덤프")]
        public static void Dump()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[HierarchyDumper] 열린 씬이 없습니다.");
                return;
            }

            var roots = scene.GetRootGameObjects();
            var sb = new StringBuilder();

            // ── 전체 통계 ────────────────────────────────
            int totalObjects = 0;
            long totalTris = 0;
            foreach (var r in roots)
            {
                totalObjects += r.GetComponentsInChildren<Transform>(true).Length;
                totalTris += CountTriangles(r);
            }

            sb.AppendLine($"# Hierarchy Dump — {scene.name}");
            sb.AppendLine();
            sb.AppendLine($"- Scene: `{scene.path}`");
            sb.AppendLine($"- 루트 오브젝트: {roots.Length}개");
            sb.AppendLine($"- 전체 오브젝트: {totalObjects:N0}개");
            sb.AppendLine($"- 전체 삼각형: {totalTris:N0}");
            sb.AppendLine();

            // ── 요약표 (제일 중요. 여기만 봐도 판단 가능) ──
            sb.AppendLine("## 루트 요약 (삼각형 많은 순)");
            sb.AppendLine();
            sb.AppendLine("| 이름 | 종류 | 직계자식 | 전체자식 | 삼각형 | 크기(m) |");
            sb.AppendLine("|---|---|---:|---:|---:|---|");

            var summary = roots
                .Select(r => new
                {
                    Obj = r,
                    Kind = Classify(r),
                    Direct = r.transform.childCount,
                    All = r.GetComponentsInChildren<Transform>(true).Length - 1,
                    Tris = CountTriangles(r),
                    Size = GetWorldSize(r)
                })
                .OrderByDescending(x => x.Tris);

            foreach (var s in summary)
                sb.AppendLine($"| {s.Obj.name} | {s.Kind} | {s.Direct} | {s.All} | {s.Tris:N0} | {FormatSize(s.Size)} |");

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // ── 트리 ────────────────────────────────────
            sb.AppendLine($"## 트리 (깊이 {MaxDepth}까지)");
            sb.AppendLine();
            sb.AppendLine("`[기호] 이름 | 직계자식 | 삼각형 | 크기`");
            sb.AppendLine();
            sb.AppendLine("```");
            foreach (var root in roots)
                WriteNode(sb, root.transform, 0);
            sb.AppendLine("```");

            // ── 저장 ────────────────────────────────────
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var safeName = string.Join("_", scene.name.Split(Path.GetInvalidFileNameChars()));
            var outPath = Path.Combine(projectRoot, $"HierarchyDump_{safeName}.md");

            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));

            Debug.Log($"[HierarchyDumper] 저장 완료 → {outPath}\n" +
                      $"오브젝트 {totalObjects:N0}개 / 삼각형 {totalTris:N0}");

            EditorUtility.RevealInFinder(outPath);
        }

        private static void WriteNode(StringBuilder sb, Transform t, int depth)
        {
            if (depth > MaxDepth) return;

            var indent = new string(' ', depth * 2);
            var kind = Classify(t.gameObject);
            var tris = CountTriangles(t.gameObject);
            var size = GetWorldSize(t.gameObject);

            sb.AppendLine($"{indent}{kind} {t.name} | {t.childCount} | {tris:N0} | {FormatSize(size)}");

            if (depth == MaxDepth && t.childCount > 0)
            {
                int deeper = t.GetComponentsInChildren<Transform>(true).Length - 1;
                sb.AppendLine($"{indent}  ...(하위 {deeper}개 생략)");
                return;
            }

            for (int i = 0; i < t.childCount; i++)
                WriteNode(sb, t.GetChild(i), depth + 1);
        }

        /// <summary>이 오브젝트가 그룹인지 메시인지 판별.</summary>
        private static string Classify(GameObject go)
        {
            bool hasMesh = go.GetComponent<MeshFilter>() != null
                        || go.GetComponent<SkinnedMeshRenderer>() != null;
            bool hasChildren = go.transform.childCount > 0;

            if (hasMesh && hasChildren) return "[GM]";
            if (hasMesh) return "[M] ";
            if (hasChildren) return "[G] ";
            return "[-] ";
        }

        /// <summary>자신과 모든 자식의 삼각형 수 합계.</summary>
        private static long CountTriangles(GameObject go)
        {
            long total = 0;

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    total += mesh.GetIndexCount(i) / 3;
            }

            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    total += mesh.GetIndexCount(i) / 3;
            }

            return total;
        }

        /// <summary>렌더러 바운즈를 합쳐 월드 크기를 구한다.</summary>
        private static Vector3 GetWorldSize(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return Vector3.zero;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds.size;
        }

        private static string FormatSize(Vector3 s)
        {
            if (s == Vector3.zero) return "-";
            return $"{s.x:F1} x {s.y:F1} x {s.z:F1}";
        }
    }
}
