using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>빛이 비스듬히 들어야 보인다</b> — 등불을 <b>어디에 거느냐</b>가 조사가 되는 자리.
    ///
    /// <b>왜 각도인가.</b> 종이에 눌린 자국은 <b>정면에서 보면 안 보인다</b>. 위에서 곧게
    /// 비추면 골과 마루가 똑같이 밝아 아무 무늬도 안 생긴다. 옆에서 낮게 스쳐 들어야
    /// 눌린 골에 그늘이 지고, 그제야 <b>글자가 떠오른다</b>. 문서 감식에서 실제로 쓰는
    /// 방법이고(사광 조명), 이 게임은 이미 그 어휘를 쓴다 — 사랑방의 <see cref="PressedMarks"/> 다.
    ///
    /// <b>그래서 이것은 수수께끼가 아니라 손짓이다.</b> 답을 맞히는 것이 아니라
    /// 등불을 들고 <b>이리저리 놓아 보다가</b> 어느 자리에서 자국이 뜬다. 등경을 여럿 두면
    /// 「어디에 걸까」가 곧 조사가 된다 — 등불을 내려놓으면 두 손이 풀린다는
    /// <see cref="LanternStand"/> 의 규칙이 여기서 값을 한다.
    ///
    /// <b>세 마디로 이른다</b> — 아무 말도 없으면 사람은 이 자리에 규칙이 있는 줄 모른다.
    ///   · 불이 없을 때        <see cref="_bodyDark"/>   「어둡다」
    ///   · 불은 있으나 각이 틀릴 때 <see cref="_bodyWrong"/>  「얼비칠 듯 말 듯하다」 ← 이 한 줄이 핵심이다
    ///   · 맞았을 때           <see cref="_bodyRight"/>  자국이 뜬다
    ///
    /// 붙이는 곳: 자국이 있을 면(궤 뚜껑·서안 상판). 콜라이더가 있어야 살펴진다.
    /// </summary>
    public class LightAngleReveal : MonoBehaviour, IInspectable
    {
        [Header("빛")]
        [Tooltip("이 거리(m) 안의 불만 센다")]
        [SerializeField] private float _radius = 3.5f;

        [Tooltip("빛이 <b>이 높이각</b>(도)으로 들어야 자국이 뜬다. 낮을수록 비스듬하다 — " +
                 "0 이면 수평, 90 이면 바로 위에서. 눌린 자국은 15~25도가 알맞다")]
        [Range(0f, 90f)] [SerializeField] private float _wantElevation = 18f;

        [Tooltip("이만큼(도) 어긋나도 봐준다. 너무 좁으면 맞춰 놓고도 안 뜬다")]
        [Range(2f, 40f)] [SerializeField] private float _tolerance = 12f;

        [Tooltip("이 밝기 아래의 불은 없는 셈 친다")]
        [SerializeField] private float _minIntensity = 0.3f;

        [Tooltip("이보다 <b>가까운</b> 불은 안 센다(m). 손에 든 등불을 코앞에 들이대는 것으로 " +
                 "풀리면 등경이 있을 까닭이 없다 — 사광은 <b>건너편에서</b> 오는 빛이다")]
        [SerializeField] private float _minDistance = 1.6f;

        [Header("드러나는 것")]
        [Tooltip("각이 맞으면 켜진다. 비워 두면 글만 바뀐다")]
        [SerializeField] private GameObject _reveal;

        [Tooltip("한 번 뜨면 그대로 둔다 — 알아본 것을 도로 못 알아볼 수는 없다")]
        [SerializeField] private bool _keepOnceSeen = true;

        [Header("글")]
        [SerializeField] private string _title = "";
        [TextArea(2, 4)] [SerializeField] private string _bodyDark = "어둡다. 아무것도 안 보인다.";
        [TextArea(2, 4)] [SerializeField] private string _bodyWrong = "빛이 곧게 들어 면이 고르게 밝다 — 무언가 얼비칠 듯 말 듯하다.";
        [TextArea(2, 4)] [SerializeField] private string _bodyRight = "빗겨 든 빛에 눌린 자국이 떠오른다.";

        [Header("수첩")]
        [SerializeField] private string _clueKey = "";
        [TextArea(2, 4)] [SerializeField] private string _clueText = "";
        [SerializeField] private CaseId _case = CaseId.Case1_Onggojip;

        private bool _seen;
        private float _next;
        private int _state;         // 0 어둠 · 1 각 틀림 · 2 맞음

        private void Start() { if (_reveal != null) _reveal.SetActive(false); }

        private void Update()
        {
            if (_seen && _keepOnceSeen) return;
            if (Time.time < _next) return;
            _next = Time.time + 0.2f;          // 매 칸 불을 다 훑을 까닭이 없다

            _state = Look();
            bool on = _state == 2;
            if (_reveal != null && _reveal.activeSelf != on) _reveal.SetActive(on);
            if (!on) return;

            _seen = true;
            if (!string.IsNullOrEmpty(_clueKey) && Journal.Instance != null)
                Journal.Instance.AddClue(_case, _clueKey,
                    string.IsNullOrEmpty(_clueText) ? _bodyRight : _clueText);
        }

        /// <summary>0 = 불이 없다 · 1 = 있으나 각이 틀리다 · 2 = 맞다.</summary>
        private int Look()
        {
            int best = 0;
            Vector3 here = transform.position;

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || !l.isActiveAndEnabled || l.intensity < _minIntensity) continue;
                if (l.type == LightType.Directional) continue;      // 방 안의 불만 센다

                Vector3 to = l.transform.position - here;
                float d2 = to.sqrMagnitude;
                if (d2 > _radius * _radius || d2 < _minDistance * _minDistance) continue;

                best = Mathf.Max(best, 1);

                // 빛이 <b>얼마나 낮게</b> 드는가 — 수평에서 잰 높이각.
                float flat = new Vector2(to.x, to.z).magnitude;
                float elev = Mathf.Atan2(Mathf.Abs(to.y), Mathf.Max(0.001f, flat)) * Mathf.Rad2Deg;
                if (Mathf.Abs(elev - _wantElevation) <= _tolerance) return 2;
            }
            return best;
        }

        // ── 살펴보기 ──

        public string GetInspectTitle() { return _title; }

        public string GetInspectBody()
        {
            if (_seen && _keepOnceSeen) return _bodyRight;
            switch (_state)
            {
                case 2:  return _bodyRight;
                case 1:  return _bodyWrong;
                default: return _bodyDark;
            }
        }

        /// <summary>여기서는 적을 것이 없다 — 적는 때는 <b>각이 맞은 순간</b>이지 본 순간이 아니다.</summary>
        public void OnInspected() { }
    }
}
