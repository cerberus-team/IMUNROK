using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Gyeonu;
using static IMUNROK.Gyeonu.Editor.SeonaHouseLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 선아 집 여닫이 가구 리거 — 서고·집무실과 **같은 방식**이다: 배치는 일절 건드리지 않고
    /// 씬 인스턴스에 <see cref="FurnitureParts"/>(클릭 토글)만 얹는다. 부품 GameObject를
    /// 리페어런트하지 않고 부모 좌표계에서 자체 회전/이동시킨다.
    ///
    /// 【두 가지 힌지 방식】
    ///   · axisInSelf  — 본 원점이 곧 경첩일 때 (kcisa 스킨드 본)
    ///   · axisInParent + pivotInParent — 본 원점이 경첩이 아닐 때. 운현궁 조립가구가 여기 해당한다:
    ///     문짝이 **독립 프리팹**이라 피벗이 문짝 한가운데다. 그래서 문짝 바운즈를 가구 루트
    ///     로컬로 재어 **바깥쪽 세로 모서리**를 경첩으로 잡아 준다.
    ///
    /// 멱등 — 다시 실행하면 파트 구성만 갱신한다. **가구의 위치·회전은 절대 손대지 않는다.**
    /// </summary>
    public static class SeonaHouseFurnitureRigger
    {
        const float DoorAngle = 100f;
        const float DrawerSlide = 0.16f;
        const float SmallDrawerSlide = 0.11f;

        [MenuItem("Tools/이문록/선아 집 ▸ ④ 여닫이 가구 설치")]
        public static void Rig()
        {
            if (SceneManager.GetActiveScene().name != SceneName)
            { Debug.LogError("[선아집 여닫이] " + SceneName + " 씬에서 실행하세요"); return; }
            var props = GameObject.Find(PropRoot);
            if (props == null) { Debug.LogError("[선아집 여닫이] 소품 루트가 없습니다 — 「② 소품 배치」를 먼저"); return; }
            var items = props.transform.Find("기물");

            var log = new List<string>();

            // ── kcisa 스킨드 궤 (반닫이류) — 위에서 아래로 당겨 여는 앞판 ──
            // ⚠️ 서고·집무실에서 확정된 설정을 그대로 옮긴 것이다:
            //      axisInParent (0,1,0) · pivotInParent = joint2의 자기 로컬 위치 · openAngle +90
            //    **닫힘 기준값도 joint2 = identity** 로 맞춰야 한다 — 프리팹 기본값(0,148.3,0)을
            //    그대로 두면 같은 축·각도를 줘도 엉뚱하게 열린다.
            RigKcisaChest(items, "옷궤", "Assets/KTinteractiveProp/Volum 02/Prefabs/HalfChest 02.prefab", log);
            RigKcisaChest(items, "반닫이", "Assets/KTinteractiveProp/Volum 02/Prefabs/HalfChest 01.prefab", log);

            // ── 찬장 (kcisa Closet 01) — 옷장처럼 좌우로 갈라지는 쌍여닫이 ──
            RigKcisaTwoDoor(items, "찬장", "Assets/KTinteractiveProp/Volum 02/Prefabs/Closet 01.prefab", log);

            // ── 연상(kcisa BesideTable 01) — 서랍 하나. 회전이 아니라 앞으로 빼낸다 ──
            RigDrawer(items, "연상", "Drawer", log);

            // ── 뚜껑 상자 (kcisa Box 01/03) — 빗접·반짇고리함·궤 ──
            foreach (var nm in new[] { "빗접", "반짇고리함", "궤", "궤_아버지방" })
                RigLidBox(items, nm, log);

            // ⚠️⚠️ **경대(Mirror 01)는 이 리거가 절대 건드리지 않는다.**
            //    사용자가 직접 방향을 잡아 둔 소품이다 (지시 2026-08-17: "경대는 내가 직접 돌려둔
            //    방향이 맞다. 절대 건드리지 마라"). 본 복원·접지 보정·FurnitureParts 부착 전부 제외.
            //    이미 원본 포즈로 복원돼 있고 무효 파트도 제거된 상태다 — 그대로 두면 된다.
            // 반짇고리함(Box 01)도 열 필요가 없는 소품이라 대상이 아니다.

            // ── 문갑 (Table04_Key) — 바깥 문 2짝(Dummy051/052) + 가운데 서랍(Dummy053) ──
            // ⚠️ door-01/02 의 렌더러는 **Dummy051/052 에만 스킨된 SkinnedMeshRenderer**다.
            //    door 트랜스폼을 돌려 봐야 메시가 안 따라온다 — 반드시 그 본을 돌려야 한다.
            RigOne(items, "문갑", "문갑", log, t => new List<FurnitureParts.Part>
            {
                Hinge(t, "Dummy051", new Vector3(0f, 0f, 1f), -90f),
                Hinge(t, "Dummy052", new Vector3(0f, 0f, 1f), 90f),
                Slide(t, "Dummy053", new Vector3(0f, -DrawerSlide, 0f)),
            });

            // ── 운현궁 조립가구 — 문짝·서랍이 전부 독립 GameObject ──
            RigUnhyeongung(items, "의걸이장", "SM_Wardrobe223_body", log,
                new[] { "SM_Wardrobe223_doorL", "SM_Wardrobe223_doorR" },
                new string[0], new string[0]);

            RigUnhyeongung(items, "문서장", "SM_Cupboard226_body", log,
                new[] { "SM_Cupboard226_doorTL", "SM_Cupboard226_doorTR",
                        "SM_Cupboard226_doorBL", "SM_Cupboard226_doorBR" },
                new[] { "SM_Cupboard226_drawer" }, new string[0]);

            // ⚠️ 삼층장 위층의 door01/door02 는 이름만 door 이지 실제로는 **서랍**이다 —
            //    회전이 아니라 앞으로 당겨져야 한다 (집무실에서 사용자가 잡아 준 사항).
            RigUnhyeongung(items, "삼층장", "SM_ThreetieredCupboard_body", log,
                new[] { "SM_ThreetieredCupboard_door03", "SM_ThreetieredCupboard_door04",
                        "SM_ThreetieredCupboard_door05", "SM_ThreetieredCupboard_door06",
                        "SM_ThreetieredCupboard_door07", "SM_ThreetieredCupboard_door08" },
                new[] { "SM_ThreetieredCupboard_door01", "SM_ThreetieredCupboard_door02" },
                new string[0]);

            RefreshSkins(items);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[선아집 여닫이] 설치 결과\n  " + string.Join("\n  ", log));
        }

        [MenuItem("Tools/이문록/선아 집 ▸ 여닫이 가구 제거")]
        public static void Unrig()
        {
            int n = 0;
            foreach (var f in Object.FindObjectsByType<FurnitureParts>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { Object.DestroyImmediate(f); n++; }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[선아집 여닫이] FurnitureParts " + n + "개 제거");
        }

        /// <summary>kcisa 팩 가구의 본 구조를 찍어 본다 — 새 가구를 리깅할 때 축·경첩을 정하는 근거.
        /// (축 이름만 봐선 모른다. 서고에서도 ±90°씩 돌려 바운즈 변화를 보고 정했다)</summary>
        [MenuItem("Tools/이문록/선아 집 ▸ 진단 ▸ 가구 본 구조 덤프")]
        public static void DumpBones()
        {
            var items = GameObject.Find(PropRoot)?.transform.Find("기물");
            if (items == null) { Debug.LogError("[선아집] 소품이 없습니다"); return; }
            var sb = new System.Text.StringBuilder();
            foreach (Transform item in items)
            {
                var joints = new List<string>();
                foreach (var t in item.GetComponentsInChildren<Transform>(true))
                {
                    if (t == item) continue;
                    string n = t.name.ToLowerInvariant();
                    if (n.StartsWith("joint") || n.StartsWith("dummy") || n.Contains("door") || n.Contains("drawer"))
                        joints.Add(Path(item, t) + " @" + t.localPosition.ToString("F3"));
                }
                if (joints.Count > 0)
                    sb.AppendLine(item.name + ":\n    " + string.Join("\n    ", joints));
            }
            Debug.Log("[선아집] 가구 본 구조\n" + sb);
        }

        static string Path(Transform root, Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        // ══════════════════════════════════════════════════════
        /// <summary>
        /// 씬 인스턴스의 본 로컬 회전을 **원본 프리팹 값으로 되돌린다.**
        /// 잘못 리깅해 본을 틀어 놓았을 때 "허공에 뜬 문짝"을 제자리로 돌리는 유일한 방법이다
        /// (닫힘 포즈는 우리가 정하는 게 아니라 팩이 구워 놓은 값이다).
        /// </summary>
        /// <summary>열지 않기로 한 소품에서 이전 <see cref="FurnitureParts"/>를 걷어낸다.
        /// 본만 원본으로 되돌리면 축이 0인 무효 파트가 남아 클릭 프롬프트만 뜬다.</summary>
        static void Unrig(Transform items, string itemName, List<string> log)
        {
            var t = items.Find(itemName);
            var fp = t != null ? t.GetComponent<FurnitureParts>() : null;
            if (fp == null) return;
            Object.DestroyImmediate(fp);
            log.Add("   ✂ " + itemName + " — 이전 FurnitureParts 제거 (여닫이 대상 아님)");
        }

        /// <summary>
        /// 스킨드 메시의 **밑면 높이를 유지한 채** 본을 손본다.
        /// ⚠️ 본을 돌리면 스킨 메시가 통째로 움직여 **가구가 바닥을 뚫고 내려앉는다** —
        ///    경대가 본 복원 직후 마루 아래 0.238m로 꺼졌다(실측). 루트를 그만큼 되올려 준다.
        ///    (놓인 자리를 알 필요가 없어 손으로 옮겨 둔 가구에도 안전하다)
        /// </summary>
        static void KeepGrounded(Transform t, System.Action work)
        {
            float before = MeshBottom(t);
            work();
            float after = MeshBottom(t);
            if (!float.IsNaN(before) && !float.IsNaN(after) && Mathf.Abs(before - after) > 0.0005f)
                t.position += new Vector3(0f, before - after, 0f);
        }

        static float MeshBottom(Transform t)
        {
            float y = float.NaN;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<BoxCollider>() != null) continue;   // 차단 박스 제외
                var smr = r as SkinnedMeshRenderer;
                if (smr != null && smr.sharedMesh != null)
                {
                    var m = new Mesh();
                    smr.BakeMesh(m, true);                              // 현재 본 포즈로 실측
                    var lb = m.bounds;
                    Object.DestroyImmediate(m);
                    for (int i = 0; i < 8; i++)
                    {
                        var c = new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x,
                                            (i & 2) == 0 ? lb.min.y : lb.max.y,
                                            (i & 4) == 0 ? lb.min.z : lb.max.z);
                        float wy = smr.transform.TransformPoint(c).y;
                        if (float.IsNaN(y) || wy < y) y = wy;
                    }
                }
                else if (float.IsNaN(y) || r.bounds.min.y < y) y = r.bounds.min.y;
            }
            return y;
        }

        static int RestoreJoints(Transform items, string itemName, string prefabPath, List<string> log)
        {
            var t = items.Find(itemName);
            if (t == null) { log.Add("· " + itemName + " — 씬에 없음"); return 0; }
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (src == null) { log.Add("✗ " + itemName + " — 원본 프리팹 없음: " + prefabPath); return 0; }

            var map = new Dictionary<string, Quaternion>();
            foreach (var s in src.GetComponentsInChildren<Transform>(true))
                if (s.name.StartsWith("joint")) map[s.name] = s.localRotation;

            int fixedCount = 0;
            KeepGrounded(t, () =>
            {
                foreach (var c in t.GetComponentsInChildren<Transform>(true))
                {
                    Quaternion q;
                    if (!c.name.StartsWith("joint") || !map.TryGetValue(c.name, out q)) continue;
                    if (Quaternion.Angle(c.localRotation, q) < 0.5f) continue;
                    log.Add("   ↺ " + itemName + "/" + c.name + " " + c.localRotation.eulerAngles.ToString("F1")
                            + " → 원본 " + q.eulerAngles.ToString("F1"));
                    c.localRotation = q;
                    fixedCount++;
                }
            });
            if (fixedCount > 0) log.Add("✓ " + itemName + " — 본 " + fixedCount + "개 원본 포즈로 복원");
            return fixedCount;
        }

        /// <summary>
        /// kcisa 스킨드 궤(반닫이류) — `*/joint1/joint2` 앞판을 아래로 당겨 연다.
        ///
        /// ⚠️ **이 팩에서 "닫힘 = identity"가 성립하는 건 HalfChest 계열뿐이다.**
        ///    프리팹 기본값이 (0, 148.3, 0)인데 그대로 두면 같은 축·각도를 줘도 엉뚱하게 열린다 —
        ///    서고·집무실에서 확정된 사항이라 여기서도 그대로 따른다.
        ///    다른 가구(Closet·Mirror)에 이 규칙을 옮겨 쓰면 **문이 90°/270° 돌아가 허공에 뜬다.**
        ///    실제로 1차에 찬장·경대를 이 함수로 묶었다가 그 사고가 났다.
        /// </summary>
        static void RigKcisaChest(Transform items, string itemName, string prefabPath, List<string> log)
        {
            RigOne(items, itemName, itemName, log, t =>
            {
                Transform j2 = null;
                foreach (var c in t.GetComponentsInChildren<Transform>(true))
                    if (c.name == "joint2") { j2 = c; break; }
                if (j2 == null) return new List<FurnitureParts.Part>();
                j2.localRotation = Quaternion.identity;      // HalfChest 한정 — 위 주석 참조
                return new List<FurnitureParts.Part>
                {
                    new FurnitureParts.Part
                    {
                        node = j2,
                        axisInParent = new Vector3(0f, 1f, 0f),
                        pivotInParent = j2.localPosition,
                        openAngle = 90f,
                    }
                };
            });
        }

        /// <summary>
        /// kcisa 찬장(Closet 01) — 옷장처럼 **좌우 문이 가운데에서 갈라지는 쌍여닫이**.
        ///
        /// 【본 구조 (원본 실측)】
        ///   joint1 (270, 179.1, 0)                     ← 문 상단 축. **이 본의 로컬 +Z가 월드 up**이다
        ///     ├ joint2 (0,0,270.9) → joint3 → joint4   ← 오른쪽 문 (경첩 = joint2 자기 원점)
        ///     └ joint5 (0,180,178.2)
        ///          └ joint6 (0,0,270.9) → joint7 → joint8  ← 왼쪽 문
        ///
        /// 닫힘 기준값은 **identity가 아니라 (0,0,270.9)** 다. joint3/joint7의 40°도 닫힘 포즈의
        /// 일부라 건드리지 않는다 — 문짝은 joint2/joint6만 돌리면 통째로 따라온다.
        ///
        /// 회전축은 `axisInSelf = (0,0,1)`. joint1이 X −90으로 누워 있어 **그 하위 본의 로컬 Z가
        /// 곧 세로축(월드 Y)** 이다 — 축 이름만 보면 절대 알 수 없고, 부모 회전을 합성해야 나온다.
        /// 열림 부호는 **자유단(joint4/joint8)이 가구 앞쪽으로 나오는 쪽**을 계산해서 정한다.
        /// </summary>
        static void RigKcisaTwoDoor(Transform items, string itemName, string prefabPath, List<string> log)
        {
            RestoreJoints(items, itemName, prefabPath, log);   // 잘못 틀어 놓은 본을 원본으로
            RigOne(items, itemName, itemName, log, t =>
            {
                var parts = new List<FurnitureParts.Part>();
                Vector3 front = -t.forward;     // Furnisher가 kcisa 프리팹을 "앞면 −Z"로 놓는다
                foreach (var chain in new[] { new[] { "joint2", "joint3", "joint4" },
                                              new[] { "joint6", "joint7", "joint8" } })
                {
                    Transform hinge = Find(t, chain[0]), mid = Find(t, chain[1]), tip = Find(t, chain[2]);
                    if (hinge == null || mid == null || tip == null)
                    { log.Add("   · " + itemName + " " + string.Join("/", chain) + " 없음"); continue; }

                    // 닫혔을 때 **정면판**이 뻗는 방향 = 경첩에서 가구 가운데를 향하는 가로 방향
                    float toCenter = Vector3.Dot(t.right, t.position - hinge.position);
                    Vector3 want = t.right * Mathf.Sign(toCenter);

                    // ① 닫힘 포즈 — **바깥판(joint3→joint4)만 편다.**
                    //    ⚠️ 경첩 마디(joint2→joint3)는 원본 그대로 둔다. 이 문은 평평한 판이 아니라
                    //       **측면판 0.277 + 정면판 0.358로 감싸는 형태**다. 경첩이 가구 깊이 한가운데
                    //       (로컬 z +0.006)에 있고 앞면은 z −0.30이라, 측면판이 앞으로 뻗어 줘야
                    //       정면판이 앞면에 닿는다. 두 마디를 다 눕혔더니 문짝이 **선반 뒤 한가운데**
                    //       에 박혀서 닫혔다(실측 — 앞에서 보면 선반이 문 앞을 가로질렀다).
                    float aMid = Flatten(mid, tip, want);

                    // ② 열림 — **의걸이장(SM_Wardrobe223)과 똑같은 방식**으로 건다:
                    //      axisInParent = 부모 로컬로 환산한 월드 세로축
                    //      pivotInParent = 문짝 **바깥쪽 세로 끝단**
                    // ⚠️ 예전엔 `axisInSelf`로 경첩 본(joint2) 자기 원점을 축으로 돌렸는데, 그 원점은
                    //    문짝 앞면이 아니라 **가구 깊이 한가운데(로컬 z +0.006)** 라서 열 때마다
                    //    경첩 쪽 세로변이 벽에서 통째로 떨어져 나왔다(사용자 보고). 실제 경첩은
                    //    측면판과 정면판이 만나는 **앞쪽 모서리 = joint3/joint7** 이다. 거기를 축으로.
                    Transform corner = mid;                       // 앞쪽 바깥 모서리
                    Transform parent = hinge.parent;
                    Vector3 axis = parent.InverseTransformDirection(Vector3.up);
                    Vector3 pivot = parent.InverseTransformPoint(corner.position);

                    Vector3 arm = tip.position - corner.position; arm.y = 0f;
                    float plus = Vector3.Dot(Quaternion.AngleAxis(100f, Vector3.up) * arm, front);
                    float minus = Vector3.Dot(Quaternion.AngleAxis(-100f, Vector3.up) * arm, front);
                    float worldOpen = plus >= minus ? 100f : -100f;

                    parts.Add(new FurnitureParts.Part
                    {
                        node = hinge,
                        axisInParent = axis,        // 부모 로컬 up (joint5 계열은 −Z로 나온다)
                        pivotInParent = pivot,      // 앞쪽 바깥 세로 끝단
                        openAngle = worldOpen,      // 축 벡터가 방향을 담고 있어 부호 보정이 필요 없다
                    });
                    log.Add("   · " + chain[1] + " 닫힘보정 " + aMid.ToString("F1")
                            + "° / 경첩 " + chain[0] + " 축=" + axis.ToString("F2")
                            + " 피벗=" + pivot.ToString("F3") + " 열림 " + worldOpen.ToString("F0") + "°");
                }
                return parts;
            });
        }

        /// <summary>
        /// 서랍 — **회전이 아니라 앞으로 빼낸다** (서고·집무실의 `slideLocal` 방식 그대로).
        ///
        /// ⚠️ `FurnitureParts.slideLocal`은 `node.localPosition`에 더해지므로 **부모 좌표계** 벡터다.
        ///    kcisa 본은 X −90으로 누워 있어 "앞으로"가 부모 로컬에서는 **+Y**로 나온다
        ///    (실측: 연상 앞면 −forward=(−0.04,0,1.00) → 부모로컬 (0, 0.16, 0)).
        ///    축 이름만 보고 넣으면 서랍이 위로 솟거나 옆으로 빠진다 — 반드시 환산해서 넣을 것.
        /// </summary>
        static void RigDrawer(Transform items, string itemName, string boneName, List<string> log)
        {
            RigOne(items, itemName, itemName, log, t =>
            {
                var bone = Find(t, boneName);
                if (bone == null) return new List<FurnitureParts.Part>();
                Vector3 front = -t.forward;                       // kcisa 프리팹은 앞면이 −Z
                Vector3 slide = bone.parent.InverseTransformVector(front * DrawerSlide);
                log.Add("   · " + itemName + "/" + boneName + " 앞으로 " + DrawerSlide.ToString("F2")
                        + "m = 부모로컬 " + slide.ToString("F3"));
                return new List<FurnitureParts.Part> { new FurnitureParts.Part { node = bone, slideLocal = slide } };
            });
        }

        /// <summary>
        /// 뚜껑 상자 (kcisa Box 01/03) — 뒤 경첩으로 뚜껑이 젖혀진다.
        ///
        /// ⚠️ **원본 프리팹은 뚜껑이 열린 채로 저장돼 있다** (Closet 01과 같은 함정).
        ///    게다가 뚜껑을 움직이는 본은 이름이 그럴싸한 `Lid`/`Lid_Hinge`가 아니라 **`Body_Hinge`** 다 —
        ///    `Lid`를 돌리면 바운즈가 1mm도 안 변한다(실측). 스윕해 보니 자기 로컬 X로
        ///    **+55°가 닫힘**(상자 높이 0.284 → 0.129에서 평평해진다). 그 자리를 닫힘 기준으로 굽고
        ///    열림은 원래 포즈로 되돌아가는 −55°다.
        /// </summary>
        static void RigLidBox(Transform items, string itemName, List<string> log)
        {
            const float ClosedAngle = 55f;
            RigOne(items, itemName, itemName, log, t =>
            {
                var bone = Find(t, "Body_Hinge");
                if (bone == null) return new List<FurnitureParts.Part>();
                // ⚠️ 닫힘 각도는 **처음 한 번만** 굽는다. 이미 리깅돼 있으면(=닫힌 상태) 또 돌리면
                //    55°씩 누적돼 뚜껑이 상자를 뚫는다. ②가 소품을 새로 깔면 다시 열린 포즈라 적용된다.
                if (t.GetComponent<FurnitureParts>() == null)
                    KeepGrounded(t, () =>
                        bone.localRotation = bone.localRotation * Quaternion.AngleAxis(ClosedAngle, Vector3.right));
                return new List<FurnitureParts.Part>
                {
                    new FurnitureParts.Part
                    {
                        node = bone,
                        axisInSelf = Vector3.right,
                        openAngle = -ClosedAngle,
                    }
                };
            });
        }

        /// <summary>
        /// 본의 **로컬 +Z가 월드에서 위인지 아래인지** (+1 / −1).
        /// ⚠️ Closet 01의 joint5가 Y 180° 뒤집혀 있어 **왼쪽 문 계열(joint6~8)은 로컬 Z가 월드 −Y**다.
        ///    이걸 안 보고 두 문에 같은 부호를 주면 한쪽이 반대로 열린다(가운데가 안 갈라진다).
        /// </summary>
        static float UpSign(Transform bone)
            => Mathf.Sign(Vector3.Dot(bone.rotation * Vector3.forward, Vector3.up));

        /// <summary>
        /// 마디 하나를 <paramref name="want"/> 방향으로 눕힌다 (수평 성분만 본다). 적용한 각도를 돌려준다.
        /// ⚠️ **Closet 01의 프리팹 저장 포즈는 "닫힘"이 아니라 "열림"이다** — 이름은 `SK_Closet_Close`인데
        ///    실제 본은 문 두 짝이 앞으로 90°씩 펼쳐진 상태로 구워져 있다(실측). 게다가 접이문이라
        ///    마디가 둘(경첩 0.277m + 바깥판 0.358m)이다. 그래서 닫힘 포즈를 **계산해서 만든다.**
        /// </summary>
        static float Flatten(Transform bone, Transform child, Vector3 want)
        {
            Vector3 arm = child.position - bone.position; arm.y = 0f;
            if (arm.sqrMagnitude < 1e-6f) return 0f;
            float world = Vector3.SignedAngle(arm, want, Vector3.up);
            bone.localRotation *= Quaternion.AngleAxis(world * UpSign(bone), Vector3.forward);
            return world;
        }

        /// <summary>
        /// 스킨드 메시를 강제로 다시 계산시킨다.
        /// ⚠️ 에디터에서는 본을 스크립트로 돌려도 **SkinnedMeshRenderer가 바로 갱신되지 않는다** —
        ///    본은 움직였는데 화면은 옛 포즈 그대로였다(실측: BakeMesh는 0.87m인데 renderer.bounds는
        ///    1.31m). sharedMesh를 재대입하면 갱신된다. Play 중에는 매 프레임 갱신되니 무관하다.
        /// </summary>
        static void RefreshSkins(Transform items)
        {
            foreach (var smr in items.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = smr.sharedMesh;
                smr.sharedMesh = null;
                smr.sharedMesh = m;
                EditorUtility.SetDirty(smr);
            }
            SceneView.RepaintAll();
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var c in root.GetComponentsInChildren<Transform>(true))
                if (c.name == name) return c;
            return null;
        }

        static void RigOne(Transform items, string itemName, string display, List<string> log,
                           System.Func<Transform, List<FurnitureParts.Part>> build)
        {
            var t = items.Find(itemName);
            if (t == null) { log.Add("· " + itemName + " — 씬에 없음 (건너뜀)"); return; }
            var parts = build(t);
            parts.RemoveAll(p => p == null || p.node == null);
            if (parts.Count == 0) { log.Add("✗ " + itemName + " — 움직일 본을 못 찾음"); return; }
            Install(t, display, parts);
            log.Add("✓ " + itemName + " — 파트 " + parts.Count);
        }

        /// <summary>
        /// 운현궁 조립가구. 문짝 피벗이 문짝 한가운데라 **바운즈로 바깥쪽 세로 모서리를 경첩으로 잡는다.**
        /// 정면은 문짝이 몸체보다 어느 z쪽으로 튀어나와 있는지로 판정한다(가구마다 다를 수 있다).
        /// </summary>
        static void RigUnhyeongung(Transform items, string itemName, string bodyName, List<string> log,
                                   string[] doors, string[] drawers, string[] smallDrawers)
        {
            var t = items.Find(itemName);
            if (t == null) { log.Add("· " + itemName + " — 씬에 없음 (건너뜀)"); return; }
            var body = t.Find(bodyName);
            if (body == null) { log.Add("✗ " + itemName + " — 몸체(" + bodyName + ") 없음"); return; }

            Bounds bb = LocalBounds(t, body);
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

        /// <summary>부모 좌표계 경첩 — 축은 부모 로컬, 피벗은 **그 본의 자기 로컬 위치**
        /// (본 원점이 곧 바깥쪽 세로 끝단인 문갑 Dummy 본용. 서고에서 확정된 방식).</summary>
        static FurnitureParts.Part Hinge(Transform root, string path, Vector3 axisInParent, float angle)
        {
            Transform n = null;
            foreach (var c in root.GetComponentsInChildren<Transform>(true))
                if (c.name == path) { n = c; break; }
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
            Transform n = null;
            foreach (var c in root.GetComponentsInChildren<Transform>(true))
                if (c.name == path) { n = c; break; }
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
