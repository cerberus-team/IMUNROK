using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 씬의 시간대를 <see cref="GyeonuWorld"/>(= 성하리 마을 기준)에 맞춘다.
    ///
    /// 씬마다 시간대를 표현하는 방법이 다르므로 두 갈래로 처리한다:
    ///   야외 — 하늘 프리셋(SkyPreset) 4종 중 하나를 방향광·안개·스카이박스에 통째로 적용
    ///   실내 — 스카이박스가 무의미하므로 <see cref="OfficeTimeOfDay"/> 의 낮/밤 조명 그룹을 전환
    ///          (창호지 재질까지 갈리므로 창밖 밝기가 함께 따라온다)
    ///
    /// ■ 기준 씬(마을)에서는 반대로 '읽는다'
    ///   <see cref="isReference"/> 를 켜면 씬에 저장된 상태를 세계 상태로 **올려보낸다**.
    ///   마을에서 밤으로 바꿔 두고 다른 씬으로 가면 그 밤이 따라가게 하기 위함이다.
    ///
    /// ■ 관측실·서고를 제외한 이유
    ///   서고는 지하라 창이 없고, 관측실은 혼상 별 투영이 성립해야 하는 공간이라
    ///   낮으로 바뀌면 연출 자체가 무너진다. 두 곳은 이 컴포넌트를 붙이지 않는다.
    ///
    /// ■ 견우마을 — 날씨만 고정 (2026-08-20)
    ///   <see cref="forceClear"/> 를 켠다. 시각(낮/밤)은 마을과 같이 가되 비는 오지 않는다.
    ///   예전에는 동기화에서 통째로 빼고 낮_맑음으로 굳혀 뒀는데, 그러면 마을이 밤이어도
    ///   견우마을만 낮이라 시각이 어긋났다.
    /// </summary>
    [AddComponentMenu("이문록/시간대 동기화 (WorldTimeSync)")]
    [DisallowMultipleComponent]
    public class WorldTimeSync : MonoBehaviour
    {
        public enum Kind
        {
            /// <summary>야외 — 하늘 프리셋을 적용한다.</summary>
            야외_하늘프리셋,
            /// <summary>실내 — OfficeTimeOfDay의 낮/밤 조명 그룹을 전환한다.</summary>
            실내_조명그룹,
        }

        public Kind kind = Kind.야외_하늘프리셋;

        [Tooltip("켜면 이 씬의 저장된 시간대를 '기준'으로 삼아 세계 상태에 올려보낸다 (성하리 마을 전용)")]
        public bool isReference = false;

        [Header("단독 Play 기본값")]
        [Tooltip("씬을 혼자 열고 Play했을 때(=아직 아무도 세계 시간대를 정하지 않았을 때)만 " +
                 "아래 값으로 세계를 시드한다. 마을에서 넘어온 경우에는 그 시간대를 그대로 따른다.\n" +
                 "기준 씬(isReference)과 달리 '세계의 기준'을 자처하지 않는다 — 검증 편의용 기본값일 뿐이다.")]
        public bool seedIfUnseeded = false;
        [Tooltip("단독 Play 기본 시간대")]
        public bool seedNight = true;
        public bool seedRain = false;

        [Header("날씨 고정 (견우마을)")]
        [Tooltip("켜면 비가 와도 이 씬만은 항상 맑음. 낮/밤은 그대로 따라간다.\n" +
                 "세계가 낮_비 → 이 씬 낮_맑음 / 밤_비 → 밤_맑음")]
        public bool forceClear = false;

        [Header("야외 — 하늘 프리셋")]
        public Light sun;
        public SkyPreset 낮_맑음, 낮_비, 밤_맑음, 밤_비;

        [Header("씬 로컬 보정 — 프리셋을 적용한 뒤 이 씬에서만 덮어쓴다")]
        // SkyPreset 은 마을·은하담·견우마을이 함께 쓰는 공용 에셋이라 거기를 고치면 다른 씬이 같이 바뀐다.
        // 한 씬만 어둡게/맑게 하고 싶을 때 프리셋 대신 여기를 쓴다. 기본값은 전부 '보정 없음'이다.
        [Tooltip("방향광 세기를 이 값으로 고정 (음수면 프리셋 그대로)")]
        public float sunIntensityOverride = -1f;
        [Tooltip("앰비언트 3색에 곱할 배율 (1 = 프리셋 그대로)")]
        public float ambientScale = 1f;
        [Tooltip("켜면 이 씬은 Linear Fog 를 아예 쓰지 않는다.\n" +
                 "유니티 안개는 '이미 그려진 픽셀을 안개색으로 물들이는' 방식이라 거리를 아무리 밀어도\n" +
                 "물체 색이 씻긴다. 건물 색이 정확해야 하는 씬(관아)은 끄고 파티클로만 안개감을 낸다.")]
        public bool fogForceOff = false;
        [Tooltip("음수가 아니면 반사(하늘 큐브맵) 강도를 이 값으로 고정한다.\n" +
                 "담장·목재가 하늘을 비춰 희푸르게 보이는 정도를 정하는 축이다.")]
        public float reflectionIntensityOverride = -1f;
        [Tooltip("켜면 안개 거리를 아래 값으로 덮어쓴다 (fogForceOff 가 우선)")]
        public bool fogDistanceOverride = false;
        public float fogStartOverride = 250f, fogEndOverride = 1500f;

        [Header("야외 — 씬에 저장된 시간대 (기준 씬일 때 이 값이 세계로 올라간다)")]
        public bool sceneNight = false;
        public bool sceneRain = false;

        [Header("실내")]
        public OfficeTimeOfDay indoor;

        void OnEnable()
        {
            if (isReference && !GyeonuWorld.TimeSeeded)
            {
                // 마을이 기준 — 씬에 칠해 둔 상태를 세계로 올린다.
                // ⚠️ 세션 최초 1회만. 매번 올리면 마을로 **돌아올 때마다** 세계 시간대가
                //    저장값으로 리셋되어, 방금 떠나온 씬과 어긋난다 (2026-08-19 Play 재현).
                GyeonuWorld.Night = sceneNight;
                GyeonuWorld.Rain = sceneRain;
                GyeonuWorld.TimeSeeded = true;
            }
            else if (seedIfUnseeded && !GyeonuWorld.TimeSeeded)
            {
                // 단독 Play — 아무도 정해 주지 않았으니 이 씬의 기본값으로 시작한다.
                // ⚠️ TimeSeeded는 세우지 않는다. 나중에 진짜 기준 씬(마을)이 열리면
                //    그쪽이 세계를 정하도록 자리를 비워 둔다.
                GyeonuWorld.Night = seedNight;
                GyeonuWorld.Rain = seedRain;
            }

            GyeonuWorld.Changed += Apply;
            Apply();
        }

        void OnDisable() => GyeonuWorld.Changed -= Apply;

        /// <summary>세계 상태를 이 씬에 반영한다.</summary>
        public void Apply()
        {
            if (kind == Kind.실내_조명그룹)
            {
                if (indoor == null) indoor = FindFirstObjectByType<OfficeTimeOfDay>();
                if (indoor == null)
                {
                    Debug.LogWarning("[시간대] 실내 모드인데 OfficeTimeOfDay가 없다: " + name, this);
                    return;
                }
                indoor.SetNight(GyeonuWorld.Night);
                return;
            }

            var preset = PickPreset();
            if (preset == null)
            {
                Debug.LogWarning($"[시간대] '{GyeonuWorld.SkyKey}' 에 해당하는 하늘 프리셋이 비어 있다: {name}", this);
                return;
            }

            if (sun == null) sun = FindSun();
            preset.Apply(sun);
            ApplyLocalTrim();
        }

        /// <summary>프리셋을 덮어쓰는 씬 로컬 보정. 프리셋을 적용한 직후에 반드시 한 번 더 돈다.</summary>
        void ApplyLocalTrim()
        {
            if (sunIntensityOverride >= 0f && sun != null) sun.intensity = sunIntensityOverride;

            if (!Mathf.Approximately(ambientScale, 1f))
            {
                RenderSettings.ambientSkyColor = RenderSettings.ambientSkyColor * ambientScale;
                RenderSettings.ambientEquatorColor = RenderSettings.ambientEquatorColor * ambientScale;
                RenderSettings.ambientGroundColor = RenderSettings.ambientGroundColor * ambientScale;
            }

            if (reflectionIntensityOverride >= 0f) RenderSettings.reflectionIntensity = reflectionIntensityOverride;

            if (fogForceOff) RenderSettings.fog = false;
            else if (fogDistanceOverride)
            {
                RenderSettings.fogStartDistance = fogStartOverride;
                RenderSettings.fogEndDistance = fogEndOverride;
            }
        }

        SkyPreset PickPreset()
        {
            // forceClear: 시각(낮/밤)은 따라가되 날씨만 맑음으로 고정한다 (2026-08-20 견우마을).
            //   예전에는 견우마을을 동기화에서 통째로 빼고 낮_맑음으로 굳혀 뒀는데,
            //   그러면 마을이 밤이어도 견우마을만 낮이라 시각이 어긋났다.
            bool rain = GyeonuWorld.Rain && !forceClear;
            if (GyeonuWorld.Night) return rain ? 밤_비 : 밤_맑음;
            return rain ? 낮_비 : 낮_맑음;
        }

        /// <summary>씬의 주 방향광 — 가장 밝은 Directional을 고른다.</summary>
        static Light FindSun()
        {
            Light best = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                if (best == null || l.intensity > best.intensity) best = l;
            }
            return best;
        }
    }
}
