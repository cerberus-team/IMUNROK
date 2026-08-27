using System.IO;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 두루마리 프리팹을 만든다 — 윗축(말린 뭉치)·종이·아랫축.
    ///
    /// 왜 손으로 안 만드는가: 종이는 Quad 하나인데 중심점이 가운데라 위쪽 끝을 원점에
    /// 맞춰 두어야 하고, 축 둘은 눕힌 원기둥이라 회전을 매번 다시 잡아야 한다.
    /// 한 번 짜 두면 사건이 늘어도 [이문록 ▸ 두루마리 만들기] 한 번이면 된다.
    ///
    /// 그림(사건 문서)은 여기서 넣지 않는다. <see cref="ScrollUnroll.SetDocument"/> 로
    /// 사건마다 갈아 끼우는 것이 이 물건의 요점이다.
    ///
    /// 메뉴: [이문록 ▸ 두루마리 만들기]
    /// </summary>
    public static class ScrollBuilder
    {
        // _Common/Art 아래는 .gitignore 로 빠진다(아트는 공유폴더로 돈다). 두루마리는
        // 아트가 아니라 씬이 참조하는 뼈대라, 빠지면 팀원이 씬을 열었을 때 빈 자리가 된다.
        // 그래서 커밋되는 곳에 둔다.
        private const string PrefabDir = "Assets/_Project/_Common/Prefabs";
        private const string PrefabPath = PrefabDir + "/두루마리.prefab";

        [MenuItem("이문록/두루마리 만들기")]
        public static void Build()
        {
            var root = new GameObject("두루마리");

            // ── 윗축: 말린 뭉치. 원기둥은 세로로 서 있으므로 눕힌다.
            var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            top.name = "윗축";
            top.transform.SetParent(root.transform, false);
            top.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // 원기둥의 기본 길이는 2다(반높이 1). 종이 폭보다 조금 길게 잡아 축이 삐져나오게.
            top.transform.localScale = new Vector3(0.055f, 0.235f, 0.055f);
            StripCollider(top);
            Paint(top, new Color(0.34f, 0.25f, 0.17f), "두루마리_축");

            // ── 종이: 아래로 자란다. 배율을 코드가 잡으므로 여기선 0 에 가깝게 접어 둔다.
            var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
            paper.name = "종이";
            paper.transform.SetParent(root.transform, false);
            // 유니티 Quad 의 앞면은 -Z 를 본다. 그대로 두면 두루마리를 정면에서 봤을 때
            // 뒷면이라 잘려 사라진다. 돌려서 두루마리의 앞(+Z)과 종이의 앞을 맞춘다.
            paper.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            paper.transform.localScale = new Vector3(0.42f, 0.0001f, 1f);
            var pc = paper.GetComponent<BoxCollider>();
            if (pc == null) pc = paper.AddComponent<BoxCollider>();
            pc.size = new Vector3(1f, 1f, 0.02f);   // 판의 배율이 곱해지므로 1×1 로 둔다
            Paint(paper, new Color(0.86f, 0.82f, 0.71f), "두루마리_종이");

            // ── 아랫축: 종이 끝을 누른다.
            var bottom = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bottom.name = "아랫축";
            bottom.transform.SetParent(root.transform, false);
            bottom.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            bottom.transform.localScale = new Vector3(0.028f, 0.225f, 0.028f);
            StripCollider(bottom);
            Paint(bottom, new Color(0.28f, 0.20f, 0.13f), "두루마리_아랫축");

            var scroll = root.AddComponent<ScrollUnroll>();
            var so = new SerializedObject(scroll);
            so.FindProperty("_topRod").objectReferenceValue = top.transform;
            so.FindProperty("_paper").objectReferenceValue = paper.transform;
            so.FindProperty("_bottomRod").objectReferenceValue = bottom.transform;
            so.FindProperty("_paperRenderer").objectReferenceValue = paper.GetComponent<Renderer>();
            so.FindProperty("_paperCollider").objectReferenceValue = pc;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(PrefabDir);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
            Debug.Log($"[두루마리] 만들었습니다 → {PrefabPath}\n" +
                      "사건 문서는 ScrollUnroll.SetDocument(텍스처) 로 갈아 끼웁니다.");
        }

        /// <summary>축은 손댈 일이 없다 — 콜라이더가 있으면 종이보다 먼저 집혀 방해만 된다.</summary>
        private static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }

        /// <summary>
        /// URP/Lit 재질을 만들어 끼운다. 기본 재질을 그대로 두면 세 조각이 한 재질을
        /// 공유해, 종이 그림을 갈아 끼울 때 축까지 같이 물들 수 있다.
        /// </summary>
        private static void Paint(GameObject go, Color color, string matName)
        {
            EnsureFolder(PrefabDir);
            string path = PrefabDir + "/" + matName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(mat);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void EnsureFolder(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            string leaf = Path.GetFileName(dir);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
