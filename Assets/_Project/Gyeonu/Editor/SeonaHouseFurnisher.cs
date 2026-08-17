using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Gyeonu;
using static IMUNROK.Gyeonu.Editor.SeonaHouseLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 선아 집 소품 배치. 멱등 — 루트 `선아집_소품`을 지우고 다시 만든다.
    ///
    /// 【이 집의 톤】 관측실처럼 어수선하지도, 집무실처럼 관청스럽지도 않다(사용자 지시).
    ///    **살림집인데 사람이 갑자기 없어진 집**이다. 정돈은 돼 있되 생활이 한가운데서 멈췄고,
    ///    아버지 방만 뒤진 흔적이 남아 있다. 먼지·거미줄은 넣지 않는다 — 며칠 전까지 사람이 있었다.
    ///
    ///    · 선아 방  — 요가 깔린 채, 소반에 그릇이 그대로, 방석이 비뚤다 (급히 나간 티)
    ///    · 아버지 방 — 서안·문갑·서책. 바닥에 서책이 흩어져 있다 (선아가 뒤진 흔적)
    ///    · 대청     — 뒤주·항아리·돗자리만. 통로라 비워 둔다
    ///
    /// 【배치는 전부 바운즈 실측 스냅】(`Place`) — 팩마다 피벗 규약이 다르다.
    ///    운현궁/kcisa = 밑면 피벗, Joseon Library = 중심 피벗. y를 직접 주면 절반이 묻힌다.
    ///
    /// ⚠️ **바닥 높이를 상수로 쓰면 안 된다.** 이 집 마루는 북서 1.07 → 남동 0.92 로 기울어 있다.
    ///    `FloorAt(x,z)`가 그 자리의 마루널 윗면을 실제로 읽어 온다.
    ///
    /// ⚠️ 조립 가구(문짝·서랍이 별 프리팹)는 **운현궁 데모 씬의 조립본을 복제**해 온다.
    ///    직접 조립하면 문짝이 어긋난다(관측실에서 확인된 결론). 데모 씬은 **저장하지 않고** 닫는다.
    /// </summary>
    public static class SeonaHouseFurnisher
    {
        const string UH = "Assets/UnhyeongungCollect/Prefabs/";
        const string KT = "Assets/KTinteractiveProp/Volum 02/Prefabs/";
        const string JL = "Assets/Joseon Library/";
        /// <summary>낙안읍성에서 뽑아 둔 민가 소품 — 운현궁(왕실 거처)보다 이 집의 격에 맞는다.</summary>
        const string NG = "Assets/_Project/Gyeonu/Prefabs/Naganeupseong/Parts/";
        const string UnDemo = "Assets/UnhyeongungCollect/Scenes/Demo.unity";
        const string ArchProps = "Assets/_Project/Gyeonu/Art/Materials/Archive/Props/";

        static Transform _root, _items, _colRoot;
        static readonly List<string> _missing = new List<string>();
        static readonly Dictionary<string, GameObject> _composites = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Pose> _keep = new Dictionary<string, Pose>();
        static readonly List<KeyValuePair<GameObject, float>> _blocks =
            new List<KeyValuePair<GameObject, float>>();
        static MeshRenderer[] _floors;

        /// <summary>소품 배치. **현재 씬의 위치·회전이 설계값보다 우선한다** (사용자 지시 2026-08-16) —
        /// 손으로 맞춰 둔 가구가 재생성으로 초기화되지 않게 이름별 스냅샷을 떠서 되돌린다.</summary>
        [MenuItem("Tools/이문록/선아 집 ▸ ② 소품 배치")]
        public static void Furnish() => Furnish(true);

        /// <summary>손 조정을 버리고 스크립트의 설계 좌표로 되돌린다.
        /// ⚠️ 확인 대화상자를 두지 않는다 — `EditorUtility.DisplayDialog`는 에디터 메인 스레드를
        ///    막아서 **MCP로 이 메뉴를 실행하면 Unity가 통째로 멈춘다**(실측으로 한 번 멈췄다).
        ///    되돌리려면 Ctrl+Z 또는 「② 소품 배치」를 다시 실행할 것.</summary>
        [MenuItem("Tools/이문록/선아 집 ▸ ② 소품 배치 (설계 위치로 초기화)")]
        public static void FurnishReset()
        {
            Debug.LogWarning("[선아집] 설계 위치로 초기화 — 손으로 조정한 가구 위치·회전이 버려집니다");
            Furnish(false);
        }

        public static void Furnish(bool preserveHandPlacement)
        {
            if (SceneManager.GetActiveScene().name != SceneName)
            { Debug.LogError("[선아집] " + SceneName + " 씬에서 실행하세요"); return; }

            var house = GameObject.Find(HouseRoot);
            if (house == null) { Debug.LogError("[선아집] 본채가 없습니다 — 「① 씬 조립」을 먼저"); return; }
            _floors = house.GetComponentsInChildren<MeshRenderer>(true)
                           .Where(r => r.name.StartsWith("SM_Floor")).ToArray();

            _missing.Clear(); _keep.Clear(); _blocks.Clear();

            var old = GameObject.Find(PropRoot);
            if (old != null)
            {
                if (preserveHandPlacement) Snapshot(old.transform);
                Object.DestroyImmediate(old);
            }
            _root = new GameObject(PropRoot).transform;
            _items = new GameObject("기물").transform; _items.SetParent(_root, false);
            _colRoot = new GameObject("가구_차단").transform; _colRoot.SetParent(_root, false);

            LoadComposites();
            BuildSeonaRoom();
            BuildDaecheong();
            BuildFatherRoom();
            BuildEastAnnex();
            BuildOnTop();      // ⚠️ 가구가 다 놓인 뒤에 — 가구 상면을 실측해서 얹는다
            foreach (var kv in _composites)
                if (kv.Value != null && !kv.Value.activeSelf) Object.DestroyImmediate(kv.Value);
            _composites.Clear();

            int restored = RestoreSnapshot();
            BuildBlockers();   // ⚠️ 반드시 복원 뒤에 — 콜라이더가 최종 위치를 따라가야 한다

            if (_missing.Count > 0)
                Debug.LogWarning("[선아집] 없는 에셋 " + _missing.Count + "건:\n  " + string.Join("\n  ", _missing));

            int tri = _items.GetComponentsInChildren<MeshFilter>()
                            .Where(m => m.sharedMesh != null).Sum(m => m.sharedMesh.triangles.Length / 3);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            // 차단 박스는 가구의 **자식**으로 들어간다(BuildBlockers 주석) — _colRoot가 아니라
            // 실제로 붙은 개수를 센다.
            int blockers = _items.GetComponentsInChildren<BoxCollider>(true)
                                 .Count(c => c.name.StartsWith("차단_"));
            Debug.Log("[선아집] ② 소품 배치 — 기물 " + _items.childCount + " / 가구 차단 " + blockers
                      + " / " + tri.ToString("N0") + " tri" + (preserveHandPlacement && restored > 0
                        ? " (손 조정 " + restored + "개 보존)" : ""));
        }

        // ══════════════════════════════════════════════════════
        // 선아 방 (서) — 살림 흔적. 그러나 급히 나간 티
        // ══════════════════════════════════════════════════════
        static void BuildSeonaRoom()
        {
            const float W = SeonaX0, N = SeonaZ1;

            // 북벽에 등을 붙인 큰 가구 셋 — 방의 뼈대.
            // ⚠️ **동쪽 끝(x > −2.9)은 비워 둔다.** 대청 뒤에서 들어오는 원본 문(x −1.40, z 1.09~1.81)
            //    앞이라 가구를 두면 그 길이 막힌다 — 1차에 찬장을 x −2.25에 세웠다가 자동 보행이
            //    문간에서 STUCK으로 멎었다(실측). 가구 폭 합이 3.6m라 4.6m 벽에서 서쪽으로 몰면 된다.
            // ⚠️ **팩마다 정면 방향이 다르다.**
            //    · kcisa(KT) 프리팹 — yaw 0에서 앞면이 **−Z**. 북벽에 붙이려면 yaw **0**,
            //      서벽에 붙이려면 yaw **270**. 1차에 북벽 가구를 180으로 돌려 세웠다가
            //      **뒷판(민무늬 널)만 방을 향해** 큰 나무판때기가 서 있는 꼴이 됐다(실측).
            //    · 운현궁 조립본 — 앞면이 **+Z**. 북벽은 yaw 180, 동벽은 −90.
            Block(Composite("SM_Wardrobe223", "의걸이장", new Vector2(-5.45f, 0f), 180f, 'N', N), 0.04f);
            Block(Place(KT + "HalfChest 02.prefab", "옷궤", new Vector2(-4.35f, 0f), 0f,
                        float.NaN, 'N', N), 0.04f);
            Block(Place(KT + "Closet 01.prefab", "찬장", new Vector2(-3.25f, 0f), 0f,
                        float.NaN, 'N', N), 0.04f);

            // 경대 — 서벽. 선아가 쓰던 자리
            Block(Place(KT + "Mirror 01.prefab", "경대", new Vector2(0f, 1.15f), 270f,
                        float.NaN, 'W', W), 0.04f);
            Block(Place(KT + "Box 01.prefab", "반짇고리함", new Vector2(0f, -0.75f), 258f,
                        float.NaN, 'W', W), 0.03f);

            // 요 — **개지 않고 깔린 채**. 방 한가운데를 비스듬히 가로지른다 (급히 나간 티의 핵심)
            // ⚠️ 원본 M_QuiltedMattress는 **왕실용 다홍·청색 비단**이라 몰락한 검수관 집에 안 맞는다.
            //    원본 폴더 수정 금지 규칙대로 재질을 복제해 우리 폴더에서 어둡게 눌러 쓴다.
            var quilt = Place(UH + "SM_QuiltedMattress.prefab", "요", new Vector2(-4.15f, 0.05f), 14f);
            // ⚠️ `_BaseColor` 곱셈만으론 부족했다 — 원본 텍스처가 워낙 진한 다홍이라 0.42를 곱해도
            //    방 안에서 혼자 새빨갛게 남았다(실측). **베이스맵을 아예 빼고** 무명빛 단색으로
            //    간다. 누비 무늬는 메시 형상에 있어서 텍스처가 없어도 요로 읽힌다.
            Retint(quilt, "선아집_요", new Color(0.60f, 0.56f, 0.49f), dropBaseMap: true);

            // 먹다 만 밥상 — 소반 위에 그릇이 그대로다
            var tray = Place(UH + "SM_WoodenTray322.prefab", "소반", new Vector2(-2.75f, 0.62f), -22f);
            if (tray != null)
            {
                float top = WorldBounds(tray).max.y;
                Place(UH + "SM_RiceBowls104.prefab", "밥그릇", new Vector2(-2.90f, 0.70f), 0f, top);
                Place(UH + "SM_SoupBowl_B.prefab", "국그릇", new Vector2(-2.62f, 0.74f), 0f, top);
                Place(UH + "SM_WhitePorcelainDish195.prefab", "찬접시", new Vector2(-2.74f, 0.46f), 0f, top);
            }
            // 방석이 밥상에서 밀려나 비뚤다
            Place(UH + "SM_Mat370.prefab", "방석", new Vector2(-2.95f, -0.18f), 27f);

            Place(UH + "SM_Candlestick128.prefab", "촛대", new Vector2(-2.30f, 1.25f), 0f);
            Place(NG + "Jar01c.prefab", "항아리_선아방", new Vector2(-5.90f, -0.15f), 12f, tiltX: -90f);
            Place(UH + "SM_Washbasin.prefab", "세숫대야", new Vector2(-5.75f, -1.05f), 0f);
            Place(UH + "SM_Pot154.prefab", "함지", new Vector2(-2.05f, -1.05f), -20f);

            // 아버지 기록을 여기까지 가져와 본 흔적 — 서책 두 권이 요 옆에 놓여 있다
            BookStack(new Vector2(-3.25f, -0.85f), 2, -12f, "선아방_서책");
            Place(JL + "gyeongbokgung-cheonchujeon-openbook/source/SM_CCJ_OpenBook.fbx",
                  "펼친책_선아방", new Vector2(-3.75f, -1.00f), 32f);

            // ── 2차 보강 (2026-08-17) — "두 방이 허전하다. 살림집이지 창고가 아니다" ──
            // ⚠️ 통행로를 비워 둔다: 대청 문간(x −1.5 부근)과 뒷문 진입선(z 1.1~1.8)은 손대지 않고,
            //    방 한복판 차선(z ≈ 0.7 / −0.9)도 **콜라이더 없는 낮은 물건만** 놓는다.
            var rnd = new System.Random(20260817);
            System.Func<float, float> J = a => a + (float)(rnd.NextDouble() * 16.0 - 8.0);   // 회전 편차

            // 경대 둘레 — 화장 도구 (경대 자체는 사용자가 맞춰 둔 자리라 건드리지 않는다)
            Place(KT + "Box 03.prefab", "빗접", new Vector2(-5.88f, 1.66f), J(250f));
            Place(UH + "SM_WhitePorcelainDish207.prefab", "분접시", new Vector2(-5.92f, 1.44f), J(0f));
            Place(UH + "SM_CeladonglazedSmall.prefab", "청자합", new Vector2(-5.80f, 1.33f), J(0f));
            Place(UH + "SM_WhitePorcelainBottle.prefab", "기름병", new Vector2(-5.97f, 0.86f), J(0f));

            // 개키다 만 이부자리 — 먼저 깔린 요 위로 한 채가 비스듬히 겹쳐 있다
            var quilt2 = Place(UH + "SM_QuiltedMattress.prefab", "이불", new Vector2(-4.32f, 0.74f), J(23f));
            Retint(quilt2, "선아집_요", new Color(0.60f, 0.56f, 0.49f), dropBaseMap: true);

            // 살림 — 낮고 작아 전부 콜라이더 없이 둔다
            // ⚠️ 팩 소품 중 **채도가 튀는 것**은 이 집에서 쓰지 않는다 (운현궁 = 왕실 거처라 흔하다).
            //    `SM_WoodenTray336`(주황 소반)·`SM_ScoopedWoodenVessel`(검붉은 함지)은 방 안에서
            //    혼자 도드라져 기각하고, 각각 이미 검증된 어두운 소반·낙안읍성 민가 대야로 바꿨다.
            Place(UH + "SM_Sieve397.prefab", "체", new Vector2(-2.18f, 1.38f), J(0f));
            Place(NG + "Basin01b.prefab", "대야", new Vector2(-2.42f, 0.52f), J(0f));
            Place(UH + "SM_Ladle.prefab", "국자", new Vector2(-2.60f, 0.24f), J(38f));
            Place(UH + "SM_Bowls100.prefab", "종지", new Vector2(-2.30f, 0.86f), J(0f));
            Place(UH + "SM_Candlestick128.prefab", "등잔_선아방", new Vector2(-3.10f, -0.52f), J(0f));
            Place(UH + "SM_WoodenTray322.prefab", "소반_작은", new Vector2(-3.58f, -1.08f), J(-14f));
        }

        // ══════════════════════════════════════════════════════
        // 대청 (중앙) — 통로 겸 공용. 비워 둔다
        // ══════════════════════════════════════════════════════
        static void BuildDaecheong()
        {
            // ⚠️ 대청은 **비우는 게 맞다** — 통로 겸 공용 공간이고(사용자 지시), 한옥 대청 자체가
            //    원래 세간을 두지 않는 마루다. 1차에는 운현궁 뒤주(SM_GrainChest)를 북벽에 세웠는데
            //    **다홍 옻칠에 놋장식**이라 살림집 대청 한가운데서 혼자 궁궐 물건으로 튀었다(실측).
            //    바닥 물건만 남기고 북벽 창은 비워 둔다 — 들어서면 그 창이 보이는 게 이 방의 인상이다.
            // ⚠️ 스폰 자리(−0.06, z −1.02)와 그 앞은 비운다 — 들어서자마자 가구에 코를 박는다.
            // ⚠️ **대청에는 벽붙임 가구를 둘 수 없다.** 동·서 벽이 곧 두 방으로 통하는 창호문이고
            //    남면은 마당 출입문이라, 벽에 등을 붙이면 반드시 어느 문간을 막는다.
            //    1차에 소반을 서벽(= 선아 방 칸막이)에 붙였다가 자동 보행이 그 문을 못 지났다(실측).
            //    바닥 물건만, 그것도 네 구석에 둔다.
            Place(UH + "SM_StrawMat372.prefab", "돗자리", new Vector2(-0.10f, 0.55f), 4f);
            Place(NG + "Jar01c.prefab", "항아리_대청", new Vector2(-0.98f, 1.98f), 0f, tiltX: -90f);
            Place(NG + "Jar01c.prefab", "항아리_대청2", new Vector2(-1.00f, 1.32f), 34f, tiltX: -90f);
            Place(NG + "Large_Scooped_Wooden_Bowl.prefab", "함지_대청", new Vector2(0.48f, 2.02f), 18f);
            Block(Place(UH + "SM_Table252.prefab", "소반_대청", new Vector2(1.00f, 1.72f), 24f), 0.03f);
            Place(UH + "SM_BlackGlazedJar.prefab", "물독", new Vector2(0.98f, 1.12f), 0f);
            Place(UH + "SM_WoodenStep.prefab", "디딤대", new Vector2(0.78f, -1.22f), 0f);
        }

        // ══════════════════════════════════════════════════════
        // 아버지 방 (동) — 기록과 물건. 선아가 뒤진 흔적
        // ══════════════════════════════════════════════════════
        static void BuildFatherRoom()
        {
            // 서안 — 밤 등잔 광원(LampSpot)이 이 상 위를 겨눈다. 자리를 옮기면 조명도 같이 옮길 것.
            var desk = Place(JL + "gyeongbokgung-sajeongjeon-tableb/source/SM_SJJ_TableB.fbx",
                             "서안", new Vector2(LampSpot.x, LampSpot.z + 0.10f), 0f);
            Block(desk, 0.06f);
            float top = desk != null ? WorldBounds(desk).max.y : 0.96f;

            Place(JL + "gyeongbokgung-cheonchujeon-openbook/source/SM_CCJ_OpenBook.fbx",
                  "펼친책", new Vector2(LampSpot.x - 0.12f, LampSpot.z + 0.08f), 6f, top);
            Place(UH + "SM_WhitePorcelainDish203.prefab", "연적접시",
                  new Vector2(LampSpot.x - 0.40f, LampSpot.z - 0.10f), 0f, top);
            // 등잔 — 낮에도 놓여 있다(불만 꺼져 있을 뿐). 밤 광원이 바로 위에 얹힌다.
            // ⚠️ kcisa `Lantern`은 등잔이 아니라 **바닥에 두는 등롱(0.5m)** 이다. 서안에 올리니
            //    상보다 커서 방 한가운데를 가렸다(실측) — 서안 위에는 촛대를 쓴다.
            Place(UH + "SM_Candlestick128.prefab", "등잔", new Vector2(LampSpot.x + 0.28f, LampSpot.z), 0f, top);
            BookStack(new Vector2(LampSpot.x + 0.02f, LampSpot.z + 0.42f), 3, 10f, "서안_서책", top);

            // ⚠️ 운현궁 방석도 요와 같은 **다홍·청색 비단**이다. 좁은 방 한복판이라 그대로 두면
            //    이 방에서 제일 먼저 눈에 들어온다 — 무명 남색으로 눌러 앉힌다.
            var cushion = Place(UH + "SM_FloorCusion.prefab", "방석_아버지",
                                new Vector2(LampSpot.x, LampSpot.z + 0.72f), 4f);
            Retint(cushion, "선아집_방석", new Color(0.34f, 0.36f, 0.43f), dropBaseMap: true);

            // 문갑 — 북벽. 여닫이 리거가 문 2짝 + 서랍을 움직이게 한다.
            // ⚠️ Joseon Library 문갑(Table04_Key)은 **앞면이 −Z** 다 (집무실에서 동벽에 −90으로 세운
            //    것과 다른 값인데, 그쪽은 사용자가 X 270°로 돌려 둔 상태라 축이 다르다).
            //    북벽에 등을 붙이려면 yaw **0**. 180으로 놨다가 민무늬 뒤판만 방을 향했다(실측).
            Block(Place(JL + "kcdf-mungap-04/source/Table04_Key.fbx", "문갑",
                        new Vector2(3.20f, 0f), 0f, float.NaN, 'N', FatherZ1), 0.04f);
            Block(Place(UH + "SM_BookCase305.prefab", "책궤", new Vector2(3.55f, -1.25f), -14f), 0.03f);

            // 뒤진 흔적 — 바닥에 흩어진 서책. 쌓다 만 것처럼 각도를 제각각으로.
            // (서책 더미는 차단 박스를 달지 않는다 — 낮아서 밟고 지나가도 어색하지 않고,
            //  좁은 방에서 동선을 잘게 끊지 않는다)
            BookStack(new Vector2(2.55f, 0.30f), 4, -18f, "뒤진서책_1");
            BookStack(new Vector2(1.85f, 0.15f), 2, 26f, "뒤진서책_2");
            BookStack(new Vector2(3.70f, -0.32f), 3, 8f, "뒤진서책_3");
            Place(JL + "gyeongbokgung-cheonchujeon-openbook/source/SM_CCJ_OpenBook.fbx",
                  "펼친책_바닥", new Vector2(2.10f, 0.52f), -42f);

            // ── 2차 보강 (2026-08-17) — 서책·문서·문방을 더 채운다 ──
            // ⚠️ 문간(x 1.32, z −0.98~0.15)과 골방으로 가는 차선(z ≈ −0.4)은 **낮은 것만** 놓는다.
            var rnd = new System.Random(20260818);
            System.Func<float, float> J = a => a + (float)(rnd.NextDouble() * 18.0 - 9.0);

            // 연상(硯箱) — 서안 옆 문방 보관함. 이 방에서 유일하게 콜라이더를 붙이는 새 소품이다.
            Block(Place(KT + "BesideTable 01.prefab", "연상", new Vector2(3.74f, -1.02f), J(268f)), 0.03f);

            // 문방 — 서안 위
            float deskTop = 1.685f;
            var d2 = GameObject.Find(PropRoot + "/기물/서안");
            if (d2 != null) deskTop = WorldBounds(d2).max.y;
            BrushPot(new Vector3(2.28f, deskTop, -0.86f));
            Place(UH + "SM_Cup28.prefab", "붓씻이", new Vector2(2.46f, -0.72f), J(0f), deskTop);
            Place(UH + "SM_CeladonglazedSmall.prefab", "연적", new Vector2(2.92f, -0.80f), J(0f), deskTop);

            // 두루마리 — 팩에 없어 직접 만든다. 바닥과 문갑 위에 굴러다닌다.
            Scroll(new Vector2(1.72f, -0.62f), J(74f), 0.46f, "두루마리_1");
            Scroll(new Vector2(1.88f, -0.72f), J(58f), 0.38f, "두루마리_2");
            Scroll(new Vector2(3.34f, 0.34f), J(15f), 0.42f, "두루마리_3");

            // 흐트러진 문서 — 선아가 뒤진 흔적을 더 넓게
            BookStack(new Vector2(1.68f, -0.90f), 4, J(-30f), "뒤진서책_4");
            BookStack(new Vector2(2.22f, -0.10f), 2, J(48f), "뒤진서책_5");
            BookStack(new Vector2(3.46f, 0.14f), 3, J(-6f), "뒤진서책_6");
            Place(JL + "gyeongbokgung-cheonchujeon-openbook/source/SM_CCJ_OpenBook.fbx",
                  "펼친책_바닥2", new Vector2(3.26f, -0.58f), J(-25f));
            Place(JL + "gyeongbokgung-cheonchujeon-openbook/source/SM_CCJ_OpenBook.fbx",
                  "펼친책_바닥3", new Vector2(1.80f, 0.34f), J(58f));
            Place(KT + "Box 01.prefab", "궤_아버지방", new Vector2(1.62f, 0.46f), J(196f));
        }

        // ══════════════════════════════════════════════════════
        // 낮은 가구 윗면에 얹는 소품
        // ══════════════════════════════════════════════════════
        /// <summary>
        /// 가구 윗면에 소품을 한두 개씩 올린다 (사용자 지시 2026-08-17: "윗면이 비어 있다").
        ///
        /// ⚠️ 지지면 높이를 **상수로 쓰지 않고 그 가구의 현재 바운즈에서 실측**한다 — 사용자가
        ///    손으로 옮긴 가구도 있어서 설계 좌표를 믿으면 소품이 뜨거나 파묻힌다.
        ///    `Snap`이 손 조정 포즈를 **놓는 즉시** 적용하므로, 이 시점의 상면이 곧 최종 상면이다.
        /// ⚠️ 여기 놓는 것은 전부 작고 낮다 — **콜라이더를 붙이지 않는다**(`Block` 호출 없음).
        ///    캡슐이 가구 위 물건에 걸려 끼는 걸 막기 위해서다.
        /// ⚠️ 차단 박스는 바운즈에서 제외한다 — 포함하면 가구보다 큰 상자 윗면을 지지면으로 잡는다.
        /// </summary>
        static void BuildOnTop()
        {
            var rnd = new System.Random(20260820);
            System.Func<float, float> J = a => a + (float)(rnd.NextDouble() * 22.0 - 11.0);

            // ── 선아 방: 옷궤 위 — 반짇고리와 등잔 ──
            float t1 = TopOf("옷궤");
            if (!float.IsNaN(t1))
            {
                Place(KT + "Box 01.prefab", "반짇고리_옷궤위", new Vector2(-4.63f, 2.01f), J(214f), t1);
                Place(UH + "SM_Candlestick128.prefab", "등잔_옷궤위", new Vector2(-4.06f, 2.11f), J(0f), t1);
            }

            // ── 대청: 소반 위 — 백자 화병 ──
            float t2 = TopOf("소반_대청");
            if (!float.IsNaN(t2))
                Place(UH + "SM_WhitePorcelainBottle.prefab", "화병_대청", new Vector2(1.00f, 1.73f), J(0f), t2);

            // ── 아버지 방: 문갑 위 — 서책 두 권과 문진 ──
            float t3 = TopOf("문갑");
            if (!float.IsNaN(t3))
            {
                BookStack(new Vector2(2.83f, 0.47f), 2, J(-16f), "문갑위_서책", t3);
                Place(UH + "SM_CeladonglazedSmall.prefab", "문진", new Vector2(3.54f, 0.51f), J(0f), t3);
            }

            // ── 아버지 방: 연상 위 — 두루마리와 백자 접시 ──
            float t4 = TopOf("연상");
            if (!float.IsNaN(t4))
            {
                Scroll(new Vector2(3.60f, -0.76f), J(96f), 0.30f, "두루마리_연상위", t4);
                Place(UH + "SM_WhitePorcelainDish207.prefab", "접시_연상위", new Vector2(3.63f, -1.01f), J(0f), t4);
            }

            // ── 골방: 반닫이 위 — 서책 (문서를 쌓아 둔 광) ──
            float t5 = TopOf("반닫이");
            if (!float.IsNaN(t5))
                BookStack(new Vector2(4.44f, -2.28f), 2, J(24f), "반닫이위_서책", t5);
        }

        /// <summary>그 가구의 **현재** 윗면 Y. 차단 박스는 빼고 실제 메시만 본다.</summary>
        static float TopOf(string furnitureName)
        {
            var t = _items.Find(furnitureName);
            if (t == null) { _missing.Add("가구 " + furnitureName + " (상면 계산 대상)"); return float.NaN; }
            Bounds b = new Bounds(); bool first = true;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<BoxCollider>() != null) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return first ? float.NaN : b.max.y;
        }

        // ══════════════════════════════════════════════════════
        // 절차 소품 — 문방사우가 어느 팩에도 없어 직접 만든다
        // (집무실이 같은 이유로 붓통을 만들었다. 여기선 프리미티브로 간단히)
        // ══════════════════════════════════════════════════════
        static Material _wood, _paper;

        static Material PackMat(ref Material cache, string name)
        {
            if (cache != null) return cache;
            cache = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Gyeonu/Art/Materials/SeonaHouse/선아집_" + name + ".mat");
            if (cache == null) _missing.Add("선아집_" + name + ".mat (①을 먼저 실행)");
            return cache;
        }

        /// <summary>프리미티브 조각 하나. **콜라이더는 반드시 지운다** — 작은 소품이 캡슐을 문다.</summary>
        static GameObject Piece(Transform parent, string name, PrimitiveType type,
                                Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>붓통 + 꽂힌 붓 몇 자루.</summary>
        static void BrushPot(Vector3 baseCenter)
        {
            var root = new GameObject("붓통"); root.transform.SetParent(_items, false);
            var wood = PackMat(ref _wood, "MI_Wood02A");
            var paper = PackMat(ref _paper, "MI_Door01A");
            Piece(root.transform, "통", PrimitiveType.Cylinder,
                  baseCenter + Vector3.up * 0.078f, new Vector3(0.11f, 0.078f, 0.11f), Quaternion.identity, wood);
            var rnd = new System.Random(20260819);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + 0.5f;
                float lean = 7f + (float)rnd.NextDouble() * 10f;
                float len = 0.26f + (float)rnd.NextDouble() * 0.06f;
                var rot = Quaternion.Euler(lean * Mathf.Cos(a), 0f, -lean * Mathf.Sin(a));
                Vector3 root0 = baseCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.026f + Vector3.up * 0.10f;
                Piece(root.transform, "붓_" + i, PrimitiveType.Cylinder,
                      root0 + rot * (Vector3.up * len * 0.5f), new Vector3(0.011f, len * 0.5f, 0.011f), rot, wood);
                Piece(root.transform, "붓끝_" + i, PrimitiveType.Cylinder,
                      root0 + rot * (Vector3.up * (len + 0.028f)), new Vector3(0.016f, 0.028f, 0.016f), rot, paper);
            }
        }

        /// <summary>말아 둔 두루마리 하나 — 바닥에 눕혀 놓는다.</summary>
        static void Scroll(Vector2 xz, float yaw, float len, string name, float supportY = float.NaN)
        {
            var paper = PackMat(ref _paper, "MI_Door01A");
            var wood = PackMat(ref _wood, "MI_Wood02A");
            // ⚠️ 스케일을 준 조각에 자식을 달면 그 스케일이 상속돼 축이 찌그러진다 — 무스케일 루트를 둔다.
            var root = new GameObject(name); root.transform.SetParent(_items, false);
            float y = (float.IsNaN(supportY) ? FloorAt(xz) : supportY) + 0.026f;
            var pos = new Vector3(xz.x, y, xz.y);
            var rot = Quaternion.Euler(0f, yaw, 90f);      // 눕힌다
            root.transform.SetPositionAndRotation(pos, Quaternion.identity);
            Piece(root.transform, "종이", PrimitiveType.Cylinder,
                  pos, new Vector3(0.052f, len * 0.5f, 0.052f), rot, paper);
            for (int s = -1; s <= 1; s += 2)   // 축(軸) — 양 끝에 삐져나온 나무 심
                Piece(root.transform, "축" + (s > 0 ? "A" : "B"), PrimitiveType.Cylinder,
                      pos + rot * (Vector3.up * (len * 0.5f + 0.014f) * s),
                      new Vector3(0.015f, 0.016f, 0.015f), rot, wood);
        }

        // ══════════════════════════════════════════════════════
        // 동쪽 골방 — 아버지 방에서 이어지는 광. 문서·궤가 쌓여 있다
        // ══════════════════════════════════════════════════════
        static void BuildEastAnnex()
        {
            Block(Composite("SM_ThreetieredCupboard", "삼층장", new Vector2(0f, -0.35f), -90f, 'E', WingX1), 0.04f);
            Block(Composite("SM_Cupboard226", "문서장", new Vector2(0f, -1.85f), -90f, 'E', WingX1), 0.04f);
            Block(Place(KT + "HalfChest 01.prefab", "반닫이", new Vector2(0f, -2.35f), 270f,
                        float.NaN, 'W', WingX0), 0.04f);   // kcisa 앞면 −Z → 서벽은 270
            Block(Place(KT + "Box 01.prefab", "궤", new Vector2(5.05f, -2.95f), 204f), 0.03f);
            Place(UH + "SM_BlackGlazedJar.prefab", "항아리_골방", new Vector2(4.55f, 0.28f), 0f);
            BookStack(new Vector2(5.15f, -1.05f), 5, -8f, "골방_서책");
        }

        // ══════════════════════════════════════════════════════
        // 배치 헬퍼
        // ══════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════
        // 바로 세우기 — 항아리·그릇류 (씬에서 직접 고친다)
        // ══════════════════════════════════════════════════════
        /// <summary>이름에 이 조각이 들어가면 "세워 놓는 물건"으로 본다.</summary>
        static readonly string[] UprightNames = { "항아리", "물독", "함지", "세숫대야", "촛대", "등잔" };

        /// <summary>바닥에 서는 가구 — 회전은 그대로 두고 **묻힌 것만** 마루 위로 올린다.
        /// ⚠️ **경대는 넣지 않는다.** 사용자가 직접 방향·자리를 잡아 둔 소품이라 어떤 자동 보정도
        ///    적용하지 않는다 (지시 2026-08-17).</summary>
        static readonly string[] GroundNames =
            { "옷궤", "찬장", "의걸이장", "반닫이", "문서장", "삼층장", "반짇고리함", "궤", "문갑", "책궤", "서안" };

        /// <summary>
        /// 항아리류를 **똑바로 세우고 밑면을 마루에 정확히 앉힌다.**
        /// 위치(XZ)와 방위(yaw)는 손대지 않는다 — 사용자가 손으로 맞춰 둔 배치를 지키기 위해서다.
        /// 그래서 「② 소품 배치」를 다시 돌리지 않고 **씬의 현재 오브젝트를 직접 고친다.**
        ///
        /// ⚠️ 왜 루트 회전 X·Z를 0으로 두는 것만으론 안 되나:
        ///    낙안읍성 `Jar01c`는 **자식 트랜스폼에 (90, 19, 0)이 구워져 있다.** 루트를 X −90으로
        ///    돌려 눕힘을 상쇄했더니 이번엔 그 19°가 **Z축 기울기로 남아** 항아리가 옆으로 기울었다.
        ///    (합성해 보면 world = Ry(yaw)·Rz(−19)가 된다. 1차 실측에서 정확히 그 값이 나왔다.)
        ///    그래서 루트 오일러를 만지는 대신 **메시의 월드 up이 진짜 up을 향하도록** 보정한다.
        ///    팩마다 구워진 회전이 달라도 이 방식은 통한다.
        /// </summary>
        [MenuItem("Tools/이문록/선아 집 ▸ 소품 바로 세우기 (항아리·그릇)")]
        public static void StandUprightProps()
        {
            if (SceneManager.GetActiveScene().name != SceneName)
            { Debug.LogError("[선아집] " + SceneName + " 씬에서 실행하세요"); return; }
            var items = GameObject.Find(PropRoot)?.transform.Find("기물");
            var house = GameObject.Find(HouseRoot);
            if (items == null || house == null) { Debug.LogError("[선아집] 소품/본채가 없습니다"); return; }
            _floors = house.GetComponentsInChildren<MeshRenderer>(true)
                           .Where(r => r.name.StartsWith("SM_Floor")).ToArray();

            var log = new List<string>();

            // ── 바닥에 놓인 가구가 마루를 뚫고 내려앉았으면 되올린다 (회전은 손대지 않는다) ──
            // 스킨드 가구는 본을 고치면 메시가 통째로 내려가서 루트를 그대로 두면 바닥에 묻힌다.
            // 5mm 넘게 **묻힌 것만** 건드린다 — 손으로 올려 둔 물건을 끌어내리지 않기 위해서다.
            foreach (Transform t in items)
            {
                if (!GroundNames.Any(k => t.name.Contains(k))) continue;
                var gb = WorldBounds(t.gameObject);
                float gf = FloorAt(new Vector2(gb.center.x, gb.center.z));
                float sunk = gf - gb.min.y;
                if (sunk <= 0.005f) continue;
                t.position += new Vector3(0f, sunk, 0f);
                log.Add(t.name.PadRight(12) + " 마루 아래 " + sunk.ToString("F3") + "m 묻힘 → 올림");
            }

            foreach (Transform t in items)
            {
                if (!UprightNames.Any(k => t.name.Contains(k))) continue;
                var mf = t.GetComponentInChildren<MeshFilter>();
                if (mf == null) continue;

                var b0 = WorldBounds(t.gameObject);
                var r0 = mf.transform.rotation.eulerAngles;
                float lift0 = b0.min.y - FloorAt(new Vector2(b0.center.x, b0.center.z));

                // ⚠️ **가구 위에 놓인 물건은 지지면이 마루가 아니다.** 서안 위 등잔을 마루로
                //    끌어내렸다가 밤 등잔 광원만 공중에 남았다(실측). 이미 마루에서 10cm 넘게
                //    떠 있으면 "무언가 위에 얹힌 것"으로 보고 **높이를 그대로 둔다.**
                bool onFurniture = lift0 > 0.10f;

                // ① 메시의 up을 월드 up으로 — 방위(yaw)는 그대로 남는다
                Vector3 meshUp = mf.transform.rotation * Vector3.up;
                t.rotation = Quaternion.FromToRotation(meshUp, Vector3.up) * t.rotation;
                // ② 밑면을 지지면에 정확히 앉힌다 (기울기를 편 뒤라야 바운즈 밑면 = 진짜 밑면)
                var b1 = WorldBounds(t.gameObject);
                float support = onFurniture ? b0.min.y : FloorAt(new Vector2(b1.center.x, b1.center.z));
                t.position += new Vector3(0f, support - b1.min.y, 0f);

                var b2 = WorldBounds(t.gameObject);
                var r2 = mf.transform.rotation.eulerAngles;
                log.Add(t.name.PadRight(12)
                    + " 메시회전 " + Fmt(r0) + " → " + Fmt(r2)
                    + " / 밑면 " + b0.min.y.ToString("F3") + " → " + b2.min.y.ToString("F3")
                    + (onFurniture ? "  (가구 위 — 높이 유지)" : "  (마루 접지)"));
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[선아집] 바로 세우기 " + log.Count + "개 (위치·방위는 손댄 그대로)\n  " + string.Join("\n  ", log));
        }

        /// <summary>기울기만 보이게 — 360° 근처를 음수로 접어 읽는다.</summary>
        static string Fmt(Vector3 e)
        {
            System.Func<float, float> w = a => a > 180f ? a - 360f : a;
            return "(X" + w(e.x).ToString("F1") + " Z" + w(e.z).ToString("F1") + ")";
        }

        /// <summary>그 자리의 **마루널 윗면**. 이 집 마루는 15cm 기울어 있어 상수를 쓰면 안 된다.</summary>
        static float FloorAt(Vector2 xz)
        {
            float best = float.NaN, area = float.MaxValue;
            foreach (var r in _floors)
            {
                var b = r.bounds;
                if (xz.x < b.min.x - 0.05f || xz.x > b.max.x + 0.05f) continue;
                if (xz.y < b.min.z - 0.05f || xz.y > b.max.z + 0.05f) continue;
                float a = b.size.x * b.size.z;          // 여러 판이 겹치면 작은 쪽이 그 자리의 판이다
                if (a < area) { area = a; best = b.max.y; }
            }
            return float.IsNaN(best) ? 1.00f : best;
        }

        /// <summary>
        /// 회전 → 바운즈 실측 → 지지면·벽에 스냅. wall이 'W'/'E'/'N'/'S'이면 그 벽면(wallPlane)에
        /// 등을 붙이고 나머지 축만 xz 값을 쓴다. supportY가 NaN이면 그 자리 마루 높이에 앉힌다.
        /// </summary>
        /// <param name="tiltX">세우기 보정. 낙안읍성 `Jar01c`는 **자식 트랜스폼이 X+90으로 구워져
        /// 있어 항아리가 옆으로 누워 있다**(실측 — 대청에 통을 굴려 놓은 꼴이 됐다).
        /// 루트에 −90을 주면 상쇄돼 바로 선다.</param>
        static GameObject Place(string path, string name, Vector2 xz, float yaw,
                                float supportY = float.NaN, char wall = '.', float wallPlane = 0f,
                                float gap = 0.05f, float tiltX = 0f)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { _missing.Add(path); return null; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.name = name;
            go.transform.SetParent(_items, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(tiltX, yaw, 0f));
            Strip(go);
            Snap(go, xz, yaw, supportY, wall, wallPlane, gap, tiltX);
            // 항아리·그릇류는 놓은 뒤 항상 바로 세운다 (손 조정 포즈에도 적용 — 사용자 지시
            // "전부 똑바로 세워라". 자세한 이유는 StandUpright 주석 참조)
            if (UprightNames.Any(k => name.Contains(k))) StandUpright(go, supportY);
            return go;
        }

        /// <summary>메시의 up을 월드 up으로 돌리고 밑면을 지지면에 앉힌다. 방위(yaw)는 보존된다.</summary>
        static void StandUpright(GameObject go, float supportY)
        {
            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf == null) return;
            Vector3 meshUp = mf.transform.rotation * Vector3.up;
            go.transform.rotation = Quaternion.FromToRotation(meshUp, Vector3.up) * go.transform.rotation;
            var b = WorldBounds(go);
            float sy = float.IsNaN(supportY) ? FloorAt(new Vector2(b.center.x, b.center.z)) : supportY;
            go.transform.position += new Vector3(0f, sy - b.min.y, 0f);
        }

        static void Snap(GameObject go, Vector2 xz, float yaw, float supportY,
                         char wall, float wallPlane, float gap, float tiltX = 0f)
        {
            // ⚠️ 스냅샷이 있으면 계산하지 않고 그 포즈를 그대로 쓴다 (사용자 지시: 재계산·정규화 금지).
            //    설계 배치는 **처음 놓을 때만** 쓰인다.
            Pose kept;
            if (_keep.TryGetValue(go.name, out kept))
            { go.transform.SetPositionAndRotation(kept.position, kept.rotation); return; }

            go.transform.rotation = Quaternion.Euler(tiltX, yaw, 0f);
            var b = WorldBounds(go);
            Vector2 foot = xz;
            Vector3 d = Vector3.zero;
            d.x = wall == 'W' ? (wallPlane + gap) - b.min.x
                : wall == 'E' ? (wallPlane - gap) - b.max.x
                : xz.x - b.center.x;
            d.z = wall == 'N' ? (wallPlane - gap) - b.max.z
                : wall == 'S' ? (wallPlane + gap) - b.min.z
                : xz.y - b.center.z;
            // 벽 붙임이면 그 축 좌표를 스냅 뒤에 다시 읽어 바닥 높이를 정확히 고른다
            foot = new Vector2(b.center.x + d.x, b.center.z + d.z);
            float sy = float.IsNaN(supportY) ? FloorAt(foot) : supportY;
            d.y = sy - b.min.y;
            go.transform.position += d;
        }

        /// <summary>서책은 조선식으로 **눕혀 쌓는다** (세워 꽂지 않는다).</summary>
        static void BookStack(Vector2 xz, int count, float yaw, string name, float supportY = float.NaN)
        {
            string[] books = {
                JL + "gyeongbokgung-cheonchujeon-book/source/SM_CCJ_BookA.fbx",
                JL + "gyeongbokgung-cheonchujeon-book/source/SM_CCJ_BookB.fbx",
                JL + "gyeongbokgung-cheonchujeon-book/source/SM_CCJ_BookC.fbx",
            };
            var rnd = new System.Random(name.GetHashCode());
            float y = float.IsNaN(supportY) ? FloorAt(xz) : supportY;
            for (int i = 0; i < count; i++)
            {
                float jy = yaw + (float)(rnd.NextDouble() * 14.0 - 7.0);
                var go = Place(books[i % books.Length], name + "_" + (i + 1),
                               xz + new Vector2((float)(rnd.NextDouble() * 0.05 - 0.025),
                                                (float)(rnd.NextDouble() * 0.05 - 0.025)),
                               jy, y);
                if (go == null) return;
                y = WorldBounds(go).max.y;
            }
        }

        // ══════════════════════════════════════════════════════
        // 운현궁 조립 가구 — 데모 씬의 조립본을 복제해 온다
        // ⚠️ 몸체·문짝·서랍이 별 프리팹이고 피벗이 제각각이라 직접 조립하면 문짝이 어긋난다.
        //    데모 씬은 **저장하지 않고** 닫는다 — 프로젝트 절대 규칙.
        // ══════════════════════════════════════════════════════
        static void LoadComposites()
        {
            _composites.Clear();
            var target = SceneManager.GetActiveScene();
            var demo = EditorSceneManager.OpenScene(UnDemo, OpenSceneMode.Additive);
            try
            {
                Transform model = null;
                foreach (var g in demo.GetRootGameObjects()) if (g.name == "Model") model = g.transform;
                // ⚠️ `SM_GrainChest`(뒤주)는 기각했다 — 다홍 옻칠이라 살림집에 안 맞는다(BuildDaecheong 주석).
                foreach (var nm in new[] { "SM_Wardrobe223",
                                           "SM_ThreetieredCupboard", "SM_Cupboard226" })
                {
                    var src = model != null ? model.Find(nm) : null;
                    if (src == null) { _missing.Add("운현궁 데모 조립본 " + nm); continue; }
                    var copy = Object.Instantiate(src.gameObject);
                    SceneManager.MoveGameObjectToScene(copy, target);
                    copy.name = nm;
                    copy.SetActive(false);          // 스테이징 — Composite에서 활성화
                    _composites[nm] = copy;
                }
            }
            finally { EditorSceneManager.CloseScene(demo, true); }   // 저장 없이 닫는다
        }

        static GameObject Composite(string name, string label, Vector2 xz, float yaw, char wall, float wallPlane)
        {
            GameObject go;
            if (!_composites.TryGetValue(name, out go) || go == null) { _missing.Add("조립본 " + name); return null; }
            go.SetActive(true);
            go.name = label;
            go.transform.SetParent(_items, true);
            _composites[name] = null;
            Strip(go);
            Snap(go, xz, yaw, float.NaN, wall, wallPlane, 0.05f);
            return go;
        }

        // ══════════════════════════════════════════════════════
        // 정리 · 차단
        // ══════════════════════════════════════════════════════
        /// <summary>MeshCollider·Rigidbody·Animator·팩 스크립트를 걷어내고 미설정 재질을 메꾼다.
        /// ⚠️ Rigidbody를 남기면 Play 순간 자유낙하해 소품이 사라진다 (서고에서 실측).
        /// ⚠️ kcisa Lantern은 **자체 Point Light를 달고 온다** — URP 추가 광원 한도(4)를 잡아먹으므로
        ///    언팩 후 광원 GO째 지운다.</summary>
        static void Strip(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            if (go.GetComponentInChildren<Light>(true) != null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                                                      InteractionMode.AutomatedAction);
                foreach (var l in go.GetComponentsInChildren<Light>(true))
                    Object.DestroyImmediate(l.gameObject);
            }
            foreach (var m in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (m != null && m.GetType().Namespace != "IMUNROK.Gyeonu") Object.DestroyImmediate(m);
            FixJoseonMaterials(go);
        }

        /// <summary>Joseon Library fbx 일부는 재질이 미설정이라 흰 덩어리로 뜬다 (문갑 `Wood`/`Gold sus_*`).
        /// 서고가 이미 만들어 둔 우리 자산을 재사용한다 (중복 생성 금지).</summary>
        static void FixJoseonMaterials(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = mats[i].name;
                    Material repl = null;
                    if (n == "Wood")
                        repl = AssetDatabase.LoadAssetAtPath<Material>(ArchProps + "M_JL_문갑목.mat");
                    else if (n.StartsWith("Gold sus"))
                        repl = AssetDatabase.LoadAssetAtPath<Material>(ArchProps + "M_JL_문갑쇠.mat");
                    if (repl != null) { mats[i] = repl; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// 팩 재질이 이 집의 격에 안 맞을 때 **복제해서 우리 폴더에 저장한 뒤 눌러 쓴다**
        /// (프로젝트 규칙: 원본 에셋 폴더는 절대 수정하지 않는다 — gitignore라 팀에 전달되지 않는다).
        /// 색은 텍스처에 들어 있어 `_BaseColor` 곱셈으로만 조절된다 — 채도를 없애진 못하고
        /// 어둡게·차갑게 눌러 앉히는 정도다. 그것만으로 비단 다홍이 낡은 무명빛으로 내려온다.
        /// </summary>
        static void Retint(GameObject go, string matName, Color tint, bool dropBaseMap = false)
        {
            if (go == null) return;
            const string dir = "Assets/_Project/Gyeonu/Art/Materials/SeonaHouse/";
            System.IO.Directory.CreateDirectory(dir);
            string path = dir + matName + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial == null) continue;
                if (m == null)
                {
                    m = new Material(r.sharedMaterial);
                    AssetDatabase.CreateAsset(m, path);
                }
                if (dropBaseMap) m.SetTexture("_BaseMap", null);   // 노멀맵은 남는다 — 결은 유지
                m.SetColor("_BaseColor", tint);
                m.SetFloat("_Smoothness", 0.08f);
                EditorUtility.SetDirty(m);
                r.sharedMaterial = m;
            }
        }

        static void Block(GameObject go, float shrink)
        {
            if (go != null) _blocks.Add(new KeyValuePair<GameObject, float>(go, shrink));
        }

        /// <summary>가구 차단 박스 — MeshCollider 금지 규칙대로 박스 하나로. 낮은 소품은 제외.</summary>
        static void BuildBlockers()
        {
            foreach (var kv in _blocks)
            {
                var go = kv.Key; float shrink = kv.Value;
                if (go == null) continue;
                var b = WorldBounds(go);
                if (b.size.y < 0.30f) continue;
                // ⚠️ 차단 박스를 **가구의 자식**으로 둔다. DebugInteractor는 레이가 맞은 콜라이더에서
                //    `GetComponentInParent<Interactable>()`로 대상을 찾으므로, 별도 루트에 있으면
                //    여닫이(FurnitureParts)를 클릭으로 집을 수 없다.
                var col = new GameObject("차단_" + go.name);
                col.transform.SetParent(go.transform, false);
                col.transform.SetPositionAndRotation(b.center, Quaternion.identity);
                col.AddComponent<BoxCollider>().size =
                    new Vector3(Mathf.Max(0.05f, b.size.x - shrink * 2f), b.size.y,
                                Mathf.Max(0.05f, b.size.z - shrink * 2f));
            }
        }

        // ── 손 조정 보존 ─────────────────────────────────────
        static void Snapshot(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root || t.parent == null) continue;
                if (t.parent.name == "기물") _keep[t.name] = new Pose(t.position, t.rotation);
            }
        }

        static int RestoreSnapshot()
        {
            if (_keep.Count == 0) return 0;
            int n = 0;
            foreach (var t in _root.GetComponentsInChildren<Transform>(true))
            {
                if (t == _root || t.parent == null || t.parent.name != "기물") continue;
                Pose p;
                if (!_keep.TryGetValue(t.name, out p)) continue;
                t.SetPositionAndRotation(p.position, p.rotation);
                n++;
            }
            return n;
        }

        static Bounds WorldBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }
}
