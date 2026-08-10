using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관측실(Gyeonu_Observatory) 방 구조·암문 통로·기본 조명 절차 생성 (2026-08-11 개편, 멱등).
    ///
    /// 구성: 암문(입구) → 꺾임 4회·계단 하강 2구간의 좁은 통로(약 42m, 폭 1.8 × 천장 2.35)
    ///       → **사각 작업실** 13×10m, 천장 3.6m (주 공간, 낮은 바닥)
    ///       → 북벽 아치 홍예 + 계단 4단(0.72m) → **돔 관측실** 지름 8m (부속, 높은 단).
    ///
    /// 2026-08-11 개편 이유: 방 전체가 원형 돔이라 벽에 평평한 면이 없어 책장·서가를 붙일 수
    /// 없었다. 주 공간을 직사각형으로 바꿔 벽 4면을 평면으로 확보하고(장식·걸레받이 없음),
    /// 돔은 혼상 별 투영 전용 부속 공간으로 분리했다. 아치가 프레임이 되어, 계단을 오르는
    /// 순간 돔 천장이 눈앞에 펼쳐진다. 분위기·재질은 ALP Alchemist House Room00 참고
    /// (석벽 + 마루널 + 노출 목조 천장 + 웜 포인트 다수), 붕괴 계단 개구부는 Room01 방식
    /// (위층엔 개구부·난간만, 계단 구조물은 그 아래에 붙는다).
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
        const string ALPTex = "Assets/BK_AlchemistHouse/Textures/Surfaces/";  // ALP Alchemist House (폴더명 ≠ 팩 이름)

        // 통로 단면 — VR 눈높이 1.7 기준: 답답하되 부딪히지는 않게
        const float W = 1.8f;   // 폭
        const float H = 2.35f;  // 천장고
        const float T = 0.30f;  // 벽 두께

        // 사각 작업실 (주 공간, 낮은 바닥) — 벽 4면이 평평해야 서가·책상을 뒷판째 붙일 수 있다.
        // 이게 개편의 핵심이라 어떤 장식도 벽면 밖으로 튀어나오게 두지 않는다 (걸레받이·부벽 없음).
        const float ShopW = 13.0f;   // 폭 (X)
        const float ShopD = 10.0f;   // 깊이 (Z)
        // 2026-08-11: 3.60에서는 눈높이 1.7로 서면 3.3m 앞부터 천장이 시야에 들어와 답답했다.
        // 5.30이면 6.2m 밖에서야 걸리기 시작한다 (수직 화각 60° 기준)
        const float ShopH = 5.30f;   // 바닥 → 천장 밑면
        const float WT = 0.35f;      // 방 벽 두께

        // 작업실 → 돔 단 (계단 4단, 총 0.72m)
        const int StepN = 4;
        const float StepRise = 0.18f;
        const float StepRun = 0.40f;

        // 돔 관측실 (부속, 높은 단)
        const float R = 4.0f;         // 드럼 반지름 = 지름 8m
        const int Sides = 16;         // 드럼 변 수. 아치 개구부가 2면(±22.5°)을 차지한다
        // ⚠️ 드럼 높이를 작업실 층고에 묶으면 안 된다 (2026-08-11 실측):
        //    층고를 5.30으로 올리자 드럼도 같이 올라가 **하늘이 시작되는 높이**가 눈높이 위 2.8m까지
        //    밀려났고, 정점은 높아졌는데 오히려 덜 감싸는 하늘이 됐다 (수평 기준 15.5° → 35.8°).
        //    드럼은 단 바닥 기준으로 따로 잡고, 높이는 돔 곡면 쪽에 몰아준다
        const float DrumH = 3.43f;    // 단 바닥 → 드럼 꼭대기 (스프링라인). 눈높이 위 1.6m
        const float DomeRise = 4.90f; // 스프링라인 → 정점. 단 바닥에서 8.2m

        // 심벽(心壁) 벽면 구성 — 하단 석축 / 하방 / 회벽 밭 + 기둥 / 상방.
        // 2026-08-11: 벽 전체가 막돌(기단석 텍스처)이라 자연 동굴로 읽혔다. Room00·Room01처럼
        // **다듬은 층쌓기 석축 + 회벽 + 목재 프레임**으로 나눠 사람이 지은 방으로 만든다.
        // ⚠️ 모든 층이 같은 평면에 놓인다 — 벽면에서 튀어나오는 부재는 하나도 없다 (서가가 뒷판째 붙어야 함)
        const float BaseH = 0.85f;     // 하단 석축 높이
        const float TrimH = 0.13f;     // 하방·중방·상방 목재 띠 두께
        const float PostW = 0.20f;     // 심벽 기둥 폭
        const float PostStep = 2.6f;   // 기둥 간격 목표 (칸 수는 벽 길이로 반올림)
        // 층고 5.30으로 올리면서 회벽 밭이 4.2m가 되어 허전해졌다 — 중방 한 켜로 위아래를 나눈다.
        // ⚠️ 중방과 기둥은 **서로 관통시키지 않고** 켜를 잘라 이어 붙인다.
        //    둘 다 벽면과 같은 평면이라 겹치면 앞면끼리 z-파이팅이 난다
        const float MidRailY = 2.55f;  // 중방 밑단 (바닥 기준)

        const float UvStone = 0.40f;  // 막돌(통로·홍예·수직갱): 약 2.5m마다 1회 반복
        const float UvBase = 0.31f;   // 다듬은 층쌓기 석축: 한 겹 ≈ 0.25m
        const float UvPlaster = 0.28f;
        const float UvFloor = 0.34f;
        const float UvWood = 0.55f;

        static readonly List<Mesh> tempMeshes = new List<Mesh>();

        /// <summary>보행 콜라이더용 박스 원장 — AddBox가 찍는 모든 구조 박스를 그대로 받아둔다.
        /// 합성 메시에 MeshCollider를 걸면 얇은 삼각형에 끼이므로 전부 박스로 낸다 (마을·은하담과 동일 방침).
        /// 렌더 박스와 1:1이라 형상이 바뀌어도 콜라이더가 저절로 따라온다.</summary>
        static readonly List<(Vector3 c, Vector3 s, Quaternion r)> colBoxes = new List<(Vector3, Vector3, Quaternion)>();

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
            colBoxes.Clear();
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

            // 손으로 옮긴 혼천의 위치는 보존한다 (재생성 때마다 되돌아가 사용자 배치가 날아갔음)
            bool hasHonPose = false;
            Vector3 honPos = Vector3.zero; Quaternion honRot = Quaternion.identity;
            var oldRoom = FindRootIncludingInactive(RootName);
            if (oldRoom != null)
            {
                var oldHon = oldRoom.transform.Find("혼천의_B_장식받침");
                if (oldHon != null) { honPos = oldHon.position; honRot = oldHon.rotation; hasHonPose = true; }
            }
            for (GameObject g; (g = FindRootIncludingInactive(RootName)) != null;) Object.DestroyImmediate(g);

            // ── 머티리얼 (외부 팩 텍스처 참조, 에셋은 우리 폴더에 생성) ──
            var texWallBC = LoadTex(KMTex + "T_GidanStone01A_BC.png");
            var texWallNM = LoadTex(KMTex + "T_GidanStone01A_NM.png");
            var texFloorBC = LoadTex(KMTex + "T_Stone02A_BC.png");
            var texFloorNM = LoadTex(KMTex + "T_Stone02A_NM.png");
            var texWoodBC = LoadTex(SWTex + "T_Wood_BC.png");
            var texWoodNM = LoadTex(SWTex + "T_Wood_NM.png"); // 없으면 null — 그대로 진행
            var texMaruBC = LoadTex(KMTex + "T_Floor01A_BC.png");  // 작업실 마루널
            var texMaruNM = LoadTex(KMTex + "T_Floor01A_NM.png");
            // 심벽 재료 (2026-08-11) — 회벽은 김명관 회벽(균일해서 월드 UV로 아무 크기나 붙는다),
            // 석축은 ALP Room00과 같은 다듬은 층쌓기 돌. 원본 폴더는 참조만 한다
            var texPlasterBC = LoadTex(KMTex + "T_WhiteWall01A_BC.png");
            var texPlasterNM = LoadTex(KMTex + "T_WhiteWall01A_NM.png");
            var texBaseBC = LoadTex(ALPTex + "StoneWall01.png");
            var texBaseNM = LoadTex(ALPTex + "StoneWall01_n.png");
            var texBaseAO = LoadTex(ALPTex + "StoneWall01_o.png");

            // 쿨 강 확정값 (2026-08-10 사용자 결정) — ObservatoryToneTuner의 Strong과 동일.
            // 전체는 차갑고 등잔 불빛(웜)만 따뜻한 대비. 목조는 절반 강도로만 식힌다
            var matWall = Mat("M_관측실_석벽", new Color(0.42f, 0.54f, 0.74f), 0f, 0.16f, texWallBC, texWallNM);
            var matFloor = Mat("M_관측실_바닥", new Color(0.30f, 0.40f, 0.60f), 0f, 0.22f, texFloorBC, texFloorNM);
            var matMaru = Mat("M_관측실_마루", new Color(0.40f, 0.45f, 0.58f), 0f, 0.15f, texMaruBC, texMaruNM);
            // 회벽 원본 텍스처는 거의 흰색(휘도 ≈0.85)이라 틴트를 세게 먹여야 한다.
            // ⚠️ 실측(2026-08-11): 틴트 0.44/0.50/0.63으로 두니 웜 등잔빛이 회벽을 완전히 지배해
            // 방이 크림색으로 뜨고 쿨톤이 통째로 날아갔다. 최종 알베도를 옛 막돌벽(≈0.24)과
            // 비슷한 수준까지 낮춰야 **무늬만 바뀌고 분위기는 유지**된다.
            //   회벽 0.85×0.32 ≈ 0.27  /  석축 0.42×0.44 ≈ 0.18  → 회벽이 석축보다 1.5배 밝다 (마감면 신호)
            var matPlaster = Mat("M_관측실_회벽", new Color(0.27f, 0.33f, 0.46f), 0f, 0.05f, texPlasterBC, texPlasterNM);
            var matBase = Mat("M_관측실_석축", new Color(0.38f, 0.46f, 0.62f), 0f, 0.13f, texBaseBC, texBaseNM, null, texBaseAO);
            var matSimbyeok = Mat("M_관측실_심벽목", new Color(0.28f, 0.30f, 0.37f), 0f, 0.11f, texWoodBC, texWoodNM);
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
            var roomStone = new List<CombineInstance>();  // 작업실·돔 석조 (벽·홍예·계단·수직갱)
            var floorMain = new List<CombineInstance>();  // 마루널·천장널·돔 단 바닥
            var floorPat = new List<CombineInstance>();   // 바닥 원형 무늬·28수 눈금
            var maru = new List<CombineInstance>();       // 작업실 마루널·천장널
            var roomBase = new List<CombineInstance>();   // 심벽 하단 석축 (다듬은 층쌓기)
            var roomPlaster = new List<CombineInstance>();// 심벽 회벽 밭
            var roomTrim = new List<CombineInstance>();   // 심벽 목재 프레임 (하방·상방·기둥)
            var domeShell = new List<CombineInstance>();  // 돔 판재
            var domeFrame = new List<CombineInstance>();  // 보·장선·난간·서까래

            // 축 정렬 상자를 min/max 코너로 찍는다 — 방 구조는 중심/크기보다 이쪽이 읽기 쉽다.
            // 두께가 0에 수렴하는 조각은 버린다 (심벽 밭이 기둥에 다 먹힌 경우 등)
            void Slab(List<CombineInstance> list, float x0, float x1, float y0, float y1, float z0, float z1, float uv, Vector2? uvOff = null)
            {
                if (x1 - x0 < 0.004f || y1 - y0 < 0.004f || z1 - z0 < 0.004f) return;
                AddBox(list, new Vector3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2),
                    new Vector3(x1 - x0, y1 - y0, z1 - z0), Quaternion.identity, uv, uvOff);
            }

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

            Vector3 posEnd = pos;          // 통로 끝 = 작업실 남벽 문간
            Straight(0.6f);                // 남벽 두께(0.35)를 채우고 조금 더 물리는 문간
            float roomY = fy;              // 작업실 바닥 높이 (통로 총 하강량)
            if (Mathf.Abs(dir.z - 1f) > 1e-3f)
                Debug.LogWarning("[관측실] 통로가 +Z로 들어오지 않습니다 — 아래 작업실 좌표계는 +Z 진입 전제");

            // ── 작업실 좌표계 (모두 벽 **안쪽면** 기준) ─────────────────
            // 통로는 남벽 x=posEnd.x 로 들어온다. 문을 서편으로 치우치게 두어
            // 들어서는 순간 방이 오른쪽(동)으로 길게 열리고, 그 끝 북동편에 돔 아치가 보이게 한다.
            float sx0 = posEnd.x - 3.70f;              // 서벽
            float sx1 = sx0 + ShopW;                   // 동벽
            float sz0 = posEnd.z + 0.60f;              // 남벽
            float sz1 = sz0 + ShopD;                   // 북벽
            float shopTop = roomY + ShopH;             // 천장 밑면
            float platY = roomY + StepRise * StepN;    // 돔 단 바닥 (작업실보다 0.72 높다)
            float doorX0 = posEnd.x - W / 2, doorX1 = posEnd.x + W / 2;

            // 돔 — 북벽 아치 통로 너머. 개구부 폭은 드럼 2면(±360/Sides°)을 그대로 뜯어낸 값이라
            // 통로 옆벽과 드럼 개구부 모서리가 저절로 맞물린다.
            float archX = sx1 - 4.0f;                                  // 아치 중심 (동편으로 치우침)
            float archHalf = R * Mathf.Sin(Mathf.PI * 2f / Sides);     // 개구부 반폭 1.531
            float archChordZ = R * Mathf.Cos(Mathf.PI * 2f / Sides);   // 돔 중심 → 개구부 현 3.696
            const float Throat = 1.35f;                                // 아치 통로 깊이
            Vector3 C = new Vector3(archX, 0f, sz1 + Throat + archChordZ);  // 돔 중심 (y는 0 기준)
            float drumTop = platY + DrumH;             // 드럼 꼭대기 = 돔 스프링라인 (작업실 천장보다 낮다)
            float throatZ1 = C.z - archChordZ;         // 아치 통로 북끝 = 드럼 개구부

            // 붕괴 서고 계단 — 서벽에 붙은 바닥 개구부 3.0×3.0
            float ox0 = sx0, ox1 = sx0 + 3.0f;
            float oz0 = sz0 + 5.0f, oz1 = oz0 + 3.0f;
            float stubX1 = ox0 + 1.4f;                 // 남은 계단 폭 (서벽에 붙임)
            float shaftBottom = roomY - 8.2f;

            // 혼천의 자리 (작업실 한복판, 바닥 메달리온 위). y=0 기준 — C와 같은 규약
            var honSpot = new Vector3(sx0 + 6.3f, 0f, sz0 + 4.2f);

            // ── 작업실 바닥: 마루널. 붕괴 개구부만 뚫는다 ──
            float fy0 = roomY - 0.30f;
            Slab(maru, sx0 - WT, sx1 + WT, fy0, roomY, sz0 - WT, oz0, UvFloor);
            Slab(maru, sx0 - WT, sx1 + WT, fy0, roomY, oz1, sz1 + WT, UvFloor);
            Slab(maru, ox1, sx1 + WT, fy0, roomY, oz0, oz1, UvFloor);

            // ── 작업실 벽 4면 = 심벽 ──────────────────────────────
            // 아래에서 위로: 석축(0~0.85) / 하방(목) / 회벽 밭 + 기둥(목) / 상방(목).
            // 네 층이 **모두 같은 평면**을 나눠 가지므로 벽면은 여전히 완전 평면이다 —
            // 겹치는 상자가 없어 z-파이팅도 없고, 서가는 그대로 뒷판째 붙는다.
            float yStone = roomY + BaseH;          // 석축 상단
            float yRailB = yStone + TrimH;         // 하방 상단 = 회벽 밑단
            float yMid = roomY + MidRailY;         // 중방 밑단
            float yRailT = shopTop - TrimH;        // 상방 하단 = 회벽 윗단
            var baseUv = new Vector2(0f, 0f);      // 석축은 고정 오프셋 — 이음매에서 켜가 어긋나지 않게

            // 벽 한 장. alongX면 a축이 X(두께가 Z), 아니면 a축이 Z(두께가 X).
            // yBot은 석축을 어디까지 내릴지 (바닥 밑 / 수직갱 바닥).
            void Simbyeok(bool alongX, float t0, float t1, float a0, float a1, float yBot)
            {
                void Band(List<CombineInstance> L, float b0, float b1, float y0, float y1, float uv, Vector2? off = null)
                {
                    if (alongX) Slab(L, b0, b1, y0, y1, t0, t1, uv, off);
                    else Slab(L, t0, t1, y0, y1, b0, b1, uv, off);
                }
                // 회벽 밭 한 켜 — 좌우 끝 반기둥 + 사이 기둥. 켜마다 따로 세워 중방과 겹치지 않게 한다
                void Field(float y0, float y1)
                {
                    if (y1 - y0 < 0.06f) return;
                    float span = a1 - a0;
                    int bays = Mathf.Max(1, Mathf.RoundToInt(span / PostStep));
                    float step = span / bays;
                    Band(roomTrim, a0, a0 + PostW / 2, y0, y1, UvWood);
                    Band(roomTrim, a1 - PostW / 2, a1, y0, y1, UvWood);
                    float cur = a0 + PostW / 2;
                    for (int i = 1; i < bays; i++)
                    {
                        float e = a0 + step * i;
                        Band(roomPlaster, cur, e - PostW / 2, y0, y1, UvPlaster);
                        Band(roomTrim, e - PostW / 2, e + PostW / 2, y0, y1, UvWood);
                        cur = e + PostW / 2;
                    }
                    Band(roomPlaster, cur, a1 - PostW / 2, y0, y1, UvPlaster);
                }
                Band(roomBase, a0, a1, yBot, yStone, UvBase, baseUv);   // 하단 석축
                Band(roomTrim, a0, a1, yStone, yRailB, UvWood);         // 하방
                Field(yRailB, yMid);                                    // 아래 켜
                Band(roomTrim, a0, a1, yMid, yMid + TrimH, UvWood);     // 중방
                Field(yMid + TrimH, yRailT);                            // 위 켜
                Band(roomTrim, a0, a1, yRailT, shopTop, UvWood);        // 상방
            }

            Simbyeok(false, sx0 - WT, sx0, sz0 - WT, oz0, fy0);                 // 서벽 (남)
            Simbyeok(false, sx0 - WT, sx0, oz0, oz1, roomY);                    // 서벽 (개구부 구간)
            Slab(roomStone, sx0 - WT, sx0, shaftBottom - 0.3f, roomY, oz0, oz1, UvStone);  //   └ 그 아래 수직갱 벽은 막돌 그대로
            Simbyeok(false, sx0 - WT, sx0, oz1, sz1 + WT, fy0);                 // 서벽 (북)
            Simbyeok(false, sx1, sx1 + WT, sz0 - WT, sz1 + WT, fy0);            // 동벽
            Simbyeok(true, sz0 - WT, sz0, sx0 - WT, doorX0, fy0);               // 남벽 (서)
            Simbyeok(true, sz0 - WT, sz0, doorX1, sx1 + WT, fy0);               // 남벽 (동)
            Simbyeok(true, sz1, sz1 + WT, sx0 - WT, archX - archHalf, fy0);     // 북벽 (서)
            Simbyeok(true, sz1, sz1 + WT, archX + archHalf, sx1 + WT, fy0);     // 북벽 (동)
            // 남벽 문 위 인방 — 목재 창방 한 줄 + 그 위 회벽, 맨 위는 상방
            Slab(roomTrim, doorX0, doorX1, roomY + 2.15f, roomY + 2.28f, sz0 - WT, sz0, UvWood);
            Slab(roomPlaster, doorX0, doorX1, roomY + 2.28f, yRailT, sz0 - WT, sz0, UvPlaster);
            Slab(roomTrim, doorX0, doorX1, yRailT, shopTop, sz0 - WT, sz0, UvWood);

            // ── 천장: 널 + 보(Z방향 4) + 장선(X방향 9). Room00의 노출 목조 천장 참고 ──
            Slab(maru, sx0 - WT, sx1 + WT, shopTop, shopTop + 0.18f, sz0 - WT, sz1 + WT, UvFloor);
            for (int i = 0; i < 4; i++)
            {
                float bx = sx0 + 1.7f + i * 3.2f;
                Slab(domeFrame, bx - 0.16f, bx + 0.16f, shopTop - 0.36f, shopTop, sz0, sz1, UvWood);
            }
            for (int i = 0; i < 9; i++)
            {
                float bz = sz0 + 0.6f + i * 1.1f;
                Slab(domeFrame, sx0, sx1, shopTop - 0.20f, shopTop, bz - 0.07f, bz + 0.07f, UvWood);
            }
            // 귀퉁이 기둥은 두지 않는다 — 심벽 기둥이 벽면 안에서 모서리를 잡아주므로
            // 방 안으로 튀어나오는 부재가 하나도 없다

            // ── 돔으로 오르는 계단 4단 + 옆 석재 볼 ──
            // 계단·홍예는 **다듬은 돌**(석축과 같은 재질)로 쌓는다 — 막돌로 두면 회벽 방 한가운데
            //    동굴 조각이 박힌 것처럼 읽힌다. 막돌은 암문 통로와 무너진 수직갱에만 남긴다
            for (int k = 0; k < StepN; k++)
                Slab(roomBase, archX - archHalf, archX + archHalf, roomY - 0.05f, roomY + StepRise * (k + 1),
                    sz1 - StepRun * (StepN - k), sz1 - StepRun * (StepN - 1 - k), UvBase, baseUv);
            for (int s = -1; s <= 1; s += 2)
            {
                float cx = archX + s * (archHalf + WT / 2);
                Slab(roomBase, cx - WT / 2, cx + WT / 2, roomY - 0.05f, roomY + 0.61f, sz1 - StepRun * StepN, sz1 - StepRun * 2, UvBase, baseUv);
                Slab(roomBase, cx - WT / 2, cx + WT / 2, roomY - 0.05f, roomY + 0.97f, sz1 - StepRun * 2, sz1, UvBase, baseUv);
            }

            // ── 아치 통로: 작업실 북벽부터 드럼까지 관통하는 홍예 볼트 ──
            //    분절 홍예(스프링라인 2.60 → 정점 3.50)를 세로 기둥 30개로 층지게 쌓는다.
            //    잘게 썬 층이 그대로 홍예석(voussoir)처럼 읽히고, 머리 부딪힘 콜라이더도 같이 나온다.
            //    2026-08-11: 홍예를 통째로 돌로 두니 회벽 방 한가운데 돌덩이 하나가 겉돌았다.
            //    이제 **목재 홍예 테두리(0.26) + 그 위는 벽과 똑같은 회벽 + 상방**으로 쌓는다 —
            //    아치 윗면이 양옆 벽의 회벽·상방과 같은 높이에서 그대로 이어지고,
            //    양옆 문선은 북벽 심벽 패널이 끝단에 세우는 반기둥이 그대로 맡는다.
            {
                const int cols = 44;
                const float ArchRing = 0.26f;   // 홍예 목재 테두리 (소피트 + 작업실 쪽 면)
                float springY = roomY + 2.75f, apexY = roomY + 3.70f;
                float rise = apexY - springY;
                float rad = (archHalf * archHalf + rise * rise) / (2f * rise);
                float cy = apexY - rad;
                float zWall = sz1 + WT, zEnd = throatZ1 + 0.05f;
                // 통로는 z로 두 토막이다: 벽 두께 구간은 작업실 천장까지, 드럼 쪽 구간은 드럼 꼭대기까지.
                // 층이 갈리는 자리(z=zWall)는 홍예 위 실체 속이라 어느 쪽에서도 보이지 않는다
                for (int seg = 0; seg < 2; seg++)
                {
                    float z0 = seg == 0 ? sz1 : zWall, z1 = seg == 0 ? zWall : zEnd;
                    float top = seg == 0 ? shopTop : drumTop;
                    float railT = top - TrimH;
                    for (int k = 0; k < cols; k++)
                    {
                        float u0 = -archHalf + 2f * archHalf * k / cols;
                        float u1 = -archHalf + 2f * archHalf * (k + 1) / cols;
                        float uo = Mathf.Max(Mathf.Abs(u0), Mathf.Abs(u1));   // 바깥 모서리 = 낮은 쪽 (개구부를 좁히지 않는다)
                        float y = cy + Mathf.Sqrt(Mathf.Max(0f, rad * rad - uo * uo));
                        if (y >= top - 0.02f) continue;
                        float x0 = archX + u0, x1 = archX + u1;
                        float yRing = Mathf.Min(y + ArchRing, top);
                        Slab(roomTrim, x0, x1, y, yRing, z0, z1, UvWood);
                        float yPl = Mathf.Max(yRing, Mathf.Min(railT, top));
                        Slab(roomPlaster, x0, x1, yRing, yPl, z0, z1, UvPlaster);
                        Slab(roomTrim, x0, x1, yPl, top, z0, z1, UvWood);
                    }
                    // 통로 옆벽(문선 안쪽 볼)도 벽과 같은 켜로 — 석축 / 하방 / 회벽 / 상방
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float w0 = s < 0 ? archX - archHalf - WT : archX + archHalf;
                        float w1 = s < 0 ? archX - archHalf : archX + archHalf + WT;
                        float pStone = platY + BaseH;
                        Slab(roomBase, w0, w1, platY - 0.30f, pStone, z0, z1, UvBase, baseUv);
                        Slab(roomTrim, w0, w1, pStone, pStone + TrimH, z0, z1, UvWood);
                        Slab(roomPlaster, w0, w1, pStone + TrimH, railT, z0, z1, UvPlaster);
                        Slab(roomTrim, w0, w1, railT, top, z0, z1, UvWood);
                    }
                }
                Slab(roomBase, archX - archHalf, archX + archHalf, platY - 0.30f, platY, sz1, zEnd, UvBase, baseUv); // 통로 바닥 = 단 높이
            }

            // ── 돔 드럼: 16각. 아치 개구부가 2면(i=0, Sides-1)을 차지한다.
            //    작업실과 같은 심벽 구성(석축/하방/회벽/상방)으로 쌓아 두 공간이 한 건물로 읽히게 한다.
            //    단 바닥이 0.72 높으므로 석축 상단도 그만큼 올라간다 ──
            float chord = 2f * R * Mathf.Tan(Mathf.PI / Sides) + 0.06f;
            const float domeEntry = 180f;   // 돔 중심 → 작업실 방향(-Z)
            float dStone = platY + BaseH, dRailB = dStone + TrimH, dRailT = drumTop - TrimH;
            for (int i = 0; i < Sides; i++)
            {
                if (i == 0 || i == Sides - 1) continue;
                float ang = domeEntry + (i + 0.5f) * (360f / Sides);
                var d = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                var rot = Quaternion.LookRotation(-d);
                void Ring(List<CombineInstance> L, float y0, float y1, float uv, Vector2? off)
                {
                    AddBox(L, C + d * R + Vector3.up * ((y0 + y1) / 2), new Vector3(chord, y1 - y0, WT), rot, uv, off);
                }
                float dMid = platY + MidRailY;                            // 드럼 중방 (단 바닥 기준)
                Ring(roomBase, platY - 0.25f, dStone, UvBase, baseUv);   // 석축
                Ring(roomTrim, dStone, dRailB, UvWood, null);            // 하방
                Ring(roomPlaster, dRailB, dMid, UvPlaster, null);        // 회벽 아래 켜
                Ring(roomTrim, dMid, dMid + TrimH, UvWood, null);        // 중방
                Ring(roomPlaster, dMid + TrimH, dRailT, UvPlaster, null);// 회벽 위 켜
                Ring(roomTrim, dRailT, drumTop, UvWood, null);           // 상방
                // 돌림띠 — 돔 밑단을 받는 도리. 여기만 벽면에서 안으로 나오지만 눈높이 위라 가구와 무관하다
                AddBox(roomTrim, C + d * (R - 0.18f) + Vector3.up * (drumTop + 0.10f),
                    new Vector3(chord, 0.22f, 0.5f), rot, UvWood);
            }

            // ── 무너진 서고 계단 (2026-08-11 개편) — 관측실이 위층, 서고가 아래층.
            //    ALP Room01 방식: 위층에는 **개구부와 난간만** 있고 계단 구조물은 그 아래에 붙어 있다.
            //    몇 단 내려가다 무너져 끊겼고, 그 아래 깊은 곳에서 선아가 들고 간 등불 빛만 새어 올라온다.
            //    광원 자체는 중턱에 박힌 낙석 판에 가려 형체가 보이지 않고, 차단벽 때문에 내려갈 수 없다.
            var rnd = new System.Random(20260811);
            {
                // 수직갱 (서벽은 위에서 이미 갱바닥까지 내렸다)
                Slab(roomStone, ox1, ox1 + WT, shaftBottom - 0.3f, fy0, oz0 - WT, oz1 + WT, UvStone);        // 동
                Slab(roomStone, ox0 - WT, ox1 + WT, shaftBottom - 0.3f, fy0, oz0 - WT, oz0, UvStone);        // 남
                Slab(roomStone, ox0 - WT, ox1 + WT, shaftBottom - 0.3f, fy0, oz1, oz1 + WT, UvStone);        // 북
                Slab(roomStone, ox0 - WT, ox1 + WT, shaftBottom - 0.3f, shaftBottom, oz0 - WT, oz1 + WT, UvStone); // 갱바닥

                // 남은 계단 5단 — 서벽에 붙어 북(개구부 어귀)에서 남으로 내려가다 끊긴다
                for (int i = 0; i < 5; i++)
                {
                    float topY = roomY - 0.18f * (i + 1);
                    Slab(roomStone, ox0, stubX1, topY - 0.50f, topY, oz1 - 0.42f * (i + 1), oz1 - 0.42f * i, UvStone);
                }
                float breakZ = oz1 - 0.42f * 5;   // 끊긴 자리
                // 끊긴 자리의 기운 판석 조각
                AddBox(roomStone, new Vector3(ox0 + 0.55f, roomY - 1.12f, breakZ - 0.30f), new Vector3(0.9f, 0.32f, 0.5f), Quaternion.Euler(14f, 6f, -9f), UvStone);
                AddBox(roomStone, new Vector3(ox0 + 1.05f, roomY - 1.48f, breakZ - 0.62f), new Vector3(0.7f, 0.26f, 0.45f), Quaternion.Euler(-10f, 18f, 12f), UvStone);
                // 수직갱 중턱에 박힌 낙석 판 — 아래 등불의 광원을 직접 보이지 않게 가리되,
                // 가장자리로 빛이 새어 나올 틈은 남긴다. 계단 옆 빈 슬롯(x 15.9~17.5)이
                // 위에서 내려다보는 유일한 시선이라, 판은 **그 슬롯 바로 위**에 걸쳐야 광원이 가려진다
                // 갱 단면(3.0×3.0)을 다 덮으면 아래 빛이 통째로 막힌다 — 광원만 가리는 크기로
                AddBox(roomStone, new Vector3(ox0 + 1.8f, roomY - 5.3f, oz0 + 1.5f), new Vector3(1.7f, 0.3f, 1.2f), Quaternion.Euler(16f, 0, 9f), UvStone);
                // 갱바닥 잔해
                for (int i = 0; i < 8; i++)
                {
                    float u = (float)rnd.NextDouble(), v = (float)rnd.NextDouble(), w2 = (float)rnd.NextDouble();
                    var p = new Vector3(ox0 + 0.5f + u * 2.0f, shaftBottom + 0.25f + w2 * 0.5f, oz0 + 0.5f + v * 2.0f);
                    AddBox(roomStone, p, new Vector3(0.3f + u * 0.5f, 0.25f + v * 0.35f, 0.3f + w2 * 0.45f),
                        Quaternion.Euler((float)rnd.NextDouble() * 30f - 15f, (float)rnd.NextDouble() * 180f, (float)rnd.NextDouble() * 30f - 15f), UvStone);
                }

                // 개구부 난간 (Room01 방식) — 동·남은 통으로, 북은 계단 입구 1.4m만 비운다
                const float RailH = 0.95f;
                void Rail(Vector3 a, Vector3 b)
                {
                    var ab = b - a; float len = ab.magnitude;
                    var rt = Quaternion.LookRotation(ab.normalized);
                    AddBox(domeFrame, (a + b) / 2 + Vector3.up * RailH, new Vector3(0.09f, 0.09f, len), rt, UvWood);
                    AddBox(domeFrame, (a + b) / 2 + Vector3.up * (RailH * 0.52f), new Vector3(0.07f, 0.07f, len), rt, UvWood);
                    int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.95f) + 1);
                    for (int i = 0; i < n; i++)
                        AddBox(domeFrame, Vector3.Lerp(a, b, (float)i / (n - 1)) + Vector3.up * (RailH / 2), new Vector3(0.10f, RailH, 0.10f), rt, UvWood);
                }
                Rail(new Vector3(ox1 - 0.06f, roomY, oz0 + 0.06f), new Vector3(ox1 - 0.06f, roomY, oz1 - 0.06f));
                Rail(new Vector3(ox0, roomY, oz0 + 0.06f), new Vector3(ox1 - 0.06f, roomY, oz0 + 0.06f));
                Rail(new Vector3(stubX1, roomY, oz1 - 0.06f), new Vector3(ox1 - 0.06f, roomY, oz1 - 0.06f));
                // 끊긴 단 앞의 기운 난간 — 이 너머로 아래를 내려다본다.
                // 위층 바닥면(roomY)과 겹치면 구멍을 가로지르는 막대로 읽히므로 확실히 아래로 내린다
                AddBox(domeFrame, new Vector3(ox0 + 0.7f, roomY - 0.90f + 0.78f, breakZ + 0.10f), new Vector3(1.4f, 0.09f, 0.09f), Quaternion.Euler(0, 0, -11f), UvWood);
                AddBox(domeFrame, new Vector3(ox0 + 0.06f, roomY - 0.90f + 0.42f, breakZ + 0.10f), new Vector3(0.08f, 0.86f, 0.08f), Quaternion.Euler(0, 0, -12f), UvWood);
                AddBox(domeFrame, new Vector3(stubX1 - 0.06f, roomY - 0.90f + 0.38f, breakZ + 0.12f), new Vector3(0.08f, 0.80f, 0.08f), Quaternion.Euler(6f, 0, 0), UvWood);

                // 진입·추락 차단 (보이지 않는 벽). 난간은 살 사이가 벌어져 캡슐이 빠져나간다 —
                // 난간 뒤에 판을 세워 개구부 세 변을 막고, 무너진 구간에서 더 내려가지 못하게 한다
                var block = NewChild(root, "계단_차단");
                void Blk(string nm, Vector3 c, Vector3 s)
                {
                    var g2 = new GameObject(nm);
                    g2.transform.SetParent(block.transform, false);
                    g2.transform.position = c;
                    g2.AddComponent<BoxCollider>().size = s;
                    g2.isStatic = true;
                }
                Blk("무너진구간", new Vector3((ox0 + stubX1) / 2, roomY - 0.90f + 1.30f, breakZ - 0.02f), new Vector3(1.6f, 2.6f, 0.2f));
                Blk("계단_동측", new Vector3(stubX1, roomY - 0.45f, (breakZ + oz1) / 2), new Vector3(0.15f, 2.4f, oz1 - breakZ));
                Blk("개구부_동", new Vector3(ox1, roomY + 0.60f, (oz0 + oz1) / 2), new Vector3(0.15f, 1.2f, oz1 - oz0));
                Blk("개구부_남", new Vector3((ox0 + ox1) / 2, roomY + 0.60f, oz0), new Vector3(ox1 - ox0, 1.2f, 0.15f));
                Blk("개구부_북", new Vector3((stubX1 + ox1) / 2, roomY + 0.60f, oz1), new Vector3(ox1 - stubX1, 1.2f, 0.15f));

                // 아래층 등불 (선아) — 아주 약한 웜 라이트 + 낙석 판 뒤에 숨긴 불씨
                var glowGo = new GameObject("아래층_등불");
                glowGo.transform.SetParent(grpLight.transform, false);
                glowGo.transform.position = new Vector3(ox0 + 1.7f, roomY - 6.5f, oz0 + 1.5f);
                var gl = glowGo.AddComponent<Light>();
                gl.type = LightType.Point;
                gl.color = new Color(1f, 0.62f, 0.30f);
                gl.intensity = 4.4f; // 쿨 강 앰비언트에 묻히지 않게 — 낙석 판 가장자리로 새어 나오는 정도
                gl.range = 8.5f;
                Prim(glowGo.transform, "불씨", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.05f, matFlame);
                // 산란광 보조 — 낙석 판 **위쪽** 갱 벽면을 은은하게 데워
                // '깊은 아래에서 빛이 올라온다'는 인상을 만든다 (광원 자체는 판에 가려 안 보인다)
                var scatterGo = new GameObject("아래층_등불_산란");
                scatterGo.transform.SetParent(glowGo.transform, false);
                scatterGo.transform.position = new Vector3(ox0 + 2.0f, roomY - 4.0f, oz0 + 1.8f);
                var sc = scatterGo.AddComponent<Light>();
                sc.type = LightType.Point;
                sc.color = new Color(1f, 0.60f, 0.28f);
                sc.intensity = 1.7f;
                sc.range = 5.0f;
            }

            // ── 돔 단 바닥: 화강암 원반 + 좌대(혼상 자리) ──
            AddRing(floorMain, C, R + 0.25f, 0.001f, platY - 0.003f, platY - 0.003f, 64, UvFloor, true);   // 단 바닥판
            AddRing(floorMain, C, 1.3f, 0.001f, platY + 0.15f, platY + 0.15f, 48, UvFloor, true);          // 좌대 상판
            AddRing(floorMain, C, 1.3f, 1.3f, platY - 0.05f, platY + 0.15f, 48, UvFloor, false);           // 좌대 옆면
            // 무늬 (2026-08-11 정리): **작업실 바닥은 완전히 비운다** — 가구가 놓일 자리이고,
            // 동심원 상감은 바닥 그래픽처럼 읽혔다. 돔은 혼상이 고정된 관측 자리라
            // **28수 눈금 + 그것을 묶는 외곽선 하나만** 남긴다. 눈금은 장식이 아니라
            // 천구 좌표의 기준선이라 이 방이 무슨 방인지 알려주는 유일한 표시다.
            // 반대로 동심원 3겹은 순수 장식이라 같이 걷어냈다
            AddRing(floorPat, C, 3.30f, 3.18f, platY + 0.006f, platY + 0.006f, 64, UvFloor, true);
            for (int i = 0; i < 28; i++) // 28수 눈금
            {
                float ang = i * (360f / 28f);
                var d = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                AddBox(floorPat, C + d * 2.98f + Vector3.up * (platY + 0.007f), new Vector3(0.09f, 0.012f, 0.30f), Quaternion.LookRotation(d), UvFloor);
            }

            // ── 돔 (2026-08-11: 지름 8m로 축소, 완전한 반구형):
            //    벽→천장 전이부만 목조로 남기고 그 위는 매끈한 암흑 셸.
            //    혼상 별 투영 시 무한히 먼 하늘처럼 보여야 해서 서까래는 전이부 그루터기로만,
            //    천장 중앙은 비운다. 암흑 셸은 48각으로 굴곡을 없애고 반사 0의 근흑색.
            //    프로필은 타원 회전면 — 스프링라인은 드럼 안쪽면(R-0.18)보다 살짝 안쪽에서 시작해
            //    돌림띠 뒤로 물려 들어가고, 정점은 단 바닥에서 6.4m 위에 선다.
            var dProf = new Vector2[13];
            for (int i = 0; i < dProf.Length; i++)
            {
                float t = (float)i / (dProf.Length - 1) * Mathf.PI * 0.5f;
                dProf[i] = new Vector2((R - 0.18f) * Mathf.Cos(t), (drumTop - roomY - 0.12f) + DomeRise * Mathf.Sin(t));
            }
            dProf[dProf.Length - 1].x = 0.001f;
            float vArc = 0f;
            for (int k = 0; k < 2; k++) // 목조 코브 (전이부 2단)
            {
                float seg = Vector2.Distance(dProf[k], dProf[k + 1]);
                AddLoftStrip(domeShell, C, dProf[k + 1].x, roomY + dProf[k + 1].y, dProf[k].x, roomY + dProf[k].y,
                    Sides * 2, UvWood, vArc + seg, vArc); // (위→아래 순서 = 안쪽면)
                vArc += seg;
            }
            for (int i = 0; i < Sides * 2; i++) // 서까래 그루터기 — 전이부에만 낮은 부조로
            {
                float ang = domeEntry + i * (360f / (Sides * 2)) + 360f / (Sides * 4);
                var d = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                var a = C + d * (dProf[0].x + 0.03f) + Vector3.up * (roomY + dProf[0].y + 0.03f);
                var b = C + d * (dProf[2].x - 0.02f) + Vector3.up * (roomY + dProf[2].y - 0.02f);
                var ab = b - a;
                AddBox(domeFrame, (a + b) / 2, new Vector3(0.13f, ab.magnitude, 0.10f), Quaternion.FromToRotation(Vector3.up, ab.normalized), UvWood);
            }
            for (int i = 0; i < Sides; i++) // 마감 중도리 — 목조와 암흑 하늘의 경계 고리
            {
                float ang = domeEntry + (i + 0.5f) * (360f / Sides);
                var d = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                var tangent = Vector3.Cross(Vector3.up, d);
                AddBox(domeFrame, C + d * (dProf[2].x - 0.06f) + Vector3.up * (roomY + dProf[2].y + 0.02f),
                    new Vector3(0.17f, 0.20f, 2f * dProf[2].x * Mathf.Tan(Mathf.PI / Sides) + 0.1f), Quaternion.LookRotation(tangent), UvWood);
            }
            var nightProfile = new Vector2[dProf.Length - 2];
            System.Array.Copy(dProf, 2, nightProfile, 0, nightProfile.Length);
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
            //    돔이 작아졌으므로 개수는 줄이고 크기도 줄인다 — 그래도 면적당 밀도는 이전보다 높다.
            //    나중에 혼상 조작으로 켜고 끌 수 있게 그룹 하나로 묶는다
            var starDot = BuildStarDot();
            BuildStars(root, C, roomY, nightProfile, rnd, "혼상별_별빛", starDot,
                nightProfile[0].x - 0.10f, (platY - roomY) + 1.7f);

            // ── 합성 메시 저장·배치 ──
            // 콜라이더는 아래에서 박스로 따로 낸다 — 합성 메시 MeshCollider는 끼임을 만든다
            MeshGO(NewChild(root, "통로_석조"), Save(Combine(corStone), "관측실_통로석조"), matWall, false);
            MeshGO(NewChild(root, "방_석벽"), Save(Combine(roomStone), "관측실_방석조"), matWall, false);
            MeshGO(NewChild(root, "벽_석축"), Save(Combine(roomBase), "관측실_벽석축"), matBase, false);
            MeshGO(NewChild(root, "벽_회벽"), Save(Combine(roomPlaster), "관측실_벽회벽"), matPlaster, false);
            MeshGO(NewChild(root, "벽_심벽목"), Save(Combine(roomTrim), "관측실_벽심벽목"), matSimbyeok, false);
            MeshGO(NewChild(root, "작업실_마루"), Save(Combine(maru), "관측실_마루"), matMaru, false);
            MeshGO(NewChild(root, "방_바닥"), Save(Combine(floorMain), "관측실_바닥"), matFloor, false);
            MeshGO(NewChild(root, "방_바닥무늬"), Save(Combine(floorPat), "관측실_바닥무늬"), matPattern, false);
            MeshGO(NewChild(root, "돔_판재"), Save(Combine(domeShell), "관측실_돔판재"), matWood, false);
            MeshGO(NewChild(root, "돔_골조"), Save(Combine(domeFrame), "관측실_돔골조"), matWoodDark, false);
            MeshGO(NewChild(root, "돔_밤하늘"), Save(Combine(nightShell), "관측실_돔밤하늘"), matNight, false);
            foreach (var m in tempMeshes) Object.DestroyImmediate(m);
            tempMeshes.Clear();

            // ── 보행 콜라이더 (전부 Box) ──
            // ① 구조: 렌더 박스 원장을 그대로 박스 콜라이더로. 통로 바닥·벽·천장·계단 디딤판,
            //    방 석벽·문틀, 붕괴 계단실이 한 번에 덮인다 (틈 없음이 보장된다)
            var colRoot = NewChild(root, "보행콜라이더");
            var colStruct = NewChild(colRoot, "구조");
            foreach (var b in colBoxes) ColBox(colStruct.transform, "구조", b.c, b.s, b.r);
            // ② 돔 단 바닥·좌대는 로프트 원반이라 원장에 없다 — 박스로 따로 깐다.
            //    단 바닥판은 드럼 지름(8)에 맞춰 8.5×8.5. 모서리는 석벽 뒤라 닿지 않고,
            //    아치 통로 쪽(z=중심-4.25)에서 통로 바닥과 맞물린다
            ColBox(colStruct.transform, "돔_단바닥", C + Vector3.up * (platY - 0.20f), new Vector3(2f * R + 0.5f, 0.4f, 2f * R + 0.5f), Quaternion.identity);
            // 좌대(반지름 1.3 원반) — 정사각 4개를 22.5°씩 엇갈려 16각으로 근사.
            // 2개(팔각)로 하면 외접 반지름이 1.70까지 튀어나와 원반 밖 0.4m에 투명 턱이 생기고,
            // 거기 올라선 채 혼상 차단벽에 눌리면 워커가 낀다 (Play 검증에서 확인).
            // 16각은 내접 1.28 / 외접 1.305라 원반과 거의 일치한다
            for (int k = 0; k < 4; k++)
                ColBox(colStruct.transform, "좌대", C + Vector3.up * platY, new Vector3(2.56f, 0.3f, 2.56f), Quaternion.Euler(0, 22.5f * k, 0));

            // ── 조명 ──
            // ⚠️ 2026-08-11: **작업실·돔의 조명 기구(메시)는 전부 걷어냈다.** 사용자가 직접 고른
            //    에셋으로 배치할 예정이라 자리를 비워두는 게 목적이다. 빛만 남기되 광원을
            //    벽면·천장 쪽으로 올려 바닥과 가구 자리를 완전히 비운다.
            //    조명 기구를 다시 넣을 때는 이 좌표에 맞춰 걸면 밝기가 그대로 유지된다.
            //    암문 통로 사방등은 그대로 둔다 — 42m 어둠 속 길잡이라 없으면 방향을 잃는다.
            foreach (var (p, f) in sconces) Sconce(grpLight.transform, p, f, matIron, matFlame);
            Sconce(grpLight.transform, new Vector3(0.78f, 1.62f, 0.6f), Vector3.left, matIron, matFlame); // 암문 앞

            // 작업실 벽 광원 6 — 옛 사방등 자리에서 1.62 → 2.85로 올렸다.
            // 나중에 놓일 서가(2m 남짓)에 파묻히지 않는 높이이고, 벽면만 데우는 성격은 그대로다
            float sy = roomY + 2.85f;
            var warm = new Color(1f, 0.68f, 0.38f);
            foreach (var p in new[] {
                new Vector3(sx0 + 1.7f, sy, sz0 + 0.25f), new Vector3(sx1 - 2.2f, sy, sz0 + 0.25f),
                new Vector3(sx1 - 0.25f, sy, sz0 + 2.7f), new Vector3(sx1 - 0.25f, sy, sz1 - 2.6f),
                new Vector3(sx0 + 0.25f, sy, sz0 + 1.9f), new Vector3(archX - archHalf - 1.1f, sy, sz1 - 0.25f) })
                BareLight(grpLight.transform, "벽_광원", p, warm, 1.75f, 6.0f, false);

            // 방 가운데(혼천의·개구부)는 천장 쪽 광원이 맡는다. 옛 등롱 불씨 높이(roomY+3.36)에서
            // roomY+4.05로 올리고 거리 제곱만큼 세기를 키웠다 — 천장 밑 1.25m라 어떤 가구와도 안 겹친다
            float hangY = roomY + 4.05f;
            BareLight(grpLight.transform, "천장_광원", new Vector3(honSpot.x, hangY, honSpot.z), new Color(1f, 0.74f, 0.46f), 4.5f, 13f, true);
            BareLight(grpLight.transform, "천장_광원", new Vector3(sx1 - 2.9f, hangY, sz1 - 2.9f), new Color(1f, 0.74f, 0.46f), 3.8f, 12f, true);
            BareLight(grpLight.transform, "천장_광원", new Vector3(ox1 + 1.2f, hangY, oz1 - 0.9f), new Color(1f, 0.74f, 0.46f), 3.2f, 11f, false);
            // 아주 약한 전역 채움 2 — 벽 사이 어둠이 통째로 검게 죽지 않을 정도만.
            // 회벽에서는 이 둘이 벽 전체를 고르게 발라 쿨톤을 지워버린 주범이었다 (0.85 → 0.4).
            // 색도 웜에서 중성 쪽으로 당겨 쿨 앰비언트를 덮지 않게 한다
            foreach (var fp in new[] { new Vector3(sx0 + 3.6f, roomY + 3.6f, sz0 + 3.0f), new Vector3(sx1 - 3.4f, roomY + 3.6f, sz1 - 3.0f) })
                BareLight(grpLight.transform, "작업실_채움", fp, new Color(0.92f, 0.86f, 0.78f), 0.4f, 6.5f, false);
            // 돔: 별이 압도해야 하므로 드럼 벽 광원 셋만. 혼상은 자체 내부광원(2.4/사거리 7)이 있어
            // 기구를 걷어내도 형태가 살아난다. 광원은 드럼 벽에 바짝 붙여 단 바닥을 완전히 비운다
            for (int i = 0; i < 3; i++)
            {
                float ang = domeEntry + new[] { 68f, -68f, 180f }[i];
                var d = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0, Mathf.Cos(ang * Mathf.Deg2Rad));
                BareLight(grpLight.transform, "드럼_광원", C + d * (R - 0.30f) + Vector3.up * (platY + 2.35f), warm, 1.25f, 5.5f, false);
            }
            // 프로브 굽기 기준점 — 금속(혼천의)이 비출 대상은 작업실이다.
            // 눈높이보다 조금 위에서 방 전체를 내려다보게 둔다 (기물 안쪽에 들어가면 큐브맵이 통째로 암흑)
            var centerGo = new GameObject("방_중심");
            centerGo.transform.SetParent(grpLight.transform, false);
            centerGo.transform.position = new Vector3((sx0 + sx1) / 2, roomY + ShopH * 0.47f, (sz0 + sz1) / 2);

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

            // ── 혼천의 (2026-08-11 이설): 돔 좌대는 혼상 차지 — 천장 투영 광원이 돔 중심에 와야
            //    별이 고르게 퍼진다. 혼천의는 조작 대상이라 작업실 한복판 바닥 메달리온 위에 둔다.
            //    손으로 옮긴 위치는 존중하되, 이번처럼 방 형상이 통째로 바뀌면 설계 자리로 되돌린다
            //    (1m 넘게 어긋난 보존값은 옛 배치의 잔재로 본다)
            var honDesign = honSpot + Vector3.up * roomY;
            bool keepPose = hasHonPose
                            && Vector3.Distance(new Vector3(honPos.x, 0f, honPos.z), honSpot) < 1.0f
                            && Mathf.Abs(honPos.y - roomY) < 0.05f;
            var honPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/혼천의_B_장식받침.prefab");
            if (honPrefab != null)
            {
                var hon = (GameObject)PrefabUtility.InstantiatePrefab(honPrefab);
                hon.transform.SetParent(root.transform, false);
                if (keepPose) hon.transform.SetPositionAndRotation(honPos, honRot);
                else hon.transform.SetPositionAndRotation(honDesign, Quaternion.Euler(0f, 18f, 0f));
            }
            else Debug.LogWarning("[관측실] 혼천의 프리팹이 없습니다 — '혼천의 생성'을 먼저 실행하세요");

            // ── 혼상 (씬 루트 오브젝트라 이 빌더가 만들지는 않는다):
            //    돔 좌대 위가 별 투영의 기하학적 중심이므로 매번 그 자리에 맞춘다
            var honsang = FindRootIncludingInactive("혼상");
            if (honsang != null) honsang.transform.position = C + Vector3.up * (platY + 0.15f);
            else Debug.LogWarning("[관측실] 혼상이 씬에 없습니다 — '혼상 생성'을 먼저 실행하세요");

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
            // 끊긴 계단 끝 (수직갱을 내려다보는 자리)
            Marker("Exit_ToArchive", new Vector3(ox0 + 0.7f, roomY - 0.90f + 1.0f, oz1 - 0.42f * 4.5f), Quaternion.LookRotation(Vector3.back));
            Marker("Spawn_DomeCenter", C + Vector3.up * platY, Quaternion.LookRotation(Vector3.back));

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            int tris = CountTris(root);
            Debug.Log($"[관측실] 생성 완료 — 작업실 x[{sx0:F2}~{sx1:F2}] z[{sz0:F2}~{sz1:F2}] 바닥Y={roomY:F2} 천장{ShopH:F2}, " +
                      $"돔 중심 {C.x:F2},{C.z:F2} 단Y={platY:F2} 지름 {2f * R:F1} 정점Y={roomY + dProf[dProf.Length - 1].y:F2}, " +
                      $"아치 x{archX:F2} 폭{2f * archHalf:F2}, 붕괴 개구부 x[{ox0:F2}~{ox1:F2}] z[{oz0:F2}~{oz1:F2}], " +
                      $"총 {tris:n0}tri, 보행 박스 콜라이더 {colBoxes.Count + 5}개. " +
                      $"다음: [관측실 프로브 굽기] → [관측실 보행 콜라이더 구축]");
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
                probe.size = new Vector3(ShopW + 0.8f, ShopH + 0.6f, ShopD + 0.8f);   // 작업실 한 칸을 덮는다
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
        static void Sconce(Transform parent, Vector3 pos, Vector3 facing, Material iron, Material flame, float mul = 1f)
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
            l.intensity = 1.7f * mul;
            l.range = 5.5f * Mathf.Lerp(0.72f, 1f, mul);
        }

        /// <summary>기구 없는 순수 광원. 2026-08-11: 작업실·돔의 조명 기구(사방등·등롱)는
        /// 사용자가 직접 고른 에셋으로 교체할 예정이라 **메시를 전부 걷어내고 빛만 남겼다**.
        /// 광원은 벽면·천장 쪽에 두어 바닥과 가구 자리를 비운다.</summary>
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

        // ── 메시 유틸 ─────────────────────────────────────────

        /// <summary>월드 크기 비례 UV 박스를 합성 목록에 추가 (위치 해시로 UV를 어긋내 반복감 완화).
        /// uvOff를 주면 해시 대신 고정 오프셋 — **층쌓기 석축처럼 이음매에서 켜가 어긋나면 안 되는 것**에 쓴다.</summary>
        static void AddBox(List<CombineInstance> list, Vector3 center, Vector3 size, Quaternion rot, float uvScale, Vector2? uvOff = null)
        {
            colBoxes.Add((center, size, rot));
            var off = uvOff ?? new Vector2(
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
        static GameObject BuildStars(GameObject root, Vector3 C, float roomY, Vector2[] profile, System.Random rnd, string name, Texture2D dot,
            float maxR, float eyeLocalY)
        {
            var grp = NewChild(root, name);
            const string key = "B"; // B안 확정 — 에셋 이름은 비교 당시 그대로 유지 (GUID 보존)
            // 돔 지름 12→8 축소(2026-08-11)에 맞춰 개수·크기를 줄였다가,
            // 같은 날 정점을 6.4→8.2m로 올리면서 셸이 1.3배 멀어져 크기를 다시 1.25배 되돌렸다
            // (각크기 유지). 개수는 1.2배 — 멀어질수록 촘촘해야 하늘로 읽힌다
            var tiers = new (string tier, int count, float sMin, float sMax, Color emis)[] {
                    ("대", 66, 0.126f, 0.198f, new Color(1.9f, 2.2f, 2.9f)),
                    ("중", 414, 0.063f, 0.108f, new Color(1.15f, 1.35f, 1.85f)),
                    ("소", 834, 0.031f, 0.063f, new Color(0.60f, 0.72f, 1.00f)),
                    ("온", 55, 0.073f, 0.135f, new Color(2.2f, 1.6f, 0.85f)) };
            var bandN = Vector3.Normalize(new Vector3(0.80f, 0.38f, -0.44f)); // 은하수 띠 평면 법선
            var gaze = C + Vector3.up * (roomY + eyeLocalY - 0.3f); // 쿼드가 바라보는 지점 (돔 안 눈높이 언저리)
            foreach (var t in tiers)
            {
                var list = new List<CombineInstance>();
                for (int i = 0; i < t.count; i++)
                {
                    bool inBand = rnd.NextDouble() < 0.55;
                    Vector3 p = Vector3.zero;
                    for (int guard = 0; guard < 60; guard++)
                    {
                        float rr = maxR * Mathf.Sqrt((float)rnd.NextDouble());
                        float aa = (float)rnd.NextDouble() * Mathf.PI * 2f;
                        p = new Vector3(Mathf.Sin(aa), 0, Mathf.Cos(aa)) * rr + Vector3.up * (ProfileY(profile, rr) - 0.14f);
                        bool isIn = Mathf.Abs(Vector3.Dot((p - Vector3.up * eyeLocalY).normalized, bandN)) < 0.20f;
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

        /// <summary>보이지 않는 박스 콜라이더 하나. BoxCollider는 자체 회전이 없어 GO 트랜스폼으로 준다.</summary>
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
            if (t == null) Debug.LogWarning($"[관측실] 텍스처 없음: {path}");
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
