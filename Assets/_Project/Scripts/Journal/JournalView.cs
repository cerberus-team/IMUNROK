using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 수첩(手帖) — I 로 펼쳐 지금 사건에서 <b>주운 물증</b>을 본다.
    /// 심문 중이면 각 물증에 "들이밀기"(증거 제시) 버튼이 붙는다.
    ///
    /// <b>왜 I 인가</b>: J 는 왼손 검지가 놓이는 자리라 걷기(WASD)와 멀고, 오른손으로
    /// 옮겨 잡아야 눌린다. I 는 조사(調査)의 머리글자이기도 하다.
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
            // 수첩은 두 손으로 펴 드는 것이다. 그 앞에 떠 있던 것들은 무릎께로 내려놓는다 —
            // 자막도, 도구벨트도, 쥐고 있던 종이도. 끄지 않고 내려놓는 까닭은 없어진 것과
            // 잠시 내려둔 것이 손에 남는 느낌이 다르기 때문이다.
            WorldHudAnchor.StowAll = true;
            JournalPanel.Open(this);
        }

        public void Close()
        {
            _open = false;
            AnyOpen = false;
            WorldHudAnchor.StowAll = false;
            JournalPanel.Close();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // 글을 치는 동안에는 안 듣는다 — 「이」의 첫 소리가 <c>d</c>… 가 아니라
            // 로마자 i 그대로라, 영문으로 무엇을 치든 수첩이 펄럭인다.
            if (Typing.Now) return;
            var kb = Keyboard.current;
            if (kb != null && kb.iKey.wasPressedThisFrame) Toggle();
#endif
        }

        // ─────────────────────────────────────────────
        //  그리기는 JournalPanel(월드 공간 Canvas)이 맡는다.
        //  OnGUI는 헤드셋에 렌더링되지 않아 VR에서 수첩이 통째로 안 보였고,
        //  데스크탑에선 월드 UI와 겹쳐 보여 오히려 가렸다.
        // ─────────────────────────────────────────────
    }
}
