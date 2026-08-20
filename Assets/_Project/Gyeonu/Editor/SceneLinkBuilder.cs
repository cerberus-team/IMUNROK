using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 씬 전환 연결 — 출구·도착 스폰을 씬에 배치한다. 멱등(몇 번 실행해도 같은 결과).
    ///
    /// ■ 왜 기존 마커를 건드리지 않고 새 그룹에 만드는가
    ///   각 씬 빌더(EunhaDamBuilder 등)는 마커 그룹을 RecreateGroup으로 **통째로 지웠다
    ///   다시 만든다**. 거기에 SceneExit를 붙여 두면 빌더를 한 번만 다시 돌려도 전환이
    ///   전부 날아간다. 그래서 전환 오브젝트는 아무 빌더도 손대지 않는 루트 그룹
    ///   <see cref="LinkRoot"/> 안에만 만든다. 기존 마커는 좌표 근거로만 읽는다.
    ///
    /// ■ 허브 복귀는 **꺼져 있다** (2026-08-20 방침 확정)
    ///   조사청은 한양이라 멀다. 사건이 끝나 판결을 내릴 때가 아니면 돌아갈 수 없다.
    ///   그래서 사건 도중 조사청으로 가는 경로를 전부 없앴다.
    ///   ⚠️ **코드는 지우지 않았다.** 엔딩 시점에는 복귀가 필요하고, 아래 세 곳의 좌표는
    ///      실측으로 정한 값이라 지우면 다시 찾아야 한다. <see cref="HubReturnEnabled"/> 를
    ///      true로 되돌리고 빌더를 한 번 돌리면 그대로 살아난다.
    ///   ⚠️ "안 만든다"가 아니라 **"있으면 지운다"** 로 짰다 (<see cref="HubReturn"/>).
    ///      안 만들기만 하면 이미 씬에 놓인 그룹이 그대로 남는다.
    ///
    /// ■ 좌표
    ///   XZ는 실측으로 확정한 값(2026-08-17 승인)이고, Y는 실행 시점에 레이캐스트로
    ///   보행면에 앉힌다. 지형이 조금 바뀌어도 마커가 공중에 뜨거나 묻히지 않는다.
    /// </summary>
    public static class SceneLinkBuilder
    {
        const string LinkRoot = "씬전환";
        const string HubRoot  = "씬전환_허브복귀";
        const string HubScene = "HubScene";

        /// <summary>
        /// 조사청 복귀 출구를 놓을 것인가. **엔딩이 붙기 전까지는 false** (2026-08-20 방침).
        /// true로 바꾸고 `Tools ▸ 이문록 ▸ 씬 전환 연결 (전 씬 일괄 + 저장)` 을 한 번 돌리면
        /// 성하리·은하담·관아 세 곳의 복귀 출구가 원래 좌표 그대로 되살아난다.
        /// </summary>
        const bool HubReturnEnabled = false;

        /// <summary>시간대 동기화 컴포넌트를 담는 루트 오브젝트 이름.</summary>
        const string TimeSyncName = "씬전환_시간대";

        // ── 씬 이름 ──
        const string S_Village    = "Gyeonu";
        const string S_EunhaDam   = "Gyeonu_EunhaDam";
        const string S_Gwana      = "Gyeonu_Gwana";
        const string S_Office     = "Gyeonu_GwanaOffice";
        const string S_Observ     = "Gyeonu_Observatory";   // 관측실 + 서고 통합
        const string S_GyeonuVil  = "Gyeonu_GyeonuVillage";
        const string S_SeonaHouse = "Gyeonu_SeonaHouse";

        static readonly string[] AllScenes =
        {
            S_Village, S_EunhaDam, S_Gwana, S_Office, S_Observ, S_GyeonuVil, S_SeonaHouse,
        };

        static string ScenePath(string name) => "Assets/_Project/Gyeonu/Scenes/" + name + ".unity";

        // ══════════════════════════════════════════════════════════
        //  메뉴
        // ══════════════════════════════════════════════════════════

        [MenuItem("Tools/이문록/씬 전환 연결 (활성 씬)", priority = 300)]
        public static void BuildActive()
        {
            var scene = SceneManager.GetActiveScene();
            if (!Build(scene.name))
            {
                Debug.LogWarning($"[씬전환] '{scene.name}' 은 연결 대상 씬이 아니다.");
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[씬전환] '{scene.name}' 연결 완료 — 저장은 직접 할 것.");
        }

        [MenuItem("Tools/이문록/씬 전환 연결 (전 씬 일괄 + 저장)", priority = 301)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[씬전환] 사용자가 취소했다.");
                return;
            }

            string restore = SceneManager.GetActiveScene().path;
            int done = 0;

            foreach (var name in AllScenes)
            {
                var path = ScenePath(name);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!Build(name)) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                done++;
                Debug.Log($"[씬전환] {name} 연결·저장 완료");
            }

            if (!string.IsNullOrEmpty(restore))
                EditorSceneManager.OpenScene(restore, OpenSceneMode.Single);

            Debug.Log($"[씬전환] 전 씬 일괄 완료 — {done}/{AllScenes.Length}");
        }

        [MenuItem("Tools/이문록/씬 전환 — Build Settings 등록", priority = 302)]
        public static void RegisterBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var have = new HashSet<string>();
            foreach (var s in list) have.Add(s.path);

            int added = 0;
            foreach (var name in AllScenes)
            {
                var path = ScenePath(name);
                if (have.Contains(path)) continue;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogError($"[씬전환] 씬 파일이 없다: {path}");
                    continue;
                }
                list.Add(new EditorBuildSettingsScene(path, true));
                added++;
                Debug.Log($"[씬전환] Build Settings 추가: {path}");
            }

            if (added > 0) EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[씬전환] Build Settings — {added}개 추가, 총 {list.Count}개");
        }

        // ══════════════════════════════════════════════════════════
        //  씬별 배치
        // ══════════════════════════════════════════════════════════

        static bool Build(string sceneName)
        {
            switch (sceneName)
            {
                case S_Village:    BuildVillage();    return true;
                case S_EunhaDam:   BuildEunhaDam();   return true;
                case S_Gwana:      BuildGwana();      return true;
                case S_Office:     BuildOffice();     return true;
                case S_Observ:     BuildObservatory();return true;
                case S_GyeonuVil:  BuildGyeonuVil();  return true;
                case S_SeonaHouse: BuildSeonaHouse(); return true;
                default: return false;
            }
        }

        // ── ① 성하리 마을 ──────────────────────────────────────
        static void BuildVillage()
        {
            var link = Recreate(LinkRoot);
            var hub  = HubReturn();          // 꺼져 있으면 씬에 남은 그룹을 지우고 null

            // 동쪽 능선의 안부(고개) — 마루 너머로 씬 밖이 보이기 직전에 넘긴다
            Walk(link, "출구_은하담", new Vector3(86f, 0f, 11f), 90f,
                 new Vector3(4f, 6f, 20f), S_EunhaDam, "SpawnPoint_FromVillage");
            // 트리거는 x 84~88. 도착 스폰은 그 밖(x 82)에 둔다 — 볼륨 경계에 세우면
            // 되돌아 나가려고 한 발 떼기도 전에 다시 넘어간다.
            Spawn(link, "SpawnPoint_FromEunhaDam", new Vector3(82f, 0f, 11f), 270f);

            // 관아 언덕 계단 꼭대기. (언덕 안개는 이제 위치 기반 양방향이라 예약 키가 필요 없다)
            Walk(link, "출구_관아", new Vector3(-15.9f, 0f, 91f), 0f,
                 new Vector3(12f, 6f, 3f), S_Gwana, "SpawnPoint_FromVillage");
            // 트리거는 z 89.5~92.5. 스폰을 z 89.7에 두면 **트리거 한복판에서 시작**한다 —
            // 계단 마지막 단(z 88.3) 위로 내린다.
            Spawn(link, "SpawnPoint_FromGwana", new Vector3(-15.9f, 0f, 88.3f), 180f);

            // 선아집 남면 문 — 마당 일각문 축(x −44.6)에 가장 가깝고 마루 높이인 문(y 1.12).
            // 문짝 한가운데를 조준하도록 y를 고정한다(지붕 밑이라 레이캐스트를 쓸 수 없다).
            TouchFixed(link, "출구_선아집", new Vector3(-44.97f, 1.95f, 60.55f), 180f,
                       new Vector3(1.8f, 2.0f, 0.6f), S_SeonaHouse, "SpawnPoint_FromVillage",
                       "선아 집", "들어가기");
            // 디딤돌 위 — 처마 밑이라 낮은 곳에서 탐침한다
            Spawn(link, "SpawnPoint_FromSeonaHouse", new Vector3(-44.97f, 0f, 58.5f), 180f, 4f);

            // 조사청 복귀 — 마을 입구 오공문 남쪽. 시작 지점(z −82)에서 뒤로 물러설 때
            // 곧바로 밟히지 않도록 z −85 → −89로 물린다 (면까지 5.5m).
            // 2026-08-20: 사건 도중에는 놓지 않는다 (HubReturnEnabled 참고).
            if (hub != null)
                Walk(hub, "출구_조사청", new Vector3(-13.3f, 0f, -89f), 180f,
                     new Vector3(8f, 5f, 3f), HubScene, "");

            // 성하리가 시간대의 **기준 씬**이다 — 여기 상태가 다른 씬으로 따라간다.
            InstallTimeSync(WorldTimeSync.Kind.야외_하늘프리셋, isReference: true);
        }

        // ── ② 은하담 ──────────────────────────────────────────
        static void BuildEunhaDam()
        {
            var link = Recreate(LinkRoot);
            var hub  = HubReturn();          // 꺼져 있으면 씬에 남은 그룹을 지우고 null

            // 마을 방향 +X 끝 (기존 Exit_ToVillage 자리)
            Walk(link, "출구_마을", new Vector3(136f, 0f, 0f), 90f,
                 new Vector3(4f, 6f, 24f), S_Village, "SpawnPoint_FromEunhaDam");
            Spawn(link, "SpawnPoint_FromVillage", new Vector3(132f, 0f, 0f), 270f);   // 트리거 x 134~138 밖

            // 오작교 암문 — 2026-08-18 교대_남 석축으로 옮겼다.
            // 문과 전환 트리거는 AmmunBuilder가 별도 루트(교대_암문)에 만든다.
            // 다리 한복판 난간 앞(0, 9.9, 4.2)에 있던 옛 출구는 여기서 만들지 않는다:
            //   ① 93m 다리 한가운데 난간에 비밀문이 있을 까닭이 없고
            //   ② 아무 표시가 없어 찾을 수가 없었다.

            // 암문에서 나오면 석축 하단 물가다 (2026-08-19 — 문이 교대 하단으로 옮겨짐).
            // 문을 등지고 물가(남쪽)를 본다. 통로 트리거(z −3.0~−1.8)에서 충분히 밖이다.
            // 2026-08-20 3차 — 문이 교대 **서면**(x 29.71, z −7.95)으로 옮겨졌다.
            //   나오면 물가 띠 위, 강(서쪽)을 등지고 석축을 마주 본 채가 아니라
            //   **강 쪽(서, yaw 270)** 을 보게 둔다 — 나와서 곧장 북상해 돌아갈 수 있게.
            SpawnFixed(link, "SpawnPoint_FromObservatory", new Vector3(28.85f, 0.70f, -7.95f), 270f);

            // 북서 숲 뒤 — 견우마을로. 진행 조건이 걸린 유일한 출구다.
            //
            // 2026-08-18 이전: 정서(-95, 13). 다리 서단에서 일직선이라 "그냥 쭉 걸으면 도착"이었다.
            // 지금은 서안 벚·사시나무 군락(x -66~-95 / z 3~27)을 **북으로 돌아** 언덕을 올라야
            // 닿는다. 직선 거리는 비슷하지만 군락이 시야와 동선을 끊어 길을 찾아가는 맛이 생긴다.
            var toGyeonu = Walk(link, "출구_견우마을", new Vector3(-74f, 0f, 58f), 200f,
                                new Vector3(8f, 6f, 8f), S_GyeonuVil, "SpawnPoint_FromEunhaDam");
            toGyeonu.requiredFlags = new[] { GyeonuWorld.F_비밀지도획득, GyeonuWorld.F_타공지도_길밝힘 };
            // 시간대가 마을을 따라 낮이 될 수도 있으므로 문구는 밤을 전제하지 않는다.
            toGyeonu.blockedMessage = "숲길이 어디로 이어지는지 가늠할 수 없다.";   // 부족한 조건을 굳이 알려 주지 않는다 (정답 누설)

            // 견우마을에서 돌아왔을 때 서는 자리 — 트리거(반경 4m) 밖에 두고 다리 쪽을 보게 한다.
            Spawn(link, "SpawnPoint_FromGyeonuVillage", new Vector3(-68f, 0f, 52f), 160f);

            // 조사청 복귀 — 진입 지점 옆 (기존 Exit_ToHub 자리)
            // 2026-08-20: 사건 도중에는 놓지 않는다 (HubReturnEnabled 참고).
            if (hub != null)
                Walk(hub, "출구_조사청", new Vector3(102f, 0f, -7f), 180f,
                     new Vector3(6f, 5f, 3f), HubScene, "");

            InstallTimeSync(WorldTimeSync.Kind.야외_하늘프리셋);
        }

        // ── ③ 관아 외부 ───────────────────────────────────────
        static void BuildGwana()
        {
            var link = Recreate(LinkRoot);
            var hub  = HubReturn();          // 꺼져 있으면 씬에 남은 그룹을 지우고 null

            Walk(link, "출구_마을", new Vector3(0f, 0f, -77f), 180f,
                 new Vector3(16f, 6f, 3f), S_Village, "SpawnPoint_FromGwana");

            // ── 동헌 온돌방 창호문 (2026-08-17) ─────────────────
            // 전에는 대청 한복판(0, 4, 11.9)에 2.2×2.2×0.8 상자를 놓아, 마루 어디서 쳐다봐도
            // 들어가졌다. 실측하니 온돌방으로 드는 문은 **측벽의 분합문 2짝**이다:
            //   Door_L1 (−4.57, 4.94, 13.64) / Door_L2 (−4.57, 4.94, 14.86), 각 폭 1.12·높이 1.86
            // 두 짝을 합친 크기로 좁히고 그 자리에 붙인다. 회벽 콜라이더(x −4.50~−5.20)보다
            // 앞면이 앞서야 레이가 벽에 먼저 맞지 않는다 → 중심 x −4.55, 두께 0.5 (앞면 −4.30).
            TouchFixed(link, "출구_집무실", new Vector3(-4.55f, 4.94f, 14.25f), 90f,
                       new Vector3(0.5f, 1.86f, 2.45f), S_Office, "SpawnPoint_FromGwana",
                       "온돌방", "들어가기");

            // 집무실에서 나오면 그 문 앞 대청마루(y 4.00)에 선다. 문을 등지고 마루 쪽(동)을 본다.
            SpawnFixed(link, "SpawnPoint_FromGwanaOffice", new Vector3(-3.4f, 4.0f, 14.25f), 90f);

            // 진입 지점(0, −74) 옆이되 **걷는 길에서는 비켜서** 둔다.
            // x −4에 두면 도착 지점에서 면까지 2m라, 옆으로 한 발 디디면 조사청으로 끌려간다.
            // 2026-08-20: 사건 도중에는 놓지 않는다 (HubReturnEnabled 참고).
            if (hub != null)
                Walk(hub, "출구_조사청", new Vector3(-10f, 0f, -72f), 180f,
                     new Vector3(4f, 5f, 4f), HubScene, "");

            // 언덕 안개를 **위치 기반 양방향**으로 돌린다 (2026-08-17).
            //   올라오면 걷히고, 내려가면 다시 자욱해진다 → 아래가 마을인지 숲인지 모른 채 내려간다.
            //   집무실에서 나온 자리(언덕 위)는 진행도 1이라 안개가 끼지 않는다 —
            //   External 예약이 없어도 헛연출이 안 나므로 SceneEntry로 되돌린다.
            SetFogRevealBidirectional();

            InstallTimeSync(WorldTimeSync.Kind.야외_하늘프리셋);
        }

        // ── ④ 관아 집무실 ─────────────────────────────────────
        static void BuildOffice()
        {
            var link = Recreate(LinkRoot);

            // 들어온 문 (−Z)
            TouchFixed(link, "출구_관아", new Vector3(0f, 1.1f, -2.05f), 180f,
                       new Vector3(2.2f, 2.2f, 0.6f), S_Gwana, "SpawnPoint_FromGwanaOffice",
                       "마당", "나가기");

            // 비밀 통로 끝 → 서고 (서고는 관측실 씬 안에 있다)
            // 지하 통로라 위에서 쏜 레이가 천장에 맞는다 — 바닥 −1.65를 고정으로 준다
            WalkFixed(link, "출구_서고", new Vector3(0f, -1.65f, 12.09f), 0f,
                      new Vector3(3f, 3f, 1.2f), S_Observ, "SpawnPoint_FromGwanaOffice");
            // 트리거는 z 11.49~12.69. 스폰을 11.4에 두면 9cm 앞이라 사실상 붙어 있다.
            SpawnFixed(link, "SpawnPoint_FromArchive", new Vector3(0f, -1.65f, 10.4f), 180f);

            // 병풍·비밀문 상태를 세션에 기억시킨다 —
            // 서고에 다녀와도 병풍은 치워진 채, 문은 열린 채, 잠김 안내도 다시 뜨지 않는다.
            BindSecretDoorPersistence();

            InstallTimeSync(WorldTimeSync.Kind.실내_조명그룹);
        }

        /// <summary>집무실 병풍·비밀문에 상태 유지 키를 물린다.</summary>
        static void BindSecretDoorPersistence()
        {
            var screen = Object.FindFirstObjectByType<FoldingScreen>();
            if (screen != null)
            {
                Undo.RecordObject(screen, "병풍 상태 유지");
                screen.persistKey = GyeonuWorld.F_병풍_치움;
                EditorUtility.SetDirty(screen);
            }
            else Debug.LogWarning("[씬전환] 병풍(FoldingScreen)을 찾지 못했다");

            var door = Object.FindFirstObjectByType<LockedDoor>();
            if (door != null)
            {
                Undo.RecordObject(door, "비밀문 상태 유지");
                door.unlockedKey  = GyeonuWorld.F_비밀문_해제;
                door.openKey      = GyeonuWorld.F_비밀문_열림;
                door.announcedKey = GyeonuWorld.F_비밀문_안내함;
                EditorUtility.SetDirty(door);
            }
            else Debug.LogWarning("[씬전환] 비밀문(LockedDoor)을 찾지 못했다");

            Debug.Log("[씬전환] 병풍·비밀문 상태 유지 연결 완료");
        }

        // ── ⑤ 관측실 + 서고 ───────────────────────────────────
        static void BuildObservatory()
        {
            // 서고 구역 마커 개명 — 이 출구들이 통하는 곳은 관아 외부가 아니라 '집무실'이다
            RenameMarker("SpawnPoint_FromGwana", "SpawnPoint_FromGwanaOffice");
            RenameMarker("Exit_ToGwana", "Exit_ToGwanaOffice");

            var link = Recreate(LinkRoot);

            // 오작교 암문 (관측실 통로 입구)
            TouchFixed(link, "출구_은하담", new Vector3(0f, 1.1f, -0.02f), 180f,
                       new Vector3(2.4f, 2.2f, 0.6f), S_EunhaDam, "SpawnPoint_FromObservatory",
                       "암문", "나가기");

            // ⚠️ 시간대 동기화를 **일부러 붙이지 않는다**.
            //    서고는 지하라 창이 없어 낮밤이 드러날 곳이 없고, 관측실은 혼상에 별을 투영해
            //    읽어 내는 공간이라 낮으로 바뀌면 사건의 핵심 연출이 성립하지 않는다.
            //    두 공간은 늘 자기 조명을 유지한다.

            // 서고 복도 시작 → 집무실 비밀 통로
            // 서가·궤가 머리 위에 걸려 레이캐스트가 그쪽에 맞는다 — 서고 바닥 0을 고정으로 준다.
            // z를 42.2로 민 이유: 도착 스폰(SpawnPoint_FromGwanaOffice, z 40.9)은 ArchiveBuilder가
            // 만든 기존 마커라 건드리지 않고, 대신 트리거를 물려 간격 0.35 → 0.7m로 벌린다.
            WalkFixed(link, "출구_집무실", new Vector3(43.01f, 0f, 42.2f), 0f,
                      new Vector3(3f, 3f, 1.2f), S_Office, "SpawnPoint_FromArchive");
        }

        // ── ⑥ 견우마을 (엔딩) ─────────────────────────────────
        static void BuildGyeonuVil()
        {
            var link = Recreate(LinkRoot);

            // 들어온 숲길로 되돌아 나간다 (2026-08-17 왕복 전환).
            // 기존 Exit_ToEunhaDam 마커(z −49.5) 자리를 그대로 쓴다.
            Walk(link, "출구_은하담", new Vector3(2f, 0f, -49.5f), 180f,
                 new Vector3(16f, 6f, 3f), S_EunhaDam, "SpawnPoint_FromGyeonuVillage");

            // 도착 지점은 트리거(z −51~−48) 밖으로 물린다 — 도착하자마자 되돌아가지 않게.
            Spawn(link, "SpawnPoint_FromEunhaDam", new Vector3(2f, 0f, -46f), 9f);

            // 견우마을은 **날씨만** 고정한다 (2026-08-20 지시로 정정).
            //   시각(낮/밤)은 성하리 마을을 따라간다. 비만 오지 않는다.
            //     마을 낮_맑음 → 낮_맑음 / 밤_맑음 → 밤_맑음
            //     마을 낮_비   → 낮_맑음 / 밤_비   → 밤_맑음
            //   2026-08-19에는 동기화에서 통째로 뺐었는데(항상 낮_맑음), 그러면 마을이
            //   밤이어도 견우마을만 낮이라 시각이 어긋났다.
            InstallTimeSync(WorldTimeSync.Kind.야외_하늘프리셋, forceClear: true);
        }

        // ── ⑦ 선아 집 실내 ────────────────────────────────────
        static void BuildSeonaHouse()
        {
            var link = Recreate(LinkRoot);
            TouchFixed(link, "출구_마을", new Vector3(-0.06f, 1.9f, -1.45f), 180f,
                       new Vector3(2.6f, 2.2f, 0.6f), S_Village, "SpawnPoint_FromSeonaHouse",
                       "마당", "나가기");

            InstallTimeSync(WorldTimeSync.Kind.실내_조명그룹);
        }

        // ══════════════════════════════════════════════════════════
        //  헬퍼
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 조사청 복귀 그룹. <see cref="HubReturnEnabled"/> 가 꺼져 있으면 **씬에 남아 있는 그룹을
        /// 지우고 null을 돌려준다** — 호출부는 null이면 출구를 놓지 않는다.
        /// 단순히 "안 만들기"로 두면 지난 실행에서 놓인 그룹이 그대로 살아 있어 소용이 없다.
        /// </summary>
        static Transform HubReturn()
        {
            if (HubReturnEnabled) return Recreate(HubRoot);

            var old = GameObject.Find(HubRoot);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
                Debug.Log($"[씬전환] 조사청 복귀 그룹 '{HubRoot}' 제거 — 사건 도중에는 돌아갈 수 없다");
            }
            return null;
        }

        /// <summary>루트 그룹을 지우고 새로 만든다 — 멱등성의 근거.</summary>
        static Transform Recreate(string name)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "씬 전환 연결");
            return go.transform;
        }

        /// <summary>걸어서 밟는 출구 — 트리거 박스. y는 보행면에 앉힌다.</summary>
        static SceneExit Walk(Transform parent, string name, Vector3 xzPos, float yaw,
                              Vector3 size, string scene, string spawn,
                              float probeFrom = 200f, string fogKey = "")
        {
            return WalkFixed(parent, name, new Vector3(xzPos.x, GroundY(xzPos, probeFrom), xzPos.z),
                             yaw, size, scene, spawn, fogKey);
        }

        /// <summary>
        /// 걸어서 밟는 출구 — 바닥 높이를 그대로 받는다.
        /// 실내·지하처럼 위에서 쏜 레이가 천장·선반에 맞는 곳에 쓴다.
        ///
        /// ⚠️ <paramref name="size"/> 는 **월드 축** 기준이고, 이 트리거는 회전시키지 않는다.
        ///    yaw를 주면 박스가 같이 돌아 x·z가 뒤바뀐다 — 길을 가로막는 벽으로 놓은 것이
        ///    진행 방향으로 길쭉한 복도가 되어 옆으로 비껴 걸으면 안 밟힌다(2026-08-17 실측 버그).
        ///    출구의 '방향'은 어차피 쓰이지 않는다(목적지는 targetScene이 말해 준다).
        /// </summary>
        static SceneExit WalkFixed(Transform parent, string name, Vector3 floorPos, float yaw,
                                   Vector3 size, string scene, string spawn, string fogKey = "")
        {
            var go = MakeExit(parent, name,
                              new Vector3(floorPos.x, floorPos.y + size.y * 0.5f, floorPos.z), 0f, size);
            var ex = go.GetComponent<SceneExit>();
            ex.mode = SceneExit.Trigger.걸어서_트리거;
            ex.targetScene = scene;
            ex.targetSpawn = spawn;
            ex.displayName = name;
            ex.armFogRevealKey = fogKey;
            return ex;
        }

        /// <summary>
        /// 클릭해서 들어가는 출구 — 좌표를 그대로 쓴다(실내·구조물 위).
        ///
        /// ⚠️ <paramref name="size"/> 는 <see cref="WalkFixed"/> 와 마찬가지로 **월드 축** 기준이고
        ///    박스를 회전시키지 않는다. yaw 90/270을 주면 x·z가 뒤바뀌어, 문짝 폭으로 좁힌 판이
        ///    엉뚱한 축으로 누워 버린다 (동헌 창호문에서 실측 — 폭 2.45가 벽을 뚫고 X로 누웠다).
        ///    <paramref name="yaw"/> 는 문서용으로만 받는다.
        /// </summary>
        static void TouchFixed(Transform parent, string name, Vector3 pos, float yaw, Vector3 size,
                               string scene, string spawn, string label, string prompt)
        {
            var go = MakeExit(parent, name, pos, 0f, size);
            var ex = go.GetComponent<SceneExit>();
            ex.mode = SceneExit.Trigger.상호작용;
            ex.targetScene = scene;
            ex.targetSpawn = spawn;
            ex.displayName = label;
            ex.promptText = prompt;
        }

        static GameObject MakeExit(Transform parent, string name, Vector3 pos, float yaw, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;          // 보행을 막지 않는다. 레이캐스트는 트리거도 맞는다.
            box.size = size;

            go.AddComponent<SceneExit>();
            return go;
        }

        /// <summary>도착 스폰 마커 — y를 보행면에 앉힌다.</summary>
        static void Spawn(Transform parent, string name, Vector3 xzPos, float yaw, float probeFrom = 200f)
        {
            SpawnFixed(parent, name, new Vector3(xzPos.x, GroundY(xzPos, probeFrom), xzPos.z), yaw);
        }

        /// <summary>도착 스폰 마커 — 좌표를 그대로 쓴다.</summary>
        static void SpawnFixed(Transform parent, string name, Vector3 pos, float yaw)
        {
            if (GameObject.Find(name) != null)
            {
                Debug.Log($"[씬전환] '{name}' 는 씬에 이미 있다 — 새로 만들지 않는다.");
                return;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>
        /// 위에서 아래로 쏴 보행면 높이를 구한다. 못 맞히면 지형 샘플, 그것도 없으면 0.
        ///
        /// ⚠️ <paramref name="probeFrom"/> 는 지붕 때문에 있다. 처마 밑(동헌 앞 월대, 툇마루
        ///    앞 디딤돌 등)에서 하늘에서 쏘면 **지붕에 먼저 맞아** 마커가 지붕 위에 올라앉는다.
        ///    그런 자리는 보행면보다 위·지붕보다 아래인 높이를 넘겨야 한다.
        /// </summary>
        static float GroundY(Vector3 xzPos, float probeFrom = 200f)
        {
            var from = new Vector3(xzPos.x, probeFrom, xzPos.z);
            RaycastHit hit;
            if (Physics.Raycast(from, Vector3.down, out hit, probeFrom + 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                Debug.Log($"[씬전환] 보행면 @{xzPos.x:F1},{xzPos.z:F1} = {hit.point.y:F2} ({hit.collider.name})");
                return hit.point.y;
            }

            var t = Terrain.activeTerrain;
            if (t != null) return t.SampleHeight(xzPos);

            Debug.LogWarning($"[씬전환] 보행면을 못 찾음 @{xzPos.ToString("F1")} — y=0 사용");
            return 0f;
        }

        /// <summary>
        /// 지정 위치 근처의 리깅된 문을 찾아 DoorSceneExit를 붙인다.
        /// 문이 없으면 false — 호출 쪽이 클릭 전환으로 대체한다.
        /// </summary>
        static bool AttachDoorExit(Vector3 near, float radius, string scene, string spawn)
        {
            MonoBehaviour best = null;
            float bestDist = float.MaxValue;

            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(mb is IOpenable)) continue;
                float d = Vector3.Distance(mb.transform.position, near);
                if (d < radius && d < bestDist) { bestDist = d; best = mb; }
            }
            if (best == null) return false;

            var old = best.GetComponent<DoorSceneExit>();
            if (old != null) Object.DestroyImmediate(old);

            var ex = best.gameObject.AddComponent<DoorSceneExit>();
            ex.door = best;
            ex.targetScene = scene;
            ex.targetSpawn = spawn;
            Debug.Log($"[씬전환] 문 '{best.name}'({best.GetType().Name}) 에 DoorSceneExit 부착 — {scene}");
            return true;
        }

        /// <summary>
        /// 언덕 안개를 위치 기반 양방향으로 돌린다 — 오르면 걷히고 내려가면 다시 짙어진다.
        /// 농도·구간·속도 같은 연출 값은 그대로 두고 발동 방식만 바꾼다.
        /// </summary>
        static void SetFogRevealBidirectional()
        {
            var fog = Object.FindFirstObjectByType<FogReveal>();
            if (fog == null) { Debug.LogWarning("[씬전환] FogReveal을 찾지 못했다 — 안개 연동 건너뜀"); return; }

            // (조기 반환 없음 — 농도 값까지 매번 다시 적용해야 튜닝이 반영된다)

            Undo.RecordObject(fog, "언덕 안개 양방향 전환");
            fog.monotonic = false;
            fog.arm = FogReveal.ArmMode.SceneEntry;

            // 2026-08-18 — 짙은 쪽 끝값을 대폭 올린다.
            // 그 전 값(3/26, 밀도 0.09, 파티클 0.16, 베일 0.55)으로는 내려가는 동안
            // 아래 숲과 벌판이 그대로 보여 "뭐가 있는지 모르는" 상태가 되지 않았다.
            // Linear Fog는 소실 거리가 짧을수록 짙다 — 26m → 7m 로 약 3.7배.
            fog.startFogStart      = 0.5f;    // 3    → 0.5
            fog.startFogEnd        = 7f;      // 26   → 7     (약 3.7배)
            fog.startFogDensity    = 0.38f;   // 0.09 → 0.38  (Exp 모드 대비, 약 4.2배)
            fog.particleStartAlpha = 0.60f;   // 0.16 → 0.60  (약 3.8배)
            fog.skyVeilStartAlpha  = 0.95f;   // 0.55 → 0.95  (거의 불투명 — 하늘까지 가린다)

            EditorUtility.SetDirty(fog);
            Debug.Log("[씬전환] 언덕 안개 '" + fog.name + "' → 위치 기반 양방향 + 농도 강화"
                    + " (소실 " + fog.startFogEnd + "m, 파티클 " + fog.particleStartAlpha
                    + ", 베일 " + fog.skyVeilStartAlpha + ")");
        }

        /// <summary>
        /// 시간대 동기화 컴포넌트를 씬에 세운다. 이미 있으면 값만 갱신한다.
        /// 야외는 하늘 프리셋 4종을 물려 주고, 실내는 OfficeTimeOfDay를 찾아 물린다.
        /// </summary>
        static void InstallTimeSync(WorldTimeSync.Kind kind, bool isReference = false,
                                    bool forceClear = false)
        {
            var go = GameObject.Find(TimeSyncName);
            if (go == null)
            {
                go = new GameObject(TimeSyncName);
                Undo.RegisterCreatedObjectUndo(go, "시간대 동기화 설치");
            }

            var sync = go.GetComponent<WorldTimeSync>();
            if (sync == null) sync = go.AddComponent<WorldTimeSync>();

            sync.kind = kind;
            sync.isReference = isReference;
            sync.forceClear = forceClear;

            if (kind == WorldTimeSync.Kind.실내_조명그룹)
            {
                sync.indoor = Object.FindFirstObjectByType<OfficeTimeOfDay>();
                if (sync.indoor == null)
                    Debug.LogWarning("[씬전환] 실내 시간대: OfficeTimeOfDay를 찾지 못했다");
                else
                    // 씬에 저장된 낮/밤을 기준 값의 초기치로 삼는다
                    Debug.Log($"[씬전환] 실내 시간대 연결 — 현재 {(sync.indoor.IsNight ? "밤" : "낮")}");
            }
            else
            {
                sync.낮_맑음 = LoadPreset("낮_맑음");
                sync.낮_비   = LoadPreset("낮_비");
                sync.밤_맑음 = LoadPreset("밤_맑음");
                sync.밤_비   = LoadPreset("밤_비");
                sync.sun = null;   // 런타임에 가장 밝은 방향광을 스스로 찾는다
            }

            EditorUtility.SetDirty(sync);
            Debug.Log($"[씬전환] 시간대 동기화 설치 — {kind}{(isReference ? " (기준 씬)" : "")}"
                      + (forceClear ? " (날씨 고정: 항상 맑음)" : ""));
        }

        /// <summary>씬의 시간대 동기화를 제거한다 — 시간이 고정된 공간(견우마을)용.</summary>
        static void RemoveTimeSync()
        {
            var go = GameObject.Find(TimeSyncName);
            if (go != null)
            {
                Object.DestroyImmediate(go);
                Debug.Log("[씬전환] 시간대 동기화 제거 (시간 고정 씬)");
            }
        }

        /// <summary>하늘 프리셋을 에디터 시점에 한 번 적용해 씬에 굽는다 — 런타임 동기화가 없는 씬용.</summary>
        static void ApplyFixedSky(string key)
        {
            var preset = LoadPreset(key);
            if (preset == null) return;
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && (sun == null || l.intensity > sun.intensity)) sun = l;
            preset.Apply(sun);
            Debug.Log($"[씬전환] 하늘 고정 적용: {key}");
        }

        static SkyPreset LoadPreset(string key)
        {
            string path = $"Assets/_Project/Gyeonu/Art/Lighting/SkyPreset_{key}.asset";
            var p = AssetDatabase.LoadAssetAtPath<SkyPreset>(path);
            if (p == null) Debug.LogWarning("[씬전환] 하늘 프리셋을 찾지 못했다: " + path);
            return p;
        }

        /// <summary>마커 이름 변경 — 이미 새 이름이면 건드리지 않는다.</summary>
        static void RenameMarker(string oldName, string newName)
        {
            if (GameObject.Find(newName) != null) return;
            var go = GameObject.Find(oldName);
            if (go == null)
            {
                Debug.LogWarning($"[씬전환] 개명 대상 '{oldName}' 을 찾지 못했다.");
                return;
            }
            Undo.RecordObject(go, "마커 개명");
            go.name = newName;
            Debug.Log($"[씬전환] 마커 개명: {oldName} → {newName}");
        }
    }
}
