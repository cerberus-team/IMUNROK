using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>말하기를 씬에 붙인다.</b> 메뉴: [이문록 ▸ 소리 ▸ 말하기 붙이기]
    ///
    /// 헤드셋에서 마이크 단추를 눌러도 아무 일이 없던 까닭은 단추가 안 걸려서가 아니라
    /// <b>알아들을 물건이 씬에 없어서</b>였다. 말하기는 셋이 다 있어야 돈다:
    ///
    ///   ① <c>WitConfiguration</c> 에셋 — 사람이 [Meta ▸ Voice SDK ▸ Get Started] 에서 만든다
    ///   ② <c>AppDictationExperience</c> — 마이크를 듣고 글로 옮기는 것
    ///   ③ <c>MicInput</c> — 그 글을 이 게임의 심문으로 넘기는 것
    ///
    /// ①은 계정 창이 뜰 수 있어 사람이 눌러야 하고, ②③이 이 도구의 몫이다.
    ///
    /// <b>씬마다 하나씩 필요하다.</b> <see cref="MicInput"/> 은 씬을 건너 살아남지 않는다 —
    /// 심문이 벌어지는 씬(1막 옹고집, 2막 관아)에 각각 세운다.
    ///
    /// <b>형을 이름으로 찾는다.</b> 이 도구가 Voice SDK 를 직접 부르면 편집 어셈블리가
    /// 그 꾸러미에 매이고, 꾸러미를 빼는 순간 프로젝트가 안 열린다. 이름으로 찾아
    /// 붙이면 <b>없으면 없다고 말하고 끝날 뿐</b>이다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class VoiceSetup
    {
        private const string ConfigPath = "Assets/WitConfiguration.asset";
        private const string HostName = "_말하기";

        private static readonly string[] Scenes =
        {
            "Assets/_Project/Onggojip/Scenes/Onggojip.unity",
            "Assets/_Project/Onggojip/Scenes/Onggojip_Gwana.unity",
        };

        [MenuItem("이문록/소리/말하기 붙이기")]
        public static void Run()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigPath);
            if (cfg == null)
            {
                Debug.LogWarning("[말하기] " + ConfigPath + " 이 없습니다.\n" +
                                 "  [Meta ▸ Voice SDK ▸ Get Started] 에서 Built-In Models ▸ Korean 을 먼저 고르십시오.");
                return;
            }

            var dictType = FindType("Meta.WitAi.Dictation.AppDictationExperience", "AppDictationExperience");
            if (dictType == null)
            {
                Debug.LogWarning("[말하기] AppDictationExperience 형을 못 찾았습니다 — Voice SDK 가 안 들어와 있습니다.");
                return;
            }

            var log = new System.Text.StringBuilder("[말하기] 씬에 붙인다\n");
            log.AppendLine("── 설정 " + cfg.name + " (" + Lang(cfg) + ")");

            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var host = Find(scene, HostName);
                if (host == null)
                {
                    var go = new GameObject(HostName);
                    SceneManager.MoveGameObjectToScene(go, scene);
                    host = go.transform;
                }

                var dict = host.GetComponent(dictType) as MonoBehaviour;
                if (dict == null) dict = host.gameObject.AddComponent(dictType) as MonoBehaviour;

                // 설정을 물린다. 이 칸이 비면 마이크는 열리는데 아무 말도 안 돌아온다.
                var so = new SerializedObject(dict);
                var slot = so.FindProperty("runtimeConfiguration.witConfiguration");
                if (slot != null) { slot.objectReferenceValue = cfg; so.ApplyModifiedPropertiesWithoutUndo(); }
                else log.AppendLine("   ※ " + scene.name + ": 설정 칸을 못 찾았다");

                var mic = host.GetComponent<MicInput>();
                if (mic == null) mic = host.gameObject.AddComponent<MicInput>();
                var mso = new SerializedObject(mic);
                mso.FindProperty("_dictation").objectReferenceValue = dict;
                mso.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(dict); EditorUtility.SetDirty(mic);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine("── " + scene.name + " : " + HostName + " 세우고 설정을 물렸다");
            }
            Debug.Log(log.ToString());
        }

        private static string Lang(ScriptableObject cfg)
        {
            var p = new SerializedObject(cfg).FindProperty("_appInfo.lang");
            return p == null || string.IsNullOrEmpty(p.stringValue) ? "말 미상" : p.stringValue;
        }

        private static System.Type FindType(string full, string shortName)
        {
            var t = System.Type.GetType(full);
            if (t != null) return t;
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType(full);
                if (t != null) return t;
                foreach (var x in a.GetTypes()) if (x.Name == shortName) return x;
            }
            return null;
        }

        private static Transform Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;
            return null;
        }
    }
}
