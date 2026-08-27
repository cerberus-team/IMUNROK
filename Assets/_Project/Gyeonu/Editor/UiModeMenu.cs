using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// UI 모드 전환 메뉴 (2026-08-26) — <c>Tools ▸ 이문록 ▸ UI 모드</c>.
    ///
    /// ■ 왜 에디터 메뉴가 필요한가
    ///   헤드셋이 아직 없다. 자동 감지에만 맡기면 <b>VR 배치를 볼 방법이 영영 없다.</b>
    ///   여기서 「VR 고정」을 켜면 모니터에 VR 배치가 그대로 뜬다 — 판이 어디에 얼마나 크게
    ///   서는지, 글자가 몇 도인지 눈으로 보고 고칠 수 있다. 지금 우리가 가진 유일한 검증 수단이다.
    ///
    /// ■ Play 중에는 <b>F8</b> 로도 돈다 (<see cref="UiModeWatcher"/>).
    ///   선택은 PlayerPrefs에 남아 다음 실행까지 간다.
    /// </summary>
    public static class UiModeMenu
    {
        const string Root = "Tools/이문록/UI 모드/";
        const string Auto = Root + "자동 (헤드셋 감지)";
        const string Pc = Root + "PC 고정";
        const string Vr = Root + "VR 고정 (헤드셋 없이 배치 확인)";
        const string Info = Root + "지금 상태 찍어 보기";

        [MenuItem(Auto, priority = 100)]
        static void SetAuto() { UiModes.Pref = UiModes.Preference.자동; Report(); }
        [MenuItem(Auto, validate = true)]
        static bool ValAuto() { Menu.SetChecked(Auto, UiModes.Pref == UiModes.Preference.자동); return true; }

        [MenuItem(Pc, priority = 101)]
        static void SetPc() { UiModes.Pref = UiModes.Preference.PC고정; Report(); }
        [MenuItem(Pc, validate = true)]
        static bool ValPc() { Menu.SetChecked(Pc, UiModes.Pref == UiModes.Preference.PC고정); return true; }

        [MenuItem(Vr, priority = 102)]
        static void SetVr() { UiModes.Pref = UiModes.Preference.VR고정; Report(); }
        [MenuItem(Vr, validate = true)]
        static bool ValVr() { Menu.SetChecked(Vr, UiModes.Pref == UiModes.Preference.VR고정); return true; }

        [MenuItem(Info, priority = 120)]
        static void Report()
        {
            var l = UiTuning.Compute(Camera.main);
            Debug.Log(string.Format(
                "[UI 모드] 설정 {0} ▸ 지금 {1}\n" +
                "  헤드셋 감지 = {2}   (XR 리그·로더가 없으면 늘 False)\n" +
                "  판 거리 {3:F2} m / '화면' {4:F0}×{5:F0} 단위 / 1단위 = {6:F3}°\n" +
                "  글자 각도 — 14단위 {7:F2}° · 18단위 {8:F2}° · 22단위 {9:F2}°  (VR 편안한 하한 ≈ 1.3°)",
                UiModes.Pref, UiModes.Current, UiModes.HeadsetPresent,
                l.distance, l.refWidth, l.refHeight, l.DegPerUnit,
                l.TextDeg(14), l.TextDeg(18), l.TextDeg(22)));
        }
    }
}
