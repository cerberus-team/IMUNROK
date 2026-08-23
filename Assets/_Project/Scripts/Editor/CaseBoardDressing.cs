using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 사건판의 봉서를 <b>펼친 두루마리</b>로 꾸민다. 메뉴: [이문록 ▸ 조사청 ▸ 사건판 꾸미기]
    ///
    /// <b>지금까지</b>: 널빤지 하나(Plane)에 종이 석 장(Quad)을 얹어 둔 것이 전부였다.
    /// 종이에 그림은 발려 있으나 종이가 <b>어떻게 거기 있는지</b>가 없다 — 붙인 것도
    /// 아니고 걸린 것도 아니라, 벽에 스티커를 붙인 꼴이다.
    ///
    /// <b>무엇을 더하나</b>: 위아래 축과 축머리, 그리고 <b>걸이끈과 못</b>.
    ///
    /// 축이 있으면 종이가 어전에서 <b>동그랗게 말려 온 것</b>으로 보인다. 그리고 그것을
    /// 벽에 <b>걸어 두었다</b>는 것은 걸이끈이 말한다 — 붉은 끈이 위축 양 끝에서 올라가
    /// 못 하나에 걸린다.
    ///
    /// <b>가로띠가 아니다</b>: 처음엔 붉은 끈을 종이 한가운데에 가로로 둘렀는데,
    /// 그건 <b>아직 봉해져 있는</b> 봉서의 모습이다. 여기 걸린 것은 이미 풀어 읽은
    /// 봉서다 — 봉한 띠가 그대로 있으면 펼쳐진 종이와 말이 안 맞는다. 그 끈은 풀려서
    /// 걸이줄이 되는 것이 순리다.
    ///
    /// <b>종이의 자식으로 단다</b>: 사건판 옆에 따로 세우면 종이를 옮길 때 꾸밈이
    /// 제자리에 남는다. 다만 종이가 (0.30, 0.42) 로 눌려 있어 그대로 자식을 달면
    /// 축이 타원이 된다 — 그래서 한 겹 사이에 <b>되돌리는 자</b>를 넣어 그 아래를
    /// 정방으로 만든다.
    ///
    /// 다시 부르면 지웠다 새로 단다. 종이의 자리·크기는 건드리지 않는다.
    /// </summary>
    public static class CaseBoardDressing
    {
        private const string PileName = "조사청_소품/문서 더미";
        private const string DressName = "_꾸밈";
        private const string CordPath = "Assets/_Project/_Common/Materials/M_조사청_홍끈.mat";
        private const string WoodPath = "Assets/_Project/Onggojip/Art/KimMyeonggwanHouse/Material/MI_Wood02A.mat";

        // ── 치수(m) ──
        private const float RodR = 0.010f;      // 축 굵기(반지름)
        private const float RodOver = 0.030f;   // 축이 종이 밖으로 나오는 길이(한쪽)
        private const float KnobR = 0.017f;     // 축머리
        private const float KnobL = 0.022f;
        private const float CordW = 0.020f;     // 끈 너비
        private const float CordT = 0.004f;     // 끈 두께
        private const float HangRatio = 0.30f;  // 걸이끈이 종이 위로 올라가는 높이(종이 높이 대비)
        private const float NailR = 0.011f;     // 못 굵기
        private const float NailOut = 0.016f;   // 못이 튀어나온 길이
        private const float Front = -0.012f;    // 종이보다 이만큼 앞(Quad 는 -Z 를 향한다)

        [MenuItem("이문록/조사청/사건판 꾸미기")]
        private static void Run()
        {
            var pile = GameObject.Find(PileName);
            if (pile == null) { Debug.LogError("[사건판] " + PileName + " 을 못 찾았습니다."); return; }

            var cord = Cord();
            var wood = AssetDatabase.LoadAssetAtPath<Material>(WoodPath);
            if (wood == null) { Debug.LogError("[사건판] 나무 재질을 못 찾았습니다: " + WoodPath); return; }

            int n = 0;
            foreach (Transform paper in pile.transform)
            {
                var mf = paper.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var old = paper.Find(DressName);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

                Dress(paper, wood, cord);
                n++;
            }

            EditorUtility.SetDirty(pile);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pile.scene);
            Debug.Log("[사건판] 봉서 " + n + "장을 펼친 두루마리로 꾸몄습니다.");
        }

        private static void Dress(Transform paper, Material wood, Material cord)
        {
            // 눌린 자를 되돌린다 — 이 아래는 정방이라 축이 타원이 되지 않는다.
            var s = paper.localScale;
            var frame = new GameObject(DressName);
            Undo.RegisterCreatedObjectUndo(frame, "사건판 꾸미기");
            frame.transform.SetParent(paper, false);
            frame.transform.localPosition = Vector3.zero;
            frame.transform.localRotation = Quaternion.identity;
            frame.transform.localScale = new Vector3(
                Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x,
                Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y,
                Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z);

            // 종이의 실제 크기(m). Quad 는 1x1 이므로 눌린 자가 곧 크기다.
            float w = Mathf.Abs(s.x), h = Mathf.Abs(s.y);
            var g = frame.transform;
            float endX = w * 0.5f + RodOver;

            for (int side = 0; side < 2; side++)
            {
                float y = (side == 0 ? 1f : -1f) * h * 0.5f;
                string tag = side == 0 ? "위" : "아래";

                Rod(g, "축_" + tag, new Vector3(0f, y, Front), w + RodOver * 2f, RodR, wood);
                Rod(g, "축머리_" + tag + "_좌", new Vector3(-endX, y, Front), KnobL, KnobR, wood);
                Rod(g, "축머리_" + tag + "_우", new Vector3(+endX, y, Front), KnobL, KnobR, wood);
            }

            // 걸이끈 — 위축 양 끝에서 올라가 한 점에서 만난다.
            // 그 만나는 자리에 못이 박혀 있다. 이 둘이 있어야 "걸어 두었다"가 된다.
            float topY = h * 0.5f;
            float apexY = topY + h * HangRatio;
            var apex = new Vector3(0f, apexY, Front);
            Cord(g, "걸이끈_좌", new Vector3(-endX, topY, Front), apex, cord);
            Cord(g, "걸이끈_우", new Vector3(+endX, topY, Front), apex, cord);

            // 못 — 대가리가 조금 튀어나온다. 납작하면 그린 것으로 보인다.
            Rod(g, "못", apex + new Vector3(0f, 0f, -NailOut * 0.5f), NailOut, NailR, wood);
            Box(g, "못머리", apex + new Vector3(0f, 0f, -NailOut),
                new Vector3(NailR * 2.6f, NailR * 2.6f, NailR * 1.2f), cord);
        }

        /// <summary>두 점을 잇는 끈 한 가닥. 길이와 기울기를 두 점에서 구한다.</summary>
        private static void Cord(Transform parent, string name, Vector3 a, Vector3 b, Material mat)
        {
            var d = b - a;
            float len = new Vector2(d.x, d.y).magnitude;
            if (len < 1e-4f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            go.transform.localScale = new Vector3(len, CordW * 0.62f, CordT);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>가로로 눕힌 원기둥. 유니티 원기둥은 높이 2·반지름 0.5 라 그만큼 되돌린다.</summary>
        private static void Rod(Transform parent, string name, Vector3 pos, float length, float radius, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // 축을 x 로 눕힌다
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>붉은 끈 재질. 없으면 만든다 — 봉서를 봉하던 그 빛깔이다.</summary>
        private static Material Cord()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(CordPath);
            if (m != null) return m;

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh) { name = "M_조사청_홍끈" };
            m.SetColor("_BaseColor", new Color(0.55f, 0.09f, 0.08f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.28f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, CordPath);
            AssetDatabase.SaveAssets();
            return m;
        }
    }
}
