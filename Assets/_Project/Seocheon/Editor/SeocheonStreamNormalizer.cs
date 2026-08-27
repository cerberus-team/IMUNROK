using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// 그린 링(말굽) 전체를 하나로 잇고 바닥·둑 높이를 정규화한다. ★평면(경로·굽이)은 유지 — 높이만.
    /// 메뉴: [Tools ▸ Seocheon ▸ Normalize Stream Terrain]
    ///
    /// 연결: 깊은 골(임계 0.4)이 27조각으로 끊겨 있으므로, ★그 조각들 근처(코리도) 안에서만
    ///       낮은 임계(0.10)로 얕은 골까지 포함해 잇는다. = 당신이 그린 얕은 골(브러시 자국)을
    ///       그대로 따라감(직선 연결 아님). 링 밖으로는 안 번짐(코리도 Dmax).
    /// 바닥: 링 안에서만 강한 블러(반경 20m)로 매끄럽게(협곡 메움·과높이 깎음) → 루프 완만 수위.
    /// 둑: 낮은 쪽을 바닥+1.8m(경계는 +1.5m=수면 위)로 완만(3~5m) 상승, 높은 언덕은 보존.
    /// </summary>
    public static class SeocheonStreamNormalizer
    {
        private const string ScenePath = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string CurPath   = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain.asset";
        private const string BakPath   = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain_backup_predepth.asset";
        private const string MaskFile  = @"C:\Users\User\_AssetBackup\seocheon_stream_mask.txt";
        private const string RenderDir = @"C:\Users\User\_AssetBackup\render_water6";

        private const float RXMin=360f, RXMax=580f, RZMin=560f, RZMax=790f;
        private const float Grid=1.0f;
        private const float BankProbe=25f;       // ★넓은 링의 진짜 둑까지 닿게(10→25). 10m는 링 안에서 끝나 폭 넓은 구간이 안 잡혔음
        private const float CarveDeep=0.4f;      // 확실한 깊은 골
        private const float CarveLow=0.06f;      // 얕은 틈까지(연결용) — 희미한 링도 잡게 낮춤
        private const float Dmax=18f;            // 코리도: 깊은 골에서 이 거리 안에서만 얕은 골 포함(링 밖 번짐 방지). 얕은골 게이트가 평지 확산을 막음
        private const int   GapMinCells=40;
        private const float BlurRadiusM=20f;     // 바닥 스무딩 반경(루프 완만 수위)
        private const float URISE=0.2f, UHALF=4f;
        private const float BankBase=1.5f, BankAbove=1.8f, RAMP=3f, BERM=5f;
        private const float WaterDepth=1.2f;     // 빌더 수면=바닥+1.2 → 담수깊이 기준
        private const float MinDepthPass=1.0f;

        [MenuItem("Tools/Seocheon/Normalize Stream Terrain")]
        public static void Normalize()
        {
            var td=AssetDatabase.LoadAssetAtPath<TerrainData>(CurPath);
            if (td==null){ EditorUtility.DisplayDialog("Seocheon","TerrainData 없음.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 링 전체 연결·정규화 =====");

            if (AssetDatabase.LoadAssetAtPath<TerrainData>(BakPath)!=null) AssetDatabase.DeleteAsset(BakPath);
            sb.AppendLine($"[0] 백업 {(AssetDatabase.CopyAsset(CurPath,BakPath)?"완료":"실패")} → {BakPath}");

            int R=td.heightmapResolution; Vector3 S=td.size;
            var Hn=td.GetHeights(0,0,R,R);
            float HcAt(float x,float z)=>Sample(Hn,S,R,x,z);
            sb.AppendLine($"대상 {CurPath} (해상도 {R}, size {S.x:F0}×{S.y:F0}×{S.z:F0})");

            int nx=Mathf.FloorToInt((RXMax-RXMin)/Grid)+1;
            int nz=Mathf.FloorToInt((RZMax-RZMin)/Grid)+1;
            float WX(int i)=>RXMin+i*Grid; float WZ(int j)=>RZMin+j*Grid;

            // ── 바닥 + 골짜기(깊은/얕은) ──
            var Hc=new float[nx,nz]; var bankRef=new float[nx,nz];
            var dir8=new Vector2[]{ new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1),
                                    new Vector2(.707f,.707f),new Vector2(-.707f,.707f),new Vector2(.707f,-.707f),new Vector2(-.707f,-.707f)};
            var ring0=new float[8];
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++){ float x=WX(i),z=WZ(j); Hc[i,j]=HcAt(x,z);
                for (int d=0;d<8;d++) ring0[d]=HcAt(x+dir8[d].x*BankProbe, z+dir8[d].y*BankProbe); bankRef[i,j]=Median8(ring0); }
            var mDeep=new bool[nx,nz]; var mLow=new bool[nx,nz]; int deepCnt=0;
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++){ float carve=bankRef[i,j]-Hc[i,j];
                if(carve>=CarveDeep){ mDeep[i,j]=true; deepCnt++; } if(carve>=CarveLow) mLow[i,j]=true; }

            // ── 연결: 깊은 골 ∪ (깊은 골 코리도 Dmax 안의 얕은 골) → 최대 연결영역 = 링 ──
            var dNear=DistToTrue(mDeep,nx,nz); // 깊은 골까지 거리(m)
            var cand=new bool[nx,nz];
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++) cand[i,j]= mDeep[i,j] || (mLow[i,j] && dNear[i,j]<=Dmax);
            int candComps=CountComponents(cand,nx,nz,GapMinCells);
            var ring=LargestComponent(cand,nx,nz,out int ringCnt);
            int newCnt=0, deepInRing=0;
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++) if(ring[i,j]){ if(mDeep[i,j]) deepInRing++; else newCnt++; }
            sb.AppendLine($"[연결] 깊은 골 {deepCnt}칸(27조각) → 코리도 얕은 골로 연결. 링(최대 연결영역) {ringCnt}칸");
            sb.AppendLine($"   그중 새로 추가(팔) 칸 {newCnt} · 기존 깊은 골 {deepInRing}. 연결 조각 수(≥{GapMinCells}칸): {candComps} {(candComps==1?"→ 하나로 이어짐 ✓":"→ ⚠ 아직 끊긴 틈 있음(아래)")}");
            if (candComps>1){
                foreach (var c in ComponentList(cand,nx,nz)) if(c.count>=GapMinCells && !ContainsRing(c,ring,nx,nz))
                    sb.AppendLine($"     ↳ 미연결 조각 X{WX(Mathf.RoundToInt(c.sumI/(float)c.count)):F0} Z{WZ(Mathf.RoundToInt(c.sumJ/(float)c.count)):F0} ({c.count}칸) — 틈 {Dmax:F0}m↑, 이 부분은 손으로 더 파야 함");
            }

            // ── 바닥 블러(링 안에서만) ──
            var rcells=new List<Vector2Int>(); for(int i=0;i<nx;i++) for(int j=0;j<nz;j++) if(ring[i,j]) rcells.Add(new Vector2Int(i,j));
            int passes=Mathf.Max(1,Mathf.RoundToInt((BlurRadiusM/Grid)*(BlurRadiusM/Grid)));
            var cur=new float[nx,nz]; var nxt=new float[nx,nz]; foreach(var c in rcells) cur[c.x,c.y]=Hc[c.x,c.y];
            var nb4=new Vector2Int[]{ new Vector2Int(1,0),new Vector2Int(-1,0),new Vector2Int(0,1),new Vector2Int(0,-1)};
            for (int p=0;p<passes;p++){ foreach(var c in rcells){ int i=c.x,j=c.y; float s=cur[i,j]; int n=1;
                foreach(var d in nb4){ int ni=i+d.x,nj=j+d.y; if(ni>=0&&nj>=0&&ni<nx&&nj<nz&&ring[ni,nj]){ s+=cur[ni,nj]; n++; } } nxt[i,j]=s/n; }
                var tmp=cur; cur=nxt; nxt=tmp; }
            var blurred=cur;
            sb.AppendLine($"[바닥] 링 안 블러 {passes}회(반경≈{Grid*Mathf.Sqrt(passes):F0}m) → 협곡 메움·과높이 깎음, 루프 완만.");

            float NearestRingFloor(float x,float z){ int ci=Mathf.RoundToInt((x-RXMin)/Grid), cj=Mathf.RoundToInt((z-RZMin)/Grid);
                if(ci>=0&&cj>=0&&ci<nx&&cj<nz&&ring[ci,cj]) return blurred[ci,cj];
                float bd=float.MaxValue,bf=2f; for(int di=-7;di<=7;di++)for(int dj=-7;dj<=7;dj++){ int ni=ci+di,nj=cj+dj; if(ni<0||nj<0||ni>=nx||nj>=nz||!ring[ni,nj])continue; float dd=di*di+dj*dj; if(dd<bd){bd=dd;bf=blurred[ni,nj];}} return bf; }
            bool InRing(float x,float z){ int i=Mathf.RoundToInt((x-RXMin)/Grid), j=Mathf.RoundToInt((z-RZMin)/Grid); return i>=0&&j>=0&&i<nx&&j<nz&&ring[i,j]; }

            // 동결 저장(빌더 공유)
            SaveMask(nx,nz,ring,ringCnt);
            sb.AppendLine($"[동결 저장] {MaskFile}");

            // ── 렌더 前 ──
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            Terrain terrain=terr.Length>0?terr[0]:null;
            string beforePng=terrain!=null?RenderAerial(terrain,"Terrain_before"):"(terrain 없음)";

            // ── 텍셀 높이 수정 ──
            int hxMin=Mathf.Clamp(Mathf.FloorToInt(RXMin/S.x*(R-1))-2,0,R-1), hxMax=Mathf.Clamp(Mathf.CeilToInt(RXMax/S.x*(R-1))+2,0,R-1);
            int hzMin=Mathf.Clamp(Mathf.FloorToInt(RZMin/S.z*(R-1))-2,0,R-1), hzMax=Mathf.Clamp(Mathf.CeilToInt(RZMax/S.z*(R-1))+2,0,R-1);
            float texX=S.x/(R-1), texZ=S.z/(R-1);
            var member=new bool[R,R];
            for (int hz=hzMin;hz<=hzMax;hz++) for (int hx=hxMin;hx<=hxMax;hx++){ float x=hx*texX, z=hz*texZ; if(InRing(x,z)) member[hz,hx]=true; }
            var dToMember=DistTransform(member,true, hxMin,hxMax,hzMin,hzMax,texX,texZ);
            var dToNon   =DistTransform(member,false,hxMin,hxMax,hzMin,hzMax,texX,texZ);

            int edited=0;
            for (int hz=hzMin;hz<=hzMax;hz++) for (int hx=hxMin;hx<=hxMax;hx++){
                float x=hx*texX, z=hz*texZ; if(x<RXMin-BERM||x>RXMax+BERM||z<RZMin-BERM||z>RZMax+BERM) continue;
                if (member[hz,hx]){ float bf=NearestRingFloor(x,z); float dE=dToNon[hz,hx];
                    Hn[hz,hx]=Mathf.Clamp01((bf+URISE*(1f-Mathf.Clamp01(dE/UHALF)))/S.y); edited++; }
                else { float dM=dToMember[hz,hx]; if(dM<=BERM){ float bf=NearestRingFloor(x,z);
                    float ramp=(bf+BankBase)+(BankAbove-BankBase)*Mathf.Clamp01(dM/RAMP);
                    Hn[hz,hx]=Mathf.Clamp01(Mathf.Max(Hn[hz,hx]*S.y,ramp)/S.y); edited++; } }
            }
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
            if (terrain!=null) terrain.Flush();
            sb.AppendLine($"[높이 수정] 텍셀 {edited}개(멤버=바닥U, 낮은둑 +{BankAbove}m). 마스크 밖 미변경.");

            // ── 검증(수정 후) ──
            var He=td.GetHeights(0,0,R,R); float HeAt(float x,float z)=>Sample(He,S,R,x,z);
            // 담수깊이 = 8방향 최저 둑마루 − 바닥 (방향 무관). 링 셀 표본.
            float minDepth=float.MaxValue; string minAt=""; int nSamp=0; int belowCnt=0;
            int stepC=Mathf.Max(1,rcells.Count/400);
            for (int s=0;s<rcells.Count;s+=stepC){ var c=rcells[s]; float x=WX(c.x), z=WZ(c.y);
                float dep=RimDepth(HeAt,x,z); nSamp++; if(dep<MinDepthPass) belowCnt++; if(dep<minDepth){ minDepth=dep; minAt=$"X{x:F0} Z{z:F0}"; } }
            sb.AppendLine($"\n[검증] 담수깊이(8방향 최저둑−바닥) 표본 {nSamp}개: 최소 {minDepth:F2}m @{minAt}, <{MinDepthPass}m 인 표본 {belowCnt}개 {(belowCnt==0?"✓ 전 지점 ≥1.0":"⚠")}");
            // 예상 물없는 칸: 수면(≈바닥+1.2)이 지형바닥보다 항상 위 → 0
            sb.AppendLine("[검증] 예상 물없는 칸 0(수면=바닥+1.2 > 바닥). 실제는 Build에서 확정.");
            sb.AppendLine($"[검증] 링 연결 조각 수 {candComps} {(candComps==1?"= 1 ✓(하나로 이어짐)":"⚠(위 미연결 조각 참고)")}");
            sb.AppendLine($"[검증] 최종 채널 칸 {ringCnt} (새로 추가 {newCnt}, 기존 {deepInRing})");

            string afterPng=terrain!=null?RenderAerial(terrain,"Terrain_after"):"(terrain 없음)";
            sb.AppendLine($"\n[렌더] 前 {beforePng}\n       後 {afterPng}  (두 장 나란히 놓고 링 모양 동일한지 확인)");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon",
                $"링 정규화 완료.\n채널 {ringCnt}칸(새 {newCnt}), 연결조각 {candComps}, 최소담수 {minDepth:F2}m.\n다음: Build Stream Water 실행.","확인");
        }

        // 8방향으로 훑어 각 방향 둑마루(crest)를 찾고, 그 최저값 − 바닥 = 넘치는 최저 깊이
        private static float RimDepth(System.Func<float,float,float> Hf, float sx, float sz)
        {
            var dirs=new Vector2[]{ new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1),
                                    new Vector2(.707f,.707f),new Vector2(-.707f,.707f),new Vector2(.707f,-.707f),new Vector2(-.707f,-.707f)};
            float floor=Hf(sx,sz); float minCrest=float.MaxValue;
            foreach (var d in dirs){ float maxH=float.MinValue;
                for (float t=0.25f;t<=20f;t+=0.25f){ float h=Hf(sx+d.x*t, sz+d.y*t); if(h<floor)floor=h; if(h>maxH)maxH=h; if(h<maxH-0.2f) break; }
                if(maxH<minCrest) minCrest=maxH; }
            return minCrest-floor;
        }

        private static void SaveMask(int nx,int nz,bool[,] mask,int cnt)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MaskFile)); var sw=new StringBuilder();
            sw.AppendLine($"REGION {RXMin} {RXMax} {RZMin} {RZMax} {Grid} {nx} {nz}");
            sw.AppendLine($"MASK {cnt}");
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++) if (mask[i,j]) sw.AppendLine($"{i} {j}");
            File.WriteAllText(MaskFile, sw.ToString());
        }

        private static string RenderAerial(Terrain t,string name)
        {
            try{ Directory.CreateDirectory(RenderDir);
                float g0=t.transform.position.y + t.SampleHeight(new Vector3(463f,0f,678f));
                const int W=1280,H=720; var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
                var camGO=new GameObject("__nrmCam"); var cam=camGO.AddComponent<Camera>();
                cam.clearFlags=CameraClearFlags.Skybox; cam.fieldOfView=60f; cam.nearClipPlane=0.1f; cam.farClipPlane=2000f; cam.enabled=false;
                cam.transform.position=new Vector3(463f,g0+250f,678f); cam.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
                string fn=Path.Combine(RenderDir,name+".png");
                try{ var req=new UniversalRenderPipeline.SingleCameraRequest(); if(RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req);} else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                    RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null; File.WriteAllBytes(fn,tex.EncodeToPNG()); }
                finally{ Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
                return fn; } catch(System.Exception e){ return "예외:"+e.Message; }
        }

        // 1m 격자: true 셀까지 최단거리(m), 8-이웃 BFS(단위 스텝 근사)
        private static float[,] DistToTrue(bool[,] src,int nx,int nz)
        {
            var dist=new float[nx,nz]; for(int i=0;i<nx;i++)for(int j=0;j<nz;j++) dist[i,j]=float.MaxValue;
            var q=new Queue<int>(); for(int i=0;i<nx;i++)for(int j=0;j<nz;j++) if(src[i,j]){ dist[i,j]=0; q.Enqueue(i*nz+j); }
            int[,] d8={{1,0},{-1,0},{0,1},{0,-1},{1,1},{1,-1},{-1,1},{-1,-1}};
            while(q.Count>0){ int c=q.Dequeue(); int ci=c/nz,cj=c%nz;
                for(int d=0;d<8;d++){ int ni=ci+d8[d,0],nj=cj+d8[d,1]; if(ni<0||nj<0||ni>=nx||nj>=nz) continue; float w=(d<4)?Grid:Grid*1.41421f; if(dist[ci,cj]+w<dist[ni,nj]-1e-4f){ dist[ni,nj]=dist[ci,cj]+w; q.Enqueue(ni*nz+nj); } } }
            return dist;
        }

        // 텍셀 격자 다중소스 BFS 거리장(m)
        private static float[,] DistTransform(bool[,] member,bool toMember,int hxMin,int hxMax,int hzMin,int hzMax,float texX,float texZ)
        {
            int R=member.GetLength(0); var dist=new float[R,R]; for(int a=0;a<R;a++)for(int b=0;b<R;b++) dist[a,b]=float.MaxValue;
            var q=new Queue<int>();
            for (int hz=hzMin;hz<=hzMax;hz++) for (int hx=hxMin;hx<=hxMax;hx++){ bool src=toMember?member[hz,hx]:!member[hz,hx]; if(src){ dist[hz,hx]=0; q.Enqueue(hz*R+hx);} }
            int[,] d8={{1,0},{-1,0},{0,1},{0,-1},{1,1},{1,-1},{-1,1},{-1,-1}}; float step=(texX+texZ)*0.5f, diag=step*1.41421f;
            while(q.Count>0){ int c=q.Dequeue(); int cz=c/R, cx=c%R;
                for(int d=0;d<8;d++){ int nz2=cz+d8[d,0], nx2=cx+d8[d,1]; if(nx2<hxMin||nx2>hxMax||nz2<hzMin||nz2>hzMax) continue; float w=(d<4)?step:diag; if(dist[cz,cx]+w<dist[nz2,nx2]-1e-4f){ dist[nz2,nx2]=dist[cz,cx]+w; q.Enqueue(nz2*R+nx2);} } }
            return dist;
        }

        private struct Comp { public int count; public long sumI,sumJ; public int minI,maxI,minJ,maxJ; }

        private static bool[,] LargestComponent(bool[,] src,int nx,int nz,out int bestCount)
        {
            var label=Label(src,nx,nz,out var comps); int best=0; bestCount=0;
            for(int k=1;k<comps.Count;k++) if(comps[k].count>bestCount){ bestCount=comps[k].count; best=k; }
            var mask=new bool[nx,nz]; for(int i=0;i<nx;i++)for(int j=0;j<nz;j++) mask[i,j]=(label[i,j]==best); return mask;
        }
        private static int CountComponents(bool[,] src,int nx,int nz,int minCells)
        { Label(src,nx,nz,out var comps); int n=0; for(int k=1;k<comps.Count;k++) if(comps[k].count>=minCells) n++; return n; }
        private static List<Comp> ComponentList(bool[,] src,int nx,int nz){ Label(src,nx,nz,out var comps); comps.RemoveAt(0); return comps; }
        private static bool ContainsRing(Comp c,bool[,] ring,int nx,int nz)
        { for(int i=c.minI;i<=c.maxI;i++) for(int j=c.minJ;j<=c.maxJ;j++) if(i>=0&&j>=0&&i<nx&&j<nz&&ring[i,j]) return true; return false; }

        private static int[,] Label(bool[,] src,int nx,int nz,out List<Comp> comps)
        {
            var label=new int[nx,nz]; comps=new List<Comp>{ default }; int[,] d8={{1,0},{-1,0},{0,1},{0,-1},{1,1},{1,-1},{-1,1},{-1,-1}};
            var st=new Stack<int>(); int cur=0;
            for(int i=0;i<nx;i++)for(int j=0;j<nz;j++){ if(!src[i,j]||label[i,j]!=0) continue; cur++; label[i,j]=cur; st.Push(i*nz+j);
                var cp=new Comp{count=0,sumI=0,sumJ=0,minI=int.MaxValue,maxI=int.MinValue,minJ=int.MaxValue,maxJ=int.MinValue};
                while(st.Count>0){ int c=st.Pop(); int ci=c/nz,cj=c%nz; cp.count++; cp.sumI+=ci; cp.sumJ+=cj;
                    if(ci<cp.minI)cp.minI=ci; if(ci>cp.maxI)cp.maxI=ci; if(cj<cp.minJ)cp.minJ=cj; if(cj>cp.maxJ)cp.maxJ=cj;
                    for(int d=0;d<8;d++){ int ni=ci+d8[d,0],nj=cj+d8[d,1]; if(ni<0||nj<0||ni>=nx||nj>=nz) continue; if(src[ni,nj]&&label[ni,nj]==0){ label[ni,nj]=cur; st.Push(ni*nz+nj);} } }
                comps.Add(cp); }
            return label;
        }

        private static float Median8(float[] a){ var b=(float[])a.Clone(); System.Array.Sort(b); return 0.5f*(b[3]+b[4]); }
        private static float Sample(float[,] Hn,Vector3 size,int R,float x,float z)
        { float u=x/size.x,v=z/size.z; float fx=Mathf.Clamp(u*(R-1),0,R-1),fz=Mathf.Clamp(v*(R-1),0,R-1);
          int x0=(int)fx,z0=(int)fz,x1=Mathf.Min(x0+1,R-1),z1=Mathf.Min(z0+1,R-1); float tx=fx-x0,tz=fz-z0;
          return Mathf.Lerp(Mathf.Lerp(Hn[z0,x0],Hn[z0,x1],tx),Mathf.Lerp(Hn[z1,x0],Hn[z1,x1],tx),tz)*size.y; }
    }
}
