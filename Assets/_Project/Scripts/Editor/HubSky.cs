using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 조사청 바깥에 <b>하늘과 안개 고리</b>를 두른다.
    /// 메뉴: [이문록 ▸ 조사청 ▸ 하늘과 안개 두르기]
    ///
    /// <b>무엇을 하려는 것인가</b>: 문을 열고 마당에 나서서 하늘을 볼 수 있게 하는 것.
    /// 그러자면 세 가지가 한꺼번에 있어야 한다 — 볼 만한 하늘, 세상의 끝을 가리는
    /// 안개, 그리고 그 끝에 부딪히지 않고 돌아 나오는 길.
    ///
    /// <b>① 하늘</b>: 받아 둔 SkySeries 꾸러미의 <c>CoriolisNight4k</c> 를 쓴다.
    /// 달빛에 구름이 갈라진 밤하늘이라 씬의 달빛(Directional Light)·밤Volume 와 결이
    /// 맞는다. 꾸러미 재질을 그대로 물리지 않고 <b>제 재질을 따로 만든다</b> — 남의
    /// 꾸러미를 고쳐 놓으면 다시 받는 날 사라진다.
    /// 카메라의 지우기 방식도 함께 고친다. 여태 <c>SolidColor</c> 였다 — 하늘을
    /// 아무리 좋은 것으로 걸어도 카메라가 단색으로 덮어 버리니 보일 리가 없었다.
    ///
    /// <b>② 안개 고리</b>: 유니티의 안개는 <b>카메라에서 잰 거리</b>로 짙어진다.
    /// 그러니 걸어 나가도 맑은 자리는 나를 따라오고 안개는 늘 저 멀리에 있다.
    /// 그리려는 것은 그 반대 — 안개가 <b>조사청을 둘러 제자리에 있고</b> 걸어 나가면
    /// 내가 그 속으로 든다. 그래서 고리 모양 껍데기 둘을 세우고 안개 셰이더를 바른다.
    /// 안쪽 고리는 옅고 바깥 고리는 짙어, 둘 사이에 <b>깊이</b>가 생긴다.
    /// 거리 안개는 그대로 두되 빛깔을 하늘에 맞춘다 — 둘이 겹쳐야 한 덩이로 보인다.
    ///
    /// <b>③ 돌아 나오기</b>: <see cref="FogBoundary"/> 를 플레이어에 단다. 안개 속으로
    /// 걸어 들면 잿빛에 잠겼다가 눈을 뜨면 조사청이 앞에 있다. 자세한 까닭은 그쪽에 적었다.
    ///
    /// <b>땅을 다시 앉힌다</b>: 평지가 (27.8, 285.2)에 109×105 로 놓여 있어 조사청
    /// 동쪽으로는 <b>12m 만에 땅이 끝난다</b>. 담장 바로 너머다. 안개 고리를 반지름
    /// 몇으로 잡든 그 밖은 허공이라, 고리를 두르기 전에 땅부터 조사청 밑으로 옮기고
    /// 150m 로 넓힌다. 무늬 되풀이도 커진 만큼 다시 잡는다.
    /// ※ 손으로 옮겨 두신 자리를 되돌리는 유일한 대목이다. 마음에 안 들면 Ctrl+Z 한 번.
    ///
    /// ★플레이를 멈추고 실행할 것.
    /// </summary>
    public static class HubSky
    {
        private const string RoomName = "조사청_실내";
        private const string FieldName = "조사청_들판";
        private const string FogRoot = "조사청_안개";

        private const string SkySrc = "Assets/SkySeries Freebie/CoriolisNight4k.mat";
        private const string SkyMat = "Assets/_Project/Art/Materials/M_하늘_조사청밤.mat";
        private const string FogShader = "이문록/안개벽";
        private const string MatDir = "Assets/_Project/Art/Materials";
        private const string MeshDir = "Assets/_Project/Art/Meshes";
        private const string RingMesh = MeshDir + "/안개고리.asset";

        // ── 땅 ──
        private const float GroundY = 136.95f;   // 기단이 앉는 높이(HubField 와 같은 값)
        private const float Ground = 150f;       // 평지 한 변(m)

        // ── 고리 ──
        // 안쪽 고리를 지나고도 한참 더 걸어야 붙잡히게 한다. 고리에 닿자마자 되돌리면
        // 벽에 부딪힌 것이 되고, 그러면 안개로 만든 뜻이 없다.
        private const float InnerR = 44f;
        private const float OuterR = 55f;
        private const float FootY = 136.0f;      // 안개 밑동 — 풀뿌리보다 한 자쯤 아래
        private const float InnerH = 24f;
        private const float OuterH = 30f;

        // ── 거리 안개 ──
        // 하늘의 지평 언저리 빛깔에 맞춘 것이다. 여기가 어긋나면 안개 고리만 허옇게
        // 떠서 커튼처럼 보인다.
        // 끝나는 거리는 <b>안쪽 고리보다 앞</b>이어야 한다. 처음엔 62m 로 두었는데,
        // 그러면 40m 밖의 땅이 아직 4할밖에 안 흐려서 땅과 하늘이 맞닿는 곧은 금이
        // 그대로 남았다 — 안개를 두르고도 지평선이 보였다. 48m 에서 다 묻히게 하면
        // 고리에 닿기 전에 땅이 먼저 안개가 된다.
        private static readonly Color FogColor = new Color(0.34f, 0.38f, 0.45f);
        private const float FogStart = 14f;
        private const float FogEnd = 40f;

        [MenuItem("이문록/조사청/하늘과 안개 두르기")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[하늘] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var room = GameObject.Find(RoomName);
            if (room == null) { Debug.LogError("[하늘] " + RoomName + " 을 못 찾았습니다."); return; }
            Vector3 center = room.transform.position;

            var log = new StringBuilder();
            Sky(log);
            Ground_(center, log);
            Ring(center, log);
            DistanceFog(log);
            Boundary(center, log);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(room.scene);
            Debug.Log("[하늘과 안개]\n" + log);
        }

        // ── ① 하늘 ────────────────────────────────────────────

        private static void Sky(StringBuilder log)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMat);
            if (mat == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>(SkySrc);
                if (src == null)
                {
                    log.AppendLine("   ✘ " + SkySrc + " 이 없습니다(하늘 꾸러미는 아트라 git 밖입니다).");
                    return;
                }
                var shader = Shader.Find("Skybox/Cubemap");
                if (shader == null) { log.AppendLine("   ✘ Skybox/Cubemap 셰이더가 없습니다."); return; }

                EnsureFolder(MatDir);
                mat = new Material(shader);
                if (src.HasProperty("_Tex")) mat.SetTexture("_Tex", src.GetTexture("_Tex"));

                // 밤이되 새까맣지는 않게. 노출을 조금 올려 구름의 결이 보이게 하고,
                // 푸른 쪽으로 살짝 기울여 달빛(Directional Light)과 같은 색온도로 맞춘다.
                mat.SetColor("_Tint", new Color(0.62f, 0.66f, 0.78f));
                mat.SetFloat("_Exposure", 1.15f);
                // 구름이 갈라진 자리가 남쪽 마당 위로 오게 돌린다. 문을 열고 나서면
                // 정면이 트여 있어야 "하늘 구경"이 된다.
                mat.SetFloat("_Rotation", 205f);

                AssetDatabase.CreateAsset(mat, SkyMat);
                AssetDatabase.SaveAssets();
                log.AppendLine("   하늘 재질을 만들었습니다 → " + SkyMat);
            }

            RenderSettings.skybox = mat;
            // ambientMode 는 손대지 않는다 — WorldStateController 가 Flat 모드의
            // ambientLight 를 흔들어 '세계가 열리는' 연출을 만든다. Skybox 모드로
            // 바꾸면 그 값이 통째로 무시되어 그 연출이 죽는다.
            DynamicGI.UpdateEnvironment();

            int fixedCam = 0;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.clearFlags == CameraClearFlags.Skybox) continue;
                Undo.RecordObject(cam, "카메라 하늘 보이게");
                cam.clearFlags = CameraClearFlags.Skybox;
                fixedCam++;
            }
            log.AppendLine("   하늘을 걸었습니다: " + mat.name
                           + (fixedCam > 0 ? "  · 카메라 " + fixedCam + "대의 지우기 방식을 Skybox 로 고쳤습니다(여태 단색이라 하늘이 안 보였습니다)" : ""));
        }

        // ── ② 땅 ────────────────────────────────────────────

        private static void Ground_(Vector3 center, StringBuilder log)
        {
            var field = GameObject.Find(FieldName);
            var t = field != null ? field.transform.Find("평지") : null;
            if (t == null) { log.AppendLine("   ✘ 평지를 못 찾아 그대로 둡니다."); return; }

            Vector3 was = t.position;
            Vector3 wasScale = t.localScale;
            Undo.RecordObject(t, "평지 다시 앉히기");
            t.position = new Vector3(center.x, GroundY - 0.5f, center.z);
            t.localScale = new Vector3(Ground, 1f, Ground);

            // 무늬는 제 크기에 맞춰 되풀이시킨다. 안 그러면 10m 짜리 풀잎이 된다.
            var r = t.GetComponent<Renderer>();
            if (r != null)
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                var st = new Vector4(Ground * 0.25f, Ground * 0.25f, 0f, 0f);
                mpb.SetVector(Shader.PropertyToID("_BaseMap_ST"), st);
                mpb.SetVector(Shader.PropertyToID("_MainTex_ST"), st);
                r.SetPropertyBlock(mpb);
            }

            log.AppendLine("   평지를 조사청 밑으로 옮기고 넓혔습니다: "
                           + wasScale.x.ToString("F0") + "×" + wasScale.z.ToString("F0")
                           + " @" + was.ToString("F1") + "  →  " + Ground + "×" + Ground
                           + " @" + t.position.ToString("F1"));
            log.AppendLine("     (동쪽으로 12m 만에 땅이 끝나 있었습니다. 안개 고리를 두를 자리가 없었습니다)");
        }

        // ── ③ 안개 고리 ──────────────────────────────────────

        private static void Ring(Vector3 center, StringBuilder log)
        {
            var shader = Shader.Find(FogShader);
            if (shader == null) { log.AppendLine("   ✘ 셰이더 " + FogShader + " 를 못 찾았습니다."); return; }

            var mesh = RingMeshAsset();
            if (mesh == null) { log.AppendLine("   ✘ 고리 메시를 못 만들었습니다."); return; }

            var old = GameObject.Find(FogRoot);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(FogRoot);
            Undo.RegisterCreatedObjectUndo(root, "안개 고리 두르기");
            root.transform.position = new Vector3(center.x, FootY, center.z);

            // 안쪽은 옅게 — 여기는 아직 걸어 다니는 자리다. 짙으면 마당이 답답해진다.
            // 그래도 <b>속이 비쳐서는 안 된다</b>. 0.62 로 두었더니 안개 너머의 지평선이
            // 그대로 읽혔다 — 가리라고 세운 것이 무늬가 되어 버린다.
            Shell(root.transform, mesh, shader, "안개_안", InnerR, InnerH,
                  new Color(0.52f, 0.57f, 0.64f), 0.88f, 2.1f, 0.030f, 0.50f);
            // 바깥은 짙게 — 여기가 세상의 끝이다. 너머가 비쳐 보이면 안 된다.
            // 결(무늬)도 약하게 준다. 짙은 벽에 결이 세면 안개가 아니라 커튼이 된다.
            Shell(root.transform, mesh, shader, "안개_밖", OuterR, OuterH,
                  new Color(0.44f, 0.49f, 0.57f), 1.0f, 1.25f, 0.018f, 0.30f);

            log.AppendLine("   안개 고리 두 겹: 반지름 " + InnerR + "m(옅게) · " + OuterR + "m(짙게)");
        }

        private static void Shell(Transform parent, Mesh mesh, Shader shader, string name,
                                  float radius, float height, Color color, float alpha,
                                  float curve, float scale, float wisp)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(radius, height, radius);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            string path = MatDir + "/M_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                EnsureFolder(MatDir);
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_Color", color);
            m.SetFloat("_Alpha", alpha);
            m.SetFloat("_FootY", FootY);
            m.SetFloat("_Height", height);
            m.SetFloat("_Curve", curve);
            m.SetFloat("_Scale", scale);
            m.SetFloat("_Speed", 0.35f);
            m.SetFloat("_Wisp", wisp);
            EditorUtility.SetDirty(m);
            r.sharedMaterial = m;
        }

        /// <summary>
        /// 고리 껍데기 하나. 반지름 1, 높이 0~1 짜리 <b>뚜껑 없는</b> 원통이다.
        ///
        /// 유니티가 주는 Cylinder 를 쓸 수 없다 — 그것은 위아래에 뚜껑이 있어서,
        /// 짙기를 높이로 매기는 이 셰이더에서는 아랫뚜껑이 <b>땅을 통째로 덮는</b>
        /// 원판이 된다. 발밑이 안개로 칠해진다.
        ///
        /// 한 번 만들어 파일로 저장하고 그다음부터는 그것을 쓴다. 코드가 지어 낸
        /// 메시를 저장하지 않고 물려 두면 씬을 다시 열 때 사라진다.
        /// </summary>
        private static Mesh RingMeshAsset()
        {
            var had = AssetDatabase.LoadAssetAtPath<Mesh>(RingMesh);
            if (had != null) return had;

            const int Seg = 72;
            const int Rings = 4;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int r = 0; r <= Rings; r++)
            {
                float v = r / (float)Rings;
                for (int s = 0; s <= Seg; s++)
                {
                    float a = s / (float)Seg * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a), v, Mathf.Sin(a)));
                    uvs.Add(new Vector2(s / (float)Seg, v));
                }
            }

            var tris = new List<int>();
            int stride = Seg + 1;
            for (int r = 0; r < Rings; r++)
                for (int s = 0; s < Seg; s++)
                {
                    int i0 = r * stride + s, i1 = i0 + 1;
                    int i2 = i0 + stride, i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }

            var mesh = new Mesh { name = "안개고리" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            EnsureFolder(MeshDir);
            AssetDatabase.CreateAsset(mesh, RingMesh);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<Mesh>(RingMesh);
        }

        // ── ④ 거리 안개 ──────────────────────────────────────

        private static void DistanceFog(StringBuilder log)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            log.AppendLine("   거리 안개: " + FogStart + "~" + FogEnd + "m · 빛깔을 하늘의 지평에 맞췄습니다");
        }

        // ── ⑤ 경계 ──────────────────────────────────────────

        private static void Boundary(Vector3 center, StringBuilder log)
        {
            var cam = Camera.main;
            if (cam == null) { log.AppendLine("   ✘ 메인 카메라가 없어 경계를 못 달았습니다."); return; }

            var player = cam.transform.root.gameObject;
            var fb = player.GetComponent<FogBoundary>();
            if (fb == null) fb = Undo.AddComponent<FogBoundary>(player);
            Undo.RecordObject(fb, "안개 경계");
            // 붙잡는 자리는 <b>두 고리 사이</b>다. 안쪽 고리(44m)를 지나 안개 속으로
            // 들어선 뒤라야 "홀려서 돌아 나왔다"가 되고, 바깥 고리(55m)에 닿기 전이라야
            // 세상의 끝을 눈으로 보지 않는다.
            float hard = (InnerR + OuterR) * 0.5f;      // 49.5m
            float soft = InnerR - 10f;                  // 34m — 여기서부터 안개가 조여 온다
            float back = InnerR - 14f;                  // 30m — 되돌려 세우는 자리
            fb.Configure(center, soft, hard, back, new Vector2(FogStart, FogEnd));
            EditorUtility.SetDirty(fb);
            log.AppendLine("   안개 경계를 " + player.name + " 에 달았습니다 — "
                           + soft + "m 부터 조여 오고 " + hard + "m 에서 " + back + "m 로 돌려세웁니다");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
