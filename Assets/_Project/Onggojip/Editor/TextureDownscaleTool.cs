using System.IO;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 고택 텍스처(PNG)를 최대 해상도로 실제 다운스케일해 "파일 자체"를 줄인다.
    /// (Unity Max Size는 게임 용량만 줄이지만, 이건 소스 PNG를 줄여 저장소·LFS 용량을 줄임)
    ///
    /// ⚠️ 원본 덮어씀 — 반드시 먼저 백업할 것. (이미 데스크탑에 백업해둠)
    /// PNG 파일을 직접 읽어 리사이즈 → 다시 PNG로 저장하므로 임포트 설정과 무관하게 동작.
    ///
    /// 메뉴: [이문록 ▸ 에셋: 고택 텍스처 1024로 줄이기] / [… 512로 줄이기].
    /// </summary>
    public static class TextureDownscaleTool
    {
        private const string TexFolder =
            "Assets/_Project/Onggojip/Art/KimMyeonggwanHouse/Texture";
        private const long MB = 1024 * 1024;

        [MenuItem("이문록/에셋: 고택 텍스처 1024로 줄이기")]
        public static void To1024() => Downscale(1024);

        [MenuItem("이문록/에셋: 고택 텍스처 512로 줄이기")]
        public static void To512() => Downscale(512);

        private static void Downscale(int maxSize)
        {
            string abs = Path.GetFullPath(TexFolder);
            if (!Directory.Exists(abs))
            {
                EditorUtility.DisplayDialog("이문록", $"텍스처 폴더를 못 찾음:\n{TexFolder}", "확인");
                return;
            }

            var files = Directory.GetFiles(abs, "*.png", SearchOption.TopDirectoryOnly);
            if (!EditorUtility.DisplayDialog("이문록",
                    $"텍스처 {files.Length}개를 최대 {maxSize}px로 줄입니다(원본 덮어씀).\n" +
                    "데스크탑 백업 있는지 확인하고 진행하세요.\n\n계속?", "실행", "취소"))
                return;

            long before = 0, after = 0;
            int changed = 0;

            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string path = files[i];
                    EditorUtility.DisplayProgressBar("텍스처 줄이는 중",
                        $"{i + 1}/{files.Length}  {Path.GetFileName(path)}", (float)i / files.Length);

                    before += new FileInfo(path).Length;

                    byte[] data = File.ReadAllBytes(path);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(data))
                    {
                        after += new FileInfo(path).Length;
                        Object.DestroyImmediate(tex);
                        continue;
                    }

                    int w = tex.width, h = tex.height;
                    int mx = Mathf.Max(w, h);
                    if (mx <= maxSize)   // 이미 작으면 그대로
                    {
                        after += new FileInfo(path).Length;
                        Object.DestroyImmediate(tex);
                        continue;
                    }

                    float s = (float)maxSize / mx;
                    int nw = Mathf.Max(1, Mathf.RoundToInt(w * s));
                    int nh = Mathf.Max(1, Mathf.RoundToInt(h * s));

                    var rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
                    var prev = RenderTexture.active;
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    var outTex = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
                    outTex.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
                    outTex.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);

                    File.WriteAllBytes(path, outTex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    Object.DestroyImmediate(outTex);

                    after += new FileInfo(path).Length;
                    changed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("이문록",
                $"완료!\n\n· 줄인 텍스처: {changed}개\n" +
                $"· 용량: {before / MB}MB → {after / MB}MB\n\n" +
                "이제 채팅으로 '끝났어' 알려주면 커밋 정리해줄게요.", "확인");
        }
    }
}
