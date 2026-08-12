using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 밤 분위기 기본팩 한 번에 설치:
    ///  · 항상 켜지는 볼륨(_밤분위기_Volume): 블룸 + 색보정(차갑게·대비) + 톤매핑
    ///  · 달빛(차가운 디렉셔널) + 은은한 안개
    ///  · 메인 카메라 포스트프로세싱 ON
    /// 대화 연출 볼륨(_연출_Volume, DOF·비네트)과 별개로 함께 작동한다.
    /// </summary>
    public static class OnggojipNightAtmosphere
    {
        private const string Folder = "Assets/_Project/Onggojip/Rendering";
        private const string ProfilePath = Folder + "/밤분위기_Profile.asset";

        [MenuItem("이문록/연출: 밤 분위기 기본팩(블룸·색보정·달빛·안개)")]
        public static void Setup()
        {
            // 폴더 + 프로파일
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project/Onggojip", "Rendering");

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // 블룸 — 등불·창문 불빛이 은은히 번져 빛남
            if (!profile.TryGet<Bloom>(out var bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.96f, 0.88f));

            // 색보정 — 살짝 차갑고 대비↑, 채도↓ (밤 영화 톤)
            if (!profile.TryGet<ColorAdjustments>(out var ca)) ca = profile.Add<ColorAdjustments>(true);
            ca.active = true;
            ca.postExposure.Override(-0.15f);
            ca.contrast.Override(12f);
            ca.saturation.Override(-8f);
            ca.colorFilter.Override(new Color(0.86f, 0.91f, 1f));

            // 톤매핑 — 밝은 부분 자연스럽게
            if (!profile.TryGet<Tonemapping>(out var tm)) tm = profile.Add<Tonemapping>(true);
            tm.active = true;
            tm.mode.Override(TonemappingMode.Neutral);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // 항상 켜지는 글로벌 볼륨(대화 볼륨보다 우선순위 낮게)
            var volGo = GameObject.Find("_밤분위기_Volume") ?? new GameObject("_밤분위기_Volume");
            var vol = volGo.GetComponent<Volume>() ?? volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
            vol.weight = 1f;
            vol.priority = 5;

            // 안개 — 먼 가장자리를 가려 깊이·분위기
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.015f;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.10f);

            // 달빛 — 차갑고 은은한 디렉셔널
            var moonGo = GameObject.Find("_달빛") ?? new GameObject("_달빛");
            var moon = moonGo.GetComponent<Light>() ?? moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 0.92f);
            moon.intensity = 0.6f;
            moon.shadows = LightShadows.Soft;   // Quest 무거우면 None으로
            moonGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 카메라 포스트프로세싱 ON
            var cam = Camera.main;
            string camMsg = "⚠ MainCamera를 못 찾음 — 카메라 Post Processing을 직접 체크하세요";
            if (cam != null)
            {
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                EditorUtility.SetDirty(cam);
                camMsg = $"메인 카메라({cam.name}) Post Processing ON";
            }

            // 다른 밝은 디렉셔널 라이트가 있으면 알림(밤인데 낮 조명이면 씻겨나감)
            int otherDir = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.gameObject != moonGo && l.enabled && l.intensity > 0.9f)
                    otherDir++;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("이문록",
                "밤 분위기 기본팩 설치 완료!\n\n" +
                "· 볼륨: _밤분위기_Volume (블룸·색보정·톤매핑, 항상 켜짐)\n" +
                "· 달빛(_달빛) + 안개(Exp²)\n" +
                "· " + camMsg + "\n" +
                (otherDir > 0 ? $"\n⚠ 밝은 디렉셔널 라이트가 {otherDir}개 더 있어요. 밤이 씻겨 밝으면 그것들을 끄거나 어둡게 하세요.\n" : "") +
                "\nCtrl+S 저장 후 Play. 등불(L) 켜면 블룸으로 확 빛나 보일 거예요.\n" +
                "안개 진하면 Lighting 설정, 달빛 그림자 무거우면 _달빛 ▸ Shadow Type을 No Shadows로.", "확인");
        }
    }
}
