using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>엔딩 어전에 배경을 입힌다.</b>
    /// 메뉴: [이문록 ▸ 엔딩 ▸ 어전 차리기]
    ///
    /// 지어 두기만 한 방은 <b>상자</b>다 — 바닥과 벽과 어좌뿐이면 어전이 아니라
    /// 어딘가의 빈 방이고, 발 너머에서 목소리가 나도 그 임자가 누구인지 알 도리가 없다.
    /// 어전을 어전이게 하는 것은 방의 크기가 아니라 <b>차림</b>이다.
    ///
    /// 셋을 들인다. 셋 다 <b>인트로의 어전에 이미 있던 것</b>이고 재질도 그때 것이다 —
    /// 게임이 <b>같은 자리에서 열리고 닫히게</b> 하려는 것이다. 처음에 봉서를 받은
    /// 그 자리에 돌아와 복명을 올리는 것이라야 이야기가 닫힌다.
    ///
    ///   ① <b>전돌 바닥</b> — 나뭇바닥은 살림집 것이다. 대궐 바닥은 벽돌을 깐다.
    ///   ② <b>어도(御道)</b> — 임금에게로 난 붉은 길. 이 한 줄이 방에 <b>방향</b>을 준다.
    ///      깔기 전에는 어느 쪽이 위인지 그림만 봐서는 몰랐다.
    ///   ③ <b>일월오봉도(日月五峰圖)</b> — 어좌 뒤에 서는 병풍. 해와 달과 다섯 봉우리.
    ///      조선 임금의 자리에는 <b>반드시</b> 이것이 있었다. 그림이 없으니 그려 넣는다.
    ///
    /// 특히 ③이 값이 크다: 뒷불이 이것을 비추므로 <b>발 너머로 비쳐 드는 것</b>이
    /// 그저 빛이 아니라 <b>다섯 봉우리</b>가 된다. 왕은 안 보이되 그 자리가
    /// 무엇인지는 보인다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class EndingHallDress
    {
        private const string ScenePath = "Assets/_Project/Scenes/Core/EndingScene.unity";
        private const string GroupName = "어전_차림";
        private const string PaintingPath = "Assets/_Project/Art/Props/Textures/T_일월오봉도.png";
        private const string PaintMatPath = "Assets/_Project/Art/Materials/M_일월오봉도.mat";

        [MenuItem("이문록/엔딩/어전 차리기")]
        public static void Dress()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogWarning("[엔딩] 재생을 멈추고 다시 누르십시오."); return; }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var log = new System.Text.StringBuilder("[엔딩] 어전 차리기\n");

            Transform hall = null, floor = null, seat = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "어전") hall = t;
                    else if (t.name == "마루") floor = t;
                    else if (t.name == "어좌") seat = t;
                }
            if (hall == null || seat == null)
            { Debug.LogWarning(log + "── 어전이나 어좌를 못 찾았다"); return; }

            foreach (Transform t in hall)
                if (t.name == GroupName)
                { Object.DestroyImmediate(t.gameObject); log.AppendLine("── 먼저 차린 것을 걷었다"); break; }

            var group = new GameObject(GroupName).transform;
            group.SetParent(hall, false);

            float floorY = 0f;
            if (floor != null)
            {
                var fr = floor.GetComponent<Renderer>();
                if (fr != null) floorY = fr.bounds.max.y;
            }

            // ── ① 전돌 바닥 ──────────────────────────────
            var brick = Find<Material>("M_어전_전돌");
            if (floor != null && brick != null)
            {
                var r = floor.GetComponent<Renderer>();
                if (r != null)
                {
                    Undo.RecordObject(r, "전돌");
                    r.sharedMaterial = brick;
                    EditorUtility.SetDirty(r);
                    log.AppendLine("── 마루를 전돌로 갈았다 (대궐 바닥은 벽돌이다)");
                }
            }
            else log.AppendLine("── 전돌 재질을 못 찾아 바닥은 그대로 둔다");

            // ── ② 어도 ──────────────────────────────────
            var road = Find<Material>("M_어전_어도");
            if (road != null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Cube);
                q.name = "어도";
                q.transform.SetParent(group, false);
                float z0 = -4.2f, z1 = seat.position.z;
                q.transform.position = new Vector3(0f, floorY + 0.006f, (z0 + z1) * 0.5f);
                q.transform.localScale = new Vector3(1.55f, 0.012f, z1 - z0);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.GetComponent<Renderer>().sharedMaterial = road;
                log.AppendLine("── 어도를 깔았다 (z " + z0.ToString("F1") + " → " + z1.ToString("F1")
                             + ") — 방에 위아래가 생긴다");
            }

            // ── ③ 일월오봉도 ─────────────────────────────
            var tex = Painting(log);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PaintMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, PaintMatPath);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(mat);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "일월오봉도";
            panel.transform.SetParent(group, false);
            // 병풍은 <b>임금을 감싸는 크기</b>다. 작게 세웠더니 발 너머에
            // 밝은 네모 하나가 떠 있는 꼴이 되어, 다섯 봉우리가 아니라
            // 초록 옷 입은 사람처럼 보였다. 뒷벽만큼 넓힌다.
            panel.transform.position = new Vector3(0f, floorY + 1.62f, seat.position.z + 0.62f);
            panel.transform.localScale = new Vector3(6.4f, 3.0f, 1f);
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.GetComponent<Renderer>().sharedMaterial = mat;
            log.AppendLine("── 일월오봉도를 세웠다 @ z " + panel.transform.position.z.ToString("F2"));

            // 뒷불이 <b>병풍을 비춰야</b> 발에 봉우리가 비친다.
            // 병풍보다 뒤에 있으면 병풍의 뒷면만 밝히고 앞은 그대로 캄캄하다.
            foreach (Transform t in hall)
            {
                if (t.name != "어좌_뒷불") continue;
                var li = t.GetComponent<Light>();
                t.position = new Vector3(0f, floorY + 2.05f, seat.position.z + 0.10f);
                if (li != null) { li.intensity = 2.9f; li.range = 8.5f; }   // 비치는 것이지 비추는 것이 아니다
                log.AppendLine("── 뒷불을 병풍 앞으로 옮겼다 " + t.position.ToString("F2"));
            }

            Selection.activeGameObject = group.gameObject;
            EditorUtility.SetDirty(hall.gameObject);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(log.ToString());
        }

        private static T Find<T>(string name) where T : Object
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:" + typeof(T).Name))
            {
                var o = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g));
                if (o != null && o.name == name) return o;
            }
            return null;
        }

        /// <summary>
        /// <b>일월오봉도를 그린다.</b> 그림 파일이 없으니 그린다 — 발 너머로 비칠 것이라
        /// <b>또렷한 윤곽</b>이면 되고, 붓질은 어차피 안 보인다.
        ///
        /// 짜임은 정해져 있다: 다섯 봉우리, 한쪽에 해·다른 쪽에 달, 양옆에 소나무 둘,
        /// 아래에 물결. 이 배치가 곧 「임금의 자리」라는 표다.
        /// </summary>
        private static Texture2D Painting(System.Text.StringBuilder log)
        {
            var made = AssetDatabase.LoadAssetAtPath<Texture2D>(PaintingPath);
            if (made != null) { log.AppendLine("── 일월오봉도: 전에 그린 것을 쓴다"); return made; }

            const int W = 1024, H = 720;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var sky = new Color(0.09f, 0.10f, 0.15f);
            var peak = new Color(0.13f, 0.19f, 0.21f);
            var peakLit = new Color(0.18f, 0.26f, 0.27f);
            var water = new Color(0.11f, 0.16f, 0.20f);
            var sun = new Color(0.62f, 0.20f, 0.15f);
            var moon = new Color(0.80f, 0.80f, 0.74f);
            var trunk = new Color(0.30f, 0.18f, 0.11f);
            var leaf = new Color(0.11f, 0.19f, 0.15f);

            float[] cx = { 0.13f, 0.31f, 0.50f, 0.69f, 0.87f };
            float[] hh = { 0.42f, 0.56f, 0.72f, 0.56f, 0.42f };
            float[] ww = { 0.15f, 0.18f, 0.22f, 0.18f, 0.15f };
            const float waterTop = 0.20f;

            for (int y = 0; y < H; y++)
            {
                float v = y / (float)H;
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    Color c = sky;

                    if (v < waterTop)
                    {
                        float band = Mathf.Sin(v * 90f + Mathf.Sin(u * 18f) * 1.6f);
                        c = band > 0.72f ? Color.Lerp(water, moon, 0.28f) : water;
                    }
                    else
                    {
                        for (int k = 0; k < 5; k++)
                        {
                            float d = Mathf.Abs(u - cx[k]) / ww[k];
                            if (d > 1f) continue;
                            float top = waterTop + hh[k] * (1f - d * d);
                            if (v > top) continue;
                            c = Color.Lerp(peakLit, peak, Mathf.Clamp01(d + 0.15f));
                            if (k == 2 && v > top - 0.012f) c = Color.Lerp(c, moon, 0.35f);
                            break;
                        }
                    }

                    float sd = Mathf.Sqrt((u - 0.80f) * (u - 0.80f) + (v - 0.86f) * (v - 0.86f) * 2.0f);
                    if (sd < 0.055f) c = sun;
                    float md = Mathf.Sqrt((u - 0.20f) * (u - 0.20f) + (v - 0.86f) * (v - 0.86f) * 2.0f);
                    if (md < 0.050f) c = moon;

                    for (int s = 0; s < 2; s++)
                    {
                        float tx = s == 0 ? 0.055f : 0.945f;
                        if (Mathf.Abs(u - tx) < 0.010f && v > waterTop && v < waterTop + 0.34f) c = trunk;
                        float ld = Mathf.Sqrt((u - tx) * (u - tx)
                                   + (v - (waterTop + 0.36f)) * (v - (waterTop + 0.36f)) * 1.7f);
                        if (ld < 0.075f) c = leaf;
                    }

                    t.SetPixel(x, y, c);
                }
            }
            t.Apply();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PaintingPath));
            System.IO.File.WriteAllBytes(PaintingPath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(PaintingPath);
            log.AppendLine("── 일월오봉도를 그렸다 → " + PaintingPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PaintingPath);
        }
    }
}
