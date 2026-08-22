using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// [레이아웃 0-1] 백업 + 외삼문(SM_Oisamun) 임포트.
    /// Z-up 정립 처리 · 임베드 텍스처 추출/압축(BaseColor1024·Normal512·ASTC6x6) · 프리팹.
    /// ★폴리 감축 없음(245k 그대로). 개천 링·수면·다리 미변경. 원본 FBX 미수정(임포트 설정만).
    /// </summary>
    public static class SeocheonOisamun
    {
        private const string Fbx    = "Assets/_Project/Seocheon/Art/Models/SM_Oisamun.fbx";
        private const string TexDir = "Assets/_Project/Seocheon/Art/Textures/Oisamun";
        private const string PrefabPath="Assets/_Project/Seocheon/Prefabs/Oisamun.prefab";
        private const string CurTerr= "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain.asset";
        private const string BakTerr= "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain_backup_prelayout.asset";
        private const string CurScene="Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string BakScene="Assets/_Project/Seocheon/Scenes/_Village_backup_prelayout.unity";
        // 기대 실측
        private const int ExpTris=245430; private static readonly Vector3 ExpBbox=new Vector3(18.79f,8.33f,8.34f); private const int ExpMat=15;

        [MenuItem("Tools/Seocheon/Village/5a. Backup + Import Oisamun")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 5a Backup + Oisamun Import =====");
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx)==null && !File.Exists(Path.GetFullPath(Fbx))){ EditorUtility.DisplayDialog("Seocheon","FBX 없음:\n"+Fbx,"확인"); return; }

            // ── [0] 백업 ──
            if(AssetDatabase.LoadAssetAtPath<TerrainData>(CurTerr)!=null){ if(AssetDatabase.LoadAssetAtPath<TerrainData>(BakTerr)!=null) AssetDatabase.DeleteAsset(BakTerr);
                AssetDatabase.CopyAsset(CurTerr, BakTerr); sb.AppendLine($"[0] 지형(높이+알파맵) 백업 → {BakTerr}"); }
            else sb.AppendLine($"[0] ⚠ 현재 지형 에셋 {CurTerr} 못 찾음 — 지형 백업 생략(씬 내 TerrainData가 별도일 수 있음)");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(BakScene)!=null) AssetDatabase.DeleteAsset(BakScene);
            AssetDatabase.CopyAsset(CurScene, BakScene); sb.AppendLine($"[0] 씬 백업 → {BakScene}");

            // ── [1] 임포트 설정 ──
            AssetDatabase.ImportAsset(Fbx, ImportAssetOptions.ForceUpdate);
            var mi=(ModelImporter)AssetImporter.GetAtPath(Fbx);
            mi.globalScale=1f; mi.useFileScale=true; mi.isReadable=false;
            mi.meshOptimizationFlags=MeshOptimizationFlags.Everything; mi.addCollider=false;
            mi.importCameras=false; mi.importLights=false;
            mi.bakeAxisConversion=false;
            mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            mi.SaveAndReimport();

            // 측정 helper
            Vector3 Measure(){ var go=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx); bool has=false; Bounds b=new Bounds();
                foreach(var mf in go.GetComponentsInChildren<MeshFilter>(true)){ if(mf.sharedMesh==null) continue; var lb=mf.sharedMesh.bounds; // 로컬
                    var wb=new Bounds(mf.transform.TransformPoint(lb.center), Vector3.zero); // 근사(회전 없음 가정)
                    wb=GeomBounds(mf); if(!has){b=wb;has=true;} else b.Encapsulate(wb); } return has?b.size:Vector3.zero; }

            Vector3 sz=Measure();
            // Z-up 정립: 폭(18.79)이 수평이고 높이 Y≈8.3 이어야. 아니면 Bake Axis Conversion.
            string orient;
            bool upright = sz.y>6f && sz.y<11f && Mathf.Max(sz.x,sz.z)>15f;
            if(upright){ orient="자동 정립(Bake Axis OFF)"; }
            else {
                mi.bakeAxisConversion=true; mi.SaveAndReimport(); sz=Measure();
                upright = sz.y>6f && sz.y<11f && Mathf.Max(sz.x,sz.z)>15f;
                if(upright) orient="Bake Axis Conversion ON 으로 정립";
                else orient="⚠ 여전히 누움 → 프리팹 인스턴스에 −90°X 적용";
            }

            // ── [1] 텍스처 추출 + 압축 ──
            EnsureFolder(TexDir);
            bool extracted=mi.ExtractTextures(TexDir); AssetDatabase.Refresh();
            mi.SaveAndReimport(); // 추출 텍스처로 재연결
            var texGuids=AssetDatabase.FindAssets("t:Texture2D", new[]{TexDir});
            int nNormal=0, nBase=0; var normalNames=new List<string>();
            foreach(var g in texGuids){ string p=AssetDatabase.GUIDToAssetPath(g); var ti=(TextureImporter)AssetImporter.GetAtPath(p); if(ti==null) continue;
                string nm=Path.GetFileNameWithoutExtension(p); string low=nm.ToLowerInvariant();
                bool isN = low.Contains("normal")||low.EndsWith("_n")||low.Contains("_nrm")||low.Contains("_norm");
                if(isN){ ti.textureType=TextureImporterType.NormalMap; ti.maxTextureSize=512; nNormal++; normalNames.Add(nm); }
                else { ti.textureType=TextureImporterType.Default; ti.maxTextureSize=1024; nBase++; }
                var ps=new TextureImporterPlatformSettings{ name="Android", overridden=true, maxTextureSize=isN?512:1024, format=TextureImporterFormat.ASTC_6x6, compressionQuality=100 };
                ti.SetPlatformTextureSettings(ps); ti.SaveAndReimport();
            }
            long texMem=0; foreach(var g in texGuids){ var tex=AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(g)); if(tex!=null) texMem+=Profiler.GetRuntimeMemorySizeLong(tex); }

            // ── 실측 ──
            var fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            int tris=0; foreach(var mf in fbxGo.GetComponentsInChildren<MeshFilter>(true)) if(mf.sharedMesh!=null) for(int s=0;s<mf.sharedMesh.subMeshCount;s++) tris+=(int)(mf.sharedMesh.GetIndexCount(s)/3);
            int meshObjs=fbxGo.GetComponentsInChildren<MeshFilter>(true).Count(mf=>mf.sharedMesh!=null);
            var matSet=new HashSet<string>(); foreach(var r in fbxGo.GetComponentsInChildren<MeshRenderer>(true)) foreach(var m in r.sharedMaterials) if(m!=null) matSet.Add(m.name);
            sz=Measure();

            sb.AppendLine($"\n[1] 임포트 실측 vs 기대:");
            sb.AppendLine($"   tris {tris} (기대 {ExpTris} {(tris==ExpTris?"✓":"⚠ 불일치")})");
            sb.AppendLine($"   메시 오브젝트 {meshObjs} · 머티리얼 {matSet.Count} (기대 {ExpMat} {(matSet.Count==ExpMat?"✓":"⚠")})");
            sb.AppendLine($"   bbox {sz.x:F2}×{sz.z:F2}×{sz.y:F2}m (W×D×H) (기대 {ExpBbox.x}×{ExpBbox.y}×{ExpBbox.z} {(Approx(sz,ExpBbox)?"✓":"⚠ 확인")})");
            sb.AppendLine($"   Z-up 처리: {orient}");
            sb.AppendLine($"   텍스처 {texGuids.Length}장 (추출 {(extracted?"성공":"⚠실패")}) · 노멀 {nNormal}장(512) · BaseColor {nBase}장(1024) · ASTC6x6");
            sb.AppendLine($"   노멀 목록: {(normalNames.Count>0?string.Join(", ",normalNames):"(이름 규칙 미검출 — 수동 확인 필요)")}");
            sb.AppendLine($"   텍스처 런타임 메모리(에디터 기준) {texMem/1048576f:F1}MB · Android는 ASTC로 더 작음");

            // ── 프리팹 ──
            EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace("\\","/"));
            var inst=(GameObject)PrefabUtility.InstantiatePrefab(fbxGo);
            if(!upright) inst.transform.rotation=Quaternion.Euler(-90f,0,0); // 폴백
            SetStaticRec(inst);
            PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath); Object.DestroyImmediate(inst);
            sb.AppendLine($"[1] 프리팹 → {PrefabPath} (Static ON{(!upright?", 인스턴스 −90°X":"")})");
            sb.AppendLine($"\n※ 245k tris는 씬 총합에 그대로 얹힘(폴리 감축 안 함 — 블렌더 별도). 실측 기대와 다르면 여기서 확인 후 5b 진행.");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"5a 완료. 외삼문 tris {tris} · 머티 {matSet.Count} · {orient}\ntex {texGuids.Length}장 {texMem/1048576f:F0}MB\n실측 확인 후 5b Layout. Console 확인.","확인");
        }

        private static Bounds GeomBounds(MeshFilter mf){ var m=mf.sharedMesh; var lb=m.bounds; var t=mf.transform; // 로컬 bbox 8코너 월드변환
            Vector3 c=lb.center, e=lb.extents; bool has=false; Bounds b=new Bounds();
            for(int i=0;i<8;i++){ Vector3 corner=c+new Vector3((i&1)==0?-e.x:e.x,(i&2)==0?-e.y:e.y,(i&4)==0?-e.z:e.z); Vector3 w=t.TransformPoint(corner); if(!has){b=new Bounds(w,Vector3.zero);has=true;} else b.Encapsulate(w); } return b; }
        private static bool Approx(Vector3 s, Vector3 e){ return Mathf.Abs(s.x-e.x)<1f && Mathf.Abs(s.z-e.y)<1f && Mathf.Abs(s.y-e.z)<1f; }
        private static void SetStaticRec(GameObject g){ g.isStatic=true; foreach(Transform c in g.transform) SetStaticRec(c.gameObject); }
        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
    }
}
