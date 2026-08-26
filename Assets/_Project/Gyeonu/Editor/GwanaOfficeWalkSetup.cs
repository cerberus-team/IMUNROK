using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Gyeonu;
using static IMUNROK.Gyeonu.Editor.GwanaOfficeLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 집무실 보행 세팅. 전부 멱등. 프로젝트 규칙대로 **MeshCollider 금지 — 전부 Box**.
    ///
    /// 보행 구역 = 방 전체. 막는 것 = 벽 4면 · 기둥 · 가구(Furnisher가 따로 깐다) · 잠긴 비밀문.
    /// 비밀 계단은 문이 열린 뒤를 대비해 미리 깔아 둔다 (지금은 문 콜라이더가 막는다).
    ///
    /// ⚠️ **stepOffset 0.35** — 다른 씬(0.6)과 다르다. CC의 스텝 판정 상승 스윕이
    ///    `height + stepOffset` 만큼 헤드룸을 요구하는데(서고에서 실측), 이 방은 대들보 밑이
    ///    2.35m 라 0.6이면 1.8+0.6=2.4 를 요구해 보 밑에서 Sides 끼임이 난다.
    ///    계단 챌판이 0.205라 0.35로 충분하다.
    /// </summary>
    public static class GwanaOfficeWalkSetup
    {
        const string RootName = "집무실_보행콜라이더";
        const float WallTop = 3.20f;

        [MenuItem("Tools/이문록/관아 집무실 ▸ ③ 보행 콜라이더 구축")]
        public static void BuildColliders()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);

            // ── 바닥 ──
            Slab(root, "바닥", -HalfX, HalfX, FloorY, ZS, ZN);

            // ── 벽 4면 (심벽 목부재가 0.05 나와 있지만 그건 무시 — 벽면 하나로 막는다) ──
            Wall(root, "벽_서", -OutX, -HalfX, FloorY, WallTop, OutZS, OutZN);
            Wall(root, "벽_동", HalfX, OutX, FloorY, WallTop, OutZS, OutZN);
            Wall(root, "벽_남", -HalfX, HalfX, FloorY, WallTop, OutZS, ZS);
            // 북벽은 비밀문 개구부를 비운다 (문짝 콜라이더가 잠긴 동안 막는다)
            Wall(root, "벽_북_서", -HalfX, -SecretHalfX, FloorY, WallTop, ZN, OutZN);
            Wall(root, "벽_북_동", SecretHalfX, HalfX, FloorY, WallTop, ZN, OutZN);
            // ⚠️ 비밀문 상인방 콜라이더는 **눈에 보이는 문 높이(1.85)보다 높은 2.25**에서 시작한다.
            //    CC의 스텝 판정 상승 스윕이 `height+stepOffset` = 1.8+0.35 = 2.15 의 헤드룸을 요구해
            //    1.85 문간에서 Sides 끼임이 났다(실측 — 오토워커가 문 앞에서 못 넘어갔다).
            //    캡슐 자체는 1.8이라 문을 통과하고, 카메라도 1.7이라 시각적으로는 아무 차이가 없다.
            //    문을 2.15 이상으로 키우면 병풍(1.93)보다 높아져 **숨겨진 문이 삐져나온다** — 그래서
            //    형상은 그대로 두고 콜라이더만 비운다.
            Wall(root, "벽_북_상인방", -SecretHalfX, SecretHalfX, SecretTopY + 0.40f, WallTop, ZN, OutZN);

            // ── 기둥 8본 (벽면에서 방 안으로 0.155 나와 있다) ──
            foreach (float cx in ColXs)
                foreach (float cz in new[] { ZS, ZN })
                    Wall(root, "기둥", cx - ColR0, cx + ColR0, FloorY, ChangbangY1, cz - ColR0, cz + ColR0);

            // 비밀 통로 콜라이더는 **GwanaOfficeBuilder가 통로 형상과 같이** 만들어
            // `집무실_비밀통로/통로_콜라이더`에 둔다 (여기서 만들지 않는다 — 클래스 주석 참조).
            int pass = 0;
            var passGroup = GameObject.Find("집무실_비밀통로/통로_콜라이더");
            if (passGroup != null) pass = passGroup.transform.childCount;
            else Debug.LogWarning("[집무실] 통로 콜라이더가 없습니다 — 「① 씬 조립」을 먼저 실행하세요");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[집무실] ③ 보행 콜라이더 — 바닥 1 + 벽 6 + 기둥 8 (통로 " + pass + "개는 통로 그룹 소유)");
        }

        [MenuItem("Tools/이문록/관아 집무실 ▸ 디버그 워커 설치")]
        public static void InstallWalker()
        {
            var oldWalker = GameObject.Find("디버그_워커");
            if (oldWalker != null) Object.DestroyImmediate(oldWalker);

            Vector3 spawn = SpawnFromGwana;
            float yaw = 0f;
            var marker = GameObject.Find("SpawnPoint_FromGwana");
            if (marker != null) { spawn = marker.transform.position; yaw = marker.transform.eulerAngles.y; }

            var go = new GameObject("디버그_워커");
            go.transform.SetPositionAndRotation(spawn + Vector3.up * 0.1f, Quaternion.Euler(0f, yaw, 0f));
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;   // ⚠️ 대들보 밑 2.35m 헤드룸 — 클래스 주석 참조
            cc.slopeLimit = 50f;

            var eye = new GameObject("워커_카메라");
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            eye.tag = "MainCamera";
            var cam = eye.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            eye.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
               .renderPostProcessing = true;   // URP 신규 카메라는 기본 OFF
            eye.AddComponent<AudioListener>();

            go.AddComponent<DebugWalkController>().eye = eye.transform;
            eye.AddComponent<DebugInteractor>();

            var mainCam = GameObject.Find("Main Camera");
            if (mainCam != null && mainCam != eye)
            {
                var lis = mainCam.GetComponent<AudioListener>();
                if (lis != null) lis.enabled = false;
                mainCam.SetActive(false);
            }

            RemoveAutoWalkers();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[집무실] 디버그 워커 설치: " + spawn.ToString("F2") + " (stepOffset 0.35)");
        }

        /// <summary>자동 보행 검증 준비 — 방을 한 바퀴 돌고 병풍·비밀문 앞을 지난다.</summary>
        [MenuItem("Tools/이문록/관아 집무실 ▸ 자동 보행 검증 준비")]
        public static void PrepareAutoWalk()
        {
            var walker = GameObject.Find("디버그_워커");
            if (walker == null) { Debug.LogError("[집무실] 디버그 워커가 없습니다 — 먼저 설치하세요"); return; }
            var auto = walker.GetComponent<DebugAutoWalker>();
            if (auto == null) auto = walker.AddComponent<DebugAutoWalker>();
            auto.waypoints = new[]
            {
                // ⚠️ DebugAutoWalker는 **길찾기가 없어 직선으로만** 간다. 웨이포인트가 가구를
                //    가로지르면 차단 콜라이더에 갈려 STUCK이 찍힌다 — 레벨 결함이 아니다.
                //    그래서 빈 차선으로만 잇는다: 서안(x ±0.61, z 0.49~1.01)은 **동쪽 x=1.0 차선**으로
                //    돌아가고, 접힌 병풍(x −1.53~−0.57)이 있는 뒷벽 서측은 밟지 않는다.
                //    반닫이는 사용자가 270°로 돌려 둔 탓에 방 안쪽으로 더 나와 있어 x −1.75까지만.
                new Vector3( 0.00f, FloorY, -1.50f),   // 들어선 자리
                new Vector3(-1.75f, FloorY, -1.55f),   // 서남 구석
                new Vector3(-1.85f, FloorY,  0.60f),   // 서벽 격자창 앞
                new Vector3(-1.75f, FloorY,  1.50f),   // 서북 (반닫이 앞)
                // 방을 가로지르지 않고 남쪽으로 되내려와 동벽으로 — 서안 북쪽(z 1.31)과
                // 접힌 병풍 남단(z 1.36) 사이 틈이 5cm뿐이라 그리로는 못 지나간다
                new Vector3(-1.75f, FloorY, -0.20f),
                new Vector3(-1.30f, FloorY, -1.40f),   // 남서
                new Vector3( 1.30f, FloorY, -1.45f),   // 남동
                new Vector3( 1.90f, FloorY,  0.05f),   // 동벽 문갑 앞
                new Vector3( 1.85f, FloorY,  1.55f),   // 동북 구석
                new Vector3( 0.60f, FloorY,  1.55f),   // 병풍 앞 (접힌 병풍 x≤−0.57 을 비껴 동측)
                new Vector3( 0.05f, FloorY,  1.85f),   // 비밀문 앞
                // ── 비밀 통로 (문이 열려 있어야 통과한다 — 닫혀 있으면 여기서 STUCK) ──
                // 직선 하나뿐이라 웨이포인트도 셋이면 된다
                new Vector3( 0.00f, FloorY,   3.20f),   // 문간 → 통로
                new Vector3( 0.00f, -1.65f,   7.60f),   // 계단 내려선 뒤
                new Vector3( 0.00f, -1.65f,  12.05f),   // 통로 끝 (Exit_ToArchive)
            };
            auto.fallY = -3.5f;   // 통로가 -1.65까지 내려간다
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[집무실] 자동 보행 경로 " + auto.waypoints.Length + "점 — 검증 후 반드시 해제할 것");
        }

        /// <summary>DebugAutoWalker 는 검증용 임시 컴포넌트 — 씬에 남기면 WASD가 죽는다.</summary>
        [MenuItem("Tools/이문록/관아 집무실 ▸ 자동 보행 해제")]
        public static void RemoveAutoWalkers()
        {
            int n = 0;
            foreach (var w in Object.FindObjectsByType<DebugAutoWalker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { Object.DestroyImmediate(w); n++; }
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log("[집무실] DebugAutoWalker " + n + "개 제거");
            }
        }

        // ── 헬퍼 (min/max 코너 지정 — 방 구조는 중심·크기보다 이쪽이 읽힌다) ──
        static GameObject Slab(GameObject root, string name, float x0, float x1, float top, float z0, float z1)
            => Wall(root, name, x0, x1, top - 1.2f, top, z0, z1);

        static GameObject Wall(GameObject root, string name,
                               float x0, float x1, float y0, float y1, float z0, float z1)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            if (y0 > y1) (y0, y1) = (y1, y0);
            if (z0 > z1) (z0, z1) = (z1, z0);
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            go.AddComponent<BoxCollider>().size = new Vector3(x1 - x0, y1 - y0, z1 - z0);
            return go;
        }

        static void Ramp(GameObject root, string name, Vector3 center, Vector3 size, float rotX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(rotX, 0f, 0f);
            go.AddComponent<BoxCollider>().size = size;
        }
    }
}
