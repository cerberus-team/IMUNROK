// SeocheonMtnGen.cs  — 마커 기반 산기슭 지형 생성 (성 밖 개천 남쪽)
// 규칙: 좌표 하드코딩 금지(전부 마커에서 읽음), 건물/수면 Transform 불변, 하이트맵+스플랫만,
//       개천 수면 메시 실제 링 + 8m 안쪽 하이트맵 수정 금지, 작업 전 백업.
// 메뉴: Tools/Seocheon/Mtn/Generate
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class SeocheonMtnGen
{
    const float ROAD_HALF = 2.5f;    // 노면 폭 5m (선명하게)
    const float SMOOTH    = 1.5f;    // 양옆 Smooth
    const float MAX_GRADE = 0.12f;   // 노면 경사 클램프
    const float RIDGE_RISE= 15f;     // 능선 마루 +15m (완만 사면 ~16%)
    const float RIDGE_SIG = 22f;     // (미사용)
    const float TER_R     = 9f;      // 터 반경
    const float TER_EDGE  = 4f;      // 터 가장자리 smooth (부드럽게)
    const float STREAM_KEEP=8f;      // 개천 마스크 여유
    const int   DIRT      = 1;       // 기존 흙 TerrainLayer index (TR_Ground01b)
    const bool  ENABLE_SPLAT_PAINT=false; // ★스플랫 재칠 비활성화. 스플랫은 백업 대입(RestoreSplatFromWide)으로만 변경.
    const string RENDER_DIR=@"C:\Users\User\_Renders\MtnGen\";

    [MenuItem("Tools/Seocheon/Mtn/Generate")]
    public static void Generate()
    {
        var log=new StringBuilder();
        var terrain=Terrain.activeTerrain; if(terrain==null){Debug.LogError("[MtnGen] no terrain");return;}
        var td=terrain.terrainData;

        // ---- 백업 확인 + 신규 ----
        const string bkDir="Assets/_Project/Seocheon/Art/Terrain/_Backup";
        var existing=AssetDatabase.FindAssets("t:TerrainData",new[]{bkDir});
        if(existing.Length==0){Debug.LogError("[MtnGen] 백업 없음 — 중단");return;}
        string src=AssetDatabase.GetAssetPath(td);
        string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string dst=bkDir+"/Seocheon_Village_Terrain_MtnGenBK_"+stamp+".asset";
        if(!AssetDatabase.CopyAsset(src,dst)){Debug.LogError("[MtnGen] 백업 실패 — 중단");return;}
        AssetDatabase.SaveAssets();
        log.AppendLine("BACKUP: "+dst);

        // ---- 마커 읽기 ----
        var mk=GameObject.Find("_MtnMarkers"); if(mk==null){Debug.LogError("[MtnGen] _MtnMarkers 없음");return;}
        var path=ReadOrdered(mk.transform,"_MtnPath_");
        var ridge=ReadOrdered(mk.transform,"_MtnRidge_");
        Transform terMud=mk.transform.Find("_Terrace_Mudang"), terTomb=mk.transform.Find("_Terrace_Tomb");
        if(path.Count<2||ridge.Count<2||terMud==null||terTomb==null){Debug.LogError("[MtnGen] 마커 부족");return;}

        int R=td.heightmapResolution; Vector3 size=td.size; Vector3 tpos=terrain.transform.position;
        float cx=size.x/(R-1), cz=size.z/(R-1);
        float[,] H0=td.GetHeights(0,0,R,R);
        float[,] H=(float[,])H0.Clone();

        // ---- 개천 마스크 (실제 링 + 8m) ----
        bool[,] mask=BuildStreamMask(R,size,tpos,cx,cz);

        // ---- 처리 영역 ----
        var pathXZ=ToXZ(path); var ridgeXZ=ToXZ(ridge);
        Vector2 mudXZ=new Vector2(terMud.position.x,terMud.position.z), tombXZ=new Vector2(terTomb.position.x,terTomb.position.z);
        var allXZ=new List<Vector2>(pathXZ); allXZ.AddRange(ridgeXZ); allXZ.Add(mudXZ); allXZ.Add(tombXZ);
        float minx=1e9f,maxx=-1e9f,minz=1e9f,maxz=-1e9f;
        foreach(var p in allXZ){minx=Mathf.Min(minx,p.x);maxx=Mathf.Max(maxx,p.x);minz=Mathf.Min(minz,p.y);maxz=Mathf.Max(maxz,p.y);}
        // 여백: 좌우/뒤(남)는 능선 falloff용으로 넉넉, 북(마을쪽)은 기슭까지만 (개천 침범 방지)
        float xMarg=32f, sMarg=45f, nMarg=6f;
        int c0=Mathf.Clamp((int)((minx-xMarg-tpos.x)/cx),0,R-1), c1=Mathf.Clamp((int)((maxx+xMarg-tpos.x)/cx)+1,0,R-1);
        int r0=Mathf.Clamp((int)((minz-sMarg-tpos.z)/cz),0,R-1), r1=Mathf.Clamp((int)((maxz+nMarg-tpos.z)/cz)+1,0,R-1);

        // ---- ridge cumulative length + foot ----
        float[] rcum=Cumulative(ridgeXZ); float rlen=rcum[rcum.Length-1];
        float footBase=terrain.SampleHeight(new Vector3(pathXZ[0].x,0,pathXZ[0].y))+tpos.y;

        // ===== A: 능선 = 마을 쪽(북)으로 내려오는 사면 램프 (마운드/크레이터 방지) =====
        // crest = footBase+25, 능선선에서 북으로 95m 걸쳐 기슭높이로 하강, 남(뒤)으론 30m 걸쳐 원지형과 연결.
        for(int row=r0;row<=r1;row++){ float wz=row*cz+tpos.z;
          for(int cc=c0;cc<=c1;cc++){ if(mask[row,cc])continue; float wx=cc*cx+tpos.x;
            float h0=H0[row,cc]*size.y+tpos.y;
            float tgt=RidgeTarget(new Vector2(wx,wz),ridgeXZ,rcum,rlen,footBase,h0);
            float nh=Mathf.Max(h0,tgt);
            H[row,cc]=Mathf.Clamp01((nh-tpos.y)/size.y);
          }
        }

        // ===== B: 노면 (경로 확정 -> 지형 맞춤, 12% 클램프) =====
        float[] pcum=Cumulative(pathXZ);
        float[] nodeH=new float[pathXZ.Count];
        nodeH[0]=SampleGrid(H,pathXZ[0],R,size,tpos,cx,cz);
        float maxGrade=0f;
        for(int i=1;i<pathXZ.Count;i++){
            float segLen=(pathXZ[i]-pathXZ[i-1]).magnitude;
            float ceil=SampleGrid(H,pathXZ[i],R,size,tpos,cx,cz);
            float lo=nodeH[i-1]-MAX_GRADE*segLen, hi=nodeH[i-1]+MAX_GRADE*segLen;
            nodeH[i]=Mathf.Clamp(ceil,lo,hi);
            float g=Mathf.Abs(nodeH[i]-nodeH[i-1])/segLen; if(g>maxGrade)maxGrade=g;
        }
        // carve road
        for(int row=r0;row<=r1;row++){ float wz=row*cz+tpos.z;
          for(int cc=c0;cc<=c1;cc++){ if(mask[row,cc])continue; float wx=cc*cx+tpos.x;
            int seg; float t,dp; NearestPolyline(new Vector2(wx,wz),pathXZ,out seg,out t,out dp);
            if(dp>ROAD_HALF+SMOOTH) continue;
            float roadH=Mathf.Lerp(nodeH[seg],nodeH[seg+1],t);
            float cur=H[row,cc]*size.y+tpos.y; float nh;
            if(dp<=ROAD_HALF) nh=roadH;
            else{ float w=1f-Smoothstep(ROAD_HALF,ROAD_HALF+SMOOTH,dp); nh=Mathf.Lerp(cur,roadH,w); }
            H[row,cc]=Mathf.Clamp01((nh-tpos.y)/size.y);
          }
        }

        // ===== D: 터 2개 (반경9 SetHeight, 가장자리3 smooth) =====
        float mudH=RoadHeightNear(mudXZ,pathXZ,nodeH);
        float tombH=RoadHeightNear(tombXZ,pathXZ,nodeH);
        StampTerrace(H,mudXZ,mudH,R,size,tpos,cx,cz,mask,r0,r1,c0,c1);
        StampTerrace(H,tombXZ,tombH,R,size,tpos,cx,cz,mask,r0,r1,c0,c1);

        td.SetHeights(0,0,H); td.SyncHeightmap(); terrain.Flush();
        var tcol=terrain.GetComponent<TerrainCollider>(); if(tcol!=null) tcol.terrainData=td;
        EditorUtility.SetDirty(td);

        // ===== E: 흙 스플랫 — 비활성화됨 (ENABLE_SPLAT_PAINT=false). 스플랫은 백업 대입으로만. =====
        if(ENABLE_SPLAT_PAINT){
        int aRes=td.alphamapResolution; var A=td.GetAlphamaps(0,0,aRes,aRes);
        int nLayers=td.alphamapLayers;
        for(int az=0;az<aRes;az++){ float wz=(float)az/(aRes-1)*size.z+tpos.z;
          if(wz<minz-sMarg||wz>maxz+nMarg) continue;
          for(int ax=0;ax<aRes;ax++){ float wx=(float)ax/(aRes-1)*size.x+tpos.x;
            if(wx<minx-xMarg||wx>maxx+xMarg) continue;
            // mask guard (approx map alpha cell to height mask)
            int hr=Mathf.Clamp((int)((wz-tpos.z)/cz),0,R-1), hc=Mathf.Clamp((int)((wx-tpos.x)/cx),0,R-1);
            if(mask[hr,hc]) continue;
            int seg; float t,dp; NearestPolyline(new Vector2(wx,wz),pathXZ,out seg,out t,out dp);
            float dter=Mathf.Min((new Vector2(wx,wz)-mudXZ).magnitude,(new Vector2(wx,wz)-tombXZ).magnitude);
            float d=Mathf.Min(dp, dter<=TER_R? 0f : dter-TER_R);
            // irregular feather
            float noise=(Mathf.PerlinNoise(wx*0.15f,wz*0.15f)-0.5f)*1.6f;
            float wRoad=ROAD_HALF+SMOOTH;
            float dirtEdge=wRoad+2.0f+noise;
            float dirt;
            if(d<=wRoad*0.6f) dirt=1f;
            else if(d<=dirtEdge){ float tt=(d-wRoad*0.6f)/Mathf.Max(0.3f,dirtEdge-wRoad*0.6f); dirt=1f-Mathf.Clamp01(tt); }
            else continue;
            float cur=A[az,ax,DIRT]; float nd=Mathf.Max(cur,dirt);
            float rest=1f-nd; float others=0f; for(int L=0;L<nLayers;L++) if(L!=DIRT) others+=A[az,ax,L];
            A[az,ax,DIRT]=nd;
            if(others>0.0001f){ float k=rest/others; for(int L=0;L<nLayers;L++) if(L!=DIRT) A[az,ax,L]*=k; }
            else { A[az,ax,DIRT]=1f; }
          }
        }
        td.SetAlphamaps(0,0,A);
        } // if(ENABLE_SPLAT_PAINT)
        EditorUtility.SetDirty(td); AssetDatabase.SaveAssets(); SceneView.RepaintAll();

        // ================= 보고 =================
        log.AppendLine();
        log.AppendLine("== 마커별 최종 지형 높이 ==");
        foreach(Transform t in mk.transform){
            float h=terrain.SampleHeight(new Vector3(t.position.x,0,t.position.z))+tpos.y;
            log.AppendLine(string.Format("  {0,-16} ({1:F0},{2:F0})  H={3:F2}",t.name,t.position.x,t.position.z,h));
        }
        log.AppendLine();
        log.AppendLine("무당집 터 H="+mudH.ToString("F2")+"  묘역 터 H="+tombH.ToString("F2")+"  (묘역-무당집="+(tombH-mudH).ToString("F2")+"m)");
        log.AppendLine("노면 최대 경사 = "+(maxGrade*100f).ToString("F1")+"%  (클램프 "+(MAX_GRADE*100f)+"%)");
        // road cut depth
        float maxCut=0f; for(int i=0;i<pathXZ.Count;i++){ float na=SampleGrid(H,pathXZ[i],R,size,tpos,cx,cz); /*after*/ }

        // 굽이별 시선 차단 (1.6m)
        log.AppendLine();
        log.AppendLine("== 굽이별 시선 차단 (카메라 1.6m, Linecast) ==");
        for(int i=1;i<pathXZ.Count-1;i++){
            Vector3 a=RoadPt(pathXZ[i-1],nodeH[i-1])+Vector3.up*1.6f;
            Vector3 b=RoadPt(pathXZ[i+1],nodeH[i+1])+Vector3.up*1.6f;
            bool blocked=Physics.Linecast(a,b);
            log.AppendLine(string.Format("  굽이 node{0}: 앞구간(node{1}) {2}",i,i+1, blocked?"가려짐(차단O)":"보임(차단X)"));
        }
        // 터 은폐: 기슭/직전에서 터가 보이나
        Vector3 foot=RoadPt(pathXZ[0],nodeH[0])+Vector3.up*1.6f;
        bool mudFromFoot=Physics.Linecast(foot, new Vector3(mudXZ.x,mudH+1.0f,mudXZ.y));
        bool tombFromFoot=Physics.Linecast(foot, new Vector3(tombXZ.x,tombH+1.0f,tombXZ.y));
        log.AppendLine("  기슭에서 무당집터 "+(mudFromFoot?"가려짐":"보임")+" / 묘역터 "+(tombFromFoot?"가려짐":"보임"));

        // 개천 최소거리
        var sw=GameObject.Find("_Stream_Water");
        if(sw!=null){ var mf2=sw.GetComponentInChildren<MeshFilter>(); var mm=mf2.transform.localToWorldMatrix; var vv=mf2.sharedMesh.vertices;
            var wpts=new List<Vector2>(); for(int i=0;i<vv.Length;i+=8){ var w=mm.MultiplyPoint3x4(vv[i]); wpts.Add(new Vector2(w.x,w.z)); }
            float minD2=1e18f, ax2=0,az2=0;
            for(int row=r0;row<=r1;row++) for(int cc=c0;cc<=c1;cc++){ if(mask[row,cc])continue;
                if(Mathf.Abs(H[row,cc]-H0[row,cc])<=0.0005f) continue;
                float wx=cc*cx+tpos.x, wz=row*cz+tpos.z;
                for(int q=0;q<wpts.Count;q++){ float dx=wx-wpts[q].x, dz=wz-wpts[q].y; float d2=dx*dx+dz*dz; if(d2<minD2){minD2=d2;ax2=wx;az2=wz;} }
            }
            log.AppendLine("개천 수면까지 실제 XZ 최소거리 ≈ "+Mathf.Sqrt(minD2).ToString("F1")+"m  (가장 가까운 편집셀 "+ax2.ToString("F0")+","+az2.ToString("F0")+")  [8m 이상 정상]");
        }

        Directory.CreateDirectory(RENDER_DIR);
        File.WriteAllText(RENDER_DIR+"report.txt",log.ToString());
        Debug.Log("[MtnGen]\n"+log.ToString());
    }

    // 흙 스플랫만 좁게 다시 칠함 (하이트맵 불변). 길 밖 번진 흙은 잔디로 복귀.
    // 백업 대입 전용 복원 (스플랫을 바꾸는 유일하게 허용된 경로)
    [MenuItem("Tools/Seocheon/Mtn/RestoreSplatFromWide")]
    public static void RestoreSplatFromWide()
    {
        var terrain=Terrain.activeTerrain; var td=terrain.terrainData;
        const string bkPath="Assets/_Project/Seocheon/Art/Terrain/_Backup/Seocheon_Village_Terrain_MtnWideSplat_20260815_164505.asset";
        var bk=AssetDatabase.LoadAssetAtPath<TerrainData>(bkPath);
        if(bk==null){ Debug.LogError("[RestoreSplat] 백업 없음: "+bkPath); return; }
        var A=bk.GetAlphamaps(0,0,bk.alphamapResolution,bk.alphamapResolution);
        td.SetAlphamaps(0,0,A);
        EditorUtility.SetDirty(td); AssetDatabase.SaveAssets(); SceneView.RepaintAll();
        Debug.Log("[RestoreSplat] MtnWideSplat 백업 alphamap 통째 대입 완료 (재칠 아님).");
    }

    // ★비활성화됨 — 스플랫 재칠 금지
    // 흙길 노면 + 터 2곳에만 흙(DIRT)을 "더하기만" 한다.
    //  - 길·터 밖(d>dirtEdge)은 alphamap을 아예 건드리지 않는다(continue → 기존 값 유지).
    //  - 잔디(레이어0)에 값을 대입하는 줄이 하나도 없다. 흙을 올린 만큼 다른 레이어를 비율 축소만 함(합=1 유지).
    //  - 하이트맵은 건드리지 않는다.
    [MenuItem("Tools/Seocheon/Mtn/PaintDirtAdditive")]
    public static void PaintDirtAdditive()
    {
        var terrain=Terrain.activeTerrain; var td=terrain.terrainData;
        var mk=GameObject.Find("_MtnMarkers"); if(mk==null){Debug.LogError("[DirtAdd] no markers");return;}
        var path=ReadOrdered(mk.transform,"_MtnPath_");
        Transform terMud=mk.transform.Find("_Terrace_Mudang"), terTomb=mk.transform.Find("_Terrace_Tomb");
        var pathXZ=ToXZ(path);
        Vector2 mudXZ=new Vector2(terMud.position.x,terMud.position.z), tombXZ=new Vector2(terTomb.position.x,terTomb.position.z);
        int R=td.heightmapResolution; Vector3 size=td.size; Vector3 tpos=terrain.transform.position; float cx=size.x/(R-1), cz=size.z/(R-1);
        bool[,] mask=BuildStreamMask(R,size,tpos,cx,cz);
        int aRes=td.alphamapResolution; var A=td.GetAlphamaps(0,0,aRes,aRes); int nL=td.alphamapLayers;
        // region (마커 AABB + 여유). 이 박스는 순회 범위 최적화용일 뿐, 박스 안이라도 길·터 밖이면 손 안 댐.
        var all=new List<Vector2>(pathXZ); all.Add(mudXZ); all.Add(tombXZ);
        float minx=1e9f,maxx=-1e9f,minz=1e9f,maxz=-1e9f; foreach(var p in all){minx=Mathf.Min(minx,p.x);maxx=Mathf.Max(maxx,p.x);minz=Mathf.Min(minz,p.y);maxz=Mathf.Max(maxz,p.y);}
        float xM=20f,sM=20f,nM=6f;
        int touched=0; double addSum=0;
        for(int az=0;az<aRes;az++){ float wz=(float)az/(aRes-1)*size.z+tpos.z; if(wz<minz-sM||wz>maxz+nM) continue;
          for(int ax=0;ax<aRes;ax++){ float wx=(float)ax/(aRes-1)*size.x+tpos.x; if(wx<minx-xM||wx>maxx+xM) continue;
            int hr=Mathf.Clamp((int)((wz-tpos.z)/cz),0,R-1), hc=Mathf.Clamp((int)((wx-tpos.x)/cx),0,R-1); if(mask[hr,hc]) continue; // 개천+8m 잠금
            int seg; float t,dp; NearestPolyline(new Vector2(wx,wz),pathXZ,out seg,out t,out dp);
            float dter=Mathf.Min((new Vector2(wx,wz)-mudXZ).magnitude,(new Vector2(wx,wz)-tombXZ).magnitude);
            float d=Mathf.Min(dp, dter<=TER_R? 0f : dter-TER_R);   // 길 또는 터까지의 최소 거리
            float noise=(Mathf.PerlinNoise(wx*0.15f,wz*0.15f)-0.5f)*1.6f;
            float wRoad=ROAD_HALF+SMOOTH;
            float dirtEdge=wRoad+2.0f+noise;                        // 자연스러운 넓은 번짐
            float dirt;
            if(d<=wRoad*0.6f) dirt=1f;
            else if(d<=dirtEdge) dirt=1f-Mathf.Clamp01((d-wRoad*0.6f)/Mathf.Max(0.3f,dirtEdge-wRoad*0.6f));
            else continue;                                          // ★길·터 밖: alphamap 손 안 댐 (기존 값 유지)
            dirt=Mathf.Clamp01(dirt);
            float cur=A[az,ax,DIRT];
            float nd=Mathf.Max(cur,dirt);                           // ★흙은 더하기만 — 기존보다 절대 줄이지 않음
            if(nd<=cur+1e-5f) continue;                             // 추가될 흙 없음 → 기존 그대로 둠
            float rest=1f-nd; float others=0f; for(int L=0;L<nL;L++) if(L!=DIRT) others+=A[az,ax,L];
            A[az,ax,DIRT]=nd;
            if(others>0.0001f){ float k=rest/others; for(int L=0;L<nL;L++) if(L!=DIRT) A[az,ax,L]*=k; } // 잔디=대입 아님, 비율 축소만
            else { A[az,ax,DIRT]=1f; }
            touched++; addSum+=(nd-cur);
          }
        }
        td.SetAlphamaps(0,0,A); EditorUtility.SetDirty(td); AssetDatabase.SaveAssets(); SceneView.RepaintAll();
        Debug.Log("[DirtAdd] 흙 추가 셀 "+touched+"개, 총 추가가중치 "+addSum.ToString("F1")+"  |  잔디 대입 0줄, 길·터 밖 alphamap 불변, 하이트맵 불변.");
    }

    // ---------- helpers ----------
    static List<Transform> ReadOrdered(Transform root,string prefix){
        var l=new List<Transform>(); foreach(Transform t in root) if(t.name.StartsWith(prefix)) l.Add(t);
        l.Sort((a,b)=>string.Compare(a.name,b.name)); return l;
    }
    static List<Vector2> ToXZ(List<Transform> ts){ var l=new List<Vector2>(); foreach(var t in ts) l.Add(new Vector2(t.position.x,t.position.z)); return l; }
    static float[] Cumulative(List<Vector2> p){ var c=new float[p.Count]; c[0]=0; for(int i=1;i<p.Count;i++) c[i]=c[i-1]+(p[i]-p[i-1]).magnitude; return c; }
    static float Smoothstep(float a,float b,float x){ float t=Mathf.Clamp01((x-a)/(b-a)); return t*t*(3-2*t); }
    static Vector3 RoadPt(Vector2 xz,float h){ return new Vector3(xz.x,h,xz.y); }

    static void NearestPolyline(Vector2 p,List<Vector2> poly,out int seg,out float t,out float dist){
        seg=0;t=0;dist=1e9f;
        for(int i=0;i<poly.Count-1;i++){ Vector2 a=poly[i],b=poly[i+1]; Vector2 v=b-a; float len2=v.sqrMagnitude;
            float tt=len2<1e-6f?0f:Mathf.Clamp01(Vector2.Dot(p-a,v)/len2); Vector2 cp=a+v*tt; float d=(p-cp).magnitude;
            if(d<dist){dist=d;seg=i;t=tt;} }
    }
    static float RidgeTarget(Vector2 p,List<Vector2> ridge,float[] cum,float total,float footBase,float h0){
        int seg;float t,dp; NearestPolyline(p,ridge,out seg,out t,out dp);
        float u=total<1e-4f?0f:(cum[seg]+t*(cum[seg+1]-cum[seg]))/total;
        float e=Mathf.Clamp01(Mathf.Min(u,1-u)/0.25f); float taper=e*e*(3-2*e);
        float crestH=footBase+RIDGE_RISE*taper;
        float Pz=Mathf.Lerp(ridge[seg].y,ridge[seg+1].y,t);
        if(p.y>=Pz){ float frac=Mathf.Clamp01((p.y-Pz)/95f); return Mathf.Lerp(crestH,footBase,frac); } // 북사면(마을쪽)
        else { float frac=Mathf.Clamp01((Pz-p.y)/30f); return Mathf.Lerp(crestH,h0,frac); }            // 남(뒤) 연결
    }
    static float SampleGrid(float[,] H,Vector2 xz,int R,Vector3 size,Vector3 tpos,float cx,float cz){
        int c=Mathf.Clamp((int)((xz.x-tpos.x)/cx),0,R-1), r=Mathf.Clamp((int)((xz.y-tpos.z)/cz),0,R-1);
        return H[r,c]*size.y+tpos.y;
    }
    static float RoadHeightNear(Vector2 xz,List<Vector2> path,float[] nodeH){
        int seg;float t,dp; NearestPolyline(xz,path,out seg,out t,out dp); return Mathf.Lerp(nodeH[seg],nodeH[seg+1],t);
    }
    static void StampTerrace(float[,] H,Vector2 c,float h,int R,Vector3 size,Vector3 tpos,float cx,float cz,bool[,] mask,int r0,int r1,int c0,int c1){
        for(int row=r0;row<=r1;row++){ float wz=row*cz+tpos.z;
          for(int cc=c0;cc<=c1;cc++){ if(mask[row,cc])continue; float wx=cc*cx+tpos.x; float d=Mathf.Sqrt((wx-c.x)*(wx-c.x)+(wz-c.y)*(wz-c.y));
            if(d<=TER_R) H[row,cc]=Mathf.Clamp01((h-tpos.y)/size.y);
            else if(d<=TER_R+TER_EDGE){ float w=1f-Smoothstep(TER_R,TER_R+TER_EDGE,d); float cur=H[row,cc]*size.y+tpos.y; float nh=Mathf.Lerp(cur,h,w); H[row,cc]=Mathf.Clamp01((nh-tpos.y)/size.y); }
          }
        }
    }
    static bool[,] BuildStreamMask(int R,Vector3 size,Vector3 tpos,float cx,float cz){
        var mask=new bool[R,R]; var sw=GameObject.Find("_Stream_Water"); if(sw==null) return mask;
        foreach(var mr in sw.GetComponentsInChildren<MeshRenderer>(true)){
            var mf=mr.GetComponent<MeshFilter>(); if(mf==null||mf.sharedMesh==null)continue;
            var v=mf.sharedMesh.vertices; var tr=mf.sharedMesh.triangles; var m=mr.transform.localToWorldMatrix;
            for(int i=0;i<tr.Length;i+=3){ Vector3 a=m.MultiplyPoint3x4(v[tr[i]]),b=m.MultiplyPoint3x4(v[tr[i+1]]),c=m.MultiplyPoint3x4(v[tr[i+2]]);
                RasterTri(mask,R,size,tpos,cx,cz,a.x,a.z,b.x,b.z,c.x,c.z); }
        }
        int k=Mathf.CeilToInt(STREAM_KEEP/Mathf.Min(cx,cz)); Dilate(mask,R,k); return mask;
    }
    static void RasterTri(bool[,] mask,int R,Vector3 size,Vector3 tpos,float cx,float cz,float ax,float az,float bx,float bz,float cx2,float cz2){
        float mnx=Mathf.Min(ax,Mathf.Min(bx,cx2)),mxx=Mathf.Max(ax,Mathf.Max(bx,cx2)),mnz=Mathf.Min(az,Mathf.Min(bz,cz2)),mxz=Mathf.Max(az,Mathf.Max(bz,cz2));
        int C0=Mathf.Clamp((int)((mnx-tpos.x)/cx),0,R-1),C1=Mathf.Clamp((int)((mxx-tpos.x)/cx)+1,0,R-1),Rr0=Mathf.Clamp((int)((mnz-tpos.z)/cz),0,R-1),Rr1=Mathf.Clamp((int)((mxz-tpos.z)/cz)+1,0,R-1);
        for(int row=Rr0;row<=Rr1;row++){ float wz=row*cz+tpos.z; for(int col=C0;col<=C1;col++){ float wx=col*cx+tpos.x; if(PtInTri(wx,wz,ax,az,bx,bz,cx2,cz2)) mask[row,col]=true; } }
    }
    static bool PtInTri(float px,float pz,float ax,float az,float bx,float bz,float cx,float cz){
        float d1=(px-bx)*(az-bz)-(ax-bx)*(pz-bz),d2=(px-cx)*(bz-cz)-(bx-cx)*(pz-cz),d3=(px-ax)*(cz-az)-(cx-ax)*(pz-az);
        bool neg=(d1<0)||(d2<0)||(d3<0),pos=(d1>0)||(d2>0)||(d3>0); return !(neg&&pos);
    }
    static void Dilate(bool[,] mask,int R,int k){
        for(int s=0;s<k;s++){ var nm=(bool[,])mask.Clone();
          for(int row=0;row<R;row++) for(int col=0;col<R;col++){ if(mask[row,col])continue; bool any=false;
            for(int dz=-1;dz<=1&&!any;dz++) for(int dx=-1;dx<=1;dx++){ int rr=row+dz,cc=col+dx; if(rr<0||cc<0||rr>=R||cc>=R)continue; if(mask[rr,cc]){any=true;break;} }
            if(any)nm[row,col]=true; }
          Array.Copy(nm,mask,mask.Length);
        }
    }
}
