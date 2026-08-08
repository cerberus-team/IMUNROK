using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 마을 건물 내부 진입 차단 (2026-08-08, 멱등) — 건물마다 몸통을 감싸는 박스 1개.
    ///
    /// 산정: 건물 렌더러 중 "벽 높이대"(기단 위 1.3~2.2m 구간에 걸친 것 — 지붕·낮은
    /// 기단/툇마루 제외)의 로컬 AABB → XZ를 0.6m 안쪽으로 축소(툇마루 기둥 라인 보정),
    /// 바닥은 기단 위 1.0m부터(툇마루·기단 위 보행 유지), 천장은 건물 꼭대기.
    /// 박스는 건물 자식 GO(회전 동기) — 프리팹 인스턴스에 자식 추가는 허용.
    ///
    /// 선아집도 동일 차단 + 실내 씬 전환용 Door_SeonaHouse 마커(남면 주 출입문,
    /// 마당 일각문(-44.3, 51.6) 축과 가장 가까운 문)를 성하리_마커에 생성.
    /// </summary>
    public static class VillageBuildingBlocker
    {
        const string BoxName = "몸통차단";
        static readonly string[] BodyPatterns =
        {
            "Tavern_House", "Bamboo_Rafter_House", "House_With_", "L_Shaped_House",
            "Local_Clerks_House", "Local_Personnel", "Wooden_Bench_House",
        };

        [MenuItem("Tools/이문록/마을 건물 내부 차단")]
        public static void Build()
        {
            var targets = new List<Transform>();

            var bld = GameObject.Find("성하리_건물");
            if (bld != null)
                foreach (Transform c in bld.transform)
                {
                    if (c.name.Contains("담장") || c.name == "Low_Wooden_Bench") continue;
                    if (c.name == "Ox_Mill" || c.name == "MotherHouse" || c.name == "SeonaHouse" || Match(c.name))
                        targets.Add(c);
                }

            var jip = GameObject.Find("성하리_집터");
            if (jip != null)
                foreach (Transform lot in jip.transform)
                    foreach (Transform c in lot)
                        if (Match(c.name)) targets.Add(c);

            int made = 0, updated = 0, skipped = 0;
            foreach (var t in targets)
            {
                if (t.name == "SeonaHouse") { AddSeonaException(t); continue; }   // 예외: 툇마루 진입 허용
                if (AddBodyBox(t, ref made, ref updated) == false) skipped++;
            }

            int propsOff = DisableYardPropColliders();
            PlaceSeonaDoorMarker();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[건물차단] 완료 — 대상 {targets.Count}, 신규 {made}, 갱신 {updated}, 실패 {skipped}, 마당 소품 콜라이더 OFF {propsOff}");
        }

        static bool Match(string n)
        {
            foreach (var p in BodyPatterns) if (n.StartsWith(p)) return true;
            return false;
        }

        /// <summary>v2 (2026-08-08): 툇마루·기단·난간·계단 포함 바닥부터 통째로 감싼다 —
        /// 올라탈 곳을 없애 끼임 원천 차단. 건물 내부 팩 콜라이더는 전부 끈다(도달 불가+성능).</summary>
        static bool AddBodyBox(Transform bldg, ref int made, ref int updated)
        {
            var rends = bldg.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return false;

            float baseY = float.MaxValue;
            foreach (var r in rends) baseY = Mathf.Min(baseY, r.bounds.min.y);

            // 지붕(기단+2.2 위에서 시작) 제외한 전 렌더러의 로컬 AABB — 기단·마루·난간·계단 포함
            bool any = false;
            Vector3 lMin = Vector3.one * float.MaxValue, lMax = Vector3.one * float.MinValue;
            foreach (var r in rends)
            {
                var wb = r.bounds;
                if (r.name == BoxName) continue;
                if (wb.min.y > baseY + 2.2f) continue;          // 지붕·처마만 제외 (발이 닿을 일 없음)
                // ⚠️ 월드 AABB 꼭짓점을 역변환하면 회전 건물(주막 별채 158° 등)에서 이중 팽창해
                //    문간까지 침범한다 — 메시 로컬 바운드 꼭짓점을 직접 변환(타이트).
                AccumulateLocalAabb(bldg, r, ref lMin, ref lMax);
                any = true;
            }
            if (!any) return false;

            float topLocal = float.MinValue;
            foreach (var r in rends)
            {
                var lp = bldg.InverseTransformPoint(new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z));
                topLocal = Mathf.Max(topLocal, lp.y);
            }
            float baseLocal = bldg.InverseTransformPoint(new Vector3(bldg.position.x, baseY, bldg.position.z)).y;

            const float inset = 0.15f;                           // 처마 그림자 정도만 보정
            var size = lMax - lMin;
            size.x = Mathf.Max(0.5f, size.x - inset * 2f);
            size.z = Mathf.Max(0.5f, size.z - inset * 2f);
            float yBottom = baseLocal + 0.05f;                   // 바닥부터 — 올라탈 곳 없음
            size.y = Mathf.Max(0.5f, topLocal - yBottom);
            var center = new Vector3((lMin.x + lMax.x) * 0.5f, yBottom + size.y * 0.5f, (lMin.z + lMax.z) * 0.5f);

            var boxTr = bldg.Find(BoxName);
            bool isNew = boxTr == null;
            if (isNew)
            {
                var go = new GameObject(BoxName);
                boxTr = go.transform;
                boxTr.SetParent(bldg, false);                    // 로컬 정렬 — 건물 회전 동기
            }
            var box = boxTr.GetComponent<BoxCollider>();
            if (box == null) box = boxTr.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;

            // 건물 내부 팩 콜라이더 전부 OFF — 박스가 대체 (끼임 원인 + 성능)
            foreach (var col in bldg.GetComponentsInChildren<Collider>(true))
                if (col != box && col.name != BoxName && col.enabled) col.enabled = false;

            if (isNew) made++; else updated++;
            return true;
        }

        /// <summary>메시 로컬 바운드 꼭짓점을 대상 로컬로 변환해 AABB 누적 (월드 AABB 역변환의
        /// 회전 팽창 회피). MeshFilter 없으면 월드 바운드로 폴백.</summary>
        static void AccumulateLocalAabb(Transform space, Renderer r, ref Vector3 lMin, ref Vector3 lMax)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
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
                return;
            }
            var wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? wb.min.x : wb.max.x,
                    (i & 2) == 0 ? wb.min.y : wb.max.y,
                    (i & 4) == 0 ? wb.min.z : wb.max.z);
                var lp = space.InverseTransformPoint(corner);
                lMin = Vector3.Min(lMin, lp);
                lMax = Vector3.Max(lMax, lp);
            }
        }

        /// <summary>선아집 예외 (2026-08-08): 나중에 툇마루에서 실내 씬 전환 —
        /// 마당→기단(0.56)→디딤돌(0.95)→마루(1.37)까지 오를 수 있게 플레이트 박스를 깔고,
        /// 몸통 차단은 마루 위(1.40)부터. 팩 콜라이더는 전부 OFF 유지(끼임 방지 —
        /// 원래 끼임 원인이 이 마루·난간 콜라이더였음). 전부 평평한 박스라 낄 틈이 없다.</summary>
        static void AddSeonaException(Transform bldg)
        {
            const float PodiumTop = 0.56f;   // 기단 상면 (정점 히스토그램 실측 0.50~0.55)
            const float MaruTop = 1.37f;     // 마루 상면 (실측 1.30~1.40; 1.6대는 실내 방바닥)

            // 팩 콜라이더 OFF 유지
            foreach (var col in bldg.GetComponentsInChildren<Collider>(true))
                if (!col.name.StartsWith("선아_") && col.name != BoxName && col.enabled) col.enabled = false;
            var oldBody = bldg.Find(BoxName);
            if (oldBody != null) Object.DestroyImmediate(oldBody.gameObject);

            var rends = bldg.GetComponentsInChildren<Renderer>(true);

            // 이전 방식(밴드 추정 플레이트) 잔재 제거 — 문지방·초석이 밴드에 걸려 부정확했음
            foreach (var stale in new[] { "선아_기단", "선아_마루" })
            {
                var st = bldg.Find(stale);
                if (st != null) Object.DestroyImmediate(st.gameObject);
            }

            // 기단(SM_Gidan)·마루(SM_Floor) 실부재에 BoxCollider 직접 부여 — 평평해서 낄 틈 없음
            int plates = 0;
            float maruSouthZ = float.MaxValue;                   // 마커 축 근처 마루 남단 (디딤돌 위치용)
            foreach (var r in rends)
            {
                bool isFloor = r.name.StartsWith("SM_Floor");
                bool isGidan = r.name.StartsWith("SM_Gidan");
                if (!isFloor && !isGidan) continue;
                var mf2 = r.GetComponent<MeshFilter>();
                if (mf2 == null || mf2.sharedMesh == null) continue;
                var pb = r.GetComponent<BoxCollider>();
                if (pb == null) pb = r.gameObject.AddComponent<BoxCollider>();
                pb.center = mf2.sharedMesh.bounds.center;
                pb.size = mf2.sharedMesh.bounds.size;
                pb.enabled = true;
                plates++;
                if (isFloor && r.bounds.max.y < 1.5f && r.bounds.min.x < -43.5f && r.bounds.max.x > -46.5f)
                    maruSouthZ = Mathf.Min(maruSouthZ, r.bounds.min.z);
            }

            // 몸통 = 문 라인(Door_SeonaHouse z 60.64) 북쪽 실내 전체 — 실측 하드코딩.
            // 남측 툇마루 띠(z 59.15~60.6, 마루 1.0)는 밖에 남아 마커 앞에 설 수 있다.
            // (밴드 추정은 문지방·인방에 걸려 두 번 실패 — 이 건물 전용 수치로 확정)

            // 몸통 차단 박스 (월드 좌표 실측: 벽 외곽 x -49.3~-36.2, 실내 시작 z 60.75, 지붕 ~6.8)
            {
                var go = new GameObject(BoxName);
                go.transform.SetParent(bldg, true);              // 월드 정렬 (건물 rotY 359의 1° 무시)
                go.transform.rotation = Quaternion.identity;
                var box = go.AddComponent<BoxCollider>();
                var min = new Vector3(-49.3f, 1.05f, 60.75f);
                var max = new Vector3(-36.2f, 6.8f, 66.1f);
                go.transform.position = (min + max) * 0.5f;
                box.size = max - min;
            }

            // 디딤돌: 마커 축의 마루 남단 바로 앞 (기단 0.56 → 마루 1.37 단차 0.81 — 중간단 0.97)
            var marker = GameObject.Find("성하리_마커/Door_SeonaHouse");
            if (marker != null)
            {
                float stepZ = (maruSouthZ < float.MaxValue ? maruSouthZ : marker.transform.position.z - 0.85f) - 0.45f;
                var stepGo = bldg.Find("선아_디딤돌");
                if (stepGo == null)
                {
                    var g = new GameObject("선아_디딤돌");
                    g.transform.SetParent(bldg, true);
                    stepGo = g.transform;
                }
                stepGo.position = new Vector3(marker.transform.position.x, 0f, stepZ);
                stepGo.rotation = Quaternion.identity;
                // 2단 디딤돌: 지면 0 → 0.50 → 0.97 → 마루 1.0 (기단 남단 노출이 15cm뿐이라
                // 지면→0.97 한 번에 오르게 되는 문제를 하단 단으로 해소)
                var boxes = stepGo.GetComponents<BoxCollider>();
                var scU = boxes.Length > 0 ? boxes[0] : stepGo.gameObject.AddComponent<BoxCollider>();
                scU.center = new Vector3(0f, 0.77f, 0f);
                scU.size = new Vector3(1.6f, 0.4f, 1.0f);                // 상면 0.97
                var scL = boxes.Length > 1 ? boxes[1] : stepGo.gameObject.AddComponent<BoxCollider>();
                scL.center = new Vector3(0f, 0.30f, -0.95f);
                scL.size = new Vector3(1.6f, 0.4f, 1.0f);                // 상면 0.50 (남쪽 한 단 아래)
            }
            Debug.Log("[건물차단] 선아집 예외 — 기단·마루 실부재 박스 " + plates + "개 + 디딤돌(z남단 " + (maruSouthZ < float.MaxValue ? maruSouthZ.ToString("F1") : "?") + ") + 마루 위 몸통 차단");
        }

        static void Plate(Transform bldg, string name, Vector3 lMin, Vector3 lMax, float yBottom, float yTop)
        {
            if (lMin.x > lMax.x) { Debug.LogWarning("[건물차단] " + name + " footprint 없음"); return; }
            var tr = bldg.Find(name);
            if (tr == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(bldg, false);
                tr = go.transform;
            }
            var box = tr.GetComponent<BoxCollider>();
            if (box == null) box = tr.gameObject.AddComponent<BoxCollider>();
            var size = new Vector3(lMax.x - lMin.x, yTop - yBottom, lMax.z - lMin.z);
            box.center = new Vector3((lMin.x + lMax.x) * 0.5f, yBottom + size.y * 0.5f, (lMin.z + lMax.z) * 0.5f);
            box.size = size;
        }

        /// <summary>마당의 소형 소품(항아리·소반·지게 등, 올라탈 높이대) 콜라이더 OFF —
        /// 캡슐이 올라탄 뒤 좁은 틈에 끼는 대표 원인. 평상·멍석 같은 크고 낮은 것은 유지.</summary>
        static int DisableYardPropColliders()
        {
            int off = 0;
            var jip = GameObject.Find("성하리_집터");
            if (jip == null) return 0;
            foreach (var col in jip.GetComponentsInChildren<Collider>(true))
            {
                if (!col.enabled) continue;
                var n = col.name;
                if (n.StartsWith("Wall") || n.StartsWith("Door") || n == BoxName || n.StartsWith("문짝")) continue;
                var b = col.bounds;
                float maxXZ = Mathf.Max(b.size.x, b.size.z);
                float height = b.size.y;
                float footTop = b.max.y;
                // 올라탈 수 있는 높이대의 자잘한 물건만 (평상·텃밭 Ground04a 등 큰 것 제외)
                if (footTop < 0.25f || footTop > 1.3f) continue;
                if (height < 0.3f) continue;
                if (maxXZ > 1.6f) continue;
                col.enabled = false;
                off++;
            }
            return off;
        }

        /// <summary>선아집 실내 씬 전환용 문 마커 — 남면(마당 일각문 쪽) 낮은 문 중 일각문 축에 최근접.</summary>
        static void PlaceSeonaDoorMarker()
        {
            var seona = GameObject.Find("성하리_건물/SeonaHouse");
            var markers = GameObject.Find("성하리_마커");
            if (seona == null || markers == null) { Debug.LogWarning("[건물차단] 선아집/마커 그룹 없음"); return; }

            const float gateX = -44.3f;                          // 선아집 일각문 축
            float centerZ = seona.GetComponentInChildren<Renderer>().bounds.center.z;
            Transform best = null;
            float bestScore = float.MaxValue;
            foreach (var tr in seona.GetComponentsInChildren<Transform>(true))
            {
                if (!tr.name.Contains("Door")) continue;
                var p = tr.position;
                if (p.y > 2.0f) continue;                        // 상층·환기창 제외
                if (p.z > 61.5f) continue;                       // 남면(일각문 쪽)만
                float score = Mathf.Abs(p.x - gateX);
                if (score < bestScore) { bestScore = score; best = tr; }
            }
            if (best == null) { Debug.LogWarning("[건물차단] 선아집 남면 문을 못 찾음"); return; }

            var old = markers.transform.Find("Door_SeonaHouse");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var m = new GameObject("Door_SeonaHouse");
            m.transform.SetParent(markers.transform, false);
            m.transform.position = best.position;
            m.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 마당(남쪽)을 바라봄
            Debug.Log("[건물차단] Door_SeonaHouse 마커: " + best.name + " @" + best.position.ToString("F2"));
        }
    }
}
