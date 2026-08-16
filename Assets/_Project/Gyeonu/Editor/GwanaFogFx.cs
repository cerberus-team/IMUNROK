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
    /// 관아 진입 안개 연출 — 파티클(A) + 하늘 베일(D) 조립기. 멱등.
    ///
    /// ■ 왜 Linear Fog만으로는 안 되는가
    ///   Unity 기본 안개는 픽셀 셰이더 마지막에 lerp(물체색, 안개색, f(거리)) 한 줄이다.
    ///   **화면에 이미 그려진 픽셀만 물들이고 빈 공간에는 아무것도 그리지 않는다.**
    ///   그래서 짙게 할수록 "안개 속"이 아니라 "회색으로 칠한 것"이 된다. 값으로는 못 넘는다.
    ///   → 공기 중에 실제 지오메트리를 띄우는 파티클이 주역이고, Linear Fog는 보조로 약하게만 남긴다.
    ///
    /// ■ 하늘 베일 (D)
    ///   Unity 안개는 스카이박스에 걸리지 않아 "지면은 뿌연데 하늘만 파란" 그림이 된다.
    ///   카메라를 감싸는 반경 450m 구를 안개색 반투명으로 덮어 **하늘 픽셀만** 물들인다.
    ///   가까운 지형·건물은 깊이 테스트에서 구보다 앞이라 영향을 받지 않는다.
    ///   ⚠️ 공유 스카이박스 머티리얼(FS003_Day)은 건드리지 않는다 — 별도 지오메트리다.
    ///
    /// ■ 에셋 의존 없음
    ///   파티클 스프라이트는 절차적으로 구워 쓴다(은하담 산_원경_Gradient 굽기와 같은 방식).
    ///   Gyeonu/**/*.png 는 gitignore라 커밋되지 않으므로 이 메뉴가 곧 재생성 수단이다.
    /// </summary>
    public static class GwanaFogFx
    {
        /// <summary>Final = 확정값(중 × 1.2). 약/중/강은 비교용 기준점으로 남겨 둔다.</summary>
        public enum Level { Soft, Medium, Strong, Final }

        const string TexDir = "Assets/_Project/Gyeonu/Art/Textures";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials";
        const string SpritePath = TexDir + "/안개_스프라이트.png";
        const string PartMatPath = MatDir + "/관아_안개파티클.mat";
        const string VeilMatPath = MatDir + "/관아_하늘베일.mat";
        const string GroupName = "관아_연출";

        // 걷힌 뒤 최종 안개 = 성하리 마을 실측값 (GwanaBuilder와 동일)
        static readonly Color FogColor = new Color(0.76f, 0.79f, 0.81f);

        // 강도 프리셋 — Linear Fog는 전부 "보조" 수준으로 약하게 잡는다 (끝 상태는 40~160)
        struct Preset
        {
            public float fogStart, fogEnd;     // 시작 시점의 Linear Fog
            public float veilAlpha;            // 하늘 베일 최대 알파
            public float partAlpha;            // 파티클 최대 알파
            public int partCount;              // 방출기 1개당 최대 입자
            public float sizeMin, sizeMax;
        }

        static Preset Of(Level lv) => lv switch
        {
            Level.Soft => new Preset { fogStart = 32f, fogEnd = 135f, veilAlpha = 0.30f, partAlpha = 0.10f, partCount = 14, sizeMin = 10f, sizeMax = 18f },
            Level.Medium => new Preset { fogStart = 26f, fogEnd = 112f, veilAlpha = 0.50f, partAlpha = 0.17f, partCount = 22, sizeMin = 12f, sizeMax = 22f },
            Level.Strong => new Preset { fogStart = 20f, fogEnd = 92f, veilAlpha = 0.70f, partAlpha = 0.26f, partCount = 30, sizeMin = 14f, sizeMax = 26f },

            // ── 확정값 (2026-08-17): 중 × 1.2 ──
            //  · 파티클 불투명도 0.17 → 0.204,  밀도 22 → 26   (선형 값이라 그대로 1.2배)
            //  · 하늘베일 0.50 → 0.60
            //  · Linear Fog: 거리를 1.2로 나누면(21.7~93) 사실상 '강'(20~92)이 되어 버린다.
            //    안개 "세기"의 실제 척도는 거리가 아니라 특정 거리에서의 안개 계수
            //    f = (d−start)/(end−start) 다. 23.5~104 로 잡으면
            //      d=40m: 0.163 → 0.205 (1.26배) / d=60m: 0.395 → 0.453 (1.15배)
            //    로 목표 1.2배에 들어온다. (강은 각각 1.70배·1.41배로 과하다)
            //  · 입자 크기는 손대지 않는다 — 지시가 "밀도·불투명도"였다.
            _ => new Preset { fogStart = 23.5f, fogEnd = 104f, veilAlpha = 0.60f, partAlpha = 0.204f, partCount = 26, sizeMin = 12f, sizeMax = 22f },
        };

        [MenuItem("Tools/이문록/관아 ▸ 진입 안개 ▸ 확정 (중 × 1.2)")]
        public static void BuildFinal() => Build(Level.Final);
        [MenuItem("Tools/이문록/관아 ▸ 진입 안개 ▸ 비교용 ▸ 약")]
        public static void BuildSoft() => Build(Level.Soft);
        [MenuItem("Tools/이문록/관아 ▸ 진입 안개 ▸ 비교용 ▸ 중")]
        public static void BuildMedium() => Build(Level.Medium);
        [MenuItem("Tools/이문록/관아 ▸ 진입 안개 ▸ 비교용 ▸ 강")]
        public static void BuildStrong() => Build(Level.Strong);

        // 걷힌 뒤 최종 Linear Fog = 성하리 마을 실측값 (BuildLighting과 동일)
        const float FogStartEnd = 40f, FogEndEnd = 160f;

        public static void Build(Level lv)
        {
            var p = Of(lv);

            // ⚠️ 끝 상태를 여기서도 반드시 못 박는다.
            //    FogReveal은 Awake 시점의 RenderSettings를 목표값으로 삼는데, 이 메뉴가 씬을
            //    저장하므로 그때 씬에 남아 있던 값이 그대로 굳는다. 실제로 하늘 프리셋 값
            //    (60~220)으로 밀려 저장된 적이 있다. 강도 메뉴만 눌러도 끝 상태가 보장되게 한다.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStartEnd;
            RenderSettings.fogEndDistance = FogEndEnd;
            var sprite = EnsureSprite();
            var partMat = EnsureParticleMat(sprite, p);
            var veilMat = EnsureVeilMat(p);

            var old = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == GroupName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(GroupName);

            // ── 하늘 베일 ──
            var veil = BuildSkyVeil(root.transform, veilMat);

            // ── 안개 파티클 (언덕길에 낮게) ──
            var systems = BuildRoadFog(root.transform, partMat, p);

            // ── FogReveal ──
            var go = new GameObject("진입안개_FogReveal");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(0f, 0f, (SpawnZ + BrowZ) * 0.5f);
            var fr = go.AddComponent<FogReveal>();
            fr.arm = FogReveal.ArmMode.SceneEntry;
            fr.revealKey = "Gwana_FromVillage";
            fr.fromPoint = new Vector3(0f, 0f, SpawnZ);
            fr.toPoint = new Vector3(0f, 0f, BrowZ);
            fr.flatten = true;
            fr.startFogStart = p.fogStart;
            fr.startFogEnd = p.fogEnd;
            fr.overrideStartColor = false;
            fr.maxClearSpeed = 0.55f;
            fr.fogParticles = systems;
            fr.particleStartAlpha = p.partAlpha;
            fr.skyVeil = veil;
            fr.skyVeilStartAlpha = p.veilAlpha;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"[관아] 진입 안개 [{lv}] — 파티클 {systems.Length}기×{p.partCount} / 하늘베일 α{p.veilAlpha:F2} / " +
                      $"보조 Linear {p.fogStart}~{p.fogEnd} → 끝 {FogStartEnd}~{FogEndEnd} (마을 값)");
        }

        // ── 파티클 스프라이트 굽기 ────────────────────────────
        static Texture2D EnsureSprite()
        {
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                    float dx = u - 0.5f, dy = v - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;          // 0 중심 → 1 가장자리
                    float edge = 1f - Smooth(0.18f, 1f, r);                 // 둥글고 아주 부드러운 감쇠
                    // 뭉게뭉게한 결 — 옥타브 3개 fbm
                    float n = 0.55f * Mathf.PerlinNoise(u * 3.1f + 11.3f, v * 3.1f + 4.7f)
                            + 0.30f * Mathf.PerlinNoise(u * 7.3f + 23.9f, v * 7.3f + 8.1f)
                            + 0.15f * Mathf.PerlinNoise(u * 15.7f + 5.5f, v * 15.7f + 31.2f);
                    float a = edge * Mathf.Clamp01(0.35f + 0.9f * n);
                    a = Mathf.Pow(Mathf.Clamp01(a), 1.5f);                  // 가장자리를 더 얇게
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(TexDir));
            System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(SpritePath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);

            var ti = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.maxTextureSize = 256;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SpritePath);
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(a, b, x));
            return t * t * (3f - 2f * t);
        }

        // ── 머티리얼 ─────────────────────────────────────────
        static Material EnsureParticleMat(Texture2D sprite, Preset p)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(PartMatPath);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, PartMatPath); }
            m.shader = sh;
            m.SetTexture("_BaseMap", sprite);
            var c = FogColor; c.a = p.partAlpha;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f);      // Transparent
            m.SetFloat("_Blend", 0f);        // Alpha
            m.SetFloat("_Cull", 0f);         // Off
            m.SetFloat("_ZWrite", 0f);
            // 소프트 파티클 — 지형과 만나는 절단선을 없앤다 (URP DepthTexture=True 확인함)
            m.SetFloat("_SoftParticlesEnabled", 1f);
            m.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            m.SetFloat("_SoftParticlesFarFadeDistance", 4f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_SOFTPARTICLES_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material EnsureVeilMat(Preset p)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(VeilMatPath);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, VeilMatPath); }
            m.shader = sh;
            var c = FogColor; c.a = p.veilAlpha;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 1f);         // Front — 구 안쪽에서 본다
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ── 하늘 베일 구 ─────────────────────────────────────
        static Renderer BuildSkyVeil(Transform parent, Material mat)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "하늘베일";
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(parent, false);
            // 보행 구역(±32 / −78~42) 전체를 넉넉히 감싸는 반경 450m
            sphere.transform.position = new Vector3(0f, 0f, (PlayZS + PlayZN) * 0.5f);
            sphere.transform.localScale = Vector3.one * 900f;
            var r = sphere.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return r;
        }

        // ── 언덕길 안개 파티클 ───────────────────────────────
        static ParticleSystem[] BuildRoadFog(Transform parent, Material mat, Preset p)
        {
            var group = new GameObject("언덕길_안개");
            group.transform.SetParent(parent, false);

            // 방출기를 4기로 쪼갠 이유: 언덕이 약 20° 경사라 상자 하나로는 지면을 따라가지 못한다
            float[] zs = { SpawnZ + 0.5f, SpawnZ + 5.5f, SpawnZ + 10.5f, BrowZ - 0.5f };
            var list = new List<ParticleSystem>();
            for (int i = 0; i < zs.Length; i++)
            {
                float z = zs[i];
                float gy = GwanaBuilder.SampleTerrainH(0f, z);
                var go = new GameObject($"안개_{i}");
                go.transform.SetParent(group.transform, false);
                go.transform.position = new Vector3(0f, gy + 1.1f, z);

                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 12f;
                main.loop = true;
                main.prewarm = true;                       // Play 시작 순간부터 자욱하게
                main.startLifetime = 26f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(p.sizeMin, p.sizeMax);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(1f, 1f, 1f, 1f);   // 알파는 머티리얼/MPB가 관리
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = p.partCount;
                main.gravityModifier = 0f;

                var em = ps.emission;
                em.enabled = true;
                em.rateOverTime = p.partCount / main.startLifetime.constant;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(26f, 2.2f, 6f);

                // 수명 양끝을 부드럽게 — 갑자기 나타나고 사라지면 눈에 띈다
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.22f),
                            new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(grad);

                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

                var sizeOL = ps.sizeOverLifetime;
                sizeOL.enabled = true;
                sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.25f));

                var pr = ps.GetComponent<ParticleSystemRenderer>();
                pr.sharedMaterial = mat;
                pr.renderMode = ParticleSystemRenderMode.Billboard;
                pr.sortMode = ParticleSystemSortMode.Distance;
                pr.alignment = ParticleSystemRenderSpace.View;
                pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pr.receiveShadows = false;
                pr.sortingFudge = 30f;                     // 지형·나무보다 뒤에서 그려지게

                go.AddComponent<FogParticleAutoPlay>();    // 에디터에서 Play 없이도 보이게
                list.Add(ps);
            }
            return list.ToArray();
        }
    }
}
