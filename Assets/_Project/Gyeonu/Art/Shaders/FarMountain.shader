// 은하담 원경 산 전용 무조명 셰이더 (2026-08-07).
//
// 왜 커스텀인가: 낮 안개(선형 60→220)가 220m 밖을 완전히 지워 URP Lit 산이 안 보인다.
// 하늘 프리셋(안개)은 수정 금지라서, 산 쪽에서 안개를 "부분 저항"한다.
//   - _FogResist: 완전 안개 거리에서도 이만큼은 자기 색을 유지 (0.4 = 40%)
//   - 밤 대응: 자기 색에 saturate(안개색×2.5)를 곱한다 — 낮 안개색(밝음)은 1로 포화되어
//     무영향, 밤 안개색(어두운 남색)은 산을 자동으로 어둡게·푸르게 만든다.
// LightMode 태그가 없는 패스는 URP가 SRPDefaultUnlit으로 그려준다.
// unity_FogColor / unity_FogParams는 URP도 채워 주는 내장 상수.
Shader "IMUNROK/Gyeonu/FarMountain"
{
    Properties
    {
        _BaseMap ("Gradient (명암)", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _FogResist ("Fog Resist", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            fixed4 _BaseColor;
            float _FogResist;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float depth : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _BaseMap);
                o.depth = -UnityObjectToViewPos(v.vertex).z;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 own = tex2D(_BaseMap, i.uv).rgb * _BaseColor.rgb;
                own *= saturate(unity_FogColor.rgb * 2.5 + 0.08);   // 밤 자동 감광
                // 선형 안개 가시율 (1=선명, 0=완전 안개) — 바닥을 _FogResist로 받친다
                float vis = saturate(i.depth * unity_FogParams.z + unity_FogParams.w);
                vis = max(vis, _FogResist);
                return fixed4(lerp(unity_FogColor.rgb, own, vis), 1);
            }
            ENDCG
        }
    }
}
