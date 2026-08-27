using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// ★읽기 전용★ 링 바깥 지형 측정: 8방위×반경 높이표 + 산/언덕 검출 + 산기슭 + 링까지 거리.
    /// 지형·씬 아무것도 수정/저장하지 않는다.
    /// </summary>
    public static class SeocheonTerrainProbe
    {
        private const string ScenePath = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string PathFile  = @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const float CX=463f, CZ=678f;
        private static readonly string[] DIRN={"N","NE","E","SE","S","SW","W","NW"};
        private static readonly Vector2[] DIRV={ new Vector2(0,1), new Vector2(0.707f,0.707f), new Vector2(1,0), new Vector2(0.707f,-0.707f),
                                                 new Vector2(0,-1), new Vector2(-0.707f,-0.707f), new Vector2(-1,0), new Vector2(-0.707f,0.707f) };

        [MenuItem("Tools/Seocheon/Terrain Probe (read-only)")]
        public static void Probe()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null;
            if(terrain==null){ EditorUtility.DisplayDialog("Seocheon","Terrain 없음.","확인"); return; }
            var td=terrain.terrainData; int R=td.heightmapResolution; Vector3 S=td.size; var Hn=td.GetHeights(0,0,R,R);
            float tY=terrain.transform.position.y, tX0=terrain.transform.position.x, tZ0=terrain.transform.position.z;
            // 월드(x,z) → 높이(월드 Y). 지형 로컬로 변환 후 정규화 높이 샘플.
            float H(float x,float z){ float u=(x-tX0)/S.x, v=(z-tZ0)/S.z; if(u<0||u>1||v<0||v>1) return float.NaN;
                float fx=Mathf.Clamp(u*(R-1),0,R-1), fz=Mathf.Clamp(v*(R-1),0,R-1); int x0=(int)fx,z0=(int)fz,x1=Mathf.Min(x0+1,R-1),z1=Mathf.Min(z0+1,R-1); float tx=fx-x0,tz=fz-z0;
                float hh=Mathf.Lerp(Mathf.Lerp(Hn[z0,x0],Hn[z0,x1],tx),Mathf.Lerp(Hn[z1,x0],Hn[z1,x1],tx),tz); return tY+hh*S.y; }

            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] Terrain Probe (읽기 전용) =====");
            float hc=H(CX,CZ); sb.AppendLine($"중심(463,678) 높이 {hc:F1}m · 지형 size {S.x:F0}×{S.z:F0}, res {R}, baseY {tY:F1}");

            // [1] 8방위 × 반경 높이표
            float[] radii={120,150,200,250,300};
            sb.AppendLine("\n[1] 링 바깥 방위별 높이(m) — 값은 월드 Y, (Δ=중심대비)");
            sb.Append("  방위 |"); foreach(var r in radii) sb.Append($"  {r,4:F0}m       |"); sb.AppendLine();
            for(int d=0;d<8;d++){ sb.Append($"  {DIRN[d],-3} |");
                foreach(var r in radii){ float x=CX+DIRV[d].x*r, z=CZ+DIRV[d].y*r; float h=H(x,z);
                    if(float.IsNaN(h)) sb.Append("   (밖)      |"); else sb.Append($" {h,6:F1}(Δ{h-hc,5:F1}) |"); }
                sb.AppendLine(); }

            // [2] 산/언덕 검출: r∈[120,330] 격자 스캔, 주변 45m 평균보다 +15m & 국소최대
            var peaks=new List<Vector4>(); // x,z,height,prominence
            float step=5f;
            for(float x=CX-330;x<=CX+330;x+=step) for(float z=CZ-330;z<=CZ+330;z+=step){
                float dx=x-CX,dz=z-CZ; float r=Mathf.Sqrt(dx*dx+dz*dz); if(r<120||r>330) continue;
                float h=H(x,z); if(float.IsNaN(h)) continue;
                float sur=0; int sn=0; for(int a=0;a<8;a++){ float sx=x+DIRV[a].x*45f, sz=z+DIRV[a].y*45f; float sh=H(sx,sz); if(!float.IsNaN(sh)){ sur+=sh; sn++; } }
                if(sn<6) continue; float prom=h-sur/sn; if(prom<15f) continue;
                // 국소 최대(반경 25m 내 더 높은 표본 없음)
                bool isMax=true; for(int a=0;a<8&&isMax;a++){ float sx=x+DIRV[a].x*25f, sz=z+DIRV[a].y*25f; if(H(sx,sz)>h+0.1f) isMax=false; }
                if(isMax) peaks.Add(new Vector4(x,z,h,prom));
            }
            // NMS: 45m 내 최고(융기)만 남김
            var kept=new List<Vector4>();
            var byH=new List<Vector4>(peaks); byH.Sort((a,b)=>b.w.CompareTo(a.w));
            foreach(var p in byH){ bool near=false; foreach(var k in kept){ if((p.x-k.x)*(p.x-k.x)+(p.y-k.y)*(p.y-k.y)<45f*45f){ near=true; break; } } if(!near) kept.Add(p); }
            sb.AppendLine($"\n[2] 산/언덕(주변보다 +15m 이상, 국소최대) — {kept.Count}개");
            sb.AppendLine("  # |  정점 X   Z  | 정점높이 | 융기 | 방위 | 링r(θ)까지");
            // r(θ) 로더
            var rth=LoadRth();
            int idx=1;
            foreach(var p in kept){ float dx=p.x-CX,dz=p.y-CZ; float r=Mathf.Sqrt(dx*dx+dz*dz); float ang=Mathf.Atan2(dz,dx)*Mathf.Rad2Deg;
                string dir=CompassOf(ang); float ringR=RthAt(rth,ang);
                sb.AppendLine($"  {idx,1} | ({p.x,4:F0},{p.y,4:F0}) | {p.z,6:F1}m | +{p.w,4:F1} | {dir,-2} | r={r:F0}m, 링{ringR:F0}m → {r-ringR:F0}m 밖"); idx++; }

            // [3][4] 각 산 산기슭(사면 시작) — 정점에서 8방위로 바깥march, 융기의 20% 지점까지 내려온 곳
            sb.AppendLine("\n[3] 각 산 산기슭(사면이 낮아져 평지에 가까워지는 지점) + [4] 링까지 거리");
            idx=1;
            foreach(var p in kept){ float baseH=p.z-p.w*0.8f; // 산기슭 기준 높이(정점−융기80%)
                sb.AppendLine($"  ■ 산{idx} 정점({p.x:F0},{p.y:F0}) {p.z:F1}m");
                for(int d=0;d<8;d++){ float fx=p.x, fz=p.y; float prev=p.z;
                    for(float t=4;t<=180;t+=4){ float sx=p.x+DIRV[d].x*t, sz=p.y+DIRV[d].y*t; float sh=H(sx,sz);
                        if(float.IsNaN(sh)) { fx=sx-DIRV[d].x*4; fz=sz-DIRV[d].y*4; break; }
                        if(sh<=baseH){ fx=sx; fz=sz; break; } fx=sx; fz=sz; prev=sh; }
                    float fr=Mathf.Sqrt((fx-CX)*(fx-CX)+(fz-CZ)*(fz-CZ)); float fang=Mathf.Atan2(fz-CZ,fx-CX)*Mathf.Rad2Deg; float ringR=RthAt(rth,fang);
                    float fh=H(fx,fz);
                    sb.AppendLine($"     {DIRN[d],-2} 기슭 ({fx,4:F0},{fz,4:F0}) {fh,6:F1}m · 중심 {fr:F0}m · 링에서 {fr-ringR:F0}m"); }
                idx++; }

            // 결론: 링에서 가장 가까운 산(정점 기준)
            if(kept.Count>0){ var near=kept[0]; float best=1e9f; foreach(var p in kept){ float dx=p.x-CX,dz=p.y-CZ; float r=Mathf.Sqrt(dx*dx+dz*dz); float ang=Mathf.Atan2(dz,dx)*Mathf.Rad2Deg; float dd=r-RthAt(rth,ang); if(dd<best){best=dd;near=p;} }
                float nang=Mathf.Atan2(near.y-CZ,near.x-CX)*Mathf.Rad2Deg;
                sb.AppendLine($"\n[결론] 산 {kept.Count}개. 링에서 가장 가까운 산 = {CompassOf(nang)} 방위 정점({near.x:F0},{near.y:F0}) {near.z:F1}m, 링에서 약 {best:F0}m 바깥.");
            } else sb.AppendLine("\n[결론] +15m 이상 산/언덕이 검출되지 않음(완만한 지형).");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon","지형 측정 완료(읽기 전용). Console 확인.","확인");
        }

        private static string CompassOf(float angDeg){ // atan2(dz,dx): +X=E(0), +Z=N(90), -X=W(180/-180), -Z=S(-90)
            float a=angDeg; if(a<0)a+=360f; // 0..360, 0=E
            // E=0,N=90,W=180,S=270
            string[] names={"E","NE","N","NW","W","SW","S","SE"}; int i=Mathf.RoundToInt(a/45f)%8; return names[i]; }

        private static List<Vector3> LoadRth(){ var list=new List<Vector3>(); if(!File.Exists(PathFile)) return list; var ci=CultureInfo.InvariantCulture;
            var lines=File.ReadAllLines(PathFile); for(int i=1;i<lines.Length;i++){ var t=lines[i].Split(' '); if(t.Length<2) continue; float x=float.Parse(t[0],ci), z=float.Parse(t[1],ci);
                float dx=x-CX,dz=z-CZ; list.Add(new Vector3(Mathf.Atan2(dz,dx)*Mathf.Rad2Deg, Mathf.Sqrt(dx*dx+dz*dz), 0)); } return list; }
        private static float RthAt(List<Vector3> rth,float angDeg){ if(rth.Count==0) return 97f; float best=1e9f, br=97f; foreach(var v in rth){ float d=Mathf.Abs(Mathf.DeltaAngle(v.x,angDeg)); if(d<best){best=d;br=v.y;} } return br; }
    }
}
