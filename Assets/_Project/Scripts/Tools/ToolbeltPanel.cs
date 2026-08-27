using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 도구벨트의 VR용 뷰 — <see cref="ToolbeltHud"/>가 들고 있는 상태를 월드 공간 Canvas로 그린다.
    /// (데스크탑 OnGUI 뷰는 ToolbeltHud 안에 그대로 있다. 둘은 같은 상태를 서로 다르게 그릴 뿐이다.)
    ///
    /// 붙이는 법:
    ///   1) 빈 GameObject "VR_도구벨트" 생성
    ///   2) Canvas + <see cref="WorldHudAnchor"/>(Placement=Waist) + 이 컴포넌트 추가
    ///   3) _font 에 한글 .ttf 연결 (안 하면 한글이 네모로 깨진다)
    ///   4) 씬에 ToolbeltHud가 있는지 확인 — 도구 목록은 거기서 읽어온다
    ///
    /// 슬롯은 시작할 때 코드로 만든다. 프리팹을 따로 만들 필요가 없고,
    /// 도구를 추가해도 ToolbeltHud의 목록만 고치면 여기 UI는 알아서 맞춰진다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class ToolbeltPanel : MonoBehaviour
    {
        [Header("모양")]
        [Tooltip("슬롯 한 변(px, Canvas 기준). WorldHudAnchor의 스케일과 곱해져 실제 크기가 된다")]
        [SerializeField] private float _slotSize = 120f;
        [SerializeField] private float _gap = 18f;
        [Tooltip("한글 .ttf. 비우면 기본 폰트라 한글이 깨진다")]
        [SerializeField] private Font _font;
        [SerializeField] private int _fontSize = 28;

        [Header("색")]
        [SerializeField] private Color _slotColor = new Color(0.05f, 0.05f, 0.06f, 0.72f);
        [SerializeField] private Color _selectedColor = new Color(0.62f, 0.14f, 0.11f, 0.85f);   // 낙관 붉은색
        [SerializeField] private Color _textColor = new Color(0.98f, 0.94f, 0.86f);

        private ToolbeltHud _belt;
        private readonly List<Image> _slotBgs = new List<Image>();
        private Text _caption;
        private CanvasGroup _group;

        /// <summary>코드로 만들 때 폰트를 넘겨준다(Start 전에 호출되어야 반영된다).</summary>
        public void Configure(Font font) => _font = font;

        private void Start()
        {
            _belt = ToolbeltHud.Instance;
            if (_belt == null)
            {
                Debug.LogWarning("[ToolbeltPanel] 씬에서 ToolbeltHud를 찾지 못했습니다. " +
                                 "도구벨트 상태를 읽을 수 없어 패널을 끕니다.");
                gameObject.SetActive(false);
                return;
            }

            // 내 폰트 → 공용 폰트(UiFont) → 유니티 기본 순으로 찾는다.
            _font = UiFont.Resolve(_font);
            if (UiFont.Korean == null)
                Debug.LogWarning("[ToolbeltPanel] 한글 폰트를 못 찾아 기본 폰트를 씁니다(한글이 네모로 깨짐). " +
                                 "ToolbeltHud의 VR 폰트 칸에 한글 .ttf를 연결하세요.");

            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            Build();
            _belt.OnChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_belt != null) _belt.OnChanged -= Refresh;
        }

        /// <summary>도구를 바꾼 뒤 이만큼 떠 있다가 스러진다(초).</summary>
        private const float LingerSeconds = 2.4f;
        private const float FadeSpeed = 3.2f;

        private float _linger = LingerSeconds;

        private void Update()
        {
            // 심문·수첩 중엔 벨트를 감춘다(데스크탑 뷰와 같은 규칙).
            // 오브젝트를 끄지 않고 알파만 내려야 WorldHudAnchor의 위치 추종이 안 끊긴다.
            if (_belt == null || _group == null) return;

            // 벨트는 <b>바꿀 때만</b> 뜬다.
            //
            // 허리 앞 0.6m 에 늘 떠 있게 두었더니, 헤드셋에서는 허리춤이지만 모니터로
            // 보면 <b>마룻바닥에 붉은 판이 하나 놓여 있는</b> 꼴이 되었다. 무엇보다
            // 조사청은 방을 둘러보는 곳인데, 시야 아래쪽 한 자리를 늘 도구판이 차지하고
            // 있으면 방이 그만큼 좁아진다. 손에 무엇을 들었는지는 손을 보면 되고,
            // 벨트는 <b>바꾸는 순간</b>에만 있으면 된다.
            _linger -= Time.deltaTime;
            bool show = !_belt.Hidden && _linger > 0f;

            float want = show ? 1f : 0f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, want, FadeSpeed * Time.deltaTime);
            _group.blocksRaycasts = _group.alpha > 0.5f;
        }

        // ─────────────────────────────────────────────

        private void Build()
        {
            int n = _belt.SlotCount;
            float totalW = n * _slotSize + (n - 1) * _gap;
            float x0 = -totalW * 0.5f + _slotSize * 0.5f;

            for (int i = 0; i < n; i++)
            {
                int slot = i;   // 클로저 캡처
                var bg = NewChild($"슬롯{i}", new Vector2(x0 + i * (_slotSize + _gap), 0f),
                                  new Vector2(_slotSize, _slotSize));
                var img = bg.gameObject.AddComponent<Image>();
                img.color = _slotColor;
                _slotBgs.Add(img);

                // 레이 인터랙터로 슬롯을 직접 집을 수 있게(Canvas.worldCamera는 WorldHudAnchor가 물려준다)
                var btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => _belt.Select(slot));

                var icon = _belt.SlotIcon(i);
                if (icon != null)
                {
                    var iconRt = NewChild("아이콘", Vector2.zero,
                                          new Vector2(_slotSize * 0.7f, _slotSize * 0.7f), bg);
                    var raw = iconRt.gameObject.AddComponent<RawImage>();
                    raw.texture = icon;
                    raw.raycastTarget = false;
                }
                else
                {
                    var label = NewText("이름", i == 0 ? "맨손" : First(_belt.SlotName(i)),
                                        Vector2.zero, new Vector2(_slotSize, _slotSize), bg);
                    label.fontStyle = FontStyle.Bold;
                }
            }

            _caption = NewText("설명", "", new Vector2(0f, -_slotSize * 0.85f),
                               new Vector2(totalW + 200f, 48f));
            _caption.fontSize = Mathf.RoundToInt(_fontSize * 0.85f);
        }

        private void Refresh()
        {
            for (int i = 0; i < _slotBgs.Count; i++)
                _slotBgs[i].color = (i == _belt.SelectedIndex) ? _selectedColor : _slotColor;

            // 무엇을 들었는지 <b>아래에 쓰는 법 한 줄</b>을 붙인다.
            //
            // 여태 벨트는 "손 : 돋보기"까지만 말했다. 그러면 고르는 법은 알아도
            // <b>쓰는 법</b>은 아무 데도 안 적혀 있어서, 도구를 든 채로 서 있게 된다.
            // 도구를 바꾼 바로 그 자리가 일러 주기 가장 좋은 자리다.
            string how = Controls.HowTo(IdOf(_belt.SelectedIndex));
            if (_caption != null)
                _caption.text = $"손 : {_belt.SlotName(_belt.SelectedIndex)}"
                              + (string.IsNullOrEmpty(how) ? "" : "\n" + how);

            // 바뀌었으니 다시 떠오른다. 도구를 새로 받았을 때도 여기를 지나므로,
            // 방금 익힌 것이 벨트에 들어가 앉는 것을 눈으로 보게 된다.
            // 쓰는 법이 붙은 도구는 <b>읽을 시간</b>이 든다.
            _linger = string.IsNullOrEmpty(how) ? LingerSeconds : LingerSeconds * 2f;
        }

        // ── UI 만들기 헬퍼 ──

        private RectTransform NewChild(string name, Vector2 pos, Vector2 size, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent != null ? parent : transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private Text NewText(string name, string content, Vector2 pos, Vector2 size, Transform parent = null)
        {
            var rt = NewChild(name, pos, size, parent);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = _font;
            txt.fontSize = _fontSize;
            txt.color = _textColor;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            txt.text = content;
            return txt;
        }

        private static string First(string s) => string.IsNullOrEmpty(s) ? "?" : s.Substring(0, 1);

        /// <summary>이 칸의 도구 id("" = 맨손).</summary>
        private string IdOf(int i)
            => (i <= 0 || i > _belt.Tools.Count || _belt.Tools[i - 1] == null) ? "" : _belt.Tools[i - 1].id;
    }
}
