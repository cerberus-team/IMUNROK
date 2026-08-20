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
    /// </summary>
    public class InspectableNote : MonoBehaviour, IInspectable, ISelectable
    {
        [Header("눌러서 크게 보기")]
        [Tooltip("눈앞에 펼쳐 보여줄 종이 면. 비우면 이 물건의 재질에서 알아서 찾는다")]
        [SerializeField] private Texture2D _page;
        [Tooltip("끄면 눌러도 안 펼쳐진다(펼쳐 볼 것이 없는 물건)")]
        [SerializeField] private bool _canOpen = true;
        [Tooltip("이 거리(m) 안에서만 펼쳐 볼 수 있다")]
        [SerializeField] private float _maxTouchDistance = 3f;
        [TextArea]
        [Tooltip("돋보기로 들여다봐야 비로소 읽히는 것. 비우면 본문만 보인다")]
        [SerializeField] private string _fineText = "";
        [Tooltip("돋보기로 읽어야만 단서가 적힌다. 끄면 맨눈으로 펼쳐도 적힌다")]
        [SerializeField] private bool _clueNeedsMagnifier = false;
        [Tooltip("돋보기로 취급할 도구 id")]
        [SerializeField] private string _magnifierToolId = "magnify";

        [SerializeField] private string _title = "";
        [TextArea]
        [SerializeField] private string _body = "";

        [Tooltip("켜면 '돋보기'를 손에 들었을 때만 보인다(손목 흉터·필적 등 세밀한 단서). 끄면 맨눈으로도 보임")]
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

        public void OnInspected()
        {
            // 가리키기만 해서 적히는 단서는 '돋보기로만 읽히는' 것이 아니어야 한다.
            if (_clueNeedsMagnifier) return;
            Record();
        }

        private void Record()
        {
            if (!_recordClue || _recorded) return;

            string key = string.IsNullOrEmpty(_clueKey) ? _title : _clueKey;
            string text = string.IsNullOrEmpty(_clueText) ? _body : _clueText;
            Journal.Instance.AddClue(_clueCase, key, text, _clueImage);
            _recorded = true;
        }

        // ── 눌러서 크게 보기 ────────────────────────

        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        /// <summary>
        /// 종이를 눈앞에 펼친다 — <b>집어 들지 않고</b>.
        ///
        /// 손바닥만 한 종잇장을 마루에 놓인 채로 읽을 수는 없다. 그렇다고 집어 들게 하면
        /// 손에 든 물건이 하나 더 늘 뿐이다. 돋보기를 들여다보는 일은 물건을 옮기는 게
        /// 아니라 눈을 갖다 대는 것이므로, 종이는 그 자리에 두고 면만 끌어와 펼친다.
        /// </summary>
        public void OnSelect()
        {
            if (!_canOpen) return;

            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;

            bool magnified = ToolbeltHud.SelectedToolId == _magnifierToolId;
            string text = _body;
            if (magnified && !string.IsNullOrEmpty(_fineText))
                text = string.IsNullOrEmpty(_body) ? _fineText : _body + System.Environment.NewLine + _fineText;

            DocumentView.Show(ResolvePage(), _title, text, magnified);

            if (magnified || !_clueNeedsMagnifier) Record();
        }

        /// <summary>펼쳐 보일 종이 면. 손으로 걸어 두지 않았으면 제 재질에서 찾는다.</summary>
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
