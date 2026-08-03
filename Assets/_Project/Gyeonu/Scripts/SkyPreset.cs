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

            DynamicGI.UpdateEnvironment();
        }
    }
}
