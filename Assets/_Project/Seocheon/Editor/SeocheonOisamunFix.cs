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
    /// [1] 외삼문 진단·복구 — 흰색 원인(텍스처 미할당) 규명 후 URP/Lit 재질에 BaseColor+Normal 재연결.
    /// 축(누움) 점검 · 텍스처 축소(BaseColor1024/Normal512/ASTC6x6) · 프리팹 재생성. 폴리 감축 없음.
    /// </summary>
    public static class SeocheonOisamunFix
    {
        private const string Fbx    = "Assets/_Project/Seocheon/Art/Models/SM_Oisamun.fbx";
        private const string TexDir = "Assets/_Project/Seocheon/Art/Textures/Oisamun";
        private const string MatDir = "Assets/_Project/Seocheon/Art/Materials/Oisamun";
        private const string PrefabPath="Assets/_Project/Seocheon/Prefabs/Oisamun.prefab";

        [MenuItem("Tools/Seocheon/Village/5a2. Oisamun Diagnose + Fix")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] 5a2 Oisamun Diagnose + Fix =====");
            var mi=(ModelImporter)AssetImporter.GetAtPath(Fbx); if(mi==null){ EditorUtility.DisplayDialog("Seocheon","FBX 임포터 없음.","확인"); return; }

            // 텍스처 강제 추출(이미 있으면 재추출)
            EnsureFolder(TexDir);
            bool ext=mi.ExtractTextures(TexDir); AssetDatabase.Refresh();
            var texPaths=AssetDatabase.FindAssets("t:Texture2D", new[]{TexDir}).Select(g=>AssetDatabase.GUIDToAssetPath(g)).ToList();
            sb.AppendLine($"[1] Extract Textures {(ext?"실행":"이미추출/변화없음")} · 추출 텍스처 {texPaths.Count}장 @ {TexDir}");

            // 슬롯명 수집(현 상태 진단)
            var fbxGo=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var slots=new List<string>(); var slotHasBase=new Dictionary<string,bool>();
            foreach(var r in fbxGo.GetComponentsInChildren<MeshRenderer>(true)) foreach(var m in r.sharedMaterials){ if(m==null) continue; if(!slots.Contains(m.name)){ slots.Add(m.name);
                bool hasBase = (m.HasProperty("_BaseMap")&&m.GetTexture("_BaseMap")!=null)||(m.HasProperty("_MainTex")&&m.GetTexture("_MainTex")!=null); slotHasBase[m.name]=hasBase; } }

            // 텍스처 매칭 헬퍼
            string FindTex(string mat, string[] baseKeys){ // mat 이름으로 시작하고 key 포함
                string best=null; foreach(var p in texPaths){ string n=Path.GetFileNameWithoutExtension(p); if(!n.ToLowerInvariant().StartsWith(mat.ToLowerInvariant())) continue;
                    foreach(var k in baseKeys) if(n.ToLowerInvariant().Contains(k)){ return p; } }
                // 완화: 포함만
                foreach(var p in texPaths){ string n=Path.GetFileNameWithoutExtension(p); if(n.ToLowerInvariant().Contains(mat.ToLowerInvariant())) foreach(var k in baseKeys) if(n.ToLowerInvariant().Contains(k)) return p; }
                return best; }

            // URP/Lit 재질 생성 + 재연결 + 리맵
            EnsureFolder(MatDir);
            var lit=Shader.Find("Universal Render Pipeline/Lit");
            var unmatched=new List<string>(); int fixedBase=0, fixedNorm=0;
            sb.AppendLine("\n[1] 머티리얼 15 진단·복구 (Base/Normal 텍스처):");
            sb.AppendLine("   재질 | 기존Base | 새Base | 새Normal");
            foreach(var s in slots){ string bp=FindTex(s, new[]{"basecolor","albedo","_bc","_d","diffuse","_col"}); string np=FindTex(s, new[]{"normal","_n","_nrm","_norm"});
                var mp=$"{MatDir}/{s}.mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(mp); if(mat==null){ mat=new Material(lit); AssetDatabase.CreateAsset(mat, mp); }
                if(bp!=null){ var bt=AssetDatabase.LoadAssetAtPath<Texture2D>(bp); mat.SetTexture("_BaseMap",bt); mat.SetColor("_BaseColor",Color.white); fixedBase++; }
                if(np!=null){ var nt=AssetDatabase.LoadAssetAtPath<Texture2D>(np); mat.SetTexture("_BumpMap",nt); mat.EnableKeyword("_NORMALMAP"); fixedNorm++; }
                if(s.ToLowerInvariant().Contains("paper")||s.ToLowerInvariant().Contains("changho")){ mat.SetFloat("_Smoothness",0.1f); }
                EditorUtility.SetDirty(mat);
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), s), mat);
                if(bp==null) unmatched.Add(s);
                sb.AppendLine($"   {s} | {(slotHasBase.TryGetValue(s,out var hb)&&hb?"있음":"없음")} | {(bp!=null?Path.GetFileNameWithoutExtension(bp):"⚠없음")} | {(np!=null?Path.GetFileNameWithoutExtension(np):"-")}");
            }
            AssetDatabase.SaveAssets();

            // 텍스처 임포트 설정(축소·ASTC·Normal type)
            int nN=0,nB=0; foreach(var p in texPaths){ var ti=(TextureImporter)AssetImporter.GetAtPath(p); if(ti==null) continue; string low=Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                bool isN=low.Contains("normal")||low.EndsWith("_n")||low.Contains("_nrm")||low.Contains("_norm");
                if(isN){ ti.textureType=TextureImporterType.NormalMap; ti.maxTextureSize=512; nN++; } else { ti.textureType=TextureImporterType.Default; ti.maxTextureSize=1024; nB++; }
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings{ name="Android", overridden=true, maxTextureSize=isN?512:1024, format=TextureImporterFormat.ASTC_6x6, compressionQuality=100 });
                ti.SaveAndReimport(); }
            mi.SaveAndReimport();

            // 축·bbox 실측(프리팹 인스턴스로 월드 측정)
            var probe=(GameObject)PrefabUtility.InstantiatePrefab(fbxGo); probe.transform.position=Vector3.zero; probe.transform.rotation=Quaternion.identity;
            var rns=probe.GetComponentsInChildren<Renderer>(true); Bounds wb=rns[0].bounds; foreach(var r in rns) wb.Encapsulate(r.bounds);
            Vector3 sz=wb.size; float baseY=wb.min.y; string wideAxis=(sz.x>=sz.z&&sz.x>=sz.y)?"X":(sz.z>=sz.y?"Z":"Y");
            bool lying = !(sz.y>6f&&sz.y<11f && Mathf.Max(sz.x,sz.z)>15f);
            Object.DestroyImmediate(probe);

            long texMem=0; foreach(var p in texPaths){ var tex=AssetDatabase.LoadAssetAtPath<Texture>(p); if(tex!=null) texMem+=Profiler.GetRuntimeMemorySizeLong(tex); }

            // 프리팹 재생성
            var inst=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
            if(lying){ inst.transform.rotation=Quaternion.Euler(-90,0,0); }
            SetStaticRec(inst); PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath); Object.DestroyImmediate(inst);

            sb.AppendLine($"\n[1] 재연결: Base {fixedBase}/15 · Normal {fixedNorm}/15 · ★미할당(흰색후보): {(unmatched.Count==0?"없음 ✓":string.Join(", ",unmatched))}");
            sb.AppendLine($"[1] 축: 월드 bbox {sz.x:F2}(X)×{sz.z:F2}(Z)×{sz.y:F2}(Y) · 폭 18.79 축={wideAxis} · 밑면Y {baseY:F2} · {(lying?"⚠누움→프리팹 −90°X 적용":"정립 ✓(회전보정 없음)")}");
            sb.AppendLine($"[1] 텍스처 노멀 {nN}·Base {nB} · ASTC6x6 · 런타임메모리(에디터) {texMem/1048576f:F1}MB");
            sb.AppendLine($"[1] 프리팹 갱신 → {PrefabPath}");
            sb.AppendLine("\n※ 흰색이 텍스처 미할당이면 위 재연결로 해결. 미할당 목록이 있으면 그 재질만 수동 확인. 확인 후 5b(남북).");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"5a2 완료. Base {fixedBase}/15·Normal {fixedNorm}/15 재연결\n미할당 {unmatched.Count} · 폭축 {wideAxis} · {(lying?"누움-보정":"정립")}\ntex {texMem/1048576f:F0}MB. Console 확인.","확인");
        }

        private static void SetStaticRec(GameObject g){ g.isStatic=true; foreach(Transform c in g.transform) SetStaticRec(c.gameObject); }
        private static void EnsureFolder(string path){ if(AssetDatabase.IsValidFolder(path)) return; var parent=Path.GetDirectoryName(path).Replace("\\","/"); var leaf=Path.GetFileName(path); if(!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,leaf); }
    }
}
