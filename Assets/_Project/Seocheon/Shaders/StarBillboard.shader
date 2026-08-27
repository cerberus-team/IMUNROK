Shader "Seocheon/StarBillboard"
{
    // 밝은 별 전용 가산 빌보드. 단일 메시 / 드로우콜 1.
    // ★RenderSettings.fog 를 적용하지 않는다. 배치 거리가 2500m 라 fogEnd 700 이면 통째로 지워진다.
    // ★반짝임은 정점 셰이더에서 별마다 다른 위상·속도로 계산한다. CPU 비용 0.
    //   uv2.x = 위상 seed, uv2.y = 각속도. 전부 같은 박자로 뛰면 즉시 들통나므로 별마다 다르다.
    // ★큐브맵에 구운 별은 하늘과 같은 _Exposure 로 함께 어두워져 t 에 따라 돋아날 수 없다.
    //   그래서 밝은 별은 여기로 분리하고 _Intensity 를 t 로 구동한다.
    Properties
    {
        _MainTex ("Star Sprite (R)", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1.0
        _TwinkleDepth ("Twinkle Depth", Range(0,0.5)) = 0.35
        _MagCut ("Magnitude Cut", Range(0,1)) = 0.30
        _MagSoft ("Magnitude Softness", Range(0.01,0.5)) = 0.16
        _SizeBoost ("Size Boost", Range(0.5,3)) = 1.0
        _TwinkleZenith ("Twinkle Zenith Factor", Range(0.1,1)) = 0.40
        _StarTime ("Star Time (초, 컨트롤러 구동)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-110" }
        Pass
        {
            Name "StarBillboard"
            Blend One One
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 modu       : TEXCOORD1;
                float3 center     : TEXCOORD2;
                float2 rw         : TEXCOORD3;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Intensity;
                float _TwinkleDepth;
                float _MagCut;
                float _MagSoft;
                float _SizeBoost;
                float _TwinkleZenith;
                float _StarTime;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // ★t=0 은 하늘이 밝아(표시 0.844) 가산의 천장이 0.156 뿐이다.
                //   밝기로는 또렷해질 수 없으므로 보이는 소수의 별을 키운다.
                float suSize = saturate((v.modu.y - 0.55) / (2.30 - 0.55));
                float sizePeriod = lerp(2.5, 0.8, suSize);
                float sizeOsc = sin(_StarTime * (6.2831853 / sizePeriod) + v.modu.x);
                float strongTier = step(1.10, v.rw.x);
                float sizePulse = 1.0 + strongTier * 0.10 * sizeOsc;
                float3 pos = v.center + (v.positionOS.xyz - v.center) * (_SizeBoost * sizePulse);
                o.positionCS = TransformObjectToHClip(pos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // ★등급 임계. 밝기 배율만 올리면 900개가 통째로 밝아져 먼지가 된다.
                //   어느 별이 보이는가가 t 에 따라 달라져야 하므로 임계를 별도로 구동한다.
                //   계단이 아니라 부드러운 전환이어야 별이 툭 나타나지 않는다.
                float k = saturate((v.color.a - _MagCut) / max(1e-4, _MagSoft));
                k = k * k * (3.0 - 2.0 * k);
                // ── 반짝임 ──
                // modu.x = 위상 seed, modu.y = 메시에 구워진 원속도(0.55~2.30 rad/s).
                // ★메시를 다시 굽지 않고 정점 단계에서 주기대(1.5~4초)로 재사상한다.
                float su = saturate((v.modu.y - 0.55) / (2.30 - 0.55));
                float s1 = lerp(1.6, 4.2, su);                 // 주기 1.50 ~ 3.93초
                float p1 = v.modu.x;
                s1 = 6.2831853 / lerp(2.5, 0.8, su);
                // ★단일 sin 은 주기가 규칙적이라 기계적으로 읽힌다. 주파수가 다른 두 개를 합성.
                //   비율은 1.6~2.3 이며 정수배를 피해 주기가 다시 맞아떨어지지 않게 한다.
                float r2 = 1.6 + 0.7 * frac(p1 * 1.37);
                float s2 = s1 * r2;
                s2 = 6.2831853 / lerp(0.8, 2.5, frac(p1 * 1.37));
                float p2 = frac(p1 * 2.7) * 6.2831853;
                // ★가중치도 별마다 다르다. w1 이 0.9 쪽이면 거의 단일 사인(단순 맥동),
                //   0.5 쪽이면 두 파가 대등해 불규칙. 고정 가중이면 모든 별이 같은 모양을 그린다.
                //   ★rw 는 메시에서 독립 시드로 뽑은 값이다. p1 에서 파생하면 상관이 생겨
                //   별들이 몇 개 그룹으로 뭉쳐 보인다.
                float w1 = v.rw.y;
                float osc = w1 * sin(_StarTime * s1 + p1) + (1.0 - w1) * sin(_StarTime * s2 + p2);

                // ★고도가 낮은 별일수록 더 반짝인다. 대기를 길게 통과하기 때문이고,
                //   화면에서 실제로 잘 보이는 영역도 거기다. 천정은 잠잠해야 하늘 전체가
                //   맥동해 보이지 않는다. center 는 카메라 기준 방향이므로 y 가 sin(고도).
                float elevSin = normalize(v.center).y;
                float eh = saturate((elevSin - 0.2588) / (0.7071 - 0.2588));   // 15도 -> 45도
                eh = eh * eh * (3.0 - 2.0 * eh);
                // ★진폭도 별마다 다르다. r 은 낮은 쪽에 치우친 분포(0.20~1.00)라
                //   대다수는 거의 가만히 있고 소수만 크게 흔들린다. 실제 밤하늘의 모습이다.
                float d = min(_TwinkleDepth * lerp(1.0, _TwinkleZenith, eh) * v.rw.x, 0.5);

                // osc 는 [-1,1] 이므로 tw 는 [1-2d, 1]. d<=0.5 라 0 아래로 내려가지 않는다.
                float tw = 1.0 + d * osc;
                o.color = float4(v.color.rgb, v.color.a * k * tw * _Intensity);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).r;
                return half4(i.color.rgb * a * i.color.a, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
