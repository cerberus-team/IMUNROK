using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 문을 열면 씬이 넘어가는 출구.
    ///
    /// <see cref="SceneExit"/> 가 "볼륨을 밟거나 대상을 클릭하면 전환"이라면, 이쪽은
    /// **문이 실제로 열리는 것을 보고 나서** 전환한다. 여닫이 연출을 잘라먹지 않으려고
    /// <see cref="delayAfterOpen"/> 만큼 문이 열리는 모습을 보여준 뒤 암전을 시작한다.
    ///
    /// 문 종류(HingeDoor·DoubleHingeDoor·LockedDoor)를 가리지 않는다 —
    /// <see cref="IOpenable"/> 만 보므로 잠긴 문은 풀리기 전까지 저절로 발동하지 않는다.
    ///
    /// 붙이는 곳: 문 컴포넌트와 **같은 GameObject** 이거나, door 필드로 지정한 문.
    /// (같은 GO에 두면 Interactable이 둘이 되어 조준 대상이 모호해지므로,
    ///  이 컴포넌트는 Interactable을 상속하지 않는다.)
    /// </summary>
    [AddComponentMenu("이문록/문 씬 출구 (DoorSceneExit)")]
    [DisallowMultipleComponent]
    public class DoorSceneExit : MonoBehaviour
    {
        [Header("감시할 문 (비우면 같은 GameObject에서 찾는다)")]
        public MonoBehaviour door;

        [Header("목적지")]
        public string targetScene = "";
        public string targetSpawn = "";

        [Header("타이밍")]
        [Tooltip("문이 열리기 시작하고 이만큼 보여준 뒤 암전을 시작한다")]
        public float delayAfterOpen = 0.55f;
        public float fadeOut = 0.45f;
        public float fadeIn = 0.55f;

        [Tooltip("한 번 전환하면 다시 발동하지 않는다 (문을 닫았다 열어도)")]
        public bool once = false;

        IOpenable _openable;
        bool _wasOpen;
        bool _armed;      // 열림을 감지해 대기 중
        float _openAt;
        bool _spent;

        void Awake()
        {
            _openable = door as IOpenable;
            if (_openable == null) _openable = GetComponent<IOpenable>();
            if (_openable == null)
                Debug.LogWarning($"[문출구] {name} — 감시할 문(IOpenable)을 찾지 못했다.", this);
        }

        void Update()
        {
            if (_openable == null) return;
            if (_spent && once) return;

            bool open = _openable.IsOpen;

            // 닫힘 → 열림으로 바뀌는 순간에만 예약한다 (열려 있는 내내 재발동하지 않게)
            if (open && !_wasOpen && !_armed)
            {
                _armed = true;
                _openAt = Time.time;
            }
            // 대기 중에 도로 닫으면 취소 — 플레이어가 마음을 바꾼 것이다
            if (!open && _armed)
            {
                _armed = false;
            }
            _wasOpen = open;

            if (!_armed) return;
            if (Time.time - _openAt < delayAfterOpen) return;
            if (SceneTransition.IsTransitioning) return;

            _armed = false;
            _spent = true;
            SceneTransition.Go(targetScene, targetSpawn, fadeOut, fadeIn);
        }
    }
}
