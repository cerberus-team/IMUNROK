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
    /// [4-1단계] 마을 간격/흙 스플랫 ★조사 전용★ — 지형·씬·프리팹 아무것도 변경/저장 안 함.
    /// Terrain 레이어·알파맵 / 흙 텍스처 후보 / 채 반경·간격 / 방사 스케일 1.15·1.30·1.45 시뮬.
    /// </summary>
    public static class SeocheonVillageSpace
    {
        private const string Scene   = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string PathFile= @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const string TxtPath = "Assets/_Project/Seocheon/Data/village_space_investigate.txt";
        private const float CX=463f, CZ=678f, RingBand=11.8f;
        private static readonly float[] Scales={1.15f,1.30f,1.45f};
        // 흙 후보 텍스처(낙안 팩)
        private static readonly string[] DirtTex={
            "Assets/Naganeupseong/Resource/Textures/Ground/T_Ground01a_BC.png",
            "Assets/Naganeupseong/Resource/Textures/Ground/T_Ground02a_BC.png",
            "Assets/Naganeupseong/Scene/Resource/Terrain/T_Ground01b_BC.png",
            "Assets/Naganeupseong/Scene/Resource/Terrain/T_Ground02b_BC.png",
            "Assets/Naganeupseong/Resource/Textures/Floor/T_Floor01a_BC.png",
            "Assets/Naganeupseong/Resource/Textures/Floor/T_Floor01b_BC.png",
            "Assets/Naganeupseong/Resource/Textures/Floor/T_Floor01d_BC.png" };
        private static readonly string[] DirtLayer={
            "Assets/Naganeupseong/Scene/Resource/Terrain/TR_Ground01b.terrainlayer",
            "Assets/Naganeupseong/Scene/Resource/Terrain/TR_Ground02b.terrainlayer" };

        private class H { public string name; public Vector2 c; public Rect r; public float rad, maxRad; }

        [MenuItem("Tools/Seocheon/Village/4a. Space+Splat Investigate (read-only)")]
        public static void Investigate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] Village 4a Space+Splat 조사 (읽기전용) =====");

            // [1a] Terrain 레이어·알파맵
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null;
            if(terrain==null){ sb.AppendLine("Terrain 없음."); Debug.Log(sb.ToString()); return; }
            var td=terrain.terrainData;
            sb.AppendLine($"\n[1a] Terrain: 알파맵 {td.alphamapResolution} · 하이트맵 {td.heightmapResolution} · size {td.size.x:F0}×{td.size.z:F0} · 레이어 {td.terrainLayers.Length}개");
            for(int i=0;i<td.terrainLayers.Length;i++){ var L=td.terrainLayers[i]; if(L==null){ sb.AppendLine($"   #{i} (null)"); continue; }
                sb.AppendLine($"   #{i} {L.name} · tex {(L.diffuseTexture!=null?L.diffuseTexture.name:"-")} · 타일 {L.tileSize.x:F1}×{L.tileSize.y:F1}m · offset {L.tileOffset.x:F1},{L.tileOffset.y:F1}"); }
            sb.AppendLine($"   ※ 레이어 여유: 현재 {td.terrainLayers.Length}개 → 흙 1장 추가 시 {td.terrainLayers.Length+1}개 (4 초과면 중단 규칙)");

            // [1b] 흙 텍스처 후보
            sb.AppendLine("\n[1b] 흙·마사토·황토 후보 텍스처(낙안 팩):");
            foreach(var p in DirtTex){ var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(p); if(tex==null){ sb.AppendLine($"   (없음) {p}"); continue; }
                var ti=(TextureImporter)AssetImporter.GetAtPath(p); string wrap=ti!=null?ti.wrapMode.ToString():"?";
                sb.AppendLine($"   {tex.width}×{tex.height} · wrap {wrap} {(wrap=="Repeat"?"✓타일러블":"⚠")} · {Path.GetFileName(p)}"); }
            sb.AppendLine("   기존 TerrainLayer 에셋:");
            foreach(var p in DirtLayer){ var L=AssetDatabase.LoadAssetAtPath<TerrainLayer>(p); if(L==null){ sb.AppendLine($"   (없음) {p}"); continue; }
                sb.AppendLine($"   {L.name} · tex {(L.diffuseTexture!=null?L.diffuseTexture.name:"-")} · 타일 {L.tileSize.x:F1}m · {Path.GetFileName(p)}"); }

            // [1c] 채 28 반경·간격
            var holder=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="_Village");
            if(holder==null){ sb.AppendLine("\n[1c] _Village 루트 없음 — 3단계 배치 먼저."); Debug.Log(sb.ToString()); return; }
            var hs=new List<H>();
            foreach(Transform ch in holder.transform){ var rends=ch.GetComponentsInChildren<Renderer>(true); if(rends.Length==0) continue;
                Bounds b=rends[0].bounds; foreach(var r in rends) b.Encapsulate(r.bounds);
                var rect=Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z); Vector2 c=new Vector2(b.center.x,b.center.z);
                float maxr=0; foreach(var cc in Corners(rect)){ float d=Vector2.Distance(cc,new Vector2(CX,CZ)); if(d>maxr) maxr=d; }
                hs.Add(new H{ name=ch.name, c=c, r=rect, rad=Vector2.Distance(c,new Vector2(CX,CZ)), maxRad=maxr }); }
            var rth=LoadRth(); float ringMin=rth.Count>0?rth.Min(v=>v.y):100f;
            float curMinGap=MinGap(hs.Select(h=>h.r).ToList()); float curOuter=hs.Max(h=>h.maxRad);
            sb.AppendLine($"\n[1c] 채 {hs.Count}개 · 외곽 최대반경 {curOuter:F1}m · 채끼리 최소간격 {curMinGap:F2}m {(curMinGap<0?"(겹침)":"")} · 개천 링 최소반경 {ringMin:F1}m · 빈 잔디 {ringMin-curOuter:F1}m");
            sb.AppendLine("   채별 반경(중심 463,678):");
            foreach(var h in hs.OrderBy(h=>h.rad)) sb.AppendLine($"      {h.rad,5:F1}m (외곽 {h.maxRad,5:F1}) · {h.name}");

            // [1d] 방사 스케일 시뮬 (동헌·다리·마커 고정)
            Rect donheon=Rect.MinMaxRect(462-9.35f,616-5.67f,462+9.35f,616+5.67f);
            sb.AppendLine("\n[1d] 방사 스케일 시뮬 (채 위치만 중심기준 확대; 동헌·다리·마커 고정):");
            sb.AppendLine("   scale | 최소간격 | 외곽반경 | 링까지잔디 | 링밴드침범 | 동헌겹침 | 통행2.5m달성");
            foreach(var s in Scales){ var recs=hs.Select(h=>Scaled(h.r,h.c,s)).ToList();
                float mg=MinGap(recs); float outer=hs.Max(h=>MaxRad(Scaled(h.r,h.c,s)));
                int ring=0,don=0; foreach(var rc in recs){ if(RingHit(rc,rth)) ring++; if(rc.Overlaps(donheon)) don++; }
                sb.AppendLine($"   {s:F2}  | {mg,6:F2}m | {outer,6:F1}m | {ringMin-outer,6:F1}m | {ring,2}채 | {don,2}채 | {(mg>=2.5f?"✓":"✗ ("+mg.ToString("F1")+"m)")}"); }

            // [1e] 동헌 남쪽 12m 마당 자리 침범(현재)
            Rect yard=Rect.MinMaxRect(462-9.35f,616-12f-5.67f,462+9.35f,616-0f); // 동헌 정면(남)에서 12m
            var yardHit=hs.Where(h=>h.r.Overlaps(yard)).Select(h=>h.name).ToList();
            sb.AppendLine($"\n[1e] 동헌 정면 남쪽 12m 마당예정지 침범 채(현재): {(yardHit.Count==0?"없음 ✓":string.Join(", ",yardHit))}");

            sb.AppendLine("\n※ 4a는 조사만. 스케일 확정 주시면 4b(간격) → 4c(흙 스플랫) 진행.");
            File.WriteAllText(Path.GetFullPath(TxtPath), sb.ToString(), new UTF8Encoding(true)); AssetDatabase.ImportAsset(TxtPath);
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon","4a 조사 완료(읽기전용). Console/Data txt 확인.\n스케일(1.15/1.30/1.45) 골라주세요.","확인");
        }

        private static Vector2[] Corners(Rect r){ return new[]{ new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax) }; }
        private static Rect Scaled(Rect r, Vector2 c, float s){ Vector2 nc=new Vector2(CX,CZ)+(c-new Vector2(CX,CZ))*s; Vector2 d=nc-c; return new Rect(r.x+d.x,r.y+d.y,r.width,r.height); }
        private static float MaxRad(Rect r){ float m=0; foreach(var cc in Corners(r)){ float d=Vector2.Distance(cc,new Vector2(CX,CZ)); if(d>m) m=d; } return m; }
        private static float GapOf(Rect a, Rect b){ float ix=Mathf.Max(a.xMin,b.xMin)-Mathf.Min(a.xMax,b.xMax); float iz=Mathf.Max(a.yMin,b.yMin)-Mathf.Min(a.yMax,b.yMax);
            // 두 축 다 양수면 대각선 거리, 하나만 양수면 그 값, 둘다 음수면 겹침(음수 반환)
            if(ix>0&&iz>0) return Mathf.Sqrt(ix*ix+iz*iz); if(ix>0) return ix; if(iz>0) return iz; return Mathf.Max(ix,iz); }
        private static float MinGap(List<Rect> rs){ float m=1e9f; for(int a=0;a<rs.Count;a++) for(int b=a+1;b<rs.Count;b++){ float g=GapOf(rs[a],rs[b]); if(g<m) m=g; } return m; }
        private static List<Vector2> LoadRth(){ var list=new List<Vector2>(); if(!File.Exists(PathFile)) return list; var ci=CultureInfo.InvariantCulture; var lines=File.ReadAllLines(PathFile);
            for(int i=1;i<lines.Length;i++){ var t=lines[i].Split(' '); if(t.Length<2) continue; float x=float.Parse(t[0],ci),z=float.Parse(t[1],ci); list.Add(new Vector2(Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg, Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ)))); } return list; }
        private static float RthAt(List<Vector2> rth, float ang){ if(rth.Count==0) return 100f; float best=1e9f,br=100f; foreach(var v in rth){ float d=Mathf.Abs(Mathf.DeltaAngle(v.x,ang)); if(d<best){best=d;br=v.y;} } return br; }
        private static bool RingHit(Rect xz, List<Vector2> rth){ foreach(var p in new[]{ new Vector2(xz.center.x,xz.center.y) }.Concat(Corners(xz))){ float d=Vector2.Distance(p,new Vector2(CX,CZ)); float r=RthAt(rth, Mathf.Atan2(p.y-CZ,p.x-CX)*Mathf.Rad2Deg); if(Mathf.Abs(d-r)<=RingBand) return true; } return false; }
    }
}
