using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using IMUNROK.Common;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 대화(심문) 연출 한 번에 설치:
    ///  ① 비네트+피사계심도(DOF) 볼륨 프로파일 생성
    ///  ② 씬에 글로벌 볼륨(_연출_Volume) + ConversationView 배치·연결
    ///  ③ 메인 카메라 포스트프로세싱 ON
    /// 실행 후 Ctrl+S. 이 도구는 한 번만 쓰면 되니 나중에 지워도 됨.
    /// </summary>
    public static class OnggojipConversationSetup
    {
        private const string Folder = "Assets/_Project/Onggojip/Rendering";
        private const string ProfilePath = Folder + "/연출_Profile.asset";

        [MenuItem("이문록/연출: 대화 카메라·밤 분위기 설치")]
        public static void Setup()
        {
            // 1) 폴더 + 볼륨 프로파일
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project/Onggojip", "Rendering");

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // 비네트 — 가장자리를 어둡게 해 인물에 집중
            if (!profile.TryGet<Vignette>(out var vig)) vig = profile.Add<Vignette>(true);
            vig.active = true;
            vig.intensity.Override(0.30f);
            vig.smoothness.Override(0.5f);
            vig.color.Override(Color.black);

            // 피사계심도 — 먼 배경 흐리게(인물만 또렷). Quest 대비 Gaussian(가벼움)
            if (!profile.TryGet<DepthOfField>(out var dof)) dof = profile.Add<DepthOfField>(true);
            dof.active = true;
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(3.5f);
            dof.gaussianEnd.Override(14f);
            dof.gaussianMaxRadius.Override(1f);
            dof.highQualitySampling.Override(false);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // 2) 씬 글로벌 볼륨 + ConversationView
            var scene = EditorSceneManager.GetActiveScene();
            var volGo = GameObject.Find("_연출_Volume") ?? new GameObject("_연출_Volume");
            var vol = volGo.GetComponent<Volume>() ?? volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
            vol.weight = 0.12f;
            vol.priority = 10;

            var view = volGo.GetComponent<ConversationView>() ?? volGo.AddComponent<ConversationView>();
            var so = new SerializedObject(view);
            var pVol = so.FindProperty("_volume");
            if (pVol != null) pVol.objectReferenceValue = vol;

            // 3) 메인 카메라: 포스트프로세싱 ON + 대화 조명(등불) 자식 생성
            var cam = Camera.main;
            string camMsg;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(cam);

                // 카메라 자식에 은은한 Point Light("_대화조명") — 말 걸 때만 켜짐
                var lightT = cam.transform.Find("_대화조명");
                var lgo = lightT != null ? lightT.gameObject : new GameObject("_대화조명");
                lgo.transform.SetParent(cam.transform, false);
                lgo.transform.localPosition = new Vector3(0f, 0.15f, 0.3f);
                lgo.transform.localRotation = Quaternion.identity;
                var light = lgo.GetComponent<Light>() ?? lgo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.9f, 0.72f);   // 따뜻한 등불색
                light.range = 8f;
                light.intensity = 0f;                        // 평소 꺼짐(ConversationView가 조절)
                light.shadows = LightShadows.None;           // Quest 대비 그림자 끔

                var pLight = so.FindProperty("_talkLight");
                if (pLight != null) pLight.objectReferenceValue = light;

                camMsg = $"메인 카메라({cam.name}) Post Processing ON + 대화 조명 생성";
            }
            else
            {
                camMsg = "⚠ MainCamera 태그 카메라를 못 찾음 — 카메라 Post Processing/대화 조명은 수동 설정 필요";
            }

            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("이문록",
                "대화 연출 설치 완료!\n\n" +
                "· 볼륨 프로파일: " + ProfilePath + "\n" +
                "· 씬: _연출_Volume (비네트+DOF, ConversationView 연결)\n" +
                "· " + camMsg + "\n\n" +
                "Ctrl+S 저장 후 Play → 마을사람에게 말 걸어보세요.\n" +
                "(대화 시 화면이 인물로 당겨지고 배경이 흐려집니다)", "확인");
        }
    }
}
