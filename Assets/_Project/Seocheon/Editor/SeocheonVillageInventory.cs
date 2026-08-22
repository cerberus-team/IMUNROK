using System.Collections.Generic;
using System.Globalization;
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
    /// [1단계] 낙안읍성 Demo 씬 인벤토리 — ★읽기 전용★. 프리팹 생성/씬 저장 없음.
    /// Demo.unity 는 Additive 로 열고(활성 씬 보존) 저장 없이 닫음 → 원본 무수정.
    /// 유닛 수집(컨테이너 드릴다운·중첩 프리팹 보존) → 15m 클러스터링 → CSV/리포트.
    /// </summary>
    public static class SeocheonVillageInventory
    {
        private const string DemoScene = "Assets/Naganeupseong/Scene/Demo.unity";
        private const string DataDir   = "Assets/_Project/Seocheon/Data";
        private const string CsvPath   = DataDir + "/village_inventory.csv";
        private const string TxtPath   = DataDir + "/village_inventory_report.txt";

        // 시드(주 가옥) 판별 토큰 — 이름 또는 프리팹 에셋명에 포함되면 시드
        private static readonly string[] SeedTokens = {
            "House","L_Shaped","LShaped","Local_Personnel","LocalPersonnel","Tavern",
            "대장간","Blacksmith","Smithy","Forge","Ox_Mill","OxMill","Mill","Guest","Inn" };
        private const float AssignRadius = 15f;
        private const int   TrapTris     = 30000;

        private class Unit {
            public GameObject go; public bool isPrefab; public string assetPath; public string assetName;
            public int tris; public Bounds b; public Vector2 xz; public float groundY; public float terrH;
            public List<string> prefabTypes = new List<string>();
            public bool isSeed; public int cluster = -1;
        }

        [MenuItem("Tools/Seocheon/Village/1. Inventory (read-only)")]
        public static void Inventory()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드에서는 씬을 열 수 없습니다.\nPlay 를 끄고(Edit 모드) 다시 실행하세요.","확인"); return; }
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScene)==null){ EditorUtility.DisplayDialog("Seocheon","Demo 씬 없음:\n"+DemoScene,"확인"); return; }
            string hashBefore = FileHash(DemoScene);

            // 활성 씬 보존 + Demo Additive 로드
            var demo = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Additive);
            var sb = new StringBuilder();
            sb.AppendLine("===== [Seocheon] Village Inventory (읽기 전용) =====");
            sb.AppendLine($"Demo 씬: {DemoScene}  (Additive 로드, 저장 안 함)");

            // Demo 씬 내 Terrain
            Terrain terrain=null; foreach(var r in demo.GetRootGameObjects()){ var t=r.GetComponentInChildren<Terrain>(true); if(t!=null){ terrain=t; break; } }
            float TerrH(float x,float z){ if(terrain==null) return float.NaN; return terrain.transform.position.y+terrain.SampleHeight(new Vector3(x,0,z)); }

            // ── 유닛 수집 + 루트 3분류 ──
            var units = new List<Unit>();
            var cat3 = new List<string>();      // Terrain/Light/Camera/Sky 등 배치대상 아님
            var empties = new List<string>();   // 렌더러 없는 순수 빈 오브젝트
            int rootPrefab=0, rootPure=0, rootCat3=0, drilled=0;
            var roots = demo.GetRootGameObjects();
            foreach(var r in roots){
                if(IsCat3(r.gameObject)){ rootCat3++; }
                else if(PrefabUtility.IsAnyPrefabInstanceRoot(r.gameObject)){ rootPrefab++; }
                else rootPure++;
                Collect(r.transform, units, cat3, empties, ref drilled);
            }

            // ── 유닛 메트릭 ──
            foreach(var u in units){
                var mfs = u.go.GetComponentsInChildren<MeshFilter>(true);
                var smrs= u.go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                int tris=0; bool hasB=false; Bounds bb=new Bounds();
                foreach(var mf in mfs){ var mr=mf.GetComponent<MeshRenderer>(); if(mr==null||mf.sharedMesh==null) continue; // MeshCollider 전용 제외
                    tris += MeshTris(mf.sharedMesh); if(!hasB){ bb=mr.bounds; hasB=true; } else bb.Encapsulate(mr.bounds); }
                foreach(var smr in smrs){ if(smr.sharedMesh==null) continue; tris += MeshTris(smr.sharedMesh); if(!hasB){ bb=smr.bounds; hasB=true; } else bb.Encapsulate(smr.bounds); }
                u.tris=tris; u.b=hasB?bb:new Bounds(u.go.transform.position,Vector3.zero);
                u.xz=new Vector2(u.b.center.x, u.b.center.z); u.groundY=u.b.min.y; u.terrH=TerrH(u.xz.x,u.xz.y);
                // 중첩 프리팹 종류 목록
                var types=new HashSet<string>();
                if(u.isPrefab && !string.IsNullOrEmpty(u.assetName)) types.Add(u.assetName);
                foreach(var t in u.go.GetComponentsInChildren<Transform>(true)){ if(PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)){ var ap=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject); if(!string.IsNullOrEmpty(ap)) types.Add(Path.GetFileNameWithoutExtension(ap)); } }
                u.prefabTypes=types.ToList();
                // 시드 판별
                string keyName=(u.go.name+" "+u.assetName).ToLowerInvariant();
                u.isSeed = SeedTokens.Any(tok=>keyName.Contains(tok.ToLowerInvariant()));
            }

            // ── 클러스터링: 시드 = 클러스터 중심, 비시드는 15m 내 최근접 시드 ──
            var seeds = units.Where(u=>u.isSeed).ToList();
            for(int i=0;i<seeds.Count;i++) seeds[i].cluster=i;
            var unassigned = new List<Unit>();
            foreach(var u in units){ if(u.isSeed) continue;
                int best=-1; float bd=AssignRadius;
                for(int i=0;i<seeds.Count;i++){ float d=Vector2.Distance(u.xz, seeds[i].xz); if(d<=bd){ bd=d; best=i; } }
                if(best>=0) u.cluster=best; else { unassigned.Add(u); u.cluster=-1; }
            }

            // ── 개별 메시 오브젝트 poly 전수집계(함정 + 최다 tris + Chili 진단) ──
            var objRows = new List<(string name,int tris,string owner,string cl)>();
            void Tally(Transform tr, Mesh m){ if(m==null) return; var owner=OwningUnit(tr, units); string cl=owner!=null?ClusterName(owner,seeds):"(유닛밖)"; objRows.Add((tr.gameObject.name, MeshTris(m), owner!=null?owner.go.name:"-", cl)); }
            foreach(var mf in demo.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true))){ if(mf.GetComponent<MeshRenderer>()==null) continue; Tally(mf.transform, mf.sharedMesh); }
            foreach(var smr in demo.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SkinnedMeshRenderer>(true))) Tally(smr.transform, smr.sharedMesh);
            var traps = objRows.Where(o=>o.tris>TrapTris).OrderByDescending(o=>o.tris).Select(o=>$"{o.name},{o.tris},{o.owner},{o.cl}").ToList();
            var top15 = objRows.OrderByDescending(o=>o.tris).Take(15).ToList();
            var chili = objRows.Where(o=>o.name.ToLowerInvariant().Contains("chili")).OrderByDescending(o=>o.tris).ToList();

            // ── 리포트 ──
            sb.AppendLine($"\n[루트] 총 {roots.Length}개 · 프리팹인스턴스 {rootPrefab} · 순수GO {rootPure} · 배치대상아님 {rootCat3} · (컨테이너 드릴다운 {drilled}회)");
            sb.AppendLine($"[유닛] 수집 {units.Count} (프리팹 {units.Count(u=>u.isPrefab)} · 순수 {units.Count(u=>!u.isPrefab)}) · 시드(주가옥) {seeds.Count} · Cat3 {cat3.Count} · 빈오브젝트 {empties.Count}");

            // ① 프리팹 인스턴스 — 에셋별 개수
            sb.AppendLine("\n① 프리팹 인스턴스 (에셋 경로별 개수):");
            foreach(var grp in units.Where(u=>u.isPrefab).GroupBy(u=>u.assetPath).OrderByDescending(g=>g.Count()))
                sb.AppendLine($"   {grp.Count(),3} × {grp.Key}");
            // ② 순수 GameObject
            sb.AppendLine($"\n② 프리팹 링크 없는 순수 GameObject ({units.Count(u=>!u.isPrefab)}):");
            foreach(var u in units.Where(u=>!u.isPrefab)) sb.AppendLine($"   {u.go.name} (tris {u.tris}, XZ {u.xz.x:F0},{u.xz.y:F0})");
            // ③ Cat3
            sb.AppendLine($"\n③ 배치 대상 아님 (Terrain·Light·Camera·Sky 등, {cat3.Count}):");
            foreach(var c in cat3) sb.AppendLine("   "+c);

            // 함정 프롭
            sb.AppendLine($"\n★ 함정 프롭 (개별 오브젝트 tris>{TrapTris}) — {traps.Count}개:");
            sb.AppendLine("   오브젝트,tris,소속유닛,클러스터");
            foreach(var tp in traps) sb.AppendLine("   "+tp);
            sb.AppendLine($"\n[단일 오브젝트 tris Top 15] (함정 판정 근거):");
            foreach(var o in top15) sb.AppendLine($"   {o.tris,7} · {o.name} · 클러스터 {o.cl}");
            sb.AppendLine($"\n[Chili 개별 tris 실측] {chili.Count}개:");
            if(chili.Count==0) sb.AppendLine("   (Chili 이름 오브젝트 없음)");
            foreach(var o in chili) sb.AppendLine($"   {o.tris,7} · {o.name} · 클러스터 {o.cl}");

            // 클러스터 CSV
            var csv = new StringBuilder();
            csv.AppendLine("cluster,objects,tris,bboxW,bboxD,bboxH,centerX,centerZ,groundY,terrainH,prefabTypes");
            int clTotalTris=0;
            for(int i=0;i<seeds.Count;i++){
                var members = units.Where(u=>u.cluster==i).ToList();
                if(members.Count==0) continue;
                Bounds cb=members[0].b; foreach(var m in members) cb.Encapsulate(m.b);
                int tris=members.Sum(m=>m.tris); clTotalTris+=tris;
                var types=new HashSet<string>(); foreach(var m in members) foreach(var t in m.prefabTypes) types.Add(t);
                string cname=ClusterName(seeds[i],seeds);
                Vector2 cxz=new Vector2(cb.center.x,cb.center.z); float th=TerrH(cxz.x,cxz.y);
                csv.AppendLine($"{cname},{members.Count},{tris},{cb.size.x:F1},{cb.size.z:F1},{cb.size.y:F1},{cxz.x:F1},{cxz.y:F1},{cb.min.y:F2},{(float.IsNaN(th)?"NA":th.ToString("F2"))},{string.Join("|",types)}");
            }
            int unTris=unassigned.Sum(u=>u.tris);
            sb.AppendLine($"\n[클러스터 CSV] → {CsvPath}");
            sb.AppendLine(csv.ToString());
            sb.AppendLine($"[_Unassigned] {unassigned.Count}개 · 합계 tris {unTris}:");
            foreach(var u in unassigned) sb.AppendLine($"   {u.go.name} (tris {u.tris}, XZ {u.xz.x:F0},{u.xz.y:F0}, 최근접시드 {NearestSeedDist(u,seeds):F0}m)");

            // 총계
            sb.AppendLine($"\n[총계] 클러스터 {seeds.Count(i2=>units.Any(u=>u.cluster==seeds.IndexOf(i2)))}개 · 클러스터합계 tris {clTotalTris} · Unassigned 합계 tris {unTris} · 함정프롭 {traps.Count}개");

            // Demo 저장 없이 닫기 → 해시 확인 (파일 기록 전에 sb 완성)
            EditorSceneManager.CloseScene(demo, true);
            string hashAfter = FileHash(DemoScene);
            sb.AppendLine($"\n[무수정 확인] Demo.unity 해시 {(hashBefore==hashAfter?"동일 ✓":"⚠ 변경됨")}  (before {hashBefore.Substring(0,8)} / after {hashAfter.Substring(0,8)})");

            // 파일 기록(리포트/CSV — Demo 미수정)
            EnsureFolder(DataDir);
            File.WriteAllText(Path.GetFullPath(CsvPath), csv.ToString(), new UTF8Encoding(true));
            File.WriteAllText(Path.GetFullPath(TxtPath), sb.ToString(), new UTF8Encoding(true));
            AssetDatabase.ImportAsset(CsvPath); AssetDatabase.ImportAsset(TxtPath);

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon",
                $"인벤토리 완료(읽기 전용).\n시드 {seeds.Count} · 유닛 {units.Count} · 함정 {traps.Count} · Unassigned {unassigned.Count}\nCSV/리포트 → Data/. Console 확인.\nDemo 해시 {(hashBefore==hashAfter?"동일 ✓":"⚠변경")}","확인");
        }

        // ── 유닛 수집: 컨테이너는 드릴다운, 프리팹 인스턴스 루트는 통째 1유닛(중첩 보존) ──
        private static void Collect(Transform t, List<Unit> units, List<string> cat3, List<string> empties, ref int drilled){
            var go=t.gameObject;
            if(IsCat3(go)){ cat3.Add($"{go.name} [{Cat3Reason(go)}]"); return; }
            if(PrefabUtility.IsAnyPrefabInstanceRoot(go)){
                var ap=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                units.Add(new Unit{ go=go, isPrefab=true, assetPath=string.IsNullOrEmpty(ap)?"(unknown)":ap, assetName=string.IsNullOrEmpty(ap)?go.name:Path.GetFileNameWithoutExtension(ap) });
                return;
            }
            // 비프리팹
            if(HasPrefabDescendant(t)){ drilled++; foreach(Transform c in t) Collect(c, units, cat3, empties, ref drilled); }
            else if(HasRenderer(t)){ units.Add(new Unit{ go=go, isPrefab=false, assetPath="", assetName=go.name }); }
            else if(t.childCount>0){ drilled++; foreach(Transform c in t) Collect(c, units, cat3, empties, ref drilled); }
            else empties.Add(go.name);
        }

        private static bool HasPrefabDescendant(Transform t){ foreach(Transform c in t){ if(PrefabUtility.IsAnyPrefabInstanceRoot(c.gameObject)) return true; if(HasPrefabDescendant(c)) return true; } return false; }
        private static bool HasRenderer(Transform t){ foreach(var mf in t.GetComponentsInChildren<MeshFilter>(true)) if(mf.GetComponent<MeshRenderer>()!=null) return true; return t.GetComponentInChildren<SkinnedMeshRenderer>(true)!=null; }
        private static bool IsCat3(GameObject go){ return go.GetComponent<Terrain>()!=null||go.GetComponent<Light>()!=null||go.GetComponent<Camera>()!=null||go.GetComponent<ReflectionProbe>()!=null||NameHasSky(go.name); }
        private static bool NameHasSky(string n){ n=n.ToLowerInvariant(); return n.Contains("sky")||n.Contains("sun")||n.Contains("cloud")||n.Contains("moon")||n.Contains("directional light"); }
        private static string Cat3Reason(GameObject go){ if(go.GetComponent<Terrain>()!=null) return "Terrain"; if(go.GetComponent<Light>()!=null) return "Light"; if(go.GetComponent<Camera>()!=null) return "Camera"; if(go.GetComponent<ReflectionProbe>()!=null) return "ReflectionProbe"; return "Sky/기타"; }

        private static int MeshTris(Mesh m){ int t=0; for(int s=0;s<m.subMeshCount;s++) t+=(int)(m.GetIndexCount(s)/3); return t; }
        private static Unit OwningUnit(Transform t, List<Unit> units){ foreach(var u in units){ var p=t; while(p!=null){ if(p.gameObject==u.go) return u; p=p.parent; } } return null; }
        private static string ClusterName(Unit seed, List<Unit> seeds){ int idx=seeds.IndexOf(seed); if(idx<0){ // 비시드 유닛이면 소속 클러스터의 시드
                if(seed.cluster>=0&&seed.cluster<seeds.Count) return CName(seeds[seed.cluster],seed.cluster); return "(?)"; } return CName(seed,idx); }
        private static string CName(Unit seed,int idx){ string tok="Cluster"; string low=(seed.assetName+" "+seed.go.name).ToLowerInvariant();
            foreach(var t in SeedTokens) if(low.Contains(t.ToLowerInvariant())){ tok=t; break; } return $"Village_{tok}_{idx+1:00}"; }
        private static float NearestSeedDist(Unit u, List<Unit> seeds){ float best=1e9f; foreach(var s in seeds){ float d=Vector2.Distance(u.xz,s.xz); if(d<best) best=d; } return best; }

        private static string FileHash(string assetPath){ string abs=Path.GetFullPath(assetPath); if(!File.Exists(abs)) return "NOFILE"; using(var md5=MD5.Create()) using(var fs=File.OpenRead(abs)){ var h=md5.ComputeHash(fs); return string.Concat(h.Select(b=>b.ToString("x2"))); } }
        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
    }
}
