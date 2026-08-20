using UnityEngine;
using UnityEngine.Rendering;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 화면 암전 — 씬 전환의 VR 멀미 방지용(문서 필수 요구사항).
    ///
    /// ■ 왜 Canvas가 아니라 카메라에 붙는 쿼드인가
    ///   ScreenSpace-Overlay 캔버스는 헤드셋에 렌더되지 않는다(에디터 Game 뷰에만 보임).
    ///   ScreenSpace-Camera로 바꿔도 XR 스테레오에서 눈별 정렬이 어긋난다. 그래서
    ///   **현재 활성 카메라의 자식으로 근평면 바로 앞에 놓은 언릿 쿼드**로 덮는다 —
    ///   플랫(디버그 워커)과 VR 양쪽에서 같은 코드로 동작한다.
    ///
    /// ■ 카메라가 씬마다 바뀐다
    ///   전환 도중 씬이 갈리면 카메라 인스턴스도 갈린다. 그래서 쿼드는 이 컴포넌트가
    ///   들고 있다가 매 프레임 <see cref="Follow"/>로 현재 카메라 앞에 다시 붙인다.
    ///   부모를 카메라로 삼지 않는 이유: 씬 언로드 때 같이 파괴되기 때문.
    ///
    /// ■ 알파 0일 때는 렌더러를 꺼 둔다
    ///   투명 쿼드가 근평면 앞에 상시 떠 있으면 오버드로도 그렇고, 무엇보다 반투명
    ///   정렬에 끼어들어 물·안개 같은 것과 순서가 꼬인다.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class ScreenFader : MonoBehaviour
    {
        [Tooltip("암전 색 (알파는 무시 — 진행도로 덮어쓴다)")]
        public Color color = Color.black;

        /// <summary>0 = 투명, 1 = 완전 암전.</summary>
        public float Alpha { get; private set; }

        MeshRenderer _mr;
        Transform _quad;
        MaterialPropertyBlock _mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            BuildQuad();
            SetAlpha(0f);
        }

        void LateUpdate()
        {
            // 카메라가 움직인 뒤(LateUpdate) 따라붙어야 한 프레임 밀리지 않는다.
            if (Alpha > 0f) Follow();
        }

        /// <summary>0~1. 1이면 완전 암전.</summary>
        public void SetAlpha(float a)
        {
            Alpha = Mathf.Clamp01(a);
            if (_mr == null) return;

            bool visible = Alpha > 0.001f;
            _mr.enabled = visible;
            if (!visible) return;

            var c = color; c.a = Alpha;
            _mr.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);   // URP Unlit
            _mpb.SetColor(ColorId, c);       // 빌트인 폴백 셰이더용
            _mr.SetPropertyBlock(_mpb);
            Follow();
        }

        /// <summary>현재 활성 카메라 바로 앞에 쿼드를 세운다.</summary>
        void Follow()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                // MainCamera 태그가 없는 씬(디버그 워커 카메라 등) 대비
                cam = Camera.current;
                if (cam == null)
                {
                    var all = Camera.allCameras;
                    if (all.Length > 0) cam = all[0];
                }
            }
            if (cam == null) { _mr.enabled = false; return; }

            float dist = Mathf.Max(cam.nearClipPlane * 1.5f, 0.05f);
            _quad.SetPositionAndRotation(
                cam.transform.position + cam.transform.forward * dist,
                cam.transform.rotation);

            // 시야를 넉넉히 덮는다 — VR은 눈마다 절두체가 비대칭이라 여유를 크게 준다.
            float h = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float w = h * Mathf.Max(cam.aspect, 1f);
            float s = Mathf.Max(h, w) * 4f;
            _quad.localScale = new Vector3(s, s, 1f);
        }

        void BuildQuad()
        {
            var go = new GameObject("암전_쿼드") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            _quad = go.transform;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = MakeQuadMesh();

            _mr = go.AddComponent<MeshRenderer>();
            _mr.shadowCastingMode = ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.lightProbeUsage = LightProbeUsage.Off;
            _mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _mr.allowOcclusionWhenDynamic = false;
            _mr.sharedMaterial = MakeMaterial();

            _mpb = new MaterialPropertyBlock();
        }

        static Mesh MakeQuadMesh()
        {
            var m = new Mesh { name = "암전_쿼드메시", hideFlags = HideFlags.DontSave };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f),
            };
            m.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            // 근평면 앞이라 컬링 대상이 되지 않게 경계를 크게 잡는다
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            return m;
        }

        static Material MakeMaterial()
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            var mat = new Material(sh) { name = "암전_재질", hideFlags = HideFlags.DontSave };

            // URP Unlit을 투명으로 전환 — 인스펙터에서 Surface=Transparent 를 고른 것과 같다.
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);                                   // 0 Opaque / 1 Transparent
                mat.SetFloat("_Blend", 0f);                                     // Alpha
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.SetInt("_Cull", (int)CullMode.Off);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            mat.renderQueue = (int)RenderQueue.Overlay;   // 물·안개·투명 소품보다 확실히 나중
            mat.SetColor(BaseColorId, new Color(0f, 0f, 0f, 0f));
            return mat;
        }
    }
}
