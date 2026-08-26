using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>사목(事目)</b> — 어사에게 내리는 직무 규정서.
    ///
    /// 고증: 암행어사가 받는 것은 넷이다. <b>봉서</b>(누구를 어디로 보내는가),
    /// <b>사목</b>(무엇을 어떻게 살필 것인가), <b>마패</b>(역마를 징발하는 표),
    /// <b>유척</b>(검시와 도량형을 재는 놋자). 이 게임에는 봉서도 마패도 유척도
    /// 진작 있었는데 <b>사목만 없었다</b> — 넷 중 하나가 빠져 있던 셈이다.
    ///
    /// 게다가 사목은 고증이면서 <b>규칙서</b>이기도 하다. 실제 사목이 "무엇을
    /// 살피고 무엇은 손대지 말라" 를 적은 문서이니, 이 게임에서 할 수 있는 일과
    /// 해서는 안 되는 일을 적어 두기에 그보다 맞는 그릇이 없다. 규칙을 메뉴로
    /// 만들면 방 안에 있던 사람이 화면 앞으로 끌려 나오지만, 사목으로 만들면
    /// <b>조사청 서안 위의 종이 한 장</b>이 된다.
    ///
    /// 붙이는 법: 두루마리 모양에 콜라이더를 주고 이 부품을 얹는다.
    /// [이문록 ▸ 조사청 ▸ 사목 두기] 가 대신 놓아 준다.
    /// </summary>
    public class Samok : MonoBehaviour, ISelectable
    {
        [Tooltip("가리켰을 때 뜰 이름")]
        [SerializeField] private string _label = "사목(事目)";

        [Tooltip("펼쳤을 때의 표제")]
        [SerializeField] private string _title = "사목(事目) — 어사가 받드는 조목";

        [TextArea(10, 30)]
        [Tooltip("펼쳤을 때 읽히는 것. 고증이자 이 게임의 규칙서다")]
        [SerializeField]
        private string _body =
            "하나. 어사는 이름을 밝히지 아니한다.\n" +
            "    마패를 내보이기 전까지 그대는 지나가는 나그네다.\n" +
            "    이름을 밝히면 듣던 말이 그친다.\n" +
            "\n" +
            "둘. 본 것만 적는다.\n" +
            "    수첩에 오르는 것은 <b>손에 잡히는 물증</b>뿐이다.\n" +
            "    들은 말은 적히지 아니한다 — 받아 적는 것은 조사관의 몫이다.\n" +
            "\n" +
            "셋. 물증으로 묻는다.\n" +
            "    심문에서 상대의 입을 여는 것은 다그침이 아니라 종이 한 장이다.\n" +
            "    수첩에서 골라 내밀라.\n" +
            "\n" +
            "넷. 도구는 받은 것이다.\n" +
            "    <b>수첩</b>은 적고, <b>지도</b>는 길을 알고, <b>등불</b>은 어둠을 걷고,\n" +
            "    <b>돋보기</b>는 잔글씨를 읽고, <b>유척</b>은 치수를 재고,\n" +
            "    <b>마패</b>는 신분을 드러낸다. 선반에서 집는 물건이 아니다.\n" +
            "\n" +
            "다섯. 소리를 삼간다.\n" +
            "    잠행 중에 낸 소리는 쌓인다. 쌓이면 사람이 나온다.\n" +
            "\n" +
            "여섯. 판결은 되돌릴 수 없다.\n" +
            "    대청에 올라 내리는 말 한마디로 사건이 닫힌다.\n" +
            "    닫기 전에 수첩을 한 번 더 펴라.";

        [Tooltip("돋보기로 들여다봐야 읽히는 잔글씨. 비우면 없다")]
        [TextArea(2, 6)]
        [SerializeField]
        private string _fine =
            "…어사가 사사로이 형벌을 쓰거나 재물을 받는 일이 있으면, 그 죄를 어사에게 묻는다.";

        [Tooltip("펼칠 종이 그림(선택). 없으면 글만 뜬다")]
        [SerializeField] private Texture2D _page;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private Color _home = new Color(0.85f, 0.80f, 0.68f);
        private bool _hovered;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
        }

        public void OnHoverEnter()
        {
            _hovered = true;
            Tint(Color.Lerp(_home, Color.white, 0.30f));
            SubtitleView.Show("", _label, "(가리켜 누르면 펼친다)");
        }

        public void OnHoverExit()
        {
            if (!_hovered) return;
            _hovered = false;
            Tint(_home);
            SubtitleView.Hide();
        }

        public void OnSelect()
        {
            SubtitleView.Hide();
            DocumentView.Show(_page, _title, _body, string.IsNullOrEmpty(_fine) ? null : _fine);
        }

        private void Tint(Color c)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
