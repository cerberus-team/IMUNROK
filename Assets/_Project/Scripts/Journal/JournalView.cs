using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 수첩(手帖) — J(또는 도구벨트)로 펼쳐 지금 사건의 단서를 본다.
    /// 심문 중이면 각 단서에 "들이밀기"(증거 제시) 버튼이 붙는다.
    ///
    /// 이 클래스는 열림/닫힘 상태만 들고 있고, 그리기는 JournalPanel(월드 Canvas)이 맡는다.
    /// </summary>
    public class JournalView : MonoBehaviour
    {
        [SerializeField] private bool _startOpen = false;

        [TextArea(3, 10)]
        [Tooltip("사건 개요(봉서 요지). 처음부터 볼 수 있음")]
        [SerializeField] private string _caseBrief = "";

        [Tooltip("수첩에 쓸 한글 폰트(비우면 씬의 다른 UI가 올려둔 공용 폰트)")]
        [SerializeField] private Font _font;

        private bool _open;

        /// <summary>수첩 첫 장에 적히는 사건 개요(봉서 요지).</summary>
        public string CaseBrief => _caseBrief;

        /// <summary>수첩이 펼쳐져 있나(다른 UI가 참고해 자기를 숨김).</summary>
        public static bool AnyOpen { get; private set; }

        private void Awake() => UiFont.Publish(_font);

        private void Start() { if (_startOpen) Open(); }

        public void Toggle() { if (_open) Close(); else Open(); }

        public void Open()
        {
            _open = true;
            AnyOpen = true;
            JournalPanel.Open(this);
        }

        public void Close()
        {
            _open = false;
            AnyOpen = false;
            JournalPanel.Close();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.jKey.wasPressedThisFrame) Toggle();
#endif
        }

        // ─────────────────────────────────────────────
        //  그리기는 JournalPanel(월드 공간 Canvas)이 맡는다.
        //  OnGUI는 헤드셋에 렌더링되지 않아 VR에서 수첩이 통째로 안 보였고,
        //  데스크탑에선 월드 UI와 겹쳐 보여 오히려 가렸다.
        // ─────────────────────────────────────────────
    }
}
