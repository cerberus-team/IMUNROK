using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 돋보기 살결 재질 둘을 만든다. 메뉴: [이문록 ▸ 돋보기 ▸ 살결 재질 만들기]
    ///
    /// 가르는 일은 <see cref="MagnifierImport"/> 가 FBX 를 들일 때 한다. 여기서는
    /// 그때 물릴 재질만 마련한다 — 임포터가 도는 중에 에셋을 새로 만들면 다시
    /// 들이기가 겹쳐 돌아 탈이 나므로, 만드는 쪽은 사람이 부르는 자리에 둔다.
    ///
    /// <b>알베도는 쓰지 않는다</b>: 딸려 온 그림이 Meshy AI 가 구운 것인데 펼친
    /// 자리가 수천 조각으로 부서져 조각마다 진흙빛 얼룩이 들어 있다. 그것을 바르면
    /// 돋보기가 통째로 거무튀튀한 덩이가 된다 — UV 가 어긋난 것이 아니라 그림
    /// 자체가 쓸 것이 못 된다.
    ///
    /// 그래서 <b>노멀맵만 남기고</b> 빛깔은 값으로 준다. 노멀맵은 같은 자리에
    /// 구웠어도 쓸 만하다 — 새김과 마디의 굴곡이 제대로 박혀 있고, 조각난 자리는
    /// 굴곡이 없는 평평한 값이라 티가 안 난다.
    ///
    /// ★<b>이미 있는 재질은 건드리지 않는다.</b> 눈으로 맞춰 둔 값을 도구가 도로
    ///  밀어 버리면 만질 수가 없다. 다시 만들고 싶으면 재질을 지우고 누르면 된다.
    /// </summary>
    public static class MagnifierSkin
    {
        private const string Dir = "Assets/_Project/Art/Tools/Magnifier/";
        private const string BrassPath = Dir + "M_돋보기_놋쇠.mat";
        private const string SilkPath = Dir + "M_돋보기_술.mat";
        private const string NormalPath = Dir + "Meshy_AI_Antique_Magnifying_Gl_0811194209_texture_normal.png";

        [MenuItem("이문록/돋보기/살결 재질 만들기")]
        private static void Run()
        {
            // 놋쇠 — 손때 앉은 오래된 놋. 갓 닦아 낸 금빛이면 눈앞에서 번쩍여
            // 그 옆의 술이 묻힌다.
            var brass = Make(BrassPath, "M_돋보기_놋쇠", new Color(0.26f, 0.21f, 0.14f), 0.60f, 0.26f);

            // 술 — 붉은빛만 살린다. 비단은 결을 따라 윤이 나므로 조금 매끄럽게.
            var silk = Make(SilkPath, "M_돋보기_술", new Color(0.66f, 0.11f, 0.10f), 0f, 0.34f);

            AssetDatabase.SaveAssets();
            Debug.Log("[돋보기] 살결 재질: " + (brass != null ? brass.name : "✘")
                      + " · " + (silk != null ? silk.name : "✘")
                      + "\n이제 FBX 를 다시 들이면(Reimport) 몸통과 술이 갈려 이 둘을 뭅니다.");
        }

        private static Material Make(string path, string name, Color color, float metallic, float smoothness)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;   // 있으면 그대로 둔다

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) { Debug.LogError("[돋보기] URP Lit 셰이더를 못 찾았습니다."); return null; }

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
    }
}
