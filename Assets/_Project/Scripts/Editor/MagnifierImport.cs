using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 돋보기 FBX 가 들어올 때 <b>몸통과 술을 갈라서</b> 들인다.
    ///
    /// <b>왜 임포터에서 하나</b>: 갈라 놓은 메시를 따로 구워 두고 씬의 소품마다
    /// 갈아 끼우면, 소품이 늘 때마다 사람이 기억해서 물려야 한다. 하나 빠뜨리면
    /// 그 하나만 옛 살결로 남는다 — 문갑 위 돋보기가 실제로 그랬다.
    /// 갈라지는 것이 이 물건의 성질이면 <b>물건이 들어올 때</b> 갈라져 있어야 한다.
    /// 그래야 FBX 를 끌어다 놓기만 해도 제대로 된 것이 나온다.
    ///
    /// <b>무엇을 가르나</b>: 몸통은 놋쇠, 술은 비단이다. 한 재질로 칠하면 비단 술까지
    /// 놋쇠가 된다. 메시가 서브메시 하나뿐이라 나눌 데가 없어 보이지만, 나눌 근거는
    /// 이미 있다 — <b>술은 흔들리는 것</b>이다. 동작에서 움직이는 뼈에 매인 정점이
    /// 술이고, 가만있는 뼈에 매인 것이 몸통이다.
    ///
    /// 뼈 번호를 코드에 박지 않고 <see cref="OnPostprocessAnimation"/> 에서 클립을
    /// 읽어 적어 둔다. 소품을 다시 구워 와 뼈 차례가 바뀌어도, 흔들리는 것이 술이라는
    /// 사실은 변하지 않는다.
    ///
    /// <b>알베도는 안 쓴다</b>: 딸려 온 그림이 Meshy AI 가 구운 것인데 펼친 자리가
    /// 수천 조각으로 부서져 진흙빛 얼룩이 앉아 있다. 노멀맵만 살리고 빛깔은 값으로
    /// 준다(<c>M_돋보기_놋쇠</c> · <c>M_돋보기_술</c>).
    /// </summary>
    public class MagnifierImport : AssetPostprocessor
    {
        private const string Target = "Magnifier_Tassel.fbx";
        private const string Dir = "Assets/_Project/Art/Tools/Magnifier/";
        private const string BrassPath = Dir + "M_돋보기_놋쇠.mat";
        private const string SilkPath = Dir + "M_돋보기_술.mat";

        /// <summary>파일마다 "흔들리는 뼈" 이름. 동작을 먼저 읽고 모델에서 쓴다.</summary>
        private static readonly Dictionary<string, HashSet<string>> s_swing =
            new Dictionary<string, HashSet<string>>();

        private bool Mine => assetPath.EndsWith(Target, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 동작이 먼저 들어온다. 여기서 어느 뼈가 흔들리는지 적어 둔다 —
        /// 값이 처음부터 끝까지 한자리면 가만있는 뼈다.
        /// </summary>
        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!Mine || clip == null) return;

            HashSet<string> set;
            if (!s_swing.TryGetValue(assetPath, out set))
            {
                set = new HashSet<string>();
                s_swing[assetPath] = set;
            }

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
                set.Add(node);
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!Mine) return;

            HashSet<string> swing;
            if (!s_swing.TryGetValue(assetPath, out swing) || swing.Count == 0)
            {
                Debug.LogWarning("[돋보기] 흔들리는 뼈를 못 읽어 몸통과 술을 못 갈랐습니다. " +
                                 "한 번 더 다시 들이면(Reimport) 됩니다.", root);
                return;
            }

            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null) return;
            var mesh = smr.sharedMesh;
            if (mesh.subMeshCount >= 2) return;   // 이미 갈라져 있다

            // 뼈 번호 → 술인가
            var bones = smr.bones;
            var silkBone = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                silkBone[i] = bones[i] != null && swing.Contains(bones[i].name);

            var bw = mesh.boneWeights;
            var vertSilk = new bool[mesh.vertexCount];
            for (int i = 0; i < bw.Length; i++)
            {
                int b = bw[i].weight1 > bw[i].weight0 ? bw[i].boneIndex1 : bw[i].boneIndex0;
                vertSilk[i] = b >= 0 && b < silkBone.Length && silkBone[b];
            }

            var tris = mesh.triangles;
            var body = new List<int>();
            var silk = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                // 세 꼭짓점 가운데 둘 이상이 술이면 술이다
                int n = (vertSilk[tris[i]] ? 1 : 0) + (vertSilk[tris[i + 1]] ? 1 : 0) + (vertSilk[tris[i + 2]] ? 1 : 0);
                var into = n >= 2 ? silk : body;
                into.Add(tris[i]); into.Add(tris[i + 1]); into.Add(tris[i + 2]);
            }
            if (body.Count == 0 || silk.Count == 0)
            {
                Debug.LogWarning("[돋보기] 몸통과 술이 갈리지 않았습니다 — 그대로 들입니다.", root);
                return;
            }

            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(silk, 1);

            var brass = AssetDatabase.LoadAssetAtPath<Material>(BrassPath);
            var s = AssetDatabase.LoadAssetAtPath<Material>(SilkPath);
            if (brass == null || s == null)
            {
                Debug.LogWarning("[돋보기] 재질을 못 찾아 살결은 못 입혔습니다 — " +
                                 "[이문록 ▸ 돋보기 ▸ 살결 재질 만들기] 를 한 번 누르십시오.", root);
                return;
            }
            smr.sharedMaterials = new[] { brass, s };

            Debug.Log("[돋보기] 들이면서 갈랐습니다 — 몸통 " + body.Count / 3
                      + " · 술 " + silk.Count / 3 + " 삼각형.", root);
        }
    }
}
