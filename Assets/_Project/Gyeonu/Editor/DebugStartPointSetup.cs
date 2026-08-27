using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 검증용 시작 지점 — 설치 메뉴 + 자리 전환 메뉴 (2026-08-23).
    ///
    /// 씬을 단독으로 Play할 때 어디서 시작할지 고른다. 정식 스폰 마커(`SpawnPoint_*`)는
    /// 손대지 않는다 — 진입점 자리는 좌표를 베끼지 않고 마커 이름으로 참조한다
    /// (자세한 것은 <see cref="DebugStartPoint"/> 주석).
    ///
    /// 전환 메뉴는 **번호로** 동작한다. 이름은 관측실 기준으로 적어 두었지만, 다른 씬에
    /// DebugStartPoint를 놓아도 같은 순서대로 골라진다.
    /// </summary>
    public static class DebugStartPointSetup
    {
        const string Root = "Tools/이문록/디버그/";
        const string ObjName = "디버그_시작지점";
        const string ObservatoryScene = "Gyeonu_Observatory";

        // ── 설치 ──────────────────────────────────────────
        [MenuItem("Tools/이문록/관측실 검증용 시작 지점 설치", priority = 210)]
        public static void InstallObservatory()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != ObservatoryScene)
            {
                EditorUtility.DisplayDialog("검증용 시작 지점",
                    $"{ObservatoryScene} 씬을 연 뒤에 눌러라.\n(지금 열린 씬: {scene.name})", "알겠다");
                return;
            }

            var go = GameObject.Find(ObjName) ?? new GameObject(ObjName);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var sp = go.GetComponent<DebugStartPoint>() ?? go.AddComponent<DebugStartPoint>();

            sp.useOnPlay = true;
            sp.index = 0;                                  // 기본 = 관측실 작업실
            sp.spots = new[]
            {
                // ① 작업실 — 오른쪽에 혼천의(2.7m), 왼쪽 홍예 아치 너머로 돔의 혼상(14.7m).
                //    가운데에 table03(선아의 관측 수기가 놓인 상)이 들어온다. 바닥 −3.06.
                //    ⚠️ 처음엔 (21.10, 29.90) yaw 24로 잡았는데 혼천의가 화면 오른쪽에서 잘렸다 —
                //       2.15m에서 반각이 20°라 시야 반각(45.8°)을 넘겼다. 0.5m 물러서고
                //       4° 더 돌려 완전히 담았다 (2026-08-23 실측).
                new DebugStartPoint.Spot {
                    label = "관측실 작업실",
                    position = new Vector3(20.70f, -3.06f, 29.70f),
                    yaw = 28f, pitch = 3f,
                },
                // ② 암문 통로 입구 — 은하담에서 들어오는 정식 마커를 그대로 쓴다
                new DebugStartPoint.Spot {
                    label = "암문 통로 입구",
                    markerName = "SpawnPoint_FromEunhaDam",
                    position = new Vector3(0f, 0f, 0.80f), yaw = 0f, pitch = 0f,
                },
                // ③ 서고 관아 통로 입구 — 집무실에서 내려오는 정식 마커
                new DebugStartPoint.Spot {
                    label = "서고 관아 통로 입구",
                    markerName = "SpawnPoint_FromGwanaOffice",
                    position = new Vector3(43.01f, 0f, 40.90f), yaw = 180f, pitch = 0f,
                },
            };

            EditorUtility.SetDirty(sp);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = go;
            Debug.Log($"[검증 시작] '{ObjName}' 설치 — 기본 자리 「{sp.LabelOf(0)}」. " +
                      "바꾸려면 Tools ▸ 이문록 ▸ 디버그 ▸ 시작 지점 — …");
        }

        // ── 전환 ──────────────────────────────────────────
        [MenuItem(Root + "시작 지점 — 관측실 작업실", priority = 450)]
        static void Pick0() => Pick(0);
        [MenuItem(Root + "시작 지점 — 관측실 작업실", validate = true)]
        static bool Pick0V() => Validate(0, "시작 지점 — 관측실 작업실");

        [MenuItem(Root + "시작 지점 — 암문 통로 입구", priority = 451)]
        static void Pick1() => Pick(1);
        [MenuItem(Root + "시작 지점 — 암문 통로 입구", validate = true)]
        static bool Pick1V() => Validate(1, "시작 지점 — 암문 통로 입구");

        [MenuItem(Root + "시작 지점 — 서고 관아 통로 입구", priority = 452)]
        static void Pick2() => Pick(2);
        [MenuItem(Root + "시작 지점 — 서고 관아 통로 입구", validate = true)]
        static bool Pick2V() => Validate(2, "시작 지점 — 서고 관아 통로 입구");

        [MenuItem(Root + "시작 지점 — 쓰지 않음 (씬에 저장된 자리)", priority = 453)]
        static void PickOff()
        {
            var sp = Find();
            if (sp == null) return;
            sp.useOnPlay = false;
            Mark(sp);
            Debug.Log("[검증 시작] 끔 — 씬에 저장된 워커 자리에서 시작한다");
        }
        [MenuItem(Root + "시작 지점 — 쓰지 않음 (씬에 저장된 자리)", validate = true)]
        static bool PickOffV()
        {
            var sp = Find();
            Menu.SetChecked(Root + "시작 지점 — 쓰지 않음 (씬에 저장된 자리)", sp != null && !sp.useOnPlay);
            return sp != null;
        }

        static void Pick(int i)
        {
            var sp = Find();
            if (sp == null || i >= sp.Count) return;
            sp.useOnPlay = true;
            sp.index = i;
            Mark(sp);
            // Play 중이면 그 자리에서 바로 옮겨 준다 — 다시 시작할 것 없이 확인된다
            if (Application.isPlaying) sp.Apply();
            else Debug.Log($"[검증 시작] 다음 Play부터 「{sp.LabelOf(i)}」 에서 시작한다");
        }

        static bool Validate(int i, string path)
        {
            var sp = Find();
            Menu.SetChecked(path, sp != null && sp.useOnPlay && sp.index == i);
            return sp != null && i < sp.Count;
        }

        static DebugStartPoint Find() =>
            Object.FindFirstObjectByType<DebugStartPoint>(FindObjectsInactive.Include);

        static void Mark(DebugStartPoint sp)
        {
            EditorUtility.SetDirty(sp);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
