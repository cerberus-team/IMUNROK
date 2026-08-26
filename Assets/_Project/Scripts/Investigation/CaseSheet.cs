using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>사건표</b> — 사건에 들어서면 맨 처음 펴 보는 종이이자, 수첩의 첫 줄.
    ///
    /// 여태 사건 씬은 <b>아무것도 쥐여 주지 않고</b> 시작했다. 어전에서 봉서를
    /// 받아 조사청을 거쳐 현장에 닿았는데, 정작 그 봉서에 뭐라 적혀 있었는지는
    /// 다시 볼 데가 없었다. 조사가 무엇을 밝히는 일인지 모르는 채로 마당에
    /// 서 있게 되는 것이다.
    ///
    /// 그래서 두 가지를 함께 한다:
    ///   ① <b>펴 보인다</b> — 들어서자마자 눈앞에 한 장. 내려놓으면 조사가 시작된다.
    ///   ② <b>수첩에 넣는다</b> — 이것이 <b>첫 물증</b>이다. 심문에서 내밀 수 있고,
    ///      "그 봉서에 뭐라 했더라" 하고 되짚을 때 방으로 돌아갈 필요가 없다.
    ///
    /// <b>두 번째 판에는 안 편다.</b> 이미 수첩에 있으면 아는 사람이다 —
    /// 이어하기로 들어온 사람 앞을 종이 한 장이 가로막을 이유가 없다.
    /// 그래도 수첩에는 그대로 있으니 언제든 다시 펴 볼 수 있다.
    ///
    /// 붙이는 곳: 사건 씬의 아무 오브젝트에나 하나.
    /// </summary>
    public class CaseSheet : MonoBehaviour
    {
        [Tooltip("어느 사건의 표인가")]
        [SerializeField] private CaseId _caseId = CaseId.Case1_Onggojip;

        [Tooltip("수첩에서의 식별자. 같은 key 는 한 번만 적힌다")]
        [SerializeField] private string _key = "사건표";

        [Tooltip("수첩에 적힐 한 줄")]
        [SerializeField] private string _clueLine = "사건표 — 어전에서 받은 봉서";

        [Tooltip("펼쳤을 때의 표제")]
        [SerializeField] private string _title = "제1사건 — 옹고집전";

        [TextArea(4, 12)]
        [Tooltip("펼쳤을 때 읽히는 것. 비우면 조사청의 요지를 가져다 쓴다")]
        [SerializeField] private string _body = "";

        [Tooltip("펼칠 종이 그림. [이문록 ▸ 사건 문서 굽기] 로 구운 것")]
        [SerializeField] private Texture2D _page;

        [Tooltip("돋보기로만 읽히는 잔글씨. 비우면 없다")]
        [TextArea(2, 6)]
        [SerializeField] private string _fine = "";

        [Tooltip("화면이 밝아질 틈(초). 암전 중에 펴면 아무도 못 본다")]
        [SerializeField] private float _delay = 1.1f;

        [Tooltip("끄면 수첩에만 넣고 펴지는 않는다")]
        [SerializeField] private bool _openOnArrive = true;

        private IEnumerator Start()
        {
            var journal = Journal.Instance;
            if (journal == null) yield break;

            // 이미 아는 사람인가 — 수첩에 있으면 이어하기로 들어온 것이다.
            bool knew = journal.HasClue(_caseId, _key);

            string body = string.IsNullOrEmpty(_body) ? journal.GetBrief(_caseId) : _body;

            journal.AddClue(_caseId, _key, _clueLine, _page);
            if (_page != null)
                journal.AttachDocument(_caseId, _key, _page, _title, body,
                                       string.IsNullOrEmpty(_fine) ? null : _fine);

            if (knew || !_openOnArrive) yield break;

            yield return new WaitForSeconds(Mathf.Max(0f, _delay));
            DocumentView.Show(_page, _title, body, string.IsNullOrEmpty(_fine) ? null : _fine);
        }
    }
}
