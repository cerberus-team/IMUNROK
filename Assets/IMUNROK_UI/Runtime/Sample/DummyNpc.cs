using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 더미 NPC — 바라보고 누르면 대화창이 열린다.
    /// 말 상대는 <see cref="UiDialogue.Dummy"/> 라 정해진 대꾸만 돌아온다.
    /// 자기 NPC 를 붙이려면 <see cref="IDialogueSpeaker"/> 를 구현하고,
    /// <see cref="IDialogueBackend"/> 를 만들어 <c>DialogueUI.Ensure().Open(화자, 상대)</c> 를 부르면 된다.
    /// </summary>
    [AddComponentMenu("이문록 UI/더미 NPC (DummyNpc)")]
    public class DummyNpc : FocusInteractable, IDialogueSpeaker
    {
        [Tooltip("이름패에 뜰 이름")]
        public string speakerName = "이름 없는 이";

        [Tooltip("대화창 배치안 — 하단 가로 바가 확정안이다. 나머지는 지난 시안")]
        public DialogueLayout layout = DialogueLayout.하단바_확정;

        UiDialogue.Dummy backend;

        public override string Prompt { get { return "말 걸기"; } }

        // ── IDialogueSpeaker ──
        DialogueLayout IDialogueSpeaker.Layout { get { return layout; } }
        bool IDialogueSpeaker.VoiceAutoSend { get { return true; } }

        public override void Interact(GameObject actor)
        {
            if (backend == null) backend = new UiDialogue.Dummy(speakerName);
            DialogueUI.Ensure().Open(this, backend);
        }

        /// <summary>대화창은 끌어 돌릴 것이 없다.</summary>
        public override void HandleDrag(Vector2 delta) { }
    }
}
