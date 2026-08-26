# Donheon (동헌) — Unity 임포트 가이드 v1

풍화당(堂無使) 기반 관아 동헌 모델. 좌·우 회벽에 세살 분합문 3조씩 이식본.

## 1. 모델 요약
- **총 tris: 104,703**
- 메시 오브젝트: **19개**
- 실사용 머티리얼: **13종**
- 건물 치수(지붕 기준): **약 18.6 m(폭) × 9.8 m(깊이) × 7.2 m(높이)**
  - ※ FBX 전체 bbox는 44×44 m — 본체 메시에 넓은 지반/기단 평면이 포함되어 있음
- 스케일: 트랜스폼 적용 완료 → 모든 오브젝트 **Scale 1.0**

### 오브젝트 구성
- `Donheon_Body_Mesh` (본체: 골조·지붕·마루·축대·기단)
- `Donheon_Paper` (전면 창호지 컷아웃), `Donheon_Sign` (현판)
- `Door_L1~L3` / `Door_R1~R3` : 좌·우 회벽 세살 분합문 (각 2짝, 총 6 오브젝트)
- `Munseon_L1~L4` / `Munseon_R1~R4` : 문선(기둥 사이 세로 틀), 총 8개
- `Blackout_L` / `Blackout_R` : 문 뒤 차폐판(검정, 실외 투과 방지)

## 2. 유니티 임포트 설정 (Model 탭)
- **Scale Factor = 1**
- **Convert Units = 체크(ON)**
- **Bake Axis Conversion = 해제(OFF)**  ← 블렌더에서 축/트랜스폼을 이미 구웠음
- Read/Write Enabled = 불필요(OFF)
- Weld Vertices 기본, Legacy Blend Shape Normals 불필요

## 3. ★반드시 할 것 2가지
1. **노멀맵 텍스처들을 Texture Type = `Normal Map` 으로 변경** (아래 6개)
   - `MI_R_Roof_Normal.png`, `MI_R_BrickConcrete_Normal.png`, `MI_R_Buyeon_2_Normal.png`,
     `MI_KoreanWood_1_Normal.png`, `MI_KoreanPaper_1_Normal.png`, `T_Wall01b_N.png`
   - (변경 안 하면 sRGB로 읽혀 라이팅이 틀어짐)
2. **유니티가 FBX 임포트 시 자동 생성하는 `.fbm` 폴더 삭제** (중복 텍스처 방지)
   - 텍스처는 `Textures/` 폴더의 것을 사용

## 4. 머티리얼 매핑표 (셰이더: 전부 **URP/Lit**)
| Material | BaseMap | BumpMap(Normal) | Surface Type |
|---|---|---|---|
| Donheon_Body | Donheon_Atlas_Albedo_final.png | — | Opaque |
| M_Blackout | — (텍스처 없음, 순검정) | — | Opaque |
| M_Wood_Tile | T_Wood_Tile_512.png | — | Opaque |
| M_Stone_Rubble (축대) | T_Wall01b_BC_light.png | T_Wall01b_N.png | Opaque |
| M_Stone_Granite (갑석/계단) | T_Stone_Granite_512.png | — | Opaque |
| MI_R_Roof1 (기와) | MI_R_Roof_BaseColor.png | MI_R_Roof_Normal.png | Opaque |
| MI_R_BrickConcrete1 (적심) | MI_R_BrickConcrete_BaseColor.png | MI_R_BrickConcrete_Normal.png | Opaque |
| MI_R_Buyeon_2.001 (부연) | MI_R_Buyeon_2_BaseColor.png | MI_R_Buyeon_2_Normal.png | Opaque |
| MI_KoreanWood_1.001 (문짝 울거미/살) | MI_KoreanWood_1_BaseColor.png | MI_KoreanWood_1_Normal.png | Opaque |
| MI_KoreanPaper_1.004 (문짝 창호지) | MI_KoreanPaper_1_BaseColor.png | MI_KoreanPaper_1_Normal.png | Opaque |
| MI_Metal (문살/장석) | — (base color 지정, 텍스처 없음) | — | Opaque |
| MI_R_Sign_PHD (현판) | T_Sign_SaMuDang_1024x306.png | — | Opaque |
| **Changho_Cutout** (전면 창호 빌보드) | changho_cutout.png | — | **Opaque + Alpha Clipping ON** |

- `Changho_Cutout` : 알파 컷아웃 — Surface Type=Opaque, **Alpha Clipping 체크**, Clip Threshold ~0.5
- `M_Blackout` : 텍스처 없이 Base Color 검정. 문 뒤 실외 투과 차폐용

## 5. 현재 상태 / 알려진 제한
- 문짝은 **정적(static)** — 여닫이 미구현, **콜라이더 없음**
- 문짝 6개는 **개별 오브젝트**(프리팹 인스턴싱 권장)
- 축대 텍스처 `T_Wall01b_BC_light.png` 는 낙안읍성 팩 **원본이 아니라 밝기 보정 사본**
  (원본 `T_Wall01b_BC.png` 은 미수정 — 낙안 팩 35개 벽 공유 보호)
- **절단면 rim**: 회벽 개구부 절단 테두리는 문을 회전(여닫이)시키면 드러남. 현재 문 고정 상태에선 문/문선/Blackout이 가림
- **폴리 예산**: 씬 총 104,703 tris — Quest 3 단일 오브젝트 예산을 상회. 최적화(지반 평면 분리·지붕 데시메이트) 여지 있음
- 노멀맵 2048² 총 **56.3 MB** — Quest 3 VRAM 고려해 512~1024로 다운스케일 검토 권장

## 6. 소스 관련 주의
- ★소스 `.blend`(Donheon_Quest3_doors.blend)에는 문짝 메시에 **UV1(`UVMap.001`)이 남아 있음**.
  이 FBX는 익스포트 직전 인메모리로 UV1 제거·트랜스폼 적용 후 뽑은 것 →
  **재익스포트 시 UV1 제거와 트랜스폼 적용을 다시 해야 함**
- `.blend` 는 이 패키지에 포함하지 않음

## 파일 구성
```
Donheon_v1/
├─ Donheon_Quest3_v1.fbx
├─ README.md
└─ Textures/  (17개 PNG, 약 95 MB)
```
