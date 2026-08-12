using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 등불 한 번에 설치:
    ///  ① 등불 FBX용 URP/Lit 재질 생성(BaseMap + 발광Emission → 스스로 빛나 보임)
    ///  ② 카메라에 "_등불"(모델 + Point Light) 장착, 크기 자동 맞춤
    ///  ③ LanternController 연결 → 도구벨트 '등불' 선택 또는 L키로 켜고 끔
    /// </summary>
    public static class LanternSetup
    {
        private const string Folder = "Assets/_Project/Art/Tools/Lantern";

        [MenuItem("이문록/연출: 등불 설치")]
        public static void Setup()
        {
            // 1) 등불 FBX 찾기
            var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { Folder });
            if (fbxGuids.Length == 0)
            {
                EditorUtility.DisplayDialog("이문록", $"{Folder} 에서 등불 FBX를 못 찾았어요.\nFBX가 그 폴더에 있는지 확인하세요.", "확인");
                return;
            }
            string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[0]);
            string basePng = fbxPath.Substring(0, fbxPath.Length - 4) + ".png";   // ..._texture.fbx → ..._texture.png
            string normalPng = fbxPath.Substring(0, fbxPath.Length - 4) + "_normal.png";

            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePng);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPng);

            // 노멀맵은 타입을 NormalMap으로(경고 방지)
            if (normalTex != null)
            {
                var ni = AssetImporter.GetAtPath(normalPng) as TextureImporter;
                if (ni != null && ni.textureType != TextureImporterType.NormalMap)
                {
                    ni.textureType = TextureImporterType.NormalMap;
                    ni.SaveAndReimport();
                }
            }

            // 2) 재질(URP/Lit + 발광)
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            string matPath = Folder + "/Lantern_Mat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader;
            if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
            mat.SetColor("_BaseColor", Color.white);
            if (normalTex != null) { mat.SetTexture("_BumpMap", normalTex); mat.EnableKeyword("_NORMALMAP"); }
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.2f) * 1.6f);   // 따뜻한 발광
            if (baseTex != null) mat.SetTexture("_EmissionMap", baseTex);
            EditorUtility.SetDirty(mat);

            // 3) 씬에 "_등불" 장착
            var scene = EditorSceneManager.GetActiveScene();
            var cam = Camera.main;

            var parent = GameObject.Find("_등불");
            if (parent == null) parent = new GameObject("_등불");
            if (cam != null) parent.transform.SetParent(cam.transform, false);
            parent.transform.localPosition = new Vector3(0.28f, -0.28f, 0.55f);
            parent.transform.localRotation = Quaternion.identity;

            // 기존 모델/빛 정리 후 새로 생성(순회 중 삭제 방지 위해 먼저 수집)
            var oldKids = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in parent.transform) oldKids.Add(child.gameObject);
            foreach (var k in oldKids) Object.DestroyImmediate(k);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "등불_모델";

            // 크기 자동 맞춤(약 30cm)로 스케일
            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxDim > 0.0001f) model.transform.localScale *= (0.4f / maxDim);
                // 재질 적용
                foreach (var r in rends)
                {
                    var arr = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                    r.sharedMaterials = arr;
                }
            }
            model.transform.SetParent(parent.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);   // 세우고 앞면이 플레이어를 향하게

            // 빛
            var lightGo = new GameObject("등불_빛");
            lightGo.transform.SetParent(parent.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.05f, 0.05f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.5f);
            light.range = 6f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;   // Quest 대비

            // 4) LanternController 연결
            var lc = parent.GetComponent<LanternController>() ?? parent.AddComponent<LanternController>();
            var so = new SerializedObject(lc);
            so.FindProperty("_model").objectReferenceValue = model;
            so.FindProperty("_light").objectReferenceValue = light;
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("이문록",
                "등불 설치 완료!\n\n" +
                "· 재질: Lantern_Mat (BaseMap + 발광)\n" +
                "· 씬: _등불 (카메라에 장착, 크기 자동 ≈30cm)\n" +
                "· 켜기: 도구벨트 '등불' 선택 또는 L키\n\n" +
                (cam == null ? "⚠ MainCamera를 못 찾아 월드 원점에 뒀어요. 카메라 밑으로 옮기세요.\n" : "") +
                "Play → L 눌러보세요. 위치·크기는 _등불 Transform에서 조절하면 됩니다. Ctrl+S.", "확인");
        }
    }
}
