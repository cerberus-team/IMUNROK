// SeocheonTrailFoliage.cs
// 성 밖 산길 수목·지면피복. 지형 하이트맵은 읽기 전용(수정 안 함).
// 마스크는 씬 랜드마크/마커/스플랫에서 데이터 기반으로 유도(좌표 하드코딩 금지).
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class SeocheonTrailFoliage
{
    // ---- config ----
    const float MAXZ = 558f;          // 성 밖(개천 남쪽)만
    const float CORRIDOR = 30f;       // 길 중심선 좌우 회랑(밀도는 25m부터 급감)
    const float TERRACE_R = 12f;      // 터 제외 반경
    const float STREAM_BUF = 8f;      // 개천 링 +8m
    const float ROADCTR_KEEP = 1.5f;  // 노면 중심 ±1.5m 식재 금지
    const int   DENSITY = 4700;       // 다트 시도수(측정→감축 루프에서 조정)

    static readonly string[] CANOPY = {
        "SM_Salixpierotii_Summer_2","SM_UlmusDavidiana_Summer_1","SM_MeliaAzedarach_Summer_1",
        "SM_AphanantheAspera_Summer_1","SM_Dendropanaxtrifidus_Summer_1" };
    static readonly float[] CANOPY_W = { 34f,18f,16f,16f,16f };
    static readonly string[] SHRUB = {
        "SM_Viburnumodoratissimum_Summer_1","SM_Camelliajaponica_Summer_1" };
    static readonly float[] SHRUB_W = { 55f,45f };
    const string PREF_DIR = "Assets/SeyeonjeongPavilion/Prefabs/";

    static Terrain _terr; static TerrainData _td;
    static void Grab(){ _terr = Object.FindObjectOfType<Terrain>(); _td = _terr.terrainData; }
    static float H(float x,float z){ return _terr.SampleHeight(new Vector3(x,0,z)) + _terr.transform.position.y; }

    // ---------- keep-out landmarks ----------
    struct Rect { public float x0,x1,z0,z1; }
    static List<Rect> _keepRects; static Vector2 _mud,_tomb;
    static void BuildKeepouts()
    {
        _keepRects = new List<Rect>();
        AddRect("_Village_Sedae",2f); AddRect("_Village_Landscape",0f);
        AddRect("GwanaGateSet",2f); AddRect("_Bridge",3f);
        AddRect("_Stream_Water",STREAM_BUF);
        _mud = MarkerXZ("_Terrace_Mudang"); _tomb = MarkerXZ("_Terrace_Tomb");
    }
    static void AddRect(string name,float pad){
        var go = GameObject.Find(name); if(go==null){ Debug.LogWarning("keepout missing: "+name); return; }
        var rs = go.GetComponentsInChildren<Renderer>(); if(rs.Length==0) return;
        var b = rs[0].bounds; foreach(var r in rs) b.Encapsulate(r.bounds);
        _keepRects.Add(new Rect{ x0=b.min.x-pad, x1=b.max.x+pad, z0=b.min.z-pad, z1=b.max.z+pad });
    }
    static Vector2 MarkerXZ(string n){ var g=GameObject.Find(n); return g!=null? new Vector2(g.transform.position.x,g.transform.position.z): Vector2.zero; }

    // ---------- trail polyline (bridge south -> tomb -> mudang), from markers ----------
    static List<Vector2> _trail;
    static void BuildTrail(){
        _trail=new List<Vector2>();
        var bs=GameObject.Find("Bridge_South");
        Vector2 p0 = bs!=null? new Vector2(bs.transform.position.x,bs.transform.position.z) : MarkerXZ("_Bridge");
        _trail.Add(p0); _trail.Add(_tomb); _trail.Add(_mud);
    }
    static float DistTrail(float x,float z){
        var p=new Vector2(x,z); float best=1e9f;
        for(int i=0;i<_trail.Count-1;i++){
            Vector2 a=_trail[i],b=_trail[i+1]; Vector2 ab=b-a; float t=Vector2.Dot(p-a,ab)/Mathf.Max(1e-4f,ab.sqrMagnitude);
            t=Mathf.Clamp01(t); float d=(p-(a+ab*t)).magnitude; if(d<best) best=d;
        }
        return best;
    }
    static bool InKeepout(float x,float z){
        if(z>=MAXZ) return true;
        if((new Vector2(x,z)-_mud).magnitude<TERRACE_R) return true;
        if((new Vector2(x,z)-_tomb).magnitude<TERRACE_R) return true;
        foreach(var r in _keepRects) if(x>=r.x0&&x<=r.x1&&z>=r.z0&&z<=r.z1) return true;
        return false;
    }

    // ---------- road distance field (from painted dirt, layer 1) ----------
    static int _aw; static float _cell; static float[,] _distRoad; static float[,] _edgeDepth; static bool[,] _dirt;
    static void BuildRoadField()
    {
        _aw = _td.alphamapResolution; _cell = _td.size.x/_aw;
        var maps = _td.GetAlphamaps(0,0,_aw,_aw); // [z,x,layer]
        _dirt = new bool[_aw,_aw];
        var dist = new float[_aw,_aw];
        float BIG = 1e9f;
        for(int z=0;z<_aw;z++) for(int x=0;x<_aw;x++){
            float wx=(x+0.5f)*_cell, wz=(z+0.5f)*_cell;
            bool isDirt = maps[z,x,1] > 0.5f;
            // terrace dirt is NOT road
            if(isDirt && ((new Vector2(wx,wz)-_mud).magnitude<TERRACE_R || (new Vector2(wx,wz)-_tomb).magnitude<TERRACE_R)) isDirt=false;
            _dirt[z,x]=isDirt;
            dist[z,x] = isDirt?0f:BIG;
        }
        _distRoad = Chamfer(dist,_cell);
        // edgeDepth = distance INSIDE dirt to nearest non-dirt
        var din = new float[_aw,_aw];
        for(int z=0;z<_aw;z++) for(int x=0;x<_aw;x++) din[z,x] = _dirt[z,x]?BIG:0f;
        _edgeDepth = Chamfer(din,_cell);
    }
    static float[,] Chamfer(float[,] d,float cell){
        int n=d.GetLength(0); float da=cell, db=cell*1.41421356f;
        for(int z=1;z<n;z++) for(int x=0;x<n;x++){
            float v=d[z,x];
            v=Mathf.Min(v,d[z-1,x]+da);
            if(x>0)   v=Mathf.Min(v,d[z-1,x-1]+db);
            if(x<n-1) v=Mathf.Min(v,d[z-1,x+1]+db);
            if(x>0)   v=Mathf.Min(v,d[z,x-1]+da);
            d[z,x]=v;
        }
        for(int z=n-2;z>=0;z--) for(int x=n-1;x>=0;x--){
            float v=d[z,x];
            v=Mathf.Min(v,d[z+1,x]+da);
            if(x<n-1) v=Mathf.Min(v,d[z+1,x+1]+db);
            if(x>0)   v=Mathf.Min(v,d[z+1,x-1]+db);
            if(x<n-1) v=Mathf.Min(v,d[z,x+1]+da);
            d[z,x]=v;
        }
        return d;
    }
    static float DistRoad(float x,float z){
        int ax=Mathf.Clamp(Mathf.FloorToInt(x/_cell),0,_aw-1);
        int az=Mathf.Clamp(Mathf.FloorToInt(z/_cell),0,_aw-1);
        return _distRoad[az,ax];
    }
    static bool OnRoad(float x,float z){ return DistRoad(x,z) < ROADCTR_KEEP; }

    static float SlopeDeg(float x,float z){
        float e=2f;
        float hL=H(x-e,z),hR=H(x+e,z),hD=H(x,z-e),hU=H(x,z+e);
        float dx=(hR-hL)/(2*e), dz=(hU-hD)/(2*e);
        return Mathf.Atan(Mathf.Sqrt(dx*dx+dz*dz))*Mathf.Rad2Deg;
    }

    // density from road distance
    static float DensDist(float dR){
        if(dR<ROADCTR_KEEP) return 0f;
        if(dR<3f)  return Mathf.Lerp(0.55f,1f,(dR-ROADCTR_KEEP)/(3f-ROADCTR_KEEP));
        if(dR<8f)  return 1f;
        if(dR<25f) return Mathf.Lerp(1f,0.35f,(dR-8f)/17f);
        if(dR<CORRIDOR) return Mathf.Lerp(0.35f,0f,(dR-25f)/(CORRIDOR-25f));
        return 0f;
    }

    // ==================================================================
    [MenuItem("Seocheon/Foliage/1 Narrow Path (splat only)")]
    public static void NarrowPath()
    {
        Grab(); BuildKeepouts(); BuildTrail(); BuildRoadField();
        var maps=_td.GetAlphamaps(0,0,_aw,_aw);
        int nL=_td.alphamapLayers; // 0 grass,1 dirt,2 leaf
        int changed=0;
        for(int z=0;z<_aw;z++) for(int x=0;x<_aw;x++){
            if(!_dirt[z,x]) continue;
            float wx=(x+0.5f)*_cell, wz=(z+0.5f)*_cell;
            if(wz>=MAXZ) continue;                 // don't touch village-side dirt
            float keep = 0.72f + (Mathf.PerlinNoise(wx*0.9f,wz*0.9f)-0.5f)*0.9f; // irregular edge
            if(_edgeDepth[z,x] < keep){
                // peel: dirt -> mostly leaf + some grass
                float d=maps[z,x,1]; maps[z,x,1]=0f;
                maps[z,x,0]+=d*0.35f; if(nL>2) maps[z,x,2]+=d*0.65f; else maps[z,x,0]+=d*0.65f;
                changed++;
            }
        }
        Normalize(maps);
        _td.SetAlphamaps(0,0,maps); EditorUtility.SetDirty(_td); AssetDatabase.SaveAssets();
        Debug.Log("[Foliage] NarrowPath peeled cells="+changed);
    }

    [MenuItem("Seocheon/Foliage/2 Paint Leaf Ground")]
    public static void PaintLeaf()
    {
        Grab(); BuildKeepouts(); BuildTrail(); BuildRoadField();
        var maps=_td.GetAlphamaps(0,0,_aw,_aw);
        int nL=_td.alphamapLayers; if(nL<3){ Debug.LogError("leaf layer missing"); return; }
        // reset any existing leaf (layer2) back to grass in the 성 밖 area (idempotent re-run)
        for(int z=0;z<_aw;z++) for(int x=0;x<_aw;x++){ float wz=(z+0.5f)*_cell; if(wz>=MAXZ) continue; if(maps[z,x,2]>0f){ maps[z,x,0]+=maps[z,x,2]; maps[z,x,2]=0f; } }
        int painted=0;
        for(int z=0;z<_aw;z++) for(int x=0;x<_aw;x++){
            float wx=(x+0.5f)*_cell, wz=(z+0.5f)*_cell;
            if(wz>=MAXZ) continue;
            if(_dirt[z,x]) continue;                          // keep road surface
            if(InKeepout(wx,wz)) continue;
            float dT=DistTrail(wx,wz);
            if(dT>=CORRIDOR) continue;
            // leaf coverage strongest near path, irregular; fades out by corridor edge
            float near = Mathf.Clamp01(1f-(dT/CORRIDOR));
            float blotch = Mathf.PerlinNoise(wx*0.18f+5f, wz*0.18f+5f);
            float leaf = Mathf.Clamp01((near*0.7f+0.3f) * (0.45f+blotch*0.8f));
            if(leaf<0.05f) continue;
            // feather near actual road edge so it blends with dirt shoulder
            float shoulder = Mathf.Clamp01((_distRoad[z,x]-1.0f)/2.0f);
            leaf*=shoulder;
            float g=maps[z,x,0];
            float take=Mathf.Min(g, leaf);
            maps[z,x,0]=g-take; maps[z,x,2]+=take;
            if(take>0.001f) painted++;
        }
        Normalize(maps);
        _td.SetAlphamaps(0,0,maps); EditorUtility.SetDirty(_td); AssetDatabase.SaveAssets();
        Debug.Log("[Foliage] PaintLeaf cells="+painted);
    }

    static void Normalize(float[,,] m){
        int H2=m.GetLength(0),W2=m.GetLength(1),L=m.GetLength(2);
        for(int z=0;z<H2;z++) for(int x=0;x<W2;x++){
            float s=0; for(int l=0;l<L;l++){ if(m[z,x,l]<0)m[z,x,l]=0; s+=m[z,x,l]; }
            if(s<=1e-5f){ m[z,x,0]=1f; continue; }
            for(int l=0;l<L;l++) m[z,x,l]/=s;
        }
    }

    // ==================================================================
    [MenuItem("Seocheon/Foliage/3 Plant Trees")]
    public static void PlantTrees()
    {
        Grab(); BuildKeepouts(); BuildTrail(); BuildRoadField();
        // prototypes
        var protoPaths=new List<string>();
        foreach(var n in CANOPY) protoPaths.Add(PREF_DIR+n+".prefab");
        foreach(var n in SHRUB)  protoPaths.Add(PREF_DIR+n+".prefab");
        var protos=new List<TreePrototype>();
        foreach(var p in protoPaths){ var go=AssetDatabase.LoadAssetAtPath<GameObject>(p); if(go==null){Debug.LogError("proto missing "+p);return;} protos.Add(new TreePrototype{prefab=go}); }
        _td.treePrototypes=protos.ToArray();
        int nCanopy=CANOPY.Length;
        float cwSum=0; foreach(var w in CANOPY_W) cwSum+=w;
        float swSum=0; foreach(var w in SHRUB_W) swSum+=w;

        var placed=new List<TreeInstance>();
        var cells=new Dictionary<long,List<Vector2>>(); float gridSize=3.5f;
        System.Func<Vector2,float,bool> farEnough=(pt,minR)=>{
            int gx=Mathf.FloorToInt(pt.x/gridSize), gz=Mathf.FloorToInt(pt.y/gridSize);
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++){
                long key=((long)(gx+dx)<<32)^(uint)(gz+dz);
                List<Vector2> lst; if(cells.TryGetValue(key,out lst)) foreach(var q in lst) if((q-pt).sqrMagnitude<minR*minR) return false;
            }
            return true;
        };
        System.Action<Vector2> add=(pt)=>{
            int gx=Mathf.FloorToInt(pt.x/gridSize), gz=Mathf.FloorToInt(pt.y/gridSize);
            long key=((long)gx<<32)^(uint)gz; List<Vector2> lst;
            if(!cells.TryGetValue(key,out lst)){ lst=new List<Vector2>(); cells[key]=lst; } lst.Add(pt);
        };

        // region bbox = trail polyline bbox expanded by corridor
        float minX=1e9f,maxX=-1e9f,minZ=1e9f,maxZ=-1e9f;
        foreach(var p in _trail){ if(p.x<minX)minX=p.x; if(p.x>maxX)maxX=p.x; if(p.y<minZ)minZ=p.y; if(p.y>maxZ)maxZ=p.y; }
        minX-=CORRIDOR; maxX+=CORRIDOR; minZ-=CORRIDOR; maxZ+=CORRIDOR;
        var rng=new System.Random(90210);
        int canopyN=0,shrubN=0;
        for(int i=0;i<DENSITY;i++){
            float x=minX+(float)rng.NextDouble()*(maxX-minX);
            float z=minZ+(float)rng.NextDouble()*(maxZ-minZ);
            if(InKeepout(x,z)) continue;
            if(OnRoad(x,z)) continue;                       // 노면 중심 식재 금지
            float dT=DistTrail(x,z); float dens=DensDist(dT); if(dens<=0f) continue;
            float sl=SlopeDeg(x,z); if(sl>55f) continue; if(sl>40f) dens*=0.5f;
            // continuous lining near path; glades only further out
            float clump=Mathf.PerlinNoise(x*0.05f+2f,z*0.05f+2f);
            float clumpF = (dT<9f)? Mathf.Lerp(0.8f,1.25f,clump) : (0.12f+clump*1.3f);
            dens*=clumpF;
            if((float)rng.NextDouble()>dens) continue;
            var pt=new Vector2(x,z);
            // canopy hugs the path edge (overhead tunnel); shrubs sparse midstory
            float shrubProb=Mathf.Lerp(0.1f,0.3f,Mathf.Clamp01((dT-2f)/16f));
            bool isShrub=(float)rng.NextDouble()<shrubProb;
            float minR=isShrub?3.0f:4.0f;
            if(!farEnough(pt,minR)) continue;
            // choose species
            int proto;
            if(isShrub){ float r=(float)rng.NextDouble()*swSum,acc=0; proto=nCanopy; for(int s=0;s<SHRUB_W.Length;s++){acc+=SHRUB_W[s]; if(r<=acc){proto=nCanopy+s;break;}} shrubN++; }
            else { float r=(float)rng.NextDouble()*cwSum,acc=0; proto=0; for(int s=0;s<CANOPY_W.Length;s++){acc+=CANOPY_W[s]; if(r<=acc){proto=s;break;}} canopyN++; }
            add(pt);
            float wy=H(x,z)/_td.size.y;
            float sc=0.8f+(float)rng.NextDouble()*0.5f;
            var ti=new TreeInstance{
                position=new Vector3(x/_td.size.x, wy, z/_td.size.z),
                prototypeIndex=proto, widthScale=sc, heightScale=sc*(0.95f+(float)rng.NextDouble()*0.15f),
                rotation=(float)(rng.NextDouble()*6.2831853), color=Color.white, lightmapColor=Color.white };
            placed.Add(ti);
        }
        _td.SetTreeInstances(placed.ToArray(),true);
        _terr.Flush();
        EditorUtility.SetDirty(_td); AssetDatabase.SaveAssets();
        Debug.Log("[Foliage] Trees placed="+placed.Count+" (canopy="+canopyN+" shrub="+shrubN+")");
    }

    [MenuItem("Seocheon/Foliage/4 Add Detail (fern)")]
    public static void AddDetail()
    {
        Grab(); BuildKeepouts(); BuildTrail(); BuildRoadField();
        // detail prototype: Deparia_4 lowest-LOD mesh
        var dep=AssetDatabase.LoadAssetAtPath<GameObject>(PREF_DIR+"SM_Deparia_4.prefab");
        var lg=dep.GetComponentInChildren<LODGroup>(); var lods=lg.GetLODs(); var last=lods[lods.Length-1];
        GameObject protoGo=null; foreach(var r in last.renderers){ if(r!=null){ protoGo=r.gameObject; break; } }
        // build a standalone prefab that contains only the LOD3 mesh+mat (so detail uses the light mesh)
        string detPrefab="Assets/_Project/Seocheon/Art/Terrain/Detail_Fern_LOD3.prefab";
        var inst=Object.Instantiate(protoGo); inst.name="Detail_Fern_LOD3"; inst.transform.localScale=protoGo.transform.lossyScale;
        var savedGo=PrefabUtility.SaveAsPrefabAsset(inst,detPrefab); Object.DestroyImmediate(inst);

        var dp=new DetailPrototype{
            prototype=savedGo, usePrototypeMesh=true, renderMode=DetailRenderMode.VertexLit,
            useInstancing=true, minWidth=0.7f,maxWidth=1.15f,minHeight=0.7f,maxHeight=1.2f,
            noiseSpread=0.3f, healthyColor=Color.white, dryColor=new Color(0.85f,0.8f,0.7f)
        };
        _td.detailPrototypes=new DetailPrototype[]{dp};
        int dres=_td.detailResolution; float dcell=_td.size.x/dres;
        var layer=new int[dres,dres]; int cnt=0;
        for(int z=0;z<dres;z++)for(int x=0;x<dres;x++){
            float wx=(x+0.5f)*dcell, wz=(z+0.5f)*dcell;
            if(wz>=MAXZ) continue; if(InKeepout(wx,wz)) continue;
            float dR=DistTrail(wx,wz);
            if(dR<3f||dR>8f) continue;                 // fern only in 3~8m path-edge band
            if(_dirt[Mathf.Clamp(Mathf.FloorToInt(wz/_cell),0,_aw-1),Mathf.Clamp(Mathf.FloorToInt(wx/_cell),0,_aw-1)]) continue;
            float sl=SlopeDeg(wx,wz); if(sl>50f) continue;
            float blotch=Mathf.PerlinNoise(wx*0.3f+9f,wz*0.3f+9f);
            if(blotch<0.5f) continue;                  // patchy
            layer[z,x]=Mathf.RoundToInt(Mathf.Lerp(1f,6f,blotch)); cnt++;
        }
        _td.SetDetailLayer(0,0,0,layer);
        EditorUtility.SetDirty(_td); AssetDatabase.SaveAssets();
        Debug.Log("[Foliage] Fern detail cells="+cnt);
    }

    [MenuItem("Seocheon/Foliage/5 Perf Settings")]
    public static void Perf()
    {
        Grab();
        _terr.treeDistance=50f; _terr.treeBillboardDistance=50f; _terr.treeCrossFadeLength=5f;
        _terr.detailObjectDistance=25f; _terr.detailObjectDensity=1f;
        _terr.treeMaximumFullLODCount=8;
        _terr.drawInstanced=true;
        QualitySettings.shadowDistance=25f;
        QualitySettings.lodBias=0.45f;   // push trees to lower LODs sooner
        // terrain tree LOD bias multiplier (reflection; not in public API)
        var pi=typeof(Terrain).GetProperty("treeLODBiasMultiplier");
        if(pi!=null) pi.SetValue(_terr,0.4f,null);
        // fog
        RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Linear;
        RenderSettings.fogStartDistance=35f; RenderSettings.fogEndDistance=80f;
        RenderSettings.fogColor=new Color(0.74f,0.78f,0.80f);
        EditorUtility.SetDirty(_terr); Debug.Log("[Foliage] Perf set: treeDist50 bbStart50 fade5 detail25 shadow25 lodBias0.45 fog35-80");
    }

    // ==================================================================
    // 앞능선 좌우 두 덩어리 마루선 위 실루엣용 교목 (스카이라인 깨기)
    [MenuItem("Seocheon/Foliage/6 Ridge Silhouette")]
    public static void RidgeSilhouette()
    {
        PlantTrees();                       // 회랑 식재를 결정론적으로 재구성(그대로 유지)
        Grab(); BuildKeepouts(); BuildTrail();
        var bs=GameObject.Find("Bridge_South"); float zBridge= bs!=null? bs.transform.position.z : 571f;
        float zLo=_tomb.y+2f, zHi=zBridge-13f;        // 마커에서 유도한 능선 밴드 z
        float xLo=_tomb.x-40f, xHi=_mud.x+35f;
        float zc=(zLo+zHi)*0.5f; float xSplit=TrailXAtZ(zc);   // 길 골짜기 = 좌우 분할선
        Vector2 pkL=FindPeak(xLo,xSplit,zLo,zHi), pkR=FindPeak(xSplit,xHi,zLo,zHi);
        float hL=H(pkL.x,pkL.y), hR=H(pkR.x,pkR.y);
        var list=new List<TreeInstance>(_td.treeInstances);
        int a1=PlantCrest(list,xLo,xSplit,zLo,zHi,hL,6);
        int a2=PlantCrest(list,xSplit,xHi,zLo,zHi,hR,6);
        _td.SetTreeInstances(list.ToArray(),true); _terr.Flush();
        EditorUtility.SetDirty(_td); AssetDatabase.SaveAssets();
        Debug.Log("[Foliage] Ridge silhouette added="+(a1+a2)+" (L "+a1+" pk"+hL.ToString("F1")+" / R "+a2+" pk"+hR.ToString("F1")+") total trees="+list.Count);
    }
    static float TrailXAtZ(float z){ for(int i=0;i<_trail.Count-1;i++){ var a=_trail[i]; var b=_trail[i+1]; if(z>=Mathf.Min(a.y,b.y)&&z<=Mathf.Max(a.y,b.y)){ float t=(z-a.y)/(b.y-a.y); return a.x+(b.x-a.x)*t; } } return (_trail[0].x+_trail[_trail.Count-1].x)*0.5f; }
    static Vector2 FindPeak(float xa,float xb,float zLo,float zHi){ float bh=-1e9f; Vector2 bp=new Vector2((xa+xb)*0.5f,(zLo+zHi)*0.5f); for(float x=xa;x<=xb;x+=1.5f)for(float z=zLo;z<=zHi;z+=1.5f){ if(InKeepout(x,z))continue; float h=H(x,z); if(h>bh){bh=h;bp=new Vector2(x,z);} } return bp; }
    static int PlantCrest(List<TreeInstance> list,float xa,float xb,float zLo,float zHi,float peakH,int maxN)
    {
        var crest=new List<Vector2>();
        for(float x=xa;x<=xb;x+=1.5f) for(float z=zLo;z<=zHi;z+=1.5f){
            if(InKeepout(x,z)) continue;
            if(H(x,z) < peakH-2.5f) continue;          // 마루 캡(정상 ~2.5m 이내) ≈ 마루선 ±8m 띠
            if(SlopeDeg(x,z)>55f) continue;
            crest.Add(new Vector2(x,z));
        }
        var rng=new System.Random(1234+(int)xa);
        for(int i=crest.Count-1;i>0;i--){ int j=rng.Next(i+1); var t=crest[i]; crest[i]=crest[j]; crest[j]=t; }
        var placed=new List<Vector2>();
        foreach(var c in crest){ bool ok=true; foreach(var q in placed) if((q-c).sqrMagnitude<16f){ ok=false; break; } if(!ok) continue; placed.Add(c); if(placed.Count>=maxN) break; }
        foreach(var c in placed){
            float wy=H(c.x,c.y)/_td.size.y;
            float sc=0.7f+(float)rng.NextDouble()*1.0f;   // 0.7~1.7 : 높이 편차 크게
            int proto=rng.Next(0,CANOPY.Length);          // 교목만(0..4)
            list.Add(new TreeInstance{ position=new Vector3(c.x/_td.size.x, wy, c.y/_td.size.z), prototypeIndex=proto,
                widthScale=Mathf.Lerp(0.85f,1.2f,(float)rng.NextDouble()), heightScale=sc,
                rotation=(float)(rng.NextDouble()*6.2831853), color=Color.white, lightmapColor=Color.white });
        }
        return placed.Count;
    }
}
