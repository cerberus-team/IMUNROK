using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// 개천 골 깊이를 아치 경로 그대로 추적해 측정한다. 완전 읽기 전용(지형/씬/에셋 수정 없음).
    /// 메뉴: [Tools ▸ Seocheon ▸ Measure Stream Depth]
    ///
    /// 방법:
    ///  1) 지형 높이를 격자로 읽어 골짜기 칸 분류 → 최대 연결영역이 개천.
    ///  2) 개천 안에서 가장 먼 두 끝(양 tip)을 이중 BFS로 찾고, 그 사이 최단경로(=아치 척추)를 뽑는다.
    ///     → X열이 아니라 골 경로를 따라감. 시작점은 남동(SE) tip.
    ///  3) 경로 10m 간격 각 지점에서 경로에 수직으로 양쪽을 훑어 좌우 둑·바닥·폭·깊이를 잰다.
    ///  4) 현재 지형 vs _backup 을 같은 격자로 비교해 메워진(높아진) 구간을 보고한다.
    /// </summary>
    public static class SeocheonStreamMeasurer
    {
        private const string CurPath = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain.asset";
        private const string BakPath = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain_backup.asset";

        private const float RXMin=360f, RXMax=580f, RZMin=560f, RZMax=790f;
        private const float Grid=1.0f;              // 분류·추적 격자(m)
        private const float BankProbe=10f;          // 골짜기 판정용 좌우 참조 거리
        private const float CarveThresh=0.4f;       // 주변보다 이만큼 낮으면 골
        private const float Station=10f;            // 경로 표본 간격(m)
        private const float MaxHalf=20f;            // 단면 최대 반폭(m)
        private const float CrestDrop=0.2f;         // 이만큼 다시 내려가면 둑마루 통과로 간주
        private const float TargetDepth=1.5f;       // 목표 깊이(합격선)
        private const float FillEps=0.15f;          // 메움 판정 임계(m)

        [MenuItem("Tools/Seocheon/Measure Stream Depth")]
        public static void Measure()
        {
            var cur=AssetDatabase.LoadAssetAtPath<TerrainData>(CurPath);
            if (cur==null){ EditorUtility.DisplayDialog("Seocheon","현재 TerrainData 없음:\n"+CurPath,"확인"); return; }
            var bak=AssetDatabase.LoadAssetAtPath<TerrainData>(BakPath);

            int Rc=cur.heightmapResolution; Vector3 Sc=cur.size;
            var Hc=cur.GetHeights(0,0,Rc,Rc);
            int Rb=0; Vector3 Sb=Vector3.zero; float[,] Hb=null;
            if (bak!=null){ Rb=bak.heightmapResolution; Sb=bak.size; Hb=bak.GetHeights(0,0,Rb,Rb); }

            float HcAt(float x,float z)=>Sample(Hc,Sc,Rc,x,z);
            float HbAt(float x,float z)=>Hb!=null?Sample(Hb,Sb,Rb,x,z):float.NaN;

            var sb=new StringBuilder();
            sb.AppendLine("===== [Seocheon] 개천 골 깊이 측정 (읽기 전용) =====");
            sb.AppendLine($"대상: {CurPath}  (해상도 {Rc}, size {Sc.x:F0}×{Sc.y:F0}×{Sc.z:F0})");
            sb.AppendLine($"백업: {(bak!=null?BakPath+$" (해상도 {Rb})":"없음 — [6] 생략")}");
            sb.AppendLine($"영역 X[{RXMin:F0},{RXMax:F0}] Z[{RZMin:F0},{RZMax:F0}], 격자 {Grid}m, 표본간격 {Station}m\n");

            // ── 격자 + 골짜기 분류 ──
            int nx=Mathf.FloorToInt((RXMax-RXMin)/Grid)+1;
            int nz=Mathf.FloorToInt((RZMax-RZMin)/Grid)+1;
            float WX(int i)=>RXMin+i*Grid; float WZ(int j)=>RZMin+j*Grid;
            var H=new float[nx,nz]; var chan=new bool[nx,nz];
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++) H[i,j]=HcAt(WX(i),WZ(j));
            var dir8=new Vector2[]{ new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1),
                                    new Vector2(.707f,.707f),new Vector2(-.707f,.707f),new Vector2(.707f,-.707f),new Vector2(-.707f,-.707f)};
            var ring=new float[8];
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++){
                float x=WX(i), z=WZ(j);
                for (int d=0;d<8;d++) ring[d]=HcAt(x+dir8[d].x*BankProbe, z+dir8[d].y*BankProbe);
                if (Median8(ring)-H[i,j]>=CarveThresh) chan[i,j]=true;
            }
            var mask=LargestComponent(chan,nx,nz,out int maskCount);
            sb.AppendLine($"[분류] 개천(최대 연결영역) {maskCount}칸 (≈{maskCount*Grid*Grid:F0} m²)");
            if (maskCount<20){ Debug.Log(sb.ToString()); EditorUtility.DisplayDialog("Seocheon","개천 인식 실패. Console 확인.","확인"); return; }

            // ── 아치 척추(양 tip 최단경로) ──
            // SE tip 근처 seed = (i - j) 최대 칸
            int seed=-1; float best=float.MinValue;
            for (int i=0;i<nx;i++) for (int j=0;j<nz;j++) if (mask[i,j]){ float s=i-j; if(s>best){best=s;seed=i*nz+j;} }
            int endA=Bfs(mask,nx,nz,seed,null);
            var parent=new int[nx*nz];
            int endB=Bfs(mask,nx,nz,endA,parent);
            // endA→endB 경로 복원
            var path=new List<int>(); for (int c=endB;c!=-1;c=parent[c]) path.Add(c); path.Reverse();
            // 시작을 SE(=X-Z 큰 쪽)로 정렬
            int ai=path[0]/nz, aj=path[0]%nz, bi=path[path.Count-1]/nz, bj=path[path.Count-1]%nz;
            if ((WX(ai)-WZ(aj)) < (WX(bi)-WZ(bj))) path.Reverse();
            sb.AppendLine($"[경로] 아치 척추 {path.Count}칸, tip A(X{WX(path[0]/nz):F0},Z{WZ(path[0]%nz):F0}) → tip B(X{WX(path[path.Count-1]/nz):F0},Z{WZ(path[path.Count-1]%nz):F0}) (시작=남동)\n");

            // 경로 폴리라인(월드) + 누적거리
            var px=new List<float>(); var pz=new List<float>();
            foreach (int c in path){ px.Add(WX(c/nz)); pz.Add(WZ(c%nz)); }
            var cum=new float[px.Count]; cum[0]=0;
            for (int k=1;k<px.Count;k++) cum[k]=cum[k-1]+Mathf.Sqrt((px[k]-px[k-1])*(px[k]-px[k-1])+(pz[k]-pz[k-1])*(pz[k]-pz[k-1]));
            float total=cum[px.Count-1];

            // ── 10m 간격 표본에서 단면 측정 ──
            var stX=new List<float>(); var stZ=new List<float>(); var stFloor=new List<float>();
            var stL=new List<float>(); var stR=new List<float>(); var stDepth=new List<float>(); var stWidth=new List<float>();
            var stBakFill=new List<float>();
            int nSt=Mathf.FloorToInt(total/Station);
            for (int s=0;s<=nSt;s++){
                float d=s*Station; PointAt(px,pz,cum,d,out float sx,out float sz);
                PointAt(px,pz,cum,Mathf.Min(d+2f,total),out float fx,out float fz);
                PointAt(px,pz,cum,Mathf.Max(d-2f,0f),out float bx,out float bz);
                Vector2 tan=new Vector2(fx-bx,fz-bz); if(tan.sqrMagnitude<1e-4f) tan=new Vector2(1,0); tan.Normalize();
                Vector2 perp=new Vector2(-tan.y,tan.x);
                float lC,lD,rC,rD,fl; CrossSection(HcAt,sx,sz,perp, out lC,out lD, out rC,out rD, out fl);
                float depth=Mathf.Min(lC,rC)-fl; float width=lD+rD;
                stX.Add(sx); stZ.Add(sz); stFloor.Add(fl); stL.Add(lC); stR.Add(rC); stDepth.Add(depth); stWidth.Add(width);
                if (Hb!=null){ CrossSection(HbAt,sx,sz,perp,out _,out _,out _,out _,out float flB); stBakFill.Add(fl-flB); } else stBakFill.Add(float.NaN);
            }

            // [2] 표
            sb.AppendLine("[2] 경로 10m 표 — (거리) X Z | 바닥 | 좌둑 우둑 | 깊이 | 폭 | (현−백업 바닥)");
            for (int s=0;s<stX.Count;s++){
                string fill = float.IsNaN(stBakFill[s]) ? "" : $" | Δbak {stBakFill[s]:+0.00;-0.00}";
                sb.AppendLine($"   {s*Station,4:F0}m  X{stX[s]:F0} Z{stZ[s]:F0} | 바닥 {stFloor[s]:F2} | 둑 L{stL[s]:F2} R{stR[s]:F2} | 깊이 {stDepth[s]:F2} | 폭 {stWidth[s]:F1}{fill}");
            }

            // [3] 깊이<1.5 구간
            sb.AppendLine($"\n[3] 깊이 {TargetDepth}m 미만 구간(=물 안 참)");
            int shallow=0; for (int s=0;s<stX.Count;s++) if (stDepth[s]<TargetDepth){ shallow++;
                sb.AppendLine($"   {s*Station,4:F0}m  X{stX[s]:F0} Z{stZ[s]:F0}  깊이 {stDepth[s]:F2}  (부족 {TargetDepth-stDepth[s]:F2}m)"); }
            if (shallow==0) sb.AppendLine("   없음 ✓");

            // [4] 길이·비율
            int okCnt=0; for (int s=0;s<stX.Count;s++) if (stDepth[s]>=TargetDepth) okCnt++;
            sb.AppendLine($"\n[4] 아치 전체 경로 길이 ≈ {total:F0}m, 표본 {stX.Count}개 중 깊이≥{TargetDepth}m : {okCnt}개 ({100f*okCnt/stX.Count:F0}%)");

            // [5] 바닥 단조감소(경로 시작=SE 기준)
            sb.AppendLine($"\n[5] 경로 바닥 단조감소(SE→반대 tip)");
            var rev=new List<string>();
            for (int s=0;s<stX.Count-1;s++) if (stFloor[s+1] > stFloor[s]+0.10f)
                rev.Add($"{s*Station:F0}→{(s+1)*Station:F0}m: 바닥 {stFloor[s]:F2}→{stFloor[s+1]:F2} (다시 올라감) @X{stX[s+1]:F0} Z{stZ[s+1]:F0}");
            sb.AppendLine(rev.Count==0?"   단조감소 ✓":$"   역행 {rev.Count}곳 ⚠"); foreach (var r in rev) sb.AppendLine("     ↳ "+r);

            // [6] 백업 대비 메움
            sb.AppendLine("\n[6] 현재 vs _backup — 메워진(높아진) 곳");
            if (Hb==null) sb.AppendLine("   백업 없음 — 생략");
            else {
                var raised=new bool[nx,nz]; int rc=0; float maxR=0; float mrx=0,mrz=0; double sumR=0;
                for (int i=0;i<nx;i++) for (int j=0;j<nz;j++){ float dz=H[i,j]-HbAt(WX(i),WZ(j)); if(dz>FillEps){ raised[i,j]=true; rc++; sumR+=dz; if(dz>maxR){maxR=dz;mrx=WX(i);mrz=WZ(j);} } }
                sb.AppendLine($"   전체 영역 중 {FillEps}m 초과 상승 칸 {rc}개 (≈{rc*Grid*Grid:F0} m²), 평균 상승 {(rc>0?sumR/rc:0):F2}m, 최대 상승 {maxR:F2}m @X{mrx:F0} Z{mrz:F0}");
                var rClusters=LargestComponentsAll(raised,nx,nz);
                int shown=0;
                foreach (var cc in rClusters){ if(cc.count<20) continue; if(shown++>=8) break;
                    double avg=0; for(int i=cc.minI;i<=cc.maxI;i++) for(int j=cc.minJ;j<=cc.maxJ;j++) if(raised[i,j]) avg+=H[i,j]-HbAt(WX(i),WZ(j)); avg/= cc.count;
                    bool onChannel=false; for(int i=cc.minI;i<=cc.maxI&&!onChannel;i++) for(int j=cc.minJ;j<=cc.maxJ;j++) if(mask[i,j]){onChannel=true;break;}
                    sb.AppendLine($"     ↳ 상승덩이 {cc.count}칸 @X{WX(Mathf.RoundToInt(cc.sumI/(float)cc.count)):F0} Z{WZ(Mathf.RoundToInt(cc.sumJ/(float)cc.count)):F0}, X[{WX(cc.minI):F0},{WX(cc.maxI):F0}] Z[{WZ(cc.minJ):F0},{WZ(cc.maxJ):F0}], 평균 +{avg:F2}m {(onChannel?"★개천과 겹침(골 메움)":"")}");
                }
                // 경로 위 메움(단면 바닥 기준)
                int pf=0; for (int s=0;s<stBakFill.Count;s++) if(!float.IsNaN(stBakFill[s]) && stBakFill[s]>FillEps){ pf++;
                    sb.AppendLine($"     · 경로 {s*Station:F0}m @X{stX[s]:F0} Z{stZ[s]:F0} 바닥이 백업보다 +{stBakFill[s]:F2}m 높음(메움)"); }
                if (pf==0) sb.AppendLine("     · 경로 위 바닥 메움: 없음");
            }

            // 결론
            sb.AppendLine("\n===== 결론 — 물이 전 구간 차려면 더 파야 할 곳(고치지 않음) =====");
            bool any=false;
            for (int s=0;s<stX.Count;s++) if (stDepth[s]<TargetDepth){ any=true;
                sb.AppendLine($"   X{stX[s]:F0} Z{stZ[s]:F0} : 현재 깊이 {stDepth[s]:F2}m → 바닥을 {TargetDepth-stDepth[s]:F2}m 더 낮춰 목표 {TargetDepth:F1}m"); }
            if (!any) sb.AppendLine("   전 구간 깊이 충족 — 추가 굴착 불필요 ✓");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon","측정 완료(읽기 전용). Console 로그 확인.","확인");
        }

        // 단면: 중심에서 perp 양방향으로 훑어 좌우 둑마루·거리·바닥 최소
        private static void CrossSection(System.Func<float,float,float> Hf, float sx, float sz, Vector2 perp,
                                         out float lC,out float lD, out float rC,out float rD, out float floor)
        {
            float c0=Hf(sx,sz); floor=c0;
            Scan(Hf,sx,sz, perp, ref floor, out lC, out lD);
            Scan(Hf,sx,sz,-perp, ref floor, out rC, out rD);
        }
        private static void Scan(System.Func<float,float,float> Hf, float sx, float sz, Vector2 dir, ref float floor, out float crest, out float crestDist)
        {
            float maxH=float.MinValue; float argD=0;
            for (float d=0.25f; d<=MaxHalf; d+=0.25f){
                float h=Hf(sx+dir.x*d, sz+dir.y*d);
                if (h<floor) floor=h;
                if (h>maxH){ maxH=h; argD=d; }
                if (h < maxH-CrestDrop) break;   // 둑마루 통과
            }
            crest=maxH; crestDist=argD;
        }

        private static void PointAt(List<float> px, List<float> pz, float[] cum, float d, out float x, out float z)
        {
            int n=px.Count; if (d<=0){ x=px[0]; z=pz[0]; return; } if (d>=cum[n-1]){ x=px[n-1]; z=pz[n-1]; return; }
            int lo=0,hi=n-1; while (lo+1<hi){ int m=(lo+hi)/2; if (cum[m]<=d) lo=m; else hi=m; }
            float t=(d-cum[lo])/Mathf.Max(1e-4f,cum[hi]-cum[lo]);
            x=Mathf.Lerp(px[lo],px[hi],t); z=Mathf.Lerp(pz[lo],pz[hi],t);
        }

        // BFS(8-이웃, 단위 스텝). parent!=null이면 부모 기록. 반환=가장 먼 칸 인덱스.
        private static int Bfs(bool[,] mask,int nx,int nz,int start,int[] parent)
        {
            var dist=new int[nx*nz]; for (int k=0;k<dist.Length;k++) dist[k]=-1;
            if (parent!=null) for (int k=0;k<parent.Length;k++) parent[k]=-1;
            var q=new Queue<int>(); dist[start]=0; q.Enqueue(start); int far=start,fd=0;
            var d8=new int[,]{{1,0},{-1,0},{0,1},{0,-1},{1,1},{1,-1},{-1,1},{-1,-1}};
            while (q.Count>0){ int c=q.Dequeue(); int ci=c/nz, cj=c%nz;
                if (dist[c]>fd){ fd=dist[c]; far=c; }
                for (int d=0;d<8;d++){ int ni=ci+d8[d,0], nj=cj+d8[d,1]; if(ni<0||nj<0||ni>=nx||nj>=nz) continue; if(!mask[ni,nj]) continue; int nc=ni*nz+nj; if(dist[nc]<0){ dist[nc]=dist[c]+1; if(parent!=null) parent[nc]=c; q.Enqueue(nc); } }
            }
            return far;
        }

        private struct Comp { public int count; public long sumI,sumJ; public int minI,maxI,minJ,maxJ; }

        private static bool[,] LargestComponent(bool[,] src,int nx,int nz,out int bestCount)
        {
            var comps=Components(src,nx,nz,out int[,] label);
            int best=0; bestCount=0; for (int k=1;k<comps.Count;k++) if(comps[k].count>bestCount){bestCount=comps[k].count;best=k;}
            var mask=new bool[nx,nz]; for (int i=0;i<nx;i++) for(int j=0;j<nz;j++) mask[i,j]=(label[i,j]==best);
            return mask;
        }
        private static List<Comp> LargestComponentsAll(bool[,] src,int nx,int nz)
        {
            var comps=Components(src,nx,nz,out _); comps.RemoveAt(0);
            comps.Sort((a,b)=>b.count.CompareTo(a.count)); return comps;
        }
        private static List<Comp> Components(bool[,] src,int nx,int nz,out int[,] label)
        {
            label=new int[nx,nz]; var comps=new List<Comp>(); comps.Add(default);
            var d8=new int[,]{{1,0},{-1,0},{0,1},{0,-1},{1,1},{1,-1},{-1,1},{-1,-1}};
            var st=new Stack<int>(); int cur=0;
            for (int i=0;i<nx;i++) for(int j=0;j<nz;j++){
                if (!src[i,j]||label[i,j]!=0) continue; cur++; label[i,j]=cur; st.Push(i*nz+j);
                var cp=new Comp{count=0,sumI=0,sumJ=0,minI=int.MaxValue,maxI=int.MinValue,minJ=int.MaxValue,maxJ=int.MinValue};
                while (st.Count>0){ int c=st.Pop(); int ci=c/nz,cj=c%nz; cp.count++; cp.sumI+=ci; cp.sumJ+=cj;
                    if(ci<cp.minI)cp.minI=ci; if(ci>cp.maxI)cp.maxI=ci; if(cj<cp.minJ)cp.minJ=cj; if(cj>cp.maxJ)cp.maxJ=cj;
                    for (int d=0;d<8;d++){ int ni=ci+d8[d,0],nj=cj+d8[d,1]; if(ni<0||nj<0||ni>=nx||nj>=nz) continue; if(src[ni,nj]&&label[ni,nj]==0){ label[ni,nj]=cur; st.Push(ni*nz+nj); } }
                }
                comps.Add(cp);
            }
            return comps;
        }

        private static float Median8(float[] a){ var b=(float[])a.Clone(); System.Array.Sort(b); return 0.5f*(b[3]+b[4]); }

        // 월드(x,z) 지형 높이 — 지형 원점 (0,0,0) 가정, GetHeights[row=z, col=x] 바이리니어
        private static float Sample(float[,] Hn, Vector3 size, int R, float x, float z)
        {
            float u=x/size.x, v=z/size.z;
            float fx=Mathf.Clamp(u*(R-1),0,R-1), fz=Mathf.Clamp(v*(R-1),0,R-1);
            int x0=(int)fx, z0=(int)fz, x1=Mathf.Min(x0+1,R-1), z1=Mathf.Min(z0+1,R-1);
            float tx=fx-x0, tz=fz-z0;
            float h00=Hn[z0,x0], h10=Hn[z0,x1], h01=Hn[z1,x0], h11=Hn[z1,x1];
            return Mathf.Lerp(Mathf.Lerp(h00,h10,tx), Mathf.Lerp(h01,h11,tx), tz) * size.y;
        }
    }
}
