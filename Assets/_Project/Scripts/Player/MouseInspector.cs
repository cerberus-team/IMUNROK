using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// "살펴보기" 도구(확대경 대역).
    /// 카메라에서 마우스 방향으로 레이를 쏘아 IInspectable 대상을 가리키면
    /// 화면에 제목·본문 패널을 띄우고, 처음 가리킨 순간 OnInspected()를 호출한다.
    ///
    /// ★ 지금은 "가리키면 바로" 보이지만, 실제 게임에선 "확대경을 든 상태"에서만
    ///   보이도록 게이트할 예정(확대경을 손에 든 것과 잇는다). 그래도 대상 로직
    ///   (IInspectable / InspectableNote)은 그대로 재사용된다.
    /// </summary>
    public class MouseInspector : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _maxDistance = 20f;
        [SerializeField] private LayerMask _mask = ~0;
        [Tooltip("돋보기로 취급할 도구 id")]
        [SerializeField] private string _magnifierToolId = "magnify";

        private IInspectable _current;
        private bool _needMagHint;   // 돋보기 필요한데 안 든 물건을 가리키는 중

        // ── 판 ──────────────────────────────────────────────────
        //
        // <b>IMGUI 에서 캔버스로 옮겼다.</b> 이 판만 낡은 방식으로 남아 있었는데,
        // 그러면 두 가지가 어긋난다:
        //   · <b>글씨</b> — IMGUI 는 TMP 를 못 쓴다. 다른 판이 전부 조선궁서체 SDF 로
        //     옮겨 간 마당에 이 판만 운영체제 래스터라이저로 그려져 획이 가늘었다.
        //   · <b>헤드셋</b> — IMGUI 는 화면 버퍼에 곧장 그리므로 스테레오 경로를 안 탄다.
        //     견우 꾸러미 문서의 첫 경고가 그것이다 — HMD 에서는 아예 안 보인다.
        private RectTransform _panel;
        private Image _back;
        private TextMeshProUGUI _title, _body;

        /// <summary>꾸러미의 결. 정적으로 들면 판을 넘겨 살아남되 내용이 죽는다 — 제 것으로 든다.</summary>
        private readonly IMUNROK.Ui.InventorySkin _skin = new IMUNROK.Ui.InventorySkin();

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) _camera = Camera.main;
            Build();
        }

        /// <summary>
        /// 판을 짓는다. 자리는 IMGUI 시절 그대로 <b>오른쪽 위</b>다 — 옮기는 김에 자리까지
        /// 바꾸면 무엇 때문에 달라 보이는지 가릴 수 없게 된다.
        /// 치수는 화면 1732단위로 환산했다(380×120 화소 ≒ 640×202).
        /// </summary>
        private void Build()
        {
            var go = new GameObject("살펴보기_판", typeof(RectTransform));
            ScreenPanel.Raise(go, ScreenPanel.LayerBar - 10);
            var root = (RectTransform)go.transform;

            const float W = 640f, H = 202f, Pad = 24f;
            _panel = new GameObject("바탕", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            _panel.SetParent(root, false);
            _panel.anchorMin = _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(1f, 1f);
            _panel.anchoredPosition = new Vector2(-34f, -98f);   // 우상단 지도 단추와 안 겹치게
            _panel.sizeDelta = new Vector2(W, H);
            _back = _panel.GetComponent<Image>();
            _back.sprite = _skin.Slot_;
            _back.color = UiLook.With(UiLook.Panel, 0.62f);
            _back.raycastTarget = false;

            _title = UiText.New("제목", "", new Vector2(0f, H * 0.5f - 34f), new Vector2(W - Pad * 2f, 44f),
                                _panel, 30f, TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.6f));
            _title.fontStyle = FontStyles.Bold;
            UiText.Wrap(_title, false);

            _body = UiText.New("글", "", new Vector2(0f, -18f), new Vector2(W - Pad * 2f, H - 76f),
                               _panel, 26f, TextAnchor.UpperLeft, UiLook.Text);
            UiText.Wrap(_body, true);

            _panel.gameObject.SetActive(false);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null || _camera == null) return;

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            IInspectable hit = null;
            if (Physics.Raycast(ray, out RaycastHit info, _maxDistance, _mask))
                hit = info.collider.GetComponentInParent<IInspectable>();

            // 이 물건이 '돋보기 필요' 표시인데 지금 들여다보고 있지 않으면 → 힌트만, 단서 기록 X
            //
            // 예전에는 '돋보기를 들었는가'만 봤다. 그러면 돋보기는 주머니에 든 열쇠지
            // 눈에 대는 유리가 아니다. 이제는 렌즈를 눈에 대고 그 물건을 짚어야 한다.
            bool hasMag = MagnifierLens.Peering
                          || (ToolbeltHud.SelectedToolId == _magnifierToolId && !MagnifierLens.Held);
            bool needsMag = (hit as InspectableNote)?.RequiresMagnifier ?? false;
            if (hit != null && needsMag && !hasMag)
            {
                _current = null;
                _needMagHint = true;
                return;
            }
            _needMagHint = false;

            if (!ReferenceEquals(hit, _current))
            {
                _current = hit;
                _current?.OnInspected(); // 처음 가리킨 순간 1회(단서 자동 기록 등)
            }
#endif
        }

        private void LateUpdate()
        {
            if (_panel == null) return;

            // 수첩을 펴면 살펴보기는 물러난다 — 읽는 자리는 하나다.
            bool show = !JournalView.AnyOpen && (_current != null || _needMagHint);
            if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);
            if (!show) return;

            if (_current == null)
            {
                // 돋보기 필요한 물건인데 안 든 상태 → 안내 한 줄만. 바탕도 안 깐다.
                _back.enabled = false;
                _title.text = MagnifierLens.Held ? "돋보기를 눈에 대야겠다"
                                                 : "돋보기로 자세히 봐야 할 것 같다…";
                _body.text = "";
                return;
            }

            _back.enabled = true;
            _title.text = _current.GetInspectTitle();
            _body.text = Emphasis.Rich(_current.GetInspectBody(), Emphasis.OnDark);
        }

        private void OnDestroy()
        {
            if (_panel != null && _panel.parent != null) Destroy(_panel.parent.gameObject);
        }
    }
}
