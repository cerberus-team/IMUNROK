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

        [Header("날씨 고정 (견우마을)")]
        [Tooltip("켜면 비가 와도 이 씬만은 항상 맑음. 낮/밤은 그대로 따라간다.\n" +
                 "세계가 낮_비 → 이 씬 낮_맑음 / 밤_비 → 밤_맑음")]
        public bool forceClear = false;

        [Header("야외 — 하늘 프리셋")]
        public Light sun;
        public SkyPreset 낮_맑음, 낮_비, 밤_맑음, 밤_비;

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
