using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// 심문의 조작부(마이크 · 추천 질문 · 닫기)를 월드 공간 Canvas로 그린다.
    /// 대사 자체는 <see cref="SubtitleView"/>가 맡고, 여기는 "누를 것"만 담당한다.
    ///
    /// OnGUI로는 헤드셋에 아무것도 안 뜨므로, 이게 없으면 VR에서 심문을 시작하거나
    /// 끝낼 방법이 없다.
    ///
    /// 씬에 미리 둘 필요 없다 — <see cref="InterrogationController"/>가 심문을 시작할 때
    /// 스스로 만든다. 자막 바로 아래에 붙어 함께 따라다닌다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class InterrogationPanel : MonoBehaviour
    {
        [SerializeField] private Font _font;
        [SerializeField] private int _fontSize = 30;
        [SerializeField] private Color _chipColor = new Color(0.06f, 0.06f, 0.07f, 0.85f);
        [SerializeField] private Color _micColor = new Color(0.20f, 0.35f, 0.28f, 0.9f);
        [SerializeField] private Color _micOnColor = new Color(0.72f, 0.20f, 0.16f, 0.95f);
        [SerializeField] private Color _closeColor = new Color(0.28f, 0.10f, 0.09f, 0.9f);
        [Tooltip("되돌릴 수 없는 '마치기' 버튼. 잠시 멈추기와 눈에 띄게 달라야 한다")]
        [SerializeField] private Color _endColor = new Color(0.42f, 0.30f, 0.10f, 0.95f);
        [SerializeField] private Color _textColor = new Color(0.98f, 0.96f, 0.92f);

        private static InterrogationPanel _instance;

        private InterrogationController _owner;
        private CanvasGroup _group;
        private WorldHudAnchor _anchor;
        private Image _micBg;
        private Text _micLabel;
        private readonly List<GameObject> _chips = new List<GameObject>();
        private RectTransform _chipRow;
        private RawImage _evidence;
        private float _evidenceTimer;

        /// <summary>심문이 시작될 때 호출 — 없으면 만들고, 이 심문에 맞춰 버튼을 다시 만든다.</summary>
        public static void Open(InterrogationController owner)
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<InterrogationPanel>();
                if (_instance == null)
                {
                    var go = new GameObject("VR_심문조작", typeof(Canvas));
                    // 자막(-0.28)보다 아래 — 대사를 가리지 않게
                    go.AddComponent<WorldHudAnchor>().Configure(WorldHudAnchor.Placement.Front);
                    _instance = go.AddComponent<InterrogationPanel>();
                }
            }
            _instance.OpenInternal(owner);
        }

        /// <summary>제시한 증거 그림을 잠깐 띄운다("탁" 들이미는 연출).</summary>
        public static void ShowEvidence(Texture2D tex, float seconds)
        {
            if (_instance == null || tex == null) return;
            _instance._evidence.texture = tex;
            _instance._evidenceTimer = seconds;
        }

        /// <summary>심문이 끝날 때 호출.</summary>
        /// <summary>이 인물보다 앞에 서게 한다(상대 몸에 가리지 않게).</summary>
        public static void KeepInFrontOf(Transform target)
        {
            var p = _instance;
            if (p != null && p._anchor != null) p._anchor.KeepInFrontOf(target);
        }

        public static void Close()
        {
            if (_instance != null) _instance.SetVisible(false);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _anchor = GetComponent<WorldHudAnchor>();
            if (_anchor == null) _anchor = gameObject.AddComponent<WorldHudAnchor>();
            _anchor.SetDistance(1.3f, -0.62f);   // 자막 바로 아래

            _font = UiFont.Resolve(_font);
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            BuildFixedParts();
            SetVisible(false);
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void OpenInternal(InterrogationController owner)
        {
            _owner = owner;
            RebuildChips(owner != null ? owner.Topics : null);
            SetVisible(true);
            if (_anchor != null) _anchor.Recenter();
        }

        private void SetVisible(bool on)
        {
            if (_group == null) return;
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
            _group.interactable = on;
        }

        private void Update()
        {
            // 듣는 중이면 마이크 버튼을 붉게 — 말해도 되는지 한눈에 보이게
            var mic = MicInput.Instance;
            bool listening = mic != null && mic.IsListening;
            if (_micBg != null) _micBg.color = listening ? _micOnColor : _micColor;
            if (_micLabel != null) _micLabel.text = listening ? "● 듣는 중 (다시 눌러 끝내기)" : "🎤 눌러서 말하기";

            if (_evidence != null)
            {
                bool show = _evidenceTimer > 0f;
                if (show) _evidenceTimer -= Time.deltaTime;
                if (_evidence.gameObject.activeSelf != show) _evidence.gameObject.SetActive(show);
                if (show) _evidence.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_evidenceTimer));   // 마지막 1초 페이드
            }
        }

        // ── 만들기 ──

        private void BuildFixedParts()
        {
            // 마이크 — 가장 크게. VR에서 주된 입력 수단이다.
            var micRt = NewRect("마이크", new Vector2(-330f, 60f), new Vector2(460f, 96f), transform);
            _micBg = micRt.gameObject.AddComponent<Image>();
            _micBg.color = _micColor;
            var micBtn = micRt.gameObject.AddComponent<Button>();
            micBtn.targetGraphic = _micBg;
            micBtn.onClick.AddListener(() => MicInput.Instance?.Toggle());
            _micLabel = NewText("라벨", "🎤 눌러서 말하기", Vector2.zero, new Vector2(460f, 96f), micRt, _fontSize - 2);

            // 잠시 멈추기 — 창만 닫는다. 다시 말을 걸면 이어진다.
            var closeRt = NewRect("닫기", new Vector2(90f, 60f), new Vector2(260f, 96f), transform);
            var closeBg = closeRt.gameObject.AddComponent<Image>();
            closeBg.color = _closeColor;
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => _owner?.CloseFromUi());
            NewText("라벨", "✕ 잠시 멈추다", Vector2.zero, new Vector2(260f, 96f), closeRt, _fontSize - 4);

            // 마치기 — 이건 되돌릴 수 없다. 상대가 자리를 뜬다.
            //
            // 예전엔 버튼이 하나였다. 잠깐 창을 치우려고 누른 것과 "볼일이 끝났소" 하고
            // 일어서는 것이 같은 버튼이면, 손이 미끄러진 한 번으로 심문이 영영 끝난다.
            // 되돌릴 수 없는 것은 따로 떼어 놓고, 색도 달리 한다.
            var endRt = NewRect("마치기", new Vector2(400f, 60f), new Vector2(300f, 96f), transform);
            var endBg = endRt.gameObject.AddComponent<Image>();
            endBg.color = _endColor;
            var endBtn = endRt.gameObject.AddComponent<Button>();
            endBtn.targetGraphic = endBg;
            endBtn.onClick.AddListener(() => _owner?.FinishFromUi());
            NewText("라벨", "이만 마치겠소", Vector2.zero, new Vector2(300f, 96f), endRt, _fontSize - 4);

            // 추천 질문이 들어갈 줄
            _chipRow = NewRect("질문줄", new Vector2(0f, -60f), new Vector2(1200f, 90f), transform);

            // 제시한 증거 그림 — 평소엔 꺼져 있다가 잠깐 뜬다
            var evRt = NewRect("증거그림", new Vector2(0f, 300f), new Vector2(420f, 300f), transform);
            _evidence = evRt.gameObject.AddComponent<RawImage>();
            _evidence.raycastTarget = false;
            evRt.gameObject.SetActive(false);
        }

        private void RebuildChips(IReadOnlyList<TopicQuestion> topics)
        {
            foreach (var c in _chips) if (c != null) Destroy(c);
            _chips.Clear();
            if (topics == null || topics.Count == 0) return;

            int n = topics.Count;
            const float gap = 16f;
            float w = Mathf.Min(380f, (1200f - gap * (n - 1)) / n);
            float total = n * w + gap * (n - 1);
            float x0 = -total * 0.5f + w * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var t = topics[i];
                if (t == null) continue;
                var rt = NewRect($"질문{i}", new Vector2(x0 + i * (w + gap), 0f), new Vector2(w, 84f), _chipRow);
                var bg = rt.gameObject.AddComponent<Image>();
                bg.color = _chipColor;
                var btn = rt.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                var captured = t;
                btn.onClick.AddListener(() => _owner?.AskTopicFromUi(captured));
                var label = NewText("라벨", t.question, Vector2.zero, new Vector2(w - 24f, 84f), rt, _fontSize - 4);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                _chips.Add(rt.gameObject);
            }
        }

        // ── UI 헬퍼 ──

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

        private Text NewText(string name, string content, Vector2 pos, Vector2 size, Transform parent, int size2)
        {
            var rt = NewRect(name, pos, size, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size2;
            t.color = _textColor;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = content;
            return t;
        }
    }
}
