using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// 팀 공용 글꼴을 **TMP 폰트 에셋으로 굽는다** (2026-08-26, 멱등).
    ///
    /// ■ 왜 이 도구가 있는가
    ///   원본 .ttf 는 저장소에 넣지 않는다 (LFS 사용량 0 방침 — .gitignore 참조).
    ///   구운 결과물도 아틀라스가 커서 커밋하지 않는다. 그래서 <b>메뉴 한 번으로 다시 만들어지는</b>
    ///   길을 열어 둔다 — 서고 장부 텍스처·관아 담장 메시와 같은 규약이다.
    ///
    /// ■ 두 벌을 굽고 한 벌로 이어 붙인다
    ///   ① 한글 — ChosunCentennial (조선 궁서체). 본문 글꼴이다.
    ///   ② 한자 — tkFangSong (방송체). ①의 <b>폴백</b>으로 매단다.
    ///   한글 글꼴에 없는 글자를 만나면 TMP 가 알아서 ②를 뒤진다. 코드는 ①만 알면 된다.
    ///
    /// ■ 아틀라스를 어떻게 작게 유지하는가
    ///   한글 완성형 11,172자를 다 넣으면 2048² 아틀라스가 여러 장 필요하다. 그래서
    ///   <b>Dynamic 방식 + 실사용 문자 선굽기</b>를 쓴다:
    ///     · 프로젝트의 문자열 리터럴·NPC 프로필을 훑어 <b>실제로 쓰는 글자만</b> 미리 구워 둔다.
    ///     · 그러고도 없는 글자가 나오면 (Gemini 가 만들어 내는 대사가 그렇다) TMP 가
    ///       <b>그 자리에서</b> 아틀라스에 새로 굽는다 — 네모(□)가 뜨지 않는다.
    ///   고정 집합만 쓰면 LLM 대사에서 반드시 구멍이 난다. 이 프로젝트에서는 Dynamic 이 유일한 답이다.
    ///
    /// ■ 자리
    ///   <c>Assets/_Project/_Common/Art/Fonts/Resources/</c> — Resources 아래에 두는 까닭은
    ///   <see cref="UiSkin"/> 가 정적 클래스라 직렬화 참조를 들 수 없기 때문이다.
    ///   이름으로 불러 쓴다. 그래서 다시 구워 GUID 가 바뀌어도 아무것도 깨지지 않는다.
    /// </summary>
    public static class GyeonuFontBaker
    {
        /// <summary>이 프로젝트에서 글꼴이 있는 자리. 없으면 이름으로 프로젝트 전체를 뒤진다.</summary>
        const string PreferredDir = "Assets/_Project/_Common/Art/Fonts";

        const string HangulFile = "ChosunCentennial_ttf";
        const string HanjaFile = "tkFangSong";

        /// <summary>
        /// 글꼴 파일을 찾는다 — <see cref="PreferredDir"/> 를 먼저 보고, 없으면 이름으로 뒤진다.
        ///
        /// 고정 경로만 보지 않는 까닭: 이 도구는 <b>다른 사건 담당자에게 통째로 건네지는</b>
        /// UI 꾸러미에 함께 들어간다(<c>Tools ▸ 이문록 ▸ UI 패키지 내보내기</c>).
        /// 받는 쪽 폴더 구조는 우리와 다르므로, 글꼴을 아무 데나 넣어도 찾아내야 한다.
        /// </summary>
        static Font FindTtf(string fileNameNoExt)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Font>(PreferredDir + "/" + fileNameNoExt + ".ttf");
            if (direct != null) return direct;

            foreach (string guid in AssetDatabase.FindAssets(fileNameNoExt + " t:Font"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p) != fileNameNoExt) continue;
                var f = AssetDatabase.LoadAssetAtPath<Font>(p);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>구운 에셋을 둘 곳 — 원본 글꼴 옆의 <c>Resources/</c>. 폴더 구조를 안 가린다.</summary>
        static string ResFolder(Font ttf)
        {
            string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(ttf)).Replace('\\', '/');
            return dir + "/Resources";
        }

        /// <summary>런타임이 <c>Resources.Load</c> 로 부르는 이름. 바꾸면 <see cref="UiSkin"/> 도 함께 고칠 것.</summary>
        public const string HangulAsset = "TMP_Hangul_ChosunCentennial";
        public const string HanjaAsset = "TMP_Hanja_tkFangSong";

        /// <summary>
        /// <b>화면에 나올 글자를 찾아 훑을 폴더</b> — 여기 있는 <c>.cs</c> 문자열 리터럴과
        /// <c>.asset</c> 을 뒤져 미리 구울 문자를 모은다. <b>다른 사건에서 쓰려면 여기를 자기 폴더로 고칠 것.</b>
        ///
        /// 없는 폴더는 조용히 건너뛴다 — 하나도 못 찾아도 굽기는 성공한다.
        /// 다만 그때는 라틴·문장부호 기본 판만 미리 구워지고 한글은 전부 <b>실행 중에</b> 구워진다.
        /// 아틀라스에 없던 글자가 처음 뜨는 그 한 프레임 동안 네모 덩어리로 번쩍이므로,
        /// 화면에 나올 글이 있는 폴더는 반드시 여기 적어 둘 것.
        /// </summary>
        public static readonly string[] ScanRoots =
        {
            "Assets/_Project/Gyeonu",
        };

        /// <summary><see cref="ScanRoots"/> 아래에서 <b>글이 든 ScriptableObject</b>가 사는 하위 폴더.
        /// (NPC 프로필·소지품 정의 따위. 사건마다 이름이 다르면 여기에 더 적을 것)</summary>
        public static readonly string[] TextAssetFolders = { "Npc", "Resources" };

        // 굽기 설정 — 표본 크기가 곧 글자 원본의 해상도다.
        // 48 은 궁서체의 가는 획이 뭉개지지 않는 하한이다.
        const int SamplingSize = 48;
        const int Padding = 8;
        const int HangulAtlas = 2048;   // 실사용 900여 자가 한 장에 들어간다
        const int HanjaAtlas = 512;     // 실제 쓰는 한자는 열 자 안쪽 — 모자라면 자동으로 한 장 더 붙는다

        // ─────────────────────────────────────────────────────
        [MenuItem("Tools/이문록/글꼴/TMP 폰트 에셋 굽기", false, 10)]
        public static void Bake()
        {
            var hangulTtf = FindTtf(HangulFile);
            var hanjaTtf = FindTtf(HanjaFile);
            if (hangulTtf == null || hanjaTtf == null)
            {
                EditorUtility.DisplayDialog("글꼴 굽기",
                    "원본 .ttf 를 찾지 못했다.\n\n" + PreferredDir + " 아래(또는 프로젝트 아무 데나)에\n" +
                    "  ChosunCentennial_ttf.ttf\n  tkFangSong.ttf\n\n" +
                    "두 파일을 넣고 다시 눌러라.\n(.ttf 는 gitignore 대상 — 파일로 따로 받아야 한다)", "확인");
                return;
            }

            string ResDir = ResFolder(hangulTtf);
            if (!AssetDatabase.IsValidFolder(ResDir))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(ResDir).Replace('\\', '/'), "Resources");

            var set = Survey();

            // ① 한글 글꼴 — 실사용 문자 선굽기
            var hangul = Create(hangulTtf, HangulAsset, HangulAtlas);
            string missedInHangul;
            hangul.TryAddCharacters(set.NonCjk, out missedInHangul);

            // ② 한자 글꼴 — 한자 + 한글 글꼴이 못 낸 글자를 함께 굽는다.
            //    ①이 기호(①─「」 따위)를 못 가진 경우가 흔한데, 그 구멍을 여기서 메운다.
            var hanja = Create(hanjaTtf, HanjaAsset, HanjaAtlas);
            string hanjaSeed = set.Cjk + (missedInHangul ?? "");
            string missedInHanja;
            hanja.TryAddCharacters(hanjaSeed, out missedInHanja);

            // ③ 폴백 글꼴의 <b>몸집을 본문 글꼴에 맞춘다</b>
            //
            // ⚠️ 이걸 빼먹으면 한자만 눈에 띄게 작게 나온다 (2026-08-26 나침반에서 실측).
            //    두 글꼴을 같은 48pt 로 구워도 글자가 em 상자를 채우는 정도가 서로 다르다 —
            //    tkFangSong 의 상승선은 38.4, ChosunCentennial 은 45.6 이다. TMP 는 폴백 글리프를
            //    <b>그 글꼴 자신의 지표</b>로 재므로, 같은 fontSize 를 줘도 한자가 84% 크기로 앉는다.
            //    나침반의 北·東·南·西 는 퍼즐을 푸는 데 꼭 읽어야 하는 글자라 그냥 두면 안 된다.
            //    <c>faceInfo.scale</c> 로 상승선을 맞춰 두면 한 줄에 섞여도 크기가 고르다.
            var hjFace = hanja.faceInfo;
            float ascentA = hangul.faceInfo.ascentLine, ascentB = hanja.faceInfo.ascentLine;
            if (ascentB > 0.01f) hjFace.scale = ascentA / ascentB;
            hanja.faceInfo = hjFace;

            // ④ 폴백을 매단다 — 한글 글꼴에 없는 글자는 자동으로 한자 글꼴에서 찾는다
            hangul.fallbackFontAssetTable = new List<TMP_FontAsset> { hanja };

            Save(hanja, ResDir + "/" + HanjaAsset + ".asset");
            Save(hangul, ResDir + "/" + HangulAsset + ".asset");

            EditorUtility.SetDirty(hangul);
            EditorUtility.SetDirty(hanja);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var sb = new StringBuilder();
            sb.AppendLine("[글꼴] TMP 폰트 에셋 두 벌을 구웠다 — " + ResDir);
            sb.AppendLine("  한글 " + HangulAsset + "  선굽기 " + set.NonCjk.Length + "자"
                          + "  아틀라스 " + hangul.atlasTextures.Length + "장 × " + HangulAtlas
                          + "  글리프 " + hangul.glyphTable.Count);
            sb.AppendLine("  한자 " + HanjaAsset + "  선굽기 " + hanjaSeed.Length + "자"
                          + "  아틀라스 " + hanja.atlasTextures.Length + "장 × " + HanjaAtlas
                          + "  글리프 " + hanja.glyphTable.Count);
            if (!string.IsNullOrEmpty(missedInHangul))
                sb.AppendLine("  한글 글꼴에 없어 한자 글꼴로 넘긴 글자: " + Describe(missedInHangul));
            if (!string.IsNullOrEmpty(missedInHanja))
                sb.AppendLine("  ⚠ 두 글꼴 어디에도 없는 글자: " + Describe(missedInHanja));
            sb.AppendLine("  폴백: " + HangulAsset + " → " + HanjaAsset);
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/이문록/글꼴/쓰이는 문자 조사", false, 11)]
        public static void SurveyMenu()
        {
            var s = Survey();
            var sb = new StringBuilder();
            sb.AppendLine("[글꼴] 제3사건이 쓰는 문자 전수 조사");
            sb.AppendLine("  한글 음절 " + s.HangulCount + "자");
            sb.AppendLine("  한자      " + s.Cjk.Length + "자 : " + s.Cjk);
            sb.AppendLine("  ASCII     " + s.AsciiCount + "자");
            sb.AppendLine("  기타 기호 " + s.SymbolCount + "자 : " + s.Symbols);
            sb.AppendLine("  ─ 훑은 곳: " + s.SourceCount + "개 파일");
            Debug.Log(sb.ToString());
        }

        // ── 굽기 ──────────────────────────────────────────────
        static TMP_FontAsset Create(Font ttf, string name, int atlas)
        {
            var fa = TMP_FontAsset.CreateFontAsset(
                ttf, SamplingSize, Padding, GlyphRenderMode.SDFAA,
                atlas, atlas, AtlasPopulationMode.Dynamic, true);
            fa.name = name;
            fa.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fa.isMultiAtlasTexturesEnabled = true;
            return fa;
        }

        /// <summary>
        /// 에셋으로 저장한다. 아틀라스 텍스처와 재질은 <b>부속 에셋</b>으로 넣어야 한다 —
        /// 안 넣으면 저장 후 참조가 끊겨 다음 실행에서 글자가 통째로 사라진다.
        /// </summary>
        static void Save(TMP_FontAsset fa, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null)
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.CreateAsset(fa, path);

            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                if (fa.atlasTextures[i] == null) continue;
                fa.atlasTextures[i].name = fa.name + " Atlas " + i;
                if (AssetDatabase.GetAssetPath(fa.atlasTextures[i]) != path)
                    AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
            }
            if (fa.material != null)
            {
                fa.material.name = fa.name + " Material";
                if (AssetDatabase.GetAssetPath(fa.material) != path)
                    AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            AssetDatabase.SaveAssets();
        }

        // ── 문자 조사 ─────────────────────────────────────────
        public class CharSet
        {
            public string NonCjk;     // 한글 글꼴이 맡을 것
            public string Cjk;        // 한자 글꼴이 맡을 것
            public string Symbols;
            public int HangulCount, AsciiCount, SymbolCount, SourceCount;
        }

        /// <summary>
        /// 게임에 실제로 나오는 문자를 모은다.
        ///
        /// ⚠️ 주석은 세지 않는다 — 이 프로젝트의 주석은 본문보다 길고 한글이 많아, 함께 세면
        ///    아틀라스가 두 배로 부푼다. C# 은 <b>문자열 리터럴</b>만 훑는다.
        /// </summary>
        public static CharSet Survey()
        {
            var chars = new HashSet<char>();
            int files = 0;

            // 기본 판 — 라틴·숫자·문장부호는 무조건 넣는다
            for (int i = 32; i < 127; i++) chars.Add((char)i);
            foreach (char c in "　·—―…‘’“”「」『』〈〉【】〔〕±×÷≈≒≤≥°′″←→↑↓↔↺│─┌┐└┘▶◀▲▼◆◇○●★☆①②③④⑤⑥⑦⑧⑨⑩✓✕✗✂⚠、。〃")
                chars.Add(c);

            // ① C# 문자열 리터럴
            var lit = new Regex("@?\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.Singleline);
            for (int d = 0; d < ScanRoots.Length; d++)
                foreach (string p in AllFiles(ScanRoots[d], ".cs"))
                {
                    files++;
                    string src = System.IO.File.ReadAllText(p, Encoding.UTF8);
                    foreach (Match m in lit.Matches(src))
                        foreach (char c in m.Value) chars.Add(c);
                }

            // ② NPC 프로필과 소지품 정의 — 통째로 훑는다 (거의 다 화면에 뜨는 글이다)
            //
            // ⚠️ 소지품(Resources/GyeonuItems)을 빠뜨리면 안 된다. 물건 설명은 <b>상세 화면에
            //    처음 들어간 순간</b> 나타나는데, 그때 아틀라스에 없는 글자는 TMP 가 그 자리에서
            //    굽는다 — 굽는 <b>그 한 프레임 동안 글자가 네모난 덩어리로 번쩍인다</b>
            //    (2026-08-26 「후고 반출 대장」 상세에서 실측). 미리 구워 두면 안 생긴다.
            //
            // ⚠️ <c>.asset</c> 은 <b>글이 든 하위 폴더만</b> 훑는다. 사건 폴더를 통째로 뒤지면
            //    구운 메시·반사 큐브맵 같은 <b>바이너리 .asset</b>(개당 수백 MB)까지 글자로 읽어
            //    굽기가 몇 분씩 걸리고 쓰레기 문자가 섞인다.
            for (int d = 0; d < ScanRoots.Length; d++)
                for (int s = 0; s < TextAssetFolders.Length; s++)
                    foreach (string p in AllFiles(ScanRoots[d] + "/" + TextAssetFolders[s], ".asset"))
                    {
                        var fi = new System.IO.FileInfo(p);
                        if (fi.Length > 2 * 1024 * 1024) continue;   // 글이 든 정의 파일은 이만큼 크지 않다
                        files++;
                        foreach (char c in System.IO.File.ReadAllText(p, Encoding.UTF8)) chars.Add(c);
                    }

            var nonCjk = new StringBuilder();
            var cjk = new StringBuilder();
            var sym = new StringBuilder();
            int hangul = 0, ascii = 0;
            foreach (char c in chars)
            {
                // ⚠️ 대리쌍(이모지)과 서식 제어 문자는 빼고 센다. char 하나로는 반쪽짜리라
                //    TMP 에 넘기면 짝 없는 코드포인트가 되어 굽기가 실패한다 (🔍·U+FE0F 에서 겪음).
                if (char.IsControl(c) || char.IsSurrogate(c) || c == '️' || c == '﻿') continue;
                bool isCjk = (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF)
                             || (c >= 0xF900 && c <= 0xFAFF);
                if (isCjk) { cjk.Append(c); continue; }
                nonCjk.Append(c);
                if (c >= 0xAC00 && c <= 0xD7A3) hangul++;
                else if (c < 127) ascii++;
                else sym.Append(c);
            }

            return new CharSet
            {
                NonCjk = nonCjk.ToString(),
                Cjk = cjk.ToString(),
                Symbols = sym.ToString(),
                HangulCount = hangul,
                AsciiCount = ascii,
                SymbolCount = sym.Length,
                SourceCount = files,
            };
        }

        static IEnumerable<string> AllFiles(string dir, string ext)
        {
            string root = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            string abs = System.IO.Path.Combine(root, dir);
            if (!System.IO.Directory.Exists(abs)) yield break;
            foreach (string p in System.IO.Directory.GetFiles(abs, "*" + ext, System.IO.SearchOption.AllDirectories))
                yield return p;
        }

        static string Describe(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(c).Append("(U+").Append(((int)c).ToString("X4")).Append(") ");
            return sb.ToString();
        }
    }
}
