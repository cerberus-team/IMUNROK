using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 은하담 수면. URP Lit 머티리얼의 UV를 흘려보내 강물 흐름을 만들고,
    /// WaterPreset(물_맑음/물_흐림)을 런타임에 갈아끼운다.
    /// 반사는 스카이박스 환경 리플렉션 — 별도 렌더 비용 없이 밤 하늘 별이 그대로 비친다.
    /// (스카이박스를 바꾼 뒤에는 SkyPreset.Apply가 DynamicGI.UpdateEnvironment를 불러 반사도 갱신됨)
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class WaterSurface : MonoBehaviour
    {
        [Tooltip("시작 상태. 런타임 전환은 ApplyPreset() 호출")]
        public WaterPreset preset;

        Material _mat;          // 런타임 인스턴스 (공유 에셋을 더럽히지 않음)
        Vector2 _offset;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        void Awake()
        {
            _mat = GetComponent<Renderer>().material;
            if (preset != null) preset.ApplyTo(_mat);
        }

        void Update()
        {
            if (preset == null || _mat == null) return;
            _offset += preset.flowSpeed * Time.deltaTime;
            _offset.x %= 1f;
            _offset.y %= 1f;
            _mat.SetTextureOffset(BaseMapId, _offset);
        }

        /// <summary>물 상태 전환 (게임 진행 이벤트에서 호출).</summary>
        public void ApplyPreset(WaterPreset p)
        {
            preset = p;
            if (p != null && _mat != null) p.ApplyTo(_mat);
        }
    }
}
