using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>조사청에 들어선 사람에게 어디로 갈지 일러 준다.</b>
    ///
    /// 어전에서 봉서를 맡으면 화면이 한 번 캄캄해졌다 조사청에서 뜬다. 그런데
    /// 여태 그 자리에는 <b>아무 말도 없었다</b> — 여기가 어디인지, 무엇을 할 차례인지,
    /// 사건 문서가 어느 쪽인지. 처음 온 사람은 봉서를 맡자마자 낯선 마당 한복판에
    /// 서 있게 된다. 게다가 문서 셋은 실내에 있어서, 들어선 방향에 따라 <b>등 뒤에</b>
    /// 놓이는 일이 흔하다. 그때는 "문서를 고르시오" 라고 적어 봐야 안 적은 것과 같다.
    ///
    /// 그래서 두 가지를 한다:
    ///   ① <b>여기가 어디인지</b> 한 줄(자막). 몇 초 뒤 스스로 걷힌다.
    ///   ② <b>어느 쪽인지</b> 가리킨다(<see cref="WayMark"/>). 등 뒤에 있으면
    ///      화면 가장자리에서 그쪽을 가리키고, 고개를 돌려 눈에 들어오면
    ///      그 위에 이름표로 붙는다.
    ///
    /// <b>사건을 고르면 걷는다.</b> 아는 사람 앞을 계속 가릴 이유가 없다 —
    /// 봉서 안내에서 이미 한 번 배운 것이다(<c>_everPicked</c>).
    /// 이어하기로 들어온 사람에게도 안 띄운다. 그는 여기가 어딘지 안다.
    ///
    /// 붙이는 곳: 조사청 씬의 아무 오브젝트에나 하나.
    /// [이문록 ▸ 조사청 ▸ 들어선 자리에 길잡이 두기] 가 대신 놓아 준다.
    /// </summary>
    public class HubArrival : MonoBehaviour
    {
        [Tooltip("들어서면 한 줄. 비우면 자막은 안 띄우고 방향만 가리킨다")]
        [TextArea]
        [SerializeField] private string _line = "조사청이오. 맡으신 사건의 문서는 안에 펼쳐 두었소.";

        [SerializeField] private string _speaker = "";

        [Tooltip("그 줄이 머무는 시간(초)")]
        [SerializeField] private float _lineSeconds = 4.5f;

        [Tooltip("화면이 밝아질 틈. 암전 중에 띄우면 아무도 못 본다")]
        [SerializeField] private float _delay = 0.9f;

        [Tooltip("가리킬 것에 붙일 이름")]
        [SerializeField] private string _markLabel = "사건 문서";

        private CaseCube _aim;

        private IEnumerator Start()
        {
            // 이미 사건 안이면 여기가 아니다. 조사청에 있을 때만 말한다.
            if (GameState.Instance != null && GameState.Instance.InCase) yield break;

            yield return new WaitForSeconds(Mathf.Max(0f, _delay));

            _aim = PickAim();
            if (_aim == null) yield break;      // 가리킬 것이 없으면 아무 말도 안 한다

            if (!string.IsNullOrEmpty(_line))
            {
                SubtitleView.Show(_speaker, _line, "");
                StartCoroutine(HideLineSoon());
            }
            WayMark.Show(_aim.transform, _markLabel);
        }

        private IEnumerator HideLineSoon()
        {
            yield return new WaitForSeconds(Mathf.Max(0.5f, _lineSeconds));
            SubtitleView.Hide();

            // <b>길표도 말과 함께 걷는다.</b>
            //
            // 여태 길표는 사건을 고를 때까지 <b>계속 떠 있었다</b>. 등 뒤에 놓인 문서를
            // 가리켜 주자는 뜻이었는데, 한 번 그쪽을 보고 나면 그 뒤로는 알려 줄 것이
            // 없는데도 화살표가 화면에 남는다 — 안내가 아니라 <b>거슬리는 것</b>이 된다.
            //
            // 알려 줄 것은 「저쪽이오」 한 번이다. 말이 끝나면 그 말도 길표도 함께 걷는다.
            // 잊었으면 다시 들어오면 되고, 방은 한눈에 들어올 만큼 좁다.
            WayMark.Hide();
        }

        /// <summary>
        /// 어느 문서를 가리킬 것인가.
        ///
        /// <b>하던 것이 먼저다.</b> 진행중인 사건이 있으면 그것을 가리킨다 — 판결을
        /// 내리고 돌아온 사람에게 엉뚱한 사건을 가리키면 안내가 아니라 훼방이다.
        /// 없으면 <b>열 수 있는 것</b> 중 첫째. 씬이 없어 못 여는 문서를 가리켜 놓고
        /// "아직 봉서가 닿지 않았소" 를 듣게 하는 것도 훼방이다.
        /// </summary>
        private static CaseCube PickAim()
        {
            var cubes = FindObjectsByType<CaseCube>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (cubes == null || cubes.Length == 0) return null;

            var gs = GameState.Instance;
            if (gs != null)
                foreach (var c in cubes)
                    if (c.Openable && gs.GetStatus(c.CaseId) == CaseStatus.InProgress) return c;

            foreach (var c in cubes)
                if (c.Openable && (gs == null || gs.GetStatus(c.CaseId) != CaseStatus.Completed)) return c;

            return null;
        }

        /// <summary>
        /// 고를 창이 열렸으면 길표를 걷는다. 무엇을 가리키는지 이미 알고 그 앞에
        /// 선 사람에게, 그 창 위로 또 화살표가 겹칠 이유가 없다.
        /// </summary>
        private void Update()
        {
            if (!WayMark.Up) return;
            if (_aim == null) { WayMark.Hide(); return; }
            if (GameState.Instance != null && GameState.Instance.InCase) { WayMark.Hide(); return; }
            if (CaseChoicePanel.Showing) WayMark.Hide();
        }
    }
}
