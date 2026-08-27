using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 서천 꽃밭(2부) 하늘 상태 컨트롤러.
    ///
    /// 설계 — 서천꽃밭은 저승이다. 하늘에 태양이 없다.
    ///  · 스카이박스 큐브맵에는 그라데이션 · 구름 · 별만 굽는다. 태양도 행성도 없다.
    ///  · 거대 행성은 별도 빌보드 쿼드(드로우콜 1 / 트라이앵글 2)이며 이 씬의 주광원이다.
    ///  · Directional Light 는 행성 방향과 항상 같은 값으로 구동된다. 따로 만지지 말 것.
    ///
    /// ★행성이 스카이박스가 아니라 쿼드인 이유
    ///  Skybox/Cubemap 의 _Rotation 은 Y축 회전만 지원한다. Y축 회전은 등고도 원을 따라
    ///  움직이므로 고도를 바꿀 수 없다. 귀환 시한 연출("행성이 가라앉는다")을 하려면
    ///  고도가 변해야 하고, 큐브맵 2장 블렌드는 커스텀 셰이더가 필요해 Quest 에서 비싸다.
    ///  쿼드는 드로우콜 1개로 고도를 자유롭게 보간한다.
    ///
    /// ★산괴(_DistantMassifs)는 Seocheon/DistantSilhouette (URP Unlit) 이라
    ///  RenderSettings.fog 가 적용되지 않는다. 거리 기반 안개색을 여기서 직접 계산해
    ///  MaterialPropertyBlock 으로 먹인다(GPU 비용 0).
    /// </summary>
    [ExecuteAlways]
    public class FlowerfieldSkyController : MonoBehaviour
    {
        [System.Serializable]
        public class SkyPreset
        {
            [ColorUsage(false)] public Color skyTint = new Color(0.5f, 0.5f, 0.5f);
            public float exposure = 1.05f;
            [ColorUsage(false)] public Color fogColor = new Color(0.459f, 0.325f, 0.464f);
            public float fogStart = 150f;
            public float fogEnd = 700f;

            [Header("행성 = 주광원")]
            [Tooltip("지평선 위 각도(도). 이 값이 곧 Directional Light 의 euler X 다.")]
            public float planetElevation = 10f;
            [ColorUsage(false)] public Color planetTint = Color.white;
            public float planetBrightness = 1f;

            [Header("행성 대기 감쇠")]
            [Tooltip("원반 상단의 haze 량.")]
            [Range(0f, 1f)] public float hazeBase = 0.30f;
            [Tooltip("원반 하단에 추가로 더해지는 haze 량. 세로 기울기가 자연스러움의 핵심이다.")]
            [Range(0f, 1f)] public float hazeGrad = 0.35f;
            [Tooltip("원반 하단의 알파 감소량. 색만 섞으면 스티커로 보이므로 알파도 떨어뜨린다.")]
            [Range(0f, 1f)] public float bottomFade = 0.30f;

            [Header("빛")]
            [ColorUsage(false)] public Color sunColor = new Color(1f, 0.66f, 0.42f);
            public float sunIntensity = 1.35f;
            public float ambientIntensity = 0.75f;

            [Header("별")]
            [Tooltip("밝은 별 빌보드의 밝기 배율. ★큐브맵에 구운 별은 하늘과 같은 _Exposure 로 " +
                     "함께 어두워져 t 로 돋아나게 할 수 없다. 그래서 별밭을 빌보드로 분리하고 " +
                     "이 값을 t 로 구동한다. t=0 에서 거의 0, t=1 에서 1.")]
            public float starIntensity = 0.10f;

            [Tooltip("보이는 별의 등급 하한. 이 값보다 어두운 별은 꺼진다. " +
                     "★밝기 배율만 t 로 올리면 900개가 통째로 밝아져 먼지가 된다. " +
                     "어느 별이 보이는가가 t 에 따라 달라져야 한다.")]
            [Range(0f, 1f)] public float starMagCut = 0.30f;

            [Tooltip("반짝임 진폭. 0.45 면 0.10~1.00, 0.35 면 0.30~1.00 을 오간다. " +
                     "t=0 은 보이는 별이 적어 진폭이 커야 눈에 띈다.")]
            [Range(0f, 0.5f)] public float starTwinkleDepth = 0.35f;

            [Tooltip("별 쿼드 크기 배율. ★t=0 은 하늘이 밝아 가산의 천장이 표시값 0.156 뿐이라 " +
                     "밝기로는 또렷해질 수 없다. 보이는 소수의 별을 키워서 읽히게 한다.")]
            [Range(0.5f, 3f)] public float starSizeBoost = 1f;
        }

        [Header("상태 (0 = 행성 높음/밝음, 1 = 행성 지평선/어두움)")]
        [Range(0f, 1f)] public float t = 0f;

        [Header("프리셋")]
        public SkyPreset sunset = new SkyPreset();
        public SkyPreset night = new SkyPreset();

        [Header("참조")]
        public Material skyboxMaterial;
        public Light sun;
        public Transform massifRoot;

        [Header("행성 빌보드")]
        public Transform planetQuad;
        public Renderer planetRenderer;
        [Tooltip("행성의 방위. compass 0 = 북. 고도와 달리 t 에 따라 변하지 않는다.")]
        public float planetCompass = 202f;
        [Tooltip("쿼드를 놓는 거리(m). 카메라 far clip 보다 작아야 한다.")]
        public float planetDistance = 2500f;
        [Tooltip("쿼드가 덮는 반각(도). 텍스처를 구운 값과 일치해야 한다.")]
        public float planetHalfAngleDeg = 30f;

        [Tooltip("대기 감쇠에 쓸 색. 하늘 지평선대 평균색을 넣는다.")]
        [ColorUsage(false)] public Color planetHazeColor = new Color(0.414f, 0.314f, 0.582f);

        [Tooltip("행성 원반 반경(텍스처 정규화). 텍스처를 구운 값과 일치해야 haze 기울기가 맞는다.")]
        public float planetDiscRadiusNorm = 0.1333f;

        [Header("행성 앞 구름")]
        [Tooltip("행성 쿼드 여백까지 그려지는 구름의 불투명도. ★원반 안 얼룩은 표면으로 읽히므로, " +
                 "구름이 앞을 지나간다고 읽히려면 림(원반 윤곽선)이 끊겨야 한다.")]
        [Range(0f, 1f)] public float cloudOpacity = 0.62f;

        [Header("밝은 별 빌보드")]
        [Tooltip("별 빌보드 단일 메시의 루트. 카메라를 따라다니기만 하고 회전하지 않는다.")]
        public Transform starRoot;
        public Renderer starRenderer;
        [Tooltip("등급 임계의 전환 폭. 계단이 아니라 부드럽게 켜지도록 한다. 0 이면 별이 툭 나타난다.")]
        [Range(0.01f, 0.5f)] public float starMagSoft = 0.16f;

        [Header("산괴 거리 (자식 순서와 일치, 단위 m)")]
        public float[] massifDistances = new float[] { 419f, 569f, 612f, 595f, 418f };

        [Range(0f, 1f)] public float bottomFogScale = 0.85f;

        [Tooltip("산괴 안개 계수의 상한. 1.0 이면 fogEnd 보다 먼 조각이 전부 같은 색이 되어 " +
                 "3티어 층감이 사라진다. 0.85~0.9 권장.")]
        [Range(0.5f, 1f)] public float massifFogMax = 0.88f;

        // ─────────────────────────────────────────────────────────────
        // ★S22 지면 색감 — 안개 / 환경광 오버라이드
        //
        //  Apply() 가 RenderSettings.fog 와 ambientMode 를 하드코딩하고 있어서
        //  바깥에서 RenderSettings 에 써 봐야 다음 LateUpdate 에 되돌아갔다.
        //  ★그래서 두 값을 여기로 끌어올린다. 기존 동작은 기본값 그대로다.
        // ─────────────────────────────────────────────────────────────
        [Header("★S22 지면 색감")]
        [Tooltip("false 면 RenderSettings.fog 를 끈다. ★산괴 안개는 프리셋 fogStart/fogEnd 로 " +
                 "직접 계산되므로 이 토글의 영향을 받지 않는다.")]
        public bool fogEnabled = true;

        [Tooltip("켜면 환경광을 스카이박스가 아니라 아래 단색으로 준다. " +
                 "★스카이박스 환경광은 분홍-라벤더 하늘색을 지면에 그대로 덮어 채도를 깎는다. " +
                 "S22 실측 : 등휘도에서 채도 0.1262 -> 0.2332 (+84.8%), 색상각 334.7° -> 71.3°.")]
        public bool overrideAmbient = false;

        [Tooltip("overrideAmbient 가 켜졌을 때 쓰는 환경광 색. ★감마 공간이다.")]
        [ColorUsage(false)] public Color ambientFlatColor = new Color(0.62f, 0.64f, 0.60f);

        [Tooltip("ambientFlatColor 에 곱하는 배율. ★S22 등휘도 배율 = 1.5769 " +
                 "(스카이박스 1.65 와 지면 선형휘도 0.15450 을 맞춘 값).")]
        [Range(0.1f, 4f)] public float ambientFlatScale = 1.5769f;

        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdExposure = Shader.PropertyToID("_Exposure");
        static readonly int IdTop = Shader.PropertyToID("_TopColor");
        static readonly int IdBottom = Shader.PropertyToID("_BottomColor");
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdBrightness = Shader.PropertyToID("_Brightness");
        static readonly int IdHazeColor = Shader.PropertyToID("_HazeColor");
        static readonly int IdHazeBase = Shader.PropertyToID("_HazeBase");
        static readonly int IdHazeGrad = Shader.PropertyToID("_HazeGrad");
        static readonly int IdDiscRadius = Shader.PropertyToID("_DiscRadius");
        static readonly int IdBottomFade = Shader.PropertyToID("_BottomFade");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        static readonly int IdTwinkleDepth = Shader.PropertyToID("_TwinkleDepth");
        static readonly int IdMagCut = Shader.PropertyToID("_MagCut");
        static readonly int IdMagSoft = Shader.PropertyToID("_MagSoft");
        static readonly int IdSizeBoost = Shader.PropertyToID("_SizeBoost");
        static readonly int IdCloudOpacity = Shader.PropertyToID("_CloudOpacity");
        static readonly int IdCloudTint = Shader.PropertyToID("_CloudTint");
        static readonly int IdAuroraGain = Shader.PropertyToID("_AuroraGain");
        static readonly int IdAuroraTint = Shader.PropertyToID("_AuroraTint");
        static readonly int IdLimbSkyColor = Shader.PropertyToID("_LimbSkyColor");
        static readonly int IdFlowTime = Shader.PropertyToID("_FlowTime");
        static readonly int IdStarTime = Shader.PropertyToID("_StarTime");
        static readonly int IdFlowSpeed = Shader.PropertyToID("_FlowSpeed");
        static readonly int IdFlowAmpAz = Shader.PropertyToID("_FlowAmpAz");
        static readonly int IdFlowAmpEl = Shader.PropertyToID("_FlowAmpEl");
        static readonly int IdFlowPulse = Shader.PropertyToID("_FlowPulse");

        MaterialPropertyBlock _mpb;
        MaterialPropertyBlock _planetMpb;
        Color[] _baseTop;
        Color[] _baseBottom;

        void OnEnable() { CacheMassifBaseColors(); Apply(); }
        void OnValidate() { if (!isActiveAndEnabled) return; CacheMassifBaseColors(); Apply(); }
        [Header("애니메이션 시간 (v22)")]
        [Tooltip("체크하면 Time 대신 manualAnimTime 을 쓴다. ★판정용 결정론적 주입 경로. " +
                 "_Time 에 의존해 연속 촬영하면 MCP 지연 편차 때문에 판정이 재현되지 않는다.")]
        public bool useManualAnimTime = false;
        public float manualAnimTime = 0f;

        /// <summary>오로라 흐름·별 반짝임의 시간을 셰이더에 밀어넣는다.
        /// ★에디터 비플레이 상태에서도 호출 가능해야 렌더 시퀀스를 찍을 수 있다.</summary>
        public void PushAnimTime(float seconds)
        {
            if (skyboxMaterial != null) skyboxMaterial.SetFloat(IdFlowTime, seconds);
            if (starRenderer != null && starRenderer.sharedMaterial != null)
                starRenderer.sharedMaterial.SetFloat(IdStarTime, seconds);
        }

        void LateUpdate()
        {
            PlacePlanet(); PlaceStars();
            // ★매 프레임 밀어넣는다. 안 하면 오로라와 별이 정지한다.
            PushAnimTime(useManualAnimTime ? manualAnimTime : Time.timeSinceLevelLoad);
        }

        /// <summary>현재 t 에서의 행성 고도(도).</summary>
        public float CurrentPlanetElevation()
        {
            return Mathf.Lerp(sunset.planetElevation, night.planetElevation, Mathf.Clamp01(t));
        }

        /// <summary>행성을 향하는 월드 방향 단위벡터.</summary>
        public Vector3 PlanetDirection()
        {
            float elev = CurrentPlanetElevation() * Mathf.Deg2Rad;
            // compass 0 = 북(+Z). 내부 az = 90 - compass.
            float iaz = (90f - planetCompass) * Mathf.Deg2Rad;
            float ce = Mathf.Cos(elev);
            return new Vector3(Mathf.Cos(iaz) * ce, Mathf.Sin(elev), Mathf.Sin(iaz) * ce).normalized;
        }

        public void CacheMassifBaseColors()
        {
            if (massifRoot == null) { _baseTop = null; _baseBottom = null; return; }
            int n = massifRoot.childCount;
            _baseTop = new Color[n];
            _baseBottom = new Color[n];
            for (int i = 0; i < n; i++)
            {
                var mr = massifRoot.GetChild(i).GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null) continue;
                var m = mr.sharedMaterial;
                if (m.HasProperty(IdTop)) _baseTop[i] = m.GetColor(IdTop);
                if (m.HasProperty(IdBottom)) _baseBottom[i] = m.GetColor(IdBottom);
            }
        }

        /// <summary>쿼드를 카메라 기준 고정 방향·고정 거리에 놓고 정면을 향하게 한다.</summary>
        public void PlacePlanet()
        {
            if (planetQuad == null) return;
            Camera cam = Camera.main;
            Vector3 origin = cam != null ? cam.transform.position : Vector3.zero;
            Vector3 dir = PlanetDirection();
            planetQuad.position = origin + dir * planetDistance;
            planetQuad.rotation = Quaternion.LookRotation(dir, Vector3.up);
            float size = 2f * planetDistance * Mathf.Tan(planetHalfAngleDeg * Mathf.Deg2Rad);
            planetQuad.localScale = new Vector3(size, size, 1f);
        }

        /// <summary>별 빌보드 메시를 카메라 위치로 옮긴다. 회전은 하지 않는다 —
        /// 쿼드가 이미 원점을 향해 구워져 있어 루트가 카메라에 있으면 항상 정면이다.</summary>
        public void PlaceStars()
        {
            if (starRoot == null) return;
            Camera cam = Camera.main;
            starRoot.position = cam != null ? cam.transform.position : Vector3.zero;
            starRoot.rotation = Quaternion.identity;
            starRoot.localScale = Vector3.one;
        }

        // 오로라 게인 곡선. t=0 0.30 / t=0.5 1.65 / t=1 1.35 의 구간 선형 보간 (v19 갱신).
        // ★t=0 이 0.45 -> 0.30 인 이유: 색 치환(AuroraTintAt)이 그 역할을 대신한다.
        // ★t=0 을 1.0 미만으로 낮추는 것이 v16 클리핑(오로라대 12.6%)의 해소책이다.
        public float AuroraGainAt(float k)
        {
            k = Mathf.Clamp01(k);
            return k < 0.5f
                ? Mathf.Lerp(0.30f, 1.65f, k / 0.5f)
                : Mathf.Lerp(1.65f, 1.35f, (k - 0.5f) / 0.5f);
        }

        /// <summary>t=0 / 0.5 / 1 의 세 값을 구간 선형 보간한다.</summary>
        public static float PiecewiseAt(float k, float a0, float a5, float a1)
        {
            k = Mathf.Clamp01(k);
            return k < 0.5f ? Mathf.Lerp(a0, a5, k / 0.5f)
                            : Mathf.Lerp(a5, a1, (k - 0.5f) / 0.5f);
        }

        // 오로라 색 치환 강도. t=0 0.38 / t=0.5 0.18 / t=1 0.00 의 구간 선형 보간.
        // ★밝기를 바꾸지 않고 색만 얹는 항이다. 밤에는 가산만으로 충분하므로 0 으로 내린다.
        public float AuroraTintAt(float k)
        {
            k = Mathf.Clamp01(k);
            return k < 0.5f
                ? Mathf.Lerp(0.38f, 0.18f, k / 0.5f)
                : Mathf.Lerp(0.18f, 0.00f, (k - 0.5f) / 0.5f);
        }

        public void Apply()
        {
            float k = Mathf.Clamp01(t);

            Color fogColor = Color.Lerp(sunset.fogColor, night.fogColor, k);
            float fogStart = Mathf.Lerp(sunset.fogStart, night.fogStart, k);
            float fogEnd = Mathf.Lerp(sunset.fogEnd, night.fogEnd, k);

            if (skyboxMaterial != null)
            {
                skyboxMaterial.SetColor(IdTint, Color.Lerp(sunset.skyTint, night.skyTint, k));
                skyboxMaterial.SetFloat(IdExposure, Mathf.Lerp(sunset.exposure, night.exposure, k));

                // ★오로라 게인 — 하늘 _Exposure 와 독립. 큐브맵 알파의 강도 마스크에 곱해진다.
                //   t=0 에서 낮추는 것이 목적이다. 밝게 만들려는 게 아니라 1.0 클리핑을 피해 "색을 살리려는" 것.
                //   t=1 에서 올려야 하늘이 어두워질 때 오로라가 함께 사라지지 않는다.
                //   ★내장 Skybox/Cubemap 에는 이 프로퍼티가 없다. SetFloat 은 조용히 무시되므로 안전하다.
                skyboxMaterial.SetFloat(IdAuroraGain, AuroraGainAt(k));
                skyboxMaterial.SetFloat(IdAuroraTint, AuroraTintAt(k));

                // ★v23 : 흐름을 t 로 구동한다.
                //   대비가 낮을수록 움직임을 ★크고 느리게★ 해야 지각된다.
                //   t=0 은 오로라가 옅어 같은 흐름이 화면에서 1/255 밖에 안 변했다(v23 사전 검증).
                //   ★t=1 값은 v22 확정값 그대로 — 밤 결과가 바뀌면 안 된다.
                //   ★각속도 = Amp x Speed x 0.514 이므로 진폭을 키우는 만큼 속도를 낮춰
                //     VR 상한 0.35 °/s 를 세 지점 전부에서 지킨다.
                skyboxMaterial.SetFloat(IdFlowAmpAz, PiecewiseAt(k, 2.2f, 1.9f, 1.6f));
                skyboxMaterial.SetFloat(IdFlowAmpEl, PiecewiseAt(k, 0.7f, 0.6f, 0.5f));
                skyboxMaterial.SetFloat(IdFlowSpeed, PiecewiseAt(k, 0.26f, 0.30f, 0.35f));
                skyboxMaterial.SetFloat(IdFlowPulse, PiecewiseAt(k, 0.20f, 0.16f, 0.12f));
                if (RenderSettings.skybox != skyboxMaterial) RenderSettings.skybox = skyboxMaterial;
            }

            RenderSettings.fog = fogEnabled;           // ★S22 : 하드코딩 true 에서 토글로
            RenderSettings.fogMode = FogMode.Linear;   // Quest 타일 GPU 에서 Exponential 은 픽셀당 비용이 크다
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            if (overrideAmbient)                       // ★S22
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = ambientFlatColor * ambientFlatScale;
            }
            else
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity = Mathf.Lerp(sunset.ambientIntensity, night.ambientIntensity, k);
            }

            // ── 행성 = 광원. 빛의 방향은 행성 방향에서 파생된다 ──
            PlacePlanet();
            if (sun != null)
            {
                sun.color = Color.Lerp(sunset.sunColor, night.sunColor, k);
                sun.intensity = Mathf.Lerp(sunset.sunIntensity, night.sunIntensity, k);
                // Directional Light 의 euler X = 광원 고도, euler Y = compass - 180.
                sun.transform.rotation = Quaternion.Euler(CurrentPlanetElevation(), planetCompass - 180f, 0f);
            }
            if (planetRenderer != null)
            {
                if (_planetMpb == null) _planetMpb = new MaterialPropertyBlock();
                _planetMpb.Clear();
                planetRenderer.GetPropertyBlock(_planetMpb);
                _planetMpb.SetColor(IdColor, Color.Lerp(sunset.planetTint, night.planetTint, k));
                _planetMpb.SetFloat(IdBrightness, Mathf.Lerp(sunset.planetBrightness, night.planetBrightness, k));
                planetRenderer.SetPropertyBlock(_planetMpb);

                // haze 는 머티리얼에 직접 쓴다. UnityPerMaterial CBUFFER 프로퍼티라
                // SRP Batcher 경로에서 MaterialPropertyBlock 오버라이드가 무시될 수 있다.
                var pmat = planetRenderer.sharedMaterial;
                if (pmat != null)
                {
                    pmat.SetColor(IdHazeColor, planetHazeColor);
                    // ★v19 : 림 부근에 섞을 하늘색. 안개색과 같은 소스를 쓴다 —
                    //   텍스처에 구워진 광무리 색(0.859,0.780,0.882)이 정확히 sunset fogColor 였다.
                    //   ★.linear 를 빼면 감마값이 그대로 들어가 림이 허옇게 뜬다.
                    pmat.SetColor(IdLimbSkyColor, fogColor.linear);
                    pmat.SetFloat(IdHazeBase, Mathf.Lerp(sunset.hazeBase, night.hazeBase, k));
                    pmat.SetFloat(IdHazeGrad, Mathf.Lerp(sunset.hazeGrad, night.hazeGrad, k));
                    pmat.SetFloat(IdDiscRadius, planetDiscRadiusNorm);
                    pmat.SetFloat(IdBottomFade, Mathf.Lerp(sunset.bottomFade, night.bottomFade, k));

                    // ★행성 앞 구름은 하늘과 같은 비율로 어두워져야 한다. 임의 곡선을 만들면
                    //   밤에 크림색 구름만 떠 보인다. 스카이박스 _Exposure 비율을 그대로 쓴다.
                    float expNow = Mathf.Lerp(sunset.exposure, night.exposure, k);
                    float expRef = Mathf.Max(1e-4f, sunset.exposure);
                    float cloudScale = expNow / expRef;
                    pmat.SetFloat(IdCloudOpacity, cloudOpacity);
                    pmat.SetColor(IdCloudTint, new Color(cloudScale, cloudScale, cloudScale, 1f));
                }
            }

            // ── 밝은 별 빌보드 ──
            PlaceStars();
            if (starRenderer != null)
            {
                var smat = starRenderer.sharedMaterial;
                if (smat != null)
                {
                    smat.SetFloat(IdIntensity, Mathf.Lerp(sunset.starIntensity, night.starIntensity, k));
                    smat.SetFloat(IdTwinkleDepth, Mathf.Lerp(sunset.starTwinkleDepth, night.starTwinkleDepth, k));
                    smat.SetFloat(IdMagCut, Mathf.Lerp(sunset.starMagCut, night.starMagCut, k));
                    smat.SetFloat(IdMagSoft, starMagSoft);
                    smat.SetFloat(IdSizeBoost, Mathf.Lerp(sunset.starSizeBoost, night.starSizeBoost, k));
                }
            }

            ApplyMassifFog(fogColor, fogStart, fogEnd);
        }

        void ApplyMassifFog(Color fogColor, float fogStart, float fogEnd)
        {
            if (massifRoot == null || _baseTop == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            float span = Mathf.Max(1f, fogEnd - fogStart);
            int n = massifRoot.childCount;
            for (int i = 0; i < n && i < _baseTop.Length; i++)
            {
                var mr = massifRoot.GetChild(i).GetComponent<MeshRenderer>();
                if (mr == null) continue;

                float d = (massifDistances != null && i < massifDistances.Length) ? massifDistances[i] : fogEnd;
                float f = Mathf.Min(Mathf.Clamp01((d - fogStart) / span), massifFogMax);

                _mpb.Clear();
                mr.GetPropertyBlock(_mpb);
                _mpb.SetColor(IdTop, Color.Lerp(_baseTop[i], fogColor, f));
                _mpb.SetColor(IdBottom, Color.Lerp(_baseBottom[i], fogColor, f * bottomFogScale));
                mr.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>산괴별 안개 계수 f 를 조회한다(보고·디버그용).</summary>
        public float GetMassifFogFactor(int index)
        {
            float k = Mathf.Clamp01(t);
            float fogStart = Mathf.Lerp(sunset.fogStart, night.fogStart, k);
            float fogEnd = Mathf.Lerp(sunset.fogEnd, night.fogEnd, k);
            if (massifDistances == null || index < 0 || index >= massifDistances.Length) return 0f;
            return Mathf.Min(Mathf.Clamp01((massifDistances[index] - fogStart) / Mathf.Max(1f, fogEnd - fogStart)), massifFogMax);
        }
    }
}
