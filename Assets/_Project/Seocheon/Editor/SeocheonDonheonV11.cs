using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    /// 동헌 v11 임포트(교체) + (462,616) 배치. 팀 공통 빌더 패턴.
    /// ★접지: 인스턴스 Y = padY + 0.70 (bounds.min 보정 금지 — 기단 base 0.8m 들림 방지).
    /// ★정면(돌계단·대청)이 −Z(남/다리). 원본 FBX·낙안 팩·링·수면·다리 미변경.
    /// </summary>
    public static class SeocheonDonheonV11
    {
        private const string SrcFbx   = @"C:\Users\User\관아\_export\Donheon_Quest3_v11.fbx";
        private const string Fbx      = "Assets/_Project/Seocheon/Art/Models/Donheon_Quest3_v11.fbx";
        private const string Fbm      = "Assets/_Project/Seocheon/Art/Models/Donheon_Quest3_v11.fbm";
        private const string TexDir   = "Assets/_Project/Seocheon/Art/Textures/Donheon";
        private const string MatDir   = "Assets/_Project/Seocheon/Art/Materials/Donheon";
        private const string PrefabDir= "Assets/_Project/Seocheon/Prefabs";
        private const string PrefabPath="Assets/_Project/Seocheon/Prefabs/Donheon.prefab";
        private const string NoGround = "Assets/_Project/Seocheon/Art/Models/Donheon_Body_NoGround.asset";
        private const string ScenePath= "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string BakTerr  = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain_backup_predonheon.asset";
        private const string PathFile = @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const string RenderDir= @"C:\Users\User\_AssetBackup\render_donheon3";

        private const float CX=463f, CZ=678f;                 // 개천 링 중심
        private static readonly Vector2 PadC=new Vector2(462f,616f);
        private static readonly Vector2 OldPad=new Vector2(462f,597f); // 이전 실행 평탄 지점(원복 확인용)
        private static readonly Vector2 BridgeTop=new Vector2(462f,571f);
        private const float HalfX=12f, HalfZ=9f, Skirt=8f;    // 평탄 24×18 + 사면 8m
        private const float RingBand=11.8f;                   // 링 중심선 ±이 안 침범 금지(검사용)
        private const float GroundOffset=0.70f;               // 마당면 로컬 −0.70 → +0.70 올려 접지
        // ★렌더 카메라 거리용 하드코딩 반치수(월드 bounds 사용 금지)
        private const float HalfW=9.35f, HalfD=6.52f;

        private static readonly string[] NormalMaps = {
            "MI_R_Roof_Normal","MI_R_BrickConcrete_Normal","MI_R_Buyeon_2_Normal",
            "MI_KoreanWood_1_Normal","MI_KoreanPaper_1_Normal","T_Wall01b_N" };

        [MenuItem("Tools/Seocheon/Donheon/Import v11 + Place (462,616)")]
        public static void Run()
        {
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] Donheon v11 Import + Place(462,616) =====");
            if(!File.Exists(SrcFbx)){ EditorUtility.DisplayDialog("Seocheon","원본 FBX 없음:\n"+SrcFbx,"확인"); return; }

            // ───────────────────────── [0] 임포트 교체 ─────────────────────────
            string absDst=Path.GetFullPath(Fbx);
            Directory.CreateDirectory(Path.GetDirectoryName(absDst));
            File.Copy(SrcFbx, absDst, true);
            AssetDatabase.ImportAsset(Fbx, ImportAssetOptions.ForceUpdate);
            sb.AppendLine($"[0] 복사: {SrcFbx}\n     → {Fbx} (기존 v1 유지)");

            var mi=(ModelImporter)AssetImporter.GetAtPath(Fbx);
            mi.globalScale=1f; mi.useFileScale=true;                       // Scale 1 · Convert Units ON
            mi.bakeAxisConversion=false;                                   // Bake Axis Conversion OFF
            mi.isReadable=true;                                            // 측정 위해 임시 ON(끝에서 OFF)
            mi.meshOptimizationFlags=MeshOptimizationFlags.Everything;     // Optimize Mesh ON
            mi.addCollider=false;                                          // Generate Colliders OFF
            mi.importCameras=false; mi.importLights=false;                 // Cameras·Lights OFF
            mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            mi.SaveAndReimport();
            sb.AppendLine("[0] 임포트: Scale 1 · Convert Units ON · Bake Axis OFF · Optimize Mesh ON · Colliders OFF · Cameras/Lights OFF · R/W(임시 ON)");

            // .fbm 삭제
            if(AssetDatabase.IsValidFolder(Fbm)){ AssetDatabase.DeleteAsset(Fbm); sb.AppendLine("[0] .fbm 폴더 삭제"); }
            else sb.AppendLine("[0] .fbm 폴더 없음");

            // 기존 13 머티리얼 로드
            var mats=new Dictionary<string,Material>();
            foreach(var g in AssetDatabase.FindAssets("t:Material", new[]{MatDir})){ var p=AssetDatabase.GUIDToAssetPath(g);
                var m=AssetDatabase.LoadAssetAtPath<Material>(p); if(m!=null) mats[Path.GetFileNameWithoutExtension(p)]=m; }
            sb.AppendLine($"[0] 기존 머티리얼 {mats.Count}종 로드 ({MatDir})");

            // FBX 슬롯명 + 슬롯별 tris(실사용 판별)
            var fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var slotTris=new Dictionary<string,int>();
            foreach(var r in fbxGo.GetComponentsInChildren<MeshRenderer>(true)){ var mf=r.GetComponent<MeshFilter>(); var msh=mf!=null?mf.sharedMesh:null; if(msh==null) continue;
                var sm=r.sharedMaterials; for(int s=0;s<sm.Length;s++){ string nm=sm[s]!=null?sm[s].name:"(null)"; int tc=s<msh.subMeshCount?(int)(msh.GetIndexCount(s)/3):0;
                    slotTris.TryGetValue(nm,out int prev); slotTris[nm]=prev+tc; } }

            // 리맵: 슬롯 → 기존 머티리얼(정확/접두/strip 매칭)
            var unmatched=new List<string>();
            foreach(var s in slotTris.Keys){ if(s=="(null)"){ unmatched.Add(s); continue; } Material target=null;
                if(mats.ContainsKey(s)) target=mats[s];
                else foreach(var kv in mats){ string k=kv.Key; if(s==k||Strip(s)==Strip(k)||s.StartsWith(k)||k.StartsWith(s)){ target=kv.Value; break; } }
                if(target!=null) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),s), target);
                else unmatched.Add(s); }
            mi.SaveAndReimport();
            sb.AppendLine($"[0] 리맵 {slotTris.Count-unmatched.Count}/{slotTris.Count} 슬롯");

            // 미할당(핑크 후보) 목록 — 실사용 tris 함께(0이면 빈 슬롯=무해)
            if(unmatched.Count==0) sb.AppendLine("[0] 미할당 머티리얼: 없음 ✓");
            else { sb.AppendLine($"[0] ★미할당 머티리얼 {unmatched.Count}종 (tris=0 은 빈 슬롯/무해):");
                foreach(var u in unmatched){ slotTris.TryGetValue(u,out int tc); sb.AppendLine($"     - {u}  (사용 {tc} tris){(tc==0?"  ← 빈 슬롯":"  ← ★실사용 핑크")}"); } }

            // 노멀맵 6장 Texture Type 확인
            sb.AppendLine("[0] 노멀맵 6장 Texture Type:");
            foreach(var nmName in NormalMaps){ var tp=$"{TexDir}/{nmName}.png"; var ti=(TextureImporter)AssetImporter.GetAtPath(tp);
                if(ti==null) sb.AppendLine($"     - {nmName}: ⚠ 텍스처 없음");
                else sb.AppendLine($"     - {nmName}: {(ti.textureType==TextureImporterType.NormalMap?"Normal Map ✓":"⚠ "+ti.textureType)}"); }

            // ── 정면(계단) 방향 측정: M_Stone_Granite 지오메트리 로컬 Z ──
            float aPos=0f, aNeg=0f, wZsum=0f, wSum=0f;
            fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            foreach(var r in fbxGo.GetComponentsInChildren<MeshRenderer>(true)){ var mf=r.GetComponent<MeshFilter>(); var msh=mf!=null?mf.sharedMesh:null; if(msh==null) continue;
                var sm=r.sharedMaterials; var vs=msh.vertices; var tr=r.transform;
                for(int s=0;s<sm.Length && s<msh.subMeshCount;s++){ if(sm[s]==null||!sm[s].name.Contains("Granite")) continue;
                    var tri=msh.GetTriangles(s);
                    for(int t=0;t<tri.Length;t+=3){ Vector3 a=tr.TransformPoint(vs[tri[t]]), b=tr.TransformPoint(vs[tri[t+1]]), c=tr.TransformPoint(vs[tri[t+2]]);
                        float area=0.5f*Vector3.Cross(b-a,c-a).magnitude; float cz=(a.z+b.z+c.z)/3f;
                        wZsum+=area*cz; wSum+=area; if(cz>=0f) aPos+=area; else aNeg+=area; } } }
            float gcz = wSum>0.0001f ? wZsum/wSum : 0f;
            bool dominantPos = aPos>=aNeg;                 // 계단 우세측이 +Z이면 남향 위해 180° 필요
            float rotY = dominantPos ? 180f : 0f;
            sb.AppendLine("\n[3] 정면 방향(M_Stone_Granite 계단):");
            sb.AppendLine($"     계단 면적 +Z {aPos:F1}㎡ / −Z {aNeg:F1}㎡ · 면적가중 중심 로컬Z {gcz:+0.00;-0.00}");
            sb.AppendLine($"     → 계단 우세측 {(dominantPos?"+Z":"−Z")} · 남향(−Z)으로 정렬 위해 rotY {rotY:F0}° (틀리면 Flip Front 180 메뉴)");

            // 프리팹 v11 기준 재생성(자식 전부 Static)
            EnsureFolder(PrefabDir);
            var pinst=(GameObject)PrefabUtility.InstantiatePrefab(fbxGo);
            SetStaticRec(pinst);
            PrefabUtility.SaveAsPrefabAsset(pinst, PrefabPath);
            Object.DestroyImmediate(pinst);
            sb.AppendLine($"[0] 프리팹 재생성 → {PrefabPath} (v11 FBX 직접 참조 · Static ON)");

            // NoGround 참조 검사
            bool ngExists=AssetDatabase.LoadAssetAtPath<Mesh>(NoGround)!=null;
            int ngRefs=CountAssetRefs(NoGround);
            sb.AppendLine($"[0] NoGround.asset: {(ngExists?"파일 존재":"없음")} · 프로젝트 참조 {ngRefs}건 {(ngRefs==0?"(신규 프리팹은 v11 메시 직접 참조 → 불필요, 수동 삭제 가능)":"⚠ 아직 참조됨")}");

            // R/W 최종 OFF
            mi.isReadable=false; mi.SaveAndReimport();
            sb.AppendLine("[0] Read/Write 최종 OFF");

            // ───────────────────────── 씬 작업 ─────────────────────────
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terrs=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terrs.Length>0?terrs[0]:null;
            if(terrain==null){ EditorUtility.DisplayDialog("Seocheon","Terrain 없음.","확인"); Debug.Log(sb.ToString()); return; }
            var td=terrain.terrainData; int Rr=td.heightmapResolution; Vector3 S=td.size;
            float tY=terrain.transform.position.y, tX0=terrain.transform.position.x, tZ0=terrain.transform.position.z;
            float texX=S.x/(Rr-1), texZ=S.z/(Rr-1);

            // [1] 지형 되돌리기 — 백업 전체 복원
            float oldBefore=terrain.SampleHeight(new Vector3(OldPad.x,0,OldPad.y))+tY;
            var bak=AssetDatabase.LoadAssetAtPath<TerrainData>(BakTerr);
            if(bak!=null && bak.heightmapResolution==Rr){ td.SetHeights(0,0,bak.GetHeights(0,0,Rr,Rr)); EditorUtility.SetDirty(td); terrain.Flush(); }
            var Hn=td.GetHeights(0,0,Rr,Rr);
            float H(float x,float z){ float u=(x-tX0)/S.x,v=(z-tZ0)/S.z; float fx=Mathf.Clamp(u*(Rr-1),0,Rr-1),fz=Mathf.Clamp(v*(Rr-1),0,Rr-1);
                int x0=(int)fx,z0=(int)fz,x1=Mathf.Min(x0+1,Rr-1),z1=Mathf.Min(z0+1,Rr-1); float tx=fx-x0,tz=fz-z0;
                return tY+Mathf.Lerp(Mathf.Lerp(Hn[z0,x0],Hn[z0,x1],tx),Mathf.Lerp(Hn[z1,x0],Hn[z1,x1],tx),tz)*S.y; }
            float oldAfter=H(OldPad.x,OldPad.y);
            KillRoot(scene,"_Donheon");
            sb.AppendLine($"\n[1] 지형 복원 {(bak!=null?"완료":"⚠백업없음")} · (462,597) {oldBefore:F2}→{oldAfter:F2}m (원복 {(Mathf.Abs(oldBefore-oldAfter)>0.05f?"확인 — 이전 평탄 제거됨":"변화 미미")}) · 기존 _Donheon 루트 제거");

            // [2] 대지 (462,616) 사전 측정
            float padY=H(PadC.x,PadC.y);
            float slope=0; foreach(var d in new[]{new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1)}) slope=Mathf.Max(slope,Mathf.Abs(H(PadC.x+d.x*13,PadC.y+d.y*13)-padY));
            sb.AppendLine($"[2] (462,616) 현재높이 {padY:F2}m · 주변 ±13m 최대경사 {slope:F2}m");

            // [2] 평탄화 24×18 + 8m 사면, 링밴드 검사(침범 0 기대)
            float padN=Mathf.Clamp01((padY-tY)/S.y);
            int hxMin=Mathf.Clamp(Mathf.FloorToInt((PadC.x-tX0-HalfX-Skirt)/texX),0,Rr-1), hxMax=Mathf.Clamp(Mathf.CeilToInt((PadC.x-tX0+HalfX+Skirt)/texX),0,Rr-1);
            int hzMin=Mathf.Clamp(Mathf.FloorToInt((PadC.y-tZ0-HalfZ-Skirt)/texZ),0,Rr-1), hzMax=Mathf.Clamp(Mathf.CeilToInt((PadC.y-tZ0+HalfZ+Skirt)/texZ),0,Rr-1);
            int flat=0, ringHit=0;
            for(int hz=hzMin;hz<=hzMax;hz++) for(int hx=hxMin;hx<=hxMax;hx++){ float x=tX0+hx*texX, z=tZ0+hz*texZ;
                float rr=Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ)); float rth=RthAt(Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg);
                if(Mathf.Abs(rr-rth)<=RingBand){ ringHit++; continue; }   // 방어적 스킵 + 카운트
                float ox=Mathf.Max(0,Mathf.Abs(x-PadC.x)-HalfX), oz=Mathf.Max(0,Mathf.Abs(z-PadC.y)-HalfZ); float dd=Mathf.Sqrt(ox*ox+oz*oz);
                if(dd<=0.001f){ Hn[hz,hx]=padN; flat++; } else if(dd<=Skirt){ float tt=Mathf.SmoothStep(0,1,dd/Skirt); Hn[hz,hx]=Mathf.Lerp(padN,Hn[hz,hx],tt); } }
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); terrain.Flush();
            sb.AppendLine($"[2] 평탄 24×18({flat}텍셀) {padY:F2}m + 사면 {Skirt}m · 링밴드(±{RingBand}m) 침범 {ringHit}텍셀 {(ringHit==0?"✓ (한 텍셀도 안 들어감)":"⚠ 침범 — 방어스킵됨")}");

            // [3] 배치 — Y = padY + 0.70 (bounds 보정 금지)
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var holder=new GameObject("_Donheon");
            var inst=(GameObject)PrefabUtility.InstantiatePrefab(prefab); inst.transform.SetParent(holder.transform);
            inst.transform.rotation=Quaternion.Euler(0,rotY,0);
            inst.transform.position=new Vector3(PadC.x, padY+GroundOffset, PadC.y);
            SetStaticRec(holder);
            var rends=inst.GetComponentsInChildren<Renderer>(true); Bounds wb=rends[0].bounds; foreach(var r in rends) wb.Encapsulate(r.bounds);
            float frontZ=wb.min.z;                             // 정면(−Z) 실측
            sb.AppendLine($"[3] 배치 (462,616) Y={padY+GroundOffset:F2}(=padY {padY:F2}+0.70) rotY {rotY:F0}° · 정면Z(실측) {frontZ:F2} · bbox {wb.size.x:F1}×{wb.size.z:F1}×{wb.size.y:F1}m");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);

            // ───────────────────────── [4] 검증 ─────────────────────────
            int donTris=0; foreach(var mf in inst.GetComponentsInChildren<MeshFilter>(true)) if(mf.sharedMesh!=null) for(int s=0;s<mf.sharedMesh.subMeshCount;s++) donTris+=(int)(mf.sharedMesh.GetIndexCount(s)/3);
            int sceneTris=0; foreach(var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) if(mf.sharedMesh!=null) for(int s=0;s<mf.sharedMesh.subMeshCount;s++) sceneTris+=(int)(mf.sharedMesh.GetIndexCount(s)/3);
            var pinks=new List<string>(); foreach(var r in inst.GetComponentsInChildren<Renderer>(true)) foreach(var m in r.sharedMaterials) if(m==null||m.shader==null||m.shader.name.Contains("InternalErrorShader")){ string pn=m!=null?m.name:"(null)"; if(!pinks.Contains(pn)) pinks.Add(pn); }
            long texMem=0; foreach(var g in AssetDatabase.FindAssets("t:Texture2D", new[]{TexDir})){ var tex=AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(g)); if(tex!=null) texMem+=Profiler.GetRuntimeMemorySizeLong(tex); }
            // 밑면-지형 틈/파묻힘 (footprint 9점, bounds.min.y 기준 밑면)
            float bottomY=wb.min.y, maxGap=0, maxEmbed=0;
            for(int a=-1;a<=1;a++) for(int b=-1;b<=1;b++){ float x=PadC.x+a*HalfW*0.8f, z=PadC.y+b*HalfD*0.8f; float dh=H(x,z)-bottomY;
                if(dh>maxEmbed) maxEmbed=dh; if(-dh>maxGap) maxGap=-dh; }
            // 다리 상면(462,571) → 정면 Z · 개천 남측 둑 여유
            float deckY=DeckTop(BridgeTop.x, BridgeTop.y); if(float.IsNaN(deckY)) deckY=padY;
            float bridgeToFront=frontZ-BridgeTop.y;
            float rSouth=RthAt(-90f); float bankSouthZ=CZ-rSouth; float backZ=wb.max.z; float bankGap=bankSouthZ-backZ;
            sb.AppendLine("\n[4] 검증");
            sb.AppendLine($"· 동헌 tris {donTris} (기대 ~104,072) · 씬 총 tris {sceneTris} · 텍스처 메모리 {texMem/1048576f:F1}MB");
            sb.AppendLine($"· 핑크(InternalErrorShader) {(pinks.Count==0?"없음 ✓":string.Join(", ",pinks))}");
            sb.AppendLine($"· 밑면-지형 최대 틈 {maxGap:F2}m · 최대 파묻힘 {maxEmbed:F2}m (footprint 9점)");
            sb.AppendLine($"· 다리 상면(462,571 topY {deckY:F2}) → 관아 정면Z {frontZ:F2} 거리 {bridgeToFront:F1}m");
            sb.AppendLine($"· 관아 배면Z {backZ:F2} → 개천 남측 둑 Z {bankSouthZ:F2}(rSouth {rSouth:F1}) 여유 {bankGap:F1}m");

            // ───────────────────────── [5] 렌더 ─────────────────────────
            Directory.CreateDirectory(RenderDir); var files=new List<string>(); string rerr=null;
            float frontLine=PadC.y-HalfD;                       // ★하드코딩 정면선
            float midY=padY+GroundOffset+wb.size.y*0.45f;       // 룩 타깃 높이(거리는 하드코딩)
            try{
                files.Add(Cam("OnBridge_north", new Vector3(BridgeTop.x, deckY+1.6f, BridgeTop.y), new Vector3(PadC.x,midY,PadC.y)));
                files.Add(Cam("Front_20m",      new Vector3(PadC.x, H(PadC.x,frontLine-20f)+1.6f, frontLine-20f), new Vector3(PadC.x,midY,PadC.y)));
                files.Add(Cam("Base_close",     new Vector3(PadC.x, padY+1.6f, frontLine-6f), new Vector3(PadC.x, padY+0.4f, PadC.y)));
                files.Add(Cam("Side_east",      new Vector3(PadC.x+18f, H(PadC.x+18f,PadC.y)+1.6f, PadC.y), new Vector3(PadC.x,midY,PadC.y)));
                files.Add(CamTop("Aerial_120",  PadC.x, 593f, 120f));
            }catch(System.Exception e){ rerr=e.ToString(); }
            if(rerr!=null) sb.AppendLine("\n[5] 렌더 실패: "+rerr);
            else { sb.AppendLine($"\n[5] 렌더 {files.Count}장 → {RenderDir}"); foreach(var f in files) sb.AppendLine("   "+f); }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"동헌 v11 임포트+배치 완료.\n정면 rotY {rotY:F0}° · 여유 다리→정면 {bridgeToFront:F1}m.\n정면 틀리면 Flip Front 180. Console/렌더 확인.","확인");
        }

        // 같은 경로를 SeocheonDonheonPlace 도 쓰고 있어 하나가 등록에 실패했다 — 이쪽 이름을 v11 로 밝힌다.
        [MenuItem("Tools/Seocheon/Donheon/Flip Front 180 (v11)")]
        public static void FlipFront()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject holder=null; foreach(var r in scene.GetRootGameObjects()) if(r.name=="_Donheon"){ holder=r; break; }
            if(holder==null||holder.transform.childCount==0){ EditorUtility.DisplayDialog("Seocheon","_Donheon 인스턴스 없음. 먼저 Import v11 + Place 실행.","확인"); return; }
            var inst=holder.transform.GetChild(0);
            var e=inst.rotation.eulerAngles; inst.rotation=Quaternion.Euler(e.x, e.y+180f, e.z);
            inst.position=new Vector3(PadC.x, inst.position.y, PadC.y);   // 회전 후 중심 재고정
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Seocheon] Flip Front 180 → rotY {inst.rotation.eulerAngles.y:F0}°");
            EditorUtility.DisplayDialog("Seocheon", $"정면 180° 회전 완료 (rotY {inst.rotation.eulerAngles.y:F0}°). 씬 저장됨.","확인");
        }

        // ── helpers ──
        private static string Strip(string s){ int d=s.LastIndexOf('.'); if(d>0 && s.Length-d<=4){ bool num=true; for(int i=d+1;i<s.Length;i++) if(!char.IsDigit(s[i])) num=false; if(num) return s.Substring(0,d); } return s; }
        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
        private static void SetStaticRec(GameObject g){ g.isStatic=true; foreach(Transform c in g.transform) SetStaticRec(c.gameObject); }
        private static void KillRoot(Scene s,string n){ foreach(var r in s.GetRootGameObjects()) if(r.name==n) Object.DestroyImmediate(r); }
        private static int CountAssetRefs(string assetPath){ var guid=AssetDatabase.AssetPathToGUID(assetPath); if(string.IsNullOrEmpty(guid)) return 0; int n=0;
            foreach(var g in AssetDatabase.FindAssets("t:Prefab t:Scene")){ var p=AssetDatabase.GUIDToAssetPath(g); foreach(var dep in AssetDatabase.GetDependencies(p,false)) if(dep==assetPath){ n++; break; } } return n; }

        private static float DeckTop(float x,float z){ float best=float.NaN;
            foreach(var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)){ var t=mr.transform; bool skip=false;
                for(var p=t; p!=null; p=p.parent) if(p.name=="_Donheon"){ skip=true; break; } if(skip) continue;
                var b=mr.bounds; if(x>=b.min.x&&x<=b.max.x&&z>=b.min.z&&z<=b.max.z){ if(float.IsNaN(best)||b.max.y>best) best=b.max.y; } } return best; }

        private static float RthAt(float angDeg){ if(!File.Exists(PathFile)) return 106f; var ci=CultureInfo.InvariantCulture; var lines=File.ReadAllLines(PathFile);
            float best=1e9f, br=106f; for(int i=1;i<lines.Length;i++){ var t=lines[i].Split(' '); if(t.Length<2) continue; float x=float.Parse(t[0],ci), z=float.Parse(t[1],ci);
                float a=Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg; float dd=Mathf.Abs(Mathf.DeltaAngle(a,angDeg)); if(dd<best){best=dd; br=Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ));} } return br; }

        private static string Cam(string name,Vector3 pos,Vector3 look){ return Shot(name,pos,(look-pos).normalized,Vector3.up,55f); }
        private static string CamTop(string name,float cx,float cz,float alt){ var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain t=terr.Length>0?terr[0]:null;
            float g=t!=null?t.transform.position.y+t.SampleHeight(new Vector3(cx,0,cz)):0f; return Shot(name,new Vector3(cx,g+alt,cz),Vector3.down,Vector3.forward,60f); }
        private static string Shot(string name,Vector3 pos,Vector3 fwd,Vector3 up,float fov)
        {
            const int W=1280,H=720; var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
            var camGO=new GameObject("__c"); var cam=camGO.AddComponent<Camera>(); cam.clearFlags=CameraClearFlags.Skybox; cam.fieldOfView=fov; cam.nearClipPlane=0.05f; cam.farClipPlane=3000f; cam.enabled=false;
            cam.transform.position=pos; cam.transform.rotation=Quaternion.LookRotation(fwd,up);
            string fn=Path.Combine(RenderDir,name+".png");
            try{ var req=new UniversalRenderPipeline.SingleCameraRequest(); if(RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req);} else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null; File.WriteAllBytes(fn,tex.EncodeToPNG()); }
            finally{ Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
            return fn;
        }
    }
}
