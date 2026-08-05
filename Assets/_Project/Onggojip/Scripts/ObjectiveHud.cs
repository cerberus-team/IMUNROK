using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using IMUNROK.Common;

namespace IMUNROK.Onggojip
{
    /// <summary>
    /// 1막 목표 안내 HUD — 화면 상단에 "지금 뭘 할지"를 보여준다(길 잃음 방지).
    /// 진행에 따라 문구가 바뀐다:
    ///   · 필수 단서(J04·J09·J13·J15) 부족 → "집 안을 조사하라 (필수 N/4)"
    ///   · 필수 단서 다 모음         → "출도하라! (F2 ▸ 출도)"
    /// H 키로 켜고 끌 수 있다. 아무 오브젝트(예: _OnggojipCase)에 붙이면 됨.
    /// </summary>
    public class ObjectiveHud : MonoBehaviour
    {
        [SerializeField] private bool _show = true;
        [TextArea] [SerializeField] private string _investigateLine = "밤이다. 몰래 집 안을 조사하라";
        [TextArea] [SerializeField] private string _readyLine = "증거를 충분히 모았다 — 출도하라!  (F2 ▸ 출도)";

        private const CaseId ThisCase = CaseId.Case1_Onggojip;
        private GUIStyle _main, _sub;

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                _show = !_show;
#endif
        }

        private void OnGUI()
        {
            if (!_show) return;
            if (InterrogationController.AnyOpen) return;   // 심문 중엔 목표 숨김
            var journal = Journal.Instance;
            if (journal == null) return;

            // 필수 단서 진행도
            int req = 0, have = 0;
            foreach (var k in OnggojipClues.RequiredForReveal)
            {
                req++;
                if (journal.HasClue(ThisCase, k)) have++;
            }
            // 발견한 잠행 단서 수
            int found = 0, total = 0;
            foreach (var c in OnggojipClues.All)
            {
                if (!c.stealth) continue;
                total++;
                if (journal.HasClue(ThisCase, c.key)) found++;
            }

            bool ready = have >= req;
            string main = ready ? _readyLine : _investigateLine;
            string sub = ready
                ? "필수 단서 완료"
                : $"필수 단서 {have}/{req}  ·  발견 {found}/{total}   (H: 목표 숨기기)";

            EnsureStyles();
            float w = 620f, h = 62f;
            float x = (Screen.width - w) * 0.5f;
            float y = 12f;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none);
            GUI.Label(new Rect(x, y + 8, w, 26), main, _main);
            GUI.Label(new Rect(x, y + 34, w, 22), sub, _sub);
        }

        private void EnsureStyles()
        {
            if (_main != null) return;
            _main = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.5f) }
            };
            _sub = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.75f) }
            };
        }
    }
}
