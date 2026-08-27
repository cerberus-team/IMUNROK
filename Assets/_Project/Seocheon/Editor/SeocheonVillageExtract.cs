using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// [2a단계] 낙안읍성 Demo 분류·조사 — ★읽기 전용★(프리팹/씬 저장 없음).
    /// ★폴더 기반 3계층: Prefabs/Build=A · Prefabs/Structure=B · Prefabs/Prop=C.
    /// ★시드 = Build 28개만(1단계 Hand_Mill/Winnow 오염 제거). 15m 재클러스터링.
    /// 소품 정책 프리뷰 + 창고후보 top3 + 담장 조사 + 저폴리 벽 FBX 실측.
    /// </summary>
    public static class SeocheonVillageExtract
    {
        private const string DemoScene = "Assets/Naganeupseong/Scene/Demo.unity";
        private const string DataDir   = "Assets/_Project/Seocheon/Data";
        private const string PreviewCsv= DataDir + "/village_placement_preview.csv";
        private const string TxtPath   = DataDir + "/village_extract_2a_report.txt";
        private const string WallMeshDir="Assets/Naganeupseong/Resource/Meshes/Wall";

        private const string BuildDir  = "/Prefabs/Build/";
        private const string StructDir = "/Prefabs/Structure/";
        private const string PropDir   = "/Prefabs/Prop/";
        private const float AssignRadius = 15f;

        // 창고 후보: 곡식·독·가마니류 소품 토큰
        private static readonly string[] WarehouseTokens = {
            "Rice","Grain","Corn","Bean","Meju","Straw_Bag","Straw_Jar","Sack","Jar","Jars","Chest","Bushel","Barley","Millet" };

        private class Unit {
            public GameObject go; public string assetPath; public string assetName; public char tier; // A/B/C/?
            public int tris; public Bounds b; public Vector2 xz; public Quaternion rot; public int cluster=-1;
        }

        [MenuItem("Tools/Seocheon/Village/2a. Classify + Investigate (read-only)")]
        public static void Classify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드에서는 실행 불가. Edit 모드로.","확인"); return; }
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScene)==null){ EditorUtility.DisplayDialog("Seocheon","Demo 씬 없음.","확인"); return; }
            string hashBefore = FileHash(DemoScene);
            var demo = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Additive);
            var sb = new StringBuilder(); sb.AppendLine("===== [Seocheon] Village 2a Classify+Investigate (읽기 전용) =====");

            Terrain terrain=null; foreach(var r in demo.GetRootGameObjects()){ var t=r.GetComponentInChildren<Terrain>(true); if(t!=null){ terrain=t; break; } }
            float TerrH(float x,float z){ if(terrain==null) return float.NaN; return terrain.transform.position.y+terrain.SampleHeight(new Vector3(x,0,z)); }

            // ── 유닛 수집(루트=프리팹 인스턴스) + 폴더 기반 분류 ──
            var units=new List<Unit>(); var cat3=new List<string>();
            foreach(var r in demo.GetRootGameObjects()){
                var go=r.gameObject;
                if(go.GetComponent<Terrain>()||go.GetComponent<Light>()||go.GetComponent<Camera>()||go.GetComponent<ReflectionProbe>()){ cat3.Add(go.name); continue; }
                if(!PrefabUtility.IsAnyPrefabInstanceRoot(go)){ // 비프리팹 루트 → Ambiguous 후보
                    units.Add(new Unit{ go=go, assetPath="", assetName=go.name, tier='?' }); continue; }
                var ap=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                char tier = ap.Contains(BuildDir)?'A' : ap.Contains(StructDir)?'B' : ap.Contains(PropDir)?'C' : '?';
                units.Add(new Unit{ go=go, assetPath=ap, assetName=Path.GetFileNameWithoutExtension(ap), tier=tier });
            }
            // 메트릭
            foreach(var u in units){ int tris=0; bool hasB=false; Bounds bb=new Bounds();
                foreach(var mf in u.go.GetComponentsInChildren<MeshFilter>(true)){ var mr=mf.GetComponent<MeshRenderer>(); if(mr==null||mf.sharedMesh==null) continue; tris+=MeshTris(mf.sharedMesh); if(!hasB){bb=mr.bounds;hasB=true;} else bb.Encapsulate(mr.bounds); }
                foreach(var smr in u.go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){ if(smr.sharedMesh==null) continue; tris+=MeshTris(smr.sharedMesh); if(!hasB){bb=smr.bounds;hasB=true;} else bb.Encapsulate(smr.bounds); }
                u.tris=tris; u.b=hasB?bb:new Bounds(u.go.transform.position,Vector3.zero); u.xz=new Vector2(u.b.center.x,u.b.center.z); u.rot=u.go.transform.rotation; }

            // ── 시드 = A(Build) 28개. 15m 재클러스터링 ──
            var seeds=units.Where(u=>u.tier=='A').ToList();
            for(int i=0;i<seeds.Count;i++) seeds[i].cluster=i;
            var unassigned=new List<Unit>();
            foreach(var u in units){ if(u.tier=='A') continue;
                int best=-1; float bd=AssignRadius;
                for(int i=0;i<seeds.Count;i++){ float d=Vector2.Distance(u.xz,seeds[i].xz); if(d<=bd){bd=d;best=i;} }
                if(best>=0) u.cluster=best; else { unassigned.Add(u); u.cluster=-1; }
            }

            sb.AppendLine($"\n[분류] 유닛 {units.Count} · A(Build) {units.Count(u=>u.tier=='A')} · B(Structure) {units.Count(u=>u.tier=='B')} · C(Prop) {units.Count(u=>u.tier=='C')} · ?(Ambiguous) {units.Count(u=>u.tier=='?')} · Cat3 {cat3.Count}");
            sb.AppendLine($"[클러스터] 시드(건물) {seeds.Count}개 · _Unassigned {unassigned.Count}개 (1단계 35→2a {seeds.Count} : Hand_Mill/Winnow 가짜시드 제거)");

            // ── _Ambiguous: 폴더 미분류 프리팹 종류 ──
            var amb = units.Where(u=>u.tier=='?').ToList();
            sb.AppendLine($"\n[_Ambiguous] 폴더 미분류 {amb.Count}개 (임의 판정 안 함 — 지시 요망):");
            foreach(var grp in amb.GroupBy(u=>string.IsNullOrEmpty(u.assetPath)?"(비프리팹) "+u.assetName:u.assetPath)) sb.AppendLine($"   {grp.Count(),3} × {grp.Key}");
            if(amb.Count==0) sb.AppendLine("   없음 ✓ (전부 Build/Structure/Prop 로 분류됨)");

            // ── 클러스터별 판정 + 정책 + 프리뷰 CSV ──
            var csv=new StringBuilder();
            csv.AppendLine("prefab,policy,A,B,C,trisFull,trisAfter,bboxW,bboxD,bboxH,posX,posY,posZ,rotY,terrainH");
            int sumFull=0, sumAfter=0;
            var clusterInfo=new List<(int idx,string name,string policy,int A,int B,int C,int tf,int ta)>();
            for(int i=0;i<seeds.Count;i++){
                var seed=seeds[i]; var members=units.Where(u=>u.cluster==i).ToList(); members.Add(seed);
                members=members.Distinct().ToList();
                int cA=members.Count(u=>u.tier=='A'), cB=members.Count(u=>u.tier=='B'), cC=members.Count(u=>u.tier=='C');
                bool isTavern = seed.assetName.ToLowerInvariant().Contains("tavern");
                string policy = isTavern ? "Full(A+B+C)" : "A+B";
                var incl = policy.StartsWith("Full") ? members : members.Where(u=>u.tier!='C').ToList();
                int trisFull=members.Sum(u=>u.tris); int trisAfter=incl.Sum(u=>u.tris);
                sumFull+=trisFull; sumAfter+=trisAfter;
                Bounds cb=incl[0].b; foreach(var m in incl) cb.Encapsulate(m.b);
                Vector2 org=seed.xz; float minY=incl.Min(u=>u.b.min.y); float th=TerrH(org.x,org.y);
                string pname="Village_"+seed.assetName+(policy.StartsWith("Full")?"_Full":"");
                csv.AppendLine($"{pname},{policy},{cA},{cB},{cC},{trisFull},{trisAfter},{cb.size.x:F1},{cb.size.z:F1},{cb.size.y:F1},{org.x:F2},{minY:F2},{org.y:F2},{seed.rot.eulerAngles.y:F1},{(float.IsNaN(th)?"NA":th.ToString("F2"))}");
                clusterInfo.Add((i,pname,policy,cA,cB,cC,trisFull,trisAfter));
            }
            sb.AppendLine($"\n[클러스터 프리뷰 CSV] → {PreviewCsv}");
            sb.AppendLine("prefab / policy / A B C / trisFull→trisAfter");
            foreach(var c in clusterInfo.OrderByDescending(c=>c.tf)) sb.AppendLine($"   {c.name,-42} {c.policy,-11} A{c.A} B{c.B} C{c.C} · {c.tf}→{c.ta}");

            // ── 창고 후보 top3(곡식·독·가마니 소품 최다) — 보류 ──
            var whRank = new List<(string name,int whCount,int cIdx,string props)>();
            for(int i=0;i<seeds.Count;i++){ var members=units.Where(u=>u.cluster==i && u.tier=='C').ToList();
                var wh=members.Where(u=>WarehouseTokens.Any(t=>u.assetName.ToLowerInvariant().Contains(t.ToLowerInvariant()))).ToList();
                if(wh.Count==0) continue; string props=string.Join("|", wh.GroupBy(u=>u.assetName).Select(g=>$"{g.Key}×{g.Count()}"));
                whRank.Add(("Village_"+seeds[i].assetName, wh.Count, i, props)); }
            sb.AppendLine($"\n[창고 후보 top3] (곡식·독·가마니 소품 최다 — ★이번 추출 보류, 선정은 지시):");
            foreach(var w in whRank.OrderByDescending(w=>w.whCount).Take(3)) sb.AppendLine($"   {w.name} · 창고류 {w.whCount}개 · {w.props}");

            // ── _Unassigned ──
            sb.AppendLine($"\n[_Unassigned] {unassigned.Count}개 · tris {unassigned.Sum(u=>u.tris)} (15m 내 건물 없음):");
            foreach(var grp in unassigned.GroupBy(u=>u.assetName).OrderByDescending(g=>g.Count())) sb.AppendLine($"   {grp.Count(),3} × {grp.Key} (tier {grp.First().tier})");

            // ── 별건: Wall01c/02c/03c 인스턴스 수·tris ──
            sb.AppendLine("\n[별건] Wall01c/02c/03c 사용 현황(씬):");
            foreach(var w in new[]{"Wall01c","Wall02c","Wall03c"}){ var wu=units.Where(u=>u.assetName==w).ToList(); sb.AppendLine($"   {w}: {wu.Count}개 · tris합 {wu.Sum(u=>u.tris)} · 단품 {(wu.Count>0?wu[0].tris:0)}tris"); }

            // ── 별건: 저폴리 벽 FBX 실측(SM_Wall*.fbx) — 교체 판단용, 교체 안 함 ──
            sb.AppendLine("\n[별건] 팩 벽 메시 FBX 실측 (SM_Wall*.fbx) — 파일 / tris / X길이 × Z두께 × Y높이:");
            foreach(var g in AssetDatabase.FindAssets("t:Model", new[]{WallMeshDir}).OrderBy(x=>x)){
                var p=AssetDatabase.GUIDToAssetPath(g); var go=AssetDatabase.LoadAssetAtPath<GameObject>(p); if(go==null) continue;
                int tris=0; bool hasB=false; Bounds bb=new Bounds();
                foreach(var mf in go.GetComponentsInChildren<MeshFilter>(true)){ if(mf.sharedMesh==null) continue; tris+=MeshTris(mf.sharedMesh); var lb=mf.sharedMesh.bounds; if(!hasB){bb=lb;hasB=true;} else bb.Encapsulate(lb); }
                sb.AppendLine($"   {Path.GetFileNameWithoutExtension(p),-12} {tris,6} tris · {bb.size.x:F2} × {bb.size.z:F2} × {bb.size.y:F2} m");
            }

            // ── 총계 ──
            sb.AppendLine($"\n[총계] 클러스터 {seeds.Count}개 · 1단계 합계 5,276,345 → Full {sumFull} → 소품정책 적용후 {sumAfter} (감소 {5276345-sumAfter}, {100f*(5276345-sumAfter)/5276345f:F1}%)");
            sb.AppendLine("※ 2a는 프리팹/씬 미생성. 확정 후 2b Extract 실행.");

            // 닫기 + 해시
            EditorSceneManager.CloseScene(demo, true);
            string hashAfter=FileHash(DemoScene);
            sb.AppendLine($"\n[무수정 확인] Demo.unity 해시 {(hashBefore==hashAfter?"동일 ✓":"⚠ 변경")} ({hashBefore.Substring(0,8)}/{hashAfter.Substring(0,8)})");

            EnsureFolder(DataDir);
            File.WriteAllText(Path.GetFullPath(PreviewCsv), csv.ToString(), new UTF8Encoding(true));
            File.WriteAllText(Path.GetFullPath(TxtPath), sb.ToString(), new UTF8Encoding(true));
            AssetDatabase.ImportAsset(PreviewCsv); AssetDatabase.ImportAsset(TxtPath);
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"2a 분류/조사 완료(읽기 전용).\n건물 {seeds.Count} · Ambiguous {amb.Count} · Unassigned {unassigned.Count}\nFull {sumFull}→정책후 {sumAfter} tris\n프리뷰 CSV/리포트 → Data/. Console 확인.","확인");
        }

        private static int MeshTris(Mesh m){ int t=0; for(int s=0;s<m.subMeshCount;s++) t+=(int)(m.GetIndexCount(s)/3); return t; }
        private static string FileHash(string assetPath){ string abs=Path.GetFullPath(assetPath); if(!File.Exists(abs)) return "NOFILE"; using(var md5=MD5.Create()) using(var fs=File.OpenRead(abs)){ return string.Concat(md5.ComputeHash(fs).Select(b=>b.ToString("x2"))); } }
        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
    }
}
