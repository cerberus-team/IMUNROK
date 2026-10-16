using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 마주 앉아 있는 동안 <b>손대면 제지당하는 것</b>.
    ///
    /// 주인과 마주 앉아 이야기하는 중에 뒤로 손을 뻗어 문갑을 열고 장부를 뒤진다면,
    /// 그것은 조사가 아니라 도둑질이다. 그렇다고 아무 반응 없이 막아 두면 플레이어는
    /// "왜 안 되지" 하고 같은 자리를 열 번 더 누른다 — 막힌 줄을 모르니까.
    ///
    /// 그래서 <b>사람이 말로 막는다</b>. 무엇을 짚었는지에 따라 다른 말이 나온다.
    /// 물건마다 제 나름의 사정이 있고, 그 사정이 곧 이 집을 설명한다 —
    /// 문갑은 주인어른 것이고, 장롱은 안사람 것이고, 보료는 지금 앉아 계신 자리다.
    ///
    /// 붙이는 곳: 앉아 있는 동안 못 만지게 할 물건(콜라이더가 있는 쪽).
    /// 병풍처럼 멀거나 아무래도 좋은 것에는 붙이지 않는다 — 짚어도 아무 일이 없는 편이 낫다.
    ///
    /// 언제 막히나: 내가 <b>앉아 있는 동안</b>, 또는 <b>심문 중</b>.
    /// 甲이 자리를 뜨고 내가 일어서면 저절로 풀린다.
    /// </summary>
    public class TouchRefusal : MonoBehaviour
    {
        [Tooltip("누가 막는가")]
        [SerializeField] private string _speaker = "복동";
        [TextArea(2, 3)]
        [Tooltip("손대려 할 때 나오는 말")]
        [SerializeField] private string _line = "나리, 그건 그냥 두시지요.";
        [Tooltip("같은 말을 이만큼(초) 안에 두 번 하지 않는다")]
        [SerializeField] private float _cooldown = 2.5f;
        [Tooltip("끄면 앉아 있지 않아도 늘 막는다(끝내 못 만지는 것)")]
        [SerializeField] private bool _onlyWhileSeated = true;

        [Tooltip("<b>보는 사람이 없을 때</b> 대신 뜨는 말. 비우면 그때는 막지 않는다(여태의 규칙). " +
                 "말리는 사람이 가고 나면 이 물건은 <b>아무 대꾸도 없는 것</b>이 된다. " +
                 "사랑방의 분합문이 그랬다 — 마당을 향한 넉 짝은 <b>일부러</b> 안 열리게 " +
                 "해 두었는데(DoorController 의 「플레이어가 여닫기」를 끈 것), 甲이 나간 " +
                 "뒤에는 말리는 말도 없어서 눌러도 <b>아무 일도 안 났다</b>. 막은 것과 " +
                 "고장난 것이 구별되지 않으면 사람은 같은 자리를 열 번 더 누른다. " +
                 "말리는 이가 없으니 여기는 <b>내 생각</b>으로 적는다 — 말한 이는 안 붙는다")]
        [TextArea(2, 3)]
        [SerializeField] private string _lineAfter = "";

        [Header("말리면서 흘리는 것")]
        [Tooltip("말리는 말이 곧 단서면 수첩에 적는다(정황, 말한 이는 위 _speaker). 甲 이 장부를 만지지 말라며 " +
                 "「스무 해를 내 손으로 적었소」라 흘리는데, 여태 대사로만 지나가 수첩에 안 남았다(2026-09-15)")]
        [SerializeField] private string _grantsClueKey = "";
        [SerializeField] private string _grantsClueTitle = "";
        [TextArea(2, 3)] [SerializeField] private string _grantsClueText = "";
        [SerializeField] private CaseId _grantsCase = CaseId.Case1_Onggojip;

        private float _quiet;

        /// <summary>
        /// <b>지금 누가 보고 있나</b> — 마주 앉았거나 앉기를 권받았거나 말하는 중. 이때는 방의 물건을
        /// 짚어도, 돋보기로 들여다봐도 수첩에 안 적힌다. 여태 말리는 부품이 붙은 물건만 막아서,
        /// 甲 앞에 앉은 채로 부서진 자물쇠·빈 종이·돋보기 응시로 단서가 적혔다.
        /// </summary>
        public static bool WatchingNow => Watching;

        /// <summary>지금 이 손길을 막아야 하는가. 막았으면 참 — 부른 쪽은 하던 일을 그만둔다.</summary>
        public bool Refuse()
        {
            bool watched = Watching;
            string say = _line;
            string who = _speaker;

            if (_onlyWhileSeated && !watched)
            {
                // 말리는 사람이 없다. 적어 둔 혼잣말이 있으면 그것으로 대신한다.
                if (string.IsNullOrWhiteSpace(_lineAfter)) return false;
                say = _lineAfter;
                who = "";       // 이름패 없이 — 내 생각이다
            }

            if (_quiet <= 0f)
            {
                SubtitleView.Show(who, say, "");
                _quiet = _cooldown;
            }
            if (watched && !string.IsNullOrEmpty(_grantsClueKey))
            {
                var j = Journal.Instance;
                if (j != null && !j.HasClue(_grantsCase, _grantsClueKey))
                {
                    j.AddClue(_grantsCase, _grantsClueKey, string.IsNullOrEmpty(_grantsClueText) ? _line : _grantsClueText,
                              null, ClueKind.정황, true, string.IsNullOrEmpty(_grantsClueTitle) ? null : _grantsClueTitle);
                    if (!string.IsNullOrEmpty(_speaker)) j.SetSpeaker(_grantsCase, _grantsClueKey, _speaker);
                }
            }
            return true;
        }

        private void Update() { if (_quiet > 0f) _quiet -= Time.deltaTime; }

        /// <summary>누가 나를 보고 있나 — 앉아 있거나 심문 중이면 그렇다.</summary>
        private static bool Watching
        {
            get
            {
                if (InterrogationController.AnyOpen) return true;
                var seat = PlayerSeat.Instance;
                return seat != null && (seat.Seated || seat.Offered);
            }
        }

        /// <summary>
        /// 이 물건(또는 그 어버이)이 손길을 막는가. 고르는 쪽에서 한 줄로 물어본다.
        /// </summary>
        public static bool Blocks(Component target)
        {
            if (target == null) return false;
            var r = target.GetComponentInParent<TouchRefusal>();
            return r != null && r.Refuse();
        }
    }
}
