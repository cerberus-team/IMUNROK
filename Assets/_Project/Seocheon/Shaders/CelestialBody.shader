Shader "Seocheon/CelestialBody"
{
    // 거대 행성 + 광무리 + 위성용 빌보드.
    // ★RenderSettings.fog 를 적용하지 않는다. 배치 거리가 2500m 라 fogEnd 700 이면 통째로 지워진다.
    //   대신 _HazeColor / _HazeBase / _HazeGrad 로 대기 감쇠를 직접 섞는다.
    // ★haze 는 원반 내 세로 위치에 따라 기울기를 갖는다. 고도가 낮은 천체는 아래쪽일수록
    //   대기 경로가 길어 하늘색에 더 녹아야 한다. 위아래가 같은 값이면 스티커로 보인다.
    Properties
    {
        _MainTex ("Planet (RGBA)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Brightness ("Brightness", Float) = 1.0
        _HazeColor ("Haze Color", Color) = (0.414, 0.314, 0.582, 1)
        _HazeBase ("Haze Base (원반 상단)", Range(0,1)) = 0.30
        _HazeGrad ("Haze Gradient (하단 추가분)", Range(0,1)) = 0.35
        _DiscRadius ("Disc Radius (텍스처 정규화)", Range(0.01,0.5)) = 0.1333
        _BottomFade ("Bottom Alpha Fade (하단 소산)", Range(0,1)) = 0.30
        _FrontCloudTex ("Front Cloud (RGBA)", 2D) = "black" {}
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = 0.62
        _CloudTint ("Cloud Tint", Color) = (1,1,1,1)

        [Header(Rim Lift v21)]
        _RimLift ("Rim Lift (림 리프트 양)", Range(0,1.5)) = 0.0
        _RimLiftStart ("Rim Lift Start (r/R)", Range(0.4,0.8)) = 0.55

        [Header(Disc Gain v20)]
        _DiscGain ("Disc Gain (균일 배율)", Range(0.5,2.5)) = 1.0
        _DiscKnee ("Disc Knee", Range(0.5,1.0)) = 0.93
        _DiscCeiling ("Disc Ceiling", Range(0.5,1.0)) = 0.995

        [Header(Rim v19)]
        _RimSoft ("Rim Soft (원반반경 비율)", Range(0.005,0.12)) = 0.035
        _RimGlowFloor ("Rim Glow Floor (광무리 알파 바닥)", Range(0,0.5)) = 0.16
        _LimbBlendStart ("Limb Blend Start", Range(0.5,1.0)) = 0.85
        _LimbSkyBlend ("Limb Sky Blend", Range(0,1)) = 0.25
        _LimbSkyColor ("Limb Sky Color (linear)", Color) = (0.708,0.573,0.748,1)

        [Header(Satellite v19)]
        _SatTex ("Satellite (RGBA)", 2D) = "black" {}
        _BakedSatUV ("Baked Satellite UV (지우기)", Vector) = (0.28086,0.77441,0,0)
        _BakedSatR ("Baked Satellite Kill Radius (UV)", Float) = 0.0371
        _SatUV ("Satellite UV", Vector) = (0.333589,0.708379,0,0)
        _SatRadius ("Satellite UV Radius", Float) = 0.02
        _SatDirUV ("Satellite Dir from Planet (UV)", Vector) = (-0.62404,0.78142,0,0)
        _SatBrightness ("Satellite Brightness", Float) = 0.78
        _SatAmbient ("Satellite Ambient", Range(0,1)) = 0.42
        _SatPhase ("Satellite Phase (deg)", Range(0,90)) = 40
        _SatLimbDark ("Satellite Limb Darkening", Range(0,1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-100" }
        Pass
        {
            Name "Celestial"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // VR 싱글패스 인스턴싱(SPI) 대응. 2500m 쿼드는 양안 어긋남이 특히 잘 보이는 거리다.
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_FrontCloudTex); SAMPLER(sampler_FrontCloudTex);
            TEXTURE2D(_SatTex); SAMPLER(sampler_SatTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float4 _Color; float _Brightness;
                float4 _HazeColor; float _HazeBase; float _HazeGrad; float _DiscRadius; float _BottomFade;
                float4 _FrontCloudTex_ST; float _CloudOpacity; float4 _CloudTint;
                float _RimLift; float _RimLiftStart;
                float _DiscGain; float _DiscKnee; float _DiscCeiling;
                float _RimSoft; float _RimGlowFloor; float _LimbBlendStart; float _LimbSkyBlend; float4 _LimbSkyColor;
                float4 _SatTex_ST; float4 _BakedSatUV; float _BakedSatR; float4 _SatUV; float _SatRadius;
                float4 _SatDirUV; float _SatBrightness; float _SatAmbient; float _SatPhase; float _SatLimbDark;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // ── ★v19 : 텍스처에 구워진 위성을 지운다 ──
                // 위성은 T_Planet_Neptune_v15 에 구워져 있고 텍스처는 다시 구울 수 없다.
                // 다행히 그 자리(행성중심에서 r/R 2.6)는 광무리 밖이라 배경 알파가 실측 0.0000 이다.
                // 따라서 알파만 눌러 지우면 되고 인페인트가 필요 없다.
                c.a *= smoothstep(_BakedSatR * 0.82, _BakedSatR, length(i.uv - _BakedSatUV.xy));

                c.rgb *= _Color.rgb * _Brightness;

                // 원반 중심으로부터의 정규화 거리 (0 = 중심, 1 = 림)
                float rDisc = length(i.uv - 0.5) / max(1e-4, _DiscRadius);
                half  soft  = 1.0 - smoothstep(1.0 - _RimSoft, 1.0 + _RimSoft, rDisc);

                // ── ★v21 반경 의존 리프트 : 구워진 림 감쇠 보정 ──
                // T_Planet_Neptune_v15 자체가 r/R 0.40(휘도 0.652) -> 0.98(0.500) 으로 23% 어두워지고
                // 색도 남색으로 치우쳐 원반 둘레에 짙은 테두리가 보였다 (v21 진단 테스트 1·2·3 = 후보 A).
                // ★텍스처는 다시 굽지 않는다. 여기서 반경에 따라 되살린다.
                // ★전역 균일 배율(_DiscGain)은 중심 청색이 천장에 닿는 순간 전체가 묶인다(v20 확인).
                //   림은 천장에서 멀어 여유가 있으므로, 림만 올리면 테두리도 없애고 평균 휘도도 오른다.
                // ★r 에 대해 단조 증가여야 한다. 아니면 링을 하나 더 만든다.
                // ★_DiscGain 보다 ★앞★ 에서 적용해 뒤의 색도 보존 롤오프를 반드시 통과시킨다.
                c.rgb *= 1.0 + _RimLift * smoothstep(_RimLiftStart, 1.0, rDisc);

                // ── ★v20 원반 밝기 : RGB 균일 배율 ──
                // 행성이 이 씬의 주광원인데 하늘보다 어두워 "구멍" 으로 읽혔다.
                // ★채도 S=(max-min)/max 는 균일 배율에 대해 불변이므로 밝히는 데 드는 채도 비용이 0 이다.
                // ★소프트니는 반드시 ★최대채널★ 에 걸고 그 비율을 RGB 에 균일 적용한다.
                //   휘도에 걸면 청색이 하드 클램프되어 계조가 무너진다 (실패 확인된 경로).
                // ★원반에만 건다. soft 로 가중해 광무리(r/R>1)는 밝히지 않는다 — 밝히면 하늘에
                //   밝은 고리가 생긴다.
                // ★haze · 림 하늘색 혼입 · BottomFade 보다 앞에서 적용한다.
                half  gAmt = lerp(1.0, _DiscGain, soft);
                half3 dRGB = c.rgb * gAmt;
                half  dMax = max(dRGB.r, max(dRGB.g, dRGB.b));
                half  kOut = (dMax <= _DiscKnee)
                             ? dMax
                             : _DiscKnee + (_DiscCeiling - _DiscKnee)
                               * (1.0 - exp(-(dMax - _DiscKnee) / max(1e-4, _DiscCeiling - _DiscKnee)));
                c.rgb = dRGB * (kOut / max(1e-4, dMax));

                // ── ★v19 ① 림 소프트 엣지 ──
                // 원반 알파는 r/R 0.975 에서 1.0, 1.025 에서 0.16 으로 사실상 하드 컷이었다.
                // ★그냥 smoothstep 을 곱하면 r/R > 1 의 광무리까지 죽는다. 그래서 알파를
                //   [광무리 성분 + 원반 성분] 으로 분해해 ★원반 성분에만 소프트 엣지를 건다.
                half glowA = min(c.a, _RimGlowFloor);
                half discA = max(0.0, c.a - glowA);
                c.a = glowA + discA * soft;

                // 원반 내 세로 위치: 0 = 원반 하단, 1 = 원반 상단. 원반 밖은 clamp.
                float relY = saturate((i.uv.y - (0.5 - _DiscRadius)) / max(1e-4, 2.0 * _DiscRadius));
                float haze = saturate(_HazeBase + _HazeGrad * (1.0 - relY));
                c.rgb = lerp(c.rgb, _HazeColor.rgb, haze);

                // ── ★v19 ② 림 부근에만 하늘색 혼입 ──
                // 산·산괴는 안개로 하늘에 녹는데 행성만 하늘색이 한 톨도 안 섞여 스티커로 읽혔다.
                // ★중심부는 건드리지 않는다 (_LimbBlendStart 0.85 미만으로 내리면 채도가 무너진다).
                half wLimb = smoothstep(_LimbBlendStart, 1.0, rDisc) * _LimbSkyBlend;
                c.rgb = lerp(c.rgb, _LimbSkyColor.rgb, wLimb);

                // 하단 소산 — 대기 경로가 긴 아래쪽은 알파도 떨어져 하늘에 녹아든다.
                // haze(색) 만으로는 원반이 하늘 위에 얹힌 스티커로 읽힌다.
                c.a *= lerp(1.0 - _BottomFade, 1.0, relY);

                c.a *= _Color.a;

                // ── ★v19 ③ 위성 (절차) ──
                // 텍스처 베이크였던 위성을 셰이더로 옮겨 그린다. 이격 22도 -> 16도.
                // ★광원 방향은 별도 상수가 아니라 위성 오프셋(_SatDirUV)에서 파생된다.
                //   그 오프셋이 놓이는 쿼드 자체가 PlanetDirection() 으로 배치되므로
                //   광원 방향의 단일 소스는 PlanetDirection() 이다.
                float2 sl  = (i.uv - _SatUV.xy) / max(1e-4, _SatRadius);   // 원판 좌표 -1..1
                half4  st  = SAMPLE_TEXTURE2D(_SatTex, sampler_SatTex, sl * 0.5 + 0.5);
                float  nz  = sqrt(saturate(1.0 - dot(sl, sl)));            // 가상 구면 법선 z
                float3 nrm = float3(sl, nz);
                float  ph  = radians(_SatPhase);
                // 행성 쪽을 향하는 방향 = 위성 오프셋의 반대. 위상각이 작아 거의 보름달이 된다.
                float3 Ldir = normalize(float3(-normalize(_SatDirUV.xy) * sin(ph), cos(ph)));
                half   shade = _SatAmbient + (1.0 - _SatAmbient) * saturate(dot(nrm, Ldir));
                half   limbD = lerp(1.0, nz, _SatLimbDark);
                half3  sCol  = st.rgb * _SatBrightness * shade * limbD;
                sCol = lerp(sCol, _HazeColor.rgb, haze);   // 같은 거리이므로 같은 대기 감쇠
                // ★기하 마스크 필수. 위성 UV 도함수가 쿼드의 1/_SatRadius(약 52배)라
                //   GPU 가 아주 거친 밉을 고르고, Clamp 샘플이 원판 밖까지 알파를 번지게 해
                //   화면을 가로지르는 흰 십자선이 생긴다. 텍스처 알파만 믿으면 안 된다.
                half   satMask = 1.0 - smoothstep(0.965, 1.0, length(sl));
                half   sa = st.a * satMask;
                c.rgb = lerp(c.rgb, sCol, sa);
                c.a   = max(c.a, sa);

                // ── ★행성 앞을 지나가는 구름 ──
                // 원반 안 얼룩은 아무리 그려도 표면으로 읽힌다. 구름이 앞에 있다고 읽히는
                // 유일한 단서는 림(원반 윤곽선)이 끊기는 것이므로, 구름은 원반 바깥
                // 여백까지 이어져야 한다. 그래서 쿼드 UV 전체에 그린다.
                // ★haze 와 림 감쇠가 모두 끝난 뒤 마지막에 합성한다 — 구름은 행성 표면이
                //   아니라 그 앞에 있는 것이므로 haze 를 받으면 안 된다.
                // ★이 셰이더는 fog 를 적용하지 않는다(2500m 쿼드가 지워짐). 구름도 동일.
                half4 cld = SAMPLE_TEXTURE2D(_FrontCloudTex, sampler_FrontCloudTex, i.uv);
                half ca = cld.a * _CloudOpacity;
                c.rgb = lerp(c.rgb, cld.rgb * _CloudTint.rgb, ca);
                // ★max 가 빠지면 원반 바깥 구름이 보이지 않는다. 이 한 줄이 작업의 핵심이다.
                c.a = max(c.a, ca);
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
