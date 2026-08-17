using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Gyeonu;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 집무실 여닫이 가구 리거 — 서고(ArchiveFurnitureRigger)와 **같은 방식**이다:
    /// 배치는 일절 건드리지 않고 씬 인스턴스에 <see cref="FurnitureParts"/>(클릭 토글)만 얹는다.
    /// 부품 GameObject를 리페어런트하지 않고 부모 좌표계에서 자체 회전/이동시킨다.
    ///
    /// 【두 가지 힌지 방식 (FurnitureParts가 지원)】
    ///   · axisInSelf  — 본 원점이 곧 경첩일 때 (kcisa 스킨드 본, 문갑 Dummy 본)
    ///   · axisInParent + pivotInParent — 본 원점이 경첩이 아닐 때. 운현궁 조립가구가 여기 해당한다:
    ///     문짝이 **독립 프리팹**이라 피벗이 문짝 한가운데다. 그래서 문짝 바운즈를 부모(가구 루트)
    ///     로컬로 재어 **바깥쪽 세로 모서리**를 경첩으로 잡아 준다.
    ///
    /// 【축을 계측으로 정한 근거】 스킨드 본은 축 이름만 봐선 모른다. 임시로 ±90°씩 돌려
    /// BakeMesh 바운즈 변화를 보고 정했다 (반닫이: X축이 앞판을 앞으로 눕힌다. Z축은 위로 들린다).
    ///
    /// 멱등 — 다시 실행하면 파트 구성만 갱신한다. **가구의 위치·회전은 절대 손대지 않는다.**
    /// </summary>
    public static class GwanaOfficeFurnitureRigger
    {
        // 문짝이 열리는 각도 / 서랍이 빠져나오는 거리
        const float DoorAngle = 100f;
        const float LidAngle = 100f;
        const float DrawerSlide = 0.16f;
        const float SmallDrawerSlide = 0.11f;

        [MenuItem("Tools/이문록/관아 집무실 ▸ 여닫이 가구 설치")]
        public static void Rig()
        {
            if (SceneManager.GetActiveScene().name != "Gyeonu_GwanaOffice")
            { Debug.LogError("[집무실 여닫이] Gyeonu_GwanaOffice 씬에서 실행하세요"); return; }
            var props = GameObject.Find("집무실_소품");
            if (props == null) { Debug.LogError("[집무실 여닫이] 집무실_소품 루트가 없습니다"); return; }
            var items = props.transform.Find("기물");

            var log = new List<string>();

            // ── 반닫이 (kcisa 스킨드) — 위에서 아래로 당겨 여는 앞판 ──
            // ⚠️ **서고 씬(Gyeonu_Observatory)의 확정 설정을 그대로 옮긴 것이다.**
            //    씬 파일에서 직접 읽었다 (다른 씬은 열지도 고치지도 않았다):
            //      axisInParent (0,1,0) · pivotInParent (0.4347611, 0, -0.1983914) · openAngle +90
            //    피벗은 joint2의 자기 로컬 위치와 같다 = 앞판 아래 모서리.
            //    **닫힘 기준값도 서고와 같은 joint2 = identity** 로 맞춘다 — 프리팹 기본값
            //    (0,148.3,0)을 그대로 두면 같은 축·각도를 줘도 엉뚱하게 열린다.
            //    (자기 로컬 X축으로 돌리던 3차 설정은 폐기)
            RigOne(items, "반닫이", "반닫이", log, t =>
            {
                var j2 = t.Find("SM_HalfChest_Close/joint1/joint2");
                if (j2 == null) return new List<FurnitureParts.Part>();
                j2.localRotation = Quaternion.identity;          // 닫힘 기준값 (서고와 동일)
                return new List<FurnitureParts.Part>
                {
                    new FurnitureParts.Part
                    {
                        node = j2,
                        axisInParent = new Vector3(0f, 1f, 0f),
                        pivotInParent = new Vector3(0.4347611f, 0f, -0.1983914f),
                        openAngle = 90f,
                    }
                };
            });

            // ── 문갑 (Table04_Key) — 바깥 문 2짝(door-01/02) + 가운데 서랍 ──
            // ⚠️ door-01/door-02 의 렌더러는 전부 **Dummy051/Dummy052 에만 스킨된 SkinnedMeshRenderer**다
            //    (실측). 그래서 door-01/02 트랜스폼을 돌려 봐야 메시가 따라오지 않는다 —
            //    반드시 그 본을 돌려야 한다.
            // ⚠️ 축도 서고 확정값으로 바꿨다: **axisInParent (0,0,1) = 문갑 루트의 로컬 수직축**,
            //    피벗은 각 본의 자기 로컬 위치(= 바깥쪽 세로 끝단). 본의 자기 로컬 Z를 쓰던
            //    3차 설정은 본이 X+90으로 누워 있어 축이 어긋났다.
            RigOne(items, "문갑", "문갑", log, t => new List<FurnitureParts.Part>
            {
                Hinge(t, "Dummy051", new Vector3(0f, 0f, 1f), -90f),   // door-01 (바깥 세로 끝단)
                Hinge(t, "Dummy052", new Vector3(0f, 0f, 1f), 90f),    // door-02 (반대쪽 끝단)
                Slide(t, "Dummy053", new Vector3(0f, -DrawerSlide, 0f)),
            });

            // ── 운현궁 조립가구 — 문짝·서랍이 전부 독립 GameObject ──
            RigUnhyeongung(items, "이층서랍장", "SM_Board229_body", log,
                new[] { "SM_Board229_doorL", "SM_Board229_doorR" },
                new[] { "SM_Board229_drawerB01", "SM_Board229_drawerB02",
                        "SM_Board229_drawerB03", "SM_Board229_drawerB04" },
                new[] { "SM_Board229_drawerS01", "SM_Board229_drawerS02", "SM_Board229_drawerS03" });

            RigUnhyeongung(items, "문서장", "SM_Cupboard226_body", log,
                new[] { "SM_Cupboard226_doorTL", "SM_Cupboard226_doorTR",
                        "SM_Cupboard226_doorBL", "SM_Cupboard226_doorBR" },
                new[] { "SM_Cupboard226_drawer" }, new string[0]);

            // ⚠️ 삼층장 위층의 door01/door02 는 이름만 door 이지 실제로는 **서랍**이다.
            //    회전이 아니라 앞으로 당겨져야 한다 (사용자 지시) → drawers 목록으로 옮겼다.
            RigUnhyeongung(items, "삼층장", "SM_ThreetieredCupboard_body", log,
                new[] { "SM_ThreetieredCupboard_door03", "SM_ThreetieredCupboard_door04",
                        "SM_ThreetieredCupboard_door05", "SM_ThreetieredCupboard_door06",
                        "SM_ThreetieredCupboard_door07", "SM_ThreetieredCupboard_door08" },
                new[] { "SM_ThreetieredCupboard_door01", "SM_ThreetieredCupboard_door02" },
                new string[0]);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[집무실 여닫이] 설치 결과\n  " + string.Join("\n  ", log));
        }

        [MenuItem("Tools/이문록/관아 집무실 ▸ 여닫이 가구 제거")]
        public static void Unrig()
        {
            int n = 0;
            foreach (var f in Object.FindObjectsByType<FurnitureParts>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { Object.DestroyImmediate(f); n++; }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[집무실 여닫이] FurnitureParts " + n + "개 제거");
        }

        // ══════════════════════════════════════════════════════
        static void RigOne(Transform items, string itemName, string display, List<string> log,
                           System.Func<Transform, List<FurnitureParts.Part>> build)
        {
            var t = items.Find(itemName);
            if (t == null) { log.Add("✗ " + itemName + " — 씬에 없음"); return; }
            var parts = build(t);
            parts.RemoveAll(p => p == null || p.node == null);
            if (parts.Count == 0) { log.Add("✗ " + itemName + " — 움직일 본을 못 찾음"); return; }
            Install(t, display, parts);
            log.Add("✓ " + itemName + " — 파트 " + parts.Count);
        }

        /// <summary>
        /// 운현궁 조립가구. 문짝 피벗이 문짝 한가운데라 **바운즈로 바깥쪽 세로 모서리를 경첩으로 잡는다.**
        /// 정면 방향은 문짝이 몸체보다 어느 쪽으로 튀어나와 있는지로 판정한다(가구마다 다를 수 있어 고정하지 않음).
        /// </summary>
        static void RigUnhyeongung(Transform items, string itemName, string bodyName, List<string> log,
                                   string[] doors, string[] drawers, string[] smallDrawers)
        {
            var t = items.Find(itemName);
            if (t == null) { log.Add("✗ " + itemName + " — 씬에 없음"); return; }
            var body = t.Find(bodyName);
            if (body == null) { log.Add("✗ " + itemName + " — 몸체(" + bodyName + ") 없음"); return; }

            Bounds bb = LocalBounds(t, body);
            // 정면 = 문짝들의 평균이 몸체보다 어느 z쪽인가
            float sumZ = 0f; int cnt = 0;
            foreach (var d in doors) { var dt = t.Find(d); if (dt != null) { sumZ += LocalBounds(t, dt).center.z; cnt++; } }
            foreach (var d in drawers) { var dt = t.Find(d); if (dt != null) { sumZ += LocalBounds(t, dt).center.z; cnt++; } }
            float frontSign = cnt > 0 && sumZ / cnt < bb.center.z ? -1f : 1f;

            var parts = new List<FurnitureParts.Part>();
            foreach (var d in doors)
            {
                var dt = t.Find(d);
                if (dt == null) { log.Add("   · " + d + " 없음"); continue; }
                var b = LocalBounds(t, dt);
                // 바깥쪽(몸체 중심에서 먼 쪽) 세로 모서리가 경첩
                float side = b.center.x >= bb.center.x ? 1f : -1f;
                float hingeX = side > 0f ? b.max.x : b.min.x;
                parts.Add(new FurnitureParts.Part
                {
                    node = dt,
                    axisInParent = Vector3.up,
                    pivotInParent = new Vector3(hingeX, b.center.y, b.center.z),
                    openAngle = side * frontSign * DoorAngle,
                });
            }
            foreach (var d in drawers) AddSlide(t, d, frontSign * DrawerSlide, parts, log);
            foreach (var d in smallDrawers) AddSlide(t, d, frontSign * SmallDrawerSlide, parts, log);

            if (parts.Count == 0) { log.Add("✗ " + itemName + " — 부품 0"); return; }
            Install(t, itemName, parts);
            log.Add("✓ " + itemName + " — 문 " + doors.Length + " / 서랍 " +
                    (drawers.Length + smallDrawers.Length) + " (정면 " + (frontSign > 0 ? "+Z" : "-Z") + ")");
        }

        static void AddSlide(Transform t, string name, float dz, List<FurnitureParts.Part> parts, List<string> log)
        {
            var dt = t.Find(name);
            if (dt == null) { log.Add("   · " + name + " 없음"); return; }
            parts.Add(new FurnitureParts.Part { node = dt, slideLocal = new Vector3(0f, 0f, dz) });
        }

        static void Install(Transform t, string display, List<FurnitureParts.Part> parts)
        {
            var fp = t.GetComponent<FurnitureParts>();
            if (fp == null) fp = t.gameObject.AddComponent<FurnitureParts>();
            fp.displayName = display;
            fp.duration = 0.9f;
            fp.parts = parts;
            EditorUtility.SetDirty(fp);
        }

        static FurnitureParts.Part Self(Transform root, string path, Vector3 axis, float angle)
        {
            var n = root.Find(path);
            return n == null ? null : new FurnitureParts.Part
            { node = n, axisInSelf = axis, openAngle = angle };
        }

        /// <summary>부모 좌표계 경첩 — 축은 부모 로컬, 피벗은 **그 본의 자기 로컬 위치**
        /// (본 원점이 곧 바깥쪽 세로 끝단인 문갑 Dummy 본용. 서고에서 확정된 방식).</summary>
        static FurnitureParts.Part Hinge(Transform root, string path, Vector3 axisInParent, float angle)
        {
            var n = root.Find(path);
            return n == null ? null : new FurnitureParts.Part
            {
                node = n,
                axisInParent = axisInParent,
                pivotInParent = n.localPosition,
                openAngle = angle,
            };
        }

        static FurnitureParts.Part Slide(Transform root, string path, Vector3 slide)
        {
            var n = root.Find(path);
            return n == null ? null : new FurnitureParts.Part { node = n, slideLocal = slide };
        }

        /// <summary>부품의 메시 바운즈를 가구 루트 로컬 좌표로 정확히 환산한다.</summary>
        static Bounds LocalBounds(Transform root, Transform node)
        {
            Bounds b = new Bounds(); bool first = true;
            foreach (var mf in node.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x,
                                        (i & 2) == 0 ? mb.min.y : mb.max.y,
                                        (i & 4) == 0 ? mb.min.z : mb.max.z);
                    var lp = root.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (first) { b = new Bounds(lp, Vector3.zero); first = false; } else b.Encapsulate(lp);
                }
            }
            return b;
        }
    }
}
