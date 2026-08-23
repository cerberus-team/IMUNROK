using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// 받아 온 자연물 꾸러미의 재질을 <b>URP 로 옮긴다</b>.
    /// 메뉴: [이문록 ▸ 에셋: Fristy 재질을 URP 로]
    ///
    /// <b>왜 필요한가</b>: 들판을 깔고 보니 나무도 바위도 풀도 전부 자홍색이었다.
    /// 꾸러미가 파이프라인마다 셰이더를 따로 싣고 오는데(Fristy/HDRP/… ·
    /// Fristy/Nature/… · Standard), 정작 재질이 물고 있는 것은 HDRP 와 빌트인 쪽이다.
    /// 이 프로젝트는 URP 라 그 셰이더들이 아예 컴파일되지 않고, 유니티는 그럴 때
    /// 자홍색을 칠한다. 재질이 깨진 것이 아니라 <b>다른 집 열쇠</b>인 것이다.
    ///
    /// <b>무엇을 옮기나</b>: 그림은 그대로 쓴다. 셰이더만 URP/Lit 으로 갈아 끼우고
    /// 슬롯 이름을 맞춰 준다 — 빌트인의 <c>_MainTex</c> 가 URP 에서는 <c>_BaseMap</c> 이고,
    /// 이 꾸러미는 노멀을 <c>_MainNormal</c> 이라는 제 이름으로 들고 있다.
    ///
    /// <b>잎은 뚫어 준다</b>: 나뭇잎·풀은 네모난 판에 그림을 얹고 <b>알파로 오려 낸</b>
    /// 것이다. 그냥 불투명으로 두면 잎이 아니라 판때기가 매달린다. 이름에 잎·풀이
    /// 든 것은 알파 컷아웃을 켜고, 뒷면도 그리게 한다(잎은 뒤에서도 보인다).
    ///
    /// ★한 번만 돌리면 된다. 이미 URP 인 재질은 건드리지 않는다.
    ///  꾸러미는 아트라 git 밖이므로, 새로 받은 사람은 이 메뉴를 한 번 눌러야 한다.
    /// </summary>
    public static class FristyToUrp
    {
        private static readonly string[] Roots =
        {
            "Assets/Fristy stylize Modular Assets 2",
            "Assets/Fristy Mobile CG",
        };

        /// <summary>이름에 이것이 들면 알파로 오려 낸 잎으로 본다.</summary>
        private static readonly string[] FoliageWords =
        { "leaf", "leaves", "foliage", "grass", "plant", "weed", "ivy", "veg", "bush", "tree" };

        [MenuItem("이문록/에셋: Fristy 재질을 URP 로")]
        private static void Run()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) { Debug.LogError("[Fristy] URP Lit 셰이더를 못 찾았습니다."); return; }

            var log = new StringBuilder();
            int moved = 0, kept = 0;

            foreach (var root in Roots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { root }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (m == null || m.shader == null) continue;

                    // 이미 URP 면 그대로 둔다. 눈으로 맞춰 둔 값을 밀지 않는다.
                    if (m.shader.name.StartsWith("Universal Render Pipeline/")) { kept++; continue; }

                    var albedo = FindTex(m, new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" });
                    var normal = FindTex(m, new[] { "_MainNormal", "_BumpMap", "_NormalMap", "_Normal" });
                    var color = m.HasProperty("_Color") ? m.GetColor("_Color")
                              : m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;

                    // 그림 슬롯 이름이 죄다 제각각이면(Texture2D_5EA200BF 같은 것) 이름으로는
                    // 못 찾는다. 그때는 재질이 들고 있는 그림 아무거나 하나를 albedo 로 본다.
                    if (albedo == null) albedo = AnyTex(m);

                    m.shader = lit;
                    if (albedo != null) m.SetTexture("_BaseMap", albedo);

                    // 밑빛의 <b>알파는 버린다</b>. 이 꾸러미의 잎 재질은 옛 셰이더에서
                    // 알파를 다른 뜻으로 쓰고 있어서 0 이 들어 있는데, URP 는 그것을
                    // 그림 알파에 곱한다 — 0 을 곱하니 잎이 통째로 잘려 나갔다.
                    // 나무 줄기만 남고 잎이 사라진 까닭이 이것이었다. 여기서 만드는 것은
                    // 죄다 불투명이거나 오려 낸 것이라, 밑빛 알파는 늘 1 이어야 한다.
                    color.a = 1f;
                    m.SetColor("_BaseColor", color);
                    if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.10f);
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);

                    if (IsFoliage(m.name) || IsFoliage(path))
                    {
                        // 잎은 알파로 오려 낸 그림이다. 컷아웃을 켜고 뒷면도 그린다.
                        m.SetFloat("_AlphaClip", 1f);
                        m.SetFloat("_Cutoff", 0.42f);
                        m.EnableKeyword("_ALPHATEST_ON");
                        m.SetOverrideTag("RenderType", "TransparentCutout");
                        m.renderQueue = 2450;
                        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // 양면
                    }

                    EditorUtility.SetDirty(m);
                    moved++;
                    log.AppendLine("   " + m.name + "  ← " + (albedo == null ? "그림 없음" : albedo.name)
                                   + (normal != null ? " + 노멀" : ""));
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Fristy] URP 로 옮긴 재질 " + moved + "개 (이미 URP 라 둔 것 " + kept + ")\n" + log);
        }

        private static bool IsFoliage(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.ToLowerInvariant();
            foreach (var w in FoliageWords) if (s.Contains(w)) return true;
            return false;
        }

        private static Texture FindTex(Material m, string[] names)
        {
            foreach (var n in names)
                if (m.HasProperty(n))
                {
                    var t = m.GetTexture(n);
                    if (t != null) return t;
                }
            return null;
        }

        /// <summary>이름을 못 알아볼 때 — 들고 있는 그림 가운데 노멀이 아닌 첫 장.</summary>
        private static Texture AnyTex(Material m)
        {
            var so = new SerializedObject(m);
            var te = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (te == null) return null;
            for (int i = 0; i < te.arraySize; i++)
            {
                var e = te.GetArrayElementAtIndex(i);
                var t = e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                if (t == null) continue;
                var n = t.name.ToLowerInvariant();
                if (n.Contains("normal") || n.Contains("_nm") || n.Contains("bump")) continue;
                return t;
            }
            return null;
        }
    }
}
