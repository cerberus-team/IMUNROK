using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// [4b단계] 마을 간격 벌리기 — 방사 스케일 1.30 + 건물본체 겹침만 개별 오프셋(≤6m).
    /// ★위치만 이동(회전·프리팹·메시 무변경), Y는 현재 지형 재샘플. 담장 겹침(컴파운드)은 안 건드림.
    /// ★동헌 정면 남쪽 12m 마당자리·개천 링밴드 비움. 원본 미변경.
    /// </summary>
    public static class SeocheonVillageSpread
    {
        private const string Scene   = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string PathFile= @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const string TxtPath = "Assets/_Project/Seocheon/Data/village_spread_report.txt";
        private const float CX=463f, CZ=678f, RingBand=11.8f, Scale=1.30f, MaxOffset=6f;
        private const string BuildDir="/Prefabs/Build/";

        private class H { public Transform t; public string name, seed; public Vector2 offset=Vector2.zero;
            public Bounds bldg; public Bounds full; }

        [MenuItem("Tools/Seocheon/Village/4b. Spread x1.30 + offsets")]
        public static void Spread()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null;
            float TerrH(float x,float z){ if(terrain==null) return 0f; return terrain.transform.position.y+terrain.SampleHeight(new Vector3(x,0,z)); }
            var holder=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="_Village");
            if(holder==null){ EditorUtility.DisplayDialog("Seocheon","_Village 없음. 3단계 먼저.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] Village 4b Spread x1.30 =====");

            var hs=new List<H>();
            foreach(Transform ch in holder.transform){ string seed=ch.name.Replace("Village_","").Replace("_Full",""); hs.Add(new H{ t=ch, name=ch.name, seed=seed }); }

            // [1] 방사 스케일 1.30 (위치만), Y 재샘플
            foreach(var h in hs){ Vector3 p=h.t.position; Vector2 xz=new Vector2(p.x,p.z);
                Vector2 nxz=new Vector2(CX,CZ)+(xz-new Vector2(CX,CZ))*Scale;
                h.t.position=new Vector3(nxz.x, TerrH(nxz.x,nxz.y), nxz.y); }
            RecalcBounds(hs);
            float mg0=MinBboxGap(hs); float bo0=CountBldgOverlap(hs);
            sb.AppendLine($"[1] 스케일 1.30 적용 · 스케일후 bbox최소간격 {mg0:F2}m · 건물본체 겹침쌍 {bo0:F0}");

            // [2] 건물본체 겹침만 분리(≤6m 누적), 담장 겹침은 유지
            int iters=0; var rth=LoadRth();
            for(int it=0; it<20; it++){ iters=it+1; bool moved=false;
                for(int i=0;i<hs.Count;i++) for(int j=i+1;j<hs.Count;j++){
                    var a=hs[i].bldg; var b=hs[j].bldg;
                    float ix=Mathf.Min(a.max.x,b.max.x)-Mathf.Max(a.min.x,b.min.x);
                    float iz=Mathf.Min(a.max.z,b.max.z)-Mathf.Max(a.min.z,b.min.z);
                    if(ix<=0.05f||iz<=0.05f) continue; // 건물본체 안 겹침 → 컴파운드/이웃, 유지
                    // 겹침 작은 축으로 분리 + 0.3 여유
                    Vector2 push; float need;
                    if(ix<iz){ float dir=Mathf.Sign(b.center.x-a.center.x); if(dir==0)dir=1; need=(ix*0.5f+0.3f); push=new Vector2(dir*need,0); }
                    else { float dir=Mathf.Sign(b.center.z-a.center.z); if(dir==0)dir=1; need=(iz*0.5f+0.3f); push=new Vector2(0,dir*need); }
                    Nudge(hs[i], -push, TerrH); Nudge(hs[j], push, TerrH); moved=true;
                }
                if(!moved) break;
            }
            RecalcBounds(hs);

            // [3] 동헌 남쪽 12m 마당자리 + 링밴드에서 밀어내기(반경 내측으로)
            Rect yard=Rect.MinMaxRect(462-9.35f,616-12f-5.67f,462+9.35f,616+2f);
            foreach(var h in hs){ int guard=0;
                while(guard++<40 && (RectOf(h.full).Overlaps(yard) || RingHit(RectOf(h.full),rth))){
                    Vector2 c=new Vector2(h.t.position.x,h.t.position.z); Vector2 toC=(new Vector2(CX,CZ)-c).normalized;
                    Nudge(h, toC*0.5f, TerrH); RecalcOne(h);
                    if(h.offset.magnitude>MaxOffset+3f) break;
                }
            }
            RecalcBounds(hs);

            // 최종 Y 재샘플
            foreach(var h in hs){ var p=h.t.position; h.t.position=new Vector3(p.x, TerrH(p.x,p.z), p.z); }
            SetStaticRec(holder);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, Scene);

            // 보고
            var mo4=hs.Where(h=>h.offset.magnitude>0.05f).OrderByDescending(h=>h.offset.magnitude).ToList();
            sb.AppendLine($"[2] 건물본체 분리 {iters}회 반복 · 이동한 채 {mo4.Count}개 (개별 오프셋, 스케일 제외):");
            foreach(var h in mo4) sb.AppendLine($"   {h.name}: Δ({h.offset.x:+0.0;-0.0},{h.offset.y:+0.0;-0.0}) = {h.offset.magnitude:F1}m {(h.offset.magnitude>MaxOffset?"⚠>6m":"")}");
            float mg1=MinBboxGap(hs); float bo1=CountBldgOverlap(hs);
            sb.AppendLine($"[3] 최종 · bbox최소간격 {mg1:F2}m · ★건물본체 잔존겹침 {bo1:F0}쌍 {(bo1==0?"✓":"⚠")} · 외곽반경 {hs.Max(h=>MaxRad(h.full)):F1}m");
            var yh=hs.Where(h=>RectOf(h.full).Overlaps(yard)).Select(h=>h.name).ToList();
            var rh=hs.Where(h=>RingHit(RectOf(h.full),rth)).Select(h=>h.name).ToList();
            sb.AppendLine($"[3] 동헌 마당자리 침범 {(yh.Count==0?"없음 ✓":string.Join(",",yh))} · 링밴드 침범 {(rh.Count==0?"없음 ✓":string.Join(",",rh))}");
            var over=hs.Where(h=>h.offset.magnitude>MaxOffset).Select(h=>h.name).ToList();
            if(over.Count>0) sb.AppendLine($"[3] ⚠ 오프셋 6m 초과 채: {string.Join(",",over)} (지시 한계 초과 — 확인 요망)");

            File.WriteAllText(Path.GetFullPath(TxtPath), sb.ToString(), new UTF8Encoding(true)); AssetDatabase.ImportAsset(TxtPath);
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"4b 간격 완료. 이동 {mo4.Count}채 · 건물잔존겹침 {bo1:F0} · 최소간격 {mg1:F1}m\n간격 확인 후 4c(흙+렌더). Console 확인.","확인");
        }

        private static void Nudge(H h, Vector2 d, System.Func<float,float,float> TerrH){ var p=h.t.position; float nx=p.x+d.x, nz=p.z+d.y;
            h.offset+=d; h.t.position=new Vector3(nx, TerrH(nx,nz), nz); RecalcOne(h); }
        private static void RecalcOne(H h){ var rends=h.t.GetComponentsInChildren<Renderer>(true); if(rends.Length==0){ h.full=new Bounds(h.t.position,Vector3.zero); h.bldg=h.full; return; }
            Bounds f=rends[0].bounds; foreach(var r in rends) f.Encapsulate(r.bounds); h.full=f;
            // 건물본체 = seed 이름 자식
            Transform bt=null; foreach(var tr in h.t.GetComponentsInChildren<Transform>(true)) if(tr.name==h.seed){ bt=tr; break; }
            if(bt!=null){ var br=bt.GetComponentsInChildren<Renderer>(true); if(br.Length>0){ Bounds bb=br[0].bounds; foreach(var r in br) bb.Encapsulate(r.bounds); h.bldg=bb; return; } }
            h.bldg=f; }
        private static void RecalcBounds(List<H> hs){ foreach(var h in hs) RecalcOne(h); }
        private static Rect RectOf(Bounds b){ return Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z); }
        private static float MaxRad(Bounds b){ var r=RectOf(b); float m=0; foreach(var c in new[]{ new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax) }){ float d=Vector2.Distance(c,new Vector2(CX,CZ)); if(d>m)m=d; } return m; }
        private static float GapOf(Rect a, Rect b){ float ix=Mathf.Max(a.xMin,b.xMin)-Mathf.Min(a.xMax,b.xMax); float iz=Mathf.Max(a.yMin,b.yMin)-Mathf.Min(a.yMax,b.yMax);
            if(ix>0&&iz>0) return Mathf.Sqrt(ix*ix+iz*iz); if(ix>0) return ix; if(iz>0) return iz; return Mathf.Max(ix,iz); }
        private static float MinBboxGap(List<H> hs){ float m=1e9f; for(int a=0;a<hs.Count;a++) for(int b=a+1;b<hs.Count;b++){ float g=GapOf(RectOf(hs[a].full),RectOf(hs[b].full)); if(g<m)m=g; } return m; }
        private static float CountBldgOverlap(List<H> hs){ int n=0; for(int a=0;a<hs.Count;a++) for(int b=a+1;b<hs.Count;b++){ var A=hs[a].bldg; var B=hs[b].bldg;
            float ix=Mathf.Min(A.max.x,B.max.x)-Mathf.Max(A.min.x,B.min.x); float iz=Mathf.Min(A.max.z,B.max.z)-Mathf.Max(A.min.z,B.min.z); if(ix>0.05f&&iz>0.05f) n++; } return n; }
        private static void SetStaticRec(GameObject g){ g.isStatic=true; foreach(Transform c in g.transform) SetStaticRec(c.gameObject); }
        private static List<Vector2> LoadRth(){ var list=new List<Vector2>(); if(!File.Exists(PathFile)) return list; var ci=CultureInfo.InvariantCulture; var lines=File.ReadAllLines(PathFile);
            for(int i=1;i<lines.Length;i++){ var t=lines[i].Split(' '); if(t.Length<2) continue; float x=float.Parse(t[0],ci),z=float.Parse(t[1],ci); list.Add(new Vector2(Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg, Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ)))); } return list; }
        private static float RthAt(List<Vector2> rth, float ang){ if(rth.Count==0) return 100f; float best=1e9f,br=100f; foreach(var v in rth){ float d=Mathf.Abs(Mathf.DeltaAngle(v.x,ang)); if(d<best){best=d;br=v.y;} } return br; }
        private static bool RingHit(Rect xz, List<Vector2> rth){ foreach(var p in new[]{ new Vector2(xz.center.x,xz.center.y), new Vector2(xz.xMin,xz.yMin),new Vector2(xz.xMax,xz.yMin),new Vector2(xz.xMin,xz.yMax),new Vector2(xz.xMax,xz.yMax) }){ float d=Vector2.Distance(p,new Vector2(CX,CZ)); float r=RthAt(rth, Mathf.Atan2(p.y-CZ,p.x-CX)*Mathf.Rad2Deg); if(Mathf.Abs(d-r)<=RingBand) return true; } return false; }
    }
}
