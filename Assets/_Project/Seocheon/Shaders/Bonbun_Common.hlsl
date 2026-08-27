#ifndef BONBUN_COMMON_INCLUDED
#define BONBUN_COMMON_INCLUDED

// ============================================================================
//  봉분 셰이더 공용 HLSL  (Shader Graph Custom Function 노드에서 호출)
//
//  정점컬러 인코딩 (블렌더 실측으로 확정, EXPORT_NOTES 참조)
//   본체 Bonbun_Sculpt : R = dig_depth   (원시 0~1 저작 필드, 미터 아님. vcol R = 원시값 1:1)
//                        G = 0.5 + 0.5*(patch_sat - patch_grass)   >0.5 흙강제 / <0.5 잔디강제
//                        B = bnd_dist / 0.30m                      (0.08m -> 0.26667)
//   덩어리 Dirt/Grass  : R = contactAO (원시, 0=완전폐색)          G=B=A=1
//
//  좌표계: 블렌더 Z-up -> 유니티 Y-up. 블렌더 셰이더의 "법선 Z" 는 여기서 월드 법선 Y.
// ============================================================================


// ---------------------------------------------------------------- 3D 노이즈
// 블렌더 Noise Texture(Perlin, 월드 입력) 대응. Scale S = 미터당 사이클 수이므로
// 특징 크기 ~= 1/S m.  Detail = fBm 옥타브 수.
float3 _BB_hash33(float3 p)
{
    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
               dot(p, float3(269.5, 183.3, 246.1)),
               dot(p, float3(113.5, 271.9, 124.6)));
    return frac(sin(p) * 43758.5453123) * 2.0 - 1.0;
}

// 그래디언트(Perlin) 노이즈, 대략 -0.7..0.7
float _BB_perlin(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);

    float n000 = dot(_BB_hash33(i + float3(0, 0, 0)), f - float3(0, 0, 0));
    float n100 = dot(_BB_hash33(i + float3(1, 0, 0)), f - float3(1, 0, 0));
    float n010 = dot(_BB_hash33(i + float3(0, 1, 0)), f - float3(0, 1, 0));
    float n110 = dot(_BB_hash33(i + float3(1, 1, 0)), f - float3(1, 1, 0));
    float n001 = dot(_BB_hash33(i + float3(0, 0, 1)), f - float3(0, 0, 1));
    float n101 = dot(_BB_hash33(i + float3(1, 0, 1)), f - float3(1, 0, 1));
    float n011 = dot(_BB_hash33(i + float3(0, 1, 1)), f - float3(0, 1, 1));
    float n111 = dot(_BB_hash33(i + float3(1, 1, 1)), f - float3(1, 1, 1));

    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);
    float nxy0 = lerp(nx00, nx10, u.y);
    float nxy1 = lerp(nx01, nx11, u.y);
    return lerp(nxy0, nxy1, u.z);
}

// fBm. octaves = 블렌더 Detail. 출력은 대략 -0.5..0.5 (0 중심, 부호 있음)
float _BB_fbm(float3 p, int octaves)
{
    float a = 0.5, s = 0.0, norm = 0.0;
    [unroll(4)]
    for (int o = 0; o < 4; o++)
    {
        if (o >= octaves) break;
        s += _BB_perlin(p) * a;
        norm += a;
        p *= 2.0;
        a *= 0.5;
    }
    return (norm > 0.0) ? s / norm * 0.7 : 0.0;
}


// ------------------------------------------------------------ 트라이플래너
// tileMeters : 타일 하나가 덮는 미터 수.  rotDeg : 면내 회전(보조 레이어용)
float3 _BB_triplanarWeights(float3 n, float sharpness)
{
    float3 w = pow(abs(n), sharpness);
    return w / max(dot(w, 1.0.xxx), 1e-5);
}

float2 _BB_rot2(float2 uv, float rotDeg)
{
    float s, c;
    sincos(radians(rotDeg), s, c);
    return float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
}

float4 BB_TriplanarColor(UnityTexture2D tex, float3 wpos, float3 wnrm, float tileMeters, float rotDeg, float sharpness)
{
    float inv = 1.0 / max(tileMeters, 1e-4);
    float3 w = _BB_triplanarWeights(wnrm, sharpness);
    float2 uvX = _BB_rot2(wpos.zy * inv, rotDeg);
    float2 uvY = _BB_rot2(wpos.xz * inv, rotDeg);
    float2 uvZ = _BB_rot2(wpos.xy * inv, rotDeg);
    float4 cx = SAMPLE_TEXTURE2D(tex.tex, tex.samplerstate, uvX);
    float4 cy = SAMPLE_TEXTURE2D(tex.tex, tex.samplerstate, uvY);
    float4 cz = SAMPLE_TEXTURE2D(tex.tex, tex.samplerstate, uvZ);
    return cx * w.x + cy * w.y + cz * w.z;
}

