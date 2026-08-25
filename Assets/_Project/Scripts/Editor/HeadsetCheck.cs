using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>헤드셋이 붙을 채비가 됐나 한 번에 본다.</b>
    /// 메뉴: [이문록 ▸ 헤드셋 ▸ 준비됐나 보기]
    ///
    /// 링크가 안 붙을 때 볼 데가 여러 군데로 흩어져 있다 — 빌드 대상, XR 꾸러미 설정,
    /// 색공간, 씬에 섞여 든 OVR 리그, 그리고 <b>헤드셋 쪽 사정</b>. 하나씩 뒤지다 보면
    /// 무엇을 이미 봤는지도 잊는다. 한 번 눌러 다 찍는다.
    ///
    /// <b>USB 냐 무선이냐는 이 표와 상관없다.</b> 케이블은 통로일 뿐이고, 유니티가 말을
    /// 거는 상대는 PC 에 깔린 <b>Meta Quest Link 앱</b>이다. 그래서 케이블을 꽂아도
    /// 그 앱이 없으면 안 붙는다 — 자주 걸리는 데라 아래 안내에 적어 둔다.
    /// </summary>
    public static class HeadsetCheck
    {
        [MenuItem("이문록/헤드셋/준비됐나 보기")]
        public static void Check()
        {
            var sb = new System.Text.StringBuilder("[헤드셋] 준비 점검\n");
            int bad = 0;

            // ── 프로젝트 쪽 ──
            var target = EditorUserBuildSettings.activeBuildTarget;
            bool pc = target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows;
            bad += Line(sb, pc, "빌드 대상", target.ToString(),
                        "링크로 쓰려면 PC(StandaloneWindows64) 여야 한다. Android 로 두면 재생해도 평면이다");

            bool loader = false, initOnStart = false;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/XR/XRGeneralSettingsPerBuildTarget.asset"))
            {
                if (o == null || o.GetType().Name != "XRGeneralSettings" || o.name.IndexOf("Standalone") < 0) continue;
                var so = new SerializedObject(o);
                var init = so.FindProperty("m_InitManagerOnStart");
                initOnStart = init != null && init.boolValue;
                var mgr = so.FindProperty("m_LoaderManagerInstance").objectReferenceValue;
                if (mgr != null)
                {
                    var mso = new SerializedObject(mgr);
                    var l = mso.FindProperty("m_Loaders");
                    for (int i = 0; l != null && i < l.arraySize; i++)
                    {
                        var v = l.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (v != null && v.GetType().Name.IndexOf("Oculus") >= 0) loader = true;
                    }
                }
            }
            bad += Line(sb, loader, "PC 쪽 XR 로더", loader ? "OculusLoader" : "없음",
                        "Project Settings ▸ XR Plug-in Management ▸ PC 탭에서 Oculus 를 켤 것");
            bad += Line(sb, initOnStart, "시작할 때 XR 켜기", initOnStart ? "켜짐" : "꺼짐",
                        "꺼져 있으면 재생해도 헤드셋을 안 찾는다");
            bad += Line(sb, PlayerSettings.colorSpace == ColorSpace.Linear, "색공간",
                        PlayerSettings.colorSpace.ToString(), "Gamma 면 헤드셋에서 색이 바래 보인다");

            // 씬에 OVR 리그가 섞여 들면 머리가 둘이 된다(VRRig 가 카메라를 옮기는데
            // OVRCameraRig 가 제 카메라를 또 세운다). 프리팹은 세지 않는다.
            int ovr = 0;
            foreach (var mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (string.IsNullOrEmpty(mb.gameObject.scene.name)) continue;   // 프리팹 에셋
                string n = mb.GetType().Name;
                if (n == "OVRCameraRig" || n == "OVRPlayerController")
                { sb.AppendLine("   ※ " + mb.gameObject.scene.name + " 에 " + n + " 이 있다 — VRRig 와 머리를 두고 다툰다"); ovr++; }
            }
            bad += Line(sb, ovr == 0, "씬에 섞인 OVR 리그", ovr == 0 ? "없음" : ovr + "개",
                        "지우거나 꺼 둘 것. VRRig 가 있던 카메라를 그대로 옮겨 쓴다");

            // PC 쪽 런타임이 돌고 있나. 이게 없으면 헤드셋을 꽂아도 말 걸 상대가 없다.
            bool runtime = System.Diagnostics.Process.GetProcessesByName("OVRServer_x64").Length > 0;
            bad += Line(sb, runtime, "메타 런타임", runtime ? "돌고 있다 (OVRServer_x64)" : "안 돌고 있다",
                        "Meta Quest Link 앱을 켤 것. 케이블은 통로일 뿐이고 유니티가 말을 거는 상대는 이 앱이다");

            // ── 지금 붙어 있나 ──
            sb.AppendLine();
            if (!Application.isPlaying)
                sb.AppendLine("  (재생 중이 아니라 헤드셋이 붙었는지는 알 수 없다 — 재생하고 다시 눌러 볼 것)");
            else
            {
                sb.AppendLine("  XR 켜짐 = " + XRSettings.enabled + "   기기 = \"" + XRSettings.loadedDeviceName + "\"");
                var devices = new List<InputDevice>();
                InputDevices.GetDevices(devices);
                sb.AppendLine("  붙은 기기 " + devices.Count + "개");
                foreach (var d in devices) sb.AppendLine("     " + d.name + "  (" + d.characteristics + ")");
                sb.AppendLine("  VR 로 서 있나 = " + VRRig.Active);
            }

            // ── 지난 재생 때 XR 이 왜 안 올라왔나 ──
            sb.AppendLine();
            sb.AppendLine("── 지난 재생 때 XR 이 어떻게 됐나 (유니티 로그에서) ──");
            sb.AppendLine(Verdict());

            sb.AppendLine();
            sb.AppendLine("── 헤드셋 쪽에서 할 것 (USB 든 무선이든 같다) ──");
            sb.AppendLine("  ① PC 에 <b>Meta Quest Link 앱</b>을 깔고 로그인해 둘 것.");
            sb.AppendLine("     케이블은 통로일 뿐이고, 유니티가 말을 거는 상대는 이 앱이다 —");
            sb.AppendLine("     꽂아만 두고 앱이 없으면 안 붙는다.");
            sb.AppendLine("  ② 케이블은 <b>데이터가 다니는 것</b>이라야 한다. 충전 전용이면 안 붙는다.");
            sb.AppendLine("  ③ 헤드셋에서 빠른 설정 ▸ <b>Quest Link</b> 를 켤 것.");
            sb.AppendLine("  ④ 그리고 유니티에서 그냥 재생. 개발자 모드도 APK 도 필요 없다.");
            sb.AppendLine("     (재생을 먼저 눌러도 된다 — VRRig 가 몇 초 동안 다시 두드려 본다)");

            sb.AppendLine();
            sb.AppendLine(bad == 0 ? "  ▶ 프로젝트 쪽은 다 됐다. 위 넷만 하면 된다."
                                   : "  ▶ 고칠 것 " + bad + "가지가 위에 ✗ 로 찍혀 있다.");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// <b>유니티 로그가 이미 답을 알고 있다.</b>
        ///
        /// "헤드셋이 안 잡힌다"는 한 마디에 원인이 여럿 섞여 있는데, 로그에는 그중
        /// 무엇인지가 <b>또렷하게</b> 남는다. 그 한 줄을 사람이 찾으러 4.8MB 를
        /// 뒤지게 두지 말고 여기서 읽어 온다.
        ///
        /// 가장 헷갈리는 것이 <c>XR_ERROR_FORM_FACTOR_UNAVAILABLE</c> 이다.
        /// 이건 런타임이 없는 것도, 케이블이 안 꽂힌 것도 아니다 — <b>런타임은 멀쩡히
        /// 대답하는데 그 런타임에게 지금 헤드셋이 없다</b>는 뜻이다. 꽂아만 두고
        /// 헤드셋 안에서 링크를 켜지 않으면 딱 이렇게 된다. USB 장치는 꽂는 순간
        /// 잡히므로 장치 목록만 보면 다 붙은 것처럼 보이는 것이 함정이다.
        /// </summary>
        private static string Verdict()
        {
            string path = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Unity", "Editor", "Editor.log");
            if (!System.IO.File.Exists(path)) return "  로그를 못 찾았다 (" + path + ")";

            string tail;
            try
            {
                // 유니티가 쥐고 있는 파일이라 함께 읽기로 연다. 끝 512KB 면 넉넉하다.
                using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Open,
                           System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete))
                {
                    long take = System.Math.Min(fs.Length, 512 * 1024);
                    fs.Seek(-take, System.IO.SeekOrigin.End);
                    using (var sr = new System.IO.StreamReader(fs)) tail = sr.ReadToEnd();
                }
            }
            catch (System.Exception e) { return "  로그를 못 읽었다: " + e.Message; }

            if (tail.IndexOf("Unable to start Oculus XR Plugin", System.StringComparison.Ordinal) < 0 &&
                tail.IndexOf("Loading plugin OculusXRPlugin", System.StringComparison.Ordinal) >= 0)
                return Join(new[] { "  ○ XR 이 올라간 적이 있다." });

            // 여기가 가장 헷갈리는 자리다 — 아래 여러 줄로 풀어 적는다.
            if (tail.IndexOf("XR_ERROR_FORM_FACTOR_UNAVAILABLE", System.StringComparison.Ordinal) >= 0)
                return Join(new[] {
                    "  ✗ XR_ERROR_FORM_FACTOR_UNAVAILABLE",
                    "       런타임(Meta)은 <b>멀쩡히 대답하는데 헤드셋이 없다</b>고 한다.",
                    "       케이블 탓도, 프로그램이 없는 탓도 아니다 —",
                    "       <b>헤드셋 안에서 Quest Link 를 아직 안 켠 것</b>이다.",
                    "       USB 장치는 꽂는 순간 잡히므로, 장치 목록만 보면 다 붙은 것처럼 보인다.",
                    "       헤드셋을 쓰고 빠른 설정 ▸ Quest Link ▸ 시작을 누른 다음,",
                    "       <b>회색 링크 화면이 눈앞에 뜬 것을 보고</b> 재생할 것.",
                });

            if (tail.IndexOf("Unable to start Oculus XR Plugin", System.StringComparison.Ordinal) >= 0)
                return Join(new[] {
                    "  ✗ Unable to start Oculus XR Plugin — 런타임이 아예 안 잡혔다.",
                    "       Meta Quest Link 앱이 켜져 있는지, 최근에 깔았다면 컴퓨터를 다시 켰는지 볼 것.",
                });

            return "  (로그에 XR 을 올려 본 자취가 없다 — 아직 재생을 안 했거나 로그가 지워졌다)";
        }

        /// <summary>여러 줄을 이어 붙인다. C# 글월 안에 줄바꿈을 직접 넣지 않으려는 것이다.</summary>
        private static string Join(string[] lines) => string.Join(System.Environment.NewLine, lines);

        private static int Line(System.Text.StringBuilder sb, bool ok, string what, string value, string how)
        {
            sb.AppendLine("  " + (ok ? "○" : "✗") + " " + what.PadRight(18) + value);
            if (!ok) sb.AppendLine("       → " + how);
            return ok ? 0 : 1;
        }
    }
}
