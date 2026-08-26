using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// **UI 뼈대를 다른 사건 담당자에게 건넬 꾸러미로 굽는다** (2026-08-26, 멱등).
    ///
    /// ■ 왜 원본을 그대로 내보내지 않는가
    ///   <c>AssetDatabase.ExportPackage</c> 는 에셋을 <b>지금 경로 그대로</b> 담는다. 원본을 담으면
    ///   받는 쪽 프로젝트에 <c>Assets/_Project/Gyeonu/Scripts/UI/…</c> 가 생긴다 — 남의 사건 폴더가
    ///   내 프로젝트에 들어앉는 꼴이다. 그래서 <b>사본을 임시 폴더에 세워</b> 굽고 지운다.
    ///   원본은 손대지 않는다(옮기지도, 지우지도 않는다).
    ///
    /// ■ 사본을 뜨면서 딱 한 가지를 바꾼다 — 이름 공간
    ///   <c>namespace IMUNROK.Gyeonu</c> → <c>namespace IMUNROK.Ui</c>.
    ///   견우 사건의 이름을 달고 다른 사건에 들어가면 안 되기 때문이다. 파일이 통째로 함께
    ///   옮겨 가므로 안쪽 참조는 하나도 안 깨진다.
    ///   ⚠️ 그래서 <b>이 꾸러미를 이 프로젝트에 도로 임포트하지 말 것.</b> 원본과 사본이
    ///      나란히 서서 같은 이름의 클래스가 두 벌이 된다(이름 공간이 달라 컴파일은 되지만 헷갈린다).
    ///
    /// ■ 무엇을 넣고 무엇을 뺐는가 — <see cref="Runtime"/> 목록이 답이다
    ///   넣은 것: <b>완성된 화면</b>(대화창·소지품 판·안내·퍼즐 얼개)과 그것들이 서는 뼈대,
    ///           갈아 끼우는 자리(인터페이스), 아무것도 구현 안 해도 도는 더미, 글꼴.
    ///   뺀 것: 나침반(혼천의 전용) · 사건 상태(<c>GyeonuCase</c>) · 소지품 저장소(<c>Inventory</c>) ·
    ///          물건 정의(<c>InventoryItem</c>) · 대화 내용(<c>DialogueSession</c>·Gemini) · 줍기(<c>ItemPickup</c>).
    ///          이것들은 전부 <b>인터페이스 뒤로 물러났다</b> — 받는 쪽이 자기 것을 꽂는다.
    ///
    /// ■ 의존성 자동 포함은 <b>끈다</b>
    ///   <c>ExportPackageOptions.Default</c> = 재귀 없음 + 의존성 없음. 켜면 견우 코드가 줄줄이
    ///   딸려 온다(판이 쓰는 타입을 타고 상태 시스템까지 간다).
    /// </summary>
    public static class UiPackageExporter
    {
        const string SrcRoot = "Assets/_Project/Gyeonu/Scripts";
        const string Stage = "Assets/IMUNROK_UI";
        const string PkgName = "IMUNROK_UI.unitypackage";
        const string DocPath = "docs/UI시스템_사용법.md";

        const string FromNs = "namespace IMUNROK.Gyeonu";
        const string ToNs = "namespace IMUNROK.Ui";

        /// <summary>꾸러미에 담을 런타임 코드. <c>원본경로 → 사본이름</c>.</summary>
        static readonly (string src, string dst)[] Runtime =
        {
            // ── 인터페이스 (팀원이 자기 시스템을 잇는 자리) ──
            ("UI/IUiItem.cs",            "IUiItem.cs"),              // IUiItem · IUiItemSource · IUiItemUse · UiItems
            ("UI/IDialogueBackend.cs",   "IDialogueBackend.cs"),     // IDialogueBackend · IDialogueSpeaker · IVoiceTranscriber · UiDialogue
            // ── 완성된 화면: 소지품 ──
            ("Inventory/InventoryUI.cs",      "InventoryUI.cs"),     // 목록 · 상세
            ("Inventory/InventoryInspect.cs", "InventoryInspect.cs"),// 전체 화면 조사
            ("Inventory/InventoryPreview.cs", "InventoryPreview.cs"),// 렌더텍스처 3D 미리보기
            ("Inventory/InventoryInput.cs",   "InventoryInput.cs"),  // I 키 · 획득 연출
            ("Inventory/InventoryHotspot.cs", "InventoryHotspot.cs"),
            // ── 완성된 화면: 대화 ──
            ("Dialogue/DialogueUI.cs",       "DialogueUI.cs"),
            ("Dialogue/DialogueHotspot.cs",  "DialogueHotspot.cs"),
            ("Dialogue/VoiceInput.cs",       "VoiceInput.cs"),       // 마이크 녹음·WAV (받아쓰기는 인터페이스)
            // ── 퍼즐 화면의 공통 얼개 ──
            ("Interactable.cs",          "Interactable.cs"),
            ("IOpenable.cs",             "IOpenable.cs"),            // 여닫이 상태를 밖에서 읽는 최소 계약
            ("IInnerTarget.cs",          "IInnerTarget.cs"),         // 열린 가구 속에서도 조준되게 하는 표식
            ("FocusInteractable.cs",     "FocusInteractable.cs"),    // 포커스 대상의 밑절미
            ("FocusReticle.cs",          "FocusReticle.cs"),         // 포커스 대상 위 표시 (FocusInteractable 이 쓴다)
            ("DebugFocusRig.cs",         "DebugFocusRig.cs"),        // 눈앞으로 당기기 · 비네트
            ("DebugInteractor.cs",       "DebugInteractor.cs"),      // 조준·누르기
            // ── 더미 (아무것도 구현 안 해도 화면이 돌게) ──
            ("UI/Sample/DummyItem.cs",       "Sample/DummyItem.cs"),
            ("UI/Sample/DummyItemSource.cs", "Sample/DummyItemSource.cs"),
            ("UI/Sample/DummyNpc.cs",        "Sample/DummyNpc.cs"),
            ("UI/Sample/DummyProp.cs",       "Sample/DummyProp.cs"),
            // ── 판 뼈대와 배치 ──
            ("UI/VrPanel.cs",            "VrPanel.cs"),              // 월드 캔버스·시야 추종·벽 회피
            ("UI/UiTuning.cs",           "UiTuning.cs"),             // PC/VR 배율·거리 (1단위 = 1픽셀 규약)
            ("UI/UiMode.cs",             "UiMode.cs"),               // PC/VR 판별
            ("UI/UiModeWatcher.cs",      "UiModeWatcher.cs"),        // F8 전환
            // ── 입력 추상화 ──
            ("UI/UiPointer.cs",          "UiPointer.cs"),            // IUiPointer + 마우스 + XR 컨트롤러 광선
            ("UI/UiWords.cs",            "UiWords.cs"),              // "좌클릭" ↔ "트리거"
            // ── 차림새 ──
            ("UI/UiSkin.cs",             "UiSkin.cs"),               // 글꼴·색·절차 스프라이트
            ("Inventory/InventorySkin.cs", "InventorySkin.cs"),      // 한지·목재·칸·돋보기 절차 텍스처
            // ── 갖춰진 판들 ──
            ("UI/AimPanel.cs",           "AimPanel.cs"),             // 조준점
            ("UI/NotePanel.cs",          "NotePanel.cs"),            // 제목+본문 메모
            ("UI/ToastPanel.cs",         "ToastPanel.cs"),           // 안내 문구
            ("UI/FocusHintPanel.cs",     "FocusHintPanel.cs"),       // 하단 상태·조작 두 줄
            ("DebugToast.cs",            "DebugToast.cs"),           // Show/ShowPinned 정적 창구
            // ── 눈(카메라) ──
            //    VrPanel·DebugToast 가 플레이어 눈을 이걸로 찾는다. 없으면 컴파일이 안 된다.
            //    자기 리그를 쓰더라도 이 파일은 함께 가야 한다 (문서의 「눈을 어디서 찾는가」 참고).
            ("DebugWalkController.cs",   "DebugWalkController.cs"),
        };

        /// <summary>에디터 도구. <c>원본경로 → 사본이름</c>.</summary>
        static readonly (string src, string dst)[] Editors =
        {
            ("../Editor/GyeonuFontBaker.cs",      "UiFontBaker.cs"),
            ("../Editor/UiSampleSceneBuilder.cs", "UiSampleSceneBuilder.cs"),
        };

        /// <summary>
        /// 글꼴 — <b>원본 .ttf 와 구워 둔 TMP 에셋을 함께</b> 넣는다 (합 44MB).
        ///
        /// 왜 둘 다인가: 꾸러미의 목적이 <b>팀 전체의 화면을 같아 보이게</b> 하는 것이라,
        /// 받자마자 우리 화면과 같아야 한다.
        ///   · 구운 에셋 → 임포트 즉시 조선 궁서체로 뜬다 (굽기 단계가 없다)
        ///   · 원본 .ttf → 미리 안 구운 글자(팀원 사건의 고유 명사)를 실행 중에 구울 수 있다
        /// 하나만 넣으면 각각 「굽기 한 번 더」와 「모르는 글자가 □」라는 구멍이 생긴다.
        /// </summary>
        static readonly string[] Fonts =
        {
            "Assets/_Project/_Common/Art/Fonts/ChosunCentennial_ttf.ttf",
            "Assets/_Project/_Common/Art/Fonts/tkFangSong.ttf",
            "Assets/_Project/_Common/Art/Fonts/Resources/TMP_Hangul_ChosunCentennial.asset",
            "Assets/_Project/_Common/Art/Fonts/Resources/TMP_Hanja_tkFangSong.asset",
        };

        // ─────────────────────────────────────────────────────
        [MenuItem("Tools/이문록/UI 패키지 내보내기", false, 400)]
        public static void Export() { Run(true); }

        /// <summary>글꼴을 뺀 가벼운 판 — 이미 글꼴을 가진 사람에게 코드만 다시 줄 때.</summary>
        [MenuItem("Tools/이문록/UI 패키지 내보내기 (글꼴 없이)", false, 401)]
        public static void ExportCodeOnly() { Run(false); }

        [MenuItem("Tools/이문록/UI 패키지 — 검사용 사본 만들기", false, 420)]
        public static void StageOnly()
        {
            var made = BuildStage();
            AssetDatabase.Refresh();
            Debug.Log("[UI 꾸러미] 검사용 사본을 " + Stage + " 에 세웠다 (" + made.Count + "개). "
                      + "컴파일 오류가 없는지 콘솔을 볼 것. 다 보면 「검사용 사본 지우기」로 치울 것.");
        }

        [MenuItem("Tools/이문록/UI 패키지 — 검사용 사본 지우기", false, 421)]
        public static void ClearStage()
        {
            if (AssetDatabase.IsValidFolder(Stage)) AssetDatabase.DeleteAsset(Stage);
            AssetDatabase.Refresh();
            Debug.Log("[UI 꾸러미] 검사용 사본을 치웠다.");
        }

        // ─────────────────────────────────────────────────────
        static void Run(bool withFonts)
        {
            // ⚠️ 사본을 만들면 Unity 가 곧바로 다시 컴파일하며 도메인을 갈아 끼운다 — 그러면
            //    이 함수가 도중에 끊긴다. 굽는 동안만 어셈블리 재적재를 잠근다.
            EditorApplication.LockReloadAssemblies();
            try
            {
                var paths = BuildStage();
                if (withFonts)
                    for (int i = 0; i < Fonts.Length; i++)
                    {
                        // ⚠️ .ttf 도 구운 TMP .asset 도 담아야 하므로 형을 가리지 않고 본다.
                        //    둘 다 gitignore 대상이라 새로 받은 사람 컴퓨터에는 없을 수 있다 —
                        //    없으면 경고만 남기고 빼고 굽는다(꾸러미 자체는 나온다).
                        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Fonts[i]) == null)
                        {
                            Debug.LogWarning("[UI 꾸러미] 글꼴을 못 찾았다 — 빼고 굽는다: " + Fonts[i]
                                + "\n  (.ttf 를 받아 넣고 Tools ▸ 이문록 ▸ 글꼴 ▸ TMP 폰트 에셋 굽기 를 먼저 누를 것)");
                            continue;
                        }
                        paths.Add(Fonts[i]);
                    }

                string outPath = System.IO.Path.Combine(ProjectRoot, PkgName).Replace('\\', '/');
                AssetDatabase.ExportPackage(paths.ToArray(), outPath, ExportPackageOptions.Default);

                AssetDatabase.DeleteAsset(Stage);
                Report(outPath, paths, withFonts);
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
                AssetDatabase.Refresh();
            }
        }

        /// <summary>임시 폴더에 사본을 세우고, 담을 에셋 경로 목록을 돌려준다.</summary>
        static List<string> BuildStage()
        {
            if (AssetDatabase.IsValidFolder(Stage)) AssetDatabase.DeleteAsset(Stage);
            AssetDatabase.CreateFolder("Assets", "IMUNROK_UI");
            AssetDatabase.CreateFolder(Stage, "Runtime");
            AssetDatabase.CreateFolder(Stage + "/Runtime", "Sample");
            AssetDatabase.CreateFolder(Stage, "Editor");

            var paths = new List<string>();

            for (int i = 0; i < Runtime.Length; i++)
                paths.Add(CopyCs(SrcRoot + "/" + Runtime[i].src, Stage + "/Runtime/" + Runtime[i].dst));
            for (int i = 0; i < Editors.Length; i++)
                paths.Add(CopyCs(SrcRoot + "/" + Editors[i].src, Stage + "/Editor/" + Editors[i].dst));

            paths.Add(Write(Stage + "/Runtime/IMUNROK.Ui.asmdef", RuntimeAsmdef));
            paths.Add(Write(Stage + "/Editor/IMUNROK.Ui.Editor.asmdef", EditorAsmdef));

            // 사용법 문서를 꾸러미 안에도 넣는다 — 받는 사람이 파일 하나만 열면 되게
            string doc = System.IO.Path.Combine(ProjectRoot, DocPath);
            paths.Add(Write(Stage + "/README.md",
                System.IO.File.Exists(doc)
                    ? System.IO.File.ReadAllText(doc, Encoding.UTF8)
                    : "# IMUNROK UI\n\n사용법 문서를 찾지 못했다 (" + DocPath + ").\n"));

            AssetDatabase.Refresh();
            return paths;
        }

        /// <summary>
        /// 파일 이름과 어긋나는 <b>형(型) 이름</b>을 함께 갈아 준다.
        ///
        /// 꾸러미로 나갈 때 파일 이름을 바꾸는 것이 몇 개 있는데(견우 냄새를 빼려고),
        /// 안의 클래스 이름은 그대로 남아 <c>UiFontBaker.cs</c> 안에 <c>GyeonuFontBaker</c> 가
        /// 앉아 있었다. 정적 클래스라 <b>컴파일은 된다</b> — 파일명 = 클래스명 규칙은
        /// <c>MonoBehaviour</c> 에만 걸린다. 그래도 남에게 줄 물건이라 맞춘다.
        /// </summary>
        static readonly (string From, string To)[] TypeRenames =
        {
            ("GyeonuFontBaker", "UiFontBaker"),
        };

        /// <summary>.cs 사본 — 이름 공간과 어긋난 형 이름만 바꿔 옮긴다.</summary>
        static string CopyCs(string src, string dst)
        {
            string abs = System.IO.Path.Combine(ProjectRoot, src);
            if (!System.IO.File.Exists(abs))
            {
                Debug.LogError("[UI 꾸러미] 원본이 없다: " + src);
                return Write(dst, "// 원본을 찾지 못했다: " + src + "\n");
            }
            string text = System.IO.File.ReadAllText(abs, Encoding.UTF8).Replace(FromNs, ToNs);
            foreach (var r in TypeRenames) text = text.Replace(r.From, r.To);
            return Write(dst, text);
        }

        static string Write(string assetPath, string text)
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(ProjectRoot, assetPath), text, new UTF8Encoding(false));
            return assetPath;
        }

        static string ProjectRoot
        {
            get { return Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length); }
        }

        // ── 어셈블리 정의 ─────────────────────────────────────
        //    autoReferenced: true 라 받는 쪽의 평범한 스크립트(Assembly-CSharp)는 참조를
        //    따로 걸지 않아도 바로 쓸 수 있다. 자기 asmdef 를 쓰는 코드만 IMUNROK.Ui 를 추가하면 된다.
        const string RuntimeAsmdef = @"{
    ""name"": ""IMUNROK.Ui"",
    ""rootNamespace"": ""IMUNROK.Ui"",
    ""references"": [""Unity.InputSystem"", ""UnityEngine.UI"", ""Unity.TextMeshPro"", ""Unity.RenderPipelines.Universal.Runtime""],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}";

        const string EditorAsmdef = @"{
    ""name"": ""IMUNROK.Ui.Editor"",
    ""rootNamespace"": ""IMUNROK.Ui.EditorTools"",
    ""references"": [""IMUNROK.Ui"", ""Unity.TextMeshPro"", ""Unity.TextMeshPro.Editor""],
    ""includePlatforms"": [""Editor""],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}";

        // ── 보고 ──────────────────────────────────────────────
        static void Report(string outPath, List<string> paths, bool withFonts)
        {
            var sb = new StringBuilder();
            long size = System.IO.File.Exists(outPath) ? new System.IO.FileInfo(outPath).Length : 0;
            sb.AppendLine("[UI 꾸러미] " + outPath);
            sb.AppendLine("  크기 " + (size / 1024f / 1024f).ToString("F2") + " MB   담은 것 " + paths.Count + "개"
                          + (withFonts ? "   (글꼴 포함)" : "   (글꼴 없음 — 따로 전달할 것)"));
            for (int i = 0; i < paths.Count; i++) sb.AppendLine("    " + paths[i]);
            Debug.Log(sb.ToString());
        }
    }
}
