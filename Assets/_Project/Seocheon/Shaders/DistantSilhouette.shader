Shader "Seocheon/DistantSilhouette"
{
    Properties
    {
        _BottomColor ("Bottom Color", Color) = (0.40,0.48,0.42,1)
        _TopColor ("Top Color", Color) = (0.60,0.67,0.72,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+5" }
        Pass
        {
            Name "Silhouette"
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float4 _BottomColor; float4 _TopColor;
            CBUFFER_END
            Varyings vert(Attributes v){ Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 frag(Varyings i):SV_Target { return lerp(_BottomColor,_TopColor,saturate(i.uv.y)); }
            ENDHLSL
        }
    }
    Fallback Off
}