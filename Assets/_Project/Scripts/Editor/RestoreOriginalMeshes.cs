using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 깎여 버린 원본을 되살린다. 메뉴: [이문록 ▸ 에셋: 깎여 버린 원본 되살리기]
    ///
    /// <b>무슨 일이 있었나</b>: <see cref="MeshDecimator"/> 는 씬의 MeshFilter 를 깎은 메시로
    /// <b>갈아 끼운다</b> — 판을 얹는 것이 아니라 원본을 치운다. 그래서 한 번 돌리고 나면
    /// 가까이 다가가도 원본이 돌아오지 않는다. 고택에는 그렇게 당한 자리가 서른여덟 군데
    /// 있었고(담장 열여덟·담 열하나·굴뚝 넷·기단 셋·박석 하나 등), 마당을 걷다 보면
    /// 지붕이 납작한 판때기가 되어 있거나 헛간이 조각나 보인다.
    ///
    /// 다행히 그 도구는 <b>원본 FBX 를 건드리지 않았다</b>. 원본이 그대로 있으니 되돌릴 수 있다.
    ///
    /// <b>어떻게 되돌리나</b>: 깎인 메시 이름은 늘 "원본이름_간소" 꼴이다. 뒤를 떼어 낸
    /// 이름으로 FBX 를 찾아 그 안의 메시를 도로 물린다. 그러고 나서 <b>방금 밀려난 깎은
    /// 메시를 먼거리 판(LOD1)으로 내린다</b> — 버리지 않는다. 가까이서는 원본, 멀리서는
    /// 지금까지 보던 그 메시가 나오므로, 눈에 띄는 깨짐만 사라지고 먼 데 값은 그대로다.
    ///
    /// 이미 먼거리 판이 붙어 있던 자리는 그 판을 "원본_간소_간소"(두 번 깎은 것)에서
    /// "원본_간소"(한 번 깎은 것)로 바꿔 단다. 두 번 깎은 쪽은 쓸 데가 없어진다.
    ///
    /// ★되돌리기(Ctrl+Z)가 듣는다. 씬만 되돌리면 원래대로다.
    /// </summary>
    public static class RestoreOriginalMeshes
    {
        private const string Suffix = "_간소";
        private const string FarChild = "_먼거리";

        /// <summary>원본을 유지할 화면 높이 비율. HeavyMeshLod 와 같은 값을 쓴다.</summary>
        private const float SwapAt = 1.0f;

        /// <summary>이보다 작아 보이면 안 그린다. HeavyMeshLod 와 같은 값.</summary>
        private const float CullAt = 0.006f;

        [MenuItem("이문록/에셋: 깎여 버린 원본 되살리기")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[되살리기] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var work = new List<MeshFilter>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.gameObject.name == FarChild) continue;             // 판은 원래 깎은 것이라 정상
                if (!mf.sharedMesh.name.EndsWith(Suffix)) continue;
                if (mf.GetComponent<MeshRenderer>() == null) continue;
                work.Add(mf);
            }

            if (work.Count == 0)
            {
                Debug.Log("[되살리기] 깎인 메시를 쓰는 자리가 없습니다.");
                return;
            }

            var log = new StringBuilder();
            long before = 0, after = 0;
            int done = 0, missing = 0;

            try
            {
                for (int i = 0; i < work.Count; i++)
                {
                    var mf = work[i];
                    EditorUtility.DisplayProgressBar("깎여 버린 원본 되살리기",
                        mf.name + " (" + (i + 1) + "/" + work.Count + ")", (float)(i + 1) / work.Count);

                    var cut = mf.sharedMesh;
                    string origName = cut.name.Substring(0, cut.name.Length - Suffix.Length);
                    var orig = FindOriginal(origName);
                    if (orig == null)
                    {
                        missing++;
                        log.AppendLine("   ✘ " + origName + " : 원본을 못 찾았습니다");
                        continue;
                    }

                    int cutTri = cut.triangles.Length / 3;
                    int origTri = orig.triangles.Length / 3;
                    before += cutTri;
                    after += origTri;

                    Undo.RecordObject(mf, "원본 되살리기");
                    mf.sharedMesh = orig;
                    EditorUtility.SetDirty(mf);

                    Demote(mf, cut);
                    done++;
                    log.AppendLine("   " + origName + " : " + cutTri.ToString("N0") + " → " + origTri.ToString("N0")
                                   + " (밀려난 것은 먼거리 판으로)");
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[되살리기] " + done + "자리를 원본으로 되돌렸습니다"
                      + (missing > 0 ? " (못 찾은 것 " + missing + ")" : "")
                      + " / 가까이서 " + before.ToString("N0") + " → " + after.ToString("N0") + " 삼각형\n" + log);
        }

        /// <summary>
        /// 이름으로 원본 메시를 찾는다. 깎은 메시는 원본 FBX 옆 _간소화메시 폴더에 굽히므로,
        /// 그 위 폴더에서 같은 이름의 FBX 를 먼저 본다 — 이름이 겹치는 메시가 프로젝트
        /// 곳곳에 있어도 엉뚱한 것을 물지 않는다.
        /// </summary>
        private static Mesh FindOriginal(string origName)
        {
            foreach (var guid in AssetDatabase.FindAssets(origName + " t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/" + origName + ".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                Mesh first = null;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(o is Mesh m)) continue;
                    if (m.name == origName) return m;
                    if (first == null) first = m;
                }
                if (first != null) return first;
            }
            return null;
        }

        /// <summary>
        /// 밀려난 메시를 먼거리 판으로 내려 단다. 이미 판이 있으면 그 메시만 바꿔 끼운다
        /// (두 번 깎은 것보다 한 번 깎은 것이 낫다).
        /// </summary>
        private static void Demote(MeshFilter mf, Mesh cut)
        {
            var go = mf.gameObject;
            var srcR = go.GetComponent<MeshRenderer>();

            var child = go.transform.Find(FarChild);
            if (child == null)
            {
                var c = new GameObject(FarChild);
                Undo.RegisterCreatedObjectUndo(c, "먼거리 판");
                c.transform.SetParent(go.transform, false);
                child = c.transform;
            }
            var childGo = child.gameObject;
            GameObjectUtility.SetStaticEditorFlags(childGo, GameObjectUtility.GetStaticEditorFlags(go));

            var cf = childGo.GetComponent<MeshFilter>();
            if (cf == null) cf = childGo.AddComponent<MeshFilter>();
            cf.sharedMesh = cut;

            var cr = childGo.GetComponent<MeshRenderer>();
            if (cr == null) cr = childGo.AddComponent<MeshRenderer>();
            cr.sharedMaterials = srcR.sharedMaterials;
            cr.shadowCastingMode = srcR.shadowCastingMode;
            cr.receiveShadows = srcR.receiveShadows;
            cr.lightProbeUsage = srcR.lightProbeUsage;

            var group = go.GetComponent<LODGroup>();
            if (group == null) group = go.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(SwapAt, new Renderer[] { srcR }),
                new LOD(CullAt, new Renderer[] { cr })
            });
            group.RecalculateBounds();
            EditorUtility.SetDirty(go);
        }
    }
}
