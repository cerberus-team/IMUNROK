using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>어사가 받는 넷을 다 채워 준다.</b>
    /// 메뉴: [이문록 ▸ 사건 ▸ 사건표 얹기]
    ///
    /// 고증으로는 봉서·사목·마패·유척 넷이다. 사목은 한동안 조사청에 두었다가 걷었다 —
    /// 아래에 그 까닭을 적어 두었다.
    /// 그리고 사건 씬은 아무것도 쥐여 주지 않고 시작해서, 봉서에 뭐라 적혀
    /// 있었는지 다시 볼 데가 없었다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class CaseWiring
    {
        private const string HubPath = "Assets/_Project/Scenes/Core/HubScene.unity";
        private const string ScrollPrefab = "Assets/_Project/_Common/Prefabs/두루마리.prefab";

        // <b>사목을 걷었다.</b> 고증으로는 어사가 봉서·사목·마패·유척 넷을 받으므로
        // 조사청에 한 장 놓아 두었는데, 화면에서 하는 일이 없었다 — 집어 펴 볼 수는
        // 있으나 거기 적힌 것이 조사에 쓰이지 않았고, 상 위에서 자리만 차지했다.
        // 있어야 할 물건과 <b>일을 하는 물건</b>은 다르다.
        // (도구를 익힐 때 쥐여 주는 「연습_사목」은 딴 것이다. 그것은 그대로 있다.)

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
