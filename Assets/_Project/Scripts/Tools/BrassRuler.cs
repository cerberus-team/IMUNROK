using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>유척(鍮尺)</b> — 놋쇠 자. 대면 <b>치수가 나온다</b>.
    ///
    /// <b>왜 이것이 어사의 물건인가</b>: 사람의 말은 흔들린다. 甲은 형제가 없다 하고,
    /// 마름은 취해서 말을 바꾸고, 하인은 호칭을 얼버무린다. 그러나 <b>치수는 안 흔들린다</b>.
    /// 관아 호적대장에 적힌 "왼팔 안쪽 데인 자국 <b>두 치 남짓</b>"은 스무 해 전에 적힌
    /// 숫자이고, 지금 저 팔에 있는 흉터도 숫자다. 둘을 견주는 데는 말이 필요 없다.
    ///
    /// 조선의 어사가 유척을 지닌 본뜻도 같다 — 고을 수령이 말과 되를 속이지 않는지
    /// <b>재어서</b> 밝히라는 것이다. 따져 묻는 것이 아니라 대어 보는 것이다.
    ///
    /// <b>쓰는 법</b>: 손에 들고 <see cref="Measurable"/> 이 달린 것을 겨눈다.
    /// 재는 데 <see cref="_measureSeconds"/> 초가 든다 — 자를 대고 눈금을 읽는 시간이다.
    /// 다 재면 치수가 뜨고, 견줄 것이 있으면 <b>맞는지 어긋나는지</b>까지 말해 준다.
    ///
    /// 붙이는 곳: 아무 데나 하나.
    /// </summary>
    public class BrassRuler : MonoBehaviour
    {
        [Header("무엇을 들었을 때")]
        [SerializeField] private string _toolId = "yucheok";

        [Header("재기")]
        [Tooltip("이 거리(m) 안의 것만 잰다. 자를 대야 하므로 팔이 닿아야 한다")]
        [SerializeField] private float _reach = 2.2f;
        [Tooltip("눈금을 읽는 데 드는 시간(초). 대자마자 나오면 잰 것이 아니라 본 것이다")]
        [SerializeField] private float _measureSeconds = 1.8f;

        [Header("말")]
        [SerializeField] private string _speaker = "";
        [TextArea(2, 3)]
        [Tooltip("들었는데 잴 것을 안 겨눴을 때")]
        [SerializeField] private string _idleLine = "놋쇠 자다. 잴 것에 대어 본다.";

        private float _dwell;
        private Measurable _at;

        private void Update()
        {
            if (ToolbeltHud.SelectedToolId != _toolId) { _at = null; _dwell = 0f; return; }

            var cam = Camera.main;
            if (cam == null) return;

            Measurable hit = null;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var info, _reach))
                hit = info.collider.GetComponentInParent<Measurable>();

            if (hit != _at) { _at = hit; _dwell = 0f; }
            if (_at == null) { Say(_idleLine, false); return; }
            if (_at.Measured) { Say(_at.ResultLine(), true); return; }

            _dwell += Time.deltaTime;
            if (_dwell < _measureSeconds)
            {
                Say($"{_at.What} 을(를) 재는 중… {Mathf.RoundToInt(_dwell / _measureSeconds * 100f)}%", false);
                return;
            }
            _at.Measure();
            Say(_at.ResultLine(), true);
        }

        private string _last;
        private void Say(string line, bool key)
        {
            if (line == _last) return;
            _last = line;
            SubtitleView.Show(_speaker, line, "", key);
        }

        private void OnDisable() { _last = null; }
    }

    /// <summary>
    /// <b>잴 수 있는 것.</b> 유척을 대면 치수가 나온다.
    ///
    /// 치수는 <b>치(寸)</b> 로 적는다. 한 치는 약 3.03cm 다. 미터로 적으면 조선의 물건이
    /// 아니라 실험 기구가 된다 — "두 치 남짓"이라야 호적대장의 그 문장과 겹친다.
    ///
    /// 견줄 값(<see cref="_expected"/>)을 적어 두면 잰 값과 맞는지까지 말한다. 그것이
    /// 이 장치의 요지다 — 재는 것이 목적이 아니라 <b>대조</b>가 목적이다.
    /// </summary>
    public class Measurable : MonoBehaviour, IInspectable
    {
        [Tooltip("무엇을 재는가 — '왼팔 안쪽 흉터', '되', '말'")]
        [SerializeField] private string _what = "흉터";
        [Tooltip("실제 치수(치). 한 치 ≈ 3.03cm")]
        [SerializeField] private float _chi = 2.1f;

        [Header("견줄 것")]
        [Tooltip("문서에 적혀 있던 값(치). 0이면 견주지 않고 치수만 말한다")]
        [SerializeField] private float _expected = 0f;
        [Tooltip("이 안쪽이면 '맞다'고 본다(치). 두 치 남짓의 '남짓'이 이만큼이다")]
        [SerializeField] private float _tolerance = 0.35f;

        [Tooltip("<b>잴 것이 아예 없을 때</b>의 말. 채워 두면 치수 대신 이 말이 나온다.\n" +
                 "흉터가 없는 팔이 그렇다 — '영 치'는 치수가 아니라 <b>없다</b>는 뜻인데, " +
                 "숫자로 적으면 잰 것처럼 보인다. 없는 것은 없다고 말해야 그것도 하나의 답이 된다")]
        [TextArea(2, 3)] [SerializeField] private string _nothingLine = "";
        [Tooltip("견줄 문서를 아직 못 봤으면 대조하지 않는다. 비우면 늘 대조한다")]
        [SerializeField] private string _needsClueKey = "";
        [SerializeField] private CaseId _case = CaseId.Case1_Onggojip;

        [Header("잰 뒤 수첩에")]
        [SerializeField] private string _clueKey = "";
        [TextArea(2, 3)] [SerializeField] private string _clueText = "";
        [Tooltip("치수가 맞아떨어진 순간 한 번")]
        [SerializeField] private UnityEvent _onMatched;

        /// <summary>이미 쟀나.</summary>
        public bool Measured { get; private set; }

        /// <summary>무엇을 재는 것인가.</summary>
        public string What => _what;

        /// <summary>잰 값이 문서와 맞는가. 아직 안 쟀으면 거짓.</summary>
        public bool Matched { get; private set; }

        private bool HasCompare =>
            _expected > 0f &&
            (string.IsNullOrEmpty(_needsClueKey) ||
             (Journal.Instance != null && Journal.Instance.HasClue(_case, _needsClueKey)));

        public void Measure()
        {
            if (Measured) return;
            Measured = true;
            Matched = HasCompare && Mathf.Abs(_chi - _expected) <= _tolerance;

            if (!string.IsNullOrEmpty(_clueKey) && Journal.Instance != null)
                Journal.Instance.AddClue(_case, _clueKey,
                    string.IsNullOrEmpty(_clueText) ? ResultLine() : _clueText);

            if (Matched) _onMatched?.Invoke();
        }

        /// <summary>잰 결과를 사람 말로.</summary>
        public string ResultLine()
        {
            string size = Chi(_chi);
            if (!Measured) return $"{_what} — 아직 재지 않았다.";
            // 없는 것은 없다고 말한다. 이것도 대답이다 — 乙의 팔이 그렇다.
            if (!string.IsNullOrEmpty(_nothingLine)) return _nothingLine;
            if (!HasCompare) return $"{_what}, {size}.";
            return Matched
                ? $"{_what}, {size}. 적힌 것과 <b>같다</b>."
                : $"{_what}, {size}. 적힌 것은 {Chi(_expected)}였다 — <b>어긋난다</b>.";
        }

        /// <summary>치수를 조선 말로 적는다. 2.1 → "두 치 남짓".</summary>
        private static string Chi(float chi)
        {
            string[] n = { "영", "한", "두", "석", "넉", "닷", "엿", "일곱", "여덟", "아홉" };
            int whole = Mathf.FloorToInt(chi);
            float frac = chi - whole;
            string head = whole < n.Length ? n[whole] : whole.ToString();
            if (frac < 0.12f) return head + " 치";
            if (frac < 0.4f) return head + " 치 남짓";
            if (frac < 0.7f) return head + " 치 반";
            return (whole + 1 < n.Length ? n[whole + 1] : (whole + 1).ToString()) + " 치에 조금 못 미친다";
        }

        // ── 가리키면 ──
        public string GetInspectTitle() => _what;
        public string GetInspectBody() =>
            Measured ? ResultLine() : $"{_what}. 눈으로는 가늠이 안 된다.\n(유척을 대어 본다)";
        public void OnInspected() { }
    }
}
