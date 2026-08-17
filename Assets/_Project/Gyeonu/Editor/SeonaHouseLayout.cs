using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 선아 집 실내(Gyeonu_SeonaHouse) 공용 좌표. **모든 수치는 실측이다** — 이 씬은
    /// 관측실·서고·집무실과 달리 큐브로 짓지 않고 `Prefabs/KimMyeonggwan/SeonaHouse.prefab`
    /// 원본 한옥을 그대로 쓴다(사용자 지시 2026-08-17). 그래서 좌표를 우리가 정하는 게 아니라
    /// **건물에서 읽어 온 값을 여기 적어 두는** 방향이다.
    ///
    /// 【건물 개요】 15.36 × 9.51m, 높이 6.46m, 269 렌더러 / 206,836 tri (지붕만 164,430).
    ///    마루 상면 0.92~1.07 (북서가 높고 남동이 낮게 기울어 있다), 서까래 밑면 3.0~4.05.
    ///    프리팹 로컬 = 월드 (원점 배치). 마을 씬과 같은 방위 — **앞면(툇마루) = −Z = 남**.
    ///
    /// ⚠️ **건물이 반듯하지 않다.** 기둥열이 x −1.481(남) → −1.414(북) 처럼 몇 cm씩 틀어져 있다.
    ///    벽면을 직선으로 가정하면 5~15cm 어긋난다 — 방 경계는 전부 **안쪽으로 넉넉히 물려**
    ///    잡았다. 콜라이더가 벽보다 조금 안쪽에 있는 건 보이지 않지만, 반대는 벽을 뚫는다.
    ///
    /// 【세 칸 구획 — 어떻게 나눴나】
    ///    원본 몸채(z −1.65~0.9)는 12.4m가 칸막이 없이 통으로 트여 있었다. 사용자 지시대로
    ///    팩의 창호문 모듈(DoorFrame01A + 세살문 4짝 + Wall02B)을 **기둥열 위에** 복제해 세워
    ///    선아 방 │ 대청 │ 아버지 방 으로 갈랐다. 기둥 간격이 2.60m, 모듈이 2.65m라
    ///    문틀 끝이 기둥 단면 안에 묻힌다 — 원본이 지어진 방식 그대로다.
    ///
    /// ⚠️ **대청 남면은 원래 벽(머름 + 창 2짝)이었다.** 원본에서 바닥까지 열린 출입문은
    ///    서쪽 두 칸(x −6.3~−1.48)뿐이라, 그대로 두면 플레이어가 선아 방으로 들어서게 된다.
    ///    사용자 지시가 "대청 = 들어서면 처음 서는 곳"이므로 중앙 칸 남면의 벽·머름·창을
    ///    **비활성화**하고(삭제 아님 — 프리팹 인스턴스라 되돌릴 수 있다) 같은 창호문 모듈을 얹었다.
    ///    대청이 마당을 향해 열리는 건 한옥의 원래 문법이기도 하다.
    /// </summary>
    public static class SeonaHouseLayout
    {
        public const string SceneName = "Gyeonu_SeonaHouse";
        public const string ScenePath = "Assets/_Project/Gyeonu/Scenes/Gyeonu_SeonaHouse.unity";
        public const string HousePrefab = "Assets/_Project/Gyeonu/Prefabs/KimMyeonggwan/SeonaHouse.prefab";

        public const string HouseRoot = "선아집_본채";
        public const string PartRoot = "선아집_칸막이";
        public const string LightRoot = "선아집_조명";
        public const string MarkerRoot = "선아집_마커";
        public const string PropRoot = "선아집_소품";
        public const string WalkRoot = "선아집_보행콜라이더";

        // ── 기둥열 (칸막이가 서는 자리 — 실측 기둥 중심) ──────────────
        /// <summary>선아 방 │ 대청 경계. 남·북 기둥 중심의 중점.</summary>
        public static readonly Vector3 PartWest = new Vector3(-1.4475f, 0f, -0.3445f);
        /// <summary>대청 │ 아버지 방 경계.</summary>
        public static readonly Vector3 PartEast = new Vector3(1.3215f, 0f, -0.4155f);
        /// <summary>대청 남면 — 새 출입 창호문이 서는 자리 (기둥 x −1.481 / +1.288 사이).</summary>
        public static readonly Vector3 PartSouth = new Vector3(-0.0965f, 0f, -1.680f);
        /// <summary>아버지 방 북면 — 부엌을 막는 고정 창호문 (기둥 x 1.355 / 4.124 사이).</summary>
        public static readonly Vector3 PartKitchen = new Vector3(2.7395f, 0f, 0.849f);
        /// <summary>뒷골방 북면 — 원본에 벽이 없어 하늘이 뚫려 있던 자리 (기둥 x −1.39 / +1.38).</summary>
        public static readonly Vector3 PartNook = new Vector3(-0.005f, 0f, 2.485f);

        // ── 방 경계 (보행 콜라이더용 안쪽면. 벽에서 3~8cm 물렸다) ────
        // 선아 방 = 몸채 서쪽 + 그 북쪽 칸 (사이에 벽이 없어 한 방이다)
        public const float SeonaX0 = -6.13f, SeonaX1 = -1.56f;
        public const float SeonaZ0 = -1.44f, SeonaZ1 = 2.40f;
        // 대청 = 중앙 칸 **전체 깊이**.
        // ⚠️ 원본은 z 0.92에도 벽(머름 + 창 2짝)이 있어 중앙 칸이 앞뒤로 갈려 있었다.
        //    앞칸만 쓰면 깊이가 2.33m뿐이라, 들어서자마자 1.8m 앞이 벽이었다(1차 실측 스크린샷).
        //    그 벽을 비우고 뒤칸까지 합쳐 2.55 × 3.90m 로 만들었다 — 대청 뒤가 트인 건
        //    한옥에서 흔한 구성이고, 문설주 위 인방(Wall02F)은 남겨 개구부로 읽힌다.
        public const float DaecheongX0 = -1.34f, DaecheongX1 = 1.21f;
        public const float DaecheongZ0 = -1.53f, DaecheongZ1 = 2.38f;
        // 아버지 방 = 몸채 동쪽
        public const float FatherX0 = 1.44f, FatherX1 = 4.00f;
        public const float FatherZ0 = -1.60f, FatherZ1 = 0.76f;
        // 아버지 방 동쪽 골방 (남으로 뻗은 날개 — 사이에 벽이 없어 아버지 방의 일부다)
        public const float WingX0 = 4.09f, WingX1 = 6.04f;
        public const float WingZ0 = -3.22f, WingZ1 = 0.68f;

        /// <summary>대청 뒤쪽 ↔ 선아 방 북칸을 잇는 원본 문(SM_Door03A, x −1.40).
        /// 대청에서 선아 방으로 가는 **두 번째 길**이다 — 여기만 벽 콜라이더를 비운다.</summary>
        public const float BackDoorZ0 = 1.09f, BackDoorZ1 = 1.81f;

        /// <summary>벽 콜라이더 윗면. 서까래 밑면(3.0~4.05)보다 위 — 넘어갈 수 없게.</summary>
        public const float WallTop = 4.30f;
        /// <summary>바닥 콜라이더가 없는 곳으로 떨어지지 않게 하는 바닥 밑면.</summary>
        public const float FloorBottom = -0.60f;

        // ── 마커 ────────────────────────────────────────────────
        /// <summary>마을에서 들어선 자리 — 대청 남쪽 문 바로 안쪽, 방 안(북)을 향한다.
        /// 좌우로 선아 방·아버지 방 창호문이 함께 보이는 자리다.</summary>
        public static readonly Vector3 SpawnFromVillage = new Vector3(-0.06f, 1.00f, -1.02f);
        public const float SpawnYaw = 0f;
        /// <summary>마을로 나가는 지점 — 대청 남쪽 문간.</summary>
        public static readonly Vector3 ExitToVillage = new Vector3(-0.06f, 1.00f, -1.45f);

        /// <summary>밤의 유일한 실내 광원 자리 — 아버지 방 서안 위 등잔.
        /// Furnisher의 등잔 기물과 Builder의 밤 광원이 이 값을 공유한다.
        /// ⚠️ 서안을 문간 가까이(x 2.05) 두면 **칸막이 문 앞을 막는다** — 문 개구부가 z −0.98~0.15인데
        ///    서안 차단 박스가 바로 그 앞에 걸려 자동 보행이 아버지 방에 못 들어갔다(실측).
        ///    남벽 쪽으로 물려 문간을 비웠다.</summary>
        public static readonly Vector3 LampSpot = new Vector3(2.70f, 0.96f, -1.05f);
    }
}
