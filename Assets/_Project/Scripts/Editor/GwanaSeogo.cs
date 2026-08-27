using System.Collections.Generic;
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
    /// 생김새는 문서고답게 잡았다 — 정면 다섯 칸에 측면 두 칸, 기단은 낮다.
    ///
    /// <b>지붕과 문은 상자로 짓지 않는다.</b> 처음엔 맞배지붕을 상자로 세웠는데, 상자로는
    /// 처마가 휘지 않아 판때기를 얹은 꼴이었다. 동헌에 이미 제대로 된 팔작지붕과
    /// 세살문이 있으므로 <b>그것을 떼어 온다</b> — 지붕은 Donheon_Opaque 의 서브메시
    /// 셋(기와·합각·부연, 42,763), 문은 SM_Door_Sesal_r40(한 짝 6,923).
    /// 두 채가 같은 손에서 나온 것처럼 보이는 것은 실제로 같은 물건이기 때문이다.
    ///
    /// 문은 <b>가운데 세 칸에만</b> 두 짝씩 단다. 다섯 칸에 다 달면 열 짝 69,230 이라
    /// 지붕값을 넘는다. 양 끝 두 칸은 판벽으로 막는데, 문서고는 볕과 비를 꺼려
    /// 어차피 문을 적게 내는 집이다.
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
        private const float EaveY = 3.30f;                    // 처마(도리) 높이 — 지붕 밑면이 여기 앉는다

        /// <summary>
        /// 동헌 지붕을 서고에 맞추는 배율. 동헌 지붕은 18.689 x 9.891m 인데
        /// 정면 12m + 처마 0.95m x2 = 13.9m 에 맞추면 0.7437 이다. 그때 측면이 7.36m 라
        /// 몸통 6m 에 처마가 0.68m 씩 나온다 — 따로 맞출 것도 없이 떨어진다.
        /// 가로세로를 따로 눌러 맞추지 않는 까닭은 처마 곡선과 합각이 일그러지기 때문이다.
        /// </summary>
        private const float RoofScale = 0.7437f;

        private const string DonheonPrefab = "Assets/_Project/_Common/Sets/Gwana/Prefabs/PF_Donheon.prefab";
        private const string RoofMeshPath = "Assets/_Project/_Common/Sets/Gwana/Donheon/Meshes/SM_동헌지붕.asset";

        /// <summary>Donheon_Opaque 에서 지붕에 해당하는 서브메시 — 기와·합각·부연.</summary>
        private static readonly int[] RoofSubmeshes = { 5, 6, 7 };

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
                      + "m · 처마 " + EaveY.ToString("F1") + "m (지붕은 동헌 것을 " + RoofScale.ToString("F3") + " 로 줄였다)");
        }

        // ── 재질 ──

        /// <summary>동헌이 쓰던 재질을 그대로 쓴다. 두 채가 한 마당에 서니 결이 같아야 한다.</summary>
        private static bool LoadMaterials()
        {
            _wood = Load("MI_KoreanWood_1.001.mat");   // 기둥·도리
            _door = Load("M_Wood_Tile.mat");           // 판문 — 널을 세워 짠 문
            _floor = Load("M_Wood_Tile.mat");          // 마루
            // 벽에 Donheon_Body 를 썼다가 물렀다. 그것은 <b>아틀라스</b>다
            // (Donheon_Atlas_Albedo_final). 아틀라스는 한 장에 여러 부재의 껍질이 모여 있어서,
            // 무늬를 되풀이시키면 남의 부재가 벽에 끌려 들어온다 — 벽 한 면이 알록달록한
            // 조각보가 되었다. 되풀이시켜도 되는 것은 이음매가 물리는 낱장 텍스처뿐이다.
            _wall = Load("MI_R_BrickConcrete1.mat");   // 벽 — 동헌 합각벽이 쓰던 것
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
            float sill = StoneTop + 0.12f;                 // 마루 윗면 — 문이 여기 선다
            float z = SideD * 0.5f;

            var (leaf, leafMats, leafRot) = LoadDonheonPart("SM_Door_Sesal_r40");
            float leafW = 1.124f, leafH = 1.858f;

            for (int i = 0; i < 5; i++)
            {
                float x = -FrontW * 0.5f + Bay * (i + 0.5f);

                // 양 끝 두 칸은 판벽. 문을 다섯 칸에 다 달면 열 짝 69,230 이라 지붕값을 넘는다.
                if (i == 0 || i == 4 || leaf == null)
                {
                    float h = EaveY - sill - 0.25f;
                    Box(g, "판벽_" + i, new Vector3(x, sill + h * 0.5f, z),
                        new Vector3(Bay * 0.96f, h, 0.12f), _door, null, 0.9f);
                    continue;
                }

                // 가운데 세 칸 — 두 짝씩. 한 칸 2.4m 에 두 짝 2.25m 라 좌우로 조금 남는다.
                for (int k = 0; k < 2; k++)
                {
                    float dx = x + (k == 0 ? -1f : 1f) * leafW * 0.5f;
                    PlaceDonheonPart(g, "문_" + i + "_" + (k == 0 ? "좌" : "우"), leaf, leafMats, leafRot,
                                     new Vector3(dx, sill + leafH * 0.5f, z));
                }

                // 문 위 — 문(1.858m)이 칸 높이(2.48m)보다 낮아 그만큼 빈다. 벽으로 메운다.
                float topY = sill + leafH;
                float gap = EaveY - 0.25f - topY;
                if (gap > 0.05f)
                    Box(g, "문위벽_" + i, new Vector3(x, topY + gap * 0.5f, z),
                        new Vector3(Bay * 0.96f, gap, 0.12f), _wall, null, 0.5f);
            }

            // 인방 — 문 위를 가로지르는 나무. 문과 도리 사이의 틈을 막는다.
            Box(g, "인방", new Vector3(0f, EaveY - 0.12f, z), new Vector3(FrontW, 0.25f, 0.16f), _wood, null, 0.8f);
        }

        // ── 동헌에서 떼어 오기 ──

        /// <summary>
        /// 동헌 프리팹에서 부재 하나를 찾아 메시·재질·자세를 돌려준다.
        ///
        /// 자세를 그대로 들고 오는 까닭: 동헌 FBX 는 부재마다 원점이 제각각이고
        /// (문 하나가 로컬 (4.570, -0.860, 2.441) 에 있다) 축도 돌아가 있다(270, 270, 0).
        /// 그 값을 손으로 풀어 쓰느니 <b>회전은 원본을 베끼고 자리는 bounds 로 맞추는</b> 편이
        /// 안전하다 — 나중에 동헌 FBX 가 새로 와도 이 코드는 그대로 산다.
        /// </summary>
        private static (Mesh, Material[], Quaternion) LoadDonheonPart(string partName)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(DonheonPrefab);
            if (pf == null) { Debug.LogWarning("[관아] 동헌 프리팹이 없습니다: " + DonheonPrefab); return (null, null, Quaternion.identity); }
            foreach (var t in pf.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != partName) continue;
                var mf = t.GetComponent<MeshFilter>();
                var r = t.GetComponent<Renderer>();
                if (mf == null || mf.sharedMesh == null || r == null) break;
                return (mf.sharedMesh, r.sharedMaterials, t.rotation);
            }
            Debug.LogWarning("[관아] 동헌에서 '" + partName + "' 을 못 찾았습니다.");
            return (null, null, Quaternion.identity);
        }

        /// <summary>
        /// 떼어 온 부재를 놓는다. 자리는 <b>렌더러 bounds 의 가운데</b>로 맞춘다 —
        /// 메시 원점이 어디에 있든 상관없어진다.
        /// </summary>
        private static GameObject PlaceDonheonPart(Transform parent, string name, Mesh mesh,
                                                   Material[] mats, Quaternion rot, Vector3 localCenter,
                                                   float scale = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = rot;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;

            // 문서고 뿌리는 돌아가 있지도 늘어나 있지도 않으므로 월드 어긋남이 곧 로컬 어긋남이다.
            var b = r.bounds;
            go.transform.position += (Where + localCenter) - b.center;
            return go;
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
            var mesh = EnsureRoofMesh(out var mats, out var rot);
            if (mesh == null)
            {
                Debug.LogWarning("[관아] 동헌 지붕을 못 떼어 왔습니다. 지붕 없이 세웁니다.");
                return;
            }

            // 동헌 지붕과 같은 자세로 세운 뒤, 긴 축을 서고 정면(X)으로 눕힌다.
            var go = new GameObject("지붕");
            go.transform.SetParent(g, false);
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * rot;
            go.transform.localScale = Vector3.one * RoofScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;

            // 밑면을 처마 높이에 앉힌다. 지붕 메시의 원점이 어디든 bounds 로 맞추면 된다.
            var b = r.bounds;
            var want = new Vector3(Where.x, EaveY + b.size.y * 0.5f, Where.z);
            go.transform.position += want - b.center;
        }

        /// <summary>
        /// 동헌 지붕만 따로 구운 메시를 찾아 준다. 없으면 그 자리에서 굽는다.
        ///
        /// 왜 미리 구워 두지 않나: 이 메시는 <b>동헌 FBX 에서 뽑아낸 것</b>이라 아트다.
        /// .gitignore 가 Sets/Gwana 밑의 asset 을 걷어내므로 저장소에서 받아만 봐서는 없다.
        /// 그러니 없으면 만든다 — 원본(동헌 FBX)은 공유폴더로 오니 그것만 있으면 산다.
        ///
        /// 뽑는 법: Donheon_Opaque 는 서브메시 여덟 장짜리 한 덩이인데 그중 다섯째~일곱째가
        /// 기와·합각·부연이다. 그 셋의 인덱스만 가져오고 쓰는 정점만 추려 다시 번호를 매긴다
        /// (통째로 베끼면 정점 13만을 다 들고 오게 된다 — 지붕이 쓰는 것은 9만 6천이다).
        /// </summary>
        private static Mesh EnsureRoofMesh(out Material[] mats, out Quaternion rot)
        {
            mats = null; rot = Quaternion.identity;

            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(DonheonPrefab);
            if (pf == null) { Debug.LogWarning("[관아] 동헌 프리팹이 없습니다: " + DonheonPrefab); return null; }

            Transform opaque = null;
            foreach (var t in pf.GetComponentsInChildren<Transform>(true))
                if (t.name == "Donheon_Opaque") { opaque = t; break; }
            if (opaque == null) { Debug.LogWarning("[관아] 동헌에서 Donheon_Opaque 를 못 찾았습니다."); return null; }

            var srcR = opaque.GetComponent<Renderer>();
            var src = opaque.GetComponent<MeshFilter>().sharedMesh;
            rot = opaque.rotation;

            var sm = srcR.sharedMaterials;
            mats = new Material[RoofSubmeshes.Length];
            for (int i = 0; i < RoofSubmeshes.Length; i++)
                mats[i] = RoofSubmeshes[i] < sm.Length ? sm[RoofSubmeshes[i]] : null;

            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(RoofMeshPath);
            if (cached != null) return cached;

            if (!src.isReadable) { Debug.LogWarning("[관아] 동헌 메시를 읽을 수 없습니다(Read/Write 꺼짐)."); return null; }

            var map = new Dictionary<int, int>();
            var vs = src.vertices; var ns = src.normals; var uv = src.uv; var tg = src.tangents;
            var nv = new List<Vector3>(); var nn = new List<Vector3>();
            var nu = new List<Vector2>(); var nt = new List<Vector4>();
            var subs = new List<int[]>();

            foreach (int si in RoofSubmeshes)
            {
                if (si >= src.subMeshCount) continue;
                var tri = src.GetTriangles(si);
                var outT = new int[tri.Length];
                for (int k = 0; k < tri.Length; k++)
                {
                    int o = tri[k];
                    if (!map.TryGetValue(o, out int n))
                    {
                        n = nv.Count; map[o] = n;
                        nv.Add(vs[o]);
                        nn.Add(ns.Length > o ? ns[o] : Vector3.up);
                        nu.Add(uv.Length > o ? uv[o] : Vector2.zero);
                        nt.Add(tg.Length > o ? tg[o] : new Vector4(1f, 0f, 0f, 1f));
                    }
                    outT[k] = n;
                }
                subs.Add(outT);
            }

            var m = new Mesh { name = "SM_동헌지붕" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;   // 정점 9만 6천 — 16비트로는 못 담는다
            m.SetVertices(nv); m.SetNormals(nn); m.SetUVs(0, nu); m.SetTangents(nt);
            m.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
            m.RecalculateBounds();

            string dir = System.IO.Path.GetDirectoryName(RoofMeshPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(dir).Replace('\\', '/'),
                                           System.IO.Path.GetFileName(dir));
            AssetDatabase.CreateAsset(m, RoofMeshPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[관아] 동헌 지붕을 떼어 구웠습니다 — 삼각형 " + (m.triangles.Length / 3).ToString("N0")
                      + " · 정점 " + m.vertexCount.ToString("N0") + "  ·  " + RoofMeshPath);
            return m;
        }
    }
}
