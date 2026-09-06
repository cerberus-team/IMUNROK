using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>물증을 주우면 그 자리에서 한 번 펼쳐 보인다</b> — 「새로 얻은 것」.
    ///
    /// 여태 물증을 얻어도 <b>아무 일도 일어나지 않았다</b>. 재를 헤쳐 서찰 조각을 찾아도,
    /// 눌린 자국을 알아봐도, 수첩 어딘가에 한 줄이 늘 뿐이었다. 그러면 무엇을 얻었는지
    /// 확인하려고 <b>수첩을 따로 펴야</b> 하고, 대개는 그러지 않는다 — 얻은 줄도 모르고 지나간다.
    ///
    /// 견우팀 꾸러미는 이 자리를 이렇게 둔다: 물건을 얻으면 목록을 건너뛰고 <b>그 물건의
    /// 상세가 저절로 뜬다</b>. 제목이 「새로 얻은 것」이 되고, 물러나기는 「목록으로」가
    /// 아니라 「닫기」다 — 목록이 아니라 <b>하던 일</b>로 돌아가기 때문이다. 같은 얼개를 받는다.
    ///
    /// <b>안 띄우는 자리가 셋이다.</b> 이것이 이 부품의 절반이다 —
    ///
    ///   · 이미 그 종이를 손에 들고 있을 때. 방에서 집어 읽다가 기록되는 것이 흔한데,
    ///     그때 또 펴면 <b>보고 있던 종이가 저 혼자 다시 뜬다</b>.
    ///   · 심문 중일 때. 마주 앉아 묻는 도중에 종이가 화면을 덮으면 말이 끊긴다.
    ///     증언으로 얻은 것은 어차피 물건이 아니다.
    ///   · 딸린 종이가 없을 때. 들은 말·본 것에는 펼칠 것이 없다.
    ///
    /// 씬에 놓을 것이 없다. 판이 올라오면 저 혼자 붙는다.
    /// </summary>
    public class NewFindNotice : MonoBehaviour
    {
        private static NewFindNotice _live;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_live != null) return;
            var go = new GameObject("_새로얻은것");
            DontDestroyOnLoad(go);
            _live = go.AddComponent<NewFindNotice>();
        }

        private void OnEnable()
        {
            var j = Journal.Instance;
            if (j != null) { j.OnClueAdded -= OnAdded; j.OnClueAdded += OnAdded; }
        }

        private void OnDisable()
        {
            var j = Journal.Instance;
            if (j != null) j.OnClueAdded -= OnAdded;
        }

        private void OnAdded(ClueEntry entry)
        {
            if (entry == null || entry.kind != ClueKind.물증) return;

            // 손에 이미 그 종이가 있으면 두 번 펴지 않는다.
            if (DocumentView.IsOpen) return;

            // 마주 앉아 묻는 중에는 말이 먼저다.
            if (InterrogationController.AnyOpen) return;

            var gs = GameState.Instance;
            if (gs == null || !gs.InCase) return;
            var doc = Journal.Instance.GetDocument(gs.CurrentCase.Value, entry.key);
            if (doc == null || doc.page == null) return;   // 펼칠 종이가 없는 것은 들은 말이다

            // <b>제목에 「새로 얻은 것」을 얹는다.</b> 같은 종이를 수첩에서 꺼내 볼 때와
            // 겉모습이 같으면, 방금 얻었다는 것이 화면 어디에도 안 적힌다.
            DocumentView.Show(doc.page, "새로 얻은 것 — " + doc.title, doc.body, doc.fine,
                              null, true, null, null, null, null, doc.back);
        }
    }
}
