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
    /// 관아 안개 연출 — 파티클(A) + 하늘 베일(D) 조립기. 멱등.
    /// **두 벌**을 만든다: 마을에서 올라올 때 걷히는 진입 안개와, 마을로 내려갈수록 짙어지는 하산 안개.
    /// 각자 파티클·베일·FogReveal 을 따로 갖는다(공유하면 MPB 를 매 프레임 서로 덮어쓴다).
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
        const string DownPartMatPath = MatDir + "/관아_하산안개파티클.mat";
        const string DownVeilMatPath = MatDir + "/관아_하산하늘베일.mat";
        const string GroupName = "관아_연출";

        // ── 하산 안개 (관아 → 마을) ──────────────────────────
        // 진입 안개와 **정반대 방향**이다: 언덕을 내려갈수록 짙어져, 전환 직전에는 아래 숲이
        // 전혀 안 보인다. 마을 씬으로 넘어가는 이음매를 가리는 게 목적이라 끝에서 거의 불투명해야 한다.
        //
        // ★왜 진입 안개와 한 컴포넌트로 못 쓰는가 (2026-08-21 원인 규명)
        //   원래는 FogReveal 하나를 monotonic=false 로 돌려 양방향을 겸했다(SceneLinkBuilder).
        //   그런데 한 컴포넌트는 **진행도→농도 곡선이 하나뿐**이라, 올라올 때와 내려갈 때가
        //   같은 값을 쓴다. 진입이 과하다는 지적에 농도를 0.60→0.34 로 내리자 하산도 같이 묽어졌고,
        //   게다가 이 메뉴가 연출 그룹을 통째로 다시 만들면서 monotonic 을 기본값(true)으로
        //   되돌려 **하산 연출 자체가 사라졌다.** (씬 파일에 monotonic: 1 로 굳어 있었다)
        //   → 진입용/하산용 FogReveal 을 따로 만들고, 하산용은 armAfterReachingEnd 게이트로
        //     "한 번 관아까지 올라간 뒤"에만 발동하게 해 서로 간섭하지 않게 했다.
        const float DownPartAlpha = 0.90f;      // 전환 직전 파티클 불투명도 — 사실상 시야 차단
        const float DownVeilAlpha = 0.98f;      // 하늘까지 안개색으로 덮는다
        const int DownPartCount = 24;           // 방출기당 입자 (8기 × 24 = 192)
        const float DownSizeMin = 16f, DownSizeMax = 30f;
        // 편집 모드에서만 보이는 값. 런타임에는 FogReveal 이 MPB 로 덮어쓴다.
        // 0.90 을 그대로 구우면 씬 뷰가 하얀 벽이 되어 편집을 못 한다.
        const float DownPreviewAlpha = 0.06f;
        // 방출기 z — 마루(−58)부터 골짜기 바닥(−86)까지 4m 간격. 지면을 샘플링해 얹는다.
        static readonly float[] DownZs = { -58f, -62f, -66f, -70f, -74f, -78f, -82f, -86f };
        // ★1차 시도에서 밟은 함정: 진입 안개와 같이 지면 +1.1 / 상자 높이 2.2 로 얹었더니
        //   **언덕 아래로 갈수록 안개가 오히려 옅어졌다.**
        //   언덕이 −58(y 0) → −84(y −12.2) 로 25° 나 떨어져서, 아래쪽 방출기의 얇은 상자가
        //   전부 **눈높이 밑**에 깔린다. z −73 에 서면 눈이 y −3.7 인데 z −76 방출기의 상자는
        //   −8.1 ~ −4.1 이라 시선이 상자 위를 지나갔다. 안개가 발밑에만 깔린 그림이 나온다.
        //   → 상자를 눈높이에 맞춰 **높이 12m** 로 세우고 중심을 지면 +3.5 에 둔다.
        //     그러면 어디에 서든(눈 = 지면 +1.7) 시선이 상자 한가운데를 지난다.
        const float DownYOffset = 3.5f;
        static readonly Vector3 DownBox = new Vector3(36f, 12f, 6f);
        /// <summary>
        /// ParticleSystemRenderer.maxParticleSize — **유니티 기본값 0.5 가 진짜 범인이었다.**
        /// 이 값은 "입자 하나가 화면에서 차지할 수 있는 최대 크기(뷰포트 비율)" 라서, 16~30m 짜리
        /// 큰 입자를 코앞 1~3m 에 둬도 화면의 절반까지밖에 못 덮는다. 그래서 불투명도를 0.90 으로
        /// 올리고 밀도를 3배(24→72)로 해도 아래 숲이 그대로 보였다. 이 값만 6 으로 풀면
        /// **밀도 24 그대로도 시야가 막힌다** (실측: 같은 자리에서 나무·산이 완전히 사라졌다).
        /// </summary>
        const float DownMaxParticleSize = 6f;
        /// <summary>
        /// 진행도 → 걷힘 정도 곡선의 지수. 1 = 선형, &lt;1 = 위쪽이 빨리 맑아진다(= 아래에서만 짙다).
        ///
        /// ★기본 EaseInOut 으로는 **중간이 이미 새하얬다.** 파티클이 여러 겹 겹치니 화면 불투명도는
        ///  알파에 선형이 아니라 1 − (1 − k·α)^겹수 로 붙는다 — 실측하면 α 0.35(구간 절반 지점)에서
        ///  이미 77% 라 산도 숲도 안 보였다. "점점 짙어진다"가 아니라 "금방 하얘진다"가 된다.
        ///  → 곡선을 t^0.35 로 눕혀 아래쪽 1/3 에서 몰아 짙어지게 한다.
        ///  겹수를 5로 잡고 0.55 로 시작했더니 실측이 목표보다 훨씬 앞섰다(−64 에서 이미 90%) —
        ///  방출기 8기가 4m 간격이라 시선 하나에 걸리는 겹이 5가 아니라 8 가까이 된다.
        ///  0.35 로 더 눕혀 실측 불투명도를 이렇게 맞췄다:
        ///      z −58(마루) 13% / −64 58% / −70 85% / −76.5(전환 직전) 99%+
        /// </summary>
        const float DownCurveExp = 0.35f;
        /// <summary>하산 구간: from = 짙은 끝(전환 트리거 z −77), to = 맑은 끝(마루 너머 −56).</summary>
        const float DownFromZ = -77f, DownToZ = -56f;

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
            // ── 확정값 재조정 (2026-08-20) ──
            // 그 사이 씬의 FogReveal 이 손으로 훨씬 짙게 바뀌어 있었다(0.5~7 / 베일 0.95 / 파티클 0.60).
            // 스폰에서 앞이 하얀 벽이라 "무엇이 있는지 모르겠다"를 넘어 "아무것도 없다"가 됐다.
            // → 그보다 약하게, 다만 옛 확정값(23.5~104)만큼 묽으면 스폰에서 외삼문(40m 앞)이
            //   그대로 보여 걷히는 연출이 성립하지 않는다. 그 사이를 잡는다:
            //     fogEnd 30 → 스폰에서 가시거리 30m. 언덕 아래 골짜기와 외삼문이 둘 다 가려진다.
            // ── 확정값 재조정 ② (2026-08-20 밤): Linear Fog 를 완전히 끄고 파티클 전용으로 ──
            // Fog 는 원리가 "이미 그려진 픽셀을 안개색으로 물들이기"라 물체 색을 안 바꿀 수가 없다.
            // 담장·동헌 색을 정확히 맞추려면 끄는 수밖에 없고, 그만큼 파티클이 안개감을 다 져야 한다.
            //   불투명도 0.40 → 0.58, 밀도 26 → 44, 크기도 키워 화면을 덮게 한다.
            //   fogStart/End 는 useLinearFog=false 라 쓰이지 않지만 값은 남겨 둔다(폴백용).
            // ── 재조정 ③ (2026-08-21): 파티클이 과했다 ──
            // Linear Fog 를 없앤 보상으로 0.58×44×6기까지 올렸더니 "안개가 여전히 강하다"가 됐다.
            // 방출기 6기(마루 너머까지 덮음)는 유지하고 불투명도·밀도만 내린다.
            _ => new Preset { fogStart = 3f, fogEnd = 30f, veilAlpha = 0.62f, partAlpha = 0.34f, partCount = 26, sizeMin = 14f, sizeMax = 26f },
        };

        [MenuItem("Tools/이문록/관아 ▸ 진입 안개 ▸ 확정 (중 × 1.2)")]
        public static void BuildFinal() => Build(Level.Final);
        [MenuItem("Tools/이문록/관아 ▸ 진입 안개 ▸ 비교용 ▸ 약")]
        public static void BuildSoft() => Build(Level.Soft);
        [MenuItem("Tools/이문록/관아 ▸ 진입 안개 ▸ 비교용 ▸ 중")]
        public static void BuildMedium() => Build(Level.Medium);
        [MenuItem("Tools/이문록/관아 ▸ 진입 안개 ▸ 비교용 ▸ 강")]
        public static void BuildStrong() => Build(Level.Strong);

        // ── 걷힌 뒤 최종 안개 (2026-08-20 변경) ──
        // 예전에는 마을 실측값(40~160)을 그대로 끝 상태로 삼았는데, 그러면 연출이 끝난 뒤에도
        // 마당 전체가 옅은 안개에 잠겨 담장·동헌 색이 씻긴다(관아 마당 대각선이 60m 남짓이라
        // 40~160 이면 마당 끝에서 안개 계수가 0.17 이나 된다).
        // → 사실상 안개가 없는 거리로 민다.
        //   실측 거리: 관아 마당 대각선 70m / 담장밖 숲 13~100m / 원경 산 190~376m.
        //   250~1500 으로 완전히 밀면 원경 산까지 쨍해져 배경이 판때기처럼 앞으로 튀어나온다.
        //   → 120~900. 관아 구조물·숲(≤100m)은 안개 계수 0, 원경 산만 0.09~0.33 으로 옅게 남는다.
        const float FogStartEnd = 120f, FogEndEnd = 900f;

        // ── 최종 조명 (2026-08-20 확정, B −40%) ──
        // SkyPreset 은 공용이라 못 고친다 → WorldTimeSync 의 씬 로컬 보정으로 이 씬에만 적용한다.
        const float SunFinal = 0.90f;        // 프리셋 1.10 → 0.90
        // 앰비언트는 동헌·지면 등 확산광 물체를 지배한다. ×0.60 에서 동헌 목재 휘도가
        // 팀원 렌더값(0.279)과 정확히 일치한다. 담장은 확산광이 아니라 하늘 반사가 지배하므로
        // 앰비언트로는 거의 움직이지 않는다(×0.4~1.0 에서 0.394→0.431).
        const float AmbientScaleFinal = 0.60f;

        // ── 하늘 반사 강도 = C안 확정 (2026-08-21) ──
        // 담장의 푸른 기, 목재의 회백색, 나뭇잎이 하얗게 뜨는 것이 전부 이 하나에서 나왔다.
        // A 1.00(기존) → B 0.55 → C 0.25 비교 후 C 확정. 씬 로컬이라 다른 씬에 영향 없다.
        const float ReflectionFinal = 0.25f;

        public static void Build(Level lv)
        {
            var p = Of(lv);

            // ⚠️ 끝 상태를 여기서도 반드시 못 박는다.
            //    FogReveal은 Awake 시점의 RenderSettings를 목표값으로 삼는데, 이 메뉴가 씬을
            //    저장하므로 그때 씬에 남아 있던 값이 그대로 굳는다. 실제로 하늘 프리셋 값
            //    (60~220)으로 밀려 저장된 적이 있다. 강도 메뉴만 눌러도 끝 상태가 보장되게 한다.
            // ★Linear Fog 는 아예 끈다 (파티클 전용). 켜 두면 거리를 아무리 밀어도 물체 색이 씻긴다.
            RenderSettings.fog = false;
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
            // 언덕 마루(−58)가 아니라 외삼문 앞(−40)에서 끝난다 —
            // 마루에서 끝내면 문에 다가서는 20m 구간이 여전히 안개에 잠긴 채 남았다.
            fr.toPoint = new Vector3(0f, 0f, GateZ - 6f);
            fr.flatten = true;
            fr.startFogStart = p.fogStart;
            fr.startFogEnd = p.fogEnd;
            fr.overrideStartColor = false;
            fr.maxClearSpeed = 0.55f;
            fr.fogParticles = systems;
            fr.particleStartAlpha = p.partAlpha;
            fr.skyVeil = veil;
            fr.skyVeilStartAlpha = p.veilAlpha;
            // 끝 상태를 못 박는다 — WorldTimeSync 와의 실행 순서에 좌우되지 않는다
            fr.useLinearFog = false;          // 파티클 전용
            fr.overrideEndFog = true;
            fr.endFogStart = FogStartEnd;
            fr.endFogEnd = FogEndEnd;
            // ★단조 증가를 **명시**한다. 기본값에 기대면 안 된다 —
            //   예전에 SceneLinkBuilder 가 이 값을 false 로 바꿔 놓았는데 이 메뉴가 그룹을
            //   새로 만들며 조용히 되돌려 버렸다. 하산 연출은 이제 아래 별도 컴포넌트가 맡는다.
            fr.monotonic = true;
            fr.armAfterReachingEnd = false;

            // ── 하산 안개 (관아 → 마을): 내려갈수록 짙어진다 ──
            var downSystems = BuildDescentFog(root.transform, sprite);

            // ── 씬 로컬 조명·안개 보정 (프리셋은 공용이라 못 건드린다) ──
            var wts = Object.FindFirstObjectByType<WorldTimeSync>();
            if (wts != null)
            {
                wts.sunIntensityOverride = SunFinal;
                wts.ambientScale = AmbientScaleFinal;
                wts.reflectionIntensityOverride = ReflectionFinal;
                RenderSettings.reflectionIntensity = ReflectionFinal;
                wts.fogForceOff = true;
                wts.fogDistanceOverride = false;
                wts.fogStartOverride = FogStartEnd;
                wts.fogEndOverride = FogEndEnd;
                EditorUtility.SetDirty(wts);

                // 편집 모드에서도 같은 그림이 보이도록 씬에 굳혀 둔다.
                // ★프리셋 값에서 곱한다 — 씬의 현재 앰비언트에 곱하면 메뉴를 누를 때마다 겹쳐 어두워진다.
                var day = wts.낮_맑음;
                if (day != null)
                {
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                    RenderSettings.ambientSkyColor = day.ambientSky * AmbientScaleFinal;
                    RenderSettings.ambientEquatorColor = day.ambientEquator * AmbientScaleFinal;
                    RenderSettings.ambientGroundColor = day.ambientGround * AmbientScaleFinal;
                }
                var sun = wts.sun != null ? wts.sun : Object.FindFirstObjectByType<Light>();
                if (sun != null && sun.type == LightType.Directional) sun.intensity = SunFinal;
            }
            else Debug.LogWarning("[관아] WorldTimeSync 를 못 찾아 조명 보정을 걸지 못했다");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"[관아] 진입 안개 [{lv}] — 파티클 {systems.Length}기×{p.partCount} / 하늘베일 α{p.veilAlpha:F2} / " +
                      $"보조 Linear {p.fogStart}~{p.fogEnd} → 끝 {FogStartEnd}~{FogEndEnd} (마을 값)" +
                      $" ‖ 하산 안개 — 파티클 {downSystems}기×{DownPartCount} α{DownPartAlpha:F2} / 베일 α{DownVeilAlpha:F2}" +
                      $" / 구간 z {DownFromZ}(짙음) → {DownToZ}(맑음)");
        }

        /// <summary>
        /// 하산 안개 한 벌(파티클 + 하늘 베일 + FogReveal)을 만든다. 방출기 수를 돌려준다.
        ///
        /// 진입 안개와 **공유하는 것이 하나도 없다** — 파티클도 베일도 머티리얼도 별개다.
        /// FogReveal 은 알파를 MaterialPropertyBlock 으로 렌더러에 직접 쓰므로, 같은 렌더러를
        /// 두 컴포넌트가 나눠 쓰면 매 프레임 서로 덮어써 값이 튄다.
        /// </summary>
        static int BuildDescentFog(Transform root, Texture2D sprite)
        {
            var mat = EnsureDownParticleMat(sprite);
            var veilMat = EnsureDownVeilMat();

            // 진입 베일(지름 900)과 겹치지 않게 조금 작게 — 같은 반경이면 z-파이팅이 난다
            var veil = BuildSkyVeil(root, veilMat, "하산_하늘베일", 860f);

            // 상자를 진입용(26×2.2×6)보다 훨씬 높게 세운다 — 이유는 DownYOffset 주석 참조.
            var systems = BuildFogEmitters(root, "하산_안개", mat, DownZs, DownPartCount,
                                           DownSizeMin, DownSizeMax, DownBox, DownYOffset, DownMaxParticleSize);

            var go = new GameObject("하산안개_FogReveal");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 0f, (DownFromZ + DownToZ) * 0.5f);
            var fr = go.AddComponent<FogReveal>();
            fr.arm = FogReveal.ArmMode.SceneEntry;
            fr.revealKey = "Gwana_ToVillage";
            // ★from 이 '짙은 끝'이다 — 진입 안개와 구간을 거꾸로 잡은 것이 곧 반대 방향 연출이다.
            fr.fromPoint = new Vector3(0f, 0f, DownFromZ);
            fr.toPoint = new Vector3(0f, 0f, DownToZ);
            fr.flatten = true;
            fr.monotonic = false;              // 위치를 그대로 따라간다 — 내려가면 다시 짙어져야 한다
            fr.armAfterReachingEnd = true;     // 관아까지 한 번 올라간 뒤에만 발동 (진입 연출과 분리)
            fr.curve = PowerCurve(DownCurveExp);
            fr.overrideStartColor = false;
            fr.maxClearSpeed = 0.55f;
            fr.fogParticles = systems;
            fr.particleStartAlpha = DownPartAlpha;
            fr.skyVeil = veil;
            fr.skyVeilStartAlpha = DownVeilAlpha;
            fr.useLinearFog = false;           // ★Linear Fog 금지 — 담장·동헌 픽셀 색이 씻긴다
            fr.overrideEndFog = true;
            fr.endFogStart = FogStartEnd;
            fr.endFogEnd = FogEndEnd;
            return systems.Length;
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

        /// <summary>t^exp 를 키 9개로 샘플링한 곡선. exp&lt;1 이면 초반이 가파르다.</summary>
        static AnimationCurve PowerCurve(float exp)
        {
            const int N = 9;
            var keys = new Keyframe[N];
            for (int i = 0; i < N; i++)
            {
                float t = i / (float)(N - 1);
                keys[i] = new Keyframe(t, Mathf.Pow(t, exp));
            }
            var c = new AnimationCurve(keys);
            for (int i = 0; i < N; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        /// <summary>하산 파티클 머티리얼. 알파는 **편집 프리뷰용 낮은 값**을 굽는다 (런타임은 MPB).</summary>
        static Material EnsureDownParticleMat(Texture2D sprite)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(DownPartMatPath);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, DownPartMatPath); }
            m.shader = sh;
            m.SetTexture("_BaseMap", sprite);
            var c = FogColor; c.a = DownPreviewAlpha;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_ZWrite", 0f);
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

        /// <summary>하산 하늘 베일 머티리얼. 알파는 프리뷰용 낮은 값 (런타임은 MPB).</summary>
        static Material EnsureDownVeilMat()
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(DownVeilMatPath);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, DownVeilMatPath); }
            m.shader = sh;
            var c = FogColor; c.a = DownPreviewAlpha;
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
        static Renderer BuildSkyVeil(Transform parent, Material mat, string name = "하늘베일", float diameter = 900f)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(parent, false);
            // 보행 구역(±32 / −78~42) 전체를 넉넉히 감싸는 반경 450m
            // ⚠️ 진입용·하산용 두 개를 놓으므로 지름을 달리해야 한다 — 같은 반경이면 두 구가
            //    동일 평면에 겹쳐 z-파이팅이 난다(둘 다 ZWrite Off / renderQueue 3000).
            sphere.transform.position = new Vector3(0f, 0f, (PlayZS + PlayZN) * 0.5f);
            sphere.transform.localScale = Vector3.one * diameter;
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
            // 방출기를 4기로 쪼갠 이유: 언덕이 약 20° 경사라 상자 하나로는 지면을 따라가지 못한다
            // 걷히는 구간이 외삼문 앞(−40)까지 늘었고 Linear Fog 도 없앴으므로,
            // 마루 너머 −52·−46 에도 방출기를 둬 그 구간이 갑자기 맑아지지 않게 한다.
            float[] zs = { SpawnZ + 0.5f, SpawnZ + 5.5f, SpawnZ + 10.5f, BrowZ - 0.5f, -52f, -46f };
            // maxParticleSize 는 유니티 기본값 0.5 그대로 — 지금 진입 안개 톤이 사용자 확정값이다.
            return BuildFogEmitters(parent, "언덕길_안개", mat, zs, p.partCount,
                                    p.sizeMin, p.sizeMax, new Vector3(26f, 2.2f, 6f), 1.1f, 0.5f);
        }

        /// <summary>
        /// 지면을 따라 늘어선 안개 방출기 한 벌. 진입(걷히는)·하산(짙어지는) 양쪽이 같이 쓴다.
        /// 알파는 굽지 않는다 — 런타임에는 FogReveal 이 MaterialPropertyBlock 으로 덮어쓰고,
        /// 편집 모드에서만 머티리얼에 구워 둔 값이 보인다.
        /// </summary>
        static ParticleSystem[] BuildFogEmitters(Transform parent, string groupName, Material mat,
                                                 float[] zs, int count, float sizeMin, float sizeMax,
                                                 Vector3 boxScale, float yOffset, float maxParticleSize)
        {
            var group = new GameObject(groupName);
            group.transform.SetParent(parent, false);

            var list = new List<ParticleSystem>();
            for (int i = 0; i < zs.Length; i++)
            {
                float z = zs[i];
                float gy = GwanaBuilder.SampleTerrainH(0f, z);
                var go = new GameObject($"안개_{i}");
                go.transform.SetParent(group.transform, false);
                go.transform.position = new Vector3(0f, gy + yOffset, z);

                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 12f;
                main.loop = true;
                main.prewarm = true;                       // Play 시작 순간부터 자욱하게
                main.startLifetime = 26f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(1f, 1f, 1f, 1f);   // 알파는 머티리얼/MPB가 관리
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = count;
                main.gravityModifier = 0f;

                var em = ps.emission;
                em.enabled = true;
                em.rateOverTime = count / main.startLifetime.constant;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = boxScale;

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
                // ★★ 유니티 기본값 0.5 는 **화면에 그려지는 입자 크기를 뷰포트의 50%로 잘라 버린다.**
                //   16~30m 짜리 큰 입자를 코앞(1~3m)에 둬도 화면을 못 덮는다 — 하산 안개를 아무리
                //   짙게(α 0.90) 밀도 3배로 올려도 나무·산이 그대로 보였던 진짜 원인이 이것이었다.
                //   (밀도 24→72 로 3배를 해도 안 되고, 이 값만 풀면 24개로도 시야가 막힌다)
                //   진입 안개는 지금 톤이 사용자 확정값이라 기본값 0.5 를 그대로 둔다.
                pr.maxParticleSize = maxParticleSize;

                go.AddComponent<FogParticleAutoPlay>();    // 에디터에서 Play 없이도 보이게
                list.Add(ps);
            }
            return list.ToArray();
        }
    }
}
