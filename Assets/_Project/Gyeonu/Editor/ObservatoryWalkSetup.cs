using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관측실 보행 세팅 (2026-08-10, 멱등) — 마을·은하담·견우마을과 같은 방침: **전부 Box**.
    ///
    /// 구조(통로 바닥·벽·천장·계단 디딤판, 방 석벽·문틀, 붕괴 계단실)는 ObservatoryBuilder가
    /// 렌더 박스 원장을 그대로 박스 콜라이더로 내보낸다 — 여기서 중복 생성하지 않는다.
    /// 합성 메시 MeshCollider는 얇은 삼각형에 끼이므로 쓰지 않는다.
    ///
    /// 이 스크립트가 맡는 것:
    ///   ① 소품 차단 — 혼상·혼천의를 통과하지 못하게. 다만 조작할 수 있게 바짝 붙을 수는 있다
    ///      (차단 박스를 기물 외곽선 바로 바깥에 맞춘다).
    ///   ② 수직갱 안전판 — 계단 차단벽을 빠져나가도 8m 아래로 떨어지지 않게 받는 보이지 않는 판.
    ///   ③ 디버그 워커 설치 (SpawnPoint_FromEunhaDam) + Play 자동 보행 검증 웨이포인트.
    /// </summary>
    public static class ObservatoryWalkSetup
    {
        const string RootName = "관측실_보행콜라이더";
        // 좌표는 ObservatoryBuilder의 2026-08-11 개편안과 일치해야 한다 (생성 로그가 실측치를 찍어준다)
        const float RoomY = -3.06f;                              // 작업실 바닥
        const float PlatY = -2.34f;                              // 돔 단 바닥
        static readonly Vector3 ShaftC = new Vector3(16.0f, RoomY, 33.1f);   // 붕괴 개구부 중심 (3.0×3.0)

        [MenuItem("Tools/이문록/관측실 보행 콜라이더 구축")]
        public static void Build()
        {
            if (SceneManager.GetActiveScene().name != "Gyeonu_Observatory")
            {
                Debug.LogError("[관측실 보행] 활성 씬이 Gyeonu_Observatory가 아닙니다");
                return;
            }
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);

            // 구조 콜라이더는 빌더 소관 — 있는지만 확인한다
            int structCount = 0;
            var room = GameObject.Find("관측실");
            if (room != null)
            {
                var s = room.transform.Find("보행콜라이더/구조");
                if (s != null) structCount = s.GetComponentsInChildren<BoxCollider>(true).Length;
            }
            if (structCount == 0)
                Debug.LogWarning("[관측실 보행] 구조 콜라이더가 없습니다 — [관측실 생성]을 먼저 실행하세요");

            int stale = 0;
            foreach (var mc in (room != null ? room.GetComponentsInChildren<MeshCollider>(true) : new MeshCollider[0]))
            {
                Object.DestroyImmediate(mc);   // 예전 빌드가 남긴 합성 메시 콜라이더 제거
                stale++;
            }

            int props = BlockProps(root);
            BuildShaftGuard(root);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[관측실 보행] 구축 완료 — 구조 박스 {structCount}, 소품 차단 {props}, 수직갱 안전판 1" +
                      (stale > 0 ? $", 잔존 MeshCollider 제거 {stale}" : ""));
        }

        /// <summary>혼상·혼천의 차단 — 기물 외곽선 바로 바깥에 세워 손 닿는 거리까지는 접근시킨다.
        /// 혼상 십자 다리(높이 0.13)와 좌대(0.15)는 넘어 다니는 단차라 막지 않는다.</summary>
        static int BlockProps(GameObject root)
        {
            var g = new GameObject("소품차단").transform;
            g.SetParent(root.transform, false);
            int n = 0;

            // ⚠️ 차단 박스는 기물을 통째로 감싸므로 조작 레이캐스트가 항상 여기 먼저 맞는다 —
            //    혼상 클릭이 안 먹히던 원인 (2026-08-14 실측). 빌트인 Ignore Raycast 레이어(2)로
            //    옮겨 레이는 통과시키고 보행 충돌만 남긴다 (ProjectSettings 무수정)
            const int IgnoreRaycast = 2;

            var honsang = GameObject.Find("혼상");
            if (honsang != null)
            {
                // 구 반지름 0.60 → 반폭 0.675면 구 표면에서 0.075m 앞까지 다가선다
                Box(g, "혼상_차단", honsang.transform.position + Vector3.up * 0.95f,
                    new Vector3(1.35f, 1.90f, 1.35f), honsang.transform.rotation, IgnoreRaycast);
                n++;
            }
            else Debug.LogWarning("[관측실 보행] 혼상 없음 — 차단 생략");

            var hon = GameObject.Find("관측실/혼천의_B_장식받침");
            if (hon != null)
            {
                // 지평환 반지름 0.55 → 반폭 0.59면 고리 바로 앞에 서서 돌릴 수 있다
                Box(g, "혼천의_차단", hon.transform.position + Vector3.up * 0.95f,
                    new Vector3(1.18f, 1.90f, 1.18f), hon.transform.rotation, IgnoreRaycast);
                n++;
            }
            else Debug.LogWarning("[관측실 보행] 혼천의 없음 — 차단 생략");
            return n;
        }

        /// <summary>수직갱 안전판 — 끊긴 최하단(y≈-3.96) 바로 아래에 보이지 않는 바닥을 깔아
        /// 만에 하나 난간·차단판을 빠져나가도 8m 아래 갱바닥(-11.3)으로 떨어지지 않게 받는다.
        /// 렌더러가 없으므로 아래를 내려다보는 연출(등불 빛)은 가리지 않는다.</summary>
        static void BuildShaftGuard(GameObject root)
        {
            var g = new GameObject("수직갱안전판").transform;
            g.SetParent(root.transform, false);
            Box(g, "안전판", new Vector3(ShaftC.x, RoomY - 1.30f, ShaftC.z),
                new Vector3(3.2f, 0.2f, 3.2f), Quaternion.identity);
        }

        // ── 디버그 워커 ──────────────────────────────────────
        // 다른 씬(마을·은하담·견우마을)과 동일 구성:
        //   디버그_워커 [CharacterController(1.8/0.3/slope 50/step 0.6) + DebugWalkController]
        //     └ 워커_카메라 [Camera + AudioListener + DebugInteractor], localY 1.7
        // ⚠️ DebugAutoWalker(검증용 오토파일럿)는 Awake에서 DebugWalkController를 꺼버린다 —
        //    씬에 남아 저장되면 WASD가 먹지 않는다. 설치 시 항상 제거한다.
        [MenuItem("Tools/이문록/관측실 디버그 워커 설치")]
        public static void InstallWalker()
        {
            var old = GameObject.Find("디버그_워커");
            if (old != null) Object.DestroyImmediate(old);

            Vector3 spawn = new Vector3(0f, 0f, 0.8f);
            float yaw = 0f;
            var marker = GameObject.Find("SpawnPoint_FromEunhaDam");
            if (marker != null) { spawn = marker.transform.position; yaw = marker.transform.eulerAngles.y; }
            else Debug.LogWarning("[관측실 보행] SpawnPoint_FromEunhaDam 없음 — 통로 입구 기본값 사용");

            var go = new GameObject("디버그_워커");
            go.transform.position = spawn + Vector3.up * 0.1f;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.6f;   // 다른 씬과 동일 (통로 계단 단차 0.17·좌대 0.15에 충분)
            cc.slopeLimit = 50f;

            var eye = new GameObject("워커_카메라");
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            eye.tag = "MainCamera";
            var cam = eye.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            eye.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
               .renderPostProcessing = true;
            eye.AddComponent<AudioListener>();
            eye.AddComponent<IMUNROK.Gyeonu.DebugInteractor>();   // 마을·견우마을과 동일
            go.AddComponent<IMUNROK.Gyeonu.DebugWalkController>().eye = eye.transform;

            var strays = Object.FindObjectsByType<IMUNROK.Gyeonu.DebugAutoWalker>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var s in strays) Object.DestroyImmediate(s);   // 오토파일럿 잔존물 제거

            var mainCam = GameObject.Find("Main Camera");
            if (mainCam != null && mainCam != eye)
            {
                var lis = mainCam.GetComponent<AudioListener>();
                if (lis != null) lis.enabled = false;
                mainCam.SetActive(false);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[관측실 보행] 디버그 워커 설치 @ " + go.transform.position.ToString("F2"));
        }

        /// <summary>Play 자동 보행 경로 — 스폰 → 통로(꺾임 4·계단 2) → 사각 작업실 한 바퀴 →
        /// 붕괴 개구부 계단 내려갔다 오기 → 혼천의 주변 → 계단 4단 올라 돔 한 바퀴 → 복귀.
        /// 좌표는 빌더의 통로 상태기계·작업실 좌표계와 일치한다.</summary>
        public static readonly Vector3[] Route =
        {
            new Vector3(0f, 0f, 0.8f), new Vector3(0f, 0f, 9.0f),
            new Vector3(0.9f, 0f, 9.9f), new Vector3(3.4f, 0f, 9.9f),
            new Vector3(8.9f, -1.7f, 9.9f), new Vector3(9.8f, -1.7f, 10.8f),
            new Vector3(9.8f, -1.7f, 19.8f), new Vector3(10.7f, -1.7f, 20.7f),
            new Vector3(15.3f, -3.06f, 20.7f), new Vector3(17.3f, -3.06f, 20.7f),
            new Vector3(18.2f, -3.06f, 21.6f), new Vector3(18.2f, -3.06f, 28.0f),
            new Vector3(18.2f, -3.06f, 30.3f),                       // 작업실 진입 (10.0×7.0×3.60)
            new Vector3(18.6f, -3.06f, 30.6f), new Vector3(18.6f, -3.06f, 33.6f),  // 개구부 동쪽 통로
            new Vector3(16.9f, -3.06f, 35.4f),                       // 개구부 북쪽 통로
            new Vector3(15.2f, -3.06f, 35.5f),                       // 계단 어귀 (난간 트인 곳)
            new Vector3(15.2f, -3.24f, 34.4f), new Vector3(15.2f, -3.96f, 32.8f),  // 끊긴 데까지 내려가기
            new Vector3(15.2f, -3.06f, 35.5f),                       // 되올라오기
            new Vector3(18.9f, -3.06f, 35.2f),
            new Vector3(22.5f, -3.06f, 33.0f), new Vector3(23.6f, -3.06f, 31.4f),  // 혼천의 한 바퀴
            new Vector3(22.5f, -3.06f, 30.2f), new Vector3(21.3f, -3.06f, 31.4f),
            new Vector3(20.5f, -3.06f, 33.8f),                       // 계단 앞
            new Vector3(20.5f, -2.34f, 37.2f), new Vector3(20.5f, -2.34f, 39.0f),  // 아치 지나 돔 진입
            new Vector3(23.2f, -2.34f, 41.2f), new Vector3(20.5f, -2.34f, 44.0f),  // 돔 한 바퀴
            new Vector3(17.8f, -2.34f, 41.2f), new Vector3(20.5f, -2.34f, 38.0f),
            new Vector3(20.5f, -3.06f, 33.6f), new Vector3(18.2f, -3.06f, 30.2f),  // 복귀
        };

        [MenuItem("Tools/이문록/관측실 자동 보행 검증 준비")]
        public static void ArmAutoWalk()
        {
            InstallWalker();
            var go = GameObject.Find("디버그_워커");
            var auto = go.AddComponent<IMUNROK.Gyeonu.DebugAutoWalker>();
            auto.waypoints = Route;
            auto.speed = 2.6f;
            auto.fallY = -5.2f;   // 정상 최저 보행면 -3.96(끊긴 계단 끝) 아래, 갱바닥 -11.3 위
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.LogWarning($"[관측실 보행] 자동 보행 준비 — 웨이포인트 {Route.Length}개. Play 실행.\n" +
                             "⚠️ 검증이 끝나면 반드시 [관측실 자동 보행 해제]를 실행할 것 — " +
                             "DebugAutoWalker가 씬에 저장되면 WASD 수동 조작이 먹지 않는다.");
        }

        /// <summary>오토파일럿 해제 — 수동 WASD 조작으로 되돌린다 (검증 후 필수).</summary>
        [MenuItem("Tools/이문록/관측실 자동 보행 해제")]
        public static void DisarmAutoWalk()
        {
            var strays = Object.FindObjectsByType<IMUNROK.Gyeonu.DebugAutoWalker>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var s in strays) Object.DestroyImmediate(s);
            int revived = 0;
            foreach (var w in Object.FindObjectsByType<IMUNROK.Gyeonu.DebugWalkController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!w.enabled) { w.enabled = true; revived++; }   // AutoWalker가 꺼둔 것 복구
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[관측실 보행] 자동 보행 해제 — AutoWalker 제거 {strays.Length}, 수동 컨트롤러 복구 {revived}");
        }

        static void Box(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
        }

        static void Box(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rot, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.AddComponent<BoxCollider>().size = size;
            go.layer = layer;
            go.isStatic = true;
        }
    }
}
