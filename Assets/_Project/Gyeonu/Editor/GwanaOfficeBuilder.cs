using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Gyeonu;
using static IMUNROK.Gyeonu.Editor.GwanaOfficeLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 집무실(동헌 온돌방) 씬 조립기. 멱등 — 다시 실행하면 관리 그룹을 지우고 재생성한다.
    ///
    /// ⚠️ **방 하나의 실내만 만든다** (사용자 지시). 큐브로 바닥·천장·벽 4면을 세운 모델하우스다.
    ///    동헌 외형·지붕·마당·지형·Terrain은 만들지 않는다 — 전부 관아 외부 씬 소관.
    ///    창은 벽을 뚫지 않는다: 벽면에 격자창을 붙이고 **빛만** 안쪽 스포트라이트로 흉내낸다.
    ///    (관측실·서고를 지은 방식과 같다 — 절차 메시 + AddBox 원장 → BoxCollider 1:1)
    ///
    /// 【서사】 낮에는 수령이 있어 못 들어오고, 밤에 몰래 잠입하는 방.
    ///    뒷벽 병풍 뒤에 **잠긴** 비밀문이 있고 그 너머로 서고까지 돌계단이 내려간다.
    ///    관측실 바닥 비밀문(우연히 발견 → 열리지만 내려갈 수단이 없음)과 문법이 대비된다.
    ///
    /// 【조명 — 왜 방향광이 아닌가】 사용자 지시는 "방향광 강하게 + 앰비언트 약하게"지만
    ///    이 방은 사방이 막힌 상자다. 그림자를 켠 Directional Light는 벽에 전부 차단돼
    ///    실내에 한 줌도 들어오지 않는다(실측 전에 자명). 그래서 **격자 쿠키를 물린 스포트라이트**를
    ///    창 안쪽에 두어 같은 그림(강한 측광 + 바닥의 창살 그림자)을 만든다.
    ///    앰비언트는 낮춰 대비를 살린다 — 지시의 '의도'를 그대로 따른 것이고 수단만 다르다.
    /// </summary>
    public static class GwanaOfficeBuilder
    {
        public const string ScenePath = "Assets/_Project/Gyeonu/Scenes/Gyeonu_GwanaOffice.unity";
        const string MeshDir = "Assets/_Project/Gyeonu/Art/Models/GwanaOffice";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/GwanaOffice";
        const string TexDir = "Assets/_Project/Gyeonu/Art/Textures/GwanaOffice";
        const string DonheonTex = "Assets/Donheon_v1/Textures/";
        const string KMTex = "Assets/KimMyeonggwanHouse/Texture/";

        const float Sink = 0.006f;   // 목부재를 회벽 속으로 살짝 묻어 동일평면 z-파이팅을 막는다
        // 문선이 개구부 안쪽으로 물리는 깊이. 0이면 문선 안쪽면(x=±개구부 반폭, 인방 밑면)이
        // 벽 개구부의 반턱면과 **정확히 같은 평면**이 되어 문설주·상인방을 따라 색이 번갈아 뜬다
        // (2026-08-20 실측: 간격 0.00mm). 6mm 물려 두면 문선이 확실히 이기고 문턱처럼 읽힌다.
        const float Lip = 0.006f;

        internal static Material MatMaru, MatHoebyeok, MatMokjae, MatMunmok, MatHanji,
                                 MatPan, MatSecret, MatStone, MatDark,
                                 MatHanjiNight, MatLanternPaper,
                                 MatPassStone, MatPassFloor, MatPassWood;

        // 서고 씬이 이미 만들어 둔 관아 통로 재질 — **그대로 재사용**해야 두 통로가 한 길로 읽힌다
        const string ArchiveMat = "Assets/_Project/Gyeonu/Art/Materials/Archive/";

        // ══════════════════════════════════════════════════════
        [MenuItem("Tools/이문록/관아 집무실 ▸ ① 씬 조립")]
        public static void BuildAll()
        {
            var scene = EnsureScene();
            LoadMaterials();
            RemoveLegacyGroups();
            BuildShell();
            BuildCeiling();
            BuildOpenings();
            BuildSecretPassage();
            BuildLighting();
            BuildMarkers();
            BuildDebugUnlocker();   // ⚠️ 임시 — 퍼즐이 붙으면 이 줄과 아래 메서드를 지운다

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[집무실] ① 씬 조립 완료 — " + ScenePath + " (Build Settings 미등록: 지시대로)");
        }

        [MenuItem("Tools/이문록/관아 집무실 ▸ ④ 전체 재생성 (①~③ + 보행)")]
        public static void BuildEverything()
        {
            BuildAll();
            GwanaOfficeFurnisher.Furnish();
            GwanaOfficeWalkSetup.BuildColliders();
            GwanaOfficeWalkSetup.InstallWalker();
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[집무실] 전체 재생성 완료");
        }

        // ── 씬 ───────────────────────────────────────────────
        static Scene EnsureScene()
        {
            Scene scene;
            if (System.IO.File.Exists(ScenePath))
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            SceneManager.SetActiveScene(scene);
            return scene;
        }

        internal static GameObject FindRoot(string name) =>
            SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);

        internal static GameObject RecreateGroup(string name)
        {
            var old = FindRoot(name);
            if (old != null) Object.DestroyImmediate(old);
            return new GameObject(name);
        }

        /// <summary>
        /// 옛 설계에서 이름이 바뀌며 버려진 그룹을 지운다.
        ///
        /// ⚠️ **`RecreateGroup`은 같은 이름만 지운다.** 비밀 통로 1·2차 설계는 그룹 이름이
        ///    `집무실_비밀계단`(돌계단 12단, 0.205×0.29)이었는데, 3차에 통로를 새로 쓰면서
        ///    이름을 `집무실_비밀통로`로 바꿨다. 그 뒤로 몇 번을 재생성해도 **옛 그룹은
        ///    씬에 그대로 남아 있었다** (2026-08-20 발견 — 어느 스크립트도 만들지 않는 유령 216 tri).
        ///    새 통로와 정확히 같은 평면을 공유해서(층계참 윗면 y=0 / 앞면 z=2.44 / 천장 y=2.30,
        ///    전부 간격 0.00mm) 비밀문을 열면 문 너머 바닥이 시점에 따라 두 색으로 번갈아 떴다.
        ///    이름을 바꿀 땐 옛 이름을 여기에 남겨 둘 것.
        /// </summary>
        static void RemoveLegacyGroups()
        {
            foreach (var name in new[] { "집무실_비밀계단" })
            {
                var old = FindRoot(name);
                if (old == null) continue;
                Object.DestroyImmediate(old);
                Debug.Log("[집무실] 옛 그룹 제거 — " + name + " (비밀 통로 1·2차 설계의 잔재)");
            }
        }

        // ══════════════════════════════════════════════════════
        // 머티리얼
        // ══════════════════════════════════════════════════════
        /// <summary>
        /// 재질 static이 비어 있으면 채운다.
        /// ⚠️ `LoadMaterials()`는 BuildAll 안에서만 불리는데 **static 필드는 도메인 리로드에서
        ///    비워진다.** 그래서 「② 소품 배치」만 따로 돌리면 절차 소품(붓통·붓)이 재질 null로
        ///    생성돼 **마젠타**로 뜬다 (실측). 소품 배치도 이 함수를 먼저 부르게 해 뒀다.
        /// </summary>
        internal static void EnsureMaterials()
        {
            if (MatMokjae == null || MatMunmok == null || MatHanji == null) LoadMaterials();
        }

        internal static void LoadMaterials()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(MatDir));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(MeshDir));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(TexDir));

            // 마루 — 밝은 갈색 널. 김명관 고택 마루널 (관측실 작업실과 같은 텍스처)
            MatMaru = TexAt("집무실_마루", KMTex + "T_Floor01A_BC.png", KMTex + "T_Floor01A_NM.png",
                            new Color(0.72f, 0.56f, 0.38f), 0.16f);
            // 회벽 — 한지·회벽 느낌의 밝은 미색.
            // ⚠️ 동헌 `T_Wall01b`는 이름과 달리 **막돌 벽**이라 실내에 쓰면 동굴로 읽힌다(실측).
            //    회벽은 김명관 T_WhiteWall01A — 완전 균일해 월드 UV로 아무 크기나 붙는다.
            MatHoebyeok = TexAt("집무실_회벽", KMTex + "T_WhiteWall01A_BC.png", KMTex + "T_WhiteWall01A_NM.png",
                                new Color(0.780f, 0.735f, 0.655f), 0.03f);
            // 구조 목재 (기둥·보·도리·서까래)
            MatMokjae = Tex("집무실_목재", "MI_KoreanWood_1_BaseColor.png", "MI_KoreanWood_1_Normal.png",
                            new Color(0.44f, 0.32f, 0.22f), 0.10f);
            // 창호·문틀 — 구조재보다 붉고 밝다
            MatMunmok = Tex("집무실_문목재", "MI_KoreanWood_1_BaseColor.png", "MI_KoreanWood_1_Normal.png",
                            new Color(0.56f, 0.40f, 0.26f), 0.13f);
            // 창호지 — 바깥이 밝다는 인상을 주기 위해 약하게 자체발광
            MatHanji = Tex("집무실_한지", "MI_KoreanPaper_1_BaseColor.png", "MI_KoreanPaper_1_Normal.png",
                           new Color(0.90f, 0.85f, 0.72f), 0.02f);
            MatHanji.EnableKeyword("_EMISSION");
            MatHanji.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            // ⚠️ 세게 주면 창이 흰 판때기가 되어 **살대가 안 보인다** (실측). 살대가 읽힐 만큼만.
            MatHanji.SetColor("_EmissionColor", new Color(1.00f, 0.92f, 0.74f) * 0.22f);
            EditorUtility.SetDirty(MatHanji);
            // 개판(천장널) — 서까래 사이로 보이는 널
            MatPan = Tex("집무실_반자", "MI_KoreanWood_1_BaseColor.png", "MI_KoreanWood_1_Normal.png",
                         new Color(0.50f, 0.38f, 0.27f), 0.06f);
            // 비밀문 — 벽과 구분되는 검은 널문
            MatSecret = Tex("집무실_비밀문", "MI_KoreanWood_1_BaseColor.png", "MI_KoreanWood_1_Normal.png",
                            new Color(0.20f, 0.16f, 0.13f), 0.09f);
            // 계단 돌
            MatStone = Tex("집무실_계단돌", "T_Stone_Granite_512.png", null,
                           new Color(0.44f, 0.43f, 0.41f), 0.05f);
            MatDark = Solid("집무실_어둠", new Color(0.018f, 0.017f, 0.016f), 0f);

            // 밤 창호지 — 바깥이 어두우니 자체발광을 달빛 색으로 낮춘다
            MatHanjiNight = Tex("집무실_한지_밤", "MI_KoreanPaper_1_BaseColor.png", "MI_KoreanPaper_1_Normal.png",
                                new Color(0.34f, 0.38f, 0.46f), 0.02f);
            MatHanjiNight.EnableKeyword("_EMISSION");
            MatHanjiNight.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            MatHanjiNight.SetColor("_EmissionColor", new Color(0.42f, 0.52f, 0.74f) * 0.06f);
            EditorUtility.SetDirty(MatHanjiNight);

            // 등롱 한지 — 늘 켜져 있으므로 자체발광
            MatLanternPaper = Tex("집무실_등롱한지", "MI_KoreanPaper_1_BaseColor.png", null,
                                  new Color(0.86f, 0.72f, 0.48f), 0.02f);
            MatLanternPaper.EnableKeyword("_EMISSION");
            MatLanternPaper.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            MatLanternPaper.SetColor("_EmissionColor", new Color(1.00f, 0.80f, 0.52f) * 1.6f);
            EditorUtility.SetDirty(MatLanternPaper);

            // ── 통로 재질: 서고 씬이 만든 것을 **그대로** 쓴다 (없으면 같은 텍스처로 복제) ──
            MatPassStone = Reuse("M_서고_통로석벽", "집무실_통로석벽",
                                 "Assets/BK_AlchemistHouse/Textures/Surfaces/StoneWall01.png",
                                 new Color(0.520f, 0.470f, 0.400f), 0.15f);
            MatPassFloor = Reuse("M_서고_통로바닥", "집무실_통로바닥",
                                 KMTex + "T_Stone02A_BC.png", new Color(0.420f, 0.390f, 0.340f), 0.20f);
            MatPassWood = Reuse("M_서고_통로목재", "집무실_통로목재",
                                "Assets/Soswaewon/Textures/Buildings/T_Wood_BC.png",
                                new Color(0.420f, 0.340f, 0.260f), 0.16f);
        }

        /// <summary>서고 재질을 재사용하고, 없으면 같은 텍스처·틴트로 우리 폴더에 복제본을 만든다.</summary>
        static Material Reuse(string archiveName, string localName, string texPath, Color tint, float smooth)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(ArchiveMat + archiveName + ".mat");
            if (m != null) return m;
            Debug.LogWarning("[집무실] " + archiveName + " 없음 — 같은 텍스처로 복제본 생성 (서고 통로와 톤을 맞춰 둠)");
            return TexAt(localName, texPath, null, tint, smooth);
        }

        static Material Tex(string name, string baseTex, string nrmTex, Color tint, float smooth) =>
            TexAt(name, DonheonTex + baseTex, string.IsNullOrEmpty(nrmTex) ? null : DonheonTex + nrmTex,
                  tint, smooth);

        static Material TexAt(string name, string basePath, string nrmPath, Color tint, float smooth)
        {
            var m = Solid(name, tint, smooth);
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            if (t != null) m.SetTexture("_BaseMap", t); else Debug.LogWarning("[집무실] 텍스처 없음: " + basePath);
            if (!string.IsNullOrEmpty(nrmPath))
            {
                var n = AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath);
                if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Solid(string name, Color c, float smooth)
        {
            string p = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ══════════════════════════════════════════════════════
        // ① 방 껍데기 — 바닥 · 벽 4면 (심벽)
        // ══════════════════════════════════════════════════════
        static void BuildShell()
        {
            var group = RecreateGroup("집무실_방");

            // ── 바닥 (마루널) ──
            var floor = new GwanaMeshKit(0.70f);   // 널 폭이 읽히게 UV를 잘게
            floor.BoxMinMax(-OutX, OutX, -0.30f, FloorY, OutZS, OutZN);
            Piece(group.transform, "바닥_마루", floor, MatMaru);

            // ── 벽 4면 ──
            var plaster = new GwanaMeshKit(1.60f);
            var wood = new GwanaMeshKit(GwanaMeshKit.MpuWood);

            // 남벽 (들어온 문) — 문 개구부
            WallFace(plaster, wood, false, ZS, +1, -HalfX, HalfX,
                     new[] { -1.52f, 1.52f },
                     -DoorHalfX, DoorHalfX, FloorY, DoorTopY);
            // 북벽 (뒷벽) — 비밀문 개구부
            WallFace(plaster, wood, false, ZN, -1, -HalfX, HalfX,
                     new[] { -1.52f, 1.52f },
                     -SecretHalfX, SecretHalfX, FloorY, SecretTopY);
            // 서벽 (왼쪽) — 격자창
            WallFace(plaster, wood, true, -HalfX, +1, ZS, ZN,
                     new[] { -1.55f, 1.55f },
                     WinWestZ0, WinWestZ1, WinY0, WinY1);
            // 동벽 (오른쪽) — 격자창
            WallFace(plaster, wood, true, HalfX, -1, ZS, ZN,
                     new[] { -1.55f, 1.55f },
                     WinEastZ0, WinEastZ1, WinY0, WinY1);

            // ── 남·북벽의 처마 밑 소벽 (별도 메시) ──
            // 종도리가 X축과 나란해 이쪽 벽은 위가 수평으로 끝난다. 창방(2.35) 위에서
            // 서까래 밑면까지 막지 않으면 배경이 그대로 새어 방 위쪽에 띠가 생긴다.
            //
            // ⚠️ **이 두 상자만은 `plaster` 킷에 합치면 렌더링되지 않는다** (2026-08-16, 원인 미상).
            //    실측으로 확인한 것: 메시에 삼각형이 정확히 들어 있고(232개, 예상치와 일치),
            //    기하 법선·저장 법선 모두 (0,0,-1)로 방을 향하며, 서브메시 인덱스도 온전하고
            //    (696/696), 렌더러도 켜져 있고, 정적 배칭 플래그를 지우거나 오브젝트를 복제하거나
            //    UploadMeshData로 GPU에 다시 올려도 그대로였다. 그런데 **같은 자리에 놓은 큐브는
            //    정상 렌더**된다. 그래서 별 메시로 뽑는다 — 원인이 밝혀지면 되돌려도 된다.
            var upper = new GwanaMeshKit(1.60f);
            foreach (float s in new[] { -1f, 1f })
                upper.BoxMinMax(-OutX, OutX, ChangbangY1, 2.98f,
                                s > 0f ? ZN : OutZS, s > 0f ? OutZN : ZS);

            // ── 박공 삼각벽 (동·서 — 종도리가 X축과 나란하므로 이쪽에 생긴다) ──
            foreach (float s in new[] { -1f, 1f })
            {
                float xIn = s * HalfX, xOut = s * OutX;
                foreach (float sz in new[] { -1f, 1f })
                {
                    // ⚠️ 박공 윗변을 **서까래 밑면**(RafterBotY)에 맞추면 안 된다. 개판 밑면은
                    //    RafterTopY라 그 사이 10.3cm가 뚫린 채 남고, 서까래(0.095/간격 0.375)
                    //    사이사이로 배경이 새어 방 위쪽에 파란 띠가 생긴다 (실측으로 잡은 버그).
                    //    개판 속까지 밀어 넣는다 — 남는 부분은 지붕 실체에 묻혀 안 보인다.
                    float y0 = ChangbangY1;
                    Vector3 a = new Vector3(xIn, y0, 0f);
                    Vector3 b = new Vector3(xIn, RafterTopY(0f) + 0.07f, 0f);
                    Vector3 c = new Vector3(xIn, RafterTopY(OutZN) + 0.07f, sz * OutZN);
                    Vector3 d = new Vector3(xIn, y0, sz * OutZN);
                    // 안쪽 면이 방을 보게 감는다 (s = 벽의 바깥 방향)
                    // 박공도 소벽과 같은 이유로 `upper` 메시에 넣는다 (plaster에 합치면 안 그려진다)
                    if (s * sz > 0f) upper.Quad(a, b, c, d); else upper.Quad(d, c, b, a);
                    // 두께 채움 (바깥면은 안 보이지만 실루엣이 새면 안 된다)
                    Vector3 o = new Vector3(s * WallT, 0f, 0f);
                    if (s * sz > 0f) upper.Quad(d + o, c + o, b + o, a + o);
                    else upper.Quad(a + o, b + o, c + o, d + o);
                }
            }
            Piece(group.transform, "벽_상부", upper, MatHoebyeok);

            // ── 기둥 8본 (민흘림 원주) ──
            var cols = new GwanaMeshKit(GwanaMeshKit.MpuWood);
            foreach (float cx in ColXs)
                foreach (float cz in new[] { ZS, ZN })
                    cols.Cylinder(new Vector3(cx, FloorY, cz), ColR0, ColR1, ChangbangY1, 16);

            Piece(group.transform, "벽_회벽", plaster, MatHoebyeok);
            Piece(group.transform, "벽_목부재", wood, MatMokjae);
            Piece(group.transform, "기둥", cols, MatMokjae);
        }

        /// <summary>
        /// 벽 한 면 — 회벽 밭 + 심벽 목부재(하방·중방·창방·기둥).
        /// alongX=true 면 벽이 Z로 뻗고 면은 x=facePos, inward = 방이 있는 쪽(±1).
        /// 개구부는 하나만 받는다(창 또는 문) — 이 방은 벽마다 하나뿐이다.
        /// ⚠️ 심벽 기둥은 **켜마다 잘라** 세운다. 통짜로 세워 중방과 겹치면
        ///    둘 다 같은 평면이라 앞면끼리 z-파이팅이 난다 (관측실에서 밟은 함정).
        /// </summary>
        static void WallFace(GwanaMeshKit plaster, GwanaMeshKit wood,
                             bool alongX, float facePos, int inward,
                             float a0, float a1, float[] colA,
                             float oa0, float oa1, float oy0, float oy1)
        {
            float back = facePos - inward * WallT;         // 벽 바깥면
            float pf = facePos + inward * Proud;           // 목부재 앞면
            float ps = facePos - inward * Sink;            // 목부재 뒷면(회벽 속)
            float top = ChangbangY1;

            // (a, y, c) → 월드
            System.Func<float, float, float, Vector3> P = alongX
                ? (System.Func<float, float, float, Vector3>)((a, y, c) => new Vector3(c, y, a))
                : ((a, y, c) => new Vector3(a, y, c));

            System.Action<GwanaMeshKit, float, float, float, float, float, float> B =
                (k, aa0, aa1, yy0, yy1, cc0, cc1) =>
                {
                    var lo = P(Mathf.Min(aa0, aa1), Mathf.Min(yy0, yy1), Mathf.Min(cc0, cc1));
                    var hi = P(Mathf.Max(aa0, aa1), Mathf.Max(yy0, yy1), Mathf.Max(cc0, cc1));
                    k.BoxMinMax(lo.x, hi.x, lo.y, hi.y, lo.z, hi.z);
                };

            // ── 회벽 밭 (개구부를 피해 네 조각) ──
            B(plaster, a0, oa0, FloorY, top, back, facePos);              // 개구부 앞쪽
            B(plaster, oa1, a1, FloorY, top, back, facePos);              // 개구부 뒤쪽
            // ⚠️ **높이가 0인 조각은 만들지 않는다.** 문은 개구부가 바닥(FloorY)에서 시작하므로
            //    "개구부 아래" 조각의 높이가 0이 되는데, BoxMinMax는 그래도 상자를 만들어
            //    **y=0에 윗면 하나를 남긴다.** 그 면이 바닥_마루 윗면(역시 y=0)과 정확히 같은
            //    평면에 놓여, 문지방(z 2.20~2.44)에서 회벽 색과 마루 색이 시점에 따라 번갈아
            //    나타났다 (2026-08-20 실측: 간격 0.00mm, 겹침 1.10×0.24m).
            //    같은 이유로 개구부 위 조각도 높이 0이면 건너뛴다.
            if (oy0 > FloorY + 1e-4f) B(plaster, oa0, oa1, FloorY, oy0, back, facePos);   // 개구부 아래 (창이면 하방 밑)
            if (top > oy1 + 1e-4f) B(plaster, oa0, oa1, oy1, top, back, facePos);         // 개구부 위 (인방 위 소벽)

            // ── 하방 / 중방 / 창방 ──
            // ⚠️ **개구부와 높이가 겹치는 켜는 잘라 낸다.** 통짜로 두면 중방(1.13~1.30)이
            //    비밀문 문간을 가로질러, 문을 열어도 그 가로재만 벽에 남는다 (사용자 지적).
            //    문선(수직 문설주+상인방)은 BuildOpenings가 따로 세우므로 테두리는 그대로 산다.
            //    창은 격자창이 이 켜보다 더 앞(0.10)에 붙어 있어 잘라도 보이는 차이가 없다.
            System.Action<float, float> Rail = (ry0, ry1) =>
            {
                bool crosses = ry1 > oy0 && ry0 < oy1;
                if (!crosses) { B(wood, a0, a1, ry0, ry1, ps, pf); return; }
                B(wood, a0, oa0, ry0, ry1, ps, pf);
                B(wood, oa1, a1, ry0, ry1, ps, pf);
            };
            Rail(FloorY, HabangY);
            Rail(JungbangY0, JungbangY1);
            Rail(ChangbangY0, ChangbangY1);

            // ── 심벽 기둥 (켜마다 따로) ──
            float hw = SimbyeokColW * 0.5f;
            foreach (float ca in colA)
            {
                if (ca + hw > oa0 && ca - hw < oa1) continue;   // 개구부를 물면 세우지 않는다
                B(wood, ca - hw, ca + hw, HabangY, JungbangY0, ps, pf);
                B(wood, ca - hw, ca + hw, JungbangY1, ChangbangY0, ps, pf);
            }
        }

        // ══════════════════════════════════════════════════════
        // ② 천장 — 맞배 연등천장 (대들보 · 도리 3줄 · 서까래 · 개판)
        // ══════════════════════════════════════════════════════
        static void BuildCeiling()
        {
            var group = RecreateGroup("집무실_천장");
            var wood = new GwanaMeshKit(GwanaMeshKit.MpuWood);

            // 대들보 2본 (x = ±ColX, z방향) — 기둥 머리에 얹힌다
            foreach (float s in new[] { -1f, 1f })
                wood.BoxMinMax(s * ColX - BeamW * 0.5f, s * ColX + BeamW * 0.5f,
                               BeamY0, BeamY1, OutZS, OutZN);

            // 도리 3줄 (X축과 나란) — **윗면이 정확히 한 평면**에 오도록 높이를 잡았다.
            // GwanaMeshKit.Cylinder는 +Y로만 뻗어 눕힐 수 없으므로, 축정렬 상자 + 45° 상자를
            // 겹친 팔각 단면으로 낸다 (원도리와 실루엣 차이가 눈에 띄지 않는다).
            System.Action<float, float, float> Dori = (z, y, r) =>
            {
                // 팔각 단면: 정사각 2개를 45° 엇갈려 겹친 근사 (축정렬 + 회전 1개)
                wood.BoxMinMax(-OutX, OutX, y - r, y + r, z - r, z + r);
                wood.BoxRot(new Vector3(0f, y, z), new Vector3(OutX * 2f, r * 1.42f, r * 1.42f),
                            Quaternion.Euler(45f, 0f, 0f));
            };
            Dori(-EaveZ, EaveY, PurlinR);
            Dori(EaveZ, EaveY, PurlinR);
            Dori(-MidZ, MidY, MidPurlinR);
            Dori(MidZ, MidY, MidPurlinR);
            Dori(0f, RidgeY, PurlinR);

            // 동자주 · 대공 (대들보 위에 서서 중도리·종도리를 받는다)
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                    wood.BoxMinMax(sx * ColX - 0.11f, sx * ColX + 0.11f,
                                   BeamY1, MidY - MidPurlinR, sz * MidZ - 0.11f, sz * MidZ + 0.11f);
                wood.BoxMinMax(sx * ColX - 0.12f, sx * ColX + 0.12f,
                               BeamY1, RidgeY - PurlinR, -0.12f, 0.12f);
            }

            Piece(group.transform, "지붕틀", wood, MatMokjae);

            // ── 서까래 (Z방향, 도리 위에 얹혀 물매를 따라간다) ──
            var rafters = new GwanaMeshKit(GwanaMeshKit.MpuWood);
            float slopeDeg = Mathf.Atan(0.42035f) * Mathf.Rad2Deg;      // 22.8°
            int n = Mathf.FloorToInt((OutX * 2f - 0.30f) / 0.375f);
            float step = (OutX * 2f - 0.30f) / n;
            for (int i = 0; i <= n; i++)
            {
                float x = -OutX + 0.15f + i * step;
                foreach (float s in new[] { -1f, 1f })
                {
                    float zA = 0f, zB = s * OutZN;
                    float yA = RafterTopY(0f), yB = RafterTopY(OutZN);
                    float len = Mathf.Sqrt((zB - zA) * (zB - zA) + (yB - yA) * (yB - yA));
                    // 중심을 윗면에서 두께 절반만큼 내린다 (경사 보정)
                    float half = RafterT * 0.5f / Mathf.Cos(slopeDeg * Mathf.Deg2Rad);
                    rafters.BoxRot(new Vector3(x, (yA + yB) * 0.5f - half, (zA + zB) * 0.5f),
                                   new Vector3(RafterT, RafterT, len),
                                   Quaternion.Euler(s * slopeDeg, 0f, 0f));
                }
            }
            Piece(group.transform, "서까래", rafters, MatMokjae);

            // ── 개판 (서까래 위 널 — 서까래 사이로 보이는 천장면) ──
            var pan = new GwanaMeshKit(0.55f);
            foreach (float s in new[] { -1f, 1f })
            {
                float len = Mathf.Sqrt(OutZN * OutZN + Mathf.Pow(RafterTopY(0f) - RafterTopY(OutZN), 2f));
                float half = PanT * 0.5f / Mathf.Cos(slopeDeg * Mathf.Deg2Rad);
                pan.BoxRot(new Vector3(0f, (RafterTopY(0f) + RafterTopY(OutZN)) * 0.5f + half, s * OutZN * 0.5f),
                           new Vector3(OutX * 2f, PanT, len),
                           Quaternion.Euler(s * slopeDeg, 0f, 0f));
            }
            Piece(group.transform, "개판", pan, MatPan);
        }

        // ══════════════════════════════════════════════════════
        // ③ 개구부 — 격자창 2 · 들어온 문 · 비밀문
        // ══════════════════════════════════════════════════════
        static void BuildOpenings()
        {
            var group = RecreateGroup("집무실_창호");

            // ── 격자창 (서·동) ──
            Lattice(group.transform, "격자창_서", true, -HalfX, +1, WinWestZ0, WinWestZ1, WinY0, WinY1);
            Lattice(group.transform, "격자창_동", true, HalfX, -1, WinEastZ0, WinEastZ1, WinY0, WinY1);

            // ── 남벽 분합문 2짝 (들어온 문 — 닫혀 있다) ──
            {
                var frame = new GwanaMeshKit(GwanaMeshKit.MpuWood);
                frame.BoxMinMax(-DoorHalfX - 0.07f, -DoorHalfX + Lip, FloorY, DoorTopY + 0.07f, ZS - 0.02f, ZS + 0.08f);
                frame.BoxMinMax(DoorHalfX - Lip, DoorHalfX + 0.07f, FloorY, DoorTopY + 0.07f, ZS - 0.02f, ZS + 0.08f);
                frame.BoxMinMax(-DoorHalfX - 0.07f, DoorHalfX + 0.07f, DoorTopY - Lip, DoorTopY + 0.07f, ZS - 0.02f, ZS + 0.08f);
                Piece(group.transform, "남문_문선", frame, MatMunmok);

                var leafW = new GwanaMeshKit(GwanaMeshKit.MpuWood);
                var leafP = new GwanaMeshKit(1.0f);
                foreach (float s in new[] { -1f, 1f })
                {
                    float x0 = s > 0 ? 0f : -DoorHalfX, x1 = s > 0 ? DoorHalfX : 0f;
                    DoorLeaf(leafW, leafP, x0, x1, FloorY, DoorTopY, ZS + 0.015f, ZS + 0.055f);
                }
                Piece(group.transform, "남문_문짝", leafW, MatMunmok);
                Piece(group.transform, "남문_창호지", leafP, MatHanji);
            }

            // ── 북벽 비밀문 (잠김) ──
            {
                var frame = new GwanaMeshKit(GwanaMeshKit.MpuWood);
                frame.BoxMinMax(-SecretHalfX - 0.07f, -SecretHalfX + Lip, FloorY, SecretTopY + 0.07f, ZN - 0.08f, ZN + 0.02f);
                frame.BoxMinMax(SecretHalfX - Lip, SecretHalfX + 0.07f, FloorY, SecretTopY + 0.07f, ZN - 0.08f, ZN + 0.02f);
                frame.BoxMinMax(-SecretHalfX - 0.07f, SecretHalfX + 0.07f, SecretTopY - Lip, SecretTopY + 0.07f, ZN - 0.08f, ZN + 0.02f);
                Piece(group.transform, "비밀문_문선", frame, MatMunmok);

                // 문짝 — 별 GO (LockedDoor가 경첩 회전시킨다)
                var leaf = new GwanaMeshKit(GwanaMeshKit.MpuWood);
                leaf.BoxMinMax(-SecretHalfX, SecretHalfX, FloorY + 0.02f, SecretTopY - 0.02f, ZN - 0.055f, ZN - 0.015f);
                // 널 사이 틈이 보이게 세로 띠 5줄을 앞으로 덧댄다
                for (int i = 1; i < 5; i++)
                {
                    float x = -SecretHalfX + i * (SecretHalfX * 2f / 5f);
                    leaf.BoxMinMax(x - 0.012f, x + 0.012f, FloorY + 0.02f, SecretTopY - 0.02f,
                                   ZN - 0.075f, ZN - 0.055f);
                }
                // 가로 띠쇠 2줄
                foreach (float y in new[] { 0.42f, 1.42f })
                    leaf.BoxMinMax(-SecretHalfX + 0.02f, SecretHalfX - 0.02f, y - 0.035f, y + 0.035f,
                                   ZN - 0.078f, ZN - 0.058f);

                var doorGo = PieceGO(group.transform, "비밀문_문짝", leaf, MatSecret);
                // ⚠️ **정적 플래그를 반드시 지운다.** Piece는 기본으로 BatchingStatic을 걸어 두는데,
                //    정적 배칭은 메시를 월드 공간에 구워 버려 **트랜스폼을 움직여도 그림이 안 따라온다**.
                //    콜라이더는 정상적으로 돌아가서 통과는 되는데 문짝만 제자리에 남는다 —
                //    "문이 열렸다는데 화면엔 닫혀 있다"로 나타난다 (Play에서 실측).
                GameObjectUtility.SetStaticEditorFlags(doorGo, 0);
                var col = doorGo.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, (SecretTopY) * 0.5f, ZN - 0.045f);
                col.size = new Vector3(SecretHalfX * 2f, SecretTopY, 0.09f);
                var lockedDoor = doorGo.AddComponent<LockedDoor>();
                lockedDoor.displayName = "낡은 널문";
                lockedDoor.pivotInParent = new Vector3(-SecretHalfX, 0f, ZN - 0.035f);
                lockedDoor.openAngle = -100f;    // 계단 쪽(북)으로 열린다
                Debug.Log("[집무실] 비밀문 = 잠김 (LockedDoor.locked). 퍼즐 쪽에서 Unlock() 호출하면 열린다");
            }
        }

        /// <summary>격자창 한 짝 — 문틀 + 창호지 + 살대(세로·가로). 벽을 뚫지 않고 벽면에 붙인다.</summary>
        static void Lattice(Transform parent, string name, bool alongX, float facePos, int inward,
                            float a0, float a1, float y0, float y1)
        {
            var wood = new GwanaMeshKit(GwanaMeshKit.MpuWood);
            var paper = new GwanaMeshKit(1.0f);

            float c0 = facePos, c1 = facePos + inward * 0.10f;         // 벽면 → 방 안쪽 10cm
            float cPaper0 = facePos + inward * 0.020f, cPaper1 = facePos + inward * 0.035f;
            float cSal0 = facePos + inward * 0.035f, cSal1 = facePos + inward * 0.062f;

            System.Func<float, float, float, Vector3> P = alongX
                ? (System.Func<float, float, float, Vector3>)((a, y, c) => new Vector3(c, y, a))
                : ((a, y, c) => new Vector3(a, y, c));
            System.Action<GwanaMeshKit, float, float, float, float, float, float> B =
                (k, aa0, aa1, yy0, yy1, cc0, cc1) =>
                {
                    var lo = P(Mathf.Min(aa0, aa1), Mathf.Min(yy0, yy1), Mathf.Min(cc0, cc1));
                    var hi = P(Mathf.Max(aa0, aa1), Mathf.Max(yy0, yy1), Mathf.Max(cc0, cc1));
                    k.BoxMinMax(lo.x, hi.x, lo.y, hi.y, lo.z, hi.z);
                };

            // 문틀 (사방 테두리)
            const float fw = 0.075f;
            B(wood, a0 - fw, a0, y0 - fw, y1 + fw, c0, c1);
            B(wood, a1, a1 + fw, y0 - fw, y1 + fw, c0, c1);
            B(wood, a0, a1, y0 - fw, y0, c0, c1);
            B(wood, a0, a1, y1, y1 + fw, c0, c1);
            // 창호지
            B(paper, a0, a1, y0, y1, cPaper0, cPaper1);
            // 살대 — 세로 촘촘, 가로 성글게 (띠살창)
            int nv = Mathf.Max(3, Mathf.RoundToInt((a1 - a0) / 0.135f));
            for (int i = 1; i < nv; i++)
            {
                float a = a0 + i * (a1 - a0) / nv;
                B(wood, a - 0.013f, a + 0.013f, y0, y1, cSal0, cSal1);
            }
            int nh = Mathf.Max(2, Mathf.RoundToInt((y1 - y0) / 0.30f));
            for (int i = 1; i < nh; i++)
            {
                float y = y0 + i * (y1 - y0) / nh;
                B(wood, a0, a1, y - 0.013f, y + 0.013f, cSal0, cSal1);
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Piece(go.transform, name + "_틀", wood, MatMunmok);
            Piece(go.transform, name + "_창호지", paper, MatHanji);
        }

        /// <summary>문짝 한 짝 — 틀 + 창호지 + 띠살.</summary>
        static void DoorLeaf(GwanaMeshKit wood, GwanaMeshKit paper,
                             float x0, float x1, float y0, float y1, float c0, float c1)
        {
            const float fw = 0.065f;
            wood.BoxMinMax(x0, x0 + fw, y0, y1, c0, c1);
            wood.BoxMinMax(x1 - fw, x1, y0, y1, c0, c1);
            wood.BoxMinMax(x0, x1, y0, y0 + fw, c0, c1);
            wood.BoxMinMax(x0, x1, y1 - fw, y1, c0, c1);
            wood.BoxMinMax(x0, x1, y0 + 0.32f, y0 + 0.38f, c0, c1);      // 궁창 가로대
            paper.BoxMinMax(x0 + fw, x1 - fw, y0 + 0.38f, y1 - fw, c0 + 0.012f, c0 + 0.024f);
            wood.BoxMinMax(x0 + fw, x1 - fw, y0 + fw, y0 + 0.32f, c0 + 0.004f, c1 - 0.004f);  // 궁창널
            // 띠살 (아래·가운데·위 세 묶음)
            int nv = 5;
            for (int i = 1; i < nv; i++)
            {
                float x = x0 + fw + i * (x1 - x0 - fw * 2f) / nv;
                wood.BoxMinMax(x - 0.011f, x + 0.011f, y0 + 0.38f, y1 - fw, c0 + 0.024f, c0 + 0.044f);
            }
            foreach (float fy in new[] { 0.20f, 0.50f, 0.80f })
            {
                float y = y0 + 0.38f + fy * (y1 - fw - y0 - 0.38f);
                for (int j = -1; j <= 1; j++)
                    wood.BoxMinMax(x0 + fw, x1 - fw, y + j * 0.035f - 0.010f, y + j * 0.035f + 0.010f,
                                   c0 + 0.024f, c0 + 0.044f);
            }
        }

        // ══════════════════════════════════════════════════════
        // ④ 비밀 계단 (북벽 너머 — 서고로 내려간다)
        // ══════════════════════════════════════════════════════
        /// <summary>
        /// 비밀 통로 — 비밀문 → 계단 → 짧은 통로 → Exit_ToArchive.
        ///
        /// ⚠️ 폭·층고·계단 물매·재질을 **서고 씬의 「관아 비밀 통로」와 동일**하게 맞췄다
        ///    (ArchiveBuilder: W 1.70 / H 2.30 / T 0.30 / 0.165×0.32, 다듬은 석벽 + 목재 리브
        ///    + 목재 천장널). 씬 전환으로 그쪽 통로에 이어지므로 치수가 다르면 딴 길이 된다.
        ///    관측실 암문(거친 막돌 동굴)과 대비되는 **정돈된 길** — 수령이 손본 통로다.
        ///
        /// 경로는 아래 한 줄로 읽힌다. 보행 약 17.4m ≈ 6초(3m/s), 하강 2.64m.
        /// 마커 Exit_ToArchive는 실제 끝점으로 덮어쓰므로 경로를 바꿔도 따라온다.
        /// </summary>
        static void BuildSecretPassage()
        {
            var group = RecreateGroup("집무실_비밀통로");
            var stone = new GwanaMeshKit(0.31f);   // UvCut  (= ArchiveBuilder)
            var floor = new GwanaMeshKit(0.34f);   // UvFloor
            var wood = new GwanaMeshKit(0.55f);    // UvWood
            var dark = new GwanaMeshKit(4f);
            var lamps = new List<Vector3>();

            // ⚠️ 통로 콜라이더는 **여기서 바로 씬에 만든다.** 예전엔 static 원장에 모아 두고
            //    WalkSetup이 꺼내 썼는데, static은 **도메인 리로드(스크립트 재컴파일)에서 비워진다**.
            //    그 상태로 보행 콜라이더만 다시 돌리면 통로 바닥이 통째로 사라져 워커가 −242까지
            //    추락한다 (실측으로 잡음). 형상과 콜라이더를 같은 자리에서 만들어 둬야 안전하다.
            var colRoot = new GameObject("통로_콜라이더");
            colRoot.transform.SetParent(group.transform, false);
            int colCount = 0;

            const float W = PassW, H = PassH, T = PassT;
            void Col(Vector3 c, Vector3 s, Quaternion r)
            {
                var go = new GameObject("통로");
                go.transform.SetParent(colRoot.transform, false);
                go.transform.SetPositionAndRotation(c, r);
                go.AddComponent<BoxCollider>().size = s;
                colCount++;
            }
            Vector3 pos = PassStart;
            Vector3 dir = Vector3.forward;
            float fy = FloorY;
            float ribDist = 0f, lampDist = LampSpacing * 0.5f;

            void Rib(Vector3 at, Vector3 d, float y)
            {
                var right = Vector3.Cross(Vector3.up, d);
                var rot = Quaternion.LookRotation(d);
                wood.BoxRot(at + right * (W / 2f - 0.055f) + Vector3.up * (y + H / 2f),
                            new Vector3(0.11f, H, 0.16f), rot);
                wood.BoxRot(at - right * (W / 2f - 0.055f) + Vector3.up * (y + H / 2f),
                            new Vector3(0.11f, H, 0.16f), rot);
                wood.BoxRot(at + Vector3.up * (y + H - 0.075f), new Vector3(W, 0.15f, 0.16f), rot);
            }

            void Straight(float len)
            {
                var rot = Quaternion.LookRotation(dir);
                var right = Vector3.Cross(Vector3.up, dir);
                floor.BoxRot(pos + dir * (len / 2f) + Vector3.up * (fy - T / 2f),
                             new Vector3(W + 2 * T, T, len), rot);
                wood.BoxRot(pos + dir * (len / 2f) + Vector3.up * (fy + H + T / 2f),
                            new Vector3(W + 2 * T, T, len), rot);
                foreach (float s in new[] { 1f, -1f })
                    stone.BoxRot(pos + dir * (len / 2f) + right * s * (W / 2f + T / 2f)
                                 + Vector3.up * (fy + H / 2f), new Vector3(T, H, len), rot);
                Col(pos + dir * (len / 2f) + Vector3.up * (fy - 0.15f), new Vector3(W, 0.30f, len), rot);
                foreach (float s in new[] { 1f, -1f })
                    Col(pos + dir * (len / 2f) + right * s * (W / 2f + T / 2f)
                        + Vector3.up * (fy + H / 2f), new Vector3(T, H, len), rot);
                // 천장 — 없으면 위로 빠져나갈 수 있다
                Col(pos + dir * (len / 2f) + Vector3.up * (fy + H + T / 2f),
                    new Vector3(W + 2 * T, T, len), rot);

                for (float t = 0f; t < len; t += 0.1f)
                {
                    ribDist += 0.1f; lampDist += 0.1f;
                    if (ribDist < RibSpacing) continue;
                    ribDist = 0f;
                    var at = pos + dir * t;
                    Rib(at, dir, fy);
                    if (lampDist >= LampSpacing) { lampDist = 0f; lamps.Add(at + Vector3.up * (fy + H - 0.42f)); }
                }
                pos += dir * len;
            }

            void Stairs(int n)
            {
                float len = n * StepRun, drop = n * StepRise;
                var rot = Quaternion.LookRotation(dir);
                var right = Vector3.Cross(Vector3.up, dir);
                foreach (float s in new[] { 1f, -1f })
                    stone.BoxRot(pos + dir * (len / 2f) + right * s * (W / 2f + T / 2f)
                                 + Vector3.up * (fy + (H - drop) / 2f),
                                 new Vector3(T, H + drop, len), rot);
                float ang = Mathf.Atan2(drop, len) * Mathf.Rad2Deg;
                wood.BoxRot(pos + dir * (len / 2f) + Vector3.up * (fy - drop / 2f + H + T / 2f),
                            new Vector3(W + 2 * T, T, Mathf.Sqrt(len * len + drop * drop) + 0.3f),
                            rot * Quaternion.Euler(ang, 0f, 0f));
                // ⚠️ 디딤돌은 단마다 **바닥까지 통짜 블록**. 밑을 길게 한 장으로 깔면
                //    그 윗면이 다음 단 디딤면과 같은 높이가 되어 계단이 경사로로 보인다(실측).
                for (int i = 0; i < n; i++)
                {
                    float topY = fy - StepRise * (i + 1);
                    float botY = fy - drop - T;
                    floor.BoxRot(pos + dir * (StepRun * (i + 0.5f)) + Vector3.up * ((topY + botY) / 2f),
                                 new Vector3(W, topY - botY, StepRun + 0.02f), rot);
                }
                // 보행은 8개 디딤돌 대신 **경사 램프 한 장**으로 받는다.
                // ⚠️ 길이는 정확히 빗변, 중심은 판 두께 절반만큼 아래(sink) — 여유를 주면
                //    상단이 디딤면 위로 솟아 턱이 된다 (관아 씬에서 밟은 함정).
                //    `Euler(+θ,0,0)`은 +Z 끝을 내리므로 진행 방향이 내려가는 이 계단엔 +θ가 맞다.
                {
                    const float ct = 0.30f;
                    float sink = ct * 0.5f * Mathf.Cos(ang * Mathf.Deg2Rad);
                    Col(pos + dir * (len / 2f) + Vector3.up * (fy - drop / 2f - sink),
                        new Vector3(W, ct, Mathf.Sqrt(len * len + drop * drop)),
                        rot * Quaternion.Euler(ang, 0f, 0f));
                    foreach (float s in new[] { 1f, -1f })
                        Col(pos + dir * (len / 2f) + right * s * (W / 2f + T / 2f)
                            + Vector3.up * (fy + (H - drop) / 2f), new Vector3(T, H + drop, len), rot);
                    // 경사 천장
                    Col(pos + dir * (len / 2f) + Vector3.up * (fy - drop / 2f + H + T / 2f),
                        new Vector3(W + 2 * T, T, Mathf.Sqrt(len * len + drop * drop) + 0.3f),
                        rot * Quaternion.Euler(ang, 0f, 0f));
                }

                Rib(pos + dir * 0.10f, dir, fy);
                // 계단 중앙 등롱 — 없으면 하강 구간이 통째로 어둠 (서고 통로에서 확인된 규칙)
                lamps.Add(pos + dir * (len / 2f) + Vector3.up * (fy - drop / 2f + H - 0.40f));
                ribDist = 0f; lampDist = 0f;
                pos += dir * len;
                fy -= drop;
            }

            // (꺾임 헬퍼 Turn 은 삭제했다 — 이 통로는 직선 하나뿐이다.
            //  다시 필요해지면 서고 씬 ArchiveBuilder 의 Turn 을 참고할 것)

            // ── 문간 바닥 ──
            // 방 바닥은 z 2.20에서 끝나고 통로 바닥은 2.44에서 시작한다. 이 0.24m를 안 깔면
            // 문지방에서 발밑이 비어 떨어진다 (사용자 지시 "바닥 전부 막아라")
            Col(new Vector3(0f, FloorY - 0.15f, (ZN + OutZN) * 0.5f),
                new Vector3(W, 0.30f, OutZN - ZN), Quaternion.identity);

            // ── 경로 — **직선 하나. 꺾임·갈래 없음** (사용자 지시 2026-08-16) ──
            // 진짜 긴 통로는 서고 씬(Gyeonu_Observatory)에 이미 있다. 여기는 그리로 넘어가기
            // 직전의 **짧은 진입부**일 뿐이라 3~5초에 끝나야 한다. 보행 10.2m ≈ 3.4초.
            Straight(1.50f);
            Stairs(10);
            Straight(5.50f);

            // ── 끝 — 통로가 어둠으로 계속되는 척 (여기서 서고 씬으로 전환) ──
            // 마지막 구간은 등롱 간격에 안 걸려 캄캄해진다 → 끝 앞에 하나 못 박아 둔다
            lamps.Add(pos - dir * 0.95f + Vector3.up * (fy + H - 0.42f));
            {
                var rot = Quaternion.LookRotation(dir);
                dark.BoxRot(pos + Vector3.up * (fy + H / 2f), new Vector3(W, H, 0.06f), rot);
                stone.BoxRot(pos + dir * 0.15f + Vector3.up * (fy + H / 2f),
                             new Vector3(W + 2 * T, H + 2 * T, T), rot);
                // 끝벽 콜라이더 — 이게 없으면 어둠 판을 그대로 뚫고 나간다 (사용자 지적)
                Col(pos + dir * 0.15f + Vector3.up * (fy + H / 2f),
                    new Vector3(W + 2 * T, H + 2 * T, T), rot);
            }
            PassageEnd = pos - dir * 0.55f + Vector3.up * fy;
            PassageEndYaw = Quaternion.LookRotation(dir).eulerAngles.y;

            Piece(group.transform, "통로_석벽", stone, MatPassStone);
            Piece(group.transform, "통로_바닥", floor, MatPassFloor);
            Piece(group.transform, "통로_목재", wood, MatPassWood);
            Piece(group.transform, "통로_어둠", dark, MatDark);

            // ── 등롱 (통로는 낮·밤 관계없이 늘 어둡다 — 조명 그룹 밖에 둔다) ──
            var lampRoot = new GameObject("통로_등롱");
            lampRoot.transform.SetParent(group.transform, false);
            foreach (var lp in lamps) Lantern(lampRoot.transform, lp);

            Debug.Log("[집무실] 비밀 통로 — 보행 " + PassageWalkLen.ToString("F1") + "m (약 " +
                      (PassageWalkLen / 3f).ToString("F1") + "초) / 하강 " + (-fy).ToString("F2") +
                      "m / 등롱 " + lamps.Count + " / 콜라이더 " + colCount + " / 끝 " + PassageEnd.ToString("F2"));
        }

        internal struct ColBox { public Vector3 c, s; public Quaternion r; }
        /// <summary>통로 보행 콜라이더 — 경로를 걸으며 같이 뽑아 둔다 (WalkSetup이 그대로 박스로 낸다).
        /// 경로를 바꿔도 콜라이더가 따라오게 하려고 route와 한 곳에서 만든다.</summary>
        internal static readonly List<ColBox> PassageCols = new List<ColBox>();
        internal static Vector3 PassageEnd;
        internal static float PassageEndYaw;
        const float PassageWalkLen = 1.50f + 10 * StepRun + 5.50f;   // 10.20m ≈ 3.4초

        /// <summary>등롱 하나 — 목재 틀 + 한지 + 불씨. 서고 통로의 등롱과 같은 문법.</summary>
        static void Lantern(Transform parent, Vector3 pos)
        {
            var go = new GameObject("등롱");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var frame = new GwanaMeshKit(0.4f);
            const float r = 0.105f, h = 0.30f;
            frame.BoxMinMax(-r, r, h * 0.5f, h * 0.5f + 0.035f, -r, r);      // 갓
            frame.BoxMinMax(-r, r, -h * 0.5f - 0.03f, -h * 0.5f, -r, r);     // 받침
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    frame.BoxMinMax(sx * r - 0.018f, sx * r, -h * 0.5f, h * 0.5f, sz * r - 0.018f, sz * r);
            frame.BoxMinMax(-0.012f, 0.012f, h * 0.5f + 0.035f, h * 0.5f + 0.16f, -0.012f, 0.012f);  // 걸이
            var f = PieceGO(go.transform, "등롱_틀", frame, MatPassWood);
            if (f != null) f.transform.position = pos;

            var paper = new GwanaMeshKit(0.4f);
            paper.BoxMinMax(-r + 0.014f, r - 0.014f, -h * 0.5f + 0.01f, h * 0.5f - 0.01f,
                            -r + 0.014f, r - 0.014f);
            var p = PieceGO(go.transform, "등롱_한지", paper, MatLanternPaper);
            if (p != null) p.transform.position = pos;

            var lgo = new GameObject("불빛");
            lgo.transform.SetParent(go.transform, false);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            // 5를 넘기면 1m 거리의 석벽이 흰색으로 타 버린다 (실측) — 통로 재질은 서고와 공유하는
            // 에셋이라 틴트를 못 건드리므로 밝기로 맞춘다
            l.intensity = 4.5f;
            // ⚠️ **사거리 2.5를 넘기지 말 것.** 그림자가 없는 점광원은 벽을 그냥 통과하고,
            //    더 나쁘게는 URP가 광원을 **오브젝트 바운즈 기준으로 고르기 때문에** 방의 큰
            //    메시(바닥·회벽, z −2.44~2.44)에 사거리 구가 닿으면 창빛 하나를 밀어낸다.
            //    첫 등롱이 z 5.02이므로 5.02 − 2.5 = 2.52 > 2.44 로 아슬하게 방을 비껴간다.
            //    (밤에 방이 통째로 주황색으로 밝아지던 원인이 이것이었다 — 실측)
            l.range = 2.5f;
            l.color = new Color(1.00f, 0.83f, 0.58f);
            l.shadows = LightShadows.None;
        }

        // ══════════════════════════════════════════════════════
        // ⑤ 조명
        // ══════════════════════════════════════════════════════
        /// <summary>서안 위 등잔 자리 — 밤 광원과 Furnisher의 등잔 기물이 공유한다.</summary>
        internal static readonly Vector3 LampSpot = new Vector3(0.36f, 0.692f, 0.62f);

        static void BuildLighting()
        {
            // 재생성해도 낮/밤 상태는 유지한다 (사용자가 골라 둔 상태를 되돌리지 않는다)
            var prev = Object.FindFirstObjectByType<OfficeTimeOfDay>();
            bool wasNight = prev != null && prev.IsNight;

            var group = RecreateGroup("집무실_조명");

            RenderSettings.fog = false;
            RenderSettings.skybox = null;

            var cookie = BuildLatticeCookie();
            var day = new GameObject("낮"); day.transform.SetParent(group.transform, false);
            var night = new GameObject("밤"); night.transform.SetParent(group.transform, false);

            // ⚠️ **추가 광원은 4개까지다.** URP `PC_RPAsset`의 maxAdditionalLightsCount = 4 이고
            //    그 에셋은 `_Project/Settings/` 소속이라 수정 금지(팀 전체 전파). 5번째를 놓으면
            //    오브젝트마다 가까운 4개만 골라 켜져 창빛이 들쭉날쭉해진다.
            //    그래서 **방향광 1(메인) + 추가 4** 로 정확히 맞춘다 — 메인 광원은 이 한도 밖이다.

            // ══════ 낮 ══════ 수령이 있는 시간. 좌우 창에서 강한 측광
            // 방향광은 그림자를 끈다 — 켜면 닫힌 상자라 실내에 한 줌도 못 들어온다.
            // 끈 채로 두면 방향성 있는 명암만 얹히는 값싼 채움이 되고, 진짜 그림자는 창 스포트가 만든다.
            Dir(day, "방향광_낮", new Vector3(32f, 74f, 0f), 0.48f, new Color(1.00f, 0.94f, 0.84f));

            // ⚠️ 창 스포트의 겨냥을 수평에 가깝게 두면 안 된다 (1차에서 밟음). 원뿔의 뜨거운 중심이
            //    맞은편 **윗벽**을 때려 회벽이 흰 띠로 타 버린다. 실제 창빛처럼 아래로 꽂아야
            //    빛 웅덩이가 바닥에 생기고 윗벽은 앰비언트에 남는다.
            var key = Spot(day, "서창_햇살",
                           new Vector3(-HalfX + 0.16f, 1.92f, -0.10f),
                           new Vector3(1.90f, -1.05f, -0.50f),
                           150f, 12f, 82f, new Color(1.00f, 0.90f, 0.73f));
            Shadowed(key, 0.92f, cookie);

            var fill = Spot(day, "동창_햇살",
                            new Vector3(HalfX - 0.16f, 1.88f, 0.40f),
                            new Vector3(-1.00f, -1.30f, -0.35f),
                            55f, 11f, 78f, new Color(1.00f, 0.93f, 0.80f));
            Shadowed(fill, 0.75f, cookie);

            Lamp(day, "서창_창면빛", new Vector3(-HalfX + 0.42f, 1.30f, 0f), 12f, 3.0f,
                 new Color(1.00f, 0.94f, 0.82f));
            Lamp(day, "동창_창면빛", new Vector3(HalfX - 0.42f, 1.30f, 0.40f), 8f, 2.8f,
                 new Color(1.00f, 0.95f, 0.86f));

            // ══════ 밤 ══════ 아무도 없는 시간. 달빛이 희미하게, 실내는 등잔 하나
            Dir(night, "방향광_밤", new Vector3(24f, 74f, 0f), 0.10f, new Color(0.62f, 0.72f, 1.00f));

            // 달빛 — 같은 창에서 같은 창살 무늬로 들어오되 차갑고 훨씬 약하다.
            // 그림자를 살려 두는 이유: 바닥의 창살 무늬가 밤에도 이 방의 인상이기 때문
            var moon = Spot(night, "서창_달빛",
                            new Vector3(-HalfX + 0.16f, 1.92f, -0.10f),
                            new Vector3(1.90f, -1.05f, -0.50f),
                            16f, 12f, 82f, new Color(0.60f, 0.72f, 1.00f));
            Shadowed(moon, 0.90f, cookie);

            var moonE = Spot(night, "동창_달빛",
                             new Vector3(HalfX - 0.16f, 1.88f, 0.40f),
                             new Vector3(-1.00f, -1.30f, -0.35f),
                             5f, 10f, 78f, new Color(0.58f, 0.70f, 1.00f));
            moonE.shadows = LightShadows.None;
            moonE.cookie = cookie;

            // 등잔 — 밤의 유일한 실내 광원. 서안 위 등잔 기물이 광원처럼 읽힌다.
            // ⚠️ 광원을 등잔 **몸통 안**에 두면 안 된다 — 그림자를 켜 둔 탓에 등잔 자신의 메시가
            //    빛을 거의 다 막아 방이 캄캄해진다. 기물 윗면(0.48m) 바로 위에 얹어
            //    서안으로 쏟아지는 웅덩이를 만든다 (관측실 Room00의 "밝은 웅덩이" 방식).
            // 사거리를 방보다 작게(3.2 < 5.8×4.4) 잡아야 구석이 어둠에 잠긴다 —
            // 4.6이면 방 전체가 고르게 밝아져 "등잔 하나"로 안 읽힌다 (실측)
            var oil = Lamp(night, "등잔_불빛", LampSpot + new Vector3(0f, 0.55f, 0f), 13f, 3.2f,
                           new Color(1.00f, 0.78f, 0.48f));
            oil.shadows = LightShadows.Soft;
            oil.shadowStrength = 0.85f;
            oil.shadowBias = 0.05f;
            oil.shadowNormalBias = 0.25f;

            // ⚠️ 두 그룹 **각각** 추가 광원 4개다 (URP PC_RPAsset 한도).
            //    낮 = 햇살2 + 창면빛2 / 밤 = 달빛2 + 등잔1 (+1 여유). 더 넣으려면 세어 볼 것.
            var tod = group.GetComponent<OfficeTimeOfDay>();
            if (tod == null) tod = group.AddComponent<OfficeTimeOfDay>();
            tod.dayGroup = day;
            tod.nightGroup = night;
            tod.paperDay = MatHanji;
            tod.paperNight = MatHanjiNight;
            tod.paperRenderers = FindRoot("집무실_창호") is GameObject ch
                ? ch.GetComponentsInChildren<MeshRenderer>(true)
                     .Where(r => r.sharedMaterial == MatHanji || r.sharedMaterial == MatHanjiNight).ToArray()
                : new Renderer[0];
            tod.SetNight(wasNight);

            Debug.Log("[집무실] 조명 — 낮/밤 그룹 각각 방향광 1(메인) + 추가 4/3. 현재 = " +
                      (wasNight ? "밤" : "낮") + " (Tools ▸ 이문록 ▸ 관아 집무실 ▸ 낮/밤 전환)");
        }

        static Light Spot(GameObject parent, string name, Vector3 pos, Vector3 aimDir,
                          float intensity, float range, float angle, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(aimDir.normalized, Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.55f;
            l.color = c;
            l.shadows = LightShadows.None;
            return l;
        }

        static Light Lamp(GameObject parent, string name, Vector3 pos, float intensity, float range, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = intensity;
            l.range = range;
            l.color = c;
            l.shadows = LightShadows.None;
            return l;
        }

        static void Dir(GameObject parent, string name, Vector3 euler, float intensity, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = c;
            l.shadows = LightShadows.None;
        }

        static void Shadowed(Light l, float strength, Texture cookie)
        {
            l.shadows = LightShadows.Soft;
            l.shadowStrength = strength;
            l.shadowBias = 0.03f;
            l.shadowNormalBias = 0.20f;
            l.cookie = cookie;
        }

        // ── 낮/밤 전환 ───────────────────────────────────────
        [MenuItem("Tools/이문록/관아 집무실 ▸ 낮으로")]
        public static void SetDay() => SetTime(false);

        [MenuItem("Tools/이문록/관아 집무실 ▸ 밤으로")]
        public static void SetNight() => SetTime(true);

        static void SetTime(bool night)
        {
            var tod = Object.FindFirstObjectByType<OfficeTimeOfDay>();
            if (tod == null) { Debug.LogError("[집무실] OfficeTimeOfDay가 없습니다 — 씬 조립을 먼저 실행하세요"); return; }
            tod.SetNight(night);
            EditorUtility.SetDirty(tod);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[집무실] " + (night ? "밤" : "낮") + " 상태로 전환");
        }

        /// <summary>
        /// 격자창 쿠키 — 스포트라이트에 물려 바닥에 창살 그림자를 던진다.
        /// 이게 이 방의 인상(레퍼런스 사진의 바닥 빛 웅덩이)을 만드는 핵심이다.
        /// 원형 비네트를 곱해 스포트 원뿔 가장자리가 사각으로 잘리지 않게 한다.
        /// </summary>
        static Texture2D BuildLatticeCookie()
        {
            const string path = TexDir + "/T_집무실_창살쿠키.png";
            const int S = 512;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            // 세로 살 11줄 / 가로 살 5줄 — 실제 격자창(nv≈13, nh≈5)과 결이 맞게
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    // 창틀 안쪽으로 살짝 여백을 준 뒤 살대 격자
                    float pane = 1f;
                    float fu = Mathf.Abs(Mathf.Repeat(u * 11f, 1f) - 0.5f);
                    float fv = Mathf.Abs(Mathf.Repeat(v * 5f, 1f) - 0.5f);
                    if (fu > 0.40f) pane *= Mathf.InverseLerp(0.48f, 0.40f, fu) * 0.75f + 0.15f;
                    if (fv > 0.38f) pane *= Mathf.InverseLerp(0.47f, 0.38f, fv) * 0.75f + 0.15f;
                    // 창틀 테두리 — 바깥 8%는 완전 차단
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    pane *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.13f, edge));
                    // 원형 비네트 (원뿔 가장자리 각짐 방지)
                    float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                    pane *= Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.80f, 1.02f, r));
                    byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(pane) * 255f);
                    px[y * S + x] = new Color32(b, b, b, b);
                }
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.sRGBTexture = false;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ══════════════════════════════════════════════════════
        // ⑥ 마커
        // ══════════════════════════════════════════════════════
        static void BuildMarkers()
        {
            var group = RecreateGroup("집무실_마커");
            Marker(group, "SpawnPoint_FromGwana", SpawnFromGwana, 0f);    // +Z = 방 안쪽을 본다
            Marker(group, "Exit_ToGwana", ExitToGwana, 180f);             // -Z = 마당 쪽
            Marker(group, "Spawn_Suryeong", SpawnSuryeong, 180f);         // 병풍을 등지고 문을 본다
            // 통로 끝 — 빌더가 실제로 놓은 경로의 끝점 (경로를 바꿔도 따라온다)
            Marker(group, "Exit_ToArchive",
                   PassageEnd == Vector3.zero ? ExitToArchive : PassageEnd, PassageEndYaw);
            Debug.Log("[집무실] 마커 4종 — SpawnPoint_FromGwana / Exit_ToGwana / Spawn_Suryeong / Exit_ToArchive");
        }

        // ══════════════════════════════════════════════════════
        // ⚠️ 임시 디버그 장치 — 진짜 퍼즐·열쇠가 붙으면 **이 블록과 BuildAll의 호출 한 줄**을
        //    지우고, 씬의 `디버그_비밀문해제` 오브젝트와 DebugDoorUnlocker.cs를 삭제하면 끝.
        //    LockedDoor는 건드리지 않았으므로 잠금 로직 쪽은 손볼 것이 없다.
        // ══════════════════════════════════════════════════════
        static void BuildDebugUnlocker()
        {
            var go = RecreateGroup("디버그_비밀문해제");
            go.AddComponent<DebugDoorUnlocker>();
        }

        [MenuItem("Tools/이문록/관아 집무실 ▸ 비밀문 임시 해제 (디버그)")]
        public static void DebugUnlockDoor()
        {
            int n = DebugDoorUnlocker.SetLocked(false);
            MarkDoorsDirty();
            Debug.Log("[집무실] 비밀문 " + n + "개 잠금 해제 (임시) — Play 중에는 F1/F2로도 된다");
        }

        [MenuItem("Tools/이문록/관아 집무실 ▸ 비밀문 다시 잠그기")]
        public static void DebugRelockDoor()
        {
            int n = DebugDoorUnlocker.SetLocked(true);
            MarkDoorsDirty();
            Debug.Log("[집무실] 비밀문 " + n + "개 다시 잠금");
        }

        [MenuItem("Tools/이문록/관아 집무실 ▸ 디버그 임시 해제 장치 제거")]
        public static void RemoveDebugUnlocker()
        {
            var go = FindRoot("디버그_비밀문해제");
            if (go == null) { Debug.Log("[집무실] 임시 해제 장치가 이미 없습니다"); return; }
            Object.DestroyImmediate(go);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[집무실] 임시 해제 장치 제거 — BuildAll의 BuildDebugUnlocker() 호출도 지우면 완전히 사라집니다");
        }

        static void MarkDoorsDirty()
        {
            foreach (var d in Object.FindObjectsByType<LockedDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                EditorUtility.SetDirty(d);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void Marker(GameObject group, string name, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(group.transform, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        }

        // ══════════════════════════════════════════════════════
        // 메시 헬퍼
        // ══════════════════════════════════════════════════════
        internal static void Piece(Transform parent, string name, GwanaMeshKit kit, Material mat)
        {
            PieceGO(parent, name, kit, mat);
        }

        internal static GameObject PieceGO(Transform parent, string name, GwanaMeshKit kit, Material mat)
        {
            if (kit.TriCount == 0) return null;
            var mesh = SaveMesh(kit.Build("집무실_" + name));
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic
                | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
            return go;
        }

        /// <summary>메시 에셋은 경로를 재사용해 GUID를 보존한다(멱등).
        /// ⚠️ 반드시 **반환값**을 렌더러에 물릴 것 — 인자로 준 새 메시는 에셋이 아니라 씬에 직렬화된다.</summary>
        internal static Mesh SaveMesh(Mesh mesh)
        {
            string path = MeshDir + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
