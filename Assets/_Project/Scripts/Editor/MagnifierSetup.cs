using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 돋보기 한 번에 설치:
    ///  ① 돋보기 FBX용 URP/Lit 재질(BaseMap + Normal, 발광 없음)
    ///  ② 카메라에 "_돋보기"(모델) 장착, 크기 자동 맞춤
    ///  ③ HeldToolModel 연결 → 도구벨트에서 '돋보기' 들었을 때만 보임
    /// 돋보기 기능(단서 물건 반응)은 MouseInspector가 '돋보기 든 상태'에서만 작동.
    /// </summary>
    public static class MagnifierSetup
    {
        private const string Folder = "Assets/_Project/Art/Tools/Magnifier";

        [MenuItem("이문록/연출: 돋보기 설치")]
        public static void Setup()
        {
            var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { Folder });
            if (fbxGuids.Length == 0)
            {
                EditorUtility.DisplayDialog("이문록", $"{Folder} 에서 돋보기 FBX를 못 찾았어요.", "확인");
                return;
            }
            string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[0]);
            string basePng = fbxPath.Substring(0, fbxPath.Length - 4) + ".png";
            string normalPng = fbxPath.Substring(0, fbxPath.Length - 4) + "_normal.png";

            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePng);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPng);

            if (normalTex != null)
            {
                var ni = AssetImporter.GetAtPath(normalPng) as TextureImporter;
                if (ni != null && ni.textureType != TextureImporterType.NormalMap)
                {
                    ni.textureType = TextureImporterType.NormalMap;
                    ni.SaveAndReimport();
                }
            }

            // 재질(발광 없음)
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            string matPath = Folder + "/Magnifier_Mat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader;
            if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
            mat.SetColor("_BaseColor", Color.white);
            if (normalTex != null) { mat.SetTexture("_BumpMap", normalTex); mat.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(mat);

            // 씬에 "_돋보기" 장착
            var scene = EditorSceneManager.GetActiveScene();
            var cam = Camera.main;

            var parent = GameObject.Find("_돋보기") ?? new GameObject("_돋보기");
            if (cam != null) parent.transform.SetParent(cam.transform, false);
            parent.transform.localPosition = new Vector3(0.22f, -0.20f, 0.32f);   // 더 가깝게(화면에 더 보이게)
            parent.transform.localRotation = Quaternion.identity;

            var oldKids = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in parent.transform) oldKids.Add(child.gameObject);
            foreach (var k in oldKids) Object.DestroyImmediate(k);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "돋보기_모델";

            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxDim > 0.0001f) model.transform.localScale *= (0.4f / maxDim);
                foreach (var r in rends)
                {
                    var arr = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                    r.sharedMaterials = arr;
                }
            }
            model.transform.SetParent(parent.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(-90f, 270f, 0f);   // 세워서 앞면이 보이게

            // HeldToolModel 연결
            var htm = parent.GetComponent<HeldToolModel>() ?? parent.AddComponent<HeldToolModel>();
            var so = new SerializedObject(htm);
            so.FindProperty("_toolId").stringValue = "magnify";
            so.FindProperty("_model").objectReferenceValue = model;
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("이문록",
                "돋보기 설치 완료!\n\n" +
                "· 재질: Magnifier_Mat\n" +
                "· 씬: _돋보기 (도구벨트에서 '돋보기' 들면 손에 뜸)\n" +
                (cam == null ? "⚠ MainCamera 못 찾음 — 카메라 밑으로 옮기세요.\n" : "") +
                "\nPlay → Q/휠로 '돋보기' 선택 → 단서 물건 보면 설명이 떠요.\n" +
                "방향/크기는 _돋보기 Transform에서 조절. Ctrl+S.", "확인");
        }
    }
}
