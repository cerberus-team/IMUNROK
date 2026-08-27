Shader "Seocheon/SkyboxCloudDrama"
{
    Properties
    {
        _Tint ("Controller Tint", Color) = (.5,.5,.5,1)
        [Gamma] _Exposure ("Controller Exposure", Range(0,8)) = 1
        _Rotation ("Rotation", Range(0,360)) = 0
        [NoScaleOffset] _MainTex ("Equirectangular Cloud Panorama", 2D) = "grey" {}
        _DramaGain ("Drama Gain", Range(0,2)) = .72
        _ColorBalance ("Color Balance", Color) = (.82,.90,1,1)
        _Saturation ("Saturation", Range(0,2)) = .82
        _Contrast ("Contrast", Range(.5,2)) = 1.04
        _HorizonLift ("Horizon Lift", Range(0,.5)) = .04
        _ZenithColor ("Zenith Cap Color", Color) = (.16,.09,.30,1)
        _NadirColor ("Nadir Cap Color", Color) = (.32,.16,.24,1)
        _PoleBlendStart ("Pole Blend Start", Range(0,1)) = .55
        _PoleBlendEnd ("Pole Blend End", Range(0,1)) = .88

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

            sampler2D _MainTex;
            half4 _Tint;
            half _Exposure;
            float _Rotation;
            half _DramaGain;
            half4 _ColorBalance;
            half _Saturation;
            half _Contrast;
            half _HorizonLift;
            half4 _ZenithColor;
            half4 _NadirColor;
            half _PoleBlendStart;
            half _PoleBlendEnd;

            struct appdata_t
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float compass = atan2(d.x,d.z) + radians(_Rotation);
                float2 uv;
                uv.x = frac(compass / (2.0 * UNITY_PI) + 0.5);
                uv.y = saturate(asin(clamp(d.y,-1.0,1.0)) / UNITY_PI + 0.5);
                // frac(u) has a derivative discontinuity at the 0/360 seam.
                // Explicit LOD prevents that single radial line from selecting
                // an unrelated coarse mip level.
                half3 c = tex2Dlod(_MainTex,float4(uv,0,0.5)).rgb;
                c *= _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                c *= _ColorBalance.rgb * _DramaGain;
                half lum = dot(c,half3(0.2126,0.7152,0.0722));
                c = lerp(lum.xxx,c,_Saturation);
                c = (c - 0.5h) * _Contrast + 0.5h;
                half horizon = 1.0h - saturate(abs(d.y) * 6.0h);
                c += _HorizonLift * horizon * half3(1.0h,0.78h,0.88h);
                // Equirectangular panoramas collapse every longitude into one pixel at
                // each pole. Fade cloud detail into a stable cap before that collapse
                // becomes visible as a radial starburst.
                half pole = smoothstep(_PoleBlendStart,_PoleBlendEnd,abs(d.y));
                half3 poleColor = d.y >= 0.0 ? _ZenithColor.rgb : _NadirColor.rgb;
                c = lerp(c,poleColor,pole);
                return half4(max(c,0),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
