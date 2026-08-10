using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관측실 소품 배치 (2026-08-10, 멱등) — "여러 사람이 이어 써온 방".
    ///
    /// 세 시대가 배치로 드러나야 한다:
    ///   시대1_직녀   — 북서 구석·벽 쪽. 손 안 탄 것. 먼지 머티리얼(어둡고 채도 낮게)
    ///   시대2_아버지 — 서편 평상 작업 공간. 정돈된 채로. 약한 먼지
    ///   시대3_선아   — 남·남동 중앙 가까이. 흐트러진 채, 등잔이 아직 켜져 있다. 원색 그대로
    ///   낙석주변     — 무너진 계단 어귀. 넘어지고 엎어진 것들
    ///
    /// 배치는 방 중심 기준 극좌표(각도·반지름)로 준다. 각도 0=계단 쪽(+Z), 90=혼천의 쪽(+X),
    /// 180=암문 입구(-Z), 270=서편(-X). 배치 후 통행로·기물·등잔대 침범을 자동 검증한다.
    ///
    /// 콜라이더는 마을 방침대로: 팩 콜라이더 전부 OFF → 크고 높은 것만 박스로 다시 준다.
    /// 머티리얼은 원본 팩을 건드리지 않고 우리 폴더에 먼지 변형본을 복제해서 쓴다.
    /// </summary>
    public static class ObservatoryPropPlanter
    {
        const string RootName = "관측실_소품";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Observatory/Props";
        static readonly Vector3 C = new Vector3(18.2f, -3.06f, 34.6f);
        const float FloorY = -3.06f;
        const float WallR = 5.825f;   // 석벽 안쪽면

        enum Era { 직녀, 아버지, 선아, 낙석 }

        struct P
        {
            public Era era; public string asset; public string label;
            public float ang, rad;      // 방 중심 기준 극좌표
            public float yawJitter;     // 중심을 향한 방향에서 추가 회전
            public float pitch, roll;   // 기울임 (기대놓기·넘어뜨리기)
            public float lift;          // 바닥 대신 띄울 높이 (벽 걸이·평상 위)
            public bool solid;          // 박스 콜라이더를 줄 큰 물건
            public bool randomYaw;      // 벽을 향하지 않고 아무렇게나
            public bool onWall;         // 벽 걸이 — 이완·벽 검사에서 제외
        }

        static readonly P[] Props =
        {
            // ── 시대1 · 직녀 — 북서 구석, 먼지 쌓인 채 ──────────────────
            new P { era=Era.직녀, asset="Cabinet01b",       label="장",     ang=338, rad=5.05f, yawJitter=84, solid=true },
            new P { era=Era.직녀, asset="Mungap",           label="문갑",   ang=318, rad=5.20f, yawJitter=88, solid=true },
            new P { era=Era.직녀, asset="Rice_Chest",       label="궤",     ang=327, rad=5.15f, yawJitter=-9, solid=true },
            new P { era=Era.직녀, asset="Jar01c",           label="항아리", ang=332, rad=5.42f, yawJitter=12 },
            new P { era=Era.직녀, asset="Jar01a",           label="항아리", ang=311, rad=5.42f, yawJitter=-14, solid=true },
            new P { era=Era.직녀, asset="Straw_Jar01a",     label="짚독",   ang=304, rad=5.38f, yawJitter=20 },
            new P { era=Era.직녀, asset="Basket01a",        label="소쿠리", ang=294, rad=5.28f, yawJitter=-22 },
            new P { era=Era.직녀, asset="Geomungo",         label="거문고", ang=284, rad=5.32f, yawJitter=8, pitch=-72 },
            new P { era=Era.직녀, asset="Earthenware_Bowl", label="사발",   ang=323, rad=4.80f, randomYaw=true },
            new P { era=Era.직녀, asset="Tablet01b",        label="편액",   ang=318, rad=5.70f, lift=1.55f, yawJitter=90, onWall=true },

            // ── 시대2 · 아버지 — 서편 평상 작업 공간, 정돈 ──────────────
            new P { era=Era.아버지, asset="Low_Wooden_Bench",   label="평상",   ang=270, rad=3.95f, yawJitter=-4, solid=true },
            new P { era=Era.아버지, asset="Mungap",             label="문갑",   ang=258, rad=5.20f, yawJitter=5, solid=true },
            new P { era=Era.아버지, asset="Pantry_Shelf",       label="선반",   ang=282, rad=5.30f, yawJitter=-5, solid=true },
            new P { era=Era.아버지, asset="Lampstand",          label="등잔대", ang=263, rad=3.25f, yawJitter=10 },
            new P { era=Era.아버지, asset="Baduk01a",           label="바둑판", ang=272, rad=3.60f, lift=0.47f, yawJitter=-7 },
            new P { era=Era.아버지, asset="Baduk02a",           label="바둑알통", ang=277, rad=3.85f, lift=0.47f, randomYaw=true },
            new P { era=Era.아버지, asset="Wooden_Tray",        label="목판",   ang=265, rad=4.15f, lift=0.47f, yawJitter=14 },
            new P { era=Era.아버지, asset="Bowl01a",            label="그릇",   ang=266, rad=4.12f, lift=0.53f, randomYaw=true },
            new P { era=Era.아버지, asset="Small_Dining_Table", label="소반",   ang=252, rad=4.10f, yawJitter=-12 },
            new P { era=Era.아버지, asset="Jar01b",             label="항아리", ang=248, rad=5.25f, yawJitter=16, solid=true },
            new P { era=Era.아버지, asset="Wicker_Basket",      label="바구니", ang=288, rad=5.00f, randomYaw=true },
            new P { era=Era.아버지, asset="Brazer",             label="화로",   ang=252, rad=4.60f, randomYaw=true },

            // ── 시대3 · 선아 — 남·남동 중앙 가까이, 급히 두고 간 채 ──────
            new P { era=Era.선아, asset="Mat",           label="자리",   ang=160, rad=3.10f, yawJitter=13 },
            new P { era=Era.선아, asset="Straw_Mat01b",  label="말아둔짚자리", ang=138, rad=2.95f, yawJitter=-34, roll=90 },
            new P { era=Era.선아, asset="Pillow",        label="방석",   ang=152, rad=2.55f, randomYaw=true },
            new P { era=Era.선아, asset="Lampstand",     label="등잔대", ang=146, rad=2.45f, yawJitter=7 },
            new P { era=Era.선아, asset="Lamp",          label="등잔",   ang=168, rad=2.70f, randomYaw=true },
            new P { era=Era.선아, asset="Wooden_Tray",   label="목판",   ang=157, rad=3.35f, yawJitter=-25 },
            new P { era=Era.선아, asset="Bowl01a",       label="그릇",   ang=163, rad=3.50f, randomYaw=true },
            new P { era=Era.선아, asset="Cup",           label="잔",     ang=150, rad=3.05f, randomYaw=true },
            new P { era=Era.선아, asset="Bottle",        label="병",     ang=150, rad=3.62f, randomYaw=true },
            new P { era=Era.선아, asset="Water_Jar",     label="물동이", ang=134, rad=3.50f, yawJitter=18 },
            new P { era=Era.선아, asset="Basin01a",      label="대야",   ang=132, rad=3.70f, randomYaw=true },
            new P { era=Era.선아, asset="Bamboo_Basket", label="바구니", ang=200, rad=4.00f, randomYaw=true },

            // ── 낙석 주변 — 무너진 계단 어귀, 넘어지고 엎어진 것 ─────────
            new P { era=Era.낙석, asset="Jar01c",       label="넘어진항아리", ang=28,  rad=5.00f, roll=88, randomYaw=true },
            new P { era=Era.낙석, asset="Basket01a",    label="엎어진소쿠리", ang=38,  rad=5.15f, roll=172, randomYaw=true },
            new P { era=Era.낙석, asset="Plate",        label="깨진접시",     ang=22,  rad=4.70f, roll=24, randomYaw=true },
            new P { era=Era.낙석, asset="Straw_Jar01a", label="쓰러진짚독",   ang=32,  rad=5.15f, pitch=64, randomYaw=true },
        };

        [MenuItem("Tools/이문록/관측실 소품 배치")]
        public static void Build()
        {
            if (SceneManager.GetActiveScene().name != "Gyeonu_Observatory")
            { Debug.LogError("[관측실 소품] 활성 씬이 Gyeonu_Observatory가 아닙니다"); return; }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            EnsureFolder(MatDir);

            var root = new GameObject(RootName);
            var groups = new Dictionary<Era, Transform>();
            foreach (Era e in System.Enum.GetValues(typeof(Era)))
            {
                var g = new GameObject(e == Era.낙석 ? "낙석주변" : $"시대{(int)e + 1}_{e}");
                g.transform.SetParent(root.transform, false);
                groups[e] = g.transform;
            }

            var rnd = new System.Random(70707);
            int placed = 0, missing = 0, solids = 0;
            var missingList = new List<string>();
            var spawned = new List<(GameObject go, P p)>();

            foreach (var p in Props)
            {
                var prefab = FindProp(p.asset);
                if (prefab == null) { missing++; missingList.Add(p.asset); continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = $"{p.era}_{p.label}";
                go.transform.SetParent(groups[p.era], false);

                float rad = p.ang * Mathf.Deg2Rad;
                var pos = C + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * p.rad;
                float yaw = p.randomYaw ? (float)rnd.NextDouble() * 360f
                                        : p.ang + 180f + p.yawJitter + ((float)rnd.NextDouble() - 0.5f) * 6f;
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(p.pitch, yaw, p.roll));

                // 접지 — 프리팹 피벗이 제각각이라 회전 후 실제 바운드로 바닥에 앉힌다
                if (p.lift > 0f) go.transform.position = new Vector3(pos.x, FloorY + p.lift, pos.z);
                else
                {
                    var b = WorldBounds(go);
                    if (b.HasValue) go.transform.position += Vector3.up * (FloorY - b.Value.min.y);
                }

                Dust(go, p.era);   // 선아 것도 같은 셰이더로 (차이가 색조로만 읽히게)
                spawned.Add((go, p));
                placed++;
            }

            // 손으로 잡은 좌표는 시작점일 뿐 — 겹침·통행로·기물·벽에서 자동으로 밀어낸다.
            // "어수선함"은 유지하면서 낄 자리만 없앤다
            int moved = Relax(spawned);

            // 콜라이더는 이완이 끝난 뒤에 (팩 것 전부 제거 후, 큰 물건만 박스로)
            foreach (var (go, p) in spawned)
            {
                foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                if (p.solid && AddBoxCollider(go)) solids++;
            }

            AttachLampLights(groups);
            AssetDatabase.SaveAssets();

            int tris = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;

            int warn = Validate(root);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[관측실 소품] 배치 완료 — {placed}개 ({tris:n0}tri), 박스 콜라이더 {solids}, " +
                      $"자동 이완 {moved}개, 침범 경고 {warn}" +
                      (missing > 0 ? $"\n찾지 못한 에셋 {missing}: {string.Join(", ", missingList)}" : ""));
        }

        /// <summary>등잔에 실제 불빛을 단다 — 선아 것만 켜 두고(가장 최근 흔적), 아버지 것은 꺼 둔다.
        /// 별밤 연출에서 함께 꺼지도록 HonsangController가 이 그룹도 훑는다.</summary>
        static void AttachLampLights(Dictionary<Era, Transform> groups)
        {
            foreach (var era in new[] { Era.선아, Era.아버지 })
                foreach (Transform t in groups[era])
                {
                    if (!t.name.Contains("등잔")) continue;
                    var b = WorldBounds(t.gameObject);
                    var go = new GameObject("불빛");
                    go.transform.SetParent(t, true);
                    go.transform.position = b.HasValue
                        ? new Vector3(b.Value.center.x, b.Value.max.y + 0.06f, b.Value.center.z)
                        : t.position + Vector3.up * 0.75f;
                    var l = go.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.70f, 0.40f);
                    l.intensity = era == Era.선아 ? 2.2f : 0f;   // 아버지 등잔은 꺼진 채
                    l.range = era == Era.선아 ? 6.5f : 3f;
                    l.enabled = era == Era.선아;
                }
        }

        /// <summary>시대 색조 — 낙안읍성 팩 재질은 커스텀 셰이더(Unreal/PBR_Shaders)라 색 속성이 없다.
        /// 원본은 그대로 두고, 같은 텍스처를 물린 URP Lit 복제본을 우리 폴더에 만들어 색조를 준다.
        /// 슬롯 규약: 0=노멀 / 1=베이스컬러 / 2=메탈릭 / 3=러프니스 / 4=AO (팩 전역 동일, 실측 확인).
        /// 세 시대 모두 같은 셰이더를 쓰게 해야 차이가 "색조"로만 읽힌다.</summary>
        static void Dust(GameObject go, Era era)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i] = EraMat(mats[i], era);
                r.sharedMaterials = mats;
            }
        }

        static Material EraMat(Material src, Era era)
        {
            // 텍스처 곱셈 틴트라 진짜 채도 감소는 못 한다 — 어둡게 + 중성 회색 쪽으로 눌러 먼지를 낸다
            Color tint; string tag; float smooth;
            switch (era)
            {
                case Era.직녀: tint = new Color(0.50f, 0.49f, 0.46f); tag = "먼지"; smooth = 0.06f; break;
                case Era.아버지: tint = new Color(0.82f, 0.81f, 0.79f); tag = "세월"; smooth = 0.14f; break;
                case Era.낙석: tint = new Color(0.58f, 0.57f, 0.55f); tag = "낙석"; smooth = 0.08f; break;
                default: tint = Color.white; tag = "본래"; smooth = 0.20f; break;
            }
            string path = $"{MatDir}/{src.name}_{tag}.mat";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(lit) { name = src.name + "_" + tag };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.shader != lit) m.shader = lit;

            Texture Slot(int i) => src.HasProperty("Material_Texture2D_" + i) ? src.GetTexture("Material_Texture2D_" + i) : null;
            var bc = Slot(1) ?? (src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null);
            var nm = Slot(0);
            var ao = Slot(4);

            m.SetTexture("_BaseMap", bc);
            m.SetTexture("_BumpMap", nm);
            if (nm != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", ao);
            if (ao != null) { m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_OcclusionStrength", 1f); }
            else m.DisableKeyword("_OCCLUSIONMAP");
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>렌더러 월드 AABB를 로컬로 되돌려 박스 콜라이더 하나. 발끝이 걸리지 않게 XZ를 살짝 줄인다.</summary>
        static bool AddBoxCollider(GameObject go)
        {
            var b = WorldBounds(go);
            if (!b.HasValue) return false;
            var bc = go.AddComponent<BoxCollider>();
            var localCenter = go.transform.InverseTransformPoint(b.Value.center);
            var ls = go.transform.lossyScale;
            var size = b.Value.size;
            // 회전이 있으면 월드 AABB가 부풀지만, 소품은 대부분 축정렬이라 그대로 쓴다
            bc.center = localCenter;
            bc.size = new Vector3(
                Mathf.Max(0.05f, size.x * 0.88f / Mathf.Max(0.0001f, Mathf.Abs(ls.x))),
                Mathf.Max(0.05f, size.y / Mathf.Max(0.0001f, Mathf.Abs(ls.y))),
                Mathf.Max(0.05f, size.z * 0.88f / Mathf.Max(0.0001f, Mathf.Abs(ls.z))));
            return true;
        }

        static readonly (string name, Vector2 c, float r)[] KeepOut =
        {
            ("혼상(중앙)", new Vector2(18.2f, 34.6f), 2.00f),
            ("혼천의", new Vector2(21.6f, 34.6f), 1.25f),
            ("등잔대_남서", new Vector2(14.48f, 32.45f), 0.45f),
            ("등잔대_북서", new Vector2(14.48f, 36.75f), 0.45f),
            ("등잔대_북동", new Vector2(21.92f, 36.75f), 0.45f),
            ("등잔대_남동", new Vector2(21.92f, 32.45f), 0.45f),
        };
        // 통행로 (x최소, x최대, z최소, z최대)
        static readonly Vector4[] Lanes =
        {
            new Vector4(17.30f, 19.10f, 28.60f, 32.50f),   // 암문 입구
            new Vector4(17.25f, 19.15f, 38.00f, 41.00f),   // 계단 어귀
        };

        /// <summary>배치 이완 — 서로 겹치거나 통행로·기물·벽을 침범한 것만 XZ로 밀어낸다.
        /// 손으로 준 좌표의 "어수선함"은 유지되고, 낄 자리만 사라진다.</summary>
        static int Relax(List<(GameObject go, P p)> items)
        {
            // 벽 걸이·가구 위에 얹은 것은 이완에서 뺀다 — 밀어내면 받침에서 떨어진다
            var live = new List<(GameObject go, P p)>();
            foreach (var it in items) if (!it.p.onWall && it.p.lift <= 0f) live.Add(it);

            var start = new Vector3[live.Count];
            for (int i = 0; i < live.Count; i++) start[i] = live[i].go.transform.position;

            for (int iter = 0; iter < 40; iter++)
            {
                var box = new Bounds[live.Count];
                var rad = new float[live.Count];
                var flat = new bool[live.Count];
                for (int i = 0; i < live.Count; i++)
                {
                    var b = WorldBounds(live[i].go) ?? new Bounds(live[i].go.transform.position, Vector3.one * 0.1f);
                    box[i] = b;
                    rad[i] = Mathf.Max(b.extents.x, b.extents.z);
                    flat[i] = b.size.y < 0.20f;   // 자리·목판·접시는 밟고 지나간다
                }
                var push = new Vector2[live.Count];

                for (int i = 0; i < live.Count; i++)
                {
                    if (flat[i]) continue;
                    var ci = new Vector2(box[i].center.x, box[i].center.z);

                    for (int j = i + 1; j < live.Count; j++)
                    {
                        if (flat[j]) continue;
                        var cj = new Vector2(box[j].center.x, box[j].center.z);
                        float min = rad[i] + rad[j] + 0.05f;
                        var d = cj - ci;
                        float dist = Mathf.Max(0.01f, d.magnitude);
                        if (dist >= min) continue;
                        var dir = d / dist;
                        float half = (min - dist) * 0.5f;
                        push[i] -= dir * half; push[j] += dir * half;
                    }
                    foreach (var k in KeepOut)
                    {
                        float min = k.r + rad[i];
                        var d = ci - k.c;
                        float dist = Mathf.Max(0.01f, d.magnitude);
                        if (dist < min) push[i] += (d / dist) * (min - dist);
                    }
                    foreach (var ln in Lanes)   // 통행로 밖으로 (가까운 쪽 옆으로)
                    {
                        if (box[i].max.x <= ln.x || box[i].min.x >= ln.y ||
                            box[i].max.z <= ln.z || box[i].min.z >= ln.w) continue;
                        float outL = box[i].max.x - ln.x + 0.05f;   // 왼(-x)으로 빠지기
                        float outR = ln.y - box[i].min.x + 0.05f;   // 오른(+x)으로 빠지기
                        push[i].x += outL < outR ? -outL : outR;
                    }
                    // 벽 — 반지름 방향 지지폭으로 판정 (접선으로 긴 물건이 오판되지 않게)
                    var toC = ci - new Vector2(C.x, C.z);
                    float dc = Mathf.Max(0.01f, toC.magnitude);
                    var rd = toC / dc;
                    float support = Mathf.Abs(box[i].extents.x * rd.x) + Mathf.Abs(box[i].extents.z * rd.y);
                    if (dc + support > WallR - 0.04f) push[i] -= rd * (dc + support - (WallR - 0.04f));
                }

                bool any = false;
                for (int i = 0; i < live.Count; i++)
                {
                    if (push[i].sqrMagnitude < 1e-6f) continue;
                    var t = live[i].go.transform;
                    t.position += new Vector3(push[i].x, 0f, push[i].y);
                    any = true;
                }
                if (!any) break;
            }

            int moved = 0;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i].go.transform;
                if (Vector3.Distance(start[i], t.position) > 0.02f) moved++;
                if (live[i].p.lift > 0f) continue;
                var b = WorldBounds(live[i].go);        // 이완 뒤 다시 접지
                if (b.HasValue) t.position += Vector3.up * (FloorY - b.Value.min.y);
            }
            return moved;
        }

        /// <summary>배치 검증 — 통행로·혼상·혼천의·등잔대·벽 침범을 잡아 로그로 알린다.</summary>
        static int Validate(GameObject root)
        {
            int warn = 0;
            foreach (Transform grp in root.transform)
                foreach (Transform t in grp)
                {
                    var b = WorldBounds(t.gameObject);
                    if (!b.HasValue) continue;
                    var bb = b.Value;
                    bool onWall = bb.min.y > FloorY + 0.9f;          // 벽 걸이
                    bool flat = bb.size.y < 0.20f;                   // 밟고 지나갈 수 있는 것
                    var p2 = new Vector2(bb.center.x, bb.center.z);
                    float rad = Mathf.Max(bb.extents.x, bb.extents.z);

                    if (!flat && !onWall)
                    {
                        foreach (var k in KeepOut)
                            if (Vector2.Distance(p2, k.c) < k.r + rad - 0.02f)
                            { Debug.LogWarning($"[관측실 소품] {t.name} 이(가) {k.name} 영역을 침범"); warn++; }
                        foreach (var ln in Lanes)
                            if (bb.max.x > ln.x && bb.min.x < ln.y && bb.max.z > ln.z && bb.min.z < ln.w)
                            { Debug.LogWarning($"[관측실 소품] {t.name} 이(가) 통행로를 막음 ({ln.x:F1}~{ln.y:F1} / {ln.z:F1}~{ln.w:F1})"); warn++; }
                    }
                    if (!onWall)
                    {
                        var toC = p2 - new Vector2(C.x, C.z);
                        float dc = Mathf.Max(0.01f, toC.magnitude);
                        var rd = toC / dc;
                        float support = Mathf.Abs(bb.extents.x * rd.x) + Mathf.Abs(bb.extents.z * rd.y);
                        if (dc + support > WallR + 0.06f)
                        { Debug.LogWarning($"[관측실 소품] {t.name} 이(가) 석벽을 파고듦 ({dc + support:F2} > {WallR:F2})"); warn++; }
                    }
                }
            return warn;
        }

        // ── 헬퍼 ────────────────────────────────────────────
        static GameObject FindProp(string name)
        {
            GameObject fbx = null;
            foreach (var g in AssetDatabase.FindAssets(name))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var bn = System.IO.Path.GetFileNameWithoutExtension(p);
                if (bn != name && bn != "SM_" + name) continue;
                if (p.EndsWith(".prefab")) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (p.EndsWith(".fbx") && fbx == null) fbx = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return fbx;
        }

        static Bounds? WorldBounds(GameObject go)
        {
            Bounds b = new Bounds();
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<MeshFilter>() == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? b : (Bounds?)null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
