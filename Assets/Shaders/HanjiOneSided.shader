Shader "Onggojip/HanjiOneSided"
{
    // 한지 창호: 한쪽 면은 불투명(종이), 반대 면은 반투명(필름).
    //  · 불투명 면이 뒤의 이상한 배경(나무 등)을 가려줌
    //  · 반대에서 보면 은은하게 비치는 종이 질감
    //  · 앞/뒤가 반대로 보이면 재질에서 _CullOpaque ↔ _CullTrans 값을 서로 바꾸면 됨(Front=1, Back=2)
    Properties
    {
        _BaseMap ("Base Map (문 텍스처)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Alpha ("반투명 면 알파(0=투명)", Range(0,1)) = 0.45
        [Enum(UnityEngine.Rendering.CullMode)] _CullOpaque ("불투명 면 Cull", Float) = 1  // Front
        [Enum(UnityEngine.Rendering.CullMode)] _CullTrans  ("반투명 면 Cull", Float) = 2  // Back
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float  _Alpha;
            float  _CullOpaque;
            float  _CullTrans;
        CBUFFER_END
        ENDHLSL

        // ── 불투명 면 ────────────────────────────────
        Pass
        {
            Name "OpaqueSide"
            Tags { "LightMode"="UniversalForward" }
            Cull [_CullOpaque]
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 pos:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float3 nrm:TEXCOORD1; float2 uv:TEXCOORD0; };

            V vert (A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.nrm = TransformObjectToWorldNormal(i.normal);
                o.uv  = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(normalize(i.nrm), mainLight.direction));
                half3 lit = mainLight.color * (ndl * 0.7 + 0.3);   // 부드러운 음영 + 기본 밝기
                c.rgb *= lit;
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }

        // ── 반투명 면 ────────────────────────────────
        Pass
        {
            Name "TransSide"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull [_CullTrans]
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            V vert (A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv  = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                return half4(c.rgb, _Alpha);
            }
            ENDHLSL
        }
    }
}
