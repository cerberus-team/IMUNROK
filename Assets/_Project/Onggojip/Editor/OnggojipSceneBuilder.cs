using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IMUNROK.Common;
using IMUNROK.Onggojip;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 제1사건(옹고집전) 씬 생성기 — 2막 구조 + 살펴보기 단서 연결.
    ///   1부 "Onggojip"        : 옹씨댁, 밤/저녁, 잠행 (대문앞·마당행랑·사랑방)
    ///   2부 "Onggojip_Gwana"  : 관아, 아침, 공개조사 (문서고·동헌)
    ///
    /// 물증/문서 마커에는 InspectableNote가 붙어, 살펴보면(마우스로 가리키면)
    /// 정보가 뜨고 해당 단서(Jxx/Gxx)가 공통 수첩에 자동 기록된다.
    /// 인물(甲·아내·하인 등) 마커는 아직 프리미티브 — 심문 연결은 다음 단계(3b).
    ///
    /// 메뉴: [이문록 ▸ 옹고집 사건 씬 생성 (1부+2부)].
    /// ★ 시작 레이아웃 생성용. 손편집을 시작하면 재실행하지 말 것(덮어써짐).
    /// </summary>
    public static class OnggojipSceneBuilder
    {
        private const string Act1Path = "Assets/_Project/Onggojip/Scenes/Onggojip.unity";
        private const string Act2Path = "Assets/_Project/Onggojip/Scenes/Onggojip_Gwana.unity";
        private const string Act2SceneName = "Onggojip_Gwana";
        private const float ZoneGap = 12f;

        /// <summary>마커 하나. clueKey가 있으면 살펴보기(InspectableNote)로 그 단서를 기록.</summary>
        private struct Marker
        {
            public string name;
            public string clueKey;  // null이면 살펴보기 없음(인물 등)
            public string body;     // 살펴봤을 때 본문
        }
        private static Marker M(string name) => new Marker { name = name };
        private static Marker MI(string name, string key, string body) =>
            new Marker { name = name, clueKey = key, body = body };

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
                "옹고집 1부(Onggojip)·2부(Onggojip_Gwana) 생성 완료!\n\n" +
                "물증/문서를 마우스로 가리키면 단서가 수첩에 기록됩니다.\n" +
                "조사청 제1사건 큐브 → 1부. 출도(마패) → 2부.", "확인");
        }

        // ── 1부: 옹씨댁 (밤/저녁, 잠행) ──
        private static void BuildAct1()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting(0.4f, new Color(0.55f, 0.6f, 0.75f), new Color(0.18f, 0.18f, 0.24f));
            BuildCamera();

            BuildZone(0, "1_대문앞", new[]
            {
                M("乙_진짜옹덕구"), M("옹씨댁_대문"), M("마을사람"),
            });
            BuildZone(1, "2_마당·행랑", new[]
            {
                M("마름"), M("행랑채"), M("늙은하인"),
            });
            BuildZone(2, "3_사랑방(밤)", new[]
            {
                M("甲_가짜"),
                MI("문갑", "J08", "자물쇠가 부서지고 비어 있다. 스무 해 잠겨 있던 것이 최근 열렸다."),
                M("서안·벼루"),
                MI("장부", "J09", "필적이 한 달 전후로 뚜렷이 바뀌어 있다."),
                MI("아궁이", "J13", "타다 만 서찰 조각이 재 속에 남아 있다."),
                MI("행랑궤", "J15", "밑바닥에서 속량(贖良) 문서가 나온다."),
            });

            BuildCase(OnggojipCase.Phase.Stealth, Act2SceneName);
            SaveActive(Act1Path);
        }

        // ── 2부: 관아 (아침, 공개조사) ──
        private static void BuildAct2()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting(1.1f, new Color(1f, 0.96f, 0.85f), new Color(0.5f, 0.5f, 0.55f));
            BuildCamera();

            BuildZone(0, "1_관아문서고", new[]
            {
                MI("호적대장", "G01", "노 복동 — 왼팔 안쪽 데인 자국 두 치 남짓."),
                MI("호구단자", "G02", "노 복동 신미년 사망. 필체가 다르다."),
                MI("입안대장", "G03", "별급문기 사본 — 수취인 '종 복동'."),
                MI("환곡대장", "G04", "한 달간 소작료가 인하되어 있다."),
            });
            BuildZone(1, "2_동헌(심문)", new[]
            {
                M("甲_자리"), M("乙_자리"), M("아내"), M("늙은하인_소환"),
            });

            BuildCase(OnggojipCase.Phase.Revealed, Act2SceneName);
            SaveActive(Act2Path);
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

        private static void BuildZone(int index, string zoneName, Marker[] markers)
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
                var spec = markers[i];
                var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m.name = spec.name;
                m.transform.SetParent(zone.transform);
                m.transform.localPosition = new Vector3(1.5f, 0.5f, -2f + i * 1.2f);
                m.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

                if (!string.IsNullOrEmpty(spec.clueKey))
                    AddInspectable(m, spec.name, spec.body, spec.clueKey);
            }
        }

        /// <summary>마커에 살펴보기(InspectableNote)를 붙이고, 살펴보면 단서를 수첩에 기록하게 설정.</summary>
        private static void AddInspectable(GameObject go, string title, string body, string clueKey)
        {
            var note = go.AddComponent<InspectableNote>();
            var so = new SerializedObject(note);
            so.FindProperty("_title").stringValue = title;
            so.FindProperty("_body").stringValue = body;
            so.FindProperty("_recordClue").boolValue = true;
            so.FindProperty("_clueCase").enumValueIndex = (int)CaseId.Case1_Onggojip;
            so.FindProperty("_clueKey").stringValue = clueKey;
            so.FindProperty("_clueText").stringValue = $"[{clueKey}] {body}";
            so.ApplyModifiedPropertiesWithoutUndo();
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

        private static void SaveActive(string path)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
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
