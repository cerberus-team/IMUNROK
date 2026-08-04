using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 수면 상태 묶음 (물_맑음 / 물_흐림). SkyPreset과 같은 패턴.
    /// 에디터에서는 Tools ▸ 이문록 ▸ 은하담 물 설정 창으로 적용하고,
    /// 런타임에서는 waterSurface.ApplyPreset(preset) 한 줄로 전환한다.
    /// </summary>
    [CreateAssetMenu(menuName = "이문록/견우/물 프리셋", fileName = "WaterPreset")]
    public class WaterPreset : ScriptableObject
    {
        [Header("수면 색 (알파 = 탁도. 낮을수록 바닥이 비침)")]
        public Color waterColor = new Color(0.16f, 0.28f, 0.30f, 0.5f);

        [Header("반사 선명도 (URP Smoothness. 별 반사는 0.9 이상)")]
        [Range(0f, 1f)] public float smoothness = 0.92f;

        [Header("물결 노멀 강도")]
        [Range(0f, 4f)] public float waveStrength = 0.8f;

        [Header("흐름 속도 (UV/초). 강물은 Z방향 = y성분")]
        public Vector2 flowSpeed = new Vector2(0f, 0.025f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");

        /// <summary>머티리얼에 색·반사·물결 강도를 적용한다 (흐름 속도는 WaterSurface가 읽는다).</summary>
        public void ApplyTo(Material mat)
        {
            if (mat == null) return;
            mat.SetColor(BaseColorId, waterColor);
            mat.SetFloat(SmoothnessId, smoothness);
            mat.SetFloat(BumpScaleId, waveStrength);
        }
    }
}
