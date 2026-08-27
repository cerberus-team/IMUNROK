using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>사랑채부터 시작하는 단추 하나.</b>
    /// 메뉴: [이문록 ▸ 바로 시작 ▸ 사랑채] (Ctrl+Shift+O)
    ///
    /// 옹고집 한 판은 대문 두드리기 → 마름 → 복동이 앞장서기 → 사랑채 → 마당 순이다.
    /// 아궁이 하나를, 발소리 하나를 확인하려고 그 줄을 매번 처음부터 따라가는 것은
    /// 만드는 사람에게만 드는 값이고, <b>그 값이 확인을 안 하게 만든다</b>.
    ///
    /// 이 단추가 하는 일은 셋뿐이다.
    ///   ① 옹고집 씬을 연다(다른 씬을 보고 있었으면 저장할지 먼저 묻는다)
    ///   ② <see cref="StartAt"/> 에 쪽지를 한 장 남긴다
    ///   ③ 재생을 켠다
    ///
    /// 사람을 옮기는 일은 여기서 안 한다 — <see cref="QuickStart"/> 가 재생 중에 한다.
    /// 여기서 씬의 카메라를 옮겨 두면 <b>그 자리가 씬에 저장될 수 있다</b>.
    /// 지름길 하나 내려다 남의 시작 자리를 옮겨 커밋하는 것이 제일 나쁘다.
    ///
    /// 쪽지는 <b>한 번 쓰고 지워진다</b>. 다음에 그냥 재생을 누르면 처음부터다.
    /// </summary>
    public static class QuickStartMenu
    {
        private const string CasePath = "Assets/_Project/Onggojip/Scenes/Onggojip.unity";

        [MenuItem("이문록/바로 시작/사랑채 %#o")]
        public static void Sarangchae()
        {
            if (EditorApplication.isPlaying)
            {
                // 재생 중에 씬을 열 수는 없다. 멈추고 다시 누르라고 이르는 편이
                // 반쯤 열린 채로 이상하게 도는 것보다 낫다.
                Debug.LogWarning("[바로 시작] 재생을 멈추고 다시 누르십시오.");
                return;
            }

            var open = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (open.path != CasePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(CasePath, OpenSceneMode.Single);
            }

            StartAt.Where = StartAt.사랑채;
            Debug.Log("[바로 시작] 사랑채부터 — 재생을 켠다. (이 한 판만이고, 다음 재생은 처음부터다)");
            EditorApplication.isPlaying = true;
        }

        [MenuItem("이문록/바로 시작/처음부터(대문)")]
        public static void FromGate()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[바로 시작] 재생을 멈추고 다시 누르십시오.");
                return;
            }

            var open = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (open.path != CasePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(CasePath, OpenSceneMode.Single);
            }

            // 앞서 쪽지를 남겨 놓고 재생을 안 켠 적이 있으면 여기서 지운다.
            // 쪽지가 남아 있는 줄 모르고 눌렀다가 사랑채에서 시작하는 일이 없게.
            StartAt.Where = "";
            EditorApplication.isPlaying = true;
        }
    }
}
