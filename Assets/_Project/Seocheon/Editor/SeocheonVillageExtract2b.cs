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
    /// [2b단계] 낙안읍성 클러스터 → 프리팹 추출.
    /// ★WIP 사본(_VillageSplit_WIP.unity)에서만 재부모화. 원본 Demo.unity 미개봉·미저장.
    /// 정책: 주막 2채 _Full(A+B+C), 나머지 26채 A+B. 창고 후보 top3은 CSV 플래그만.
    /// ★중첩 프리팹 링크 유지(unpack 금지) · Static/머티리얼/레이어 원본 보존.
    /// </summary>
    public static class SeocheonVillageExtract2b
    {
        private const string DemoScene = "Assets/Naganeupseong/Scene/Demo.unity";
        private const string WipScene  = "Assets/_Project/Seocheon/Scenes/_VillageSplit_WIP.unity";
        private const string OutDir    = "Assets/_Project/Seocheon/Prefabs/Village";
        private const string DataDir   = "Assets/_Project/Seocheon/Data";
        private const string PlaceCsv  = DataDir + "/village_placement.csv";
        private const string TxtPath   = DataDir + "/village_extract_2b_report.txt";

        private const string BuildDir  = "/Prefabs/Build/";
        private const string StructDir = "/Prefabs/Structure/";
        private const string PropDir   = "/Prefabs/Prop/";
        private const string MeshPropDir="/Resource/Meshes/Prop/"; // raw 모델 소품(예: SM_Seive_Support) → C
        private const float AssignRadius = 15f;

        private static readonly string[] WarehouseTokens = {
            "Rice","Grain","Corn","Bean","Meju","Straw_Bag","Straw_Jar","Sack","Jar","Jars","Chest","Bushel","Barley","Millet" };

        private class Unit {
            public GameObject go; public string assetName; public char tier; // A/B/C
            public int tris; public Bounds b; public Vector2 xz; public Quaternion rot; public int cluster=-1;
        }

        [MenuItem("Tools/Seocheon/Village/2b. Extract Prefabs (WIP copy)")]
        public static void Extract()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드에서는 실행 불가. Edit 모드로.","확인"); return; }
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScene)==null){ EditorUtility.DisplayDialog("Seocheon","Demo 씬 없음.","확인"); return; }
            string hashBefore = FileHash(DemoScene);
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] Village 2b Extract =====");

            // WIP 사본 (기존 있으면 교체). Demo 원본은 건드리지 않음.
            EnsureFolder("Assets/_Project/Seocheon/Scenes");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(WipScene)!=null) AssetDatabase.DeleteAsset(WipScene);
            if(!AssetDatabase.CopyAsset(DemoScene, WipScene)){ EditorUtility.DisplayDialog("Seocheon","WIP 사본 생성 실패.","확인"); return; }
            AssetDatabase.Refresh();
            var wip = EditorSceneManager.OpenScene(WipScene, OpenSceneMode.Single);
            sb.AppendLine($"[0] WIP 사본 열림: {WipScene} (Demo 미개봉)");

            Terrain terrain=null; foreach(var r in wip.GetRootGameObjects()){ var t=r.GetComponentInChildren<Terrain>(true); if(t!=null){ terrain=t; break; } }
            float TerrH(float x,float z){ if(terrain==null) return float.NaN; return terrain.transform.position.y+terrain.SampleHeight(new Vector3(x,0,z)); }

            // ── 유닛 수집 + 폴더 기반 분류(+_Ambiguous 해소: sky 제외 / raw prop → C) ──
            var units=new List<Unit>(); var excludedSky=new List<string>(); var stillAmbiguous=new List<string>();
            foreach(var r in wip.GetRootGameObjects()){
                var go=r.gameObject;
                if(go.GetComponent<Terrain>()||go.GetComponent<Light>()||go.GetComponent<Camera>()||go.GetComponent<ReflectionProbe>()) continue;
                if(!PrefabUtility.IsAnyPrefabInstanceRoot(go)){ stillAmbiguous.Add(go.name); continue; }
                var ap=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                char tier;
                if(ap.Contains(BuildDir)) tier='A';
                else if(ap.Contains(StructDir)) tier='B';
                else if(ap.Contains(PropDir)) tier='C';
                else if(go.name.ToLowerInvariant().Contains("sky")){ excludedSky.Add(go.name); continue; } // SkySphere 제외
                else if(ap.Contains(MeshPropDir)) tier='C';                                                // raw 모델 소품 → C
                else { stillAmbiguous.Add(go.name+" ("+ap+")"); continue; }
                units.Add(new Unit{ go=go, assetName=Path.GetFileNameWithoutExtension(ap), tier=tier });
            }
            foreach(var u in units){ int tris=0; bool hasB=false; Bounds bb=new Bounds();
                foreach(var mf in u.go.GetComponentsInChildren<MeshFilter>(true)){ var mr=mf.GetComponent<MeshRenderer>(); if(mr==null||mf.sharedMesh==null) continue; tris+=MeshTris(mf.sharedMesh); if(!hasB){bb=mr.bounds;hasB=true;} else bb.Encapsulate(mr.bounds); }
                foreach(var smr in u.go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){ if(smr.sharedMesh==null) continue; tris+=MeshTris(smr.sharedMesh); if(!hasB){bb=smr.bounds;hasB=true;} else bb.Encapsulate(smr.bounds); }
                u.tris=tris; u.b=hasB?bb:new Bounds(u.go.transform.position,Vector3.zero); u.xz=new Vector2(u.b.center.x,u.b.center.z); u.rot=u.go.transform.rotation; }

            // ── 시드 = A(28). 15m 재클러스터링 ──
            var seeds=units.Where(u=>u.tier=='A').ToList();
            for(int i=0;i<seeds.Count;i++) seeds[i].cluster=i;
            foreach(var u in units){ if(u.tier=='A') continue; int best=-1; float bd=AssignRadius;
                for(int i=0;i<seeds.Count;i++){ float d=Vector2.Distance(u.xz,seeds[i].xz); if(d<=bd){bd=d;best=i;} }
                u.cluster=best; }

            // ── 창고 후보 top3 (플래그용) ──
            var whCount=new int[seeds.Count];
            for(int i=0;i<seeds.Count;i++) whCount[i]=units.Count(u=>u.cluster==i && u.tier=='C' && WarehouseTokens.Any(t=>u.assetName.ToLowerInvariant().Contains(t.ToLowerInvariant())));
            var whTop=Enumerable.Range(0,seeds.Count).OrderByDescending(i=>whCount[i]).Take(3).ToList();

            // ── 추출 ──
            EnsureFolder(OutDir);
            var csv=new StringBuilder();
            csv.AppendLine("prefab,policy,warehouseFlag,whProps,A,B,C,trisFull,trisAfter,bboxW,bboxD,bboxH,posX,posY,posZ,origRotY,terrainH");
            var included=new HashSet<Unit>();
            var saved=new List<string>(); int sumAfter=0, failSave=0;
            for(int i=0;i<seeds.Count;i++){
                var seed=seeds[i]; var members=units.Where(u=>u.cluster==i).ToList(); if(!members.Contains(seed)) members.Add(seed);
                int cA=members.Count(u=>u.tier=='A'), cB=members.Count(u=>u.tier=='B'), cC=members.Count(u=>u.tier=='C');
                bool isTavern=seed.assetName.ToLowerInvariant().Contains("tavern");
                string policy=isTavern?"Full":"A+B";
                var incl = isTavern ? members : members.Where(u=>u.tier!='C').ToList();
                int trisFull=members.Sum(u=>u.tris); int trisAfter=incl.Sum(u=>u.tris); sumAfter+=trisAfter;
                Vector2 org=seed.xz; float minY=incl.Min(u=>u.b.min.y);
                Bounds cb=incl[0].b; foreach(var m in incl) cb.Encapsulate(m.b);
                string pname="Village_"+seed.assetName+(isTavern?"_Full":"");
                string whFlag = whTop.Contains(i)?("WH"+(whTop.IndexOf(i)+1)):"";
                string whProps = whFlag!="" ? string.Join("|", units.Where(u=>u.cluster==i&&u.tier=='C'&&WarehouseTokens.Any(t=>u.assetName.ToLowerInvariant().Contains(t.ToLowerInvariant()))).GroupBy(u=>u.assetName).Select(g=>$"{g.Key}×{g.Count()}")) : "";

                // 빈 루트 원점 = 주가옥 중심 XZ + 접지 minY, identity. worldPositionStays 재부모화.
                var root=new GameObject(pname);
                root.transform.position=new Vector3(org.x, minY, org.y); root.transform.rotation=Quaternion.identity;
                foreach(var u in incl){ u.go.transform.SetParent(root.transform, true); included.Add(u); }

                string path=$"{OutDir}/{pname}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok); if(!ok) failSave++;
                saved.Add(path);
                csv.AppendLine($"{path},{policy},{whFlag},{whProps},{cA},{cB},{cC},{trisFull},{trisAfter},{cb.size.x:F1},{cb.size.z:F1},{cb.size.y:F1},{org.x:F2},{minY:F2},{org.y:F2},{seed.rot.eulerAngles.y:F1},{(float.IsNaN(TerrH(org.x,org.y))?"NA":TerrH(org.x,org.y).ToString("F2"))}");
            }
            EditorSceneManager.MarkSceneDirty(wip); EditorSceneManager.SaveScene(wip, WipScene); // ★WIP만 저장

            // ── 제외 C 집계(되살림용) ──
            var excludedC = units.Where(u=>u.tier=='C' && !included.Contains(u)).ToList();

            // ── 검증: 프리팹별 Missing 메시/머티리얼·핑크 ──
            int totMissMesh=0, totMissMat=0, totPink=0; var badList=new List<string>();
            foreach(var path in saved){ var go=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(go==null){ badList.Add(path+" (로드 실패)"); continue; }
                int mm=0, mt=0, pk=0;
                foreach(var mf in go.GetComponentsInChildren<MeshFilter>(true)) if(mf.sharedMesh==null) mm++;
                foreach(var rr in go.GetComponentsInChildren<Renderer>(true)) foreach(var m in rr.sharedMaterials){ if(m==null) mt++; else if(m.shader==null||m.shader.name.Contains("InternalErrorShader")) pk++; }
                totMissMesh+=mm; totMissMat+=mt; totPink+=pk;
                if(mm+mt+pk>0) badList.Add($"{Path.GetFileName(path)}: missMesh {mm} missMat {mt} pink {pk}");
            }

            // ── 리포트 ──
            sb.AppendLine($"[분류] A {seeds.Count} · B {units.Count(u=>u.tier=='B')} · C {units.Count(u=>u.tier=='C')} · 제외(하늘) {excludedSky.Count} · 미해소Ambiguous {stillAmbiguous.Count}");
            if(stillAmbiguous.Count>0) foreach(var a in stillAmbiguous) sb.AppendLine("   ⚠ 미분류(추출 제외): "+a);
            sb.AppendLine($"\n[추출] 프리팹 {saved.Count}개 → {OutDir} · 저장실패 {failSave}");
            sb.AppendLine($"[정책] 주막 _Full {seeds.Count(s=>s.assetName.ToLowerInvariant().Contains("tavern"))}채 · 나머지 A+B");
            sb.AppendLine($"[창고 플래그] WH1~3 = " + string.Join(", ", whTop.Select((ci,r)=>$"WH{r+1} {seeds[ci].assetName}({whCount[ci]})")));
            sb.AppendLine($"\n[검증] Missing 메시 {totMissMesh} · Missing 머티리얼 {totMissMat} · 핑크 {totPink} {((totMissMesh+totMissMat+totPink)==0?"✓ 전부 0":"⚠")}");
            foreach(var b in badList) sb.AppendLine("   "+b);
            sb.AppendLine($"\n[제외 C 소품 집계] {excludedC.Count}개 · tris {excludedC.Sum(u=>u.tris)} (되살림용 — 종류별):");
            foreach(var grp in excludedC.GroupBy(u=>u.assetName).OrderByDescending(g=>g.Sum(x=>x.tris))) sb.AppendLine($"   {grp.Count(),3} × {grp.Key} · tris {grp.Sum(x=>x.tris)}");
            sb.AppendLine($"\n[총계] 프리팹 {saved.Count}개 합계 tris {sumAfter} (1단계 5,276,345 대비 {100f*sumAfter/5276345f:F1}%)");

            string hashAfter=FileHash(DemoScene);
            sb.AppendLine($"\n[무수정 확인] Demo.unity 해시 {(hashBefore==hashAfter?"동일 ✓":"⚠ 변경")} ({hashBefore.Substring(0,8)}/{hashAfter.Substring(0,8)})");

            EnsureFolder(DataDir);
            File.WriteAllText(Path.GetFullPath(PlaceCsv), csv.ToString(), new UTF8Encoding(true));
            File.WriteAllText(Path.GetFullPath(TxtPath), sb.ToString(), new UTF8Encoding(true));
            AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(PlaceCsv); AssetDatabase.ImportAsset(TxtPath);
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"2b 추출 완료.\n프리팹 {saved.Count}개 · 저장실패 {failSave}\nMissing메시 {totMissMesh}·머티 {totMissMat}·핑크 {totPink}\n제외 C {excludedC.Count}개\nDemo 해시 {(hashBefore==hashAfter?"동일 ✓":"⚠")}\nCSV/리포트 → Data/","확인");
        }

        private static int MeshTris(Mesh m){ int t=0; for(int s=0;s<m.subMeshCount;s++) t+=(int)(m.GetIndexCount(s)/3); return t; }
        private static string FileHash(string assetPath){ string abs=Path.GetFullPath(assetPath); if(!File.Exists(abs)) return "NOFILE"; using(var md5=MD5.Create()) using(var fs=File.OpenRead(abs)){ return string.Concat(md5.ComputeHash(fs).Select(b=>b.ToString("x2"))); } }
        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
    }
}
