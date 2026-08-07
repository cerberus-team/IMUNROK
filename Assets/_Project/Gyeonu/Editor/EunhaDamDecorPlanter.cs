using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 은하담 구조물·식생 배치기 (고정 시드 = 재실행 시 동일 결과, 멱등).
    ///
    /// 구조물(2026-08-07 확정): 정자는 풍영정 하나 — 세연정·광풍각처럼 못 위 대(臺,
    ///         수면 아래 0.15 평탄부)에 세우고 StoneBridge_1로 서안과 잇는다.
    ///         정자에서 남쪽으로 오작교가 보인다. 풍영정 FBX는 텍스처 미연결이라
    ///         URP Lit 머티리얼(Art/Materials/풍영정_*)을 씬 인스턴스에만 입힌다 (원본 무수정).
    ///
    /// 식생(2026-08-07 재구성): 버드나무 Salix_VR(물가), 갈대 Phragmites_VR,
    ///         수련 Nymphaea(수면 0.5 부유), 벚나무JG 여름잎 중경 주력 + 소나무VR 근경 소수,
    ///         사시나무 원경(확장 지형 포함), 대나무 경계·틈새.
    ///         흑송JG는 전정된 일본 정원수 형태라 전량 제거 (2026-08-07).
    ///         +X 마을 길목~확장 지형에 숲 연출 — 길(|z|&lt;11)은 항상 트여 있다.
    ///         기존 블록은 GroundHeight(해석 함수), 새 블록은 SampleH(실제 Terrain, 스커트 포함).
    /// </summary>
    public static class EunhaDamDecorPlanter
    {
        const int Seed = 20260806;
        const string VegPath = "Assets/_Project/Gyeonu/Prefabs/Vegetation/";
        const string SeyeonPath = "Assets/_Project/Gyeonu/Prefabs/Seyeonjeong/";
        const string MatFolder = "Assets/_Project/Gyeonu/Art/Materials";

        const float WaterLevel = 0.5f;

        // 랜드마크 버드나무 — 씬 진입 지점(구 지형 +X 끝, x≈100)에서 서쪽 문루를 볼 때
        // 가지가 프레임이 되는 위치. 2026-08-07 후보 C 임시 적용 (A 96,6 / B 95,-6.5 / D 89,-10·rot320).
        // 사용자 확정 시 이 세 값만 갱신하고 재배치.
        const float LandmarkX = 91f;
        const float LandmarkZ = 9f;
        const float LandmarkRotY = 200f;

        // 회피 구역 (xMin, xMax, zMin, zMax)
        static readonly float[][] AvoidRects =
        {
            new[] { -60f, 60f, -16f, 16f },     // 다리 전체 (문루~문루)
            new[] { 36f, 100f, -15f, 15f },     // 입구 통로 (둑 위 진입로)
        };

        [MenuItem("Tools/이문록/은하담 구조물 배치")]
        public static void PlaceStructures()
        {
            var group = RecreateGroup("은하담_구조물");
            var rnd = new System.Random(Seed);

            // ── 풍영정 (확정): 호수 안쪽 대(臺) 위 — 사방이 물, 계단(정면 local +Z)은
            //    서쪽 물가(bearing 260)를 향하고 남동 개방면으로 오작교가 보인다 ──
            float px = EunhaDamBuilder.PondPadX, pz = EunhaDamBuilder.PondPadZ;
            // rotY 90 (2026-08-07 재배치): 계단(local +Z)이 동쪽 — 마을 방향 돌다리로 진입.
            // 피벗이 평면 중심이라 회전해도 발자국은 그대로.
            var pyj = PlacePrefab(group,
                "Assets/woljeonggyo/pungyeongjeong-pavilion/source/01_PoongYoungJung.fbx",
                "정자_풍영정", new Vector3(px, 0.15f, pz), 90f, 1f, snapBaseY: true);
            if (pyj != null) ApplyPungyeongMaterials(pyj);

            // ── 돌다리 (2026-08-07 재배치): 동안(마을 길목 쪽) 물가 x≈29 → 정자 계단부 x≈-5.4,
            //    z 39.6 일직선 9연결. 피벗 y=0 → 보행면 1.10 (수면 +0.6, 정자 마루 1.15와 5cm 단차).
            //    길이 3.95(스케일 1.3) / 간격 3.80 → 15cm 겹침, 틈 없음. MeshCollider 내장 확인됨.
            float[] bridgeJitter = { 2f, -3f, 4f, -2f, 3f, -4f, 2f, -3f, 3f, -2f };
            const float BridgeZ = 39.6f, BridgeSpacing = 3.8025f, BridgeX0 = 27.0f;
            for (int i = 0; i < 10; i++)   // 10연결 — 서단이 정자 동면(-9.05)까지 닿는다
                PlacePrefab(group, SeyeonPath + "SM_StoneBridge_1.prefab",
                    "돌다리_정자연결_" + (i + 1),
                    new Vector3(BridgeX0 - i * BridgeSpacing, 0f, BridgeZ), bridgeJitter[i], 1.3f);
            // 이음매 아래 받침 바위 (수면 근처까지 — 물속 부유감 방지)
            for (int i = 0; i < 9; i++)
                PlacePrefab(group, SeyeonPath + (i % 2 == 0 ? "SM_Rock_K_VR.prefab" : "SM_Rock_G_VR.prefab"),
                    "돌다리_받침_" + (i + 1),
                    new Vector3(BridgeX0 - (i + 0.5f) * BridgeSpacing, -0.55f, BridgeZ + (i % 2 == 0 ? 0.3f : -0.3f)),
                    i * 47f, 0.8f);
            // 물가 진입 디딤돌 (둑 0.5~0.6 → 상판 1.10 중간 단)
            var entry = PlacePrefab(group, SeyeonPath + "SM_Rock_K_VR.prefab",
                "돌다리_진입석", new Vector3(29.9f, 0.45f, BridgeZ), 25f, 1f);
            if (entry != null) entry.transform.localScale = new Vector3(1.0f, 0.35f, 1.0f);

            // ── 풍영정 보행 콜라이더 (FBX에 콜라이더 없음 — 마루 + 다리 이음) ──
            var walk = new GameObject("풍영정_보행콜라이더");
            walk.transform.SetParent(group.transform, false);
            var deckCol = walk.AddComponent<BoxCollider>();
            deckCol.center = new Vector3(px, 1.095f, pz);          // 마루 상면 1.15
            deckCol.size = new Vector3(7.3f, 0.12f, 12.3f);
            var seamCol = walk.AddComponent<BoxCollider>();
            seamCol.center = new Vector3(-9.0f, 1.1f, BridgeZ);    // 다리 서단(-9.2)~마루 동단(-9.35) 이음
            seamCol.size = new Vector3(0.9f, 0.08f, 2.0f);
            // 다리 전장 보행판 — 판석(1.03~1.07) 사이 저단 구간(0.5m 꺼짐)을 덮는 평평한 투명 콜라이더.
            // 판석 콜라이더(컨벡스 헐)가 끝단에서 꺼져 걸음이 튀는 것도 함께 해결.
            var walkwayCol = walk.AddComponent<BoxCollider>();
            walkwayCol.center = new Vector3(9.9f, 1.02f, BridgeZ);    // 상면 1.08, x -9.0~28.8
            walkwayCol.size = new Vector3(37.8f, 0.12f, 1.7f);
            // 물가 진입 램프 (둑 0.54 → 보행판 1.08)
            var ramp = new GameObject("풍영정_진입램프");
            ramp.transform.SetParent(group.transform, false);
            ramp.transform.position = new Vector3(29.7f, 0.8f, BridgeZ);
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, -17f);   // +X로 갈수록 내려감
            var rampCol = ramp.AddComponent<BoxCollider>();
            rampCol.size = new Vector3(2.1f, 0.08f, 1.7f);
            // 동안 물가 징검다리
            PlacePrefab(group, SeyeonPath + "SM_StoneBridge_2.prefab",
                "돌다리_물가", new Vector3(31.8f, 0.18f, -24f), 75f, 1f);

            // ── 물가 바위 ──
            string[] rockNames = { "SM_Rock_A_VR", "SM_Rock_C_VR", "SM_Rock_E_VR",
                                   "SM_Rock_G_VR", "SM_Rock_K_VR", "SM_Rock_L_VR" };
            var rocks = rockNames.Select(n =>
                AssetDatabase.LoadAssetAtPath<GameObject>(SeyeonPath + n + ".prefab")).ToArray();
            if (rocks.Any(r => r == null)) { Debug.LogError("[은하담] 세연정 바위 프리팹 누락"); return; }

            int placedShore = 0, placedWater = 0, attempts = 0;
            var points = new List<Vector2>();
            while (placedShore < 16 && attempts++ < 800)
            {
                float z = Mathf.Lerp(-85f, 85f, (float)rnd.NextDouble());
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float x = side * (EunhaDamBuilder.ChannelHalfWidth(z) + Mathf.Lerp(-2f, 4.5f, (float)rnd.NextDouble()));
                float h = EunhaDamBuilder.GroundHeight(x, z);
                if (h < 0.0f || h > 1.0f) continue;
                if (NearPavilion(x, z, 4f)) continue;
                if (Blocked(x, z, points, 5f)) continue;

                int idx = rnd.Next(rocks.Length);
                float s = Mathf.Lerp(0.6f, 1.1f, (float)rnd.NextDouble());
                if (idx == 0) s *= 0.55f;   // Rock_A는 9.6m급 — 축소
                var go = (GameObject)PrefabUtility.InstantiatePrefab(rocks[idx], group.transform);
                go.name = "바위_물가_" + placedShore;
                go.transform.position = new Vector3(x, h - 0.12f, z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * s;
                points.Add(new Vector2(x, z));
                placedShore++;
            }
            attempts = 0;
            while (placedWater < 5 && attempts++ < 400)
            {
                float z = Mathf.Lerp(-70f, 70f, (float)rnd.NextDouble());
                if (Mathf.Abs(z) < 16f) continue;
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float x = side * (EunhaDamBuilder.ChannelHalfWidth(z) - Mathf.Lerp(2f, 8f, (float)rnd.NextDouble()));
                float h = EunhaDamBuilder.GroundHeight(x, z);
                if (h > 0.0f) continue;
                if (NearPavilion(x, z, 6f)) continue;
                if (Blocked(x, z, points, 8f)) continue;

                int idx = rnd.Next(rocks.Length);
                if (idx == 5) idx = 2;      // 물속엔 작은 L 대신 중형
                float s = Mathf.Lerp(0.7f, 1.0f, (float)rnd.NextDouble());
                if (idx == 0) s *= 0.55f;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(rocks[idx], group.transform);
                go.name = "바위_물속_" + placedWater;
                go.transform.position = new Vector3(x, h - 0.15f, z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * s;
                points.Add(new Vector2(x, z));
                placedWater++;
            }

            ApplyStaticFlags(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[은하담] 구조물 배치 완료: 풍영정 1, 돌다리 2, 바위 물가 {placedShore} + 물속 {placedWater}");
        }

        [MenuItem("Tools/이문록/은하담 식생 배치")]
        public static void PlantVegetation()
        {
            var root = FindRoot("은하담_식생") ?? new GameObject("은하담_식생");
            foreach (var n in new[] { "버드나무", "갈대", "수련", "외곽숲" })
            {
                var old = root.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var willowG = NewChild(root, "버드나무");
            var reedG = NewChild(root, "갈대");
            var lilyG = NewChild(root, "수련");
            var forestG = NewChild(root, "외곽숲");

            var rnd = new System.Random(Seed + 1);
            var planted = new List<Vector2>();

            // ── 버드나무 (은하담 상징 — 물가선 h 0.6~0.9 띠, x는 강폭에서 계산) ──
            var willows = new[]
            {
                LoadVeg("Willow_버드나무_Salix_1_VR"),
                LoadVeg("Willow_버드나무_Salix_2_VR"),
            };
            float[][] willowAnchors =   // { z, 강가 쪽(-1 서안 / +1 동안) }
            {
                new[] { 55f, -1f }, new[] { 22f, -1f }, new[] { -36f, -1f }, new[] { -62f, -1f },
                new[] { 22f, 1f }, new[] { 48f, 1f }, new[] { -38f, 1f }, new[] { -65f, 1f },
                new[] { 70f, -1f },
            };
            int w = 0;
            foreach (var a in willowAnchors)
            {
                float z = a[0] + Jitter(rnd, 1.5f);
                float x = a[1] * (EunhaDamBuilder.ChannelHalfWidth(z) + 5.5f) + Jitter(rnd, 1f);
                if (NearPavilion(x, z, 6f)) continue;
                float h = EunhaDamBuilder.GroundHeight(x, z);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(willows[w % 2], willowG);
                go.name = "버드나무_" + w;
                go.transform.position = new Vector3(x, h - 0.08f, z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.2f, (float)rnd.NextDouble());
                planted.Add(new Vector2(x, z));
                w++;
            }

            // ── 갈대 (물가선 h 0.05~0.6 띠) ──
            var reeds = new[] { LoadVeg("Reed_갈대_Phragmites_1_VR"), LoadVeg("Reed_갈대_Phragmites_2_VR") };
            int reedCount = 0, attempts = 0;
            while (reedCount < 60 && attempts++ < 3000)
            {
                float z = Mathf.Lerp(-88f, 88f, (float)rnd.NextDouble());
                if (Mathf.Abs(z) < 16f) continue;   // 다리 아래 제외
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float x = side * (EunhaDamBuilder.ChannelHalfWidth(z) + Mathf.Lerp(2f, 6f, (float)rnd.NextDouble()));
                float h = EunhaDamBuilder.GroundHeight(x, z);
                if (h < 0.05f || h > 0.65f) continue;
                if (NearPavilion(x, z, 8f)) continue;
                if (Blocked(x, z, planted, 1.6f)) continue;
                PlaceClump(reedG, reeds[rnd.Next(2)], "갈대_" + reedCount,
                    new Vector3(x, h - 0.05f, z), rnd, 0.8f, 1.3f);
                planted.Add(new Vector2(x, z));
                reedCount++;
            }

            // ── 수련 (수면 y 0.5, 못 가장자리 군집 — 풍영정 둘레 포함) ──
            var lilyBlooms = new List<GameObject>();
            var lilyLeaves = new List<GameObject>();
            for (int i = 1; i <= 4; i++)
            {
                var bloom = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SeyeonjeongPavilion/Prefabs/SM_NymphaeaTetragona_" + i + ".prefab");
                if (bloom != null) lilyBlooms.Add(bloom);
                var leaf = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SeyeonjeongPavilion/Prefabs/SM_NymphaeaTetragona_Leaf_" + i + ".prefab");
                if (leaf != null) lilyLeaves.Add(leaf);
            }
            if (lilyLeaves.Count == 0) lilyLeaves = lilyBlooms;   // Leaf 프리팹이 없으면 꽃으로
            float[][] lilyClusters =    // { z, 쪽 }
            {
                new[] { 28f, -1f }, new[] { 34f, 1f }, new[] { -32f, 1f },
                new[] { -42f, -1f }, new[] { 56f, 1f }, new[] { -58f, -1f },
            };
            int lilyCount = 0;
            void LilyCluster(float cx, float cz, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = cx + Jitter(rnd, 2.5f), z = cz + Jitter(rnd, 2.5f);
                    if (EunhaDamBuilder.GroundHeight(x, z) > 0.35f) continue;   // 물속만
                    var pool = rnd.NextDouble() < 0.35 && lilyBlooms.Count > 0 ? lilyBlooms : lilyLeaves;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pool[rnd.Next(pool.Count)], lilyG);
                    go.name = "수련_" + lilyCount;
                    go.transform.position = new Vector3(x, WaterLevel, z);
                    go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                    go.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.4f, (float)rnd.NextDouble());
                    lilyCount++;
                }
            }
            foreach (var c in lilyClusters)
                LilyCluster(c[1] * (EunhaDamBuilder.ChannelHalfWidth(c[0]) - 4.5f), c[0], 4 + rnd.Next(3));
            // 풍영정 둘레 수련 고리 (대 가장자리 밖 수면)
            LilyCluster(-5.9f, 31.6f, 5);
            LilyCluster(-16.8f, 29.7f, 4);
            LilyCluster(-20.1f, 48.4f, 5);
            LilyCluster(-2.2f, 41.9f, 4);

            // ── 나무 (2026-08-07 재구성: 흑송 전량 제거 — 전정된 일본 정원수 형태라 조선 배경 부적합.
            //    벚나무JG(01·05 모두 여름잎 머티리얼) 중경 주력, +X 마을 길목~확장 지형(스커트)에 숲.
            //    새 배치는 실제 Terrain 높이 샘플(SampleH — 스커트 타일 포함) 사용) ──
            var aspen = LoadVeg("Aspen_사시나무_원경");
            var cherries = new[] { "Cherry_벚나무JG_대_01", "Cherry_벚나무JG_대_05", "Cherry_벚나무JG_중_01",
                                   "Cherry_벚나무JG_중_05", "Cherry_벚나무JG_소_01", "Cherry_벚나무JG_소_05" }
                           .Select(LoadVeg).ToArray();
            var bamboos = new[] { LoadVeg("Bamboo_대나무_Henonis_1_VR"), LoadVeg("Bamboo_대나무_Henonis_2_VR"),
                                  LoadVeg("Bamboo_왕대JG_중_01"), LoadVeg("Bamboo_왕대JG_중_02") };

            int aspenCount = 0; attempts = 0;
            while (aspenCount < 24 && attempts++ < 3000)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(70f, 95f, (float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                float h = EunhaDamBuilder.GroundHeight(x, z);
                if (h < 1.5f || h > 5.5f) continue;
                if (SlopeDeg(x, z) > 25f) continue;
                if (InAvoid(x, z)) continue;
                if (Blocked(x, z, planted, 4f)) continue;
                Plant(forestG, aspen, "사시_" + aspenCount, x, h, z, rnd, 0.8f, 1.35f);
                planted.Add(new Vector2(x, z));
                aspenCount++;
            }

            // ── 사시나무 확장 지형(스커트) 원경 링 — 안개 소실(220m) 직전까지 ──
            int aspenFar = 0; attempts = 0;
            while (aspenFar < 30 && attempts++ < 4000)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(100f, 185f, (float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                if (x > 36f && Mathf.Abs(z) < 11f) continue;   // 마을 길 통행로
                float h = SampleH(x, z);
                if (h < 0.9f || h > 6.5f) continue;            // 강 골·자갈 물가 제외
                if (SlopeDegT(x, z) > 30f) continue;
                if (Blocked(x, z, planted, 5f)) continue;
                Plant(forestG, aspen, "사시원경_" + aspenFar, x, h, z, rnd, 0.7f, 1.3f);
                planted.Add(new Vector2(x, z));
                aspenFar++;
            }

            // ── 벚나무 중경 링 (흑송 대체 주력) — 군집 단위, 줄 서기 방지 ──
            int cherryCount = 0, cherryClusters = 0; attempts = 0;
            while (cherryClusters < 12 && attempts++ < 1500)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(48f, 92f, (float)rnd.NextDouble());
                float cx = Mathf.Cos(ang) * r, cz = Mathf.Sin(ang) * r;
                float ch = SampleH(cx, cz);
                if (ch < 1.2f || ch > 6.5f) continue;
                if (InAvoid(cx, cz) || NearPavilion(cx, cz, 12f)) continue;
                if (PlantCluster(forestG, rnd, planted, "벚", ref cherryCount, cherries, null,
                        cx, cz, 3 + rnd.Next(2), 6.5f, 1.2f, 6.5f, 2.8f) > 0)
                    cherryClusters++;
            }

            // ── 입구 랜드마크 버드나무 — 극적 연출 (2026-08-07 3차 정정):
            //    스폰(48, y7, 0)에서 다리(-X)를 바라볼 때 가지가 화면 한쪽을 걸치고
            //    그 너머로 월정교가 드러나는 프레임 위치. 상수 좌표 — 후보 확정 시 여기만 수정.
            float lmX = LandmarkX, lmZ = LandmarkZ;
            {
                var lm = (GameObject)PrefabUtility.InstantiatePrefab(willows[0], forestG);
                lm.name = "랜드마크_버드나무";
                lm.transform.position = new Vector3(lmX, SampleH(lmX, lmZ) - 0.1f, lmZ);
                lm.transform.rotation = Quaternion.Euler(0f, LandmarkRotY, 0f);
                lm.transform.localScale = Vector3.one * 1.75f;   // 랜드마크 — 일반 버드나무의 약 1.7배
                planted.Add(new Vector2(lmX, lmZ));

                // 그늘 아래 걸터앉을 낮은 바위 2개 (길 반대편이 아니라 항상 길 쪽으로)
                var seatRock = AssetDatabase.LoadAssetAtPath<GameObject>(SeyeonPath + "SM_Rock_K_VR.prefab");
                if (seatRock != null)
                {
                    float zs = Mathf.Sign(lmZ);   // 나무가 남측이면 바위 오프셋도 뒤집는다
                    var r1 = (GameObject)PrefabUtility.InstantiatePrefab(seatRock, forestG);
                    r1.name = "랜드마크_앉음바위_1";
                    float rx = lmX - 2.2f, rz = lmZ - 2.6f * zs;
                    r1.transform.position = new Vector3(rx, SampleH(rx, rz) - 0.45f, rz);
                    r1.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
                    r1.transform.localScale = new Vector3(1.1f, 0.55f, 1.1f);   // 낮고 넓게
                    var r2 = (GameObject)PrefabUtility.InstantiatePrefab(seatRock, forestG);
                    r2.name = "랜드마크_앉음바위_2";
                    rx = lmX + 1.9f; rz = lmZ - 1.8f * zs;
                    r2.transform.position = new Vector3(rx, SampleH(rx, rz) - 0.55f, rz);
                    r2.transform.rotation = Quaternion.Euler(0f, 210f, 0f);
                    r2.transform.localScale = new Vector3(0.8f, 0.45f, 0.8f);
                }
            }

            // ── +X 마을 길목 양옆 — 듬성듬성 (시야 트임, 꽃밭이 주인공) ──
            var gatePool = new[] { cherries[2], cherries[3], cherries[4], cherries[5],
                                   bamboos[0], bamboos[1], aspen };
            int gateCount = 0, gateClusters = 0; attempts = 0;
            while (gateClusters < 3 && attempts++ < 600)
            {
                float cx = Mathf.Lerp(58f, 108f, (float)rnd.NextDouble());
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float cz = side * Mathf.Lerp(14f, 28f, (float)rnd.NextDouble());
                if (PlantCluster(forestG, rnd, planted, "길목", ref gateCount, gatePool, null,
                        cx, cz, 2 + rnd.Next(2), 7f, 1.0f, 7.2f, 5f) > 0)
                    gateClusters++;
            }

            // ── +X 확장 지형 숲 — 멀리 보이는 정도, 넓은 간격 ──
            var farPool = new[] { aspen, cherries[0], cherries[1], cherries[2], cherries[3],
                                  bamboos[2], bamboos[3] };
            var farW = new[] { 0.50f, 0.08f, 0.08f, 0.12f, 0.12f, 0.05f, 0.05f };
            int farCount = 0, farClusters = 0; attempts = 0;
            while (farClusters < 9 && attempts++ < 1800)
            {
                float cx = Mathf.Lerp(110f, 190f, (float)rnd.NextDouble());
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float cz = side * Mathf.Lerp(18f, 62f, (float)rnd.NextDouble());
                if (PlantCluster(forestG, rnd, planted, "동숲", ref farCount, farPool, farW,
                        cx, cz, 4 + rnd.Next(2), 12f, 0.9f, 7f, 6f) > 0)
                    farClusters++;
            }

            // ── 대나무 — 경계·틈새 (풍영정 맞은편 기존 6 + 외곽 경계 4) ──
            int hCount = 0;
            foreach (var c in new[] { new Vector2(-38f, 44f), new Vector2(-41f, 39f), new Vector2(-35f, 49f),
                                      new Vector2(-37f, -42f), new Vector2(-40f, -37f), new Vector2(-34f, -47f),
                                      new Vector2(93f, 42f), new Vector2(95f, -47f),
                                      new Vector2(-88f, 57f), new Vector2(-86f, -62f) })
            {
                float x = c.x + Jitter(rnd, 1.5f), z = c.y + Jitter(rnd, 1.5f);
                float h = SampleH(x, z);
                if (h < 0.9f || h > 7.2f) continue;
                Plant(forestG, bamboos[hCount % bamboos.Length], "대나무_" + hCount, x, h, z, rnd, 0.8f, 1.25f);
                hCount++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[은하담] 식생 배치 완료: 버드나무 {w}+랜드마크 1(x{lmX:F1}, z{lmZ:F1}), " +
                      $"갈대 {reedCount}, 수련 {lilyCount}, " +
                      $"사시 근경 {aspenCount} + 원경 {aspenFar}, 벚나무 {cherryCount}(군집 {cherryClusters}), " +
                      $"길목 {gateCount}(군집 {gateClusters}), 동쪽숲 {farCount}(군집 {farClusters}), " +
                      $"대나무 {hCount} — 흑송·소나무VR 0 (전량 제거)");
        }

        // ── 풍영정 URP 머티리얼 (원본 FBX·폴더 무수정 — 씬 인스턴스에만 적용) ──
        static void ApplyPungyeongMaterials(GameObject instance)
        {
            const string texDir = "Assets/woljeonggyo/pungyeongjeong-pavilion/textures/";
            var mats = new Dictionary<string, Material>();
            foreach (var key in new[] { "woods", "Roof", "extra" })
            {
                string matPath = MatFolder + "/풍영정_" + key + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Poongyoung_" + key + "_albedo.jpg");
                mat.SetTexture("_BaseMap", albedo);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.2f);
                EditorUtility.SetDirty(mat);
                mats["Poongyoung_" + key] = mat;
            }
            AssetDatabase.SaveAssets();

            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                    if (shared[i] != null && mats.TryGetValue(shared[i].name, out var m))
                        shared[i] = m;
                r.sharedMaterials = shared;
            }
        }

        // ── 헬퍼 ────────────────────────────────────────────
        static GameObject PlacePrefab(GameObject parent, string path, string name,
            Vector3 pos, float rotY, float scale, bool snapBaseY = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError("[은하담] 프리팹 없음: " + path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            go.transform.localScale = Vector3.one * scale;
            if (snapBaseY)
            {
                var rends = go.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    go.transform.position += Vector3.up * (pos.y - b.min.y);
                }
            }
            return go;
        }

        static void Plant(Transform parent, GameObject prefab, string name,
            float x, float h, float z, System.Random rnd, float sMin, float sMax)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = new Vector3(x, h - 0.06f, z);
            go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            go.transform.localScale = Vector3.one * Mathf.Lerp(sMin, sMax, (float)rnd.NextDouble());
        }

        static void PlaceClump(Transform parent, GameObject prefab, string name,
            Vector3 pos, System.Random rnd, float sMin, float sMax)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            go.transform.localScale = Vector3.one * Mathf.Lerp(sMin, sMax, (float)rnd.NextDouble());
        }

        static GameObject LoadVeg(string name)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(VegPath + name + ".prefab");
            if (p == null) Debug.LogError("[은하담] 식생 프리팹 없음: " + name);
            return p;
        }

        /// <summary>실제 Terrain 높이 (스커트 타일 포함 — 어느 타일 위든 동작).
        /// 지형 밖이면 해석 함수로 폴백.</summary>
        static float SampleH(float x, float z)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var p = t.transform.position; var s = t.terrainData.size;
                if (x >= p.x && x <= p.x + s.x && z >= p.z && z <= p.z + s.z)
                    return t.SampleHeight(new Vector3(x, 0f, z)) + p.y;
            }
            return EunhaDamBuilder.GroundHeight(x, z);
        }

        static float SlopeDegT(float x, float z)
        {
            float sx = SampleH(x + 1f, z) - SampleH(x - 1f, z);
            float sz = SampleH(x, z + 1f) - SampleH(x, z - 1f);
            return Mathf.Atan(0.5f * Mathf.Sqrt(sx * sx + sz * sz)) * Mathf.Rad2Deg;
        }

        static GameObject WeightedPick(System.Random rnd, GameObject[] pool, float[] weights)
        {
            if (weights == null) return pool[rnd.Next(pool.Length)];
            float sum = 0f;
            foreach (var wgt in weights) sum += wgt;
            float pick = (float)rnd.NextDouble() * sum;
            for (int i = 0; i < pool.Length; i++)
            {
                pick -= weights[i];
                if (pick <= 0f) return pool[i];
            }
            return pool[pool.Length - 1];
        }

        /// <summary>군집 식재 — 중심 주변 원판에 랜덤 산포(√r 분포로 중심 밀집).
        /// 통행로(x&gt;36, |z|&lt;11)·회피 구역·풍영정 근처는 건너뛴다. 스케일 0.7~1.3.</summary>
        static int PlantCluster(Transform parent, System.Random rnd, List<Vector2> planted,
            string prefix, ref int counter, GameObject[] pool, float[] weights,
            float cx, float cz, int count, float radius, float minH, float maxH, float spacing)
        {
            int placed = 0, tries = 0;
            while (placed < count && tries++ < count * 12)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rnd.NextDouble()) * radius;
                float x = cx + Mathf.Cos(ang) * r, z = cz + Mathf.Sin(ang) * r;
                if (x > 36f && Mathf.Abs(z) < 11f) continue;   // 마을 길 통행로는 항상 트인다
                if (InAvoid(x, z) || NearPavilion(x, z, 8f)) continue;
                float h = SampleH(x, z);
                if (h < minH || h > maxH) continue;
                if (SlopeDegT(x, z) > 30f) continue;
                if (Blocked(x, z, planted, spacing)) continue;
                Plant(parent, WeightedPick(rnd, pool, weights), prefix + "_" + counter, x, h, z, rnd, 0.7f, 1.3f);
                planted.Add(new Vector2(x, z));
                counter++; placed++;
            }
            return placed;
        }

        static float SlopeDeg(float x, float z)
        {
            float sx = EunhaDamBuilder.GroundHeight(x + 1f, z) - EunhaDamBuilder.GroundHeight(x - 1f, z);
            float sz = EunhaDamBuilder.GroundHeight(x, z + 1f) - EunhaDamBuilder.GroundHeight(x, z - 1f);
            return Mathf.Atan(0.5f * Mathf.Sqrt(sx * sx + sz * sz)) * Mathf.Rad2Deg;
        }

        static bool InAvoid(float x, float z) =>
            AvoidRects.Any(r => x > r[0] && x < r[1] && z > r[2] && z < r[3]);

        static bool NearPavilion(float x, float z, float margin) =>
            Vector2.Distance(new Vector2(x, z),
                new Vector2(EunhaDamBuilder.PondPadX, EunhaDamBuilder.PondPadZ)) < margin;

        static bool Blocked(float x, float z, List<Vector2> pts, float minDist)
        {
            var v = new Vector2(x, z);
            foreach (var p in pts) if (Vector2.Distance(p, v) < minDist) return true;
            return false;
        }

        static float Jitter(System.Random rnd, float amp) =>
            ((float)rnd.NextDouble() * 2f - 1f) * amp;

        static Transform NewChild(GameObject parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent.transform, false);
            return t;
        }

        static GameObject RecreateGroup(string name)
        {
            var old = FindRoot(name);
            if (old != null) Object.DestroyImmediate(old);
            return new GameObject(name);
        }

        static GameObject FindRoot(string name) =>
            SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);

        static void ApplyStaticFlags(GameObject group)
        {
            var flags = StaticEditorFlags.BatchingStatic
                      | StaticEditorFlags.OccluderStatic
                      | StaticEditorFlags.OccludeeStatic;
            foreach (var t in group.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }
    }
}
