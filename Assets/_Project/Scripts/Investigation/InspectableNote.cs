using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 코드 없이 인스펙터로 채우는 "살펴보기 대상". 사건 팀원이 단서 오브젝트에 붙이고
    /// 제목·본문을 적으면 된다. 원하면 살펴본 순간 수첩(Journal)에 단서를 자동 기록한다.
    ///
    /// [사용] 단서가 될 오브젝트(콜라이더 필요)에 이 컴포넌트를 붙이고 값 입력:
    ///   Title  : "옹고집의 점"
    ///   Body   : "왼뺨에 점이 없다. 진짜에게는 있었다."
    ///   Record Clue 체크 → Clue Case/Key/Text 입력 시, 살펴보면 수첩에 자동 기록.
    ///
    /// 문서라면 <b>종이 면(_page)</b>을 걸어 둔다. 비워 두면 이 물건의 재질에서 알아서
    /// 찾는다. 방에서 짚으면 그 종이를 <b>손에 쥐고</b> 끌어서 돌려 볼 수 있다
    /// (<see cref="DocumentView"/>). 이름은 종이에 붙은 표제 쪽지로 적히고, 요약은
    /// <b>수첩</b>에서 읽는다. 잔글씨(_fineText)는 돋보기로 들여다봐야 읽힌다.
    ///
    /// 종이가 없는 것 — 재 무더기 안쪽처럼 쥘 수 없는 것 — 은 그 자리에 이름 한 줄만
    /// 뜬다(<see cref="WorldNote"/>).
    /// </summary>
    public class InspectableNote : MonoBehaviour, IInspectable, ISelectable, IMagnifiable
    {
        [Header("눌러서 손에 쥐기")]
        [Tooltip("손에 쥐고 볼 종이 면. 비우면 이 물건의 재질에서 알아서 찾는다")]
        [SerializeField] private Texture2D _page;
        [Tooltip("끄면 눌러도 안 쥐어진다(쥐어 볼 것이 없는 물건)")]
        [SerializeField] private bool _canOpen = true;
        [Tooltip("이 거리(m) 안에서만 쥘 수 있다")]
        [SerializeField] private float _maxTouchDistance = 3f;
        [TextArea]
        [Tooltip("종이에 작게 적히는 글. 돋보기를 대야 읽힌다. 비우면 본문만 보인다")]
        [SerializeField] private string _fineText = "";
        [Tooltip("돋보기로 읽어야만 단서가 적힌다. 끄면 쥐어 보기만 해도 적힌다")]
        [SerializeField] private bool _clueNeedsMagnifier = false;

        [SerializeField] private string _title = "";
        [TextArea]
        [SerializeField] private string _body = "";

        [Tooltip("켜면 '돋보기'로 들여다봐야 보인다(손목 흉터·필적 등 세밀한 단서). 끄면 맨눈으로도 보임")]
        [SerializeField] private bool _requiresMagnifier = false;
        public bool RequiresMagnifier => _requiresMagnifier;

        [Header("살펴보면 수첩에 자동 기록(선택)")]
        [SerializeField] private bool _recordClue = false;
        [SerializeField] private CaseId _clueCase = CaseId.Case1_Onggojip;
        [Tooltip("중복 방지용 식별자(비우면 제목 사용)")]
        [SerializeField] private string _clueKey = "";
        [Tooltip("기록할 단서 문구(비우면 본문 사용)")]
        [TextArea]
        [SerializeField] private string _clueText = "";
        [Tooltip("이 단서의 상황 그림(선택). 수첩 카드·증거 제시 때 뜸")]
        [SerializeField] private Texture2D _clueImage;

        private bool _recorded;

        public string GetInspectTitle() => _title;
        public string GetInspectBody() => _body;

        /// <summary>
        /// 가리키는 것만으로는 <b>아무 일도 없다</b>.
        ///
        /// 예전에는 여기서 단서를 적었다. 마우스가 스치기만 해도 수첩에 오른다는 뜻이라,
        /// 방에 들어서서 고개 한 번 돌리면 증거가 저절로 쌓였다 — 닫힌 장 속의 수표까지
        /// 그렇게 올라와 있었다. 조사는 <b>손을 대는 일</b>이지 눈이 스치는 일이 아니다.
        /// 그래서 적는 것은 누를 때(OnSelect)와 돋보기로 들여다볼 때(OnMagnifiedGaze)뿐이다.
        /// </summary>
        public void OnInspected() { }

        private void Record()
        {
            if (!_recordClue || _recorded) return;

            string key = string.IsNullOrEmpty(_clueKey) ? _title : _clueKey;
            string text = string.IsNullOrEmpty(_clueText) ? _body : _clueText;
            Journal.Instance.AddClue(_clueCase, key, text, _clueImage);
            Remember(key);
            _recorded = true;
        }

        /// <summary>
        /// 수첩에서 이 문서를 다시 펼쳐 볼 수 있게 종이를 함께 걸어 둔다.
        ///
        /// 물증은 한 번 보고 마는 것이 아니다. 심문 도중 "그 장부에 뭐라 적혀 있었더라"
        /// 하고 되짚어야 하는데, 방에 두고 온 종이를 다시 보러 돌아갈 수는 없다.
        /// </summary>
        private void Remember(string key)
        {
            var page = ResolvePage();
            if (page == null) return;
            Journal.Instance.AttachDocument(_clueCase, key, page, _title, _body, _fineText);
        }

        // ── 눌러서 손에 쥐기 ────────────────────────

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        /// <summary>
        /// 종이를 손에 쥔다 — 방은 그대로 두고.
        ///
        /// 손바닥만 한 종잇장을 마루에 놓인 채로 읽을 수는 없다. 그렇다고 화면을 까맣게
        /// 덮으면 조사하던 방이 사라진다. 그래서 종이만 눈앞으로 들어올린다.
        /// 잔글씨는 종이에 작게 적힌 채로 올라오므로, 읽으려면 돋보기를 대야 한다.
        /// </summary>
        public void OnSelect()
        {
            if (!_canOpen) return;

            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;

            // 종이는 <b>집어 든다</b>. 마루에 엎어진 채로 들여다보는 문서는 없다.
            //
            // 한때는 이름 한 줄만 물건 위에 띄우고 말았다. 방을 뒤지다 말고 커다란 글자판이
            // 열리는 것을 막자는 뜻이었는데, 그러다 보니 문서를 <b>본 적이 없는데</b>
            // 수첩에는 올라 있는 일이 생겼다. 짚었다는 것과 읽었다는 것이 같아진 것이다.
            // 이제는 손에 쥐고, 돌려 보고, 그러고 나서 적힌다.
            //
            // 손에 든 것에는 종이와 표제뿐이다. 요약은 수첩에서 읽는다 — 방에서는 아직
            // 무엇인지 알아보는 중이지 정리하는 중이 아니다.
            var page = ResolvePage();
            if (page != null)
            {
                DocumentView.Show(page, _title, "", _fineText,
                                  _clueNeedsMagnifier ? new System.Action(Record) : null);
            }
            else
            {
                // 종이가 아닌 것 — 재 안쪽처럼 쥘 수 없는 것은 이름만 그 자리에 뜬다.
                WorldNote.Show(transform, _title);
            }

            // 맨눈으로도 아는 것이면 쥔 것만으로 적힌다.
            // 잔글씨라야 아는 것이면 돋보기로 다 읽어야(위 onRead) 적힌다.
            if (!_clueNeedsMagnifier) Record();
        }

        // ── 돋보기로 들여다보기 ────────────────────

        /// <summary>
        /// 방에 놓인 채로 돋보기를 대고 들여다본 것. 손목 흉터처럼 쥘 수 없는 것에 쓴다.
        /// </summary>
        public void OnMagnifiedGaze(float progress)
        {
            if (progress < 1f || _recorded) return;

            // 적을 것이 없는 물건은 들여다봐도 아무 말도 하지 않는다. 빈 종이에 대고
            // "수첩에 적어 두었다" 하면, 적힌 것도 없는데 다 본 줄로 알고 지나간다.
            if (!_recordClue) return;

            Record();
            WorldNote.Show(transform, _title + "  —  수첩에 적어 두었다");
        }

        /// <summary>쥐어 보일 종이 면. 손으로 걸어 두지 않았으면 제 재질에서 찾는다.</summary>
        private Texture2D ResolvePage()
        {
            if (_page != null) return _page;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null) continue;
                if (m.HasProperty(BaseMapId)) { var t = m.GetTexture(BaseMapId) as Texture2D; if (t != null) return t; }
                if (m.mainTexture is Texture2D mt) return mt;
            }
            return null;
        }

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    }
}
