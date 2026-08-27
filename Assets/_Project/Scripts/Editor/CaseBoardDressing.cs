using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 사건판에 걸린 <b>봉서</b>를 봉서답게 꾸민다. 메뉴: [이문록 ▸ 조사청 ▸ 사건판 꾸미기]
    ///
    /// <b>이것이 무엇인가부터</b>: 사건판의 종이 석 장은 그림이 아니라 <b>어전에서 왕이
    /// 내린 봉서</b>다. 사건으로 드는 입구이자, 이 방에 있는 물건 가운데 가장 격이 높다.
    ///
    /// 그래서 <b>족자로 꾸미면 안 된다</b>. 처음엔 위아래에 축을 물리고 걸이끈을 달았는데,
    /// 축과 걸이끈은 그림을 <b>꾸며 걸어 두는</b> 장치다. 왕명은 감상하라고 내리는 것이
    /// 아니다. 받아서 펴 보고, 벽에 붙여 두고, 그 앞에서 일하는 물건이다.
    ///
    /// <b>봉서는 이렇게 생겼다</b>:
    ///   · 말려서 온다   → 붙여 두어도 아랫자락이 아직 말려 있다(<see cref="Curl"/>).
    ///   · 봉해서 온다   → 붉은 봉함끈이 풀린 채 한쪽에 늘어져 있다.
    ///   · 붙여 둔다     → 위 귀퉁이 둘을 침으로 찔러 둔다. 축도 걸이줄도 없다.
    ///
    /// 인장은 종이 그림에 이미 찍혀 있으므로 여기서 더하지 않는다.
    ///
    /// <b>종이의 자식으로 단다</b>: 사건판 옆에 따로 세우면 종이를 옮길 때 꾸밈이
    /// 제자리에 남는다. 다만 종이가 (0.30, 0.42) 로 눌려 있어 그대로 자식을 달면
    /// 침이 타원이 된다 — 그래서 한 겹 사이에 <b>되돌리는 자</b>를 넣어 그 아래를
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

        private const string PaperPath = "Assets/_Project/_Common/Materials/M_조사청_봉서지.mat";

        // ── 치수(m) ──
        private const float Curl = 0.013f;      // 아랫자락이 말린 굵기(반지름)
        private const float PinR = 0.006f;      // 침 굵기
        private const float PinOut = 0.014f;    // 침이 튀어나온 길이
        private const float PinIn = 0.022f;     // 침이 귀퉁이에서 안쪽으로 들어온 거리
        private const float CordW = 0.016f;     // 봉함끈 너비
        private const float CordT = 0.004f;     // 봉함끈 두께
        private const float Front = -0.010f;    // 종이보다 이만큼 앞(Quad 는 -Z 를 향한다)

        [MenuItem("이문록/조사청/사건판 꾸미기")]
        private static void Run()
        {
            var pile = GameObject.Find(PileName);
            if (pile == null) { Debug.LogError("[사건판] " + PileName + " 을 못 찾았습니다."); return; }

            var cord = Cord();
            var sheet = Sheet();
            var pin = AssetDatabase.LoadAssetAtPath<Material>(WoodPath);
            if (pin == null) { Debug.LogError("[사건판] 침 재질을 못 찾았습니다: " + WoodPath); return; }

            int n = 0;
            foreach (Transform paper in pile.transform)
            {
                var mf = paper.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var old = paper.Find(DressName);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

                Dress(paper, pin, cord, sheet);
                n++;
            }

            EditorUtility.SetDirty(pile);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pile.scene);
            Debug.Log("[사건판] 봉서 " + n + "장에 침과 봉함끈을 달았습니다.");
        }

        private static void Dress(Transform paper, Material pin, Material cord, Material sheet)
        {
            // 눌린 자를 되돌린다 — 이 아래는 정방이라 침이 타원이 되지 않는다.
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
            float topY = h * 0.5f, botY = -h * 0.5f;
            float pinX = w * 0.5f - PinIn;
            float pinY = topY - PinIn * 0.7f;

            // ① 아랫자락 — 말려 온 종이라 아래가 아직 동그랗다.
            //    나무가 아니라 <b>같은 종이</b>다. 축을 물리면 족자가 된다.
            Rod(g, "말린자락", new Vector3(0f, botY + Curl * 0.5f, Front - Curl * 0.4f),
                w, Curl, sheet);

            // ② 침 둘 — 위 귀퉁이를 찔러 판에 붙여 둔다.
            Pin(g, "침_좌", new Vector3(-pinX, pinY, Front), pin);
            Pin(g, "침_우", new Vector3(+pinX, pinY, Front), pin);

            // ③ 봉함끈 — 봉했던 끈이 풀린 채 왼쪽 침에 걸려 늘어져 있다.
            //    두 마디로 꺾어 두면 팽팽한 줄이 아니라 늘어진 끈으로 보인다.
            var a = new Vector3(-pinX, pinY, Front - PinOut * 0.5f);
            var b = new Vector3(-(w * 0.5f + 0.012f), pinY - h * 0.20f, Front - PinOut * 0.5f);
            var c = new Vector3(-(w * 0.5f + 0.004f), pinY - h * 0.40f, Front - PinOut * 0.5f);
            Cord(g, "봉함끈_1", a, b, cord);
            Cord(g, "봉함끈_2", b, c, cord);
            Box(g, "끈끝", c + new Vector3(0f, -CordW * 0.4f, 0f),
                new Vector3(CordW * 0.9f, CordW * 1.1f, CordT), cord);
        }

        /// <summary>침 한 대 — 대가리가 조금 내밀어야 박은 것으로 보인다.</summary>
        private static void Pin(Transform parent, string name, Vector3 at, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at + new Vector3(0f, 0f, -PinOut * 0.5f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 축을 z 로 눕힌다
            go.transform.localScale = new Vector3(PinR * 2f, PinOut * 0.5f, PinR * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = name + "_머리";
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.transform.SetParent(parent, false);
            head.transform.localPosition = at + new Vector3(0f, 0f, -PinOut);
            head.transform.localScale = Vector3.one * (PinR * 3.2f);
            head.GetComponent<Renderer>().sharedMaterial = mat;
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

        /// <summary>말린 아랫자락에 쓸 종이 빛깔. 없으면 만든다.</summary>
        private static Material Sheet()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(PaperPath);
            if (m != null) return m;

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh) { name = "M_조사청_봉서지" };
            m.SetColor("_BaseColor", new Color(0.87f, 0.83f, 0.72f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, PaperPath);
            AssetDatabase.SaveAssets();
            return m;
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
