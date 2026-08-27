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
    /// [레이아웃 5b·남북축] 마을 블록 이동(−X 되돌리고 −Z) + 외삼문/동헌 남향 배치 + 평탄화 + 채별접지 + 검증 + 렌더.
    /// 축 = X=462(다리축). 동헌 정면 = 현판(使無堂 · MI_R_Sign_PHD) 쪽(계단 아님). 외삼문 = MI_R_Sign_OSM.
    /// ★개천 링·수면·물머티리얼·다리 절대 미변경(측정만). 링 여유<10m면 저장전 중단.
    /// </summary>
    public static class SeocheonLayout
    {
        private const string Scene   = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string BakTerr = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain_backup_predonheon.asset";
        private const string DonPrefab="Assets/_Project/Seocheon/Prefabs/Donheon.prefab";
        private const string OisPrefab="Assets/_Project/Seocheon/Prefabs/Oisamun.prefab";
        private const string DonFbx  = "Assets/_Project/Seocheon/Art/Models/Donheon_Quest3_v11.fbx";
        private const string OisFbx  = "Assets/_Project/Seocheon/Art/Models/SM_Oisamun.fbx";
        private const string PathFile= @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const string RenderDir=@"C:\Users\User\_AssetBackup\render_layout2";
        private const string TxtPath = "Assets/_Project/Seocheon/Data/village_layout2_report.txt";
        private const float CX=463f, CZ=678f, RingBand=11.8f, AxisX=462f;
        private const float ORIG_MAXX=510.06f;   // 5b −X(Δ−24.06) 되돌림 목표
        private const float VILL_MAXZ=694f, PLAZA=18f, COURT=10f;
        private const float OIS_DEPTH=8.33f, DON_DEPTH=11.34f, DON_GROUND=0.70f;

        [MenuItem("Tools/Seocheon/Village/5b. Block Move NS + Face-off")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 5b Block Move NS (X=462) =====");
            if(AssetDatabase.LoadAssetAtPath<GameObject>(OisPrefab)==null){ EditorUtility.DisplayDialog("Seocheon","Oisamun.prefab 없음. 5a2 먼저.","확인"); return; }

            var scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var terrs=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terrs.Length>0?terrs[0]:null;
            var td=terrain.terrainData; int Rr=td.heightmapResolution; Vector3 S=td.size; float tY=terrain.transform.position.y,tX0=terrain.transform.position.x,tZ0=terrain.transform.position.z; float texX=S.x/(Rr-1),texZ=S.z/(Rr-1);

            // [2] 지형 원복(높이) + 기존 동헌/외삼문 제거
            var bak=AssetDatabase.LoadAssetAtPath<TerrainData>(BakTerr);
            if(bak!=null && bak.heightmapResolution==Rr){ td.SetHeights(0,0,bak.GetHeights(0,0,Rr,Rr)); EditorUtility.SetDirty(td); terrain.Flush(); }
            var Hn=td.GetHeights(0,0,Rr,Rr);
            float H(float x,float z){ float u=(x-tX0)/S.x,v=(z-tZ0)/S.z; float fx=Mathf.Clamp(u*(Rr-1),0,Rr-1),fz=Mathf.Clamp(v*(Rr-1),0,Rr-1);
                int x0=(int)fx,z0=(int)fz,x1=Mathf.Min(x0+1,Rr-1),z1=Mathf.Min(z0+1,Rr-1); float tx=fx-x0,tz=fz-z0;
                return tY+Mathf.Lerp(Mathf.Lerp(Hn[z0,x0],Hn[z0,x1],tx),Mathf.Lerp(Hn[z1,x0],Hn[z1,x1],tx),tz)*S.y; }
            foreach(var r in scene.GetRootGameObjects()) if(r.name=="_Donheon"||r.name=="_Oisamun") Object.DestroyImmediate(r);
            sb.AppendLine($"[2] 지형 원복(predonheon) {(bak!=null?"✓":"⚠")} · 기존 _Donheon/_Oisamun 제거");

            // [3] 마을: X 되돌림 + −Z 이동 (idempotent)
            var holder=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="_Village");
            if(holder==null){ EditorUtility.DisplayDialog("Seocheon","_Village 없음.","확인"); return; }
            var rth=LoadRth();
            float curMaxX=-1e9f, curMaxZ=-1e9f, curMinZ=1e9f;
            foreach(Transform ch in holder.transform){ var rn=ch.GetComponentsInChildren<Renderer>(true); if(rn.Length==0) continue; Bounds b=rn[0].bounds; foreach(var r in rn) b.Encapsulate(r.bounds); curMaxX=Mathf.Max(curMaxX,b.max.x); curMaxZ=Mathf.Max(curMaxZ,b.max.z); curMinZ=Mathf.Min(curMinZ,b.min.z); }
            float dX=ORIG_MAXX-curMaxX;       // −X 되돌림
            float dZ=VILL_MAXZ-(curMaxZ);     // maxZ→694
            foreach(Transform ch in holder.transform){ var p=ch.position; float nx=p.x+dX, nz=p.z+dZ; ch.position=new Vector3(nx, H(nx,nz), nz); }
            // 새 범위
            float vMaxZ=-1e9f, vMinZ=1e9f, vMinX=1e9f, vMaxX=-1e9f;
            foreach(Transform ch in holder.transform){ var rn=ch.GetComponentsInChildren<Renderer>(true); if(rn.Length==0) continue; Bounds b=rn[0].bounds; foreach(var r in rn) b.Encapsulate(r.bounds); vMaxZ=Mathf.Max(vMaxZ,b.max.z); vMinZ=Mathf.Min(vMinZ,b.min.z); vMinX=Mathf.Min(vMinX,b.min.x); vMaxX=Mathf.Max(vMaxX,b.max.x); }
            sb.AppendLine($"[3] 마을 이동 Δx={dX:+0.00}(되돌림) Δz={dZ:+0.00} → Z {vMinZ:F1}~{vMaxZ:F1}, X {vMinX:F1}~{vMaxX:F1} · 상대위치·회전·담장 불변");

            // 정면 측정 (현판 기준)
            Vector2 oisF=FrontDir(OisPrefab,OisFbx,"Sign_OSM",out bool oF,out string oMat);
            Vector2 donF=FrontDir(DonPrefab,DonFbx,"Sign_PHD",out bool dF,out string dMat);
            if(!dF){ donF=FrontDir(DonPrefab,DonFbx,"Sign",out dF,out dMat); } // 폴백: 임의 현판
            float oisRotY=PickRotYtoNegZ(oisF), donRotY=PickRotYtoNegZ(donF);
            sb.AppendLine($"[3] 외삼문 정면=재질 '{oMat}' 로컬dir({oisF.x:+0.0;-0.0},{oisF.y:+0.0;-0.0}) {(oF?"":"⚠미검출")} → rotY {oisRotY:F0}(남향)");
            sb.AppendLine($"[3] 동헌 정면=현판 재질 '{dMat}' 로컬dir({donF.x:+0.0;-0.0},{donF.y:+0.0;-0.0}) {(dF?"":"⚠미검출-계단無근거")} → rotY {donRotY:F0}(남향) · 근거: 사무당 현판(계단 아님)");

            // 좌표(남북)
            float oisSouthZ=VILL_MAXZ+PLAZA;              // 712
            float oisCenZ=oisSouthZ+OIS_DEPTH/2f;         // 716.17
            float oisNorthZ=oisSouthZ+OIS_DEPTH;          // 720.33
            float donFrontZ=oisNorthZ+COURT;              // 730.33
            float donCenZ=donFrontZ+DON_DEPTH/2f;         // 736.0
            float donNorthZ=donFrontZ+DON_DEPTH;          // 741.67

            // ── 게이트: 링 밴드 안쪽 경계까지 여유 ──
            float ringN=RingRadAt(rth,90f), ringS=RingRadAt(rth,-90f);
            float clrN=(ringN-RingBand)-(donNorthZ-CZ);   // 동헌 북단 → 북측 링밴드 안쪽
            float clrS=(ringS-RingBand)-(CZ-vMinZ);       // 마을 남단 → 남측 링밴드 안쪽
            sb.AppendLine($"[3] ★링 여유(밴드 안쪽까지): 북 {clrN:F1}m (링r {ringN:F1}, 동헌북단 {donNorthZ:F1}) · 남 {clrS:F1}m (링r {ringS:F1}, 마을남단 {vMinZ:F1})");
            if(clrN<10f){ sb.AppendLine($"★★ 중단: 북측 링 여유 {clrN:F1}m < 10m. 저장 안 함. 광장/내정 축소 요망.");
                File.WriteAllText(Path.GetFullPath(TxtPath), sb.ToString(), new UTF8Encoding(true)); AssetDatabase.ImportAsset(TxtPath); Debug.Log(sb.ToString());
                EditorUtility.DisplayDialog("Seocheon", $"★중단: 북 링여유 {clrN:F1}m<10m. 저장 안 함.","확인"); return; }

            // [4] 평탄화(공통 padY, 링밴드 제외)
            float padY=H(AxisX,donCenZ);
            int flatD=Flatten(Hn,S,tY,tX0,tZ0,texX,texZ,Rr, AxisX,donCenZ, 12f,9f,8f, padY, rth); // 동헌 반폭X12 반깊이Z9
            int flatO=Flatten(Hn,S,tY,tX0,tZ0,texX,texZ,Rr, AxisX,oisCenZ, 11f,6f,6f, padY, rth); // 외삼문 반폭X11 반깊이Z6

            // 배치
            var ois=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(OisPrefab)); var oh=new GameObject("_Oisamun"); ois.transform.SetParent(oh.transform);
            ois.transform.rotation=Quaternion.Euler(0,oisRotY,0);
            var don=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DonPrefab)); var dh=new GameObject("_Donheon"); don.transform.SetParent(dh.transform);
            don.transform.rotation=Quaternion.Euler(0,donRotY,0);

            // [5] 채별 접지 보정 (footprint 최저값에 밑면; 편차>0.30 → footprint 평탄화+3m)
            int gFix=0, gFlat=0;
            foreach(Transform ch in holder.transform){ var rn=ch.GetComponentsInChildren<Renderer>(true); if(rn.Length==0) continue; Bounds b=rn[0].bounds; foreach(var r in rn) b.Encapsulate(r.bounds);
                float hx=b.size.x*0.5f, hz=b.size.z*0.5f, cx=b.center.x, cz=b.center.z;
                var samp=new List<float>{ H(cx,cz), H(cx-hx,cz-hz),H(cx+hx,cz-hz),H(cx-hx,cz+hz),H(cx+hx,cz+hz) };
                float minT=samp.Min(), maxT=samp.Max();
                float dy=minT-b.min.y; ch.position+=new Vector3(0,dy,0); gFix++;
                if(maxT-minT>0.30f){ Flatten(Hn,S,tY,tX0,tZ0,texX,texZ,Rr, cx,cz, hx*0.9f,hz*0.9f,3f, minT, rth); gFlat++; }
            }
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); terrain.Flush();

            // 외삼문·동헌 최종 Y (평탄화 후)
            ois.transform.position=new Vector3(AxisX, H(AxisX,oisCenZ), oisCenZ); SetStaticRec(oh);
            don.transform.position=new Vector3(AxisX, H(AxisX,donCenZ)+DON_GROUND, donCenZ); SetStaticRec(dh);
            SetStaticRec(holder);
            float plazaMid=H(AxisX,(VILL_MAXZ+oisSouthZ)/2f), courtMid=H(AxisX,(oisNorthZ+donFrontZ)/2f);
            sb.AppendLine($"[4] padY {padY:F2} · 동헌 {flatD}텍셀 · 외삼문 {flatO}텍셀 · 광장중앙 Δ{plazaMid-padY:+0.00} · 내정중앙 Δ{courtMid-padY:+0.00} {(Mathf.Abs(courtMid-padY)<=0.1f?"✓":"⚠")}");
            sb.AppendLine($"[3] 외삼문 ({AxisX:F0},{oisCenZ:F1}) rotY{oisRotY:F0} · 동헌 ({AxisX:F0},{donCenZ:F1}) rotY{donRotY:F0} Y=pad+0.70");
            sb.AppendLine($"[5] 접지보정: 채 {gFix}개 밑면 최저값 정렬 · footprint 평탄화 {gFlat}개(편차>0.30)");

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, Scene);

            // [6] 검증
            int sceneTris=0; foreach(var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) if(mf.sharedMesh!=null) for(int s=0;s<mf.sharedMesh.subMeshCount;s++) sceneTris+=(int)(mf.sharedMesh.GetIndexCount(s)/3);
            long texMem=0; foreach(var g in AssetDatabase.FindAssets("t:Texture2D", new[]{"Assets/_Project/Seocheon/Art/Textures"})){ var tex=AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(g)); if(tex!=null) texMem+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex); }
            var ringViol=new List<string>();
            foreach(Transform ch in holder.transform){ var rn=ch.GetComponentsInChildren<Renderer>(true); if(rn.Length==0) continue; Bounds b=rn[0].bounds; foreach(var r in rn) b.Encapsulate(r.bounds); if(RingHit(Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z),rth)) ringViol.Add(ch.name); }
            sb.AppendLine($"\n[6] 씬 총 tris {sceneTris} · 텍스처메모리 {texMem/1048576f:F1}MB · 링밴드 침범 {(ringViol.Count==0?"없음 ✓":string.Join(",",ringViol))}");
            sb.AppendLine($"[6] 링 여유 4방위 r(θ): E {RingRadAt(rth,0):F1} / W {RingRadAt(rth,180):F1} / S {RingRadAt(rth,-90):F1} / N {RingRadAt(rth,90):F1}");
            sb.AppendLine("[6] 밑면-지형 틈>0.15m:");
            int gc=0; gc+=GapReport(sb,don,H); gc+=GapReport(sb,ois,H); foreach(Transform ch in holder.transform) gc+=GapReport(sb,ch.gameObject,H);
            if(gc==0) sb.AppendLine("     없음 ✓");
            sb.AppendLine("[6] 축선(X=462):");
            sb.AppendLine($"     다리북단 Z571 → 마을남단 Z{vMinZ:F1} → 마을북단 Z{vMaxZ:F1} → 광장 → 외삼문중심 Z{oisCenZ:F1} → 동헌정면 Z{donFrontZ:F1} → 동헌중심 Z{donCenZ:F1}");
            sb.AppendLine($"     구간: 다리→마을남단 {vMinZ-571f:F1}m · 마을깊이 {vMaxZ-vMinZ:F1}m · 마을북단→외삼문남면 {PLAZA}m · 외삼문깊이 {OIS_DEPTH}m · 내정 {COURT}m");

            // [7] 렌더
            Directory.CreateDirectory(RenderDir); var files=new List<string>(); string rerr=null;
            float midDon=H(AxisX,donCenZ)+4f, midOis=H(AxisX,oisCenZ)+4f;
            try{
                files.Add(CamTop("Aerial_300", AxisX,690f,300f));
                files.Add(Cam("OnBridge",      new Vector3(AxisX,H(AxisX,571)+1.6f,571), new Vector3(AxisX,midDon,donCenZ)));
                files.Add(Cam("Plaza_south",   new Vector3(AxisX,H(AxisX,696)+1.6f,696), new Vector3(AxisX,midDon,donCenZ)));
                files.Add(Cam("Oisamun_front", new Vector3(AxisX,H(AxisX,oisSouthZ-12f)+1.6f,oisSouthZ-12f), new Vector3(AxisX,midOis,oisCenZ)));
                files.Add(Cam("Through_gate",  new Vector3(AxisX,H(AxisX,oisCenZ)+1.6f,oisCenZ), new Vector3(AxisX,H(AxisX,donCenZ)+3f,donCenZ)));
                files.Add(Cam("Street_eye",    new Vector3(AxisX-6f,H(AxisX-6f,(vMinZ+vMaxZ)/2f)+1.6f,(vMinZ+vMaxZ)/2f), new Vector3(AxisX,H(AxisX,vMaxZ)+3f,vMaxZ)));
            }catch(System.Exception e){ rerr=e.ToString(); }
            if(rerr!=null) sb.AppendLine("\n[7] 렌더 실패: "+rerr); else { sb.AppendLine($"\n[7] 렌더 {files.Count}장 → {RenderDir}"); foreach(var f in files) sb.AppendLine("   "+f); }
            sb.AppendLine("\n※ 흙 스플랫은 다음 단계. 남북축·마주보기·문틀정렬 확인 후.");

            File.WriteAllText(Path.GetFullPath(TxtPath), sb.ToString(), new UTF8Encoding(true)); AssetDatabase.ImportAsset(TxtPath);
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"5b(남북) 완료. Δz{dZ:F1} · 외삼문 rotY{oisRotY:F0} · 동헌 rotY{donRotY:F0}(현판)\n북링여유 {clrN:F0}m · 접지보정 {gFix}채\n렌더 6 → render_layout2. 확인 후 흙.","확인");
        }

        private static Vector2 FrontDir(string prefab, string fbx, string matContains, out bool found, out string matName){ found=false; matName="";
            var mi=(ModelImporter)AssetImporter.GetAtPath(fbx); bool was=mi.isReadable; if(!was){ mi.isReadable=true; mi.SaveAndReimport(); }
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            Vector3 allC=Vector3.zero; float allA=0, subA=0; Vector3 subC=Vector3.zero;
            foreach(var mr in go.GetComponentsInChildren<MeshRenderer>(true)){ var mf=mr.GetComponent<MeshFilter>(); if(mf==null||mf.sharedMesh==null) continue; var m=mf.sharedMesh; Vector3[] vs; try{ vs=m.vertices; }catch{ continue; } var mats=mr.sharedMaterials; var tr=mr.transform;
                for(int s=0;s<m.subMeshCount && s<mats.Length;s++){ var tri=m.GetTriangles(s); bool isSub=mats[s]!=null && mats[s].name.ToLowerInvariant().Contains(matContains.ToLowerInvariant()); if(isSub&&mats[s]!=null) matName=mats[s].name;
                    for(int t=0;t<tri.Length;t+=3){ Vector3 a=tr.TransformPoint(vs[tri[t]]),b=tr.TransformPoint(vs[tri[t+1]]),c=tr.TransformPoint(vs[tri[t+2]]); float ar=0.5f*Vector3.Cross(b-a,c-a).magnitude; Vector3 ct=(a+b+c)/3f;
                        allC+=ct*ar; allA+=ar; if(isSub){ subC+=ct*ar; subA+=ar; } } } }
            if(!was){ mi.isReadable=false; mi.SaveAndReimport(); }
            if(allA<=0||subA<=0) return Vector2.zero; found=true; Vector3 g=allC/allA, sgc=subC/subA; return new Vector2(sgc.x-g.x, sgc.z-g.z);
        }
        private static float PickRotYtoNegZ(Vector2 f){ if(f.sqrMagnitude<1e-6f) return 0f; float best=-1e9f,br=0; foreach(float ry in new[]{0f,90f,180f,270f}){ Vector3 w=Quaternion.Euler(0,ry,0)*new Vector3(f.x,0,f.y); float d=Vector3.Dot(w.normalized,Vector3.back); if(d>best){best=d;br=ry;} } return br; }

        private static int Flatten(float[,] Hn, Vector3 S, float tY,float tX0,float tZ0,float texX,float texZ,int Rr, float cx,float cz, float hx,float hz,float skirt, float padY, List<Vector2> rth){
            float padN=Mathf.Clamp01((padY-tY)/S.y); int n=0;
            int xMin=Mathf.Clamp(Mathf.FloorToInt((cx-tX0-hx-skirt)/texX),0,Rr-1), xMax=Mathf.Clamp(Mathf.CeilToInt((cx-tX0+hx+skirt)/texX),0,Rr-1);
            int zMin=Mathf.Clamp(Mathf.FloorToInt((cz-tZ0-hz-skirt)/texZ),0,Rr-1), zMax=Mathf.Clamp(Mathf.CeilToInt((cz-tZ0+hz+skirt)/texZ),0,Rr-1);
            for(int hzz=zMin;hzz<=zMax;hzz++) for(int hxx=xMin;hxx<=xMax;hxx++){ float x=tX0+hxx*texX,z=tZ0+hzz*texZ;
                float rr=Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ)); float rr2=RingRadAt(rth,Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg); if(Mathf.Abs(rr-rr2)<=RingBand) continue;
                float ox=Mathf.Max(0,Mathf.Abs(x-cx)-hx), oz=Mathf.Max(0,Mathf.Abs(z-cz)-hz); float dd=Mathf.Sqrt(ox*ox+oz*oz);
                if(dd<=0.001f){ Hn[hzz,hxx]=padN; n++; } else if(dd<=skirt){ float tt=Mathf.SmoothStep(0,1,dd/skirt); Hn[hzz,hxx]=Mathf.Lerp(padN,Hn[hzz,hxx],tt); } }
            return n;
        }
        private static int GapReport(StringBuilder sb, GameObject go, System.Func<float,float,float> H){ var rn=go.GetComponentsInChildren<Renderer>(true); if(rn.Length==0) return 0; Bounds b=rn[0].bounds; foreach(var r in rn) b.Encapsulate(r.bounds);
            float bottom=b.min.y, maxGap=0,maxEmbed=0; float hx=b.size.x*0.4f,hz=b.size.z*0.4f,cx=b.center.x,cz=b.center.z;
            for(int a=-1;a<=1;a++) for(int c=-1;c<=1;c++){ float dh=H(cx+a*hx,cz+c*hz)-bottom; if(dh>maxEmbed)maxEmbed=dh; if(-dh>maxGap)maxGap=-dh; }
            if(maxGap>0.15f){ sb.AppendLine($"     {go.name}: 틈 {maxGap:F2}m · 파묻힘 {maxEmbed:F2}m"); return 1; } return 0; }
        private static void SetStaticRec(GameObject g){ g.isStatic=true; foreach(Transform c in g.transform) SetStaticRec(c.gameObject); }
        private static List<Vector2> LoadRth(){ var list=new List<Vector2>(); if(!File.Exists(PathFile)) return list; var ci=CultureInfo.InvariantCulture; var lines=File.ReadAllLines(PathFile);
            for(int i=1;i<lines.Length;i++){ var t=lines[i].Split(' '); if(t.Length<2) continue; float x=float.Parse(t[0],ci),z=float.Parse(t[1],ci); list.Add(new Vector2(Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg, Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ)))); } return list; }
        private static float RingRadAt(List<Vector2> rth, float ang){ if(rth.Count==0) return 100f; float best=1e9f,br=100f; foreach(var v in rth){ float d=Mathf.Abs(Mathf.DeltaAngle(v.x,ang)); if(d<best){best=d;br=v.y;} } return br; }
        private static bool RingHit(Rect xz, List<Vector2> rth){ foreach(var p in new[]{ new Vector2(xz.center.x,xz.center.y), new Vector2(xz.xMin,xz.yMin),new Vector2(xz.xMax,xz.yMin),new Vector2(xz.xMin,xz.yMax),new Vector2(xz.xMax,xz.yMax) }){ float d=Vector2.Distance(p,new Vector2(CX,CZ)); float r=RingRadAt(rth, Mathf.Atan2(p.y-CZ,p.x-CX)*Mathf.Rad2Deg); if(Mathf.Abs(d-r)<=RingBand) return true; } return false; }
        private static string Cam(string name,Vector3 pos,Vector3 look){ return Shot(name,pos,(look-pos).normalized,Vector3.up,60f); }
        private static string CamTop(string name,float cx,float cz,float alt){ var terr=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain t=terr.Length>0?terr[0]:null; float g=t!=null?t.transform.position.y+t.SampleHeight(new Vector3(cx,0,cz)):0f; return Shot(name,new Vector3(cx,g+alt,cz),Vector3.down,Vector3.forward,70f); }
        private static string Shot(string name,Vector3 pos,Vector3 fwd,Vector3 up,float fov){
            const int W=1600,H=900; var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
            var camGO=new GameObject("__c"); var cam=camGO.AddComponent<Camera>(); cam.clearFlags=CameraClearFlags.Skybox; cam.fieldOfView=fov; cam.nearClipPlane=0.05f; cam.farClipPlane=4000f; cam.enabled=false;
            cam.transform.position=pos; cam.transform.rotation=Quaternion.LookRotation(fwd,up); string fn=Path.Combine(RenderDir,name+".png");
            try{ var req=new UniversalRenderPipeline.SingleCameraRequest(); if(RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req);} else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null; File.WriteAllBytes(fn,tex.EncodeToPNG()); }
            finally{ Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
            return fn;
        }
    }
}
