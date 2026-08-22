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
        /// <summary>
        /// 이 삼각형 수를 넘는 메시에만 붙인다. 그 아래는 붙여봐야 본전이다.
        ///
        /// 30,000 으로 두었더니 담장 스플라인(한 짝 1만7천~2만7천)이 죄다 빠져
        /// 판이 일곱 덩이에만 붙었다. 담은 마당을 빙 둘러 있어 수가 많으므로
        /// 한 짝은 작아도 합치면 크다. 15,000 으로 내린다.
        /// </summary>
        private const int Threshold = 15000;

        /// <summary>
        /// <b>중간 판</b>에 남길 비율. 마당 건너 정도에서 쓰인다. 기와 골이 살아 있어 티가 안 난다.
        /// </summary>
        private const float MidRatio = 0.25f;

        /// <summary>
        /// <b>먼거리 판</b>에 남길 비율. 8% 로 구우면 지붕 실루엣이 조각나지만,
        /// 이게 쓰이는 거리(담 너머)에서는 실루엣만 남아 티가 안 난다.
        /// 가까이서 이게 나오면 안 되므로 문턱을 낮게 잡는다(<see cref="FarAt"/>).
        /// </summary>
        private const float FarRatio = 0.08f;

        /// <summary>
        /// 원본을 유지할 화면 높이 비율. 이보다 <b>크게</b> 보일 때 원본을 쓴다.
        ///
        /// ★유니티는 이 값을 <b>0~1 로 잘라 넣는다</b> — 1.40 을 넣으면 1.0 이 된다.
        /// 1.0 은 "화면 높이를 꽉 채울 때만 원본"이라는 뜻인데, 처마 밑에 들어서면
        /// 실제로 그렇게 되므로 이 값은 이대로가 맞다. 머리 위 지붕은 원본이어야 한다.
        /// </summary>
        private const float SwapAt = 1.0f;

        /// <summary>
        /// 중간 판으로 내려가는 문턱. <b>상한인 1.0</b> 으로 둔다.
        ///
        /// 0.30 으로 두었더니 지붕 한 채(size 15m)가 44m 밖까지 원본이었다 — 고택이
        /// 60m 남짓이니 어디에 서든 원본이라는 뜻이다. 1.0 이면 13m 안쪽에서만 원본이다.
        ///
        /// 더 올릴 수는 없다(유니티가 1.0 으로 자른다). 그래서 <b>지붕 두 채는 손을 못 댄다</b> —
        /// SM_RoofE 가 30.1m, SM_RoofG 가 35.6m 짜리 한 덩이라 1.0 에서도 26~31m 안이
        /// 원본이고, 그 안에 마당 전체가 들어온다. 저 둘을 잡으려면 채별로 쪼개야 한다.
        /// </summary>
        private const float MidAt = 1.0f;

        /// <summary>
        /// 먼거리 판으로 내려가는 문턱. 25% 판도 처마 밑에서 보면 서까래가 깨지므로,
        /// 판이 나오는 거리를 넉넉히 잡는다. 0.25 면 15m 짜리 지붕이 52m 밖에서 8% 판이 된다.
        /// </summary>
        private const float FarAt = 0.25f;

        /// <summary>이보다 작아 보이면 아예 안 그린다.</summary>
        private const float CullAt = 0.006f;

        /// <summary>
        /// 품질 설정의 LOD 배율. 1보다 작으면 물체가 작아 보이는 셈이 되어 판이 일찍 나온다.
        ///
        /// 0.5 로 내려 봤더니 삼각형은 2,852,459 → 1,796,733 으로 잘 떨어졌으나
        /// <b>머리 위 지붕까지 판으로 바뀌어</b> 처마 밑에서 서까래가 조각나 보였다.
        /// 그래서 배율은 1 로 두고 대신 단을 셋으로 나눈다 — 가까이는 원본,
        /// 마당 건너는 중간 판, 담 너머는 먼거리 판.
        /// </summary>
        private const float TargetLodBias = 1f;

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
                var rr = mf.GetComponent<MeshRenderer>();
                if (rr == null) continue;
                // 꺼 둔 렌더러에는 붙이지 않는다. 쪼개기(BigMeshSplitter)가 원본을 끄고
                // 조각을 대신 세우는데, 여기서 원본에도 판을 구우면 안 쓰는 메시가
                // 프로젝트에 쌓이고 셈까지 부풀린다.
                if (!rr.enabled) continue;
                work.Add(mf);
            }

            if (work.Count == 0)
            {
                Debug.Log("[먼거리판] 삼각형 " + Threshold + " 개를 넘는 메시가 없습니다.");
                return;
            }

            // 같은 메시를 여러 오브젝트가 쓰면 줄인 것도 하나만 굽는다
            var bakedMid = new Dictionary<Mesh, Mesh>();
            var bakedFar = new Dictionary<Mesh, Mesh>();
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

                    int srcTri = src.triangles.Length / 3;

                    Mesh mid;
                    if (!bakedMid.TryGetValue(src, out mid))
                    {
                        mid = MeshDecimator.Bake(src, Mathf.Max(4000, Mathf.RoundToInt(srcTri * MidRatio)), "_중간");
                        bakedMid[src] = mid;
                    }
                    Mesh far;
                    if (!bakedFar.TryGetValue(src, out far))
                    {
                        far = MeshDecimator.Bake(src, Mathf.Max(1500, Mathf.RoundToInt(srcTri * FarRatio)), "_먼");
                        bakedFar[src] = far;
                        if (far != null && mid != null)
                            log.AppendLine("   " + src.name + " : " + srcTri
                                           + " → 중간 " + (mid.triangles.Length / 3)
                                           + " → 먼 " + (far.triangles.Length / 3));
                    }
                    if (far == null || mid == null) continue;

                    before += srcTri;
                    after += mid.triangles.Length / 3;
                    Attach(mf, mid, far);
                    done++;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            if (Mathf.Abs(QualitySettings.lodBias - TargetLodBias) > 0.01f)
            {
                log.AppendLine("   LOD 배율 " + QualitySettings.lodBias + " → " + TargetLodBias + " (먼거리 판이 제때 나오게)");
                QualitySettings.lodBias = TargetLodBias;
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

        /// <summary>
        /// 원본을 LOD0, 중간 판을 LOD1, 먼거리 판을 LOD2 로 묶는다.
        ///
        /// 왜 두 단인가: 한 단만 두고 그것을 8% 로 깎았더니, 마당 건너에서는 멀쩡한데
        /// 처마 밑에 들어서면 서까래가 조각나 보였다. 한 장으로 가까운 데와 먼 데를
        /// 다 감당하려니 어느 쪽이든 틀리는 것이다.
        /// </summary>
        private static void Attach(MeshFilter mf, Mesh mid, Mesh far)
        {
            var go = mf.gameObject;
            var srcRenderer = mf.GetComponent<MeshRenderer>();

            var midR = MakeChild(go, srcRenderer, ChildName + "_중간", mid);
            var farR = MakeChild(go, srcRenderer, ChildName, far);

            var group = go.GetComponent<LODGroup>();
            if (group == null) group = go.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(MidAt, new Renderer[] { srcRenderer }),
                new LOD(FarAt, new Renderer[] { midR }),
                new LOD(CullAt, new Renderer[] { farR })
            });
            group.RecalculateBounds();
            EditorUtility.SetDirty(go);
        }

        /// <summary>줄인 메시를 든 자식 하나. 원본의 재질·그림자 설정을 그대로 물려받는다.</summary>
        private static Renderer MakeChild(GameObject go, MeshRenderer src, string name, Mesh mesh)
        {
            var child = go.transform.Find(name);
            if (child == null)
            {
                var c = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(c, "먼거리 판");
                c.transform.SetParent(go.transform, false);
                child = c.transform;
            }
            var childGo = child.gameObject;
            GameObjectUtility.SetStaticEditorFlags(childGo, GameObjectUtility.GetStaticEditorFlags(go));

            var cf = childGo.GetComponent<MeshFilter>();
            if (cf == null) cf = childGo.AddComponent<MeshFilter>();
            cf.sharedMesh = mesh;

            var cr = childGo.GetComponent<MeshRenderer>();
            if (cr == null) cr = childGo.AddComponent<MeshRenderer>();
            cr.sharedMaterials = src.sharedMaterials;
            cr.shadowCastingMode = src.shadowCastingMode;
            cr.receiveShadows = src.receiveShadows;
            cr.lightProbeUsage = src.lightProbeUsage;
            return cr;
        }
    }
}
