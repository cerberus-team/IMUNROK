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
    ///   ② <b>수첩에 넣는다</b> — "그 봉서에 뭐라 했더라" 하고 되짚을 때 방으로
    ///      돌아갈 필요가 없다.
    ///
    /// <b>다만 들이밀 수는 없다.</b> 수첩에는 남되 심문의 「제시」 단추는 안 붙는다
    /// (<see cref="ClueEntry.presentable"/> = false). <see cref="CaseBriefing"/> 이
    /// 먼저 세워 둔 규칙인데 이쪽이 물려받지 못했다 —
    /// <b>처음부터 쥐고 있던 것은 증거가 아니라 출발점이다.</b>
    /// 어전에서 받아 온 봉서를 옹덕구 앞에 들이밀며 「이걸 어떻게 설명할 테냐」 하는 것은
    /// 말이 안 된다. 게다가 제시 목록의 <b>맨 첫 줄</b>을 늘 차지하고 있어서,
    /// 정작 파헤쳐 찾은 물증들이 그 아래로 밀렸다.
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

        [Header("현장에 들어서며 펴 보는 것")]
        [Tooltip("봉서와 다른 한 장. 봉서에는 사건의 까닭만 적혀 있다 — 현장에 들어서는 사람에게 필요한 것은 누가 무엇을 언제, 무엇을 밝혀야 하는가다. 비우면 예전처럼 봉서를 편다")]
        [SerializeField] private Texture2D _fieldPage;
        [Tooltip("그 한 장의 표제")]
        [SerializeField] private string _fieldTitle = "";
        [TextArea(4, 12)]
        [Tooltip("그 한 장에 적힌 것")]
        [SerializeField] private string _fieldBody = "";
        [Tooltip("그 한 장의 잔글씨(돋보기)")]
        [TextArea(2, 6)]
        [SerializeField] private string _fieldFine = "";
        [Tooltip("수첩에서의 식별자. 봉서와 달라야 두 장이 따로 남는다")]
        [SerializeField] private string _fieldKey = "조사문서";
        [Tooltip("수첩에 적힐 한 줄")]
        [SerializeField] private string _fieldLine = "조사 문서 — 무엇을 밝혀야 하는가";

        [TextArea(2, 5)]
        [Tooltip("현장에 닿았을 때 <b>펴는 대신 알리는</b> 한 마디. 별표로 감싼 낱말이 도드라진다.\n" +
                 "여기에 <b>그 종이에 담긴 것</b>을 함께 적을 것 — 「무엇이 들었다」만 알리면 " +
                 "펴 볼 까닭이 안 생긴다. 비우면 아무 말도 안 한다")]
        [SerializeField] private string _fieldNotice = "";

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

            // presentable: false — 수첩에는 남되 심문에서 들이밀 수는 없다.
            journal.AddClue(_caseId, _key, _clueLine, _page, ClueKind.물증, false);

            // <b>이미 적혀 있던 것도 내린다.</b> AddClue 는 같은 key 가 있으면 그냥
            // 돌아서므로, 이 고침 이전에 저장된 판으로 이어하면 옛 줄이 들이밀 수 있는
            // 채로 남는다. 저장을 지우게 할 일이 아니라 여기서 한 번 내려 주면 된다.
            foreach (var c in journal.GetClues(_caseId))
                if (c != null && c.key == _key) c.presentable = false;
            if (_page != null)
                journal.AttachDocument(_caseId, _key, _page, _title, body,
                                       string.IsNullOrEmpty(_fine) ? null : _fine,
                                       null, null, default,
                                       caseSheet: true);   // 조사청에서도 펴 볼 수 있는 한 장

            // <b>현장 문서는 수첩에 따로 남는다.</b> 봉서와 한 장으로 묶지 않는다 —
            // 하나는 왜 왔는지이고 하나는 무엇을 밝히는지라, 되짚어 볼 때 찾는 것이 다르다.
            if (_fieldPage != null && !string.IsNullOrEmpty(_fieldKey))
            {
                journal.AddClue(_caseId, _fieldKey, _fieldLine, _fieldPage, ClueKind.물증, false);
                foreach (var c in journal.GetClues(_caseId))
                    if (c != null && c.key == _fieldKey) c.presentable = false;
                journal.AttachDocument(_caseId, _fieldKey, _fieldPage,
                                       string.IsNullOrEmpty(_fieldTitle) ? _title : _fieldTitle,
                                       _fieldBody,
                                       string.IsNullOrEmpty(_fieldFine) ? null : _fieldFine);
            }

            if (knew || !_openOnArrive) yield break;

            yield return new WaitForSeconds(Mathf.Max(0f, _delay));

            // <b>사목은 펴지 않는다 — 알리기만 한다.</b>
            //
            // 여태 현장에 닿자마자 사목을 눈앞에 펴 주었다. 그런데 <b>받은 것은
            // 봉서지 사목이 아니다</b> — 어전에서 봉서를 받아 조사청을 거쳐 왔고,
            // 사목은 현감이 못 가려 상부에 올린 관아의 종이다. 그것이 손에 쥐여진 듯
            // 첫 화면에 펼쳐지면 받은 것과 주운 것이 한데 섞인다.
            //
            // 게다가 걸음을 떼기도 전에 종이 한 장이 시야를 덮는 것은, 읽으라는
            // 것이 아니라 <b>치우라는</b> 것이 된다 — 실제로 거의 다 곧바로 내려놓았다.
            //
            // 그래서 담긴 것을 <b>알림 한 마디에 같이 실어</b> 보내고, 펴 보는 것은
            // 소지품에 맡긴다. 알림은 소지품을 한 번 펴면 스스로 물러난다.
            if (_fieldPage != null)
            {
                if (!string.IsNullOrWhiteSpace(_fieldNotice))
                {
                    StatusPanel.Set(NoticeOwner, 2, _fieldNotice);
                    _noticeUp = true;
                }
            }
            else
            {
                // 사목이 아직 없는 사건이면 예전처럼 봉서를 편다.
                // <b>뒤를 어둡게 덮는다</b>(dim) — 앉아서 하나만 뜯어보는 자리다.
                DocumentView.Show(_page, _title, body, string.IsNullOrEmpty(_fine) ? null : _fine,
                                  null, true);
            }
        }

        /// <summary>상태 줄에서 쓰는 이름표. 다른 안내와 안 겹치게 한 군데에 적어 둔다.</summary>
        private const string NoticeOwner = "사목";
        private bool _noticeUp;

        /// <summary>
        /// <b>한 번 펴 보면 알림은 물러난다.</b>
        ///
        /// 안내는 <b>아직 안 한 일</b>을 가리킬 때만 안내다. 소지품을 펴 본 사람에게
        /// 「소지품을 펴 보라」가 계속 붙어 있으면 그때부터는 잔소리이고, 잔소리는
        /// 안 읽히면서 자리만 차지한다.
        /// </summary>
        private void Update()
        {
            if (!_noticeUp || !JournalView.AnyOpen) return;
            _noticeUp = false;
            StatusPanel.Clear(NoticeOwner);
        }

        private void OnDisable()
        {
            if (!_noticeUp) return;
            _noticeUp = false;
            StatusPanel.Clear(NoticeOwner);
        }
    }
}
