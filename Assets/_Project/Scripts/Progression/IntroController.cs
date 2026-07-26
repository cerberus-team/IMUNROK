using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 장면 1 — 어전(도입). 플레이어는 왕 앞에 부복(무릎 꿇음)한 상태로 시작한다.
    /// 왕은 모델 없이 목소리+자막(지금은 자막만)으로 세 미제 사건을 내린다.
    /// 대사가 끝나면 세 문서가 하나씩 "주르륵" 밀려나오고, 하나를 집으면
    /// 그 사건을 시작하며 조사청으로 진입한다.
    ///
    /// 자막은 OnGUI(엔딩과 동일 방식)로 그린다 — 한글 폰트 에셋 없이 확실히 표시.
    /// </summary>
    public class IntroController : MonoBehaviour
    {
        [Header("진행")]
        [SerializeField] private float _secondsPerLine = 4.5f;
        [SerializeField] private string _speakerName = "왕";

        [Header("왕의 대사(인스펙터에서 수정 가능)")]
        [TextArea]
        [SerializeField]
        private string[] _kingLines =
        {
            "어사(御史)는 부복하라.",
            "괴이하다는 말로 닫힌 문서들이다.",
            "도술이라 하고, 신이 데려갔다 하고, 꽃이 되살렸다 한다.",
            "나는 그 말을 믿지 않는다.",
            "봉서와 마패, 유척을 내리니 — 가서 무엇이 있었는지 기록해 오라.",
        };

        [Tooltip("문서를 집으라는 안내(대사 후 표시)")]
        [SerializeField] private string _pickPrompt = "세 문서 중 하나를 집으라. 거기서부터 조사가 시작된다.";

        [Header("문서 등장 연출")]
        [SerializeField] private IntroDocument[] _documents;
        [Tooltip("문서 사이 등장 시차(초)")]
        [SerializeField] private float _docStagger = 0.4f;
        [SerializeField] private float _docSlideDuration = 0.6f;
        [Tooltip("문서가 왕 쪽(뒤)에서 밀려나오는 거리")]
        [SerializeField] private float _docFromDistance = 1.2f;

        private int _index;
        private float _timer;
        private bool _speechDone;
        private bool _docsRevealed;

        private GUIStyle _speakerStyle;
        private GUIStyle _lineStyle;
        private GUIStyle _hintStyle;

        private void Update()
        {
            if (_speechDone) return;

            _timer += Time.deltaTime;
            if (_timer >= _secondsPerLine || AdvancePressed())
                Next();
        }

        private void Next()
        {
            _timer = 0f;
            _index++;
            if (_index >= _kingLines.Length)
            {
                _index = _kingLines.Length - 1;
                _speechDone = true;
                RevealDocuments();
            }
        }

        /// <summary>세 문서를 시차를 두고 등장시키고 선택 가능하게 만든다.</summary>
        private void RevealDocuments()
        {
            if (_docsRevealed) return;
            _docsRevealed = true;

            if (_documents == null || _documents.Length == 0)
            {
                Debug.LogWarning("[IntroController] 연결된 문서가 없습니다.");
                return;
            }

            for (int i = 0; i < _documents.Length; i++)
                if (_documents[i] != null)
                    _documents[i].PlaySlideIn(i * _docStagger, _docSlideDuration, _docFromDistance);
        }

        private bool AdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool space = kb != null && kb.spaceKey.wasPressedThisFrame;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            return space || click;
#else
            return false;
#endif
        }

        private void OnGUI()
        {
            EnsureStyles();

            float w = Screen.width * 0.8f;
            float h = 150f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - h - 48f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none);

            if (!_speechDone)
            {
                GUI.Label(new Rect(x + 24, y + 12, w - 48, 30), _speakerName, _speakerStyle);
                string line = (_kingLines != null && _kingLines.Length > 0)
                    ? _kingLines[Mathf.Clamp(_index, 0, _kingLines.Length - 1)] : "";
                GUI.Label(new Rect(x + 24, y + 48, w - 48, h - 60), line, _lineStyle);
                GUI.Label(new Rect(x, y + h + 6, w, 24), "(스페이스 / 클릭: 다음)", _hintStyle);
            }
            else
            {
                GUI.Label(new Rect(x + 24, y + 24, w - 48, h - 40), _pickPrompt, _lineStyle);
                GUI.Label(new Rect(x, y + h + 6, w, 24), "(문서를 가리켜 클릭 — 나중엔 손을 뻗어 집는다)", _hintStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_lineStyle != null) return;
            _speakerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            _lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, wordWrap = true, richText = true,
                normal = { textColor = Color.white }
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.5f) }
            };
        }
    }
}
