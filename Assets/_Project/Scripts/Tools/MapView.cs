using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 사건 지도 — M 키 또는 도구벨트의 '지도'로 펼친다.
    /// 챕터마다 _mapImage를 그 사건 지도로 바꿔 끼우면 된다(코드 수정 없음).
    /// </summary>
    public class MapView : MonoBehaviour
    {
        [Tooltip("이 챕터(사건)의 지도 이미지. 챕터마다 다르게 지정")]
        [SerializeField] private Texture2D _mapImage;
        [SerializeField] private string _title = "사건 지도";

        private bool _open;
        public bool IsOpen => _open;
        public void Toggle() => _open = !_open;
        public void Open() => _open = true;
        public void Close() => _open = false;


        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) _open = !_open;
#endif
        }

        // 지도는 월드 공간 알림판으로 띄운다.
        private bool _shown;

        private void LateUpdate()
        {
            bool want = _open && !JournalView.AnyOpen;   // 수첩을 펼치면 지도는 접는다
            if (want == _shown) return;
            _shown = want;
            if (want) WorldNotice.ShowImage("지도", _mapImage, -0.05f, _title);
            else WorldNotice.Hide("지도");
        }
    }
}
