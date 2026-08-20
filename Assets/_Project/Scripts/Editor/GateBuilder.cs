using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 옹고집 1막에 딱 필요한 만큼만 상자로 짓는다 — <b>대문채 · 담장 · 중문</b>.
    /// 메뉴: [이문록 ▸ 옹고집 대문 짓기]
    ///
    /// 왜 이것만인가: 1막에서 플레이어가 지나는 곳은 대문 앞마당에서 중문까지다.
    /// 받아온 김명관 고택은 113×138m 에 천칠백 덩이라 그 길 하나 보자고 마을 전체를
    /// 들고 있어야 한다. 사랑채와 조사청이 같은 이유로 상자 실내를 따로 지었다.
    ///
    /// 치수는 지어내지 않고 고택에서 잰 것이다:
    ///   · 대문 두 짝 각 1.07 × 2.53 (열린 폭 2.1)
    ///   · 중문 두 짝 각 0.60 × 1.83 (열린 폭 1.1)
    ///   · 기둥 0.26~0.35 굵기에 2.95 높이, 창방 0.28
    ///   · 칸 사이 2.6
    ///
    /// 자리 관계도 실제 그대로다. <b>대문 축은 12도, 중문 축은 99도</b> — 대문을 지나
    /// 여덟 자 반쯤 들어가 거의 직각으로 꺾어야 중문이 나온다. 곧게 뚫린 길이 아니라
    /// 꺾이는 길이라는 것이 이 집의 첫인상이므로 그것부터 맞춘다.
    ///
    /// 처음엔 빈 터에 따로 세운다(원본과 겹치면 둘 다 못 본다). 마음에 들면
    /// [옹고집 대문 제자리에 앉히기] 로 실제 자리로 옮긴다.
    /// </summary>
    public static class GateBuilder
    {
        private const string RootName = "옹고집_대문블록";

        // ── 고택에서 잰 값 ───────────────────────────
        private const float Bay = 2.6f;            // 칸
        private const float PillarW = 0.30f;
        private const float PillarH = 2.95f;
        private const float BeamThick = 0.28f;

        private const float GateLeaf = 1.07f;      // 대문 한 짝
        private const float GateDoorH = 2.53f;
        private const float MidLeaf = 0.60f;       // 중문 한 짝
        private const float MidDoorH = 1.83f;

        private const float GidanH = 0.35f;        // 기단 높이
        private const float GateDepth = 2.6f;      // 대문채 깊이(한 칸)

        // 담장
        private const float WallH = 1.5f;          // 몸통 높이
        private const float WallThick = 0.45f;
        private const float WallBaseH = 0.5f;      // 밑돌
        private const float WallRun = 12f;         // 대문 좌우로 뻗는 길이
        private const float MidWallRun = 5f;       // 중문 좌우

        // 실제 자리(고택 기준)
        private static readonly Vector3 GateSpot = new Vector3(-7.54f, -2.50f, -24.40f);
        private const float GateYaw = 12f;
        private static readonly Vector3 MidSpot = new Vector3(-2.01f, -0.94f, -17.42f);
        private const float MidYaw = 99f;

        private static Material _wood, _beam, _wall, _door, _giwa, _gidan, _stone, _wallStone, _ground;
        private static Material _plank, _hanji, _metal, _rafter;

        /// <summary>처마 끝 막새·마구리를 붙일지. 동그라미 한 줄이 원기둥 서른 개라 무겁다.</summary>
        private const bool EaveDetail = true;

        [MenuItem("이문록/옹고집 대문 짓기")]
        public static void Build()
        {
            if (!LoadMaterials()) return;

            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "대문 짓기");
            // 빈 터에 세운다. 고택 한복판에 겹쳐 두면 어느 것이 새로 지은 것인지 못 가린다.
            root.transform.position = GateSpot + new Vector3(-60f, 0f, 0f);

            // 대문 축을 0도로 놓고 짓는다. 중문은 그 차이(87도)만큼만 꺾어 앉힌다 —
            // 그래야 통째로 옮겨도 둘 사이의 관계가 흐트러지지 않는다.
            var 마당 = Group(root.transform, "마당");
            Box(마당, "바닥", new Vector3(0f, -0.15f, 4f), new Vector3(34f, 0.3f, 26f), _ground, true, null, 0.35f);

            var 대문채 = Group(root.transform, "대문채");
            BuildGateHouse(대문채);

            var 담 = Group(root.transform, "담장");
            BuildFence(담, new Vector3(-(Bay * 1.5f + WallRun * 0.5f), 0f, 0f), WallRun, 0f, "담장_서");
            BuildFence(담, new Vector3(+(Bay * 1.5f + WallRun * 0.5f), 0f, 0f), WallRun, 0f, "담장_동");

            // 중문 — 대문에서 잰 그대로. 여기서 길이 꺾인다.
            Vector3 rel = Quaternion.Euler(0f, -GateYaw, 0f) * (MidSpot - GateSpot);
            var 중문 = Group(root.transform, "중문채");
            중문.localPosition = new Vector3(rel.x, rel.y, rel.z);
            중문.localRotation = Quaternion.Euler(0f, MidYaw - GateYaw, 0f);
            BuildMiddleGate(중문, rel.y);

            int tris = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;

            Selection.activeGameObject = root;
            Debug.Log($"[대문] 지었습니다 — 오브젝트 {root.GetComponentsInChildren<Transform>().Length}개, {tris} 삼각형. " +
                      $"중문은 대문에서 {rel.magnitude:F1}m, {MidYaw - GateYaw:F0}도 꺾인 자리.");
        }

        [MenuItem("이문록/옹고집 대문 제자리에 앉히기")]
        public static void PutInPlace()
        {
            var root = GameObject.Find(RootName);
            if (root == null) { Debug.LogWarning("[대문] 지은 것이 없습니다."); return; }
            Undo.RecordObject(root.transform, "대문 제자리");
            root.transform.SetPositionAndRotation(GateSpot, Quaternion.Euler(0f, GateYaw, 0f));
            Debug.Log("[대문] 실제 자리에 앉혔습니다. 원본 고택과 겹치니 견주어 보고 한쪽을 끄세요.");
        }

        // ── 대문채 ───────────────────────────────────

        /// <summary>정면 세 칸. 가운데가 문이고 좌우는 행랑(벽)이다.</summary>
        private static void BuildGateHouse(Transform g)
        {
            float w = Bay * 3f;
            float half = w * 0.5f;

            Box(g, "기단", new Vector3(0f, GidanH * 0.5f, 0f),
                new Vector3(w + 0.8f, GidanH, GateDepth + 0.8f), _gidan, true, null, 0.6f);

            // 기둥 넷 × 앞뒤 두 줄
            for (int i = 0; i <= 3; i++)
            {
                float x = -half + Bay * i;
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = s * GateDepth * 0.5f;
                    Box(g, $"주춧돌_{i}_{s}", new Vector3(x, GidanH + 0.09f, z),
                        new Vector3(0.7f, 0.18f, 0.7f), _stone, false, null, 1f);
                    Box(g, $"기둥_{i}_{s}", new Vector3(x, GidanH + 0.18f + PillarH * 0.5f, z),
                        new Vector3(PillarW, PillarH, PillarW), _wood, true, null, 0.6f);
                }
            }

            float top = GidanH + 0.18f + PillarH;
            // 창방 — 기둥 머리를 앞뒤로 두 줄 묶는다
            for (int s = -1; s <= 1; s += 2)
                Box(g, s < 0 ? "창방_앞" : "창방_뒤", new Vector3(0f, top - BeamThick * 0.5f, s * GateDepth * 0.5f),
                    new Vector3(w + PillarW, BeamThick, 0.24f), _beam, false, null, 0.6f);
            // 보 — 앞뒤를 건너지른다
            for (int i = 0; i <= 3; i++)
                Box(g, $"보_{i}", new Vector3(-half + Bay * i, top - BeamThick * 0.5f, 0f),
                    new Vector3(0.24f, BeamThick, GateDepth), _beam, false, null, 0.6f);

            // 좌우 행랑 벽
            for (int s = -1; s <= 1; s += 2)
            {
                float cx = s * Bay;
                Box(g, s < 0 ? "행랑벽_서" : "행랑벽_동",
                    new Vector3(cx, GidanH + (top - GidanH) * 0.5f, 0f),
                    new Vector3(Bay - PillarW, top - GidanH, 0.30f), _wall, true, null, 0.5f);
            }

            // 가운데 칸 = 문. 두 짝이 가운데서 맞물린다.
            float sill = GidanH;
            Box(g, "문지방", new Vector3(0f, sill + 0.06f, 0f), new Vector3(Bay, 0.12f, 0.34f), _beam, false, null, 1f);
            for (int s = -1; s <= 1; s += 2)
                DoorLeaf(g, s < 0 ? "대문짝_서" : "대문짝_동",
                         new Vector3(s * GateLeaf * 0.5f, sill + 0.12f + GateDoorH * 0.5f, 0f),
                         GateLeaf - 0.02f, GateDoorH, 0.10f, _plank, 5, 3, s);
            // 문 위 인방 — 문짝이 처마까지 닿으면 문이 아니라 벽이 된다
            float lintel = top - (sill + 0.12f + GateDoorH);
            if (lintel > 0.05f)
                Box(g, "문위_인방", new Vector3(0f, top - lintel * 0.5f, 0f),
                    new Vector3(Bay - PillarW, lintel, 0.28f), _wall, false, null, 0.5f);

            Roof(g, "지붕", 0f, top, w, GateDepth, 1.0f, 1.5f);
        }

        // ── 담장 ─────────────────────────────────────

        /// <summary>밑돌 · 몸통 · 기와를 얹은 담 한 줄. len 만큼 x 방향으로 뻗는다.</summary>
        private static void BuildFence(Transform parent, Vector3 center, float len, float yaw, string name)
        {
            var g = Group(parent, name);
            g.localPosition = center;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box(g, "밑돌", new Vector3(0f, WallBaseH * 0.5f, 0f),
                new Vector3(len, WallBaseH, WallThick + 0.12f), _stone, true, null, 0.8f);
            Box(g, "몸통", new Vector3(0f, WallBaseH + WallH * 0.5f, 0f),
                new Vector3(len, WallH, WallThick), _wallStone, true, null, 0.7f);

            // 담도 지붕을 인다. 이것이 없으면 담이 아니라 옹벽으로 보인다.
            float capY = WallBaseH + WallH;
            for (int s = -1; s <= 1; s += 2)
                Box(g, s < 0 ? "기와_남" : "기와_북", new Vector3(0f, capY + 0.12f, s * 0.16f),
                    new Vector3(len, 0.14f, 0.42f), _giwa, false,
                    Quaternion.Euler(s * 18f, 0f, 0f), 1.2f);
            Box(g, "용마루", new Vector3(0f, capY + 0.2f, 0f), new Vector3(len, 0.12f, 0.18f), _giwa, false, null, 1.2f);
        }

        // ── 중문 ─────────────────────────────────────

        /// <summary>한 칸짜리 작은 문. 좌우로 담이 조금 붙고, 아래로 축대와 계단이 받친다.</summary>
        private static void BuildMiddleGate(Transform g, float rise)
        {
            // 이 집은 터가 앞뒤로 기울어 있다 — 대문에서 중문까지 한 길 반쯤 오른다.
            // 축대로 그 턱을 받치고 계단으로 올라가게 해야 "꺾어 올라간다"가 몸에 남는다.
            if (rise > 0.2f) BuildTerrace(g, rise);

            const float open = 1.3f;                 // 열린 폭
            float top = MidDoorH + 0.5f;

            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (open * 0.5f + PillarW * 0.5f);
                Box(g, $"기둥_{s}", new Vector3(x, top * 0.5f, 0f),
                    new Vector3(PillarW, top, PillarW), _wood, true, null, 0.6f);
            }
            Box(g, "문지방", new Vector3(0f, 0.06f, 0f), new Vector3(open, 0.12f, 0.3f), _beam, false, null, 1f);
            for (int s = -1; s <= 1; s += 2)
                DoorLeaf(g, s < 0 ? "중문짝_서" : "중문짝_동",
                         new Vector3(s * MidLeaf * 0.5f, 0.12f + MidDoorH * 0.5f, 0f),
                         MidLeaf - 0.02f, MidDoorH, 0.08f, _hanji, 3, 6, s);
            Box(g, "인방", new Vector3(0f, top - 0.14f, 0f), new Vector3(open + PillarW, 0.28f, 0.26f), _beam, false, null, 0.6f);

            Roof(g, "지붕", 0f, top, open + PillarW * 2f, 1.2f, 0.6f, 0.8f);

            // 중문에 붙는 담 — 문만 서 있으면 옆으로 돌아 들어갈 수 있어 보인다
            for (int s = -1; s <= 1; s += 2)
                BuildFence(g, new Vector3(s * (open * 0.5f + PillarW + MidWallRun * 0.5f), 0f, 0f),
                           MidWallRun, 0f, s < 0 ? "담장_서" : "담장_동");
        }

        // ── 문짝 한 짝 ───────────────────────────────

        /// <summary>
        /// 문짝을 통짜 판때기로 세우지 않고 <b>짜 맞춘다</b> — 테를 두르고, 그 안에 살대를
        /// 걸고, 뒤에 판(또는 한지)을 댄다.
        ///
        /// 왜: 상자 하나에 문 그림을 붙이면 그림이 상자 크기대로 늘어나 살대 간격이
        /// 제멋대로가 된다. 처음에 대문이 이상해 보인 까닭이 그것이었다. 살대를 실제로
        /// 세워 두면 어느 크기로 짜도 간격이 살아 있고, 문이 두께를 가진 물건이 된다.
        ///
        /// cols·rows 는 살대 수다. 대문은 성글게(판문에 띠장), 중문은 촘촘하게(창호).
        /// side 는 -1/+1 — 문고리가 맞물리는 쪽에 붙게 한다.
        /// </summary>
        private static void DoorLeaf(Transform parent, string name, Vector3 center,
                                     float w, float h, float t, Material panel,
                                     int cols, int rows, int side)
        {
            var g = Group(parent, name);
            g.localPosition = center;

            const float FrameW = 0.075f;      // 테 굵기
            const float barW = 0.055f;        // 살대 굵기 — 얇으면 밝은 판에 묻혀 안 보인다
            const float barOut = 0.42f;       // 판보다 이만큼(두께 배수) 앞으로 나온다

            // 뒤판 — 대문은 널판, 중문은 한지
            Box(g, "판", Vector3.zero, new Vector3(w - FrameW, h - FrameW, t * 0.45f), panel, true, null, 1.4f);

            // 테 넷
            Box(g, "테_위", new Vector3(0f, (h - FrameW) * 0.5f, 0f), new Vector3(w, FrameW, t), _beam, false, null, 1f);
            Box(g, "테_아래", new Vector3(0f, -(h - FrameW) * 0.5f, 0f), new Vector3(w, FrameW, t), _beam, false, null, 1f);
            Box(g, "테_왼", new Vector3(-(w - FrameW) * 0.5f, 0f, 0f), new Vector3(FrameW, h, t), _beam, false, null, 1f);
            Box(g, "테_오른", new Vector3((w - FrameW) * 0.5f, 0f, 0f), new Vector3(FrameW, h, t), _beam, false, null, 1f);

            // 살대 — 세로
            for (int i = 1; i <= cols; i++)
            {
                float x = -w * 0.5f + w * i / (cols + 1f);
                Box(g, $"살_세로{i}", new Vector3(x, 0f, -t * barOut),
                    new Vector3(barW, h - FrameW * 2f, t * 0.55f), _beam, false, null, 1f);
            }
            // 살대 — 가로(대문에서는 띠장 노릇을 한다)
            for (int i = 1; i <= rows; i++)
            {
                float y = -h * 0.5f + h * i / (rows + 1f);
                Box(g, $"살_가로{i}", new Vector3(0f, y, -t * barOut),
                    new Vector3(w - FrameW * 2f, barW * 1.3f, t * 0.6f), _beam, false, null, 1f);
            }

            // 문고리 — 맞물리는 쪽에
            if (_metal != null)
            {
                float hx = -side * (w * 0.5f - FrameW * 1.6f);
                Box(g, "문고리_바탕", new Vector3(hx, 0f, -t * 0.55f), new Vector3(0.13f, 0.13f, 0.03f), _metal, false, null, 1f);
                Ring(g, "문고리", new Vector3(hx, -0.07f, -t * 0.62f), 0.09f, 0.018f, _metal);
            }
        }

        /// <summary>납작한 고리 하나. 원통 여덟 도막을 둥글게 돌려 세운다.</summary>
        private static void Ring(Transform parent, string name, Vector3 center, float radius, float thick, Material mat)
        {
            var g = Group(parent, name);
            g.localPosition = center;
            const int N = 8;
            for (int i = 0; i < N; i++)
            {
                float a = Mathf.PI * 2f * i / N;
                float seg = 2f * Mathf.PI * radius / N * 1.15f;
                Box(g, $"도막{i}", new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f),
                    new Vector3(thick, seg, thick), mat, false,
                    Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f), 0f);
            }
        }

        // ── 축대와 계단 ──────────────────────────────

        /// <summary>중문이 올라앉은 단. 뒤로는 마당이 이어지고 앞으로는 계단이 내려간다.</summary>
        private static void BuildTerrace(Transform g, float rise)
        {
            var t = Group(g, "축대");

            const float Wide = 16f;    // 좌우로 넉넉히 — 담장까지 받쳐야 한다
            const float Deep = 12f;    // 중문 뒤쪽(안마당) 방향

            // 단 자체. 윗면이 중문 발치와 같아야 한다.
            Box(t, "단", new Vector3(0f, -rise * 0.5f, Deep * 0.5f - 0.6f),
                new Vector3(Wide, rise, Deep), _wallStone, true, null, 0.6f);
            // 단 앞면 갓돌 — 돌이 그냥 잘린 것처럼 보이지 않게
            Box(t, "갓돌", new Vector3(0f, -0.09f, -0.6f + 0.06f),
                new Vector3(Wide, 0.18f, 0.5f), _stone, false, null, 0.9f);

            // 계단 — 한 단 0.18 높이로 나눈다(한옥 댓돌 어림)
            int steps = Mathf.Max(2, Mathf.RoundToInt(rise / 0.18f));
            float h = rise / steps;
            const float TreadZ = 0.34f;
            for (int i = 0; i < steps; i++)
            {
                float y = -h * (i + 0.5f);
                float z = -0.6f - TreadZ * (i + 0.5f);
                Box(t, $"디딤_{i}", new Vector3(0f, y, z),
                    new Vector3(2.4f, h, TreadZ + 0.02f), _stone, true, null, 1f);
            }
        }

        // ── 지붕 한 채 ───────────────────────────────

        /// <summary>맞배지붕. 처마를 내밀고 물매를 세운다.</summary>
        private static void Roof(Transform g, string name, float cx, float eaveY,
                                 float width, float depth, float overhang, float rise)
        {
            var r = Group(g, name);
            float runZ = depth * 0.5f + overhang;
            float slope = Mathf.Sqrt(runZ * runZ + rise * rise);
            float ang = Mathf.Atan2(rise, runZ) * Mathf.Rad2Deg;
            float y = eaveY + rise * 0.5f;
            float w = width + overhang * 2f;

            Box(r, "물매_북", new Vector3(cx, y, runZ * 0.5f), new Vector3(w, 0.22f, slope),
                _giwa, false, Quaternion.Euler(ang, 0f, 0f), 1.0f);
            Box(r, "물매_남", new Vector3(cx, y, -runZ * 0.5f), new Vector3(w, 0.22f, slope),
                _giwa, false, Quaternion.Euler(ang, 180f, 0f), 1.0f);
            Box(r, "용마루", new Vector3(cx, eaveY + rise + 0.09f, 0f), new Vector3(w, 0.22f, 0.45f), _giwa, false, null, 1.2f);

            if (EaveDetail) { EaveEnds(r, cx, eaveY, w, runZ); }

            // 박공 — 옆으로 하늘이 새지 않게 층지어 깎는다
            const int Steps = 6;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = cx + s * width * 0.5f;
                for (int k = 0; k < Steps; k++)
                {
                    float t0 = (float)k / Steps, t1 = (float)(k + 1) / Steps;
                    float d = 2f * runZ * (1f - (t0 + t1) * 0.5f);
                    if (d < 0.05f) continue;
                    Box(r, $"박공_{s}_{k}", new Vector3(x, eaveY + rise * (t0 + t1) * 0.5f, 0f),
                        new Vector3(0.12f, rise / Steps + 0.01f, d), _wall, false, null, 0.6f);
                }
            }
        }

        /// <summary>
        /// 처마 끝에 동그란 것 두 줄 — <b>막새</b>와 <b>서까래 마구리</b>다.
        ///
        /// 한옥 처마를 밑에서 올려다보면 동그라미가 위아래 두 줄로 늘어선다. 위는 기와가
        /// 끝나는 자리에 물린 막새(수막새)의 얼굴이고, 아래는 그 기와를 받치는 서까래의
        /// 잘린 끝이다. 처마를 판때기 한 장으로 끝내면 이 두 줄이 없어 지붕이 종이처럼
        /// 얇아 보인다 — 무게가 실리는 곳이 여기다.
        /// </summary>
        private static void EaveEnds(Transform r, float cx, float eaveY, float w, float runZ)
        {
            var g = Group(r, "처마끝");
            const float Step = 0.33f;          // 기와 한 장 폭 어림
            int n = Mathf.Max(2, Mathf.FloorToInt(w / Step));
            float pitch = w / n;

            for (int s = -1; s <= 1; s += 2)      // 앞뒤 두 물매
            {
                float z = s * (runZ + 0.02f);
                for (int i = 0; i <= n; i++)
                {
                    float x = cx - w * 0.5f + pitch * i;

                    // 막새 — 기와가 끝나는 자리의 동그란 얼굴
                    Disc(g, $"막새_{s}_{i}", new Vector3(x, eaveY + 0.02f, z), 0.15f, 0.10f, _giwa, s);
                    // 서까래 마구리 — 그 아래, 조금 작고 나무다
                    Disc(g, $"마구리_{s}_{i}", new Vector3(x, eaveY - 0.20f, z - s * 0.05f), 0.11f, 0.16f, _rafter, s);
                }
            }
        }

        /// <summary>동그란 면이 바깥(±z)을 보게 세운 짧은 원기둥.</summary>
        private static void Disc(Transform parent, string name, Vector3 center, float diameter, float depth,
                                 Material mat, int facing)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            // 원기둥은 길이가 y 축이다. x 로 아흔 도 눕히면 동그란 면이 z 를 본다.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(diameter, depth * 0.5f, diameter);
            var rr = go.GetComponent<Renderer>();
            rr.sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        // ── 재질과 상자 ──────────────────────────────

        private const string HouseDir = "Assets/_Project/Onggojip/Art/KimMyeonggwanHouse/Material/";

        private static bool LoadMaterials()
        {
            _wood      = Load("MI_Wood01A");        // 기둥
            _beam      = Load("MI_Wood02A");        // 창방·보·문지방
            _wall      = Load("MI_WhiteWall01A");   // 회벽
            _door      = Load("MI_Door01A");        // 문짝
            _giwa      = Load("MI_Giwa");           // 기와
            _gidan     = Load("MI_GidanStone01A");  // 기단
            _stone     = Load("MI_Stone02A");       // 주춧돌·밑돌
            _wallStone = Load("MI_StoneWall02A");   // 담 몸통
            _ground    = Load("MI_Ground01A");      // 마당
            _plank     = Load("MI_Wood01A");        // 대문 널판 — 마루판(Wood03A)은 번들거려 허옇게 난다
            _metal     = Load("MI_Blackmetal01A");  // 문고리·못 — 진짜 대문이 쓰는 것
            _rafter    = Load("MI_RafterA");        // 서까래
            _hanji = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Onggojip/Art/사랑채실내/MI_사랑방_한지.mat");   // 중문 창호

            if (_wood == null || _wall == null || _giwa == null)
            {
                Debug.LogError("[대문] 고택 재질을 못 찾았습니다: " + HouseDir);
                return false;
            }
            if (_door == null) _door = _beam;
            if (_wallStone == null) _wallStone = _stone;
            if (_ground == null) _ground = _gidan;
            if (_plank == null) _plank = _wood;
            if (_hanji == null) _hanji = _wall;
            if (_rafter == null) _rafter = _beam;
            return true;
        }

        private static Material Load(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(HouseDir + name + ".mat");
            if (m == null) Debug.LogWarning("[대문] 재질 없음: " + name);
            return m;
        }

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        /// <summary>상자 하나. tile 은 1m 에 무늬를 몇 번 되풀이할지.</summary>
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
                // 큐브는 여섯 면이 다 UV 0~1 이라, 크게 늘리면 무늬 한 장이 그만큼 늘어난다.
                float a = Mathf.Max(size.x, size.z), b = Mathf.Max(size.y, Mathf.Min(size.x, size.z));
                var mpb = new MaterialPropertyBlock();
                mpb.SetVector(BaseMapSt, new Vector4(Mathf.Max(1f, a * tile), Mathf.Max(1f, b * tile), 0f, 0f));
                r.SetPropertyBlock(mpb);
            }
            if (!collide) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }
    }
}
