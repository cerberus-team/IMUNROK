// <b>Measurable 은 제 파일에 산다.</b> 여태 BrassRuler.cs 안의 곁가지 클래스였는데,
// 유니티는 파일 이름과 다른 클래스에 MonoScript 자산을 못 붙인다. 그래서 씬에 붙은
// 두 팔의 Measurable 이 guid 없는 m_Script 로 저장되어, 다시 들여오면 Missing 이 될
// 자리였다 — G07·G08 이 통째로 죽는다. 클래스는 한 글자도 안 바꿨다.
using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
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
        [Tooltip("재고 나서 <b>맞든 안 맞든</b> 한 번 — 말끔한 팔(乙)도 재면 반응이 있어야 한다(2026-09-15)")]
        [SerializeField] private UnityEvent _onMeasured;

        /// <summary>잰 뒤 수첩에 적을 열쇠.</summary>
        public string ClueKey => _clueKey;

        /// <summary>잴 것이 <b>없는</b> 자리인가(乙 의 팔). 없다는 말을 적어 두었으면 그렇다.</summary>
        public bool Bare => !string.IsNullOrEmpty(_nothingLine);

        /// <summary>어느 사건의 것인가.</summary>
        public CaseId Case => _case;

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
            _onMeasured?.Invoke();
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
