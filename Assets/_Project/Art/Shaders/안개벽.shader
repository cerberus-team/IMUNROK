// 안개벽 — 세상의 가장자리에 두르는 안개 고리.
//
// 왜 이것이 따로 필요한가: 유니티의 안개(RenderSettings.fog)는 <b>카메라에서 잰
// 거리</b>로 짙어진다. 그러니 걸어 나가도 맑은 자리는 늘 나를 따라오고, 안개는
// 언제나 저 멀리에 있다. 우리가 그리려는 것은 그 반대다 — 안개는 <b>조사청을 둘러
// 제자리에 있고</b>, 걸어 나가면 내가 그 속으로 들어간다. 그러려면 안개가 세계에
// 박힌 물건이어야 한다. 그래서 고리 모양 껍데기를 세우고 여기에 이 셰이더를 바른다.
//
// 짙기는 <b>높이로</b> 정한다. 발치는 짙고 위로 갈수록 옅어져, 고개를 들면 안개
// 너머로 하늘이 보인다. 안개가 하늘까지 덮으면 상자에 갇힌 것이 되고, 그러면
// 문을 열고 나가 하늘을 볼 까닭이 없어진다.
//
// 결은 <b>월드 좌표로</b> 뜬다. 껍데기의 uv 로 뜨면 고리가 한 바퀴 돌아 맞물리는
// 자리에 솔기가 선다 — 안개에 세로줄이 하나 그어진다.
Shader "이문록/안개벽"
{
    Properties
    {
        _Color   ("빛깔",              Color)        = (0.60, 0.65, 0.74, 1)
        _Alpha   ("짙기",              Range(0, 1))  = 0.55
        _FootY   ("밑동 높이(월드 y)", Float)        = 135
        _Height  ("옅어지는 높이(m)",  Float)        = 24
        _Curve   ("위로 옅어지는 세기", Range(0.4, 5)) = 1.9
        _Scale   ("결의 굵기",         Float)        = 0.035
        _Speed   ("흐르는 속도",       Float)        = 0.35
        _Wisp    ("결의 세기",         Range(0, 1))  = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        // 고리 안에 서 있으므로 <b>안쪽 면</b>을 봐야 한다. 자르지 않으면 앞뒤가
        // 겹쳐 두 겹으로 짙어지지만, 그 두 겹이 안개에 깊이를 준다 — 일부러 둔다.
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Alpha;
                float  _FootY;
                float  _Height;
                float  _Curve;
                float  _Scale;
                float  _Speed;
                float  _Wisp;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 값 잡음 한 겹. 네 귀퉁이를 매끈하게 섞는다.
            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs v = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = v.positionCS;
                OUT.positionWS  = v.positionWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ① 높이 — 발치가 짙고 위가 옅다
                float h  = saturate((IN.positionWS.y - _FootY) / max(_Height, 0.001));
                float a  = pow(1.0 - h, _Curve);

                // 맨 밑동은 조금 눅인다. 그러지 않으면 안개가 땅에 칼로 잘린 듯 끝난다.
                a *= lerp(0.55, 1.0, smoothstep(0.0, 0.09, h));

                // ② 결 — 월드 xz 로 뜨고 천천히 흐른다. 솔기가 없다.
                float2 q = IN.positionWS.xz * _Scale;
                q += float2(_Time.y * _Speed * 0.06, _Time.y * _Speed * 0.02);
                q += IN.positionWS.y * 0.012;
                float n = VNoise(q) * 0.62 + VNoise(q * 2.7 + 13.1) * 0.38;
                a *= lerp(1.0, n * 1.6 + 0.25, _Wisp);

                return half4(_Color.rgb, saturate(a * _Alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
