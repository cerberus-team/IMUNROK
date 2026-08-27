using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// glb 소품이 지고 들어온 텍스처를 뽑아 줄이고 압축해, 쓸 만한 재질로 바꿔 끼운다.
    ///
    /// 왜 필요한가: glb 안에 박혀 들어온 텍스처는 <b>임포터 설정이 없다</b>.
    /// glTFast 임포터가 주는 것은 밉맵·필터·읽기가능뿐이고 압축도 최대 크기도 없다.
    /// 그래서 4096짜리가 ARGB32 생 데이터로 그대로 올라간다 — 붓 한 자루가 256MB였다.
    /// 이 상태로는 로딩이 길고 퀘스트에는 아예 못 올린다.
    ///
    /// 무엇을 하나:
    ///   ① 씬에서 실제로 쓰는 glb 재질만 골라
    ///   ② 베이스컬러·노멀을 뽑아 MaxSize 이하로 줄이고 압축해 png 로 저장
    ///   ③ 금속·거칠기 맵은 버리고 값(상수)으로 대체 — 소품에서 그 맵은 대개 아무도 못 알아본다
    ///   ④ URP/Lit 재질을 만들어 씬의 렌더러에 갈아 끼운다
    ///
    /// 메시와 프리팹은 건드리지 않는다. 문제는 텍스처뿐이라 손대는 범위를 좁게 잡았다.
    ///
    /// 결과 png 는 아트라 git 에 올리지 않는다(.gitignore). 대신 .meta 는 커밋하므로
    /// 팀원이 이 메뉴를 다시 돌려도 GUID 가 유지돼 재질 연결이 안 끊긴다
    /// (사건 문서 텍스처와 같은 방식).
    ///
    /// 메뉴: [이문록 ▸ 에셋: glb 텍스처 정리]
    /// </summary>
    public static class GlbTextureBaker
    {
        /// <summary>뽑아낸 텍스처의 최대 한 변(px).</summary>
        private const int MaxSize = 1024;

        /// <summary>구운 것을 넣을 폴더 이름(원본 glb 옆에 만든다).</summary>
        private const string OutFolder = "_구운텍스처";

        /// <summary>메뉴로 부를 때만 확인 창을 띄운다(스크립트로 부를 땐 콘솔 로그만).</summary>
        private static bool _showDialog;

        [MenuItem("이문록/에셋: glb 텍스처 정리")]
        private static void BakeFromMenu() { _showDialog = true; try { Bake(); } finally { _showDialog = false; } }

        public static void Bake()
        {
            // 플레이 중에 돌리면 재질을 갈아 끼워도 정지하는 순간 되돌아간다(씬 변경이 버려진다).
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("이문록",
                    "플레이를 멈추고 다시 실행하세요.\n\n재생 중에 바꾼 씬은 정지할 때 되돌아갑니다.", "확인");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var targets = CollectSceneGlbMaterials();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("이문록", "이 씬에서 쓰는 glb 재질이 없습니다.", "확인");
                return;
            }

            long before = MeasureSceneTextures();
            var remap = new Dictionary<Material, Material>();

            try
            {
                int i = 0;
                foreach (var src in targets)
                {
                    EditorUtility.DisplayProgressBar("glb 텍스처 정리",
                        src.name + " (" + (++i) + "/" + targets.Count + ")", (float)i / targets.Count);
                    var baked = BakeMaterial(src);
                    if (baked != null) remap[src] = baked;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            int swapped = SwapInScene(remap);
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            long after = MeasureSceneTextures();
            string msg =
                "glb 재질 " + remap.Count + "개를 구워 렌더러 " + swapped + "개에 갈아 끼웠습니다.\n\n" +
                "씬 텍스처 " + (before / 1048576) + " MB → " + (after / 1048576) + " MB\n\n" +
                "결과 png 는 git 에 올라가지 않습니다(공유폴더 배포).";
            // 결과는 콘솔에 먼저 남긴다. 확인 창은 모달이라 누르기 전까지 에디터가 멈추는데,
            // 자동화(MCP)로 돌릴 때는 그것이 통째로 멈춘 것처럼 보인다.
            Debug.Log("[glb정리] " + msg.Replace("\n\n", " / "));
            if (!Application.isBatchMode && _showDialog)
                EditorUtility.DisplayDialog("이문록", msg, "확인");
        }

        /// <summary>씬에서 실제로 쓰는 재질 중, glb 안에 들어있는 것만.</summary>
        private static HashSet<Material> CollectSceneGlbMaterials()
        {
            var set = new HashSet<Material>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    string p = AssetDatabase.GetAssetPath(m);
                    if (!p.EndsWith(".glb") && !p.EndsWith(".gltf")) continue;   // glb 안에 든 재질만
                    set.Add(m);
                }
            return set;
        }

        /// <summary>재질 하나를 구워 URP/Lit 재질로 만든다. 이미 있으면 그것을 다시 채운다.</summary>
        private static Material BakeMaterial(Material src)
        {
            string glbPath = AssetDatabase.GetAssetPath(src);
            string dir = Path.GetDirectoryName(glbPath).Replace('\\', '/');
            string outDir = dir + "/" + OutFolder;
            if (!AssetDatabase.IsValidFolder(outDir)) AssetDatabase.CreateFolder(dir, OutFolder);

            string stem = Sanitize(Path.GetFileNameWithoutExtension(glbPath) + "_" + src.name);

            Texture2D color = null, normal = null;
            foreach (string prop in src.GetTexturePropertyNames())
            {
                var tex = src.GetTexture(prop) as Texture2D;
                if (tex == null) continue;
                string kind = Classify(prop, tex.name);
                if (kind == "color" && color == null) color = tex;
                else if (kind == "normal" && normal == null) normal = tex;
            }
            if (color == null && normal == null) return null;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            if (color != null)
            {
                var t = Extract(color, outDir + "/T_Glb_" + stem + "_BaseColor.png", false);
                if (t != null) mat.SetTexture("_BaseMap", t);
            }
            if (normal != null)
            {
                var t = Extract(normal, outDir + "/T_Glb_" + stem + "_Normal.png", true);
                if (t != null) { mat.SetTexture("_BumpMap", t); mat.EnableKeyword("_NORMALMAP"); }
            }

            // 금속·거칠기는 맵 대신 값으로. 원본 재질에 계수가 있으면 그것을 따른다.
            float metallic = GetFloat(src, new[] { "metallicFactor", "_Metallic", "_MetallicFactor" }, 0f);
            float rough = GetFloat(src, new[] { "roughnessFactor", "_Roughness", "_RoughnessFactor" }, 0.75f);
            mat.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            mat.SetFloat("_Smoothness", Mathf.Clamp01(1f - rough));
            mat.SetColor("_BaseColor", GetColor(src, new[] { "baseColorFactor", "_BaseColor", "_Color" }, Color.white));

            string matPath = outDir + "/" + stem + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                existing.CopyPropertiesFromMaterial(mat);
                Object.DestroyImmediate(mat);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        /// <summary>
        /// 텍스처를 뽑아 png 로 저장하고 임포트 설정까지 걸어 준다.
        /// glb 안의 텍스처는 읽기가 막혀 있으므로 한 번 그려(Blit) 픽셀을 되읽는다.
        /// </summary>
        private static Texture2D Extract(Texture2D src, string path, bool isNormal)
        {
            int w = src.width, h = src.height;
            float k = Mathf.Min(1f, (float)MaxSize / Mathf.Max(w, h));
            int tw = Mathf.Max(4, Mathf.RoundToInt(w * k));
            int th = Mathf.Max(4, Mathf.RoundToInt(h * k));

            var rw = isNormal ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
            var rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32, rw);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            var flat = new Texture2D(tw, th, TextureFormat.RGBA32, false, isNormal);
            flat.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
            flat.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            File.WriteAllBytes(path, flat.EncodeToPNG());
            Object.DestroyImmediate(flat);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ApplyImportSettings(path, isNormal);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ApplyImportSettings(string path, bool isNormal)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;

            ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !isNormal;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 2;
            ti.maxTextureSize = MaxSize;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.alphaSource = TextureImporterAlphaSource.None;

            foreach (string platform in new[] { "Standalone", "Android" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = MaxSize;
                ps.format = platform == "Android"
                    ? TextureImporterFormat.ASTC_6x6
                    : (isNormal ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT1);
                ps.compressionQuality = 100;
                ti.SetPlatformTextureSettings(ps);
            }
            ti.SaveAndReimport();
        }

        /// <summary>씬의 렌더러에서 옛 재질을 새 재질로 갈아 끼운다.</summary>
        private static int SwapInScene(Dictionary<Material, Material> remap)
        {
            int n = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material to;
                    if (mats[i] != null && remap.TryGetValue(mats[i], out to)) { mats[i] = to; changed = true; }
                }
                if (!changed) continue;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
                n++;
            }
            return n;
        }

        // ───────── 헬퍼 ─────────

        /// <summary>이름만 보고 무슨 맵인지 가른다. glTF 쪽 이름이 제각각이라 넉넉히 본다.</summary>
        private static string Classify(string prop, string texName)
        {
            string s = (prop + " " + texName).ToLowerInvariant();
            if (s.Contains("normal") || s.Contains("bump")) return "normal";
            if (s.Contains("metal") || s.Contains("rough") || s.Contains("occlusion") || s.Contains("orm")) return "data";
            if (s.Contains("emis")) return "emissive";
            return "color";
        }

        private static float GetFloat(Material m, string[] names, float fallback)
        {
            foreach (var n in names) if (m.HasProperty(n)) return m.GetFloat(n);
            return fallback;
        }

        private static Color GetColor(Material m, string[] names, Color fallback)
        {
            foreach (var n in names) if (m.HasProperty(n)) return m.GetColor(n);
            return fallback;
        }

        /// <summary>파일 이름에 쓸 수 없는 글자를 걷어낸다.</summary>
        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c > 127 ? c : '_');
            return sb.ToString();
        }

        /// <summary>씬이 지고 있는 텍스처 용량(바이트).</summary>
        private static long MeasureSceneTextures()
        {
            var texs = new HashSet<Texture>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    foreach (string prop in m.GetTexturePropertyNames())
                    {
                        var t = m.GetTexture(prop);
                        if (t != null) texs.Add(t);
                    }
                }
            long sum = 0;
            foreach (var t in texs) sum += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
            return sum;
        }
    }
}
