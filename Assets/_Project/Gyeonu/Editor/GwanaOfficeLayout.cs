using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 집무실(동헌 온돌방, Gyeonu_GwanaOffice) 공용 좌표.
    ///
    /// ⚠️ **이 씬은 방 하나의 실내만 짓는다** (사용자 지시 2026-08-16).
    ///    동헌 외형·지붕·마당·지형은 관아 외부 씬(Gyeonu_Gwana) 소관이고 여기서는 만들지 않는다.
    ///    그래서 외부 씬 월드 좌표를 따라가지 않고 **방을 원점에 놓는다** — 관측실·서고와 같은 방식.
    ///    씬 전환이 플레이어를 SpawnPoint_FromGwana에 세우므로 두 씬의 좌표가 맞을 필요가 없다.
    ///
    /// 【방위】 들어온 문이 남(−Z), 병풍·비밀문이 있는 뒷벽이 북(+Z).
    ///    플레이어는 +Z를 보며 들어서므로 "왼쪽 벽" = 서(−X), "오른쪽 벽" = 동(+X)이다.
    ///
    /// 【천장】 종도리가 X축과 나란한 맞배 연등천장 — 서까래는 Z방향으로 흐르고
    ///    박공(삼각벽)은 동·서 벽에 생긴다. 도리 3줄의 윗면이 정확히 한 평면에 오도록
    ///    높이를 잡았다(2.96 / 3.435 / 3.91, z는 2.26 / 1.13 / 0) — 그래야 서까래가
    ///    세 도리에 동시에 닿는다. 물매 22.8°.
    /// </summary>
    public static class GwanaOfficeLayout
    {
        // ── 방 (실내 치수) ───────────────────────────────────
        public const float FloorY = 0f;
        public const float HalfX = 2.90f;      // 실내 반폭 → 5.80m
        public const float ZS = -2.20f;        // 남벽(들어온 문) 안쪽면
        public const float ZN = 2.20f;         // 북벽(뒷벽) 안쪽면 → 깊이 4.40m
        public const float WallT = 0.24f;      // 벽 두께

        public static float OutX => HalfX + WallT;   // 3.14
        public static float OutZS => ZS - WallT;     // -2.44
        public static float OutZN => ZN + WallT;     // 2.44

        // ── 심벽 켜 (하방 → 회벽 → 중방 → 회벽 → 창방) ──────
        public const float HabangY = 0.17f;     // 하방 상단
        public const float JungbangY0 = 1.13f, JungbangY1 = 1.30f;   // 중방
        public const float ChangbangY0 = 2.18f, ChangbangY1 = 2.35f; // 창방 = 기둥 머리
        public const float Proud = 0.05f;       // 목부재가 회벽면보다 방 안쪽으로 나온 깊이
        public const float SimbyeokColW = 0.20f;

        // ── 기둥 (평주 8본 — 네 귀퉁이 + 대들보를 받는 자리) ──
        public const float ColX = 1.52f;        // 대들보·중간 기둥의 x (±)
        public static readonly float[] ColXs = { -HalfX, -ColX, ColX, HalfX };
        public const float ColR0 = 0.155f, ColR1 = 0.132f;   // 민흘림 (아래→위)

        // ── 지붕틀 ───────────────────────────────────────────
        public const float BeamY0 = 2.35f, BeamY1 = 2.73f, BeamW = 0.32f;   // 대들보 (x=±ColX, z방향)
        public const float PurlinR = 0.115f, MidPurlinR = 0.115f;
        public const float EaveZ = 2.26f, EaveY = 2.845f;      // 주심도리 (윗면 2.96)
        public const float MidZ = 1.13f, MidY = 3.320f;        // 중도리   (윗면 3.435)
        public const float RidgeY = 3.795f;                    // 종도리   (윗면 3.91)
        public const float RafterT = 0.095f;                   // 서까래 단면
        public const float PanT = 0.05f;                       // 개판 두께

        /// <summary>서까래 윗면 = 개판 아랫면. 세 도리 윗면을 지나는 직선.</summary>
        public static float RafterTopY(float z) => 3.91f - 0.42035f * Mathf.Abs(z);
        /// <summary>서까래 아랫면 (경사 보정 — 박공 삼각벽이 여기에 맞물린다).</summary>
        public static float RafterBotY(float z) => RafterTopY(z) - RafterT / 0.9218f;

        // ── 창 (격자창 2, 좌우 벽) ───────────────────────────
        public const float WinY0 = 0.55f, WinY1 = 2.05f;
        public const float WinWestZ0 = -0.85f, WinWestZ1 = 0.85f;
        public const float WinEastZ0 = -0.75f, WinEastZ1 = 0.95f;   // 일부러 어긋나게 (완전 미러 회피)

        // ── 들어온 문 (남벽 분합문 2짝) ──────────────────────
        public const float DoorHalfX = 0.80f, DoorTopY = 1.95f;

        // ── 비밀문 (북벽, 병풍 뒤) ───────────────────────────
        public const float SecretHalfX = 0.55f, SecretTopY = 1.85f;

        // ── 비밀 통로 (북벽 너머 — 계단 → 짧은 통로 → 서고로) ─
        // ⚠️ 치수·재질을 **서고 씬의 「관아 비밀 통로」와 똑같이** 잡았다 (ArchiveBuilder).
        //    거기서 이어지는 길이라 폭·층고·계단 물매가 다르면 씬 전환 때 딴 통로가 된다.
        //    관측실 암문(거친 막돌 동굴)과 달리 이쪽은 **다듬은 석벽 + 목재 리브·천장널** —
        //    수령이 손본 정돈된 길이다.
        public const float PassW = 1.70f;      // 통로 안폭   (= ArchiveBuilder.W)
        public const float PassH = 2.30f;      // 통로 층고   (= ArchiveBuilder.H)
        public const float PassT = 0.30f;      // 벽·바닥 두께 (= ArchiveBuilder.T)
        public const float StepRise = 0.165f;  // (= ArchiveBuilder.StepRise)
        public const float StepRun = 0.32f;    // (= ArchiveBuilder.StepRun)
        public const float RibSpacing = 2.30f; // 목재 리브 간격
        public const float LampSpacing = 3.00f;// 등롱 간격 (사거리를 2.5로 줄인 만큼 촘촘히)

        /// <summary>통로가 시작되는 자리 — 비밀문 바깥면.</summary>
        public static Vector3 PassStart => new Vector3(0f, FloorY, OutZN);

        // 마커는 빌더가 통로를 다 놓은 뒤 실제 끝점으로 덮어쓴다 (경로를 바꿔도 따라온다)
        public static Vector3 ExitToArchiveFallback => new Vector3(0f, -2.64f, 12f);

        // ── 병풍 ─────────────────────────────────────────────
        /// <summary>펼친 병풍의 앞면이 서는 z (뒷벽에서 살짝 떼어 놓는다).</summary>
        public const float ScreenZ = 2.02f;
        /// <summary>접어서 치웠을 때 몰아 두는 x (서쪽 끝).</summary>
        public const float ScreenFoldedX = -2.35f;

        // ── 마커 ─────────────────────────────────────────────
        /// <summary>관아 외부에서 들어선 자리 — 남쪽 문 안쪽, 방을 마주 본다.</summary>
        public static Vector3 SpawnFromGwana => new Vector3(0f, FloorY, ZS + 0.70f);
        /// <summary>관아 외부로 나가는 지점 — 남쪽 문간.</summary>
        public static Vector3 ExitToGwana => new Vector3(0f, FloorY, ZS + 0.15f);
        /// <summary>수령 자리 — 서안 뒤, 병풍을 등지고 문을 마주 본다.</summary>
        public static Vector3 SpawnSuryeong => new Vector3(0f, FloorY, 1.42f);
        /// <summary>서고로 가는 비밀 통로의 끝 — 빌더가 실제 경로 끝점으로 덮어쓴다.</summary>
        public static Vector3 ExitToArchive => ExitToArchiveFallback;
    }
}
