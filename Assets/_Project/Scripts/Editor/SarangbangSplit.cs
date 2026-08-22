using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 사랑방을 씬째로 갈라 놓은 뒤에 남는 잔손. 메뉴: [이문록 ▸ 사랑방 ▸ …]
    ///
    /// 갈라 놓고 보니 손으로는 못 채우는 자리가 둘 있다.
    ///
    /// <b>하나 — 방 넓이</b>. 안팎을 가르는 선은 방바닥이 정하는데, 마당에 서 있는 동안
    /// 실내 씬은 올라와 있지도 않다. 그러니 실려 있는 값으로 판단해야 한다.
    /// 여기서 실내 씬을 잠깐 열어 방바닥을 재고 그 값을 <see cref="InteriorSceneSwap"/> 에 박는다.
    ///
    /// <b>둘 — 끊긴 참조</b>. 김명관고택이 마당 씬으로 떨어지면서 껍데기 씬에서 고택 안을
    /// 가리키던 참조가 넷 끊겼다. 그중 둘(중문)은 <see cref="YardBinder"/> 가 씬이 올라올 때마다
    /// 이름으로 잇는다. 그 잇는 이를 씬에 세우고 물려 주는 것이 여기 일이다.
    /// 나머지 둘(사랑채_원본·김명관고택)은 이제 쓰지 않으므로 이을 것이 없다.
    ///
    /// ★두 메뉴 다 씬을 고친다. 끝나면 저장까지 한다.
    /// </summary>
    public static class SarangbangSplit
    {
        private const string ShellPath = "Assets/_Project/Onggojip/Scenes/Onggojip.unity";
        private const string YardPath = "Assets/_Project/Onggojip/Scenes/Onggojip_마당.unity";
        private const string RoomPath = "Assets/_Project/Onggojip/Scenes/Onggojip_사랑방.unity";
        private const string FloorName = "장판바닥";
        private const string BinderName = "_마당_잇기";

        [MenuItem("이문록/사랑방/방 넓이 재기")]
        public static void MeasureRoom()
        {
            var swap = Object.FindFirstObjectByType<InteriorSceneSwap>();
            if (swap == null) { Debug.LogError("[사랑방] 껍데기 씬에 InteriorSceneSwap 이 없습니다. " + ShellPath + " 를 여십시오."); return; }

            bool wasOpen = SceneManager.GetSceneByPath(RoomPath).isLoaded;
            var room = wasOpen ? SceneManager.GetSceneByPath(RoomPath)
                               : EditorSceneManager.OpenScene(RoomPath, OpenSceneMode.Additive);

            Transform floor = null;
            foreach (var root in room.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == FloorName) { floor = t; break; }

            if (floor == null)
            {
                Debug.LogError("[사랑방] 실내 씬에서 '" + FloorName + "' 을 못 찾았습니다.");
                if (!wasOpen) EditorSceneManager.CloseScene(room, true);
                return;
            }

            var rs = floor.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0)
            {
                Debug.LogError("[사랑방] '" + FloorName + "' 에 렌더러가 없어 넓이를 못 잽니다.");
                if (!wasOpen) EditorSceneManager.CloseScene(room, true);
                return;
            }
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

            Undo.RecordObject(swap, "방 넓이 재기");
            swap.RoomBox = b;
            EditorUtility.SetDirty(swap);
            EditorSceneManager.MarkSceneDirty(swap.gameObject.scene);
            EditorSceneManager.SaveScene(swap.gameObject.scene);

            if (!wasOpen) EditorSceneManager.CloseScene(room, true);

            Debug.Log("[사랑방] 방 넓이를 재어 박았습니다.\n  가운데 " + b.center.ToString("F3")
                      + " · 넓이 " + b.size.x.ToString("F2") + " x " + b.size.z.ToString("F2") + "m");
        }

        [MenuItem("이문록/사랑방/끊긴 참조 잇는 이 세우기")]
        public static void SetUpBinder()
        {
            var shell = EditorSceneManager.GetActiveScene();
            if (shell.path != ShellPath) { Debug.LogError("[사랑방] " + ShellPath + " 를 열고 실행하십시오."); return; }

            var go = GameObject.Find(BinderName);
            if (go == null)
            {
                go = new GameObject(BinderName);
                Undo.RegisterCreatedObjectUndo(go, "마당 잇는 이");
            }
            var binder = go.GetComponent<YardBinder>();
            if (binder == null) binder = Undo.AddComponent<YardBinder>(go);

            var so = new SerializedObject(binder);
            so.FindProperty("_teleport").objectReferenceValue = Object.FindFirstObjectByType<TeleportZone>();

            var gap = GameObject.Find("甲_가짜");
            so.FindProperty("_gap").objectReferenceValue = gap != null ? gap.GetComponent<BokdongController>() : null;
            so.ApplyModifiedProperties();

            var sb = new StringBuilder();
            sb.AppendLine("  순간이동 구역 = " + (so.FindProperty("_teleport").objectReferenceValue != null ? "이음" : "✘ 못 찾음"));
            sb.AppendLine("  甲(복동) = " + (so.FindProperty("_gap").objectReferenceValue != null ? "이음" : "✘ 못 찾음"));

            AddToBuild(YardPath, sb);

            EditorSceneManager.MarkSceneDirty(shell);
            EditorSceneManager.SaveScene(shell);
            Selection.activeGameObject = go;
            Debug.Log("[사랑방] 잇는 이를 세웠습니다.\n" + sb);
        }

        /// <summary>마당 씬이 빌드 목록에 없으면 넣는다. 없으면 실행 중에 못 불러온다.</summary>
        private static void AddToBuild(string path, StringBuilder sb)
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list)
                if (s.path == path)
                {
                    if (!s.enabled) { s.enabled = true; EditorBuildSettings.scenes = list.ToArray(); sb.AppendLine("  빌드 목록: " + path + " 를 켰습니다."); }
                    else sb.AppendLine("  빌드 목록: 이미 들어 있습니다.");
                    return;
                }
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
            sb.AppendLine("  빌드 목록에 " + path + " 를 넣었습니다.");
        }
    }
}
