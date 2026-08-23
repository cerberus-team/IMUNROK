using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 사건을 고를 때 눈앞에 <b>풀려 내려오는 봉서</b>. 제목과 요지를 보이고,
    /// 처음부터 갈지 하던 데부터 갈지 묻는다.
    ///
    /// <b>왜 한 겹 두나</b>: 여태는 문서를 누르면 곧바로 씬이 갈렸다. 그러면 잘못
    /// 누른 사람에게 되돌릴 틈이 없고, 무엇보다 <b>이미 하던 사건</b>인지 처음 여는
    /// 사건인지 고를 데가 없다. 사건에 들어가는 것은 되돌리기 어려운 일이라 한 번은
    /// 물어야 한다.
    ///
    /// <b>왜 두루마리로 여나</b>: 조사청의 물건은 죄다 종이다 — 봉서·수첩·조사종이.
    /// 고르는 창만 네모난 판으로 뜨면 그것만 게임 밖에서 온 물건이 된다. 위축에서
    /// 종이가 <b>아래로 풀리며</b> 열리고, 아래축이 그 끝을 따라 내려온다.
    /// 글은 다 풀린 뒤에 배어 나온다 — 종이가 내려오는 동안 글자가 같이 늘어나면
    /// 고무처럼 보인다(<see cref="ScrollUnroll"/> 이 3D 에서 겪은 것과 같은 이치다).
    ///
    /// 씬에 미리 둘 필요 없다. 처음 열 때 스스로 만들어진다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class CaseChoicePanel : MonoBehaviour
    {
        [Header("모양")]
        [SerializeField] private Font _font;
        [SerializeField] private Color _paperColor = new Color(0.91f, 0.87f, 0.76f, 0.98f);
        [SerializeField] private Color _inkColor = new Color(0.15f, 0.10f, 0.06f);
        [SerializeField] private Color _rodColor = new Color(0.33f, 0.22f, 0.12f);
        [SerializeField] private Color _cordColor = new Color(0.58f, 0.10f, 0.09f);
        [SerializeField] private Color _labelColor = new Color(0.98f, 0.94f, 0.86f);

        [Header("풀리기")]
        [Tooltip("종이가 다 풀리는 데 걸리는 시간(초)")]
        [SerializeField] private float _unrollSeconds = 0.85f;
        [Tooltip("글이 배어 나오기 시작하는 지점(0~1). 종이가 이만큼 풀린 뒤부터")]
        [SerializeField] private float _inkFrom = 0.55f;

        private const float PageW = 1180f, PageH = 900f, RodH = 34f;

        private static CaseChoicePanel _instance;

        private CanvasGroup _group;
        private WorldHudAnchor _anchor;
        private RectTransform _paper, _bottomRod, _body;
        private CanvasGroup _bodyGroup;
        private Text _title, _sub, _brief;
        private Button _fresh, _cont, _back;
        private Text _freshLabel, _contLabel;
        private Coroutine _run;

        /// <summary>지금 고르는 창이 떠 있나. 다른 것이 끼어들지 않게 참고한다.</summary>
        public static bool IsOpen => _instance != null && _instance._group != null && _instance._group.alpha > 0.5f;

        /// <summary>
        /// 창을 편다.
        /// </summary>
        /// <param name="title">제1사건 같은 차례</param>
        /// <param name="subtitle">사건 이름</param>
        /// <param name="brief">요지 두어 줄</param>
        /// <param name="hasProgress">하던 것이 있나 — 없으면 이어하기 단추를 안 보인다</param>
        public static void Open(string title, string subtitle, string brief, bool hasProgress,
                                Action onFresh, Action onContinue)
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CaseChoicePanel>();
                if (_instance == null)
                {
                    var go = new GameObject("VR_사건고르기", typeof(Canvas));
                    go.AddComponent<WorldHudAnchor>().Configure(WorldHudAnchor.Placement.Front);
                    _instance = go.AddComponent<CaseChoicePanel>();
                }
            }
            _instance.OpenInternal(title, subtitle, brief, hasProgress, onFresh, onContinue);
        }

        public static void Close()
        {
            if (_instance != null) _instance.SetVisible(false);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            EnsureBuilt();
            SetVisible(false);
        }

        /// <summary>
        /// 뼈대를 한 번 세운다. 두 번 불러도 한 번만 선다.
        ///
        /// <b>Awake 에 두지 않는 까닭</b>: 편집 중에는 Awake 가 돌지 않는다.
        /// 도구로 창을 세워 눈맞춤하려 하면 안쪽이 죄다 비어 있어 곧바로 터진다.
        /// 세우는 일을 여는 쪽에서도 부를 수 있게 빼 둔다.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_title != null) return;

            _anchor = GetComponent<WorldHudAnchor>();
            if (_anchor == null) _anchor = gameObject.AddComponent<WorldHudAnchor>();
            _anchor.SetDistance(0.90f, -0.10f);
            _anchor.SetStowable(false);

            _font = UiFont.Resolve(_font);
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            Build();
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        // ── 뼈대(한 번만) ──

        private void Build()
        {
            // 위축 — 여기서 종이가 풀려 내려온다. 자리가 고정이라 축이 매달린 것으로 보인다.
            var top = NewRect("위축", new Vector2(0f, PageH * 0.5f), new Vector2(PageW + 90f, RodH), transform);
            top.gameObject.AddComponent<Image>().color = _rodColor;
            Knob(top, -(PageW + 90f) * 0.5f);
            Knob(top, +(PageW + 90f) * 0.5f);

            // 종이 — 위쪽에 매달아 두고 세로만 키운다(pivot 이 위여야 아래로 풀린다)
            _paper = NewRect("종이", new Vector2(0f, PageH * 0.5f - RodH * 0.5f), new Vector2(PageW, 0f), transform);
            _paper.pivot = new Vector2(0.5f, 1f);
            _paper.gameObject.AddComponent<Image>().color = _paperColor;

            // 아래축 — 종이 끝을 따라 내려온다
            _bottomRod = NewRect("아래축", new Vector2(0f, PageH * 0.5f - RodH), new Vector2(PageW + 70f, RodH), transform);
            _bottomRod.gameObject.AddComponent<Image>().color = _rodColor;
            Knob(_bottomRod, -(PageW + 70f) * 0.5f);
            Knob(_bottomRod, +(PageW + 70f) * 0.5f);

            // 글과 단추는 따로 묶어 두고 나중에 배어 나오게 한다
            _body = NewRect("글", Vector2.zero, new Vector2(PageW, PageH), transform);
            _bodyGroup = _body.gameObject.AddComponent<CanvasGroup>();
            _bodyGroup.alpha = 0f;

            _title = NewText("차례", "", new Vector2(0f, PageH * 0.5f - 150f), new Vector2(PageW - 120f, 70f),
                             _body, 46, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.75f));
            _sub = NewText("이름", "", new Vector2(0f, PageH * 0.5f - 228f), new Vector2(PageW - 120f, 90f),
                           _body, 62, _inkColor);

            var rule = NewRect("가는줄", new Vector2(0f, PageH * 0.5f - 292f), new Vector2(360f, 3f), _body);
            rule.gameObject.AddComponent<Image>().color = new Color(_cordColor.r, _cordColor.g, _cordColor.b, 0.65f);

            _brief = NewText("요지", "", new Vector2(0f, 40f), new Vector2(PageW - 220f, 300f),
                             _body, 34, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.90f));
            _brief.horizontalOverflow = HorizontalWrapMode.Wrap;
            _brief.verticalOverflow = VerticalWrapMode.Truncate;

            // 단추 셋 — 왼쪽부터 처음부터 · 이어하기 · 물러나기
            _fresh = MakeButton("처음부터", new Vector2(-330f, -PageH * 0.5f + 150f), _cordColor, out _freshLabel);
            _cont = MakeButton("이어하기", new Vector2(0f, -PageH * 0.5f + 150f), new Color(0.24f, 0.22f, 0.18f, 0.94f), out _contLabel);
            Text backLabel;
            _back = MakeButton("물러나기", new Vector2(330f, -PageH * 0.5f + 150f), new Color(0.30f, 0.28f, 0.24f, 0.75f), out backLabel);
            _back.onClick.AddListener(Close);
        }

        /// <summary>축머리 — 축 양 끝에 박힌 마디. 이것이 있어야 막대가 아니라 축으로 보인다.</summary>
        private void Knob(RectTransform rod, float x)
        {
            var k = NewRect("축머리", new Vector2(x, 0f), new Vector2(26f, RodH + 16f), rod);
            k.gameObject.AddComponent<Image>().color = new Color(_rodColor.r * 0.75f, _rodColor.g * 0.75f, _rodColor.b * 0.75f);
        }

        private Button MakeButton(string label, Vector2 pos, Color bg, out Text text)
        {
            var rt = NewRect(label, pos, new Vector2(280f, 84f), _body);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            text = NewText("라벨", label, Vector2.zero, new Vector2(280f, 84f), rt, 36, _labelColor);
            return b;
        }

        // ── 열기 ──

        private void OpenInternal(string title, string subtitle, string brief, bool hasProgress,
                                  Action onFresh, Action onContinue)
        {
            EnsureBuilt();
            _title.text = title ?? "";
            _sub.text = subtitle ?? "";
            _brief.text = brief ?? "";

            _fresh.onClick.RemoveAllListeners();
            _cont.onClick.RemoveAllListeners();
            _fresh.onClick.AddListener(delegate { Close(); if (onFresh != null) onFresh(); });
            _cont.onClick.AddListener(delegate { Close(); if (onContinue != null) onContinue(); });

            // 하던 것이 없으면 이어할 것도 없다. 눌리지 않는 단추를 보여 주면
            // 눌러 보고 나서야 안 된다는 것을 알게 된다 — 아예 치운다.
            _cont.gameObject.SetActive(hasProgress);
            _fresh.gameObject.SetActive(true);
            _freshLabel.text = hasProgress ? "처음부터" : "봉서를 펴다";

            SetVisible(true);
            if (_anchor != null) _anchor.Recenter();

            // 편집 중에는 코루틴이 돌지 않는다(유니티가 막는다). 그럴 때는 다 풀린
            // 모양으로 곧장 세운다 — 도구로 눈맞춤할 때 창이 안 보이면 맞출 수가 없다.
            if (Application.isPlaying)
            {
                if (_run != null) StopCoroutine(_run);
                _run = StartCoroutine(Unroll());
            }
            else ApplyUnroll(1f);
        }

        /// <summary>위축에서 종이가 아래로 풀린다. 다 풀린 뒤에야 글이 배어 나온다.</summary>
        private IEnumerator Unroll()
        {
            float t = 0f;
            _bodyGroup.alpha = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.05f, _unrollSeconds);
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);   // 처음 빠르고 끝에서 눕는다
                float h = PageH * e;

                ApplyUnroll(e);
                yield return null;
            }
            ApplyUnroll(1f);
            _run = null;
        }

        /// <summary>풀린 정도(0~1)를 종이·아래축·글에 그대로 옮긴다.</summary>
        private void ApplyUnroll(float e)
        {
            float h = PageH * Mathf.Clamp01(e);
            _paper.sizeDelta = new Vector2(PageW, h);
            _bottomRod.anchoredPosition = new Vector2(0f, PageH * 0.5f - RodH - h + RodH * 0.5f);
            _bodyGroup.alpha = e >= 1f ? 1f : (e > _inkFrom ? Mathf.InverseLerp(_inkFrom, 1f, e) : 0f);
        }

        private void SetVisible(bool on)
        {
            if (_group == null) return;
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
            _group.interactable = on;
            if (!on && _run != null) { StopCoroutine(_run); _run = null; }
        }

        // ── UI 헬퍼(수첩과 같은 꼴) ──

        private RectTransform NewRect(string name, Vector2 pos, Vector2 size, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private Text NewText(string name, string content, Vector2 pos, Vector2 size,
                             Transform parent, int fontSize, Color color)
        {
            var rt = NewRect(name, pos, size, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = content;
            return t;
        }
    }
}
