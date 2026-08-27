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
    /// 브러시로 다듬은 말굽형 물길에 "바닥 추종" 수면 메시를 만든다. 중심선 없이 방향 무관.
    /// 메뉴: [Tools ▸ Seocheon ▸ Build Stream Water]
    ///
    /// 원칙: 지형 읽기만(수정 금지). Ayo 원본 미수정(복사만). Opaque Texture·Fog 안 켬. Collider 없음.
    ///
    /// 수면 높이 = (채널 마스크 안에서만 이웃으로 인정하는) 바닥 높이 반복 블러 + 깊이 1.0m.
    ///  · 블러 유효 반경 ≈ 10m → 강 폭(12~20m)보다 커서 폭방향 평평, 강 길이보다 작아 경사 유지.
    ///  · 방향 무관 → 아치 전 구간이 채워짐. 안 채워지면 분류 안 된 것(더 파야 함)으로 좌표 보고.
    ///  · 강안(둑) − 0.05m 로 캡(범람 방지). 외곽 1칸 침식으로 경계를 둑 밑에 숨김.
    ///
    /// 코스틱 비교용 2안: A(_Voronoi_Density=20) / B(코스틱 OFF). 각각 4장 렌더.
    /// </summary>
    public static class SeocheonWaterBuilder
    {
        private const string ScenePath = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string AyoMat    = "Assets/Ayo Free Toon Water/Materials/Toon Water.mat"; // 원본(복사만)
        private const string MatPathA  = "Assets/_Project/Seocheon/Art/Materials/M_Seocheon_StreamWater_A.mat";
        private const string MatPathB  = "Assets/_Project/Seocheon/Art/Materials/M_Seocheon_StreamWater_B.mat";
        private const string MeshPath  = "Assets/_Project/Seocheon/Art/Models/Seocheon_StreamWater.asset";
        private const string RenderDir = @"C:\Users\User\_AssetBackup\render_water6";
        private const string MaskFile  = @"C:\Users\User\_AssetBackup\seocheon_stream_mask.txt"; // Normalizer가 동결한 마스크

        private const float RXMin=360f, RXMax=580f, RZMin=560f, RZMax=790f;
        private const float Grid=0.5f;               // 격자(m) — 톱니 완화
        private const float BankProbe=10f;           // 좌우 둑 참조 거리(m)
        private const float CarveThresh=0.4f;        // 주변보다 이만큼 낮으면 골짜기
        private const float BlurRadiusM=9f;          // 바닥 블러 유효 반경(m) → 폭방향 평탄화
        private const float DepthTarget=1.2f;        // 목표 수심
        private const float BankClear=0.05f;         // 둑보다 이만큼 아래로 수면 캡
        private const float CapRadiusM=2.5f;         // 국소 둑 탐색 반경(캡용)
        private const float MinDepthPass=0.3f;       // 합격 최소 수심
        private const int   GapMinCells=40;          // 이 이상인 '버려진 연결영역'은 미충전 구간으로 보고
        // UV(잔물결)
        private const float UVScale=0.5f, WarpAmp=0.25f, WarpWave=18f;

        [MenuItem("Tools/Seocheon/Build Stream Water")]
        public static void BuildStreamWater()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)){ EditorUtility.DisplayDialog("Seocheon","씬 없음.","확인"); return; }
            if (AssetDatabase.LoadAssetAtPath<Material>(AyoMat)==null){ EditorUtility.DisplayDialog("Seocheon","Ayo 워터 머티리얼을 못 찾음:\n"+AyoMat,"확인"); return; }

            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terr.Length==0){ EditorUtility.DisplayDialog("Seocheon","Terrain 없음.","확인"); return; }
            var t=terr[0]; Vector3 tpos=t.transform.position;
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] Stream Water (블러 추종, 아치 대응) =====");

            KillRoot(scene,"_Stream_Water");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath)!=null) AssetDatabase.DeleteAsset(MeshPath);

            int nx=Mathf.FloorToInt((RXMax-RXMin)/Grid)+1;
            int nz=Mathf.FloorToInt((RZMax-RZMin)/Grid)+1;
            float WX(int i)=>RXMin+i*Grid;
            float WZ(float j)=>RZMin+j*Grid;
            float Floor(float x,float z)=>tpos.y+t.SampleHeight(new Vector3(x,0f,z));

            // ── [1] 바닥 읽기 + 마스크 ──
            var H=new float[nx,nz];
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++) H[i,j]=Floor(WX(i),WZ(j));

            // 동결 마스크(Normalizer 저장) 최우선 — 평면 형태 고정. 없으면 골짜기 재검출.
            bool[,] mask; int maskCount; var gaps=new List<string>();
            if (File.Exists(MaskFile))
            {
                mask=LoadFrozenMask(nx,nz,WX,WZ,out maskCount,out int frozenCells);
                sb.AppendLine($"[채널] 동결 마스크 사용({MaskFile}): 1m {frozenCells}칸 → 0.5m 업샘플 {maskCount}칸. 평면 고정.");
            }
            else
            {
                var chan=new bool[nx,nz];
                var dir8=new Vector2[]{ new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1),
                                        new Vector2(0.707f,0.707f),new Vector2(-0.707f,0.707f),new Vector2(0.707f,-0.707f),new Vector2(-0.707f,-0.707f)};
                var ring=new float[8];
                for (int i=0;i<nx;i++) for (int j=0;j<nz;j++){ float x=WX(i), z=WZ(j);
                    for (int d=0;d<8;d++) ring[d]=Floor(x+dir8[d].x*BankProbe, z+dir8[d].y*BankProbe);
                    if (Median8(ring) - H[i,j] >= CarveThresh) chan[i,j]=true; }
                mask=LargestComponent(chan,nx,nz,out maskCount,out int totalChan,out var others);
                sb.AppendLine($"[채널] (동결 파일 없음) 골짜기 {totalChan}칸 중 최대 {maskCount}칸. 격자 {Grid}m");
                foreach (var c in others) if (c.count>=GapMinCells)
                    gaps.Add($"X{WX(Mathf.RoundToInt(c.sumI/c.count)):F0} Z{WZ(c.sumJ/(float)c.count):F0} ({c.count}칸)");
            }
            if (maskCount<20){ Debug.Log(sb.ToString()); EditorUtility.DisplayDialog("Seocheon","물길 인식 실패. Console 확인.","확인"); return; }

            // ── [2] 바닥 블러(마스크 안에서만 4-이웃) ──
            var cells=new List<Vector2Int>();
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++) if (mask[i,j]) cells.Add(new Vector2Int(i,j));
            int passes=Mathf.Max(1,Mathf.RoundToInt((BlurRadiusM/Grid)*(BlurRadiusM/Grid)));
            var cur=new float[nx,nz]; var nxt=new float[nx,nz];
            foreach (var c in cells) cur[c.x,c.y]=H[c.x,c.y];
            var nb4=new Vector2Int[]{ new Vector2Int(1,0),new Vector2Int(-1,0),new Vector2Int(0,1),new Vector2Int(0,-1)};
            for (int p=0;p<passes;p++){
                foreach (var c in cells){ int i=c.x,j=c.y; float s=cur[i,j]; int n=1;
                    foreach (var d in nb4){ int ni=i+d.x,nj=j+d.y; if(ni>=0&&nj>=0&&ni<nx&&nj<nz&&mask[ni,nj]){ s+=cur[ni,nj]; n++; } }
                    nxt[i,j]=s/n; }
                var tmp=cur; cur=nxt; nxt=tmp;
            }
            float effR=Grid*Mathf.Sqrt(passes);
            sb.AppendLine($"[블러] {passes}회(유효 반경≈{effR:F1}m, 강폭>이면 폭평탄·강길이<이면 경사유지) → 각 칸 수면 = 블러바닥 + {DepthTarget:F1}m, 둑−{BankClear}m 캡");

            // ── 수면 높이 + 국소 둑 캡 ──
            int capR=Mathf.Max(1,Mathf.RoundToInt(CapRadiusM/Grid));
            var Surf=new float[nx,nz];
            foreach (var c in cells){ int i=c.x,j=c.y;
                float bank=float.MaxValue;
                for (int di=-capR;di<=capR;di++) for (int dj=-capR;dj<=capR;dj++){ int ni=i+di,nj=j+dj; if(ni<0||nj<0||ni>=nx||nj>=nz) continue; if(!mask[ni,nj] && H[ni,nj]<bank) bank=H[ni,nj]; }
                float sRaw=cur[i,j]+DepthTarget;
                Surf[i,j]=(bank<float.MaxValue)? Mathf.Min(sRaw,bank-BankClear) : sRaw;
            }

            // ── 외곽 1칸 침식(경계를 둑 밑에 숨김) ──
            var waterMask=new bool[nx,nz];
            foreach (var c in cells){ int i=c.x,j=c.y; bool inner=true;
                foreach (var d in nb4){ int ni=i+d.x,nj=j+d.y; if(ni<0||nj<0||ni>=nx||nj>=nz||!mask[ni,nj]){ inner=false; break; } }
                if (inner) waterMask[i,j]=true;
            }

            // ── 메시(물칸: waterMask & 수면>바닥) ──
            var verts=new List<Vector3>(); var tris=new List<int>(); var uvs=new List<Vector2>(); var nrm=new List<Vector3>(); var tan=new List<Vector4>();
            float hg=Grid*0.5f; float kw=Mathf.PI*2f/WarpWave;
            int watered=0, dryCells=0, overflow=0; float minDepth=float.MaxValue; Vector2 minAt=Vector2.zero;
            var dryList=new List<string>(); var ofList=new List<string>();
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++){
                if (!mask[i,j]) continue;
                float sY=Surf[i,j];
                if (sY<=H[i,j]){ dryCells++; if(dryList.Count<20) dryList.Add($"X{WX(i):F0} Z{WZ(j):F0} 수면{sY:F2}≤바닥{H[i,j]:F2}"); continue; }
                // 범람은 실제 물/땅 경계(전체 마스크)에서 검사 — 침식 전 가장자리 포함
                foreach (var d in nb4){ int ni=i+d.x,nj=j+d.y; if(ni<0||nj<0||ni>=nx||nj>=nz) continue; if(!mask[ni,nj] && H[ni,nj]<sY){ overflow++; if(ofList.Count<20) ofList.Add($"X{WX(i):F0} Z{WZ(j):F0} 수면{sY:F2}>둑{H[ni,nj]:F2}"); break; } }
                if (!waterMask[i,j]) continue; // 침식된 외곽은 메시에서만 제외
                float depth=sY-H[i,j]; if (depth<minDepth){ minDepth=depth; minAt=new Vector2(WX(i),WZ(j)); }
                float x=WX(i), z=WZ(j); int b=verts.Count;
                verts.Add(new Vector3(x-hg,sY,z-hg)); verts.Add(new Vector3(x+hg,sY,z-hg));
                verts.Add(new Vector3(x-hg,sY,z+hg)); verts.Add(new Vector3(x+hg,sY,z+hg));
                AddUV(uvs,x-hg,z-hg,kw); AddUV(uvs,x+hg,z-hg,kw); AddUV(uvs,x-hg,z+hg,kw); AddUV(uvs,x+hg,z+hg,kw);
                for (int cc=0;cc<4;cc++){ nrm.Add(Vector3.up); tan.Add(new Vector4(1,0,0,1)); }
                tris.Add(b); tris.Add(b+2); tris.Add(b+1); tris.Add(b+1); tris.Add(b+2); tris.Add(b+3); watered++;
            }
            var mesh=new Mesh{name="Seocheon_StreamWater"}; mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetTriangles(tris,0); mesh.SetUVs(0,uvs); mesh.SetNormals(nrm); mesh.SetTangents(tan); mesh.RecalculateBounds();
            EnsureFolder("Assets/_Project/Seocheon/Art/Models");
            AssetDatabase.CreateAsset(mesh, MeshPath);
            sb.AppendLine($"[메시] 채널 {maskCount}칸 · 물칸(외곽침식 후) {watered} · tris {tris.Count/3} · verts {verts.Count} → {MeshPath}");

            // ── 수면 단조감소(동→서, X열 평균) + 폭평탄(열내 스프레드) ──
            var colMean=new float[nx]; var colCnt=new int[nx]; var colMin=new float[nx]; var colMax=new float[nx];
            for (int i=0;i<nx;i++){ colMin[i]=float.MaxValue; colMax[i]=float.MinValue; }
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++) if (mask[i,j] && Surf[i,j]>H[i,j]){ colMean[i]+=Surf[i,j]; colCnt[i]++; if(Surf[i,j]<colMin[i])colMin[i]=Surf[i,j]; if(Surf[i,j]>colMax[i])colMax[i]=Surf[i,j]; }
            var xs=new List<int>(); for (int i=0;i<nx;i++) if (colCnt[i]>0){ colMean[i]/=colCnt[i]; xs.Add(i); }
            float maxSpread=0; foreach (int i in xs) maxSpread=Mathf.Max(maxSpread,colMax[i]-colMin[i]);
            bool mono=true; var monoRev=new List<string>();
            for (int k=0;k<xs.Count-1;k++){ int iw=xs[k], ie=xs[k+1]; if (colMean[iw] > colMean[ie]+0.10f){ mono=false; if(monoRev.Count<12) monoRev.Add($"X{WX(iw):F0}(수면평균{colMean[iw]:F2}) > 동 X{WX(ie):F0}({colMean[ie]:F2})"); } }
            sb.AppendLine("[수면 프로파일 — 동→서, X열 평균] (약 10m 간격)");
            int step=Mathf.Max(1,Mathf.RoundToInt(10f/Grid));
            for (int k=xs.Count-1;k>=0;k--){ int i=xs[k]; if((xs.Count-1-k)%step==0||k==0) sb.AppendLine($"   X{WX(i):F0}  수면평균 {colMean[i]:F2}  (열내 {colCnt[i]}칸, 폭방향 편차 {colMax[i]-colMin[i]:F2})"); }
            sb.AppendLine($"[폭방향 평탄] 최대 열내 편차 {maxSpread:F2}m (두 다리가 같은 X에 걸리면 다리간 차이 포함)");

            // ── [4] 머티리얼: 코스틱 OFF(수영장 무늬 제거) + 선반사 OFF + 스펙큘러 하향. B안 삭제. ──
            var matA=MakeMaterial(MatPathA, causticsOn:false, voronoi:20f);
            if (AssetDatabase.LoadAssetAtPath<Material>(MatPathB)!=null) AssetDatabase.DeleteAsset(MatPathB);
            sb.AppendLine("[머티리얼] 코스틱 OFF · 선반사 OFF · Spec(1,1,1) · 굴절 0 · Shallow(0.45,0.72,0.68,0.10) · Deep(0.09,0.28,0.30,0.80) · Foam(1,1,1,0.85) · Wave_Scale 4 · Foam_Scale 5 · Water_Depth_Distance 1.5. (B안 삭제)");

            // 배치(기본 A 물림, Collider 없음)
            var go=new GameObject("_Stream_Water");
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mr=go.AddComponent<MeshRenderer>(); mr.sharedMaterial=matA;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);

            // ── 합격 기준 ──
            sb.AppendLine("── 합격 기준 ──");
            sb.AppendLine($"① 강안 넘은 물칸: {overflow} {(overflow==0?"✓":"⚠")}"); foreach (var s in ofList) sb.AppendLine("     ↳ "+s);
            sb.AppendLine($"② 물길인데 물 없는 칸: {dryCells} {(dryCells==0?"✓":"⚠")}"); foreach (var s in dryList) sb.AppendLine("     ↳ "+s);
            sb.AppendLine($"   미충전(비연결) 골짜기 조각 {gaps.Count}개 {(gaps.Count==0?"✓ 아치 단일연결":"⚠ 아래 좌표 — 물길로 분류/연결 안 됨, 더 파야 함")}"); foreach (var s in gaps) sb.AppendLine("     ↳ "+s);
            sb.AppendLine($"③ 수면 동→서 단조감소: {(mono?"예 ✓":"아니오 ⚠")}"); foreach (var s in monoRev) sb.AppendLine("     ↳ "+s);
            string md=minDepth==float.MaxValue?"물칸 없음":$"{minDepth:F2}m @ X{minAt.x:F0} Z{minAt.y:F0}";
            sb.AppendLine($"④ 최소 수심 ≥{MinDepthPass}m: {md} {(minDepth>=MinDepthPass?"✓":"⚠")}");
            sb.AppendLine("[URP] "+DepthOpaqueStatus());
            sb.AppendLine("[VR SPI] "+SpiStatus(matA));

            // ── [5] 렌더 4장 → render_water6 ──
            Vector3 wPt,ePt,mPt; RepPoints(mask,Surf,H,nx,nz,WX,WZ,out wPt,out ePt,out mPt);
            string rerr=null; var files=new List<string>();
            try {
                files.AddRange(RenderSet("", matA, mr, t, tpos, wPt, ePt, mPt));
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
            } catch(System.Exception e){ rerr=e.ToString(); }
            if (rerr!=null) sb.AppendLine("[렌더] 실패: "+rerr); else { sb.AppendLine($"[렌더] {files.Count}장 → {RenderDir} (지형 前 비교본은 Terrain_before.png)"); foreach (var f in files) sb.AppendLine("   "+f); }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon","수면 재생성 완료(A안).\nConsole 로그 확인.","확인");
        }

        private static Material MakeMaterial(string path, bool causticsOn, float voronoi)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path)!=null) AssetDatabase.DeleteAsset(path);
            EnsureFolder("Assets/_Project/Seocheon/Art/Materials");
            AssetDatabase.CopyAsset(AyoMat, path);
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            SetF(m,"_Refraction_Strength",0f); SetF(m,"_Refraction_Scale",0f);
            SetC(m,"_Shallow_Water_Color", new Color(0.45f,0.72f,0.68f,0.10f));
            SetC(m,"_Deep_Water_Color",    new Color(0.09f,0.28f,0.30f,0.80f));
            SetC(m,"_Foam_Color",          new Color(1f,1f,1f,0.85f));
            SetC(m,"_Specular_Color",      new Color(1f,1f,1f,1f));  // 2→1 (반짝임 하향)
            SetF(m,"_Smoothness",0.35f);                             // 스펙큘러 번짐 줄여 삼각형 이음선 완화
            SetF(m,"_Sun_Reflection_Enable",0f);                     // 선반사(줄무늬) OFF
            SetF(m,"_Water_Depth_Distance",1.5f);                  // 수심 그라디언트
            SetF(m,"_Wave_Scale",4f); SetF(m,"_Wave_Speed",0.15f); SetF(m,"_Wave_Strength",0.12f);
            SetF(m,"_Foam_Scale",5f); SetF(m,"_Foam_Amount",0.25f); SetF(m,"_Foam_Cutoff",0.25f); SetF(m,"_Foam_Speed",0.15f);
            SetV(m,"_Water_Direction", new Vector4(-1f,0f,0f,0f));
            SetF(m,"_Voronoi_Density",voronoi);
            if (causticsOn){ SetF(m,"_1st_Caustics_Enable",1f); SetF(m,"_2nd_Caustics_Enable",1f); }
            else { SetF(m,"_1st_Caustics_Enable",0f); SetF(m,"_2nd_Caustics_Enable",0f); SetF(m,"_1st_Caustics_Brightness",0f); SetF(m,"_2nd_Caustics_Brightness",0f); }
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
            return m;
        }

        private static void RepPoints(bool[,] mask,float[,] Surf,float[,] H,int nx,int nz,
                                      System.Func<int,float> WX,System.Func<float,float> WZ,
                                      out Vector3 wPt,out Vector3 ePt,out Vector3 mPt)
        {
            int minI=int.MaxValue,maxI=int.MinValue; long sI=0,sJ=0,cnt=0;
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++) if (mask[i,j] && Surf[i,j]>H[i,j]){ if(i<minI)minI=i; if(i>maxI)maxI=i; sI+=i; sJ+=j; cnt++; }
            if (cnt==0){ wPt=ePt=mPt=new Vector3(463,0,678); return; }
            Vector3 Pt(int ti){ long zz=0; int c=0; float sy=0; for(int j=0;j<nz;j++) if(mask[ti,j]&&Surf[ti,j]>H[ti,j]){ zz+=j; c++; sy+=Surf[ti,j]; } if(c==0) c=1; return new Vector3(WX(ti), sy/c, WZ(zz/(float)c)); }
            wPt=Pt(minI); ePt=Pt(maxI); mPt=Pt((minI+maxI)/2);
        }

        private static List<string> RenderSet(string prefix, Material mat, MeshRenderer mr, Terrain t, Vector3 tpos, Vector3 wPt, Vector3 ePt, Vector3 mPt)
        {
            mr.sharedMaterial=mat;
            Directory.CreateDirectory(RenderDir);
            var outFiles=new List<string>();
            const int W=1280,H=720;
            var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
            var camGO=new GameObject("__waterCam"); var cam=camGO.AddComponent<Camera>();
            cam.clearFlags=CameraClearFlags.Skybox; cam.nearClipPlane=0.02f; cam.farClipPlane=2000f; cam.enabled=false;
            void Shot(string name, Vector3 pos, Vector3 look, float fov){
                cam.transform.position=pos; cam.fieldOfView=fov; cam.transform.rotation=Quaternion.LookRotation((look-pos).normalized, Vector3.up);
                var req=new UniversalRenderPipeline.SingleCameraRequest();
                if (RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req); } else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null;
                string fn=Path.Combine(RenderDir,prefix+name+".png"); File.WriteAllBytes(fn, tex.EncodeToPNG()); outFiles.Add(fn);
            }
            try {
                Vector3 flow=(wPt-ePt); flow.y=0; flow=flow.sqrMagnitude>1e-3f?flow.normalized:Vector3.left;
                Vector3 perp=Vector3.Cross(flow,Vector3.up).normalized;
                float g0=tpos.y+t.SampleHeight(new Vector3(463f,0f,678f));
                Shot("Aerial_Top250", new Vector3(463f,g0+250f,678f), new Vector3(463f,mPt.y,678f), 60f);
                Shot("Bank_oblique",  ePt - flow*4f + Vector3.up*5f + perp*6f, mPt, 62f);
                Shot("Near_1p5m",     mPt + Vector3.up*0.9f - flow*1.2f, mPt, 55f);
                Shot("Mid_8m",        mPt + Vector3.up*3f + perp*2f - flow*7f, mPt, 50f);
            } finally { Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
            return outFiles;
        }

        private struct Comp { public int count; public long sumI,sumJ; public int minI,maxI,minJ,maxJ; }

        /// <summary>8-이웃 연결. 최대 성분 마스크 반환 + 나머지 성분 목록(out others).</summary>
        private static bool[,] LargestComponent(bool[,] src,int nx,int nz,out int bestCount,out int total,out List<Comp> others)
        {
            var label=new int[nx,nz]; total=0;
            for (int i=0;i<nx;i++) for(int j=0;j<nz;j++) if(src[i,j]) total++;
            var d8=new Vector2Int[]{ new Vector2Int(1,0),new Vector2Int(-1,0),new Vector2Int(0,1),new Vector2Int(0,-1),
                                     new Vector2Int(1,1),new Vector2Int(1,-1),new Vector2Int(-1,1),new Vector2Int(-1,-1)};
            var comps=new List<Comp>(); comps.Add(default); // index 0 미사용
            int cur=0; var st=new Stack<Vector2Int>();
            for (int i=0;i<nx;i++) for(int j=0;j<nz;j++){
                if (!src[i,j]||label[i,j]!=0) continue;
                cur++; label[i,j]=cur; st.Push(new Vector2Int(i,j));
                var cp=new Comp{count=0,sumI=0,sumJ=0,minI=int.MaxValue,maxI=int.MinValue,minJ=int.MaxValue,maxJ=int.MinValue};
                while (st.Count>0){ var c=st.Pop(); cp.count++; cp.sumI+=c.x; cp.sumJ+=c.y;
                    if(c.x<cp.minI)cp.minI=c.x; if(c.x>cp.maxI)cp.maxI=c.x; if(c.y<cp.minJ)cp.minJ=c.y; if(c.y>cp.maxJ)cp.maxJ=c.y;
                    foreach (var d in d8){ int ni=c.x+d.x,nj=c.y+d.y; if(ni<0||nj<0||ni>=nx||nj>=nz) continue; if(src[ni,nj]&&label[ni,nj]==0){ label[ni,nj]=cur; st.Push(new Vector2Int(ni,nj)); } }
                }
                comps.Add(cp);
            }
            int best=0; bestCount=0;
            for (int k=1;k<comps.Count;k++) if (comps[k].count>bestCount){ bestCount=comps[k].count; best=k; }
            others=new List<Comp>(); for (int k=1;k<comps.Count;k++) if (k!=best) others.Add(comps[k]);
            var mask=new bool[nx,nz]; for (int i=0;i<nx;i++) for(int j=0;j<nz;j++) mask[i,j]=(label[i,j]==best);
            return mask;
        }

        // Normalizer가 동결한 1m 마스크를 읽어 0.5m 격자로 업샘플(평면 형태 고정)
        private static bool[,] LoadFrozenMask(int nx,int nz,System.Func<int,float> WX,System.Func<float,float> WZ,out int maskCount,out int frozenCells)
        {
            var lines=File.ReadAllLines(MaskFile); var ci=CultureInfo.InvariantCulture;
            int nx1=0,nz1=0; float rx=RXMin,rz=RZMin,g1=1f; int maskStart=-1,cnt=0;
            for (int k=0;k<lines.Length;k++){ var tk=lines[k].Split(' '); if(tk.Length==0) continue;
                if (tk[0]=="REGION"){ rx=float.Parse(tk[1],ci); rz=float.Parse(tk[3],ci); g1=float.Parse(tk[5],ci); nx1=int.Parse(tk[6]); nz1=int.Parse(tk[7]); }
                else if (tk[0]=="MASK"){ cnt=int.Parse(tk[1]); maskStart=k+1; break; } }
            var frozen=new bool[Mathf.Max(1,nx1),Mathf.Max(1,nz1)];
            for (int k=maskStart;k>=0 && k<maskStart+cnt && k<lines.Length;k++){ var tk=lines[k].Split(' '); if(tk.Length<2) continue; int i=int.Parse(tk[0]),j=int.Parse(tk[1]); if(i>=0&&j>=0&&i<nx1&&j<nz1) frozen[i,j]=true; }
            frozenCells=cnt;
            var mask=new bool[nx,nz]; int mc=0;
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++){ float x=WX(i), z=WZ(j); int i1=Mathf.RoundToInt((x-rx)/g1), j1=Mathf.RoundToInt((z-rz)/g1); if(i1<0||j1<0||i1>=nx1||j1>=nz1) continue; if(frozen[i1,j1]){ mask[i,j]=true; mc++; } }
            maskCount=mc; return mask;
        }

        private static void AddUV(List<Vector2> uvs, float x, float z, float kw){
            uvs.Add(new Vector2(x*UVScale + WarpAmp*Mathf.Sin(z*kw), z*UVScale + WarpAmp*Mathf.Sin(x*kw)));
        }
        private static float Median8(float[] a){ var b=(float[])a.Clone(); System.Array.Sort(b); return 0.5f*(b[3]+b[4]); }
        private static void SetF(Material m,string p,float v){ if(m.HasProperty(p)) m.SetFloat(p,v); }
        private static void SetC(Material m,string p,Color v){ if(m.HasProperty(p)) m.SetColor(p,v); }
        private static void SetV(Material m,string p,Vector4 v){ if(m.HasProperty(p)) m.SetVector(p,v); }

        private static string SpiStatus(Material mat)
        {
            if (mat==null || mat.shader==null) return "머티리얼/셰이더 없음.";
            var names=mat.shader.keywordSpace.keywordNames; var hits=new List<string>(); bool spi=false, mv=false;
            foreach (var n in names){ if(n=="STEREO_INSTANCING_ON"){spi=true;hits.Add(n);} else if(n=="STEREO_MULTIVIEW_ON"){mv=true;hits.Add(n);} else if(n=="STEREO_CUBEMAP_RENDER_ON") hits.Add(n); }
            string v = spi ? "SPI 대응 확정 ✓ (STEREO_INSTANCING_ON 변형 존재)"
                           : (mv ? "Multiview 변형 존재" : "스테레오 키워드 미검출 ⚠(임포트 직후 변형 미생성일 수 있음)");
            return $"'{mat.shader.name}' 키워드 {names.Length}개 중 스테레오=[{(hits.Count>0?string.Join(", ",hits):"없음")}] → {v}";
        }

        private static string DepthOpaqueStatus()
        {
            var rp=(QualitySettings.renderPipeline as UniversalRenderPipelineAsset) ?? (GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset);
            if (rp==null) return "URP Asset 못 찾음.";
            bool depth=rp.supportsCameraDepthTexture, opaque=rp.supportsCameraOpaqueTexture;
            string msg=$"Depth Texture={depth}, Opaque Texture={opaque}(미변경). ";
            if (!depth){ var so=new SerializedObject(rp); var p=so.FindProperty("m_RequireDepthTexture"); if(p!=null){ p.boolValue=true; so.ApplyModifiedProperties(); EditorUtility.SetDirty(rp); AssetDatabase.SaveAssets(); msg+="Depth 꺼져있어 켰음. "; } }
            msg+="굴절 off라 Opaque 불필요(그대로).";
            return msg;
        }

        private static void KillRoot(Scene scene,string name){ foreach (var r in scene.GetRootGameObjects()) if (r.name==name) Object.DestroyImmediate(r); }
        private static void EnsureFolder(string path){ if (AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
    }
}
