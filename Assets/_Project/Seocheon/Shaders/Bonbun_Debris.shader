// 봉분 덩어리/뗏장 — contactAO(vcol.r) + 트라이플래너.
// 키워드 _ROUGHTEX: OFF=BB_Debris_float(흙, 러프니스 float) / ON=BB_DebrisR_float(잔디, 러프니스 텍스처)
Shader "Seocheon/Bonbun_Debris"
{
    Properties
    {
        [MainTexture] _BC ("BaseColor", 2D) = "white" {}
        [Normal] _NRM ("Normal", 2D) = "bump" {}
        [Toggle(_ROUGHTEX)] _UseRoughTex ("Use Roughness Texture (sod)", Float) = 0
        _ROUGH ("Roughness Tex", 2D) = "gray" {}
        _Roughness ("Roughness (dirt)", Range(0,1)) = 0.85
        _TileMain   ("Tile Main (m)", Float) = 0.40
        _TileDetail ("Tile Detail (m)", Float) = 0.12
        _RotDetail  ("Rot Detail (deg)", Float) = 27.0
        _Soft  ("Soft", Range(0,1)) = 0.5
        _Desat ("Desat", Range(0,1)) = 0.2
        _Brightness ("Brightness (dirt HueSat 2.12)", Float) = 2.12
        _AOFloor ("AO Floor", Range(0,1)) = 0.35
        _TriSharpness ("Triplanar Sharpness", Float) = 4.0
        _NormalStrength ("Normal Strength", Range(0,3)) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local _ROUGHTEX
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

            TEXTURE2D(_BC);  SAMPLER(sampler_BC);
            TEXTURE2D(_NRM); SAMPLER(sampler_NRM);
            TEXTURE2D(_ROUGH); SAMPLER(sampler_ROUGH);

            CBUFFER_START(UnityPerMaterial)
                float4 _BC_ST; float4 _BC_TexelSize;
                float4 _NRM_ST; float4 _NRM_TexelSize;
                float4 _ROUGH_ST; float4 _ROUGH_TexelSize;
                float _Roughness, _TileMain, _TileDetail, _RotDetail, _Soft, _Desat;
                float _Brightness, _AOFloor, _TriSharpness, _UseRoughTex, _NormalStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float4 color:COLOR; float2 lightmapUV:TEXCOORD1; };
            struct Varyings {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 color:COLOR;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 2); float fogFactor:TEXCOORD3;
                float3 positionOS:TEXCOORD4; float3 normalOS:TEXCOORD5;   // ★ 오브젝트 좌표: 트라이플래너 샘플용
            };

            Varyings vert(Attributes v){
                Varyings o=(Varyings)0;
                VertexPositionInputs pin=GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nin=GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS=pin.positionCS; o.positionWS=pin.positionWS; o.normalWS=nin.normalWS; o.color=v.color;
                o.positionOS=v.positionOS.xyz; o.normalOS=v.normalOS;     // ★ 텍스처 샘플용 — 오브젝트
                OUTPUT_LIGHTMAP_UV(v.lightmapUV, unity_LightmapST, o.lightmapUV);
                OUTPUT_SH(o.normalWS, o.vertexSH);
                o.fogFactor=ComputeFogFactor(pin.positionCS.z);
                return o;
            }

            half4 frag(Varyings i):SV_Target {
                float3 baseColor; float3 normalOSout; float smoothness; float occlusion;
            #if _ROUGHTEX
                BB_DebrisR_float(
                    i.positionWS, normalize(i.normalWS), i.color,   // ★ 월드 좌표로 샘플 (조각 55개, 원점 제각각)
                    UnityBuildTexture2DStruct(_BC), UnityBuildTexture2DStruct(_NRM), UnityBuildTexture2DStruct(_ROUGH),
                    _TileMain, _TileDetail, _RotDetail, _Soft, _Desat,
                    _Brightness, _AOFloor, _TriSharpness, _NormalStrength,
                    baseColor, normalOSout, smoothness, occlusion);
            #else
                BB_Debris_float(
                    i.positionWS, normalize(i.normalWS), i.color,   // ★ 월드 좌표로 샘플 (조각 55개, 원점 제각각)
                    UnityBuildTexture2DStruct(_BC), UnityBuildTexture2DStruct(_NRM),
                    _TileMain, _TileDetail, _RotDetail, _Soft, _Desat,
                    _Roughness, _Brightness, _AOFloor, _TriSharpness, _NormalStrength,
                    baseColor, normalOSout, smoothness, occlusion);
            #endif
                // 트라이플래너가 이미 월드공간 법선을 냈으니 그대로 사용
                float3 normalWS = normalize(normalOSout);

                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalWS=normalize(normalWS);
                inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord=i.fogFactor;
                inputData.bakedGI=SAMPLE_GI(i.lightmapUV, i.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask=SAMPLE_SHADOWMASK(i.lightmapUV);

                SurfaceData surface=(SurfaceData)0;
                surface.albedo=baseColor; surface.metallic=0; surface.smoothness=smoothness; surface.occlusion=occlusion; surface.alpha=1; surface.normalTS=float3(0,0,1);
                half4 col=UniversalFragmentPBR(inputData, surface);
                col.rgb=MixFog(col.rgb, inputData.fogCoord);
                return col;
            }
            ENDHLSL
        }

        Pass {
            Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex sv
            #pragma fragment sf
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A{float4 positionOS:POSITION; float3 normalOS:NORMAL;}; struct V{float4 positionCS:SV_POSITION;};
            V sv(A v){ V o; float3 wp=TransformObjectToWorld(v.positionOS.xyz); float3 wn=TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 L=normalize(_LightPosition-wp);
            #else
                float3 L=_LightDirection;
            #endif
                float4 cs=TransformWorldToHClip(ApplyShadowBias(wp,wn,L));
                #if UNITY_REVERSED_Z
                    cs.z=min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z=max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS=cs; return o; }
            half4 sf(V i):SV_Target{return 0;}
            ENDHLSL
        }
        Pass {
            Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ZWrite On ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex dv
            #pragma fragment df
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 positionOS:POSITION;}; struct V{float4 positionCS:SV_POSITION;};
            V dv(A v){ V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 df(V i):SV_Target{return 0;}
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
