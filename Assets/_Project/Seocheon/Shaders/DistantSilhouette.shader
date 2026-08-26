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
            // VR 싱글패스 인스턴싱(SPI) 대응. 700m 산괴는 양안 어긋남이 특히 잘 보이는 거리다.
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
                float4 _BottomColor; float4 _TopColor;
            CBUFFER_END
            Varyings vert(Attributes v){
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 frag(Varyings i):SV_Target { return lerp(_BottomColor,_TopColor,saturate(i.uv.y)); }
            ENDHLSL
        }
    }
    Fallback Off
}