using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 서고 생성 (2026-08-15 v4 — **관측실 씬 통합**, 멱등).
    ///
    /// 씬 통합: 서고는 이제 Gyeonu_Observatory 씬 안의 루트 "서고"다 (관측실 루트와 별개).
    /// 위층 작업실 바로 아래 실제 좌표에 있으므로 두 층의 정합 문제가 원천적으로 사라진다.
    /// 옛 Gyeonu_Archive.unity는 확인용으로 남겨 둔다 (사용 안 함 — 삭제는 사용자가).
    ///
    /// 층간 연결 (2026-08-15 설정 변경): 무너진 계단이 아니라 **바닥 비밀문 + 삭은 줄사다리**.
    ///   - 작업실 바닥 서편의 작은 문(0.9×0.9, ObservatoryBuilder가 마루에 구멍만 남긴다).
    ///     평소 닫혀 있어 마루와 구분이 안 되고, 문틈으로 아래층 등불 빛이 가늘게 샌다
    ///   - 문짝은 TrapdoorLid(클릭 개폐, 부드러운 경첩 회전, 열리면 하단 안내 문구).
    ///     같은 씬이므로 위·아래에서 보는 문 상태는 저절로 동기화된다
    ///   - 문 밑 걸쇠에 줄사다리 상부가 끊긴 채 매달려 있고, 나머지는 서고 바닥에 쌓여 있다
    ///     (RopeLadderBuilder의 줄사다리_끊어짐 프리팹). 수령은 이 문을 몰랐다 —
    ///     그래서 위층 관측실을 스무 해 동안 몰랐다
    ///   - 추락 방지: 구멍에 보이지 않는 판(Ignore Raycast 레이어 — 클릭은 통과, 보행만 차단)
    ///
    /// 서고 본체: 하나의 공간 (Room00~02는 재질·조명 문법만 참고, 구조 복사 금지 — 사용자 확정).
    ///   다리 공사 자재 창고였다가 수령의 은닉처가 된 암반 공간. 바닥 -6.6, 천장 2.6
    ///   (작업실 3.6보다 낮고 답답하게). 동쪽 = 은닉처(밝은 웅덩이 1), 천장 서편 = 비밀문·줄사다리,
    ///   남서쪽으로 꺾인 구석(천장 2.1 + 어귀 암반 리브) = 선아 자리 (위층 빛이 닿지 않는다).
    ///
    /// 비밀 통로 (관아 집무실 → 동벽 문): 꺾임 5회·계단 8단×5(하강 6.6m)·보행 약 40m.
    /// 암문 통로(거친 막돌)와 대비되는 다듬은 층쌓기 석벽 + 목재 늑재 + 판재 천장 + 정돈된 등롱.
    ///
    /// 조명은 Room00 문법: 은닉처 웅덩이 1(그림자) + 중앙 채움 0.3 + 통로 등롱 + 선아 등불(줄사다리 곁)
    /// + 문틈 불빛(문 바로 밑 — 위층에서 문틈이 빛나 보이게 하는 근거리 광).
    /// 앰비언트·안개는 관측실 씬 설정을 그대로 쓴다 (여기서 건드리지 않는다).
    /// </summary>
    public static class ArchiveBuilder
    {
        const string RootName = "서고";
        const string MarkerRootName = "서고_마커";
        const string ModelDir = "Assets/_Project/Gyeonu/Art/Models/Archive";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Archive";
        const string PrefabDir = "Assets/_Project/Gyeonu/Prefabs/Observatory";
        const string KMTex = "Assets/KimMyeonggwanHouse/Texture/";
        const string SWTex = "Assets/Soswaewon/Textures/Buildings/";
        const string ALPTex = "Assets/BK_AlchemistHouse/Textures/Surfaces/";  // ALP Alchemist House (폴더명 ≠ 팩 이름)

        // ── 비밀문 구멍 (ObservatoryBuilder의 ox·oz와 반드시 일치) ──
        const float DX0 = 15.1f, DX1 = 16.0f;
        const float DZ0 = 32.5f, DZ1 = 33.4f;
        const float ObsFloorY = -3.06f;        // 작업실 마루 윗면
        const float ObsFloorBot = -3.36f;      // 작업실 마루 밑면

        // ── 서고 (단일 공간, ㄱ자 평면) ──
        const float FY = -6.60f;               // 서고 바닥 — 줄사다리 한 층 분량(3.54m) 아래
        const float CH = 2.60f;                // 천장고 (작업실 3.6보다 낮게)
        const float WTk = 0.40f;
        const float HX0 = 13.9f, HX1 = 21.9f;  // 본채 내부 x (8.0)
        const float HZ0 = 30.7f, HZ1 = 35.3f;  // 본채 내부 z (4.6)
        const float KX0 = HX0, KX1 = 16.7f;    // 꺾인 구석 (남서)
        const float KZ0 = 27.5f, KZ1 = HZ0;
        const float KH = 2.10f;

        // ── 비밀 통로 ──
        const float W = 1.7f;
        const float H = 2.30f;
        const float T = 0.30f;
        const float StepRise = 0.165f;         // 40단 × 0.165 = 6.60 → 입구 바닥이 정확히 y0
        const float StepRun = 0.32f;

        const float UvRock = 0.40f;
        const float UvCut = 0.31f;
        const float UvFloor = 0.34f;
        const float UvWood = 0.55f;

        static readonly List<Mesh> tempMeshes = new List<Mesh>();
        static readonly List<(Vector3 c, Vector3 s, Quaternion r)> colBoxes = new List<(Vector3, Vector3, Quaternion)>();

        // ─────────────────────────────────────────────────────
        [MenuItem("Tools/이문록/서고 생성")]
        public static void Build()
        {
            // 통합 이후 서고는 관측실 씬 안에 짓는다
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gyeonu_Observatory")
            {
                Debug.LogError("[서고] 활성 씬이 Gyeonu_Observatory가 아닙니다 — 통합 씬을 먼저 열고 실행하세요");
                return;
            }
            tempMeshes.Clear();
            colBoxes.Clear();
            EnsureFolder(ModelDir); EnsureFolder(MatDir);

            var prevMarkers = new Dictionary<string, (Vector3 p, Quaternion r)>();
            var oldMarkers = FindRootIncludingInactive(MarkerRootName);
            if (oldMarkers != null)
            {
                foreach (Transform ch in oldMarkers.transform) prevMarkers[ch.name] = (ch.position, ch.rotation);
                Object.DestroyImmediate(oldMarkers);
            }
            for (GameObject g; (g = FindRootIncludingInactive(RootName)) != null;) Object.DestroyImmediate(g);

            // ── 머티리얼 (외부 팩 텍스처 참조만, 에셋은 우리 폴더에 생성) ──
            var texRockBC = LoadTex(KMTex + "T_GidanStone01A_BC.png");
            var texRockNM = LoadTex(KMTex + "T_GidanStone01A_NM.png");
            var texFloorBC = LoadTex(KMTex + "T_Stone02A_BC.png");
            var texFloorNM = LoadTex(KMTex + "T_Stone02A_NM.png");
            var texCutBC = LoadTex(ALPTex + "StoneWall01.png");
            var texCutNM = LoadTex(ALPTex + "StoneWall01_n.png");
            var texCutAO = LoadTex(ALPTex + "StoneWall01_o.png");
            var texWoodBC = LoadTex(SWTex + "T_Wood_BC.png");
            var texWoodNM = LoadTex(SWTex + "T_Wood_NM.png");
            var texMaruBC = LoadTex(KMTex + "T_Floor01A_BC.png");
            var texMaruNM = LoadTex(KMTex + "T_Floor01A_NM.png");

            var matRock = Mat("M_서고_암반", new Color(0.40f, 0.41f, 0.45f), 0f, 0.14f, texRockBC, texRockNM);
            var matFloor = Mat("M_서고_바닥", new Color(0.33f, 0.32f, 0.33f), 0f, 0.18f, texFloorBC, texFloorNM);
            var matCut = Mat("M_서고_통로석벽", new Color(0.52f, 0.47f, 0.40f), 0f, 0.15f, texCutBC, texCutNM, null, texCutAO);
            var matPFloor = Mat("M_서고_통로바닥", new Color(0.42f, 0.39f, 0.34f), 0f, 0.20f, texFloorBC, texFloorNM);
            var matWood = Mat("M_서고_통로목재", new Color(0.42f, 0.34f, 0.26f), 0f, 0.16f, texWoodBC, texWoodNM);
            var matMaruC = Mat("M_서고_통로천장널", new Color(0.44f, 0.38f, 0.31f), 0f, 0.13f, texMaruBC, texMaruNM);
            var matShoring = Mat("M_서고_동바리", new Color(0.30f, 0.26f, 0.21f), 0f, 0.10f, texWoodBC, texWoodNM);
            var matIron = Mat("M_서고_흑철", new Color(0.09f, 0.09f, 0.10f), 0.6f, 0.42f);
            var matFlame = Mat("M_서고_불꽃", new Color(1f, 0.55f, 0.22f), 0f, 0.5f, null, null,
                new Color(2.6f, 1.15f, 0.35f));
            // 문짝은 작업실 마루와 같은 재질 — "바닥 판자와 구분이 안 되게"
            var matObsMaru = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Gyeonu/Art/Materials/Observatory/M_관측실_마루.mat");
            if (matObsMaru == null) { matObsMaru = matMaruC; Debug.LogWarning("[서고] 관측실 마루 재질 없음 — 통로천장널로 대체"); }

            var root = new GameObject(RootName);
            var grpLight = new GameObject("조명"); grpLight.transform.SetParent(root.transform, false);

            var passStone = new List<CombineInstance>();
            var passFloor = new List<CombineInstance>();
            var passWood = new List<CombineInstance>();
            var passCeil = new List<CombineInstance>();
            var hallRock = new List<CombineInstance>();
            var hallFloor = new List<CombineInstance>();
            var shoring = new List<CombineInstance>();

            void Slab(List<CombineInstance> list, float x0, float x1, float y0, float y1, float z0, float z1, float uv, Vector2? uvOff = null)
            {
                if (x1 - x0 < 0.004f || y1 - y0 < 0.004f || z1 - z0 < 0.004f) return;
                AddBox(list, new Vector3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2),
                    new Vector3(x1 - x0, y1 - y0, z1 - z0), Quaternion.identity, uv, uvOff);
            }

            // ═════════════════════════════════════════════════
            // 1. 비밀 통로 — 관아 집무실 뒤편 → 서고 동벽 문 (평면 경로는 v2와 동일)
            //    끝이 동벽 바깥면(22.3, 32.6)에 닿도록 역산해 둔 경로 — 구간을 바꾸면 패딩으로 되맞출 것
            // ═════════════════════════════════════════════════
            var lanterns = new List<Vector3>();
            float totalDrop = 40 * StepRise;              // 6.60
            Vector3 pos = new Vector3(43.01f, 0, 41.93f);
            Vector3 dir = Vector3.back;
            float fy = FY + totalDrop;                    // = 0.00

            float ribDist = 0f;
            float lampDist = 3.2f;

            void Rib(Vector3 at, Vector3 d, float floorY)
            {
                var right = Vector3.Cross(Vector3.up, d);
                var rot = Quaternion.LookRotation(d);
                AddBox(passWood, at + right * (W / 2 - 0.055f) + Vector3.up * (floorY + H / 2), new Vector3(0.11f, H, 0.16f), rot, UvWood);
                AddBox(passWood, at - right * (W / 2 - 0.055f) + Vector3.up * (floorY + H / 2), new Vector3(0.11f, H, 0.16f), rot, UvWood);
                AddBox(passWood, at + Vector3.up * (floorY + H - 0.075f), new Vector3(W, 0.15f, 0.16f), rot, UvWood);
            }

            void Straight(float len)
            {
                var rot = Quaternion.LookRotation(dir);
                var right = Vector3.Cross(Vector3.up, dir);
                AddBox(passFloor, pos + dir * (len / 2) + Vector3.up * (fy - T / 2), new Vector3(W + 2 * T, T, len), rot, UvFloor);
                AddBox(passCeil, pos + dir * (len / 2) + Vector3.up * (fy + H + T / 2), new Vector3(W + 2 * T, T, len), rot, UvWood);
                AddBox(passStone, pos + dir * (len / 2) + right * (W / 2 + T / 2) + Vector3.up * (fy + H / 2), new Vector3(T, H, len), rot, UvCut);
                AddBox(passStone, pos + dir * (len / 2) - right * (W / 2 + T / 2) + Vector3.up * (fy + H / 2), new Vector3(T, H, len), rot, UvCut);
                float step = 0.1f;
                for (float t2 = 0; t2 < len; t2 += step)
                {
                    ribDist += step; lampDist += step;
                    if (ribDist >= 2.3f)
                    {
                        ribDist = 0f;
                        var at = pos + dir * t2;
                        Rib(at, dir, fy);
                        if (lampDist >= 4.6f)
                        {
                            lampDist = 0f;
                            lanterns.Add(at + Vector3.up * (fy + H - 0.42f));
                        }
                    }
                }
                pos += dir * len;
            }

            void Stairs(int n)
            {
                float len = n * StepRun, drop = n * StepRise;
                var rot = Quaternion.LookRotation(dir);
                var right = Vector3.Cross(Vector3.up, dir);
                AddBox(passStone, pos + dir * (len / 2) + right * (W / 2 + T / 2) + Vector3.up * (fy + (H - drop) / 2), new Vector3(T, H + drop, len), rot, UvCut);
                AddBox(passStone, pos + dir * (len / 2) - right * (W / 2 + T / 2) + Vector3.up * (fy + (H - drop) / 2), new Vector3(T, H + drop, len), rot, UvCut);
                float angDeg = Mathf.Atan2(drop, len) * Mathf.Rad2Deg;
                AddBox(passCeil, pos + dir * (len / 2) + Vector3.up * (fy - drop / 2 + H + T / 2),
                    new Vector3(W + 2 * T, T, Mathf.Sqrt(len * len + drop * drop) + 0.3f), rot * Quaternion.Euler(angDeg, 0, 0), UvWood);
                for (int i = 0; i < n; i++)
                {
                    float topY = fy - StepRise * (i + 1);
                    float bottomY = fy - drop - T;
                    AddBox(passFloor, pos + dir * (StepRun * (i + 0.5f)) + Vector3.up * (bottomY + (topY - bottomY) / 2),
                        new Vector3(W, topY - bottomY, StepRun + 0.02f), rot, UvFloor);
                }
                Rib(pos + dir * 0.10f, dir, fy);
                // 계단 중앙 등롱 — 이게 없으면 꺾임 사이 하강 구간이 통째로 어둠 (v1 실측)
                lanterns.Add(pos + dir * (len / 2) + Vector3.up * (fy - drop / 2 + H - 0.40f));
                ribDist = 0f; lampDist = 0f;
                pos += dir * len;
                fy -= drop;
            }

            void Turn(bool rightTurn)
            {
                var right = Vector3.Cross(Vector3.up, dir);
                var nd = rightTurn ? right : -right;
                var cc = pos + dir * (W / 2);
                AddBox(passFloor, cc + Vector3.up * (fy - T / 2), new Vector3(W + 2 * T, T, W + 2 * T), Quaternion.identity, UvFloor);
                AddBox(passCeil, cc + Vector3.up * (fy + H + T / 2), new Vector3(W + 2 * T, T, W + 2 * T), Quaternion.identity, UvWood);
                AddBox(passStone, cc + dir * (W / 2 + T / 2) + Vector3.up * (fy + H / 2), new Vector3(W + 2 * T, H, T), Quaternion.LookRotation(dir), UvCut);
                AddBox(passStone, cc - nd * (W / 2 + T / 2) + Vector3.up * (fy + H / 2), new Vector3(W + 2 * T, H, T), Quaternion.LookRotation(nd), UvCut);
                lanterns.Add(cc + Vector3.up * (fy + H - 0.42f));
                lampDist = 0f;
                pos = cc + nd * (W / 2);
                dir = nd;
            }

            // ── 입구 (관아 집무실 뒤편으로 통하는 문) ──
            {
                float ey = fy;
                AddBox(passStone, new Vector3(pos.x, ey + H / 2 + T, pos.z + 0.30f), new Vector3(W + 2 * T, H + 2 * T, T), Quaternion.identity, UvCut);
                AddBox(passWood, new Vector3(pos.x + 0.80f, ey + 1.05f, pos.z + 0.10f), new Vector3(0.18f, 2.10f, 0.24f), Quaternion.identity, UvWood);
                AddBox(passWood, new Vector3(pos.x - 0.80f, ey + 1.05f, pos.z + 0.10f), new Vector3(0.18f, 2.10f, 0.24f), Quaternion.identity, UvWood);
                AddBox(passWood, new Vector3(pos.x, ey + 2.16f, pos.z + 0.10f), new Vector3(1.78f, 0.22f, 0.24f), Quaternion.identity, UvWood);
                // 문 안 어둠 (집무실 쪽 포털 자리)
                var darkGo = NewChild(root, "관아문_어둠");
                var darkList = new List<CombineInstance>();
                AddBox(darkList, new Vector3(pos.x, ey + 1.02f, pos.z + 0.20f), new Vector3(1.44f, 2.04f, 0.06f), Quaternion.identity, 1f);
                MeshGO(darkGo, Save(Combine(darkList), "서고_관아문어둠"),
                    Mat("M_서고_어둠", new Color(0.010f, 0.010f, 0.012f), 0f, 0.02f), false);
            }

            // ── 경로: 꺾임 5회, 계단 8단×5 = 40단 ──
            Straight(3.0f); Stairs(8); Straight(2.08f);
            Turn(true);                                    // → -X
            Straight(1.6f); Stairs(8); Straight(2.12f);
            Turn(false);                                   // → -Z
            Straight(1.4f); Stairs(8); Straight(2.12f);
            Turn(true);                                    // → -X
            Straight(1.6f); Stairs(8); Straight(2.12f);
            Turn(true);                                    // → +Z
            Straight(1.2f); Stairs(8); Straight(1.48f);
            Turn(false);                                   // → -X
            Straight(3.9f);                                // 끝 = 동벽 바깥면 (22.3, 32.6)

            if (Mathf.Abs(fy - FY) > 0.02f)
                Debug.LogWarning($"[서고] 통로 하강량 불일치: fy={fy:F3}, 기대 {FY}");
            // pos는 평면 추적값(y 항상 0) — y는 비교에서 뺀다
            if ((pos - new Vector3(HX1 + WTk, 0f, 32.6f)).magnitude > 0.35f)
                Debug.LogWarning($"[서고] 통로 끝 위치 어긋남: {pos}");

            // ═════════════════════════════════════════════════
            // 2. 서고 — 단일 공간 (본채 + 남서쪽으로 꺾인 구석)
            // ═════════════════════════════════════════════════
            float cy = FY + CH;              // 본채 천장 밑면 -4.00
            float cyT = cy + 0.35f;          // 본채 천장 윗면 -3.65 (작업실 마루 밑 -3.36과 0.29 암반 사이)
            float kcy = FY + KH;             // 구석 천장 밑면 -4.50
            const float DoorZ0 = 31.75f, DoorZ1 = 33.45f, DoorH = 2.10f;

            // 바닥 — 한 장 (단일 공간)
            Slab(hallFloor, HX0 - WTk, HX1 + WTk, FY - 0.30f, FY, KZ0 - WTk, HZ1 + WTk, UvFloor);
            // 북벽
            Slab(hallRock, HX0 - WTk, HX1 + WTk, FY, cyT, HZ1, HZ1 + WTk, UvRock);
            // 동벽 (관아 통로 문)
            Slab(hallRock, HX1, HX1 + WTk, FY, cyT, HZ0 - WTk, DoorZ0, UvRock);
            Slab(hallRock, HX1, HX1 + WTk, FY, cyT, DoorZ1, HZ1 + WTk, UvRock);
            Slab(hallRock, HX1, HX1 + WTk, FY + DoorH, cyT, DoorZ0, DoorZ1, UvRock);
            // 본채 남벽 (구석 동쪽 ~ 동벽)
            Slab(hallRock, KX1, HX1 + WTk, FY, cyT, HZ0 - WTk, HZ0, UvRock);
            // 서벽 — 본채와 구석이 공유하는 한 면 (단일 공간의 증거)
            Slab(hallRock, HX0 - WTk, HX0, FY, cyT, KZ0 - WTk, HZ1 + WTk, UvRock);
            // 꺾인 구석: 남벽·동벽 + 어귀 암반 리브 (기둥 삼아 안 판 바위 — 방을 나누지 않으면서 시선·빛을 꺾는다)
            Slab(hallRock, KX0 - WTk, KX1 + WTk, FY, cyT, KZ0 - WTk, KZ0, UvRock);
            Slab(hallRock, KX1, KX1 + WTk, FY, cyT, KZ0 - WTk, HZ0, UvRock);
            Slab(hallRock, HX0, 14.9f, FY, cy, 30.1f, HZ0, UvRock);
            // 본채 천장 (비밀문 구멍만 뚫는다) — 위층 작업실 마루(-3.36)와는 0.3m 암반 층
            Slab(hallRock, HX0 - WTk, DX0, cy, cyT, HZ0, HZ1 + WTk, UvRock);
            Slab(hallRock, DX1, HX1 + WTk, cy, cyT, HZ0, HZ1 + WTk, UvRock);
            Slab(hallRock, DX0, DX1, cy, cyT, HZ0, DZ0, UvRock);
            Slab(hallRock, DX0, DX1, cy, cyT, DZ1, HZ1 + WTk, UvRock);
            // 구석 천장 (한 단 낮게 — 윗면이 본채 천장 밑면과 맞닿아 틈이 없다)
            Slab(hallRock, KX0 - WTk, KX1 + WTk, kcy, cy, KZ0 - WTk, HZ0, UvRock);

            // 동벽 문틀 (수령이 손본 잘 짠 문얼굴)
            AddBox(passWood, new Vector3(HX1 + WTk / 2, FY + DoorH / 2, DoorZ0 + 0.07f), new Vector3(WTk + 0.10f, DoorH, 0.16f), Quaternion.identity, UvWood);
            AddBox(passWood, new Vector3(HX1 + WTk / 2, FY + DoorH / 2, DoorZ1 - 0.07f), new Vector3(WTk + 0.10f, DoorH, 0.16f), Quaternion.identity, UvWood);
            AddBox(passWood, new Vector3(HX1 + WTk / 2, FY + DoorH + 0.09f, (DoorZ0 + DoorZ1) / 2), new Vector3(WTk + 0.10f, 0.18f, DoorZ1 - DoorZ0), Quaternion.identity, UvWood);

            // 목재 동바리 (자재 창고의 뼈대) — 보 2개가 z 방향으로 천장을 받친다
            float beamTop = cy, beamBot = cy - 0.22f;
            foreach (float bx in new[] { 18.6f, 20.4f })
            {
                Slab(shoring, bx - 0.09f, bx + 0.09f, beamBot, beamTop, HZ0, HZ1, UvWood);
                Slab(shoring, bx - 0.08f, bx + 0.08f, FY, beamBot, HZ0 + 0.06f, HZ0 + 0.22f, UvWood);
                Slab(shoring, bx - 0.08f, bx + 0.08f, FY, beamBot, HZ1 - 0.22f, HZ1 - 0.06f, UvWood);
            }

            // ═════════════════════════════════════════════════
            // 3. 비밀문 — 문틀 목재 라이닝 + 문짝(TrapdoorLid) + 추락 방지 + 문틈 불빛
            // ═════════════════════════════════════════════════
            // 구멍 목재 라이닝: 서고 천장 윗면(-3.65) ~ 작업실 마루 윗면(-3.06)을 잇는 통
            Slab(passWood, DX0 - 0.08f, DX0, -3.66f, ObsFloorY, DZ0 - 0.08f, DZ1 + 0.08f, UvWood);
            Slab(passWood, DX1, DX1 + 0.08f, -3.66f, ObsFloorY, DZ0 - 0.08f, DZ1 + 0.08f, UvWood);
            Slab(passWood, DX0, DX1, -3.66f, ObsFloorY, DZ0 - 0.08f, DZ0, UvWood);
            Slab(passWood, DX0, DX1, -3.66f, ObsFloorY, DZ1, DZ1 + 0.08f, UvWood);
            float doorCx = (DX0 + DX1) / 2, doorCz = (DZ0 + DZ1) / 2;   // 15.55, 32.95
            {
                // 문짝 — 마루와 같은 재질, 구멍보다 사방 1.5cm 작아 틈이 보인다 (빛 새는 단서)
                var lidParent = NewChild(root, "비밀문");
                lidParent.transform.position = new Vector3(doorCx, ObsFloorY - 0.0225f, doorCz);
                var lid = new GameObject("문짝");
                lid.transform.SetParent(lidParent.transform, false);
                // ⚠️ BuildBoxMesh는 노멀을 안 만든다 (Combine 경로가 계산해 줬음) — 단독 메시는 직접 계산
                var rawLid = BuildBoxMesh(new Vector3(0.87f, 0.045f, 0.87f), UvFloor, new Vector2(0.31f, 0.47f));
                rawLid.RecalculateNormals(); rawLid.RecalculateTangents(); rawLid.RecalculateBounds();
                var lidMesh = Save(rawLid, "서고_비밀문짝");
                lid.AddComponent<MeshFilter>().sharedMesh = lidMesh;
                lid.AddComponent<MeshRenderer>().sharedMaterial = matObsMaru;
                lid.AddComponent<BoxCollider>().size = new Vector3(0.87f, 0.045f, 0.87f);
                var td = lid.AddComponent<IMUNROK.Gyeonu.TrapdoorLid>();
                td.displayName = "바닥의 판자문";
                td.pivotInParent = new Vector3(0, 0.02f, 0.435f);   // 북변 경첩
                td.axisInParent = Vector3.right;
                td.openAngle = 110f;                                 // 북쪽으로 젖혀 올라간다
                td.duration = 1.1f;
                td.openMessageUpper = "내려갈 방법이 없다. 다른 길을 찾아야겠다.";
                td.openMessageLower = "위는 아까 그 관측실이다. 그 바닥 밑에 이 서고가 숨어 있었다.";
                td.floorSplitY = -3.5f;
                // 추락 방지 — 보이지 않는 판 (Ignore Raycast: 클릭 레이는 통과, 보행만 받는다)
                var guard = new GameObject("추락방지판");
                guard.transform.SetParent(lidParent.transform, false);
                guard.transform.position = new Vector3(doorCx, ObsFloorY - 0.11f, doorCz);
                guard.AddComponent<BoxCollider>().size = new Vector3(1.02f, 0.10f, 1.02f);
                guard.layer = 2;
                guard.isStatic = true;
            }
            // 줄사다리 (끊어짐) — 걸쇠가 문 밑 북변에 걸려 있고, 잔해는 서고 바닥에 쌓인다.
            // 선아는 마지막에 "바닥에서 뭔가에 걸려" 이 문을 발견했다 — 그 뭔가가 이 걸쇠다
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/줄사다리_끊어짐.prefab");
                if (prefab == null)
                {
                    RopeLadderBuilder.Build();
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/줄사다리_끊어짐.prefab");
                }
                if (prefab != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    inst.transform.SetParent(root.transform, false);
                    inst.transform.position = new Vector3(doorCx, -3.13f, DZ1 - 0.10f);   // 걸쇠 = 문 북변 밑
                    // 잔해 무더기 위 보행 콜라이더 (낮은 둔덕 — 밟고 지나갈 수 있게)
                    ColBox(root.transform, "줄사다리잔해_콜라이더",
                        new Vector3(doorCx + 0.12f, -3.13f + RopeLadderBuilder.PileDropY + 0.07f, DZ1 - 0.10f + 0.10f),
                        new Vector3(1.05f, 0.14f, 1.0f), Quaternion.identity);
                }
                else Debug.LogWarning("[서고] 줄사다리_끊어짐 프리팹을 찾지 못했습니다");
            }

            // ═════════════════════════════════════════════════
            // 4. 조명 — Room00 문법 (웅덩이 1 + 채움 1 + 등불들). 씬 앰비언트·안개는 관측실 소관
            // ═════════════════════════════════════════════════
            var grpLan = NewChild(grpLight, "통로_등롱");
            foreach (var lp in lanterns)
                Lantern(grpLan.transform, lp, matIron, matWood, matFlame);
            BareLight(grpLight.transform, "은닉처_웅덩이", new Vector3(20.2f, cy - 0.30f, 32.9f),
                new Color(1f, 0.72f, 0.42f), 4.2f, 6.5f, true);
            BareLight(grpLight.transform, "중앙_채움", new Vector3(17.8f, FY + 1.9f, 32.9f),
                new Color(0.75f, 0.78f, 0.85f), 0.30f, 8.0f, false);
            // 선아의 등불 — 꺾인 구석, 선아가 문서를 뒤지던 자리 (Furnisher가 같은 자리에 등불 소품을 놓는다).
            // 위층 문틈으로 새는 빛의 서사적 근원이고, 실제 문틈 광원은 아래의 문틈_불빛이 맡는다
            {
                var go = new GameObject("선아_등불");
                go.transform.SetParent(grpLight.transform, false);
                go.transform.position = new Vector3(14.3f, FY + 0.42f, 28.9f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.62f, 0.30f);
                l.intensity = 2.4f;
                l.range = 7.0f;
                l.shadows = LightShadows.Soft; l.shadowStrength = 0.6f;
                Prim(go.transform, "불씨", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.06f, matFlame);
            }
            // 문틈 불빛 — 문 바로 밑 통 안. 닫힌 문짝 둘레 1.5cm 틈으로만 새어 나가
            // 위층에서 "마루 틈이 가늘게 빛나는" 발견 단서가 된다
            BareLight(grpLight.transform, "문틈_불빛", new Vector3(doorCx, -3.38f, doorCz),
                new Color(1f, 0.62f, 0.30f), 1.6f, 2.2f, false);

            // ═════════════════════════════════════════════════
            // 5. 합성 메시 + 콜라이더 + 마커
            // ═════════════════════════════════════════════════
            MeshGO(NewChild(root, "통로_석벽"), Save(Combine(passStone), "서고_통로석벽"), matCut, false);
            MeshGO(NewChild(root, "통로_바닥"), Save(Combine(passFloor), "서고_통로바닥"), matPFloor, false);
            MeshGO(NewChild(root, "통로_목재"), Save(Combine(passWood), "서고_통로목재"), matWood, false);
            MeshGO(NewChild(root, "통로_천장널"), Save(Combine(passCeil), "서고_통로천장널"), matMaruC, false);
            MeshGO(NewChild(root, "서고_암반"), Save(Combine(hallRock), "서고_본실암반"), matRock, false);
            MeshGO(NewChild(root, "서고_바닥"), Save(Combine(hallFloor), "서고_본실바닥"), matFloor, false);
            MeshGO(NewChild(root, "동바리_목재"), Save(Combine(shoring), "서고_동바리"), matShoring, false);

            var colRoot = NewChild(root, "보행콜라이더");
            var colStruct = NewChild(colRoot, "구조");
            for (int i = 0; i < colBoxes.Count; i++)
                ColBox(colStruct.transform, $"box_{i:D3}", colBoxes[i].c, colBoxes[i].s, colBoxes[i].r);

            var markerRoot = new GameObject(MarkerRootName);
            void Marker(string name, Vector3 p, Quaternion r)
            {
                var m = new GameObject(name);
                m.transform.SetParent(markerRoot.transform, false);
                if (prevMarkers.TryGetValue(name, out var prev)) { m.transform.SetPositionAndRotation(prev.p, prev.r); }
                else m.transform.SetPositionAndRotation(p, r);
            }
            // 이 통로가 통하는 곳은 관아 '외부'(Gyeonu_Gwana)가 아니라 집무실(Gyeonu_GwanaOffice)이다.
            // 옛 이름(..._FromGwana / Exit_ToGwana)은 외부 씬과 헷갈려 2026-08-17 개명했다.
            Marker("SpawnPoint_FromGwanaOffice", new Vector3(43.01f, 0f, 40.9f), Quaternion.LookRotation(Vector3.back));
            Marker("Exit_ToGwanaOffice", new Vector3(43.01f, 0f, 41.85f), Quaternion.LookRotation(Vector3.forward));
            Marker("Spawn_Seona", new Vector3(14.5f, FY, 28.15f), Quaternion.LookRotation(Vector3.forward));
            Marker("Marker_은닉처", new Vector3(20.3f, FY, 32.9f), Quaternion.LookRotation(Vector3.left));

            foreach (var m in tempMeshes) if (m != null) Object.DestroyImmediate(m);
            tempMeshes.Clear();
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[서고] 생성 완료(v4 관측실 씬 통합) — 바닥 {FY}, 비밀문 ({doorCx:F2}, {doorCz:F2}), " +
                      $"콜라이더 박스 {colBoxes.Count}개, 등롱 {lanterns.Count}개, 삼각형 {CountTris(root):N0}");
        }

        // ── 등롱 (통로 천장걸이 — 나무 살 + 흑철 고리 + 불꽃) ──
        static void Lantern(Transform parent, Vector3 pos, Material iron, Material wood, Material flame)
        {
            var go = new GameObject("등롱");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            Prim(go.transform, "고리", PrimitiveType.Cylinder, new Vector3(0, 0.30f, 0), new Vector3(0.02f, 0.09f, 0.02f), iron);
            Prim(go.transform, "지붕", PrimitiveType.Cube, new Vector3(0, 0.185f, 0), new Vector3(0.24f, 0.03f, 0.24f), wood);
            Prim(go.transform, "살_1", PrimitiveType.Cube, new Vector3(0.095f, 0, 0.095f), new Vector3(0.025f, 0.34f, 0.025f), wood);
            Prim(go.transform, "살_2", PrimitiveType.Cube, new Vector3(-0.095f, 0, 0.095f), new Vector3(0.025f, 0.34f, 0.025f), wood);
            Prim(go.transform, "살_3", PrimitiveType.Cube, new Vector3(0.095f, 0, -0.095f), new Vector3(0.025f, 0.34f, 0.025f), wood);
            Prim(go.transform, "살_4", PrimitiveType.Cube, new Vector3(-0.095f, 0, -0.095f), new Vector3(0.025f, 0.34f, 0.025f), wood);
            Prim(go.transform, "받침", PrimitiveType.Cube, new Vector3(0, -0.165f, 0), new Vector3(0.22f, 0.03f, 0.22f), wood);
            Prim(go.transform, "불꽃", PrimitiveType.Sphere, new Vector3(0, -0.10f, 0), Vector3.one * 0.07f, flame);
            var l = new GameObject("불빛").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0, -0.06f, 0);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.68f, 0.38f);
            l.intensity = 1.55f;
            l.range = 5.2f;
        }

        static void BareLight(Transform parent, string name, Vector3 pos, Color col, float intensity, float range, bool shadow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = col;
            l.intensity = intensity;
            l.range = range;
            if (shadow) { l.shadows = LightShadows.Soft; l.shadowStrength = 0.55f; }
        }

        static void Prim(Transform parent, string name, PrimitiveType t, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ── 메시 유틸 (ObservatoryBuilder와 동일 규약) ──
        static void AddBox(List<CombineInstance> list, Vector3 center, Vector3 size, Quaternion rot, float uvScale, Vector2? uvOff = null)
        {
            colBoxes.Add((center, size, rot));
            var off = uvOff ?? new Vector2(
                Mathf.Abs(center.x * 0.173f + center.z * 0.331f) % 1f,
                Mathf.Abs(center.y * 0.257f + center.z * 0.119f) % 1f);
            var m = BuildBoxMesh(size, uvScale, off);
            tempMeshes.Add(m);
            list.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(center, rot, Vector3.one) });
        }

        static Mesh BuildBoxMesh(Vector3 s, float uv, Vector2 off)
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

        static void ColBox(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
        }

        static GameObject NewChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static void MeshGO(GameObject go, Mesh mesh, Material mat, bool collider)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
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
            if (t == null) Debug.LogWarning($"[서고] 텍스처 없음: {path}");
            return t;
        }

        static Material Mat(string name, Color c, float metallic, float smooth,
            Texture2D baseMap = null, Texture2D normal = null, Color? emission = null, Texture2D occlusion = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            m.SetTexture("_BaseMap", baseMap);
            m.SetTexture("_BumpMap", normal);
            if (normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", occlusion);
            if (occlusion != null) { m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_OcclusionStrength", 1f); }
            else m.DisableKeyword("_OCCLUSIONMAP");
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject FindRootIncludingInactive(string name)
        {
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.name == name) return g;
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
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
