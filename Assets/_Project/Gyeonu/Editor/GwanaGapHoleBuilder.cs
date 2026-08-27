using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 담장(화성 행궁 모듈)에 아이들만 아는 개구멍을 낸다. 멱등.
    ///
    /// ■ 자리 — 사용자가 놓아 둔 위치 지정 큐브 (2026-08-21 실측)
    ///   pos (16.911, 0.547, 26.173) / scale (1.00, 1.10, 1.00) — 담장 동쪽 줄(x=17),
    ///   북쪽 모서리에 가까운 조각(동담_14, z 23.41~27.53 구간)에 걸린다.
    ///   큐브를 그대로 따른다 → 구멍 자리 x17 / z 25.67~26.67(폭 1.0) / y −0.03~1.10(기어들 높이).
    ///
    /// ■ 왜 은하담 암문과 다른 방식인가
    ///   암문(교대 석축)은 3D 스캔 통짜 바위라 "표면 근처만 얇게 뜯어낸 뒤 별도 통로를 새로
    ///   짓는" 방식이었다. 이 담장은 화성 행궁 **타일링 모듈**(SM_StraightStronewall_1)이라
    ///   두께가 얇고(0.858m) 균일해서 훨씬 단순하다 — 벽 두께를 통째로 관통하는 상자 하나를
    ///   원본 메시에서 오려 내면 그 자체가 완성된 구멍이다. 별도 굴을 지을 필요가 없다.
    ///   오려 낸 조각(<see cref="BuildGapMeshes"/>의 slabMesh)은 문짝(GapHoleDoor.leaf)으로
    ///   그대로 쓴다 — 재질·형상이 담장과 100% 같으므로 닫혀 있으면 구분이 안 된다.
    ///
    /// ■ 콜라이더 — "구멍"은 상시, "문짝"은 열렸을 때만 (2026-08-21 2차)
    ///   "구멍" 오브젝트의 BoxCollider 하나가 늘 그 자리를 지키며 raycast·보행 차단을 겸한다
    ///   — 들어가는 것은 걸어서가 아니라 클릭 한 번으로 트는 짧은 포복 연출이기 때문이다
    ///   (GapHoleDoor 참고). 문짝(leaf)에도 이제 콜라이더가 있다 — 열린 뒤 "닫기"로 조준할
    ///   수 있어야 하기 때문(GapHoleLeaf)이다. 닫혀 있는 동안은 그 콜라이더를 꺼 둔다 —
    ///   "구멍"과 같은 자리에 겹쳐 있으면 raycast가 어느 쪽을 잡을지 갈린다.
    ///
    /// ■ 문틀 라이너 — 열렸을 때 담장 두께가 두툼하게 보이도록
    ///   원본 메시를 상자로 오려 내면 잘린 경계에 별도 "안쪽 면"이 없다 — 원래 있던 삼각형이
    ///   빠졌을 뿐이라, 뜯긴 자리가 종잇장처럼 얇게 보인다(2026-08-21 사용자 지적). 그래서
    ///   개구멍 높이대(y −0.03~1.10)에서 **실측한 담장 두께**(약 0.63m — 위쪽 벽돌보다
    ///   안쪽으로 물러난 돌 밑단이라 전체 두께 0.858보다 얇다) 그대로 천장·바닥·양옆
    ///   문설주 4장을 담장 재질로 덧대 두툼한 단면을 만든다(<see cref="BuildLiner"/>).
    ///
    /// ■ 이 조각만 LOD0 고정
    ///   담장 조각은 LODGroup(LOD0~3)을 쓴다. 먼 LOD는 구멍이 뚫리지 않은 원본 메시라,
    ///   멀어지면 문이 열려 있어도 도로 막힌 담장처럼 보인다. 동담_14 하나만 LODGroup을
    ///   떼고 LOD0만 남겨 항상 뚫린 채로 보이게 한다(44조각 중 하나뿐이라 부담 없음).
    ///
    /// ■ 원본 무수정
    ///   화성 행궁 FBX·프리팹은 절대 만지지 않는다. mf.sharedMesh를 **이 씬 인스턴스에만**
    ///   재배선한다(같은 프리팹을 쓰는 다른 43개 조각은 원본 메시 그대로).
    ///
    /// ■ 재실행 시 주의
    ///   'Tools ▸ 이문록 ▸ 관아 ▸ 담장 교체'나 '보행 콜라이더 구축'을 다시 돌리면 담장·
    ///   콜라이더가 원본으로 되살아난다 — 그 뒤에는 이 메뉴를 한 번 더 실행할 것.
    /// </summary>
    public static class GwanaGapHoleBuilder
    {
        const string RootName = "관아_개구멍";
        const string MeshDir = "Assets/_Project/Gyeonu/Art/Models/Gwana";

        // ── 자리 (사용자 큐브 실측, 2026-08-21) ──
        const float WallX = 17f;            // GwanaLayout.WallHalfX — 동담 중심선
        const float HoleZ = 26.173f;        // 큐브 z
        const float HoleHalfZ = 0.50f;      // 큐브 scale.z = 1.0 → 폭 1.0
        const float HoleBottom = -0.03f;    // 큐브 하단(-0.003) — 바닥에 살짝 파묻는다
        const float HoleTop = 1.10f;        // 큐브 상단(1.097) 반올림 — 기어들 높이
        const float ThickHalf = 0.50f;      // 절단 반두께 — 실측 담장 두께(0.4289)보다 넉넉히 관통
        const float ColliderThick = 0.80f;  // "구멍" 콜라이더 두께 — 담장 살 속에 파묻히게 실측보다 살짝 얇게

        // ── 문틀 라이너 (2026-08-21 2차) ──
        const float LinerZPad = 0.08f;      // 천장·바닥 라이너가 담장 속으로 파고드는 여유(뜯긴 경계를 덮는다)
        const float LinerYPad = 0.08f;      // 문설주 라이너가 담장 속으로 파고드는 여유
        const float LinerT = 0.05f;         // 라이너 판 두께

        // ── 문 동작 — 은하담 암문과 같은 두 박자 (2026-08-21 2차) ──
        // 담장 실측 두께(약 0.63m)만큼 완전히 빠져나온 뒤에야 옆으로 미끄러져야 마당 쪽
        // 빈 허공에서 깨끗이 떨어져 보인다(안 그러면 옆 담장 몸통과 겹쳐 보인다).
        const float PushDepth = 0.70f;
        const float SlideDistance = 1.30f;
        const float OpenDuration = 1.7f;    // 물러남+미끄러짐 전체 — 1.5~2초 권장 범위 안

        static float HoleZ0 => HoleZ - HoleHalfZ;
        static float HoleZ1 => HoleZ + HoleHalfZ;

        [MenuItem("Tools/이문록/관아 ▸ 개구멍 생성 (담장)", priority = 130)]
        public static void Build()
        {
            if (SceneManager.GetActiveScene().name != "Gyeonu_Gwana")
            { Debug.LogError("[개구멍] 'Gyeonu_Gwana' 씬에서 실행하세요."); return; }

            var wallRoot = GameObject.Find("관아_건물/관아_구조물/담장");
            if (wallRoot == null)
            { Debug.LogError("[개구멍] 담장이 없다 — 먼저 'Tools ▸ 이문록 ▸ 관아 ▸ 담장 교체'를 실행할 것"); return; }

            var piece = FindPieceContaining(wallRoot.transform, new Vector3(WallX, 0.5f, HoleZ));
            if (piece == null)
            { Debug.LogError("[개구멍] 큐브 위치를 포함하는 담장 조각을 찾지 못했다"); return; }

            var lod0T = piece.transform.Find("SM_StraightStronewall_1_LOD0");
            var mf = lod0T != null ? lod0T.GetComponent<MeshFilter>() : piece.GetComponentInChildren<MeshFilter>();
            if (mf == null) { Debug.LogError("[개구멍] 담장 조각에서 LOD0 메시를 찾지 못했다: " + piece.name); return; }

            // 재실행 대비 — 이 조각이 이미 뚫린 사본을 물고 있으면 되돌릴 수 없다.
            // 프리팹 원본(같은 이름의 다른 조각들이 아직 물고 있는 것)에서 다시 읽는다.
            var srcMesh = OriginalPieceMesh(mf);
            if (srcMesh == null) { Debug.LogError("[개구멍] 원본 담장 조각 메시를 읽지 못했다"); return; }

            EnsureFolder(MeshDir);
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var mats = mf.GetComponent<MeshRenderer>().sharedMaterials;

            if (!BuildGapMeshes(mf, srcMesh, out Mesh slabMesh, out Mesh wallMesh, out Vector3 pivot))
            { Debug.LogError("[개구멍] 오려 낼 삼각형이 없다 — 큐브 위치가 이 조각 범위 밖일 수 있다"); return; }

            Undo.RecordObject(mf, "개구멍 뚫기");
            mf.sharedMesh = wallMesh;
            EditorUtility.SetDirty(mf);

            ForceLod0Only(piece);   // 이 조각만 — 멀리서 LOD 갈리면 구멍이 도로 메워져 보인다

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "개구멍 생성");

            var leaf = new GameObject("문짝잎");
            leaf.transform.SetParent(root.transform, false);
            leaf.transform.position = pivot;
            leaf.AddComponent<MeshFilter>().sharedMesh = slabMesh;
            leaf.AddComponent<MeshRenderer>().sharedMaterials = mats;
            var leafCol = leaf.AddComponent<BoxCollider>();
            leafCol.center = slabMesh.bounds.center;
            leafCol.size = slabMesh.bounds.size;
            leafCol.enabled = false;   // 닫혀 있을 때는 꺼 둔다 — "구멍" 콜라이더와 자리가 겹친다

            var holeGo = new GameObject("구멍");
            holeGo.transform.SetParent(root.transform, false);
            holeGo.transform.position = new Vector3(WallX, 0f, HoleZ);
            var bc = holeGo.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, (HoleBottom + HoleTop) * 0.5f, 0f);
            bc.size = new Vector3(ColliderThick, HoleTop - HoleBottom, 2f * HoleHalfZ - 0.04f);

            var outsideA = new GameObject("바깥지점").transform;
            outsideA.SetParent(root.transform, true);
            outsideA.SetPositionAndRotation(new Vector3(WallX + 1.30f, 0f, HoleZ),
                                             Quaternion.LookRotation(Vector3.left, Vector3.up));

            var insideA = new GameObject("안지점").transform;
            insideA.SetParent(root.transform, true);
            insideA.SetPositionAndRotation(new Vector3(WallX - 1.50f, 0f, HoleZ),
                                            Quaternion.LookRotation(Vector3.right, Vector3.up));

            var door = holeGo.AddComponent<GapHoleDoor>();
            door.leaf = leaf.transform;
            door.pushAxis = Vector3.left;                         // 먼저 마당 쪽(−X)으로 물러난다
            door.pushDepth = PushDepth;
            door.slideAxis = Vector3.back;                        // 그다음 담장을 따라 남쪽으로 미끄러진다
            door.slideDistance = SlideDistance;
            door.openDuration = OpenDuration;
            door.requiredFlag = GyeonuWorld.F_개구멍이야기;
            door.openKey = "gwana_gap_hole_opened";
            door.outsideDir = Vector3.right;                      // 동담 바깥쪽 = +X
            door.outsideAnchor = outsideA;
            door.insideAnchor = insideA;
            door.crawlDuration = 1.2f;
            door.displayName = "";   // "밀기/들어가기/나가기"만 뜬다 — "상호작용" 접두사 없이

            var leafInteract = leaf.AddComponent<GapHoleLeaf>();
            leafInteract.door = door;
            leafInteract.displayName = "";   // "닫기"만 뜬다

            BuildLiner(root.transform, mats[2], pivot, slabMesh.bounds);

            AdjustWalkCollider(root.transform);

            int killed = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t == null || t.parent != null) continue;
                if (t.name != "Cube" && !t.name.StartsWith("Cube (")) continue;
                Object.DestroyImmediate(t.gameObject); killed++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[개구멍] {piece.name}에 개구멍 생성 — x{WallX} z{HoleZ0:F2}~{HoleZ1:F2} y{HoleBottom:F2}~{HoleTop:F2}. 위치용 큐브 {killed}개 제거.");
        }

        /// <summary>
        /// 개구부 둘레(천장·바닥·양옆 문설주)에 담장 재질 판을 덧대 두툼한 단면을 만든다.
        /// 두께는 slabBounds(오려 낸 문짝의 로컬 바운즈 — pivot 기준)에서 그대로 읽는다 —
        /// 개구멍 높이대에서 실제로 존재하는 돌의 두께이므로 하드코딩보다 정확하다.
        /// </summary>
        static void BuildLiner(Transform root, Material stoneMat, Vector3 pivot, Bounds slabBounds)
        {
            float xMin = pivot.x + slabBounds.min.x, xMax = pivot.x + slabBounds.max.x;

            var liner = new GameObject("문틀_라이너");
            liner.transform.SetParent(root, true);

            void Box(string nm, Vector3 mn, Vector3 mx)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = nm;
                Object.DestroyImmediate(go.GetComponent<BoxCollider>());
                go.transform.SetParent(liner.transform, true);
                go.transform.position = (mn + mx) * 0.5f;
                go.transform.localScale = mx - mn;
                go.GetComponent<MeshRenderer>().sharedMaterial = stoneMat;
            }

            Box("천장", new Vector3(xMin, HoleTop - LinerT, HoleZ0 - LinerZPad), new Vector3(xMax, HoleTop, HoleZ1 + LinerZPad));
            Box("바닥", new Vector3(xMin, HoleBottom, HoleZ0 - LinerZPad), new Vector3(xMax, HoleBottom + LinerT, HoleZ1 + LinerZPad));
            Box("문설주_남", new Vector3(xMin, HoleBottom - LinerYPad, HoleZ0 - LinerT), new Vector3(xMax, HoleTop + LinerYPad, HoleZ0));
            Box("문설주_북", new Vector3(xMin, HoleBottom - LinerYPad, HoleZ1), new Vector3(xMax, HoleTop + LinerYPad, HoleZ1 + LinerT));

            Debug.Log($"[개구멍] 문틀 라이너 4장 — 실측 두께 {xMax - xMin:F3}m (x{xMin:F3}~{xMax:F3})");
        }

        /// <summary>wallRoot(서담/동담/남담_서/남담_동/북담) 밑을 뒤져 점을 담는 조각을 찾는다.</summary>
        static GameObject FindPieceContaining(Transform wallRoot, Vector3 point)
        {
            foreach (Transform run in wallRoot)
                foreach (Transform piece in run)
                {
                    var r = piece.GetComponentInChildren<Renderer>();
                    if (r == null) continue;
                    var b = r.bounds;
                    if (point.x >= b.min.x - 0.05f && point.x <= b.max.x + 0.05f &&
                        point.z >= b.min.z - 0.05f && point.z <= b.max.z + 0.05f)
                        return piece.gameObject;
                }
            return null;
        }

        /// <summary>이 조각이 이미 뚫린 사본을 물고 있을 수 있으므로, 같은 프리팹을 쓰는
        /// 형제 조각(아직 안 뚫린 것) 중 하나에서 원본 메시를 다시 얻는다. 전부 뚫려 있으면
        /// (있을 수 없지만) 현재 것을 그대로 쓴다 — 이미 뚫려 있다면 아래에서 후보가 0개로 걸린다.</summary>
        static Mesh OriginalPieceMesh(MeshFilter mf)
        {
            var run = mf.transform.parent.parent;   // 조각 → LOD0(mf) → parent=조각, parent.parent=담장줄
            if (run != null)
            {
                foreach (Transform sib in run)
                {
                    var t = sib.Find("SM_StraightStronewall_1_LOD0");
                    if (t == null) continue;
                    var sibMf = t.GetComponent<MeshFilter>();
                    if (sibMf == null || sibMf == mf) continue;
                    if (sibMf.sharedMesh != null && sibMf.sharedMesh.name == "SM_StraightStronewall_1_LOD0")
                        return sibMf.sharedMesh;
                }
            }
            return mf.sharedMesh;
        }

        /// <summary>이 담장 조각 하나만 LODGroup을 떼고 LOD0만 남긴다 (원본 프리팹은 무수정).</summary>
        static void ForceLod0Only(GameObject piece)
        {
            var lg = piece.GetComponent<LODGroup>();
            if (lg != null) Object.DestroyImmediate(lg);
            var toKill = new List<GameObject>();
            foreach (Transform c in piece.transform)
                if (c.name.EndsWith("LOD1") || c.name.EndsWith("LOD2") || c.name.EndsWith("LOD3"))
                    toKill.Add(c.gameObject);
            foreach (var g in toKill) Object.DestroyImmediate(g);
        }

        // ══════════════════════════════════════════════════
        //  메시 절단 — 벽 두께를 관통하는 상자를 원본에서 오려 낸다
        // ══════════════════════════════════════════════════

        /// <summary>
        /// 담장 조각 로컬 좌표계는 그대로 두고(World→Local 왕복), 월드 기준 상자
        /// (x0~x1 / y0~y1 / z0~z1)에 걸치는 삼각형만 6면으로 잘라 안쪽은 문짝(slabMesh),
        /// 바깥 자투리는 담장(wallMesh)에 남긴다. 서브메시(브릭·기와 4종)를 그대로 보존해서
        /// 문짝도 담장과 같은 재질 배열로 칠하면 완전히 같은 표면이 된다.
        /// </summary>
        static bool BuildGapMeshes(MeshFilter mf, Mesh srcMesh, out Mesh slabMesh, out Mesh wallMesh, out Vector3 pivot)
        {
            slabMesh = null; wallMesh = null;
            pivot = new Vector3(WallX, (HoleBottom + HoleTop) * 0.5f, HoleZ);

            Matrix4x4 mtx = mf.transform.localToWorldMatrix;
            Matrix4x4 inv = mf.transform.worldToLocalMatrix;

            var v = srcMesh.vertices;
            var nrms = srcMesh.normals;
            bool hasN = nrms != null && nrms.Length == v.Length;
            var uvs = srcMesh.uv;
            bool hasUV = uvs != null && uvs.Length == v.Length;

            float x0 = WallX - ThickHalf, x1 = WallX + ThickHalf;
            float y0 = HoleBottom, y1 = HoleTop;
            float z0 = HoleZ0, z1 = HoleZ1;
            var planes = new System.Func<Vector3, float>[]
            {
                p => p.x - x0, p => x1 - p.x,
                p => p.y - y0, p => y1 - p.y,
                p => p.z - z0, p => z1 - p.z,
            };

            var wv = new List<Vector3>(v);
            var wn = new List<Vector3>(hasN ? nrms : new Vector3[v.Length]);
            var wuv = new List<Vector2>(hasUV ? uvs : new Vector2[v.Length]);
            var sv = new List<Vector3>(); var sn = new List<Vector3>(); var suv = new List<Vector2>();

            var wallSub = new List<List<int>>();
            var slabSub = new List<List<int>>();
            int cutTotal = 0, slabTriCount = 0;

            for (int s = 0; s < srcMesh.subMeshCount; s++)
            {
                var sub = srcMesh.GetTriangles(s);
                var wallKeep = new List<int>(sub.Length);
                var slabKeep = new List<int>();

                for (int i = 0; i < sub.Length; i += 3)
                {
                    int ia = sub[i], ib = sub[i + 1], ic = sub[i + 2];
                    var wa = mtx.MultiplyPoint3x4(v[ia]);
                    var wb = mtx.MultiplyPoint3x4(v[ib]);
                    var wc = mtx.MultiplyPoint3x4(v[ic]);

                    bool outsideBox =
                        Mathf.Max(wa.x, Mathf.Max(wb.x, wc.x)) < x0 || Mathf.Min(wa.x, Mathf.Min(wb.x, wc.x)) > x1 ||
                        Mathf.Max(wa.y, Mathf.Max(wb.y, wc.y)) < y0 || Mathf.Min(wa.y, Mathf.Min(wb.y, wc.y)) > y1 ||
                        Mathf.Max(wa.z, Mathf.Max(wb.z, wc.z)) < z0 || Mathf.Min(wa.z, Mathf.Min(wb.z, wc.z)) > z1;

                    if (outsideBox)
                    { wallKeep.Add(ia); wallKeep.Add(ib); wallKeep.Add(ic); continue; }

                    cutTotal++;
                    var tri = new[]
                    {
                        new Vtx { p = wa, n = hasN ? mtx.MultiplyVector(nrms[ia]).normalized : Vector3.up, uv = hasUV ? uvs[ia] : Vector2.zero },
                        new Vtx { p = wb, n = hasN ? mtx.MultiplyVector(nrms[ib]).normalized : Vector3.up, uv = hasUV ? uvs[ib] : Vector2.zero },
                        new Vtx { p = wc, n = hasN ? mtx.MultiplyVector(nrms[ic]).normalized : Vector3.up, uv = hasUV ? uvs[ic] : Vector2.zero },
                    };

                    var cur = new List<Vtx[]> { tri };
                    var frag = new List<Vtx[]>();
                    foreach (var pl in planes)
                    {
                        var next = new List<Vtx[]>();
                        foreach (var tt in cur) SplitTri(tt, pl, next, frag);
                        cur = next;
                    }
                    foreach (var f in cur)              // 안쪽 = 뜯겨 나가는 문짝
                    {
                        slabTriCount++;
                        for (int k = 0; k < 3; k++)
                        { slabKeep.Add(sv.Count); sv.Add(f[k].p - pivot); sn.Add(f[k].n); suv.Add(f[k].uv); }
                    }
                    foreach (var f in frag)             // 담장에 남는 자투리 — 로컬로 되돌린다
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            wallKeep.Add(wv.Count);
                            wv.Add(inv.MultiplyPoint3x4(f[k].p));
                            wn.Add(inv.MultiplyVector(f[k].n).normalized);
                            wuv.Add(f[k].uv);
                        }
                    }
                }
                wallSub.Add(wallKeep);
                slabSub.Add(slabKeep);
            }

            if (slabTriCount == 0) return false;

            var sm = new Mesh { name = "관아_개구멍_문짝잎", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            sm.SetVertices(sv); sm.SetNormals(sn); sm.SetUVs(0, suv);
            sm.subMeshCount = slabSub.Count;
            for (int s = 0; s < slabSub.Count; s++) sm.SetTriangles(slabSub[s], s);
            sm.RecalculateBounds();
            slabMesh = SaveMesh(sm, "관아_개구멍_문짝잎");

            var wm = new Mesh { name = "관아_담장_개구멍뚫림", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            wm.SetVertices(wv); wm.SetNormals(wn); wm.SetUVs(0, wuv);
            wm.subMeshCount = wallSub.Count;
            for (int s = 0; s < wallSub.Count; s++) wm.SetTriangles(wallSub[s], s);
            wm.RecalculateBounds();
            wallMesh = SaveMesh(wm, "관아_담장_개구멍뚫림");

            AssetDatabase.SaveAssets();
            Debug.Log($"[개구멍] 오려 냄 — 걸친 삼각 {cutTotal}개 처리, 문짝 {slabTriCount}삼각");
            return true;
        }

        struct Vtx { public Vector3 p, n; public Vector2 uv; }

        static Vtx VLerp(Vtx a, Vtx b, float t) => new Vtx
        {
            p = Vector3.Lerp(a.p, b.p, t),
            n = Vector3.Slerp(a.n, b.n, t).normalized,
            uv = Vector2.Lerp(a.uv, b.uv, t)
        };

        /// <summary>삼각형 하나를 평면(f ≥ 0 이 안쪽)으로 가른다. Ammun(교대 암문)과 같은 절단기.</summary>
        static void SplitTri(Vtx[] t, System.Func<Vector3, float> f, List<Vtx[]> inList, List<Vtx[]> outList)
        {
            float d0 = f(t[0].p), d1 = f(t[1].p), d2 = f(t[2].p);
            int pos = (d0 >= 0 ? 1 : 0) + (d1 >= 0 ? 1 : 0) + (d2 >= 0 ? 1 : 0);
            if (pos == 3) { inList.Add(t); return; }
            if (pos == 0) { outList.Add(t); return; }

            var d = new[] { d0, d1, d2 };
            int lone = 0;
            for (int i = 0; i < 3; i++)
                if ((pos == 1) == (d[i] >= 0)) { lone = i; break; }
            int i1 = (lone + 1) % 3, i2 = (lone + 2) % 3;
            Vtx A = t[lone], B = t[i1], C = t[i2];
            var AB = VLerp(A, B, d[lone] / (d[lone] - d[i1]));
            var AC = VLerp(A, C, d[lone] / (d[lone] - d[i2]));
            var aSide = new[] { A, AB, AC };
            var q1 = new[] { AB, B, C };
            var q2 = new[] { AB, C, AC };
            if (pos == 1) { inList.Add(aSide); outList.Add(q1); outList.Add(q2); }
            else { outList.Add(aSide); inList.Add(q1); inList.Add(q2); }
        }

        static Mesh SaveMesh(Mesh built, string name)
        {
            string path = MeshDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                built.name = name;
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            existing.Clear();
            existing.indexFormat = built.indexFormat;
            existing.vertices = built.vertices;
            existing.normals = built.normals;
            existing.uv = built.uv;
            existing.subMeshCount = built.subMeshCount;
            for (int s = 0; s < built.subMeshCount; s++) existing.SetTriangles(built.GetTriangles(s), s);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        // ══════════════════════════════════════════════════
        //  보행 콜라이더 재조정 — 동담 줄에서 구멍 대역만 비운다
        // ══════════════════════════════════════════════════

        /// <summary>
        /// GwanaWalkSetup이 만든 "담장_측"(동담, x&gt;0) 박스 하나가 동담 전체를 통짜로 막는다.
        /// 그 박스를 끄고, 구멍 대역만 비운 3토막(남쪽/북쪽/구멍 위)으로 대신 세운다 —
        /// 은하담 암문의 "문 상부/하부만 막고 가운데는 비우는" 패턴과 같다.
        /// 구멍 대역의 아래쪽(y −0.03~1.10)은 비워 둔다 — 닫혀 있을 때는 "구멍" 오브젝트의
        /// BoxCollider가 그 자리를 채운다(GapHoleDoor 참고).
        /// </summary>
        static void AdjustWalkCollider(Transform gapRoot)
        {
            var walkRoot = GameObject.Find("관아_보행콜라이더");
            if (walkRoot == null)
            { Debug.LogWarning("[개구멍] 관아_보행콜라이더가 없다 — 먼저 '관아 ▸ 보행 콜라이더 구축'을 실행할 것. 콜라이더 조정 건너뜀"); return; }

            Transform east = null;
            foreach (Transform c in walkRoot.transform)
                if (c.name == "담장_측" && c.position.x > 0f) { east = c; break; }
            if (east == null) { Debug.LogWarning("[개구멍] 동담 보행 콜라이더(담장_측)를 찾지 못했다"); return; }

            var bc = east.GetComponent<BoxCollider>();
            if (bc == null) { Debug.LogWarning("[개구멍] 동담 보행 콜라이더에 BoxCollider가 없다"); return; }
            var b = bc.bounds;   // 회전·스케일 없음 → center ± size/2 그대로 월드 범위

            float zPad = 0.03f;
            float gz0 = HoleZ0 - zPad, gz1 = HoleZ1 + zPad;

            east.gameObject.SetActive(false);   // 원본은 끄고 남긴다(재실행 대비 원형 보존)

            var grp = new GameObject("동담_보행콜라이더_개구멍적용");
            grp.transform.SetParent(gapRoot, true);

            void Box(string nm, Vector3 mn, Vector3 mx)
            {
                if (mx.x - mn.x < 0.03f || mx.y - mn.y < 0.03f || mx.z - mn.z < 0.03f) return;
                var go = new GameObject(nm);
                go.transform.SetParent(grp.transform, true);
                go.transform.position = (mn + mx) * 0.5f;
                go.AddComponent<BoxCollider>().size = mx - mn;
            }

            Box("동담_남쪽", new Vector3(b.min.x, b.min.y, b.min.z), new Vector3(b.max.x, b.max.y, gz0));
            Box("동담_북쪽", new Vector3(b.min.x, b.min.y, gz1), new Vector3(b.max.x, b.max.y, b.max.z));
            Box("동담_개구멍_윗막이", new Vector3(b.min.x, HoleTop + 0.05f, gz0), new Vector3(b.max.x, b.max.y, gz1));

            Debug.Log($"[개구멍] 동담 보행 콜라이더 재조정 — z {gz0:F2}~{gz1:F2} 대역만 y{HoleTop + 0.05f:F2} 아래로 비움");
        }
    }
}
