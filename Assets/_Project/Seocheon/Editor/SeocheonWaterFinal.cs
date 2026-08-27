using System.Globalization;
using System.Collections.Generic;
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
    /// AllSkyFree BlueSky(주간) 스카이박스 적용 + M_Seocheon_StreamWater_A 를 SG_Seocheon_Water(큐브맵 하늘반사)로 교체 + 4장 렌더.
    /// 지형/링/수면 메시는 안 건드림(메시 마무리는 Stream Force ▸ 3에서). Opaque·Fog 미사용. SUIMONO·Ayo 원본 미변경.
    /// </summary>
    public static class SeocheonWaterFinal
    {
        private const string ScenePath = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string SkyMat    = "Assets/AllSkyFree/Cartoon Base BlueSky/Day_BlueSky_Nothing.mat";
        private const string MatA      = "Assets/_Project/Seocheon/Art/Materials/M_Seocheon_StreamWater_A.mat";
        private const string ShaderName= "Seocheon/SG_Seocheon_Water";
        private const string PathFile  = @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const string RenderDir = @"C:\Users\User\_AssetBackup\render_final";
        private const float CX=463f, CZ=678f;

        [MenuItem("Tools/Seocheon/Water Sky/SG Water + Render (current skybox)")]
        public static void Run()
        {
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] BlueSky + SG Water =====");
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null;
            if(terrain==null){ EditorUtility.DisplayDialog("Seocheon","Terrain 없음.","확인"); return; }

            // 태양
            Light sun=null; foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if(l.type==LightType.Directional){ sun=l; break; }

            // 스카이박스는 사용자가 직접 부착 → 건드리지 않음. 현재 설정된 스카이박스에서 환경반사(unity_SpecCube0)만 갱신.
            DynamicGI.UpdateEnvironment();
            sb.AppendLine("[환경] 스카이박스 미변경(사용자 부착). 환경반사만 갱신.");

            // [2] 머티리얼 A 를 SG_Seocheon_Water 로 교체 + B안+반사 값
            var sh=Shader.Find(ShaderName);
            if(sh==null){ EditorUtility.DisplayDialog("Seocheon","셰이더 컴파일 안 됨: "+ShaderName+"\n먼저 에러 0 확인.","확인"); Debug.Log(sb.ToString()); return; }
            var mat=AssetDatabase.LoadAssetAtPath<Material>(MatA);
            if(mat==null){ EditorUtility.DisplayDialog("Seocheon","M_Seocheon_StreamWater_A 없음.","확인"); return; }
            mat.shader=sh;
            void F(string p,float v){ if(mat.HasProperty(p)) mat.SetFloat(p,v); } void C(string p,Color v){ if(mat.HasProperty(p)) mat.SetColor(p,v); }
            C("_ShallowColor", new Color(0.42f,0.66f,0.70f,0.30f)); C("_DeepColor", new Color(0.09f,0.24f,0.30f,0.85f)); F("_DepthDistance",2.0f);
            C("_FoamColor", new Color(1f,1f,1f,0.72f)); F("_FoamAmount",0.30f); F("_FoamCutoff",0.35f);
            F("_Tiling",0.08f); F("_WaveScale",1.4f); F("_WaveSpeed",0.15f); F("_WaveStrength",0.15f);
            F("_ReflStrength",0.25f); F("_FresnelPower",6.5f); F("_Smoothness",0.65f);      // ★반사 약하게: 물색 지배, 비스듬할 때만 하늘
            C("_SunGlintColor", new Color(0.85f,0.85f,0.78f,1f)); F("_SunGlintStrength",0.6f); F("_SunGlintSharp",120f); F("_SunGlintSoft",0.4f); F("_Specular",1.0f); // 글린트 흰색 과노출 감소
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
            sb.AppendLine("[머티리얼] M_Seocheon_StreamWater_A → SG_Seocheon_Water (물색 지배 · ReflStrength0.25·Fresnel6.5·Glint0.6).");

            // 물에 적용
            GameObject water=null; foreach(var r in scene.GetRootGameObjects()) if(r.name=="_Stream_Water"){ water=r; break; }
            if(water==null){ EditorUtility.DisplayDialog("Seocheon","_Stream_Water 없음.","확인"); Debug.Log(sb.ToString()); return; }
            var mr=water.GetComponent<MeshRenderer>(); mr.sharedMaterial=mat;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);

            // 앵커/방향
            float ax=CX, az=CZ-105f, aground=0f; Vector2 perp=new Vector2(0,1);
            if(File.Exists(PathFile)){ var lines=File.ReadAllLines(PathFile); var ci=CultureInfo.InvariantCulture;
                var p0=lines[1].Split(' '); var p1=lines[2].Split(' ');
                ax=float.Parse(p0[0],ci); az=float.Parse(p0[1],ci); aground=float.Parse(p0[3],ci);
                float bx=float.Parse(p1[0],ci), bz=float.Parse(p1[1],ci); Vector2 t=new Vector2(bx-ax,bz-az); if(t.sqrMagnitude<1e-4f) t=new Vector2(1,0); t.Normalize(); perp=new Vector2(-t.y,t.x); }
            float wY=terrain.transform.position.y+aground; Vector3 P=new Vector3(ax,wY,az);
            Vector3 h=new Vector3(0,0,1); if(sun!=null){ Vector3 f=sun.transform.forward; h=new Vector3(-f.x,0,-f.z); if(h.sqrMagnitude<1e-3f) h=new Vector3(0,0,1); h.Normalize(); }

            Directory.CreateDirectory(RenderDir); var files=new List<string>(); string rerr=null;
            try{
                files.Add(Shot("Bank_oblique", P - new Vector3(h.x,0,h.z)*13f + Vector3.up*1.9f, P + new Vector3(h.x,0,h.z)*8f + Vector3.up*3.5f, 62f)); // ★반사 세기 확인
                files.Add(Shot("Near_1p5m",    P + new Vector3(perp.x,0,perp.y)*0.6f + Vector3.up*1.5f, P, 55f));
            }catch(System.Exception e){ rerr=e.ToString(); }
            if(rerr!=null) sb.AppendLine("[렌더] 실패: "+rerr); else { sb.AppendLine($"[렌더] {files.Count}장 → {RenderDir}"); foreach(var f in files) sb.AppendLine("   "+f); }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon","SG Water 적용 + 물가/근접 렌더 완료.\nrender_final 확인.","확인");
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
    }
}
