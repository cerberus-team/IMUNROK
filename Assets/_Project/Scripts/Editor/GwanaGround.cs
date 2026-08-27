using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>관아 마당에 바닥을 깔고, 회색 상자 시절의 잔재를 걷는다.</b>
    /// 메뉴: [이문록 ▸ 관아 ▸ ⑪ 마당 바닥 깔고 잔재 걷기]
    ///
    /// <b>㉠ 바닥이 없었다 — 정확히는 있는데 안 보였다.</b>
    /// 마당에는 38.5×28m 짜리 판이 하나 깔려 있고 그 위를 걸어 다니고 있었는데,
    /// <b>그 판의 렌더러가 꺼져 있었다</b>. 그래서 발은 땅을 딛는데 눈에는 아무것도
    /// 안 보인다 — 담과 집만 허공에 떠 있고 그 사이가 죄 비어 있었다.
    ///
    /// 그런데도 바닥이 있는 것처럼 보였던 까닭은 <b>하늘 때문</b>이다. 유니티 기본
    /// 하늘상자는 위가 파랗고 아래가 회색인데, 그 아래 회색이 꼭 마른 흙바닥처럼
    /// 보인다. 담 너머까지 한 색으로 이어지는 것이 유일한 단서였다 — 흙이라면
    /// 담 밑에서 끊겨야 한다.
    ///
    /// 판을 지우고 새로 만들지 않는다. <b>이미 있는 판을 켠다.</b> 걷는 것도 계단도
    /// 댓돌도 죄 그 판의 콜라이더를 딛고 맞춰 둔 것이라, 새 판으로 갈면 여태 잰
    /// 높이가 다 어긋난다. 켜고, 흙을 입히고, 늘어난 UV만 되잡는다.
    ///
    /// <b>흙은 1막 것을 그대로 쓴다</b>(T_Ground01A_BC). 옹진골의 흙은 담 하나
    /// 사이로 달라지지 않는다 — 같은 고을이면 같은 흙이라야 두 막이 한 마을이 된다.
    /// 다만 타일링은 따로 잡는다(2.5m 마다 한 번). 판이 38m 인데 한 번만 깔면
    /// 흙 알갱이가 사람 키만 해진다.
    ///
    /// <b>㉡ 회색 상자 잔재.</b> 세트가 들어오기 전에 "여기가 문서고", "여기가 동헌"
    /// 하고 세워 둔 <b>기둥 표식 둘</b>이 아직 마당에 서 있었다. 이제 그 자리에는
    /// 진짜 집이 서 있고, 어디가 어디인지는 <b>서리가 나와서 일러 준다</b>. 표식이
    /// 할 일이 없다. 게다가 눈에 잘 안 띄는 회색 기둥에 콜라이더가 달려 있어,
    /// 마당을 걷다 보이지 않는 것에 걸린다.
    ///
    /// 표식을 걷으면 Zone_ 껍데기도 빈다. 빈 껍데기는 계층창에서 자리만 차지하고
    /// 나중에 "이게 뭐였더라" 하게 되므로 같이 걷는다.
    ///
    /// 두 번 눌러도 탈이 없다 — 이미 켜진 바닥은 그대로 두고, 이미 걷은 것은 넘어간다.
    /// </summary>
    public static class GwanaGround
    {
        private const string GroundTex = "Assets/_Project/Onggojip/Art/KimMyeonggwanHouse/Texture/T_Ground01A_BC.png";
        private const string MatPath = "Assets/_Project/_Common/Sets/Gwana/Donheon/Materials/M_관아_마당.mat";

        /// <summary>흙 알갱이가 몇 미터마다 한 번 되풀이되나. 이보다 크면 무늬가 눈에 띈다.</summary>
        private const float TileMeters = 2.5f;

        [MenuItem("이문록/관아/⑪ 마당 바닥 깔고 잔재 걷기")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 마당을 손본다\n");
            Ground(scene, log);
            Sweep(scene, log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ── 바닥 ──

        private static void Ground(Scene scene, System.Text.StringBuilder log)
        {
            var floor = FindGround(scene);
            if (floor == null) { log.AppendLine("  ※ 마당 판을 못 찾았다 — ③ 을 먼저 누르십시오"); return; }

            floor.name = "마당바닥";
            if (floor.transform.parent != null) floor.transform.SetParent(null, true);

            var mr = floor.GetComponent<MeshRenderer>();
            if (mr == null) { log.AppendLine("  ※ 마당 판에 렌더러가 없다"); return; }

            if (!mr.enabled)
            {
                mr.enabled = true;
                log.AppendLine("  · 바닥 렌더러를 켰다 — <b>꺼져 있었다</b>. 딛고는 있는데 안 보이던 까닭이다");
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 땅은 제 그림자를 안 던진다
            mr.receiveShadows = true;

            mr.sharedMaterial = EnsureMaterial(floor, log);

            // 이 판이 실제로 몇 미터인지 재서 그만큼 되풀이한다.
            // Plane 프리미티브는 크기를 스케일로 주므로 스케일을 보고 잡아야 한다.
            var mf = floor.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mr.sharedMaterial != null)
            {
                Vector3 world = Vector3.Scale(mf.sharedMesh.bounds.size, floor.transform.lossyScale);
                var tiling = new Vector2(Mathf.Max(1f, world.x / TileMeters), Mathf.Max(1f, world.z / TileMeters));
                mr.sharedMaterial.SetTextureScale("_BaseMap", tiling);
                EditorUtility.SetDirty(mr.sharedMaterial);
                log.AppendLine("  · 마당 " + world.x.ToString("F1") + "×" + world.z.ToString("F1") + "m — 흙을 "
                             + TileMeters + "m 마다 되풀이(" + tiling.x.ToString("F0") + "×" + tiling.y.ToString("F0") + ")");
            }
        }

        /// <summary>
        /// 마당 판 찾기. 이름이 무엇이든(Floor · 마당바닥) <b>y=0 에 넓게 깔린 판</b>을 찾는다.
        /// 이름으로만 찾으면 한 번 이름을 바꾼 뒤에 두 번째로 못 찾는다.
        /// </summary>
        private static GameObject FindGround(Scene scene)
        {
            GameObject best = null;
            float widest = 0f;
            foreach (var mf in Resources.FindObjectsOfTypeAll<MeshFilter>())
            {
                if (mf.gameObject.scene != scene || mf.sharedMesh == null) continue;
                if (mf.GetComponent<MeshRenderer>() == null) continue;
                var t = mf.transform;
                if (Mathf.Abs(t.position.y) > 0.05f) continue;                       // 땅에 붙어 있어야 한다
                Vector3 w = Vector3.Scale(mf.sharedMesh.bounds.size, t.lossyScale);
                if (w.y > 0.5f) continue;                                            // 납작해야 한다
                float span = Mathf.Min(w.x, w.z);
                if (span < 15f || span <= widest) continue;                          // 마당만 하게 넓어야 한다
                widest = span; best = mf.gameObject;
            }
            return best;
        }

        /// <summary>
        /// 마당 흙 머티리얼. 없으면 만든다.
        ///
        /// 1막 흙(<c>MI_Ground01A</c>)을 그대로 쓰지 않고 <b>따로 하나 만든다</b> —
        /// 타일링을 여기서 바꿔야 하는데, 그 머티리얼은 1막 마당이 함께 쓰는 것이라
        /// 손대면 저쪽 흙 알갱이가 같이 잘아진다. 그림(텍스처)만 같이 쓴다.
        /// </summary>
        private static Material EnsureMaterial(GameObject floor, System.Text.StringBuilder log)
        {
            var had = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (had != null) return had;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTex);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = "M_관아_마당" };
            if (tex != null) m.SetTexture("_BaseMap", tex);
            else log.AppendLine("  ※ 흙 그림을 못 찾았다(" + GroundTex + ") — 민색으로 깐다");
            // 마른 마당 흙이다. 반들거리면 비 온 뒤가 된다.
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.06f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, MatPath);
            AssetDatabase.SaveAssets();
            log.AppendLine("  · 마당 흙을 만들었다 (" + MatPath + ")");
            return m;
        }

        // ── 잔재 ──

        private static void Sweep(Scene scene, System.Text.StringBuilder log)
        {
            int n = 0;

            // 표식 기둥 — 세트가 들어오기 전에 "여기가 문서고" 하고 세워 둔 것
            foreach (var t in AllTransforms(scene))
            {
                if (t == null || !t.name.StartsWith("[구역]")) continue;
                log.AppendLine("  · 표식 기둥을 걷었다: " + t.name + " " + t.position.ToString("F1"));
                Undo.DestroyObjectImmediate(t.gameObject);
                n++;
            }

            // 안 쓰는 회색 상자 바닥(꺼둔 채 남아 있던 것)
            foreach (var t in AllTransforms(scene))
            {
                if (t == null || t.name != "Floor") continue;
                log.AppendLine("  · 안 쓰는 회색 상자 바닥을 걷었다 (" + (t.parent == null ? "-" : t.parent.name) + " 밑)");
                Undo.DestroyObjectImmediate(t.gameObject);
                n++;
            }

            // 빈 껍데기
            foreach (var t in AllTransforms(scene))
            {
                if (t == null || !t.name.StartsWith("Zone_")) continue;
                if (t.childCount > 0) continue;
                log.AppendLine("  · 빈 껍데기를 걷었다: " + t.name);
                Undo.DestroyObjectImmediate(t.gameObject);
                n++;
            }

            if (n == 0) log.AppendLine("  · 걷을 잔재가 없다");
        }

        private static Transform[] AllTransforms(Scene scene)
        {
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<Transform>(true));
            return list.ToArray();
        }
    }
}
