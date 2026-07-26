using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 수첩(手帖) 뷰어 — J 키로 펼쳐 발견한 단서를 사건별로 보여준다.
    /// 지금은 OnGUI 패널(한글 확실히 표시), 나중에 VR에선 손에 드는 공책 UI로 교체.
    ///
    /// Journal(데이터)과 분리된 "보기 전용" 컴포넌트. Journal.OnClueAdded로
    /// 자동 갱신되지만, OnGUI가 매 프레임 최신 상태를 읽으므로 특별한 갱신 처리는 불필요.
    /// </summary>
    public class JournalView : MonoBehaviour
    {
        [Tooltip("시작할 때 펼친 상태로 둘지")]
        [SerializeField] private bool _startOpen = false;

        private bool _open;

        private GUIStyle _titleStyle;
        private GUIStyle _caseStyle;
        private GUIStyle _clueStyle;
        private GUIStyle _hintStyle;

        private void Awake() => _open = _startOpen;

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.jKey.wasPressedThisFrame)
                _open = !_open;
#endif
        }

        private void OnGUI()
        {
            EnsureStyles();

            // 닫힘 상태에서도 여는 법을 알려주는 작은 힌트
            if (!_open)
            {
                GUI.Label(new Rect(16, Screen.height - 30, 260, 24), "J : 수첩 열기", _hintStyle);
                return;
            }

            float w = 540f, h = 470f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none);

            float cx = x + 24f;
            float cy = y + 18f;
            GUI.Label(new Rect(cx, cy, w - 48, 30), "📖  수첩 (手帖)", _titleStyle);
            GUI.Label(new Rect(x, y + h - 30, w, 24), "(J : 닫기)", _hintStyle);
            cy += 44f;

            // 수첩은 "지금 들어가 있는 사건"의 단서만 보여준다.
            // 조사청(사건 밖)에서는 사건 단서가 보이지 않는다.
            var gs = GameState.Instance;
            if (!gs.InCase)
            {
                GUI.Label(new Rect(cx, cy, w - 48, 26), "조사청 (사건 밖)", _caseStyle);
                cy += 30f;
                GUI.Label(new Rect(cx + 16, cy, w - 64, 44),
                    "사건에 들어가면, 그 사건에서 모은 단서가 여기 보인다.", _clueStyle);
                return;
            }

            CaseId current = gs.CurrentCase.Value;
            GUI.Label(new Rect(cx, cy, w - 48, 26), CaseTitle(current), _caseStyle);
            cy += 30f;

            var clues = Journal.Instance.GetClues(current);
            if (clues.Count == 0)
            {
                GUI.Label(new Rect(cx + 16, cy, w - 64, 22), "· (아직 이 사건의 단서가 없다)", _clueStyle);
            }
            else
            {
                foreach (var c in clues)
                {
                    GUI.Label(new Rect(cx + 16, cy, w - 64, 22), "· " + c.text, _clueStyle);
                    cy += 24f;
                }
            }
        }

        private static string CaseTitle(CaseId id)
        {
            return id switch
            {
                CaseId.Case1_Onggojip => "제일사건 · 옹고집전",
                CaseId.Case2_Seocheon => "제이사건 · 서천꽃밭",
                CaseId.Case3_Gyeonu   => "제삼사건 · 견우직녀",
                _ => id.ToString(),
            };
        }

        private void EnsureStyles()
        {
            if (_clueStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            _caseStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.88f, 1f) }
            };
            _clueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, wordWrap = true,
                normal = { textColor = Color.white }
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(1f, 1f, 1f, 0.55f) }
            };
        }
    }
}
