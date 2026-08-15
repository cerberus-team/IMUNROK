using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 서고 보행 세팅 (2026-08-15, 멱등) — 마을·은하담·견우마을·관측실과 같은 방침.
    ///
    /// 구조 콜라이더(통로·서고·잔해)는 ArchiveBuilder가 렌더 박스 원장을 1:1 박스로
    /// 내보낸다 — 여기서 중복 생성하지 않는다. 갱 진입 차단판도 빌더 소관.
    ///
    /// 이 스크립트가 맡는 것:
    ///   ① 디버그 워커 설치 (SpawnPoint_FromGwanaOffice) — 다른 씬과 동일 구성:
    ///      디버그_워커 [CharacterController 1.8/0.3/slope50/step0.6 + DebugWalkController]
    ///        └ 워커_카메라 [Camera + AudioListener + DebugInteractor], localY 1.7
    ///   ② Play 자동 보행 검증 (통로 진입 → 서고 → 잔해 곁 → 꺾인 구석 선아 자리 → 복귀)
    ///
    /// ⚠️ DebugAutoWalker는 Awake에서 DebugWalkController를 꺼버린다 — 씬에 저장되면
    ///    WASD가 죽는다 (관측실에서 실제 발생). 검증 후 반드시 [서고 자동 보행 해제] 실행.
    /// </summary>
    public static class ArchiveWalkSetup
    {
        [MenuItem("Tools/이문록/서고 디버그 워커 설치 (관아 통로)")]
        public static void InstallWalker()
        {
            // 통합 이후 서고는 관측실 씬 안에 있다. 암문 쪽에서 시작하려면
            // [관측실 디버그 워커 설치](SpawnPoint_FromEunhaDam)를 대신 실행하면 된다 — 두 스폰 겸용
            if (SceneManager.GetActiveScene().name != "Gyeonu_Observatory")
            {
                Debug.LogError("[서고 보행] 활성 씬이 Gyeonu_Observatory가 아닙니다 (씬 통합 후)");
                return;
            }
            var old = GameObject.Find("디버그_워커");
            if (old != null) Object.DestroyImmediate(old);

            Vector3 spawn = new Vector3(43.01f, 0f, 40.9f);
            float yaw = 180f;
            var marker = GameObject.Find("SpawnPoint_FromGwana");
            if (marker != null) { spawn = marker.transform.position; yaw = marker.transform.eulerAngles.y; }
            else Debug.LogWarning("[서고 보행] SpawnPoint_FromGwana 없음 — 통로 입구 기본값 사용");

            var go = new GameObject("디버그_워커");
            go.transform.position = spawn + Vector3.up * 0.1f;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            // ⚠️ 0.6이면 CC가 스텝 판정용으로 캡슐을 0.6 올려 스윕한다 — 필요 헤드룸이 1.8+0.6=2.4가 되어
            //    꺾인 구석(천장 2.1)에 못 들어간다 (2026-08-15 실측: 구석 천장 콜라이더에 Sides 끼임).
            //    통로 계단 0.165·줄사다리 잔해 둔덕 0.14에는 0.35면 충분하다
            cc.stepOffset = 0.35f;
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
            eye.AddComponent<IMUNROK.Gyeonu.DebugInteractor>();
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
            Debug.Log("[서고 보행] 디버그 워커 설치 @ " + go.transform.position.ToString("F2"));
        }

        /// <summary>Play 자동 보행 경로 — 스폰 → 관아 통로(꺾임 5·계단 8단×5) → 동벽 문 →
        /// 은닉처 → 비밀문·줄사다리 잔해 아래 → 꺾인 구석(선아 자리) → 복귀.
        /// 통로 좌표는 빌더 상태기계와 일치 (모퉁이 중심을 찍는다). AutoWalker는 경로 탐색이 없다 —
        /// 줄사다리 잔해(15.7, 33.4)는 낮은 둔덕 콜라이더라 밟고 지나간다.</summary>
        public static readonly Vector3[] Route =
        {
            new Vector3(43.01f, 0f, 40.9f),                                     // 스폰
            new Vector3(43.01f, -1.32f, 33.44f),                                // 계단1 내려 T1 모퉁이
            new Vector3(35.03f, -2.64f, 33.44f),                                // 계단2 내려 T2 모퉁이
            new Vector3(35.03f, -3.96f, 25.66f),                                // 계단3 내려 T3 모퉁이
            new Vector3(27.05f, -5.28f, 25.66f),                                // 계단4 내려 T4 모퉁이
            new Vector3(27.05f, -6.60f, 32.60f),                                // 계단5 내려 T5 모퉁이
            new Vector3(22.0f, -6.60f, 32.60f),                                 // 동벽 문
            new Vector3(20.3f, -6.60f, 32.9f),                                  // 은닉처 (수령의 웅덩이 아래)
            new Vector3(18.4f, -6.60f, 34.3f),                                  // 동바리 보 아래
            new Vector3(15.7f, -6.60f, 33.0f),                                  // 비밀문 바로 아래 (줄사다리 잔해 곁)
            new Vector3(15.8f, -6.60f, 29.6f),                                  // 꺾인 구석 어귀 (리브 동쪽)
            new Vector3(14.5f, -6.60f, 28.15f),                                 // 선아 자리
            new Vector3(15.8f, -6.60f, 30.4f),                                  // 되돌아 나옴
            new Vector3(20.3f, -6.60f, 32.9f),                                  // 은닉처 복귀
        };

        [MenuItem("Tools/이문록/서고 자동 보행 검증 준비")]
        public static void ArmAutoWalk()
        {
            InstallWalker();
            var go = GameObject.Find("디버그_워커");
            if (go == null) return;
            var auto = go.AddComponent<IMUNROK.Gyeonu.DebugAutoWalker>();
            auto.waypoints = Route;
            auto.speed = 2.6f;
            auto.fallY = -8.2f;   // 정상 최저 보행면 -6.60 아래
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.LogWarning($"[서고 보행] 자동 보행 준비 — 웨이포인트 {Route.Length}개. Play 실행.\n" +
                             "⚠️ 검증이 끝나면 반드시 [서고 자동 보행 해제]를 실행할 것 — " +
                             "DebugAutoWalker가 씬에 저장되면 WASD 수동 조작이 먹지 않는다.");
        }

        /// <summary>워커 스폰 전환 — 이미 설치된 디버그_워커를 암문 통로 입구(위층)와
        /// 관아 통로 입구(아래층) 사이로 옮긴다. Play 시작 위치를 바꾸는 가장 빠른 방법:
        /// 메뉴 한 번 클릭 → 저장 없이 바로 Play. (워커가 없으면 관아 쪽으로 새로 설치)</summary>
        [MenuItem("Tools/이문록/워커 스폰 전환 (암문 ↔ 관아)")]
        public static void ToggleSpawn()
        {
            var w = GameObject.Find("디버그_워커");
            if (w == null) { InstallWalker(); return; }
            var eun = GameObject.Find("SpawnPoint_FromEunhaDam");
            var gwa = GameObject.Find("SpawnPoint_FromGwana");
            if (eun == null || gwa == null)
            {
                Debug.LogError("[워커 스폰] 마커가 없습니다 — [관측실 생성]과 [서고 생성]을 먼저 실행하세요");
                return;
            }
            // 지금 있는 곳에서 먼 쪽으로
            bool nearEun = (w.transform.position - eun.transform.position).sqrMagnitude
                         < (w.transform.position - gwa.transform.position).sqrMagnitude;
            var target = nearEun ? gwa.transform : eun.transform;
            w.transform.SetPositionAndRotation(target.position + Vector3.up * 0.1f,
                Quaternion.Euler(0f, target.eulerAngles.y, 0f));
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[워커 스폰] " + (nearEun ? "관아 통로 입구(아래층 서고 쪽)" : "암문 통로 입구(위층 관측실 쪽)") + "로 전환");
        }

        /// <summary>오토파일럿 해제 — 수동 WASD 조작으로 되돌린다 (검증 후 필수).</summary>
        [MenuItem("Tools/이문록/서고 자동 보행 해제")]
        public static void DisarmAutoWalk()
        {
            var strays = Object.FindObjectsByType<IMUNROK.Gyeonu.DebugAutoWalker>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var s in strays) Object.DestroyImmediate(s);
            int revived = 0;
            foreach (var w in Object.FindObjectsByType<IMUNROK.Gyeonu.DebugWalkController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!w.enabled) { w.enabled = true; revived++; }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[서고 보행] 자동 보행 해제 — AutoWalker 제거 {strays.Length}, 수동 컨트롤러 복구 {revived}");
        }
    }
}
