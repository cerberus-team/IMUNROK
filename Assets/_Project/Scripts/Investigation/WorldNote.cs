using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 물건 <b>바로 위에</b> 잠깐 떠오르는 한 줄.
    ///
    /// 조사하다 무엇을 짚었을 때 화면 한복판에 커다란 창이 열리면, 방을 보던 눈이
    /// 글자판으로 끌려간다. 그러나 짚은 순간에 알아야 할 것은 많지 않다 — 이것이
    /// 무엇인가, 그뿐이다. 자세한 것은 나중에 수첩을 펴서 읽으면 된다.
    ///
    /// 그래서 여기서는 물건 위에 <b>물건 크기만 한</b> 글자를 잠깐 띄운다.
    /// 눈은 여전히 그 물건을 보고 있고, 방도 그대로 있다.
    ///
    /// 씬에 미리 둘 필요 없다 — 처음 부를 때 스스로 만든다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class WorldNote : MonoBehaviour
    {
        [SerializeField] private Font _font;
        [SerializeField] private int _fontSize = 40;
        [Tooltip("물건 꼭대기에서 이만큼 위(m)")]
        [SerializeField] private float _lift = 0.14f;
        [Tooltip("이만큼(초) 떠 있다가 스러진다")]
        [SerializeField] private float _seconds = 3.2f;
        [SerializeField] private Color _inkColor = new Color(1f, 0.94f, 0.82f);
        [SerializeField] private Color _plateColor = new Color(0.05f, 0.05f, 0.06f, 0.72f);

        private static WorldNote _instance;

        private Canvas _canvas;
        private CanvasGroup _group;
        private Text _text;
        private Image _plate;
        private RectTransform _rect;
        private Transform _target;
        private Vector3 _worldTop;
        private float _left;

        /// <summary>이 물건 위에 한 줄 띄운다.</summary>
        public static void Show(Transform target, string line)
        {
            if (target == null || string.IsNullOrEmpty(line)) return;
            Ensure();
            _instance.ShowInternal(target, line);
        }

        /// <summary>지금 떠 있는 것을 곧바로 거둔다.</summary>
        public static void Hide()
        {
            if (_instance != null) _instance._left = 0f;
        }

        private static void Ensure()
        {
            if (_instance != null) return;
            _instance = FindFirstObjectByType<WorldNote>();
            if (_instance != null) return;
            var go = new GameObject("VR_물건글", typeof(Canvas));
            _instance = go.AddComponent<WorldNote>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            _canvas = GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _rect = (RectTransform)transform;
            _rect.sizeDelta = new Vector2(700f, 120f);
            _rect.localScale = Vector3.one * 0.0009f;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            _font = UiFont.Resolve(_font);

            var plateGo = new GameObject("바탕", typeof(RectTransform));
            plateGo.transform.SetParent(transform, false);
            var prt = (RectTransform)plateGo.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(700f, 96f);
            _plate = plateGo.AddComponent<Image>();
            _plate.color = _plateColor;
            _plate.raycastTarget = false;

            var textGo = new GameObject("글", typeof(RectTransform));
            textGo.transform.SetParent(transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(660f, 96f);
            _text = textGo.AddComponent<Text>();
            _text.font = _font;
            _text.fontSize = _fontSize;
            _text.color = _inkColor;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.raycastTarget = false;
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void ShowInternal(Transform target, string line)
        {
            _target = target;
            _text.text = Emphasis.Rich(line, Emphasis.OnDark);
            _left = _seconds;
            _worldTop = TopOf(target);
            Place(true);
        }

        private void LateUpdate()
        {
            if (_left <= 0f)
            {
                if (_group.alpha > 0f) _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, 4f * Time.deltaTime);
                return;
            }
            _left -= Time.deltaTime;
            _group.alpha = _left < 0.6f ? Mathf.Clamp01(_left / 0.6f) : 1f;
            if (_target != null) _worldTop = TopOf(_target);
            Place(false);
        }

        /// <summary>글자는 늘 눈을 마주 본다. 물건 위에 얹히되 그 물건에 묻히지 않게 조금 띄운다.</summary>
        private void Place(bool instant)
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 pos = _worldTop + Vector3.up * _lift;
            transform.position = instant ? pos : Vector3.Lerp(transform.position, pos, 0.5f);
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);

            // 멀어지면 글자도 작아진다 — 그러면 방 저쪽 것이 코앞 것처럼 커 보이지 않는다.
            float d = Vector3.Distance(cam.transform.position, transform.position);
            _rect.localScale = Vector3.one * Mathf.Clamp(0.0009f * d, 0.0006f, 0.0022f);
        }

        private static Vector3 TopOf(Transform t)
        {
            bool any = false;
            var acc = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                if (!any) { acc = r.bounds; any = true; }
                else acc.Encapsulate(r.bounds);
            }
            if (!any) return t.position;
            return new Vector3(acc.center.x, acc.max.y, acc.center.z);
        }
    }
}
