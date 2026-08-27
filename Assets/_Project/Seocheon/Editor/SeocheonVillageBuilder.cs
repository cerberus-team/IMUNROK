using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// 스테이지 1부 서천 마을 씬의 "기반"만 생성한다.
    /// 메뉴: [Tools ▸ Seocheon ▸ Build Village Base]
    ///
    /// 생성물: Main Camera / Directional Light / Terrain(팩 원본 높이 그대로 복제).
    ///  · 개천·능선·존 마커는 만들지 않는다.
    ///  · Fog 는 끈 상태로 둔다.
    /// ★ 클린-슬레이트: 실행 시 씬의 모든 루트를 지우고 위 3개만 새로 만든다(개천/다리/물/존 등 잔재 제거).
    /// ★ 지형 복제본은 매 실행마다 팩 원본으로 새로 복제 → 개천·능선 흔적 0.
    /// ★ 팩 원본(Assets/Naganeupseong/…)은 읽기·복제만.
    /// </summary>
    public static class SeocheonVillageBuilder
    {
        private const string ScenePath        = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string SrcTerrainData   = "Assets/Naganeupseong/Scene/Resource/Terrain.asset"; // 원본(수정 금지)
        private const string TerrainFolder    = "Assets/_Project/Seocheon/Art/Terrain";
        private const string CloneTerrainData = TerrainFolder + "/Seocheon_Village_Terrain.asset";
        private const string ResetRenderDir   = @"C:\Users\User\_AssetBackup\render_reset";

        private static readonly Vector2 EntryXZ  = new Vector2(445f, 722f);
        private static readonly Vector2 CenterXZ = new Vector2(463f, 678f);

        [MenuItem("Tools/Seocheon/Build Village Base")]
        public static void BuildVillageBase()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets/_Project/Seocheon/Scenes");

            Scene scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 클린-슬레이트: 모든 루트 제거(개천/다리/물/존/기타 전부)
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);

            // ── Terrain (팩 원본으로 새 복제본) ──
            Terrain terrain = BuildTerrainFresh(out float baseY);
            float entryY = terrain.SampleHeight(new Vector3(EntryXZ.x, 0f, EntryXZ.y)) + baseY;

            // ── Main Camera ──
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.transform.position = new Vector3(EntryXZ.x, entryY + 1.6f, EntryXZ.y);
            Vector3 lookTarget = new Vector3(CenterXZ.x, entryY + 1.4f, CenterXZ.y);
            camGO.transform.rotation = Quaternion.LookRotation((lookTarget - camGO.transform.position).normalized, Vector3.up);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;

            // ── Directional Light (주간) ──
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.957f, 0.839f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ── 렌더 설정 (Fog 는 끈다) ──
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");

            GameObjectUtility.SetStaticEditorFlags(terrain.gameObject, (StaticEditorFlags)~0);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            long tris = CountSceneTris();
            Debug.Log($"[Seocheon] Village Base(클린) 생성 완료 → {ScenePath}\n" +
                      $"[루트] {RootList(scene)}\n[Fog] {RenderSettings.fog}  [렌더 tris] {tris}");
            EditorUtility.DisplayDialog("Seocheon", "Village Base 생성 완료(클린, Fog off).\n" + ScenePath, "확인");
        }

        /// <summary>초기화 검증: 지형 높이 팩 일치(20점)·씬 tris·루트 목록·부감 렌더 1장.</summary>
        [MenuItem("Tools/Seocheon/Verify Reset")]
        public static void VerifyReset()
        {
            if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog("Seocheon","씬 없음.","확인"); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            var sb = new StringBuilder();
            sb.AppendLine("===== [Seocheon] Reset 검증 =====");
            sb.AppendLine("[씬 루트] " + RootList(scene));
            sb.AppendLine("[Fog] " + RenderSettings.fog);

            if (terrains.Length > 0)
            {
                var cloneTD = terrains[0].terrainData;
                var packTD  = AssetDatabase.LoadAssetAtPath<TerrainData>(SrcTerrainData);
                int res = cloneTD.heightmapResolution;
                if (packTD != null && packTD.heightmapResolution == res)
                {
                    var hc = cloneTD.GetHeights(0,0,res,res);
                    var hp = packTD.GetHeights(0,0,res,res);
                    float maxDiff=0f; int mism=0;
                    sb.AppendLine("[지형 높이 20점 비교 (clone vs pack, 정규값)]");
                    for (int k=0;k<20;k++)
                    {
                        int ix=(int)((k*37+11)%res), iy=(int)((k*53+29)%res); // 결정적 표본 20점
                        float d=Mathf.Abs(hc[iy,ix]-hp[iy,ix]);
                        maxDiff=Mathf.Max(maxDiff,d); if (d>1e-6f) mism++;
                        if (k<6) sb.AppendLine($"   ({ix},{iy}) clone {hc[iy,ix]:F6} vs pack {hp[iy,ix]:F6}  Δ{d:F6}");
                    }
                    sb.AppendLine($"   → 불일치 점수 {mism}/20, 최대 Δ {maxDiff:F6}  ({(maxDiff<1e-5f ? "일치 ✓" : "불일치 ⚠")})");
                }
                else sb.AppendLine("[지형] 팩과 해상도 불일치 또는 팩 로드 실패");
            }
            sb.AppendLine("[씬 총 렌더 tris] " + CountSceneTris());

            string render = RenderAerial(terrains.Length>0?terrains[0]:null);
            sb.AppendLine("[부감 렌더] " + (render ?? "실패"));
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", "Reset 검증 완료. Console 로그 확인.", "확인");
        }

        private static string RenderAerial(Terrain t)
        {
            try
            {
                Directory.CreateDirectory(ResetRenderDir);
                float g0 = (t!=null ? t.transform.position.y + t.SampleHeight(new Vector3(463f,0f,678f)) : 0f);
                const int W=1280,H=720;
                var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32);
                var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
                var camGO=new GameObject("__resetCam");
                var cam=camGO.AddComponent<Camera>();
                cam.clearFlags=CameraClearFlags.Skybox; cam.fieldOfView=60f; cam.nearClipPlane=0.1f; cam.farClipPlane=2000f; cam.enabled=false;
                cam.transform.position=new Vector3(463f, g0+300f, 678f);
                cam.transform.rotation=Quaternion.LookRotation(Vector3.down, Vector3.forward);
                string fn=Path.Combine(ResetRenderDir,"Aerial_Top300.png");
                try
                {
                    var req=new UniversalRenderPipeline.SingleCameraRequest();
                    if (RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req); }
                    else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                    RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null;
                    File.WriteAllBytes(fn, tex.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
                return fn;
            }
            catch (System.Exception e) { return "예외: " + e.Message; }
        }

        private static Terrain BuildTerrainFresh(out float baseY)
        {
            EnsureFolder(TerrainFolder);
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(SrcTerrainData) == null)
                throw new System.Exception("원본 TerrainData를 찾을 수 없음: " + SrcTerrainData);
            // 기존 복제본 제거 후 팩 원본으로 새로 복제 → pristine 보장
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(CloneTerrainData) != null)
                AssetDatabase.DeleteAsset(CloneTerrainData);
            AssetDatabase.CopyAsset(SrcTerrainData, CloneTerrainData);
            AssetDatabase.ImportAsset(CloneTerrainData);

            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(CloneTerrainData);
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.position = Vector3.zero;
            var terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = 22f;
            terrain.drawInstanced = true;
            baseY = go.transform.position.y;
            return terrain;
        }

        private static long CountSceneTris()
        {
            long tris = 0;
            foreach (var f in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if (f.sharedMesh != null) tris += f.sharedMesh.triangles.Length / 3;
            return tris; // Terrain은 MeshFilter가 아니라 별도(LOD 동적)
        }

        private static string RootList(Scene scene)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var r in scene.GetRootGameObjects()) names.Add(r.name);
            return string.Join(", ", names);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
