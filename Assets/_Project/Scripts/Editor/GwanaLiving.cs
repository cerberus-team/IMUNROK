using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>관아에 살림을 놓는다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑤ 살림 놓기]
    ///
    /// 관아 씬에는 집과 문서만 서 있고 <b>살림이 하나도 없었다</b> — 수첩도, 도구벨트도,
    /// 클릭을 받아 줄 EventSystem 도. 그래서 사람을 세워도 말을 걸 수가 없고, 대장을
    /// 읽어도 적을 데가 없다. 1막 껍데기(Onggojip.unity)가 들고 있는 것을 같은 꼴로
    /// 옮겨 놓는다.
    ///
    /// <b>왜 손으로 안 하고 도구로 만드나</b>: 관아는 앞으로 씬이 갈릴 수도 있고(문서고를
    /// 따로 떼는 이야기가 있다), 그때마다 이 살림을 다시 놓아야 한다. 손으로 놓으면
    /// 그때마다 무엇을 빠뜨렸는지 알 수 없다. 두 번 눌러도 두 벌이 안 서게 해 두면,
    /// 언제든 눌러서 <b>모자란 것만</b> 채울 수 있다.
    ///
    /// <b>안 만드는 것</b>: 자막(SubtitleView)·심문판(InterrogationPanel)·수첩 알맹이
    /// (Journal)·게임상태(GameState)는 <b>저 혼자 태어난다</b>. 처음 불릴 때 제가
    /// 오브젝트를 지어 붙으므로 씬에 미리 둘 것이 없다 — 미리 두면 오히려 두 벌이 된다.
    /// </summary>
    public static class GwanaLiving
    {
        private const string JournalName = "수첩(JournalView)";
        private const string HudName = "_HUD";
        private const string FontPath = "Assets/_Project/_Common/Art/Fonts/ChosunCentennial_otf.otf";

        private const string LanternTool = "Assets/_Project/Data/Tools/Tool_lantern.asset";
        private const string MagnifyTool = "Assets/_Project/Data/Tools/Tool_magnify.asset";
        private const string YucheokModel = "Assets/_Project/Art/Tools/유척/유척.obj";

        [MenuItem("이문록/관아/⑤ 살림 놓기")]
        public static void Place()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 지금 열린 씬이 관아가 아닙니다(" + scene.name + "). " +
                                 "Onggojip_Gwana 를 열고 다시 누르십시오.");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 살림을 놓았다\n");

            // ── 수첩 ──
            var journal = GameObject.Find(JournalName);
            if (journal == null)
            {
                journal = new GameObject(JournalName);
                Undo.RegisterCreatedObjectUndo(journal, "관아 살림");
                log.AppendLine("  · 수첩(JournalView) 을 세웠다");
            }
            var jv = journal.GetComponent<JournalView>();
            if (jv == null) jv = journal.AddComponent<JournalView>();
            SetIfNull(jv, "_font", AssetDatabase.LoadAssetAtPath<Font>(FontPath));

            // ── 도구벨트 ──
            //
            // 마패는 <b>안 얹는다</b>. 이미 내보이고 들어온 마당이라 다시 꺼낼 물건이
            // 아니고, 애초에 벨트에 사는 물건이 아니다(RoyalWarrant 가 품에서 꺼낸다).
            // 유척도 안 얹는다 — 관아에 들어설 때 ToolIssue 가 내준다.
            var hudGo = GameObject.Find(HudName);
            if (hudGo == null)
            {
                hudGo = new GameObject(HudName);
                Undo.RegisterCreatedObjectUndo(hudGo, "관아 살림");
                log.AppendLine("  · _HUD 를 세웠다");
            }
            var belt = hudGo.GetComponent<ToolbeltHud>();
            if (belt == null) belt = hudGo.AddComponent<ToolbeltHud>();
            if (hudGo.GetComponent<MapView>() == null) hudGo.AddComponent<MapView>();

            var beltSo = new SerializedObject(belt);
            var tools = beltSo.FindProperty("_tools");
            if (tools.arraySize == 0)
            {
                AddTool(tools, LanternTool);
                AddTool(tools, MagnifyTool);
                beltSo.ApplyModifiedProperties();
                log.AppendLine("  · 도구벨트에 등불·돋보기를 얹었다(유척은 들어설 때 받는다)");
            }

            // ── 누름을 받아 줄 것 ──
            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                // 입력 모듈은 <b>이름으로</b> 찾아 붙인다. 이 편집기 어셈블리는 새 입력
                // 꾸러미를 참조하지 않아, 형을 직접 적으면 컴파일이 안 된다.
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
                var moduleType = System.Type.GetType(
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (moduleType != null) es.AddComponent(moduleType);
                else es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(es, "관아 살림");
                log.AppendLine("  · EventSystem 을 세웠다");
            }

            // ── 손에 드는 유척 ──
            //
            // 벨트에 얹히기만 하고 <b>손에 아무것도 안 들리면</b> 도구를 받은 줄도 모른다.
            // 매단 자리는 카메라 밑에 둔다 — 자리와 크기는 HeldRig 가 공통으로 정한다.
            var cam = Camera.main;
            if (cam == null) log.AppendLine("  ※ 카메라를 못 찾아 유척 손자리는 못 만들었다");
            else
            {
                var holder = cam.transform.Find("_유척");
                if (holder == null)
                {
                    var src = AssetDatabase.LoadAssetAtPath<GameObject>(YucheokModel);
                    if (src == null) log.AppendLine("  ※ 유척 모델을 못 찾았다(" + YucheokModel + ") — 공유폴더에서 받으십시오");
                    else
                    {
                        var go = new GameObject("_유척");
                        go.transform.SetParent(cam.transform, false);
                        var model = (GameObject)PrefabUtility.InstantiatePrefab(src, go.transform);
                        model.name = "유척";
                        model.transform.localPosition = Vector3.zero;
                        model.transform.localRotation = Quaternion.identity;
                        foreach (var col in model.GetComponentsInChildren<Collider>(true))
                            UnityEngine.Object.DestroyImmediate(col);
                        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                        {
                            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            r.receiveShadows = false;
                        }
                        var htm = go.AddComponent<HeldToolModel>();
                        var hso = new SerializedObject(htm);
                        hso.FindProperty("_toolId").stringValue = "yucheok";
                        hso.FindProperty("_model").objectReferenceValue = model;
                        hso.FindProperty("_takeState").stringValue = "";
                        hso.ApplyModifiedProperties();
                        go.AddComponent<BrassRuler>();
                        HeldRig.Apply(go.transform, "yucheok", model.transform);
                        model.SetActive(false);
                        Undo.RegisterCreatedObjectUndo(go, "관아 살림");
                        log.AppendLine("  · 카메라 밑에 유척 손자리를 만들었다");
                    }
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        private static void AddTool(SerializedProperty list, string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<ToolDef>(path);
            if (def == null) { Debug.LogWarning("[관아] 도구를 못 찾았다: " + path); return; }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = def;
        }

        /// <summary>비어 있을 때만 채운다. 손으로 맞춰 둔 값을 덮지 않으려는 것이다.</summary>
        private static void SetIfNull(Object target, string field, Object value)
        {
            if (value == null) return;
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null || p.objectReferenceValue != null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
