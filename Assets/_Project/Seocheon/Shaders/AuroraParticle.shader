Shader "Seocheon/AuroraParticle"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Noise", 2D) = "white" {}
        _Intensity ("Intensity", Range(0,3)) = 1
        _EdgeSoftness ("Edge Softness", Range(0.2,4)) = 1.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex; half _Intensity; half _EdgeSoftness;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                half n=tex2D(_MainTex,float2(i.uv.x*0.72,i.uv.y)).r;
                half side=pow(saturate(1.0-abs(i.uv.x*2.0-1.0)),_EdgeSoftness);
                half vertical=smoothstep(0.0,0.18,i.uv.y)*(1.0-smoothstep(0.72,1.0,i.uv.y));
                half folds=0.55+0.45*sin((i.uv.x*7.0+i.uv.y*2.0)*UNITY_PI);
                half a=saturate(n*side*vertical*folds*i.color.a*_Intensity);
                return fixed4(i.color.rgb,a);
            }
            ENDCG
        }
    }
    Fallback Off
}
