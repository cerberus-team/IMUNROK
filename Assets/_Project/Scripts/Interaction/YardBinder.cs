using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// 마당 씬이 올라올 때마다 <b>중문</b>을 이름으로 다시 물려 준다.
    ///
    /// 왜 필요한가: 김명관고택을 Onggojip_마당 씬으로 떼어내면서, 껍데기 씬에서 고택 안을
    /// 가리키던 참조가 끊겼다. 끊긴 것은 딱 둘이고 둘 다 <b>같은 문</b>을 가리킨다 —
    /// 순간이동 구역이 "중문이 열려야 넘어간다"고 볼 때, 그리고 甲이 그 문으로 나갈 때.
    /// 유니티는 씬을 건너뛰는 참조를 저장하지 못하므로 인스펙터로는 이을 수 없다.
    ///
    /// 마당 씬은 방에 들 때마다 내려가고 나올 때마다 다시 올라온다. 그러니 이 손은
    /// <b>올라올 때마다</b> 가야 한다 — 한 번 잇고 마는 것이 아니다.
    ///
    /// <b>이름으로 잇는 것이 위험하지 않은가</b>: 위험하다. 문 이름을 바꾸면 조용히 끊긴다.
    /// 그래서 못 찾으면 남김없이 경고로 찍는다 — 조용히 실패하지 않는 것이 여기서는
    /// 잇는 것보다 중요하다. ([[SarangbangBinder]] 와 같은 원칙)
    /// </summary>
    public class YardBinder : MonoBehaviour
    {
        [Tooltip("마당·고택이 들어 있는 씬 이름. 이 씬이 올라오면 잇는다")]
        [SerializeField] private string _sceneName = "Onggojip_마당";

        [Tooltip("찾을 문 이름")]
        [SerializeField] private string _doorName = "중문";

        [Header("마당 쪽 문을 가리켜야 하는 것들")]
        [SerializeField] private TeleportZone _teleport;
        [SerializeField] private BokdongController _gap;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryBindLoaded();
        }

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (s.name == _sceneName) Bind(s);
        }

        /// <summary>이미 올라와 있으면(에디터에서 두 씬을 함께 열어 둔 경우) 곧바로 잇는다.</summary>
        private void TryBindLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name == _sceneName) { Bind(s); return; }
            }
        }

        /// <summary>마당 씬 안에서 문을 찾아 물려 준다.</summary>
        public void Bind(Scene yard)
        {
            DoorController door = null;
            foreach (var root in yard.GetRootGameObjects())
            {
                foreach (var d in root.GetComponentsInChildren<DoorController>(true))
                    if (d.name == _doorName) { door = d; break; }
                if (door != null) break;
            }

            if (door == null)
            {
                Debug.LogWarning($"[마당 잇기] 마당 씬에서 '{_doorName}' 을 못 찾았습니다 — " +
                                 "이름이 바뀌었는지 확인하시오.", this);
                return;
            }

            if (_teleport != null) _teleport.BindRequiredDoor(door);
            if (_gap != null) _gap.BindDoor(door);
            Debug.Log($"[마당 잇기] '{_doorName}' 을 이었습니다.", this);
        }
    }
}
