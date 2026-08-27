// 별 길(비밀지도 안내) 전용 가산 셰이더.
// 관측실 천장 별(IMUNROK/별_가산)에서 갈라져 나왔다 — 천장 별은 제자리에서 숨쉬기만 하면 되지만
// 길 안내는 **진행 방향으로 빛이 흘러가야** 어디로 가라는 건지 읽힌다. 그래서 정점에
// "경로 시작점부터의 거리"를 실어 보내고, 그 거리를 따라 밝기 파동을 흘린다.
//
//   COLOR      = 별 색조 (LDR)
//   TEXCOORD1  = (x 반짝임 위상 0~1, y 반짝임 속도 지터, z HDR 세기, w 경로상 거리 m)
Shader "IMUNROK/별길_가산"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _Intensity("Intensity", Float) = 1
        _TwinkleAmp("Twinkle Amp", Range(0, 1)) = 0.3
        _TwinkleSpeed("Twinkle Speed", Float) = 1.6
        _FlowAmp("Flow Amp", Range(0, 1)) = 0.55
        _FlowLength("Flow Length (m)", Float) = 9
        _FlowSpeed("Flow Speed (m/s)", Float) = 6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "StarPathAdditive"
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
                float _FlowAmp, _FlowLength, _FlowSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;

                // 별마다 제각각 숨쉬는 반짝임
                float tw = 1.0 - _TwinkleAmp * (0.5 + 0.5 * sin(_Time.y * _TwinkleSpeed * (0.6 + IN.tw.y) + IN.tw.x * 6.2831853));

                // 경로를 따라 흘러가는 빛 — 거리(tw.w)에서 시간을 빼면 파동이 진행 방향으로 움직인다
                float phase = (IN.tw.w - _Time.y * _FlowSpeed) / max(0.5, _FlowLength);
                float flow = 0.5 + 0.5 * sin(phase * 6.2831853);
                flow = 1.0 - _FlowAmp + _FlowAmp * (flow * flow);   // 제곱 — 마루는 밝고 골은 길게

                OUT.color = half4(IN.color.rgb * (IN.tw.z * tw * flow * _Intensity), 1);
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
