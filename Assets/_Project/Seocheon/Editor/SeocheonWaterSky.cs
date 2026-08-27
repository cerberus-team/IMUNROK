using System.Collections.Generic;
using System.Globalization;
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
    /// 주간 스카이박스 적용 + Ayo 물 하늘/태양 반사 3안(A/B/C) 렌더 비교.
    /// ★Ayo는 Unlit 셰이더 → 큐브맵 환경반사 없음. 여기서 올리는 '반사'는 태양 스펙큘러 글린트 + 프레넬 톤.
    ///   리플렉션 프로브는 Ayo가 무시하므로 설치하지 않는다.
    /// 지형/링/수면 메시는 건드리지 않는다. Opaque Texture·Fog 안 켠다.
    /// </summary>
    public static class SeocheonWaterSky
    {
        private const string ScenePath = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string AyoMat    = "Assets/Ayo Free Toon Water/Materials/Toon Water.mat";
        private const string MatDir    = "Assets/_Project/Seocheon/Art/Materials";
        private const string SkyMatPath= "Assets/_Project/Seocheon/Art/Materials/M_Seocheon_DaySky.mat";
        private const string PathFile  = @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const string RenderDir = @"C:\Users\User\_AssetBackup\render_sky";
        private const float CX=463f, CZ=678f;

        [MenuItem("Tools/Seocheon/Water Sky/Apply Sky + Render 3 Variants")]
        public static void ApplyAndRender()
        {
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] Water Sky =====");
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null;
            if (terrain==null){ EditorUtility.DisplayDialog("Seocheon","Terrain 없음.","확인"); return; }

            // 방향광(태양)
            Light sun=null; foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if(l.type==LightType.Directional){ sun=l; break; }
            sb.AppendLine(sun!=null?$"[태양] 방향광 발견: {sun.name} (dir {sun.transform.forward})":"[태양] ⚠ 방향광 없음 — 태양 글린트가 약할 수 있음");

            // [2] 주간 프로시저럴 스카이박스 생성·적용
            var skyMat=BuildDaySky();
            RenderSettings.skybox=skyMat; if(sun!=null) RenderSettings.sun=sun; DynamicGI.UpdateEnvironment();
            sb.AppendLine($"[스카이박스] Skybox/Procedural 주간 적용 → {SkyMatPath}");

            // 물 오브젝트
            GameObject water=null; foreach(var r in scene.GetRootGameObjects()) if(r.name=="_Stream_Water"){ water=r; break; }
            if (water==null){ EditorUtility.DisplayDialog("Seocheon","_Stream_Water 없음. 먼저 링 물을 생성하세요.","확인"); Debug.Log(sb.ToString()); return; }
            var mr=water.GetComponent<MeshRenderer>();
            if (mr==null){ EditorUtility.DisplayDialog("Seocheon","_Stream_Water에 MeshRenderer 없음.","확인"); return; }

            // 카메라 앵커(경로 첫 점 + 접선/수직)
            float ax=CX, az=CZ-105f, aground=0f; Vector2 perp=new Vector2(0,1);
            if (File.Exists(PathFile)){
                var lines=File.ReadAllLines(PathFile); var ci=CultureInfo.InvariantCulture;
                var p0=lines[1].Split(' '); var p1=lines[2].Split(' ');
                ax=float.Parse(p0[0],ci); az=float.Parse(p0[1],ci); aground=float.Parse(p0[3],ci);
                float bx=float.Parse(p1[0],ci), bz=float.Parse(p1[1],ci);
                Vector2 t=new Vector2(bx-ax,bz-az); if(t.sqrMagnitude<1e-4f) t=new Vector2(1,0); t.Normalize(); perp=new Vector2(-t.y,t.x);
            }
            float wY=terrain.transform.position.y+aground; // 수면 근사 높이
            Vector3 P=new Vector3(ax,wY,az);
            // 태양이 오는 수평 방향(글린트가 보이는 쪽)
            Vector3 h=new Vector3(0,0,1);
            if (sun!=null){ Vector3 f=sun.transform.forward; h=new Vector3(-f.x,0,-f.z); if(h.sqrMagnitude<1e-3f) h=new Vector3(0,0,1); h.Normalize(); }

            Directory.CreateDirectory(RenderDir);
            var files=new List<string>();
            char[] tags={'A','B','C'}; Material applied=null;
            foreach(var tag in tags){
                var mat=BuildVariant(tag); if(tag=='B') applied=mat;
                mr.sharedMaterial=mat;
                // 3 샷
                files.Add(Shot($"{tag}_Near_1p5m", P+new Vector3(perp.x,0,perp.y)*0.6f+Vector3.up*1.5f, P, 55f));
                files.Add(Shot($"{tag}_Mid_8m",     P+new Vector3(perp.x,0,perp.y)*3f+Vector3.up*8f,    P, 55f));
                files.Add(Shot($"{tag}_Sky_grazing",P-h*12f+Vector3.up*1.7f, P+h*30f+Vector3.up*3f,      60f));
            }
            // 기본은 B 적용
            if(applied!=null) mr.sharedMaterial=applied;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);
            sb.AppendLine($"[변경] 3안 머티리얼 생성(M_StreamWater_SkyA/B/C), 현재 씬엔 B 적용. 원본 M_Seocheon_StreamWater_A 는 미변경.");
            sb.AppendLine($"[렌더] {files.Count}장 → {RenderDir}");
            foreach(var f in files) sb.AppendLine("   "+f);
            sb.AppendLine("── 반사 현실 ──");
            sb.AppendLine("Ayo=Unlit → 큐브맵 하늘반사 없음. 위 '반사'는 태양 글린트+프레넬 톤. 리플렉션 프로브 미설치(무시됨).");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", "스카이 적용 + A/B/C 9장 렌더 완료.\nrender_sky 확인 후 원하는 안 알려주세요.","확인");
        }

        private static Material BuildDaySky()
        {
            EnsureFolder(MatDir);
            var sh=Shader.Find("Skybox/Procedural");
            Material m=AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
            if(m==null){ m=new Material(sh); AssetDatabase.CreateAsset(m,SkyMatPath); }
            else m.shader=sh;
            void F(string p,float v){ if(m.HasProperty(p)) m.SetFloat(p,v); } void C(string p,Color v){ if(m.HasProperty(p)) m.SetColor(p,v); }
            F("_SunDisk",2); F("_SunSize",0.04f); F("_SunSizeConvergence",5f);
            F("_AtmosphereThickness",1.0f); F("_Exposure",1.15f);
            C("_SkyTint", new Color(0.52f,0.62f,0.78f,1f));   // 조선 주간 하늘색(약간 맑은 파랑)
            C("_GroundColor", new Color(0.38f,0.40f,0.42f,1f));
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssets(); return m;
        }

        // 태양 글린트 + 프레넬 톤 3안. 굴절 0(Opaque 금지), 코스틱 OFF.
        private static Material BuildVariant(char tag)
        {
            string path=$"{MatDir}/M_StreamWater_Sky{tag}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)!=null) AssetDatabase.DeleteAsset(path);
            EnsureFolder(MatDir); AssetDatabase.CopyAsset(AyoMat, path);
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            void F(string p,float v){ if(m.HasProperty(p)) m.SetFloat(p,v); } void C(string p,Color v){ if(m.HasProperty(p)) m.SetColor(p,v); } void V(string p,Vector4 v){ if(m.HasProperty(p)) m.SetVector(p,v); }

            // 공통
            F("_Refraction_Strength",0f); F("_Refraction_Scale",0f);          // Opaque Texture 금지 → 굴절 0
            F("_1st_Caustics_Enable",0f); F("_2nd_Caustics_Enable",0f); F("_1st_Caustics_Brightness",0f); F("_2nd_Caustics_Brightness",0f);
            V("_Water_Direction", new Vector4(-1f,0f,0f,0f)); F("_Voronoi_Density",20f);
            F("_Wave_Scale",4f); F("_Wave_Speed",0.15f); F("_Wave_Strength",0.06f);
            F("_Sun_Reflection_Enable",1f); F("_Sun_Reflection_Offset",1.85f); F("_Sun_Reflection_Speed",0.08f);
            C("_Deep_Water_Color", new Color(0.09f,0.24f,0.30f,0.85f));
            C("_Foam_Color", new Color(1f,1f,1f,0.72f));

            switch(tag){
                case 'A': // 하늘/태양 반사 강, 매끈
                    F("_Smoothness",0.9f); C("_Specular_Color", new Color(3.5f,3.5f,3.5f,1f));
                    F("_Sun_Reflection_Cutoff",0.06f); F("_Sun_Reflection_Noise",0.4f);
                    F("_Foam_Amount",0.30f); F("_Foam_Scale",6f); F("_Foam_Cutoff",0.25f); F("_Foam_Speed",0.15f);
                    C("_Shallow_Water_Color", new Color(0.44f,0.68f,0.74f,0.26f)); break;
                case 'B': // 중간
                    F("_Smoothness",0.75f); C("_Specular_Color", new Color(1.6f,1.6f,1.6f,1f));
                    F("_Sun_Reflection_Cutoff",0.12f); F("_Sun_Reflection_Noise",0.5f);
                    F("_Foam_Amount",0.30f); F("_Foam_Scale",6f); F("_Foam_Cutoff",0.22f); F("_Foam_Speed",0.15f);
                    C("_Shallow_Water_Color", new Color(0.42f,0.66f,0.70f,0.30f)); break;
                default:  // C: 잔잔, 거품 강조
                    F("_Smoothness",0.6f); C("_Specular_Color", new Color(0.8f,0.8f,0.8f,1f));
                    F("_Sun_Reflection_Cutoff",0.2f); F("_Sun_Reflection_Noise",0.6f);
                    F("_Foam_Amount",0.45f); F("_Foam_Scale",7f); F("_Foam_Cutoff",0.20f); F("_Foam_Speed",0.18f);
                    C("_Shallow_Water_Color", new Color(0.40f,0.63f,0.66f,0.34f)); break;
            }
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssets(); return m;
        }

        private static string Shot(string name,Vector3 pos,Vector3 look,float fov)
        {
            const int W=1280,H=720; var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
            var camGO=new GameObject("__c"); var cam=camGO.AddComponent<Camera>(); cam.clearFlags=CameraClearFlags.Skybox; cam.fieldOfView=fov; cam.nearClipPlane=0.02f; cam.farClipPlane=3000f; cam.enabled=false;
            cam.transform.position=pos; cam.transform.rotation=Quaternion.LookRotation((look-pos).normalized,Vector3.up);
            string fn=Path.Combine(RenderDir,name+".png");
            try{ var req=new UniversalRenderPipeline.SingleCameraRequest(); if(RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req);} else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null; File.WriteAllBytes(fn,tex.EncodeToPNG()); }
            finally{ Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
            return fn;
        }

        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
    }
}
