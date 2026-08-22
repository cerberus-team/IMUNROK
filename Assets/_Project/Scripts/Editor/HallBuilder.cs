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

            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

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
            _changho = Load("Assets/_Project/_Common/Materials/M_조사청_한지.mat");

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

            // 방과 방 사이 칸막이
            Box(g, "벽_칸막이", new Vector3(WestX1, y, (RoomZMin + RoomZMax) * 0.5f),
                new Vector3(WallThick, h, RoomZMax - RoomZMin), _wall, true, null, 0.5f);

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

        private static void BuildDoors(Transform g)
        {
            // 한 짝 폭 0.75 — 한 칸(1.5)에 두 짝. 남쪽·서쪽·동쪽 세 면이 문이다.
            float h = DoorTop - FloorTop;
            float y = FloorTop + h * 0.5f;

            // 남쪽 두 면만 문이다. 옆면은 창으로 바뀌었다(BuildWindows).
            Leaves(g, "문_서방_남", WestX0, WestX1, y, h, RoomZMin, true);
            Leaves(g, "문_중방_남", MidX0, MidX1, y, h, RoomZMin, true);
        }

        /// <summary>
        /// 한 면을 문짝으로 채운다. 창과 같은 숫대살을 짜되, <b>발치에는 널을 댄다</b>
        /// (청판). 문은 드나드는 데라 아랫도리에 발이 닿고 치맛자락이 스치는데,
        /// 거기까지 종이를 바르면 한 철을 못 간다.
        ///
        /// alongX 면 x 방향으로, 아니면 z 방향으로 늘어놓는다.
        /// </summary>
        private static void Leaves(Transform g, string name, float a0, float a1,
                                   float y, float h, float fixedCoord, bool alongX)
        {
            const float LeafW = 0.75f;
            const float PanelH = 0.40f;      // 아래 청판 높이
            float y0 = y - h * 0.5f, y1 = y + h * 0.5f;
            float span = a1 - a0, mid = (a0 + a1) * 0.5f;

            var group = Group(g, name);
            int n = Mathf.Max(1, Mathf.RoundToInt(span / LeafW));
            float w = span / n;

            // 청판 — 발치
            Face(group, name + "_청판", mid, y0 + PanelH * 0.5f, span, PanelH, 0.055f,
                 fixedCoord, alongX, _beam, true, 0.7f);

            // 한지 — 청판 위. 살보다 바깥쪽 한 겹.
            float ph = y1 - (y0 + PanelH);
            Face(group, name + "_한지", mid, y0 + PanelH + ph * 0.5f, span, ph, 0.014f,
                 fixedCoord, alongX, _paper, true, 1f);

            // 문틀 — 위아래 가로대와 짝 사이 세로대
            Face(group, name + "_위틀", mid, y1 - FrameW * 0.5f, span, FrameW, 0.10f,
                 fixedCoord, alongX, _beam, false, 0.6f);
            Face(group, name + "_아래틀", mid, y0 + PanelH + FrameW * 0.5f, span, FrameW, 0.10f,
                 fixedCoord, alongX, _beam, false, 0.6f);
            for (int i = 0; i <= n; i++)
                Face(group, name + "_설주" + i, a0 + w * i, (y0 + y1) * 0.5f, FrameW, h, 0.10f,
                     fixedCoord, alongX, _beam, false, 0.6f);

            // 살 — 창과 같은 숫대살
            Sal(Group(group, "살"), fixedCoord, a0 + FrameW * 0.5f, a1 - FrameW * 0.5f,
                y0 + PanelH + FrameW, y1 - FrameW, !alongX);
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

            // ③ 창틀 — 아래·위 가로틀과 양끝·가운데 세로틀
            Box(g, "아래틀", new Vector3(fx, sillTop + FrameW * 0.5f, cz),
                new Vector3(WallThick, FrameW, span), _beam, false, null, 0.6f);
            Box(g, "위틀", new Vector3(fx, winTop - FrameW * 0.5f, cz),
                new Vector3(WallThick, FrameW, span), _beam, false, null, 0.6f);
            for (int k = 0; k <= 2; k++)
                Box(g, "세로틀_" + k, new Vector3(fx, (sillTop + winTop) * 0.5f, z0 + span * 0.5f * k),
                    new Vector3(WallThick, WinH, FrameW), _beam, false, null, 0.6f);

            // ④ 한지 — 살보다 바깥쪽 한 겹
            float inner0 = sillTop + FrameW, inner1 = winTop - FrameW;
            Box(g, "창호지", new Vector3(fx + outward * 0.035f, (inner0 + inner1) * 0.5f, cz),
                new Vector3(0.012f, inner1 - inner0, span - FrameW), _changho, false, null, 1f);

            // ⑤ 살 — 숫대살
            Sal(Group(g, "창살"), fx, z0 + FrameW * 0.5f, z1 - FrameW * 0.5f, inner0, inner1, true);
        }

        // ── 숫대살 ──────────────────────────────────

        /// <summary>
        /// <b>숫대살</b> — 산가지를 늘어놓은 듯 크고 작은 네모가 엇물리는 살.
        ///
        /// 세살(가는 세로살에 가로 세 줄)은 방문에 쓰는 수수한 살이다. 사랑채 실내가
        /// 그것을 쓴다(M_창살문, 세로 일곱에 가로 세 묶음). 조사청의 창은 격을 한 단
        /// 올린다 — 왕명을 받는 마루니 살도 그만한 것이 걸려야 한다.
        ///
        /// <b>어떻게 짜나</b>: 한 짝을 <b>바람개비꼴</b>로 가른다. 네 변에 띠를 두르되
        /// 서로 한 칸씩 밀어 붙여(위 띠는 오른쪽을 비우고, 오른 띠는 아래를 비우고…)
        /// 돌아가게 하고, 남은 가운데를 다시 같은 식으로 가른다. 마지막에 남는 것이
        /// 가장 큰 알이다. 이 규칙은 <b>언제나 빈틈 없이 들어맞으므로</b> 살이 허공에서
        /// 끊기는 일이 없다 — 무늬를 손으로 하나씩 적어 넣으면 반드시 한둘이 어긋난다.
        ///
        /// 띠를 길이 방향으로 잘게 썰어 잔살을 만든다. 그래서 겉은 잘고 안으로 갈수록
        /// 성기며, 한가운데는 훤한 알 하나가 남는다.
        /// </summary>
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
