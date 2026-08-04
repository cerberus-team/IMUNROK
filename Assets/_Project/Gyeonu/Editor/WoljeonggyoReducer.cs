using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 월정교 누각·문루 부위별 폴리곤 감축 (A안, 2026-08-04 승인).
    /// 원본 FBX는 건드리지 않고, 감축 메시를 Art/Models/Woljeonggyo에,
    /// 감축 프리팹을 Prefabs/Woljeonggyo에 저장한다.
    /// 5,000 tri 미만 부위는 감축하지 않고 원본 메시를 그대로 참조한다.
    /// </summary>
    public static class WoljeonggyoReducer
    {
        const string NugagFbx = "Assets/woljeonggyo/woljeonggyo-pavilion/source/SM_CWEB002_춘양교 누각.fbx";
        const string GateFbx = "Assets/woljeonggyo/woljeonggyo-south-gate-gyeongju/source/SM_CWEA003_월정교 남문루.fbx";
        const string ModelDir = "Assets/_Project/Gyeonu/Art/Models/Woljeonggyo";
        const string PrefabDir = "Assets/_Project/Gyeonu/Prefabs/Woljeonggyo";
        const int MinTris = 5000;      // 이보다 작으면 감축 안 함
        const float DefaultRate = 0.40f; // 미지정 부위 (사용자 지시: 40%)

        // 실루엣 최우선 부위: 경계선 완전 보존 (감축이 덜 되어도 허용)
        // 그 외 부위는 경계 보존을 풀되 UV심·곡면 보존 + 정점 융합으로 형태 유지
        // (기와·공포처럼 열린 모서리 수천 개인 조립식 메시는 경계 보존 시 감축 불가 — 1차 실행 실측)
        static readonly HashSet<string> StrictParts = new HashSet<string>
        {
            "SM_CWEB_002_NUGAG_SEOGARAE",
            "SM_CWEB_002_GYOGAG_SEOGARAE",
            "SM_CWEB_002_GYOGAG_BITMULNULL",
            "SM_CWEA_003_SEOGARAE",
            "SM_CWEA_003_CHUNYEO",
        };

        // 바닥 감축 부위 (2026-08-04 승인): 모든 보존 플래그 해제, 알고리즘 한계까지 감축.
        // 기와(다리 위에서 안 보임, 원경 실루엣만) + 석재(평면 위주).
        static readonly HashSet<string> FloorParts = new HashSet<string>
        {
            "SM_CWEB_002_GIWA",
            "SM_CWEA_003_GIWA",
            "SM_CWEA_003_NANGANSEOG_M001", "SM_CWEA_003_NANGANSEOG_M002", "SM_CWEA_003_NANGANSEOG_M003",
            "SM_CWEA_003_NANGANSEOG_M004", "SM_CWEA_003_NANGANSEOG_M005", "SM_CWEA_003_NANGANSEOG_M006",
            "SM_CWEA_003_GEDAN_M001", "SM_CWEA_003_GEDAN_M002",
            "SM_CWEA_003_CHOSEOK", "SM_CWEA_003_DANCHOSEOK",
            "SM_CWEA_003_GIDAN_M001", "SM_CWEA_003_GIDAN_M002", "SM_CWEA_003_GIDAN_M003",
            "SM_CWEA_003_GIDAN_M004", "SM_CWEA_003_GIDAN_M005",
            "SM_CWEA_003_WOLDAE_M001", "SM_CWEA_003_WOLDAE_M002", "SM_CWEA_003_WOLDAE_M003", "SM_CWEA_003_WOLDAE_M004",
            "SM_CWEA_003_CHEOMASEOG_M001", "SM_CWEA_003_CHEOMASEOG_M002",
            "SM_CWEA_003_SEOKJU", "SM_CWEA_003_JEONDOL_M001", "SM_CWEA_003_JEONDOL_M002",
        };

        // ── 누각: 부위별 감축률 (깎는 비율) ──
        static readonly Dictionary<string, float> NugagRates = new Dictionary<string, float>
        {
            // 기와 75%
            { "SM_CWEB_002_GIWA", 0.75f },
            // 지붕 장식 60%
            { "SM_CWEB_002_GWIMYEONWA", 0.60f },
            { "SM_CWEB_002_JINECHEOL", 0.60f },
            // 공포(주두·소로) 50%
            { "SM_CWEB_002_NUGAG_JUDUSORO", 0.50f },
            { "SM_CWEB_002_GYOGAG_SORO", 0.50f },
            // 첨차 50%
            { "SM_CWEB_002_NUGAG_CHUMCHA", 0.50f },
            { "SM_CWEB_002_GYOGAG_CHUMCHA", 0.50f },
            // 난간 장식·연봉 60%
            { "SM_CWEB_002_GYOGAG_NANGAN_M004", 0.60f },
            { "SM_CWEB_002_GYOGAG_YEONBONG", 0.60f },
            // 처마·서까래 15% (실루엣 보존)
            { "SM_CWEB_002_NUGAG_SEOGARAE", 0.15f },
            { "SM_CWEB_002_GYOGAG_SEOGARAE", 0.15f },
            { "SM_CWEB_002_GYOGAG_BITMULNULL", 0.15f },
            // 기둥 40%
            { "SM_CWEB_002_NUGAG_GIDUNG_M001", 0.40f },
            { "SM_CWEB_002_NUGAG_GIDUNG_M002", 0.40f },
            // 상판·마루·난간 골격·받침목 30% (걷는 자리)
            { "SM_CWEB_002_GYOGAG_JANGMARU", 0.30f },
            { "SM_CWEB_002_GYOGAG_NANGAN_M001", 0.30f },
            { "SM_CWEB_002_GYOGAG_NANGAN_M002", 0.30f },
            { "SM_CWEB_002_GYOGAG_NANGAN_M003", 0.30f },
            { "SM_CWEB_002_GYOGAG_GWITEUL_M001", 0.30f },
            { "SM_CWEB_002_GYOGAG_GWITEUL_M002", 0.30f },
            { "SM_CWEB_002_GYOGAG_DORI_M001", 0.30f },
            { "SM_CWEB_002_GYOGAG_DORI_M002", 0.30f },
            { "SM_CWEB_002_GYOGAG_BATCHIMMOCK_M001", 0.30f },
            { "SM_CWEB_002_GYOGAG_BATCHIMMOCK_M002", 0.30f },
            { "SM_CWEB_002_GYOGAG_BATCHIMMOCK_M004", 0.30f },
            // 나머지(보·창방·대공·천장류)는 DefaultRate 40%
        };

        // ── 문루: 부위별 감축률 ──
        static readonly Dictionary<string, float> GateRates = new Dictionary<string, float>
        {
            // 기와 75%
            { "SM_CWEA_003_GIWA", 0.75f },
            // 지붕 장식 60%
            { "SM_CWEA_003_CHIMI", 0.60f },
            { "SM_CWEA_003_GWIMYEONWA", 0.60f },
            { "SM_CWEA_003_JINECHEOL", 0.60f },
            // 공포 50%
            { "SM_CWEA_003_JUDUSORO", 0.50f },
            // 첨차 50%
            { "SM_CWEA_003_CHUMCHA_M001", 0.50f },
            { "SM_CWEA_003_CHUMCHA_M002", 0.50f },
            { "SM_CWEA_003_CHUMCHA_M003", 0.50f },
            { "SM_CWEA_003_CHUMCHA_M004", 0.50f },
            { "SM_CWEA_003_CHUMCHA_M005", 0.50f },
            // 처마·서까래·추녀 15% (실루엣 보존)
            { "SM_CWEA_003_SEOGARAE", 0.15f },
            { "SM_CWEA_003_CHUNYEO", 0.15f },
            // 석재 80% (평면)
            { "SM_CWEA_003_NANGANSEOG_M001", 0.80f },
            { "SM_CWEA_003_NANGANSEOG_M002", 0.80f },
            { "SM_CWEA_003_NANGANSEOG_M003", 0.80f },
            { "SM_CWEA_003_NANGANSEOG_M004", 0.80f },
            { "SM_CWEA_003_NANGANSEOG_M005", 0.80f },
            { "SM_CWEA_003_NANGANSEOG_M006", 0.80f },
            { "SM_CWEA_003_GEDAN_M001", 0.80f },
            { "SM_CWEA_003_GEDAN_M002", 0.80f },
            { "SM_CWEA_003_CHOSEOK", 0.80f },
            { "SM_CWEA_003_DANCHOSEOK", 0.80f },
            { "SM_CWEA_003_GIDAN_M001", 0.80f },
            { "SM_CWEA_003_GIDAN_M002", 0.80f },
            { "SM_CWEA_003_GIDAN_M003", 0.80f },
            { "SM_CWEA_003_GIDAN_M004", 0.80f },
            { "SM_CWEA_003_GIDAN_M005", 0.80f },
            { "SM_CWEA_003_WOLDAE_M001", 0.80f },
            { "SM_CWEA_003_WOLDAE_M002", 0.80f },
            { "SM_CWEA_003_WOLDAE_M003", 0.80f },
            { "SM_CWEA_003_WOLDAE_M004", 0.80f },
            { "SM_CWEA_003_CHEOMASEOG_M001", 0.80f },
            { "SM_CWEA_003_CHEOMASEOG_M002", 0.80f },
            { "SM_CWEA_003_SEOKJU", 0.80f },
            { "SM_CWEA_003_JEONDOL_M001", 0.80f },
            { "SM_CWEA_003_JEONDOL_M002", 0.80f },
            // 난간·연봉 60%
            { "SM_CWEA_003_1F_NANGAN", 0.60f },
            { "SM_CWEA_003_2F_NANGAN", 0.60f },
            { "SM_CWEA_003_YEONBONG", 0.60f },
            // 기둥 40%
            { "SM_CWEA_003_GIDUnG_M001", 0.40f },
            { "SM_CWEA_003_GIDUNG_M002", 0.40f },
            // 마루 30% (걷는 자리)
            { "SM_CWEA_003_2F_MARUBADAK", 0.30f },
            { "SM_CWEA_003_GWITEUL", 0.30f },
            // 창호·문·벽 40% (사용자 지시: 40% 초과 금지. 가까이서 보는 부위)
            { "SM_CWEA_003_2F_CHANGMUN_M001", 0.40f },
            { "SM_CWEA_003_2F_CHANGMUN_M002", 0.40f },
            { "SM_CWEA_003_2F_CHANGMUN_M003", 0.40f },
            { "SM_CWEA_003_2F_CHANGTEUL", 0.40f },
            { "SM_CWEA_003_2F_MUN", 0.40f },
            { "SM_CWEA_003_HYUNPAN", 0.40f },
            // 나머지(보·창방·벽·천장류)는 DefaultRate 40%
        };

        // ── 심층 세대 (2026-08-04 승인): 창호·문·벽·누각 서까래 2종은 미실행(원본 유지),
        //    나머지 전 부위는 바닥 프로파일로 알고리즘 한계까지 감축 ──
        static bool _deep;
        static readonly HashSet<string> DeepSkip = new HashSet<string>
        {
            // 누각: 처마 실루엣 서까래 2종 원본 유지
            "SM_CWEB_002_NUGAG_SEOGARAE",
            "SM_CWEB_002_GYOGAG_SEOGARAE",
            // 문루: 창호·문·벽 계열 전체 원본 유지 (근접 부위, 합 71,400)
            "SM_CWEA_003_2F_CHANGMUN_M001", "SM_CWEA_003_2F_CHANGMUN_M002", "SM_CWEA_003_2F_CHANGMUN_M003",
            "SM_CWEA_003_2F_CHANGTEUL", "SM_CWEA_003_2F_MUN", "SM_CWEA_003_HYUNPAN",
            "SM_CWEA_003_1F_WALL_M001", "SM_CWEA_003_1F_WALL_M002", "SM_CWEA_003_2F_WALL",
            "SM_CWEA_003_DORIWALL", "SM_CWEA_003_HAPJANGWALL",
        };

        [MenuItem("Tools/이문록/월정교 감축 1 - 누각 (A안)")]
        public static void RunNugag()
        {
            Reduce(NugagFbx, NugagRates, "Nugag", "SM_CWEB_002_");
        }

        [MenuItem("Tools/이문록/월정교 감축 2 - 문루 (A안)")]
        public static void RunGate()
        {
            Reduce(GateFbx, GateRates, "Munru", "SM_CWEA_003_");
        }

        [MenuItem("Tools/이문록/월정교 감축 3 - 누각 (심층)")]
        public static void RunNugagDeep()
        {
            _deep = true;
            try { Reduce(NugagFbx, NugagRates, "Nugag", "SM_CWEB_002_"); }
            finally { _deep = false; }
        }

        [MenuItem("Tools/이문록/월정교 감축 4 - 문루 (심층)")]
        public static void RunGateDeep()
        {
            _deep = true;
            try { Reduce(GateFbx, GateRates, "Munru", "SM_CWEA_003_"); }
            finally { _deep = false; }
        }

        static void Reduce(string fbxPath, Dictionary<string, float> rates, string outName, string prefix)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (src == null) { Debug.LogError("FBX 없음: " + fbxPath); return; }

            EnsureFolder(ModelDir);
            EnsureFolder(ModelDir + "/" + outName);
            EnsureFolder(PrefabDir);

            var report = new StringBuilder();
            report.AppendLine("# 월정교 감축 결과 — " + outName);
            report.AppendLine("| 부위 | 전 | 후 | 감축률 |");
            report.AppendLine("|---|---:|---:|---:|");

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = outName + "_Reduced";
            inst.transform.position = Vector3.zero;

            long totalBefore = 0, totalAfter = 0;
            var filters = inst.GetComponentsInChildren<MeshFilter>(true);
            try
            {
                for (int i = 0; i < filters.Length; i++)
                {
                    var mf = filters[i];
                    var mesh = mf.sharedMesh;
                    if (mesh == null) continue;
                    long tris = CountTris(mesh);
                    totalBefore += tris;

                    float rate;
                    bool listed = rates.TryGetValue(mf.name, out rate);
                    if (!listed) rate = DefaultRate;

                    EditorUtility.DisplayProgressBar("월정교 감축 — " + outName,
                        mf.name + " (" + tris.ToString("N0") + " tris)", (float)i / filters.Length);

                    if (tris < MinTris || (_deep && DeepSkip.Contains(mf.name)))
                    {
                        totalAfter += tris;
                        string why = (_deep && DeepSkip.Contains(mf.name)) ? "(미실행-보호)" : "(원본 유지)";
                        report.AppendLine("| " + Short(mf.name, prefix) + " | " + tris.ToString("N0") + " | " + why + " | 0% |");
                        continue;
                    }

                    bool strict = !_deep && StrictParts.Contains(mf.name);
                    bool floor = _deep || FloorParts.Contains(mf.name);
                    var simplifier = new MeshSimplifier();
                    var opts = SimplificationOptions.Default;
                    opts.EnableSmartLink = true;
                    if (floor)
                    {
                        // 바닥 감축: 보존 전부 해제, 최대 공격 설정
                        opts.PreserveBorderEdges = false;
                        opts.PreserveUVSeamEdges = false;
                        opts.PreserveUVFoldoverEdges = false;
                        opts.PreserveSurfaceCurvature = false;
                        opts.VertexLinkDistance = 0.001;
                        opts.Agressiveness = 10.0;
                        opts.MaxIterationCount = 300;
                    }
                    else if (strict)
                    {
                        opts.PreserveBorderEdges = true;  // 처마·서까래: 경계선까지 완전 보존
                        opts.PreserveUVSeamEdges = true;
                        opts.PreserveSurfaceCurvature = true;
                    }
                    else
                    {
                        opts.PreserveBorderEdges = false;
                        opts.PreserveUVSeamEdges = true;      // UV 심 보존
                        opts.PreserveSurfaceCurvature = true; // 곡면(실루엣) 보존
                        opts.VertexLinkDistance = 1e-4;   // 인접 정점 융합으로 열린 모서리 최소화
                        opts.Agressiveness = 8.0;
                        opts.MaxIterationCount = 150;
                    }
                    simplifier.SimplificationOptions = opts;
                    simplifier.Initialize(mesh);
                    // 바닥 부위는 지정 비율보다 깊게 요청해 알고리즘 한계까지 감축
                    simplifier.SimplifyMesh(floor ? Mathf.Min(0.10f, 1f - rate) : 1f - rate);

                    var reduced = simplifier.ToMesh();
                    reduced.name = Short(mf.name, prefix) + "_LOD";
                    reduced.RecalculateBounds();
                    long after = CountTris(reduced);
                    totalAfter += after;

                    // GUID 보존: 기존 에셋이 있으면 삭제하지 않고 내용만 덮어쓴다
                    // (Delete+Create는 GUID가 바뀌어 씬·프리팹 참조가 끊긴다 — 2026-08-04 사고 원인)
                    string assetPath = ModelDir + "/" + outName + "/" + reduced.name + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                    if (existing != null)
                    {
                        string keepName = existing.name;
                        EditorUtility.CopySerialized(reduced, existing);
                        existing.name = keepName;
                        Object.DestroyImmediate(reduced);
                        EditorUtility.SetDirty(existing);
                        mf.sharedMesh = existing;
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(reduced, assetPath);
                        mf.sharedMesh = reduced;
                    }

                    report.AppendLine("| " + Short(mf.name, prefix) + " | " + tris.ToString("N0") + " | " + after.ToString("N0") + " | " +
                        Mathf.RoundToInt((1f - (float)after / tris) * 100) + "%" + (listed ? "" : " (기본40)") + (strict ? " (엄격)" : "") + (floor ? " (바닥)" : "") + " |");
                }

                string prefabPath = PrefabDir + "/" + outName + "_Reduced.prefab";
                PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
                report.AppendLine();
                report.AppendLine("**합계: " + totalBefore.ToString("N0") + " → " + totalAfter.ToString("N0") +
                    " (" + Mathf.RoundToInt((1f - (float)totalAfter / totalBefore) * 100) + "% 감축)**");
                report.AppendLine();
                report.AppendLine("프리팹: " + prefabPath);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Object.DestroyImmediate(inst);
            }

            AssetDatabase.SaveAssets();
            string reportPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../Captures/Reduction_" + outName + ".md"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(reportPath));
            System.IO.File.WriteAllText(reportPath, report.ToString());
            Debug.Log("월정교 감축 완료: " + outName + " " + totalBefore.ToString("N0") + " → " + totalAfter.ToString("N0") +
                "\n보고서: " + reportPath);
        }

        static long CountTris(Mesh m)
        {
            long t = 0;
            for (int i = 0; i < m.subMeshCount; i++) t += (long)(m.GetIndexCount(i) / 3);
            return t;
        }

        static string Short(string name, string prefix)
        {
            return name.StartsWith(prefix) ? name.Substring(prefix.Length) : name;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            string leaf = path.Substring(path.LastIndexOf('/') + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
