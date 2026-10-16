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

        /// <summary>
        /// 지금 지도가 <b>펴져 있나</b>. 부르기 판이 이것을 보고 물러난다 —
        /// 앉은 채로 M 을 누르면 지도와 사람 이름표가 화면 한가운데서 겹쳤다.
        /// </summary>
        public static bool AnyOpen { get; private set; }
        public void Toggle() => _open = !_open;
        public void Open() => _open = true;
        public void Close() => _open = false;


        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // <b>치는 동안에는 안 듣는다.</b> 「지도」를 치면 ㅈㅣㄷㅗ 사이에 든 글쇠가
            // 그대로 명령이 된다 — 묻는 말을 치다가 지도가 펴진다.
            if (Typing.Now) return;

            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) _open = !_open;
#endif
        }

        // 지도는 월드 공간 알림판으로 띄운다.
        private bool _shown;

        private void LateUpdate()
        {
            // 수첩·문서·심문 중에는 접는다. 읽거나 묻는 중에 지도가 겹치면
            // 어느 쪽을 보라는 것인지가 없어진다.
            bool want = _open && !JournalView.AnyOpen && !DocumentView.IsOpen
                              && !InterrogationController.AnyOpen;
            AnyOpen = want;
            if (want == _shown) return;
            _shown = want;
            if (want) WorldNotice.ShowImage("지도", _mapImage, -0.05f, _title);
            else WorldNotice.Hide("지도");
        }
    }
}
