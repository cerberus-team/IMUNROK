using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 마을 담장 문 리깅 v3 (2026-08-08, 멱등) — 사립문 11개를 쌍여닫이로.
    ///
    /// SM_Door01k 메시는 폭이 로컬 Z축(±0.936), 좌우 문짝이 한 메시에 조각돼 있으나
    /// 중앙선 걸침 삼각형이 16개뿐이라 무게중심 기준 좌/우 절반 메시로 분할 가능
    /// (분할본은 Art/Models/사립문짝_좌·우.asset — 원본 에셋 무수정, 11개 문이 공유).
    /// 집터가 프리팹 인스턴스라 기존 자식 구조 변경 불가 → 원본 문짝은 비활성화하고
    /// 새 문짝 GO 2개(추가 오브젝트는 허용)를 만들어 DoubleHingeDoor(부모에 부착)로 구동.
    /// 콜라이더는 문짝별 BoxCollider — 함께 회전. 열림 방향은 마당 안쪽(5° 가상 회전 실측).
    ///
    /// 입구 대문(Ogongmun)은 여닫이가 아님 — v2가 붙인 HingeDoor·BoxCollider를 제거해
    /// 항상 열린 상태(문짝 3개는 열린 포즈로 배치·조각된 원래 모습 유지)로 되돌린다.
    /// </summary>
    public static class VillageDoorRigger
    {
        const string LeftMeshPath = "Assets/_Project/Gyeonu/Art/Models/사립문짝_좌.asset";
        const string RightMeshPath = "Assets/_Project/Gyeonu/Art/Models/사립문짝_우.asset";

        [MenuItem("Tools/이문록/마을 담장 문 리깅")]
        public static void Rig()
        {
            var jip = GameObject.Find("성하리_집터");
            if (jip == null) { Debug.LogError("[문리깅] 성하리_집터 없음"); return; }

            int made = 0, updated = 0;
            Mesh leftMesh = null, rightMesh = null;

            foreach (var tr in jip.GetComponentsInChildren<Transform>(true))
            {
                if (!tr.name.StartsWith("Door01k")) continue;
                if (tr.parent != null && tr.parent.name.StartsWith("Door01k")) continue;
                var orig = FindChild(tr, "SM_Door01k");
                if (orig == null) continue;

                // 분할 메시 준비 (최초 1회 — 전 문이 공유)
                if (leftMesh == null)
                {
                    var srcMesh = orig.GetComponent<MeshFilter>().sharedMesh;
                    leftMesh = LoadOrBuildHalf(srcMesh, true, LeftMeshPath);
                    rightMesh = LoadOrBuildHalf(srcMesh, false, RightMeshPath);
                }

                RigDouble(tr, orig, leftMesh, rightMesh, LotInward(tr), ref made, ref updated);
            }

            // ── 입구 대문: 여닫이 아님 — v2 리깅 제거, 항상 열림 ──
            int ogmCleaned = 0;
            var ogm = GameObject.Find("성하리_소품/Ogongmun_Gate");
            if (ogm != null)
                foreach (var tr in ogm.GetComponentsInChildren<Transform>(true))
                {
                    if (!tr.name.Contains("Door")) continue;
                    var hd = tr.GetComponent<HingeDoor>();
                    if (hd != null) { Object.DestroyImmediate(hd); ogmCleaned++; }
                    var bc = tr.GetComponent<BoxCollider>();
                    if (bc != null) { Object.DestroyImmediate(bc); ogmCleaned++; }
                }

            // ── KM 일각문 쌍여닫이 (선아집 — 어머니집은 대문 프리팹 미배치) ──
            int km = 0;
            km += RigKmGate("성하리_건물/선아집_담장", "성하리_건물/SeonaHouse");
            km += RigKmGate("성하리_건물/어머니집_담장", "성하리_건물/MotherHouse");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[문리깅] v3 완료 — 쌍여닫이 신규 {made}, 갱신 {updated}, 대문 리깅 제거 {ogmCleaned}컴포넌트, KM 일각문 {km}");
        }

        /// <summary>KM 일각문: 문짝 2개(SM_Door01C*)가 이미 분리돼 있음 — 메시 분할 없이
        /// DoubleHingeDoor로 구동. 문짝의 MeshCollider는 끄고 메시 자식에 BoxCollider.</summary>
        static int RigKmGate(string wallRootPath, string housePath)
        {
            var wallRoot = GameObject.Find(wallRootPath);
            if (wallRoot == null) return 0;
            Transform gate = null;
            foreach (Transform c in wallRoot.transform)
                if (c.name.Contains("일각문")) { gate = c; break; }
            if (gate == null) { Debug.LogWarning("[문리깅] " + wallRootPath + "에 일각문 없음 — 건너뜀"); return 0; }

            var leaves = new List<Transform>();
            foreach (var tr in gate.GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith("SM_Door01C")) leaves.Add(tr);
            if (leaves.Count == 0) { Debug.LogWarning("[문리깅] 일각문 문짝(SM_Door01C*) 없음"); return 0; }

            // 마당 안쪽 = 집 방향
            Vector3 inward = gate.forward;
            var house = GameObject.Find(housePath);
            if (house != null)
            {
                var hr = house.GetComponentInChildren<Renderer>();
                inward = hr.bounds.center - gate.position;
                inward.y = 0f; inward.Normalize();
            }

            // 문짝별 경첩(바깥) 에지: 두 문짝 중점에서 먼 쪽
            Vector3 mid = Vector3.zero;
            foreach (var l in leaves) mid += l.GetComponentInChildren<Renderer>().bounds.center;
            mid /= leaves.Count;

            var pivots = new Vector3[leaves.Count];
            var frees = new Vector3[leaves.Count];
            for (int i = 0; i < leaves.Count; i++)
            {
                var mf = leaves[i].GetComponentInChildren<MeshFilter>();
                var b = mf.sharedMesh.bounds;
                bool widthX = b.extents.x >= b.extents.z;
                Vector3 axis = widthX ? new Vector3(b.extents.x, 0f, 0f) : new Vector3(0f, 0f, b.extents.z);
                Vector3 eA = mf.transform.TransformPoint(b.center + axis);
                Vector3 eB = mf.transform.TransformPoint(b.center - axis);
                bool aOuter = (eA - mid).sqrMagnitude >= (eB - mid).sqrMagnitude;
                pivots[i] = aOuter ? eA : eB;
                frees[i] = aOuter ? eB : eA;

                // 콜라이더: MeshCollider OFF → 메시 자식에 Box (문짝과 함께 회전)
                foreach (var col in leaves[i].GetComponentsInChildren<Collider>(true))
                    if (col is MeshCollider) col.enabled = false;
                var meshGo = mf.gameObject;
                var box = meshGo.GetComponent<BoxCollider>();
                if (box == null) box = meshGo.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }

            if (leaves.Count >= 2)
            {
                var dd = gate.GetComponent<DoubleHingeDoor>();
                if (dd == null) dd = gate.gameObject.AddComponent<DoubleHingeDoor>();
                dd.leftLeaf = leaves[0];
                dd.rightLeaf = leaves[1];
                dd.leftPivot = leaves[0].parent.InverseTransformPoint(pivots[0]);
                dd.rightPivot = leaves[1].parent.InverseTransformPoint(pivots[1]);
                dd.leftAngle = 100f * SwingSignAt(pivots[0], frees[0], inward);
                dd.rightAngle = 100f * SwingSignAt(pivots[1], frees[1], inward);
                dd.duration = 0.8f;
                dd.displayName = "일각문";
                EditorUtility.SetDirty(gate.gameObject);
                return 1;
            }
            else
            {
                var hd = leaves[0].GetComponent<HingeDoor>();
                if (hd == null) hd = leaves[0].gameObject.AddComponent<HingeDoor>();
                hd.pivotInParent = leaves[0].parent.InverseTransformPoint(pivots[0]);
                hd.openAngle = 100f * SwingSignAt(pivots[0], frees[0], inward);
                hd.duration = 0.8f;
                hd.displayName = "일각문";
                EditorUtility.SetDirty(leaves[0].gameObject);
                return 1;
            }
        }

        static float SwingSignAt(Vector3 pivotW, Vector3 freeW, Vector3 inward)
        {
            Vector3 v = freeW - pivotW; v.y = 0f;
            Vector3 moved = Quaternion.AngleAxis(5f, Vector3.up) * v - v;
            return Vector3.Dot(moved, inward) >= 0f ? 1f : -1f;
        }

        static void RigDouble(Transform doorParent, Transform orig, Mesh leftMesh, Mesh rightMesh,
            Vector3 inward, ref int made, ref int updated)
        {
            // v2 잔재 제거 + 원본 문짝 비활성 (렌더·콜라이더 모두 새 문짝이 담당)
            var oldHd = orig.GetComponent<HingeDoor>();
            if (oldHd != null) Object.DestroyImmediate(oldHd);
            var oldBox = orig.GetComponent<BoxCollider>();
            if (oldBox != null) Object.DestroyImmediate(oldBox);
            orig.gameObject.SetActive(false);

            var parentMc = doorParent.GetComponent<MeshCollider>();
            if (parentMc != null) parentMc.enabled = false;   // 팩의 통짜 문 콜라이더

            var mats = orig.GetComponent<MeshRenderer>().sharedMaterials;
            bool isNew = doorParent.Find("문짝_좌") == null;
            var lLeaf = EnsureLeaf(doorParent, "문짝_좌", orig, leftMesh, mats);
            var rLeaf = EnsureLeaf(doorParent, "문짝_우", orig, rightMesh, mats);

            // 경첩(바깥 끝)·자유단(중앙) — 원본 메시 로컬 기준, 폭 = Z축
            var src = orig.GetComponent<MeshFilter>().sharedMesh;
            var b = src.bounds;
            Vector3 centerEdge = b.center;                                   // 중앙(자유단)
            Vector3 lEdge = b.center - new Vector3(0f, 0f, b.extents.z);     // 좌짝 바깥 = -z
            Vector3 rEdge = b.center + new Vector3(0f, 0f, b.extents.z);     // 우짝 바깥 = +z

            Vector3 centerW = orig.TransformPoint(centerEdge);
            Vector3 lPivotW = orig.TransformPoint(lEdge);
            Vector3 rPivotW = orig.TransformPoint(rEdge);

            var dd = doorParent.GetComponent<DoubleHingeDoor>();
            if (dd == null) dd = doorParent.gameObject.AddComponent<DoubleHingeDoor>();
            dd.leftLeaf = lLeaf;
            dd.rightLeaf = rLeaf;
            dd.leftPivot = doorParent.InverseTransformPoint(lPivotW);
            dd.rightPivot = doorParent.InverseTransformPoint(rPivotW);
            dd.leftAngle = 100f * SwingSign(lPivotW, centerW, inward);
            dd.rightAngle = 100f * SwingSign(rPivotW, centerW, inward);
            dd.duration = 0.8f;
            dd.displayName = "사립문";
            EditorUtility.SetDirty(doorParent.gameObject);
            if (isNew) made++; else updated++;
        }

        /// <summary>자유단을 경첩 기준 +5° 회전 → 마당 쪽으로 움직이면 +1.</summary>
        static float SwingSign(Vector3 pivotW, Vector3 freeW, Vector3 inward)
        {
            Vector3 v = freeW - pivotW; v.y = 0f;
            Vector3 moved = Quaternion.AngleAxis(5f, Vector3.up) * v - v;
            return Vector3.Dot(moved, inward) >= 0f ? 1f : -1f;
        }

        static Transform EnsureLeaf(Transform doorParent, string name, Transform orig, Mesh mesh, Material[] mats)
        {
            var leaf = doorParent.Find(name);
            if (leaf == null)
            {
                var go = new GameObject(name);
                leaf = go.transform;
                leaf.SetParent(doorParent, false);
            }
            leaf.localPosition = orig.localPosition;
            leaf.localRotation = orig.localRotation;
            leaf.localScale = orig.localScale;

            var mf = leaf.GetComponent<MeshFilter>();
            if (mf == null) mf = leaf.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = leaf.GetComponent<MeshRenderer>();
            if (mr == null) mr = leaf.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            var box = leaf.GetComponent<BoxCollider>();
            if (box == null) box = leaf.gameObject.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = mesh.bounds.size;
            return leaf;
        }

        static Mesh LoadOrBuildHalf(Mesh src, bool leftSide, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            float cz = src.bounds.center.z;
            var verts = src.vertices; var normals = src.normals;
            var uvs = src.uv; var tangents = src.tangents;
            var tris = src.GetTriangles(0);

            var map = new Dictionary<int, int>();
            var oV = new List<Vector3>(); var oN = new List<Vector3>();
            var oU = new List<Vector2>(); var oT = new List<Vector4>();
            var oTris = new List<int>();

            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                float triZ = (verts[a].z + verts[b].z + verts[c].z) / 3f - cz;   // 무게중심으로 소속 결정
                if ((triZ < 0f) != leftSide) continue;
                foreach (int idx in new[] { a, b, c })
                {
                    if (!map.TryGetValue(idx, out int ni))
                    {
                        ni = oV.Count;
                        map[idx] = ni;
                        oV.Add(verts[idx]);
                        if (normals.Length > 0) oN.Add(normals[idx]);
                        if (uvs.Length > 0) oU.Add(uvs[idx]);
                        if (tangents.Length > 0) oT.Add(tangents[idx]);
                    }
                    oTris.Add(ni);
                }
            }

            var m = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            m.SetVertices(oV);
            if (oN.Count > 0) m.SetNormals(oN);
            if (oU.Count > 0) m.SetUVs(0, oU);
            if (oT.Count > 0) m.SetTangents(oT);
            m.SetTriangles(oTris, 0);
            m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[문리깅] 분할 메시 생성: " + path + " (tris " + oTris.Count / 3 + ")");
            return m;
        }

        static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform c in parent) if (c.name == name) return c;
            return null;
        }

        /// <summary>집터 담장(Wall*) 위치 평균 ≈ 마당 중심 → 문에서 마당으로 향하는 수평 방향.</summary>
        static Vector3 LotInward(Transform door)
        {
            var lot = door.parent;
            var sum = Vector3.zero; int n = 0;
            foreach (Transform c in lot)
                if (c.name.StartsWith("Wall")) { sum += c.position; n++; }
            if (n == 0) return door.forward;
            var inward = sum / n - door.position;
            inward.y = 0f;
            return inward.normalized;
        }
    }
}
