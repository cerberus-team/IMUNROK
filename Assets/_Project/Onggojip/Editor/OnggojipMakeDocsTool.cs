using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 사건 문서 소품(별급문기·속량문서·차용증 등)을 "얇은 한지 평면"으로 만들어
    /// 해당 단서 마커 옆에 놓는다. 나중에 한지+한자 텍스처만 입히면 됨.
    /// 메뉴: [이문록 ▸ 옹고집: 문서 소품 만들기].
    /// </summary>
    public static class OnggojipMakeDocsTool
    {
        private struct Doc { public string name; public string anchor; public Vector3 offset; public Color color; }

        [MenuItem("이문록/옹고집: 문서 소품 만들기")]
        public static void Make()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var hanji = new Color(0.90f, 0.85f, 0.72f);   // 한지색
            var burnt = new Color(0.25f, 0.22f, 0.20f);   // 탄 종이

            var docs = new[]
            {
                new Doc{ name="별급문기(찢긴조각)", anchor="문갑",   offset=new Vector3(0,0.3f,0), color=hanji },
                new Doc{ name="속량문서",          anchor="행랑궤", offset=new Vector3(0,0.3f,0), color=hanji },
                new Doc{ name="탄서찰조각",        anchor="아궁이", offset=new Vector3(0,0.1f,0), color=burnt },
                new Doc{ name="차용증",            anchor="장부",   offset=new Vector3(0.25f,0.05f,0), color=hanji },
                new Doc{ name="물목장부",          anchor="장부",   offset=new Vector3(0,0.05f,0.25f), color=hanji },
                new Doc{ name="미회수증서",        anchor="장부",   offset=new Vector3(-0.25f,0.05f,0), color=hanji },
            };

            var group = new GameObject("문서(소품)");
            int made = 0;
            var noAnchor = new System.Text.StringBuilder();

            foreach (var d in docs)
            {
                var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
                paper.name = d.name;
                paper.transform.SetParent(group.transform, false);
                paper.transform.localScale = new Vector3(0.28f, 0.01f, 0.4f); // 종이 두께

                var anchor = Find(scene, d.anchor);
                if (anchor != null)
                    paper.transform.position = anchor.transform.position + d.offset;
                else
                {
                    paper.transform.position = d.offset;
                    noAnchor.AppendLine("· " + d.name + " (마커 '" + d.anchor + "' 못 찾음)");
                }

                var r = paper.GetComponent<Renderer>();
                var b = new MaterialPropertyBlock();
                r.GetPropertyBlock(b); b.SetColor("_BaseColor", d.color); r.SetPropertyBlock(b);
                made++;
            }

            Selection.activeGameObject = group;
            EditorSceneManager.MarkSceneDirty(scene);
            string msg = $"문서 소품 {made}개 생성('문서(소품)' 그룹).\n한지색 평면이라 나중에 한자 텍스처만 입히면 됨.";
            if (noAnchor.Length > 0) msg += "\n\n마커 못 찾아 원점 근처에 둔 것:\n" + noAnchor;
            EditorUtility.DisplayDialog("이문록", msg, "확인");
        }

        private static GameObject Find(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    if (tr.name == name) return tr.gameObject;
            return null;
        }
    }
}
