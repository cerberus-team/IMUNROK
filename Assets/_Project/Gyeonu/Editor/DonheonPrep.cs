using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// Donheon_v1(팀원 제작 FBX) 임포트 정리 + 관아 씬용 프리팹/머티리얼 추출.
    /// 원본 폴더의 "내용"은 건드리지 않는다. 손대는 것은 두 가지뿐이며 둘 다 패키지 README가
    /// 명시적으로 요구한 임포트 설정이다:
    ///   ① 노멀맵 6종 Texture Type = NormalMap (Default로 두면 sRGB로 읽혀 라이팅이 틀어짐)
    ///   ② (필요 시) .fbm 중복 폴더 — 현재 생성되지 않아 처리 불필요
    ///
    /// 씬에 쓸 것은 전부 _Project/Gyeonu/ 아래로 복제한다:
    ///   - Art/Models/Donheon_Body_NoGround.asset — 본체 메시에서 44×44m 지반 평면(2삼각형) 제거본.
    ///     원본은 y=-0.70에 사방 22m 평면이 박혀 있어 지형·담장을 뚫는다.
    ///   - Art/Materials/관아_*.mat — FBX 내장 머티리얼(서브에셋이라 편집 불가)의 복제본.
    ///     외삼문·담장을 같은 재질로 지어 동헌과 톤을 맞추기 위함.
    ///   - Prefabs/Donheon/Donheon.prefab — 지반 제거 + 박스 콜라이더 포함
    ///
    /// 모델 좌표(루트 기준, 스케일 1):
    ///   지반평면 y=-0.70 / 축대(잡석) y -0.80~0.80 / 갑석 y~1.22 / 마루 y=1.50 / 지붕마루 y=7.20
    ///   정면 = +Z (계단이 +Z로 1.5m 돌출). 처마 폭 18.6m, 깊이 9.8m
    /// 멱등 — 다시 실행해도 같은 결과.
    /// </summary>
    public static class DonheonPrep
    {
        const string Fbx = "Assets/Donheon_v1/Donheon_Quest3_v1.fbx";
        const string TexDir = "Assets/Donheon_v1/Textures/";
        const string ModelDir = "Assets/_Project/Gyeonu/Art/Models";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials";
        const string PrefabDir = "Assets/_Project/Gyeonu/Prefabs/Donheon";
        const string MeshPath = ModelDir + "/Donheon_Body_NoGround.asset";
        public const string PrefabPath = PrefabDir + "/Donheon.prefab";

        // 원본 머티리얼 이름 → 복제본 이름 (외삼문·담장에서 재사용할 것들만)
        static readonly (string src, string dst)[] MatCopies =
        {
            ("MI_R_Roof1",        "관아_기와"),
            ("M_Stone_Rubble",    "관아_석축"),
            ("M_Stone_Granite",   "관아_화강암"),
            ("M_Wood_Tile",       "관아_목재"),
            ("MI_R_Buyeon_2.001", "관아_부연"),
            ("MI_KoreanWood_1.001", "관아_문목재"),
        };

        [MenuItem("Tools/이문록/관아 ▸ ① 동헌 에셋 준비")]
        public static void Prepare()
        {
            FixNormalMaps();
            var mesh = BuildNoGroundMesh();
            CopyMaterials();
            BuildPrefab(mesh);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ── ① 노멀맵 임포트 타입 ─────────────────────────────
        static void FixNormalMaps()
        {
            string[] names =
            {
                "MI_R_Roof_Normal", "MI_R_BrickConcrete_Normal", "MI_R_Buyeon_2_Normal",
                "MI_KoreanWood_1_Normal", "MI_KoreanPaper_1_Normal", "T_Wall01b_N",
            };
            int fixedCount = 0;
            foreach (var n in names)
            {
                var ti = AssetImporter.GetAtPath(TexDir + n + ".png") as TextureImporter;
                if (ti == null) { Debug.LogWarning("[동헌] 텍스처 없음: " + n); continue; }
                if (ti.textureType == TextureImporterType.NormalMap) continue;
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
                ti.SaveAndReimport();
                fixedCount++;
            }
            Debug.Log($"[동헌] 노멀맵 타입 교정 {fixedCount}종 (README 필수 항목)");
        }

        // ── ② 지반 평면 제거 메시 ────────────────────────────
        static Mesh BuildNoGroundMesh()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var bodyT = src.transform.Find("Donheon_Body_Mesh");
            var srcMesh = bodyT.GetComponent<MeshFilter>().sharedMesh;

            var dst = new Mesh { name = "Donheon_Body_NoGround", indexFormat = srcMesh.indexFormat };
            dst.vertices = srcMesh.vertices;
            dst.normals = srcMesh.normals;
            dst.tangents = srcMesh.tangents;
            dst.uv = srcMesh.uv;
            dst.subMeshCount = srcMesh.subMeshCount;

            var v = srcMesh.vertices;
            int removed = 0;
            for (int s = 0; s < srcMesh.subMeshCount; s++)
            {
                var tri = srcMesh.GetTriangles(s);
                if (s != 0) { dst.SetTriangles(tri, s); continue; }
                var keep = new List<int>(tri.Length);
                for (int t = 0; t < tri.Length; t += 3)
                {
                    // 지반 평면: 메시 로컬 |x|,|y| = 22 인 거대 사각형 2장 (건물 본체는 |x|<9.5)
                    bool ground = Mathf.Abs(v[tri[t]].x) > 12f || Mathf.Abs(v[tri[t + 1]].x) > 12f
                               || Mathf.Abs(v[tri[t + 2]].x) > 12f;
                    if (ground) { removed++; continue; }
                    keep.Add(tri[t]); keep.Add(tri[t + 1]); keep.Add(tri[t + 2]);
                }
                dst.SetTriangles(keep, s);
            }
            // 정점 배열은 그대로라 RecalculateBounds()가 여전히 44×44를 낸다 —
            // 실제로 쓰이는 인덱스만으로 바운즈를 다시 잡아 컬링을 정상화한다.
            {
                var used = new Bounds(); bool first = true;
                for (int s = 0; s < dst.subMeshCount; s++)
                    foreach (var i in dst.GetTriangles(s))
                    {
                        if (first) { used = new Bounds(v[i], Vector3.zero); first = false; }
                        else used.Encapsulate(v[i]);
                    }
                dst.bounds = used;
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(ModelDir));
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null) { EditorUtility.CopySerialized(dst, existing); dst = existing; EditorUtility.SetDirty(dst); }
            else AssetDatabase.CreateAsset(dst, MeshPath);

            Debug.Log($"[동헌] 지반 평면 삼각형 {removed}개 제거 → {MeshPath}  " +
                      $"새 bbox size={dst.bounds.size:F2}");
            return dst;
        }

        // ── ③ 머티리얼 복제 (FBX 내장은 서브에셋이라 편집 불가) ──
        static void CopyMaterials()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var byName = new Dictionary<string, Material>();
            foreach (var r in src.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && !byName.ContainsKey(m.name)) byName[m.name] = m;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(MatDir));
            foreach (var (s, d) in MatCopies)
            {
                if (!byName.TryGetValue(s, out var srcMat)) { Debug.LogWarning("[동헌] 머티리얼 없음: " + s); continue; }
                string path = MatDir + "/" + d + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(srcMat.shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = srcMat.shader;
                mat.CopyPropertiesFromMaterial(srcMat);
                EditorUtility.SetDirty(mat);
            }
            Debug.Log($"[동헌] 머티리얼 복제 {MatCopies.Length}종 → {MatDir}");
        }

        /// <summary>이름으로 복제 머티리얼을 얻는다 (외삼문·담장 빌더용).</summary>
        public static Material Mat(string koreanName) =>
            AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + koreanName + ".mat");

        // ── ④ 프리팹 ────────────────────────────────────────
        static void BuildPrefab(Mesh noGround)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Donheon";

            var body = inst.transform.Find("Donheon_Body_Mesh");
            body.GetComponent<MeshFilter>().sharedMesh = noGround;

            // 계단(화강암) 실측 → 로그로 남긴다. 콜라이더 램프 각도 근거.
            LogStairs(src);

            // ── 박스 콜라이더 (MeshCollider 금지 규칙) ──
            // 상면 프로브 실측: 지반 -0.70 / 기단 갑석 0.95 / 대청 마루 1.50 / 정면 계단 x±1 구간만
            var colRoot = new GameObject("동헌_콜라이더");
            colRoot.transform.SetParent(inst.transform, false);
            Box(colRoot, "기단", new Vector3(0f, -0.25f, 0.47f), new Vector3(17.10f, 2.40f, 8.63f));
            Box(colRoot, "대청마루", new Vector3(0f, 0.75f, 0.05f), new Vector3(9.10f, 1.50f, 5.90f));
            // 정면 계단 램프 (+Z, 폭 2m). 지반 -0.70 → 기단 0.95, 약 44°
            Ramp(colRoot, "정면계단", new Vector3(0f, 0.10f, 5.75f), new Vector3(2.0f, 0.20f, 2.45f), 44f);
            // 회벽(분합문)·뒷벽 — 실내는 별도 씬이므로 방 안으로는 못 들어간다
            Box(colRoot, "회벽_좌", new Vector3(4.85f, 2.60f, 0.0f), new Vector3(0.70f, 2.20f, 6.00f));
            Box(colRoot, "회벽_우", new Vector3(-4.85f, 2.60f, 0.0f), new Vector3(0.70f, 2.20f, 6.00f));
            Box(colRoot, "뒷벽", new Vector3(0f, 2.60f, -3.20f), new Vector3(10.0f, 2.20f, 0.70f));

            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic
                    | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(PrefabDir));
            PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
            Object.DestroyImmediate(inst);
            Debug.Log("[동헌] 프리팹 저장: " + PrefabPath);
        }

        static void LogStairs(GameObject src)
        {
            var bodyT = src.transform.Find("Donheon_Body_Mesh");
            var m = bodyT.GetComponent<MeshFilter>().sharedMesh;
            var v = m.vertices;
            var tri = m.GetTriangles(2);   // M_Stone_Granite
            float minY = 99f, maxY = -99f, maxZ = -99f, minZstep = 99f;
            foreach (var i in tri)
            {
                var p = bodyT.TransformPoint(v[i]);
                if (p.z > 4.8f) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); maxZ = Mathf.Max(maxZ, p.z); minZstep = Mathf.Min(minZstep, p.z); }
            }
            Debug.Log($"[동헌] 정면 계단(화강암 z>4.8): z {minZstep:F2}~{maxZ:F2}, y {minY:F2}~{maxY:F2} " +
                      $"→ 경사 {Mathf.Atan2(maxY - minY, maxZ - minZstep) * Mathf.Rad2Deg:F1}°");
        }

        static void Box(GameObject root, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        static void Ramp(GameObject root, string name, Vector3 center, Vector3 size, float rotX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(rotX, 0f, 0f);
            go.AddComponent<BoxCollider>().size = size;
        }
    }
}
