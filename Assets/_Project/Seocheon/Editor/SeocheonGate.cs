using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// [13] 관아 진입부 세트(외삼문+담장5) Unity 임포트·배치·측정·렌더.
    /// 13a 임포트+머티리얼 / 13b 배치+지형·메트릭 / 13c 렌더 확인.
    /// ★담장 코핑은 양면 셸 → 담장 머티리얼 Render Face=Both. 흰벽은 _002_light_mulV16.
    /// </summary>
    public static class SeocheonGate
    {
        private const string SrcFbx = @"C:\Users\User\관아\Gwana_Gate_Set_v1.fbx";
        private const string SrcTex = @"C:\Users\User\관아\Gwana_Gate_Set_v1_Textures";
        private const string DstDir = "Assets/Models";
        private const string DstFbx = "Assets/Models/Gwana_Gate_Set_v1.fbx";
        private const string TexDir = "Assets/Models/Gwana_Gate_Set_Tex";
        private const string MatDir = "Assets/Models/Gwana_Gate_Mats";
        private const string Scene  = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string DonPrefab = "Assets/_Project/Seocheon/Prefabs/Donheon.prefab";
        private const string RenderDir = @"C:\Users\User\관아\_renders\gate_set";
        private const int ExpTris = 100908;

        // 게이트 원점 (동헌 616 − 블렌더프레임 26.345)
        private const float GateX = 462f, GateZ = 589.66f;
        // footprint (지형 편차 검사): X 462±18, Z 581.7~623.8
        private const float FpHalfX = 18f, FpZ0 = 581.7f, FpZ1 = 623.8f;

        // matName -> (baseFile, normalFile)  "" = 없음
        private static readonly (string mat, string bas, string nrm)[] Map = {
            ("MI_KoreanWall_1.001",  "base_color_texture_011.png",              "normalmap_texture_012.png"),
            ("MI_KoreanStone_1.002", "base_color_texture.png",                  "normalmap_texture.png"),
            ("MI_KoreanWood_1.001",  "base_color_texture_001.png",              "normalmap_texture_001.png"),
            ("MI_W_Orange_2",        "base_color_texture_002_light_mulV16.png", "normalmap_texture_002.png"),
            ("MI_KoreanBrick_2.001", "base_color_texture_003.png",              "normalmap_texture_003.png"),
            ("MI_KoreanBrick_4.001", "base_color_texture_004.png",              "normalmap_texture_004.png"),
            ("MI_Metal",             "",                                        ""),
            ("MI_KoreanPaper_1.004", "base_color_texture_005.png",              "normalmap_texture_005.png"),
            ("MI_KoreanFloor_2",     "base_color_texture1.png",                 "normalmap_texture1.png"),
            ("MI_R_Roof1",           "base_color_texture_006.png",              "normalmap_texture_006.png"),
            ("MI_R_BrickConcrete1",  "base_color_texture_007.png",              "normalmap_texture_007.png"),
            ("MI_R_Decors_4.001",    "base_color_texture_008.png",              "normalmap_texture_008.png"),
            ("MI_R_Buyeon_2.002",    "base_color_texture_008.png",              "normalmap_texture_009.png"),
            ("MI_R_Sign_OSM.001",    "base_color_texture_009.png",              "normalmap_texture_010.png"),
            ("MI_R_Sign_SPR.001",    "base_color_texture_010.png",              "normalmap_texture_011.png"),
            ("MI_R_Roof",            "base_color_texture_006.png",              ""),
        };
        // 담장이 쓰는 머티리얼 → Render Face = Both (코핑 양면)
        private static readonly HashSet<string> WallMats = new HashSet<string>{
            "MI_R_Roof", "MI_KoreanStone_1.002", "MI_KoreanBrick_2.001", "MI_KoreanBrick_4.001" };

        // ─────────────────────────── 13a ───────────────────────────
        [MenuItem("Tools/Seocheon/Gate/13a. Import FBX + Textures + Materials")]
        public static void Import()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 13a Gate Import + Materials =====");
            if(!File.Exists(SrcFbx)){ EditorUtility.DisplayDialog("Seocheon","소스 FBX 없음:\n"+SrcFbx,"확인"); return; }

            EnsureFolder(DstDir); EnsureFolder(TexDir); EnsureFolder(MatDir);

            // [1] 텍스처·FBX 복사
            int copied=0;
            foreach(var f in Directory.GetFiles(SrcTex,"*.png")){
                File.Copy(f, Path.Combine(Path.GetFullPath(TexDir), Path.GetFileName(f)), true); copied++; }
            File.Copy(SrcFbx, Path.GetFullPath(DstFbx), true);
            AssetDatabase.Refresh();
            sb.AppendLine($"[1] 복사: FBX 1 + 텍스처 {copied}장 → {TexDir}");

            // .fbm 삭제(있으면)
            string fbm = Path.Combine(Path.GetFullPath(DstDir), "Gwana_Gate_Set_v1.fbm");
            if(Directory.Exists(fbm)){ Directory.Delete(fbm,true); File.Delete(fbm+".meta"); AssetDatabase.Refresh();
                sb.AppendLine("[1] .fbm 폴더 삭제됨(중복 텍스처 방지)"); }
            else sb.AppendLine("[1] .fbm 폴더 없음(STRIP 익스포트라 정상)");

            // [2] 노멀맵 Texture Type 교정
            var normalFiles = new HashSet<string>(Map.Where(m=>m.nrm!="").Select(m=>m.nrm));
            int nN=0,nB=0;
            foreach(var file in Map.SelectMany(m=>new[]{m.bas,m.nrm}).Where(x=>x!="").Distinct()){
                var ti=(TextureImporter)AssetImporter.GetAtPath(TexDir+"/"+file); if(ti==null) continue;
                bool isN=normalFiles.Contains(file);
                if(isN){ if(ti.textureType!=TextureImporterType.NormalMap){ ti.textureType=TextureImporterType.NormalMap; ti.SaveAndReimport(); } nN++; }
                else { bool ch=false; if(ti.textureType!=TextureImporterType.Default){ ti.textureType=TextureImporterType.Default; ch=true; } if(!ti.sRGBTexture){ ti.sRGBTexture=true; ch=true; } if(ch) ti.SaveAndReimport(); nB++; }
            }
            sb.AppendLine($"[2] 텍스처 타입: Normal Map {nN}장 · Base(sRGB) {nB}장");

            // [1] 모델 임포트 설정
            var mi=(ModelImporter)AssetImporter.GetAtPath(DstFbx);
            mi.globalScale=1f; mi.useFileScale=true;               // Scale 1 · Convert Units ON
            mi.bakeAxisConversion=false;                            // Bake Axis Conversion OFF
            mi.isReadable=true;                                     // 정면판별 위해 임시 ON
            mi.addCollider=false; mi.importCameras=false; mi.importLights=false;
            mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            mi.SaveAndReimport();

            // [2] URP/Lit 머티리얼 생성 + 리맵
            var lit=Shader.Find("Universal Render Pipeline/Lit");
            var fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(DstFbx);
            var slots=new HashSet<string>();
            foreach(var r in fbxGo.GetComponentsInChildren<MeshRenderer>(true)) foreach(var sm in r.sharedMaterials) if(sm!=null) slots.Add(sm.name);

            foreach(var m in Map){
                var mp=$"{MatDir}/{m.mat}.mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);
                if(mat==null){ mat=new Material(lit); AssetDatabase.CreateAsset(mat,mp); } else mat.shader=lit;
                if(m.bas!=""){ var bt=AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir+"/"+m.bas); if(bt!=null) mat.SetTexture("_BaseMap",bt); mat.SetColor("_BaseColor",Color.white); }
                if(m.nrm!=""){ var nt=AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir+"/"+m.nrm); if(nt!=null){ mat.SetTexture("_BumpMap",nt); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale",1f);} }
                if(m.mat=="MI_Metal"){ mat.SetColor("_BaseColor",new Color(0.60f,0.60f,0.63f)); mat.SetFloat("_Metallic",0.9f); mat.SetFloat("_Smoothness",0.5f); }
                if(m.mat.ToLowerInvariant().Contains("paper")) mat.SetFloat("_Smoothness",0.1f);
                // Render Face
                if(WallMats.Contains(m.mat)){ mat.SetFloat("_Cull",0f); mat.doubleSidedGI=true; }   // Both
                else mat.SetFloat("_Cull",2f);                                                        // Back
                EditorUtility.SetDirty(mat);
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.mat), mat);
            }
            var unmatched = slots.Where(s=>!Map.Any(m=>m.mat==s)).ToList();
            AssetDatabase.SaveAssets();

            // 정면(현판 Sign_OSM) 로컬 Z 판별
            fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(DstFbx);
            float signZsum=0f, signA=0f, allZsum=0f, allA=0f;
            foreach(var r in fbxGo.GetComponentsInChildren<MeshRenderer>(true)){
                var mf=r.GetComponent<MeshFilter>(); var msh=mf!=null?mf.sharedMesh:null; if(msh==null) continue;
                var sm=r.sharedMaterials; Vector3[] vs; try{ vs=msh.vertices; }catch{ continue; } var tr=r.transform;
                for(int s=0;s<sm.Length && s<msh.subMeshCount;s++){ bool isSign=sm[s]!=null && sm[s].name.Contains("Sign_OSM");
                    var tri=msh.GetTriangles(s);
                    for(int t=0;t<tri.Length;t+=3){ Vector3 a=tr.TransformPoint(vs[tri[t]]),b=tr.TransformPoint(vs[tri[t+1]]),c=tr.TransformPoint(vs[tri[t+2]]);
                        float ar=0.5f*Vector3.Cross(b-a,c-a).magnitude; float cz=(a.z+b.z+c.z)/3f;
                        allZsum+=ar*cz; allA+=ar; if(isSign){ signZsum+=ar*cz; signA+=ar; } } } }
            float signZ = signA>0.001f ? signZsum/signA : 0f;
            float allZ  = allA>0.001f ? allZsum/allA : 0f;
            bool frontNegZ = signZ < allZ;   // 현판이 세트 중심보다 −Z면 정면 남향 OK

            // R/W 최종 OFF
            mi.isReadable=false; mi.SaveAndReimport(); AssetDatabase.Refresh();

            // [검증]
            fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(DstFbx);
            var mfs=fbxGo.GetComponentsInChildren<MeshFilter>(true);
            int tris=0; foreach(var mf in mfs) if(mf.sharedMesh!=null) for(int s=0;s<mf.sharedMesh.subMeshCount;s++) tris+=(int)(mf.sharedMesh.GetIndexCount(s)/3);
            var names=mfs.Select(x=>x.name).OrderBy(x=>x).ToArray();
            var pinks=new List<string>();
            foreach(var r in fbxGo.GetComponentsInChildren<MeshRenderer>(true)) foreach(var sm in r.sharedMaterials)
                if(sm==null||sm.shader==null||sm.shader.name.Contains("InternalErrorShader")){ string pn=sm!=null?sm.name:"(null)"; if(!pinks.Contains(pn)) pinks.Add(pn); }
            var probe=(GameObject)PrefabUtility.InstantiatePrefab(fbxGo); probe.transform.localScale=Vector3.one;
            bool scaleOk=true; foreach(var t in probe.GetComponentsInChildren<Transform>()) if((t.localScale-Vector3.one).sqrMagnitude>1e-4f) scaleOk=false;
            Object.DestroyImmediate(probe);

            sb.AppendLine($"\n[검증] 오브젝트 {mfs.Length} [{string.Join(", ",names)}] {(mfs.Length==6?"✓":"⚠")}");
            sb.AppendLine($"[검증] tris {tris} (기대 {ExpTris} {(tris==ExpTris?"✓":"⚠")})");
            sb.AppendLine($"[검증] 스케일 1.0 {(scaleOk?"✓":"⚠ 불일치")}");
            sb.AppendLine($"[검증] 핑크(미할당) 머티리얼 {(pinks.Count==0?"없음 ✓":string.Join(", ",pinks))}");
            if(unmatched.Count>0) sb.AppendLine($"[검증] ⚠ 매핑표에 없는 FBX 슬롯: {string.Join(", ",unmatched)}");
            else sb.AppendLine("[검증] 매핑표 = FBX 슬롯 일치 ✓");
            sb.AppendLine($"[검증] 담장 Both 컬링 머티리얼: {string.Join(", ",WallMats)}");
            sb.AppendLine($"[검증] 정면(현판 Sign_OSM) 로컬Z {signZ:+0.00;-0.00} vs 세트중심 {allZ:+0.00;-0.00} → 정면 {(frontNegZ?"−Z(남) ✓":"⚠ +Z(반대) — 배치 후 확인")}");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"13a 완료. 오브젝트 {mfs.Length}·tris {tris}·핑크 {pinks.Count}\n정면 {(frontNegZ?"−Z 남향":"⚠확인")}. 다음 13b 배치. Console 확인.","확인");
        }

        // ─────────────────────────── 13b ───────────────────────────
        [MenuItem("Tools/Seocheon/Gate/13b. Place in scene + Terrain + Metrics")]
        public static void Place()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 13b Gate Place + Terrain + Metrics =====");
            var fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(DstFbx);
            if(fbxGo==null){ EditorUtility.DisplayDialog("Seocheon","FBX 없음 — 먼저 13a 실행.","확인"); return; }

            var logs=new List<string>();
            Application.LogCallback cb=(c,st,ty)=>{ if(ty==LogType.Error||ty==LogType.Warning||ty==LogType.Exception) logs.Add($"[{ty}] {c}"); };
            Application.logMessageReceived += cb;

            var scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var terrs=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terrs.Length>0?terrs[0]:null;
            if(terrain==null){ Application.logMessageReceived-=cb; EditorUtility.DisplayDialog("Seocheon","Terrain 없음.","확인"); return; }
            var td=terrain.terrainData; int Rr=td.heightmapResolution; Vector3 S=td.size;
            float tY=terrain.transform.position.y,tX0=terrain.transform.position.x,tZ0=terrain.transform.position.z;
            var Hn=td.GetHeights(0,0,Rr,Rr);
            float H(float x,float z){ float u=(x-tX0)/S.x,v=(z-tZ0)/S.z; float fx=Mathf.Clamp(u*(Rr-1),0,Rr-1),fz=Mathf.Clamp(v*(Rr-1),0,Rr-1);
                int x0=(int)fx,z0=(int)fz,x1=Mathf.Min(x0+1,Rr-1),z1=Mathf.Min(z0+1,Rr-1); float tx=fx-x0,tz=fz-z0;
                return tY+Mathf.Lerp(Mathf.Lerp(Hn[z0,x0],Hn[z0,x1],tx),Mathf.Lerp(Hn[z1,x0],Hn[z1,x1],tx),tz)*S.y; }

            // [3] 배치
            foreach(var r in scene.GetRootGameObjects().ToList()) if(r.name=="_GwanaGate") Object.DestroyImmediate(r);
            float gy=H(GateX,GateZ);
            var holder=new GameObject("_GwanaGate");
            var inst=(GameObject)PrefabUtility.InstantiatePrefab(fbxGo); inst.transform.SetParent(holder.transform);
            inst.transform.position=new Vector3(GateX, gy, GateZ);
            inst.transform.rotation=Quaternion.identity;
            SetStaticRec(holder);

            var rends=inst.GetComponentsInChildren<Renderer>(true); Bounds wb=rends[0].bounds; foreach(var r in rends) wb.Encapsulate(r.bounds);
            sb.AppendLine($"[3] 배치 (X {GateX}, Z {GateZ}) Y={gy:F2}(지형고도) rot 0 · bbox {wb.size.x:F1}×{wb.size.z:F1}×{wb.size.y:F1}m · 밑면Y {wb.min.y:F2}");
            sb.AppendLine($"[3] 정면(−Z) 실측 남단 Z {wb.min.z:F2} · 북단 Z {wb.max.z:F2} → 정면이 남(−Z)을 봄 {(wb.min.z<GateZ?"(현판 남향 예상)":"")} ※렌더 A에서 현판 육안 확인");

            // [3] 지형 footprint 편차
            float mn=1e9f,mx=-1e9f,sum=0f; int cnt=0;
            for(float x=GateX-FpHalfX;x<=GateX+FpHalfX;x+=2f) for(float z=FpZ0;z<=FpZ1;z+=2f){ float h=H(x,z); mn=Mathf.Min(mn,h); mx=Mathf.Max(mx,h); sum+=h; cnt++; }
            float dev=mx-mn, mean=sum/cnt;
            sb.AppendLine($"[3] 지형 footprint(X {GateX}±{FpHalfX}, Z {FpZ0}~{FpZ1}, {cnt}점): 고도 {mn:F2}~{mx:F2}m 평균 {mean:F2} · 편차 {dev:F2}m {(dev>0.15f?"⚠ 0.15m 초과 → 평탄화 필요(보고만, 미수정)":"✓ 평탄")}");
            sb.AppendLine($"[3] 세트 밑면 Y {wb.min.y:F2} vs 원점 지형 {gy:F2} (밑면이 지형과 {(Mathf.Abs(wb.min.y-gy)<0.05f?"일치 ✓":$"차이 {wb.min.y-gy:+0.00;-0.00}m")})");

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, Scene);

            // [4] 메트릭
            int rc=rends.Length, sub=0, tris=0;
            foreach(var mf in inst.GetComponentsInChildren<MeshFilter>(true)) if(mf.sharedMesh!=null){ sub+=mf.sharedMesh.subMeshCount; for(int s=0;s<mf.sharedMesh.subMeshCount;s++) tris+=(int)(mf.sharedMesh.GetIndexCount(s)/3); }
            var uniqMat=new HashSet<Material>(); foreach(var r in rends) foreach(var m in r.sharedMaterials) if(m!=null) uniqMat.Add(m);
            sb.AppendLine("\n[4] 메트릭");
            sb.AppendLine($"· 렌더러 {rc} · 서브메시 {sub}(배칭前 드로우콜 상한) · 고유 머티리얼 {uniqMat.Count}(SetPass 하한) · tris {tris}");
            sb.AppendLine($"· 정적배칭: 세트 Static ON → 동일 머티리얼 서브메시 결합. 정확한 드로우콜/배칭은 ▶Play + Stats/Frame Debugger 필요");
            // 텍스처 메모리
            long total=0; var byFmt=new Dictionary<string,(int n,long b)>();
            foreach(var g in AssetDatabase.FindAssets("t:Texture2D", new[]{TexDir})){
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g)); if(tex==null) continue;
                long bytes=Profiler.GetRuntimeMemorySizeLong(tex); total+=bytes;
                string k=$"{tex.width}x{tex.height} {tex.format}"; byFmt.TryGetValue(k,out var v); byFmt[k]=(v.n+1,v.b+bytes);
            }
            sb.AppendLine($"· 텍스처 런타임 메모리(에디터) 합 {total/1048576f:F1}MB — 내역:");
            foreach(var kv in byFmt.OrderByDescending(x=>x.Value.b)) sb.AppendLine($"    {kv.Key} × {kv.Value.n}장 = {kv.Value.b/1048576f:F1}MB");
            sb.AppendLine("· ※Quest3는 Android ASTC 6x6·BaseColor1024/Normal512 축소 권장(README)");

            Application.logMessageReceived -= cb;
            sb.AppendLine($"\n[4] 콘솔 에러/경고(이 실행 중 캡처) {logs.Count}건:");
            if(logs.Count==0) sb.AppendLine("   없음 ✓");
            else foreach(var l in logs.Take(40)) sb.AppendLine("   "+l.Replace("\n"," "));

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"13b 완료. 배치 Y={gy:F2}·지형편차 {dev:F2}m{(dev>0.15f?"(⚠평탄화 필요)":"")}\n렌더러 {rc}·머티 {uniqMat.Count}·tex {total/1048576f:F0}MB. 다음 13c 렌더.","확인");
        }

        // ─────────────────────────── 13c ───────────────────────────
        [MenuItem("Tools/Seocheon/Gate/13c. Render checks (A/B/C/D)")]
        public static void RenderChecks()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 13c Gate Render checks =====");
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var terrs=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terrs.Length>0?terrs[0]:null;
            float gy = terrain!=null ? terrain.transform.position.y+terrain.SampleHeight(new Vector3(GateX,0,GateZ)) : 0f;
            float eye=gy+1.6f;
            Directory.CreateDirectory(RenderDir);
            var files=new List<string>(); string err=null;
            try{
                // A: 광장에서 외삼문 정면 (남쪽에서 북쪽 바라봄)
                files.Add(Shot("unity_A_plaza_front", new Vector3(GateX, eye, GateZ-20f), new Vector3(GateX, gy+4f, GateZ-2f), 55f));
                // B: 문 앞에서 북쪽 — 동헌 보이는지 (게이트 개구부에서 +Z)
                files.Add(Shot("unity_B_through_gate", new Vector3(GateX, eye, GateZ-6f), new Vector3(GateX, gy+2f, GateZ+30f), 60f));
                // C: 마당에서 담장 내측 근접 — 코핑 능선 판정 (동담 안쪽을 올려다봄)
                files.Add(Shot("unity_C_coping", new Vector3(GateX+8f, eye, GateZ+16f), new Vector3(GateX+17.4f, gy+2.3f, GateZ+16f), 55f));
                // D: 부감 전체
                files.Add(Shot("unity_D_aerial", new Vector3(GateX, gy+42f, GateZ-18f), new Vector3(GateX, gy+2f, GateZ+13f), 55f));
            }catch(System.Exception e){ err=e.ToString(); }
            if(err!=null) sb.AppendLine("렌더 실패: "+err);
            else { sb.AppendLine($"렌더 4장 → {RenderDir}"); foreach(var f in files) sb.AppendLine("   "+f); }
            sb.AppendLine("판정: A 현판(정면 남향) · B 동헌 보임 · C 코핑 능선 살아있음(양면) · D 전체 조립");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"13c 렌더 {files.Count}장 완료 → {RenderDir}\nConsole 확인.","확인");
        }

        // ─────────────────────────── 13d (필요시) ───────────────────────────
        [MenuItem("Tools/Seocheon/Gate/13d. Flip gate 180 (정면이 북향일 때만)")]
        public static void Flip()
        {
            var scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            GameObject holder=null; foreach(var r in scene.GetRootGameObjects()) if(r.name=="_GwanaGate") holder=r;
            if(holder==null||holder.transform.childCount==0){ EditorUtility.DisplayDialog("Seocheon","_GwanaGate 없음 — 먼저 13b 실행.","확인"); return; }
            var inst=holder.transform.GetChild(0);
            var e=inst.rotation.eulerAngles; inst.rotation=Quaternion.Euler(e.x, e.y+180f, e.z);   // 게이트 원점 기준 회전(위치 유지)
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, Scene);
            Debug.Log($"[Seocheon] Gate Flip 180 → rotY {inst.rotation.eulerAngles.y:F0}° (마당은 북쪽 동헌 쪽 유지)");
            EditorUtility.DisplayDialog("Seocheon", $"외삼문 180° 회전 (rotY {inst.rotation.eulerAngles.y:F0}°). 씬 저장. 13c로 재확인.","확인");
        }

        // ─────────────────────────── 13e ───────────────────────────
        [MenuItem("Tools/Seocheon/Gate/13e. Delete old Donheon from scene (no save)")]
        public static void DeleteOldDonheon()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 13e Delete old Donheon (no save) =====");
            var scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var cands=FindDonheonRoots(scene);
            sb.AppendLine($"동헌 후보 루트 {cands.Count}개 (이름에 donheon/동헌 포함):");
            foreach(var g in cands){
                var t=g.transform;
                sb.AppendLine($"  · '{g.name}'  pos {V(t.position)} rot {V(t.eulerAngles)} scale {V(t.localScale)}");
                foreach(Transform c in t) sb.AppendLine($"      child '{c.name}'  world pos {V(c.position)} rot {V(c.eulerAngles)}");
                var rr=g.GetComponentsInChildren<Renderer>(true);
                if(rr.Length>0){ Bounds b=rr[0].bounds; foreach(var r in rr) b.Encapsulate(r.bounds); sb.AppendLine($"      bounds center {V(b.center)} size {V(b.size)}"); }
            }
            if(cands.Count==0){ sb.AppendLine("→ 삭제 대상 없음. (이미 제거됨?)"); }
            else if(cands.Count>1){ sb.AppendLine("→ ★여러 개 잡힘. 임의 삭제 안 함 — 어느 것을 지울지 알려주세요(멈춤)."); }
            else {
                var victim=cands[0];
                sb.AppendLine($"→ 단일 후보 '{victim.name}' 삭제(씬 인스턴스만 · Assets 미변경).");
                Object.DestroyImmediate(victim);
                sb.AppendLine("남은 루트 오브젝트: "+string.Join(", ", scene.GetRootGameObjects().Select(r=>r.name)));
                sb.AppendLine("※ 씬 저장 안 함 — 이대로 13f 실행하면 이 삭제 상태에서 이어집니다.");
            }
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", cands.Count==1?"동헌 1개 삭제(미저장). 13f로 이어가세요.":$"동헌 후보 {cands.Count}개 — Console 확인.","확인");
        }

        // ─────────────────────────── 13f ───────────────────────────
        [MenuItem("Tools/Seocheon/Gate/13f. Group set + attach Donheon")]
        public static void GroupAndAttach()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 13f Group set + attach Donheon =====");
            // 13e 직후(미저장) 상태를 잇기 위해 활성 씬이 대상이면 재로드하지 않는다
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=Scene){ scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single); sb.AppendLine("씬 새로 엶."); }
            else sb.AppendLine("활성 씬 사용(13e 미저장 상태 승계).");

            // 남은 옛 동헌 있으면 중단(13e 먼저)
            var leftover=FindDonheonRoots(scene);
            if(leftover.Count>0){ sb.AppendLine($"★옛 동헌 루트 {leftover.Count}개 남아 있음: {string.Join(", ",leftover.Select(g=>g.name))} → 먼저 13e 실행 후 다시.");
                Debug.Log(sb.ToString()); EditorUtility.DisplayDialog("Seocheon","옛 동헌이 남아 있습니다. 13e 먼저 실행.","확인"); return; }

            // 게이트 찾기
            Transform oisamun=null;
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if(t.name=="SM_Oisamun"){ oisamun=t; break; }
            if(oisamun==null){ sb.AppendLine("★SM_Oisamun 없음 — 먼저 13b 실행."); Debug.Log(sb.ToString()); EditorUtility.DisplayDialog("Seocheon","게이트 미배치 — 13b 먼저.","확인"); return; }
            var gateRoot=oisamun.parent;  // FBX 인스턴스 루트(6개 자식)

            // GwanaGateSet(피벗=게이트 원점, 회전0) 생성 후 게이트 편입
            var setGO=new GameObject("GwanaGateSet");
            setGO.transform.position=gateRoot.position; setGO.transform.rotation=Quaternion.identity; setGO.transform.localScale=Vector3.one;
            var oldHolder=gateRoot.parent;
            gateRoot.SetParent(setGO.transform, true);  // 월드 유지
            if(oldHolder!=null && oldHolder.name=="_GwanaGate" && oldHolder.childCount==0) Object.DestroyImmediate(oldHolder.gameObject);
            sb.AppendLine($"[집합] GwanaGateSet 생성 @ {V(setGO.transform.position)} rot0 · 게이트 루트 '{gateRoot.name}'(자식 {gateRoot.childCount}) 편입");

            // 새 동헌 부착
            var donPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(DonPrefab);
            if(donPrefab==null){ sb.AppendLine($"★동헌 프리팹 없음: {DonPrefab}"); Debug.Log(sb.ToString()); EditorUtility.DisplayDialog("Seocheon","Donheon.prefab 없음.","확인"); return; }
            var don=(GameObject)PrefabUtility.InstantiatePrefab(donPrefab);
            don.transform.SetParent(setGO.transform, false);
            don.transform.localPosition=new Vector3(0f, 0.70f, 26.345f);
            don.transform.localRotation=Quaternion.identity;
            SetStaticRec(setGO);

            // 출력
            sb.AppendLine("[자식] GwanaGateSet 직계: "+string.Join(", ", Enumerable.Range(0,setGO.transform.childCount).Select(i=>setGO.transform.GetChild(i).name)));
            sb.AppendLine($"[동헌] local {V(don.transform.localPosition)} → world {V(don.transform.position)} (기대 world Z≈616)");
            var all=setGO.GetComponentsInChildren<Renderer>(true); Bounds wb=all[0].bounds; foreach(var r in all) wb.Encapsulate(r.bounds);
            sb.AppendLine($"[세트] 전체 bbox center {V(wb.center)} size {V(wb.size)} (min {V(wb.min)} max {V(wb.max)})");
            sb.AppendLine("※ 동헌 정면(현판)이 게이트(−Z)를 보는지 렌더 B로 확인. 반대면 don 자식만 rotY 180.");

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, Scene);
            sb.AppendLine("씬 저장됨.");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"13f 완료. GwanaGateSet(게이트+담장+동헌) 하나로 묶음.\n동헌 world {V(don.transform.position)}. Console 확인.","확인");
        }

        private static List<GameObject> FindDonheonRoots(Scene scene)
        {
            var list=new List<GameObject>();
            foreach(var r in scene.GetRootGameObjects()){ string n=r.name.ToLowerInvariant();
                if(n.Contains("donheon")||r.name.Contains("동헌")) list.Add(r); }
            return list;
        }
        private static string V(Vector3 v){ return $"({v.x:F2}, {v.y:F2}, {v.z:F2})"; }

        // ── helpers ──
        private static string Shot(string name, Vector3 pos, Vector3 look, float fov)
        {
            const int W=1600,H=900; var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
            var camGO=new GameObject("__gcam"); var cam=camGO.AddComponent<Camera>();
            cam.clearFlags=CameraClearFlags.Skybox; cam.fieldOfView=fov; cam.nearClipPlane=0.05f; cam.farClipPlane=3000f; cam.enabled=false;
            cam.transform.position=pos; cam.transform.rotation=Quaternion.LookRotation((look-pos).normalized, Vector3.up);
            string fn=Path.Combine(RenderDir, name+".png");
            try{ var req=new UniversalRenderPipeline.SingleCameraRequest();
                 if(RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req); }
                 else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                 RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null;
                 File.WriteAllBytes(fn, tex.EncodeToPNG()); }
            finally{ Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
            return fn;
        }
        private static void SetStaticRec(GameObject g){ g.isStatic=true; foreach(Transform c in g.transform) SetStaticRec(c.gameObject); }
        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
    }
}
