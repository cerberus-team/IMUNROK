using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// 리셋: 동헌만 원래 자리(462,616 남향)로 배치하고 _Village·_Oisamun 은 씬에서 제거.
    /// 프리팹/백업 에셋은 보존. 개천 링·수면·물머티리얼·다리 미변경.
    /// </summary>
    public static class SeocheonResetDonheon
    {
        private const string Scene   = "Assets/_Project/Seocheon/Scenes/Seocheon_Village.unity";
        private const string BakTerr = "Assets/_Project/Seocheon/Art/Terrain/Seocheon_Village_Terrain_backup_predonheon.asset";
        private const string DonPrefab="Assets/_Project/Seocheon/Prefabs/Donheon.prefab";
        private const string DonFbx  = "Assets/_Project/Seocheon/Art/Models/Donheon_Quest3_v11.fbx";
        private const string PathFile= @"C:\Users\User\_AssetBackup\seocheon_stream_path.txt";
        private const float CX=463f, CZ=678f, RingBand=11.8f;
        private static readonly Vector2 PadC=new Vector2(462f,616f);
        private const float HalfX=12f, HalfZ=9f, Skirt=8f, Ground=0.70f;

        [MenuItem("Tools/Seocheon/Village/RESET - Donheon only (remove village)")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){ EditorUtility.DisplayDialog("Seocheon","▶ Play 모드 불가.","확인"); return; }
            var sb=new StringBuilder(); sb.AppendLine("===== [Seocheon] RESET Donheon only =====");
            var scene=EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);

            // 마을·외삼문·기존 동헌 제거
            int removed=0;
            foreach(var r in scene.GetRootGameObjects().ToList()) if(r.name=="_Village"||r.name=="_Oisamun"||r.name=="_Donheon"){ Object.DestroyImmediate(r); removed++; }
            sb.AppendLine($"[1] 루트 제거 {removed}개(_Village/_Oisamun/_Donheon) — 프리팹 에셋은 보존");

            // 지형 원복
            var terrs=Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); Terrain terrain=terrs.Length>0?terrs[0]:null;
            var td=terrain.terrainData; int Rr=td.heightmapResolution; Vector3 S=td.size; float tY=terrain.transform.position.y,tX0=terrain.transform.position.x,tZ0=terrain.transform.position.z; float texX=S.x/(Rr-1),texZ=S.z/(Rr-1);
            var bak=AssetDatabase.LoadAssetAtPath<TerrainData>(BakTerr);
            if(bak!=null && bak.heightmapResolution==Rr){ td.SetHeights(0,0,bak.GetHeights(0,0,Rr,Rr)); EditorUtility.SetDirty(td); terrain.Flush(); }
            var Hn=td.GetHeights(0,0,Rr,Rr);
            float H(float x,float z){ float u=(x-tX0)/S.x,v=(z-tZ0)/S.z; float fx=Mathf.Clamp(u*(Rr-1),0,Rr-1),fz=Mathf.Clamp(v*(Rr-1),0,Rr-1);
                int x0=(int)fx,z0=(int)fz,x1=Mathf.Min(x0+1,Rr-1),z1=Mathf.Min(z0+1,Rr-1); float tx=fx-x0,tz=fz-z0;
                return tY+Mathf.Lerp(Mathf.Lerp(Hn[z0,x0],Hn[z0,x1],tx),Mathf.Lerp(Hn[z1,x0],Hn[z1,x1],tx),tz)*S.y; }
            sb.AppendLine($"[2] 지형 원복(predonheon) {(bak!=null?"✓":"⚠백업없음")}");

            // 동헌 대지 평탄화 (462,616), 링밴드 제외
            var rth=LoadRth(); float padY=H(PadC.x,PadC.y); float padN=Mathf.Clamp01((padY-tY)/S.y);
            int xMin=Mathf.Clamp(Mathf.FloorToInt((PadC.x-tX0-HalfX-Skirt)/texX),0,Rr-1), xMax=Mathf.Clamp(Mathf.CeilToInt((PadC.x-tX0+HalfX+Skirt)/texX),0,Rr-1);
            int zMin=Mathf.Clamp(Mathf.FloorToInt((PadC.y-tZ0-HalfZ-Skirt)/texZ),0,Rr-1), zMax=Mathf.Clamp(Mathf.CeilToInt((PadC.y-tZ0+HalfZ+Skirt)/texZ),0,Rr-1);
            int flat=0; for(int hz=zMin;hz<=zMax;hz++) for(int hx=xMin;hx<=xMax;hx++){ float x=tX0+hx*texX,z=tZ0+hz*texZ;
                float rr=Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ)); float r2=RthAt(rth,Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg); if(Mathf.Abs(rr-r2)<=RingBand) continue;
                float ox=Mathf.Max(0,Mathf.Abs(x-PadC.x)-HalfX), oz=Mathf.Max(0,Mathf.Abs(z-PadC.y)-HalfZ); float dd=Mathf.Sqrt(ox*ox+oz*oz);
                if(dd<=0.001f){ Hn[hz,hx]=padN; flat++; } else if(dd<=Skirt){ float tt=Mathf.SmoothStep(0,1,dd/Skirt); Hn[hz,hx]=Mathf.Lerp(padN,Hn[hz,hx],tt); } }
            td.SetHeights(0,0,Hn); EditorUtility.SetDirty(td); terrain.Flush();
            sb.AppendLine($"[3] 동헌 대지 평탄화 {flat}텍셀 · padY {padY:F2}m");

            // 정면 = 현판(Sign_PHD) → −Z(남/다리쪽), 폴백 granite
            Vector2 f=FrontDir(DonPrefab,DonFbx,"Sign_PHD",out bool ok,out string mat); if(!ok){ f=FrontDir(DonPrefab,DonFbx,"Granite",out ok,out mat); }
            float rotY=PickRotYtoNegZ(f);

            // 배치 (462,616), Y=padY+0.70
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(DonPrefab);
            var holder=new GameObject("_Donheon"); var inst=(GameObject)PrefabUtility.InstantiatePrefab(prefab); inst.transform.SetParent(holder.transform);
            inst.transform.rotation=Quaternion.Euler(0,rotY,0); inst.transform.position=new Vector3(PadC.x, padY+Ground, PadC.y); SetStaticRec(holder);
            sb.AppendLine($"[4] 동헌 배치 (462,616) rotY {rotY:F0}°(정면=재질 '{mat}', 남향) Y={padY+Ground:F2}(pad+0.70)");
            sb.AppendLine("    ※ 정면이 반대면 Tools/Seocheon/Donheon/Flip Front 180");

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, Scene);
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Seocheon", $"리셋 완료. 마을/외삼문 제거 · 동헌만 (462,616) 남향 배치.\n정면 틀리면 Flip Front 180. 씬 저장됨.","확인");
        }

        private static Vector2 FrontDir(string prefab, string fbx, string matContains, out bool found, out string matName){ found=false; matName="";
            var mi=(ModelImporter)AssetImporter.GetAtPath(fbx); if(mi==null) return Vector2.zero; bool was=mi.isReadable; if(!was){ mi.isReadable=true; mi.SaveAndReimport(); }
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            Vector3 allC=Vector3.zero; float allA=0, subA=0; Vector3 subC=Vector3.zero;
            foreach(var mr in go.GetComponentsInChildren<MeshRenderer>(true)){ var mf=mr.GetComponent<MeshFilter>(); if(mf==null||mf.sharedMesh==null) continue; var m=mf.sharedMesh; Vector3[] vs; try{ vs=m.vertices; }catch{ continue; } var mats=mr.sharedMaterials; var tr=mr.transform;
                for(int s=0;s<m.subMeshCount && s<mats.Length;s++){ var tri=m.GetTriangles(s); bool isSub=mats[s]!=null && mats[s].name.ToLowerInvariant().Contains(matContains.ToLowerInvariant()); if(isSub&&mats[s]!=null) matName=mats[s].name;
                    for(int t=0;t<tri.Length;t+=3){ Vector3 a=tr.TransformPoint(vs[tri[t]]),b=tr.TransformPoint(vs[tri[t+1]]),c=tr.TransformPoint(vs[tri[t+2]]); float ar=0.5f*Vector3.Cross(b-a,c-a).magnitude; Vector3 ct=(a+b+c)/3f;
                        allC+=ct*ar; allA+=ar; if(isSub){ subC+=ct*ar; subA+=ar; } } } }
            if(!was){ mi.isReadable=false; mi.SaveAndReimport(); }
            if(allA<=0||subA<=0) return Vector2.zero; found=true; Vector3 g=allC/allA, sg=subC/subA; return new Vector2(sg.x-g.x, sg.z-g.z);
        }
        private static float PickRotYtoNegZ(Vector2 f){ if(f.sqrMagnitude<1e-6f) return 0f; float best=-1e9f,br=0; foreach(float ry in new[]{0f,90f,180f,270f}){ Vector3 w=Quaternion.Euler(0,ry,0)*new Vector3(f.x,0,f.y); float d=Vector3.Dot(w.normalized,Vector3.back); if(d>best){best=d;br=ry;} } return br; }
        private static void SetStaticRec(GameObject g){ g.isStatic=true; foreach(Transform c in g.transform) SetStaticRec(c.gameObject); }
        private static List<Vector2> LoadRth(){ var list=new List<Vector2>(); if(!File.Exists(PathFile)) return list; var ci=CultureInfo.InvariantCulture; var lines=File.ReadAllLines(PathFile);
            for(int i=1;i<lines.Length;i++){ var t=lines[i].Split(' '); if(t.Length<2) continue; float x=float.Parse(t[0],ci),z=float.Parse(t[1],ci); list.Add(new Vector2(Mathf.Atan2(z-CZ,x-CX)*Mathf.Rad2Deg, Mathf.Sqrt((x-CX)*(x-CX)+(z-CZ)*(z-CZ)))); } return list; }
        private static float RthAt(List<Vector2> rth, float ang){ if(rth.Count==0) return 100f; float best=1e9f,br=100f; foreach(var v in rth){ float d=Mathf.Abs(Mathf.DeltaAngle(v.x,ang)); if(d<best){best=d;br=v.y;} } return br; }
    }
}
