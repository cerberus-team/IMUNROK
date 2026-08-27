using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 은하담 수면. Uber Stylized Water 머티리얼에 WaterPreset(물_맑음/물_흐림)을 적용한다.
    /// 물결·거품 패닝은 셰이더가 시간 기반으로 자체 처리하므로 스크립트 스크롤은 없다.
    ///
    /// 하늘 반사(밤의 별·달)는 이 컴포넌트가 아니라 SkyPreset.reflectionCubemap이 담당한다 —
    /// 미리 구운 큐브맵을 SkyPreset.Apply가 RenderSettings.customReflectionTexture로 지정.
    /// (URP는 리얼타임 프로브 미지원 + Update 중 RenderToCubemap이 조용히 실패해 런타임 굽기 불가)
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class WaterSurface : MonoBehaviour
    {
        [Tooltip("시작 상태. 런타임 전환은 ApplyPreset() 호출")]
        public WaterPreset preset;

        Material _mat;          // 런타임 인스턴스 (공유 에셋을 더럽히지 않음)

        void Awake()
        {
            _mat = GetComponent<Renderer>().material;
            if (preset != null) preset.ApplyTo(_mat);
        }

        /// <summary>물 상태 전환 (게임 진행 이벤트에서 호출).</summary>
        public void ApplyPreset(WaterPreset p) => ApplyPreset(p, Color.white);

        /// <summary>시간대 물색 보정(tint) 포함 전환 — SkyPreset.Apply가 사용.</summary>
        public void ApplyPreset(WaterPreset p, Color tint)
        {
            preset = p;
            var mat = Application.isPlaying ? _mat : GetComponent<Renderer>().sharedMaterial;
            if (p != null && mat != null) p.ApplyTo(mat, tint);
        }

        /// <summary>
        /// 프리셋별 반사 오버라이드 (밤에 별 반사를 강조할 때). 음수 값은 무시되어
        /// 물 프리셋의 원래 값이 유지된다. ApplyPreset 다음에 호출할 것.
        /// </summary>
        public void ApplyReflectionOverride(float strength, float fresnel, float distortion)
        {
            var mat = Application.isPlaying ? _mat : GetComponent<Renderer>().sharedMaterial;
            if (mat == null) return;
            if (strength >= 0f) mat.SetFloat("_Reflection_Strength", strength);
            if (fresnel >= 0f) mat.SetFloat("_Reflection_Fresnel", fresnel);
            if (distortion >= 0f) mat.SetFloat("_Reflection_Distortion", distortion);
        }
    }
}