// 탄젠트공간 노멀맵 3장을 월드공간으로 (whiteout 블렌드)
float3 BB_TriplanarNormal(UnityTexture2D tex, float3 wpos, float3 wnrm, float tileMeters, float rotDeg, float sharpness, float strength)
{
    float inv = 1.0 / max(tileMeters, 1e-4);
    float3 w = _BB_triplanarWeights(wnrm, sharpness);
    float3 sgn = sign(wnrm);

    float3 nx = UnpackNormal(SAMPLE_TEXTURE2D(tex.tex, tex.samplerstate, _BB_rot2(wpos.zy * inv, rotDeg)));
    float3 ny = UnpackNormal(SAMPLE_TEXTURE2D(tex.tex, tex.samplerstate, _BB_rot2(wpos.xz * inv, rotDeg)));
    float3 nz = UnpackNormal(SAMPLE_TEXTURE2D(tex.tex, tex.samplerstate, _BB_rot2(wpos.xy * inv, rotDeg)));

    nx.xy *= strength; ny.xy *= strength; nz.xy *= strength;

    // whiteout: 접선 성분을 지오메트리 법선에 더한다
    float3 wx = float3(nx.z * sgn.x, nx.y, nx.x);
    float3 wy = float3(ny.x, ny.z * sgn.y, ny.y);
    float3 wz = float3(nz.x, nz.y, nz.z * sgn.z);
    return normalize(wx * w.x + wy * w.y + wz * w.z + wnrm);
}


// --------------------------------------------------------------- 소프트라이트
float3 _BB_softLight(float3 b, float3 s)
{
    // Photoshop/Blender 소프트라이트
    float3 lo = 2.0 * b * s + b * b * (1.0 - 2.0 * s);
    float3 hi = 2.0 * b * (1.0 - s) + sqrt(saturate(b)) * (2.0 * s - 1.0);
    return lerp(lo, hi, step(0.5, s));
}

float3 _BB_desat(float3 c, float amt)
{
    float l = dot(c, float3(0.2126, 0.7152, 0.0722));
    return lerp(c, l.xxx, amt);
}

// 주 레이어 + 보조 레이어(회전/소프트라이트/탈채도) 합성
float3 BB_LayeredColor(UnityTexture2D texMain, float3 wpos, float3 wnrm,
                       float tileMain, float tileDetail, float rotDetail,
                       float softAmount, float desatAmount, float sharpness)
{
    float3 a = BB_TriplanarColor(texMain, wpos, wnrm, tileMain, 0.0, sharpness).rgb;
    float3 b = BB_TriplanarColor(texMain, wpos, wnrm, tileDetail, rotDetail, sharpness).rgb;
    b = _BB_desat(b, desatAmount);
    return lerp(a, saturate(_BB_softLight(a, b)), softAmount);
}


// ============================================================================
//  [2] SG_Bonbun_Body  — 본체 (정점컬러 블렌드)
// ============================================================================
void BB_Body_float(
    float3 WorldPos, float3 WorldNormal, float4 VertexColor,
    UnityTexture2D GrassBC, UnityTexture2D GrassN, UnityTexture2D GrassR,
    UnityTexture2D DirtBC,  UnityTexture2D DirtN,
    float DigThreshold, float DigWidth,
    float NoiseAmp1, float NoiseAmp2, float NoiseAmp3,
    float MasterMin, float MasterMax,
    float GrassTileMain, float GrassTileDetail, float GrassRotDetail, float GrassSoft, float GrassDesat,
    float DirtTileMain,  float DirtTileDetail,  float DirtRotDetail,  float DirtSoft,
    float DigAOMax, float DirtRough, float GrassBrightness, float DirtBrightness,
    float TriSharpness,
    out float3 BaseColor, out float3 NormalWS, out float Smoothness, out float Occlusion)
{
    float3 nrm = normalize(WorldNormal);

    float dig  = VertexColor.r;              // 원시 dig_depth (1:1)
    float gsat = VertexColor.g;              // 0.5 + 0.5*(sat-grass)
    float bnd  = VertexColor.b * 0.30;       // 미터로 환원

    // ---- 마스터 진폭 변조 (주기 약 1.2m = 블렌더 Scale 0.83, Detail 1) ----
    float master = _BB_fbm(WorldPos * 0.83, 1) + 0.5;          // 0~1 근방
    master = lerp(MasterMin, MasterMax, saturate(master));     // 0.05~1.7
    // 하한 구간 = 삽날로 잘린 절단선, 상한 구간 = 뜯긴 경계

    // ---- 3스케일 노이즈 (블렌더 Scale 3 / 12 / 40) ----
    float n1 = _BB_fbm(WorldPos * 3.0,  2);
    float n2 = _BB_fbm(WorldPos * 12.0, 2);
    float n3 = _BB_fbm(WorldPos * 40.0, 2);
    float noise = n1 * NoiseAmp1 + n2 * NoiseAmp2 + n3 * NoiseAmp3;

    // ---- 노이즈 밴드 게이팅: dig 0.005~0.10 구간에만, 피크 0.035 ----
    // 게이팅이 없으면 굴착 내부에 잔디 구멍이 뚫린다.
    float gateUp   = smoothstep(0.005, 0.035, dig);
    float gateDown = 1.0 - smoothstep(0.035, 0.100, dig);
    float gate     = gateUp * gateDown;

    float digEff = dig + noise * master * gate;

    // ---- 기본 마스크: 임계 DigThreshold, 전이 폭 DigWidth (중앙 정렬) ----
    float half_ = max(DigWidth, 1e-5) * 0.5;
    float mask = smoothstep(DigThreshold - half_, DigThreshold + half_, digEff);   // 1 = 흙

    // ---- 정점컬러 G 강제 ----
    float dirtForce  = saturate((gsat - 0.5) * 2.0);
    float grassForce = saturate((0.5 - gsat) * 2.0);
    mask = saturate(mask * (1.0 - grassForce) + dirtForce);

    // ---- 립 측벽: bnd_dist < 0.08 AND 월드법선 Y < 0.55 -> 흙 강제 ----
    float lipB = 1.0 - smoothstep(0.070, 0.080, bnd);
    float lipN = 1.0 - smoothstep(0.500, 0.550, nrm.y);
    mask = max(mask, lipB * lipN);

    // ---- 알베도 / 노멀 / 러프니스를 모두 같은 마스크로 ----
    float3 grassC = BB_LayeredColor(GrassBC, WorldPos, nrm, GrassTileMain, GrassTileDetail, GrassRotDetail, GrassSoft, GrassDesat, TriSharpness) * GrassBrightness;
    float3 dirtC  = BB_LayeredColor(DirtBC,  WorldPos, nrm, DirtTileMain,  DirtTileDetail,  DirtRotDetail,  DirtSoft,  0.0,        TriSharpness) * DirtBrightness;

    float3 grassNrm = BB_TriplanarNormal(GrassN, WorldPos, nrm, GrassTileMain, 0.0, TriSharpness, 1.0);
    float3 dirtNrm  = BB_TriplanarNormal(DirtN,  WorldPos, nrm, DirtTileMain,  0.0, TriSharpness, 1.0);

    float grassRough = BB_TriplanarColor(GrassR, WorldPos, nrm, GrassTileMain, 0.0, TriSharpness).r;

    BaseColor  = lerp(grassC, dirtC, mask);
    NormalWS   = normalize(lerp(grassNrm, dirtNrm, mask));
    Smoothness = 1.0 - lerp(grassRough, DirtRough, mask);

    // ---- dig-AO: dig 0.035~0.40 에 완만하게, 최대 DigAOMax ----
    float aoT = saturate((dig - 0.035) / (0.400 - 0.035));
    Occlusion = 1.0 - aoT * DigAOMax;
}


