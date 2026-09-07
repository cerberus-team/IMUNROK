using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// 조사청 사건판에 붙는 사건 문서(소장·첩보·전령)와, 사건별 잠행 단서 문서 텍스처를 구워낸다.
    ///
    /// 떨어지는 자리가 둘로 갈린다 — 세 사건 공용은 Art/Props/Textures,
    /// 한 사건에서만 쓰는 단서는 그 사건 폴더의 Art/Textures. docs.json 의 "case" 필드가 그 기준이다.
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
        private const string ScriptRel     = "Tools/DocBaker/make_docs.ps1";
        private const string IconScriptRel = "Tools/DocBaker/make_tool_icons.ps1";
        /// <summary>사건 구분 없이 쓰는 것(조사청 사건판)만 여기 떨어진다.</summary>
        private const string SharedFolder = "Assets/_Project/Art/Props/Textures";

        /// <summary>사건 전용 문서는 그 사건 폴더로. docs.json 의 "case" 값과 짝이 맞아야 한다.</summary>
        private const string OnggojipFolder = "Assets/_Project/Onggojip/Art/Textures";

        /// <summary>
        /// 구워낼 텍스처. needsAlpha=투명한 부분이 있는 것(타다 만 조각 등).
        /// folder 는 make_docs.ps1 이 실제로 쓰는 자리와 같아야 임포트 설정이 먹는다.
        /// </summary>
        private static readonly (string name, bool needsAlpha, string folder)[] Textures =
        {
            // 조사청 사건판 — 세 사건 공용이라 공통 소품에 둔다
            ("T_Doc_Case1_Onggojip", false, SharedFolder),   // 제1사건 · 옹고집전 — 소장(訴狀)
            ("T_Doc_Case2_Seocheon", false, SharedFolder),   // 제2사건 · 서천꽃밭 — 첩보(牒報)
            ("T_Doc_Case3_Gyeonu",   false, SharedFolder),   // 제3사건 · 견우직녀 — 전령(傳令)

            // 제1사건 잠행 단서 — 옹고집 전용
            ("T_Doc_J06_Jangbu",   false, OnggojipFolder),   // J06 전조기 — 최근 두 줄만 필적이 다르다
            ("T_Doc_J07_Mulmok",   false, OnggojipFolder),   // J07 물목기 — 최근 두 줄에만 수결(押)
            ("T_Doc_J08_Chayong",  false, OnggojipFolder),   // J08 차용증 — 갑리(연 10할)
            ("T_Doc_J09_Jeungseo", false, OnggojipFolder),   // J09 수표 — 미회수, 다른 고을
            ("T_Doc_J11_Byeolgeup",false, OnggojipFolder),   // J11 별급문기 — '오래 부린 종 복동에게'
            ("T_Doc_J10_Seochal",  true,  OnggojipFolder),   // J10 타다 만 서찰 조각 — 가장자리가 뚫려 있다
            ("T_Doc_J12_Hojeok",   false, OnggojipFolder),   // J12 호적대장 — 관리의 손. 복동이 살아 있다
            ("T_Doc_J13_Hogu",     false, OnggojipFolder),   // J13 호구단자 — 집안 사람의 손. 복동이 죽었다
            ("T_Doc_J06_Yeonseup", false, OnggojipFolder),   // J06 보강 — 위조를 연습한 눌린 자국. 돋보기로만 보인다

            // 제2막 공개 단서 — 관아 문서고의 대장 넷.
            //
            // 넷 다 <b>집에서 가져온 무엇과 짝</b>이다(CrossCheck 표를 볼 것).
            // 혼자 보면 아무것도 아닌 종이고, 겹쳐 놓아야 비로소 말을 한다.
            ("T_Doc_G01_Hojeok_Wonbon", false, OnggojipFolder),  // G01 호적대장 원본 — 파기(疤記) 한 줄이 더 있다
            ("T_Doc_G02_Hogu_Wonbon",   false, OnggojipFolder),  // G02 관아가 받은 호구단자 — 집 것과 같은 손
            ("T_Doc_G03_Ipan",          false, OnggojipFolder),  // G03 입안대장 — 별급문기 사본, 면천 기록 없음
            ("T_Doc_G04_Hwansang",      false, OnggojipFolder),  // G04 환상대장 — 한 달간 소작료 인하
        };

        /// <summary>
        /// 도구벨트 아이콘(등불·돋보기·수첩·지도)을 다시 그린다.
        /// 그림은 Tools/DocBaker/make_tool_icons.ps1 이 그리고, 결과는 ToolDef 에셋에 이미 연결돼 있다.
        /// 문서와 달리 UI라서 PNG도 git에 들어간다(작고, 팀원이 굽지 않아도 보여야 한다).
        /// </summary>
        [MenuItem("이문록/에셋: 도구 아이콘 굽기")]
        public static void BakeToolIcons()
        {
            if (!RunScript(IconScriptRel, "도구 아이콘 그리는 중", out string stdout)) return;
            Debug.Log($"[도구아이콘] {stdout}");
            // ForceUpdate 까지 줘야 유니티가 디스크 쪽을 정답으로 삼고 다시 읽는다.
            // 빼면 굽기 전 기록을 붙들고 Import Error Code 4 를 낸다.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            EditorUtility.DisplayDialog("이문록",
                "도구 아이콘을 다시 구웠어요.\n\n" +
                "모양을 바꾸려면 Tools/DocBaker/make_tool_icons.ps1 을 고치고 다시 실행하세요.", "확인");
        }

        [MenuItem("이문록/에셋: 사건 문서 텍스처 굽기")]
        public static void Bake()
        {
            if (!RunScript(ScriptRel, "사건 문서 굽는 중", out string stdout)) return;

            Debug.Log($"[사건문서] {stdout}");
            // ForceUpdate 까지 줘야 유니티가 디스크 쪽을 정답으로 삼고 다시 읽는다.
            // 빼면 굽기 전 기록을 붙들고 Import Error Code 4 를 낸다.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            int applied = 0;
            foreach (var (name, needsAlpha, folder) in Textures)
                if (ApplyImportSettings($"{folder}/{name}.png", needsAlpha)) applied++;

            EditorUtility.DisplayDialog("이문록",
                $"사건 문서 텍스처 {applied}개 완료!\n\n" +
                "문구를 바꾸려면 Tools/DocBaker/docs.json 을 고치고 다시 실행하세요.", "확인");
        }

        /// <summary>
        /// Tools/DocBaker 의 그리기 스크립트를 돌린다. 실패하면 창을 띄우고 false.
        /// 문서와 도구 아이콘이 같은 방식(윈도우 PowerShell + System.Drawing)이라 함께 쓴다.
        /// </summary>
        private static bool RunScript(string scriptRel, string progressTitle, out string stdout)
        {
            stdout = "";
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string script = Path.Combine(projectRoot, scriptRel.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(script))
            {
                EditorUtility.DisplayDialog("이문록",
                    $"생성 스크립트를 못 찾았어요:\n{scriptRel}\n\n저장소에서 Tools 폴더를 받았는지 확인하세요.", "확인");
                return false;
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

            string stderr;
            int exitCode;
            try
            {
                EditorUtility.DisplayProgressBar(progressTitle, "궁서체 세로쓰기 렌더링…", 0.4f);

                // 굽는 동안 자동 새로고침을 막는다. 켜두면 유니티가 아직 쓰는 중인 PNG를
                // 물고 들어가 "modification time of ... while content on disk has ..."
                // (Import Error Code 4)를 무더기로 뱉는다 — 에셋DB와 디스크가 어긋난 것이다.
                AssetDatabase.DisallowAutoRefresh();
                try
                {
                    using var p = Process.Start(psi);
                    stdout = p!.StandardOutput.ReadToEnd();
                    stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                }
                finally { AssetDatabase.AllowAutoRefresh(); }
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("이문록", $"PowerShell 실행 실패:\n{e.Message}", "확인");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (exitCode != 0)
            {
                Debug.LogError($"[{scriptRel}] 생성 실패 (exit {exitCode})\n{stdout}\n{stderr}");
                EditorUtility.DisplayDialog("이문록", "생성에 실패했어요. Console 로그를 확인하세요.", "확인");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 임포트 설정. 1024 정사각(POT)이라 블록압축이 걸린다.
        /// 알파가 필요한 것(타다 만 조각)은 DXT5/ASTC, 나머지는 절반 용량인 DXT1.
        /// </summary>
        private static bool ApplyImportSettings(string path, bool needsAlpha)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter ti)
            {
                Debug.LogWarning($"[사건문서] 임포트 안 됨: {path}");
                return false;
            }

            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = needsAlpha
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = needsAlpha;
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
                ps.format = platform == "Android"
                    ? TextureImporterFormat.ASTC_6x6                                     // ASTC는 알파를 같이 담는다
                    : (needsAlpha ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT1);
                ps.compressionQuality = 100;
                ti.SetPlatformTextureSettings(ps);
            }

            ti.SaveAndReimport();
            return true;
        }
    }
}
