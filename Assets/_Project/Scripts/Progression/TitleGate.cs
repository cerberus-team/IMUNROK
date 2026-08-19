using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 표제 — 게임을 켜면 가장 먼저 오는 자리. 어전 씬의 앞 열두 마디다.
    ///
    /// 어둠 속에 소반 하나, 그 위에 봉서 한 통. 허공에 「이문록」 석 자.
    /// 봉서를 집으면 제목이 스러지고 어전에 불이 들며 어명이 시작된다.
    ///
    /// 왜 메뉴가 아니라 물건인가: 헤드셋을 쓰면 눈앞에 뜬 버튼은 손이 닿지 않는
    /// 유리창처럼 느껴진다. 첫 동작이 <b>집는 것</b>이면, 이 게임이 손으로 물건을
    /// 다루는 게임이라는 걸 첫 3초에 가르친다. 그래서 시작도 이어하기도 버튼이
    /// 아니라 물건이다 — 봉서와 수첩.
    ///
    /// 왜 씬을 따로 안 만드는가: 표제와 어명을 끊어 놓으면 플레이어가 두 번 들어오게
    /// 된다. 어둠에서 제목이 스러지고 그 자리에 불이 드는 편이 한 번에 이어진다.
    ///
    /// 붙이는 법: 어전 씬의 빈 오브젝트에 붙이고
    ///   · _startObject   에 봉서(콜라이더 있는 것)
    ///   · _continueObject 에 수첩(없으면 비워도 된다)
    ///   · _intro         에 IntroController
    ///   · _hideUntilStart 에 어전의 불·왕·봉서 셋 등 시작 전엔 없어야 할 것들
    /// </summary>
    public class TitleGate : MonoBehaviour
    {
        [Header("제목")]
        [SerializeField] private string _title = "이문록";
        [SerializeField] private string _subtitle = "異聞錄";
        [Tooltip("제목이 뜰 자리. 비우면 카메라 앞 2.2m 에 띄운다")]
        [SerializeField] private Transform _titleAnchor;
        [SerializeField] private float _titleDistance = 2.2f;
        [SerializeField] private float _titleHeight = 0.15f;
        [SerializeField] private Font _font;
        [SerializeField] private Color _titleColor = new Color(0.97f, 0.93f, 0.82f);
        [SerializeField] private Color _subtitleColor = new Color(0.78f, 0.24f, 0.19f);

        [Header("집을 것")]
        [Tooltip("집으면 처음부터 시작. 콜라이더가 있어야 한다")]
        [SerializeField] private GameObject _startObject;
        [Tooltip("집으면 이어하기. 저장이 없으면 저절로 감춰진다. 없어도 된다")]
        [SerializeField] private GameObject _continueObject;

        [Header("시작 전엔 없어야 할 것")]
        [Tooltip("어전의 불·왕·봉서 셋 등. 표제 동안 꺼둔다")]
        [SerializeField] private GameObject[] _hideUntilStart;

        [Tooltip("표제에만 있는 것 — 소반·등잔 따위. 시작하면 걷는다")]
        [SerializeField] private GameObject[] _hideAfterStart;
        [Tooltip("어명 진행 담당. 표제 동안 꺼두었다가 시작할 때 켠다")]
        [SerializeField] private IntroController _intro;

        [Header("무엇을 하라는 말")]
        [Tooltip("제목이 뜬 뒤 이만큼 있다가 안내가 나온다(초). 제목을 먼저 읽게 두는 뜸이다")]
        [SerializeField] private float _promptDelay = 1.6f;
        [Tooltip("저장이 없을 때")]
        [SerializeField] private string _prompt = "봉서를 집으라.";
        [Tooltip("저장이 있을 때 — 물건이 둘이라 어느 쪽인지 말해 줘야 한다")]
        [SerializeField] private string _promptWithSave = "봉서를 집으면 처음부터, 수첩을 집으면 하던 데부터.";
        [SerializeField] private string _promptHint = "(가리켜 누르기)";

        [Header("이어하기가 갈 곳")]
        [SerializeField] private string _hubSceneName = "HubScene";

        [Header("넘어가기")]
        [SerializeField] private float _fadeOut = 0.5f;
        [SerializeField] private float _fadeIn = 0.6f;

        private GameObject _titleGo;
        private bool _left;

        private void Start()
        {
            // 어전을 통째로 재워 둔다. 표제가 걷히면 깨운다.
            if (_hideUntilStart != null)
                foreach (var go in _hideUntilStart)
                    if (go != null) go.SetActive(false);
            if (_intro != null) _intro.enabled = false;

            // 이어할 것이 없으면 수첩은 아예 없는 편이 낫다 — 눌러도 아무 일이 없는
            // 물건이 놓여 있으면 플레이어는 그것을 고장으로 읽는다.
            if (_continueObject != null) _continueObject.SetActive(SaveSystem.HasSave);

            Arm(_startObject, false);
            Arm(_continueObject, true);

            BuildTitle();

            // 제목만 떠 있으면 무엇을 하라는 건지 알 수가 없다. 물건이 놓여 있어도
            // 그게 손대야 하는 것인지는 말해 주기 전엔 모른다.
            Invoke(nameof(ShowPrompt), Mathf.Max(0f, _promptDelay));
        }

        private void ShowPrompt()
        {
            if (_left) return;
            bool hasSave = _continueObject != null && _continueObject.activeSelf;
            SubtitleView.Show("", hasSave ? _promptWithSave : _prompt, _promptHint);
        }

        /// <summary>집을 수 있게 만든다. 콜라이더가 없으면 알려 준다.</summary>
        private void Arm(GameObject go, bool isContinue)
        {
            if (go == null || !go.activeSelf) return;
            if (go.GetComponent<Collider>() == null)
                Debug.LogWarning($"[TitleGate] '{go.name}' 에 콜라이더가 없어 집을 수 없습니다.", go);

            var opt = go.GetComponent<TitleOption>();
            if (opt == null) opt = go.AddComponent<TitleOption>();
            opt.Bind(this, isContinue);
        }

        // ── 제목 글씨 ──────────────────────────────────

        private void BuildTitle()
        {
            var cam = Camera.main;
            _titleGo = new GameObject("표제_글씨", typeof(Canvas));
            var canvas = _titleGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900f, 460f);
            rt.localScale = Vector3.one * 0.0016f;

            if (_titleAnchor != null)
            {
                rt.position = _titleAnchor.position;
                rt.rotation = _titleAnchor.rotation;
            }
            else if (cam != null)
            {
                rt.position = cam.transform.position + cam.transform.forward * _titleDistance + Vector3.up * _titleHeight;
                rt.rotation = Quaternion.LookRotation(rt.position - cam.transform.position);
            }

            var font = UiFont.Resolve(_font);
            UiFont.Publish(_font);

            MakeText(rt, _title, font, 190, _titleColor, new Vector2(0f, 40f));
            MakeText(rt, _subtitle, font, 72, _subtitleColor, new Vector2(0f, -140f));
        }

        private void MakeText(RectTransform parent, string s, Font font, int size, Color color, Vector2 offset)
        {
            var go = new GameObject(s, typeof(Text));
            var t = go.GetComponent<Text>();
            t.text = s;
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(880f, size + 40f);
        }

        // ── 집었을 때 ──────────────────────────────────

        /// <summary>봉서를 집었다 — 처음부터.</summary>
        public void BeginNew()
        {
            if (_left) return;
            _left = true;

            // 저장 파일은 지우지 않는다. 덮어쓸지 말지는 플레이어가 나중에 정할 일이고,
            // 여기서 말없이 지우면 잘못 집은 한 번으로 지난 진행이 사라진다.
            GameState.Instance.ResetAll();

            ScreenFade.Blink(_fadeOut, _fadeIn, OpenThrone);
        }

        /// <summary>수첩을 집었다 — 하던 데부터. 어명은 이미 들은 사람이라 건너뛴다.</summary>
        public void ContinueSaved()
        {
            if (_left) return;
            _left = true;

            if (!SaveSystem.Load())
            {
                Debug.LogWarning("[TitleGate] 불러오기에 실패해 처음부터 시작합니다.", this);
                _left = false;
                BeginNew();
                return;
            }

            ScreenFade.Blink(_fadeOut, _fadeIn, GoToHub);
        }

        private void OpenThrone()
        {
            if (_titleGo != null) Destroy(_titleGo);
            if (_startObject != null) _startObject.SetActive(false);
            if (_continueObject != null) _continueObject.SetActive(false);
            if (_hideAfterStart != null)
                foreach (var go in _hideAfterStart)
                    if (go != null) go.SetActive(false);

            if (_hideUntilStart != null)
                foreach (var go in _hideUntilStart)
                    if (go != null) go.SetActive(true);

            if (_intro != null) _intro.enabled = true;
            else Debug.LogWarning("[TitleGate] IntroController 가 연결되지 않아 어명이 시작되지 않습니다.", this);
        }

        private void GoToHub()
        {
            if (_titleGo != null) Destroy(_titleGo);

            if (string.IsNullOrEmpty(_hubSceneName) || !Application.CanStreamedLevelBeLoaded(_hubSceneName))
            {
                Debug.LogWarning($"[TitleGate] 조사청 씬('{_hubSceneName}')을 찾을 수 없습니다.", this);
                return;
            }
            SceneManager.LoadScene(_hubSceneName);
        }
    }

    /// <summary>
    /// 표제에 놓인 물건 하나(봉서 또는 수첩). 집히면 <see cref="TitleGate"/>에 알린다.
    /// TitleGate 가 코드로 붙이므로 인스펙터에서 끌어다 놓을 일은 없다.
    /// </summary>
    public class TitleOption : MonoBehaviour, ISelectable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private TitleGate _gate;
        private bool _isContinue;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private Color[] _base;

        public void Bind(TitleGate gate, bool isContinue)
        {
            _gate = gate;
            _isContinue = isContinue;

            // 가리키면 밝아지게 — 어둠 속에 놓인 물건은 손대도 되는 것인지
            // 눈으로 알 길이 없다. 반응이 있어야 만질 수 있는 것으로 읽힌다.
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            _base = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var m = _renderers[i].sharedMaterial;
                _base[i] = (m != null && m.HasProperty(BaseColorId)) ? m.GetColor(BaseColorId) : Color.white;
            }
        }

        public void OnHoverEnter() => Tint(0.34f);
        public void OnHoverExit() => Tint(0f);

        private void Tint(float toward)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, Color.Lerp(_base[i], Color.white, toward));
                _renderers[i].SetPropertyBlock(_mpb);
            }
        }

        public void OnSelect()
        {
            if (_gate == null) return;
            SubtitleView.Hide();
            if (_isContinue) _gate.ContinueSaved();
            else _gate.BeginNew();
        }
    }
}
