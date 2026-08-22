using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 관아 문서고를 상자로 짓는다. 메뉴: [이문록 ▸ 관아 ▸ ⑦ 문서고 짓기]
    ///
    /// 왜 받아온 건물을 안 쓰는가: 화성행궁 서리청을 놓아 봤더니 <b>492,430 삼각형</b>이었다.
    /// 동헌이 62,408, 외삼문이 55,668 인 마당에 곁방 하나가 여덟 배다. 격자 뭉치기로
    /// 줄여 봤지만 162,177 에서 바닥이었고 그마저 모양이 부서졌다 — 서브메시 스무 장에
    /// UV 이음새가 많아 뭉칠 데가 없다. 그 도구는 고르게 촘촘한 메시용이지 이런 물건용이 아니다.
    ///
    /// 그래서 사랑채 실내·조사청과 같은 길로 간다 — <b>상자로 짓는다</b>. 부재 하나가
    /// 열두 삼각형이라 한 채를 다 지어도 천 남짓이다.
    ///
    /// <b>단청도 같이 풀린다</b>: 서리청은 붉고 푸른 단청을 입힌 행궁 청사라, 민무늬 동헌
    /// 곁에 세우면 곁방이 주인보다 화려해진다. 여기서는 <b>동헌이 쓰던 재질</b>을 그대로 쓴다.
    ///
    /// 생김새는 문서고답게 잡았다 — 정면 다섯 칸에 측면 두 칸, 기단은 낮고, 창 대신
    /// 판문만 낸다. 문서고는 볕과 비를 꺼려 창을 잘 내지 않는다. 지붕은 맞배다
    /// (상자로는 사다리꼴을 못 깎아서 우진각을 세우면 옆 물매가 엇갈린다 — 조사청에서 겪은 것).
    ///
    /// 다시 부르면 통째로 지우고 새로 짓는다.
    /// </summary>
    public static class GwanaSeogo
    {
        private const string RootName = "문서고";
        private const string MatDir = "Assets/_Project/_Common/Sets/Gwana/Donheon/Materials/";

        // ── 치수(m) ──
        // 정면 다섯 칸 × 측면 두 칸. 서리청(18.79 x 10.17)보다 작게 잡았다 —
        // 마당 너비가 26m 로 줄어서 그만한 채를 옆에 뉘면 길이 막힌다.
        private const float Bay = 2.4f;                       // 정면 한 칸
        private const float FrontW = Bay * 5f;                // 12.0
        private const float SideD = 6.0f;                     // 측면 두 칸(3.0 x 2)

        private const float GidanH = 0.55f;                   // 기단 높이
        private const float GidanOut = 0.9f;                  // 기단이 기둥 밖으로 나온 폭
        private const float StoneTop = 0.70f;                 // 주춧돌 윗면 = 기둥이 앉는 자리
        private const float EaveY = 3.30f;                    // 처마(도리) 높이
        private const float RidgeY = 5.10f;                   // 용마루 높이
        private const float Overhang = 0.95f;                 // 처마 내밀기
        private const float RoofThick = 0.22f;

        /// <summary>
        /// 마당 남쪽. 앞면(+Z)이 마당을 본다. 담이 z=-13, 동헌 앞면이 x=7.7 이다.
        /// z 를 -9.0 에 두었더니 뒤 처마가 담을 0.45m 파고들었다 — 처마는 기단이 아니라
        /// 지붕 폭(측면 절반 + 내밀기 = 3.95m)으로 재야 한다.
        /// </summary>
        private static readonly Vector3 Where = new Vector3(-2.0f, 0f, -8.3f);

        private static Material _wood, _wall, _stone, _rubble, _floor, _roof, _door;
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

        [MenuItem("이문록/관아/⑦ 문서고 짓기")]
        public static void Build()
        {
            if (!LoadMaterials()) return;

            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "문서고 짓기");
            root.transform.position = Where;

            BuildGidan(Group(root.transform, "기단"));
            BuildStones(Group(root.transform, "주춧돌"));
            BuildPillars(Group(root.transform, "기둥"));
            BuildFloor(Group(root.transform, "마루"));
            BuildWalls(Group(root.transform, "벽"));
            BuildDoors(Group(root.transform, "판문"));
            BuildBeams(Group(root.transform, "도리"));
            BuildRoof(Group(root.transform, "지붕"));

            int tri = 0, rend = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) { rend++; tri += mf.sharedMesh.triangles.Length / 3; }

            Selection.activeGameObject = root;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("[관아] 문서고를 지었습니다.\n  부재 " + rend + " · 삼각형 " + tri.ToString("N0")
                      + "\n  정면 " + FrontW.ToString("F1") + "m · 측면 " + SideD.ToString("F1")
                      + "m · 용마루 " + RidgeY.ToString("F1") + "m");
        }

        // ── 재질 ──

        /// <summary>동헌이 쓰던 재질을 그대로 쓴다. 두 채가 한 마당에 서니 결이 같아야 한다.</summary>
        private static bool LoadMaterials()
        {
            _wood = Load("MI_KoreanWood_1.001.mat");   // 기둥·도리
            _door = Load("M_Wood_Tile.mat");           // 판문 — 널을 세워 짠 문
            _floor = Load("M_Wood_Tile.mat");          // 마루
            _wall = Load("Donheon_Body.mat");          // 벽
            _stone = Load("M_Stone_Granite.mat");      // 기단 장대석
            _rubble = Load("M_Stone_Rubble.mat");      // 기단 몸통·주춧돌
            _roof = Load("MI_R_Roof1.mat");            // 기와

            if (_wood == null || _wall == null || _stone == null)
            {
                Debug.LogError("[관아] 동헌 재질을 못 찾았습니다: " + MatDir);
                return false;
            }
            if (_rubble == null) _rubble = _stone;
            if (_floor == null) _floor = _wood;
            if (_door == null) _door = _wood;
            if (_roof == null) _roof = _stone;
            return true;
        }

        private static Material Load(string file)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + file);
            if (m == null) Debug.LogWarning("[관아] 재질 없음: " + MatDir + file);
            return m;
        }

        // ── 부재 하나 ──

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>
        /// 상자 하나. 자리는 <b>가운데</b>, 크기는 실제 치수(m).
        /// tile 은 1m 에 무늬를 몇 번 되풀이할지 — 큐브는 여섯 면이 다 UV 0~1 이라
        /// 그냥 늘리면 벽돌 한 장이 12m 짜리가 된다.
        /// </summary>
        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size,
                                      Material mat, Quaternion? rot = null, float tile = 0.5f)
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
                float a = Mathf.Max(size.x, size.z), b = Mathf.Max(size.y, Mathf.Min(size.x, size.z));
                var st = new Vector4(Mathf.Max(1f, a * tile), Mathf.Max(1f, b * tile), 0f, 0f);
                var mpb = new MaterialPropertyBlock();
                mpb.SetVector(BaseMapSt, st);
                mpb.SetVector(MainTexSt, st);
                r.SetPropertyBlock(mpb);
            }
            // 콜라이더는 붙이지 않는다. 관아 전체를 한 번에 두르기로 되어 있다.
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }

        // ── 짓기 ──

        private static void BuildGidan(Transform g)
        {
            float w = FrontW + GidanOut * 2f, d = SideD + GidanOut * 2f;
            Box(g, "기단_몸", new Vector3(0f, GidanH * 0.5f, 0f), new Vector3(w, GidanH, d), _rubble, null, 0.8f);
            // 장대석 — 기단 윗면 테두리. 돌을 두른 것이 보여야 기단이 흙더미로 안 보인다.
            Box(g, "장대석", new Vector3(0f, GidanH + 0.06f, 0f), new Vector3(w + 0.1f, 0.12f, d + 0.1f), _stone, null, 0.6f);

            // 계단 셋. 앞면 가운데.
            for (int k = 0; k < 3; k++)
            {
                float h = GidanH * (k + 1) / 3f;
                Box(g, "계단_" + k, new Vector3(0f, h * 0.5f, d * 0.5f + 0.55f - k * 0.28f),
                    new Vector3(2.2f, h, 0.56f), _stone, null, 0.8f);
            }
        }

        private static void BuildStones(Transform g)
        {
            foreach (var p in PillarSpots())
                Box(g, "주춧돌", new Vector3(p.x, GidanH + (StoneTop - GidanH) * 0.5f, p.z),
                    new Vector3(0.52f, StoneTop - GidanH, 0.52f), _rubble, null, 1.2f);
        }

        private static void BuildPillars(Transform g)
        {
            float h = EaveY - StoneTop;
            foreach (var p in PillarSpots())
                Box(g, "기둥", new Vector3(p.x, StoneTop + h * 0.5f, p.z),
                    new Vector3(0.30f, h, 0.30f), _wood, null, 1f);
        }

        /// <summary>정면 여섯 줄 × 측면 세 줄. 다섯 칸 × 두 칸이면 기둥은 이렇게 선다.</summary>
        private static System.Collections.Generic.List<Vector3> PillarSpots()
        {
            var list = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i <= 5; i++)
            {
                float x = -FrontW * 0.5f + Bay * i;
                for (int j = 0; j <= 2; j++)
                    list.Add(new Vector3(x, 0f, -SideD * 0.5f + (SideD * 0.5f) * j));
            }
            return list;
        }

        private static void BuildFloor(Transform g)
        {
            Box(g, "우물마루", new Vector3(0f, StoneTop + 0.06f, 0f),
                new Vector3(FrontW, 0.12f, SideD), _floor, null, 0.9f);
        }

        private static void BuildWalls(Transform g)
        {
            float h = EaveY - StoneTop;
            float y = StoneTop + h * 0.5f;
            // 뒤·옆 세 면은 막는다. 문서고는 볕과 비를 꺼려 창을 내지 않는다.
            Box(g, "벽_뒤", new Vector3(0f, y, -SideD * 0.5f), new Vector3(FrontW, h, 0.22f), _wall, null, 0.5f);
            Box(g, "벽_서", new Vector3(-FrontW * 0.5f, y, 0f), new Vector3(0.22f, h, SideD), _wall, null, 0.5f);
            Box(g, "벽_동", new Vector3(FrontW * 0.5f, y, 0f), new Vector3(0.22f, h, SideD), _wall, null, 0.5f);
        }

        private static void BuildDoors(Transform g)
        {
            // 앞면 다섯 칸에 판문. 가운데 칸만 두 짝으로 열리게 갈라 둔다 —
            // 나중에 여닫이를 붙일 자리라 이름을 따로 준다.
            float h = EaveY - StoneTop - 0.25f;
            float y = StoneTop + h * 0.5f;
            float z = SideD * 0.5f;
            for (int i = 0; i < 5; i++)
            {
                float x = -FrontW * 0.5f + Bay * (i + 0.5f);
                if (i == 2)
                {
                    Box(g, "문_가운데_좌", new Vector3(x - Bay * 0.24f, y, z), new Vector3(Bay * 0.46f, h, 0.12f), _door, null, 0.9f);
                    Box(g, "문_가운데_우", new Vector3(x + Bay * 0.24f, y, z), new Vector3(Bay * 0.46f, h, 0.12f), _door, null, 0.9f);
                }
                else
                {
                    Box(g, "문_" + i, new Vector3(x, y, z), new Vector3(Bay * 0.92f, h, 0.12f), _door, null, 0.9f);
                }
            }
            // 인방 — 문 위를 가로지르는 나무. 문과 도리 사이의 틈을 메운다.
            Box(g, "인방", new Vector3(0f, EaveY - 0.12f, z), new Vector3(FrontW, 0.25f, 0.16f), _wood, null, 0.8f);
        }

        private static void BuildBeams(Transform g)
        {
            for (int j = 0; j <= 2; j++)
            {
                float z = -SideD * 0.5f + (SideD * 0.5f) * j;
                Box(g, "도리_" + j, new Vector3(0f, EaveY + 0.12f, z), new Vector3(FrontW + 0.3f, 0.24f, 0.24f), _wood, null, 1f);
            }
            for (int i = 0; i <= 5; i++)
            {
                float x = -FrontW * 0.5f + Bay * i;
                Box(g, "보_" + i, new Vector3(x, EaveY + 0.12f, 0f), new Vector3(0.22f, 0.22f, SideD + 0.2f), _wood, null, 1f);
            }
        }

        private static void BuildRoof(Transform g)
        {
            // 맞배지붕. 앞뒤 두 폭만 세우고 옆은 박공널로 막는다 —
            // 우진각으로 네 물매를 다 세우면 상자끼리 엇갈린다(조사청에서 겪은 것).
            float halfZ = SideD * 0.5f;
            float rise = RidgeY - EaveY;
            float y = (RidgeY + EaveY) * 0.5f;

            float runZ = halfZ + Overhang;
            float slopeZ = Mathf.Sqrt(runZ * runZ + rise * rise);
            float ang = Mathf.Atan2(rise, runZ) * Mathf.Rad2Deg;
            float widthX = FrontW + Overhang * 2f;

            Box(g, "지붕_남", new Vector3(0f, y, runZ * 0.5f), new Vector3(widthX, RoofThick, slopeZ),
                _roof, Quaternion.Euler(ang, 0f, 0f), 0.9f);
            Box(g, "지붕_북", new Vector3(0f, y, -runZ * 0.5f), new Vector3(widthX, RoofThick, slopeZ),
                _roof, Quaternion.Euler(ang, 180f, 0f), 0.9f);

            Box(g, "용마루", new Vector3(0f, RidgeY + 0.10f, 0f), new Vector3(widthX, 0.28f, 0.55f), _roof, null, 1f);

            // 박공널 — 지붕 양 끝의 세모. 상자로 세모를 못 만드니 층을 지어 깎는다.
            // 지붕 폭 밖으로 나가면 날개가 돋친 꼴이 되므로 안쪽에 세운다.
            const int Steps = 8;
            for (int side = 0; side < 2; side++)
            {
                float x = (side == 0 ? -1f : 1f) * FrontW * 0.5f;
                var grp = Group(g, side == 0 ? "박공_서" : "박공_동");
                for (int k = 0; k < Steps; k++)
                {
                    float t0 = (float)k / Steps, t1 = (float)(k + 1) / Steps;
                    float yc = EaveY + rise * (t0 + t1) * 0.5f;
                    float depth = 2f * runZ * (1f - (t0 + t1) * 0.5f);
                    if (depth < 0.05f) continue;
                    Box(grp, "박공_" + k, new Vector3(x, yc, 0f),
                        new Vector3(0.14f, rise / Steps + 0.01f, depth), _wall, null, 0.6f);
                }
            }
        }
    }
}
