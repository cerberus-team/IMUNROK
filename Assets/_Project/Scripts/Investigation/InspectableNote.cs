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
        [Tooltip("<b>옛 칸 — 이제 아무것도 가르지 않는다</b>(2026-09-13). 무엇이든 쥐면 적히고, " +
                 "돋보기로 다 읽으면 아래 「돋보기로 다 읽고 나서」 한 줄이 그 뒤에 덧붙는다. " +
                 "씬에 켜 둔 값이 남아 있어 지우지 않았다")]
        [SerializeField] private bool _clueNeedsMagnifier = false;

        [Tooltip("<b>종이 뒤를 어둡게 깐다.</b> 여태 꺼져 있었다 — 방 안 물건들 위에 종이가 " +
                 "겹쳐 떠서 어디까지가 읽을 것인지 눈이 못 갈랐다. 읽는 동안은 방이 물러나야 한다")]
        [SerializeField] private bool _dimBehind = true;

        [Tooltip("<b>돋보기로 다 읽고 나서</b> 수첩의 그 단서에 덧붙는 한 줄. 비우면 안 붙는다. " +
                 "종이 위에 우리말을 굳혀 붙이는 대신 여기에 적는다 — 방에서는 유리로 읽고, " +
                 "정리는 수첩에서 한다. " +
                 "<b>「돋보기라야 아는 것」이 아니어도 붙는다</b>: 맨눈으로 집어 적힌 줄에도 " +
                 "유리로 끝까지 읽으면 이 한 줄이 덧붙는다. 읽은 사람과 집기만 한 사람은 " +
                 "수첩이 달라야 한다")]
        [TextArea(2, 4)]
        [SerializeField] private string _clueNoteAfterMagnify = "";

        [Header("종이가 아닌 것")]
        [Tooltip("무대에 세워 돌려 볼 3D 모델(선택). 넣으면 종이 대신 이것이 뜬다 — " +
                 "마패·유척·부서진 자물쇠처럼 앞뒤가 아니라 사방이 있는 물건에 쓴다")]
        [SerializeField] private GameObject _model;
        [Tooltip("무대에 올릴 때의 첫 자세(도). 눕혀 두면 앞이 안 보이는 물건이 있다")]
        [SerializeField] private Vector3 _modelEuler;

        [Tooltip("<b>같은 종이의 한글 벌</b>(선택). 있으면 <b>돋보기를 댄 자리만</b> 한자가 " +
                 "우리말로 바뀐다 — 유리만 보는 켜에 얹히므로 맨눈에는 안 보인다. " +
                 "손으로 그릴 것이 아니라 <c>Tools/DocBaker</c> 가 굽는다: docs.json 의 각 줄에 " +
                 "<c>ko</c> 를 적어 두면 같은 칸·같은 줄·같은 도장으로 <c>&lt;이름&gt;_ko.png</c> 가 " +
                 "따라 나온다. 두 장을 손으로 맞추면 반드시 어긋나므로 반드시 구워서 쓸 것")]
        [SerializeField] private Texture2D _pageKo;

        [Tooltip("손에 들고 돋보기로 끝까지 읽으면 <b>이 눌린 자국도 읽은 것으로</b> 친다. " +
                 "빈 종이의 자국은 놓인 채로 유리를 대야만 읽혔다 — 집어 들고 유리를 대면 " +
                 "아무 일이 없어서, 빈 종이가 무엇을 주는지 알 길이 없었다. 들고 읽어도 같게 한다")]
        [SerializeField] private PressedMarks _revealsPressed;

        [Header("뒤집으면 나오는 것")]
        [Tooltip("종이 뒤에 새겨진 것(선택). 뒤집어야 나온다 — 맞은편 백지에 눌린 자국, 재에 닿아 그을린 쪽, 배접과 수결 같은 것. 비우면 예전처럼 민면이다")]
        [SerializeField] private Texture2D _backPage;

        [Header("등불에 비추면 나오는 것")]
        [Tooltip("배접 속에 숨긴 글이 보이는 종이 면(선택). 등불을 들면 원래 면 위로 배어 나온다")]
        [SerializeField] private Texture2D _litPage;
        [TextArea]
        [Tooltip("등불에 비춰야 드러나는 것. 잔글씨(돋보기)와 달리 이 글자는 원래 종이에 없던 것이다")]
        [SerializeField] private string _litText = "";
        [Tooltip("<b>옛 칸</b> — 이제 쥐면 적히고, 불빛에 다 배어 나오면 그 줄이 고쳐 적힌다(2026-09-13)")]
        [SerializeField] private bool _clueNeedsLantern = false;
        [Tooltip("획만 잡혔을 때 보여 줄 것 — 대개 한자 그대로다. " +
                 "잠깐 대면 무언가 비치는 것만 알고, 오래 대면 글자가 잡히고, " +
                 "끝까지 대야 뜻이 새겨진다. 그 가운데 켜에 쓴다")]
        [SerializeField] private string _litGlyphs = "";

        [Tooltip("불빛에 다 드러났을 때 <b>따로 적는 단서</b>의 식별자(비우면 안 적음)." +
                 "왜 따로 적는가: 위의 _litClueText 는 <b>이미 있는 줄을 고쳐 적는다</b>. " +
                 "같은 종이에서 나온 이야기라 줄을 늘리지 않으려는 뜻인데, 그러면 " +
                 "<b>등불로 본 사람과 그냥 읽은 사람을 가릴 수가 없다</b> — 열쇠가 같아서다. " +
                 "장계의 빈칸처럼 '등불까지 댄 사람만 쓸 수 있는 말'을 두려면 " +
                 "열쇠가 하나 더 있어야 한다")]
        [SerializeField] private string _litClueKey = "";

        [TextArea(2, 4)]
        [Tooltip("위 열쇠로 적을 문구")]
        [SerializeField] private string _litClueOwnText = "";

        [TextArea(2, 4)]
        [Tooltip("불빛에 다 드러났을 때 <b>수첩의 그 단서를 이 문구로 고쳐 적는다</b>(비우면 안 고침). " +
                 "단서 줄을 새로 늘리지 않는 까닭은, 같은 종이에서 나온 이야기가 " +
                 "두 줄로 갈라지면 어느 것이 그 종이였는지 알 수 없게 되기 때문이다")]
        [SerializeField] private string _litClueText = "";

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

            // <b>유리로 읽어낸 것은 수첩에 한 줄로 남는다.</b>
            //
            // 종이 위에 우리말을 굳혀 붙이면 두 가지가 나빠진다 — 한자 위에 한글이 겹쳐
            // 보기 어렵고, 한 번 읽고 나면 돋보기를 다시 들 까닭이 없어진다.
            // 방에서는 <b>유리를 통해</b> 읽고(DocumentView 의 유리 켜), 알아낸 것은
            // 여기 수첩에 적는다. 그래야 도구는 도구대로 남고 기록은 기록대로 쌓인다.
            //
            // 2026-09-13 — <b>집어 든 것만으로 적는다.</b> 유리로 읽은 한 줄은 여기서 미리 붙이지
            // 않고, 다 읽었을 때(OnFineRead) 그 줄 뒤에 덧붙는다. 그래야 수첩의 글이 <b>읽은 만큼
            // 늘어난다</b> — 집기만 한 사람과 끝까지 읽은 사람의 수첩이 다르다.
            if (_magnifyNoted && !string.IsNullOrWhiteSpace(_clueNoteAfterMagnify))
                text = text + System.Environment.NewLine + System.Environment.NewLine + _clueNoteAfterMagnify.Trim();

            // 이름도 함께 건다. 종이가 없는 물증(부서진 자물쇠)은 카드 이름을 달 데가 이것뿐이라,
            // 안 걸면 본문 첫머리가 잘려 이름 자리에 앉는다(「문갑 앞 바닥에 떨어져 있던…」).
            Journal.Instance.AddClue(_clueCase, key, text, _clueImage, ClueKind.물증, true,
                                     string.IsNullOrWhiteSpace(_title) ? null : _title);
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
            // 종이가 아니어도 <b>모형이 있으면</b> 건다 — 부서진 자물쇠처럼 쥘 종이가 없는 물증도
            // 수첩에서 돌려 볼 수 있어야 한다. 여태 종이가 없으면 그냥 돌아 나가 카드에 「?」가 떴다.
            var page = _model != null ? _page : ResolvePage();   // 모형이 있으면 모형의 무늬를 종이로 오인하지 않는다
            if (page == null && _model == null) return;
            Journal.Instance.AttachDocument(_clueCase, key, page, _title, _body, _fineText, _backPage,
                                            _model, _modelEuler);
            Journal.Instance.SetDocumentLayers(_clueCase, key, _pageKo, _litPage, _litText, _litGlyphs);
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
        /// <summary>
        /// 손이 닿는 거리를 따지지 않고 <b>바로 펴 준다</b>.
        ///
        /// 조사청에서 도구를 익힐 때 쓴다 — 그때 종이는 플레이어가 집어 든 것이 아니라
        /// 가르치는 쪽이 쥐여 준 것이므로, 방 저쪽에 놓여 있어도 손에 온다.
        /// </summary>
        public void OpenNow() { Open(); }

        public void OnSelect()
        {
            if (Watched()) return;
            if (!_canOpen) { TouchOnly(); return; }

            // 마주 앉아 말하는 중에는 방을 뒤지지 않는다.
            //
            // 화면에 남은 자국이 이것이었다 — 옹덕구가 아직 앞에 앉아 말하는데 그 위로
            // 빈 종이가 펼쳐졌다. 빈 종이라는 것은 복동이 아직 안 나갔다는 뜻이니,
            // 애초에 손이 가면 안 되는 때다. 도구벨트·노크·만지기거부는 이미 이 값을
            // 보고 있었는데 여기만 빠져 있었다.
            if (InterrogationController.AnyOpen) return;

            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;

            Open();
        }

        /// <summary>
        /// <b>집어 들 수 없는 물증을 짚었을 때.</b>
        ///
        /// 문갑 앞의 부서진 자물쇠가 이 경우다(<c>_canOpen = false</c>). 여태 짚으면
        /// <b>아무 일도 없이</b> 돌아 나갔다 — 적히는 길이 돋보기로 한참 들여다보는 것 하나뿐이라,
        /// 눌러 본 사람에게는 자물쇠가 그냥 무늬였고 문갑은 영영 안 열렸다(J15 가 여는 열쇠다).
        /// 맨눈으로 아는 것이면 짚는 것으로 알아본 것이다: 이름을 띄우고, 한 줄을 들려주고, 적는다.
        /// </summary>
        private void TouchOnly()
        {
            if (InterrogationController.AnyOpen) return;
            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;
            if (!string.IsNullOrEmpty(_body)) SubtitleView.Show("", _body, "");
            bool canRecord = _recordClue && !_recorded;
            WorldNote.Show(transform, canRecord ? _title + "  —  수첩에 적어 두었다" : _title);
            if (canRecord) Record();
        }

        // ── 수첩에서 다시 펴기 ────────────────────

        private static readonly System.Collections.Generic.Dictionary<string, InspectableNote> _byKey =
            new System.Collections.Generic.Dictionary<string, InspectableNote>();

        private static string KeyOf(CaseId c, string key) { return (int)c + "/" + key; }

        // 켜고 꺼질 때가 아니라 <b>살고 죽을 때</b> 적는다. 사랑방 종이는 방 밖에 서 있는 동안
        // 뿌리째 꺼져 있는데, 그때도 수첩에서는 그 종이를 펴야 한다.
        private void Awake()
        {
            if (!string.IsNullOrEmpty(_clueKey) && (_page != null || _pageKo != null))
                _byKey[KeyOf(_clueCase, _clueKey)] = this;
        }

        private void OnDestroy()
        {
            if (string.IsNullOrEmpty(_clueKey)) return;
            InspectableNote had;
            if (_byKey.TryGetValue(KeyOf(_clueCase, _clueKey), out had) && had == this)
                _byKey.Remove(KeyOf(_clueCase, _clueKey));
        }

        /// <summary>
        /// <b>수첩에서 이 종이를 방에서 집을 때와 똑같이 편다.</b>
        ///
        /// 수첩의 「손에 들고 본다」는 여태 종이 그림만 받아 폈다 — 한글 벌도, 다 읽었을 때
        /// 할 일도 없이. 그래서 수첩에서 돋보기를 대면 한자만 보이고, 끝까지 읽어도 수첩에
        /// <b>아무것도 안 붙었다</b>. 방에서 읽으나 수첩에서 읽으나 같아야 한다.
        /// 그 종이가 지금 씬에 없으면(2막에서 1막 종이를 펴는 경우) false — 부른 쪽이 그림만 편다.
        /// </summary>
        public static bool OpenFromJournal(CaseId c, string key)
        {
            InspectableNote n;
            if (string.IsNullOrEmpty(key) || !_byKey.TryGetValue(KeyOf(c, key), out n) || n == null) return false;
            var page = n.ResolvePage();
            if (page == null) return false;
            DocumentView.Show(page, n._title, "", n._fineText, new System.Action(n.OnFineRead), true,
                              n._litPage, n._litText, new System.Action(n.OnLitThrough),
                              n._litGlyphs, n._backPage, n._pageKo);
            return true;
        }

        /// <summary>실제로 펴는 일. 손이 닿는지·말하는 중인지는 부른 쪽이 이미 봤다.</summary>
        private void Open()
        {
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
                                  // <b>다 읽었으면 늘 부른다.</b> 여태 「돋보기라야 아는
                                  // 것」에만 걸어 두었다. 그래서 맨눈으로도 아는 문서를
                                  // 유리로 끝까지 읽어도 <b>수첩은 그대로</b>였다 —
                                  // 읽은 사람과 집기만 한 사람이 구별되지 않았다.
                                  new System.Action(OnFineRead),
                                  _dimBehind,
                                  _litPage, _litText,
                                  new System.Action(OnLitThrough),
                                  _litGlyphs, _backPage, _pageKo);
            }
            else
            {
                // 종이가 아닌 것 — 재 안쪽처럼 쥘 수 없는 것은 이름만 그 자리에 뜬다.
                WorldNote.Show(transform, _title);
            }

            // <b>쥔 것만으로 적힌다</b> — 무엇이든. 잔글씨·배접 속 글은 돋보기·등불로 다 읽었을 때
            // 그 줄에 <b>덧붙는다</b>(위 onRead · onLit). 여태 「돋보기라야 아는 것」은 쥐어도 수첩에
            // 안 올라, 집어 본 종이가 어디에도 남지 않았다.
            Record();
        }

        /// <summary>
        /// <b>돋보기로 잔글씨를 끝까지 읽었을 때</b> 한 번.
        ///
        /// 갈래가 둘이다.
        ///   · 쥐었을 때 이미 적혀 있다(2026-09-13 부터 무엇이든). 그 줄에 <b>유리로 알아낸 것만</b>
        ///     덧붙인다 — 수첩의 글이 읽은 만큼 늘어난다.
        ///   · 쥐지 않고 방에 놓인 채 들여다본 것이면 먼저 적고 덧붙인다.
        ///
        /// 왜 덧붙이는가: 유리로 읽어낸 말은 <b>그 자리에서만</b> 읽힌다(종이 위에 굳혀
        /// 붙이지 않는 까닭은 <see cref="DocumentView.ShowTranslation"/> 에). 그런데 한 시간
        /// 뒤 판결을 세울 때 「그 문서에 뭐라 적혀 있었더라」를 되짚을 곳이 없으면, 읽은
        /// 일이 <b>기억에만</b> 남는다. 기억은 증거가 아니다.
        /// </summary>
        private void OnFineRead()
        {
            if (_revealsPressed != null) _revealsPressed.OnMagnifiedGaze(1f);
            // <b>적는 일이 이 종이의 몫일 때만</b> 여기서 적고 끝낸다. 보료 밑 별급문기처럼
            // 줄은 <b>남이</b>(보료를 들춘 손이) 적고 이 종이는 유리로 읽은 것만 보태는 경우,
            // 여기서 Record 로 돌아 나가면 Record 가 「내 몫이 아니다」로 또 돌아 나가 —
            // 이름을 짚어 읽고도 수첩에 한 글자도 안 붙었다.
            // 2026-09-13 — 적힌 줄이 없으면 먼저 적고(쥐었으니), 그 줄 뒤에 유리로 읽은 것을 덧붙인다.
            Record();

            // <b>이 줄을 적는 것이 이 물건의 일이 아니어도 고쳐 적는다.</b> 아궁이 재처럼
            // <b>다른 것이</b> 같은 열쇠로 적어 둔 줄이 있다(AshRake 가 J10 을 적는다).
            // 그 줄에도 유리로 읽어낸 것은 덧붙어야 한다 — 열쇠가 같으면 같은 종이다.
            if (string.IsNullOrWhiteSpace(_clueNoteAfterMagnify) || string.IsNullOrEmpty(_clueKey)) return;
            if (_magnifyNoted) return;
            var j = Journal.Instance;
            if (j == null) return;

            string key = _clueKey;
            string had = string.IsNullOrEmpty(_clueText) ? _body : _clueText;
            // 이미 적혀 있으면 그 문구 위에 덧붙인다 — 남이 적어 둔 줄을 내 문구로
            // 갈아치우면 그쪽이 알려 준 것이 사라진다.
            var was = j.GetClues(_clueCase);
            for (int i = 0; i < was.Count; i++)
                if (was[i] != null && was[i].key == key && !string.IsNullOrEmpty(was[i].text))
                { had = was[i].text; break; }
            string more = had + System.Environment.NewLine + System.Environment.NewLine
                        + _clueNoteAfterMagnify.Trim();
            // 이미 있는 줄을 고쳐 적는다 — 같은 종이가 두 줄로 갈라지면 어느 것이 그
            // 종이였는지 알 수 없게 된다.
            if (!j.UpgradeClue(_clueCase, key, more)) j.AddClue(_clueCase, key, more, _clueImage);
            _magnifyNoted = true;
        }

        private bool _magnifyNoted;

        /// <summary>
        /// 불빛에 <b>다 배어 나왔을 때</b> 한 번.
        ///
        /// 쥐었을 때 이미 수첩에 올라 있다(아직이면 여기서 적는다). 그 줄을 <b>고쳐 적는다</b>(<see cref="_litClueText"/>) — 같은 종이에서
        /// 나온 이야기이므로 줄을 늘리지 않는다.
        /// </summary>
        private void OnLitThrough()
        {
            Record();

            // 등불로 본 사람만 갖는 열쇠. 이것이 있어야 "등불까지 댄 사람만 쓸 수 있는 말"을
            // 장계에 걸 수 있다.
            if (!string.IsNullOrEmpty(_litClueKey) && Journal.Instance != null)
                Journal.Instance.AddClue(_clueCase, _litClueKey,
                    string.IsNullOrEmpty(_litClueOwnText) ? _litClueText : _litClueOwnText, _clueImage);

            if (string.IsNullOrEmpty(_litClueText) || Journal.Instance == null) return;
            string key = string.IsNullOrEmpty(_clueKey) ? _title : _clueKey;
            // 불빛으로 고쳐 적는 줄은 <b>통째로 갈아 끼우는</b> 문구다. 그런데 유리로 먼저
            // 읽어 덧붙인 한 줄이 있으면 그것까지 함께 지워진다 — 읽은 순서에 따라 수첩이
            // 달라지면 안 된다. 이미 붙어 있던 유리 쪽 한 줄은 새 문구 뒤에 다시 붙인다.
            string lit = _litClueText;
            string note = string.IsNullOrWhiteSpace(_clueNoteAfterMagnify) ? null : _clueNoteAfterMagnify.Trim();
            if (note != null && !lit.Contains(note))
            {
                var had = Journal.Instance.GetClues(_clueCase);
                for (int i = 0; i < had.Count; i++)
                    if (had[i] != null && had[i].key == key && had[i].text != null && had[i].text.Contains(note))
                    { lit = lit + System.Environment.NewLine + System.Environment.NewLine + note; break; }
            }
            // 불빛 문구가 본문 첫머리로 시작하지 않으면 <b>덧붙인다</b> — 갈아 끼우면 「신고서다」 같은 설명이 사라졌다
            string had0 = null;
            foreach (var c in Journal.Instance.GetClues(_clueCase)) if (c != null && c.key == key) { had0 = c.text; break; }
            string head = string.IsNullOrEmpty(_clueText) ? "" : _clueText.Substring(0, Mathf.Min(12, _clueText.Length));
            if (had0 != null && head.Length > 0 && !lit.StartsWith(head) && !had0.Contains(lit.Trim()))
                lit = had0 + System.Environment.NewLine + System.Environment.NewLine + lit.Trim();
            if (!Journal.Instance.UpgradeClue(_clueCase, key, lit))
                Journal.Instance.AddClue(_clueCase, key, lit, _clueImage);
        }

        // ── 돋보기로 들여다보기 ────────────────────

        /// <summary>
        /// 방에 놓인 채로 돋보기를 대고 들여다본 것. 손목 흉터처럼 쥘 수 없는 것에 쓴다.
        /// </summary>
        public void OnMagnifiedGaze(float progress)
        {
            if (progress < 1f) return;
            if (Watched()) return;       // 주인 앞에서 돋보기로 들여다보는 것도 뒤지는 일이다
            bool noteLeft = !_magnifyNoted && !string.IsNullOrWhiteSpace(_clueNoteAfterMagnify);
            if (_recorded && !noteLeft) return;

            // 적을 것이 없는 물건은 들여다봐도 아무 말도 하지 않는다. 빈 종이에 대고
            // "수첩에 적어 두었다" 하면, 적힌 것도 없는데 다 본 줄로 알고 지나간다.
            if (!_recordClue && !noteLeft) return;

            OnFineRead();       // 적고, 유리로 읽은 한 줄을 덧붙인다 — 손에 들고 읽을 때와 같다
            WorldNote.Show(transform, _title + "  —  수첩에 적어 두었다");
        }

        /// <summary>
        /// <b>지금 누가 보고 있으면 안 뒤진다</b>(<see cref="TouchRefusal.WatchingNow"/>). 막는 말은 한 번만 띄운다.
        /// 수첩에서 다시 펴는 것(OpenFromJournal)은 뒤지는 일이 아니라 이 검사를 안 거친다.
        /// </summary>
        private bool Watched()
        {
            if (!TouchRefusal.WatchingNow || !IsInHouse()) return false;
            if (Time.unscaledTime > _watchedSaid)
            {
                SubtitleView.Show("", "주인이 보는 앞에서 남의 물건을 뒤질 수는 없다.", "");
                _watchedSaid = Time.unscaledTime + 3f;
            }
            return true;
        }

        private static float _watchedSaid;

        /// <summary>1막 옹씨댁의 물건인가(2막 관아 문서는 앉아 묻는 중에도 수첩으로 편다 — 여기 막을 까닭이 없다).</summary>
        private bool IsInHouse() { return !GameStateRevealed(); }

        private static bool GameStateRevealed() { var gs = GameState.Instance; return gs != null && gs.IdentityRevealed; }

        /// <summary>같은 열쇠를 쓰는 다른 물건(보료가 적는 별급문기)이 이 종이의 겹을 수첩에 넘겨 받는다.</summary>
        public void ShareLayers()
        {
            if (Journal.Instance == null || string.IsNullOrEmpty(_clueKey)) return;
            Journal.Instance.SetDocumentLayers(_clueCase, _clueKey, _pageKo, _litPage, _litText, _litGlyphs);
        }

        public string ClueKey => _clueKey;

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
