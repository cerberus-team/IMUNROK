using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static IMUNROK.Gyeonu.Editor.GwanaLayout;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관문(외삼문)을 팀원 전달본 에셋으로 교체하고 문지기 나졸 2명을 세운다. 멱등.
    ///
    /// 전달본: Assets/Joseon Library/GwanaSet_FOR_TEAM/GwanaSet_FOR_TEAM.unitypackage
    ///   → 통째로 임포트하면 팀원 작업 폴더 _Project/Seocheon/ 에 동헌 텍스처 150MB가 쏟아진다.
    ///     문·나졸에 필요한 51개만 골라 원본 경로(Assets/Models, _Import, GwanaHandoff)에 풀고
    ///     그 세 폴더를 gitignore 했다. GUID는 전달본 그대로라 나중에 전체 임포트해도 충돌 없다.
    ///   → 담장·동헌은 우리 것을 그대로 쓰므로 프리팹에서 SM_Oisamun 과 나졸만 꺼내 쓴다.
    ///     (프리팹의 담장 조각들은 메시가 _Project/ 밑이라 임포트하지 않았다 — 참조가 비어 있는 게 정상)
    ///
    /// ── 스케일 (2026-08-20 저녁 방향 전환) ──
    /// 팀 문은 프리팹에서 이미 0.65배 축소돼 있고 그 상태의 실측이 폭 12.212 / 전고 5.418 /
    /// 깊이 5.417 이다. **이 크기를 그대로 쓴다(Fit = 1).**
    ///
    /// 처음에는 우리 절차생성 담장(높이 3.08)에 문을 맞추려고 1.30배 키웠는데, 그러자 실제 키
    /// 1.903m 인 나졸이 문에 비해 피규어처럼 작아 보였다. 문을 키우는 대신 **담장을 팀 담장
    /// (높이 2.09)으로 교체**해 비례를 맞춘다 — 문 5.418 : 담장 2.09 : 사람 1.903 은 전달본이
    /// 원래 가지고 있던 비례다. 담장 쪽은 GwanaWallSwap 참조.
    ///
    /// ★아래 실측 상수는 전부 Fit=1 기준이고 프로퍼티에서 Fit 을 곱한다. Fit 만 바꾸면
    ///  콜라이더·문짝 분리 상자·나졸 위치가 함께 따라온다(예전엔 배율마다 손으로 고쳐야 했다).
    /// </summary>
    public static class GwanaGateSwap
    {
        public const string PrefabPath = "Assets/GwanaHandoff/Prefabs/PF_GwanaGateSet.prefab";

        /// <summary>
        /// 프리팹의 0.65배 위에 더 곱하는 배율.
        ///
        /// 2026-08-20 저녁: 1.30 → **1.00 (원본 크기)** 로 되돌렸다.
        /// 우리 절차생성 담장(높이 3.08)에 문 높이를 맞추려고 1.30배 키웠더니, 실제 키(1.903m)인
        /// 나졸이 문에 비해 피규어처럼 작아 보였다. 문을 키우는 대신 **담장을 팀 담장(높이 2.09)으로
        /// 바꿔** 비례를 맞추는 쪽으로 방향을 틀었다 — 문 5.418 : 담장 2.09 : 사람 1.903 은
        /// 전달본이 원래 갖고 있던 비례다. 담장 교체는 GwanaWallSwap 참조.
        /// </summary>
        public const float Fit = 1.00f;

        // ── 문 실측값 ────────────────────────────────────────
        // 아래는 전부 **Fit=1 (원본 크기) 기준 실측치**이고, 실제로 쓰는 값은 Fit 을 곱한 프로퍼티다.
        // 예전에 Fit=1.30 기준 숫자를 상수로 박아 뒀다가 배율을 바꾸면 전부 어긋났다 —
        // 이제 Fit 만 바꾸면 콜라이더·문짝 분리 상자·나졸 위치가 같이 따라온다.
        const float N_PlatformTop = 0.18538f;   // 기단 상면
        const float N_SillTop = 0.39923f;       // 중앙칸 문지방 윗면
        const float N_PlatformHalfX = 5.9231f;
        const float N_PlatformZ0 = -2.7692f, N_PlatformZ1 = 2.2692f;
        const float N_BodyZ0 = -1.4615f, N_BodyZ1 = 0.7308f;
        const float N_PassHalfX = 1.0154f;
        const float N_GuardX = 2.3846f, N_GuardZ = -2.3077f;   // 문 중심 기준 로컬

        /// <summary>기단 상면. 마당(0)에서 이만큼 올라선다 — stepOffset 0.6 으로 그냥 오른다.</summary>
        public static float PlatformTop => N_PlatformTop * Fit;
        /// <summary>중앙칸 문지방(하인방) 윗면.</summary>
        public static float SillTop => N_SillTop * Fit;
        public static float PlatformHalfX => N_PlatformHalfX * Fit;
        /// <summary>기단 앞뒤 끝 (문 중심 기준 로컬 z).</summary>
        public static float PlatformZ0 => N_PlatformZ0 * Fit;
        public static float PlatformZ1 => N_PlatformZ1 * Fit;
        /// <summary>벽·방·문짝이 들어찬 몸체 깊이 (문 중심 기준 로컬 z). 앞뒤 처마밑 앞마당은 열려 있다.</summary>
        public static float BodyZ0 => N_BodyZ0 * Fit;
        public static float BodyZ1 => N_BodyZ1 * Fit;
        /// <summary>중앙칸 통로 유효 반폭. 문선 안쪽면 실측보다 3cm 안 — 어깨가 문설주에 박히지 않게.</summary>
        public static float PassHalfX => N_PassHalfX * Fit;

        /// <summary>나졸 좌우 위치 — 통로 밖, 협문칸 앞. 나졸 자체는 배율 1(실측 키 1.903)로 둔다.</summary>
        public static float GuardX => N_GuardX * Fit;
        /// <summary>나졸 z (월드) — 기단 앞마당 한가운데. y는 기단 상면을 실측해 얹는다.</summary>
        public static float GuardZ => GateZ + N_GuardZ * Fit;

        /// <summary>
        /// 담장이 파고들 문 옆면의 x (실측, Fit=1 기준 3.86).
        ///
        /// ★함정: 처마 끝은 ±6.106, 기단은 ±5.92, 협문칸 앞뒤 벽은 ±5.84 까지 나오지만,
        ///  담장 높이대(y 0.3~2.0)의 **연속된 면은 협문칸 안쪽 벽 ±3.86** 뿐이다.
        ///  협문칸은 바깥면이 없이 뚫려 있고, 전달본에서는 그 자리를 별도 조각
        ///  (Wall_S_*_Patch, 메시가 _Project/Seocheon 밑이라 미임포트)으로 메우고 있었다.
        ///  5.77 로 잡았다가 담장이 문에 닿지 않고 1.5m 벌어졌다 — 담장을 3.86 안쪽까지 넣어
        ///  협문칸의 뚫린 옆을 담장이 직접 막게 한다.
        /// </summary>
        public static float SideFaceX => 3.86f * Fit;

        // ── 중앙칸 문짝 열기 ──────────────────────────────────
        // ★전달본 외삼문은 세 칸 문짝이 전부 닫힌 상태로 온다. 그대로 두면 관아에 들어갈 수 없다.
        //   원본 FBX를 건드리지 않고, 중앙칸 문짝 삼각형만 뽑아 별도 메시로 만들고
        //   본체 메시에서는 그 삼각형을 빼는 파생 메시를 만들어 쓴다.
        //   파생 메시는 gitignore — 원본이 gitignore라 커밋하면 외부 에셋을 우회 커밋하는 셈이 된다.
        //   메뉴를 다시 돌리면 재생성되므로 잃어버릴 일은 없다.
        //
        // 아래 상자는 문 그룹 로컬(= 월드 − (0,0,GateZ)) 기준 실측값이다.
        //   중앙칸 문선  x ±2.10 / 큰 기둥 x ±4.95 (기둥은 z −0.10~0.05 라 z 하한으로 걸러진다)
        //   문짝 판      z 0.10~0.25, y 0.53(문지방 윗면 0.519 바로 위)~3.78(상인방 밑)
        // ★판정은 삼각형 "무게중심"으로 한다. 정점 전부가 상자 안이어야 한다는 조건으로 했더니
        //  문짝 아래쪽 큰 사각형(정점이 상자 경계에 1cm 걸침)이 통째로 남아 반만 열렸다.
        static readonly Vector3 N_DoorBoxMin = new Vector3(-1.5846f, 0.4077f, 0.0692f);
        static readonly Vector3 N_DoorBoxMax = new Vector3(1.5846f, 2.9077f, 0.2077f);
        static Vector3 DoorBoxMin => N_DoorBoxMin * Fit;
        static Vector3 DoorBoxMax => N_DoorBoxMax * Fit;
        static float HingeX => 1.5385f * Fit;
        static float HingeZ => 0.1308f * Fit;
        /// <summary>여는 각. 90°를 넘겨 문선 쪽으로 살짝 접어 붙인다. 안쪽(+Z, 마당)으로 열린다.</summary>
        const float OpenAngle = 100f;

        const string DerivedDir = "Assets/_Project/Gyeonu/Art/Models/Gwana";

        public static bool Available => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;

        [MenuItem("Tools/이문록/관아 ▸ 관문 교체(팀 에셋)")]
        public static void SwapInScene()
        {
            var host = GameObject.Find("관아_건물/관아_구조물");
            if (host == null) { Debug.LogError("[관아] 관아_건물/관아_구조물 이 없다 — 구조물부터 생성할 것"); return; }

            // 담장도 함께 갈아끼운다 — 문과 담장은 비례가 한 몸이라 따로 돌리면 어긋난다.
            var oldWall = host.transform.Find("담장");
            int wallSib = oldWall != null ? oldWall.GetSiblingIndex() : 0;
            if (oldWall != null) Object.DestroyImmediate(oldWall.gameObject);
            if (GwanaWallSwap.BuildInto(host.transform))
                host.transform.Find("담장").SetSiblingIndex(wallSib);

            var old = host.transform.Find("외삼문");
            int sibling = old != null ? old.GetSiblingIndex() : host.transform.childCount;
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = BuildGateInto(host.transform);
            if (go == null) return;
            go.transform.SetSiblingIndex(sibling);
            RigDoors(go.transform);

            GwanaWalkSetup.BuildColliders();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>외삼문 + 문지기를 parent 아래에 만든다. 에셋이 없으면 null.</summary>
        public static GameObject BuildGateInto(Transform parent)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (pf == null)
            {
                Debug.LogError("[관아] 팀 게이트 프리팹 없음: " + PrefabPath +
                               "\n  GwanaSet_FOR_TEAM.unitypackage 에서 임포트할 것");
                return null;
            }

            var src = Find(pf, "SM_Oisamun");
            if (src == null) { Debug.LogError("[관아] 프리팹에 SM_Oisamun 이 없다"); return null; }

            var go = new GameObject("외삼문");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, CourtY, GateZ);

            // 프리팹 공간에서 문은 통과축이 X, 폭축이 Z 다. +90° 돌려 통과축을 Z 로 맞춘다.
            // 프리팹 −X(나졸이 선 바깥면)가 월드 −Z, 즉 언덕에서 올라오는 플레이어를 향한다.
            var align = Quaternion.Euler(0f, -90f, 0f);

            Matrix4x4 m = LocalToRoot(pf.transform, src);
            var mf = src.GetComponent<MeshFilter>();
            var mr = src.GetComponent<MeshRenderer>();

            // 문의 기준점 = 폭·깊이 중심의 지면. 메시 원점이 여기와 같다는 보장이 없으므로 실측한다.
            var vs = mf.sharedMesh.vertices;
            Vector3 mn = new Vector3(9e9f, 9e9f, 9e9f), mx = -mn;
            for (int i = 0; i < vs.Length; i++) { var p = m.MultiplyPoint3x4(vs[i]); mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
            Vector3 baseP = new Vector3((mn.x + mx.x) * 0.5f, mn.y, (mn.z + mx.z) * 0.5f);
            Vector3 originP = m.GetColumn(3);

            var body = new GameObject("문채", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = align * ((originP - baseP) * Fit);
            body.transform.localRotation = align * m.rotation;
            body.transform.localScale = Vector3.one * (m.lossyScale.x * Fit);
            body.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            body.GetComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;

            OpenCenterDoors(go.transform, body);
            BuildGuards(go.transform, pf);
            return go;
        }

        /// <summary>
        /// 중앙칸 문짝 두 짝을 본체 메시에서 떼어내 경첩(문선)을 축으로 안쪽으로 연다.
        /// 실패하면 본체는 원본 그대로 두므로 문이 닫힌 채 남는다(로그로 알린다).
        /// </summary>
        static void OpenCenterDoors(Transform gate, GameObject body)
        {
            var src = body.GetComponent<MeshFilter>().sharedMesh;
            var mats = body.GetComponent<MeshRenderer>().sharedMaterials;
            // 문 그룹 로컬 ← 문채 로컬
            Matrix4x4 toGate = gate.worldToLocalMatrix * body.transform.localToWorldMatrix;
            Quaternion nrot = Quaternion.Inverse(gate.rotation) * body.transform.rotation;

            var sv = src.vertices; var sn = src.normals; var st = src.tangents; var su = src.uv;
            var g = new Vector3[sv.Length];
            for (int i = 0; i < sv.Length; i++) g[i] = toGate.MultiplyPoint3x4(sv[i]);

            // 서브메시별로 문짝(좌/우)·본체로 삼각형을 가른다
            var keep = new System.Collections.Generic.List<int[]>();
            var leaf = new System.Collections.Generic.List<int>[2];
            leaf[0] = new System.Collections.Generic.List<int>();
            leaf[1] = new System.Collections.Generic.List<int>();
            var leafSub = new System.Collections.Generic.List<int>[2];
            leafSub[0] = new System.Collections.Generic.List<int>();
            leafSub[1] = new System.Collections.Generic.List<int>();
            int moved = 0;

            for (int s = 0; s < src.subMeshCount; s++)
            {
                var idx = src.GetTriangles(s);
                var kept = new System.Collections.Generic.List<int>(idx.Length);
                for (int i = 0; i < idx.Length; i += 3)
                {
                    int a = idx[i], b = idx[i + 1], c = idx[i + 2];
                    Vector3 ct = (g[a] + g[b] + g[c]) / 3f;
                    if (In(ct))
                    {
                        // 좌/우는 무게중심 x 로 가른다 (짝은 x=0 에서 맞물린다)
                        int side = ct.x < 0f ? 0 : 1;
                        leaf[side].Add(a); leaf[side].Add(b); leaf[side].Add(c);
                        leafSub[side].Add(s);
                        moved++;
                    }
                    else { kept.Add(a); kept.Add(b); kept.Add(c); }
                }
                keep.Add(kept.ToArray());
            }

            if (moved < 100 || leaf[0].Count == 0 || leaf[1].Count == 0)
            {
                Debug.LogError("[관아] 중앙칸 문짝을 찾지 못했다 (삼각형 " + moved + "개) — 문이 닫힌 채로 남는다."
                             + "\n  전달본 외삼문의 형상이 바뀌었을 수 있다. GwanaGateSwap.DoorBoxMin/Max 를 다시 실측할 것");
                return;
            }

            // ① 본체 = 원본 정점 그대로, 문짝 삼각형만 뺀 사본
            var nb = new Mesh { name = "외삼문_문채", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            nb.vertices = sv; if (sn != null && sn.Length == sv.Length) nb.normals = sn;
            if (st != null && st.Length == sv.Length) nb.tangents = st;
            if (su != null && su.Length == sv.Length) nb.uv = su;
            nb.subMeshCount = src.subMeshCount;
            for (int s = 0; s < keep.Count; s++) nb.SetTriangles(keep[s], s, false);
            nb.RecalculateBounds();
            body.GetComponent<MeshFilter>().sharedMesh = SaveMesh(nb);

            // ② 문짝 2짝 — 경첩 원점 기준으로 좌표를 구워 넣고 GO 회전으로 연다
            for (int side = 0; side < 2; side++)
            {
                float hx = side == 0 ? -HingeX : HingeX;
                float ang = side == 0 ? -OpenAngle : OpenAngle;
                var hinge = new Vector3(hx, 0f, HingeZ);

                var map = new System.Collections.Generic.Dictionary<int, int>();
                var vp = new System.Collections.Generic.List<Vector3>();
                var vn = new System.Collections.Generic.List<Vector3>();
                var vt = new System.Collections.Generic.List<Vector4>();
                var vu = new System.Collections.Generic.List<Vector2>();
                var bySub = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>>();

                var li = leaf[side]; var ls = leafSub[side];
                for (int t = 0; t < li.Count; t += 3)
                {
                    int sub = ls[t / 3];
                    if (!bySub.TryGetValue(sub, out var lst)) { lst = new System.Collections.Generic.List<int>(); bySub[sub] = lst; }
                    for (int k = 0; k < 3; k++)
                    {
                        int o = li[t + k];
                        if (!map.TryGetValue(o, out int n2))
                        {
                            n2 = vp.Count; map[o] = n2;
                            vp.Add(g[o] - hinge);
                            vn.Add(sn != null && sn.Length == sv.Length ? nrot * sn[o] : Vector3.up);
                            if (st != null && st.Length == sv.Length)
                            { var tt = st[o]; var r = nrot * new Vector3(tt.x, tt.y, tt.z); vt.Add(new Vector4(r.x, r.y, r.z, tt.w)); }
                            vu.Add(su != null && su.Length == sv.Length ? su[o] : Vector2.zero);
                        }
                        lst.Add(n2);
                    }
                }

                var lm = new Mesh { name = "외삼문_문짝_" + (side == 0 ? "좌" : "우") };
                lm.SetVertices(vp); lm.SetNormals(vn);
                if (vt.Count == vp.Count) lm.SetTangents(vt);
                lm.SetUVs(0, vu);
                var subs = new System.Collections.Generic.List<int>(bySub.Keys); subs.Sort();
                lm.subMeshCount = subs.Count;
                var lmats = new Material[subs.Count];
                for (int i = 0; i < subs.Count; i++) { lm.SetTriangles(bySub[subs[i]], i, false); lmats[i] = mats[subs[i]]; }
                lm.RecalculateBounds();

                var lo = new GameObject(lm.name, typeof(MeshFilter), typeof(MeshRenderer));
                lo.transform.SetParent(gate, false);
                lo.transform.localPosition = hinge;
                lo.transform.localRotation = Quaternion.identity;   // 씬에는 닫힌 자세로 저장 — 여닫이가 열고 닫는다
                lo.GetComponent<MeshFilter>().sharedMesh = SaveMesh(lm);
                lo.GetComponent<MeshRenderer>().sharedMaterials = lmats;
            }

            Debug.Log("[관아] 중앙칸 문짝 " + moved + "삼각형을 좌우로 떼어냈다 (닫힌 자세로 저장, 여닫이 " + OpenAngle + "°)");
        }

        /// <summary>
        /// 중앙칸 문짝 2짝을 쌍여닫이로 리깅한다 — 마을 사립문·김명관 일각문과 같은 방식
        /// (DoubleHingeDoor 를 문 부모에 붙이고, 문짝 콜라이더에서 GetComponentInParent 로 찾는다).
        ///
        /// · 씬에는 닫힌 자세로 저장된다 — DoubleHingeDoor.Awake 가 그 자세를 '닫힘'으로 기억한다.
        /// · 콜라이더는 문짝별 BoxCollider 라 문과 함께 회전한다 → 닫히면 막히고 열리면 통과된다.
        ///   (중앙 통로는 GwanaWalkSetup 의 정적 벽이 비워 두고 있으므로, 밤에 막는 것은 이 문짝이다)
        /// · 낮/밤 기본 자세는 GateDayNight 가 GyeonuWorld 를 구독해 정한다.
        /// </summary>
        static void RigDoors(Transform gate)
        {
            var l = gate.Find("외삼문_문짝_좌");
            var r = gate.Find("외삼문_문짝_우");
            if (l == null || r == null)
            { Debug.LogWarning("[관아] 문짝을 못 찾아 여닫이 리깅을 건너뛴다"); return; }

            foreach (var leaf in new[] { l, r })
            {
                var mesh = leaf.GetComponent<MeshFilter>().sharedMesh;
                var box = leaf.GetComponent<BoxCollider>();
                if (box == null) box = leaf.gameObject.AddComponent<BoxCollider>();
                box.center = mesh.bounds.center;
                box.size = mesh.bounds.size;
            }

            var dd = gate.GetComponent<DoubleHingeDoor>();
            if (dd == null) dd = gate.gameObject.AddComponent<DoubleHingeDoor>();
            dd.leftLeaf = l;
            dd.rightLeaf = r;
            // 문짝 메시는 경첩 원점 기준으로 구워져 있고 GO 가 경첩 위치에 놓여 있다 → 피벗 = 그 위치
            dd.leftPivot = l.localPosition;
            dd.rightPivot = r.localPosition;
            dd.leftAngle = -OpenAngle;    // 둘 다 마당 안쪽(+Z)으로 열린다
            dd.rightAngle = OpenAngle;
            dd.duration = 1.2f;           // 큰 대문이라 사립문(0.8)보다 느리게
            dd.displayName = "외삼문";
            dd.lockedPrompt = "굳게 닫혀 있다";
            dd.locked = false;
            // ★관아 정문은 플레이어가 여닫지 못한다 (2026-08-20 확정).
            //   낮엔 열려 있고 밤엔 닫혀 있는 것이 연출이고, 그 통제권은 관아에 있지 플레이어에게 없다.
            //   끄면 조준해도 조준점·이름이 뜨지 않고 클릭도 먹지 않는다. 시간대 자동 전환은 그대로다.
            dd.playerOperable = false;

            var dn = gate.GetComponent<GateDayNight>();
            if (dn == null) dn = gate.gameObject.AddComponent<GateDayNight>();
            dn.door = dd;
            dn.openByDay = true;
            dn.openByNight = false;
            // 2026-09-10 — 밤 잠금은 GateDayNight 가 늘 켠다 (개구멍이 정규 경로). 스위치는 없앴다.

            // ★컴포넌트를 붙이는 순간 에디터가 Awake/OnEnable 을 돌려 문짝을 '열린 자세'로 밀어 놓는다
            //   (실제로 문짝이 처마 밖 1.5m 로 날아갔다). 씬에는 반드시 닫힌 자세가 저장돼야 하므로
            //   컴포넌트를 다 붙인 **뒤에** 경첩 자리로 되돌려 못박는다. 여는 것은 런타임의 몫이다.
            l.localPosition = dd.leftPivot; l.localRotation = Quaternion.identity;
            r.localPosition = dd.rightPivot; r.localRotation = Quaternion.identity;

            EditorUtility.SetDirty(gate.gameObject);
            Debug.Log("[관아] 외삼문 쌍여닫이 리깅 — 낮 열림 / 밤 닫힘, 클릭 토글 (밤 잠금 항상)");
        }

        static bool In(Vector3 p) =>
            p.x >= DoorBoxMin.x && p.x <= DoorBoxMax.x &&
            p.y >= DoorBoxMin.y && p.y <= DoorBoxMax.y &&
            p.z >= DoorBoxMin.z && p.z <= DoorBoxMax.z;

        /// <summary>
        /// 파생 메시를 경로 재사용으로 저장(GUID 보존).
        /// ★함정: EditorUtility.CopySerialized 는 Mesh 의 CPU 데이터만 바꾸고 GPU 버퍼는 그대로 둔다.
        ///  그래서 메뉴를 두 번째 돌리면 데이터는 새 메시인데 화면에는 **이전 메시가 계속 그려진다**.
        ///  (실제로 문짝을 다 떼어냈는데도 닫힌 문이 그대로 보였고, 레이캐스트·정점 계산은 전부
        ///   "뚫려 있다"고 나와 한참 헤맸다.) UploadMeshData 로 버퍼를 강제로 다시 올려야 한다.
        /// </summary>
        static Mesh SaveMesh(Mesh m)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath(DerivedDir));
            string path = DerivedDir + "/" + m.name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                EditorUtility.CopySerialized(m, old);
                old.UploadMeshData(false);          // ★이 줄이 없으면 화면이 갱신되지 않는다
                EditorUtility.SetDirty(old);
                return old;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>
        /// 문지기 나졸 2명. 문에는 Fit 배율을 걸었지만 사람은 실측 키(1.903m, 전립 포함)가
        /// 맞으므로 배율 1 로 둔다 — 1.30을 같이 먹이면 2.47m 거인이 된다.
        /// ★NPC_Gatekeeper_S 의 localScale.x 는 음수(미러)다. 부호를 유지해야 두 명이
        ///  서로 반대 손에 삼지창을 든다. 미러 쪽 재질(M_Gatekeeper_Mirror)도 짝을 지켜야 한다.
        ///  셰이더가 URP/Unlit 이라 음수 스케일로 인한 노멀 반전은 문제가 없다.
        /// </summary>
        static void BuildGuards(Transform gate, GameObject pf)
        {
            var group = new GameObject("문지기");
            group.transform.SetParent(gate, false);

            // 발 높이는 기단 상면을 실물로 재서 얹는다 — Fit 을 바꿔도 따라온다.
            var body = gate.Find("문채").gameObject;
            var probe = body.AddComponent<MeshCollider>();
            // ★레이 시작점을 처마보다 아래에서 잡는다. 예전엔 y=4 에서 쐈다가 Fit 을 1.0 으로
            //   되돌리자 지붕(3.44)에 먼저 맞아 나졸이 공중에 떴다.
            float footY = GroundHeight(GuardX, GuardZ);
            float from = PlatformTop + 1.0f;
            if (probe.Raycast(new Ray(new Vector3(GuardX, from, GuardZ), Vector3.down), out var h, from + 2f)) footY = h.point.y;
            Object.DestroyImmediate(probe);

            // 프리팹 Z(폭축) → 월드 −X. S(−2.6)가 동쪽, N(+2.4)이 서쪽이 된다.
            Guard(group.transform, pf, "NPC_Gatekeeper_S", "나졸_동", +GuardX, footY);
            Guard(group.transform, pf, "NPC_Gatekeeper_N", "나졸_서", -GuardX, footY);
        }

        static void Guard(Transform parent, GameObject pf, string srcName, string name, float x, float footY)
        {
            var src = Find(pf, srcName);
            if (src == null) { Debug.LogError("[관아] 프리팹에 " + srcName + " 없음"); return; }
            var srcMesh = src.Find("Mesh");

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            // 발바닥이 루트 y 에 딱 맞는 모델이다(로컬 bounds min.y = 0).
            go.transform.position = new Vector3(x, footY, GuardZ);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 문 바깥(−Z) = 플레이어를 향한다
            go.transform.localScale = src.localScale;                  // ★S는 (−1,1,1) 미러

            var mesh = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            mesh.transform.SetParent(go.transform, false);
            mesh.transform.localPosition = srcMesh.localPosition;
            mesh.transform.localRotation = srcMesh.localRotation;
            mesh.transform.localScale = srcMesh.localScale;            // FBX 보정 (100,100,100)
            mesh.GetComponent<MeshFilter>().sharedMesh = srcMesh.GetComponent<MeshFilter>().sharedMesh;
            mesh.GetComponent<MeshRenderer>().sharedMaterials = srcMesh.GetComponent<MeshRenderer>().sharedMaterials;

            // 캡슐은 켜 둔다 — 나졸은 통로(±1.6) 밖 x=±3.10 에 서므로 통행을 막지 않고,
            // 사람 몸을 통과해 지나가 버리는 VR 최악의 인상만 없앤다.
            // 낮에 앞을 가로막는 연출을 붙일 때는 이 캡슐을 옮기거나 별도 차단 트리거를 더하면 된다.
            var src_cc = src.GetComponent<CapsuleCollider>();
            var cc = go.AddComponent<CapsuleCollider>();
            if (src_cc != null)
            {
                cc.center = src_cc.center; cc.radius = src_cc.radius;
                cc.height = src_cc.height; cc.direction = src_cc.direction;
            }
        }

        // ── 헬퍼 ──────────────────────────────────────────────
        static Transform Find(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>t 의 로컬 → root 로컬 변환. 프리팹 에셋은 씬에 없어 worldToLocal 을 못 쓴다.</summary>
        static Matrix4x4 LocalToRoot(Transform root, Transform t)
        {
            Matrix4x4 m = Matrix4x4.identity;
            for (var c = t; c != null && c != root; c = c.parent)
                m = Matrix4x4.TRS(c.localPosition, c.localRotation, c.localScale) * m;
            return m;
        }
    }
}
