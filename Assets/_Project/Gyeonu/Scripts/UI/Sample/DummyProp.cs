using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 더미 소품 — 바라보고 누르면 <b>퍼즐 화면의 공통 얼개</b>가 그대로 나온다:
    /// 눈앞으로 당겨지고, 비네트가 깔리고, <see cref="NotePanel"/> 에 문제 글이 뜨고,
    /// 아래에 상태·조작 두 줄(<see cref="FocusHintPanel"/>)이 붙는다.
    ///
    /// 자기 퍼즐을 만들 때 <b>이 파일을 본떠</b> <see cref="FocusInteractable"/> 를 상속하면 된다 —
    /// 실제 견우 퍼즐(혼천의·렌즈·서고)도 전부 이 얼개 위에 얹혀 있다.
    /// </summary>
    [AddComponentMenu("이문록 UI/더미 소품 (DummyProp)")]
    public class DummyProp : FocusInteractable
    {
        [Tooltip("포커스에 들어가면 왼쪽 위에 뜨는 문제 글")]
        [TextArea] public string note = "돌쩌귀에 긁힌 자국이 넉 줄. 세 줄은 깊고 한 줄은 얕다.";

        [Tooltip("아래 가운데에 뜨는 상태 줄 (비우면 안 뜬다)")]
        public string status = "";

        NotePanel panel;
        float turned;

        public override string Prompt { get { return "살펴보기"; } }
        public override string FocusHint { get { return "드래그 — 돌려 보기      " + UiWords.Back + " — 물러나기"; } }
        public override string FocusStatus { get { return string.IsNullOrEmpty(status) ? null : status; } }

        public override void HandleDrag(Vector2 delta)
        {
            turned += delta.x * 0.4f;
            transform.localRotation = Quaternion.Euler(0f, turned, 0f);
        }

        public override void OnFocusChanged(bool focused)
        {
            if (!focused) { if (panel != null) panel.body = null; return; }
            if (panel == null)
            {
                panel = NotePanel.Create(transform, "더미_문제판");
                panel.title = null;
                panel.width = 520f;
                panel.topLeftPx = new Vector2(26f, 26f);
                panel.bodySize = 17;
                panel.bodyBold = true;
                panel.padX = panel.padTop = panel.padBottom = 14f;
                panel.backColor = new Color(0f, 0f, 0f, 0.52f);
                panel.bodyColor = new Color(1f, 0.92f, 0.72f, 1f);
            }
            panel.body = note;
        }
    }
}
