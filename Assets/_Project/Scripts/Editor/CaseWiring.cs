using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>어사가 받는 넷을 다 채워 준다.</b>
    /// 메뉴: [이문록 ▸ 조사청 ▸ 사목 두기] · [이문록 ▸ 사건 ▸ 사건표 얹기]
    ///
    /// 고증으로는 봉서·사목·마패·유척 넷인데 이 게임에는 <b>사목만 없었다</b>.
    /// 그리고 사건 씬은 아무것도 쥐여 주지 않고 시작해서, 봉서에 뭐라 적혀
    /// 있었는지 다시 볼 데가 없었다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class CaseWiring
    {
        private const string HubPath = "Assets/_Project/Scenes/Core/HubScene.unity";
        private const string ScrollPrefab = "Assets/_Project/_Common/Prefabs/두루마리.prefab";

        // ── 사목 ──────────────────────────────────────

        [MenuItem("이문록/조사청/사목 두기")]
        public static void PlaceSamok()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[사목] 재생을 멈추고 다시 누르십시오.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != HubPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(HubPath, OpenSceneMode.Single);
            }

            var log = new System.Text.StringBuilder("[사목] 조사청에 두기\n");

            var already = Object.FindFirstObjectByType<Samok>(FindObjectsInactive.Include);
            if (already != null)
            {
                Debug.Log(log + "── 이미 있다: " + already.gameObject.name
                        + " (자리는 " + already.transform.position.ToString("F2") + ")\n"
                        + "   자리가 마음에 안 들면 인스펙터에서 옮기십시오 — 이 도구는 다시 안 놓습니다.");
                return;
            }

            // <b>자리는 사건 문서 곁에서 뽑는다.</b> 조사청 배치는 손으로 맞춰 둔
            // 것이라 방을 다시 짜면 안 된다. 이미 놓인 것을 기준 삼아 그 옆에
            // 한 자리만 얻는다 — 사목은 봉서와 함께 받는 것이니 곁에 있는 것이 맞다.
            var cubes = Object.FindObjectsByType<CaseCube>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (cubes == null || cubes.Length == 0)
            {
                Debug.LogWarning(log + "── 사건 문서를 못 찾아 자리를 못 잡는다.");
                return;
            }

            Vector3 mid = Vector3.zero;
            foreach (var c in cubes) mid += c.transform.position;
            mid /= cubes.Length;

            var host = new GameObject("사목");
            EditorSceneManager.MoveGameObjectToScene(host, scene);
            host.transform.position = mid + new Vector3(0f, -0.42f, 0f);
            host.transform.rotation = cubes[0].transform.rotation;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScrollPrefab);
            if (prefab != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, host.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // 눕혀 둔다
                model.transform.localScale = Vector3.one * 0.9f;
                foreach (var c in model.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(c);       // 짚는 자리는 껍데기 하나여야 한다
                log.AppendLine("── 두루마리를 얹었다");
            }
            else log.AppendLine("── 두루마리 프리팹이 없어 껍데기만 세웠다(아트는 공유 폴더)");

            var box = host.AddComponent<BoxCollider>();
            box.size = new Vector3(0.30f, 0.10f, 0.10f);

            host.AddComponent<Samok>();

            Selection.activeGameObject = host;
            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("── 자리 " + host.transform.position.ToString("F2")
                         + " (사건 문서 셋의 한가운데에서 42cm 아래)");
            log.AppendLine("   마음에 안 들면 인스펙터에서 옮기십시오 — 다시 눌러도 안 옮깁니다.");
            Debug.Log(log.ToString());
        }

        // ── 사건표 ────────────────────────────────────

        [MenuItem("이문록/사건/사건표 얹기")]
        public static void PlaceSheet()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[사건표] 재생을 멈추고 다시 누르십시오.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var log = new System.Text.StringBuilder("[사건표] " + scene.name + "\n");

            var already = Object.FindFirstObjectByType<CaseSheet>(FindObjectsInactive.Include);
            if (already != null)
            {
                Debug.Log(log + "── 이미 있다: " + already.gameObject.name);
                return;
            }

            GameObject host = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name.IndexOf("_") == 0 || root.name.IndexOf("State") >= 0) { host = root; break; }
            if (host == null)
            {
                host = new GameObject("_사건표");
                EditorSceneManager.MoveGameObjectToScene(host, scene);
                log.AppendLine("── 담을 데가 없어 '_사건표' 를 세웠다");
            }

            var sheet = Undo.AddComponent<CaseSheet>(host);
            log.AppendLine("── '" + host.name + "' 에 붙였다");

            // 이 씬이 어느 사건인지 짐작해 둔다. 틀리면 인스펙터에서 고치면 된다.
            var so = new SerializedObject(sheet);
            string n = scene.name;
            if (n.IndexOf("Onggojip") >= 0) Guess(so, CaseId.Case1_Onggojip, "제1사건 — 옹고집전", log);
            else if (n.IndexOf("Seocheon") >= 0) Guess(so, CaseId.Case2_Seocheon, "제2사건 — 서천꽃밭", log);
            else if (n.IndexOf("Gyeonu") >= 0) Guess(so, CaseId.Case3_Gyeonu, "제3사건 — 견우직녀", log);
            else log.AppendLine("   ⚠ 씬 이름으로 사건을 못 짚었다 — 인스펙터에서 골라 주십시오");
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = host;
            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("── 종이 그림은 인스펙터의 '펼칠 종이 그림' 에 꽂으십시오(T_Doc_Case…)");
            Debug.Log(log.ToString());
        }

        private static void Guess(SerializedObject so, CaseId id, string title, System.Text.StringBuilder log)
        {
            so.FindProperty("_caseId").enumValueIndex = (int)id;
            so.FindProperty("_title").stringValue = title;
            log.AppendLine("── 씬 이름으로 짚었다: " + title);

            // 구워 둔 봉서 그림이 있으면 같이 꽂아 준다 — 손으로 찾을 일을 하나 줄인다.
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D T_Doc_"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (p.IndexOf(id.ToString().Replace("Case1_", "Case1_")) < 0
                 && p.IndexOf(id.ToString()) < 0) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (tex == null) continue;
                so.FindProperty("_page").objectReferenceValue = tex;
                log.AppendLine("── 봉서 그림도 꽂았다: " + tex.name);
                break;
            }
        }
    }
}
