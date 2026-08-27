// 봉분 본체 — 정점컬러 잔디/흙 마스크 (BB_Body_float 호출)
// Shader Graph 미사용. URP Forward + Bonbun_Common.hlsl.
Shader "Seocheon/Bonbun_Body"
{
    Properties
    {
        [MainTexture] _GrassBC ("Grass BaseColor", 2D) = "white" {}
        [Normal]      _GrassN  ("Grass Normal", 2D)    = "bump" {}
        _GrassR ("Grass Roughness", 2D) = "gray" {}
        _DirtBC ("Dirt BaseColor", 2D)  = "white" {}
        [Normal] _DirtN ("Dirt Normal", 2D) = "bump" {}

        _DigThreshold ("Dig Threshold", Float) = 0.035
        _DigWidth     ("Dig Width", Float)     = 0.008
        _NoiseAmp1 ("Noise Amp 1", Float) = 0.020
        _NoiseAmp2 ("Noise Amp 2", Float) = 0.012
        _NoiseAmp3 ("Noise Amp 3", Float) = 0.006
        _MasterMin ("Master Min", Float) = 0.05
        _MasterMax ("Master Max", Float) = 1.70

        _GrassTileMain   ("Grass Tile Main (m)", Float)   = 0.50
        _GrassTileDetail ("Grass Tile Detail (m)", Float) = 0.16
        _GrassRotDetail  ("Grass Rot Detail (deg)", Float)= 35.0
        _GrassSoft   ("Grass Soft", Range(0,1)) = 0.5
        _GrassDesat  ("Grass Desat", Range(0,1)) = 0.3
        _DirtTileMain   ("Dirt Tile Main (m)", Float)   = 0.40
        _DirtTileDetail ("Dirt Tile Detail (m)", Float) = 0.12
        _DirtRotDetail  ("Dirt Rot Detail (deg)", Float)= 27.0
        _DirtSoft   ("Dirt Soft", Range(0,1)) = 0.5

        _DigAOMax ("Dig AO Max", Range(0,1)) = 0.5
        _DirtRough ("Dirt Roughness", Range(0,1)) = 0.85
        _GrassBrightness ("Grass Brightness", Float) = 1.0
        _DirtBrightness  ("Dirt Brightness (HueSat 2.12)", Float) = 2.12
        _TriSharpness ("Triplanar Sharpness", Float) = 4.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off            // 백업 시점 값(양면). 캐비티 작업 전 상태로 복원

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Seocheon/Shaders/Bonbun_Common.hlsl"

            TEXTURE2D(_GrassBC); SAMPLER(sampler_GrassBC);
            TEXTURE2D(_GrassN);  SAMPLER(sampler_GrassN);
            TEXTURE2D(_GrassR);  SAMPLER(sampler_GrassR);
            TEXTURE2D(_DirtBC);  SAMPLER(sampler_DirtBC);
            TEXTURE2D(_DirtN);   SAMPLER(sampler_DirtN);

            CBUFFER_START(UnityPerMaterial)
                float4 _GrassBC_ST; float4 _GrassBC_TexelSize;
                float4 _GrassN_ST;  float4 _GrassN_TexelSize;
                float4 _GrassR_ST;  float4 _GrassR_TexelSize;
                float4 _DirtBC_ST;  float4 _DirtBC_TexelSize;
                float4 _DirtN_ST;   float4 _DirtN_TexelSize;
                float _DigThreshold, _DigWidth, _NoiseAmp1, _NoiseAmp2, _NoiseAmp3;
                float _MasterMin, _MasterMax;
                float _GrassTileMain, _GrassTileDetail, _GrassRotDetail, _GrassSoft, _GrassDesat;
                float _DirtTileMain, _DirtTileDetail, _DirtRotDetail, _DirtSoft;
                float _DigAOMax, _DirtRough, _GrassBrightness, _DirtBrightness, _TriSharpness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 2);
                float  fogFactor  : TEXCOORD3;
                float3 positionOS : TEXCOORD4;   // ★ 오브젝트 좌표: 트라이플래너/노이즈 샘플용(회전에 안 미끄러짐)
                float3 normalOS   : TEXCOORD5;   // ★ 오브젝트 법선: 트라이플래너 가중치/립 판정용
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs pin = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs   nin = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = pin.positionCS;
                o.positionWS = pin.positionWS;      // 조명/그림자/포그용 — 월드 유지
                o.normalWS   = nin.normalWS;
                o.positionOS = v.positionOS.xyz;    // 텍스처 샘플용 — 오브젝트
                o.normalOS   = v.normalOS;
                o.color      = v.color;          // ★ 정점컬러 = 마스크 입력 (raw, 감마변환 없음)
                OUTPUT_LIGHTMAP_UV(v.lightmapUV, unity_LightmapST, o.lightmapUV);
                OUTPUT_SH(o.normalWS, o.vertexSH);
                o.fogFactor = ComputeFogFactor(pin.positionCS.z);
                return o;
            }

            half4 frag (Varyings i, FRONT_FACE_TYPE vface : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 baseColor; float3 normalOSout; float smoothness; float occlusion;
                // 뒷면(캐비티 안쪽)이면 법선을 뒤집어 정상 음영
                float faceSign = IS_FRONT_VFACE(vface, 1.0, -1.0);
                float3 nOS = normalize(i.normalOS) * faceSign;
                BB_Body_float(
                    i.positionOS, nOS, i.color,   // ★ 오브젝트 좌표로 샘플 (뒷면 법선반전)
                    UnityBuildTexture2DStruct(_GrassBC), UnityBuildTexture2DStruct(_GrassN), UnityBuildTexture2DStruct(_GrassR),
                    UnityBuildTexture2DStruct(_DirtBC),  UnityBuildTexture2DStruct(_DirtN),
                    _DigThreshold, _DigWidth,
                    _NoiseAmp1, _NoiseAmp2, _NoiseAmp3,
                    _MasterMin, _MasterMax,
                    _GrassTileMain, _GrassTileDetail, _GrassRotDetail, _GrassSoft, _GrassDesat,
                    _DirtTileMain, _DirtTileDetail, _DirtRotDetail, _DirtSoft,
                    _DigAOMax, _DirtRough, _GrassBrightness, _DirtBrightness,
                    _TriSharpness,
                    baseColor, normalOSout, smoothness, occlusion);
                // 트라이플래너가 오브젝트공간 법선을 냈으니 조명용으로 월드로 변환
                float3 normalWS = normalize(TransformObjectToWorldNormal(normalOSout));

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalize(normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SAMPLE_GI(i.lightmapUV, i.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(i.lightmapUV);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = baseColor;
                surface.metallic = 0.0;
                surface.smoothness = smoothness;
                surface.occlusion = occlusion;
                surface.alpha = 1.0;
                surface.normalTS = float3(0,0,1);

                half4 col = UniversalFragmentPBR(inputData, surface);
                col.rgb = MixFog(col.rgb, inputData.fogCoord);
                return col;
            }
            ENDHLSL
        }

        // 그림자
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; };
            V shadowVert(A v){
                V o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                float3 wn = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 L = normalize(_LightPosition - wp);
            #else
                float3 L = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(wp, wn, L));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs; return o;
            }
            half4 shadowFrag(V i):SV_Target { return 0; }
            ENDHLSL
        }

        // 뎁스
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; };
            V depthVert(A v){ V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 depthFrag(V i):SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
