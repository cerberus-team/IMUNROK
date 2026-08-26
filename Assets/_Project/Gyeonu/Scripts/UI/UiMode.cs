using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>UI가 서는 방식. 화면 구조는 하나(월드 캔버스)이고 이 값이 배치·입력만 가른다.</summary>
    public enum UiMode
    {
        /// <summary>모니터 + 마우스. 판이 화면에 붙박이고, 조준은 마우스 커서 광선이다.</summary>
        PC = 0,
        /// <summary>HMD + 컨트롤러. 판이 시야를 느슨히 따라가고, 조준은 컨트롤러 광선이다.</summary>
        VR = 1,
    }

    /// <summary>
    /// UI 모드 스위치 (2026-08-26).
    ///
    /// ■ 왜 한 벌인가
    ///   PC판·VR판을 따로 만들면 고칠 때마다 양쪽을 손봐야 하고, 한쪽만 고치면 갈라진다.
    ///   그래서 <b>화면 구조는 월드 스페이스 캔버스 하나로 통일</b>하고, 갈리는 것은
    ///   ① 어디에 얼마나 크게 세우는가(<see cref="UiTuning"/>)와
    ///   ② 무엇으로 가리키는가(<see cref="UiPointers"/>) 둘뿐이다.
    ///
    /// ■ 어떻게 정해지는가 — 수동이 자동을 이긴다
    ///   <see cref="Pref"/> 가 <see cref="Preference.자동"/> 이면 헤드셋 유무를 보고 고르고,
    ///   PC고정/VR고정이면 그대로 따른다. 헤드셋이 없는 지금은 자동 = PC 다.
    ///   <b>VR고정으로 두면 헤드셋 없이도 모니터에서 VR 배치를 미리 볼 수 있다</b> —
    ///   지금 우리가 VR을 검증할 수 있는 유일한 방법이다.
    ///
    /// ■ 바꾸는 곳 세 군데
    ///   ① 에디터 메뉴  Tools ▸ 이문록 ▸ UI 모드 ▸ 자동 / PC 고정 / VR 고정
    ///   ② Play 중 <b>F8</b> — 자동 ▸ PC고정 ▸ VR고정 순환 (UiModeWatcher)
    ///   ③ 나중에 옵션 화면이 붙으면 <see cref="Pref"/> 에 쓰기만 하면 된다.
    ///   선택은 <see cref="PrefKey"/> 로 PlayerPrefs에 남아 다음 실행까지 간다.
    ///
    /// ⚠️ 이 프로젝트에는 아직 XR 리그도, XR Plug-in Management 설정도 없다(2026-08-26 확인).
    ///    그래서 <see cref="HeadsetPresent"/> 는 늘 false 이고 자동은 항상 PC로 떨어진다.
    ///    리그가 붙는 순간 이 함수 하나가 true가 되면서 나머지가 저절로 따라온다.
    /// </summary>
    public static class UiModes
    {
        /// <summary>무엇을 우선할지. 자동이면 헤드셋 유무를 본다.</summary>
        public enum Preference { 자동 = 0, PC고정 = 1, VR고정 = 2 }

        public const string PrefKey = "Gyeonu.UiMode";

        static Preference _pref;
        static UiMode _current = UiMode.PC;
        static bool _loaded;

        /// <summary>모드가 바뀌었다. 판들이 구독해 자리를 다시 잡는다.</summary>
        public static event System.Action<UiMode> Changed;

        // 도메인 리로드가 꺼진 프로젝트 — 정적 필드가 플레이 세션을 넘겨 살아남으므로 직접 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            _loaded = false;
            _current = UiMode.PC;
            Load();
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _pref = (Preference)PlayerPrefs.GetInt(PrefKey, (int)Preference.자동);
            _current = Resolve();
        }

        /// <summary>수동 지정. 자동으로 되돌리려면 <see cref="Preference.자동"/>.</summary>
        public static Preference Pref
        {
            get { Load(); return _pref; }
            set
            {
                Load();
                if (_pref == value) return;
                _pref = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
                Refresh();
            }
        }

        /// <summary>지금 쓰는 모드.</summary>
        public static UiMode Current { get { Load(); return _current; } }

        public static bool IsVr => Current == UiMode.VR;
        public static bool IsPc => Current == UiMode.PC;

        /// <summary>
        /// 헤드셋이 실제로 돌고 있는가.
        ///
        /// XR 디스플레이 서브시스템이 <b>running</b> 인지를 본다. 패키지가 설치돼 있기만 하고
        /// 로더가 꺼져 있으면 false — "Meta XR SDK가 있으니 VR이겠지"로 판단하면 안 된다.
        /// </summary>
        public static bool HeadsetPresent
        {
            get
            {
#if UNITY_2020_1_OR_NEWER
                var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
                UnityEngine.SubsystemManager.GetSubsystems(displays);
                for (int i = 0; i < displays.Count; i++)
                    if (displays[i] != null && displays[i].running) return true;
#endif
                return UnityEngine.XR.XRSettings.isDeviceActive;
            }
        }

        static UiMode Resolve()
        {
            switch (_pref)
            {
                case Preference.PC고정: return UiMode.PC;
                case Preference.VR고정: return UiMode.VR;
                default: return HeadsetPresent ? UiMode.VR : UiMode.PC;
            }
        }

        /// <summary>헤드셋이 붙거나 빠졌을 수 있다 — 다시 판정하고, 바뀌었으면 알린다.
        /// <see cref="UiModeWatcher"/> 가 주기적으로 부른다.</summary>
        public static void Refresh()
        {
            Load();
            var next = Resolve();
            if (next == _current) return;
            _current = next;
            Debug.Log("[UI 모드] " + next + " (설정: " + _pref + ")");
            if (Changed != null) Changed(next);
        }

        /// <summary>F8이 도는 순서 — 자동 ▸ PC고정 ▸ VR고정 ▸ 자동.</summary>
        public static void Cycle()
        {
            Pref = (Preference)(((int)Pref + 1) % 3);
            Debug.Log("[UI 모드] 설정 = " + Pref + " ▸ 지금 " + Current);
        }
    }
}
