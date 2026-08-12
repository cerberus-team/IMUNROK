using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 등불(燈): "등불"을 손에 든 동안(도구벨트 SelectedToolId == "lantern")만 켜진다.
    ///  · 켤 때는 부드럽게 밝아지고, 다른 도구로 바꾸면 즉시 꺼진다(잔상 없음).
    ///  · 빛은 Unity Light, 모델은 발광 재질로 스스로 빛나 보임.
    /// 한 손에 하나 원칙: 별도 토글 키 없음 — 손 도구 전환(Q/휠)으로만 켜고 끈다.
    /// </summary>
    public class LanternController : MonoBehaviour
    {
        [SerializeField] private GameObject _model;   // 등불 모델(들었을 때만 보임)
        [SerializeField] private Light _light;
        [Tooltip("켰을 때 빛 밝기")]
        [SerializeField] private float _onIntensity = 3f;
        [Tooltip("켤 때 밝아지는 부드러움")]
        [SerializeField] private float _ease = 8f;
        [SerializeField] private string _toolId = "lantern";

        private void Start()
        {
            if (_light != null) _light.intensity = 0f;
            if (_model != null) _model.SetActive(false);
        }

        private void Update()
        {
            bool want = ToolbeltHud.SelectedToolId == _toolId;

            if (_light != null)
            {
                if (want)
                {
                    float k = 1f - Mathf.Exp(-_ease * Time.deltaTime);
                    _light.intensity = Mathf.Lerp(_light.intensity, _onIntensity, k);
                }
                else
                {
                    _light.intensity = 0f;   // 즉시 꺼서 잔상 없음
                }
            }

            if (_model != null && _model.activeSelf != want) _model.SetActive(want);
        }
    }
}
