using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 돋보기에 살결을 입힌다. 메뉴: [이문록 ▸ 돋보기 ▸ 살결 입히기]
    ///
    /// <b>무엇이 잘못돼 있었나</b>: 소품에 발린 알베도가 Meshy AI 가 구운
    /// 2048짜리 그림인데, 펼친 자리가 수천 조각으로 잘게 부서진 것이다. 조각마다
    /// 진흙빛 얼룩이 들어 있어, 그것을 바르면 돋보기가 통째로 거무튀튀한 덩이가 된다.
    /// UV 가 어긋난 것이 아니다 — <b>그림 자체가 쓸 것이 못 된다</b>. 눈앞 36cm 에
    /// 들고 보는 물건이라 그 얼룩이 그대로 눈에 든다.
    ///
    /// <b>그래서</b>: 알베도는 버리고 <b>노멀맵만 남긴다</b>. 노멀맵은 같은 자리에
    /// 구웠어도 쓸 만하다 — 새김과 마디의 굴곡이 제대로 박혀 있고, 조각난 자리는
    /// 굴곡이 없는 평평한 값이라 티가 안 난다. 빛깔은 값으로 준다.
    ///
    /// <b>몸통과 술을 가른다</b>: 한 재질로 칠하면 비단 술까지 놋쇠가 된다.
    /// 메시가 서브메시 하나뿐이라 나눌 데가 없어 보이지만, 나눌 근거가 이미 있다 —
    /// <b>술은 흔들리는 것</b>이다. Tassel_Lift 에서 움직이는 뼈(Bone_001~012)에
    /// 매인 정점이 술이고, 가만있는 뼈(Bone_000·013~019)에 매인 것이 몸통이다.
    /// 뼈 번호를 코드에 박지 않고 <b>클립을 읽어</b> 가른다 — 소품을 다시 구워 와도
    /// 흔들리는 것이 술이라는 사실은 변하지 않는다.
    ///
    /// 원본 FBX 는 건드리지 않는다. 서브메시만 둘로 나눈 새 메시를 옆에 굽고,
    /// 씬의 소품이 그것을 물게 한다. 되돌리려면 소품의 메시를 FBX 것으로 되돌리면 된다.
    /// </summary>
    public static class MagnifierSkin
    {
        private const string Dir = "Assets/_Project/Art/Tools/Magnifier/";
        private const string FbxPath = Dir + "Magnifier_Tassel.fbx";
        private const string OutMesh = Dir + "돋보기_몸통술_Mesh.asset";
        private const string BrassPath = Dir + "M_돋보기_놋쇠.mat";
        private const string SilkPath = Dir + "M_돋보기_술.mat";
        private const string NormalPath = Dir + "Meshy_AI_Antique_Magnifying_Gl_0811194209_texture_normal.png";

        private const string ToolId = "magnify";

        [MenuItem("이문록/돋보기/살결 입히기")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[돋보기] 플레이를 멈추고 다시 실행하세요.");
                return;
            }

            var log = new StringBuilder();

            var swing = SwingingBones(log);
            if (swing == null) return;

            var baked = Split(swing, log);
            if (baked == null) return;

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(OutMesh);
            if (existing != null) { EditorUtility.CopySerialized(baked, existing); Object.DestroyImmediate(baked); baked = existing; }
            else AssetDatabase.CreateAsset(baked, OutMesh);

            var brass = MakeMaterial(BrassPath, "M_돋보기_놋쇠",
                                     new Color(0.50f, 0.37f, 0.18f), 0.85f, 0.42f);
            var silk = MakeMaterial(SilkPath, "M_돋보기_술",
                                    new Color(0.40f, 0.11f, 0.10f), 0f, 0.22f);
            AssetDatabase.SaveAssets();

            Wire(baked, brass, silk, log);
            Debug.Log("[돋보기] 살결을 입혔습니다.\n" + log);
        }

        /// <summary>
        /// 흔들리는 뼈를 클립에서 읽는다. 값이 처음부터 끝까지 한자리면 가만있는 뼈다.
        /// </summary>
        private static HashSet<string> SwingingBones(StringBuilder log)
        {
            AnimationClip clip = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) clip = c;

            if (clip == null) { Debug.LogError("[돋보기] " + FbxPath + " 안에서 동작을 못 찾았습니다."); return null; }

            var swing = new HashSet<string>();
            foreach (var bind in AnimationUtility.GetCurveBindings(clip))
            {
                var cur = AnimationUtility.GetEditorCurve(clip, bind);
                if (cur == null || cur.keys.Length < 2) continue;
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var k in cur.keys) { if (k.value < lo) lo = k.value; if (k.value > hi) hi = k.value; }
                if (hi - lo < 1e-4f) continue;

                string node = bind.path;
                int slash = node.LastIndexOf('/');
                if (slash >= 0) node = node.Substring(slash + 1);
                swing.Add(node);
            }

            log.AppendLine("   흔들리는 뼈 " + swing.Count + "개 = 술");
            return swing.Count > 0 ? swing : null;
        }

        /// <summary>서브메시를 몸통(0)과 술(1)로 가른다. 정점은 그대로 둔다.</summary>
        private static Mesh Split(HashSet<string> swing, StringBuilder log)
        {
            Mesh src = null;
            SkinnedMeshRenderer smr = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (o is Mesh m && m.vertexCount > 100) src = m;
                if (o is GameObject g)
                {
                    var s = g.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (s != null) smr = s;
                }
            }
            if (src == null || smr == null) { Debug.LogError("[돋보기] 소품 메시를 못 찾았습니다."); return null; }

            // 뼈 번호 → 술인가
            var bones = smr.bones;
            var isSilk = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                isSilk[i] = bones[i] != null && swing.Contains(bones[i].name);

            var bw = src.boneWeights;
            var vertSilk = new bool[src.vertexCount];
            for (int i = 0; i < bw.Length; i++)
            {
                int b = bw[i].boneIndex0;
                if (bw[i].weight1 > bw[i].weight0) b = bw[i].boneIndex1;
                vertSilk[i] = b >= 0 && b < isSilk.Length && isSilk[b];
            }

            var tris = src.triangles;
            var body = new List<int>();
            var silk = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                // 세 꼭짓점 가운데 둘 이상이 술이면 술이다
                int n = (vertSilk[tris[i]] ? 1 : 0) + (vertSilk[tris[i + 1]] ? 1 : 0) + (vertSilk[tris[i + 2]] ? 1 : 0);
                var into = n >= 2 ? silk : body;
                into.Add(tris[i]); into.Add(tris[i + 1]); into.Add(tris[i + 2]);
            }
            if (silk.Count == 0 || body.Count == 0)
            {
                Debug.LogError("[돋보기] 몸통과 술이 갈리지 않았습니다(몸통 " + body.Count / 3 + " · 술 " + silk.Count / 3 + ").");
                return null;
            }

            var mesh = Object.Instantiate(src);
            mesh.name = "돋보기_몸통술_Mesh";
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(silk, 1);
            mesh.RecalculateBounds();

            log.AppendLine("   몸통 " + body.Count / 3 + " 삼각형 · 술 " + silk.Count / 3 + " 삼각형");
            return mesh;
        }

        /// <summary>
        /// 빛깔은 값으로, 굴곡은 노멀맵으로. 이미 있으면 손대지 않는다 —
        /// 눈으로 맞춰 둔 값을 도구가 도로 밀어 버리면 만질 수가 없다.
        /// </summary>
        private static Material MakeMaterial(string path, string name, Color color,
                                             float metallic, float smoothness)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return null;
            m = new Material(sh) { name = name };
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);

            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            if (n != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", n);
                m.EnableKeyword("_NORMALMAP");
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>열려 있는 씬의 돋보기 소품에 새 메시와 재질 둘을 물린다.</summary>
        private static void Wire(Mesh mesh, Material brass, Material silk, StringBuilder log)
        {
            int n = 0;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!sc.isLoaded) continue;
                foreach (var root in sc.GetRootGameObjects())
                    foreach (var h in root.GetComponentsInChildren<HeldToolModel>(true))
                    {
                        if (h.ToolId != ToolId) continue;
                        foreach (var r in h.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            if (r.sharedMesh == null || r.sharedMesh.vertexCount != mesh.vertexCount) continue;
                            Undo.RecordObject(r, "돋보기 살결");
                            r.sharedMesh = mesh;
                            r.sharedMaterials = new[] { brass, silk };
                            EditorUtility.SetDirty(r);
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
                            log.AppendLine("   " + sc.name + " / " + r.name + " 에 물렸습니다");
                            n++;
                        }
                    }
            }
            if (n == 0)
                log.AppendLine("   ※ 열린 씬에 돋보기 소품이 없습니다. Onggojip.unity 를 열고 다시 누르십시오.");
        }
    }
}
