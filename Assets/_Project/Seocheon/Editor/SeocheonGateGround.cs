using System.Collections.Generic;
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
    /// [14] 관아 마당 지형 스플랫: 14a 리포트 / 14b 평탄화(백업) / 14c 흙칠(펄린 페더) / 14d 렌더.
    /// 좌표는 세트 부모(GwanaGateSet) 로컬 프레임 기준(회전 지원). ★새 메시·프롭 금지, 지형만.
    /// </summary>
    public static class SeocheonGateGround
    {
        private const string Scene = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string SetName = "GwanaGateSet";
        private const string RenderDir = @"C:\Users\User\관아\_renders\ground";
        private const string BackupBin = @"C:\Users\User\관아\_backup\gate_heightmap_backup.bin";
        private const string TerrainLayerDir = "Assets/_Project/Seocheon/Art/Terrain";
        private const string DirtPrefKey = "Seocheon.Gate.DirtLayerIndex";
        private const int DirtLayerOverride = -1;   // ≥0 이면 강제 지정

        // 로컬 프레임(GwanaGateSet 기준) 마당/광장 사각형
        private const float LocXhalf = 16.95f;      // 담장 내측 반폭
        private const float LocZ0 = -4.17f;         // 남담 라인
        private const float LocZ1 = 33.27f;         // 북담 내측
        private const float GateSouth = -7.924f;    // 외삼문 남면
        private const float PlazaLen = 18f;         // 게이트 남쪽 광장
        private const float DirtBite = 0.3f;        // 담장 밑으로 흙 물림
        private const float FlatBlend = 3f;         // 평탄 경계 밖 블렌드
        private const float PaintFeather = 2f;      // 흙칠 페더
        private static float PlazaZ0 => GateSouth - PlazaLen;   // -25.924

        private static readonly string[] DirtKeys = {"황토","흙","토","dirt","earth","soil","loess","mud","ground","path","clay"};

        // [15] 접지 평탄화
        private const float StreamMinClear = 3f;    // 15a: 개천-footprint 최단거리 이 미만이면 중단
        private const float StreamKeep = 2f;        // 15b: 개천에서 이만큼 남기고 평탄 정지
        private const bool StreamCheckOverride = false;  // true = 개천 판정 무시 강제진행(비권장)
        private const string FKeyTargetY = "Seocheon.Gate.Flatten.TargetY";
        private const string FKeyOK = "Seocheon.Gate.Flatten.OK";
        private static readonly string[] SixNames = {"SM_Oisamun","Wall_S_L","Wall_S_R","Wall_E","Wall_W","Wall_N"};

        // ─────────────────────────── 14a ───────────────────────────
        [MenuItem("Tools/Seocheon/Ground/14a. Report (read-only)")]
        public static void Report()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 14a Ground Report =====");
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            if(!GetTerrain(out var terrain, out var td, sb)) { Debug.Log(sb.ToString()); return; }

            // 레이어
            var layers=td.terrainLayers;
            sb.AppendLine($"[레이어] {layers.Length}개 {(layers.Length>4?"⚠ 4개 초과":"")}");
            int dirtIdx=-1;
            for(int i=0;i<layers.Length;i++){ var l=layers[i];
                string dn=l!=null?l.name:"(null)"; string tex=l!=null&&l.diffuseTexture!=null?l.diffuseTexture.name:"(none)";
                Vector2 sz=l!=null?l.tileSize:Vector2.zero;
                bool isDirt = l!=null && DirtKeys.Any(k=>(l.name??"").ToLowerInvariant().Contains(k.ToLowerInvariant()) || (tex??"").ToLowerInvariant().Contains(k.ToLowerInvariant()));
                if(isDirt && dirtIdx<0) dirtIdx=i;
                sb.AppendLine($"   [{i}] '{dn}' · diffuse '{tex}' · tileSize {sz.x}x{sz.y}{(isDirt?"  ← 황토/흙 계열":"")}"); }
            if(dirtIdx>=0){ EditorPrefs.SetInt(DirtPrefKey, dirtIdx); sb.AppendLine($"[레이어] 흙 레이어 인덱스 = {dirtIdx} (14c가 이걸 사용)"); }
            else { EditorPrefs.SetInt(DirtPrefKey, -1); sb.AppendLine("[레이어] 흙 계열 없음 → 14c 실행 시 새로 생성"); }

            sb.AppendLine($"[해상도] alphamap {td.alphamapResolution} · heightmap {td.heightmapResolution} · terrainData size {td.size.x}×{td.size.z} (높이 {td.size.y}) · 원점 {V(terrain.transform.position)}");

            // 세트 부모
            var set=FindSet();
            if(set==null) sb.AppendLine($"[세트] '{SetName}' 못 찾음 — 13f 먼저(임시로 원점 462/589.66 사용).");
            else sb.AppendLine($"[세트] '{set.name}' pos {V(set.position)} rot {V(set.eulerAngles)} scale {V(set.localScale)}");

            // 지형 고도 편차 (담장 사각형 / 광장)
            float setY = set!=null?set.position.y:SampleH(terrain,td,462f,589.66f);
            var rectDev = RegionDev(terrain, td, set, -LocXhalf, LocXhalf, LocZ0, LocZ1, setY);
            var plazaDev= RegionDev(terrain, td, set, -LocXhalf, LocXhalf, PlazaZ0, LocZ0, setY);
            sb.AppendLine($"[편차] 담장사각형(로컬 X±{LocXhalf}, Z {LocZ0}~{LocZ1}): 고도 {rectDev.mn:F2}~{rectDev.mx:F2} 평균 {rectDev.mean:F2} · 스프레드 {rectDev.mx-rectDev.mn:F2}m · 세트Y({setY:F2}) 대비 최대편차 {rectDev.maxAbs:F2}m");
            sb.AppendLine($"[편차] 광장(로컬 X±{LocXhalf}, Z {PlazaZ0:F1}~{LocZ0}): 고도 {plazaDev.mn:F2}~{plazaDev.mx:F2} 평균 {plazaDev.mean:F2} · 스프레드 {plazaDev.mx-plazaDev.mn:F2}m · 세트Y 대비 최대편차 {plazaDev.maxAbs:F2}m");
            sb.AppendLine($"→ 스프레드/편차 0.15m 초과면 14b 평탄화 권장.");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"14a 리포트. 레이어 {layers.Length}개·흙 idx {dirtIdx}\n담장사각형 스프레드 {rectDev.mx-rectDev.mn:F2}m. Console 확인.","확인");
        }

        // ─────────────────────────── 14b ───────────────────────────
        [MenuItem("Tools/Seocheon/Ground/14b. Flatten (backup first)")]
        public static void Flatten()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 14b Flatten =====");
            var scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            if(!GetTerrain(out var terrain, out var td, sb)) { Debug.Log(sb.ToString()); return; }
            var set=FindSet(); float setY = set!=null?set.position.y:SampleH(terrain,td,462f,589.66f);

            int Rr=td.heightmapResolution; Vector3 S=td.size;
            float tY=terrain.transform.position.y, tX0=terrain.transform.position.x, tZ0=terrain.transform.position.z;
            float texX=S.x/(Rr-1), texZ=S.z/(Rr-1);
            var Hn=td.GetHeights(0,0,Rr,Rr);

            // ★백업 (heightmap 파일 + TerrainData 에셋 1회)
            Directory.CreateDirectory(Path.GetDirectoryName(BackupBin));
            using(var bw=new BinaryWriter(File.Open(BackupBin, FileMode.Create))){
                bw.Write(Rr); for(int z=0;z<Rr;z++) for(int x=0;x<Rr;x++) bw.Write(Hn[z,x]); }
            string tdPath=AssetDatabase.GetAssetPath(td);
            string bakAsset = string.IsNullOrEmpty(tdPath)?null:Path.GetDirectoryName(tdPath).Replace("\\","/")+"/"+Path.GetFileNameWithoutExtension(tdPath)+"_backup_pregateground.asset";
            if(bakAsset!=null && AssetDatabase.LoadAssetAtPath<TerrainData>(bakAsset)==null){ AssetDatabase.CopyAsset(tdPath, bakAsset); sb.AppendLine($"[백업] TerrainData(pristine) → {bakAsset}"); }
            else if(bakAsset!=null) sb.AppendLine($"[백업] TerrainData 백업 이미 존재(원본 보존) → {bakAsset}");
            sb.AppendLine($"[백업] heightmap 파일 → {BackupBin} (14e Restore 로 복원)");

            float padN=Mathf.Clamp01((setY-tY)/S.y);
            int flat=0, blend=0;
            for(int hz=0;hz<Rr;hz++) for(int hx=0;hx<Rr;hx++){ float wx=tX0+hx*texX, wz=tZ0+hz*texZ;
                LocalXZ(set, wx, wz, out float lx, out float lz);
                float dd=RectOutsideDist(lx,lz,-LocXhalf,LocXhalf,PlazaZ0,LocZ1);
                if(dd<=0.0001f){ Hn[hz,hx]=padN; flat++; }
                else if(dd<=FlatBlend){ float t=Mathf.SmoothStep(0,1,dd/FlatBlend); Hn[hz,hx]=Mathf.Lerp(padN,Hn[hz,hx],t); blend++; } }
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); terrain.Flush();
            sb.AppendLine($"[평탄] 세트Y {setY:F2}m 로 평탄 {flat}텍셀 + 블렌드({FlatBlend}m) {blend}텍셀 (로컬 X±{LocXhalf}, Z {PlazaZ0:F1}~{LocZ1})");

            // 재측정
            var rectDev=RegionDev(terrain, td, set, -LocXhalf, LocXhalf, LocZ0, LocZ1, setY);
            var plazaDev=RegionDev(terrain, td, set, -LocXhalf, LocXhalf, PlazaZ0, LocZ0, setY);
            sb.AppendLine($"[재측정] 담장사각형 스프레드 {rectDev.mx-rectDev.mn:F3}m · 세트Y대비 최대편차 {rectDev.maxAbs:F3}m {(rectDev.maxAbs<0.02f?"✓":"")}");
            sb.AppendLine($"[재측정] 광장 스프레드 {plazaDev.mx-plazaDev.mn:F3}m · 세트Y대비 최대편차 {plazaDev.maxAbs:F3}m {(plazaDev.maxAbs<0.02f?"✓":"")}");

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, Scene);
            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"14b 평탄화 완료. 편차 {rectDev.maxAbs:F3}m.\n백업 저장됨(14e Restore 가능). 다음 14c.","확인");
        }

        [MenuItem("Tools/Seocheon/Ground/14e. Restore heightmap backup")]
        public static void Restore()
        {
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 14e Restore heightmap =====");
            var scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            if(!GetTerrain(out var terrain, out var td, sb)) { Debug.Log(sb.ToString()); return; }
            if(!File.Exists(BackupBin)){ sb.AppendLine("백업 파일 없음: "+BackupBin); Debug.Log(sb.ToString()); EditorUtility.DisplayDialog("Seocheon","백업 없음.","확인"); return; }
            int Rr=td.heightmapResolution;
            using(var br=new BinaryReader(File.Open(BackupBin, FileMode.Open))){ int r=br.ReadInt32();
                if(r!=Rr){ sb.AppendLine($"⚠ 해상도 불일치 {r} vs {Rr} — 중단."); Debug.Log(sb.ToString()); return; }
                var Hn=new float[Rr,Rr]; for(int z=0;z<Rr;z++) for(int x=0;x<Rr;x++) Hn[z,x]=br.ReadSingle();
                td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); terrain.Flush(); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, Scene);
            sb.AppendLine("heightmap 복원 완료.");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon","heightmap 백업 복원 완료.","확인");
        }

        // ─────────────────────────── 14c ───────────────────────────
        [MenuItem("Tools/Seocheon/Ground/14c. Paint dirt (perlin feather)")]
        public static void Paint()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 14c Paint dirt =====");
            var scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            if(!GetTerrain(out var terrain, out var td, sb)) { Debug.Log(sb.ToString()); return; }
            var set=FindSet();

            // 흙 레이어 인덱스 결정
            int dirt = DirtLayerOverride>=0 ? DirtLayerOverride : EditorPrefs.GetInt(DirtPrefKey, -1);
            var layers=new List<TerrainLayer>(td.terrainLayers);
            if(dirt<0 || dirt>=layers.Count){
                var newLayer=CreateDirtLayer(sb);
                layers.Add(newLayer); td.terrainLayers=layers.ToArray(); dirt=layers.Count-1;
                sb.AppendLine($"[흙] 새 레이어 생성 → 인덱스 {dirt}");
            } else sb.AppendLine($"[흙] 기존 레이어 인덱스 {dirt} 사용 ('{layers[dirt].name}')");

            int A=td.alphamapResolution; int L=td.terrainLayers.Length; Vector3 S=td.size;
            float tX0=terrain.transform.position.x, tZ0=terrain.transform.position.z;
            var maps=td.GetAlphamaps(0,0,A,A);   // [z,x,layer]
            // 흙칠 사각형(담장 밑 0.3m 물림) + 광장
            float px0=-LocXhalf-DirtBite, px1=LocXhalf+DirtBite, pz0=PlazaZ0, pz1=LocZ1+DirtBite;
            int painted=0;
            for(int az=0; az<A; az++) for(int ax=0; ax<A; ax++){
                float wx=tX0+((ax+0.5f)/A)*S.x, wz=tZ0+((az+0.5f)/A)*S.z;
                LocalXZ(set, wx, wz, out float lx, out float lz);
                float dd=RectOutsideDist(lx,lz,px0,px1,pz0,pz1);
                // 펄린으로 경계 흐트러뜨림: 유효거리 = dd − noise*amp
                float n=(Mathf.PerlinNoise(wx*0.18f+13.7f, wz*0.18f+7.3f)-0.5f)*2f; // -1..1
                float eff=dd - n*1.0f;   // ±1m 흔들림
                float w = dd<=0.0001f ? 1f : Mathf.Clamp01(1f - Mathf.SmoothStep(0f, PaintFeather, eff));
                if(w<=0.0001f) continue;
                float oldDirt=maps[az,ax,dirt];
                float newDirt=Mathf.Max(oldDirt, w);
                float otherSum=0f; for(int l=0;l<L;l++) if(l!=dirt) otherSum+=maps[az,ax,l];
                float scale = otherSum>1e-5f ? (1f-newDirt)/otherSum : 0f;
                for(int l=0;l<L;l++) maps[az,ax,l] = (l==dirt)? newDirt : maps[az,ax,l]*scale;
                painted++;
            }
            td.SetAlphamaps(0,0,maps); EditorUtility.SetDirty(td); terrain.Flush();
            sb.AppendLine($"[흙칠] 사각형(로컬 X±{px1:F2}, Z {pz0:F1}~{pz1:F2}) + 페더 {PaintFeather}m(펄린 ±1m) · {painted} 알파텍셀 · 정규화(합=1)");
            sb.AppendLine($"[흙칠] 담장 밑 {DirtBite}m 물림 · alphamap {A}px, 레이어 {L}개");

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, Scene);
            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"14c 흙칠 완료. 흙 idx {dirt}·{painted}텍셀.\n다음 14d 렌더.","확인");
        }

        // ─────────────────────────── 14d ───────────────────────────
        [MenuItem("Tools/Seocheon/Ground/14d. Render (eye 1.6m)")]
        public static void Render()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 14d Ground Render =====");
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            if(!GetTerrain(out var terrain, out var td, sb)) { Debug.Log(sb.ToString()); return; }
            var set=FindSet();
            Vector3 O = set!=null?set.position:new Vector3(462f, SampleH(terrain,td,462f,589.66f), 589.66f);
            float gy=O.y, eye=gy+1.6f;
            // 로컬→월드 헬퍼(회전 지원)
            Vector3 W(float lx,float ly,float lz)=> set!=null? set.TransformPoint(new Vector3(lx,ly,lz)) : O+new Vector3(lx,ly,lz);
            Directory.CreateDirectory(RenderDir);
            var files=new List<string>(); string err=null;
            try{
                files.Add(Shot("ground_A_plaza_to_gate", W(0,1.6f,GateSouth-12f), W(0,3f,GateSouth+1f), 55f));
                files.Add(Shot("ground_B_court_to_donheon", W(0,1.6f,4f), W(0,2f,26.345f), 60f));
                files.Add(Shot("ground_C_wallbase", W(9f,1.6f,16f), W(16.95f,0.15f,16f), 50f));   // 동담 밑 잔디 삐져나옴 확인
                files.Add(Shot("ground_D_aerial", W(0,42f,-14f), W(0,2f,14f), 55f));
            }catch(System.Exception e){ err=e.ToString(); }
            if(err!=null) sb.AppendLine("렌더 실패: "+err);
            else { sb.AppendLine($"렌더 4장 → {RenderDir}"); foreach(var f in files) sb.AppendLine("   "+f); }
            sb.AppendLine("판정: A 광장 흙·문 / B 마당 흙·동헌 / C 담장밑 잔디 안 삐져나옴 / D 마당형태·경계 자연스러움");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"14d 렌더 {files.Count}장 → {RenderDir}\nConsole 확인.","확인");
        }

        // ═══════════════════════════ [15] 접지 평탄화 ═══════════════════════════
        // ★절대: 세트·동헌·자식 transform 을 절대 수정하지 않는다(읽기 전용).

        [MenuItem("Tools/Seocheon/Ground/15a. Backup + Report (read-only)")]
        public static void F_Report()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 15a Flatten Backup+Report =====");
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            if(!GetTerrain(out var terrain, out var td, sb)){ Debug.Log(sb.ToString()); return; }
            var set=FindSet();
            if(set==null){ sb.AppendLine("★GwanaGateSet 없음 — 13f 먼저. 중단."); Debug.Log(sb.ToString()); EditorUtility.DisplayDialog("Seocheon","GwanaGateSet 없음.","확인"); return; }

            // 1) 백업
            string tdPath=AssetDatabase.GetAssetPath(td);
            string bak = string.IsNullOrEmpty(tdPath)?null:Path.GetDirectoryName(tdPath).Replace("\\","/")+"/"+Path.GetFileNameWithoutExtension(tdPath)+"_backup_preflatten.asset";
            if(bak!=null && AssetDatabase.LoadAssetAtPath<TerrainData>(bak)==null){ AssetDatabase.CopyAsset(tdPath, bak); sb.AppendLine($"[1] TerrainData 백업 → {bak}"); }
            else if(bak!=null) sb.AppendLine($"[1] TerrainData 백업 이미 존재(원본 보존) → {bak}");
            else sb.AppendLine("[1] ⚠ TerrainData 에셋 경로 불명 — 백업 실패(중단 권장)");

            // 2) 세트 6개 밑면 = 목표 높이 (읽기 전용)
            if(!SixBounds(set, out var wb, sb)){ Debug.Log(sb.ToString()); return; }
            float targetY=wb.min.y;
            sb.AppendLine($"[2] 세트6 월드 bbox center {V(wb.center)} size {V(wb.size)} · ★밑면(목표높이) Y {targetY:F3}");

            // 3) 로컬 축 (읽기 전용)
            sb.AppendLine($"[3] 세트 pos {V(set.position)} rot {V(set.eulerAngles)} scale {V(set.localScale)}");
            sb.AppendLine($"[3] forward {V(set.forward)} right {V(set.right)} (footprint 는 세트 로컬 축 기준)");

            // 4)+5) footprint 편차
            var dev=RegionDev(terrain, td, set, -LocXhalf, LocXhalf, PlazaZ0, LocZ1, targetY);
            sb.AppendLine($"[4] footprint(로컬 X±{LocXhalf}, Z {PlazaZ0:F1}~{LocZ1} = 담장사각형+현판쪽 광장 {PlazaLen}m) · 블렌드링 {FlatBlend}m");
            sb.AppendLine($"[5] 지형 고도 {dev.mn:F2}~{dev.mx:F2} 평균 {dev.mean:F2} · 목표({targetY:F2}) 대비 최대편차 {dev.maxAbs:F3}m · 스프레드 {dev.mx-dev.mn:F3}m");

            // 6) 개천 간섭
            float sdist; bool sreadable; int scount;
            StreamNearest(set, terrain, out sdist, out sreadable, out scount, sb);
            bool blocked = !StreamCheckOverride && (scount>0) && (sdist < StreamMinClear);
            sb.AppendLine($"[6] 개천(_Stream/Water) {scount}개 · footprint 최단거리 {(scount>0?sdist.ToString("F2")+"m":"—")} {(sreadable?"(정점기준)":"(bounds기준·근사)")} {(blocked?"★ 3m 미만 → 15b 중단":(scount>0?"✓ 여유":"(개천 못찾음 — 수동확인)"))}");

            // 7) 동헌 밑면
            var don=FindDonUnder(set);
            if(don!=null){ var dr=don.GetComponentsInChildren<Renderer>(true); if(dr.Length>0){ Bounds db=dr[0].bounds; foreach(var r in dr) db.Encapsulate(r.bounds);
                float diff=db.min.y-targetY; sb.AppendLine($"[7] 동헌 '{don.name}' 밑면 Y {db.min.y:F3} · 목표대비 {diff:+0.000;-0.000}m (기단 −0.10 파묻힘 정상 → −0.10±여유면 OK)"); } }
            else sb.AppendLine("[7] 동헌 못 찾음(세트 하위에 없음).");

            // 상태 저장
            EditorPrefs.SetFloat(FKeyTargetY, targetY);
            EditorPrefs.SetInt(FKeyOK, blocked?0:1);
            sb.AppendLine(blocked? "\n→ ★개천 간섭으로 15b 실행 금지. 세트를 개천에서 더 떨어뜨리거나 광장 길이를 줄여야 함."
                                 : "\n→ 15a 통과. 15b 평탄화 진행 가능.");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"15a. 목표높이 {targetY:F2}·편차 {dev.maxAbs:F3}m·개천 {(scount>0?sdist.ToString("F1")+"m":"?")}\n{(blocked?"★개천간섭—15b 금지":"통과—15b 가능")}. Console 확인.","확인");
        }

        [MenuItem("Tools/Seocheon/Ground/15b. Flatten (only if 15a passed)")]
        public static void F_Flatten()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 15b Flatten =====");
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            if(!GetTerrain(out var terrain, out var td, sb)){ Debug.Log(sb.ToString()); return; }
            var set=FindSet(); if(set==null){ sb.AppendLine("★세트 없음."); Debug.Log(sb.ToString()); return; }
            if(EditorPrefs.GetInt(FKeyOK,-1)!=1){ sb.AppendLine("★15a 미통과/미실행 — 먼저 15a 실행(개천검사·백업)."); Debug.Log(sb.ToString()); EditorUtility.DisplayDialog("Seocheon","15a 먼저 실행/통과 필요.","확인"); return; }
            // 개천 재확인
            StreamNearest(set, terrain, out float sdist, out bool sr, out int scount, sb);
            if(!StreamCheckOverride && scount>0 && sdist<StreamMinClear){ sb.AppendLine($"★개천 {sdist:F2}m<{StreamMinClear} — 중단."); Debug.Log(sb.ToString()); EditorUtility.DisplayDialog("Seocheon","개천 간섭 — 중단.","확인"); return; }

            if(!SixBounds(set, out var wb, sb)){ Debug.Log(sb.ToString()); return; }
            float targetY=wb.min.y;
            int Rr=td.heightmapResolution; Vector3 S=td.size;
            float tY=terrain.transform.position.y, tX0=terrain.transform.position.x, tZ0=terrain.transform.position.z;
            float texX=S.x/(Rr-1), texZ=S.z/(Rr-1);
            var Hn=td.GetHeights(0,0,Rr,Rr);
            float padN=Mathf.Clamp01((targetY-tY)/S.y);
            var streamR=StreamRenderers();
            int flat=0,blend=0,kept=0;
            for(int hz=0;hz<Rr;hz++) for(int hx=0;hx<Rr;hx++){ float wx=tX0+hx*texX, wz=tZ0+hz*texZ;
                LocalXZ(set, wx, wz, out float lx, out float lz);
                float dd=RectOutsideDist(lx,lz,-LocXhalf,LocXhalf,PlazaZ0,LocZ1);
                if(dd>FlatBlend) continue;
                // 개천 2m 보존
                if(streamR.Count>0){ float sd=NearestStreamDist(wx,wz,streamR); if(sd<StreamKeep){ kept++; continue; } }
                if(dd<=0.0001f){ Hn[hz,hx]=padN; flat++; }
                else { float t=Mathf.SmoothStep(0,1,dd/FlatBlend); Hn[hz,hx]=Mathf.Lerp(padN,Hn[hz,hx],t); blend++; }
            }
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); terrain.Flush();
            sb.AppendLine($"[평탄] 목표 {targetY:F3}m · 평탄 {flat} + 블렌드 {blend} + 개천보존 {kept} 텍셀");
            var dev=RegionDev(terrain, td, set, -LocXhalf, LocXhalf, PlazaZ0, LocZ1, targetY);
            sb.AppendLine($"[재측정] footprint 목표대비 최대편차 {dev.maxAbs:F3}m {(dev.maxAbs<0.02f?"✓ (±0.02 이내)":"⚠")} · 스프레드 {dev.mx-dev.mn:F3}m");
            sb.AppendLine("※ 씬 저장 안 함(지형 에셋에는 변경 남음 — 15a 백업으로 복원 가능).");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"15b 평탄 완료. 편차 {dev.maxAbs:F3}m.\n다음 15c 스무스 / 15d 접지검증.","확인");
        }

        [MenuItem("Tools/Seocheon/Ground/15c. Smooth blend ring only")]
        public static void F_Smooth()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 15c Smooth blend ring =====");
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            if(!GetTerrain(out var terrain, out var td, sb)){ Debug.Log(sb.ToString()); return; }
            var set=FindSet(); if(set==null){ sb.AppendLine("★세트 없음."); Debug.Log(sb.ToString()); return; }
            int Rr=td.heightmapResolution; Vector3 S=td.size;
            float tX0=terrain.transform.position.x, tZ0=terrain.transform.position.z, texX=S.x/(Rr-1), texZ=S.z/(Rr-1);
            var Hn=td.GetHeights(0,0,Rr,Rr);
            // 블렌드 링 마스크: 0<dd<=FlatBlend (내부 dd==0 제외)
            var ring=new bool[Rr,Rr];
            for(int hz=0;hz<Rr;hz++) for(int hx=0;hx<Rr;hx++){ float wx=tX0+hx*texX, wz=tZ0+hz*texZ; LocalXZ(set,wx,wz,out float lx,out float lz);
                float dd=RectOutsideDist(lx,lz,-LocXhalf,LocXhalf,PlazaZ0,LocZ1); ring[hz,hx]= dd>0.0001f && dd<=FlatBlend; }
            int passes=2, sm=0;
            for(int p=0;p<passes;p++){ var src=(float[,])Hn.Clone();
                for(int hz=1;hz<Rr-1;hz++) for(int hx=1;hx<Rr-1;hx++){ if(!ring[hz,hx]) continue;
                    Hn[hz,hx]=(src[hz,hx]*4+src[hz-1,hx]+src[hz+1,hx]+src[hz,hx-1]+src[hz,hx+1])/8f; if(p==0) sm++; } }
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); terrain.Flush();
            sb.AppendLine($"[스무스] 블렌드 링만 {passes}패스 ({sm}텍셀) · footprint 내부 미변경");
            sb.AppendLine("※ 씬 저장 안 함.");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon","15c 스무스 완료. 15d 접지검증.","확인");
        }

        [MenuItem("Tools/Seocheon/Ground/15d. Grounding verify")]
        public static void F_Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 15d Grounding verify =====");
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            if(!GetTerrain(out var terrain, out var td, sb)){ Debug.Log(sb.ToString()); return; }
            var set=FindSet(); if(set==null){ sb.AppendLine("★세트 없음."); Debug.Log(sb.ToString()); return; }
            sb.AppendLine("오브젝트별(밑면 1m 그리드 → 지형 고도 비교, +틈/−파묻힘):");
            foreach(var nm in SixNames){ var t=FindUnder(set,nm); if(t==null){ sb.AppendLine($"  {nm}: ⚠ 없음"); continue; }
                var rs=t.GetComponentsInChildren<Renderer>(true); if(rs.Length==0){ sb.AppendLine($"  {nm}: 렌더러 없음"); continue; }
                var g=Grounding(terrain, rs, 0.15f);
                sb.AppendLine($"  {nm}: 최대틈 {g.maxGap:F3}m · 최대파묻힘 {g.maxEmbed:F3}m · 틈>0.15 지점 {g.over.Count}개{(g.over.Count>0?" ⚠":" ✓")}");
                foreach(var p in g.over.Take(6)) sb.AppendLine($"       틈 @ {V(p)}"); }
            var don=FindDonUnder(set);
            if(don!=null){ var rs=don.GetComponentsInChildren<Renderer>(true); if(rs.Length>0){ var g=Grounding(terrain, rs, 0.15f);
                sb.AppendLine($"  동헌 '{don.name}': 최대틈 {g.maxGap:F3}m · 최대파묻힘 {g.maxEmbed:F3}m (0.10 파묻힘 정상) · 틈>0.15 {g.over.Count}개{(g.over.Count>0?" ⚠":" ✓")}");
                foreach(var p in g.over.Take(6)) sb.AppendLine($"       틈 @ {V(p)}"); } }
            sb.AppendLine("※ 씬 저장 안 함.");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon","15d 접지검증 완료. Console 확인 → 15e 렌더.","확인");
        }

        [MenuItem("Tools/Seocheon/Ground/15e. Render flatten (eye 1.6m)")]
        public static void F_Render()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 15e Render flatten =====");
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            if(!GetTerrain(out var terrain, out var td, sb)){ Debug.Log(sb.ToString()); return; }
            var set=FindSet(); if(set==null){ sb.AppendLine("★세트 없음."); Debug.Log(sb.ToString()); return; }
            SixBounds(set, out var wb, sb); float baseY=wb.min.y;
            Vector3 W(float lx,float ly,float lz)=> set.TransformPoint(new Vector3(lx,ly,lz));
            string dir=@"C:\Users\User\관아\_renders\flatten"; Directory.CreateDirectory(dir);
            var files=new List<string>(); string err=null;
            try{
                files.Add(ShotTo(dir,"flatten_A_front_stream", W(0,1.6f,GateSouth-12f), W(0,1.0f,GateSouth+1f), 55f));   // 개천쪽 정면 담장밑 틈
                files.Add(ShotTo(dir,"flatten_B_back",         W(0,1.6f,LocZ1+12f),   W(0,1.0f,LocZ1-1f),   55f));       // 반대쪽 파묻힘
                files.Add(ShotTo(dir,"flatten_C_court_wallbase",W(9f,1.6f,16f),        W(16.95f,0.1f,16f),  50f));        // 마당 담장밑 근접
                files.Add(ShotTo(dir,"flatten_D_aerial",       W(0,45f,-16f),         W(0,1.5f,14f),        55f));        // 부감
            }catch(System.Exception e){ err=e.ToString(); }
            if(err!=null) sb.AppendLine("렌더 실패: "+err);
            else { sb.AppendLine($"렌더 4장 → {dir}"); foreach(var f in files) sb.AppendLine("   "+f); }
            sb.AppendLine("판정: A 담장밑 틈 사라짐 / B 파묻힘 과다X / C 마당 담장밑 / D 평탄경계·흙스플랫 경계 자연스러움");
            sb.AppendLine("※ 씬 저장 안 함.");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"15e 렌더 {files.Count}장 → {dir}\nConsole 확인.","확인");
        }

        // [15] helpers
        private static bool SixBounds(Transform set, out Bounds wb, StringBuilder sb){
            wb=new Bounds(); bool has=false;
            foreach(var nm in SixNames){ var t=FindUnder(set,nm); if(t==null){ sb.AppendLine($"★{nm} 없음 — 13f/13b 확인. 중단."); return false; }
                foreach(var r in t.GetComponentsInChildren<Renderer>(true)){ if(!has){ wb=r.bounds; has=true; } else wb.Encapsulate(r.bounds); } }
            return has;
        }
        private static Transform FindUnder(Transform root, string name){ foreach(var t in root.GetComponentsInChildren<Transform>(true)) if(t.name==name) return t; return null; }
        private static Transform FindDonUnder(Transform root){ foreach(var t in root.GetComponentsInChildren<Transform>(true)){ string n=t.name.ToLowerInvariant(); if(n.Contains("donheon")||t.name.Contains("동헌")) return t; } return null; }
        private static List<Renderer> StreamRenderers(){
            var list=new List<Renderer>();
            foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)){ string n=r.gameObject.name.ToLowerInvariant();
                bool inSet=false; for(var p=r.transform; p!=null; p=p.parent){ if(p.name==SetName||p.name=="_GwanaGate"){ inSet=true; break; } }
                if(inSet) continue;
                if(n.Contains("stream")||n.Contains("water")||n.Contains("개천")||n.Contains("수면")) list.Add(r); }
            return list;
        }
        private static void StreamNearest(Transform set, Terrain terrain, out float dist, out bool readable, out int count, StringBuilder sb){
            var rs=StreamRenderers(); count=rs.Count; dist=float.PositiveInfinity; readable=false;
            if(count==0){ dist=float.PositiveInfinity; return; }
            // footprint 경계 샘플(로컬 사각형 둘레 1m)
            var pts=new List<Vector3>();
            for(float lx=-LocXhalf; lx<=LocXhalf; lx+=1f){ pts.Add(set.TransformPoint(new Vector3(lx,0,PlazaZ0))); pts.Add(set.TransformPoint(new Vector3(lx,0,LocZ1))); }
            for(float lz=PlazaZ0; lz<=LocZ1; lz+=1f){ pts.Add(set.TransformPoint(new Vector3(-LocXhalf,0,lz))); pts.Add(set.TransformPoint(new Vector3(LocXhalf,0,lz))); }
            // 정점기준 시도
            foreach(var r in rs){ var mf=r.GetComponent<MeshFilter>(); var m=mf!=null?mf.sharedMesh:null; if(m==null) continue;
                Vector3[] vs=null; try{ vs=m.vertices; }catch{ vs=null; }
                if(vs!=null && vs.Length>0){ readable=true; var tr=r.transform; int stride=Mathf.Max(1, vs.Length/4000);
                    for(int i=0;i<vs.Length;i+=stride){ Vector3 w=tr.TransformPoint(vs[i]); foreach(var p in pts){ float d=Mathf.Sqrt((w.x-p.x)*(w.x-p.x)+(w.z-p.z)*(w.z-p.z)); if(d<dist) dist=d; } } } }
            if(!readable){ // bounds 근사(링이면 과대 근접일 수 있음 — 경고)
                foreach(var r in rs){ var b=r.bounds; foreach(var p in pts){ float d=DistToAABB2D(p.x,p.z,b.min,b.max); if(d<dist) dist=d; } }
                sb.AppendLine("   [6주의] 개천 메시가 Read/Write OFF — bounds 근사(링 AABB면 과대근접 가능). 렌더로 재확인."); }
        }
        private static float NearestStreamDist(float wx,float wz, List<Renderer> rs){
            float best=float.PositiveInfinity;
            foreach(var r in rs){ var mf=r.GetComponent<MeshFilter>(); var m=mf!=null?mf.sharedMesh:null;
                if(m!=null){ Vector3[] vs=null; try{ vs=m.vertices; }catch{ vs=null; }
                    if(vs!=null){ var tr=r.transform; int stride=Mathf.Max(1, vs.Length/2000);
                        for(int i=0;i<vs.Length;i+=stride){ Vector3 w=tr.TransformPoint(vs[i]); float d=Mathf.Sqrt((w.x-wx)*(w.x-wx)+(w.z-wz)*(w.z-wz)); if(d<best) best=d; } continue; } }
                var b=r.bounds; float dd=DistToAABB2D(wx,wz,b.min,b.max); if(dd<best) best=dd; }
            return best;
        }
        private static float DistToAABB2D(float x,float z, Vector3 mn, Vector3 mx){
            float dx=Mathf.Max(mn.x-x, 0f, x-mx.x); float dz=Mathf.Max(mn.z-z, 0f, z-mx.z); return Mathf.Sqrt(dx*dx+dz*dz); }
        private static (float maxGap,float maxEmbed,List<Vector3> over) Grounding(Terrain terrain, Renderer[] rs, float overThresh){
            Bounds b=rs[0].bounds; foreach(var r in rs) b.Encapsulate(r.bounds);
            float bottom=b.min.y, maxGap=0f, maxEmbed=0f; var over=new List<Vector3>();
            for(float x=b.min.x; x<=b.max.x; x+=1f) for(float z=b.min.z; z<=b.max.z; z+=1f){
                float th=terrain.transform.position.y+terrain.SampleHeight(new Vector3(x,0,z));
                float gap=bottom-th;
                if(gap>maxGap) maxGap=gap; if(-gap>maxEmbed) maxEmbed=-gap;
                if(gap>overThresh) over.Add(new Vector3(x,th,z)); }
            return (maxGap,maxEmbed,over);
        }
        private static string ShotTo(string dir, string name, Vector3 pos, Vector3 look, float fov){
            const int W=1600,H=900; var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32); var tex=new Texture2D(W,H,TextureFormat.RGB24,false);
            var camGO=new GameObject("__gcam"); var cam=camGO.AddComponent<Camera>();
            cam.clearFlags=CameraClearFlags.Skybox; cam.fieldOfView=fov; cam.nearClipPlane=0.05f; cam.farClipPlane=3000f; cam.enabled=false;
            cam.transform.position=pos; cam.transform.rotation=Quaternion.LookRotation((look-pos).normalized, Vector3.up);
            string fn=Path.Combine(dir, name+".png");
            try{ var req=new UniversalRenderPipeline.SingleCameraRequest();
                 if(RenderPipeline.SupportsRenderRequest(cam,req)){ req.destination=rt; RenderPipeline.SubmitRenderRequest(cam,req); }
                 else { cam.targetTexture=rt; cam.Render(); cam.targetTexture=null; }
                 RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=null; File.WriteAllBytes(fn, tex.EncodeToPNG()); }
            finally{ Object.DestroyImmediate(camGO); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
            return fn;
        }

        // ── helpers ──
        private static bool GetTerrain(out Terrain terrain, out TerrainData td, StringBuilder sb)
        {
            var terrs=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            terrain = terrs.Length>0?terrs[0]:null; td = terrain!=null?terrain.terrainData:null;
            if(terrain==null||td==null){ sb.AppendLine("★Terrain 없음."); return false; }
            return true;
        }
        private static Transform FindSet(){ foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if(t.name==SetName) return t;
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if(t.name=="_GwanaGate") return t; return null; }
        private static void LocalXZ(Transform set, float wx, float wz, out float lx, out float lz){
            if(set!=null){ var l=set.InverseTransformPoint(new Vector3(wx,0f,wz)); lx=l.x; lz=l.z; }
            else { lx=wx-462f; lz=wz-589.66f; } }
        private static float RectOutsideDist(float x,float z,float x0,float x1,float z0,float z1){
            float ox=Mathf.Max(0f, Mathf.Max(x0-x, x-x1)); float oz=Mathf.Max(0f, Mathf.Max(z0-z, z-z1));
            return Mathf.Sqrt(ox*ox+oz*oz); }
        private static float SampleH(Terrain t, TerrainData td, float x, float z){ return t.transform.position.y + t.SampleHeight(new Vector3(x,0,z)); }
        private static (float mn,float mx,float mean,float maxAbs) RegionDev(Terrain t, TerrainData td, Transform set, float x0,float x1,float z0,float z1, float refY){
            float mn=1e9f,mx=-1e9f,sum=0f,maxAbs=0f; int n=0;
            for(float lx=x0; lx<=x1; lx+=1f) for(float lz=z0; lz<=z1; lz+=1f){
                Vector3 w = set!=null? set.TransformPoint(new Vector3(lx,0,lz)) : new Vector3(462f+lx,0,589.66f+lz);
                float h=SampleH(t,td,w.x,w.z); mn=Mathf.Min(mn,h); mx=Mathf.Max(mx,h); sum+=h; maxAbs=Mathf.Max(maxAbs,Mathf.Abs(h-refY)); n++; }
            return (mn,mx, n>0?sum/n:0f, maxAbs);
        }
        private static TerrainLayer CreateDirtLayer(StringBuilder sb){
            EnsureFolder(TerrainLayerDir);
            // 브라운 펄린 디퓨즈 생성
            int T=256; var tex=new Texture2D(T,T,TextureFormat.RGB24,true);
            var px=new Color[T*T];
            for(int y=0;y<T;y++) for(int x=0;x<T;x++){ float n=Mathf.PerlinNoise(x*0.06f,y*0.06f)*0.35f+Mathf.PerlinNoise(x*0.22f+5,y*0.22f+5)*0.15f;
                float r=0.45f+n*0.5f, g=0.33f+n*0.4f, b=0.20f+n*0.3f; px[y*T+x]=new Color(Mathf.Clamp01(r),Mathf.Clamp01(g),Mathf.Clamp01(b)); }
            tex.SetPixels(px); tex.Apply();
            string texPath=TerrainLayerDir+"/T_Gwana_Dirt.png";
            File.WriteAllBytes(Path.GetFullPath(texPath), tex.EncodeToPNG()); AssetDatabase.ImportAsset(texPath);
            var ti=(TextureImporter)AssetImporter.GetAtPath(texPath); if(ti!=null){ ti.wrapMode=TextureWrapMode.Repeat; ti.SaveAndReimport(); }
            var diff=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var layer=new TerrainLayer{ diffuseTexture=diff, tileSize=new Vector2(3f,3f), name="Gwana_Dirt_황토" };
            string lp=TerrainLayerDir+"/TL_Gwana_Dirt.terrainlayer"; AssetDatabase.CreateAsset(layer, lp);
            sb.AppendLine($"[흙] 디퓨즈 {texPath} · 레이어 {lp} (tileSize 3x3)");
            return layer;
        }
        private static string V(Vector3 v){ return $"({v.x:F2}, {v.y:F2}, {v.z:F2})"; }
        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }

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
    }
}
