Shader "Seocheon/CloudParticle"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Cloud Luminance", 2D) = "white" {}
        _Density ("Density", Range(0,2)) = 1
        _Threshold ("Threshold", Range(0,1)) = .34
        _Softness ("Softness", Range(.01,.5)) = .28
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex; half _Density; half _Threshold; half _Softness;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                half lum=dot(tex2D(_MainTex,i.uv).rgb,half3(.299,.587,.114));
                half cloud=smoothstep(_Threshold,_Threshold+_Softness,lum);
                half edge=smoothstep(0,.14,i.uv.x)*smoothstep(0,.14,i.uv.y)*smoothstep(0,.14,1-i.uv.x)*smoothstep(0,.14,1-i.uv.y);
                half a=saturate(cloud*edge*i.color.a*_Density);
                half3 lit=lerp(i.color.rgb*.58,i.color.rgb,cloud);
                return fixed4(lit,a);
            }
            ENDCG
        }
    }
    Fallback Off
}
