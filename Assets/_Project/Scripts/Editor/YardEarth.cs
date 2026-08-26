using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>마당을 잔디에서 맨흙으로 돌리고, 빈 중거리에 산등성이를 하나 세운다.</b>
    /// 메뉴: [이문록 ▸ 옹고집 ▸ 마당 흙 깔기] / [… 마당 흙 걷기]
    ///
    /// <b>고증</b>: 조선 살림집의 안마당은 <b>잔디밭이 아니다</b>. 쓸고 다지고 밟아
    /// 굳힌 맨흙이다 — 타작도 하고 멍석도 펴고 혼례상도 차리는 <b>일하는 바닥</b>이라
    /// 풀이 자랄 틈이 없다. 풀은 담장 밖이나 뒤란처럼 발이 덜 닿는 데서 자란다.
    /// 여태 담장 안팎이 온통 한 가지 초록이라, 고택이 아니라 <b>잔디 공원에 놓인
    /// 한옥</b>으로 보였다.
    ///
    /// 그래서 <b>덮되 다 덮지 않는다</b>. 담장 안 한복판은 흙, 가장자리는 잔디가
    /// 그대로 비친다 — 실제로도 마당 귀퉁이와 담 밑에는 풀이 남는다.
    ///
    /// <b>중거리</b>: 능선이 셋 있는데 가장 가까운 것이 260m 밖이다. 그 앞은
    /// 55m 남짓한 바깥숲이 끝나고부터 텅 비어서, 산이 <b>배경 그림</b>처럼 붙어
    /// 보이고 깊이가 안 생긴다. 있는 능선을 그대로 줄여 130m 자리에 한 겹 더 놓는다 —
    /// 새 모델을 들이지 않으므로 색도 안개도 이미 맞아 있는 것을 쓴다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class YardEarth
    {
        private const string ScenePath = "Assets/_Project/Onggojip/Scenes/Onggojip_마당.unity";
        private const string EarthName = "마당_흙";
        private const string RidgeName = "능선_가까이";

        /// <summary>흙을 깔 자리 — 담장(x −40~32 · z −33~51) 안쪽으로 물린 몫.</summary>
        private const float X0 = -33f, X1 = 29f, Z0 = -34f, Z1 = 30f;

        /// <summary>잔디 윗면이 −1.67 이다. 그 바로 위에 얹어야 z-파이팅이 안 난다.</summary>
        private const float TopY = -1.655f;

        [MenuItem("이문록/옹고집/마당 흙 깔기")]
        public static void Lay() { Run(true); }

        [MenuItem("이문록/옹고집/마당 흙 걷기")]
        public static void Clear() { Run(false); }

        private static void Run(bool lay)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[마당] 재생을 멈추고 다시 누르십시오.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var log = new System.Text.StringBuilder(lay ? "[마당] 흙 깔기\n" : "[마당] 흙 걷기\n");

            foreach (var root in scene.GetRootGameObjects())
                if (root.name == EarthName || root.name == RidgeName)
                { Object.DestroyImmediate(root); log.AppendLine("── 먼저 깐 것을 걷었다: " + root.name); }

            if (!lay)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log(log.ToString());
                return;
            }

            var dirt = EarthMaterial(log);
            var group = new GameObject(EarthName);
            EditorSceneManager.MoveGameObjectToScene(group, scene);

            // ① 안마당 — 한 장으로 넓게
            Slab(group.transform, "안마당", new Vector3((X0 + X1) * 0.5f, TopY, (Z0 + Z1) * 0.5f),
                 new Vector3(X1 - X0, 1f, Z1 - Z0), dirt);

            // ② 대문에서 안으로 드는 길 — 마당보다 조금 더 다져진 자리라 한 겹 더 얹는다.
            //    발이 늘 닿는 데는 더 굳고 더 밝다.
            Slab(group.transform, "드나든 자리", new Vector3(0f, TopY + 0.004f, 6f),
                 new Vector3(6.5f, 1f, 46f), dirt);

            log.AppendLine("── 흙 x " + X0 + "~" + X1 + " · z " + Z0 + "~" + Z1 + " (담장 안쪽으로 물렸다 — 가장자리는 잔디)");

            // ③ 중거리 능선
            Transform far = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "먼데")
                    foreach (Transform t in root.transform)
                        if (t.name == "능선_1") far = t;

            if (far == null) log.AppendLine("── 능선_1 을 못 찾아 중거리는 못 세웠다");
            else
            {
                var near = Object.Instantiate(far.gameObject, far.parent);
                near.name = RidgeName;
                near.transform.position = far.position;
                near.transform.rotation = far.rotation * Quaternion.Euler(0f, 37f, 0f);   // 같은 산이 겹쳐 보이지 않게 돌린다
                near.transform.localScale = far.localScale * 0.50f;                        // 260m → 130m
                log.AppendLine("── 능선을 하나 더: 130m 자리 (있던 것을 절반으로 줄이고 37도 돌렸다)");
            }

            Selection.activeGameObject = group;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("   마음에 안 들면 [이문록 ▸ 옹고집 ▸ 마당 흙 걷기]");
            Debug.Log(log.ToString());
        }

        private static void Slab(Transform parent, string name, Vector3 at, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(at.x, at.y - 0.5f, at.z);   // 윗면이 at.y 에 오게
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());           // 바닥은 이미 밑에 있다
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// 흙 재질. <b>있는 것을 쓴다</b> — 고택 팩에 이미 마당 흙(MI_Ground01A)이 있다.
        /// 없으면 잔디를 복사해 흙빛으로 물들인다(그림은 그대로라 결은 남는다).
        /// </summary>
        private static Material EarthMaterial(System.Text.StringBuilder log)
        {
            const string Made = "Assets/_Project/Art/Materials/M_마당_흙.mat";
            var mine = AssetDatabase.LoadAssetAtPath<Material>(Made);
            if (mine != null) { log.AppendLine("── 흙 재질: 전에 만든 것을 쓴다"); return mine; }

            foreach (var g in AssetDatabase.FindAssets("t:Material MI_Ground01A"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m == null) continue;
                var copy = new Material(m);
                copy.name = "M_마당_흙";
                if (copy.HasProperty("_BaseMap")) copy.SetTextureScale("_BaseMap", new Vector2(14f, 14f));
                if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", new Color(0.60f, 0.52f, 0.42f));
                if (copy.HasProperty("_Smoothness")) copy.SetFloat("_Smoothness", 0.04f);
                AssetDatabase.CreateAsset(copy, Made);
                AssetDatabase.SaveAssets();
                log.AppendLine("── 흙 재질: 고택 팩의 MI_Ground01A 를 떠서 M_마당_흙 을 만들었다");
                return copy;
            }

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var plain = new Material(sh);
            plain.name = "M_마당_흙";
            plain.SetColor("_BaseColor", new Color(0.58f, 0.50f, 0.40f));
            AssetDatabase.CreateAsset(plain, Made);
            AssetDatabase.SaveAssets();
            log.AppendLine("── 흙 재질: 바탕그림을 못 찾아 빛깔만 준다");
            return plain;
        }
    }
}
