using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 마을 담장 콜라이더 박스화 (2026-08-08, 멱등).
    ///
    /// 팩 담장 MeshCollider는 기와 처마 오버행 때문에 오목한 형태 — 캡슐이 처마 밑에
    /// 끼는 원인. 세그먼트별로 MeshCollider를 끄고, 세그먼트 로컬 AABB 기반 BoxCollider로
    /// 대체한다 (두께는 0.7로 클램프 — 처마 폭 제외, 상단 +0.6 연장 — 담장 위 등반 차단).
    /// 문 세그먼트가 없는 자리(문 개구부)는 자동으로 뚫려 있다.
    ///
    /// 대상: 집터 Wall01c/02c/03c 전부, 마을 전면 석벽(SM_Stonewall02a),
    ///       KM 담장(선아집·어머니집) 직선/코너 — 일각문 제외(문리깅 담당).
    /// </summary>
    public static class VillageWallBoxer
    {
        [MenuItem("Tools/이문록/마을 담장 박스화")]
        public static void Build()
        {
            int boxed = 0, meshOff = 0;

            // 집터 담장 세그먼트
            var jip = GameObject.Find("성하리_집터");
            if (jip != null)
                foreach (var tr in jip.GetComponentsInChildren<Transform>(true))
                    if (tr.name.StartsWith("Wall01c") || tr.name.StartsWith("Wall02c") || tr.name.StartsWith("Wall03c"))
                        BoxSegment(tr, ref boxed, ref meshOff);

            // 마을 전면 석벽
            var props = GameObject.Find("성하리_소품");
            if (props != null)
                foreach (Transform tr in props.transform)
                    if (tr.name.StartsWith("SM_Stonewall"))
                        BoxSegment(tr, ref boxed, ref meshOff);

            // KM 담장 (일각문 제외)
            foreach (var rootName in new[] { "성하리_건물/선아집_담장", "성하리_건물/어머니집_담장" })
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (Transform tr in root.transform)
                {
                    if (tr.name.Contains("일각문")) continue;
                    BoxSegment(tr, ref boxed, ref meshOff);
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[담장박스] 완료 — 박스 {boxed}, MeshCollider OFF {meshOff}");
        }

        static void BoxSegment(Transform seg, ref int boxed, ref int meshOff)
        {
            var rends = seg.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;

            // 기존 메시 콜라이더 OFF
            foreach (var col in seg.GetComponentsInChildren<Collider>(true))
                if (col is MeshCollider && col.enabled) { col.enabled = false; meshOff++; }

            // 로컬 AABB — 메시 로컬 바운드 꼭짓점 변환 (월드 AABB 역변환은 회전 세그먼트에서 팽창)
            Vector3 lMin = Vector3.one * float.MaxValue, lMax = Vector3.one * float.MinValue;
            foreach (var r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? mb.min.x : mb.max.x,
                        (i & 2) == 0 ? mb.min.y : mb.max.y,
                        (i & 4) == 0 ? mb.min.z : mb.max.z);
                    var lp = seg.InverseTransformPoint(r.transform.TransformPoint(corner));
                    lMin = Vector3.Min(lMin, lp);
                    lMax = Vector3.Max(lMax, lp);
                }
            }
            if (lMin.x > lMax.x) return;
            var size = lMax - lMin;
            // 두께(짧은 수평축)를 0.7로 클램프 — 기와 처마 오버행 제외
            if (size.x < size.z) size.x = Mathf.Min(size.x, 0.7f);
            else size.z = Mathf.Min(size.z, 0.7f);
            size.y += 0.6f;                                      // 상단 연장 — 담장 위 등반 차단
            var center = new Vector3((lMin.x + lMax.x) * 0.5f, lMin.y + size.y * 0.5f, (lMin.z + lMax.z) * 0.5f);

            var box = seg.GetComponent<BoxCollider>();
            if (box == null) box = seg.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            boxed++;
        }
    }
}
