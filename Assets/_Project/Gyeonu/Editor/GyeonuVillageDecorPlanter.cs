using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 견우마을 건물·식생 배치기 (고정 시드, 멱등 — 그룹 단위 재생성).
    ///
    /// 은둔처: 후보 3종 스왑 메뉴(제월당/초정/광풍각) — 공용 키트(디딤돌·축대 바위·
    ///         징검다리·보행 콜라이더)는 후보와 무관하게 유지. 남단이 물에 걸치고
    ///         진입은 서쪽 물가 → 징검다리 → 대(臺) 서편 디딤돌.
    ///
    /// 숲 4존: ① 협곡 rim 빽빽(왕대·벚·곰솔/소나무VR 소수 — 시야 완전 차단)
    ///         ② 고지대 중밀도(사시·벚·느릅 소수) ③ 연못가 성김(세연정식 — 물가 소나무
    ///         드문드문 + 서쪽 버드나무 1) ④ 외곽~스커트 원경 숲(사시 위주, 원경 산 기슭까지)
    ///         흑송JG·상수리(55만tri) 미사용. VR 소나무류는 LOD0 10만tri라 수량 캡.
    ///
    /// 물가: 바위 물가띠 + 반쯤 잠긴 바위(물가선 원호 깨기), 갈대 h 0.05~0.65 띠,
    ///         수련 군집(못 중앙은 별 반사를 위해 비움).
    ///
    /// 폐가: 낙안 초가 4채 — 길에서 8~12m 벗어난 협곡 rim 위. 기울이고(3~8°) 가라앉혀
    ///         (0.4~0.9m) 폐가 연출, Props(세간) 비활성 — 버려진 지 오래된 느낌.
    ///         접근 불가(분위기용) 전제라 부재 단위 파괴는 하지 않는다.
    /// </summary>
    public static class GyeonuVillageDecorPlanter
    {
        const int Seed = 20260809;
        const string VegPath = "Assets/_Project/Gyeonu/Prefabs/Vegetation/";
        const string HutPath = "Assets/_Project/Gyeonu/Prefabs/Naganeupseong/Buildings/";
        const float WaterLevel = GyeonuVillageBuilder.WaterLevel;
        const float PadX = GyeonuVillageBuilder.PadX, PadZ = GyeonuVillageBuilder.PadZ;

        // 숲길 폴리라인 (빌더와 동일 — 회피·rim 판정용)
        static readonly Vector2[] PathPts =
        {
            new Vector2(2f, -51f), new Vector2(3f, -42f), new Vector2(11f, -34f),
            new Vector2(12f, -25f), new Vector2(3f, -17f), new Vector2(-7f, -12f),
            new Vector2(-13f, -5f), new Vector2(-11f, 1f),
        };

        // ── 은둔처 후보 ──────────────────────────────────────
        [MenuItem("Tools/이문록/견우마을 은둔처 후보 A (제월당)")]
        public static void PlaceCandidateA() =>
            PlaceHermitage("Assets/Soswaewon/Prefabs/Buildings/Jewoldang_Hall.prefab",
                "은둔처_제월당", new Vector3(4f, 1.7f, 32.8f), stepsFace: 270f);

        [MenuItem("Tools/이문록/견우마을 은둔처 후보 B (초정)")]
        public static void PlaceCandidateB() =>
            PlaceHermitage("Assets/Soswaewon/Prefabs/Buildings/Chojeong_Pavilion.prefab",
                "은둔처_초정", new Vector3(4f, 1.7f, 31.8f), stepsFace: -1f);

        [MenuItem("Tools/이문록/견우마을 은둔처 후보 C (광풍각)")]
        public static void PlaceCandidateC() =>
            PlaceHermitage("Assets/Soswaewon/Prefabs/Buildings/Gwangpunggak_Pavilion.prefab",
                "은둔처_광풍각", new Vector3(4f, 1.7f, 33.2f), stepsFace: 270f);

        /// <summary>후보 건물 교체 + 공용 키트 보장. stepsFace: 돌계단이 향할 방위(-1이면 회전 안 함).</summary>
        static void PlaceHermitage(string prefabPath, string name, Vector3 pos, float stepsFace)
        {
            var group = EnsureGroup("견우마을_건물");
            // 기존 후보 제거 (은둔처_* 전부)
            for (int i = group.transform.childCount - 1; i >= 0; i--)
            {
                var c = group.transform.GetChild(i);
                if (c.name.StartsWith("은둔처_")) Object.DestroyImmediate(c.gameObject);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogError("[견우마을] 프리팹 없음: " + prefabPath); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            go.name = name;
            go.transform.position = pos;

            // 돌계단(진입면)이 지정 방위를 향하게 회전 — 계단 자식들의 평균 방향으로 계산
            if (stepsFace >= 0f)
            {
                var steps = go.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.Contains("Stone_Steps") || t.name.Contains("Stair"))
                    .ToArray();
                if (steps.Length > 0)
                {
                    var centerXZ = new Vector2(pos.x, pos.z);
                    Vector2 dir = Vector2.zero;
                    foreach (var s in steps)
                        dir += new Vector2(s.position.x, s.position.z) - centerXZ;
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        float cur = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                        go.transform.rotation = Quaternion.Euler(0f, stepsFace - cur, 0f);
                    }
                }
            }

            // 기단 하부가 바닥에 붙게 스냅 (광풍각은 피벗 아래 -0.85 기단)
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                go.transform.position += Vector3.up * (pos.y - 0.15f - b.min.y);   // 기단 15cm 묻기
            }

            EnsureHermitageKit(group);
            ApplyStaticFlags(go);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[견우마을] 은둔처 후보 배치: " + name);
        }

        /// <summary>공용 키트 — 징검다리(서남 출수 계류 횡단)·디딤돌·축대 바위·보행 콜라이더. 멱등.</summary>
        static void EnsureHermitageKit(GameObject group)
        {
            var old = group.transform.Find("은둔처키트");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var kit = NewChild(group, "은둔처키트");

            var flatRock = AssetDatabase.LoadAssetAtPath<GameObject>(VegPath + "SM_Rock_K_VR.prefab");
            var bigRock = AssetDatabase.LoadAssetAtPath<GameObject>(VegPath + "SM_Rock_G_VR.prefab");
            if (flatRock == null || bigRock == null) { Debug.LogError("[견우마을] 키트 바위 프리팹 누락"); return; }

            // 징검다리 3석 — 출수 계류((-13,11)→(-21,5), 반폭 2.8) 중점 (-17,8) 횡단.
            // 횡단 방향 (0.6,-0.8) — 남동안에서 북서안으로
            var crossDir = new Vector2(0.6f, -0.8f);
            for (int i = -1; i <= 1; i++)
            {
                float x = -17f + crossDir.x * i * 2.3f;
                float z = 8f + crossDir.y * i * 2.3f;
                var s = (GameObject)PrefabUtility.InstantiatePrefab(flatRock, kit);
                s.name = "징검다리_" + (i + 2);
                s.transform.position = new Vector3(x, 0.32f, z);
                s.transform.rotation = Quaternion.Euler(0f, 60f * i + 20f, 0f);
                s.transform.localScale = new Vector3(1.15f, 0.35f, 1.15f);
            }
            // 징검다리 보행판 (얇은 투명 콜라이더 — 걸음 튐 방지)
            var walk = new GameObject("징검다리_보행판");
            walk.transform.SetParent(kit, false);
            walk.transform.position = new Vector3(-17f, 0.72f, 8f);
            walk.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(crossDir.x, crossDir.y) * Mathf.Rad2Deg, 0f);
            var wc = walk.AddComponent<BoxCollider>();
            wc.size = new Vector3(1.3f, 0.08f, 9f);

            // 대(臺) 서편 디딤돌 (저지대 1.3 → 대 1.7)
            var st = (GameObject)PrefabUtility.InstantiatePrefab(flatRock, kit);
            st.name = "디딤돌_대서편";
            st.transform.position = new Vector3(PadX - 5.6f, 1.15f, PadZ - 1.5f);
            st.transform.rotation = Quaternion.Euler(0f, 100f, 0f);
            st.transform.localScale = new Vector3(1.3f, 0.4f, 1.3f);

            // 대 남단 축대 바위 — 물에 걸친 기단을 받치는 큰 바위들
            float[][] jugak = { new[] { 0.5f, 29.6f, 0.9f, 40f }, new[] { 4.5f, 29.0f, 1.05f, 160f },
                                new[] { 8.0f, 30.2f, 0.8f, 280f }, new[] { -2.2f, 31.5f, 0.7f, 220f } };
            int ji = 0;
            foreach (var j in jugak)
            {
                var r = (GameObject)PrefabUtility.InstantiatePrefab(bigRock, kit);
                r.name = "축대바위_" + ji++;
                r.transform.position = new Vector3(j[0], -0.15f, j[1]);
                r.transform.rotation = Quaternion.Euler(0f, j[3], 0f);
                r.transform.localScale = Vector3.one * j[2];
            }
            ApplyStaticFlags(kit.gameObject);
        }

        // ── 폐가 ────────────────────────────────────────────
        // { 프리팹, x, z, rotY, tiltX, tiltZ, 가라앉기 }
        static readonly object[][] Huts =
        {
            new object[] { "Bamboo_Rafter_House_02", -8f, -44f, 115f, 4.5f, -6f, 0.7f },
            new object[] { "Bamboo_Rafter_House_01", 21f, -32f, 250f, -5f, 3.5f, 0.55f },
            new object[] { "Wooden_Bench_House_03", -6f, -27f, 80f, 6f, 4f, 0.85f },
            new object[] { "Bamboo_Rafter_House_03", -18f, -16f, 330f, -4f, -7f, 0.6f },
        };

        [MenuItem("Tools/이문록/견우마을 폐가 배치")]
        public static void PlaceRuinedHuts()
        {
            var group = EnsureGroup("견우마을_건물");
            var old = group.transform.Find("폐가");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = NewChild(group, "폐가");

            int placed = 0;
            foreach (var h in Huts)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HutPath + (string)h[0] + ".prefab");
                if (prefab == null) { Debug.LogWarning("[견우마을] 폐가 프리팹 없음: " + h[0]); continue; }
                float x = (float)h[1], z = (float)h[2];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = "폐가_" + (string)h[0];
                go.transform.position = new Vector3(x, SampleH(x, z) - (float)h[6], z);
                go.transform.rotation = Quaternion.Euler((float)h[4], (float)h[3], (float)h[5]);

                // 세간(Props) 비활성 — 오래 버려진 빈집
                var props = go.transform.Find("Props");
                if (props != null) props.gameObject.SetActive(false);
                placed++;
            }
            ApplyStaticFlags(root.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[견우마을] 폐가 " + placed + "채 배치 (기울임+침하, Props 제거)");
        }

        // ── 숲 ──────────────────────────────────────────────
        [MenuItem("Tools/이문록/견우마을 숲 배치")]
        public static void PlantForest()
        {
            var root = EnsureGroup("견우마을_식생");
            foreach (var n in new[] { "협곡숲", "고지대숲", "연못가", "외곽숲", "고사리" })
            {
                var old = root.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var gorgeG = NewChild(root, "협곡숲");
            var highG = NewChild(root, "고지대숲");
            var pondG = NewChild(root, "연못가");
            var farG = NewChild(root, "외곽숲");
            var fernG = NewChild(root, "고사리");

            var rnd = new System.Random(Seed);
            var planted = new List<Vector2>();

            var aspen = LoadVeg("Aspen_사시나무_원경");
            var cherries = new[] { "Cherry_벚나무JG_대_01", "Cherry_벚나무JG_대_05", "Cherry_벚나무JG_중_01",
                                   "Cherry_벚나무JG_중_05" }.Select(LoadVeg).ToArray();
            var bamboosJG = new[] { "Bamboo_왕대JG_대_01", "Bamboo_왕대JG_대_02", "Bamboo_왕대JG_중_01",
                                    "Bamboo_왕대JG_중_02" }.Select(LoadVeg).ToArray();
            var pinesVR = new[] { "Pine_곰솔_PinusThunbergii_1_VR", "Pine_곰솔_PinusThunbergii_2_VR",
                                  "Pine_소나무_PinusDensiflora_1_VR", "Pine_소나무_PinusDensiflora_2_VR" }
                          .Select(LoadVeg).ToArray();
            var elm = LoadVeg("Elm_느릅_Ulmus_2_VR");
            var willow = LoadVeg("Willow_버드나무_Salix_1_VR");
            var henonis = new[] { LoadVeg("Bamboo_대나무_Henonis_1_VR"), LoadVeg("Bamboo_대나무_Henonis_2_VR") };
            var ferns = new[] { "Fern_고사리JG_군락_01", "Fern_고사리JG_군락_02" }.Select(LoadVeg).ToArray();

            int pineVRUsed = 0, elmUsed = 0;
            const int PineVRCap = 42, ElmCap = 30;

            // ── ① 협곡 rim — 길 양옆 2줄(안 3.5~6.5 / 밖 7~11.5m), 1.6m 간격 빽빽하게.
            //    밤에 잎이 하얗게 뜨는 사시·벚은 배제 — 왕대·담대·솔·느릅 등 어두운 수종만 ──
            int gorge = 0;
            float pathTotal = 0f;
            for (int i = 1; i < PathPts.Length; i++) pathTotal += Vector2.Distance(PathPts[i - 1], PathPts[i]);
            for (float d = 2f; d < pathTotal - 3f; d += 1.6f)
            {
                var (pt, tang) = PointOnPath(d);
                var normal = new Vector2(-tang.y, tang.x);
                foreach (float side in new[] { 1f, -1f })
                foreach (float[] band in new[] { new[] { 3.5f, 6.5f }, new[] { 7f, 11.5f } })
                {
                    if (rnd.NextDouble() < 0.25) continue;   // 가끔 빈 자리 — 자연스러운 불규칙
                    float off = Mathf.Lerp(band[0], band[1], (float)rnd.NextDouble());
                    float x = pt.x + normal.x * off * side + Jitter(rnd, 0.7f);
                    float z = pt.y + normal.y * off * side + Jitter(rnd, 0.7f);
                    float h = SampleH(x, z);
                    float floorH = SampleH(pt.x, pt.y);
                    if (h < floorH + 0.4f) continue;         // 길바닥엔 심지 않는다 (벽·rim만)
                    if (PondSd(x, z) < 4f) continue;
                    if (NearHut(x, z, 4.5f)) continue;
                    if (Blocked(x, z, planted, 1.7f)) continue;

                    GameObject pick;
                    double roll = rnd.NextDouble();
                    if (roll < 0.42) pick = bamboosJG[rnd.Next(bamboosJG.Length)];
                    else if (roll < 0.56) pick = henonis[rnd.Next(2)];
                    else if (roll < 0.80 && pineVRUsed < PineVRCap) { pick = pinesVR[rnd.Next(pinesVR.Length)]; pineVRUsed++; }
                    else if (elmUsed < ElmCap) { pick = elm; elmUsed++; }
                    else pick = bamboosJG[rnd.Next(bamboosJG.Length)];
                    Plant(gorgeG, pick, "협곡_" + gorge, x, h, z, rnd, 0.95f, 1.45f);
                    planted.Add(new Vector2(x, z));
                    gorge++;
                }
            }

            // ── ② 고지대 숲 — 협곡·연못 밖 중밀도 ──
            int high = 0, attempts = 0;
            while (high < 120 && attempts++ < 6000)
            {
                float x = Mathf.Lerp(-48f, 48f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(-48f, 48f, (float)rnd.NextDouble());
                float h = SampleH(x, z);
                if (h < 4.2f || h > 13f) continue;           // 고지대만
                if (PathD(x, z) < 10f) continue;             // 협곡숲과 분리
                if (PondSd(x, z) < 8f) continue;
                if (SlopeDegT(x, z) > 33f) continue;
                if (NearHut(x, z, 5.5f)) continue;
                if (Blocked(x, z, planted, 4.2f)) continue;

                GameObject pick;
                double roll = rnd.NextDouble();
                if (roll < 0.30) pick = aspen;                       // 원경 실루엣용만
                else if (roll < 0.48) pick = cherries[rnd.Next(cherries.Length)];
                else if (roll < 0.62 && elmUsed < ElmCap) { pick = elm; elmUsed++; }
                else if (roll < 0.72) pick = henonis[rnd.Next(2)];
                else pick = bamboosJG[rnd.Next(bamboosJG.Length)];
                Plant(highG, pick, "고지대_" + high, x, h, z, rnd, 0.8f, 1.3f);
                planted.Add(new Vector2(x, z));
                high++;
            }

            // ── ③ 연못가 — 세연정식 드문드문 (물가 소나무 + 서쪽 버드나무 + 입수구 대숲) ──
            // { x, z } 물가 앵커 — 못 중앙 시야(모퉁이→은둔처)는 비운다
            float[][] pondAnchors =
            {
                new[] { -14f, 27f }, new[] { -21f, 17f }, new[] { 13f, 4.5f }, new[] { 22f, 9f },
                new[] { 25f, 24f }, new[] { 17f, 30f }, new[] { -3f, 6f }, new[] { 30f, 34f },
                new[] { -9f, 30f }, new[] { 12f, 36f },
            };
            int pond = 0;
            foreach (var a in pondAnchors)
            {
                float x = a[0] + Jitter(rnd, 1.2f), z = a[1] + Jitter(rnd, 1.2f);
                float h = SampleH(x, z);
                if (h < 0.55f || h > 3.5f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(PadX, PadZ)) < 7f) continue;
                var pick = pineVRUsed < PineVRCap + 10 ? pinesVR[rnd.Next(pinesVR.Length)] : cherries[0];
                pineVRUsed++;
                Plant(pondG, pick, "물가솔_" + pond, x, h, z, rnd, 1.1f, 1.5f);   // VR솔 5~6m — 키워서
                planted.Add(new Vector2(x, z));
                pond++;
            }
            // 서쪽 볼록 물가 버드나무 1 (세연정 물가 실루엣)
            {
                float x = -22.5f, z = 22f;
                Plant(pondG, willow, "물가버들", x, SampleH(x, z) - 0.05f + 0.05f, z, rnd, 0.9f, 1.0f);
                planted.Add(new Vector2(x, z));
            }
            // 북동 입수 계류 옆 담대 (Henonis — 어두운 대숲 실루엣)
            foreach (var c in new[] { new Vector2(27f, 30f), new Vector2(30f, 38f), new Vector2(23f, 40f) })
            {
                float x = c.x + Jitter(rnd, 1f), z = c.y + Jitter(rnd, 1f);
                float h = SampleH(x, z);
                if (h < 0.55f) continue;
                Plant(pondG, henonis[pond % 2], "입수구대숲_" + pond, x, h, z, rnd, 0.9f, 1.3f);
                planted.Add(new Vector2(x, z));
                pond++;
            }

            // ── ④ 외곽 숲 — 지형 가장자리~스커트, 원경 산 기슭까지 ──
            int far = 0; attempts = 0;
            while (far < 150 && attempts++ < 8000)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(48f, 145f, (float)rnd.NextDouble());
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                if (Mathf.Abs(x - 2f) < 8f && z < -44f) continue;   // 남쪽 숲길 연장 통로
                float h = SampleH(x, z);
                if (h < 2f || h > 20f) continue;
                if (SlopeDegT(x, z) > 34f) continue;
                if (Blocked(x, z, planted, 6.5f)) continue;
                var pick = rnd.NextDouble() < 0.78 ? aspen : cherries[rnd.Next(cherries.Length)];
                Plant(farG, pick, "외곽_" + far, x, h, z, rnd, 0.85f, 1.45f);
                planted.Add(new Vector2(x, z));
                far++;
            }

            // ── 고사리 — 길바닥 가장자리·폐가·대(臺) 둘레 (306tri — 부담 없음) ──
            int fern = 0;
            for (float d = 4f; d < pathTotal - 4f; d += 5.5f)
            {
                var (pt, tang) = PointOnPath(d);
                var normal = new Vector2(-tang.y, tang.x);
                float side = rnd.NextDouble() < 0.5 ? 1f : -1f;
                float off = Mathf.Lerp(1.6f, 2.6f, (float)rnd.NextDouble());
                float x = pt.x + normal.x * off * side, z = pt.y + normal.y * off * side;
                Plant(fernG, ferns[rnd.Next(2)], "고사리_길_" + fern++, x, SampleH(x, z), z, rnd, 0.9f, 1.6f);
            }
            foreach (var h in Huts)
                for (int i = 0; i < 3; i++)
                {
                    float x = (float)h[1] + Jitter(rnd, 3.5f), z = (float)h[2] + Jitter(rnd, 3.5f);
                    Plant(fernG, ferns[rnd.Next(2)], "고사리_폐가_" + fern++, x, SampleH(x, z), z, rnd, 1.0f, 1.8f);
                }
            for (int i = 0; i < 5; i++)
            {
                float x = PadX + Jitter(rnd, 6f), z = PadZ + Jitter(rnd, 4f) + 2f;
                float hh = SampleH(x, z);
                if (hh < 0.6f) continue;
                Plant(fernG, ferns[rnd.Next(2)], "고사리_대_" + fern++, x, hh, z, rnd, 0.9f, 1.4f);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[견우마을] 숲 배치: 협곡 {gorge}, 고지대 {high}, 연못가 {pond}+버들1, " +
                      $"외곽 {far}, 고사리 {fern} (VR솔 {pineVRUsed}, 느릅 {elmUsed}, 흑송 0)");
        }

        // ── 물가 정리 ────────────────────────────────────────
        [MenuItem("Tools/이문록/견우마을 물가 배치")]
        public static void PlaceWaterside()
        {
            var root = EnsureGroup("견우마을_식생");
            var old = root.transform.Find("물가");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var wg = NewChild(root, "물가");

            var rnd = new System.Random(Seed + 7);
            var pts = new List<Vector2>();

            string[] rockNames = { "SM_Rock_A_VR", "SM_Rock_C_VR", "SM_Rock_E_VR",
                                   "SM_Rock_G_VR", "SM_Rock_K_VR", "SM_Rock_L_VR" };
            var rocks = rockNames.Select(n => LoadVeg(n)).ToArray();
            if (rocks.Any(r => r == null)) return;

            // 물가 바위 — 물가선(sd -0.5~2) 원호를 깨는 띠
            int shore = 0, attempts = 0;
            while (shore < 24 && attempts++ < 3000)
            {
                float x = Mathf.Lerp(-28f, 36f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(-1f, 48f, (float)rnd.NextDouble());
                float sd = PondSd(x, z);
                if (sd < -0.5f || sd > 2.0f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(PadX, PadZ)) < 7f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(-17f, 8f)) < 4.5f) continue;   // 징검다리
                if (Blocked(x, z, pts, 3.2f)) continue;
                int idx = rnd.Next(rocks.Length);
                float s = Mathf.Lerp(0.5f, 1.0f, (float)rnd.NextDouble());
                if (idx == 0) s *= 0.5f;   // Rock_A 9.6m급
                var go = (GameObject)PrefabUtility.InstantiatePrefab(rocks[idx], wg);
                go.name = "바위_물가_" + shore;
                go.transform.position = new Vector3(x, SampleH(x, z) - 0.15f, z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * s;
                pts.Add(new Vector2(x, z));
                shore++;
            }
            // 반쯤 잠긴 바위 — 물속(sd -3.5~-1)에서 머리만 내밀게
            int inWater = 0; attempts = 0;
            while (inWater < 10 && attempts++ < 2000)
            {
                float x = Mathf.Lerp(-26f, 34f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(1f, 46f, (float)rnd.NextDouble());
                float sd = PondSd(x, z);
                if (sd < -3.5f || sd > -1.0f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(PadX, PadZ)) < 8f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(-17f, 8f)) < 5f) continue;
                if (Blocked(x, z, pts, 6f)) continue;
                int idx = 1 + rnd.Next(rocks.Length - 1);   // A(대형) 제외
                var go = (GameObject)PrefabUtility.InstantiatePrefab(rocks[idx], wg);
                go.name = "바위_물속_" + inWater;
                go.transform.position = new Vector3(x, SampleH(x, z) - 0.1f, z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.1f, (float)rnd.NextDouble());
                pts.Add(new Vector2(x, z));
                inWater++;
            }

            // 갈대 — 물가선 얕은 띠 (h 0.05~0.65)
            var reeds = new[] { LoadVeg("Reed_갈대_Phragmites_1_VR"), LoadVeg("Reed_갈대_Phragmites_2_VR") };
            int reed = 0; attempts = 0;
            while (reed < 42 && attempts++ < 4000)
            {
                float x = Mathf.Lerp(-28f, 36f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(-1f, 48f, (float)rnd.NextDouble());
                float h = SampleH(x, z);
                if (h < 0.05f || h > 0.65f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(PadX, PadZ)) < 6.5f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(-17f, 8f)) < 4f) continue;
                if (Blocked(x, z, pts, 1.7f)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(reeds[rnd.Next(2)], wg);
                go.name = "갈대_" + reed;
                go.transform.position = new Vector3(x, h - 0.05f, z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.3f, (float)rnd.NextDouble());
                pts.Add(new Vector2(x, z));
                reed++;
            }

            // 수련 — 물가 가까운 얕은 물 군집. 못 중앙(별 반사)은 비운다
            var lilyBlooms = new List<GameObject>();
            var lilyLeaves = new List<GameObject>();
            for (int i = 1; i <= 4; i++)
            {
                var b = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SeyeonjeongPavilion/Prefabs/SM_NymphaeaTetragona_" + i + ".prefab");
                if (b != null) lilyBlooms.Add(b);
                var l = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SeyeonjeongPavilion/Prefabs/SM_NymphaeaTetragona_Leaf_" + i + ".prefab");
                if (l != null) lilyLeaves.Add(l);
            }
            if (lilyLeaves.Count == 0) lilyLeaves = lilyBlooms;
            int lily = 0;
            float[][] lilyClusters =
            {
                new[] { -13f, 23f }, new[] { -18f, 17f }, new[] { 14f, 7f }, new[] { 22f, 13f },
                new[] { 24f, 22f }, new[] { -1f, 8f }, new[] { 10f, 29f },
            };
            foreach (var c in lilyClusters)
            {
                int n = 4 + rnd.Next(3);
                for (int i = 0; i < n; i++)
                {
                    float x = c[0] + Jitter(rnd, 2.4f), z = c[1] + Jitter(rnd, 2.4f);
                    if (PondSd(x, z) > -0.6f) continue;   // 물속만
                    var pool = rnd.NextDouble() < 0.3 && lilyBlooms.Count > 0 ? lilyBlooms : lilyLeaves;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pool[rnd.Next(pool.Count)], wg);
                    go.name = "수련_" + lily++;
                    go.transform.position = new Vector3(x, WaterLevel, z);
                    go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                    go.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.4f, (float)rnd.NextDouble());
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[견우마을] 물가 배치: 바위 물가 {shore} + 물속 {inWater}, 갈대 {reed}, 수련 {lily}");
        }

        // ── 헬퍼 ────────────────────────────────────────────
        static (Vector2 pt, Vector2 tang) PointOnPath(float dist)
        {
            float acc = 0f;
            for (int i = 1; i < PathPts.Length; i++)
            {
                float seg = Vector2.Distance(PathPts[i - 1], PathPts[i]);
                if (acc + seg >= dist)
                {
                    float t = (dist - acc) / seg;
                    return (Vector2.Lerp(PathPts[i - 1], PathPts[i], t),
                            (PathPts[i] - PathPts[i - 1]).normalized);
                }
                acc += seg;
            }
            return (PathPts[PathPts.Length - 1],
                    (PathPts[PathPts.Length - 1] - PathPts[PathPts.Length - 2]).normalized);
        }

        static float PathD(float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 1; i < PathPts.Length; i++)
            {
                Vector2 a = PathPts[i - 1], b = PathPts[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(x, z) - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(new Vector2(x, z), a + ab * t));
            }
            return best;
        }

        static float PondSd(float x, float z) => GyeonuVillageBuilder.PondSignedDist(x, z);

        static bool NearHut(float x, float z, float m)
        {
            foreach (var h in Huts)
                if (Vector2.Distance(new Vector2(x, z), new Vector2((float)h[1], (float)h[2])) < m)
                    return true;
            return false;
        }

        static void Plant(Transform parent, GameObject prefab, string name,
            float x, float h, float z, System.Random rnd, float sMin, float sMax)
        {
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = new Vector3(x, h - 0.06f, z);
            go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            go.transform.localScale = Vector3.one * Mathf.Lerp(sMin, sMax, (float)rnd.NextDouble());
        }

        static GameObject LoadVeg(string name)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(VegPath + name + ".prefab");
            if (p == null) Debug.LogError("[견우마을] 식생 프리팹 없음: " + name);
            return p;
        }

        static float SampleH(float x, float z)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var p = t.transform.position; var s = t.terrainData.size;
                if (x >= p.x && x <= p.x + s.x && z >= p.z && z <= p.z + s.z)
                    return t.SampleHeight(new Vector3(x, 0f, z)) + p.y;
            }
            return GyeonuVillageBuilder.GroundHeight(Mathf.Clamp(x, -49f, 49f), Mathf.Clamp(z, -49f, 49f));
        }

        static float SlopeDegT(float x, float z)
        {
            float sx = SampleH(x + 1f, z) - SampleH(x - 1f, z);
            float sz = SampleH(x, z + 1f) - SampleH(x, z - 1f);
            return Mathf.Atan(0.5f * Mathf.Sqrt(sx * sx + sz * sz)) * Mathf.Rad2Deg;
        }

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

        static GameObject EnsureGroup(string name) =>
            SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name)
            ?? new GameObject(name);

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
