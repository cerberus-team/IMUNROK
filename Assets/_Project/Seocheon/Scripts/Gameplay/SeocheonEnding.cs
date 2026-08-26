// 1부 마지막 연출 이벤트에 연결해 판결 기록 없이 서천꽃밭 씬으로 전환합니다.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Seocheon
{
    public sealed class SeocheonEnding : MonoBehaviour
    {
        [Tooltip("1부 종료 후 로드할 2부 씬 이름")]
        [SerializeField] private string flowerfieldSceneName = "Seocheon_Flowerfield";

        public void LoadFlowerfield()
        {
            // 서천 사건의 SetVerdict는 2부 마지막에서만 호출합니다.
            if (string.IsNullOrEmpty(flowerfieldSceneName))
            {
                Debug.LogError("[SeocheonEnding] 2부 씬 이름이 비어 있습니다.", this);
                return;
            }

            SceneManager.LoadScene(flowerfieldSceneName);
        }
    }
}
