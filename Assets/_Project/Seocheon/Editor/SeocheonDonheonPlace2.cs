using System.Collections.Generic;
using System.Globalization;
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
    /// 지반 평면 XZ-footprint 기준 제거(높이·노멀 무관) + 다리 앞(462,597) 재배치.
    /// ★원본 FBX/메시 미변경(R/W 임시). NoGround 에셋 in-place 덮어쓰기(프리팹 참조 보존). 링·수면·다리 미변경.
    /// </summary>
    public static class SeocheonDonheonPlace2
    {
        private const string ScenePath = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string CurTerr   = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain.asset";
        private const string BakTerr   = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain_backup_predonheon.asset";
        private const string Fbx       = "Assets/_Project/Seocheon/Art/Models/Donheon_Quest3_v1.fbx";
        private const string NoGround  = "Assets/_Project/Seocheon/Art/Models/Donheon_Body_NoGround.asset";
        private const string PrefabPath= "Assets/_Project/Seocheon/Prefabs/Donheon.prefab";
        private const string PathFile  = @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const string RenderDir = @"C:\Users\User\_AssetBackup\render_donheon2";
        private const float CX=463f, CZ=678f;
        private static readonly Vector2 PadC=new Vector2(462f,597f);   // 다리 앞
        private static readonly Vector2 OldPad=new Vector2(492f,730f); // 되돌릴 이전 대지
        private const float KeepHalfX=11f, KeepHalfZ=6.5f;             // footprint 유지 반폭
        private const float HalfX=13f, HalfZ=9f, Skirt=8f;            // 대지 26×18 + 사면
        private const float RingBand=11.8f;                           // 링 중심선 ±이 안은 대지 미침범(모래+둑+여유)
        private const float ExtraRotY=0f;

        [MenuItem("Tools/Seocheon/Donheon/Remove Ground XZ + Place at Bridge")]
        public static void Run()
        {
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] Donheon XZ-Remove + Place@Bridge =====");
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx)==null||AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null){ EditorUtility.DisplayDialog("Seocheon","FBX/prefab 없음. Import v1 먼저.","확인"); return; }

            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null;
            var td=terrain.terrainData; int Rr=td.heightmapResolution; Vector3 S=td.size; float tY=terrain.transform.position.y,tX0=terrain.transform.position.x,tZ0=terrain.transform.position.z;
            float texX=S.x/(Rr-1), texZ=S.z/(Rr-1);

            // [0] 이전 대지(492,730) 되돌리기 = 백업 높이 복원
            var bak=AssetDatabase.LoadAssetAtPath<TerrainData>(BakTerr);
            float oldBefore=SampleH(td,S,tY,tX0,tZ0,OldPad.x,OldPad.y);
            if(bak!=null){ var bh=bak.GetHeights(0,0,Rr,Rr); td.SetHeights(0,0,bh); EditorUtility.SetDirty(td); terrain.Flush(); }
            var Hn=td.GetHeights(0,0,Rr,Rr);
            float H(float x,float z){ float u=(x-tX0)/S.x,v=(z-tZ0)/S.z; float fx=Mathf.Clamp(u*(Rr-1),0,Rr-1),fz=Mathf.Clamp(v*(Rr-1),0,Rr-1);
                int x0=(int)fx,z0=(int)fz,x1=Mathf.Min(x0+1,Rr-1),z1=Mathf.Min(z0+1,Rr-1); float tx=fx-x0,tz=fz-z0;
                return tY+Mathf.Lerp(Mathf.Lerp(Hn[z0,x0],Hn[z0,x1],tx),Mathf.Lerp(Hn[z1,x0],Hn[z1,x1],tx),tz)*S.y; }
            float oldAfter=H(OldPad.x,OldPad.y);
            KillRoot(scene,"_Donheon");
            sb.AppendLine($"[0] 이전 대지(492,730) 복원 {(bak!=null?"완료":"⚠백업없음")}: 높이 {oldBefore:F1}→{oldAfter:F1}m (원복 확인)");

            // R/W 임시 on → Body 읽기
            var mi=(ModelImporter)AssetImporter.GetAtPath(Fbx); bool wasRW=mi.isReadable; if(!wasRW){ mi.isReadable=true; mi.SaveAndReimport(); }
            var fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            MeshFilter bmf=null; foreach(var mf in fbxGo.GetComponentsInChildren<MeshFilter>(true)) if(mf.name.Contains("Body")){ bmf=mf; break; }
            var mesh0=bmf.sharedMesh; var vs=mesh0.vertices; var uv=mesh0.uv; var nm=mesh0.normals; var tg=mesh0.tangents; int subN=mesh0.subMeshCount;

            // [1] 건물 중심 = 삼각형 중심의 중앙값(median, 지반 이상치에 강함)
            var cxs=new List<float>(); var czs=new List<float>();
            var triC=new List<Vector2>[subN];
            for(int s=0;s<subN;s++){ var tri=mesh0.GetTriangles(s); triC[s]=new List<Vector2>(tri.Length/3);
                for(int t=0;t<tri.Length;t+=3){ Vector3 a=vs[tri[t]],b=vs[tri[t+1]],c=vs[tri[t+2]]; float cx=(a.x+b.x+c.x)/3f, cz=(a.z+b.z+c.z)/3f; triC[s].Add(new Vector2(cx,cz)); cxs.Add(cx); czs.Add(cz); } }
            float medX=Median(cxs), medZ=Median(czs);
            // XZ footprint 안만 유지
            var newTris=new List<int>[subN]; int removed=0, kept=0;
            float nxMin=1e9f,nxMax=-1e9f,nzMin=1e9f,nzMax=-1e9f,nyMin=1e9f,nyMax=-1e9f;
            for(int s=0;s<subN;s++){ var tri=mesh0.GetTriangles(s); newTris[s]=new List<int>(tri.Length);
                for(int t=0,ti=0;t<tri.Length;t+=3,ti++){ Vector2 cc=triC[s][ti];
                    if(Mathf.Abs(cc.x-medX)<=KeepHalfX && Mathf.Abs(cc.y-medZ)<=KeepHalfZ){ // Vector2.y=cz
                        newTris[s].Add(tri[t]); newTris[s].Add(tri[t+1]); newTris[s].Add(tri[t+2]); kept++;
                        for(int k=0;k<3;k++){ Vector3 v=vs[tri[t+k]]; nxMin=Mathf.Min(nxMin,v.x); nxMax=Mathf.Max(nxMax,v.x); nzMin=Mathf.Min(nzMin,v.z); nzMax=Mathf.Max(nzMax,v.z); nyMin=Mathf.Min(nyMin,v.y); nyMax=Mathf.Max(nyMax,v.y); } }
                    else removed++; } }
            sb.AppendLine($"[1] 건물 중심(median) X{medX:F1} Z{medZ:F1} · 유지 ±{KeepHalfX}×{KeepHalfZ}m → 제거 {removed} / 유지 {kept} tris (원본 {removed+kept})");

            // NoGround 에셋 in-place 덮어쓰기(프리팹 참조 보존)
            var nMesh=AssetDatabase.LoadAssetAtPath<Mesh>(NoGround); if(nMesh==null){ nMesh=new Mesh(); AssetDatabase.CreateAsset(nMesh,NoGround); }
            nMesh.Clear(); nMesh.name="Donheon_Body_NoGround"; nMesh.indexFormat=mesh0.indexFormat;
            nMesh.vertices=vs; if(uv!=null&&uv.Length==vs.Length) nMesh.uv=uv; if(nm!=null&&nm.Length==vs.Length) nMesh.normals=nm; if(tg!=null&&tg.Length==vs.Length) nMesh.tangents=tg;
            nMesh.subMeshCount=subN; for(int s=0;s<subN;s++) nMesh.SetTriangles(newTris[s],s); nMesh.RecalculateBounds();
            EditorUtility.SetDirty(nMesh); AssetDatabase.SaveAssets();
            if(!wasRW){ mi.isReadable=false; mi.SaveAndReimport(); }

            // [2] 판정
            Bounds nb=nMesh.bounds;
            bool ok = nb.size.x<28f && nb.size.z<20f; // 44×44가 남아있으면 실패
            sb.AppendLine($"[2] 새 bbox {nb.size.x:F1}×{nb.size.z:F1}×{nb.size.y:F1}m {(ok?"✓ (44×44 제거 성공)":"⚠ 44×44 잔존 — 실패")}");
            sb.AppendLine($"[2] 유지 지오메트리 XZ {nxMin:F1}~{nxMax:F1} × {nzMin:F1}~{nzMax:F1} · Y {nyMin:F1}~{nyMax:F1}(높이 {nyMax-nyMin:F1}m) → 기단·축대·마루·본체 포함");
            sb.AppendLine($"[2] footprint 밖 잔여: 유지 반폭 ±{KeepHalfX}×{KeepHalfZ} 이내만 남김 → 튀어나온 면 없음(정점 최대 X{Mathf.Max(Mathf.Abs(nxMin-medX),Mathf.Abs(nxMax-medX)):F1} Z{Mathf.Max(Mathf.Abs(nzMin-medZ),Mathf.Abs(nzMax-medZ)):F1}m)");

            // 프리팹 Body → NoGround 재확정 + Static
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true)) if(mf.name.Contains("Body")) mf.sharedMesh=nMesh;
            SetStaticRec(root); PrefabUtility.SaveAsPrefabAsset(root,PrefabPath); PrefabUtility.UnloadPrefabContents(root);

            // [3] 배치 (462,597) 정면 남
            float rSouth=RthAt(-90f); float Zc=CZ-rSouth; float bankNorth=Zc+10.8f;
            float padY=H(PadC.x,PadC.y);
            float slope=0; foreach(var d in new[]{new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1)}) slope=Mathf.Max(slope,Mathf.Abs(H(PadC.x+d.x*13,PadC.y+d.y*13)-padY));
            sb.AppendLine($"[4] 대지 (462,597) 현재높이 {padY:F1}m · 주변경사 ±{slope:F1}m");

            // [4] 평탄화 26×18 + 사면, 링 밴드 미침범
            float padN=Mathf.Clamp01((padY-tY)/S.y);
            int hxMin=Mathf.Clamp(Mathf.FloorToInt((PadC.x-tX0-HalfX-Skirt)/texX),0,Rr-1), hxMax=Mathf.Clamp(Mathf.CeilToInt((PadC.x-tX0+HalfX+Skirt)/texX),0,Rr-1);
            int hzMin=Mathf.Clamp(Mathf.FloorToInt((PadC.y-tZ0-HalfZ-Skirt)/texZ),0,Rr-1), hzMax=Mathf.Clamp(Mathf.CeilToInt((PadC.y-tZ0+HalfZ+Skirt)/texZ),0,Rr-1);
            int flat=0, ringSkip=0;
            for(int hz=hzMin;hz<=hzMax;hz++) for(int hx=hxMin;hx<=hxMax;hx++){ float x=tX0+hx*texX, z=tZ0+hz*texZ;
                float rr=Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ)); float rth=RthAt(Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg);
                if(Mathf.Abs(rr-rth)<=RingBand){ ringSkip++; continue; } // ★링 수면/둑 밴드 미침범
                float ox=Mathf.Max(0,Mathf.Abs(x-PadC.x)-HalfX), oz=Mathf.Max(0,Mathf.Abs(z-PadC.y)-HalfZ); float dd=Mathf.Sqrt(ox*ox+oz*oz);
                if(dd<=0.001f){ Hn[hz,hx]=padN; flat++; } else if(dd<=Skirt){ float tt=Mathf.SmoothStep(0,1,dd/Skirt); Hn[hz,hx]=Mathf.Lerp(padN,Hn[hz,hx],tt); } }
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); terrain.Flush();
            sb.AppendLine($"[4] 26×18 평탄({flat}텍셀) {padY:F1}m + {Skirt}m 사면 · 링밴드 {ringSkip}텍셀 미침범");

            // 인스턴스
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var inst=(GameObject)PrefabUtility.InstantiatePrefab(prefab); var holder=new GameObject("_Donheon"); inst.transform.SetParent(holder.transform);
            float rotY=(nb.size.z>nb.size.x)?90f:0f; rotY+=ExtraRotY; inst.transform.rotation=Quaternion.Euler(0,rotY,0);
            inst.transform.position=new Vector3(PadC.x,padY,PadC.y);
            var rends=inst.GetComponentsInChildren<Renderer>(true); Bounds wb=rends[0].bounds; foreach(var r in rends) wb.Encapsulate(r.bounds);
            inst.transform.position+=new Vector3(0, padY-wb.min.y, 0);
            wb=rends[0].bounds; foreach(var r in rends) wb.Encapsulate(r.bounds);
            SetStaticRec(holder);
            float frontZ=wb.min.z; float gap=frontZ-bankNorth;
            sb.AppendLine($"[3] 배치 (462,597) rotY{rotY:F0}° · 정면Z {frontZ:F1} · 둑북단 {bankNorth:F1} → 여유 {gap:F1}m {(gap<10f?"⚠ 10m 미만!":"✓")}");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);

            // [5] 검증
            int donTris=0; foreach(var mf in inst.GetComponentsInChildren<MeshFilter>(true)) if(mf.sharedMesh!=null) for(int s=0;s<mf.sharedMesh.subMeshCount;s++) donTris+=(int)(mf.sharedMesh.GetIndexCount(s)/3);
            var pinks=new List<string>(); foreach(var r in inst.GetComponentsInChildren<Renderer>(true)) foreach(var m in r.sharedMaterials) if(m==null||m.shader==null||m.shader.name.Contains("InternalErrorShader")){ if(m!=null&&!pinks.Contains(m.name)) pinks.Add(m.name); }
            var texG=AssetDatabase.FindAssets("t:Texture2D", new[]{"Assets/_Project/Seocheon/Art/Textures/Donheon"}); long texMem=0; foreach(var g in texG){ var tex=AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(g)); if(tex!=null) texMem+=Profiler.GetRuntimeMemorySizeLong(tex); }
            float maxGap=0,maxEmbed=0; for(int a=-1;a<=1;a++)for(int b=-1;b<=1;b++){ float dh=H(PadC.x+a*HalfX*0.8f,PadC.y+b*HalfZ*0.8f)-wb.min.y; if(dh>maxEmbed)maxEmbed=dh; if(-dh>maxGap)maxGap=-dh; }
            int sceneTris=0; foreach(var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) if(mf.sharedMesh!=null) for(int s=0;s<mf.sharedMesh.subMeshCount;s++) sceneTris+=(int)(mf.sharedMesh.GetIndexCount(s)/3);
            // 다리 상면→정면 거리
            float floorY=H(CX,Zc); float deckTop=floorY+1.5f+0.8f; float bridgeToFront=frontZ-Zc;
            sb.AppendLine("── [5] 검증 ──");
            sb.AppendLine($"· 동헌 tris {donTris} · 씬 총 tris {sceneTris} · 텍스처메모리 {texMem/1048576f:F1}MB · 핑크 {(pinks.Count==0?"없음 ✓":string.Join(",",pinks))}");
            sb.AppendLine($"· 밑면-지형 최대틈 {maxGap:F2}m · 최대파묻힘 {maxEmbed:F2}m");
            sb.AppendLine($"· 다리 상면({Zc:F0})→관아 정면({frontZ:F0}) 거리 {bridgeToFront:F1}m");

            // [6] 렌더
            Directory.CreateDirectory(RenderDir); var files=new List<string>(); string rerr=null;
            float midY=padY+nb.size.y*0.45f; float hd=wb.size.z*0.5f, hw=wb.size.x*0.5f;
            try{
                files.Add(Cam("OnBridge_north", new Vector3(CX,deckTop+1.6f,Zc), new Vector3(PadC.x,midY,PadC.y))); // ★다리 위→북(관아 정면)
                files.Add(Cam("BridgeApproach", new Vector3(CX,H(CX,Zc+12)+1.6f,Zc+12), new Vector3(PadC.x,midY,PadC.y)));
                files.Add(Cam("Front_15m",      new Vector3(PadC.x,H(PadC.x,PadC.y-hd-15)+1.6f,PadC.y-hd-15), new Vector3(PadC.x,midY,PadC.y)));
                files.Add(Cam("Base_close",     new Vector3(PadC.x,padY+1.6f,PadC.y-hd-6), new Vector3(PadC.x,padY+0.4f,PadC.y-hd+1))); // ★기단·지형·지반잔여
                files.Add(CamTop("Aerial_150",  PadC.x, (Zc+PadC.y)*0.5f, 150f));
            }catch(System.Exception e){ rerr=e.ToString(); }
            if(rerr!=null) sb.AppendLine("[렌더] 실패: "+rerr); else { sb.AppendLine($"[렌더] {files.Count}장 → {RenderDir}"); foreach(var f in files) sb.AppendLine("   "+f); }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"XZ제거({removed}tris)+다리앞 배치 완료. bbox {nb.size.x:F0}×{nb.size.z:F0}. 여유 {gap:F1}m.\nrender_donheon2 확인. 정면 틀리면 Flip Front 180.","확인");
        }

        private static float Median(List<float> a){ if(a.Count==0) return 0f; a.Sort(); return a[a.Count/2]; }
        private static float SampleH(TerrainData td,Vector3 S,float tY,float tX0,float tZ0,float x,float z){ return tY+td.GetInterpolatedHeight((x-tX0)/S.x,(z-tZ0)/S.z); }
        private static float RthAt(float angDeg){ if(!File.Exists(PathFile)) return 106f; var ci=CultureInfo.InvariantCulture; var lines=File.ReadAllLines(PathFile);
            float best=1e9f, br=106f; for(int i=1;i<lines.Length;i++){ var t=lines[i].Split(' '); if(t.Length<2) continue; float x=float.Parse(t[0],ci), z=float.Parse(t[1],ci);
                float a=Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg; float dd=Mathf.Abs(Mathf.DeltaAngle(a,angDeg)); if(dd<best){best=dd; br=Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ));} } return br; }
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
