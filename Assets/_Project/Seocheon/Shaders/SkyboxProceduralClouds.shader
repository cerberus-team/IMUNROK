Shader "Seocheon/SkyboxProceduralClouds"
{
    Properties
    {
        _Tint ("Controller Tint", Color) = (.5,.5,.5,1)
        [Gamma] _Exposure ("Controller Exposure", Range(0,8)) = 1
        _Rotation ("Rotation", Range(0,360)) = 0
        [NoScaleOffset] _MainTex ("Colour Panorama", 2D) = "grey" {}
        [NoScaleOffset] _CloudNoise ("Cloud Noise", 2D) = "grey" {}
        _ColorBlurMip ("Colour Blur Mip", Range(0,9)) = 6
        _DramaGain ("Drama Gain", Range(0,2)) = .72
        _ColorBalance ("Color Balance", Color) = (.82,.90,1,1)
        _Saturation ("Saturation", Range(0,2)) = .82
        _Contrast ("Contrast", Range(.5,2)) = 1.04
        _HorizonLift ("Horizon Lift", Range(0,.5)) = .04
        _CloudCoverage ("Cloud Coverage", Range(0,1)) = .52
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = .62
        _CloudScale ("Cloud Scale", Range(.25,4)) = 1.15
        _CloudLight ("Cloud Light", Color) = (1,.68,.82,1)
        _CloudDark ("Cloud Dark", Color) = (.25,.16,.43,1)
        _ZenithColor ("Zenith Cap Color", Color) = (.16,.09,.30,1)
        _NadirColor ("Nadir Cap Color", Color) = (.32,.16,.24,1)
        _PoleBlendStart ("Pole Blend Start", Range(0,1)) = .70
        _PoleBlendEnd ("Pole Blend End", Range(0,1)) = .94
        [HideInInspector] _AuroraGain ("Aurora Gain", Float) = 0
        [HideInInspector] _AuroraTint ("Aurora Tint", Float) = 0
        [HideInInspector] _FlowTime ("Flow Time", Float) = 0
        [HideInInspector] _FlowAmpAz ("Flow Amp Az", Float) = 0
        [HideInInspector] _FlowAmpEl ("Flow Amp El", Float) = 0
        [HideInInspector] _FlowSpeed ("Flow Speed", Float) = 0
        [HideInInspector] _FlowPulse ("Flow Pulse", Float) = 0
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
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex,_CloudNoise;
            half4 _Tint,_ColorBalance,_CloudLight,_CloudDark,_ZenithColor,_NadirColor;
            half _Exposure,_ColorBlurMip,_DramaGain,_Saturation,_Contrast,_HorizonLift;
            half _CloudCoverage,_CloudOpacity,_CloudScale,_PoleBlendStart,_PoleBlendEnd;
            float _Rotation;
            struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float3 dir:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
            half sampleNoise(float2 p, float lod){return 1.0h-tex2Dlod(_CloudNoise,float4(frac(p.x),frac(p.y),0,lod)).r;}
            half4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.dir);float compass=atan2(d.x,d.z)+radians(_Rotation);
                float2 uv=float2(frac(compass/(2.0*UNITY_PI)+.5),saturate(asin(clamp(d.y,-1.0,1.0))/UNITY_PI+.5));
                half3 c=tex2Dlod(_MainTex,float4(uv,0,_ColorBlurMip)).rgb;
                c*=_Tint.rgb*unity_ColorSpaceDouble.rgb*_Exposure;c*=_ColorBalance.rgb*_DramaGain;
                half lum=dot(c,half3(.2126,.7152,.0722));c=lerp(lum.xxx,c,_Saturation);c=(c-.5h)*_Contrast+.5h;
                half horizon=1.0h-saturate(abs(d.y)*6.0h);c+=_HorizonLift*horizon*half3(1,.78,.88);

                float2 p=float2(uv.x*2.0,uv.y*1.35)*_CloudScale;
                half warp=sampleNoise(p*.48+float2(.17,.31),1.0)-.5h;p+=float2(warp*.28h,warp*.12h);
                half n=.50h*sampleNoise(p,0.0)+.27h*sampleNoise(p*2.07+float2(.23,.41),0.5)+.15h*sampleNoise(p*4.13+float2(.61,.19),1.0)+.08h*sampleNoise(p*8.21+float2(.37,.73),1.5);
                half cloud=smoothstep(_CloudCoverage,_CloudCoverage+.16h,n);
                half altitude=smoothstep(.505h,.565h,uv.y)*(1.0h-smoothstep(.82h,.94h,uv.y));
                half opening=1.0h-smoothstep(.18h,.02h,abs(uv.x-.64h))*smoothstep(.53h,.70h,uv.y);
                half alpha=cloud*altitude*_CloudOpacity*opening;
                half lightTerm=saturate((n-_CloudCoverage)/.30h+.25h);half3 cloudCol=lerp(_CloudDark.rgb,_CloudLight.rgb,lightTerm);
                c=lerp(c,cloudCol,alpha);
                half pole=smoothstep(_PoleBlendStart,_PoleBlendEnd,abs(d.y));c=lerp(c,d.y>=0?_ZenithColor.rgb:_NadirColor.rgb,pole);
                return half4(max(c,0),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
