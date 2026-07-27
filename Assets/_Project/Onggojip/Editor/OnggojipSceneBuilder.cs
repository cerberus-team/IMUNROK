using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IMUNROK.Common;
using IMUNROK.Onggojip;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 제1사건(옹고집전) 씬 생성기 — 2막 구조.
    ///   1부 "Onggojip"        : 옹씨댁, 밤/저녁, 잠행 (대문앞·마당행랑·사랑방)
    ///   2부 "Onggojip_Gwana"  : 관아, 아침, 공개조사 (문서고·동헌)
    /// 출도(마패)하면 1부 → 2부 씬으로 전환(되돌릴 수 없음). 단서는 공통 수첩에 유지됨.
    ///
    /// 메뉴: [이문록 ▸ 옹고집 사건 씬 생성 (1부+2부)].
    /// ★ 시작 레이아웃 생성용. 한 번 만든 뒤 손편집을 시작하면 재실행하지 말 것(덮어써짐).
    /// </summary>
    public static class OnggojipSceneBuilder
    {
        private const string Act1Path = "Assets/_Project/Onggojip/Scenes/Onggojip.unity";
        private const string Act2Path = "Assets/_Project/Onggojip/Scenes/Onggojip_Gwana.unity";
        private const string Act2SceneName = "Onggojip_Gwana";
        private const float ZoneGap = 12f;

        [MenuItem("이문록/옹고집 사건 씬 생성 (1부+2부)")]
        public static void BuildOnggojipScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder("Assets/_Project/Onggojip/Scenes");
            BuildAct1();
            BuildAct2();
            AddSceneToBuildSettings(Act1Path);
            AddSceneToBuildSettings(Act2Path);

            Debug.Log("[OnggojipSceneBuilder] 1부/2부 씬 생성 완료");
            EditorUtility.DisplayDialog("이문록",
                "옹고집 1부(Onggojip) · 2부(Onggojip_Gwana) 생성 완료!\n\n" +
                "조사청 제1사건 큐브 → 1부 로드.\n1부에서 출도(마패) → 2부로 전환.", "확인");
        }

        // ── 1부: 옹씨댁 (밤/저녁, 잠행) ──
        private static void BuildAct1()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting(intensity: 0.4f, color: new Color(0.55f, 0.6f, 0.75f), ambient: new Color(0.18f, 0.18f, 0.24f));
            BuildCamera();

            BuildZone(0, "1_대문앞",      new[] { "乙_진짜옹덕구", "옹씨댁_대문", "마을사람" });
            BuildZone(1, "2_마당·행랑",   new[] { "마름", "행랑채", "늙은하인" });
            BuildZone(2, "3_사랑방(밤)",  new[] { "甲_가짜", "문갑(J08)", "서안·벼루", "장부(J09)", "아궁이(J13)", "행랑궤(J15)" });

            BuildCase(OnggojipCase.Phase.Stealth, Act2SceneName);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Act1Path);
        }

        // ── 2부: 관아 (아침, 공개조사) ──
        private static void BuildAct2()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting(intensity: 1.1f, color: new Color(1f, 0.96f, 0.85f), ambient: new Color(0.5f, 0.5f, 0.55f));
            BuildCamera();

            BuildZone(0, "1_관아문서고",  new[] { "호적대장(G01)", "호구단자(G02)", "입안대장(G03)", "환곡대장(G04)" });
            BuildZone(1, "2_동헌(심문)",  new[] { "甲_자리", "乙_자리", "아내(G05)", "늙은하인_소환(G06)" });

            BuildCase(OnggojipCase.Phase.Revealed, Act2SceneName);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Act2Path);
        }

        // ── 공통 조각 ──
        private static void BuildLighting(float intensity, Color color, Color ambient)
        {
            var root = new GameObject("Environment");
            var lightGO = new GameObject("Sun");
            lightGO.transform.SetParent(root.transform);
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            RenderSettings.ambientLight = ambient;
        }

        private static void BuildCamera()
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0, 1.6f, -6f);
            camGO.transform.LookAt(new Vector3(0, 1f, 0));
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<DebugFlyCamera>();
            camGO.AddComponent<MouseRaySelector>();
            camGO.AddComponent<MouseInspector>();
        }

        private static void BuildZone(int index, string zoneName, string[] markers)
        {
            float x = index * ZoneGap;
            var zone = new GameObject($"Zone_{zoneName}");
            zone.transform.position = new Vector3(x, 0, 0);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(zone.transform);
            floor.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = $"[구역] {zoneName}";
            sign.transform.SetParent(zone.transform);
            sign.transform.localPosition = new Vector3(-3.2f, 1.2f, -3.2f);
            sign.transform.localScale = new Vector3(0.15f, 2.4f, 0.15f);

            for (int i = 0; i < markers.Length; i++)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m.name = markers[i];
                m.transform.SetParent(zone.transform);
                m.transform.localPosition = new Vector3(1.5f, 0.5f, -2f + i * 1.2f);
                m.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            }
        }

        private static void BuildCase(OnggojipCase.Phase startPhase, string act2SceneName)
        {
            var go = new GameObject("_OnggojipCase");
            var comp = go.AddComponent<OnggojipCase>();
            var so = new SerializedObject(comp);
            so.FindProperty("_startPhase").enumValueIndex = (int)startPhase;
            so.FindProperty("_act2SceneName").stringValue = act2SceneName;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 유틸 ──
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
