using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// 동헌 v1 임포트: 모델/텍스처 임포트 설정 + URP/Lit 13종 머티리얼 + 지반 평면 조사(삭제 안 함) + 검증 + 프리팹.
    /// ★씬에 배치하지 않음. 지반 평면 삭제하지 않음(판정만). 낙안 팩 원본 미변경.
    /// </summary>
    public static class SeocheonDonheonImport
    {
        private const string Fbx    = "Assets/_Project/Seocheon/Art/Models/Donheon_Quest3_v1.fbx";
        private const string Fbm    = "Assets/_Project/Seocheon/Art/Models/Donheon_Quest3_v1.fbm";
        private const string TexDir = "Assets/_Project/Seocheon/Art/Textures/Donheon";
        private const string MatDir = "Assets/_Project/Seocheon/Art/Materials/Donheon";
        private const string PrefabDir = "Assets/_Project/Seocheon/Prefabs";
        private const string PrefabPath= "Assets/_Project/Seocheon/Prefabs/Donheon.prefab";

        private static readonly string[] NormalMaps = {
            "MI_R_Roof_Normal","MI_R_BrickConcrete_Normal","MI_R_Buyeon_2_Normal",
            "MI_KoreanWood_1_Normal","MI_KoreanPaper_1_Normal","T_Wall01b_N" };

        // {머티리얼명, base텍스처, normal텍스처, 특수}   특수: "" | "black" | "metal" | "cutout"
        private static readonly string[][] Mats = {
            new[]{"Donheon_Body","Donheon_Atlas_Albedo_final","",""},
            new[]{"M_Blackout","","","black"},
            new[]{"M_Wood_Tile","T_Wood_Tile_512","",""},
            new[]{"M_Stone_Rubble","T_Wall01b_BC_light","T_Wall01b_N",""},
            new[]{"M_Stone_Granite","T_Stone_Granite_512","",""},
            new[]{"MI_R_Roof1","MI_R_Roof_BaseColor","MI_R_Roof_Normal",""},
            new[]{"MI_R_BrickConcrete1","MI_R_BrickConcrete_BaseColor","MI_R_BrickConcrete_Normal",""},
            new[]{"MI_R_Buyeon_2.001","MI_R_Buyeon_2_BaseColor","MI_R_Buyeon_2_Normal",""},
            new[]{"MI_KoreanWood_1.001","MI_KoreanWood_1_BaseColor","MI_KoreanWood_1_Normal",""},
            new[]{"MI_KoreanPaper_1.004","MI_KoreanPaper_1_BaseColor","MI_KoreanPaper_1_Normal",""},
            new[]{"MI_Metal","","","metal"},
            new[]{"MI_R_Sign_PHD","T_Sign_SaMuDang_1024x306","",""},
            new[]{"Changho_Cutout","changho_cutout","","cutout"},
        };

        [MenuItem("Tools/Seocheon/Donheon/Import v1 (settings+mats+probe)")]
        public static void Run()
        {
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] Donheon v1 Import =====");
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx)==null){ EditorUtility.DisplayDialog("Seocheon","FBX 없음:\n"+Fbx,"확인"); return; }

            // [2] 텍스처 임포트 설정: 노멀맵 6장 + Max Size 1024 + RW off
            long memBefore=0, memAfter=0; int nNormal=0;
            var texGuids=AssetDatabase.FindAssets("t:Texture2D", new[]{TexDir});
            foreach(var gid in texGuids){ string p=AssetDatabase.GUIDToAssetPath(gid); var ti=(TextureImporter)AssetImporter.GetAtPath(p); if(ti==null) continue;
                string nm=Path.GetFileNameWithoutExtension(p); bool isN=System.Array.IndexOf(NormalMaps,nm)>=0;
                ti.maxTextureSize=1024; ti.isReadable=false;
                if(isN){ ti.textureType=TextureImporterType.NormalMap; nNormal++; }
                else { ti.textureType= nm=="changho_cutout"?TextureImporterType.Default:TextureImporterType.Default; ti.sRGBTexture=true; if(nm=="changho_cutout") ti.alphaIsTransparency=true; }
                ti.SaveAndReimport(); }
            sb.AppendLine($"[2] 텍스처 {texGuids.Length}장 · 노멀맵 {nNormal}장(Normal Map) · 전부 Max Size 1024 · RW off");

            // [2] 모델 임포트 설정
            var mi=(ModelImporter)AssetImporter.GetAtPath(Fbx);
            mi.globalScale=1f; mi.useFileScale=true; mi.bakeAxisConversion=false; mi.isReadable=false;
            mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            mi.SaveAndReimport();
            sb.AppendLine("[2] 모델: Scale 1 · Convert Units(useFileScale) ON · Bake Axis Conversion OFF · Read/Write OFF");

            // [2] .fbm 삭제
            if(AssetDatabase.IsValidFolder(Fbm)){ AssetDatabase.DeleteAsset(Fbm); sb.AppendLine("[2] .fbm 폴더 삭제"); }
            else sb.AppendLine("[2] .fbm 폴더 없음(이미 없음/외부텍스처 사용)");

            // FBX 슬롯 이름 수집
            var fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var slotNames=new HashSet<string>(); int meshObjs=0;
            foreach(var mr in fbxGo.GetComponentsInChildren<MeshRenderer>(true)){ foreach(var sm in mr.sharedMaterials) if(sm!=null) slotNames.Add(sm.name); }
            foreach(var mf in fbxGo.GetComponentsInChildren<MeshFilter>(true)) if(mf.sharedMesh!=null) meshObjs++;

            // [3] URP/Lit 13종 생성
            EnsureFolder(MatDir);
            var lit=Shader.Find("Universal Render Pipeline/Lit");
            var made=new Dictionary<string,Material>();
            foreach(var e in Mats){ string name=e[0], baseT=e[1], normT=e[2], sp=e[3];
                string mp=$"{MatDir}/{name}.mat"; var m=new Material(lit);
                Texture2D bt = baseT!=""?AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{baseT}.png"):null;
                Texture2D nt = normT!=""?AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{normT}.png"):null;
                if(bt!=null) m.SetTexture("_BaseMap",bt);
                if(nt!=null){ m.SetTexture("_BumpMap",nt); m.EnableKeyword("_NORMALMAP"); }
                if(sp=="black"){ m.SetColor("_BaseColor", Color.black); }
                if(sp=="metal"){ m.SetColor("_BaseColor", new Color(0.22f,0.22f,0.24f)); if(m.HasProperty("_Metallic")) m.SetFloat("_Metallic",0.85f); if(m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",0.55f); }
                if(sp=="cutout"){ m.SetFloat("_Surface",0f); m.SetFloat("_AlphaClip",1f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff",0.5f); }
                AssetDatabase.CreateAsset(m, mp); made[name]=m; }
            AssetDatabase.SaveAssets();
            sb.AppendLine($"[3] URP/Lit 머티리얼 {made.Count}종 생성 → {MatDir}");

            // [3] 리맵: FBX 슬롯명 → 내 머티리얼(정확/접두 매칭). 미매칭 = 핑크 후보
            var unmatched=new List<string>();
            foreach(var s in slotNames){ Material target=null;
                if(made.ContainsKey(s)) target=made[s];
                else foreach(var kv in made){ string k=kv.Key; if(s==k||s.StartsWith(k)||k.StartsWith(s)||Strip(s)==Strip(k)){ target=kv.Value; break; } }
                if(target!=null) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), s), target);
                else unmatched.Add(s); }
            mi.SaveAndReimport();
            sb.AppendLine($"[3] 리맵 {slotNames.Count-unmatched.Count}/{slotNames.Count} · ★핑크(미매칭): {(unmatched.Count==0?"없음 ✓":string.Join(", ",unmatched))}");

            // ── [4] 지반 평면 조사 (Donheon_Body_Mesh) ──
            var prefabGo=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            Transform body=null; foreach(var mf in prefabGo.GetComponentsInChildren<MeshFilter>(true)) if(mf.name.Contains("Body")){ body=mf.transform; break; }
            sb.AppendLine("\n── [4] 지반 평면 조사 (삭제 안 함, 판정만) ──");
            if(body==null){ sb.AppendLine("  Donheon_Body_Mesh 를 못 찾음 — 오브젝트명 확인 필요."); }
            else {
                var bmf=body.GetComponent<MeshFilter>(); var bmr=body.GetComponent<MeshRenderer>(); var mesh=bmf.sharedMesh;
                var vs=mesh.vertices; var mats=bmr.sharedMaterials;
                Bounds fb=mesh.bounds; float minY=fb.min.y;
                sb.AppendLine($"  Body_Mesh bbox: X {fb.size.x:F1} × Z {fb.size.z:F1} × Y {fb.size.y:F1}m, minY {minY:F2}, submesh {mesh.subMeshCount}");
                // 지반 후보: 수평(+Y) & minY 근처 삼각형
                float gxMin=1e9f,gxMax=-1e9f,gzMin=1e9f,gzMax=-1e9f; int gTris=0; var gBySub=new int[mesh.subMeshCount];
                // 본체(평면 위) bbox
                float bxMin=1e9f,bxMax=-1e9f,bzMin=1e9f,bzMax=-1e9f,byMax=-1e9f;
                for(int s=0;s<mesh.subMeshCount;s++){ var tri=mesh.GetTriangles(s);
                    float sxMin=1e9f,sxMax=-1e9f,szMin=1e9f,szMax=-1e9f; int sTris=tri.Length/3;
                    for(int t=0;t<tri.Length;t+=3){ Vector3 a=vs[tri[t]],b=vs[tri[t+1]],c=vs[tri[t+2]];
                        Vector3 n=Vector3.Cross(b-a,c-a).normalized; float cy=(a.y+b.y+c.y)/3f;
                        float xmn=Mathf.Min(a.x,Mathf.Min(b.x,c.x)),xmx=Mathf.Max(a.x,Mathf.Max(b.x,c.x));
                        float zmn=Mathf.Min(a.z,Mathf.Min(b.z,c.z)),zmx=Mathf.Max(a.z,Mathf.Max(b.z,c.z));
                        sxMin=Mathf.Min(sxMin,xmn); sxMax=Mathf.Max(sxMax,xmx); szMin=Mathf.Min(szMin,zmn); szMax=Mathf.Max(szMax,zmx);
                        bool ground = (Mathf.Abs(n.y)>0.9f) && (cy<=minY+0.15f);
                        if(ground){ gTris++; gBySub[s]++; gxMin=Mathf.Min(gxMin,xmn); gxMax=Mathf.Max(gxMax,xmx); gzMin=Mathf.Min(gzMin,zmn); gzMax=Mathf.Max(gzMax,zmx); }
                        else { // 평면 아닌 본체
                            bxMin=Mathf.Min(bxMin,xmn); bxMax=Mathf.Max(bxMax,xmx); bzMin=Mathf.Min(bzMin,zmn); bzMax=Mathf.Max(bzMax,zmx); byMax=Mathf.Max(byMax,Mathf.Max(a.y,Mathf.Max(b.y,c.y))); } }
                    string matN=(s<mats.Length&&mats[s]!=null)?mats[s].name:"(null)";
                    sb.AppendLine($"    submesh#{s} mat[{matN}] tris {sTris}, XZ범위 {sxMax-sxMin:F1}×{szMax-szMin:F1}m, 그중 지반 {gBySub[s]}");
                }
                sb.AppendLine($"  ▶ 지반 평면(수평·최저Y): tris {gTris}, XZ {gxMin:F1}~{gxMax:F1}(폭 {gxMax-gxMin:F1}) × {gzMin:F1}~{gzMax:F1}(깊이 {gzMax-gzMin:F1}) m");
                sb.AppendLine($"  ▶ 본체(평면 제외): XZ {bxMax-bxMin:F1}×{bzMax-bzMin:F1}m, 높이 {byMax-minY:F1}m");
                // 구분 기준 판정
                int gSubs=0, mixedSubs=0; for(int s=0;s<mesh.subMeshCount;s++){ if(gBySub[s]>0){ gSubs++; int tot=mesh.GetTriangles(s).Length/3; if(gBySub[s]<tot) mixedSubs++; } }
                sb.AppendLine($"  ▶ 구분 기준: 지반은 '수평면 & 최저Y {minY:F2}m'로 분리 가능. 지반 포함 submesh {gSubs}개(그중 본체와 혼재 {mixedSubs}개).");
                if(mixedSubs==0) sb.AppendLine("  ▶ 판정: 지반이 별도 submesh(머티리얼)로 분리됨 → 그 submesh만 지우면 본체 안 뚫림(단, 기단/축대가 평면 위에 별도 존재 시). 본체 높이 확인 필요.");
                else sb.AppendLine("  ▶ 판정: 지반이 본체와 같은 submesh에 혼재 → submesh 통삭제 불가, 면 단위 삭제 필요. 통삭제 시 그 머티리얼 본체 파트도 사라짐.");
                sb.AppendLine("  ▶ 뚫림 여부: 본체 footprint 위에 기단/마루 지오메트리(평면보다 높은 면)가 있으면 평면 삭제해도 건물 아래 안 뚫림. 위 '본체 높이'가 0보다 크면 별도 지오메트리 존재.");
                sb.AppendLine("  ※ 지시대로 삭제하지 않음. 위 수치로 결정 요망.");
            }

            // ── [5] 검증 ──
            int totalTris=0; int matCount; foreach(var mf in prefabGo.GetComponentsInChildren<MeshFilter>(true)) if(mf.sharedMesh!=null){ for(int s=0;s<mf.sharedMesh.subMeshCount;s++) totalTris+=(int)(mf.sharedMesh.GetIndexCount(s)/3); }
            var distinctMats=new HashSet<string>(); foreach(var mr in prefabGo.GetComponentsInChildren<MeshRenderer>(true)) foreach(var m in mr.sharedMaterials) if(m!=null) distinctMats.Add(m.name); matCount=distinctMats.Count;
            // 전체 bbox(월드=로컬, 인스턴스로 측정)
            var inst=(GameObject)PrefabUtility.InstantiatePrefab(prefabGo); Bounds wb=new Bounds(inst.transform.position,Vector3.zero); bool has=false;
            foreach(var r in inst.GetComponentsInChildren<Renderer>(true)){ if(!has){ wb=r.bounds; has=true; } else wb.Encapsulate(r.bounds); }
            // 텍스처 메모리(1024 적용 후)
            long texMem=0; foreach(var gid in texGuids){ var tex=AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(gid)); if(tex!=null) texMem+=Profiler.GetRuntimeMemorySizeLong(tex); }
            sb.AppendLine("\n── [5] 검증 ──");
            sb.AppendLine($"· 총 tris {totalTris} (README 104,703 {(totalTris==104703?"✓":"⚠ 불일치")})");
            sb.AppendLine($"· 메시 오브젝트 {meshObjs} (README 19 {(meshObjs==19?"✓":"⚠")}) · 머티리얼 {matCount} (README 13 {(matCount==13?"✓":"⚠")})");
            sb.AppendLine($"· 전체 bbox {wb.size.x:F1}×{wb.size.z:F1}×{wb.size.y:F1}m (지반 포함이라 44×44 예상; 본체는 [4]의 '본체 제외' 참고)");
            sb.AppendLine($"· 텍스처 런타임 메모리 합계 {texMem/1048576f:F1} MB (Max Size 1024 후)");

            // [5] 프리팹 저장(씬 배치 안 함)
            EnsureFolder(PrefabDir);
            PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
            Object.DestroyImmediate(inst); // 씬에서 제거 → 배치 안 함
            sb.AppendLine($"· 프리팹 저장 → {PrefabPath} (씬 미배치)");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon","동헌 임포트+머티리얼+지반조사+프리팹 완료.\n지반 평면은 삭제 안 함(판정만). Console 확인.","확인");
        }

        private static string Strip(string s){ int d=s.LastIndexOf('.'); if(d>0 && s.Length-d<=4){ bool num=true; for(int i=d+1;i<s.Length;i++) if(!char.IsDigit(s[i])) num=false; if(num) return s.Substring(0,d); } return s; }
        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
    }
}
