using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 열어 보면 한 줄 — <b>안에 아무것도 없을 때</b>를 위해 있다.
    ///
    /// 열리는 것이 죄다 증거를 물고 있으면 여는 일은 조사가 아니라 답 맞히기가 된다.
    /// 그러니 헛걸음도 있어야 하는데, 열었더니 정말 아무 반응이 없으면 이번에는
    /// "고장인가" 싶어진다. 그래서 헛걸음에도 <b>헛걸음이라는 대답</b>을 준다.
    ///
    /// 붙이는 곳: <see cref="DoorController"/> 가 달린 세간. 다 열린 순간에 한 번 뜬다.
    /// </summary>
    [RequireComponent(typeof(DoorController))]
    public class SayWhenOpened : MonoBehaviour
    {
        [TextArea(2, 3)]
        [Tooltip("다 열렸을 때 물건 위에 뜨는 한 줄")]
        [SerializeField] private string _line = "안은 비어 있다.";
        [Tooltip("한 번만 말할 것인가. 끄면 열 때마다 말한다")]
        [SerializeField] private bool _once = true;

        private DoorController _door;
        private bool _said;
        private bool _wasOpen;

        private void Awake() { _door = GetComponent<DoorController>(); }

        private void Update()
        {
            if (_door == null) return;
            bool open = _door.IsOpen;
            if (open && !_wasOpen && !(_once && _said))
            {
                WorldNote.Show(transform, _line);
                _said = true;
            }
            _wasOpen = open;
        }
    }
}
