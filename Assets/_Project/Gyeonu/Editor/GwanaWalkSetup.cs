using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static IMUNROK.Gyeonu.Editor.GwanaLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 씬 보행 세팅. 둘 다 멱등.
    ///  ① 보행 콜라이더 — 담장·외삼문(중앙 통로만 개방)·월대와 그 계단·씬 경계.
    ///     MeshCollider 금지(프로젝트 규칙) — 전부 Box. 렌더러 없는 콜라이더 전용 GO.
    ///  ② 디버그 워커 — SpawnPoint_FromVillage 에 DebugWalkController + DebugInteractor.
    ///     DebugAutoWalker 가 남아 있으면 제거한다(작업 흔적 금지).
    ///
    /// 보행 영역: 진입로 회랑(|x| ≤ 9.5, z −56~−34) + 담장 안 마당 + 월대.
    /// 담장 밖 평탄지(|x| 17~25)는 진입로 옆 경계로 막아 뒤로 돌아 들어갈 수 없다.
    /// </summary>
    public static class GwanaWalkSetup
    {
        const string RootName = "관아_보행콜라이더";
        // 담장 좌우 지터를 되돌렸으므로 폭도 실제 석축(밑동 1.04)에 맞춰 되돌린다.
        // 밑단 −1.0은 유지 — 담장 밖 지면에는 미세 기복이 남아 −0.3까지 내려간다.
        const float WallColW = 1.10f;
        const float WallColBottom = -1.0f;

        [MenuItem("Tools/이문록/관아 ▸ 보행 콜라이더 구축")]
        public static void BuildColliders()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);

            // ── 담장 (용마루 3.08, 밑동 폭 1.1) ──
            const float ov = 0.56f;
            float wallH = WallTop - WallColBottom;
            float wallC = (WallTop + WallColBottom) * 0.5f;
            Box(root, "담장_남서", new Vector3((-WallHalfX - ov - GateHalfW) * 0.5f, wallC, WallZS),
                new Vector3(WallHalfX + ov - GateHalfW, wallH, WallColW));
            Box(root, "담장_남동", new Vector3((WallHalfX + ov + GateHalfW) * 0.5f, wallC, WallZS),
                new Vector3(WallHalfX + ov - GateHalfW, wallH, WallColW));
            foreach (float s in new[] { -1f, 1f })
                Box(root, "담장_측", new Vector3(s * WallHalfX, wallC, (WallZS + WallZN) * 0.5f),
                    new Vector3(WallColW, wallH, WallZN - WallZS + 2f * ov));
            Box(root, "담장_북", new Vector3(0f, wallC, WallZN),
                new Vector3(2f * (WallHalfX + ov), wallH, WallColW));

            // ── 외삼문 ──
            // 기단 (상면 0.45가 통로 바닥)
            Box(root, "문_기단", new Vector3(0f, GateBaseTop - 0.75f, GateZ), new Vector3(2f * GateHalfW, 1.5f, 5.2f));
            // 앞뒤 계단 (3단 0.45 — 램프로 대체, 16°)
            foreach (float s in new[] { -1f, 1f })
                Ramp(root, "문_계단", new Vector3(0f, 0.24f, GateZ + s * 3.28f),
                     new Vector3(4.8f, 0.20f, 1.70f), s * 15f);
            // 기둥 8개
            foreach (float cx in new[] { -6.6f, -2.2f, 2.2f, 6.6f })
                foreach (float cz in new[] { -1.6f, 1.6f })
                    Box(root, "문_기둥", new Vector3(cx, 2.4f, GateZ + cz), new Vector3(0.55f, 4.0f, 0.55f));
            // 협문(닫힌 문짝) — 좌우 간은 통과 불가
            foreach (float s in new[] { -1f, 1f })
                Box(root, "문_협문", new Vector3(s * 4.40f, 1.85f, GateZ), new Vector3(3.92f, 2.8f, 0.35f));
            // 열려 있는 중앙 문짝 — ⚠️ 문짝 두께(0.14)만 막으면 문짝과 문선 사이에 폭 0.5m
            // 슬롯이 남아 캡슐(지름 0.6)이 그 구석에 몰려 빠져나오지 못한다(Play 검증에서 발생).
            // 문선 라인(±2.52)까지 한 덩어리로 채워 슬롯 자체를 없앤다. 통로 유효폭 3.6m.
            foreach (float s in new[] { -1f, 1f })
                Box(root, "문_열린문짝", new Vector3(s * 2.16f, 1.95f, GateZ + 0.62f), new Vector3(0.72f, 3.05f, 1.96f));

            // ── 월대 ──
            Box(root, "월대", new Vector3(0f, DaeTop - 2.0f, (DaeZ0 + DaeZ1) * 0.5f),
                new Vector3(2f * (DaeHalfX + 0.15f), 4.0f, DaeZ1 - DaeZ0 + 0.3f));
            // 계단 — 램프로 근사. rotX 음수라야 +Z(월대) 쪽 끝이 올라간다.
            // ⚠️ 길이를 여유롭게 잡고 중심을 경사면 위에 두면 상단이 월대 상면보다 솟아 턱이 된다
            //    (Play 검증에서 내려올 때 끼임 발생). 길이는 정확히 빗변, 중심은 판 두께 절반만큼 아래.
            float run = DaeZ0 - DaeStairZ0;
            float slope = Mathf.Atan2(DaeTop, run) * Mathf.Rad2Deg;      // 26.6°
            const float rampT = 0.30f;
            float sink = rampT * 0.5f * Mathf.Cos(slope * Mathf.Deg2Rad);
            Ramp(root, "월대_계단",
                 new Vector3(0f, DaeTop * 0.5f - sink, (DaeStairZ0 + DaeZ0) * 0.5f),
                 new Vector3(2f * DaeStairHalfX, rampT, Mathf.Sqrt(run * run + DaeTop * DaeTop)), -slope);
            // 소맷돌 (계단 옆 난간)
            foreach (float s in new[] { -1f, 1f })
                Box(root, "월대_소맷돌", new Vector3(s * (DaeStairHalfX + 0.22f), DaeTop * 0.65f, (DaeStairZ0 + DaeZ0) * 0.5f),
                    new Vector3(0.45f, 2.4f, run));

            // ── 씬 경계 ──
            // 지형이 평지라 예전처럼 낙차로 막을 수 없다. 대지+언덕길을 사각으로 두른다.
            // 남쪽 끝은 지면이 −9m까지 내려가므로 박스 밑단을 −16까지 내린다(밑으로 새는 것 방지)
            float zc = (PlayZS + PlayZN) * 0.5f, zl = PlayZN - PlayZS;
            foreach (float s in new[] { -1f, 1f })
                Box(root, "경계_측", new Vector3(s * PlayHalfX, 4f, zc), new Vector3(0.8f, 40f, zl));
            Box(root, "경계_북", new Vector3(0f, 4f, PlayZN), new Vector3(2f * PlayHalfX, 40f, 0.8f));
            Box(root, "경계_남", new Vector3(0f, 4f, PlayZS), new Vector3(2f * PlayHalfX, 40f, 0.8f));

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[관아] 보행 콜라이더 구축 — 담장 5, 외삼문 13, 월대 4, 경계 4");
        }

        [MenuItem("Tools/이문록/관아 ▸ 디버그 워커 설치")]
        public static void InstallWalker()
        {
            var oldWalker = GameObject.Find("디버그_워커");
            if (oldWalker != null) Object.DestroyImmediate(oldWalker);

            Vector3 spawn = new Vector3(0f, 0f, SpawnZ);
            float yaw = 0f;
            var marker = GameObject.Find("SpawnPoint_FromVillage");
            if (marker != null) { spawn = marker.transform.position; yaw = marker.transform.eulerAngles.y; }
            else Debug.LogWarning("[관아] SpawnPoint_FromVillage 없음 — 기본 위치에 설치");

            var go = new GameObject("디버그_워커");
            go.transform.SetPositionAndRotation(spawn + Vector3.up * 0.1f, Quaternion.Euler(0f, yaw, 0f));
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.6f;   // 동헌 기단(0.95) → 대청 마루(1.50) 단차 0.55 대응
            cc.slopeLimit = 50f;

            var eye = new GameObject("워커_카메라");
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            eye.tag = "MainCamera";
            var cam = eye.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            eye.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
               .renderPostProcessing = true;   // URP 신규 카메라는 기본 OFF — 켜야 색이 정상
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
            Debug.Log("[관아] 디버그 워커 설치: " + spawn.ToString("F2"));
        }

        /// <summary>DebugAutoWalker 는 검증용 임시 컴포넌트 — 씬에 남기지 않는다.</summary>
        [MenuItem("Tools/이문록/관아 ▸ 오토워커 제거")]
        public static void RemoveAutoWalkers()
        {
            int n = 0;
            foreach (var w in Object.FindObjectsByType<DebugAutoWalker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { Object.DestroyImmediate(w); n++; }
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log("[관아] DebugAutoWalker " + n + "개 제거");
            }
        }

        // ── 헬퍼 ──
        static void Box(GameObject root, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
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
