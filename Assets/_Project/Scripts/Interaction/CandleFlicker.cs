using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 촛불/등잔불 깜빡임 — Light 세기와(선택) 불꽃 emission을 은은하게 흔든다.
    /// Perlin 노이즈라 부드럽고, 위치 기반 seed로 촛불마다 따로 흔들림.
    /// </summary>
    public class CandleFlicker : MonoBehaviour
    {
        [SerializeField] private Light _light;
        [SerializeField] private float _baseIntensity = 1.3f;
        [Tooltip("흔들림 폭(0~1). 클수록 크게 깜빡")]
        [SerializeField] private float _amount = 0.3f;
        [SerializeField] private float _speed = 8f;

        [Header("선택: 불꽃 발광도 같이 흔들기")]
        [SerializeField] private Renderer _flameRenderer;
        [SerializeField] private Color _emissionColor = new Color(1f, 0.5f, 0.15f);
        [SerializeField] private float _emissionBase = 3f;

        private float _seed;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            if (_light == null) _light = GetComponentInChildren<Light>();
            _seed = transform.position.x * 0.13f + transform.position.z * 0.37f;   // 촛불마다 다르게
            if (_flameRenderer != null) _mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(Time.time * _speed, _seed);   // 0~1
            float f = 1f + (n - 0.5f) * 2f * _amount;

            if (_light != null) _light.intensity = _baseIntensity * f;

            if (_flameRenderer != null && _mpb != null)
            {
                _flameRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", _emissionColor * (_emissionBase * f));
                _flameRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
