using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 서고 가구·소품 배치 (2026-08-15, 멱등 — 루트 `서고_소품` 삭제 후 재생성).
    ///
    /// 분위기: 다리 공사 자재 창고였다가 수령의 은닉처가 된 조선 서고 + 장물 창고.
    /// Room00 원리(가장자리 포화·중앙 절제 / 3단 높이 / 기능 군집 / 반복+랜드마크 /
    /// 군집 정렬 85% + 흐트러짐 15%)를 따르되 구조는 우리 공간에 맞춘다.
    ///
    /// 구역:
    ///   동측(입구) = 수령의 은닉처 — 정돈. 서가(장부)·수령의 서안(사정전 책상)·궤짝·
    ///                빼돌린 물품(놋기물·백자·자재). 각 잡아 배치, 편차 작게
    ///   중간·남벽 = 자재 창고 흔적 — 장작·목재 가로쌓기, 찬장에 낡은 잡동사니, 바구니
    ///   꺾인 구석 = 선아의 흔적 — 문서(경복궁 천추전 책·펼친 책) 흐트러짐 + 등불.
    ///                편차 크게 (다급히 뒤진 자리)
    ///
    /// 에셋: Joseon Library(경복궁 서책·사정전 책상·낙안 찬장·KCDF 등잔 — fbx 직접 임포트라
    ///       재질을 여기서 만들어 입힌다) / 운현궁(단품 프리팹만 — 문·서랍 분리형 조립가구 금지) /
    ///       kcisa(Animator·스크립트 떼고 정물로) / BK(서책·장작·바구니만 — 서양·오컬트 제외).
    ///       낙안읍성 소품 금지 (사용자 지시).
    ///
    /// ⚠️ BK 프리팹 루트에 (270,0,0) 회전이 구워져 있다 — 회전은 반드시 합성(Euler(e)*rot).
    ///    단권 Book*은 (0,0,90) — 눕히려면 X+90 합성. Log는 서 있는 장작 — 눕히려면 X+90.
    ///    Custom/Refraction 재질(Jar 등)은 화면 전체를 하얗게 깬다 — 사용 금지 (전부 실측).
    /// 콜라이더: 크고 높은 가구만 바운즈 박스 1개, 작은 소품은 전부 제거 (끼임 방지).
    /// 통행로(동벽 문→은닉처→중간→구석)와 줄사다리 아래는 비워 둔다.
    /// </summary>
    public static class ArchiveFurnisher
    {
        const string RootName = "서고_소품";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Archive/Props";
        const float FY = -6.60f;

        const string UH = "Assets/UnhyeongungCollect/Prefabs/";
        const string BK = "Assets/BK_AlchemistHouse/Prefabs/";
        const string KC = "Assets/KTinteractiveProp/Volum 02/Prefabs/";
        const string JL = "Assets/Joseon Library/";

        static Transform root;
        static System.Random rnd;
        static readonly Dictionary<string, Material> jlMats = new Dictionary<string, Material>();
        static int placed, colliders;
        static readonly List<GameObject> bigFurniture = new List<GameObject>();
        static readonly List<string> rotatedLog = new List<string>();
        /// <summary>가구별 실측 바운즈 (임시 콜라이더 합집합). ⚠️ kcisa 스킨드 렌더러의 bounds는
        /// Animator 제거 후 부풀거나 어긋난다 — 렌더러 바운즈로 선반 중심·클램프를 잡으면
        /// 책 줄이 옆으로 밀려 허공에 걸린다 (실측).</summary>
        static readonly Dictionary<GameObject, Bounds> accBounds = new Dictionary<GameObject, Bounds>();

        [MenuItem("Tools/이문록/서고 소품 배치")]
        public static void Build()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gyeonu_Observatory")
            {
                Debug.LogError("[서고 소품] 활성 씬이 Gyeonu_Observatory가 아닙니다");
                return;
            }
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            root = new GameObject(RootName).transform;
            rnd = new System.Random(20260817);
            placed = 0; colliders = 0;
            bigFurniture.Clear(); rotatedLog.Clear(); accBounds.Clear();
            EnsureFolder(MatDir);
            BuildJLMaterials();

            // ═══ 동측 — 수령의 은닉처 (정돈, 편차 작게) ═══
            // 서가 두 틀 (동벽 북단 + 북벽) — 뒷판을 벽에, 선반은 레이캐스트 실측으로 채운다
            var shelfCab = Place(KC + "ShelfCabinet 01.prefab", new Vector3(21.5f, FY, 34.35f), -90f, true, 1.5f);
            FillShelves(shelfCab, -90f, true);
            var shelves = Place(UH + "SM_Shelves241.prefab", new Vector3(20.05f, FY, 34.85f), 180f, true, 1.5f);
            FillShelves(shelves, 180f, true);
            // 수령의 서안 (사정전 책상) — 서가 앞, 방을 향해. 위에 펼친 장부·서책·등잔
            var desk = PlaceJL("gyeongbokgung-sajeongjeon-tableb/source/SM_SJJ_TableB.fbx",
                new Vector3(20.5f, FY, 33.55f), 180f, true, 1.5f);
            if (desk != null)
            {
                float top = TopY(desk);
                PlaceJL("gyeongbokgung-cheonchujeon-openbook/source/SM_CCJ_OpenBook.fbx", new Vector3(20.35f, top, 33.5f), 171f, false, 0f);
                JLBookStack(new Vector3(20.95f, top, 33.62f), 3, 12f);
                PlaceJL("kcdf-lamp-derivation/source/KCDF_2022_prop_Lamp.obj", new Vector3(19.98f, top, 33.6f), 40f, false, 0f,
                    new Vector3(-90f, 0, 0));   // obj가 z-up이라 눕는다 — 세워 준다
            }
            // 서안 곁 책궤 (BookCase305는 이름과 달리 0.36m 책궤다) — 위에 장부 서책
            var bookBox = Place(UH + "SM_BookCase305.prefab", new Vector3(21.55f, FY, 33.15f), -87f, false, 0f);
            if (bookBox != null) JLBookStack(new Vector3(21.55f, TopY(bookBox), 33.15f), 2, -84f);
            // 북벽: 장(단품)
            Place(UH + "SM_Cabinet238.prefab", new Vector3(18.85f, FY, 34.9f), 180f, true, 1.5f);
            // 문 남쪽 구석: 궤짝 군집 — 붉은 함(값진 장물)·나무 궤·소함 스택.
            // ⚠️ 함을 문간(z32.4대)에 두면 차단 AABB가 동벽 문 차선을 막는다 (실측 — 남벽 쪽으로)
            var redBox = Place(UH + "SM_Box286.prefab", new Vector3(20.55f, FY, 30.98f), 4f, true, 1.5f);
            var chest = Place(KC + "HalfChest 01.prefab", new Vector3(21.35f, FY, 31.3f), -92f, true, 1.5f);
            if (chest != null)
            {
                float top = TopY(chest);
                Place(KC + "Box 01.prefab", new Vector3(21.35f, top, 31.45f), -88f, false, 3f);
                Place(KC + "Box 03.prefab", new Vector3(21.4f, top, 31.05f), -95f, false, 3f);
            }
            // 빼돌린 기물 — 함 위·곁 (놋촛대·백자·약탕관·말 = 관아에서 빼돌린 물건·자재)
            if (redBox != null)
            {
                float rTop = TopY(redBox);
                Place(UH + "SM_Candlestick128.prefab", new Vector3(20.75f, rTop, 31.0f), -60f, false, 0f);
                Place(UH + "SM_WhitePorcelainBottle.prefab", new Vector3(20.35f, rTop, 30.95f), 20f, false, 0f);
            }
            Place(UH + "SM_PotForMedicines.prefab", new Vector3(21.85f, FY, 30.85f), 150f, false, 0f);
            Place(UH + "SM_Mal.prefab", new Vector3(20.05f, FY, 31.45f), 8f, false, 0f);   // 말(斗) — 자재 계량
            // 좌등 — 은닉처의 정돈된 조명 기구 (광원은 빌더의 웅덩이가 맡는다)
            Place(UH + "SM_FloorLamp.prefab", new Vector3(19.35f, FY, 34.75f), 160f, false, 0f);
            // 서가 곁 바닥 서책 줄 (조선식 눕혀 쌓기)
            BKBookStack(new Vector3(21.55f, FY, 33.7f), 5, -90f);

            // ═══ 중간 — 자재 창고의 흔적 (낡고 먼지 쌓임) ═══
            // 남벽: 오래된 목재 — 장작·통나무 가로쌓기 2더미
            LogPile(new Vector3(18.1f, FY, 31.05f), 7, 88f);
            LogPile(new Vector3(19.3f, FY, 30.98f), 5, 92f);
            // 북벽: 낡은 찬장 (낙안읍성 찬장 모델 — Joseon Library 경유) + 잡동사니
            var pantry = PlaceJL("pantry-chest/source/SM_Pantry_Chest.fbx", new Vector3(17.15f, FY, 34.8f), 180f, true, 1.5f);
            FillShelves(pantry, 180f);
            if (pantry != null)
            {
                float top = TopY(pantry);
                Place(UH + "SM_ScoopedWoodenVessel.prefab", new Vector3(16.8f, top, 34.8f), 25f, false, 0f);
                Place(UH + "SM_WoodenTray336.prefab", new Vector3(17.5f, top, 34.75f), -12f, false, 0f);
                Place(UH + "SM_DarkBrownBottle.prefab", new Vector3(17.15f, top, 34.9f), 60f, false, 0f);
            }
            Place(BK + "Items/Misc/Basket02.prefab", new Vector3(16.35f, FY, 34.85f), 30f, false, 0f);
            Place(BK + "Items/Misc/Basket01.prefab", new Vector3(16.0f, FY, 34.55f), 130f, false, 0f);
            // 서벽(줄사다리 남쪽): 책장 하나 더 — "도서관처럼 책장에 책이 빼곡"한 결을 만든다
            var westShelf = Place(KC + "ShelfCabinet 02.prefab", new Vector3(14.35f, FY, 31.9f), 90f, true, 1.5f);
            FillShelves(westShelf, 90f, true);
            // 서벽(줄사다리 북쪽): 낡은 수납장 + 위에 먼지 쌓인 그릇
            var storage = Place(KC + "StorageCabinets 01.prefab", new Vector3(14.45f, FY, 34.6f), 90f, true, 1.5f);
            FillShelves(storage, 90f);
            if (storage != null)
            {
                float top = TopY(storage);
                Place(UH + "SM_WoodenBowl_B.prefab", new Vector3(14.4f, top, 34.4f), 15f, false, 0f);
                Place(UH + "SM_DarkBrownBowl.prefab", new Vector3(14.5f, top, 34.85f), 80f, false, 0f);
            }
            // 남벽 목재 곁 — 낡은 걸상·되말·바구니 (공사 인부들의 흔적)
            Place(UH + "SM_Stool254.prefab", new Vector3(17.0f, FY, 31.3f), 24f, false, 0f);
            Place(UH + "SM_WoodenStep.prefab", new Vector3(20.3f, FY, 30.95f), 95f, false, 0f);

            // ═══ 꺾인 구석 — 선아의 흔적 (편차 크게, 다급히 뒤진 자리) ═══
            // 문갑 (구석 남벽 동편 — 선아 마커(14.5, 28.15) 보행 반경을 비워 둔다) — 선아가 뒤지던 문서장
            PlaceJL("kcdf-mungap-04/source/Table04_Key.fbx", new Vector3(15.75f, FY, 27.85f), 180f, true, 1.5f);
            // 등불 — 빌더의 선아_등불 광원과 같은 자리에 실물 (보행선 밖 서쪽)
            Place(KC + "Lantern.prefab", new Vector3(14.3f, FY, 28.9f), 205f, false, 0f);
            // 흐트러진 문서 — 경복궁 천추전 서책이 바닥에 흩어져 있다
            ScatterJLBooks(new Vector3(14.9f, FY, 28.5f), 7, 1.05f);
            PlaceJL("gyeongbokgung-cheonchujeon-openbook/source/SM_CCJ_OpenBook.fbx", new Vector3(15.05f, FY, 28.2f), 233f, false, 0f);
            // 무너진 서책 더미 — 뒤지다 쓰러뜨린 것
            BKBookStack(new Vector3(14.3f, FY, 27.75f), 5, 140f, 14f);
            // ⚠️ '꽂힌 책 덩어리(Books1~5)'를 바닥에 두면 허공에 뜬 것처럼 어색하다 (사용자 지적) —
            //    구석에 사방탁자 서가를 세우고 그 선반에 빼곡히 꽂는다. 낱권 흩뿌림은 그대로 둔다
            var nookShelf = Place(UH + "SM_Shelves241.prefab", new Vector3(16.42f, FY, 29.2f), 90f, true, 1.5f);
            FillShelves(nookShelf, 90f, true);
            Place(UH + "SM_BlackGlazedJar.prefab", new Vector3(16.35f, FY, 30.3f), 40f, false, 0f);

            // ═══ 벽 따라 바닥 서책 줄 — 서고의 기본 결 (조선식 눕혀 쌓기) ═══
            BKBookStack(new Vector3(14.25f, FY, 33.9f), 6, 90f);
            BKBookStack(new Vector3(14.3f, FY, 33.45f), 4, 96f);
            BKBookStack(new Vector3(18.0f, FY, 34.95f), 5, 178f);
            BKBookStack(new Vector3(16.55f, FY, 30.95f), 3, 12f);
            JLBookStack(new Vector3(14.28f, FY, 32.9f), 4, 88f);
            JLBookStack(new Vector3(21.6f, FY, 34.95f), 3, -95f);

            // 마무리 정착 — 모든 소품의 바닥을 지지면에 실측으로 맞추고, 선반 밖으로 넘친 것을 수납
            SettleProps();

            Debug.Log($"[서고 소품] 배치 완료 — 오브젝트 {placed}개, 가구 콜라이더 {colliders}개, " +
                      $"방향 교정 {rotatedLog.Count}건 [{string.Join(", ", rotatedLog)}], 삼각형 {CountTris(root.gameObject):N0}");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        // ── Joseon Library 재질 (fbx 직접 임포트 — 팩 텍스처 참조, 에셋은 우리 폴더) ──
        static void BuildJLMaterials()
        {
            jlMats.Clear();
            Material M(string slot, string name, string bc, string nm = null, float smooth = 0.3f, float metallic = 0f)
            {
                var m = Mat(name, Color.white, metallic, smooth,
                    bc != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(bc) : null,
                    nm != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(nm) : null);
                jlMats[slot] = m;
                return m;
            }
            string bkT = JL + "gyeongbokgung-cheonchujeon-book/textures/";
            M("M_CCJ_BookA", "M_JL_책A", bkT + "T_CCJ_BookA_BC.png", bkT + "T_CCJ_Book_N.png", 0.32f);
            M("M_CCJ_BookB", "M_JL_책B", bkT + "T_CCJ_BookA_BC.png", bkT + "T_CCJ_Book_N.png", 0.32f);
            M("M_CCJ_BookC", "M_JL_책C", bkT + "T_CCJ_BookA_BC.png", bkT + "T_CCJ_Book_N.png", 0.32f);
            string obT = JL + "gyeongbokgung-cheonchujeon-openbook/textures/";
            M("M_CCJ_OpenBook", "M_JL_펼친책", obT + "T_CCJ_OpenBook_BC.png", obT + "T_CCJ_OpenBook_N.png", 0.3f);
            string tbT = JL + "gyeongbokgung-sajeongjeon-tableb/textures/";
            M("M_SJJ_TableB", "M_JL_사정전책상", tbT + "T_SJJ_TableB_BC.png", tbT + "T_SJJ_TableB_N.png", 0.35f);
            string pcT = JL + "pantry-chest/textures/";
            M("M_Pantry_Chest_01", "M_JL_찬장01", pcT + "T_Pantry_Chest_01_BC.png", pcT + "T_Pantry_Chest_01_N.png", 0.25f);
            M("M_Pantry_Chest_02", "M_JL_찬장02", pcT + "T_Pantry_Chest_02_BC.png", pcT + "T_Pantry_Chest_02_N.png", 0.35f, 0.5f);
            M("KCDF_2022_prop_Lamp", "M_JL_등잔", JL + "kcdf-lamp-derivation/textures/KCDF_2022_prop_Lamp_3.jpg", null, 0.4f);
            string mgT = JL + "kcdf-mungap-04/textures/";
            M("Wood", "M_JL_문갑목", mgT + "Table04_Wood_BaseColor.png", mgT + "Table04_Wood_Normal.png", 0.4f);
            M("Gold sus_Hinge", "M_JL_문갑쇠", mgT + "Table04_Gold_sus_Hinge_BaseColor.png", null, 0.55f, 0.8f);
            jlMats["Gold sus_Handle"] = jlMats["Gold sus_Hinge"];
            jlMats["Gold sus_Handle_02"] = jlMats["Gold sus_Hinge"];
            // 책A/B/C가 각자 BC를 갖도록 덮어쓴다
            jlMats["M_CCJ_BookB"].SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(bkT + "T_CCJ_BookB_BC.png"));
            jlMats["M_CCJ_BookC"].SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(bkT + "T_CCJ_BookC_BC.png"));
        }

        // ── 배치 헬퍼 ─────────────────────────────────────────
        /// <summary>프리팹/모델 배치 — 스크립트·Animator·콜라이더를 떼고, 바운즈 바닥을 groundY에 맞춘다.
        /// 회전은 기존 회전에 합성 (BK 구운 회전 대응). big이면 바운즈 박스 콜라이더 1개.</summary>
        static GameObject Place(string path, Vector3 pos, float yaw, bool big, float jitterDeg,
            Vector3? extraRot = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("[서고 소품] 없음: " + path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(root, false);
            float jy = jitterDeg > 0 ? ((float)rnd.NextDouble() - 0.5f) * 2f * jitterDeg : 0f;
            var rot = Quaternion.Euler(extraRot ?? Vector3.zero);
            go.transform.rotation = Quaternion.Euler(0, yaw + jy, 0) * rot * go.transform.rotation;
            Strip(go);
            Ground(go, pos);
            if (big)
            {
                // 열린 면(선반 칸)이 벽을 향하지 않게 — 이름·기본 회전을 믿지 않고 메시 형태로 실측 교정
                OrientOpenFace(go);
                Ground(go, pos);
                AddBigCollider(go);
                bigFurniture.Add(go);
            }
            placed++;
            return go;
        }

        /// <summary>Joseon Library fbx/obj 배치 + 재질 슬롯 교체.</summary>
        static GameObject PlaceJL(string rel, Vector3 pos, float yaw, bool big, float jitterDeg,
            Vector3? extraRot = null)
        {
            var go = Place(JL + rel, pos, yaw, big, jitterDeg, extraRot);
            if (go == null) return null;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && jlMats.TryGetValue(mats[i].name, out var m)) mats[i] = m;
                r.sharedMaterials = mats;
            }
            return go;
        }

        /// <summary>BK 서책 눕혀 쌓기 — 단권 Book*은 (0,0,90) 구움이라 X+90 합성으로 눕힌다.</summary>
        static void BKBookStack(Vector3 basePos, int n, float yaw, float jitterDeg = 8f)
        {
            string[] books = { "Book02", "Book04", "Book06c", "Book08", "Book10", "Book10b", "Book12", "Book07", "Book13" };
            float y = basePos.y;
            for (int i = 0; i < n; i++)
            {
                string b = books[rnd.Next(books.Length)];
                var go = Place(BK + "Items/Books/" + b + ".prefab",
                    new Vector3(basePos.x + ((float)rnd.NextDouble() - 0.5f) * 0.05f, y,
                                basePos.z + ((float)rnd.NextDouble() - 0.5f) * 0.05f),
                    yaw, false, jitterDeg, new Vector3(90f, 0, 0));
                if (go == null) continue;
                y = TopWorldY(go) + 0.004f;
            }
        }

        /// <summary>경복궁 천추전 서책 쌓기 (A/B/C 번갈아).</summary>
        static void JLBookStack(Vector3 basePos, int n, float yaw)
        {
            string[] abc = { "A", "B", "C" };
            float y = basePos.y;
            for (int i = 0; i < n; i++)
            {
                var go = PlaceJL("gyeongbokgung-cheonchujeon-book/source/SM_CCJ_Book" + abc[rnd.Next(3)] + ".fbx",
                    new Vector3(basePos.x + ((float)rnd.NextDouble() - 0.5f) * 0.04f, y,
                                basePos.z + ((float)rnd.NextDouble() - 0.5f) * 0.04f), yaw, false, 9f);
                if (go == null) continue;
                y = TopWorldY(go) + 0.003f;
            }
        }

        /// <summary>선아가 흩뜨린 서책 — 반경 안에 낱권을 아무렇게나.</summary>
        static void ScatterJLBooks(Vector3 center, int n, float radius)
        {
            string[] abc = { "A", "B", "C" };
            for (int i = 0; i < n; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 0.25f + (float)rnd.NextDouble() * radius;
                PlaceJL("gyeongbokgung-cheonchujeon-book/source/SM_CCJ_Book" + abc[rnd.Next(3)] + ".fbx",
                    new Vector3(center.x + Mathf.Cos(a) * r, center.y, center.z + Mathf.Sin(a) * r * 0.8f),
                    (float)rnd.NextDouble() * 360f, false, 0f);
            }
        }

        /// <summary>BK 장작 가로쌓기 — Log는 서 있는 프리팹이라 X+90 합성으로 눕힌다.</summary>
        static void LogPile(Vector3 basePos, int n, float yaw)
        {
            string[] logs = { "Log01", "Log02", "Log03" };
            for (int i = 0; i < n; i++)
            {
                int row = i / 3, col = i % 3;
                var go = Place(BK + "Items/Misc/" + logs[rnd.Next(logs.Length)] + ".prefab",
                    new Vector3(basePos.x + (col - 1) * 0.24f + ((float)rnd.NextDouble() - 0.5f) * 0.05f,
                                basePos.y + row * 0.21f,
                                basePos.z + ((float)rnd.NextDouble() - 0.5f) * 0.06f),
                    yaw, false, 7f, new Vector3(90f, 0, 0));
            }
        }

        /// <summary>서가 선반 채우기 — 임시 메시 콜라이더를 붙이고 위에서 아래로 레이캐스트해
        /// 선반 면 높이를 실측한 뒤, 선반마다 서책 스택 2벌 + 기물 1~2개(7~8점)를 얹는다.
        /// Room00 실측 밀도(선반당 7.7점)가 기준 — 이 밀도에 못 미치면 허전해진다 (사용자 확정 규칙).</summary>
        static void FillShelves(GameObject go, float yawDeg, bool includeTop = false)
        {
            if (go == null) return;
            var levels = ShelfLevels(go, includeTop);
            var b = accBounds.ContainsKey(go) ? accBounds[go] : WorldBounds(go);
            // 선반 진행 축 = 가구의 가로 방향 (수평 투영)
            var axis = Quaternion.Euler(0, yawDeg, 0) * Vector3.right;
            axis.y = 0; axis.Normalize();
            string[] small = { UH + "SM_WhitePorcelainBottle.prefab", UH + "SM_DarkBrownBottle.prefab",
                               UH + "SM_WhitePorcelainBowl.prefab", UH + "SM_Cup28.prefab" };
            int shelfIdx = 0;
            bool axisX = Mathf.Abs(axis.x) > Mathf.Abs(axis.z);
            float halfA = ((axisX ? b.size.x : b.size.z) - 0.12f) * 0.5f;   // 선반 가용 반폭
            bool wide = halfA * 2f >= 0.6f;                                  // 책 줄(~0.9)이 들어가는 폭인가
            foreach (var y in levels)
            {
                if (shelfIdx++ >= 4) break;
                var c = new Vector3(b.center.x, y, b.center.z);
                if (wide)
                {
                    // 꽂힌 책 줄(Books1~5) — '책장에 책이 빼곡한' 도서관 인상의 핵심 (선반 전용)
                    var row = Place(BK + "Items/Books/Books" + (1 + rnd.Next(5)) + ".prefab", c + axis * (-0.55f * halfA), yawDeg, false, 4f);
                    if (!FitRow(row, go, axis))
                    {   // 줄이 안 들어가는 선반 — 폐기하고 눕혀 쌓기로 대체 (밖으로 걸치는 것 금지)
                        if (row != null) { Object.DestroyImmediate(row); placed--; }
                        BKBookStack(c + axis * (-0.45f * halfA), 3, yawDeg + 92f, 8f);
                    }
                    BKBookStack(c + axis * (0.2f * halfA), 3, yawDeg + 90f, 9f);
                }
                else
                {
                    // 좁은 선반(폭 < 0.6) — 줄이 물리적으로 안 들어간다 (실측): 눕혀 쌓기 두 벌로 채움
                    BKBookStack(c + axis * (-0.45f * halfA), 3, yawDeg + 90f, 8f);
                    BKBookStack(c + axis * (0.35f * halfA), 3, yawDeg + 88f, 9f);
                }
                Place(small[rnd.Next(small.Length)], c + axis * (0.75f * halfA), (float)rnd.NextDouble() * 360f, false, 0f);
                if (rnd.Next(2) == 0)
                    JLBookStack(c + axis * (0.7f * halfA), 1, yawDeg + 80f);
            }
        }

        // ── 임시 콜라이더 (메시 실측용 — 스킨드는 베이크) ──
        static List<Collider> AddTempCols(GameObject go, List<Mesh> bakes)
        {
            var temp = new List<Collider>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null)
                { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc); }
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = new Mesh(); smr.BakeMesh(m); bakes.Add(m);
                var mc = smr.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = m; temp.Add(mc);
            }
            Physics.SyncTransforms();
            return temp;
        }

        static void RemoveTempCols(List<Collider> temp, List<Mesh> bakes)
        {
            foreach (var c in temp) if (c != null) Object.DestroyImmediate(c);
            foreach (var m in bakes) if (m != null) Object.DestroyImmediate(m);
        }

        /// <summary>임시 콜라이더 합집합 = 실측 월드 바운즈 (스킨드 바운즈 오차 회피).</summary>
        static Bounds ColUnion(List<Collider> temp, GameObject fallback)
        {
            if (temp.Count == 0) return WorldBounds(fallback);
            var b = temp[0].bounds;
            foreach (var c in temp) b.Encapsulate(c.bounds);
            return b;
        }

        /// <summary>열린 면(선반 칸) 실측 교정 — 사방 수평 레이 격자(높이 5×가로 5)로 각 면의
        /// 평균 관통 깊이를 재서, 가장 깊이 뚫리는 면(=열린 정면)이 가장 가까운 벽의 안쪽을
        /// 향하게 회전한다. 이름·기본 회전은 믿지 않는다 (사용자 지시).
        /// 사방이 고르게 뚫리거나(사방탁자·상) 고르게 막히면(닫힌 궤) 판별 불가 — 건드리지 않는다.</summary>
        static void OrientOpenFace(GameObject go)
        {
            var bakes = new List<Mesh>();
            var temp = AddTempCols(go, bakes);
            var b = ColUnion(temp, go);
            accBounds[go] = b;
            var dirs = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            // 지표 = "깊은 관통 레이 비율" — 열린 정면은 레이가 안쪽 깊숙이(또는 관통까지) 들어가는
            // 비율이 높다. 평균 깊이는 문틀·선반 앞단에 희석돼 둔감했다 (HalfChest 미교정 실측)
            var deepFrac = new float[4];
            for (int d = 0; d < 4; d++)
            {
                var dir = dirs[d];
                float ext = dir.x != 0 ? b.extents.x : b.extents.z;
                var lat = dir.x != 0 ? Vector3.forward : Vector3.right;
                float latExt = dir.x != 0 ? b.extents.z : b.extents.x;
                int deep = 0, cnt = 0;
                for (int hi = 0; hi < 5; hi++)
                    for (int li = -2; li <= 2; li++)
                    {
                        var origin = b.center + dir * (ext + 0.3f) + lat * (latExt * 0.32f * li);
                        origin.y = b.min.y + b.size.y * (0.25f + 0.15f * hi);
                        var hits = Physics.RaycastAll(origin, -dir, ext * 2f + 0.35f);
                        System.Array.Sort(hits, (a, b2) => a.distance.CompareTo(b2.distance));
                        float dv = -1f;
                        foreach (var h in hits)
                            if (temp.Contains(h.collider)) { dv = h.distance - 0.3f; break; }
                        if (dv < 0) dv = ext * 2f;   // 관통 = 그 면은 완전 개방
                        if (dv > ext * 0.9f) deep++;
                        cnt++;
                    }
                deepFrac[d] = (float)deep / cnt;
            }
            RemoveTempCols(temp, bakes);
            int best = 0; for (int d = 1; d < 4; d++) if (deepFrac[d] > deepFrac[best]) best = d;
            float second = -1f; for (int d = 0; d < 4; d++) if (d != best && deepFrac[d] > second) second = deepFrac[d];
            if (deepFrac[best] < 0.15f || deepFrac[best] - second < 0.15f) return;   // 판별 불가 (사면 개방/밀폐)
            var desired = InwardDir(b.center);
            if (Vector3.Dot(dirs[best], desired) > 0.9f) return;    // 이미 안쪽
            float delta = Vector3.SignedAngle(dirs[best], desired, Vector3.up);
            go.transform.RotateAround(b.center, Vector3.up, delta);
            rotatedLog.Add(go.name + " " + Mathf.RoundToInt(delta) + "°");
        }

        /// <summary>꽂힌 책 줄을 선반 축에 정렬하고 가구 발자국 안으로 밀어 넣는다.
        /// Books 프리팹은 줄이 로컬 축과 어긋나게 배열돼 있어 yaw만 맞춰선 대각선으로 걸친다 (실측) —
        /// 5°씩 훑으며 선반 깊이 방향 AABB가 최소가 되는 각을 찾는다.</summary>
        /// <returns>줄이 선반 안에 들어갔으면 true. false면 호출자가 줄을 폐기하고 쌓기로 대체할 것.</returns>
        static bool FitRow(GameObject row, GameObject furniture, Vector3 axis)
        {
            if (row == null || furniture == null) return false;
            bool axisX = Mathf.Abs(axis.x) > Mathf.Abs(axis.z);
            float bestPerp = float.MaxValue; int bestStep = 0;
            var center = WorldBounds(row).center;
            for (int i = 0; i < 36; i++)
            {
                row.transform.RotateAround(center, Vector3.up, 5f);
                var rb = WorldBounds(row);
                float perp = axisX ? rb.size.z : rb.size.x;
                if (perp < bestPerp) { bestPerp = perp; bestStep = i; }
            }
            // 스윕이 끝나면 시작+180° 상태 — 최적 각까지 차이만큼 되돌린다
            row.transform.RotateAround(center, Vector3.up, (bestStep + 1) * 5f - 180f);
            // 줄이 선반 폭보다 길면 폭에 맞춰 축소 (책 크기 왜곡은 0.65배까지만 허용)
            var fb = accBounds.ContainsKey(furniture) ? accBounds[furniture] : WorldBounds(furniture);
            var bb = WorldBounds(row);
            float rowLen = axisX ? bb.size.x : bb.size.z;
            float avail = (axisX ? fb.size.x : fb.size.z) - 0.12f;
            if (rowLen > avail && avail > 0.2f)
            {
                float k = Mathf.Max(0.6f, avail / rowLen);
                var cen = bb.center;
                row.transform.localScale *= k;
                var nb = WorldBounds(row);
                row.transform.position += cen - nb.center;
                bb = WorldBounds(row);
                rowLen = axisX ? bb.size.x : bb.size.z;
                // 0.6배로도 안 들어가는 긴 줄(ALP Books는 최장 1.5m) — 실패 판정, 호출자가 대체한다
                if (rowLen > avail + 0.03f) return false;
            }
            // 가구 발자국 안으로 (양쪽 0.05 여유)
            var shift = Vector3.zero;
            if (bb.min.x < fb.min.x + 0.05f) shift.x = fb.min.x + 0.05f - bb.min.x;
            if (bb.max.x > fb.max.x - 0.05f) shift.x = fb.max.x - 0.05f - bb.max.x;
            if (bb.min.z < fb.min.z + 0.05f) shift.z = fb.min.z + 0.05f - bb.min.z;
            if (bb.max.z > fb.max.z - 0.05f) shift.z = fb.max.z - 0.05f - bb.max.z;
            row.transform.position += shift;
            return true;
        }

        /// <summary>가장 가까운 벽에서 방 안쪽으로 향하는 방향 (꺾인 구석은 구석 벽 기준).</summary>
        static Vector3 InwardDir(Vector3 c)
        {
            bool nook = c.z < 30.7f && c.x < 17.2f;
            float dW = c.x - 13.9f, dE = (nook ? 16.7f : 21.9f) - c.x;
            float dS = c.z - (nook ? 27.5f : 30.7f), dN = 35.3f - c.z;
            float mn = Mathf.Min(Mathf.Min(dW, dE), Mathf.Min(dS, dN));
            if (mn == dW) return Vector3.right;
            if (mn == dE) return Vector3.left;
            if (mn == dS) return Vector3.forward;
            return Vector3.back;
        }

        /// <summary>마무리 정착 — 모든 소품의 바닥 Y를 아래 지지면(가구 선반·바닥·먼저 정착한 소품)에
        /// 실측으로 맞추고, 가구 위 소품이 가구 발자국 밖으로 넘치면 안으로 밀어 넣는다.
        /// 낮은 것부터 정착시켜 책 더미의 쌓임 관계를 보존한다. 결과 수치는 로그로 보고.</summary>
        static void SettleProps()
        {
            var bakes = new List<Mesh>();
            var temp = new List<Collider>();
            var colOwner = new Dictionary<Collider, GameObject>();
            foreach (var f in bigFurniture)
            {
                var t2 = AddTempCols(f, bakes);
                foreach (var c in t2) colOwner[c] = f;
                temp.AddRange(t2);
            }
            var props = new List<GameObject>();
            foreach (Transform ch in root)
                if (!bigFurniture.Contains(ch.gameObject) && ch.GetComponentInChildren<Renderer>(true) != null)
                    props.Add(ch.gameObject);
            props.Sort((a, b2) => WorldBounds(a).min.y.CompareTo(WorldBounds(b2).min.y));

            var supports = new List<Bounds>();
            int adj = 0, hMoved = 0, measured = 0;
            float sumErr = 0, maxErr = 0;
            foreach (var p in props)
            {
                var b = WorldBounds(p);
                float surf = float.NegativeInfinity;
                GameObject furn = null;
                var hits = Physics.RaycastAll(new Vector3(b.center.x, b.min.y + 0.06f, b.center.z), Vector3.down, 4.5f);
                System.Array.Sort(hits, (a, b2) => a.distance.CompareTo(b2.distance));
                foreach (var h in hits)
                {
                    if (h.collider.name == "차단" || h.collider.name == "추락방지판") continue;
                    if (h.normal.y < 0.5f) continue;
                    surf = h.point.y;
                    colOwner.TryGetValue(h.collider, out furn);
                    break;
                }
                if (float.IsNegativeInfinity(surf)) { supports.Add(b); continue; }
                // 먼저 정착한 소품 위에 쌓인 경우 — 발자국이 30% 이상 겹치면 그 윗면이 지지면
                foreach (var s in supports)
                {
                    float ox = Mathf.Min(b.max.x, s.max.x) - Mathf.Max(b.min.x, s.min.x);
                    float oz = Mathf.Min(b.max.z, s.max.z) - Mathf.Max(b.min.z, s.min.z);
                    if (ox > b.size.x * 0.3f && oz > b.size.z * 0.3f && s.max.y > surf && s.max.y <= b.min.y + 0.2f)
                        surf = s.max.y;
                }
                // 선반 안 수납 — 가구 위 소품이 가구 발자국 밖으로 넘치면 안으로
                if (furn != null)
                {
                    var fb = accBounds.ContainsKey(furn) ? accBounds[furn] : WorldBounds(furn);
                    var shift = Vector3.zero;
                    if (b.min.x < fb.min.x + 0.02f) shift.x = fb.min.x + 0.02f - b.min.x;
                    if (b.max.x > fb.max.x - 0.02f) shift.x = fb.max.x - 0.02f - b.max.x;
                    if (b.min.z < fb.min.z + 0.02f) shift.z = fb.min.z + 0.02f - b.min.z;
                    if (b.max.z > fb.max.z - 0.02f) shift.z = fb.max.z - 0.02f - b.max.z;
                    if (shift.magnitude > 0.005f && shift.magnitude < 0.45f)
                    {
                        p.transform.position += shift;
                        b = WorldBounds(p);
                        hMoved++;
                    }
                }
                float gap = b.min.y - surf;
                measured++;
                if (Mathf.Abs(gap) > 0.004f)
                {
                    p.transform.position -= Vector3.up * (gap - 0.002f);
                    adj++; sumErr += Mathf.Abs(gap);
                    if (Mathf.Abs(gap) > maxErr) maxErr = Mathf.Abs(gap);
                    b = WorldBounds(p);
                }
                supports.Add(b);
            }
            RemoveTempCols(temp, bakes);
            Debug.Log("[서고 소품] 정착 — 소품 " + measured + "개 실측, 높이 보정 " + adj + "개 (평균 "
                + (adj > 0 ? (sumErr / adj * 100f).ToString("F1") : "0") + "cm, 최대 " + (maxErr * 100f).ToString("F1")
                + "cm), 선반 안 수납 " + hMoved + "개");
        }

        /// <summary>선반 면 높이 실측 — 임시 콜라이더(+스킨드 베이크)에 하향 반복 레이캐스트.
        /// 위를 향한 면만 취하고 6cm 안의 중복을 합친다. 맨 윗면(상판)은 제외한다.
        /// 표본 기둥은 가로축을 따라 5곳 — 가운데만 찍으면 문설주·문짝에 막혀 빈 선반으로 오판한다.</summary>
        static List<float> ShelfLevels(GameObject go, bool includeTop = false)
        {
            var bakes = new List<Mesh>();
            var temp = AddTempCols(go, bakes);
            var b = ColUnion(temp, go);
            accBounds[go] = b;
            var ys = new List<float>();
            var axis = b.size.x > b.size.z ? Vector3.right : Vector3.forward;
            float aSize = Mathf.Max(b.size.x, b.size.z);
            // ⚠️ RaycastAll은 콜라이더당 첫 교차 1건만 준다 (통짜 메시 서가 = 상판만 잡힘, 실측).
            //    히트 지점 바로 아래에서 레이를 다시 쏘며 내부 선반 면을 차례로 걷는다
            foreach (var frac in new[] { -0.3f, -0.15f, 0f, 0.15f, 0.3f })
            {
                var off = axis * (aSize * frac);
                float castY = b.max.y + 0.3f;
                for (int guard = 0; guard < 12 && castY > b.min.y + 0.03f; guard++)
                {
                    var origin = new Vector3(b.center.x + off.x, castY, b.center.z + off.z);
                    if (!Physics.Raycast(origin, Vector3.down, out var hit, castY - b.min.y + 0.1f)) break;
                    castY = hit.point.y - 0.04f;
                    if (!temp.Contains(hit.collider) || hit.normal.y < 0.7f) continue;
                    // 상판: 개방형 서가(사방탁자류)는 맨 윗단도 책을 얹는 선반이다 — 제외하면
                    // 윗단만 텅 빈 "빈 책장"으로 보인다 (실측). 손으로 위를 채운 장만 제외
                    if (!includeTop && hit.point.y > b.max.y - 0.08f) continue;
                    bool merged = false;
                    for (int i = 0; i < ys.Count; i++)
                        if (Mathf.Abs(ys[i] - hit.point.y) < 0.06f) { merged = true; break; }
                    if (!merged) ys.Add(hit.point.y);
                }
            }
            RemoveTempCols(temp, bakes);
            ys.Sort();
            Debug.Log("[서고 소품] " + go.name + " 선반 " + ys.Count + "단" + (ys.Count == 0 ? " ⚠️ 빈 선반 — 채움 불가" : ""));
            return ys;
        }

        // ── 유틸 ─────────────────────────────────────────────
        static void Strip(GameObject go)
        {
            // ⚠️ BK 프리팹에는 비-키네마틱 Rigidbody가 들어 있다 — 콜라이더만 떼고 남겨 두면
            //    Play 순간 자유낙하해 y -3355까지 떨어지며 "책이 사라진다" (2026-08-15 실측).
            //    (위층 책은 static 배칭이 지오메트리를 구워 둔 덕에 낙하가 안 보였을 뿐이다)
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);   // kcisa AnimProp 등
            foreach (var l in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);  // 광원은 빌더 소관 (Room00 문법)
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>바운즈 바닥 중심이 pos에 오도록 이동 (pos.y = 바닥면).</summary>
        static void Ground(GameObject go, Vector3 pos)
        {
            var b = WorldBounds(go);
            go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
        }

        static float TopWorldY(GameObject go) => WorldBounds(go).max.y;
        static float TopY(GameObject go) => WorldBounds(go).max.y;                 // 월드 상판 높이
        static float StackOn(GameObject go) => go == null ? 0f : WorldBounds(go).max.y - FY;   // FY 기준 상판 오프셋

        static void AddBigCollider(GameObject go)
        {
            var b = accBounds.ContainsKey(go) ? accBounds[go] : WorldBounds(go);
            var col = new GameObject("차단");
            col.transform.SetParent(go.transform, false);
            col.transform.position = b.center;
            col.transform.rotation = Quaternion.identity;
            var bc = col.AddComponent<BoxCollider>();
            bc.size = b.size;
            col.isStatic = true;
            colliders++;
        }

        static Material Mat(string name, Color c, float metallic, float smooth, Texture2D baseMap, Texture2D normal)
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
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static int CountTris(GameObject go)
        {
            int n = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) n += mf.sharedMesh.triangles.Length / 3;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) n += smr.sharedMesh.triangles.Length / 3;
            return n;
        }
    }
}
