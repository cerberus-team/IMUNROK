using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 줄사다리 프리팹 생성 (2026-08-15, 멱등) — 나무 발판 + 줄. 스무 해 삭아 색이 바랬다.
    ///
    /// 두 버전을 Prefabs/Observatory/에 저장한다:
    ///   줄사다리_온전.prefab   — 걸쇠 가로대(피벗 = 최상단) 아래로 발판 10개, 길이 약 3.4m.
    ///                            회상 장면 등에서 쓸 수 있게 남겨 둔다
    ///   줄사다리_끊어짐.prefab — 상부_매달림(걸쇠 + 발판 2 + 끊어져 풀린 줄 끝)과
    ///                            하부_잔해(y -3.5, 바닥에 쌓인 발판·줄 무더기)의 두 자식.
    ///                            피벗을 비밀문 밑 걸쇠 자리에 놓으면 잔해가 서고 바닥에 온다
    ///
    /// 원작: 아버지(검수관)가 걸어 둔 사다리. 칠석날 밤 선아의 다급한 무게에 삭은 줄이 끊어졌다.
    /// 배치는 ArchiveBuilder(서고 생성)가 한다 — 여기는 프리팹만 만든다.
    /// </summary>
    public static class RopeLadderBuilder
    {
        const string PrefabDir = "Assets/_Project/Gyeonu/Prefabs/Observatory";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Observatory";
        const string SWTex = "Assets/Soswaewon/Textures/Buildings/";

        const float RopeHalfSpan = 0.23f;   // 줄 좌우 간격의 절반
        const float RungStep = 0.33f;       // 발판 간격
        public const float PileDropY = -3.5f;  // 끊어짐 프리팹에서 잔해 무더기의 y 오프셋

        [MenuItem("Tools/이문록/줄사다리 프리팹 생성")]
        public static void Build()
        {
            EnsureFolder(PrefabDir); EnsureFolder(MatDir);
            var texWoodBC = AssetDatabase.LoadAssetAtPath<Texture2D>(SWTex + "T_Wood_BC.png");
            var texWoodNM = AssetDatabase.LoadAssetAtPath<Texture2D>(SWTex + "T_Wood_NM.png");
            // 스무 해 삭은 톤 — 어둡고 채도 낮게, 광 없음
            var matWood = Mat("M_줄사다리_목", new Color(0.36f, 0.31f, 0.24f), 0.08f, texWoodBC, texWoodNM);
            var matRope = Mat("M_줄사다리_줄", new Color(0.41f, 0.37f, 0.30f), 0.04f, null, null);

            SavePrefab(BuildIntact(matWood, matRope), "줄사다리_온전");
            SavePrefab(BuildBroken(matWood, matRope), "줄사다리_끊어짐");
            AssetDatabase.SaveAssets();
            Debug.Log("[줄사다리] 프리팹 2종 저장 — " + PrefabDir + "/줄사다리_온전·줄사다리_끊어짐");
        }

        // ── 온전한 사다리 ────────────────────────────────────
        static GameObject BuildIntact(Material wood, Material rope)
        {
            var rnd = new System.Random(20260815);
            float R01() => (float)rnd.NextDouble();
            var root = new GameObject("줄사다리_온전");
            Anchor(root.transform, wood);
            // 줄 2가닥 — 발판 사이 구간마다 살짝 어긋나게 (삭아 뒤틀린 줄)
            for (int side = 0; side < 2; side++)
            {
                float x = (side == 0 ? -1 : 1) * RopeHalfSpan;
                var prev = new Vector3(x, -0.02f, 0);
                for (int i = 0; i < 11; i++)
                {
                    var next = new Vector3(x + (R01() - 0.5f) * 0.03f, -0.30f - RungStep * i, (R01() - 0.5f) * 0.03f);
                    RopeSeg(root.transform, prev, next, rope);
                    prev = next;
                }
                RopeSeg(root.transform, prev, prev + new Vector3(0.01f, -0.14f, 0.01f), rope);  // 끝단 늘어짐
            }
            // 발판 10개 — 몇 개는 살짝 기울어 삭은 티를 낸다
            for (int i = 0; i < 10; i++)
                Rung(root.transform, new Vector3((R01() - 0.5f) * 0.02f, -0.30f - RungStep * i, (R01() - 0.5f) * 0.02f),
                    (R01() - 0.5f) * 7f, (R01() - 0.5f) * 5f, wood);
            return root;
        }

        // ── 끊어진 사다리 (상부 매달림 + 하부 잔해) ─────────────
        static GameObject BuildBroken(Material wood, Material rope)
        {
            var rnd = new System.Random(20260816);
            float R01() => (float)rnd.NextDouble();
            var root = new GameObject("줄사다리_끊어짐");

            // 상부 — 걸쇠에 매달린 채 끊겨 있다. 왼줄이 조금 더 길게 남아 발판 하나가 비뚤게 대롱거린다
            var top = new GameObject("상부_매달림");
            top.transform.SetParent(root.transform, false);
            Anchor(top.transform, wood);
            RopeSeg(top.transform, new Vector3(-RopeHalfSpan, -0.02f, 0), new Vector3(-RopeHalfSpan - 0.01f, -0.63f, 0.02f), rope);
            RopeSeg(top.transform, new Vector3(-RopeHalfSpan - 0.01f, -0.63f, 0.02f), new Vector3(-RopeHalfSpan - 0.05f, -0.94f, 0.05f), rope);
            RopeSeg(top.transform, new Vector3(RopeHalfSpan, -0.02f, 0), new Vector3(RopeHalfSpan + 0.02f, -0.42f, -0.01f), rope);
            Rung(top.transform, new Vector3(0, -0.30f, 0), 4f, 3f, wood);                      // 첫 발판은 양줄에 붙어 산다
            Rung(top.transform, new Vector3(-0.09f, -0.80f, 0.03f), 8f, 34f, wood);            // 왼줄에만 매달려 기운 발판
            // 끊어져 풀린 줄 끝 — 잔올이 벌어진 인상
            Fray(top.transform, new Vector3(-RopeHalfSpan - 0.05f, -0.94f, 0.05f), rope, rnd);
            Fray(top.transform, new Vector3(RopeHalfSpan + 0.02f, -0.42f, -0.01f), rope, rnd);

            // 하부 — 바닥에 쌓인 잔해. 발판이 흩어지고 줄이 대충 사려 있다
            var pile = new GameObject("하부_잔해");
            pile.transform.SetParent(root.transform, false);
            pile.transform.localPosition = new Vector3(0.12f, PileDropY, 0.10f);
            for (int i = 0; i < 7; i++)
            {
                float a = R01() * Mathf.PI * 2f, r = R01() * 0.38f;
                Rung(pile.transform, new Vector3(Mathf.Cos(a) * r, 0.035f + (i % 2) * 0.055f, Mathf.Sin(a) * r * 0.8f),
                    (R01() - 0.5f) * 14f, R01() * 180f, wood);
            }
            foreach (float loopR in new[] { 0.21f, 0.15f })   // 줄 사림 두 겹
            {
                int seg = 7;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    var p0 = new Vector3(Mathf.Cos(a0) * loopR - 0.05f, 0.045f + loopR * 0.16f, Mathf.Sin(a0) * loopR + 0.06f);
                    var p1 = new Vector3(Mathf.Cos(a1) * loopR - 0.05f, 0.045f + loopR * 0.16f, Mathf.Sin(a1) * loopR + 0.06f);
                    RopeSeg(pile.transform, p0, p1, rope);
                }
            }
            RopeSeg(pile.transform, new Vector3(-0.28f, 0.03f, -0.18f), new Vector3(0.30f, 0.05f, -0.34f), rope);  // 흘러내린 줄
            RopeSeg(pile.transform, new Vector3(0.30f, 0.05f, -0.34f), new Vector3(0.48f, 0.03f, -0.12f), rope);
            return root;
        }

        // ── 부재 ─────────────────────────────────────────────
        /// <summary>걸쇠 가로대 — 문틀 밑에 걸치는 목봉. 프리팹 피벗(0,0,0)이 이 봉의 중심이다.</summary>
        static void Anchor(Transform parent, Material wood)
        {
            Prim(parent, "걸쇠", new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 90), new Vector3(0.05f, 0.47f, 0.05f), wood);
        }

        static void Rung(Transform parent, Vector3 pos, float tilt, float yaw, Material wood)
        {
            Prim(parent, "발판", pos, Quaternion.Euler(tilt, yaw, 90), new Vector3(0.036f, 0.25f, 0.036f), wood);
        }

        static void RopeSeg(Transform parent, Vector3 a, Vector3 b, Material rope)
        {
            var d = b - a;
            Prim(parent, "줄", (a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d.normalized),
                new Vector3(0.024f, d.magnitude * 0.5f, 0.024f), rope);
        }

        /// <summary>끊어져 풀린 줄 끝 — 잔올 세 가닥이 벌어진다.</summary>
        static void Fray(Transform parent, Vector3 at, Material rope, System.Random rnd)
        {
            for (int i = 0; i < 3; i++)
            {
                var dir = new Vector3((float)rnd.NextDouble() - 0.5f, -1f, (float)rnd.NextDouble() - 0.5f).normalized;
                Prim(parent, "잔올", at + dir * 0.045f, Quaternion.FromToRotation(Vector3.up, dir),
                    new Vector3(0.010f, 0.05f, 0.010f), rope);
            }
        }

        static void Prim(Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 시각 전용 — 보행 콜라이더는 배치자가 깐다
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void SavePrefab(GameObject temp, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
        }

        static Material Mat(string name, Color c, float smooth, Texture2D baseMap, Texture2D normal)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", 0f);
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
    }
}
