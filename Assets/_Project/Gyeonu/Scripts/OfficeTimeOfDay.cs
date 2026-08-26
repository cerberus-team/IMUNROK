using UnityEngine;
using UnityEngine.Rendering;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 관아 집무실의 낮/밤 상태. 실내라 스카이박스는 무관하고 **창으로 들어오는 빛과
    /// 실내 조명**만 바뀐다 — 그래서 하늘 프리셋(SkyPreset)과 달리 씬 안의 조명 그룹을
    /// 통째로 갈아 끼우는 방식이다.
    ///
    ///   낮 — 수령이 있는 시간. 좌우 창에서 강한 측광, 바닥에 창살 그림자
    ///   밤 — 몰래 잠입하는 시간. 창밖이 어둡고 달빛만 희미하게, 실내는 등잔 하나
    ///
    /// 상태가 **씬에 직렬화**되도록 조명 그룹 2개를 미리 세워 두고 하나만 켠다
    /// (머티리얼 에셋에 상태를 담으면 씬을 열 때마다 마지막 상태가 따라온다).
    /// 런타임에서도 `SetNight(true)` 한 줄로 전환된다 — 낮에 쫓겨나는 로직이 붙을 때 쓸 것.
    ///
    /// ⚠️ URP `PC_RPAsset`의 추가 광원 한도가 4다. 두 그룹 **각각** 그 한도를 지켜야 한다
    ///    (메인 방향광은 한도 밖). 그룹에 광원을 더할 때 반드시 세어 볼 것.
    /// </summary>
    [ExecuteAlways]
    public class OfficeTimeOfDay : MonoBehaviour
    {
        [Header("조명 그룹 (하나만 켜진다)")]
        public GameObject dayGroup;
        public GameObject nightGroup;

        [Header("창호지 — 바깥 밝기를 나타낸다")]
        public Renderer[] paperRenderers;
        public Material paperDay;
        public Material paperNight;

        [Header("앰비언트 (Trilight)")]
        public Color ambientSkyDay = new Color(0.36f, 0.34f, 0.30f);
        public Color ambientEquatorDay = new Color(0.28f, 0.26f, 0.22f);
        public Color ambientGroundDay = new Color(0.15f, 0.13f, 0.10f);
        public Color ambientSkyNight = new Color(0.085f, 0.100f, 0.145f);
        public Color ambientEquatorNight = new Color(0.055f, 0.062f, 0.082f);
        public Color ambientGroundNight = new Color(0.030f, 0.032f, 0.040f);

        [SerializeField] bool night;

        public bool IsNight => night;

        void OnEnable() => Apply();

        /// <summary>낮↔밤 전환. 런타임·에디터 양쪽에서 쓴다.</summary>
        public void SetNight(bool value)
        {
            night = value;
            Apply();
        }

        public void Apply()
        {
            if (dayGroup != null) dayGroup.SetActive(!night);
            if (nightGroup != null) nightGroup.SetActive(night);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = night ? ambientSkyNight : ambientSkyDay;
            RenderSettings.ambientEquatorColor = night ? ambientEquatorNight : ambientEquatorDay;
            RenderSettings.ambientGroundColor = night ? ambientGroundNight : ambientGroundDay;
            RenderSettings.ambientIntensity = 1f;

            var paper = night ? paperNight : paperDay;
            if (paper != null && paperRenderers != null)
                foreach (var r in paperRenderers)
                    if (r != null) r.sharedMaterial = paper;
        }
    }
}
