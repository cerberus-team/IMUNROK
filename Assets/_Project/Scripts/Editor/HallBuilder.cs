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

        // 방 둘의 경계(원본 벽 자리에서 잰 것)
        private const float RoomZMin = -1.54f, RoomZMax = 1.46f;
        private const float WestX0 = -4.54f, WestX1 = -1.54f;
        private const float MidX0 = -1.54f, MidX1 = 1.46f;

        private static Material _wood, _wall, _floor, _gidan, _stone, _paper, _roof;
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
            BuildDoors(Group(구조, "문"));
            BuildRoof(Group(구조, "지붕"));

            int tris = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;

            Selection.activeGameObject = root;
            Debug.Log($"[조사청] 실내를 지었습니다 — 오브젝트 {root.GetComponentsInChildren<Transform>().Length}개, {tris} 삼각형.");
        }

        private const string MatDir = "Assets/_Project/_Common/Materials";

        /// <summary>
        /// 부재에 쓸 재질을 갖춘다. 소쇄원 재질을 <b>그대로 쓰지 않고</b> 같은 그림으로
        /// 새로 만든다.
        ///
        /// 왜: 소쇄원 재질은 언리얼에서 옮겨 온 셰이더(Unreal/PBR_Shader)라 그림 슬롯이
        /// Material_Texture2D_0..4 라는 이름이다. 유니티가 아는 _BaseMap 이 없으니
        /// 되풀이 값(_BaseMap_ST)을 아무리 줘도 씹힌다. 첫 판에 지붕이 거울처럼 번들거린
        /// 것이 그 탓이었다 — 거칠기 그림이 11m 짜리 한 장으로 늘어나 매끈한 쪽만 남았다.
        /// 같은 그림을 URP Lit 에 물려 두면 되풀이도 먹고 번들거림도 우리가 정한다.
        /// </summary>
        private static bool LoadMaterials()
        {
            const string dir = "Assets/Soswaewon/Materials/Buildings/";
            if (!AssetDatabase.IsValidFolder(MatDir))
                AssetDatabase.CreateFolder("Assets/_Project/_Common", "Materials");

            _wood  = Ensure("M_조사청_나무", dir + "MI_Wood.mat", 0.12f);
            _wall  = Ensure("M_조사청_벽",   dir + "MI_Wall.mat", 0.06f);
            _floor = Ensure("M_조사청_마루", dir + "MI_GPG_Floor01a.mat", 0.18f);
            _gidan = Ensure("M_조사청_기단", dir + "MI_Gidan01b.mat", 0.08f);
            _stone = Ensure("M_조사청_돌",   dir + "MI_Cornerstone.mat", 0.08f);
            _paper = Ensure("M_조사청_한지", dir + "MI_GPG_Door01a.mat", 0.05f);
            _roof  = Ensure("M_조사청_기와", dir + "MI_Roof.mat", 0.10f);

            if (_wood == null || _wall == null || _floor == null || _gidan == null || _stone == null)
            {
                Debug.LogError("[조사청] 재질을 만들지 못했습니다. 소쇄원 재질을 찾을 수 없습니다: " + dir);
                return false;
            }
            if (_paper == null) _paper = _wall;
            if (_roof == null) _roof = _gidan;
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>소쇄원 재질에서 그림만 빌려 URP Lit 재질을 만든다(이미 있으면 그것을 쓴다).</summary>
        private static Material Ensure(string name, string sourcePath, float smoothness)
        {
            string path = $"{MatDir}/{name}.mat";
            var mine = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mine != null) return mine;

            var src = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (src == null) return null;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("[조사청] URP Lit 셰이더가 없습니다."); return null; }

            mine = new Material(shader) { name = name };
            // 언리얼 쪽 슬롯: 0=법선(NM), 1=바탕색(BC), 3=거칠기(RN)
            var bc = src.HasProperty("Material_Texture2D_1") ? src.GetTexture("Material_Texture2D_1") : null;
            var nm = src.HasProperty("Material_Texture2D_0") ? src.GetTexture("Material_Texture2D_0") : null;
            if (bc != null) mine.SetTexture("_BaseMap", bc);
            if (nm != null) { mine.SetTexture("_BumpMap", nm); mine.EnableKeyword("_NORMALMAP"); }
            mine.SetFloat("_Smoothness", smoothness);
            mine.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(mine, path);
            Debug.Log($"[조사청] 재질을 만들었습니다: {path}");
            return mine;
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
            float cx = (XMin + XMax) * 0.5f, cz = (ZMin + ZMax) * 0.5f;
            Box(g, "마루_판", new Vector3(cx, FloorTop - FloorThick * 0.5f, cz),
                new Vector3(XMax - XMin, FloorThick, ZMax - ZMin), _floor, true, null, 0.7f);
        }

        private static void BuildBeams(Transform g)
        {
            float y = BeamBottom + BeamThick * 0.5f;
            int nx = Mathf.RoundToInt((XMax - XMin) / Bay) + 1;
            int nz = Mathf.RoundToInt((ZMax - ZMin) / Bay) + 1;
            float cx = (XMin + XMax) * 0.5f, cz = (ZMin + ZMax) * 0.5f;

            for (int j = 0; j < nz; j++)   // 도리 — 정면과 나란히
                Box(g, $"도리_z{j}", new Vector3(cx, y, ZMin + Bay * j),
                    new Vector3(XMax - XMin + PillarW, BeamThick, 0.20f), _wood, false, null, 0.6f);

            for (int i = 0; i < nx; i++)   // 보 — 앞뒤로 건너지른다
                Box(g, $"보_x{i}", new Vector3(XMin + Bay * i, y, cz),
                    new Vector3(0.20f, BeamThick, ZMax - ZMin), _wood, false, null, 0.6f);
        }

        private static void BuildRafters(Transform g)
        {
            // 서까래는 처마 쪽으로 조금 빠져나온다. 끝이 벽에서 딱 끊기면 지붕이 없어 보인다.
            float len = (ZMax - ZMin) + 1.6f;
            float cz = (ZMin + ZMax) * 0.5f;
            int n = Mathf.FloorToInt((XMax - XMin) / RafterStep);
            for (int i = 0; i <= n; i++)
                Box(g, $"서까래_{i}", new Vector3(XMin + RafterStep * i, RafterY, cz),
                    new Vector3(0.11f, 0.11f, len), _wood, false, null, 0.6f);
        }

        private static void BuildCeiling(Transform g)
        {
            // 반자는 방 위에만 있다. 열린 마루 위는 서까래가 그대로 보이는 자리다(연등천장).
            Box(g, "반자_서", Mid(WestX0, WestX1, CeilY, RoomZMin, RoomZMax),
                new Vector3(WestX1 - WestX0, 0.08f, RoomZMax - RoomZMin), _wall, false, null, 0.5f);
            Box(g, "반자_중", Mid(MidX0, MidX1, CeilY, RoomZMin, RoomZMax),
                new Vector3(MidX1 - MidX0, 0.08f, RoomZMax - RoomZMin), _wall, false, null, 0.5f);
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
            Box(g, "인방위_서_서", new Vector3(WestX0, ly, (RoomZMin + RoomZMax) * 0.5f),
                new Vector3(WallThick, lintel, RoomZMax - RoomZMin), _wall, false, null, 0.5f);
            Box(g, "인방위_중_동", new Vector3(MidX1, ly, (RoomZMin + RoomZMax) * 0.5f),
                new Vector3(WallThick, lintel, RoomZMax - RoomZMin), _wall, false, null, 0.5f);
        }

        private static void BuildDoors(Transform g)
        {
            // 한 짝 폭 0.75 — 한 칸(1.5)에 두 짝. 남쪽·서쪽·동쪽 세 면이 문이다.
            float h = DoorTop - FloorTop;
            float y = FloorTop + h * 0.5f;

            Leaves(g, "문_서방_남", WestX0, WestX1, y, h, RoomZMin, true);
            Leaves(g, "문_중방_남", MidX0, MidX1, y, h, RoomZMin, true);
            Leaves(g, "문_서방_서", RoomZMin, RoomZMax, y, h, WestX0, false);
            Leaves(g, "문_중방_동", RoomZMin, RoomZMax, y, h, MidX1, false);
        }

        /// <summary>한 면을 문짝으로 채운다. alongX 면 x 방향으로, 아니면 z 방향으로 늘어놓는다.</summary>
        private static void Leaves(Transform g, string name, float a0, float a1,
                                   float y, float h, float fixedCoord, bool alongX)
        {
            const float LeafW = 0.75f;
            int n = Mathf.Max(1, Mathf.RoundToInt((a1 - a0) / LeafW));
            float w = (a1 - a0) / n;
            var group = Group(g, name);
            for (int i = 0; i < n; i++)
            {
                float c = a0 + w * (i + 0.5f);
                var pos = alongX ? new Vector3(c, y, fixedCoord) : new Vector3(fixedCoord, y, c);
                var size = alongX ? new Vector3(w - 0.03f, h, 0.06f) : new Vector3(0.06f, h, w - 0.03f);
                Box(group, $"{name}_{i}", pos, size, _paper, true, null, 1f);

                // 문틀 — 짝과 짝 사이 세로대. 이것이 없으면 종이 한 장이 된다.
                var sPos = alongX ? new Vector3(a0 + w * i, y, fixedCoord) : new Vector3(fixedCoord, y, a0 + w * i);
                var sSize = alongX ? new Vector3(0.07f, h, 0.09f) : new Vector3(0.09f, h, 0.07f);
                Box(group, $"{name}_틀{i}", sPos, sSize, _wood, false, null, 0.6f);
            }
            var endPos = alongX ? new Vector3(a1, y, fixedCoord) : new Vector3(fixedCoord, y, a1);
            var endSize = alongX ? new Vector3(0.07f, h, 0.09f) : new Vector3(0.09f, h, 0.07f);
            Box(group, $"{name}_틀끝", endPos, endSize, _wood, false, null, 0.6f);
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
