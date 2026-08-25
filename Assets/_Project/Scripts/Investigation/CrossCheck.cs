using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>맞대보기(對照)</b> — 두 문서를 나란히 놓아야 비로소 보이는 것.
    ///
    /// <b>왜 이것이 2막의 알맹이인가.</b> 1막에서 집을 뒤져 얻은 것은 죄 <b>한쪽</b>이다.
    /// 집에 있던 호구단자에 "노 복동 신미년 사망"이라 적혀 있다는 것만으로는 아무것도
    /// 아니다 — 사람은 죽으니까. 관아가 보관한 원본에 <b>같은 손</b>이 적혀 있다는 것을
    /// 보고 나서야 그 한 줄이 위조가 된다.
    ///
    /// 그러니 서고에서 하는 일은 새 단서를 줍는 것이 아니라 <b>이미 가진 것을 겹쳐 보는
    /// 일</b>이다. 문서고에 들어가 종이를 헤집는 것이 그래서 필요하다. 뒤져서 나오는 것은
    /// 대장 넉 권뿐이지만, 그 넉 권은 저마다 집에서 가져온 무엇과 <b>짝</b>이 있다.
    ///
    /// <b>어떻게 도나</b>: 짝이 둘 다 수첩에 들어온 순간 한 번, 자막이 붉게 뜬다.
    /// 문서를 펼쳐 놓은 동안에는 안 뜬다 — 종이를 들여다보는 눈앞에 글이 겹치면
    /// 어느 쪽을 읽으라는 것인지 알 수 없다. 덮고 나서 뜬다.
    ///
    /// <b>새 단서를 줄 수도 있고 안 줄 수도 있다.</b> 대개는 안 준다. 겹쳐 보아 알게 된
    /// 것은 손에 쥔 물건이 아니라 <b>알게 된 사실</b>이라, 수첩에 또 한 줄을 늘리면
    /// 들이밀 것이 늘어난 것처럼 보인다. 다만 그 자리에서 <b>다음 문이 열리는</b>
    /// 겹침이라면 적어 둔다(그때는 <see cref="Pair.새단서key"/> 를 채운다).
    ///
    /// 붙이는 곳: 씬에 빈 오브젝트 하나(관아의 _대조). 표가 한 군데 모여 있어야
    /// 무엇과 무엇이 짝인지 한눈에 보인다.
    /// </summary>
    public class CrossCheck : MonoBehaviour
    {
        [System.Serializable]
        public class Pair
        {
            [Tooltip("편집기에서 알아보려고 붙이는 이름. 게임에는 안 나온다")]
            public string 이름 = "";

            [Tooltip("관아에서 얻는 쪽(G…)")]
            public string 이쪽 = "";

            [Tooltip("집에서 가져온 쪽(J…)")]
            public string 저쪽 = "";

            [TextArea(2, 4)]
            [Tooltip("둘이 겹친 순간 뜨는 말. *별표*로 감싼 낱말은 도드라진다")]
            public string 맞대면 = "";

            [Tooltip("겹쳐서 <b>새로 얻는</b> 단서 key. 비우면 말만 하고 수첩엔 안 적는다")]
            public string 새단서key = "";

            [TextArea(2, 3)] public string 새단서 = "";

            [Tooltip("겹친 순간 한 번")]
            public UnityEvent 그때;

            [System.NonSerialized] public bool 끝남;
        }

        [SerializeField] private CaseId _case = CaseId.Case1_Onggojip;
        [SerializeField] private Pair[] _pairs;

        [Tooltip("겹친 것을 알아채기까지 두는 뜸(초). 문서를 덮자마자 튀어나오면 " +
                 "내가 알아낸 것이 아니라 화면이 알려 준 것이 된다")]
        [SerializeField] private float _beat = 0.9f;

        [SerializeField] private string _speaker = "";

        [Tooltip("자막 아랫줄. 무엇이 겹친 것인지 한 마디로")]
        [SerializeField] private string _hint = "맞대어 보니 —";

        private float _wait;

        private void Update()
        {
            if (_pairs == null || _pairs.Length == 0) return;

            var j = Journal.Instance;
            if (j == null) return;

            // 종이를 들여다보는 중이거나 사람과 마주 선 중에는 끼어들지 않는다.
            if (DocumentView.IsOpen || InterrogationController.AnyOpen || JournalView.AnyOpen)
            { _wait = 0f; return; }

            for (int i = 0; i < _pairs.Length; i++)
            {
                var p = _pairs[i];
                if (p == null || p.끝남) continue;
                if (string.IsNullOrEmpty(p.이쪽) || string.IsNullOrEmpty(p.저쪽)) continue;
                if (!j.HasClue(_case, p.이쪽) || !j.HasClue(_case, p.저쪽)) continue;

                // 한 박자 두고 뜬다
                _wait += Time.deltaTime;
                if (_wait < _beat) return;
                _wait = 0f;

                p.끝남 = true;
                if (!string.IsNullOrEmpty(p.맞대면))
                    SubtitleView.Show(_speaker, p.맞대면, _hint, true);
                if (!string.IsNullOrEmpty(p.새단서key))
                    j.AddClue(_case, p.새단서key, p.새단서);
                p.그때?.Invoke();
                return;      // 한 프레임에 하나씩. 둘이 한꺼번에 뜨면 둘 다 안 읽힌다
            }

            _wait = 0f;
        }

#if UNITY_EDITOR
        /// <summary>지금 무엇이 겹칠 수 있고 무엇이 모자란지 찍어 본다.</summary>
        [ContextMenu("진단: 맞대볼 것 찍기")]
        private void Diagnose()
        {
            var j = Journal.Instance;
            var sb = new System.Text.StringBuilder("[맞대보기] " + SceneManager.GetActiveScene().name + "\n");
            foreach (var p in _pairs)
            {
                bool a = j != null && j.HasClue(_case, p.이쪽);
                bool b = j != null && j.HasClue(_case, p.저쪽);
                sb.AppendLine($"  {p.이름}  {p.이쪽}{(a ? "○" : "×")} + {p.저쪽}{(b ? "○" : "×")}" +
                              (p.끝남 ? "   ← 이미 겹쳤다" : (a && b ? "   ← 곧 뜬다" : "")));
            }
            Debug.Log(sb.ToString(), this);
        }
#endif
    }
}
