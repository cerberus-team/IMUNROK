using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 씬(Gyeonu_Gwana) 공용 좌표. 지형·구조물·콜라이더·마커가 전부 이 값을 참조한다.
    ///
    /// 축: 진입축 = +Z. 플레이어는 남(-Z)에서 북(+Z)으로 걸어 올라온다.
    ///   스폰(-48) → 언덕길 → 외삼문(-34) → 박석마당 → 월대 계단(3.0~6.0) → 동헌(+14.5)
    /// 마당 바닥 y=0. 대지 밖은 사방으로 떨어지고 북쪽만 뒷산으로 솟는다.
    /// </summary>
    public static class GwanaLayout
    {
        // ── 지형 (2026-08-16 개편: 평지 + 남쪽 언덕길만) ──
        public const float Half = 100f;              // 지형 반폭 (200×200, 스커트 포함 600×600)
        public const float TerrainBaseY = -30f;      // Terrain GO의 y
        public const float TerrainHeight = 60f;
        public const float CourtY = 0f;              // 마당 바닥

        // 관아 대지는 완전 평지(y=0). 경사는 남쪽 진입 언덕길 하나뿐이다.
        public const float BrowZ = -58f;             // 언덕 마루 — 여기부터 북쪽 전 방향 평지
        public const float RoadFootZ = -69f;         // 언덕 아래 = 스폰 (낙차 3.7, 최대 24°)
        public const float RoadDrop = 3.7f;
        public const float VillageFallZ = -84f;      // 마을 쪽으로 계속 내려가는 사면 끝
        public const float VillageFallDrop = 8.5f;
        // 보행 범위 = 담장에서 바깥 15m + 언덕길 전체. 담장을 한 바퀴 돌 수 있을 만큼은 열되,
        // 그 밖은 나무로 막아 "숲에 둘러싸인 관아"로 읽히게 한다.
        public const float PlayHalfX = 32f;          // 담장 x±17 → 바깥 15m
        public const float PlayZN = 42f;             // 북담 z27 → 바깥 15m
        public const float PlayZS = -78f;            // 언덕 아래 (스폰 −69보다 9m 더)

        // ── 담장 ──
        public const float WallHalfX = 17f;
        public const float WallZS = -34f;            // 남담(외삼문 선)
        public const float WallZN = 27f;             // 북담
        public const float WallTop = 3.08f;          // 용마루 상단

        // ── 외삼문 ──
        public const float GateZ = WallZS;
        public const float GateHalfW = 7.4f;         // 기단 반폭 — 담장이 여기서 만난다
        public const float GateBaseTop = 0.45f;      // 문지방(기단) 상면
        public const float GateRidge = 6.78f;        // 중앙 용마루 상단

        // ── 월대 (동헌 기단) ──
        public const float DaeHalfX = 12.4f;
        public const float DaeZ0 = 6.0f, DaeZ1 = 22.0f;
        // 4단 석축 1.68 + 갑석 0.12. 동헌 자체 축대 2.2를 얹으면 마당→대청 마루 4.0m —
        // "높은 기단 위 동헌" 고증(3~4층 석축)을 만족하고, 마당에서 올려다보는 각이 선다
        public const float DaeTop = 1.80f;
        public const float DaeTiers = 4f, DaeTierH = 0.42f;
        public const float DaeStairZ0 = 2.4f;        // 계단 아랫단 (마당 y=0) — 물매 26.6°
        public const float DaeStairHalfX = 2.8f;

        // ── 동헌 ──
        public const float DonheonZ = 14.5f;
        /// <summary>모델 지반면(로컬 -0.70)이 월대 상면에 닿도록 하는 루트 y.</summary>
        public const float DonheonY = DaeTop + 0.70f;   // 1.90
        public const float DonheonMaru = DonheonY + 1.50f;   // 대청 마루 3.40
        public const float DonheonGidan = DonheonY + 0.95f;  // 기단 갑석 2.85

        // ── 어도 (박석 진입축) ──
        public const float AxisHalfW = 2.2f;
        public const float AxisZ0 = -31.4f, AxisZ1 = DaeStairZ0;

        // ── 마커 ──
        // 스폰을 언덕 아래로 더 내렸다(2026-08-17): −69 → −74. 마루(−58)까지 16m,
        // 보행 속도 3m/s 기준 약 5.3초 — 그 사이에 진입 안개가 걷힐 시간이 생긴다.
        // 지형은 손대지 않았다. −74 지점은 이미 마을 쪽 사면(지면 약 −5.9)이라 그대로 쓴다.
        public const float SpawnZ = -74f;
        public const float ExitZ = -77f;             // 마을로 되내려가는 사면 (경계 −78 바로 앞)

        /// <summary>GLSL smoothstep — Mathf.SmoothStep의 인자 순서 함정 회피용.</summary>
        public static float SStep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(e0, e1, x));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 씬 지면 높이 (지형 생성·마커·원경 산 배치 공용).
        ///
        /// 관아는 정돈된 공간이라 지형에 기복을 두지 않는다 — 언덕 마루(z −58) 북쪽은
        /// 사방 어디든 정확히 y=0 평지다. 유일한 경사는 마을에서 올라오는 남쪽 언덕길:
        ///   z −69(스폰, −3.70) → −58(마루, 0). t^1.35 물매 — 마루에서 평평해지고
        ///   아래로 갈수록 가팔라진다(최대 24°). 마루가 볼록해 스폰에서는 관문 지붕만 보인다.
        /// 스폰 아래(z −69 → −84)는 마을 쪽으로 8.5m 더 떨어진다 — 뒤돌아보면 내려온
        /// 언덕이 보인다. 마을 씬 관아 계단(z 79.8 y 0 → 89.6 y 7.6, 지형 끝 11.7까지 상승)의
        /// 연속이 되도록 "아직 오르는 중"에서 시작하는 구성.
        /// </summary>
        public static float GroundHeight(float wx, float wz)
        {
            float h;
            if (wz >= BrowZ) h = 0f;                          // 관아 대지
            else
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(BrowZ, RoadFootZ, wz));
                h = -RoadDrop * Mathf.Pow(t, 1.35f);          // 언덕길 (마루에서 볼록)
                h -= SStep(RoadFootZ, VillageFallZ, wz) * VillageFallDrop;   // 마을 쪽 사면
            }
            return h + Micro(wx, wz);
        }

        /// <summary>
        /// 미세 기복 (2026-08-17). 완전 평면이면 큐브를 늘어놓은 것처럼 읽혀 이것만으로도
        /// 인공적인 인상이 크게 줄어든다. 진폭 ±0.4m / 파장 12~28m — 걸을 때 체감되지 않는 수준.
        ///
        /// 마당(담장 안)과 외삼문 기단·계단 자리는 그대로 0 을 유지한다. 정돈된 공간이라는 설정도
        /// 있지만, 실용적으로도 기단·어도·월대가 고정 y로 놓여 있어 지면이 출렁이면 밑동이 뜬다.
        /// 담장 바깥으로 6~7m에 걸쳐 서서히 기복이 살아나므로 경계에 능선이 생기지 않는다.
        /// </summary>
        public static float Micro(float wx, float wz)
        {
            float ax = Mathf.Abs(wx);
            // 평탄 유지 구역: 담장 안 + 외삼문 앞뒤 진입부
            float fx = 1f - SStep(WallHalfX + 1f, WallHalfX + 7f, ax);
            float fz = SStep(WallZS - 12f, WallZS - 5f, wz)
                     * (1f - SStep(WallZN + 1f, WallZN + 7f, wz));
            float flat = fx * fz;
            if (flat > 0.999f) return 0f;

            float n = (Mathf.PerlinNoise(wx * 0.035f + 21.7f, wz * 0.035f + 13.1f) - 0.5f) * 2f * 0.30f
                    + (Mathf.PerlinNoise(wx * 0.085f + 5.3f, wz * 0.085f + 9.9f) - 0.5f) * 2f * 0.11f;
            return n * (1f - flat);
        }
    }
}
