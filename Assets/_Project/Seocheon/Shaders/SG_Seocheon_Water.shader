// Seocheon 개천 수면 셰이더 (URP Unlit, Transparent)
// ★Ayo 원본 미변경 — 별도 셰이더. B안 룩 + 큐브맵 하늘반사(Fresnel) + 부드러운 태양 글린트.
// 화면 텍스처(Scene Color / Opaque) 미사용. 깊이 텍스처(_CameraDepthTexture)만 사용 → Opaque 불필요.
// 하늘 반사 = unity_SpecCube0(스카이박스에서 URP가 자동 생성하는 환경 큐브맵) 샘플. 리플렉션 프로브 불필요.
// SPI(단일 패스 인스턴싱) 안전: UNITY_VERTEX_OUTPUT_STEREO + 반사는 월드 반사벡터 큐브 룩업(스크린 무관).
Shader "Seocheon/SG_Seocheon_Water"
{
    Properties
    {
        [Header(Water Color)]
        _ShallowColor ("Shallow Color", Color) = (0.42,0.66,0.70,0.30)
        _DeepColor    ("Deep Color",    Color) = (0.09,0.24,0.30,0.85)
        _DepthDistance("Depth Distance (m)", Float) = 2.0

        [Header(Foam)]
        _FoamColor  ("Foam Color", Color) = (1,1,1,0.72)
        _FoamAmount ("Foam Depth (m)", Float) = 0.30
        _FoamCutoff ("Foam Cutoff", Range(0,1)) = 0.35

        [Header(Waves)]
        _Tiling      ("World Tiling", Float) = 0.08
        _WaveScale   ("Wave Scale", Float) = 1.4
        _WaveSpeed   ("Wave Speed", Float) = 0.15
        _WaveStrength("Wave Normal Strength", Range(0,1)) = 0.15

        [Header(Sky Reflection)]
        _ReflStrength ("Sky Reflection Strength", Range(0,2)) = 0.6
        _FresnelPower ("Fresnel Power", Range(0.5,8)) = 4.0
        _Smoothness   ("Smoothness (refl sharpness)", Range(0,1)) = 0.75

        [Header(Sun Glint)]
        _SunGlintColor    ("Sun Glint Color", Color) = (0.95,0.93,0.82,1)
        _SunGlintStrength ("Sun Glint Strength", Range(0,4)) = 1.4
        _SunGlintSharp    ("Sun Glint Sharpness", Range(1,400)) = 90
        _SunGlintSoft     ("Sun Glint Softness", Range(0.01,1)) = 0.35
        _Specular         ("Specular Boost", Range(0,4)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor, _DeepColor, _FoamColor, _SunGlintColor;
                float  _DepthDistance, _FoamAmount, _FoamCutoff;
                float  _Tiling, _WaveScale, _WaveSpeed, _WaveStrength;
                float  _ReflStrength, _FresnelPower, _Smoothness;
                float  _SunGlintStrength, _SunGlintSharp, _SunGlintSoft, _Specular;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash21(float2 p){ p = frac(p*float2(123.34,345.45)); p += dot(p, p+34.345); return frac(p.x*p.y); }
            float vnoise(float2 p){ float2 i=floor(p), f=frac(p); float2 u=f*f*(3.0-2.0*f);
                float a=hash21(i), b=hash21(i+float2(1,0)), c=hash21(i+float2(0,1)), d=hash21(i+float2(1,1));
                return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y); }
            float waveH(float2 w, float t){ return vnoise(w*_WaveScale + float2(t,t*0.3))
                                                 + 0.5*vnoise(w*_WaveScale*2.1 - float2(t*0.8,t)); }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos  = ComputeScreenPos(p.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // ── 물 깊이(깊이 텍스처만 사용) ──
                float2 suv = i.screenPos.xy / max(i.screenPos.w, 1e-5);
                float rawD = SampleSceneDepth(suv);
                float sceneEye = LinearEyeDepth(rawD, _ZBufferParams);
                float surfEye  = i.screenPos.w;
                float waterDepth = max(0.0, sceneEye - surfEye);

                // ── 애니메이션 파도 노멀(절차적) ──
                float2 w = i.positionWS.xz * _Tiling;
                float t = _Time.y * _WaveSpeed;
                float e = 0.06;
                float hx = waveH(w+float2(e,0), t) - waveH(w-float2(e,0), t);
                float hz = waveH(w+float2(0,e), t) - waveH(w-float2(0,e), t);
                float3 N = normalize(float3(-hx*_WaveStrength*4.0, 1.0, -hz*_WaveStrength*4.0));

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));

                // ── 하늘 큐브맵 반사 + Fresnel ──
                float3 R = reflect(-V, N);
                float mip = (1.0 - _Smoothness) * 6.0;
                half4 enc = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, R, mip);
                float3 sky = DecodeHDREnvironment(enc, unity_SpecCube0_HDR);
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower); // 비스듬 → 1, 수직 → 0
                float reflMix = saturate(fres * _ReflStrength);

                // ── 물색(깊이 기반) ──
                float depth01 = saturate(waterDepth / max(_DepthDistance, 0.01));
                float4 wcol = lerp(_ShallowColor, _DeepColor, depth01);
                float3 baseCol = wcol.rgb;
                float alpha = wcol.a;

                // ── 부드러운 태양 글린트(하늘·태양색, 계조) ──
                Light mainL = GetMainLight();
                float3 H = normalize(mainL.direction + V);
                float ndh = saturate(dot(N, H));
                float glintRaw = pow(ndh, _SunGlintSharp);
                float glint = smoothstep(0.0, _SunGlintSoft, glintRaw);              // 흰색 뚝→ 계조 페이드
                float3 glintCol = _SunGlintColor.rgb * (mainL.color*0.5 + sky*0.5);  // 태양+하늘색 혼합 → 과노출 감소
                float3 glintTerm = glintCol * glint * _SunGlintStrength * _Specular;

                // ── 합성: 물색 → 하늘반사 → 글린트 → 물가 거품 ──
                float3 col = lerp(baseCol, sky, reflMix);
                col += glintTerm;

                float foam = 1.0 - saturate(waterDepth / max(_FoamAmount, 0.01));
                float fn = vnoise(w*4.0 + float2(t,-t));
                foam = smoothstep(_FoamCutoff, 1.0, foam * (0.6 + 0.4*fn));
                col = lerp(col, _FoamColor.rgb, foam);
                alpha = lerp(alpha, _FoamColor.a, foam);
                alpha = saturate(alpha + reflMix*0.25); // 반사 강한 곳은 살짝 더 불투명

                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
