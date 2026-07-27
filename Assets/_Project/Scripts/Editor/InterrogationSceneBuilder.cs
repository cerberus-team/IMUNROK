using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 심문 테스트 도구.
    ///  - 옹고집 샘플 심문 캐릭터 에셋을 (없으면) 만든다.
    ///  - 그 캐릭터로 심문할 수 있는 InterrogationScene을 만든다.
    /// 메뉴: [이문록 ▸ 심문 테스트 씬 생성 (InterrogationScene)]
    ///
    /// 오늘은 Mock(녹음테이프)로 동작. 내일 실제 AI를 붙이면 컨트롤러의 Backend만 바꾸면 됨.
    /// </summary>
    public static class InterrogationSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Core/InterrogationScene.unity";
        private const string CharacterPath = "Assets/_Project/Onggojip/Data/Sample_Onggojip_Interrogation.asset";

        [MenuItem("이문록/심문 테스트 씬 생성 (InterrogationScene)")]
        public static void BuildInterrogationScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var character = LoadOrCreateSampleCharacter();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 카메라(어두운 취조실 느낌)
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
            AtmosphereSetup.ApplyDarkSkybox(cam); // 360 배경(VR 규칙)
            camGO.AddComponent<AudioListener>();

            // 심문 컨트롤러 + 캐릭터 연결
            var go = new GameObject("_InterrogationController");
            var controller = go.AddComponent<InterrogationController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_character").objectReferenceValue = character;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder("Assets/_Project/Scenes/Core");
            EditorSceneManager.MarkSceneDirty(scene); // 연결 내용이 확실히 저장되도록
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"[InterrogationSceneBuilder] 심문 테스트 씬 생성 완료 → {ScenePath}");
            EditorUtility.DisplayDialog("이문록",
                "심문 테스트 씬 생성 완료!\n" + ScenePath +
                "\n\n샘플 캐릭터: " + CharacterPath, "확인");
        }

        private static InterrogationCharacter LoadOrCreateSampleCharacter()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InterrogationCharacter>(CharacterPath);
            if (existing != null) return existing;

            EnsureFolder("Assets/_Project/Onggojip/Data");

            var ch = ScriptableObject.CreateInstance<InterrogationCharacter>();
            ch.characterName = "옹고집(자칭)";
            ch.caseId = CaseId.Case1_Onggojip;
            ch.persona =
                "너는 스스로를 진짜 옹고집이라 우기는 인물이다. 사실은 도술로 만들어진 가짜(갑리)다. " +
                "어사의 심문에 능청스럽게 시치미를 뗀다. 하지만 결정적 증거를 제시받으면 마지못해 조금씩 실토한다. " +
                "조선시대 양반 말투('~하오', '소인')를 쓰고, 절대 먼저 진실을 말하지 않는다.";
            ch.openingLine = "어허, 어사또께서 예까지 웬일이시오? 소인이 바로 진짜 옹고집이올시다.";
            ch.evidenceGates = new List<EvidenceGate>
            {
                new EvidenceGate {
                    clueKey = "mole",
                    clueText = "옹고집의 점 위치가 다르다",
                    revealsInfo = "…어사또 눈썰미가 매섭구려. 진짜 옹고집은 왼뺨에 점이 있소. 헌데… 소인에겐 없구려.",
                },
                new EvidenceGate {
                    clueKey = "wife",
                    clueText = "부인이 두 사람을 구별하지 못함",
                    revealsInfo = "…부인조차 우리를 가리지 못했소. 그러니 소인인들 어찌 떳떳하다 하겠소.",
                },
            };

            AssetDatabase.CreateAsset(ch, CharacterPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[InterrogationSceneBuilder] 샘플 심문 캐릭터 생성 → {CharacterPath}");

            // 방금 만든 에셋을 "다시 불러와서" 반환 → 씬 컴포넌트에 확실히 연결되도록(GUID 등록 후)
            return AssetDatabase.LoadAssetAtPath<InterrogationCharacter>(CharacterPath);
        }

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
