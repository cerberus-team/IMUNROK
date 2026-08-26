using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>재를 가루로 만든다.</b>
    /// 메뉴: [이문록 ▸ 아궁이 ▸ 재를 가루로]
    ///
    /// 앞서 아궁이를 고칠 때 <b>움직임</b>만 손봤다 — 옆으로 젓고, 눌려 얇아지고,
    /// 먼지가 인다. 그런데도 「가루 느낌이 전혀 없다」는 말이 그대로 남았다.
    /// 재 보니 <b>움직임의 문제가 아니었다</b>.
    ///
    /// 지금 재는 이렇게 생겼다:
    ///   · <b>납작하게 눌린 구(球) 아홉 개</b> (0.13~0.23m 짜리, 두께 2cm)
    ///   · 재질에 <b>요철 맵이 없다</b> — 바탕 그림 한 장뿐이다
    ///
    /// 구는 아무리 눌러도 <b>매끈한 둔덕</b>이다. 빛이 그 위를 고르게 훑고 지나가니
    /// 알갱이가 설 자리가 없다. 가루로 보이려면 <b>빛이 알갱이마다 걸려 넘어져야</b>
    /// 하는데, 걸릴 것이 하나도 없었던 것이다.
    ///
    /// 그래서 셋을 한다. <b>배치는 안 건드린다</b> — 있는 둔덕 아홉은 그대로 두고
    /// 그 위에 얹는다.
    ///
    ///   ① <b>알갱이 결을 구워 씌운다</b> — 잔 요철 맵을 만들어 재 재질에 물린다.
    ///      한 장으로 재질 둘이 한꺼번에 거칠어진다. 이것이 값이 가장 싸고 효과가 크다.
    ///   ② <b>잔 알갱이를 뿌린다</b> — 아주 잘고 납작한 덩이 수십을 둔덕 위에 흩는다.
    ///      실루엣의 가장자리가 톱니처럼 되어야 「쌓인 가루」로 읽힌다.
    ///      숯 부스러기도 몇 섞는다 — 재는 온통 회색이 아니다.
    ///   ③ <b>헤집으면 가루가 튄다</b> — 지금 먼지는 <c>T_Smoke_Soft</c> 를 쓴다.
    ///      그건 <b>연기</b>지 가루가 아니다. 무겁고 잘아서 <b>떨어지는</b> 알갱이를
    ///      따로 만들어 건다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class AshGrain
    {
        private const string TexDir = "Assets/_Project/Onggojip/Art/Textures";
        private const string NormalPath = TexDir + "/T_재알갱이_N.png";
        private const string GritName = "재가루";

        /// <summary>흩뿌릴 잔 알갱이 수. 너무 많으면 둔덕이 안 보이고, 적으면 티가 안 난다.</summary>
        private const int Grains = 120;

        [MenuItem("이문록/아궁이/재를 가루로")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[아궁이] 재생을 멈추고 다시 누르십시오.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var log = new System.Text.StringBuilder("[아궁이] 재를 가루로\n");

            var rake = Object.FindFirstObjectByType<AshRake>(FindObjectsInactive.Include);
            if (rake == null) { Debug.LogWarning(log + "── 이 씬에 아궁이가 없다"); return; }

            var so = new SerializedObject(rake);
            var covered = so.FindProperty("_hinge").objectReferenceValue as Transform;   // 재_덮인
            var raked = so.FindProperty("_after").objectReferenceValue as GameObject;    // 재_헤집힌
            if (covered == null) { Debug.LogWarning(log + "── 덮인 재를 못 찾았다"); return; }

            // ── ① 알갱이 결 ─────────────────────────
            var normal = BakeGrainNormal(log);
            int dressed = 0;
            foreach (var r in covered.GetComponentsInChildren<Renderer>(true)) dressed += Dress(r, normal);
            if (raked != null)
                foreach (var r in raked.GetComponentsInChildren<Renderer>(true)) dressed += Dress(r, normal);
            log.AppendLine("── 재 재질 " + dressed + "가지에 알갱이 결을 물렸다 (되풀이 6×6 — 알갱이 하나가 2cm 남짓)");

            // ── ② 잔 알갱이 ─────────────────────────
            int sown = Sow(covered, log);
            log.AppendLine("── 둔덕 위에 잔 알갱이 " + sown + "알을 흩었다 (숯 부스러기 섞음)");

            // ── ③ 튀는 가루 ─────────────────────────
            var grit = MakeGrit(rake.transform, log);
            so.Update();
            var gritProp = so.FindProperty("_grit");
            if (gritProp != null) { gritProp.objectReferenceValue = grit; so.ApplyModifiedPropertiesWithoutUndo(); }
            else log.AppendLine("── ⚠ AshRake 에 _grit 칸이 없다 — 스크립트가 최신인지 보시오");

            EditorUtility.SetDirty(rake);
            EditorSceneManager.MarkDirty(scene);
            Debug.Log(log.ToString());
            Selection.activeTransform = covered;
        }

        private static class EditorSceneManager
        {
            public static void MarkDirty(Scene s)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s);
            }
        }

        // ───────── ① 알갱이 결 ─────────

        /// <summary>
        /// 잔 알갱이의 요철 맵을 굽는다.
        ///
        /// <b>그림을 그리지 않고 높이를 짓는다</b>: 굵기가 다른 잡음 세 겹을 겹쳐
        /// 높이밭을 만들고, 그 기울기를 법선으로 바꾼다. 굵은 겹은 재가 뭉친 결이고
        /// 잔 겹이 알갱이다. 한 겹만 쓰면 사포처럼 고르게 거칠어 <b>가루가 아니라
        /// 까끌한 판</b>이 된다.
        ///
        /// 이어 붙어야 하므로 잡음은 <b>둘러 감는</b> 좌표로 뽑는다 — 안 그러면
        /// 되풀이 자국이 격자로 드러난다.
        /// </summary>
        private static Texture2D BakeGrainNormal(System.Text.StringBuilder log)
        {
            // <b>있어도 다시 굽는다.</b> 한 번 잘못 구워 놓고 "이미 있다"고 넘어가면,
            // 도구를 고쳐도 화면은 옛 결 그대로다. 굽는 값이 싸므로 늘 새로 굽는다.

            if (!Directory.Exists(TexDir)) Directory.CreateDirectory(TexDir);

            const int N = 512;
            var h = new float[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    // 세 겹: 뭉친 결(14) . 알갱이(52) . 아주 잔 티끌(140)
                    float a = Value(u, v, 14) * 0.34f;
                    float b = Value(u, v, 52) * 0.40f;
                    float c = Value(u, v, 140) * 0.26f;
                    h[y * N + x] = a + b + c;
                }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true, true);
            var px = new Color32[N * N];
            // 기울기 → 법선. 세기를 키우면 알갱이가 도드라지되 지나치면 기름진 자갈이 된다.
            const float strength = 3.2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float hl = h[y * N + ((x - 1 + N) % N)];
                    float hr = h[y * N + ((x + 1) % N)];
                    float hd = h[((y - 1 + N) % N) * N + x];
                    float hu = h[((y + 1) % N) * N + x];
                    var n = new Vector3((hl - hr) * strength, (hd - hu) * strength, 1f).normalized;
                    px[y * N + x] = new Color32(
                        (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            tex.SetPixels32(px);
            tex.Apply(true);

            File.WriteAllBytes(NormalPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(NormalPath, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(NormalPath);
            imp.textureType = TextureImporterType.NormalMap;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.SaveAndReimport();

            log.AppendLine("── 알갱이 결을 구웠다 " + NormalPath + " (잡음 세 겹 — 뭉친 결·알갱이·티끌)");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        }

        /// <summary>
        /// 둘러 감는 값잡음 한 겹. 이어 붙는 자국이 안 생긴다.
        ///
        /// <b>사인·코사인을 곱해 쓰면 안 된다.</b> 처음에 그렇게 짰더니 값이 <b>격자</b>로
        /// 규칙적으로 뜨고, 그것이 구(球)의 UV 를 타면서 동심원으로 감겨 재가 아니라
        /// <b>대바구니 짜임</b>처럼 보였다. 가루는 규칙이 없어야 가루다.
        /// 그래서 자리마다 값을 뒤섞어 뽑고(해시) 그 사이를 부드럽게 잇는다.
        /// </summary>
        private static float Value(float u, float v, int period)
        {
            float fx = u * period, fy = v * period;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);          // 부드럽게 — 각지면 격자가 도로 보인다
            ty = ty * ty * (3f - 2f * ty);
            int x1 = Wrap(x0 + 1, period), y1 = Wrap(y0 + 1, period);
            x0 = Wrap(x0, period); y0 = Wrap(y0, period);
            float a = Mathf.Lerp(Hash(x0, y0), Hash(x1, y0), tx);
            float b = Mathf.Lerp(Hash(x0, y1), Hash(x1, y1), tx);
            return Mathf.Lerp(a, b, ty);
        }

        private static int Wrap(int v, int n) { v %= n; return v < 0 ? v + n : v; }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f;
            }
        }

        private static int Dress(Renderer r, Texture2D normal)
        {
            var m = r.sharedMaterial;
            if (m == null || normal == null) return 0;
            // <b>재만 거칠게 한다.</b> 처음에 밑에 든 것까지 훑었더니 잉걸불과
            // <b>서찰 조각</b>에도 재 결이 씌워졌다 — 종이가 자갈밭이 되었다.
            // 이름으로 가른다: 재질 이름에 「재」가 든 것만.
            if (m.name.IndexOf("재") < 0) return 0;
            if (!m.HasProperty("_BumpMap")) return 0;
            if (m.GetTexture("_BumpMap") == normal) return 0;      // 이미 물렸다

            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_BumpMap", normal);
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1.6f);
            // 되풀이는 <b>바탕 그림과 따로</b> 잡는다. 알갱이는 바탕 그림보다 훨씬 잘아야 한다.
            m.SetTextureScale("_BumpMap", new Vector2(6f, 6f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.02f);
            EditorUtility.SetDirty(m);
            return 1;
        }

        // ───────── ② 잔 알갱이 ─────────

        private static int Sow(Transform covered, System.Text.StringBuilder log)
        {
            // 두 번 눌러도 두 벌이 안 서게 먼저 걷는다
            for (int i = covered.childCount - 1; i >= 0; i--)
                if (covered.GetChild(i).name.StartsWith("재알_"))
                    Object.DestroyImmediate(covered.GetChild(i).gameObject);

            // 둔덕에서 재질을 빌려 온다 — 새 재질을 만들면 색이 따로 논다
            Material ash = null, cold = null;
            foreach (var r in covered.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial == null) continue;
                if (r.sharedMaterial.name.Contains("식은")) cold = r.sharedMaterial;
                else ash = r.sharedMaterial;
            }
            if (ash == null) ash = cold;
            if (ash == null) { log.AppendLine("── ⚠ 재 재질을 못 찾아 알갱이를 못 뿌렸다"); return 0; }

            var char̲ = CharcoalMat(ash);

            // 둔덕이 덮은 넓이를 재서 그 안에만 흩는다
            Bounds b = new Bounds(covered.position, Vector3.zero); bool first = true;
            foreach (var r in covered.GetComponentsInChildren<Renderer>(true))
            { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }

            var rnd = new System.Random(20260827);
            for (int i = 0; i < Grains; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "재알_" + i;
                Object.DestroyImmediate(go.GetComponent<Collider>());   // 짚을 것이 아니다
                go.transform.SetParent(covered, true);

                // 가장자리로 갈수록 성기게 — 한가운데가 두툼해야 「쌓인」 것으로 보인다
                float rr = Mathf.Sqrt((float)rnd.NextDouble());
                float th = (float)rnd.NextDouble() * 6.283185f;
                // 반쯤 파묻힌다. 온전히 얹히면 <b>위에 놓인 것</b>이 되고,
                // 반쯤 잠겨야 <b>쌓인 것</b>이 된다.
                go.transform.position = new Vector3(
                    b.center.x + Mathf.Cos(th) * rr * b.extents.x * 0.95f,
                    b.max.y - 0.001f - (float)rnd.NextDouble() * 0.004f,
                    b.center.z + Mathf.Sin(th) * rr * b.extents.z * 0.95f);

                // <b>훨씬 잘고 훨씬 납작하게.</b> 처음에 지름 1.4~4.2cm 로 뿌렸더니
                // 가루가 아니라 <b>자갈</b>이 되었다 — 둔덕이 13~23cm 인데 그 위에
                // 3cm 짜리 공이 얹히면 누가 봐도 돌멩이다.
                // 4~11mm 에 두께는 그 5분의 1. 하나하나는 거의 안 보이고
                // <b>가장자리의 톱니</b>와 <b>결의 얼룩</b>으로만 읽힌다 — 그것이 가루다.
                float d = 0.004f + (float)rnd.NextDouble() * 0.007f;
                go.transform.localScale = new Vector3(d, d * (0.16f + (float)rnd.NextDouble() * 0.14f), d);
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);

                // 열에 하나쯤은 숯 부스러기. 재는 온통 회색이 아니다.
                var pick = rnd.NextDouble();
                go.GetComponent<Renderer>().sharedMaterial =
                    pick < 0.12 ? char̲ : (pick < 0.55 && cold != null ? cold : ash);
            }
            return Grains;
        }

        /// <summary>숯 부스러기 재질. 재 재질을 베껴 색만 낮춘다 — 결과 매끄러움을 같이 물려받는다.</summary>
        private static Material CharcoalMat(Material from)
        {
            const string path = "Assets/_Project/Art/Materials/M_숯부스러기.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(from);
                AssetDatabase.CreateAsset(m, path);
            }
            var ink = new Color(0.085f, 0.078f, 0.075f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", ink);
            if (m.HasProperty("_Color")) m.SetColor("_Color", ink);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.06f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ───────── ③ 튀는 가루 ─────────

        /// <summary>
        /// 헤집을 때 <b>떨어지는</b> 알갱이.
        ///
        /// 지금 먼지(<c>재먼지</c>)는 <c>T_Smoke_Soft</c> — 뭉게뭉게 퍼지는 <b>연기</b>다.
        /// 연기는 떠오르고 가루는 떨어진다. 둘은 같은 것이 아니므로 하나로 못 쓴다.
        /// 여기 것은 <b>작고, 또렷하고, 중력을 받고, 금세 가라앉는다</b>.
        /// </summary>
        private static ParticleSystem MakeGrit(Transform host, System.Text.StringBuilder log)
        {
            var had = host.Find(GritName);
            if (had != null) { log.AppendLine("── 튀는 가루는 이미 있다 — 값만 다시 잡는다"); }
            else
            {
                var go = new GameObject(GritName);
                go.transform.SetParent(host, false);
                had = go.transform;
                log.AppendLine("── 튀는 가루를 만들었다");
            }

            var ps = had.GetComponent<ParticleSystem>();
            if (ps == null) ps = had.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.013f);   // 알갱이다 — 잘아야 한다
            main.gravityModifier = 0.75f;                                     // 떠오르지 않고 떨어진다
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.78f, 0.76f, 0.72f), new Color(0.24f, 0.22f, 0.21f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;     // 우리가 직접 뿜는다
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 42f;            // 흩어지되 퍼지지는 않게
            sh.radius = 0.10f;
            sh.rotation = new Vector3(-90f, 0f, 0f);   // 위로 튄다

            // 연기 재질을 그대로 쓰면 또 뭉게뭉게가 된다. 알갱이는 <b>또렷한 점</b>이라야 한다.
            var rr = ps.GetComponent<ParticleSystemRenderer>();
            rr.renderMode = ParticleSystemRenderMode.Billboard;
            rr.sharedMaterial = GritMat();
            rr.sortingOrder = 1;

            var col = ps.colorOverLifetime; col.enabled = false;               // 안 흐려진다 — 가라앉을 뿐이다
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-3.2f, 3.2f);

            return ps;
        }

        private static Material GritMat()
        {
            const string path = "Assets/_Project/Art/Materials/M_재가루.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            // 바탕 그림을 안 준다 — 그림이 없으면 네모난 <b>점</b>이 되고, 그게 알갱이에 맞다.
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
