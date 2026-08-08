using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 은하담 보행 세팅 (2026-08-07):
    /// ① 보행 콜라이더 구축 — 월정교 마루(실측 8.8 평탄, x ±46.5)·문루 계단 램프·문루 벽,
    ///    물가 투명 벽(수면 0.45 등고선 자동 추적, 풍영정 돌다리 입구만 개방),
    ///    돌다리 양측 난간, 풍영정 마루 가장자리, 씬 경계(±140).
    ///    MeshCollider 금지 — 전부 Box. 렌더러 없는 콜라이더 전용 GO(투명).
    /// ② 디버그 워커 설치 — SpawnPoint_PlayerStart에 DebugWalkController + 카메라.
    /// 둘 다 멱등. 씬 경계 ±140 근거: 중앙 지형 ±100 + 스커트 40m(랜드마크 x91·동쪽숲 대부분 포함,
    /// 안개 소실선 220 한참 안쪽).
    /// </summary>
    public static class EunhaDamWalkSetup
    {
        const string RootName = "은하담_보행콜라이더";
        const float DeckTop = 8.8f;          // 월정교 마루 (정점 히스토그램 실측)
        const float WalkBound = 140f;        // 씬 경계 반폭
        const float WaterLevel = 0.5f;

        [MenuItem("Tools/이문록/은하담 보행 콜라이더 구축")]
        public static void Build()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);

            // ── 월정교 마루 + 문루 계단 램프 (지형 7.02 ↔ 마루 8.8) ──
            Box(root, "다리_마루", new Vector3(0f, DeckTop - 0.1f, 0f), new Vector3(93f, 0.2f, 8.8f));
            Ramp(root, "문루계단_동", new Vector3(48f, 7.91f, 0f), new Vector3(3.6f, 0.14f, 6.5f), -30.7f);
            Ramp(root, "문루계단_서", new Vector3(-48f, 7.91f, 0f), new Vector3(3.6f, 0.14f, 6.5f), 30.7f);

            // ── 문루 벽 (통로 |z|<2.7 개방, 양옆 막음) ──
            foreach (float gx in new[] { 38.65f, -38.65f })
                foreach (float zs in new[] { 1f, -1f })
                    Box(root, "문루벽", new Vector3(gx, 9.7f, zs * 5.0f), new Vector3(4.2f, 5.4f, 4.6f));

            // ── 물가 투명 벽: 수면 등고선(h=0.45) 자동 추적, 8m 세그먼트 ──
            int waterWalls = 0;
            for (float z = -136f; z <= 136f; z += 8f)
            {
                foreach (float side in new[] { 1f, -1f })
                {
                    float xs = FindShore(side, z);
                    if (float.IsNaN(xs)) continue;
                    // 동안 돌다리 입구(z 38.5~40.7)는 개방
                    if (side > 0f && z > 35.9f && z < 44.1f)
                    {
                        Box(root, "물벽_돌다리남측", new Vector3(FindShore(1f, 37f), 1.5f, 37.2f), new Vector3(0.6f, 2.6f, 2.8f));
                        Box(root, "물벽_돌다리북측", new Vector3(FindShore(1f, 43f), 1.5f, 42.5f), new Vector3(0.6f, 2.6f, 3.6f));
                        waterWalls += 2;
                        continue;
                    }
                    Box(root, "물벽", new Vector3(xs, 1.5f, z), new Vector3(0.6f, 2.6f, 8.8f));
                    waterWalls++;
                }
            }

            // ── 풍영정 돌다리 양측 난간 (보행판 1.08 위, x -9~30.8 — 진입 램프 포함) ──
            Box(root, "돌다리난간_남", new Vector3(10.9f, 2.1f, 38.55f), new Vector3(41.6f, 2.2f, 0.15f));
            Box(root, "돌다리난간_북", new Vector3(10.9f, 2.1f, 40.65f), new Vector3(41.6f, 2.2f, 0.15f));

            // ── 풍영정 마루 가장자리 (동면=진입만 개방) ──
            Box(root, "정자벽_북", new Vector3(-13f, 2.3f, 46.2f), new Vector3(7.6f, 2.2f, 0.2f));
            Box(root, "정자벽_남", new Vector3(-13f, 2.3f, 33.8f), new Vector3(7.6f, 2.2f, 0.2f));
            Box(root, "정자벽_서", new Vector3(-16.7f, 2.3f, 40f), new Vector3(0.2f, 2.2f, 12.6f));

            // ── 씬 경계 (±140) ──
            Box(root, "경계_동", new Vector3(WalkBound, 3f, 0f), new Vector3(1f, 12f, 2f * WalkBound + 2f));
            Box(root, "경계_서", new Vector3(-WalkBound, 3f, 0f), new Vector3(1f, 12f, 2f * WalkBound + 2f));
            Box(root, "경계_북", new Vector3(0f, 3f, WalkBound), new Vector3(2f * WalkBound + 2f, 12f, 1f));
            Box(root, "경계_남", new Vector3(0f, 3f, -WalkBound), new Vector3(2f * WalkBound + 2f, 12f, 1f));

            // ── 스커트 지형 콜라이더 ON (경계 안 40m 폭은 걸을 수 있어야 함) ──
            int skirts = 0;
            foreach (var t in Terrain.activeTerrains)
                if (t.name.StartsWith("Terrain_Skirt_"))
                {
                    var col = t.GetComponent<TerrainCollider>();
                    if (col != null && !col.enabled) { col.enabled = true; skirts++; }
                }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[보행] 콜라이더 구축 완료 — 물벽 {waterWalls}, 스커트 콜라이더 ON {skirts}, 경계 ±{WalkBound}");
        }

        /// <summary>디버그 워커 — 활성 씬의 SpawnPoint_PlayerStart에 설치. 기존 Main Camera는 비활성화.</summary>
        [MenuItem("Tools/이문록/디버그 워커 설치 (활성 씬)")]
        public static void InstallWalker()
        {
            var oldWalker = GameObject.Find("디버그_워커");
            if (oldWalker != null) Object.DestroyImmediate(oldWalker);

            Vector3 spawn = Vector3.zero;
            float spawnYaw = 0f;
            var marker = GameObject.Find("SpawnPoint_PlayerStart");
            if (marker != null) { spawn = marker.transform.position; spawnYaw = marker.transform.eulerAngles.y; }
            else Debug.LogWarning("[보행] SpawnPoint_PlayerStart 없음 — 원점에 설치");

            var go = new GameObject("디버그_워커");
            go.transform.position = spawn + Vector3.up * 0.1f;
            go.transform.rotation = Quaternion.Euler(0f, spawnYaw, 0f);   // 마커 방향으로 시작
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.6f;   // 관아 언덕 박스 계단 최대 단차 0.52 대응
            cc.slopeLimit = 50f;

            var eyeGo = new GameObject("워커_카메라");
            eyeGo.transform.SetParent(go.transform, false);
            eyeGo.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            eyeGo.tag = "MainCamera";
            var cam = eyeGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            var urpCam = eyeGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = true;   // URP 신규 카메라는 기본 OFF — 켜야 색이 정상
            eyeGo.AddComponent<AudioListener>();

            var walker = go.AddComponent<DebugWalkController>();
            walker.eye = eyeGo.transform;
            eyeGo.AddComponent<DebugInteractor>();   // 임시 상호작용 입력 (화면 중앙 레이 + 좌클릭)

            // 기존 카메라 비활성 (VR 리그 교체 시 되살릴 것)
            var mainCam = GameObject.Find("Main Camera");
            if (mainCam != null && mainCam != eyeGo)
            {
                var lis = mainCam.GetComponent<AudioListener>();
                if (lis != null) lis.enabled = false;
                mainCam.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[보행] 디버그 워커 설치: " + spawn + " (씬 " + SceneManager.GetActiveScene().name + ")");
        }

        // ── 헬퍼 ──
        static void Box(GameObject root, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            var b = go.AddComponent<BoxCollider>();
            b.size = size;
        }

        static void Ramp(GameObject root, string name, Vector3 center, Vector3 size, float rotZ)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
            var b = go.AddComponent<BoxCollider>();
            b.size = size;
        }

        /// <summary>물가선 탐색: 해당 측에서 물 쪽으로 진행하며 지형이 0.45 아래로 처음 꺼지는 x.</summary>
        static float FindShore(float side, float z)
        {
            for (float d = 44f; d >= 2f; d -= 0.25f)
            {
                float x = side * d;
                float h = SampleH(x, z);
                if (float.IsNaN(h)) continue;
                if (h < 0.45f)
                {
                    // 아직 물 안 — 더 안쪽이 이미 물이면 여기서 시작. 물가 방향으로 스캔:
                    // d를 줄이며 처음 h<0.45가 나온 지점이 물가 바로 안쪽
                    return x;
                }
            }
            return float.NaN;
        }

        static float SampleH(float x, float z)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var p = t.transform.position; var s = t.terrainData.size;
                if (x >= p.x && x <= p.x + s.x && z >= p.z && z <= p.z + s.z)
                    return t.SampleHeight(new Vector3(x, 0f, z)) + p.y;
            }
            return float.NaN;
        }
    }
}
