using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 씬에 어두운 360도 스카이박스를 적용하는 공용 헬퍼(VR 배경 규칙 대응).
    /// 카메라 배경을 단색 대신 스카이박스로 바꿔, VR에서 사방을 둘러봐도 배경이 채워지게 한다.
    ///
    /// - 어두운 스카이박스 머티리얼을 (없으면) 만들어 재사용.
    /// - 조명은 코드가 제어하는 ambientLight(Flat)를 유지 → 기존 세계 열림 연출 안 깨짐.
    /// - 실제 360 이미지가 준비되면 이 머티리얼을 Panoramic 셰이더로 교체하면 됨.
    /// </summary>
    public static class AtmosphereSetup
    {
        private const string MaterialPath = "Assets/_Project/Art/Materials/DarkSkybox.mat";

        public static void ApplyDarkSkybox(Camera cam)
        {
            var mat = LoadOrCreateSkybox();

            RenderSettings.skybox = mat;
            // 조명은 코드가 넣는 ambientLight(색상)로 계속 제어 → 세계 열림 lerp 유지
            RenderSettings.ambientMode = AmbientMode.Flat;

            if (cam != null)
                cam.clearFlags = CameraClearFlags.Skybox;
        }

        private static Material LoadOrCreateSkybox()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            EnsureFolder("Assets/_Project/Art/Materials");

            var shader = Shader.Find("Skybox/Procedural");
            var mat = new Material(shader);
            // 어둡고 창백한 분위기(사건의 문턱)
            mat.SetFloat("_SunSize", 0f);
            mat.SetFloat("_SunSizeConvergence", 1f);
            mat.SetFloat("_AtmosphereThickness", 0.4f);
            mat.SetColor("_SkyTint", new Color(0.12f, 0.12f, 0.18f));
            mat.SetColor("_GroundColor", new Color(0.03f, 0.03f, 0.04f));
            mat.SetFloat("_Exposure", 0.45f);

            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AtmosphereSetup] 어두운 스카이박스 생성 → {MaterialPath}");
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
