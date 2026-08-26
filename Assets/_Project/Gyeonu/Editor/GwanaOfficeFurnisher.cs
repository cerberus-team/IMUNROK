using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Gyeonu;
using static IMUNROK.Gyeonu.Editor.GwanaOfficeLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 집무실 소품 배치. 멱등 — 루트 `집무실_소품`을 지우고 다시 만든다.
    ///
    /// ⚠️ **여기는 관청이다.** 관측실·서고와 달리 정돈돼 있어야 한다 (사용자 지시):
    ///    가구는 벽에 등을 붙여 좌-중-우 3단 대칭, 중앙은 서안 하나로 절제하고 바닥을 비운다.
    ///    다만 수령이 실무를 보는 방이니 장부·서책은 쌓여 있어도 된다.
    ///
    /// 배치는 전부 **바운즈 실측 스냅**이다 (`Place`) — 팩마다 피벗 규약이 다르기 때문이다.
    ///    운현궁/kcisa = 밑면 피벗, Joseon Library(사정전 책상·문갑) = **중심 피벗**.
    ///    좌표로 y를 직접 주면 절반이 바닥에 묻히거나 뜬다. 벽 붙임도 깊이를 짐작하지 않고
    ///    회전 후 바운즈로 밀어붙인다.
    /// </summary>
    public static class GwanaOfficeFurnisher
    {
        const string Root = "집무실_소품";
        const string UH = "Assets/UnhyeongungCollect/Prefabs/";
        const string KT = "Assets/KTinteractiveProp/Volum 02/Prefabs/";
        const string JL = "Assets/Joseon Library/";

        const string ScreenFbx = JL + "gyeongbokgung-cheonchujeon-foldingscreena/source/SM_CCJ_FoldingScreenA.fbx";

        /// <summary>병풍 앞면이 방을 보게 하는 보정. 원본 FBX의 앞뒤가 뒤집혀 있으면 180으로 바꾼다.</summary>
        const float ScreenYawOffset = 0f;

        const string UnDemo = "Assets/UnhyeongungCollect/Scenes/Demo.unity";

        static Transform _root, _propRoot, _colRoot;
        static readonly List<string> _missing = new List<string>();
        static readonly Dictionary<string, GameObject> _composites = new Dictionary<string, GameObject>();
        /// <summary>재생성 전에 떠 두는 현재 씬 트랜스폼 (이름 → 월드 포즈).</summary>
        static readonly Dictionary<string, Pose> _keep = new Dictionary<string, Pose>();

        static void Snapshot(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root || t.parent == null) continue;
                // 소품 본체(기물/… 의 직계)와 병풍 루트만 — 프리팹 내부 자식까지 뜨면 이름이 겹친다
                bool isProp = t.parent.name == "기물" || t.parent == root;
                if (isProp) _keep[t.name] = new Pose(t.position, t.rotation);
            }
        }

        static int RestoreSnapshot()
        {
            if (_keep.Count == 0) return 0;
            int n = 0;
            foreach (var t in _root.GetComponentsInChildren<Transform>(true))
            {
                if (t == _root || t.parent == null) continue;
                if (!(t.parent.name == "기물" || t.parent == _root)) continue;
                Pose p;
                if (!_keep.TryGetValue(t.name, out p)) continue;
                t.SetPositionAndRotation(p.position, p.rotation);
                n++;
            }
            return n;
        }

        /// <summary>
        /// 소품 배치. **현재 씬의 위치·회전이 설계값보다 우선한다** (사용자 지시 2026-08-16) —
        /// 손으로 맞춰 둔 가구가 재생성으로 초기화되지 않게 이름별로 스냅샷을 떠서 되돌려 놓는다.
        /// 설계 위치로 되돌리려면 「소품 배치 (설계 위치로 초기화)」를 쓸 것.
        /// </summary>
        [MenuItem("Tools/이문록/관아 집무실 ▸ ② 소품 배치")]
        public static void Furnish() => Furnish(true);

        [MenuItem("Tools/이문록/관아 집무실 ▸ ② 소품 배치 (설계 위치로 초기화)")]
        public static void FurnishReset()
        {
            if (!EditorUtility.DisplayDialog("설계 위치로 초기화",
                "손으로 조정한 가구 위치·회전이 모두 설계값으로 되돌아갑니다. 계속할까요?",
                "초기화", "취소")) return;
            Furnish(false);
        }

        public static void Furnish(bool preserveHandPlacement)
        {
            if (SceneManager.GetActiveScene().name != "Gyeonu_GwanaOffice")
            { Debug.LogError("[집무실] Gyeonu_GwanaOffice 씬에서 실행하세요"); return; }

            // ⚠️ 절차 소품(붓통·붓)이 쓰는 빌더 재질 static은 도메인 리로드에서 비워진다.
            //    이걸 빼먹으면 붓통·붓이 재질 null → 마젠타로 뜬다 (실측으로 잡은 버그).
            GwanaOfficeBuilder.EnsureMaterials();

            _missing.Clear();
            _keep.Clear();
            _blocks.Clear();
            var old = GameObject.Find(Root);
            if (old != null)
            {
                if (preserveHandPlacement) Snapshot(old.transform);
                Object.DestroyImmediate(old);
            }
            _root = new GameObject(Root).transform;
            _propRoot = new GameObject("기물").transform; _propRoot.SetParent(_root, false);
            _colRoot = new GameObject("가구_차단").transform; _colRoot.SetParent(_root, false);

            LoadComposites();
            BuildScreen();
            BuildCenter();
            BuildWestWall();
            BuildEastWall();
            // 스테이징에 남은 미사용 복제본 정리
            foreach (var kv in _composites)
                if (kv.Value != null && !kv.Value.activeSelf) Object.DestroyImmediate(kv.Value);
            _composites.Clear();

            int restored = RestoreSnapshot();
            BuildBlockers();   // ⚠️ 반드시 복원 뒤에 — 콜라이더가 최종 위치를 따라가야 한다

            if (_missing.Count > 0)
                Debug.LogWarning("[집무실] 없는 에셋 " + _missing.Count + "건:\n  " + string.Join("\n  ", _missing));
            if (preserveHandPlacement)
                Debug.Log("[집무실] 손 조정 보존 — " + restored + "개를 현재 씬 트랜스폼으로 되돌림"
                          + (restored == 0 ? " (스냅샷 없음: 첫 배치)" : ""));

            int tri = _root.GetComponentsInChildren<MeshFilter>()
                           .Where(m => m.sharedMesh != null).Sum(m => m.sharedMesh.triangles.Length / 3);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[집무실] ② 소품 배치 — 기물 " + _propRoot.childCount +
                      " / 가구 차단 " + _colRoot.childCount + " / " + tri.ToString("N0") + " tri");
        }

        // ══════════════════════════════════════════════════════
        // 병풍 — 뒷벽을 가로지르고, 클릭하면 접혀서 서쪽으로 치워진다
        // ══════════════════════════════════════════════════════
        static void BuildScreen()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenFbx);
            if (src == null) { _missing.Add(ScreenFbx); return; }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "병풍";
            root.transform.SetParent(_root, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Strip(root);

            var panes = root.GetComponentsInChildren<MeshFilter>()
                            .Where(m => m.sharedMesh != null).ToArray();
            if (panes.Length < 2) { _missing.Add("병풍 폭 메시"); return; }

            // ── ① 폭마다 수평 주축(폭 방향)·폭 너비·중심을 실측 ──
            var ctr = new Vector3[panes.Length];
            var axis = new Vector2[panes.Length];
            var width = new float[panes.Length];
            for (int i = 0; i < panes.Length; i++)
            {
                PanelFrame(panes[i], out ctr[i], out axis[i], out width[i]);
            }

            // ── ② 병풍이 뻗는 방향을 +X로 맞춘다 (루트 회전을 자식에 구워 넣는다) ──
            // 루트 회전을 남겨 두면 이후 접힘 계산이 두 좌표계를 오가야 해서 굽는 편이 단순하다.
            Vector3 run = ctr[panes.Length - 1] - ctr[0];
            float runYaw = Mathf.Atan2(run.z, run.x) * Mathf.Rad2Deg;
            var bake = Quaternion.Euler(0f, runYaw + ScreenYawOffset, 0f);
            foreach (var t in panes.Select(p => p.transform))
            {
                t.localPosition = bake * t.localPosition;
                t.localRotation = bake * t.localRotation;
            }
            for (int i = 0; i < panes.Length; i++) PanelFrame(panes[i], out ctr[i], out axis[i], out width[i]);

            // ── ③ 루트를 방 뒷벽에 앉힌다 (바닥 접지 + x 중앙 + 앞면을 ScreenZ에) ──
            Bounds lb = LocalBounds(panes);
            root.transform.position = new Vector3(-lb.center.x, FloorY - lb.min.y, ScreenZ - lb.center.z);

            // ── ④ 서→동 순서로 정렬한 뒤 접힘 포즈를 경첩 사슬로 계산 ──
            var order = Enumerable.Range(0, panes.Length).OrderBy(i => ctr[i].x).ToArray();
            const float alpha = 74f;                       // 접었을 때 폭이 진행축과 이루는 각
            float ca = Mathf.Cos(alpha * Mathf.Deg2Rad), sa = Mathf.Sin(alpha * Mathf.Deg2Rad);
            Vector2 e = new Vector2(lb.min.x, lb.center.z - 0.34f);   // 서쪽 끝에서 시작, 방 안쪽으로 물려
            float sgn = 1f;

            var list = new List<FoldingScreen.Panel>();
            foreach (int i in order)
            {
                var t = panes[i].transform;
                Vector2 u = new Vector2(ca, sgn * sa);
                Vector2 tc = e + u * (width[i] * 0.5f);

                // 폭의 주축을 u 방향으로 돌리는 데 필요한 Y 회전
                float cur = Mathf.Atan2(axis[i].y, axis[i].x) * Mathf.Rad2Deg;
                float tgt = Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg;
                var dq = Quaternion.Euler(0f, cur - tgt, 0f);
                var newRot = dq * t.localRotation;
                // 메시 로컬 중심을 목표 중심에 맞춘다 (피벗이 폭 중심이 아니므로 역산)
                Vector3 mc = panes[i].sharedMesh.bounds.center;
                Vector3 target = new Vector3(tc.x, ctr[i].y, tc.y);
                Vector3 newPos = target - newRot * mc;

                list.Add(new FoldingScreen.Panel
                {
                    target = t,
                    spreadPos = t.localPosition,
                    spreadEuler = t.localRotation.eulerAngles,
                    foldedPos = newPos,
                    foldedEuler = newRot.eulerAngles,
                });

                e += u * width[i];
                sgn = -sgn;
            }

            // ── ⑤ 상호작용 + 차단 박스 (루트에 하나만 — 폭마다 달면 접히는 동안 캡슐을 문다) ──
            var col = root.AddComponent<BoxCollider>();
            var fs = root.AddComponent<FoldingScreen>();
            fs.displayName = "병풍";
            fs.panels = list.ToArray();
            fs.blocker = col;
            fs.spreadBoxCenter = new Vector3(lb.center.x, lb.size.y * 0.5f, lb.center.z);
            fs.spreadBoxSize = new Vector3(lb.size.x, lb.size.y, Mathf.Max(0.28f, lb.size.z));
            float foldRun = Mathf.Abs(e.x - lb.min.x);
            fs.foldedBoxCenter = new Vector3(lb.min.x + foldRun * 0.5f, lb.size.y * 0.5f, lb.center.z - 0.34f);
            fs.foldedBoxSize = new Vector3(foldRun + 0.12f, lb.size.y, width.Max() * sa * 2f + 0.12f);
            col.center = fs.spreadBoxCenter;
            col.size = fs.spreadBoxSize;

            Debug.Log("[집무실] 병풍 " + panes.Length + "폭 — 펼침 폭 " + lb.size.x.ToString("F2") +
                      "m / 접힘 폭 " + foldRun.ToString("F2") + "m, 뒷벽 z=" + ScreenZ +
                      " (비밀문 x±" + SecretHalfX + " 은 접으면 드러난다)");
        }

        /// <summary>폭 하나의 (부모로컬 중심, 수평 주축, 폭 너비) — 얇은 직사각형이라 2×2 공분산의
        /// 지배 고유벡터가 곧 폭 방향이다. 바운즈 크기만으로는 **부호**를 알 수 없어 정점을 쓴다.</summary>
        static void PanelFrame(MeshFilter mf, out Vector3 center, out Vector2 axis, out float width)
        {
            var t = mf.transform;
            var verts = mf.sharedMesh.vertices;
            center = t.localPosition + t.localRotation * mf.sharedMesh.bounds.center;

            double sxx = 0, sxz = 0, szz = 0;
            var pts = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 w = t.localPosition + t.localRotation * verts[i];
                pts[i] = new Vector2(w.x - center.x, w.z - center.z);
                sxx += pts[i].x * pts[i].x; sxz += pts[i].x * pts[i].y; szz += pts[i].y * pts[i].y;
            }
            // 2×2 대칭행렬의 큰 고유값에 대응하는 고유벡터 (닫힌 형태)
            double tr = sxx + szz, det = sxx * szz - sxz * sxz;
            double lam = tr * 0.5 + System.Math.Sqrt(System.Math.Max(0.0, tr * tr * 0.25 - det));
            axis = System.Math.Abs(sxz) > 1e-9
                 ? new Vector2((float)(lam - szz), (float)sxz).normalized
                 : (sxx >= szz ? new Vector2(1f, 0f) : new Vector2(0f, 1f));

            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var p in pts) { float d = Vector2.Dot(p, axis); if (d < lo) lo = d; if (d > hi) hi = d; }
            width = hi - lo;
            // 부호를 +X 쪽으로 통일 (사슬 계산이 방향에 의존한다)
            if (axis.x < 0f) axis = -axis;
        }

        static Bounds LocalBounds(MeshFilter[] panes)
        {
            Bounds b = new Bounds(); bool first = true;
            foreach (var mf in panes)
            {
                var t = mf.transform;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3(
                        (i & 1) == 0 ? mb.min.x : mb.max.x,
                        (i & 2) == 0 ? mb.min.y : mb.max.y,
                        (i & 4) == 0 ? mb.min.z : mb.max.z);
                    var p = t.localPosition + t.localRotation * c;
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        // ══════════════════════════════════════════════════════
        // 중앙 — 돗자리 · 서안 · 방석 · 향로 · 문방
        // ══════════════════════════════════════════════════════
        static void BuildCenter()
        {
            Place(UH + "SM_StrawMat372.prefab", "돗자리", new Vector2(0f, 0.60f), 0f, FloorY);

            var desk = Place(JL + "gyeongbokgung-sajeongjeon-tableb/source/SM_SJJ_TableB.fbx",
                             "서안", new Vector2(0f, 0.78f), 0f, FloorY);
            float deskTop = desk != null ? WorldBounds(desk).max.y : 0.69f;
            Block(desk, 0.06f);

            // 방석 — 수령 자리(병풍 앞) + 손님 자리
            Place(UH + "SM_Mat370.prefab", "방석_수령", new Vector2(0f, 1.40f), 12f, FloorY);
            Place(UH + "SM_ArmrestCushion.prefab", "안석", new Vector2(0.52f, 1.46f), -18f, FloorY);
            Place(UH + "SM_FloorCusion.prefab", "방석_손님", new Vector2(-0.10f, -0.42f), 6f, FloorY);
            Place(UH + "SM_Mat370.prefab", "방석_곁", new Vector2(-1.32f, 0.24f), -22f, FloorY);

            // 향로 — 병풍 앞 바닥
            Place(UH + "SM_Brazier.prefab", "향로", new Vector2(-1.28f, 1.76f), 0f, FloorY);

            // 서안 위 문방 — 붓통(절차 생성) · 펼친 책 · 서책 · 벼루 대용 백자
            BrushPot(new Vector3(-0.44f, deskTop, 0.88f));

            // 등잔 — 밤의 유일한 실내 광원 자리. 낮에도 놓여 있다(불만 꺼져 있을 뿐)
            var oil = Place(KT + "Lantern.prefab", "등잔",
                            new Vector2(GwanaOfficeBuilder.LampSpot.x, GwanaOfficeBuilder.LampSpot.z),
                            -24f, deskTop);
            if (oil == null)
                Place(UH + "SM_Candlestick128.prefab", "등잔",
                      new Vector2(GwanaOfficeBuilder.LampSpot.x, GwanaOfficeBuilder.LampSpot.z),
                      0f, deskTop);
            Place(JL + "gyeongbokgung-cheonchujeon-openbook/source/SM_CCJ_OpenBook.fbx",
                  "펼친책", new Vector2(0.02f, 0.76f), 4f, deskTop);
            Place(UH + "SM_WhitePorcelainDish203.prefab", "연적접시", new Vector2(-0.24f, 0.63f), 0f, deskTop);
            BookStack(new Vector2(0.42f, 0.88f), deskTop, 3, 8f, "서안_서책");

            // 바닥에 쌓인 장부 (수령이 실무를 보는 방 — 여기만 흐트러져도 된다)
            BookStack(new Vector2(1.24f, 0.52f), FloorY, 5, -12f, "장부더미_동");
            BookStack(new Vector2(1.10f, 0.02f), FloorY, 3, 24f, "장부더미_동2");
            BookStack(new Vector2(-1.06f, 1.06f), FloorY, 4, 16f, "장부더미_서");
        }

        // ══════════════════════════════════════════════════════
        // 운현궁 조립 가구 — 데모 씬의 조립본을 복제해 온다
        // ⚠️ 몸체·문짝·서랍이 별 프리팹이고 피벗이 제각각이라 직접 조립하면 문짝이 어긋난다
        //    (관측실에서 확인된 결론). 데모 씬은 **저장하지 않고** 닫는다 — 프로젝트 절대 규칙.
        // ⚠️ 단품 프리팹 SM_Cabinet237/238·SM_Shelves241 은 쓰지 않는다: 문짝 없는 **개가식 선반틀**
        //    이라 관청 집무실 벽에 세우면 현대 앵글 선반처럼 읽힌다 (1차 배치에서 실측).
        // ══════════════════════════════════════════════════════
        static void LoadComposites()
        {
            _composites.Clear();
            var target = SceneManager.GetActiveScene();
            var demo = EditorSceneManager.OpenScene(UnDemo, OpenSceneMode.Additive);
            try
            {
                Transform model = null;
                foreach (var g in demo.GetRootGameObjects()) if (g.name == "Model") model = g.transform;
                // ⚠️ 후보 실사 심사 결과 (2026-08-16): `SM_BookCabinet224`는 **붉은 옻칠 4층장**이라
                //    회벽 방에서 새빨간 덩어리로 튄다(운현궁=왕실 거처라 화려하다). `SM_Cabinet243`은
                //    장이 아니라 **작은 받침 + 층층 선반**이라 벽 가구로 안 읽힌다. 둘 다 기각.
                foreach (var nm in new[] { "SM_Board229", "SM_Cupboard226", "SM_ThreetieredCupboard" })
                {
                    var src = model != null ? model.Find(nm) : null;
                    if (src == null) { _missing.Add("운현궁 데모 조립본 " + nm); continue; }
                    var copy = Object.Instantiate(src.gameObject);
                    SceneManager.MoveGameObjectToScene(copy, target);
                    copy.name = nm;
                    copy.SetActive(false);          // 스테이징 — Snap에서 활성화
                    _composites[nm] = copy;
                }
            }
            finally { EditorSceneManager.CloseScene(demo, true); }   // 저장 없이 닫는다
        }

        static GameObject Composite(string name, string label, Vector2 xz, float yaw, char wall)
        {
            GameObject go;
            if (!_composites.TryGetValue(name, out go) || go == null) { _missing.Add("조립본 " + name); return null; }
            go.SetActive(true);
            go.name = label;
            go.transform.SetParent(_propRoot, true);
            _composites[name] = null;
            Strip(go);
            Snap(go, xz, yaw, FloorY, wall, 0.05f);
            return go;
        }

        // ══════════════════════════════════════════════════════
        // 왼쪽(서) 벽 — 이층 서랍장 · 낮은 서안형 가구 · 반닫이
        // ══════════════════════════════════════════════════════
        static void BuildWestWall()
        {
            Block(Composite("SM_Board229", "이층서랍장", new Vector2(0f, -1.55f), 90f, 'W'), 0.04f);

            // 격자창 아래 낮은 서안형 가구 + 그 위 소품
            var low = Place(UH + "SM_Table252.prefab", "서안형_낮은장", new Vector2(0f, 0.02f), 90f, FloorY, 'W');
            Block(low, 0.04f);
            if (low != null)
            {
                float top = WorldBounds(low).max.y;
                Place(UH + "SM_CeladonglazedSmall.prefab", "청자접시", new Vector2(-2.62f, -0.14f), 0f, top);
                BookStack(new Vector2(-2.62f, 0.16f), top, 3, -8f, "낮은장_서책");
            }

            // 반닫이 — 2단 장식 손잡이 궤.
            // ⚠️ **270°는 사용자가 손으로 맞춘 방향이다** (2026-08-16 씬에서 실측해 상수에 반영).
            //    빌더 초기값은 90°였다. 임의로 되돌리지 말 것.
            var chest = Place(KT + "HalfChest 01.prefab", "반닫이", new Vector2(0f, 1.46f), 270f, FloorY, 'W');
            Block(chest, 0.04f);
            if (chest != null)
            {
                float top = WorldBounds(chest).max.y;
                Place(UH + "SM_BookCase305.prefab", "책궤", new Vector2(-2.52f, 1.66f), 90f, top);
                Place(UH + "SM_Candlestick128.prefab", "촛대", new Vector2(-2.52f, 1.12f), 0f, top);
                BookStack(new Vector2(-2.50f, 1.40f), top, 2, 14f, "반닫이_서책");
            }
        }

        // ══════════════════════════════════════════════════════
        // 오른쪽(동) 벽 — 문갑 · 백자 화병 · 발 · 사방탁자
        // ══════════════════════════════════════════════════════
        static void BuildEastWall()
        {
            var mungap = Place(JL + "kcdf-mungap-04/source/Table04_Key.fbx", "문갑",
                               new Vector2(0f, 0.10f), -90f, FloorY, 'E');
            Block(mungap, 0.04f);
            if (mungap != null)
            {
                float top = WorldBounds(mungap).max.y;
                // 백자 화병 — 흰 꽃은 어느 팩에도 없다 (사용자 보고 항목)
                Place(UH + "SM_WhitePorcelainBottle.prefab", "백자_화병", new Vector2(2.60f, 0.54f), 0f, top);
                Place(UH + "SM_WhitePorcelainDish203.prefab", "백자_접시", new Vector2(2.58f, -0.20f), 0f, top);
                Place(UH + "SM_CeladonglazedSmall.prefab", "청자_소접", new Vector2(2.62f, -0.35f), 0f, top);
                BookStack(new Vector2(2.58f, 0.20f), top, 2, -10f, "문갑_서책");
            }

            // 발 (대나무 발) — 동창 위에 세 폭
            float winMid = (WinEastZ0 + WinEastZ1) * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                float z = winMid + (i - 1) * 0.58f;
                var b = Place(UH + "SM_BambooBlind.prefab", "발_" + (i + 1),
                              new Vector2(HalfX - 0.13f, z), 90f, float.NaN, '.', 0.05f, 1.30f);
                if (b != null) b.transform.position = new Vector3(HalfX - 0.13f, WinY1 - 0.03f, z);
            }

            // 장부·문서를 넣는 장 — 관청 집무실의 랜드마크 가구 (2.0m, 창방에 닿을 듯)
            Block(Composite("SM_Cupboard226", "문서장", new Vector2(0f, -1.45f), -90f, 'E'), 0.04f);
            // 북동 구석 삼층장
            Block(Composite("SM_ThreetieredCupboard", "삼층장", new Vector2(0f, 1.58f), -90f, 'E'), 0.04f);

            // 바닥에 쌓인 장부 — 실무를 보는 방이라 여기만 흐트러져도 된다
            BookStack(new Vector2(1.98f, -1.90f), FloorY, 6, 18f, "장부더미_남동");
            BookStack(new Vector2(1.62f, -2.00f), FloorY, 4, -14f, "장부더미_남동2");
        }

        // ══════════════════════════════════════════════════════
        // 절차 소품 — 붓통 (팩 어디에도 문방사우가 없어 직접 만든다)
        // ══════════════════════════════════════════════════════
        static void BrushPot(Vector3 baseCenter)
        {
            var pot = new GwanaMeshKit(0.35f);
            pot.Cylinder(baseCenter, 0.056f, 0.052f, 0.155f, 16);
            var go = GwanaOfficeBuilder.PieceGO(_propRoot, "붓통", pot, GwanaOfficeBuilder.MatMokjae);

            var hair = new GwanaMeshKit(0.25f);
            var shaft = new GwanaMeshKit(0.25f);
            var rnd = new System.Random(20260816);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + 0.4f;
                float lean = 6f + (float)rnd.NextDouble() * 9f;
                float len = 0.30f + (float)rnd.NextDouble() * 0.06f;
                Vector3 root = baseCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.028f + Vector3.up * 0.10f;
                var rot = Quaternion.Euler(lean * Mathf.Cos(a), 0f, -lean * Mathf.Sin(a));
                shaft.BoxRot(root + rot * (Vector3.up * len * 0.5f),
                             new Vector3(0.011f, len, 0.011f), rot);
                // 붓끝은 아래를 향해 꽂혀 있지 않고 위로 솟은 자루 끝 — 흰 붓털은 위쪽
                hair.BoxRot(root + rot * (Vector3.up * (len + 0.032f)),
                            new Vector3(0.016f, 0.064f, 0.016f), rot);
            }
            GwanaOfficeBuilder.PieceGO(_propRoot, "붓_자루", shaft, GwanaOfficeBuilder.MatMunmok);
            GwanaOfficeBuilder.PieceGO(_propRoot, "붓_털", hair, GwanaOfficeBuilder.MatHanji);
        }

        /// <summary>서책을 조선식으로 **눕혀 쌓는다** (세워 꽂지 않는다).</summary>
        static void BookStack(Vector2 xz, float supportY, int count, float yaw, string name)
        {
            string[] books = {
                JL + "gyeongbokgung-cheonchujeon-book/source/SM_CCJ_BookA.fbx",
                JL + "gyeongbokgung-cheonchujeon-book/source/SM_CCJ_BookB.fbx",
                JL + "gyeongbokgung-cheonchujeon-book/source/SM_CCJ_BookC.fbx",
            };
            var rnd = new System.Random(name.GetHashCode());
            float y = supportY;
            for (int i = 0; i < count; i++)
            {
                float jy = yaw + (float)(rnd.NextDouble() * 8.0 - 4.0);
                var go = Place(books[i % books.Length], name + "_" + (i + 1),
                               xz + new Vector2((float)(rnd.NextDouble() * 0.03 - 0.015f),
                                                (float)(rnd.NextDouble() * 0.03 - 0.015f)),
                               jy, y);
                if (go == null) return;
                if (i == 0 && name == "장부더미_동")
                {
                    var clue = go.AddComponent<CaseClueInspectable>();
                    clue.clue = ClueId.C5;
                    clue.onlyAt = new[] { TimeOfDay.LateNight };
                    clue.prompt = "장부의 필체와 먹색 살피기";
                }
                y = WorldBounds(go).max.y;
            }
        }

        // ══════════════════════════════════════════════════════
        // 배치 헬퍼
        // ══════════════════════════════════════════════════════
        /// <summary>
        /// 회전 → 바운즈 실측 → 지지면·벽에 스냅. wall: 'W'/'E'/'N'/'S' 이면 그 벽에 등을 붙이고
        /// 나머지 축만 xz 값을 쓴다. supportY 가 NaN 이면 y는 건드리지 않는다.
        /// </summary>
        static GameObject Place(string path, string name, Vector2 xz, float yaw, float supportY,
                                char wall = '.', float gap = 0.05f, float scale = 1f)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { _missing.Add(path); return null; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.name = name;
            go.transform.SetParent(_propRoot, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale = Vector3.one * scale;
            Strip(go);
            Snap(go, xz, yaw, supportY, wall, gap);
            return go;
        }

        /// <summary>
        /// 회전시킨 뒤 바운즈로 지지면·벽에 밀어붙인다 (피벗 규약에 무관).
        ///
        /// ⚠️ **스냅샷이 있으면 계산을 아예 하지 않고 그 포즈를 그대로 쓴다** (사용자 지시:
        ///    "배치를 재계산하거나 정규화하지 마라"). 설계 배치는 **처음 놓을 때만** 쓰인다.
        ///    빌더는 yaw만 주므로, 사용자가 X/Z축까지 돌려 둔 물건(문갑 = KCDF z-up 에셋을
        ///    X 270°로 세워 둠)은 재계산을 타면 통째로 눕는다.
        /// </summary>
        static void Snap(GameObject go, Vector2 xz, float yaw, float supportY, char wall, float gap)
        {
            Pose kept;
            if (_keep.TryGetValue(go.name, out kept))
            {
                go.transform.SetPositionAndRotation(kept.position, kept.rotation);
                return;
            }
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var b = WorldBounds(go);
            Vector3 d = Vector3.zero;
            d.x = wall == 'W' ? (-HalfX + gap) - b.min.x
                : wall == 'E' ? (HalfX - gap) - b.max.x
                : xz.x - b.center.x;
            d.z = wall == 'N' ? (ZN - gap) - b.max.z
                : wall == 'S' ? (ZS + gap) - b.min.z
                : xz.y - b.center.z;
            if (!float.IsNaN(supportY)) d.y = supportY - b.min.y;
            go.transform.position += d;
        }

        /// <summary>MeshCollider·Rigidbody·Animator·팩 스크립트를 걷어내고 미설정 재질을 메꾼다.
        /// ⚠️ Rigidbody를 남기면 Play 순간 자유낙하해 소품이 사라진다 (서고에서 실측).</summary>
        static void Strip(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            // ⚠️ 팩 프리팹이 **자체 Light를 달고 온다** (kcisa Lantern이 흰 Point Light 1개, 실측).
            //    URP 추가 광원 한도가 4라 이런 떠돌이 광원 하나가 창빛 하나를 밀어낸다.
            //    광원이 프리팹 **자식 GO** 에 있으면 컴포넌트 제거가 먹지 않으므로 언팩 후 지운다.
            if (go.GetComponentInChildren<Light>(true) != null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                                                      InteractionMode.AutomatedAction);
                foreach (var l in go.GetComponentsInChildren<Light>(true))
                    Object.DestroyImmediate(l.gameObject);
            }
            foreach (var m in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (m != null && m.GetType().Namespace != "IMUNROK.Gyeonu") Object.DestroyImmediate(m);
            FixJoseonMaterials(go);
        }

        /// <summary>
        /// Joseon Library fbx 일부는 **재질이 미설정**이라 텍스처 없는 흰 덩어리로 뜬다
        /// (병풍 `lambert3`, 문갑 `Wood`/`Gold sus_*` — 실측). 슬롯 이름으로 갈아 끼운다.
        /// 문갑 재질은 서고가 이미 만들어 둔 우리 자산을 재사용한다(중복 생성 금지).
        /// </summary>
        static void FixJoseonMaterials(GameObject go)
        {
            const string ArchProps = "Assets/_Project/Gyeonu/Art/Materials/Archive/Props/";
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = mats[i].name;
                    Material repl = null;
                    if (n.StartsWith("lambert"))
                        repl = ScreenMat();
                    else if (n == "Wood")
                        repl = AssetDatabase.LoadAssetAtPath<Material>(ArchProps + "M_JL_문갑목.mat");
                    else if (n.StartsWith("Gold sus"))
                        repl = AssetDatabase.LoadAssetAtPath<Material>(ArchProps + "M_JL_문갑쇠.mat");
                    if (repl != null) { mats[i] = repl; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static Material _screenMat;
        static Material ScreenMat()
        {
            if (_screenMat != null) return _screenMat;
            const string dir = "Assets/_Project/Gyeonu/Art/Materials/GwanaOffice/";
            const string tex = JL + "gyeongbokgung-cheonchujeon-foldingscreena/textures/T_CCJ_FoldingScreenA_";
            string path = dir + "집무실_병풍.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "BC.png");
            var nm = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "N.png");
            var rn = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "R.png");
            if (bc != null) m.SetTexture("_BaseMap", bc); else _missing.Add(tex + "BC.png");
            if (nm != null) { m.SetTexture("_BumpMap", nm); m.EnableKeyword("_NORMALMAP"); }
            if (rn != null)
            {
                m.SetTexture("_MetallicGlossMap", rn);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(m);
            _screenMat = m;
            return m;
        }

        static Bounds WorldBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>차단 대상 등록만 한다 — 실제 박스는 손 조정 복원 **뒤에** 만든다.
        /// (복원 전에 만들면 콜라이더가 설계 위치에 남아 가구와 어긋난다)</summary>
        static void Block(GameObject go, float shrink)
        {
            if (go != null) _blocks.Add(new KeyValuePair<GameObject, float>(go, shrink));
        }

        static readonly List<KeyValuePair<GameObject, float>> _blocks =
            new List<KeyValuePair<GameObject, float>>();

        /// <summary>가구 차단 박스 — MeshCollider 금지 규칙대로 박스 하나로. 낮은 소품은 제외.</summary>
        static void BuildBlockers()
        {
            foreach (var kv in _blocks)
            {
                var go = kv.Key; float shrink = kv.Value;
                if (go == null) continue;
                var b = WorldBounds(go);
                if (b.size.y < 0.30f) continue;
                // ⚠️ 차단 박스를 **가구의 자식**으로 둔다. DebugInteractor는 레이가 맞은 콜라이더에서
                //    `GetComponentInParent<Interactable>()` 로 대상을 찾으므로, 별도 루트에 있으면
                //    여닫이(FurnitureParts)를 클릭으로 집을 수 없다. 부모만 바꾸고 월드 포즈는 유지.
                var col = new GameObject("차단_" + go.name);
                col.transform.SetParent(go.transform, false);
                col.transform.SetPositionAndRotation(b.center, Quaternion.identity);
                col.AddComponent<BoxCollider>().size =
                    new Vector3(Mathf.Max(0.05f, b.size.x - shrink * 2f),
                                b.size.y,
                                Mathf.Max(0.05f, b.size.z - shrink * 2f));
            }
        }
    }
}
