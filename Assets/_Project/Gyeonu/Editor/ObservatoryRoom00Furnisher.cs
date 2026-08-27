using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 작업실 소품 배치 — ALP Room00 **원리 재현** (2026-08-13, 멱등).
    ///
    /// 세 번째 접근이다:
    ///   1차(낙안 소품 자유 배치) → 시장통. 2차(Room00 좌표 통째 이식) → 크기 차이로 허전.
    ///   이번에는 Room00을 전수 덤프해 "분위기를 만드는 원리"를 뽑고, 그 원리대로
    ///   우리 방(10×7, Room00의 1.4배)에 맞게 **재구성**한다. 좌표는 옮기지 않는다.
    ///
    /// Room00 분석 요약 (가구 36 + 소품 282, 10×5×2.5):
    ///   ① 가장자리 포화·중앙 절제 — 가구는 전부 벽에 등을 대고, 중앙엔 작업 섬 하나뿐.
    ///      바닥의 약 70%가 비어 있고 빈 곳은 통행 고리가 된다
    ///   ② 3단 높이 구성 — 바닥(바구니·장작·항아리) / 허리(작업면, 소품 밀집) /
    ///      눈높이(벽걸이 선반 + 병 줄). 2.2m 위와 천장은 완전히 비운다
    ///   ③ 기능 군집 — 물건은 "일 단위"로 뭉친다(불 자리+장작+도끼 / 물 자리+그릇 /
    ///      서책 자리+문방). 군집 안은 정렬 ~85% + 흐트러짐 ~15% (기울어진 책, 쓰러진 초)
    ///   ④ 반복 vs 유일 — 싼 것은 대량 반복(병 수십, 서책 수십, 장작 30+), 벽마다
    ///      랜드마크 하나씩(증류기·화덕·개수대·칠판)만 유일하게 둔다
    ///   ⑤ 불빛은 작업면 위 — 방 중앙 채움 1 + 촛불·등잔을 군집마다 얹는다(세기 1, 사거리 2.4~2.7)
    ///
    /// 조선화: 명백한 서양·오컬트(플라스크·증류기·해골·표본·지구의·칠판·서양 촛불)만
    /// 같은 역할·크기의 운현궁/kcisa 기물로 교체. 선반·궤·바구니·통나무·목재 탁자는 공통이라 유지.
    /// 서책은 눕혀 쌓는다 (조선식 보관 — Room00의 세워 꽂기 대신).
    ///
    /// 낙안읍성 소품은 쓰지 않는다 (1차 실패 원인).
    /// 운현궁 조립 가구(몸체+문짝 분리)는 데모 씬의 조립본을 복제해 온다 — 피벗이 제각각이라
    /// 직접 조립하면 문짝이 어긋난다. 배치는 전부 렌더러 바운즈 기준 정렬이라 피벗과 무관.
    /// ⚠️ 데모 씬들은 추가 로드해서 읽기만 하고 저장 없이 닫는다.
    /// </summary>
    public static class ObservatoryRoom00Furnisher
    {
        const string RootName = "관측실_소품";   // HonsangController.propLightRootName과 일치 — 별밤에 등불이 함께 꺼진다
        const string UnDemo = "Assets/UnhyeongungCollect/Scenes/Demo.unity";
        const string BK = "Assets/BK_AlchemistHouse/Prefabs/";
        const string UN = "Assets/UnhyeongungCollect/Prefabs/";
        const string KC = "Assets/KTinteractiveProp/Volum 02/Prefabs/";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Observatory";

        // 작업실 벽 안쪽면 (ObservatoryBuilder 2026-08-11 개정 10×7×3.6 기준 실측)
        const float X0 = 14.5f, X1 = 24.5f, Z0 = 29.2f, Z1 = 36.2f;
        const float FloorY = -3.06f;
        // 우리 방에만 있는 요소 (배치 금지 구역)
        //   암문 문간 x[17.3~19.1] 남벽 / 붕괴 개구부 x[14.5~17.5] z[31.6~34.6] (+ 북측 통로와 계단 어귀 x[14.5~15.9])
        //   돔 아치 x[18.97~22.03] 북벽 + 계단 z[34.6~36.2] / 혼천의 (22.5, 31.4) 반폭 0.66 + 조작 공간

        static System.Random rnd;
        static Transform grpFurn, grpItem;
        static GameObject pickupCandle;    // 큰 탁자 촛대 — Build 마지막에 픽업으로 배선 (2026-08-15 등롱→촛대)
        static Material matFlame;
        static Scene target;
        static readonly Dictionary<string, GameObject> composites = new Dictionary<string, GameObject>();

        // ─────────────────────────────────────────────────────
        [MenuItem("Tools/이문록/관측실 소품 배치 (Room00 원리 재현)")]
        public static void Build()
        {
            if (SceneManager.GetActiveScene().name != "Gyeonu_Observatory")
            { Debug.LogError("[소품] 활성 씬이 Gyeonu_Observatory가 아닙니다"); return; }
            target = SceneManager.GetActiveScene();
            rnd = new System.Random(20260813);

            for (GameObject g; (g = GameObject.Find(RootName)) != null;) Object.DestroyImmediate(g);
            // 2차 시도의 비활성 잔재도 지운다
            foreach (var g in target.GetRootGameObjects())
                if (g.name == RootName) { Object.DestroyImmediate(g); break; }

            var root = new GameObject(RootName);
            grpFurn = new GameObject("가구").transform; grpFurn.SetParent(root.transform, false);
            grpItem = new GameObject("소품").transform; grpItem.SetParent(root.transform, false);
            matFlame = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_관측실_불꽃.mat");
            if (matFlame == null) Debug.LogWarning("[소품] M_관측실_불꽃.mat 없음 — 불꽃 구체 생략, 광원만 배치");

            // 운현궁 조립 가구 복제 (읽기만, 저장 없이 닫기)
            composites.Clear();
            var demo = EditorSceneManager.OpenScene(UnDemo, OpenSceneMode.Additive);
            try
            {
                Transform model = null;
                foreach (var g in demo.GetRootGameObjects()) if (g.name == "Model") model = g.transform;
                foreach (var nm in new[] { "SM_Cupboard226", "SM_ThreetieredCupboard", "SM_BookCabinet224",
                                           "SM_GrainChest", "SM_Board229", "SM_Cabinet243", "SM_CupBoard205" })
                {
                    var src = model != null ? model.Find(nm) : null;
                    if (src == null) { Debug.LogWarning("[소품] 운현궁 데모에 " + nm + " 없음"); continue; }
                    var copy = Object.Instantiate(src.gameObject);
                    SceneManager.MoveGameObjectToScene(copy, target);
                    copy.name = nm;
                    copy.SetActive(false);              // 스테이징 — Place에서 활성화
                    composites[nm] = copy;
                }
            }
            finally { EditorSceneManager.CloseScene(demo, true); }

            BuildSouthWall();
            BuildEastWall();
            BuildNorthWall();
            BuildWestWall();
            BuildCenter();

            // 스테이징에 남은 미사용 복제본 정리
            foreach (var kv in composites) if (kv.Value != null && !kv.Value.activeSelf) Object.DestroyImmediate(kv.Value);

            // 콜라이더 — 크고 높은 가구만 박스. 작고 낮은 소품은 없음 (끼임 방지)
            int solids = 0;
            foreach (var col in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            foreach (Transform c in grpFurn)
            {
                var b = BoundsOf(c.gameObject);
                if (b.size.y < 0.55f || b.size.x * b.size.z < 0.09f) continue;
                var cg = new GameObject("차단");
                cg.transform.SetParent(c, true);
                cg.transform.SetPositionAndRotation(b.center, Quaternion.identity);
                cg.AddComponent<BoxCollider>().size = b.size;
                cg.isStatic = true;
                solids++;
            }

            int tri = 0, items = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) tri += mf.sharedMesh.triangles.Length / 3;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true)) mr.gameObject.isStatic = true;
            foreach (Transform c in grpItem) items++;
            int lights = root.GetComponentsInChildren<Light>(true).Length;

            // 큰 탁자 촛대 = 집는 기물 (2026-08-15 등롱→촛대 교체) — 혼상 점등 흐름의 열쇠.
            // 손에 들려 움직이므로 정적 배칭에서 빼고, 조준 콜라이더와 LanternPickup을 단다.
            // 집기 게이트는 LanternPickup.PickupAllowed — 혼상 안내가 뜨기 전에는 상호작용이 안 뜬다
            if (pickupCandle != null)
            {
                foreach (var mr in pickupCandle.GetComponentsInChildren<MeshRenderer>(true))
                    mr.gameObject.isStatic = false;
                var pcol = pickupCandle.AddComponent<SphereCollider>();
                pcol.center = new Vector3(0f, 0.20f, 0f);   // 촛대 높이 0.40 — 몸통 중앙
                pcol.radius = 0.22f;
                var pk = pickupCandle.AddComponent<IMUNROK.Gyeonu.LanternPickup>();
                pk.displayName = "촛대";
            }

            EditorSceneManager.MarkSceneDirty(target);
            Debug.Log($"[소품] Room00 원리 재현 완료 — 가구 {grpFurn.childCount}, 소품 {items}, " +
                      $"불빛 {lights}, 박스 콜라이더 {solids}, 총 {tri:n0}tri");
        }

        // ── 남벽: (서) 장롱 쌍 → 문간 → (동) 반닫이·기대 세운 판 → 높은 장 → 화로 → 장작 ──
        // Room00 남벽 원리: 수납 가구를 어깨 맞대 줄 세우고, 불 자리(화덕+장작+도끼)를 끝에 몬다
        static void BuildSouthWall()
        {
            // 서편 (x14.5~17.3): 장롱 2가 어깨를 맞댄다 (Room00 WoodCupboard02 쌍 = 폭 2.3 / 높이 1.9)
            var cb = PlaceWall(SpawnUn("SM_Cupboard226"), 'S', 15.15f, 0f);
            var tc = PlaceWall(SpawnUn("SM_ThreetieredCupboard"), 'S', 16.45f, 0f);
            // 그 위 — Room00은 장 위에 궤짝·책 더미·항아리를 얹는다 (머리 위 높이대는 비움)
            PlaceOn(Spawn(BK + "Items/Misc/Trunk01.prefab", grpItem), 15.0f, 29.5f, Top(cb), 12f); // 긴 축을 벽과 나란히 — 96°면 남벽을 8cm 뚫는다
            StackBooks(15.35f, 29.5f, Top(cb), 3, 90f);
            PlaceOn(Spawn(UN + "SM_BlackGlazedJar.prefab", grpItem), 16.2f, 29.5f, Top(tc), 0f);
            PlaceOn(Spawn(UN + "SM_WhitePorcelainBottle.prefab", grpItem), 16.7f, 29.45f, Top(tc), 0f);

            // 문간 동편 (x19.1~): 반닫이 + 위에 소반·그릇, 벽에 기대 세운 다듬은 판 (Room00 Drawer03 + 칠판 기대 세움)
            var box = PlaceWall(Spawn(UN + "SM_Box286.prefab", grpFurn), 'S', 19.75f, 0f);
            PlaceOn(Spawn(UN + "SM_WoodenTray322.prefab", grpItem), 19.55f, 29.5f, Top(box), 4f);
            PlaceOn(Spawn(UN + "SM_RiceBowls104.prefab", grpItem), 19.55f, 29.48f, Top(box) + 0.075f, 0f);
            PlaceOn(Spawn(UN + "SM_PorcelainCoveredBowl.prefab", grpItem), 19.95f, 29.52f, Top(box), 0f);
            // 기대 세운 상(1.5m)은 반닫이~높은장 틈(0.75m)에 안 들어간다 — 북벽 통로 쪽으로 옮겼다.
            // 빈 틈은 되(말)로 채운다
            Place(Spawn(UN + "SM_Mal.prefab", grpItem), 20.55f, 29.48f, FloorY, 15f);

            // 높은 장 (Room00 WoodCupboard03 2.2m — 남벽의 세로 랜드마크)
            var tall = PlaceWall(SpawnUn("SM_Cabinet243"), 'S', 21.35f, 0f);
            PlaceOn(Spawn(UN + "SM_Bowls100.prefab", grpItem), 21.2f, 29.5f, Top(tall), 0f);
            PlaceOn(Spawn(UN + "SM_BlackGlazedJar.prefab", grpItem), 21.6f, 29.48f, Top(tall), 0f);

            // 불 자리 (Room00 StoveLit → 화로. 남벽의 유일한 불빛, 붉은 기 도는 색)
            var br = Place(Spawn(UN + "SM_Brazier.prefab", grpFurn), 22.5f, 29.75f, FloorY, new Vector3(0, 30f, 0));
            PlaceOn(Spawn(UN + "SM_Pot154.prefab", grpItem), 22.5f, 29.75f, Top(br) - 0.02f, 0f);
            AddFlame(br.transform, "화로_불씨", new Vector3(22.5f, Top(br) + 0.06f, 29.75f),
                new Color(1f, 0.52f, 0.12f), 1.0f, 1.7f, 0.05f);
            PlaceOn(Spawn(UN + "SM_PotForMedicines.prefab", grpItem), 21.9f, 30.28f, FloorY, 18f);
            // 화롯가 걸상 — 불 곁에 앉던 자리 (원래 작업대 모서리에 박혀 있던 것을 옮겼다)
            Place(Spawn(UN + "SM_Stool254.prefab", grpFurn), 23.15f, 30.35f, FloorY, 168f);

            // 장작 더미 + 도끼 (Room00: 화덕 옆 통나무 30여 개 + 도끼).
            // 구석(x>23.95)까지 밀면 물항아리·찬장을 뚫는다
            Pile(new[] { BK + "Items/Misc/Log01.prefab", BK + "Items/Misc/Log02.prefab", BK + "Items/Misc/Log03.prefab" },
                23.15f, 23.85f, 29.4f, 29.85f, 3, 5, 90f);   // x1을 물항아리(24.11~)에서 띄운다
            Place(Spawn(BK + "Items/Weapons/Axe01.prefab", grpItem), 23.05f, 29.55f, FloorY + 0.02f, new Vector3(34f, 10f, 176f));
        }

        // ── 동벽 (개구부 없는 7m — Room00 동벽 원리 그대로): 남→북으로 물 자리 / 선반·병 벽 / 작업대 ──
        static void BuildEastWall()
        {
            // 물 자리 (Room00 TableSink 역할 — 낮은 찬장 + 대야·국자, 아래 바구니, 곁에 물항아리)
            var sink = PlaceWall(SpawnUn("SM_CupBoard205"), 'E', 30.3f, 0f);
            PlaceOn(Spawn(UN + "SM_Washbasin.prefab", grpItem), 24.2f, 30.42f, Top(sink), 0f);
            PlaceOn(Spawn(UN + "SM_Ladle.prefab", grpItem), 24.13f, 30.55f, Top(sink), 100f); // 대야에 걸쳐 둔 국자 (이 둘만 의도적 접촉)
            var cd = PlaceOn(Spawn(KC + "Lantern.prefab", grpItem), 24.28f, 29.98f, Top(sink), 0f); // 상판 남단 — 밖으로 밀면 반쯤 허공에 뜬다
            AddFlame(cd.transform, "등불", new Vector3(24.28f, Top(sink) + 0.24f, 29.98f), new Color(0.93f, 0.79f, 0.60f), 1.0f, 2.5f, 0.03f);
            // ⚠️ BK Jar01(Organic)은 Custom/Refraction 셰이더 — URP 렌더를 통째로 깨뜨린다 (2026-08-13 실측).
            //    물항아리는 흑유 항아리를 키워 쓴다
            var jar1 = Spawn(UN + "SM_BlackGlazedJar.prefab", grpItem);
            jar1.transform.localScale = Vector3.one * 1.9f;
            Place(jar1, 24.25f, 29.6f, FloorY, Yaw());
            Place(Spawn(BK + "Items/Misc/Basket01.prefab", grpItem), 23.7f, 30.55f, FloorY, Yaw());
            Place(Spawn(BK + "Items/Misc/Basket03.prefab", grpItem), 23.75f, 31.05f, FloorY, Yaw());
            // 물 자리 위 선반 2단 + 백자 병 줄 (Room00: 개수대 위 선반 2단 + 유리병 줄 → 백자·도기로)
            var s1 = ShelfE(30.3f, FloorY + 1.32f);
            var s2 = ShelfE(30.3f, FloorY + 1.70f);
            RowZ(new[] { UN + "SM_WhitePorcelainBottle.prefab", UN + "SM_DarkBrownBottle.prefab",
                         UN + "SM_WhitePorcelainBottle.prefab", UN + "SM_BlackGlazedJar.prefab" },
                24.34f, 29.55f, 31.05f, Surface(s1), 8);   // 선반당 8개 — Room00 실측 밀도 7.7 이상 유지
            RowZ(new[] { UN + "SM_WhitePorcelainDish194.prefab", UN + "SM_RiceBowls104.prefab",
                         UN + "SM_WhitePorcelainCup.prefab", UN + "SM_DarkBrownSmallCup.prefab" },
                24.36f, 29.6f, 31.0f, Surface(s2), 9);

            // 선반·병의 벽 (Room00 동벽 중단: 선반 3단 + 서랍장 + 병·책·초 밀집)
            var s3 = ShelfE(32.7f, FloorY + 0.98f);
            var s4 = ShelfE(32.7f, FloorY + 1.36f);
            var s5 = ShelfE(32.7f, FloorY + 1.74f);
            RowZ(new[] { UN + "SM_DarkBrownBottle.prefab", UN + "SM_WhitePorcelainBottle.prefab",
                         UN + "SM_PorcelainCoveredBowl.prefab", UN + "SM_WhitePorcelainBottle.prefab" },
                24.34f, 31.95f, 33.45f, Surface(s3), 8);
            StackBooks(24.3f, 31.98f, Surface(s4), 3, 90f);
            RowZ(new[] { UN + "SM_WhitePorcelainBowl.prefab", UN + "SM_SoupBowl_B.prefab",
                         UN + "SM_CookieBowl352.prefab" }, 24.35f, 32.55f, 33.4f, Surface(s4), 5);
            StackBooks(24.3f, 32.85f, Surface(s5), 4, 92f);
            RowZ(new[] { UN + "SM_WhitePorcelainCup.prefab", UN + "SM_DarkBrownSmallCup.prefab",
                         UN + "SM_WhitePorcelainBottle.prefab" }, 24.35f, 31.95f, 32.5f, Surface(s5), 3);
            var cd2 = PlaceOn(Spawn(UN + "SM_Candlestick128.prefab", grpItem), 24.32f, 33.35f, Surface(s5), 0f);
            AddFlame(cd2.transform, "촛불", new Vector3(24.32f, Top(cd2) + 0.01f, 33.35f), new Color(0.98f, 0.62f, 0.40f), 1.0f, 2.4f, 0.035f);
            // 선반 아래 반닫이 + 차 도구 (Room00 Drawer02 자리)
            var box2 = PlaceWall(Spawn(UN + "SM_Box286.prefab", grpFurn), 'E', 32.7f, 0f);
            PlaceOn(Spawn(UN + "SM_WoodenTray336.prefab", grpItem), 24.12f, 32.55f, Top(box2), 12f); // 24.18은 동벽을 1cm 뚫는다
            PlaceOn(Spawn(UN + "SM_Teapot_Body.prefab", grpItem), 24.12f, 32.5f, Top(box2) + 0.085f, 40f);
            PlaceOn(Spawn(UN + "SM_Cup28.prefab", grpItem), 24.22f, 32.68f, Top(box2) + 0.085f, 0f);
            PlaceOn(Spawn(UN + "SM_WhitePorcelainDish195.prefab", grpItem), 24.05f, 32.9f, Top(box2), 0f);
            Place(Spawn(BK + "Items/Misc/Basket02.prefab", grpItem), 23.6f, 31.85f, FloorY, Yaw()); // 작업대 다리·혼천의 바운즈 밖

            // 작업대 (Room00 북벽 작업대들의 이식 — 서책은 세우지 않고 눕혀 쌓는다, 조선식)
            var wb = PlaceWall(Spawn(BK + "Furniture/Workbench.prefab", grpFurn), 'E', 34.2f, 0f);
            StackBooks(24.2f, 33.6f, Top(wb), 4, 88f);
            StackBooks(24.25f, 34.05f, Top(wb), 6, 92f);
            StackBooks(24.15f, 34.5f, Top(wb), 3, 85f);
            Place(Spawn(BK + "Items/Books/Book08.prefab", grpItem), 24.25f, 34.75f, Top(wb), new Vector3(90f, 104f, 0)); // 펼쳐 둔 책
            var lan = PlaceOn(Spawn(KC + "Lantern.prefab", grpItem), 24.33f, 34.9f, Top(wb), 0f);
            AddFlame(lan.transform, "등불", new Vector3(24.33f, Top(wb) + 0.24f, 34.9f), new Color(0.93f, 0.79f, 0.60f), 1.0f, 2.7f, 0.03f);
            PlaceOn(Spawn(BK + "Items/Ingredients/ClutterBowl03Ing02.prefab", grpItem), 24.2f, 34.62f, Top(wb), Yaw());
            Place(Spawn(BK + "Items/Books/Books2.prefab", grpItem), 24.12f, 34.3f, FloorY, 0f);  // 작업대 밑 책 더미 (yaw 90이면 동벽을 29cm 뚫는다)
            // 눈높이 랜드마크 (Room00 칠판 자리, 2026-08-13 사용자 결정 순서 적용):
            // 운현궁·kcisa에 족자·서화·현판류 벽걸이 없음(발 0.45m뿐) → 2안 채택 —
            // 서가를 눈높이(1.66m) 사방탁자형(SM_Shelves241)으로 세우고 맨 윗단에 병·서책을 올린다
            var lm = PlaceWall(Spawn(UN + "SM_Shelves241.prefab", grpFurn), 'E', 35.6f, 0f);
            RowZ(new[] { UN + "SM_WhitePorcelainBottle.prefab", UN + "SM_BlackGlazedJar.prefab",
                         UN + "SM_DarkBrownBottle.prefab" }, 24.33f, 35.15f, 35.95f, Top(lm), 3);
            StackBooks(24.14f, 35.4f, Top(lm), 2, 90f);
        }

        // ── 북벽: (서) 문갑 — (아치) — (동) 책장. 아치가 새 랜드마크라 가구는 양옆에서 받친다 ──
        static void BuildNorthWall()
        {
            // 서편 x17.5~18.97 (개구부 곁 좁은 벽): 문갑 + 위에 촛대·병·책 (Room00 북벽 중단 궤 자리)
            var mg = PlaceWall(SpawnUn("SM_Board229"), 'N', 18.2f, 0f);
            var cd = PlaceOn(Spawn(UN + "SM_Candlestick128.prefab", grpItem), 17.9f, 35.95f, Top(mg), 0f);
            AddFlame(cd.transform, "촛불", new Vector3(17.9f, Top(cd) + 0.01f, 35.95f), new Color(0.98f, 0.62f, 0.40f), 1.0f, 2.4f, 0.035f);
            PlaceOn(Spawn(UN + "SM_WhitePorcelainBottle.prefab", grpItem), 18.25f, 35.93f, Top(mg), 0f);
            StackBooks(18.55f, 35.95f, Top(mg), 2, 88f);

            // 동편 x22.03~24.5: 책장 (Room00 북벽 높은 장 자리 — 관측실의 정체를 말하는 랜드마크)
            var bc = PlaceWall(SpawnUn("SM_BookCabinet224"), 'N', 23.35f, 0f);
            PlaceOn(Spawn(UN + "SM_BlackGlazedJar.prefab", grpItem), 23.0f, 35.95f, Top(bc), 0f);
            // 책장 앞 좌식 서안 + 방석 (Room00 작업대+걸상의 좌식 번안 — 유일한 좌식 자리)
            var sa = Place(Spawn(UN + "SM_TongyeongTable.prefab", grpFurn), 23.2f, 35.0f, FloorY, new Vector3(0, 8f, 0));
            PlaceOn(Spawn(UN + "SM_BookCase305.prefab", grpItem), 23.1f, 35.05f, Top(sa), 6f);   // 서견대
            PlaceOn(Spawn(UN + "SM_WhitePorcelainCup.prefab", grpItem), 23.5f, 34.9f, Top(sa), 0f);
            Place(Spawn(UN + "SM_FloorCusion.prefab", grpItem), 23.15f, 34.35f, FloorY, 4f);
            StackBooks(23.95f, 34.85f, FloorY, 5, 95f);   // 서안 곁 바닥 책 더미

            // 아치 계단 곁 항아리 (작업대 다리 속에 박혀 있던 것을 옮겼다)
            var jar2 = Spawn(UN + "SM_BlackGlazedJar.prefab", grpItem);
            jar2.transform.localScale = Vector3.one * 1.7f;
            Place(jar2, 22.6f, 34.75f, FloorY, Yaw());

            // 개구부 북측 통로 (x14.5~17.5): 계단 어귀(x<15.9)는 완전히 비우고, 벽에 붙는 가벼운 것만.
            // 기대 세운 상(남벽에서 이사) — 위 끝을 북벽에 기대고 아래가 통로로 0.4쯤 나온다
            Place(Spawn(UN + "SM_ChoppingBoard.prefab", grpItem), 16.65f, 35.99f, FloorY, new Vector3(75f, 0f, 0f));
            Place(Spawn(BK + "Items/Misc/Basket02.prefab", grpItem), 17.25f, 35.55f, FloorY, Yaw());
            Place(Spawn(UN + "SM_WoodenSmoothingRollerw.prefab", grpItem), 15.2f, 35.93f, FloorY, new Vector3(0, 4f, 0));
        }

        // ── 서벽 남단 z29.2~31.6 (Room00 서벽 원리: 계단 밑 장작 벽 — 우리는 붕괴 개구부 곁): 뒤주 + 장작 ──
        static void BuildWestWall()
        {
            // 뒤주는 개구부 쪽(북), 장작은 장롱 쪽(남) — 반대로 두면 뒤주가 남서 장롱과 반쯤 합체한다 (2026-08-13 검출)
            var gc = PlaceWall(SpawnUn("SM_GrainChest"), 'W', 30.9f, 0f);
            PlaceOn(Spawn(UN + "SM_ScoopedWoodenVessel.prefab", grpItem), 14.93f, 30.65f, Top(gc), 15f);
            PlaceOn(Spawn(UN + "SM_Mal.prefab", grpItem), 14.95f, 31.22f, Top(gc), 40f);
            // 장작은 장롱(z≤29.79)과 뒤주(z≥30.31) 사이 0.5m 틈 — 세로로 눕히면 양쪽을 찌른다.
            // 자른 단면이 방을 보게 가로로(yaw 90) 쌓는다
            Pile(new[] { BK + "Items/Misc/Log01.prefab", BK + "Items/Misc/Log02.prefab", BK + "Items/Misc/Log03.prefab" },
                14.72f, 14.85f, 29.92f, 30.18f, 3, 4, 90f);   // 중심을 벽에서 띄운다 — 통나무 반 길이가 0.22라 14.55면 벽을 뚫는다
        }

        // ── 중앙: 작업 섬 (Room00 원리 그대로 — 탁자 둘 붙임 + 걸상 + 작업면 소품 밀집) + 서편 소반 ──
        static void BuildCenter()
        {
            // 섬 — 혼천의(22.5, 31.4)와 돔 계단(z≥34.6) 사이. 문→아치 동선(x18~20.5)의 동쪽
            var t1 = Place(Spawn(BK + "Furniture/table03.prefab", grpFurn), 21.9f, 33.35f, FloorY, 0f);
            // 곁상은 kcisa 낮은 상(0.39m) — 입식 작업대 사이에 좌식을 하나 섞는다 (2026-08-13 사용자 결정 ②)
            var t2 = Place(Spawn(KC + "Tabletop 01.prefab", grpFurn), 23.05f, 33.4f, FloorY, 90f);
            // 큰 탁자 위 — 관측 기록 자리 (서책·문방·촛대·약저울 대신 그릇, 정렬 85%)
            Place(Spawn(BK + "Items/Books/Book08.prefab", grpItem), 21.55f, 33.3f, Top(t1), new Vector3(90f, 285f, 0)); // 펼친 책
            StackBooks(22.05f, 33.55f, Top(t1), 3, 8f);
            // 이 촛대가 집는 기물이다 (2026-08-15 — 곁상 등롱에서 교체). 혼상 점등의 열쇠
            var cd = PlaceOn(Spawn(UN + "SM_Candlestick128.prefab", grpItem), 22.35f, 33.15f, Top(t1), 0f);
            AddFlame(cd.transform, "촛불", new Vector3(22.35f, Top(cd) + 0.01f, 33.15f), new Color(0.98f, 0.62f, 0.40f), 1.0f, 2.5f, 0.035f);
            pickupCandle = cd;
            PlaceOn(Spawn(UN + "SM_PotForMedicines.prefab", grpItem), 21.75f, 33.6f, Top(t1), 65f);
            PlaceOn(Spawn(BK + "Items/Ingredients/ClutterBowl03Ing05.prefab", grpItem), 21.4f, 33.55f, Top(t1), Yaw());
            PlaceOn(Spawn(BK + "Items/Ingredients/ClutterBowl03Ing03b.prefab", grpItem), 22.32f, 33.7f, Top(t1), Yaw());
            PlaceOn(Spawn(UN + "SM_WoodenBowl_B.prefab", grpItem), 21.95f, 33.1f, Top(t1), 0f);
            PlaceOn(Spawn(UN + "SM_Cup28.prefab", grpItem), 22.18f, 33.28f, Top(t1), 0f);
            Place(Spawn(UN + "SM_Cup28.prefab", grpItem), 21.7f, 33.12f, Top(t1) + 0.012f, new Vector3(90f, Yaw(), 0f)); // 쓰러진 잔 (흐트러짐 15%)
            // 작은 탁자 위 — 찻주전자·잔 (곁상 등롱은 2026-08-15 제거 — 집는 기물이 큰 탁자 촛대로 바뀌었다)
            PlaceOn(Spawn(UN + "SM_Teapot_Body.prefab", grpItem), 22.8f, 33.55f, Top(t2), 20f);
            PlaceOn(Spawn(UN + "SM_DarkBrownSmallCup.prefab", grpItem), 23.1f, 33.25f, Top(t2), 0f);
            // 걸상 (Room00: 섬 둘레 3~4개, 앉던 각도 그대로 어긋나 있다)
            Place(Spawn(BK + "Furniture/WoodStool.prefab", grpFurn), 21.5f, 32.62f, FloorY, 12f);
            Place(Spawn(BK + "Furniture/WoodStool.prefab", grpFurn), 22.75f, 32.7f, FloorY, 351f);
            Place(Spawn(BK + "Furniture/WoodStool.prefab", grpFurn), 21.35f, 34.05f, FloorY, 205f);
            // 큰 탁자 밑 책 더미 (Room00: 탁자 밑에도 쓰던 것이 내려앉아 있다 — 바닥에 두면 어디든 동선·가구와 겹친다)
            Place(Spawn(BK + "Items/Books/Books5.prefab", grpItem), 22.0f, 33.4f, FloorY, 100f);
            Place(Spawn(BK + "Items/Books/Book11.prefab", grpItem), 21.15f, 32.9f, FloorY, new Vector3(90f, 130f, 0)); // 떨어진 책 한 권

            // 소반 자리 (Room00 서편 소탁 — 차 마시던 구석). 원래 개구부 난간 곁이었으나
            // 그 자리는 간의(GanuiBuilder)가 차지한다 — 남서 문간 곁 구석으로 이동
            var hb = Place(Spawn(UN + "SM_TableTigersLegs.prefab", grpFurn), 16.9f, 30.5f, FloorY, 6f);
            PlaceOn(Spawn(UN + "SM_DarkBrownBottle.prefab", grpItem), 16.8f, 30.45f, Top(hb), 0f);
            PlaceOn(Spawn(UN + "SM_DarkBrownSmallCup.prefab", grpItem), 17.05f, 30.6f, Top(hb), 0f);
            Place(Spawn(BK + "Items/Books/Book05.prefab", grpItem), 17.0f, 30.35f, Top(hb), new Vector3(90f, Yaw(), 0f));
            Place(Spawn(UN + "SM_FloorCusion.prefab", grpItem), 16.9f, 31.15f, FloorY, 2f);
        }

        // ── 배치 유틸 ─────────────────────────────────────────

        /// <summary>프리팹 인스턴스화 + 팩 부속(콜라이더·스크립트·애니메이터·기존 광원) 제거.</summary>
        static GameObject Spawn(string path, Transform parent)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (pf == null)
            {
                Debug.LogError("[소품] 프리팹 없음: " + path);
                var miss = new GameObject("누락_" + System.IO.Path.GetFileNameWithoutExtension(path));
                miss.transform.SetParent(parent, false);   // 루트에 남기면 재실행 정리에서 빠진다
                return miss;
            }
            var go = (GameObject)Object.Instantiate(pf);
            go.name = pf.name;
            go.transform.SetParent(parent, false);
            Strip(go);
            // ⚠️ BK 프리팹 루트에는 회전 (270,0,0)이 구워져 있다 (Z-up 메시 보정).
            //    그래서 배치 회전은 반드시 **합성**해야 한다 — 덮어쓰면 탁자가 모로 서고
            //    서책이 모서리로 선다 (2026-08-13 1차 실행에서 실제 발생)
            return go;
        }

        static GameObject Spawn(string path) { return Spawn(path, grpFurn); }

        /// <summary>운현궁 조립 복제본 꺼내기 (문짝·서랍 정렬 유지).</summary>
        static GameObject SpawnUn(string name)
        {
            if (!composites.TryGetValue(name, out var go) || go == null)
            {
                Debug.LogError("[소품] 조립본 없음: " + name);
                var miss = new GameObject("누락_" + name);
                miss.transform.SetParent(grpFurn, false);
                return miss;
            }
            go.SetActive(true);
            go.transform.SetParent(grpFurn, true);
            Strip(go);
            composites[name] = null;
            return go;
        }

        static void Strip(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(c.gameObject);
            foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(c.gameObject);
        }

        static Bounds BoundsOf(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            return b;
        }

        static float Top(GameObject go) { return BoundsOf(go).max.y; }
        /// <summary>BK 벽걸이 선반의 실사용 면 — 바운즈 상단이 아니라 널판 윗면 (Room00 실측 min+0.19).</summary>
        static float Surface(GameObject shelf) { return BoundsOf(shelf).min.y + 0.19f; }
        static float Yaw() { return (float)rnd.NextDouble() * 360f; }

        /// <summary>회전 먼저 → 렌더러 바운즈로 정렬: 바닥면 y, 중심 xz. 피벗이 어디 있든 동작한다.
        /// 회전은 기존(Z-up 보정) 위에 합성한다 — 덮어쓰면 보정이 날아간다.</summary>
        static GameObject Place(GameObject go, float x, float z, float bottomY, Vector3 euler)
        {
            go.transform.rotation = Quaternion.Euler(euler) * go.transform.rotation;
            var b = BoundsOf(go);
            go.transform.position += new Vector3(x - b.center.x, bottomY - b.min.y, z - b.center.z);
            return go;
        }

        static GameObject Place(GameObject go, float x, float z, float bottomY, float yaw)
        { return Place(go, x, z, bottomY, new Vector3(0, yaw, 0)); }

        static GameObject PlaceOn(GameObject go, float x, float z, float surfaceY, float yaw)
        { return Place(go, x, z, surfaceY, new Vector3(0, yaw, 0)); }

        /// <summary>벽에 등을 붙인다 — 회전 후 바운즈 깊이를 재서 뒷면을 벽에서 gap+0.02만 띄운다.</summary>
        static GameObject PlaceWall(GameObject go, char wall, float along, float gap)
        {
            float yaw = wall == 'S' ? 0f : wall == 'N' ? 180f : wall == 'E' ? 270f : 90f;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0) * go.transform.rotation;
            var b = BoundsOf(go);
            float g = gap + 0.02f;
            float x, z;
            if (wall == 'S') { x = along; z = Z0 + g + b.size.z / 2; }
            else if (wall == 'N') { x = along; z = Z1 - g - b.size.z / 2; }
            else if (wall == 'E') { x = X1 - g - b.size.x / 2; z = along; }
            else { x = X0 + g + b.size.x / 2; z = along; }
            go.transform.position += new Vector3(x - b.center.x, FloorY - b.min.y, z - b.center.z);
            return go;
        }

        /// <summary>동벽 벽걸이 선반 (BK WoodShelf01, 공통 기물이라 유지 — Room00 눈높이대의 뼈대).</summary>
        static GameObject ShelfE(float z, float bottomY)
        {
            var s = Spawn(BK + "Furniture/WoodShelf01.prefab", grpFurn);
            s.transform.rotation = Quaternion.Euler(0, 270f, 0) * s.transform.rotation;
            var b = BoundsOf(s);
            s.transform.position += new Vector3((X1 - 0.02f - b.size.x / 2) - b.center.x, bottomY - b.min.y, z - b.center.z);
            return s;
        }

        /// <summary>선반 병 줄 — Room00 원리: 같은 계열을 줄 세우되 간격·각도를 조금씩 흐트러뜨린다.</summary>
        static void RowZ(string[] paths, float x, float z0, float z1, float surfaceY, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float z = Mathf.Lerp(z0, z1, n == 1 ? 0.5f : (float)i / (n - 1)) + ((float)rnd.NextDouble() - 0.5f) * 0.05f;
                var go = Spawn(paths[i % paths.Length], grpItem);
                Place(go, x + ((float)rnd.NextDouble() - 0.5f) * 0.04f, z, surfaceY, Yaw());
            }
        }

        /// <summary>서책은 눕혀 쌓는다 (조선식) — 켜마다 살짝 어긋난 각도.</summary>
        static void StackBooks(float x, float z, float surfaceY, int n, float yawBase)
        {
            string[] books = { "Book02", "Book04", "Book05b", "Book06c", "Book07", "Book09d", "Book10", "Book12", "Book13" };
            float y = surfaceY;
            for (int i = 0; i < n; i++)
            {
                var go = Spawn(BK + "Items/Books/" + books[rnd.Next(books.Length)] + ".prefab", grpItem);
                // 단권 서책 루트 회전은 (0,0,90) — 모서리로 선 저작이라 X축 90을 더해 눕힌다
                Place(go, x + ((float)rnd.NextDouble() - 0.5f) * 0.03f, z + ((float)rnd.NextDouble() - 0.5f) * 0.03f,
                    y, new Vector3(90f, yawBase + ((float)rnd.NextDouble() - 0.5f) * 24f, 0f));
                y = BoundsOf(go).max.y;
            }
        }

        /// <summary>장작 더미 — 층층이 쌓되 층마다 조금씩 흐트러진다 (Room00 통나무 더미 방식).</summary>
        static void Pile(string[] paths, float x0, float x1, float z0, float z1, int layers, int perLayer, float yawBase)
        {
            for (int L = 0; L < layers; L++)
                for (int i = 0; i < perLayer; i++)
                {
                    var go = Spawn(paths[rnd.Next(paths.Length)], grpItem);
                    float x = Mathf.Lerp(x0, x1, (float)rnd.NextDouble());
                    float z = Mathf.Lerp(z0, z1, perLayer == 1 ? 0.5f : (float)i / (perLayer - 1)) + ((float)rnd.NextDouble() - 0.5f) * 0.08f;
                    // BK 통나무는 "서 있는 장작"으로 저작돼 있다 — X축 90을 더해 눕힌다
                    Place(go, x, z, FloorY + L * 0.15f,
                        new Vector3(90f + ((float)rnd.NextDouble() - 0.5f) * 14f, yawBase + ((float)rnd.NextDouble() - 0.5f) * 28f, ((float)rnd.NextDouble() - 0.5f) * 14f));
                }
        }

        /// <summary>불꽃 구체 + 포인트 라이트 (Room00 촛불 값: 세기 1, 사거리 2.4~2.7).
        /// 루트가 관측실_소품이라 HonsangController가 별밤에 함께 꺼 준다.</summary>
        static void AddFlame(Transform parent, string name, Vector3 pos, Color col, float intensity, float range, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            if (matFlame != null)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "불꽃";
                Object.DestroyImmediate(s.GetComponent<Collider>());
                s.transform.SetParent(go.transform, false);
                s.transform.localScale = Vector3.one * size;
                s.GetComponent<MeshRenderer>().sharedMaterial = matFlame;
            }
            var lg = new GameObject("불빛");
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = new Vector3(0, 0.05f, 0);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = col;
            l.intensity = intensity;
            l.range = range;
        }
    }
}
