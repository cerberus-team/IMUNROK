using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>발 너머에 사람을 앉힌다.</b>
    /// 메뉴: [이문록 ▸ 엔딩 ▸ 발 너머에 앉히기]
    ///
    /// 앞서는 발을 걷고 <b>빈 어좌</b>를 보였다. 그것이 틀렸다 — 「왕은 어디에 갔나」라는
    /// 물음이 생기는데 이야기에는 그 물음에 답할 것이 없다. 대답할 수 없는 것을 묻게
    /// 만드는 연출은 <b>여운이 아니라 구멍</b>이다.
    ///
    /// 고쳐 잡은 규칙: <b>왕은 거기 계신다. 다만 끝내 안 보인다.</b>
    /// 있는 줄 알면서 못 보는 것이, 없는 것보다 멀다.
    ///
    /// 그래서 여기서 짓는 것은 사람이 아니라 <b>사람의 윤곽</b>이다.
    ///
    /// <b>왜 상자로 짓는가</b> — 발 너머라 어차피 살 틈으로 새는 것과 발에 지는 그림자,
    /// 둘밖에 안 보인다. 얼굴을 만들어 봐야 한 픽셀도 안 보이고, 오히려 <b>또렷할수록
    /// 뜯어보게 된다</b>. 상자 열한 개와 공 하나로 짓되 <b>익선관만 또렷하게</b> 세운다 —
    /// 실루엣에서 사람을 임금으로 만드는 것은 어깨선이 아니라 <b>머리에 쓴 것</b>이다.
    ///
    /// <b>보이는 것은 그림자가 아니라 「틈」이다</b> — 여기서 두 번 헛짚었다.
    ///
    /// <b>첫 번째</b>: 있던 뒷불 하나로 발에 그림자를 지우려 했는데, 재 보니 그 불이
    /// <b>왕의 목 자리에 박혀</b> 있었다(0, 2.05, 2.85). 빛이 가리개 <b>안</b>에 있으면
    /// 그림자는 40배로 부풀어 발을 통째로 덮는 검은 얼룩이 된다. 도구가 배율을
    /// 계산해서 찍어 주지 않았으면 재생해 보고서야 알았을 것이다.
    ///
    /// <b>두 번째</b>: 불을 뒷벽 밖으로 멀찍이 빼서 배율을 1.7배까지 낮췄는데도
    /// 화면에는 아무것도 안 보였다. 까닭은 간단했다 — <b>발의 살은 불투명한 상자다.</b>
    /// 뒤에서 오는 빛은 살의 <b>뒷면</b>을 비추고, 우리가 보는 것은 <b>앞면</b>이다.
    /// 진짜 발이라면 빛이 대를 통과해 배어 나오지만, 여기 것은 나무 널이라 안 배어
    /// 나온다. 그러니 <b>발에 그림자를 그려 봐야 볼 수가 없다.</b>
    ///
    /// 볼 수 있는 것은 <b>살과 살 사이의 틈</b>뿐이다. 그래서 규칙을 뒤집었다.
    ///
    ///   <b>발 너머를 밝히고, 왕을 그 앞에 앉힌다.</b>
    ///   틈으로 보이는 밝은 배경(일월오봉도)에 왕이 <b>검게 뚫려</b> 보인다.
    ///
    /// 그러려면 불이 <b>왕과 병풍 사이</b>에 있어야 한다. 왕 뒤 0.4m, 병풍 앞 0.2m —
    /// 그 좁은 틈이 이 연출이 서는 자리다. 불이 왕보다 앞에 있으면 얼굴이 드러나고,
    /// 병풍보다 뒤에 있으면 병풍의 앞면이 캄캄해진다.
    ///
    ///   · <b>왕_그림자불</b> — 왕과 병풍 사이. 병풍을 밝혀 <b>화면</b>으로 만들고,
    ///     왕의 등을 때려 앞면을 어둡게 남긴다. 복명이 끝나는 자리에서 이 불이
    ///     차오르면 그제야 윤곽이 떠오른다.
    ///   · <b>어좌_뒷불</b> — 옛 불. 왕의 머리 <b>위</b>로 올리고 아주 낮춰 둔다.
    ///     그림자는 끈다 — 두 불이 저마다 그림자를 지면 윤곽이 둘로 겹친다.
    ///
    /// 어좌 세 덩이의 그림자도 끈다. 등받이가 왕과 발 사이에 서서, 그대로 두면
    /// 왕의 윤곽과 등받이의 윤곽이 한 덩어리로 뭉친다.
    ///
    /// ⚠ [발 너머로 고치기]를 다시 누르면 뒷불이 옛 자리로 돌아간다. 그때는 이것을
    /// 한 번 더 누르면 된다 — 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class EndingKing
    {
        private const string ScenePath = "Assets/_Project/Scenes/Core/EndingScene.unity";
        private const string MatPath = "Assets/_Project/Art/Materials/M_어전_그림자.mat";
        private const string Root = "왕";

        [MenuItem("이문록/엔딩/발 너머에 앉히기")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[엔딩] 재생을 멈추고 다시 누르십시오.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var log = new System.Text.StringBuilder("[엔딩] 발 너머에 앉힌다\n");

            Transform hall = null, seat = null, back = null, veil = null, screen = null;
            // 그림자를 꺼야 하는 것들 — 왕과 발 사이에 서서 윤곽을 뭉치게 하는 덩이들
            var blockers = new System.Collections.Generic.List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "어전") hall = t;
                    else if (t.name == "어좌") { seat = t; blockers.Add(t); }
                    else if (t.name == "어좌_등" || t.name == "어좌_단") blockers.Add(t);
                    else if (t.name == "일월오봉도") screen = t;
                    else if (t.name == "어좌_뒷불") back = t;
                    else if (t.name == "발") veil = t;
                }
            if (seat == null) { Debug.LogWarning(log + "── 어좌를 못 찾았다. [어전 짓기]를 먼저 누르십시오."); return; }
            if (hall == null) hall = seat.parent;

            // ── 어좌의 <b>앉는 면</b>을 잰다. 값을 새로 정하지 않고 있는 것에서 뽑는다 ──
            Bounds sb = Measure(seat);
            float sit = sb.max.y;                      // 앉는 자리 높이
            float mid = sb.center.z;                   // 어좌의 앞뒤 한가운데
            log.AppendLine("── 어좌: 앉는 면 y " + sit.ToString("F2") + " · z " + mid.ToString("F2"));

            // ── 어좌 세 덩이의 그림자를 끈다 ──────────────
            // 어좌는 왕과 발 사이에 서 있다. 그대로 두면 왕의 윤곽과 등받이의 윤곽이
            // 한 덩어리로 뭉쳐, 사람이 아니라 <b>큰 상자</b>가 앉아 있는 것으로 보인다.
            int off = 0;
            foreach (var t in blockers)
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                    if (r.shadowCastingMode != ShadowCastingMode.Off)
                    { r.shadowCastingMode = ShadowCastingMode.Off; off++; }
            if (off > 0) log.AppendLine("── 어좌 " + off + "덩이의 그림자를 껐다 (윤곽은 왕 하나라야 한다)");

            // 뒷벽과 병풍은 <b>도로 켠다</b>. 앞선 판에서 불을 벽 밖에 세우느라 껐는데,
            // 이제 불이 병풍 앞에 있으니 끌 까닭이 없다 — 끄고 두면 왜 꺼져 있는지
            // 아무도 모르는 값이 하나 남는다.
            int on = 0;
            foreach (var t in new[] { screen, Find(scene, "벽_뒤") })
                if (t != null)
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                        if (r.shadowCastingMode == ShadowCastingMode.Off)
                        { r.shadowCastingMode = ShadowCastingMode.On; on++; }
            if (on > 0) log.AppendLine("── 뒷벽·병풍의 그림자를 도로 켰다 " + on + "덩이");

            // ── 왕 ────────────────────────────────────
            var mat = Skin();
            var old = hall.Find(Root);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var king = new GameObject(Root).transform;
            king.SetParent(hall, false);
            king.position = new Vector3(sb.center.x, sit, mid);
            king.rotation = Quaternion.identity;       // 발 쪽(-z)을 본다

            // 앉은 몸. 치수는 <b>앉은키</b> 기준이다 — 어른이 책상다리로 앉으면
            // 방석 위에서 정수리까지 0.9m 남짓이고, 익선관이 그 위로 0.25m 더 선다.
            Box(king, mat, "무릎",      new Vector3( 0f,    0.09f, -0.30f), new Vector3(0.76f, 0.18f, 0.32f), Vector3.zero);
            Box(king, mat, "허리",      new Vector3( 0f,    0.08f,  0.00f), new Vector3(0.58f, 0.16f, 0.44f), Vector3.zero);
            Box(king, mat, "몸통",      new Vector3( 0f,    0.42f,  0.02f), new Vector3(0.52f, 0.56f, 0.36f), Vector3.zero);
            // 곤룡포의 소매는 크다. 어깨에서 아래로 벌어지는 이 두 덩이가
            // 실루엣을 <b>사람</b>이 아니라 <b>차려입은 사람</b>으로 만든다.
            Box(king, mat, "소매_좌",   new Vector3(-0.36f, 0.36f, -0.05f), new Vector3(0.28f, 0.38f, 0.32f), new Vector3(0f, 0f, -12f));
            Box(king, mat, "소매_우",   new Vector3( 0.36f, 0.36f, -0.05f), new Vector3(0.28f, 0.38f, 0.32f), new Vector3(0f, 0f,  12f));
            Box(king, mat, "목",        new Vector3( 0f,    0.74f,  0.02f), new Vector3(0.14f, 0.09f, 0.14f), Vector3.zero);
            Ball(king, mat, "머리",     new Vector3( 0f,    0.86f,  0.02f), 0.23f);

            // 익선관 — 여기만 또렷하게 짓는다. 실루엣에서 임금을 임금이게 하는 것은
            // 어깨선이 아니라 머리에 쓴 것이다. 뒤가 높은 두 층에 소각(뿔) 둘.
            Box(king, mat, "익선관_앞", new Vector3( 0f,    0.99f,  0.03f), new Vector3(0.24f, 0.14f, 0.24f), Vector3.zero);
            Box(king, mat, "익선관_뒤", new Vector3( 0f,    1.08f,  0.09f), new Vector3(0.22f, 0.13f, 0.15f), Vector3.zero);
            // 소각은 <b>위로 뻗어야</b> 소각이다. 처음에 옆으로 눕혀 놓았더니
            // 실루엣에서 머리와 붙어 <b>가로 막대 하나</b>로 뭉쳐 버렸다 —
            // 관을 쓴 사람이 아니라 무언가 걸친 사람으로 보였다.
            // 위로 세우고 바깥으로 벌리면 그제야 둘로 갈라져 보인다.
            Box(king, mat, "소각_좌",   new Vector3(-0.10f, 1.26f,  0.11f), new Vector3(0.055f, 0.26f, 0.025f), new Vector3(-14f, 0f, -18f));
            Box(king, mat, "소각_우",   new Vector3( 0.10f, 1.26f,  0.11f), new Vector3(0.055f, 0.26f, 0.025f), new Vector3(-14f, 0f,  18f));

            // 콜라이더는 없다 — 만질 것이 아니다. 부딪힐 일도 없다(발 너머다).
            foreach (var c in king.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            log.AppendLine("── 왕을 앉혔다 — 상자 11에 공 1. 익선관만 또렷하다");

            // ── 옛 불: 왕의 머리 <b>위</b>로 올리고 아주 낮추고 그림자를 끈다 ──
            if (back != null)
            {
                back.position = new Vector3(0f, 2.90f, 3.15f);
                var bl = back.GetComponent<Light>();
                if (bl != null) { bl.intensity = 1.0f; bl.shadows = LightShadows.None; }
                log.AppendLine("── 어좌_뒷불을 왕 머리 위로 올리고 낮췄다 " +
                               "(왕의 목 자리에 박혀 있었다 — 그래서 그림자가 40배로 부풀었다)");
            }

            // ── 배경을 밝히는 불: 왕과 병풍 <b>사이</b> ──
            // 자리를 손으로 정하지 않고 <b>두 물건에서 뽑는다</b>. 병풍을 옮기면
            // 이 불도 따라 옮겨진다 — 값을 손으로 적어 두면 그때 조용히 어긋난다.
            float kingBack = king.position.z + 0.24f;                       // 왕의 등
            float panelFront = screen != null ? screen.position.z - 0.03f   // 병풍 앞면
                                              : king.position.z + 0.62f;
            float lampZ = Mathf.Lerp(kingBack, panelFront, 0.55f);

            var lamp = hall.Find("왕_그림자불");
            if (lamp == null)
            {
                var go = new GameObject("왕_그림자불");
                go.transform.SetParent(hall, false);
                lamp = go.transform;
            }
            // 높이는 <b>익선관 끝</b>에 맞춘다. 그러면 가장 밝은 데가 머리 뒤라
            // 얼굴이 아니라 <b>관의 윤곽</b>이 제일 또렷해진다 — 보라는 것이 그것이다.
            lamp.position = new Vector3(king.position.x, king.position.y + 1.22f, lampZ);
            var sl = lamp.GetComponent<Light>();
            if (sl == null) sl = lamp.gameObject.AddComponent<Light>();
            sl.type = LightType.Point;
            sl.color = new Color(1f, 0.84f, 0.58f);        // 등잔빛
            // 처음엔 <b>희미하다</b>. 복명이 끝나는 자리에서 이 불이 2.3배로 차오르며
            // 그제야 윤곽이 떠오른다 — 나타나는 것이라야 사건이 된다.
            // 2.6 은 <b>너무 밝았다</b> — 복명에서 2.3배로 차오르면 6이 되어
            // 어전이 통째로 환해지고 어둠이 거짓말이 된다. 재 보고 절반으로 낮췄다.
            sl.intensity = 1.2f;
            sl.range = 8f;
            sl.shadows = LightShadows.Soft;
            sl.shadowStrength = 0.92f;

            log.AppendLine("── 왕_그림자불을 왕(z " + king.position.z.ToString("F2") + ")과 병풍(z "
                         + panelFront.ToString("F2") + ") 사이 z " + lampZ.ToString("F2")
                         + " 에 두었다 — 왕 뒤 " + (lampZ - kingBack).ToString("F2")
                         + "m · 병풍 앞 " + (panelFront - lampZ).ToString("F2") + "m");
            if (lampZ <= kingBack + 0.02f || lampZ >= panelFront - 0.02f)
                log.AppendLine("── ⚠ 불이 낄 틈이 없다. 왕과 병풍이 너무 붙어 있다");

            // 발까지 몇 배로 벌어지는지도 적어 둔다. 발에 지는 그림자는 <b>못 보지만</b>
            // (살이 불투명해 앞면에 안 온다), 배율이 너무 크면 틈으로 새는 빛까지
            // 왕이 통째로 가려 버려 배경이 안 남는다.
            if (veil != null)
            {
                // 발의 부모는 원점에 있고 살만 앞으로 나가 있다 — 부모 자리를 쓰면
                // 계산이 통째로 틀어진다. 한 번 그렇게 해서 40배가 나왔다.
                float vz = veil.childCount > 0 ? veil.GetChild(0).position.z : veil.position.z;
                float ks = (lampZ - vz) / (lampZ - (king.position.z + 0.02f));
                log.AppendLine("── 발까지 " + ks.ToString("F1") + "배 · 어깨 그림자 너비 "
                             + (1.00f * ks).ToString("F1") + "m (발 너비는 7.8m)");
            }
            if (king.position.x != 0f)
                log.AppendLine("── ⚠ 왕이 한가운데가 아니다 (x " + king.position.x.ToString("F2") + ")");

            EditorUtility.SetDirty(hall.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
            Selection.activeTransform = king;
        }

        private static Transform Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }

        private static Bounds Measure(Transform t)
        {
            Bounds b = new Bounds(t.position, Vector3.zero);
            bool first = true;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            return b;
        }

        /// <summary>
        /// 그림자를 만들 몸의 재질. <b>새까맣게 하지 않는다</b> — 아주 검으면 오려 붙인
        /// 종이가 되어 살 틈으로 볼 때 깊이가 없다. 등잔빛이 가장자리에 조금 걸리도록
        /// 아주 어두운 남빛으로 둔다.
        /// </summary>
        private static Material Skin()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (m == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, MatPath);
            }
            var ink = new Color(0.045f, 0.042f, 0.055f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", ink);
            if (m.HasProperty("_Color")) m.SetColor("_Color", ink);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.10f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void Box(Transform parent, Material mat, string name,
                                Vector3 at, Vector3 size, Vector3 euler)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void Ball(Transform parent, Material mat, string name, Vector3 at, float d)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = new Vector3(d, d * 1.12f, d);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
