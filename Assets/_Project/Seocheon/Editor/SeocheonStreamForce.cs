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
    /// 수면 우선 강제 방식. 지형을 측정해 물을 찾지 않는다.
    ///  1) [Stream Force ▸ 1 Extract Path] : 그린 링을 중심(463,678) 기준 각도별 반경함수 r(θ)로 재구성 → 각도당 1점, 자기교차 불가한 닫힌 원형 폴리라인. 지형 미수정(+백업, 경로 렌더).
    ///  3) [Stream Force ▸ 3 Clean & Rebuild] : 원본 복원 → 링만 강제(선분거리 기준, 폭 14m 고정, 바닥=W−1.5·둑=W+0.8) → 연속 수면 메시(높이=W, 지형으로 밀어올리지 않음) → 머티리얼 → 검증 렌더.
    /// ★Ayo 원본·SUIMONO 미변경, Opaque·Fog 안 켬. git 무변경.
    /// </summary>
    public static class SeocheonStreamForce
    {
        private const string ScenePath = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string CurPath   = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain.asset";
        private const string BakPath   = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain_backup_prefill.asset";
        private const string AyoMat    = "Assets/Ayo Free Toon Water/Materials/Toon Water.mat";
        private const string MatPathA  = "Assets/_Project/Seocheon/Art/Materials/M_Seocheon_StreamWater_A.mat";
        private const string MeshPath  = "Assets/_Project/Seocheon/Art/Models/Seocheon_StreamWater.asset";
        private const string PathFile  = @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const string RenderDir = @"C:\Users\User\_AssetBackup\render_final";
        private const string PackTerrain = "Assets/Naganeupseong/Scene/Resource/Terrain.asset"; // 원본 미개간 지형(읽기만)
        private const float BandIn=65f; // 이 반경 안(마을)만 보존, 밖은 원본 복원

        private const float RXMin=360f, RXMax=580f, RZMin=560f, RZMax=790f;
        private const float CX=463f, CZ=678f;         // 링 중심(마을)
        private const float RadMin=60f, RadMax=175f;  // 링 검출 반경 밴드
        private const float BankProbe=25f, CarveMin=0.12f;
        private const int   NB=240;                   // 각도 빈(=경로 점 수, 닫힌 루프)
        private const int   RSmoothWin=20, RSmoothIter=3; // r(θ) 래핑 스무딩

        private const float ChanWidth=14f;   // ★전 구간 고정 폭
        private const float WetTol=1f;       // wet = 반폭 + WetTol(=8m), 수면선이 여기서 지형과 만남
        private const float BankW=3f;        // 수면선 → 둑 상단까지 폭(낮은 둑)
        private const float Depth=1.5f, BankAbove=0.8f;
        private const float MeshStep=0.5f;   // 경로 리샘플·격자 간격
        private const float UVScale=0.5f, WarpAmp=0.25f, WarpWave=18f;

        // 레거시(2 Force & Fill)용
        private const float WMinWidth=8f, WMaxWidth=16f, EdgeDepth=0.2f, BERM=4f, Inset=0.3f;
        private const int   NCross=4;

        // ─────────────────────────── 1) 경로 추출: r(θ) 재구성 ───────────────────────────
        [MenuItem("Tools/Seocheon/Stream Force/1 Extract Path")]
        public static void ExtractPath()
        {
            var td=AssetDatabase.LoadAssetAtPath<TerrainData>(CurPath);
            if (td==null){ EditorUtility.DisplayDialog("Seocheon","TerrainData 없음.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 1 Extract Path (r(θ) 재구성) =====");
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(BakPath)==null)
                sb.AppendLine($"[0] 백업 {(AssetDatabase.CopyAsset(CurPath,BakPath)?"완료":"실패")} → {BakPath}");
            else sb.AppendLine($"[0] 백업 이미 존재(원본 보존) → {BakPath}");

            int R=td.heightmapResolution; Vector3 S=td.size; var Hn=td.GetHeights(0,0,R,R);
            float HcAt(float x,float z)=>Sample(Hn,S,R,x,z);

            // ── 링 셀 → 각도 빈별 반경 수집 ──
            var radii=new List<float>[NB]; for(int b=0;b<NB;b++) radii[b]=new List<float>();
            var sumG=new double[NB]; var cnt=new int[NB]; var ringSamp=new float[8];
            for (float x=RXMin;x<=RXMax;x+=1f) for (float z=RZMin;z<=RZMax;z+=1f){
                float dx=x-CX, dz=z-CZ; float r=Mathf.Sqrt(dx*dx+dz*dz); if(r<RadMin||r>RadMax) continue;
                float h=HcAt(x,z); for(int d=0;d<8;d++){ var dd=Dir8(d); ringSamp[d]=HcAt(x+dd.x*BankProbe,z+dd.y*BankProbe); }
                if (Median8(ringSamp)-h < CarveMin) continue;
                float ang=Mathf.Atan2(dz,dx); int bin=Mathf.Clamp(Mathf.FloorToInt((ang+Mathf.PI)/(2f*Mathf.PI)*NB),0,NB-1);
                radii[bin].Add(r); sumG[bin]+=h; cnt[bin]++;
            }
            // 빈별 반경 중앙값 r(θ) + 바닥 g(θ)
            var has=new bool[NB]; var rB=new float[NB]; var gB=new float[NB]; int used=0;
            for (int b=0;b<NB;b++){ if(cnt[b]>0){ has[b]=true; used++; rB[b]=Median(radii[b]); gB[b]=(float)(sumG[b]/cnt[b]); } }
            sb.AppendLine($"[검출] 각도 빈 {NB}개 중 {used}개에 링 셀 존재(밴드 {RadMin:F0}~{RadMax:F0}m, carve≥{CarveMin})");
            if (used<20){ Debug.Log(sb.ToString()); EditorUtility.DisplayDialog("Seocheon","링 검출 실패.","확인"); return; }

            // 미검출 빈(북쪽 개구부 포함) 원형 보간 → 강한 래핑 스무딩(창 20, 3회) → 각도당 정확히 1점
            CircInterp(rB,has,NB); CircInterp(gB,has,NB);
            for (int it=0;it<RSmoothIter;it++){ SmoothLoop(rB,RSmoothWin); SmoothLoop(gB,RSmoothWin); }

            // 경로 점 = 중심 + r·(cosθ,sinθ), 폭 14m 고정, 닫힌 루프
            var ax=new float[NB]; var az=new float[NB]; var aw=new float[NB]; var ag=new float[NB];
            for (int b=0;b<NB;b++){ float th=-Mathf.PI+((b+0.5f)/NB)*2f*Mathf.PI; ax[b]=CX+rB[b]*Mathf.Cos(th); az[b]=CZ+rB[b]*Mathf.Sin(th); aw[b]=ChanWidth; ag[b]=gB[b]; }

            Directory.CreateDirectory(Path.GetDirectoryName(PathFile));
            var ci=CultureInfo.InvariantCulture; var sw=new StringBuilder();
            sw.AppendLine($"PATH {NB} {CX.ToString(ci)} {CZ.ToString(ci)}");
            for (int b=0;b<NB;b++) sw.AppendLine($"{ax[b].ToString(ci)} {az[b].ToString(ci)} {aw[b].ToString(ci)} {ag[b].ToString(ci)}");
            File.WriteAllText(PathFile, sw.ToString());
            float arcLen=0; for(int b=1;b<=NB;b++){ int p=b%NB,q=b-1; arcLen+=Mathf.Sqrt((ax[p]-ax[q])*(ax[p]-ax[q])+(az[p]-az[q])*(az[p]-az[q])); }
            sb.AppendLine($"[경로] 닫힌 루프 {NB}점, 둘레≈{arcLen:F0}m, 반경 r(θ) {Mn(rB):F1}~{Mx(rB):F1}m, 폭 {ChanWidth}m 고정 → {PathFile}");

            // 렌더: 작업 전 + 경로 오버레이(닫힌 리본)
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            KillRoot(scene,"_Stream_Water");
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null;
            string before=terrain!=null?RenderAerial(terrain,"Before"):"(no terrain)";
            string overlay="(no terrain)";
            if (terrain!=null){
                var ox=new float[NB+1]; var oz=new float[NB+1]; var ow=new float[NB+1]; var og=new float[NB+1];
                for(int b=0;b<NB;b++){ ox[b]=ax[b]; oz[b]=az[b]; ow[b]=aw[b]; og[b]=ag[b]; } ox[NB]=ax[0]; oz[NB]=az[0]; ow[NB]=aw[0]; og[NB]=ag[0];
                var rx=new List<float>(); var rz=new List<float>(); var rw=new List<float>(); var ry=new List<float>();
                Resample(ox,oz,ow,og,NB+1,MeshStep, rx,rz,rw,ry);
                var hy=new float[rx.Count]; var wConst=new float[rx.Count];
                for(int p=0;p<rx.Count;p++){ hy[p]=terrain.transform.position.y+terrain.SampleHeight(new Vector3(rx[p],0,rz[p]))+3f; wConst[p]=8f; }
                var mesh=BuildRibbon(rx,rz,wConst,hy,0f);
                var go=new GameObject("__pathOverlay"); go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var mr=go.AddComponent<MeshRenderer>(); mr.sharedMaterial=BrightMat();
                overlay=RenderAerial(terrain,"Path_overlay");
                Object.DestroyImmediate(go); Object.DestroyImmediate(mesh);
            }
            sb.AppendLine($"[렌더] {before}\n       {overlay}  ← 그린 링과 비교. 맞으면 '3 Clean & Rebuild' 실행.");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"경로 추출 완료(r(θ) 닫힌 루프 {NB}점).\nPath_overlay.png 비교 후\n'Stream Force ▸ 3 Clean & Rebuild (pristine)' 실행.","확인");
        }

        // ─────────────────────────── 2) (레거시) 강제 + 충수 ───────────────────────────
        [MenuItem("Tools/Seocheon/Stream Force/2 Force & Fill")]
        public static void ForceAndFill()
        {
            if (!File.Exists(PathFile)){ EditorUtility.DisplayDialog("Seocheon","경로 파일 없음. 먼저 '1 Extract Path' 실행.","확인"); return; }
            var td=AssetDatabase.LoadAssetAtPath<TerrainData>(CurPath);
            if (td==null){ EditorUtility.DisplayDialog("Seocheon","TerrainData 없음.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 2 Force & Fill (레거시) =====");
            var lines=File.ReadAllLines(PathFile); var ci=CultureInfo.InvariantCulture;
            var t0=lines[0].Split(' '); int L=int.Parse(t0[1]);
            var px=new float[L]; var pz=new float[L]; var pw=new float[L]; var pg=new float[L];
            for (int p=0;p<L;p++){ var tk=lines[p+1].Split(' '); px[p]=float.Parse(tk[0],ci); pz[p]=float.Parse(tk[1],ci); pw[p]=float.Parse(tk[2],ci); pg[p]=float.Parse(tk[3],ci); }
            var rx=new List<float>(); var rz=new List<float>(); var rw=new List<float>(); var rg=new List<float>();
            Resample(px,pz,pw,pg,L,MeshStep, rx,rz,rw,rg); int M=rx.Count;
            var Ws=new float[M]; for(int p=0;p<M;p++) Ws[p]=rg[p]; var tmp=(float[])Ws.Clone();
            for (int it=0;it<40;it++){ for(int p=0;p<M;p++){ float s=Ws[p]; int n=1; for(int q=-8;q<=8;q++){ int k=p+q; if(k>=0&&k<M){ s+=Ws[k]; n++; } } tmp[p]=s/n; } var sw2=Ws; Ws=tmp; tmp=sw2; }
            int R=td.heightmapResolution; Vector3 S=td.size; var Hn=td.GetHeights(0,0,R,R);
            float texX=S.x/(R-1), texZ=S.z/(R-1);
            float maxW=0; for(int p=0;p<M;p++) maxW=Mathf.Max(maxW,rw[p]);
            float pad=maxW*0.5f+BERM+2f;
            int hxMin=Mathf.Clamp(Mathf.FloorToInt((Mn(rx)-pad)/S.x*(R-1)),0,R-1), hxMax=Mathf.Clamp(Mathf.CeilToInt((Mx(rx)+pad)/S.x*(R-1)),0,R-1);
            int hzMin=Mathf.Clamp(Mathf.FloorToInt((Mn(rz)-pad)/S.z*(R-1)),0,R-1), hzMax=Mathf.Clamp(Mathf.CeilToInt((Mx(rz)+pad)/S.z*(R-1)),0,R-1);
            int forced=0;
            for (int hz=hzMin;hz<=hzMax;hz++) for (int hx=hxMin;hx<=hxMax;hx++){
                float x=hx*texX, z=hz*texZ; int k=Nearest(rx,rz,M,x,z, out float d);
                float halfw=rw[k]*0.5f, W=Ws[k];
                if (d<=halfw){ float f=d/halfw; float floor=W-Depth+(Depth-EdgeDepth)*f*f; Hn[hz,hx]=Mathf.Clamp01(floor/S.y); forced++; }
                else if (d<=halfw+BERM){ float tt=(d-halfw)/BERM; float target=(W-EdgeDepth)+(BankAbove+EdgeDepth)*tt; Hn[hz,hx]=Mathf.Clamp01(Mathf.Max(Hn[hz,hx]*S.y,target)/S.y); forced++; }
            }
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null; if(terrain!=null) terrain.Flush();
            KillRoot(scene,"_Stream_Water");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath)!=null) AssetDatabase.DeleteAsset(MeshPath);
            var wInset=new float[M]; for(int p=0;p<M;p++) wInset[p]=Mathf.Max(1f, rw[p]-2f*Inset);
            var mesh=BuildRibbon(rx,rz,wInset,Ws,0f); mesh.name="Seocheon_StreamWater";
            EnsureFolder("Assets/_Project/Seocheon/Art/Models"); AssetDatabase.CreateAsset(mesh,MeshPath);
            var mat=MakeWaterMat();
            var go=new GameObject("_Stream_Water"); go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=mat;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);
            var files=new List<string>(); string rerr=null;
            try{ files=Render6(terrain,td,S,R,rx,rz,Ws,M); }catch(System.Exception e){ rerr=e.ToString(); }
            if (rerr!=null) sb.AppendLine("[렌더] 실패: "+rerr); else sb.AppendLine($"[렌더] {files.Count}장 → {RenderDir}");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", "레거시 완료. 권장: '3 Clean & Rebuild'.","확인");
        }

        // ─────────────────────────── 3) 원본 복원 + 링만(★반경 r(θ) 기준·고정폭·수면=W) ───────────────────────────
        [MenuItem("Tools/Seocheon/Stream Force/3 Clean & Rebuild (pristine)")]
        public static void CleanRebuild()
        {
            if (!File.Exists(PathFile)){ EditorUtility.DisplayDialog("Seocheon","경로 파일 없음. '1 Extract Path' 먼저.","확인"); return; }
            var packTd=AssetDatabase.LoadAssetAtPath<TerrainData>(PackTerrain);
            if (packTd==null){ EditorUtility.DisplayDialog("Seocheon","원본(팩) 지형 없음:\n"+PackTerrain,"확인"); return; }
            var td=AssetDatabase.LoadAssetAtPath<TerrainData>(CurPath);
            if (td==null){ EditorUtility.DisplayDialog("Seocheon","TerrainData 없음.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 3 Clean & Rebuild (pristine) =====");

            int Rp=packTd.heightmapResolution; Vector3 Sp=packTd.size; var Hp=packTd.GetHeights(0,0,Rp,Rp);
            int R=td.heightmapResolution; Vector3 S=td.size; var Hn=td.GetHeights(0,0,R,R);
            float PristineAt(float x,float z)=>Sample(Hp,Sp,Rp,x,z);
            float texX=S.x/(R-1), texZ=S.z/(R-1);

            // 경로 로드(닫힌 r(θ) 루프) + 약한 래핑 스무딩 + 닫힌 0.5m 리샘플
            var lines=File.ReadAllLines(PathFile); var ci=CultureInfo.InvariantCulture;
            int L=int.Parse(lines[0].Split(' ')[1]);
            var px=new float[L]; var pz=new float[L]; var pw=new float[L]; var pg=new float[L];
            for (int p=0;p<L;p++){ var tk=lines[p+1].Split(' '); px[p]=float.Parse(tk[0],ci); pz[p]=float.Parse(tk[1],ci); pw[p]=float.Parse(tk[2],ci); pg[p]=float.Parse(tk[3],ci); }
            var cpx=(float[])px.Clone(); var cpz=(float[])pz.Clone();
            SmoothLoop(cpx,6); SmoothLoop(cpz,6);
            var rpx=new float[L+1]; var rpz=new float[L+1]; var rpw=new float[L+1]; var rpg=new float[L+1];
            for (int p=0;p<L;p++){ rpx[p]=cpx[p]; rpz[p]=cpz[p]; rpw[p]=ChanWidth; rpg[p]=pg[p]; }
            rpx[L]=cpx[0]; rpz[L]=cpz[0]; rpw[L]=ChanWidth; rpg[L]=pg[0];
            var rx=new List<float>(); var rz=new List<float>(); var rw=new List<float>(); var rg=new List<float>();
            Resample(rpx,rpz,rpw,rpg,L+1,MeshStep, rx,rz,rw,rg); int M=rx.Count;

            // W(s) = 원본(미개간) 바닥 강한 래핑 스무딩(닫힌 루프 → 이음새 없음)
            var Ws=new float[M]; for(int p=0;p<M;p++) Ws[p]=PristineAt(rx[p],rz[p]);
            for (int it=0;it<40;it++){ var t=(float[])Ws.Clone(); for(int p=0;p<M;p++){ float s=0; int n=0; for(int q=-8;q<=8;q++){ int k=((p+q)%M+M)%M; s+=Ws[k]; n++; } t[p]=s/n; } Ws=t; }

            // ① 맵 전체를 원본으로 복원(마을 반경 BandIn 안만 보존) → 손으로 판 흔적 전부 제거
            int wiped=0;
            for (int hz=0;hz<R;hz++) for (int hx=0;hx<R;hx++){ float x=hx*texX, z=hz*texZ; float r=Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ));
                if (r>=BandIn){ float pr=Mathf.Clamp01(PristineAt(x,z)/S.y); if(Mathf.Abs(pr-Hn[hz,hx])>1e-5f) wiped++; Hn[hz,hx]=pr; } }
            sb.AppendLine($"[복원] 마을(반경<{BandIn:F0}m) 밖 전체를 원본으로 되돌림. 실제 변경 {wiped}텍셀(=판 흔적).");

            // ── ★반경 비교 r(θ) 방식 (선분거리 폐기) — 각 셀이 각도로 정확히 1회만 판정 → 겹침·빈틈 원리적 불가 ──
            const int NA=1440;
            var rSum=new double[NA]; var wSum=new double[NA]; var cA=new int[NA];
            for(int k=0;k<M;k++){ float ddx=rx[k]-CX, ddz=rz[k]-CZ; float ang0=Mathf.Atan2(ddz,ddx); float rr0=Mathf.Sqrt(ddx*ddx+ddz*ddz);
                int b=Mathf.Clamp(Mathf.FloorToInt((ang0+Mathf.PI)/(2f*Mathf.PI)*NA),0,NA-1); rSum[b]+=rr0; wSum[b]+=Ws[k]; cA[b]++; }
            var rLut=new float[NA]; var wLut=new float[NA]; var hasA=new bool[NA]; var hasB=new bool[NA];
            for(int b=0;b<NA;b++){ if(cA[b]>0){ hasA[b]=true; hasB[b]=true; rLut[b]=(float)(rSum[b]/cA[b]); wLut[b]=(float)(wSum[b]/cA[b]); } }
            CircInterp(rLut,hasA,NA); CircInterp(wLut,hasB,NA);
            // 각도→(반경편차 dev=r_cell−r(θ), 수위 W). O(1), 셀당 1회.
            void RingAt(float x,float z, out float dev, out float Wv){
                float ex=x-CX, ez=z-CZ; float ang=Mathf.Atan2(ez,ex); float rc=Mathf.Sqrt(ex*ex+ez*ez);
                float ff=(ang+Mathf.PI)/(2f*Mathf.PI)*NA - 0.5f; int b0=Mathf.FloorToInt(ff); float tt2=ff-b0;
                int i0=((b0%NA)+NA)%NA, i1=(i0+1)%NA; float rth=Mathf.Lerp(rLut[i0],rLut[i1],tt2); Wv=Mathf.Lerp(wLut[i0],wLut[i1],tt2); dev=rc-rth; }

            // 지형 강제(반경 편차 d=|dev| 기준)  d≤7 바닥 W−1.5 | 7<d≤7.8 물가 W−1.5→W−0.1 | 7.8<d≤10.8 둑 W−0.1→W+0.8
            float HALF=ChanWidth*0.5f;              // 7
            float Tuck=0.8f; float shoreEnd=HALF+Tuck; float bankEnd=shoreEnd+BankW;
            float fpad=bankEnd+2f;
            int fxMin=Mathf.Clamp(Mathf.FloorToInt((Mn(rx)-fpad)/S.x*(R-1)),0,R-1), fxMax=Mathf.Clamp(Mathf.CeilToInt((Mx(rx)+fpad)/S.x*(R-1)),0,R-1);
            int fzMin=Mathf.Clamp(Mathf.FloorToInt((Mn(rz)-fpad)/S.z*(R-1)),0,R-1), fzMax=Mathf.Clamp(Mathf.CeilToInt((Mx(rz)+fpad)/S.z*(R-1)),0,R-1);
            int forced=0;
            for (int hz=fzMin;hz<=fzMax;hz++) for (int hx=fxMin;hx<=fxMax;hx++){ float x=hx*texX, z=hz*texZ;
                RingAt(x,z, out float dev, out float W); float d=Mathf.Abs(dev); if(d>bankEnd) continue;
                float target; bool force;
                if (d<=HALF){ target=W-Depth; force=true; }
                else if (d<=shoreEnd){ float t2=(d-HALF)/Tuck; target=(W-Depth)+(Depth-0.1f)*t2; force=true; }
                else { float t2=(d-shoreEnd)/BankW; target=(W-0.1f)+(BankAbove+0.1f)*t2; force=false; }
                float cur=Hn[hz,hx]*S.y; float nv=force?target:Mathf.Max(cur,target);
                Hn[hz,hx]=Mathf.Clamp01(nv/S.y); forced++;
            }
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null; if(terrain!=null) terrain.Flush();
            sb.AppendLine($"[강제] 텍셀 {forced}개. 바닥=W−{Depth}, 둑=W+{BankAbove}, 폭 {ChanWidth}m 고정. W {Mn(Ws):F1}~{Mx(Ws):F1}m");

            // ── 수면: 반경 기준 wet 격자 → 연속 메시(높이=W(θ), 지형으로 밀어올리지 않음) ──
            float g=MeshStep, wetR=shoreEnd;   // 7.8 (물 외곽을 둑 밑으로 tuck → 계단 숨김)
            float wminx=Mn(rx)-wetR-1f, wmaxx=Mx(rx)+wetR+1f, wminz=Mn(rz)-wetR-1f, wmaxz=Mx(rz)+wetR+1f;
            int gnx=Mathf.CeilToInt((wmaxx-wminx)/g)+1, gnz=Mathf.CeilToInt((wmaxz-wminz)/g)+1;
            int nnx=gnx+1, nnz=gnz+1;
            var ndDist=new float[nnx,nnz]; var ndW=new float[nnx,nnz];
            for(int i=0;i<nnx;i++)for(int j=0;j<nnz;j++){ float x=wminx+i*g, z=wminz+j*g; RingAt(x,z, out float dev, out float W); ndDist[i,j]=Mathf.Abs(dev); ndW[i,j]=W; }
            // 수위 미세 블러(모든 노드 유효)
            for(int it=0;it<3;it++){ var t=(float[,])ndW.Clone(); for(int i=0;i<nnx;i++)for(int j=0;j<nnz;j++){ float s=ndW[i,j]; int n=1;
                if(i>0){s+=ndW[i-1,j];n++;} if(i<nnx-1){s+=ndW[i+1,j];n++;} if(j>0){s+=ndW[i,j-1];n++;} if(j<nnz-1){s+=ndW[i,j+1];n++;} t[i,j]=s/n; } ndW=t; }
            var wet=new bool[gnx,gnz];
            for(int i=0;i<gnx;i++)for(int j=0;j<gnz;j++){ float dm=Mathf.Min(Mathf.Min(ndDist[i,j],ndDist[i+1,j]),Mathf.Min(ndDist[i,j+1],ndDist[i+1,j+1])); if(dm<=wetR) wet[i,j]=true; }
            int comp=Components(wet,gnx,gnz);

            KillRoot(scene,"_Stream_Water");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath)!=null) AssetDatabase.DeleteAsset(MeshPath);
            var mesh=BuildMeshFromGrid(wet,ndW,gnx,gnz,wminx,wminz,g); mesh.name="Seocheon_StreamWater";
            EnsureFolder("Assets/_Project/Seocheon/Art/Models"); AssetDatabase.CreateAsset(mesh,MeshPath);
            var mat=MakeWaterMat();
            var go=new GameObject("_Stream_Water"); go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=mat;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);
            sb.AppendLine($"[수면] 연속 메시 tris {mesh.triangles.Length/3}, verts {mesh.vertexCount} · 높이=W(지형 밀어올림 없음) · 머티리얼 A.");

            // ── [3] 합격 기준(wet 격자 전수 조사, 720 각도 구간) ──
            const int NB2=720; var angCov=new bool[NB2];
            float minDep=1e9f; string minAt=""; int poke=0; string pokeAt=""; int wetCells=0;
            for(int i=0;i<gnx;i++)for(int j=0;j<gnz;j++){ if(!wet[i,j])continue; wetCells++;
                float x=wminx+(i+0.5f)*g, z=wminz+(j+0.5f)*g;
                float ang=Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg; int ab=Mathf.Clamp(Mathf.FloorToInt((ang+180f)/360f*NB2),0,NB2-1); angCov[ab]=true;
                float water=0.25f*(ndW[i,j]+ndW[i+1,j]+ndW[i,j+1]+ndW[i+1,j+1]);
                float floor=Sample(Hn,S,R,x,z); float dep=water-floor;
                if(dep<minDep){ minDep=dep; minAt=$"X{x:F0} Z{z:F0}"; }
                if(floor>water+1e-3f){ poke++; if(pokeAt=="") pokeAt=$"X{x:F0} Z{z:F0}"; } }
            int emptyAng=0; var emptyList=new StringBuilder();
            for(int a=0;a<NB2;a++) if(!angCov[a]){ emptyAng++; if(emptyAng<=12){ float th=((a+0.5f)*(360f/NB2)-180f)*Mathf.Deg2Rad; emptyList.Append($" [{a*360/NB2}° ~X{CX+105f*Mathf.Cos(th):F0} Z{CZ+105f*Mathf.Sin(th):F0}]"); } }
            sb.AppendLine("── [3] 합격 기준 (반경 r(θ) 방식) ──");
            sb.AppendLine($"· 720 각도 구간 빈 구간 {emptyAng}개 {(emptyAng==0?"✓ (전 구간 물, 쐐기 0)":"⚠"+emptyList)}");
            sb.AppendLine($"· wet 연결 조각 {comp}개 {(comp==1?"✓":"⚠")}  (wet셀 {wetCells})");
            sb.AppendLine($"· 물 밑 지형>수면 셀 {poke}개 {(poke==0?"✓":"⚠ 예:"+pokeAt)}");
            sb.AppendLine($"· 최소 담수깊이 {minDep:F2}m @{minAt} {(minDep>=Depth-0.1f?"✓":"⚠")}");
            sb.AppendLine($"· 수면 메시 tris {mesh.triangles.Length/3} · 폭 {ChanWidth}m·수위 불변");

            var files=new List<string>(); string rerr=null;
            try{
                files.Add(RenderTop(terrain,"Aerial_250",CX,CZ,250f,62f));           // 부감: 쐐기 0 확인
                files.Add(RenderTop(terrain,"Aerial_120_W",364f,642f,120f,50f));     // 서(왼쪽 8~9시) 구간 확대
            }catch(System.Exception e){ rerr=e.ToString(); }
            if (rerr!=null) sb.AppendLine("[렌더] 실패: "+rerr); else { sb.AppendLine($"[렌더] {files.Count}장 → {RenderDir}"); foreach(var f in files) sb.AppendLine("   "+f); }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"완료(반경 방식). wet 조각 {comp} · 빈각도 {emptyAng} · tris {mesh.triangles.Length/3}.\nConsole·render_final 확인.","확인");
        }

        // ─────────── 핵심 헬퍼 ───────────
        // wet 격자의 4-이웃 연결 조각 수(1이어야 완전한 링)
        private static int Components(bool[,] wet,int gnx,int gnz)
        {
            var seen=new bool[gnx,gnz]; int comp=0; var st=new Stack<int>();
            for (int si=0;si<gnx;si++) for (int sj=0;sj<gnz;sj++){ if(!wet[si,sj]||seen[si,sj]) continue;
                comp++; st.Push(si*gnz+sj); seen[si,sj]=true;
                while(st.Count>0){ int c=st.Pop(); int i=c/gnz, j=c%gnz;
                    if(i>0&&wet[i-1,j]&&!seen[i-1,j]){seen[i-1,j]=true;st.Push((i-1)*gnz+j);}
                    if(i<gnx-1&&wet[i+1,j]&&!seen[i+1,j]){seen[i+1,j]=true;st.Push((i+1)*gnz+j);}
                    if(j>0&&wet[i,j-1]&&!seen[i,j-1]){seen[i,j-1]=true;st.Push(i*gnz+(j-1));}
                    if(j<gnz-1&&wet[i,j+1]&&!seen[i,j+1]){seen[i,j+1]=true;st.Push(i*gnz+(j+1));} } }
            return comp;
        }

        // wet 격자 → 정점 공유 연속 메시. 노드 높이 = ndW(=W). ★floor max 클램프 없음(둑 위로 안 올라감).
        private static Mesh BuildMeshFromGrid(bool[,] wet,float[,] ndW,int gnx,int gnz,float minx,float minz,float g)
        {
            int nnx=gnx+1, nnz=gnz+1; var idx=new int[nnx,nnz]; for(int i=0;i<nnx;i++)for(int j=0;j<nnz;j++) idx[i,j]=-1;
            var verts=new List<Vector3>(); var uvs=new List<Vector2>(); var nrm=new List<Vector3>(); var tan=new List<Vector4>(); var tris=new List<int>(); float kw=Mathf.PI*2f/WarpWave;
            int Node(int i,int j){ if(idx[i,j]>=0) return idx[i,j]; float x=minx+i*g, z=minz+j*g; float y=ndW[i,j];
                int id=verts.Count; verts.Add(new Vector3(x,y,z)); uvs.Add(new Vector2(x*UVScale+WarpAmp*Mathf.Sin(z*kw), z*UVScale+WarpAmp*Mathf.Sin(x*kw))); nrm.Add(Vector3.up); tan.Add(new Vector4(1,0,0,1)); idx[i,j]=id; return id; }
            for (int i=0;i<gnx;i++)for(int j=0;j<gnz;j++){ if(!wet[i,j])continue; int a=Node(i,j),b=Node(i,j+1),c=Node(i+1,j),d=Node(i+1,j+1);
                tris.Add(a);tris.Add(b);tris.Add(c); tris.Add(c);tris.Add(b);tris.Add(d); } // 위(+Y)
            var mesh=new Mesh(); mesh.indexFormat=IndexFormat.UInt32; mesh.SetVertices(verts); mesh.SetTriangles(tris,0); mesh.SetUVs(0,uvs); mesh.SetNormals(nrm); mesh.SetTangents(tan); mesh.RecalculateBounds(); return mesh;
        }

        private static void CircInterp(float[] v,bool[] has,int N)
        {
            bool any=false; for(int b=0;b<N;b++) if(has[b]){ any=true; break; } if(!any) return;
            for (int b=0;b<N;b++){ if(has[b]) continue;
                int a=b, sa=0; do{ a=(a-1+N)%N; sa++; } while(!has[a] && sa<=N);
                int c=b, sc=0; do{ c=(c+1)%N; sc++; } while(!has[c] && sc<=N);
                float t=(float)sa/(sa+sc); v[b]=Mathf.Lerp(v[a],v[c],t); }
        }
        private static float Median(List<float> a){ if(a.Count==0) return 0f; a.Sort(); int n=a.Count; return n%2==1?a[n/2]:0.5f*(a[n/2-1]+a[n/2]); }

        // ─────────── 공통 헬퍼 ───────────
        private static void InterpGaps(float[] ax,float[] az,float[] aw,float[] ag,bool[] ah,int L)
        {
            int firstH=-1; for(int p=0;p<L;p++) if(ah[p]){ firstH=p; break; }
            if (firstH<0) return;
            for (int p=0;p<L;p++) if(!ah[p]){ int a=p; while(a>=0&&!ah[a]) a--; int b=p; while(b<L&&!ah[b]) b++;
                if (a<0){ ax[p]=ax[b]; az[p]=az[b]; aw[p]=aw[b]; ag[p]=ag[b]; }
                else if (b>=L){ ax[p]=ax[a]; az[p]=az[a]; aw[p]=aw[a]; ag[p]=ag[a]; }
                else { float t=(float)(p-a)/(b-a); ax[p]=Mathf.Lerp(ax[a],ax[b],t); az[p]=Mathf.Lerp(az[a],az[b],t); aw[p]=Mathf.Lerp(aw[a],aw[b],t); ag[p]=Mathf.Lerp(ag[a],ag[b],t); } }
        }
        private static void SmoothCirc(float[] a,int win){ int L=a.Length; var b=new float[L];
            for(int p=0;p<L;p++){ float s=0; int n=0; for(int q=-win;q<=win;q++){ int k=p+q; if(k>=0&&k<L){ s+=a[k]; n++; } } b[p]=s/n; }
            System.Array.Copy(b,a,L); }
        private static void SmoothLoop(float[] a,int win){ int L=a.Length; var b=new float[L];
            for(int p=0;p<L;p++){ float s=0; for(int q=-win;q<=win;q++){ int k=((p+q)%L+L)%L; s+=a[k]; } b[p]=s/(2*win+1); }
            System.Array.Copy(b,a,L); }

        private static void Resample(float[] px,float[] pz,float[] pw,float[] pg,int L,float step,
                                     List<float> rx,List<float> rz,List<float> rw,List<float> rg)
        {
            var cum=new float[L]; for(int p=1;p<L;p++) cum[p]=cum[p-1]+Mathf.Sqrt((px[p]-px[p-1])*(px[p]-px[p-1])+(pz[p]-pz[p-1])*(pz[p]-pz[p-1]));
            float total=cum[L-1]; int nStep=Mathf.Max(1,Mathf.FloorToInt(total/step));
            for (int s=0;s<=nStep;s++){ float d=s*step; int lo=0,hi=L-1; if(d>=total){ rx.Add(px[L-1]); rz.Add(pz[L-1]); rw.Add(pw[L-1]); rg.Add(pg[L-1]); continue; }
                while(lo+1<hi){ int m=(lo+hi)/2; if(cum[m]<=d) lo=m; else hi=m; } float t=(d-cum[lo])/Mathf.Max(1e-4f,cum[hi]-cum[lo]);
                rx.Add(Mathf.Lerp(px[lo],px[hi],t)); rz.Add(Mathf.Lerp(pz[lo],pz[hi],t)); rw.Add(Mathf.Lerp(pw[lo],pw[hi],t)); rg.Add(Mathf.Lerp(pg[lo],pg[hi],t)); }
        }

        private static int Nearest(List<float> rx,List<float> rz,int M,float x,float z,out float dist)
        { float bd=float.MaxValue; int bi=0; for(int p=0;p<M;p++){ float dx=x-rx[p], dz=z-rz[p]; float dd=dx*dx+dz*dz; if(dd<bd){bd=dd;bi=p;} } dist=Mathf.Sqrt(bd); return bi; }
        private static Vector2 Perp(List<float> rx,List<float> rz,int M,int p)
        { int a=Mathf.Max(0,p-1), b=Mathf.Min(M-1,p+1); Vector2 t=new Vector2(rx[b]-rx[a],rz[b]-rz[a]); if(t.sqrMagnitude<1e-4f) t=new Vector2(1,0); t.Normalize(); return new Vector2(-t.y,t.x); }

        private static Mesh BuildRibbon(List<float> rx,List<float> rz,float[] width,float[] hy,float _pad)
        {
            int M=rx.Count; var verts=new List<Vector3>(); var tris=new List<int>(); var uvs=new List<Vector2>(); var nrm=new List<Vector3>(); var tan=new List<Vector4>();
            float kw=Mathf.PI*2f/WarpWave;
            for (int p=0;p<M;p++){ int a=Mathf.Max(0,p-1), b=Mathf.Min(M-1,p+1); Vector2 tg=new Vector2(rx[b]-rx[a],rz[b]-rz[a]); if(tg.sqrMagnitude<1e-4f) tg=new Vector2(1,0); tg.Normalize(); Vector2 pp=new Vector2(-tg.y,tg.x);
                for (int c=0;c<=NCross;c++){ float lat=((float)c/NCross-0.5f)*width[p]; float x=rx[p]+pp.x*lat, z=rz[p]+pp.y*lat;
                    verts.Add(new Vector3(x,hy[p],z)); uvs.Add(new Vector2(x*UVScale+WarpAmp*Mathf.Sin(z*kw), z*UVScale+WarpAmp*Mathf.Sin(x*kw))); nrm.Add(Vector3.up); tan.Add(new Vector4(pp.x,0,pp.y,1)); } }
            int stride=NCross+1;
            for (int p=0;p<M-1;p++) for (int c=0;c<NCross;c++){ int v00=p*stride+c, v01=p*stride+c+1, v10=(p+1)*stride+c, v11=(p+1)*stride+c+1;
                tris.Add(v00); tris.Add(v01); tris.Add(v10); tris.Add(v01); tris.Add(v11); tris.Add(v10); }
            var mesh=new Mesh(); mesh.indexFormat=IndexFormat.UInt32; mesh.SetVertices(verts); mesh.SetTriangles(tris,0); mesh.SetUVs(0,uvs); mesh.SetNormals(nrm); mesh.SetTangents(tan); mesh.RecalculateBounds();
            return mesh;
        }

        private static Material MakeWaterMat()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MatPathA)!=null) AssetDatabase.DeleteAsset(MatPathA);
            EnsureFolder("Assets/_Project/Seocheon/Art/Materials"); AssetDatabase.CopyAsset(AyoMat, MatPathA);
            var m=AssetDatabase.LoadAssetAtPath<Material>(MatPathA);
            void F(string p,float v){ if(m.HasProperty(p)) m.SetFloat(p,v); } void C(string p,Color v){ if(m.HasProperty(p)) m.SetColor(p,v); } void V(string p,Vector4 v){ if(m.HasProperty(p)) m.SetVector(p,v); }
            F("_Refraction_Strength",0f); F("_Refraction_Scale",0f);
            C("_Shallow_Water_Color", new Color(0.38f,0.62f,0.58f,0.75f)); C("_Deep_Water_Color", new Color(0.10f,0.30f,0.32f,0.9f));
            C("_Foam_Color", new Color(1f,1f,1f,0.7f)); C("_Specular_Color", new Color(0.3f,0.3f,0.3f,1f));
            F("_Smoothness",0.22f); F("_Sun_Reflection_Enable",0f);
            F("_Water_Depth_Distance",0.7f); F("_Wave_Scale",4f); F("_Wave_Speed",0.15f); F("_Wave_Strength",0.06f);
            F("_Foam_Scale",5f); F("_Foam_Amount",0.06f); F("_Foam_Cutoff",0.2f); F("_Foam_Speed",0.15f);
            V("_Water_Direction", new Vector4(-1f,0f,0f,0f)); F("_Voronoi_Density",20f);
            F("_1st_Caustics_Enable",0f); F("_2nd_Caustics_Enable",0f); F("_1st_Caustics_Brightness",0f); F("_2nd_Caustics_Brightness",0f);
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssets(); return m;
        }

        private static Material BrightMat()
        { var sh=Shader.Find("Universal Render Pipeline/Unlit"); if(sh==null) sh=Shader.Find("Unlit/Color"); var m=new Material(sh);
          if(m.HasProperty("_BaseColor")) m.SetColor("_BaseColor",new Color(1f,0f,1f,1f)); if(m.HasProperty("_Color")) m.SetColor("_Color",new Color(1f,0f,1f,1f)); return m; }

        private static string RenderAerial(Terrain t,string name)
        {
            try{ Directory.CreateDirectory(RenderDir);
                float g0=t.transform.position.y+t.SampleHeight(new Vector3(CX,0,CZ));
                const int W=1280,H=720; var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
                var camGO=new GameObject("__c"); var cam=camGO.AddComponent<Camera>(); cam.clearFlags=CameraClearFlags.Skybox; cam.fieldOfView=62f; cam.nearClipPlane=0.1f; cam.farClipPlane=2000f; cam.enabled=false;
                cam.transform.position=new Vector3(CX,g0+260f,CZ); cam.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
                string fn=Path.Combine(RenderDir,name+".png");
                try{ var req=new UniversalRenderPipeline.SingleCameraRequest(); if(RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req);} else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                    RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null; File.WriteAllBytes(fn,tex.EncodeToPNG()); }
                finally{ Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
                return fn; } catch(System.Exception e){ return "예외:"+e.Message; }
        }

        private static string RenderTop(Terrain t,string name,float cx,float cz,float alt,float fov)
        {
            Directory.CreateDirectory(RenderDir);
            float g0=t.transform.position.y+t.SampleHeight(new Vector3(cx,0,cz));
            const int W=1280,H=720; var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
            var camGO=new GameObject("__c"); var cam=camGO.AddComponent<Camera>(); cam.clearFlags=CameraClearFlags.Skybox; cam.fieldOfView=fov; cam.nearClipPlane=0.1f; cam.farClipPlane=2000f; cam.enabled=false;
            cam.transform.position=new Vector3(cx,g0+alt,cz); cam.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
            string fn=Path.Combine(RenderDir,name+".png");
            try{ var req=new UniversalRenderPipeline.SingleCameraRequest(); if(RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req);} else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null; File.WriteAllBytes(fn,tex.EncodeToPNG()); }
            finally{ Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
            return fn;
        }

        private static List<string> Render6(Terrain t,TerrainData td,Vector3 S,int R,List<float> rx,List<float> rz,float[] Ws,int M)
        {
            Directory.CreateDirectory(RenderDir); var outF=new List<string>(); float g0=t.transform.position.y+t.SampleHeight(new Vector3(CX,0,CZ));
            const int W=1280,H=720; var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
            var camGO=new GameObject("__c"); var cam=camGO.AddComponent<Camera>(); cam.clearFlags=CameraClearFlags.Skybox; cam.nearClipPlane=0.02f; cam.farClipPlane=2000f; cam.enabled=false;
            void Shot(string n,Vector3 pos,Vector3 look,float fov){ cam.transform.position=pos; cam.fieldOfView=fov; cam.transform.rotation=Quaternion.LookRotation((look-pos).normalized,Vector3.up);
                var req=new UniversalRenderPipeline.SingleCameraRequest(); if(RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req);} else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null; string fn=Path.Combine(RenderDir,n+".png"); File.WriteAllBytes(fn,tex.EncodeToPNG()); outF.Add(fn); }
            try{
                int mi=M/2; Vector3 mp=new Vector3(rx[mi],Ws[mi],rz[mi]); Vector2 pp=Perp(rx,rz,M,mi);
                Shot("Final_Aerial", new Vector3(CX,g0+260f,CZ), new Vector3(CX,Ws[mi],CZ), 62f);
                Shot("Bank_oblique", mp+new Vector3(pp.x,0,pp.y)*10f+Vector3.up*4f, mp, 60f);
                Shot("Near_1p5m", mp+new Vector3(pp.x,0,pp.y)*1.0f+Vector3.up*1.1f, mp, 55f);
            } finally { Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
            return outF;
        }

        private static Vector2 Dir8(int d){ switch(d){ case 0:return new Vector2(1,0); case 1:return new Vector2(-1,0); case 2:return new Vector2(0,1); case 3:return new Vector2(0,-1);
            case 4:return new Vector2(.707f,.707f); case 5:return new Vector2(-.707f,.707f); case 6:return new Vector2(.707f,-.707f); default:return new Vector2(-.707f,-.707f); } }
        private static float Median8(float[] a){ var b=(float[])a.Clone(); System.Array.Sort(b); return 0.5f*(b[3]+b[4]); }
        private static float Mn(float[] a){ float m=float.MaxValue; foreach(var v in a) if(v<m)m=v; return m; }
        private static float Mx(float[] a){ float m=float.MinValue; foreach(var v in a) if(v>m)m=v; return m; }
        private static float Mn(List<float> a){ float m=float.MaxValue; foreach(var v in a) if(v<m)m=v; return m; }
        private static float Mx(List<float> a){ float m=float.MinValue; foreach(var v in a) if(v>m)m=v; return m; }
        private static void KillRoot(Scene s,string n){ foreach(var r in s.GetRootGameObjects()) if(r.name==n) Object.DestroyImmediate(r); }
        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
        private static float Sample(float[,] Hn,Vector3 size,int R,float x,float z)
        { float u=x/size.x,v=z/size.z; float fx=Mathf.Clamp(u*(R-1),0,R-1),fz=Mathf.Clamp(v*(R-1),0,R-1);
          int x0=(int)fx,z0=(int)fz,x1=Mathf.Min(x0+1,R-1),z1=Mathf.Min(z0+1,R-1); float tx=fx-x0,tz=fz-z0;
          return Mathf.Lerp(Mathf.Lerp(Hn[z0,x0],Hn[z0,x1],tx),Mathf.Lerp(Hn[z1,x0],Hn[z1,x1],tx),tz)*size.y; }
    }
}
