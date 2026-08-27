using UnityEngine;
using UnityEngine.Rendering;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 하늘·조명·안개 설정 묶음.
    /// 에디터에서는 Tools ▸ 이문록 ▸ 성하리 하늘 설정 창으로 적용하고,
    /// 런타임에서는 preset.Apply(sun) 한 줄로 적용한다 (흐림 전환 등).
    /// </summary>
    [CreateAssetMenu(menuName = "이문록/견우/하늘 프리셋", fileName = "SkyPreset")]
    public class SkyPreset : ScriptableObject
    {
        [Header("스카이박스")]
        public Material skyboxMaterial;

        [Tooltip("이 하늘을 미리 구운 반사 큐브맵 (Tools ▸ 이문록 ▸ 하늘 반사 큐브맵 굽기). " +
                 "지정하면 수면 등 반사 재질에 이 하늘이 비친다 — 밤엔 별·달. " +
                 "런타임 굽기는 URP 제약(리얼타임 프로브 미지원, Update 중 RenderToCubemap 실패)으로 불가")]
        public Cubemap reflectionCubemap;

        [Header("물 (씬에 WaterSurface가 있으면 함께 적용)")]
        [Tooltip("맑음 상태 → 물_맑음, 비 상태 → 물_흐림. 물이 없는 씬(마을 등)에서는 무시된다")]
        public WaterPreset waterPreset;

        [Tooltip("시간대 물색 보정 — 물 셰이더가 조명을 안 받아 밤에는 어두운 색을 곱해줘야 한다. 낮=흰색")]
        public Color waterColorMultiplier = Color.white;

        [Header("물 반사 오버라이드 (-1 = 물 프리셋 값 그대로)")]
        [Tooltip("밤_맑음에서 별 반사를 화려하게 할 때 사용. 낮은 -1로 두면 물 프리셋 값 유지")]
        public float waterReflectionStrength = -1f;
        public float waterReflectionFresnel = -1f;
        public float waterReflectionDistortion = -1f;


        [Header("방향광 (해/달)")]
        [Tooltip("X = 고도(높을수록 한낮), Y = 방위(그림자 방향)")]
        public Vector3 sunEulerAngles = new Vector3(35f, 60f, 0f);
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;
        [Range(0f, 1f)] public float sunShadowStrength = 0.85f;

        [Header("앰비언트 (Trilight)")]
        [ColorUsage(false, true)] public Color ambientSky = new Color(0.55f, 0.62f, 0.72f);
        [ColorUsage(false, true)] public Color ambientEquator = new Color(0.45f, 0.45f, 0.42f);
        [ColorUsage(false, true)] public Color ambientGround = new Color(0.25f, 0.23f, 0.20f);

        [Header("안개 (Linear)")]
        public bool fogEnabled = true;
        public Color fogColor = new Color(0.75f, 0.78f, 0.80f);
        [Tooltip("이 거리부터 흐려지기 시작")]
        public float fogStart = 40f;
        [Tooltip("이 거리에서 완전히 안개색이 됨")]
        public float fogEnd = 160f;

        /// <summary>현재 씬의 RenderSettings와 방향광에 이 프리셋을 적용한다.</summary>
        public void Apply(Light sun)
        {
            if (skyboxMaterial != null)
                RenderSettings.skybox = skyboxMaterial;

            if (reflectionCubemap != null)
            {
                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = reflectionCubemap;
            }
            else
            {
                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
                RenderSettings.customReflectionTexture = null;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;

            RenderSettings.fog = fogEnabled;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(sunEulerAngles);
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.shadowStrength = sunShadowStrength;
            }

            // 물 상태 연동 — 씬에 수면이 있으면 같은 버튼 한 번으로 물까지 전환된다
            if (waterPreset != null)
                foreach (var water in Object.FindObjectsByType<WaterSurface>(FindObjectsSortMode.None))
                {
                    water.ApplyPreset(waterPreset, waterColorMultiplier);
                    water.ApplyReflectionOverride(
                        waterReflectionStrength, waterReflectionFresnel, waterReflectionDistortion);
                }

            DynamicGI.UpdateEnvironment();
        }
    }
}
