using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Gyeonu;
using static IMUNROK.Gyeonu.Editor.SeonaHouseLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 선아 집 실내 씬 조립. 멱등 — 루트를 지우고 다시 만든다.
    ///
    /// 【이 씬만 다른 점】 관측실·서고·집무실은 큐브로 방을 지었지만 여기는 **원본 한옥
    /// 프리팹을 그대로 쓴다**(사용자 지시). 그래서 빌더가 하는 일은 "짓기"가 아니라
    ///   ① 원본 배치  ② 원본 부재를 복제해 칸막이 세우기  ③ 뚫린 외피 메우기  ④ 조명·마커
    /// 네 가지뿐이다. 벽·바닥·창호·서까래는 하나도 만들지 않는다.
    ///
    /// ⚠️ **본채는 프리팹 인스턴스로 둔다 (언팩하지 않는다).** 원본이 39,357줄이라
    ///    언팩하면 씬 파일이 통째로 그만큼 불어난다. 프리팹 인스턴스에 허용되는
    ///    "컴포넌트 추가 · 프로퍼티 오버라이드"만으로 필요한 일이 전부 된다.
    ///
    /// ⚠️ **원본의 MeshCollider 269개는 전부 sharedMesh가 null인 빈 껍데기다** (실측).
    ///    물리에 아무 기여도 하지 않으므로 건드리지 않고 둔다 — 지우면 프리팹 인스턴스에
    ///    "제거된 컴포넌트" 오버라이드가 269개 쌓인다. 보행은 SeonaHouseWalkSetup이
    ///    박스로 따로 깐다 (프로젝트 규칙: MeshCollider 금지).
    ///
    /// 【복제 방식】 팩 부재는 **부모 GO의 로컬 위치가 메시 중심이 아니다**
    ///    (예: 부모 `SM_Pillar01A63` 안에 지붕 전체 `SM_Roof01A`가 들어 있다).
    ///    그래서 좌표를 직접 주지 않고 **렌더러 바운즈 중심을 기준으로** 회전·이동시킨다.
    /// </summary>
    public static class SeonaHouseBuilder
    {
        static readonly List<string> _missing = new List<string>();
        static GameObject _prefab;

        // ══════════════════════════════════════════════════════
        // 복제할 원본 부재 (프리팹 직계 자식 이름 — 전부 유일하다)
        // ══════════════════════════════════════════════════════

        /// <summary>창호문 한 칸 모듈 — 남벽 x −4.18~−1.48 칸에서 떠 온다.
        /// 문틀(2.65) + 세살문 4짝 + 문 위 벽 + 창방 2. 기둥 간격 2.60m 자리에 딱 맞는다.</summary>
        static readonly string[] DoorModule =
        {
            "SM_DoorFrame01A",     // 문틀      c=(-2.829, 1.999, -1.607) s=(2.651, 2.072, 0.186)
            "SM_Door03A4",         // 세살문 ①  c=(-3.687, 2.011, -1.616)
            "SM_Door03A3",         // 세살문 ②  ← 여닫이
            "SM_Door03A2",         // 세살문 ③  ← 여닫이
            "SM_Door03A",          // 세살문 ④
            "SM_Wall02B2",         // 문 위 벽  c=(-2.789, 3.426, -1.622)
            "SM_ChangBang02A36",   // 창방 상
            "SM_ChangBang02A35",   // 창방 하
        };
        /// <summary>모듈의 기준점 = 문틀 렌더러 중심의 XZ.</summary>
        static readonly Vector3 DoorModuleAxis = new Vector3(-2.829f, 0f, -1.607f);
        /// <summary>여닫이로 만들 두 짝 (모듈 가운데 두 짝).</summary>
        const string LeafA = "SM_Door03A3", LeafB = "SM_Door03A2";

        /// <summary>막힌 벽 한 칸 모듈 — 북벽 x −3.95~−1.55 칸.
        /// 하방 + 머름 + 창 2짝 + 벽 + 상부. 폭 2.80으로 뒷골방 북면(2.77)에 그대로 맞는다.</summary>
        static readonly string[] WallModule =
        {
            "SM_Pillar01A41",      // 하방(이름만 Pillar — 실제 메시는 가로 부재)
            "SM_WindoSill03Eb5",   // 머름
            "SM_Door02A9",         // 창 ①
            "SM_Door02A10",        // 창 ②
            "SM_Wall01A4",         // 벽      c=(-2.715, 2.133, 2.541) s=(2.799, 1.600, 0.170)
            "SM_Wall02A3",         // 벽 상
            "SM_ChangBang02A28",
            "SM_ChangBang02A27",
        };
        static readonly Vector3 WallModuleAxis = new Vector3(-2.715f, 0f, 2.541f);

        /// <summary>대청 남면에서 비워야 하는 원본 부재 — 벽·머름·창 2짝.
        /// 삭제가 아니라 SetActive(false) 다 (프리팹 인스턴스 오버라이드 → 되돌릴 수 있다).
        /// 문 위 벽 `SM_Wall02B`(y 2.80~3.84)는 그대로 두고 새 문틀 머리를 그 뒤로 물린다.</summary>
        static readonly string[] DaecheongSouthHide =
        {
            "SM_Wall01A3",         // 벽   c=(-0.115, 2.087, -1.677)
            "SM_WindoSill03Eb2",   // 머름 c=(-0.108, 1.017, -1.657)
            "SM_Door02A5",         // 창 ①
            "SM_Door02A6",         // 창 ②
        };

        /// <summary>대청 북면(z 0.92)에서 비우는 부재 — 벽·머름·창 2짝.
        /// 원본은 중앙 칸이 앞뒤로 갈려 있어 대청 깊이가 2.33m뿐이었다(들어서면 1.8m 앞이 벽).
        /// 뒤칸까지 트면 3.90m가 된다. 문 위 인방(`SM_Wall02B7` = Wall02F)과 환기창은 남겨
        /// **개구부**로 읽히게 한다 — 대청 뒤가 트인 건 한옥의 원래 문법이다.</summary>
        static readonly string[] DaecheongNorthHide =
        {
            "SM_Wall01A5",         // 벽   c=(0.002, 2.113, 0.920)
            "SM_WindoSill03Eb4",   // 머름 c=(-0.041, 1.043, 0.932)
            "SM_Door02A7",         // 창 ①
            "SM_Door02A8",         // 창 ②
        };

        // ══════════════════════════════════════════════════════
        [MenuItem("Tools/이문록/선아 집 ▸ ① 씬 조립")]
        public static void Build()
        {
            if (SceneManager.GetActiveScene().name != SceneName)
            { Debug.LogError("[선아집] " + SceneName + " 씬에서 실행하세요"); return; }

            _missing.Clear();
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HousePrefab);
            if (_prefab == null) { Debug.LogError("[선아집] 본채 프리팹 없음: " + HousePrefab); return; }

            var house = PlaceHouse();
            HideDaecheongSouthWall(house);
            BuildPartitions();
            FaceLatticeInward(house);
            DullPackMaterials();
            BuildRidgeBeam();
            BuildDarkShell();
            BuildLighting();
            BuildMarkers();

            if (_missing.Count > 0)
                Debug.LogWarning("[선아집] 못 찾은 부재 " + _missing.Count + "건:\n  " + string.Join("\n  ", _missing));

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[선아집] ① 씬 조립 완료 — 본채(프리팹 인스턴스) + 칸막이 4 + 종도리 + 조명 + 마커");
        }

        // ══════════════════════════════════════════════════════
        // ① 본채
        // ══════════════════════════════════════════════════════
        static Transform PlaceHouse()
        {
            var old = GameObject.Find(HouseRoot);
            if (old != null) Object.DestroyImmediate(old);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(_prefab);
            go.name = HouseRoot;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return go.transform;
        }

        /// <summary>
        /// 대청 남면 개방 — 원본은 여기가 머름 + 창 2짝짜리 **벽**이라 마당에서 들어올 수 없다.
        /// 바닥까지 열린 출입문은 서쪽 두 칸뿐인데, 그러면 플레이어가 선아 방으로 들어서게 된다.
        /// 사용자 지시("대청 = 들어서면 처음 서는 곳")대로 중앙을 열고 같은 창호문 모듈을 얹는다.
        /// </summary>
        static void HideDaecheongSouthWall(Transform house)
        {
            int n = Hide(house, DaecheongSouthHide, "대청 남면")
                  + Hide(house, DaecheongNorthHide, "대청 북면");
            Debug.Log("[선아집] 대청 앞뒤 원본 부재 " + n + "개 비활성 (삭제 아님 — 되돌릴 수 있다)");
        }

        static int Hide(Transform house, string[] names, string label)
        {
            int n = 0;
            foreach (var nm in names)
            {
                var t = house.Find(nm);
                if (t == null) { _missing.Add(label + " " + nm); continue; }
                t.gameObject.SetActive(false);
                n++;
            }
            return n;
        }

        // ══════════════════════════════════════════════════════
        // ② 칸막이 — 원본 부재 복제
        // ══════════════════════════════════════════════════════
        static void BuildPartitions()
        {
            var root = Recreate(PartRoot).transform;

            // 선아 방 │ 대청 — 문 앞면(원본 −Z면)이 대청(+X)을 보도록 −90°
            Partition(root, "칸막이_선아방", PartWest, -90f, true, openable: true);
            // 대청 │ 아버지 방 — 앞면이 대청(−X)을 보도록 +90°
            Partition(root, "칸막이_아버지방", PartEast, 90f, false, openable: true);
            // ⚠️ 아래 셋은 **살대가 방을 보도록 원본 남벽과 반대로 돌려 세운다.**
            //    창호는 살대가 실내를 향하고 종이를 바깥쪽에 바르는 게 한옥 문법이다. 팩 메시도
            //    한쪽 면에만 살대가 있어서, 방향을 잘못 잡으면 방 안에서 **민무늬 종이판**만 보인다
            //    (사용자 보고 — 실측으로 확인: 바깥에서는 세살+문고리, 안에서는 맨 종이였다).
            // 대청 남면 출입 창호문 — 닫힌 채 고정. 바깥(툇마루)이 −Z라 180°.
            Partition(root, "칸막이_대청남면", PartSouth, 180f, false, openable: false);
            // 아버지 방 북면 — 부엌을 막는 고정 창호문. 바깥(부엌)이 +Z라 0°.
            Partition(root, "칸막이_부엌", PartKitchen, 0f, false, openable: false);

            // 뒷골방 북면 — 원본에 벽이 아예 없어 **하늘이 뚫려 있던** 자리 (레이 144발 중 24발 누출).
            WallPanel(root, "벽_뒷골방북", PartNook, 180f);
        }

        /// <summary>창호문 한 칸. <paramref name="openInPlusX"/>는 여닫이가 열리는 쪽.</summary>
        static void Partition(Transform root, string name, Vector3 axis, float yaw,
                              bool openInPlusX, bool openable)
        {
            var group = new GameObject(name);
            group.transform.SetParent(root, false);

            var leaves = new List<GameObject>();
            foreach (var src in DoorModule)
            {
                var go = Clone(src, DoorModuleAxis, axis, yaw, group.transform);
                if (go == null) continue;
                bool isLeaf = src.StartsWith("SM_Door03A");
                if (openable && (src == LeafA || src == LeafB)) { leaves.Add(go); continue; }
                // ⚠️ **문짝에만 콜라이더를 단다.** 문틀(DoorFrame01A)은 가운데가 뚫린 ㅁ자인데
                //    박스 콜라이더는 메시 AABB라 **개구부까지 통짜로 막아 버린다** — 문이 열려도
                //    못 지나간다. 문 위 벽(y 2.96~3.89)·창방은 캡슐(1.8) 위쪽이라 막을 게 없고,
                //    오히려 콜라이더를 두면 CC의 스텝 상승 스윕(height+stepOffset)이 문간에서
                //    헤드룸에 걸린다(집무실에서 밟은 함정). 그래서 둘 다 콜라이더 없이 둔다.
                if (isLeaf) AddBlocker(go);
            }

            if (!openable || leaves.Count != 2) return;

            // ── 여닫이 두 짝 ──────────────────────────────────
            // 부모를 원점·무회전으로 두면 부모 로컬 = 월드라 피벗 계산이 그대로 읽힌다.
            var hinge = new GameObject("여닫이");
            hinge.transform.SetParent(group.transform, false);
            hinge.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // 칸막이는 Z방향으로 뻗는다 — z가 작은 짝이 '좌', 큰 짝이 '우'.
            leaves.Sort((a, b) => Center(a).z.CompareTo(Center(b).z));
            var lo = leaves[0]; var hi = leaves[1];
            lo.name = "문짝_좌"; hi.name = "문짝_우";
            lo.transform.SetParent(hinge.transform, true);
            hi.transform.SetParent(hinge.transform, true);
            AddBlocker(lo); AddBlocker(hi);

            var bLo = WorldBounds(lo); var bHi = WorldBounds(hi);
            // 경첩은 각 짝의 **바깥쪽** 끝 (좌=−Z끝, 우=+Z끝)
            var pLo = new Vector3(bLo.center.x, bLo.center.y, bLo.min.z);
            var pHi = new Vector3(bHi.center.x, bHi.center.y, bHi.max.z);

            // 자유단(좌 = +Z, 우 = −Z)이 열리는 쪽으로 오는 Y회전 부호 — 손으로 유도한 값이다.
            float sign = openInPlusX ? 1f : -1f;

            var d = hinge.AddComponent<DoubleHingeDoor>();
            d.displayName = "창호문";
            d.leftLeaf = lo.transform; d.rightLeaf = hi.transform;
            d.leftPivot = pLo; d.rightPivot = pHi;
            d.leftAngle = 100f * sign; d.rightAngle = -100f * sign;
            d.duration = 0.85f;
        }

        /// <summary>막힌 벽 한 칸 (하방 + 머름 + 창 + 벽 + 상부).</summary>
        static void WallPanel(Transform root, string name, Vector3 axis, float yaw)
        {
            var group = new GameObject(name);
            group.transform.SetParent(root, false);
            foreach (var src in WallModule)
            {
                var go = Clone(src, WallModuleAxis, axis, yaw, group.transform);
                if (go == null) continue;
                // 캡슐 머리(바닥 1.0 + 1.8 = 2.8)보다 아래 부재만 막는다 — 위쪽 창방·상부벽에
                // 콜라이더를 두면 얻는 것 없이 스텝 스윕 헤드룸만 잡아먹는다.
                if (WorldBounds(go).min.y < 3.0f) AddBlocker(go);
            }
        }

        /// <summary>
        /// 원본 부재 하나를 복제해 목표 자리에 놓는다.
        /// 팩 부재는 부모 GO의 로컬 위치가 메시 중심과 무관하므로 **렌더러 바운즈 중심**을 기준으로
        /// 잡는다: 모듈 기준점에서의 XZ 오프셋을 yaw만큼 돌린 뒤, 회전시킨 복제본의 중심이
        /// 그 자리에 오도록 평행이동한다. y는 원본 높이를 그대로 쓴다(층고가 같은 자리들이므로).
        /// </summary>
        static GameObject Clone(string childName, Vector3 srcAxis, Vector3 dstAxis, float yaw, Transform parent)
        {
            var src = _prefab.transform.Find(childName);
            if (src == null) { _missing.Add(childName); return null; }

            var srcCenter = Center(src.gameObject);
            var o = srcCenter - srcAxis;
            var o2 = Quaternion.Euler(0f, yaw, 0f) * new Vector3(o.x, 0f, o.z);
            var target = new Vector3(dstAxis.x + o2.x, srcCenter.y, dstAxis.z + o2.z);

            var go = Object.Instantiate(src.gameObject);
            go.name = childName;
            go.transform.SetParent(parent, true);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * src.rotation;
            go.transform.position += target - Center(go);

            // 원본에서 딸려 온 빈 MeshCollider는 복제본에서는 지운다 (여기선 우리가 박스를 단다)
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>(true)) Object.DestroyImmediate(mc);
            return go;
        }

        /// <summary>부재 하나에 박스 콜라이더 — 메시 바운즈를 그 GO 로컬로 환산해 딱 맞춘다.
        /// (프로젝트 규칙: MeshCollider 금지. 회전한 부재라도 로컬 AABB는 타이트하다)</summary>
        static void AddBlocker(GameObject go)
        {
            if (go == null) return;
            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var b = LocalMeshBounds(go.transform, mf);
            var col = go.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;
        }

        // ══════════════════════════════════════════════════════
        // ③ 재질 광택 죽이기
        // ══════════════════════════════════════════════════════
        /// <summary>
        /// 팩 재질(`Assets/KimMyeonggwanHouse/Material/*`)은 **전부 `_Smoothness = 1.0`** 이다(실측).
        /// 야외 데모에서는 티가 안 났지만 실내에서 점광원을 켜면 창호지·회벽이 **비닐처럼 번들거리고
        /// 광원 자리마다 전구 같은 흰 반사점**이 찍힌다 — 밤 등잔 옆 벽에서 그대로 드러났다.
        /// 원본 폴더는 수정 금지이므로 **우리 폴더로 복제해 광택만 낮춰** 갈아 끼운다.
        /// 재질 종류가 10여 개뿐이라 에셋은 몇 개 안 늘고, 대신 실내 인상이 통째로 바뀐다.
        ///
        /// (반사 큐브맵을 끄는 것과는 별개 문제다 — 그쪽은 환경 반사, 이쪽은 광원 스페큘러다.)
        /// </summary>
        /// <summary>양면으로 그려야 하는 재질 — 창호지·창살처럼 **안쪽에서도 보여야** 하는 것.</summary>
        static readonly HashSet<string> DoubleSided = new HashSet<string> { "MI_Door01A" };

        // ══════════════════════════════════════════════════════
        /// <summary>
        /// **외피의 창호를 돌려세워 살대가 방을 보게 한다.**
        ///
        /// 팩의 창·문 메시는 한쪽 면에만 세살(격자)이 있고 반대면은 민무늬 종이다.
        /// 원본은 그 살대가 **바깥을 향하도록** 설치돼 있어서, 실내에서 창을 보면 맨 종이판만
        /// 보였다 (실측: 같은 창을 바깥에서 보면 세살+문고리, 안에서 보면 아무 무늬 없는 판).
        /// 한옥 창호는 살대가 실내를 향하고 종이를 바깥면에 바르므로 **돌려세우는 쪽이 옳다.**
        /// 이 집은 외관을 쓰지 않으니 바깥 인상이 바뀌어도 손해가 없다.
        ///
        /// 문짝을 자기 바운즈 중심 세로축으로 180° 돌린다 — 두께가 0.11이라 자리도 그대로다.
        /// 프리팹 인스턴스의 **트랜스폼 오버라이드**라 원본 에셋은 건드리지 않는다.
        ///
        /// ⚠️ 실내 칸막이(대청 ↔ 두 방)는 대상이 아니다. 이미 살대가 대청을 보고 있고,
        ///    돌리면 반대쪽 방이 민무늬를 보게 된다.
        /// </summary>
        static void FaceLatticeInward(Transform house)
        {
            int n = 0;
            foreach (var r in house.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue;
                if (!(r.name.StartsWith("SM_Door") || r.name.StartsWith("SM_Window"))) continue;
                var c = r.bounds.center;
                // 외피 벽선 — 이 밖은 전부 바깥(마당·툇마루·부엌 등 플레이 구역 밖)이다
                bool envelope = c.z < -1.40f || c.z > 2.30f || c.x < -6.00f || c.x > 6.00f;
                if (!envelope) continue;

                var leaf = r.transform.parent != null && r.transform.parent != house
                         ? r.transform.parent : r.transform;
                leaf.RotateAround(c, Vector3.up, 180f);
                n++;
            }
            Debug.Log("[선아집] 창호 " + n + "짝을 돌려세움 — 살대가 방 안을 향한다");
        }

        static void DullPackMaterials()
        {
            const string dir = "Assets/_Project/Gyeonu/Art/Materials/SeonaHouse/";
            System.IO.Directory.CreateDirectory(dir);

            // 재질별 목표 광택 — 한지·회벽은 거의 무광, 마루는 결이 살 만큼만
            var target = new Dictionary<string, float>
            {
                { "MI_Door01A", 0.10f },        // 창호지 + 세살
                { "MI_WhiteWall01A", 0.07f },   // 회벽
                { "MI_Wood01A", 0.18f },
                { "MI_Wood02A", 0.18f },
                { "MI_Floor01A", 0.28f },       // 마루널 — 약간의 윤은 남긴다
                { "MI_RafterA", 0.14f },        // 서까래·개판
                { "MI_Stone01A", 0.12f },
                { "MI_Stone01B", 0.12f },
                { "MI_Stone02A_Tile", 0.14f },
                { "MI_Ground01B", 0.10f },
            };

            var map = new Dictionary<Material, Material>();
            var roots = new[] { GameObject.Find(HouseRoot), GameObject.Find(PartRoot) };

            int swapped = 0;

            foreach (var root in roots)
            {
                if (root == null) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var src = mats[i];
                        if (src == null) continue;
                        var path = AssetDatabase.GetAssetPath(src);
                        if (!path.StartsWith("Assets/KimMyeonggwanHouse/")) continue;

                        Material dull;
                        if (!map.TryGetValue(src, out dull))
                        {
                            string outPath = dir + "선아집_" + src.name + ".mat";
                            dull = AssetDatabase.LoadAssetAtPath<Material>(outPath);
                            if (dull == null)
                            {
                                dull = new Material(src);
                                AssetDatabase.CreateAsset(dull, outPath);
                            }
                            else dull.CopyPropertiesFromMaterial(src);
                            float s;
                            dull.SetFloat("_Smoothness", target.TryGetValue(src.name, out s) ? s : 0.16f);
                            // ⚠️ **창호·창은 양면으로 그린다.** 원본 메시가 단면(one-sided)이라
                            //    실내에서 창을 보면 뒷면이 컬링돼 **창호지가 통째로 사라지고 바깥
                            //    풍경처럼 뚫려 보인다** (사용자 보고). 원본 폴더는 수정 금지라
                            //    복제본에서 렌더 페이스를 Both로 바꾼다.
                            //    URP Lit: _Cull 0=Both / 1=Front / 2=Back(기본). Both면 뒷면 노멀도
                            //    셰이더가 뒤집어 주므로 안쪽에서도 제대로 음영이 잡힌다.
                            if (DoubleSided.Contains(src.name))
                            {
                                dull.SetFloat("_Cull", 0f);
                                dull.doubleSidedGI = true;
                                dull.renderQueue = -1;
                            }
                            EditorUtility.SetDirty(dull);
                            map[src] = dull;
                        }
                        mats[i] = dull; changed = true;
                    }
                    if (changed) { r.sharedMaterials = mats; swapped++; }
                }
            }
            Debug.Log("[선아집] 재질 광택 조정 — 팩 재질 " + map.Count + "종 복제, 렌더러 " + swapped + "개 교체");
        }

        // ══════════════════════════════════════════════════════
        // ④ 종도리 — 지붕 메시의 용마루 틈 막기
        // ══════════════════════════════════════════════════════
        /// <summary>
        /// 원본 지붕은 능선을 따라 **실제로 틈이 벌어져 있다** — 실내에서 위를 보면 하늘이
        /// 한 줄로 새어 들어온다(조사 사진 ③). 사방 레이 테스트에서도 고각 44°에서만 누출이
        /// 잡혔다. 그 틈을 종도리(마룻대) 한 대로 덮는다 — 원래 그 자리에 있어야 하는 부재라
        /// 시각적으로도 자연스럽다.
        /// </summary>
        static void BuildRidgeBeam()
        {
            var root = Recreate("선아집_종도리").transform;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "종도리";
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 4.05f, -0.04f);
            go.transform.localScale = new Vector3(15.7f, 0.24f, 0.26f);
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());   // 서까래 위 — 막을 필요 없다
            var mat = FindPackMaterial("MI_Wood01A");
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            else _missing.Add("MI_Wood01A (종도리 재질)");
        }

        // ══════════════════════════════════════════════════════
        // ④ 어둠막 — 지붕 개판 틈으로 새는 하늘 가리기
        // ══════════════════════════════════════════════════════
        /// <summary>
        /// 원본 지붕은 개판(천장널)이 **한 장씩 띄엄띄엄 놓여** 널 사이가 실제로 벌어져 있다.
        /// 실내에서 올려다보면 그 틈마다 하늘이 파랗게 줄줄이 비친다(실측 사진). 널을 메우려면
        /// 지붕 메시를 손봐야 하는데 그건 원본 에셋 수정이라 금지다 — 대신 집 전체를 감싸는
        /// **안쪽을 보는 어둠 상자**를 둔다. 틈으로는 이제 어두운 흙빛만 보인다(실제 지붕 속
        /// 적심·알매흙이 그렇다). Unlit이라 광원 예산과 무관하고, 그림자도 만들지 않는다.
        ///
        /// ⚠️ 컬링을 Front로 뒤집는 게 핵심이다. 기본 Back이면 상자 바깥면만 그려져
        ///    실내에서는 아무것도 안 보인다.
        /// </summary>
        static void BuildDarkShell()
        {
            var root = Recreate("선아집_어둠막").transform;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "어둠막";
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 6f, 0f);
            go.transform.localScale = new Vector3(48f, 34f, 40f);
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());

            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterial = DarkMaterial();
        }

        static Material DarkMaterial()
        {
            const string path = "Assets/_Project/Gyeonu/Art/Materials/SeonaHouse/선아집_어둠막.mat";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", new Color(0.021f, 0.019f, 0.016f));
            m.SetFloat("_Cull", 1f);      // 1 = Front — 상자 안쪽 면을 그린다
            m.doubleSidedGI = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ══════════════════════════════════════════════════════
        // ⑤ 조명 — 낮/밤 두 상태 (집무실과 같은 방식)
        // ══════════════════════════════════════════════════════
        /// <summary>
        /// 【왜 방향광이 아닌가】 집무실과 같은 이유다. 이 집은 지붕까지 덮인 닫힌 상자라
        /// 그림자를 켠 Directional Light는 실내에 한 줌도 들어오지 못한다. 창호지도 불투명
        /// 지오메트리라 밖에 둔 스포트도 통째로 막힌다. 그래서 **창호문 바로 안쪽에 살대 쿠키를
        /// 물린 스포트**를 두어 "창에서 들어온 빛 + 바닥의 살대 그림자"를 만든다.
        ///
        /// ⚠️ URP `PC_RPAsset`의 추가 광원 한도가 **4**다 (그 에셋은 수정 금지 영역).
        ///    낮/밤 그룹 **각각** 4개를 넘기면 안 된다 — 넘기면 오브젝트마다 가까운 4개만
        ///    골라 켜져 방마다 밝기가 들쭉날쭉해진다. 지금 낮 4 / 밤 3.
        /// </summary>
        static void BuildLighting()
        {
            var prev = Object.FindFirstObjectByType<OfficeTimeOfDay>();
            bool wasNight = prev != null && prev.IsNight;

            var group = Recreate(LightRoot);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;

            // ⚠️ **반사 큐브맵을 끄는 게 이 씬에서 제일 큰 한 줄이다.** `skybox = null` 로 해도
            //    반사 소스는 여전히 Skybox 모드라 기본 하늘 큐브맵이 남는다. 지붕 서까래·개판
            //    재질(MI_RafterA)이 광택을 갖고 있어, 널 사이 모서리마다 그 하늘이 **파란 줄**로
            //    비쳐 천장이 형광등처럼 줄무늬로 빛났다(1차 실측 — 하늘이 새는 줄 알고 어둠막부터
            //    세웠지만 진단용 빨강으로 칠해 보니 어둠막이 아니었다).
            //    Custom + 텍스처 null = 반사 검정. 그제야 연등천장이 제 색으로 어두워진다.
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;

            var cookie = BuildLatticeCookie();
            var day = new GameObject("낮"); day.transform.SetParent(group.transform, false);
            var night = new GameObject("밤"); night.transform.SetParent(group.transform, false);

            // ══════ 낮 — 마당 쪽(남) 창호문에서 들어오는 빛 ══════
            // 방향광은 그림자를 끈다 — 켜면 닫힌 상자라 실내에 한 줌도 못 들어온다. 끈 채로 두면
            // 방향성 있는 명암만 얹히는 값싼 채움이 되고, 진짜 그림자는 창 스포트가 만든다.
            // 동쪽 골방은 추가 광원 한도(4)를 다 써서 전용 광원이 없다 — 형태만 읽히게 이걸로 받친다.
            Dir(day, "방향광_낮", new Vector3(34f, 20f, 0f), 0.52f, new Color(1.00f, 0.95f, 0.86f));

            // 세 방 각각 남쪽 창호문 안쪽. 겨냥을 **가파르게 아래로** 꽂아야 빛 웅덩이가
            // 바닥에 생긴다 — 수평에 가까우면 맞은편 회벽이 흰 판때기로 타 버린다
            // (1차 배치에서 그대로 밟았다: intensity 150 + 수평 겨냥 → 북벽 전소).
            // ⚠️ 방이 2.5~4.9m로 좁아 집무실(5.8×4.4) 값을 그대로 쓰면 안 된다. 1/4 수준이다.
            // 【광원 자리 = 실제 개구부 자리】(사용자 지시 2026-08-17). 방마다 실측한 창·문 위치:
            //   선아 방  남 창호문 6짝 x −5.84~−1.97 (벽 z −1.60) · 북 창 3 (x −5.10/−3.01/−2.40, z 2.58)
            //           · 서 문 2 (x −6.31, z −0.53/0.11)
            //   대청     남 창호문 4짝 (문틀 x −0.10, 벽 z −1.70) · 북 창 2 (x −0.30/0.31, z 2.51)
            //   아버지 방 남 창 2 (x 2.40/3.00, 벽 z −1.78, 머름 0.31)
            //   동쪽 골방 남 창 2 · 동 판문 2
            // ⚠️ **창이 없는 자리에 광원을 두지 않는다.** 1차에는 선아 방 한복판 허공(−3.90, 0.75)에
            //    채움광이 떠 있어 벽도 창도 없는 데서 빛이 쏟아졌다 — 북창 안쪽으로 옮겼다.
            Shadowed(Spot(day, "선아방_남창햇살", new Vector3(-3.60f, 2.05f, -1.48f),
                          new Vector3(-0.10f, -1.45f, 1.00f), 42f, 7.5f, 100f,
                          new Color(1.00f, 0.92f, 0.76f)), 0.92f, cookie);

            // ⚠️ 사거리를 4.6으로 끊는다 — 6.0이면 3.9m 뒤 북벽 창호지까지 원뿔 윗자락이 닿아
            //    종이 두 짝이 새하얗게 탔다(실측). 빛은 앞쪽 바닥에만 고이는 게 맞다.
            Shadowed(Spot(day, "대청_남창햇살", new Vector3(-0.10f, 2.05f, -1.40f),
                          new Vector3(0.03f, -1.55f, 1.00f), 34f, 4.6f, 104f,
                          new Color(1.00f, 0.93f, 0.79f)), 0.92f, cookie);

            // 남창 2짝(x 2.40 / 3.00)의 가운데. 머름이 0.31 있어 창 높이(y 1.3~2.7)에서 들어온다.
            Shadowed(Spot(day, "아버지방_남창햇살", new Vector3(2.70f, 2.00f, -1.62f),
                          new Vector3(0.10f, -1.50f, 1.00f), 36f, 7.0f, 100f,
                          new Color(1.00f, 0.91f, 0.74f)), 0.88f, cookie);

            // 선아 방 뒤쪽 채움 — **아주 약하게, 그림자·쿠키 없이.**
            // ⚠️ 사용자 지시(2026-08-17): "선아 방은 빛이 한 방향에서만 들어와야 한다.
            //    의걸이장·찬장이 있는 쪽(북벽) 창호지에서는 빛이 안 들어오게." 앞서 북창을
            //    제대로 된 창빛(22, 쿠키+그림자)으로 켰더니 **남·북 양쪽에서 빛이 들어와**
            //    방향이 읽히지 않았다. 이제 이건 창빛이 아니라 **바닥 반사 정도의 바운스**다 —
            //    살대 그림자를 만들지 않으므로 "두 번째 창"으로 읽히지 않는다.
            //    (창호지 자체는 그대로 둔다 — 빛만 줄였다)
            var fill = Spot(day, "선아방_뒤바운스", new Vector3(-3.30f, 1.90f, 1.55f),
                            new Vector3(0f, -1f, -0.35f), 3.2f, 3.2f, 120f,
                            new Color(0.92f, 0.90f, 0.86f));
            fill.innerSpotAngle = 90f;   // 거의 균일 — 방향성 없는 바운스로 읽히게

            // ══════ 밤 — 어둡고 등잔 하나 ══════
            Dir(night, "방향광_밤", new Vector3(22f, 20f, 0f), 0.08f, new Color(0.60f, 0.72f, 1.00f));

            // 달빛도 낮과 **같은 개구부 자리**에서 들어온다 (대청 남 창호문 / 선아 방 남 창호문).
            //
            // ⚠️ 달빛 세팅은 낮 창빛을 그대로 줄인 게 아니다 — 세 가지를 따로 잡아야 한다:
            //  ① **세기**: 광원이 바닥에서 1m 남짓이라 세기를 15→6→2로 낮춰도 바닥이 계속 흰색으로
            //     탔다. 근거리에서는 1/d²이 워낙 커서 세기만으로는 안 잡힌다.
            //  ② **물매**: 광원을 2.45로 올리고 물매를 38°로 눕혀 빛웅덩이를 방 안쪽으로 밀어냈다.
            //     벽 바로 밑에 꽂히던 게 사라지면서 비로소 "달빛"으로 읽힌다.
            //  ③ **사거리·원뿔**: 4.2/5.2로 끊어 맞은편 벽에 닿지 않게, 원뿔도 76/84°로 좁혀
            //     윗벽 splash를 없앴다. 안 그러면 밤이 아니라 흐린 낮처럼 보인다.
            //
            // ⚠️ **선아방_달빛은 반드시 그림자를 켠다.** 그림자 없는 스포트는 벽을 그냥 통과해서
            //    선아 방 달빛이 칸막이를 뚫고 **대청 바닥까지** 비췄다 — 대청 달빛 세기를 아무리
            //    낮춰도 그 웅덩이가 안 없어져 한참 헤맸다.
            Shadowed(Spot(night, "대청_달빛", new Vector3(-0.10f, 2.45f, -1.55f),
                          new Vector3(0.05f, -0.80f, 1.00f), 2.6f, 4.2f, 76f,
                          new Color(0.58f, 0.70f, 1.00f)), 0.90f, cookie);
            FindLight(night, "대청_달빛").innerSpotAngle = 32f;

            var moonW = Spot(night, "선아방_달빛", new Vector3(-3.60f, 2.45f, -1.62f),
                             new Vector3(-0.10f, -0.78f, 1.00f), 3.0f, 5.2f, 84f,
                             new Color(0.56f, 0.68f, 1.00f));
            moonW.innerSpotAngle = 34f;
            Shadowed(moonW, 0.92f, cookie);   // ← 그림자 필수 (위 주석)

            // 등잔 — 밤의 유일한 실내 광원. 아버지 방 서안 위.
            // ⚠️ 광원을 등잔 몸통 **안**에 두면 그림자 때문에 제 메시가 빛을 다 막는다.
            //    기물 윗면 바로 위에 얹어 서안으로 쏟아지는 웅덩이를 만든다.
            //    사거리를 방보다 작게 잡아야 "등잔 하나"로 읽힌다 (집무실 실측).
            // ⚠️ 광원 자리는 **촛대 불꽃 높이**(서안 위 2.05)다. LampSpot 바로 위(+0.52)에 두면
            //    상판 안쪽에서 켜져 서안이 제 빛을 가리고, 세기를 올려 보정하면 0.6m 앞 창호지가
            //    통째로 하얗게 탔다(실측). 세기는 4.2면 충분하다 — 방보다 작은 사거리로 끊어야
            //    구석이 어둠에 잠기고 "등잔 하나"로 읽힌다.
            var oil = Lamp(night, "등잔_불빛", LampSpot + new Vector3(0.38f, 1.09f, 0f), 4.2f, 2.7f,
                           new Color(1.00f, 0.78f, 0.48f));
            oil.shadows = LightShadows.Soft;
            oil.shadowStrength = 0.60f;
            oil.shadowBias = 0.05f;
            oil.shadowNormalBias = 0.25f;

            var tod = group.GetComponent<OfficeTimeOfDay>();
            if (tod == null) tod = group.AddComponent<OfficeTimeOfDay>();
            tod.dayGroup = day;
            tod.nightGroup = night;
            // 살림집 실내라 집무실보다 앰비언트를 조금 따뜻하게 (회벽이 아니라 나무·한지다)
            tod.ambientSkyDay = new Color(0.46f, 0.44f, 0.39f);
            tod.ambientEquatorDay = new Color(0.35f, 0.33f, 0.28f);
            tod.ambientGroundDay = new Color(0.19f, 0.17f, 0.13f);
            tod.ambientSkyNight = new Color(0.048f, 0.056f, 0.080f);
            tod.ambientEquatorNight = new Color(0.032f, 0.036f, 0.048f);
            tod.ambientGroundNight = new Color(0.018f, 0.019f, 0.023f);
            tod.paperRenderers = new Renderer[0];   // 창호지는 팩 재질 그대로 (원본 수정 금지)
            tod.SetNight(wasNight);

            Debug.Log("[선아집] 조명 — 낮 추가광원 4 / 밤 3 (URP 한도 4). 현재 = " + (wasNight ? "밤" : "낮"));
        }

        /// <summary>
        /// 세살 쿠키 — 창호문 살대 그림자를 흉내낸다. 창호지가 불투명 지오메트리라
        /// 밖에 둔 광원은 통째로 막힌다. 그래서 광원은 실내에 두고 무늬만 쿠키로 넣는다.
        /// 세살문이라 세로살이 촘촘하고 가로살은 위·중·아래 세 줄뿐이다.
        /// </summary>
        static Texture2D BuildLatticeCookie()
        {
            const string path = "Assets/_Project/Gyeonu/Art/Textures/SeonaHouse/선아집_살대쿠키.png";
            var exist = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (exist != null) return exist;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // 가장자리는 문틀 — 완전히 막는다
                    bool frame = x < 14 || x > N - 15 || y < 14 || y > N - 15;
                    bool bar = (x % 26) < 6;                        // 세로살
                    bool rail = (y > 60 && y < 70) || (y > 126 && y < 136) || (y > 192 && y < 202);
                    byte v = (byte)(frame ? 0 : (bar || rail ? 60 : 255));
                    px[y * N + x] = new Color32(v, v, v, v);
                }
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ══════════════════════════════════════════════════════
        // ⑤ 마커
        // ══════════════════════════════════════════════════════
        static void BuildMarkers()
        {
            var root = Recreate(MarkerRoot).transform;
            Marker(root, "SpawnPoint_FromVillage", SpawnFromVillage, SpawnYaw);
            Marker(root, "Exit_ToVillage", ExitToVillage, 180f);   // 마당(남)을 바라본다
        }

        static void Marker(Transform root, string name, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        }

        // ══════════════════════════════════════════════════════
        // 낮/밤 전환
        // ══════════════════════════════════════════════════════
        [MenuItem("Tools/이문록/선아 집 ▸ 낮으로")]
        public static void SetDay() => SetTime(false);

        [MenuItem("Tools/이문록/선아 집 ▸ 밤으로")]
        public static void SetNight() => SetTime(true);

        static void SetTime(bool night)
        {
            var tod = Object.FindFirstObjectByType<OfficeTimeOfDay>();
            if (tod == null) { Debug.LogError("[선아집] 조명 그룹이 없습니다 — 「① 씬 조립」을 먼저"); return; }
            tod.SetNight(night);
            EditorUtility.SetDirty(tod);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[선아집] " + (night ? "밤" : "낮") + "으로 전환");
        }

        // ══════════════════════════════════════════════════════
        // 헬퍼
        // ══════════════════════════════════════════════════════
        internal static GameObject Recreate(string name)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            return new GameObject(name);
        }

        /// <summary>렌더러 월드 바운즈의 중심 — 팩 부재의 위치는 이걸로만 다룬다.</summary>
        internal static Vector3 Center(GameObject go) => WorldBounds(go).center;

        internal static Bounds WorldBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>메시 바운즈를 지정한 트랜스폼의 로컬 좌표로 환산한다 (박스 콜라이더용).</summary>
        internal static Bounds LocalMeshBounds(Transform root, MeshFilter mf)
        {
            var mb = mf.sharedMesh.bounds;
            Bounds b = new Bounds(); bool first = true;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x,
                                    (i & 2) == 0 ? mb.min.y : mb.max.y,
                                    (i & 4) == 0 ? mb.min.z : mb.max.z);
                var lp = root.InverseTransformPoint(mf.transform.TransformPoint(c));
                if (first) { b = new Bounds(lp, Vector3.zero); first = false; } else b.Encapsulate(lp);
            }
            return b;
        }

        /// <summary>팩 원본 재질을 **읽기만** 한다 (원본 폴더 수정 금지 규칙).</summary>
        internal static Material FindPackMaterial(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material " + name))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.StartsWith("Assets/KimMyeonggwanHouse/")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m != null && m.name == name) return m;
            }
            return null;
        }

        /// <summary>그룹 안에서 이름으로 광원 찾기 (Spot 반환값을 못 받은 자리용).</summary>
        static Light FindLight(GameObject group, string name)
        {
            var t = group.transform.Find(name);
            return t != null ? t.GetComponent<Light>() : null;
        }

        static Light Spot(GameObject parent, string name, Vector3 pos, Vector3 aimDir,
                          float intensity, float range, float angle, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(aimDir.normalized, Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.5f;
            l.color = c;
            l.shadows = LightShadows.None;
            return l;
        }

        static Light Lamp(GameObject parent, string name, Vector3 pos, float intensity, float range, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = intensity;
            l.range = range;
            l.color = c;
            l.shadows = LightShadows.None;
            return l;
        }

        static void Dir(GameObject parent, string name, Vector3 euler, float intensity, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = c;
            l.shadows = LightShadows.None;   // 닫힌 상자 — 켜도 실내에 못 들어온다
        }

        static void Shadowed(Light l, float strength, Texture cookie)
        {
            l.shadows = LightShadows.Soft;
            l.shadowStrength = strength;
            l.shadowBias = 0.03f;
            l.shadowNormalBias = 0.20f;
            l.cookie = cookie;
        }
    }
}
