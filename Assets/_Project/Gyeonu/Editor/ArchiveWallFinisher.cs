using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 서고 벽 목재 마감 (2026-08-16, 멱등) — 관측실 작업실과 **같은 심벽 구성**을 서고 벽에 입힌다.
    ///
    /// 관측실 작업실 벽(ObservatoryBuilder.Simbyeok) 실측 구성을 그대로 따른다:
    ///   아래에서 위로 — 하단 석축(0.85, ALP Room00 StoneWall01) / 하방(목) /
    ///   회벽 밭 + 기둥(폭 0.20, 약 2.6m 간격) / 상방(목).
    ///   **네 층이 모두 같은 평면을 나눠 가진다** — 겹치는 상자가 없어 z-파이팅이 없고,
    ///   벽면이 완전 평면이라 서가가 지금처럼 뒷판째 붙은 상태가 유지된다.
    ///
    /// ⚠️ 사용자 수작업 배치 보존이 최우선이라 **ArchiveBuilder·ArchiveFurnisher를 재실행하지 않는다.**
    ///    이 스크립트는 루트 `서고_벽마감`만 만들고 지운다 — 기존 `서고`(구조·조명·비밀문·줄사다리)와
    ///    `서고_소품`(가구·책)은 읽지도 쓰지도 않는다. 보행 콜라이더도 건드리지 않는다
    ///    (마감이 방 안쪽으로 1.2cm만 나오므로 충돌 체감 차이가 없다).
    ///
    /// 마감판은 기존 암반 벽의 **안쪽 면보다 1.2cm 앞**에 서고 두께 6cm로 암반 속에 박힌다 —
    /// 암반 면과 같은 평면에 두면 z-파이팅이 나므로 살짝 띄운 값이다.
    ///
    /// 천장: 목재 천장널을 천장 밑면에 5cm 두께로 덧댄다 (자재 창고의 노출 동바리와 어울린다).
    ///       기존 동바리 보(밑면 -4.22)는 천장널(-4.05)보다 17cm 아래로 남아 그대로 노출된다.
    /// 바닥: **그대로 둔다.** 창고 바닥은 흙·돌이 자연스럽고, 무엇보다 소품 165개가 바닥 접지로
    ///       정착해 있어 바닥 높이를 바꾸면 전부 뜨거나 파묻힌다 (배치 보존 최우선).
    /// </summary>
    public static class ArchiveWallFinisher
    {
        const string RootName = "서고_벽마감";
        const string ModelDir = "Assets/_Project/Gyeonu/Art/Models/Archive";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Archive";
        const string KMTex = "Assets/KimMyeonggwanHouse/Texture/";
        const string SWTex = "Assets/Soswaewon/Textures/Buildings/";
        const string ALPTex = "Assets/BK_AlchemistHouse/Textures/Surfaces/";  // ALP Room00 석축

        // ── 서고 좌표 (ArchiveBuilder와 일치해야 한다) ──
        const float FY = -6.60f;                 // 바닥
        const float CY = FY + 2.60f;             // 본채 천장 밑면 -4.00
        const float KCY = FY + 2.10f;            // 꺾인 구석 천장 밑면 -4.50
        const float HX0 = 13.9f, HX1 = 21.9f;    // 본채 내부 x
        const float HZ0 = 30.7f, HZ1 = 35.3f;    // 본채 내부 z
        const float KX1 = 16.7f, KZ0 = 27.5f;    // 꺾인 구석
        const float DoorZ0 = 31.75f, DoorZ1 = 33.45f, DoorTop = FY + 2.10f;  // 동벽 문
        const float DX0 = 15.1f, DX1 = 16.0f, DZ0 = 32.5f, DZ1 = 33.4f;      // 천장 비밀문 구멍

        // ── 심벽 치수 (관측실과 동일) ──
        const float BaseH = 0.85f;    // 하단 석축 높이
        const float TrimH = 0.13f;    // 하방·상방 두께
        const float PostW = 0.20f;    // 기둥 폭
        const float PostStep = 2.6f;  // 기둥 간격 목표
        const float UvBase = 0.31f, UvPlaster = 0.28f, UvWood = 0.55f, UvFloor = 0.34f;

        const float Proud = 0.012f;   // 암반 면보다 방 안쪽으로 나오는 양 (z-파이팅 회피)
        const float Deep = 0.048f;    // 암반 속으로 박히는 양
        const float CeilH = 0.05f;    // 천장널 두께
        const float E = 0.06f;        // 모서리에서 이웃 벽 뒤로 물리는 여유

        static readonly List<Mesh> temp = new List<Mesh>();

        [MenuItem("Tools/이문록/서고 벽 목재 마감")]
        public static void Build()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gyeonu_Observatory")
            {
                Debug.LogError("[서고 벽] 활성 씬이 Gyeonu_Observatory가 아닙니다");
                return;
            }
            temp.Clear();
            for (GameObject g; (g = FindRoot(RootName)) != null;) Object.DestroyImmediate(g);

            var texBaseBC = LoadTex(ALPTex + "StoneWall01.png");
            var texBaseNM = LoadTex(ALPTex + "StoneWall01_n.png");
            var texBaseAO = LoadTex(ALPTex + "StoneWall01_o.png");
            var texPlBC = LoadTex(KMTex + "T_WhiteWall01A_BC.png");
            var texPlNM = LoadTex(KMTex + "T_WhiteWall01A_NM.png");
            var texWdBC = LoadTex(SWTex + "T_Wood_BC.png");
            var texWdNM = LoadTex(SWTex + "T_Wood_NM.png");
            var texMaBC = LoadTex(KMTex + "T_Floor01A_BC.png");
            var texMaNM = LoadTex(KMTex + "T_Floor01A_NM.png");

            // 틴트는 서고 팔레트(중성·따뜻한 어둠)에 맞춘다 — 관측실의 쿨 틴트를 그대로 쓰면
            // 웜 등불 아래에서 벽만 파랗게 뜬다. 텍스처·구성은 관측실과 동일하다.
            // 회벽 원본은 거의 흰색(휘도 ≈0.85)이라 세게 낮춰야 석축과 층이 진다 (관측실에서 실측한 규칙).
            var matBase = Mat("M_서고_벽석축", new Color(0.38f, 0.38f, 0.40f), 0.13f, texBaseBC, texBaseNM, texBaseAO);
            var matPlaster = Mat("M_서고_벽회벽", new Color(0.31f, 0.30f, 0.28f), 0.05f, texPlBC, texPlNM, null);
            var matWood = Mat("M_서고_벽목재", new Color(0.40f, 0.33f, 0.25f), 0.14f, texWdBC, texWdNM, null);
            var matCeil = Mat("M_서고_천장널", new Color(0.42f, 0.36f, 0.29f), 0.12f, texMaBC, texMaNM, null);

            var wallBase = new List<CombineInstance>();
            var wallPlaster = new List<CombineInstance>();
            var wallWood = new List<CombineInstance>();
            var ceil = new List<CombineInstance>();

            void Slab(List<CombineInstance> list, float x0, float x1, float y0, float y1, float z0, float z1, float uv, Vector2? off = null)
            {
                if (x1 - x0 < 0.004f || y1 - y0 < 0.004f || z1 - z0 < 0.004f) return;
                AddBox(list, new Vector3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2),
                    new Vector3(x1 - x0, y1 - y0, z1 - z0), uv, off);
            }

            // ── 심벽 한 장 (관측실 Simbyeok과 동일 구성) ──
            // alongX면 벽이 X를 따라 뻗는다(두께는 Z). t0..t1 = 두께 구간, a0..a1 = 벽 길이 구간.
            void Simbyeok(bool alongX, float t0, float t1, float a0, float a1, float yTop)
            {
                void Band(List<CombineInstance> L, float b0, float b1, float y0, float y1, float uv, Vector2? off = null)
                {
                    if (alongX) Slab(L, b0, b1, y0, y1, t0, t1, uv, off);
                    else Slab(L, t0, t1, y0, y1, b0, b1, uv, off);
                }
                // 회벽 밭 — 좌우 끝 반기둥 + 사이 기둥. 기둥도 회벽과 같은 평면이라 벽면은 평평하다
                void Field(float y0, float y1)
                {
                    if (y1 - y0 < 0.06f) return;
                    float span = a1 - a0;
                    int bays = Mathf.Max(1, Mathf.RoundToInt(span / PostStep));
                    float step = span / bays;
                    Band(wallWood, a0, a0 + PostW / 2, y0, y1, UvWood);
                    Band(wallWood, a1 - PostW / 2, a1, y0, y1, UvWood);
                    float cur = a0 + PostW / 2;
                    for (int i = 1; i < bays; i++)
                    {
                        float e = a0 + step * i;
                        Band(wallPlaster, cur, e - PostW / 2, y0, y1, UvPlaster);
                        Band(wallWood, e - PostW / 2, e + PostW / 2, y0, y1, UvWood);
                        cur = e + PostW / 2;
                    }
                    Band(wallPlaster, cur, a1 - PostW / 2, y0, y1, UvPlaster);
                }
                float yStone = FY + BaseH;
                float yRailB = yStone + TrimH;
                float yRailT = yTop - TrimH;
                Band(wallBase, a0, a1, FY, yStone, UvBase, Vector2.zero);  // 석축은 고정 UV — 켜가 어긋나지 않게
                Band(wallWood, a0, a1, yStone, yRailB, UvWood);            // 하방
                Field(yRailB, yRailT);                                     // 회벽 밭 + 기둥
                Band(wallWood, a0, a1, yRailT, yTop, UvWood);              // 상방
            }

            // ── 본채 4면 ──
            Simbyeok(true, HZ1 - Proud, HZ1 + Deep, HX0 - E, HX1 + E, CY);        // 북벽
            Simbyeok(true, HZ0 - Deep, HZ0 + Proud, KX1, HX1 + E, CY);            // 남벽 (구석 어귀 동쪽만)
            Simbyeok(false, HX0 - Deep, HX0 + Proud, HZ0, HZ1 + E, CY);           // 서벽 (본채 구간)
            Simbyeok(false, HX1 - Proud, HX1 + Deep, HZ0 - E, DoorZ0, CY);        // 동벽 (문 남쪽)
            Simbyeok(false, HX1 - Proud, HX1 + Deep, DoorZ1, HZ1 + E, CY);        // 동벽 (문 북쪽)
            // 동벽 문 인방 — 문 위 0.5m는 층을 나누기엔 얕다. 상방과 같은 목재로 채운다
            Slab(wallWood, HX1 - Proud, HX1 + Deep, DoorTop, CY, DoorZ0, DoorZ1, UvWood);

            // ── 꺾인 구석 3면 (천장이 한 단 낮다) ──
            Simbyeok(false, HX0 - Deep, HX0 + Proud, KZ0 - E, HZ0, KCY);          // 서벽 (구석 구간)
            Simbyeok(true, KZ0 - Deep, KZ0 + Proud, HX0 - E, KX1 + E, KCY);       // 구석 남벽
            Simbyeok(false, KX1 - Proud, KX1 + Deep, KZ0 - E, HZ0, KCY);          // 구석 동벽

            // ── 천장널 (비밀문 구멍만 비운다) ──
            Slab(ceil, HX0, DX0, CY - CeilH, CY, HZ0, HZ1, UvFloor);
            Slab(ceil, DX1, HX1, CY - CeilH, CY, HZ0, HZ1, UvFloor);
            Slab(ceil, DX0, DX1, CY - CeilH, CY, HZ0, DZ0, UvFloor);
            Slab(ceil, DX0, DX1, CY - CeilH, CY, DZ1, HZ1, UvFloor);
            Slab(ceil, HX0, KX1, KCY - CeilH, KCY, KZ0, HZ0, UvFloor);            // 구석 천장

            var root = new GameObject(RootName);
            MeshGO(NewChild(root, "벽_석축"), Save(Combine(wallBase), "서고_벽석축"), matBase);
            MeshGO(NewChild(root, "벽_회벽"), Save(Combine(wallPlaster), "서고_벽회벽"), matPlaster);
            MeshGO(NewChild(root, "벽_목재"), Save(Combine(wallWood), "서고_벽목재"), matWood);
            MeshGO(NewChild(root, "천장널"), Save(Combine(ceil), "서고_천장널"), matCeil);

            foreach (var m in temp) if (m != null) Object.DestroyImmediate(m);
            temp.Clear();
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[서고 벽] 심벽 마감 완료 — 삼각형 {CountTris(root):N0}. 바닥은 그대로 (소품 접지 보존).");
            ReportClashes();
        }

        /// <summary>가구·소품이 새 마감면을 파고드는 곳을 **보고만** 한다 (사용자 판단 사항 — 고치지 않는다).</summary>
        [MenuItem("Tools/이문록/서고 벽 마감 간섭 검사")]
        public static void ReportClashes()
        {
            var props = FindRoot("서고_소품");
            if (props == null) { Debug.LogWarning("[서고 벽] 서고_소품이 없어 간섭 검사를 건너뜁니다"); return; }
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (Transform ch in props.transform)
            {
                var rends = ch.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0) continue;
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                bool nook = b.center.z < HZ0 && b.center.x < KX1;
                float pen = 0f; string where = null;
                void Check(float depth, string label) { if (depth > pen) { pen = depth; where = label; } }
                Check(HX0 + Proud - b.min.x, "서벽");
                Check(b.max.z - (HZ1 - Proud), "북벽");
                if (nook)
                {
                    Check(KZ0 + Proud - b.min.z, "구석 남벽");
                    Check(b.max.x - (KX1 - Proud), "구석 동벽");
                    Check(b.max.y - (KCY - CeilH), "구석 천장널");
                }
                else
                {
                    Check(HZ0 + Proud - b.min.z, "남벽");
                    Check(b.max.x - (HX1 - Proud), "동벽");
                    Check(b.max.y - (CY - CeilH), "천장널");
                }
                if (pen > 0.02f) { sb.AppendLine($"   {ch.name} — {where} {pen * 100f:F1}cm 파고듦"); n++; }
            }
            Debug.Log(n == 0
                ? "[서고 벽] 간섭 검사 — 마감면을 2cm 넘게 파고드는 가구·소품 없음 (마감이 1.2cm만 나오므로 뜨는 곳도 없다)"
                : $"[서고 벽] 간섭 검사 — {n}건 (보고만 하고 고치지 않음):\n{sb}");
        }

        // ── 유틸 (ArchiveBuilder와 동일 규약) ──
        static void AddBox(List<CombineInstance> list, Vector3 center, Vector3 size, float uvScale, Vector2? uvOff)
        {
            var off = uvOff ?? new Vector2(
                Mathf.Abs(center.x * 0.173f + center.z * 0.331f) % 1f,
                Mathf.Abs(center.y * 0.257f + center.z * 0.119f) % 1f);
            var m = BoxMesh(size, uvScale, off);
            temp.Add(m);
            list.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one) });
        }

        static Mesh BoxMesh(Vector3 s, float uv, Vector2 off)
        {
            var h = s * 0.5f;
            var v = new List<Vector3>(24); var u = new List<Vector2>(24); var tr = new List<int>(36);
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uw, float vh)
            {
                int i0 = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                u.Add(off); u.Add(off + new Vector2(uw * uv, 0)); u.Add(off + new Vector2(uw * uv, vh * uv)); u.Add(off + new Vector2(0, vh * uv));
                tr.Add(i0); tr.Add(i0 + 1); tr.Add(i0 + 2); tr.Add(i0); tr.Add(i0 + 2); tr.Add(i0 + 3);
            }
            Quad(new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), s.x, s.y);
            Quad(new Vector3(h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), s.x, s.y);
            Quad(new Vector3(h.x, -h.y, h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), s.z, s.y);
            Quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), s.z, s.y);
            Quad(new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z), s.x, s.z);
            Quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), s.x, s.z);
            var m = new Mesh();
            m.SetVertices(v); m.SetUVs(0, u); m.SetTriangles(tr, 0);
            return m;
        }

        static GameObject NewChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static void MeshGO(GameObject go, Mesh mesh, Material mat)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Mesh Combine(List<CombineInstance> list)
        {
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(list.ToArray(), true, true);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        static Mesh Save(Mesh built, string name)
        {
            string path = $"{ModelDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                built.name = name;
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            existing.Clear();
            existing.indexFormat = built.indexFormat;
            existing.vertices = built.vertices;
            existing.normals = built.normals;
            existing.tangents = built.tangents;
            existing.uv = built.uv;
            existing.triangles = built.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        static Texture2D LoadTex(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) Debug.LogWarning($"[서고 벽] 텍스처 없음: {path}");
            return t;
        }

        static Material Mat(string name, Color c, float smooth, Texture2D baseMap, Texture2D normal, Texture2D occlusion)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smooth);
            m.SetTexture("_BaseMap", baseMap);
            m.SetTexture("_BumpMap", normal);
            if (normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", occlusion);
            if (occlusion != null) { m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_OcclusionStrength", 1f); }
            else m.DisableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject FindRoot(string name)
        {
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.name == name) return g;
            return null;
        }

        static int CountTris(GameObject root)
        {
            int n = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) n += mf.sharedMesh.triangles.Length / 3;
            return n;
        }
    }
}
