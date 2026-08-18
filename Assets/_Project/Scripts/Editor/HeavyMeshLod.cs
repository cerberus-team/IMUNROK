using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 무거운 메시에 <b>먼거리 판</b>을 붙인다. 가까이서는 원본 그대로, 멀어지면 줄인 메시로 바뀐다.
    ///
    /// 왜 이렇게 하나: 받아온 고택은 367만 삼각형이고 그 중 지붕 열셋과 담장 스플라인이
    /// 280만을 차지한다. 메시를 아예 줄여 갈아끼우면 코앞에서 기와가 뭉개져 보인다.
    /// 그렇다고 그냥 두면 퀘스트 한 프레임 예산(20~50만)을 한참 넘는다.
    /// 마당 건너 20m 밖 건물은 뭉개져도 눈에 안 띄므로, 거리로 갈라 쓴다.
    ///
    /// 원본은 건드리지 않는다 — 원본 렌더러가 LOD0 이고, 줄인 메시를 든 자식을 LOD1 로 얹는다.
    /// 되돌리려면 씬만 되돌리면 된다.
    ///
    /// 주의: 품질 설정의 LOD 배율(lodBias)이 1보다 크면 먼거리 판이 그만큼 늦게 나온다.
    /// 이 도구가 1로 되돌린다.
    ///
    /// 메뉴: [이문록 ▸ 에셋: 무거운 메시에 먼거리 판 붙이기]
    /// </summary>
    public static class HeavyMeshLod
    {
        /// <summary>이 삼각형 수를 넘는 메시에만 붙인다. 그 아래는 붙여봐야 본전이다.</summary>
        private const int Threshold = 30000;

        /// <summary>먼거리 판에 남길 비율.</summary>
        private const float FarRatio = 0.20f;

        /// <summary>원본을 유지할 화면 높이 비율. 이보다 작아 보이면 먼거리 판으로 바꾼다.</summary>
        private const float SwapAt = 1.40f;

        /// <summary>이보다 작아 보이면 아예 안 그린다.</summary>
        private const float CullAt = 0.006f;

        private const string ChildName = "_먼거리";

        [MenuItem("이문록/에셋: 무거운 메시에 먼거리 판 붙이기")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[먼거리판] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var work = new List<MeshFilter>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var m = mf.sharedMesh;
                if (m == null || !m.isReadable) continue;
                if (m.triangles.Length / 3 <= Threshold) continue;
                if (mf.gameObject.name == ChildName) continue;              // 내가 만든 판은 건너뛴다
                if (mf.GetComponent<MeshRenderer>() == null) continue;
                work.Add(mf);
            }

            if (work.Count == 0)
            {
                Debug.Log("[먼거리판] 삼각형 " + Threshold + " 개를 넘는 메시가 없습니다.");
                return;
            }

            // 같은 메시를 여러 오브젝트가 쓰면 줄인 것도 하나만 굽는다
            var baked = new Dictionary<Mesh, Mesh>();
            long before = 0, after = 0;
            int done = 0;
            var log = new System.Text.StringBuilder();

            try
            {
                for (int i = 0; i < work.Count; i++)
                {
                    var mf = work[i];
                    var src = mf.sharedMesh;
                    EditorUtility.DisplayProgressBar("먼거리 판 붙이기",
                        src.name + " (" + (i + 1) + "/" + work.Count + ")", (float)(i + 1) / work.Count);

                    Mesh far;
                    if (!baked.TryGetValue(src, out far))
                    {
                        int srcTri = src.triangles.Length / 3;
                        far = MeshDecimator.Bake(src, Mathf.Max(2000, Mathf.RoundToInt(srcTri * FarRatio)));
                        baked[src] = far;
                        if (far != null)
                            log.AppendLine("   " + src.name + " : " + srcTri + " → " + (far.triangles.Length / 3));
                    }
                    if (far == null) continue;

                    before += src.triangles.Length / 3;
                    after += far.triangles.Length / 3;
                    Attach(mf, far);
                    done++;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            if (QualitySettings.lodBias > 1.01f)
            {
                log.AppendLine("   LOD 배율 " + QualitySettings.lodBias + " → 1 (먼거리 판이 제때 나오게)");
                QualitySettings.lodBias = 1f;
            }

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            string msg = "오브젝트 " + done + "개에 먼거리 판을 붙였습니다.\n\n" +
                         "멀리서 볼 때 " + before + " → " + after +
                         " 삼각형 (" + (100f * after / Mathf.Max(1, before)).ToString("F0") + "%)";
            // 모달 대화상자는 에디터를 멈춰 세운다(원격으로 명령을 넣는 동안 다리가 끊긴다).
            // 결과는 콘솔로만 남긴다.
            Debug.Log("[먼거리판] " + msg.Replace("\n\n", " / ") + "\n" + log);
        }

        /// <summary>원본 렌더러를 LOD0, 줄인 메시를 든 자식을 LOD1 로 묶는다.</summary>
        private static void Attach(MeshFilter mf, Mesh far)
        {
            var go = mf.gameObject;
            var srcRenderer = mf.GetComponent<MeshRenderer>();

            var child = go.transform.Find(ChildName);
            if (child == null)
            {
                var c = new GameObject(ChildName);
                Undo.RegisterCreatedObjectUndo(c, "먼거리 판");
                c.transform.SetParent(go.transform, false);
                child = c.transform;
            }
            var childGo = child.gameObject;
            GameObjectUtility.SetStaticEditorFlags(childGo, GameObjectUtility.GetStaticEditorFlags(go));

            var cf = childGo.GetComponent<MeshFilter>();
            if (cf == null) cf = childGo.AddComponent<MeshFilter>();
            cf.sharedMesh = far;

            var cr = childGo.GetComponent<MeshRenderer>();
            if (cr == null) cr = childGo.AddComponent<MeshRenderer>();
            cr.sharedMaterials = srcRenderer.sharedMaterials;
            cr.shadowCastingMode = srcRenderer.shadowCastingMode;
            cr.receiveShadows = srcRenderer.receiveShadows;
            cr.lightProbeUsage = srcRenderer.lightProbeUsage;

            var group = go.GetComponent<LODGroup>();
            if (group == null) group = go.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(SwapAt, new Renderer[] { srcRenderer }),
                new LOD(CullAt, new Renderer[] { cr })
            });
            group.RecalculateBounds();
            EditorUtility.SetDirty(go);
        }
    }
}