// ============================================================================
//  [3] SG_Bonbun_Dirt / SG_Bonbun_SodGrass — 덩어리 (contactAO)
//      R = contactAO 원시값. 하한 리맵 후 곱한다.
// ============================================================================
void BB_Debris_float(
    float3 WorldPos, float3 WorldNormal, float4 VertexColor,
    UnityTexture2D BC, UnityTexture2D NRM,
    float TileMain, float TileDetail, float RotDetail, float SoftAmount, float DesatAmount,
    float Roughness, float Brightness, float AOFloor, float TriSharpness, float NormalStr,
    out float3 BaseColor, out float3 NormalWS, out float Smoothness, out float Occlusion)
{
    float3 nrm = normalize(WorldNormal);

    float3 c = BB_LayeredColor(BC, WorldPos, nrm, TileMain, TileDetail, RotDetail, SoftAmount, DesatAmount, TriSharpness) * Brightness;

    BaseColor  = c;
    NormalWS   = BB_TriplanarNormal(NRM, WorldPos, nrm, TileMain, 0.0, TriSharpness, NormalStr);
    Smoothness = 1.0 - Roughness;

    // contactAO 하한 리맵: AOFloor + (1-AOFloor)*AO   (기본 0.35 + 0.65*AO)
    Occlusion = AOFloor + (1.0 - AOFloor) * saturate(VertexColor.r);
}

// 잔디 캡용 — 러프니스 텍스처를 쓰는 변형
void BB_DebrisR_float(
    float3 WorldPos, float3 WorldNormal, float4 VertexColor,
    UnityTexture2D BC, UnityTexture2D NRM, UnityTexture2D ROUGH,
    float TileMain, float TileDetail, float RotDetail, float SoftAmount, float DesatAmount,
    float Brightness, float AOFloor, float TriSharpness, float NormalStr,
    out float3 BaseColor, out float3 NormalWS, out float Smoothness, out float Occlusion)
{
    float3 nrm = normalize(WorldNormal);
    BaseColor  = BB_LayeredColor(BC, WorldPos, nrm, TileMain, TileDetail, RotDetail, SoftAmount, DesatAmount, TriSharpness) * Brightness;
    NormalWS   = BB_TriplanarNormal(NRM, WorldPos, nrm, TileMain, 0.0, TriSharpness, NormalStr);
    Smoothness = 1.0 - BB_TriplanarColor(ROUGH, WorldPos, nrm, TileMain, 0.0, TriSharpness).r;
    Occlusion  = AOFloor + (1.0 - AOFloor) * saturate(VertexColor.r);
}

#endif // BONBUN_COMMON_INCLUDED
