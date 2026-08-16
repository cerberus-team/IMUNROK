using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static IMUNROK.Gyeonu.Editor.GwanaLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 구조물 절차 생성 — 외삼문(솟을삼문)·담장·월대·어도.
    ///
    /// 왜 절차 생성인가: 임포트한 4개 팩(낙안읍성·소쇄원·세연정·김명관)을 전수 조사했으나
    /// 통행 가능한 높이(2.2m 이상)의 독립 문·누문이 하나도 없다. 낙안 지붕 메시는 전부 초가다.
    /// 대신 동헌 FBX의 재질(기와·석축·화강암·목재·부연)을 복제해 쓰므로, 문·담장·동헌의
    /// 돌결·기왓골 크기가 정확히 일치한다(UV 스케일 실측 반영 — GwanaMeshKit 참조).
    ///
    /// 외삼문 규격 (조선 관아 정문 = 삼문 형식, 가운데가 솟은 솟을삼문):
    ///   전체 폭 15.9m(처마) / 깊이 5.9m(처마) / 중앙 용마루 6.36m
    ///   중앙간 통로 3.9m × 3.1m — 양옆 협문보다 크고 지붕이 한 단 솟는다
    ///   중앙 문짝은 통로 옆으로 활짝 열어 붙였고(보행 방해 없음), 협문 2쌍은 닫힘
    ///
    /// 멱등 — 메시 에셋은 경로 재사용(GUID 보존), 씬 그룹은 지우고 재생성.
    /// </summary>
    public static class GwanaStructures
    {
        const string MeshDir = "Assets/_Project/Gyeonu/Art/Models/Gwana";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials";

        // 담장 단면
        const float WBaseHalf = 0.52f, WTopHalf = 0.44f;   // 석축 물매
        const float WStoneTop = 2.35f;
        const float WCapTop = 2.50f, WCapHalf = 0.56f;
        const float WRidgeY = 2.92f, WRidgeCapHalf = 0.15f;
        // 담장 기와 물매 — (용마루에서의 반깊이, 높이, 판 두께)
        static readonly Vector3[] WallRoofProfile =
        {
            new Vector3(0.00f, 2.92f, 0.15f),
            new Vector3(0.34f, 2.76f, 0.13f),
            new Vector3(0.62f, 2.65f, 0.11f),
            new Vector3(0.72f, 2.67f, 0.09f),   // 처마 들림
        };

        static Material MatGiwa, MatSeokchuk, MatHwagang, MatMokjae, MatBuyeon, MatMunmok, MatHoebyeok, MatBakseok;

        // ── 진입점 ────────────────────────────────────────────
        public static void BuildInto(Transform parent)
        {
            LoadMaterials();
            _wallCache.Clear();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(MeshDir));

            var root = new GameObject("관아_구조물");
            root.transform.SetParent(parent, false);

            BuildWalls(root.transform);
            BuildGate(root.transform);
            BuildDae(root.transform);
            BuildAxisPaving(root.transform);

            int tris = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) tris += (int)(mf.sharedMesh.GetIndexCount(0) / 3);
            foreach (var t in root.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic
                    | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);

            AssetDatabase.SaveAssets();
            Debug.Log($"[관아] 구조물 생성 — 삼각형 {tris:N0} (외삼문·담장·월대·어도)");
        }

        static void LoadMaterials()
        {
            MatGiwa = DonheonPrep.Mat("관아_기와");
            MatSeokchuk = DonheonPrep.Mat("관아_석축");
            MatHwagang = DonheonPrep.Mat("관아_화강암");
            MatMokjae = DonheonPrep.Mat("관아_목재");
            MatBuyeon = DonheonPrep.Mat("관아_부연");
            MatMunmok = DonheonPrep.Mat("관아_문목재");
            MatHoebyeok = EnsureHoebyeok();
            MatBakseok = EnsureBakseok();
            if (MatGiwa == null || MatSeokchuk == null)
                Debug.LogError("[관아] 동헌 복제 머티리얼이 없습니다 — 「① 동헌 에셋 준비」를 먼저 실행하세요.");
        }

        /// <summary>회벽(백토 미장) — 팩에 해당 텍스처가 없어 무텍스처 단색으로 만든다.</summary>
        static Material EnsureHoebyeok()
        {
            string p = MatDir + "/관아_회벽.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, p);
            }
            m.SetColor("_BaseColor", new Color(0.80f, 0.77f, 0.70f));
            m.SetFloat("_Smoothness", 0.06f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>어도 박석 — 새 화강암 그대로면 마당에서 흰 리본으로 튄다. 다져진 톤으로 낮춘 사본.</summary>
        static Material EnsureBakseok()
        {
            string p = MatDir + "/관아_박석.mat";
            var src = DonheonPrep.Mat("관아_화강암");
            if (src == null) return null;
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(src.shader); AssetDatabase.CreateAsset(m, p); }
            m.shader = src.shader;
            m.CopyPropertiesFromMaterial(src);
            m.SetColor("_BaseColor", new Color(0.70f, 0.68f, 0.64f));
            EditorUtility.SetDirty(m);
            return m;
        }

        // ── 담장 ──────────────────────────────────────────────
        static readonly Dictionary<float, Mesh[]> _wallCache = new Dictionary<float, Mesh[]>();

        static void BuildWalls(Transform parent)
        {
            var group = new GameObject("담장");
            group.transform.SetParent(parent, false);

            float ov = 0.56f;   // 모서리 겹침 — 겹친 단면이 실제 모서리처럼 읽힌다
            // 남담: 외삼문 좌우
            WallRun(group.transform, "남담_서", new Vector3(-WallHalfX - ov, 0f, WallZS), new Vector3(-GateHalfW, 0f, WallZS));
            WallRun(group.transform, "남담_동", new Vector3(GateHalfW, 0f, WallZS), new Vector3(WallHalfX + ov, 0f, WallZS));
            // 좌우담 (모서리까지 연장)
            WallRun(group.transform, "서담", new Vector3(-WallHalfX, 0f, WallZS - ov), new Vector3(-WallHalfX, 0f, WallZN + ov));
            WallRun(group.transform, "동담", new Vector3(WallHalfX, 0f, WallZS - ov), new Vector3(WallHalfX, 0f, WallZN + ov));
            // 북담
            WallRun(group.transform, "북담", new Vector3(-WallHalfX - ov, 0f, WallZN), new Vector3(WallHalfX + ov, 0f, WallZN));
        }

        // 한 변 = 메시 한 장, y는 마당 바닥(0) 고정. 2026-08-17에 단 분절 + 높이·좌우·요 지터를
        // 넣어 봤으나 되돌렸다 — 관아 담장은 민가 돌담이 아니라 정돈된 관청 시설이라
        // 고른 수평선이 맞다(사용자 확정). 지형에 미세 기복이 있어도 담장은 따라가지 않는다.
        static void WallRun(Transform parent, string name, Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(a, b);
            float key = Mathf.Round(len * 100f) / 100f;
            if (!_wallCache.TryGetValue(key, out var meshes))
            {
                meshes = BuildWallMeshes(key);
                _wallCache[key] = meshes;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(a.x, CourtY, a.z);
            // 로컬 +X가 진행 방향이 되도록: right = Cross(up, forward) 이므로 forward = Cross(dir, up)
            go.transform.rotation = Quaternion.LookRotation(Vector3.Cross((b - a).normalized, Vector3.up), Vector3.up);
            Piece(go.transform, "석축", meshes[0], MatSeokchuk);
            Piece(go.transform, "갑석", meshes[1], MatHwagang);
            Piece(go.transform, "기와", meshes[2], MatGiwa);
        }

        /// <summary>담장 1주(走) 메시 3종. 로컬 +X 방향으로 length만큼 뻗고, 단면은 ZY 평면.</summary>
        static Mesh[] BuildWallMeshes(float length)
        {
            var stone = new GwanaMeshKit(GwanaMeshKit.MpuRubble);
            var cap = new GwanaMeshKit(GwanaMeshKit.MpuGranite);
            var tile = new GwanaMeshKit(GwanaMeshKit.MpuRoof);

            stone.Tapered(0f, length, -1.4f, WStoneTop, 0f, WBaseHalf, WTopHalf);
            cap.BoxMinMax(0f, length, WStoneTop, WCapTop, -WCapHalf, WCapHalf);
            tile.GableRoofProfile(0f, length, 0f, WallRoofProfile);
            tile.BoxMinMax(0f, length, WRidgeY, WRidgeY + 0.16f, -WRidgeCapHalf, WRidgeCapHalf);

            return new[]
            {
                SaveMesh(stone.Build("담장_석축_" + length), "담장_석축_" + length),
                SaveMesh(cap.Build("담장_갑석_" + length), "담장_갑석_" + length),
                SaveMesh(tile.Build("담장_기와_" + length), "담장_기와_" + length),
            };
        }

        // ── 외삼문 (솟을삼문) ─────────────────────────────────
        // 로컬 원점 = 문 중심, 지면 y=0, 통과 방향 = ±Z
        const float GColOutX = 6.6f, GColInX = 2.2f, GColZ = 1.6f;
        const float GColR = 0.26f, GColRTop = 0.22f;
        const float GPlinthTop = 0.73f;                 // 초석 윗면 = 기둥 밑동
        const float GSideColTop = 3.45f, GMidColTop = 4.85f;
        const float GSideRidge = 5.05f, GMidRidge = 6.55f;
        const float GEaveHalfZ = 2.95f;
        // 지붕 물매 — 마루 쪽 34°, 처마로 갈수록 눕다가 끝에서 살짝 들린다
        static readonly Vector3[] SideRoofProfile =
        {
            new Vector3(0.00f, 5.05f, 0.26f),
            new Vector3(1.20f, 4.24f, 0.22f),
            new Vector3(2.30f, 3.72f, 0.17f),
            new Vector3(2.95f, 3.66f, 0.12f),
        };
        static readonly Vector3[] MidRoofProfile =
        {
            new Vector3(0.00f, 6.55f, 0.26f),
            new Vector3(1.20f, 5.74f, 0.22f),
            new Vector3(2.30f, 5.22f, 0.17f),
            new Vector3(2.95f, 5.16f, 0.12f),
        };
        const float GSideEave = 3.66f, GMidEave = 5.16f;

        static void BuildGate(Transform parent)
        {
            var stone = new GwanaMeshKit(GwanaMeshKit.MpuGranite);
            var rubble = new GwanaMeshKit(GwanaMeshKit.MpuRubble);
            var wood = new GwanaMeshKit(GwanaMeshKit.MpuWood);
            var door = new GwanaMeshKit(GwanaMeshKit.MpuWood);
            var tile = new GwanaMeshKit(GwanaMeshKit.MpuRoof);
            var buyeon = new GwanaMeshKit(GwanaMeshKit.MpuBuyeon);
            var plaster = new GwanaMeshKit(1.0f);

            // ① 기단 — 잡석 몸체 + 화강암 갑석
            rubble.BoxMinMax(-GateHalfW, GateHalfW, -1.30f, GateBaseTop - 0.12f, -2.50f, 2.50f);
            stone.BoxMinMax(-GateHalfW - 0.10f, GateHalfW + 0.10f, GateBaseTop - 0.12f, GateBaseTop, -2.60f, 2.60f);

            // ② 계단 3단 (앞·뒤, 중앙간 폭)
            for (int i = 0; i < 3; i++)
            {
                float y = GateBaseTop * (i + 1) / 3f;
                float d = 0.45f * (3 - i);
                stone.BoxMinMax(-2.4f, 2.4f, -0.35f, y, -2.60f - d, -2.60f);
                stone.BoxMinMax(-2.4f, 2.4f, -0.35f, y, 2.60f, 2.60f + d);
            }

            // ③ 초석 + 기둥 (민흘림 원기둥)
            foreach (float cx in new[] { -GColOutX, -GColInX, GColInX, GColOutX })
                foreach (float cz in new[] { -GColZ, GColZ })
                {
                    stone.Cylinder(new Vector3(cx, GateBaseTop, cz), 0.38f, 0.34f, GPlinthTop - GateBaseTop, 12);
                    float top = Mathf.Abs(cx) < 4f ? GMidColTop : GSideColTop;
                    wood.Cylinder(new Vector3(cx, GPlinthTop, cz), GColR, GColRTop, top - GPlinthTop, 14);
                }

            // ④ 창방·평방 (기둥머리 연결) — 좌우 각 간, 앞뒤 2열 + 측면 보
            foreach (float cz in new[] { -GColZ, GColZ })
            {
                Beam(wood, -GColOutX - 0.26f, -GColInX + 0.26f, GSideColTop - 0.30f, GSideColTop, cz, 0.22f);
                Beam(wood, GColInX - 0.26f, GColOutX + 0.26f, GSideColTop - 0.30f, GSideColTop, cz, 0.22f);
                Beam(wood, -GColInX - 0.26f, GColInX + 0.26f, GMidColTop - 0.30f, GMidColTop, cz, 0.26f);
            }
            foreach (float cx in new[] { -GColOutX, -GColInX, GColInX, GColOutX })
            {
                float top = Mathf.Abs(cx) < 4f ? GMidColTop : GSideColTop;
                wood.BoxMinMax(cx - 0.11f, cx + 0.11f, top - 0.30f, top,
                               -GColZ - 0.26f, GColZ + 0.26f);
                // 주두 + 보아지 (기둥머리 받침) — 없으면 각재 뼈대처럼 밋밋하게 읽힌다
                foreach (float cz in new[] { -GColZ, GColZ })
                {
                    wood.BoxMinMax(cx - 0.29f, cx + 0.29f, top - 0.48f, top - 0.30f, cz - 0.29f, cz + 0.29f);
                    wood.BoxMinMax(cx - 0.62f, cx + 0.62f, top - 0.30f, top - 0.14f, cz - 0.17f, cz + 0.17f);
                    wood.BoxMinMax(cx - 0.17f, cx + 0.17f, top - 0.30f, top - 0.14f, cz - 0.62f, cz + 0.62f);
                }
            }

            // ⑤ 상인방 + 그 위 회벽
            Beam(wood, -GColOutX, -GColInX, 2.95f, 3.22f, 0f, 0.34f);
            Beam(wood, GColInX, GColOutX, 2.95f, 3.22f, 0f, 0.34f);
            Beam(wood, -GColInX - 0.12f, GColInX + 0.12f, 3.55f, 3.85f, 0f, 0.38f);
            plaster.BoxMinMax(-GColOutX, -GColInX, 3.22f, GSideColTop - 0.30f, -0.14f, 0.14f);
            plaster.BoxMinMax(GColInX, GColOutX, 3.22f, GSideColTop - 0.30f, -0.14f, 0.14f);
            plaster.BoxMinMax(-GColInX, GColInX, 3.85f, GMidColTop - 0.30f, -0.16f, 0.16f);
            // 솟을 부분 옆면 (중앙간이 측간보다 솟은 만큼 생기는 벽)
            foreach (float s in new[] { -1f, 1f })
                plaster.BoxMinMax(s * (GColInX - 0.14f), s * (GColInX + 0.14f),
                                  GSideColTop, GMidRidge - 0.65f, -1.1f, 1.1f);

            // ⑥ 문짝 — 널판에 띠장을 두른 판문(板門). 중앙 2짝은 통로 옆으로 활짝 열렸고 협문 4짝은 닫힘.
            //    널판은 마루널(밝은 결), 띠장은 문목재(짙은 색) — 단색 판때기로 읽히지 않게 대비를 준다
            foreach (float s in new[] { -1f, 1f })
            {
                // 열린 중앙 문짝: 안쪽 기둥열(z=+1.6)에 경첩, 통로 벽에 붙어 있다
                wood.BoxMinMax(s * 1.88f - 0.05f, s * 1.88f + 0.05f, GateBaseTop, 3.50f, -0.36f, 1.60f);
                foreach (float by in new[] { 0.66f, 1.98f, 3.30f })
                    door.BoxMinMax(s * 1.88f - 0.085f, s * 1.88f + 0.085f, by - 0.09f, by + 0.09f, -0.36f, 1.60f);
                // 닫힌 협문 2짝
                foreach (float t in new[] { -1f, 1f })
                {
                    float x0 = s * 4.40f, x1 = s * 4.40f + t * 1.96f;
                    wood.BoxMinMax(x0, x1, GateBaseTop, 2.95f, -0.05f, 0.05f);
                    foreach (float by in new[] { 0.66f, 1.70f, 2.74f })
                        door.BoxMinMax(x0, x1, by - 0.09f, by + 0.09f, -0.085f, 0.085f);
                    // 문설주 쪽 세로 울거미
                    door.BoxMinMax(x1 - t * 0.16f, x1, GateBaseTop, 2.95f, -0.085f, 0.085f);
                }
                // 문선(문틀 세로)
                wood.BoxMinMax(s * 2.44f - 0.09f, s * 2.44f + 0.09f, GateBaseTop, 3.55f, -0.14f, 0.14f);
                wood.BoxMinMax(s * 6.36f - 0.09f, s * 6.36f + 0.09f, GateBaseTop, 2.95f, -0.14f, 0.14f);
            }

            // ⑦ 지붕 — 측간 맞배 2채 + 중앙 솟을 맞배 1채 (중앙이 1.5m 솟는다)
            tile.GableRoofProfile(-7.95f, -2.10f, 0f, SideRoofProfile, 1);
            tile.GableRoofProfile(2.10f, 7.95f, 0f, SideRoofProfile, 1);
            tile.GableRoofProfile(-3.40f, 3.40f, 0f, MidRoofProfile, 1);
            // 밑면은 기와가 아니라 개판(널) — 문 아래를 지날 때 천장이 기왓장으로 보이면 안 된다
            wood.GableRoofProfile(-7.95f, -2.10f, 0f, SideRoofProfile, 2);
            wood.GableRoofProfile(2.10f, 7.95f, 0f, SideRoofProfile, 2);
            wood.GableRoofProfile(-3.40f, 3.40f, 0f, MidRoofProfile, 2);
            tile.BoxMinMax(-7.95f, -2.10f, GSideRidge, GSideRidge + 0.21f, -0.28f, 0.28f);
            tile.BoxMinMax(2.10f, 7.95f, GSideRidge, GSideRidge + 0.21f, -0.28f, 0.28f);
            tile.BoxMinMax(-3.40f, 3.40f, GMidRidge, GMidRidge + 0.23f, -0.30f, 0.30f);

            // ⑧ 박공널(풍판) + 서까래·부연
            wood.GablePanel(-7.95f, 0f, GEaveHalfZ, GSideEave - 0.06f, GSideRidge, 0.16f);
            wood.GablePanel(7.95f, 0f, GEaveHalfZ, GSideEave - 0.06f, GSideRidge, 0.16f);
            wood.GablePanel(-3.40f, 0f, GEaveHalfZ, GMidEave - 0.06f, GMidRidge, 0.16f);
            wood.GablePanel(3.40f, 0f, GEaveHalfZ, GMidEave - 0.06f, GMidRidge, 0.16f);
            buyeon.Rafters(-7.95f, -2.10f, 0f, SideRoofProfile);
            buyeon.Rafters(2.10f, 7.95f, 0f, SideRoofProfile);
            buyeon.Rafters(-3.40f, 3.40f, 0f, MidRoofProfile);

            var go = new GameObject("외삼문");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, CourtY, GateZ);
            Piece(go.transform, "기단_잡석", SaveMesh(rubble.Build("문_잡석"), "문_잡석"), MatSeokchuk);
            Piece(go.transform, "기단_화강암", SaveMesh(stone.Build("문_화강암"), "문_화강암"), MatHwagang);
            Piece(go.transform, "목부재", SaveMesh(wood.Build("문_목재"), "문_목재"), MatMokjae);
            Piece(go.transform, "문짝", SaveMesh(door.Build("문_문짝"), "문_문짝"), MatMunmok);
            Piece(go.transform, "지붕", SaveMesh(tile.Build("문_기와"), "문_기와"), MatGiwa);
            Piece(go.transform, "부연", SaveMesh(buyeon.Build("문_부연"), "문_부연"), MatBuyeon);
            Piece(go.transform, "회벽", SaveMesh(plaster.Build("문_회벽"), "문_회벽"), MatHoebyeok);
        }

        static void Beam(GwanaMeshKit k, float x0, float x1, float y0, float y1, float zc, float halfZ) =>
            k.BoxMinMax(x0, x1, y0, y1, zc - halfZ, zc + halfZ);

        // ── 월대 (동헌 기단) ──────────────────────────────────
        static void BuildDae(Transform parent)
        {
            var rubble = new GwanaMeshKit(GwanaMeshKit.MpuRubble);
            var stone = new GwanaMeshKit(GwanaMeshKit.MpuGranite);

            // 4단 석축 (고증: 동헌은 여러 단 쌓은 석축 위에 선다) — 위로 갈수록 들여쌓기
            int tiers = (int)DaeTiers;
            for (int i = 0; i < tiers; i++)
            {
                float inset = i * 0.26f;
                rubble.BoxMinMax(-DaeHalfX - 0.55f + inset, DaeHalfX + 0.55f - inset,
                                 i * DaeTierH - (i == 0 ? 1.4f : 0f), (i + 1) * DaeTierH,
                                 DaeZ0 - 0.55f + inset, DaeZ1 + 0.55f - inset);
            }
            // 갑석
            stone.BoxMinMax(-DaeHalfX - 0.15f, DaeHalfX + 0.15f, DaeTiers * DaeTierH, DaeTop,
                            DaeZ0 - 0.15f, DaeZ1 + 0.15f);

            // 계단 12단 (마당 0 → 월대 1.80). 한 단 0.15 × 0.30
            const int steps = 12;
            for (int i = 0; i < steps; i++)
            {
                float y = DaeTop * (i + 1) / steps;
                float z = DaeStairZ0 + (DaeZ0 - DaeStairZ0) * i / steps;
                stone.BoxMinMax(-DaeStairHalfX, DaeStairHalfX, -0.4f, y, z, DaeZ0 + 0.2f);
                // 소맷돌 (계단 옆 난간돌)
                foreach (float s in new[] { -1f, 1f })
                    rubble.BoxMinMax(s * DaeStairHalfX, s * (DaeStairHalfX + 0.45f),
                                     -0.4f, y + 0.34f, z, DaeZ0 + 0.2f);
            }

            var go = new GameObject("월대");
            go.transform.SetParent(parent, false);
            Piece(go.transform, "석축", SaveMesh(rubble.Build("월대_석축"), "월대_석축"), MatSeokchuk);
            Piece(go.transform, "화강암", SaveMesh(stone.Build("월대_화강암"), "월대_화강암"), MatHwagang);
        }

        // ── 어도 (박석 진입축) ────────────────────────────────
        static void BuildAxisPaving(Transform parent)
        {
            var k = new GwanaMeshKit(GwanaMeshKit.MpuGranite);
            // 담장 안쪽만 깐다. 문 밖은 흙길 — 진입로 지면이 오르막이라 평평한 판을 깔면
            // 아래쪽 모서리가 뜬다(어도는 원래 관아 안쪽 축이라 고증과도 맞다).
            k.BoxMinMax(-AxisHalfW, AxisHalfW, -0.12f, 0.045f, AxisZ0, AxisZ1);

            var go = new GameObject("어도");
            go.transform.SetParent(parent, false);
            Piece(go.transform, "박석", SaveMesh(k.Build("어도_박석"), "어도_박석"), MatBakseok);
        }

        // ── 공통 ──────────────────────────────────────────────
        static void Piece(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }

        static Mesh SaveMesh(Mesh m, string fileName)
        {
            string path = MeshDir + "/" + fileName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
