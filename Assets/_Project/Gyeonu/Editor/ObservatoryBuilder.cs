using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관측실(Gyeonu_Observatory) 방 구조·암문 통로·기본 조명 절차 생성 (2026-08-10, 멱등).
    ///
    /// 구성: 암문(입구) → 꺾임 4회·계단 하강 2구간의 좁은 통로(약 42m, 폭 1.8 × 천장 2.35)
    ///       → 지름 12m 돔 관측실 (석벽 드럼 3.4m + 목조 서까래 돔, 정점 6.6m).
    ///       마지막 모퉁이를 돌면 돔이 트이는 대비가 연출 핵심 — 통로는 의도적으로 낮고 좁다.
    ///
    /// 석재는 김명관 기단석(GidanStone01A)·화강암(Stone02A), 돔 판재는 소쇄원 목재 텍스처 재사용
    /// (원본 폴더는 건드리지 않고 참조만, 머티리얼은 Gyeonu/Art/Materials/Observatory에 생성).
    ///
    /// 모든 벽·바닥·돔은 월드 크기 비례 UV의 커스텀 메시를 용도별 5개로 합성 —
    /// 프리미티브 큐브의 면당 0..1 UV 늘어남(혼천의 받침에서 실측한 얼룩 원인)을 피하고
    /// 드로우콜을 줄인다. 보행 충돌은 합성 메시의 MeshCollider(정적)로 처리.
    ///
    /// 조명: 등잔·사방등 소수(포인트) + 매우 어두운 평면 앰비언트 + 선형 안개.
    /// 금속(혼천의)이 빛나려면 반사 대상이 필요 — [관측실 프로브 굽기] 메뉴로
    /// 방 내부를 커스텀 큐브맵에 구워 리플렉션 프로브를 세운다 (혼천의 프리뷰와 동일 기법,
    /// URP 렌더그래프 충돌 때문에 delayCall로 면당 1프레임씩 굽는다).
    /// </summary>
    public static class ObservatoryBuilder
    {
        const string RootName = "관측실";
        const string MarkerRootName = "관측실_마커";
        const string ModelDir = "Assets/_Project/Gyeonu/Art/Models/Observatory";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Observatory";
        const string TexDir = "Assets/_Project/Gyeonu/Art/Textures/Observatory";
        const string PrefabDir = "Assets/_Project/Gyeonu/Prefabs/Observatory";
        const string KMTex = "Assets/KimMyeonggwanHouse/Texture/";
        const string SWTex = "Assets/Soswaewon/Textures/Buildings/";

        // 통로 단면 — VR 눈높이 1.7 기준: 답답하되 부딪히지는 않게
        const float W = 1.8f;   // 폭
        const float H = 2.35f;  // 천장고
        const float T = 0.30f;  // 벽 두께

        // 방 (돔 관측실)
        const float R = 6.0f;      // 반지름 = 지름 12m
        const float WallH = 3.4f;  // 석벽 드럼 높이
        const int Sides = 16;      // 드럼 다각형 변 수 = 서까래 기수의 절반

        const float UvStone = 0.40f;  // 석재 텍스처: 약 2.5m마다 1회 반복
        const float UvFloor = 0.34f;
        const float UvWood = 0.55f;

        static readonly List<Mesh> tempMeshes = new List<Mesh>();

        // ─────────────────────────────────────────────────────
        [MenuItem("Tools/이문록/관측실 생성")]
        public static void Build()
        {
            // 씬 가드 — 다른 씬(특히 에셋 팩 데모 씬)에 실수로 지어 저장하는 사고 방지 (2026-08-10 실제 발생)
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gyeonu_Observatory")
            {
                Debug.LogError("[관측실] 활성 씬이 Gyeonu_Observatory가 아닙니다 — 씬을 먼저 열고 실행하세요");
                return;
            }
            tempMeshes.Clear();
            EnsureFolder(ModelDir); EnsureFolder(MatDir); EnsureFolder(TexDir); EnsureFolder(PrefabDir);

            // 기존 마커 위치 보존 (사용자 배치 존중)
            var prevMarkers = new Dictionary<string, (Vector3 p, Quaternion r)>();
            var oldMarkers = FindRootIncludingInactive(MarkerRootName);
            if (oldMarkers != null)
            {
                foreach (Transform ch in oldMarkers.transform) prevMarkers[ch.name] = (ch.position, ch.rotation);
                Object.DestroyImmediate(oldMarkers);
            }
            prevMarkers.Remove("Exit_ToArchive"); // 계단 방향 반전(2026-08-10)으로 위치가 바뀌므로 보존하지 않는다
            for (GameObject g; (g = FindRootIncludingInactive(RootName)) != null;) Object.DestroyImmediate(g);

            // ── 머티리얼 (외부 팩 텍스처 참조, 에셋은 우리 폴더에 생성) ──
            var texWallBC = LoadTex(KMTex + "T_GidanStone01A_BC.png");
            var texWallNM = LoadTex(KMTex + "T_GidanStone01A_NM.png");
            var texFloorBC = LoadTex(KMTex + "T_Stone02A_BC.png");
            var texFloorNM = LoadTex(KMTex + "T_Stone02A_NM.png");
            var texWoodBC = LoadTex(SWTex + "T_Wood_BC.png");
            var texWoodNM = LoadTex(SWTex + "T_Wood_NM.png"); // 없으면 null — 그대로 진행

            // 쿨 강 확정값 (2026-08-10 사용자 결정) — ObservatoryToneTuner의 Strong과 동일.
            // 전체는 차갑고 등잔 불빛(웜)만 따뜻한 대비. 목조는 절반 강도로만 식힌다
            var matWall = Mat("M_관측실_석벽", new Color(0.42f, 0.54f, 0.74f), 0f, 0.16f, texWallBC, texWallNM);
            var matFloor = Mat("M_관측실_바닥", new Color(0.30f, 0.40f, 0.60f), 0f, 0.22f, texFloorBC, texFloorNM);
            var matPattern = Mat("M_관측실_바닥무늬", new Color(0.14f, 0.19f, 0.34f), 0f, 0.34f, texFloorBC, texFloorNM);
            var matWood = Mat("M_관측실_돔판재", new Color(0.44f, 0.45f, 0.52f), 0f, 0.12f, texWoodBC, texWoodNM);
            var matWoodDark = Mat("M_관측실_돔골조", new Color(0.29f, 0.30f, 0.34f), 0f, 0.10f, texWoodBC, texWoodNM);
            var matNight = Mat("M_관측실_밤하늘", new Color(0.013f, 0.014f, 0.020f), 0f, 0.0f);
            var matIron = Mat("M_관측실_흑철", new Color(0.09f, 0.09f, 0.10f), 0.6f, 0.42f);
            var matDark = Mat("M_관측실_암문어둠", new Color(0.012f, 0.012f, 0.018f), 0f, 0.05f);
            var matFlame = Mat("M_관측실_불꽃", new Color(1f, 0.55f, 0.22f), 0f, 0.5f, null, null,
                new Color(2.6f, 1.15f, 0.35f));

            var root = new GameObject(RootName);
            var grpLight = new GameObject("조명"); grpLight.transform.SetParent(root.transform, false);

            var corStone = new List<CombineInstance>();   // 통로 석조 (바닥·벽·천장·계단)
            var roomStone = new List<CombineInstance>();  // 방 석벽·문틀·붕괴 계단실
            var floorMain = new List<CombineInstance>();  // 방 바닥·좌대
            var floorPat = new List<CombineInstance>();   // 바닥 원형 무늬·28수 눈금
            var domeShell = new List<CombineInstance>();  // 돔 판재
            var domeFrame = new List<CombineInstance>();  // 서까래·중도리·정심

            var sconces = new List<(Vector3 pos, Vector3 facing)>();

            // ── 통로: 직선·계단·모퉁이 상태기계 ──
            Vector3 pos = Vector3.zero;
            Vector3 dir = Vector3.forward;
            float fy = 0f;

            void Straight(float len, bool sconceMid = false)
            {
                var rot = Quaternion.LookRotation(dir);
                var right = Vector3.Cross(Vector3.up, dir);
                AddBox(corStone, pos + dir * (len / 2) + Vector3.up * (fy - T / 2), new Vector3(W + 2 * T, T, len), rot, UvStone);
                AddBox(corStone, pos + dir * (len / 2) + Vector3.up * (fy + H + T / 2), new Vector3(W + 2 * T, T, len), rot, UvStone);
                AddBox(corStone, pos + dir * (len / 2) + right * (W / 2 + T / 2) + Vector3.up * (fy + H / 2), new Vector3(T, H, len), rot, UvStone);
                AddBox(corStone, pos + dir * (len / 2) - right * (W / 2 + T / 2) + Vector3.up * (fy + H / 2), new Vector3(T, H, len), rot, UvStone);
                if (sconceMid)
                    sconces.Add((pos + dir * (len / 2) + right * (W / 2 - 0.10f) + Vector3.up * (fy + 1.62f), -right));
                pos += dir * len;
            }

            void Stairs(int n, float rise = 0.17f, float run = 0.35f)
            {
                float len = n * run, drop = n * rise;
                var rot = Quaternion.LookRotation(dir);
                var right = Vector3.Cross(Vector3.up, dir);
                // 옆벽: 아래층 바닥부터 위층 천장까지
                AddBox(corStone, pos + dir * (len / 2) + right * (W / 2 + T / 2) + Vector3.up * (fy + (H - drop) / 2), new Vector3(T, H + drop, len), rot, UvStone);
                AddBox(corStone, pos + dir * (len / 2) - right * (W / 2 + T / 2) + Vector3.up * (fy + (H - drop) / 2), new Vector3(T, H + drop, len), rot, UvStone);
                // 경사 천장 (진행 방향으로 내려가게 피치)
                float angDeg = Mathf.Atan2(drop, len) * Mathf.Rad2Deg;
                AddBox(corStone, pos + dir * (len / 2) + Vector3.up * (fy - drop / 2 + H + T / 2),
                    new Vector3(W + 2 * T, T, Mathf.Sqrt(len * len + drop * drop) + 0.3f), rot * Quaternion.Euler(angDeg, 0, 0), UvStone);
                // 디딤판 — 각 단이 계단 하단 밑판까지 내려오는 통짜 (밟는 면이 곧 콜라이더)
                for (int i = 0; i < n; i++)
                {
                    float topY = fy - rise * (i + 1);
                    float bottomY = fy - drop - T;
                    float h = topY - bottomY;
                    AddBox(corStone, pos + dir * (run * (i + 0.5f)) + Vector3.up * (bottomY + h / 2), new Vector3(W, h, run + 0.02f), rot, UvStone);
                }
                pos += dir * len;
                fy -= drop;
            }

            void Turn(bool rightTurn)
            {
                var right = Vector3.Cross(Vector3.up, dir);
                var nd = rightTurn ? right : -right;
                var cc = pos + dir * (W / 2);
                AddBox(corStone, cc + Vector3.up * (fy - T / 2), new Vector3(W + 2 * T, T, W + 2 * T), Quaternion.identity, UvStone);
                AddBox(corStone, cc + Vector3.up * (fy + H + T / 2), new Vector3(W + 2 * T, T, W + 2 * T), Quaternion.identity, UvStone);
                AddBox(corStone, cc + dir * (W / 2 + T / 2) + Vector3.up * (fy + H / 2), new Vector3(W + 2 * T, H, T), Quaternion.LookRotation(dir), UvStone);
                AddBox(corStone, cc - nd * (W / 2 + T / 2) + Vector3.up * (fy + H / 2), new Vector3(W + 2 * T, H, T), Quaternion.LookRotation(nd), UvStone);
                sconces.Add((cc + dir * (W / 2 - 0.10f) + Vector3.up * (fy + 1.62f), -dir));
                pos = cc + nd * (W / 2);
                dir = nd;
            }

            // ── 암문 (통로 시작점, 은하담 오작교 쪽) ──
            AddBox(corStone, new Vector3(0, H / 2, -0.2f), new Vector3(W + 2 * T, H + 2 * T, T), Quaternion.identity, UvStone); // 막음벽
            AddBox(corStone, new Vector3(0.86f, 1.05f, 0.0f), new Vector3(0.22f, 2.10f, 0.28f), Quaternion.identity, UvStone);  // 문설주
            AddBox(corStone, new Vector3(-0.86f, 1.05f, 0.0f), new Vector3(0.22f, 2.10f, 0.28f), Quaternion.identity, UvStone);
            AddBox(corStone, new Vector3(0, 2.24f, 0.0f), new Vector3(1.94f, 0.28f, 0.28f), Quaternion.identity, UvStone);       // 인방
            {   // 문 안쪽 어둠 (은하담으로 나가는 포털 자리)
                var darkGo = new GameObject("암문_어둠");
                darkGo.transform.SetParent(root.transform, false);
                var darkList = new List<CombineInstance>();
                AddBox(darkList, new Vector3(0, 1.02f, -0.09f), new Vector3(1.5f, 2.04f, 0.06f), Quaternion.identity, 1f);
                MeshGO(darkGo, Save(Combine(darkList), "관측실_암문어둠"), matDark, false);
            }

            // ── 통로 경로: 직선 9 → 우 → 2.5+계단10+2 → 좌 → 직선 9 → 우 → 1.8+계단8+2 → 좌 → 직선 7 ──
            Straight(9f, sconceMid: true);
            Turn(true);
            Straight(2.5f); Stairs(10); Straight(2.0f);
            Turn(false);
            Straight(9f, sconceMid: true);
            Turn(true);
            Straight(1.8f); Stairs(8); Straight(2.0f);
            Turn(false);
            Straight(7f);

            Vector3 posEnd = pos;          // 통로 끝 = 방 입구 패널 위치
            Straight(0.6f);                // 드럼 벽 두께를 뚫고 들어가는 여유
            float roomY = fy;              // 방 바닥 높이 (총 하강량)
            Vector3 C = posEnd + dir * R;  // 방 중심

            // ── 방: 16각 석벽 드럼 (입구·붕괴계단 개구부 2곳) ──
            float chord = 2f * R * Mathf.Tan(Mathf.PI / Sides) + 0.06f;
            float entryAngle = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg; // C→입구 방향
            for (int i = 0; i < Sides; i++)
            {
                float ang = entryAngle + i * (360f / Sides);
                var d = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                var center = C + d * R;
                var rot = Quaternion.LookRotation(-d);
                var tangent = rot * Vector3.right;
                bool isEntry = i == 0, isAlcove = i == Sides / 2;
                if (isEntry || isAlcove)
                {
                    float openW = isEntry ? 1.7f : 1.9f;
                    float lintelY = isEntry ? 2.15f : 2.35f;
                    float jambW = (chord - openW) / 2;
                    AddBox(roomStone, center + tangent * ((openW + jambW) / 2) + Vector3.up * (roomY + (WallH + 0.25f) / 2 - 0.25f), new Vector3(jambW, WallH + 0.25f, 0.35f), rot, UvStone);
                    AddBox(roomStone, center - tangent * ((openW + jambW) / 2) + Vector3.up * (roomY + (WallH + 0.25f) / 2 - 0.25f), new Vector3(jambW, WallH + 0.25f, 0.35f), rot, UvStone);
                    AddBox(roomStone, center + Vector3.up * (roomY + lintelY + (WallH - lintelY) / 2), new Vector3(chord, WallH - lintelY, 0.35f), rot, UvStone);
                    AddBox(roomStone, center + Vector3.up * (roomY + 0.02f), new Vector3(openW, 0.05f, 0.4f), rot, UvStone); // 문지방
                }
                else
                {
                    AddBox(roomStone, center + Vector3.up * (roomY + (WallH + 0.25f) / 2 - 0.25f), new Vector3(chord, WallH + 0.25f, 0.35f), rot, UvStone);
                }
                // 벽 상단 돌림띠 (돔 밑단 받침)
                AddBox(roomStone, C + d * (R - 0.18f) + Vector3.up * (roomY + WallH + 0.10f), new Vector3(chord, 0.22f, 0.5f), rot, UvStone);
            }

            // ── 무너진 서고 계단실 (입구 반대편) — 관측실이 위층, 서고가 아래층 (2026-08-10 방향 반전).
            //    계단이 아래로 내려가다 무너져 끊겼다. 기운 난간 너머로 수직갱을 내려다보면
            //    아래 깊은 곳에서 선아가 들고 간 등불의 희미한 빛만 새어 올라온다.
            //    광원 자체는 중턱에 박힌 낙석 판에 가려 형체가 보이지 않고, 차단벽 때문에 내려갈 수 없다.
            var rnd = new System.Random(20260810);
            {
                var dOut = new Vector3(Mathf.Sin((entryAngle + 180f) * Mathf.Deg2Rad), 0, Mathf.Cos((entryAngle + 180f) * Mathf.Deg2Rad));
                var rotO = Quaternion.LookRotation(dOut);
                var rightO = Vector3.Cross(Vector3.up, dOut);
                float cw = 2.0f, cd = 3.6f, chH = 2.9f;
                float shaftBottom = roomY - 8.2f;
                Vector3 mouth = C + dOut * (R + 0.18f);
                // 옆벽·안벽은 수직갱 바닥까지, 천장·바닥판으로 밀폐
                AddBox(roomStone, mouth + dOut * (cd / 2) + rightO * (cw / 2 + T / 2) + Vector3.up * ((roomY + chH + shaftBottom) / 2), new Vector3(T, chH + (roomY - shaftBottom), cd + 0.3f), rotO, UvStone);
                AddBox(roomStone, mouth + dOut * (cd / 2) - rightO * (cw / 2 + T / 2) + Vector3.up * ((roomY + chH + shaftBottom) / 2), new Vector3(T, chH + (roomY - shaftBottom), cd + 0.3f), rotO, UvStone);
                AddBox(roomStone, mouth + dOut * (cd + T / 2) + Vector3.up * ((roomY + chH + shaftBottom) / 2), new Vector3(cw + 2 * T, chH + (roomY - shaftBottom), T), rotO, UvStone);
                AddBox(roomStone, mouth + dOut * (cd / 2) + Vector3.up * (roomY + chH + T / 2), new Vector3(cw + 2 * T, T, cd + 0.6f), rotO, UvStone);
                AddBox(roomStone, mouth + dOut * (cd / 2) + Vector3.up * (shaftBottom - T / 2), new Vector3(cw, T, cd), rotO, UvStone);
                // 착지 마루 + 그 아래 통돌 (방 쪽 수직갱 벽)
                AddBox(roomStone, mouth + dOut * 0.35f + Vector3.up * (roomY - T / 2), new Vector3(cw, T, 0.7f), rotO, UvStone);
                AddBox(roomStone, mouth + dOut * 0.35f + Vector3.up * ((roomY - T + shaftBottom) / 2), new Vector3(cw, (roomY - T) - shaftBottom, 0.7f), rotO, UvStone);
                // 남은 계단 4단 (아래로 내려가는 두꺼운 판석)
                for (int i = 0; i < 4; i++)
                {
                    float topY = roomY - 0.18f * (i + 1);
                    AddBox(roomStone, mouth + dOut * (0.7f + 0.42f * (i + 0.5f)) + Vector3.up * (topY - 0.25f), new Vector3(cw, 0.5f, 0.44f), rotO, UvStone);
                }
                // 끊긴 자리의 기운 판석 조각
                AddBox(roomStone, mouth + dOut * 2.55f - rightO * 0.45f + Vector3.up * (roomY - 1.05f), new Vector3(0.9f, 0.32f, 0.5f), rotO * Quaternion.Euler(14f, 6f, -9f), UvStone);
                AddBox(roomStone, mouth + dOut * 2.75f + rightO * 0.55f + Vector3.up * (roomY - 1.35f), new Vector3(0.7f, 0.26f, 0.45f), rotO * Quaternion.Euler(-10f, 18f, 12f), UvStone);
                // 수직갱 중턱에 박힌 낙석 판 — 아래 등불의 광원을 직접 보이지 않게 가리되,
                // 가장자리로 빛이 새어 나올 틈은 남긴다
                AddBox(roomStone, mouth + dOut * (cd - 0.95f) + Vector3.up * (roomY - 5.6f), new Vector3(cw * 0.65f, 0.3f, 1.1f), rotO * Quaternion.Euler(18f, 0, 9f), UvStone);
                // 바닥 잔해
                for (int i = 0; i < 8; i++)
                {
                    float u = (float)rnd.NextDouble(), v = (float)rnd.NextDouble(), w2 = (float)rnd.NextDouble();
                    var p = mouth + dOut * (0.9f + u * (cd - 1.3f)) + rightO * ((v - 0.5f) * (cw - 0.6f)) + Vector3.up * (shaftBottom + 0.25f + w2 * 0.5f);
                    AddBox(roomStone, p, new Vector3(0.3f + u * 0.5f, 0.25f + v * 0.35f, 0.3f + w2 * 0.45f),
                        Quaternion.Euler((float)rnd.NextDouble() * 30f - 15f, (float)rnd.NextDouble() * 180f, (float)rnd.NextDouble() * 30f - 15f), UvStone);
                }
                // 기운 나무 난간 — 끊긴 단 바로 앞, 이 너머로 아래를 내려다본다
                AddBox(domeFrame, mouth + dOut * 2.32f + Vector3.up * (roomY - 0.72f + 0.95f), new Vector3(cw, 0.09f, 0.09f), rotO * Quaternion.Euler(0, 0, -5f), UvWood);
                AddBox(domeFrame, mouth + dOut * 2.30f - rightO * 0.85f + Vector3.up * (roomY - 0.72f + 0.48f), new Vector3(0.08f, 1.0f, 0.08f), rotO * Quaternion.Euler(0, 0, -6f), UvWood);
                AddBox(domeFrame, mouth + dOut * 2.34f + rightO * 0.85f + Vector3.up * (roomY - 0.72f + 0.45f), new Vector3(0.08f, 0.95f, 0.08f), rotO * Quaternion.Euler(4f, 0, 0), UvWood);
                // 진입 차단 (보이지 않는 벽 — 무너져서 내려갈 수 없다)
                var block = new GameObject("계단_차단벽");
                block.transform.SetParent(root.transform, false);
                block.transform.SetPositionAndRotation(mouth + dOut * 2.42f + Vector3.up * (roomY - 0.72f + 1.2f), rotO);
                block.AddComponent<BoxCollider>().size = new Vector3(cw + 0.4f, 2.6f, 0.2f);
                // 아래층 등불 (선아) — 아주 약한 웜 라이트 + 낙석 판 뒤에 숨긴 불씨
                var glowGo = new GameObject("아래층_등불");
                glowGo.transform.SetParent(grpLight.transform, false);
                glowGo.transform.position = mouth + dOut * (cd - 0.75f) + Vector3.up * (roomY - 6.3f);
                var gl = glowGo.AddComponent<Light>();
                gl.type = LightType.Point;
                gl.color = new Color(1f, 0.62f, 0.30f);
                gl.intensity = 3.2f; // 쿨 강 앰비언트에 묻히지 않게 — 낙석 판 가장자리로 새어 나오는 정도
                gl.range = 6.8f;
                Prim(glowGo.transform, "불씨", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.05f, matFlame);
                // 산란광 보조 — 갱 중간 벽면을 은은하게 데워 '아래에서 빛이 올라온다'는 인상을 만든다
                var scatterGo = new GameObject("아래층_등불_산란");
                scatterGo.transform.SetParent(glowGo.transform, false);
                scatterGo.transform.position = mouth + dOut * (cd - 0.5f) + Vector3.up * (roomY - 4.2f);
                var sc = scatterGo.AddComponent<Light>();
                sc.type = LightType.Point;
                sc.color = new Color(1f, 0.60f, 0.28f);
                sc.intensity = 1.0f;
                sc.range = 3.5f;
            }

            // ── 방 바닥: 화강암 원반 + 좌대, 무늬(동심원 3 + 외곽원 + 28수 눈금) ──
            AddRing(floorMain, C, R + 0.5f, 0.001f, roomY - 0.003f, roomY - 0.003f, 64, UvFloor, true);   // 바닥판
            AddRing(floorMain, C, 1.3f, 0.001f, roomY + 0.15f, roomY + 0.15f, 48, UvFloor, true);          // 좌대 상판
            AddRing(floorMain, C, 1.3f, 1.3f, roomY - 0.05f, roomY + 0.15f, 48, UvFloor, false);           // 좌대 옆면
            float[][] rings = { new[] { 1.58f, 1.45f }, new[] { 2.38f, 2.25f }, new[] { 3.18f, 3.05f }, new[] { 4.68f, 4.55f } };
            foreach (var rg in rings)
                AddRing(floorPat, C, rg[0], rg[1], roomY + 0.006f, roomY + 0.006f, 64, UvFloor, true);
            for (int i = 0; i < 28; i++) // 28수 눈금
            {
                float ang = i * (360f / 28f);
                var d = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                AddBox(floorPat, C + d * 3.55f + Vector3.up * (roomY + 0.007f), new Vector3(0.09f, 0.012f, 0.30f), Quaternion.LookRotation(d), UvFloor);
            }

            // ── 돔 (2026-08-10 개편): 벽→천장 전이부만 목조로 남기고, 그 위는 매끈한 암흑 셸.
            //    혼상 별 투영 시 무한히 먼 하늘처럼 보여야 해서 서까래는 전이부 그루터기로만,
            //    천장 중앙은 비운다. 암흑 셸은 48각으로 굴곡을 없애고 반사 0의 근흑색
            var woodBand = new[] { new Vector2(6.35f, 3.26f), new Vector2(5.95f, 3.90f), new Vector2(5.55f, 4.42f) };
            float vArc = 0f;
            for (int k = 0; k < woodBand.Length - 1; k++)
            {
                float seg = Vector2.Distance(woodBand[k], woodBand[k + 1]);
                AddLoftStrip(domeShell, C, woodBand[k + 1].x, roomY + woodBand[k + 1].y, woodBand[k].x, roomY + woodBand[k].y,
                    Sides, UvWood, vArc + seg, vArc); // (위→아래 순서 = 안쪽면)
                vArc += seg;
            }
            for (int i = 0; i < Sides * 2; i++) // 서까래 그루터기 — 전이부에만 낮은 부조로
            {
                float ang = entryAngle + i * (360f / (Sides * 2)) + 360f / (Sides * 4);
                var d = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                var a = C + d * 6.08f + Vector3.up * (roomY + 3.45f);
                var b = C + d * 5.30f + Vector3.up * (roomY + 4.38f);
                var ab = b - a;
                AddBox(domeFrame, (a + b) / 2, new Vector3(0.14f, ab.magnitude, 0.10f), Quaternion.FromToRotation(Vector3.up, ab.normalized), UvWood);
            }
            for (int i = 0; i < Sides; i++) // 마감 중도리 — 목조와 암흑 하늘의 경계 고리
            {
                float ang = entryAngle + (i + 0.5f) * (360f / Sides);
                var d = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                var tangent = Vector3.Cross(Vector3.up, d);
                AddBox(domeFrame, C + d * 5.28f + Vector3.up * (roomY + 4.44f), new Vector3(0.18f, 0.22f, 2f * 5.28f * Mathf.Tan(Mathf.PI / Sides) + 0.1f), Quaternion.LookRotation(tangent), UvWood);
            }
            var nightProfile = new[] {
                new Vector2(5.62f, 4.40f), new Vector2(4.90f, 5.05f), new Vector2(4.00f, 5.60f),
                new Vector2(3.00f, 6.00f), new Vector2(2.00f, 6.28f), new Vector2(1.00f, 6.46f), new Vector2(0.001f, 6.55f) };
            var nightShell = new List<CombineInstance>();
            float vNight = 0f;
            for (int k = 0; k < nightProfile.Length - 1; k++)
            {
                float seg = Vector2.Distance(nightProfile[k], nightProfile[k + 1]);
                AddLoftStrip(nightShell, C, nightProfile[k + 1].x, roomY + nightProfile[k + 1].y, nightProfile[k].x, roomY + nightProfile[k].y,
                    48, UvWood, vNight + seg, vNight);
                vNight += seg;
            }

            // ── 혼상 별 (칠석 은하수) — B(흰빛~푸른빛 + 따뜻한 별 소수) 확정 (2026-08-10).
            //    나중에 혼상 조작으로 켜고 끌 수 있게 그룹 하나로 묶는다
            var starDot = BuildStarDot();
            BuildStars(root, C, roomY, nightProfile, rnd, "혼상별_별빛", starDot);

            // ── 합성 메시 저장·배치 ──
            MeshGO(NewChild(root, "통로_석조"), Save(Combine(corStone), "관측실_통로석조"), matWall, true);
            MeshGO(NewChild(root, "방_석벽"), Save(Combine(roomStone), "관측실_방석조"), matWall, true);
            MeshGO(NewChild(root, "방_바닥"), Save(Combine(floorMain), "관측실_바닥"), matFloor, true);
            MeshGO(NewChild(root, "방_바닥무늬"), Save(Combine(floorPat), "관측실_바닥무늬"), matPattern, false);
            MeshGO(NewChild(root, "돔_판재"), Save(Combine(domeShell), "관측실_돔판재"), matWood, false);
            MeshGO(NewChild(root, "돔_골조"), Save(Combine(domeFrame), "관측실_돔골조"), matWoodDark, false);
            MeshGO(NewChild(root, "돔_밤하늘"), Save(Combine(nightShell), "관측실_돔밤하늘"), matNight, false);
            foreach (var m in tempMeshes) Object.DestroyImmediate(m);
            tempMeshes.Clear();

            // ── 조명: 통로 사방등 + 방 등잔대 4 ──
            foreach (var (p, f) in sconces) Sconce(grpLight.transform, p, f, matIron, matFlame);
            Sconce(grpLight.transform, new Vector3(0.78f, 1.62f, 0.6f), Vector3.left, matIron, matFlame); // 암문 앞
            for (int i = 0; i < 4; i++)
            {
                float ang = entryAngle + new[] { 60f, 120f, 240f, 300f }[i];
                var d = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                LampStand(grpLight.transform, C + d * 4.3f + Vector3.up * roomY, matIron, matFlame, shadow: i < 2);
            }
            {   // 붕괴 계단실 입구 사방등
                var dOut = new Vector3(Mathf.Sin((entryAngle + 180f) * Mathf.Deg2Rad), 0, Mathf.Cos((entryAngle + 180f) * Mathf.Deg2Rad));
                var rightO = Vector3.Cross(Vector3.up, dOut);
                Sconce(grpLight.transform, C + dOut * (R + 0.8f) + rightO * (1.0f - 0.10f) + Vector3.up * (roomY + 1.62f), -rightO, matIron, matFlame);
            }
            // 프로브 굽기 기준점 — 눈높이(1.7)는 이제 중앙 혼상 구 **안쪽**이라 큐브맵이
            // 통째로 구 내부 암흑으로 구워진다 (2026-08-10 실측: 방 전체 금속 광택이 죽음).
            // 구 꼭대기(roomY+1.87) 위로 올려 방을 내려다보게 한다
            var centerGo = new GameObject("방_중심");
            centerGo.transform.SetParent(grpLight.transform, false);
            centerGo.transform.position = C + Vector3.up * (roomY + 2.70f);

            // ── 환경: 어두운 실내 ──
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.140f, 0.180f, 0.310f); // 쿨 강
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 8f;
            RenderSettings.fogEndDistance = 42f;
            RenderSettings.fogColor = new Color(0.026f, 0.038f, 0.085f); // 쿨 강

            // 정적 플래그 (배칭) — 혼천의는 퍼즐 상호작용 대상이라 제외
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true)) mr.gameObject.isStatic = true;

            // ── 혼천의 (2026-08-10 이설): 중앙 좌대는 혼상 차지 — 천장 투영 광원이 중심에 와야
            //    별이 고르게 퍼진다. 혼천의는 조작 대상이므로 옆 작업 공간(등잔 근처)으로 내린다
            var honPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/혼천의_B_장식받침.prefab");
            if (honPrefab != null)
            {
                var hon = (GameObject)PrefabUtility.InstantiatePrefab(honPrefab);
                hon.transform.SetParent(root.transform, false);
                var sideDir = new Vector3(Mathf.Sin((entryAngle + 270f) * Mathf.Deg2Rad), 0, Mathf.Cos((entryAngle + 270f) * Mathf.Deg2Rad));
                hon.transform.position = C + sideDir * 3.4f + Vector3.up * roomY;
            }
            else Debug.LogWarning("[관측실] 혼천의 프리팹이 없습니다 — '혼천의 생성'을 먼저 실행하세요");

            // ── 마커 (기존 위치 보존) ──
            var markers = new GameObject(MarkerRootName);
            void Marker(string name, Vector3 p, Quaternion r)
            {
                var m = new GameObject(name);
                m.transform.SetParent(markers.transform, false);
                if (prevMarkers.TryGetValue(name, out var prev)) { m.transform.position = prev.p; m.transform.rotation = prev.r; }
                else { m.transform.position = p; m.transform.rotation = r; }
            }
            Marker("SpawnPoint_FromEunhaDam", new Vector3(0, 0, 0.8f), Quaternion.identity);
            Marker("Exit_ToEunhaDam", new Vector3(0, 1.0f, -0.02f), Quaternion.LookRotation(Vector3.back));
            {
                var dOut = new Vector3(Mathf.Sin((entryAngle + 180f) * Mathf.Deg2Rad), 0, Mathf.Cos((entryAngle + 180f) * Mathf.Deg2Rad));
                Marker("Exit_ToArchive", C + dOut * (R + 2.5f) + Vector3.up * (roomY - 0.72f + 1.0f), Quaternion.LookRotation(dOut)); // 끊긴 계단 끝, 수직갱 위
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            int tris = CountTris(root);
            Debug.Log($"[관측실] 생성 완료 — 방 중심 {C}, 방 바닥 Y={roomY:F2}, 통로 끝 {posEnd}, 총 {tris:n0}tri. " +
                      "다음: [관측실 프로브 굽기] 실행");
        }

        // ─────────────────────────────────────────────────────
        [MenuItem("Tools/이문록/관측실 프로브 굽기")]
        public static void BakeProbe()
        {
            var root = FindRootIncludingInactive(RootName);
            if (root == null) { Debug.LogError("[관측실] 루트가 없습니다 — 먼저 '관측실 생성' 실행"); return; }
            var center = root.transform.Find("조명/방_중심");
            if (center == null) { Debug.LogError("[관측실] 방_중심 마커가 없습니다"); return; }

            var old = root.transform.Find("조명/리플렉션프로브");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // 스테일 큐브맵 참조가 URP 프로브 매니저 NRE를 유발하므로 먼저 끊는다 (혼천의 실측)
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.customReflectionTexture = null;
            var lightGrp = root.transform.Find("조명");
            StartCubemapBake(center.position, cube =>
            {
                if (cube == null || lightGrp == null) return;
                var probeGo = new GameObject("리플렉션프로브");
                probeGo.transform.SetParent(lightGrp, false);
                probeGo.transform.position = center.position;
                var probe = probeGo.AddComponent<ReflectionProbe>();
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
                probe.customBakedTexture = cube;
                probe.size = new Vector3(13.4f, 7.6f, 13.4f);
                probe.boxProjection = true;
                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = cube;
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log("[관측실] 리플렉션 프로브 완료 (커스텀 큐브맵)");
            });
            Debug.Log("[관측실] 큐브맵 굽는 중 — 면당 1프레임, 수 프레임 뒤 완료 로그");
        }

        /// <summary>에디터가 백그라운드일 때 delayCall이 굶는 것 방지 — MCP에서 수동 호출용.</summary>
        public static void FlushDelayCalls(int n)
        {
            var fi = typeof(EditorApplication).GetField("delayCall",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < n; i++)
            {
                var d = fi.GetValue(null) as System.Delegate;
                if (d == null) break;
                fi.SetValue(null, null);
                d.DynamicInvoke();
            }
        }

        // URP 렌더그래프 ZBinning 잡과의 충돌 회피 — delayCall로 면당 1프레임씩 (혼천의와 동일)
        static void StartCubemapBake(Vector3 pos, System.Action<Cubemap> onDone)
        {
            const int res = 256;
            string path = TexDir + "/CM_관측실.asset";
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (cube == null || cube.width != res)
            {
                cube = new Cubemap(res, TextureFormat.RGBA32, true);
                AssetDatabase.CreateAsset(cube, path);
                cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            }
            var rt = new RenderTexture(res, res, 16, RenderTextureFormat.ARGB32);
            var face = new Texture2D(res, res, TextureFormat.RGBA32, false);
            var camGo = new GameObject("__ObsBakeCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.transform.position = pos;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.farClipPlane = 60f;
            cam.fieldOfView = 90f;
            cam.aspect = 1f;
            cam.targetTexture = rt;

            var faces = new (CubemapFace f, Vector3 euler)[]
            {
                (CubemapFace.PositiveX, new Vector3(0, 90, 0)),
                (CubemapFace.NegativeX, new Vector3(0, -90, 0)),
                (CubemapFace.PositiveY, new Vector3(-90, 0, 0)),
                (CubemapFace.NegativeY, new Vector3(90, 0, 0)),
                (CubemapFace.PositiveZ, new Vector3(0, 0, 0)),
                (CubemapFace.NegativeZ, new Vector3(0, 180, 0)),
            };
            var flipped = new Color[res * res];
            int i = 0;

            void Cleanup()
            {
                if (cam != null) cam.targetTexture = null;
                if (camGo != null) Object.DestroyImmediate(camGo);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (face != null) Object.DestroyImmediate(face);
            }
            void Step()
            {
                if (camGo == null || cube == null) { Cleanup(); onDone(null); return; }
                cam.transform.rotation = Quaternion.Euler(faces[i].euler);
                cam.Render();
                RenderTexture.active = rt;
                face.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                RenderTexture.active = null;
                var px = face.GetPixels();
                for (int y = 0; y < res; y++)
                    System.Array.Copy(px, y * res, flipped, (res - 1 - y) * res, res);
                cube.SetPixels(flipped, faces[i].f);
                i++;
                if (i < faces.Length) { EditorApplication.delayCall += Step; return; }
                cube.Apply(true);
                EditorUtility.SetDirty(cube);
                AssetDatabase.SaveAssets();
                Cleanup();
                onDone(cube);
            }
            EditorApplication.delayCall += Step;
        }

        // ── 조명 소품 ─────────────────────────────────────────
        static void Sconce(Transform parent, Vector3 pos, Vector3 facing, Material iron, Material flame)
        {
            var go = new GameObject("사방등");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(facing);
            Prim(go.transform, "받침", PrimitiveType.Cube, new Vector3(0, -0.06f, -0.09f), new Vector3(0.10f, 0.05f, 0.20f), iron);
            Prim(go.transform, "접시", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.16f, 0.015f, 0.16f), iron);
            Prim(go.transform, "불꽃", PrimitiveType.Sphere, new Vector3(0, 0.055f, 0), Vector3.one * 0.075f, flame);
            var l = new GameObject("불빛").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0, 0.14f, 0.05f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.68f, 0.38f);
            l.intensity = 1.7f;
            l.range = 5.5f;
        }

        static void LampStand(Transform parent, Vector3 floorPos, Material iron, Material flame, bool shadow)
        {
            var go = new GameObject("등잔대");
            go.transform.SetParent(parent, false);
            go.transform.position = floorPos;
            Prim(go.transform, "받침", PrimitiveType.Cylinder, new Vector3(0, 0.025f, 0), new Vector3(0.26f, 0.025f, 0.26f), iron);
            Prim(go.transform, "기둥", PrimitiveType.Cylinder, new Vector3(0, 0.5f, 0), new Vector3(0.045f, 0.475f, 0.045f), iron);
            Prim(go.transform, "접시", PrimitiveType.Cylinder, new Vector3(0, 0.99f, 0), new Vector3(0.19f, 0.018f, 0.19f), iron);
            Prim(go.transform, "불꽃", PrimitiveType.Sphere, new Vector3(0, 1.06f, 0), Vector3.one * 0.085f, flame);
            var l = new GameObject("불빛").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0, 1.16f, 0);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.42f);
            l.intensity = 2.6f;
            l.range = 8.5f;
            if (shadow)
            {
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.6f;
            }
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

        // ── 메시 유틸 ─────────────────────────────────────────

        /// <summary>월드 크기 비례 UV 박스를 합성 목록에 추가 (위치 해시로 UV를 어긋내 반복감 완화).</summary>
        static void AddBox(List<CombineInstance> list, Vector3 center, Vector3 size, Quaternion rot, float uvScale)
        {
            var off = new Vector2(
                Mathf.Abs(center.x * 0.173f + center.z * 0.331f) % 1f,
                Mathf.Abs(center.y * 0.257f + center.z * 0.119f) % 1f);
            var m = BoxMesh(size, uvScale, off);
            tempMeshes.Add(m);
            list.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(center, rot, Vector3.one) });
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
            Quad(new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), s.x, s.y);      // +Z
            Quad(new Vector3(h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), s.x, s.y);  // -Z
            Quad(new Vector3(h.x, -h.y, h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), s.z, s.y);      // +X
            Quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), s.z, s.y);  // -X
            Quad(new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z), s.x, s.z);      // +Y
            Quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), s.x, s.z);  // -Y
            var m = new Mesh();
            m.SetVertices(v); m.SetUVs(0, u); m.SetTriangles(tr, 0);
            return m;
        }

        /// <summary>회전면 스트립. 쌍 순서가 앞뒷면을 정한다 (혼천의 Strip과 동일 규칙):
        /// 수평 고리 — (바깥, 안) = 윗면 / (안, 바깥) = 아랫면.
        /// 수직·경사 — (아래, 위) = 바깥면 / (위, 아래) = 안쪽면.</summary>
        static void AddLoftStrip(List<CombineInstance> list, Vector3 center, float r0, float y0, float r1, float y1,
            int seg, float uvScale, float v0, float v1, bool planarUV = false)
        {
            var v = new List<Vector3>(); var u = new List<Vector2>(); var tr = new List<int>();
            float rAvg = (r0 + r1) * 0.5f;
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                var p0 = d * r0 + Vector3.up * y0;
                var p1 = d * r1 + Vector3.up * y1;
                v.Add(p0); v.Add(p1);
                if (planarUV)
                {
                    u.Add(new Vector2(p0.x, p0.z) * uvScale);
                    u.Add(new Vector2(p1.x, p1.z) * uvScale);
                }
                else
                {
                    float uu = a * rAvg * uvScale;
                    u.Add(new Vector2(uu, v0 * uvScale));
                    u.Add(new Vector2(uu, v1 * uvScale));
                }
            }
            for (int i = 0; i < seg; i++)
            {
                // (sin, cos) 각도 진행은 혼천의의 (cos, sin)과 좌우가 반대라 감김도 뒤집는다
                int b = i * 2;
                tr.Add(b); tr.Add(b + 2); tr.Add(b + 1);
                tr.Add(b + 2); tr.Add(b + 3); tr.Add(b + 1);
            }
            var m = new Mesh();
            m.SetVertices(v); m.SetUVs(0, u); m.SetTriangles(tr, 0);
            tempMeshes.Add(m);
            list.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one) });
        }

        /// <summary>수평 고리/원반(윗면) 또는 옆면 밴드. flipDisc=true면 아랫면(아래에서 보임).</summary>
        static void AddRing(List<CombineInstance> list, Vector3 center, float rA, float rB, float yA, float yB,
            int seg, float uvScale, bool planarUV, bool flipDisc = false)
        {
            if (flipDisc) AddLoftStrip(list, center, rB, yB, rA, yA, seg, uvScale, 0, rA - rB, planarUV);
            else if (Mathf.Approximately(yA, yB)) AddLoftStrip(list, center, rA, yA, rB, yB, seg, uvScale, 0, rA - rB, planarUV); // 윗면: 바깥→안
            else AddLoftStrip(list, center, rA, yA, rB, yB, seg, uvScale, yA, yB, planarUV); // 옆면: 아래→위 = 바깥
        }

        // ── 혼상 별 (시험 배치) ───────────────────────────────
        // 실내라 스카이박스 합성을 못 쓴다 — 암흑 돔 셸 안쪽 0.14m 아래에 이미시브 쿼드를
        // 뿌린다. 티어별(대·중·소)로 메시·머티리얼을 나눠 밝기 편차를 주고, 55%는
        // 기각 샘플링으로 은하수 띠 평면 근처에 몰아 배치한다. 그룹 루트를 끄면 통째로 꺼진다.
        static GameObject BuildStars(GameObject root, Vector3 C, float roomY, Vector2[] profile, System.Random rnd, string name, Texture2D dot)
        {
            var grp = NewChild(root, name);
            const string key = "B"; // B안 확정 — 에셋 이름은 비교 당시 그대로 유지 (GUID 보존)
            var tiers = new (string tier, int count, float sMin, float sMax, Color emis)[] {
                    ("대", 80, 0.14f, 0.22f, new Color(1.9f, 2.2f, 2.9f)),
                    ("중", 520, 0.07f, 0.12f, new Color(1.15f, 1.35f, 1.85f)),
                    ("소", 1050, 0.035f, 0.07f, new Color(0.60f, 0.72f, 1.00f)),
                    ("온", 70, 0.08f, 0.15f, new Color(2.2f, 1.6f, 0.85f)) };
            var bandN = Vector3.Normalize(new Vector3(0.80f, 0.38f, -0.44f)); // 은하수 띠 평면 법선
            var gaze = C + Vector3.up * (roomY + 1.4f); // 쿼드가 바라보는 방 중심
            foreach (var t in tiers)
            {
                var list = new List<CombineInstance>();
                for (int i = 0; i < t.count; i++)
                {
                    bool inBand = rnd.NextDouble() < 0.55;
                    Vector3 p = Vector3.zero;
                    for (int guard = 0; guard < 60; guard++)
                    {
                        float rr = 5.35f * Mathf.Sqrt((float)rnd.NextDouble());
                        float aa = (float)rnd.NextDouble() * Mathf.PI * 2f;
                        p = new Vector3(Mathf.Sin(aa), 0, Mathf.Cos(aa)) * rr + Vector3.up * (ProfileY(profile, rr) - 0.14f);
                        bool isIn = Mathf.Abs(Vector3.Dot((p - Vector3.up * 3.5f).normalized, bandN)) < 0.20f;
                        if (isIn == inBand) break;
                    }
                    float size = Mathf.Lerp(t.sMin, t.sMax, (float)rnd.NextDouble());
                    AddStarQuad(list, C + new Vector3(p.x, roomY + p.y, p.z), size, gaze);
                }
                var mat = MatStar($"M_관측실_별{key}_{t.tier}", t.emis, dot);
                MeshGO(NewChild(grp, $"별_{t.tier}"), Save(Combine(list), $"관측실_별{key}_{t.tier}"), mat, false);
            }
            return grp;
        }

        /// <summary>방사형 소프트 도트 텍스처 — 별이 사각 쿼드로 안 읽히게 하는 빛망울. 64², PNG.</summary>
        static Texture2D BuildStarDot()
        {
            const int res = 64;
            string path = TexDir + "/T_관측실_별.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            float c = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float v = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    v = Mathf.Clamp01(v + Mathf.Pow(Mathf.Clamp01(1f - d * 2.6f), 2f) * 0.8f); // 중심 코어 강조
                    tex.SetPixel(x, y, new Color(v, v, v, v));
                }
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>별 머티리얼 — URP Unlit, 가산 블렌딩(One/One), HDR 색으로 세기 표현.</summary>
        static Material MatStar(string name, Color hdrColor, Texture2D dot)
        {
            string path = $"{MatDir}/{name}.mat";
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(unlit) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != unlit)
            {
                m.shader = unlit; // 이전 빌드가 Lit로 만든 경우 강제 교체 (키워드 초기화)
            }
            m.DisableKeyword("_EMISSION");
            m.SetTexture("_BaseMap", dot);
            m.SetColor("_BaseColor", hdrColor);
            m.SetFloat("_Surface", 1f);   // Transparent
            m.SetFloat("_Blend", 2f);     // Additive
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>돔 프로필(바깥→정점 순, r 내림차순)에서 반지름 r의 높이를 보간.</summary>
        static float ProfileY(Vector2[] prof, float r)
        {
            r = Mathf.Min(r, prof[0].x);
            for (int k = 0; k < prof.Length - 1; k++)
                if (r <= prof[k].x && r >= prof[k + 1].x)
                    return Mathf.Lerp(prof[k + 1].y, prof[k].y, Mathf.InverseLerp(prof[k + 1].x, prof[k].x, r));
            return prof[prof.Length - 1].y;
        }

        /// <summary>target을 바라보는 정사각 쿼드(별 하나)를 합성 목록에 추가.</summary>
        static void AddStarQuad(List<CombineInstance> list, Vector3 pos, float size, Vector3 target)
        {
            var n = (target - pos).normalized;
            var right = Vector3.Cross(Vector3.up, n);
            if (right.sqrMagnitude < 1e-4f) right = Vector3.right; else right.Normalize();
            var up2 = Vector3.Cross(n, right);
            float h = size * 0.5f;
            var m = new Mesh();
            m.SetVertices(new List<Vector3> { -right * h - up2 * h, right * h - up2 * h, right * h + up2 * h, -right * h + up2 * h });
            m.SetUVs(0, new List<Vector2> { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            tempMeshes.Add(m);
            list.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one) });
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
            if (t == null) Debug.LogWarning($"[관측실] 텍스처 없음: {path}");
            return t;
        }

        static Material Mat(string name, Color c, float metallic, float smooth,
            Texture2D baseMap = null, Texture2D normal = null, Color? emission = null)
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
