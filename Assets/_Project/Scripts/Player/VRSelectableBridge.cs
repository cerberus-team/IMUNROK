using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// VR(메타 Interaction SDK / Building Blocks)의 상호작용 이벤트를
    /// 기존 <see cref="ISelectable"/> 로직에 연결하는 "다리".
    ///
    /// 지금은 헤드셋 없이 마우스(MouseRaySelector)로 테스트하지만,
    /// 나중에 Building Blocks로 레이 인터랙터를 넣으면 이 다리만 연결하면 된다.
    /// → 사건 큐브/봉서함의 선택 "로직"(CaseCube, BongseoBox)은 그대로 두고,
    ///   입력 "방식"만 마우스 → VR 컨트롤러로 갈아끼운다.
    ///
    /// [나중에 VR 붙일 때 순서]
    ///  1) Building Blocks로 Camera Rig + Controller + Ray Interactor 배치.
    ///  2) 선택 대상(사건 큐브/봉서함)에 메타의 Interactable + Collider를 붙이고,
    ///     메타의 PointableUnityEventWrapper 를 추가한다.
    ///  3) 그 Wrapper의 UnityEvent에 이 컴포넌트의 메서드를 연결:
    ///        WhenHover   → HandleHoverEnter
    ///        WhenUnhover → HandleHoverExit
    ///        WhenSelect  → HandleSelect
    ///  4) MouseRaySelector / DebugFlyCamera 는 VR 빌드에서 비활성화.
    /// </summary>
    public class VRSelectableBridge : MonoBehaviour
    {
        [Tooltip("연결할 ISelectable 구현체(비우면 이 오브젝트/부모/자식에서 자동 탐색)")]
        [SerializeField] private MonoBehaviour _selectableSource;

        private ISelectable _selectable;

        private void Awake()
        {
            _selectable = _selectableSource as ISelectable;

            if (_selectable == null)
                _selectable = GetComponentInParent<ISelectable>();
            if (_selectable == null)
                _selectable = GetComponentInChildren<ISelectable>();

            if (_selectable == null)
                Debug.LogWarning($"[VRSelectableBridge] '{name}' 에서 ISelectable을 찾지 못했습니다. " +
                                 $"_selectableSource를 지정하거나 같은 오브젝트에 CaseCube/BongseoBox를 두세요.");
        }

        // 아래 3개를 메타 PointableUnityEventWrapper의 UnityEvent에 연결한다.
        public void HandleHoverEnter() => _selectable?.OnHoverEnter();
        public void HandleHoverExit()  => _selectable?.OnHoverExit();
        public void HandleSelect()     => _selectable?.OnSelect();
    }
}
