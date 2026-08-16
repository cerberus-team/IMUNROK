using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 조사청 사건판에 붙는 사건 문서(방문·첩보·전령) 텍스처를 구워낸다.
    ///
    /// 그림은 Tools/DocBaker/make_docs.ps1 이 그리고(궁서체 세로쓰기 + 한지 질감),
    /// 문구는 Tools/DocBaker/docs.json 에만 있다. 문구를 고치려면 json만 고치고 이 메뉴를 다시 실행.
    ///
    /// 결과 PNG는 git에 올리지 않는다(.gitignore). 대신 .meta는 커밋하므로
    /// 팀원이 이 메뉴를 실행하면 GUID가 유지돼 재질 연결이 안 끊긴다.
    ///
    /// 메뉴: [이문록 ▸ 에셋: 사건 문서 텍스처 굽기]. 윈도우 전용(System.Drawing·궁서체 의존).
    /// </summary>
    public static class CaseDocumentBaker
    {
        private const string ScriptRel = "Tools/DocBaker/make_docs.ps1";
        private const string TexFolder = "Assets/_Project/Art/Props/Textures";

        private static readonly string[] Textures =
        {
            "T_Doc_Case1_Onggojip",   // 제일사건 · 옹고집전 — 소장(訴狀)
            "T_Doc_Case2_Seocheon",   // 제이사건 · 서천꽃밭 — 첩보(牒報)
            "T_Doc_Case3_Gyeonu",     // 제삼사건 · 견우직녀 — 전령(傳令)
        };

        [MenuItem("이문록/에셋: 사건 문서 텍스처 굽기")]
        public static void Bake()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string script = Path.Combine(projectRoot, ScriptRel.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(script))
            {
                EditorUtility.DisplayDialog("이문록",
                    $"생성 스크립트를 못 찾았어요:\n{ScriptRel}\n\n저장소에서 Tools 폴더를 받았는지 확인하세요.", "확인");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -ProjectRoot \"{projectRoot}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            string stdout, stderr;
            int exitCode;
            try
            {
                EditorUtility.DisplayProgressBar("사건 문서 굽는 중", "궁서체 세로쓰기 렌더링…", 0.4f);
                using var p = Process.Start(psi);
                stdout = p!.StandardOutput.ReadToEnd();
                stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                exitCode = p.ExitCode;
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("이문록", $"PowerShell 실행 실패:\n{e.Message}", "확인");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (exitCode != 0)
            {
                Debug.LogError($"[사건문서] 생성 실패 (exit {exitCode})\n{stdout}\n{stderr}");
                EditorUtility.DisplayDialog("이문록", "문서 생성에 실패했어요. Console 로그를 확인하세요.", "확인");
                return;
            }

            Debug.Log($"[사건문서] {stdout}");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            int applied = 0;
            foreach (string name in Textures)
                if (ApplyImportSettings($"{TexFolder}/{name}.png")) applied++;

            EditorUtility.DisplayDialog("이문록",
                $"사건 문서 텍스처 {applied}개 완료!\n\n" +
                "문구를 바꾸려면 Tools/DocBaker/docs.json 을 고치고 다시 실행하세요.", "확인");
        }

        /// <summary>VR용 임포트 설정. 1024 정사각(POT)이라 DXT1/ASTC 블록압축이 걸린다.</summary>
        private static bool ApplyImportSettings(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter ti)
            {
                Debug.LogWarning($"[사건문서] 임포트 안 됨: {path}");
                return false;
            }

            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;

            foreach (string platform in new[] { "Standalone", "Android" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = 1024;
                ps.format = platform == "Android" ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.DXT1;
                ps.compressionQuality = 100;
                ti.SetPlatformTextureSettings(ps);
            }

            ti.SaveAndReimport();
            return true;
        }
    }
}
