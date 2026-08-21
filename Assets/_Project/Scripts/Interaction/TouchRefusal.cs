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

        private float _quiet;

        /// <summary>지금 이 손길을 막아야 하는가. 막았으면 참 — 부른 쪽은 하던 일을 그만둔다.</summary>
        public bool Refuse()
        {
            if (_onlyWhileSeated && !Watching) return false;

            if (_quiet <= 0f)
            {
                SubtitleView.Show(_speaker, _line, "");
                _quiet = _cooldown;
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
