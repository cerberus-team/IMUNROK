using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    /// [3단계] 창고 1채 _Full 재추출 + 마을 28채 서천 씬 배치 + 충돌검사 + 검증 + 렌더.
    /// ★배치 회전=identity(방향은 프리팹에 구워짐; origRotY 이중적용 금지).
    /// ★Y는 CSV terrainH 아닌 현재 씬 지형 재샘플. 프리팹 인스턴스(unpack 금지)·Static ON·라이트맵 베이크 없음.
    /// ★원본 Demo/낙안 팩 미변경. 개천 링·수면·다리·동헌 미변경(측정만).
    /// </summary>
    public static class SeocheonVillagePlace
    {
        private const string DemoScene = "Assets/Naganeupseong/Scene/Demo.unity";
        private const string WipScene  = "Assets/_Project/Seocheon/Scenes/_VillageSplit_WIP.unity";
        private const string Scene     = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string OutDir    = "Assets/_Project/Seocheon/Prefabs/Village";
        private const string CsvIn     = "Assets/_Project/Seocheon/Data/village_placement.csv";
        private const string TxtPath   = "Assets/_Project/Seocheon/Data/village_place_report.txt";
        private const string PathFile  = @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const string RenderDir = @"C:\Users\User\_AssetBackup\render_village1";
        private const string BuildDir="/Prefabs/Build/", StructDir="/Prefabs/Structure/", PropDir="/Prefabs/Prop/", MeshPropDir="/Resource/Meshes/Prop/";
        private const float CX=463f, CZ=678f, RingBand=11.8f, AssignRadius=15f;
        private const string WarehouseSeed="House_By_The_West_Gate_01";

        [MenuItem("Tools/Seocheon/Village/3. Warehouse _Full + Place")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가. Edit 모드로.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] Village 3 Place =====");

            // ───────── [0] 창고 _Full 재추출 (fresh Demo 사본) ─────────
            EnsureFolder("Assets/_Project/Seocheon/Scenes");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(WipScene)!=null) AssetDatabase.DeleteAsset(WipScene);
            AssetDatabase.CopyAsset(DemoScene, WipScene); AssetDatabase.Refresh();
            var wip=EditorSceneManager.OpenScene(WipScene, OpenSceneMode.Single);
            var wu=CollectUnits(wip); var wseeds=wu.Where(u=>u.tier=='A').ToList();
            for(int i=0;i<wseeds.Count;i++) wseeds[i].cluster=i;
            foreach(var u in wu){ if(u.tier=='A') continue; int best=-1; float bd=AssignRadius; for(int i=0;i<wseeds.Count;i++){ float d=Vector2.Distance(u.xz,wseeds[i].xz); if(d<=bd){bd=d;best=i;} } u.cluster=best; }
            int wi=wseeds.FindIndex(s=>s.assetName==WarehouseSeed);
            string fullPath=$"{OutDir}/Village_{WarehouseSeed}_Full.prefab";
            if(wi<0) sb.AppendLine($"[0] ⚠ 창고 시드 {WarehouseSeed} 못 찾음 — _Full 생략");
            else {
                var seed=wseeds[wi]; var members=wu.Where(u=>u.cluster==wi).ToList(); if(!members.Contains(seed)) members.Add(seed);
                float minY=members.Min(u=>u.b.min.y);
                var root=new GameObject($"Village_{WarehouseSeed}_Full");
                root.transform.position=new Vector3(seed.xz.x,minY,seed.xz.y); root.transform.rotation=Quaternion.identity;
                foreach(var u in members) u.go.transform.SetParent(root.transform,true);
                PrefabUtility.SaveAsPrefabAsset(root, fullPath, out bool ok);
                int cA=members.Count(u=>u.tier=='A'),cB=members.Count(u=>u.tier=='B'),cC=members.Count(u=>u.tier=='C');
                sb.AppendLine($"[0] 창고 _Full 저장 {(ok?"✓":"⚠")}: {fullPath} · A{cA} B{cB} C{cC} · tris {members.Sum(u=>u.tris)} (A+B판 유지)");
            }
            EditorSceneManager.SaveScene(wip, WipScene); // WIP만 저장, Demo 미변경

            // ───────── [1] 배치 ─────────
            var scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terr.Length>0?terr[0]:null;
            float TerrH(float x,float z){ if(terrain==null) return 0f; return terrain.transform.position.y+terrain.SampleHeight(new Vector3(x,0,z)); }
            foreach(var r in scene.GetRootGameObjects()) if(r.name=="_Village") Object.DestroyImmediate(r);
            var holder=new GameObject("_Village");

            var rows=File.ReadAllLines(Path.GetFullPath(CsvIn)); var placed=new List<(string name,GameObject go,Rect xz,float rotY)>();
            int nPlaced=0, missPrefab=0;
            for(int i=1;i<rows.Length;i++){ var t=rows[i].Split(','); if(t.Length<16) continue;
                string prefabPath=t[0]; float posX=PF(t[12]), posZ=PF(t[14]), origRotY=PF(t[15]);
                if(prefabPath.Contains(WarehouseSeed) && !prefabPath.Contains("_Full")) prefabPath=fullPath; // 창고는 _Full 사용
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); if(prefab==null){ missPrefab++; sb.AppendLine($"[1] ⚠ 프리팹 로드 실패: {prefabPath}"); continue; }
                var inst=(GameObject)PrefabUtility.InstantiatePrefab(prefab); inst.transform.SetParent(holder.transform);
                float reY=TerrH(posX,posZ);
                inst.transform.position=new Vector3(posX, reY, posZ); inst.transform.rotation=Quaternion.identity; // ★identity(방향은 구워짐)
                SetStaticRec(inst);
                var rends=inst.GetComponentsInChildren<Renderer>(true); if(rends.Length==0){ placed.Add((inst.name,inst,new Rect(posX,posZ,0,0),origRotY)); nPlaced++; continue; }
                Bounds wb=rends[0].bounds; foreach(var rr in rends) wb.Encapsulate(rr.bounds);
                placed.Add((inst.name, inst, Rect.MinMaxRect(wb.min.x,wb.min.z,wb.max.x,wb.max.z), origRotY)); nPlaced++;
            }
            SetStaticRec(holder);
            sb.AppendLine($"\n[1] 배치 {nPlaced}채 (프리팹 실패 {missPrefab}) · 루트 _Village · 회전 identity · Y=현재지형 재샘플 · Static ON");

            // ───────── [2] 충돌 검사(배치는 유지, 겹침만 보고) ─────────
            sb.AppendLine("\n[2] 충돌 검사 (배치 유지·겹침만):");
            // 동헌 박스
            Rect donheon=Rect.MinMaxRect(462-9.35f,616-5.67f,462+9.35f,616+5.67f);
            var hitDon=placed.Where(p=>p.xz.Overlaps(donheon)).Select(p=>p.name).ToList();
            sb.AppendLine($"  · 동헌(462,616 18.7×11.34) 겹침: {(hitDon.Count==0?"없음 ✓":string.Join(", ",hitDon))}");
            // 다리
            var bridgeGOs=scene.GetRootGameObjects().Where(g=>g.name.ToLowerInvariant().Contains("bridge")||g.name.Contains("다리")).ToList();
            Rect bridge; if(bridgeGOs.Count>0){ var b=bridgeGOs[0].GetComponentsInChildren<Renderer>(true); if(b.Length>0){ Bounds bb=b[0].bounds; foreach(var r in b) bb.Encapsulate(r.bounds); bridge=Rect.MinMaxRect(bb.min.x,bb.min.z,bb.max.x,bb.max.z);} else bridge=new Rect(462-4,571-6,8,12);} else bridge=new Rect(462-4,571-6,8,12);
            var hitBr=placed.Where(p=>p.xz.Overlaps(bridge)).Select(p=>p.name).ToList();
            sb.AppendLine($"  · 다리({(bridgeGOs.Count>0?bridgeGOs[0].name:"추정 462,571")}) 겹침: {(hitBr.Count==0?"없음 ✓":string.Join(", ",hitBr))}");
            // 링 밴드
            var rth=LoadRth();
            var hitRing=new List<string>();
            foreach(var p in placed){ if(RingHit(p.xz,rth)) hitRing.Add(p.name); }
            sb.AppendLine($"  · ★개천 링밴드(중심선 ±{RingBand}m) 침범: {(hitRing.Count==0?"없음 ✓":string.Join(", ",hitRing))}");
            // 존 마커 7개
            string[] mk={"Entry","Tavern","Donheon","Storehouse","Gate","Mudang","Tomb"};
            sb.AppendLine("  · 존 마커 겹침:");
            var allGO=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToList();
            foreach(var m in mk){ var mt=allGO.FirstOrDefault(x=>x.gameObject.name.ToLowerInvariant().Contains(m.ToLowerInvariant()) && !x.IsChildOf(holder.transform));
                if(mt==null){ sb.AppendLine($"     {m}: 마커 없음"); continue; }
                Vector2 mp=new Vector2(mt.position.x,mt.position.z); var hit=placed.Where(p=>p.xz.Contains(mp)).Select(p=>p.name).ToList();
                sb.AppendLine($"     {m}({mp.x:F0},{mp.y:F0}): {(hit.Count==0?"겹침 없음":string.Join(",",hit))}"); }
            // 채끼리
            var pairs=new List<string>();
            for(int a=0;a<placed.Count;a++) for(int b=a+1;b<placed.Count;b++){ var A=placed[a].xz; var B=placed[b].xz; var ov=Overlap(A,B); if(ov>0.5f) pairs.Add($"{placed[a].name}↔{placed[b].name} ({ov:F1}㎡)"); }
            sb.AppendLine($"  · 채끼리 bbox 겹침(>0.5㎡): {(pairs.Count==0?"없음 ✓":$"{pairs.Count}쌍")}");
            foreach(var pr in pairs.Take(20)) sb.AppendLine("     "+pr);

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, Scene);

            // ───────── [3] 검증 ─────────
            int sceneTris=0; foreach(var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) if(mf.sharedMesh!=null) for(int s=0;s<mf.sharedMesh.subMeshCount;s++) sceneTris+=(int)(mf.sharedMesh.GetIndexCount(s)/3);
            sb.AppendLine($"\n[3] 검증 · 배치 {nPlaced}채 · 씬 총 tris {sceneTris}");
            sb.AppendLine("  밑면-지형 틈>0.15m 인 채 (9점 샘플):");
            float villMaxR=0; int gapCount=0;
            foreach(var p in placed){ if(p.go==null) continue; var rends=p.go.GetComponentsInChildren<Renderer>(true); if(rends.Length==0) continue;
                Bounds wb=rends[0].bounds; foreach(var r in rends) wb.Encapsulate(r.bounds);
                float bottomY=wb.min.y, maxGap=0,maxEmbed=0;
                float hx=wb.size.x*0.4f, hz=wb.size.z*0.4f, cx=wb.center.x, cz=wb.center.z;
                for(int a=-1;a<=1;a++) for(int b=-1;b<=1;b++){ float dh=TerrH(cx+a*hx,cz+b*hz)-bottomY; if(dh>maxEmbed)maxEmbed=dh; if(-dh>maxGap)maxGap=-dh; }
                if(maxGap>0.15f){ gapCount++; sb.AppendLine($"     {p.name}: 틈 {maxGap:F2}m · 파묻힘 {maxEmbed:F2}m"); }
                foreach(var c in new[]{new Vector2(wb.min.x,wb.min.z),new Vector2(wb.max.x,wb.min.z),new Vector2(wb.min.x,wb.max.z),new Vector2(wb.max.x,wb.max.z)}){ float d=Vector2.Distance(c,new Vector2(CX,CZ)); if(d>villMaxR) villMaxR=d; }
            }
            if(gapCount==0) sb.AppendLine("     없음 ✓ (전 채 틈 ≤0.15m)");
            float ringMinR=RingMin(rth);
            sb.AppendLine($"  ★마을 외곽반경(중심 463,678 최대거리) {villMaxR:F1}m · 개천 링 최소반경 {ringMinR:F1}m · 빈 잔디 {ringMinR-villMaxR:F1}m");

            // ───────── [4] 렌더 4장 ─────────
            Directory.CreateDirectory(RenderDir); var files=new List<string>(); string rerr=null;
            float brY=DeckTop(462f,571f,scene,holder); if(float.IsNaN(brY)) brY=TerrH(462,571);
            try{
                files.Add(CamTop("Aerial_250", 463f,650f,250f));
                files.Add(Cam("OnBridge",           new Vector3(462,brY+1.6f,571), new Vector3(463,TerrH(463,640)+6f,645)));
                files.Add(Cam("Donheon_to_village", new Vector3(462,TerrH(462,605)+1.6f,605), new Vector3(463,TerrH(463,660)+7f,678)));
                files.Add(Cam("Street",             new Vector3(474,TerrH(474,703)+1.6f,703), new Vector3(463,TerrH(463,685)+3f,682)));
            }catch(System.Exception e){ rerr=e.ToString(); }
            if(rerr!=null) sb.AppendLine("\n[4] 렌더 실패: "+rerr);
            else { sb.AppendLine($"\n[4] 렌더 {files.Count}장 → {RenderDir}"); foreach(var f in files) sb.AppendLine("   "+f); }

            File.WriteAllText(Path.GetFullPath(TxtPath), sb.ToString(), new UTF8Encoding(true)); AssetDatabase.ImportAsset(TxtPath);
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"3단계 완료. 배치 {nPlaced}채 · 링침범 {hitRing.Count} · 채겹침 {pairs.Count}쌍 · 빈잔디 {ringMinR-villMaxR:F0}m\n렌더 4장 → render_village1. Console 확인.","확인");
        }

        // ── 유닛 수집(폴더 분류, sky제외, raw prop→C) ──
        private class U { public GameObject go; public string assetName; public char tier; public int tris; public Bounds b; public Vector2 xz; public int cluster=-1; }
        private static List<U> CollectUnits(Scene sc){ var units=new List<U>();
            foreach(var r in sc.GetRootGameObjects()){ var go=r.gameObject;
                if(go.GetComponent<Terrain>()||go.GetComponent<Light>()||go.GetComponent<Camera>()||go.GetComponent<ReflectionProbe>()) continue;
                if(!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                var ap=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go); char tier;
                if(ap.Contains(BuildDir)) tier='A'; else if(ap.Contains(StructDir)) tier='B'; else if(ap.Contains(PropDir)) tier='C';
                else if(go.name.ToLowerInvariant().Contains("sky")) continue; else if(ap.Contains(MeshPropDir)) tier='C'; else continue;
                int tris=0; bool hasB=false; Bounds bb=new Bounds();
                foreach(var mf in go.GetComponentsInChildren<MeshFilter>(true)){ var mr=mf.GetComponent<MeshRenderer>(); if(mr==null||mf.sharedMesh==null) continue; tris+=MeshTris(mf.sharedMesh); if(!hasB){bb=mr.bounds;hasB=true;} else bb.Encapsulate(mr.bounds); }
                foreach(var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){ if(smr.sharedMesh==null) continue; tris+=MeshTris(smr.sharedMesh); if(!hasB){bb=smr.bounds;hasB=true;} else bb.Encapsulate(smr.bounds); }
                var b2=hasB?bb:new Bounds(go.transform.position,Vector3.zero);
                units.Add(new U{ go=go, assetName=Path.GetFileNameWithoutExtension(ap), tier=tier, tris=tris, b=b2, xz=new Vector2(b2.center.x,b2.center.z) }); }
            return units; }

        private static float PF(string s){ return float.Parse(s, CultureInfo.InvariantCulture); }
        private static int MeshTris(Mesh m){ int t=0; for(int s=0;s<m.subMeshCount;s++) t+=(int)(m.GetIndexCount(s)/3); return t; }
        private static void SetStaticRec(GameObject g){ g.isStatic=true; foreach(Transform c in g.transform) SetStaticRec(c.gameObject); }
        private static float Overlap(Rect a, Rect b){ float ix=Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin); float iz=Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin); return (ix>0&&iz>0)?ix*iz:0f; }
        private static List<Vector2> LoadRth(){ var list=new List<Vector2>(); if(!File.Exists(PathFile)) return list; var ci=CultureInfo.InvariantCulture; var lines=File.ReadAllLines(PathFile);
            for(int i=1;i<lines.Length;i++){ var t=lines[i].Split(' '); if(t.Length<2) continue; float x=float.Parse(t[0],ci),z=float.Parse(t[1],ci); list.Add(new Vector2(Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg, Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ)))); } return list; }
        private static float RthAt(List<Vector2> rth, float ang){ if(rth.Count==0) return 100f; float best=1e9f,br=100f; foreach(var v in rth){ float d=Mathf.Abs(Mathf.DeltaAngle(v.x,ang)); if(d<best){best=d;br=v.y;} } return br; }
        private static float RingMin(List<Vector2> rth){ float m=1e9f; foreach(var v in rth) if(v.y<m) m=v.y; return m<1e9f?m:100f; }
        private static bool RingHit(Rect xz, List<Vector2> rth){ var pts=new[]{ new Vector2(xz.center.x,xz.center.y), new Vector2(xz.xMin,xz.yMin), new Vector2(xz.xMax,xz.yMin), new Vector2(xz.xMin,xz.yMax), new Vector2(xz.xMax,xz.yMax) };
            foreach(var p in pts){ float d=Vector2.Distance(p,new Vector2(CX,CZ)); float r=RthAt(rth, Mathf.Atan2(p.y-CZ,p.x-CX)*Mathf.Rad2Deg); if(Mathf.Abs(d-r)<=RingBand) return true; } return false; }

        private static float DeckTop(float x,float z,Scene sc,GameObject skip){ float best=float.NaN;
            foreach(var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)){ bool sk=false; for(var p=mr.transform;p!=null;p=p.parent){ if(skip!=null&&p==skip.transform){sk=true;break;} } if(sk) continue;
                var b=mr.bounds; if(x>=b.min.x&&x<=b.max.x&&z>=b.min.z&&z<=b.max.z){ if(float.IsNaN(best)||b.max.y>best) best=b.max.y; } } return best; }

        private static string Cam(string name,Vector3 pos,Vector3 look){ return Shot(name,pos,(look-pos).normalized,Vector3.up,60f); }
        private static string CamTop(string name,float cx,float cz,float alt){ var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain t=terr.Length>0?terr[0]:null;
            float g=t!=null?t.transform.position.y+t.SampleHeight(new Vector3(cx,0,cz)):0f; return Shot(name,new Vector3(cx,g+alt,cz),Vector3.down,Vector3.forward,70f); }
        private static string Shot(string name,Vector3 pos,Vector3 fwd,Vector3 up,float fov){
            const int W=1600,H=900; var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
            var camGO=new GameObject("__c"); var cam=camGO.AddComponent<Camera>(); cam.clearFlags=CameraClearFlags.Skybox; cam.fieldOfView=fov; cam.nearClipPlane=0.05f; cam.farClipPlane=4000f; cam.enabled=false;
            cam.transform.position=pos; cam.transform.rotation=Quaternion.LookRotation(fwd,up); string fn=Path.Combine(RenderDir,name+".png");
            try{ var req=new UniversalRenderPipeline.SingleCameraRequest(); if(RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req);} else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null; File.WriteAllBytes(fn,tex.EncodeToPNG()); }
            finally{ Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
            return fn;
        }
        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
    }
}
