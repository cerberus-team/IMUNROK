using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 견우마을 보행 세팅 + 식생 접지 정리 (2026-08-09, 멱등).
    ///
    /// 보행 (마을·은하담과 같은 방식 — MeshCollider 금지, 전부 Box):
    ///   걷기: 지형(터레인 콜라이더) / 숲길 협곡 / 징검다리 보행판 → 디딤돌 →
    ///         은둔처 계단 램프 → 기단(2.62) → 돌계단 → 툇마루(3.20).
    ///         제월당 기단·마루는 선아집 방식 — 실부재에 mesh 로컬 바운드 박스(평평, 낄 틈 없음).
    ///   차단: 물가 투명 벽(중심 (2,18) 방사 h=0.45 물가선 추적, 징검다리 구간만 개방 + 양측 난간),
    ///         협곡 벽(경사 43° 월담 방지, 길 양옆 ±3.3m), 은둔처 실내(마루 위 몸통),
    ///         폐가 몸통(바닥부터 전체 — 팩 MeshCollider 71개 전부 OFF), 씬 경계.
    ///   경계 근거: 플레이 콘텐츠가 전부 중앙 지형(±50) 안 — x ±56 / z -53~52로 잡아
    ///         스폰(z-48)·출구 마커(z-49.5)·입수구(z43)를 여유 있게 포함하고 스커트 배회를 막는다.
    ///
    /// 식생 접지: 나무 피벗을 지형에 재스냅하되 경사에 비례해 밑동을 묻는다
    ///   (침하 = 0.06 + 0.5·tan(경사), 최대 0.55 — 내리막쪽 뿌리 노출 해소).
    ///   경사 40° 초과는 삭제. 바위(설계상 침하)·수련(수면 부유)은 뜬 것만 교정.
    /// </summary>
    public static class GyeonuVillageWalkSetup
    {
        const string RootName = "견우마을_보행콜라이더";
        const float WaterLevel = GyeonuVillageBuilder.WaterLevel;
        const float PadX = GyeonuVillageBuilder.PadX, PadZ = GyeonuVillageBuilder.PadZ;
        static readonly Vector2 CrossMid = new Vector2(-17f, 8f);   // 징검다리 중점

        static readonly Vector2[] PathPts =
        {
            new Vector2(2f, -51f), new Vector2(3f, -42f), new Vector2(11f, -34f),
            new Vector2(12f, -25f), new Vector2(3f, -17f), new Vector2(-7f, -12f),
            new Vector2(-13f, -5f), new Vector2(-11f, 1f),
        };

        // ── 식생 접지 정리 ───────────────────────────────────
        [MenuItem("Tools/이문록/견우마을 식생 접지 정리")]
        public static void FixVegetationGrounding()
        {
            var root = GameObject.Find("견우마을_식생");
            if (root == null) { Debug.LogError("[접지] 견우마을_식생 없음"); return; }

            int snapped = 0, sunk = 0, deleted = 0, rockFixed = 0;
            foreach (var groupName in new[] { "협곡숲", "고지대숲", "연못가", "외곽숲", "고사리" })
            {
                var g = root.transform.Find(groupName);
                if (g == null) continue;
                for (int i = g.childCount - 1; i >= 0; i--)
                {
                    var c = g.GetChild(i);
                    var p = c.position;
                    float h = SampleH(p.x, p.z);
                    float slope = SlopeDegT(p.x, p.z);
                    if (slope > 40f)
                    {
                        Object.DestroyImmediate(c.gameObject);
                        deleted++;
                        continue;
                    }
                    float sink = Mathf.Min(0.06f + 0.5f * Mathf.Tan(slope * Mathf.Deg2Rad), 0.55f);
                    float targetY = h - sink;
                    if (Mathf.Abs(p.y - targetY) > 0.02f)
                    {
                        if (p.y > h + 0.03f) snapped++; else sunk++;
                        c.position = new Vector3(p.x, targetY, p.z);
                    }
                }
            }
            // 물가: 바위는 설계상 침하(-0.10~-0.15) — 떠 있는 것만 재침하. 갈대는 -0.05 스냅
            var water = root.transform.Find("물가");
            if (water != null)
                for (int i = water.childCount - 1; i >= 0; i--)
                {
                    var c = water.GetChild(i);
                    var p = c.position;
                    if (c.name.StartsWith("수련")) continue;
                    float h = SampleH(p.x, p.z);
                    if (c.name.StartsWith("바위"))
                    {
                        if (p.y > h - 0.05f) { c.position = new Vector3(p.x, h - 0.15f, p.z); rockFixed++; }
                    }
                    else if (c.name.StartsWith("갈대") && Mathf.Abs(p.y - (h - 0.05f)) > 0.02f)
                    {
                        c.position = new Vector3(p.x, h - 0.05f, p.z);
                        snapped++;
                    }
                }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[접지] 완료 — 뜬 것 스냅 {snapped}, 경사 침하 보정 {sunk}, 삭제(경사>40°) {deleted}, 바위 재침하 {rockFixed}");
        }

        // ── 보행 콜라이더 ────────────────────────────────────
        [MenuItem("Tools/이문록/견우마을 보행 콜라이더 구축")]
        public static void Build()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);

            int waterWalls = BuildWaterWalls(root);
            int gorgeWalls = BuildGorgeWalls(root);
            BuildPlankRails(root);
            BuildBoundary(root);
            BuildHermitageWalk(root);
            int hutCols = BlockRuinedHuts();
            int vegCols = FixVegetationColliders(root);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[보행] 구축 완료 — 물벽 {waterWalls}, 협곡벽 {gorgeWalls}, 폐가 팩콜라이더 OFF {hutCols}, " +
                      $"식생 콜라이더 정리 {vegCols}, 경계 x±56/z-53~52");
        }

        /// <summary>물가 투명 벽 — 못 중심 (2,18)에서 방사형으로 h=0.45 물가선을 찾아 접선 방향
        /// 박스를 두른다 (별 모양 유니온이라 방사 추적으로 전체 커버). 징검다리 구간만 개방.</summary>
        static int BuildWaterWalls(GameObject root)
        {
            var g = NewChild(root, "물벽");
            int made = 0;
            const float step = 2.5f;   // 도(度) — 최원점 38m에서 1.7m 간격, 박스 2.4m로 덮임
            for (float ang = 0f; ang < 360f; ang += step)
            {
                float rad = ang * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float found = -1f;
                for (float r = 5f; r < 42f; r += 0.25f)
                {
                    float x = 2f + dir.x * r, z = 18f + dir.y * r;
                    if (SampleH(x, z) >= 0.45f) { found = r; break; }
                }
                if (found < 0f) continue;
                var pos = new Vector2(2f, 18f) + dir * found;
                // 징검다리 개방 — 보행판이 중점 ±4.7m라 개방 반경은 그보다 넓게
                // (2.8로 좁게 잡으면 판 끝이 물벽에 막혀 못 내린다 — Play 검증에서 확인)
                if (Vector2.Distance(pos, CrossMid) < 5.6f) continue;
                Box(g, "물벽", new Vector3(pos.x, 1.6f, pos.y),
                    new Vector3(2.4f, 2.6f, 0.5f), Quaternion.Euler(0f, -ang, 0f));
                made++;
            }
            return made;
        }

        /// <summary>협곡 월담 방지 벽 — 길 양옆 ±3.3m, 어귀 마지막 7m는 개방 (분지 진출).</summary>
        static int BuildGorgeWalls(GameObject root)
        {
            var g = NewChild(root, "협곡벽");
            int made = 0;
            float total = 0f;
            for (int i = 1; i < PathPts.Length; i++) total += Vector2.Distance(PathPts[i - 1], PathPts[i]);
            for (float d = 1f; d < total - 7f; d += 2.6f)
            {
                var (pt, tang) = PointOnPath(d);
                var normal = new Vector2(-tang.y, tang.x);
                float yaw = Mathf.Atan2(tang.x, tang.y) * Mathf.Rad2Deg;
                float floorH = SampleH(pt.x, pt.y);
                foreach (float side in new[] { 1f, -1f })
                {
                    float x = pt.x + normal.x * 3.3f * side, z = pt.y + normal.y * 3.3f * side;
                    Box(g, "협곡벽", new Vector3(x, floorH + 1.4f, z),
                        new Vector3(0.4f, 4.2f, 3.0f), Quaternion.Euler(0f, yaw, 0f));
                    made++;
                }
            }
            return made;
        }

        /// <summary>징검다리 양측 난간 — 보행판(폭 1.3, 상면 0.76)에서 물로 벗어나지 않게.</summary>
        static void BuildPlankRails(GameObject root)
        {
            var g = NewChild(root, "징검다리난간");
            float yaw = Mathf.Atan2(0.6f, -0.8f) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var right = rot * Vector3.right;
            foreach (float side in new[] { 1f, -1f })
            {
                var c = new Vector3(CrossMid.x, 1.45f, CrossMid.y) + right * 0.85f * side;
                Box(g, "난간", c, new Vector3(0.15f, 1.6f, 9.4f), rot);
            }
        }

        /// <summary>씬 경계 — 콘텐츠(±50 지형) + 여유. 남측은 출구 마커(z-49.5) 뒤 -53.</summary>
        static void BuildBoundary(GameObject root)
        {
            var g = NewChild(root, "경계");
            Box(g, "경계_동", new Vector3(56f, 6f, 0f), new Vector3(1f, 14f, 112f), Quaternion.identity);
            Box(g, "경계_서", new Vector3(-56f, 6f, 0f), new Vector3(1f, 14f, 112f), Quaternion.identity);
            Box(g, "경계_북", new Vector3(0f, 6f, 52f), new Vector3(113f, 14f, 1f), Quaternion.identity);
            Box(g, "경계_남", new Vector3(0f, 6f, -53f), new Vector3(113f, 14f, 1f), Quaternion.identity);
        }

        /// <summary>은둔처 보행 — 선아집 방식: 기단·마루 실부재에 mesh 로컬 바운드 박스,
        /// 서면 계단은 램프 박스(지면 1.7 → 계단 상면 2.50 → 기단 2.62), 돌계단(기단→마루) 박스,
        /// 실내는 벽·문 로컬 AABB로 마루 위부터 차단. 키트 바위 MeshCollider는 박스로 대체.</summary>
        static void BuildHermitageWalk(GameObject root)
        {
            var bldg = GameObject.Find("견우마을_건물/은둔처_제월당");
            if (bldg == null) { Debug.LogWarning("[보행] 은둔처_제월당 없음 — 은둔처 보행 생략"); return; }
            var tr = bldg.transform;

            // 잔재 정리
            foreach (var n in new[] { "실내차단", "계단램프" })
            {
                var stale = tr.Find(n);
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
            }

            // ① 기단·마루·돌계단 실부재 박스 (mesh 로컬 바운드 — 회전 동기, 평평)
            int plates = 0;
            foreach (var r in bldg.GetComponentsInChildren<Renderer>(true))
            {
                bool plate = r.name.StartsWith("SM_Gidan") || r.name.StartsWith("SM_Floor")
                          || r.name.StartsWith("SM_Stone_Steps");
                if (!plate) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var bc = r.GetComponent<BoxCollider>();
                if (bc == null) bc = r.gameObject.AddComponent<BoxCollider>();
                bc.center = mf.sharedMesh.bounds.center;
                bc.size = mf.sharedMesh.bounds.size;
                plates++;
            }

            // ② 서면 계단(SM_JWD_Stair) 램프 — 지면(패드 1.7)에서 계단 상면(2.50)으로
            var ramps = NewChild(bldg, "계단램프");
            int rampCount = 0;
            foreach (var r in bldg.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith("SM_JWD_Stair")) continue;
                var b = r.bounds;
                // 계단은 건물 서면(진입면) — 램프는 월드 정렬 경사 박스로 충분
                var center = new Vector3(b.center.x - 0.4f, (1.7f + 2.5f) * 0.5f, b.center.z);
                var go = new GameObject("램프_" + rampCount);
                go.transform.SetParent(ramps, false);
                go.transform.position = center;
                // 경사 방향: 건물 중심에서 계단으로의 XZ 방향
                var dir = new Vector3(b.center.x - tr.position.x, 0f, b.center.z - tr.position.z).normalized;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                go.transform.rotation = Quaternion.Euler(24f, yaw, 0f);   // 오르막 24°
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.5f, 0.12f, 2.4f);
                rampCount++;
            }

            // ③ 실내 차단 v2 — 통짜 AABB는 마루 전체를 덮어 폐기 (Play 검증에서 확인).
            //    벽·문·기둥 부재마다 mesh 로컬 바운드 박스 — 열린 칸(대청)은 열린 채로,
            //    닫힌 벽·문이 스스로 차단벽이 된다. 전부 평판·기둥이라 낄 틈 없음.
            //    나중에 문을 여닫이로 바꿀 때도 부재 단위라 그대로 활용 가능.
            int panels = 0;
            foreach (var r in bldg.GetComponentsInChildren<Renderer>(true))
            {
                bool panel = r.name.StartsWith("SM_JWD_Wall") || r.name.StartsWith("SM_JWD_Door")
                          || r.name.StartsWith("SM_Pillar");
                if (!panel) continue;
                var mf2 = r.GetComponent<MeshFilter>();
                if (mf2 == null || mf2.sharedMesh == null) continue;
                var pc = r.GetComponent<BoxCollider>();
                if (pc == null) pc = r.gameObject.AddComponent<BoxCollider>();
                pc.center = mf2.sharedMesh.bounds.center;
                pc.size = mf2.sharedMesh.bounds.size;
                panels++;
            }

            // ④ 키트 — 바위 MeshCollider 전부 OFF, 디딤돌엔 상면 박스
            var kit = GameObject.Find("견우마을_건물/은둔처키트");
            if (kit != null)
            {
                foreach (var mc in kit.GetComponentsInChildren<MeshCollider>(true)) mc.enabled = false;
                var stale = kit.transform.Find("디딤돌_상면");
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
                var step = kit.transform.Find("디딤돌_대서편");
                if (step != null)
                {
                    var rend = step.GetComponentInChildren<Renderer>();
                    if (rend != null)
                    {
                        var b = rend.bounds;
                        var go = new GameObject("디딤돌_상면");
                        go.transform.SetParent(kit.transform, false);
                        go.transform.position = new Vector3(b.center.x, b.max.y - 0.06f, b.center.z);
                        var bc = go.AddComponent<BoxCollider>();
                        bc.size = new Vector3(b.size.x * 0.75f, 0.12f, b.size.z * 0.75f);
                    }
                }
                // 징검다리 3석에도 상면 박스 (보행판이 주 통로지만 발판 감각 보강)
                foreach (Transform c in kit.transform)
                {
                    if (!c.name.StartsWith("징검다리_") || c.name.Contains("보행판")) continue;
                    var rend = c.GetComponentInChildren<Renderer>();
                    if (rend == null) continue;
                    var b = rend.bounds;
                    var exist = c.Find("상면");
                    if (exist != null) Object.DestroyImmediate(exist.gameObject);
                    var go = new GameObject("상면");
                    go.transform.SetParent(c, true);
                    go.transform.rotation = Quaternion.identity;
                    go.transform.position = new Vector3(b.center.x, b.max.y - 0.05f, b.center.z);
                    var bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(b.size.x * 0.8f, 0.1f, b.size.z * 0.8f);
                }
            }
            Debug.Log($"[보행] 은둔처 — 실부재 박스 {plates}, 램프 {rampCount}, 벽·문·기둥 패널 박스 {panels}");
        }

        /// <summary>폐가 — 팩 MeshCollider 전부 OFF, 바닥부터 전체를 감싸는 몸통 박스 1개씩.</summary>
        static int BlockRuinedHuts()
        {
            var huts = GameObject.Find("견우마을_건물/폐가");
            if (huts == null) return 0;
            int off = 0;
            foreach (Transform hut in huts.transform)
            {
                foreach (var c in hut.GetComponentsInChildren<Collider>(true))
                    if (c.enabled && c.name != "몸통차단") { c.enabled = false; off++; }

                var stale = hut.Find("몸통차단");
                if (stale != null) Object.DestroyImmediate(stale.gameObject);

                Vector3 lMin = Vector3.one * float.MaxValue, lMax = Vector3.one * float.MinValue;
                bool any = false;
                foreach (var r in hut.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.gameObject.activeInHierarchy) continue;
                    AccumulateLocalAabb(hut, r, ref lMin, ref lMax);
                    any = true;
                }
                if (!any) continue;
                var go = new GameObject("몸통차단");
                go.transform.SetParent(hut, false);
                var bc = go.AddComponent<BoxCollider>();
                var size = lMax - lMin;
                size.x = Mathf.Max(0.5f, size.x - 0.2f);
                size.z = Mathf.Max(0.5f, size.z - 0.2f);
                bc.size = size;
                bc.center = (lMin + lMax) * 0.5f;
            }
            return off;
        }

        /// <summary>식생 콜라이더 정리 — 도달 가능 지역(연못가·물가)의 MeshCollider를 박스로 대체.
        /// 협곡·고지대·외곽 나무는 협곡벽·경사 뒤라 그대로 둔다. 갈대·수련은 통과.</summary>
        static int FixVegetationColliders(GameObject root)
        {
            var veg = GameObject.Find("견우마을_식생");
            if (veg == null) return 0;
            int fixedCount = 0;

            // 연못가 나무 — mesh OFF + 몸통(줄기) 박스
            var pond = veg.transform.Find("연못가");
            if (pond != null)
                foreach (Transform c in pond)
                {
                    bool had = false;
                    foreach (var mc in c.GetComponentsInChildren<MeshCollider>(true))
                        if (mc.enabled) { mc.enabled = false; had = true; }
                    if (!had) continue;
                    var stale = c.Find("줄기박스");
                    if (stale != null) Object.DestroyImmediate(stale.gameObject);
                    var go = new GameObject("줄기박스");
                    go.transform.SetParent(c, false);
                    float s = c.localScale.x;
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 2f, 0f);
                    bc.size = new Vector3(0.45f, 4f, 0.45f);
                    fixedCount++;
                }

            // 물가 바위 — mesh OFF + 로컬 바운드 박스 (0.85 축소)
            var water = veg.transform.Find("물가");
            if (water != null)
                foreach (Transform c in water)
                {
                    if (!c.name.StartsWith("바위"))
                    {
                        foreach (var col in c.GetComponentsInChildren<Collider>(true))
                            if (col.enabled) { col.enabled = false; fixedCount++; }
                        continue;
                    }
                    bool had = false;
                    foreach (var mc in c.GetComponentsInChildren<MeshCollider>(true))
                        if (mc.enabled) { mc.enabled = false; had = true; }
                    if (!had) continue;
                    Vector3 lMin = Vector3.one * float.MaxValue, lMax = Vector3.one * float.MinValue;
                    bool any = false;
                    foreach (var r in c.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r.GetComponent<MeshFilter>() == null) continue;
                        AccumulateLocalAabb(c, r, ref lMin, ref lMax);
                        any = true;
                        break;   // LOD0 하나면 충분
                    }
                    if (!any) continue;
                    var stale = c.Find("바위박스");
                    if (stale != null) Object.DestroyImmediate(stale.gameObject);
                    var go = new GameObject("바위박스");
                    go.transform.SetParent(c, false);
                    var bc = go.AddComponent<BoxCollider>();
                    var size = lMax - lMin;
                    bc.size = new Vector3(size.x * 0.85f, size.y, size.z * 0.85f);
                    bc.center = (lMin + lMax) * 0.5f;
                    fixedCount++;
                }
            return fixedCount;
        }

        // ── 헬퍼 ────────────────────────────────────────────
        static void AccumulateLocalAabb(Transform space, Renderer r, ref Vector3 lMin, ref Vector3 lMax)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? mb.min.x : mb.max.x,
                    (i & 2) == 0 ? mb.min.y : mb.max.y,
                    (i & 4) == 0 ? mb.min.z : mb.max.z);
                var lp = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                lMin = Vector3.Min(lMin, lp);
                lMax = Vector3.Max(lMax, lp);
            }
        }

        static (Vector2 pt, Vector2 tang) PointOnPath(float dist)
        {
            float acc = 0f;
            for (int i = 1; i < PathPts.Length; i++)
            {
                float seg = Vector2.Distance(PathPts[i - 1], PathPts[i]);
                if (acc + seg >= dist)
                {
                    float t = (dist - acc) / seg;
                    return (Vector2.Lerp(PathPts[i - 1], PathPts[i], t),
                            (PathPts[i] - PathPts[i - 1]).normalized);
                }
                acc += seg;
            }
            return (PathPts[PathPts.Length - 1],
                    (PathPts[PathPts.Length - 1] - PathPts[PathPts.Length - 2]).normalized);
        }

        static void Box(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot);
            var b = go.AddComponent<BoxCollider>();
            b.size = size;
        }

        static Transform NewChild(GameObject parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent.transform, false);
            return t;
        }

        static float SampleH(float x, float z)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var p = t.transform.position; var s = t.terrainData.size;
                if (x >= p.x && x <= p.x + s.x && z >= p.z && z <= p.z + s.z)
                    return t.SampleHeight(new Vector3(x, 0f, z)) + p.y;
            }
            return GyeonuVillageBuilder.GroundHeight(Mathf.Clamp(x, -49f, 49f), Mathf.Clamp(z, -49f, 49f));
        }

        static float SlopeDegT(float x, float z)
        {
            float sx = SampleH(x + 1f, z) - SampleH(x - 1f, z);
            float sz = SampleH(x, z + 1f) - SampleH(x, z - 1f);
            return Mathf.Atan(0.5f * Mathf.Sqrt(sx * sx + sz * sz)) * Mathf.Rad2Deg;
        }
    }
}
