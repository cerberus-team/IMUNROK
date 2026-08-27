using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static IMUNROK.Gyeonu.Editor.GwanaLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 씬 식생·소품 배치 + 지형 디테일(잔디·들꽃) 도색. 멱등 — 그룹을 지우고 재생성한다.
    ///
    /// 레퍼런스(순천 낙안읍성 관아 항공사진) 해석:
    ///   · 마당 안에는 아름드리 활엽수 몇 그루만. 나머지는 비운다 — 관아 마당은 정돈된 공간이다
    ///   · 담장 바깥은 나무가 빙 두르고, 담장 밑동에만 낮은 식생
    ///   · 문 앞 길가에 선정비(불망비) 열이 늘어선다
    ///
    /// 배치 원칙:
    ///   · 진입축(어도 ±3.5m)과 월대·동헌 위는 절대 비운다 — 동헌 정면 시야를 가리면 안 된다
    ///   · 큰 나무는 좌우 비대칭으로 어긋나게 (대칭이면 인공적으로 읽힌다)
    ///   · 바깥 나무는 균등 산포가 아니라 군락 중심 + 가우시안 산포 ("덩어리로 뭉치게")
    ///   · 흑송JG 제외 (사용자 지정)
    ///
    /// 트리 콜라이더: 팩 프리팹에 MeshCollider가 하나씩 들어 있는데 가지까지 물려 끼임을 만든다.
    /// 전부 끄고 밑동에 BoxCollider만 새로 붙인다 (프로젝트 규칙: MeshCollider 금지).
    /// </summary>
    public static class GwanaDecorPlanter
    {
        const int Seed = 20260817;
        const string VegDir = "Assets/_Project/Gyeonu/Prefabs/Vegetation/";
        const string VegGroup = "관아_식생";
        const string PropGroup = "관아_소품";
        const string MeshDir = "Assets/_Project/Gyeonu/Art/Models/Gwana";

        static System.Random _rnd;
        static float R(float a, float b) => Mathf.Lerp(a, b, (float)_rnd.NextDouble());
        /// <summary>−1~1 근처에 몰리는 난수 (군락 산포용).</summary>
        static float Gauss() => (float)((_rnd.NextDouble() + _rnd.NextDouble() + _rnd.NextDouble()) / 1.5 - 1.0);

        // 수종 — (프리팹, 밑동 콜라이더 반폭, 스케일 하한, 상한)
        struct Sp { public string p; public float trunk, s0, s1; public Sp(string p, float t, float a, float b) { this.p = p; trunk = t; s0 = a; s1 = b; } }

        static readonly Sp Oak = new Sp("Oak_상수리_당산나무용_Quercus_2_VR", 1.15f, 0.85f, 1.05f);
        static readonly Sp Elm = new Sp("Elm_느릅_Ulmus_2_VR", 0.62f, 0.90f, 1.15f);
        static readonly Sp[] Pines =
        {
            new Sp("Pine_소나무_PinusDensiflora_1_VR", 0.26f, 0.95f, 1.55f),
            new Sp("Pine_소나무_PinusDensiflora_2_VR", 0.26f, 0.95f, 1.55f),
            new Sp("Pine_곰솔_PinusThunbergii_1_VR", 0.24f, 1.00f, 1.60f),
            new Sp("Pine_곰솔_PinusThunbergii_2_VR", 0.28f, 0.95f, 1.45f),
        };
        static readonly Sp[] Cherries =
        {
            new Sp("Cherry_벚나무JG_대_01", 0.34f, 0.85f, 1.15f),
            new Sp("Cherry_벚나무JG_대_05", 0.34f, 0.85f, 1.15f),
            new Sp("Cherry_벚나무JG_중_01", 0.26f, 0.90f, 1.20f),
            new Sp("Cherry_벚나무JG_중_05", 0.26f, 0.90f, 1.20f),
        };
        static readonly Sp[] Bamboos =
        {
            new Sp("Bamboo_왕대JG_중_01", 0f, 0.9f, 1.3f),
            new Sp("Bamboo_왕대JG_중_02", 0f, 0.9f, 1.3f),
            new Sp("Bamboo_왕대JG_대_01", 0f, 0.9f, 1.2f),
        };
        static readonly string[] LowPlants =
        {
            "Bush_회양목JG_군락_01", "Bush_회양목JG_군락_02", "Bush_회양목JG_군락_03",
            "Bush_회양목JG_한그루_01", "Bush_회양목JG_한그루_02",
            "Fern_고사리JG_군락_01", "Fern_고사리JG_군락_02", "Fern_고사리JG_한그루_01",
        };

        [MenuItem("Tools/이문록/관아 ▸ ③ 식생·소품 배치")]
        public static void PlantAll()
        {
            _rnd = new System.Random(Seed);
            _trunks.Clear();
            var veg = RecreateGroup(VegGroup).transform;
            var props = RecreateGroup(PropGroup).transform;

            int nCourt = PlantCourtyardTrees(veg);
            int nRing = PlantWallRing(veg);
            int nRoad = PlantRoadside(veg);
            int nLow = PlantLowGrowth(veg);
            int nProp = BuildProps(props);
            PaintTerrainDetail();

            int tris = 0;
            foreach (var mf in veg.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) tris += (int)(mf.sharedMesh.GetIndexCount(0) / 3);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[관아] 식생 {nCourt + nRing + nRoad + nLow}그루 (마당 {nCourt} / 담장밖 {nRing} / 언덕길 {nRoad} / 낮은식생 {nLow}), " +
                      $"소품 {nProp}점, 전체 LOD 합산 {tris:N0} 삼각형");
        }

        // ── 마당 안 큰 나무 ───────────────────────────────────
        // 레퍼런스처럼 아름드리 몇 그루만. 어도(±3.5)와 월대·동헌 정면 시야는 비운다.
        static int PlantCourtyardTrees(Transform parent)
        {
            var g = Child(parent, "마당_큰나무");
            // 서쪽 4 / 동쪽 3 — 그루 수까지 어긋나게 둔다. 좌우 같은 수·같은 거리면
            // 아무리 위치를 흔들어도 대칭으로 읽힌다(2026-08-17 지적).
            Plant(g, Oak, -11.5f, -2.0f);
            Plant(g, Elm, -12.9f, -21.0f);
            Plant(g, Elm, -13.9f, -28.8f);
            Plant(g, Cherries[2], -13.7f, 11.0f);   // 월대 옆 (동헌 처마 밖)
            Plant(g, Oak, 12.4f, -18.5f);
            Plant(g, Elm, 13.1f, 2.5f);
            Plant(g, Cherries[3], 13.0f, 16.8f);
            return g.childCount;
        }

        // ── 담장 바깥 둘레 ───────────────────────────────────
        static int PlantWallRing(Transform parent)
        {
            var g = Child(parent, "담장밖_숲");
            // 담장 사각을 따라 군락 중심을 돌리고, 중심마다 3~8그루를 가우시안으로 흩는다
            var centers = new List<Vector2>();
            for (float t = 0f; t < 1f; t += 1f / 26f)
            {
                var p = WallRectPoint(t);
                float outward = R(6f, 24f);
                centers.Add(p + WallRectNormal(t) * outward);
            }
            int n = 0;
            foreach (var c in centers)
            {
                int k = _rnd.Next(3, 9);
                for (int i = 0; i < k; i++)
                {
                    float x = c.x + Gauss() * 6.5f, z = c.y + Gauss() * 6.5f;
                    if (!OpenGround(x, z)) continue;
                    if (Plant(g, PickOuter(), x, z)) n++;
                }
            }
            // 보행 경계 바로 밖을 숲으로 둘러 투명벽을 자연스럽게 읽히게 한다
            for (int i = 0; i < 130; i++)
            {
                var e = PlayRectPoint((float)_rnd.NextDouble());
                var nrm = PlayRectNormal(e);
                float push = R(1.5f, 34f);
                float x = e.x + nrm.x * push + Gauss() * 5f;
                float z = e.y + nrm.y * push + Gauss() * 5f;
                if (InsidePlay(x, z)) continue;                       // 보행 구역 안엔 심지 않는다
                if (Plant(g, PickOuter(), x, z)) n++;
            }
            return n;
        }

        // ── 언덕길 양옆 ──────────────────────────────────────
        static int PlantRoadside(Transform parent)
        {
            var g = Child(parent, "언덕길_숲");
            int n = 0;
            for (float z = -78f; z <= -38f; z += 3.2f)
                foreach (float s in new[] { -1f, 1f })
                {
                    int k = _rnd.Next(1, 4);
                    for (int i = 0; i < k; i++)
                    {
                        float x = s * R(8f, 27f) + Gauss() * 2.5f;
                        float zz = z + Gauss() * 2.2f;
                        if (!OpenGround(x, zz)) continue;
                        Sp sp = _rnd.NextDouble() < 0.5 ? Pick(Pines)
                              : _rnd.NextDouble() < 0.5 ? Pick(Cherries) : Pick(Bamboos);
                        if (Plant(g, sp, x, zz)) n++;
                    }
                }
            return n;
        }

        // ── 낮은 식생 (담장 밑·구석) ─────────────────────────
        static int PlantLowGrowth(Transform parent)
        {
            var g = Child(parent, "낮은식생");
            int n = 0;
            // 마당 안: 네 귀퉁이 담장 밑에만 조금 — 마당 한복판은 비운다
            var corners = new[]
            {
                new Vector2(-14.6f, -31.4f), new Vector2(14.6f, -31.4f),
                new Vector2(-14.6f, 24.4f), new Vector2(14.6f, 24.4f),
            };
            foreach (var c in corners)
                for (int i = 0; i < 5; i++)
                {
                    float x = c.x + Gauss() * 1.7f, z = c.y + Gauss() * 2.6f;
                    if (Mathf.Abs(x) > 16.1f || z < -32.8f || z > 25.8f) continue;
                    if (PlantLow(g, x, z)) n++;
                }
            // 담장 바깥 밑동을 따라
            for (float t = 0f; t < 1f; t += 1f / 120f)
            {
                if (_rnd.NextDouble() > 0.55) continue;
                var p = WallRectPoint(t) + WallRectNormal(t) * R(1.3f, 4.5f);
                if (!OpenGround(p.x, p.y)) continue;
                if (PlantLow(g, p.x, p.y)) n++;
            }
            return n;
        }

        static bool PlantLow(Transform g, float x, float z)
        {
            var prefab = Load(LowPlants[_rnd.Next(LowPlants.Length)]);
            if (prefab == null) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
            go.transform.position = new Vector3(x, GwanaBuilder.SampleTerrainH(x, z) - 0.03f, z);
            go.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * R(0.8f, 1.5f);
            StripColliders(go);
            MarkStatic(go);
            return true;
        }

        // ── 나무 한 그루 ─────────────────────────────────────
        /// 콜라이더가 붙는 나무들의 (x, z, 밑동 반지름). 서로 너무 붙으면 캡슐이 끼는 슬롯이 된다.
        static readonly List<Vector3> _trunks = new List<Vector3>();

        static bool Plant(Transform g, Sp sp, float x, float z)
        {
            var prefab = Load(sp.p);
            if (prefab == null) { Debug.LogWarning("[관아] 프리팹 없음: " + sp.p); return false; }

            bool walkable = sp.trunk > 0f && Mathf.Abs(x) <= PlayHalfX + 3f && z >= PlayZS - 3f && z <= PlayZN + 3f;
            float s = R(sp.s0, sp.s1);
            if (walkable)
            {
                // 밑동 사이 통행 폭 확보 — 두 그루가 붙으면 그 틈에 캡슐이 낀다.
                // 캡슐 유효 지름은 0.76(반지름 0.3 + 스킨 0.08)이라 최소 1m는 띄운다.
                // 밑동이 박스라 반지름이 아니라 **대각 반지름**(×1.42)으로 재야 실제 틈이 나온다.
                float r = sp.trunk * s * 1.42f;
                foreach (var t in _trunks)
                {
                    float d = Vector2.Distance(new Vector2(x, z), new Vector2(t.x, t.y));
                    if (d < r + t.z + 1.0f) return false;
                }
                _trunks.Add(new Vector3(x, z, r));
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
            go.transform.position = new Vector3(x, GwanaBuilder.SampleTerrainH(x, z) - 0.08f, z);
            go.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            go.transform.localScale = new Vector3(s * R(0.94f, 1.06f), s, s * R(0.94f, 1.06f));

            StripColliders(go);
            // 밑동만 박스로 — 보행 구역 안쪽 나무에만 (바깥은 경계벽이 막는다)
            if (walkable)
            {
                var col = new GameObject("밑동");
                col.transform.SetParent(go.transform, false);
                col.transform.localPosition = new Vector3(0f, 1.5f / s, 0f);
                col.AddComponent<BoxCollider>().size =
                    new Vector3(sp.trunk * 2f / s, 3f / s, sp.trunk * 2f / s);
            }
            MarkStatic(go);
            return true;
        }

        static Sp Pick(Sp[] a) => a[_rnd.Next(a.Length)];

        /// <summary>
        /// 담장 밖·경계 숲 수종. ⚠️ 사시나무(Aspen)는 이 씬에서 쓰지 않는다 —
        /// LOD도 빌보드도 없는 "원경 전용"이라 40~60m에서도 잎이 성겨 흰 장대로 보인다.
        /// 이 씬은 바깥 숲이 20~60m 거리에 있어 전부 시야에 들어온다.
        /// 벚나무는 빌보드 LOD가 있어 멀리서 오히려 사시나무보다 싸다.
        /// </summary>
        static Sp PickOuter()
        {
            double r = _rnd.NextDouble();
            if (r < 0.52) return Pick(Cherries);
            if (r < 0.88) return Pick(Pines);
            return Elm;
        }
        static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(VegDir + name + ".prefab");

        /// <summary>팩 MeshCollider는 가지까지 물려 캡슐이 낀다 — 전부 제거한다.</summary>
        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c, true);
        }

        static void MarkStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
        }

        // ── 배치 금지 구역 ───────────────────────────────────
        /// <summary>구조물·통로를 피한 빈 땅인가.</summary>
        static bool OpenGround(float x, float z)
        {
            float ax = Mathf.Abs(x);
            if (ax > 96f || z < -96f || z > 96f) return false;
            // 담장 안(마당)은 전용 배치만 허용
            if (ax < 17.8f && z > -34.8f && z < 27.8f) return false;
            // 담장 몸통 주변 여유 (밑동 반지름 최대 0.62 → 통행 폭 1.9m 확보)
            if (WallRectDist(x, z) < 2.6f) return false;
            // ⚠️ 보행 경계(투명벽)에 밀착한 나무는 벽과 밑동 사이에 0.3m 포켓을 만든다
            //    (Play 검증에서 실제로 캡슐이 끼었다). 경계선 ±2.4m 띠는 비운다.
            if (Mathf.Abs(PlayRectSD(x, z)) < 2.4f) return false;
            // 외삼문 앞뒤 통로
            if (ax < 10.5f && z > -41f && z < -27f) return false;
            // 진입로(흙길) 위
            if (ax < 6.5f && z < -34f) return false;
            // 문 앞 선정비·깃대 자리
            if (ax > 6.5f && ax < 11.5f && z > -52f && z < -36f) return false;
            return true;
        }

        /// <summary>담장 사각 둘레 위의 점 (t 0~1).</summary>
        static Vector2 WallRectPoint(float t)
        {
            float w = 2f * WallHalfX, d = WallZN - WallZS, per = 2f * (w + d);
            float s = t * per;
            if (s < w) return new Vector2(-WallHalfX + s, WallZS);
            s -= w;
            if (s < d) return new Vector2(WallHalfX, WallZS + s);
            s -= d;
            if (s < w) return new Vector2(WallHalfX - s, WallZN);
            return new Vector2(-WallHalfX, WallZN - (s - w));
        }

        static Vector2 WallRectNormal(float t)
        {
            var p = WallRectPoint(t);
            return new Vector2(
                Mathf.Abs(p.x) >= WallHalfX - 0.01f ? Mathf.Sign(p.x) : 0f,
                Mathf.Abs(p.x) >= WallHalfX - 0.01f ? 0f : (p.y > 0f ? 1f : -1f)).normalized;
        }

        /// <summary>보행 경계 사각까지의 부호 거리 (안쪽 음수 / 바깥 양수).</summary>
        static float PlayRectSD(float x, float z)
        {
            float dx = Mathf.Abs(x) - PlayHalfX;
            float dz = Mathf.Max(PlayZS - z, z - PlayZN);
            if (dx <= 0f && dz <= 0f) return Mathf.Max(dx, dz);
            return Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dz, 0f) * Mathf.Max(dz, 0f));
        }

        static bool InsidePlay(float x, float z) =>
            Mathf.Abs(x) < PlayHalfX + 1.5f && z > PlayZS - 1.5f && z < PlayZN + 1.5f;

        /// <summary>보행 경계 사각 둘레 위의 점 (t 0~1).</summary>
        static Vector2 PlayRectPoint(float t)
        {
            float w = 2f * PlayHalfX, d = PlayZN - PlayZS, per = 2f * (w + d);
            float s = t * per;
            if (s < w) return new Vector2(-PlayHalfX + s, PlayZS);
            s -= w;
            if (s < d) return new Vector2(PlayHalfX, PlayZS + s);
            s -= d;
            if (s < w) return new Vector2(PlayHalfX - s, PlayZN);
            return new Vector2(-PlayHalfX, PlayZN - (s - w));
        }

        static Vector2 PlayRectNormal(Vector2 p) =>
            Mathf.Abs(p.x) >= PlayHalfX - 0.01f ? new Vector2(Mathf.Sign(p.x), 0f)
                                                : new Vector2(0f, p.y > 0f ? 1f : -1f);

        /// <summary>담장 사각 바깥으로의 거리 (안쪽이면 0).</summary>
        static float WallRectDist(float x, float z)
        {
            float dx = Mathf.Abs(x) - WallHalfX;
            float dz = Mathf.Max(WallZS - z, z - WallZN);
            if (dx <= 0f && dz <= 0f) return 0f;
            return Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dz, 0f) * Mathf.Max(dz, 0f));
        }

        // ── 소품 (전부 절차 생성) ────────────────────────────
        // 팩 4종 + 운현궁·KT소품·조선서고를 전수 조사했으나 관아 야외 기물
        // (형틀·신문고·깃대·석등·하마비·선정비)이 하나도 없다. 동헌 재질 복제본으로 만든다.
        static int BuildProps(Transform parent)
        {
            var gran = new GwanaMeshKit(GwanaMeshKit.MpuGranite);
            var rub = new GwanaMeshKit(GwanaMeshKit.MpuRubble);
            var wood = new GwanaMeshKit(GwanaMeshKit.MpuWood);
            var cols = new GameObject("소품_콜라이더");
            cols.transform.SetParent(parent, false);
            System.Action<string, Vector3, Vector3> Col = (nm, c, s) =>
            {
                var go = new GameObject(nm);
                go.transform.SetParent(cols.transform, false);
                go.transform.position = c;
                go.AddComponent<BoxCollider>().size = s;
            };
            int n = 0;

            // 지면에 미세 기복이 들어와서(2026-08-17) 소품도 그 자리 지면 높이를 따라간다.
            System.Func<float, float, float> G = (px, pz) => GwanaBuilder.SampleTerrainH(px, pz);

            // ① 선정비군 — 레퍼런스 사진처럼 문 밖 길가에 늘어선 비석 열.
            //    간격·좌우 어긋남을 크게 줘서 자로 잰 열로 보이지 않게 한다
            const float szStart = -38.2f;
            float sz = szStart;
            for (int i = 0; i < 7; i++)
            {
                sz -= R(1.25f, 1.7f);
                float x = -8.6f + R(-0.3f, 0.3f);
                float g = G(x, sz);
                float h = R(1.05f, 1.6f), w = R(0.34f, 0.48f);
                rub.BoxMinMax(x - w * 0.9f, x + w * 0.9f, g - 0.28f, g + 0.22f, sz - 0.34f, sz + 0.34f);
                gran.BoxMinMax(x - w * 0.5f, x + w * 0.5f, g + 0.22f, g + 0.22f + h, sz - 0.14f, sz + 0.14f);
                gran.BoxMinMax(x - w * 0.62f, x + w * 0.62f, g + 0.22f + h, g + 0.36f + h, sz - 0.20f, sz + 0.20f);
                n++;
            }
            // 비석 간격이 좁아 낱개로 막으면 사이에 슬롯이 남아 캡슐이 낀다 →
            // 열 전체를 한 덩어리로 막는다 (실제로도 비석 사이로는 지나다니지 않는다)
            // ⚠️ 길이는 |szStart − sz| — 부호를 놓치면 87m짜리 벽이 되어 마당 서쪽을 통째로 막는다
            //    (Play 검증에서 실제로 그랬다)
            Col("선정비열", new Vector3(-8.6f, 0.95f, (szStart + sz) * 0.5f),
                new Vector3(1.45f, 2.6f, Mathf.Abs(szStart - sz) + 1.2f));
            // ② 하마비 — 길 동쪽, 문에서 조금 떨어져
            {
                float x = 7.9f, z = -49.4f, g = G(x, z);
                rub.BoxMinMax(x - 0.5f, x + 0.5f, g - 0.34f, g + 0.26f, z - 0.5f, z + 0.5f);
                gran.BoxMinMax(x - 0.24f, x + 0.24f, g + 0.26f, g + 2.05f, z - 0.13f, z + 0.13f);
                gran.BoxMinMax(x - 0.34f, x + 0.34f, g + 2.05f, g + 2.24f, z - 0.22f, z + 0.22f);
                Col("하마비", new Vector3(x, g + 1.12f, z), new Vector3(1.05f, 2.5f, 1.05f));
                n++;
            }
            // ③ 깃대 2기 — 외삼문 좌우 밖. 좌우 위치·높이를 다르게 (완전 미러는 인공적으로 읽힌다)
            float[] fx = { -9.4f, 10.3f }, fz = { -37.1f, -38.2f }, fh = { 6.5f, 5.7f };
            for (int i = 0; i < 2; i++)
            {
                float x = fx[i], z = fz[i], g = G(x, z);
                rub.BoxMinMax(x - 0.62f, x + 0.62f, g - 0.45f, g + 0.34f, z - 0.62f, z + 0.62f);
                gran.BoxMinMax(x - 0.40f, x + 0.40f, g + 0.34f, g + 0.62f, z - 0.40f, z + 0.40f);
                wood.Cylinder(new Vector3(x, g + 0.62f, z), 0.11f, 0.07f, fh[i], 10);
                Col("깃대", new Vector3(x, g + 1.6f, z), new Vector3(1.3f, 3.4f, 1.3f));
                n++;
            }
            // ④ 정료대 2기 — 월대 계단 앞 좌우 (야간 관솔불 받침). 이것도 좌우를 어긋나게
            float[] bx = { -6.6f, 5.9f }, bz = { 2.9f, 3.7f }, bw = { 0.31f, 0.27f }, bh = { 1.20f, 1.06f };
            for (int i = 0; i < 2; i++)
            {
                float x = bx[i], z = bz[i], g = G(x, z);
                rub.BoxMinMax(x - 0.62f, x + 0.62f, g - 0.30f, g + 0.30f, z - 0.62f, z + 0.62f);
                gran.BoxMinMax(x - bw[i], x + bw[i], g + 0.30f, g + bh[i], z - bw[i], z + bw[i]);
                gran.BoxMinMax(x - 0.58f, x + 0.58f, g + bh[i], g + bh[i] + 0.16f, z - 0.58f, z + 0.58f);
                Col("정료대", new Vector3(x, g + 0.73f, z), new Vector3(1.30f, 1.46f, 1.30f));
                n++;
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(MeshDir));
            Piece(parent, "소품_화강암", Save(gran.Build("소품_화강암"), "소품_화강암"), DonheonPrep.Mat("관아_화강암"));
            Piece(parent, "소품_석축", Save(rub.Build("소품_석축"), "소품_석축"), DonheonPrep.Mat("관아_석축"));
            Piece(parent, "소품_목재", Save(wood.Build("소품_목재"), "소품_목재"), DonheonPrep.Mat("관아_목재"));

            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
            AssetDatabase.SaveAssets();
            return n;
        }

        static void Piece(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Mesh Save(Mesh m, string file)
        {
            string path = MeshDir + "/" + file + ".asset";
            var e = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (e != null) { EditorUtility.CopySerialized(m, e); EditorUtility.SetDirty(e); return e; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ── 지형 디테일 (잔디·들꽃) ──────────────────────────
        /// <summary>마을 TerrainData의 프로토타입 12종을 그대로 등록하고, 담장 밖·언덕에만 뿌린다.
        /// 마당 안과 어도·흙길은 0 — 정돈된 공간이라 풀이 나면 안 된다.</summary>
        [MenuItem("Tools/이문록/관아 ▸ 지형 디테일 도색")]
        public static void PaintTerrainDetail()
        {
            var vil = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Gyeonu/Art/Terrain/Gyeonu_TerrainData.asset");
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Gyeonu/Art/Terrain/Gwana_TerrainData.asset");
            if (vil == null || td == null) { Debug.LogError("[관아] TerrainData 없음"); return; }

            if (td.detailScatterMode != vil.detailScatterMode)
            {
                var m = typeof(TerrainData).GetMethod("SetDetailScatterMode");
                if (m != null) m.Invoke(td, new object[] { vil.detailScatterMode });
            }
            td.SetDetailResolution(vil.detailResolution, vil.detailResolutionPerPatch);
            td.detailPrototypes = vil.detailPrototypes;
            td.wavingGrassAmount = vil.wavingGrassAmount;
            td.wavingGrassSpeed = vil.wavingGrassSpeed;
            td.wavingGrassStrength = vil.wavingGrassStrength;
            td.wavingGrassTint = vil.wavingGrassTint;

            int res = td.detailResolution, layers = td.detailPrototypes.Length;
            var maps = new int[layers][,];
            for (int l = 0; l < layers; l++) maps[l] = new int[res, res];

            for (int dz = 0; dz < res; dz++)
            {
                float wz = (dz + 0.5f) / res * 2f * Half - Half;
                for (int dx = 0; dx < res; dx++)
                {
                    float wx = (dx + 0.5f) / res * 2f * Half - Half;
                    if (!GrassAllowed(wx, wz)) continue;

                    float dens = Mathf.PerlinNoise(wx * 0.055f + 3.3f, wz * 0.055f + 7.7f);
                    // 담장에서 멀수록 짙게 (관아 주변은 밟혀 성기다)
                    float near = Mathf.Clamp01(WallRectDist(wx, wz) / 14f);
                    int grass = Mathf.RoundToInt(Mathf.Lerp(10f, 52f, dens) * Mathf.Lerp(0.35f, 1f, near));
                    if (grass <= 0) continue;
                    maps[dz % 2][dz, dx] = grass;          // 잔디 2종을 줄 단위로 번갈아
                    if (dens > 0.62f && ((dx + dz) % 7) == 0)
                    {
                        int f = 2 + ((dx * 7 + dz * 3) % (layers - 2));
                        maps[f][dz, dx] = Mathf.RoundToInt(Mathf.Lerp(3f, 9f, dens));
                    }
                }
            }
            for (int l = 0; l < layers; l++) td.SetDetailLayer(0, 0, l, maps[l]);
            EditorUtility.SetDirty(td);

            foreach (var t in Terrain.activeTerrains)
                if (t.name == "Terrain_Gwana") { t.detailObjectDistance = 130f; t.detailObjectDensity = 1f; EditorUtility.SetDirty(t); }

            AssetDatabase.SaveAssets();
            Debug.Log($"[관아] 지형 디테일 도색 — 프로토타입 {layers}종, 해상도 {res} (마당·어도·흙길 제외)");
        }

        /// <summary>풀이 나도 되는 곳인가 — 마당 안·어도·흙길·구조물 자리는 제외.</summary>
        static bool GrassAllowed(float wx, float wz)
        {
            float ax = Mathf.Abs(wx);
            if (ax < 17.6f && wz > -34.6f && wz < 27.6f) return false;   // 담장 안 마당
            if (WallRectDist(wx, wz) < 1.4f) return false;               // 담장 몸통
            if (ax < 4.6f && wz < -34f) return false;                    // 진입 흙길
            if (ax < 10.5f && wz > -40.5f && wz < -27.5f) return false;  // 외삼문 기단·계단
            return true;
        }

        // ── 공통 ─────────────────────────────────────────────
        static GameObject RecreateGroup(string name)
        {
            var old = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (old != null) Object.DestroyImmediate(old);
            return new GameObject(name);
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
