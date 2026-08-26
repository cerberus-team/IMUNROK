using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>헤드셋을 쓸지 말지 한 자리에서 끈다.</b>
    /// 메뉴: [이문록 ▸ VR ▸ 헤드셋 쓰기] — 체크 표시가 곧 지금 값이다.
    ///
    /// 링크가 꺼져 있는데 XR 을 올려 보면, 오큘러스 런타임은 "없다"고 대답하기까지
    /// <b>5초 넘게 주 실을 붙잡는다</b>. 그 두드림이 <b>두 군데</b>서 온다는 것이
    /// 이 스위치가 필요한 까닭이다 — 한 쪽만 꺼서는 조용해지지 않는다.
    ///
    ///   ① <b>유니티가 게임 뜰 때 한 번</b>
    ///      Project Settings ▸ XR Plug-in Management ▸ Initialize XR on Startup.
    ///      이것이 재생 직후의 첫 멈춤이다.
    ///   ② <b>VRRig 가 뒤늦게 켤 사람을 위해 몇 번 더</b>
    ///      "재생을 누른 뒤에 링크를 켜도 붙게" 하려고 둔 두드림이다.
    ///
    /// 끄면 재생이 곧바로 뜨고, 켜면 도로 헤드셋을 찾는다.
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class VrMenu
    {
        private const string Item = "이문록/VR/헤드셋 쓰기";
        private const string XrAsset = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";

        [MenuItem(Item, true)]
        private static bool Check()
        {
            Menu.SetChecked(Item, !VrPref.HeadsetOff);
            return true;
        }

        [MenuItem(Item)]
        private static void Toggle()
        {
            bool use = VrPref.HeadsetOff;      // 지금 꺼져 있으면 켜는 것이 된다
            VrPref.HeadsetOff = !use;
            SetUnityAutoInit(use);

            Debug.Log(use
                ? "[VR] 헤드셋 쓰기 — 켰습니다.\n"
                + "   · 유니티가 게임 뜰 때 XR 을 올립니다\n"
                + "   · VRRig 도 몇 번 더 두드립니다(재생 뒤에 링크를 켜도 붙게)\n"
                + "   링크가 꺼져 있으면 그 두드림 한 번마다 5초씩 화면이 멈춥니다."
                : "[VR] 헤드셋 쓰기 — 껐습니다.\n"
                + "   · 시작할 때 XR 켜기: 꺼짐\n"
                + "   · VRRig 두드림: 없음\n"
                + "   재생이 곧바로 뜹니다. 헤드셋을 다시 쓰려면 같은 자리를 한 번 더 누르십시오.");
        }

        /// <summary>
        /// 유니티 제 몫의 자동 XR 시작을 켜고 끈다.
        ///
        /// <b>PC 쪽만</b> 건드린다. 안드로이드(퀘스트 빌드) 쪽까지 꺼 버리면 빌드해서
        /// 헤드셋에 얹었을 때 평면으로 뜬다 — 책상 사정 때문에 빌드를 망칠 일이 아니다.
        /// </summary>
        private static void SetUnityAutoInit(bool on)
        {
            int n = 0;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(XrAsset))
            {
                if (o == null || o.GetType().Name != "XRGeneralSettings") continue;
                if (o.name.IndexOf("Standalone") < 0) continue;   // PC 쪽만
                var so = new SerializedObject(o);
                var p = so.FindProperty("m_InitManagerOnStart");
                if (p == null) continue;
                p.boolValue = on;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(o);
                n++;
            }
            if (n == 0)
                Debug.LogWarning("[VR] XR 설정 파일을 못 찾았습니다(" + XrAsset + "). "
                               + "VRRig 두드림만 껐습니다 — 유니티가 뜰 때 한 번 올려 보는 것은 그대로입니다.");
            else
                AssetDatabase.SaveAssets();
        }
    }
}
