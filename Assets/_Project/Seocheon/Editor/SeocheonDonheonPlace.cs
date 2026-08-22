using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// 지반 평면 제거(삼각형 단위, 새 메시 에셋) → 프리팹 Body 교체 → 마을 북단(492,730) 대지 평탄화 + 배치 + 검증 + 렌더.
    /// ★원본 FBX/메시 미변경(R/W 임시 on→off 복원, 새 에셋 별도 저장). 링·수면·다리 미변경.
    /// </summary>
    public static class SeocheonDonheonPlace
    {
        private const string ScenePath = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string CurTerr   = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain.asset";
        private const string BakTerr   = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain_backup_predonheon.asset";
        private const string Fbx       = "Assets/_Project/Seocheon/Art/Models/Donheon_Quest3_v1.fbx";
        private const string NoGround  = "Assets/_Project/Seocheon/Art/Models/Donheon_Body_NoGround.asset";
        private const string PrefabPath= "Assets/_Project/Seocheon/Prefabs/Donheon.prefab";
        private const string RenderDir = @"C:\Users\User\_AssetBackup\render_donheon";
        private static readonly Vector2 PadC=new Vector2(492f,730f);
        private const float HalfX=13f, HalfZ=9f, Skirt=8f;         // 대지 26×18 + 사면 8
        private static readonly Rect Content=new Rect(416f,640f, 94f,77f); // X[416,510] Z[640,717] 침범 금지
        private const float ExtraRotY=0f;                          // 정면(대청)=−Z(남) 가정. 틀리면 Flip 180 메뉴.

        [MenuItem("Tools/Seocheon/Donheon/Remove Ground + Place")]
        public static void Run()
        {
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] Donheon Remove-Ground + Place =====");
            var fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if(fbxGo==null){ EditorUtility.DisplayDialog("Seocheon","FBX 없음. Import v1 먼저.","확인"); return; }
            if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null){ EditorUtility.DisplayDialog("Seocheon","Donheon.prefab 없음. Import v1 먼저.","확인"); return; }

            // R/W 임시 on
            var mi=(ModelImporter)AssetImporter.GetAtPath(Fbx); bool wasRW=mi.isReadable;
            if(!wasRW){ mi.isReadable=true; mi.SaveAndReimport(); }
            fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);

            // Body 메시 찾기
            MeshFilter bmf=null; foreach(var mf in fbxGo.GetComponentsInChildren<MeshFilter>(true)) if(mf.name.Contains("Body")){ bmf=mf; break; }
            if(bmf==null){ if(!wasRW){ mi.isReadable=false; mi.SaveAndReimport(); } EditorUtility.DisplayDialog("Seocheon","Body 메시 없음.","확인"); Debug.Log(sb.ToString()); return; }
            var mesh0=bmf.sharedMesh;
            var vs=mesh0.vertices; var uv=mesh0.uv; var nm=mesh0.normals; var tg=mesh0.tangents;
            float minY=mesh0.bounds.min.y; int subN=mesh0.subMeshCount;

            // ── 지반 판정 + 삼각형 필터(수평 & 최저Y 근처) ──
            var newTris=new List<int>[subN]; int removed=0, kept=0;
            float gxMin=1e9f,gxMax=-1e9f,gzMin=1e9f,gzMax=-1e9f;
            float bxMin=1e9f,bxMax=-1e9f,bzMin=1e9f,bzMax=-1e9f,byMax=-1e9f, byMin=1e9f;
            for(int s=0;s<subN;s++){ var tri=mesh0.GetTriangles(s); newTris[s]=new List<int>(tri.Length);
                for(int t=0;t<tri.Length;t+=3){ int i0=tri[t],i1=tri[t+1],i2=tri[t+2]; Vector3 a=vs[i0],b=vs[i1],c=vs[i2];
                    Vector3 n=Vector3.Cross(b-a,c-a).normalized; float cy=(a.y+b.y+c.y)/3f;
                    bool ground=(Mathf.Abs(n.y)>0.9f)&&(cy<=minY+0.15f);
                    if(ground){ removed++; gxMin=Mathf.Min(gxMin,Mathf.Min(a.x,Mathf.Min(b.x,c.x))); gxMax=Mathf.Max(gxMax,Mathf.Max(a.x,Mathf.Max(b.x,c.x)));
                        gzMin=Mathf.Min(gzMin,Mathf.Min(a.z,Mathf.Min(b.z,c.z))); gzMax=Mathf.Max(gzMax,Mathf.Max(a.z,Mathf.Max(b.z,c.z))); }
                    else { newTris[s].Add(i0); newTris[s].Add(i1); newTris[s].Add(i2); kept++;
                        bxMin=Mathf.Min(bxMin,Mathf.Min(a.x,Mathf.Min(b.x,c.x))); bxMax=Mathf.Max(bxMax,Mathf.Max(a.x,Mathf.Max(b.x,c.x)));
                        bzMin=Mathf.Min(bzMin,Mathf.Min(a.z,Mathf.Min(b.z,c.z))); bzMax=Mathf.Max(bzMax,Mathf.Max(a.z,Mathf.Max(b.z,c.z)));
                        byMax=Mathf.Max(byMax,Mathf.Max(a.y,Mathf.Max(b.y,c.y))); byMin=Mathf.Min(byMin,Mathf.Min(a.y,Mathf.Min(b.y,c.y))); } } }
            sb.AppendLine($"[조사/2] 지반 판정: 수평(+Y)&최저Y({minY:F2}) → 제거 {removed}tris, XZ {gxMax-gxMin:F1}×{gzMax-gzMin:F1}m");
            sb.AppendLine($"[2] 제거 전 {removed+kept}tris → 제거 후 {kept}tris");

            // 새 메시 생성(정점/UV/노멀/탄젠트/슬롯순서 보존)
            if(AssetDatabase.LoadAssetAtPath<Mesh>(NoGround)!=null) AssetDatabase.DeleteAsset(NoGround);
            var nMesh=new Mesh(); nMesh.name="Donheon_Body_NoGround"; nMesh.indexFormat=mesh0.indexFormat;
            nMesh.vertices=vs; if(uv!=null&&uv.Length==vs.Length) nMesh.uv=uv; if(nm!=null&&nm.Length==vs.Length) nMesh.normals=nm; if(tg!=null&&tg.Length==vs.Length) nMesh.tangents=tg;
            nMesh.subMeshCount=subN; for(int s=0;s<subN;s++) nMesh.SetTriangles(newTris[s],s);
            nMesh.RecalculateBounds();
            AssetDatabase.CreateAsset(nMesh,NoGround); AssetDatabase.SaveAssets();

            // ── [3] 뚫림 확인: 본체(평면 제외) bbox·높이 ──
            Bounds nb=nMesh.bounds;
            sb.AppendLine($"[3] 새 메시 bbox {nb.size.x:F1}×{nb.size.z:F1}×{nb.size.y:F1}m (44×44→축소), 최저Y {byMin:F2}(=기단 밑, minY와 Δ{byMin-minY:F2})");
            bool holed = (byMin > minY+0.3f); // 기단 밑면까지 사라졌으면 바닥이 위로 뜸 → 뚫림 신호
            sb.AppendLine($"[3] 건물 아래: 기단/축대/마루 지오메트리 {(nb.size.y>1f&&!holed?"남아있음 ✓ (안 뚫림)":"⚠ 확인 필요")} · 본체높이 {nb.size.y:F1}m");
            if(holed) sb.AppendLine("[3] ⚠ 기단 밑면까지 제거된 정황 — 렌더의 밑동 근접 샷 확인 요망. 문제면 NoGround 삭제로 되돌림.");

            // R/W 복원
            if(!wasRW){ mi.isReadable=false; mi.SaveAndReimport(); }

            // ★뚫림 감지 시 [3] 지시대로 되돌림(프리팹·씬 미변경, NoGround는 검사용으로 남김)
            if(holed){ sb.AppendLine("[중단] 뚫림 신호 → 프리팹 교체·배치 안 함. NoGround는 검사용으로 남김. 판정 요망(문제면 NoGround 삭제).");
                Debug.Log(sb.ToString()); EditorUtility.DisplayDialog("Seocheon","⚠ 지반 제거 시 기단 밑면까지 사라진 정황.\n프리팹/배치 중단. Console의 [3] 확인.","확인"); return; }

            // ── [4] 프리팹 Body 교체 + Static ──
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            MeshFilter pbody=null; foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true)) if(mf.name.Contains("Body")){ pbody=mf; break; }
            if(pbody!=null) pbody.sharedMesh=nMesh;
            SetStaticRec(root);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath); PrefabUtility.UnloadPrefabContents(root);
            sb.AppendLine($"[4] 프리팹 Body 메시 → {NoGround} 교체 · Static ON");

            // ── [5] 배치 + 대지 ──
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null;
            var td=terrain.terrainData; int Rr=td.heightmapResolution; Vector3 S=td.size; float tY=terrain.transform.position.y,tX0=terrain.transform.position.x,tZ0=terrain.transform.position.z;
            if(AssetDatabase.LoadAssetAtPath<TerrainData>(BakTerr)==null) AssetDatabase.CopyAsset(CurTerr,BakTerr);
            var Hn=td.GetHeights(0,0,Rr,Rr);
            float H(float x,float z){ float u=(x-tX0)/S.x,v=(z-tZ0)/S.z; float fx=Mathf.Clamp(u*(Rr-1),0,Rr-1),fz=Mathf.Clamp(v*(Rr-1),0,Rr-1);
                int x0=(int)fx,z0=(int)fz,x1=Mathf.Min(x0+1,Rr-1),z1=Mathf.Min(z0+1,Rr-1); float tx=fx-x0,tz=fz-z0;
                return tY+Mathf.Lerp(Mathf.Lerp(Hn[z0,x0],Hn[z0,x1],tx),Mathf.Lerp(Hn[z1,x0],Hn[z1,x1],tx),tz)*S.y; }

            float padY=H(PadC.x,PadC.y);
            float slope=0; foreach(var d in new[]{new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1)}) slope=Mathf.Max(slope,Mathf.Abs(H(PadC.x+d.x*13,PadC.y+d.y*13)-padY));
            sb.AppendLine($"[5] 대지 (492,730) 현재높이 {padY:F1}m · 주변경사 ±{slope:F1}m");
            // 평탄화(사각 26×18 + 사면, 마을 콘텐츠 영역 침범 금지)
            float texX=S.x/(Rr-1), texZ=S.z/(Rr-1); float padN=Mathf.Clamp01((padY-tY)/S.y);
            int hxMin=Mathf.Clamp(Mathf.FloorToInt((PadC.x-tX0-HalfX-Skirt)/texX),0,Rr-1), hxMax=Mathf.Clamp(Mathf.CeilToInt((PadC.x-tX0+HalfX+Skirt)/texX),0,Rr-1);
            int hzMin=Mathf.Clamp(Mathf.FloorToInt((PadC.y-tZ0-HalfZ-Skirt)/texZ),0,Rr-1), hzMax=Mathf.Clamp(Mathf.CeilToInt((PadC.y-tZ0+HalfZ+Skirt)/texZ),0,Rr-1);
            int flat=0;
            for(int hz=hzMin;hz<=hzMax;hz++) for(int hx=hxMin;hx<=hxMax;hx++){ float x=tX0+hx*texX, z=tZ0+hz*texZ;
                if(Content.Contains(new Vector2(x,z))) continue; // ★콘텐츠 영역 미침범
                float ox=Mathf.Max(0,Mathf.Abs(x-PadC.x)-HalfX), oz=Mathf.Max(0,Mathf.Abs(z-PadC.y)-HalfZ); float dd=Mathf.Sqrt(ox*ox+oz*oz);
                if(dd<=0.001f){ Hn[hz,hx]=padN; flat++; }
                else if(dd<=Skirt){ float tt=Mathf.SmoothStep(0,1,dd/Skirt); Hn[hz,hx]=Mathf.Lerp(padN,Hn[hz,hx],tt); } }
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); terrain.Flush();
            sb.AppendLine($"[5] 26×18 평탄({flat}텍셀) {padY:F1}m + {Skirt}m 완만사면 · 콘텐츠 영역 미침범");

            // 배치
            KillRoot(scene,"_Donheon");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var inst=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var holder=new GameObject("_Donheon"); inst.transform.SetParent(holder.transform);
            // 장축(폭 18.6)→X 정렬 후 정면=−Z(남) 가정
            float rotY=(nb.size.z>nb.size.x)?90f:0f; rotY+=ExtraRotY;
            inst.transform.rotation=Quaternion.Euler(0,rotY,0);
            inst.transform.position=new Vector3(PadC.x,padY,PadC.y);
            // 기단 밑면을 대지에 정확히
            var rends=inst.GetComponentsInChildren<Renderer>(true); Bounds wb=rends[0].bounds; foreach(var r in rends) wb.Encapsulate(r.bounds);
            inst.transform.position+=new Vector3(0, padY-wb.min.y, 0);
            wb=rends[0].bounds; foreach(var r in rends) wb.Encapsulate(r.bounds);
            SetStaticRec(holder);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);
            sb.AppendLine($"[5] 배치 (492,730) rotY {rotY:F0}° · 밑면Y {wb.min.y:F2}=대지 {padY:F2} · footprint {wb.size.x:F1}×{wb.size.z:F1}m");

            // ── [6] 검증 ──
            int donTris=0; foreach(var mf in inst.GetComponentsInChildren<MeshFilter>(true)) if(mf.sharedMesh!=null) for(int s=0;s<mf.sharedMesh.subMeshCount;s++) donTris+=(int)(mf.sharedMesh.GetIndexCount(s)/3);
            var pinks=new List<string>(); foreach(var r in inst.GetComponentsInChildren<Renderer>(true)) foreach(var m in r.sharedMaterials){ if(m==null||m.shader==null||m.shader.name.Contains("InternalErrorShader")){ if(m!=null&&!pinks.Contains(m.name)) pinks.Add(m.name); } }
            var texGuids=AssetDatabase.FindAssets("t:Texture2D", new[]{"Assets/_Project/Seocheon/Art/Textures/Donheon"});
            long texMem=0; foreach(var g in texGuids){ var tex=AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(g)); if(tex!=null) texMem+=Profiler.GetRuntimeMemorySizeLong(tex); }
            // 밑면-지형 틈/파묻힘: footprint 9점 샘플
            float maxGap=0,maxEmbed=0; for(int a=-1;a<=1;a++)for(int b=-1;b<=1;b++){ float x=PadC.x+a*HalfX*0.8f, z=PadC.y+b*HalfZ*0.8f; float dh=H(x,z)-wb.min.y; if(dh>maxEmbed)maxEmbed=dh; if(-dh>maxGap)maxGap=-dh; }
            int sceneTris=0; foreach(var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) if(mf.sharedMesh!=null) for(int s=0;s<mf.sharedMesh.subMeshCount;s++) sceneTris+=(int)(mf.sharedMesh.GetIndexCount(s)/3);
            sb.AppendLine("── [6] 검증 ──");
            sb.AppendLine($"· 동헌 tris {donTris} · 텍스처메모리 {texMem/1048576f:F1}MB · 핑크 {(pinks.Count==0?"없음 ✓":string.Join(",",pinks))}");
            sb.AppendLine($"· 밑면-지형 최대틈 {maxGap:F2}m · 최대파묻힘 {maxEmbed:F2}m (0 근접)");
            sb.AppendLine($"· 씬 총 렌더 tris {sceneTris}");

            // ── [7] 렌더 ──
            Directory.CreateDirectory(RenderDir); var files=new List<string>(); string rerr=null;
            float midY=padY+nb.size.y*0.45f; float hd=wb.size.z*0.5f, hw=wb.size.x*0.5f;
            try{
                files.Add(Cam("Front_15m", new Vector3(PadC.x,H(PadC.x,PadC.y-hd-15)+1.6f,PadC.y-hd-15), new Vector3(PadC.x,midY,PadC.y)));
                files.Add(Cam("Front_30m", new Vector3(PadC.x,H(PadC.x,PadC.y-hd-30)+1.6f,PadC.y-hd-30), new Vector3(PadC.x,midY,PadC.y)));
                files.Add(Cam("Side_20m",  new Vector3(PadC.x+hw+20,H(PadC.x+hw+20,PadC.y)+1.6f,PadC.y), new Vector3(PadC.x,midY,PadC.y)));
                files.Add(Cam("Base_close",new Vector3(PadC.x,padY+1.6f,PadC.y-hd-6), new Vector3(PadC.x,padY+0.4f,PadC.y-hd+1))); // ★기단·지형 접합
                files.Add(CamTop("Aerial_120", PadC.x, PadC.y, 120f));
            }catch(System.Exception e){ rerr=e.ToString(); }
            if(rerr!=null) sb.AppendLine("[렌더] 실패: "+rerr); else { sb.AppendLine($"[렌더] {files.Count}장 → {RenderDir}"); foreach(var f in files) sb.AppendLine("   "+f); }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"지반제거({removed}tris)+배치 완료. 핑크 {pinks.Count}. render_donheon 확인.\n정면 방향 틀리면 'Flip Front 180' 실행.","확인");
        }

        [MenuItem("Tools/Seocheon/Donheon/Flip Front 180")]
        public static void Flip()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject holder=null; foreach(var r in scene.GetRootGameObjects()) if(r.name=="_Donheon") holder=r;
            if(holder==null){ EditorUtility.DisplayDialog("Seocheon","_Donheon 없음.","확인"); return; }
            var inst=holder.transform.GetChild(0); inst.rotation=inst.rotation*Quaternion.Euler(0,180,0);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);
            EditorUtility.DisplayDialog("Seocheon","정면 180° 회전 완료. 다시 렌더는 Remove Ground+Place 재실행 또는 수동 확인.","확인");
        }

        private static void SetStaticRec(GameObject g){ g.isStatic=true; foreach(Transform c in g.transform) SetStaticRec(c.gameObject); }
        private static void KillRoot(Scene s,string n){ foreach(var r in s.GetRootGameObjects()) if(r.name==n) Object.DestroyImmediate(r); }

        private static string Cam(string name,Vector3 pos,Vector3 look){ return Shot(name,pos,(look-pos).normalized,Vector3.up,55f); }
        private static string CamTop(string name,float cx,float cz,float alt){ var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain t=terr.Length>0?terr[0]:null;
            float g=t!=null?t.transform.position.y+t.SampleHeight(new Vector3(cx,0,cz)):0f; return Shot(name,new Vector3(cx,g+alt,cz),Vector3.down,Vector3.forward,60f); }
        private static string Shot(string name,Vector3 pos,Vector3 fwd,Vector3 up,float fov)
        {
            const int W=1280,H=720; var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
            var camGO=new GameObject("__c"); var cam=camGO.AddComponent<Camera>(); cam.clearFlags=CameraClearFlags.Skybox; cam.fieldOfView=fov; cam.nearClipPlane=0.05f; cam.farClipPlane=3000f; cam.enabled=false;
            cam.transform.position=pos; cam.transform.rotation=Quaternion.LookRotation(fwd,up);
            string fn=Path.Combine(RenderDir,name+".png");
            try{ var req=new UniversalRenderPipeline.SingleCameraRequest(); if(RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req);} else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null; File.WriteAllBytes(fn,tex.EncodeToPNG()); }
            finally{ Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
            return fn;
        }
    }
}
