using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 수면 상태 묶음 (물_맑음 / 물_흐림). SkyPreset과 같은 패턴.
    /// Uber Stylized Water 셰이더(Assets/Shaders/Uber Stylized Water, gitignore — 팀원 각자 임포트)의
    /// 머티리얼 프로퍼티에 매핑된다. 셰이더 기능 키워드(굴절·커스틱 등)는 머티리얼에 고정하고
    /// 프리셋은 float/color 값만 바꾼다 (빌드에서 셰이더 배리언트가 빠지는 사고 방지).
    /// 에디터: Tools ▸ 이문록 ▸ 은하담 물 설정 창 버튼. 런타임: waterSurface.ApplyPreset(preset).
    /// </summary>
    [CreateAssetMenu(menuName = "이문록/견우/물 프리셋", fileName = "WaterPreset")]
    public class WaterPreset : ScriptableObject
    {
        [Header("물색 (알파 = 불투명도. 낮을수록 바닥이 비침)")]
        public Color shallowColor = new Color(0.18f, 0.55f, 0.50f, 0.05f);
        public Color deepColor = new Color(0.02f, 0.20f, 0.28f, 0.60f);
        [Tooltip("얕은색→깊은색 그라데이션이 끝나는 수심(m). 낮을수록 금방 탁해 보임")]
        public float waterDepth = 1.2f;

        [Header("반사 (스카이박스 프로브 — 밤엔 별이 비침)")]
        [Range(0f, 1f)] public float reflectionStrength = 0.95f;
        public float reflectionFresnel = 1f;
        [Tooltip("낮을수록 반사가 또렷. 별 반사는 0.05 근처")]
        public float reflectionDistortion = 0.05f;

        [Header("물결·흐름")]
        public float normalStrength = 0.08f;
        [Tooltip("노멀 패닝 속도 (물결이 흘러가는 빠르기)")]
        public float normalPan = 0.08f;

        [Header("바닥 효과 (맑을 때만 보임)")]
        public float causticsStrength = 2f;
        public float refractionStrength = 0.5f;

        static readonly int ShallowId = Shader.PropertyToID("_Color_Shallow");
        static readonly int DeepId = Shader.PropertyToID("_Color_Deep");
        static readonly int WaterDepthId = Shader.PropertyToID("_Water_Depth");
        static readonly int ReflStrengthId = Shader.PropertyToID("_Reflection_Strength");
        static readonly int ReflFresnelId = Shader.PropertyToID("_Reflection_Fresnel");
        static readonly int ReflDistortId = Shader.PropertyToID("_Reflection_Distortion");
        static readonly int NormalStrengthId = Shader.PropertyToID("_Normal_Strength");
        static readonly int NormalPanId = Shader.PropertyToID("_Normal_Pan");
        static readonly int CausticsId = Shader.PropertyToID("_Caustics_Strength");
        static readonly int RefractionId = Shader.PropertyToID("_Refraction_Strength");

        /// <summary>머티리얼에 프리셋 값을 적용한다.</summary>
        public void ApplyTo(Material mat) => ApplyTo(mat, Color.white);

        /// <summary>
        /// 시간대 보정 포함 적용. Uber 셰이더는 물색이 조명을 거의 안 받아
        /// 밤에도 낮 색 그대로 나온다 — SkyPreset이 넘겨주는 곱색(tint)으로 어둡게 만든다.
        /// 커스틱 강도도 tint 밝기에 비례해 감쇠.
        /// </summary>
        public void ApplyTo(Material mat, Color tint)
        {
            if (mat == null) return;
            var shallow = shallowColor * tint; shallow.a = shallowColor.a;
            var deep = deepColor * tint; deep.a = deepColor.a;
            mat.SetColor(ShallowId, shallow);
            mat.SetColor(DeepId, deep);
            mat.SetFloat(WaterDepthId, waterDepth);
            mat.SetFloat(ReflStrengthId, reflectionStrength);
            mat.SetFloat(ReflFresnelId, reflectionFresnel);
            mat.SetFloat(ReflDistortId, reflectionDistortion);
            mat.SetFloat(NormalStrengthId, normalStrength);
            mat.SetFloat(NormalPanId, normalPan);
            mat.SetFloat(CausticsId, causticsStrength * tint.grayscale);
            mat.SetFloat(RefractionId, refractionStrength);
        }
    }
}
