Shader "Seocheon/SkyboxAurora"
{
    // 서천 꽃밭 스카이박스. 하늘은 내장 Skybox/Cubemap 과 완전히 동일하게 동작하고,
    // 오로라만 큐브맵 알파(강도 마스크)에서 읽어 하늘 노출과 독립적으로 가산한다.
    //
    // ★왜 알파로 옮겼는가 (v16 판정)
    //   1) 오로라를 RGB 에 구우면 t=0 에서 밝은 하늘 위에 더해져 12.6% 가 1.0 에 클립 -> 흰색으로 무너짐
    //   2) 색 램프가 방위 σ 에 묶여 있어 σ 를 넓히면 화면 안에 램프 중앙(탈색 라벤더)만 들어옴
    //   3) 큐브맵 RGB 인 한 _Exposure 가 하늘과 오로라에 똑같이 걸려 밤에 함께 사라짐
    //   -> 강도만 알파에 굽고, 색은 여기서 방위로 재구성하며, 밝기는 _AuroraGain 으로 분리한다.
    //
    // ★이 셰이더는 내장 Skybox/Cubemap 의 하늘 경로를 한 항도 바꾸지 않는다.
    //   _Tint(0.5 회색) x unity_ColorSpaceDouble x _Exposure 구조를 빠뜨리면 하늘 전체가
    //   조용히 절반 밝기로 어긋난다. _AuroraGain=0 으로 두고 내장과 픽셀 비교해 검증할 것.
    Properties
    {
        _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
        [NoScaleOffset] _Tex ("Cubemap   (HDR)", Cube) = "grey" {}

        [Header(Aurora)]
        _AuroraPeak ("Aurora Peak (고정 0.417)", Float) = 0.417
        _AuroraGain ("Aurora Gain (컨트롤러 구동)", Float) = 1.0
        _AuroraTint ("Aurora Tint (색 치환, 컨트롤러 구동)", Range(0,1)) = 0.0
        _AuroraCenter ("Aurora Center Compass", Float) = 157.5
        _AuroraRampWidth ("Aurora Ramp Width (deg, σ와 무관)", Float) = 100.0
        _AuroraColorA ("Aurora Color A (램프 시작)", Color) = (1.00, 0.20, 0.80, 1)
        _AuroraColorB ("Aurora Color B (램프 끝)", Color) = (0.20, 1.00, 0.90, 1)

        [Header(Flow v22)]
        _FlowTime ("Flow Time (초, 컨트롤러 구동)", Float) = 0
        _FlowSpeed ("Flow Speed", Float) = 0.35
        _FlowAmpAz ("Flow Amp Az (deg)", Float) = 0
        _FlowAmpEl ("Flow Amp El (deg)", Float) = 0
        _FlowPulse ("Flow Pulse", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            // VR 싱글패스 인스턴싱(SPI) 대응. CelestialBody / DistantSilhouette / StarBillboard 와 동일 패턴.
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            samplerCUBE _Tex;
            half4 _Tex_HDR;
            half4 _Tint;
            half _Exposure;
            float _Rotation;

            half _AuroraPeak;
            half _AuroraGain;
            half _AuroraTint;
            float _AuroraCenter;
            float _AuroraRampWidth;
            half4 _AuroraColorA;
            half4 _AuroraColorB;
            float _FlowTime;
            float _FlowSpeed;
            float _FlowAmpAz;
            float _FlowAmpEl;
            float _FlowPulse;

            // ★compass 규약 atan2(x,z) 의 정확한 역함수.
            //   atan2(sin(a)*ce, cos(a)*ce) = a , asin(sin(e)) = e 이므로 왕복 오차가 0 이다.
            float3 DirFromAzEl (float azDeg, float elDeg)
            {
                float a = radians(azDeg);
                float e = radians(elDeg);
                float ce = cos(e);
                return float3(sin(a) * ce, sin(e), cos(a) * ce);
            }

            float3 RotateAroundYInDegrees (float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            struct appdata_t
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 rotated = RotateAroundYInDegrees(v.vertex, _Rotation);
                o.vertex = UnityObjectToClipPos(rotated);
                o.texcoord = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 tex = texCUBE (_Tex, i.texcoord);

                // ── 하늘: 내장 Skybox/Cubemap 과 동일 ──
                // ★DecodeHDR 을 쓰지 않는다. DecodeHDR 은 data.a 로 rgb 를 변조하는데,
                //   v13 부터 알파에 오로라 마스크가 들어가므로 하늘이 오로라 모양으로 어두워진다.
                //   비HDR 큐브맵은 _Tex_HDR = (1,1,0,0) 이라 DecodeHDR 결과가 tex.rgb 와 같으므로
                //   아래 식은 v13 에 대해 내장과 동일하다. (게이트 A 가 이걸 확인한다)
                half3 c = tex.rgb;
                c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb;
                c *= _Exposure;

                // ── 오로라: 강도는 알파에서, 색은 방위에서 재구성 ──
                // 같은 texCUBE 샘플을 재사용하므로 텍스처 fetch 추가는 0.
                // ★compass 는 큐브맵을 구울 때 쓴 규약과 반드시 같아야 한다:
                //     compass = atan2(dir.x, dir.z), 0=+Z, 90=+X, 시계방향
                //   좌우가 뒤집히면 마젠타와 청록이 서로 반대편으로 간다. 렌더로 확인할 것.
                // ★_Rotation 이 걸려도 texcoord 기준이므로 색이 마스크를 따라간다.
                float compass = degrees(atan2(i.texcoord.x, i.texcoord.z));
                compass = compass - 360.0 * floor(compass / 360.0);

                // 중심에서의 부호 있는 각차 (-180..180) — 램프를 중심 대칭으로 깐다.
                float d = compass - _AuroraCenter;
                d = d - 360.0 * floor(d / 360.0 + 0.5);

                // ★램프 폭을 σ 에서 분리한다. 이것이 v16 문제 2 의 본체다.
                //   창 바깥은 saturate 로 끝색 유지 -> 넓은 방위에서도 채도가 남는다.
                float uAz = saturate(d / _AuroraRampWidth + 0.5);

                // ★v18: 색을 고도 주도로 바꾼다. 실제 오로라는 아래 가장자리가 분홍·마젠타,
                //   위쪽 몸통이 청록이다. 방위 단독이면 단조 무지개가 되고, 색 대비가
                //   화면의 방위 폭에 의존해 버린다. 고도 주도면 한 화면 안에서 위아래로
                //   마젠타와 청록이 같이 잡힌다.
                float elDeg = degrees(asin(saturate(normalize(i.texcoord).y)));
                float uAlt = saturate((elDeg - 22.0) / 28.0);
                float u = saturate(0.75 * uAlt + 0.25 * uAz);
                half3 auroraCol = lerp(_AuroraColorA.rgb, _AuroraColorB.rgb, u);

                // ── ★v22 오로라 흐름 ──
                // 오로라는 큐브맵 알파에 구워져 있어 정지다. ★알파만★ 살짝 틀어진 방향에서
                // 다시 샘플링해 커튼이 물결치게 한다.
                // ★RGB 는 절대 워프하지 않는다. 워프하면 별·구름이 함께 흘러 하늘 전체가 돈다.
                // ★고도에 따라 위상이 달라져야 "물결" 이 된다. el 항을 빼면 커튼이 통째로 미끄러진다.
                // ★시간은 _Time 이 아니라 _FlowTime(컨트롤러 주입)이다. 안 그러면 판정 재현이 불가능하다.
                half aFlow = tex.a;
                if (_FlowAmpAz > 0.0001 || _FlowAmpEl > 0.0001)
                {
                    float elT = degrees(asin(clamp(normalize(i.texcoord).y, -1.0, 1.0)));
                    float ph  = _FlowTime * _FlowSpeed;
                    float dAz = _FlowAmpAz * ( sin(ph * 0.61 + elT * 0.13)       * 0.6
                                             + sin(ph * 0.37 + elT * 0.21 + 1.7) * 0.4 );
                    float dEl = _FlowAmpEl *   sin(ph * 0.29 + compass * 0.05 + 0.9);
                    aFlow = texCUBE(_Tex, DirFromAzEl(compass + dAz, elT + dEl)).a;
                }
                // 밝기 파동 — 추가 페치 없이 공짜.
                aFlow *= 1.0 + _FlowPulse * sin(_FlowTime * _FlowSpeed * 0.44 + compass * 0.11 + elDeg * 0.07);
                aFlow = saturate(aFlow);

                // ── ★v19 ① 색 치환 ──
                // 가산 합성은 밝은 노을 하늘에서 채도를 만들 수 없다 (v17 확정).
                // 하늘이 G 결핍 파랑이라 무엇을 더해도 G 를 메워 흰쪽으로 탈색된다.
                // 그래서 t=0 에서는 더하지 말고 ★바꿔치기 한다 — 휘도는 그대로 두고 색상만.
                // ★L/aL 휘도 정규화를 빼면 밝기가 변하고 클리핑이 생긴다. 이게 있어야 원리적으로 0.
                // ★반드시 가산 항보다 먼저 와야 한다.
                half  Lp  = dot(c, half3(0.2126, 0.7152, 0.0722));
                half  aLp = max(1e-4, dot(auroraCol, half3(0.2126, 0.7152, 0.0722)));
                half3 tgt = auroraCol * (Lp / aLp);
                c = lerp(c, tgt, saturate(aFlow * _AuroraTint));

                // ── ② 가산 (기존 항 그대로) ──
                c += auroraCol * aFlow * _AuroraPeak * _AuroraGain;

                return half4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
