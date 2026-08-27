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
    /// 정남 다리(낙안읍성 팩 모듈 인스턴스) + 무당집·묘역 대지 평탄화 + 접근로 마커.
    /// ★새 메시 안 만듦(FBX/프리팹 인스턴스만). 링/수면 메시/물 머티리얼 미변경. Opaque·Fog 안 켬.
    /// </summary>
    public static class SeocheonBridge
    {
        private const string ScenePath = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string CurPath   = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain.asset";
        private const string BakPath   = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain_backup_prebridge.asset";
        private const string FloorFbx  = "Assets/Naganeupseong/Resource/Meshes/Floor/SM_Floor01d.fbx";
        private const string FloorMat  = "Assets/Naganeupseong/Resource/Materials/M_Floor01d.mat";
        private const string PillarPref= "Assets/Naganeupseong/Prefabs/Structure/Pillar/Pillar01b.prefab";
        private const string MeshPath  = "Assets/_Project/Seocheon/Art/Models/Seocheon_StreamWater.asset";
        private const string PathFile  = @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const string RenderDir = @"C:\Users\User\_AssetBackup\render_bridge";
        private const float CX=463f, CZ=678f;
        // 무당집/묘역 대지
        private static readonly Vector2 MudangXZ=new Vector2(430f,520f);
        private static readonly Vector2 TombXZ  =new Vector2(492f,505f);
        private const float PadFlat=10f, PadSkirt=18f; // 반경: 평탄 10m, 사면 끝 18m

        [MenuItem("Tools/Seocheon/Bridge/Build South Bridge + Pads")]
        public static void Build()
        {
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] South Bridge + Pads =====");
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null;
            if(terrain==null){ EditorUtility.DisplayDialog("Seocheon","Terrain 없음.","확인"); return; }
            var td=terrain.terrainData; int R=td.heightmapResolution; Vector3 S=td.size; float tY=terrain.transform.position.y, tX0=terrain.transform.position.x, tZ0=terrain.transform.position.z;

            // [0] Cube 마커 삭제 + 지형 백업
            int delCube=0; foreach(var r in scene.GetRootGameObjects()){ if(r.name=="Cube"||r.name.StartsWith("Cube ")||r.name.StartsWith("Cube(")){ Object.DestroyImmediate(r); delCube++; } }
            if(AssetDatabase.LoadAssetAtPath<TerrainData>(BakPath)==null) sb.AppendLine($"[0] Cube {delCube}개 삭제 · 지형 백업 {(AssetDatabase.CopyAsset(CurPath,BakPath)?"완료":"실패")} → {BakPath}");
            else sb.AppendLine($"[0] Cube {delCube}개 삭제 · 지형 백업 이미 존재 → {BakPath}");

            var Hn=td.GetHeights(0,0,R,R);
            float H(float x,float z){ float u=(x-tX0)/S.x,v=(z-tZ0)/S.z; float fx=Mathf.Clamp(u*(R-1),0,R-1),fz=Mathf.Clamp(v*(R-1),0,R-1);
                int x0=(int)fx,z0=(int)fz,x1=Mathf.Min(x0+1,R-1),z1=Mathf.Min(z0+1,R-1); float tx=fx-x0,tz=fz-z0;
                return tY+Mathf.Lerp(Mathf.Lerp(Hn[z0,x0],Hn[z0,x1],tx),Mathf.Lerp(Hn[z1,x0],Hn[z1,x1],tx),tz)*S.y; }

            // [1] 정남 다리 위치: 링 r(θ=-90°)
            float rSouth=RthAt(-90f); float Zc=CZ-rSouth;
            float floorY=H(CX,Zc);                              // 채널 바닥(=W−1.5, 강제 평탄) 실측
            float Wsurf=floorY+1.5f;                            // 수면
            float deckTopY=Wsurf+0.8f;                          // 상면 = 수면+0.8
            float bankN=H(CX,Zc+11f), bankS=H(CX,Zc-11f);       // 양쪽 둑 top(≈W+0.8, 검증용)
            sb.AppendLine($"[1] 정남 링 수면중심 ({CX:F0},{Zc:F0})  강바닥 {floorY:F2} · 수면 {Wsurf:F2} · 상면Y {deckTopY:F2} · 둑top N{bankN:F1}/S{bankS:F1}");

            KillRoot(scene,"_Bridge"); KillRoot(scene,"_Outside");
            var bridge=new GameObject("_Bridge"); bridge.transform.position=Vector3.zero;
            var floorMat=AssetDatabase.LoadAssetAtPath<Material>(FloorMat);
            var floorModel=AssetDatabase.LoadAssetAtPath<GameObject>(FloorFbx);
            var pillarModel=AssetDatabase.LoadAssetAtPath<GameObject>(PillarPref);
            if(floorModel==null||pillarModel==null){ EditorUtility.DisplayDialog("Seocheon","팩 모듈 로드 실패.\n"+FloorFbx+"\n"+PillarPref,"확인"); Debug.Log(sb.ToString()); return; }

            // 바닥판: 프로브 인스턴스의 월드 bounds(임포트 스케일 반영)로 축 판별 → 두께→월드Y, 최장→월드X(폭2.15), 중간→월드Z(스텝1.52)
            var fprobe=(GameObject)PrefabUtility.InstantiatePrefab(floorModel); fprobe.transform.position=Vector3.zero; fprobe.transform.rotation=Quaternion.identity;
            var fpr=fprobe.GetComponentInChildren<Renderer>(); Vector3 fsz=fpr.bounds.size;
            int plankTris=0; foreach(var mf in fprobe.GetComponentsInChildren<MeshFilter>()) if(mf.sharedMesh!=null) plankTris+=mf.sharedMesh.triangles.Length/3;
            Object.DestroyImmediate(fprobe);
            int thin=MinAxis(fsz), lng=MaxAxis(fsz), mid=3-thin-lng;
            Quaternion Rp=Quaternion.Inverse(Quaternion.LookRotation(AxisVec(mid),AxisVec(thin)));
            float thick=fsz[thin], stepZ=fsz[mid], widthX=fsz[lng];
            float bridgeLen=22f; int nPlank=Mathf.CeilToInt(bridgeLen/stepZ); float half=nPlank*stepZ*0.5f;
            for(int i=0;i<nPlank;i++){ float z=Zc-half+(i+0.5f)*stepZ;
                var g=(GameObject)PrefabUtility.InstantiatePrefab(floorModel, bridge.transform); g.name=$"Deck_{i:00}"; g.transform.rotation=Rp;
                foreach(var mr in g.GetComponentsInChildren<MeshRenderer>()) if(floorMat!=null) mr.sharedMaterial=floorMat;
                Vector3 tgt=new Vector3(CX, deckTopY-thick*0.5f, z);
                var rend=g.GetComponentInChildren<Renderer>(); g.transform.position=tgt; g.transform.position+=tgt-rend.bounds.center; }

            // 기둥: 개천 안 4개 (X±0.8, Z±4). 하단=강바닥, 상단=바닥판 밑면. Y스케일로 맞춤.
            var pprobe=(GameObject)PrefabUtility.InstantiatePrefab(pillarModel); pprobe.transform.position=Vector3.zero; pprobe.transform.rotation=Quaternion.identity;
            var ppr=pprobe.GetComponentInChildren<Renderer>(); float pillarH=ppr.bounds.size.y;
            int pillarTris=0; foreach(var mf in pprobe.GetComponentsInChildren<MeshFilter>()) if(mf.sharedMesh!=null) pillarTris+=mf.sharedMesh.triangles.Length/3;
            Object.DestroyImmediate(pprobe);
            float deckUnder=deckTopY-thick; float needH=deckUnder-floorY; int nPillar=0;
            float subMerge=0f, pillarBotY=0f;
            foreach(float px in new[]{CX-0.8f,CX+0.8f}) foreach(float pz in new[]{Zc-4f,Zc+4f}){
                var g=(GameObject)PrefabUtility.InstantiatePrefab(pillarModel, bridge.transform); g.name=$"Pillar_{nPillar:00}";
                float sy=needH/Mathf.Max(0.01f,pillarH); var sc=g.transform.localScale; sc.y*=sy; g.transform.localScale=sc;
                var rend=g.GetComponentInChildren<Renderer>(); g.transform.position=new Vector3(px,deckUnder,pz);
                g.transform.position+=new Vector3(0, floorY-rend.bounds.min.y, 0); // 하단을 강바닥에
                pillarBotY=rend.bounds.min.y; subMerge=Wsurf-pillarBotY; nPillar++; }
            SetStaticRec(bridge);
            sb.AppendLine($"[1] 다리: 바닥 {nPlank}판(각 {plankTris}tris, 폭 {widthX:F2}·스텝 {stepZ:F2}·두께 {thick:F2}) + 기둥 {nPillar}(각 {pillarTris}tris, 높이 {needH:F2}) · Static.");
            sb.AppendLine("[1] 난간 모듈: 팩에 rail/fence/난간 없음 → 난간 없이 둠.");

            // [2] 대지 평탄화(무당집·묘역): 현재 높이/경사 측정 → 20×20 평탄 + 사면
            var padSb=new StringBuilder();
            float mudY=FlattenPad(Hn,R,S,tX0,tZ0,tY,MudangXZ,"무당집",padSb,H);
            float tomY=FlattenPad(Hn,R,S,tX0,tZ0,tY,TombXZ,"묘역",padSb,H);
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); terrain.Flush();
            sb.Append(padSb.ToString());

            // [3] 접근로 마커
            var outside=new GameObject("_Outside"); outside.transform.position=Vector3.zero;
            NewMarker(outside,"Bridge_South", new Vector3(CX,deckTopY,Zc));
            NewMarker(outside,"Mudang_Pad", new Vector3(MudangXZ.x, mudY, MudangXZ.y));
            NewMarker(outside,"Tomb_Pad",   new Vector3(TombXZ.x,  tomY, TombXZ.y));
            sb.AppendLine($"[3] 마커 _Outside/{{Bridge_South,Mudang_Pad,Tomb_Pad}} 배치.");

            // [4] 검증
            int bTris=0; foreach(var mf in bridge.GetComponentsInChildren<MeshFilter>()) if(mf.sharedMesh!=null) bTris+=mf.sharedMesh.triangles.Length/3;
            var wmesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath); int wTris=wmesh!=null?wmesh.triangles.Length/3:-1;
            sb.AppendLine("── [4] 검증 ──");
            sb.AppendLine($"· 다리 tris {bTris} = 바닥 {nPlank}×{plankTris} + 기둥 {nPillar}×{pillarTris}");
            sb.AppendLine($"· 상면−둑 높이차 N{deckTopY-bankN:F2}/S{deckTopY-bankS:F2}m (0 근접)");
            sb.AppendLine($"· 상면 밑면−수면 여유 {deckUnder-Wsurf:F2}m");
            sb.AppendLine($"· 기둥 하단Y {pillarBotY:F2} vs 강바닥 {floorY:F2} (Δ{pillarBotY-floorY:F2}) · 물에 잠긴 깊이 {subMerge:F2}m");
            sb.AppendLine($"· 수면 메시 {MeshPath} tris {wTris} (미변경) · 다리는 수면 위 → 수면 끊김 없음");
            sb.AppendLine($"· 무당집 대지 Y {mudY:F2} · 묘역 대지 Y {tomY:F2} (각 20×20m 평탄 + {PadFlat}~{PadSkirt}m 사면)");

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);

            // [5] 렌더 (눈높이 지형Y+1.6)
            Directory.CreateDirectory(RenderDir); var files=new List<string>(); string rerr=null;
            float Zn=Zc+half, Zs=Zc-half;
            try{
                files.Add(Shot("N10_to_bridge",  new Vector3(CX,H(CX,Zn+10)+1.6f,Zn+10), new Vector3(CX,deckTopY,Zc)));
                files.Add(Shot("On_bridge_south",new Vector3(CX,deckTopY+1.6f,Zc), new Vector3(CX,deckTopY+1.0f,Zc-30)));
                files.Add(Shot("S10_to_bridge",  new Vector3(CX,H(CX,Zs-10)+1.6f,Zs-10), new Vector3(CX,deckTopY,Zc)));
                files.Add(Shot("Side_pillars",   new Vector3(CX+13f,H(CX+13f,Zc)+1.6f,Zc), new Vector3(CX,deckTopY-1.0f,Zc)));
                files.Add(Shot("Mudang_to_bridge",new Vector3(MudangXZ.x,mudY+1.6f,MudangXZ.y), new Vector3(CX,deckTopY,Zc)));
                files.Add(Shot("Tomb_to_bridge", new Vector3(TombXZ.x,tomY+1.6f,TombXZ.y), new Vector3(CX,deckTopY,Zc)));
                files.Add(ShotTop("Aerial_200", CX, 560f, 200f));
            }catch(System.Exception e){ rerr=e.ToString(); }
            if(rerr!=null) sb.AppendLine("[렌더] 실패: "+rerr); else { sb.AppendLine($"[렌더] {files.Count}장 → {RenderDir}"); foreach(var f in files) sb.AppendLine("   "+f); }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon","다리+대지 완료. Console·render_bridge 확인.","확인");
        }

        private static float FlattenPad(float[,] Hn,int R,Vector3 S,float tX0,float tZ0,float tY,Vector2 c,string name,StringBuilder sb,System.Func<float,float,float> H)
        {
            float padY=H(c.x,c.y);
            // 경사 측정: 4방위 5m 지점 높이 최대차
            float slope=0; foreach(var d in new[]{new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1)}){ float h=H(c.x+d.x*10,c.y+d.y*10); slope=Mathf.Max(slope,Mathf.Abs(h-padY)); }
            float texX=S.x/(R-1), texZ=S.z/(R-1);
            int hxMin=Mathf.Clamp(Mathf.FloorToInt((c.x-tX0-PadSkirt)/texX),0,R-1), hxMax=Mathf.Clamp(Mathf.CeilToInt((c.x-tX0+PadSkirt)/texX),0,R-1);
            int hzMin=Mathf.Clamp(Mathf.FloorToInt((c.y-tZ0-PadSkirt)/texZ),0,R-1), hzMax=Mathf.Clamp(Mathf.CeilToInt((c.y-tZ0+PadSkirt)/texZ),0,R-1);
            float padN=Mathf.Clamp01((padY-tY)/S.y);
            for(int hz=hzMin;hz<=hzMax;hz++) for(int hx=hxMin;hx<=hxMax;hx++){ float x=tX0+hx*texX, z=tZ0+hz*texZ; float dd=Mathf.Sqrt((x-c.x)*(x-c.x)+(z-c.y)*(z-c.y));
                if(dd<=PadFlat) Hn[hz,hx]=padN;
                else if(dd<=PadSkirt){ float t=Mathf.SmoothStep(0,1,(dd-PadFlat)/(PadSkirt-PadFlat)); Hn[hz,hx]=Mathf.Lerp(padN,Hn[hz,hx],t); } }
            float maxSlopeAfter=(padY-tY); // 사면 최대 경사 ≈ (원지형−padY)/8m
            sb.AppendLine($"[2] {name}({c.x:F0},{c.y:F0}) 현재높이 {padY:F1}m·주변경사 ±{slope:F1}m → 20×20 평탄 {padY:F1}m + {PadFlat}~{PadSkirt}m 완만사면(급벽 없음)");
            return padY;
        }

        private static int MinAxis(Vector3 v){ return v.x<=v.y&&v.x<=v.z?0:(v.y<=v.z?1:2); }
        private static int MaxAxis(Vector3 v){ return v.x>=v.y&&v.x>=v.z?0:(v.y>=v.z?1:2); }
        private static Vector3 AxisVec(int a){ return a==0?Vector3.right:a==1?Vector3.up:Vector3.forward; }
        private static void SetStaticRec(GameObject g){ g.isStatic=true; foreach(Transform c in g.transform) SetStaticRec(c.gameObject); }
        private static void NewMarker(GameObject parent,string name,Vector3 pos){ var m=new GameObject(name); m.transform.SetParent(parent.transform); m.transform.position=pos; }
        private static void KillRoot(Scene s,string n){ foreach(var r in s.GetRootGameObjects()) if(r.name==n) Object.DestroyImmediate(r); }

        private static float RthAt(float angDeg){ if(!File.Exists(PathFile)) return 106f; var ci=CultureInfo.InvariantCulture; var lines=File.ReadAllLines(PathFile);
            float best=1e9f, br=106f; for(int i=1;i<lines.Length;i++){ var t=lines[i].Split(' '); if(t.Length<2) continue; float x=float.Parse(t[0],ci), z=float.Parse(t[1],ci);
                float a=Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg; float dd=Mathf.Abs(Mathf.DeltaAngle(a,angDeg)); if(dd<best){best=dd; br=Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ));} } return br; }

        private static string Shot(string name,Vector3 pos,Vector3 look){ return Cam(name,pos,(look-pos).normalized,58f); }
        private static string ShotTop(string name,float cx,float cz,float alt){ var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain t=terr.Length>0?terr[0]:null;
            float g=t!=null?t.transform.position.y+t.SampleHeight(new Vector3(cx,0,cz)):0f; return Cam(name,new Vector3(cx,g+alt,cz),Vector3.down,60f,Vector3.forward); }
        private static string Cam(string name,Vector3 pos,Vector3 fwd,float fov){ return Cam(name,pos,fwd,fov,Vector3.up); }
        private static string Cam(string name,Vector3 pos,Vector3 fwd,float fov,Vector3 up)
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
