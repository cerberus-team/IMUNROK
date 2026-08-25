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

        private static int Line(System.Text.StringBuilder sb, bool ok, string what, string value, string how)
        {
            sb.AppendLine("  " + (ok ? "○" : "✗") + " " + what.PadRight(18) + value);
            if (!ok) sb.AppendLine("       → " + how);
            return ok ? 0 : 1;
        }
    }
}
