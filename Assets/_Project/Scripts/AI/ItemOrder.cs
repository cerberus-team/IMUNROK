using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>물건으로 여는 자리</b> — 수첩에서 종이를 꺼내 들이미는 것이 아니라,
    /// 상 위의 것을 밀어 주거나 손에 든 연장을 대어 보는 일.
    ///
    /// <b>왜 종이로는 안 되는가.</b> 이 사건의 마지막 두 자리가 그렇다.
    ///   · <b>게</b> — 진짜 옹덕구는 여섯 살에 체한 뒤로 비린 것을 못 먹는다.
    ///     이것은 들이밀 종이가 아니라 <b>상 위에 밀어 놓고 보는</b> 것이다.
    ///   · <b>유척</b> — 대장에 적힌 "두 치 남짓"과 저 팔의 자국을 견주는 일.
    ///     손에 든 놋쇠 자가 하는 일이지 수첩이 하는 일이 아니다.
    /// 이런 것을 증거 제시 틀에 우겨 넣으면 「(증거) 게장」을 들이미는 꼴이 된다.
    ///
    /// <see cref="SleeveOrder"/> 는 소매 하나를 위해 따로 지은 집이다. 이쪽은
    /// <b>인스펙터에서 채우는</b> 일반형이라 새 물건마다 코드를 짓지 않아도 된다.
    ///
    /// <b>근거가 없으면 단추가 안 뜬다.</b> 게를 권하려면 그가 게를 못 먹는다는 것을
    /// 먼저 어디선가 들었어야 하고, 유척을 대려면 잴 치수가 대장에 있어야 한다.
    /// 까닭 없이 남의 상을 물리거나 팔을 잡는 것은 어사가 하는 일이 아니다.
    ///
    /// <b>대답은 AI 가 낸다.</b> 정해 둔 자막을 띄우고 끝나면 그 자리만 딴 사람의 것이 된다.
    /// 여기서 하는 일은 「이런 일이 벌어졌다」를 인물에게 건네는 데까지고,
    /// 어떻게 반응하는지는 그 인물의 성격이 정한다.
    ///
    /// 붙이는 곳: 인물(<see cref="InterrogationController"/> 가 달린 오브젝트). 여럿 붙여도 된다.
    /// </summary>
    public class ItemOrder : InterrogationOrder
    {
        [Header("단추")]
        [Tooltip("단추에 뜨는 말. 「게장을 권한다」 「유척을 대어 본다」처럼 시키는 말로")]
        [SerializeField] private string _label = "물건을 내민다";

        [Header("근거 — 이것이 없으면 단추가 아예 안 뜬다")]
        [SerializeField] private CaseId _case = CaseId.Case1_Onggojip;

        [Tooltip("수첩에 쥐고 있어야 하는 단서 key. 쉼표로 여럿이면 <b>모두</b> 있어야 한다.\n" +
                 "비우면 근거를 안 본다 — 그러면 까닭 없는 짓이 되니 되도록 채울 것")]
        [SerializeField] private string _needsClueKeys = "";

        [Tooltip("<b>손에 들고 있어야</b> 하는 도구 id(예: yucheok). 비우면 맨손이어도 된다.\n" +
                 "도구띠의 id 와 같아야 한다 — 벨트에 없는 id 를 적으면 영영 안 뜬다")]
        [SerializeField] private string _needsToolId = "";

        [Header("한 일")]
        [Tooltip("대화 기록과 입력줄에 남는 말. 물음이 아니라 <b>벌어진 일</b>이라 괄호로 적는다")]
        [SerializeField] private string _deedLine = "(상 위의 것을 앞으로 민다)";

        [Tooltip("이 일로 <b>드러나는 사실</b>. 인물이 마지못해 인정하게 될 내용을 적는다.\n" +
                 "비우면 아무것도 안 열리고 그냥 반응만 본다")]
        [TextArea(2, 4)] [SerializeField] private string _revealsInfo = "";

        [Header("수첩")]
        [Tooltip("이 일로 수첩에 <b>정황</b>으로 적힐 key. 비우면 붉게 한 번 지나가고 안 남는다")]
        [SerializeField] private string _grantsClueKey = "";
        [Tooltip("수첩에 적힐 문구. 비우면 위 '드러나는 사실'을 그대로 쓴다")]
        [TextArea(2, 4)] [SerializeField] private string _grantsClueText = "";

        [Tooltip("이 일이 벌어지면 <b>무너짐 단계</b>를 적어도 이만큼으로 올린다(0~10, 0이면 안 건드림).\n" +
                 "대장에 적힌 두 치와 제 팔의 자국이 눈앞에서 맞아떨어진 사람은 그 뒤로 " +
                 "같은 얼굴일 수 없다. 물건이 하는 말은 사람의 말보다 무겁다")]
        [Range(0, 10)]
        [SerializeField] private int _stageAtLeast = 0;

        [Header("무대")]
        [Tooltip("이 일에 딸린 물건 — 누르면 켜진다(상 위의 게장 따위). 없어도 된다")]
        [SerializeField] private GameObject _prop;
        [Tooltip("누른 순간 한 번. 연출을 걸 자리")]
        [SerializeField] private UnityEvent _onDone;

        [Tooltip("한 번 하면 단추가 사라진다. 끄면 몇 번이고 다시 할 수 있다 — " +
                 "재어 보는 일처럼 되풀이해도 되는 것에 쓴다")]
        [SerializeField] private bool _once = true;

        [Tooltip("<b>단추로는 안 뜬다.</b> 무대에서 벌어진 일로만 열린다(Trigger).\n" +
                 "유척으로 팔을 재고 난 뒤의 반응처럼, 어사가 「하겠다」고 고르는 것이 아니라 " +
                 "이미 벌어진 일에 딸려 오는 것에 쓴다")]
        [SerializeField] private bool _buttonless = false;

        private bool _done;

        public override string Label => _label;

        public override bool Available
        {
            get
            {
                if (_buttonless) return false;   // 이건 고르는 것이 아니라 벌어지는 것이다
                if (_done && _once) return false;
                if (!FitsAct) return false;
                if (!string.IsNullOrEmpty(_needsToolId)
                    && ToolbeltHud.SelectedToolId != _needsToolId) return false;

                if (string.IsNullOrWhiteSpace(_needsClueKeys)) return true;
                var j = Journal.Instance;
                if (j == null) return false;
                foreach (var raw in _needsClueKeys.Split(','))
                {
                    var k = raw.Trim();
                    if (k.Length == 0) continue;
                    if (!j.HasClue(_case, k)) return false;
                }
                return true;
            }
        }

        public override void Run(InterrogationController who)
        {
            if (who == null) return;
            _done = true;
            if (_prop != null) _prop.SetActive(true);
            who.Offer(_deedLine, _revealsInfo, _grantsClueKey, _grantsClueText, _stageAtLeast);
            _onDone?.Invoke();
        }

        /// <summary>
        /// 밖에서 부르는 자리 — 단추를 누르지 않고 <b>무대에서 벌어진 일</b>로 여는 길.
        ///
        /// 유척으로 팔을 재고 나면(<c>Measurable._onMatched</c>) 치수가 수첩에 적히는데,
        /// 정작 <b>당사자는 아무 말도 안 한다</b>. 그 자리에 이것을 걸어 두면 재고 난 뒤에
        /// 그 사람이 반응한다. UnityEvent 에 그대로 끼울 수 있게 인자가 없다.
        /// </summary>
        public void Trigger()
        {
            var who = GetComponent<InterrogationController>();
            if (who == null || !who.IsOpen) return;
            Run(who);
            InterrogationPanel.Refresh();
        }
    }
}
