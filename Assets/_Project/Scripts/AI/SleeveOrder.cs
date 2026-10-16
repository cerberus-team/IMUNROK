using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>「왼 소매를 걷으시오」</b> — 말로 안 되는 데까지 왔을 때 몸을 보는 명령.
    ///
    /// <b>왜 이 사건에 이것이 필요한가.</b> 甲과 乙은 닮았다(J04). 키를 재도 같고
    /// (1.70 대 1.70), 집안 사정을 물어도 甲이 더 잘 안다(J03). 필적은 위조를 밝히지만
    /// <b>누가 복동인지</b>는 밝히지 못한다 — 문서를 위조한 자가 甲이라는 것과, 甲이
    /// 곧 종 복동이라는 것은 다른 말이다. 말과 종이로는 여기서 더 못 간다.
    ///
    /// 남은 것은 <b>몸</b>이다. 관아 호적대장 원본에는 스무 해 전 관리가 적어 둔 한 줄이
    /// 있다 — "노 복동, <b>왼팔 안쪽 데인 자국 두 치 남짓</b>". 그 줄은 지금 이 자리에서
    /// 쓰려고 적힌 것이 아니라 그때 그냥 적힌 것이라, <b>아무도 손댈 수 없었다</b>.
    ///
    /// <b>차례</b>
    ///   ① 서고에서 대장 원본을 펼친다 → G01. 이 단추는 <b>그때부터</b> 뜬다.
    ///   ② 누른다 → 甲은 버틴다(<see cref="_refuseLines"/>). 버티는 것 자체가 대답이다.
    ///   ③ 다시 누른다 → 소매가 걷히고 팔이 드러난다(<see cref="_arm"/> 이 켜진다).
    ///   ④ 유척을 들고 그 팔을 겨눈다 → <see cref="Measurable"/> 이 재어 G07 을 준다.
    ///
    /// <b>乙에게도 붙인다.</b> 그가 곧바로 걷고 팔에 아무것도 없어야, 이 명령이 甲만
    /// 겨냥해 놓은 덫이 아니라 <b>양쪽에 똑같이 대는 자</b>가 된다. 어사가 하는 일이 그것이다.
    ///
    /// <b>근거 없이는 안 뜬다.</b> <see cref="_needsClueKey"/> 를 아직 못 얻었으면 단추가
    /// 아예 없다. 소매를 걷으라는 말은 까닭이 있어야 서는 말이고, 까닭은 대장에 있다.
    ///
    /// 붙이는 곳: 인물(<see cref="InterrogationController"/> 가 달린 오브젝트).
    /// </summary>
    public class SleeveOrder : InterrogationOrder
    {
        [Header("근거")]
        [SerializeField] private CaseId _case = CaseId.Case1_Onggojip;
        [Tooltip("이 단서를 얻어야 명령이 선다. 비우면 늘 뜬다(그러면 근거 없는 명령이 된다)")]
        [SerializeField] private string _needsClueKey = "G01";

        [Header("말")]
        [SerializeField] private string _label = "왼 소매를 걷으시오";
        [Tooltip("버티는 말. 적힌 수만큼 버틴다. 비우면 곧바로 걷는다 — 숨길 것이 없는 사람이 그렇다")]
        [TextArea(2, 3)] [SerializeField] private string[] _refuseLines;
        [Tooltip("걷으면서 <b>벌어지는 일</b>. 인물에게 이대로 건네지고, 대답은 그가 낸다.\n" +
                 "여태 이 칸은 정해진 자막이었다 — 그래서 소매를 걷혀 놓고도 인물은 " +
                 "그 사실을 모른 채 다음 물음에 팔을 내놓은 적 없는 사람처럼 답했다")]
        [TextArea(2, 3)] [SerializeField] private string _bareLine =
            "소매가 걷히고 왼팔 안쪽이 드러난다.";
        [Tooltip("걷은 뒤 화면 귀퉁이에 남는 말. 다음에 무엇을 할지 여기 적는다.\n" +
                 "자막은 인물의 대답이 차지하므로 이쪽은 <b>안내판</b>에 건다 — 한 번 스치고 " +
                 "사라지면 유척을 꺼내야 한다는 것을 알 길이 없다")]
        [SerializeField] private string _bareHint = "(유척을 들고 저 팔을 겨눈다)";

        [Tooltip("걷힌 것을 수첩에 <b>정황</b>으로 적을 key. 비우면 안 적는다")]
        [SerializeField] private string _grantsClueKey = "";

        [Tooltip("걷은 일이 <b>무너짐 단계</b>를 적어도 이만큼으로 올린다(0~10)")]
        [Range(0, 10)] [SerializeField] private int _stageAtLeast = 0;

        [Header("드러나는 것")]
        [Tooltip("걷으면 켜지는 것 — 팔(Measurable 이 붙은 오브젝트). 평소엔 꺼 둔다. " +
                 "꺼 두어야 소매 속을 미리 잴 수 없다")]
        [SerializeField] private GameObject _arm;
        [Tooltip("걷은 순간 한 번")]
        [SerializeField] private UnityEvent _onBared;

        private const string HintKey = "소매";

        private int _pressed;
        private bool _bared;

        /// <summary>이미 걷었나.</summary>
        public bool Bared => _bared;

        private void Awake()
        {
            // 소매 속은 못 잰다 — 겨눌 것이 아예 없어야 한다.
            if (_arm != null) _arm.SetActive(false);
        }

        /// <summary>재고 나면 안내판을 걷는다 — Measurable 의 잰 뒤 자리에 걸어 둘 것.</summary>
        public void ClearHint() => StatusPanel.Clear(HintKey);

        private void OnDisable() { StatusPanel.Clear(HintKey); }

        public override string Label => _label;

        public override bool Available
        {
            get
            {
                if (_bared) return false;      // 이미 걷었으면 시킬 것이 없다
                if (string.IsNullOrEmpty(_needsClueKey)) return true;
                var j = Journal.Instance;
                return j != null && j.HasClue(_case, _needsClueKey);
            }
        }

        public override string LockedReason
        {
            get
            {
                if (_bared || Available) return null;
                return "소매를 걷게 할 근거가 아직 없다 — 관의 호적 원본에 무엇이 적혀 있는지부터 봐야 한다(문서고).";
            }
        }

        /// <summary>「소매를 걷어라」「왼팔을 보이라」처럼 쳐도 이 명령으로 친다.</summary>
        public override bool Matches(string said)
        {
            if (string.IsNullOrEmpty(said)) return false;
            string t = said.Replace(" ", "");
            bool part = t.Contains("소매") || t.Contains("팔");
            bool act = t.Contains("걷") || t.Contains("보이") || t.Contains("보여") || t.Contains("내밀") || t.Contains("드러");
            return part && act;
        }

        public override void Run(InterrogationController who)
        {
            if (_bared) return;
            string speaker = who != null && who.Character != null ? who.Character.ShownName : name;

            // 아직 버틸 몫이 남았다. <b>버티는 것도 심문의 일부다</b> —
            // 한 번에 걷으면 무엇을 감췄는지가 아니라 무엇을 시켰는지만 남는다.
            int refusals = _refuseLines != null ? _refuseLines.Length : 0;
            if (_pressed < refusals)
            {
                SubtitleView.Show(speaker, _refuseLines[_pressed], "(한 번 더 이르면 걷는다)", true);
                _pressed++;
                return;
            }

            if (who == null) return;   // 건넬 데가 없으면 걷지도 않는다
            // 대답을 기다리는 중이면 걷지 않는다 — 소매만 걷히고 대화 기록에는 안 남았다(Offer 가 돌아 나감)
            if (who.Busy) { SubtitleView.Show("", "(대답을 다 듣고 이르시오)", "", false); return; }

            _bared = true;
            if (_arm != null) _arm.SetActive(true);

            // <b>벌어진 일로 건넨다.</b> 여태 자막만 띄우고 끝냈더니 대화 기록에도,
            // 밝혀진 사실에도, 무너짐 단계에도 아무것도 안 남아 — 소매를 걷힌 사람이
            // 바로 다음 물음에 팔을 내놓은 적 없는 사람처럼 답했다.
            who.Offer("(왼 소매를 걷게 한다)", _bareLine, _grantsClueKey, "", _stageAtLeast);

            // 다음에 할 일은 자막이 아니라 안내판에 남긴다 — 자막은 곧 대답이 덮는다.
            if (!string.IsNullOrWhiteSpace(_bareHint)) StatusPanel.Set(HintKey, 1, _bareHint);
            _onBared?.Invoke();
        }
    }
}
