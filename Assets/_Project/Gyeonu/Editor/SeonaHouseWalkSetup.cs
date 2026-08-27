using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Gyeonu;
using static IMUNROK.Gyeonu.Editor.SeonaHouseLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 선아 집 보행 세팅. 전부 멱등. 프로젝트 규칙대로 **MeshCollider 금지 — 전부 Box**.
    ///
    /// 【바닥은 실측이 아니라 실물에서 뜬다】 이 집의 마루는 평평하지 않다 — 북서 1.07,
    ///    남동 0.92 로 15cm 기울어 있다. 평면 하나로 깔면 한쪽은 뜨고 한쪽은 묻힌다.
    ///    그래서 원본의 마루널 렌더러(SM_Floor01A/02A/02B) **하나마다 박스를 하나씩** 만들어
    ///    메시 바운즈를 그대로 쓴다. 판끼리 3~4cm씩 어긋나지만 stepOffset 아래라 걸리지 않는다.
    ///
    /// 【보행 구역】 대청 · 선아 방 · 아버지 방(+동쪽 골방) 셋뿐이다.
    ///    툇마루 · 부엌 · 동남 골방은 담장 밖처럼 **가두어 막는다** — 마당·지형을 만들지 않기로
    ///    했으므로(사용자 지시) 툇마루로 나가면 허공이 보인다. 남면 창호문은 닫힌 채 고정이고
    ///    그 뒤에 안전용 벽을 한 겹 더 둔다.
    ///
    /// ⚠️ **stepOffset 0.35** — 문간 헤드룸은 문제없다. 칸막이 문틀·상부벽에 콜라이더를 달지
    ///    않았기 때문이다(SeonaHouseBuilder 참조). 문짝만 막으므로 열면 위가 통째로 비어 있다.
    /// </summary>
    public static class SeonaHouseWalkSetup
    {
        const float WallBottom = 0.30f;   // 마루(≈1.0) 아래까지 내려 틈을 없앤다

        [MenuItem("Tools/이문록/선아 집 ▸ ③ 보행 콜라이더 구축")]
        public static void BuildColliders()
        {
            if (SceneManager.GetActiveScene().name != SceneName)
            { Debug.LogError("[선아집] " + SceneName + " 씬에서 실행하세요"); return; }

            var root = SeonaHouseBuilder.Recreate(WalkRoot).transform;
            int floors = BuildFloors(root);
            BuildFence(root);
            RigBackDoor();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[선아집] ③ 보행 콜라이더 — 바닥판 " + floors + " + 울타리 " +
                      (root.childCount - 1) + "면 (전부 Box)");
        }

        // ══════════════════════════════════════════════════════
        // 바닥 — 원본 마루널마다 박스 하나
        // ══════════════════════════════════════════════════════
        static int BuildFloors(Transform root)
        {
            var house = GameObject.Find(HouseRoot);
            if (house == null) { Debug.LogError("[선아집] 본채가 없습니다 — 「① 씬 조립」을 먼저"); return 0; }

            var group = new GameObject("바닥");
            group.transform.SetParent(root, false);

            int n = 0;
            foreach (var mf in house.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                bool isFloor = mf.name.StartsWith("SM_Floor");
                // 기단(흙·돌바닥)은 마루에서 40cm 아래다. 보행 구역은 아니지만 **안전 그물**로 깐다 —
                // 울타리에 틈이 생겨도 허공으로 떨어지지 않는다.
                bool isGidan = mf.name.StartsWith("SM_Gidan");
                if (!isFloor && !isGidan) continue;

                var go = new GameObject((isFloor ? "마루_" : "기단_") + n);
                go.transform.SetParent(group.transform, false);
                // ⚠️ **스케일까지 복사해야 한다.** 마루널 13장이 전부 같은 `SM_Floor01A` 메시를
                //    쓰면서 인스턴스마다 스케일이 다르다. 위치·회전만 맞추면 콜라이더가 전부
                //    원본 메시 크기(2.48 × 1.26)로 깔려 실제 마루(최대 2.84 × 2.64)보다 작아진다 —
                //    Play 검증에서 대청 한복판과 뒷문 문간에 **바닥이 없어** 기단(0.4m 아래)으로
                //    떨어지고 다시 못 올라와 STUCK이 4개 찍혔다. 부모가 무회전·무스케일이라
                //    lossyScale을 localScale에 그대로 넣으면 된다.
                go.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                go.transform.localScale = mf.transform.lossyScale;
                var lb = mf.sharedMesh.bounds;
                var col = go.AddComponent<BoxCollider>();
                // 아래로 두껍게 — 판끼리 3~4cm 어긋나도 틈이 생기지 않게
                col.center = new Vector3(lb.center.x, lb.center.y - 0.30f, lb.center.z);
                col.size = new Vector3(lb.size.x, lb.size.y + 0.60f, lb.size.z);
                n++;
            }
            return n;
        }

        // ══════════════════════════════════════════════════════
        // 울타리 — 보행 구역 경계
        // ══════════════════════════════════════════════════════
        static void BuildFence(Transform root)
        {
            // ── 선아 방 (서쪽 방 + 그 북칸) ──────────────────
            Wall(root, "선아방_서벽", -6.20f, -6.13f, -3.40f, 2.60f);
            Wall(root, "선아방_남벽", -6.20f, -1.40f, -1.51f, -1.44f);   // 툇마루 차단
            Wall(root, "선아방_북벽", -6.20f, -1.40f, 2.40f, 2.48f);
            // 동쪽 — 몸채 구간은 새 칸막이(문짝)가 막는다. 북칸 구간만 벽을 세우되
            // 원본 문(SM_Door03A, z 1.09~1.81)만 비워 대청과 잇는다.
            Wall(root, "선아방_동벽_남", -1.47f, -1.40f, 0.95f, BackDoorZ0);
            Wall(root, "선아방_동벽_북", -1.47f, -1.40f, BackDoorZ1, 2.48f);

            // ── 대청 (중앙 칸 전체) ─────────────────────────
            // 남·북면은 창호문/벽 부재가 이미 막지만, 스폰 자리라 안전용으로 한 겹 더 둔다.
            Wall(root, "대청_남벽", -1.40f, 1.28f, -1.53f, -1.46f);
            Wall(root, "대청_북벽", -1.40f, 1.28f, 2.38f, 2.46f);
            Wall(root, "대청_동벽_북", 1.28f, 1.35f, 0.88f, 2.46f);      // 부엌 쪽 (원본 Wall01C)

            // ── 아버지 방 + 동쪽 골방 ───────────────────────
            Wall(root, "아버지방_남벽", 1.35f, 4.10f, -1.67f, -1.60f);
            Wall(root, "아버지방_북벽", 1.35f, 6.12f, 0.76f, 0.86f);     // 부엌 차단
            Wall(root, "골방_서벽", 4.02f, 4.09f, -3.35f, -1.60f);       // 툇마루 차단
            Wall(root, "골방_남벽", 4.02f, 6.12f, -3.29f, -3.22f);
            Wall(root, "골방_동벽", 6.04f, 6.12f, -3.35f, 0.86f);
        }

        static void Wall(Transform root, string name, float x0, float x1, float z0, float z1)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            if (z0 > z1) (z0, z1) = (z1, z0);
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (WallBottom + WallTop) * 0.5f, (z0 + z1) * 0.5f);
            go.AddComponent<BoxCollider>().size =
                new Vector3(x1 - x0, WallTop - WallBottom, z1 - z0);
        }

        // ══════════════════════════════════════════════════════
        // 대청 ↔ 선아 방 뒷문 (원본 부재를 여닫이로)
        // ══════════════════════════════════════════════════════
        /// <summary>
        /// 원본에 이미 달려 있는 외짝문(x −1.40, z 1.45)을 클릭 여닫이로 만든다.
        /// 대청 뒤에서 선아 방으로 가는 **두 번째 길**이라 동선이 한 줄로 늘어지지 않는다.
        /// 프리팹 인스턴스라 구조 변경은 못 하지만 **컴포넌트 추가·트랜스폼 오버라이드는 허용**되므로
        /// HingeDoor가 쓰는 "부모 좌표계 피벗 자체 회전" 방식이 그대로 먹는다(마을 담장 문과 동일).
        /// </summary>
        static void RigBackDoor()
        {
            var house = GameObject.Find(HouseRoot);
            if (house == null) return;

            Transform leaf = null;
            float best = float.MaxValue;
            foreach (Transform t in house.transform)
            {
                var r = t.GetComponentInChildren<MeshRenderer>(true);
                if (r == null || !r.name.StartsWith("SM_Door03A")) continue;
                var c = r.bounds.center;
                float d = Mathf.Abs(c.x + 1.38f) + Mathf.Abs(c.z - 1.45f);
                if (c.z < 1.0f || c.z > 1.9f) continue;      // 남벽 문들 제외
                if (d < best) { best = d; leaf = t; }
            }
            if (leaf == null) { Debug.LogWarning("[선아집] 대청 뒷문(SM_Door03A)을 못 찾음 — 통로가 뚫린 채 남는다"); return; }

            var mf = leaf.GetComponentInChildren<MeshFilter>();
            var col = leaf.GetComponent<BoxCollider>();
            if (col == null) col = leaf.gameObject.AddComponent<BoxCollider>();
            var lb = SeonaHouseBuilder.LocalMeshBounds(leaf, mf);
            col.center = lb.center; col.size = lb.size;

            var b = SeonaHouseBuilder.WorldBounds(leaf.gameObject);
            var hinge = leaf.GetComponent<HingeDoor>();
            if (hinge == null) hinge = leaf.gameObject.AddComponent<HingeDoor>();
            hinge.displayName = "뒷문";
            // 경첩 = 북쪽 끝단(+Z). 자유단(−Z)이 대청(+X)으로 열리게 −100°.
            hinge.pivotInParent = new Vector3(b.center.x, b.center.y, b.max.z);
            hinge.openAngle = -100f;
            hinge.duration = 0.8f;
            EditorUtility.SetDirty(hinge);
        }

        // ══════════════════════════════════════════════════════
        // 디버그 워커
        // ══════════════════════════════════════════════════════
        [MenuItem("Tools/이문록/선아 집 ▸ 디버그 워커 설치")]
        public static void InstallWalker()
        {
            // ⚠️ `GameObject.Find`는 **비활성 오브젝트를 못 찾는다** — 촬영하느라 워커를 꺼 둔
            //    상태에서 다시 설치했더니 씬에 워커가 둘이 됐다(실측). 비활성까지 훑어 지운다.
            foreach (var cc0 in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cc0.name == "디버그_워커") Object.DestroyImmediate(cc0.gameObject);

            Vector3 spawn = SpawnFromVillage;
            float yaw = SpawnYaw;
            var marker = GameObject.Find("SpawnPoint_FromVillage");
            if (marker != null) { spawn = marker.transform.position; yaw = marker.transform.eulerAngles.y; }

            var go = new GameObject("디버그_워커");
            go.transform.SetPositionAndRotation(spawn + Vector3.up * 0.15f, Quaternion.Euler(0f, yaw, 0f));
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;

            var eye = new GameObject("워커_카메라");
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            eye.tag = "MainCamera";
            var cam = eye.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.fieldOfView = 70f;
            eye.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
               .renderPostProcessing = true;   // URP 신규 카메라는 기본 OFF
            eye.AddComponent<AudioListener>();

            go.AddComponent<DebugWalkController>().eye = eye.transform;
            eye.AddComponent<DebugInteractor>();

            foreach (var n in new[] { "Main Camera", "검증카메라" })
            {
                var other = GameObject.Find(n);
                if (other == null || other == eye) continue;
                var lis = other.GetComponent<AudioListener>();
                if (lis != null) lis.enabled = false;
                other.SetActive(false);
            }

            RemoveAutoWalkers();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[선아집] 디버그 워커 설치: " + spawn.ToString("F2") + " yaw " + yaw);
        }

        /// <summary>자동 보행 검증 — 대청에서 시작해 두 방을 오가고 뒷문으로 돌아온다.</summary>
        [MenuItem("Tools/이문록/선아 집 ▸ 자동 보행 검증 준비")]
        public static void PrepareAutoWalk()
        {
            var walker = GameObject.Find("디버그_워커");
            if (walker == null) { Debug.LogError("[선아집] 디버그 워커가 없습니다 — 먼저 설치하세요"); return; }
            var auto = walker.GetComponent<DebugAutoWalker>();
            if (auto == null) auto = walker.AddComponent<DebugAutoWalker>();
            // ⚠️ DebugAutoWalker는 길찾기가 없어 **직선으로만** 간다. 가구를 가로지르면 STUCK이
            //    찍히는데 그건 레벨 결함이 아니다 — 빈 차선으로만 잇는다.
            //    칸막이 문은 **열려 있어야** 통과한다 (닫힌 채면 문 앞에서 STUCK).
            // ⚠️ 벽에 붙인 가구는 방 안쪽으로 0.5~0.6m 나와 있다. 벽을 따라 걷는 경로를 그리면
            //    가구 차단 박스에 갈려 STUCK이 찍힌다(1차에 선아 방 북벽을 따라 갔다가 그랬다).
            //    그래서 **방 한복판 빈 차선**으로만 잇는다.
            auto.waypoints = new[]
            {
                new Vector3(-0.06f, 1.00f, -1.02f),   // 스폰 (대청 남단)
                new Vector3(-0.06f, 1.00f,  1.90f),   // 대청 북단
                new Vector3(-1.00f, 1.00f,  1.45f),   // 뒷문 앞
                new Vector3(-2.00f, 1.05f,  1.30f),   // 뒷문 통과 → 선아 방
                new Vector3(-2.60f, 1.05f,  0.70f),   // 북벽 가구를 비껴 남쪽 차선으로
                new Vector3(-5.20f, 1.05f,  0.60f),   // 선아 방 서쪽
                new Vector3(-5.60f, 1.04f, -0.80f),   // 서남 구석
                new Vector3(-3.20f, 1.03f, -0.95f),   // 남쪽 차선
                new Vector3(-1.90f, 1.02f, -0.45f),   // 칸막이 문 앞 (선아 방 쪽)
                new Vector3(-0.60f, 1.00f, -0.45f),   // 대청으로 복귀
                new Vector3( 0.90f, 0.99f, -0.55f),   // 칸막이 문 앞 (대청 쪽) — 문 개구부 z −0.98~0.15
                new Vector3( 2.00f, 0.97f, -0.45f),   // 아버지 방 (서안은 남벽 z −0.95 쪽에 있다)
                new Vector3( 3.30f, 0.96f, -0.35f),   // 동단 — 문갑(북벽)과 책궤(z −1.25) 사이 차선
                new Vector3( 4.60f, 0.94f, -0.60f),   // 골방 입구
                new Vector3( 5.10f, 0.92f, -1.50f),   // 동쪽 골방
                new Vector3( 5.10f, 0.88f, -2.60f),   // 골방 남단
            };
            auto.fallY = -1.0f;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[선아집] 자동 보행 경로 " + auto.waypoints.Length + "점 — 검증 후 반드시 해제할 것");
        }

        /// <summary>DebugAutoWalker 는 검증용 임시 컴포넌트 — 씬에 남기면 WASD가 죽는다.</summary>
        [MenuItem("Tools/이문록/선아 집 ▸ 자동 보행 해제")]
        public static void RemoveAutoWalkers()
        {
            int n = 0;
            foreach (var w in Object.FindObjectsByType<DebugAutoWalker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { Object.DestroyImmediate(w); n++; }
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log("[선아집] DebugAutoWalker " + n + "개 제거");
            }
        }
    }
}
