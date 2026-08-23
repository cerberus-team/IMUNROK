using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 조사청(광풍각) 실내를 상자로 짓는다. 메뉴: [이문록 ▸ 조사청 실내 짓기]
    ///
    /// 왜 손으로 짓는가: 지금 조사청은 받아온 소쇄원 정원의 광풍각 안에 세간만 놓은 것이다.
    /// 그 한 채를 보자고 정원 전체(682개 메시, 1,120만 삼각형)를 들고 있어야 하고,
    /// 배경을 걷어내면 방까지 같이 사라진다. 사랑채가 같은 이유로 상자 실내를 따로 지었다
    /// (원본 20만 삼각형 → 상자 9천). 조사청도 같은 길로 간다.
    ///
    /// 치수는 지어내지 않고 광풍각에서 잰 것이다 — 기둥 격자 한 칸 1.5m,
    /// 정면 9m × 측면 6m, 마루 윗면 0.71, 도리 3.10. 그래서 배경을 켜 둔 채로 지어도
    /// 이미 놓인 세간(보료·문갑·사건판)이 제자리에 맞는다.
    ///
    /// 방 배치도 원본 그대로다. 광풍각은 통칸이 아니라 <b>가운데 방 둘 + 동쪽 열린 마루</b>다.
    ///   · 서쪽 방(x -4.54~-1.54) — 보료·경상·문갑이 있는 자리. 플레이어가 여기서 시작한다.
    ///   · 가운데 방(x -1.54~1.46) — 북벽에 사건판이 걸린다.
    ///   · 동쪽 마루(x 1.46~4.46) — 바둑판이 놓인 열린 자리.
    /// 북쪽만 벽이고 나머지 세 면은 문이다(원본의 문짝 자리와 같다).
    ///
    /// <b>무늬 늘어남</b>: 유니티 기본 큐브는 어느 면이든 UV 가 0~1 이다. 그것을 10m 로
    /// 늘리면 벽돌 한 장이 10m 짜리가 된다 — 처음 지었을 때 기단과 마루가 뭉개져 보인
    /// 까닭이 이것이다. 그래서 부재마다 제 크기에 맞춰 무늬를 되풀이시킨다(TilePerMeter).
    ///
    /// 다시 부르면 통째로 지우고 새로 짓는다. 치수를 고치고 메뉴를 다시 누르면 된다.
    /// </summary>
    public static class HallBuilder
    {
        private const string RootName = "조사청_실내";

        // ── 광풍각에서 잰 치수(로컬 m) ─────────────────
        private const float Bay = 1.5f;                    // 기둥 한 칸
        private const float XMin = -4.54f, XMax = 4.46f;   // 정면 9m (일곱 줄)
        private const float ZMin = -3.04f, ZMax = 2.96f;   // 측면 6m (다섯 줄)

        private const float GidanTop = 0.25f;    // 기단 윗면
        private const float StoneTop = 0.41f;    // 주춧돌 윗면 = 기둥이 앉는 자리
        private const float FloorTop = 0.71f;    // 마루 윗면 — 세간이 놓인 높이
        private const float FloorThick = 0.16f;
        private const float BeamBottom = 3.10f;  // 도리 밑면 = 기둥 머리
        private const float BeamThick = 0.22f;

        // 안에서 올려다보면 반자가 먼저고 서까래는 그 위에 숨는다. 순서가 뒤집히면
        // 천장을 발라 놓고 그 아래로 서까래가 내려온 꼴이 된다.
        private const float CeilY = 3.36f;
        private const float RafterY = 3.52f;

        // 지붕. 한옥 지붕은 얕지 않다 — 처음에 13도로 눕혔더니 유리 차양이 되었다.
        // 물매를 스물다섯 도로 세우고 처마를 1.2m 내밀어 그늘이 지게 한다.
        private const float EaveY = 3.60f;       // 처마 끝
        private const float RidgeY = 5.55f;      // 용마루
        private const float Overhang = 1.20f;
        private const float RoofThick = 0.26f;

        private const float PillarW = 0.30f;
        private const float WallThick = 0.16f;
        private const float DoorTop = 2.75f;     // 문 위 인방 밑
        private const float RafterStep = 0.6f;

        // ── 창(窓) ────────────────────────────────
        //
        // 옆면은 문이 아니라 창이다. 문은 드나드는 데고 창은 앉아서 내다보는 데라,
        // 아래에 머름을 두르고 그 위에만 종이를 바른다. 보료에 앉은 눈높이가
        // 대략 1.1m 인데 머름 윗면이 1.16 이라, 앉으면 창턱 너머로 마당이 보이고
        // 서면 창살이 눈앞에 온다 — 앉는 방이라는 것이 자세로 드러난다.
        private const float SillH = 0.45f;       // 머름 높이(마루 윗면에서)
        private const float WinH = 1.40f;        // 창 높이
        private const float FrameW = 0.075f;     // 창틀 굵기
        private const float BarW = 0.030f;       // 살 굵기
        private const float BarT = 0.030f;       // 살이 면에서 튀어나오는 깊이
        private const float SalLeaf = 0.75f;     // 살을 짜는 한 짝의 너비
        private const int SalDepth = 1;          // 바람개비를 몇 겹 두를지(0=한 겹, 1=두 겹)
        private const float PairW = 1.5f;        // 한 칸에 두 짝 — 기둥 줄과 같은 1.5m
        private const float OpenAngle = 22f;     // 열어 둔 짝이 밖으로 밀린 각(도)
        private const int OpenIdx = 1;           // 몇 번째 짝을 열어 둘지(0부터). -1이면 다 닫는다

        // 방 둘의 경계(원본 벽 자리에서 잰 것)
        private const float RoomZMin = -1.54f, RoomZMax = 1.46f;
        private const float WestX0 = -4.54f, WestX1 = -1.54f;
        private const float MidX0 = -1.54f, MidX1 = 1.46f;

        private static Material _wood, _beam, _rafter, _wall, _ceil, _floor, _gidan, _stone, _paper, _roof, _jangpan, _changho;
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

        [MenuItem("이문록/조사청 실내 짓기")]
        public static void Build()
        {
            if (!LoadMaterials()) return;

            // 다시 짓기는 <b>지우고 새로 짓는</b> 것이다. 손으로 옮겨 둔 벽이며 걷어낸
            // 칸막이며 전부 같이 사라진다. 한 번 그렇게 날려 먹은 뒤로 여기서 묻는다.
            var old = GameObject.Find(RootName);
            if (old != null)
            {
                bool go = EditorUtility.DisplayDialog(
                    "조사청 실내를 다시 짓습니다",
                    "이미 지어 둔 방을 통째로 지우고 새로 짓습니다.\n" +
                    "손으로 옮기거나 지운 것이 있으면 전부 사라집니다.\n\n계속할까요?",
                    "다시 짓는다", "그만둔다");
                if (!go) { Debug.Log("[조사청] 다시 짓기를 그만두었습니다."); return; }
                Undo.DestroyObjectImmediate(old);
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "조사청 실내 짓기");

            // 자리와 방향은 원본 광풍각에서 그대로 받아 온다. 배경을 지운 뒤에도
            // 방이 제자리에 있어야 세간을 다시 놓을 일이 없다.
            var pav = GameObject.Find("소쇄원_정원/Gwangpunggak_Pavilion");
            if (pav != null)
            {
                root.transform.SetPositionAndRotation(pav.transform.position, pav.transform.rotation);
            }
            else
            {
                root.transform.SetPositionAndRotation(new Vector3(70.13f, 137.73f, 283.33f),
                                                      Quaternion.Euler(0f, 140f, 0f));
            }

            var 구조 = Group(root.transform, "구조");
            BuildGidan(Group(구조, "기단"));
            BuildStones(Group(구조, "주춧돌"));
            BuildPillars(Group(구조, "기둥"));
            BuildFloor(Group(구조, "마루"));
            BuildBeams(Group(구조, "도리_보"));
            BuildRafters(Group(구조, "서까래"));
            BuildCeiling(Group(구조, "반자천장"));
            BuildWalls(Group(구조, "벽"));
            BuildWindows(Group(구조, "창"));
            BuildDoors(Group(구조, "문"));
            BuildRoof(Group(구조, "지붕"));

            int tris = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;

            Selection.activeGameObject = root;
            Debug.Log($"[조사청] 실내를 지었습니다 — 오브젝트 {root.GetComponentsInChildren<Transform>().Length}개, {tris} 삼각형.");
        }

        /// <summary>
        /// <b>남쪽 문만</b> 지우고 다시 짠다. 메뉴: [이문록 ▸ 조사청 ▸ 남쪽 문 다시 짜기]
        ///
        /// 방 전체를 다시 지으면 손으로 밀어 둔 창짝이며 옮겨 둔 세간이며 전부 사라진다.
        /// 문 하나 고치자고 방을 헐 수는 없으므로, 구조 밑의 <b>문 무리만</b> 갈아 끼운다.
        /// </summary>
        [MenuItem("이문록/조사청/남쪽 문 다시 짜기")]
        public static void RebuildDoors()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[조사청] 플레이를 멈추고 다시 실행하세요.");
                return;
            }
            if (!LoadMaterials()) return;

            var room = GameObject.Find(RootName);
            if (room == null) { Debug.LogWarning("[조사청] 지은 방이 없습니다."); return; }
            var 구조 = room.transform.Find("구조");
            if (구조 == null) { Debug.LogWarning("[조사청] 구조 무리를 못 찾았습니다."); return; }

            var old = 구조.Find("문");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var g = Group(구조, "문");
            Undo.RegisterCreatedObjectUndo(g.gameObject, "남쪽 문 다시 짜기");
            BuildDoors(g);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(room.scene);
            Selection.activeGameObject = g.gameObject;
            Debug.Log("[조사청] 남쪽 문을 여닫이 분합문으로 다시 짰습니다 — 한 면에 넉 짝, 밖으로 열립니다.");
        }

        /// <summary>옆에 빼둘 때 원본에서 이만큼 떨어뜨린다(m).</summary>
        private const float AsideDistance = 24f;

        // ── 옆에 빼두기 ──────────────────────────────
        //
        // 새로 지은 방을 원본 광풍각 자리에 그대로 겹쳐 두면 둘 중 하나는 꺼야 하고,
        // 끄면 견줄 수가 없다. 마음에 들 때까지는 옆에 나란히 세워 두고 오간다.

        [MenuItem("이문록/조사청 실내 옆으로 빼두기")]
        public static void ParkAside()
        {
            var room = GameObject.Find(RootName);
            if (room == null) { Debug.LogWarning("[조사청] 지은 방이 없습니다."); return; }

            var pav = FindPavilion();
            if (pav == null) { Debug.LogWarning("[조사청] 광풍각을 못 찾았습니다."); return; }

            Undo.RecordObject(room.transform, "조사청 실내 옆으로");
            room.transform.position = pav.transform.position + pav.transform.right * AsideDistance;
            room.transform.rotation = pav.transform.rotation;

            // 원본을 도로 켠다 — 세간은 원본 안에 있으므로 그쪽이 다시 조사청이 된다.
            Undo.RecordObject(pav, "광풍각 켜기");
            pav.SetActive(true);
            Selection.activeGameObject = room;
            Debug.Log("[조사청] 지은 방을 옆으로 빼두고 원본 광풍각을 켰습니다.");
        }

        [MenuItem("이문록/조사청 실내 제자리에 앉히기")]
        public static void PutBack()
        {
            var room = GameObject.Find(RootName);
            if (room == null) { Debug.LogWarning("[조사청] 지은 방이 없습니다."); return; }

            var pav = FindPavilion();
            if (pav == null) { Debug.LogWarning("[조사청] 광풍각을 못 찾았습니다."); return; }

            Undo.RecordObject(room.transform, "조사청 실내 제자리");
            room.transform.SetPositionAndRotation(pav.transform.position, pav.transform.rotation);

            // 겹치면 z 싸움이 난다. 앉히는 순간 원본은 꺼야 한다.
            Undo.RecordObject(pav, "광풍각 끄기");
            pav.SetActive(false);
            Debug.Log("[조사청] 지은 방을 제자리에 앉히고 원본 광풍각을 껐습니다.");
        }

        /// <summary>꺼져 있어도 찾는다 — GameObject.Find 는 꺼진 것을 못 본다.</summary>
        private static GameObject FindPavilion()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != "소쇄원_정원") continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Gwangpunggak_Pavilion") return t.gameObject;
            }
            return null;
        }

        private const string HouseDir = "Assets/_Project/Onggojip/Art/KimMyeonggwanHouse/Material/";
        private const string RoomDir = "Assets/_Project/Onggojip/Art/사랑채실내/";

        /// <summary>
        /// 재질은 <b>사랑채 실내가 쓰는 것을 그대로</b> 쓴다.
        ///
        /// 처음엔 소쇄원(광풍각) 재질로 지었다. 그런데 그 팩은 언리얼에서 옮겨 온
        /// 셰이더라 그림 슬롯 이름이 Material_Texture2D_0..4 다 — 유니티가 아는
        /// _BaseMap 이 없으니 되풀이 값이 씹히고, 거칠기 그림이 통째로 늘어나 지붕이
        /// 거울처럼 번들거렸다. 같은 그림으로 URP 재질을 새로 만들어 막긴 했지만
        /// 결이 겉돌았다.
        ///
        /// 고택 팩은 처음부터 URP Lit 이고 부재마다 제 재질이 갖춰져 있다 —
        /// 기와·서까래·마루·기단까지. 사랑채가 그것으로 지어졌으니 조사청도 같은 것을
        /// 쓰면 두 방이 한 집처럼 보인다.
        ///
        /// 사건 폴더의 아트를 공통 씬이 참조하게 되는 것은 아는 값이다. 조사청과
        /// 사랑채가 같은 조선 집이어야 한다는 쪽을 택했다.
        /// </summary>
        private static bool LoadMaterials()
        {
            _wood   = Load(HouseDir + "MI_Wood01A.mat");        // 기둥
            _beam   = Load(HouseDir + "MI_Wood02A.mat");        // 도리·보·문틀
            _rafter = Load(HouseDir + "MI_RafterA.mat");        // 서까래
            _floor  = Load(HouseDir + "MI_Floor01A.mat");       // 마루
            _wall   = Load(HouseDir + "MI_WhiteWall01A.mat");   // 회벽
            _gidan  = Load(HouseDir + "MI_GidanStone01A.mat");  // 기단
            _stone  = Load(HouseDir + "MI_Stone02A.mat");       // 주춧돌·댓돌
            _roof   = Load(HouseDir + "MI_Giwa.mat");           // 기와
            _paper  = Load(RoomDir + "MI_사랑방_한지.mat");       // 문·창에 바른 한지
            _ceil   = Load(RoomDir + "MI_사랑방_반자.mat");       // 반자 천장
            _jangpan = Load(RoomDir + "MI_사랑방_장판.mat");      // 방바닥 장판

            // 창에 바른 종이만은 <b>불투명</b>한 것을 쓴다. 문에 바른 사랑방 한지는
            // 투명도 0.62 라, 문짝처럼 뒤에 방이 있을 때는 알맞지만 창처럼 뒤가
            // 바깥일 때는 나뭇가지가 그대로 비쳐 유리창이 된다. 창호지는 빛만
            // 들이고 모양은 안 들이는 물건이다.
            _changho = ChanghoPaper();

            if (_wood == null || _wall == null || _floor == null || _gidan == null || _stone == null)
            {
                Debug.LogError("[조사청] 고택 재질을 못 찾았습니다: " + HouseDir);
                return false;
            }
            if (_beam == null) _beam = _wood;
            if (_rafter == null) _rafter = _beam;
            if (_roof == null) _roof = _gidan;
            if (_paper == null) _paper = _wall;
            if (_ceil == null) _ceil = _wall;
            if (_jangpan == null) _jangpan = _floor;
            if (_changho == null) _changho = _paper;
            return true;
        }

        private const string ChanghoPath = "Assets/_Project/_Common/Materials/M_조사청_창호지.mat";

        /// <summary>
        /// 창에 바를 종이. 없으면 만든다.
        ///
        /// <b>왜 새로 만드나</b>: 처음엔 M_조사청_한지 를 물렸는데, 그것이 문 그림
        /// <b>아틀라스</b>(T_GPG_Door01a_BC)를 쓴다. 한 장에 나뭇결·회색 바닥·종이가
        /// 다 들어 있는 그림이라, 창호지 판 하나에 통째로 늘여 바르면 종이 자리에
        /// 나뭇결과 회색 바닥까지 같이 찍힌다 — 창이 누더기가 된 까닭이 이것이다.
        /// 아틀라스에서 종이 조각만 오려 쓰려면 UV 를 손으로 맞춰야 하는데, 창호지는
        /// 본디 무늬가 없는 물건이라 그럴 값어치가 없다. 빛깔 하나로 족하다.
        /// </summary>
        private static Material ChanghoPaper()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(ChanghoPath);
            if (m != null) return m;

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return null;
            m = new Material(sh) { name = "M_조사청_창호지" };
            m.SetColor("_BaseColor", new Color(0.90f, 0.86f, 0.76f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.06f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, ChanghoPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[조사청] 창호지 재질을 새로 만들었습니다 — " + ChanghoPath);
            return m;
        }

        private static Material Load(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) Debug.LogWarning("[조사청] 재질 없음: " + path);
            return m;
        }

        // ── 부재 하나 ────────────────────────────────

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>
        /// 상자 하나. 자리는 <b>가운데</b>, 크기는 실제 치수(m)로 준다.
        /// tile 은 1m 에 무늬를 몇 번 되풀이할지 — 0이면 늘린 그대로 둔다.
        /// </summary>
        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size,
                                      Material mat, bool collide = false, Quaternion? rot = null,
                                      float tile = 0.5f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = size;

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (tile > 0f)
            {
                // 큐브는 여섯 면이 모두 UV 0~1 이라 한 벌의 되풀이 값밖에 줄 수 없다.
                // 가장 넓은 면(큰 변 둘)에 맞추면 나머지 면도 얼추 같은 낱장 크기가 된다.
                float a = Mathf.Max(size.x, size.z), b = Mathf.Max(size.y, Mathf.Min(size.x, size.z));
                var st = new Vector4(Mathf.Max(1f, a * tile), Mathf.Max(1f, b * tile), 0f, 0f);
                var mpb = new MaterialPropertyBlock();
                mpb.SetVector(BaseMapSt, st);
                mpb.SetVector(MainTexSt, st);
                r.SetPropertyBlock(mpb);
            }
            if (!collide) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }

        // ── 짓기 ────────────────────────────────────

        private static void BuildGidan(Transform g)
        {
            // 기단은 집보다 한 자쯤 넓게 나온다. 그 턱이 있어야 집이 땅에 얹힌 것으로 보인다.
            float cx = (XMin + XMax) * 0.5f, cz = (ZMin + ZMax) * 0.5f;
            float w = (XMax - XMin) + 1.4f, d = (ZMax - ZMin) + 1.4f;
            Box(g, "기단_단", new Vector3(cx, GidanTop - 0.6f, cz), new Vector3(w, 1.2f, d), _gidan, true, null, 0.8f);

            // 남쪽 가운데 디딤돌 — 마당에서 마루로 오르는 자리
            Box(g, "디딤돌_남", new Vector3(cx, GidanTop + 0.09f, ZMin - 1.05f),
                new Vector3(1.2f, 0.18f, 0.7f), _stone, true, null, 1f);
        }

        private static void BuildStones(Transform g)
        {
            foreach (var p in PillarSpots())
                Box(g, $"주춧돌_{p.x:F1}_{p.z:F1}",
                    new Vector3(p.x, (GidanTop + StoneTop) * 0.5f, p.z),
                    new Vector3(0.5f, StoneTop - GidanTop, 0.5f), _stone, false, null, 1f);
        }

        private static void BuildPillars(Transform g)
        {
            float h = BeamBottom - StoneTop;
            foreach (var p in PillarSpots())
                Box(g, $"기둥_{p.x:F1}_{p.z:F1}",
                    new Vector3(p.x, StoneTop + h * 0.5f, p.z),
                    new Vector3(PillarW, h, PillarW), _wood, true, null, 0.6f);
        }

        /// <summary>기둥이 서는 자리 — 격자의 가장자리와, 방을 가르는 줄.</summary>
        private static System.Collections.Generic.List<Vector3> PillarSpots()
        {
            var list = new System.Collections.Generic.List<Vector3>();
            int nx = Mathf.RoundToInt((XMax - XMin) / Bay) + 1;
            int nz = Mathf.RoundToInt((ZMax - ZMin) / Bay) + 1;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = XMin + Bay * i, z = ZMin + Bay * j;
                    bool edge = i == 0 || j == 0 || i == nx - 1 || j == nz - 1;
                    // 방을 가르는 줄에도 기둥이 선다 — 벽이 허공에 붙으면 안 된다.
                    bool roomLine = Mathf.Abs(z - RoomZMin) < 0.01f || Mathf.Abs(z - RoomZMax) < 0.01f
                                    || Mathf.Abs(x - WestX1) < 0.01f || Mathf.Abs(x - MidX1) < 0.01f;
                    if (edge || roomLine) list.Add(new Vector3(x, 0f, z));
                }
            return list;
        }

        private static void BuildFloor(Transform g)
        {
            // 바닥은 한 장이 아니다. <b>방은 장판, 열린 마루는 널</b>이다 —
            // 신을 벗고 앉는 자리와 신은 채로 걷는 자리가 같은 재질이면 어디까지가
            // 방인지 알 수 없다. 사랑채 실내가 쓰는 장판을 그대로 쓴다.
            float cx = (XMin + XMax) * 0.5f, cz = (ZMin + ZMax) * 0.5f;
            Box(g, "마루_널", new Vector3(cx, FloorTop - FloorThick * 0.5f, cz),
                new Vector3(XMax - XMin, FloorThick, ZMax - ZMin), _floor, true, null, 0.7f);

            // 장판은 널 위에 종이 한 겹으로 덮는다(방 두 칸에만).
            float y = FloorTop + 0.004f;
            Box(g, "장판_서방", Mid(WestX0, WestX1, y, RoomZMin, RoomZMax),
                new Vector3(WestX1 - WestX0, 0.008f, RoomZMax - RoomZMin), _jangpan, false, null, 0.35f);
            Box(g, "장판_중방", Mid(MidX0, MidX1, y, RoomZMin, RoomZMax),
                new Vector3(MidX1 - MidX0, 0.008f, RoomZMax - RoomZMin), _jangpan, false, null, 0.35f);
        }

        private static void BuildBeams(Transform g)
        {
            float y = BeamBottom + BeamThick * 0.5f;
            int nx = Mathf.RoundToInt((XMax - XMin) / Bay) + 1;
            int nz = Mathf.RoundToInt((ZMax - ZMin) / Bay) + 1;
            float cx = (XMin + XMax) * 0.5f, cz = (ZMin + ZMax) * 0.5f;

            for (int j = 0; j < nz; j++)   // 도리 — 정면과 나란히
                Box(g, $"도리_z{j}", new Vector3(cx, y, ZMin + Bay * j),
                    new Vector3(XMax - XMin + PillarW, BeamThick, 0.20f), _beam, false, null, 0.6f);

            for (int i = 0; i < nx; i++)   // 보 — 앞뒤로 건너지른다
                Box(g, $"보_x{i}", new Vector3(XMin + Bay * i, y, cz),
                    new Vector3(0.20f, BeamThick, ZMax - ZMin), _beam, false, null, 0.6f);
        }

        private static void BuildRafters(Transform g)
        {
            // 서까래는 처마 쪽으로 조금 빠져나온다. 끝이 벽에서 딱 끊기면 지붕이 없어 보인다.
            float len = (ZMax - ZMin) + 1.6f;
            float cz = (ZMin + ZMax) * 0.5f;
            int n = Mathf.FloorToInt((XMax - XMin) / RafterStep);
            for (int i = 0; i <= n; i++)
                Box(g, $"서까래_{i}", new Vector3(XMin + RafterStep * i, RafterY, cz),
                    new Vector3(0.11f, 0.11f, len), _rafter, false, null, 0.6f);
        }

        private static void BuildCeiling(Transform g)
        {
            // 반자는 방 위에만 있다. 열린 마루 위는 서까래가 그대로 보이는 자리다(연등천장).
            Box(g, "반자_서", Mid(WestX0, WestX1, CeilY, RoomZMin, RoomZMax),
                new Vector3(WestX1 - WestX0, 0.08f, RoomZMax - RoomZMin), _ceil, false, null, 0.5f);
            Box(g, "반자_중", Mid(MidX0, MidX1, CeilY, RoomZMin, RoomZMax),
                new Vector3(MidX1 - MidX0, 0.08f, RoomZMax - RoomZMin), _ceil, false, null, 0.5f);
        }

        private static Vector3 Mid(float x0, float x1, float y, float z0, float z1)
            => new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f);

        private static void BuildWalls(Transform g)
        {
            float h = BeamBottom - FloorTop;
            float y = FloorTop + h * 0.5f;

            // 북쪽만 막힌 벽이다 — 사건판이 걸리는 면이고, 뒤가 트여 있으면 걸 데가 없다.
            Box(g, "벽_북_서방", new Vector3((WestX0 + WestX1) * 0.5f, y, RoomZMax),
                new Vector3(WestX1 - WestX0, h, WallThick), _wall, true, null, 0.5f);
            Box(g, "벽_북_중방", new Vector3((MidX0 + MidX1) * 0.5f, y, RoomZMax),
                new Vector3(MidX1 - MidX0, h, WallThick), _wall, true, null, 0.5f);

            // 방과 방 사이 칸막이는 <b>세우지 않는다</b>. 원본 광풍각에 있어서 처음엔
            // 따라 세웠는데, 조사청은 한 사람이 한 자리에서 다 보는 방이라 가운데를
            // 막으면 사건판과 보료가 서로 안 보인다. 손으로 걷어낸 것을 도구가 다시
            // 세우는 일이 없도록 여기서 아예 뺀다.

            // 문 위로 남는 자리(인방 위 벽). 문이 천장까지 닿으면 한옥이 아니라 유리문이 된다.
            float lintel = BeamBottom - DoorTop;
            float ly = DoorTop + lintel * 0.5f;
            Box(g, "인방위_서_남", new Vector3((WestX0 + WestX1) * 0.5f, ly, RoomZMin),
                new Vector3(WestX1 - WestX0, lintel, WallThick), _wall, false, null, 0.5f);
            Box(g, "인방위_중_남", new Vector3((MidX0 + MidX1) * 0.5f, ly, RoomZMin),
                new Vector3(MidX1 - MidX0, lintel, WallThick), _wall, false, null, 0.5f);
            // 옆면(서·동)의 위쪽 벽은 창이 제 몫으로 세운다 — 창머리 높이가
            // 문머리와 다르므로 여기서 같이 재면 어긋난다.
        }

        /// <summary>
        /// 남쪽 두 면에 <b>분합문</b>을 단다 — 한 칸(1.5m)에 두 짝씩, 한 면에 넉 짝.
        ///
        /// <b>왜 다시 짰나</b>: 처음엔 한 면을 종이 한 장으로 발라 두고 설주로 넉 짝처럼
        /// 금만 그어 두었다. 보기에는 문인데 <b>열리지가 않는다</b>. 조사청은 사건을
        /// 고르는 방이지 갇히는 방이 아니고, 문을 열고 마당에 나서서 하늘을 보는 것이
        /// 이 방이 바깥을 가진 까닭이다. 그러자면 문짝이 진짜로 돌아야 한다.
        ///
        /// <b>지도리는 칸의 양 끝</b>에 있고 두 짝이 가운데서 만난다(창과 같다).
        /// 밖으로 밀어 여는 여닫이라, 열면 마당 쪽으로 활짝 젖혀진다.
        /// 짝마다 제 틀과 제 청판과 제 살을 지닌다 — 한 장으로 발라 놓고 나중에
        /// 가르면 살이 짝 경계에서 끊겨 열 때마다 찢긴 무늬가 드러난다.
        ///
        /// 여닫는 일은 <see cref="DoorController"/> 가 맡는다. 그 부품이 이미
        /// 여닫이·미닫이를 다 알고 클릭도 받으므로, 여기서는 짝만 제자리에 세우고
        /// 지도리 목록을 넘겨 준다.
        /// </summary>
        private static void BuildDoors(Transform g)
        {
            var a = Swing(g, "문_서방_남", WestX0, WestX1);
            var b = Swing(g, "문_중방_남", MidX0, MidX1);

            // 댓돌은 <b>두 면을 합쳐 한 번만</b> 놓는다. 면마다 놓으면 열리는 짝이
            // 서로 맞닿아 있을 때(지금이 그렇다) 계단이 두 벌 겹쳐 선다.
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.y, b.y);
            if (x1 > x0) Steps(g, x0, x1);
            else Debug.LogWarning("[조사청] 열리는 짝이 하나도 없어 댓돌을 못 놓았습니다.");
        }

        /// <summary>문 아래 청판(널)의 높이. 발이 닿고 치맛자락이 스치는 자리다.</summary>
        private const float PanelH = 0.40f;

        /// <summary>열었을 때 짝이 젖혀지는 각(도). 사람이 지나갈 만큼은 열려야 한다.</summary>
        private const float DoorOpen = 78f;

        /// <summary>한 면 — 붙박이 문틀과 여닫이 짝 넷. 열리는 짝들이 걸친 x 구간을 돌려준다.</summary>
        private static Vector2 Swing(Transform g, string name, float a0, float a1)
        {
            float h = DoorTop - FloorTop;
            float y0 = FloorTop, y1 = FloorTop + h, yc = FloorTop + h * 0.5f;
            float span = a1 - a0, mid = (a0 + a1) * 0.5f;
            const float outward = -1f;          // 남쪽 = -Z 가 바깥이다

            var group = Group(g, name);

            // ── 붙박이 문틀 ──
            // 설주는 <b>칸 경계에만</b> 세운다. 여태처럼 0.75m 마다 세우면 그 기둥이
            // 짝이 돌아 나갈 길을 막는다 — 문틀이 문을 가두는 꼴이다.
            int pairs = Mathf.Max(1, Mathf.RoundToInt(span / PairW));
            float pw = span / pairs;

            Face(group, name + "_위틀", mid, y1 - FrameW * 0.5f, span, FrameW, 0.12f,
                 RoomZMin, true, _beam, false, 0.6f);
            // 문지방 — 낮게. 턱은 있되 걸려 넘어질 만큼은 아니어야 한다.
            Face(group, name + "_문지방", mid, y0 + 0.03f, span, 0.06f, 0.14f,
                 RoomZMin, true, _beam, false, 0.6f);
            for (int k = 0; k <= pairs; k++)
                Face(group, name + "_설주" + k, a0 + pw * k, yc, FrameW, h, 0.12f,
                     RoomZMin, true, _beam, false, 0.6f);

            // ── 짝 넷 ──
            float lw = pw * 0.5f - FrameW * 0.5f;
            var blocked = new System.Collections.Generic.List<Furniture>();
            GatherFurniture(blocked);
            var blockLog = new System.Collections.Generic.List<string>();
            int locked = 0;
            // 열리는 짝들이 걸쳐 있는 x 구간. 디딤돌은 그 앞에만 놓는다.
            var openSpan = new Vector2(float.MaxValue, -float.MaxValue);

            int idx = 0;
            for (int p = 0; p < pairs; p++)
                for (int side = 0; side < 2; side++, idx++)
                {
                    float hinge = a0 + pw * p + (side == 0 ? 0f : pw);
                    float dir = side == 0 ? 1f : -1f;

                    var leaf = new GameObject("짝_" + idx);
                    leaf.transform.SetParent(group, false);
                    leaf.transform.localPosition = new Vector3(hinge, yc, RoomZMin);

                    DoorLeaf(leaf.transform, dir, lw, h, outward);

                    // 앞이 막힌 짝인가 — 병풍이 붙어 섰거나 문갑이 등지고 있으면 잠근다.
                    string by = Blocked(blocked, hinge, dir, lw, y0, y1);
                    bool shut = by != null;
                    if (shut) { leaf.name += "_막힘"; locked++; blockLog.Add(leaf.name + "  ←  " + by); }
                    else
                    {
                        openSpan.x = Mathf.Min(openSpan.x, Mathf.Min(hinge, hinge + dir * lw));
                        openSpan.y = Mathf.Max(openSpan.y, Mathf.Max(hinge, hinge + dir * lw));
                    }

                    // 자유단이 바깥(-Z)으로 나가려면 회전 부호가 dir 을 따라가야 한다.
                    // 부호를 뒤집으면 문이 방 안쪽으로 열려 세간을 뚫는다.
                    LeafController(leaf.transform, dir * DoorOpen, !shut);
                }

            // 어느 짝이 무엇에 막혔는지 적어 둔다. 잠긴 까닭이 안 보이면 잘못 잠근 것을
            // 알아챌 길이 없다 — 세간을 옮기고도 문이 안 열리면 문이 고장 난 줄 안다.
            Debug.Log("[조사청] " + name + " — 열 수 있는 짝 " + (4 - locked) + " · 잠근 것 " + locked
                      + (blockLog.Count > 0 ? "\n     " + string.Join("\n     ", blockLog.ToArray()) : ""));

            return openSpan;
        }

        /// <summary>
        /// 방 안에 놓인 세간을 <b>방 기준 좌표</b>로 모은다.
        ///
        /// 월드 AABB 로 견주면 방이 140도 돌아앉아 있어서 상자가 실제보다 훨씬 크게
        /// 잡힌다 — 방 한가운데 놓인 서안이 문짝까지 닿은 것으로 나온다. 여덟 귀퉁이를
        /// 방 좌표로 옮겨 다시 재야 맞다.
        /// </summary>
        /// <summary>마당 바닥(방 기준 y). 기단이 앉는 높이 136.95 에서 방 원점 137.73 을 뺀 것이다.</summary>
        private const float YardY = -0.78f;

        /// <summary>
        /// 열리는 문 앞의 <b>댓돌</b>. 마루는 마당에서 1.5m 위다.
        ///
        /// 문만 열어 놓고 끝냈더니 드나드는 일이 이상해졌다 — 마당에서는 못 오르고,
        /// 방에서는 낭떠러지로 내려선다. 조선 집이 이 높이를 다루는 방식이 댓돌이다:
        /// 기단 앞에 넓적한 돌을 두어 단 놓아 마당과 마루를 잇는다. 계단이라기보다
        /// <b>신 벗는 자리</b>여서, 한 단이 넓고 낮다.
        ///
        /// 단은 <b>위로 갈수록 좁아진다</b>. 아래가 넓어야 마당에서 발을 얹기 쉽고,
        /// 위가 좁아야 마루 끝에 걸터앉는 자리가 남는다.
        /// </summary>
        private static void Steps(Transform g, float x0, float x1)
        {
            var group = Group(g, "댓돌");
            float mid = (x0 + x1) * 0.5f;
            float width = (x1 - x0) + 0.6f;          // 문보다 조금 넓게 — 문틀에 발이 안 걸리게

            // 높이를 헤아려 보면 이렇다(방 기준):
            //   마당 -0.78 · 기단 윗면 0.25 · 마루 0.71
            // 마당에서 기단까지 1.03m 를 한 번에 오를 수는 없다. 그래서 기단 앞에
            // 두 단을 놓아 세 걸음으로 나눈다 — 0.36 · 0.33 · 0.34.
            // 기단에서 마루로 오르는 0.46m 는 한 걸음으로 둔다. 문지방을 넘어서는
            // 자리라 오히려 턱이 있어야 <b>들어선다</b>는 느낌이 난다.
            float front = ZMin - 0.70f;              // 기단 앞면
            float[] tops = { -0.42f, -0.09f };
            float[] depth = { 0.72f, 0.58f };

            float outer = front;
            for (int i = tops.Length - 1; i >= 0; i--)   // 위 단부터 기단에 붙여 나간다
            {
                float top = tops[i];
                float bottom = (i == 0) ? YardY - 0.25f : tops[i - 1];
                float h = top - bottom;
                float z = outer - depth[i] * 0.5f;
                outer -= depth[i];

                Box(group, "댓돌_" + i,
                    new Vector3(mid, bottom + h * 0.5f, z),
                    new Vector3(width - i * 0.14f, h, depth[i]),
                    _stone, true, null, 0.5f);
            }

            // 옛 디딤돌 하나를 걷는다.
            //
            // BuildGidan 이 남쪽 한가운데(x -0.04)에 넓적한 돌을 하나 놓아 두었는데,
            // 윗면이 0.43 이라 <b>기단(0.25)보다 높다</b>. 오르려고 밟으면 도로 내려서야
            // 하고, 무엇보다 문이 열리는 자리는 거기가 아니다. 열리는 짝 앞에 제대로
            // 놓았으므로 그것은 없어도 된다.
            var gidan = g.parent != null ? g.parent.Find("기단") : null;
            var oldStone = gidan != null ? gidan.Find("디딤돌_남") : null;
            if (oldStone != null) Undo.DestroyObjectImmediate(oldStone.gameObject);
        }

        /// <summary>세간 하나 — 방 기준 상자와 그 이름. 무엇이 막았는지 적어 주려고 이름을 같이 든다.</summary>
        private class Furniture { public Bounds box; public string name; }

        private static void GatherFurniture(System.Collections.Generic.List<Furniture> into)
        {
            var room = GameObject.Find(RootName);
            if (room == null) return;
            var inv = room.transform.worldToLocalMatrix;

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // 방 자신과, 카메라에 매달린 것(손에 든 도구)은 세간이 아니다.
                if (r.transform.IsChildOf(room.transform)) continue;
                var cam = Camera.main;
                if (cam != null && r.transform.IsChildOf(cam.transform.root)) continue;

                // 물건이 <b>제 축으로</b> 차지한 상자를 가져다 방 좌표로 옮긴다.
                //
                // 처음엔 Renderer.bounds(월드 축 상자)의 여덟 귀퉁이를 옮겼다. 그런데
                // 방이 140도 돌아앉아 있으므로, 방과 나란히 놓인 물건일수록 월드 상자가
                // 이미 부풀어 있고 그것을 <b>다시</b> 돌려 상자를 뜨면 한 번 더 부푼다.
                // 2.2×2.3m 짜리 보료가 3.13×3.13 이 되어 서방 문짝 넉 장을 다 덮었다.
                // 메시가 제 좌표에서 차지한 상자를 물건의 행렬로 옮기면 부풀지 않는다.
                var mf = r.GetComponent<MeshFilter>();
                Bounds src = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.bounds : r.bounds;
                Matrix4x4 m = (mf != null && mf.sharedMesh != null)
                            ? inv * r.transform.localToWorldMatrix
                            : inv;

                var c = src.center; var e = src.extents;
                var local = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(c.x + ((i & 1) == 0 ? -e.x : e.x),
                                             c.y + ((i & 2) == 0 ? -e.y : e.y),
                                             c.z + ((i & 4) == 0 ? -e.z : e.z));
                    local.Encapsulate(m.MultiplyPoint3x4(corner));
                }
                if (Mathf.Abs(local.center.x) > 8f || Mathf.Abs(local.center.z) > 6f) continue;

                // 방보다 큰 것은 세간이 아니다.
                //
                // 안개 고리(반지름 44·55m)와 들판 판때기가 여기 걸려 있었다. 가운데가
                // 조사청이니 <b>중심은 방 안</b>이고, 상자는 방을 통째로 삼킨다. 그래서
                // 문짝 여덟이 남김없이 "앞이 막혔다"로 잠겼다. 크기로 거른다 — 방 한 칸이
                // 3m 인데 6m 를 넘는 세간은 없다.
                var s = local.size;
                if (s.x > 6f || s.z > 6f || s.y > 6f) continue;

                string path = r.name;
                var up = r.transform.parent;
                while (up != null && up.parent != null) { path = up.name + "/" + path; up = up.parent; }
                into.Add(new Furniture { box = local, name = path });
            }
        }

        /// <summary>문 안쪽으로 이만큼까지에 세간이 있으면 그 짝은 못 연다(m).</summary>
        private const float BlockDepth = 0.80f;

        /// <summary>
        /// 이 짝 앞이 막혔는가. 문 <b>안쪽</b>으로 <see cref="BlockDepth"/> m 짜리 상자를
        /// 하나 세우고 세간과 겹치는지만 본다.
        ///
        /// 문은 바깥으로 열리므로 열리는 길 자체는 마당이라 늘 비어 있다. 막히는 것은
        /// <b>다가서는 길</b>이다. 병풍이 문에 붙어 서 있거나 문갑이 등지고 있으면
        /// 사람이 그 짝에 손을 댈 수가 없다 — 그런 짝이 열리면 세간을 뚫고 바깥이 보인다.
        /// </summary>
        private static string Blocked(System.Collections.Generic.List<Furniture> furniture,
                                      float hinge, float dir, float lw, float y0, float y1)
        {
            // 여유는 아주 조금만 준다. 0.12m 를 주었더니 문갑 모서리에서 4cm 가 겹쳐
            // 멀쩡히 드나들 수 있는 짝까지 잠겼다. 한 짝이 0.72m 인데 0.12 는 육분의 일이다.
            float x0 = Mathf.Min(hinge, hinge + dir * lw) - 0.04f;
            float x1 = Mathf.Max(hinge, hinge + dir * lw) + 0.04f;
            float z0 = RoomZMin - 0.06f, z1 = RoomZMin + BlockDepth;

            foreach (var f in furniture)
            {
                var b = f.box;
                if (b.max.x < x0 || b.min.x > x1) continue;
                if (b.max.z < z0 || b.min.z > z1) continue;
                if (b.max.y < y0 + 0.10f || b.min.y > y1) continue;
                return f.name;
            }
            return null;
        }

        /// <summary>
        /// 짝 하나에 여닫는 부품을 물린다 — <b>짝마다 하나씩</b>이다.
        ///
        /// 처음엔 한 면(넉 짝)에 부품 하나를 물렸다. 그랬더니 짝 하나를 밀었는데
        /// 넉 짝이 한꺼번에 활짝 열렸다. 사람이 미는 문은 <b>민 짝만</b> 열린다.
        /// 부품을 짝마다 두면 그 일이 저절로 된다 — 어느 짝을 눌렀는지 따로 알아낼
        /// 것도 없이, 눌린 짝의 부품이 제 짝만 돌린다.
        ///
        /// 사유 항목이라 <see cref="SerializedObject"/> 로 채운다 — 인스펙터에서 손으로
        /// 끌어다 넣는 것과 같은 일을 코드로 하는 것이다.
        /// </summary>
        private static void LeafController(Transform leaf, float angle, bool canOpen)
        {
            var dc = leaf.gameObject.AddComponent<DoorController>();
            var so = new SerializedObject(dc);

            var arr = so.FindProperty("_leaves");
            arr.arraySize = 1;
            var e = arr.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("pivot").objectReferenceValue = leaf;
            e.FindPropertyRelative("swingAngle").floatValue = angle;
            e.FindPropertyRelative("slideOffset").vector3Value = Vector3.zero;

            so.FindProperty("_motion").enumValueIndex = 0;      // 0 = Swing
            so.FindProperty("_openDuration").floatValue = 0.9f;
            so.FindProperty("_startOpen").boolValue = false;
            so.FindProperty("_playerCanToggle").boolValue = canOpen;
            so.FindProperty("_locked").boolValue = false;
            // 문 앞까지 와야 손이 닿는다. 방 건너에서 눌러 열리면 손이 아니라 마술이다.
            so.FindProperty("_maxTouchDistance").floatValue = 2.4f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 짝 한 장. 지도리를 원점으로 x 방향 <paramref name="dir"/> 로 lw 만큼 뻗는다.
        /// 아랫도리는 널(청판)이고 그 위만 살을 짜 종이를 바른다.
        /// </summary>
        private static void DoorLeaf(Transform g, float dir, float lw, float h, float outward)
        {
            float mid = dir * lw * 0.5f;
            float y0 = -h * 0.5f, y1 = h * 0.5f;
            float paperY0 = y0 + PanelH;                 // 종이가 시작하는 높이
            float ph = y1 - paperY0;

            // 청판 — 발치의 널. 클릭을 받아야 하므로 몸을 가진다.
            Box(g, "청판", new Vector3(mid, y0 + PanelH * 0.5f, 0f),
                new Vector3(lw, PanelH, 0.055f), _beam, true, null, 0.7f);

            // 짝틀 넷
            Box(g, "틀_위", new Vector3(mid, y1 - FrameW * 0.5f, 0f),
                new Vector3(lw, FrameW, 0.09f), _beam, false, null, 0.6f);
            Box(g, "틀_허리", new Vector3(mid, paperY0 + FrameW * 0.5f, 0f),
                new Vector3(lw, FrameW, 0.09f), _beam, false, null, 0.6f);
            Box(g, "틀_지도리", new Vector3(dir * FrameW * 0.5f, 0f, 0f),
                new Vector3(FrameW, h, 0.09f), _beam, false, null, 0.6f);
            Box(g, "틀_자유단", new Vector3(dir * (lw - FrameW * 0.5f), 0f, 0f),
                new Vector3(FrameW, h, 0.09f), _beam, false, null, 0.6f);

            // 한지 — 살보다 <b>바깥쪽</b> 한 겹. 조선 창호는 밖에서 바르므로
            // 방 안에서 보면 살이 도드라지고 밖에서 보면 종이만 희다.
            Box(g, "한지", new Vector3(mid, paperY0 + ph * 0.5f, outward * 0.020f),
                new Vector3(lw - FrameW, ph - FrameW, 0.012f), _paper, true, null, 1f);

            // 살 — 창과 같은 숫대살
            float s0 = Mathf.Min(0f, dir * lw) + FrameW;
            float s1 = Mathf.Max(0f, dir * lw) - FrameW;
            Sal(Group(g, "살"), 0f, s0, s1, paperY0 + FrameW, y1 - FrameW, false);
        }


        /// <summary>
        /// 벽면 하나에 얹는 판. u 는 면을 따라간 자리, v 는 높이, t 는 두께다.
        /// alongX 면 면이 z 에 서서 x 로 뻗고, 아니면 x 에 서서 z 로 뻗는다.
        /// </summary>
        private static void Face(Transform g, string name, float u, float v, float du, float dv,
                                 float t, float fixedCoord, bool alongX, Material mat,
                                 bool collide, float tile)
        {
            var pos = alongX ? new Vector3(u, v, fixedCoord) : new Vector3(fixedCoord, v, u);
            var size = alongX ? new Vector3(du, dv, t) : new Vector3(t, dv, du);
            Box(g, name, pos, size, mat, collide, null, tile);
        }

        // ── 창 ──────────────────────────────────────

        /// <summary>
        /// 옆면 둘에 창을 단다 — 서방의 서쪽, 중방의 동쪽.
        ///
        /// 한 면은 다섯 켜로 쌓인다: 머름벽 · 아래틀 · 창(살+한지) · 위틀 · 창머리 위 벽.
        /// 살은 세로로 촘촘히 세우고 가로로 세 줄 지른다(세살창). 세로살만 세우면
        /// 발처럼 보이고, 격자로 짜면 왜식 장지문이 된다.
        ///
        /// 한지는 살보다 <b>바깥쪽</b>에 바른다. 조선 창호는 밖에서 바르므로 안에서
        /// 보면 살이 도드라지고 밖에서 보면 종이만 희다 — 방 안에 앉았을 때
        /// 창살 그림자가 지는 것이 그 때문이다.
        /// </summary>
        private static void BuildWindows(Transform g)
        {
            Window(Group(g, "창_서방_서"), WestX0, RoomZMin, RoomZMax, -1f);
            Window(Group(g, "창_중방_동"), MidX1, RoomZMin, RoomZMax, +1f);
        }

        /// <summary>한 면. fx 는 창이 선 x, z0~z1 은 면의 길이, outward 는 바깥쪽(-1/+1).</summary>
        private static void Window(Transform g, float fx, float z0, float z1, float outward)
        {
            float span = z1 - z0, cz = (z0 + z1) * 0.5f;
            float sillTop = FloorTop + SillH;             // 머름 윗면
            float winTop = sillTop + WinH;                // 창 윗면

            // ① 머름벽 — 앉은 사람의 등 뒤를 막아 주는 낮은 벽
            Box(g, "머름", new Vector3(fx, FloorTop + SillH * 0.5f, cz),
                new Vector3(WallThick, SillH, span), _wall, true, null, 0.5f);
            Box(g, "머름대", new Vector3(fx, sillTop + 0.03f, cz),
                new Vector3(WallThick + 0.05f, 0.06f, span), _beam, false, null, 0.6f);

            // ② 창머리 위 벽 — 창이 도리까지 닿으면 벽이 없는 집이 된다
            float upper = BeamBottom - winTop;
            if (upper > 0.02f)
                Box(g, "창머리위벽", new Vector3(fx, winTop + upper * 0.5f, cz),
                    new Vector3(WallThick, upper, span), _wall, false, null, 0.5f);

            // ③ 붙박이 틀 — 아래·위 가로틀과, 짝 둘씩 나누는 세로 설주
            Box(g, "아래틀", new Vector3(fx, sillTop + FrameW * 0.5f, cz),
                new Vector3(WallThick, FrameW, span), _beam, false, null, 0.6f);
            Box(g, "위틀", new Vector3(fx, winTop - FrameW * 0.5f, cz),
                new Vector3(WallThick, FrameW, span), _beam, false, null, 0.6f);

            int pairs = Mathf.Max(1, Mathf.RoundToInt(span / PairW));
            float pw = span / pairs;
            for (int k = 0; k <= pairs; k++)
                Box(g, "설주_" + k, new Vector3(fx, (sillTop + winTop) * 0.5f, z0 + pw * k),
                    new Vector3(WallThick, WinH, FrameW), _beam, false, null, 0.6f);

            // ④ 짝 — 한 칸에 둘, 바깥으로 여닫는다. 하나는 조금 열어 둔다.
            BuildLeaves(g, fx, z0, pw, pairs, sillTop, winTop, outward);
        }

        /// <summary>
        /// 창짝을 단다. 한 칸(1.5m)에 두 짝이고, 지도리는 <b>칸의 양 끝</b>에 있어
        /// 두 짝이 가운데서 만난다. 밖으로 밀어 여는 여닫이다.
        ///
        /// <b>하나는 조금 열어 둔다</b>. 창이 넉 짝 다 닫혀 있으면 종이를 바른 벽과
        /// 다를 것이 없다 — 열리는 물건이라는 것은 열려 있는 것을 한 번 보여야 안다.
        /// 열린 틈으로 바깥이 들어오면 방이 상자가 아니라 집 속의 한 칸이 된다.
        ///
        /// 짝은 <b>지도리 자리에 빈 오브젝트를 세우고 그 밑에</b> 짠다. 그래야 여는
        /// 각을 회전 하나로 주고, 나중에 손으로 여닫게 할 때도 그대로 쓴다.
        /// </summary>
        private static void BuildLeaves(Transform g, float fx, float z0, float pw, int pairs,
                                        float sillTop, float winTop, float outward)
        {
            float yc = (sillTop + winTop) * 0.5f;
            float lh = WinH - FrameW * 2f;      // 짝 높이(위아래 틀 안쪽)
            float lw = pw * 0.5f - FrameW * 0.5f;

            int idx = 0;
            for (int p = 0; p < pairs; p++)
                for (int side = 0; side < 2; side++, idx++)
                {
                    // 지도리는 칸의 양 끝. 자유단은 가운데를 향한다.
                    float hinge = z0 + pw * p + (side == 0 ? 0f : pw);
                    float dir = side == 0 ? 1f : -1f;

                    var leaf = new GameObject("짝_" + idx);
                    leaf.transform.SetParent(g, false);
                    leaf.transform.localPosition = new Vector3(fx, yc, hinge);
                    float open = (idx == OpenIdx) ? OpenAngle : 0f;
                    leaf.transform.localRotation = Quaternion.Euler(0f, outward * dir * open, 0f);

                    Leaf(leaf.transform, dir, lw, lh, outward);
                }
        }

        /// <summary>짝 한 장 — 지도리를 원점으로 z 방향 dir 로 lw 만큼 뻗는다.</summary>
        private static void Leaf(Transform g, float dir, float lw, float lh, float outward)
        {
            float mid = dir * lw * 0.5f;

            // 짝틀 넷
            Box(g, "틀_위", new Vector3(0f, lh * 0.5f - FrameW * 0.5f, mid),
                new Vector3(BarT * 1.6f, FrameW, lw), _beam, false, null, 0.6f);
            Box(g, "틀_아래", new Vector3(0f, -lh * 0.5f + FrameW * 0.5f, mid),
                new Vector3(BarT * 1.6f, FrameW, lw), _beam, false, null, 0.6f);
            Box(g, "틀_지도리", new Vector3(0f, 0f, dir * FrameW * 0.5f),
                new Vector3(BarT * 1.6f, lh, FrameW), _beam, false, null, 0.6f);
            Box(g, "틀_자유단", new Vector3(0f, 0f, dir * (lw - FrameW * 0.5f)),
                new Vector3(BarT * 1.6f, lh, FrameW), _beam, false, null, 0.6f);

            // 한지 — 살보다 바깥쪽 한 겹
            Box(g, "창호지", new Vector3(outward * 0.022f, 0f, mid),
                new Vector3(0.012f, lh - FrameW, lw - FrameW), _changho, false, null, 1f);

            // 살 — 숫대살
            float a0 = Mathf.Min(0f, dir * lw) + FrameW;
            float a1 = Mathf.Max(0f, dir * lw) - FrameW;
            Sal(Group(g, "살"), 0f, a0, a1, -lh * 0.5f + FrameW, lh * 0.5f - FrameW, true);
        }

        private static void Sal(Transform g, float fixedCoord, float a0, float a1,
                                float v0, float v1, bool alongZ)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt((a1 - a0) / SalLeaf));
            float w = (a1 - a0) / n;

            // 살은 <b>금</b>으로 모은다. 판마다 테두리를 두르면 이웃한 판이 맞닿는 자리에
            // 같은 살이 두 번씩 놓여, 한 면에 360대가 섰다(제대로 짜면 서른 남짓이다).
            var cuts = new System.Collections.Generic.List<Vector4>();
            for (int i = 0; i < n; i++)
            {
                float u0 = a0 + w * i, u1 = u0 + w;
                if (i > 0) cuts.Add(new Vector4(v0, v1, u0, 0f));   // 짝과 짝 사이 세로살
                Cut(cuts, u0, v0, u1, v1, 0);
            }

            for (int i = 0; i < cuts.Count; i++)
            {
                var c = cuts[i];
                bool horizontal = c.w > 0.5f;
                float len = c.y - c.x, mid = (c.x + c.y) * 0.5f;
                if (len < 0.015f) continue;

                Vector3 pos, size;
                if (alongZ)
                {
                    pos = horizontal ? new Vector3(fixedCoord, c.z, mid) : new Vector3(fixedCoord, mid, c.z);
                    size = horizontal ? new Vector3(BarT, BarW, len + BarW) : new Vector3(BarT, len + BarW, BarW);
                }
                else
                {
                    pos = horizontal ? new Vector3(mid, c.z, fixedCoord) : new Vector3(c.z, mid, fixedCoord);
                    size = horizontal ? new Vector3(len + BarW, BarW, BarT) : new Vector3(BarW, len + BarW, BarT);
                }
                Box(g, (horizontal ? "가로살_" : "세로살_") + i, pos, size, _beam, false, null, 0f);
            }
        }

        /// <summary>가로살 한 금 — 높이 at 에서 x0..x1 까지.</summary>
        private static Vector4 H(float x0, float x1, float at) { return new Vector4(x0, x1, at, 1f); }

        /// <summary>세로살 한 금 — 자리 at 에서 y0..y1 까지.</summary>
        private static Vector4 V(float y0, float y1, float at) { return new Vector4(y0, y1, at, 0f); }

        /// <summary>
        /// 바람개비 가르기. 네 띠(A 위·B 오른·C 왼·D 아래)가 서로 한 칸씩 물려 돌아가고
        /// 가운데(E)가 남는다. 넷과 가운데를 합치면 원래 네모가 빈틈없이 채워지므로,
        /// 살이 허공에서 끊기는 일이 없다 — 무늬를 손으로 적어 넣으면 반드시 한둘이 어긋난다.
        ///
        /// 여기서는 <b>판이 아니라 금</b>을 낸다. 바람개비의 금 넷과, 띠 안을 잘게 써는
        /// 잔금들이다. 겉은 잘고 안으로 갈수록 성기며 한가운데에 훤한 알 하나가 남는다.
        /// </summary>
        private static void Cut(System.Collections.Generic.List<Vector4> segs,
                                float x0, float y0, float x1, float y1, int depth)
        {
            float w = x1 - x0, h = y1 - y0;
            float band = Mathf.Min(w, h) * (depth == 0 ? 0.15f : 0.24f);
            if (depth > SalDepth || Mathf.Min(w, h) < band * 3.2f || band < 0.03f) return;

            float ax = x1 - band, by = y0 + band, cx = x0 + band, dy = y1 - band;
            float cell = band * (depth == 0 ? 1.7f : 2.2f);

            segs.Add(H(x0, ax, by));    // A 와 가운데 사이
            segs.Add(V(y0, dy, ax));    // B
            segs.Add(V(by, y1, cx));    // C
            segs.Add(H(cx, x1, dy));    // D

            Slice(segs, x0, y0, ax, by, true, cell);    // A 위   — 세로 잔금
            Slice(segs, ax, y0, x1, dy, false, cell);   // B 오른 — 가로 잔금
            Slice(segs, x0, by, cx, y1, false, cell);   // C 왼   — 가로 잔금
            Slice(segs, cx, dy, x1, y1, true, cell);    // D 아래 — 세로 잔금

            Cut(segs, cx, by, ax, dy, depth + 1);
        }

        /// <summary>띠 하나를 길이 방향으로 잘게 썬다. 금만 낸다(양 끝은 이미 있다).</summary>
        private static void Slice(System.Collections.Generic.List<Vector4> segs,
                                  float x0, float y0, float x1, float y1, bool alongX, float cell)
        {
            float len = alongX ? x1 - x0 : y1 - y0;
            int k = Mathf.Max(1, Mathf.RoundToInt(len / Mathf.Max(0.02f, cell)));
            float step = len / k;
            for (int i = 1; i < k; i++)
            {
                if (alongX) segs.Add(V(y0, y1, x0 + step * i));
                else segs.Add(H(x0, x1, y0 + step * i));
            }
        }

        private static void BuildRoof(Transform g)
        {
            // 맞배지붕. 처음엔 우진각으로 네 물매를 다 세웠는데, 상자는 사다리꼴로 못 깎으니
            // 옆 물매가 앞뒤 물매 위로 솟아 바람개비처럼 엇갈렸다. 앞뒤 두 폭만 두고
            // 옆은 박공널로 막으면 엇갈릴 면 자체가 없다.
            float cx = (XMin + XMax) * 0.5f, cz = (ZMin + ZMax) * 0.5f;
            float halfZ = (ZMax - ZMin) * 0.5f;
            float rise = RidgeY - EaveY;
            float y = (RidgeY + EaveY) * 0.5f;

            float runZ = halfZ + Overhang;                       // 용마루에서 처마 끝까지
            float slopeZ = Mathf.Sqrt(runZ * runZ + rise * rise);
            float angZ = Mathf.Atan2(rise, runZ) * Mathf.Rad2Deg;
            float widthX = (XMax - XMin) + Overhang * 2f;

            Box(g, "지붕_북", new Vector3(cx, y, cz + runZ * 0.5f), new Vector3(widthX, RoofThick, slopeZ),
                _roof, false, Quaternion.Euler(angZ, 0f, 0f), 0.9f);
            Box(g, "지붕_남", new Vector3(cx, y, cz - runZ * 0.5f), new Vector3(widthX, RoofThick, slopeZ),
                _roof, false, Quaternion.Euler(angZ, 180f, 0f), 0.9f);

            // 용마루 — 두 폭이 만나는 등성이. 지붕 길이만큼 곧게 간다.
            Box(g, "용마루", new Vector3(cx, RidgeY + 0.10f, cz),
                new Vector3(widthX, 0.28f, 0.55f), _roof, false, null, 1f);

            // 박공널(풍판) — 지붕 양 끝의 세모. 상자로는 세모를 못 만드니 층을 지어 깎는다.
            // 지붕 밖으로 나가면 안 된다. 첫 판에 이것을 판때기로 세워 옆으로 삐져나오게
            // 두었더니 날개가 돋친 꼴이 되었다.
            const int Steps = 8;
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? XMin : XMax;
                var grp = Group(g, side == 0 ? "박공_서" : "박공_동");
                for (int k = 0; k < Steps; k++)
                {
                    float t0 = (float)k / Steps, t1 = (float)(k + 1) / Steps;
                    float yc = EaveY + rise * (t0 + t1) * 0.5f;
                    float depth = 2f * runZ * (1f - (t0 + t1) * 0.5f);
                    if (depth < 0.05f) continue;
                    Box(grp, $"박공_{k}", new Vector3(x, yc, cz),
                        new Vector3(0.14f, rise / Steps + 0.01f, depth), _wall, false, null, 0.6f);
                }
            }
        }
    }
}
