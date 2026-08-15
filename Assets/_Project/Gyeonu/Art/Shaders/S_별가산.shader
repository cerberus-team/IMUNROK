// 관측실 천장 별 전용 가산 셰이더 (2026-08-14).
// 정적 합성 메시 위에서 별마다 다른 색·밝기·반짝임을 내기 위한 것:
//   COLOR      = 별 색조 (LDR 색상만 — HDR 세기는 uv1.z에 분리. CombineMeshes가
//                정점색을 LDR로 눌러도 밝기가 안 죽게 하기 위함)
//   TEXCOORD1  = (x 반짝임 위상 0~1, y 반짝임 속도 지터, z HDR 세기, w 미사용)
// 가산(One One)이라 별이 겹치면 빛이 저절로 뭉쳐 밝아진다.
// 파티클이 아니라 에디터 포커스·Play 여부와 무관하게 항상 렌더된다.
Shader "IMUNROK/별_가산"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _Intensity("Intensity", Float) = 1
        _TwinkleAmp("Twinkle Amp", Range(0, 1)) = 0.15
        _TwinkleSpeed("Twinkle Speed", Float) = 1.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "StarAdditive"
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 tw : TEXCOORD1;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Intensity, _TwinkleAmp, _TwinkleSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                // 위상·속도가 정점(별)마다 달라 제각각 숨쉬듯 깜빡인다
                float fl = 1.0 - _TwinkleAmp * (0.5 + 0.5 * sin(_Time.y * _TwinkleSpeed * (0.6 + IN.tw.y) + IN.tw.x * 6.2831853));
                OUT.color = half4(IN.color.rgb * (IN.tw.z * fl * _Intensity), 1);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).r;
                return half4(IN.color.rgb * t, 1);
            }
            ENDHLSL
        }
    }
}
